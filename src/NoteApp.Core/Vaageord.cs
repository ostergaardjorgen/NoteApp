namespace NoteApp.Core;

/// <summary>Hvorfor der lyttes — eller ikke gør.</summary>
public enum Lyttesvar
{
    /// <summary>Der lyttes.</summary>
    Lytter,

    /// <summary>Vågeordet er slået fra.</summary>
    Slukket,

    /// <summary>Der optages allerede. Vågeordet har intet at starte.</summary>
    Optager,

    /// <summary>Maskinen er låst. Ingen sidder her.</summary>
    Laast,

    /// <summary>Uden for vinduet omkring en aftale.</summary>
    UdenforVindue,

    /// <summary>Motoren mangler. Der er ikke noget at lytte med.</summary>
    IngenMotor,
}

/// <summary>
/// Afgør, hvornår mikrofonen skal være åben for vågeordet.
///
/// DET ER HER, LØSNINGEN BLIVER BILLIG — ikke i valget af motor.
///
/// En mikrofon, der er åben døgnet rundt, er problemet: den koster strøm, den
/// koster tillid, og den er svær at forklare en kunde. Men vågeordet behøver
/// ikke være åbent døgnet rundt. Det skal være åbent, når det er brugbart.
///
/// Appen ved allerede, hvornår det er: den har kalenderen. Er vinduet ti
/// minutter før og efter en aftale, er mikrofonen åben måske en time om dagen
/// frem for fireogtyve — og det er den samme funktion.
///
/// HVORFOR DEN LIGGER I CORE
///
/// «Er maskinen låst» og «kører der en optagelse» kræver Windows og appen.
/// Svaret på, hvad de så BETYDER, gør ikke — og det er dét, der skal kunne
/// prøves af: et vindue, der aldrig lukker, eller et, der aldrig åbner.
/// </summary>
public static class Vaageord
{
    /// <summary>Standardordet. Kan skiftes af brugeren.</summary>
    public const string Standardord = "hey pia";

    /// <summary>Hvor længe før en aftale der lyttes.</summary>
    public const int StandardFoerMinutter = 10;

    /// <summary>Hvor længe efter en aftales start der lyttes.</summary>
    /// <remarks>
    /// Efter er ikke det samme som før. Man siger «Hey Pia», når mødet er
    /// begyndt, og man opdager, at man glemte at trykke — sjældnere før.
    /// </remarks>
    public const int StandardEfterMinutter = 10;

    /// <summary>Mindste og største vindue, der kan vælges.</summary>
    public const int MindsteMinutter = 1;
    public const int StoersteMinutter = 120;

    /// <summary>Holder et valgt vindue inden for det mulige.</summary>
    public static int Minutter(int valgt, int standard) =>
        Math.Clamp(valgt <= 0 ? standard : valgt, MindsteMinutter, StoersteMinutter);

    /// <param name="til">Er vågeordet slået til?</param>
    /// <param name="kunVedMoeder">Skal der kun lyttes omkring aftaler?</param>
    /// <param name="motorFindes">Er der en motor at lytte med?</param>
    /// <param name="optager">Kører der en optagelse?</param>
    /// <param name="laast">Er maskinen låst?</param>
    /// <param name="nu">Tidspunktet.</param>
    /// <param name="naesteAftale">
    /// Nærmeste aftale — før eller efter <paramref name="nu"/>. Null, hvis der
    /// ingen er.
    /// </param>
    /// <param name="foerMinutter">Vinduet før aftalen.</param>
    /// <param name="efterMinutter">Vinduet efter aftalens start.</param>
    public static Lyttesvar Skal(
        bool til,
        bool kunVedMoeder,
        bool motorFindes,
        bool optager,
        bool laast,
        DateTimeOffset nu,
        DateTimeOffset? naesteAftale,
        int foerMinutter = StandardFoerMinutter,
        int efterMinutter = StandardEfterMinutter)
    {
        if (!til) return Lyttesvar.Slukket;
        if (!motorFindes) return Lyttesvar.IngenMotor;

        // OPTAGELSEN VINDER. Koerer der en, har vaageordet intet at starte -
        // og mikrofonen er i brug til noget vigtigere.
        if (optager) return Lyttesvar.Optager;

        // Er maskinen laast, sidder der ingen. At lytte ville vaere at lytte
        // til et tomt kontor.
        if (laast) return Lyttesvar.Laast;

        if (!kunVedMoeder) return Lyttesvar.Lytter;

        if (naesteAftale is not { } aftale) return Lyttesvar.UdenforVindue;

        var foer = Minutter(foerMinutter, StandardFoerMinutter);
        var efter = Minutter(efterMinutter, StandardEfterMinutter);

        var aabner = aftale.AddMinutes(-foer);
        var lukker = aftale.AddMinutes(efter);

        return nu >= aabner && nu <= lukker ? Lyttesvar.Lytter : Lyttesvar.UdenforVindue;
    }

    /// <summary>
    /// Renser vågeordet. Tom betyder «brug standarden».
    /// </summary>
    /// <remarks>
    /// ET VÅGEORD PÅ ÉT ORD ER FOR KORT. «Pia» alene ville udløse sig selv,
    /// hver gang nogen nævner et navn — og en optagelse, der starter af sig
    /// selv midt i et møde, er værre end ingen vågeord.
    /// </remarks>
    public static string Rens(string? ord)
    {
        var s = (ord ?? "").Trim().ToLowerInvariant();

        while (s.Contains("  ", StringComparison.Ordinal)) s = s.Replace("  ", " ");

        return s.Length >= 6 && s.Contains(' ', StringComparison.Ordinal) ? s : "";
    }
}
