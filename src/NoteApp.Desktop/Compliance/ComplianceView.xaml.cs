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

        VisKvitteringer();

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

    /// <summary>
    /// Kvitteringerne — hvad der faktisk har forladt maskinen.
    ///
    /// Resten af skærmen beskriver, hvad appen GØR. Det her er, hvad der
    /// SKETE. En revision spørger om det sidste, og en beskrivelse af koden
    /// kan ikke svare på det.
    /// </summary>
    private void VisKvitteringer()
    {
        var alle = Kvitteringer.Laes();

        KvitAntal.Text = alle.Count.ToString();
        KvitTegn.Text = alle.Count == 0 ? "0" : $"{alle.Sum(k => (long)k.Tegn):N0}";
        // KURSEN ER STABIL, OG DET ER GRUNDEN TIL AT VISE KRONER.
        //
        // Danmark foerer fastkurspolitik over for euroen, saa 7,46 staar
        // stille. Den gamle omregning gik gennem dollar med et rundt tal paa
        // 6,50 - en kurs, der flytter sig, og som gjorde kronebeloebet
        // mindre paalideligt end euroen, det kom fra.
        //
        // Konstanten staar her og ikke i en indstilling: et felt, brugeren
        // kan rette, ville love en noejagtighed, tallet ikke har.
        const decimal KronerPrEuro = 7.46m;

        var samlet = alle.Sum(k => k.PrisEur);

        KvitPris.Text = $"€{samlet:0.00}";
        KvitPrisKr.Text = alle.Count == 0 ? "" : $"ca. {samlet * KronerPrEuro:0.00} kr.";

        KvitTom.Visibility = alle.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        KvitListe.ItemsSource = alle.Select(k => new
        {
            Hvad = k.KildeTitel.Length > 0
                ? $"{k.Skabelon} — {k.KildeTitel}"
                : k.Skabelon,

            Naar = k.Tidspunkt.LocalDateTime.ToString("dd-MM-yyyy HH:mm:ss"),

            Linje = $"{new Uri(k.Endepunkt).Host}  ·  {k.Model}  ·  " +
                    $"{k.Tegn:N0} tegn sendt  ·  {k.TokensInd:N0} ind / {k.TokensUd:N0} ud  ·  " +
                    $"€{k.PrisEur:0.0000} (ca. {k.PrisEur * KronerPrEuro:0.00} kr.)  ·  {k.Sekunder:0.0} sek",

            // Kontrolsummen staar HELT ud. Det er den, der goer kvitteringen
            // til et bevis - en forkortet sum kan ikke sammenlignes med noget.
            Sum = "SHA-256: " + k.Sum,

            Kant = (System.Windows.Media.Brush)FindResource(k.Lykkedes ? "PanelKant" : "FejlTekst"),
            Fejl = k.Fejl,
            FejlSynlig = k.Fejl.Length > 0 ? Visibility.Visible : Visibility.Collapsed
        }).ToList();
    }

    private void Kvitteringer_Klik(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Kvitteringer.Directory);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(Kvitteringer.Directory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne mappen",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
    }
}
