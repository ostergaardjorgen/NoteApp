using System.IO;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Én sætning fra optagelsen, stillet op mod det, der stod i manuskriptet.
///
/// Tidsrummet er det vigtige. Uden det kan man se, at et ord blev hørt
/// forkert, men ikke AFGØRE HVEM DER FEJLEDE — appen eller oplæseren. Med det
/// kan sætningen spilles igen, og så hører man selv, om ordet blev sagt
/// tydeligt.
/// </summary>
public sealed record Traeningssaetning(
    int Nummer,
    TimeSpan Fra,
    TimeSpan Til,
    string Hørt,
    string Forventet,
    IReadOnlyList<Afvigelse> Afvigelser,
    int Ord,
    int Ramt)
{
    public double Procent => Ord == 0 ? 100 : 100.0 * Ramt / Ord;

    /// <summary>Sætninger uden afvigelser er der ikke noget at gøre ved.</summary>
    public bool Fejler => Afvigelser.Count > 0;
}

/// <summary>
/// Målingen af én træningsoptagelse: hvor mange ord der blev ramt, hvilke der
/// ikke blev, og hvor i lyden de sidder.
///
/// HVORFOR MÅLINGEN LÆSES FRA JSON OG IKKE FRA .TXT-FILEN
///
/// Tekstfilen er gået gennem rettelserne fra ordbogen. Måler man på den, måler
/// man på noget, appen selv har lavet om — og tallet ville stige, uden at
/// genkendelsen var blevet bedre. JSON-filen fra Whisper er den rå udskrift,
/// og den har oven i købet et tidsrum pr. sætning.
/// </summary>
public sealed class Traeningsmaaling
{
    public required string Mappe { get; init; }
    public required string LydFil { get; init; }
    public required IReadOnlyList<Traeningssaetning> Saetninger { get; init; }

    public int Ord { get; init; }
    public int Ramt { get; init; }

    public int Forkerte => Ord - Ramt;
    public double Procent => Ord == 0 ? 0 : 100.0 * Ramt / Ord;

    /// <summary>
    /// Skalaens loft.
    ///
    /// Hundrede er ikke et mål, det er en fantasi: to mennesker, der skriver
    /// den samme optagelse ned, er heller ikke enige om hvert ord. En skala,
    /// der går til hundrede, ville få 92 % til at ligne en halv løsning.
    /// </summary>
    public const double Loft = 96.0;

    /// <summary>Navnet på farven i temaet, der passer til tallet.</summary>
    public static string Farve(double procent) =>
        procent >= 90 ? "Godkendt" : procent >= 80 ? "Advarsel" : "Optager";

    /// <summary>
    /// Måler en optagelsesmappe mod manuskriptet.
    ///
    /// Returnerer null, når der ikke er noget at måle — mappen er ikke skrevet
    /// ud endnu, eller manuskriptet er tomt. Det er ikke en fejl; det er
    /// tilstanden lige efter en optagelse.
    /// </summary>
    public static Traeningsmaaling? Laes(string mappe, string manuskriptMarkdown)
    {
        var json = FindJson(mappe);
        if (json is null) return null;

        // GEMT SVAR. Opstillingen er en tabel på ord gange ord — for den danske
        // tekst 2342 × 2400 celler. Det tager under et tiendedels sekund, men
        // «Start her» spørger ved hvert skift mellem de tre tekster, og en
        // hakkende skærm er en hakkende skærm, uanset hvor lille pausen er.
        //
        // Nøglen er filen, dens tidsstempel og manuskriptets længde. Ændrer
        // udskriften sig — en ny transskription — skifter tidsstemplet, og
        // svaret bliver regnet forfra. Der kan altså ikke vises et forældet tal.
        var noegle = $"{json}|{File.GetLastWriteTimeUtc(json).Ticks}|{manuskriptMarkdown.Length}";

        lock (Gemte)
        {
            if (Gemte.TryGetValue(mappe, out var gemt) && gemt.Noegle == noegle) return gemt.Svar;
        }

        var manus = ReadAloudScore.Ord(ReadAloudScore.ManuskriptTekst(manuskriptMarkdown));
        if (manus.Count == 0) return null;

        List<(TimeSpan Fra, TimeSpan Til, string Tekst)> stykker;
        try { stykker = LaesStykker(json); }
        catch (Exception) { return null; }

        if (stykker.Count == 0) return null;

        // Ordene deles op PR. SÆTNING og lægges derefter i forlængelse af
        // hinanden. Så er der en nøjagtig grænse mellem sætningerne i den
        // samlede ordliste, og hvert ord kan føres tilbage til sit tidsrum.
        var udskrift = new List<string>();
        var graenser = new List<(int Fra, int Til)>();

        foreach (var s in stykker)
        {
            var start = udskrift.Count;
            udskrift.AddRange(ReadAloudScore.Ord(s.Tekst));
            graenser.Add((start, udskrift.Count));
        }

        var opstilling = ReadAloudScore.StilOp(manus, udskrift, maksAfvigelser: 2000);

        // Afvigelserne fordeles ud på de sætninger, de sidder i.
        var pr = new List<Afvigelse>[stykker.Count];
        for (var i = 0; i < pr.Length; i++) pr[i] = new List<Afvigelse>();

        foreach (var a in opstilling.Afvigelser)
        {
            var i = FindStykke(graenser, a.UdskriftPosition);
            if (i >= 0) pr[i].Add(a);
        }

        var saetninger = new List<Traeningssaetning>(stykker.Count);

        for (var i = 0; i < stykker.Count; i++)
        {
            var (fra, til) = graenser[i];
            var antal = til - fra;

            saetninger.Add(new Traeningssaetning(
                Nummer: i + 1,
                Fra: stykker[i].Fra,
                Til: stykker[i].Til,
                Hørt: stykker[i].Tekst.Trim(),
                Forventet: Manuskriptstykke(manus, opstilling.TilManuskript, fra, til),
                Afvigelser: pr[i],
                Ord: antal,
                Ramt: Math.Max(0, antal - pr[i].Sum(a => Math.Max(1, a.Forventet.Split(' ').Length)))));
        }

        var svar = new Traeningsmaaling
        {
            Mappe = mappe,
            LydFil = FindLyd(mappe) ?? "",
            Saetninger = saetninger,
            Ord = opstilling.Ialt,
            Ramt = opstilling.Ramt
        };

        lock (Gemte) Gemte[mappe] = (noegle, svar);

        return svar;
    }

    /// <summary>Ét gemt svar pr. mappe. Se nøglen i <see cref="Laes"/>.</summary>
    private static readonly Dictionary<string, (string Noegle, Traeningsmaaling Svar)> Gemte = new();

    // ------------------------------------------------------------- hjælpere

    /// <summary>
    /// Det stykke af manuskriptet, som sætningen blev stillet op imod — altså
    /// hvad der SKULLE have stået. Det er den tekst, brugeren skal kunne læse
    /// op igen.
    /// </summary>
    private static string Manuskriptstykke(
        IReadOnlyList<string> manus, IReadOnlyList<int> tilManus, int fra, int til)
    {
        if (til <= fra || tilManus.Count == 0) return "";

        var a = tilManus[Math.Min(fra, tilManus.Count - 1)];
        var b = tilManus[Math.Min(til - 1, tilManus.Count - 1)];

        if (b < a) (a, b) = (b, a);

        a = Math.Clamp(a, 0, manus.Count - 1);
        b = Math.Clamp(b, 0, manus.Count - 1);

        return string.Join(' ', manus.Skip(a).Take(b - a + 1));
    }

    private static int FindStykke(List<(int Fra, int Til)> graenser, int ord)
    {
        for (var i = 0; i < graenser.Count; i++)
            if (ord >= graenser[i].Fra && ord < graenser[i].Til) return i;

        // Ord, der mangler i udskriften, kan pege lige forbi enden. De hører
        // til i den sidste sætning frem for at forsvinde ud af listen.
        return graenser.Count - 1;
    }

    private static List<(TimeSpan, TimeSpan, string)> LaesStykker(string json)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(json, System.Text.Encoding.UTF8));

        var ud = new List<(TimeSpan, TimeSpan, string)>();

        if (!doc.RootElement.TryGetProperty("transcription", out var liste)) return ud;

        foreach (var s in liste.EnumerateArray())
        {
            var tekst = s.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            if (tekst.Trim().Length == 0) continue;

            var fra = TimeSpan.Zero;
            var til = TimeSpan.Zero;

            if (s.TryGetProperty("offsets", out var o))
            {
                if (o.TryGetProperty("from", out var f)) fra = TimeSpan.FromMilliseconds(f.GetDouble());
                if (o.TryGetProperty("to", out var u)) til = TimeSpan.FromMilliseconds(u.GetDouble());
            }

            ud.Add((fra, til, tekst));
        }

        return ud;
    }

    /// <summary>Nyeste udskrift fra Whisper. Der kan ligge flere, hvis mappen er skrevet ud med to modeller.</summary>
    public static string? FindJson(string mappe) =>
        !Directory.Exists(mappe) ? null
        : Directory.GetFiles(mappe, "*.json")
            .Where(f => !Path.GetFileName(f).Equals("meeting.json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

    /// <summary>Mikrofonsporet. Det er dét, der blev læst op i — højttalersporet er tomt ved en oplæsning.</summary>
    public static string? FindLyd(string mappe)
    {
        if (!Directory.Exists(mappe)) return null;

        var wav = Directory.GetFiles(mappe, "*.wav");

        return wav.FirstOrDefault(f => Path.GetFileName(f).StartsWith("mikrofon", StringComparison.OrdinalIgnoreCase))
            ?? wav.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
    }
}
