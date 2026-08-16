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
    /// Hændelser, der er værd at give besked om. Se klassens forklaring —
    /// resten er ting, brugeren selv stod og gjorde.
    /// </summary>
    private static bool Vaerd(Haendelse h) =>
        h.Udfald is Udfald.Fejlet or Udfald.SeEfter ||
        h.Slags is HaendelseType.Transskription or HaendelseType.Dokument or HaendelseType.Backup;

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
