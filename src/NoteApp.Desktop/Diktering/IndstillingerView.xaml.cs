using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Diktering;

/// <summary>
/// Dikteringens egne indstillinger.
/// </summary>
/// <remarks>
/// FLYTTET FRA INDSTILLINGER 31-08-2026. Alt andet om diktering laa allerede
/// under Diktering; kun kontakten og sproget laa et andet sted. Man skulle
/// altsaa forbi en skaerm mere for at slaa til, hvad man stod og skulle
/// bruge.
///
/// Indholdet er det samme. Det, der var <c>Status</c> paa indstillingsskaermen,
/// er nu fanens egen linje nederst.
/// </remarks>
public partial class IndstillingerView : UserControl
{
    public IndstillingerView()
    {
        InitializeComponent();

        // Samme regel som de oevrige faner: det afgoeres af, om fanen kan SES.
        // Bliver noget aendret i opsaetningen, skal fanen vise det, naeste gang
        // man kigger paa den - og ikke det, der stod, da appen startede.
        IsVisibleChanged += (_, _) => { if (IsVisible) IndlaesDiktering(); };
        Loaded += (_, _) => IndlaesDiktering();
    }

    // ============================ DIKTERING ============================

    /// <summary>Ét sprog i rullelisten.</summary>
    private sealed record Sprogvalg(string Kode, string Navn);

    /// <summary>Et formål, som det står i listen.</summary>
    private sealed record Formaalsvalg(Dikteringsformaal Vaerdi, string Navn);

    private bool _dikteringIndlaest;

    /// <summary>
    /// Fylder dikteringsfanen ud fra det, der faktisk er gemt.
    /// </summary>
    /// <remarks>
    /// <c>_dikteringIndlaest</c> holder hændelserne ude, mens felterne sættes.
    /// Uden den ville hvert felt gemme sig selv under indlæsningen — og et
    /// valg, brugeren aldrig har truffet, ville blive skrevet ned som om han
    /// havde.
    /// </remarks>
    private void IndlaesDiktering()
    {
        _dikteringIndlaest = false;

        try
        {
            var v = AppSettings.Current;

            // ============ SPROGET, DU TALER ============
            //
            // Samme liste som moederne bruger. «Lad appen finde selv» er med,
            // men den er ikke standarden: uden et sprog gaetter modellen, og
            // paa et kort klip er der naesten intet at gaette ud fra.
            //
            // Maalt 30-08-2026: «hallo, hallo, hallo» paa dansk kom tilbage
            // som «Alors, alors, alors ?». Fransk.
            // EN NAVNGIVEN TYPE OG IKKE EN ANONYM. Den anonyme oversatte fint
            // og braekkede foerst paa skaermen: castet til Sprogvalg kan ikke
            // kontrolleres af oversaetteren, saa fejlen viste sig som
            // «InvalidCastException», da fanen blev aabnet. Set 30-08-2026.
            var sprogene = Transcribe.SprogvalgWindow.Sprog
                .Select(s => new Sprogvalg(s.Kode, s.Navn))
                .ToList();

            DikteringSprog.ItemsSource = sprogene;

            var mit = v.Talesprog;
            DikteringSprog.SelectedItem =
                sprogene.FirstOrDefault(s => s.Kode == mit)
                ?? sprogene.FirstOrDefault(s => s.Kode == "da");

            DitNavn.Text = v.DitNavn ?? "";

            DikteringTil.IsChecked = v.DikteringTil;
            DikteringPuds.IsChecked = v.DikteringPuds;
            DikteringFagord.IsChecked = v.DikteringFagord;
            DikteringIndsaet.IsChecked = v.DikteringIndsaet;
            DikteringEfterProgram.IsChecked = v.DikteringEfterProgram;

            // Loftet: hvert minut fra det mindste til det stoerste. En fri
            // talindtastning ville give nul og bogstaver, og saa skal der
            // baade valideres og forklares.
            DikteringLoft.ItemsSource = Enumerable
                .Range(Holdvurdering.MindsteLoftMinutter,
                       Holdvurdering.StoersteLoftMinutter - Holdvurdering.MindsteLoftMinutter + 1)
                .ToList();

            DikteringLoft.SelectedItem =
                (int)Holdvurdering.LoftFra(v.DikteringLoftMinutter).TotalMinutes;

            DikteringFormaal.ItemsSource = new[]
            {
                new Formaalsvalg(Dikteringsformaal.Note, Sprog.T("settingsview.diktering_formaal_note")),
                new Formaalsvalg(Dikteringsformaal.Mail, Sprog.T("settingsview.diktering_formaal_mail")),
                new Formaalsvalg(Dikteringsformaal.Opgave, Sprog.T("settingsview.diktering_formaal_opgave")),
            };

            var valgt = Enum.TryParse<Dikteringsformaal>(v.DikteringFormaal, ignoreCase: true, out var f)
                ? f
                : Dikteringsformaal.Note;

            DikteringFormaal.SelectedItem = ((IEnumerable<Formaalsvalg>)DikteringFormaal.ItemsSource)
                .FirstOrDefault(x => x.Vaerdi == valgt);

            // Uden noegle kan der ikke dikteres. Det skal staa, FOER man slaar
            // noget til - ikke bagefter, naar man taler til et program, der
            // ikke kan svare.
            DikteringNoegle.Visibility = SkyNoegle.Hent() is null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            // ============ ÉN FANE MÅ IKKE VÆLTE HELE SKÆRMEN ============
            //
            // Her var kun «finally». Gik indlaesningen galt, slap fejlen ud
            // til appens faelles haandtering, og saa kom «Der gik noget galt»
            // som en modal kasse - oven paa en skaerm, brugeren ikke engang
            // kunne bruge bagefter.
            //
            // Set 30-08-2026: et forkert cast i sprogvalget. Det oversatte
            // fint og braekkede foerst, da fanen blev aabnet.
            //
            // Fejlen skjules ikke - den skrives i historikken og staar paa
            // fanen. Men resten af indstillingerne bliver ved med at virke.
            Status.Text = $"Dikteringsindstillingerne kunne ikke læses: {ex.Message}";

            try
            {
                Historik.Skriv(HaendelseType.Andet, "En indstillingsfane kunne ikke læses",
                    $"Diktering: {ex.GetType().Name} — {ex.Message}", Udfald.SeEfter);
            }
            catch (Exception)
            {
                // Kan historikken ikke skrives, er der ikke mere at goere.
            }
        }
        finally
        {
            _dikteringIndlaest = true;
        }
    }

    private void GemDiktering()
    {
        if (!_dikteringIndlaest) return;

        var v = AppSettings.Current;

        v.DikteringTil = DikteringTil.IsChecked == true;
        v.DikteringPuds = DikteringPuds.IsChecked == true;
        v.DikteringFagord = DikteringFagord.IsChecked == true;
        v.DikteringIndsaet = DikteringIndsaet.IsChecked == true;
        v.DikteringEfterProgram = DikteringEfterProgram.IsChecked == true;

        if (DikteringLoft.SelectedItem is int minutter) v.DikteringLoftMinutter = minutter;
        if (DikteringFormaal.SelectedItem is Formaalsvalg f) v.DikteringFormaal = f.Vaerdi.ToString();

        // Sproget, brugeren TALER. Det sendes med hver diktering, saa modellen
        // ikke skal gaette paa et klip, der maaske er tre ord langt.
        //
        // «is Sprogvalg» og ikke dynamic: passer typen ikke, sker der
        // ingenting - i stedet for at vaelte skaermen med en fejl, der
        // foerst viser sig, naar nogen aabner fanen.
        if (DikteringSprog.SelectedItem is Sprogvalg valgtSprog) v.MitSprog = valgtSprog.Kode;

        // Navnet gemmes ogsaa herfra. Tomt felt betyder tomt navn og ikke
        // «husk det gamle» - sletter man det med vilje, skal underskriften
        // under en dikteret mail ogsaa vaere vaek.
        var navn = DitNavn.Text.Trim();
        v.DitNavn = navn.Length == 0 ? null : navn;

        v.Save();

        // Genvejen skal vide det MED DET SAMME. Ellers skal appen genstartes,
        // foer et hold begynder at betyde noget - og saa tror man, det er
        // gaaet galt.
        Dikteringsskift?.Invoke(v.DikteringTil);
    }

    /// <summary>Siger til, når dikteringen bliver slået til eller fra.</summary>
    public static Action<bool>? Dikteringsskift { get; set; }

    private void DikteringTil_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringPuds_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringFagord_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringIndsaet_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringEfterProgram_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringLoft_Valgt(object sender, SelectionChangedEventArgs e) => GemDiktering();

    private void DikteringFormaal_Valgt(object sender, SelectionChangedEventArgs e) => GemDiktering();

    private void DikteringSprog_Valgt(object sender, SelectionChangedEventArgs e) => GemDiktering();

    /// <summary>Gemmer navnet, naar feltet forlades.</summary>
    private void DitNavn_Forladt(object sender, RoutedEventArgs e)
    {
        if (!_dikteringIndlaest) return;

        var navn = DitNavn.Text.Trim();
        AppSettings.Current.DitNavn = navn.Length == 0 ? null : navn;
        AppSettings.Current.Save();
    }
}
