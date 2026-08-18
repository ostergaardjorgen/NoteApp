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
    private readonly string _optagelse;

    public NewDocumentWindow(string optagelsesTitel, IReadOnlyList<PromptTemplate> skabeloner)
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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler valg", "Vælg en skabelon.", Dialogs.Slags.Valg);
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
