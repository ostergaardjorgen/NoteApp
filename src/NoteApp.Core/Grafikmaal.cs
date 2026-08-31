namespace NoteApp.Core;

/// <summary>
/// Hvad vågeordet lægger på grafikkortet.
/// </summary>
/// <remarks>
/// TALLENE KOMMER FRA MOTOREN SELV. whisper.cpp skriver ved opstart både
/// kortets samlede hukommelse og præcis hvor meget modellen fylder på det:
///
///   ggml_cuda_init: found 1 CUDA devices (Total VRAM: 6143 MiB):
///   whisper_model_load:        CUDA0 total size =   487.01 MB
///
/// Det er de to linjer, der læses her. Et skøn ville være nemmere og
/// dårligere: hukommelsen på et grafikkort deles med alt andet, der bruger
/// det, og et gæt på, hvad der er tilbage, ville hverken kunne efterprøves
/// eller passe på to maskiner.
///
/// HVORFOR PROCENTEN ER MED. «487 MB» siger ikke noget om, hvorvidt det er
/// meget. På et kort med 6 GB er det otte procent, og så kan man tage
/// stilling. Det samme spørgsmål gjaldt CPU'en: et tal alene læses som en
/// påstand, et tal sat i forhold til maskinen er en oplysning.
/// </remarks>
public sealed record Grafikmaal(double ModelMB, double KortMB)
{
    /// <summary>Modellens andel af kortets hukommelse, i procent.</summary>
    public double Procent => KortMB <= 0 ? 0 : ModelMB / KortMB * 100.0;

    /// <summary>
    /// Læser kortets samlede hukommelse ud af «Total VRAM: 6143 MiB».
    /// </summary>
    /// <remarks>
    /// MiB og MB blandes sammen i whispers egen udskrift: kortet opgives i
    /// MiB, modellen i MB. Forskellen er under fem procent og betyder intet
    /// for et tal, der vises med ét ciffer — men den skal være nævnt, så
    /// ingen regner videre på det som var det den samme enhed.
    /// </remarks>
    public static double? Kort(string? linje)
    {
        if (linje is null) return null;

        const string maerke = "Total VRAM:";
        var i = linje.IndexOf(maerke, StringComparison.Ordinal);
        if (i < 0) return null;

        var rest = linje[(i + maerke.Length)..].Trim();
        var slut = rest.IndexOf(' ');
        if (slut > 0) rest = rest[..slut];

        return double.TryParse(rest, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var mb) ? mb : null;
    }

    /// <summary>
    /// Læser modellens størrelse ud af «CUDA0 total size = 487.01 MB».
    /// </summary>
    /// <remarks>
    /// Der står ikke altid CUDA0. Køres der på CPU, hedder linjen noget
    /// andet, og så er der ingenting på grafikkortet at fortælle om — det er
    /// et rigtigt svar og ikke en fejl.
    /// </remarks>
    public static double? Model(string? linje)
    {
        if (linje is null) return null;
        if (!linje.Contains("CUDA", StringComparison.Ordinal)) return null;

        const string maerke = "total size =";
        var i = linje.IndexOf(maerke, StringComparison.Ordinal);
        if (i < 0) return null;

        var rest = linje[(i + maerke.Length)..].Trim();
        var slut = rest.IndexOf(' ');
        if (slut > 0) rest = rest[..slut];

        return double.TryParse(rest, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var mb) ? mb : null;
    }
}
