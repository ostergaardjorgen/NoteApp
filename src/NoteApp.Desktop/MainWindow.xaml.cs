using System.ComponentModel;
using System.Reflection;
using System.Windows;
using NoteApp.Core;
using NoteApp.Desktop.Dictionary;
using NoteApp.Desktop.ReadAloud;

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

        if (NavOrdbog.IsChecked == true)
        {
            // Ordbogen åbner først en databaseforbindelse, når den vises.
            _ordbog ??= new DictionaryView();
            Indhold.Content = _ordbog;
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
