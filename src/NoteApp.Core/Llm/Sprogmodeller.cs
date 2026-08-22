namespace NoteApp.Core.Llm;

/// <summary>
/// En sprogmodel, appen kan hente og køre på maskinen.
/// </summary>
public sealed record Sprogmodel(
    string Id,
    string Navn,
    string Filnavn,
    string Url,
    long Bytes,
    /// <summary>Hvad den bruges til. Står på skærmen — en model uden en opgave er en fil.</summary>
    string Opgave,
    string Licens,
    /// <summary>Hvorfor netop den. Målingen, ikke en anbefaling.</summary>
    string Hvorfor)
{
    public double GigaBytes => Bytes / 1024.0 / 1024.0 / 1024.0;

    public string SizeText => Bytes >= 1_000_000_000
        ? $"{GigaBytes:0.0} GB"
        : $"{Bytes / 1024.0 / 1024.0:0} MB";
}

/// <summary>
/// De sprogmodeller, der kan køre på maskinen.
///
/// HVORFOR DE HENTES OG IKKE FØLGER MED
///
/// Talergenkendelsens filer fylder 61 MB og ligger i installationspakken.
/// En sprogmodel fylder 2,3 GB. Pakket med ville installationsfilen gå fra
/// 104 MB til 2,5 GB, og så bliver den ikke sendt til nogen.
///
/// Til gengæld hentes de ÉN gang og ændrer sig aldrig. Der er ingen
/// opdateringer at holde styr på — modellen er en fast fil, præcis som
/// whisper-modellen.
///
/// STØRRELSEN SKAL VÆRE NØJAGTIG
///
/// Hentningen genoptager en afbrudt fil ved at sammenligne med det forventede
/// antal byte. Er tallet gættet, passer det aldrig, og så begynder den forfra
/// hver gang. Tallene her er talt på filen, ikke slået op.
/// </summary>
public static class Sprogmodeller
{
    public static IReadOnlyList<Sprogmodel> Katalog { get; } = new[]
    {
        // MÅLT 20-08-2026 PÅ ET MØDE PÅ EN TIME.
        //
        // Qwen3-4B: 34 sekunder, færdig tekst, nul opfundne påstande, og den
        // fangede mødets aftale. gemma-3-4b brugte 131 sekunder på det samme.
        // Qwen3-8B blev afbrudt efter elleve minutter: vægtene fylder 4.795 MB
        // af kortets 6.144, og så er der ikke plads til udskriftens 23.345
        // tokens.
        //
        // Størrelse er hverken kvalitet eller hastighed. Den her er valgt,
        // fordi den blev målt bedst — ikke fordi den er lille.
        new Sprogmodel(
            "qwen3-4b",
            "Qwen3 4B",
            "Qwen3-4B-Q4_K_M.gguf",
            "https://huggingface.co/unsloth/Qwen3-4B-GGUF/resolve/main/Qwen3-4B-Q4_K_M.gguf",
            2_497_281_312,
            "Korte opsummeringer af møder — her på maskinen.",
            "Apache-2.0",
            "Målt hurtigst og mest præcis af de tre, der blev prøvet. En 8B kan ikke være på kortet sammen med et helt mødes transkription.")
    };

    public static Sprogmodel Standard => Katalog[0];

    public static Sprogmodel? Model(string id) =>
        Katalog.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static string Sti(Sprogmodel m) =>
        Path.Combine(LlmRunner.ModelDirectory, m.Filnavn);

    public static bool ErHentet(Sprogmodel m) => File.Exists(Sti(m));

    /// <summary>
    /// Den model, appen VILLE bruge til en opsummering — eller null, hvis der
    /// ikke ligger nogen.
    ///
    /// Kataloget går forud for det, der tilfældigvis ligger i mappen: har man
    /// selv lagt en anden gguf-fil ind, virker den, men den, der er målt, får
    /// lov at vinde.
    /// </summary>
    public static string? Valgt()
    {
        foreach (var m in Katalog)
            if (ErHentet(m)) return Sti(m);

        var andre = LlmRunner.InstalledModels();
        return andre.Count == 0
            ? null
            : andre.OrderBy(f => new FileInfo(f).Length).First();
    }
}
