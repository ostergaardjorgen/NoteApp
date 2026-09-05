using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Diktering;

/// <summary>
/// De dikteringer, brugeren har valgt at gemme.
/// </summary>
/// <remarks>
/// FANEN STÅR FØRST, fordi det er den, man kommer for. De tre andre er
/// opsætning — noget man gør én gang og sjældent igen.
/// </remarks>
public partial class NoterView : UserControl
{
    /// <summary>Kaldes, når der er kommet eller forsvundet en note.</summary>
    public static Action? Aendret;

    private sealed record Visning(
        string Naar, string Tekst, string Form, Diktatnote Note, Visibility KanSkifte);

    /// <summary>Formen som et kort navn, saa man kan se, hvad noten er nu.</summary>
    private static string Form(string? formaal) => formaal switch
    {
        "Mail" => Sprog.T("noter.type_mail"),
        "Prompt" => Sprog.T("noter.type_prompt"),
        "Opgave" => Sprog.T("noter.type_opgave"),
        _ => Sprog.T("noter.type_note"),
    };

    public NoterView()
    {
        InitializeComponent();

        // Samme regel som i proeverummet: det afgoeres af, om fanen kan SES.
        // «Loaded» daekker ogsaa en fane, man er klikket vaek fra.
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) { Aendret = null; return; }

            Vis();

            // Gemmes en note fra baandet, mens fanen er fremme, skal den dukke
            // op uden at man skal klikke rundt for at faa den frem.
            Aendret = () => Dispatcher.BeginInvoke(new Action(Vis));
        };

        Loaded += (_, _) => Vis();
        Unloaded += (_, _) => Aendret = null;

        // KLIKKET HOERER TIL DEN SKAERM, MAN STAAR PAA. Bliver det staaende,
        // naar man klikker vaek, ville et klik i en anden note aabne ruden
        // paa en skaerm, ingen kigger paa.
        IsVisibleChanged += (_, _) =>
            Ordklik.Klikket = IsVisible ? Ordvalgt : null;
    }

    /// <summary>Én periode i spalten til venstre.</summary>
    public sealed class Periodevalg
    {
        public required string Navn { get; init; }
        public required int Dage { get; init; }
        public int Antal { get; set; }
        public bool Valgt { get; set; }

        /// <summary>En tom periode kan ikke vælges.</summary>
        public bool Kan => Antal > 0;
    }

    /// <summary>
    /// Perioderne, man kan vælge imellem.
    /// </summary>
    /// <remarks>
    /// DATOER OG IKKE MAPPER. En diktering laves i forbifarten, og man leder
    /// efter den på «hvornår sagde jeg det» — ikke på «hvor lagde jeg den».
    /// Der er ingen mapper at oprette og ingen at holde styr på.
    ///
    /// Tallet er DAGE TILBAGE og ikke kalendergrænser: «denne uge» er de
    /// sidste syv dage og ikke «siden mandag». En note fra i søndags er
    /// stadig fra denne uge om mandagen — kalenderugen ville smide den i
    /// «denne måned», og så leder man forgæves.
    /// </remarks>
    private static IEnumerable<Periodevalg> Skabeloner() => new[]
    {
        new Periodevalg { Navn = Sprog.T("noter.i_dag"), Dage = 1 },
        new Periodevalg { Navn = Sprog.T("noter.denne_uge"), Dage = 7 },
        new Periodevalg { Navn = Sprog.T("noter.denne_maaned"), Dage = 31 },
        new Periodevalg { Navn = Sprog.T("noter.dette_aar"), Dage = 366 },
        new Periodevalg { Navn = Sprog.T("noter.alle"), Dage = int.MaxValue },
    };

    /// <summary>Den valgte periode i dage. Huskes mellem to tegninger.</summary>
    private int _dage = 1;

    private void Vis()
    {
        var noter = Diktatnoter.Laes();
        var nu = DateTime.Now;

        // ============ PERIODERNE FOERST ============
        //
        // Tallene skal staa, ogsaa naar den valgte periode er tom - ellers
        // kan man ikke se, at der ER noget under en af de andre.
        var perioder = Skabeloner().ToList();

        foreach (var p in perioder)
            p.Antal = noter.Count(n => Indenfor(n.Tid, nu, p.Dage));

        // ER DEN VALGTE PERIODE TOM, SKIFTES DER TIL DEN FOERSTE, DER IKKE ER.
        // Ellers stod man paa «I dag» med en tom liste og en fyldt maaned ved
        // siden af - og skulle selv gaette, hvor noterne var.
        if (!perioder.Any(p => p.Dage == _dage && p.Antal > 0))
            _dage = perioder.FirstOrDefault(p => p.Antal > 0)?.Dage ?? 1;

        foreach (var p in perioder) p.Valgt = p.Dage == _dage;

        Perioder.ItemsSource = perioder;

        var vist = noter.Where(n => Indenfor(n.Tid, nu, _dage)).ToList();

        Liste.ItemsSource = vist
            .Select(n => new Visning(
                Naar(n.Tid), n.Tekst, Form(n.Formaal), n,
                n.KanSkiftes ? Visibility.Visible : Visibility.Collapsed))
            .ToList();

        Tom.Visibility = vist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RydKnap.Visibility = noter.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        Antal.Text = vist.Count switch
        {
            0 => "",
            1 => "1 note",
            _ => $"{vist.Count} noter",
        };
    }

    /// <summary>
    /// Ligger noten inden for de sidste <paramref name="dage"/> døgn?
    /// </summary>
    /// <remarks>
    /// «I dag» er dagens dato og ikke de sidste fireogtyve timer. Klokken ni
    /// om morgenen er en note fra i går aftes ikke fra i dag, uanset at der er
    /// gået mindre end et døgn.
    /// </remarks>
    private static bool Indenfor(DateTime tid, DateTime nu, int dage) =>
        dage == int.MaxValue || (nu.Date - tid.Date).Days < dage;

    private void Periode_Valgt(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Periodevalg p) return;
        if (p.Dage == _dage) return;

        _dage = p.Dage;
        Vis();
    }

    /// <summary>
    /// Tidspunktet, som man tænker på det.
    /// </summary>
    /// <remarks>
    /// «I dag 14:32» siger mere end en dato, når noten er fra for en time
    /// siden — og det er de fleste af dem.
    /// </remarks>
    private static string Naar(DateTime t)
    {
        var d = DateTime.Now.Date - t.Date;

        return d.Days switch
        {
            0 => $"I dag {t:HH:mm}",
            1 => $"I går {t:HH:mm}",
            _ => t.ToString("d. MMMM HH:mm"),
        };
    }

    /// <summary>
    /// Laver en aftale ud af det, der lige er sagt.
    /// </summary>
    /// <remarks>
    /// DET ER DÉT, DER GØR EN DIKTERING FÆRDIG I APPEN. En note er ellers et
    /// stykke tekst, man selv skal gøre noget ved — kopiere et sted hen og
    /// huske hvorhen. To af de steder kender appen i forvejen, og så skal den
    /// også kunne lave dem.
    ///
    /// Vinduet åbnes med teksten som titel, og resten — tidspunkt, sted,
    /// mødetype — sættes dér. Talen siger sjældent klokkeslættet i en form,
    /// en maskine kan stole på, og en aftale på et gættet tidspunkt er værre
    /// end ingen aftale.
    /// </remarks>
    private void TilAftale_Klik(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Visning v) return;

        var aftale = new Aftale { Titel = Opgave.Kort(v.Tekst, 120) };

        var vindue = new Meeting.AftaleWindow(aftale) { Owner = Window.GetWindow(this) };
        if (vindue.ShowDialog() != true) return;

        try
        {
            Kalender.Gem(vindue.Aftalen);
            Antal.Text = Sprog.T("noter.aftale_lavet");
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Aftalen blev ikke gemt",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    /// <summary>
    /// Laver en opgave ud af det, der lige er sagt.
    /// </summary>
    /// <remarks>
    /// HELE TEKSTEN KOMMER MED, ikke kun en overskrift. Opgavevinduet udleder
    /// selv det korte navn af teksten — se <see cref="Opgave.Visningsnavn"/> —
    /// så «ring til Paludan om budgettet inden fredag» bliver til et navn, man
    /// kan skimme, og en beskrivelse, man kan læse.
    /// </remarks>
    private void TilOpgave_Klik(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Visning v) return;

        var vindue = Search.OpgaveWindow.Ny(v.Tekst);
        vindue.Owner = Window.GetWindow(this);

        if (vindue.ShowDialog() == true) Antal.Text = Sprog.T("noter.opgave_lavet");
    }

    private void Kopier_Klik(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Visning v) return;

        try
        {
            Clipboard.SetText(v.Tekst);
            Antal.Text = "Kopieret — sæt ind med Ctrl+V eller Shift+Insert";
        }
        catch (Exception)
        {
            // Udklipsholderen kan vaere laast af et andet program et oejeblik.
            Antal.Text = "Kunne ikke kopiere lige nu — prøv igen";
        }
    }

    /// <summary>
    /// Skriver noten om til en anden teksttype.
    /// </summary>
    /// <remarks>
    /// DER SKRIVES OM FRA DEN RÅ UDSKRIFT, ikke fra den tekst, der står på
    /// skærmen. Pudsningen har allerede kastet fyldordene væk og valgt en
    /// form; en omskrivning af en omskrivning driver længere og længere væk
    /// fra det, der faktisk blev sagt.
    ///
    /// Noter fra før den rå udskrift blev gemt med, kan ikke skiftes. Så står
    /// knapperne der ikke — frem for at stå der og ikke virke.
    /// </remarks>
    private async void Skift_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button knap) return;
        if (knap.Tag is not Visning v) return;
        if (!v.Note.KanSkiftes) { Antal.Text = Sprog.T("noter.skift_kan_ikke"); return; }

        if (!Enum.TryParse<Core.Llm.Dikteringsformaal>((string)knap.CommandParameter, out var formaal)) return;

        var noegle = Core.Llm.SkyNoegle.Hent();
        if (noegle is null)
        {
            Antal.Text = Sprog.T("settingsview.diktering_kraever_noegle");
            return;
        }

        Antal.Text = Sprog.T("noter.skifter");
        IsEnabled = false;

        try
        {
            var klient = new Core.Llm.Dikteringsklient(noegle);
            var ny = await klient.PudsAsync(v.Note.Raa, formaal);

            if (ny.Trim().Length == 0) { Antal.Text = Sprog.T("diktering.intet_hoert"); return; }

            Diktatnoter.Erstat(v.Note, v.Note with { Tekst = ny.Trim(), Formaal = formaal.ToString() });
            Vis();
        }
        catch (Exception ex)
        {
            Antal.Text = Sprog.T("diktering.gik_galt", ex.Message);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void Slet_Klik(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Visning v) return;

        Diktatnoter.Slet(v.Note);
        Vis();
    }

    private void Ryd_Klik(object sender, RoutedEventArgs e)
    {
        var svar = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            "Slet alle noter?",
            "Alle gemte dikteringer fjernes. Det kan ikke fortrydes.",
            "Slet alle", "Behold", Dialogs.Slags.Pas_paa);

        if (!svar) return;

        Diktatnoter.Ryd();
        Vis();
    }

    // ==================== RET ET ORD ====================

    /// <summary>Det ord, der er klikket paa.</summary>
    private string? _retter;

    /// <summary>
    /// Åbner rettelsen for det ord, der blev klikket på.
    /// </summary>
    /// <remarks>
    /// MAN OPDAGER FEJLEN HER. Et forkert hørt ord ses dér, hvor det står — i
    /// noten, lige efter man har dikteret. Skal man huske stavemåden, gå til
    /// ordbogen og skrive den ind, bliver det ikke gjort.
    ///
    /// Brugerens ord 31-08-2026: «jeg burde kunne klikke på Storistech og
    /// tilføje det til StorageTek».
    /// </remarks>
    private void Ordvalgt(string ord)
    {
        _retter = ord;

        Rettitel.Text = Sprog.T("searchview.ret_titel", ord);
        Retsvar.Text = "";
        Retfelt.Text = "";
        Retrude.Visibility = Visibility.Visible;

        Retfelt.Focus();
    }

    private void Retfelt_Tast(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;

        GemRettelsen();
        e.Handled = true;
    }

    private void RetGem_Klik(object sender, RoutedEventArgs e) => GemRettelsen();

    private void RetLuk_Klik(object sender, RoutedEventArgs e)
    {
        Retrude.Visibility = Visibility.Collapsed;
        _retter = null;
    }

    /// <summary>
    /// Skriver rettelsen i ordbogen.
    /// </summary>
    /// <remarks>
    /// TO TING SKER PÅ ÉN GANG. Det rigtige ord lægges i ordbogen, hvis det
    /// ikke står der, og det forkerte lægges som alias. Uden det første ville
    /// aliasset pege på et ord, der ikke findes; uden det andet ville den
    /// samme fejl komme igen i morgen.
    ///
    /// NOTEN SELV RETTES IKKE. Den er en gengivelse af det, der blev sagt,
    /// og den skal blive ved at være dét. Rettelsen gælder fremover — det er
    /// ordbogen, der lærer noget, ikke historikken, der bliver lavet om.
    /// </remarks>
    private void GemRettelsen()
    {
        if (_retter is not { } forkert) return;

        var rigtigt = (Retfelt.Text ?? "").Trim();
        if (rigtigt.Length == 0) return;

        try
        {
            Ordbibliotek.Tilfoej(rigtigt);

            Retsvar.Text = Ordbibliotek.TilfoejAlias(rigtigt, forkert)
                ? Sprog.T("searchview.ret_lagt", forkert, rigtigt)
                : Sprog.T("searchview.ret_kan_ikke", forkert);
        }
        catch (Exception ex)
        {
            Retsvar.Text = Sprog.T("diktering.gik_galt", ex.Message);
        }
    }
}
