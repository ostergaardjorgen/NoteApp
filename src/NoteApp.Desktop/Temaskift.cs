using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Skifter appen mellem lyst og mørkt — uden at tegne den om.
///
/// HVORDAN DET KAN LADE SIG GØRE SÅ BILLIGT
///
/// Skærmbillederne slår farverne op på navn, `{StaticResource Baggrund}`, og
/// det gør de 969 steder. Byttede vi ordbogen ud, ville ingen af dem opdage
/// det: StaticResource slår op ÉN gang, ved indlæsningen. Så skulle alle 969
/// laves om til DynamicResource — en ændring i hver eneste fil, og et
/// langsommere opslag hver gang.
///
/// I stedet BLIVER PENSLERNE LIGGENDE. Det er de samme objekter hele appens
/// levetid, og det er FARVEN bag dem, der skiftes:
///
///     &lt;Color x:Key="BaggrundFarve"&gt;#FFF7F7F5&lt;/Color&gt;
///     &lt;SolidColorBrush x:Key="Baggrund" Color="{DynamicResource BaggrundFarve}" /&gt;
///
/// Her byttes `BaggrundFarve` ud. Penslen henter sin farve dynamisk, opdager
/// det selv, og alt, der bruger den, bliver tegnet om. Ét sted at skifte, nul
/// ændringer i skærmbillederne.
///
/// OMVEJEN OVER EN Color ER IKKE PYNT. Første forsøg satte `pensel.Color`
/// direkte, og det virkede ikke: WPF FRYSER SELV RESURSER FRA XAML, og en
/// frossen Freezable kan ikke ændres. Alle femogtyve pensler var frosne, og
/// skiftet sprang dem tavst over — appen sagde «mørkt» og så lys ud.
///
/// En Freezable med en uafklaret dynamisk reference KAN ikke fryses. Det er
/// hele grunden til, at farven står for sig selv. Skriver nogen farven direkte
/// på penslen igen, fejler TemafilTest — og det er med vilje, for det er ikke
/// noget, man kan huske om et halvt år.
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

    /// <summary>Navnet på farve-nøglen bag en pensel. «Baggrund» → «BaggrundFarve».</summary>
    public static string Farvenoegle(string paletnoegle) => paletnoegle + "Farve";

    private static void Saet(bool lyst)
    {
        ErLyst = lyst;

        var app = Application.Current;
        if (app is null) return;

        var mangler = new List<string>();

        foreach (var (navn, farve) in Tema.Palet(lyst))
        {
            var noegle = Farvenoegle(navn);

            if (app.Resources[noegle] is not Color)
            {
                mangler.Add(navn);
                continue;
            }

            app.Resources[noegle] = (Color)ColorConverter.ConvertFromString(farve);
        }

#pragma warning disable WPF0001 // ThemeMode er stadig markeret som eksperimentel
        app.ThemeMode = lyst ? ThemeMode.Light : ThemeMode.Dark;
#pragma warning restore WPF0001

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
    /// Kan hver palet-nøgle rent faktisk skifte farve? Til prøverne.
    /// </summary>
    /// <returns>Det, der er galt. Tom liste = alt kan skifte.</returns>
    /// <remarks>
    /// Den prøver TRE ting, og alle tre har fejlet i virkeligheden:
    ///
    ///   1. At penslen findes. En manglende nøgle vælter det vindue, der
    ///      slår den op — set 18-08-2026.
    ///   2. At FARVEN findes ved siden af den. Uden den kan penslen ikke
    ///      skifte, og temaet bliver hængende.
    ///   3. At penslen ikke er frosset. Det var de alle femogtyve, første
    ///      gang skiftet blev prøvet, fordi WPF fryser XAML-resurser af sig
    ///      selv.
    /// </remarks>
    public static IReadOnlyList<string> Efterse(ResourceDictionary ordbog)
    {
        var galt = new List<string>();

        foreach (var navn in Tema.Nøglerne)
        {
            if (ordbog[navn] is not SolidColorBrush p)
            {
                galt.Add($"{navn}: findes ikke som pensel");
                continue;
            }

            if (p.IsFrozen) galt.Add($"{navn}: er frosset og kan ikke skifte farve");
            if (ordbog[Farvenoegle(navn)] is not Color) galt.Add($"{navn}: mangler {Farvenoegle(navn)}");
        }

        return galt;
    }
}
