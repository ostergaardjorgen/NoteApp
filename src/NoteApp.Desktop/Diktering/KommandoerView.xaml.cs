using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoteApp.Core;

namespace NoteApp.Desktop.Diktering;

/// <summary>
/// Vågeordet og kommandoerne: hvad du kan sige, og hvad der så sker.
///
/// LISTEN ER EN HVIDLISTE, OG DEN ER DIN EGEN. Der kan ikke køre noget, som
/// ikke står på den.
/// </summary>
public partial class KommandoerView : UserControl
{
    /// <summary>Én kommando, som listen viser den.</summary>
    private sealed record Linje(Kommando Kommando, string Udtryk, string Beskrivelse);

    /// <summary>Ét valg i typelisten.</summary>
    private sealed record Typevalg(Kommandotype Vaerdi, string Navn);

    private bool _indlaest;

    /// <summary>Siger til, når vågeordet er slået til eller fra.</summary>
    public static Action? Aendret { get; set; }

    public KommandoerView()
    {
        InitializeComponent();
        Loaded += (_, _) => Indlaes();
    }

    private void Indlaes()
    {
        _indlaest = false;

        try
        {
            var v = AppSettings.Current;

            VaageordTil.IsChecked = v.VaageordTil;
            KunMoeder.IsChecked = v.VaageordKunVedMoeder;

            Ord.Text = string.Join(Environment.NewLine,
                v.Vaageord is { Count: > 0 } egne ? egne : Vaageord.Standardord);

            // Uden en motor kan der ikke lyttes. Det skal staa, FOER man slaar
            // noget til - ikke bagefter, naar man taler forgaeves.
            IngenMotor.Visibility = Vaageordsvagt.MotorFindes
                ? Visibility.Collapsed
                : Visibility.Visible;

            var minutter = Enumerable
                .Range(Vaageord.MindsteMinutter,
                       Vaageord.StoersteMinutter - Vaageord.MindsteMinutter + 1)
                .ToList();

            Foer.ItemsSource = minutter;
            Efter.ItemsSource = minutter;
            Foer.SelectedItem = Vaageord.Minutter(v.VaageordFoerMinutter, Vaageord.StandardFoerMinutter);
            Efter.SelectedItem = Vaageord.Minutter(v.VaageordEfterMinutter, Vaageord.StandardEfterMinutter);

            NyType.ItemsSource = new[]
            {
                new Typevalg(Kommandotype.Optag, Sprog.T("kommandoer.type_optag")),
                new Typevalg(Kommandotype.Webinar, Sprog.T("kommandoer.type_webinar")),
                new Typevalg(Kommandotype.AabnSkaerm, Sprog.T("kommandoer.type_skaerm")),
                new Typevalg(Kommandotype.AabnProgram, Sprog.T("kommandoer.type_program")),
            };
            NyType.SelectedIndex = 0;

            VisListe();
        }
        finally
        {
            _indlaest = true;
        }
    }

    private static List<Kommando> Liste_() =>
        AppSettings.Current.Kommandoer is { Count: > 0 } egne
            ? egne
            : Kommandotolk.Standard.ToList();

    private void VisListe()
    {
        var liste = Liste_();

        Liste.ItemsSource = liste
            .Select(k => new Linje(k, k.Udtryk, Beskriv(k)))
            .ToList();

        Status.Text = liste.Count == 0
            ? Sprog.T("kommandoer.tom")
            : Sprog.T("kommandoer.antal", liste.Count);
    }

    private static string Beskriv(Kommando k)
    {
        var hvad = Sprog.T(k.Type switch
        {
            Kommandotype.Optag => "kommandoer.type_optag",
            Kommandotype.Webinar => "kommandoer.type_webinar",
            Kommandotype.AabnSkaerm => "kommandoer.type_skaerm",
            _ => "kommandoer.type_program",
        });

        return k.Maal.Length > 0 ? $"{hvad} — {k.Maal}" : hvad;
    }

    // ============================ VÅGEORDET ============================

    private void VaageordTil_Klik(object sender, RoutedEventArgs e) => Gem();

    private void KunMoeder_Klik(object sender, RoutedEventArgs e) => Gem();

    private void Vindue_Valgt(object sender, SelectionChangedEventArgs e) => Gem();

    /// <summary>
    /// Ordene gemmes, når feltet forlades — ikke ved hvert tastetryk.
    /// </summary>
    /// <remarks>
    /// Skrev vi ved hvert tastetryk, ville et halvt ord blive gemt som et
    /// vågeord, og vagten ville blive startet og stoppet for hvert bogstav.
    /// </remarks>
    private void Ord_Forladt(object sender, RoutedEventArgs e) => Gem();

    private void Gem()
    {
        if (!_indlaest) return;

        var v = AppSettings.Current;

        v.VaageordTil = VaageordTil.IsChecked == true;
        v.VaageordKunVedMoeder = KunMoeder.IsChecked == true;

        if (Foer.SelectedItem is int f) v.VaageordFoerMinutter = f;
        if (Efter.SelectedItem is int ef) v.VaageordEfterMinutter = ef;

        // Kun ord, der DUER, gemmes. Et ord paa eet ord ville udloese sig selv,
        // hver gang nogen naevner et navn - se Vaageord.Rens.
        v.Vaageord = Ord.Text
            .Split('\n', '\r')
            .Select(Vaageord.Rens)
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        v.Save();

        // Vagten skal vide det MED DET SAMME. Ellers skal appen genstartes,
        // foer vaageordet begynder at virke - og saa tror man, det er gaaet
        // galt.
        Aendret?.Invoke();
    }

    // ============================ KOMMANDOERNE ============================

    private void NyType_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (NyType.SelectedItem is not Typevalg t) return;

        // «Optag» og «webinar» har intet maal. Et felt, der ikke bruges, skal
        // ikke staa og se ud, som om det skal udfyldes.
        Maalpanel.Visibility = t.Vaerdi is Kommandotype.AabnSkaerm or Kommandotype.AabnProgram
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void NytUdtryk_Tast(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        TilfoejKommando();
        e.Handled = true;
    }

    private void Tilfoej_Klik(object sender, RoutedEventArgs e) => TilfoejKommando();

    private void TilfoejKommando()
    {
        var udtryk = Kommandotolk.Rens(NytUdtryk.Text);
        if (udtryk.Length == 0) return;

        if (NyType.SelectedItem is not Typevalg t) return;

        var liste = Liste_();

        if (liste.Any(k => Kommandotolk.Rens(k.Udtryk).Equals(udtryk, StringComparison.OrdinalIgnoreCase)))
        {
            // Feltet ryddes IKKE - saa kan man se, hvad man skrev, og rette.
            Status.Text = Sprog.T("kommandoer.findes", udtryk);
            return;
        }

        var maal = t.Vaerdi is Kommandotype.AabnSkaerm or Kommandotype.AabnProgram
            ? NytMaal.Text.Trim()
            : "";

        liste.Add(new Kommando(udtryk, t.Vaerdi, maal));

        AppSettings.Current.Kommandoer = liste;
        AppSettings.Current.Save();

        NytUdtryk.Clear();
        NytMaal.Clear();
        VisListe();
        NytUdtryk.Focus();
    }

    private void Fjern_Klik(object sender, RoutedEventArgs e)
    {
        if (Liste.SelectedItem is not Linje valgt) return;

        var liste = Liste_();
        liste.RemoveAll(k => k.Udtryk == valgt.Kommando.Udtryk && k.Type == valgt.Kommando.Type);

        AppSettings.Current.Kommandoer = liste;
        AppSettings.Current.Save();

        VisListe();
    }

    /// <summary>
    /// Henter standardlisten frem igen.
    /// </summary>
    /// <remarks>
    /// DEN ERSTATTER. Det er den eneste knap her, der kan smide noget væk, man
    /// selv har lavet — og derfor spørges der først.
    /// </remarks>
    private void Nulstil_Klik(object sender, RoutedEventArgs e)
    {
        var svar = Dialogs.AppDialog.Spoerg(
            Window.GetWindow(this),
            Sprog.T("kommandoer.nulstil"),
            Sprog.T("kommandoer.nulstil_spoerg"),
            Sprog.T("kommandoer.nulstil"),
            slags: Dialogs.Slags.Valg,
            godkendErStandard: false);

        if (!svar) return;

        AppSettings.Current.Kommandoer = Kommandotolk.Standard.ToList();
        AppSettings.Current.Save();

        VisListe();
    }
}
