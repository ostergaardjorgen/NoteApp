using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Skifter appen mellem lyst og mørkt.
///
/// HVORDAN
///
/// Paletten ligger som femogtyve pensler i Application.Resources, og et skift
/// UDSKIFTER dem: `app.Resources["Panel"] = new SolidColorBrush(...)`.
/// Skærmbillederne slår dem op med `{DynamicResource Panel}`, og det opslag er
/// levende — det opdager, at nøglen peger på noget andet, og tegner om.
///
/// HVORFOR IKKE BARE ÆNDRE PENSLENS FARVE
///
/// Fordi den ikke kan. WPF fryser Freezables, når den kan, og en frossen
/// pensel er uforanderlig for altid. To omveje blev prøvet, og BEGGE SÅ
/// RIGTIGE UD VED OPSTART:
///
///   1. Sætte `pensel.Color` direkte. Alle femogtyve pensler var allerede
///      frosne ved indlæsningen af App.xaml, og skiftet gjorde ingenting.
///   2. Lade farven komme fra en `&lt;Color&gt;` gennem en DynamicResource, så
///      penslen ikke KUNNE fryses ved indlæsningen. Den blev frosset senere.
///
/// Målt 28-08-2026 efter et tryk på knappen:
///
///     Baggrund pensel = #FF14161A  frossen=False   skiftede
///     Panel    pensel = #FFFFFFFF  frossen=True    skiftede ikke
///     Tekst    pensel = #FF1B1B19  frossen=True    skiftede ikke
///
/// Forskellen er, at `Panel` og `Tekst` bruges i STILARTER. WPF forsegler en
/// stil, første gang den bruges, og det fryser de Freezables, der står i dens
/// settere. `Baggrund` bruges kun direkte i markup og fryses derfor aldrig.
///
/// DERFOR VAR ET SKÆRMBILLEDE FRA EN OPSTART IKKE NOK. Ved opstart bygges
/// penslerne, efter farverne er sat, og alt ser rigtigt ud. Fejlen viser sig
/// først, når nogen trykker på knappen. Prøv den vej, ikke opstarten.
///
/// WINDOWS' EGNE KONTROLLER FØLGER MED
///
/// ThemeMode er WPF's indbyggede Fluent-tema. Den sørger for det, vi ikke selv
/// tegner: rullebjælker, markeringsfarve, vindueskant og systemets accent. Uden
/// den ville en lys app få mørke rullebjælker — præcis den slags, der får en
/// app til at se forkert ud uden at man kan sige hvorfor.
/// </summary>
public static class Temaskift
{
    /// <summary>Temaet er skiftet. Til de steder, der sætter farver i kode.</summary>
    public static event Action? Skiftet;

    /// <summary>Er appen lys lige nu?</summary>
    public static bool ErLyst { get; private set; } = true;

    private static bool _lytter;

    /// <summary>
    /// Sæt temaet efter brugerens valg. Kaldes ved opstart og efter et skift
    /// i Indstillinger.
    /// </summary>
    public static void Anvend()
    {
        var valg = AppSettings.Current.Tema;

        var lyst = valg switch
        {
            Temavalg.Lyst => true,
            Temavalg.Moerkt => false,
            _ => WindowsErLyst()
        };

        Saet(lyst);

        // FOELGER VI WINDOWS, SKAL VI OGSAA FOELGE MED, NAAR DEN SKIFTER.
        // Windows skifter selv ved solnedgang, hvis man har sat det op, og en
        // app, der bliver staaende lys i et moerkt skrivebord til aften, ser
        // ud som om den har hængt sig.
        if (!_lytter)
        {
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category != UserPreferenceCategory.General) return;
                if (AppSettings.Current.Tema != Temavalg.FoelgWindows) return;

                // Skiftet kommer paa en anden traad end den, der ejer
                // penslerne. Uden den her linje kaster WPF.
                Application.Current?.Dispatcher.Invoke(() => Saet(WindowsErLyst()));
            };
            _lytter = true;
        }
    }

    private static void Saet(bool lyst)
    {
        ErLyst = lyst;

        var app = Application.Current;
        if (app is null) return;

        // WINDOWS' EGNE KONTROLLER FOERST. Rullebjaelker, markering og
        // vindueskant kommer fra WPF's Fluent-tema, ikke fra vores palet.
#pragma warning disable WPF0001 // ThemeMode er stadig markeret som eksperimentel
        var oensket = lyst ? ThemeMode.Light : ThemeMode.Dark;
        if (app.ThemeMode != oensket) app.ThemeMode = oensket;
#pragma warning restore WPF0001

        var mangler = new List<string>();

        foreach (var (navn, farve) in Tema.Palet(lyst))
        {
            if (app.Resources[navn] is not SolidColorBrush)
            {
                mangler.Add(navn);
                continue;
            }

            // EN NY PENSEL HVER GANG. Den gamle kan vaere frossen, og en
            // frossen pensel kan ikke aendres - se forklaringen paa klassen.
            // DynamicResource-opslagene ude i skaermbillederne opdager, at
            // noeglen peger et andet sted hen, og tegner om.
            app.Resources[navn] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(farve));
        }

        // ============ WINDOWS' EGEN MARKERINGSFARVE ============
        //
        // Naar en linje er markeret i en liste, tegner WPF teksten med
        // SystemColors.HighlightTextBrush - Windows' farve, ikke appens. I et
        // moerkt tema bliver den markerede linje derfor ulaeselig.
        //
        // TRE SKAERME PROEVEDE AT LOESE DET I XAML:
        //
        //   Color="{Binding Color, Source={DynamicResource Tekst}}"
        //
        // Det er ulovligt. «Source» paa en Binding er en almindelig
        // egenskab, ikke en afhaengighedsegenskab, og DynamicResource virker
        // kun paa de sidste. Linjen oversaettes uden brok og kaster foerst,
        // naar netop den skabelon skal tegnes - saa den kunne staa i tre
        // filer i lang tid uden at nogen opdagede det.
        //
        // Set 03-09-2026: en optagelse blev flyttet fra Webinarer til Moeder,
        // flytningen lykkedes, og bagefter kom «A DynamicResourceExtension
        // cannot be set on the Source property of type Binding».
        //
        // Den hoerer hjemme HER. Temaet udskifter alligevel penslerne ved
        // hvert skift, saa markeringsfarven foelger med af sig selv - og
        // skaermene behoever ikke vide, at Windows har en mening om det.
        if (app.Resources["Tekst"] is SolidColorBrush tekst)
            app.Resources[SystemColors.HighlightTextBrushKey] = tekst;

        // DEN SKAL SIGE TIL. Foerste udgave sprang tavst over det, den ikke
        // kunne skifte - og saa blev HELE appen staaende lys, mens den mente,
        // den var moerk. Der gik en halv time med at lede efter en fejl, som
        // koden allerede kendte og ikke fortalte om. 28-08-2026.
        if (mangler.Count > 0)
        {
            try
            {
                Historik.Skriv(HaendelseType.Andet, "Temaet kunne ikke skiftes helt",
                    "Disse farver mangler i App.xaml: " + string.Join(", ", mangler),
                    Udfald.SeEfter);
            }
            catch (Exception)
            {
                // Kan historikken ikke skrives, er der ikke mere at goere.
            }
        }

        Skiftet?.Invoke();
    }

    /// <summary>
    /// Står Windows selv lyst?
    /// </summary>
    /// <remarks>
    /// AppsUseLightTheme er den, der gælder for programmer.
    /// SystemUsesLightTheme gælder proceslinjen og menuen, og de to kan stå
    /// forskelligt — mange kører mørk proceslinje med lyse programmer.
    ///
    /// Kan nøglen ikke læses, svares LYST. Windows' egen standard er lys, og
    /// det er det mindst overraskende at falde tilbage på.
    /// </remarks>
    public static bool WindowsErLyst()
    {
        try
        {
            var v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);

            return v is not int i || i != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Penslen fra paletten. Til den kode, der sætter en farve i hånden.
    /// </summary>
    /// <remarks>
    /// DEN SKAL BRUGES I STEDET FOR «new SolidColorBrush(...)». En pensel,
    /// der er lavet med new, står uden for paletten: den bliver liggende i
    /// sin farve, når temaet skifter, og så står der en mørk etiket i en lys
    /// app. Det var tilfældet 28 steder, indtil 28-08-2026.
    ///
    /// Her deles ét objekt mellem alle, der beder om samme nøgle — og det er
    /// netop pointen: skifter temaet, skifter de alle sammen med.
    ///
    /// Findes nøglen ikke, svares en gennemsigtig pensel frem for at kaste.
    /// Etiketten bliver usynlig, hvilket er slemt nok til at blive opdaget —
    /// og bedre end et vindue, der ikke kan åbnes.
    /// </remarks>
    public static SolidColorBrush Pensel(string noegle)
    {
        if (Application.Current?.TryFindResource(noegle) is SolidColorBrush p) return p;
        return new SolidColorBrush(Colors.Transparent);
    }

    /// <summary>
    /// Findes hver palet-nøgle som en pensel? Til prøverne.
    /// </summary>
    /// <returns>Det, der mangler. Tom liste = alt er der.</returns>
    /// <remarks>
    /// Frossenhed prøves IKKE længere. Penslerne bliver udskiftet og ikke
    /// ændret, så det er ligegyldigt, om den gamle var frossen — og en prøve,
    /// der holder øje med noget, der ikke længere betyder noget, er værre end
    /// ingen prøve: den ser ud, som om den passer på noget.
    ///
    /// Det, der SKAL passes på, er, at skærmbillederne slår paletten op med
    /// DynamicResource. Det prøver TemafilTest på filerne.
    /// </remarks>
    public static IReadOnlyList<string> Efterse(ResourceDictionary ordbog)
    {
        var galt = new List<string>();

        foreach (var navn in Tema.Nøglerne)
            if (ordbog[navn] is not SolidColorBrush)
                galt.Add($"{navn}: findes ikke som pensel");

        return galt;
    }
}
