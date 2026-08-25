namespace NoteApp.Core.Llm;

/// <summary>Hvor den korte opsummering laves.</summary>
public enum Opsummeringssted
{
    /// <summary>Hos leverandøren i Europa. Standard.</summary>
    Sky,

    /// <summary>På maskinen, med en sprogmodel der er hentet ned.</summary>
    Lokal,

    /// <summary>Hverken det ene eller det andet er sat op.</summary>
    Ingen
}

/// <summary>
/// Hvor den korte opsummering laves — og hvorfor det som standard er i skyen.
///
/// HVAD DET HANDLER OM
///
/// Opsummeringen er ti linjer om, hvad mødet handlede om. Den kostede indtil
/// 25-08-2026 **4 GB** at have med: llama-motoren på 1,68 GB og en sprogmodel
/// på 2,33 GB, hentet ned ved opsætningen.
///
/// Det er halvdelen af alt, en ny bruger skal hente, for at kunne bruge
/// appen — og det er den halvdel, der giver mindst. Dokumenterne, som er det
/// egentlige arbejde, laves i forvejen i skyen.
///
/// HVORFOR DET IKKE ÆNDRER NOGET PÅ COMPLIANCE-SIDEN
///
/// Det, der sendes, er transkriptionens tekst — præcis som når der laves et
/// dokument. Lyden bliver, hvor den er. Afsendelsen bogføres af
/// <see cref="SkyRunner"/> som alle andre, med tidspunkt, model, tegn, pris
/// og kontrolsum.
///
/// Grænsen flytter sig ikke. Det er den samme slags tekst, der går samme vej,
/// til den samme leverandør.
///
/// DEN LOKALE FORSVINDER IKKE
///
/// Den, der vil have opsummeringen på maskinen, kan hente modellen og slå det
/// til. Det er et **tilvalg** i stedet for en forudsætning — og for den, der
/// arbejder med noget, der ikke må sendes, er det stadig svaret.
/// </summary>
public static class Opsummeringsvej
{
    /// <summary>Er der en nøgle til leverandøren?</summary>
    public static bool SkyKlar
    {
        get
        {
            try { return !string.IsNullOrWhiteSpace(SkyNoegle.Hent()); }
            catch (Exception) { return false; }
        }
    }

    /// <summary>Er både motoren og en sprogmodel på plads?</summary>
    public static bool LokalKlar
    {
        get
        {
            try { return LlmRunner.FindCli() is not null && Sprogmodeller.Valgt() is not null; }
            catch (Exception) { return false; }
        }
    }

    /// <summary>
    /// Vejen, der bruges nu.
    ///
    /// ØNSKET GÅR FORUD, MEN KUN NÅR DET KAN LADE SIG GØRE. Har man valgt
    /// den lokale og ikke hentet modellen, er svaret ikke «lokal» — det er
    /// «sky», hvis der er en nøgle. Ellers «ingen», og så skal skærmen sige,
    /// hvad der mangler.
    /// </summary>
    public static Opsummeringssted Valgt
    {
        get
        {
            var vilLokalt = AppSettings.Current.OpsummerLokalt;

            if (vilLokalt && LokalKlar) return Opsummeringssted.Lokal;
            if (SkyKlar) return Opsummeringssted.Sky;
            if (LokalKlar) return Opsummeringssted.Lokal;

            return Opsummeringssted.Ingen;
        }
    }

    /// <summary>
    /// Er den valgte vej en anden end den ønskede? Så skal skærmen sige det —
    /// ellers ser det ud, som om indstillingen ikke virker.
    /// </summary>
    public static bool ValgtErIkkeDetOenskede =>
        AppSettings.Current.OpsummerLokalt && !LokalKlar && SkyKlar;
}
