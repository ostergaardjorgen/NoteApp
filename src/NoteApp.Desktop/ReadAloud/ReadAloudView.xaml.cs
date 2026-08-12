using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.ReadAloud;

/// <summary>
/// Oplæsningsstudiet: teleprompter plus optager.
///
/// Formålet er ikke at være pæn, men at gøre det svært at ødelægge testen.
/// De to fejl, der koster hele optagelsen, er at læse for hurtigt (så der
/// ikke er 15-20 minutters lyd at måle på) og at glemme, hvilken blok man er
/// i (så transskriptionen ikke kan holdes op mod facitlisten). Begge dele
/// vises hele tiden, og blokskift skrives som bogmærker i notes.jsonl.
/// </summary>
public partial class ReadAloudView : UserControl
{
    private ScriptDocument _script;
    private readonly DispatcherTimer _timer;

    private RecordingSession? _session;
    private int _afsnitIndex;
    private int _sidsteBlok = -1;

    // Live-lytningen der skifter afsnit af sig selv. Den er en hjaelper:
    // fejler den, eller taber den traaden, virker mellemrum praecis som foer.
    private LiveListener? _lytter;
    private readonly ScriptFollower _foelger = new();

    // Vagthunden fanger det tilfaelde, hvor medlytningen lever, men intet
    // hoerer. Uden den ser den slags ud praecis som en, der virker.
    private DispatcherTimer? _vagthund;
    private bool _hoertNoget;

    public ReadAloudView()
    {
        InitializeComponent();

        _script = ScriptDocument.Load();
        VisTekst();

        Praktisk.Text =
            "Læs ikke overskrifterne højt — de er kun til dig. Sid i normal afstand fra mikrofonen.";

        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        MikrofonNavn.Text = mik?.FriendlyName ?? "ingen mikrofon fundet";
        OptagKnap.IsEnabled = mik is not null;
        if (mik is null) Status.Text = "Der er ingen mikrofon. Tilslut en, og genstart appen.";

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Opdater();

        FoelgMed.IsChecked = AppSettings.Current.AutoAdvance;
        VisFoelgStatus();

        // Bjaelkens bredde kan foerst regnes, naar sporet har en bredde. Den
        // saettes derfor baade ved indlaesning og naar vinduet aendrer stoerrelse.
        Loaded += (_, _) => { Focus(); SaetBjaelke(); };
        SizeChanged += (_, _) => SaetBjaelke();
    }

    public bool IsRecording => _session?.IsRecording == true;

    // ------------------------------------------------------------ tekstvalg

    /// <summary>
    /// Skifter oplæsningstekst. Kan ikke ske under en optagelse — teksten er
    /// det, transskriptionen holdes op imod, og skiftes den midtvejs, måler
    /// optagelsen ingenting.
    /// </summary>
    /// <summary>Hvilket af de tre kort der er valgt.</summary>
    private int ValgtIndex =>
        Valg2.IsChecked == true ? 2 : Valg1.IsChecked == true ? 1 : 0;

    private void Vaelg(int index)
    {
        var knap = index switch { 2 => Valg2, 1 => Valg1, _ => Valg0 };
        knap.IsChecked = true;    // Tekst_Valgt henter teksten
    }

    private void Tekst_Valgt(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;

        if (IsRecording)
        {
            MessageBox.Show(
                "Teksten kan ikke skiftes, mens der optages.\n\n" +
                "Stop optagelsen først — det, du har læst, bliver gemt.",
                "Optagelsen kører", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var valgt = ScriptDocument.Available[ValgtIndex];
        try
        {
            _script = ScriptDocument.Load(valgt.File);
        }
        catch (Exception ex)
        {
            // Teksten kan mangle, hvis repo-mappen er flyttet OG appen er bygget
            // uden den indlejrede kopi. Sig hvad der mangler frem for at falde.
            MessageBox.Show(ex.Message, "Teksten kunne ikke hentes",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _afsnitIndex = 0;
        _sidsteBlok = -1;

        // Et tekstskift lukker kvitteringen: den hoerer til den forrige
        // oplaesning, og at lade den staa ville vise tal for noget andet end
        // det, der nu er valgt.
        KvitteringPanel.Visibility = Visibility.Collapsed;
        VejledningPanel.Visibility = Visibility.Visible;

        VisTekst();
    }

    /// <summary>Alt, der afhænger af hvilken tekst der er valgt, ét sted.</summary>
    private void VisTekst()
    {
        var valgt = ScriptDocument.Available[ValgtIndex];
        var minutter = (int)Math.Round(_script.EstimatedDuration.TotalMinutes);

        Overskrift.Text = "Lær appen dine ord at kende";
        Indledning.Text =
            $"Valgt: {valgt.Name} — {_script.TotalWords} ord, {_script.Paragraphs.Count} afsnit, cirka {minutter} minutter. " +
            "Indholdet er et opdigtet møde og betyder ikke noget. Det er din udtale og dine fagord, der bliver målt.";

        HvorforOverskrift.Text = $"Hvorfor «{valgt.Name}»";
        HvorforLaengde.Text = valgt.Why;
        HvorforNaeste.Text = valgt.Next;

        VisSkarphed();

        if (_session is null && OptagKnap.IsEnabled)
            Status.Text = $"{_script.Blocks.Count} blokke, {_script.Paragraphs.Count} afsnit. " +
                          $"Optagelsen gemmes i {UserDataPaths.Meetings}";
    }

    /// <summary>
    /// Måleren over, hvor meget appen har at gå efter.
    ///
    /// Den lover IKKE, at Whisper lærer din stemme — det gør den ikke, vægtene
    /// ligger fast. Det, der bliver skarpere, er to ting, der begge kan måles:
    /// ordbogen, som går med ind i genkendelsen, og sikkerheden i målingen af,
    /// hvor godt det går. Derfor er det de to tal, der står, frem for en
    /// procentsats, ingen kan efterprøve.
    /// </summary>
    private void VisSkarphed()
    {
        var (minutter, læst) = OptagetIndtilNu();

        Status0.Text = læst.Contains("dansk") ? "✓ indtalt" : "";
        Status1.Text = læst.Contains("blandet") ? "✓ indtalt" : "";
        Status2.Text = læst.Contains("engelsk") ? "✓ indtalt" : "";

        // 30 minutter er "fuld" bjaelke: de tre tekster tilsammen. Det er et
        // maal, ikke en graense — den bliver ved med at blive bedre bagefter.
        const double maal = 30.0;
        var andel = Math.Min(1.0, minutter / maal);

        SkarpTal.Text = $"{minutter:0} min · {læst.Count} af 3";
        SkarpBjaelke.Width = Math.Max(0, SkarpBjaelke.Width);
        SkarpBjaelke.Tag = andel;   // bredden saettes i Loaded/SizeChanged
        SaetBjaelke();

        SkarpTekst.Text = læst.Count switch
        {
            0 => "Appen har intet at gå efter endnu. Den første indtaling er også den, der giver mest: uden den findes der ikke et udgangspunkt at måle senere forbedringer imod.",
            1 => "Godt begyndt. Hver ny indtaling dækker noget, den forrige ikke gjorde — og jo flere fagord og navne der har været forbi, jo flere kan ordbogen holde styr på.",
            2 => "Der mangler én. Den sidste er den, der lukker hullet: så er både dansk, engelsk og sprogskiftet dækket, og du ved, hvor grænsen går.",
            _ => "Alle tre er indtalt. Herfra bliver det skarpere af sig selv: hver gang du retter et ord i en transskription, lærer ordbogen det, og næste møde bliver ramt bedre."
        };
    }

    private void SaetBjaelke()
    {
        if (SkarpBjaelke.Tag is not double andel) return;
        if (SkarpBjaelke.Parent is not FrameworkElement spor) return;

        var bredde = spor.ActualWidth;
        if (bredde > 0) SkarpBjaelke.Width = bredde * andel;
    }

    /// <summary>
    /// Hvor meget der er indtalt indtil nu, og hvilke af de tre tekster der er
    /// dækket. Læses af mapperne frem for at blive gemt et sted for sig — så
    /// kan tallet ikke komme ud af trit med det, der faktisk ligger på disken.
    /// </summary>
    private static (double Minutter, HashSet<string> Laest) OptagetIndtilNu()
    {
        var læst = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        double minutter = 0;

        if (!Directory.Exists(UserDataPaths.Meetings)) return (0, læst);

        foreach (var mappe in Directory.GetDirectories(UserDataPaths.Meetings))
        {
            var meta = MeetingStore.Load(mappe);
            if (meta?.Title is null) continue;

            // «Fase0-oplaesning» er den gamle titel, fra dengang der kun fandtes
            // een tekst. Den taeller som den danske — optagelsen er lavet, og
            // den skal ikke forsvinde fra taellingen, fordi navngivningen er
            // aendret bagefter.
            var gammel = meta.Title.Equals("Fase0-oplaesning", StringComparison.OrdinalIgnoreCase);
            if (!gammel && !meta.Title.StartsWith("Oplaesning", StringComparison.OrdinalIgnoreCase))
                continue;

            minutter += meta.DurationSeconds / 60.0;

            if (gammel) { læst.Add("dansk"); continue; }

            foreach (var t in ScriptDocument.Available)
                if (meta.Title.EndsWith(t.Key, StringComparison.OrdinalIgnoreCase)) læst.Add(t.Key);
        }

        return (minutter, læst);
    }

    // ---------------------------------------------------------------- optag

    private void Optag_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) StartOptagelse();
        else StopOptagelse();
    }

    private void StartOptagelse()
    {
        // Den valgte mikrofon fra Indstillinger. Er den taget ud siden sidst,
        // falder vi tilbage paa Windows' standard — men siger det foerst, saa
        // man ikke opdager det efter tyve minutters oplaesning.
        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var fallback);
        if (mik is null)
        {
            MessageBox.Show("Ingen mikrofon fundet.", "Kan ikke optage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (fallback)
        {
            var svar = MessageBox.Show(
                $"Den mikrofon, du havde valgt under Indstillinger, er ikke tilsluttet.\n\n" +
                $"Der optages i stedet fra: {mik.FriendlyName}\n\nFortsæt?",
                "Mikrofonen er skiftet", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (svar != MessageBoxResult.OK) return;
        }

        // Oplæsning er per definition et fysisk møde: ét spor, kun mikrofonen.
        // Der er ingen anden part, og derfor intet loopback-spor at fejle på.
        // Titlen baerer, HVILKEN tekst der blev laest. Uden den kan skaermen
        // ikke vise, hvad der mangler — og saa er de tre kort bare tre knapper.
        _session = RecordingSession.Create(
            MeetingType.Physical,
            "Oplaesning-" + ScriptDocument.Available[ValgtIndex].Key,
            mik, null);
        _session.IncidentOccurred += i => Dispatcher.Invoke(() =>
            Status.Text = $"Hændelse ved {TimeSpan.FromSeconds(i.AtSeconds):mm\\:ss}: {i.What}");

        _session.Start();

        _afsnitIndex = 0;
        _sidsteBlok = -1;
        VejledningPanel.Visibility = Visibility.Collapsed;
        KvitteringPanel.Visibility = Visibility.Collapsed;
        LaesePanel.Visibility = Visibility.Visible;
        OptagKnap.Content = "■ Stop og gem";
        NaesteKnap.IsEnabled = true;
        ForrigeKnap.IsEnabled = false;
        PauseKnap.Visibility = Visibility.Visible;
        PauseKnap.Content = "❚❚ Pause";
        OptagerPrik.Fill = (Brush)FindResource("Optager");

        VisAfsnit();
        _timer.Start();

        if (AppSettings.Current.AutoAdvance) StartLytning();

        Focus();
    }

    private void StopOptagelse()
    {
        if (_session is null) return;

        _timer.Stop();
        StopLytning();

        var filer = _session.Stop();
        var mappe = _session.SessionDir;
        var længde = _session.Elapsed;
        var noter = _session.Notebook.Notes.Count;

        _session.Dispose();
        _session = null;

        OptagKnap.Content = "● Start optagelse";
        NaesteKnap.IsEnabled = false;
        ForrigeKnap.IsEnabled = false;
        PauseKnap.Visibility = Visibility.Collapsed;
        LaesePanel.Visibility = Visibility.Collapsed;
        OptagerPrik.Fill = (Brush)FindResource("TekstMeget");

        VisKvittering(mappe, længde, noter);
    }

    // ---------------------------------------------------------- kvitteringen

    private string? _sidsteOptagelse;

    /// <summary>
    /// Kvitteringen efter en oplæsning.
    ///
    /// Den findes, fordi den forrige udgave sluttede med ingenting: en dialog
    /// spurgte, om mappen skulle åbnes, og så stod man i en mappe med tre filer
    /// uden at vide, hvad der skulle ske nu. Tyve minutters oplæsning er et
    /// stykke arbejde, og et stykke arbejde skal kvitteres og pege videre.
    /// </summary>
    private void VisKvittering(string mappe, TimeSpan længde, int blokke)
    {
        _sidsteOptagelse = mappe;

        VejledningPanel.Visibility = Visibility.Collapsed;
        KvitteringPanel.Visibility = Visibility.Visible;

        var læste = _afsnitIndex + 1;
        var helt = læste >= _script.Paragraphs.Count;
        var ord = _script.Paragraphs.Take(læste).Sum(p => p.WordCount);
        var tempo = længde.TotalMinutes > 0 ? ord / længde.TotalMinutes : 0;

        BlokTitel.Text = "Optagelsen er gemt";
        BlokUnder.Text = $"{længde:hh\\:mm\\:ss} lyd, {blokke} blokmærker";

        KvitOverskrift.Text = helt ? "Godt læst op — hele teksten er i hus" : "Optagelsen er gemt";
        KvitUnder.Text = helt
            ? "Appen har nu din stemme på det materiale, den kan måles på."
            : $"Du nåede {læste} af {_script.Paragraphs.Count} afsnit. Det er gemt, og du kan læse resten en anden gang — det bliver bare en ny optagelse.";

        KvitLaengde.Text = længde.ToString(@"mm\:ss");
        KvitAfsnit.Text = $"{læste}/{_script.Paragraphs.Count}";
        KvitTempo.Text = $"{tempo:0}";

        // Vurderingen skal sige, hvad tallene BETYDER. "15:10" er ikke en
        // vurdering; "det er nok til gaten" er.
        var nok = længde >= TimeSpan.FromMinutes(15);
        KvitVurdering.Text = nok
            ? $"{længde:mm\\:ss} er nok til at give pålidelige tal. Læste du hurtigere end de 120 ord i minuttet, teksten er sat efter, betyder det ikke noget for målingen — det er ordene, der tælles, ikke minutterne."
            : $"{længde:mm\\:ss} er under de 15 minutter, målingen har brug for. Du kan godt transskribere den, men tallene bliver usikre. Læs gerne en tekst mere.";
        KvitVurdering.Foreground = (Brush)FindResource(nok ? "Godkendt" : "Advarsel");

        Status.Text = nok
            ? $"{længde:mm\\:ss} optaget — det er nok til gaten."
            : $"Kun {længde:mm\\:ss}. Gaten har brug for 15-20 minutter for at give brugbare tal.";

        // Naeste tekst: den foerste af de tre, der IKKE er indtalt endnu. Er de
        // alle taget, peges der paa den naeste i raekken — en tekst kan laeses
        // igen, og mere materiale er ikke spildt.
        var (_, alleredeLæst) = OptagetIndtilNu();
        var næste = -1;
        for (var i = 0; i < ScriptDocument.Available.Count; i++)
            if (!alleredeLæst.Contains(ScriptDocument.Available[i].Key)) { næste = i; break; }

        var alleTaget = næste < 0;
        if (alleTaget) næste = (ValgtIndex + 1) % ScriptDocument.Available.Count;

        KvitNaesteKnapIndex = næste;
        var n = ScriptDocument.Available[næste];
        KvitNaesteTekst.Content = alleTaget ? $"Læs «{n.Name}» igen" : $"Næste: {n.Name}";
        KvitNaesteHvad.Text = alleTaget
            ? "Alle tre er indtalt. Herfra flytter det mest at rette ord i transskriptionerne — det er dét, ordbogen lærer af."
            : n.Why;

        VisSkarphed();
    }

    private int KvitNaesteKnapIndex;

    private void KvitTransskriber_Click(object sender, RoutedEventArgs e) => TranskriptionØnskes?.Invoke();

    /// <summary>
    /// Bedt om at komme videre til transskription. Skærmskiftet ligger i
    /// MainWindow, som er den eneste, der kender navigationen.
    /// </summary>
    public event Action? TranskriptionØnskes;

    private void KvitNaesteTekst_Click(object sender, RoutedEventArgs e)
    {
        KvitteringPanel.Visibility = Visibility.Collapsed;
        VejledningPanel.Visibility = Visibility.Visible;
        BlokTitel.Text = "Klar til oplæsning";
        BlokUnder.Text = string.Empty;
        Vaelg(KvitNaesteKnapIndex);
    }

    private void KvitAabnMappe_Click(object sender, RoutedEventArgs e)
    {
        if (_sidsteOptagelse is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_sidsteOptagelse}\"") { UseShellExecute = true });
    }

    // ------------------------------------------------------------ auto-skift

    /// <summary>
    /// Fortæller, om automatisk skift kan lade sig gøre — og hvorfor ikke,
    /// hvis det ikke kan. Et afkrydsningsfelt, der stille intet gør, er
    /// værre end et, der er slået fra med en begrundelse.
    /// </summary>
    private void VisFoelgStatus()
    {
        var install = WhisperInstall.Locate();
        var stream = LiveListener.FindStreamExe(install.WhisperCli);
        var model = LiveModelPath();

        if (stream is null)
        {
            FoelgMed.IsEnabled = false;
            FoelgStatus.Text = "kræver whisper-stream, som ikke findes i din motor-mappe";
        }
        else if (model is null)
        {
            FoelgMed.IsEnabled = false;
            FoelgStatus.Text = $"hent modellen «{AppSettings.Current.LiveModel}» under Motor og model først";
        }
        else
        {
            FoelgMed.IsEnabled = true;
            FoelgStatus.Text = FoelgMed.IsChecked == true
                ? $"lytter med {AppSettings.Current.LiveModel} — mellemrum virker stadig"
                : "";
        }
    }

    /// <summary>
    /// Stien til live-modellen. Den er bevidst en anden end den store: den
    /// skal svare hvert andet sekund, ikke skrive det bedste resultat.
    /// </summary>
    private static string? LiveModelPath()
    {
        var id = AppSettings.Current.LiveModel;
        var model = WhisperInstall.Model(id);
        if (model is null) return null;

        var sti = WhisperInstall.Locate(id).ModelPath;
        return sti is not null && Path.GetFileName(sti).Equals(model.FileName, StringComparison.OrdinalIgnoreCase)
            ? sti
            : null;
    }

    private void FoelgMed_Klik(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.AutoAdvance = FoelgMed.IsChecked == true;
        AppSettings.Current.Save();
        VisFoelgStatus();

        if (_session is null) return;

        if (AppSettings.Current.AutoAdvance) StartLytning();
        else StopLytning();
    }

    private void StartLytning()
    {
        var install = WhisperInstall.Locate();
        var stream = LiveListener.FindStreamExe(install.WhisperCli);
        var model = LiveModelPath();
        if (stream is null || model is null) return;

        // Lyt paa den mikrofon, brugeren har valgt — ikke paa Windows'
        // standard. Ellers foelger appen en anden lyd end den, den optager.
        var enheder = LiveListener.ListCaptureDevices(stream);
        var valgt = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        var sdlId = LiveListener.MatchDevice(enheder, valgt?.FriendlyName);

        _lytter = new LiveListener(stream);
        _lytter.Heard += tekst => Dispatcher.BeginInvoke(() => Hoert(tekst));

        // Fejler medlytningen, SKAL afkrydsningsfeltet slaa fra. Ellers staar
        // der, at der lyttes med, mens der ikke goer — og saa venter man paa et
        // skift, der aldrig kommer, i stedet for at bruge mellemrum.
        _lytter.Failed += fejl => Dispatcher.BeginInvoke(() =>
        {
            FoelgMed.IsChecked = false;
            FoelgStatus.Text = $"{fejl} — brug mellemrum";
            FoelgStatus.Foreground = (Brush)FindResource("Advarsel");
        });

        // Vagthund: hoeres der intet i det foerste stykke tid, er noget galt,
        // selv om processen lever. En medlytning, der koerer uden at hoere
        // noget, ligner en, der virker.
        _hoertNoget = false;
        _vagthund = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
        _vagthund.Tick += (_, _) =>
        {
            _vagthund!.Stop();
            if (_hoertNoget || _lytter is null) return;

            FoelgMed.IsChecked = false;
            FoelgStatus.Text = "der blev ikke hørt noget på 25 sekunder — brug mellemrum";
            FoelgStatus.Foreground = (Brush)FindResource("Advarsel");
        };
        _vagthund.Start();

        _foelger.SetParagraph(_script.Paragraphs[_afsnitIndex].Text);

        // Lyt paa TEKSTENS sprog. Lytter den efter dansk, mens der laeses
        // engelsk, holder den op med at genkende ordene og staar stille — og
        // saa ser det ud, som om appen har mistet traaden.
        _lytter.Start(model, sdlId, _script.Language);

        FoelgStatus.Text = sdlId >= 0
            ? $"lytter med {AppSettings.Current.LiveModel} — mellemrum virker stadig"
            : $"lytter på Windows' standardmikrofon — mellemrum virker stadig";
    }

    private void StopLytning()
    {
        _vagthund?.Stop();
        _vagthund = null;
        _lytter?.Dispose();
        _lytter = null;
    }

    /// <summary>
    /// Kaldes for hver linje, live-lytningen producerer. Skifter afsnit, når
    /// slutningen af det aktuelle er hørt.
    /// </summary>
    private void Hoert(string tekst)
    {
        _hoertNoget = true;

        if (_session is null || _session.IsPaused) return;
        if (_afsnitIndex + 1 >= _script.Paragraphs.Count) return;

        if (_foelger.Feed(tekst)) Flyt(1);
    }

    /// <summary>
    /// Pause og fortsæt. Under pausen optages der intet — mikrofonen slippes,
    /// og uret står stille. Det er dét, der gør det trygt at trykke start:
    /// kommer der nogen ind ad døren, skal man ikke først finde ud af, om
    /// samtalen bliver optaget.
    /// </summary>
    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;

        if (_session.IsPaused)
        {
            _session.Resume();
            PauseKnap.Content = "❚❚ Pause";
            OptagerPrik.Fill = (Brush)FindResource("Optager");
            Status.Text = "Optager igen.";
            _timer.Start();
            if (AppSettings.Current.AutoAdvance) StartLytning();
        }
        else
        {
            _session.Pause();
            _timer.Stop();

            // Lytningen stoppes ogsaa. Ellers ville den blive ved med at
            // hoere efter under en pause, hvor der netop ikke skal optages.
            StopLytning();
            PauseKnap.Content = "▶ Fortsæt";
            OptagerPrik.Fill = (Brush)FindResource("Advarsel");
            Niveau.Width = 0;
            Ur.Text = _session.Elapsed.ToString(@"mm\:ss");
            Status.Text = "PÅ PAUSE — der optages ikke. Tryk Fortsæt, når du er klar.";
        }

        Focus();
    }

    // ------------------------------------------------------------ navigation

    private void Naeste_Click(object sender, RoutedEventArgs e) => Flyt(1);

    private void Forrige_Click(object sender, RoutedEventArgs e) => Flyt(-1);

    private void Flyt(int retning)
    {
        if (_session is null) return;

        var ny = _afsnitIndex + retning;
        if (ny < 0 || ny >= _script.Paragraphs.Count) return;

        _afsnitIndex = ny;
        VisAfsnit();

        // Foelgeren skal vide, hvad den nu skal lytte efter — ogsaa naar man
        // selv trykker mellemrum. Ellers ville den blive ved med at vente paa
        // slutningen af et afsnit, brugeren allerede har forladt.
        _foelger.SetParagraph(_script.Paragraphs[_afsnitIndex].Text);
    }

    /// <summary>
    /// Mellemrum og pil frem går videre. Tasterne fanges her frem for som
    /// knap-genveje, så de virker uanset hvad der har fokus — man skal kunne
    /// læse videre uden at se på skærmen.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_session is not null)
        {
            switch (e.Key)
            {
                case Key.Space:
                case Key.Right:
                case Key.PageDown:
                case Key.Down:
                    Flyt(1);
                    e.Handled = true;
                    return;
                case Key.Left:
                case Key.PageUp:
                case Key.Up:
                    Flyt(-1);
                    e.Handled = true;
                    return;
            }
        }
        base.OnPreviewKeyDown(e);
    }

    private void VisAfsnit()
    {
        var p = _script.Paragraphs[_afsnitIndex];
        Afsnit.Text = p.Text;

        var blok = _script.Blocks[p.BlockIndex];
        BlokTitel.Text = blok.Title;
        var sidste = _afsnitIndex + 1 >= _script.Paragraphs.Count;
        BlokUnder.Text = sidste
            ? $"Sidste afsnit af {_script.Paragraphs.Count} — tryk «Stop og gem», når du har læst det"
            : $"Afsnit {_afsnitIndex + 1} af {_script.Paragraphs.Count}  ·  mål for blokken {blok.TargetStart:mm\\:ss}–{blok.TargetEnd:mm\\:ss}";

        // Blokskiftet skrives som bogmærke. Uden det kan transskriptionen ikke
        // holdes op mod facitlisten blok for blok, og så skal hele teksten
        // læses igennem manuelt for at score den.
        if (p.BlockIndex != _sidsteBlok)
        {
            _sidsteBlok = p.BlockIndex;
            _session?.Notebook.Add($"[blok {p.BlockIndex + 1}] {blok.Title}");
        }

        ForrigeKnap.IsEnabled = _afsnitIndex > 0;
        NaesteKnap.IsEnabled = _afsnitIndex + 1 < _script.Paragraphs.Count;
        Fremdrift.Value = (_afsnitIndex + 1) * 100.0 / _script.Paragraphs.Count;
    }

    // ---------------------------------------------------------------- timer

    private void Opdater()
    {
        if (_session is null) return;

        var gået = _session.Elapsed;
        Ur.Text = gået.ToString(@"mm\:ss");

        var top = _session.Microphone?.ReadPeak() ?? 0f;
        Niveau.Width = 220 * PeakMeter.ToMeterScale(top);

        // Tavshed under en optagelse er den fejl, man opdager bagefter.
        Niveau.Background = top < AudioDevices.SilenceThreshold
            ? (Brush)FindResource("Optager")
            : (Brush)FindResource("Godkendt");

        // Hvor burde jeg være nu? Målet regnes ud fra, hvor langt gennem
        // teksten jeg er — ikke fra blokkens starttid alene, for så ville
        // tallet hoppe ved hvert blokskift.
        var andel = (_afsnitIndex + 1.0) / _script.Paragraphs.Count;
        var mål = TimeSpan.FromSeconds(_script.EstimatedDuration.TotalSeconds * andel);
        UrMaal.Text = $"mål {mål:mm\\:ss}";

        var afvigelse = gået - mål;
        if (Math.Abs(afvigelse.TotalSeconds) < 45)
        {
            Tempo.Text = "i takt";
            Tempo.Foreground = (Brush)FindResource("Godkendt");
        }
        else if (afvigelse < TimeSpan.Zero)
        {
            Tempo.Text = $"{Math.Abs(afvigelse.TotalSeconds):0} sek for hurtigt";
            Tempo.Foreground = (Brush)FindResource("Advarsel");
        }
        else
        {
            Tempo.Text = $"{afvigelse.TotalSeconds:0} sek bagud";
            Tempo.Foreground = (Brush)FindResource("TekstSvag");
        }

        OptagerPrik.Opacity = gået.Milliseconds < 500 ? 1.0 : 0.35;
    }

    /// <summary>Kaldes når vinduet lukkes, så en optagelse ikke tabes ved et uheld.</summary>
    public bool StopHvisIGang()
    {
        if (_session is null) return true;

        var svar = MessageBox.Show(
            "Der er en optagelse i gang. Vil du stoppe og gemme den, før appen lukkes?",
            "Optagelse i gang", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        if (svar == MessageBoxResult.Cancel) return false;
        if (svar == MessageBoxResult.Yes) StopOptagelse();
        return true;
    }
}
