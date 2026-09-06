namespace NoteApp.Core;

/// <summary>
/// Klipper et stykke ud af en wav-fil og lægger det i en ny.
/// </summary>
/// <remarks>
/// ============ DEN LÆSER IKKE FILEN IND ============
///
/// <see cref="Stilhedsklip"/> gør det, og det er rigtigt dér: den skal måle
/// styrken i hele filen for at finde tavsheden. Her skal der kun kopieres et
/// kendt stykke, og kildefilerne er hundrede megabyte. Så headeren læses,
/// stykket findes ved at søge frem i filen, og resten strømmes forbi.
///
/// ============ HEADEREN SKRIVES OM ============
///
/// RIFF- og data-størrelserne står i filens første fyrre bytes og skal passe
/// til det, der faktisk ligger bagefter. Gør de ikke det, spiller filen
/// stadig i de fleste programmer — men længden er forkert, og en optagelse,
/// der siger 40 minutter og varer 60 sekunder, er værre end en, der ikke
/// spiller.
/// </remarks>
public static class Lyduddrag
{
    /// <summary>
    /// Skriver <paramref name="sekunder"/> af <paramref name="kilde"/> fra
    /// <paramref name="fra"/> ned i <paramref name="maal"/>.
    /// </summary>
    /// <returns>Længden i sekunder på det, der faktisk blev skrevet. 0 ved fejl.</returns>
    public static double Klip(string kilde, string maal, double fra, double sekunder)
    {
        if (!File.Exists(kilde) || sekunder <= 0) return 0;

        try
        {
            using var ind = File.OpenRead(kilde);

            // Headeren er kort. 4 KB rækker til enhver almindelig wav, og
            // FindData giver op efter det samme.
            var hoved = new byte[4096];
            var laest = ind.Read(hoved, 0, hoved.Length);
            if (laest < 44) return 0;

            var data = FindData(hoved, laest);
            if (data < 0) return 0;

            // Bytes pr. sekund står i fmt-blokken. Den er filens eget svar og
            // gælder også for en fil, der ikke er 16 kHz mono.
            var prSekund = (int)BitConverter.ToUInt32(hoved, 28);
            var blok = Math.Max(1, (int)BitConverter.ToUInt16(hoved, 32));

            if (prSekund <= 0) return 0;

            var lyd = ind.Length - data;
            if (lyd <= 0) return 0;

            // Start på en hel prøve. Midt i en prøve giver et knæk ved
            // begyndelsen af klippet.
            var start = (long)(fra * prSekund);
            start -= start % blok;
            start = Math.Max(0, Math.Min(start, lyd));

            var laengde = (long)(sekunder * prSekund);
            laengde -= laengde % blok;
            laengde = Math.Min(laengde, lyd - start);

            if (laengde <= 0) return 0;

            Directory.CreateDirectory(Path.GetDirectoryName(maal)!);

            var midlertidig = maal + ".ny";

            using (var ud = new FileStream(midlertidig, FileMode.Create, FileAccess.Write))
            {
                var nyt = new byte[data];
                Array.Copy(hoved, nyt, data);

                Skriv32(nyt, 4, (int)(laengde + data - 8));
                Skriv32(nyt, data - 4, (int)laengde);

                ud.Write(nyt, 0, data);

                ind.Seek(data + start, SeekOrigin.Begin);

                var bunke = new byte[64 * 1024];
                var tilbage = laengde;

                while (tilbage > 0)
                {
                    var n = ind.Read(bunke, 0, (int)Math.Min(bunke.Length, tilbage));
                    if (n <= 0) break;

                    ud.Write(bunke, 0, n);
                    tilbage -= n;
                }
            }

            File.Move(midlertidig, maal, overwrite: true);
            return Math.Round(laengde / (double)prSekund, 1);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Hvor lyden begynder i filen. -1 hvis den ikke kan findes.</summary>
    private static int FindData(byte[] b, int laengde)
    {
        var p = 12;

        while (p + 8 < laengde && p < 4096)
        {
            var id = System.Text.Encoding.ASCII.GetString(b, p, 4);
            var sz = BitConverter.ToInt32(b, p + 4);

            if (id == "data") return p + 8;
            if (sz <= 0) return -1;

            p += 8 + sz + (sz % 2);
        }

        return -1;
    }

    private static void Skriv32(byte[] b, int ved, int vaerdi)
    {
        if (ved < 0 || ved + 4 > b.Length) return;
        BitConverter.GetBytes(vaerdi).CopyTo(b, ved);
    }
}
