namespace NoteApp.Core;

/// <summary>
/// Klipper tavsheden af enden på en optagelse.
///
/// HVORFOR DEN FINDES
///
/// Et webinar kan optages, mens man er et andet sted. Optagelsen stopper af sig
/// selv, når der ikke har været lyd i fem minutter — men de fem minutter er
/// allerede optaget, når beslutningen træffes. Uden det her ville hver eneste
/// webinaroptagelse slutte med et kvarters tomhed: fem minutter fra stoppet,
/// plus den pause, der gik forud for, at nogen lukkede mødet.
///
/// Det er ikke kun spildplads. Whisper skriver stilhed ud som hallucinationer
/// — «Undertekster af Ai-Media», «Teksting av Nicolai Winther» — og de skal
/// fjernes bagefter af <see cref="Samtale"/>. Klippes tavsheden væk først,
/// opstår de aldrig.
///
/// DEN KLIPPER KUN I ENDEN
///
/// Pauser midt i et webinar er en del af webinaret: en oplægsholder, der tier
/// mens et videoklip loader, eller et spørgsmål, ingen svarer på. Klippes de
/// væk, skrider tidsstemplerne, og udskriften kan ikke længere holdes op mod
/// lyden. Kun halen skæres.
/// </summary>
public static class Stilhedsklip
{
    /// <summary>
    /// Under det her regnes et 30 ms-vindue for tavst.
    ///
    /// Absolut og ikke relativt — modsat opdelingen i måleskripterne. Grunden
    /// er, at der her ikke findes et «klippets største udsving» at måle mod:
    /// en optagelse, der ER helt tavs, ville få sin egen baggrundsstøj som
    /// målestok og se ud til at indeholde tale.
    /// </summary>
    private const double Tavshedsgraense = 90.0;

    private const int Vindue = 480;   // 30 ms ved 16 kHz

    /// <summary>
    /// Fjerner tavsheden fra BEGYNDELSEN af en wav-fil.
    /// </summary>
    /// <remarks>
    /// FORDI WHISPER FINDER PÅ ORD I STILHED.
    ///
    /// En diktering fra vågeordet begynder ti sekunder BAGUD i tiden, så
    /// intet af det, man sagde, kan nå at falde ud. Prisen er, at klippet
    /// som regel begynder med flere sekunders tavshed — man sagde jo ikke
    /// noget, før man sagde noget.
    ///
    /// MÅLT 31-08-2026, to dikteringer i træk med samme opsætning:
    ///
    ///   29 sekunder  →  «Vi vil gerne bede om at få noteret IBM, StorageTek …»
    ///   16 sekunder  →  «Og med Ibum klokkvöls.»
    ///
    /// Den anden var kortere, altså mest tavshed. «klokkvöls» er ikke et ord;
    /// det er en model, der bliver bedt om at skrive noget ud af ingenting og
    /// gør sit bedste. Hallucination i stilhed er en kendt egenskab ved
    /// whisper, ikke en fejl i lyden.
    ///
    /// Der beholdes et halvt sekund foran. Klippes der helt ind til første
    /// lyd, ryger begyndelsen af det første ord — modellen har brug for at
    /// høre ordet starte.
    /// </remarks>
    /// <returns>Hvor mange sekunder der blev klippet af. 0 hvis intet.</returns>
    public static double KlipHovedet(string wav, double behold = 0.5)
    {
        if (!File.Exists(wav)) return 0;

        byte[] b;
        try { b = File.ReadAllBytes(wav); }
        catch (IOException) { return 0; }

        var data = FindData(b);
        if (data < 0) return 0;

        var lyd = b.Length - data;
        if (lyd <= 0) return 0;

        var proever = lyd / 2;
        var vinduer = proever / Vindue;
        if (vinduer < 2) return 0;

        // Forfra: find det foerste vindue, der IKKE er tavst.
        var foersteLyd = -1;

        for (var v = 0; v < vinduer; v++)
        {
            if (Styrke(b, data, v) <= Tavshedsgraense) continue;
            foersteLyd = v;
            break;
        }

        // Var der overhovedet ingen lyd, roeres filen ikke. En tom optagelse
        // skal kunne ses som en tom optagelse.
        if (foersteLyd < 0) return 0;

        var behold_v = (int)(behold * 1000 / 30);
        var fra = Math.Max(0, foersteLyd - behold_v);

        if (fra == 0) return 0;

        var spring = fra * Vindue * 2;
        var nyeBytes = lyd - spring;

        var klippet = spring / (double)(AudioFormat.SampleRate * 2);

        try
        {
            var midlertidig = wav + ".klippet";

            using (var ud = new FileStream(midlertidig, FileMode.Create, FileAccess.Write))
            {
                var hoved = new byte[data];
                Array.Copy(b, hoved, data);

                Skriv32(hoved, 4, nyeBytes + data - 8);
                Skriv32(hoved, data - 4, nyeBytes);

                ud.Write(hoved, 0, data);
                ud.Write(b, data + spring, nyeBytes);
            }

            File.Move(midlertidig, wav, overwrite: true);
            return klippet;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Fjerner tavsheden fra slutningen af en wav-fil.
    ///
    /// <paramref name="behold"/> er den stilhed, der får lov at blive stående
    /// til sidst — en optagelse, der ender midt i det sidste ord, lyder som en
    /// fejl.
    /// </summary>
    /// <returns>Hvor mange sekunder der blev klippet af. 0 hvis intet.</returns>
    public static double KlipHalen(string wav, double behold = 1.5)
    {
        if (!File.Exists(wav)) return 0;

        byte[] b;
        try { b = File.ReadAllBytes(wav); }
        catch (IOException) { return 0; }

        var data = FindData(b);
        if (data < 0) return 0;

        var lyd = b.Length - data;
        if (lyd <= 0) return 0;

        var proever = lyd / 2;
        var vinduer = proever / Vindue;
        if (vinduer < 2) return 0;

        // Bagfra: find det sidste vindue, der IKKE er tavst.
        var sidsteLyd = -1;

        for (var v = vinduer - 1; v >= 0; v--)
        {
            if (Styrke(b, data, v) <= Tavshedsgraense) continue;
            sidsteLyd = v;
            break;
        }

        // Var der overhovedet ingen lyd, roeres filen ikke. En tom optagelse
        // skal kunne ses som en tom optagelse - ikke som en fil paa nul sekunder.
        if (sidsteLyd < 0) return 0;

        var behold_v = (int)(behold * 1000 / 30);
        var slut = Math.Min(vinduer - 1, sidsteLyd + behold_v);

        var nyeProever = (slut + 1) * Vindue;
        var nyeBytes = nyeProever * 2;

        if (nyeBytes >= lyd) return 0;

        var klippet = (lyd - nyeBytes) / (double)(AudioFormat.SampleRate * 2);

        try
        {
            // Skriv til en ny fil og byt om. Skrives der i den samme, staar der
            // en halv fil, hvis noget gaar galt undervejs - og saa er
            // optagelsen vaek.
            var midlertidig = wav + ".klippet";

            using (var ud = new FileStream(midlertidig, FileMode.Create, FileAccess.Write))
            {
                var hoved = new byte[data];
                Array.Copy(b, hoved, data);

                // RIFF- og data-stoerrelserne skal rettes, ellers staar der i
                // filens eget hoved, at den er laengere end den er. Nogle
                // afspillere retter sig efter hovedet og loeber ud over enden.
                Skriv32(hoved, 4, nyeBytes + data - 8);
                Skriv32(hoved, data - 4, nyeBytes);

                ud.Write(hoved, 0, data);
                ud.Write(b, data, nyeBytes);
            }

            File.Delete(wav);
            File.Move(midlertidig, wav);

            return klippet;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static double Styrke(byte[] b, int data, int vindue)
    {
        var sum = 0.0;
        var talt = 0;
        var fra = data + vindue * Vindue * 2;
        var sidste = b.Length - 2;

        // Hver fjerde proeve raekker. Det er en tavshedsmaaling, ikke en
        // lydanalyse, og det er fire gange hurtigere paa en time lyd.
        for (var i = 0; i < Vindue; i += 4)
        {
            var ved = fra + i * 2;
            if (ved > sidste) break;

            sum += Math.Abs(BitConverter.ToInt16(b, ved));
            talt++;
        }

        return talt > 0 ? sum / talt : 0;
    }

    private static int FindData(byte[] b)
    {
        var p = 12;

        while (p + 8 < b.Length && p < 4096)
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
