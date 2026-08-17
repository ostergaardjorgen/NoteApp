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
        Kilde.Text = $"Bygges på «{optagelsesTitel}». Dokumentet gemmes som et Word-dokument (.docx).";

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

        // MODELLEN VAELGES AUTOMATISK, OG SAA SKAL DEN IKKE STAA DER.
        //
        // Her stod "Sprogmodel: Qwen3-8B-Q4_K_M. Tager typisk to til fire
        // minutter." hver gang. Det ligner et valg, brugeren skal traeffe, og
        // det er det ikke - appen vaelger selv. En oplysning, man ikke kan
        // handle paa, er stoej i en dialog, man aabner ti gange om ugen.
        //
        // Linjen bliver derfor kun til noget, naar der ER noget at handle paa:
        // modellen mangler, eller skabelonen oenskede en anden.
        var problem = ModelSti.Length == 0
            ? "Der er ingen sprogmodel hentet. Hent en under «AI-modeller», eller lav referatet i Europa."
            : model is null && ønsket is not null
                ? $"Skabelonen foretrækker «{ønsket}», som ikke er hentet. Bruger {Path.GetFileNameWithoutExtension(ModelSti)} i stedet."
                : "";

        ModelTekst.Text = problem;
        ModelTekst.Visibility = problem.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

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
        SkySaetOp.Visibility = harNoegle ? Visibility.Collapsed : Visibility.Visible;

        if (harNoegle) return;

        // Fjernes noeglen midt i det hele, maa valget ikke blive haengende.
        SkyHak.IsChecked = false;
        SkyValgt = null;
        OpretKnap.Content = "Opret dokument";
    }

    private void SkySaetOp_Click(object sender, RoutedEventArgs e)
    {
        new SkySetupWindow { Owner = this }.ShowDialog();
        VisSkyTilstand();
    }

    /// <summary>
    /// Hakket er sat eller fjernet.
    ///
    /// MODELLEN VÆLGES AF APPEN. Der er målt på begge: Medium 3.5 ramte 101 %
    /// af facits længde mod Large 3's 97 %, og fandt flere navne og tal. At
    /// lade brugeren vælge mellem dem ville være at flytte en beslutning, vi
    /// har målt os frem til, over på en, der ikke har tallene.
    /// </summary>
    private void Sky_Skiftet(object sender, RoutedEventArgs e)
    {
        SkyValgt = SkyHak.IsChecked == true
            ? SkyKatalog.Kendte.FirstOrDefault(m => m.Id == "mistral-medium") ?? SkyKatalog.Kendte[0]
            : null;

        // Knappens tekst skal foelge med. Trykker man "Opret dokument", og der
        // ryger et moede til Frankrig, er det ikke det, knappen lovede. Det er
        // den ENE oplysning, der bliver tilbage i daglig brug - og den staar
        // paa knappen, hvor man ser den uden at laese noget.
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

        // Kravet om en lokal model gaelder kun den lokale vej. Er Europa valgt,
        // er der ingen model paa maskinen at mangle.
        if (ModelSti.Length == 0 && SkyValgt is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler sprogmodel",
                "Der er ingen sprogmodel at lave dokumentet med.\n\n" +
                "Hent en under «AI-modeller», eller sæt hakket ved «Lav referatet i Europa».",
                Dialogs.Slags.Valg);
            return;
        }

        Valgt = t;
        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
