using System.Text;

namespace NoteApp.Core;

/// <summary>
/// Forstærker et spor, hvor der tales for lavt, før det skrives ud.
/// </summary>
/// <remarks>
/// ============ HVORFOR ============
///
/// En Jabra SPEAK 510 på bordet giver din egen stemme omkring −32 dBFS målt
/// her (tre opkald 08-10/09-2026), mod −19 med mikrofonen tæt på.
///
/// MÅLT 10-09-2026: otte klip à to minutter fra et møde med god lyd, dæmpet
/// 14 dB til højttalerniveau og skrevet ud med og uden forstærkning. Ordfejl
/// mod det samme klip i fuld styrke:
///
///   uden forstærkning   8,4 % i gennemsnit   ca. 151 ordfejl
///   med                 6,1 %                ca. 111 ordfejl
///
/// Fire klip blev bedre, to lige gode og to værre. Det er en gevinst i
/// gennemsnit, ikke på hvert klip — og det skal stå her, så ingen tror noget
/// andet. Whisper svarer ens på den samme fil (målt: 0,0 % forskel ved
/// gentagelse), så forskellene er lydens, ikke tilfældighedens.
///
/// ============ DET RØRER IKKE OPTAGELSEN ============
///
/// Der skrives en kopi ved siden af, og det er kopien, der skrives ud.
/// Lydfilen på disken er den, der blev optaget, og det skal den blive ved med
/// at være.
///
/// ============ KUN NÅR DET ER FOR LAVT ============
///
/// Et spor, der allerede ligger over <see cref="Graense"/>, røres ikke. Det
/// er de fleste — og at forstærke dem ville kun flytte støjen op.
/// </remarks>
public static class Lydniveau
{
    /// <summary>Talen løftes hertil. Omtrent dér, et headset ligger.</summary>
    public const double Maal = -20;

    /// <summary>Under det her forstærkes der. Over det røres sporet ikke.</summary>
    public const double Graense = -28;

    /// <summary>Aldrig mere end det her. Så bliver støjen det, man hører.</summary>
    public const double MaksForstaerkning = 20;

    /// <summary>
    /// Talens niveau i dBFS — ikke hele filens.
    /// </summary>
    /// <remarks>
    /// Et opkald er for en stor del stilhed: den anden taler. Et gennemsnit af
    /// HELE filen ville derfor ligge langt under talen og give alt for meget
    /// forstærkning. Der måles i stykker à 20 ms, og kun de stykker tæller,
    /// der ligger inden for 20 dB af de kraftigste — det er talen.
    ///
    /// Null, hvis der ikke er noget, der ligner tale.
    /// </remarks>
    public static double? Taleniveau(ReadOnlySpan<short> x, int rate)
    {
        var bid = Math.Max(1, rate / 50);
        var stykker = new List<double>();

        for (var i = 0; i + bid <= x.Length; i += bid)
        {
            double sum = 0;
            for (var k = i; k < i + bid; k++) sum += (double)x[k] * x[k];

            var rms = Math.Sqrt(sum / bid) / 32768;
            if (rms > 0) stykker.Add(20 * Math.Log10(rms));
        }

        if (stykker.Count == 0) return null;

        stykker.Sort();
        var top = stykker[(int)(stykker.Count * 0.95)];

        var tale = stykker.Where(d => d > top - 20 && d > -60).ToList();
        if (tale.Count == 0) return null;

        // Energigennemsnit, ikke gennemsnit af decibel.
        var energi = tale.Average(d => Math.Pow(10, d / 10));
        return 10 * Math.Log10(energi);
    }

    /// <summary>Hvor meget sporet skal løftes. 0, når det ikke skal.</summary>
    public static double Forstaerkning(double? taleniveau) =>
        taleniveau is not { } n || n >= Graense ? 0 : Math.Min(Maal - n, MaksForstaerkning);

    /// <summary>
    /// Skriver en forstærket kopi, hvis talen ligger for lavt.
    /// </summary>
    /// <returns>Forstærkningen i dB, eller 0 hvis der ikke blev skrevet noget.</returns>
    /// <remarks>
    /// Kun 16-bit PCM, som appen selv optager. Alt andet efterlades urørt, og
    /// så skrives originalen ud som før.
    /// </remarks>
    public static double Forstaerk(string ind, string ud)
    {
        if (!Laes(ind, out var x, out var rate, out var kanaler) || kanaler != 1) return 0;

        var db = Forstaerkning(Taleniveau(x, rate));
        if (db <= 0) return 0;

        Skriv(ud, Loeft(x, db), rate);
        return db;
    }

    /// <summary>
    /// Løfter signalet — med en blød loftgrænse, så en dør, der smækker,
    /// ikke bliver til forvrængning.
    /// </summary>
    /// <remarks>
    /// Talen skal op; de få toppe, der så ville gå over 0 dBFS, bøjes blødt
    /// af over 90 % i stedet for at blive klippet. Klipning giver skarpe
    /// knæk, som modellen hører som konsonanter.
    /// </remarks>
    public static short[] Loeft(ReadOnlySpan<short> x, double db)
    {
        var g = Math.Pow(10, db / 20);
        var y = new short[x.Length];

        const double knae = 0.9;

        for (var i = 0; i < x.Length; i++)
        {
            var v = x[i] / 32768.0 * g;
            var a = Math.Abs(v);

            if (a > knae) a = knae + (1 - knae) * Math.Tanh((a - knae) / (1 - knae));

            y[i] = (short)Math.Round(Math.Sign(v) * a * 32767);
        }

        return y;
    }

    /// <summary>Læser en PCM-fil. Leder efter bidderne i stedet for at gå ud fra 44 byte.</summary>
    private static bool Laes(string sti, out short[] x, out int rate, out int kanaler)
    {
        x = Array.Empty<short>();
        rate = 0;
        kanaler = 0;

        try
        {
            using var r = new BinaryReader(File.OpenRead(sti));

            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "RIFF") return false;
            r.ReadInt32();
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "WAVE") return false;

            short format = 0, bits = 0;

            while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
            {
                var id = Encoding.ASCII.GetString(r.ReadBytes(4));
                var laengde = r.ReadInt32();
                var start = r.BaseStream.Position;

                if (id == "fmt ")
                {
                    format = r.ReadInt16();
                    kanaler = r.ReadInt16();
                    rate = r.ReadInt32();
                    r.ReadInt32();
                    r.ReadInt16();
                    bits = r.ReadInt16();
                }
                else if (id == "data")
                {
                    if (format != 1 || bits != 16) return false;

                    var n = (int)Math.Min(laengde, r.BaseStream.Length - start) / 2;
                    var b = r.ReadBytes(n * 2);
                    x = new short[n];
                    Buffer.BlockCopy(b, 0, x, 0, n * 2);
                    return rate > 0;
                }

                r.BaseStream.Position = start + laengde + (laengde & 1);
            }
        }
        catch (Exception)
        {
            // En fil, der ikke kan laeses, forstaerkes ikke. Originalen skrives ud.
        }

        return false;
    }

    /// <summary>Skriver 16-bit mono PCM med det almindelige 44-byte-hoved.</summary>
    private static void Skriv(string sti, short[] y, int rate)
    {
        using var w = new BinaryWriter(File.Create(sti));

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + y.Length * 2);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(rate);
        w.Write(rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(y.Length * 2);

        var b = new byte[y.Length * 2];
        Buffer.BlockCopy(y, 0, b, 0, b.Length);
        w.Write(b);
    }
}
