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
    public static IReadOnlyList<string> Byg(IEnumerable<string> vaageord)
    {
        var rene = vaageord
            .Select(Vaageord.Rens)
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rene.Count == 0) rene = Vaageord.Standardord.ToList();

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
    /// TRE GANGE REN GÆTNING. Med fjorten udtryk er gætning 7 %, og grænsen
    /// bliver 21 %. Det lyder lavt og er det ikke: sandsynlighederne fordeles
    /// mellem alle udtrykkene, så et udtryk, der får en femtedel af det hele,
    /// skiller sig markant ud fra de tretten andre.
    ///
    /// Grænsen følger listens længde i stedet for at være et fast tal. Så
    /// holder den, når nogen tilføjer et vågeord mere.
    /// </remarks>
    public static double Graense(int antalUdtryk) =>
        antalUdtryk <= 1 ? 1.0 : Math.Min(0.9, 3.0 / antalUdtryk);

    /// <summary>
    /// Blev et vågeord hørt sikkert nok?
    /// </summary>
    public static bool Taeller(Vaageordsfund fund, IEnumerable<string> vaageord, int antalUdtryk)
    {
        if (fund.Sikkerhed < Graense(antalUdtryk)) return false;

        var v = Vaageord.Rens(fund.Udtryk);
        return v.Length > 0
               && vaageord.Any(o => Vaageord.Rens(o).Equals(v, StringComparison.OrdinalIgnoreCase));
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
