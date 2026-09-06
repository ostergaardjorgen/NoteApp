using System.Text;

namespace NoteApp.Core;

/// <summary>Én kilde, der kom med i et dokument — og hvor meget af den.</summary>
public sealed record Kildehenvisning(string Navn, string Sti, int Tegn, bool Egen);

/// <summary>
/// Materialet, en skabelon skal bygge på — og listen over, hvad der kom med.
/// </summary>
/// <param name="Afkortet">
/// Sandt, når der var mere materiale end der var plads til. Et dokument bygget
/// på halvdelen skal sige det.
/// </param>
public sealed record Projektsvar(
    string Tekst,
    IReadOnlyList<Kildehenvisning> Kilder,
    bool Afkortet);

/// <summary>
/// Samler et projekts materiale til én tekst, en model kan læse.
/// </summary>
/// <remarks>
/// ============ HVORFOR DER IKKE BARE SENDES DET HELE ============
///
/// Et projekt kan være fire hundrede sider. Det kan ikke sendes, det ville
/// koste urimeligt, og en model, der får alt, svarer dårligere end en, der får
/// det rigtige. Så der skal vælges — og valget skal kunne efterprøves.
///
/// TO SLAGS MATERIALE KOMMER MED, og de dækker hver sin svaghed:
///
///   BEGYNDELSEN AF HVER FIL. Den siger, hvad dokumentet ER. Uden den ville
///   en fil, hvis emne ikke tilfældigvis rammer et af skabelonens afsnit,
///   være usynlig for modellen — også når den er den vigtigste i mappen.
///
///   PASSAGER, DER SVARER PÅ ET AFSNIT. Skabelonens afsnit er
///   fremsøgningsspørgsmålene: hedder et afsnit «Tidsplan», hentes de steder,
///   der handler om tidsplan. Det er dét, der gør et langt dokument brugbart
///   uden at sende det hele.
///
/// KILDERNE STÅR I SVARET. Hver stump er mærket med den fil, den kom fra, så
/// modellen kan henvise — og så mennesket kan slå efter. Et dokument, der
/// ikke kan efterprøves, er en påstand.
///
/// ET AFSNIT UDEN SVAR SIGES HØJT. Står der ingenting om tidsplan i
/// materialet, skrives det i teksten. Så kan skabelonen svare «det står ikke
/// i dine kilder» i stedet for at finde på noget, der lyder rigtigt.
/// </remarks>
public static class Projektkontekst
{
    /// <summary>Så meget af hver fils begyndelse kommer altid med.</summary>
    private const int Indledning = 1200;

    /// <summary>Så meget tekst omkring et træf.</summary>
    private const int Omkring = 600;

    /// <summary>Så mange steder pr. afsnit. Flere er sjældent flere svar.</summary>
    private const int StederPrAfsnit = 3;

    /// <summary>Loftet over hele materialet, i tegn.</summary>
    /// <remarks>
    /// Omtrent 30.000 tegn er i omegnen af 8.000 tokens. Det er rigeligt til
    /// et dokument og langt under, hvad modellen kan tage — grænsen står her
    /// for at holde prisen og svartiden nede, ikke fordi den skal.
    /// </remarks>
    public const int Standardloft = 30_000;

    public static Projektsvar Byg(Projekt projekt, IReadOnlyList<string> afsnit,
                                  int loft = Standardloft)
    {
        var filer = Projektkilder.Filer(projekt).Where(f => f.Laesbar).ToList();

        var sb = new StringBuilder();
        var kilder = new List<Kildehenvisning>();
        var afkortet = false;

        // ---------- 1) hvad ligger der overhovedet? ----------
        foreach (var fil in filer)
        {
            var tekst = Tekstcache.Hent(fil.Sti).Tekst;
            if (tekst.Length == 0) continue;

            if (sb.Length >= loft) { afkortet = true; break; }

            var stump = tekst.Length <= Indledning ? tekst : tekst[..Indledning] + " …";

            sb.AppendLine($"### KILDE: {fil.Filnavn}");
            sb.AppendLine(stump);
            sb.AppendLine();

            kilder.Add(new Kildehenvisning(fil.Filnavn, fil.Sti, stump.Length, fil.Egen));
        }

        // ---------- 2) det, afsnittene spørger om ----------
        foreach (var navn in afsnit)
        {
            if (string.IsNullOrWhiteSpace(navn)) continue;

            if (sb.Length >= loft) { afkortet = true; break; }

            var fund = Soegning.Soeg(navn, new Soegefilter(Projekt: new[] { projekt.Id }))
                .Where(f => f.Slags == Fundtype.Projektfil)
                .ToList();

            sb.AppendLine($"### OM AFSNITTET «{navn}»");

            if (fund.Count == 0)
            {
                // DET SKAL STAA. Ellers finder modellen paa noget, der lyder
                // rigtigt, og ingen kan se, at der ikke var daekning.
                sb.AppendLine($"Der står intet om «{navn}» i projektets materiale.");
                sb.AppendLine();
                continue;
            }

            foreach (var f in fund.Take(StederPrAfsnit))
            {
                var tekst = Tekstcache.Hent(f.Kilde).Tekst;
                if (tekst.Length == 0) continue;

                foreach (var t in f.Traef.Take(StederPrAfsnit))
                {
                    var stump = Omkring_(tekst, t.Position);

                    sb.AppendLine($"Fra {Path.GetFileName(f.Kilde)}:");
                    sb.AppendLine(stump);
                    sb.AppendLine();

                    if (sb.Length >= loft) { afkortet = true; break; }
                }

                if (afkortet) break;
            }
        }

        return new Projektsvar(sb.ToString().TrimEnd(), kilder, afkortet);
    }

    /// <summary>Teksten omkring et sted — klippet ved et mellemrum, ikke midt i et ord.</summary>
    private static string Omkring_(string tekst, int position)
    {
        var fra = Math.Max(0, position - Omkring / 2);
        var til = Math.Min(tekst.Length, position + Omkring / 2);

        // KLIP VED ET MELLEMRUM. Et uddrag, der begynder midt i et ord, ser
        // ud som en fejl i teksten frem for som et udsnit af den.
        while (fra > 0 && !char.IsWhiteSpace(tekst[fra])) fra--;
        while (til < tekst.Length && !char.IsWhiteSpace(tekst[til])) til++;

        var stump = tekst[fra..til].Trim();

        return (fra > 0 ? "… " : "") + stump + (til < tekst.Length ? " …" : "");
    }

    /// <summary>
    /// Kildelisten, som den skal stå nederst i dokumentet.
    /// </summary>
    /// <remarks>
    /// DEN ER IKKE PYNT. Et dokument bygget af en model er et forslag, indtil
    /// nogen har set efter — og det kan man kun, hvis der står hvor.
    /// </remarks>
    public static string Kildeliste(Projektsvar svar)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Kilder");
        sb.AppendLine();

        if (svar.Kilder.Count == 0)
        {
            sb.AppendLine("Der var intet materiale i projektets fundament.");
            return sb.ToString();
        }

        sb.AppendLine("Dokumentet er bygget på følgende filer fra projektets fundament:");
        sb.AppendLine();

        foreach (var k in svar.Kilder.OrderByDescending(k => k.Egen).ThenBy(k => k.Navn))
            sb.AppendLine($"- **{k.Navn}** — `{k.Sti}`");

        if (svar.Afkortet)
        {
            sb.AppendLine();
            sb.AppendLine("> Der var mere materiale, end der var plads til. "
                          + "Dokumentet bygger på et udsnit — se listen ovenfor for, "
                          + "hvad der kom med.");
        }

        return sb.ToString();
    }
}
