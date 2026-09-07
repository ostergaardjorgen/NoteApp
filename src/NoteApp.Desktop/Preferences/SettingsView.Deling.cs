using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;
using NoteApp.Core.Deling;

namespace NoteApp.Desktop.Preferences;

/// <summary>
/// Delingsfanen: den her PC, den fælles mappe og de maskiner, den deler med.
/// </summary>
/// <remarks>
/// ============ HVAD BRUGEREN SKAL FORSTÅ, OG INTET MERE ============
///
/// To ting: hvad den her computer laver (skriver ud, eller sender videre), og
/// hvor de to mødes. Resten sker af sig selv.
///
/// ============ PARRINGEN ER ET MENNESKE, DER SAMMENLIGNER ============
///
/// Nøglerne udveksles gennem mappen, og den, der kan skrive i mappen, kan
/// bytte en nøgle ud og sætte sig i midten. Derfor vises seks cifre, der er
/// regnet ud af BEGGE nøgler: står der det samme tal på de to skærme, er det
/// de rigtige nøgler. Det er dét, en maskine ikke kan gøre for et menneske.
///
/// Se <see cref="Parring"/> for, hvad det beskytter mod — og hvad det ikke
/// gør.
/// </remarks>
public partial class SettingsView
{
    private bool _delingIndlaeser;

    /// <summary>Fylder hele fanen. Kaldes ved opstart og efter hver ændring.</summary>
    private void VisDeling()
    {
        _delingIndlaeser = true;

        try
        {
            Deling_Navn.Text = Maskinid.Navn;

            var rolle = Maskinid.Rolle;
            Deling_Arbejdsstation.IsChecked = rolle == Maskinrolle.Arbejdsstation;
            Deling_Let.IsChecked = rolle == Maskinrolle.Let;

            var mappe = Maskinid.Deltmappe;

            Deling_Sti.Text = mappe ?? Sprog.T("settingsview.deling_ingen_mappe");
            Deling_Aabn.IsEnabled = mappe is not null && Directory.Exists(mappe);
            Deling_Fra.IsEnabled = mappe is not null;

            // ADVARSLEN STÅR KUN, NÅR DER ER NOGET AT ADVARE OM. En kasse,
            // der altid er der, bliver til noget, man læser forbi.
            Deling_Advarsel.Visibility = mappe is null ? Visibility.Collapsed : Visibility.Visible;

            Deling_Status.Text = Statuslinje(mappe);

            VisMaskiner();
        }
        finally
        {
            _delingIndlaeser = false;
        }
    }

    private static string Statuslinje(string? mappe)
    {
        if (mappe is null) return Sprog.T("settingsview.deling_ingen_mappe_hjaelp");

        if (!Directory.Exists(mappe)) return Sprog.T("settingsview.deling_mappe_vaek");

        var oplysning = Delt.Oplysning(mappe);

        return oplysning is null
            ? Sprog.T("settingsview.deling_ikke_klargjort")
            : Sprog.T("settingsview.deling_klar", oplysning.Navn,
                      oplysning.Oprettet.ToLocalTime().ToString("d. MMMM yyyy", Sprog.Kultur));
    }

    // ================================================================== maskinerne

    private void VisMaskiner()
    {
        Deling_Liste.Children.Clear();

        var andre = Delt.Mappe is null ? Array.Empty<Maskinoplysning>() : Delt.Andre().ToArray();

        Deling_Ingen.Visibility = andre.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var m in andre)
            Deling_Liste.Children.Add(Maskinekort(m));
    }

    /// <summary>Ét kort pr. maskine: hvem den er, hvornår den sidst var her, og hvad man kan gøre.</summary>
    private Border Maskinekort(Maskinoplysning m)
    {
        var tilstand = Parring.Tilstand(m);

        var rude = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("Trykket"),
            BorderBrush = (System.Windows.Media.Brush)FindResource(
                tilstand == Parringstilstand.Nyngle ? "FejlTekst" : "PanelKant"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 8, 0, 0),
        };

        var raekke = new DockPanel();

        // ---------- knapperne til højre ----------
        var knapper = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(knapper, Dock.Right);

        if (tilstand == Parringstilstand.Parret)
        {
            knapper.Children.Add(Knap(Sprog.T("settingsview.deling_fjern_parring"),
                () => { Parring.Glem(m.Id); VisDeling(); }));
        }
        else
        {
            var par = Knap(Sprog.T("settingsview.deling_par"), () => Par(m));
            par.Style = (Style)FindResource("Primaer");
            knapper.Children.Add(par);
        }

        knapper.Children.Add(Knap(Sprog.T("settingsview.deling_fjern_maskine"), () => Fjern(m),
                                  venstremargin: 8));

        raekke.Children.Add(knapper);

        // ---------- navn og tilstand til venstre ----------
        var tekst = new StackPanel();

        tekst.Children.Add(new TextBlock
        {
            Text = m.Navn,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
        });

        tekst.Children.Add(new TextBlock
        {
            Text = Sprog.T(m.Rolle == Maskinrolle.Arbejdsstation
                       ? "settingsview.deling_rolle_arbejdsstation"
                       : "settingsview.deling_rolle_let")
                   + " · " + Sidst(m),
            FontSize = 11.5,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = (System.Windows.Media.Brush)FindResource("TekstMeget"),
        });

        var (besked, farve) = tilstand switch
        {
            Parringstilstand.Parret => ("settingsview.deling_er_parret", "Godkendt"),
            Parringstilstand.Nyngle => ("settingsview.deling_ny_noegle", "FejlTekst"),
            _ => ("settingsview.deling_ikke_parret", "Advarsel"),
        };

        tekst.Children.Add(new TextBlock
        {
            Text = Sprog.T(besked),
            FontSize = 11.5,
            Margin = new Thickness(0, 5, 12, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource(farve),
        });

        raekke.Children.Add(tekst);
        rude.Child = raekke;

        return rude;
    }

    private static string Sidst(Maskinoplysning m)
    {
        if (m.SidstSet == default) return Sprog.T("settingsview.deling_aldrig_set");

        if (m.ILive) return Sprog.T("settingsview.deling_lige_nu");

        var tid = m.SidstSet.ToLocalTime();

        return Sprog.T("settingsview.deling_sidst_set",
            tid.ToString("d. MMM 'kl.' HH:mm", Sprog.Kultur));
    }

    private Button Knap(string tekst, Action klik, double venstremargin = 0)
    {
        var b = new Button
        {
            Content = tekst,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(venstremargin, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        b.Click += (_, _) => klik();
        return b;
    }

    // ================================================================== parringen

    /// <summary>
    /// Viser koden og spørger, om den står ens på begge skærme.
    /// </summary>
    /// <remarks>
    /// SPØRGSMÅLET SKAL VÆRE DET RIGTIGE. «Vil du parre?» er noget, man
    /// trykker ja til. «Står der 412 908 på den anden skærm?» er noget, man
    /// bliver nødt til at se efter — og det er hele beskyttelsen.
    /// </remarks>
    private void Par(Maskinoplysning m)
    {
        if (m.Noegle.Length == 0)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                Sprog.T("settingsview.deling_mangler_noegle_titel"),
                Sprog.T("settingsview.deling_mangler_noegle"), Dialogs.Slags.Pas_paa);
            return;
        }

        string kode;

        try
        {
            kode = Parring.Kodevisning(Maskinid.Offentlignoegle(), m.Noegle);
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                Sprog.T("settingsview.deling_mangler_noegle_titel"), ex.Message, Dialogs.Slags.Fejl);
            return;
        }

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            Sprog.T("settingsview.deling_par_titel", m.Navn),
            Sprog.T("settingsview.deling_par_tekst", kode, m.Navn),
            godkend: Sprog.T("settingsview.deling_par_ja"),
            annuller: Sprog.T("settingsview.deling_par_nej"),
            slags: Dialogs.Slags.Valg, godkendErStandard: false);

        if (!ja) return;

        Parring.Betro(m);

        Historik.Skriv(HaendelseType.Andet, Sprog.T("settingsview.deling_historik_parret", m.Navn),
            Sprog.T("settingsview.deling_historik_parret_detalje", kode), Udfald.Fuldført);

        VisDeling();
    }

    private void Fjern(Maskinoplysning m)
    {
        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            Sprog.T("settingsview.deling_fjern_titel", m.Navn),
            Sprog.T("settingsview.deling_fjern_tekst"),
            godkend: Sprog.T("settingsview.deling_fjern_ja"),
            annuller: Sprog.T("faelles.annuller"),
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        Delt.Fjern(m.Id);
        VisDeling();
    }

    // =================================================================== knapperne

    private void DelingNavn_Aendret(object sender, RoutedEventArgs e)
    {
        if (_delingIndlaeser) return;

        Maskinid.Navn = Deling_Navn.Text;
        Delt.Meld();
        VisDeling();
    }

    private void Delingsrolle_Aendret(object sender, RoutedEventArgs e)
    {
        if (_delingIndlaeser) return;

        Maskinid.Rolle = Deling_Let.IsChecked == true ? Maskinrolle.Let : Maskinrolle.Arbejdsstation;
        Delt.Meld();
    }

    private void DelingMappe_Klik(object sender, RoutedEventArgs e)
    {
        var valgt = VaelgMappe(Sprog.T("settingsview.deling_vaelg_titel"),
                               Maskinid.Deltmappe ?? UserDataPaths.Root);

        if (valgt is null) return;

        // ============ EN NY DELING OPRETTES KUN MED ET JA ============
        //
        // Peger man paa den mappe, den anden maskine allerede har klargjort,
        // skal der ikke spoerges om noget - man skal bare med. Er mappen tom,
        // er det den FOERSTE maskine, og saa er der et valg at traeffe.
        if (!Delt.Er(valgt))
        {
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                Sprog.T("settingsview.deling_opret_titel"),
                Sprog.T("settingsview.deling_opret_tekst", valgt),
                godkend: Sprog.T("settingsview.deling_opret_ja"),
                annuller: Sprog.T("faelles.annuller"),
                slags: Dialogs.Slags.Valg);

            if (!ja) return;
        }

        try
        {
            Delt.Klargoer(valgt);
            Maskinid.Deltmappe = valgt;

            if (!Delt.Meld())
            {
                Dialogs.AppDialog.Vis(Window.GetWindow(this),
                    Sprog.T("settingsview.deling_kunne_ikke_titel"),
                    Sprog.T("settingsview.deling_kunne_ikke"), Dialogs.Slags.Pas_paa);
            }

            Historik.Skriv(HaendelseType.Andet, Sprog.T("settingsview.deling_historik_mappe"),
                valgt, Udfald.Fuldført);
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                Sprog.T("settingsview.deling_kunne_ikke_titel"), ex.Message, Dialogs.Slags.Fejl);
        }

        VisDeling();
    }

    private void DelingAabn_Klik(object sender, RoutedEventArgs e)
    {
        if (Maskinid.Deltmappe is { } m) Aabn(m);
    }

    private void DelingFra_Klik(object sender, RoutedEventArgs e)
    {
        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            Sprog.T("settingsview.deling_fra_titel"),
            Sprog.T("settingsview.deling_fra_tekst"),
            godkend: Sprog.T("settingsview.deling_fra_ja"),
            annuller: Sprog.T("faelles.annuller"),
            slags: Dialogs.Slags.Valg, godkendErStandard: false);

        if (!ja) return;

        Maskinid.Deltmappe = null;
        VisDeling();
    }

    private void DelingOpdater_Klik(object sender, RoutedEventArgs e)
    {
        Delt.Meld();
        VisDeling();
    }
}
