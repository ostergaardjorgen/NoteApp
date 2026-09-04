using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoteApp.Core;
using NoteApp.Core.Documents;

namespace NoteApp.Desktop.Transcribe;

/// <summary>
/// Dokumenterne, der er lavet ud af ÉN optagelse — ét ad gangen.
/// </summary>
/// <remarks>
/// ET DOKUMENT FINDES IKKE UDEN EN OPTAGELSE. Det skrives ud af et møde eller
/// et webinar, og der er ingen anden vej til at lave et. Derfor er det HER,
/// dokumenterne bor; dokumentarkivet som selvstændig skærm er fjernet
/// 04-09-2026, og alt der pegede derhen lander nu på den optagelse,
/// dokumentet er lavet af.
///
/// Opslaget står i <see cref="DocumentStore.ForMoede"/>, så det kan prøves
/// uden en skærm. Se <c>DokumentsammenhaengTest</c>.
/// </remarks>
public partial class Dokumentrude : UserControl
{
    private IReadOnlyList<DocumentInfo> _dokumenter = Array.Empty<DocumentInfo>();
    private DocumentInfo? _valgt;

    private string? _moedeId;
    private string? _mappe;
    private string? _titel;

    /// <summary>Sat, mens rækken bygges — så et Checked ikke tegner alt om midt i.</summary>
    private bool _bygger;

    /// <summary>Stilen på knapperne i dokumentrækken — den samme som fanerne ovenfor.</summary>
    /// <remarks>
    /// SLÅS OP ÉN GANG og gemmes. Rækken bygges om ved hvert tastetryk i
    /// søgefeltet og ved hvert tik fra en kørsel, og et opslag pr. knap pr.
    /// gang er både spild og en ting, der kan fejle midt i en tegning.
    /// Farverne i stilen er DynamicResource, så et temaskift følger stadig med.
    /// </remarks>
    private readonly Style? _fanestil;

    public Dokumentrude()
    {
        InitializeComponent();

        _fanestil = TryFindResource("Fanevalg") as Style
                    ?? Application.Current?.TryFindResource("Fanevalg") as Style;

        Vis(null, null, null);
    }

    /// <summary>Hvor mange dokumenter der er lavet ud af den viste optagelse.</summary>
    public int Antal => _dokumenter.Count;

    /// <summary>Der blev trykket «Opret dokument» her i ruden.</summary>
    /// <remarks>
    /// SAMME VEJ SOM KNAPPEN I UDSKRIFTSRUDEN. Ruden kender dokumenterne og
    /// ikke optagelsen — hvor lyden ligger, hvad mødet hedder, hvilken mødetype
    /// der blev valgt — så den siger til, og <c>TranscribeView</c> gør arbejdet.
    /// To knapper, ét forløb: en kopi ville skride fra den anden ved første
    /// rettelse.
    /// </remarks>
    public event Action? OpretDokument;

    /// <summary>
    /// Viser dokumenterne for én optagelse.
    /// </summary>
    /// <remarks>
    /// DER LÆSES FRA DISKEN HVER GANG. En kopi, der kan blive uenig med
    /// virkeligheden, er værre end en langsom læsning — og et dokument, der
    /// lige er blevet færdigt i baggrunden, skal stå her uden en genstart.
    /// Samme valg som i kvitteringerne og i søgningen, og af samme grund.
    /// </remarks>
    /// <param name="aabnId">
    /// Dokumentet, der skal være valgt. Null betyder «det nyeste» — og det er
    /// det normale: kommer man ind på fanen, er det seneste dokument det, man
    /// er ude efter. Sat, når man kommer fra en søgning eller fra beskeden om,
    /// at et bestemt dokument blev færdigt.
    /// </param>
    /// <param name="position">
    /// Tegnnummeret i dokumentets tekst, der skal springes til. Nul betyder
    /// «vis bare dokumentet». Kommer fra en søgning, hvor man klikkede på ét
    /// bestemt sted i teksten frem for på dokumentet som helhed.
    /// </param>
    public void Vis(string? moedeId, string? mappe, string? titel,
                    string? aabnId = null, int position = 0)
    {
        var samme = moedeId == _moedeId && mappe == _mappe;

        _moedeId = moedeId;
        _mappe = mappe;
        _titel = titel;

        // NYESTE FOERST, OG DET SORTERES HER. Raekkefoelgen fra lageret er
        // filsystemets, og den er ikke et loefte.
        _dokumenter = DocumentStore.ForMoede(moedeId, mappe)
            .OrderByDescending(d => d.Created)
            .ToList();

        _valgt = Vaelg(samme, aabnId);

        Byg_Raekke();
        Tegn();

        if (position > 0) Spring_Til(position, 60);
    }

    /// <summary>
    /// Hvilket dokument der skal stå valgt.
    /// </summary>
    /// <remarks>
    /// STANDARDEN ER DET NYESTE. Det er dét, man lige har lavet, og dét man
    /// kommer ind for at se.
    ///
    /// MEN VALGET MAA IKKE HOPPE TILBAGE, MENS MAN LÆSER. <c>Vis</c> kaldes
    /// igen ved hvert tik fra en kørsel i baggrunden — havde den altid valgt
    /// det nyeste, ville et dokument, man havde klikket sig ind på, blive
    /// skiftet væk under hånden hvert sekund. Er det stadig den samme
    /// optagelse, og findes valget endnu, bliver det stående. Undtagelsen er
    /// et NYT nyeste dokument: så er det lige blevet lavet, og så er det dét,
    /// man venter på.
    /// </remarks>
    private DocumentInfo? Vaelg(bool sammeOptagelse, string? aabnId)
    {
        if (_dokumenter.Count == 0) return null;

        if (aabnId is { Length: > 0 }
            && _dokumenter.FirstOrDefault(d => d.Id == aabnId) is { } bedt)
            return bedt;

        var nyeste = _dokumenter[0];

        if (!sammeOptagelse || _valgt is null) return nyeste;

        // Valget er vaek - slettet, mens ruden stod aaben.
        if (_dokumenter.All(d => d.Id != _valgt.Id)) return nyeste;

        // Et dokument, raekken ikke kendte, er lige blevet lavet. Det skal frem.
        if (!Kendt(nyeste.Id)) return nyeste;

        return _dokumenter.First(d => d.Id == _valgt.Id);
    }

    /// <summary>
    /// Stiller ruden tilbage på det nyeste dokument.
    /// </summary>
    /// <remarks>
    /// Kaldes, når dokumentfanen bliver valgt — se <c>TranscribeView.Fane_Skiftet</c>
    /// for hvorfor nulstillingen hører til dér og ikke i <see cref="Vis"/>.
    /// </remarks>
    public void VaelgNyeste()
    {
        if (_dokumenter.Count == 0) return;
        if (_valgt is not null && _valgt.Id == _dokumenter[0].Id) return;

        _valgt = _dokumenter[0];

        Byg_Raekke();
        Tegn();
        Rulle.ScrollToTop();

        // Stod der et soegeord, gaelder det ogsaa her.
        Spring_Til_Traef();
    }

    /// <summary>Har rækken allerede en knap for det id? Så er dokumentet ikke nyt.</summary>
    private bool Kendt(string id) =>
        Dokumentraekke.Children.OfType<RadioButton>().Any(k => (string?)k.Tag == id);

    /// <summary>Én knap pr. dokument, nyeste først.</summary>
    private void Byg_Raekke()
    {
        _bygger = true;

        Dokumentraekke.Children.Clear();

        // Er der mere end eet dokument med den samme moedetype, skal tiden med.
        // Ellers staar der «Moedereferat» to gange, og saa er raekken ubrugelig.
        var typer = _dokumenter.GroupBy(Etiket).ToDictionary(g => g.Key, g => g.Count());

        foreach (var d in _dokumenter)
        {
            var navn = Etiket(d);

            var knap = new RadioButton
            {
                GroupName = "dokumentvalg",
                Style = _fanestil,
                Content = typer[navn] > 1
                    ? $"{navn} · {d.Created.LocalDateTime:dd-MM HH:mm}"
                    : navn,
                Tag = d.Id,
                Margin = new Thickness(0, 0, 8, 0),
                IsChecked = _valgt is not null && d.Id == _valgt.Id,
                ToolTip = $"{(d.Title.Length > 0 ? d.Title : d.FileName)}\n"
                          + d.Created.LocalDateTime.ToString("dd-MM-yyyy HH:mm")
            };

            knap.Checked += Dokument_Valgt;
            Dokumentraekke.Children.Add(knap);
        }

        // RAEKKEN ER VAEK, NAAR DER IKKE ER NOGET AT VAELGE IMELLEM. En tom
        // stribe med en kant under er en rude, der ser i stykker ud.
        Raekkeramme.Visibility = _dokumenter.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;

        _bygger = false;
    }

    private static string Etiket(DocumentInfo d) =>
        d.Template.Length > 0 ? d.Template
        : d.Title.Length > 0 ? d.Title
        : d.FileName;

    private void Dokument_Valgt(object sender, RoutedEventArgs e)
    {
        if (_bygger) return;
        if (sender is not RadioButton { Tag: string id }) return;

        _valgt = _dokumenter.FirstOrDefault(d => d.Id == id);

        Tegn();
        Rulle.ScrollToTop();

        // Stod der et soegeord, gaelder det ogsaa det dokument, man lige valgte.
        Spring_Til_Traef();
    }

    /// <summary>Fylder ruden med det valgte dokument — eller med beskeden om, at der ingen er.</summary>
    private void Tegn()
    {
        var harValg = _valgt is not null;

        Soegefelt.IsEnabled = _dokumenter.Count > 0;
        SletKnap.IsEnabled = harValg;
        AabnKnap.IsEnabled = harValg && File.Exists(DocumentStore.Path_(_valgt!));

        Indholdsramme.Visibility = Visibility.Collapsed;
        Mangler.Visibility = Visibility.Collapsed;

        if (_mappe is null && _moedeId is null)
        {
            Overskrift.Text = "Dokumenter";
            Underskrift.Text =
                "Vælg en optagelse i træet til venstre. Her står de dokumenter, der er lavet "
                + "ud af netop den — referater, notater og alt andet, mødetyperne kan lave.";
            return;
        }

        var navn = string.IsNullOrWhiteSpace(_titel) ? "optagelsen" : $"«{_titel}»";

        if (_valgt is null)
        {
            Overskrift.Text = "Ingen dokumenter endnu";

            // TEKSTEN SIGER, HVAD MAN GOER - ikke bare at der ikke er noget.
            // En tom skaerm, der kun konstaterer, er en blindgyde.
            Underskrift.Text =
                $"Der er ikke lavet dokumenter ud af {navn} endnu. Skriv optagelsen ud, "
                + "og tryk så «Opret dokument» øverst — så laves et referat eller "
                + "et andet dokument ud fra den mødetype, du vælger.";
            return;
        }

        Overskrift.Text = _valgt.Title.Length > 0 ? _valgt.Title : _valgt.FileName;

        var linje = string.Join("  ·  ", new[]
        {
            _valgt.Created.LocalDateTime.ToString("dd-MM-yyyy HH:mm"),
            _valgt.Template,
            _valgt.Model
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        Underskrift.Text = _valgt.Description.Length > 0
            ? $"{linje}\n{_valgt.Description}"
            : linje;

        if (!File.Exists(DocumentStore.Path_(_valgt)))
            Mangler.Visibility = Visibility.Visible;

        var tekst = _valgt.Markdown.Trim();

        Indhold.Text = tekst.Length > 0 ? tekst : "(intet gemt indhold)";
        Indholdsramme.Visibility = Visibility.Visible;
    }

    // ------------------------------------------------------------ søgningen

    /// <summary>
    /// Søgningen: hvilke dokumenter rammer, og hvor i teksten.
    /// </summary>
    /// <remarks>
    /// DEN SKIFTER DOKUMENT, HVIS DET, MAN STÅR PÅ, IKKE RAMMER. Ellers ville
    /// en søgning på noget, der står i det andet referat, se ud som om ordet
    /// ikke fandtes nogen steder — og det er præcis den slags, der får folk
    /// til at åbne Word alligevel.
    /// </remarks>
    private void Soeg_Aendret(object sender, TextChangedEventArgs e)
    {
        SoegPladsholder.Visibility = Soeg.Text.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;

        SoegRyd.Visibility = Soeg.Text.Length == 0
            ? Visibility.Collapsed : Visibility.Visible;

        var soeg = Soeg.Text.Trim();

        if (soeg.Length > 0 && _valgt is not null && !Rammer(_valgt, soeg)
            && _dokumenter.FirstOrDefault(d => Rammer(d, soeg)) is { } andet)
        {
            _valgt = andet;
            Byg_Raekke();
            Tegn();
        }

        Spring_Til_Traef();
    }

    /// <summary>Springer hen til første forekomst i det viste dokument og markerer den.</summary>
    private void Spring_Til_Traef()
    {
        var soeg = Soeg.Text.Trim();

        if (soeg.Length == 0 || _valgt is null)
        {
            Indhold.Select(0, 0);
            return;
        }

        var nr = Indhold.Text.IndexOf(soeg, StringComparison.CurrentCultureIgnoreCase);

        if (nr < 0)
        {
            Indhold.Select(0, 0);
            return;
        }

        Spring_Til(nr, soeg.Length);
    }

    /// <summary>Markerer et sted i teksten og ruller det frem.</summary>
    /// <remarks>
    /// Rullelisten skal FLYTTE SIG, ellers er markeringen et sted, man ikke
    /// kan se. Teksten er én lang TextBox uden egen rulning, så det er ruden
    /// udenom, der skal flyttes.
    /// </remarks>
    private void Spring_Til(int position, int laengde)
    {
        if (position < 0 || position >= Indhold.Text.Length) return;

        Indhold.Select(position, Math.Min(laengde, Indhold.Text.Length - position));

        try
        {
            var kasse = Indhold.GetRectFromCharacterIndex(position);
            var punkt = Indhold.TransformToAncestor(Indre).Transform(new Point(0, kasse.Top));

            Rulle.ScrollToVerticalOffset(Math.Max(0, punkt.Y - 80));
        }
        catch (InvalidOperationException)
        {
            // Endnu ikke maalt op. Saa staar man i toppen, og det er i orden.
        }
    }

    /// <summary>Rammer søgeordet dokumentet — i titel, beskrivelse, mødetype eller tekst?</summary>
    private static bool Rammer(DocumentInfo d, string soeg) =>
        d.Title.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || d.Description.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || d.Template.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || d.FileName.Contains(soeg, StringComparison.CurrentCultureIgnoreCase)
        || d.Markdown.Contains(soeg, StringComparison.CurrentCultureIgnoreCase);

    private void Soeg_Tast(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Soeg.Text.Length == 0) return;

        Soeg.Clear();
        e.Handled = true;
    }

    private void SoegRyd_Klik(object sender, RoutedEventArgs e)
    {
        Soeg.Clear();
        Soeg.Focus();
    }

    // ------------------------------------------------------------ knapperne

    private void OpretKlik(object sender, RoutedEventArgs e) => OpretDokument?.Invoke();

    private void Aabn_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var fil = DocumentStore.Path_(_valgt);

        if (!File.Exists(fil))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke åbne",
                "Filen findes ikke længere.", Dialogs.Slags.Valg);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(fil) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne",
                $"Dokumentet kunne ikke åbnes.\n\n{ex.Message}\n\n"
                + "Er der ikke noget program til .docx-filer, kan de åbnes i Word, "
                + "LibreOffice eller Google Docs.", Dialogs.Slags.Valg);
        }
    }

    /// <summary>
    /// Sletter det valgte dokument.
    /// </summary>
    /// <remarks>
    /// DEN LAA I DOKUMENTARKIVET, og arkivet er væk. Uden den her kunne et
    /// dokument aldrig fjernes igen — og en app, hvor noget kun kan blive
    /// flere, er ikke færdig.
    ///
    /// «Behold det» er forvalgt. Der er ingen papirkurv.
    /// </remarks>
    private void Slet_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgt is not { } d) return;

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Slet «{(d.Title.Length > 0 ? d.Title : d.FileName)}»?",
            "Selve optagelsen og transkriptionen bliver liggende — det er kun dokumentet, "
            + "der slettes. Du kan lave et nyt af den samme transkription.",
            godkend: "Slet dokumentet", annuller: "Behold det",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        try
        {
            // SLETNINGEN LOGGES, FOER FILEN ER VAEK. Der er ingen papirkurv.
            // Linjen i historikken er derfor det eneste spor af, at dokumentet
            // fandtes - og id'et goer, at en aeldre linje om det samme dokument
            // stadig kan kendes.
            var navn = d.Title.Length > 0 ? d.Title : d.FileName;
            var id = d.Id;

            DocumentStore.Delete(d);

            Historik.Skriv(HaendelseType.Slettet, $"Dokument slettet: {navn}",
                "Der er ingen papirkurv — filen er væk.",
                Udfald.Fuldført, kilde: id);

            _valgt = null;
            Vis(_moedeId, _mappe, _titel);
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke slette",
                $"Dokumentet kunne ikke slettes.\n\n{ex.Message}", Dialogs.Slags.Pas_paa);
        }
    }
}
