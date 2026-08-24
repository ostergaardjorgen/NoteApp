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

        // EN OPGAVE FRA GOOGLE RETTES DÉR, IKKE HER.
        //
        // Teksten kommer fra Google og bliver overskrevet ved næste hentning.
        // Felter, man kan skrive i, og som bliver rullet tilbage en time
        // senere, er værre end felter, der er låst.
        //
        // Fluebenet er undtagelsen: DET går begge veje.
        if (r.Opgave.Herkomst == Opgavekilde.Google)
        {
            dele.Insert(0, "Google Tasks");

            Navn.IsReadOnly = true;
            Beskrivelse.IsReadOnly = true;

            // FRISTEN SOM TEKST. En laast datovaelger staar som en graa kasse
            // midt i en moerk skaerm og ser ud som noget, der er gaaet i
            // stykker.
            Frist.Visibility = System.Windows.Visibility.Collapsed;
            Fristtekst.Visibility = System.Windows.Visibility.Visible;

            Fristtekst.Text = r.Opgave.Deadline is { } f
                ? f.LocalDateTime.ToString("d. MMMM yyyy")
                : "ingen frist";

            // NOTET STAAR FOR SIG, ikke i Fejl-feltet. Det laa i samme celle
            // som knapperne og loeb ind under dem.
            Googlenote.Text = "Opgaven kommer fra Google Tasks. Tekst og frist rettes dér — "
                            + "her kan du sætte prioritet og krydse den af, og "
                            + "afkrydsningen sendes op igen.";

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

        Gemt = true;
        DialogResult = true;
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
