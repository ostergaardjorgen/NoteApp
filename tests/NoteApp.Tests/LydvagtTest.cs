using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af den beslutning, der starter en optagelse, uden at nogen trykker.
///
/// Den skal kunne to ting, der trækker hver sin vej: gå i gang på en samtale
/// med det samme, og IKKE gå i gang på alt det andet, en computer larmer af.
/// Prøverne herunder er skrevet som de fejl, den kan lave.
/// </summary>
public sealed class LydvagtTest
{
    private static DateTimeOffset T0 => new(2026, 8, 25, 14, 0, 0, TimeSpan.Zero);

    /// <summary>Aflæsninger hvert kvarte sekund, som måleren gør det.</summary>
    private static DateTimeOffset Tik(int n) => T0.AddMilliseconds(250 * n);

    private const float Tale = 0.15f;
    private const float Stille = 0.001f;

    // ---------------------------------------------------------------- starter

    [Fact]
    public void Samtale_starter()
    {
        // Modparten taler, man svarer. Det er mødet.
        var v = new Lydvagt();
        var gik = false;

        for (var i = 0; i < 12 && !gik; i++) gik = v.Meld(Stille, Tale, Tik(i));
        Assert.False(gik, "gik i gang paa lyd fra kun det ene spor");

        for (var i = 12; i < 30 && !gik; i++) gik = v.Meld(Tale, Tale, Tik(i));
        Assert.True(gik, "en samtale paa begge spor satte ikke optagelsen i gang");
    }

    [Fact]
    public void Man_maa_gerne_tale_foerst()
    {
        // «Hej, kan I hoere mig?» — og saa svarer de. Lige saa meget et moede.
        var v = new Lydvagt();
        var gik = false;

        for (var i = 0; i < 12 && !gik; i++) gik = v.Meld(Tale, Stille, Tik(i));
        Assert.False(gik, "gik i gang paa mikrofonen alene");

        for (var i = 12; i < 30 && !gik; i++) gik = v.Meld(Tale, Tale, Tik(i));
        Assert.True(gik, "raekkefoelgen burde vaere ligegyldig");
    }

    [Fact]
    public void Samtale_starter_hurtigt()
    {
        // Den maa ikke bare komme i gang - den skal komme i gang HURTIGT,
        // ellers mangler de foerste replikker.
        var v = new Lydvagt();
        var ved = -1;
        for (var i = 0; i < 60 && ved < 0; i++)
            if (v.Meld(Tale, Tale, Tik(i))) ved = i;

        Assert.True(ved >= 0, "gik aldrig i gang paa ubrudt samtale");
        Assert.True(ved < 12, $"foerst i gang efter {ved} aflaesninger - over tre sekunder");
    }

    [Fact]
    public void Tale_med_pauser_starter()
    {
        // Tale er ikke sammenhaengende. To aflaesninger med lyd, een uden.
        var v = new Lydvagt();
        var gik = false;
        for (var i = 0; i < 40 && !gik; i++)
        {
            var n = i % 3 == 2 ? Stille : Tale;
            gik = v.Meld(n, n, Tik(i));
        }

        Assert.True(gik, "tale med almindelige pauser satte ikke optagelsen i gang");
    }

    [Fact]
    public void Fysisk_moede_starter_paa_mikrofonen_alene()
    {
        // Ingen hoejttalerenhed: ingen modpart at vente paa. Ellers ville et
        // moede omkring et bord aldrig kunne komme i gang.
        var v = new Lydvagt(harOnlinespor: false);
        var gik = false;
        for (var i = 0; i < 20 && !gik; i++) gik = v.Meld(Tale, 0f, Tik(i));

        Assert.True(gik, "et fysisk moede kunne ikke starte");
    }

    // ------------------------------------------------------------ starter ikke

    [Fact]
    public void Stilhed_starter_ikke()
    {
        var v = new Lydvagt();
        for (var i = 0; i < 80; i++)
            Assert.False(v.Meld(Stille, Stille, Tik(i)), $"gik i gang paa stilhed ved {i}");
    }

    [Fact]
    public void En_video_der_spiller_starter_ikke()
    {
        // Lyd paa hoejttalersporet i lang tid, intet svar. Det er en video,
        // en podcast eller musik - ikke et moede.
        var v = new Lydvagt();
        for (var i = 0; i < 240; i++)
            Assert.False(v.Meld(Stille, Tale, Tik(i)),
                         $"en video satte optagelsen i gang ved aflaesning {i}");
    }

    [Fact]
    public void Snak_i_rummet_starter_ikke()
    {
        // Den anden vej: nogen taler i rummet, computeren er tavs.
        var v = new Lydvagt();
        for (var i = 0; i < 240; i++)
            Assert.False(v.Meld(Tale, Stille, Tik(i)),
                         $"snak i rummet satte optagelsen i gang ved {i}");
    }

    [Fact]
    public void Et_enkelt_smaeld_starter_ikke()
    {
        // En doer, der smaekker: eet meget hoejt tal paa begge spor, saa
        // stilhed. Niveauet er langt over taersklen - det er netop pointen,
        // at hoejden alene ikke er nok.
        var v = new Lydvagt();
        Assert.False(v.Meld(0.9f, 0.9f, Tik(0)));
        for (var i = 1; i < 60; i++)
            Assert.False(v.Meld(Stille, Stille, Tik(i)), $"gik i gang efter et smaeld ved {i}");
    }

    [Fact]
    public void For_sent_svar_starter_ikke()
    {
        // Modparten siger noget. Der gaar et halvt minut. SAA siger nogen noget
        // i rummet. To ting, der ikke har med hinanden at goere.
        var v = new Lydvagt();
        for (var i = 0; i < 12; i++)
            Assert.False(v.Meld(Stille, Tale, Tik(i)));

        // Stilhed laenge nok til, at svartiden er loebet ud.
        for (var i = 12; i < 60; i++)
            Assert.False(v.Meld(Stille, Stille, Tik(i)));

        // Og saa noget paa mikrofonen alene.
        for (var i = 60; i < 100; i++)
            Assert.False(v.Meld(Tale, Stille, Tik(i)),
                         $"et svar {(Tik(i) - Tik(0)).TotalSeconds:0} sekunder efter blev talt med");
    }

    [Fact]
    public void Kraever_flere_aflaesninger_end_een()
    {
        var v = new Lydvagt();
        Assert.False(v.Meld(0.9f, 0.9f, Tik(0)), "een aflaesning var nok - bunden virker ikke");
    }

    // ------------------------------------------------------------------ andet

    [Fact]
    public void Nulstil_glemmer_alt()
    {
        var v = new Lydvagt();
        for (var i = 0; i < 20; i++) v.Meld(Tale, Tale, Tik(i));
        v.Nulstil();

        // Udslaget skal laeses FOER der meldes igen. Goer man det bagefter, er
        // det den nye aflaesning, man maaler - og saa proever man ingenting.
        Assert.Equal(0, v.UdslagMikrofon);
        Assert.Equal(0, v.UdslagOnline);

        Assert.False(v.Meld(0.9f, 0.9f, Tik(21)), "huskede efter Nulstil");
    }

    [Fact]
    public void Venter_paa_svar_naar_kun_modparten_har_talt()
    {
        var v = new Lydvagt();
        for (var i = 0; i < 12; i++) v.Meld(Stille, Tale, Tik(i));

        Assert.True(v.VenterPaaSvar, "burde vente paa svar, naar kun modparten har talt");
    }

    [Fact]
    public void Taersklen_ligger_mellem_rumstoej_og_tale()
    {
        // Tallene er dem, det hele bygger paa. Flyttes de, skal denne proeve
        // fejle, saa det bliver en beslutning og ikke en glidning.
        Assert.True(Lydvagt.Taerskel > 0.01f, "taersklen er nede i rumstoej");
        Assert.True(Lydvagt.Taerskel < 0.05f, "taersklen er oppe i tale - stille talere tabes");
        Assert.Equal(10, Lydvagt.Svartid.TotalSeconds);
    }
}
