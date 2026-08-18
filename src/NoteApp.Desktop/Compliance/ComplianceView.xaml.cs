using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Compliance;

/// <summary>En model eller et stykke software, der skal kunne gøres rede for.</summary>
public sealed record Komponent(string Navn, string Licens, string Hvor, string Rolle, string Note)
{
    /// <summary>
    /// Farven på licensmærkatet.
    ///
    /// Grøn er «fri at sælge med» — MIT, Apache. Gul er «der følger
    /// betingelser med, som skal videre til kunden». Det er dén forskel, der
    /// koster noget at opdage sent.
    /// </summary>
    public Brush Farve => Licens is "MIT" or "Apache 2.0"
        ? new SolidColorBrush(Color.FromRgb(0x4C, 0xBE, 0x72))
        : new SolidColorBrush(Color.FromRgb(0xF0, 0xB2, 0x3C));
}

/// <summary>
/// Alt, en indkøber, en revisor eller en databeskyttelsesrådgiver spørger om,
/// samlet ét sted.
///
/// HVORFOR TALLENE IKKE REGNES UD HER
///
/// Oplysningerne kommer fra dokumenterne i repoet — mistral-dpa.md for
/// vilkårene, WhisperInstall for modellerne. De er skrevet ind som tekst med
/// en dato på frem for at blive hentet levende, og det er med vilje: en side,
/// der henter sine egne påstande fra nettet, kan ikke sige, hvornår den sidst
/// blev efterprøvet af et menneske. Datoen er hele pointen.
/// </summary>
public partial class ComplianceView : UserControl
{
    public ComplianceView()
    {
        InitializeComponent();

        var tilsluttet = SkyNoegle.Hent() is not null;

        SkyLinje.Text = tilsluttet
            ? $"Sendes til {new Uri(SkyKatalog.Endpoint).Host}"
            : "Ikke sat op endnu — intet sendes";

        Endepunkt.Text = SkyKatalog.Endpoint;

        var whisper = WhisperInstall.Standard;

        Modeller.ItemsSource = new[]
        {
            new Komponent(
                "whisper.cpp",
                "MIT",
                "Kører lokalt",
                "Motoren, der afvikler Whisper-modellen på din maskine. Hentes fra projektets eget udgivelsesarkiv på github.com.",
                "MIT er fri at sælge med. Der er ingen betingelser, der skal videregives til dine kunder."),

            new Komponent(
                whisper.Id,
                "MIT",
                "Kører lokalt",
                "Modellen, der laver lyd om til tekst. Vægtene er OpenAI's og hentes fra huggingface.co.",
                "Lyden forlader ikke maskinen. Hentningen er en envejsforbindelse: appen beder om en navngiven fil og modtager den."),

            new Komponent(
                SkyKatalog.Standard.Navn,
                "Tjeneste",
                "Kører i Frankrig",
                "Sprogmodellen, der laver et dokument ud af udskriften. Kører hos leverandøren — der hentes ingen vægte, og der installeres ingenting.",
                "Det er en tjeneste, ikke en licens. Det, der gælder, er databehandleraftalen og vilkårene ovenfor.")

            // HER STOD ROEST V3. Den er undersoegt og fravalgt, ikke i brug -
            // og en liste over det, der KUNNE have vaeret brugt, hoerer ikke
            // hjemme paa en compliance-side. Undersoegelsen staar i
            // doc/maaling-whisper.md, og licensforholdet (OpenRAIL-M med
            // brugsbegraensninger, der skal videregives) er noteret dér.
        };
    }

    private void Dpa_Klik(object sender, RoutedEventArgs e) => Aabn("https://legal.mistral.ai");
    private void Trust_Klik(object sender, RoutedEventArgs e) => Aabn("https://trust.mistral.ai");
    private void Konsol_Klik(object sender, RoutedEventArgs e) => Aabn("https://console.mistral.ai");

    private void Aabn(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // En browser, der ikke vil aabne, maa ikke ende som en tom knap.
            // Adressen staar der, saa den kan skrives af i haanden.
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne browseren",
                $"Gå til denne adresse i din browser:\n\n{url}", Dialogs.Slags.Valg);
        }
    }
}
