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
            Deling_Primaer.IsChecked = rolle == Maskinrolle.Primaer;
            Deling_Sekundaer.IsChecked = rolle == Maskinrolle.Sekundaer;

            var mappe = Maskinid.Deltmappe;

            Deling_Sti.Text = mappe ?? Sprog.T("settingsview.deling_ingen_mappe");
            Deling_Aabn.IsEnabled = mappe is not null && Directory.Exists(mappe);
            Deling_Fra.IsEnabled = mappe is not null;

            // ADVARSLEN STÅR KUN, NÅR DER ER NOGET AT ADVARE OM. En kasse,
            // der altid er der, bliver til noget, man læser forbi.
            Deling_Advarsel.Visibility = mappe is null ? Visibility.Collapsed : Visibility.Visible;

            Deling_Status.Text = Statuslinje(mappe);

            // HAKKET STAAR KUN FOR DEN SEKUNDAERE. Den primaere skal ikke
            // sende sit arbejde nogen steder - den ER stedet.
            Deling_Automatisk.IsChecked = AppSettings.Current.SendAutomatisk;
            Deling_Automatisk.Visibility = rolle == Maskinrolle.Sekundaer
                ? Visibility.Visible
                : Visibility.Collapsed;

            VisGuide();
            VisKoe();
            VisArkiv();
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

    // =================================================================== vejledningen

    /// <summary>
    /// Trinnene øverst på fanen: hvad er gjort, og hvad er det næste?
    /// </summary>
    /// <remarks>
    /// DEN BYGGES FORFRA HVER GANG. Tilstanden ligger i den fælles mappe og på
    /// den anden computer, ikke her — et kort, der blev sat op én gang og
    /// derefter rettet i, ville før eller siden vise noget, der ikke passer.
    /// </remarks>
    private void VisGuide()
    {
        var trin = Delingsguide.Trin();
        var mangler = trin.Count(t => t.Stand != Trinstand.Klar);

        Deling_Guideoverskrift.Text = Sprog.T(mangler == 0
            ? "settingsview.guide_overskrift_faerdig"
            : "settingsview.guide_overskrift");

        Deling_Guideunder.Text = Sprog.T(mangler == 0
            ? "settingsview.guide_under_faerdig"
            : "settingsview.guide_under");

        Deling_Guide.Children.Clear();

        foreach (var t in trin)
            Deling_Guide.Children.Add(Guidekort(t));

        // TALLET PAA FANEN OG UDE I MENUEN FOELGER DET SAMME. Sker der noget
        // her - en mappe vaelges, en computer godkendes - skal begge to
        // rette sig med det samme og ikke ved naeste opstart.
        DelingMaerkat.Visibility = mangler == 0 ? Visibility.Collapsed : Visibility.Visible;
        DelingMaerkatTal.Text = mangler.ToString();

        (Application.Current.MainWindow as MainWindow)?.OpdaterOpsaetningsmaerkat();
    }

    /// <summary>Ét trin: et tal eller et hak, en overskrift og en forklaring.</summary>
    private UIElement Guidekort(Delingstrin t)
    {
        var (flade, paa, tegn) = t.Stand switch
        {
            // Et hak frem for tallet. Man skal kunne se paa en halv skaerm,
            // hvor langt man er, uden at laese en linje.
            Trinstand.Klar => ("Godkendt", "PaaAccent", "\u2713"),
            Trinstand.Naeste => ("Accent", "PaaAccent", t.Nummer.ToString()),
            _ => ("Svaev", "TekstMeget", t.Nummer.ToString()),
        };

        var g = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var ring = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Margin = new Thickness(0, 1, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = (System.Windows.Media.Brush)FindResource(flade),
            Child = new TextBlock
            {
                Text = tegn,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource(paa),
            },
        };

        Grid.SetColumn(ring, 0);
        g.Children.Add(ring);

        var tekst = new StackPanel();
        Grid.SetColumn(tekst, 1);

        tekst.Children.Add(new TextBlock
        {
            Text = t.Overskrift,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource(
                t.Stand == Trinstand.Venter ? "TekstMeget" : "Tekst"),
        });

        tekst.Children.Add(new TextBlock
        {
            Text = t.Forklaring,
            FontSize = 12.5,
            LineHeight = 20,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = (System.Windows.Media.Brush)FindResource(
                t.Stand == Trinstand.Naeste ? "TekstSvag" : "TekstMeget"),
        });

        g.Children.Add(tekst);

        return g;
    }

    // ===================================================================== koeen

    /// <summary>
    /// Det arbejde, der er undervejs mellem de to computere.
    /// </summary>
    /// <remarks>
    /// TO SLAGS LINJER: det, VI har sendt, og det, vi har taget for en anden.
    /// Uden den anden slags ville den kraftige maskine skrive et fremmed møde
    /// ud i tavshed, mens brugeren sad og undrede sig over, hvorfor
    /// grafikkortet arbejdede.
    /// </remarks>
    private void VisKoe()
    {
        Deling_Koe.Children.Clear();

        var linjer = new List<string>();

        foreach (var min in Arbejdskoe.Mine())
        {
            var navn = Moedenavn(min.Moede);

            var svar = Arbejdskoe.Svar(min.Id);

            linjer.Add(Sprog.T(
                svar is not null ? "deling.koe_faerdig"
                : Taget(min) ? "deling.koe_i_gang"
                : "deling.koe_sendt", navn));
        }

        if (Delt.Mappe is { } delt)
        {
            foreach (var opgave in Andres(delt))
            {
                var fra = Delt.Alle().FirstOrDefault(m => m.Id == opgave.Fra)?.Navn ?? "";

                linjer.Add(Sprog.T("deling.koe_modtaget", fra, opgave.Spor));
            }
        }

        Deling_Koerude.Visibility = linjer.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        foreach (var linje in linjer)
        {
            Deling_Koe.Children.Add(new TextBlock
            {
                Text = linje,
                FontSize = 12.5,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)FindResource("TekstSvag"),
            });
        }
    }

    /// <summary>Er en af vores opgaver taget af den anden computer?</summary>
    private static bool Taget(Arbejdsopgave opgave) =>
        Delt.Mappe is { } delt && Arbejdskoe.Taget(delt, opgave.Id) is not null;

    /// <summary>Det arbejde, DEN HER maskine har taget for en anden.</summary>
    private static IReadOnlyList<Arbejdsopgave> Andres(string delt) =>
        Arbejdskoe.Venter()
                  .Concat(Arbejdskoe.Mine())
                  .Where(o => o.Fra != Maskinid.Id
                              && Arbejdskoe.Taget(delt, o.Id)?.Maskine == Maskinid.Id)
                  .ToList();

    /// <summary>Mødets navn, som brugeren kender det.</summary>
    private static string Moedenavn(string id)
    {
        try
        {
            if (MeetingStore.FindById(id) is { } fundet)
                return fundet.Meta.Title ?? System.IO.Path.GetFileName(fundet.Mappe);
        }
        catch (Exception)
        {
            // Et moede, der er slettet, mens udskriften var undervejs.
        }

        return Sprog.T("faelles.optagelse");
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
            // ============ OPSAETNINGEN KAN SENDES OVER ============
            //
            // Man har lige staaet og sammenlignet seks cifre paa to skaerme
            // for at sige «det er mine to computere». At skulle finde
            // API-noeglen frem OG logge ind hos Google een gang til er en
            // daarlig beloenning for det - se Noegledeling.
            //
            // EEN KNAP OG IKKE TRE. Der er tre slags at sende, og tre knapper
            // ved siden af hinanden ville goere et kort om en computer til en
            // vaerktoejskasse. Dialogen skriver, hvad der er at sende.
            if (Venter(m.Id).Count > 0)
            {
                knapper.Children.Add(Knap(Sprog.T("settingsview.deling_opsaetning_fortryd"),
                    () =>
                    {
                        foreach (var slags in Venter(m.Id)) Noegledeling.Fortryd(m.Id, slags);
                        VisDeling();
                    }));
            }
            else if (Haves().Count > 0)
            {
                knapper.Children.Add(Knap(Sprog.T("settingsview.deling_send_opsaetning"),
                    () => SendOpsaetning(m)));
            }

            knapper.Children.Add(Knap(Sprog.T("settingsview.deling_fjern_parring"),
                () => { Parring.Glem(m.Id); VisDeling(); }, venstremargin: 8));
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
            Text = Sprog.T(m.Rolle == Maskinrolle.Primaer
                       ? "settingsview.deling_rolle_primaer"
                       : "settingsview.deling_rolle_sekundaer")
                   + " · " + Sidst(m),
            FontSize = 11.5,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = (System.Windows.Media.Brush)FindResource("TekstMeget"),
        });

        var (besked, farve) = tilstand switch
        {
            // DEN ANDEN HALVDEL MANGLER. Vi har godkendt den; den har ikke
            // godkendt os. Der kan ikke udveksles noget, og skaermen skal
            // sige hvorfor - ikke bare «godkendt».
            Parringstilstand.Parret when m.HarGodkendt(Maskinid.Id) == false
                => ("settingsview.deling_venter_paa_dem", "Advarsel"),
            Parringstilstand.Parret when Venter(m.Id).Count > 0
                => ("settingsview.deling_noegle_venter", "Advarsel"),
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
    /// Godkender en computer. Selve spørgsmålet står i
    /// <see cref="Deling.Parringsdialog"/>.
    /// </summary>
    /// <remarks>
    /// DET SAMME SPØRGSMÅL BEGGE STEDER. Dialogen kommer også af sig selv på
    /// den maskine, der mangler at sige ja — og to steder med hver sin
    /// ordlyd om den samme sikkerhed er ét sted for meget.
    /// </remarks>
    private void Par(Maskinoplysning m)
    {
        Deling.Parringsdialog.Spoerg(Window.GetWindow(this), m);
        VisDeling();
    }

    /// <summary>Det, den her computer har og kan sende videre.</summary>
    private static List<Kuvertslags> Haves() =>
        Noegledeling.Slags.Where(Noegledeling.Findes).ToList();

    /// <summary>Det, der ligger og venter på at blive hentet af den anden.</summary>
    private static List<Kuvertslags> Venter(string modtagerId) =>
        Noegledeling.Slags.Where(s => Noegledeling.Sendt(modtagerId, s)).ToList();

    private static string Punkt(Kuvertslags slags) => Sprog.T(slags switch
    {
        Kuvertslags.Apinoegle => "settingsview.deling_punkt_api",
        Kuvertslags.Googlekalender => "settingsview.deling_punkt_google",
        _ => "settingsview.deling_punkt_googleopgaver",
    });

    /// <summary>
    /// Sender opsætningen til en godkendt computer.
    /// </summary>
    /// <remarks>
    /// DER SPØRGES FØRST, OG DER STÅR HVAD DER SENDES. En adgang, der flytter
    /// sig, fordi appen syntes det var nemmere, er ikke en bekvemmelighed —
    /// det er en overraskelse. Punkterne skrives ud, så det ikke er
    /// «opsætningen», man siger ja til, men netop de her tre ting.
    ///
    /// DEN ENE MÅ GERNE LYKKES, SELV OM DEN ANDEN FEJLER. Går det galt midt i,
    /// står der i historikken, hvad der nåede af sted — en halv sending, der
    /// meldes som ingenting, er det værste af begge dele.
    /// </remarks>
    private void SendOpsaetning(Maskinoplysning m)
    {
        var kan = Haves();

        if (kan.Count == 0) return;

        var punkter = string.Join("\n", kan.Select(Punkt));

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            Sprog.T("settingsview.deling_send_opsaetning_titel", m.Navn),
            Sprog.T("settingsview.deling_send_opsaetning_tekst", punkter, m.Navn),
            godkend: Sprog.T("settingsview.deling_send_opsaetning_ja"),
            annuller: Sprog.T("faelles.annuller"),
            slags: Dialogs.Slags.Valg, godkendErStandard: false);

        if (!ja) return;

        var sendt = new List<Kuvertslags>();

        try
        {
            foreach (var slags in kan)
            {
                Noegledeling.Send(m, slags);
                sendt.Add(slags);
            }
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                Sprog.T("settingsview.deling_noegle_gik_galt"), ex.Message, Dialogs.Slags.Pas_paa);
        }

        if (sendt.Count > 0)
        {
            Historik.Skriv(HaendelseType.Andet,
                Sprog.T("settingsview.deling_opsaetning_historik", m.Navn),
                string.Join("\n", sendt.Select(Punkt)) + "\n\n"
                + Sprog.T("settingsview.deling_opsaetning_historik_detalje"), Udfald.Fuldført);
        }

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

        Maskinid.Rolle = Deling_Sekundaer.IsChecked == true ? Maskinrolle.Sekundaer : Maskinrolle.Primaer;
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

    private void DelingAutomatisk_Klik(object sender, RoutedEventArgs e)
    {
        if (_delingIndlaeser) return;

        AppSettings.Current.SendAutomatisk = Deling_Automatisk.IsChecked == true;
        AppSettings.Current.Save();
    }

    private void DelingOpdater_Klik(object sender, RoutedEventArgs e)
    {
        Delt.Meld();
        VisDeling();
    }
}
