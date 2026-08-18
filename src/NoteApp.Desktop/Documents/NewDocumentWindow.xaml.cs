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
    /// Den europæiske model, når der er tilsluttet en. Null betyder lokalt.
    ///
    /// DET ER IKKE ET VALG I DENNE DIALOG.
    ///
    /// Her stod et hak, man skulle sætte pr. dokument. Det var forkert tænkt:
    /// beslutningen om, hvor referater laves, træffes ÉN gang i opsætningen —
    /// bevidst, med hosting og databehandleraftale foran sig. At stille den
    /// igen ved hvert dokument gør ikke samtykket stærkere; det gør det til
    /// et hak, man sætter uden at læse, og det er det modsatte.
    ///
    /// Er der tilsluttet en europæisk model, bruges den. Er der ikke, laves
    /// referatet på maskinen. Der er ikke noget at spørge om.
    /// </summary>
    public SkyModel? SkyValgt { get; }

    private readonly IReadOnlyList<string> _modeller;
    private readonly string _optagelse;

    public NewDocumentWindow(string optagelsesTitel, IReadOnlyList<PromptTemplate> skabeloner,
                             IReadOnlyList<string> modeller)
    {
        InitializeComponent();

        _modeller = modeller;
        _optagelse = optagelsesTitel;

        // MODELLEN VAELGES AF APPEN - se SkyKatalog.Standard for hvilken og
        // hvorfor. At lade brugeren vaelge ville flytte en beslutning, vi har
        // maalt os frem til, over paa en, der ikke har tallene.
        SkyValgt = SkyNoegle.Hent() is null ? null : SkyKatalog.Standard;

        // HER STOD "Referatet laves i Europa (Mistral AI)".
        //
        // Det ryger, og ikke kun fordi det fylder. Det kan VILDLEDE: skrevet
        // paa hvert dokument ligner det noget, der varierer fra gang til gang.
        // Det goer det ikke. Loesningen ER lokal transskription efterfulgt af
        // bearbejdning i EU - det er arkitekturen, ikke en egenskab ved det
        // enkelte referat. Man ved det, naar man vaelger loesningen, og faar
        // det bekraeftet i opsaetningen.
        Kilde.Text = $"Bygges på «{optagelsesTitel}». Gemmes som Word-dokument (.docx).";

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
