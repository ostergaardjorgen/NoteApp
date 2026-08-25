namespace NoteApp.Core;

/// <summary>
/// Afgør, om der er et MØDE i gang — ikke bare om der er lyd.
///
/// Findes til den boks, der står klar to minutter før et booket møde og
/// starter optagelsen af sig selv. Hele værdien ligger i, at man ikke skal
/// trykke; hele risikoen ligger i, at den trykker forkert.
///
/// TO FEJL, DER IKKE ER LIGE SLEMME
///
/// Starter den for sent, mangler de første replikker — og det er netop dem,
/// hvor nogen siger, hvad mødet handler om. Starter den for tidligt, ligger
/// der tomgang i filen. Det første er et tab, det andet er spildplads.
///
/// HVORFOR LYD PÅ ÉT SPOR IKKE ER NOK
///
/// Fordi computeren larmer af andre grunde. En video, der spiller, en
/// notifikation, musik i baggrunden — alt sammen lyd på højttalersporet, og
/// intet af det er et møde. Den anden vej er lige så slem: en samtale i
/// rummet, en telefon i højttaler, nogen der tænker højt.
///
/// ET MØDE ER TO, DER SKIFTES. Derfor kræves lyd på BEGGE spor inden for ti
/// sekunder af hinanden. Modparten siger noget, man svarer — dét er en
/// samtale, og en video svarer ikke igen.
///
/// REKKEFØLGEN ER LIGEGYLDIG. Som regel er det dem, der taler først, mens man
/// selv sidder og venter; men lige så tit er det en selv, der siger «hej, kan
/// I høre mig?«. Begge dele er et møde, der er gået i gang.
///
/// ET FYSISK MØDE HAR KUN ÉT SPOR. Er der ingen højttalerenhed, er der ingen
/// modpart at vente på, og så starter mikrofonen alene. Ellers ville et møde
/// omkring et bord aldrig kunne komme i gang.
///
/// HVORFOR IKKE BARE «ER NIVEAUET OVER X»
///
/// Fordi et enkelt udsving ikke er tale. En dør, der smækker, et klik på
/// tastaturet, en stol, der skubbes — de rammer ét højt tal og er væk igen.
/// Men tale er heller ikke sammenhængende: der er pauser mellem ord. Et krav
/// om, at hver eneste aflæsning skal være over tærsklen, ville falde ved den
/// første pause. Derfor ses der på en ANDEL af de sidste sekunder.
/// </summary>
public sealed class Lydvagt
{
    /// <summary>
    /// Hvor højt der skal til, før en aflæsning tæller som lyd.
    ///
    /// Niveauet er 0–1. Stilhed i et rum ligger typisk under 0,01; tale rammer
    /// 0,05 og opefter. 0,02 ligger imellem — over rumstøj, under tale.
    ///
    /// DET ER MED VILJE LAVT. En for høj tærskel koster de første replikker,
    /// og en, der taler stille, skal også kunne starte mødet. Prisen for at
    /// være lidt for lav er tomgang i starten af filen, og den pris er lille.
    /// </summary>
    public const float Taerskel = 0.02f;

    /// <summary>
    /// Hvor langt tilbage der ses, når ét spor bedømmes.
    ///
    /// Tre sekunder rummer flere ord med pauser imellem, og det er kort nok
    /// til, at optagelsen er i gang, mens den første sætning stadig bliver
    /// sagt.
    /// </summary>
    public static readonly TimeSpan Vindue = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Hvor lang tid der må gå, fra det ene spor har lyd, til det andet svarer.
    ///
    /// Ti sekunder. Langt nok til en almindelig åbning — nogen siger goddag,
    /// man finder mikrofonen frem, man svarer. Kort nok til, at en video og en
    /// tilfældig bemærkning i rummet ikke bliver læst som en samtale, bare
    /// fordi de tilfældigvis skete i samme minut.
    /// </summary>
    public static readonly TimeSpan Svartid = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Hvor stor en del af vinduet der skal have lyd.
    ///
    /// En tredjedel. Tale med almindelige pauser ligger et godt stykke over;
    /// et enkelt smæld ligger langt under, uanset hvor højt det var.
    /// </summary>
    public const double Andel = 0.34;

    /// <summary>
    /// Så mange aflæsninger skal der til, før et spor overhovedet bedømmes.
    ///
    /// Uden den ville den allerførste aflæsning være 100 % af vinduet, og en
    /// enkelt lyd i det sekund, boksen åbnede, ville tælle som tale.
    /// </summary>
    public const int MindstAflaesninger = 6;

    /// <summary>Ét spor for sig: har DET her spor lyd lige nu?</summary>
    private sealed class Spor
    {
        private readonly List<(DateTimeOffset Tid, bool Lyd)> _maalt = new();

        public bool HarLyd { get; private set; }

        public double Udslag => _maalt.Count == 0
            ? 0
            : (double)_maalt.Count(m => m.Lyd) / _maalt.Count;

        public void Meld(float niveau, DateTimeOffset nu)
        {
            _maalt.Add((nu, niveau >= Taerskel));

            // Alt uden for vinduet er uinteressant. Fjernes det ikke, ville en
            // boks, der har staaet klar i to minutter, samle hundredvis af
            // aflaesninger, og saa kunne stilhed aldrig traekke andelen ned
            // igen.
            var graense = nu - Vindue;
            _maalt.RemoveAll(m => m.Tid < graense);

            HarLyd = _maalt.Count >= MindstAflaesninger && Udslag >= Andel;
        }

        public void Nulstil()
        {
            _maalt.Clear();
            HarLyd = false;
        }
    }

    private readonly Spor _mikrofon = new();
    private readonly Spor _online = new();

    private DateTimeOffset? _mikTalte;
    private DateTimeOffset? _onlineTalte;

    /// <summary>Er der overhovedet et højttalerspor? Uden er mødet fysisk.</summary>
    private readonly bool _harOnline;

    /// <param name="harOnlinespor">
    /// Falsk ved et fysisk møde uden højttalerenhed. Så er der ingen modpart
    /// at vente på, og mikrofonen starter alene.
    /// </param>
    public Lydvagt(bool harOnlinespor = true) => _harOnline = harOnlinespor;

    /// <summary>
    /// Meld niveauet på begge spor. Sandt betyder: sæt optagelsen i gang.
    /// </summary>
    public bool Meld(float mikrofon, float online, DateTimeOffset nu)
    {
        _mikrofon.Meld(mikrofon, nu);
        if (_harOnline) _online.Meld(online, nu);

        // Tidspunktet gemmes, FØRSTE gang sporet har lyd, og bliver staaende,
        // saa laenge det har det. Ellers ville en lang replik fra modparten
        // hele tiden rykke sit eget tidspunkt frem, og de ti sekunder ville
        // aldrig loebe ud.
        if (_mikrofon.HarLyd) _mikTalte ??= nu;
        else _mikTalte = null;

        if (_online.HarLyd) _onlineTalte ??= nu;
        else _onlineTalte = null;

        // FYSISK MØDE: kun ét spor at gaa efter.
        if (!_harOnline) return _mikrofon.HarLyd;

        if (_mikTalte is not { } m || _onlineTalte is not { } o) return false;

        // Raekkefoelgen er ligegyldig - det er afstanden, der siger, om det
        // ene var et svar paa det andet.
        var imellem = m > o ? m - o : o - m;
        return imellem <= Svartid;
    }

    /// <summary>Glem det målte. Bruges, når boksen stilles klar igen.</summary>
    public void Nulstil()
    {
        _mikrofon.Nulstil();
        _online.Nulstil();
        _mikTalte = null;
        _onlineTalte = null;
    }

    /// <summary>
    /// Hvor meget der er lyd på mikrofonen lige nu, 0–1.
    ///
    /// Til at vise noget, mens man venter. En boks, der siger «klar» og ikke
    /// rører sig, kan ikke skelnes fra en, der er gået i stå.
    /// </summary>
    public double UdslagMikrofon => _mikrofon.Udslag;

    /// <summary>Hvor meget der er lyd på højttalersporet lige nu, 0–1.</summary>
    public double UdslagOnline => _online.Udslag;

    /// <summary>Har modparten sagt noget, som der endnu ikke er svaret på?</summary>
    public bool VenterPaaSvar => _harOnline && _onlineTalte is not null && _mikTalte is null;
}
