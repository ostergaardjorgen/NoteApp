using System.IO;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop;

/// <summary>
/// Binder holdet på genvejstasten sammen med dikteringen.
///
/// HOLD → OPTAG → SKRIV UD → PUDS AF → IND HVOR DU VAR
///
/// Teksten lægges, hvor markøren står, i det program du var i gang med. Kan
/// den ikke det — du er skiftet væk, eller programmet tager ikke imod — ligger
/// den i udklipsholderen i stedet, og det bliver sagt.
///
/// FORGRUNDEN LÆSES VED STARTEN, IKKE VED SLUTNINGEN. Mellem de to ligger
/// udskriften på over et sekund, og teksten skal lande dér, hvor du talte —
/// ikke dér, hvor du nåede hen imens.
/// </summary>
public sealed class Dikteringsvagt : IDisposable
{
    private ShortClipRecorder? _optager;
    private string? _klip;
    private bool _arbejder;

    /// <summary>
    /// Det vindue, der var fremme, da holdet begyndte.
    /// </summary>
    /// <remarks>
    /// LÆST VED STARTEN, IKKE VED SLUTNINGEN. Mellem de to ligger udskriften,
    /// og den tager over et sekund. Blev forgrunden læst til sidst, ville
    /// teksten lande dér, hvor man var nået hen — ikke dér, hvor man talte.
    /// </remarks>
    private Forgrundsvindue _maal;

    /// <summary>Siger til undervejs, så bjælken kan vise, hvad der sker.</summary>
    public event Action<string>? Melder;

    /// <summary>
    /// Rejses med den færdige tekst, når en diktering er landet.
    /// </summary>
    /// <remarks>
    /// DER GEMMES IKKE HER. Teksten gives videre, og skærmen tilbyder at
    /// gemme den som note — men valget er brugerens. Gemte appen hver eneste
    /// diktering, ville den lave et arkiv over alt, hvad der var sagt i løbet
    /// af en dag, uden at nogen havde bedt om det.
    ///
    /// Prøverummet rejser den ikke: dér er teksten netop noget, man kaster
    /// væk og laver om.
    /// </remarks>
    public event Action<Core.Diktatudfald>? Faerdig;

    /// <summary>
    /// Sat, når dikteringen kom fra vågeordet. Får den færdige tekst og
    /// svarer, om den blev sendt et sted hen.
    /// </summary>
    /// <remarks>
    /// «HEJ PIA, OPRET EN OPGAVE …» ER ÉN HANDLING. Kom kaldet fra vågeordet,
    /// er der som regel sagt HVAD det skal bruges til — og så skal teksten
    /// ikke bare lande, hvor markøren tilfældigvis står.
    ///
    /// Svarer ruteren falsk, var der ingen hensigt at genkende, og så går
    /// teksten den almindelige vej. Det er den rigtige måde at fejle på: man
    /// får sin tekst, den havner bare et andet sted.
    /// </remarks>
    public Func<Core.Diktatudfald, bool>? Ruter { get; set; }

    /// <summary>Kom den igangværende diktering fra vågeordet?</summary>
    private bool _fraVaageord;

    /// <summary>Et prøverum, der har taget dikteringen til sig.</summary>
    /// <param name="Formaal">Typen, der prøves af — ikke den, programmet lægger op til.</param>
    /// <param name="Vis">Kaldes med den rå udskrift og den pudsede tekst.</param>
    public sealed record Proeverum(Dikteringsformaal Formaal, Action<string, string> Vis);

    /// <summary>
    /// Sat, mens prøverummet er fremme.
    /// </summary>
    /// <remarks>
    /// ER DEN SAT, LANDER TEKSTEN DÉR — og ingen andre steder. Der sættes
    /// ikke ind i noget program, og udklipsholderen røres ikke.
    ///
    /// Uden det ville en prøve i prøverummet skrive ind i det, man sidst
    /// havde fremme. Man prøver netop, fordi man IKKE er sikker på, hvad der
    /// kommer ud — og så skal det ikke lande i en mail til en kunde.
    /// </remarks>
    public static Proeverum? Proeve { get; set; }

    /// <summary>Sandt, mens der optages eller skrives ud.</summary>
    /// <remarks>
    /// Foroptageren taeller MED, naar den er ved at samle et diktat op.
    /// Uden det ville et nyt «Hej Pia» midt i en saetning starte en diktering
    /// oven i den, der allerede kører.
    /// </remarks>
    public bool Igang =>
        _optager is not null || _arbejder || Foroptagelse is { Beholder: true };

    /// <summary>
    /// Mikrofonens niveau lige nu, 0–1. Nul, når der ikke optages.
    /// </summary>
    /// <remarks>
    /// DEN FINDES, FOR AT MAN KAN SE, AT DER BLIVER OPTAGET.
    ///
    /// En besked om, at der optages, er en påstand. En kurve, der følger
    /// stemmen, er et bevis — og forskellen mærkes i det sekund, hvor man
    /// begynder at tale og ikke ved, om appen er med.
    ///
    /// De to veje ind måler på hver sin kilde: et hold på tasten optager
    /// direkte, et vågeord henter fra ringen. Udad til er det det samme
    /// spørgsmål, og det skal have det samme svar.
    /// </remarks>
    public float Niveau =>
        _optager?.Niveau
        ?? (Foroptagelse is { Beholder: true } f ? f.Niveau : 0f);

    /// <summary>
    /// Mikrofonen, der allerede er åben, mens der lyttes. Sat af skærmen.
    /// </summary>
    /// <remarks>
    /// DEN FJERNER VENTETIDEN. Uden den blev mikrofonen først åbnet, NÅR
    /// vågeordet var hørt — og så lå der to ventetider efter hinanden:
    /// motorens afgørelse (målt 60–555 ms) og åbningen af lydenheden. Man
    /// talte ud i ingenting, og de første ord blev klippet.
    ///
    /// Med den er lyden der allerede. Se <see cref="Foroptager"/>.
    /// </remarks>
    public Foroptager? Foroptagelse { get; set; }

    /// <summary>
    /// Vågeordet blev hørt. Gør det samme som et hold på genvejstasten — og
    /// slipper selv, når der bliver stille.
    /// </summary>
    /// <remarks>
    /// DER EMULERES IKKE ET TASTETRYK. Det ville være at sende Ctrl+komma ud i
    /// Windows og håbe, at appen fangede det igen — gennem den samme
    /// registrering, der kan være taget af et andet program. I stedet kaldes
    /// den samme kode, som tastetrykket kalder.
    ///
    /// STILHEDEN SLIPPER HOLDET. Der er ingen tast at give slip på, så
    /// mikrofonens eget niveau afgør det: <see cref="Stilhed"/>.
    /// </remarks>
    public void BegyndPaaVaageord()
    {
        // ============ ER LYDEN DER ALLEREDE? ============
        //
        // Er foroptageren i gang, er de sidste sekunder i hukommelsen, og
        // dikteringen begynder BAGUD i tiden - inklusive «Hej Pia» selv, som
        // skaeres vaek af teksten bagefter.
        if (Foroptagelse is { Koerer: true } forud && !Igang)
        {
            _klip = Path.Combine(Path.GetTempPath(),
                                 "heypia-diktat-" + Guid.NewGuid().ToString("N")[..8] + ".wav");

            _maal = Indsaetter.Laes();
            forud.Behold();

            _fraVaageord = true;

            // ============ IKKE DEN SAMME BESKED SOM VED TASTEN ============
            //
            // HER STOD «Lytter - tal, og slip TASTEN naar du er faerdig».
            // Teksten er skrevet til genvejstasten og blev genbrugt her, hvor
            // der ingen tast er: efter et vaageord slutter dikteringen af sig
            // selv, naar man holder en pause.
            //
            // Brugeren skrev det selv ind som en note 31-08-2026: «det virker
            // faktisk, som om den reagerer rigtigt, men man kan ikke se det
            // paa den groenne bar». Bjaelken beskrev en anden fremgangsmaade
            // end den, appen brugte - og saa kan man ikke laese, om den er i
            // gang, uanset hvor tydeligt der staar noget.
            Melder?.Invoke(Sprog.T("diktering.lytter_vaageord"));

            Stilhedsur(() => forud.Niveau);
            return;
        }

        Begynd();
        _fraVaageord = _optager is not null;
        if (_optager is null) return;

        Stilhedsur(() => _optager?.Niveau ?? 0);
    }

    /// <summary>
    /// Slipper holdet, når der bliver stille.
    /// </summary>
    /// <remarks>
    /// Der er ingen tast at give slip på, så mikrofonens eget niveau afgør
    /// det. Skilt ud, fordi de to veje ind — foroptageren og den almindelige
    /// optager — måler på hver sin kilde, men skal slippe ens.
    /// </remarks>
    private void Stilhedsur(Func<float> niveau)
    {
        var start = DateTime.UtcNow;
        var sidstHoert = DateTime.UtcNow;
        var harTalt = false;

        var loft = Holdvurdering.LoftFra(AppSettings.Current.DikteringLoftMinutter);

        _stilhedsur?.Stop();
        _stilhedsur = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };

        _stilhedsur.Tick += (_, _) =>
        {
            // Er der ingenting i gang laengere - hverken en optager eller
            // noget paa vej i hus fra ringen - er der heller ikke noget at
            // slippe.
            if (_optager is null && Foroptagelse is not { Beholder: true })
            {
                _stilhedsur?.Stop();
                _stilhedsur = null;
                return;
            }

            if (niveau() >= Stilhed.Graense)
            {
                sidstHoert = DateTime.UtcNow;
                if (DateTime.UtcNow - start >= Stilhed.MindsteTale) harTalt = true;
            }

            if (!Stilhed.SkalSlutte(DateTime.UtcNow - sidstHoert, harTalt,
                                    DateTime.UtcNow - start, loft)) return;

            _stilhedsur?.Stop();
            _stilhedsur = null;

            _ = SlutAsync(afbrudt: false);
        };

        _stilhedsur.Start();
    }

    private System.Windows.Threading.DispatcherTimer? _stilhedsur;

    /// <summary>
    /// Var det HeyPia selv, der var fremme, da holdet begyndte?
    /// </summary>
    /// <remarks>
    /// Prøverummet må kun tage dikteringen, når man faktisk står og kigger på
    /// det. Ellers ville en fane, man for en time siden klikkede væk fra,
    /// opsluge hver eneste diktering resten af dagen.
    ///
    /// Der ses på PROCESSEN og ikke på vinduet: appen har flere — hovedvindue,
    /// optagebånd, opstartsrude — og de er alle sammen os.
    /// </remarks>
    private static bool Vores(Forgrundsvindue maal) =>
        maal.Findes && maal.Proces.Equals(
            System.Diagnostics.Process.GetCurrentProcess().ProcessName,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Tasten er holdt nede længe nok. Begynd at lytte.</summary>
    public void Begynd()
    {
        // Et hold paa tasten er ALDRIG et vaageordskald. Blev flaget ikke
        // nulstillet her, ville naeste diktering efter et «Hej Pia» stadig
        // blive rutet - og teksten lande et andet sted end ved markoeren.
        _fraVaageord = false;

        // ============ ET DIKTAT MAA IKKE LIGGE OVEN I ET ANDET ============
        //
        // Slippet kan bliver spist af et andet program, og saa kommer der et
        // nyt hold, foer det foerste er afsluttet. Uden den her ville to
        // optagere skrive i den samme fil.
        if (Igang) return;

        var noegle = SkyNoegle.Hent();
        if (noegle is null)
        {
            Melder?.Invoke(Sprog.T("settingsview.diktering_kraever_noegle"));
            return;
        }

        // ============ ER DER OVERHOVEDET EN MIKROFON? ============
        //
        // DET SKAL SIGES NU, OG DET SKAL SIGES PRAECIST. Fejlede optagelsen
        // foer, stod der «det gik galt» og en teknisk besked fra lydlaget -
        // og der gik lang tid, foer nogen gaettede paa, at det var enheden.
        //
        // Set 30-08-2026: appen havde skiftet til Windows' standard, fordi
        // brugerens headset ikke var tilsluttet i det oejeblik, Lyd-fanen
        // blev aabnet. Standarden virkede ikke, og dikteringen fejlede med en
        // besked, der ikke naevnte mikrofonen med et ord.
        var enhed = AudioDevices.ResolveMicrophone(
            AppSettings.Current.MicrophoneId, out var reserve);

        if (enhed is null)
        {
            Melder?.Invoke(Sprog.T("diktering.ingen_mikrofon"));
            return;
        }

        try
        {
            _klip = Path.Combine(Path.GetTempPath(),
                                 "heypia-diktat-" + Guid.NewGuid().ToString("N")[..8] + ".wav");

            _maal = Indsaetter.Laes();

            _optager = new ShortClipRecorder(_klip);

            // Der startes paa den enhed, der FAKTISK blev fundet - ikke paa et
            // id, der maaske ikke findes mere.
            _optager.Start(enhed.Id);

            Melder?.Invoke(reserve
                ? Sprog.T("diktering.lytter_paa_reserve", enhed.FriendlyName)
                : Sprog.T("diktering.lytter"));
        }
        catch (Exception ex)
        {
            Ryd();

            // Navnet paa enheden med. «Mikrofonen svarede ikke» uden at sige
            // hvilken er ikke til at handle paa.
            Melder?.Invoke(Sprog.T("diktering.mikrofon_svarede_ikke",
                                   enhed.FriendlyName, ex.Message));
        }
    }

    /// <summary>
    /// Tasten er sluppet — eller loftet blev nået.
    /// </summary>
    /// <param name="afbrudt">
    /// Sandt, når appen stoppede af sig selv. Så er man midt i en sætning og
    /// skal have det at vide; ellers taler man videre til noget, der er holdt
    /// op med at lytte.
    /// </param>
    public async Task SlutAsync(bool afbrudt)
    {
        // FOROPTAGEREN HAR INGEN «_optager». Den holder mikrofonen aaben hele
        // tiden, og der er derfor ingen at stoppe - kun noget at gemme.
        var fraRing = Foroptagelse is { Beholder: true };

        if (_optager is null && !fraRing) return;

        var optager = _optager;
        var klip = _klip!;
        _optager = null;
        _arbejder = true;

        try
        {
            var sekunder = fraRing
                ? Foroptagelse!.Gem(klip)
                : optager!.Stop();

            optager?.Dispose();

            // Under et halvt sekund er et fejltryk, ikke et diktat. At sende
            // det ville koste et kald og give en tom streng tilbage.
            if (sekunder < 0.5)
            {
                Melder?.Invoke(Sprog.T("diktering.for_kort"));
                return;
            }

            Melder?.Invoke(Sprog.T("diktering.skriver_ud"));

            var noegle = SkyNoegle.Hent();
            if (noegle is null)
            {
                Melder?.Invoke(Sprog.T("settingsview.diktering_kraever_noegle"));
                return;
            }

            var v = AppSettings.Current;
            var klient = new Dikteringsklient(noegle);

            var ordbog = v.DikteringFagord ? Ordbibliotek.Laes() : new List<string>();

            // ============ SPROGET SKAL MED ============
            //
            // Uden det gaetter modellen, og paa et kort klip er der naesten
            // intet at gaette ud fra. Maalt 30-08-2026: «hallo, hallo, hallo»
            // paa dansk kom tilbage som «Alors, alors, alors ?» - fransk.
            // Teksten var ikke forkert hoert; den var hoert paa det forkerte
            // sprog, og saa er hvert eneste ord forkert.
            //
            // MitSprog er dét, brugeren TALER. Null betyder dansk; «auto»
            // lader modellen gaette som foer.
            var raa = await klient.SkrivUdAsync(
                klip,
                ordbog.Count > 0 ? Ordbibliotek.TilAfsendelse(ordbog) : null,
                v.Talesprog);

            if (raa.Raa.Length == 0)
            {
                Melder?.Invoke(Sprog.T("diktering.intet_hoert"));
                return;
            }

            // ============ ORDBOGEN RETTER BAGEFTER OGSAA ============
            //
            // Forhaandsviden hjaelper udskriften, men afgoer den ikke.
            // «Kernesys» kan stadig komme tilbage som «Kernesus», og et navn,
            // der er een bogstavfejl fra det rigtige, skal ellers rettes i
            // haanden hver eneste gang. Se Ordretter for forsigtigheden.
            var (udskrift, rettelser) = ordbog.Count > 0
                ? Ordretter.Ret(raa.Raa, ordbog)
                : (raa.Raa, (IReadOnlyList<Ordrettelse>)Array.Empty<Ordrettelse>());

            // I proeverummet er det den valgte type, der gaelder. Man er
            // netop derinde for at proeve EN bestemt.
            //
            // ============ MEN KUN HVIS MAN STOD DER ============
            //
            // Proeverummet maa kun tage dikteringen, naar HeyPia var det
            // vindue, man havde fremme, da man begyndte at tale. Dikterer man
            // ind i en mail, er det mailen, teksten skal i - ogsaa selv om
            // proeverummet tilfaeldigvis er den valgte fane bag ved.
            //
            // Set 30-08-2026: brugeren dikterede og fik «Proevet som note -
            // se resultatet ovenfor». Teksten kom aldrig i udklipsholderen og
            // blev aldrig tilbudt som note. Proeverummet havde taget den, fra
            // en fane han ikke engang kiggede paa.
            var proeve = Vores(_maal) ? Proeve : null;

            var formaal = proeve is not null
                ? proeve.Formaal
                : Programformaal.Vaelg(_maal.Proces, _maal.Titel, Formaal(), v.DikteringEfterProgram);

            var tekst = udskrift;

            if (v.DikteringPuds)
            {
                Melder?.Invoke(Sprog.T("diktering.rydder_op"));
                tekst = await klient.PudsAsync(
                    udskrift, formaal, Teksttyper.Prompt(formaal, v.Teksttyper));
            }

            if (proeve is not null)
            {
                // Teksten bliver i proeverummet. Intet indsaettes,
                // udklipsholderen roeres ikke, og der skrives ikke i
                // historikken: en proeve er ikke et diktat, man skal kunne
                // finde igen - den er noget, man kaster vaek og laver om.
                proeve.Vis(udskrift, tekst);

                Melder?.Invoke(afbrudt
                    ? Sprog.T("settingsview.diktering_afbrudt")
                    : Sprog.T("diktering.proevet", formaal.ToString().ToLowerInvariant()));

                return;
            }

            // ============ KOM DET FRA VAAGEORDET? ============
            //
            // Saa er der som regel sagt HVAD det skal bruges til, og teksten
            // skal ikke bare lande, hvor markoeren tilfaeldigvis staar. Se
            // Hensigtstolk.
            //
            // Svarer ruteren falsk, var der ingen hensigt at genkende, og saa
            // gaar teksten den almindelige vej.
            if (_fraVaageord && Ruter is { } ruter
                && ruter(new Core.Diktatudfald(tekst, udskrift, formaal.ToString())))
            {
                _fraVaageord = false;
                return;
            }

            var indsat = v.DikteringIndsaet && Indsaetter.Indsaet(tekst, _maal);
            if (!indsat) Indsaetter.Læg(tekst);

            // Skaermen skal kunne tilbyde at gemme den. Teksten gives med -
            // vagten gemmer ikke selv, for det er brugerens valg.
            Faerdig?.Invoke(new Core.Diktatudfald(tekst, udskrift, formaal.ToString()));

            // BESKEDEN OM AFBRYDELSEN KOMMER TIL SIDST, efter teksten er i hus.
            // Kom den foerst, ville man tro, at det, man havde sagt, var tabt.
            Melder?.Invoke(afbrudt
                ? Sprog.T("settingsview.diktering_afbrudt")
                : indsat
                    ? Sprog.T("diktering.indsat", Vindue(), formaal.ToString().ToLowerInvariant())
                    : Sprog.T("diktering.klar", tekst.Length));

            // Rettelserne skrives med. Retter ordbogen noget forkert, er det
            // her, man kan se HVAD den rettede - ellers er der ingen vej
            // tilbage til det, der faktisk blev sagt.
            var spor = rettelser.Count == 0
                ? tekst
                : tekst
                  + Environment.NewLine + Environment.NewLine
                  + Sprog.T("diktering.rettede", rettelser.Count)
                  + Environment.NewLine
                  + string.Join(Environment.NewLine,
                                rettelser.Select(r => $"  {r.Hoert} → {r.Rigtigt}"));

            Historik.Skriv(HaendelseType.Transskription,
                Sprog.T("diktering.historik", (int)raa.Sekunder),
                spor);
        }
        catch (Exception ex)
        {
            Melder?.Invoke(Sprog.T("diktering.gik_galt", ex.Message));
        }
        finally
        {
            _arbejder = false;

            // Uret hoerer til DEN diktering, der lige sluttede. Blev den
            // afsluttet et andet sted fra - af loftet, af en fejl - ville det
            // ellers blive ved at tikke og afslutte den NAESTE.
            _stilhedsur?.Stop();
            _stilhedsur = null;

            Slet(klip);
        }
    }

    /// <summary>Programmets navn, som det kan stå i en besked til brugeren.</summary>
    private string Vindue() => _maal.Proces.Length > 0 ? _maal.Proces : "?";

    private static Dikteringsformaal Formaal() =>
        Enum.TryParse<Dikteringsformaal>(AppSettings.Current.DikteringFormaal,
                                         ignoreCase: true, out var f)
            ? f
            : Dikteringsformaal.Note;

    /// <summary>
    /// Klippet er din stemme. Det slettes, så snart teksten er i hus.
    /// </summary>
    private static void Slet(string sti)
    {
        try { if (File.Exists(sti)) File.Delete(sti); }
        catch (IOException) { /* en laast fil rydder Windows selv op i temp. */ }
    }

    private void Ryd()
    {
        try { _optager?.Dispose(); } catch (Exception) { /* var aldrig startet. */ }
        _optager = null;

        if (_klip is not null) Slet(_klip);
        _klip = null;
    }

    public void Dispose() => Ryd();
}





