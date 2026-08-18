using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Training;

/// <summary>
/// Læs én sætning op igen.
///
/// HVAD DEN SVARER PÅ
///
/// Står der «startdasser», hvor der skulle have stået «startdato», er der to
/// forklaringer, og de kræver hver sin handling: appen hørte forkert (ret det
/// i ordbogen), eller ordet blev sagt utydeligt (så er der ikke noget at
/// rette). Teksten kan ikke skelne. Det kan en oplæsning til.
///
/// HVORFOR RESULTATET IKKE GÅR IND I KARAKTEREN
///
/// Karakteren hviler på, at hele teksten blev læst i ét stykke. Talte en
/// genindtaling med, kunne man læse den samme sætning femten gange og se
/// procenten stige, uden at appen var blevet bedre til noget. Genindtalingen
/// står derfor for sig — se <see cref="Genlaesninger"/>.
///
/// FLOWET
///
/// Sætningen står på skærmen med det samme. Tryk «Optag», læs op, tryk «Gem»
/// — så lukker vinduet, og svaret står på det kort, man trykkede fra. Der er
/// ikke et mellemtrin, hvor man skal godkende noget: målingen er entydig,
/// fordi manuskriptet er facit.
/// </summary>
public partial class ReadAgainWindow : Window
{
    private readonly string _optagelse;
    private readonly int _nummer;
    private readonly string _manuskript;

    private ShortClipRecorder? _optager;
    private readonly DispatcherTimer _ur;
    private DateTime _start;
    private string? _klip;
    private double _sekunder;

    /// <summary>Resultatet. Null hvis der ikke blev gemt noget.</summary>
    public Genlaesning? Resultat { get; private set; }

    public ReadAgainWindow(string optagelse, int nummer, string manuskript)
    {
        InitializeComponent();

        _optagelse = optagelse;
        _nummer = nummer;
        _manuskript = manuskript;

        Overskrift.Text = $"Sætning {nummer} — læs op igen";
        Saetning.Text = manuskript;

        _ur = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(60) };
        _ur.Tick += (_, _) => Opdater();

        VisMikrofoner();
    }

    // ------------------------------------------------------------- mikrofon

    private bool _indlaeser;

    private void VisMikrofoner()
    {
        var alle = AudioDevices.Microphones();
        Mikrofoner.ItemsSource = alle;

        var valgt = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var faldtTilbage);

        _indlaeser = true;
        Mikrofoner.SelectedItem = alle.FirstOrDefault(d => d.Id == valgt?.Id);
        _indlaeser = false;

        if (valgt is null)
        {
            OptagStatus.Text = "Ingen mikrofon fundet. Tilslut en, og åbn vinduet igen.";
            OptagStatus.Foreground = (Brush)FindResource("Advarsel");
            OptagKnap.IsEnabled = false;
            return;
        }

        if (!faldtTilbage) return;

        // Skiftet SKAL siges. Sker det i stilhed, taler man ind i noget andet
        // end det, man tror — og skylden lander på Whisper.
        OptagStatus.Text = "Den mikrofon, du havde valgt, er ikke tilsluttet — her er Windows' standard i stedet.";
        OptagStatus.Foreground = (Brush)FindResource("Advarsel");
    }

    private void Mikrofon_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_indlaeser || Mikrofoner.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.MicrophoneId = d.Id;
        AppSettings.Current.Save();

        OptagStatus.Text = $"Optager nu fra {d.FriendlyName}.";
        OptagStatus.Foreground = (Brush)FindResource("TekstMeget");
    }

    // ------------------------------------------------------------ optagelse

    private void Opdater()
    {
        if (_optager is null) return;

        var gået = DateTime.Now - _start;
        Ur.Text = $"{gået.TotalSeconds:0.0} sek";

        if (Niveau.Parent is FrameworkElement spor && spor.ActualWidth > 0)
            Niveau.Width = spor.ActualWidth * Math.Min(1, _optager.Niveau * 3);

        // Halvandet minut er rigeligt til én sætning. Grænsen findes, så en
        // glemt optagelse ikke bliver ved — ikke for at skynde på nogen.
        if (gået.TotalSeconds >= 90) Stop();
    }

    private void Optag_Click(object sender, RoutedEventArgs e)
    {
        if (_optager is null) Start();
        else Stop();
    }

    private void Start()
    {
        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        if (mik is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke optage", "Ingen mikrofon fundet.", Dialogs.Slags.Pas_paa);
            return;
        }

        Ryd();
        _klip = Path.Combine(Path.GetTempPath(), $"noteapp-igen-{Guid.NewGuid():N}.wav");

        try
        {
            _optager = new ShortClipRecorder(_klip);
            _optager.Start(mik.Id);
        }
        catch (Exception ex)
        {
            _optager = null;
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke optage", $"Optagelsen kunne ikke startes.\n\n{ex.Message}", Dialogs.Slags.Pas_paa);
            return;
        }

        _start = DateTime.Now;
        _ur.Start();

        OptagTekst.Text = "■ Stop";
        OptagFlade.Background = (Brush)FindResource("Advarsel");
        OptagStatus.Text = "Læs sætningen op nu. Tryk «Gem», når du er færdig.";
        OptagStatus.Foreground = (Brush)FindResource("TekstMeget");

        // Gem er tilgængelig med det samme: den stopper også optagelsen, så
        // man ikke skal trykke to knapper for at blive færdig.
        GemKnap.IsEnabled = true;
    }

    private void Stop()
    {
        if (_optager is null) return;

        _ur.Stop();

        try { _sekunder = _optager.Stop(); }
        finally { _optager.Dispose(); _optager = null; }

        OptagTekst.Text = "● Optag igen";
        OptagFlade.Background = (Brush)FindResource("Optager");
        Niveau.Width = 0;

        Ur.Text = $"{_sekunder:0.0} sek";
    }

    private void Ryd()
    {
        try { if (_klip is not null && File.Exists(_klip)) File.Delete(_klip); } catch (IOException) { }
        _klip = null;
        _sekunder = 0;
    }

    // ------------------------------------------------------------------ gem

    private async void Gem_Click(object sender, RoutedEventArgs e)
    {
        // «Gem» stopper også optagelsen. Ellers ville man skulle trykke stop
        // og derefter gem, og det er et trin, der ikke tilfører noget.
        if (_optager is not null) Stop();

        if (_klip is null || !File.Exists(_klip))
        {
            OptagStatus.Text = "Der er ikke optaget noget endnu. Tryk «Optag», og læs sætningen op.";
            OptagStatus.Foreground = (Brush)FindResource("Advarsel");
            return;
        }

        // For kort lyd giver vroevl frem for en fejl. Whisper digter paa meget
        // korte klip — «Tak.», «Undertekster af …» — og det ville se ud som en
        // hoerefejl paa saetningen.
        if (_sekunder < 1.5)
        {
            OptagStatus.Text = $"Der blev kun optaget {_sekunder:0.0} sekund. Læs hele sætningen op — " +
                               "kortere klip giver Whisper for lidt at gå efter, og så digter den.";
            OptagStatus.Foreground = (Brush)FindResource("Advarsel");
            return;
        }

        var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
        if (!install.IsComplete)
        {
            OptagStatus.Text = "Whisper er ikke hentet endnu — se «AI-modeller».";
            OptagStatus.Foreground = (Brush)FindResource("Advarsel");
            return;
        }

        GemKnap.IsEnabled = false;
        OptagKnap.IsEnabled = false;

        OptagStatus.Text = $"Skriver ud med {install.ModelFileName} — den samme model som dine møder. " +
                           "Det tager et halvt minut, fordi modellen skal indlæses.";
        OptagStatus.Foreground = (Brush)FindResource("TekstMeget");

        try
        {
            var motor = new Transcriber(install.WhisperCli!);
            var udBase = Path.ChangeExtension(_klip, null);

            var r = await motor.RunAsync(
                new TranscriptionRequest(_klip, install.ModelPath!, udBase, "da"));

            var hørt = Rens(r.Text);

            if (hørt.Length == 0)
            {
                OptagStatus.Text = "Der kom ingen tekst ud af klippet. Læs sætningen op igen, lidt tydeligere.";
                OptagStatus.Foreground = (Brush)FindResource("Advarsel");
                GemKnap.IsEnabled = true;
                OptagKnap.IsEnabled = true;
                return;
            }

            // Samme måling som karakteren bruger — ellers ville de to tal ikke
            // kunne sammenlignes, og sammenligningen er hele pointen.
            var (ord, ramt, _) = ReadAloudScore.Sammenlign(_manuskript, hørt);

            Resultat = Genlaesninger.Gem(_optagelse, _nummer, _manuskript, hørt, ord, ramt, _klip);

            Historik.Skriv(HaendelseType.Traening,
                $"Sætning {_nummer} læst op igen",
                $"{ramt} af {ord} ord ramt", model: install.ModelFileName ?? "", sti: _optagelse,
                sekunder: _sekunder);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            OptagStatus.Text = $"Klippet kunne ikke skrives ud: {ex.Message}";
            OptagStatus.Foreground = (Brush)FindResource("Advarsel");
            GemKnap.IsEnabled = true;
            OptagKnap.IsEnabled = true;
        }
    }

    /// <summary>
    /// Whisper skriver tit en linje om undertekster på klip med lidt lyd. Her
    /// er der kun brug for det, der blev sagt.
    /// </summary>
    private static string Rens(string tekst)
    {
        var linjer = tekst.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('[') && !l.Contains("ndertekst"))
            .ToList();

        return string.Join(" ", linjer).Trim();
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnClosed(EventArgs e)
    {
        _ur.Stop();
        _optager?.Dispose();

        // Arbejdsklippet maa ikke blive liggende i systemets temp-mappe med
        // stemme paa. Kopien ligger ved optagelsen, hvis der blev gemt.
        Ryd();

        base.OnClosed(e);
    }
}
