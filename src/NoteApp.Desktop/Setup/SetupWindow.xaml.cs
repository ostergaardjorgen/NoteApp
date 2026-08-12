using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Setup;

public sealed class BrancheVisning
{
    public BrancheVisning(IndustryTemplate t)
    {
        Id = t.Id;
        Name = t.Name;
        Description = t.Description;
        AntalTekst = t.Count == 0 ? "" : $"{t.Count} ord";
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string AntalTekst { get; }
}

/// <summary>
/// Opsætning ved første start.
///
/// Tre trin, ikke flere. Formålet er, at appen kender brugerens fagområde og
/// navne, før det første møde — ikke at samle oplysninger. En tom ordbog gør
/// de første møder dårligere end nødvendigt, og navne er dem, Whisper oftest
/// staver forkert.
///
/// Modellen hentes IKKE her. Det sker på skærmen Motor og model, hvor
/// størrelse, fordele og ulemper står ved hvert valg — at trække en 2,9 GB
/// hentning ind i et velkomstforløb ville gøre valget til noget, man klikker
/// sig forbi.
/// </summary>
public partial class SetupWindow : Window
{
    private int _trin;
    private string _branche = "ingen";

    private static readonly (string Titel, string Under)[] Trin =
    {
        ("Velkommen til NoteApp", "Møde-noter der bliver på din egen maskine"),
        ("Hvad handler dine møder om?", "Så starter ordbogen med de rigtige fagord"),
        ("Hvem holder du møder med?", "Navne er dem, Whisper oftest staver forkert"),
        ("Sidste trin: hent Whisper", "Motoren og en sprogmodel, så appen kan skrive dine møder ud")
    };

    private EngineRelease? _udgivelse;
    private EngineBuild? _motorValg;
    private WhisperModel? _modelValg;
    private bool _henter;

    public SetupWindow()
    {
        InitializeComponent();

        DataSti.Text = UserDataPaths.Root;
        Brancher.ItemsSource = IndustryTemplates.All.Select(t => new BrancheVisning(t)).ToList();

        VisTrin(0);
    }

    private void VisTrin(int nr)
    {
        _trin = nr;

        Trin1.Visibility = nr == 0 ? Visibility.Visible : Visibility.Collapsed;
        Trin2.Visibility = nr == 1 ? Visibility.Visible : Visibility.Collapsed;
        Trin3.Visibility = nr == 2 ? Visibility.Visible : Visibility.Collapsed;
        Trin4.Visibility = nr == 3 ? Visibility.Visible : Visibility.Collapsed;

        TrinTitel.Text = Trin[nr].Titel;
        TrinUnder.Text = Trin[nr].Under;
        TrinTaeller.Text = $"Trin {nr + 1} af {Trin.Length}";

        TilbageKnap.Visibility = nr == 0 ? Visibility.Collapsed : Visibility.Visible;
        NaesteKnap.Content = nr switch
        {
            0 => "Kom i gang",
            1 => "Næste",
            2 => "Næste",
            _ => "Hent og afslut"
        };

        if (nr == 3) _ = ForberedHentning();
    }

    // ------------------------------------------------------- motor og model

    /// <summary>
    /// Slår op, hvad der skal hentes, og hvor meget det fylder — FØR brugeren
    /// trykker. Er begge dele der i forvejen, siges det, og trinnet bliver et
    /// klik videre i stedet for en hentning.
    /// </summary>
    private async Task ForberedHentning()
    {
        var installeret = WhisperInstall.Locate();

        // Motoren
        if (installeret.WhisperCli is not null)
        {
            MotorOverskrift.Text = "Motor — allerede på plads";
            MotorValg.Text = installeret.Engine == "GPU (CUDA)"
                ? "whisper.cpp med GPU-understøttelse er fundet på maskinen."
                : "whisper.cpp er fundet på maskinen.";
            MotorBegrundelse.Text = installeret.WhisperCli;
            _motorValg = null;
        }
        else
        {
            MotorValg.Text = "Slår op hos GitHub …";
            try
            {
                _udgivelse = await EngineInstaller.FetchLatestAsync();
                _motorValg = EngineInstaller.Recommend(_udgivelse);

                if (_motorValg is null)
                {
                    MotorValg.Text = "Kunne ikke finde en Windows-udgave at hente.";
                }
                else
                {
                    var harGpu = EngineInstaller.HasNvidiaGpu();
                    MotorOverskrift.Text = $"Motor — whisper.cpp {_udgivelse.Version}";
                    MotorValg.Text = $"{_motorValg.FileName}  ({_motorValg.SizeText})";
                    MotorBegrundelse.Text = harGpu
                        ? "Maskinen har et NVIDIA-kort, så GPU-udgaven vælges. Den er cirka ti gange hurtigere end CPU."
                        : "Der er ikke fundet et NVIDIA-kort, så CPU-udgaven vælges. Den virker overalt, men et langt møde tager længere tid end mødet selv.";
                }
            }
            catch (Exception ex)
            {
                MotorValg.Text = "Kunne ikke nå GitHub.";
                MotorBegrundelse.Text = $"{ex.Message} — du kan hente motoren senere under Motor og model.";
            }
        }

        // Modellen
        if (installeret.ModelPath is not null)
        {
            ModelValg.Text = $"{Path.GetFileName(installeret.ModelPath)} er allerede på maskinen.";
            ModelBegrundelse.Text = installeret.ModelPath;
            _modelValg = null;
        }
        else
        {
            // large-v3 paa en GPU-maskine, ellers den lille. Kvaliteten paa
            // dansk er markant bedre med den store, men uden GPU er den
            // ubrugelig langsom.
            _modelValg = EngineInstaller.HasNvidiaGpu()
                ? WhisperInstall.Model("large-v3")
                : WhisperInstall.Model("small");

            ModelValg.Text = $"{_modelValg!.Id}  ({_modelValg.SizeText})";
            ModelBegrundelse.Text = _modelValg.Summary + " Du kan skifte model senere under Motor og model.";
        }

        var samlet = (_motorValg?.Bytes ?? 0) + (_modelValg?.Bytes ?? 0);
        SamletStoerrelse.Text = samlet == 0
            ? "ingenting — alt er der allerede"
            : $"{samlet / 1024.0 / 1024.0:0} MB";

        NaesteKnap.Content = samlet == 0 ? "Færdig" : "Hent og afslut";
    }

    /// <summary>
    /// Lader brugeren vælge, hvor filerne skal ligge. Sker det her — før det
    /// første møde — er der intet at flytte. Vælges der om senere, flyttes det,
    /// der allerede er.
    /// </summary>
    private void SkiftMappe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Vælg hvor NoteApps filer skal ligge",
            InitialDirectory = Directory.Exists(UserDataPaths.Root)
                ? UserDataPaths.Root
                : Path.GetPathRoot(UserDataPaths.DefaultRoot)!
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            UserDataPaths.SetRoot(dialog.FolderName);
            DataSti.Text = UserDataPaths.Root;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kunne ikke skifte mappe",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Branche_Valgt(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string id }) _branche = id;
    }

    /// <summary>
    /// Pladsholderen forsvinder, så snart der står noget. WPF's TextBox har
    /// ingen indbygget pladsholder, og eksemplet i feltet er her ikke pynt:
    /// det er dét, der fortæller, at der skal skiftes linje mellem hvert navn.
    /// </summary>
    private void Felt_Changed(object sender, TextChangedEventArgs e)
    {
        if (NavnePladsholder is null || FirmaerPladsholder is null) return;

        NavnePladsholder.Visibility = Navne.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        FirmaerPladsholder.Visibility = Firmaer.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Tilbage_Click(object sender, RoutedEventArgs e) => VisTrin(Math.Max(0, _trin - 1));

    private async void Naeste_Click(object sender, RoutedEventArgs e)
    {
        if (_henter) return;

        if (_trin < Trin.Length - 1)
        {
            VisTrin(_trin + 1);
            return;
        }

        // Ordbogen gemmes FOERST. Gaar hentningen galt, eller afbryder
        // brugeren, skal fagomraade og navne ikke vaere tabt.
        GemOrdbog();

        if (_motorValg is not null || _modelValg is not null)
        {
            var ok = await HentAlt();
            if (!ok) return;
        }

        Afslut();
    }

    /// <summary>
    /// Henter motor og model. Fejler noget, siges det — og opsætningen
    /// afsluttes alligevel, så brugeren ikke sidder fast i et velkomstforløb.
    /// Begge dele kan hentes bagefter under Motor og model.
    /// </summary>
    private async Task<bool> HentAlt()
    {
        _henter = true;
        NaesteKnap.IsEnabled = false;
        TilbageKnap.IsEnabled = false;
        HentFremdrift.Visibility = Visibility.Visible;

        var fremdrift = new Progress<DownloadProgress>(p =>
        {
            HentFremdrift.Value = p.Percent;
            var tilbage = p.Remaining is null ? "" : $" · {p.Remaining.Value:mm\\:ss} tilbage";
            HentStatus.Text = $"{p.BytesDone / 1024.0 / 1024.0:0} af {p.BytesTotal / 1024.0 / 1024.0:0} MB" +
                              $" · {p.BytesPerSecond / 1024.0 / 1024.0:0.0} MB/s{tilbage}";
        });

        try
        {
            if (_motorValg is not null && _udgivelse is not null)
            {
                await EngineInstaller.InstallAsync(_motorValg, _udgivelse.Version, fremdrift,
                    new Progress<string>(s => HentStatus.Text = s));
            }

            if (_modelValg is not null)
            {
                HentStatus.Text = $"Henter {_modelValg.Id} …";
                await new Downloader().DownloadAsync(
                    _modelValg.Url, WhisperInstall.ModelDestination(_modelValg), _modelValg.Bytes, fremdrift);

                AppSettings.Current.PreferredModel = _modelValg.Id;
                AppSettings.Current.Save();
            }

            HentStatus.Text = "Færdig.";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Hentningen blev ikke færdig.\n\n{ex.Message}\n\n" +
                "Din ordbog er gemt, og opsætningen afsluttes. Du kan hente motor og model " +
                "senere under «Motor og model».",
                "Kunne ikke hente", MessageBoxButton.OK, MessageBoxImage.Warning);

            Afslut(visKvittering: false);
            return false;
        }
        finally
        {
            _henter = false;
            NaesteKnap.IsEnabled = true;
            TilbageKnap.IsEnabled = true;
            HentFremdrift.Visibility = Visibility.Collapsed;
        }
    }

    private int _fraSkabelon, _navne, _firmaer;

    private void GemOrdbog()
    {
        using var store = new LearningStore();

        var skabelon = IndustryTemplates.ById(_branche);
        _fraSkabelon = skabelon is null ? 0 : IndustryTemplates.Apply(store, skabelon);
        _navne = IndustryTemplates.AddNames(store, Navne.Text);
        _firmaer = IndustryTemplates.AddNames(store, Firmaer.Text, TermCategories.Organisation);

        // Ordlisten skrives med det samme. Ellers ville ordbogen vaere fyldt,
        // men filen Whisper faktisk laeser vaere tom indtil naeste gang nogen
        // huskede at trykke eksportér.
        store.ExportVocabularyFile();

        AppSettings.Current.SetupCompleted = true;
        AppSettings.Current.Industry = _branche;
        AppSettings.Current.Save();
    }

    private void Afslut(bool visKvittering = true)
    {
        if (visKvittering)
        {
            var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);

            var klar = install.IsComplete
                ? $"Whisper er klar: {install.ModelFileName} på {install.Engine}."
                : "Motor eller model mangler stadig — hent dem under «Motor og model».";

            MessageBox.Show(
                $"Ordbogen er sat op med {_fraSkabelon + _navne + _firmaer} ord: " +
                $"{_fraSkabelon} fra skabelonen, {_navne} navne og {_firmaer} firmaer.\n\n" +
                klar + "\n\n" +
                "Første opgave er at læse testteksten højt. Den står klar på skærmen «Oplæsning».",
                "Klar", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        DialogResult = true;
        Close();
    }
}
