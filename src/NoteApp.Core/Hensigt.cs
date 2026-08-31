namespace NoteApp.Core;

/// <summary>Hvad det, der blev sagt, skal bruges til.</summary>
public enum Hensigt
{
    /// <summary>Ingen indledning genkendt. Teksten lander, hvor markøren står.</summary>
    Diktat,

    /// <summary>Gem det som en note.</summary>
    Note,

    /// <summary>Læg det på opgavelisten.</summary>
    Opgave,

    /// <summary>Læg det i kalenderen.</summary>
    Aftale,

    /// <summary>Søg efter det.</summary>
    Soegning,
}

/// <summary>Hensigten og det, der stod tilbage, da indledningen var skåret væk.</summary>
public sealed record Hensigtsfund(Hensigt Hvad, string Tekst);

/// <summary>
/// Læser, hvad en sætning skal bruges til, ud af dens første ord.
///
/// HVORFOR DET SIGES I SÆTNINGEN OG IKKE VÆLGES BAGEFTER
///
/// Vågeordet findes, for at man ikke skal røre noget. Kom der en rude op
/// bagefter med fire knapper, ville man skulle røre noget alligevel — og så
/// var man lige så godt tjent med at trykke på genvejstasten.
///
/// «Hej Pia, opret en opgave: ring til Anders på fredag» er én handling. Man
/// siger, hvad man vil, ligesom man ville sige det til et menneske.
///
/// HVORFOR DET LÆSES PÅ DEN PUDSEDE TEKST
///
/// Vågeordet høres af whisper med en lille model, bundet til en liste. Den er
/// god til at kende to faste udtryk og elendig til fri tale — målt
/// 30-08-2026: «[Skib]», «[MUSIK]», «[Tekstet af Vigre]».
///
/// Selve sætningen skrives derimod ud af Voxtral, som er bygget til det. Det
/// er dér, indledningen skal læses — ikke i vågeordsmotoren.
///
/// FEJLER DEN, FEJLER DEN DEN RIGTIGE VEJ. Genkendes ingen indledning, er det
/// en almindelig diktering, og teksten lander, hvor markøren står. Man mister
/// ikke det, man sagde; det havner bare et andet sted.
/// </summary>
public static class Hensigtstolk
{
    /// <summary>
    /// Indledningerne. Længste først — «opret en opgave» skal slå «opgave».
    /// </summary>
    /// <remarks>
    /// De er skrevet, som man SIGER dem, ikke som en kommandoliste. «Husk at
    /// ringe til Anders» er en opgave, også selv om ordet «opgave» ikke
    /// falder.
    /// </remarks>
    private static readonly (string Udtryk, Hensigt Hvad)[] Indledninger =
    {
        // ---------------------------------------------------------- opgave
        ("opret en opgave", Hensigt.Opgave),
        ("lav en opgave", Hensigt.Opgave),
        ("tilføj en opgave", Hensigt.Opgave),
        ("ny opgave", Hensigt.Opgave),
        ("mind mig om at", Hensigt.Opgave),
        ("mind mig om", Hensigt.Opgave),
        ("husk at", Hensigt.Opgave),
        ("opgave", Hensigt.Opgave),

        // ----------------------------------------------------------- aftale
        ("opret en aftale", Hensigt.Aftale),
        ("lav en aftale", Hensigt.Aftale),
        ("book et møde", Hensigt.Aftale),
        ("book møde", Hensigt.Aftale),
        ("ny aftale", Hensigt.Aftale),
        ("sæt et møde", Hensigt.Aftale),
        ("aftale", Hensigt.Aftale),

        // ------------------------------------------------------------- note
        ("opret en note", Hensigt.Note),
        ("skriv en note", Hensigt.Note),
        ("lav en note", Hensigt.Note),
        ("gem som note", Hensigt.Note),
        ("ny note", Hensigt.Note),
        ("notér", Hensigt.Note),
        ("noter", Hensigt.Note),
        ("note", Hensigt.Note),

        // ---------------------------------------------------------- søgning
        ("søg efter", Hensigt.Soegning),
        ("søg på", Hensigt.Soegning),
        ("find noget om", Hensigt.Soegning),
        ("find", Hensigt.Soegning),
        ("søg", Hensigt.Soegning),
    };

    /// <summary>
    /// Skærer et vågeord af forrest.
    /// </summary>
    /// <remarks>
    /// DET ENDER I TEKSTEN, OG DET ER IKKE EN FEJL I OPTAGELSEN.
    ///
    /// Vågeordet høres af én motor, og optagelsen begynder først, når den
    /// har meldt det — et halvt sekund senere. Halen af «Hej Pia» er stadig i
    /// luften, og Voxtral skriver den pænt ud.
    ///
    /// Set 30-08-2026: «Hej Pia» landede i søgefeltet som tekst.
    /// </remarks>
    public static string UdenVaageord(string tekst, IEnumerable<string> vaageord)
    {
        var t = (tekst ?? "").TrimStart();

        foreach (var raa in vaageord.OrderByDescending(o => o.Length))
        {
            var v = (raa ?? "").Trim();
            if (v.Length == 0) continue;

            if (!t.StartsWith(v, StringComparison.OrdinalIgnoreCase)) continue;

            // Tegnsaetningen efter ordet skal med: «Hej Pia, opret ...»
            return t[v.Length..].TrimStart(' ', ',', '.', '!', '?', ':', ';', '-', '—');
        }

        return t;
    }

    /// <summary>
    /// Læser hensigten ud af de første ord.
    /// </summary>
    public static Hensigtsfund Tolk(string? sagt)
    {
        var t = (sagt ?? "").TrimStart();
        if (t.Length == 0) return new Hensigtsfund(Hensigt.Diktat, "");

        foreach (var (udtryk, hvad) in Indledninger.OrderByDescending(i => i.Udtryk.Length))
        {
            if (!t.StartsWith(udtryk, StringComparison.OrdinalIgnoreCase)) continue;

            var rest = t[udtryk.Length..];

            // «opgaver» maa ikke laeses som «opgave» + «r». Der skal staa et
            // skilletegn efter indledningen - eller ingenting.
            if (rest.Length > 0 && char.IsLetterOrDigit(rest[0])) continue;

            var tilbage = rest.TrimStart(' ', ',', '.', ':', ';', '-', '—');

            return new Hensigtsfund(hvad, Stort(tilbage));
        }

        return new Hensigtsfund(Hensigt.Diktat, t);
    }

    /// <summary>
    /// Stort begyndelsesbogstav, når indledningen er skåret væk.
    /// </summary>
    /// <remarks>
    /// «opret en note: husk at ringe» giver «husk at ringe» med lille h. Det
    /// er ikke en sætning, nogen ville skrive.
    /// </remarks>
    private static string Stort(string tekst) =>
        tekst.Length == 0 ? tekst : char.ToUpper(tekst[0]) + tekst[1..];
}
