using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Afløsningen af hentede aftaler — det sted, hvor en fejl koster en
/// optagelse, ingen opdager mangler.
///
/// Hver hentning kaster ALT fra kilden væk og lægger det hentede i stedet.
/// Det, der skal overleve den udskiftning, er appens egne felter: mødetype,
/// mappe, sprog — og især flaget om automatisk optagelse. Går det tabt, står
/// aftalen der stadig, den ser rigtig ud, og der bliver bare ikke optaget.
/// </summary>
public class KalenderTest
{
    private static Aftale Hentet(string fremmedId, string titel, DateTimeOffset start) =>
        new()
        {
            Titel = titel,
            Start = start,
            Slut = start.AddHours(1),
            Kilde = Kalenderkilde.Google,
            FremmedId = fremmedId
        };

    // ===================== DET, DER SKAL OVERLEVE EN HENTNING =====================

    [Fact]
    public void Automatisk_optagelse_overlever_en_hentning()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        Kalender.Afloes(Kalenderkilde.Google, new[] { Hentet("g1", "Statusmøde", start) });

        // Brugeren saetter hak i «optag automatisk».
        var min = Kalender.Alle().Single(a => a.FremmedId == "g1");
        min.OptagAutomatisk = true;
        min.Moedetype = "Statusmøde";
        min.Mappe = "Kunde A";
        Kalender.Gem(min);

        // Naeste hentning et kvarter senere henter den samme aftale igen.
        Kalender.Afloes(Kalenderkilde.Google, new[] { Hentet("g1", "Statusmøde", start) });

        var efter = Kalender.Alle().Single(a => a.FremmedId == "g1");

        Assert.True(efter.OptagAutomatisk);
        Assert.Equal("Statusmøde", efter.Moedetype);
        Assert.Equal("Kunde A", efter.Mappe);
    }

    [Fact]
    public void Flyttes_moedet_kan_der_optages_igen()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddHours(2);

        Kalender.Afloes(Kalenderkilde.Google, new[] { Hentet("g1", "Møde", start) });

        var min = Kalender.Alle().Single();
        min.OptagAutomatisk = true;
        min.Startet = DateTimeOffset.Now;      // vagten har optaget den
        Kalender.Gem(min);

        // Samme tidspunkt: «Startet» skal huskes, saa der ikke optages to gange.
        Kalender.Afloes(Kalenderkilde.Google, new[] { Hentet("g1", "Møde", start) });
        Assert.NotNull(Kalender.Alle().Single().Startet);

        // Rykket en time: det er en NY lejlighed, og der skal kunne optages igen.
        Kalender.Afloes(Kalenderkilde.Google, new[] { Hentet("g1", "Møde", start.AddHours(1)) });
        Assert.Null(Kalender.Alle().Single().Startet);
    }

    // ===================== INGEN DUBLETTER =====================

    [Fact]
    public void En_egen_aftale_der_ogsaa_ligger_i_Google_kommer_ikke_retur_som_dublet()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        // Aftalen er lavet HER og lagt op i Google; den har derfor et fremmed-id.
        Kalender.Gem(new Aftale
        {
            Titel = "Mit eget møde",
            Start = start,
            Slut = start.AddHours(1),
            Kilde = Kalenderkilde.Lokal,
            FremmedId = "g99"
        });

        // Google leverer den tilbage ved naeste hentning.
        Kalender.Afloes(Kalenderkilde.Google, new[] { Hentet("g99", "Mit eget møde", start) });

        var alle = Kalender.Alle();

        Assert.Single(alle);
        Assert.Equal(Kalenderkilde.Lokal, alle[0].Kilde);
    }

    // ===================== DET, DER FORSVINDER HOS GOOGLE =====================

    [Fact]
    public void En_aftale_der_er_slettet_i_Google_forsvinder_ogsaa_her()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        Kalender.Afloes(Kalenderkilde.Google, new[]
        {
            Hentet("g1", "Bliver", start),
            Hentet("g2", "Forsvinder", start.AddHours(2))
        });

        Assert.Equal(2, Kalender.Alle().Count);

        Kalender.Afloes(Kalenderkilde.Google, new[] { Hentet("g1", "Bliver", start) });

        var alle = Kalender.Alle();
        Assert.Single(alle);
        Assert.Equal("Bliver", alle[0].Titel);
    }

    [Fact]
    public void En_hentning_roerer_ikke_de_lokale_aftaler()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        Kalender.Gem(new Aftale { Titel = "Kun her", Start = start, Slut = start.AddHours(1) });

        Kalender.Afloes(Kalenderkilde.Google, Array.Empty<Aftale>());

        Assert.Single(Kalender.Alle());
        Assert.Equal("Kun her", Kalender.Alle()[0].Titel);
    }

    [Fact]
    public void Afloes_svarer_med_hvor_mange_der_kom_ind()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        var n = Kalender.Afloes(Kalenderkilde.Google, new[]
        {
            Hentet("g1", "En", start),
            Hentet("g2", "To", start.AddHours(1)),
            Hentet("g3", "Tre", start.AddHours(2))
        });

        Assert.Equal(3, n);
    }

    // ============ DEN SAMME AFTALE TO GANGE FRA KILDEN ============
    //
    // MAALT 04-09-2026: kalender.json havde seks aftaler, hvoraf to var den
    // SAMME post - samme id, samme titel, samme tidspunkt. Google havde sendt
    // den to gange i det samme svar.
    //
    // Resultatet var, at hver eneste hentning derefter kastede «An item with
    // the same key has already been added», og at synkroniseringen var laast
    // permanent. Der var ingen vej ud fra skaermen: fejlen laa i de gemte
    // data, og det eneste, der roerte dem, var netop den hentning, der ikke
    // kunne koere.
    //
    // Det er den slags fejl, der er vaerst - eet skaevt svar fra en
    // leverandoer, og funktionen er vaek for altid.

    [Fact]
    public void Den_samme_aftale_to_gange_fra_kilden_bliver_til_een()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        var n = Kalender.Afloes(Kalenderkilde.Google, new[]
        {
            Hentet("g1", "Projektledelse", start),
            Hentet("g1", "Projektledelse", start),
            Hentet("g2", "Noget andet", start.AddHours(2))
        });

        Assert.Equal(2, n);
        Assert.Equal(2, Kalender.Alle().Count);
        Assert.Single(Kalender.Alle().Where(a => a.FremmedId == "g1"));
    }

    [Fact]
    public void En_gemt_dublet_vaelter_ikke_naeste_hentning_og_bliver_ryddet_op()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        // Saadan SAA filen ud efter det skaeve svar. Gem(Aftale) matcher paa
        // appens EGET id og ikke paa fremmed-id'et, saa to kald med hver sit
        // objekt lander som to raekker med samme FremmedId - noejagtig den
        // tilstand, der blev maalt i kalender.json.
        Kalender.Gem(Hentet("g1", "Projektledelse", start));
        Kalender.Gem(Hentet("g1", "Projektledelse", start));

        Assert.Equal(2, Kalender.Alle().Count);

        // FOER RETTELSEN KASTEDE DEN HER.
        var n = Kalender.Afloes(Kalenderkilde.Google, new[]
        {
            Hentet("g1", "Projektledelse", start)
        });

        Assert.Equal(1, n);
        Assert.Single(Kalender.Alle());
    }

    [Fact]
    public void Brugerens_egne_felter_overlever_ogsaa_naar_der_laa_en_dublet()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        // To gemte raekker med samme id, hvor den FOERSTE baerer hakket.
        // Opslaget tager den foerste af en gruppe, saa hakket skal med over.
        var med = Hentet("g1", "Projektledelse", start);
        med.OptagAutomatisk = true;
        med.Moedetype = "Statusmøde";

        Kalender.Gem(med);
        Kalender.Gem(Hentet("g1", "Projektledelse", start));

        Kalender.Afloes(Kalenderkilde.Google, new[] { Hentet("g1", "Projektledelse", start) });

        var nu = Assert.Single(Kalender.Alle());

        Assert.True(nu.OptagAutomatisk);
        Assert.Equal("Statusmøde", nu.Moedetype);
    }

    [Fact]
    public void Aftaler_uden_fremmedid_foldes_ikke_sammen()
    {
        using var p = new Proevemappe();

        var start = DateTimeOffset.Now.AddDays(1);

        // Tom id betyder «ingen id», ikke «samme id». To saadanne er to
        // aftaler, og en sammenfoldning ville faa den ene til at forsvinde.
        var n = Kalender.Afloes(Kalenderkilde.Google, new[]
        {
            Hentet("", "En", start),
            Hentet("", "To", start.AddHours(1))
        });

        Assert.Equal(2, n);
        Assert.Equal(2, Kalender.Alle().Count);
    }
}
