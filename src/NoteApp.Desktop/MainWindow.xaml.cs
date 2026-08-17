using System.ComponentModel;
using System.Reflection;
using System.Windows;
using NoteApp.Core;
using NoteApp.Desktop.Dictionary;
using NoteApp.Desktop.Engine;
using NoteApp.Desktop.Documents;
using NoteApp.Desktop.Files;
using NoteApp.Desktop.Jobs;
using NoteApp.Desktop.Meeting;
using NoteApp.Desktop.Preferences;
using NoteApp.Desktop.ReadAloud;
using NoteApp.Desktop.Templates;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop;

public partial class MainWindow : Window
{
    private readonly MeetingView _moede = new();
    private readonly ReadAloudView _oplaesning = new();
    private readonly GlobalHotkey _genvej = new();
    private DictionaryView? _ordbog;

    /// <summary>Optagelsen, Optagelser-skærmen skal åbne på. Bruges én gang.</summary>
    private string? _aabnOptagelse;

    /// <summary>Dokumentet, Dokumenter-skærmen skal åbne på. Bruges én gang.</summary>
    private string? _aabnDokument;

    /// <summary>Id på det dokument, en kørsel netop har lavet.</summary>
    private string? _færdigtDokument;


    // ------------------------------------------------------------ kørselsbjælken

    /// <summary>
    /// Viser, hvad der kører. Bjælken bliver stående, når kørslen er færdig,
    /// indtil man selv skjuler den eller går hen og ser dokumentet.
    ///
    /// Det er hele pointen: en kvittering, der forsvinder af sig selv, når man
    /// står et andet sted i appen, er ikke en kvittering.
    /// </summary>
    private void VisJob(JobStatus s)
    {
        Dispatcher.Invoke(() =>
        {
            JobBjaelke.Visibility = Visibility.Visible;
            JobHvad.Text = s.Kører ? $"{s.Hvad} laves …" : s.Hvad + " færdigt";
            JobBesked.Text = s.Besked;

            JobDetaljer.Text = s.Detaljer;
            JobDetaljer.Visibility = s.Detaljer.Length == 0
                ? Visibility.Collapsed : Visibility.Visible;

            // Bjaelken vises kun, naar der ER noget at maale. Under
            // modelindlaesningen staar der ingen procent, og en bjaelke paa nul
            // i et halvt minut ligner en, der har haengt sig — dér er en
            // ubestemt bjaelke det aerlige svar.
            if (!s.Kører)
            {
                JobBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                JobBar.Visibility = Visibility.Visible;
                JobBar.IsIndeterminate = s.Procent < 0;
                if (s.Procent >= 0) JobBar.Value = s.Procent;
            }

            JobPrik.Fill = (System.Windows.Media.Brush)FindResource(s.Kører ? "Accent" : "Godkendt");
            JobAfbrydKnap.Visibility = s.Kører ? Visibility.Visible : Visibility.Collapsed;
            JobLukKnap.Visibility = s.Kører ? Visibility.Collapsed : Visibility.Visible;
            JobVisKnap.Visibility = !s.Kører && _færdigtDokument is not null
                ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void JobVis_Click(object sender, RoutedEventArgs e)
    {
        _aabnDokument = _færdigtDokument;
        JobBjaelke.Visibility = Visibility.Collapsed;

        if (NavDokumenter.IsChecked == true) Nav_Changed(this, new RoutedEventArgs());
        else NavDokumenter.IsChecked = true;
    }

    private void JobLuk_Click(object sender, RoutedEventArgs e) =>
        JobBjaelke.Visibility = Visibility.Collapsed;

    private void JobAfbryd_Click(object sender, RoutedEventArgs e)
    {
        var ja = Dialogs.AppDialog.Spoerg(this, "Afbryd kørslen?",
            "Der bliver ikke gemt noget dokument, og arbejdet skal gøres om.",
            godkend: "Afbryd", annuller: "Lad den køre færdig",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (ja) BackgroundJobs.Afbryd();
    }

    public MainWindow()
    {
        InitializeComponent();

        // Versionen paa appen kender vi — den staar i assemblyen. Det er kun
        // Whispers version, vi ikke kan aflaese; se WhisperInstall.
        Version.Text = "v" + (Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(2) ?? "0.0");
        DataSti.Text = UserDataPaths.Root;
        OpdaterDataLoefte();

        // Efter en oplæsning peger kvitteringen videre til transskription.
        // Skærmskiftet skal ske her, fordi det er MainWindow, der ejer
        // navigationen — og fordi menupunktet skal markeres med, ellers
        // skifter indholdet uden at menuen følger med.
        //
        // Stien gemmes, så Optagelser-skærmen kan åbne PÅ den optagelse, der
        // lige er lavet, og spørge, om den skal skrives ud.
        _oplaesning.TranskriptionØnskes += sti =>
        {
            _aabnOptagelse = sti;
            if (NavTransskriber.IsChecked == true) Nav_Changed(this, new RoutedEventArgs());
            else NavTransskriber.IsChecked = true;
        };

        // Når et møde begynder, BLIVER appen mødet. Alt andet er ligegyldigt i
        // den periode: man skal kunne skrive noter og se, at der optages — og
        // der skal være plads til at skrive, ikke et felt i en bjælke.
        _moede.Startet += () =>
        {
            Indhold.Content = new MeetingLiveView(_moede);
        };

        // Efter et møde peger appen samme vej som efter en oplæsning: hen til
        // optagelsen, med spørgsmålet om den skal skrives ud.
        _moede.FærdigMedMøde += sti =>
        {
            _aabnOptagelse = sti;
            NavTransskriber.IsChecked = true;
        };

        // Kørsler, der ikke hører til nogen skærm. Bjælken nederst er det
        // eneste sted, de kan ses fra — derfor bor den i vinduet og ikke i en
        // skærm, der bliver bygget om, hver gang man skifter menupunkt.
        BackgroundJobs.Ændret += VisJob;
        BackgroundJobs.DokumentFærdigt += id =>
        {
            _færdigtDokument = id;
            TilbydAtAabne(id);
        };

        // Optagebjælken ligger fast øverst og er den samme, uanset hvilken
        // skærm der vises. Den bygges én gang og bliver siddende — en optagelse
        // må aldrig kunne dø af, at man klikker på et menupunkt.
        OptagBjaelke.Content = _moede;

        // Startskærmen sættes HER, ikke af Nav_Changed. Menupunktet er markeret
        // fra XAML'en, men Checked fyrer under InitializeComponent, hvor
        // Indhold endnu er null — så handleren returnerer, og skærmen stod tom,
        // indtil man klikkede på et andet punkt og tilbage igen.
        Indhold.Content = _oplaesning;

        // Genvejstasten kobles på, når vinduet findes. Virker den ikke, skal
        // det siges — en genvej, der stille er død, opdages først den dag, man
        // trykker på den under et møde.
        Loaded += (_, _) =>
        {
            _genvej.Trykket += LynstartOptagelse;
            TilslutGenvej();
        };
    }

    /// <summary>
    /// Siger, at dokumentet er klart, og tilbyder at åbne det.
    ///
    /// HVORFOR DET IKKE ER NOK MED BJÆLKEN NEDERST
    ///
    /// Et referat tager fra tyve sekunder til en halv time. Man laver noget
    /// andet imens — også noget uden for appen. En knap, der dukker op i en
    /// bjælke, man ikke kigger på, bliver fundet en time senere, hvis den
    /// bliver fundet.
    ///
    /// Vinduet kommer derfor til brugeren. Det er det ene sted i appen, hvor
    /// en afbrydelse er rigtig: man har selv sat noget i gang og ventet på det.
    /// </summary>
    private void TilbydAtAabne(string id)
    {
        // Fyres fra en baggrundstraad. Uden Dispatcher rejser WPF en
        // InvalidOperationException i stedet for at vise noget.
        Dispatcher.Invoke(() =>
        {
            var doc = NoteApp.Core.Documents.DocumentStore.LoadAll().FirstOrDefault(d => d.Id == id);
            if (doc is null) return;

            var sti = NoteApp.Core.Documents.DocumentStore.Path_(doc);
            if (!System.IO.File.Exists(sti)) return;

            var aabn = Dialogs.AppDialog.Spoerg(this,
                "Dokumentet er klar",
                $"«{doc.Title}» er lavet og gemt under Dokumenter.",
                godkend: "Åbn dokumentet",
                annuller: "Senere",
                slags: Dialogs.Slags.Valg);

            if (!aabn) return;

            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(sti) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Dialogs.AppDialog.Vis(this, "Kunne ikke åbne dokumentet",
                    $"{ex.Message}\n\nFilen ligger her:\n{sti}", Dialogs.Slags.Valg);
            }
        });
    }

    /// <summary>
    /// Går til oplæsningsskærmen. Kaldes fra træningen, når en sætning skal
    /// læses op igen — dér er vejen videre ikke en knap på samme skærm, men et
    /// andet sted i appen.
    /// </summary>
    public void GaaTilOplaesning() => NavOplaesning.IsChecked = true;

    /// <summary>Går til historikken — hele listen bag klokkens beskeder.</summary>
    public void GaaTilHistorik() => NavHistorik.IsChecked = true;

    /// <summary>
    /// Registrerer genvejen og fortæller mødeskærmen, hvad der blev til noget.
    /// Kaldes igen, når valget ændres under Indstillinger.
    /// </summary>
    public void TilslutGenvej()
    {
        var ok = _genvej.Tilslut(this, AppSettings.Current.HotkeyId);
        _moede.VisGenvej(ok ? _genvej.Aktiv!.Navn : null, _genvej.Bemærkning);

        // Blev der valgt en anden end den oenskede, gemmes den. Ellers ville
        // appen proeve den optagede igen ved hver opstart og skifte hver gang.
        if (ok && _genvej.Aktiv!.Id != AppSettings.Current.HotkeyId)
        {
            AppSettings.Current.HotkeyId = _genvej.Aktiv.Id;
            AppSettings.Current.Save();
        }
    }

    /// <summary>
    /// Genvejstasten er trykket. Vinduet hentes frem, og optagelsen går i gang
    /// med det samme — man skal ikke først finde den rigtige skærm.
    /// </summary>
    private void LynstartOptagelse()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();

        _moede.Lynstart();
    }

    /// <summary>
    /// Løftet nederst i sidebjælken. Skal sige det, der faktisk gælder.
    ///
    /// «Intet forlader denne pc» stod fast, og det var rigtigt, så længe der
    /// ikke fandtes en vej ud. Der gør der nu — og et løfte, koden selv kan
    /// bryde, koster mere end det, det vinder. Derfor to tekster, og den ene
    /// er lige så præcis som den anden.
    ///
    /// Bemærk, at teksten IKKE siger «dine data sendes til Frankrig». Det ville
    /// være lige så forkert den anden vej: der sendes intet, før man sætter et
    /// hak på et bestemt dokument. Den siger, hvad der KAN ske, og hvad der
    /// skal til.
    /// </summary>
    public void OpdaterDataLoefte()
    {
        // DET ENE STED, ARKITEKTUREN HOERER HJEMME.
        //
        // Den staar her frem for i dokumentdialogen, fordi det er en egenskab
        // ved INSTALLATIONEN, ikke ved det enkelte referat. Skrevet paa hvert
        // dokument ligner det noget, der varierer; skrevet her, hvor "Dine
        // data" i forvejen staar, er det det, det er.
        //
        // Begge halvdele skal med. "Optagelser bliver her" alene fortier den
        // ene halvdel af loesningen, og en halv sandhed om databehandling er
        // den slags, der bliver opdaget af en anden end en selv.
        DataLoefte.Text = NoteApp.Core.Llm.SkyNoegle.Hent() is null
            ? "Intet forlader denne pc. Appen har ingen netværkskald."
            : "Optagelser og udskrifter bliver på denne pc. Referater bearbejdes i EU.";
    }

    private void Nav_Changed(object sender, RoutedEventArgs e)
    {
        // Konstruktøren kører før felterne er sat op; RadioButton.Checked
        // fyrer under InitializeComponent.
        if (Indhold is null) return;

        // Skærmene bygges først, når de vises. Ordbogen åbner en
        // databaseforbindelse, og motorskærmen leder efter filer på disken —
        // ingen af delene skal ske, mens man bare vil optage.
        if (NavOrdbog.IsChecked == true)
        {
            _ordbog ??= new DictionaryView();
            Indhold.Content = _ordbog;
        }
        else if (NavTransskriber.IsChecked == true)
        {
            // Bygges hver gang: listen over optagelser skal vise den, der
            // netop er lavet, uden at nogen skal genstarte appen.
            var aabn = _aabnOptagelse;
            _aabnOptagelse = null;          // gælder kun dette skift

            Indhold.Content = new TranscribeView(aabn);
        }
        else if (NavDokumenter.IsChecked == true)
        {
            var id = _aabnDokument;
            _aabnDokument = null;
            Indhold.Content = new DocumentsView(id);
        }
        else if (NavSkabeloner.IsChecked == true)
        {
            // Bygges hver gang: skabelonerne er filer, og de kan være rettet i
            // en editor siden sidst.
            Indhold.Content = new TemplatesView();
        }
        else if (NavMotor.IsChecked == true)
        {
            Indhold.Content = new EngineView();
        }
        else if (NavFiler.IsChecked == true)
        {
            Indhold.Content = new FilesView();
        }
        else if (NavHistorik.IsChecked == true)
        {
            // Bygges hver gang: historikken skal vise det, der lige er sket.
            Indhold.Content = new History.HistoryView();
        }
        else if (NavIndstillinger.IsChecked == true)
        {
            // Bygges hver gang: enhedslisten skal vise det, der er tilsluttet
            // NU, ikke da appen startede.
            Indhold.Content = new SettingsView();
        }
        else
        {
            Indhold.Content = _oplaesning;
        }
    }

    /// <summary>
    /// En optagelse i gang må ikke tabes, fordi vinduet lukkes. Det er
    /// 20 minutters oplæsning, og der er ingen fortrydelse.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_moede.StopHvisIGang() || !_oplaesning.StopHvisIGang()) { e.Cancel = true; return; }

        _genvej.Dispose();
        base.OnClosing(e);
    }
}
