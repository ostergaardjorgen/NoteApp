namespace NoteApp.Core;

/// <summary>Hvad der skal ske, næste gang tasten bliver spurgt.</summary>
public enum Holdsvar
{
    /// <summary>Ingenting endnu. Tasten er stadig nede, og grænsen er ikke nået.</summary>
    Vent,

    /// <summary>Det er et hold. Dikteringen skal begynde at lytte.</summary>
    Begynd,

    /// <summary>Tasten er sluppet. Der skal skrives ud.</summary>
    Slut,

    /// <summary>
    /// Loftet er nået, og tasten er STADIG nede.
    /// </summary>
    /// <remarks>
    /// DEN ER IKKE DET SAMME SOM <see cref="Slut"/>, og forskellen er hele
    /// pointen. Ved <see cref="Slut"/> har du selv sluppet, og du ved, at
    /// dikteringen er forbi. Her blev du afbrudt midt i en sætning.
    ///
    /// Det, der er optaget, skal stadig skrives ud — det er sagt, og det skal
    /// ikke smides væk. Men der SKAL siges til, ellers står man og taler
    /// videre til et program, der er holdt op med at lytte, og opdager det
    /// først, når teksten mangler.
    /// </remarks>
    Loftet,

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

    /// <summary>Loftet, når intet andet er valgt.</summary>
    public const int StandardLoftMinutter = 3;

    /// <summary>Korteste loft, der kan vælges.</summary>
    /// <remarks>
    /// Under et minut ville afbryde en almindelig besked midt i, og så ville
    /// loftet gøre mere skade end det, det beskytter mod.
    /// </remarks>
    public const int MindsteLoftMinutter = 1;

    /// <summary>Længste loft, der kan vælges.</summary>
    /// <remarks>
    /// Loftet findes, fordi en tast kan sidde fast. Sættes det for højt, er
    /// det der ikke længere: en halv time med lyd på vej til leverandøren er
    /// ikke noget, man opdager i tide.
    /// </remarks>
    public const int StoersteLoftMinutter = 20;

    /// <summary>
    /// Loftet som en tid, med tallet holdt inden for det, der giver mening.
    /// </summary>
    /// <remarks>
    /// En indstillingsfil kan indeholde hvad som helst — den er redigeret i
    /// hånden, eller den kommer fra en ældre udgave. Et nul ville afbryde hver
    /// diktering med det samme, og et negativt tal ville gøre det, før den
    /// begyndte.
    /// </remarks>
    public static TimeSpan LoftFra(int minutter) => TimeSpan.FromMinutes(
        Math.Clamp(minutter <= 0 ? StandardLoftMinutter : minutter,
                   MindsteLoftMinutter, StoersteLoftMinutter));

    /// <param name="gaaet">Tid siden tasten blev trykket.</param>
    /// <param name="nede">Er tasten stadig nede?</param>
    /// <param name="holderAllerede">Er <see cref="Holdsvar.Begynd"/> allerede givet?</param>
    /// <param name="loft">Længste hold. Se <see cref="LoftFra"/>.</param>
    public static Holdsvar Naeste(TimeSpan gaaet, bool nede, bool holderAllerede, TimeSpan loft)
    {
        if (nede)
        {
            // Stadig nede, og tiden er gaaet: afbrudt, ikke sluppet. Der skal
            // siges til, og det er en anden besked end den, man faar, naar man
            // selv slipper.
            if (gaaet >= loft) return holderAllerede ? Holdsvar.Loftet : Holdsvar.Tryk;

            return !holderAllerede && gaaet >= Graense
                ? Holdsvar.Begynd
                : Holdsvar.Vent;
        }

        // Sluppet. Var det aldrig blevet til et hold, er det et tryk — og saa
        // skal der ske det, der altid er sket.
        return holderAllerede ? Holdsvar.Slut : Holdsvar.Tryk;
    }
}
