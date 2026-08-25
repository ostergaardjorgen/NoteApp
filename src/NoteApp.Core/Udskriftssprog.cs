using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Hvilket sprog en færdig transskription blev lavet på — læst i den selv.
///
/// HVORFOR IKKE BARE SPØRGE MØDET
///
/// Fordi mødets metadata siger, hvad nogen VALGTE, og filen siger, hvad
/// transskriptionen ER. De to er som regel det samme, og når de ikke er, er
/// det filen, der har ret: den er skrevet af motoren, med det sprog den
/// faktisk kørte med.
///
/// DET KOSTEDE OTTE MINUTTER AT OPDAGE
///
/// Genbrugsreglen sammenlignede det ønskede sprog med
/// <c>meeting.json → ValgtSprogLoop</c>. Var feltet tomt — og det var det på
/// hvert eneste møde, der blev optaget, før opstartsdialogens fejl blev
/// rettet 25-08-2026 — kunne det ikke matche noget, og et helt færdigt spor
/// blev skrevet ud igen. Målt på et rigtigt møde: 8 minutter 9 sekunder for
/// arbejde, der allerede lå på disken.
///
/// Reglen selv er rigtig: et spor, der er skrevet ud på det forkerte sprog,
/// må aldrig genbruges. Den spurgte bare det forkerte sted.
///
/// HVAD DER LÆSES
///
/// whisper.cpp skriver både <c>params.language</c> og <c>result.language</c>.
/// Den første er det, der blev bedt om; den anden er det, motoren endte med.
/// Ved «auto» kan de være forskellige, og så er det RESULTATET, der tæller —
/// det er dét, teksten er skrevet på.
/// </summary>
public static class Udskriftssprog
{
    /// <summary>
    /// Sproget i en whisper-json. Null hvis filen ikke findes eller ikke siger det.
    /// </summary>
    /// <remarks>
    /// Fejler læsningen, er svaret null og ikke et gæt. Et gæt her ville
    /// genbruge en transskription på et sprog, ingen har efterprøvet — og det
    /// er præcis den fejl, reglen findes for at undgå.
    /// </remarks>
    public static string? Hent(string jsonSti)
    {
        try
        {
            if (!File.Exists(jsonSti)) return null;

            using var doku = JsonDocument.Parse(File.ReadAllText(jsonSti));
            var rod = doku.RootElement;

            // Resultatet foerst: ved «auto» er det dét, teksten faktisk blev
            // skrevet paa. params siger kun, hvad der blev bedt om.
            if (Tekst(rod, "result", "language") is { Length: > 0 } fra) return fra;
            if (Tekst(rod, "params", "language") is { Length: > 0 } bedt) return bedt;

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? Tekst(JsonElement rod, string afsnit, string felt) =>
        rod.ValueKind == JsonValueKind.Object
        && rod.TryGetProperty(afsnit, out var o)
        && o.ValueKind == JsonValueKind.Object
        && o.TryGetProperty(felt, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
