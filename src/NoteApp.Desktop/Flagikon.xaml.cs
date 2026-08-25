using System.Windows;
using System.Windows.Controls;

namespace NoteApp.Desktop;

/// <summary>
/// Et lille flag, valgt ud fra en landekode.
///
/// Koden kommer fra sprogfilens <c>_sprog.flag</c>, så et sprog selv siger,
/// hvilket flag det hører til. Kender ikke appen koden, vises koden i en
/// kasse — se forklaringen i XAML'en.
/// </summary>
public partial class Flagikon : UserControl
{
    public Flagikon()
    {
        InitializeComponent();
        Vis();
    }

    public static readonly DependencyProperty KodeProperty =
        DependencyProperty.Register(nameof(Kode), typeof(string), typeof(Flagikon),
            new PropertyMetadata("", (d, _) => ((Flagikon)d).Vis()));

    /// <summary>Landekoden: «DK», «GB», «DE» …</summary>
    public string Kode
    {
        get => (string)GetValue(KodeProperty);
        set => SetValue(KodeProperty, value);
    }

    private void Vis()
    {
        var kode = (Kode ?? "").Trim().ToUpperInvariant();

        Dansk.Visibility = Visibility.Collapsed;
        Britisk.Visibility = Visibility.Collapsed;
        Kodekasse.Visibility = Visibility.Collapsed;

        switch (kode)
        {
            case "DK":
                Dansk.Visibility = Visibility.Visible;
                break;

            // GB er den rigtige landekode; UK og EN er de to, folk skriver i
            // stedet. Alle tre foerer til det samme flag - en sprogfil skal
            // ikke afvises, fordi nogen skrev det almindelige frem for det
            // formelt korrekte.
            case "GB":
            case "UK":
            case "EN":
                Britisk.Visibility = Visibility.Visible;
                break;

            default:
                // Hoejst to bogstaver. En laengere kode ville sprede kassen ud
                // og goere raekken af sprog ujaevn.
                Kodetekst.Text = kode.Length > 2 ? kode[..2] : kode;
                Kodekasse.Visibility = Visibility.Visible;
                break;
        }
    }
}
