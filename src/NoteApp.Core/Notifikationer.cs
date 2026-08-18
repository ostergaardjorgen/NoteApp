namespace NoteApp.Core;

/// <summary>
/// Hvad der er sket, siden man sidst kiggede.
///
/// HVORFOR DEN LÆSER HISTORIKKEN FREM FOR AT HAVE SIN EGEN LISTE
///
/// Alt, appen gør, står allerede i historikken. En liste ved siden af ville
/// kunne komme ud af trit med den — og så viser klokken noget andet, end der
/// faktisk skete. Her er der én kilde, og «ulæst» er ikke andet end et
/// tidsstempel for, hvornår man sidst åbnede klokken.
///
/// HVORFOR IKKE ALT GIVER EN BESKED
///
/// En rettelse, man selv lige har trykket på, skal ikke give en notifikation —
/// man stod og så på det. Beskeder er til det, der sker, MENS man laver noget
/// andet: en transskription eller et referat, der bliver færdigt, en
/// sikkerhedskopi, eller noget der gik galt.
///
/// Et referat af et langt møde tager 40 minutter. Uden en besked skal man selv
/// huske at kigge efter — og så opdager man det dagen efter.
/// </summary>
public static class Notifikationer
{
    /// <summary>Rejses, når der er kommet noget nyt. UI'et kan lytte og opdatere klokken.</summary>
    public static event Action? Nyt;

    /// <summary>Sig at der er sket noget. Kaldes efter at hændelsen er skrevet i historikken.</summary>
    public static void Meld() => Nyt?.Invoke();

    /// <summary>
    /// Hændelser, der er værd at give besked om.
    ///
    /// TO TING, MAN GÅR VÆK FRA: en optagelse, der er skrevet ud, og et
    /// dokument, der er lavet. Det er dem, man sætter i gang og forlader.
    ///
    /// Og ÉN ting, der mangler: et trin i opsætningen, der ikke er gjort. Den
    /// bryder reglen med vilje. En ny installation kan optage og skrive ud,
    /// men ikke lave dokumenter, og uden en besked opdager man det først den
    /// dag, man står og skal bruge et referat. Den skrives én gang.
    ///
    /// Her stod også sikkerhedskopier og ALT, der fejlede. Klokken kom til at
    /// vise seks beskeder om sætninger, der var læst op igen — noget brugeren
    /// stod og så på i et vindue, han selv havde åbnet. En klokke, der ringer
    /// ved ting, man allerede ved, bliver en klokke, man holder op med at
    /// åbne, og så virker den heller ikke til det, den var til.
    ///
    /// Fejl på de to slags kommer stadig med: de har typen med sig, og en
    /// transskription, der gik galt, er lige så meget værd at vide som en, der
    /// lykkedes. Fejl på ALT ANDET gør ikke — de står i historikken.
    /// </summary>
    private static bool Vaerd(Haendelse h) =>
        h.Slags is HaendelseType.Transskription or HaendelseType.Dokument
                or HaendelseType.Opsaetning
        && !ErGammelTraening(h);

    /// <summary>
    /// Træningskørsler fra før <see cref="HaendelseType.Traening"/> fandtes.
    ///
    /// De ligger i historikken som «Transskription», fordi der ikke var en
    /// bedre type dengang, og de ville blive ved med at ringe med klokken,
    /// indtil de var rullet ud af de seneste 300 hændelser. Genkendes på
    /// teksten — grimt, men det er en overgang, ikke en regel.
    /// </summary>
    private static bool ErGammelTraening(Haendelse h) =>
        h.Hvad.StartsWith("Sætning ", StringComparison.Ordinal) &&
        h.Hvad.Contains("læst op igen", StringComparison.Ordinal);

    /// <summary>
    /// Skriver beskeden om, at opsætningen mangler et trin — hvis den mangler,
    /// og hvis den ikke allerede er skrevet.
    ///
    /// Kaldes ved opstart. Der ledes i historikken frem for at sætte et flag i
    /// indstillingerne: historikken ER kilden, og et flag ved siden af kunne
    /// komme ud af trit med den. Sletter man historikken, kommer beskeden
    /// igen, og det er også det rigtige — så er der ingen, der har set den.
    /// </summary>
    public static void MeldManglendeOpsaetning(bool noegleMangler)
    {
        if (!noegleMangler) return;

        var alleredeSkrevet = Historik.Laes(300)
            .Any(h => h.Slags == HaendelseType.Opsaetning);

        if (alleredeSkrevet) return;

        Historik.Skriv(HaendelseType.Opsaetning,
            "Opsætningen mangler et trin",
            "Dokumenter laves af en sprogmodel i Europa, og den er ikke sat op endnu. " +
            "Optagelse og udskrift virker uændret. Sæt den op under «AI-modeller».",
            Udfald.SeEfter);

        Meld();
    }

    /// <summary>De nyeste beskeder, uanset om de er læst.</summary>
    public static IReadOnlyList<Haendelse> Seneste(int maks = 30) =>
        Historik.Laes(300).Where(Vaerd).Take(maks).ToList();

    /// <summary>Hvor mange der er kommet, siden klokken sidst blev åbnet.</summary>
    public static int Ulaeste()
    {
        var sidst = AppSettings.Current.NotifikationerSetTil;
        return Historik.Laes(300).Count(h => Vaerd(h) && h.Tid > sidst);
    }

    /// <summary>Er en hændelse kommet, siden klokken sidst blev åbnet?</summary>
    public static bool ErNy(Haendelse h) => h.Tid > AppSettings.Current.NotifikationerSetTil;

    /// <summary>
    /// Markerer alt som set. Kaldes, når klokken åbnes — ikke når appen
    /// starter: en besked, man aldrig fik set, skal ikke forsvinde, fordi man
    /// genstartede.
    /// </summary>
    public static void MarkerSet()
    {
        AppSettings.Current.NotifikationerSetTil = DateTimeOffset.Now;
        AppSettings.Current.Save();
        Nyt?.Invoke();
    }
}
