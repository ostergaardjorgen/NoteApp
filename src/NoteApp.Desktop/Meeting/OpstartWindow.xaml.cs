using System.Windows;
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

    public OpstartWindow(Slags slags)
    {
        InitializeComponent();

        var erWebinar = slags == Slags.Webinar;

        Title = erWebinar ? "Optag et webinar" : "Optag";

        Overskrift.Text = slags switch
        {
            Slags.Webinar => "Optag et webinar",
            Slags.Igang => "Optagelsen er i gang",
            _ => "Optag et møde"
        };

        Underskrift.Text = slags switch
        {
            Slags.Webinar => "Tre svar, og den kører. Alt kan ændres bagefter.",
            Slags.Igang => "Der optages, mens du svarer. Luk vinduet, hvis du hellere vil tage det senere.",
            _ => "Tre svar, og den kører. Alt kan ændres bagefter."
        };

        WebinarForklaring.Visibility = erWebinar ? Visibility.Visible : Visibility.Collapsed;
        Linkfelt.Visibility = erWebinar ? Visibility.Visible : Visibility.Collapsed;
        Stopperselv.Visibility = erWebinar ? Visibility.Visible : Visibility.Collapsed;

        StartKnap.Content = slags switch
        {
            Slags.Webinar => "Optag webinaret",
            Slags.Igang => "Gem valgene",
            _ => "Optag"
        };

        FortrydKnap.Content = slags == Slags.Igang ? "Ikke nu" : "Fortryd";

        Bundtekst.Text = slags == Slags.Igang
            ? "Optagelsen kører uanset hvad. Svarer du ikke, ligger den uden mappe og uden type — og begge dele kan sættes på bagefter."
            : "Alle tre felter må stå tomme. Mappe og mødetype kan ændres bagefter; sproget vælges igen, når der skrives ud.";

        Fyld(erWebinar);

        // Startknappen faar fokus, ikke det foerste felt. Trykker man bare
        // retur, koerer den — og det skal den kunne, for det er hele forskellen
        // paa en dialog, der hjaelper, og en, der staar i vejen.
        Loaded += (_, _) => StartKnap.Focus();
    }

    // ------------------------------------------------------------- felterne

    private void Fyld(bool erWebinar)
    {
        // ---- mappen
        MappeHjaelp.Text = erWebinar
            ? "Læg det i en mappe nu — fx et emne eller et fag. Så samler webinarerne sig dér, hvor du senere leder efter dem."
            : "Læg det i en mappe nu — fx en kunde eller et fag. Så ligger alt om den sag samlet, og du kan søge på tværs af det hele bagefter.";

        FyldMapper(null);

        // ---- moedetypen
        TypeHjaelp.Text = "Mødetypen bestemmer, hvordan mødet bliver skrevet ud i et dokument bagefter. Den kan også vælges senere.";

        var typer = new List<Punkt> { new("Ikke valgt", "Vælges når du laver et dokument", null) };

        foreach (var t in PromptTemplate.LoadAll())
            typer.Add(new Punkt(t.Name, t.Description ?? "", t.Name));

        Typevalg.ItemsSource = typer;
        Typevalg.SelectedIndex = 0;

        // ---- sproget
        SprogOverskrift.Text = erWebinar ? "HVILKET SPROG ER WEBINARET PÅ?" : "HVILKET SPROG TALES DER?";

        SprogHjaelp.Text = erWebinar
            ? "Der spørges nu, fordi et webinar begynder på slaget — og fordi et engelsk webinar skrives ud på det halve af tiden, når sproget er sagt på forhånd."
            : "Appen gætter ellers sproget ud fra de første tredive sekunder. Rammer gættet forkert, bliver hele udskriften ubrugelig — og den ligner en færdig tekst.";

        Sprogvalg.ItemsSource = SprogvalgWindow.Sprog
            .Select(s => new { s.Kode, s.Navn })
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
        var punkter = new List<Punkt> { new(Mapper.Ingen, "", null) };

        foreach (var m in Mapper.Alle(Mapper.Slags.Optagelser))
            punkter.Add(new Punkt(m, "", m));

        Mappevalg.ItemsSource = punkter;
        Mappevalg.DisplayMemberPath = "Navn";

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

    private void Start_Klik(object sender, RoutedEventArgs e)
    {
        if (Mappevalg.SelectedItem is Punkt m) Mappe = m.Vaerdi;
        if (Typevalg.SelectedItem is Punkt t) Moedetype = t.Vaerdi;

        if (Sprogvalg.SelectedItem is { } valgt)
            Sprog = (string)valgt.GetType().GetProperty("Kode")!.GetValue(valgt)!;

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
