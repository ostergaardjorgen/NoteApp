using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace NoteApp.Desktop;

/// <summary>
/// Farver Windows' egen titellinje, så den hører til resten af appen.
///
/// HVORFOR DEN FINDES
///
/// Appen er mørk hele vejen igennem — og oven på hvert eneste vindue sad en
/// lys grå bjælke, Windows selv tegner. Den er det første, øjet møder, og den
/// hørte ikke til noget. Det gjaldt både hovedvinduet og hver eneste dialog.
///
/// HVORFOR IKKE EN EGEN TITELLINJE
///
/// WPF kan tegne sin egen med WindowChrome, og så er man fri. Prisen er høj:
/// man arver ansvaret for at trække vinduet, dobbeltklik-maksimering,
/// Windows-snap, knappernes egne svæve-farver, berøringsskærme og
/// højkontrast-tilstand. Alt sammen ting, der virker i forvejen, og som er
/// nemme at komme til at ødelægge halvt.
///
/// Windows kan farve sin EGEN bjælke, og så bliver den ved med at være
/// Windows' bjælke. Det er den samme afvejning som alle andre steder i appen:
/// brug det, styresystemet allerede kan, frem for at bygge en kopi.
///
/// TO VEJE, OG DEN ENE ER EN RESERVE
///
///   DWMWA_CAPTION_COLOR (35)   sætter en præcis farve. Windows 11, build
///                              22000 og frem. Det er den, der giver den
///                              rigtige farve — appens egen, ikke «en mørk».
///
///   DWMWA_USE_IMMERSIVE_DARK_MODE (20)  gør bjælken mørk i Windows' egen
///                              nuance. Ældre Windows 11 og sene Windows 10.
///
/// Kan ingen af dem, sker der ingenting, og bjælken er grå som før. Et
/// farvevalg må aldrig kunne forhindre et vindue i at åbne.
/// </summary>
public static class Vinduesramme
{
    private const int Moerk = 20;      // DWMWA_USE_IMMERSIVE_DARK_MODE
    private const int Kant = 34;       // DWMWA_BORDER_COLOR
    private const int Bjaelke = 35;    // DWMWA_CAPTION_COLOR
    private const int Skrift = 36;     // DWMWA_TEXT_COLOR

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr vindue, int attribut,
                                                    ref int vaerdi, int stoerrelse);

    /// <summary>
    /// Slår farvningen til for ALLE vinduer i appen — også dem, der endnu ikke
    /// findes.
    ///
    /// Kaldes én gang ved opstart. Alternativet var en linje i hver eneste
    /// vinduesklasse, og så er det et spørgsmål om tid, før nogen glemmer den
    /// i en ny dialog — og så er dét ene vindue lyst.
    ///
    /// Der hænges på Loaded og ikke SourceInitialized: SourceInitialized er
    /// ikke en routed event og kan derfor ikke fanges for en hel klasse. Ved
    /// Loaded findes vinduets håndtag, og det er alt, DWM skal bruge.
    /// </summary>
    public static void SlaaTil()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((afsender, _) =>
            {
                if (afsender is Window v) Farv(v);
            }));

        // ET TEMASKIFT SKAL OGSAA RAMME DE VINDUER, DER ALLEREDE ER AABNE.
        // Loaded er sket for laenge siden paa hovedvinduet, saa uden det her
        // bliver titellinjen staaende i den gamle farve, indtil appen
        // genstartes - og det ligner en fejl, fordi det ER en.
        Temaskift.Skiftet += FarvAlle;
    }

    /// <summary>Farver hvert åbent vindue om. Kaldes efter et temaskift.</summary>
    public static void FarvAlle()
    {
        try
        {
            foreach (Window v in Application.Current.Windows) Farv(v);
        }
        catch (Exception)
        {
            // Samme regel som i Farv: en farve maa ikke vaelte noget.
        }
    }

    /// <summary>
    /// Farver ét vindue. Fejler den, bliver bjælken grå — og intet andet sker.
    /// </summary>
    public static void Farv(Window vindue)
    {
        try
        {
            var haandtag = new WindowInteropHelper(vindue).Handle;
            if (haandtag == IntPtr.Zero) return;

            // Moerk tilstand foerst. Virker den alene, er bjaelken allerede
            // maerkbart bedre end den graa - ogsaa hvis de tre farver nedenfor
            // ikke slaar igennem paa den her udgave af Windows.
            //
            // DEN FOELGER TEMAET. Her stod «var til = 1» fast, fordi appen kun
            // fandtes i én udgave. Blev den staaende, ville et lyst tema faa
            // en moerk titellinje paa aeldre Windows, hvor de tre farver
            // nedenfor ikke slaar igennem - og saa er reserven vaerre end
            // ingenting.
            var til = Temaskift.ErLyst ? 0 : 1;
            DwmSetWindowAttribute(haandtag, Moerk, ref til, sizeof(int));

            var bjaelke = Farve("Baggrund", 0x14161A);
            var kant = Farve("PanelKant", 0x39404E);
            var skrift = Farve("Tekst", 0xF4F6FA);

            DwmSetWindowAttribute(haandtag, Bjaelke, ref bjaelke, sizeof(int));
            DwmSetWindowAttribute(haandtag, Kant, ref kant, sizeof(int));
            DwmSetWindowAttribute(haandtag, Skrift, ref skrift, sizeof(int));
        }
        catch (Exception)
        {
            // DWM findes ikke, eller udgaven kender ikke attributterne. Saa er
            // bjaelken graa, og det er en skoenhedsfejl - ikke noget, der maa
            // staa i vejen for et vindue.
        }
    }

    /// <summary>
    /// Farven fra appens palet, oversat til det, DWM vil have.
    ///
    /// DWM bruger COLORREF: 0x00BBGGRR. Rød og blå bytter altså plads i
    /// forhold til den #RRGGBB, der står i App.xaml. Byttes de ikke, bliver
    /// bjælken blå på en app, der er næsten sort — og fejlen ser ud som et
    /// bevidst valg.
    ///
    /// <paramref name="reserve"/> bruges, hvis nøglen mangler. En manglende
    /// farve er ikke værd at vælte et vindue for.
    /// </summary>
    private static int Farve(string noegle, int reserve)
    {
        var rgb = reserve;

        try
        {
            if (Application.Current?.TryFindResource(noegle) is SolidColorBrush p)
                rgb = (p.Color.R << 16) | (p.Color.G << 8) | p.Color.B;
        }
        catch (Exception)
        {
            // Reserven staar allerede.
        }

        return ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);
    }
}
