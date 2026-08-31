namespace NoteApp.Core;

/// <summary>Det, motoren meldte, at den hørte.</summary>
/// <param name="Udtryk">Udtrykket fra listen — som det står i filen.</param>
/// <param name="Sikkerhed">Hvor sikker den var, 0–1.</param>
public sealed record Vaageordsfund(string Udtryk, double Sikkerhed);

/// <summary>
/// Listen, motoren får lov at vælge imellem — og hvornår et valg tæller.
///
/// HVORFOR DER SKAL VÆRE ANDET END VÅGEORDENE PÅ LISTEN
///
/// whisper-command i «guided mode» binder afkodningen til de udtryk, den får
/// udleveret. Det er dét, der gør den brugbar: den kan ikke finde på
/// «[MUSIK]» eller «[Tekstet af Vigre]», som den frie transskription gjorde.
///
/// Men den kan heller ikke svare «ingenting». Den VÆLGER altid et af dem.
///
/// Målt 30-08-2026 med kun to udtryk på listen:
///
///   hej pia = 0.584   hey pia = 0.416
///
/// De to tal lægger sammen til ét, og med to muligheder er 50 % ren
/// gætning. Der kom en «detektion» hvert 60. millisekund, også når der ikke
/// blev sagt noget.
///
/// Derfor står der lokkeord på listen. Med fjorten udtryk falder gætningen
/// til 7 %, og et rigtigt «Hej Pia» skiller sig ud. Lokkeordene er
/// almindelige danske vendinger — dét, en mikrofon i et kontor faktisk
/// hører — så modellen har et sted at gå hen, når ordet ikke blev sagt.
/// </summary>
public static class Vaageordsliste
{
    /// <summary>
    /// Vendinger, der ikke skal udløse noget.
    /// </summary>
    /// <remarks>
    /// De skal ligne det, der bliver sagt i et rum: almindelig tale, ikke
    /// nonsens. Et lokkeord, der aldrig kan forveksles med noget, hjælper
    /// ikke — det er netop forvekslingerne, der skal have et andet sted at
    /// gå hen end vågeordet.
    /// </remarks>
    public static readonly IReadOnlyList<string> Lokkeord = new[]
    {
        "ja det er fint",
        "nej ikke lige nu",
        "vi ses i morgen",
        "hvad sagde du",
        "det ved jeg ikke",
        "lige et øjeblik",
        "kan du høre mig",
        "det lyder godt",
        "jeg kommer nu",
        "tak for det",
        "hej med dig",
        "hvordan går det",
    };

    /// <summary>
    /// Hele listen, motoren skal have: vågeordene først, lokkeordene efter.
    /// </summary>
    public static IReadOnlyList<string> Byg(IEnumerable<string> vaageord, string? sprog = null)
    {
        var rene = vaageord
            .Select(Vaageord.Rens)
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rene.Count == 0) rene = Vaageord.Standardord(sprog).ToList();

        // Lokkeord, der ligner et vaageord, ville tage fra det. Det er
        // usandsynligt, men listen kan aendres af brugeren.
        var ud = new List<string>(rene);
        ud.AddRange(Lokkeord.Where(l => !rene.Contains(l, StringComparer.OrdinalIgnoreCase)));

        return ud;
    }

    /// <summary>
    /// Hvor sikker skal motoren være, før et fund tæller?
    /// </summary>
    /// <remarks>
    /// HALVANDEN GANG REN GÆTNING. Med fjorten udtryk er gætning 7 %, og
    /// grænsen bliver knap 11 %.
    ///
    /// DER STOD TRE GANGE, OG DET VAR MÅLT FORKERT — ikke fordi tallet var
    /// gættet, men fordi det aldrig var holdt op mod, hvad et RIGTIGT
    /// vågeord faktisk scorer.
    ///
    /// Målt 31-08-2026 på brugerens egen maskine: «hej pia», sagt tydeligt og
    /// hørt korrekt, fik 0,36 mod en grænse på 0,21. Den slap lige igennem.
    /// Et vågeord sagt en anelse svagere, længere fra mikrofonen eller midt i
    /// en sætning lander under, og så sker der ingenting — uden at noget
    /// siger hvorfor. Brugeren sagde «Hej Pia» i tyve sekunder, før én af
    /// dem var stærk nok.
    ///
    /// HVORFOR EN LAV GRÆNSE ER FORSVARLIG HER. Den beskytter kun mod ÉN
    /// ting: at støj bliver bedømt til at være et vågeord. Den beskytter
    /// ikke mod lokkeordene — de kasseres på navnet i <see cref="Taeller"/>,
    /// uanset hvor sikker motoren er. Og et lokkeord kan score højt: «nej
    /// ikke lige nu» blev målt til 0,54, altså højere end det rigtige
    /// vågeord nogensinde nåede.
    ///
    /// Målt samme dag: fyrre sekunders stilhed gav NUL fund. Motoren melder
    /// ikke noget, når der ikke bliver sagt noget, og så er det ikke støjen,
    /// grænsen skal holdes stram for.
    ///
    /// Grænsen følger listens længde i stedet for at være et fast tal. Så
    /// holder den, når nogen tilføjer et vågeord mere.
    /// </remarks>
    public static double Graense(int antalUdtryk) =>
        antalUdtryk <= 1 ? 1.0 : Math.Min(0.9, Faktor / antalUdtryk);

    /// <summary>
    /// Hvor mange gange ren gætning et fund skal være.
    /// </summary>
    /// <remarks>
    /// DEN HAR VÆRET BEGGE VEJE, OG BEGGE GANGE MED EN GRUND.
    ///
    /// 3,0 fra begyndelsen. Sat ned til 1,5 den 31-08-2026, fordi et rigtigt
    /// vågeord kun nåede 0,36 — men dét tal var i sig selv et symptom: motoren
    /// bedømte otte sekunders stilhed mod to ord. Da vinduet blev sat til
    /// 1500 ms samme dag, steg de rigtige træf til 0,12–0,37, og de fleste af
    /// dem ligger over 0,30.
    ///
    /// SÅ KOM RADIOEN. Med grænsen på 0,11 begyndte appen at optage af sig
    /// selv, mens der spillede radio i rummet. Det er ikke overraskende: der
    /// er ingen luft mellem 0,11 og en tilfældig stemme, der siger noget, der
    /// ligner.
    ///
    /// EN FALSK START ER VÆRRE END EN, DER MANGLER. Siger man «Hej Pia» igen,
    /// koster det to sekunder. En optagelse, ingen har bedt om, sender lyd ud
    /// af huset — og det er dét, hele resten af appen er bygget for at undgå.
    /// </remarks>
    public const double Faktor = 3.0;

    /// <summary>Er udtrykket ét af vågeordene?</summary>
    public static bool ErVaageord(string udtryk, IEnumerable<string> vaageord)
    {
        var v = Vaageord.Rens(udtryk);
        return v.Length > 0
               && vaageord.Any(o => Vaageord.Rens(o).Equals(v, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Blev et vågeord hørt sikkert nok?
    /// </summary>
    public static bool Taeller(Vaageordsfund fund, IEnumerable<string> vaageord, int antalUdtryk)
    {
        if (fund.Sikkerhed < Graense(antalUdtryk)) return false;

        return ErVaageord(fund.Udtryk, vaageord);
    }

    /// <summary>
    /// Blev der sagt et vågeord — når varianterne lægges sammen?
    /// </summary>
    /// <remarks>
    /// «HEJ PIA» OG «HEY PIA» ER DET SAMME ORD, OG DE DELER SANDSYNLIGHEDEN.
    ///
    /// Motoren fordeler sin sikkerhed mellem alle udtryk på listen. To
    /// stavemåder af det samme vågeord konkurrerer derfor med hinanden:
    /// målt 30-08-2026 fik «hej pia» 0,584 og «hey pia» 0,416 af det SAMME
    /// «Hej Pia». Hver for sig ser de svage ud. Lagt sammen er de 1,0.
    ///
    /// Det er derfor, et rigtigt vågeord kun nåede 0,36 den 31-08-2026: en
    /// del af det lå på den anden stavemåde. Grænsen bedømte den halve
    /// sikkerhed og kasserede et ord, der var sagt tydeligt.
    ///
    /// Her lægges de sammen, før der bedømmes. Det kan ikke give falske
    /// udslag: der summeres KUN over udtryk, der er vågeord, og lokkeordene
    /// tæller ikke med, uanset hvor højt de scorer.
    /// </remarks>
    public static bool Taeller(
        IEnumerable<Vaageordsfund> fund, IEnumerable<string> vaageord, int antalUdtryk)
    {
        var ord = vaageord as IReadOnlyCollection<string> ?? vaageord.ToList();

        var samlet = fund
            .Where(f => ErVaageord(f.Udtryk, ord))
            .Sum(f => f.Sikkerhed);

        return samlet >= Graense(antalUdtryk);
    }

    /// <summary>
    /// Piller motorens linje fra hinanden.
    /// </summary>
    /// <remarks>
    /// Formen er efterprøvet mod den rigtige udskrift 30-08-2026:
    ///
    ///   process_command_list: detected command: hej pia | p = 0.584216 | t = 555 ms
    ///
    /// Udtrykket står med fed, altså mellem ANSI-koder, og de skal skæres væk
    /// før sammenligningen — ellers passer intet på noget.
    /// </remarks>
    public static Vaageordsfund? Laes(string? linje)
    {
        if (string.IsNullOrWhiteSpace(linje)) return null;

        const string maerke = "detected command:";
        var i = linje.IndexOf(maerke, StringComparison.Ordinal);
        if (i < 0) return null;

        var rest = Udenfarver(linje[(i + maerke.Length)..]);

        var dele = rest.Split('|');
        if (dele.Length < 2) return null;

        var udtryk = dele[0].Trim();
        if (udtryk.Length == 0) return null;

        var p = dele[1].Replace("p", "").Replace("=", "").Trim();

        return double.TryParse(p, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var tal)
            ? new Vaageordsfund(udtryk, tal)
            : null;
    }

    /// <summary>Skærer ANSI-farvekoder væk. Motoren skriver udtrykket med fed.</summary>
    public static string Udenfarver(string tekst)
    {
        var ud = new System.Text.StringBuilder(tekst.Length);

        for (var i = 0; i < tekst.Length; i++)
        {
            if (tekst[i] == '')
            {
                while (i < tekst.Length && tekst[i] != 'm') i++;
                continue;
            }

            ud.Append(tekst[i]);
        }

        return ud.ToString();
    }
}
