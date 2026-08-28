namespace NoteApp.Core.Llm;

/// <summary>Hvor den korte opsummering laves.</summary>
public enum Opsummeringssted
{
    /// <summary>Hos leverandøren. Den eneste vej.</summary>
    Sky,

    /// <summary>Der er ingen nøgle sat op, og så kan den ikke laves.</summary>
    Ingen
}

/// <summary>
/// Opsummeringen laves hos leverandøren. Der er ikke længere en lokal vej.
///
/// GRÆNSEN GÅR VED TRANSKRIPTIONEN
///
/// Det er den linje, appen står på, og den skal siges lige ud: lyden optages
/// og skrives ud her på maskinen, og den forlader aldrig maskinen. Alt
/// DEREFTER — opsummering, opgaver, referater — sker hos leverandøren.
///
/// Det er en indsnævring i forhold til før, og den er bevidst. Den skal stå
/// samme sted i alle tekster, så ingen først opdager den, når de har taget
/// appen i brug.
///
/// HVORFOR DEN LOKALE VEJ ER VÆK
///
/// Den kostede **4 GB** at have med: llama-motoren på 1,68 GB og en
/// sprogmodel på 2,33 GB, hentet ned ved opsætningen. Det er halvdelen af
/// alt, en ny bruger skulle hente — og det var den halvdel, der gav mindst.
///
/// Den lokale DOKUMENT-vej var i forvejen fjernet 18-08-2026 af en målt
/// grund: på det samme møde tabte den 72 % af navnene og brugte 59 minutter,
/// hvor skyen bruger 25 sekunder og taber 23 %. Se doc/maaling-sky.md.
/// Tilbage stod en lokal opsummering, der var kort, fordi vægtene og
/// udskriften ikke kunne være på grafikkortet samtidig — mens den i skyen kan
/// bære de opgaver, den finder.
///
/// Fjernet 25-08-2026. Motoren og modellen slettes fra maskinen.
///
/// HVAD DET KOSTER
///
/// To ting, og de skal stå i teksterne, ikke kun her:
///
///   1. Uden en nøgle kan appen kun transskribere. Før fik man en
///      opsummering alligevel; det gør man ikke længere.
///   2. Der er ingen opsummering uden net. I et tog eller et mødelokale uden
///      forbindelse får man teksten og intet andet.
///
/// HVAD DER SENDES, ER UÆNDRET
///
/// Transkriptionens tekst — præcis som når der laves et dokument. Lyden
/// bliver, hvor den er. Afsendelsen bogføres af <see cref="SkyRunner"/> som
/// alle andre, med tidspunkt, model, tegn, pris og kontrolsum.
///
/// LlmRunner og Referatbygger ligger stadig i Core, men bruges kun fra
/// kommandolinjen (heypia referat). Det er dér, sammenligningen mellem
/// lokalt og skyen skal kunne køres igen — en måling, man ikke kan gentage,
/// er en påstand.
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

    /// <summary>Vejen, der bruges nu. Der er kun én — eller ingen.</summary>
    public static Opsummeringssted Valgt =>
        SkyKlar ? Opsummeringssted.Sky : Opsummeringssted.Ingen;
}
