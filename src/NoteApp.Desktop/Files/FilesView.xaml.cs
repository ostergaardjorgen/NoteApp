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
        AppSettings.Current.BackupDestination ?? BackupService.DefaultDestination;

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

        TagLydMed.IsChecked = AppSettings.Current.BackupIncludeAudio;

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

        AppSettings.Current.BackupDestination = valgt;
        AppSettings.Current.Save();
        Status.Text = $"Sikkerhedskopier lægges nu i {valgt}";
        Opdater();
    }

    private void Lyd_Klik(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.BackupIncludeAudio = TagLydMed.IsChecked == true;
        AppSettings.Current.Save();
        Opdater();
    }

    private void Koer_Click(object sender, RoutedEventArgs e)
    {
        KoerKnap.IsEnabled = false;
        var medLyd = AppSettings.Current.BackupIncludeAudio;
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

    // ------------------------------------------------------------ gendannelse

    private void Arkiv_Valgt(object sender, SelectionChangedEventArgs e)
    {
        var valgt = Arkiver.SelectedItem as ArkivVisning;
        ProeveKnap.IsEnabled = valgt is not null;
        GendanKnap.IsEnabled = valgt is not null;

        if (valgt is null)
        {
            ValgtArkiv.Text = "Vælg et arkiv for at se, hvad det indeholder.";
            return;
        }

        try
        {
            var i = RestoreService.Inspect(Path.Combine(Destination, valgt.Navn));
            ValgtArkiv.Text = $"{i.Files} filer · {i.MegaBytes:0.0} MB · {i.Summary}";
        }
        catch (Exception ex)
        {
            ValgtArkiv.Text = $"Kan ikke læse arkivet: {ex.Message}";
            ProeveKnap.IsEnabled = false;
            GendanKnap.IsEnabled = false;
        }
    }

    /// <summary>
    /// Prøvekørslen rører ikke dine data. Den findes, fordi en backup, man
    /// aldrig har prøvet at gendanne, er en formodning — og det opdager man
    /// ellers først den dag, det gælder.
    /// </summary>
    private void Proeve_Click(object sender, RoutedEventArgs e)
    {
        if (Arkiver.SelectedItem is not ArkivVisning valgt) return;
        var sti = Path.Combine(Destination, valgt.Navn);

        try
        {
            var i = RestoreService.Inspect(sti);
            var udpakket = RestoreService.TestRestore(sti);

            var svar = MessageBox.Show(
                $"Prøvekørsel gennemført. Dine nuværende data er IKKE rørt.\n\n" +
                $"Arkivet: {valgt.Navn}\n" +
                $"Indhold: {i.Files} filer, {i.MegaBytes:0.0} MB\n" +
                $"         {i.Summary}\n" +
                (i.HasDictionary
                    ? "Ordbogen er med og er en gyldig databasefil.\n"
                    : "BEMÆRK: der er ingen ordbog i arkivet.\n") +
                (i.AudioFiles == 0
                    ? "Der er ingen lyd i arkivet — optagelserne kan ikke afspilles efter en gendannelse.\n"
                    : "") +
                $"\nUdpakket til:\n{udpakket}\n\nÅbn mappen?",
                "Prøvekørsel", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (svar == MessageBoxResult.Yes) Aabn(udpakket);
            Status.Text = $"Prøvekørsel af {valgt.Navn} gennemført.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Prøvekørslen fejlede", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Gendan_Click(object sender, RoutedEventArgs e)
    {
        if (Arkiver.SelectedItem is not ArkivVisning valgt) return;
        var sti = Path.Combine(Destination, valgt.Navn);

        ArchiveContents i;
        try { i = RestoreService.Inspect(sti); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kan ikke læse arkivet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var svar = MessageBox.Show(
            $"Gendan fra {valgt.Navn}?\n\n" +
            $"Fra {i.Created:dd/MM/yyyy HH:mm}\n" +
            $"Indhold: {i.Summary}\n\n" +
            $"Filerne skrives ind i {UserDataPaths.Root} og overskriver dem, der hedder det samme.\n\n" +
            (i.AudioFiles == 0
                ? "Arkivet indeholder ingen lyd. Eksisterende lydfiler bliver liggende — de bliver ikke slettet.\n\n"
                : "") +
            "Der tages automatisk et sikkerhedsarkiv af dine nuværende data først, så du kan fortryde.\n\n" +
            "Gendan nu?",
            "Gendan fra sikkerhedskopi", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

        if (svar != MessageBoxResult.OK) return;

        try
        {
            var r = RestoreService.Restore(sti);

            MessageBox.Show(
                $"{r.FilesWritten} filer gendannet.\n\n" +
                (r.SafetyArchive is null
                    ? "Der blev ikke taget et fortrydelsesarkiv — datamappen var tom, så der var intet at sikre.\n\n"
                    : $"Dine tidligere data ligger som:\n{r.SafetyArchive}\n\n") +
                "LUK OG START APPEN IGEN, så ordbogen genindlæses. Indtil da viser " +
                "appen stadig det, den havde i hukommelsen.",
                "Gendannet", MessageBoxButton.OK, MessageBoxImage.Information);

            Status.Text = $"Gendannet fra {valgt.Navn}. Genstart appen.";
            Opdater();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Gendannelsen fejlede", MessageBoxButton.OK, MessageBoxImage.Warning);
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
