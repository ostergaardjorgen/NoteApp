using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.History;

/// <summary>
/// Navnene på kilderne, slået op ÉN gang pr. tegning.
/// </summary>
/// <remarks>
/// HER LAA ET DISKOPSLAG PR. LINJE.
///
/// <c>SlaaOp</c> kaldte <c>DocumentStore.LoadAll()</c> — som læser hver eneste
/// dokument-JSON — og <c>MeetingStore.FindById</c>, som gennemgår hver eneste
/// mødemappe. Én gang pr. historikpost, op til to gange for dokumentlinjer.
///
/// Med de 500, der blev læst, mærkede man det ikke. Med hele historikken tog
/// en omtegning 331 ms på 2.054 poster — og en omtegning sker ved hvert
/// tastetryk i søgefeltet. Nu læses de to lister én gang og slås op på et id.
/// </remarks>
public sealed class Kildenavne
{
    private readonly Dictionary<string, string> _dokumenter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _optagelser = new(StringComparer.OrdinalIgnoreCase);

    public Kildenavne()
    {
        try
        {
            foreach (var d in NoteApp.Core.Documents.DocumentStore.LoadAll())
                _dokumenter[d.Id] = d.Title;
        }
        catch (Exception) { /* kan listen ikke laeses, staar linjerne uden link */ }

        try
        {
            foreach (var m in MeetingStore.Alle())
                _optagelser[m.Id.ToString()] = m.Title;
        }
        catch (Exception) { }
    }

    public string? Dokument(string id) => _dokumenter.GetValueOrDefault(id);
    public string? Optagelse(string id) => _optagelser.GetValueOrDefault(id);
}

/// <summary>En post, som listen kan vise.</summary>
public sealed class PostVisning
{
    public PostVisning(Haendelse a, Kildenavne navne, bool visMaskine = false)
    {
        Klokkeslet = a.Tid.ToString("HH:mm:ss");
        Dato = a.Tid.ToString("d. MMM yyyy");
        Hvad = a.Hvad;

        // ============ MASKINEN VISES KUN, NAAR DER ER FLERE ============
        //
        // Paa een computer er «paa Stationaer» stoej paa hver eneste linje.
        // Staar der arbejde fra to i loggen, er det derimod det foerste, man
        // spoerger om - ogsaa om det, der blev sendt til Mistral. Se
        // Haendelse.Maskine.
        var dele = new List<string>();

        if (a.Model.Length > 0) dele.Add($"Model: {a.Model}");
        if (visMaskine && a.Maskine.Length > 0) dele.Add($"på {a.Maskine}");

        Model = string.Join(" · ", dele);

        var varighed = a.Sekunder > 0
            ? $" · tog {TimeSpan.FromSeconds(a.Sekunder):mm\\:ss}"
            : "";
        Detaljer = a.Detaljer + varighed;

        (UdfaldTekst, Kantfarve) = a.Udfald switch
        {
            Udfald.Fuldført => ("✓ fuldført", Temaskift.Pensel("Godkendt")),
            Udfald.SeEfter => ("! se efter", Temaskift.Pensel("Advarsel")),
            Udfald.Afbrudt => ("— afbrudt", Temaskift.Pensel("Slukket")),
            _ => ("✕ fejlet", Temaskift.Pensel("FejlTekst"))
        };

        Slags = a.Slags;
        Kilde = a.Kilde;

        // NAVNET SLAAS OP NU, IKKE DA LINJEN BLEV SKREVET.
        //
        // Overskriften baerer det navn, tingen havde dengang. Omdoebes den,
        // peger linjen paa noget, der ikke findes - og saa leder man efter en
        // fil, der ligger lige der, under et andet navn.
        //
        // Id'et aendrer sig aldrig. Findes kilden, staar dens NUVAERENDE navn
        // som et link. Findes den ikke, staar der, at den er slettet - og det
        // er ogsaa et svar: linjen bliver staaende som det eneste spor af, at
        // noget fandtes.
        (LinkTekst, Findes) = SlaaOp(a, navne);

        HarLink = LinkTekst.Length > 0;
        LinkFarve = Findes
            ? Temaskift.Pensel("Accent")
            : Temaskift.Pensel("Slukket");
    }

    /// <summary>
    /// Finder kilden frem på id'et og giver den tekst, linjen skal vise.
    ///
    /// Returnerer tom tekst for hændelser uden en kilde at gå til — en
    /// hentning, en backup, et manglende opsætningstrin.
    /// </summary>
    private static (string Tekst, bool Findes) SlaaOp(Haendelse a, Kildenavne navne)
    {
        if (a.Kilde.Length == 0) return ("", false);

        try
        {
            switch (a.Slags)
            {
                case HaendelseType.Dokument:
                case HaendelseType.Slettet when navne.Dokument(a.Kilde) is not null:
                case HaendelseType.Flyttet:
                {
                    var titel = navne.Dokument(a.Kilde);
                    return titel is null
                        ? ("dokumentet er slettet", false)
                        : ($"Vis «{titel}»", true);
                }

                case HaendelseType.Optagelse:
                case HaendelseType.Transskription:
                {
                    var titel = navne.Optagelse(a.Kilde);
                    return titel is null
                        ? ("optagelsen er slettet", false)
                        : ($"Vis «{titel}»", true);
                }

                default:
                    return ("", false);
            }
        }
        catch (Exception)
        {
            // Kan kilden ikke slaas op, er linjen stadig laesbar uden link.
            return ("", false);
        }
    }

    public string Klokkeslet { get; }
    public string Dato { get; }
    public string Hvad { get; }
    public string Detaljer { get; }
    public string Model { get; }
    public string UdfaldTekst { get; }
    public Brush Kantfarve { get; }
    public HaendelseType Slags { get; }

    /// <summary>Id'et på kilden. Tom, hvis der ikke er en at gå til.</summary>
    public string Kilde { get; }

    /// <summary>«Vis «navn»» eller «optagelsen er slettet». Tom uden kilde.</summary>
    public string LinkTekst { get; }

    /// <summary>Findes kilden stadig? Afgør om der kan klikkes.</summary>
    public bool Findes { get; }

    public bool HarLink { get; }
    public Brush LinkFarve { get; }
}

/// <summary>
/// Historikken.
///
/// TO FORMÅL, OG DE ER LIGE VIGTIGE
///
/// Det praktiske: en transskription tager tyve minutter, og var man et andet
/// sted, da den blev færdig, er der ingen kvittering tilbage at kigge på.
/// «Kørte den overhovedet?» skal kunne besvares uden at lede i mapper.
///
/// Det formelle: hele pointen med at køre modellerne lokalt er, at mødet ikke
/// forlader maskinen. Den påstand er kun noget værd, hvis den kan efterprøves.
/// Derfor står tallet for udgående data øverst og som ét tal — et svar, der
/// skal udledes ved at læse en liste igennem, er ikke et svar.
/// </summary>
public partial class HistoryView : UserControl
{
    private IReadOnlyList<Haendelse> _alle = Array.Empty<Haendelse>();

    private static readonly (string Navn, HaendelseType? Slags)[] Filtre =
    {
        ("Alt", null),
        ("Optagelser", HaendelseType.Optagelse),
        ("Transskriptioner", HaendelseType.Transskription),
        ("Dokumenter", HaendelseType.Dokument),
        ("Hentninger", HaendelseType.Hentning),
        ("Sikkerhedskopier", HaendelseType.Backup)
    };

    /// <summary>Fanen, der er valgt. Nummer i <see cref="Filtre"/>.</summary>
    private int _fane;

    /// <summary>Stilen på fanerne — den samme som resten af appen bruger.</summary>
    private readonly System.Windows.Style? _fanestil;

    /// <summary>Sat, mens fanerækken bygges — så et Checked ikke tegner alt om midt i.</summary>
    private bool _bygger;

    public HistoryView()
    {
        InitializeComponent();

        _fanestil = TryFindResource("Fanevalg") as System.Windows.Style
                    ?? Application.Current?.TryFindResource("Fanevalg") as System.Windows.Style;

        Byg_Faner();

        // PERIODERNE ER DE SAMME SOM I COCKPIT, og med vilje: det er det samme
        // spoergsmaal stillet et andet sted. Se SearchView.
        Periodefilter.ItemsSource = new List<Periodepunkt>
        {
            new(Sprog.T("cockpit.heletiden"), null),
            new("I dag", "idag"),
            new("Denne uge", "uge"),
            new("Denne måned", "maaned"),
            new("I år", "aar"),
            new("Fra og til …", "valgt")
        };
        Periodefilter.SelectedIndex = 0;

        Indlæs();
    }

    /// <summary>Et valg i periodelisten. Samme form som Cockpits Filterpunkt.</summary>
    public sealed record Periodepunkt(string Navn, string? Vaerdi);

    /// <summary>Én fane pr. filter — bygget af den samme liste, som filtrerer.</summary>
    private void Byg_Faner()
    {
        _bygger = true;
        Faner.Children.Clear();

        for (var i = 0; i < Filtre.Length; i++)
        {
            var knap = new RadioButton
            {
                GroupName = "historikfane",
                Style = _fanestil,
                Content = Filtre[i].Navn,
                Tag = i,
                Margin = new Thickness(0, 0, 8, 4),
                IsChecked = i == _fane
            };

            knap.Checked += Fane_Valgt;
            Faner.Children.Add(knap);
        }

        _bygger = false;
    }

    private void Fane_Valgt(object sender, RoutedEventArgs e)
    {
        if (_bygger) return;
        if (sender is not RadioButton { Tag: int nr }) return;

        _fane = nr;
        Vis();
    }

    private void Indlæs()
    {
        // ============ HELE HISTORIKKEN, IKKE DE NYESTE 500 ============
        //
        // Laes() tog som standard de nyeste 500. Det gik, da skaermen kun
        // kunne rulle - men nu KAN der soeges og afgraenses paa periode, og en
        // soegning, der kun kigger i de nyeste 500, svarer forkert uden at
        // sige det. «Findes ikke» og «findes, men uden for de 500» er to
        // forskellige svar.
        //
        // Det koster ingenting. Maalt 04-09-2026 paa 2.054 poster: filen er
        // 530 KB (264 byte pr. post), og Laes(alt) tog 9,5 ms - hverken
        // hurtigere eller langsommere end Laes(500), fordi den alligevel
        // laeste og fortolkede hver linje foer den skar fra. 10.000 poster er
        // 2,5 MB og under 50 ms.
        //
        // Det, der IKKE kunne baere det, var listen: se HistoryView.xaml, hvor
        // den nu virtualiserer.
        _alle = Historik.Laes(int.MaxValue);

        // HER BLEV «Intet har forladt denne pc» regnet ud og skrevet.
        //
        // Panelet er fjernet fra skaermen 18-08-2026. Paastanden holdt kun,
        // saa laenge appen ikke havde et netvaerkskald i sig; dokumenter
        // laves nu hos Mistral i Europa, og teksten SENDES.
        //
        // Feltet DataForlodMaskinen staar stadig paa hver post, og listen
        // nedenfor viser det post for post. Det er dét, en revision kan
        // bruge - en overskrift, der opsummerer, kan kun tage fejl.

        Vis();
        Status.Text = Historik.Path;
    }

    /// <summary>
    /// Perioden, der er valgt — som i Cockpit.
    /// </summary>
    /// <remarks>
    /// Regnestykket ligger i <see cref="Soegefilter"/> og ikke her. «I år»
    /// begyndte engang 31-12 kl. 23:00, fordi 1. januar blev bygget med
    /// sommertidens forskel; den fejl skal rettes ét sted og ikke to.
    /// </remarks>
    private (DateTimeOffset? Fra, DateTimeOffset? Til) Perioden()
    {
        var valg = (Periodefilter.SelectedItem as Periodepunkt)?.Vaerdi;

        if (valg != "valgt") return Soegefilter.Periode(valg ?? "");

        return (
            FraDato.SelectedDate is { } f ? Soegefilter.Lokal(f.Date) : null,
            TilDato.SelectedDate is { } t ? Soegefilter.SlutAfDagen(t) : null);
    }

    private void Vis()
    {
        var slags = Filtre[_fane].Slags;
        var soeg = Soeg.Text.Trim();
        var (fra, til) = Perioden();

        // Navnene slaas op EEN gang for hele tegningen - se Kildenavne.
        var navne = new Kildenavne();

        // Er der arbejde fra mere end een computer i loggen, skal hver linje
        // sige hvilken. Det afgoeres af LOGGEN og ikke af indstillingerne:
        // deler man ikke laengere, staar de gamle poster stadig med hver sin
        // maskine, og saa skal de kunne skelnes.
        var visMaskine = _alle.Select(a => a.Maskine)
            .Where(m => m.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Count() > 1;

        var poster = _alle
            .Where(a => slags is null || a.Slags == slags)
            .Where(a => fra is null || a.Tid >= fra)
            .Where(a => til is null || a.Tid <= til)
            .Where(a => soeg.Length == 0 || Rammer(a, soeg))
            .Select(a => new PostVisning(a, navne, visMaskine))
            .ToList();

        Liste.ItemsSource = poster;

        Antal.Text = poster.Count switch
        {
            0 => "Ingen poster",
            1 => "1 post",
            _ => $"{poster.Count} poster"
        };

        // AFGRAENSET, MEN TOMT, ER IKKE DET SAMME SOM TOMT. Beskeden «der er
        // ikke sket noget endnu» ville vaere forkert, naar der ER sket noget -
        // det er soegningen eller perioden, der ikke rammer.
        var afgraenset = slags is not null || soeg.Length > 0 || fra is not null || til is not null;

        RydKnap.Visibility = afgraenset ? Visibility.Visible : Visibility.Collapsed;

        TomTekst.Visibility = poster.Count == 0 && !afgraenset
            ? Visibility.Visible : Visibility.Collapsed;

        IngenTraef.Visibility = poster.Count == 0 && afgraenset
            ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Rammer søgeordet posten — i overskrift, detaljer eller model?</summary>
    private static bool Rammer(Haendelse a, string soeg) =>
        a.Hvad.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || a.Detaljer.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || a.Model.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || a.Maskine.Contains(soeg, StringComparison.CurrentCultureIgnoreCase);

    private void Soeg_Aendret(object sender, TextChangedEventArgs e)
    {
        SoegPladsholder.Visibility = Soeg.Text.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;

        SoegRyd.Visibility = Soeg.Text.Length == 0
            ? Visibility.Collapsed : Visibility.Visible;

        if (IsInitialized) Vis();
    }

    private void Soeg_Tast(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || Soeg.Text.Length == 0) return;

        Soeg.Clear();
        e.Handled = true;
    }

    private void SoegRyd_Klik(object sender, RoutedEventArgs e)
    {
        Soeg.Clear();
        Soeg.Focus();
    }

    private void Periode_Aendret(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;

        var valgt = (Periodefilter.SelectedItem as Periodepunkt)?.Vaerdi == "valgt";
        Datoraekke.Visibility = valgt ? Visibility.Visible : Visibility.Collapsed;

        // Er der ikke valgt datoer endnu, er der ingen afgraensning at vise
        // paa - og saa skal listen ikke toemmes, mens man leder efter
        // datovaelgeren.
        if (valgt && FraDato.SelectedDate is null && TilDato.SelectedDate is null) return;

        Vis();
    }

    private void Dato_Aendret(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;

        // VENDT OM ER IKKE EN FEJL, MAN SKAL GAETTE SIG TIL. Uden linjen her
        // staar listen bare tom, og man leder efter en post, der er der.
        Datofejl.Text = FraDato.SelectedDate is { } f && TilDato.SelectedDate is { } t && f > t
            ? "«Fra» er efter «til»."
            : "";

        Vis();
    }

    /// <summary>Rydder alle afgrænsninger — søgeord, fane og periode.</summary>
    private void Ryd_Klik(object sender, RoutedEventArgs e)
    {
        Soeg.Clear();

        _fane = 0;
        Byg_Faner();

        Periodefilter.SelectedIndex = 0;
        FraDato.SelectedDate = null;
        TilDato.SelectedDate = null;
        Datoraekke.Visibility = Visibility.Collapsed;
        Datofejl.Text = "";

        Vis();
    }

    private void Genindlaes_Click(object sender, RoutedEventArgs e) => Indlæs();

    /// <summary>
    /// Går til det, linjen handler om.
    ///
    /// Kilden slås op på id'et her og nu. Er den forsvundet mellem visningen
    /// og klikket, siges det — frem for at skifte skærm og vise ingenting.
    /// </summary>
    private void Kilde_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not PostVisning post) return;
        if (post.Kilde.Length == 0 || !post.Findes) return;

        var hoved = Application.Current.MainWindow as MainWindow;
        if (hoved is null) return;

        var gik = post.Slags switch
        {
            HaendelseType.Optagelse or HaendelseType.Transskription
                => hoved.GaaTilOptagelse(post.Kilde),
            _ => Gaa(() => hoved.GaaTilDokumenter(post.Kilde))
        };

        if (!gik)
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den findes ikke længere",
                "Det, linjen handler om, er slettet eller flyttet uden for appen. " +
                "Linjen bliver stående som spor af, at det fandtes.",
                Dialogs.Slags.Valg);
    }

    private static bool Gaa(Action a) { a(); return true; }

    private void Aabn_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(Historik.Path))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Ingen logfil", "Der er ingen logfil endnu — der er ikke sket noget at skrive ned.", Dialogs.Slags.Valg);
            return;
        }

        // Mappen frem for filen: .jsonl har sjaeldent et program tilknyttet, og
        // en dialog om "hvad skal aabne denne fil" er ikke et svar.
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Historik.Path}\"")
        { UseShellExecute = true });
    }
}
