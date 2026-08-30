using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Diktering;

/// <summary>
/// De dikteringer, brugeren har valgt at gemme.
/// </summary>
/// <remarks>
/// FANEN STÅR FØRST, fordi det er den, man kommer for. De tre andre er
/// opsætning — noget man gør én gang og sjældent igen.
/// </remarks>
public partial class NoterView : UserControl
{
    /// <summary>Kaldes, når der er kommet eller forsvundet en note.</summary>
    public static Action? Aendret;

    private sealed record Visning(string Naar, string Tekst, Diktatnote Note);

    public NoterView()
    {
        InitializeComponent();

        // Samme regel som i proeverummet: det afgoeres af, om fanen kan SES.
        // «Loaded» daekker ogsaa en fane, man er klikket vaek fra.
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) { Aendret = null; return; }

            Vis();

            // Gemmes en note fra baandet, mens fanen er fremme, skal den dukke
            // op uden at man skal klikke rundt for at faa den frem.
            Aendret = () => Dispatcher.BeginInvoke(new Action(Vis));
        };

        Loaded += (_, _) => Vis();
        Unloaded += (_, _) => Aendret = null;
    }

    private void Vis()
    {
        var noter = Diktatnoter.Laes();

        Liste.ItemsSource = noter
            .Select(n => new Visning(Naar(n.Tid), n.Tekst, n))
            .ToList();

        Tom.Visibility = noter.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RydKnap.Visibility = noter.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        Antal.Text = noter.Count switch
        {
            0 => "",
            1 => "1 note",
            _ => $"{noter.Count} noter",
        };
    }

    /// <summary>
    /// Tidspunktet, som man tænker på det.
    /// </summary>
    /// <remarks>
    /// «I dag 14:32» siger mere end en dato, når noten er fra for en time
    /// siden — og det er de fleste af dem.
    /// </remarks>
    private static string Naar(DateTime t)
    {
        var d = DateTime.Now.Date - t.Date;

        return d.Days switch
        {
            0 => $"I dag {t:HH:mm}",
            1 => $"I går {t:HH:mm}",
            _ => t.ToString("d. MMMM HH:mm"),
        };
    }

    private void Kopier_Klik(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Visning v) return;

        try
        {
            Clipboard.SetText(v.Tekst);
            Antal.Text = "Kopieret — sæt ind med Ctrl+V eller Shift+Insert";
        }
        catch (Exception)
        {
            // Udklipsholderen kan vaere laast af et andet program et oejeblik.
            Antal.Text = "Kunne ikke kopiere lige nu — prøv igen";
        }
    }

    private void Slet_Klik(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Visning v) return;

        Diktatnoter.Slet(v.Note);
        Vis();
    }

    private void Ryd_Klik(object sender, RoutedEventArgs e)
    {
        var svar = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            "Slet alle noter?",
            "Alle gemte dikteringer fjernes. Det kan ikke fortrydes.",
            "Slet alle", "Behold", Dialogs.Slags.Pas_paa);

        if (!svar) return;

        Diktatnoter.Ryd();
        Vis();
    }
}
