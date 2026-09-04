using System.Text.RegularExpressions;
using System.Xml.Linq;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af XAML-filerne — kan temaet overhovedet skifte?
///
/// DEN HER FINDES PÅ GRUND AF EN FORMIDDAG.
///
/// Temaskiftet virkede ved opstart og ikke, når man trykkede på knappen. Det
/// gik galt to gange på hver sin måde, og begge gange så det rigtigt ud i et
/// skærmbillede fra en opstart:
///
///   1. Penslernes farve blev sat direkte. WPF FRYSER SELV RESURSER FRA XAML,
///      og en frossen pensel er uforanderlig. Alle femogtyve var frosne.
///   2. Farven kom fra en `Color` gennem en DynamicResource, så penslen ikke
///      KUNNE fryses ved indlæsningen. Den blev frosset senere alligevel —
///      første gang den blev brugt i en stil, som WPF forsegler.
///
/// Løsningen er at UDSKIFTE penslen og slå den op med DynamicResource. Det er
/// en detalje, ingen kan huske om et halvt år, og den ville forsvinde igen,
/// første gang nogen skrev «StaticResource Panel» i en ny skærm.
///
/// Prøverne læser filerne som tekst og XML. De kører altså ikke på en kørende
/// app og kræver ingen WPF-tråd.
/// </summary>
public sealed class TemafilTest
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Repoets rod, fundet ved at gå op fra prøvens egen mappe.</summary>
    private static string Rod()
    {
        var mappe = new DirectoryInfo(AppContext.BaseDirectory);

        while (mappe is not null && !File.Exists(Path.Combine(mappe.FullName, "src",
                   "NoteApp.Desktop", "App.xaml")))
            mappe = mappe.Parent;

        Assert.True(mappe is not null, "Kunne ikke finde App.xaml fra prøvens mappe");
        return mappe!.FullName;
    }

    private static IEnumerable<string> Skaermbilleder() =>
        Directory.EnumerateFiles(Path.Combine(Rod(), "src", "NoteApp.Desktop"),
                                 "*.xaml", SearchOption.AllDirectories);

    private static XElement Ressourcer() =>
        XDocument.Load(Path.Combine(Rod(), "src", "NoteApp.Desktop", "App.xaml"))
                 .Root!.Elements().First();

    [Fact]
    public void Hver_farve_i_paletten_findes_som_pensel()
    {
        var pensler = Ressourcer().Elements()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .ToDictionary(e => (string?)e.Attribute(X + "Key") ?? "",
                          e => (string?)e.Attribute("Color") ?? "");

        foreach (var navn in Tema.Nøglerne)
        {
            Assert.True(pensler.ContainsKey(navn), $"App.xaml mangler penslen «{navn}»");

            // App.xaml er udgangspunktet, indtil Temaskift har koert. Staar der
            // noget andet end STANDARDTEMAET, ser man et glimt af den anden
            // palet, foer det foerste vindue er tegnet faerdigt.
            //
            // Standarden staar ét sted: AppSettings.Tema. Skifter den, skal
            // App.xaml med - og saa fejler den her, indtil den goer det.
            var standard = Tema.Palet(lyst: new AppSettings().Tema == Temavalg.Lyst);
            Assert.Equal(standard[navn], pensler[navn]);
        }
    }

    [Fact]
    public void Paletten_slaas_op_med_DynamicResource_overalt()
    {
        // DET ER DEN HER, DER BÆRER TEMASKIFTET.
        //
        // Penslerne bliver UDSKIFTET, ikke ændret — en frossen pensel kan ikke
        // ændres, og WPF fryser dem selv, så snart de bruges i en stil. Et
        // StaticResource-opslag slår op én gang og opdager aldrig, at nøglen
        // peger et andet sted hen.
        //
        // Skriver nogen «StaticResource Panel» i en ny skærm, virker den ved
        // opstart og bliver hængende i det gamle tema, når der skiftes.
        var moenster = new Regex(@"\{StaticResource (" + string.Join("|", Tema.Nøglerne) + @")\}");

        var galt = new List<string>();

        foreach (var fil in Skaermbilleder())
        {
            var nr = 0;
            foreach (var linje in File.ReadLines(fil))
            {
                nr++;
                foreach (Match m in moenster.Matches(linje))
                    galt.Add($"{Path.GetFileName(fil)}:{nr}  {m.Value}");
            }
        }

        Assert.True(galt.Count == 0,
            "Paletten skal slås op med DynamicResource — ellers følger disse steder ikke "
            + "med, når temaet skifter:\n  " + string.Join("\n  ", galt));
    }

    [Fact]
    public void Koden_haardkoder_heller_ikke_farver()
    {
        // ============ FEJLEN, DER KUNNE GEMME SIG I ET HALVT AAR ============
        //
        // Proeven ovenfor laeser XAML. Farver sat i C# var derfor helt fri, og
        // der var syvogtredive af dem — alle sammen valgt til det MOERKE tema
        // og omhyggeligt regnet efter mod netop den flade. Kommentarerne
        // fortalte det endda selv: «elleve pixels tekst paa den moerke pille
        // #2C323D».
        //
        // I det lyse tema var de forkerte. Fristen paa en aabne opgave stod i
        // #7EDCA0 paa et hvidt kort — 1,6:1. Datoen var der, man kunne bare
        // ikke laese den. Fundet paa et skaermbillede 04-09-2026, ikke af en
        // proeve.
        //
        // En farve i koden skal komme fra paletten: Temaskift.Pensel("Navn").
        // Saa gaelder TemaTest ogsaa den, og den kan ikke blive tilbage i det
        // ene tema.
        var farve = new Regex("\"#[0-9A-Fa-f]{6,8}\"");

        var galt = new List<string>();

        var rod = Path.Combine(Rod(), "src");

        foreach (var fil in Directory.EnumerateFiles(rod, "*.cs", SearchOption.AllDirectories))
        {
            var navn = Path.GetFileName(fil);

            // Tema.cs ER paletten. Flagikon tegner Dannebrog, som er roedt i
            // begge temaer.
            if (navn is "Tema.cs" or "Flagikon.xaml.cs") continue;
            if (fil.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

            var nr = 0;
            foreach (var linje in File.ReadLines(fil))
            {
                nr++;
                if (!farve.IsMatch(linje)) continue;

                // En skygge er sort i begge temaer.
                if (linje.Contains("DropShadow") || linje.Contains("#FF000000")) continue;

                galt.Add($"{navn}:{nr}  {linje.Trim()}");
            }
        }

        Assert.True(galt.Count == 0,
            "Disse steder i koden har en farve skrevet direkte. Brug "
            + "Temaskift.Pensel(\"Noegle\") — ellers foelger de ikke med, naar "
            + "temaet skifter:\n  " + string.Join("\n  ", galt));
    }

    [Fact]
    public void Skaermbillederne_haardkoder_ikke_farver()
    {
        // Flagene og slagskyggerne er undtaget: Dannebrog er rødt i begge
        // temaer, og en skygge er sort. Alt andet skal hente sin farve i
        // paletten, ellers står der en mørk plet i en lys app.
        var galt = new List<string>();

        foreach (var fil in Skaermbilleder())
        {
            var navn = Path.GetFileName(fil);
            if (navn is "App.xaml" or "Flagikon.xaml") continue;

            var nr = 0;
            foreach (var linje in File.ReadLines(fil))
            {
                nr++;

                // En skygge er sort i begge temaer, og en halvgennemsigtig tone
                // laegger sig oven paa det, der er under den.
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
