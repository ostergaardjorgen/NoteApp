using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Målingen fra 05-09-2026, spillet om igen.
/// </summary>
/// <remarks>
/// Genvejen blev rettet tre gange for den samme fejl, fordi der ikke lå en
/// måling til grund. Den ligger her nu: de tastetryk, hooken FAKTISK så, er
/// skrevet af som de kom, og prøven siger, hvad Shift stod på undervejs.
///
/// Går nogen tilbage til at spørge Windows, dumper den her.
/// </remarks>
public class ModifikatorsporTest
{
    /// <summary>Et tastetryk, som hooken fik det.</summary>
    private readonly record struct Haendelse(uint Vk, uint Scan, bool Ned);

    // ============ MAALT: NUMLOCK TAENDT, CTRL+SHIFT, TALTASTATURETS KOMMA ============
    //
    // scan   vk    ned/op
    // 0x1D   0xA2  NED     Ctrl ned
    // 0x2A   0xA0  NED     Shift ned
    // 0x22A  0xA0  OP      OPFUNDET af Windows - ingen slap noget
    // 0x53   0x2E  NED     kommaet
    // 0x53   0x2E  OP
    // 0x2A   0xA0  NED     Windows saetter Shift tilbage
    // 0x2A   0xA0  OP      fingeren slipper for alvor
    private static readonly Haendelse[] MedNumlock =
    {
        new(0xA2, 0x1D, true),
        new(0xA0, 0x2A, true),
        new(0xA0, 0x22A, false),
        new(0x2E, 0x53, true),
        new(0x2E, 0x53, false),
        new(0xA0, 0x2A, true),
        new(0xA0, 0x2A, false),
    };

    // Samme greb med NumLock SLUKKET. Da er der intet at vende om, og Windows
    // finder ikke noget paa.
    private static readonly Haendelse[] UdenNumlock =
    {
        new(0xA2, 0x1D, true),
        new(0xA0, 0x2A, true),
        new(0x2E, 0x53, true),
        new(0x2E, 0x53, false),
        new(0xA0, 0x2A, false),
        new(0xA2, 0x1D, false),
    };

    [Fact]
    public void Det_opfundne_shift_slip_taeller_ikke()
    {
        var spor = new Modifikatorspor();

        // Frem til kommaet: Ctrl ned, Shift ned, og Windows' opfundne slip.
        foreach (var h in MedNumlock[..3]) spor.Se(h.Vk, h.Scan, h.Ned);

        Assert.True(spor.Ctrl);
        Assert.True(spor.Shift);   // fingeren ligger stadig paa tasten
        Assert.False(spor.Alt);
    }

    [Fact]
    public void Kommaet_paa_taltastaturet_er_ikke_en_holdetast()
    {
        var spor = new Modifikatorspor();

        // Kommaet kommer ind som vk 0x2E (Delete), fordi Shift vender NumLock
        // om. Det er ikke en modifikator, og bogen skal svare falsk paa det -
        // ellers ville hooken sende tasten videre i stedet for at gribe den.
        Assert.False(spor.Se(0x2E, 0x53, ned: true));
    }

    [Fact]
    public void Hele_grebet_passer_med_numlock_taendt()
    {
        var spor = new Modifikatorspor();
        var passede = false;

        foreach (var h in MedNumlock)
        {
            if (spor.Se(h.Vk, h.Scan, h.Ned)) continue;

            // Ikke en holdetast: saa er det tasten selv.
            if (h.Ned &&
                new Genvejsgreb(0x53, Udvidet: false, Ctrl: true, Shift: true, Alt: false)
                    .Passer(h.Scan, udvidet: false, spor.Ctrl, spor.Shift, spor.Alt))
                passede = true;
        }

        Assert.True(passede, "Ctrl+Shift+taltastaturets komma skal udloese genvejen");
    }

    [Fact]
    public void Hele_grebet_passer_ogsaa_med_numlock_slukket()
    {
        var spor = new Modifikatorspor();
        var passede = false;

        foreach (var h in UdenNumlock)
        {
            if (spor.Se(h.Vk, h.Scan, h.Ned)) continue;

            if (h.Ned &&
                new Genvejsgreb(0x53, Udvidet: false, Ctrl: true, Shift: true, Alt: false)
                    .Passer(h.Scan, udvidet: false, spor.Ctrl, spor.Shift, spor.Alt))
                passede = true;
        }

        Assert.True(passede);
    }

    [Fact]
    public void Shift_slippes_naar_fingeren_slipper()
    {
        var spor = new Modifikatorspor();

        foreach (var h in MedNumlock) spor.Se(h.Vk, h.Scan, h.Ned);

        // Sidste haendelse er det RIGTIGE slip. Det skal taelle.
        Assert.False(spor.Shift);
    }

    [Theory]
    [InlineData(0x10, 0x2A)]    // Shift, den generelle kode
    [InlineData(0xA0, 0x2A)]    // venstre Shift
    [InlineData(0xA1, 0x36)]    // hoejre Shift
    public void Alle_tre_shift_koder_taeller(uint vk, uint scan)
    {
        var spor = new Modifikatorspor();

        Assert.True(spor.Se(vk, scan, ned: true));
        Assert.True(spor.Shift);

        Assert.True(spor.Se(vk, scan, ned: false));
        Assert.False(spor.Shift);
    }

    [Fact]
    public void Udgangsstillingen_gaelder_indtil_der_kommer_en_haendelse()
    {
        var spor = new Modifikatorspor();
        spor.Udgangsstilling(ctrl: true, shift: true, alt: false);

        Assert.True(spor.Ctrl);
        Assert.True(spor.Shift);

        spor.Se(0xA2, 0x1D, ned: false);
        Assert.False(spor.Ctrl);
        Assert.True(spor.Shift);
    }

    [Fact]
    public void En_almindelig_tast_roerer_ikke_bogen()
    {
        var spor = new Modifikatorspor();
        spor.Se(0xA2, 0x1D, ned: true);

        Assert.False(spor.Se(0x41, 0x1E, ned: true));   // A
        Assert.True(spor.Ctrl);
    }

    [Fact]
    public void Opfundne_tryk_taeller_heller_ikke()
    {
        var spor = new Modifikatorspor();

        // Den anden vej: et opfundet TRYK maa heller ikke saette bogen, for
        // saa ville en genvej kunne udloese sig selv uden en finger.
        Assert.True(spor.Se(0xA0, Modifikatorspor.Opfundet | 0x2A, ned: true));
        Assert.False(spor.Shift);
    }
}
