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

    public TranscribeView() : this(null) { }

    /// <summary>
    /// <paramref name="aabnMappe"/> er den optagelse, skærmen skal åbne på —
    /// sat, når man kommer hertil fra kvitteringen efter en oplæsning.
    ///
    /// Uden den landede man på listen over alle optagelser og skulle selv
    /// finde den, man lige havde lavet. Det er ikke «videre», det er «start
    /// forfra et andet sted».
    /// </summary>
    public TranscribeView(string? aabnMappe)
    {
        InitializeComponent();
        IndlaesOptagelser();

        if (aabnMappe is null) return;

        var match = Optagelser.Items.Cast<OptagelseVisning>()
            .FirstOrDefault(o => string.Equals(o.Mappe.TrimEnd('\\'), aabnMappe.TrimEnd('\\'),
                                               StringComparison.OrdinalIgnoreCase));
        if (match is null) return;

        Optagelser.SelectedItem = match;
        Optagelser.ScrollIntoView(match);

        // Spoerg foerst, naar vinduet er tegnet. En dialog fra en konstruktoer
        // aabner over en halvfaerdig skaerm, og saa kan man ikke se, hvad man
        // siger ja til.
        Loaded += (_, _) => SpoergOmStart(match);
    }

    private bool _harSpurgt;

    private void SpoergOmStart(OptagelseVisning optagelse)
    {
        if (_harSpurgt) return;
        _harSpurgt = true;

        if (!optagelse.HarLyd) return;

        var minutter = optagelse.Sekunder / 60.0;
        var svar = MessageBox.Show(
            $"Skriv «{optagelse.Titel}» ud til tekst nu?\n\n" +
            $"Længde: {TimeSpan.FromSeconds(optagelse.Sekunder):mm\\:ss}\n" +
            $"Det tager typisk {Math.Max(1, Math.Round(minutter * 0.3)):0} til {Math.Max(2, Math.Round(minutter * 0.5)):0} minutter " +
            "på denne maskine.\n\n" +
            "Du kan roligt lave noget andet imens — også optage et nyt møde.",
            "Klar til at skrive ud", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (svar == MessageBoxResult.Yes) Koer_Click(this, new RoutedEventArgs());
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
        {
            Status.Text = "Ingen optagelser endnu.";
            ForklaringOverskrift.Text = "Der er ingen optagelser endnu";
            ForklaringUnder.Text =
                "Gå til «Start her» og læs en af teksterne op. Så har du en optagelse, du kan skrive ud til tekst her.";
        }
    }

    private void Optagelse_Valgt(object sender, SelectionChangedEventArgs e)
    {
        var valgt = Optagelser.SelectedItem as OptagelseVisning;
        KoerKnap.IsEnabled = valgt?.HarLyd == true && _afbryd is null;
        AabnKnap.IsEnabled = valgt is not null;
        SletKnap.IsEnabled = valgt is not null && _afbryd is null;

        if (_afbryd is not null) return;   // der koeres — forklaringen staar om det

        // Findes teksten allerede, vises den frem for forklaringen. Det er den,
        // man er kommet efter, naar optagelsen er skrevet ud een gang.
        var færdig = valgt is null ? null : FindTekst(valgt.Mappe);
        if (færdig is not null)
        {
            Forklaring.Visibility = Visibility.Collapsed;
            ResultatRude.Visibility = Visibility.Visible;
            Resultat.Text = File.ReadAllText(færdig, System.Text.Encoding.UTF8).Trim();
            Resultat.Foreground = (Brush)FindResource("Tekst");
            Status.Text = "Skrevet ud tidligere. Tryk «Transskribér» for at gøre det igen.";
            return;
        }

        Forklaring.Visibility = Visibility.Visible;
        ResultatRude.Visibility = Visibility.Collapsed;
        Maalinger.Visibility = Visibility.Collapsed;

        if (valgt is { HarLyd: false })
        {
            ForklaringOverskrift.Text = "Den optagelse har ingen lyd";
            ForklaringUnder.Text =
                "Der er ingen lydfil i mappen, så der er intet at skrive ud. Vælg en anden optagelse i listen.";
            Status.Text = "Den optagelse har ingen lydfil — der er intet at transskribere.";
            return;
        }

        ForklaringOverskrift.Text = "Fra lyd til tekst";
        ForklaringUnder.Text = valgt is null
            ? "Vælg en optagelse i listen til venstre og tryk «Transskribér» nederst til højre. Så skriver appen alt det talte ud som tekst, du kan læse, søge i og rette."
            : $"«{valgt.Titel}» er klar. Tryk «Transskribér» nederst til højre, så skriver appen alt det talte ud som tekst, du kan læse, søge i og rette.";
        Status.Text = "";
    }

    /// <summary>Den nyeste udskrevne tekst i mappen, hvis der er en.</summary>
    private static string? FindTekst(string mappe) =>
        Directory.Exists(mappe)
            ? Directory.GetFiles(mappe, "*.txt").OrderByDescending(File.GetLastWriteTime).FirstOrDefault()
            : null;

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

        // Forklaringen bliver staaende, mens der koeres. Det er praecis dér,
        // den er noget vaerd: den svarer paa "hvor lang tid tager det" og
        // "maa jeg lave noget andet imens".
        Forklaring.Visibility = Visibility.Visible;
        ResultatRude.Visibility = Visibility.Collapsed;
        ForklaringOverskrift.Text = "Skriver lyden ud …";
        ForklaringUnder.Text =
            "Fremdriften står nederst. Teksten dukker op her, når den er færdig, og bliver gemt automatisk.";

        var fremdrift = new Progress<TranscriptionProgress>(p =>
        {
            Fremdrift.Value = p.Percent;
            Status.Text = $"{p.Message}   ({modelNavn}, {install.Engine})";
        });

        try
        {
            var motor = new Transcriber(install.WhisperCli!);
            var r = await motor.RunAsync(
                // "auto": Whisper finder selv sproget. Møder holdes ikke altid
                // på dansk, og et engelsk møde tvunget gennem dansk giver
                // volapyk frem for en fejl — og volapyk ligner et resultat.
                new TranscriptionRequest(wav, install.ModelPath!, udBase, "auto", prompt),
                fremdrift, _afbryd.Token);

            // Efterretning: de fejl, du allerede har rettet én gang, rettes nu
            // af sig selv. Det er DEN vej, appen lærer — ordlisten i Whispers
            // initial_prompt er målt til ingen forskel at gøre.
            IReadOnlyList<AppliedCorrection> rettelser = Array.Empty<AppliedCorrection>();
            try
            {
                using var ordbog = new LearningStore();
                rettelser = TranscriptCorrector.FromStore(ordbog).ApplyToFile(r.TextPath);
            }
            catch (Exception)
            {
                // En fejl i efterretningen maa ikke koste transskriptionen.
                // Den raa tekst ligger der, og den er det vaesentlige.
            }

            VisResultat(r, rettelser);
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

    private void VisResultat(TranscriptionResult r, IReadOnlyList<AppliedCorrection>? rettelser = null)
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
        Forklaring.Visibility = Visibility.Collapsed;
        ResultatRude.Visibility = Visibility.Visible;
        Resultat.Text = tekst.Length == 0 ? "(tom transskription — var der lyd på optagelsen?)" : tekst;
        Resultat.Foreground = (Brush)FindResource("Tekst");

        // Sproget staar i statuslinjen, fordi det er den oplysning, der
        // forklarer en tekst, der ser forkert ud. Er detekteringen usikker,
        // skal usikkerheden staa der ogsaa — dansk, norsk og svensk ligner
        // hinanden, og et forkert sprog er ikke til at gennemskue bagefter.
        var sprog = Transcriber.LanguageName(r.DetectedLanguage);
        if (r.LanguageProbability is double p && p < 0.7)
            sprog += $" (usikker, {p * 100:0}%)";

        // Rettelserne skal SES. Bliver teksten lavet om uden at det siges,
        // ved man ikke, hvad man læser — og så kan man heller ikke opdage, at
        // en regel er blevet forkert.
        var rettet = "";
        if (rettelser is { Count: > 0 })
        {
            var antal = rettelser.Sum(x => x.Count);
            rettet = $" · {antal} rettet fra din ordbog";
        }

        Status.Text = $"Færdig · {sprog}{rettet} · {r.EngineId} · gemt som {Path.GetFileName(r.TextPath)}";
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
