using System.Text;

namespace NoteApp.Core.Llm;

/// <summary>
/// Laver ét dokument ud af et møde — også når mødet er for langt til at ligge
/// i modellen på én gang.
///
/// PROBLEMET
///
/// Et 61-minutters møde fylder 12.500 tokens. Sammen med en 8B-model på 4,7 GB
/// og beregningsbufferne er der ikke plads på et 6 GB-kort, og llama.cpp
/// skubber lag over på processoren. Målt 14. august 2026: 0,7 tokens i
/// sekundet, 28 minutter for ét referat. Det er ikke en indstilling, der kan
/// skrues på — prompten skal igennem, uanset hvor kort svaret bliver.
///
/// LØSNINGEN
///
/// Mødet læses i stykker. Hvert stykke fylder lidt, så konteksten er lille og
/// alt bliver på grafikkortet; til sidst samles stykkerne til det færdige
/// dokument efter brugerens skabelon.
///
/// Det løser to ting på én gang. Ti minutters lyd, der refereres for sig, får
/// mere plads end en time presset ned i ét svar — så referatet bliver både
/// hurtigere OG mere udførligt. Og det skalerer: et møde på to timer er bare
/// flere stykker.
///
/// BRUGEREN SKAL IKKE SE DET
///
/// Fremdriften er ét tal fra 0 til 100 for hele arbejdet, og teksten siger,
/// hvad der sker — «læser mødet igennem», «skriver referatet» — ikke hvilket
/// stykke der er nået til. Opdelingen er en teknisk nødvendighed, ikke noget,
/// nogen skal forholde sig til.
/// </summary>
public sealed class Referatbygger
{
    private readonly LlmRunner _motor;

    public Referatbygger(LlmRunner motor) => _motor = motor;

    /// <summary>
    /// Over denne længde deles mødet op. Under den er der ikke noget at vinde
    /// ved det — én kørsel er både hurtigere og mere sammenhængende, fordi
    /// modellen ser hele mødet på én gang.
    /// </summary>
    public const int DelOpOver = 5000;

    /// <summary>
    /// Hvor lange noterne må blive, før de foldes sammen.
    ///
    /// HVORFOR DEN IKKE ER DEN SAMME SOM <see cref="DelOpOver"/>
    ///
    /// Foldning KOGER NED. Hver runde er en ny gennemlæsning, og noget går af
    /// hver gang — og det, der går af, er detaljerne: tallene, navnene,
    /// begrundelserne. Præcis det, referatet skal have.
    ///
    /// Med grænsen på 5.000 foldede et almindeligt møde på en time, fordi seks
    /// blokke giver omkring 9.000 tokens noter. Det kostede detaljer på et
    /// møde, der udmærket kunne skrives sammen i én omgang.
    ///
    /// Foldning er derfor forbeholdt de virkelig lange møder, hvor alternativet
    /// ikke er «lidt tabt detalje», men «kan ikke lade sig gøre».
    /// </summary>
    private const int FoldOver = 11000;

    /// <summary>
    /// Hvor meget mødetekst der læses ad gangen, i tokens.
    ///
    /// 2500 er valgt, så stykket plus svaret plus instruktionen lander under
    /// 4096 i kontekst. Det er lille nok til at ligge helt på et 6 GB-kort med
    /// en 8B-model, og stort nok til at et emne sjældent bliver klippet over.
    /// </summary>
    private const int StykkeStoerrelse = 2500;

    /// <summary>Dansk tekst lander omkring 3 tegn pr. token. Bevidst rundhåndet.</summary>
    private static int Tokens(string tekst) => tekst.Length / 3;

    /// <summary>
    /// Hvor mange blokke mødet deles i, og hvor lang tid det cirka tager.
    ///
    /// HVORFOR DET SKAL KUNNE SPØRGES OM PÅ FORHÅND
    ///
    /// Et referat af et langt møde tager tid, og det er i orden — men kun hvis
    /// man ved det, FØR man trykker. Uden et tal sidder man og venter på noget,
    /// man tror er gået i stå, og trykker afbryd efter fem minutter.
    ///
    /// Tallet er groft med vilje. Det er bedre at sige «cirka ti minutter» og
    /// tage fejl af to end at sige «7 minutter og 40 sekunder» og tage fejl af
    /// de samme to — det sidste ligner en fejl, det første ligner et skøn.
    /// </summary>
    public static (int Blokke, TimeSpan Skoen) Forventning(string udskrift)
    {
        var tokens = Tokens(udskrift);

        if (tokens <= DelOpOver)
        {
            // Een koersel. Tiden er domineret af, hvor lang prompten er.
            return (1, TimeSpan.FromSeconds(60 + tokens / 12.0));
        }

        var blokke = (int)Math.Ceiling((double)tokens / StykkeStoerrelse);

        // MÅLT, IKKE GÆTTET.
        //
        // Her stod 90 sekunder pr. blok. Det var skrevet ud fra, hvad et RTX
        // 2060 BURDE kunne, og det var forkert med en faktor tre: appen lovede
        // ti minutter, og kørslen tog femogtyve. Brugeren troede, den var gået
        // i stå — og et skøn, der gør dét, er værre end intet skøn.
        //
        // Målt 14. august 2026 på et 61-minutters møde: 12.513 tokens, seks
        // blokke plus sammenskrivning, modellen helt på kortet og GPU'en på
        // 100 % hele vejen. Hele kørslen tog 43,6 minutter — 374 sekunder pr.
        // kørsel.
        //
        // Tallet har været forkert to gange: først 90 sekunder, gættet ud fra
        // hvad kortet BURDE kunne, så 240 fra en halv måling. Begge gange lovede
        // appen for lidt, og brugeren sad og troede, den var gået i stå.
        //
        // Nu står der det målte, rundet en anelse op. Et skøn, der er lidt for
        // højt, koster en behagelig overraskelse; et, der er for lavt, koster
        // tilliden — og den er dyrere.
        const double sekunderPrBlok = 390.0;

        var sekunder = (blokke + 1 + Foldninger(blokke)) * sekunderPrBlok;

        return (blokke, TimeSpan.FromSeconds(sekunder));
    }

    /// <summary>
    /// Hvor mange ekstra runder der skal til, før noterne er korte nok til at
    /// samles i én kørsel. Se foldningen i <see cref="ByggAsync"/>.
    /// </summary>
    private static int Foldninger(int blokke)
    {
        // Hver blok giver omkring 600 tokens noter. Passer de i een koersel, er
        // der ingen foldning; ellers halveres antallet for hver runde.
        var runder = 0;
        var noter = blokke * 600;

        while (noter > FoldOver && blokke > 1)
        {
            runder++;
            blokke = (int)Math.Ceiling(blokke / 4.0);   // ca. fire noter pr. gruppe
            noter = blokke * 600;
        }

        return runder;
    }

    public async Task<LlmResult> ByggAsync(
        string modelPath,
        PromptTemplate skabelon,
        IReadOnlyDictionary<string, string?> felter,
        IProgress<LlmProgress>? fremdrift = null,
        CancellationToken ct = default)
    {
        var udskrift = felter.TryGetValue("transskription", out var t) ? t ?? "" : "";

        // Korte møder køres som før. Modellen ser hele mødet, og det giver et
        // mere sammenhængende referat end noget, der er samlet af stykker.
        if (Tokens(udskrift) <= DelOpOver)
            return await _motor.RunAsync(modelPath, skabelon, Udfyld(skabelon.UserPrompt, felter), fremdrift, ct);

        var stykker = Del(udskrift);
        var brugt = TimeSpan.Zero;

        // HVEM ER HVEM.
        //
        // Uden det her blev karrierehistorien fra ét menneske hæftet på et
        // andet. Målt 14. august 2026: referatet af et møde med tre deltagere
        // nævnte to af dem NUL gange og tilskrev den enes livsforløb til den
        // anden.
        //
        // Årsagen var ikke modellen, men opdelingen: hver blok blev læst helt
        // isoleret. Blok 4 hørte nogen fortælle om sin baggrund uden at vide,
        // at personen blev præsenteret i blok 1 — så den gættede på det sidste
        // navn, den havde set.
        //
        // Deltagerne findes derfor ÉN gang, i begyndelsen af mødet, hvor folk
        // præsenterer sig. Listen følger med ind i hver eneste blok bagefter.
        fremdrift?.Report(new LlmProgress("Finder ud af, hvem der deltager …", Percent: 1));

        var deltagere = "";

        try
        {
            var start = string.Join("\n", stykker.Take(2));

            var d = await _motor.RunAsync(modelPath, Deltagerliste(),
                Deltagerliste().UserPrompt.Replace("{{stykke}}", start), null, ct);

            brugt += d.Elapsed;
            deltagere = d.Text.Trim();
        }
        catch (Exception)
        {
            // Kan deltagerne ikke findes, koeres der videre uden. Et referat
            // uden navne er bedre end intet referat.
        }

        // Fremdrift: gennemlæsningen er langt den største del af arbejdet og
        // får derfor 0-85. Sammenskrivningen er én kørsel og får resten.
        var noter = new List<string>();

        for (var i = 0; i < stykker.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var fra = 85.0 * i / stykker.Count;
            var til = 85.0 * (i + 1) / stykker.Count;

            fremdrift?.Report(new LlmProgress($"Læser mødet igennem — del {i + 1} af {stykker.Count}",
                Percent: fra));

            // FREMDRIFTEN SKAL RYKKE SIG INDE I BLOKKEN.
            //
            // Her stod null. Bjælken flyttede sig derfor kun MELLEM blokke, og
            // hver blok tog seks minutter — seks minutters fuldstændig
            // stilstand, seks gange. Målt 14. august 2026: hele kørslen tog 43
            // minutter, og brugeren troede med god grund, at den var gået i stå.
            //
            // Nu oversættes blokkens egen fremdrift til det udsnit af den
            // samlede bjælke, blokken fylder. Ordtællingen kommer med, for et
            // tal, der tæller op, er den eneste måde at se forskel på «arbejder»
            // og «hænger».
            var blokFremdrift = new Progress<LlmProgress>(p =>
            {
                var andel = p.Percent > 0 ? p.Percent / 100.0 : 0;

                fremdrift?.Report(new LlmProgress(
                    p.Ord > 0
                        ? $"Læser mødet igennem — del {i + 1} af {stykker.Count} · {p.Ord} ord"
                        : $"Læser mødet igennem — del {i + 1} af {stykker.Count}",
                    Ord: p.Ord,
                    Percent: fra + (til - fra) * andel,
                    Forloebet: p.Forloebet));
            });

            // Blokken faar TRE ting med: hvem der deltager, hvad der lige er
            // sket, og sit eget stykke. Halen fra forrige blok er kort med
            // vilje — den skal give traaden, ikke fylde konteksten op.
            var hale = noter.Count > 0 ? Hale(noter[^1], 700) : "";

            var prompt = Gennemlaesning().UserPrompt
                .Replace("{{deltagere}}", deltagere.Length > 0 ? deltagere : "(ikke oplyst)")
                .Replace("{{foer}}", hale.Length > 0 ? hale : "(dette er begyndelsen af mødet)")
                .Replace("{{stykke}}", stykker[i]);

            var del = await _motor.RunAsync(modelPath, Gennemlaesning(), prompt, blokFremdrift, ct);

            brugt += del.Elapsed;

            var tekst = del.Text.Trim();
            if (tekst.Length > 0) noter.Add(tekst);
        }

        // FOLDNING. Uden den ville der stadig være en øvre grænse, bare
        // flyttet: 18 blokke fra et tre-timers møde giver omkring 11.000
        // tokens noter, og så er samleturen lige så tung, som den ene store
        // kørsel var. Bliver noterne for lange, læses de sammen i omgange,
        // indtil de passer. Så er der ingen grænse tilbage — et længere møde
        // er bare en runde mere.
        var runde = 0;
        while (Tokens(string.Join("\n\n", noter)) > FoldOver && noter.Count > 1)
        {
            ct.ThrowIfCancellationRequested();
            runde++;

            fremdrift?.Report(new LlmProgress("Samler mødet …", Percent: 85 + Math.Min(8, runde * 3)));

            var naeste = new List<string>();

            foreach (var gruppe in Grupper(noter))
            {
                var foldPrompt = Gennemlaesning().UserPrompt
                    .Replace("{{deltagere}}", deltagere.Length > 0 ? deltagere : "(ikke oplyst)")
                    .Replace("{{foer}}", "(dette er noter fra flere dele af mødet)")
                    .Replace("{{stykke}}", gruppe);

                var del = await _motor.RunAsync(modelPath, Gennemlaesning(), foldPrompt, null, ct);

                brugt += del.Elapsed;

                var tekst = del.Text.Trim();
                naeste.Add(tekst.Length > 0 ? tekst : gruppe);
            }

            // Gik der ikke noget af, er der ikke mere at hente. Saa er det
            // bedre at koere videre med det, der er, end at blive haengende.
            if (naeste.Count >= noter.Count) { noter = naeste; break; }

            noter = naeste;
        }

        fremdrift?.Report(new LlmProgress("Skriver referatet sammen …", Percent: 93));

        var sidsteFremdrift = new Progress<LlmProgress>(p =>
            fremdrift?.Report(new LlmProgress(
                p.Ord > 0 ? $"Skriver referatet sammen … {p.Ord} ord" : "Skriver referatet sammen …",
                Ord: p.Ord,
                Percent: 93 + 7 * (p.Percent > 0 ? p.Percent / 100.0 : 0),
                Forloebet: p.Forloebet)));

        // Sidste kørsel bruger BRUGERENS skabelon. Det er hele pointen: den
        // bestemmer stadig, hvordan dokumentet ser ud — kun det, den læser, er
        // skiftet ud med gennemlæsningen frem for den rå udskrift.
        // Deltagerlisten lægges FORREST i det, den afsluttende kørsel læser.
        // Uden den stod referatet uden deltagere overhovedet — og et
        // mødereferat, der ikke siger hvem der var med, kan ikke bruges.
        var tilSkrivning = deltagere.Length > 0
            ? "DELTAGERE I MØDET:\n" + deltagere + "\n\nNOTER FRA MØDET:\n" + string.Join("\n\n", noter)
            : string.Join("\n\n", noter);

        var samlet = new Dictionary<string, string?>(felter, StringComparer.OrdinalIgnoreCase)
        {
            ["transskription"] = tilSkrivning
        };

        var sidste = await _motor.RunAsync(modelPath, skabelon,
            Udfyld(skabelon.UserPrompt, samlet), sidsteFremdrift, ct);

        fremdrift?.Report(new LlmProgress("Færdig", Percent: 100));

        return sidste with { Elapsed = brugt + sidste.Elapsed };
    }

    /// <summary>
    /// Samler noter to og to (eller flere), så hver gruppe passer i én kørsel.
    /// Rækkefølgen bevares — et møde er kronologisk, og bytter man rundt på
    /// stykkerne, mister sammenskrivningen tråden i, hvad der fulgte af hvad.
    /// </summary>
    private static IEnumerable<string> Grupper(List<string> noter)
    {
        var nu = new StringBuilder();

        foreach (var n in noter)
        {
            if (nu.Length > 0 && Tokens(nu.ToString()) + Tokens(n) > StykkeStoerrelse)
            {
                yield return nu.ToString().Trim();
                nu.Clear();
            }

            nu.AppendLine(n).AppendLine();
        }

        if (nu.Length > 0) yield return nu.ToString().Trim();
    }

    /// <summary>
    /// Instruktionen til gennemlæsningen af ét stykke.
    ///
    /// Den beder om en GENFORTÆLLING, ikke et resumé. Et resumé af hvert
    /// stykke ville koge mødet ned to gange — først her, så igen ved
    /// sammenskrivningen — og så var der ingenting tilbage. Det er præcis den
    /// fejl, det korte referat led af.
    /// </summary>
    private static PromptTemplate Gennemlaesning() => new()
    {
        Name = "gennemlaesning",
        Temperature = 0.2,

        // 1600 frem for 1200. Det gamle loft klippede sidste afsnit midt i en
        // saetning: «... ikke om at have tusind tilfaelde, men om at have 10».
        MaxTokens = 1600,

        SystemPrompt =
            "Du læser et stykke af en mødetranskription og skriver ned, hvad der blev sagt.\n\n" +
            "Skriv på dansk, i almindelige sætninger og afsnit.\n\n" +
            "HVEM SIGER HVAD er det vigtigste. Transkriptionen har ingen navne på talerne, " +
            "så du skal slutte dig til det af sammenhængen — og du får deltagerlisten " +
            "og slutningen af forrige stykke med netop derfor. Er du i tvivl om, hvem " +
            "der sagde noget, så skriv «en af deltagerne» frem for at gætte på et navn. " +
            "At tillægge én person en andens udtalelser er den værste fejl, du kan lave.\n\n" +
            "Dette er IKKE et resumé. Genfortæl indholdet fyldigt: hvad emnet var, " +
            "hvilke tal og navne der blev nævnt, hvem der mente hvad, og hvad de begrundede " +
            "det med. Tag forbehold og indvendinger med.\n\n" +
            "Skriv aldrig et tal, der ikke står i teksten. Find ikke på noget — heller ikke " +
            "navne. Er noget uklart, så skriv at det er uklart.\n\n" +
            "Udelad høflighedsfraser, small talk og gentagelser. Alt andet skal med.\n\n" +
            "Svar kun med genfortællingen — ingen overskrifter, ingen indledning.",

        UserPrompt =
            "DELTAGERE I MØDET:\n{{deltagere}}\n\n" +
            "SLUTNINGEN AF DET FOREGÅENDE STYKKE (til sammenhæng — skriv den ikke om):\n{{foer}}\n\n" +
            "STYKKET, DU SKAL SKRIVE NED:\n{{stykke}}"
    };

    /// <summary>
    /// Finder deltagerne én gang, ud fra mødets begyndelse.
    ///
    /// Det er dér, folk præsenterer sig — og listen er værdiløs, hvis den
    /// først bliver lavet, når halvdelen af mødet er læst. Den følger med ind
    /// i hver eneste blok bagefter, så ingen bliver forvekslet.
    /// </summary>
    private static PromptTemplate Deltagerliste() => new()
    {
        Name = "deltagere",
        Temperature = 0.0,
        MaxTokens = 400,
        SystemPrompt =
            "Du læser begyndelsen af en mødetranskription og finder ud af, hvem der deltager.\n\n" +
            "Transkriptionen har ingen navne på talerne. Folk præsenterer sig som regel selv " +
            "i begyndelsen — brug det.\n\n" +
            "Svar med én linje pr. person: navn, og hvad personen er, hvis det siges. " +
            "Fx «Espen Sjøl — sælger hos CloudWorks, 15-20 års erfaring med IAM».\n\n" +
            "Find ikke på navne. Er der en taler, du ikke kan navngive, så skriv " +
            "«en deltager mere, navn ikke nævnt». Skriv intet andet end listen.",
        UserPrompt = "{{stykke}}"
    };

    /// <summary>Slutningen af en tekst, cirka så mange tokens. Brydes på et afsnit.</summary>
    private static string Hale(string tekst, int tokens)
    {
        var tegn = tokens * 3;
        if (tekst.Length <= tegn) return tekst;

        var del = tekst[^tegn..];
        var punktum = del.IndexOf(". ", StringComparison.Ordinal);

        return punktum > 0 && punktum < del.Length - 40 ? del[(punktum + 2)..] : del;
    }

    /// <summary>
    /// Deler udskriften i stykker på sætningsgrænser.
    ///
    /// Der klippes aldrig midt i en sætning: et stykke, der begynder på
    /// halvdelen af en sætning, læses forkert, og modellen finder på det, der
    /// mangler foran.
    /// </summary>
    private static List<string> Del(string udskrift)
    {
        var stykker = new List<string>();
        var nu = new StringBuilder();

        // Der deles paa linjer foerst — Whispers udskrift har een saetning pr.
        // linje — og kun hvis en linje er urimeligt lang, deles den paa punktum.
        foreach (var linje in udskrift.Replace("\r\n", "\n").Split('\n'))
        {
            if (linje.Trim().Length == 0) continue;

            if (Tokens(nu.ToString()) + Tokens(linje) > StykkeStoerrelse && nu.Length > 0)
            {
                stykker.Add(nu.ToString().Trim());
                nu.Clear();
            }

            nu.AppendLine(linje.Trim());
        }

        if (nu.Length > 0) stykker.Add(nu.ToString().Trim());

        return stykker;
    }

    private static string Udfyld(string skabelon, IReadOnlyDictionary<string, string?> felter)
    {
        var ud = skabelon;
        foreach (var (navn, vaerdi) in felter) ud = ud.Replace("{{" + navn + "}}", vaerdi ?? "");
        return ud;
    }
}
