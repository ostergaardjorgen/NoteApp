using System.Text.Json.Serialization;

namespace NoteApp.Core;

/// <summary>Hvad en kommando gør.</summary>
public enum Kommandotype
{
    /// <summary>Starter en mødeoptagelse — som et tryk på genvejstasten.</summary>
    Optag,

    /// <summary>Starter en webinaroptagelse.</summary>
    Webinar,

    /// <summary>Går til en skærm i appen.</summary>
    AabnSkaerm,

    /// <summary>Åbner et program eller en adresse.</summary>
    AabnProgram,
}

/// <summary>
/// Én kommando, brugeren selv har lavet.
/// </summary>
/// <param name="Udtryk">Det, der skal siges. Ét eller flere ord.</param>
/// <param name="Type">Hvad der skal ske.</param>
/// <param name="Maal">
/// Programmet, adressen eller skærmen. Tom for kommandoer, der ikke har et mål.
/// </param>
public sealed record Kommando(
    string Udtryk,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] Kommandotype Type,
    string Maal = "");

/// <summary>
/// Finder den kommando, der blev sagt.
///
/// LISTEN ER EN HVIDLISTE, OG DEN ER BRUGERENS EGEN. Der kan ikke køre noget,
/// som ikke står i den. Det er ikke en sikkerhedsforanstaltning mod ondskab —
/// det er maskinen, og det er brugerens egne kommandoer — men mod FEJLHØRING:
/// en sætning i et møde må aldrig kunne blive til «åbn regnskabet».
///
/// DERFOR ER TOLKEN STRENGERE END ORDBOGENS RETTER
///
/// <see cref="Ordretter"/> retter et ord, der blev hørt næsten rigtigt, og en
/// fejl dér koster et forkert ord i en tekst, man læser igennem. En fejl her
/// starter et program. Så:
///
///   * hele udtrykket skal genkendes, ikke et enkelt ord i det
///   * kun ÉN kommando må passe. Passer to, gøres der ingenting
///   * afstanden må højst være ét tegn pr. femte tegn i udtrykket
/// </summary>
public static class Kommandotolk
{
    /// <summary>
    /// Hvor meget der må være galt, før det ikke længere er den kommando.
    /// </summary>
    /// <remarks>
    /// Ét tegn pr. femte. «åbn kalenderen» er fjorten tegn og tåler to; «optag»
    /// er fem og tåler ét. Jo kortere udtryk, jo mindre må der være galt — et
    /// kort udtryk ligner alt for meget.
    /// </remarks>
    public static int Taerskel(int laengde) => Math.Max(1, laengde / 5);

    /// <summary>Renser et udtryk, så to skrivemåder af det samme er ét.</summary>
    public static string Rens(string? udtryk)
    {
        var s = (udtryk ?? "").Trim().ToLowerInvariant();

        var ud = new System.Text.StringBuilder(s.Length);

        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '-') ud.Append(c);
            else if (char.IsPunctuation(c)) ud.Append(' ');
        }

        var r = ud.ToString();
        while (r.Contains("  ", StringComparison.Ordinal)) r = r.Replace("  ", " ");

        return r.Trim();
    }

    /// <summary>
    /// Den kommando, der utvetydigt blev sagt — eller <c>null</c>.
    /// </summary>
    /// <param name="sagt">Det, der blev hørt. Vågeordet må gerne stå i.</param>
    /// <param name="kommandoer">Brugerens egen liste.</param>
    /// <param name="vaageord">Vågeordet, der skal skæres væk først.</param>
    public static Kommando? Find(string? sagt, IEnumerable<Kommando> kommandoer, string? vaageord = null)
    {
        var s = Rens(sagt);
        if (s.Length == 0) return null;

        // Vaageordet staar foran og hoerer ikke med til kommandoen.
        var v = Rens(vaageord);
        if (v.Length > 0 && s.StartsWith(v, StringComparison.Ordinal))
            s = s[v.Length..].Trim();

        if (s.Length == 0) return null;

        Kommando? fundet = null;
        var bedste = int.MaxValue;
        var flere = false;

        foreach (var k in kommandoer)
        {
            var u = Rens(k.Udtryk);
            if (u.Length == 0) continue;

            var loft = Taerskel(u.Length);

            // Er der mere end en haandfuld tegn til forskel i LAENGDE, er det
            // ikke den. Kontrollen sparer ogsaa den dyre udregning.
            if (Math.Abs(u.Length - s.Length) > loft) continue;

            var d = Ordretter.Afstand(s, u, loft);
            if (d > loft) continue;

            if (d < bedste) { bedste = d; fundet = k; flere = false; }
            else if (d == bedste && !ReferenceEquals(k, fundet)) flere = true;
        }

        // TO KOMMANDOER, DER PASSER LIGE GODT, ER INGEN KOMMANDO. Vaelges den
        // foerste, startes det forkerte program halvdelen af gangene.
        return flere ? null : fundet;
    }

    /// <summary>
    /// De kommandoer, appen kommer med. De kan rettes og slettes som alle
    /// andre — de er et udgangspunkt, ikke en fast del af appen.
    /// </summary>
    public static IReadOnlyList<Kommando> Standard => new[]
    {
        new Kommando("optag møde", Kommandotype.Optag),
        new Kommando("optag webinar", Kommandotype.Webinar),
        new Kommando("åbn kalenderen", Kommandotype.AabnSkaerm, "cockpit"),
        new Kommando("vis optagelser", Kommandotype.AabnSkaerm, "optagelser"),
        new Kommando("åbn ordbogen", Kommandotype.AabnSkaerm, "diktering"),
    };
}
