using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        // ============ ET WEBINAR HAR INGEN MIKROFONFIL ============
        //
        // Her stod «mikrofon.wav» alene, og det var rigtigt, saa laenge alle
        // optagelser havde et mikrofonspor. Et webinar har det ikke — hele
        // pointen er, at kun det, computeren afspiller, optages.
        //
        // Uden det her stod et webinar i listen som «ingen lyd» med laengden
        // 00:00, og knappen til at skrive det ud pegede paa en fil, der ikke
        // fandtes. Lyden var der hele tiden.
        var wav = Lydfilen(mappe);

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
        // Samme fejl som i dialogen: mm klipper timerne af, saa et moede paa
        // 1:00:50 stod i traeet som «00:50». Timer vises kun, naar der ER
        // timer - ellers ville hvert femminutters moede staa som «00:05:12».
        Detaljer = længde.TotalHours >= 1
            ? $"{dato:dd/MM HH:mm} · {længde:h\\:mm\\:ss}"
            : $"{dato:dd/MM HH:mm} · {længde:mm\\:ss}";

        HarLyd = File.Exists(wav) && Sekunder > 0;
        if (!HarLyd) Detaljer += " · ingen lyd";

        // HELE TIDSRUMMET, TIL HJAELPETEKSTEN.
        //
        // Raekken i traeet er smal og viser en forkortet dato. Naar man skal
        // finde ud af, HVILKET af to moeder paa samme dag man leder efter, er
        // det klokkeslaettet fra og til, der afgoer det - og den oplysning er
        // der ikke plads til paa raekken.
        //
        // Sluttidspunktet regnes ud af laengden frem for at blive laest fra
        // meeting.json. EndedAt mangler paa optagelser fra konsolprogrammet
        // og paa dem, hvor appen doede undervejs, og et tomt "til"-felt er
        // vaerre end et regnet et: lyden ER der, og den varer det, den varer.
        var slut = dato.AddSeconds(Sekunder);

        Tidsrum = Sekunder > 0
            ? $"{dato:dddd d. MMMM yyyy}  ·  {dato:HH:mm}–{slut:HH:mm}"
            : $"{dato:dddd d. MMMM yyyy}  ·  {dato:HH:mm}";

        Varighed = Sekunder <= 0
            ? "Ingen lyd i optagelsen"
            : længde.TotalMinutes < 1
                ? $"Varighed: {længde.TotalSeconds:0} sekunder"
                : længde.TotalHours < 1
                    ? $"Varighed: {længde.TotalMinutes:0} minutter"
                    : længde.Minutes == 0
                        ? $"Varighed: {(int)længde.TotalHours} {((int)længde.TotalHours == 1 ? "time" : "timer")}"
                        : $"Varighed: {(int)længde.TotalHours} t. {længde.Minutes} min.";

        try
        {
            Bytes = Directory.EnumerateFiles(mappe, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch (IOException) { Bytes = 0; }
    }

    /// <summary>
    /// Optagelsens hovedspor: mikrofonen, eller højttaleren når der ikke er
    /// nogen mikrofonfil.
    ///
    /// ÉT STED, OG ALLE SPØRGER DET SAMME STED. Filen skal findes både af
    /// listen, af længden, af knappen der skriver ud og af transskriptionen
    /// selv. Blev svaret regnet ud fire steder, ville de fire holde op med at
    /// være enige — og uenigheden ville vise sig som et webinar, der stod med
    /// «ingen lyd» ét sted og lod sig afspille et andet.
    ///
    /// Mikrofonen vinder, når begge findes. Et onlinemøde har begge spor, og
    /// dér er mikrofonen hovedsporet — højttalersporet tages med ved siden af.
    /// </summary>
    public static string Lydfilen(string mappe)
    {
        var mik = Path.Combine(mappe, "mikrofon.wav");
        if (File.Exists(mik)) return mik;

        var loop = Path.Combine(mappe, "loopback.wav");
        return File.Exists(loop) ? loop : mik;   // findes ingen af dem, er svaret det forventede navn
    }

    /// <summary>Dato og klokkeslæt fra og til. Til hjælpeteksten i træet.</summary>
    public string Tidsrum { get; } = "";

    /// <summary>Hvor længe mødet varede, skrevet ud. Til hjælpeteksten i træet.</summary>
    public string Varighed { get; } = "";

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
    public TranscribeView(string? aabnMappe) : this(aabnMappe, spoerg: true) { }

    /// <summary>
    /// <paramref name="spoerg"/> afgør, om der spørges «skal den skrives ud
    /// nu?».
    ///
    /// Det spørgsmål hører KUN til lige efter en optagelse, hvor man står med
    /// mødet i hovedet. Kommer man hertil fra et søgeresultat eller fra en
    /// linje i historikken, vil man SE optagelsen — og så er et tilbud om at
    /// skrive den ud igen på tyve minutter ikke en hjælp, det er i vejen.
    /// </summary>
    public TranscribeView(string? aabnMappe, bool spoerg) : this(aabnMappe, spoerg, 0) { }

    /// <param name="position">
    /// Tegnnummeret i udskriften, der skal springes til. Nul betyder «vis
    /// bare optagelsen». Kommer fra søgningen, hvor man klikkede på ét
    /// bestemt sted i teksten.
    /// </param>
    public TranscribeView(string? aabnMappe, bool spoerg, int position)
    {
        InitializeComponent();
        IndlaesOptagelser();
        VisSeneste();

        // Panelet til hoejre skal foelge med, mens der skrives ud - ellers
        // staar der «ikke skrevet ud» paa den optagelse, motoren arbejder paa.
        void Foelgmed() => Dispatcher.Invoke(VisSeneste);

        Jobs.Udskriftsvagt.Aendret += Foelgmed;
        Unloaded += (_, _) => Jobs.Udskriftsvagt.Aendret -= Foelgmed;

        if (aabnMappe is null) return;

        VaelgOptagelse(AlleKnuder()
            .Select(k => k.Optagelse?.Mappe)
            .FirstOrDefault(m => m is not null &&
                                 string.Equals(m.TrimEnd('\\'), aabnMappe.TrimEnd('\\'),
                                               StringComparison.OrdinalIgnoreCase)));

        if (Valgt is not { } match) return;

        // Spoerg foerst, naar vinduet er tegnet. En dialog fra en konstruktoer
        // aabner over en halvfaerdig skaerm, og saa kan man ikke se, hvad man
        // siger ja til.
        if (spoerg) Loaded += (_, _) => SpoergOmStart(match);

        // SPRINGET SKER FOERST, NAAR TEKSTEN ER TEGNET.
        //
        // Select og ScrollToLine paa en TextBox, der endnu ikke har maalt sit
        // indhold, goer ingenting - og saa lander man oeverst i en udskrift
        // paa halvtreds sider uden at vide hvorfor.
        if (position > 0) Loaded += (_, _) => SpringTil(position);
    }

    /// <summary>
    /// Ruller hen til et bestemt sted i udskriften og markerer det.
    ///
    /// Markeringen er ikke pynt: uden den lander man et sted i en mur af
    /// tekst og skal selv finde ordet igen. Fokus flyttes til feltet, saa
    /// markeringen faktisk kan ses.
    /// </summary>
    private void SpringTil(int position)
    {
        if (Resultat.Visibility != Visibility.Visible) return;

        Resultat.SpringTil(position);
    }

    private bool _harSpurgt;

    private void SpoergOmStart(OptagelseVisning optagelse)
    {
        if (_harSpurgt) return;
        _harSpurgt = true;

        if (!optagelse.HarLyd) return;

        // JA ER DET NEMME SVAR, OG NEJ ER LIGE SAA NEMT.
        //
        // "Ja" er standardknappen, saa Enter er nok. "Nej tak - senere" siger
        // ligeud, at optagelsen bliver liggende. Uden det lyder et afslag,
        // som om man mister noget, og saa siger folk ja for en sikkerheds
        // skyld til noget, der optager maskinen i fem minutter.

        var minutter = optagelse.Sekunder / 60.0;

        // DEN FORESLÅR ET SKRIDT, DEN STILLER IKKE ET SPØRGSMÅL.
        //
        // Før hed den «Skal den skrives ud til tekst nu?». Det er et
        // spørgsmål, man skal svare på uden at vide, hvad der sker bagefter —
        // og så bliver svaret nej, fordi nej lyder som det uforpligtende.
        //
        // Den siger nu, hvad næste skridt ER, og hvad der sker, når det er
        // gjort: klokken siger til. Det er dét, der gør ventetiden ligegyldig,
        // og det er derfor, det skal stå HER frem for at blive opdaget.
        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"«{optagelse.Titel}» er gemt — næste skridt er at skrive den ud",
            // hh:mm:ss og ikke mm:ss. Et moede paa 1:00:50 stod som «00:50»,
            // fordi mm klipper timerne af - og saa lignede en time et minut.
            $"Længde: {TimeSpan.FromSeconds(optagelse.Sekunder):hh\\:mm\\:ss}. " +
            $"Det tager typisk {Math.Max(1, Math.Round(minutter * 0.3)):0} til {Math.Max(2, Math.Round(minutter * 0.5)):0} minutter " +
            "på denne maskine.\n\n" +
            "Du skal ikke sidde og vente. Når teksten er klar, kommer der et tal på " +
            "klokken øverst til højre — og derfra kan du læse mødet igennem, rette " +
            "navnene og lave et referat.\n\n" +
            "Derfor er det værd at sætte i gang med det samme: maskinen arbejder, " +
            "mens du laver noget andet, og du behøver ikke huske at komme tilbage. " +
            "Du kan roligt optage et nyt møde imens.\n\n" +
            "Venter du, bliver optagelsen liggende, og knappen «Opret transskription» " +
            "øverst gør det samme når som helst.",
            godkend: "Skriv den ud nu",
            annuller: "Vent — jeg gør det senere");

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

    /// <summary>Den valgte knude. Null indtil træet er bygget.</summary>
    private Biblioteker.Biblioteksnode? _valgtKnude;

    /// <summary>
    /// Den valgte optagelse — eller null, hvis markeringen står på et
    /// bibliotek eller en mappe.
    ///
    /// Erstatter den gamle listes SelectedItem. Alt, der før spurgte listen,
    /// spørger nu her, så der er ét sted, der ved, hvad «det valgte» er.
    /// </summary>
    private OptagelseVisning? Valgt => _valgtKnude?.Optagelse;

    private Biblioteker.Biblioteksnode _rodMoeder = null!;
    private Biblioteker.Biblioteksnode _rodArkiv = null!;
    private bool _byggerTrae;

    /// <summary>
    /// Bygger hele træet: biblioteker, mapper og optagelser.
    ///
    /// Træet bygges FORFRA hver gang frem for at blive rettet til. Det koster
    /// ingenting ved den her størrelse, og det fjerner hele klassen af fejl,
    /// hvor en mappe bliver slettet ét sted og bliver stående et andet.
    ///
    /// Til gengæld skal to ting sættes tilbage bagefter, fordi objekterne er
    /// nye: hvad der var foldet ud, og hvad der var valgt. Uden det klapper
    /// træet sammen, hver gang man flytter noget.
    /// </summary>
    private void IndlaesOptagelser()
    {
        var alle = Alle();

        // En mappe, der er i brug, skal staa i traeet - ogsaa hvis
        // mapper.json er gaaet tabt.
        NoteApp.Core.Mapper.SikrFindes(NoteApp.Core.Mapper.Slags.Optagelser,
                                       alle.Select(o => o.Emnemappe));

        _byggerTrae = true;

        var udfoldet = AlleKnuder().Where(k => k.ErUdfoldet).Select(Noegle).ToHashSet();
        var varValgt = _valgtKnude is null ? null : Noegle(_valgtKnude);

        // «FOLDERE» OG IKKE «MØDER».
        //
        // Biblioteket indeholder ikke laengere kun moeder: webinarer og
        // lydfiler lagt ind udefra ligger samme sted. Og det, man vaelger
        // imellem i traeet, er ikke moeder — det er de foldere, man selv har
        // lagt tingene i. Navnet skal sige, hvad man vaelger imellem.
        _rodMoeder = Biblioteker.Biblioteksnode.Bibliotek("Foldere", "\uE8F1", Gruppe.Moede);
        _rodArkiv = Biblioteker.Biblioteksnode.Bibliotek("Arkiv", "\uE7B8", Gruppe.Arkiv);

        var mapper = NoteApp.Core.Mapper.Alle(NoteApp.Core.Mapper.Slags.Optagelser);

        foreach (var rod in new[] { _rodMoeder, _rodArkiv })
        {
            var iGruppen = alle.Where(o => o.Gruppe == rod.Gruppe).ToList();
            rod.Antal = iGruppen.Count;

            // Mapperne foerst, saa optagelserne uden mappe. Samme orden som
            // Stifinder: beholdere over indhold.
            foreach (var m in mapper)
            {
                var mappe = Biblioteker.Biblioteksnode.Mappenode(m, rod.Gruppe);
                foreach (var o in iGruppen.Where(o => m.Equals(o.Emnemappe, StringComparison.CurrentCultureIgnoreCase)))
                    mappe.Boern.Add(Biblioteker.Biblioteksnode.Optagelsesnode(o));

                mappe.Antal = mappe.Boern.Count;
                rod.Boern.Add(mappe);
            }

            foreach (var o in iGruppen.Where(o => string.IsNullOrWhiteSpace(o.Emnemappe)))
                rod.Boern.Add(Biblioteker.Biblioteksnode.Optagelsesnode(o));
        }

        Trae.ItemsSource = new[] { _rodMoeder, _rodArkiv };

        // Alt starter foldet sammen. Kun det, der VAR foldet ud, foldes ud
        // igen - og foerste gang er der ingenting i den maengde.
        foreach (var k in AlleKnuder().Where(k => k.ErBeholder))
            k.ErUdfoldet = udfoldet.Contains(Noegle(k));

        var igen = varValgt is null
            ? null
            : AlleKnuder().FirstOrDefault(k => Noegle(k) == varValgt);

        _valgtKnude = igen;
        if (igen is not null)
        {
            igen.ErValgt = true;
            foreach (var f in Forfaedre(igen)) f.ErUdfoldet = true;
        }

        _byggerTrae = false;

        VisTomBesked();
    }

    /// <summary>
    /// En knudes identitet på tværs af to opbygninger af træet.
    ///
    /// Objekterne er nye hver gang, så referencer duer ikke. En optagelse
    /// kendes på sin sti; en beholder på gruppe og navn.
    /// </summary>
    private static string Noegle(Biblioteker.Biblioteksnode k) =>
        k.Optagelse is { } o ? "o:" + o.Mappe : $"b:{k.Gruppe}:{k.Mappe ?? ""}";

    private IEnumerable<Biblioteker.Biblioteksnode> Forfaedre(Biblioteker.Biblioteksnode k)
    {
        foreach (var rod in AlleRoedder())
        {
            if (rod.Boern.Contains(k)) { yield return rod; yield break; }

            foreach (var mappe in rod.Boern.Where(b => b.ErBeholder))
            {
                if (!mappe.Boern.Contains(k)) continue;
                yield return rod;
                yield return mappe;
                yield break;
            }
        }
    }

    private IEnumerable<Biblioteker.Biblioteksnode> AlleRoedder() =>
        Trae.ItemsSource is null
            ? Enumerable.Empty<Biblioteker.Biblioteksnode>()
            : Trae.Items.OfType<Biblioteker.Biblioteksnode>();

    private IEnumerable<Biblioteker.Biblioteksnode> AlleKnuder() =>
        AlleRoedder().SelectMany(r => r.MedBoern());

    private void Bibliotek_Valgt(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_byggerTrae) return;
        if (e.NewValue is not Biblioteker.Biblioteksnode knude) return;

        _valgtKnude = knude;
        OpdaterValg();
    }

    /// <summary>
    /// Beskeden, når der ikke er nogen optagelser overhovedet. Den skal sige,
    /// hvad man gør — et tomt træ siger kun, at der ikke er noget.
    /// </summary>
    private void VisTomBesked()
    {
        if (_rodMoeder.Antal > 0 || _rodArkiv.Antal > 0) return;

        Status.Text = "Ingen møder endnu.";
        ForklaringOverskrift.Text = "Der ligger ingen møder her";
        ForklaringUnder.Text = "Tryk «Optag møde» øverst, når mødet begynder. Optagelsen dukker op her bagefter.";
    }

    // --------------------------------------------------------- traek og slip

    private Point _traekStart;

    private void Trae_MusNed(object sender, MouseButtonEventArgs e) =>
        _traekStart = e.GetPosition(null);

    /// <summary>
    /// Starter et træk, når musen er flyttet langt nok med knappen nede.
    ///
    /// Grænsen er Windows' egen. Uden den ville et almindeligt klik med en let
    /// rystende hånd starte et træk, og så kan man ikke vælge noget i træet.
    /// Kun optagelser kan trækkes — en mappe, man kunne slæbe ind i sig selv,
    /// er en fejl, der venter.
    /// </summary>
    private void Trae_MusBevaeget(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (_afbryd is not null) return;             // der koeres - flyt ikke noget

        var flyttet = e.GetPosition(null) - _traekStart;

        if (Math.Abs(flyttet.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(flyttet.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (e.OriginalSource is not DependencyObject kilde) return;
        if (FindOpad<TreeViewItem>(kilde) is not { } punkt) return;
        if (punkt.DataContext is not Biblioteker.Biblioteksnode { Optagelse: { } o }) return;

        DragDrop.DoDragDrop(punkt, new DataObject(typeof(OptagelseVisning), o), DragDropEffects.Move);
    }

    private static T? FindOpad<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T) d = VisualTreeHelper.GetParent(d);
        return d as T;
    }

    private void Trae_TraekOver(object sender, DragEventArgs e)
    {
        var maal = MaalUnderMusen(e);

        foreach (var k in AlleKnuder()) k.ErDropmaal = ReferenceEquals(k, maal);

        e.Effects = maal is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void Trae_TraekForlod(object sender, DragEventArgs e) => RydDropmaal();

    private void RydDropmaal()
    {
        foreach (var k in AlleKnuder()) k.ErDropmaal = false;
    }

    /// <summary>
    /// Knuden under musen — men kun hvis der kan slippes noget i den.
    ///
    /// Optagelser er ikke beholdere. Slipper man en optagelse på en anden
    /// optagelse, sker der ingenting, og musen viser det ved ikke at
    /// fremhæve noget.
    /// </summary>
    private Biblioteker.Biblioteksnode? MaalUnderMusen(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(OptagelseVisning))) return null;

        var ramt = Trae.InputHitTest(e.GetPosition(Trae)) as DependencyObject;
        var knude = FindOpad<TreeViewItem>(ramt)?.DataContext as Biblioteker.Biblioteksnode;

        return knude is { ErBeholder: true } ? knude : null;
    }

    /// <summary>
    /// Slipper en optagelse i en knude.
    ///
    /// REGLERNE ER STIFINDERENS: at slippe noget i en beholder lægger det i
    /// den beholder. Knuden bærer selv både gruppe og mappe, så:
    ///
    ///   · en folder under «Foldere» → hent frem af arkivet OG sæt folderen
    ///   · «Foldere» eller «Arkiv»  → skift gruppe, og tag den ud af folderen
    ///
    /// Det sidste er med vilje: biblioteket ER roden, og at trække noget op i
    /// roden er den måde, man tager det ud af en mappe igen.
    ///
    /// Der flyttes ingen filer. Kun meeting.json ændrer sig, så en flytning
    /// ikke kan gå galt halvvejs og efterlade en optagelse et sted, ingen
    /// leder.
    /// </summary>
    private void Trae_Slip(object sender, DragEventArgs e)
    {
        RydDropmaal();

        var maal = MaalUnderMusen(e);
        if (maal is null) return;
        if (e.Data.GetData(typeof(OptagelseVisning)) is not OptagelseVisning flyttet) return;

        var meta = MeetingStore.Load(flyttet.Mappe) ?? Optagelsesgruppe.Nødmetadata(flyttet.Mappe);

        var skiftedeGruppe = flyttet.Gruppe != maal.Gruppe;
        if (skiftedeGruppe)
            meta.ArchivedAt = maal.Gruppe == Gruppe.Arkiv ? DateTimeOffset.Now : null;

        meta.Mappe = maal.Mappe;      // null paa et bibliotek = ud af mappen
        MeetingStore.Save(flyttet.Mappe, meta);

        if (skiftedeGruppe)
        {
            Historik.Skriv(
                maal.Gruppe == Gruppe.Arkiv ? HaendelseType.Arkiveret : HaendelseType.HentetFrem,
                flyttet.Titel,
                maal.Gruppe == Gruppe.Arkiv ? "Lagt i arkivet" : "Hentet frem fra arkivet",
                sti: flyttet.Mappe);
        }

        var sti = flyttet.Mappe;
        maal.ErUdfoldet = true;
        IndlaesOptagelser();
        VaelgOptagelse(sti);

        Status.Text = maal.Mappe is null
            ? $"«{flyttet.Titel}» ligger nu under {maal.Navn}."
            : $"«{flyttet.Titel}» er flyttet til «{maal.Navn}».";
    }

    /// <summary>
    /// Markerer optagelsen med den sti — og folder ud, så den kan ses.
    /// </summary>
    private void VaelgOptagelse(string? sti)
    {
        if (sti is null) return;

        var knude = AlleKnuder().FirstOrDefault(k => k.Optagelse?.Mappe == sti);
        if (knude is null) return;

        foreach (var f in Forfaedre(knude)) f.ErUdfoldet = true;

        _valgtKnude = knude;
        knude.ErValgt = true;
        OpdaterValg();
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

        // Den nye mappe vaelges med det samme - man oprettede den for at
        // laegge noget i den.
        _valgtKnude = null;
        IndlaesOptagelser();

        // Den nye mappe foldes ud og markeres. Den er tom, og det skal man
        // kunne se - ellers ligner det, at der ikke skete noget.
        var ny = AlleKnuder().FirstOrDefault(
            k => k.ErBeholder && k.Gruppe == Gruppe.Moede && k.Mappe == vindue.NytNavn);

        if (ny is null) return;

        _rodMoeder.ErUdfoldet = true;
        _valgtKnude = ny;
        ny.ErValgt = true;
        OpdaterValg();
    }

    /// <summary>
    /// Flytter optagelsen til en mappe. Kun feltet i meeting.json ændrer sig —
    /// mappen på disken bliver liggende, så dokumenter, der allerede peger på
    /// den, stadig finder tilbage.
    /// </summary>
    private void Flyt_Click(object sender, RoutedEventArgs e)
    {
        if (Valgt is not { } valgt) return;

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
        VaelgOptagelse(sti);

        Status.Text = vindue.Valgt is null
            ? "Optagelsen ligger nu uden mappe."
            : $"Flyttet til «{vindue.Valgt}».";
    }

    // HER LAA Fane_Klik. Fanerne «Foldere» og «Arkiv» er blevet til de to
    // rodknuder i bibliotekstraeet, og skiftet sker nu i Bibliotek_Valgt.

    // HER LAA Arkiver_Click, og med den «Arkivér»-ikonet i vaerktoejslinjen.
    //
    // Den gjorde praecis det samme som at traekke optagelsen hen paa «Arkiv»
    // i traeet. To veje til den samme handling er ikke et valg - det er
    // noget, man skal laere at se bort fra. Fjernet 18-08-2026.
    //
    // EEN TING GIK MED: en advarsel, naar man arkiverede et moede, der ikke
    // var skrevet ud endnu. Den er droppet med vilje. Arkivering flytter
    // ingen filer og kan fortrydes ved at traekke tilbage - en dialog ved
    // hvert traek ville goere den nemme vej til den langsomme.

    /// <summary>
    /// Sætter skærmen efter det, der er valgt i træet.
    ///
    /// Hed før Optagelse_Valgt og hang på listens SelectionChanged. Nu kaldes
    /// den også, når der markeres en mappe — så skal knapperne blive grå, og
    /// forklaringen skal frem igen.
    /// </summary>
    private void OpdaterValg()
    {
        var valgt = Valgt;
        KoerKnap.IsEnabled = valgt?.HarLyd == true && _afbryd is null;
        AabnKnap.IsEnabled = valgt is not null;
        SletKnap.IsEnabled = valgt is not null && _afbryd is null;

        // HER SAD «Arkivér»-ikonet. Det gjorde praecis det samme som at
        // traekke optagelsen hen paa «Arkiv» i traeet. To veje til den samme
        // handling er ikke et valg - det er noget, man skal laere at se bort
        // fra. Arkiver_Click staar stadig; den kaldes bare ikke laengere fra
        // vaerktoejslinjen.

        if (_afbryd is not null) return;   // der koeres — forklaringen staar om det

        // Findes teksten allerede, vises den frem for forklaringen. Det er den,
        // man er kommet efter, naar optagelsen er skrevet ud een gang.
        var færdig = valgt is null ? null : FindTekst(valgt.Mappe);

        // KNAPPEN SIGER, HVAD DER SKER - IKKE HVAD DEN HEDDER.
        //
        // «Transskribér» var det samme ord, uanset om optagelsen aldrig var
        // skrevet ud, eller om man var ved at goere det om. Forskellen er
        // vaerd at vide: den ene gang faar man noget nyt, den anden gang
        // overskriver man en udskrift, man maaske har rettet i.
        KoerKnap.Content = færdig is null ? "Opret transskription" : "Opdatér transskription";
        KoerKnap.ToolTip = færdig is null
            ? "Skriv lyden ud til tekst her på maskinen"
            : "Skriv lyden ud igen. Den nuværende transkription bliver overskrevet";

        // Stien til den valgte optagelse. Samme linje samme sted som paa
        // Dokumenter og Skabeloner - det er den, man skal bruge, naar en fil
        // skal findes frem uden om appen.
        Stilinje.Text = valgt?.Mappe ?? "";

        ReferatKnap.IsEnabled = færdig is not null && _afbryd is null;
        KopierKnap.IsEnabled = færdig is not null;
        OmdoebKnap.IsEnabled = valgt is not null && _afbryd is null;
        FlytKnap.IsEnabled = valgt is not null && _afbryd is null;

        if (færdig is not null)
        {
            Forklaring.Visibility = Visibility.Collapsed;
            Resultat.Visibility = Visibility.Visible;
            Resultat.Vis(valgt!.Mappe, Modelnavn());
            Status.Text = "Skrevet ud tidligere. Tryk «Opdatér transskription» for at gøre det om.";
            return;
        }

        Forklaring.Visibility = Visibility.Visible;
        Resultat.Visibility = Visibility.Collapsed;

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
            ? "Fold «Foldere» ud i træet til venstre, vælg en optagelse, og tryk «Opret transskription» øverst. Så skriver appen alt det talte ud som tekst, du kan læse, søge i og rette."
            : $"«{valgt.Titel}» er klar. Tryk «Opret transskription» øverst, så skriver appen alt det talte ud som tekst, du kan læse, søge i og rette.";
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
        if (Valgt is not { } valgt) return;

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

            VaelgOptagelse(gemtMappe);

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
        if (Valgt is not { } valgt) return;

        var tekstFil = FindTekst(valgt.Mappe);
        if (tekstFil is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Ingen tekst at arbejde med", "Optagelsen er ikke skrevet ud endnu. Tryk «Opret transskription» først.", Dialogs.Slags.Valg);
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
                "Optagelse og transkription virker uændret uden.",
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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Ingen mødetyper",
                "Der er ingen mødetyper. Opret en under «Mødetyper».", Dialogs.Slags.Valg);
            return;
        }

        var meta = MeetingStore.Load(valgt.Mappe);

        // Mødetypen fra optagelsen sendes med, saa den er forvalgt i dialogen.
        // Sproget paa optagelsen sendes med, saa dialogen kan sige, hvad
        // sprogvalget betyder: er de to forskellige, oversaettes der undervejs.
        var dialog = new Documents.NewDocumentWindow(valgt.Titel, skabeloner,
                                                    meta?.Moedetype,
                                                    meta?.Language ?? meta?.ValgtSprogLoop ?? meta?.ValgtSprogMik)
        { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() != true || dialog.Valgt is null) return;

        var skabelon = dialog.Valgt;

        var felter = new Dictionary<string, string?>
            {
                ["transskription"] = Udskriftstekst(valgt.Mappe, meta, tekstFil),
                ["titel"] = valgt.Titel,
                ["dato"] = DateTime.Now.ToString("d. MMMM yyyy"),
                ["varighed"] = TimeSpan.FromSeconds(valgt.Sekunder).ToString(@"h\:mm"),
                ["noter"] = LaesNoter(valgt.Mappe),
                ["sprog"] = meta?.Language is null ? "ikke registreret" : Transcriber.LanguageName(meta.Language),
                ["kilde"] = meta?.Kilde ?? "",

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
        Jobs.BackgroundJobs.LavDokumentISkyen(SkyKatalog.Standard, skabelon, felter, info, valgt.Mappe,
                                              dialog.Dokumentsprog);

        // DOKUMENTET LAVES ET ANDET STED, END MAN STAAR.
        //
        // Koerslen lander under «Dokumenter», og fremdriften vises dér. Stod
        // der kun en linje her, ville man blive staaende og vente paa en
        // skaerm, hvor der ikke sker mere - og saa tror man, det gik i staa.
        var gaa = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"«{dialog.Titel}» er sat i gang",
            "Dokumentet laves nu i Europa. Det tager typisk under et minut.\n\n" +
            "Fremdriften vises under «Dokumenter», og klokken øverst giver besked, " +
            "når det er klar. Du kan roligt lave noget andet imens.",
            godkend: "Gå til Dokumenter",
            annuller: "Bliv her");

        if (gaa) (Application.Current.MainWindow as MainWindow)?.GaaTilDokumenter(info.Id);

        Status.Text = "Dokumentet laves — fremdriften står under «Dokumenter».";
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
    /// <summary>
    /// Teksten, dokumentet bliver lavet af.
    ///
    /// DEN RETTEDE UDSKRIFT GÅR FORUD FOR MASKINENS.
    ///
    /// Før 20-08-2026 blev den nyeste .txt-fil i mappen sendt afsted. Det var
    /// forkert på to måder. Rettelserne talte ikke med: har man siddet og rettet
    /// navne og fagord, blev referatet alligevel lavet af maskinens første bud.
    /// Og navnene på talerne fulgte ikke med — sprogmodellen fik «Mig» og
    /// «Gæster» at arbejde med, selv når der stod rigtige navne i mødet.
    ///
    /// Filvalget var desuden usikkert. Mappen indeholder både de enkelte spor
    /// og den flettede samtale, og «nyeste .txt» kunne lige så godt ramme det
    /// ene spor for sig — et referat af den halve samtale, uden at nogen kunne
    /// se det på resultatet.
    ///
    /// Udskriften bruges nu direkte: den rettede, hvis den findes, ellers
    /// maskinens, og med talernes navne sat på. Den gamle vej beholdes som
    /// reserve for optagelser fra før udskriftsfilen fandtes.
    /// </summary>
    private static string Udskriftstekst(string mappe, MeetingMetadata? meta, string? reserve)
    {
        if (Udskrift.HentEllerByg(mappe, Modelnavn()) is { } udskrift)
            return udskrift.SomTekst(meta?.Talere);

        return reserve is not null
            ? File.ReadAllText(reserve, System.Text.Encoding.UTF8)
            : "";
    }

    private static string? FindTekst(string mappe) =>
        Directory.Exists(mappe)
            ? Directory.GetFiles(mappe, "*.txt").OrderByDescending(File.GetLastWriteTime).FirstOrDefault()
            : null;

    private async void Koer_Click(object sender, RoutedEventArgs e)
    {
        if (Valgt is not { } valgt) return;

        var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
        if (!install.IsComplete)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler motor eller model", install.WhisperCli is null
                    ? "Whisper-motoren er ikke installeret endnu."
                    : "Der er ingen model hentet endnu.\n\nGå til Motor og model og hent en.", Dialogs.Slags.Valg);
            return;
        }

        // DET ANDET SPOR.
        //
        // Et onlinemoede optages paa to spor, og indtil nu blev kun
        // mikrofonen skrevet ud. Loopback laa og fyldte over hundrede
        // megabyte pr. moede uden at blive laest - og alt, hvad de andre
        // sagde, manglede derfor i referatet, med mindre det tilfaeldigvis
        // kunne hoeres akustisk i lokalet.
        //
        // Findes filen ikke, var det et fysisk moede. Saa koeres der som foer.
        var loopWav = Path.Combine(valgt.Mappe, "loopback.wav");

        // ============ ET WEBINAR ER ÉT SPOR — HØJTTALERENS ============
        //
        // Tre tilfaelde, ikke to:
        //
        //   fysisk moede   kun mikrofon.wav        eet spor
        //   onlinemoede    begge filer             to spor
        //   webinar        kun loopback.wav        eet spor
        //
        // Det tredje var der ikke foer, og koden gik ud fra, at mikrofonen
        // altid fandtes. Et webinar ville derfor blive skrevet ud fra en fil,
        // der ikke er der.
        //
        // Loesningen er, at hovedsporet er DET SPOR, DER FINDES, og at det
        // andet spor kun er «det andet», naar der faktisk er to. Resten af
        // metoden regner saa som foer.
        var wav = OptagelseVisning.Lydfilen(valgt.Mappe);
        var kunLoop = wav == loopWav;
        var toSpor = !kunLoop && File.Exists(loopWav);

        // ============ SPROGET SPOERGES DER OM HER ============
        //
        // Ikke en indstilling, ikke et gaet. Se SprogvalgWindow for hvorfor -
        // kort fortalt: appen gaettede begge spor til engelsk paa et
        // dansk-norsk moede, og hele udskriften blev vroevl.
        //
        // Der spoerges FOER noget saettes i gang. Et spoergsmaal midt i en
        // koersel paa tyve minutter er ikke et spoergsmaal, det er en
        // afbrydelse.
        var gemtMeta = MeetingStore.Load(valgt.Mappe);

        // ============ RETTELSER SKAL IKKE OVERRASKES ============
        //
        // Den rettede udskrift overskrives ikke af en ny koersel - den ligger
        // i sin egen fil. Men de to bliver uenige: teksten paa skaermen er
        // stadig den rettede, mens maskinens udgave nedenunder er en anden.
        //
        // Det skal siges FOER, ikke opdages bagefter. En time senere kan man
        // ikke huske, hvad man rettede.
        if (Udskrift.HarRettelser(valgt.Mappe))
        {
            var fortsaet = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Du har rettet i transkriptionen",
                "Dine rettelser bliver liggende — de bliver ikke slettet af en ny " +
                "udskrivning.\n\n" +
                "Men de to udgaver kommer til at sige noget forskelligt: du læser " +
                "stadig din rettede tekst, mens maskinens nye udgave ligger nedenunder. " +
                "Vil du have maskinens nye ord frem, skal du fjerne rettelserne " +
                "bagefter.\n\n" +
                "Skriv kun ud igen, hvis der er noget galt med selve transkriptionen — " +
                "et forkert sprog eller et spor, der manglede.",
                godkend: "Skriv ud igen",
                annuller: "Behold det, jeg har rettet",
                slags: Dialogs.Slags.Pas_paa,
                godkendErStandard: false);

            if (!fortsaet) return;
        }

        // Det huskede sprog for HOVEDSPORET. Paa et webinar er hovedsporet
        // hoejttalerens, og saa er det ValgtSprogLoop, der er svaret — ikke
        // mikrofonens, som aldrig blev optaget.
        var sidsteHovedsprog = kunLoop ? gemtMeta?.ValgtSprogLoop : gemtMeta?.ValgtSprogMik;

        var sprogvalg = new SprogvalgWindow(toSpor,
            sidsteHovedsprog, gemtMeta?.ValgtSprogLoop, valgt.Titel, kunLoop)
        { Owner = Window.GetWindow(this) };

        if (sprogvalg.ShowDialog() != true) return;

        var mitSprog = sprogvalg.MitSprog;
        var deresSprog = sprogvalg.DeresSprog ?? mitSprog;

        var modelNavn = Path.GetFileNameWithoutExtension(install.ModelPath!).Replace("ggml-", "");

        // Filnavnet foelger SPORET og ikke rollen. Et webinar skrevet ud til
        // «mikrofon_large-v3.json» ville vaere en fil, der lyver om, hvor den
        // kom fra — og den ville kollidere den dag, en mikrofonfil blev lagt
        // ind ved siden af.
        var udBase = Path.Combine(valgt.Mappe, $"{(kunLoop ? "loopback" : "mikrofon")}_{modelNavn}");
        var loopUdBase = Path.Combine(valgt.Mappe, $"loopback_{modelNavn}");

        // ============ ET SPOR, DER IKKE HAR AENDRET SIG, KOERES IKKE IGEN ============
        //
        // Retter man kun sproget paa gaesternes spor, er der ingen grund til
        // at bruge fem minutter paa mikrofonen igen - den ville give ordret
        // det samme. Det halverer ventetiden i netop det tilfaelde, der er
        // det almindelige, naar man opdager et forkert sprog.
        //
        // FIRE BETINGELSER, OG DE SKAL ALLE VAERE OPFYLDT:
        //   - der blev valgt det SAMME sprog som sidst
        //   - baade json og txt findes fra dengang
        //   - de er nyere end lydfilen (ellers er lyden lavet om)
        //   - modelnavnet staar i filnavnet, saa en anden model giver andre
        //     filer og dermed ingen genbrug
        //
        // Er én af dem ikke opfyldt, koeres sporet. Det er billigere at bruge
        // fem minutter for meget end at flette en udskrift, der ikke passer
        // til det, der blev bedt om.
        bool KanGenbruges(string udbase, string lyd, string? sidst, string nu) =>
            sidst is not null && sidst == nu
            && File.Exists(udbase + ".json") && File.Exists(udbase + ".txt")
            && File.GetLastWriteTimeUtc(udbase + ".json") > File.GetLastWriteTimeUtc(lyd);

        var genbrugMik = KanGenbruges(udBase, wav, sidsteHovedsprog, mitSprog);
        var genbrugLoop = toSpor
                          && KanGenbruges(loopUdBase, loopWav, gemtMeta?.ValgtSprogLoop, sprogvalg.DeresSprog ?? "");

        // ============ ER DER NOGET AT LAVE? ============
        //
        // Kan begge spor genbruges, er der intet at skrive ud, og saa siges
        // det - frem for at lade en bjaelke koere til hundrede uden at noget
        // skete.
        //
        // MEN "SKREVET UD" ER IKKE LAENGERE DET ENESTE, DER KAN MANGLE.
        //
        // Optagelser fra foer talergenkendelsen har to faerdige udskrifter og
        // ingen stemmer. Den her kontrol saa kun paa whisper-filerne og paa
        // sproget, saa knappen svarede "den er skrevet ud i forvejen" og
        // stoppede - og der var ingen vej til at faa navne paa talerne
        // overhovedet. Fundet 20-08-2026, umiddelbart efter funktionen kom ind.
        //
        // Mangler stemmerne, koeres der videre. Genbruget staar ved magt, saa
        // whisper springes over; det er kun talergenkendelsen, der arbejder,
        // og udskriften bliver bygget op af de filer, der allerede ligger.
        var talerSporNu = toSpor ? Samtale.Derfra : "";

        var manglerStemmer = Diarisering.ErInstalleret
                             && Diarisering.Hent(valgt.Mappe, talerSporNu) is null;

        if (genbrugMik && genbrugLoop && !manglerStemmer)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den er skrevet ud i forvejen",
                "Begge spor er allerede skrevet ud på de sprog, du valgte, og lyden er ikke ændret siden. " +
                "Vil du gøre det om alligevel, så vælg et andet sprog — eller slet transkriptionerne i mappen.",
                Dialogs.Slags.Valg);
            return;
        }

        var kunStemmer = genbrugMik && genbrugLoop;

        _afbryd = new CancellationTokenSource();
        KoerKnap.IsEnabled = false;
        AfbrydKnap.Visibility = Visibility.Visible;
        // DER SKAL SIGES NOGET MED DET SAMME.
        //
        // Modellen fylder 2,9 GB og skal laeses ind, foer whisper.cpp melder
        // sin foerste procent. Det tager omkring et halvt minut, og i den tid
        // skete der INGENTING paa skaermen - hverken bjaelke eller besked. Man
        // tror, man har trykket forkert, og trykker igen.
        Fremdriftsrude.Visibility = Visibility.Visible;
        Fremdrift.IsIndeterminate = true;
        Fremdrift.Value = 0;
        Fremdriftstal.Text = "";
        Status.Text = kunStemmer
            ? "Finder stemmerne i optagelsen …"
            : "Indlæser modellen … det tager typisk et halvt minut første gang";

        // VAGTEN SKAL VIDE DET, saa bjaelken oeverst kan staa paa ALLE
        // skaerme. Fremdriften stod foer kun her, og skiftede man skaerm, var
        // der intet spor af, at noget koerte.
        Jobs.Udskriftsvagt.Start(
            Valgt?.Mappe ?? "",
            Valgt?.Titel ?? "Optagelsen",
            kunStemmer ? "Finder stemmerne …" : "Indlæser modellen …");
        Resultat.Ryd();

        // Forklaringen bliver staaende, mens der koeres. Det er praecis dér,
        // den er noget vaerd: den svarer paa "hvor lang tid tager det" og
        // "maa jeg lave noget andet imens".
        Forklaring.Visibility = Visibility.Visible;
        Resultat.Visibility = Visibility.Collapsed;
        ForklaringOverskrift.Text = kunStemmer ? "Finder stemmerne …" : "Skriver lyden ud …";
        ForklaringUnder.Text = kunStemmer
            ? "Teksten er skrevet ud i forvejen og bliver ikke lavet om. Der bliver kun " +
              "skilt stemmer ad, så de kan navngives hver for sig. Det tager omkring et " +
              "minut for hvert kvarters optagelse."
            : "Fremdriften står nederst i ruden. Teksten dukker op her, når den er færdig, og bliver gemt automatisk.";

        // FREMDRIFTEN DAEKKER BEGGE SPOR.
        //
        // Med to spor tager det dobbelt saa lang tid, og en bjaelke, der
        // naar hundrede og saa begynder forfra, ligner en fejl. Hvert spor
        // faar derfor sin halvdel, og teksten siger hvilket.
        var sporNr = 0;
        var sporIAlt = toSpor ? 2 : 1;

        var fremdrift = new Progress<TranscriptionProgress>(p =>
        {
            // Foerste melding fra motoren: nu ER den i gang, saa bjaelken
            // skifter fra "arbejder" til at vise et rigtigt tal.
            Fremdrift.IsIndeterminate = false;
            Fremdrift.Value = (sporNr * 100.0 + p.Percent) / sporIAlt;
            Fremdriftstal.Text = $"{Fremdrift.Value:0} %";

            Jobs.Udskriftsvagt.Fremdrift(Fremdrift.Value,
                sporIAlt > 1 ? $"Skriver lyden ud — spor {sporNr + 1} af {sporIAlt}"
                             : "Skriver lyden ud");

            // HVAD DER SKRIVES UD, IKKE HVAD SPORET HEDDER.
            //
            // Her stod «herfra» og «derfra» - maerkaterne fra selve
            // udskriften. To fejl paa én linje:
            //
            //   1. De var BYTTET OM. Spor 1 er loopback (de andre) og spor 2
            //      er mikrofonen, fordi raekkefoelgen blev vendt, da sproget
            //      skulle afgoeres paa det rene spor. Maerkaterne fulgte ikke
            //      med.
            //   2. Selv rigtigt vendt siger «derfra» ingenting i en
            //      fremdriftslinje. Maerkaterne giver mening i udskriften,
            //      hvor de staar forklaret oeverst - ikke her.
            var hvilket = !toSpor
                ? ""
                : sporNr == 0
                    ? "  ·  spor 1 af 2: de andres lyd"
                    : "  ·  spor 2 af 2: din mikrofon";
            Status.Text = $"{p.Message}   ({modelNavn}, {install.Engine}){hvilket}";
        });

        try
        {
            var motor = new Transcriber(install.WhisperCli!);

            // ============ HVERT SPOR SIT SPROG ============
            //
            // Det er ikke en komplikation - det er svaret paa et rigtigt
            // problem. Maalt paa et dansk-norsk moede: gaesterne talte norsk,
            // vaerten dansk. Med ét spor er det ét sprog, og den ene side
            // bliver skrevet ud gennem den forkerte model.
            //
            // Sprogene kommer fra spoergsmaalet oeverst i metoden. Hvert
            // spor faar sit eget: de to sider af et moede taler ikke
            // noedvendigvis samme sprog.
            // ============ TALERGENKENDELSEN STARTER HER ============
            //
            // Den koerer SAMTIDIG med transskriptionen, ikke bagefter. De to
            // bruger hver sin del af maskinen: whisper regner paa
            // grafikkortet, talergenkendelsen paa CPU'en. Og der er ingen
            // raekkefoelge mellem dem — talergenkendelsen laeser boelgeformen,
            // ikke teksten, saa den kan begynde i samme sekund.
            //
            // Maalt 20-08-2026 paa fire minutters lyd: hver for sig 18,1 s +
            // 36,9 s = 55,0 s. Startet samtidig var alt faerdigt efter 42,0 s.
            // De koster hinanden fem sekunder hver og sparer 24 % af tiden.
            // Paa et moede paa en time betyder det, at talergenkendelsen
            // laegger halvandet minut til frem for ni.
            //
            // HVILKET SPOR: paa et onlinemoede er det gaesternes. Dit eget
            // spor er der kun een person paa, og hvem det er, ved appen. Er
            // der kun eet spor — et fysisk moede eller en lydfil, der er lagt
            // ind — er det dér, alle stemmerne ligger, og saa er det den.
            //
            // DEN MAA ALDRIG KUNNE VAELTE EN UDSKRIFT. Fejler den, mangler
            // vaerktoejet, eller er lyden for kort, saa er resultatet null og
            // udskriften bliver praecis som foer — bare uden navne.
            var talerWav = toSpor ? loopWav : wav;
            var talerSpor = toSpor ? Samtale.Derfra : "";

            var talerJob = Diarisering.ErInstalleret
                ? Diarisering.KoerAsync(talerWav, talerSpor, null, _afbryd.Token)
                : Task.FromResult<Talere?>(null);

            TranscriptionResult? loopR = null;

            if (toSpor)
            {
                if (genbrugLoop) Status.Text = "Gæsternes spor er skrevet ud i forvejen — genbruges.";
                else
                    loopR = await motor.RunAsync(
                        new TranscriptionRequest(loopWav, install.ModelPath!, loopUdBase, deresSprog),
                        fremdrift, _afbryd.Token);

                sporNr = 1;
            }

            TranscriptionResult? r = null;

            if (genbrugMik) Status.Text = "Dit spor er skrevet ud i forvejen — genbruges.";
            else
                r = await motor.RunAsync(
                    new TranscriptionRequest(wav, install.ModelPath!, udBase, mitSprog),
                    fremdrift, _afbryd.Token);

            // ============ ET SPOR UDEN UDSKRIFT ER EN FEJL ============
            //
            // Her stod intet, og det kostede en koersel. Motoren afviste
            // sprogkoden «nb», skrev sin hjaelpetekst og stoppede - uden at
            // det gav en fejl, appen kunne se. Sporet blev bare aldrig
            // skrevet ud.
            //
            // Fletningen laeste saa den GAMLE json fra en tidligere koersel,
            // paa et andet sprog, og satte den sammen med det nye spor. Ud
            // kom en udskrift, der saa faerdig ud og var forkert.
            //
            // Nu kontrolleres det, at filerne faktisk er der. Er de ikke, er
            // det en fejl med det samme - ikke et referat, man opdager det i.
            r ??= LaesFaerdig(udBase, wav, install, mitSprog);
            if (toSpor && loopR is null) loopR = LaesFaerdig(loopUdBase, loopWav, install, deresSprog);

            // Valget huskes FOERST nu, hvor koerslen lykkedes. Gemtes det
            // foer, ville en fejlet koersel efterlade et valg, der ser ud
            // som om det virkede - og saa ville genbruget gribe fat i det
            // naeste gang.
            if (gemtMeta is not null)
            {
                // Sproget gemmes paa DET SPOR, det blev valgt for. Paa et
                // webinar er hovedsporet hoejttalerens, og skreves valget saa
                // i ValgtSprogMik, ville det staa paa et spor, der ikke
                // findes — og genbruget ville aldrig kunne genkende sig selv.
                if (kunLoop) gemtMeta.ValgtSprogLoop = mitSprog;
                else
                {
                    gemtMeta.ValgtSprogMik = mitSprog;
                    gemtMeta.ValgtSprogLoop = sprogvalg.DeresSprog;
                }

                try { MeetingStore.Save(valgt.Mappe, gemtMeta); } catch (IOException) { }
            }

            // ============ UDSKRIFTEN GEMMES STRUKTURERET ============
            //
            // Filen med replikker, tidsstempler og spor er den rigtige. Den
            // txt-fil, resten af appen laeser, er en GENGIVELSE af den - lavet
            // med de navne, der er sat paa talerne.
            //
            // Det er den opdeling, der goer udskriften til noget, man kan
            // rette. En tekstblok kan man laese; en liste af replikker kan man
            // arbejde i.
            var replikker = Samtale.Flet(r.JsonPath, loopR?.JsonPath);
            var udskrift = Udskrift.Af(replikker);

            // Talergenkendelsen har koert imens og er som regel faerdig i
            // forvejen. Fejler den, staar udskriften uden navne — den bliver
            // ikke daarligere af det, den bliver bare ikke bedre.
            Talere? talere = null;
            try { talere = await talerJob; }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { /* uden navne, som foer */ }

            if (talere is not null)
            {
                Diarisering.Gem(valgt.Mappe, talere);

                // Stemmerne SKAL saettes paa, foer udskriften gemmes. Ellers
                // ligger de i deres egen fil uden at staa nogen steder i
                // teksten, og saa er de ikke til nogen nytte.
                Diarisering.Anvend(udskrift, talere);

                // OGSAA PAA DEN RETTEDE UDGAVE.
                //
                // Har man rettet i teksten, er det den fil, editoren laeser -
                // og saa ville navnene staa i maskinens udgave, som ingen
                // kigger i. Rettelserne roeres ikke; der saettes kun stemmer
                // paa de linjer, der allerede er der.
                if (Udskrift.HentRettet(valgt.Mappe) is { } rettet
                    && Diarisering.Anvend(rettet, talere) > 0)
                {
                    rettet.GemRettet(valgt.Mappe);
                }
            }

            udskrift.GemMaskin(valgt.Mappe, modelNavn);

            var navne = gemtMeta?.Talere;
            var udskriftSti = Path.Combine(valgt.Mappe, $"udskrift_{modelNavn}.txt");

            File.WriteAllText(udskriftSti, udskrift.SomTekst(navne),
                              new System.Text.UTF8Encoding(false));

            // TIDEN ER BEGGE SPOR TILSAMMEN.
            //
            // Uden det ville realtidsfaktoren vise halvdelen af det, koerslen
            // faktisk kostede - og RTF er netop det tal, der afgoer, om en
            // udskrift er noget, man venter paa, eller noget, man
            // planlaegger. Et maaletal, der lyver til den gode side, er
            // vaerre end ingen maaling.
            r = r with
            {
                TextPath = udskriftSti,
                ElapsedSeconds = r.ElapsedSeconds + (loopR?.ElapsedSeconds ?? 0)
            };

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

            // GENNEMLÆSNING FØRST — DOKUMENTET BAGEFTER.
            //
            // Her blev der før tilbudt et dokument med det samme, og «Lav et
            // dokument» var den knap, der stod fremhævet. Rækkefølgen var
            // forkert: referatet blev lavet af maskinens første bud, mens de
            // navne og fagord, den havde hørt forkert, stadig stod i teksten —
            // og sporene hed «Mig» og «Gæster».
            //
            // Et forkert navn i udskriften bliver til et forkert navn i
            // referatet, og dér er det sværere at få øje på: teksten er kortere,
            // den ser færdig ud, og lydfilen bliver ikke hørt igennem igen.
            // Fem minutters gennemlæsning er det billigste sted at rette det.
            //
            // Tilbuddet om et dokument står stadig — men som den anden knap.
            // Der spørges KUN, når der er en nøgle at gøre det med. Et tilbud,
            // der ender i «du mangler noget», er ikke et tilbud.
            // KOERSLEN ER SLUT HER, OG DET SKAL VAGTEN VIDE NU.
            //
            // Slut() laa i finally, altsaa EFTER dialogen nedenfor. Saa stod
            // der «skrives ud nu …» i listen, mens man laeste et spoergsmaal
            // om, hvad der skulle ske BAGEFTER - og hang der, indtil man
            // svarede. Transskriptionen er faerdig; dialogen handler om det
            // naeste skridt.
            Jobs.Udskriftsvagt.Slut();
            VisSeneste();

            if (SkyNoegle.Hent() is not null)
            {
                var gennemgaa = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                    $"«{valgt.Titel}» er skrevet ud",
                    "Læs transkriptionen igennem, før der laves et dokument. To ting betaler " +
                    "sig især: at rette navne og fagord, maskinen har hørt forkert, og at " +
                    "give de to spor rigtige navne i stedet for «Mig» og «Gæster».\n\n" +
                    "Dokumentet bliver lavet af den rettede transkription, med navnene på — " +
                    "så det, der står rigtigt her, står også rigtigt i referatet.\n\n" +
                    "Knappen «Opret dokument» står klar bagefter.",
                    godkend: "Gennemgå transkriptionen",
                    annuller: "Lav et dokument nu",
                    slags: Dialogs.Slags.Godt);

                if (gennemgaa)
                {
                    // «GENNEMGAA» SKAL FOERE HEN TIL TEKSTEN - ogsaa naar man
                    // ikke staar paa skaermen laengere.
                    //
                    // Her stod «udskriften er allerede paa skaermen, saa
                    // Gennemgaa er at blive staaende». Det holdt, dengang man
                    // var noedt til at blive. Nu kan man forlade skaermen,
                    // mens der skrives ud - det er hele pointen med bjaelken -
                    // og saa landede man paa en tom Optagelser-skaerm uden
                    // noget valgt. Knappen lovede noget, den ikke gjorde.
                    var id = MeetingStore.Load(valgt.Mappe)?.Id.ToString();

                    if (Window.GetWindow(this) is MainWindow hoved
                        && id is { Length: > 0 })
                    {
                        hoved.GaaTilOptagelse(id);
                    }
                }
                else
                {
                    Referat_Click(this, new RoutedEventArgs());
                }
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
            Jobs.Udskriftsvagt.Slut();

            Fremdriftsrude.Visibility = Visibility.Collapsed;
            Fremdrift.IsIndeterminate = false;
            AfbrydKnap.Visibility = Visibility.Collapsed;
            _afbryd?.Dispose();
            _afbryd = null;
            KoerKnap.IsEnabled = Valgt is { HarLyd: true };
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
        Resultat.Visibility = Visibility.Visible;

        if (Valgt is { } v) Resultat.Vis(v.Mappe, Modelnavn());

        // Sproget staar i statuslinjen, fordi det er den oplysning, der
        // forklarer en tekst, der ser forkert ud. Er detekteringen usikker,
        // skal usikkerheden staa der ogsaa — dansk, norsk og svensk ligner
        // hinanden, og et forkert sprog er ikke til at gennemskue bagefter.
        var sprog = Transcriber.LanguageName(r.DetectedLanguage);
        if (r.LanguageProbability is double p && p < 0.7)
            sprog += $" (usikker, {p * 100:0}%)";

        // GULT ER FORBEHOLDT NOGET, DER KAN GOERES VED.
        //
        // Et usikkert sprogvalg stod foer som «se efter». Men koerslen ER
        // faerdig, og der er intet at handle paa bagefter - udskriften er,
        // hvad maskinen hoerte. Et maerkat, der beder om en handling, som
        // ikke findes, laerer folk at se bort fra maerkatet, og saa virker
        // det heller ikke den dag, der ER noget.
        //
        // Usikkerheden forsvinder ikke: den staar i overskriften og med
        // procent i detaljelinjen. Den er en OPLYSNING, ikke en opgave.
        var usikker = r.LanguageProbability is double p2 && p2 < 0.7;
        Historik.Skriv(
            HaendelseType.Transskription,
            usikker ? "Transskription færdig — sproget er usikkert" : "Transskription færdig",
            $"{TimeSpan.FromSeconds(r.AudioSeconds):hh\\:mm\\:ss} lyd · {SporTekst(r)} · " +
            $"sprog {Transcriber.LanguageName(r.DetectedLanguage)}" +
            (r.LanguageProbability is double p3 ? $" ({p3 * 100:0}% sikker)" : " (valgt)") +
            $" · RTF {r.RealTimeFactor:0.00}",
            Udfald.Fuldført,
            r.EngineId, r.TextPath, r.ElapsedSeconds,
            // Moedemappen udledes af udskriftens sti - den ligger i mappen.
            // Valgt kan have skiftet, mens koerslen loeb.
            kilde: MeetingStore.Load(Path.GetDirectoryName(r.TextPath) ?? "")?.Id.ToString() ?? "");
        Notifikationer.Meld();

        // Den ene linje, der erstattede de fire store tal. Den siger, hvad man
        // faktisk skal vide: hvor meget lyd, hvor lang tid det tog, og hvad
        // sproget blev.
        Status.Text =
            $"Færdig · {Laengde(r.AudioSeconds)} lyd skrevet ud på " +
            $"{Laengde(r.ElapsedSeconds)} · {ord} ord · {sprog} · {SporTekst(r)}";

        AabnKnap.IsEnabled = true;
    }

    /// <summary>
    /// Bygger et resultat af de filer, et spor allerede har efterladt.
    ///
    /// Bruges to steder: når et spor er genbrugt, og som KONTROL af, at et
    /// spor, der lige er kørt, faktisk skrev noget. Motoren kan stoppe uden
    /// at give en fejl, appen kan se — det skete, da den afviste sprogkoden
    /// «nb» og bare skrev sin hjælpetekst.
    ///
    /// Mangler filerne, kastes der. Alternativet er, at fletningen læser en
    /// gammel fil fra en tidligere kørsel og laver en udskrift, der ser
    /// færdig ud og er forkert.
    /// </summary>
    private static TranscriptionResult LaesFaerdig(string udBase, string lyd,
                                                   InstallState install, string sprog)
    {
        var json = udBase + ".json";
        var txt = udBase + ".txt";

        if (!File.Exists(json) || !File.Exists(txt))
            throw new InvalidOperationException(
                $"Sporet «{Path.GetFileName(lyd)}» blev ikke skrevet ud. Motoren gav ingen fil.\n\n" +
                $"Er sproget «{sprog}» et, motoren kender? Se loggen: {udBase}.log");

        return new TranscriptionResult(
            txt, json, udBase + ".log",
            Transcriber.WavSeconds(lyd),
            // Nul sekunder: der blev ikke koert noget. Et opdigtet tal ville
            // goere realtidsfaktoren til en loegn.
            0,
            install.Engine,
            sprog == "auto" ? "da" : sprog);
    }

    /// <summary>
    /// Modelnavnet, som udskriftsfilerne er navngivet efter.
    ///
    /// Slås op frem for at blive husket: skærmen kan være bygget, før motoren
    /// er fundet, og et gemt navn ville så være tomt netop den første gang.
    /// </summary>
    private static string Modelnavn()
    {
        var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);

        return install.ModelPath is null
            ? ""
            : Path.GetFileNameWithoutExtension(install.ModelPath).Replace("ggml-", "");
    }

    /// <summary>
    /// Et tidsrum skrevet ud, med timer kun naar der ER timer.
    ///
    /// mm:ss alene klipper timerne af, saa en optagelse paa 1:00:50 stod som
    /// «00:50» - en time lignede et minut. Fejlen stod tre steder.
    /// </summary>
    private static string Laengde(double sekunder)
    {
        var t = TimeSpan.FromSeconds(sekunder);
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
    }

    /// <summary>
    /// «to spor, flettet» eller «ét spor».
    ///
    /// Forskellen er ikke kosmetisk. Med ét spor mangler alt, hvad de andre
    /// sagde i et onlinemøde — og kan man ikke se det på skærmen og i
    /// historikken, kan man heller ikke bagefter vide, om et referat bygger
    /// på hele mødet eller kun på den ene halvdel.
    /// </summary>
    private static string SporTekst(TranscriptionResult r) =>
        File.Exists(r.JsonPath) && Path.GetFileName(r.TextPath)
            .StartsWith("udskrift_", StringComparison.Ordinal)
            ? "flettet transkription"
            : "ét spor";

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
        if (Valgt is not { } valgt) return;

        var transskriptioner = Directory.Exists(valgt.Mappe)
            ? Directory.GetFiles(valgt.Mappe, "*.txt").Length
            : 0;
        var noter = File.Exists(Path.Combine(valgt.Mappe, "notes.jsonl"));

        var hvad = new List<string>();
        if (valgt.HarLyd) hvad.Add($"lyden ({TimeSpan.FromSeconds(valgt.Sekunder):mm\\:ss})");
        // «3 transskriptioner» ville vaere misvisende: et onlinemoede giver tre
        // txt-filer — de to spor og fletningen — men det er ÉN udskrift af ét
        // moede. Tallet i parentes er der, saa stoerrelsen ikke overrasker.
        if (transskriptioner > 0)
            hvad.Add(transskriptioner == 1
                ? "transkriptionen"
                : $"transkriptionen ({transskriptioner} filer: de enkelte spor og fletningen)");
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

    /// <summary>
    /// Lægger hele udskriften i udklipsholderen.
    ///
    /// Den findes, fordi udskriften er brugerens tekst og ikke appens. Vil
    /// man have den over i en anden model, en mail eller et dokument, man
    /// selv skriver, skal det ikke kræve at finde .txt-filen på disken.
    ///
    /// Der kopieres fra FILEN og ikke fra tekstfeltet: feltet kan være
    /// afkortet under visning, og en halv udskrift, der ligner en hel, er
    /// værre end ingen.
    /// </summary>
    private void Kopier_Click(object sender, RoutedEventArgs e)
    {
        if (Valgt is not { } valgt) return;
        if (FindTekst(valgt.Mappe) is not { } fil) return;

        try
        {
            var tekst = File.ReadAllText(fil, System.Text.Encoding.UTF8).Trim();
            Clipboard.SetText(tekst);

            var ord = tekst.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            Status.Text = $"Hele transkriptionen er kopieret — {ord:N0} ord. Sæt den ind, hvor du vil bruge den.";
            Fremdriftsrude.Visibility = Visibility.Visible;
            Fremdrift.Visibility = Visibility.Collapsed;
            Fremdriftstal.Text = "";
        }
        catch (Exception ex)
        {
            // Udklipsholderen kan vaere laast af et andet program. Det er
            // ikke en fejl i appen, og det skal siges som det er.
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke kopiere",
                $"Udklipsholderen kunne ikke skrives til:\n\n{ex.Message}\n\n" +
                "Det sker, hvis et andet program holder den. Prøv igen om et øjeblik.",
                Dialogs.Slags.Pas_paa);
        }
    }

    private void Afbryd_Click(object sender, RoutedEventArgs e) => _afbryd?.Cancel();

    private void Aabn_Click(object sender, RoutedEventArgs e)
    {
        var mappe = _sidsteMappe ?? Valgt?.Mappe;
        if (mappe is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{mappe}\"") { UseShellExecute = true });
    }

    // --------------------------------------------------- seneste optagelser

    /// <summary>Én optagelse i panelet til højre.</summary>
    public sealed record Senestevisning(string Titel, string Under, string Id,
                                        System.Windows.Media.Brush Kant);

    /// <summary>
    /// De nyeste optagelser og deres tilstand.
    ///
    /// HVORFOR DE STÅR HER
    ///
    /// En optagelse, der aldrig blev skrevet ud, er et hul i arkivet — og det
    /// opdager man ikke ved at lede efter det. Den står her med en gul kant, så
    /// hullet kan ses uden at lede efter det.
    ///
    /// Kun de seks nyeste. Listen er en påmindelse, ikke et arkiv; hele
    /// arkivet står i træet til venstre.
    /// </summary>
    private void VisSeneste()
    {
        List<Senestevisning> liste;

        try
        {
            liste = MeetingStore.Alle()
                .OrderByDescending(m => m.StartedAt)
                .Take(6)
                .Select(m =>
                {
                    var mappe = MeetingStore.FindById(m.Id.ToString())?.Mappe;

                    var skrevet = mappe is not null
                                  && System.IO.Directory.GetFiles(mappe, "udskrift_*.txt").Length > 0;

                    // SKRIVES DEN UD LIGE NU, ER DET DÉT, DER SKAL STAA.
                    // «ikke skrevet ud» paa en optagelse, motoren er i gang
                    // med, er ikke bare upraecist - det er svaret paa netop
                    // det spoergsmaal, man kigger for at faa.
                    var igang = Jobs.Udskriftsvagt.ErIGang(mappe);

                    var laengde = TimeSpan.FromSeconds(m.DurationSeconds);

                    var under = $"{m.StartedAt.LocalDateTime:dd-MM HH:mm}" +
                                (m.DurationSeconds > 0
                                    ? $"  ·  {(laengde.TotalHours >= 1 ? laengde.ToString(@"h\:mm\:ss") : laengde.ToString(@"mm\:ss"))}"
                                    : "") +
                                (igang ? "  ·  skrives ud nu …"
                                       : skrevet ? "" : "  ·  ikke skrevet ud");

                    return new Senestevisning(
                        m.Title ?? "Uden navn",
                        under,
                        m.Id.ToString(),
                        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter()
                            .ConvertFrom(igang ? "#FF5B9DF0"
                                       : skrevet ? "#FF3A4150" : "#FFE8A33D")!);
                })
                .ToList();
        }
        catch (Exception)
        {
            liste = new List<Senestevisning>();
        }

        Senesterude.ItemsSource = liste;
        IngenOptagelser.Visibility = liste.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Vælger optagelsen i træet.
    ///
    /// DER BYGGES IKKE EN NY SKÆRM. I Cockpittet gik det gennem
    /// MainWindow.GaaTilOptagelse, fordi man skulle et andet sted hen. Her ER
    /// vi på skærmen, og at bygge den forfra ville nulstille træets foldede
    /// grene og rulle listen op — for at lande på noget, der lå ét klik væk.
    /// </summary>
    private void Seneste_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Senestevisning v) return;

        string? mappe = null;
        try { mappe = MeetingStore.FindById(v.Id)?.Mappe; } catch (Exception) { }

        if (mappe is null)
        {
            // Slettet uden for appen, mens skaermen stod aaben. Listen laeses
            // forfra, saa den holder op med at love noget, der ikke er der.
            VisSeneste();
            return;
        }

        VaelgOptagelse(mappe);
    }
}
