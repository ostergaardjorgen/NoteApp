using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af ekkofjernelsen — den, der afgør, hvem der sagde hvad.
///
/// Fejler den til den ene side, står den samme sætning to gange i referatet.
/// Det er grimt. Fejler den til den anden side, bliver din egen udtalelse
/// slettet fra dit spor, og så findes den kun i modpartens — og referatet
/// lægger den i hans mund. Det er meget værre, og det er den fejl, prøverne
/// her er skrevet efter.
///
/// Set 25-08-2026 på et rigtigt møde: en udtalelse på tyve ord blev slettet
/// som ekko af en på otte, fordi de delte «det», «jeg» og «at».
/// </summary>
public sealed class EkkoTest
{
    private static Replik R(string tekst, int fraSek, int tilSek, string spor) =>
        new(fraSek * 1000L, tilSek * 1000L, spor, tekst);

    private static List<Replik> Mik(params Replik[] r) => r.ToList();

    // ------------------------------------------------ det, der SKAL fjernes

    [Fact]
    public void Et_rigtigt_ekko_fjernes()
    {
        // Samme saetning, samme tid, samme laengde: mikrofonen har opfanget
        // det, hoejttaleren spillede.
        var mik = Mik(R("Vi mødes igen på tirsdag klokken ti hos jer", 10, 14, "herfra"));
        var loop = Mik(R("Vi mødes igen på tirsdag klokken ti hos jer", 10, 14, "derfra"));

        Assert.Empty(Samtale.FjernEkko(mik, loop));
    }

    [Fact]
    public void Et_ekko_med_smaa_afvigelser_fjernes_ogsaa()
    {
        // Det svage spor taber typisk ord i enderne.
        var mik = Mik(R("så mødes vi igen på tirsdag klokken ti hos jer i huset", 10, 15, "herfra"));
        var loop = Mik(R("mødes vi igen på tirsdag klokken ti hos jer i huset", 10, 15, "derfra"));

        Assert.Empty(Samtale.FjernEkko(mik, loop));
    }

    // ---------------------------------------- det, der ALDRIG maa fjernes

    [Fact]
    public void En_lang_udtalelse_slettes_ikke_af_en_kort_bemaerkning()
    {
        // DET HER ER FEJLEN FRA 25-08-2026, ordret.
        var mik = Mik(R("Men jeg tror at du kan være helt sikker på at hvis du får " +
                        "de der succesoplevelser i det marked så vil de også snakke med " +
                        "deres venner i branchen", 1461, 1470, "herfra"));

        var loop = Mik(R("Det håber jeg selvfølgelig på at der så", 1461, 1466, "derfra"));

        var beholdt = Samtale.FjernEkko(mik, loop);

        Assert.Single(beholdt);
        Assert.Contains("succesoplevelser", beholdt[0].Tekst);
    }

    [Fact]
    public void Fire_funktionsord_kan_ikke_slette_tyve()
    {
        // «altsaa det var jo» deler tre af sine fire ord med saetningen
        // nedenfor - og det er rene funktionsord.
        var mik = Mik(R("det må man sige ja det er hvad er det jeg tænker er det " +
                        "tyve år siden eller sådan noget", 0, 8, "herfra"));

        var loop = Mik(R("altså det var jo", 0, 3, "derfra"));

        Assert.Single(Samtale.FjernEkko(mik, loop));
    }

    [Fact]
    public void Faelles_funktionsord_alene_er_ikke_et_ekko()
    {
        // To saetninger af samme laengde, der KUN deler smaaord. De handler om
        // hver sit, og det ses paa, at ingen indholdsord gaar igen.
        var mik = Mik(R("og det er jo også noget vi skal have styr på", 20, 24, "herfra"));
        var loop = Mik(R("og det er jo også derfor jeg ringer i dag", 20, 24, "derfra"));

        Assert.Single(Samtale.FjernEkko(mik, loop));
    }

    [Fact]
    public void To_forskellige_udtalelser_paa_samme_tid_bliver_begge()
    {
        var mik = Mik(R("jeg synes prisen ligger for højt til det marked", 30, 34, "herfra"));
        var loop = Mik(R("vi kigger nærmere på kontrakten inden fredag", 30, 34, "derfra"));

        Assert.Single(Samtale.FjernEkko(mik, loop));
    }

    [Fact]
    public void Samme_saetning_paa_et_helt_andet_tidspunkt_er_ikke_et_ekko()
    {
        // Et ekko hoeres SAMTIDIG. Siges det samme igen ti minutter senere, er
        // det, fordi nogen gentog sig - og det skal staa.
        var mik = Mik(R("vi mødes igen på tirsdag klokken ti hos jer", 600, 604, "herfra"));
        var loop = Mik(R("vi mødes igen på tirsdag klokken ti hos jer", 10, 14, "derfra"));

        Assert.Single(Samtale.FjernEkko(mik, loop));
    }

    [Fact]
    public void Korte_udbrud_bliver_staaende()
    {
        // En dublet af et «ja» koster ingenting; et tabt «ja» fra den forkerte
        // side kan vende meningen af et referat.
        var mik = Mik(R("ja lige præcis", 5, 6, "herfra"));
        var loop = Mik(R("ja lige præcis", 5, 6, "derfra"));

        Assert.Single(Samtale.FjernEkko(mik, loop));
    }

    [Fact]
    public void Uden_hoejttalerspor_fjernes_ingenting()
    {
        var mik = Mik(R("jeg synes prisen ligger for højt til det marked", 30, 34, "herfra"));

        Assert.Single(Samtale.FjernEkko(mik, new List<Replik>()));
    }
}
