using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core.Documents;

namespace NoteApp.Desktop.Transcribe;

/// <summary>
/// Dokumenterne, der er lavet ud af ÉN optagelse.
/// </summary>
/// <remarks>
/// Optagelsen og dokumentet er den samme sag set fra to sider, og de lå på
/// hver sin skærm. Ville man vide, hvad der var lavet ud af et møde, skulle
/// man gå til «Dokumenter» og læse «Fra optagelse» på hver enkelt.
/// Sammenhængen var der i data — den var bare ikke noget, man kunne se.
///
/// Opslaget står i <see cref="DocumentStore.ForMoede"/>, så det kan prøves
/// uden en skærm. Se <c>DokumentsammenhaengTest</c>.
/// </remarks>
public partial class Dokumentrude : UserControl
{
    private IReadOnlyList<DocumentInfo> _dokumenter = Array.Empty<DocumentInfo>();

    public Dokumentrude()
    {
        InitializeComponent();
        Vis(null, null, null);
    }

    /// <summary>Hvor mange dokumenter der er lavet ud af den viste optagelse.</summary>
    public int Antal => _dokumenter.Count;

    // HER LAA VisIGang - «Brainstorm er ved at blive oprettet …» oeverst i fanen.
    //
    // Se Dokumentrude.xaml for hvorfor den er vaek: den var den tredje visning af
    // den samme koersel, og den blev aldrig faerdig. Jobbjaelken nederst i vinduet
    // foelger koerslen hele vejen og staar paa alle skaerme.

    /// <summary>
    /// Viser dokumenterne for én optagelse.
    /// </summary>
    /// <remarks>
    /// DER LÆSES FRA DISKEN HVER GANG. En kopi, der kan blive uenig med
    /// virkeligheden, er værre end en langsom læsning — og et dokument, der
    /// lige er blevet færdigt i baggrunden, skal stå her uden en genstart.
    /// Samme valg som i kvitteringerne og i søgningen, og af samme grund.
    /// </remarks>
    public void Vis(string? moedeId, string? mappe, string? titel)
    {
        _moedeId = moedeId;
        _mappe = mappe;
        _titel = titel;

        _dokumenter = DocumentStore.ForMoede(moedeId, mappe);

        Tegn();
    }

    private string? _moedeId;
    private string? _mappe;
    private string? _titel;

    /// <summary>
    /// Bygger listen op — med søgningen lagt ned over, hvis der står noget i
    /// feltet.
    /// </summary>
    /// <remarks>
    /// DEN ER SKILT UD FRA <see cref="Vis"/>, fordi søgningen skal kunne
    /// tegne igen uden at læse fra disken. Læsningen hører til et skift af
    /// optagelse, ikke til hvert tastetryk i søgefeltet.
    /// </remarks>
    private void Tegn()
    {
        if (_mappe is null && _moedeId is null)
        {
            Overskrift.Text = "Dokumenter";
            Underskrift.Text =
                "Vælg en optagelse i træet til venstre. Her står de dokumenter, der er lavet "
                + "ud af netop den — referater, notater og alt andet, mødetyperne kan lave.";

            Liste.ItemsSource = null;
            Soegefelt.IsEnabled = false;
            return;
        }

        Soegefelt.IsEnabled = _dokumenter.Count > 0;

        var navn = string.IsNullOrWhiteSpace(_titel) ? "optagelsen" : $"«{_titel}»";

        if (_dokumenter.Count == 0)
        {
            Overskrift.Text = "Ingen dokumenter endnu";

            // TEKSTEN SIGER, HVAD MAN GOER - ikke bare at der ikke er noget.
            // En tom skaerm, der kun konstaterer, er en blindgyde.
            //
            // HER STOD «tryk «Opret dokument» over udskriften». Knappen sad i
            // den ANDEN fane, saa den tomme skaerm henviste til noget, man
            // skulle skifte fane for at finde. Nu staar knappen oeverst, hvor
            // udskriftsfanen ogsaa har sine.
            Underskrift.Text =
                $"Der er ikke lavet dokumenter ud af {navn} endnu. Skriv optagelsen ud, "
                + "og tryk så «Opret dokument» øverst — så laves et referat eller "
                + "et andet dokument ud fra den mødetype, du vælger.";

            Liste.ItemsSource = null;
            return;
        }

        var soeg = Soeg.Text.Trim();

        var vist = soeg.Length == 0
            ? _dokumenter
            : _dokumenter.Where(d => Rammer(d, soeg)).ToList();

        if (vist.Count == 0)
        {
            // EN TOM SOEGNING SKAL SIGE HVAD DER BLEV SOEGT I. Ellers ligner
            // det, at dokumenterne er vaek.
            Overskrift.Text = "Ingen træffere";
            Underskrift.Text =
                $"Ingen af de {_dokumenter.Count} dokumenter fra {navn} indeholder «{soeg}» — "
                + "hverken i titlen, beskrivelsen eller teksten.";

            Liste.ItemsSource = null;
            return;
        }

        if (soeg.Length > 0)
        {
            Overskrift.Text = vist.Count == 1
                ? "Ét dokument indeholder «" + soeg + "»"
                : $"{vist.Count} dokumenter indeholder «{soeg}»";

            Underskrift.Text = vist.Count == _dokumenter.Count
                ? $"Alle dokumenter fra {navn}. Nyeste først."
                : $"Ud af {_dokumenter.Count} dokumenter fra {navn}. Nyeste først.";
        }
        else
        {
            Overskrift.Text = _dokumenter.Count == 1
                ? "Ét dokument fra denne optagelse"
                : $"{_dokumenter.Count} dokumenter fra denne optagelse";

            Underskrift.Text = $"Lavet ud af {navn}. Nyeste først.";
        }

        Liste.ItemsSource = vist.Select(d =>
        {
            var findes = File.Exists(DocumentStore.Path_(d));
            var tekst = d.Markdown.Trim();

            return new
            {
                d.Id,

                Titel = d.Title.Length > 0 ? d.Title : d.FileName,

                Linje = string.Join("  ·  ", new[]
                {
                    d.Created.LocalDateTime.ToString("dd-MM-yyyy HH:mm"),
                    d.Template,
                    d.Model
                }.Where(s => !string.IsNullOrWhiteSpace(s))),

                Beskrivelse = d.Description,
                BeskrivelseSynlig = d.Description.Length > 0 ? Visibility.Visible : Visibility.Collapsed,

                // TEKSTEN STAAR HER PAA SKAERMEN. Er der ingen gemt - et gammelt
                // dokument fra foer teksten blev gemt med - staar rammen tom i
                // stedet for at vise en tom kasse.
                Indhold = tekst,
                IndholdSynlig = tekst.Length > 0 ? Visibility.Visible : Visibility.Collapsed,

                Findes = findes,
                ManglerSynlig = findes ? Visibility.Collapsed : Visibility.Visible
            };
        }).ToList();
    }

    /// <summary>Rammer søgeordet dokumentet — i titel, beskrivelse, mødetype eller tekst?</summary>
    private static bool Rammer(DocumentInfo d, string soeg) =>
        d.Title.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || d.Description.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || d.Template.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || d.FileName.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || d.Markdown.Contains(soeg, StringComparison.CurrentCultureIgnoreCase);

    private void Soeg_Aendret(object sender, TextChangedEventArgs e)
    {
        SoegPladsholder.Visibility = Soeg.Text.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;

        SoegRyd.Visibility = Soeg.Text.Length == 0
            ? Visibility.Collapsed : Visibility.Visible;

        Tegn();

        // Efter en ny soegning skal man staa i toppen af traefferne og ikke
        // dér, hvor man tilfaeldigvis var rullet hen i den forrige.
        Rulle.ScrollToTop();
    }

    private void Soeg_Tast(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || Soeg.Text.Length == 0) return;

        Soeg.Clear();
        e.Handled = true;
    }

    private void SoegRyd_Klik(object sender, RoutedEventArgs e)
    {
        Soeg.Clear();
        Soeg.Focus();
    }

    private DocumentInfo? Fra(object sender) =>
        sender is Button { Tag: string id }
            ? _dokumenter.FirstOrDefault(d => d.Id == id)
            : null;

    private void Aabn_Klik(object sender, RoutedEventArgs e)
    {
        if (Fra(sender) is not { } d) return;

        var fil = DocumentStore.Path_(d);

        if (!File.Exists(fil))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke åbne",
                "Filen findes ikke længere.", Dialogs.Slags.Valg);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(fil) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne",
                $"Dokumentet kunne ikke åbnes.\n\n{ex.Message}\n\n"
                + "Er der ikke noget program til .docx-filer, kan de åbnes i Word, "
                + "LibreOffice eller Google Docs.", Dialogs.Slags.Valg);
        }
    }

    private void Arkiv_Klik(object sender, RoutedEventArgs e)
    {
        if (Fra(sender) is not { } d) return;

        (Application.Current.MainWindow as MainWindow)?.GaaTilDokumenter(d.Id);
    }

    private void AlleKlik(object sender, RoutedEventArgs e) =>
        (Application.Current.MainWindow as MainWindow)?.GaaTilDokumenter();

    /// <summary>Der blev trykket «Opret dokument» her i ruden.</summary>
    /// <remarks>
    /// SAMME VEJ SOM KNAPPEN I UDSKRIFTSRUDEN. Ruden kender dokumenterne og
    /// ikke optagelsen — hvor lyden ligger, hvad mødet hedder, hvilken mødetype
    /// der blev valgt — så den siger til, og <c>TranscribeView</c> gør arbejdet.
    /// To knapper, ét forløb: en kopi ville skride fra den anden ved første
    /// rettelse.
    /// </remarks>
    public event Action? OpretDokument;

    private void OpretKlik(object sender, RoutedEventArgs e) => OpretDokument?.Invoke();
}
