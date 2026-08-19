using System.IO;
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
            : "Modellen er en fast fil og får ikke nye udgaver — modsat motoren. " +
              "At hente den igen giver præcis den samme fil og gør ikke udskriften bedre. " +
              "Det er kun værd at gøre, hvis filen er blevet beskadiget.";
        // MOTORKNAPPEN VAR SLAAET FRA, NAAR MOTOREN MANGLEDE.
        //
        // "IsEnabled = WhisperCli is not null" betoed, at den ENESTE knap,
        // der kan installere motoren, var graa praecis naar den ikke var
        // installeret. Motoren og modellen hentes hver for sig - det ene
        // foelger ikke med det andet - saa uden motor var der ingen vej frem
        // fra denne skaerm. Fundet 19-08-2026.
        MotorKnap.Content = s.WhisperCli is null ? "Hent motoren" : "Opdatér motoren";

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
    }

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
        Status.Text = "Slår op, om der er en nyere udgave …";

        EngineRelease nyeste;
        try
        {
            nyeste = await EngineInstaller.FetchLatestAsync();
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

        // Er versionen den samme, er der intet at hente, og det skal siges
        // som et svar - ikke som en hentning, der "ikke gjorde noget".
        if (nuvaerende is not null && nuvaerende == nyeste.Version)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Motoren er den nyeste",
                $"Du kører {nuvaerende}, og det er den seneste udgivelse af whisper.cpp.\n\n" +
                "Der er ikke hentet noget.", Dialogs.Slags.Valg);
            Status.Text = $"Motoren er den nyeste ({nuvaerende}).";
            return;
        }

        var linje = nuvaerende is null
            ? "Du kører en motor, appen ikke selv har installeret, så dens version kan ikke aflæses."
            : $"Du kører: {nuvaerende}";

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Hent {nyeste.Version}?",
            $"{linje}\n" +
            $"Nyeste udgivelse: {nyeste.Version}\n\n" +
            $"Fil: {build.FileName}\n" +
            $"Størrelse: {build.SizeText}\n" +
            $"Hentes fra: github.com\n\n" +
            "En ny motor kan ændre transskriptionen — til det bedre eller det værre. " +
            "Vil du vide hvilket, skal det måles; fremgangsmåden står i doc/whisper.md.",
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

    private void Afbryd_Click(object sender, RoutedEventArgs e) => _afbryd?.Cancel();
}
