using NoteApp.Core.Deling;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Hvornår arkivet kører.
/// </summary>
/// <remarks>
/// ============ DET, PRØVERNE HER PASSER PÅ ============
///
/// At «kl. 8 og kl. 17» BLIVER ved med at være kl. 8 og kl. 17. Regnes der på
/// mellemrum i stedet, glider tidspunktet en smule for hver dag, indtil kl. 8
/// er blevet kl. 11 — og det opdager ingen, for der sker jo noget.
///
/// At en SLUKKET maskine tager dagens tur, når den tændes. Alternativet — at
/// springe dagen over — er den slags, man opdager en uge senere.
///
/// At «kun når jeg trykker» betyder DET.
/// </remarks>
public class Arkivplantest
{
    private static DateTimeOffset Kl(int dag, int time, int minut = 0) =>
        new(2026, 9, dag, time, minut, 0, TimeSpan.FromHours(2));

    private static bool Tid(Arkivtakt takt, DateTimeOffset? sidst, DateTimeOffset nu,
                            bool kunITidsrum = false, int fra = 8, int til = 17) =>
        Arkivplan.Er_det_tid(takt, kunITidsrum, fra, til, sidst, nu);

    // ===================================================================== mellemrum

    [Fact]
    public void Aldrig_koert_er_altid_tid()
    {
        // En frisk installation skal ikke vente et kvarter paa noget, den
        // kunne have gjort med det samme.
        Assert.True(Tid(Arkivtakt.Kvarter, null, Kl(7, 10)));
        Assert.True(Tid(Arkivtakt.Togange, null, Kl(7, 10)));
    }

    [Fact]
    public void Kvarteret_venter_et_kvarter()
    {
        var sidst = Kl(7, 10, 0);

        Assert.False(Tid(Arkivtakt.Kvarter, sidst, Kl(7, 10, 14)));
        Assert.True(Tid(Arkivtakt.Kvarter, sidst, Kl(7, 10, 15)));
    }

    [Fact]
    public void Manuelt_koerer_aldrig_af_sig_selv()
    {
        // ============ «KUN NAAR JEG TRYKKER» SKAL BETYDE DET ============
        //
        // Ogsaa naar der aldrig er koert, og ogsaa naar der er gaaet en uge.
        Assert.False(Tid(Arkivtakt.Manuelt, null, Kl(7, 10)));
        Assert.False(Tid(Arkivtakt.Manuelt, Kl(1, 10), Kl(30, 10)));
    }

    // ===================================================================== tidsrummet

    [Fact]
    public void Uden_for_tidsrummet_sker_der_ingenting()
    {
        Assert.False(Tid(Arkivtakt.Kvarter, null, Kl(7, 3), kunITidsrum: true));
        Assert.True(Tid(Arkivtakt.Kvarter, null, Kl(7, 9), kunITidsrum: true));

        // Kl. 17 er UDE. Et tidsrum «8 til 17» slutter kl. 17 - ellers ville
        // det vaere 8 til 18, og det er ikke det, der staar.
        Assert.False(Tid(Arkivtakt.Kvarter, null, Kl(7, 17), kunITidsrum: true));
        Assert.True(Tid(Arkivtakt.Kvarter, null, Kl(7, 16, 59), kunITidsrum: true));
    }

    [Fact]
    public void Tidsrummet_maa_gaa_over_midnat()
    {
        // «Fra 22 til 6» er et rigtigt svar for den, der arbejder om aftenen.
        Assert.True(Arkivplan.Indenfor(22, 6, Kl(7, 23)));
        Assert.True(Arkivplan.Indenfor(22, 6, Kl(7, 2)));
        Assert.False(Arkivplan.Indenfor(22, 6, Kl(7, 12)));
    }

    [Fact]
    public void To_ens_klokkeslaet_er_hele_doegnet()
    {
        // At saette begge til 8 betyder ikke «koer aldrig». Ingen mener det.
        Assert.True(Arkivplan.Indenfor(8, 8, Kl(7, 3)));
        Assert.True(Arkivplan.Indenfor(8, 8, Kl(7, 15)));
    }

    // ================================================================ faste tidspunkter

    [Fact]
    public void To_gange_om_dagen_rammer_de_to_tidspunkter()
    {
        // Foer den foerste: intet.
        Assert.False(Tid(Arkivtakt.Togange, Kl(6, 17, 30), Kl(7, 7)));

        // Kl. 8: dagens foerste tur.
        Assert.True(Tid(Arkivtakt.Togange, Kl(6, 17, 30), Kl(7, 8)));

        // Taget kl. 8 - saa ikke igen kl. 12.
        Assert.False(Tid(Arkivtakt.Togange, Kl(7, 8, 1), Kl(7, 12)));

        // Kl. 17: dagens anden tur.
        Assert.True(Tid(Arkivtakt.Togange, Kl(7, 8, 1), Kl(7, 17)));

        // Og ikke een gang til samme aften.
        Assert.False(Tid(Arkivtakt.Togange, Kl(7, 17, 1), Kl(7, 22)));
    }

    [Fact]
    public void Tidspunktet_glider_ikke()
    {
        // ============ DET ER DERFOR DER IKKE REGNES PAA MELLEMRUM ============
        //
        // Koeres der kl. 8.55, fordi maskinen var slukket kl. 8, skal turen
        // NAESTE dag stadig komme kl. 8 - ikke kl. 8.55. Med «24 timer siden»
        // ville kl. 8 vaere blevet kl. 11 inden for en uge.
        var sidst = Kl(7, 8, 55);

        Assert.True(Tid(Arkivtakt.Engang, sidst, Kl(8, 8, 0)));
    }

    [Fact]
    public void En_slukket_maskine_tager_turen_naar_den_taendes()
    {
        // Maskinen var slukket kl. 8. Den taendes 9.30, og dagens tur er
        // stadig ikke taget.
        Assert.True(Tid(Arkivtakt.Engang, Kl(6, 8, 1), Kl(7, 9, 30)));

        // Og saa er den taget.
        Assert.False(Tid(Arkivtakt.Engang, Kl(7, 9, 31), Kl(7, 23)));
    }

    [Fact]
    public void En_gang_om_dagen_bruger_kun_det_foerste_klokkeslaet()
    {
        // Kl. 17 er tidsrummets anden ende og betyder ingenting her.
        Assert.False(Tid(Arkivtakt.Engang, Kl(7, 8, 1), Kl(7, 17)));
    }
}
