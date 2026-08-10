using System.ComponentModel;
using System.Reflection;
using System.Windows;
using NoteApp.Core;
using NoteApp.Desktop.Dictionary;
using NoteApp.Desktop.Engine;
using NoteApp.Desktop.Files;
using NoteApp.Desktop.ReadAloud;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop;

public partial class MainWindow : Window
{
    private readonly ReadAloudView _oplaesning = new();
    private DictionaryView? _ordbog;

    public MainWindow()
    {
        InitializeComponent();

        Version.Text = "v" + (Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(2) ?? "0.11");
        DataSti.Text = UserDataPaths.Root;

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
            Indhold.Content = new TranscribeView();
        }
        else if (NavMotor.IsChecked == true)
        {
            Indhold.Content = new EngineView();
        }
        else if (NavFiler.IsChecked == true)
        {
            Indhold.Content = new FilesView();
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
