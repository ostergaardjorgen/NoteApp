namespace NoteApp.Core;

/// <summary>
/// Planlægger transskription, MENS mødet kører.
///
/// HVORFOR OVERHOVEDET
///
/// Lyden ligger allerede i 30-sekunders segmenter — det gør den, fordi et
/// crash ikke må koste mere end et halvt minut. De segmenter er færdige filer
/// længe før mødet er slut, og der er ingen grund til, at de skal vente.
///
/// Efter stilhedsmodellen tager en times møde omkring fire minutter at skrive
/// ud. Køres der med undervejs, er det kun HALEN, der er tilbage, når mødet
/// stopper — og halen kan gøres så kort, man vil.
///
/// HVOR STOR EN BID
///
/// Ti segmenter, altså fem minutter. Det er et regnestykke med to sider:
///
///   Mindre bid  →  kortere hale, men flere opstarter. Motoren bruger knap
///                  fire sekunder på at læse modellen ind hver gang, og det
///                  betales forfra ved hver eneste bid.
///   Større bid  →  færre opstarter, men længere hale.
///
/// Ved fem minutter er halen højst fem minutters lyd ≈ tyve sekunders arbejde,
/// og en times møde koster tolv modelindlæsninger ≈ tre kvarters minut. Begge
/// dele er små, og det er dét, der gør fem til svaret.
///
/// SKÆREKANTERNE ER PROBLEMET
///
/// Et segment slutter efter tredive sekunder, uanset hvad der bliver sagt.
/// Skæres der midt i et ord, hører modellen en halv stavelse i begyndelsen af
/// den ene bid og en halv i slutningen af den anden — og skriver noget forkert
/// begge steder.
///
/// DERFOR OVERLAPPES DER. Hver bid får ét segment MED FRA FØR sig som lyd,
/// men teksten fra det stykke smides væk — den er allerede skrevet. Modellen
/// får altså tredive sekunders tilløb til at forstå, hvor sætningen var på vej
/// hen, og de ord, der beholdes, står aldrig først i det, den hørte.
/// </summary>
public static class Medskrift
{
    /// <summary>Segmenter pr. bid. Ti à tredive sekunder = fem minutter.</summary>
    public const int BidSegmenter = 10;

    /// <summary>
    /// Hvor meget lyd der tages med fra før — kun som tilløb, ikke som tekst.
    ///
    /// Ét segment. To ville give bedre kontekst og koste dobbelt så meget
    /// spildt arbejde; et halvt minut er rigeligt til at komme igennem den
    /// sætning, skærekanten ligger i.
    /// </summary>
    public const int Tilloeb = 1;

    /// <summary>
    /// En portion, der kan skrives ud nu.
    /// </summary>
    /// <param name="LydFra">Første segment, der læses som LYD (tilløbet talt med).</param>
    /// <param name="TekstFra">Første segment, hvis TEKST beholdes.</param>
    /// <param name="Til">Første segment, der IKKE er med (halvåbent interval).</param>
    public readonly record struct Bid(int LydFra, int TekstFra, int Til)
    {
        /// <summary>Sekunder inde i den udskrevne lyd, hvor teksten skal beholdes fra.</summary>
        public double KastVaekSekunder =>
            (TekstFra - LydFra) * AudioFormat.ChunkDuration.TotalSeconds;

        /// <summary>Hvor i mødet den beholdte tekst begynder. Til at rette tiderne med.</summary>
        public double StartISekunder => TekstFra * AudioFormat.ChunkDuration.TotalSeconds;

        public int Antal => Til - TekstFra;
    }

    /// <summary>
    /// Hvad kan skrives ud nu — hvis noget?
    /// </summary>
    /// <param name="skrevneSegmenter">Hvor mange segmentfiler der ligger i alt.</param>
    /// <param name="faerdige">Hvor mange segmenter der allerede er skrevet ud.</param>
    /// <param name="optagerStadig">
    /// Sandt, mens mødet kører. Så er det SIDSTE segment stadig åbent og må
    /// ikke røres: det er halvskrevet, og en halv WAV giver enten en fejl eller
    /// et afhugget ord. Er optagelsen stoppet, er alle segmenter færdige.
    /// </param>
    public static Bid? Naeste(int skrevneSegmenter, int faerdige, bool optagerStadig)
    {
        // Mens der optages, er det sidste segment aabent. Naar der er stoppet,
        // er de alle sammen lukkede.
        var brugbare = optagerStadig ? skrevneSegmenter - 1 : skrevneSegmenter;

        if (brugbare <= faerdige) return null;

        var venter = brugbare - faerdige;

        // UNDER EN HEL BID VENTER VI - MEN KUN MENS DER OPTAGES.
        //
        // Ellers ville motoren blive startet for hvert halve minut, og de
        // fire sekunders modelindlaesning ville fylde mere end selve
        // arbejdet. Er moedet stoppet, skal resten med, uanset hvor lidt der
        // er tilbage.
        if (optagerStadig && venter < BidSegmenter) return null;

        var til = optagerStadig
            ? faerdige + BidSegmenter
            : brugbare;

        var lydFra = Math.Max(0, faerdige - Tilloeb);

        return new Bid(lydFra, faerdige, til);
    }

    /// <summary>
    /// Hvor lang tid er der tilbage at skrive ud, når mødet stopper netop nu?
    ///
    /// Til den besked, man får at se: «transskriptionen er i gang, typisk
    /// x minutter». Et tal, der er gættet, er værre end intet tal — det her er
    /// regnet ud af, hvad der faktisk mangler.
    /// </summary>
    /// <param name="hastighed">
    /// Hvor mange sekunder lyd der skrives ud pr. sekund. Målt til omkring 17
    /// med stilhedsmodel og large-v3 på et RTX 2060 (32,7 minutter på 1:57).
    /// </param>
    public static TimeSpan Restarbejde(int skrevneSegmenter, int faerdige, double hastighed)
    {
        if (hastighed <= 0) return TimeSpan.Zero;

        var mangler = Math.Max(0, skrevneSegmenter - faerdige);
        var sekunderLyd = mangler * AudioFormat.ChunkDuration.TotalSeconds;

        return TimeSpan.FromSeconds(sekunderLyd / hastighed);
    }
}
