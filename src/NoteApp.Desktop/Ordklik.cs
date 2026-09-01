using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace NoteApp.Desktop;

/// <summary>
/// Gør hvert ord i en tekst klikbart.
/// </summary>
/// <remarks>
/// FORDI MAN OPDAGER FEJLEN I TEKSTEN, IKKE I ORDBOGEN.
///
/// Et forkert hørt ord ses dér, hvor det står — i noten, lige efter man har
/// dikteret. Skal man huske stavemåden, gå til ordbogen og skrive den ind,
/// bliver det ikke gjort. Brugerens ord 31-08-2026: «jeg burde kunne klikke
/// på Storistech og tilføje det til StorageTek».
///
/// HVORFOR HYPERLINK OG IKKE KNAPPER. Teksten skal stadig læses som tekst:
/// den skal bryde over linjer, have sin egen linjeafstand og se ud som en
/// note. En række knapper ville være en liste af ord, ikke en sætning. Et
/// Hyperlink er et element INDE i teksten og flyder med den.
///
/// De ser ikke ud som links. Ingen understregning, ingen farve — kun en
/// markering, når musen er over ordet. Ellers ville en note ligne en
/// hjemmeside fra 1998, og det er ikke det, man skal lægge mærke til.
/// </remarks>
public static class Ordklik
{
    /// <summary>Teksten, der skal deles op i klikbare ord.</summary>
    public static readonly DependencyProperty TekstProperty =
        DependencyProperty.RegisterAttached(
            "Tekst", typeof(string), typeof(Ordklik),
            new PropertyMetadata(null, Skiftet));

    public static void SetTekst(DependencyObject d, string? v) => d.SetValue(TekstProperty, v);
    public static string? GetTekst(DependencyObject d) => (string?)d.GetValue(TekstProperty);

    /// <summary>Kaldes med det ord, der blev klikket på.</summary>
    public static Action<string>? Klikket { get; set; }

    private static void Skiftet(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock blok) return;

        blok.Inlines.Clear();

        var tekst = e.NewValue as string;
        if (string.IsNullOrEmpty(tekst)) return;

        // Der deles paa mellemrum og ikke paa ordtegn. Linjeskift og dobbelte
        // mellemrum skal blive staaende, ellers falder noten sammen til én
        // stribe ord.
        var fra = 0;

        while (fra < tekst.Length)
        {
            var til = tekst.IndexOf(' ', fra);
            if (til < 0) til = tekst.Length;

            var stykke = tekst[fra..til];

            if (stykke.Length > 0) blok.Inlines.Add(Ord(stykke));
            if (til < tekst.Length) blok.Inlines.Add(new Run(" "));

            fra = til + 1;
        }
    }

    /// <summary>
    /// Ét ord, klikbart.
    /// </summary>
    /// <remarks>
    /// TEGNSAETNINGEN FOELGER MED PAA SKAERMEN og skaeres foerst fra, naar
    /// ordet gives videre. «Storistech,» skal staa med sit komma i noten - det
    /// er jo saadan, saetningen ser ud - men rettelsen handler om ordet.
    /// </remarks>
    private static Inline Ord(string stykke)
    {
        var rent = stykke.Trim(',', '.', '!', '?', ':', ';', '(', ')', '«', '»', '"');

        // Er der ikke et ord tilbage, er det tegnsaetning alene. Den skal ikke
        // kunne klikkes paa.
        if (rent.Length == 0 || !rent.Any(char.IsLetter)) return new Run(stykke);

        var link = new Hyperlink(new Run(stykke))
        {
            TextDecorations = null,
            Foreground = null,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = Core.Sprog.T("noter.ret_ordet_hjaelp", rent),
        };

        link.Click += (_, _) => Klikket?.Invoke(rent);

        return link;
    }
}
