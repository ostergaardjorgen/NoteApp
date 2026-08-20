using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>Et sted i udskriften, der kan være en opgave.</summary>
public sealed record Opgavekandidat(
    int Linje,
    string Tekst,
    string Taler,
    string Tid,
    long FraMs,
    /// <summary>Det, der udløste fundet — vises fremhævet, så valget kan træffes hurtigt.</summary>
    string Udloeser,
    /// <summary>Hvorfor det ligner en opgave. Står på skærmen; et fund uden en grund kan ikke bedømmes.</summary>
    string Grund,
    int Styrke);

/// <summary>
/// Finder de steder i en udskrift, der KAN være en opgave.
///
/// DEN FORESLÅR, DEN AFGØR IKKE.
///
/// Det er med vilje, at der ikke står en sprogmodel bag. Målt 17-08-2026 på et
/// rigtigt møde havde den lokale model 12 % dækning — to af sytten udsagn — og
/// fandt selv på navne, der aldrig blev sagt. En opgaveliste, der stille
/// mangler syv af otte, er værre end ingen, fordi man stoler på den. Og en
/// opgave, ingen har aftalt, er den dyreste fejl af dem alle.
///
/// Her leder appen efter det, folk FAKTISK siger, når de påtager sig noget:
/// «jeg sender», «kan du», «vi aftaler», «inden fredag». Den kan ikke finde på
/// noget, den kan ikke tage fejl af hvem der sagde det, og den kan ikke give et
/// andet svar i morgen. Til gengæld tager den for meget med — og det er den
/// rigtige vej at fejle, når et menneske alligevel skal sige ja eller nej.
///
/// NOTERNE ER DET STÆRKESTE SIGNAL
///
/// Skrev man en note under mødet, var det fordi noget betød noget. Det er ikke
/// et gæt om sproget; det er brugerens egen markering. De vejer derfor tungest.
/// </summary>
public static class Opgavefund
{
    /// <summary>
    /// Vendinger, hvor nogen påtager sig noget. Både dansk og norsk — møder
    /// holdes på begge, tit i den samme sætning.
    /// </summary>
    private static readonly (string Moenster, string Grund, int Styrke)[] Signaler =
    {
        // ---- nogen påtager sig noget selv
        (@"\bjeg (sender|sender dig|smider|laver|tager|kigger på|kikker på|ser på|følger op|vender tilbage|vender retur|skriver|indkalder|booker|opretter|undersøger|tjekker|checker)\b",
            "nogen påtager sig noget", 3),
        (@"\bjeg (kommer tilbake|sender over|tar|ser på|følger opp|skal se|skal sende|skal ta)\b",
            "nogen påtager sig noget", 3),
        (@"\b(jeg|vi) skal (lige )?(have|sende|lave|finde|tage|kigge|se|booke|indkalde|følge)\b",
            "noget skal gøres", 3),

        // ---- nogen bliver bedt om noget
        (@"\b(kan|kunne|vil|vil du gerne|må) du (lige )?(sende|lave|tage|kigge|se|finde|booke|skrive|dele|prøve)\b",
            "nogen bliver bedt om noget", 3),
        (@"\b(kan|kunne) (I|i|du|dere) (sende|lave|tage|se på|finne|dele)\b",
            "nogen bliver bedt om noget", 3),

        // ---- der bliver aftalt noget
        (@"\b(vi aftaler|vi aftalte|det aftaler vi|så aftaler vi|er vi enige om|vi blir enige|vi avtaler)\b",
            "der bliver aftalt noget", 3),
        (@"\b(så gør vi det|det gør vi|så siger vi det|det tager vi|lad os)\b",
            "der bliver besluttet noget", 2),
        (@"\b(vi må|vi bør|vi skal|vi trenger å|vi trænger til)\b",
            "noget mangler at blive gjort", 2),

        // ---- opfølgning
        (@"\b(følger op|følge op|følger opp|vender tilbage|vender retur|kommer tilbake|melder tilbage|giver besked|gir beskjed)\b",
            "der loves en opfølgning", 3),
        (@"\b(sætte op et møde|sætter et møde|book(e|er)? et møde|indkalde|invitere|sende en invitation|ny samtale|næste møde|neste møte)\b",
            "der skal aftales et møde", 2),

        // ---- noget skal sendes
        (@"\b(sende|sender|sendt|send mig|send over|videresende|dele|deler) (dig |jer |mig |over |et |en |de |den |det )?\w*(materiale|dokument|link|oplæg|tilbud|pris|slides|præsentation|oversigt|liste|kontrakt|aftale|forslag)\b",
            "der skal sendes noget", 3),
    };

    /// <summary>
    /// Ord, der peger på en frist. De gør ikke i sig selv noget til en opgave,
    /// men et løfte MED en dato er næsten altid en.
    /// </summary>
    //
    // RÆKKEFØLGEN I ALTERNATIVERNE BETYDER NOGET.
    //
    // .NET tager det FØRSTE alternativ, der passer — ikke det længste. Stod
    // «om en» før «om en uge», ville fundet blive vist som «(om en)», og den
    // tekst står på skærmen ved siden af forslaget. De lange står derfor først.
    private static readonly Regex Frist = new(
        @"\b(i dag|i morgen|i overmorgen|på (mandag|tirsdag|onsdag|torsdag|fredag|lørdag|søndag)|" +
        @"(mandag|tirsdag|onsdag|torsdag|fredag)en?|i næste uge|næste uge|neste uke|denne uge|denne uken|" +
        @"inden (udgangen af )?\w+|senest \w+|senest|inden for \w+ \w+|inden for \w+|" +
        @"om (halvanden uge|en uges tid|to uger|tre uger|fire uger|en uge|to uker|tre uker|en uke|" +
        @"en måned|to måneder|en måned|et par uger|et par dage|nogle dage|noen dager)|" +
        @"i (januar|februar|marts|april|maj|juni|juli|august|september|oktober|november|december)|" +
        @"\d{1,2}\.? ?(januar|februar|marts|april|maj|juni|juli|august|september|oktober|november|december)|" +
        @"efter (sommerferien|sommeren|ferien))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex[] Kompileret =
        Signaler.Select(s => new Regex(s.Moenster, RegexOptions.IgnoreCase | RegexOptions.Compiled)).ToArray();

    /// <summary>
    /// Kortere end det her er en replik ikke en opgave. «Ja, det gør vi» er
    /// et svar på noget, der stod i replikken før — og den er allerede fundet.
    /// </summary>
    private const int MindsteLaengde = 25;

    /// <summary>
    /// Grænsen mellem et forslag, der vises frem, og et, der ligger bag
    /// «vis også de svage».
    ///
    /// MÅLT PÅ ET RIGTIGT MØDE, 20-08-2026.
    ///
    /// Otte fund i 441 replikker. De to over grænsen var begge den samme
    /// rigtige aftale — «jeg tar kontakt med deg om en uke». De seks under var
    /// alle beskrivelser af den anden parts egne planer: «vi skal være med på
    /// et event», «vi skal gjøre en call-out-kampanje». Det er ikke aftaler
    /// fra mødet, og det er ikke noget, nogen skal gøre bagefter.
    ///
    /// «Vi skal» og «vi må» falder derfor under grænsen alene. De kommer over
    /// den, hvis der ALLIGEVEL står en frist i den samme replik — for så er
    /// det som regel en aftale og ikke en beskrivelse.
    ///
    /// De svage smides ikke væk. En opgaveliste, der stille udelader noget,
    /// er det, hele den her fremgangsmåde er valgt for at undgå — de ligger
    /// bare et klik væk i stedet for at fylde listen.
    /// </summary>
    public const int Vises = 3;

    /// <summary>
    /// Finder kandidaterne — både i det, der blev sagt, og i det, du selv
    /// skrev undervejs.
    /// </summary>
    /// <param name="mappe">Optagelsens mappe. Null springer noterne over.</param>
    public static List<Opgavekandidat> Find(Udskrift udskrift,
                                            IReadOnlyDictionary<string, string>? navne = null,
                                            string? mappe = null)
    {
        var fundne = new List<Opgavekandidat>();

        // ============ NOTERNE FØRST ============
        //
        // Skrev man en note under mødet, var det fordi noget betød noget.
        // Det er ikke et gæt om sproget — det er en markering, brugeren selv
        // har sat, mens det stod på. Derfor vejer de tungest af alt, og de
        // står øverst uanset hvad der ellers blev fundet.
        //
        // Rene bogmærker uden tekst springes over: de siger «her skete der
        // noget», men ikke hvad, og et forslag uden indhold kan man ikke
        // sige ja eller nej til.
        if (mappe is not null)
        {
            foreach (var n in MeetingNotebook.Read(mappe))
            {
                var tekst = n.Text.Trim();
                if (tekst.Length == 0) continue;

                fundne.Add(new Opgavekandidat(
                    -1, tekst, "Din note", n.Timecode, (long)(n.AtSeconds * 1000),
                    tekst, "du skrev det selv ned under mødet", 6));
            }
        }

        for (var i = 0; i < udskrift.Linjer.Count; i++)
        {
            var l = udskrift.Linjer[i];
            var t = l.Tekst.Trim();

            if (t.Length < MindsteLaengde) continue;

            var bedst = -1;
            var udloeser = "";

            for (var s = 0; s < Kompileret.Length; s++)
            {
                var m = Kompileret[s].Match(t);
                if (!m.Success) continue;

                if (bedst < 0 || Signaler[s].Styrke > Signaler[bedst].Styrke)
                {
                    bedst = s;
                    udloeser = m.Value;
                }
            }

            if (bedst < 0) continue;

            var styrke = Signaler[bedst].Styrke;
            var grund = Signaler[bedst].Grund;

            // En frist i den samme replik gør fundet markant staerkere. Det er
            // forskellen paa «vi skal have kigget paa det» og «jeg sender det
            // inden fredag».
            if (Frist.Match(t) is { Success: true } f)
            {
                styrke += 2;
                grund += $" med en frist ({f.Value})";
            }

            fundne.Add(new Opgavekandidat(
                i, t, Udskrift.Navn(l, navne), l.Tid, l.FraMs, udloeser, grund, styrke));
        }

        return fundne.OrderBy(k => k.FraMs).ToList();
    }
}
