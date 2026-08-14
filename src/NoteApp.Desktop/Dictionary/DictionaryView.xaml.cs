using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Dictionary;

/// <summary>Én rettelse, som den vises i gitteret.</summary>
public sealed class RettelseVisning
{
    public RettelseVisning(Rettelse r)
    {
        Row = r;
        Hørt = r.Hørt;
        Rigtigt = r.Rigtigt;
        Gange = r.Gange;

        Sidst = DateTime.TryParse(r.SidstSet, out var d) ? d.ToString("dd/MM yyyy") : "";
    }

    public Rettelse Row { get; }
    public string Hørt { get; }
    public string Rigtigt { get; }
    public int Gange { get; }
    public string Sidst { get; }
}

/// <summary>
/// Dine rettelser.
///
/// HVAD DER LÅ HER FØR
///
/// Skærmen hed «Din ordbog» og havde en ordliste: man skrev sine fagord og
/// navne ind, valgte kategori og vægt, og listen blev sendt med til Whisper
/// som ledetråd. Det blev målt. Forskellen var NUL — samme lyd gav samme
/// udskrift, med og uden. Og den kunne gøre skade: Whisper skrev ledetråden
/// ind i teksten under pauser, 372 af 388 linjer blev til den samme sætning.
///
/// 41 ord, der ikke gjorde noget, og en brugerflade, der fik det til at se ud,
/// som om appen blev skarpere for hvert ord, man skrev. Det er væk.
///
/// HVAD DER ER TILBAGE
///
/// Rettelserne. De virker målbart, fordi de arbejder på TEKSTEN bagefter og
/// ikke på modellen. Whisper kan ikke trænes — vægtene ligger fast i den fil,
/// der er hentet — men det, den skrev forkert, kan rettes én gang og blive
/// ved med at være rettet.
/// </summary>
public partial class DictionaryView : UserControl
{
    private readonly LearningStore _store = new();

    public DictionaryView()
    {
        InitializeComponent();

        Gitter.SelectionChanged += (_, _) => SletKnap.IsEnabled = Gitter.SelectedItem is not null;
        Unloaded += (_, _) => _store.Dispose();

        RydOpEnGang();
        Indlaes();
    }

    /// <summary>
    /// Rydder de gamle ord op — dem, der ikke bærer nogen rettelse.
    ///
    /// De gør ingenting, men de tæller med i «Termer: 41» og får det til at se
    /// ud, som om der ligger noget derinde, der virker. Ryddes de ikke, står
    /// de tilbage som et spøgelse af en funktion, der er fjernet.
    ///
    /// Det sker én gang og siges i statuslinjen. Der spørges ikke: der er
    /// ikke noget at miste, og et spørgsmål om noget, brugeren ikke længere
    /// kan se, kan ikke besvares meningsfuldt.
    /// </summary>
    private void RydOpEnGang()
    {
        try
        {
            var fjernet = _store.RydOrdliste();
            if (fjernet == 0) return;

            _oprydning = $"{fjernet} gamle ord fra den tidligere ordliste er ryddet væk. " +
                         "De blev sendt med til Whisper som ledetråd, og det er målt til ingen forskel.";
        }
        catch (Exception ex)
        {
            _oprydning = $"Kunne ikke rydde de gamle ord op: {ex.Message}";
        }
    }

    private string _oprydning = "";

    private void Indlaes()
    {
        var rækker = _store.ListRettelser(Soeg.Text)
            .Select(r => new RettelseVisning(r))
            .ToList();

        Gitter.ItemsSource = rækker;
        Antal.Text = rækker.Count == 1 ? "1 rettelse" : $"{rækker.Count} rettelser";

        if (_oprydning.Length > 0)
        {
            Status.Text = _oprydning;
            _oprydning = "";
            return;
        }

        Status.Text = rækker.Count > 0
            ? ""
            : "Der er ingen rettelser endnu. De kommer, når du retter et ord i en udskrift under " +
              "«Optagelser» eller i «Træning» — eller når du taler en ind her.";
    }

    private void Soeg_Changed(object sender, TextChangedEventArgs e)
    {
        if (SoegPladsholder is null) return;

        SoegPladsholder.Visibility = Soeg.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Indlaes();
    }

    /// <summary>
    /// Lær en rettelse ved at TALE ordet ind.
    ///
    /// Det, man ikke kan gætte sig til, er hvordan Whisper hører ordet — og
    /// det er præcis dét, en rettelse skal bruge. Taler man det ind, kommer
    /// den fejlhørte form af sig selv.
    /// </summary>
    private void Tal_Click(object sender, RoutedEventArgs e)
    {
        var vindue = new SpeakWordWindow() { Owner = Window.GetWindow(this) };
        if (vindue.ShowDialog() != true) return;

        // Blev ordet hørt RIGTIGT, er der ikke noget at rette. Før blev det
        // gemt som et ord i ordlisten alligevel — altså gemt som noget, der
        // ikke gjorde noget. Nu siges det, som det er.
        if (!vindue.HarRettelse)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Intet at rette", $"Whisper hørte «{vindue.Stavning}» rigtigt.\n\n" +
                "Så er der ikke noget at rette, og der bliver ikke gemt noget. " +
                "Det er faktisk den bedste udgang — ordet rammer allerede.", Dialogs.Slags.Valg);
            return;
        }

        try
        {
            _store.LearnCorrection(vindue.Forkert, vindue.Stavning, engineId: "indtalt");

            Historik.Skriv(HaendelseType.Rettelser,
                $"Rettelse lært: {vindue.Forkert} → {vindue.Stavning}", "Talt ind");

            Status.Text = $"Lært: «{vindue.Forkert}» rettes til «{vindue.Stavning}» fremover.";
            Indlaes();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke gemme", $"Rettelsen kunne ikke gemmes.\n\n{ex.Message}", Dialogs.Slags.Pas_paa);
        }
    }

    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (Gitter.SelectedItem is not RettelseVisning v) return;

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            "Slå rettelsen fra?",
            $"«{v.Hørt}» bliver ikke længere rettet til «{v.Rigtigt}».\n\n" +
            "Udskrifter, der allerede ER rettet, bliver ikke lavet om.",
            godkend: "Slå fra", annuller: "Behold den");

        if (!ja) return;

        _store.SletRettelse(v.Row.Normalized);
        Status.Text = $"«{v.Hørt}» rettes ikke længere.";
        Indlaes();
    }
}
