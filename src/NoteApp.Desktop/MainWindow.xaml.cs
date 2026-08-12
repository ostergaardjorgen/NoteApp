using System.ComponentModel;
using System.Reflection;
using System.Windows;
using NoteApp.Core;
using NoteApp.Desktop.Dictionary;
using NoteApp.Desktop.Engine;
using NoteApp.Desktop.Files;
using NoteApp.Desktop.Preferences;
using NoteApp.Desktop.ReadAloud;
using NoteApp.Desktop.Templates;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop;

public partial class MainWindow : Window
{
    private readonly ReadAloudView _oplaesning = new();
    private DictionaryView? _ordbog;

    /// <summary>Optagelsen, Optagelser-skærmen skal åbne på. Bruges én gang.</summary>
    private string? _aabnOptagelse;

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

        Indhold.Content = _oplaesning;
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
        if (!_oplaesning.StopHvisIGang()) e.Cancel = true;
        base.OnClosing(e);
    }
}
