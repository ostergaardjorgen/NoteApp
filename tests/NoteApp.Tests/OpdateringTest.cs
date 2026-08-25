using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Hvad der sker med sprog- og hjælpefiler, når appen bliver opdateret.
///
/// HVORFOR DE HER PRØVER FINDES
///
/// Den første udgave sprang over, hvis filen allerede lå i datamappen. Det
/// beskyttede brugerens rettelser — og frøs samtidig sproget og hjælpen fast
/// ved den udgave, appen blev installeret med.
///
/// Det blev opdaget 25-08-2026 på en rigtig installation: en opdatering med
/// 612 nye tekster nåede aldrig frem, og skærmen viste nøglerne —
/// «faelles.mappe» i stedet for «MAPPE».
///
/// Fejlen kunne ikke ses i en prøve, der starter med en tom datamappe. Den
/// kræver, at man gør det, en opdatering gør: lægger en GAMMEL fil ud og
/// beder appen om at læse den nye.
/// </summary>
public class OpdateringTest
{
    // ===================== SPROGET =====================

    [Fact]
    public void Nye_noegler_kommer_med_i_en_gammel_sprogfil()
    {
        using var p = new Proevemappe();

        Directory.CreateDirectory(Sprog.Mappe);

        // En sprogfil fra en aeldre udgave: den kender kun eet af de mange
        // afsnit, appen har i dag.
        File.WriteAllText(Path.Combine(Sprog.Mappe, "da.json"), """
            {
              "_sprog": { "kode": "da", "navn": "Dansk", "flag": "DK" },
              "nav": { "cockpit": "Cockpit" }
            }
            """);

        Sprog.Genindlaes();

        // Noeglen fra den NYE udgave skal vaere der.
        Assert.Equal("Optagelser", Sprog.T("nav.optagelser"));
        Assert.Equal("MAPPE", Sprog.T("faelles.mappe"));
    }

    [Fact]
    public void En_rettet_tekst_bliver_staaende_efter_en_opdatering()
    {
        using var p = new Proevemappe();

        Directory.CreateDirectory(Sprog.Mappe);

        // Brugeren har rettet EEN tekst og mangler resten.
        File.WriteAllText(Path.Combine(Sprog.Mappe, "da.json"), """
            {
              "_sprog": { "kode": "da", "navn": "Dansk", "flag": "DK" },
              "nav": { "optagelser": "Mine møder" }
            }
            """);

        Sprog.Genindlaes();

        // DEN RETTEDE ROERES IKKE. Det er hele grunden til, at der flettes
        // frem for at skrives over.
        Assert.Equal("Mine møder", Sprog.T("nav.optagelser"));

        // Og resten er kommet med.
        Assert.Equal("Dokumenter", Sprog.T("nav.dokumenter"));
    }

    [Fact]
    public void Fletningen_skriver_ikke_filen_naar_der_ikke_er_noget_nyt()
    {
        using var p = new Proevemappe();

        Sprog.Tilgaengelige();   // foerste udpakning

        var fil = Path.Combine(Sprog.Mappe, "da.json");
        var foer = File.GetLastWriteTimeUtc(fil);

        Thread.Sleep(30);

        Sprog.Genindlaes();
        Sprog.Tilgaengelige();

        // Ellers ville hver eneste opstart give filen en ny dato uden at have
        // aendret noget - og saa kan man ikke se, hvornaar den sidst blev
        // rettet.
        Assert.Equal(foer, File.GetLastWriteTimeUtc(fil));
    }

    [Fact]
    public void En_oedelagt_sprogfil_bliver_ikke_skrevet_over()
    {
        using var p = new Proevemappe();

        Directory.CreateDirectory(Sprog.Mappe);

        var fil = Path.Combine(Sprog.Mappe, "da.json");
        File.WriteAllText(fil, "{ det her er ikke JSON");

        Sprog.Genindlaes();
        Sprog.Tilgaengelige();

        // At skrive oven i den ville slette brugerens rettelser paa grund af
        // en tastefejl et sted i filen. Den bliver liggende, og appen falder
        // tilbage paa noeglerne.
        Assert.StartsWith("{ det her", File.ReadAllText(fil), StringComparison.Ordinal);
    }

    // ===================== HJÆLPEN =====================

    [Fact]
    public void En_gammel_hjaelpetekst_bliver_opdateret()
    {
        using var p = new Proevemappe();

        // Foerste udpakning skriver bogen over, hvad appen selv lagde ud.
        Hjaelp.Alle();

        var fil = Path.Combine(Hjaelp.Mappe, "da", "10-kom-i-gang.md");

        // Nu gaar der en opdatering: teksten i appen er en anden end den, der
        // ligger. Det efterlignes ved at laegge en GAMMEL tekst ud OG rette
        // bogen, saa den siger, at det var den, appen skrev.
        var gammel = "# Kom i gang" + Environment.NewLine + Environment.NewLine + "Gammel tekst.";
        File.WriteAllText(fil, gammel);

        SkrivBog("da/10-kom-i-gang.md", gammel);

        Hjaelp.Genindlaes();

        var afsnit = Hjaelp.Alle("da").Single(a => a.Id == "10-kom-i-gang");

        Assert.DoesNotContain("Gammel tekst", afsnit.Tekst, StringComparison.Ordinal);
        Assert.Contains("fire skridt", afsnit.Undertitel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void En_rettet_hjaelpetekst_bliver_staaende()
    {
        using var p = new Proevemappe();

        Hjaelp.Alle();

        var fil = Path.Combine(Hjaelp.Mappe, "da", "10-kom-i-gang.md");

        // Brugeren har rettet teksten. Bogen staar uroert og siger derfor
        // noget ANDET end filens indhold - det er sådan en rettelse kendes.
        File.WriteAllText(fil,
            "# Kom i gang" + Environment.NewLine + Environment.NewLine
            + "Sådan gør VI det her i huset.");

        Hjaelp.Genindlaes();

        var afsnit = Hjaelp.Alle("da").Single(a => a.Id == "10-kom-i-gang");

        Assert.Contains("her i huset", afsnit.Tekst, StringComparison.Ordinal);
    }

    [Fact]
    public void Uden_en_bog_roeres_teksten_ikke()
    {
        using var p = new Proevemappe();

        var mappe = Path.Combine(Hjaelp.Mappe, "da");
        Directory.CreateDirectory(mappe);

        // En installation fra foer bogen fandtes. Saa vides det ikke, om
        // filen er rettet - og saa er det rigtige at lade den vaere. Bedre en
        // foraeldet hjaelp end en slettet rettelse.
        File.WriteAllText(Path.Combine(mappe, "10-kom-i-gang.md"),
            "# Kom i gang" + Environment.NewLine + Environment.NewLine + "Ukendt oprindelse.");

        Hjaelp.Genindlaes();

        var afsnit = Hjaelp.Alle("da").Single(a => a.Id == "10-kom-i-gang");

        Assert.Contains("Ukendt oprindelse", afsnit.Tekst, StringComparison.Ordinal);
    }

    /// <summary>Skriver bogen, som appen selv ville have gjort efter at have lagt teksten ud.</summary>
    private static void SkrivBog(string noegle, string indhold)
    {
        var sti = Path.Combine(Hjaelp.Mappe, ".udgivet.txt");

        var linjer = File.Exists(sti)
            ? File.ReadAllLines(sti).Where(l => !l.StartsWith(noegle + "\t", StringComparison.Ordinal)).ToList()
            : new List<string>();

        var renset = indhold.Replace("\r\n", "\n").TrimEnd();
        var sum = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(renset)));

        linjer.Add(noegle + "\t" + sum);

        try { File.SetAttributes(sti, FileAttributes.Normal); } catch (Exception) { }

        File.WriteAllLines(sti, linjer);
    }
}
