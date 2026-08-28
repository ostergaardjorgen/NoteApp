using System.Xml.Linq;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af App.xaml — kan temaet overhovedet skifte?
///
/// DEN HER FINDES PÅ GRUND AF EN HALV TIME.
///
/// Temaskiftet virkede ikke, og det var ikke til at se hvorfor: farverne var
/// rigtige, valget blev gemt rigtigt, koden kørte. WPF FRYSER SELV RESURSER
/// FRA XAML, og en frossen pensel kan ikke skifte farve. Alle femogtyve var
/// frosne, og koden sprang dem tavst over.
///
/// Løsningen er, at farven kommer gennem en DynamicResource: en Freezable med
/// en uafklaret dynamisk reference kan ikke fryses. Det er en detalje, ingen
/// kan huske om et halvt år — og den ville forsvinde igen, første gang nogen
/// «ryddede op» i App.xaml og skrev farven direkte på penslen.
///
/// Prøven læser App.xaml som XML. Den kører altså på filen, ikke på en kørende
/// app, og kan derfor køre sammen med resten uden en WPF-tråd.
/// </summary>
public sealed class TemafilTest
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Ressourcer()
    {
        // Fra tests/NoteApp.Tests/bin/<konfig>/<mål>/ op til repoets rod.
        var mappe = new DirectoryInfo(AppContext.BaseDirectory);

        while (mappe is not null && !File.Exists(Path.Combine(mappe.FullName, "src",
                   "NoteApp.Desktop", "App.xaml")))
            mappe = mappe.Parent;

        Assert.True(mappe is not null, "Kunne ikke finde App.xaml fra prøvens mappe");

        var sti = Path.Combine(mappe!.FullName, "src", "NoteApp.Desktop", "App.xaml");
        return XDocument.Load(sti).Root!.Elements().First();
    }

    [Fact]
    public void Hver_farve_i_paletten_har_baade_en_farve_og_en_pensel()
    {
        var res = Ressourcer();

        var farver = res.Elements()
            .Where(e => e.Name.LocalName == "Color")
            .Select(e => (string?)e.Attribute(X + "Key"))
            .ToHashSet();

        var pensler = res.Elements()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .ToDictionary(e => (string?)e.Attribute(X + "Key") ?? "",
                          e => (string?)e.Attribute("Color") ?? "");

        foreach (var navn in Tema.Nøglerne)
        {
            Assert.True(pensler.ContainsKey(navn), $"App.xaml mangler penslen «{navn}»");
            Assert.True(farver.Contains(navn + "Farve"), $"App.xaml mangler farven «{navn}Farve»");

            // DET ER DEN HER LINJE, DER ER HELE POINTEN. Staar farven direkte
            // paa penslen, fryser WPF den, og temaet kan ikke skifte.
            Assert.True(pensler[navn] == $"{{DynamicResource {navn}Farve}}",
                $"Penslen «{navn}» har farven skrevet direkte i stedet for som " +
                $"DynamicResource. Saa fryser WPF den, og temaet kan ikke skifte.");
        }
    }

    [Fact]
    public void Farverne_i_App_xaml_svarer_til_den_lyse_palet()
    {
        // App.xaml er udgangspunktet, indtil Temaskift har koert. Staar der
        // noget andet end den lyse palet, ser man et glimt af forkerte farver,
        // foer det foerste vindue er tegnet faerdigt.
        var res = Ressourcer();

        var farver = res.Elements()
            .Where(e => e.Name.LocalName == "Color")
            .ToDictionary(e => (string?)e.Attribute(X + "Key") ?? "", e => e.Value.Trim());

        foreach (var (navn, vaerdi) in Tema.Lys)
            Assert.Equal(vaerdi, farver[navn + "Farve"]);
    }

    [Fact]
    public void Skaermbillederne_haardkoder_ikke_farver()
    {
        // Flagene og slagskyggerne er undtaget: Dannebrog er rødt i begge
        // temaer, og en skygge er sort. Alt andet skal hente sin farve i
        // paletten, ellers staar der en moerk plet i en lys app.
        var rod = new DirectoryInfo(AppContext.BaseDirectory);
        while (rod is not null && !Directory.Exists(Path.Combine(rod.FullName, "src")))
            rod = rod.Parent;

        Assert.True(rod is not null);

        var galt = new List<string>();

        foreach (var fil in Directory.EnumerateFiles(
                     Path.Combine(rod!.FullName, "src", "NoteApp.Desktop"), "*.xaml",
                     SearchOption.AllDirectories))
        {
            var navn = Path.GetFileName(fil);
            if (navn is "App.xaml" or "Flagikon.xaml") continue;

            var nr = 0;
            foreach (var linje in File.ReadLines(fil))
            {
                nr++;

                // En skygge er sort i begge temaer, og en halvgennemsigtig
                // tone lægger sig oven paa det, der er under den.
                if (linje.Contains("DropShadowEffect") || linje.Contains("#33")) continue;

                foreach (var attribut in new[] { "Background=\"#", "Foreground=\"#",
                             "BorderBrush=\"#", "Fill=\"#", "Stroke=\"#" })
                    if (linje.Contains(attribut))
                        galt.Add($"{navn}:{nr}  {linje.Trim()}");
            }
        }

        Assert.True(galt.Count == 0,
            "Disse steder har en farve skrevet direkte i stedet for en palet-nøgle:\n  "
            + string.Join("\n  ", galt));
    }
}
