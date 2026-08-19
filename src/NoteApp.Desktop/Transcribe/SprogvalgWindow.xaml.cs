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
    public SprogvalgWindow(bool toSpor, string? sidsteMit, string? sidsteDeres, string moedetitel)
    {
        InitializeComponent();

        Under.Text = toSpor
            ? $"«{moedetitel}» blev optaget på to spor. Hvert spor skrives ud for sig, så de to sider af mødet gerne må være på hvert sit sprog."
            : $"«{moedetitel}» blev optaget på ét spor — mikrofonen.";

        DeresPanel.Visibility = toSpor ? Visibility.Visible : Visibility.Collapsed;

        Mit.ItemsSource = Sprog.Select(s => s.Navn).ToList();
        Deres.ItemsSource = Sprog.Select(s => s.Navn).ToList();

        Mit.SelectedIndex = Nr(sidsteMit);
        Deres.SelectedIndex = Nr(sidsteDeres);

        // ER DER VALGT FOER FOR DETTE MOEDE, SIGES DET.
        //
        // Ellers ser en forudvalgt vaerdi ud som appens gaet - og saa
        // overvejer man den ikke. Staar der, at det er ens eget valg fra
        // sidst, er den til at stole paa.
        Husket.Text = sidsteMit is null
            ? ""
            : "Forudvalgt efter det, du valgte sidst for dette møde.";
    }

    private static int Nr(string? kode)
    {
        if (kode is null) return 0;

        var n = Array.FindIndex(Sprog, s => s.Kode == kode);
        return n < 0 ? 0 : n;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        MitSprog = Sprog[Math.Max(0, Mit.SelectedIndex)].Kode;

        DeresSprog = DeresPanel.Visibility == Visibility.Visible
            ? Sprog[Math.Max(0, Deres.SelectedIndex)].Kode
            : null;

        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
