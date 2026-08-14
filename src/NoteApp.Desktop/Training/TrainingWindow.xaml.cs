using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NoteApp.Core;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop.Training;

/// <summary>
/// Sætningerne fra én oplæsning: hvad appen hørte, hvad der stod, og de tre
/// ting man kan gøre ved det.
///
/// HVORFOR ET VINDUE OG IKKE EN LISTE PÅ SIDEN
///
/// Listen kan være hundrede sætninger lang. Lå den under boksene på «Start
/// her», fyldte den hele skærmen og skubbede alt andet væk — man kunne ikke se
/// de tre tekster, mens man arbejdede med den ene. Her er den et sted, man går
/// ind i og ud af igen.
///
/// LUKKEKNAPPEN SCROLLER IKKE MED
///
/// Toppen og bunden ligger uden for rullelisten. En liste på hundrede
/// sætninger, hvor man skal rulle tilbage til toppen for at komme ud, er en
/// fælde — og Esc lukker også.
/// </summary>
public partial class TrainingWindow : Window
{
    private readonly Udsnitsafspiller _afspiller = new();
    private Button? _spillerNu;

    private readonly string _mappe;
    private readonly string _titel;
    private readonly Traeningsmaaling _maaling;
    private Dictionary<int, Genlaesning> _genlaest;

    /// <summary>
    /// De fejl, der ALLEREDE er en regel for — slået op i ordbogen, ikke
    /// husket fra dette vindue.
    ///
    /// Uden opslaget så en rettelse, man lavede i går, ulært ud i dag: reglen
    /// lå i databasen og virkede, men knappen stod klar til at lære den igen.
    /// Så kunne man ikke se, hvad man manglede, og det er hele grunden til, at
    /// listen findes.
    /// </summary>
    private readonly HashSet<string> _laerte;

    /// <summary>
    /// Fejl, der er set og lukket, uden at der blev rettet noget. Se
    /// <see cref="Afklaret"/> — det er den eneste udgang for de tre fjerdedele
    /// af fejlene, der er almindelige ord.
    /// </summary>
    private readonly HashSet<string> _afklarede;

    /// <summary>Knapper og afvigelse hører sammen, når en rettelse skal gemmes.</summary>
    private sealed record Retning(Traeningssaetning Saetning, Afvigelse Afvigelse);

    /// <summary>Blev der lært en rettelse eller læst en sætning om? Så skal værten opdatere sig.</summary>
    public bool NogetAendret { get; private set; }

    public TrainingWindow(string mappe, string titel, Traeningsmaaling maaling)
    {
        InitializeComponent();

        _mappe = mappe;
        _titel = titel;
        _maaling = maaling;
        _genlaest = Genlaesninger.Laes(mappe);
        _laerte = LaesLaerte();
        _afklarede = Afklaret.Laes(mappe);

        _afspiller.Faerdig += () => Dispatcher.Invoke(NulstilAfspilKnap);

        Overskrift.Text = titel;
        Underskrift.Text = $"{maaling.Ord} ord · {maaling.Saetninger.Count} sætninger";

        TalForkerte.Text = $"{maaling.Forkerte}";
        TalProcent.Text = $"{maaling.Procent:0.0} %";
        TalProcent.Foreground = (Brush)FindResource(Traeningsmaaling.Farve(maaling.Procent));

        VisSaetninger();
        VisBund();
    }

    protected override void OnClosed(EventArgs e)
    {
        _afspiller.Dispose();
        base.OnClosed(e);
    }

    private void Luk_Click(object sender, RoutedEventArgs e) => Close();

    private static HashSet<string> LaesLaerte()
    {
        var sæt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var ordbog = new LearningStore();
            foreach (var r in ordbog.ListRettelser()) sæt.Add(r.Normalized);
        }
        catch (Exception)
        {
            // Kan ordbogen ikke laeses, staar alt bare som ulaert. Det er
            // irriterende, ikke forkert — og bedre end at vinduet ikke aabner.
        }

        return sæt;
    }

    /// <summary>Er der allerede en regel for den fejl?</summary>
    private bool ErLaert(Afvigelse a) => _laerte.Contains(LearningStore.Normalize(a.Hørt));

    /// <summary>
    /// De afvigelser i sætningen, en rettelse kan gøre noget ved.
    ///
    /// Et ord, der MANGLER i udskriften, kan ikke rettes med en regel — der er
    /// ikke noget at rette fra. Det samme gælder et ord, der er kommet til.
    /// De tæller derfor ikke med, når det afgøres, om sætningen er færdig.
    /// </summary>
    private static IEnumerable<Afvigelse> KanRettes(Traeningssaetning s) =>
        s.Afvigelser.Where(a => a.Hørt.Length > 0 && a.Forventet.Length > 0);

    /// <summary>
    /// Er der ikke mere at rette i sætningen?
    ///
    /// Kun sandt, når der VAR noget at rette, og alt er lært. En sætning, hvis
    /// eneste fejl er et manglende ord, får ikke flueben — der er intet gjort,
    /// og et grønt flueben ville sige, at den var i orden.
    /// </summary>
    private bool ErAfklaret(Traeningssaetning s, Afvigelse a) =>
        _afklarede.Contains(Afklaret.Noegle(s.Nummer, a.Hørt));

    /// <summary>Der er taget stilling: enten lært en regel, eller afklaret.</summary>
    private bool ErKlaret(Traeningssaetning s, Afvigelse a) => ErLaert(a) || ErAfklaret(s, a);

    private bool ErFaerdig(Traeningssaetning s)
    {
        var kan = KanRettes(s).ToList();
        return kan.Count > 0 && kan.All(a => ErKlaret(s, a));
    }

    /// <summary>
    /// Hvor mange gange ordet står i HELE udskriften, og hvor mange af dem der
    /// var fejl.
    ///
    /// HVORFOR DET SKAL TÆLLES, FØR EN REGEL GEMMES
    ///
    /// En rettelse gælder alle fremtidige møder. Er ordet et almindeligt dansk
    /// ord, rammer reglen hver gang det bliver sagt — også de gange, det var
    /// rigtigt.
    ///
    /// Det skete: fire regler blev lært på én sætning — «at»→«af», «af»→«er»,
    /// «går»→«gået», «kræver»→«krævede». Prøvet af på tre almindelige
    /// sætninger lavede de 15 ændringer, og alle femten var forkerte. Reglerne
    /// kører oven i købet efter hinanden på den samme tekst, så de kan kæde sig
    /// sammen.
    ///
    /// Tællingen er svaret. Står ordet 40 gange og var forkert én gang, ville
    /// reglen ødelægge 39 rigtige. Det er et tal, ikke et skøn — og det kan
    /// stå i advarslen, så valget kan træffes.
    /// </summary>
    private (int Gange, int Fejl) Udbredelse(Afvigelse a)
    {
        var søgt = ReadAloudScore.Ord(a.Hørt);
        if (søgt.Count == 0) return (0, 0);

        var gange = 0;
        var fejl = 0;

        foreach (var s in _maaling.Saetninger)
        {
            var ord = ReadAloudScore.Ord(s.Hørt);

            for (var i = 0; i + søgt.Count <= ord.Count; i++)
            {
                var passer = true;
                for (var j = 0; j < søgt.Count && passer; j++)
                    passer = ord[i + j].Equals(søgt[j], StringComparison.OrdinalIgnoreCase);

                if (passer) gange++;
            }

            fejl += s.Afvigelser.Count(x =>
                x.Hørt.Equals(a.Hørt, StringComparison.OrdinalIgnoreCase));
        }

        return (gange, fejl);
    }

    /// <summary>
    /// Hvert sted i oplæsningen, hvor ordet står — med besked om det gik godt
    /// eller galt lige dér.
    ///
    /// Tallet alene kan ikke bære valget. «43 gange, 3 forkerte» siger, at der
    /// SKAL træffes et valg, men ikke hvad det er: gentager den samme fejl sig
    /// i den samme sammenhæng, er en regel rigtig — er de tre spredt ud mellem
    /// fyrre rigtige, er den forkert. Det kan kun ses ved at læse stederne.
    ///
    /// De forkerte står øverst. Det er dem, valget handler om.
    /// </summary>
    private List<(string Tekst, bool ErFejl)> Forekomster(Afvigelse a)
    {
        var søgt = ReadAloudScore.Ord(a.Hørt);
        var ud = new List<(string, bool, int)>();

        if (søgt.Count == 0) return new List<(string, bool)>();

        foreach (var s in _maaling.Saetninger)
        {
            var ord = ReadAloudScore.Ord(s.Hørt);

            var staarHer = false;
            for (var i = 0; i + søgt.Count <= ord.Count && !staarHer; i++)
            {
                var passer = true;
                for (var j = 0; j < søgt.Count && passer; j++)
                    passer = ord[i + j].Equals(søgt[j], StringComparison.OrdinalIgnoreCase);

                staarHer = passer;
            }

            if (!staarHer) continue;

            var erFejl = s.Afvigelser.Any(x =>
                x.Hørt.Equals(a.Hørt, StringComparison.OrdinalIgnoreCase));

            ud.Add(($"{s.Fra:mm\\:ss}  {s.Hørt.Trim()}", erFejl, s.Nummer));
        }

        return ud
            .OrderByDescending(x => x.Item2)
            .ThenBy(x => x.Item3)
            .Select(x => (x.Item1, x.Item2))
            .ToList();
    }

    // -------------------------------------------------------------- sætninger

    private void KunFejl_Klik(object sender, RoutedEventArgs e) => VisSaetninger();

    private void VisSaetninger()
    {
        var kunFejl = KunFejl.IsChecked == true;
        var vises = _maaling.Saetninger.Where(s => !kunFejl || s.Fejler).ToList();

        ListeTekst.Text = kunFejl
            ? $"{vises.Count} sætninger med mindst én fejl. Gult er det, appen hørte anderledes end teksten."
            : $"Alle {vises.Count} sætninger. Gult er det, appen hørte anderledes end teksten.";

        Liste.Items.Clear();
        foreach (var s in vises) Liste.Items.Add(Kort(s));
    }

    /// <summary>
    /// Linjen nederst: hvad genindtalingerne har vist. Den står FOR SIG og
    /// lægges ikke til karakteren — talte de med, kunne man læse den samme
    /// sætning femten gange og se tallet stige, uden at appen var blevet bedre.
    /// </summary>
    private void VisBund()
    {
        if (_genlaest.Count == 0)
        {
            Bund.Text = "Hør en sætning for at afgøre, om appen hørte forkert, eller om ordet blev sagt utydeligt.";
            return;
        }

        var rene = _genlaest.Values.Count(g => g.Rent);

        Bund.Text =
            $"{_genlaest.Count} sætning{(_genlaest.Count == 1 ? "" : "er")} læst op igen · " +
            $"{rene} ramt helt anden gang · {_genlaest.Count - rene} hørt forkert igen. " +
            "Genindtalinger tæller ikke med i procenten.";
    }

    /// <summary>Ét kort pr. sætning: hvad appen hørte, hvad der stod, og handlingerne.</summary>
    private Border Kort(Traeningssaetning s)
    {
        var indhold = new StackPanel();

        var top = new Grid { Margin = new Thickness(0, 0, 0, 9) };

        var venstre = new StackPanel { Orientation = Orientation.Horizontal };
        venstre.Children.Add(new TextBlock
        {
            Text = $"{s.Fra:mm\\:ss}",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TekstMeget")
        });

        var faerdig = ErFaerdig(s);

        if (s.Fejler)
        {
            venstre.Children.Add(new TextBlock
            {
                Text = s.Afvigelser.Count == 1 ? "1 fejl" : $"{s.Afvigelser.Count} fejl",
                FontSize = 12,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource(faerdig ? "TekstMeget" : "Advarsel")
            });
        }

        // GRØNT FLUEBEN, når der ikke er mere at rette. Uden det ser en
        // sætning, man er færdig med, præcis ud som en, man ikke har rørt — og
        // så skal man læse hele kortet igennem for at finde ud af hvilken af
        // delene det er.
        if (faerdig)
        {
            venstre.Children.Add(new TextBlock
            {
                Text = "✓ rettet",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("Godkendt")
            });
        }

        top.Children.Add(venstre);

        var afspil = new Button
        {
            Content = "▶  Hør sætningen",
            Padding = new Thickness(12, 5, 12, 5),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            Tag = s
        };
        afspil.Click += Afspil_Click;
        top.Children.Add(afspil);

        indhold.Children.Add(top);

        indhold.Children.Add(Etiket("Appen hørte"));
        indhold.Children.Add(Markeret(s));

        if (s.Fejler)
        {
            indhold.Children.Add(Etiket("Der stod", 10));
            indhold.Children.Add(new TextBlock
            {
                Text = s.Forventet,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21,
                Foreground = (Brush)FindResource("TekstSvag")
            });

            indhold.Children.Add(Handlinger(s));
        }

        if (_genlaest.TryGetValue(s.Nummer, out var g)) indhold.Children.Add(Genlaest(g));

        return new Border
        {
            Background = (Brush)new BrushConverter().ConvertFrom(faerdig ? "#FF1B2A24" : "#FF1D2530")!,
            BorderBrush = (Brush)FindResource(faerdig ? "Godkendt" : "PanelKant"),
            BorderThickness = new Thickness(faerdig ? 1.5 : 1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16, 13, 16, 13),
            Margin = new Thickness(0, 0, 0, 9),
            Tag = s.Nummer,
            Child = indhold
        };
    }

    private TextBlock Etiket(string tekst, double top = 0) => new()
    {
        Text = tekst,
        FontSize = 10.5,
        Margin = new Thickness(0, top, 0, 3),
        Foreground = (Brush)FindResource("TekstMeget")
    };

    /// <summary>
    /// Sætningen med GULT på de ord, der ikke passer. Markeringen sker på
    /// selve ordene: det er forskellen på at kunne se fejlen og at skulle lede
    /// efter den i en sætning på tyve ord.
    /// </summary>
    private TextBlock Markeret(Traeningssaetning s)
    {
        var blok = new TextBlock
        {
            FontSize = 13.5,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Foreground = (Brush)FindResource("Tekst")
        };

        // Sammenligningen sker paa den normaliserede form — ellers ville
        // «Nordby-projektet,» med komma ikke matche «nordby projektet».
        var gale = new HashSet<string>(
            s.Afvigelser.SelectMany(a => ReadAloudScore.Ord(a.Hørt)),
            StringComparer.OrdinalIgnoreCase);

        if (gale.Count == 0) { blok.Text = s.Hørt; return blok; }

        var gul = (Brush)new BrushConverter().ConvertFrom("#66F0B23C")!;

        foreach (var stykke in s.Hørt.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var nøgen = ReadAloudScore.Ord(stykke);
            var ramt = nøgen.Count > 0 && nøgen.Any(gale.Contains);

            blok.Inlines.Add(ramt
                ? new Run(stykke) { Background = gul, FontWeight = FontWeights.SemiBold }
                : new Run(stykke));

            blok.Inlines.Add(new Run(" "));
        }

        return blok;
    }

    /// <summary>
    /// De to veje videre. Valget mellem dem er hele pointen: retter man et
    /// ord, der blev sagt utydeligt, lærer appen en regel, der rammer forkert
    /// på andre møder.
    /// </summary>
    private UIElement Handlinger(Traeningssaetning s)
    {
        var raekke = new WrapPanel { Margin = new Thickness(0, 11, 0, 0) };

        foreach (var a in KanRettes(s))
        {
            var laert = ErLaert(a);
            var afklaret = ErAfklaret(s, a);

            var knap = new Button
            {
                Content = laert ? $"✓ Lært: «{Kort(a.Forventet)}»"
                        : afklaret ? $"✓ Afklaret: «{Kort(a.Hørt)}»"
                        : $"Ret «{Kort(a.Hørt)}» → «{Kort(a.Forventet)}»",
                Padding = new Thickness(11, 5, 11, 5),
                FontSize = 12,
                Margin = new Thickness(0, 0, 7, 7),
                IsEnabled = !laert && !afklaret,
                Tag = new Retning(s, a)
            };

            // Ordet, reglen ville ramme, staar HER — ikke foerst naar man har
            // trykket. En knap, der ser harmloes ud, men rammer fyrre steder,
            // er ikke et valg, man traeffer; det er et, man snubler i.
            if (!laert && !afklaret)
            {
                var (gange, fejl) = Udbredelse(a);
                if (gange > fejl && gange > 1)
                {
                    knap.ToolTip =
                        $"«{a.Hørt}» står {gange} gange i denne oplæsning, og kun {fejl} af dem var forkert. " +
                        "En regel ville rette dem alle sammen — også de rigtige.";
                    knap.Foreground = (Brush)FindResource("Advarsel");
                }
            }

            knap.Click += Ret_Click;
            raekke.Children.Add(knap);
        }

        var igen = new Button
        {
            Content = "Læs sætningen op igen",
            Padding = new Thickness(11, 5, 11, 5),
            FontSize = 12,
            Margin = new Thickness(0, 0, 7, 7),
            Tag = s
        };
        igen.Click += LaesIgen_Click;
        raekke.Children.Add(igen);

        return raekke;
    }

    /// <summary>
    /// Hvad genindtalingen viste — og hvad man skal gøre ved det. Svaret er
    /// ikke et tal, det er en konklusion: «8 af 9 ord» siger ingenting i sig
    /// selv, «det var oplæsningen, ikke appen» siger, hvilket håndtag der virker.
    /// </summary>
    private UIElement Genlaest(Genlaesning g)
    {
        var farve = (Brush)FindResource(g.Rent ? "Godkendt" : "Advarsel");
        var indhold = new StackPanel();

        indhold.Children.Add(new TextBlock
        {
            Text = g.Rent
                ? $"Læst op igen {g.Tidspunkt:d/M HH:mm} — alle {g.Ord} ord ramt"
                : $"Læst op igen {g.Tidspunkt:d/M HH:mm} — {g.Ramt} af {g.Ord} ord ramt",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = farve
        });

        indhold.Children.Add(new TextBlock
        {
            Text = g.Rent
                ? "Appen KAN høre sætningen rigtigt. Første gang var det oplæsningen, der var utydelig — der er ikke noget at rette."
                : "Det blev hørt forkert igen, selvom du læste det op med vilje. Så er det appen, der ikke kan høre ordet — og dér hjælper en rettelse.",
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            Foreground = (Brush)FindResource("TekstSvag")
        });

        return new Border
        {
            Background = (Brush)new BrushConverter().ConvertFrom(g.Rent ? "#224CBE72" : "#22F0B23C")!,
            BorderBrush = farve,
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(13, 10, 13, 10),
            Margin = new Thickness(0, 11, 0, 0),
            Child = indhold
        };
    }

    private static string Kort(string t) => t.Length <= 24 ? t : t[..22] + "…";

    // ------------------------------------------------------------ afspilning

    private void Afspil_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button knap || knap.Tag is not Traeningssaetning s) return;

        // Et tryk paa den, der allerede spiller, stopper den. Ellers skulle man
        // vente saetningen ud for at faa ro.
        if (_spillerNu == knap) { _afspiller.Stop(); return; }

        var lyd = _maaling.LydFil;

        if (lyd.Length == 0 || !File.Exists(lyd))
        {
            Dialogs.AppDialog.Vis(this, "Ingen lyd",
                "Lydfilen til den her optagelse findes ikke længere, så sætningen kan ikke spilles.",
                Dialogs.Slags.Pas_paa);
            return;
        }

        try
        {
            _afspiller.Spil(lyd, s.Fra, s.Til);
            NulstilAfspilKnap();
            _spillerNu = knap;
            knap.Content = "■  Stop";
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(this, "Kunne ikke afspille", ex.Message, Dialogs.Slags.Fejl);
        }
    }

    private void NulstilAfspilKnap()
    {
        if (_spillerNu is not null) _spillerNu.Content = "▶  Hør sætningen";
        _spillerNu = null;
    }

    // -------------------------------------------------------------- rettelser

    /// <summary>
    /// Lærer appen, at det hørte ord skal være det, der stod i teksten.
    /// Rettelsen slår igennem på alle senere udskrifter — også på rigtige møder.
    /// </summary>
    private void Ret_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button knap || knap.Tag is not Retning r) return;

        var (gange, fejl) = Udbredelse(r.Afvigelse);

        // SPÆRREN. Rammer reglen ord, der var RIGTIGE, siges det med tal — og
        // der tilbydes en vej ud, der ikke ødelægger noget.
        //
        // Uden den blev der lært fire regler på én sætning — «at»→«af»,
        // «af»→«er», «går»→«gået», «kræver»→«krævede». Prøvet af på tre
        // almindelige sætninger lavede de 15 ændringer, alle forkerte. Og de
        // kører efter hinanden på samme tekst, så de kæder sig sammen.
        //
        // «Afklar» er den rigtige udgang for de fleste af dem: fejlen er set,
        // der er ikke noget at gøre, og sætningen kan lukkes. Den er derfor
        // knappen med fokus.
        if (gange > fejl && gange > 1)
        {
            var rigtige = gange - fejl;

            var valg = Dialogs.AppDialog.SpoergTre(this,
                $"«{r.Afvigelse.Hørt}» er et almindeligt ord",
                $"Ordet står {gange} gange i denne oplæsning, og kun {fejl} af dem var hørt forkert. " +
                $"De øvrige {rigtige} var rigtige.\n\n" +
                "En rettelse er en regel, der gælder alle fremtidige møder, og den skelner ikke: " +
                $"den ville også lave de {rigtige} rigtige om — og hver gang ordet bliver sagt fremover.\n\n" +
                "Rettelser hører til navne og fagord, som «cpr-nummer» eller «SCIM». Her er der " +
                "ikke noget at rette; det er ét sted, appen hørte forkert. Du kan afklare det, " +
                "så sætningen er færdigbehandlet.",
                godkend: "Gem reglen alligevel",
                tredje: "OK, jeg accepterer fejlen",
                slags: Dialogs.Slags.Pas_paa,
                grundlag: Forekomster(r.Afvigelse),
                grundlagKnap: $"Vis alle {gange} steder");

            // Afklaret: fejlen er set og lukket. Der aendres ingenting.
            if (valg == 1)
            {
                Afklaret.Gem(_mappe, r.Saetning.Nummer, r.Afvigelse.Hørt);
                _afklarede.Add(Afklaret.Noegle(r.Saetning.Nummer, r.Afvigelse.Hørt));
                NogetAendret = true;
                ErstatKort(r.Saetning);
                return;
            }

            if (valg != 0) return;
        }

        var vindue = new CorrectionWindow(r.Afvigelse.Hørt, fejl, r.Afvigelse.Forventet) { Owner = this };
        if (vindue.ShowDialog() != true) return;

        try
        {
            using var ordbog = new LearningStore();
            ordbog.LearnCorrection(vindue.Hørt, vindue.Rigtigt,
                engineId: "manuel", meetingId: Path.GetFileName(_mappe));

            Historik.Skriv(HaendelseType.Rettelser,
                $"Rettelse lært: {vindue.Hørt} → {vindue.Rigtigt}",
                $"Fra oplæsning: «{_titel}»", sti: _mappe);

            _laerte.Add(LearningStore.Normalize(vindue.Hørt));
            NogetAendret = true;

            // Kortet bygges om, saa fluebenet kommer, naar den sidste rettelse
            // i saetningen er paa plads.
            ErstatKort(r.Saetning);
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(this, "Rettelsen kunne ikke gemmes", ex.Message, Dialogs.Slags.Fejl);
        }
    }

    /// <summary>
    /// Bygger ét kort om på sin egen plads i listen. Bygges hele listen om,
    /// hopper den til toppen, og så skal man lede efter det, man stod ved.
    /// </summary>
    private void ErstatKort(Traeningssaetning s)
    {
        var kort = Liste.Items.Cast<UIElement>()
            .FirstOrDefault(k => k is FrameworkElement f && (int?)f.Tag == s.Nummer);

        var plads = kort is null ? -1 : Liste.Items.IndexOf(kort);
        if (plads < 0) return;

        Liste.Items.RemoveAt(plads);
        Liste.Items.Insert(plads, Kort(s));
    }

    /// <summary>
    /// Læser sætningen op igen — hele vejen, uden mellemstationer.
    ///
    /// Sætningen står på skærmen med det samme, man læser den op, trykker
    /// «Gem», og er tilbage på det kort, man trykkede fra, nu med svaret på.
    /// Kortet skiftes ud PÅ PLADS: en liste, der hopper til toppen, sender en
    /// tilbage til at lede efter det, man lige stod ved.
    /// </summary>
    private void LaesIgen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button knap || knap.Tag is not Traeningssaetning s) return;

        _afspiller.Stop();

        var vindue = new ReadAgainWindow(_mappe, s.Nummer, s.Forventet) { Owner = this };
        if (vindue.ShowDialog() != true || vindue.Resultat is null) return;

        _genlaest[s.Nummer] = vindue.Resultat;
        NogetAendret = true;

        ErstatKort(s);
        VisBund();
    }
}
