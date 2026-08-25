using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// Menuen bag flaget: sprogene, der ligger i sprogmappen.
///
/// DEN BYGGES HVER GANG, DEN ÅBNES. Sprogene er filer, brugeren kan lægge i
/// mappen, mens appen kører — og en menu, der blev bygget ved opstart, ville
/// først vise et nyt sprog efter en genstart. Det er billigt: der læses et
/// par små filer.
/// </summary>
public static class Sprogmenu
{
    public static void Vis(FrameworkElement ved)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = ved,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            HorizontalOffset = -8
        };

        var nu = Sprog.Kode;

        foreach (var s in Sprog.Tilgaengelige())
        {
            var raekke = new StackPanel { Orientation = Orientation.Horizontal };

            raekke.Children.Add(new Flagikon
            {
                Kode = s.Flag,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            raekke.Children.Add(new TextBlock
            {
                Text = s.Navn,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = s.Kode == nu ? FontWeights.SemiBold : FontWeights.Normal
            });

            // DET VALGTE SPROG FAAR ET HAK.
            //
            // Uden det skal man laese fedmen paa skriften for at se, hvad der
            // er valgt - og det kan man ikke, naar der kun er to punkter.
            if (s.Kode == nu)
            {
                raekke.Children.Add(new TextBlock
                {
                    Text = "",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 12,
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources["Accent"]
                });
            }

            var punkt = new MenuItem { Header = raekke, Tag = s.Kode };
            punkt.Click += (_, _) => Sprog.Skift(s.Kode);

            menu.Items.Add(punkt);
        }

        // ET SPROG MERE ER EEN FIL. Linjen staar der, saa den, der undrer sig
        // over kun at se to sprog, faar svaret paa stedet i stedet for at
        // lede i en vejledning.
        menu.Items.Add(new Separator());

        var aabn = new MenuItem { Header = Sprog.T("sprog.aabnmappe") };
        aabn.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(Sprog.Mappe);
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(Sprog.Mappe) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Dialogs.AppDialog.Vis(Window.GetWindow(ved), Sprog.T("sprog.kunneikkeaabne"),
                    ex.Message, Dialogs.Slags.Pas_paa);
            }
        };

        menu.Items.Add(aabn);
        menu.IsOpen = true;
    }
}
