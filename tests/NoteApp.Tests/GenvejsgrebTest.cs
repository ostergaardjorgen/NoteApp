using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af genvejens greb på tastaturet.
///
/// DEN HER PRØVE FINDES PÅ GRUND AF TRE DAGE, DER GIK TABT.
///
/// Genvejen blev bygget på den tastkode, Windows nåede frem til — og Windows
/// oversætter tasten, FØR programmet ser den. Målt 30-08-2026, 669 tryk i
/// træk på den samme tast:
///
///     vk=0x2E   scancode=0x53
///
/// 0x53 er taltastaturets komma. 0x2E er Delete. Brugeren trykkede på et
/// komma — tasten er mærket med et komma på et dansk tastatur — og Windows
/// leverede en Delete, fordi Shift midlertidigt vender NumLock om. Appen
/// lyttede efter kommaet ved siden af M og hørte ingenting.
///
/// Hverken bruger eller udvikler kunne se det. Derfor bygger grebet nu på
/// tastens FYSISKE plads, og derfor prøves netop det af her.
/// </summary>
public class GenvejsgrebTest
{
    private const uint VedM = Genvejsgreb.Komma;              // 0x33
    private const uint PaaTal = Genvejsgreb.Taltastaturkomma; // 0x53

    private static Genvejsgreb CtrlShift(uint sc, bool udvidet = false) =>
        new(sc, udvidet, Ctrl: true, Shift: true, Alt: false);

    // ======================= det, der gik galt =======================

    [Fact]
    public void Tastens_plads_afgoer_det_ikke_hvad_windows_kaldte_den()
    {
        // DET ER HELE OMBYGNINGEN I ÉN PROEVE. Grebet er taltastaturets
        // komma. Windows leverede en Delete - vk=0x2E - men scancoden er den
        // samme, og saa passer den.
        var greb = CtrlShift(PaaTal);

        Assert.True(greb.Passer(PaaTal, udvidet: false, ctrl: true, shift: true, alt: false));
    }

    [Fact]
    public void De_to_kommaer_er_den_samme_genvej()
    {
        // ============ VENDT OM 05-09-2026, MED VILJE ============
        //
        // Her stod det modsatte: at de to kommataster var to forskellige
        // genveje. Det er sandt om tastaturet og forkert om mennesket. Paa
        // begge taster staar der et komma; fingeren rammer «kommaet».
        //
        // Foelgen var, at den gemte genvej var bundet til DEN ENE af dem.
        // Skaermen sagde «Ctrl+Shift+,», tasten sagde komma, og der skete
        // ingenting. Den gamle loesning registrerede tvillingen i begge grene
        // netop derfor - se Genvejsgreb.Tvillingen.
        var greb = CtrlShift(PaaTal);

        Assert.True(greb.Passer(VedM, udvidet: false, ctrl: true, shift: true, alt: false));
    }

    [Fact]
    public void Delete_tasten_udloeser_ikke_taltastaturets_komma()
    {
        // De har SAMME scancode. Det, der skiller dem, er «udvidet». Uden den
        // ville et tryk paa den rigtige Delete starte en diktering.
        var greb = CtrlShift(PaaTal, udvidet: false);

        Assert.False(greb.Passer(PaaTal, udvidet: true, ctrl: true, shift: true, alt: false));
    }

    // ======================= holdetasterne =======================

    [Fact]
    public void Alle_holdetaster_skal_passe()
    {
        var greb = CtrlShift(VedM);

        Assert.False(greb.Passer(VedM, false, ctrl: true, shift: false, alt: false));
        Assert.False(greb.Passer(VedM, false, ctrl: false, shift: true, alt: false));
        Assert.False(greb.Passer(VedM, false, ctrl: true, shift: true, alt: true));
    }

    [Fact]
    public void Et_greb_uden_holdetast_duer_ikke()
    {
        // Ellers ville genvejen udloese sig selv, hver gang nogen skrev et
        // komma.
        Assert.False(new Genvejsgreb(VedM, false, false, false, false).Duer);
        Assert.False(new Genvejsgreb(0, false, true, true, false).Duer);
        Assert.True(new Genvejsgreb(VedM, false, true, false, false).Duer);
    }

    // ======================= gem og laes =======================

    [Fact]
    public void Det_gemte_kan_laeses_igen()
    {
        var greb = new Genvejsgreb(PaaTal, Udvidet: false, Ctrl: true, Shift: true, Alt: false);
        Assert.Equal(greb, Genvejsgreb.Laes(greb.Gem()));
    }

    [Fact]
    public void Udvidet_overlever_gemningen()
    {
        // Uden den ville Delete og taltastaturets komma bytte plads efter en
        // genstart - og saa var vi tilbage ved den fejl, der kostede tre dage.
        var greb = new Genvejsgreb(PaaTal, Udvidet: true, Ctrl: true, Shift: false, Alt: false);
        var igen = Genvejsgreb.Laes(greb.Gem());

        Assert.NotNull(igen);
        Assert.True(igen!.Udvidet);
    }

    [Fact]
    public void Uforstaaeligt_bliver_til_ingenting_ikke_til_et_gaet()
    {
        // Et gaet ville betyde, at genvejen stille blev en anden - og det er
        // dét, ombygningen findes for at goere en ende paa.
        Assert.Null(Genvejsgreb.Laes(null));
        Assert.Null(Genvejsgreb.Laes(""));
        Assert.Null(Genvejsgreb.Laes("noget vaas"));
        Assert.Null(Genvejsgreb.Laes("cs:ZZ"));
        Assert.Null(Genvejsgreb.Laes(":33"));      // ingen holdetast
    }

    // ======================= navnet =======================

    [Fact]
    public void Navnet_er_det_der_staar_paa_tasten()
    {
        // «Ctrl+Shift+,» og ikke «Ctrl+Shift+Delete». Windows kalder tasten en
        // Delete, naar Shift holdes nede - men paa tasten staar der et komma,
        // og det er dét, brugeren ser.
        Assert.Equal("Ctrl+Shift+,", CtrlShift(PaaTal).Navn());
        Assert.Equal("Ctrl+Shift+,", CtrlShift(VedM).Navn());
    }

    [Fact]
    public void Hvor_tasten_sidder_staar_ved_siden_af_navnet()
    {
        // BEGGE taster naevnes, og de to greb siger det samme - for de ER det
        // samme. Foer stod her, at de skulle sige noget FORSKELLIGT, og det
        // var netop den forskel, ingen kunne se paa skaermen.
        foreach (var hvor in new[] { CtrlShift(PaaTal).Hvor(), CtrlShift(VedM).Hvor() })
        {
            Assert.Contains("taltastatur", hvor);
            Assert.Contains("M", hvor);
        }

        Assert.Equal(CtrlShift(PaaTal).Hvor(), CtrlShift(VedM).Hvor());
    }

    [Fact]
    public void Standarden_er_kommaet_ved_M_med_ctrl_og_shift()
    {
        var s = Genvejsgreb.Standard;

        Assert.Equal(VedM, s.Scancode);
        Assert.True(s.Ctrl);
        Assert.True(s.Shift);
        Assert.False(s.Alt);
        Assert.True(s.Duer);
    }

    // ==================== TVILLINGEN ====================
    //
    // Et dansk tastatur har to kommataster. Den gamle loesning registrerede
    // dem begge; da genvejen blev bygget om, fulgte tvillingen ikke med.

    [Fact]
    public void Genvej_paa_taltastaturets_komma_virker_ogsaa_paa_det_ved_siden_af_M()
    {
        // Det greb, der laa gemt paa maskinen 05-09-2026: «cs:53».
        var greb = new Genvejsgreb(Genvejsgreb.Taltastaturkomma,
                                   Udvidet: false, Ctrl: true, Shift: true, Alt: false);

        Assert.True(greb.Passer(Genvejsgreb.Komma, udvidet: false,
                                ctrl: true, shift: true, alt: false));
    }

    [Fact]
    public void Og_den_anden_vej()
    {
        var greb = new Genvejsgreb(Genvejsgreb.Komma,
                                   Udvidet: false, Ctrl: true, Shift: true, Alt: false);

        Assert.True(greb.Passer(Genvejsgreb.Taltastaturkomma, udvidet: false,
                                ctrl: true, shift: true, alt: false));
    }

    [Fact]
    public void Delete_over_piletasterne_er_ikke_et_komma()
    {
        // Samme scancode som taltastaturets komma. Kun det udvidede flag
        // skiller dem, og uden det ville genvejen udloese sig selv paa Delete.
        var greb = new Genvejsgreb(Genvejsgreb.Taltastaturkomma,
                                   Udvidet: false, Ctrl: true, Shift: true, Alt: false);

        Assert.False(greb.Passer(Genvejsgreb.Taltastaturkomma, udvidet: true,
                                 ctrl: true, shift: true, alt: false));
    }

    [Fact]
    public void Punktummet_har_ingen_tvilling()
    {
        var greb = new Genvejsgreb(Genvejsgreb.Punktum,
                                   Udvidet: false, Ctrl: true, Shift: true, Alt: false);

        Assert.False(greb.Passer(Genvejsgreb.Komma, udvidet: false,
                                 ctrl: true, shift: true, alt: false));
        Assert.Equal(0u, Genvejsgreb.Tvillingen(Genvejsgreb.Punktum));
    }

    [Fact]
    public void Holdetasterne_skal_stadig_passe_paa_tvillingen()
    {
        var greb = new Genvejsgreb(Genvejsgreb.Taltastaturkomma,
                                   Udvidet: false, Ctrl: true, Shift: true, Alt: false);

        Assert.False(greb.Passer(Genvejsgreb.Komma, udvidet: false,
                                 ctrl: true, shift: false, alt: false));
    }

    [Fact]
    public void Skaermen_siger_at_begge_kommataster_taeller()
    {
        var greb = new Genvejsgreb(Genvejsgreb.Taltastaturkomma,
                                   Udvidet: false, Ctrl: true, Shift: true, Alt: false);

        Assert.Contains("begge", greb.Hvor());
        Assert.Equal("Ctrl+Shift+,", greb.Navn());
    }
}
