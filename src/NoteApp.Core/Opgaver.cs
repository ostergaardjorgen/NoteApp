using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// En opgave, der kom ud af et møde.
///
/// FELTERNE ER FÅ MED VILJE
///
/// Der er hvad, hvem og hvornår-senest. Ikke estimat, ikke status, ikke
/// prioritet, ikke hvem der har uddelegeret den. En opgaveliste, der kræver
/// syv felter, bliver ikke udfyldt — og en, der ikke bliver udfyldt, findes
/// ikke. Deadline er en dato og ikke et klokkeslæt af samme grund.
/// </summary>
public sealed record Opgave
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Tekst { get; set; } = "";

    /// <summary>
    /// Hvem der skal gøre det. Sat ud fra HVEM DER SAGDE DET — ikke gættet.
    ///
    /// Tom, når opgaven er oprettet i hånden til en, der ikke var med på
    /// mødet. Det sker, og det kan ingen model regne ud.
    /// </summary>
    public string Ejer { get; set; } = "";

    /// <summary>Senest-dato. Null betyder «ingen frist sat».</summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>
    /// Blev fristen sagt tydeligt, eller er den gættet ud af en vending?
    ///
    /// «på fredag» er entydig. «i næste uge» peger på syv dage, og appen har
    /// valgt mandag. Forskellen skal kunne ses på skærmen — en frist, appen
    /// har gættet, må aldrig se ud som en, nogen har sagt.
    /// </summary>
    public bool DeadlineUsikker { get; set; }

    /// <summary>
    /// 1, 2 eller 3 — hvor 1 er vigtigst. 0 betyder «ikke prioriteret».
    ///
    /// TRE TRIN OG IKKE FEM. Med fem bruger man kun tre af dem, og så er de to
    /// øvrige noget, man skal tage stilling til uden at få noget for det.
    ///
    /// Prioriteten sættes af MENNESKET. Den kan ikke udledes af, hvad der blev
    /// sagt i mødet — det, der lyder vigtigst, er tit bare det, der blev talt
    /// længst om.
    /// </summary>
    public int Prioritet { get; set; }

    public bool Faerdig { get; set; }

    public DateTimeOffset Oprettet { get; init; } = DateTimeOffset.Now;

    /// <summary>Tidsstemplet i optagelsen, opgaven kom fra. Tom ved en manuel opgave.</summary>
    public string Kilde { get; init; } = "";
}

/// <summary>
/// Mødets opgaver — og de forslag, der er sagt nej til.
///
/// AFVISNINGERNE GEMMES.
///
/// Uden dem ville de samme otte forslag dukke op hver gang, man åbnede fanen,
/// og så holder man op med at kigge på den. Der gemmes selve teksten og ikke
/// et linjenummer: linjerne flytter sig, hvis optagelsen bliver skrevet ud
/// igen, og så ville afvisningerne pege på noget andet.
/// </summary>
public sealed class Opgaveliste
{
    public List<Opgave> Opgaver { get; init; } = new();
    public List<string> Afvist { get; init; } = new();

    public static string Sti(string mappe) => Path.Combine(mappe, "opgaver.json");

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Opgaveliste Hent(string mappe)
    {
        var sti = Sti(mappe);
        if (!File.Exists(sti)) return new Opgaveliste();

        try
        {
            return JsonSerializer.Deserialize<Opgaveliste>(File.ReadAllText(sti, Encoding.UTF8))
                   ?? new Opgaveliste();
        }
        catch (Exception)
        {
            return new Opgaveliste();
        }
    }

    public void Gem(string mappe)
    {
        Directory.CreateDirectory(mappe);
        File.WriteAllText(Sti(mappe), JsonSerializer.Serialize(this, Format), new UTF8Encoding(false));
    }

    /// <summary>Nøglen, en afvisning huskes på. Teksten renset for mellemrum og tegn.</summary>
    public static string Noegle(string tekst)
    {
        var sb = new StringBuilder();
        foreach (var c in tekst.ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);

        var s = sb.ToString();
        return s.Length <= 120 ? s : s[..120];
    }

    public bool ErAfvist(string tekst) => Afvist.Contains(Noegle(tekst));

    public void Afvis(string tekst)
    {
        var n = Noegle(tekst);
        if (!Afvist.Contains(n)) Afvist.Add(n);
    }

    /// <summary>Er der allerede oprettet en opgave af den her tekst?</summary>
    public bool ErOprettet(string tekst) =>
        Opgaver.Any(o => Noegle(o.Tekst) == Noegle(tekst));
}
