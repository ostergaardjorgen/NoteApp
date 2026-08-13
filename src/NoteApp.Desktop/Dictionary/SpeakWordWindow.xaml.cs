using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Dictionary;

/// <summary>
/// Opret eller ret et ord ved at TALE det.
///
/// HVORFOR DET ER BEDRE END AT SKRIVE
///
/// Skriver man et ord i ordbogen, ved man, hvordan det staves. Man ved ikke,
/// hvordan Whisper hører det — og det er dét, en rettelse skal bruge. Den del
/// måtte man før opdage ved at læse en transskription igennem og finde fejlen.
///
/// Taler man ordet ind, kommer den fejlhørte form af sig selv. Whisper skriver
/// «kerne sys», man retter stavningen til «Kernesys», og så har appen begge
/// dele: ordet og den fejl, det skal rettes fra.
///
/// SAMME MODEL SOM DINE MØDER
///
/// Der bruges den model, transskriptionerne bruger. Det koster et halvt minut
/// til indlæsning, men en anden model ville høre ordet anderledes — og så
/// ville reglen ikke ramme, når det gælder.
/// </summary>
public partial class SpeakWordWindow : Window
{
    private ShortClipRecorder? _optager;
    private readonly DispatcherTimer _ur;
    private DateTime _start;
    private string? _klip;

    /// <summary>Det, Whisper hørte. Tom hvis der ikke blev optaget noget.</summary>
    public string HørtSom { get; private set; } = "";

    /// <summary>Ordet, som det staves.</summary>
    public string Stavning => FeltStavning.Text.Trim();

    /// <summary>Den forkerte del, som brugeren har godkendt. Tom = intet at rette.</summary>
    public string Forkert => FeltForkert.Text.Trim();

    /// <summary>Er der en rettelse at lære?</summary>
    public bool HarRettelse =>
        Forkert.Length > 0 && !Forkert.Equals(Stavning, StringComparison.OrdinalIgnoreCase);

    public SpeakWordWindow(string? forslag = null)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(forslag)) FeltStavning.Text = forslag;

        _ur = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(60) };
        _ur.Tick += (_, _) => Opdater();

        VisEksempel();
        VisMikrofoner();
    }

    /// <summary>
    /// Mikrofonlisten står i selve optagevinduet.
    ///
    /// Er den valgte enhed væk, falder Windows tilbage til sin standard — og
    /// gør den det uden at sige det, taler man ind i noget andet end det, man
    /// tror. Det skete: klippet blev tavst, og skylden landede på Whisper.
    ///
    /// Vælger man en anden her, gemmes den. Det er den samme indstilling som
    /// under Indstillinger; to steder at vælge fra må ikke give to svar.
    /// </summary>
    private void VisMikrofoner()
    {
        var alle = AudioDevices.Microphones();
        Mikrofoner.ItemsSource = alle;

        var valgt = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var faldtTilbage);

        _indlæser = true;
        Mikrofoner.SelectedItem = alle.FirstOrDefault(d => d.Id == valgt?.Id);
        _indlæser = false;

        Mikrofon.Text = valgt is null
            ? "Ingen mikrofon fundet."
            : faldtTilbage
                ? "Den mikrofon, du havde valgt, er ikke tilsluttet — her er Windows' standard i stedet."
                : "";

        Mikrofon.Visibility = Mikrofon.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        Mikrofon.Foreground = (Brush)FindResource(faldtTilbage ? "Advarsel" : "TekstMeget");
    }

    private bool _indlæser;

    private void Mikrofon_Valgt(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_indlæser || Mikrofoner.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.MicrophoneId = d.Id;
        AppSettings.Current.Save();

        Mikrofon.Visibility = Visibility.Collapsed;
        OptagStatus.Text = $"Optager nu fra {d.FriendlyName}.";
    }

    /// <summary>
    /// Sætningen, man skal læse op.
    ///
    /// Rammen skal virke for ALT, en mødeordbog indeholder: et system, et
    /// firma, et fagord, et personnavn. «Vi talte om X på mødet i går» gør —
    /// den er neutral, den er arbejdssprog, og ordet står midt i sætningen,
    /// hvor det smelter sammen med naboordene. Det er dér, fejlen opstår.
    ///
    /// Ordet siges FØRST alene, så begge former kommer med: den isolerede og
    /// den i sammenhæng. De bliver ofte hørt forskelligt.
    /// </summary>
    private void VisEksempel()
    {
        var ord = FeltStavning.Text.Trim();

        if (ord.Length == 0)
        {
            Eksempel.Text = "Ordet er …";
            EksempelHjaelp.Text = "Skriv ordet nedenfor, så laver appen sætningen til dig.";
            OptagKnap.IsEnabled = false;
            return;
        }

        Eksempel.Text = $"«Ordet er {ord}. Vi talte om {ord} på mødet i går, og det skal med i referatet.»";
        EksempelHjaelp.Text = "Læs sætningen op i dit normale tempo — ikke tydeligere end du ville sige det på et møde.";
        OptagKnap.IsEnabled = true;
    }

    private void Ord_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) => VisEksempel();

    private void Opdater()
    {
        if (_optager is null) return;

        var gået = DateTime.Now - _start;
        Ur.Text = $"{gået.TotalSeconds:0.0} sek";

        if (Niveau.Parent is FrameworkElement spor && spor.ActualWidth > 0)
            Niveau.Width = spor.ActualWidth * Math.Min(1, _optager.Niveau * 3);

        // Stopper af sig selv efter ti sekunder. Laengere er ikke bedre: det er
        // een saetning, der skal siges, ikke et afsnit — og hvert sekund lyd
        // koster tid i transskriptionen bagefter.
        if (gået.TotalSeconds >= 10) StopOgSkrivUd();
    }

    private void Optag_Click(object sender, RoutedEventArgs e)
    {
        if (_optager is null) StartOptagelse();
        else StopOgSkrivUd();
    }

    private void StartOptagelse()
    {
        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        if (mik is null)
        {
            MessageBox.Show("Ingen mikrofon fundet.", "Kan ikke optage",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _klip = Path.Combine(Path.GetTempPath(), $"noteapp-ord-{Guid.NewGuid():N}.wav");

        try
        {
            _optager = new ShortClipRecorder(_klip);
            _optager.Start(mik.Id);
        }
        catch (Exception ex)
        {
            _optager = null;
            MessageBox.Show($"Optagelsen kunne ikke startes.\n\n{ex.Message}", "Kunne ikke optage",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _start = DateTime.Now;
        _ur.Start();

        OptagTekst.Text = "■ Stop";
        OptagFlade.Background = (Brush)FindResource("Advarsel");
        OptagStatus.Text = "Sig sætningen nu. Den stopper af sig selv efter ti sekunder.";
        ResultatPanel.Visibility = Visibility.Collapsed;
        GemKnap.IsEnabled = false;
    }

    private async void StopOgSkrivUd()
    {
        if (_optager is null) return;

        _ur.Stop();

        double sekunder;
        try { sekunder = _optager.Stop(); }
        finally { _optager.Dispose(); _optager = null; }

        OptagTekst.Text = "● Optag igen";
        OptagFlade.Background = (Brush)FindResource("Optager");
        Niveau.Width = 0;

        // For kort lyd giver vroevl frem for en fejl. Whisper digter paa
        // meget korte klip — «Tak.», «Undertekster af …» — og det ville se ud
        // som en hoerefejl paa ordet.
        if (sekunder < 2.0)
        {
            OptagStatus.Text = $"Der blev kun optaget {sekunder:0.0} sekund. Læs hele sætningen op — " +
                               "kortere klip giver Whisper for lidt at gå efter, og så digter den.";
            return;
        }

        var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
        if (!install.IsComplete)
        {
            OptagStatus.Text = "Whisper er ikke hentet endnu — se «AI-modeller». Du kan skrive ordet i stedet.";
            return;
        }

        OptagKnap.IsEnabled = false;
        OptagStatus.Text = $"Skriver ud med {install.ModelFileName} — den samme model som dine møder. " +
                           "Det tager et halvt minut, fordi modellen skal indlæses.";

        try
        {
            var motor = new Transcriber(install.WhisperCli!);
            var udBase = Path.ChangeExtension(_klip!, null);

            var r = await motor.RunAsync(
                new TranscriptionRequest(_klip!, install.ModelPath!, udBase, "da"));

            HørtSom = RensLinjer(r.Text);

            if (HørtSom.Length == 0)
            {
                OptagStatus.Text = "Der kom ingen tekst ud. Sig sætningen lidt tydeligere, og prøv igen.";
                return;
            }

            Hoert.Text = HørtSom;
            ResultatPanel.Visibility = Visibility.Visible;

            FeltForkert.Text = GaetForkertDel(HørtSom, Stavning);

            GemKnap.IsEnabled = true;
            OptagStatus.Text = FeltForkert.Text.Length > 0
                ? "Se efter, om gættet på den forkerte del passer."
                : "Ordet blev hørt rigtigt. Så er der ikke noget at rette — gem det som et ord i ordbogen.";

            FeltForkert.Focus();
            FeltForkert.SelectAll();
        }
        catch (Exception ex)
        {
            OptagStatus.Text = $"Kunne ikke skrive klippet ud: {ex.Message}";
        }
        finally
        {
            OptagKnap.IsEnabled = true;
        }
    }

    /// <summary>
    /// Gætter, hvilken del af det hørte der er ordet, hørt forkert.
    ///
    /// Rammen er kendt — «Ordet er X. Vi talte om X på mødet i går, og det
    /// skal med i referatet.» — så alt, der IKKE er rammens ord, er
    /// kandidaten. Det er en simpel metode, og den er med vilje: den skal
    /// bare give et udgangspunkt, brugeren kan rette. Et gæt, der er tæt på,
    /// sparer skrivearbejdet; et gæt, der er forkert, koster ingenting, fordi
    /// feltet kan rettes.
    ///
    /// Bliver ordet hørt RIGTIGT, er der intet at rette, og der returneres
    /// tomt frem for at finde på noget.
    /// </summary>
    private static string GaetForkertDel(string hørt, string ord)
    {
        if (hørt.Contains(ord, StringComparison.OrdinalIgnoreCase)) return "";

        var ramme = new HashSet<string>(
            "ordet er vi talte om på mødet i går og det skal med referatet".Split(' '),
            StringComparer.OrdinalIgnoreCase);

        var rest = hørt.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(o => o.Trim('.', ',', '!', '?', ':', ';'))
            .Where(o => o.Length > 0 && !ramme.Contains(o))
            .ToList();

        if (rest.Count == 0) return "";

        // Ordet siges to gange. Staar den samme rest to gange, er det med stor
        // sandsynlighed netop den — og saa er een forekomst nok som regel.
        var gentaget = rest.GroupBy(o => o, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (gentaget.Count > 0) return string.Join(" ", gentaget);

        // Ellers de foerste par ord, der ikke er rammens. Flere end tre er
        // sjaeldent eet fejlhoert ord.
        return string.Join(" ", rest.Take(3));
    }

    /// <summary>
    /// Whisper skriver tit tegnsætning og af og til en hel linje om
    /// undertekster på tomme klip. Her er der kun brug for ordene.
    /// </summary>
    private static string RensLinjer(string tekst)
    {
        var linjer = tekst.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('[') && !l.Contains("ndertekst"))
            .ToList();

        return string.Join(" ", linjer).Trim(' ', '.', ',', '!', '?');
    }

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        if (Stavning.Length == 0)
        {
            MessageBox.Show("Skriv, hvordan ordet staves.", "Mangler stavemåde",
                MessageBoxButton.OK, MessageBoxImage.Information);
            FeltStavning.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnClosed(EventArgs e)
    {
        _ur.Stop();
        _optager?.Dispose();

        // Klippet er et arbejdsspor. Det maa ikke blive liggende i systemets
        // temp-mappe med et moede paa.
        try { if (_klip is not null && File.Exists(_klip)) File.Delete(_klip); } catch (IOException) { }

        base.OnClosed(e);
    }
}
