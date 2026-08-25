using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Oprydningen af lydfiler — de tre regler, der ikke må brydes.
///
/// DET HER ER DE VIGTIGSTE PRØVER I HELE PROJEKTET. En fejl her sletter
/// brugerens optagelser, det kan ikke fortrydes, og det opdages måneder
/// senere, når nogen leder efter et møde.
/// </summary>
public class LydoprydningTest
{
    // ===================== REGEL 1: ALDRIG UDEN EN UDSKRIFT =====================

    [Fact]
    public void Gammel_optagelse_uden_udskrift_roeres_ikke()
    {
        using var p = new Proevemappe();

        var mappe = p.Optagelse("uden-udskrift", medUdskrift: false);

        // Uden teksten er der ikke noget tilbage bagefter. Saa er det ikke
        // oprydning - saa er det sletning.
        Assert.Empty(Lydoprydning.Kandidater(1));

        var (filer, _) = Lydoprydning.Ryd(1);

        Assert.Equal(0, filer);
        Assert.True(File.Exists(Path.Combine(mappe, "mikrofon.wav")));
    }

    // ===================== REGEL 2: ALDRIG VED NUL DAGE ELLER MINDRE =====================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3650)]
    public void Nul_dage_eller_mindre_rydder_ingenting(int dage)
    {
        using var p = new Proevemappe();

        var mappe = p.Optagelse("gammel", dageSiden: 5000);

        // Nul dage kunne laeses som "slet alt med det samme". Det er praecis
        // den laesning, der skal vaere umulig: en indstilling, der ved et uheld
        // bliver nul, maa ikke toemme arkivet.
        Assert.Empty(Lydoprydning.Kandidater(dage));

        var (filer, bytes) = Lydoprydning.Ryd(dage);

        Assert.Equal(0, filer);
        Assert.Equal(0, bytes);
        Assert.True(File.Exists(Path.Combine(mappe, "mikrofon.wav")));
    }

    // ===================== REGEL 3: ALDRIG ANDET END LYD =====================

    [Fact]
    public void Kun_lydfilerne_slettes()
    {
        using var p = new Proevemappe();

        var mappe = p.Optagelse("gammel", dageSiden: 400, medLoopback: true);

        var noter = Proevemappe.Fremmedfil(mappe, "noter.jsonl");
        var doku = Proevemappe.Fremmedfil(mappe, "referat.docx");
        var meta = Path.Combine(mappe, "meeting.json");
        File.WriteAllText(meta, "{}");

        var (filer, _) = Lydoprydning.Ryd(365);

        Assert.Equal(2, filer);   // mikrofon.wav og loopback.wav

        Assert.False(File.Exists(Path.Combine(mappe, "mikrofon.wav")));
        Assert.False(File.Exists(Path.Combine(mappe, "loopback.wav")));

        // Alt andet staar uroert - ogsaa udskriften, som er hele pointen.
        Assert.True(File.Exists(noter));
        Assert.True(File.Exists(doku));
        Assert.True(File.Exists(meta));
        Assert.True(File.Exists(Path.Combine(mappe, "udskrift_large-v3.txt")));
    }

    // ===================== ALDEREN MAALES PAA UDSKRIFTEN =====================

    [Fact]
    public void For_ung_udskrift_rydder_ikke()
    {
        using var p = new Proevemappe();

        var mappe = p.Optagelse("i-gaar", dageSiden: 1);

        Assert.Empty(Lydoprydning.Kandidater(365));
        Assert.True(File.Exists(Path.Combine(mappe, "mikrofon.wav")));
    }

    [Fact]
    public void Praecis_paa_graensen_rydder()
    {
        using var p = new Proevemappe();

        // 365 dage gammel ved en graense paa 365: alder < dage springer over,
        // saa den her SKAL med. Graensetilfaeldet er skrevet ned, fordi det er
        // det eneste sted, en fortegnsfejl kan gemme sig.
        p.Optagelse("paa-graensen", dageSiden: 365);

        Assert.Single(Lydoprydning.Kandidater(365));
    }

    // ===================== EN ENKELT MAPPE =====================

    [Fact]
    public void RydEn_rydder_kun_den_mappe()
    {
        using var p = new Proevemappe();

        var en = p.Optagelse("den-ene", lydBytes: 2048);
        var anden = p.Optagelse("den-anden", lydBytes: 2048);

        var bytes = Lydoprydning.RydEn(en);

        Assert.Equal(2048, bytes);
        Assert.False(File.Exists(Path.Combine(en, "mikrofon.wav")));
        Assert.True(File.Exists(Path.Combine(anden, "mikrofon.wav")));
    }

    [Fact]
    public void RydEn_uden_udskrift_rydder_ikke()
    {
        using var p = new Proevemappe();

        var mappe = p.Optagelse("uden", medUdskrift: false);

        Assert.Equal(0, Lydoprydning.RydEn(mappe));
        Assert.True(File.Exists(Path.Combine(mappe, "mikrofon.wav")));
    }

    // ===================== OPGOERELSEN =====================

    [Fact]
    public void Lydstoerrelse_taeller_begge_spor_og_intet_andet()
    {
        using var p = new Proevemappe();

        var mappe = p.Optagelse("to-spor", lydBytes: 1000, medLoopback: true);
        Proevemappe.Fremmedfil(mappe, "stor-fil.bin", 999999);

        Assert.Equal(2000, Lydoprydning.Lydstoerrelse(mappe));
    }
}
