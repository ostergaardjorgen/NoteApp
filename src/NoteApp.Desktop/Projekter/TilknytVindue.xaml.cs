using System.ComponentModel;
using System.Windows;
using NoteApp.Core;

namespace NoteApp.Desktop.Projekter;

/// <summary>
/// Vælger, hvad projektet skal pege på.
/// </summary>
/// <remarks>
/// Der kopieres ingenting. Vinduet sætter medlemskaber: projektet husker, at
/// et møde hører med, og mødet bliver liggende under Optagelser. Se
/// <see cref="Projekt"/> for hvorfor det er den eneste vej, der holder.
/// </remarks>
public partial class TilknytVindue : Window
{
    /// <summary>Én linje, man kan sætte hak ved.</summary>
    public sealed class Valgvisning : INotifyPropertyChanged
    {
        public required Medlemsslags Slags { get; init; }
        public required string Id { get; init; }
        public required string Navn { get; init; }
        public required string Linje { get; init; }

        private bool _valgt;

        public bool Valgt
        {
            get => _valgt;
            set
            {
                if (_valgt == value) return;
                _valgt = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Valgt)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly List<Valgvisning> _optagelser = new();
    private readonly List<Valgvisning> _noter = new();

    /// <summary>Det valgte, når vinduet lukkes med «Tilknyt».</summary>
    public List<Projektmedlem>? Resultat { get; private set; }

    public TilknytVindue(Projekt projekt)
    {
        InitializeComponent();

        foreach (var m in MeetingStore.Alle().OrderByDescending(m => m.StartedAt))
        {
            var id = m.Id.ToString("N");

            _optagelser.Add(new Valgvisning
            {
                Slags = Medlemsslags.Optagelse,
                Id = id,
                Navn = string.IsNullOrWhiteSpace(m.Title) ? "Uden navn" : m.Title!,
                Linje = m.StartedAt.ToLocalTime().ToString("d. MMMM yyyy · HH:mm", Sprog.Kultur),
                Valgt = projekt.Har(Medlemsslags.Optagelse, id),
            });
        }

        foreach (var n in Diktatnoter.Laes().OrderByDescending(n => n.Tid))
        {
            // NOTEN HAR INTET ID. Tidspunktet er det eneste, der skiller to
            // noter ad, og det skrives i det format, der kan laeses tilbage
            // uanset sprog og tidszone.
            var id = n.Tid.ToString("o");

            _noter.Add(new Valgvisning
            {
                Slags = Medlemsslags.Note,
                Id = id,
                Navn = n.Overskrift,
                Linje = n.Tid.ToString("d. MMMM yyyy · HH:mm", Sprog.Kultur),
                Valgt = projekt.Har(Medlemsslags.Note, id),
            });
        }

        Optagelser.ItemsSource = _optagelser;
        Noter.ItemsSource = _noter;

        IngenOptagelser.Visibility = _optagelser.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        IngenNoter.Visibility = _noter.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Tilknyt_Klik(object sender, RoutedEventArgs e)
    {
        // HELE LISTEN SKRIVES. Saa er «fjern» og «tilfoej» den samme
        // handling, og der er ingen tredje tilstand, hvor skaermen og filen
        // er uenige.
        Resultat = _optagelser.Concat(_noter)
            .Where(v => v.Valgt)
            .Select(v => new Projektmedlem(v.Slags, v.Id))
            .ToList();

        DialogResult = true;
    }

    private void Annuller_Klik(object sender, RoutedEventArgs e) => DialogResult = false;
}
