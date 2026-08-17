using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Documents;

/// <summary>
/// Valget før et dokument bliver lavet: hvilken skabelon, hvilken titel, og
/// en beskrivelse man selv skriver.
///
/// Skabelonen fylder mest på skærmen, fordi det er dét, der afgør, hvad der
/// kommer ud — et referat og en opgaveliste af det samme møde er to vidt
/// forskellige dokumenter. Beskrivelsen af hver skabelon står under den, så
/// valget ikke er et gæt.
/// </summary>
public partial class NewDocumentWindow : Window
{
    public PromptTemplate? Valgt { get; private set; }
    public string Titel => FeltTitel.Text.Trim();
    public string Beskrivelse => FeltBeskrivelse.Text.Trim();
    public string ModelSti { get; private set; } = "";

    /// <summary>
    /// Den europæiske model, hvis brugeren har valgt den til. Null betyder
    /// lokalt — og det er forvalget hver eneste gang.
    /// </summary>
    public SkyModel? SkyValgt { get; private set; }

    private readonly IReadOnlyList<string> _modeller;
    private readonly string _optagelse;

    public NewDocumentWindow(string optagelsesTitel, IReadOnlyList<PromptTemplate> skabeloner,
                             IReadOnlyList<string> modeller)
    {
        InitializeComponent();

        _modeller = modeller;
        _optagelse = optagelsesTitel;
        Kilde.Text = $"Bygges på «{optagelsesTitel}». Dokumentet gemmes som .odt og kan åbnes i Word.";

        // Tilvalget vises kun, naar der ER en noegle. Uden en noegle ville et
        // slukket hak vaere en reklame for noget, brugeren ikke kan bruge - og
        // en paamindelse om skyen hver gang man laver et referat.
        SkyBoks.Visibility = Visibility.Visible;
        VisSkyTilstand();

        Skabeloner.ItemsSource = skabeloner;
        if (skabeloner.Count > 0) Skabeloner.SelectedIndex = 0;

        // Titlen saettes af Skabelon_Valgt, som fyrer paa SelectedIndex ovenfor.
        FeltTitel.Focus();
        FeltTitel.SelectAll();
    }

    private void Skabelon_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Skabeloner.SelectedItem is not PromptTemplate t) return;

        // Skabelonens foretrukne model, hvis den er hentet. Bliver der brugt en
        // anden, SKAL det staa — ellers tror man, man fik den, der stod i
        // skabelonen, og undrer sig over resultatet.
        var ønsket = t.PreferredModel;
        var model = _modeller.FirstOrDefault(m =>
            ønsket is not null && Path.GetFileName(m).Contains(ønsket, StringComparison.OrdinalIgnoreCase));

        ModelSti = model ?? (_modeller.Count > 0 ? _modeller[0] : "");

        ModelTekst.Text = ModelSti.Length == 0
            ? "Der er ingen sprogmodel hentet. Hent en under «AI-modeller»."
            : model is not null || ønsket is null
                ? $"Sprogmodel: {Path.GetFileNameWithoutExtension(ModelSti)}. Tager typisk to til fire minutter."
                : $"Sprogmodel: {Path.GetFileNameWithoutExtension(ModelSti)} — skabelonen foretrækker «{ønsket}», som ikke er hentet.";

        // Dokumentets navn er optagelsens navn plus skabelonens. Saadan kan man se
        // i listen, hvad det er, uden at aabne det.
        if (FeltTitel.Text.Length == 0 || Valgt is not null && FeltTitel.Text == $"{_optagelse} — {Valgt.Name}")
            FeltTitel.Text = $"{_optagelse} — {t.Name}";

        Valgt = t;
    }

    /// <summary>
    /// Viser enten hakket eller vejen ind, alt efter om der er en nøgle.
    /// Kaldes igen efter opsætningen, så vinduet ikke skal lukkes og åbnes.
    /// </summary>
    private void VisSkyTilstand()
    {
        var harNoegle = SkyNoegle.Hent() is not null;

        SkyHak.Visibility = harNoegle ? Visibility.Visible : Visibility.Collapsed;
        SkyTekst.Visibility = harNoegle ? Visibility.Visible : Visibility.Collapsed;
        SkySaetOp.Visibility = harNoegle ? Visibility.Collapsed : Visibility.Visible;
        SkySaetOpTekst.Visibility = harNoegle ? Visibility.Collapsed : Visibility.Visible;

        if (harNoegle)
        {
            // Teksten skal staa der fra begyndelsen, ogsaa naar hakket er
            // slukket. Et tomt felt under et slukket hak siger ingenting om,
            // hvad hakket goer.
            Sky_Skiftet(this, new RoutedEventArgs());
        }
        else
        {
            // Fjernes noeglen midt i det hele, maa valget ikke blive haengende.
            SkyHak.IsChecked = false;
            SkyValgt = null;
            OpretKnap.Content = "Opret dokument";
        }
    }

    private void SkySaetOp_Click(object sender, RoutedEventArgs e)
    {
        new SkySetupWindow { Owner = this }.ShowDialog();
        VisSkyTilstand();
    }

    /// <summary>
    /// Hakket er sat eller fjernet. Teksten skal sige præcis, hvad valget
    /// betyder — begge veje.
    ///
    /// Der står IKKE «sikkert» eller «GDPR-godkendt». Det er vurderinger, og
    /// de hører ikke til i en afkrydsningsboks. Der står, hvad der sker: hvad
    /// der sendes, hvorhen, og hvem der er modtager.
    /// </summary>
    private void Sky_Skiftet(object sender, RoutedEventArgs e)
    {
        var model = SkyKatalog.Kendte.FirstOrDefault(m => m.Id == "mistral-medium")
                    ?? SkyKatalog.Kendte[0];

        SkyValgt = SkyHak.IsChecked == true ? model : null;

        SkyTekst.Text = SkyValgt is null
            ? "Referatet laves her på maskinen. Intet forlader din pc."
            : $"Hele mødeudskriften sendes til {model.Leverandoer} i {model.Hjemland} " +
              $"({model.Navn}) og bearbejdes på deres europæiske servere. " +
              "Lyden og optagelsen sendes ikke — kun teksten.\n\n" +
              "Tager typisk under et minut i stedet for en halv time.";

        // Knappens tekst skal foelge med. Trykker man "Opret dokument", og der
        // ryger et moede til Frankrig, er det ikke det, knappen lovede.
        OpretKnap.Content = SkyValgt is null ? "Opret dokument" : "Send og opret dokument";
    }

    private void Opret_Click(object sender, RoutedEventArgs e)
    {
        if (Skabeloner.SelectedItem is not PromptTemplate t)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler valg", "Vælg en skabelon.", Dialogs.Slags.Valg);
            return;
        }

        if (Titel.Length == 0)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler titel", "Dokumentet skal have en titel.", Dialogs.Slags.Valg);
            FeltTitel.Focus();
            return;
        }

        if (ModelSti.Length == 0)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler sprogmodel", "Der er ingen sprogmodel at lave dokumentet med. Hent en under «AI-modeller».", Dialogs.Slags.Valg);
            return;
        }

        Valgt = t;
        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
