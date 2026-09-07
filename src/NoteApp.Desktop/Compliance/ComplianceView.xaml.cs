using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Compliance;

/// <summary>En model eller et stykke software, der skal kunne gøres rede for.</summary>
public sealed record Komponent(string Navn, string Licens, string Hvor, string Rolle, string Note)
{
    /// <summary>
    /// Farven på licensmærkatet.
    ///
    /// Grøn er «fri at sælge med» — MIT, Apache. Gul er «der følger
    /// betingelser med, som skal videre til kunden». Det er dén forskel, der
    /// koster noget at opdage sent.
    /// </summary>
    public Brush Farve => Licens is "MIT" or "Apache 2.0"
        ? Temaskift.Pensel("Godkendt")
        : Temaskift.Pensel("Advarsel");
}

/// <summary>
/// Alt, en indkøber, en revisor eller en databeskyttelsesrådgiver spørger om,
/// samlet ét sted.
///
/// HVORFOR TALLENE IKKE REGNES UD HER
///
/// Oplysningerne kommer fra dokumenterne i repoet — mistral-dpa.md for
/// vilkårene, WhisperInstall for modellerne. De er skrevet ind som tekst med
/// en dato på frem for at blive hentet levende, og det er med vilje: en side,
/// der henter sine egne påstande fra nettet, kan ikke sige, hvornår den sidst
/// blev efterprøvet af et menneske. Datoen er hele pointen.
/// </summary>
public partial class ComplianceView : UserControl
{
    public ComplianceView()
    {
        InitializeComponent();

        var tilsluttet = SkyNoegle.Hent() is not null;

        SkyLinje.Text = tilsluttet
            ? $"Sendes til {new Uri(SkyKatalog.Endpoint).Host}"
            : "Ikke sat op endnu — intet sendes";

        Endepunkt.Text = SkyKatalog.Endpoint;
        EuVaert.Text = $"Eneste tilladte vært: {SkyKatalog.TilladtVaert}";

        VisGoogletilstand();
        VisKvitteringer();
        VisAttest();
        VisSummer();
        VisLicensmappe();

        var whisper = WhisperInstall.Standard;

        Modeller.ItemsSource = new[]
        {
            new Komponent(
                "whisper.cpp",
                "MIT",
                "Kører lokalt",
                "Motoren, der afvikler Whisper-modellen på din maskine. Hentes fra projektets eget udgivelsesarkiv på github.com.",
                "MIT er fri at sælge med. Der er ingen betingelser, der skal videregives til dine kunder."),

            new Komponent(
                whisper.Id,
                "MIT",
                "Kører lokalt",
                "Modellen, der laver lyd om til tekst. Vægtene er OpenAI's og hentes fra huggingface.co.",
                "Lyden forlader ikke maskinen. Hentningen er en envejsforbindelse: appen beder om en navngiven fil og modtager den."),

            // ===================== TALERGENKENDELSEN =====================
            //
            // Tre komponenter mere, og de staar hver for sig af samme grund
            // som whisper: de har hver sin rettighedshaver.
            //
            // LICENSERNE ER EFTERPROEVET, IKKE ANTAGET. Det er ikke en
            // formalitet her: i den samme modelsamling ligger
            // reverb-diarization-v1, som er udtrykkeligt IKKE-KOMMERCIEL. At
            // en model ligger samme sted som vaerktoejet siger altsaa intet om,
            // hvad den maa bruges til, og hver enkelt er slaaet op for sig.
            //
            // Den foerste stemmemodel, der blev maalt paa, var 3D-Speakers
            // ERes2Net. Vaerktoejet bag er Apache-2.0, men selve modelvaegtene
            // har ingen oplyst licens, og de er traenet paa VoxCeleb, som er et
            // forskningsdatasaet. Den er derfor fravalgt til fordel for en med
            // en licens, der staar skrevet.
            new Komponent(
                "sherpa-onnx",
                "Apache-2.0",
                "Kører lokalt",
                "Programmet, der skiller stemmerne fra hinanden i en optagelse. Følger med appen og hentes ikke.",
                "Apache-2.0 er fri at sælge med. Betingelsen er, at licensteksten følger med — den ligger i "
                + @"programmappen under licenser\tekster\sherpa-onnx-Apache-2.0.txt."),

            new Komponent(
                "pyannote segmentation 3.0",
                "MIT",
                "Kører lokalt",
                "Modellen, der finder ud af, hvornår der bliver talt, og hvornår der skiftes taler. Vægtene er pyannote-projektets.",
                "MIT er fri at sælge med. Der er ingen betingelser, der skal videregives til dine kunder."),

            new Komponent(
                "NVIDIA TitaNet",
                "CC-BY-4.0",
                "Kører lokalt",
                "Modellen, der afgør, om to stykker tale kommer fra den samme stemme. Vægtene er NVIDIA's.",
                "CC-BY-4.0 tillader kommerciel brug og videredistribution. Betingelsen er kreditering: NVIDIA "
                + "nævnes som ophav her, og krediteringen står sammen med den fulde licenstekst i programmappen "
                + @"under licenser\NOTICE.md. Modellen er ikke ændret."),

            // HER STOD QWEN3 (4B) - sprogmodellen til den korte opsummering.
            //
            // Fjernet 25-08-2026 sammen med llama-motoren; 11 GB slettet fra
            // maskinen. En komponent, der ikke laengere er der, maa ikke staa
            // paa en compliance-side - og slet ikke med "Koerer lokalt" ud for
            // sig. Det er praecis den slags, en koeber laeser og regner med.
            //
            // Opsummeringen laves nu hos Mistral, og det staar paa linjen
            // nedenfor.

            new Komponent(
                SkyKatalog.Standard.Navn,
                "Tjeneste",
                "Kører i Frankrig",
                "Sprogmodellen, der laver den korte opsummering OG et færdigt dokument ud af transkriptionen efter mødetypen. Kører hos leverandøren — der hentes ingen vægte, og der installeres ingenting. Det er her, grænsen går: alt efter transkriptionen sker på denne linje.",
                "Det er en tjeneste, ikke en licens. Det, der gælder, er databehandleraftalen og vilkårene ovenfor.")

            // HER STOD ROEST V3. Den er undersoegt og fravalgt, ikke i brug -
            // og en liste over det, der KUNNE have vaeret brugt, hoerer ikke
            // hjemme paa en compliance-side. Undersoegelsen staar i
            // doc/maaling-whisper.md, og licensforholdet (OpenRAIL-M med
            // brugsbegraensninger, der skal videregives) er noteret dér.
        };
    }

    /// <summary>
    /// Står forbindelsen til Google lige nu?
    ///
    /// DEN STÅR PÅ COMPLIANCE-SIDEN OG IKKE KUN UNDER INDSTILLINGER. Siden
    /// beskriver, hvad der KAN ske; om det faktisk sker, afhænger af, om
    /// integrationen er slået til. Uden tilstanden skal man to skærme væk for
    /// at vide, om afsnittet overhovedet angår én.
    /// </summary>
    private void VisGoogletilstand()
    {
        var forbundet = false;

        try { forbundet = Integrationsfiler.Hent("google").ErForbundet; }
        catch (Exception)
        {
            // Kan opsaetningen ikke laeses, siges der «ikke forbundet». Det er
            // det forsigtige svar: at paastaa en forbindelse, der maaske ikke
            // findes, er den forkerte fejl paa netop den her side.
        }

        GoogleTilstand.Text = forbundet ? "FORBUNDET" : "IKKE FORBUNDET";

        var opgaver = false;
        try { opgaver = Integrationsfiler.Hent(Googleopgaver.Id).ErForbundet; }
        catch (Exception) { }

        Opgavetilstand.Text = opgaver ? "FORBUNDET" : "IKKE FORBUNDET";

        Opgavetilstand.Foreground = opgaver
            ? (System.Windows.Media.Brush)FindResource("Godkendt")
            : (System.Windows.Media.Brush)FindResource("TekstMeget");

        GoogleTilstand.Foreground = forbundet
            ? (System.Windows.Media.Brush)FindResource("Godkendt")
            : (System.Windows.Media.Brush)FindResource("TekstMeget");
    }

    private void Dpa_Klik(object sender, RoutedEventArgs e) => Aabn("https://legal.mistral.ai");
    private void Trust_Klik(object sender, RoutedEventArgs e) => Aabn("https://trust.mistral.ai");
    private void Konsol_Klik(object sender, RoutedEventArgs e) => Aabn("https://console.mistral.ai");

    private void Aabn(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // En browser, der ikke vil aabne, maa ikke ende som en tom knap.
            // Adressen staar der, saa den kan skrives af i haanden.
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne browseren",
                $"Gå til denne adresse i din browser:\n\n{url}", Dialogs.Slags.Valg);
        }
    }

    /// <summary>
    /// Kvitteringerne — hvad der faktisk har forladt maskinen.
    ///
    /// Resten af skærmen beskriver, hvad appen GØR. Det her er, hvad der
    /// SKETE. En revision spørger om det sidste, og en beskrivelse af koden
    /// kan ikke svare på det.
    /// </summary>
    private void VisKvitteringer()
    {
        var alle = Kvitteringer.Laes();

        KvitAntal.Text = alle.Count.ToString();
        KvitTegn.Text = alle.Count == 0 ? "0" : $"{alle.Sum(k => (long)k.Tegn):N0}";
        // KURSEN ER STABIL, OG DET ER GRUNDEN TIL AT VISE KRONER.
        //
        // Danmark foerer fastkurspolitik over for euroen, saa 7,46 staar
        // stille. Den gamle omregning gik gennem dollar med et rundt tal paa
        // 6,50 - en kurs, der flytter sig, og som gjorde kronebeloebet
        // mindre paalideligt end euroen, det kom fra.
        //
        // Konstanten staar her og ikke i en indstilling: et felt, brugeren
        // kan rette, ville love en noejagtighed, tallet ikke har.
        const decimal KronerPrEuro = 7.46m;

        var samlet = alle.Sum(k => k.PrisEur);

        KvitPris.Text = $"€{samlet:0.00}";
        KvitPrisKr.Text = alle.Count == 0 ? "" : $"ca. {samlet * KronerPrEuro:0.00} kr.";

        KvitTom.Visibility = alle.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        KvitListe.ItemsSource = alle.Select(k => new
        {
            Hvad = k.KildeTitel.Length > 0
                ? $"{k.Skabelon} — {k.KildeTitel}"
                : k.Skabelon,

            Naar = k.Tidspunkt.LocalDateTime.ToString("dd-MM-yyyy HH:mm:ss"),

            // MASKINEN STAAR FOERST. Naar to computere sender til den samme
            // konto, er «hvem sendte det» det foerste, en revision spoerger
            // om - foer model og pris. Tom paa kvitteringer fra dengang, der
            // kun var een computer.
            Linje = (k.Maskine.Length > 0 ? k.Maskine + "  ·  " : "")
                    + $"{new Uri(k.Endepunkt).Host}  ·  {k.Model}  ·  " +
                    $"{k.Tegn:N0} tegn sendt  ·  {k.TokensInd:N0} ind / {k.TokensUd:N0} ud  ·  " +
                    $"€{k.PrisEur:0.0000} (ca. {k.PrisEur * KronerPrEuro:0.00} kr.)  ·  {k.Sekunder:0.0} sek",

            // Kontrolsummen staar HELT ud. Det er den, der goer kvitteringen
            // til et bevis - en forkortet sum kan ikke sammenlignes med noget.
            Sum = "SHA-256: " + k.Sum,

            // Kvitteringer skrevet foer 03-09-2026 har ikke feltet. De skal
            // vise INTET frem for en tom etiket: linjen findes ikke, fordi
            // id'et ikke blev gemt dengang - ikke fordi leverandoeren undlod
            // at sende det.
            Anmodningsid = "Anmodnings-id: " + k.Anmodningsid,
            AnmodningsidSynlig = k.Anmodningsid.Length > 0 ? Visibility.Visible : Visibility.Collapsed,

            Kant = (System.Windows.Media.Brush)FindResource(k.Lykkedes ? "PanelKant" : "FejlTekst"),
            Fejl = k.Fejl,
            FejlSynlig = k.Fejl.Length > 0 ? Visibility.Visible : Visibility.Collapsed
        }).ToList();
    }

    /// <summary>
    /// Hvor licensnotitserne ligger — eller at de ikke gør.
    /// </summary>
    /// <remarks>
    /// SKÆRMEN LOVEDE DEM, FØR DE FANDTES. Der stod «licensteksten følger med
    /// — den ligger i installationsmappen» ved sherpa-onnx og NVIDIA, og der
    /// lå ingen licensfiler nogen steder 03-09-2026.
    ///
    /// Nu kopieres de med af <c>udgiv.ps1</c>, og <c>tjek-licenser.ps1</c>
    /// fejler bygget, hvis de mangler. Her siges det, der ER — inklusive det
    /// tilfælde, hvor mappen ikke kan findes. En påstand, der ikke kan
    /// efterprøves, hører ikke til på den her skærm.
    /// </remarks>
    private void VisLicensmappe()
    {
        _licensmappe = Licensmappe();

        if (_licensmappe is not null)
        {
            Licenstilstand.Text = $"Ligger i {_licensmappe}";
            Licenstilstand.Foreground = (Brush)FindResource("TekstMeget");
        }
        else
        {
            Licenstilstand.Text =
                "Mappen blev ikke fundet her. Kører appen fra en udviklingsbuild, følger "
                + "notitserne først med ved udgivelsen — i en installeret udgave er de der.";
            Licenstilstand.Foreground = (Brush)FindResource("Advarsel");
            LicensKnap.IsEnabled = false;
        }
    }

    private string? _licensmappe;

    /// <summary>
    /// Mappen med notitserne — ved siden af programmet, eller i repoet, når
    /// appen kører fra en build-mappe.
    /// </summary>
    private static string? Licensmappe()
    {
        var udgivet = Path.Combine(AppContext.BaseDirectory, "licenser");
        if (System.IO.Directory.Exists(udgivet)) return udgivet;

        // I en build-mappe ligger de ikke ved siden af exe'en. Repoet soeges
        // paa NOTICE.md - samme fremgangsmaade som resten af appen, se
        // RepoFiles, og af samme grund: afstanden op til roden er forskellig
        // fra en build-mappe og fra den udgivne app.
        return RepoFiles.Find("licenser", "NOTICE.md") is { } fil
            ? Path.GetDirectoryName(fil)
            : null;
    }

    private void Licenser_Klik(object sender, RoutedEventArgs e)
    {
        if (_licensmappe is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(_licensmappe) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne mappen",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    /// <summary>
    /// Kontrolsummerne for alt, appen henter ned.
    /// </summary>
    /// <remarks>
    /// SUMMEN STÅR HELT UD. Det er samme valg som ved kvitteringerne og af
    /// samme grund: en forkortet sum kan ikke sammenlignes med noget, og så
    /// er den pynt frem for en oplysning.
    ///
    /// Listen kommer fra manifestet i koden — ikke fra filerne på maskinen.
    /// En liste, der læste de faktiske filer, ville vise, hvad der ER hentet;
    /// det, der skal kunne svares på her, er, hvad appen kræver af det.
    /// </remarks>
    private void VisSummer()
    {
        Summer.ItemsSource = Komponentmanifest.Alle.Select(k => new
        {
            k.Filnavn,
            k.Rolle,

            Stoerrelse = k.Bytes >= 1_000_000_000
                ? $"{k.Bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB"
                : $"{k.Bytes / 1024.0 / 1024.0:0} MB",

            Sum = "SHA-256: " + k.Sha256,

            Herfra = $"Filen: {k.Kilde}  ·  Summen: {k.Sumkilde}  ·  {k.Bekraeftelse}"
        }).ToList();

        // TALERADSKILLELSEN FOELGER MED APPEN OG HENTES IKKE, og derfor staar
        // den ikke i manifestet. Den skal alligevel naevnes her: en laeser,
        // der ser en liste over hentede komponenter, spoerger med det samme,
        // hvor resten blev af.
        Medfoelgende.Text =
            "sherpa-onnx, pyannote-segmenteringen og NVIDIA TitaNet hentes ikke — de følger med "
            + "installationspakken og er derfor dækket af pakkens egen kontrol, ikke af en "
            + "kontrolsum her.";
    }

    /// <summary>
    /// Attesten for de to kontoindstillinger, appen ikke kan sætte.
    /// </summary>
    /// <remarks>
    /// TILSTANDEN SIGER «REGISTRERET», IKKE «SLÅET TIL». Forskellen er hele
    /// pointen: appen har ikke set indstillingen hos leverandøren og må ikke
    /// skrive, at den har. Se <see cref="Kontoattester"/>.
    /// </remarks>
    private void VisAttest()
    {
        var a = Kontoattester.Hent();

        Saet(a.Zdr, ZdrDato, ZdrAnsvarlig, ZdrReference, ZdrTilstand);
        Saet(a.Traening, TraeningDato, TraeningAnsvarlig, TraeningReference, TraeningTilstand);
    }

    private void Saet(Attest attest, DatePicker dato, TextBox ansvarlig,
                      TextBox reference, TextBlock tilstand)
    {
        // Datoen staar som ISO i filen, saa den kan laeses af et menneske og
        // af et script uden at gaette paa raekkefoelgen af dag og maaned.
        // Vaelgeren arbejder med DateTime, og oversaettelsen sker her.
        dato.SelectedDate = DateTime.TryParse(
            attest.Dato, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;

        ansvarlig.Text = attest.Ansvarlig;
        reference.Text = attest.Reference;

        tilstand.Text = attest.ErRegistreret ? "REGISTRERET" : "IKKE REGISTRERET";

        // Gul, ikke groen. En registrering er en attest fra brugeren, ikke en
        // verifikation - og groen ville laeses som det sidste.
        tilstand.Foreground = (Brush)FindResource(
            attest.ErRegistreret ? "Advarsel" : "TekstMeget");
    }

    private void GemAttest_Klik(object sender, RoutedEventArgs e)
    {
        var ny = new Kontoattest
        {
            Zdr = Laes(ZdrDato, ZdrAnsvarlig, ZdrReference),
            Traening = Laes(TraeningDato, TraeningAnsvarlig, TraeningReference)
        };

        try
        {
            Kontoattester.Gem(ny);

            AttestBesked.Text = "Registreringen er gemt.";
            AttestBesked.Foreground = (Brush)FindResource("Godkendt");

            VisAttest();
        }
        catch (Exception ex)
        {
            // BESKEDEN STAAR VED KNAPPEN OG IKKE I EN DIALOG. Fejlen handler
            // om det, der staar i felterne, og de skal kunne ses, mens den
            // laeses - en dialog ville daekke dem.
            AttestBesked.Text = ex.Message;
            AttestBesked.Foreground = (Brush)FindResource("FejlTekst");
        }
    }

    private static Attest Laes(DatePicker dato, TextBox ansvarlig, TextBox reference) => new()
    {
        Dato = dato.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
        Ansvarlig = ansvarlig.Text.Trim(),
        Reference = reference.Text.Trim()
    };

    private void Kvitteringer_Klik(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Kvitteringer.Directory);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(Kvitteringer.Directory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne mappen",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
    }
}
