using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.Notifications;

/// <summary>
/// Listen bag klokken: hvad appen har lavet færdigt, siden man sidst kiggede.
///
/// HVORFOR DEN FINDES
///
/// Et referat af et 61-minutters møde tog 43 minutter — målt 14. august 2026.
/// Uden en besked skal man selv huske at gå hen og kigge, og så opdager man
/// det dagen efter. Jobbjælken nederst virker kun, mens appen er fremme.
///
/// HVAD DER STÅR
///
/// Kun det, der er sket, MENS man lavede noget andet: transskriptioner,
/// dokumenter, sikkerhedskopier — og alt, hvad der gik galt. En rettelse, man
/// selv lige har trykket på, står ikke her; man stod og så på det.
/// </summary>
public partial class NotificationPopup : UserControl
{
    /// <summary>Rejses, når der bliver bedt om at se hele historikken.</summary>
    public event Action? HistorikOenskes;

    public NotificationPopup()
    {
        InitializeComponent();
        Indlaes();
    }

    private void Indlaes()
    {
        var poster = Notifikationer.Seneste();
        var nye = poster.Count(Notifikationer.ErNy);

        Under.Text = nye == 0
            ? "Alt er set"
            : nye == 1 ? "1 ny siden sidst" : $"{nye} nye siden sidst";

        Liste.Items.Clear();

        foreach (var h in poster) Liste.Items.Add(Kort(h));

        Tom.Text = poster.Count == 0
            ? "Der er ikke sket noget endnu. Her kommer besked, når en transskription eller et dokument er færdigt."
            : "";
    }

    /// <summary>
    /// Én besked. Nye markeres med en prik i kanten — ikke med en anden
    /// baggrund: læste og ulæste skal kunne stå i samme liste uden at listen
    /// bliver til to lister.
    /// </summary>
    private Border Kort(Haendelse h)
    {
        var ny = Notifikationer.ErNy(h);

        var farve = h.Udfald switch
        {
            Udfald.Fejlet => "Optager",
            Udfald.SeEfter => "Advarsel",
            Udfald.Afbrudt => "TekstMeget",
            _ => "Godkendt"
        };

        var indhold = new StackPanel();

        var top = new Grid();

        top.Children.Add(new TextBlock
        {
            Text = h.Hvad,
            FontSize = 12.5,
            FontWeight = ny ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 62, 0),
            Foreground = (Brush)FindResource(ny ? "Tekst" : "TekstSvag")
        });

        top.Children.Add(new TextBlock
        {
            Text = Naar(h.Tid),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Foreground = (Brush)FindResource("TekstMeget")
        });

        indhold.Children.Add(top);

        if (h.Detaljer.Length > 0)
        {
            indhold.Children.Add(new TextBlock
            {
                Text = h.Detaljer,
                FontSize = 11.5,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17,
                Foreground = (Brush)FindResource("TekstMeget")
            });
        }

        return new Border
        {
            Background = (Brush)new BrushConverter().ConvertFrom(ny ? "#FF1D2530" : "#00000000")!,
            BorderBrush = (Brush)FindResource(farve),
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 5),
            Child = indhold
        };
    }

    /// <summary>
    /// «for 5 min siden» frem for et klokkeslæt. Det er dét, man vil vide om
    /// noget, der lige er sket — klokkeslættet er først interessant dagen efter.
    /// </summary>
    private static string Naar(DateTimeOffset t)
    {
        var siden = DateTimeOffset.Now - t;

        return siden.TotalMinutes < 1 ? "lige nu"
             : siden.TotalMinutes < 60 ? $"{siden.TotalMinutes:0} min siden"
             : siden.TotalHours < 12 ? $"{siden.TotalHours:0} timer siden"
             : t.LocalDateTime.ToString("dd/MM HH:mm");
    }

    private void Historik_Click(object sender, RoutedEventArgs e) => HistorikOenskes?.Invoke();
}
