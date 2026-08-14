using System.IO;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Én sætning, læst op igen.
///
/// HVORFOR DEN GEMMES SEPARAT OG IKKE OVENI
///
/// Karakteren for en oplæsning hviler på, at hele teksten blev læst i ét
/// stykke. Skrev en genindtaling sig ind i det tal, kunne man læse den samme
/// sætning femten gange og se procenten stige — uden at appen var blevet
/// bedre til noget som helst.
///
/// Derfor står genindtalingerne for sig. De svarer på et andet spørgsmål end
/// karakteren, og det er det spørgsmål, de findes for: KAN appen høre det ord
/// rigtigt, når det bliver sagt tydeligt? Bliver det ramt anden gang, var det
/// oplæsningen, der var utydelig. Bliver det ramt forkert igen, er det appen,
/// der ikke kan høre det — og så er en rettelse i ordbogen den rigtige vej.
/// </summary>
public sealed class Genlaesning
{
    public int Saetning { get; set; }
    public DateTimeOffset Tidspunkt { get; set; }

    /// <summary>Det, der stod i manuskriptet — altså facit.</summary>
    public string Manuskript { get; set; } = "";

    /// <summary>Det, Whisper hørte anden gang.</summary>
    public string Hoert { get; set; } = "";

    public int Ord { get; set; }
    public int Ramt { get; set; }

    /// <summary>Filnavnet på klippet, uden sti. Ligger i undermappen «genlaest».</summary>
    public string Lyd { get; set; } = "";

    public double Procent => Ord == 0 ? 0 : 100.0 * Ramt / Ord;

    /// <summary>Blev hvert eneste ord ramt denne gang?</summary>
    public bool Rent => Ord > 0 && Ramt == Ord;
}

/// <summary>
/// Genindtalingerne for én optagelse. Ligger i undermappen «genlaest» ved
/// siden af lyden, så de følger med en flytning og en sikkerhedskopi.
/// </summary>
public static class Genlaesninger
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Mappe(string optagelse) => Path.Combine(optagelse, "genlaest");

    /// <summary>
    /// Alle genindtalinger for optagelsen, slået op på sætningsnummer.
    ///
    /// Er der læst op flere gange for den samme sætning, vinder den NYESTE.
    /// Den anden vej rundt ville betyde, at et dårligt første forsøg blev
    /// stående, uanset hvor mange gange man rettede det.
    /// </summary>
    public static Dictionary<int, Genlaesning> Laes(string optagelse)
    {
        var ud = new Dictionary<int, Genlaesning>();
        var mappe = Mappe(optagelse);

        if (!Directory.Exists(mappe)) return ud;

        foreach (var fil in Directory.GetFiles(mappe, "*.json"))
        {
            try
            {
                var g = JsonSerializer.Deserialize<Genlaesning>(
                    File.ReadAllText(fil, System.Text.Encoding.UTF8));

                if (g is null) continue;

                if (!ud.TryGetValue(g.Saetning, out var haves) || g.Tidspunkt > haves.Tidspunkt)
                    ud[g.Saetning] = g;
            }
            catch (JsonException) { /* en ødelagt fil må ikke vælte de andre */ }
        }

        return ud;
    }

    /// <summary>
    /// Gemmer en genindtaling og flytter klippet ind ved siden af.
    ///
    /// Klippet følger med, fordi hele pointen er at kunne HØRE forskellen på
    /// de to forsøg. Et tal uden lyden bagved kan man ikke gøre noget ved.
    /// </summary>
    public static Genlaesning Gem(string optagelse, int saetning, string manuskript,
                                  string hoert, int ord, int ramt, string klip)
    {
        var mappe = Mappe(optagelse);
        Directory.CreateDirectory(mappe);

        var stempel = DateTimeOffset.Now;
        var navn = $"saetning-{saetning:000}-{stempel:yyyyMMdd-HHmmss}";

        var lyd = "";

        try
        {
            if (File.Exists(klip))
            {
                lyd = navn + ".wav";
                File.Copy(klip, Path.Combine(mappe, lyd), overwrite: true);
            }
        }
        catch (IOException)
        {
            // Kunne lyden ikke flyttes med, er målingen stadig værd at gemme.
            lyd = "";
        }

        var g = new Genlaesning
        {
            Saetning = saetning,
            Tidspunkt = stempel,
            Manuskript = manuskript,
            Hoert = hoert,
            Ord = ord,
            Ramt = ramt,
            Lyd = lyd
        };

        File.WriteAllText(Path.Combine(mappe, navn + ".json"),
            JsonSerializer.Serialize(g, Options), System.Text.Encoding.UTF8);

        return g;
    }
}
