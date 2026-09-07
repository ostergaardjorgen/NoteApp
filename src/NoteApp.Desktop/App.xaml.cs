using System.IO;
using System.Windows;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop;

public partial class App : Application
{
    /// <summary>
    /// Windows' egen fejlkasse, når et hjælpeprogram ikke kan starte.
    /// </summary>
    /// <remarks>
    /// SEM_FAILCRITICALERRORS. Fejltilstanden arves af de programmer, appen
    /// starter — whisper-cli og de andre. Uden den svarer Windows selv med en
    /// kasse om en manglende DLL-fil, og PROCESSEN BLIVER STAAENDE I DEN,
    /// indtil nogen trykker OK. Appen ventede imens paa et program, der ikke
    /// var i gang med noget.
    ///
    /// Set 07-09-2026 paa en frisk Windows uden Microsofts C++-komponent:
    /// «whisper-cli.exe - Systemfejl: VCOMP140.DLL blev ikke fundet», mens
    /// appen skrev «Skriver teksten ud …» i det uendelige.
    ///
    /// Nu fejler programmet med det samme, og vi kan sige hvorfor med vores
    /// egne ord — se <see cref="NoteApp.Core.Cppkomponent"/>.
    /// </remarks>
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOOPENFILEERRORBOX = 0x8000;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try { SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX); }
        catch (Exception) { /* Er den ikke der, er alt som foer. */ }

        // ============ HEMMELIGHEDERNE BESKYTTES FOERSTE GANG ============
        //
        // Mistral-noeglen og Googles opdateringsnoegle laa som ren tekst i
        // datamappen. Filerne ligger uden for git og uden for OneDrive - det
        // beskytter mod at DELE dem ved et uheld, ikke mod at nogen laeser
        // dem. Enhver proces, der koerer som brugeren, kunne aabne dem; det
        // samme kunne enhver, der fik fat i en sikkerhedskopi af mappen.
        //
        // De bindes nu til Windows-brugerens egen noegle. Skiftet sker HER,
        // ved opstart, og kun én gang: brugeren har sat noeglen ind og
        // forbundet Google én gang og skal ikke goere det igen, fordi
        // lagringen blev bedre.
        //
        // DEN NYE FIL SKRIVES, FOER DEN GAMLE ER VAEK. En migrering, der
        // fejler halvvejs, maa aldrig koste adgangen - saa ville en app, der
        // ville beskytte noget, i stedet have taget det.
        try
        {
            Core.Opstartsbeskyttelse.Koer();
        }
        catch (Exception)
        {
            // Kan hemmelighederne ikke skiftes over, koerer appen videre paa
            // de gamle filer. Beskyttelsen er en forbedring, ikke en
            // betingelse for at komme i gang.
        }


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

        // ============ DEMOEN BYGGES HER, OG KUN HER ============
        //
        // Generatoren skriver gennem appens egne lagre, og de skriver dér, hvor
        // UserDataPaths.Root peger hen. Paa det her tidspunkt peger den paa
        // demomappen - flaget blev sat, FOER appen genstartede - og saa kan
        // eksemplerne ikke lande i nogens rigtige optagelser.
        //
        // Se Demotilstand for hele raekkefoelgen.
        if (Demotilstand.Taendt && !Demodata.Findes)
        {
            try
            {
                Demodata.Byg();
            }
            catch (Exception ex)
            {
                // En demo, der ikke kunne bygges, maa ikke spaerre appen. Der
                // skiftes tilbage til brugerens egne data - saa staar man ikke
                // med en tom app og en knap, der siger «tilbage til dine data»
                // uden at have vist noget.
                Demotilstand.Sluk();

                MessageBox.Show(
                    "Demodataene kunne ikke bygges, saa appen aabner dine egne data.\n\n"
                    + ex.Message,
                    "Demotilstand", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        DispatcherUnhandledException += VisFejl;

        // ============ HJÆLPEPROGRAMMER FRA SIDST RYDDES ============
        //
        // Vaageordet lytter med whisper-command.exe. Blev appen draebt frem
        // for lukket - og det bliver den ved hver udgivelse - levede den
        // videre med sin model i hukommelsen og sit greb om grafikkortet.
        //
        // Maalt 29-08-2026: TRE af dem samtidig, den aeldste tretten timer
        // gammel, tilsammen 2,4 GB og 94 % af grafikkortet, mens vaageordet
        // var slaaet FRA. Maskinen var maerkbart langsom, og intet i appen
        // viste hvorfor.
        //
        // Nye foraeldreloese forhindres af jobobjektet i Boernejob. Den her
        // rydder op efter dem, der allerede findes.
        try
        {
            var lukket = Boernejob.RydForaeldreloese(
                WhisperInstall.Root, "whisper-command", "whisper-cli");

            if (lukket > 0)
            {
                Historik.Skriv(HaendelseType.Andet, "Hjælpeprogrammer fra sidste kørsel er lukket",
                    $"{lukket} program(mer) kørte stadig fra en tidligere start og brugte "
                    + "hukommelse og grafikkort uden at lave noget. De er lukket.",
                    Udfald.Fuldført);
            }
        }
        catch (Exception)
        {
            // En oprydning maa aldrig kunne forhindre en opstart.
        }

        // ============ EN NULSTILLING SKAL KUNNE SES ============
        //
        // Kunne indstillingerne ikke laeses, opdagede man det foer ved, at
        // velkomstforloebet kom igen - og saa var mikrofon, sprog og
        // genvejstast allerede skrevet over med standarden. Der stod
        // ingenting nogen steder. Nu staar det i historikken.
        //
        // Foerst her, og ikke inde i indlaesningen: Historik laeser selv
        // indstillinger, og et kald tilbage i sig selv gaar i ring ved opstart.
        if (AppSettings.Indlaesningsfejl is { } besked)
        {
            try
            {
                Historik.Skriv(HaendelseType.Andet, "Indstillingerne kunne ikke læses",
                    besked, Udfald.SeEfter);
            }
            catch (Exception)
            {
                // Kan historikken ikke skrives, er der ikke mere at goere her.
            }
        }

        // LYDEN RYDDES OP VED OPSTART, ikke mens man arbejder.
        //
        // Det er den eneste stund, hvor ingenting er i gang: ingen optagelse,
        // ingen udskrift, ingen aaben fil. En sletning midt i en arbejdsdag
        // ville ramme netop den fil, der var i brug.
        //
        // Den siger ingenting. Et vindue ved opstart om noget, brugeren har
        // besluttet én gang under Indstillinger, er stoej - og tallet staar
        // paa Filer-fanen, naar man vil se det.
        try
        {
            var dage = AppSettings.Current.SletLydEfterDage;
            if (dage > 0) Lydoprydning.Ryd(dage);
        }
        catch (Exception)
        {
            // En oprydning maa aldrig kunne forhindre appen i at aabne.
        }

        // NAVNESKIFTET RYDDES OP EFTER. Den gamle autostart peger paa
        // NoteApp.exe, som ikke findes mere - og valget skal foelge med, ikke
        // gaa tabt. Se Autostart.RydGamleNavne.
        Autostart.RydGamleNavne();

        // LYST ELLER MOERKT - foer noget som helst tegnes.
        //
        // Penslerne i App.xaml er allerede lavet paa det her tidspunkt, og
        // her faar de deres rigtige farve. Sker det senere, naar man at se et
        // glimt af det forkerte tema, og det er den slags, der faar en app til
        // at virke sjusket uden at man kan sige hvorfor.
        Temaskift.Anvend();

        // Titellinjen paa ALLE vinduer - ogsaa dem, der endnu ikke findes.
        // Skal staa foer det foerste vindue aabner; opsaetningsvinduet lige
        // nedenfor er det foerste, og det skal ogsaa foelge temaet.
        Vinduesramme.SlaaTil();

        // GLIDNINGEN PAA ALLE VINDUER - ogsaa dem, der endnu ikke findes.
        //
        // Den staar her og ikke i hver enkelt konstruktoer. Fjorten vinduer
        // med hver sin linje er fjorten steder at glemme den, og det femtende
        // vindue, nogen laver om et halvt aar, ville komme uden.
        //
        // Hovedvinduet holdes udenfor. Det popper ikke op - det ER appen, og
        // en optoning ved opstart er ventetid, ikke elegance.
        EventManager.RegisterClassHandler(
            typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((afsender, _) =>
            {
                // Fuldt navn med vilje: inde i App er «MainWindow» navnet paa
                // Applications egen egenskab, ikke paa vores vinduestype.
                if (afsender is Window v && v is not global::NoteApp.Desktop.MainWindow)
                    Glid.Vindue(v);
            }));

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
    /// Henter et vindue rigtigt frem.
    ///
    /// DER ER TRE TRIN, OG DE ER ALLE TRE NOEDVENDIGE.
    ///
    /// Show() alene er ikke nok. Var vinduet minimeret - fx startet med
    /// Windows, eller lagt ned af brugeren - bliver det bare ved at vaere
    /// minimeret, og det ligner, at der ikke skete noget. Og har et ANDET
    /// program fokus, lander vinduet bag det uden Activate(); saa blinker der
    /// kun en knap i proceslinjen.
    ///
    /// DEN HER STAAR ET STED, OG KUN ET STED.
    ///
    /// Sekvensen fandtes foer i tre kopier: genvejstasten, spaerren mod to
    /// instanser, og vejen tilbage fra optagebaandet. Tre kopier af det samme
    /// kommer ud af trit - rettes den ene, glemmes de to.
    /// </summary>
    public static void HentFrem(Window? v)
    {
        if (v is null) return;

        if (v.WindowState == WindowState.Minimized)
            v.WindowState = WindowState.Normal;

        v.Show();
        v.Activate();
    }

    /// <summary>
    /// Henter vinduet frem, når nogen forsøger at starte appen igen.
    ///
    /// Kaldes fra en baggrundstråd, så alt skal over på UI-tråden først.
    /// </summary>
    private void KomFrem() => Dispatcher.Invoke(() => HentFrem(MainWindow));

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
        // EN LÅST FIL ER IKKE «NOGET GALT» — DET ER EN FIL, DER ER ÅBEN.
        //
        // «Access to the path is denied» siger ingenting om, hvad man skal
        // gøre. Ni ud af ti gange er svaret, at dokumentet står åbent i Word,
        // og så kan filen ikke skrives om. Det skal beskeden sige, for det er
        // det eneste, brugeren kan handle på.
        var laast = e.Exception is UnauthorizedAccessException or IOException;

        Dialogs.AppDialog.Vis(MainWindow,
            laast ? "Filen er i brug" : "Der gik noget galt",
            laast
                ? "Filen kunne ikke skrives, fordi et andet program har den åben — " +
                  "som regel Word.\n\nLuk dokumentet, og prøv igen.\n\n" +
                  $"{e.Exception.Message}"
                : $"{e.Exception.Message}\n\n{e.Exception.GetType().Name}",
            Dialogs.Slags.Fejl);

        e.Handled = true;
    }
}
