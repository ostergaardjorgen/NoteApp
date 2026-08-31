using NAudio.Wave;

namespace NoteApp.Core;

/// <summary>Hvad der kom ud af én indtaling af vågeordet.</summary>
/// <param name="Hoert">Det, motoren skrev — altså din stavemåde.</param>
/// <param name="Sekunder">Klippets længde.</param>
public sealed record Vaageordsforsoeg(string Hoert, double Sekunder);

/// <summary>
/// Lærer appen DIN måde at sige vågeordet på.
/// </summary>
/// <remarks>
/// BRUGERENS EGEN IDÉ, OG DEN PASSER PRÆCIS TIL DEN MOTOR, VI HAR.
///
/// whisper-command i guided mode vælger fra en LISTE AF STAVEMÅDER. Den
/// sammenligner altså det, den hører, med de bogstaver, der står på listen.
/// Står der «hej pia», og modellen hører din stemme som «hej bia», rammer
/// den skævt — hver gang.
///
/// Og det er ikke et opdigtet eksempel. Målt 31-08-2026 landede en af
/// brugerens egne dikteringer i noterne som «Hej Bia, lav nogle lydbølger».
/// Samme dag blev hans «Hej Pia» bedømt til 0,109 og 0,141 — under grænsen,
/// så der skete ingenting. Modellen hørte ham fint; den fik bare udleveret
/// en anden stavemåde end den, den selv ville skrive.
///
/// HERFRA SPØRGER VI I STEDET. Sig ordet et par gange, lad motoren skrive
/// det ned, og læg DEN stavemåde på listen. Så sammenligner den med sig selv.
///
/// Det koster ingenting i drift: indtalingen sker én gang, og listen er
/// lige så billig at slå op i, uanset hvad der står på den.
///
/// DET SKER PÅ MASKINEN. Klippene skrives ud lokalt med whisper — se
/// <see cref="Lokaludskrift"/> — og slettes bagefter. En stemmeprøve er
/// noget af det mest personlige, der findes, og den skal ikke ud af huset
/// for at appen kan høre efter to ord.
/// </remarks>
public static class Vaageordsproeve
{
    /// <summary>Hvor mange gange der skal siges, før det tæller.</summary>
    /// <remarks>
    /// TRE. Én gang siger ikke, om stavemåden er stabil, og fem er for meget
    /// at bede om, før man har set funktionen virke. Med tre kan to være
    /// enige og den tredje være et uheld.
    /// </remarks>
    public const int Gange = 3;

    /// <summary>Længste optagelse pr. forsøg.</summary>
    public static readonly TimeSpan Loft = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Renser det hørte, så det kan stå på listen.
    /// </summary>
    /// <remarks>
    /// Motoren skriver med stort, tegnsætning og indimellem et mærke som
    /// «[BLANK_AUDIO]». Listen skal have rene, små bogstaver — det er sådan,
    /// den selv sammenligner.
    /// </remarks>
    public static string Rens(string? hoert)
    {
        if (string.IsNullOrWhiteSpace(hoert)) return "";

        var kun = new string(hoert
            .Where(c => char.IsLetter(c) || char.IsWhiteSpace(c))
            .ToArray());

        return Vaageord.Rens(kun);
    }

    /// <summary>
    /// Vælger de stavemåder, der skal på listen.
    /// </summary>
    /// <remarks>
    /// DET, DER BLEV HØRT MERE END ÉN GANG, VEJER TUNGEST — men de enlige
    /// kommer med, hvis der er plads. En stavemåde, motoren fandt på én ud af
    /// tre gange, kan sagtens være den, den finder på i morgen; det er den
    /// samme stemme og den samme mikrofon.
    ///
    /// HØJST TRE. Listen skal ikke vokse ukontrolleret: hvert udtryk mere
    /// deler sandsynligheden med de andre, og så bliver hvert enkelt svagere.
    /// Det var netop dét, der gjorde «hej pia» og «hey pia» til et problem.
    ///
    /// Er der intet brugbart, svares der tomt. Så bliver standarden stående,
    /// og det er et bedre svar end en liste af tomme strenge.
    /// </remarks>
    public static IReadOnlyList<string> Vaelg(IEnumerable<Vaageordsforsoeg> forsoeg)
    {
        var rene = forsoeg
            .Select(f => Rens(f.Hoert))
            .Where(o => o.Length > 0)
            .ToList();

        if (rene.Count == 0) return Array.Empty<string>();

        return rene
            .GroupBy(o => o, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Length)
            .Select(g => g.Key)
            .Take(3)
            .ToList();
    }

    /// <summary>
    /// Er det, der blev hørt, brugbart som vågeord?
    /// </summary>
    /// <remarks>
    /// ET VÅGEORD PÅ ÉT BOGSTAV VILLE UDLØSE PÅ ALT. Og en hel sætning ville
    /// aldrig blive ramt. Grænserne er sat, så «hej pia» og «hey pja» går
    /// igennem, mens en hoste og en hel replik ikke gør.
    /// </remarks>
    public static bool Duer(string? hoert)
    {
        var rent = Rens(hoert);

        if (rent.Length < 4 || rent.Length > 30) return false;

        var ord = rent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return ord.Length is >= 1 and <= 3;
    }

    /// <summary>Skriver et klip som 16 kHz mono WAV — det, whisper vil have.</summary>
    public static string Gem(short[] proever, int frekvens, string sti)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sti)!);

        using var skriver = new WaveFileWriter(sti, new WaveFormat(frekvens, 16, 1));

        var bytes = new byte[proever.Length * 2];
        Buffer.BlockCopy(proever, 0, bytes, 0, bytes.Length);
        skriver.Write(bytes, 0, bytes.Length);

        return sti;
    }
}
