using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.Transcribe;

public sealed class OptagelseVisning
{
    public OptagelseVisning(string mappe)
    {
        Mappe = mappe;
        var meta = MeetingStore.Load(mappe);
        var wav = Path.Combine(mappe, "mikrofon.wav");

        Titel = meta?.Title ?? Path.GetFileName(mappe);
        Sekunder = File.Exists(wav) ? Transcriber.WavSeconds(wav) : 0;

        // Konsol-optagerens meeting.json har et andet skema, saa StartedAt
        // bliver default og datoen ville staa som 01-01. Mappens tidsstempel
        // er saa det eneste rigtige svar.
        var start = meta?.StartedAt ?? default;
        var dato = start == default
            ? Directory.GetLastWriteTime(mappe)
            : start.LocalDateTime;

        var længde = TimeSpan.FromSeconds(Sekunder);
        Detaljer = $"{dato:dd/MM HH:mm} · {længde:mm\\:ss}";

        HarLyd = File.Exists(wav) && Sekunder > 0;
        if (!HarLyd) Detaljer += " · ingen lyd";

        try
        {
            Bytes = Directory.EnumerateFiles(mappe, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch (IOException) { Bytes = 0; }
    }

    public string Mappe { get; }
    public string Titel { get; }
    public string Detaljer { get; }
    public double Sekunder { get; }
    public bool HarLyd { get; }
    public long Bytes { get; }

    public double MegaBytes => Bytes / 1024.0 / 1024.0;
}

/// <summary>
/// Transskription inde i appen.
///
/// Denne skærm findes, fordi trinnet før den lå i et PowerShell-script. En app,
/// andre kan installere, kan ikke bede folk køre et script bagefter — og
/// målingen af realtidsfaktoren er ikke en udvikleroplysning, men det tal der
/// afgør, om transskription er noget man venter på eller planlægger som natjob.
/// </summary>
public partial class TranscribeView : UserControl
{
    private CancellationTokenSource? _afbryd;
    private string? _sidsteMappe;

    public TranscribeView()
    {
        InitializeComponent();
        IndlaesOptagelser();
    }

    private void IndlaesOptagelser()
    {
        var mapper = new List<string>();

        // Bade appens egne optagelser og repoets fase0-mappe. En bruger uden
        // repoet ser bare den foerste; en med begge skal ikke gaette hvor
        // optagelsen fra i formiddags ligger.
        foreach (var rod in new[] { UserDataPaths.Meetings, Path.Combine("C:", "NoteApp", "fase0", "optagelser") })
        {
            if (!Directory.Exists(rod)) continue;
            mapper.AddRange(Directory.EnumerateDirectories(rod));
        }

        Optagelser.ItemsSource = mapper
            .Select(m => new OptagelseVisning(m))
            .OrderByDescending(o => Directory.GetLastWriteTime(o.Mappe))
            .ToList();

        if (Optagelser.Items.Count == 0)
            Status.Text = "Ingen optagelser endnu. Læs teksten op på skærmen Oplæsning først.";
    }

    private void Optagelse_Valgt(object sender, SelectionChangedEventArgs e)
    {
        var valgt = Optagelser.SelectedItem as OptagelseVisning;
        KoerKnap.IsEnabled = valgt?.HarLyd == true && _afbryd is null;
        AabnKnap.IsEnabled = valgt is not null;
        SletKnap.IsEnabled = valgt is not null && _afbryd is null;

        if (valgt is { HarLyd: false })
            Status.Text = "Den optagelse har ingen lydfil — der er intet at transskribere.";
        else if (valgt is not null)
            Status.Text = "";
    }

    private async void Koer_Click(object sender, RoutedEventArgs e)
    {
        if (Optagelser.SelectedItem is not OptagelseVisning valgt) return;

        var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
        if (!install.IsComplete)
        {
            MessageBox.Show(
                install.WhisperCli is null
                    ? "Whisper-motoren er ikke installeret endnu."
                    : "Der er ingen model hentet endnu.\n\nGå til Motor og model og hent en.",
                "Mangler motor eller model", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var wav = Path.Combine(valgt.Mappe, "mikrofon.wav");

        using var store = new LearningStore();
        var prompt = store.BuildWhisperPrompt();

        var modelNavn = Path.GetFileNameWithoutExtension(install.ModelPath!).Replace("ggml-", "");
        var udBase = Path.Combine(valgt.Mappe, $"mikrofon_{modelNavn}");

        _afbryd = new CancellationTokenSource();
        KoerKnap.IsEnabled = false;
        AfbrydKnap.Visibility = Visibility.Visible;
        Fremdrift.Visibility = Visibility.Visible;
        Fremdrift.Value = 0;
        Maalinger.Visibility = Visibility.Collapsed;
        Resultat.Text = "";

        var fremdrift = new Progress<TranscriptionProgress>(p =>
        {
            Fremdrift.Value = p.Percent;
            Status.Text = $"{p.Message}   ({modelNavn}, {install.Engine})";
        });

        try
        {
            var motor = new Transcriber(install.WhisperCli!);
            var r = await motor.RunAsync(
                new TranscriptionRequest(wav, install.ModelPath!, udBase, "da", prompt),
                fremdrift, _afbryd.Token);

            VisResultat(r);
            _sidsteMappe = valgt.Mappe;
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Afbrudt.";
        }
        catch (Exception ex)
        {
            Status.Text = "Transskriptionen fejlede.";
            MessageBox.Show(ex.Message, "Fejl", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Fremdrift.Visibility = Visibility.Collapsed;
            AfbrydKnap.Visibility = Visibility.Collapsed;
            _afbryd?.Dispose();
            _afbryd = null;
            KoerKnap.IsEnabled = Optagelser.SelectedItem is OptagelseVisning { HarLyd: true };
        }
    }

    private void VisResultat(TranscriptionResult r)
    {
        var tekst = r.Text.Trim();
        var ord = tekst.Split(' ', '\n', '\r').Count(s => s.Length > 0);

        TalLyd.Text = TimeSpan.FromSeconds(r.AudioSeconds).ToString(@"mm\:ss");
        TalTid.Text = TimeSpan.FromSeconds(r.ElapsedSeconds).ToString(@"mm\:ss");
        TalRtf.Text = r.RealTimeFactor.ToString("0.00");
        TalOrd.Text = ord.ToString();

        // Over 1,0 betyder, at transskriptionen tager laengere tid end moedet
        // varede. Det aendrer, hvordan appen skal se ud: et natjob frem for
        // noget man venter paa. Derfor staar tallet stort og forklaret.
        var langsom = r.RealTimeFactor > 1.0;
        TalRtf.Foreground = langsom ? (Brush)FindResource("Advarsel") : (Brush)FindResource("Godkendt");
        RtfForklaring.Text = langsom
            ? $"Over 1,0: transskriptionen tog længere tid end lyden varer. Et 90-minutters møde ville tage cirka {r.RealTimeFactor * 90:0} minutter — planlæg det som natjob."
            : $"Under 1,0: hurtigere end realtid. Et 90-minutters møde ville tage cirka {r.RealTimeFactor * 90:0} minutter.";

        Maalinger.Visibility = Visibility.Visible;
        Resultat.Text = tekst.Length == 0 ? "(tom transskription — var der lyd på optagelsen?)" : tekst;
        Resultat.Foreground = (Brush)FindResource("Tekst");

        Status.Text = $"Færdig · {r.EngineId} · gemt som {Path.GetFileName(r.TextPath)}";
        AabnKnap.IsEnabled = true;
    }

    /// <summary>
    /// Sletter en optagelse med alt, hvad der hører til den.
    ///
    /// Dialogen lister, hvad der forsvinder, og hvor meget det fylder. En
    /// optagelse kan ikke laves om — mødet er holdt — så det er ikke nok at
    /// spørge "er du sikker?"; man skal kunne se, om det er den rigtige.
    /// Der er ingen papirkurv i appen: filen ligger i din egen datamappe, og
    /// en skjult kopi ville bare være data, du ikke vidste du havde.
    /// </summary>
    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (Optagelser.SelectedItem is not OptagelseVisning valgt) return;

        var transskriptioner = Directory.Exists(valgt.Mappe)
            ? Directory.GetFiles(valgt.Mappe, "*.txt").Length
            : 0;
        var noter = File.Exists(Path.Combine(valgt.Mappe, "notes.jsonl"));

        var hvad = new List<string>();
        if (valgt.HarLyd) hvad.Add($"lyden ({TimeSpan.FromSeconds(valgt.Sekunder):mm\\:ss})");
        if (transskriptioner > 0) hvad.Add($"{transskriptioner} transskriptioner");
        if (noter) hvad.Add("noter og blokmærker");

        var svar = MessageBox.Show(
            $"Slet «{valgt.Titel}»?\n\n" +
            $"{valgt.Detaljer}\n" +
            $"Fylder: {valgt.MegaBytes:0.0} MB\n" +
            $"Mappe : {valgt.Mappe}\n\n" +
            (hvad.Count > 0 ? $"Følgende slettes: {string.Join(", ", hvad)}.\n\n" : "") +
            "Det kan ikke fortrydes. Mødet kan ikke optages om.\n\n" +
            "Ligger optagelsen i en sikkerhedskopi, findes den stadig der — men " +
            "backup uden lyd indeholder kun teksten.",
            "Slet optagelse", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (svar != MessageBoxResult.Yes) return;

        try
        {
            Directory.Delete(valgt.Mappe, recursive: true);
            Status.Text = $"«{valgt.Titel}» er slettet ({valgt.MegaBytes:0.0} MB frigjort).";
            _sidsteMappe = null;
            IndlaesOptagelser();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Kunne ikke slette:\n\n{ex.Message}\n\n" +
                "Er filen åben i et andet program, så luk det og prøv igen.",
                "Sletning fejlede", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Afbryd_Click(object sender, RoutedEventArgs e) => _afbryd?.Cancel();

    private void Aabn_Click(object sender, RoutedEventArgs e)
    {
        var mappe = _sidsteMappe ?? (Optagelser.SelectedItem as OptagelseVisning)?.Mappe;
        if (mappe is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{mappe}\"") { UseShellExecute = true });
    }
}
