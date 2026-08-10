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

        var længde = TimeSpan.FromSeconds(Sekunder);
        var dato = meta?.StartedAt.ToString("dd/MM HH:mm") ?? Directory.GetLastWriteTime(mappe).ToString("dd/MM HH:mm");
        Detaljer = $"{dato} · {længde:mm\\:ss}";

        HarLyd = File.Exists(wav) && Sekunder > 0;
        if (!HarLyd) Detaljer += " · ingen lyd";
    }

    public string Mappe { get; }
    public string Titel { get; }
    public string Detaljer { get; }
    public double Sekunder { get; }
    public bool HarLyd { get; }
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

        if (valgt is { HarLyd: false })
            Status.Text = "Den optagelse har ingen lydfil — der er intet at transskribere.";
        else if (valgt is not null)
            Status.Text = "";
    }

    private async void Koer_Click(object sender, RoutedEventArgs e)
    {
        if (Optagelser.SelectedItem is not OptagelseVisning valgt) return;

        var install = WhisperInstall.Locate(Settings.Current.PreferredModel);
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

    private void Afbryd_Click(object sender, RoutedEventArgs e) => _afbryd?.Cancel();

    private void Aabn_Click(object sender, RoutedEventArgs e)
    {
        var mappe = _sidsteMappe ?? (Optagelser.SelectedItem as OptagelseVisning)?.Mappe;
        if (mappe is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{mappe}\"") { UseShellExecute = true });
    }
}
