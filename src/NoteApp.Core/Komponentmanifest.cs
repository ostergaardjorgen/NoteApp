using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoteApp.Core;

/// <summary>Én hentet komponent og den sum, den skal have.</summary>
public sealed record Komponentsum
{
    public string Filnavn { get; init; } = "";
    public string Rolle { get; init; } = "";
    public long Bytes { get; init; }

    /// <summary>SHA-256 med små bogstaver, som <see cref="Downloader.Sha256Async"/> giver den.</summary>
    public string Sha256 { get; init; } = "";

    /// <summary>Hvor filen kommer fra.</summary>
    public string Kilde { get; init; } = "";

    /// <summary>Hvor SUMMEN kommer fra. Ikke det samme, og forskellen er hele pointen.</summary>
    public string Sumkilde { get; init; } = "";

    /// <summary>Hvad der er gjort for at bekræfte summen ud over at læse den.</summary>
    public string Bekraeftelse { get; init; } = "";

    public string Efterproevet { get; init; } = "";
}

/// <summary>
/// De forventede kontrolsummer for alt, appen henter ned.
/// </summary>
/// <remarks>
/// FILSTØRRELSE ALENE ER IKKE INTEGRITETSKONTROL.
///
/// Det er ikke en teoretisk indvending. Stemmevagtens to udgaver,
/// <c>ggml-silero-v5.1.2.bin</c> og <c>ggml-silero-v6.2.0.bin</c>, er BEGGE
/// på nøjagtig 885.098 byte og er to forskellige modeller (slået op hos
/// Hugging Face 03-09-2026). Kontrollen på størrelse, appen havde indtil da,
/// kunne ikke se forskel på dem.
///
/// HVORFOR SUMMEN LIGGER I REPOET OG IKKE HENTES
///
/// En sum, der hentes samme sted som filen, beskytter mod en afbrudt
/// hentning og mod ingenting andet: den, der kan ændre filen, kan ændre
/// summen. Kun en sum, der ligger her, kan sige, at filen er den samme som
/// den, der blev målt på.
///
/// SUMMERNE ER LEVERANDØRENS EGNE. De er slået op hos Hugging Face og GitHub,
/// ikke regnet ud af en fil på en maskine her — en sum, der er regnet ud af
/// det, appen lige har hentet, verificerer kun sig selv. De fem modelfiler er
/// desuden efterprøvet mod filerne i datamappen 03-09-2026; alle fem stemte.
/// Motorens zip-filer lå ikke på maskinen, og det står på hver linje.
///
/// HVAD DER IKKE ER DÆKKET
///
/// En komponent, manifestet ikke kender, bliver ikke verificeret. Den slags
/// skal siges højt frem for at gå stille igennem — se <see cref="Ukendt"/>
/// og fanen «Modeller og licenser» på Compliance-skærmen.
/// </remarks>
public static class Komponentmanifest
{
    private sealed record Fil(
        [property: JsonPropertyName("komponenter")] List<Komponentsum> Komponenter);

    private static readonly Lazy<IReadOnlyList<Komponentsum>> _alle = new(Laes);

    /// <summary>Alt, manifestet kender, i den rækkefølge det står i filen.</summary>
    public static IReadOnlyList<Komponentsum> Alle => _alle.Value;

    private static IReadOnlyList<Komponentsum> Laes()
    {
        try
        {
            var navn = typeof(Komponentmanifest).Assembly
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("komponenter.json", StringComparison.OrdinalIgnoreCase));

            if (navn is null) return Array.Empty<Komponentsum>();

            using var stroem = typeof(Komponentmanifest).Assembly.GetManifestResourceStream(navn)!;
            using var laeser = new StreamReader(stroem, Encoding.UTF8);

            var fil = JsonSerializer.Deserialize<Fil>(laeser.ReadToEnd(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return fil?.Komponenter ?? (IReadOnlyList<Komponentsum>)Array.Empty<Komponentsum>();
        }
        catch (Exception)
        {
            // Et manifest, der ikke kan laeses, maa ikke vaelte appen - men det
            // maa heller ikke ligne et manifest uden fund. Tom liste betyder
            // «ingen kendt sum», og saa staar hver komponent som uverificeret
            // i UI'et frem for som godkendt. Det er den rigtige vej at tage fejl.
            return Array.Empty<Komponentsum>();
        }
    }

    /// <summary>Den forventede sum for et filnavn — eller <c>null</c>, hvis der ikke er en.</summary>
    public static Komponentsum? For(string filnavnEllerSti)
    {
        var navn = Path.GetFileName(filnavnEllerSti);

        return Alle.FirstOrDefault(k =>
            k.Filnavn.Equals(navn, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Har komponenten en kendt sum? Er svaret nej, er hentningen en
    /// forsyningskæderisiko, der skal vises som det.
    /// </summary>
    public static bool Kendt(string filnavnEllerSti) => For(filnavnEllerSti) is not null;

    /// <summary>
    /// Teksten, der skal stå ved en komponent uden kendt sum.
    /// </summary>
    /// <remarks>
    /// DEN SKAL VÆRE DER, OG DEN MÅ IKKE VÆRE VAG. En hentning uden sum er
    /// ikke «verificeret på størrelsen» — den er uverificeret, og det er
    /// forskellen på en oplysning og en beroligelse.
    /// </remarks>
    public const string Ukendt =
        "Ingen kendt kontrolsum. Filen kontrolleres kun på størrelse, og det er "
        + "ikke integritetskontrol — to forskellige filer kan have samme størrelse. "
        + "Det er en forsyningskæderisiko, ikke en verifikation.";
}
