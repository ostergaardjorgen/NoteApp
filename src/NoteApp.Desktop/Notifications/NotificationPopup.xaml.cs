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

        // En knap, der ikke goer noget, skal ikke se ud, som om den goer.
        AlleLaest.IsEnabled = nye > 0;
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

        // ============ VEJEN HEN TIL DET, BESKEDEN HANDLER OM ============
        //
        // Klokken sagde, AT noget var faerdigt, og saa skulle man selv finde
        // det. Det er en halv besked - og netop den halvdel, der mangler, er
        // grunden til, at man aabner klokken.
        //
        // Historikken og soegningen aabner allerede paa id. Det samme her, og
        // af samme grund: en optagelse kan vaere flyttet og et dokument
        // omdoebt, siden beskeden blev skrevet. Navnet i beskeden er, hvad
        // tingen HED; id'et er, hvad den ER.
        if (Link(h) is { } link) indhold.Children.Add(link);

        var kort = new Border
        {
            Background = (Brush)new BrushConverter().ConvertFrom(ny ? "#FF1D2530" : "#00000000")!,
            BorderBrush = (Brush)FindResource(farve),
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 5),
            Child = indhold
        };

        // ============ EN ULAEST BESKED LAESES VED AT TRYKKE PAA DEN ============
        //
        // Det er den anden af de to veje - den foerste er «Alle laest». At
        // aabne klokken er ikke en af dem: saa forsvandt beskeder, man aldrig
        // naaede at se.
        //
        // Haanden og hjaelpeteksten er der, fordi et kort, der kan trykkes
        // paa, uden at se ud til det, ikke bliver trykket paa.
        if (ny)
        {
            kort.Cursor = System.Windows.Input.Cursors.Hand;
            kort.ToolTip = Sprog.T("notificationpopup.klik_for_laest");

            kort.MouseLeftButtonUp += (_, _) =>
            {
                Notifikationer.MarkerLaest(h);
                Indlaes();
            };
        }

        return kort;
    }

    /// <summary>
    /// Markerer alt som læst.
    /// </summary>
    /// <remarks>
    /// DEN ENESTE VEJ TIL AT RYDDE DEM ALLE PÅ EN GANG. Indtil 28-08-2026
    /// gjorde det at åbne klokken det samme — og så forsvandt tre beskeder,
    /// fordi man kiggede efter den ene, man ventede på.
    /// </remarks>
    private void AlleLaest_Click(object sender, RoutedEventArgs e)
    {
        Notifikationer.MarkerAlleLaest();
        Indlaes();
    }

    /// <summary>
    /// Linket til det, beskeden handler om. Null for beskeder uden en kilde
    /// at gå til — en sikkerhedskopi, en hentning, et manglende trin i
    /// opsætningen.
    /// </summary>
    private Button? Link(Haendelse h)
    {
        if (h.Kilde.Length == 0) return null;

        var tekst = h.Slags switch
        {
            HaendelseType.Dokument => "Vis dokumentet",
            HaendelseType.Optagelse or HaendelseType.Transskription => "Vis optagelsen",
            HaendelseType.Flyttet or HaendelseType.Slettet => null,
            _ => null
        };

        if (tekst is null) return null;

        var knap = new Button
        {
            Content = tekst,
            Tag = h,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = 11.5,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#FF5B9DF0")!,
            Template = Linkskabelon()
        };

        knap.Click += Kilde_Klik;
        return knap;
    }

    /// <summary>
    /// En knap, der ser ud som et link. Hyperlink arver ikke temaets farver
    /// og stod blåt på blåt — samme løsning som i historikken.
    /// </summary>
    private static ControlTemplate Linkskabelon()
    {
        var tekst = new FrameworkElementFactory(typeof(TextBlock));
        tekst.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        tekst.SetBinding(TextBlock.ForegroundProperty,
            new System.Windows.Data.Binding("Foreground") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        tekst.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
        tekst.SetValue(TextBlock.FontSizeProperty, 11.5);

        return new ControlTemplate(typeof(Button)) { VisualTree = tekst };
    }

    private void Kilde_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Haendelse h) return;
        if (Application.Current.MainWindow is not MainWindow hoved) return;

        // Popup'en lukkes foerst. Ellers staar den og daekker det, man netop
        // bad om at se.
        Luk?.Invoke();

        if (h.Slags == HaendelseType.Dokument)
        {
            hoved.GaaTilDokumenter(h.Kilde);
            return;
        }

        if (!hoved.GaaTilOptagelse(h.Kilde))
            Dialogs.AppDialog.Vis(hoved, "Den findes ikke længere",
                "Optagelsen er slettet eller flyttet uden for appen, siden beskeden blev skrevet.",
                Dialogs.Slags.Valg);
    }

    /// <summary>Bedes om at lukke popup'en, når man går et andet sted hen.</summary>
    public Action? Luk { get; set; }

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
