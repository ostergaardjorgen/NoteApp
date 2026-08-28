namespace NoteApp.Core;

/// <summary>Hvad der skal ske, næste gang tasten bliver spurgt.</summary>
public enum Holdsvar
{
    /// <summary>Ingenting endnu. Tasten er stadig nede, og grænsen er ikke nået.</summary>
    Vent,

    /// <summary>Det er et hold. Dikteringen skal begynde at lytte.</summary>
    Begynd,

    /// <summary>Holdet er slut. Der skal skrives ud.</summary>
    Slut,

    /// <summary>Det var et almindeligt tryk. Der skal startes en optagelse.</summary>
    Tryk,
}

/// <summary>
/// Afgør om et tastetryk blev til et tryk eller et hold.
///
/// HVORFOR DEN LIGGER I CORE OG IKKE VED SIDEN AF WINDOWS-KALDET
///
/// Selve spørgsmålet «er tasten stadig nede?» kræver Windows. Svaret på, hvad
/// det så BETYDER, gør ikke — og det er dét, der kan gå galt: en grænse, der
/// er sat forkert, en tilstand, der ikke bliver nulstillet, et hold, der
/// aldrig slutter.
///
/// Her kan det prøves af uden et tastatur, uden et vindue og uden at vente
/// 350 millisekunder på hver prøve.
/// </summary>
public static class Holdvurdering
{
    /// <summary>
    /// Hvor længe tasten skal holdes, før det tæller som et hold.
    /// </summary>
    /// <remarks>
    /// 350 ms er valgt, fordi et bevidst tryk ligger under 200 ms, og et hold
    /// er noget, man gør med vilje. Sættes den lavere, bliver et tungt tryk
    /// til en diktering; sættes den højere, skal man vente for længe, før
    /// dikteringen begynder at lytte.
    /// </remarks>
    public static readonly TimeSpan Graense = TimeSpan.FromMilliseconds(350);

    /// <summary>
    /// Længste hold. Derefter slippes der af sig selv.
    /// </summary>
    /// <remarks>
    /// EN TAST KAN SIDDE FAST. Sker det — fysisk, eller fordi et andet program
    /// spiser slippet — ville appen ellers optage og sende, til nogen opdagede
    /// det. Det koster penge hos leverandøren og er ikke til at se på skærmen.
    /// </remarks>
    public static readonly TimeSpan Loft = TimeSpan.FromMinutes(3);

    /// <param name="gaaet">Tid siden tasten blev trykket.</param>
    /// <param name="nede">Er tasten stadig nede?</param>
    /// <param name="holderAllerede">Er <see cref="Holdsvar.Begynd"/> allerede givet?</param>
    public static Holdsvar Naeste(TimeSpan gaaet, bool nede, bool holderAllerede)
    {
        if (nede && gaaet < Loft)
        {
            return !holderAllerede && gaaet >= Graense
                ? Holdsvar.Begynd
                : Holdsvar.Vent;
        }

        // Sluppet — eller loftet naaet. Var det aldrig blevet til et hold, er
        // det et tryk, og saa skal der ske det, der altid er sket.
        return holderAllerede ? Holdsvar.Slut : Holdsvar.Tryk;
    }
}
