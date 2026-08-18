using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;
using NoteApp.Core.Documents;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Transcribe;

public sealed class OptagelseVisning
{
    public OptagelseVisning(string mappe)
    {
        Mappe = mappe;
        var meta = MeetingStore.Load(mappe);
        var wav = Path.Combine(mappe, "mikrofon.wav");

        Gruppe = Optagelsesgruppe.Af(mappe, meta);
        Emnemappe = meta?.Mappe;

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

    /// <summary>Stien på disken. IKKE brugerens mappe — se <see cref="Emnemappe"/>.</summary>
    public string Mappe { get; }

    /// <summary>
    /// Brugerens egen mappe, fx et kundenavn. Null betyder «uden mappe».
    ///
    /// Hedder ikke «Mappe», fordi det navn allerede er stien på disken — og
    /// to felter, der hedder næsten det samme og betyder noget vidt
    /// forskelligt, er en fejl, der venter på at ske.
    /// </summary>
    public string? Emnemappe { get; set; }

    public string Titel { get; }
    public string Detaljer { get; }
    public double Sekunder { get; }
    public bool HarLyd { get; }
    public long Bytes { get; }

    public Gruppe Gruppe { get; }

    /// <summary>Er der en udskrift? Det afgør, om mødet overhovedet kan være færdigbehandlet.</summary>
    public bool ErSkrevetUd => Directory.EnumerateFiles(Mappe, "*.txt")
        .Any(f => !f.EndsWith(".raa.txt", StringComparison.OrdinalIgnoreCase));

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

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Skriv «{optagelse.Titel}» ud til tekst nu?",
            $"Længde: {TimeSpan.FromSeconds(optagelse.Sekunder):mm\\:ss}. " +
            $"Det tager typisk {Math.Max(1, Math.Round(minutter * 0.3)):0} til {Math.Max(2, Math.Round(minutter * 0.5)):0} minutter " +
            "på denne maskine.\n\n" +
            "Du kan roligt lave noget andet imens — også optage et nyt møde.",
            godkend: "Skriv ud nu",
            annuller: "Ikke nu");

        if (ja) Koer_Click(this, new RoutedEventArgs());
    }

    /// <summary>
    /// Alle optagelser på disken, uanset gruppe. Læses én gang og deles ud
    /// bagefter, så et skift mellem møder og arkiv ikke koster en ny tur
    /// gennem filsystemet.
    /// </summary>
    private static List<OptagelseVisning> Alle()
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

        return mapper
            .Select(m => new OptagelseVisning(m))
            .OrderByDescending(o => Directory.GetLastWriteTime(o.Mappe))
            .ToList();
    }

    /// <summary>Viser arkivet frem for de møder, der ligger fremme.</summary>
    private bool _viserArkiv;

    /// <summary>
    /// Fylder listen med den valgte gruppe.
    ///
    /// Træningsoptagelser er IKKE med nogen af stederne. En oplæsning af en
    /// prøvetekst er ikke et møde, og når den ligger i mødelisten, skal man
    /// hver gang læse forbi den for at finde det, man kom efter. Den hører
    /// under «Træning», hvor der er noget at gøre ved den.
    /// </summary>
    private void IndlaesOptagelser()
    {
        var alle = Alle();

        var moeder = alle.Count(o => o.Gruppe == Gruppe.Moede);
        var arkiv = alle.Count(o => o.Gruppe == Gruppe.Arkiv);

        var oensket = _viserArkiv ? Gruppe.Arkiv : Gruppe.Moede;

        // En mappe, der er i brug, skal staa paa listen - ogsaa hvis
        // mapper.json er gaaet tabt.
        NoteApp.Core.Mapper.SikrFindes(NoteApp.Core.Mapper.Slags.Optagelser, alle.Select(o => o.Emnemappe));
        FyldMappeFilter(alle);

        var iGruppen = alle.Where(o => o.Gruppe == oensket).ToList();
        var valgtMappe = MappeFilter.SelectedItem as string;

        Optagelser.ItemsSource = valgtMappe is null or AlleMapper
            ? iGruppen
            : valgtMappe == NoteApp.Core.Mapper.Ingen
                ? iGruppen.Where(o => string.IsNullOrWhiteSpace(o.Emnemappe)).ToList()
                : iGruppen.Where(o => valgtMappe.Equals(o.Emnemappe, StringComparison.CurrentCultureIgnoreCase)).ToList();

        FaneMoeder.Content = $"Møder ({moeder})";
        FaneArkiv.Content = $"Arkiv ({arkiv})";
        FaneMoeder.IsChecked = !_viserArkiv;
        FaneArkiv.IsChecked = _viserArkiv;


        if (Optagelser.Items.Count > 0) return;

        if (_viserArkiv)
        {
            Status.Text = "Arkivet er tomt.";
            ForklaringOverskrift.Text = "Der ligger ikke noget i arkivet";
            ForklaringUnder.Text =
                "Når du er færdig med et møde — skrevet ud, dokument lavet — kan du lægge det i arkivet. " +
                "Så bliver listen forrest ved med kun at vise det, der stadig mangler noget.";
            return;
        }

        Status.Text = "Ingen møder endnu.";
        ForklaringOverskrift.Text = "Der ligger ingen møder her";
        ForklaringUnder.Text = "Tryk «Optag møde» øverst, når mødet begynder. Optagelsen dukker op her bagefter.";
    }

    private const string AlleMapper = "Alle mapper";
    private bool _fylder;

    /// <summary>
    /// Fylder mappevælgeren uden at fyre SelectionChanged undervejs — den
    /// ville kalde IndlaesOptagelser igen midt i IndlaesOptagelser.
    /// </summary>
    private void FyldMappeFilter(IReadOnlyList<OptagelseVisning> alle)
    {
        var valgt = MappeFilter.SelectedItem as string ?? AlleMapper;

        _fylder = true;

        var punkter = new List<string> { AlleMapper };
        punkter.AddRange(NoteApp.Core.Mapper.Alle(NoteApp.Core.Mapper.Slags.Optagelser));
        if (alle.Any(o => string.IsNullOrWhiteSpace(o.Emnemappe))) punkter.Add(NoteApp.Core.Mapper.Ingen);

        MappeFilter.ItemsSource = punkter;
        MappeFilter.SelectedItem = punkter.Contains(valgt) ? valgt : AlleMapper;

        _fylder = false;
    }

    private void MappeFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_fylder || Optagelser is null) return;
        IndlaesOptagelser();
    }

    private void NyMappe_Click(object sender, RoutedEventArgs e)
    {
        var vindue = RenameWindow.TilNyMappe();
        vindue.Owner = Window.GetWindow(this);

        if (vindue.ShowDialog() != true) return;

        if (!NoteApp.Core.Mapper.Opret(NoteApp.Core.Mapper.Slags.Optagelser, vindue.NytNavn))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den findes allerede",
                $"Der er allerede en mappe, der hedder «{vindue.NytNavn}».", Dialogs.Slags.Valg);
            return;
        }

        IndlaesOptagelser();
        MappeFilter.SelectedItem = vindue.NytNavn;
    }

    /// <summary>
    /// Flytter optagelsen til en mappe. Kun feltet i meeting.json ændrer sig —
    /// mappen på disken bliver liggende, så dokumenter, der allerede peger på
    /// den, stadig finder tilbage.
    /// </summary>
    private void Flyt_Click(object sender, RoutedEventArgs e)
    {
        if (Optagelser.SelectedItem is not OptagelseVisning valgt) return;

        var vindue = new Dialogs.MappeVaelger(
            NoteApp.Core.Mapper.Slags.Optagelser, valgt.Emnemappe, "optagelsen")
        { Owner = Window.GetWindow(this) };

        if (vindue.ShowDialog() != true) return;

        var meta = MeetingStore.Load(valgt.Mappe);
        if (meta is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke flytte",
                "Der er ingen meeting.json i optagelsens mappe, så mappevalget kan ikke gemmes.",
                Dialogs.Slags.Pas_paa);
            return;
        }

        meta.Mappe = vindue.Valgt;
        MeetingStore.Save(valgt.Mappe, meta);

        var sti = valgt.Mappe;
        IndlaesOptagelser();

        // Optagelsen kan vaere filtreret vaek af mappevaelgeren nu. Findes den
        // ikke i listen, staar der ingen markering - og det er rigtigt: den
        // ligger et andet sted end det, der vises.
        Optagelser.SelectedItem = Optagelser.Items.Cast<OptagelseVisning>()
            .FirstOrDefault(o => o.Mappe == sti);

        Status.Text = vindue.Valgt is null
            ? "Optagelsen ligger nu uden mappe."
            : $"Flyttet til «{vindue.Valgt}».";
    }

    private void Fane_Klik(object sender, RoutedEventArgs e)
    {
        if (Optagelser is null) return;

        var arkiv = FaneArkiv.IsChecked == true;
        if (arkiv == _viserArkiv) return;

        _viserArkiv = arkiv;
        IndlaesOptagelser();
    }

    /// <summary>
    /// Lægger et møde væk — eller henter det frem igen.
    ///
    /// Der advares, når mødet ikke er skrevet ud. Arkivering er ikke farlig,
    /// men den flytter noget ud af syne, og et møde, der aldrig blev skrevet
    /// ud, er ikke færdigt.
    /// </summary>
    private void Arkiver_Click(object sender, RoutedEventArgs e)
    {
        if (Optagelser.SelectedItem is not OptagelseVisning valgt) return;

        var tilArkiv = valgt.Gruppe == Gruppe.Moede;

        if (tilArkiv && !valgt.ErSkrevetUd)
        {
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                $"«{valgt.Titel}» er ikke skrevet ud endnu",
                "Lægger du den i arkivet nu, ligger lyden der stadig — men den er ikke med i listen forrest, " +
                "og så er der ingen, der får skrevet den ud.",
                godkend: "Læg i arkivet alligevel",
                annuller: "Behold den fremme",
                slags: Dialogs.Slags.Pas_paa,
                godkendErStandard: false);

            if (!ja) return;
        }

        Optagelsesgruppe.Arkiver(valgt.Mappe, tilArkiv);

        Historik.Skriv(
            tilArkiv ? HaendelseType.Arkiveret : HaendelseType.HentetFrem,
            valgt.Titel,
            tilArkiv ? "Lagt i arkivet" : "Hentet frem fra arkivet",
            sti: valgt.Mappe);

        Status.Text = tilArkiv
            ? $"«{valgt.Titel}» er lagt i arkivet."
            : $"«{valgt.Titel}» er hentet frem igen.";

        IndlaesOptagelser();
    }

    private void Optagelse_Valgt(object sender, SelectionChangedEventArgs e)
    {
        var valgt = Optagelser.SelectedItem as OptagelseVisning;
        KoerKnap.IsEnabled = valgt?.HarLyd == true && _afbryd is null;
        AabnKnap.IsEnabled = valgt is not null;
        SletKnap.IsEnabled = valgt is not null && _afbryd is null;

        // Knappen siger, hvad der SKER — ikke hvor man er. «Arkivér» i
        // arkivet ville lyde som at gøre det samme to gange.
        ArkivKnap.IsEnabled = valgt is not null && _afbryd is null;
        ArkivKnap.Content = valgt?.Gruppe == Gruppe.Arkiv ? "Hent frem" : "Arkivér";

        if (_afbryd is not null) return;   // der koeres — forklaringen staar om det

        // Findes teksten allerede, vises den frem for forklaringen. Det er den,
        // man er kommet efter, naar optagelsen er skrevet ud een gang.
        var færdig = valgt is null ? null : FindTekst(valgt.Mappe);
        ReferatKnap.IsEnabled = færdig is not null && _afbryd is null;
        OmdoebKnap.IsEnabled = valgt is not null && _afbryd is null;
        FlytKnap.IsEnabled = valgt is not null && _afbryd is null;

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

    // -------------------------------------------------------------- omdøbning

    /// <summary>
    /// Omdøber en optagelse.
    ///
    /// Kun titlen i meeting.json ændres — MAPPEN røres ikke. Et dokument, der
    /// allerede er lavet, peger på stien, og en optagelse, der skifter sti,
    /// ville rive den forbindelse over. Navnet er det, man leder efter; stien
    /// er det, appen leder efter.
    /// </summary>
    private void Omdoeb_Click(object sender, RoutedEventArgs e)
    {
        if (Optagelser.SelectedItem is not OptagelseVisning valgt) return;

        var meta = MeetingStore.Load(valgt.Mappe);
        var nu = meta?.Title ?? valgt.Titel;

        var vindue = RenameWindow.TilOptagelse(nu);
        vindue.Owner = Window.GetWindow(this);
        if (vindue.ShowDialog() != true) return;

        var nyt = vindue.NytNavn;

        // Mangler meeting.json, laves den. En optagelse, man ikke kan omdøbe,
        // fordi appen selv aldrig fik skrevet sin egen fil, er ikke brugerens
        // problem at forstå.
        meta ??= Optagelsesgruppe.Nødmetadata(valgt.Mappe);

        try
        {
            meta.Title = nyt;
            MeetingStore.Save(valgt.Mappe, meta);

            var gemtMappe = valgt.Mappe;
            IndlaesOptagelser();

            Optagelser.SelectedItem = Optagelser.Items.Cast<OptagelseVisning>()
                .FirstOrDefault(o => o.Mappe == gemtMappe);

            Status.Text = $"Omdøbt til «{nyt}».";
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke omdøbe", $"Navnet kunne ikke gemmes.\n\n{ex.Message}", Dialogs.Slags.Pas_paa);
        }
    }

    // ------------------------------------------------------------- referatet

    /// <summary>
    /// Fra tekst til referat. Det er dét, hele kæden er til for, og indtil nu
    /// kunne det kun gøres fra kommandolinjen — altså ikke af den, der
    /// installerer appen.
    ///
    /// Sprogmodellen er frivillig. Er der ingen, siges det med hvad man gør
    /// ved det, og resten af appen virker uændret.
    /// </summary>
    private void Referat_Click(object sender, RoutedEventArgs e)
    {
        if (Optagelser.SelectedItem is not OptagelseVisning valgt) return;

        var tekstFil = FindTekst(valgt.Mappe);
        if (tekstFil is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Ingen tekst at arbejde med", "Optagelsen er ikke skrevet ud endnu. Tryk «Transskribér» først.", Dialogs.Slags.Valg);
            return;
        }

        // KUN EUROPA.
        //
        // Her stod et valg mellem en lokal sprogmodel og den europaeiske vej.
        // Det er fjernet 18-08-2026, fordi maalingen ikke efterlader et valg:
        // den lokale model tabte 72 % af navnene og brugte 59 minutter paa et
        // moede, Mistral Medium 3.5 klarede paa 25 sekunder med 23 % tab -
        // for 21 oere. Se doc/maaling-sky.md.
        //
        // Manglende noegle er ikke en fejl, men et manglende trin i
        // opsaetningen. Derfor peges der derhen frem for at sige nej.
        if (SkyNoegle.Hent() is null)
        {
            var opsaet = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Opsætningen mangler et trin",
                "Dokumenter laves af en sprogmodel i Europa, og den er ikke sat op endnu.\n\n" +
                "Det tager et minut: du henter en nøgle hos Mistral og sætter den ind. " +
                "Optagelse og udskrift virker uændret uden.",
                godkend: "Sæt op nu",
                annuller: "Senere",
                slags: Dialogs.Slags.Valg);

            if (opsaet)
                new Documents.SkySetupWindow { Owner = Window.GetWindow(this) }.ShowDialog();

            if (SkyNoegle.Hent() is null) return;
        }

        var skabeloner = PromptTemplate.LoadAll();
        if (skabeloner.Count == 0)
        {
            DraftStore.SeedTemplates();
            skabeloner = PromptTemplate.LoadAll();
        }
        if (skabeloner.Count == 0)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Ingen skabelon", "Der er ingen skabeloner. Opret en under «Skabeloner».", Dialogs.Slags.Valg);
            return;
        }

        var dialog = new Documents.NewDocumentWindow(valgt.Titel, skabeloner)
        { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() != true || dialog.Valgt is null) return;

        var skabelon = dialog.Valgt;

        var meta = MeetingStore.Load(valgt.Mappe);

        var felter = new Dictionary<string, string?>
            {
                ["transskription"] = File.ReadAllText(tekstFil, System.Text.Encoding.UTF8),
                ["titel"] = valgt.Titel,
                ["dato"] = DateTime.Now.ToString("d. MMMM yyyy"),
                ["varighed"] = TimeSpan.FromSeconds(valgt.Sekunder).ToString(@"h\:mm"),
                ["noter"] = LaesNoter(valgt.Mappe),
                ["sprog"] = meta?.Language is null ? "ikke registreret" : Transcriber.LanguageName(meta.Language),

            // HER LAA "ordbog": de rigtige stavemaader fra brugerens rettelser.
            // Maalt 18-08-2026 med og uden, to koersler hver: ingen forskel.
            // Se doc/maaling-sky.md. Feltet findes stadig i skabelonen og
            // udfyldes med tom tekst.
                ["ordbog"] = ""
        };

        var info = new DocumentInfo
        {
            Title = dialog.Titel,
            Description = dialog.Beskrivelse,
            // Id'et er forbindelsen. Sti og titel gemmes kun til visning og
            // bliver frisket op, naar dokumentet laeses.
            SourceMeetingId = meta?.Id.ToString() ?? "",
            SourceRecording = valgt.Mappe,
            SourceTitle = valgt.Titel,
            Template = skabelon.Name,
            // Modellen skal staa rigtigt fra begyndelsen. Bliver referatet
            // lavet i Europa, er det ikke den lokale gguf-fil, der lavede det.
            Model = SkyKatalog.Standard.Navn,
            FileName = DocumentStore.FileNameFor(dialog.Titel, skabelon.Name)
        };

        // HER LAA DEN LOKALE VEJ.
        //
        // Omkring hundrede linjer om skoen over ventetid, plads paa
        // grafikkortet og en advarsel om en halv times koersel. Alt sammen
        // handlede om at koere en model paa DENNE maskine, og intet af det
        // gaelder, naar arbejdet sker et andet sted.
        //
        // Der er ingen bekraeftelse her. Den hoerer til EEN gang - i
        // opsaetningen, hvor man tilslutter sig og laeser efter. Gentaget ved
        // hvert dokument bliver den noget, man klikker vaek uden at laese, og
        // saa beskytter den ingen.
        Jobs.BackgroundJobs.LavDokumentISkyen(SkyKatalog.Standard, skabelon, felter, info, valgt.Mappe);

        Status.Text = "Dokumentet laves — det tager typisk under et minut. " +
                      "Du får besked på klokken øverst, når det er klar.";
    }

    /// <summary>Noterne fra mødet som ren tekst, så de kan gå med til modellen.</summary>
    private static string LaesNoter(string mappe)
    {
        var fil = Path.Combine(mappe, "notes.jsonl");
        if (!File.Exists(fil)) return "";

        var linjer = new List<string>();
        foreach (var l in File.ReadLines(fil, System.Text.Encoding.UTF8))
        {
            var t = System.Text.RegularExpressions.Regex.Match(l, "\"Text\"\\s*:\\s*\"(?<t>[^\"]*)\"");
            var tid = System.Text.RegularExpressions.Regex.Match(l, "\"Timecode\"\\s*:\\s*\"(?<v>[^\"]*)\"");
            if (t.Success && t.Groups["t"].Value.Length > 0)
                linjer.Add($"[{tid.Groups["v"].Value}] {t.Groups["t"].Value}");
        }
        return string.Join("\n", linjer);
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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler motor eller model", install.WhisperCli is null
                    ? "Whisper-motoren er ikke installeret endnu."
                    : "Der er ingen model hentet endnu.\n\nGå til Motor og model og hent en.", Dialogs.Slags.Valg);
            return;
        }

        var wav = Path.Combine(valgt.Mappe, "mikrofon.wav");

        var modelNavn = Path.GetFileNameWithoutExtension(install.ModelPath!).Replace("ggml-", "");
        var udBase = Path.Combine(valgt.Mappe, $"mikrofon_{modelNavn}");

        _afbryd = new CancellationTokenSource();
        KoerKnap.IsEnabled = false;
        AfbrydKnap.Visibility = Visibility.Visible;
        Fremdrift.Visibility = Visibility.Visible;
        Fremdrift.Value = 0;
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
                new TranscriptionRequest(wav, install.ModelPath!, udBase, "auto"),
                fremdrift, _afbryd.Token);

            // HER LAA EFTERRETNINGEN: de rettelser, brugeren havde lavet, blev
            // anvendt paa udskriften bagefter, og originalen gemt som .raa.txt.
            //
            // Den er fjernet 18-08-2026. To grunde, og den anden er den
            // alvorlige:
            //
            //   1. Ordbogen til sprogmodellen maalte nul. Se doc/maaling-sky.md.
            //   2. Reglerne blev laert fra oplaesninger - ogsaa den blandede,
            //      dansk og engelsk mellem hinanden. Der laa regler som
            //      "eller -> or we" og "hvor -> where are". De ville have
            //      omskrevet ENHVER dansk udskrift, hvor ordet "eller" stod.
            //
            // Det naaede aldrig at ske: der fandtes ingen .raa.txt-filer paa
            // disken, saa ingen udskrift blev roert. Men mekanismen var live,
            // og den ventede kun paa den naeste transskription.
            VisResultat(r);

            // Vejen videre foreslås, frem for at man skal finde den selv. Det
            // er alligevel dét, man kom efter — teksten er sjældent målet.
            //
            // Der spørges KUN, når der er en nøgle at gøre det med. Et tilbud,
            // der ender i «du mangler noget», er ikke et tilbud.
            if (SkyNoegle.Hent() is not null)
            {
                var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                    $"«{valgt.Titel}» er skrevet ud",
                    "Vil du lave et dokument ud af den nu — et referat, en opgaveliste eller " +
                    "hvad du selv har lavet af skabeloner?\n\n" +
                    "Du kan også gøre det senere med knappen «Opret dokument».",
                    godkend: "Lav et dokument",
                    annuller: "Ikke nu",
                    slags: Dialogs.Slags.Godt);

                if (ja) Referat_Click(this, new RoutedEventArgs());
            }
            _sidsteMappe = valgt.Mappe;
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Afbrudt.";
        }
        catch (Exception ex)
        {
            Status.Text = "Transskriptionen fejlede.";
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Fejl", ex.Message, Dialogs.Slags.Pas_paa);
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

    private void VisResultat(TranscriptionResult r)
    {
        var tekst = r.Text.Trim();
        var ord = tekst.Split(' ', '\n', '\r').Count(s => s.Length > 0);

        // HER STOD FIRE STORE TAL: lyd, tid brugt, realtidsfaktor og ord.
        //
        // Realtidsfaktoren var udviklertelemetri. Ingen bruger handler på
        // «0,42» — man ser den, nikker, og går videre. Det, man skal vide, er
        // hvor lang tid det tog, og det står i én linje nedenfor. Tallene
        // ligger stadig i Historik, hvor de hører hjemme.
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

        // Historikken. Et usikkert sprogvalg skal stå som «se efter», ikke som
        // fuldført: netop dét kostede en times møde, der blev skrevet ned på
        // engelsk, fordi de første tredive sekunder var på engelsk.
        var usikker = r.LanguageProbability is double p2 && p2 < 0.7;
        Historik.Skriv(
            HaendelseType.Transskription,
            usikker ? "Transskription færdig — sproget er usikkert" : "Transskription færdig",
            $"{TimeSpan.FromSeconds(r.AudioSeconds):hh\\:mm\\:ss} lyd · sprog {Transcriber.LanguageName(r.DetectedLanguage)}" +
            (r.LanguageProbability is double p3 ? $" ({p3 * 100:0}% sikker)" : " (valgt)") +
            $" · RTF {r.RealTimeFactor:0.00}",
            usikker ? Udfald.SeEfter : Udfald.Fuldført,
            r.EngineId, r.TextPath, r.ElapsedSeconds);
        Notifikationer.Meld();

        // Den ene linje, der erstattede de fire store tal. Den siger, hvad man
        // faktisk skal vide: hvor meget lyd, hvor lang tid det tog, og hvad
        // sproget blev.
        Status.Text =
            $"Færdig · {TimeSpan.FromSeconds(r.AudioSeconds):mm\\:ss} lyd skrevet ud på " +
            $"{TimeSpan.FromSeconds(r.ElapsedSeconds):mm\\:ss} · {ord} ord · {sprog}";
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

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Slet «{valgt.Titel}»?",
            $"{valgt.Detaljer} · {valgt.MegaBytes:0.0} MB\n" +
            (hvad.Count > 0 ? $"Følgende slettes: {string.Join(", ", hvad)}.\n" : "") +
            "\nDet kan ikke fortrydes. Mødet kan ikke optages om.\n\n" +
            "Ligger optagelsen i en sikkerhedskopi, findes den stadig der — men " +
            "backup uden lyd indeholder kun teksten.",
            godkend: "Slet for altid",
            annuller: "Behold den",
            slags: Dialogs.Slags.Fejl,
            godkendErStandard: false);

        if (!ja) return;

        try
        {
            Directory.Delete(valgt.Mappe, recursive: true);
            Status.Text = $"«{valgt.Titel}» er slettet ({valgt.MegaBytes:0.0} MB frigjort).";
            _sidsteMappe = null;
            IndlaesOptagelser();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Sletning fejlede", $"Kunne ikke slette:\n\n{ex.Message}\n\n" +
                "Er filen åben i et andet program, så luk det og prøv igen.", Dialogs.Slags.Pas_paa);
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
