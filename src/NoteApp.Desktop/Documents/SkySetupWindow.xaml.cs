using System.Diagnostics;
using System.Windows;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Documents;

/// <summary>
/// Der, hvor man tilslutter den europæiske bearbejdning.
///
/// HVORFOR DET IKKE BARE ER ET FELT TIL EN NØGLE
///
/// To ting afgør, om løftet om europæisk bearbejdning holder, og kun den ene
/// kan appen selv sikre:
///
///   1. AT DET EUROPÆISKE ENDEPUNKT BRUGES. Det er bygget ind og kan ikke
///      slås fra. Det står her, fordi brugeren ellers ikke kan vide det —
///      og fordi det globale endepunkt er den fælde, vi selv gik i.
///
///   2. AT DATA IKKE BRUGES TIL TRÆNING. Det er en indstilling på brugerens
///      konto hos Mistral. Appen kan hverken sætte den eller aflæse den.
///
/// Nummer to er derfor et krav om en bekræftelse frem for en oplysning. Det
/// er ikke en formalitet: uden den ville appen fremstille noget som sikret,
/// som den ikke har rørt ved.
/// </summary>
public partial class SkySetupWindow : Window
{
    public SkySetupWindow()
    {
        InitializeComponent();

        EndepunktTekst.Text =
            $"Referatet sendes til {SkyKatalog.Endpoint}\n\n" +
            "Det er Mistrals europæiske endepunkt. Bearbejdningen sker i Europa, " +
            "og appen kalder ikke andet.";

        // Er der allerede en noegle, skal den kunne fjernes igen. En
        // tilslutning, der kun kan slaas til, er ikke et valg.
        if (SkyNoegle.Hent() is not null)
        {
            FjernKnap.Visibility = Visibility.Visible;
            HakTraening.IsChecked = true;
        }

        FeltNoegle.Focus();
    }

    private void AabnKonsol_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://console.mistral.ai") { UseShellExecute = true });
        }
        catch (Exception)
        {
            Dialogs.AppDialog.Vis(this, "Kunne ikke åbne browseren",
                "Gå til console.mistral.ai i din browser.", Dialogs.Slags.Valg);
        }
    }

    private async void Gem_Click(object sender, RoutedEventArgs e)
    {
        var noegle = FeltNoegle.Password.Trim();

        if (noegle.Length == 0) { Vis("Skriv nøglen ind først."); return; }

        if (HakTraening.IsChecked != true)
        {
            Vis("Sæt hakket ved træning først.\n\n" +
                "Det er ikke en formalitet. Appen kan ikke se den indstilling på din konto, " +
                "så hvis den ikke er slået fra, sender du data, der må bruges til at træne " +
                "modeller — og så holder løftet ikke.");
            return;
        }

        // AFPROEV FOER DER GEMMES.
        //
        // En noegle, der er skrevet forkert af, fejler ellers foerst den dag
        // man staar med et moede og vil have et referat. Kaldet henter
        // modellisten og sender ingen moededata.
        Vis("Afprøver nøglen mod det europæiske endepunkt …", fejl: false);

        try
        {
            var modeller = await new SkyRunner(noegle).ModellerAsync();

            if (!modeller.Any(m => m.StartsWith("mistral-", StringComparison.OrdinalIgnoreCase)))
            {
                Vis("Nøglen virker, men der er ingen Mistral-modeller på det europæiske " +
                    "endepunkt for den. Kontrollér abonnementet på console.mistral.ai.");
                return;
            }

            SkyNoegle.Gem(noegle);
            Luk();
        }
        catch (Exception ex)
        {
            Vis(ex.Message);
        }
    }

    private void Fjern_Click(object sender, RoutedEventArgs e)
    {
        var ok = Dialogs.AppDialog.Spoerg(this,
            "Fjern nøglen?",
            "Referater laves derefter kun på maskinen. Dine dokumenter bliver liggende.",
            godkend: "Fjern nøglen",
            annuller: "Behold den",
            slags: Dialogs.Slags.Pas_paa,
            godkendErStandard: false);

        if (!ok) return;

        SkyNoegle.Slet();
        Luk();
    }

    /// <summary>
    /// Lukker og opdaterer løftet i sidebjælken.
    ///
    /// Det sker HER frem for hos den, der åbnede vinduet. Der er to veje ind —
    /// «AI-modeller» og dokumentdialogen — og en tredje kommer før eller
    /// siden. Ligger opdateringen hos kalderen, er det den nye vej, der
    /// glemmer den, og så står der «intet forlader denne pc», efter man lige
    /// har tilsluttet noget, der kan.
    /// </summary>
    private void Luk()
    {
        (Application.Current.MainWindow as MainWindow)?.OpdaterDataLoefte();
        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Vis(string besked, bool fejl = true)
    {
        Fejl.Text = besked;
        Fejl.Foreground = (System.Windows.Media.Brush)FindResource(fejl ? "Fare" : "TekstSvag");
        Fejl.Visibility = Visibility.Visible;
    }
}
