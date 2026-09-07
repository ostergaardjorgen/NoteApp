using System.Windows.Threading;
using NoteApp.Core;
using NoteApp.Core.Deling;

namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Lægger den her maskines historik op i den fælles mappe.
/// </summary>
/// <remarks>
/// ============ HVORFOR DEN HAR SIT EGET UR ============
///
/// <see cref="Arbejdsvagt"/> ser efter arbejde og svar hvert minut, og den
/// holder sig selv optaget, mens den gør det. Lå arkiveringen dér, ville den
/// første kørsel — 1,8 GB over et netværk — spærre for journalen, for køen og
/// for de udskrifter, der skulle hjem, i en time.
///
/// De to ting har heller ikke det samme hastværk. Et spor, der venter i køen,
/// er nogen, der sidder og kigger. Et arkiv, der er et kvarter bagud, er der
/// ingen, der mærker.
///
/// ============ DEN VIGER FOR ALT ANDET ============
///
/// Der arkiveres ikke, mens der OPTAGES — se
/// <see cref="RecordingSession.NogenOptager"/> — og ikke, mens der kører noget
/// tungt, se <see cref="HeavyJobLock"/>. Begge dele læser og skriver på den
/// samme disk, og optagelsen er den ene ting i appen, der ikke kan tages om.
///
/// Arkiveringen kan altid tages om. Den venter et kvarter og prøver igen.
/// </remarks>
public static class Arkivvagt
{
    /// <summary>
    /// Hvor tit der ses efter noget at arkivere.
    /// </summary>
    /// <remarks>
    /// ET KVARTER, og det er valgt på arbejdet frem for på behovet: hver
    /// kørsel spørger filsystemet om størrelse og dato på hver eneste fil i
    /// datamappen — 352 filer den 07-09-2026 — og de spørgsmål går over SMB
    /// til en NAS. Hvert minut ville være 352 netværksopslag i minuttet for at
    /// opdage noget, der sker et par gange om dagen.
    /// </remarks>
    public static readonly TimeSpan Mellemrum = TimeSpan.FromMinutes(15);

    /// <summary>Første kørsel venter så længe efter opstart.</summary>
    /// <remarks>
    /// APPEN SKAL VÆRE FÆRDIG MED AT STARTE FØRST. Et vindue, der er to
    /// minutter om at komme frem, fordi arkivet blev gennemgået, er en app,
    /// der føles i stykker — også selvom den laver noget nyttigt imens.
    /// </remarks>
    public static readonly TimeSpan Foerste = TimeSpan.FromMinutes(2);

    private static DispatcherTimer? _ur;
    private static CancellationTokenSource? _stop;
    private static bool _travlt;

    /// <summary>Melder fremdrift til skærmen. Sat af delingsfanen.</summary>
    public static Action<Arkivfremdrift?>? Melder { get; set; }

    /// <summary>Hvad der sidst kom op — og hvornår. Til skærmen.</summary>
    public static (DateTimeOffset Naar, Arkivtal Tal)? Sidste { get; private set; }

    /// <summary>Kører der en arkivering lige nu?</summary>
    public static bool Koerer => _travlt;

    public static void Start()
    {
        if (_ur is not null) return;

        _ur = new DispatcherTimer(DispatcherPriority.Background) { Interval = Foerste };

        _ur.Tick += (_, _) =>
        {
            _ur.Interval = Mellemrum;
            Kig();
        };

        _ur.Start();
    }

    /// <summary>Arkiverer nu — knappen på delingsfanen.</summary>
    public static void Nu() => Kig(nu: true);

    /// <summary>Afbryder en kørsel, der er i gang. Det, der nåede op, bliver liggende.</summary>
    public static void Afbryd() => _stop?.Cancel();

    private static void Kig(bool nu = false)
    {
        if (_travlt) return;

        if (!Delt.Slaaet_til) return;

        if (!nu && !AppSettings.Current.ArkiverAutomatisk) return;

        // ============ DEN VIGER ============
        //
        // Ogsaa naar der trykkes paa knappen. Den, der trykker, kan ikke vide,
        // at der koerer en udskrift i baggrunden - men appen kan.
        if (RecordingSession.NogenOptager) return;

        if (HeavyJobLock.Current() is not null) return;

        var medLyd = AppSettings.Current.ArkiverLyd;

        _travlt = true;
        _stop = new CancellationTokenSource();

        var stop = _stop.Token;

        _ = Task.Run(() =>
        {
            try
            {
                var kom = Arkiv.Gem(medLyd, f => Melder?.Invoke(f), stop);

                if (kom.Noget) Sidste = (DateTimeOffset.Now, kom);
            }
            catch (Exception)
            {
                // Et drev, der ikke svarer lige nu. Naeste gang om et kvarter.
                // Det, der naaede op, ligger der stadig - se Arkiv.Gem.
            }
            finally
            {
                _travlt = false;
                Melder?.Invoke(null);
            }
        }, stop);
    }
}
