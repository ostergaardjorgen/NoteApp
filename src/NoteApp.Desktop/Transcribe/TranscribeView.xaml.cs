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
        if (position < 0 || position >= Resultat.Text.Length) return;

        Resultat.Focus();
        Resultat.Select(position, Math.Min(60, Resultat.Text.Length - position));
        Resultat.ScrollToLine(Math.Max(0, Resultat.GetLineIndexFromCharacterIndex(position) - 4));
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

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"«{optagelse.Titel}» er gemt. Skal den skrives ud til tekst nu?",
            // hh:mm:ss og ikke mm:ss. Et moede paa 1:00:50 stod som «00:50»,
            // fordi mm klipper timerne af - og saa lignede en time et minut.
            $"Længde: {TimeSpan.FromSeconds(optagelse.Sekunder):hh\\:mm\\:ss}. " +
            $"Det tager typisk {Math.Max(1, Math.Round(minutter * 0.3)):0} til {Math.Max(2, Math.Round(minutter * 0.5)):0} minutter " +
            "på denne maskine.\n\n" +
            "Du kan roligt lave noget andet imens — også optage et nyt møde. " +
            "Siger du nej tak, bliver optagelsen liggende, og du kan gøre det når som helst med knappen øverst.",
            godkend: "Ja, skriv den ud",
            annuller: "Nej tak — senere");

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

        _rodMoeder = Biblioteker.Biblioteksnode.Bibliotek("Møder", "\uE8F1", Gruppe.Moede);
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
    ///   · en mappe under «Møder»  → hent frem af arkivet OG sæt mappen
    ///   · «Møder» eller «Arkiv»   → skift gruppe, og tag den ud af mappen
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

    // HER LAA Fane_Klik. Fanerne «Møder» og «Arkiv» er blevet til de to
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
            : "Skriv lyden ud igen. Den nuværende udskrift bliver overskrevet";

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
            Resultat.Text = File.ReadAllText(færdig, System.Text.Encoding.UTF8).Trim();
            Resultat.Foreground = (Brush)FindResource("Tekst");
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
            ? "Fold «Møder» ud i træet til venstre, vælg en optagelse, og tryk «Opret transskription» øverst. Så skriver appen alt det talte ud som tekst, du kan læse, søge i og rette."
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

        var wav = Path.Combine(valgt.Mappe, "mikrofon.wav");

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
        var toSpor = File.Exists(loopWav);

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

        var sprogvalg = new SprogvalgWindow(toSpor,
            gemtMeta?.ValgtSprogMik, gemtMeta?.ValgtSprogLoop, valgt.Titel)
        { Owner = Window.GetWindow(this) };

        if (sprogvalg.ShowDialog() != true) return;

        var mitSprog = sprogvalg.MitSprog;
        var deresSprog = sprogvalg.DeresSprog ?? mitSprog;

        var modelNavn = Path.GetFileNameWithoutExtension(install.ModelPath!).Replace("ggml-", "");
        var udBase = Path.Combine(valgt.Mappe, $"mikrofon_{modelNavn}");
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

        var genbrugMik = KanGenbruges(udBase, wav, gemtMeta?.ValgtSprogMik, mitSprog);
        var genbrugLoop = toSpor
                          && KanGenbruges(loopUdBase, loopWav, gemtMeta?.ValgtSprogLoop, sprogvalg.DeresSprog ?? "");

        // Kan begge genbruges, er der intet at lave. Saa siges det, frem for
        // at lade en bjaelke koere til hundrede uden at noget skete.
        if (genbrugMik && genbrugLoop)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den er skrevet ud i forvejen",
                "Begge spor er allerede skrevet ud på de sprog, du valgte, og lyden er ikke ændret siden. " +
                "Vil du gøre det om alligevel, så vælg et andet sprog — eller slet udskrifterne i mappen.",
                Dialogs.Slags.Valg);
            return;
        }

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
        Status.Text = "Indlæser modellen … det tager typisk et halvt minut første gang";
        Resultat.Text = "";

        // Forklaringen bliver staaende, mens der koeres. Det er praecis dér,
        // den er noget vaerd: den svarer paa "hvor lang tid tager det" og
        // "maa jeg lave noget andet imens".
        Forklaring.Visibility = Visibility.Visible;
        Resultat.Visibility = Visibility.Collapsed;
        ForklaringOverskrift.Text = "Skriver lyden ud …";
        ForklaringUnder.Text =
            "Fremdriften står nederst i ruden. Teksten dukker op her, når den er færdig, og bliver gemt automatisk.";

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
                gemtMeta.ValgtSprogMik = mitSprog;
                gemtMeta.ValgtSprogLoop = sprogvalg.DeresSprog;
                try { MeetingStore.Save(valgt.Mappe, gemtMeta); } catch (IOException) { }
            }

            if (loopR is not null)
            {
                // Fletningen er den, dokumenterne skal bruge. Den skrives til
                // sidst, saa den er den nyeste .txt i mappen - det er den,
                // resten af appen finder frem.
                var samtale = Samtale.Flet(r.JsonPath, loopR.JsonPath,
                    Transcriber.LanguageName(r.DetectedLanguage),
                    Transcriber.LanguageName(loopR.DetectedLanguage));
                if (samtale is not null)
                {
                    var samtaleSti = Path.Combine(valgt.Mappe, $"samtale_{modelNavn}.txt");
                    File.WriteAllText(samtaleSti, samtale, new System.Text.UTF8Encoding(false));

                    // TIDEN ER BEGGE SPOR TILSAMMEN, OG SPROGET ER DET RENE
                    // SPORS.
                    //
                    // Uden tiden ville realtidsfaktoren vise halvdelen af det,
                    // koerslen faktisk kostede - og RTF er netop det tal, der
                    // afgoer, om en udskrift er noget, man venter paa, eller
                    // noget, man planlaegger. Et maaletal, der lyver til den
                    // gode side, er vaerre end ingen maaling.
                    r = r with
                    {
                        TextPath = samtaleSti,
                        ElapsedSeconds = r.ElapsedSeconds + loopR.ElapsedSeconds
                    };
                }
            }

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
        Resultat.Text = tekst.Length == 0 ? "(tom transskription — var der lyd på optagelsen?)" : tekst;
        Resultat.Foreground = (Brush)FindResource("Tekst");

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
        Path.GetFileName(r.TextPath).StartsWith("samtale_", StringComparison.Ordinal)
            ? "to spor, flettet"
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
                ? "udskriften"
                : $"udskriften ({transskriptioner} filer: de enkelte spor og fletningen)");
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
            Status.Text = $"Hele udskriften er kopieret — {ord:N0} ord. Sæt den ind, hvor du vil bruge den.";
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
}
