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
        if (!Settings.Current.SetupCompleted)
        {
            var opsaetning = new Setup.SetupWindow();
            opsaetning.ShowDialog();
        }
    }

    /// <summary>
    /// En optagelse i gang må ikke gå tabt, fordi UI'et fejlede. Vis fejlen,
    /// og lad appen blive stående, så brugeren selv kan stoppe optagelsen.
    /// </summary>
    private void VisFejl(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"{e.Exception.Message}\n\n{e.Exception.GetType().Name}",
            "Der gik noget galt",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
