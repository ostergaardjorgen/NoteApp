using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Files;

public sealed record ArkivVisning(string Navn, string Detaljer);

/// <summary>
/// Placering af filer og sikkerhedskopi.
///
/// Backup ligger her frem for i et script, af samme grund som transskriptionen
/// gør: appen skal kunne installeres af andre, og de skal ikke bede om hjælp
/// til at køre PowerShell for at få en sikkerhedskopi.
///
/// Vælges en destination uden for maskinen, gælder de tre krav fra
/// doc\mine-data.md — aktiv beslutning, godkendelse i selve øjeblikket og en
/// informeret risikoliste. Ikke en advarsel, man kan læse forbi.
/// </summary>
public partial class FilesView : UserControl
{
    public FilesView()
    {
        InitializeComponent();
        Opdater();
    }

    private string Destination =>
        Settings.Current.BackupDestination ?? BackupService.DefaultDestination;

    private void Opdater()
    {
        DataSti.Text = UserDataPaths.Root;

        var optagelser = Directory.Exists(UserDataPaths.Meetings)
            ? Directory.GetDirectories(UserDataPaths.Meetings).Length : 0;
        using (var store = new LearningStore())
            DataIndhold.Text = $"{optagelser} optagelser · {store.TermCount()} ord i ordbogen";

        BackupSti.Text = Destination;

        var seneste = BackupService.Latest(Destination);
        BackupSenest.Text = seneste is null
            ? "Ingen sikkerhedskopi taget endnu."
            : $"Senest {seneste.When:dd/MM/yyyy HH:mm} · {seneste.MegaBytes:0.0} MB";

        var ude = BackupService.LeavesMachine(Destination);
        BackupAdvarsel.Visibility = ude ? Visibility.Visible : Visibility.Collapsed;
        BackupAdvarsel.Text = ude
            ? "Denne mappe ligger uden for maskinen. Arkivet er ikke krypteret, og alle med adgang til den kan åbne det."
            : "";

        TagLydMed.IsChecked = Settings.Current.BackupIncludeAudio;

        // Forskellen mellem med og uden lyd er typisk tre stoerrelsesordener.
        // Den skal staa der, ellers er afkrydsningsfeltet et gaet.
        var (medLyd, udenLyd, lydFiler) = BackupService.Estimate();
        LydStoerrelse.Text = lydFiler == 0
            ? "Der er ingen lydfiler endnu."
            : $"{lydFiler} lydfiler. Med lyd: {medLyd / 1024.0 / 1024.0:0} MB · uden lyd: {udenLyd / 1024.0 / 1024.0:0.0} MB";

        Arkiver.ItemsSource = BackupService.Existing(Destination)
            .Select(a => new ArkivVisning(Path.GetFileName(a.Path),
                                          $"{a.When:dd/MM HH:mm} · {a.MegaBytes:0.0} MB"))
            .ToList();
    }

    // ------------------------------------------------------------ datamappen

    private void SkiftData_Click(object sender, RoutedEventArgs e)
    {
        var valgt = VaelgMappe("Vælg hvor NoteApps filer skal ligge", UserDataPaths.Root);
        if (valgt is null) return;

        var svar = MessageBox.Show(
            $"Flyt dine filer hertil?\n\n{valgt}\n\n" +
            $"Alt i {UserDataPaths.Root} kopieres derover, og det gamle sted ryddes bagefter. " +
            "Optagelser, noter, ordbog og indlærte rettelser følger med.",
            "Flyt datamappen", MessageBoxButton.OKCancel, MessageBoxImage.Question);

        if (svar != MessageBoxResult.OK) return;

        try
        {
            UserDataPaths.SetRoot(valgt);
            Status.Text = $"Dine filer ligger nu i {UserDataPaths.Root}";
            Opdater();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kunne ikke flytte", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AabnData_Click(object sender, RoutedEventArgs e) => Aabn(UserDataPaths.Root);

    // ----------------------------------------------------------------- backup

    private void SkiftBackup_Click(object sender, RoutedEventArgs e)
    {
        var valgt = VaelgMappe("Vælg hvor sikkerhedskopien skal ligge", Destination);
        if (valgt is null) return;

        // De tre krav fra datagraensen: valget er aktivt, godkendelsen sker
        // her og nu, og risikoen staar listet FOER der spoerges.
        if (BackupService.LeavesMachine(valgt))
        {
            var lokale = string.Join(", ", BackupService.LocalDrives());
            var svar = MessageBox.Show(
                "STOP — den mappe ligger uden for denne maskine.\n\n" +
                $"{valgt}\n\n" +
                "Hvad du er ved at beslutte:\n" +
                "  • Arkivet indeholder ALT: mødeoptagelser som lyd, transskriptioner,\n" +
                "    dine noter og ordbogen med indlærte rettelser.\n" +
                "  • Zip-filen er IKKE krypteret — hverken undervejs eller når den ligger der.\n" +
                "  • Alle med adgang til mappen kan åbne den, også administratorer og\n" +
                "    backup af det system, den lander på.\n" +
                "  • Mødedeltagerne har ikke sagt ja til dette.\n" +
                "  • Det kan ikke fortrydes. En kopi, der først er ude, er ude.\n\n" +
                $"Lokale drev lige nu: {lokale}\n\n" +
                "Vil du alligevel bruge den mappe?",
                "Destinationen forlader maskinen", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (svar != MessageBoxResult.OK) return;

            BackupService.Log($"GODKENDT: backupmappe uden for maskinen -> {valgt}");
        }

        Settings.Current.BackupDestination = valgt;
        Settings.Current.Save();
        Status.Text = $"Sikkerhedskopier lægges nu i {valgt}";
        Opdater();
    }

    private void Lyd_Klik(object sender, RoutedEventArgs e)
    {
        Settings.Current.BackupIncludeAudio = TagLydMed.IsChecked == true;
        Settings.Current.Save();
        Opdater();
    }

    private void Koer_Click(object sender, RoutedEventArgs e)
    {
        KoerKnap.IsEnabled = false;
        var medLyd = Settings.Current.BackupIncludeAudio;
        Status.Text = "Tager sikkerhedskopi …";

        try
        {
            var r = BackupService.Run(Destination, includeAudio: medLyd);
            Status.Text = $"Færdig: {r.Files} filer, {r.MegaBytes:0.0} MB " +
                          $"({(medLyd ? "med lyd" : "uden lyd")}) på {r.Elapsed.TotalSeconds:0.0} sek. " +
                          $"{r.Kept} arkiver gemt.";
            Opdater();
        }
        catch (Exception ex)
        {
            Status.Text = "Sikkerhedskopien fejlede.";
            MessageBox.Show(ex.Message, "Kunne ikke tage backup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            KoerKnap.IsEnabled = true;
        }
    }

    // ------------------------------------------------------------------ hjælp

    private string? VaelgMappe(string titel, string start)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = titel,
            InitialDirectory = Directory.Exists(start) ? start : "C:\\"
        };
        return dialog.ShowDialog(Window.GetWindow(this)) == true ? dialog.FolderName : null;
    }

    private static void Aabn(string sti)
    {
        Directory.CreateDirectory(sti);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{sti}\"") { UseShellExecute = true });
    }
}
