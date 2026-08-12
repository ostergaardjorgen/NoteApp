using System.Text.RegularExpressions;

namespace NoteApp.Core.Llm;

/// <summary>
/// Hvad en licens betyder for en app, der skal sælges.
///
/// Skelnen er ikke akademisk. Apache 2.0 og MIT kræver, at du beholder
/// copyright-noten. De øvrige lægger forpligtelser over på DINE kunder — og
/// det er en forretningsbeslutning, ikke en teknisk detalje.
/// </summary>
public enum LicenseClass
{
    /// <summary>Apache 2.0 eller MIT. Fri til salg; behold copyright-noten.</summary>
    FriTilSalg,

    /// <summary>Kommerciel brug tilladt, men med betingelser der skal videregives.</summary>
    BetingelserFoelgerMed,

    /// <summary>Ikke-kommerciel eller uafklaret. Må ikke bruges i et solgt produkt.</summary>
    IkkeTilSalg
}

public sealed record LlmModelInfo(
    string Id,
    string Repo,
    string FileName,
    long Bytes,
    string License,
    LicenseClass LicenseClass,
    string? LicenseObligation,
    string Summary,
    string Pros,
    string Cons,
    /// <summary>
    /// Sat, hvis modellen er prøvet af og fravalgt. Teksten er begrundelsen,
    /// og den skal kunne læses af en, der ikke var med.
    ///
    /// En fravalgt model bliver stående i kataloget frem for at blive slettet.
    /// Slettes den, er der intet, der forhindrer, at den bliver tilføjet igen
    /// om et halvt år af nøjagtig de samme gode grunde — og så koster den de
    /// samme timer at afvise en gang til.
    /// </summary>
    string? Rejected = null)
{
    public string Url => $"https://huggingface.co/{Repo}/resolve/main/{FileName}";

    public string SizeText => Bytes >= 1_000_000_000
        ? $"{Bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB"
        : $"{Bytes / 1024.0 / 1024.0:0} MB";

    /// <summary>
    /// Kan modellen ligge helt på grafikkortet? Under det grænseland bliver
    /// den langsom, ikke ubrugelig — men forskellen er en faktor ti, og den
    /// skal stå ved valget frem for opdages bagefter.
    /// </summary>
    public bool FitsInVram(long vramBytes) => Bytes + (700L * 1024 * 1024) < vramBytes;
}

/// <summary>
/// Kataloget over sprogmodeller, appen kan hente.
///
/// Kun modeller under Apache 2.0 og MIT vises som standard. Det er et bevidst
/// valg: appen skal kunne sælges, og en model, hvis vilkår skal videregives
/// til kunden, må ikke kunne vælges ved et uheld.
///
/// Størrelserne hentes fra Hugging Face, ikke hardkodet. To hardkodede
/// Whisper-størrelser var gættet forkert, og en forkert størrelse får
/// hentningen til at starte forfra hver gang.
/// </summary>
public static class LlmCatalog
{
    /// <summary>
    /// Grundlisten. Licenserne er slået op på Hugging Face 12. august 2026 og
    /// skal kontrolleres igen, hvis en model tilføjes — licensen kan skifte
    /// mellem udgaver hos samme leverandør. Mistral udgiver fx både under
    /// Apache 2.0 og under en ikke-kommerciel forskningslicens.
    /// </summary>
    public static readonly IReadOnlyList<LlmModelInfo> Known = new[]
    {
        new LlmModelInfo(
            "qwen3-8b", "Qwen/Qwen3-8B-GGUF", "Qwen3-8B-Q4_K_M.gguf", 5_027_783_488L,
            "Apache 2.0", LicenseClass.FriTilSalg, null,
            "Lille nok til at ligge helt på et 6 GB-kort. Startvalget.",
            "Kører helt på GPU'en på almindelige laptops og svarer på sekunder. Apache 2.0 — fri at sælge med.",
            "En 8B-model skriver kortere og mere overfladiske referater end de store. Dansk skal måles, ikke antages."),

        new LlmModelInfo(
            "mistral-small-24b", "bartowski/Mistral-Small-24B-Instruct-2501-GGUF",
            "Mistral-Small-24B-Instruct-2501-Q4_K_M.gguf", 14_333_908_672L,
            "Apache 2.0", LicenseClass.FriTilSalg, null,
            "Mellemklasse. Kræver, at en del af modellen ligger i almindelig RAM.",
            "Mærkbart bedre til lange, sammenhængende referater end en 8B. Apache 2.0.",
            "Fylder 14 GB og passer ikke på et 6 GB-kort.",
            Rejected:
                "Prøvet af 12. august 2026 på et 6 GB-kort (RTX 2060, 32 GB RAM) og fravalgt. " +
                "To kørsler på den samme testtekst — 20 minutters møde, 4.481 tokens — blev afbrudt " +
                "efter henholdsvis 50 og 20 minutter uden at have skrevet et referat færdigt. " +
                "Qwen3-8B klarer den samme tekst på 160 sekunder. " +
                "Årsagen er ikke modellens kvalitet, men størrelsen: 14 GB på et 6 GB-kort betyder, " +
                "at llama.cpp lægger det, der kan være, på kortet og resten i RAM, og de lag, der " +
                "ligger i RAM, sætter tempoet. Målt under kørslen: 5,8 GB på kortet og 14,1 GB i RAM. " +
                "Et referat, man venter en time på, bliver ikke lavet — man skriver det selv i mellemtiden."),

        new LlmModelInfo(
            "muse-glimmer-30b", "unsloth/Muse-Glimmer-30B-GGUF",
            "Muse-Glimmer-30B-UD-Q3_K_XL.gguf", 13_360_983_072L,
            "Apache 2.0", LicenseClass.FriTilSalg, null,
            "Stor model med langt kontekstvindue. Til de længste møder.",
            "Bygget til lange kørsler; et 90-minutters møde fylder let 20.000 tokens, og dem kan den holde styr på. Apache 2.0.",
            "Kører på CPU og RAM på et 6 GB-kort. Dansk er ikke evalueret af udgiveren — det skal måles på dine egne referater."),

        // Med vilje IKKE i standardlisten. De kan vaelges til, men kun bevidst.
        new LlmModelInfo(
            "gemma-3-12b", "unsloth/gemma-3-12b-it-qat-GGUF", "gemma-3-12b-it-qat-Q4_0.gguf", 6_909_282_688L,
            "Gemma Terms of Use", LicenseClass.BetingelserFoelgerMed,
            "Google kræver, at du videregiver Gemma-vilkårene og brugspolitikken til enhver, du giver appen eller modellen videre til. Modellen er ikke open source, og Google kan begrænse anvendelsen.",
            "Googles model. Ofte stærk på europæiske sprog.",
            "God til dansk i forhold til størrelsen.",
            "Ikke open source. Vilkårene følger med til dine kunder, og det er en forretningsbeslutning."),

        new LlmModelInfo(
            "llama-31-8b", "bartowski/Meta-Llama-3.1-8B-Instruct-GGUF",
            "Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf", 4_920_739_232L,
            "Llama 3.1 Community License", LicenseClass.BetingelserFoelgerMed,
            "Meta kræver, at der står «Built with Llama», at afledte modeller navngives med Llama som præfiks, og licensen gælder kun under 700 mio. månedlige brugere.",
            "Metas model. Bredt understøttet og velafprøvet.",
            "Meget udbredt, så der findes svar på det meste, når noget driller.",
            "Licensen stiller krav til navngivning og angivelse i dit produkt.")
    };

    /// <summary>
    /// Det brugeren ser som standard: frit at sælge med, og ikke prøvet af og
    /// fravalgt.
    /// </summary>
    public static IEnumerable<LlmModelInfo> Default =>
        Known.Where(m => m.LicenseClass == LicenseClass.FriTilSalg && m.Rejected is null);

    /// <summary>De øvrige. Vises kun, når brugeren aktivt beder om at se dem.</summary>
    public static IEnumerable<LlmModelInfo> WithObligations =>
        Known.Where(m => m.LicenseClass == LicenseClass.BetingelserFoelgerMed && m.Rejected is null);

    /// <summary>
    /// Prøvet af og fravalgt. Vises som en note frem for at blive skjult: den,
    /// der undrer sig over, hvorfor en kendt model ikke er med, skal kunne se
    /// svaret i appen i stedet for at prøve den af igen.
    /// </summary>
    public static IEnumerable<LlmModelInfo> Rejected =>
        Known.Where(m => m.Rejected is not null);

    public static LlmModelInfo? ById(string id) =>
        Known.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Henter den faktiske filstørrelse og revisionen fra Hugging Face.
    /// Revisionen gemmes ved hentning, så «søg efter opdatering» kan svare på
    /// noget, der er målt frem for gættet.
    /// </summary>
    public static async Task<(long Bytes, string? Revision)> FetchFactsAsync(
        LlmModelInfo model, CancellationToken ct = default)
    {
        var json = await new Downloader().FetchTextAsync(
            $"https://huggingface.co/api/models/{model.Repo}", ct);

        var sha = Regex.Match(json, "\"sha\"\\s*:\\s*\"(?<sha>[^\"]+)\"").Groups["sha"].Value;

        // Filstoerrelsen staar ikke i model-oversigten; den hentes med et
        // HEAD-kald paa selve filen.
        long bytes = 0;
        try
        {
            using var klient = new HttpClient();
            klient.DefaultRequestHeaders.UserAgent.ParseAdd("NoteApp");
            using var svar = await klient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, model.Url), ct);
            bytes = svar.Content.Headers.ContentLength ?? 0;
        }
        catch (HttpRequestException) { }

        return (bytes > 0 ? bytes : model.Bytes, string.IsNullOrEmpty(sha) ? null : sha);
    }
}
