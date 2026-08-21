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

    /// <summary>Sproget, dokumentet skal skrives på — «da» eller «en».</summary>
    public string Dokumentsprog => SprogEngelsk.IsChecked == true ? "en" : "da";

    private readonly string _optagelse;

    /// <param name="valgtMoedetype">
    /// Mødetypen, der blev valgt, da optagelsen blev startet. Null når der
    /// ikke blev valgt nogen.
    ///
    /// DEN SKAL VÆRE FORVALGT HER. Hele grunden til at spørge før mødet er, at
    /// svaret skal bruges bagefter — kom man til at vælge forfra i den her
    /// dialog, ville det første valg være et spørgsmål uden virkning, og så
    /// holder folk op med at svare på det.
    /// </param>
    /// <param name="moedesprog">
    /// Sprogkoden på selve optagelsen, hvis den er kendt. Bruges kun til at
    /// sige, hvad valget betyder — er mødet engelsk og dokumentet dansk,
    /// oversætter modellen undervejs, og det skal man vide, før man vælger.
    /// </param>
    public NewDocumentWindow(string optagelsesTitel, IReadOnlyList<PromptTemplate> skabeloner,
                             string? valgtMoedetype = null, string? moedesprog = null)
    {
        InitializeComponent();

        _optagelse = optagelsesTitel;

        // HER LAA ET VALG AF SPROGMODEL, OG FOER DET ET HAK PR. DOKUMENT.
        //
        // Begge dele er vaek. Modellen er den samme hver gang - se
        // SkyKatalog.Standard for hvilken og hvorfor. At lade brugeren vaelge
        // ville flytte en beslutning, vi har maalt os frem til, over paa en,
        // der ikke har tallene.

        // HER STOD "Referatet laves i Europa (Mistral AI)".
        //
        // Det ryger, og ikke kun fordi det fylder. Det kan VILDLEDE: skrevet
        // paa hvert dokument ligner det noget, der varierer fra gang til gang.
        // Det goer det ikke. Loesningen ER lokal transskription efterfulgt af
        // bearbejdning i EU - det er arkitekturen, ikke en egenskab ved det
        // enkelte referat. Man ved det, naar man vaelger loesningen, og faar
        // det bekraeftet i opsaetningen.
        Kilde.Text = $"Skrives af din online AI-model ud fra udskriften af «{optagelsesTitel}» " +
                     "og gemmes som en Word-fil (.docx). Udskriftens tekst sendes til modellen; " +
                     "lyden bliver på maskinen.";

        Skabeloner.ItemsSource = skabeloner;

        // Mødetypen fra optagelsen er forvalgt. Findes den ikke laengere — den
        // kan vaere slettet eller omdoebt — falder valget tilbage paa den
        // foerste, og saa er der stadig noget at trykke paa.
        var nr = valgtMoedetype is null
            ? -1
            : skabeloner.ToList().FindIndex(t =>
                t.Name.Equals(valgtMoedetype, StringComparison.CurrentCultureIgnoreCase));

        if (nr >= 0)
        {
            Skabeloner.SelectedIndex = nr;
            Typehjaelp.Text = $"«{skabeloner[nr].Name}» er valgt, fordi det var mødetypen på optagelsen. " +
                              "Vælg en anden, hvis du vil have noget andet ud af mødet.";
        }
        else if (skabeloner.Count > 0) Skabeloner.SelectedIndex = 0;

        Sproghjaelp.Text = Sprogforklaring(moedesprog);

        SprogDansk.Checked += (_, _) => Sproghjaelp.Text = Sprogforklaring(moedesprog);
        SprogEngelsk.Checked += (_, _) => Sproghjaelp.Text = Sprogforklaring(moedesprog);

        // Titlen saettes af Skabelon_Valgt, som fyrer paa SelectedIndex ovenfor.
        FeltTitel.Focus();
        FeltTitel.SelectAll();
    }

    /// <summary>
    /// Hvad valget betyder — sagt ud fra, hvad optagelsen var på.
    ///
    /// Er de to ens, er der ingenting at forklare, og så står der heller
    /// ingenting. Er de forskellige, oversætter modellen undervejs, og DET er
    /// en oplysning: det er dér, fagudtryk og navne kan skride.
    /// </summary>
    private string Sprogforklaring(string? moedesprog)
    {
        if (moedesprog is null || moedesprog.Length == 0) return "";

        var vaelger = Dokumentsprog;
        if (moedesprog.StartsWith(vaelger, StringComparison.OrdinalIgnoreCase))
            return $"Optagelsen er på {NoteApp.Core.Transcriber.LanguageName(moedesprog).ToLowerInvariant()} — " +
                   "dokumentet bliver skrevet på det samme sprog.";

        return $"Optagelsen er på {NoteApp.Core.Transcriber.LanguageName(moedesprog).ToLowerInvariant()}. " +
               $"Dokumentet bliver oversat til {Sprogregler.Navn(vaelger).ToLowerInvariant()} undervejs. " +
               "Navne, virksomheder og faste fagudtryk beholdes, som de blev sagt.";
    }

    private void Skabelon_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Skabeloner.SelectedItem is not PromptTemplate t) return;

        // HER STOD EN LINJE OM SPROGMODELLEN.
        //
        // "Sprogmodel: Qwen3-8B-Q4_K_M. Tager typisk to til fire minutter."
        // hver gang. Det ligner et valg, brugeren skal traeffe, og det er det
        // ikke. En oplysning, man ikke kan handle paa, er stoej i en dialog,
        // man aabner ti gange om ugen.
        //
        // Skabelonens PreferredModel er der stadig i filformatet, men den
        // peger paa lokale gguf-filer, som ikke bruges laengere. Den laeses
        // ikke.

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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler valg", "Vælg en mødetype.", Dialogs.Slags.Valg);
            return;
        }

        if (Titel.Length == 0)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler titel", "Dokumentet skal have en titel.", Dialogs.Slags.Valg);
            FeltTitel.Focus();
            return;
        }

        // HER STOD ET KRAV OM EN LOKAL SPROGMODEL. Der er ikke laengere en
        // lokal vej at mangle en model til, og noeglen er tjekket, foer
        // dialogen overhovedet aabnes.

        Valgt = t;
        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
