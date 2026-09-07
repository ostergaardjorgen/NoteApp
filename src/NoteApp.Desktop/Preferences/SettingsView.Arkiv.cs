using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NoteApp.Core;
using NoteApp.Core.Deling;

namespace NoteApp.Desktop.Preferences;

/// <summary>
/// Arkivruden på delingsfanen.
/// </summary>
/// <remarks>
/// ============ DET, DER SKAL KUNNE LÆSES HER ============
///
/// Tre ting, og de er de eneste, der betyder noget for den, der kigger:
///
///   ER MIN HISTORIK I SIKKERHED? Ét tal og én dato. «1.877 MB, sidst i går kl.
///   22.14» kan man handle på. «Arkivering aktiveret» kan man ikke.
///
///   HVAD MANGLER AT KOMME OP? Står der 0 filer tilbage, er man færdig. Står
///   der 1,8 GB, ved man, hvorfor det tager tid.
///
///   HVAD LIGGER DER FRA DE ANDRE? Det er dét, en ny bærbar henter hjem — og
///   det er dét, en genopbygget stationær kommer tilbage fra.
///
/// ============ TALLENE HENTES IKKE PÅ SKÆRMTRÅDEN ============
///
/// At gøre arkivet op er 352 opslag over SMB til en NAS. Gjort synkront i
/// VisArkiv() fryser hele vinduet, hver gang man klikker på delingsfanen —
/// og fryser det helt, hvis NAS'en sover. Derfor: linjen skrives med det
/// samme med det, der vides, og tallene kommer bagefter.
/// </remarks>
public partial class SettingsView
{
    private DispatcherTimer? _arkivur;
    private int _arkivkoersel;

    private void VisArkiv()
    {
        var deler = Delt.Slaaet_til;

        Deling_Arkivrude.Visibility = deler ? Visibility.Visible : Visibility.Collapsed;

        if (!deler) return;

        Deling_Arkivautomatisk.IsChecked = AppSettings.Current.ArkiverAutomatisk;
        Deling_Arkivlyd.IsChecked = AppSettings.Current.ArkiverLyd;
        Deling_Hentautomatisk.IsChecked = AppSettings.Current.HentAutomatisk;

        VisTakt();
        VisOversigt();

        Arkivfremdrift_vis();

        // ============ SKAERMEN SKAL FOELGE MED, MENS DET KOERER ============
        //
        // Uden uret ville bjaelken staa stille, indtil nogen klikkede paa en
        // anden fane og tilbage. Det halve sekund er valgt paa oejet: hurtigt
        // nok til at se ud som bevaegelse, langsomt nok til ikke at koste
        // noget.
        _arkivur ??= new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };

        _arkivur.Tick -= Arkivtik;
        _arkivur.Tick += Arkivtik;
        _arkivur.Start();

        Arkivtal_hent();
    }

    private void Arkivtik(object? sender, EventArgs e)
    {
        if (!IsVisible) { _arkivur?.Stop(); return; }

        Arkivfremdrift_vis();
    }

    // ======================================================================= takten

    /// <summary>Én takt, som den står i listen.</summary>
    private sealed record Taktvalg(Arkivtakt Takt, string Navn);

    private static List<Taktvalg> Takter() =>
        Enum.GetValues<Arkivtakt>()
            .Select(t => new Taktvalg(t, Sprog.T("arkiv.takt_" + t.ToString().ToLowerInvariant())))
            .ToList();

    private void VisTakt()
    {
        var i = AppSettings.Current;

        if (Deling_Arkivtakt.ItemsSource is null)
        {
            Deling_Arkivtakt.ItemsSource = Takter();

            // ============ TIMERNE ER TAL, IKKE TEKST ============
            //
            // «08» og ikke «8», saa de to bokse er lige brede og ikke hopper,
            // naar man vaelger. Og 0-23 og ikke 1-24: der findes ingen kl. 24.
            var timer = Enumerable.Range(0, 24).ToList();

            Deling_Arkivfra.ItemsSource = timer;
            Deling_Arkivtil.ItemsSource = timer;
        }

        Deling_Arkivtakt.SelectedItem = ((List<Taktvalg>)Deling_Arkivtakt.ItemsSource)
            .FirstOrDefault(t => t.Takt == i.ArkivTakt);

        Deling_Arkivfra.SelectedItem = Math.Clamp(i.ArkivFraKl, 0, 23);
        Deling_Arkivtil.SelectedItem = Math.Clamp(i.ArkivTilKl, 0, 23);

        var faste = Arkivplan.Faste(i.ArkivTakt);
        var manuelt = i.ArkivTakt == Arkivtakt.Manuelt;
        var engang = i.ArkivTakt == Arkivtakt.Engang;

        // ============ DE SAMME TO TAL BETYDER TO TING ============
        //
        // Ved de hyppige takter er de tidsrummets ender; ved de faste ER de
        // tidspunkterne. Derfor skifter baade teksten og hakket - fire bokse
        // til det samme ville vaere fire steder at tage fejl.
        Deling_Arkivtidsrum.Visibility = faste || manuelt ? Visibility.Collapsed : Visibility.Visible;
        Deling_Arkivtidsrum.IsChecked = i.ArkivKunITidsrum;

        Deling_Arkivtidsrumtekst.Text = Sprog.T(faste ? "arkiv.klokken" : "arkiv.kun_mellem");

        var vis = manuelt ? Visibility.Collapsed : Visibility.Visible;

        Deling_Arkivtidsrumtekst.Visibility = vis;
        Deling_Arkivfra.Visibility = vis;
        Deling_Arkivtil.Visibility = engang || manuelt ? Visibility.Collapsed : Visibility.Visible;
        Deling_Arkivog.Visibility = Deling_Arkivtil.Visibility;

        // Timerne betyder kun noget, naar de bruges til noget.
        Deling_Arkivfra.IsEnabled = faste || i.ArkivKunITidsrum;
        Deling_Arkivtil.IsEnabled = Deling_Arkivfra.IsEnabled;

        Deling_Arkivtakttekst.Text = Taktlinje(i);
    }

    /// <summary>Én linje, der siger hvad valget betyder — med klokkeslæt.</summary>
    /// <remarks>
    /// EN DROPDOWN ALENE FORTÆLLER IKKE, HVAD DER SKER. «To gange om dagen»
    /// er ikke et svar, før der står hvornår. Linjen her er stedet, hvor
    /// valget bliver til noget, man kan forudsige.
    /// </remarks>
    private static string Taktlinje(AppSettings i) => i.ArkivTakt switch
    {
        Arkivtakt.Manuelt => Sprog.T("arkiv.takt_linje_manuelt"),

        Arkivtakt.Engang => Sprog.T("arkiv.takt_linje_engang", Kl(i.ArkivFraKl)),

        Arkivtakt.Togange => Sprog.T("arkiv.takt_linje_togange", Kl(i.ArkivFraKl), Kl(i.ArkivTilKl)),

        _ => i.ArkivKunITidsrum
            ? Sprog.T("arkiv.takt_linje_tidsrum", Mellemrumsord(i.ArkivTakt),
                      Kl(i.ArkivFraKl), Kl(i.ArkivTilKl))
            : Sprog.T("arkiv.takt_linje_doegn", Mellemrumsord(i.ArkivTakt)),
    };

    /// <summary>Takten i tre ord. Til oversigten, hvor der ikke er plads til en sætning.</summary>
    /// <remarks>
    /// DEN LANGE LINJE HØRER OVENFOR, ved valget, hvor den forklarer hvad man
    /// vælger. Gentaget nede i oversigten fylder den to linjer og siger intet
    /// nyt — og så holder man op med at læse tabellen.
    /// </remarks>
    private static string Taktkort(AppSettings i) => i.ArkivTakt switch
    {
        Arkivtakt.Manuelt => Sprog.T("arkiv.ord_manuelt"),

        Arkivtakt.Engang => Sprog.T("arkiv.kort_engang", Kl(i.ArkivFraKl)),

        Arkivtakt.Togange => Sprog.T("arkiv.kort_togange", Kl(i.ArkivFraKl), Kl(i.ArkivTilKl)),

        _ => i.ArkivKunITidsrum
            ? Sprog.T("arkiv.kort_tidsrum", Mellemrumsord(i.ArkivTakt),
                      Kl(i.ArkivFraKl), Kl(i.ArkivTilKl))
            : Sprog.T("arkiv.kort_doegn", Mellemrumsord(i.ArkivTakt)),
    };

    private static string Mellemrumsord(Arkivtakt t) =>
        Sprog.T("arkiv.ord_" + t.ToString().ToLowerInvariant());

    private static string Kl(int time) => time.ToString("00") + ".00";

    // ==================================================================== oversigten

    /// <summary>
    /// Hvad appen ellers spørger om i den fælles mappe — og hvor tit.
    /// </summary>
    /// <remarks>
    /// DE HER KAN IKKE JUSTERES, OG DE STÅR ALLIGEVEL. Uden dem ser arkivets
    /// kvarter ud som den eneste forbindelse mellem de to maskiner, og så
    /// undrer man sig over, at en aftale er der med det samme, mens en
    /// optagelse ikke er.
    ///
    /// TALLENE HENTES FRA VAGTERNES EGNE FELTER. En tabel, der er skrevet af i
    /// hånden, holder op med at passe den dag, et af tallene ændres — og det
    /// opdager ingen, for den ser stadig rigtig ud.
    /// </remarks>
    private void VisOversigt()
    {
        Deling_Takter.Children.Clear();

        Linje(Sprog.T("arkiv.oversigt_arbejde"), Mellemrum(Jobs.Arbejdsvagt.Mellemrum));
        Linje(Sprog.T("arkiv.oversigt_maskiner"), Mellemrum(Jobs.Delingsvagt.Mellemrum));
        Linje(Sprog.T("arkiv.oversigt_arkiv"), Taktkort(AppSettings.Current), egen: true);
    }

    private void Linje(string hvad, string hvornaar, bool egen = false)
    {
        var raekke = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };

        var naar = new TextBlock
        {
            Text = hvornaar,
            FontSize = 11.5,
            Margin = new Thickness(14, 0, 0, 0),
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 260,
            Foreground = (System.Windows.Media.Brush)FindResource(egen ? "Tekst" : "TekstMeget"),
        };

        DockPanel.SetDock(naar, Dock.Right);
        raekke.Children.Add(naar);

        raekke.Children.Add(new TextBlock
        {
            Text = hvad,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("TekstMeget"),
        });

        Deling_Takter.Children.Add(raekke);
    }

    /// <summary>Et mellemrum, som man ville sige det højt.</summary>
    /// <remarks>
    /// «HVERT 1. MINUT» ER IKKE DANSK. Tallet ét skal ud af sætningen, ikke
    /// sættes ind i den — og det gælder også timen. Det er den slags, der
    /// får en tekst til at ligne noget, en maskine har sat sammen.
    /// </remarks>
    private static string Mellemrum(TimeSpan t)
    {
        if (t.TotalMinutes < 1) return Sprog.T("arkiv.hvert_sekund", (int)t.TotalSeconds);

        if (t.TotalMinutes < 60)
            return (int)t.TotalMinutes == 1
                ? Sprog.T("arkiv.hvert_minut_et")
                : Sprog.T("arkiv.hvert_minut", (int)t.TotalMinutes);

        return (int)t.TotalHours == 1
            ? Sprog.T("arkiv.hver_time_en")
            : Sprog.T("arkiv.hver_time", (int)t.TotalHours);
    }

    // ================================================================== fremdriften

    private void Arkivfremdrift_vis()
    {
        var koerer = Jobs.Arkivvagt.Koerer;

        Deling_Arkivkoerer.Visibility = koerer ? Visibility.Visible : Visibility.Collapsed;
        Deling_Arkivafbryd.Visibility = koerer ? Visibility.Visible : Visibility.Collapsed;
        Deling_Arkivnu.IsEnabled = !koerer;

        if (!koerer) return;

        if (_arkivsidst is not { } f) return;

        Deling_Arkivbjaelke.Value = f.Procent;

        Deling_Arkivfremdrift.Text = Sprog.T("arkiv.fremdrift",
            f.Nummer, f.Ialt, f.Fil, (int)f.Procent,
            (f.Byte - f.Sendt) / 1048576.0);
    }

    private static Arkivfremdrift? _arkivsidst;

    /// <summary>Kobler vagtens fremdrift til skærmen. Kaldes én gang.</summary>
    internal static void Arkivlyt()
    {
        Jobs.Arkivvagt.Melder = f => _arkivsidst = f;
    }

    // ===================================================================== tallene

    /// <summary>
    /// Henter arkivets tal i baggrunden og skriver dem, når de er der.
    /// </summary>
    /// <remarks>
    /// KØRSELSNUMMERET FORHINDRER, AT ET GAMMELT SVAR VINDER. Klikkes der frem
    /// og tilbage mellem fanerne, er der to opgørelser i luften på én gang, og
    /// den langsomste kommer sidst. Uden nummeret ville skærmen ende med det
    /// forkerte tal og blive stående med det.
    /// </remarks>
    private void Arkivtal_hent()
    {
        var nummer = ++_arkivkoersel;

        Deling_Arkivstatus.Text = Sprog.T("arkiv.taeller");
        Deling_Arkivlydhjaelp.Text = "";

        _ = Task.Run(() =>
        {
            var medLyd = AppSettings.Current.ArkiverLyd;

            Arkivtal mangler;
            IReadOnlyList<Arkivmaerke> alle;
            Arkivmaerke? mit;

            try
            {
                mangler = Arkiv.Mangler(medLyd);
                alle = Arkiv.Alle();
                mit = alle.FirstOrDefault(a => a.Id == Maskinid.Id);
            }
            catch (Exception)
            {
                mangler = Arkivtal.Intet;
                alle = Array.Empty<Arkivmaerke>();
                mit = null;
            }

            // Hvad lyden ville koste, hvis den kom med. Regnes altid, saa
            // hjaelpelinjen kan sige et tal og ikke «det fylder en del».
            Arkivtal medAlt;
            try { medAlt = medLyd ? mangler : Arkiv.Mangler(true); }
            catch (Exception) { medAlt = mangler; }

            Dispatcher.Invoke(() =>
            {
                if (nummer != _arkivkoersel) return;

                Deling_Arkivstatus.Text = Arkivlinje(mit, mangler);
                Deling_Arkivlydhjaelp.Text = Lydlinje(medLyd, mangler, medAlt);

                Deling_Arkivliste.Children.Clear();

                foreach (var a in alle.Where(x => x.Id != Maskinid.Id))
                    Deling_Arkivliste.Children.Add(Arkivkort(a));
            });
        });
    }

    private static string Arkivlinje(Arkivmaerke? mit, Arkivtal mangler)
    {
        // ============ ER DER INTET, SIGES DET LIGE UD ============
        //
        // «0 filer» paa en frisk maskine ser ud som en fejl. Det er det ikke -
        // den har bare ikke koert endnu, og foerste koersel er om et par
        // minutter.
        if (mit is null || mit.Filer == 0)
            return mangler.Noget
                ? Sprog.T("arkiv.ikke_begyndt", mangler.Filer, mangler.MB)
                : Sprog.T("arkiv.intet_at_arkivere");

        var sidst = Sprog.T("arkiv.ligger", mit.Filer, mit.MB,
                            Naar(mit.Sidst.ToLocalTime()));

        if (!mangler.Noget) return sidst + " " + Sprog.T("arkiv.ajour");

        return sidst + " " + Sprog.T("arkiv.mangler", mangler.Filer, mangler.MB);
    }

    private static string Lydlinje(bool medLyd, Arkivtal mangler, Arkivtal medAlt)
    {
        if (medLyd)
            return mangler.Lydfiler > 0
                ? Sprog.T("arkiv.medlyd_hjaelp_tal", mangler.Lydfiler, mangler.LydMB, mangler.LetMB)
                : Sprog.T("arkiv.medlyd_hjaelp");

        var lyd = medAlt.LydMB - mangler.LydMB;

        return lyd > 1
            ? Sprog.T("arkiv.udenlyd_hjaelp_tal", lyd)
            : Sprog.T("arkiv.udenlyd_hjaelp");
    }

    private static string Naar(DateTimeOffset t)
    {
        var i_dag = t.Date == DateTimeOffset.Now.Date;
        var i_gaar = t.Date == DateTimeOffset.Now.Date.AddDays(-1);

        if (i_dag) return Sprog.T("arkiv.i_dag", t.ToString("HH.mm"));
        if (i_gaar) return Sprog.T("arkiv.i_gaar", t.ToString("HH.mm"));

        return t.ToString("d. MMMM 'kl.' HH.mm", Sprog.Kultur);
    }

    // ================================================================= de andres

    /// <summary>Ét kort pr. arkiv, der ikke er vores eget.</summary>
    private Border Arkivkort(Arkivmaerke a)
    {
        var rude = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("Trykket"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("PanelKant"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 10, 0, 0),
        };

        var raekke = new DockPanel();

        var knap = Knap(Sprog.T("arkiv.hent_hjem"), () => Hentarkiv(a));
        DockPanel.SetDock(knap, Dock.Right);
        raekke.Children.Add(knap);

        var tekst = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        tekst.Children.Add(new TextBlock
        {
            Text = a.Navn,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        tekst.Children.Add(new TextBlock
        {
            Text = Sprog.T("arkiv.andres", a.Filer, a.MB, Naar(a.Sidst.ToLocalTime())),
            FontSize = 11.5,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("TekstMeget"),
        });

        raekke.Children.Add(tekst);
        rude.Child = raekke;

        return rude;
    }

    /// <summary>
    /// Henter en anden maskines arkiv hjem.
    /// </summary>
    /// <remarks>
    /// ============ DER SPØRGES FØRST, OG MED TAL ============
    ///
    /// Det kan være gigabyte over et netværk. Den, der trykker, skal vide
    /// hvor meget FØR det begynder — ikke opdage det, når maskinen har været
    /// optaget i en time.
    ///
    /// Og der siges, hvad der IKKE sker: intet af det, der ligger hjemme,
    /// bliver rørt. Det er dét, der gør knappen tryg at trykke på.
    /// </remarks>
    private void Hentarkiv(Arkivmaerke a)
    {
        var medLyd = AppSettings.Current.ArkiverLyd;

        Arkivtal tal;

        try { tal = Arkiv.Hjemme(a.Id, medLyd); }
        catch (Exception fejl)
        {
            MessageBox.Show(Window.GetWindow(this)!, fejl.Message,
                Sprog.T("arkiv.hent_hjem"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!tal.Noget)
        {
            MessageBox.Show(Window.GetWindow(this)!,
                Sprog.T("arkiv.hent_intet", a.Navn),
                Sprog.T("arkiv.hent_hjem"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var svar = MessageBox.Show(Window.GetWindow(this)!,
            Sprog.T("arkiv.hent_spoerg", a.Navn, tal.Filer, tal.MB),
            Sprog.T("arkiv.hent_hjem"), MessageBoxButton.OKCancel, MessageBoxImage.Question);

        if (svar != MessageBoxResult.OK) return;

        Deling_Arkivkoerer.Visibility = Visibility.Visible;
        Deling_Arkivnu.IsEnabled = false;

        _ = Task.Run(() =>
        {
            Arkivtal kom;

            try
            {
                kom = Arkiv.Hent(a.Id, medLyd, f => Dispatcher.Invoke(() =>
                {
                    Deling_Arkivbjaelke.Value = f.Procent;
                    Deling_Arkivfremdrift.Text = Sprog.T("arkiv.fremdrift",
                        f.Nummer, f.Ialt, f.Fil, (int)f.Procent, (f.Byte - f.Sendt) / 1048576.0);
                }));
            }
            catch (Exception)
            {
                kom = Arkivtal.Intet;
            }

            Dispatcher.Invoke(() =>
            {
                Deling_Arkivkoerer.Visibility = Visibility.Collapsed;
                Deling_Arkivnu.IsEnabled = true;

                MessageBox.Show(Window.GetWindow(this)!,
                    Sprog.T("arkiv.hent_faerdig", kom.Filer, kom.MB),
                    Sprog.T("arkiv.hent_hjem"), MessageBoxButton.OK, MessageBoxImage.Information);

                Arkivtal_hent();
            });
        });
    }

    // ===================================================================== knapperne

    private void Arkivautomatisk_Klik(object sender, RoutedEventArgs e)
    {
        if (_delingIndlaeser) return;

        AppSettings.Current.ArkiverAutomatisk = Deling_Arkivautomatisk.IsChecked == true;
        AppSettings.Current.Save();
    }

    private void Arkivlyd_Klik(object sender, RoutedEventArgs e)
    {
        if (_delingIndlaeser) return;

        AppSettings.Current.ArkiverLyd = Deling_Arkivlyd.IsChecked == true;
        AppSettings.Current.Save();

        Arkivtal_hent();
    }

    private void Hentautomatisk_Klik(object sender, RoutedEventArgs e)
    {
        if (_delingIndlaeser) return;

        AppSettings.Current.HentAutomatisk = Deling_Hentautomatisk.IsChecked == true;
        AppSettings.Current.Save();
    }

    private void Arkivtakt_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_delingIndlaeser) return;

        if (Deling_Arkivtakt.SelectedItem is not Taktvalg valg) return;

        if (valg.Takt == AppSettings.Current.ArkivTakt) return;

        AppSettings.Current.ArkivTakt = valg.Takt;

        // ============ ET SKIFT SKAL KUNNE MAERKES MED DET SAMME ============
        //
        // Uden det ville et skift fra «to gange om dagen» til «hvert kvarter»
        // vente til i morgen kl. 8, fordi dagens tur allerede var taget. Den,
        // der lige har valgt noget hyppigere, har gjort det, fordi han VIL
        // have det nu.
        AppSettings.Current.ArkivSidst = null;
        AppSettings.Current.Save();

        VisTakt();
        VisOversigt();
    }

    private void Arkivtidsrum_Klik(object sender, RoutedEventArgs e)
    {
        if (_delingIndlaeser) return;

        AppSettings.Current.ArkivKunITidsrum = Deling_Arkivtidsrum.IsChecked == true;
        AppSettings.Current.Save();

        VisTakt();
        VisOversigt();
    }

    private void Arkivtimer_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_delingIndlaeser) return;

        if (Deling_Arkivfra.SelectedItem is int fra) AppSettings.Current.ArkivFraKl = fra;
        if (Deling_Arkivtil.SelectedItem is int til) AppSettings.Current.ArkivTilKl = til;

        AppSettings.Current.Save();

        Deling_Arkivtakttekst.Text = Taktlinje(AppSettings.Current);
        VisOversigt();
    }

    private void Arkivnu_Klik(object sender, RoutedEventArgs e)
    {
        // ============ HVORFOR DEN KAN SIGE NEJ ============
        //
        // Vagten viger for optagelse og for tunge koersler, og knappen goer
        // det samme. Men en knap, der ikke goer noget, uden at sige hvorfor,
        // er en knap, man tror er i stykker.
        if (RecordingSession.NogenOptager)
        {
            Deling_Arkivstatus.Text = Sprog.T("arkiv.optager");
            return;
        }

        if (HeavyJobLock.Current() is { } tungt)
        {
            Deling_Arkivstatus.Text = Sprog.T("arkiv.optaget", tungt.Beskrivelse);
            return;
        }

        Jobs.Arkivvagt.Nu();
        Arkivfremdrift_vis();
    }

    private void Arkivafbryd_Klik(object sender, RoutedEventArgs e)
    {
        Jobs.Arkivvagt.Afbryd();

        // DET, DER NAAEDE OP, BLIVER LIGGENDE. Naeste koersel tager resten -
        // se Arkiv.Gem.
        Deling_Arkivstatus.Text = Sprog.T("arkiv.afbrudt");
    }
}
