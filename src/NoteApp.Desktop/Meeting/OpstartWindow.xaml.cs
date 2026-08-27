using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;
using NoteApp.Core.Llm;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// De tre spørgsmål, der stilles omkring en optagelse: mappe, mødetype og
/// sprog. Ét vindue til både møde og webinar.
///
/// HVORFOR DE STILLES FØR OG IKKE BAGEFTER
///
/// Mappen: bagefter er der ingen, der rydder op — så alt ender i den samme
/// bunke. Vælges den her, hvor man ved, hvad mødet handler om, ligger
/// materialet rigtigt fra begyndelsen, og en søgning inden for mappen bliver
/// et opslagsværk frem for en gennemgang.
///
/// Mødetypen: den er kendt før mødet. Bagefter skal man oversætte sit møde til
/// en skabelon, og det er dér, folk står af.
///
/// Sproget: målt 19-08-2026 gættede appen begge spor på et dansk-norsk møde
/// til engelsk, og hele udskriften blev vrøvl. Brugeren var der og ved det.
///
/// INTET AF DET MÅ KUNNE FORSINKE EN OPTAGELSE. Alle tre felter må stå tomme,
/// og genvejstasten går helt uden om vinduet: dér starter optagelsen først, og
/// spørgsmålene kommer ovenpå, mens der optages.
/// </summary>
public partial class OpstartWindow : Window
{
    /// <summary>Hvilken slags optagelse — og om den allerede er i gang.</summary>
    public enum Slags
    {
        /// <summary>Et almindeligt møde. Spørges før optagelsen.</summary>
        Moede,

        /// <summary>Et webinar. Spørges før, og der spørges også om linket.</summary>
        Webinar,

        /// <summary>
        /// Optagelsen kører allerede — startet med genvejstasten.
        ///
        /// Så er der ikke noget at fortryde: knapperne hedder noget andet, og
        /// lukker man vinduet, optages der bare videre uden mappe og type.
        /// </summary>
        Igang
    }

    /// <summary>Brugerens mappe. Null betyder «uden mappe».</summary>
    private string? _forvalgtMappe;
    private string? _forvalgtType;

    public string? Mappe { get; private set; }

    /// <summary>Navnet på den valgte mødetype. Null betyder «ikke valgt».</summary>
    public string? Moedetype { get; private set; }

    /// <summary>Sprogkoden, fx «da» eller «en». Aldrig null — «auto» er et valg.</summary>
    public string Sprog { get; private set; } = "da";

    /// <summary>Linket til webinaret. Null ved møder og ved et tomt felt.</summary>
    public string? Kilde { get; private set; }

    /// <summary>
    /// Et punkt i en af rullelisterne: det, der vises, og det, der gemmes.
    ///
    /// DEN ER PUBLIC MED VILJE. WPF binder gennem refleksion, og en privat
    /// type giver ingen fejl — felterne står bare tomme. Det er sket i det her
    /// projekt før, og det koster en halv time hver gang, fordi alt ser
    /// rigtigt ud undtagen indholdet.
    /// </summary>
    public sealed record Punkt(string Navn, string Under, string? Vaerdi);

    /// <param name="forvalgtMappe">Mappen fra aftalen, hvis den allerede er valgt dér.</param>
    /// <param name="forvalgtType">Mødetypen fra aftalen, hvis den allerede er valgt dér.</param>
    /// <remarks>
    /// DER SPØRGES KUN OM DET, DER MANGLER.
    ///
    /// En aftale fra kalenderen har allerede mappe og mødetype på sig — man har
    /// skrevet dem i aftalevinduet. Sproget står derimod på «Spørg mig», og det
    /// er med vilje: rammer sproget forkert, bliver hele transskriptionen
    /// vrøvl, og det opdages først i referatet.
    ///
    /// Men dialogen viste ALLE TRE felter igen, tomme. Så sad man og valgte
    /// mappe og mødetype for anden gang, mens webinaret gik i gang — og det
    /// ligner en fejl, ikke et spørgsmål. Er de to allerede besvaret, skjules
    /// de nu, og der står ét spørgsmål tilbage.
    ///
    /// Værdierne SKJULES OG BEVARES — de gives videre uændret, så den, der
    /// kalder, ikke skal huske at flette dem ind igen.
    /// </remarks>
    public OpstartWindow(Slags slags, string? forvalgtMappe = null, string? forvalgtType = null)
    {
        InitializeComponent();

        var erWebinar = slags == Slags.Webinar;

        _forvalgtMappe = string.IsNullOrWhiteSpace(forvalgtMappe) ? null : forvalgtMappe;
        _forvalgtType = string.IsNullOrWhiteSpace(forvalgtType) ? null : forvalgtType;
        var kunSprog = _forvalgtMappe is not null && _forvalgtType is not null;

        Title = erWebinar ? "Optag et webinar" : "Optag";

        Overskrift.Text = slags switch
        {
            Slags.Webinar => "Optag et webinar",
            Slags.Igang => "Optagelsen er i gang",
            _ => "Optag et møde"
        };

        Underskrift.Text = slags switch
        {
            // DEN ENE SÆTNING, DER SKAL LÆSES VED ET WEBINAR.
            //
            // Her stod en hel kasse om, hvorfor webinarknappen findes: ét spor
            // mod to, megabyte i timen, minutter til udskrivning. Man har
            // allerede trykket på knappen, når man læser det — og ingen
            // vælger sit webinar fra, fordi det fylder 110 MB.
            //
            // Tilbage er det ENESTE, der har en følge for brugeren: din
            // mikrofon optages ikke.
            Slags.Webinar => "Der optages kun det, du hører — ikke din mikrofon. Alt herunder kan ændres bagefter.",
            Slags.Igang => "Der optages, mens du svarer. Luk vinduet, hvis du hellere vil tage det senere.",
            _ => "Alt herunder kan ændres bagefter."
        };

        Linkfelt.Visibility = erWebinar ? Visibility.Visible : Visibility.Collapsed;
        Stopperselv.Visibility = erWebinar ? Visibility.Visible : Visibility.Collapsed;

        StartKnap.Content = slags switch
        {
            Slags.Webinar => "Optag webinaret",
            Slags.Igang => "Gem valgene",
            _ => "Optag"
        };

        FortrydKnap.Content = slags == Slags.Igang ? "Ikke nu" : "Fortryd";

        Fyld(erWebinar);

        if (kunSprog)
        {
            Mappefelt.Visibility = Visibility.Collapsed;
            Typefelt.Visibility = Visibility.Collapsed;

            Underskrift.Text = erWebinar
                ? $"Mappe og mødetype står allerede på aftalen ({_forvalgtMappe}, {_forvalgtType}). Der mangler kun sproget — og det kan ikke gættes uden at koste hele transskriptionen."
                : $"Mappe og mødetype står allerede på aftalen ({_forvalgtMappe}, {_forvalgtType}). Der mangler kun sproget.";
        }

        // Startknappen faar fokus, ikke det foerste felt. Trykker man bare
        // retur, koerer den — og det skal den kunne, for det er hele forskellen
        // paa en dialog, der hjaelper, og en, der staar i vejen.
        Loaded += (_, _) => StartKnap.Focus();
    }

    // ------------------------------------------------------------- felterne

    /// <summary>
    /// Fylder felterne — og lægger forklaringerne i værktøjstips.
    ///
    /// HVORFOR FORKLARINGERNE IKKE STÅR PÅ SKÆRMEN LÆNGERE
    ///
    /// Hvert felt havde to-tre linjers hjælpetekst under sig. Det er dobbelt
    /// så høj en dialog, og den skal besvares, mens et webinar går i gang.
    /// Teksterne er ikke slettet — de ligger som tip på selve feltet, hvor de
    /// kan hentes af den, der er i tvivl, uden at koste plads for den, der
    /// ikke er.
    /// </summary>
    private void Fyld(bool erWebinar)
    {
        // ---- mappen
        Mappevalg.ToolTip = erWebinar
            ? "Læg det i en mappe nu — fx et emne eller et fag. Så samler webinarerne sig dér, hvor du senere leder efter dem."
            : "Læg det i en mappe nu — fx en kunde eller et fag. Så ligger alt om den sag samlet, og du kan søge på tværs af det hele bagefter.";

        FyldMapper(null);

        // ---- moedetypen
        Typevalg.ToolTip = "Mødetypen bestemmer, hvordan mødet bliver skrevet ud i et dokument bagefter. Den kan også vælges senere.";

        var typer = new List<Punkt> { new("Ikke valgt", "Vælges når du laver et dokument", null) };

        foreach (var t in PromptTemplate.LoadAll())
            typer.Add(new Punkt(t.Name, t.Description ?? "", t.Name));

        Typevalg.ItemsSource = typer;
        Typevalg.SelectedIndex = 0;

        // ---- sproget
        SprogMaerkat.Text = erWebinar ? "SPROG PÅ WEBINARET" : "SPROG DER TALES";

        Sprogvalg.ToolTip = erWebinar
            ? "Der spørges nu, fordi et webinar begynder på slaget — og fordi et engelsk webinar skrives ud på det halve af tiden, når sproget er sagt på forhånd."
            : "Appen gætter ellers sproget ud fra de første tredive sekunder. Rammer gættet forkert, bliver hele transkriptionen ubrugelig — og den ligner en færdig tekst.";

        Link.ToolTip = "Ligger webinaret online bagefter, er linket vejen tilbage til det, der blev VIST — og det er væk fra indbakken en måned senere.";

        Sprogvalg.ItemsSource = SprogvalgWindow.Sprog
            .Select(s => new Punkt(s.Navn, "", s.Kode))
            .ToList();

        // ENGELSK ER FORVALGT VED WEBINARER, IKKE DANSK.
        //
        // Modsat alt andet i appen. Et webinar, man tilmelder sig, er oftere
        // paa engelsk end paa dansk — det er derfor, oversaettelsen
        // overhovedet er en funktion. Standarden skal vaere det almindelige
        // tilfaelde.
        var kode = erWebinar ? "en" : "da";
        var nr = SprogvalgWindow.Sprog.ToList().FindIndex(s => s.Kode == kode);
        Sprogvalg.SelectedIndex = nr >= 0 ? nr : 0;
    }

    /// <summary>
    /// Fylder mappelisten og vælger <paramref name="vaelg"/>, hvis den findes.
    /// </summary>
    private void FyldMapper(string? vaelg)
    {
        // «VÆLG EN MAPPE» OG IKKE «UDEN MAPPE».
        //
        // Listen står på det første punkt, indtil man vælger noget andet, og
        // det punkt er derfor det, der opfordrer. «Uden mappe» lyder som et
        // svar, man har givet — og så er der ingen grund til at åbne listen.
        var punkter = new List<Punkt> { new("Vælg en mappe", "", null) };

        foreach (var m in Mapper.Alle(Mapper.Slags.Optagelser))
            punkter.Add(new Punkt(m, "", m));

        Mappevalg.ItemsSource = punkter;

        Mappevalg.SelectedItem = vaelg is null
            ? punkter[0]
            : punkter.FirstOrDefault(p => p.Vaerdi is { } v
                && v.Equals(vaelg, StringComparison.CurrentCultureIgnoreCase)) ?? punkter[0];
    }

    /// <summary>
    /// Opretter en mappe uden at forlade dialogen.
    ///
    /// DEN SKAL KUNNE OPRETTES HER. Skulle man ud i Optagelser for at lave
    /// mappen først, ville man lade være — og så lander optagelsen uden mappe,
    /// hvilket er præcis det, hele dialogen er sat i verden for at undgå.
    /// </summary>
    private void NyMappe_Klik(object sender, RoutedEventArgs e)
    {
        var vindue = RenameWindow.TilNyMappe();
        vindue.Owner = this;

        if (vindue.ShowDialog() != true) return;

        var navn = vindue.NytNavn;

        if (!Mapper.Opret(Mapper.Slags.Optagelser, navn))
        {
            Dialogs.AppDialog.Vis(this, "Den findes allerede",
                $"Der er allerede en mappe, der hedder «{navn}».", Dialogs.Slags.Valg);

            // Den findes - saa vaelg den frem for at lade brugeren lede efter
            // den i listen bagefter.
            FyldMapper(navn);
            return;
        }

        FyldMapper(navn);
    }

    // ------------------------------------------------------------- knapperne

    /// <summary>
    /// Vælger man mødetype, følger mappen med — hvis man ikke selv har valgt en.
    /// </summary>
    /// <remarks>
    /// ET WEBINAR HØRER I «WEBINARER». Hver gang. Så skal man ikke vælge det
    /// hver gang; man har allerede sagt det ved at vælge mødetypen.
    ///
    /// DEN FORESLÅR, DEN BESTEMMER IKKE. Har man selv valgt en mappe, bliver
    /// den stående. Et valg, man har truffet med hånden, må en automatik
    /// aldrig lave om — så holder man op med at stole på begge dele.
    ///
    /// Er mappefeltet skjult, fordi aftalen allerede har svaret, sker der
    /// ingenting: så er mappen valgt, og det er ikke her, den rettes.
    /// </remarks>
    private void Type_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_forvalgtMappe is not null) return;                 // aftalen har svaret
        if (Mappevalg.SelectedItem is Punkt { Vaerdi.Length: > 0 }) return;  // selv valgt

        if ((Typevalg.SelectedItem as Punkt)?.Vaerdi is not { Length: > 0 } type) return;

        var mappe = NoteApp.Core.Llm.PromptTemplate.LoadAll()
            .FirstOrDefault(t => t.Name.Equals(type, StringComparison.CurrentCultureIgnoreCase))
            ?.Mappe;

        if (string.IsNullOrWhiteSpace(mappe)) return;

        // Findes mappen ikke i listen, laegges den ind. Ellers ville en
        // moedetype pege paa noget, man ikke kan vaelge.
        if (Mappevalg.ItemsSource is List<Punkt> punkter
            && punkter.All(x => x.Vaerdi != mappe))
        {
            punkter.Add(new Punkt(mappe, "", mappe));
            Mappevalg.ItemsSource = null;
            Mappevalg.ItemsSource = punkter;
        }

        Mappevalg.SelectedItem = (Mappevalg.ItemsSource as List<Punkt>)
            ?.FirstOrDefault(x => x.Vaerdi == mappe);
    }

    private void Start_Klik(object sender, RoutedEventArgs e)
    {
        // ER FELTET SKJULT, GAELDER AFTALENS VAERDI. Laeses rullelisten
        // alligevel, ville et skjult felt paa «Ikke valgt» slette det, der
        // stod paa aftalen - og saa havde man mistet noget ved at faa faerre
        // spoergsmaal.
        Mappe = _forvalgtMappe ?? (Mappevalg.SelectedItem as Punkt)?.Vaerdi;
        Moedetype = _forvalgtType ?? (Typevalg.SelectedItem as Punkt)?.Vaerdi;
        if (Sprogvalg.SelectedItem is Punkt s && s.Vaerdi is { } kode) Sprog = kode;

        var link = Link.Text.Trim();
        Kilde = link.Length > 0 ? link : null;

        DialogResult = true;
        Close();
    }

    private void Fortryd_Klik(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
