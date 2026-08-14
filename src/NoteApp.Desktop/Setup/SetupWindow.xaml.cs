using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Setup;

/// <summary>
/// Opsætning ved første start.
///
/// TO trin: velkomst, og hent motoren. Ikke flere.
///
/// Der var fire. To af dem — «hvad handler dine møder om» og «hvem holder du
/// møder med» — fyldte en ordbog op med fagord og navne, som blev sendt med
/// til Whisper som ledetråd. Det blev målt: forskellen var NUL. To skærme
/// spørgsmål, før man havde set appen, for at fylde noget op, der ikke gjorde
/// nogen forskel.
///
/// Modellen hentes til gengæld HER. Uden den kan appen ikke skrive et eneste
/// møde ud, og det er det eneste, en ny bruger virkelig skal have på plads.
/// </summary>
public partial class SetupWindow : Window
{
    private int _trin;

    private static readonly (string Titel, string Under)[] Trin =
    {
        ("Velkommen til NoteApp", "Møde-noter der bliver på din egen maskine"),
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
        VisTrin(0);
    }

    private void VisTrin(int nr)
    {
        _trin = nr;

        Trin1.Visibility = nr == 0 ? Visibility.Visible : Visibility.Collapsed;
        Trin4.Visibility = nr == 1 ? Visibility.Visible : Visibility.Collapsed;

        TrinTitel.Text = Trin[nr].Titel;
        TrinUnder.Text = Trin[nr].Under;
        TrinTaeller.Text = $"Trin {nr + 1} af {Trin.Length}";

        TilbageKnap.Visibility = nr == 0 ? Visibility.Collapsed : Visibility.Visible;
        NaesteKnap.Content = nr == 0 ? "Kom i gang" : "Hent og afslut";

        if (nr == 1) _ = ForberedHentning();
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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke skifte mappe", ex.Message, Dialogs.Slags.Pas_paa);
        }
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

        // Markeres FOERST. Gaar hentningen galt, eller afbryder brugeren, skal
        // velkomstforloebet ikke komme igen ved naeste start.
        AppSettings.Current.SetupCompleted = true;
        AppSettings.Current.Save();

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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke hente", $"Hentningen blev ikke færdig.\n\n{ex.Message}\n\n" +
                "Opsætningen afsluttes. Du kan hente motor og model senere under «AI-modeller».", Dialogs.Slags.Pas_paa);

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

    private void Afslut(bool visKvittering = true)
    {
        if (visKvittering)
        {
            var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);

            var klar = install.IsComplete
                ? $"Whisper er klar: {install.ModelFileName} på {install.Engine}."
                : "Motor eller model mangler stadig — hent dem under «AI-modeller».";

            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Klar", klar + "\n\n" +
                "Tryk «Optag møde», når dit næste møde begynder — så er du i gang.\n\n" +
                "Vil du vide, hvor godt appen rammer netop din stemme, kan du læse en prøvetekst " +
                "op under «Start her». Det er frivilligt.", Dialogs.Slags.Valg);
        }

        DialogResult = true;
        Close();
    }
}
