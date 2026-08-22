using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Transcribe;

/// <summary>Én replik, som skærmen viser og redigerer den.</summary>
public sealed class Replikvisning : INotifyPropertyChanged
{
    private readonly Udskriftslinje _linje;
    private readonly Action _aendret;

    public Replikvisning(Udskriftslinje linje, string navn, bool visNavne, Action aendret,
                         string soegeord = "", string bloktitel = "")
    {
        _linje = linje;
        _aendret = aendret;
        Navn = navn;

        Soegeord = soegeord;
        Bloktitel = bloktitel;

        BlokSynlig = bloktitel.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Markeringen tegnes kun paa de replikker, ordet FAKTISK staar i.
        // Naboerne er der for sammenhaengens skyld, og en tom fremhaevning
        // paa dem ville laegge en gennemsigtig tekst oven paa ingenting.
        var traef = soegeord.Length > 0
                    && linje.Tekst.Contains(soegeord, StringComparison.OrdinalIgnoreCase);

        FremhaevSynlig = traef ? Visibility.Visible : Visibility.Collapsed;

        Tekstfarve = traef
            ? Brushes.Transparent
            : new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xF2));

        // NØGLEN ER DET, NAVNET GEMMES UNDER.
        //
        // Er stemmerne skilt ad, hører navnet til STEMMEN — «DERFRA#1» — og
        // ikke til sporet. Ellers ville et navn på Gæst 2 lande på hele
        // gæstesiden og dermed også på Gæst 1.
        Noegle = linje.Stemme is { Length: > 0 } s ? s : linje.Spor;

        var gaester = linje.Spor == Samtale.Derfra;

        // ÉN PERSON MOD FLERE.
        //
        // Ikonet siger det, et maerkat skulle bruge et ord paa, og det fylder
        // en broekdel. Bag «gaester» kan der sagtens vaere flere personer -
        // det er netop dét, det andet ikon viser uden at skulle forklares.
        //
        // E77B = Contact, E716 = People. Begge efterproevet mod skrifttypens
        // egen tegntabel; et gaet giver en tom firkant.
        Ikon = gaester ? "" : "";

        Farve = gaester
            ? new SolidColorBrush(Color.FromRgb(0xC9, 0x8C, 0xF0))
            : new SolidColorBrush(Color.FromRgb(0x5B, 0x9D, 0xF0));

        var erStemme = linje.Stemme is { Length: > 0 };

        Hjaelp = erStemme
            ? "Klik for at sætte navn på denne stemme. Navnet slår igennem alle steder, hun eller han taler."
            : gaester
                ? "Gæsterne — de øvrige mødedeltagere. Klik for at give dem et navn."
                : "Dig og dem i samme lokale. Klik for at give siden et navn.";

        IkonSynlig = linje.Spor.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NavnSynlig = visNavne && navn.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Nøglen, navnet gemmes under: «HERFRA», «DERFRA» eller «DERFRA#1».</summary>
    public string Noegle { get; }

    public string Soegeord { get; } = "";
    public string Bloktitel { get; } = "";
    public Visibility BlokSynlig { get; }
    public Visibility FremhaevSynlig { get; }
    public Brush Tekstfarve { get; } = Brushes.White;

    /// <summary>
    /// Teksten skåret op i stykker, hvor de gule er dem, der blev søgt på.
    ///
    /// Der skæres på ALLE forekomster, ikke kun den første. Står ordet tre
    /// gange i den samme replik, er det de tre, man leder efter.
    /// </summary>
    public IEnumerable<(string Tekst, bool Gul)> Dele()
    {
        var t = _linje.Tekst;

        if (Soegeord.Length == 0)
        {
            yield return (t, false);
            yield break;
        }

        var i = 0;

        while (i <= t.Length)
        {
            var j = t.IndexOf(Soegeord, i, StringComparison.OrdinalIgnoreCase);

            if (j < 0)
            {
                if (i < t.Length) yield return (t[i..], false);
                yield break;
            }

            if (j > i) yield return (t[i..j], false);

            yield return (t.Substring(j, Soegeord.Length), true);
            i = j + Soegeord.Length;
        }
    }

    public Udskriftslinje Linje => _linje;

    public string Tid => _linje.Tid;
    public string Ikon { get; }
    public string Navn { get; }
    public string Hjaelp { get; }
    public Brush Farve { get; }

    public Visibility IkonSynlig { get; }
    public Visibility NavnSynlig { get; }

    public bool Rettet => _linje.Rettet;

    public string Tekst
    {
        get => _linje.Tekst;
        set
        {
            if (_linje.Tekst == value) return;

            // MASKINENS ORD GEMMES, FOERSTE GANG DER RETTES.
            //
            // Ikke for at kunne fortryde - det klarer redigeringen selv - men
            // fordi det er vaerd at kunne se, hvad der stod, naar man et halvt
            // aar senere er i tvivl om, hvorvidt en formulering er ens egen
            // eller maskinens.
            _linje.Oprindelig ??= _linje.Tekst;

            _linje.Tekst = value;
            _linje.Rettet = _linje.Oprindelig != value;

            Meld(nameof(Tekst));
            Meld(nameof(Rettet));

            _aendret();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Meld(string navn) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(navn));
}

/// <summary>Et forslag til en opgave, som skærmen viser det.</summary>
public sealed record Forslagvisning(string Noegle, string Tekst, string Taler,
                                    string Tid, string Grund);

/// <summary>
/// En oprettet opgave, som skærmen viser og redigerer den.
///
/// Den skriver DIREKTE i <see cref="Opgave"/> og melder til kalderen, at der
/// skal gemmes. Deadline sættes i en datovælger, og en datovælger giver
/// DateTime — opgaven gemmer DateTimeOffset, så den kan læses rigtigt, hvis
/// filen bliver flyttet til en anden tidszone.
/// </summary>
public sealed class Opgavevisning : INotifyPropertyChanged
{
    private readonly Opgave _o;
    private readonly Action _gem;

    public Opgavevisning(Opgave o, Action gem) { _o = o; _gem = gem; }

    public Opgave Bag => _o;
    public Guid Id => _o.Id;
    public string Ejer => _o.Ejer;
    public string Kilde => _o.Kilde;

    public Visibility EjerSynlig => _o.Ejer.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public string Tekst
    {
        get => _o.Tekst;
        set { if (_o.Tekst == value) return; _o.Tekst = value; Meld(); _gem(); }
    }

    public DateTime? Deadline
    {
        get => _o.Deadline?.LocalDateTime;
        set
        {
            _o.Deadline = value is null ? null : new DateTimeOffset(value.Value);
            Meld();
            _gem();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Meld([System.Runtime.CompilerServices.CallerMemberName] string navn = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(navn));
}

/// <summary>Én taler på statistikfanen: navn, taletid og bjælkens længde.</summary>
public sealed record Talerstat(string Navn, string Minutter, string Procent,
                               double Bredde, Brush Farve);

/// <summary>
/// Ét punkt i rullelisten over talere, og én knap i henføringen.
/// Null-nøgle betyder «alle talere».
///
/// Den er OFFENTLIG med vilje. WPF binder gennem refleksion, og en privat
/// type giver tomme felter uden en fejl at gå efter — knapperne ville stå der
/// uden tekst, og bindingen ville se ud til at være forkert skrevet.
/// </summary>
public sealed record Talerpunkt(string? Noegle, string Navn)
{
    public override string ToString() => Navn;
}

// HER LAA KLASSEN «Navnefelt».
//
// Den baar eet felt i den raekke, knappen «Navngiv talere» aabnede. Baade
// knappen og raekken er vaek: navnet saettes nu paa taleren selv, og nøglen
// foelger med i Replikvisning.Noegle. Se Taler_Klik.

/// <summary>
/// Udskriften, som man kan rette i.
///
/// HVORFOR REDIGERING ER MERE END BEKVEMMELIGHED
///
/// Kan man finpudse teksten selv, kan man i mange tilfælde nøjes med at
/// kopiere det, man skal bruge — uden at bede en sprogmodel om noget, og
/// dermed uden at sende noget ud af maskinen. Det er den eneste funktion i
/// appen, der kan gøre skyen unødvendig for en del af arbejdet.
///
/// HVORDAN RETTELSER OG NYE KØRSLER LEVER SIDE OM SIDE
///
/// Rettelserne ligger i deres egen fil. En ny transskription skriver maskinens
/// udgave og rører aldrig den rettede.
/// </summary>
public partial class UdskriftView : UserControl
{
    private readonly DispatcherTimer _gemSenere;

    private Udskrift? _udskrift;
    private string? _mappe;
    private string _model = "";
    private MeetingMetadata? _meta;
    private bool _indlæser;

    /// <summary>Stemmen, navneruden staar aaben for. Null naar den er lukket.</summary>
    private string? _navngiver;

    /// <summary>Replikken, der kan henfoeres til en anden taler. Null naar ruden er lukket.</summary>
    private Udskriftslinje? _henfoer;

    public UdskriftView()
    {
        InitializeComponent();

        // DER GEMMES EFTER EN PAUSE, IKKE VED HVERT TASTETRYK.
        //
        // En udskrift paa en time er hundredvis af replikker. At skrive hele
        // filen ved hvert bogstav ville betyde en diskskrivning i sekundet -
        // og en fil, der konstant er halvvejs skrevet.
        _gemSenere = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _gemSenere.Tick += (_, _) => { _gemSenere.Stop(); Gem(); };
    }

    /// <summary>Den gengivne tekst — det, der kopieres og sendes videre.</summary>
    public string Tekst => _udskrift?.SomTekst(_meta?.Talere) ?? "";

    // -------------------------------------------------------------- visning

    public void Vis(string mappe, string model)
    {
        _indlæser = true;

        _mappe = mappe;
        _model = model;
        _meta = MeetingStore.Load(mappe);
        _udskrift = Udskrift.HentEllerByg(mappe, model);

        if (_udskrift is null)
        {
            Liste.ItemsSource = null;
            RaaTekst.Text = "";
            Hoved.Visibility = Visibility.Collapsed;
            Navnerude.IsOpen = false;
            Meld("");
            VisIngenUdskrift(mappe);
            _indlæser = false;
            return;
        }

        IngenUdskrift.Visibility = Visibility.Collapsed;
        Faner.Visibility = Visibility.Visible;
        _kigEfter.Stop();

        Hoved.Visibility = Visibility.Visible;
        Navnerude.IsOpen = false;

        ByggTalervalg();

        Byg();
        RaaTekst.Text = Raa();
        VisOpsummering();
        VisTal();
        VisOpgaver();

        Meld(_udskrift.ErRettet ? "Rettet" : "");
        _indlæser = false;
    }

    /// <summary>
    /// Forklarer, hvorfor der ikke står noget — og hvad man gør ved det.
    ///
    /// TRE TILSTANDE, OG DE SER ENS UD PÅ SKÆRMEN UDEN DEN HER:
    ///
    ///   ikke skrevet ud     der er kun lyd i mappen
    ///   i gang lige nu      maskinens filer er ved at blive skrevet
    ///   skrevet, ikke samlet  whisper er færdig, men udskriften mangler
    ///
    /// Den tredje findes, fordi en udskrivning kan være sat i gang uden for
    /// appen. Uden den ville en optagelse, der ER skrevet ud, se ud som en,
    /// der aldrig blev det.
    ///
    /// Tiden er regnet ud af den målte hastighed — 0,09 gange lydens længde
    /// på grafikkortet her. Et skøn, der siger «et kvarter», er værd mere end
    /// ingen oplysning: det afgør, om man venter eller laver noget andet.
    /// </summary>
    /// <summary>
    /// Kigger efter, om udskriften er kommet, mens ruden står fremme.
    ///
    /// UDEN DEN VILLE BESKEDEN BLIVE STÅENDE, EFTER AT TEKSTEN VAR KLAR. Man
    /// ville sidde og se på «den bliver skrevet ud lige nu» i et kvarter efter,
    /// den var færdig — og først opdage det ved at klikke væk og tilbage.
    ///
    /// Den kører KUN, mens ruden er fremme, og stopper i samme sekund der er
    /// noget at vise. En optagelse, der er skrevet ud, koster ingenting.
    /// </summary>
    private readonly DispatcherTimer _kigEfter = new() { Interval = TimeSpan.FromSeconds(5) };

    private void VisIngenUdskrift(string mappe)
    {
        Faner.Visibility = Visibility.Collapsed;
        IngenUdskrift.Visibility = Visibility.Visible;

        if (!_kigEfter.IsEnabled)
        {
            _kigEfter.Tick += (_, _) =>
            {
                if (_mappe is null || _model is null || IngenUdskrift.Visibility != Visibility.Visible)
                {
                    _kigEfter.Stop();
                    return;
                }

                // Vis() slukker selv for ruden og for uret, hvis der er kommet
                // noget. Er der ikke, bliver teksten friskere: «i gang» kan
                // vaere blevet til «mangler at blive samlet».
                Vis(_mappe, _model);
            };

            _kigEfter.Start();
        }

        var maskinfiler = Directory.Exists(mappe)
            ? Directory.GetFiles(mappe, "*_*.json")
                .Where(f => !Path.GetFileName(f).StartsWith("udskrift", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : new List<string>();

        // En log, der er roert inden for et minut, betyder at motoren skriver
        // i den lige nu. Det er det eneste spor, en koersel uden for appen
        // efterlader — og det er nok til at sige «vent».
        var log = Directory.Exists(mappe)
            ? Directory.GetFiles(mappe, "*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        var arbejder = log is not null
                       && (DateTime.UtcNow - log.LastWriteTimeUtc) < TimeSpan.FromMinutes(1);

        if (arbejder && maskinfiler.Count == 0)
        {
            IngenOverskrift.Text = "Den bliver skrevet ud lige nu";
            IngenTekst.Text = "Teksten kommer frem her, når den er færdig, og der kommer " +
                              "besked på klokken. Du kan roligt lave noget andet imens.";
            return;
        }

        if (maskinfiler.Count > 0)
        {
            IngenOverskrift.Text = "Lyden er skrevet ud — teksten mangler at blive samlet";
            IngenTekst.Text = arbejder
                ? "Stemmerne bliver skilt ad lige nu. Det tager omkring et minut for hvert " +
                  "kvarters optagelse, og så står teksten her."
                : "Maskinens ord ligger i mappen, men de er ikke sat sammen til en udskrift. " +
                  "Tryk «Opdatér transskription» øverst — den genbruger det, der allerede er " +
                  "lavet, så det går hurtigt.";
            return;
        }

        IngenOverskrift.Text = "Den er ikke skrevet ud endnu";

        var sekunder = 0.0;
        try
        {
            var wav = OptagelseVisning.Lydfilen(mappe);
            if (File.Exists(wav)) sekunder = Transcriber.WavSeconds(wav);
        }
        catch (Exception) { /* uden et skoen staar der bare ingen tid */ }

        // 0,09 gange realtid, maalt paa denne maskine 21-08-2026 paa to
        // webinarer: 1344 sek lyd paa 122 sek, og 3218 sek lyd paa samme
        // faktor. Der rundes op til hele minutter — et skoen, der lyder
        // praecist, bliver troet som et loefte.
        var skoen = sekunder <= 0
            ? ""
            : $" Det tager omkring {Math.Max(1, Math.Round(sekunder * 0.09 / 60)):0} minutter for denne optagelse.";

        IngenTekst.Text = "Tryk «Opdatér transskription» øverst, så går den i gang." + skoen +
                          " Det sker på denne pc — ingen lyd forlader maskinen.";
    }

    public void Ryd()
    {
        _gemSenere.Stop();

        _udskrift = null;
        _mappe = null;

        Liste.ItemsSource = null;
        RaaTekst.Text = "";
        OpsumTekst.Text = "";
        OpsumRude.Visibility = Visibility.Collapsed;
        OpsumTom.Visibility = Visibility.Visible;
        Hoved.Visibility = Visibility.Collapsed;
        Navnerude.IsOpen = false;

        // Ryd betyder «ingen optagelse valgt». Saa er der heller ikke noget at
        // forklare om en manglende udskrift — ruden ville staa og sige, at man
        // skulle trykke paa en knap, der ikke gaelder noget.
        IngenUdskrift.Visibility = Visibility.Collapsed;
        Faner.Visibility = Visibility.Visible;
        _kigEfter.Stop();

        Meld("");
    }

    private string GemtNavn(string spor) =>
        _meta?.Talere.TryGetValue(spor, out var n) == true ? n : "";

    private void Meld(string tekst) => SoegStatus.Text = tekst;

    /// <summary>Bygger listen — filtreret, hvis der er søgt.</summary>
    private void Byg()
    {
        if (_udskrift is null) return;

        var navne = _meta?.Talere;
        var soeg = Soeg.Text.Trim();

        // TALEREN OG SØGEORDET VIRKER SAMMEN.
        //
        // «Alt hvad Espen sagde om pipeline» er begge dele paa een gang, og
        // det er praecis den slags spoergsmaal, man staar med bagefter. De to
        // filtre laegges derfor oven paa hinanden frem for at udelukke
        // hinanden.
        var kunTaler = (Talervalg.SelectedItem as Talerpunkt)?.Noegle;

        var alle = _udskrift.Linjer;

        // ============ SØGERESULTATET ER BLOKKE, IKKE LØSE LINJER ============
        //
        // Et soegeord alene siger sjaeldent nok. «Pipeline» kan staa i et svar
        // paa et spoergsmaal, der blev stillet i replikken foer - og uden den
        // kan man ikke huske, hvad det handlede om.
        //
        // Hvert traef vises derfor med replikken FOER og EFTER. Ligger to traef
        // ved siden af hinanden, smelter deres blokke sammen frem for at gentage
        // de samme linjer to gange.
        //
        // Konteksten hentes fra HELE udskriften, ogsaa naar der er afgraenset
        // paa en taler. Replikken foer og efter er som regel den andens - det
        // er jo en samtale - og en «sammenhaeng», hvor modparten er klippet ud,
        // er ingen sammenhaeng.
        var valgte = new List<Udskriftslinje>();
        var titler = new Dictionary<Udskriftslinje, string>();

        if (soeg.Length == 0)
        {
            valgte = kunTaler is null
                ? alle.ToList()
                : alle.Where(l => Noegle(l) == kunTaler).ToList();
        }
        else
        {
            var traef = new List<int>();

            for (var i = 0; i < alle.Count; i++)
            {
                if (kunTaler is not null && Noegle(alle[i]) != kunTaler) continue;
                if (alle[i].Tekst.Contains(soeg, StringComparison.OrdinalIgnoreCase)) traef.Add(i);
            }

            var med = new SortedSet<int>();
            foreach (var i in traef)
                for (var j = Math.Max(0, i - 1); j <= Math.Min(alle.Count - 1, i + 1); j++)
                    med.Add(j);

            // 1. Hvilken blok hoerer hver medtaget linje til? Ny blok, hver
            //    gang der er hul ned til den forrige.
            var blokAf = new Dictionary<int, int>();
            var nr = 0;
            var forrige = int.MinValue;

            foreach (var i in med)
            {
                if (i != forrige + 1) nr++;
                blokAf[i] = nr;
                forrige = i;
            }

            // 2. Blokkens tidsstempel er det FOERSTE traef i den - ikke
            //    kontekstlinjen foer, som kan ligge et halvt minut tidligere.
            var tidAf = new Dictionary<int, string>();
            foreach (var i in traef)
                if (!tidAf.ContainsKey(blokAf[i])) tidAf[blokAf[i]] = alle[i].Tid;

            // 3. Titlen staar paa blokkens foerste linje. Der taelles BLOKKE og
            //    ikke traef: ligger to traef ved siden af hinanden, er de eet
            //    resultat paa skaermen, og «traef 4 af 9» ville saa ikke passe
            //    med, hvor mange stumper man kan rulle igennem.
            forrige = int.MinValue;

            foreach (var i in med)
            {
                valgte.Add(alle[i]);

                if (i != forrige + 1)
                    titler[alle[i]] = $"TRÆF {blokAf[i]} AF {nr}  ·  {tidAf[blokAf[i]]}";

                forrige = i;
            }
        }

        // NAVNET VISES, NAAR DET SIGER MERE END IKONET.
        //
        // Uden talergenkendelse siger «Mig» og «Gæster» praecis det samme som
        // de to ikoner, og saa er maerkatet stoej; det vises derfor kun, naar
        // man selv har sat et navn.
        //
        // ER stemmerne skilt ad, er det stik modsat: «Gæst 1» og «Gæst 2» er
        // hele pointen, og forskellen kan ikke ses paa ikonet, som er det
        // samme for dem begge.
        var harStemmer = _udskrift.Linjer.Any(l => l.Stemme is { Length: > 0 });
        var harNavne = harStemmer || (navne is not null && navne.Values.Any(v => v.Length > 0));

        // Udskrift.Navn(LINJEN, ...) og ikke (l.Spor, ...).
        //
        // Den med sporet kender kun siderne af moedet og svarer «Gæster», ogsaa
        // naar stemmen er kendt. Det stod paa skaermen 20-08-2026: stemmerne var
        // fundet og skrevet i filen, men listen viste stadig «Gæster», fordi
        // netop DEN linje ikke var rettet med.
        Liste.ItemsSource = valgte
            .Select(l => new Replikvisning(l, Udskrift.Navn(l, navne), harNavne, PaaAendring,
                                           soeg, titler.GetValueOrDefault(l, "")))
            .ToList();

        SoegRyd.Visibility = Soeg.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Beskeden skal sige, hvad der blev filtreret PAA. «Ingen replikker
        // indeholder X» er forkert, naar det var taleren, der skar dem fra.
        var taler = (Talervalg.SelectedItem as Talerpunkt)?.Navn;

        IntetFundet.Visibility = valgte.Count == 0 && (soeg.Length > 0 || kunTaler is not null)
            ? Visibility.Visible : Visibility.Collapsed;

        IntetFundet.Text = (soeg.Length > 0, kunTaler is not null) switch
        {
            (true, true) => $"{taler} siger ikke «{soeg}» nogen steder.",
            (true, false) => $"Ingen replikker indeholder «{soeg}».",
            _ => $"{taler} siger ikke noget i denne udskrift."
        };
    }

    // ---------------------------------------------------- den gule markering

    private static readonly Brush Gul = new SolidColorBrush(Color.FromRgb(0xF5, 0xD1, 0x3B));
    private static readonly Brush PaaGul = new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x1F));

    /// <summary>
    /// Tegner den gule markering i TextBlock'en bag tekstfeltet.
    ///
    /// Den tegnes om, hver gang teksten ændrer sig. Feltet ovenpå er stadig
    /// det, man skriver i — og uden det her ville markeringen blive stående på
    /// de gamle ord, mens man rettede.
    /// </summary>
    private void Fremhaev_Ind(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBlock tb) return;
        if (tb.DataContext is not Replikvisning v) return;

        Tegn(tb, v);

        PropertyChangedEventHandler h = (_, a) =>
        {
            if (a.PropertyName == nameof(Replikvisning.Tekst)) Tegn(tb, v);
        };

        tb.Tag = h;
        v.PropertyChanged += h;
    }

    private void Fremhaev_Ud(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBlock tb) return;
        if (tb.DataContext is not Replikvisning v) return;
        if (tb.Tag is not PropertyChangedEventHandler h) return;

        v.PropertyChanged -= h;
        tb.Tag = null;
    }

    private static void Tegn(System.Windows.Controls.TextBlock tb, Replikvisning v)
    {
        tb.Inlines.Clear();

        foreach (var (tekst, gul) in v.Dele())
            tb.Inlines.Add(gul
                ? new System.Windows.Documents.Run(tekst) { Background = Gul, Foreground = PaaGul }
                : new System.Windows.Documents.Run(tekst));
    }

    // ------------------------------------------------------------ søgningen

    private void SoegRyd_Klik(object sender, RoutedEventArgs e)
    {
        Soeg.Clear();
        Soeg.Focus();
    }

    private void Soeg_Tast(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Soeg.Text.Length == 0) return;

        Soeg.Clear();
        e.Handled = true;
    }

    /// <summary>Nøglen, en replik hører til: stemmen hvis den er kendt, ellers sporet.</summary>
    private static string Noegle(Udskriftslinje l) =>
        l.Stemme is { Length: > 0 } s ? s : l.Spor;


    /// <summary>
    /// Fylder rullelisten med de talere, der faktisk siger noget.
    ///
    /// Den bygges af UDSKRIFTEN og ikke af de navne, der er gemt. Et navn kan
    /// være sat på en stemme, der siden er væk efter en ny kørsel — og en
    /// rulleliste med et punkt, der ikke findes i teksten, giver et tomt svar,
    /// som ligner en fejl.
    /// </summary>
    private void ByggTalervalg()
    {
        if (_udskrift is null)
        {
            Talervalg.ItemsSource = null;
            Talervalg.Visibility = Visibility.Collapsed;
            return;
        }

        var navne = _meta?.Talere;

        var punkter = _udskrift.Linjer
            .Where(l => Noegle(l).Length > 0)
            .GroupBy(Noegle)
            .OrderByDescending(g => g.Sum(l => l.TilMs - l.FraMs))
            .Select(g => new Talerpunkt(g.Key, Udskrift.Navn(g.First(), navne)))
            .ToList();

        // Med under to talere er der intet at afgraense imellem.
        if (punkter.Count < 2)
        {
            Talervalg.ItemsSource = null;
            Talervalg.Visibility = Visibility.Collapsed;
            return;
        }

        var alle = new List<Talerpunkt> { new(null, "Alle talere") };
        alle.AddRange(punkter);

        // AFGRÆNSNINGEN SKAL OVERLEVE, AT LISTEN BYGGES OM.
        //
        // Listen bygges om, hver gang en taler får et navn eller en replik
        // flyttes. Blev valget nulstillet til «Alle talere» hver gang, ville
        // man blive kastet ud af sin egen afgrænsning midt i arbejdet — og
        // det ligner, at appen ikke reagerede på det, man lige gjorde.
        //
        // Findes den valgte taler ikke længere — den sidste replik er flyttet
        // væk fra stemmen — falder den tilbage til «Alle talere».
        var foer = (Talervalg.SelectedItem as Talerpunkt)?.Noegle;
        var igen = alle.FindIndex(p => p.Noegle == foer);

        _indlæser = true;
        Talervalg.ItemsSource = alle;
        Talervalg.SelectedIndex = igen >= 0 ? igen : 0;
        _indlæser = false;

        Talervalg.Visibility = Visibility.Visible;
    }

    private void Taler_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_indlæser || _udskrift is null) return;
        Byg();
    }

    // -------------------------------------------------------------- opgaver

    private Opgaveliste _opgaver = new();
    private List<Opgavekandidat> _kandidater = new();

    /// <summary>
    /// Bygger opgavefanen: de oprettede opgaver øverst, forslagene under.
    ///
    /// Forslagene findes af sig selv, hver gang udskriften åbnes. Knappen
    /// «Find opgaver» er til at køre det om, når man har rettet i teksten —
    /// ikke til at få det til at ske første gang. En knap, man SKAL trykke på
    /// for at få en funktion til at findes, er en funktion, folk ikke opdager.
    /// </summary>
    private void VisOpgaver()
    {
        if (_mappe is null || _udskrift is null)
        {
            Opgaverude.ItemsSource = null;
            Forslagliste.ItemsSource = null;
            return;
        }

        _opgaver = Opgaveliste.Hent(_mappe);
        _kandidater = Opgavefund.Find(_udskrift, _meta?.Talere, _mappe);

        TegnOpgaver();
    }

    private void TegnOpgaver()
    {
        Opgaverude.ItemsSource = _opgaver.Opgaver
            .OrderBy(o => o.Deadline ?? DateTimeOffset.MaxValue)
            .ThenBy(o => o.Oprettet)
            .Select(o => new Opgavevisning(o, GemOpgaver))
            .ToList();

        OpgaveOverskrift.Visibility = _opgaver.Opgaver.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;

        // Et forslag, der allerede er sagt ja eller nej til, skal ikke staa
        // igen. Ellers ville listen se ud, som om intet blev gemt.
        var tilbage = _kandidater
            .Where(k => !_opgaver.ErAfvist(k.Tekst) && !_opgaver.ErOprettet(k.Tekst))
            .ToList();

        var svage = tilbage.Count(k => k.Styrke < Opgavefund.Vises);

        var vist = VisSvage.IsChecked == true
            ? tilbage
            : tilbage.Where(k => k.Styrke >= Opgavefund.Vises).ToList();

        Forslagliste.ItemsSource = vist
            .OrderByDescending(k => k.Styrke)
            .ThenBy(k => k.FraMs)
            .Select(k => new Forslagvisning(
                Opgaveliste.Noegle(k.Tekst), k.Tekst, k.Taler, k.Tid, k.Grund))
            .ToList();

        ForslagOverskrift.Visibility = vist.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        OpgaveStatus.Text = vist.Count == 0 && svage == 0
            ? "Ingen forslag tilbage — alle er enten oprettet eller afvist."
            : svage > 0 && VisSvage.IsChecked != true
                ? $"{vist.Count} forslag. {svage} svage er skjult."
                : $"{vist.Count} forslag.";

        OpgaveTom.Visibility = _opgaver.Opgaver.Count == 0 && vist.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

        OpgaveTom.Text = svage > 0
            ? "Der er ingen stærke forslag i denne udskrift. Sæt flueben i «Vis også svage forslag» " +
              "for at se de øvrige — eller tilføj en opgave i hånden."
            : "Der blev ikke fundet noget, der ligner en aftale i denne udskrift. " +
              "Du kan tilføje en opgave i hånden.";
    }

    private void GemOpgaver()
    {
        if (_mappe is null) return;
        try { _opgaver.Gem(_mappe); } catch (IOException) { }
    }

    private void FindOpgaver_Klik(object sender, RoutedEventArgs e) => VisOpgaver();

    private void VisSvage_Klik(object sender, RoutedEventArgs e) => TegnOpgaver();

    /// <summary>
    /// Opretter en opgave af et forslag.
    ///
    /// EJEREN ER DEN, DER SAGDE DET — IKKE ET GÆT.
    ///
    /// Navnet kommer fra den replik, forslaget blev fundet i, og det navn har
    /// du selv sat på taleren. Derfor kan det stå der uden forbehold. Skal en
    /// anden have opgaven, rettes det i hånden.
    /// </summary>
    private void OpretOpgave_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement k || k.Tag is not string noegle) return;

        var kandidat = _kandidater.FirstOrDefault(x => Opgaveliste.Noegle(x.Tekst) == noegle);
        if (kandidat is null) return;

        // ============ FRISTEN LÆSES UD AF DET, DER BLEV SAGT ============
        //
        // «Jeg sender det på fredag» er en opgave MED en frist. Skulle den
        // sættes i hånden bagefter, ville den ikke blive sat — og en
        // opgaveliste uden frister kan ikke sorteres efter, hvad der haster.
        //
        // Er vendingen tvetydig — «i næste uge» peger på syv dage — sættes
        // datoen alligevel, men mærket som usikker. Så kan skærmen vise
        // forskel på en frist, nogen sagde, og en, appen valgte.
        var frist = Datoforstaaelse.Find(kandidat.Tekst, DateOnly.FromDateTime(DateTime.Today));

        _opgaver.Opgaver.Add(new Opgave
        {
            Tekst = kandidat.Tekst,
            // «Din note» er ikke en person. Noten er DIN, saa opgaven er din -
            // men navnet skal vaere det, der staar paa dit spor.
            Ejer = kandidat.Taler == "Din note"
                ? Udskrift.Navn(Samtale.Herfra, _meta?.Talere)
                : kandidat.Taler,
            Kilde = kandidat.Tid,
            Deadline = frist is null
                ? null
                : new DateTimeOffset(frist.Dato.ToDateTime(TimeOnly.MinValue),
                                     TimeZoneInfo.Local.GetUtcOffset(frist.Dato.ToDateTime(TimeOnly.MinValue))),
            DeadlineUsikker = frist is { Sikker: false }
        });

        GemOpgaver();
        TegnOpgaver();
    }

    private void AfvisOpgave_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement k || k.Tag is not string noegle) return;

        var kandidat = _kandidater.FirstOrDefault(x => Opgaveliste.Noegle(x.Tekst) == noegle);
        if (kandidat is null) return;

        _opgaver.Afvis(kandidat.Tekst);
        GemOpgaver();
        TegnOpgaver();
    }

    private void NyOpgave_Klik(object sender, RoutedEventArgs e)
    {
        _opgaver.Opgaver.Add(new Opgave { Tekst = "" });
        GemOpgaver();
        TegnOpgaver();
    }

    private void SletOpgave_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement k || k.Tag is not Guid id) return;

        _opgaver.Opgaver.RemoveAll(o => o.Id == id);
        GemOpgaver();
        TegnOpgaver();
    }

    // ------------------------------------------------------------------ tal

    /// <summary>
    /// Tallene om mødet: hvornår det var, hvor længe det varede, og hvem der
    /// fyldte.
    ///
    /// PROCENTEN ER AF TALETIDEN, IKKE AF MØDET.
    ///
    /// De to er ikke det samme, og forskellen er ikke lille. Der er pauser,
    /// hvor ingen siger noget, og på et onlinemøde optages der på to spor, så
    /// to personer kan tale i det samme sekund. Regnede man i procent af
    /// mødets længde, ville tallene hverken give hundrede eller kunne
    /// sammenlignes. Af taletiden giver de hundrede, og de svarer på det,
    /// spørgsmålet handler om: hvem fyldte.
    /// </summary>
    private void VisTal()
    {
        StatTalere.ItemsSource = null;
        StatTom.Visibility = Visibility.Collapsed;
        StatNote.Text = "";

        if (_udskrift is null || _udskrift.Linjer.Count == 0)
        {
            StatDato.Text = "—";
            StatStart.Text = StatSlut.Text = StatVarighed.Text = "—";
            StatTom.Visibility = Visibility.Visible;
            StatTom.Text = "Der er ingen udskrift at regne på endnu.";
            return;
        }

        // ----- hvornår og hvor længe
        var start = _meta?.StartedAt;
        var slut = _meta?.EndedAt;

        StatDato.Text = start is { } s
            ? s.LocalDateTime.ToString("dddd 'den' d. MMMM yyyy") is var d && d.Length > 0
                ? char.ToUpper(d[0]) + d[1..]
                : "—"
            : "Datoen er ikke registreret";

        StatStart.Text = start?.LocalDateTime.ToString("HH:mm") ?? "—";
        StatSlut.Text = slut?.LocalDateTime.ToString("HH:mm") ?? "—";

        // Varigheden tages fra mødedata, hvis den er der. Ellers fra sidste
        // replik - det er kortere end mødet, men det er et tal, der er maalt,
        // og ikke et gaet.
        var sekunder = _meta?.DurationSeconds ?? 0;
        var fraUdskrift = false;

        if (sekunder <= 0)
        {
            sekunder = _udskrift.Linjer.Max(l => l.TilMs) / 1000.0;
            fraUdskrift = true;
        }

        var varighed = TimeSpan.FromSeconds(sekunder);
        StatVarighed.Text = varighed.TotalHours >= 1
            ? $"{(int)varighed.TotalHours}:{varighed.Minutes:00}"
            : $"{varighed.Minutes}:{varighed.Seconds:00}";

        // ----- taletiden
        var grupper = _udskrift.Linjer
            .GroupBy(Noegle)
            .Select(g => new
            {
                Navn = Udskrift.Navn(g.First(), _meta?.Talere),
                Spor = g.First().Spor,
                Sekunder = g.Sum(l => l.TilMs - l.FraMs) / 1000.0,
                Replikker = g.Count()
            })
            .OrderByDescending(x => x.Sekunder)
            .ToList();

        var samlet = grupper.Sum(g => g.Sekunder);
        if (samlet <= 0) return;

        var stoerste = grupper[0].Sekunder;

        StatTalere.ItemsSource = grupper.Select(g => new Talerstat(
            g.Navn,
            g.Sekunder >= 60
                ? $"{g.Sekunder / 60:0.0} min"
                : $"{g.Sekunder:0} sek",
            $"{g.Sekunder / samlet * 100:0} %",
            // Bjaelken maales mod den, der taler MEST - ikke mod hundrede
            // procent. Ellers ville tre nogenlunde jaevnbyrdige talere alle
            // faa en kort stump, og forskellen mellem dem forsvinder.
            Math.Max(3, 260 * (g.Sekunder / stoerste)),
            g.Spor == Samtale.Derfra
                ? new SolidColorBrush(Color.FromRgb(0xC9, 0x8C, 0xF0))
                : new SolidColorBrush(Color.FromRgb(0x5B, 0x9D, 0xF0))))
            .ToList();

        var taletid = TimeSpan.FromSeconds(samlet);
        var toSpor = _udskrift.Linjer.Any(l => l.Spor == Samtale.Herfra)
                     && _udskrift.Linjer.Any(l => l.Spor == Samtale.Derfra);

        StatNote.Text =
            $"Der blev talt i alt {taletid.TotalMinutes:0} minutter. Procenterne er af " +
            "taletiden og ikke af mødets længde — der er pauser, hvor ingen siger noget" +
            (toSpor
                ? ", og mødet blev optaget på to spor, så to kan tale i det samme sekund."
                : ".") +
            (fraUdskrift
                ? " Mødets længde er regnet ud fra sidste replik, fordi den ikke er registreret."
                : "");
    }

    /// <summary>
    /// Maskinens egne ord, uden rettelser.
    ///
    /// Læses fra maskinfilen og ikke fra den rettede. Det er hele pointen: har
    /// man rettet, er spørgsmålet et halvt år senere, om en formulering er ens
    /// egen eller maskinens — og det kan kun besvares, hvis begge findes.
    /// </summary>
    private string Raa()
    {
        if (_mappe is null) return "";

        var maskin = Udskrift.MaskinSti(_mappe, _model);
        if (!File.Exists(maskin)) return "Maskinens udgave findes ikke for denne optagelse.";

        try
        {
            var linjer = System.Text.Json.JsonSerializer
                .Deserialize<List<Udskriftslinje>>(File.ReadAllText(maskin, Encoding.UTF8));

            return linjer is null ? "" : new Udskrift { Linjer = linjer }.SomTekst(_meta?.Talere);
        }
        catch (Exception)
        {
            return "Maskinens udgave kunne ikke læses.";
        }
    }

    // ------------------------------------------------------------- søgning

    /// <summary>
    /// Søgning INDE i udskriften.
    ///
    /// Der filtreres frem for at fremhæve. En udskrift på en time er
    /// hundredvis af replikker, og en markering et sted i den mur kræver, at
    /// man alligevel ruller. Vises kun de replikker, ordet står i, er svaret
    /// på skærmen med det samme — og de kan rettes, mens de står der.
    ///
    /// Filtreringen rører ikke teksten. Ryddes feltet, står alt igen.
    /// </summary>
    private void Soeg_Aendret(object sender, TextChangedEventArgs e)
    {
        SoegPladsholder.Visibility = Soeg.Text.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;

        if (_udskrift is null) return;

        var foer = _indlæser;
        _indlæser = true;
        Byg();
        _indlæser = foer;

        var soeg = Soeg.Text.Trim();

        if (soeg.Length == 0)
        {
            Meld(_udskrift.ErRettet ? "Rettet" : "");
            return;
        }

        var n = Liste.ItemsSource is IEnumerable<Replikvisning> v ? v.Count() : 0;
        Meld(n == 1 ? "1 replik" : $"{n} replikker");
    }

    // --------------------------------------------------------- navngivning

    /// <summary>
    /// Navngivningen sker PÅ taleren, ikke i en rude for sig.
    ///
    /// Her lå en knap «Navngiv talere», der åbnede en række felter øverst.
    /// Den havde to problemer. Man skulle gå et andet sted hen for at rette
    /// noget, man sad og kiggede på — og man skulle selv regne ud, hvilket
    /// felt der hørte til hvilken stemme, hvilket kun kan lade sig gøre, hvis
    /// man allerede ved, hvem «Gæst 2» er.
    ///
    /// Nu klikker man på den, det handler om. Navnet gemmes på stemmens nøgle,
    /// så det slår igennem hver eneste replik, den stemme siger.
    /// </summary>
    private void Taler_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement felt || felt.DataContext is not Replikvisning v) return;
        if (v.Noegle.Length == 0 || _meta is null) return;

        _navngiver = v.Noegle;

        var erStemme = v.Noegle.Contains('#');

        NavnTitel.Text = $"Hvem er «{v.Navn}»?";

        NavnUnder.Text = erStemme
            ? "Maskinen har skilt stemmen ud efter, hvordan den lyder — den ved ikke, hvem " +
              "det er. Navnet kommer til at stå alle de steder, denne stemme taler."
            : v.Noegle == Samtale.Derfra
                ? "Det er hele gæstesiden af mødet. Der kan sidde flere personer bag den — " +
                  "skriv gerne flere navne."
                : "Det er dig og dem, der sad i samme lokale som dig.";

        NavnFelt.Text = GemtNavn(v.Noegle);

        // ----- kan replikken henføres til en taler, der allerede findes?
        //
        // Kun relevant, naar maskinen IKKE kunne knytte den til en stemme.
        // Har den en stemme, er spoergsmaalet et andet: hvad stemmen hedder.
        _henfoer = v.Linje;

        var kandidater = v.Linje.Stemme is { Length: > 0 }
            ? new List<Talerpunkt>()
            : _udskrift is null
                ? new List<Talerpunkt>()
                : _udskrift.Linjer
                    .Where(l => l.Spor == v.Linje.Spor && l.Stemme is { Length: > 0 })
                    .GroupBy(l => l.Stemme!)
                    .OrderByDescending(g => g.Sum(l => l.TilMs - l.FraMs))
                    .Select(g => new Talerpunkt(g.Key, Udskrift.Navn(g.First(), _meta.Talere)))
                    .ToList();

        Henfoer.ItemsSource = kandidater;
        HenfoerRude.Visibility = kandidater.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Hvor mange er der i alt uden taler paa dette spor?
        var udenTaler = _udskrift is null
            ? 0
            : _udskrift.Linjer.Count(l => l.Spor == v.Linje.Spor && l.Stemme is not { Length: > 0 });

        HenfoerAlle.ItemsSource = kandidater;
        HenfoerAlleRude.Visibility = kandidater.Count > 0 && udenTaler > 1
            ? Visibility.Visible : Visibility.Collapsed;

        HenfoerAlleMaerkat.Text =
            $"…ELLER LÆG ALLE {udenTaler} REPLIKKER UDEN TALER HOS";

        // Naar begge dele staar fremme, skal feltet sige, hvad det saa goer -
        // ellers ser de to ud som to maader at goere det samme paa.
        NavnFeltMaerkat.Visibility = kandidater.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NavnFeltMaerkat.Text = $"ELLER GIV HELE «{v.Navn}» ET NAVN";

        Navnerude.PlacementTarget = felt;
        Navnerude.IsOpen = true;

        NavnFelt.Focus();
        NavnFelt.SelectAll();
    }

    /// <summary>
    /// Flytter ÉN replik over til en taler, maskinen allerede har fundet.
    ///
    /// Det er en rettelse på linjen, ikke på et navn: stemmen skrives på
    /// replikken og følger med i den rettede udskrift. Skrives optagelsen ud
    /// igen, står maskinens egen udgave uændret ved siden af.
    /// </summary>
    private void Henfoer_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement k || k.Tag is not string noegle) return;
        if (_henfoer is null) return;

        _henfoer.Stemme = noegle;

        Efter_Henfoering();
    }

    /// <summary>
    /// Lægger ALLE replikker uden taler på dette spor hos én taler.
    ///
    /// De er næsten altid korte indskud — «Ja. Ja.», «Mm.» — og de er svære at
    /// placere, netop fordi der er så lidt lyd at gå efter. Kommer de fra den
    /// ene side af et møde, hvor kun én person taler i det stykke, hører de
    /// alle sammen til den samme.
    ///
    /// Det er et valg, brugeren træffer og kan se resultatet af med det samme.
    /// Maskinen ville ikke kunne træffe det: den har allerede prøvet og
    /// undladt — og en gætning, der ser ud som viden, er værre end et hul.
    /// </summary>
    private void HenfoerAlle_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement k || k.Tag is not string noegle) return;
        if (_henfoer is null || _udskrift is null) return;

        var spor = _henfoer.Spor;

        foreach (var l in _udskrift.Linjer)
            if (l.Spor == spor && l.Stemme is not { Length: > 0 })
                l.Stemme = noegle;

        Efter_Henfoering();
    }

    private void Efter_Henfoering()
    {
        _henfoer = null;
        _navngiver = null;
        Navnerude.IsOpen = false;

        ByggTalervalg();
        Byg();
        VisTal();
        PaaAendring();
    }

    private void Navn_Tast(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { SaetNavn(NavnFelt.Text); e.Handled = true; }
        else if (e.Key == Key.Escape) { Navnerude.IsOpen = false; e.Handled = true; }
    }

    private void NavnGem_Klik(object sender, RoutedEventArgs e) => SaetNavn(NavnFelt.Text);

    private void NavnRyd_Klik(object sender, RoutedEventArgs e) => SaetNavn("");

    /// <summary>
    /// Gemmer navnet på den stemme, ruden blev åbnet for — og tegner listen om,
    /// så det står med det samme, hver eneste gang stemmen taler.
    /// </summary>
    private void SaetNavn(string navn)
    {
        Navnerude.IsOpen = false;

        if (_navngiver is null || _meta is null || _mappe is null) { _navngiver = null; return; }

        _meta.Talere[_navngiver] = navn.Trim();
        _navngiver = null;

        ByggTalervalg();
        Byg();
        RaaTekst.Text = Raa();
        VisTal();
        PaaAendring();
    }

    // ------------------------------------------------------------ kopiering

    /// <summary>
    /// Knappen åbner menuen. Der er ikke ét rigtigt svar på, hvad «kopiér»
    /// betyder: skal tidsstemplerne med i en mail? Skal taleren med i et
    /// citat? Det afhænger af, hvor teksten skal hen.
    /// </summary>
    private void Kopier_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.ContextMenu is null) return;

        b.ContextMenu.PlacementTarget = b;
        b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        b.ContextMenu.IsOpen = true;
    }

    private void KopierAlt_Klik(object sender, RoutedEventArgs e) => Kopier(true, true);
    private void KopierTid_Klik(object sender, RoutedEventArgs e) => Kopier(true, false);
    private void KopierRen_Klik(object sender, RoutedEventArgs e) => Kopier(false, false);

    private void Kopier(bool medTid, bool medTaler)
    {
        if (_udskrift is null) return;

        var navne = _meta?.Talere;
        var sb = new StringBuilder();

        // Der kopieres DET, DER STAAR PAA SKAERMEN. Har man soegt, er det de
        // fundne replikker - ellers ville knappen goere noget andet end det,
        // man kigger paa.
        var linjer = Liste.ItemsSource is IEnumerable<Replikvisning> vist
            ? vist.Select(v => v.Linje).ToList()
            : _udskrift.Linjer;

        foreach (var l in linjer)
        {
            if (medTid) sb.Append('[').Append(l.Tid).Append("] ");

            if (medTaler)
            {
                var hvem = Udskrift.Navn(l.Spor, navne);
                if (hvem.Length > 0) sb.Append(hvem).Append(": ");
            }

            sb.AppendLine(l.Tekst.Trim());
            if (medTid || medTaler) sb.AppendLine();
        }

        try
        {
            Clipboard.SetText(sb.ToString().TrimEnd() + Environment.NewLine);
            Meld(linjer.Count == 1 ? "1 replik kopieret" : $"{linjer.Count} replikker kopieret");
        }
        catch (Exception)
        {
            // Udklipsholderen kan vaere laast af et andet program et oejeblik.
            Meld("Kunne ikke kopiere — prøv igen");
        }
    }

    // ------------------------------------------------------------- gemning

    private void PaaAendring()
    {
        if (_indlæser) return;

        Meld("Gemmer …");
        _gemSenere.Stop();
        _gemSenere.Start();
    }

    private void Gem()
    {
        if (_udskrift is null || _mappe is null) return;

        try
        {
            // Rettelserne i deres egen fil. Maskinens udgave roeres aldrig.
            _udskrift.GemRettet(_mappe);

            if (_meta is not null) MeetingStore.Save(_mappe, _meta);

            // Gengivelsen skrives om, saa dokumenter og soegning laeser det
            // rettede - ikke det, maskinen sagde.
            var sti = Path.Combine(_mappe, $"udskrift_{_model}.txt");
            File.WriteAllText(sti, _udskrift.SomTekst(_meta?.Talere), new UTF8Encoding(false));

            Meld(_udskrift.ErRettet
                ? $"Rettet · gemt {DateTime.Now:HH:mm:ss}"
                : $"Gemt {DateTime.Now:HH:mm:ss}");
        }
        catch (IOException ex)
        {
            Meld("Kunne ikke gemme");

            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Rettelsen blev ikke gemt",
                $"{ex.Message}\n\nRet igen om lidt — teksten på skærmen er stadig din.",
                Dialogs.Slags.Pas_paa);
        }
    }

    // --------------------------------------------------------- opsummering

    /// <summary>
    /// Sætter spørgsmålet, fanen stiller — det er ikke det samme for et
    /// webinar som for et møde.
    ///
    /// «Hvad blev der besluttet, hvad blev der aftalt, og hvad stod åbent» er
    /// tre spørgsmål uden svar, når nogen har undervist i en time. Så er
    /// spørgsmålet: hvad ville de lære mig, og hvad kan jeg tage med?
    ///
    /// Teksten skal passe, FØR man trykker. Trykker man på en knap, der lover
    /// beslutninger, og får noget andet, ser resultatet forkert ud — også når
    /// det er rigtigt.
    /// </summary>
    private void Spoergsmaalet()
    {
        var erWebinar = _meta?.Type == MeetingType.Webinar;

        OpsumOverskrift.Text = erWebinar
            ? "Hvad ville webinaret lære dig?"
            : "Hvad handlede mødet om?";

        OpsumForklaring.Text = erWebinar
            ? "En kort opsummering: hvad webinaret handlede om, og det vigtigste, du kan tage med dig. Til at huske det med — ikke et dokument, du skal navngive og gemme."
            : "En kort opsummering på ti linjer: hvad der blev besluttet, hvad der blev aftalt, og hvad der stod åbent. Til at huske mødet med — ikke et dokument, du skal navngive og gemme.";

        OpsumLokalTekst.Text = erWebinar
            ? "Cirka et halvt minut. Intet forlader maskinen. Kort — nogle få linjer og højst fire punkter med det, du kan tage med. Den skriver ikke tal, og det, den skriver, bliver efterprøvet mod udskriften."
            : "Cirka et halvt minut. Intet forlader maskinen. Kort — nogle få linjer og højst fire punkter. Den skriver ikke tal, og det, den skriver, bliver efterprøvet mod udskriften.";
    }

    /// <summary>
    /// Viser opsummeringen, hvis der er lavet en.
    ///
    /// ER UDSKRIFTEN RETTET SIDEN, SIGES DET.
    ///
    /// Opsummeringen blev lavet af den tekst, der stod dengang. Har man
    /// rettet i udskriften bagefter, passer de to ikke længere sammen — og
    /// det er ikke til at se på selve teksten. Kontrolsummen kan se det.
    /// </summary>
    private void VisOpsummering()
    {
        Spoergsmaalet();

        if (_mappe is null) { OpsumTom.Visibility = Visibility.Visible; return; }

        var o = Opsummering.Hent(_mappe);

        OpsumRude.Visibility = o is null ? Visibility.Collapsed : Visibility.Visible;
        OpsumTom.Visibility = o is null ? Visibility.Visible : Visibility.Collapsed;

        if (o is null) return;

        OpsumTekst.Text = o.Tekst;
        OpsumHoved.Text = $"Lavet {o.Lavet.LocalDateTime:d. MMMM yyyy 'kl.' HH:mm} · {o.Model}";

        var nu = Kvitteringer.Kontrolsum(_udskrift?.SomTekst(_meta?.Talere) ?? "");
        var uenig = o.UdskriftSum.Length > 0 && o.UdskriftSum != nu;

        OpsumAdvarsel.Visibility = uenig ? Visibility.Visible : Visibility.Collapsed;
        OpsumAdvarsel.Text = uenig
            ? "Udskriften er ændret, siden opsummeringen blev lavet. De to siger ikke nødvendigvis det samme længere — lav den om, hvis rettelserne betyder noget."
            : "";

        Efterproev(o.Tekst);
    }

    /// <summary>
    /// Holder opsummeringens påstande op mod udskriften.
    ///
    /// Se <see cref="Efterproevning"/> for hvorfor. Kort: en model kan skrive
    /// et tal, der aldrig blev sagt, og det læses som et faktum. Kilden ligger
    /// på maskinen, og opslaget tager millisekunder.
    /// </summary>
    private void Efterproev(string opsummering)
    {
        OpsumTjek.Visibility = Visibility.Collapsed;

        if (_udskrift is null || opsummering.Trim().Length == 0) return;

        List<Ubelagt> fundne;
        try { fundne = Efterproevning.Find(opsummering, _udskrift.SomTekst(_meta?.Talere)); }
        catch (Exception) { return; }

        if (fundne.Count == 0) return;

        OpsumTjek.Visibility = Visibility.Visible;

        OpsumTjekTitel.Text = fundne.Count == 1
            ? "Én påstand kunne ikke findes i udskriften"
            : $"{fundne.Count} påstande kunne ikke findes i udskriften";

        OpsumTjekListe.Text = string.Join("\n",
            fundne.Select(f => $"«{f.Tekst}» — {(f.Slags == "tal" ? "tallet" : "navnet")} står ikke i udskriften"));
    }

    private void OpsumKopier_Klik(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(OpsumTekst.Text); Meld("Opsummeringen er kopieret"); }
        catch (Exception) { Meld("Kunne ikke kopiere — prøv igen"); }
    }

    /// <summary>
    /// Laver opsummeringen.
    ///
    /// Der spørges ikke om lov: knappen står under en tekst, der siger, hvad
    /// den gør, og hvor teksten går hen. En dialog oveni ville være at
    /// spørge to gange om det samme.
    /// </summary>
    /// <summary>
    /// Opsummeringen lavet på maskinen — intet forlader den.
    ///
    /// MÅLT 20-08-2026 PÅ ET MØDE PÅ EN TIME
    ///
    /// Qwen3-4B i Q4_K_M brugte 76 sekunder mod Mistrals 25. Teksten er
    /// ringere, men brugbar: den fandt Alexander, Espen, CloudWorks, Omada og
    /// hovedemnet.
    ///
    /// Den skrev også «allerede seks kunder i Danmark». De ord falder ikke ét
    /// sted i udskriften. Derfor efterprøves resultatet bagefter — se
    /// <see cref="Efterproevning"/> — og det, der ikke kan findes, står gult
    /// over teksten.
    ///
    /// EN 8B-MODEL KAN IKKE BRUGES HER. Vægtene fylder 4.795 MB af kortets
    /// 6.144, og så er der ikke plads til de 23.345 tokens, udskriften fylder.
    /// Den blev afbrudt efter elleve minutter. Den mindre model er ikke et
    /// kompromis — den er den, der virker.
    /// </summary>
    private async void OpsumLokal_Klik(object sender, RoutedEventArgs e)
    {
        if (_mappe is null || _udskrift is null) return;

        var cli = LlmRunner.FindCli();
        if (cli is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den lokale motor mangler",
                "Programmet, der kører en sprogmodel på maskinen, er ikke installeret.\n\n" +
                "Du kan stadig lave opsummeringen i Europa.", Dialogs.Slags.Valg);
            return;
        }

        // MODELVALGET LIGGER ET STED — I Sprogmodeller.Valgt().
        //
        // Det laa foer baade her og paa AI-modeller-skaermen, med hver sin
        // kopi af reglen. To kopier af et valg driver fra hinanden, og saa
        // viser skaermen een model, mens der koeres med en anden.
        if (Sprogmodeller.Valgt() is not { } model)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Sprogmodellen er ikke hentet",
                "En opsummering på maskinen kræver en sprogmodel på " +
                $"{Sprogmodeller.Standard.SizeText}, og den er ikke hentet endnu.\n\n" +
                "Gå til «AI-modeller» og vælg fanen «Opsummering» — der står knappen.\n\n" +
                "Skal teksten være et rigtigt referat, kan du i mellemtiden lave et " +
                "dokument efter en skabelon.",
                Dialogs.Slags.Valg);
            return;
        }

        var tekst = _udskrift.SomTekst(_meta?.Talere);

        if (tekst.Trim().Length < 200)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Der er ikke nok tekst",
                "Udskriften er for kort til, at en opsummering siger mere end teksten selv.",
                Dialogs.Slags.Valg);
            return;
        }

        OpsumLokalKnap.IsEnabled = false;

        var navn = Path.GetFileNameWithoutExtension(model);
        var ur = System.Diagnostics.Stopwatch.StartNew();

        // FREMDRIFTEN SKAL KUNNE SES BEGGE STEDER.
        //
        // OpsumStatus ligger i den TOMME rudes panel, og det er skjult, saa
        // snart der ER en opsummering. Ved en ny koersel stod der derfor
        // ingenting i halvandet minut, og man kunne ikke vide, om appen
        // arbejdede eller var gaaet i staa. Meld() skriver i linjen oeverst,
        // som staar fremme uanset hvad.
        void Sig(string s) { OpsumStatus.Text = s; Meld(s); }

        var tikker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var laeser = _meta?.Type == MeetingType.Webinar ? "webinaret" : "mødet";

        tikker.Tick += (_, _) =>
            Sig($"{navn} læser {laeser} igennem … {ur.Elapsed.TotalSeconds:0} sek.");
        tikker.Start();

        Sig($"{navn} indlæses …");

        try
        {
            // ET WEBINAR SPØRGES OM NOGET ANDET END ET MØDE.
            //
            // Både opskriften og den linje, der følger med udskriften. Stod der
            // «Mødet hedder …» over et webinar, begyndte svaret med «Mødet
            // handlede om …» — og så ledte den efter beslutninger, der ikke
            // fandtes. Set på skærmen 21-08-2026.
            var erWebinar = _meta?.Type == MeetingType.Webinar;
            var slags = erWebinar ? "Webinaret" : "Mødet";
            var titel = _meta?.Title ?? (erWebinar ? "webinaret" : "mødet");

            var svar = await new LlmRunner(cli).RunAsync(
                model,
                Opsummering.LokalOpskrift(_meta?.Type ?? MeetingType.Online),
                $"{slags} hedder «{titel}».\n\nUdskrift:\n{tekst}");

            var ren = svar.Text.Trim();

            // ET AFKORTET SVAR MAA IKKE SE FAERDIGT UD.
            //
            // Modellen stopper midt i et ord, naar pladsen slipper op, og saa
            // stod der «- Cloudworks har planer paa at st» i ruden - uden at
            // noget sagde, at der manglede noget. Ender teksten ikke paa et
            // skilletegn, siges det.
            var afkortet = ren.Length > 0 && !".!?»\")".Contains(ren[^1]);

            Opsummering.Gem(_mappe, new Opsummeringsdata
            {
                Tekst = afkortet
                    ? ren + "\n\n[Svaret stoppede her — modellen løb tør for plads. " +
                            "Prøv igen, eller lav den i Europa.]"
                    : ren,
                Model = navn + " (på maskinen)",
                UdskriftSum = Kvitteringer.Kontrolsum(tekst)
            });

            VisOpsummering();
            OpsumStatus.Text = "";

            Meld(afkortet
                ? $"Opsummeringen blev afkortet — {ur.Elapsed.TotalSeconds:0} sek."
                : $"Opsummeringen er lavet på maskinen — {ur.Elapsed.TotalSeconds:0} sek.");
        }
        catch (Exception ex)
        {
            OpsumStatus.Text = "";

            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Opsummeringen blev ikke lavet",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
        finally
        {
            tikker.Stop();
            OpsumLokalKnap.IsEnabled = true;
        }
    }

    // HER LAA OpsumLav_Klik — opsummeringen lavet hos Mistral.
    //
    // Den gjorde det samme som et dokument, bare uden skabelon og uden et navn
    // at gemme det under: to veje til det samme, hvor den ene var ringere.
    //
    // Opsummeringen er nu ALTID den lille, lokale — ti linjer til at huske
    // moedet med. Skal teksten vaere et rigtigt referat, en opgaveliste eller
    // et tilbud, er dokumenter vejen, og skaermen siger hvor man gaar hen.

    // ---------------------------------------------------------- springet hen

    /// <summary>
    /// Ruller hen til et bestemt tegnnummer i den gengivne tekst.
    ///
    /// Søgningen på tværs kender teksten som én streng; skærmen viser den som
    /// replikker. Her regnes det ene om til det andet.
    /// </summary>
    public void SpringTil(int position)
    {
        if (_udskrift is null) return;

        var navne = _meta?.Talere;

        var toSpor = _udskrift.Linjer.Any(l => l.Spor == Samtale.Herfra)
                     && _udskrift.Linjer.Any(l => l.Spor == Samtale.Derfra);

        var nu = toSpor ? Samtale.Forklaring(navne).Length : 0;

        for (var i = 0; i < _udskrift.Linjer.Count; i++)
        {
            var l = _udskrift.Linjer[i];
            var hvem = Udskrift.Navn(l.Spor, navne);

            var laengde = 1 + l.Tid.Length + 1
                          + (hvem.Length > 0 ? 1 + hvem.Length + 1 : 0)
                          + 1 + l.Tekst.Trim().Length
                          + Environment.NewLine.Length * 2;

            if (position >= nu && position < nu + laengde)
            {
                if (Liste.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement raekke)
                    raekke.BringIntoView();
                return;
            }

            nu += laengde;
        }
    }
}
