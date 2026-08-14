using System.Windows;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Datamappen skal findes, før noget forsøger at skrive i den. Værnet
        // mod at den peger ind i kode-repoet kører her, ved opstart, frem for
        // at fejle stille den dag en optagelse havner i et git-checkout.
        UserDataPaths.EnsureCreated();

        DispatcherUnhandledException += VisFejl;

        // Første start: lad brugeren sætte fagområde og navne, før det første
        // møde. En tom ordbog gør de første transskriptioner dårligere end
        // nødvendigt, og det er ikke noget, man opdager — man tror bare, at
        // værktøjet ikke duer.
        if (!AppSettings.Current.SetupCompleted)
        {
            var opsaetning = new Setup.SetupWindow();
            opsaetning.ShowDialog();
        }

        // --minimeret: startet af Windows ved login. Så skal der ikke poppe et
        // vindue op midt i det, man var i gang med — appen ligger bare klar,
        // så genvejstasten virker. Vinduet kommer frem, når man trykker.
        if (e.Args.Any(a => a.Equals("--minimeret", StringComparison.OrdinalIgnoreCase)))
            _startMinimeret = true;
    }

    private bool _startMinimeret;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (!_startMinimeret || MainWindow is null) return;
        _startMinimeret = false;

        MainWindow.WindowState = WindowState.Minimized;
        MainWindow.ShowInTaskbar = true;
    }

    /// <summary>
    /// En optagelse i gang må ikke gå tabt, fordi UI'et fejlede. Vis fejlen,
    /// og lad appen blive stående, så brugeren selv kan stoppe optagelsen.
    /// </summary>
    private void VisFejl(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Hovedvinduet som ejer, ikke «this» — App er ikke et vindue. Findes
        // det ikke endnu (fejl under opstart), staar dialogen for sig selv.
        Dialogs.AppDialog.Vis(MainWindow, "Der gik noget galt",
            $"{e.Exception.Message}\n\n{e.Exception.GetType().Name}", Dialogs.Slags.Fejl);

        e.Handled = true;
    }
}
