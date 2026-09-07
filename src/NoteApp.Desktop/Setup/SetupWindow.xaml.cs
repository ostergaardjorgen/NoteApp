using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Setup;

/// <summary>
/// Opsætning ved første start.
///
/// TO trin: velkomst, og hent motoren. Ikke flere.
///
/// Der var fire. To af dem — «hvad handler dine møder om» og «hvem holder du
/// møder med» — fyldte en ordbog op med fagord og navne, som blev sendt med
/// til Whisper som ledetråd. Det blev målt: forskellen var NUL. To skærme
/// spørgsmål, før man havde set appen, for at fylde noget op, der ikke gjorde
/// nogen forskel.
///
/// Modellen hentes til gengæld HER. Uden den kan appen ikke skrive et eneste
/// møde ud, og det er det eneste, en ny bruger virkelig skal have på plads.
/// </summary>
public partial class SetupWindow : Window
{
    private int _trin;

    private static readonly (string Titel, string Under)[] Trin =
    {
        ("Velkommen til HeyPia", "Lyden bliver på din maskine — teksten bearbejdes i Europa"),
        ("Er det din første maskine?", "HeyPia kan dele arbejdet mellem to computere"),
        ("Sådan skal den virke", "Navn, mikrofon, genvejstast og vågeord — sat én gang, her"),
        ("Sidste trin: hent Whisper", "Appen ser efter, hvad din maskine kan, og henter det, der passer")
    };

    /// <summary>
    /// Skal forløbet køres igen? Så er der ikke noget at hente, og teksten
    /// skal ikke sige «velkommen» til en, der har brugt appen i en måned.
    /// </summary>
    private readonly bool _igen;

    private EngineRelease? _udgivelse;
    private EngineBuild? _motorValg;
    private WhisperModel? _modelValg;
    private bool _henter;

    public SetupWindow() : this(igen: false) { }

    /// <param name="igen">
    /// Kørt igen fra Indstillinger. Så er appen allerede i gang, og
    /// overskriften skal ikke byde velkommen én gang til.
    /// </param>
    public SetupWindow(bool igen)
    {
        InitializeComponent();

        _igen = igen;
        DataSti.Text = UserDataPaths.Root;

        if (igen) Title = "Opsætning";

        VisTrin(0);
    }

    private void VisTrin(int nr)
    {
        _trin = nr;

        Trin1.Visibility = nr == 0 ? Visibility.Visible : Visibility.Collapsed;
        Trin3.Visibility = nr == 1 ? Visibility.Visible : Visibility.Collapsed;
        Trin2.Visibility = nr == 2 ? Visibility.Visible : Visibility.Collapsed;
        Trin4.Visibility = nr == 3 ? Visibility.Visible : Visibility.Collapsed;

        TrinTitel.Text = nr == 0 && _igen ? "Opsætning" : Trin[nr].Titel;
        TrinUnder.Text = Trin[nr].Under;
        TrinTaeller.Text = $"Trin {nr + 1} af {Trin.Length}";

        TilbageKnap.Visibility = nr == 0 ? Visibility.Collapsed : Visibility.Visible;

        NaesteKnap.Content = nr switch
        {
            0 => _igen ? "Videre" : "Kom i gang",
            1 => "Videre",
            2 => "Videre",
            _ => "Hent og afslut",
        };

        // TRINNENE FLYTTEDE SIG, DA MASKINVALGET KOM IND SOM NR. 2.
        // «Saadan skal den virke» er nu nr. 3 og hentningen nr. 4 - og
        // hentningen skal forberedes FOERST der, ellers skriver den «Faerdig»
        // paa en knap, der stadig skal videre.
        if (nr == 2) IndlaesValg();
        if (nr == 3) _ = ForberedHentning();
    }

    // ------------------------------------------------ sådan skal den virke

    /// <summary>Sat, mens felterne fyldes — så et valg ikke gemmes af sig selv.</summary>
    private bool _fylder;

    /// <summary>
    /// Henter det, der allerede står, ind i trinnet.
    /// </summary>
    /// <remarks>
    /// Autostarten læses fra Windows og ikke fra indstillingerne. Det er
    /// registreringsdatabasen, der afgør, om appen starter — står der ét i
    /// vores fil og noget andet dér, er det vores fil, der lyver.
    /// </remarks>
    private void IndlaesValg()
    {
        _fylder = true;
        try
        {
            var v = AppSettings.Current;

            var mikrofoner = AudioDevices.Microphones();
            var hoejttalere = AudioDevices.Speakers();

            OpsMikrofon.ItemsSource = mikrofoner;
            OpsHoejttaler.ItemsSource = hoejttalere;

            var mik = Mikrofon.Valgt();
            var hoejt = AudioDevices.ResolveSpeaker(v.SpeakerId, out _);

            OpsMikrofon.SelectedItem = mikrofoner.FirstOrDefault(d => d.Id == mik?.Id);
            OpsHoejttaler.SelectedItem = hoejttalere.FirstOrDefault(d => d.Id == hoejt?.Id);

            OpsNavn.Text = v.DitNavn ?? "";

            OpsAutostart.IsChecked = Autostart.ErSlaaetTil();
            OpsDiktering.IsChecked = v.DikteringTil;
            OpsVaageord.IsChecked = v.VaageordTil;

            // Genvejen staar foerst fast, naar hovedvinduet har registreret
            // den. Her nævnes den derfor ved navn og ikke som en paastand om,
            // hvad der virker lige nu.
            DikteringUnder.Text =
                "Hold genvejstasten nede og tal — teksten lander, hvor markøren står. "
                + "Genvejen vises i toppen af appen, når den er klar.";
        }
        finally { _fylder = false; }
    }

    /// <summary>
    /// Gemmer navnet, naar feltet forlades.
    /// </summary>
    /// <remarks>
    /// Tomt felt betyder tomt navn og ikke «husk det gamle». Sletter man
    /// navnet med vilje, skal underskriften ogsaa vaere vaek.
    /// </remarks>
    private void Navn_Forladt(object sender, RoutedEventArgs e)
    {
        if (_fylder) return;

        var navn = OpsNavn.Text.Trim();
        AppSettings.Current.DitNavn = navn.Length == 0 ? null : navn;
        AppSettings.Current.Save();
    }

    private void Mikrofon_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_fylder || OpsMikrofon.SelectedItem is not DeviceInfo d) return;
        // GENNEM Mikrofon.Vaelg. Den rydder motorens eget nummer med -
        // det hoerte til den gamle enhed, og et tal, der peger forkert, faar
        // vaageordet til at lytte et andet sted, end skaermen siger.
        Mikrofon.Vaelg(d.Id);
    }

    private void Hoejttaler_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_fylder || OpsHoejttaler.SelectedItem is not DeviceInfo d) return;
        AppSettings.Current.SpeakerId = d.Id;
        AppSettings.Current.Save();
    }

    /// <summary>
    /// Gemmer hakkene fra trin 2.
    /// </summary>
    /// <remarks>
    /// Autostarten skrives i registreringsdatabasen med det samme. Gik det
    /// galt, siges det — et afkrydsningsfelt, der stille ikke gjorde noget,
    /// lover noget, det ikke holder. Se Autostart.Saet.
    /// </remarks>
    private void GemValg()
    {
        var v = AppSettings.Current;

        var navn = OpsNavn.Text.Trim();
        v.DitNavn = navn.Length == 0 ? null : navn;

        v.DikteringTil = OpsDiktering.IsChecked == true;
        v.VaageordTil = OpsVaageord.IsChecked == true;
        v.Save();

        var fejl = Autostart.Saet(OpsAutostart.IsChecked == true);
        if (fejl is null) return;

        Dialogs.AppDialog.Vis(this, "Autostart kunne ikke sættes",
            $"HeyPia kunne ikke skrive opstartsvalget: {fejl}\n\n"
            + "Resten er gemt. Du kan prøve igen under Indstillinger → Opstart.");
    }

    // ------------------------------------------------------- motor og model

    /// <summary>
    /// Slår op, hvad der skal hentes, og hvor meget det fylder — FØR brugeren
    /// trykker. Er begge dele der i forvejen, siges det, og trinnet bliver et
    /// klik videre i stedet for en hentning.
    /// </summary>
    private async Task ForberedHentning()
    {
        var installeret = WhisperInstall.Locate();

        // Motoren
        if (installeret.WhisperCli is not null)
        {
            MotorOverskrift.Text = "Motor — allerede på plads";
            MotorValg.Text = installeret.Engine == "GPU (CUDA)"
                ? "whisper.cpp med GPU-understøttelse er fundet på maskinen."
                : "whisper.cpp er fundet på maskinen.";
            MotorBegrundelse.Text = installeret.WhisperCli;
            _motorValg = null;
        }
        else
        {
            MotorValg.Text = "Slår op hos GitHub …";
            try
            {
                _udgivelse = await EngineInstaller.FetchAsync();
                _motorValg = EngineInstaller.Recommend(_udgivelse);

                if (_motorValg is null)
                {
                    MotorValg.Text = "Kunne ikke finde en Windows-udgave at hente.";
                }
                else
                {
                    var harGpu = EngineInstaller.HasNvidiaGpu();
                    MotorOverskrift.Text = $"Motor — whisper.cpp {_udgivelse.Version}";
                    MotorValg.Text = $"{_motorValg.FileName}  ({_motorValg.SizeText})";
                    MotorBegrundelse.Text = harGpu
                        ? "Maskinen har et NVIDIA-kort, så GPU-udgaven vælges. Den er cirka ti gange hurtigere end CPU."
                        : "Der er ikke fundet et NVIDIA-kort, så CPU-udgaven vælges. Den virker overalt, men et langt møde tager længere tid end mødet selv.";
                }
            }
            catch (Exception ex)
            {
                MotorValg.Text = "Kunne ikke nå GitHub.";
                MotorBegrundelse.Text = $"{ex.Message} — du kan hente motoren senere under Motor og model.";
            }
        }

        // Modellen
        if (installeret.ModelPath is not null)
        {
            ModelValg.Text = $"{Path.GetFileName(installeret.ModelPath)} er allerede på maskinen.";
            ModelBegrundelse.Text = installeret.ModelPath;
            _modelValg = null;
        }
        else
        {
            // large-v3 paa en GPU-maskine, ellers turbo. Kvaliteten paa dansk
            // er bedst med den store, men uden GPU er den ubrugelig langsom.
            //
            // Her stod Model("small") i den ene gren. Da katalogget blev
            // skaaret ned til to modeller 18-08-2026, holdt "small" op med at
            // findes, og opslaget begyndte at give null - som saa blev
            // dereferencet to linjer nede. Opsaetningen ville vaelte paa
            // enhver maskine uden NVIDIA-kort, altsaa praecis dem, grenen er
            // til for. Derfor slaas der nu op i katalogget frem for paa et
            // navn skrevet i haanden.
            // ============ EN SEKUNDAER MASKINE SKAL IKKE HENTE DEN STORE ============
            //
            // Den sender det tunge arbejde videre til den primaere. En model
            // paa 2,9 GB paa en baerbar, der alligevel ikke skal bruge den, er
            // en halv time ned ad en telefonforbindelse for ingenting.
            //
            // Den faar den lille i stedet - ikke ingenting. Uden en model kan
            // den ikke skrive noget ud, og dét er praecis, hvad man har brug
            // for i et tog, hvor den primaere ikke kan naas.
            var sekundaer = ValgSekundaer.IsChecked == true;

            _modelValg = sekundaer || !EngineInstaller.HasNvidiaGpu()
                ? WhisperInstall.Model("large-v3-turbo") ?? WhisperInstall.Standard
                : WhisperInstall.Standard;

            ModelValg.Text = $"{_modelValg.Id}  ({_modelValg.SizeText})";

            ModelBegrundelse.Text = sekundaer
                ? "Den lille, fordi den her computer sender det tunge arbejde videre til din "
                  + "primære. Den er nok til at skrive et møde ud på egen hånd, når du ikke "
                  + "kan nå den anden. Du kan skifte model senere under Motor og model."
                : _modelValg.Summary + " Du kan skifte model senere under Motor og model.";
        }

        var samlet = (_motorValg?.Bytes ?? 0) + (_modelValg?.Bytes ?? 0);
        SamletStoerrelse.Text = samlet == 0
            ? "ingenting — alt er der allerede"
            : $"{samlet / 1024.0 / 1024.0:0} MB";

        NaesteKnap.Content = samlet == 0 ? "Færdig" : "Hent og afslut";
    }

    /// <summary>
    /// Lader brugeren vælge, hvor filerne skal ligge. Sker det her — før det
    /// første møde — er der intet at flytte. Vælges der om senere, flyttes det,
    /// der allerede er.
    /// </summary>
    private void SkiftMappe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Vælg hvor HeyPias filer skal ligge",
            InitialDirectory = Directory.Exists(UserDataPaths.Root)
                ? UserDataPaths.Root
                : Path.GetPathRoot(UserDataPaths.DefaultRoot)!
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            UserDataPaths.SetRoot(dialog.FolderName);
            DataSti.Text = UserDataPaths.Root;
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke skifte mappe", ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    /// <summary>
    /// Gemmer, om det er den første eller den anden computer.
    /// </summary>
    /// <remarks>
    /// DEN SKAL SÆTTES, OGSÅ NÅR MAN IKKE RØRTE NOGET. Standarden er primær, og
    /// den skal skrives ned — ellers står maskinfilen med en rolle, ingen har
    /// taget stilling til, og den fejl viser sig først den dag, der er to
    /// computere.
    /// </remarks>
    private void GemMaskinrolle()
    {
        try
        {
            NoteApp.Core.Deling.Maskinid.Rolle = ValgSekundaer.IsChecked == true
                ? NoteApp.Core.Deling.Maskinrolle.Sekundaer
                : NoteApp.Core.Deling.Maskinrolle.Primaer;
        }
        catch (Exception)
        {
            // Kan den ikke skrives, koerer opsaetningen videre. Rollen kan
            // saettes under Indstillinger, og en opsaetning, der gaar i staa
            // paa en fil, er vaerre end en rolle, der staar forkert.
        }
    }

    private void Tilbage_Click(object sender, RoutedEventArgs e) => VisTrin(Math.Max(0, _trin - 1));

    private async void Naeste_Click(object sender, RoutedEventArgs e)
    {
        if (_henter) return;

        // Kvitteringen staar paa siden. Naar den er der, er der kun ét
        // tilbage at gøre, og knappen hedder «Luk».
        if (_faerdig)
        {
            DialogResult = true;
            Close();
            return;
        }

        // Hakkene gemmes, NAAR MAN FORLADER TRINNET - ikke først til sidst.
        // Gaar hentningen galt bagefter, eller lukkes vinduet, er valgene
        // stadig truffet. Det er dem, der er svaerest at finde igen.
        // MASKINEN GEMMES, NAAR MAN FORLADER TRINNET - ikke foerst til sidst.
        // Den afgoer, hvilken model der foreslaas paa naeste side.
        if (_trin == 1) GemMaskinrolle();

        if (_trin == 2) GemValg();

        if (_trin < Trin.Length - 1)
        {
            VisTrin(_trin + 1);
            return;
        }

        // Markeres FOERST. Gaar hentningen galt, eller afbryder brugeren, skal
        // velkomstforloebet ikke komme igen ved naeste start.
        AppSettings.Current.SetupCompleted = true;
        AppSettings.Current.Save();

        if (_motorValg is not null || _modelValg is not null)
        {
            var ok = await HentAlt();
            if (!ok) return;
        }

        Afslut();
    }

    /// <summary>
    /// Henter motor og model. Fejler noget, siges det — og opsætningen
    /// afsluttes alligevel, så brugeren ikke sidder fast i et velkomstforløb.
    /// Begge dele kan hentes bagefter under Motor og model.
    /// </summary>
    private async Task<bool> HentAlt()
    {
        _henter = true;
        NaesteKnap.IsEnabled = false;
        TilbageKnap.IsEnabled = false;
        HentFremdrift.Visibility = Visibility.Visible;

        var fremdrift = new Progress<DownloadProgress>(p =>
        {
            HentFremdrift.Value = p.Percent;
            var tilbage = p.Remaining is null ? "" : $" · {p.Remaining.Value:mm\\:ss} tilbage";
            // Kontrollen af summen bruger den samme bjaelke som hentningen.
            // Teksten skal foelge med, ellers staar der en hastighed for
            // noget, der ikke hentes.
            HentStatus.Text = p.Kontrollerer
                ? $"Kontrollerer kontrolsummen: {p.BytesDone / 1024.0 / 1024.0:0} af "
                  + $"{p.BytesTotal / 1024.0 / 1024.0:0} MB"
                : $"{p.BytesDone / 1024.0 / 1024.0:0} af {p.BytesTotal / 1024.0 / 1024.0:0} MB"
                  + $" · {p.BytesPerSecond / 1024.0 / 1024.0:0.0} MB/s{tilbage}";
        });

        try
        {
            if (_motorValg is not null && _udgivelse is not null)
            {
                await EngineInstaller.InstallAsync(_motorValg, _udgivelse.Version, fremdrift,
                    new Progress<string>(s => HentStatus.Text = s));
            }

            if (_modelValg is not null)
            {
                HentStatus.Text = $"Henter {_modelValg.Id} …";
                await new Downloader().DownloadAsync(
                    _modelValg.Url, WhisperInstall.ModelDestination(_modelValg), _modelValg.Bytes, fremdrift);

                AppSettings.Current.PreferredModel = _modelValg.Id;
                AppSettings.Current.Save();
            }

            HentStatus.Text = "Færdig.";
            return true;
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke hente", $"Hentningen blev ikke færdig.\n\n{ex.Message}\n\n" +
                "Opsætningen afsluttes. Du kan hente motor og model senere under «AI-modeller».", Dialogs.Slags.Pas_paa);

            Afslut(visKvittering: false);
            return false;
        }
        finally
        {
            _henter = false;
            NaesteKnap.IsEnabled = true;
            TilbageKnap.IsEnabled = true;
            HentFremdrift.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Er kvitteringen vist? Så er næste klik en lukning.</summary>
    private bool _faerdig;

    /// <summary>
    /// Viser kvitteringen — på siden, ikke i et vindue oven på den.
    /// </summary>
    /// <remarks>
    /// HER LAA ET POP OP-VINDUE. Man kom igennem tre skærme og fik så en
    /// fjerde kasse oven i den, man stod på, for at få at vide, at man var
    /// færdig. En kasse, der lægger sig over det hele, læses som en
    /// fejlmeddelelse — og det er den modsatte besked af den, der skal gives.
    ///
    /// Nu står den som det sidste på den sidste side, og «Luk» er det eneste,
    /// der er tilbage at gøre.
    /// </remarks>
    private void VisKvittering()
    {
        var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);

        KvitteringTitel.Text = install.IsComplete ? "Alt er klar" : "Næsten klar";

        var klar = install.IsComplete
            ? $"Whisper er klar: {install.ModelFileName} på {install.Engine}."
            : "Motor eller model mangler stadig — hent dem under «AI-modeller».";

        KvitteringTekst.Text = klar + "\n\n"
            + "Tryk «Optag møde», når dit næste møde begynder — så er du i gang.\n\n"
            // HER STOD «Start her». Det menupunkt findes ikke mere - det
            // hoerte til oplaesning og traening, som er fjernet. Testen af
            // mikrofonen er tilbage under Indstillinger, hvor man er, naar
            // man vaelger mikrofon.
            + "Vil du vide, om din mikrofon er god nok, kan du læse en prøvetekst "
            + "op under «Indstillinger» → «Lyd». Det er frivilligt.";

        Kvittering.Visibility = Visibility.Visible;

        _faerdig = true;
        TilbageKnap.Visibility = Visibility.Collapsed;
        NaesteKnap.Content = "Luk";
        NaesteKnap.IsEnabled = true;
    }

    private void Afslut(bool visKvittering = true)
    {
        if (visKvittering)
        {
            VisKvittering();
            return;
        }

        DialogResult = true;
        Close();
    }
}
