using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Engine;

/// <summary>En modelrække, som listen kan vise.</summary>
public sealed class ModelVisning
{
    public ModelVisning(WhisperModel m, bool installeret, bool iBrug)
    {
        Id = m.Id;
        Navn = m.Id;
        Stoerrelse = m.SizeText;
        Resume = m.Summary;
        Fordele = m.Pros;
        Ulemper = m.Cons;

        Maerkat = m.SupportsDanish ? "dansk" : "KUN ENGELSK";
        MaerkatFarve = m.SupportsDanish ? new SolidColorBrush(Color.FromRgb(0x3D, 0xA3, 0x5D))
                                        : new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x30));

        if (iBrug)
        {
            KnapTekst = "I brug";
            KnapAktiv = false;
            Status = "aktiv nu";
            KantFarve = new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF));
        }
        else if (installeret)
        {
            KnapTekst = "Brug denne";
            KnapAktiv = true;
            Status = "hentet";
            KantFarve = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A));
        }
        else
        {
            KnapTekst = $"Hent {m.SizeText}";
            KnapAktiv = true;
            Status = "ikke hentet";
            KantFarve = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A));
        }
    }

    /// <summary>
    /// Den samme række, men for en SPROGMODEL.
    ///
    /// Listen viser to slags modeller, og de har hver deres katalog. De deler
    /// række-udseende, fordi valget er det samme slags valg: hvad koster den,
    /// hvad kan den, og hvad giver man afkald på.
    /// </summary>
    public ModelVisning(LlmModelInfo m, bool installeret, bool iBrug)
    {
        Id = m.Id;
        Navn = m.Id;
        Stoerrelse = m.SizeText;
        Resume = m.Summary;
        Fordele = m.Pros;
        Ulemper = m.Cons;

        // Licensen er mærkatet her. Den er det, der afgør, om modellen må
        // følge med et solgt produkt — og det er en dyrere fejl at opdage
        // bagefter end en model, der er lidt langsom.
        (Maerkat, MaerkatFarve) = m.LicenseClass switch
        {
            LicenseClass.FriTilSalg => ("fri", new SolidColorBrush(Color.FromRgb(0x3D, 0xA3, 0x5D))),
            LicenseClass.BetingelserFoelgerMed => ("betingelser", new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x30))),
            _ => ("ikke til salg", new SolidColorBrush(Color.FromRgb(0xE0, 0x60, 0x60)))
        };

        if (m.Rejected is not null)
        {
            KnapTekst = "Fravalgt";
            KnapAktiv = false;
            Status = "kan ikke hentes";
            Ulemper = m.Rejected;
            KantFarve = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A));
            return;
        }

        if (iBrug)
        {
            KnapTekst = "I brug";
            KnapAktiv = false;
            Status = "aktiv nu";
            KantFarve = new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF));
        }
        else if (installeret)
        {
            KnapTekst = "Brug denne";
            KnapAktiv = true;
            Status = "hentet";
            KantFarve = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A));
        }
        else
        {
            KnapTekst = $"Hent {m.SizeText}";
            KnapAktiv = true;
            Status = "ikke hentet";
            KantFarve = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A));
        }
    }

    public string Id { get; }
    public string Navn { get; }
    public string Stoerrelse { get; }
    public string Resume { get; }
    public string Fordele { get; }
    public string Ulemper { get; }
    public string Maerkat { get; }
    public Brush MaerkatFarve { get; }
    public Brush KantFarve { get; }
    public string KnapTekst { get; }
    public bool KnapAktiv { get; }
    public string Status { get; }
}

/// <summary>
/// Motor og model: hvad kører, hvilken version, og hvad kan skiftes ud.
///
/// Skærmen findes, fordi valget af model er et reelt kompromis, brugeren skal
/// kunne træffe selv — den mindste model er ti gange hurtigere og mærkbart
/// dårligere, og en engelsk-only model kan slet ikke dansk. Fordele og ulemper
/// står derfor ved hver enkelt frem for i en vejledning, ingen læser.
///
/// Hentning er det eneste sted i appen, der rører netværket. Det sker aldrig
/// af sig selv: brugeren trykker, ser hvad der hentes og hvorfra, og siger ja.
/// </summary>
public partial class EngineView : UserControl
{
    private readonly Downloader _downloader = new();
    private CancellationTokenSource? _afbryd;

    public EngineView()
    {
        InitializeComponent();
        Opdater();
    }

    private void Opdater()
    {
        var s = WhisperInstall.Locate(AppSettings.Current.PreferredModel);

        MotorNavn.Text = s.WhisperCli is null ? "ikke installeret" : "whisper.cpp";

        // Version hvis vi kender den, ellers datoen paa binaeren. Der staar
        // aldrig "ukendt": et felt, der ikke kan svare, skal stille et andet
        // spoergsmaal, ikke vise sin egen uvidenhed.
        var (label, vaerdi) = s.AgeLine;
        AlderLabel.Text = label;
        MotorVersion.Text = s.WhisperCli is null ? "—" : vaerdi;

        MotorBeregning.Text = s.WhisperCli is null ? "—" : s.Engine;
        ModelNavn.Text = s.ModelFileName ?? "ingen model hentet";

        MotorSti.Text = s.WhisperCli is null
            ? $"Motoren hentes til {WhisperInstall.EngineDirectory}"
            : $"{s.WhisperCli}\nModeller: {WhisperInstall.ModelDirectory}";

        MotorVersion.Foreground = (Brush)FindResource("Tekst");

        var installerede = WhisperInstall.Installed().Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var iBrug = s.ModelFileName;

        VisHint();

        if (FaneSprog.IsChecked == true)
        {
            VisSprogmodeller();
            return;
        }

        ListeOverskrift.Text = "Whisper-modeller";
        ListeUnder.Text = "Den, der lytter optagelsen igennem. Én ad gangen — den valgte bruges til alle transskriptioner.";

        Modeller.ItemsSource = WhisperInstall.Models
            .Select(m => new ModelVisning(m, installerede.Contains(m.Id),
                                          iBrug is not null && m.FileName.Equals(iBrug, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!s.IsComplete)
            Status.Text = s.WhisperCli is null
                ? "Whisper-motoren mangler. Hent en model nedenfor — motoren følger med."
                : "Ingen model hentet endnu. Vælg en nedenfor.";
        else
            Status.Text = "";
    }

    /// <summary>
    /// Sprogmodellerne.
    ///
    /// De var ikke synlige nogen steder i appen. Boksen øverst beskrev dem,
    /// men kunne ikke klikkes, og listen nedenunder viste kun Whisper — så
    /// «hvilken sprogmodel bruger den» var et spørgsmål, brugerfladen ikke
    /// kunne svare på. Nu er boksen en fane.
    /// </summary>
    private void VisSprogmodeller()
    {
        ListeOverskrift.Text = "Sprogmodeller";
        ListeUnder.Text =
            "Den, der laver et udkast ud af den færdige tekst. Laget er frivilligt — " +
            "uden en sprogmodel får du stadig transskriptionen, bare ingen dokumenter.";

        // Hentet = filen ligger i sprogmodel-mappen. Der spørges paa disken
        // frem for i en indstilling: en indstilling kan pege paa en fil, der
        // er slettet, og saa staar der «hentet» om noget, der er vaek.
        var hentede = LlmRunner.InstalledModels()
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var liste = LlmCatalog.Default
            .Concat(LlmCatalog.WithObligations)
            .Concat(LlmCatalog.Rejected)
            .Select(m =>
            {
                var erHentet = hentede.Contains(m.FileName);
                return new ModelVisning(m, erHentet, erHentet && Aktiv(m.FileName));
            })
            .ToList();

        Modeller.ItemsSource = liste;

        Status.Text = hentede.Count == 0
            ? "Der er ingen sprogmodel hentet. Uden en kan appen ikke lave dokumenter — men optagelse og transskription virker uændret."
            : "";
    }

    /// <summary>Er det den sprogmodel, dokumenter faktisk laves med?</summary>
    private static bool Aktiv(string filnavn)
    {
        var foerste = LlmRunner.InstalledModels().FirstOrDefault();
        return foerste is not null &&
               Path.GetFileName(foerste).Equals(filnavn, StringComparison.OrdinalIgnoreCase);
    }

    private void Lag_Klik(object sender, RoutedEventArgs e)
    {
        if (Modeller is null) return;   // Checked fyrer under InitializeComponent
        Opdater();
    }

    /// <summary>
    /// Opfordringen står kun på den boks, man IKKE er på.
    ///
    /// Stod «Klik for at se modellerne →» på den valgte boks, ville den ligne
    /// et link, der skulle give mere at vide — og et klik ville ikke gøre
    /// noget, fordi listen allerede står nedenunder. En opfordring til noget,
    /// der allerede er sket, er en blindgyde.
    /// </summary>
    private void VisHint()
    {
        var paaWhisper = FaneWhisper.IsChecked == true;

        HintWhisper.Text = paaWhisper ? "Vises nedenfor" : "Klik for at se modellerne  →";
        HintSprog.Text = paaWhisper ? "Klik for at se modellerne  →" : "Vises nedenfor";

        HintWhisper.Foreground = (Brush)FindResource(paaWhisper ? "TekstMeget" : "Accent");
        HintSprog.Foreground = (Brush)FindResource(paaWhisper ? "Accent" : "TekstMeget");
    }

    // -------------------------------------------------------------- hentning

    private async void Model_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        var model = WhisperInstall.Model(id);
        if (model is null) return;

        var destination = WhisperInstall.ModelDestination(model);
        var alleredeHentet = WhisperInstall.Installed().Any(m => m.Id == model.Id);

        if (alleredeHentet)
        {
            AppSettings.Current.PreferredModel = model.Id;
            AppSettings.Current.Save();
            Opdater();
            Status.Text = $"{model.Id} er nu den model, transskriptionen bruger.";
            return;
        }

        // Krav 3 fra datagrænsen: en informeret godkendelse. Hvad hentes,
        // hvorfra, hvor meget — og hvad det betyder — F~R der spørges.
        var advarsel = model.SupportsDanish
            ? ""
            : "\n\nADVARSEL: denne model kan KUN engelsk. Bruges den til et dansk møde, kommer der volapyk ud — ikke en fejlmeddelelse.\n";

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Hent {model.Id}?",
            $"Fil: {model.FileName}\n" +
            $"Størrelse: {model.SizeText}\n" +
            $"Hentes fra: huggingface.co\n" +
            $"Gemmes i: {WhisperInstall.ModelDirectory}\n" +
            advarsel +
            "\nDer sendes intet fra din maskine. Appen beder om en navngiven fil og " +
            "modtager den; ingen optagelser, noter eller rettelser forlader pc'en.",
            godkend: $"Hent {model.SizeText}",
            annuller: "Ikke nu",
            slags: model.SupportsDanish ? Dialogs.Slags.Valg : Dialogs.Slags.Pas_paa);

        if (!ja) return;

        await HentAsync(model, destination);
    }

    private async Task HentAsync(WhisperModel model, string destination)
    {
        _afbryd = new CancellationTokenSource();
        Fremdrift.Visibility = Visibility.Visible;
        AfbrydKnap.Visibility = Visibility.Visible;
        var fremdrift = new Progress<DownloadProgress>(p =>
        {
            Fremdrift.Value = p.Percent;
            var mb = p.BytesDone / 1024.0 / 1024.0;
            var ialt = p.BytesTotal / 1024.0 / 1024.0;
            var fart = p.BytesPerSecond / 1024.0 / 1024.0;
            var tilbage = p.Remaining is null ? "" : $" · {p.Remaining.Value:mm\\:ss} tilbage";
            Status.Text = $"Henter {model.Id}: {mb:0} af {ialt:0} MB · {fart:0.0} MB/s{tilbage}";
        });

        try
        {
            await _downloader.DownloadAsync(model.Url, destination, model.Bytes, fremdrift, _afbryd.Token);

            AppSettings.Current.PreferredModel = model.Id;
            AppSettings.Current.Save();

            Status.Text = $"{model.Id} er hentet og er nu den model, transskriptionen bruger.";
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Afbrudt. Intet er hentet færdigt.";
        }
        catch (Exception ex)
        {
            Status.Text = $"Kunne ikke hente: {ex.Message}";
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke hente", $"Hentningen fejlede.\n\n{ex.Message}\n\n" +
                "Er der ingen internetforbindelse, kan du lægge modelfilen manuelt i:\n" +
                WhisperInstall.ModelDirectory, Dialogs.Slags.Pas_paa);
        }
        finally
        {
            Fremdrift.Visibility = Visibility.Collapsed;
            AfbrydKnap.Visibility = Visibility.Collapsed;
            _afbryd?.Dispose();
            _afbryd = null;
            Opdater();
        }
    }

    private void Afbryd_Click(object sender, RoutedEventArgs e) => _afbryd?.Cancel();

    // ------------------------------------------------------------ opdatering
    //
    // «Søg efter opdatering» er fjernet fra skærmen, og opslaget mod github er
    // fjernet med den.
    //
    // Begrundelsen er ikke, at det ikke virkede. En nyere whisper.cpp kan
    // ændre transskriptionen til det bedre ELLER til det værre, og forskellen
    // kan kun ses ved at måle den på en oplæsning med facitliste. Det er ikke
    // noget, en bruger skal opdage midt i et arbejdsår, og det er ikke en
    // beslutning, der kan tages ud fra et versionsnummer.
    //
    // Motoren opdateres, når den er afprøvet, og følger med en ny udgave af
    // appen. Det er samtidig det eneste sted, appen ellers ville have ringet
    // ud — nu gør den det kun ved hentning af motor og modeller.
}

