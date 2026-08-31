using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// Boksen, der står klar før et booket møde og selv trykker på knappen.
///
/// HVORFOR DEN IKKE BARE STARTER OPTAGELSEN
///
/// Kalendervagten gjorde netop det: to minutter før mødet gik optagelsen i
/// gang. Det virker — og det giver to minutters tomgang forrest i hver eneste
/// fil, hver gang man møder op til tiden. Værre er de møder, der bliver
/// aflyst, eller hvor man ikke er ved maskinen: så ligger der en optagelse af
/// et tomt rum, som nogen skal opdage og rydde op i.
///
/// I STEDET STÅR DEN KLAR. Alt er allerede udfyldt fra aftalen — mappe,
/// mødetype, sprog — så der er ikke noget at svare på. Boksen viser, at den er
/// klar, og lyden trykker på knappen.
///
/// DEN OPTAGER IKKE, MENS DEN VENTER
///
/// Det er hele forskellen, og den skal kunne siges rent: måleren læser
/// niveauet fra lydenheden og gemmer et enkelt tal. Der skrives ikke til disk,
/// der bufres ikke, og der er intet at gemme, hvis mødet aldrig går i gang.
/// Se <see cref="WasapiLevelProbe"/> — den holder ét flydende tal og ikke
/// andet.
///
/// HVAD DER FÅR DEN I GANG
///
/// En SAMTALE, ikke en lyd. Se <see cref="Lydvagt"/>: der skal være lyd på
/// begge spor inden for ti sekunder af hinanden. En video, der spiller, og en
/// bemærkning i rummet er ikke et møde, og ingen af dem skal starte en
/// optagelse.
///
/// OPTAGELSE MÅ ALDRIG KUNNE BLOKERES. Den her står ved siden af knappen — den
/// erstatter den ikke. Man kan til enhver tid trykke selv, og gør man det, er
/// klargøringen ligegyldig.
/// </summary>
public sealed class Startklar : IDisposable
{
    /// <summary>
    /// Hvor længe der ventes, før der gives op.
    ///
    /// Ti minutter efter mødets starttidspunkt — samme grænse som
    /// Kalendervagtens. Kommer mødet ikke i gang der, kommer det ikke i gang,
    /// og så skal måleren ikke blive ved med at holde lydenhederne åbne.
    /// </summary>
    private static readonly TimeSpan Taalmodighed = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Hvor tit der aflæses.
    ///
    /// Fire gange i sekundet. Lydvagtens vindue er tre sekunder, så det giver
    /// tolv aflæsninger at dømme på — nok til at skelne tale fra et smæld, og
    /// billigt nok til at køre i baggrunden.
    /// </summary>
    private static readonly TimeSpan Aflaesning = TimeSpan.FromMilliseconds(250);

    private readonly DispatcherTimer _ur = new() { Interval = Aflaesning };
    private readonly Func<bool> _optagerAllerede;

    private WasapiLevelProbe? _mik;
    private WasapiLevelProbe? _online;
    private Lydvagt? _vagt;

    private Aftale? _aftale;

    /// <summary>Aftalen, der ventes på. Null når der ikke ventes.</summary>
    public Aftale? Aftalen => _aftale;

    /// <summary>Hvor meget der er lyd på de to spor, 0–1. Til at vise noget, mens der ventes.</summary>
    public double UdslagMikrofon => _vagt?.UdslagMikrofon ?? 0;
    public double UdslagOnline => _vagt?.UdslagOnline ?? 0;

    /// <summary>Modparten har sagt noget, og der er ikke svaret endnu.</summary>
    public bool VenterPaaSvar => _vagt?.VenterPaaSvar ?? false;

    /// <summary>Der er sket noget, der er værd at tegne om.</summary>
    public event Action? Aendret;

    /// <summary>Mødet er gået i gang. Nu skal der optages.</summary>
    public event Action<Aftale>? Gaaigang;

    public Startklar(Func<bool> optagerAllerede)
    {
        _optagerAllerede = optagerAllerede;
        _ur.Tick += (_, _) => Kig();
    }

    /// <summary>
    /// Stil boksen klar til en aftale.
    ///
    /// Kaldes af Kalendervagten to minutter før. Er der allerede stillet klar
    /// til den samme aftale, sker der ingenting — vagten kigger hvert halve
    /// minut, og den må ikke nulstille målingen, hver gang den gør det.
    /// </summary>
    public void Stil(Aftale aftale)
    {
        if (_aftale is not null && _aftale.Id == aftale.Id) return;

        StandNed();

        try
        {
            var mik = Mikrofon.Valgt();
            if (mik is null) return;

            // ET FYSISK MØDE HAR INGEN HØJTTALER. Så er der ingen modpart at
            // vente på, og mikrofonen starter alene. Se Lydvagt.
            var hoejttaler = AudioDevices.ResolveSpeaker(AppSettings.Current.SpeakerId, out _);

            _mik = WasapiLevelProbe.Start(mik.Id, loopback: false);
            _online = hoejttaler is null ? null : WasapiLevelProbe.Start(hoejttaler.Id, loopback: true);

            // Kunne mikrofonen ikke aabnes, er der intet at maale paa, og en
            // boks der siger «klar» uden at vaere det er vaerre end ingen boks.
            if (_mik.Error is not null) { StandNed(); return; }

            _vagt = new Lydvagt(harOnlinespor: _online is { Error: null });
            _aftale = aftale;
            _ur.Start();
            Aendret?.Invoke();
        }
        catch (Exception)
        {
            // Klargoering er en hjaelp. Kan den ikke lade sig goere, staar
            // knappen der stadig, og alt andet koerer videre.
            StandNed();
        }
    }

    /// <summary>Hold op med at vente. Lydenhederne slippes.</summary>
    public void StandNed()
    {
        _ur.Stop();

        _mik?.Dispose();
        _online?.Dispose();
        _mik = null;
        _online = null;
        _vagt = null;

        var havde = _aftale is not null;
        _aftale = null;

        if (havde) Aendret?.Invoke();
    }

    private void Kig()
    {
        try
        {
            if (_aftale is not { } a || _vagt is null) { StandNed(); return; }

            // TRYKKER MAN SELV, ER KLARGOERINGEN LIGEGYLDIG. Den skal saa ogsaa
            // slippe lydenhederne - to ting, der laeser fra den samme
            // mikrofon, er ikke noget, man skal proeve af paa et rigtigt moede.
            if (_optagerAllerede()) { StandNed(); return; }

            var nu = DateTimeOffset.Now;

            if (nu > a.Start + Taalmodighed) { StandNed(); return; }

            var gaar = _vagt.Meld(_mik?.ReadPeak() ?? 0f, _online?.ReadPeak() ?? 0f, nu);

            Aendret?.Invoke();

            if (!gaar) return;

            // SLIP FOERST, START BAGEFTER. Optagelsen aabner de samme
            // lydenheder, og maaleren skal vaere ude af vejen, inden den
            // proever.
            StandNed();
            Gaaigang?.Invoke(a);
        }
        catch (Exception)
        {
            StandNed();
        }
    }

    public void Dispose() => StandNed();
}
