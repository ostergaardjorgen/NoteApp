using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af den kombination, brugeren selv trykker.
///
/// DEN HER FINDES PÅ GRUND AF EN FORMIDDAG.
///
/// Appen lyttede efter kommaet ved siden af M (0xBC). Brugeren trykkede på
/// kommaet på taltastaturet (0x6E, eller 0x2E når Shift er med) — dét, der
/// står et komma på, på et dansk tastatur. Målt med en lavniveau-hook, mens
/// han trykkede: der kom aldrig et eneste 0xBC.
///
/// Det samme gælder tallene: talrækkens 1 er 0x31, taltastaturets er 0x61, og
/// en liste viser dem begge som «1».
/// </summary>
public sealed class GenvejstastTest
{
    private const uint Ctrl = Genvejstast.MOD_CONTROL;
    private const uint Shift = Genvejstast.MOD_SHIFT;
    private const uint Alt = Genvejstast.MOD_ALT;

    // ------------------------------------------------------------ hvad der duer

    [Fact]
    public void En_tast_uden_modifikator_duer_ikke()
    {
        // Ellers gik genvejen i gang, hver gang man skrev det bogstav - i et
        // hvilket som helst program.
        Assert.False(new Genvejstast(0, 0x41).Duer);
    }

    [Fact]
    public void Modifikatoren_kan_ikke_selv_vaere_tasten()
    {
        // «Ctrl+Ctrl» er ikke en kombination. Windows afviser den, men fejlen
        // ville vise sig som «der skete ingenting».
        foreach (var vk in new uint[] { 0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0x5B })
            Assert.False(new Genvejstast(Ctrl, vk).Duer, $"0x{vk:X2} burde ikke duer");
    }

    [Fact]
    public void En_tom_kombination_er_ikke_sat()
    {
        Assert.False(default(Genvejstast).ErSat);
        Assert.False(default(Genvejstast).Duer);
    }

    [Fact]
    public void Ctrl_shift_komma_duer()
    {
        Assert.True(new Genvejstast(Ctrl | Shift, 0xBC).Duer);
    }

    // ------------------------------------------------------------ navnene

    [Fact]
    public void De_to_kommaer_hedder_IKKE_det_samme()
    {
        // DET ER HELE POINTEN. Kunne man ikke se forskel paa dem, ville
        // fejlen fra 28-08-2026 kunne ske igen.
        var vedM = new Genvejstast(Ctrl | Shift, 0xBC).Navn(",");
        var paaTal = new Genvejstast(Ctrl, 0x6E).Navn();

        Assert.NotEqual(vedM, paaTal);
        Assert.Contains("taltastatur", paaTal);
        Assert.DoesNotContain("taltastatur", vedM);
    }

    [Fact]
    public void De_to_ettaller_hedder_heller_ikke_det_samme()
    {
        var raekken = new Genvejstast(Ctrl | Shift, 0x31).Navn();
        var taltastaturet = new Genvejstast(Ctrl | Shift, 0x61).Navn();

        Assert.NotEqual(raekken, taltastaturet);
        Assert.Contains("taltastatur", taltastaturet);
    }

    [Fact]
    public void Modifikatorerne_staar_i_fast_raekkefoelge()
    {
        // Ctrl, Shift, Alt - altid i den orden. Ellers ville den samme
        // kombination staa forskelligt to steder i appen.
        Assert.Equal("Ctrl+Shift+Alt+A", new Genvejstast(Ctrl | Shift | Alt, 0x41).Navn());
        Assert.Equal("Ctrl+Shift+Alt+A", new Genvejstast(Alt | Shift | Ctrl, 0x41).Navn());
    }

    [Fact]
    public void De_saerlige_taster_har_navne_og_ikke_numre()
    {
        Assert.Equal("Ctrl+Mellemrum", new Genvejstast(Ctrl, 0x20).Navn());
        Assert.Equal("Ctrl+F9", new Genvejstast(Ctrl, 0x78).Navn());
        Assert.Equal("Ctrl+Delete", new Genvejstast(Ctrl, 0x2E).Navn());
        Assert.Equal("Ctrl+Pil op", new Genvejstast(Ctrl, 0x26).Navn());
    }

    [Fact]
    public void Et_komma_faar_sit_navn_med()
    {
        // «Ctrl+Shift+,» slutter paa et komma, og et komma sidst i en saetning
        // ligner tegnsaetning - man kan ikke se, om tasten er en del af
        // genvejen eller bare et skilletegn.
        Assert.Equal("Ctrl+Shift+, (komma)", new Genvejstast(Ctrl | Shift, 0xBC).Navn(","));
    }

    [Fact]
    public void En_ukendt_tast_staar_med_sit_nummer()
    {
        // Bedre end en tom plads: saa kan man i det mindste kende den igen.
        Assert.Contains("0xF1", new Genvejstast(Ctrl, 0xF1).Navn());
    }

    // ------------------------------------------------------------ NumLock

    [Fact]
    public void Taltastaturets_taster_har_en_tvilling()
    {
        // Den samme fysiske tast sender to forskellige virtuelle taster alt
        // efter NumLock. Registrerer man kun den ene, holder genvejen op med
        // at virke i det oejeblik, nogen roerer NumLock.
        Assert.Equal(0x2Eu, Genvejstast.Tvillingen(0x6E));   // komma <-> Delete
        Assert.Equal(0x2Du, Genvejstast.Tvillingen(0x60));   // 0 <-> Insert
        Assert.Equal(0x23u, Genvejstast.Tvillingen(0x61));   // 1 <-> End
    }

    [Fact]
    public void Tvillingen_peger_begge_veje()
    {
        // Ellers ville den ene NumLock-tilstand vaere daekket og den anden ikke.
        foreach (var vk in new uint[] { 0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6E })
            Assert.Equal(vk, Genvejstast.Tvillingen(Genvejstast.Tvillingen(vk)));
    }

    [Fact]
    public void En_almindelig_tast_har_ingen_tvilling()
    {
        foreach (var vk in new uint[] { 0x41, 0x30, 0xBC, 0x20, 0x78 })
            Assert.Equal(0u, Genvejstast.Tvillingen(vk));
    }

    [Fact]
    public void Shift_duer_ikke_paa_taltastaturet()
    {
        // Shift vender NumLock om, mens den holdes nede. Efterproevet
        // 28-08-2026: Ctrl+Shift+numpad-0 og Ctrl+Shift+numpad-komma er begge
        // doede, mens de samme uden Shift virker.
        Assert.True(new Genvejstast(Ctrl | Shift, 0x60).ShiftDuerIkke);
        Assert.True(new Genvejstast(Ctrl | Shift, 0x6E).ShiftDuerIkke);

        Assert.False(new Genvejstast(Ctrl, 0x60).ShiftDuerIkke);
        Assert.False(new Genvejstast(Ctrl | Shift, 0xBC).ShiftDuerIkke);
    }

    // ------------------------------------------------------------ gemning

    [Fact]
    public void Den_kan_gemmes_og_laeses_igen()
    {
        var t = new Genvejstast(Ctrl | Shift, 0x6E);
        Assert.Equal(t, Genvejstast.Laes(t.ToString()));
    }

    [Fact]
    public void Noget_vroevl_i_filen_giver_en_tom_kombination()
    {
        // En oedelagt indstillingsfil maa ikke forhindre appen i at starte -
        // og en halvt laest kombination ville vaere vaerre end ingen.
        foreach (var s in new[] { null, "", "  ", "ctrl-shift-1", "2:", ":45", "a:b", "2:3:4" })
            Assert.False(Genvejstast.Laes(s).ErSat, $"«{s}» burde give en tom");
    }

    [Fact]
    public void En_tom_kombination_gemmes_som_ingenting()
    {
        Assert.Equal("", default(Genvejstast).ToString());
    }
}
