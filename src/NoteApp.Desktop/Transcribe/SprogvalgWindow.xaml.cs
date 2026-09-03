using System.Windows;

namespace NoteApp.Desktop.Transcribe;

/// <summary>
/// Spørger om sproget på hvert spor, lige inden der skrives ud.
///
/// HVORFOR DET IKKE ER EN INDSTILLING
///
/// Det var det først, under Lyd. Det var forkert: man holder ikke alle sine
/// møder på samme sprog. Nogle er på dansk, nogle på engelsk, og gæsterne kan
/// tale et tredje. En global indstilling ville være rigtig for ét møde og
/// forkert for det næste — og fejlen ville ikke vise sig som en fejl.
///
/// HVORFOR APPEN IKKE BARE GÆTTER
///
/// Fordi det er målt, at den gætter forkert. På ét dansk-norsk møde blev
/// begge spor bedømt til engelsk: mikrofonen med 42 % sandsynlighed,
/// gæsternes spor med 26–34 %, målt fra fire forskellige steder i
/// optagelsen. Resultatet var en hel udskrift på engelsk af en samtale, der
/// foregik på dansk og norsk — og et referat bygget oven på den.
///
/// Brugeren var der. Brugeren ved det. Det er det billigste rigtige svar,
/// der findes.
/// </summary>
public partial class SprogvalgWindow : Window
{
    /// <summary>
    /// Sprogene, der kan vælges.
    ///
    /// Listen er kort med vilje. Whisper kan næsten hundrede sprog, men en
    /// rulleliste med halvfems punkter gør det svært at finde de fem, nogen
    /// faktisk bruger. «Find selv» står nederst, fordi den er den, der blev
    /// målt forkert.
    /// </summary>
    public static readonly (string Kode, string Navn)[] Sprog =
    {
        ("da", "Dansk"),
        ("en", "Engelsk"),
        // «no» og IKKE «nb». Whisper kender ikke Bokmaal som selvstaendig
        // kode: motoren svarer «unknown language 'nb'», skriver sin
        // hjaelpetekst og stopper. Det kostede en koersel paa tyve minutter,
        // hvor sporet fejlede uden at nogen opdagede det.
        ("no", "Norsk"),
        ("sv", "Svensk"),
        ("de", "Tysk"),
        ("fr", "Fransk"),
        ("auto", "Lad appen finde selv")
    };

    /// <summary>Sproget på mikrofonsporet.</summary>
    public string MitSprog { get; private set; } = "da";

    /// <summary>Sproget på gæsternes spor. Null når mødet kun har ét spor.</summary>
    public string? DeresSprog { get; private set; }

    /// <param name="toSpor">Findes gæsternes spor? Ellers spørges der kun om ét.</param>
    /// <param name="sidsteMit">Det, der blev valgt sidst for netop dette møde.</param>
    /// <param name="kunHoejttaler">
    /// Er det ene spor højttalerens frem for mikrofonens? Sandt ved et webinar.
    ///
    /// Dialogen spurgte før om «dit spor — mikrofonen», uanset hvad der lå i
    /// mappen. På et webinar er der ingen mikrofon: sporet er dét, computeren
    /// afspillede, og den, der taler, er oplægsholderen. Et forkert navn på
    /// sporet er ikke en detalje her — det er netop den slags, der får en til
    /// at vælge dansk, fordi man tænker på sig selv.
    /// </param>
    public SprogvalgWindow(bool toSpor, string? sidsteMit, string? sidsteDeres, string moedetitel,
                           bool kunHoejttaler = false)
    {
        InitializeComponent();

        Under.Text = toSpor
            ? $"«{moedetitel}» blev optaget på to spor. Hvert spor skrives ud for sig, så de to sider af mødet gerne må være på hvert sit sprog."
            : kunHoejttaler
                ? $"«{moedetitel}» blev optaget på ét spor — det, computeren afspillede."
                : $"«{moedetitel}» blev optaget på ét spor — mikrofonen.";

        if (kunHoejttaler)
        {
            MitNavn.Text = "Sproget i webinaret";
            MitUnder.Text = "Det, oplægsholderen taler. Din egen mikrofon blev ikke optaget.";
        }

        DeresPanel.Visibility = toSpor ? Visibility.Visible : Visibility.Collapsed;

        Mit.ItemsSource = Sprog.Select(s => s.Navn).ToList();
        Deres.ItemsSource = Sprog.Select(s => s.Navn).ToList();

        Mit.SelectedIndex = Nr(sidsteMit);
        Deres.SelectedIndex = Nr(sidsteDeres);

        // ER DER VALGT FOER FOR DETTE MOEDE, SIGES DET.
        //
        // Ellers ser en forudvalgt vaerdi ud som appens gaet - og saa
        // overvejer man den ikke. Staar der, at det er ens eget valg, er den
        // til at stole paa.
        //
        // Der staar ikke laengere «sidst». Sproget kan nu ogsaa vaere valgt,
        // FOER der blev optaget, og saa ville «sidst» pege paa noget, der
        // aldrig er sket. Det, der baerer saetningen, er hvem der valgte —
        // ikke hvornaar.
        Husket.Text = sidsteMit is null
            ? ""
            : "Forudvalgt efter dit eget valg for dette møde — ikke appens gæt.";
    }

    private static int Nr(string? kode)
    {
        if (kode is null) return 0;

        var n = Array.FindIndex(Sprog, s => s.Kode == kode);
        return n < 0 ? 0 : n;
    }

    /// <summary>
    /// «Skriv ud» — gemmer valget og lukker vinduet med et ja.
    /// </summary>
    /// <remarks>
    /// SPORET STAAR HER, FORDI FEJLEN IKKE KUNNE SES UDEFRA.
    ///
    /// Set 02-09 og 03-09-2026: brugeren vaelger dansk paa begge spor, trykker
    /// «Skriv ud», vinduet lukker - og transskriptionen bliver afbrudt med
    /// «sprogvinduet blev lukket uden at der blev valgt et sprog». Altsaa
    /// svarede ShowDialog IKKE true, selv om knappen blev trykket.
    ///
    /// Der er kun to maader: enten naaede linjen med DialogResult aldrig at
    /// blive koert, eller ogsaa kastede noget foer den. En fejl i en
    /// klik-haandtering inde i et modalt vindue ryger op i appens faelles
    /// haandtering og kan lukke vinduet med et tomt svar - og saa ser det
    /// ud, som om brugeren fortroed.
    ///
    /// Linjerne herunder svarer paa hvilken af delene. De bliver staaende:
    /// et vindue, der lukker uden at sige hvorfor, er den slags fejl, der
    /// koster en hel dag at finde.
    /// </remarks>
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MitSprog = Sprog[Math.Max(0, Mit.SelectedIndex)].Kode;

            DeresSprog = DeresPanel.Visibility == Visibility.Visible
                ? Sprog[Math.Max(0, Deres.SelectedIndex)].Kode
                : null;

            // ============ VINDUET SVARER MED SIT EGET FLAG ============
            //
            // DialogResult ER IKKE TIL AT STOLE PAA HER.
            //
            // Maalt 03-09-2026: knappen satte det til true - linjen herunder
            // blev skrevet - og ét sekund senere laeste kaldet «ShowDialog:
            // False · vinduets DialogResult: False». Noget saetter det om
            // undervejs i lukningen, og hverken en Closing-haandtering, en
            // Close(), en IsCancel i knapstilen eller en fejl i den her metode
            // kunne findes. Alle fire er efterproevet.
            //
            // Saa holdes svaret et sted, WPF ikke roerer. Et felt, der saettes
            // ét sted og laeses ét sted, kan ikke aendre sig bag om nogen -
            // og det er billigere end at blive ved med at jage aarsagen.
            Godkendt = true;

            Core.Historik.Skriv(Core.HaendelseType.Andet, "Sprogvinduet: der blev trykket «Skriv ud»",
                $"Mit: «{MitSprog}» · deres: «{DeresSprog}» · "
                + $"valgt nr {Mit.SelectedIndex}/{Deres.SelectedIndex}",
                Core.Udfald.Fuldført);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            try
            {
                Core.Historik.Skriv(Core.HaendelseType.Andet, "Sprogvinduet braekkede",
                    $"{ex.GetType().Name}: {ex.Message}", Core.Udfald.SeEfter);
            }
            catch (Exception)
            {
                // Kan historikken ikke skrives, er der ikke mere at goere her.
            }

            // Vinduet skal IKKE blive staaende med en fejl, ingen kan se.
            // Et nej er et aerligt svar; en laast dialog er ikke.
            DialogResult = false;
        }
    }

    private void Annuller_Click(object sender, RoutedEventArgs e)
    {
        Godkendt = false;
        DialogResult = false;
    }

    /// <summary>
    /// Blev der trykket «Skriv ud»? Vinduets eget svar.
    /// </summary>
    /// <remarks>
    /// Se Ok_Click: DialogResult skiftede fra true til false undervejs i
    /// lukningen, og det kostede en dags fejlsoegning. Det her felt saettes
    /// ét sted og laeses ét sted.
    /// </remarks>
    public bool Godkendt { get; private set; }
}
