namespace NoteApp.Core;

/// <summary>
/// De programmer, hvor lyden er et TELEFONOPKALD og ikke et møde.
/// </summary>
/// <remarks>
/// ============ HVORFOR DET SKILLES AD ============
///
/// Et opkald og et møde optages ens — din mikrofon og det, der kommer ud af
/// højttaleren. Men de HØRER ikke sammen bagefter. Et opkald er kort, det
/// kommer tit, og man leder efter det på en anden måde: «hvad sagde han i
/// telefonen i tirsdags». Ligger de blandt møderne, drukner de.
///
/// Derfor får de deres egen folder, og appen sætter den selv. En folder, man
/// skal huske at trække tingene ned i, bliver tom.
///
/// ============ HVORFOR DET ER EN LISTE HER, OG HVORFOR DET ER I ORDEN ============
///
/// Andre steder i koden står der udtrykkeligt, at en liste over navne bliver
/// forkert i stilhed — se <see cref="Mikrofonvagt.ErWindowsselv"/>, hvor der
/// derfor sammenlignes med en udgiver, og <c>ErEgetProgram</c>, hvor der
/// sammenlignes med en mappe.
///
/// Forskellen er, hvad en fejl KOSTER. Dér afgør listen, om appen tier eller
/// spørger om noget forkert. Her afgør den kun, hvilken folder optagelsen
/// lander i — og den kan trækkes over i en anden med musen. Et program, der
/// mangler på listen, giver en optagelse blandt møderne. Det er en irritation,
/// ikke et tab.
///
/// Der findes ingen «det her er et telefonopkald»-oplysning i Windows. Der er
/// ikke noget at sammenligne med.
///
/// ============ TELEFONLINK OG IKKE TEAMS ============
///
/// Et Teams-møde er et møde. Telefonlink er telefonen — den vej, et rigtigt
/// opkald til dit nummer kommer ind på maskinen. Zoom og Meet hører samme
/// sted som Teams: blandt møderne.
/// </remarks>
public static class Opkaldsprogrammer
{
    /// <summary>Pakkenavnene, som de står i Windows' eget register.</summary>
    /// <remarks>
    /// Telefonlink har heddet «Din telefon» og «Phone Link»; pakkenavnet har
    /// hele vejen været <c>Microsoft.YourPhone</c>. Det er derfor DET, der
    /// står her, og ikke noget, brugeren ser.
    /// </remarks>
    public static readonly IReadOnlyList<string> Pakker = new[]
    {
        "Microsoft.YourPhone",
    };

    /// <summary>Er det her program en telefon?</summary>
    /// <remarks>
    /// Der sammenlignes på pakkefamiliens NAVNEDEL — det, der står før
    /// understregen. Udgiver-id'et efter den kan skifte, hvis Microsoft
    /// nogensinde udgiver appen under en anden konto, og et opkald ville så
    /// stille og roligt begynde at lande blandt møderne.
    /// </remarks>
    public static bool Er(string? noegle)
    {
        if (string.IsNullOrWhiteSpace(noegle)) return false;

        var navn = noegle.Split('_')[0];

        return Pakker.Any(p => navn.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Er der et telefonopkald i gang lige nu?
    /// </summary>
    /// <remarks>
    /// ============ DER SPØRGES, DER HUSKES IKKE ============
    ///
    /// Alternativet var at lade mødevagten sætte et flag, når den starter en
    /// optagelse fra Telefonlink. Det ville kræve, at flaget rejste gennem
    /// tre lag og overlevede en asynkron opstart — og det ville kun virke for
    /// de optagelser, VAGTEN startede.
    ///
    /// Her spørges der i stedet på det ene tidspunkt, hvor svaret bruges: da
    /// optagelsen blev oprettet. Så virker det også, når man selv trykker på
    /// genvejstasten midt i en samtale, og det er præcis lige så rigtigt.
    ///
    /// ============ TELEFONENS LYDENHED FØRST ============
    ///
    /// Mikrofonlisten alene var ikke nok: målt 10-09-2026 stod Telefonlink
    /// der aldrig, heller ikke midt i et opkald. Se <see cref="Telefonlyd"/>.
    /// Listen bliver stående som anden vej ind, hvis en senere Telefonlink
    /// begynder at melde sig dér.
    /// </remarks>
    public static bool IGang()
    {
        try { return Telefonlyd.IGang() || Mikrofonvagt.IBrug().Any(b => Er(b.Noegle)); }
        catch (Exception) { return false; }
    }
}
