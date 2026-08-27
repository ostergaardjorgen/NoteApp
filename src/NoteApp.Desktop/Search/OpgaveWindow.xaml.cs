using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Search;

/// <summary>
/// Én opgave, åbnet så der er plads til at arbejde i den.
///
/// HVORFOR DEN FINDES
///
/// Listen i Cockpittet viser opgaven på højst to linjer, så tyve opgaver kan
/// skimmes. Men en opgave fra en transkription er en hel sætning, som nogen
/// sagde — og skal den rettes, skal der være plads til at læse den og skrive
/// i den. De to ting kan ikke være det samme felt.
///
/// DER GEMMES FØRST, NÅR DER TRYKKES GEM.
///
/// Resten af appen gemmer løbende, og det er med vilje: en note, man mister,
/// fordi man glemte at gemme, er den værste fejl, et program kan lave. Her er
/// det omvendt. Man åbner opgaven for at læse den — og en tekst, man har
/// rettet halvt om og fortrudt, skal kunne lukkes uden at have ændret noget.
///
/// Derfor spørges der også, hvis der er noget at miste. Et vindue, der
/// smider en halv times skriveri væk uden at sige noget, er værre end et, der
/// gemmer for meget.
/// </summary>
public partial class OpgaveWindow : Window
{
    private readonly Registeropgave _r;

    /// <summary>Blev der gemt? Kaldes af listen, så den kan læse forfra.</summary>
    public bool Gemt { get; private set; }

    public OpgaveWindow(Registeropgave r)
    {
        InitializeComponent();

        _r = r;

        var dele = new List<string>();

        if (r.Opgave.Ejer.Length > 0) dele.Add(r.Opgave.Ejer);
        dele.Add(r.Moedetitel);
        if (r.Opgave.Kilde.Length > 0) dele.Add("fra " + r.Opgave.Kilde);

        // HER LAA LAASNINGEN AF EN GOOGLE-OPGAVE.
        //
        // Navn, beskrivelse og frist var skrivebeskyttede, og fristen blev vist
        // som graa tekst i stedet for en datovaelger. Begrundelsen var rigtig,
        // saa laenge appen kun kunne LAESE opgaver: felter, man kan skrive i,
        // og som bliver rullet tilbage en time senere, er vaerre end felter,
        // der er laast.
        //
        // Nu skrives rettelsen tilbage - se Googleopgaver.OpdaterAsync - saa
        // den bliver ikke rullet tilbage, og saa er der ingen grund til at
        // laase. AEndringer gaar begge veje for baade kalender og opgaver.

        // ============ HERKOMSTEN AFGOER, HVAD HAKKET SIGER ============
        //
        // Ligger opgaven allerede i Google, er hakket en OPLYSNING og ikke et
        // valg - man kan ikke traekke den hjem igen herfra. Er den lokal, er
        // det et valg.
        var iGoogle = r.Opgave.Herkomst == Opgavekilde.Google;

        if (iGoogle)
        {
            dele.Insert(0, "Google Tasks");

            TilGoogle.IsChecked = true;
            TilGoogle.IsEnabled = false;
            TilGoogle.Content = NoteApp.Core.Sprog.T("opgavewindow.ligger_i_google_tasks");

            Googlenote.Text = NoteApp.Core.Sprog.T("opgavewindow.rettelser_sendes_op");

            Googlenote.Visibility = System.Windows.Visibility.Visible;
        }
        else
        {
            Googlenote.Text = NoteApp.Core.Sprog.T("opgavewindow.saet_hakket_hvis_ogsaa_i_google");

            Googlenote.Visibility = System.Windows.Visibility.Visible;
        }

        Herkomst.Text = string.Join("  ·  ", dele);

        Navn.Text = r.Opgave.Navn.Length > 0 ? r.Opgave.Navn : r.Opgave.Visningsnavn;
        Beskrivelse.Text = r.Opgave.Tekst;

        Frist.SelectedDate = r.Opgave.Deadline?.LocalDateTime.Date;
        Prioritet.SelectedIndex = r.Opgave.Prioritet is >= 1 and <= 3 ? r.Opgave.Prioritet : 0;
        Faerdig.IsChecked = r.Opgave.Faerdig;

        // Markoeren i beskrivelsen, ikke i navnet. Man aabner opgaven for at
        // laese eller rette teksten; navnet er som regel i orden.
        Loaded += (_, _) =>
        {
            Beskrivelse.Focus();
            Beskrivelse.CaretIndex = Beskrivelse.Text.Length;
        };
    }

    /// <summary>
    /// Er der rettet noget, siden vinduet blev åbnet?
    ///
    /// Navnet sammenlignes med DET, FELTET BLEV FYLDT MED — ikke med det
    /// gemte navn. Er navnet udledt af teksten, står der noget i feltet, som
    /// ikke er gemt nogen steder, og uden det her ville vinduet spørge «vil du
    /// kassere?» hver eneste gang, man lukkede en gammel opgave uden at røre
    /// den.
    /// </summary>
    private bool ErRettet()
    {
        var oprindeligtNavn = _r.Opgave.Navn.Length > 0
            ? _r.Opgave.Navn : _r.Opgave.Visningsnavn;

        return Navn.Text.Trim() != oprindeligtNavn.Trim()
            || Beskrivelse.Text != _r.Opgave.Tekst
            || Frist.SelectedDate != _r.Opgave.Deadline?.LocalDateTime.Date
            || Prioritet.SelectedIndex != (_r.Opgave.Prioritet is >= 1 and <= 3 ? _r.Opgave.Prioritet : 0)
            || (Faerdig.IsChecked == true) != _r.Opgave.Faerdig;
    }

    private void Gem_Klik(object sender, RoutedEventArgs e)
    {
        Fejl.Text = "";

        var navn = Navn.Text.Trim();
        var tekst = Beskrivelse.Text.Trim();

        if (navn.Length == 0 && tekst.Length == 0)
        {
            Fejl.Text = "Opgaven skal have et navn eller en beskrivelse.";
            Navn.Focus();
            return;
        }

        // ET UDLEDT NAVN SKRIVES IKKE NED.
        //
        // Feltet blev fyldt med det udledte navn, saa det staar der, ogsaa
        // naar brugeren ikke har rettet noget. Gemte vi det, ville et gaet
        // blive til noget, der ser ud, som om nogen havde valgt det - og saa
        // ville navnet holde op med at foelge teksten, den dag den rettes.
        _r.Opgave.Navn = navn == Opgave.Kort(tekst) ? "" : navn;
        _r.Opgave.Tekst = tekst;

        _r.Opgave.Deadline = Frist.SelectedDate is { } d
            ? new DateTimeOffset(d.Date, TimeZoneInfo.Local.GetUtcOffset(d.Date))
            : null;

        // Saettes fristen i haanden, er den ikke gaettet laengere.
        if (Frist.SelectedDate is not null) _r.Opgave.DeadlineUsikker = false;

        _r.Opgave.Prioritet = Prioritet.SelectedIndex;
        _r.Opgave.SaetFaerdig(Faerdig.IsChecked == true);

        try
        {
            Opgaveregister.Gem(_r);
        }
        catch (Exception ex)
        {
            Fejl.Text = "Kunne ikke gemme: " + ex.Message;
            return;
        }

        // ============ OG SAA OP TIL GOOGLE ============
        //
        // LOKALT FOERST, ALTID. Opgaven er gemt her, foer nettet roeres. Gaar
        // afsendelsen galt - der er ikke net, noeglen er udloebet - er
        // rettelsen der stadig, og den kan sendes op naeste gang.
        //
        // Den anden vej rundt ville betyde, at en fejl i Google kunne koste
        // det, man lige har skrevet.
        _ = TilGoogleAsync();

        Gemt = true;
        DialogResult = true;
    }

    /// <summary>
    /// Lægger opgaven op i Google — eller skriver ændringen tilbage.
    /// </summary>
    /// <remarks>
    /// DEN VENTER IKKE PAA SVAR. Dialogen lukker med det samme; et net, der er
    /// langsomt, maa ikke holde en fast i et vindue, man er faerdig med.
    ///
    /// GAAR DET GALT, SIGES DET - men i en notifikation og ikke som en fejl,
    /// der stopper noget. Opgaven ER gemt; det, der mangler, er kopien hos
    /// Google, og den kan hentes ind igen.
    /// </remarks>
    /// <summary>
    /// Siger til uden at stoppe noget.
    /// </summary>
    /// <remarks>
    /// Det gaar i historikken og i klokken, ikke i en dialog. Opgaven ER
    /// gemt; det, der mangler, er kopien hos Google. En dialog ville kraeve et
    /// klik for noget, man ikke kan goere ved lige nu.
    /// </remarks>
    private static void Meld(string hvad, string detaljer)
    {
        NoteApp.Core.Historik.Skriv(NoteApp.Core.HaendelseType.Hentning,
            hvad, detaljer, NoteApp.Core.Udfald.SeEfter);

        NoteApp.Core.Notifikationer.Meld();
    }

    private async Task TilGoogleAsync()
    {
        var vil = TilGoogle.IsChecked == true;
        var iGoogle = _r.Opgave.Herkomst == Opgavekilde.Google;

        if (!vil && !iGoogle) return;

        try
        {
            var noegle = Integrationsfiler.Hent(Googleopgaver.Id).Opdateringsnoegle;

            if (noegle.Length == 0)
            {
                Meld("Opgaven blev ikke lagt op",
                     "Google Tasks er ikke forbundet. Opgaven er gemt her. " +
                     "Forbind under «Indstillinger» og sæt hakket igen.");
                return;
            }

            if (iGoogle)
            {
                await Googleopgaver.OpdaterAsync(_r.Opgave, noegle);
                return;
            }

            var (id, liste) = await Googleopgaver.OpretAsync(_r.Opgave, noegle);

            _r.Opgave.Herkomst = Opgavekilde.Google;
            _r.Opgave.FremmedId = id;
            _r.Opgave.FremmedListe = liste;

            Opgaveregister.Gem(_r);
        }
        catch (Exception ex)
        {
            Meld("Opgaven kunne ikke sendes til Google",
                 $"Den er gemt her og går ikke tabt. {ex.Message}");
        }
    }

    private void Luk_Klik(object sender, RoutedEventArgs e)
    {
        if (ErRettet())
        {
            var ja = Dialogs.AppDialog.Spoerg(this, "Kassér rettelserne?",
                "Du har ændret noget, der ikke er gemt.",
                godkend: "Kassér", annuller: "Bliv her",
                slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

            if (!ja) return;
        }

        DialogResult = false;
    }
}
