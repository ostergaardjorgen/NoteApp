using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;

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
        MotorVersion.Text = s.EngineVersion ?? "ukendt (lagt der i hånden)";
        MotorBeregning.Text = s.WhisperCli is null ? "—" : s.Engine;
        ModelNavn.Text = s.ModelFileName ?? "ingen model hentet";

        MotorSti.Text = s.WhisperCli is null
            ? $"Motoren hentes til {WhisperInstall.EngineDirectory}"
            : $"{s.WhisperCli}\nModeller: {WhisperInstall.ModelDirectory}";

        MotorVersion.Foreground = s.EngineVersion is null
            ? (Brush)FindResource("TekstSvag")
            : (Brush)FindResource("Tekst");

        var installerede = WhisperInstall.Installed().Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var iBrug = s.ModelFileName;

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
        // hvorfra, hvor meget — og hvad det betyder — FØR der spørges.
        var advarsel = model.SupportsDanish
            ? ""
            : "\n\nADVARSEL: denne model kan KUN engelsk. Bruges den til et dansk møde, kommer der volapyk ud — ikke en fejlmeddelelse.\n";

        var svar = MessageBox.Show(
            $"Hent {model.Id}?\n\n" +
            $"Fil     : {model.FileName}\n" +
            $"Størrelse: {model.SizeText}\n" +
            $"Hentes fra: huggingface.co\n" +
            $"Gemmes i : {WhisperInstall.ModelDirectory}\n" +
            advarsel +
            "\nDer sendes intet fra din maskine. Appen beder om en navngiven fil og " +
            "modtager den; ingen optagelser, noter eller ordbog forlader pc'en.\n\n" +
            "Hent nu?",
            "Hent model", MessageBoxButton.OKCancel, MessageBoxImage.Question);

        if (svar != MessageBoxResult.OK) return;

        await HentAsync(model, destination);
    }

    private async Task HentAsync(WhisperModel model, string destination)
    {
        _afbryd = new CancellationTokenSource();
        Fremdrift.Visibility = Visibility.Visible;
        AfbrydKnap.Visibility = Visibility.Visible;
        OpdaterKnap.IsEnabled = false;

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
            MessageBox.Show(
                $"Hentningen fejlede.\n\n{ex.Message}\n\n" +
                "Er der ingen internetforbindelse, kan du lægge modelfilen manuelt i:\n" +
                WhisperInstall.ModelDirectory,
                "Kunne ikke hente", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Fremdrift.Visibility = Visibility.Collapsed;
            AfbrydKnap.Visibility = Visibility.Collapsed;
            OpdaterKnap.IsEnabled = true;
            _afbryd?.Dispose();
            _afbryd = null;
            Opdater();
        }
    }

    private void Afbryd_Click(object sender, RoutedEventArgs e) => _afbryd?.Cancel();

    // ------------------------------------------------------------ opdatering

    private async void Opdater_Click(object sender, RoutedEventArgs e)
    {
        var s = WhisperInstall.Locate();
        OpdaterKnap.IsEnabled = false;
        Status.Text = "Slår op hos github.com …";

        try
        {
            var tjek = await EngineUpdates.CheckAsync(s.EngineVersion);

            if (tjek.LatestVersion is null)
            {
                Status.Text = "Kunne ikke aflæse, hvad den nyeste version er.";
                return;
            }

            if (tjek.InstalledVersionUnknown)
            {
                MessageBox.Show(
                    $"Nyeste whisper.cpp er {tjek.LatestVersion}.\n\n" +
                    "Din motor er lagt på maskinen i hånden, og whisper.cpp stempler ikke sin " +
                    "exe-fil med en version. Appen kan derfor ikke se, hvilken du har — og " +
                    "gætter ikke.\n\n" +
                    "Vil du være sikker på at køre den nyeste, kan du hente den fra:\n" +
                    (tjek.DownloadUrl ?? "github.com/ggerganov/whisper.cpp/releases"),
                    "Version ukendt", MessageBoxButton.OK, MessageBoxImage.Information);
                Status.Text = $"Nyeste er {tjek.LatestVersion}. Din version kendes ikke.";
                return;
            }

            Status.Text = tjek.UpdateAvailable
                ? $"Nyere version findes: {tjek.LatestVersion} (du har {tjek.InstalledVersion})."
                : $"Du kører nyeste version ({tjek.InstalledVersion}).";
        }
        catch (Exception ex)
        {
            Status.Text = $"Kunne ikke søge efter opdatering: {ex.Message}";
        }
        finally
        {
            OpdaterKnap.IsEnabled = true;
        }
    }
}
