using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NoteApp.Desktop.Dialogs;

/// <summary>Hvilken slags besked — styrer kun farvestriben øverst.</summary>
public enum Slags
{
    /// <summary>Et valg. Intet er gået galt.</summary>
    Valg,

    /// <summary>Noget kan få følger, man ikke havde regnet med.</summary>
    Pas_paa,

    /// <summary>Noget gik galt.</summary>
    Fejl,

    /// <summary>Noget lykkedes.</summary>
    Godt
}

/// <summary>
/// Appens egen dialog.
///
/// HVORFOR IKKE MESSAGEBOX
///
/// Windows' MessageBox tegner en grå kasse med et gult trekant-ikon og en
/// systemlyd. Den ligner en kritisk fejl — også når beskeden bare er et valg,
/// man skal træffe. Den ser ud som om den kommer fra et andet program, fordi
/// den gør det, og på en mørk skærm springer den i øjnene som noget, der er
/// gået i stykker.
///
/// Her er farvestriben øverst hele signalet, og knapperne siger, hvad de gør,
/// frem for «Ja» og «Nej». En knap, der hedder «Ja», tvinger en til at læse
/// spørgsmålet igen for at finde ud af, hvad man siger ja til.
/// </summary>
public partial class AppDialog : Window
{
    private AppDialog() => InitializeComponent();

    /// <summary>Blev den tredje knap trykket? Kun relevant, når der er en.</summary>
    public bool TredjeValgt { get; private set; }

    /// <summary>
    /// Viser en dialog og venter på svaret.
    ///
    /// <paramref name="godkend"/> og <paramref name="annuller"/> skal sige,
    /// hvad der SKER — «Gem reglen», ikke «Ja». <paramref name="tredje"/> er
    /// en valgfri tredje vej ud.
    /// </summary>
    public static bool Spoerg(
        Window? ejer,
        string overskrift,
        string tekst,
        string godkend,
        string annuller = "Annullér",
        Slags slags = Slags.Valg,
        bool godkendErStandard = true,
        string? tredje = null)
    {
        var d = Byg(ejer, overskrift, tekst, godkend, annuller, slags, tredje);

        // Er handlingen den, der kan fortrydes mindst, skal fokus IKKE ligge
        // paa den. Et tryk paa mellemrum maa ikke kunne gemme en regel, der
        // gaelder alle fremtidige moeder.
        d.Loaded += (_, _) =>
        {
            if (godkendErStandard) d.GodkendKnap.Focus();
            else d.AnnullerKnap.Focus();
        };

        return d.ShowDialog() == true;
    }

    /// <summary>Samme dialog med kun én knap — når der ikke er noget at vælge.</summary>
    public static void Vis(Window? ejer, string overskrift, string tekst,
                           Slags slags = Slags.Valg, string knap = "OK")
    {
        var d = Byg(ejer, overskrift, tekst, knap, annuller: null, slags, tredje: null);
        d.AnnullerKnap.Visibility = Visibility.Collapsed;
        d.Loaded += (_, _) => d.GodkendKnap.Focus();
        d.ShowDialog();
    }

    /// <summary>
    /// Spørger med tre veje ud. Returnerer 0 for godkend, 1 for den tredje,
    /// og -1 hvis der blev annulleret.
    /// </summary>
    public static int SpoergTre(Window? ejer, string overskrift, string tekst,
                                string godkend, string tredje, string annuller = "Annullér",
                                Slags slags = Slags.Valg, bool godkendErStandard = false,
                                IReadOnlyList<(string Tekst, bool ErFejl)>? grundlag = null,
                                string grundlagKnap = "Vis alle fejl")
    {
        var d = Byg(ejer, overskrift, tekst, godkend, annuller, slags, tredje);

        if (grundlag is { Count: > 0 })
        {
            d._grundlag = grundlag;
            d.ListeKnap.Content = grundlagKnap;
            d.ListeKnap.Visibility = Visibility.Visible;
        }

        d.Loaded += (_, _) =>
        {
            if (godkendErStandard) d.GodkendKnap.Focus();
            else d.TredjeKnap.Focus();
        };

        var ok = d.ShowDialog() == true;

        if (d.TredjeValgt) return 1;
        return ok ? 0 : -1;
    }

    private IReadOnlyList<(string Tekst, bool ErFejl)>? _grundlag;

    /// <summary>
    /// Folder grundlaget ud: én linje pr. sted, ordet står, med besked om det
    /// var hørt forkert eller rigtigt.
    ///
    /// Det er dét, valget skal træffes på. «43 gange, 3 forkerte» siger, at
    /// der ER et valg, men ikke hvad det er — gentager den samme fejl sig, er
    /// en regel rigtig; er de tre spredt blandt fyrre rigtige, er den forkert.
    /// </summary>
    private void Liste_Click(object sender, RoutedEventArgs e)
    {
        if (_grundlag is null) return;

        if (ListePanel.Visibility == Visibility.Visible)
        {
            ListePanel.Visibility = Visibility.Collapsed;
            ListeKnap.Content = "Vis alle fejl";
            return;
        }

        if (Liste.Items.Count == 0)
        {
            foreach (var (t, erFejl) in _grundlag) Liste.Items.Add(Linje(t, erFejl));
        }

        ListePanel.Visibility = Visibility.Visible;
        ListeKnap.Content = "Skjul";
    }

    private UIElement Linje(string tekst, bool erFejl)
    {
        var g = new System.Windows.Controls.Grid { Margin = new Thickness(10, 5, 10, 5) };
        g.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
        {
            Width = new GridLength(96)
        });
        g.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());

        var maerkat = new System.Windows.Controls.TextBlock
        {
            Text = erFejl ? "hørt forkert" : "rigtigt",
            FontSize = 11,
            FontWeight = erFejl ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Top,
            Foreground = (Brush)FindResource(erFejl ? "Advarsel" : "Godkendt")
        };
        System.Windows.Controls.Grid.SetColumn(maerkat, 0);
        g.Children.Add(maerkat);

        var linje = new System.Windows.Controls.TextBlock
        {
            Text = tekst,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18,
            Foreground = (Brush)FindResource(erFejl ? "Tekst" : "TekstMeget")
        };
        System.Windows.Controls.Grid.SetColumn(linje, 1);
        g.Children.Add(linje);

        return g;
    }

    private static AppDialog Byg(Window? ejer, string overskrift, string tekst,
                                 string godkend, string? annuller, Slags slags, string? tredje)
    {
        var d = new AppDialog { Owner = ejer };

        d.Overskrift.Text = overskrift;
        d.Brodtekst.Text = tekst;
        d.GodkendKnap.Content = godkend;

        if (annuller is not null) d.AnnullerKnap.Content = annuller;

        if (tredje is not null)
        {
            d.TredjeKnap.Content = tredje;
            d.TredjeKnap.Visibility = Visibility.Visible;
        }

        d.Stribe.Background = (Brush)d.FindResource(slags switch
        {
            Slags.Pas_paa => "Advarsel",
            Slags.Fejl => "Optager",
            Slags.Godt => "Godkendt",
            _ => "Accent"
        });

        // Vinduet har ingen titellinje at traekke i. Uden det her kan dialogen
        // ikke flyttes, og staar den hen over det, man skal se for at kunne
        // svare, er der ingen vej udenom.
        d.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) d.DragMove();
        };

        return d;
    }

    private void Godkend_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Tredje_Click(object sender, RoutedEventArgs e)
    {
        TredjeValgt = true;
        DialogResult = false;
    }
}
