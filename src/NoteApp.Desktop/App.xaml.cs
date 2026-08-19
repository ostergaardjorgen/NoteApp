using System.Windows;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // KUN EEN AD GANGEN MOD SAMME DATAMAPPE.
        //
        // Skal staa foer alt andet: kommer vi hertil som nummer to, skal der
        // ikke oprettes mapper, laeses indstillinger eller vises et
        // opsaetningsvindue. Der skal lukkes ned, og den koerende skal frem.
        if (!Enkeltinstans.ErFoerste(KomFrem))
        {
            Shutdown();
            return;
        }

        // Datamappen skal findes, før noget forsøger at skrive i den. Værnet
        // mod at den peger ind i kode-repoet kører her, ved opstart, frem for
        // at fejle stille den dag en optagelse havner i et git-checkout.
        UserDataPaths.EnsureCreated();

        DispatcherUnhandledException += VisFejl;

        // Foerste start: hvor filerne skal ligge, og hentning af Whisper.
        // Uden motoren kan appen optage, men ikke skrive ud - og det opdager
        // man foerst efter det foerste moede, hvis der ikke spoerges her.
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

    /// <summary>
    /// Henter vinduet frem, når nogen forsøger at starte appen igen.
    ///
    /// Kaldes fra en baggrundstråd, så alt skal over på UI-tråden først.
    /// Var vinduet minimeret — fx startet med Windows — skal det også ud af
    /// den tilstand; ellers blinker det bare i proceslinjen, og så ligner
    /// spærren, at der ikke skete noget.
    /// </summary>
    private void KomFrem() => Dispatcher.Invoke(() =>
    {
        if (MainWindow is not { } v) return;

        if (v.WindowState == WindowState.Minimized)
            v.WindowState = WindowState.Normal;

        v.Show();
        v.Activate();
    });

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
