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

    /// <summary>Motoren mangler. Der er ikke noget at lytte med.</summary>
    IngenMotor,
}

/// <summary>
/// Afgør, hvornår mikrofonen skal være åben for vågeordet.
///
/// DEN LYTTER, NÅR DU ER VED MASKINEN. Ikke i et vindue omkring aftalerne —
/// man siger «Hej Pia», når man har brug for det, og det følger ikke mødernes
/// tidsplan. En funktion, der kun virker ti minutter om en aftale, virker
/// ikke; den virker en gang imellem, og det er værre.
///
/// TO GRUNDE TIL IKKE AT LYTTE, og de er begge klare:
///
///   LÅST SKÆRM   der sidder ingen. At lytte ville være at lytte til et tomt
///                kontor.
///   OPTAGELSE    mikrofonen er i brug til noget vigtigere, og vågeordet har
///                intet at starte. Det gælder også et onlinemøde: dér optages
///                der, og så skal der ikke lyttes efter kommandoer.
///
/// HVORFOR DEN LIGGER I CORE
///
/// «Er maskinen låst» og «kører der en optagelse» kræver Windows og appen.
/// Svaret på, hvad de så BETYDER, gør ikke — og det er dét, der skal kunne
/// prøves af.
/// </summary>
public static class Vaageord
{
    /// <summary>
    /// Ordene, appen lytter efter fra begyndelsen. Brugeren kan rette i listen.
    /// </summary>
    /// <remarks>
    /// TO STAVEMÅDER AF DET SAMME. «Hej» og «hey» lyder næsten ens, men en
    /// udskrift vælger én af dem — og hvilken, afhænger af, hvor hårdt man
    /// siger h'et. Lyttes der kun efter den ene, virker vågeordet hver anden
    /// gang, og det er værre end slet ikke at virke: så tror man, det er én
    /// selv, der siger det forkert.
    /// </remarks>
    public static readonly IReadOnlyList<string> Standardord = new[] { "hej pia", "hey pia" };

    /// <param name="til">Er vågeordet slået til?</param>
    /// <param name="motorFindes">Er der en motor at lytte med?</param>
    /// <param name="optager">Kører der en optagelse — også et onlinemøde?</param>
    /// <param name="laast">Er maskinen låst?</param>
    public static Lyttesvar Skal(bool til, bool motorFindes, bool optager, bool laast)
    {
        if (!til) return Lyttesvar.Slukket;
        if (!motorFindes) return Lyttesvar.IngenMotor;

        // OPTAGELSEN VINDER. Koerer der en, har vaageordet intet at starte -
        // og mikrofonen er i brug til noget vigtigere. Det gaelder ogsaa et
        // onlinemoede: dér optages der.
        if (optager) return Lyttesvar.Optager;

        // Er maskinen laast, sidder der ingen. At lytte ville vaere at lytte
        // til et tomt kontor.
        if (laast) return Lyttesvar.Laast;

        return Lyttesvar.Lytter;
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

    /// <summary>
    /// Blev et af vågeordene sagt? Giver det ord, der blev genkendt.
    /// </summary>
    /// <remarks>
    /// STRENGERE END ORDBOGEN, MILDERE END KOMMANDOERNE. Et vågeord, der
    /// udløses for let, starter en optagelse midt i et møde; et, der udløses
    /// for svært, får folk til at gentage sig selv, til de giver op.
    ///
    /// Ét tegn galt går an — «hey pia» mod «hey pja». To gør ikke: så er der
    /// for mange almindelige sætninger inden for rækkevidde.
    /// </remarks>
    public static string? Hoert(string? sagt, IEnumerable<string> ord)
    {
        var s = Kommandotolk.Rens(sagt);
        if (s.Length == 0) return null;

        foreach (var raa in ord)
        {
            var v = Rens(raa);
            if (v.Length == 0) continue;

            // Vaageordet staar FORREST. Bliver det sagt midt i en saetning,
            // var det ikke et kald - det var nogen, der talte om Pia.
            if (s.StartsWith(v, StringComparison.Ordinal)) return v;

            // Hele ytringen er vaageordet, med hoejst eet tegn galt.
            if (Math.Abs(s.Length - v.Length) <= 1 && Ordretter.Afstand(s, v, 1) <= 1) return v;
        }

        return null;
    }

    /// <summary>Det, der står tilbage, når vågeordet er skåret væk.</summary>
    public static string Efter(string? sagt, string vaageord)
    {
        var s = Kommandotolk.Rens(sagt);
        var v = Rens(vaageord);

        return v.Length > 0 && s.StartsWith(v, StringComparison.Ordinal)
            ? s[v.Length..].Trim()
            : "";
    }
}

/// <summary>
/// Afgør, hvornår en diktering, der ikke blev startet med en tast, skal slutte.
///
/// «HOLD NEDE» HAR INGEN TAST AT SLIPPE, NÅR DET VAR ET VÅGEORD. Så må
/// stilheden slippe den: taler man ikke længere, er man færdig.
///
/// TÅLMODIGHEDEN ER HELE SAGEN. For kort, og dikteringen klipper en tænkepause
/// over midt i en sætning. For lang, og man står og venter på, at den opdager,
/// man er færdig — og imens optages resten af rummet.
/// </summary>
public static class Stilhed
{
    /// <summary>Hvor længe der skal være stille, før dikteringen slutter.</summary>
    /// <remarks>
    /// Halvandet sekund. En tænkepause midt i en sætning er kortere; en pause
    /// efter en færdig sætning er længere.
    /// </remarks>
    public static readonly TimeSpan Taalmodighed = TimeSpan.FromMilliseconds(1500);

    /// <summary>Under dette niveau regnes der ikke for at blive talt.</summary>
    /// <remarks>
    /// Niveauet er 0-1 fra mikrofonen. 0,02 er valgt lavt: en stille stemme
    /// skal kunne holde dikteringen i gang, og et rum har altid en smule støj.
    /// </remarks>
    public const float Graense = 0.02f;

    /// <summary>Er der talt for kort til, at det var et diktat?</summary>
    /// <remarks>
    /// Uden den ville selve vågeordet kunne blive til et tomt diktat: man
    /// siger «Hej Pia», tier, og der sendes et klip uden indhold.
    /// </remarks>
    public static readonly TimeSpan MindsteTale = TimeSpan.FromMilliseconds(400);

    /// <param name="sidenTale">Tid siden mikrofonen sidst hørte noget.</param>
    /// <param name="haltTalt">Er der overhovedet blevet talt?</param>
    /// <param name="gaaet">Tid siden dikteringen begyndte.</param>
    /// <param name="loft">Længste diktering.</param>
    public static bool SkalSlutte(TimeSpan sidenTale, bool haltTalt, TimeSpan gaaet, TimeSpan loft)
    {
        if (gaaet >= loft) return true;

        // Er der ikke sagt noget endnu, venter vi. Man skal have lov at
        // traekke vejret, foer man begynder.
        if (!haltTalt) return gaaet >= loft;

        return sidenTale >= Taalmodighed;
    }
}
