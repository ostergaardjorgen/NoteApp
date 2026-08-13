using System.ComponentModel;
using System.Reflection;
using System.Windows;
using NoteApp.Core;
using NoteApp.Desktop.Dictionary;
using NoteApp.Desktop.Engine;
using NoteApp.Desktop.Documents;
using NoteApp.Desktop.Files;
using NoteApp.Desktop.Jobs;
using NoteApp.Desktop.Meeting;
using NoteApp.Desktop.Preferences;
using NoteApp.Desktop.ReadAloud;
using NoteApp.Desktop.Templates;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop;

public partial class MainWindow : Window
{
    private readonly MeetingView _moede = new();
    private readonly ReadAloudView _oplaesning = new();
    private readonly GlobalHotkey _genvej = new();
    private DictionaryView? _ordbog;

    /// <summary>Optagelsen, Optagelser-skærmen skal åbne på. Bruges én gang.</summary>
    private string? _aabnOptagelse;

    /// <summary>Dokumentet, Dokumenter-skærmen skal åbne på. Bruges én gang.</summary>
    private string? _aabnDokument;

    /// <summary>Id på det dokument, en kørsel netop har lavet.</summary>
    private string? _færdigtDokument;

    // ------------------------------------------------------------ kørselsbjælken

    /// <summary>
    /// Viser, hvad der kører. Bjælken bliver stående, når kørslen er færdig,
    /// indtil man selv skjuler den eller går hen og ser dokumentet.
    ///
    /// Det er hele pointen: en kvittering, der forsvinder af sig selv, når man
    /// står et andet sted i appen, er ikke en kvittering.
    /// </summary>
    private void VisJob(JobStatus s)
    {
        Dispatcher.Invoke(() =>
        {
            JobBjaelke.Visibility = Visibility.Visible;
            JobHvad.Text = s.Kører ? $"{s.Hvad} laves …" : s.Hvad + " færdigt";
            JobBesked.Text = s.Besked;

            JobPrik.Fill = (System.Windows.Media.Brush)FindResource(s.Kører ? "Accent" : "Godkendt");
            JobAfbrydKnap.Visibility = s.Kører ? Visibility.Visible : Visibility.Collapsed;
            JobLukKnap.Visibility = s.Kører ? Visibility.Collapsed : Visibility.Visible;
            JobVisKnap.Visibility = !s.Kører && _færdigtDokument is not null
                ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void JobVis_Click(object sender, RoutedEventArgs e)
    {
        _aabnDokument = _færdigtDokument;
        JobBjaelke.Visibility = Visibility.Collapsed;

        if (NavDokumenter.IsChecked == true) Nav_Changed(this, new RoutedEventArgs());
        else NavDokumenter.IsChecked = true;
    }

    private void JobLuk_Click(object sender, RoutedEventArgs e) =>
        JobBjaelke.Visibility = Visibility.Collapsed;

    private void JobAfbryd_Click(object sender, RoutedEventArgs e)
    {
        var svar = MessageBox.Show(
            "Afbryd kørslen?\n\nDer bliver ikke gemt noget dokument, og arbejdet skal gøres om.",
            "Afbryd", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (svar == MessageBoxResult.Yes) BackgroundJobs.Afbryd();
    }

    public MainWindow()
    {
        InitializeComponent();

        // Versionen paa appen kender vi — den staar i assemblyen. Det er kun
        // Whispers version, vi ikke kan aflaese; se WhisperInstall.
        Version.Text = "v" + (Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(2) ?? "0.0");
        DataSti.Text = UserDataPaths.Root;

        // Efter en oplæsning peger kvitteringen videre til transskription.
        // Skærmskiftet skal ske her, fordi det er MainWindow, der ejer
        // navigationen — og fordi menupunktet skal markeres med, ellers
        // skifter indholdet uden at menuen følger med.
        //
        // Stien gemmes, så Optagelser-skærmen kan åbne PÅ den optagelse, der
        // lige er lavet, og spørge, om den skal skrives ud.
        _oplaesning.TranskriptionØnskes += sti =>
        {
            _aabnOptagelse = sti;
            if (NavTransskriber.IsChecked == true) Nav_Changed(this, new RoutedEventArgs());
            else NavTransskriber.IsChecked = true;
        };

        // Efter et møde peger appen samme vej som efter en oplæsning: hen til
        // optagelsen, med spørgsmålet om den skal skrives ud.
        _moede.FærdigMedMøde += sti =>
        {
            _aabnOptagelse = sti;
            NavTransskriber.IsChecked = true;
        };

        // Kørsler, der ikke hører til nogen skærm. Bjælken nederst er det
        // eneste sted, de kan ses fra — derfor bor den i vinduet og ikke i en
        // skærm, der bliver bygget om, hver gang man skifter menupunkt.
        BackgroundJobs.Ændret += VisJob;
        BackgroundJobs.DokumentFærdigt += id => _færdigtDokument = id;

        // Optagebjælken ligger fast øverst og er den samme, uanset hvilken
        // skærm der vises. Den bygges én gang og bliver siddende — en optagelse
        // må aldrig kunne dø af, at man klikker på et menupunkt.
        OptagBjaelke.Content = _moede;

        // Genvejstasten kobles på, når vinduet findes. Virker den ikke, skal
        // det siges — en genvej, der stille er død, opdages først den dag, man
        // trykker på den under et møde.
        Loaded += (_, _) =>
        {
            _genvej.Trykket += LynstartOptagelse;
            TilslutGenvej();
        };
    }

    /// <summary>
    /// Registrerer genvejen og fortæller mødeskærmen, hvad der blev til noget.
    /// Kaldes igen, når valget ændres under Indstillinger.
    /// </summary>
    public void TilslutGenvej()
    {
        var ok = _genvej.Tilslut(this, AppSettings.Current.HotkeyId);
        _moede.VisGenvej(ok ? _genvej.Aktiv!.Navn : null, _genvej.Bemærkning);

        // Blev der valgt en anden end den oenskede, gemmes den. Ellers ville
        // appen proeve den optagede igen ved hver opstart og skifte hver gang.
        if (ok && _genvej.Aktiv!.Id != AppSettings.Current.HotkeyId)
        {
            AppSettings.Current.HotkeyId = _genvej.Aktiv.Id;
            AppSettings.Current.Save();
        }
    }

    /// <summary>
    /// Genvejstasten er trykket. Vinduet hentes frem, og optagelsen går i gang
    /// med det samme — man skal ikke først finde den rigtige skærm.
    /// </summary>
    private void LynstartOptagelse()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();

        _moede.Lynstart();
    }

    private void Nav_Changed(object sender, RoutedEventArgs e)
    {
        // Konstruktøren kører før felterne er sat op; RadioButton.Checked
        // fyrer under InitializeComponent.
        if (Indhold is null) return;

        // Skærmene bygges først, når de vises. Ordbogen åbner en
        // databaseforbindelse, og motorskærmen leder efter filer på disken —
        // ingen af delene skal ske, mens man bare vil optage.
        if (NavOrdbog.IsChecked == true)
        {
            _ordbog ??= new DictionaryView();
            Indhold.Content = _ordbog;
        }
        else if (NavTransskriber.IsChecked == true)
        {
            // Bygges hver gang: listen over optagelser skal vise den, der
            // netop er lavet, uden at nogen skal genstarte appen.
            var aabn = _aabnOptagelse;
            _aabnOptagelse = null;          // gælder kun dette skift

            Indhold.Content = new TranscribeView(aabn);
        }
        else if (NavDokumenter.IsChecked == true)
        {
            var id = _aabnDokument;
            _aabnDokument = null;
            Indhold.Content = new DocumentsView(id);
        }
        else if (NavSkabeloner.IsChecked == true)
        {
            // Bygges hver gang: skabelonerne er filer, og de kan være rettet i
            // en editor siden sidst.
            Indhold.Content = new TemplatesView();
        }
        else if (NavMotor.IsChecked == true)
        {
            Indhold.Content = new EngineView();
        }
        else if (NavFiler.IsChecked == true)
        {
            Indhold.Content = new FilesView();
        }
        else if (NavIndstillinger.IsChecked == true)
        {
            // Bygges hver gang: enhedslisten skal vise det, der er tilsluttet
            // NU, ikke da appen startede.
            Indhold.Content = new SettingsView();
        }
        else
        {
            Indhold.Content = _oplaesning;
        }
    }

    /// <summary>
    /// En optagelse i gang må ikke tabes, fordi vinduet lukkes. Det er
    /// 20 minutters oplæsning, og der er ingen fortrydelse.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_moede.StopHvisIGang() || !_oplaesning.StopHvisIGang()) { e.Cancel = true; return; }

        _genvej.Dispose();
        base.OnClosing(e);
    }
}
