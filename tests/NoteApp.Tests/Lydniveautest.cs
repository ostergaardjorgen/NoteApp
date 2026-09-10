using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Forstærkningen af et spor, hvor der tales for lavt. Se Lydniveau.
/// </summary>
public class Lydniveautest
{
    /// <summary>En «samtale»: en tone på det givne niveau i halvdelen af tiden, stilhed resten.</summary>
    private static short[] Samtale(double db, int rate = 16000, int sekunder = 4)
    {
        var a = Math.Pow(10, db / 20) * Math.Sqrt(2) * 32767;
        var x = new short[rate * sekunder];

        for (var i = 0; i < x.Length; i++)
            if (i / rate % 2 == 0)
                x[i] = (short)(a * Math.Sin(2 * Math.PI * 440 * i / rate));

        return x;
    }

    [Theory]
    [InlineData(-38)]
    [InlineData(-24)]
    public void Taleniveauet_er_talens_og_ikke_hele_filens(double db)
    {
        // Halvdelen er stilhed. Et gennemsnit af hele filen ville ligge 3 dB
        // lavere; talen maales, hvor der tales.
        var n = Lydniveau.Taleniveau(Samtale(db), 16000)!.Value;

        Assert.InRange(n, db - 0.5, db + 0.5);
    }

    [Fact]
    public void Stilhed_har_intet_taleniveau() =>
        Assert.Null(Lydniveau.Taleniveau(new short[16000], 16000));

    [Theory]
    [InlineData(-38, 18)]      // Jabra-hoejttaleren i et opkald
    [InlineData(-30, 10)]
    [InlineData(-45, 20)]      // loftet
    [InlineData(-24, 0)]       // et almindeligt moede roeres ikke
    [InlineData(-28, 0)]
    public void Forstaerkningen_loefter_talen_til_maalet(double niveau, double forventet) =>
        Assert.Equal(forventet, Lydniveau.Forstaerkning(niveau), 3);

    [Fact]
    public void Toppe_boejes_af_og_klippes_ikke()
    {
        // En doer, der smaekker: fuldt udslag, forstaerket 20 dB.
        var y = Lydniveau.Loeft(new short[] { 30000, -30000, 100, -100 }, 20);

        Assert.All(y, v => Assert.InRange(Math.Abs((int)v), 0, 32767));
        Assert.InRange((int)y[2], 999, 1001);   // svag lyd: praecis x10
        Assert.True(y[0] > 29490);              // toppen ligger over knaeet, men under loftet
    }

    [Fact]
    public void En_kopi_skrives_og_originalen_roeres_ikke()
    {
        var mappe = Path.Combine(Path.GetTempPath(), "lydniveau-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mappe);

        try
        {
            var ind = Path.Combine(mappe, "mikrofon.wav");
            var ud = Path.Combine(mappe, "forstaerket.wav");

            Skriv(ind, Samtale(-38));
            var foer = File.ReadAllBytes(ind);

            var db = Lydniveau.Forstaerk(ind, ud);

            Assert.Equal(18, db, 1);
            Assert.Equal(foer, File.ReadAllBytes(ind));
            Assert.Equal(foer.Length, new FileInfo(ud).Length);   // samme laengde: tiderne passer

            var loeftet = File.ReadAllBytes(ud)[44..];
            var x = new short[loeftet.Length / 2];
            Buffer.BlockCopy(loeftet, 0, x, 0, loeftet.Length);

            Assert.InRange(Lydniveau.Taleniveau(x, 16000)!.Value, -20.5, -19.5);
        }
        finally
        {
            Directory.Delete(mappe, recursive: true);
        }
    }

    [Fact]
    public void Et_spor_i_orden_faar_ingen_kopi()
    {
        var mappe = Path.Combine(Path.GetTempPath(), "lydniveau-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mappe);

        try
        {
            var ind = Path.Combine(mappe, "mikrofon.wav");
            var ud = Path.Combine(mappe, "forstaerket.wav");

            Skriv(ind, Samtale(-22));

            Assert.Equal(0, Lydniveau.Forstaerk(ind, ud));
            Assert.False(File.Exists(ud));
        }
        finally
        {
            Directory.Delete(mappe, recursive: true);
        }
    }

    private static void Skriv(string sti, short[] x)
    {
        using var w = new BinaryWriter(File.Create(sti));
        w.Write("RIFF"u8.ToArray()); w.Write(36 + x.Length * 2); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(16000); w.Write(32000); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(x.Length * 2);
        foreach (var v in x) w.Write(v);
    }
}
