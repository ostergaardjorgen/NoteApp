using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Engine;

/// <summary>
/// Hvad appen kører på — de to lag, og hvad der mangler.
///
/// SKÆRMEN VISER, DEN VÆLGER IKKE
///
/// Her lå to faner: seks Whisper-modeller og et katalog af lokale
/// sprogmodeller, med fordele og ulemper ved hver. Det er væk 18-08-2026.
///
/// Grunden er målt, ikke principiel. Den lokale sprogmodel tabte 72 % af
/// navnene i et rigtigt møde og brugte 59 minutter; Mistral Medium 3.5 tabte
/// 23 % og brugte 25 sekunder for 21 øre (doc/maaling-sky.md). De små
/// Whisper-modeller er hurtigere og mærkbart dårligere på dansk, og de
/// engelske kan slet ikke dansk. Et valg, hvor hvert alternativ gør
/// resultatet ringere, hjælper ingen — det flytter bare ansvaret for en
/// dårlig transskription over på den, der ikke har tallene.
///
/// Tilbage står hentning: motoren og modellen skal på maskinen, og det er
/// stadig det eneste sted i appen, der rører netværket. Det sker aldrig af
/// sig selv — brugeren trykker, ser hvad der hentes og hvorfra, og siger ja.
/// </summary>
public partial class EngineView : System.Windows.Controls.UserControl
{
    private readonly Downloader _downloader = new();
    private CancellationTokenSource? _afbryd;

    public EngineView()
    {
        InitializeComponent();
        Opdater();
    }

    private void Opdater()
    {
        VisModelvalg();

        var s = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
        var model = WhisperInstall.Model(AppSettings.Current.PreferredModel ?? "")
                    ?? WhisperInstall.Standard;

        // DER STAAR DEN FIL, DER FAKTISK INDLAESES - IKKE INDSTILLINGEN.
        //
        // De to kan vaere forskellige, og forskellen er ikke teoretisk:
        // modeller soeges tre steder, og en fil i repoets models-mappe vinder
        // over ingenting i datamappen. Stod der bare, hvad indstillingen
        // oenskede, ville skaermen svare paa et andet spoergsmaal end det,
        // man har - og man ville ikke opdage, at filen laa et andet sted.
        // "Whisper" foran modelnavnet. Uden det stod der bare "large-v3", og
        // saa kunne skaermen ikke svare paa, HVAD der skriver lyden ud.
        LydModel.Text = s.ModelPath is null
            ? $"{model.Id} — ikke hentet"
            : Path.GetFileNameWithoutExtension(s.ModelPath).Replace("ggml-", "");

        LydResume.Text = model.Summary + " " + model.Pros;

        MotorNavn.Text = s.WhisperCli is null ? "ikke installeret" : "whisper.cpp";

        // Motoren staar fremme, ikke kun i foldebjaelken. Det er to ting -
        // programmet, der koerer modellen, og modelfilen selv - og begge har
        // hver sin licens at goere rede for.
        // VERSION OG DATO SKAL STAA FREMME.
        //
        // De laa kun i foldebjaelken. Naar man lige HAR trykket "Opdatér
        // motoren", er versionen praecis det, man vil se - og uden den kan
        // man ikke afgoere, om knappen gjorde noget.
        LydMotor.Text = s.WhisperCli is null ? "whisper.cpp — ikke installeret" : "whisper.cpp";

        var dato = s.EngineInstalled is { } i
            ? $" · installeret {i.ToLocalTime():d. MMMM yyyy 'kl.' HH:mm}"
            : "";

        LydMotorLinje.Text = s.WhisperCli is null
            ? "Programmet, der afvikler modellen. Hentes for sig, uafhængigt af modellen."
            : s.EngineVersion is { } v
                ? $"Version {v}{dato} · kører på {s.Engine}."
                : $"Version ukendt — motoren er ikke hentet af appen selv{dato}. Kører på {s.Engine}.";


        // Version hvis vi kender den, ellers datoen paa binaeren. Der staar
        // aldrig "ukendt": et felt, der ikke kan svare, skal stille et andet
        // spoergsmaal, ikke vise sin egen uvidenhed.
        var (label, vaerdi) = s.AgeLine;
        AlderLabel.Text = label;
        MotorVersion.Text = s.WhisperCli is null ? "—" : vaerdi;
        MotorVersion.Foreground = (Brush)FindResource("Tekst");

        MotorBeregning.Text = s.WhisperCli is null ? "—" : s.Engine;
        ModelNavn.Text = s.ModelFileName ?? "ingen model hentet";

        MotorSti.Text = s.WhisperCli is null
            ? $"Motoren hentes til {WhisperInstall.EngineDirectory}"
            : $"{s.WhisperCli}\nModeller: {WhisperInstall.ModelDirectory}";

        // KNAPTEKSTEN MAA IKKE LOVE EN FORBEDRING.
        //
        // "Hent igen" ved siden af "Opdatér motoren" laeses let, som om der
        // ogsaa kommer en nyere model ud af det. Det goer der ikke: large-v3
        // er en fast fil med et fast indhold, og en ny hentning giver praecis
        // den samme. Knappen hedder derfor "Hent modellen igen", og linjen
        // under kassen siger hvorfor.
        LydKnap.Content = s.ModelPath is null ? $"Hent {model.SizeText}" : "Hent modellen igen";

        ModelNote.Text = s.ModelPath is null
            ? "Modellen er en fast fil. Den hentes én gang og ændrer sig ikke bagefter."
            : "Modellen er en fast fil og får ikke nye udgaver. Det samme gælder motoren: " +
              "begge dele hører til denne udgave af appen og skiftes kun med den. " +
              "At hente igen giver præcis de samme filer og gør ikke transkriptionen bedre — " +
              "det er kun værd at gøre, hvis noget er blevet beskadiget.";
        // MOTORKNAPPEN VAR SLAAET FRA, NAAR MOTOREN MANGLEDE.
        //
        // "IsEnabled = WhisperCli is not null" betoed, at den ENESTE knap,
        // der kan installere motoren, var graa praecis naar den ikke var
        // installeret. Motoren og modellen hentes hver for sig - det ene
        // foelger ikke med det andet - saa uden motor var der ingen vej frem
        // fra denne skaerm. Fundet 19-08-2026.
        MotorKnap.Content = s.WhisperCli is null ? "Hent motoren" : "Installér motoren igen";

        // Hvor filen ligger, staar fremme og ikke i en foldbar. Ligger den i
        // repoets models-mappe frem for datamappen, er det vaerd at vide -
        // den flytter ikke med en sikkerhedskopi.
        LydStatus.Text = s.ModelPath is null
            ? "Modellen er ikke hentet. Uden den kan appen ikke skrive optagelser ud."
            : s.WhisperCli is null
                ? "Motoren mangler. Tryk «Hent motoren» — den hentes for sig, uafhængigt af modellen."
                : $"I brug: {s.ModelPath}";

        Status.Text = "";
        VisSkyStatus();
        VisTalergenkendelse();
    }

    // ------------------------------------------------- talergenkendelsen

    /// <summary>Én komponent på talergenkendelsens fane.</summary>
    public sealed record Modeldel(string Slags, string Navn, string Note, string Stoerrelse);

    /// <summary>
    /// Talergenkendelsens tre dele: programmet og de to modeller.
    ///
    /// De står hver for sig, fordi de har hver sin rettighedshaver og derfor
    /// hver sin licens — præcis som motoren og modelfilen gør under lyd til
    /// tekst. Licenserne står under «Compliance».
    ///
    /// FILERNE FØLGER MED APPEN. De hentes ikke, og de kan ikke skiftes ud
    /// herfra: tærsklen, stemmerne skilles ad ved, er målt mod netop de to
    /// modelfiler, og en anden fil ville gøre målingen ugyldig.
    /// </summary>
    private void VisTalergenkendelse()
    {
        var dele = new List<Modeldel>();

        void Tilfoej(string slags, string navn, string note, string? sti)
        {
            var mb = sti is not null && File.Exists(sti)
                ? $"{new FileInfo(sti).Length / 1024.0 / 1024.0:0.0} MB"
                : "mangler";

            dele.Add(new Modeldel(slags, navn, note, mb));
        }

        Tilfoej("PROGRAM", "sherpa-onnx",
            "Kører de to modeller og samler stemmerne i grupper.", Diarisering.Vaerktoej());

        Tilfoej("MODEL 1", "pyannote segmentation 3.0",
            "Finder hvornår der bliver talt, og hvornår der skiftes taler.", Diarisering.Segmentering());

        Tilfoej("MODEL 2", "NVIDIA TitaNet",
            "Afgør om to stykker tale kommer fra den samme stemme.", Diarisering.Stemmemodel());

        Talerdele.ItemsSource = dele;

        TalerStatus.Text = Diarisering.ErInstalleret
            ? "Klar. Den kører af sig selv, hver gang en optagelse bliver skrevet ud."
            : "Ikke fuldstændig — transkriptioner får ingen navne på talerne, før filerne er på plads.";

        TalerSti.Text = $"Filerne følger med appen og ligger i {Diarisering.Mappe}";
    }

    // ------------------------------------------------ den lokale opsummering

    // HER LAA VisLokalModel og LokalModel_Klik - fanen for den lokale
    // opsummering.
    //
    // Fjernet 25-08-2026 sammen med motoren og modellen. Se
    // Core/Llm/Opsummeringsvej: graensen gaar nu ved transkriptionen, og alt
    // derefter sker hos leverandoeren.


    // ---------------------------------------------------------------- Europa

    /// <summary>
    /// Tilstanden for den europæiske bearbejdning.
    ///
    /// Der står, hvad der ER sat op, ikke hvad man kunne. «Ikke sat op» er en
    /// oplysning; «prøv vores skyløsning» er en reklame, og den hører ikke
    /// hjemme i en oversigt over, hvad appen kører på.
    /// </summary>
    private void VisSkyStatus()
    {
        var tilsluttet = SkyNoegle.Hent() is not null;

        SkyModel.Text = SkyKatalog.Standard.Navn;
        SkyKnap.Content = tilsluttet ? "Ret opsætning …" : "Sæt op …";

        SkyStatus.Text = tilsluttet
            ? $"Tilsluttet {new Uri(SkyKatalog.Endpoint).Host}. Dokumenter laves her."
            : "Ikke sat op endnu. Uden den kan appen optage og skrive ud, men ikke lave dokumenter.";

        // Vejledningen staar kun, naar der mangler noget. En vejledning til
        // det, der allerede er gjort, laerer man at laese forbi - og saa
        // laeser man ogsaa forbi den dag, der staar noget nyt.
        SkyVejledning.Visibility = tilsluttet ? Visibility.Collapsed : Visibility.Visible;

        // Maerkatet i menuen foelger den samme tilstand. Bliver noeglen sat
        // her, skal 1-tallet vaek med det samme og ikke ved naeste opstart.
        (Application.Current.MainWindow as MainWindow)?.OpdaterOpsaetningsmaerkat();
    }

    private void AabnKonsol_Klik(object sender, RoutedEventArgs e) => Aabn("https://console.mistral.ai");
    private void AabnPriser_Klik(object sender, RoutedEventArgs e) => Aabn("https://mistral.ai/pricing");

    private void Aabn(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // En browser, der ikke vil aabne, maa ikke ende som en tom
            // knap. Adressen staar der, saa den kan skrives af i haanden.
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne browseren",
                $"Gå til denne adresse i din browser:\n\n{url}", Dialogs.Slags.Valg);
        }
    }

    private void Sky_Klik(object sender, RoutedEventArgs e)
    {
        new Documents.SkySetupWindow { Owner = Window.GetWindow(this) }.ShowDialog();

        // Loeftet i sidebjaelken opdaterer vinduet selv - se SkySetupWindow.Luk.
        VisSkyStatus();
    }

    // ==================== VALGET MELLEM DE TO MODELLER ====================
    //
    // De loeser to forskellige problemer. large-v3 er den, der er MAALT paa
    // dansk her i projektet; turbo er den, der kan koere paa en maskine, hvor
    // den store ikke kan. Hvilket af dem der er det vigtigste, ved appen
    // ikke - det afhaenger af maskinen, og af om man venter paa svaret.
    //
    // Der var ikke noget valg foer. Det var rigtigt, saa laenge alternativerne
    // bare var daarligere; her er de ikke daarligere, de er ANDERLEDES.

    /// <summary>Én model, som den skal stå på skærmen.</summary>
    public sealed record Modelraekke(
        string Id, string Navn, string Linje, string Godt, string Skidt,
        string Knap, bool KanVaelges, string Bruges,
        Visibility BrugesSynlig, Brush Flade, Brush Kant);

    private void VisModelvalg()
    {
        var valgt = AppSettings.Current.PreferredModel ?? WhisperInstall.Standard.Id;
        var harGpu = EngineInstaller.HasNvidiaGpu();

        Modelvalg.ItemsSource = WhisperInstall.Models.Select(m =>
        {
            var erValgt = string.Equals(m.Id, valgt, StringComparison.OrdinalIgnoreCase);
            var erHentet = File.Exists(WhisperInstall.ModelDestination(m));
            var erStor = m.Id == "large-v3";

            var linje = Sprog.T("model.linje", m.SizeText,
                Sprog.T(erStor ? "model.kraever3gb" : "model.kraever15gb"));

            var godt = Sprog.T(erStor ? "model.storgodt" : "model.turbogodt");

            // ANBEFALINGEN AFHAENGER AF MASKINEN, og det staar der. Den store
            // paa en maskine uden grafikkort er ikke "lidt langsommere" -
            // den er ubrugelig til daglig brug.
            var skidt = erStor
                ? Sprog.T(harGpu ? "model.storskidt" : "model.storskidtudengpu")
                : Sprog.T("model.turboskidt");

            var knap = erValgt
                ? Sprog.T("model.ibrug")
                : erHentet ? Sprog.T("model.skifttil") : Sprog.T("model.hentogskift");

            return new Modelraekke(
                m.Id, m.Id, linje, godt, skidt, knap,
                KanVaelges: !erValgt,
                Bruges: Sprog.T("model.bruges"),
                BrugesSynlig: erValgt ? Visibility.Visible : Visibility.Collapsed,
                Flade: (Brush)(erValgt ? FindResource("Panel") : FindResource("Baggrund")),
                Kant: (Brush)(erValgt ? FindResource("Accent") : FindResource("PanelKant")));
        }).ToList();
    }

    private async void Modelvalg_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        if (WhisperInstall.Model(id) is not { } model) return;

        var sti = WhisperInstall.ModelDestination(model);

        if (!File.Exists(sti))
        {
            // ER DEN IKKE HENTET, ER SKIFTET EN HENTNING. Det siges FOER,
            // ikke opdages undervejs - der er halvanden til tre gigabyte
            // paa spil.
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                Sprog.T("model.hentfoerst", model.Id),
                Sprog.T("model.hentfoersttekst", model.SizeText),
                Sprog.T("model.hentnu"), slags: Dialogs.Slags.Valg);

            if (!ja) return;

            await HentAsync(model, sti);

            if (!File.Exists(sti)) return;
        }

        AppSettings.Current.PreferredModel = model.Id;
        AppSettings.Current.Save();

        Status.Text = Sprog.T("model.skiftet", model.Id);

        Opdater();
        VisModelvalg();
    }

    // -------------------------------------------------------------- hentning

    private async void Lyd_Klik(object sender, RoutedEventArgs e)
    {
        var model = WhisperInstall.Model(AppSettings.Current.PreferredModel ?? "")
                    ?? WhisperInstall.Standard;

        // Krav 3 fra datagraensen: en informeret godkendelse. Hvad hentes,
        // hvorfra, hvor meget - og hvad det betyder - FOER der spoerges.
        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Hent {model.Id}?",
            $"Fil: {model.FileName}\n" +
            $"Størrelse: {model.SizeText}\n" +
            $"Hentes fra: huggingface.co\n" +
            $"Gemmes i: {WhisperInstall.ModelDirectory}\n\n" +
            "Der sendes intet fra din maskine. Appen beder om en navngiven fil og " +
            "modtager den; ingen optagelser eller noter forlader pc'en.",
            godkend: $"Hent {model.SizeText}",
            annuller: "Ikke nu",
            slags: Dialogs.Slags.Valg);

        if (!ja) return;

        await HentAsync(model, WhisperInstall.ModelDestination(model));
    }

    /// <summary>
    /// Slår op, om der er en nyere whisper.cpp — og henter kun, hvis der er.
    ///
    /// DET KAN LADE SIG GØRE, OG DET BLEV ANTAGET AT DET IKKE KUNNE
    ///
    /// Antagelsen var, at man ikke kan spørge, om der er kommet en ny udgave,
    /// og derfor må hente for at finde ud af det. Det passer ikke. whisper.cpp
    /// udgives på github, og opslaget efter den seneste udgivelse er nogle få
    /// kilobyte JSON med et versionsnummer i. Det, der IKKE kan lade sig gøre,
    /// er at læse versionen ud af de filer, der allerede ligger på disken —
    /// whisper.cpp stempler hverken sin exe eller sit output. Derfor skriver
    /// appen selv et manifest, når den installerer, og det er det, der
    /// sammenlignes med.
    ///
    /// Konsekvensen: er motoren lagt der i hånden, findes der intet manifest,
    /// og så kan der ikke sammenlignes. Det siges, frem for at gætte.
    ///
    /// HVORFOR DER STADIG SPØRGES FØRST
    ///
    /// En nyere whisper.cpp kan ændre transskriptionen til det bedre ELLER
    /// til det værre, og forskellen kan kun ses ved at måle den. Det er ikke
    /// noget, appen skal gøre bag om ryggen på en, der har et møde i morgen.
    /// </summary>
    private async void Motor_Klik(object sender, RoutedEventArgs e)
    {
        var s = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
        var nuvaerende = s.EngineVersion;

        MotorKnap.IsEnabled = false;
        Status.Text = "Henter oplysninger om motoren …";

        EngineRelease nyeste;
        try
        {
            nyeste = await EngineInstaller.FetchAsync();
        }
        catch (Exception ex)
        {
            Status.Text = $"Kunne ikke slå op: {ex.Message}";
            MotorKnap.IsEnabled = true;
            return;
        }
        finally
        {
            MotorKnap.IsEnabled = true;
        }

        var build = EngineInstaller.Recommend(nyeste);
        if (build is null)
        {
            Status.Text = $"Udgivelsen {nyeste.Version} har ingen udgave, der passer til denne maskine.";
            return;
        }

        // KØRER DEN RIGTIGE UDGAVE ALLEREDE, ER DET EN REPARATION.
        //
        // Her stod før «Motoren er den nyeste», og knappen ved siden af hed
        // «Opdatér motoren». Begge dele byggede på, at appen hentede den
        // nyeste whisper.cpp, når nogen bad om det. Det er fjernet: motoren er
        // låst til én udgave, som appens adfærd er målt imod.
        //
        // Tilbage står den fejl, der faktisk kan ske — at filerne på disken er
        // gået i stykker. Den kan man komme ud af ved at hente dem igen, og
        // det skal siges som det, det er, frem for som en opdatering.
        var ja = nuvaerende is not null && nuvaerende == nyeste.Version
            ? Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Installér motoren igen?",
                $"Du kører {nuvaerende} — den udgave, appen er lavet til, og der findes " +
                "ingen nyere at skifte til herfra.\n\n" +
                $"Fil: {build.FileName}\n" +
                $"Størrelse: {build.SizeText}\n" +
                "Hentes fra: github.com\n\n" +
                "Det giver præcis de samme filer og gør ikke transkriptionen bedre. Det er " +
                "kun værd at gøre, hvis motoren er holdt op med at virke — for eksempel " +
                "hvis en fil er blevet beskadiget.",
                godkend: $"Hent {build.SizeText} igen",
                annuller: "Ikke nu",
                slags: Dialogs.Slags.Valg,
                godkendErStandard: false)

            : Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                $"Hent motoren ({nyeste.Version})?",
                (nuvaerende is null
                    ? "Motoren er ikke installeret af appen, så det, der eventuelt ligger, kan ikke aflæses.\n"
                    : $"Du kører: {nuvaerende}\n") +
                $"Appen er lavet til: {nyeste.Version}\n\n" +
                $"Fil: {build.FileName}\n" +
                $"Størrelse: {build.SizeText}\n" +
                "Hentes fra: github.com\n\n" +
                "Uden motoren kan optagelser ikke skrives ud.",
                godkend: $"Hent {build.SizeText}",
                annuller: "Ikke nu",
                slags: Dialogs.Slags.Valg);

        if (!ja) return;

        _afbryd = new CancellationTokenSource();
        Fremdrift.Visibility = Visibility.Visible;
        AfbrydKnap.Visibility = Visibility.Visible;

        try
        {
            var fremdrift = new Progress<DownloadProgress>(p =>
            {
                Fremdrift.Value = p.Percent;
                Status.Text = $"Henter motoren: {p.BytesDone / 1024.0 / 1024.0:0} af {p.BytesTotal / 1024.0 / 1024.0:0} MB";
            });

            await EngineInstaller.InstallAsync(build, nyeste.Version, fremdrift,
                new Progress<string>(m => Status.Text = m), _afbryd.Token);

            Status.Text = $"Motoren er opdateret til {nyeste.Version}.";
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Afbrudt. Motoren er uændret.";
        }
        catch (Exception ex)
        {
            Status.Text = $"Kunne ikke hente motoren: {ex.Message}";
        }
        finally
        {
            Fremdrift.Visibility = Visibility.Collapsed;
            AfbrydKnap.Visibility = Visibility.Collapsed;
            _afbryd?.Dispose();
            _afbryd = null;
            Opdater();
        }
    }

    private async Task HentAsync(WhisperModel model, string destination)
    {
        _afbryd = new CancellationTokenSource();
        Fremdrift.Visibility = Visibility.Visible;
        AfbrydKnap.Visibility = Visibility.Visible;
        var fremdrift = new Progress<DownloadProgress>(p =>
        {
            Fremdrift.Value = p.Percent;
            var mb = p.BytesDone / 1024.0 / 1024.0;
            var ialt = p.BytesTotal / 1024.0 / 1024.0;
            var fart = p.BytesPerSecond / 1024.0 / 1024.0;
            var tilbage = p.Remaining is null ? "" : $" · {p.Remaining.Value:mm\\:ss} tilbage";
            Status.Text = $"Henter {model.Id}: {mb:0} af {ialt:0} MB · {fart:0.0} MB/s{tilbage}";
        });

        try
        {
            await _downloader.DownloadAsync(model.Url, destination, model.Bytes, fremdrift, _afbryd.Token);

            AppSettings.Current.PreferredModel = model.Id;
            AppSettings.Current.Save();

            Status.Text = $"{model.Id} er hentet og er den model, transskriptionen bruger.";
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Afbrudt. Intet er hentet færdigt.";
        }
        catch (Exception ex)
        {
            Status.Text = $"Kunne ikke hente: {ex.Message}";
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke hente", $"Hentningen fejlede.\n\n{ex.Message}\n\n" +
                "Er der ingen internetforbindelse, kan du lægge modelfilen manuelt i:\n" +
                WhisperInstall.ModelDirectory, Dialogs.Slags.Pas_paa);
        }
        finally
        {
            Fremdrift.Visibility = Visibility.Collapsed;
            AfbrydKnap.Visibility = Visibility.Collapsed;
            _afbryd?.Dispose();
            _afbryd = null;
            Opdater();
        }
    }

    /// <summary>
    /// Lægger den gamle modelfil tilbage, hvis en reparation ikke lykkedes.
    ///
    /// Kun når der ikke ligger en ny — en halv fil fra en afbrudt hentning
    /// bliver liggende som «.delvis» og er ikke i vejen.
    /// </summary>
    private static void GendanReserve(string sti, string reserve)
    {
        try
        {
            if (!File.Exists(reserve)) return;
            if (File.Exists(sti)) { File.Delete(reserve); return; }

            File.Move(reserve, sti);
        }
        catch (IOException)
        {
            // Kan den ikke laegges tilbage, staar den stadig som «.gammel» ved
            // siden af. Filen er der; den hedder bare noget andet.
        }
    }

    private void Afbryd_Click(object sender, RoutedEventArgs e) => _afbryd?.Cancel();
}
