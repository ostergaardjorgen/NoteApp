using System.Windows.Media.Animation;

namespace NoteApp.Desktop;

/// <summary>
/// Farten, tingene glider med. ÉT sted.
///
/// HVORFOR DEN IKKE ER TO TAL, DER TILFÆLDIGVIS ER ENS
///
/// Sidespalterne i Cockpittet og menuen i venstre side gør det samme: de
/// folder sig ind og ud. Bevæger de sig forskelligt, læses de som to
/// forskellige slags ting — og man mærker det, længe før man kan sige hvorfor.
///
/// Stod tallet to steder, ville de være ens den dag, de blev skrevet, og
/// forskellige den dag, den ene blev justeret. Her er det ét tal, og så kan
/// de ikke komme fra hinanden.
/// </summary>
public static class Glid
{
    /// <summary>
    /// Hvor lang tid en foldning tager.
    /// </summary>
    /// <remarks>
    /// Fire tiendedele af et sekund. Kortere føles hastigt — en rude, der
    /// forsvinder med et snup, læses som en fejl. Længere, og man sidder og
    /// venter på at kunne se det, man bad om.
    /// </remarks>
    public static readonly TimeSpan Varighed = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Kurven: blød i begge ender.
    /// </summary>
    /// <remarks>
    /// EaseInOut, fordi bevægelsen både begynder og slutter af sig selv. En
    /// bevægelse, der starter brat, ligner et ryk; en, der stopper brat,
    /// ligner noget, der ramte en væg.
    ///
    /// Der laves en ny hver gang. En delt kurve kan fryses af den første
    /// animation, der bruger den, og så kan den anden ikke ændre den.
    /// </remarks>
    public static IEasingFunction Kurve() => new CubicEase { EasingMode = EasingMode.EaseInOut };

    /// <summary>En animation, der glider til <paramref name="maal"/>.</summary>
    public static DoubleAnimation Til(double maal) => new()
    {
        To = maal,
        Duration = Varighed,
        EasingFunction = Kurve(),
    };
}
