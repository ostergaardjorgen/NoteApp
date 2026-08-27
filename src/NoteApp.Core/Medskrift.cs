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
/// Køres der med undervejs, er det kun HALEN, der er tilbage, når mødet
/// stopper — og halen kan gøres så kort, man vil. Det er værdien: ikke at det
/// samlede arbejde bliver mindre, men at det meste af det er gjort, inden
/// nogen venter på det.
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
/// Ved fem minutter er halen højst fem minutters lyd, og en times møde koster
/// tolv modelindlæsninger à knap fire sekunder ≈ tre kvarters minut. Hvor lang
/// halen bliver i tid, afhænger af en hastighed, der svinger for meget til at
/// love noget — se doc/maaling-stilhed.md.
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

    /// <summary>Én færdigskrevet bid: hvor den hører til, og hvad den siger.</summary>
    /// <param name="Json">whisper-json'en for biddens lyd.</param>
    /// <param name="StartSekunder">Hvor i mødet biddens LYD begynder.</param>
    /// <param name="KastVaekSekunder">Hvor meget af den der er tilløb og skal væk.</param>
    public readonly record struct Faerdigbid(string Json, double StartSekunder, double KastVaekSekunder);

    /// <summary>
    /// Fletter bidderne til én transskription, som var den skrevet i ét stykke.
    /// </summary>
    /// <remarks>
    /// RESTEN AF APPEN MÅ IKKE KUNNE SE FORSKEL. Søgningen, talergenkendelsen,
    /// rettelserne og dokumenterne læser alle den samme json. Bliver den anderledes,
    /// fordi teksten blev til i bidder, skal alt det andet laves om — og så er
    /// prisen for løbende transskription pludselig hele appen.
    ///
    /// TIDERNE ER DET, DER KAN GÅ GALT. Hver bid er skrevet ud for sig og tæller
    /// derfor fra nul. Lægges de sammen uden at rette tiderne, står hele mødet
    /// oven i hinanden i de første fem minutter — og det opdages først, når nogen
    /// klikker på en replik og hører noget helt andet.
    ///
    /// TILLØBET SMIDES VÆK HER. Hver bid har ét segments lyd med fra før sig, så
    /// modellen ikke starter midt i en sætning. Den tekst er allerede skrevet af
    /// den forrige bid, og alt, der begynder før grænsen, ryger ud.
    /// </remarks>
    public static string Flet(IReadOnlyList<Faerdigbid> bidder)
    {
        if (bidder.Count == 0) throw new ArgumentException("Ingen bidder at flette", nameof(bidder));

        var samlet = new System.Text.Json.Nodes.JsonArray();
        System.Text.Json.Nodes.JsonNode? skabelon = null;

        foreach (var bid in bidder)
        {
            var rod = System.Text.Json.Nodes.JsonNode.Parse(bid.Json)
                      as System.Text.Json.Nodes.JsonObject;
            if (rod is null) continue;

            // Den foerste bid leverer alt det, der ikke er selve teksten:
            // model, parametre og det sprog, motoren koerte med. De felter er
            // ens for alle bidder - det er den samme model paa den samme lyd.
            skabelon ??= rod.DeepClone();

            if (rod["transcription"] is not System.Text.Json.Nodes.JsonArray liste) continue;

            var kastVaekMs = (long)Math.Round(bid.KastVaekSekunder * 1000);
            var forskydMs = (long)Math.Round(bid.StartSekunder * 1000);

            foreach (var post in liste)
            {
                if (post is not System.Text.Json.Nodes.JsonObject p) continue;
                if (p["offsets"] is not System.Text.Json.Nodes.JsonObject o) continue;

                var fra = o["from"]?.GetValue<long>() ?? 0;
                var til = o["to"]?.GetValue<long>() ?? 0;

                // TILLOEBET UD. Alt, der begynder foer graensen, er skrevet af
                // den forrige bid. «Begynder foer» og ikke «slutter foer»: en
                // replik, der starter i tillobet og fortsaetter ind i bidden,
                // hoerer til den forrige - ellers staar den to gange.
                if (fra < kastVaekMs) continue;

                var nyFra = fra - kastVaekMs + forskydMs;
                var nyTil = til - kastVaekMs + forskydMs;

                var kopi = p.DeepClone() as System.Text.Json.Nodes.JsonObject;
                if (kopi is null) continue;

                kopi["offsets"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["from"] = nyFra,
                    ["to"] = nyTil
                };
                kopi["timestamps"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["from"] = Stempel(nyFra),
                    ["to"] = Stempel(nyTil)
                };

                samlet.Add(kopi);
            }
        }

        if (skabelon is not System.Text.Json.Nodes.JsonObject ud)
            throw new InvalidOperationException("Ingen af bidderne kunne læses");

        ud["transcription"] = samlet;
        return ud.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Millisekunder som «HH:MM:SS,mmm» — samme form som whisper skriver.</summary>
    public static string Stempel(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }

    /// <summary>Den flettede json som ren tekst — samme form som whispers .txt.</summary>
    public static string SomTekst(string flettetJson)
    {
        var rod = System.Text.Json.Nodes.JsonNode.Parse(flettetJson) as System.Text.Json.Nodes.JsonObject;
        if (rod?["transcription"] is not System.Text.Json.Nodes.JsonArray liste) return "";

        var linjer = liste
            .OfType<System.Text.Json.Nodes.JsonObject>()
            .Select(p => p["text"]?.GetValue<string>() ?? "")
            .Where(t => t.Trim().Length > 0);

        return string.Join("\n", linjer);
    }
}
