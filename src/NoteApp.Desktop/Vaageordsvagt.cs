using System.Diagnostics;
using System.IO;
using System.Text;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Lytter efter «Hej Pia» og «Hey Pia» — og trykker på genvejstasten.
///
/// VÅGEORDET ER EN UDLØSER, IKKE EN NY FUNKTION. Bliver ordet hørt, kaldes
/// præcis den samme kode som et hold på Ctrl+, — dikteringen begynder, teksten
/// lander, hvor markøren står. Alt nedenunder findes og er prøvet af.
///
/// HVORFOR whisper-command
///
/// Den ligger allerede på maskinen. whisper.cpp, som appen henter til
/// transskriptionen, indeholder den — sammen med stemmevagten
/// ggml-silero-v5.1.2.bin på 0,9 MB. MIT-licens, ingen ny afhængighed, ingen
/// træning i en sky, og dansk er understøttet.
///
/// HVORFOR IKKE DENS EGET «-p»
///
/// whisper-command har et flag til netop et vågeord: <c>-p</c>, «the required
/// activation prompt». Det tager ÉT udtryk. Der skal være to — «hej» og «hey»
/// lyder næsten ens, og udskriften vælger den ene efter, hvor hårdt h'et
/// siges. Lyttes der kun efter den ene, virker vågeordet hver anden gang.
///
/// Derfor skriver den ud, hvad den hører, og matchningen sker her — med
/// <see cref="Vaageord.Hoert"/>, som er prøvet af uden mikrofon.
/// </summary>
public sealed class Vaageordsvagt : IDisposable
{
    private Process? _proces;
    private readonly object _laas = new();

    /// <summary>Rejses, når et vågeord blev hørt. Giver det, der stod efter det.</summary>
    public event Action<string>? Hoert;

    /// <summary>Siger til, når der sker noget, der skal kunne ses.</summary>
    public event Action<string>? Melder;

    /// <summary>Lytter der lige nu?</summary>
    public bool Lytter => _proces is { HasExited: false };

    /// <summary>
    /// Er motoren faerdig med at laese modellen ind og klar til at hoere?
    /// </summary>
    /// <remarks>
    /// «Lytter» og «klar» er ikke det samme. Processen koerer straks; modellen
    /// skal foerst paa grafikkortet, og det tager tid. Sagde skaermen «lytter»
    /// i den tid, sagde man «Hej Pia» og troede, det ikke virkede.
    /// </remarks>
    public bool Klar { get; private set; }

    private DateTime _startet;

    /// <summary>
    /// Hvor meget af én kerne lytningen bruger, i procent. Null, når der ikke
    /// lyttes.
    /// </summary>
    /// <remarks>
    /// DER MAALES, DER PAASTAAS IKKE.
    ///
    /// «Det bruger naesten ingenting» er en paastand, ingen kan efterproeve -
    /// og den slags skal en app ikke komme med om sig selv, naar den har
    /// mikrofonen aaben. Tallet her er processortiden, barneprocessen faktisk
    /// har brugt, delt med den tid der er gaaet. Det staar paa skaermen, og
    /// det kan sammenlignes med Jobliste.
    /// </remarks>
    public double? Forbrug
    {
        get
        {
            try
            {
                if (_proces is not { HasExited: false } p) return null;

                var gaaet = (DateTime.UtcNow - _startet).TotalSeconds;
                if (gaaet < 2) return null;   // for kort til at sige noget

                p.Refresh();
                return p.TotalProcessorTime.TotalSeconds / gaaet * 100;
            }
            catch (Exception)
            {
                // Processen kan vaere doed mellem de to linjer. Saa er der
                // ikke noget at maale.
                return null;
            }
        }
    }

    /// <summary>
    /// Motoren, der lyttes med. Tom, hvis den ikke er hentet.
    /// </summary>
    public static string Motorsti =>
        Path.Combine(UserDataPaths.Root, "motor", "whisper", "bin", "Release", "whisper-command.exe");

    public static bool MotorFindes => File.Exists(Motorsti);

    /// <summary>
    /// Starter lytningen, hvis den ikke kører.
    /// </summary>
    /// <param name="model">Whisper-modellen. Den, brugeren allerede har.</param>
    /// <param name="sprog">«da» eller «en».</param>
    public void Start(string model, string sprog)
    {
        lock (_laas)
        {
            if (Lytter) return;

            if (!MotorFindes || !File.Exists(model))
            {
                Melder?.Invoke(Sprog.T("vaageord.ingen_motor"));
                return;
            }

            try
            {
                // ============ GUIDED MODE, IKKE FRI TRANSSKRIPTION ============
                //
                // HER STOD DER INTET FLAG, OG DET VAR HELE FEJLEN.
                //
                // Uden -cmd og uden -p koerer whisper-command i en tilstand,
                // hvor den FOERST kraever, at man siger en fast engelsk
                // saetning. Maalt 30-08-2026, mens brugeren sagde «Hej Pia»:
                //
                //   Say the following phrase: 'Ok Whisper, start listening
                //   for commands.'
                //   Heard '[Skib]'
                //   WARNING: prompt not recognized, try again
                //
                // Appen lyttede efter «hej pia» i en stroem, hvor ordene
                // aldrig kunne staa. Og den frie transskription hallucinerede
                // paa de korte klip: [Skib], [MUSIK], [Tekstet af Vigre].
                //
                // Med -cmd bindes afkodningen til en liste. Saa KAN den ikke
                // finde paa noget - den kan svare med et af udtrykkene eller
                // ingenting. Se Vaageordsliste for lokkeordene, der giver den
                // et sted at gaa hen, naar ordet ikke blev sagt.
                var liste = Vaageordsliste.Byg(
                    Vaageord.Valgte(AppSettings.Current.Vaageord,
                                    AppSettings.Current.Talesprog),
                    AppSettings.Current.Talesprog);

                _antalUdtryk = liste.Count;
                var listefil = SkrivListe(liste);

                // ============ DEN MIKROFON, BRUGEREN HAR VALGT ============
                //
                // Uden -c aabner motoren «default capture device». Maalt
                // 30-08-2026 var der FIRE at vaelge imellem paa maskinen, og
                // appen sagde ikke hvilken. Resten af appen bruger brugerens
                // valg; vaageordet gjorde ikke.
                // ============ NUMMERET HUSKES ============
                //
                // Foerste gang koster det en genstart: motoren startes,
                // listen laeses, og den startes om med det rigtige nummer.
                // Det er to modelindlaesninger. Maalt 31-08-2026 tager ÉN
                // indlaesning 3,2 sekunder, saa genstarten koster godt tre
                // sekunder mere - ikke et minut, som der stod her.
                //
                // MINUTTET VAR EN FEJLSLUTNING. Det blev maalt paa en opstart,
                // hvor «Capture device»-linjen aldrig naaede frem, fordi
                // stderr blev kastet vaek, og hvor der derfor blev ledt
                // forgaeves. Tiden gik med at vente paa noget, der aldrig kom,
                // og ikke med at laese modellen ind.
                //
                // Derfor gemmes nummeret sammen med NAVNET. Rykker enhederne
                // rundt, passer nummeret ikke laengere, og saa findes det
                // forfra frem for at lytte paa den forkerte.
                if (_kendtIndeks < 0) _kendtIndeks = Mikrofon.Nummer();

                var mikrofon = _kendtIndeks >= 0 ? $" -c {_kendtIndeks}" : "";

                // ============ VINDUET, DER KOSTEDE SYV SEKUNDER ============
                //
                // «-cms» er, hvor lang tid motoren optager, FOER den bedoemmer.
                // Standarden er 8000 ms, og den stod uroert. Derfor gik der
                // otte sekunder fra «Hej Pia» blev sagt, til der skete noget -
                // maalt til ca. syv 31-08-2026.
                //
                // MEN DET VAR IKKE KUN LANGSOMT. DET VAR OGSAA DERFOR, DET
                // RAMTE FORKERT.
                //
                // Motoren bedoemmer HELE vinduet mod listen. Er vinduet otte
                // sekunder, og vaageordet fylder ét af dem, skal to ord
                // forklare syv sekunders stilhed - og saa vinder den mest
                // almindelige saetning paa listen i stedet. Maalt samme dag:
                // hver eneste bedoemmelse landede paa «vi ses i morgen» med
                // 0,15-0,24. «hej pia» optraadte ikke ÉN gang, uanset hvor
                // mange gange brugeren sagde det.
                //
                // 1500 ms passer til det, der skal hoeres: to ord. Vinduet
                // bliver dét, vaageordet fylder, i stedet for syv sekunders
                // stilhed med to ord i.
                //
                // -t 4 og ikke 2: ventetiden er det, der goer den ubrugelig,
                // og fire traade paa en maskine med otte er stadig ikke i
                // vejen for noget.
                var start = new ProcessStartInfo(Motorsti)
                {
                    Arguments = $"-m \"{model}\" -l {sprog} -t 4 "
                              + $"-cms {Vaageord.Kommandovindue} "
                              + "-vth " + Vaageord.Pausetaerskel.ToString(
                                    System.Globalization.CultureInfo.InvariantCulture) + " "
                              + $"-cmd \"{listefil}\"{mikrofon}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };

                _sidsteModel = model;
                _sidsteSprog = sprog;

                _proces = Process.Start(start);
                if (_proces is null) return;

                // ============ DEN SKAL DØ SAMMEN MED APPEN ============
                //
                // Lukkes appen paent, lukkes den her med i Ryd(). Men appen
                // bliver ikke altid lukket paent - udgivelsen slaar en koerende
                // app ihjel, og det goer Jobliste ogsaa. Saa levede den videre
                // med sin model i hukommelsen og sit greb om grafikkortet.
                //
                // Maalt 29-08-2026: TRE af dem samtidig, den aeldste tretten
                // timer gammel, 2,4 GB og 94 % af grafikkortet - mens
                // vaageordet var slaaet FRA. Se Boernejob.
                Boernejob.Tilfoej(_proces);

                _startet = DateTime.UtcNow;

                _proces.OutputDataReceived += (_, e) => Laes(e.Data);
                _proces.BeginOutputReadLine();

                // ============ FEJLSTROEMMEN ER DEN ENESTE STROEM ============
                //
                // HER BLEV DEN KASTET VAEK. Linjerne blev laest for ikke at
                // fylde roeret op, og saa smidt paa gulvet - ud fra en
                // antagelse om, at det vigtige stod paa stdout.
                //
                // MAALT 31-08-2026: stdout er HELT TOM. whisper-command
                // skriver hvert eneste ord paa stderr - ogsaa «listening for a
                // command», som er den, der saetter Klar, og «Capture device
                // #N», som er den, der finder mikrofonens nummer.
                //
                // To ting virkede derfor aldrig: skaermen fik aldrig at vide,
                // at vaageordet var klar, og nummeret paa mikrofonen blev
                // aldrig fundet eller gemt. Det sidste kunne ses direkte i
                // indstillingerne, hvor VaageordMikrofonNummer stod tomt,
                // uanset hvor mange gange motoren havde vaeret startet.
                //
                // Begge stroemme laeses nu det samme sted. Kommer en linje en
                // dag paa stdout i stedet, virker det stadig.
                _proces.ErrorDataReceived += (_, e) => Laes(e.Data);
                _proces.BeginErrorReadLine();

                Klar = false;
                Melder?.Invoke(Sprog.T("vaageord.goer_klar"));
            }
            catch (Exception ex)
            {
                Ryd();
                Melder?.Invoke(Sprog.T("vaageord.gik_galt", ex.Message));
            }
        }
    }

    /// <summary>Stopper lytningen.</summary>
    public void Stop()
    {
        lock (_laas)
        {
            if (_proces is null) return;

            Ryd();
            Melder?.Invoke(Sprog.T("vaageord.stoppet"));
        }
    }

    /// <summary>
    /// Én linje fra motoren.
    /// </summary>
    /// <remarks>
    /// DER LYTTES EFTER ET ORD, IKKE EFTER ALT. Linjen kan være hvad som
    /// helst — motoren skriver også sin egen opstart og sine mellemregninger.
    /// Kun linjer, der begynder med et vågeord, betyder noget; resten falder
    /// på gulvet.
    ///
    /// Det er også dét, der gør, at et møde ikke starter en diktering: der
    /// tales i timevis, og ingen af sætningerne begynder med «Hej Pia».
    /// </remarks>
    /// <summary>Hvor mange udtryk motoren vælger imellem. Afgør grænsen.</summary>
    private int _antalUdtryk;

    /// <summary>Mikrofonens nummer hos motoren. −1 = ikke fundet endnu.</summary>
    private int _kendtIndeks = -1;


    /// <summary>Hvor længe fund fra samme ytring lægges sammen.</summary>
    private static readonly TimeSpan Vindue = TimeSpan.FromMilliseconds(700);

    private sealed record Nyligt(DateTime Naar, Vaageordsfund Fund);

    private readonly List<Nyligt> _nyligeFund = new();

    private double? _kortMB;
    private double? _modelMB;

    /// <summary>
    /// Hvad vågeordet fylder på grafikkortet — målt af motoren selv.
    /// </summary>
    /// <remarks>
    /// Null, indtil motoren har skrevet begge tal, og null hele vejen på en
    /// maskine uden CUDA. Der er ikke noget mellemsvar: enten er begge tal
    /// læst, eller også er der ingenting at sige.
    /// </remarks>
    public Grafikmaal? Grafik =>
        _kortMB is { } kort && _modelMB is { } model ? new Grafikmaal(model, kort) : null;

    /// <summary>Skal vi genstarte for at få den rigtige mikrofon?</summary>
    private bool _skalSkifteMikrofon;

    /// <summary>
    /// Skriver listen, motoren skal vælge imellem.
    /// </summary>
    /// <remarks>
    /// UDEN BOM. PowerShell skrev den med, og så stod der «ï»¿hej pia» som
    /// første udtryk — motoren tog byterne med i ordet, og det første
    /// vågeord kunne aldrig rammes. Set 30-08-2026 under målingen.
    /// </remarks>
    private static string SkrivListe(IReadOnlyList<string> udtryk)
    {
        var sti = Path.Combine(UserDataPaths.Root, "vaageord-liste.txt");
        Directory.CreateDirectory(UserDataPaths.Root);

        File.WriteAllLines(sti, udtryk, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return sti;
    }

    /// <summary>
    /// Læser en linje fra motoren.
    /// </summary>
    /// <remarks>
    /// TO SLAGS LINJER ER INTERESSANTE.
    ///
    /// Listen over mikrofoner kommer ved opstart. Den bruges til at finde
    /// nummeret på DEN mikrofon, brugeren har valgt — motoren kender dem kun
    /// ved navn og nummer, og nummeret er dens eget.
    ///
    /// Og fundene: «detected command: hej pia | p = 0.58 | t = 555 ms».
    /// </remarks>
    private void Laes(string? linje)
    {
        if (string.IsNullOrWhiteSpace(linje)) return;

        Mikrofonlinje(linje);

        // Grafikkortet. Motoren skriver begge tal ved opstart - se Grafikmaal.
        _kortMB ??= Grafikmaal.Kort(linje);
        _modelMB ??= Grafikmaal.Model(linje);
        SkiftMikrofonHvisNoedvendigt(linje);

        // ============ HVORNAAR ER DEN KLAR? ============
        //
        // Modellen skal indlaeses paa grafikkortet, foer der kan hoeres noget.
        // Det tager tid, og i den tid sagde appen «lytter» - saa sagde man
        // «Hej Pia» og troede, det ikke virkede.
        //
        // Motoren siger selv til: «listening for a command ...». Foerst dér
        // er den klar, og foerst dér skal skaermen sige det.
        if (!Klar && linje.Contains("listening for a command", StringComparison.Ordinal))
        {
            Klar = true;
            Melder?.Invoke(Sprog.T("vaageord.klar"));
        }

        if (Vaageordsliste.Laes(linje) is not { } fund) return;

        // ============ HVERT FUND SKRIVES NED ============
        //
        // «Jeg sagde Hej Pia i tyve sekunder, foer der skete noget» kan ikke
        // efterproeves paa en paastand. Motoren fortaeller selv, hvad den
        // hoerte og hvor sikker den var - og uden det tal er enhver
        // forklaring paa, hvorfor den ikke svarede, et gaet.
        //
        var ord = Vaageord.Valgte(AppSettings.Current.Vaageord,
                                  AppSettings.Current.Talesprog);

        // GRAENSEN ER DET, DER GOER DEN BRUGBAR. Motoren vaelger altid et
        // udtryk; det er listens laengde, der afgoer, hvornaar et valg er
        // mere end et gaet. Se Vaageordsliste.Graense.
        // ============ VARIANTERNE LAEGGES SAMMEN ============
        //
        // «hej pia» og «hey pia» er det samme ord og deler sikkerheden mellem
        // sig. Et vaageord, der er sagt tydeligt, ser derfor svagt ud, naar
        // det bedoemmes alene - se Vaageordsliste.Taeller.
        //
        // Vinduet er kort med vilje. Fundene fra ÉN ytring kommer inden for
        // faa hundrede millisekunder; er der gaaet laengere, er det noget
        // andet, der blev sagt, og saa maa det staa paa egne ben.
        var nu = DateTime.UtcNow;
        _nyligeFund.RemoveAll(f => nu - f.Naar > Vindue);
        _nyligeFund.Add(new Nyligt(nu, fund));

        var taeller = Vaageordsliste.Taeller(
            _nyligeFund.Select(f => f.Fund), ord, _antalUdtryk);

        // Udloeste det, ryddes vinduet. Ellers ville de samme fund kunne
        // udloese en gang til, naeste gang der kommer en linje.
        if (taeller) _nyligeFund.Clear();

        // ============ KUN DE FUND, DER BETYDER NOGET ============
        //
        // HER BLEV HVERT ENESTE FUND SKREVET NED, ogsaa de kasserede, og som
        // «se efter». Det var sat ind for at kunne maale, hvorfor vaageordet
        // ikke svarede, og det gjorde sit arbejde: det var dét, der viste, at
        // motoren altid valgte lokkeordet «vi ses i morgen».
        //
        // Men maalingen er gjort, og linjerne blev staaende. Set 31-08-2026:
        // 500 poster i historikken, hvoraf de 502 var «Vaageordet hoerte noget
        // andet» - gule advarsler om noget helt normalt. Historikken er dér,
        // hvor man leder efter, hvad appen har gjort ved ens data, og den var
        // fyldt med stoej fra en lytning, der virkede.
        //
        // Nu skrives kun de fund, der UDLOESER noget. Et kasseret fund er ikke
        // en haendelse; det er lytningen, der goer sit arbejde.
        Maalelinje(fund, taeller);

        if (taeller)
        {
            try
            {
                Historik.Skriv(HaendelseType.Andet, "Vågeordet blev hørt",
                    $"«{fund.Udtryk}» · sikkerhed {fund.Sikkerhed:0.00} · "
                    + $"grænse {Vaageordsliste.Graense(_antalUdtryk):0.00}",
                    Udfald.Fuldført);
            }
            catch (Exception)
            {
                // En log, der ikke kan skrives, maa ikke stoppe lytningen.
            }
        }

        if (!taeller) return;

        Hoert?.Invoke("");
    }

    /// <summary>
    /// Skriver hvert fund i en målefil — ikke i historikken.
    /// </summary>
    /// <remarks>
    /// GRÆNSEN SKAL KUNNE SÆTTES UD FRA TAL OG IKKE UD FRA ET SKØN.
    ///
    /// Den har været 0,21 og 0,11 og er nu 0,21 igen. Hver gang blev den
    /// flyttet, fordi noget gik galt — for høj, og et rigtigt «Hej Pia» blev
    /// kasseret; for lav, og radioen i rummet startede en optagelse. Uden
    /// tallene bag de kasserede fund er den næste flytning også et gæt.
    ///
    /// DEN LIGGER IKKE I HISTORIKKEN. Dér stod den før, og 502 gule
    /// advarsler om noget helt normalt fyldte den skærm, hvor man leder
    /// efter, hvad appen har gjort ved ens data. Det her er en målefil for
    /// den, der skal justere grænsen — ikke en hændelse i appens liv.
    ///
    /// Filen holdes lille og overskriver sig selv. Den skal kunne læses, ikke
    /// arkiveres.
    /// </remarks>
    private static void Maalelinje(Vaageordsfund fund, bool taeller)
    {
        try
        {
            var sti = Path.Combine(UserDataPaths.Root, "log", "vaageord-maaling.log");
            Directory.CreateDirectory(Path.GetDirectoryName(sti)!);

            // Ti tusinde linjer er rigeligt til en dags maaling og fylder
            // under en megabyte. Derover begynder den forfra.
            try
            {
                if (new FileInfo(sti).Length > 1_000_000) File.Delete(sti);
            }
            catch (Exception) { }

            File.AppendAllText(sti,
                $"{DateTime.Now:HH:mm:ss}\t{(taeller ? "JA" : "nej")}\t"
                + fund.Sikkerhed.ToString("0.000",
                      System.Globalization.CultureInfo.InvariantCulture)
                + "\t" + fund.Udtryk + "\n");
        }
        catch (Exception)
        {
            // En maaling, der ikke kan skrives, maa ikke stoppe lytningen.
        }
    }

    /// <summary>
    /// Finder brugerens mikrofon i motorens egen liste.
    /// </summary>
    /// <remarks>
    /// Motoren skriver ved opstart:
    ///
    ///   init:    - Capture device #1: 'Mikrofon (Jabra SPEAK 510 USB)'
    ///
    /// Navnene er Windows' egne, så de kan sammenlignes direkte med det, der
    /// står i indstillingerne. Er den valgte en anden end den, motoren tog,
    /// startes den om ÉN gang med det rigtige nummer.
    /// </remarks>
    private void Mikrofonlinje(string linje)
    {
        if (_kendtIndeks >= 0) return;

        const string maerke = "Capture device #";
        var i = linje.IndexOf(maerke, StringComparison.Ordinal);
        if (i < 0) return;

        var rest = linje[(i + maerke.Length)..];
        var kolon = rest.IndexOf(':');
        if (kolon < 0) return;

        if (!int.TryParse(rest[..kolon].Trim(), out var nr)) return;

        var navn = rest[(kolon + 1)..].Trim().Trim('\'');
        if (navn.Length == 0) return;

        DeviceInfo? valgt;
        try { valgt = Mikrofon.Valgt(); }
        catch (Exception) { return; }

        if (valgt is null || !navn.Equals(valgt.FriendlyName, StringComparison.OrdinalIgnoreCase))
            return;

        _kendtIndeks = nr;
        _skalSkifteMikrofon = true;

        // Gemmes med NAVNET. Naeste opstart springer genstarten over - og
        // skifter enhederne plads, opdages det, fordi navnet ikke passer.
        try
        {
            Mikrofon.HuskNummer(nr, valgt.FriendlyName);
        }
        catch (Exception)
        {
            // Kan det ikke gemmes, koster det bare en genstart naeste gang.
        }
    }

    /// <summary>
    /// Det huskede nummer — hvis det stadig hører til den valgte mikrofon.
    /// </summary>
    private static int HusketIndeks()
    {
        try
        {
            var v = AppSettings.Current;
            if (v.VaageordMikrofonNummer is not { } nr) return -1;
            if (string.IsNullOrWhiteSpace(v.VaageordMikrofonNavn)) return -1;

            var valgt = AudioDevices.ResolveMicrophone(v.MicrophoneId, out _);

            // Passer navnet ikke, er enhederne rykket rundt. Saa er nummeret
            // ikke bare forkert - det peger paa en ANDEN mikrofon, og saa
            // ville vaageordet lytte det forkerte sted uden at sige det.
            return valgt is not null
                   && v.VaageordMikrofonNavn.Equals(valgt.FriendlyName, StringComparison.OrdinalIgnoreCase)
                ? nr
                : -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>Er motoren startet om for at få den rigtige mikrofon?</summary>
    private bool _harSkiftet;

    /// <summary>
    /// Starter motoren om, når vi har fundet nummeret på den rigtige mikrofon.
    /// </summary>
    /// <remarks>
    /// DET KOSTER ÉN GENSTART VED FØRSTE OPSTART, og det er prisen for at
    /// lytte på den rigtige. Motoren kender kun sine mikrofoner ved nummer,
    /// og nummeret står først i dens egen udskrift — altså efter den er
    /// startet. Der er ikke nogen vej udenom.
    ///
    /// Kun én gang. Ellers ville en mikrofon, motoren ikke kan åbne, sende
    /// den i ring.
    /// </remarks>
    private void SkiftMikrofonHvisNoedvendigt(string linje)
    {
        if (_harSkiftet || !_skalSkifteMikrofon) return;
        if (!linje.Contains("attempt to open default capture device", StringComparison.Ordinal)) return;

        _harSkiftet = true;
        _skalSkifteMikrofon = false;

        var model = _sidsteModel;
        var sprog = _sidsteSprog;
        if (model is null || sprog is null) return;

        // Ud af laesningens egen traad. At lukke processen inde fra dens
        // stdout-kald er en vej til at staa fast.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            Stop();
            Start(model, sprog);
        }));
    }

    private string? _sidsteModel;
    private string? _sidsteSprog;

    private void Ryd()
    {
        try
        {
            if (_proces is { HasExited: false })
            {
                _proces.Kill(entireProcessTree: true);
                _proces.WaitForExit(2000);
            }
        }
        catch (Exception)
        {
            // Processen kan vaere doed i forvejen. Der er ikke mere at goere.
        }

        try { _proces?.Dispose(); } catch (Exception) { }
        _proces = null;
    }

    public void Dispose() => Ryd();
}

