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
    /// Hvor tit uret slår.
    /// </summary>
    /// <remarks>
    /// ============ URET SLÅR TIT, ARKIVET KØRER SJÆLDENT ============
    ///
    /// Et slag koster ét opslag på klokken. Selve kørslen spørger filsystemet
    /// om størrelse og dato på hver eneste fil i datamappen — 352 den
    /// 07-09-2026 — og de spørgsmål går over SMB til en NAS. Det er DEN, der
    /// skal være sjælden, og hvor sjælden bestemmer brugeren:
    /// <see cref="Arkivplan"/>.
    ///
    /// Uret skal slå tit for at kunne ramme et klokkeslæt. Slog det hvert
    /// kvarter, ville «kl. 8» blive til «engang mellem 8 og 8.15», og en
    /// maskine, der tændes 8.05, ville vente til 8.20.
    /// </remarks>
    public static readonly TimeSpan Slag = TimeSpan.FromMinutes(1);

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

    /// <summary>Hvad der sidst kom hjem fra de andre.</summary>
    public static (DateTimeOffset Naar, Arkivtal Tal)? Hentet { get; private set; }

    /// <summary>
    /// Henter de andres nye ting hjem — men ikke lyden.
    /// </summary>
    /// <remarks>
    /// ============ KUN DET SKREVNE ============
    ///
    /// Udskrifter, referater, noter, projekter og skabeloner: 25 MB mod lydens
    /// 1,8 GB. Det er dem, man leder efter dagen efter, og det er dem, der
    /// gør, at et møde optaget på den bærbare kan læses på den stationære.
    ///
    /// Hentedes lyden ogsaa, ville hver optagelse ligge TRE steder — paa
    /// begge maskiner og paa drevet — uden at nogen havde bedt om det. Skal
    /// den hjem, staar knappen paa delingsfanen.
    ///
    /// DER OVERSKRIVES ALDRIG NOGET. Se Arkiv.Hjemplan: findes filen hjemme i
    /// forvejen, roeres den ikke. Derfor er det her ufarligt at goere af sig
    /// selv - i modsaetning til en spejling.
    /// </remarks>
    private static Arkivtal Hentnye(CancellationToken stop)
    {
        if (!AppSettings.Current.HentAutomatisk) return Arkivtal.Intet;

        var alt = Arkivtal.Intet;

        foreach (var andres in Arkiv.Andres())
        {
            if (stop.IsCancellationRequested) break;

            try
            {
                var kom = Arkiv.Hent(andres.Id, medLyd: false, f => Melder?.Invoke(f), stop);

                alt = new Arkivtal(alt.Filer + kom.Filer, alt.Byte + kom.Byte,
                                   alt.Lydfiler + kom.Lydfiler, alt.Lydbyte + kom.Lydbyte);
            }
            catch (Exception)
            {
                // Naeste maskine. Og naeste gang.
            }
        }

        return alt;
    }

    /// <summary>Kører der en arkivering lige nu?</summary>
    public static bool Koerer => _travlt;

    public static void Start()
    {
        if (_ur is not null) return;

        _ur = new DispatcherTimer(DispatcherPriority.Background) { Interval = Foerste };

        _ur.Tick += (_, _) =>
        {
            _ur.Interval = Slag;
            Kig();
        };

        _ur.Start();
    }

    /// <summary>Arkiverer nu — knappen på delingsfanen.</summary>
    public static void Nu() => Kig(nu: true);

    /// <summary>Afbryder en kørsel, der er i gang. Det, der nåede op, bliver liggende.</summary>
    public static void Afbryd() => _stop?.Cancel();

    /// <summary>Er turen kommet efter den takt, brugeren har valgt?</summary>
    internal static bool Forfalden(DateTimeOffset? klokken = null)
    {
        var i = AppSettings.Current;

        return Arkivplan.Er_det_tid(i.ArkivTakt, i.ArkivKunITidsrum,
                                    i.ArkivFraKl, i.ArkivTilKl,
                                    i.ArkivSidst, klokken ?? DateTimeOffset.Now);
    }

    private static void Kig(bool nu = false)
    {
        if (_travlt) return;

        if (!Delt.Slaaet_til) return;

        if (!nu && !AppSettings.Current.ArkiverAutomatisk) return;

        // ============ TAKTEN ER BRUGERENS ============
        //
        // «Kun naar jeg trykker» skal betyde det. Og et tidsrum paa 8-17 er
        // ikke en anbefaling, appen maa se bort fra, naar den synes, der er
        // noget vigtigt.
        if (!nu && !Forfalden()) return;

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

                var hjem = Hentnye(stop);

                if (hjem.Noget) Hentet = (DateTimeOffset.Now, hjem);

                // ============ TIDSPUNKTET SKRIVES, OGSAA NAAR DER INTET VAR ============
                //
                // Ellers ville «to gange om dagen» blive til «hvert minut, saa
                // laenge der ikke er noget at lave»: uden et sidst-tidspunkt
                // er turen forfalden hele tiden, og saa gennemgaas 352 filer
                // over netvaerket hvert eneste minut.
                AppSettings.Current.ArkivSidst = DateTimeOffset.Now;
                AppSettings.Current.Save();
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
