using System.Text;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>Én rettelse, der blev anvendt. Bruges til at vise og til at måle.</summary>
public sealed record AppliedCorrection(string Heard, string Corrected, int Count);

/// <summary>
/// Retter kendte hørefejl i en transskription, efter Whisper er færdig.
///
/// HVORFOR DET HER LAG FINDES
///
/// Whisper kan ikke trænes. Vægtene er faste, og de ændrer sig ikke, uanset
/// hvor meget der bliver talt ind. Ordlisten i <c>initial_prompt</c> er det
/// eneste håndtag på selve genkendelsen, og målt 12. august 2026 ramte den
/// kun to ud af otte fagord, der stod i den.
///
/// Derfor ligger læringen HER i stedet: en liste over «det her blev hørt som
/// det her, og det er forkert». Den er ikke gætværk — den bygges af de
/// rettelser, brugeren faktisk laver, og hver regel kan efterprøves ved at
/// køre den samme lyd igennem igen.
///
/// Det er forskellen på at håbe og at måle: ordlisten påvirker en model, vi
/// ikke kan se ind i. Rettelserne her gør præcis det, der står i dem.
///
/// TO REGLER, DER IKKE MÅ BRYDES
///
/// Den rå tekst gemmes altid ved siden af. En rettelse, der viser sig at være
/// forkert, må kunne fortrydes uden at optagelsen skal skrives ud igen.
///
/// Der rettes kun på hele ord. Uden ordgrænser ville en regel om «kø» ramme
/// inde i «køber», og en rettelse, der ødelægger rigtige ord, er værre end
/// den hørefejl, den skulle rette.
/// </summary>
public sealed class TranscriptCorrector
{
    private readonly IReadOnlyList<AliasRule> _regler;

    public TranscriptCorrector(IReadOnlyList<AliasRule> regler)
    {
        // Laengste foerst: "kerne sys" skal rettes til "Kernesys", foer en
        // regel om "sys" alene naar at roere ved den.
        _regler = regler.OrderByDescending(r => r.WordCount)
                        .ThenByDescending(r => r.Normalized.Length)
                        .ToList();
    }

    /// <summary>Bygger retteren ud fra de regler, der er godkendt til automatisk brug.</summary>
    public static TranscriptCorrector FromStore(LearningStore store) => new(store.AutoApplyRules());

    /// <summary>
    /// Retter teksten og fortæller hvad der blev ændret.
    ///
    /// Der ændres ikke på linjeskift, tegnsætning eller mellemrum — kun de ord,
    /// en regel peger på. En transskription, der er blevet «pænere» undervejs,
    /// kan ikke længere holdes op mod lyden.
    /// </summary>
    public (string Tekst, IReadOnlyList<AppliedCorrection> Ændringer) Apply(string tekst)
    {
        if (_regler.Count == 0 || string.IsNullOrEmpty(tekst))
            return (tekst, Array.Empty<AppliedCorrection>());

        var ændringer = new List<AppliedCorrection>();
        var resultat = tekst;

        foreach (var regel in _regler)
        {
            if (string.IsNullOrWhiteSpace(regel.Normalized)) continue;

            // Reglen er gemt normaliseret (smaa bogstaver, uden tegn). Den skal
            // derfor matche uanset store bogstaver og med vilkaarligt mellemrum
            // mellem ordene — "kerne, sys" og "Kerne Sys" er den samme fejl.
            var dele = regel.Normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(Regex.Escape);
            var mønster = @"(?<![\p{L}\p{N}])" + string.Join(@"[\s,.\-]+", dele) + @"(?![\p{L}\p{N}])";

            var antal = 0;
            resultat = Regex.Replace(resultat, mønster, m =>
            {
                antal++;
                return BevarStortBegyndelsesbogstav(m.Value, regel.Canonical);
            }, RegexOptions.IgnoreCase);

            if (antal > 0) ændringer.Add(new AppliedCorrection(regel.Normalized, regel.Canonical, antal));
        }

        return (resultat, ændringer);
    }

    /// <summary>
    /// Stod det oprindelige ord først i en sætning, skal erstatningen også
    /// gøre det. Ellers får man «vi bruger scim» midt i en tekst, hvor alt
    /// andet er skrevet korrekt — og det ligner en ny fejl.
    ///
    /// Er den kanoniske form skrevet med stort i forvejen (SCIM, MitID),
    /// røres den ikke: dét er stavemåden, brugeren har valgt.
    /// </summary>
    private static string BevarStortBegyndelsesbogstav(string fundet, string kanonisk)
    {
        if (kanonisk.Length == 0 || char.IsUpper(kanonisk[0])) return kanonisk;
        if (fundet.Length == 0 || !char.IsUpper(fundet[0])) return kanonisk;

        return char.ToUpperInvariant(kanonisk[0]) + kanonisk[1..];
    }

    /// <summary>
    /// Retter tekstfilen på disken og lægger den rå udgave ved siden af som
    /// <c>.raa.txt</c>, hvis den ikke allerede ligger der.
    ///
    /// Returnerer hvad der blev ændret. Er der intet at rette, røres filerne
    /// ikke — så bliver der ikke en .raa.txt for hver transskription, der
    /// alligevel var ren.
    /// </summary>
    public IReadOnlyList<AppliedCorrection> ApplyToFile(string tekstFil)
    {
        if (!File.Exists(tekstFil)) return Array.Empty<AppliedCorrection>();

        var original = File.ReadAllText(tekstFil, Encoding.UTF8);
        var (rettet, ændringer) = Apply(original);
        if (ændringer.Count == 0) return ændringer;

        var råFil = Path.ChangeExtension(tekstFil, ".raa.txt");
        if (!File.Exists(råFil))
            File.WriteAllText(råFil, original, new UTF8Encoding(false));

        File.WriteAllText(tekstFil, rettet, new UTF8Encoding(false));
        return ændringer;
    }
}
