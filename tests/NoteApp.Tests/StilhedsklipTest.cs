using System.IO;
using NAudio.Wave;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Tavsheden skal vaek, foer klippet skrives ud.
/// </summary>
/// <remarks>
/// WHISPER FINDER PAA ORD I STILHED.
///
/// En diktering fra vaageordet begynder ti sekunder BAGUD i tiden, saa intet
/// af det, man sagde, kan naa at falde ud. Prisen er, at klippet som regel
/// begynder med flere sekunders tavshed.
///
/// MAALT 31-08-2026, to dikteringer i traek med samme opsaetning:
///
///   29 sekunder  ->  «Vi vil gerne bede om at faa noteret IBM, StorageTek ...»
///   16 sekunder  ->  «Og med Ibum klokkvoels.»
///
/// Den anden var kortere, altsaa mest tavshed. «klokkvoels» er ikke et ord.
/// </remarks>
public class StilhedsklipTest
{
    private const int Frekvens = 16000;

    /// <summary>Laver en wav med stilhed, saa lyd, saa stilhed.</summary>
    private static string Klip(double stilhedFoer, double lyd, double stilhedEfter,
                               int frekvens = Frekvens)
    {
        var sti = Path.Combine(Path.GetTempPath(), "proeve-" + Guid.NewGuid().ToString("N")[..8] + ".wav");

        var ialt = (int)((stilhedFoer + lyd + stilhedEfter) * frekvens);
        var proever = new short[ialt];

        var fra = (int)(stilhedFoer * frekvens);
        var til = fra + (int)(lyd * frekvens);

        // En tone, der er tydeligt over tavshedsgraensen.
        for (var i = fra; i < til && i < proever.Length; i++)
            proever[i] = (short)(8000 * Math.Sin(i * 0.05));

        using (var w = new WaveFileWriter(sti, new WaveFormat(frekvens, 16, 1)))
        {
            var bytes = new byte[proever.Length * 2];
            Buffer.BlockCopy(proever, 0, bytes, 0, bytes.Length);
            w.Write(bytes, 0, bytes.Length);
        }

        return sti;
    }

    private static double Sekunder(string wav)
    {
        using var r = new WaveFileReader(wav);
        return r.TotalTime.TotalSeconds;
    }

    [Fact]
    public void Tavshed_foran_klippes_vaek()
    {
        var sti = Klip(stilhedFoer: 8, lyd: 3, stilhedEfter: 0.2);

        try
        {
            var foer = Sekunder(sti);
            var klippet = Stilhedsklip.KlipHovedet(sti);

            Assert.True(klippet > 6, $"Der blev kun klippet {klippet:0.0} sek af otte.");
            Assert.True(Sekunder(sti) < foer);
        }
        finally { File.Delete(sti); }
    }

    /// <summary>
    /// Men ikke helt ind til foerste lyd.
    /// </summary>
    /// <remarks>
    /// Klippes der helt ind, ryger begyndelsen af det foerste ord. Modellen
    /// har brug for at hoere ordet starte.
    /// </remarks>
    [Fact]
    public void Der_beholdes_et_oejeblik_foran()
    {
        var sti = Klip(stilhedFoer: 5, lyd: 2, stilhedEfter: 0.2);

        try
        {
            Stilhedsklip.KlipHovedet(sti);

            // To sekunders lyd plus det, der blev beholdt foran.
            Assert.True(Sekunder(sti) > 2.2, "Der blev klippet helt ind til foerste lyd.");
        }
        finally { File.Delete(sti); }
    }

    [Fact]
    public void Et_klip_uden_tavshed_foran_roeres_ikke()
    {
        var sti = Klip(stilhedFoer: 0, lyd: 3, stilhedEfter: 0.2);

        try
        {
            Assert.Equal(0, Stilhedsklip.KlipHovedet(sti));
        }
        finally { File.Delete(sti); }
    }

    /// <summary>
    /// En helt tavs optagelse skal kunne SES som tavs.
    /// </summary>
    /// <remarks>
    /// Klippes den til nul sekunder, ligner den en fil, der gik galt. Det
    /// gjorde den ikke - der blev bare ikke sagt noget.
    /// </remarks>
    [Fact]
    public void En_helt_tavs_optagelse_roeres_ikke()
    {
        var sti = Klip(stilhedFoer: 4, lyd: 0, stilhedEfter: 0);

        try
        {
            Assert.Equal(0, Stilhedsklip.KlipHovedet(sti));
            Assert.True(Sekunder(sti) > 3.5);
        }
        finally { File.Delete(sti); }
    }

    [Fact]
    public void Hoved_og_hale_kan_klippes_i_samme_fil()
    {
        var sti = Klip(stilhedFoer: 6, lyd: 2, stilhedEfter: 6);

        try
        {
            Stilhedsklip.KlipHovedet(sti);
            Stilhedsklip.KlipHalen(sti);

            // To sekunders tale plus det, der beholdes i hver ende.
            Assert.True(Sekunder(sti) < 5, $"Klippet er stadig {Sekunder(sti):0.0} sek af fjorten.");
        }
        finally { File.Delete(sti); }
    }

    // ============ FREKVENSEN ============

    /// <summary>
    /// En diktering optages i MIKROFONENS frekvens, ikke appens.
    /// </summary>
    /// <remarks>
    /// Moederne optages i 16 kHz, som appen selv vaelger. En diktering fra
    /// vaageordet optages i det, enheden leverer - typisk 48 kHz.
    ///
    /// Klipperne regnede sekunder ud fra en FAST frekvens paa 16 kHz. Paa et
    /// 48 kHz-klip blev hvert klippet sekund derfor talt tre gange, og en
    /// saetning paa fem sekunder endte under et halvt og blev afvist med «for
    /// kort til et diktat». Maalt 31-08-2026.
    /// </remarks>
    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void Det_klippede_maales_i_filens_egen_frekvens(int frekvens)
    {
        var sti = Klip(stilhedFoer: 6, lyd: 2, stilhedEfter: 0.2, frekvens: frekvens);

        try
        {
            var klippet = Stilhedsklip.KlipHovedet(sti);

            // Der blev klippet cirka 5,5 sekunder, uanset frekvensen.
            Assert.InRange(klippet, 5.0, 6.0);
        }
        finally { File.Delete(sti); }
    }

    /// <summary>
    /// Laengden laeses af filen og regnes ikke ud.
    /// </summary>
    /// <remarks>
    /// Filen ved, hvor lang den er, og den kan ikke tage fejl af sig selv.
    /// </remarks>
    [Theory]
    [InlineData(16000)]
    [InlineData(48000)]
    public void Laengden_laeses_af_filen(int frekvens)
    {
        var sti = Klip(stilhedFoer: 1, lyd: 3, stilhedEfter: 1, frekvens: frekvens);

        try
        {
            Assert.InRange(Stilhedsklip.Sekunder(sti), 4.9, 5.1);
        }
        finally { File.Delete(sti); }
    }

    /// <summary>
    /// Og efter klipningen er der stadig noget tilbage at skrive ud.
    /// </summary>
    /// <remarks>
    /// Det var dét, der gik galt: en rigtig saetning blev afvist som «for kort
    /// til et diktat», fordi der blev trukket tre gange for meget fra.
    /// </remarks>
    [Fact]
    public void En_rigtig_saetning_overlever_klipningen()
    {
        var sti = Klip(stilhedFoer: 9, lyd: 5, stilhedEfter: 3, frekvens: 48000);

        try
        {
            Stilhedsklip.KlipHovedet(sti);
            Stilhedsklip.KlipHalen(sti);

            Assert.True(Stilhedsklip.Sekunder(sti) > 0.5,
                $"Klippet er {Stilhedsklip.Sekunder(sti):0.00} sek - det ville blive afvist.");
        }
        finally { File.Delete(sti); }
    }
}
