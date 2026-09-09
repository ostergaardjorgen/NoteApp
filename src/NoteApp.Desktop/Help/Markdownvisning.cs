using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NoteApp.Desktop.Help;

/// <summary>
/// Markdown til et FlowDocument — nok til hjælpeteksterne, og ikke mere.
///
/// HVORFOR DEN ER SKREVET SELV OG IKKE HENTET
///
/// En Markdown-pakke kan alt: tabeller, fodnoter, HTML midt i teksten. Alt
/// det koster en afhængighed mere, som skal holdes opdateret og gøres rede
/// for under Compliance — for en hjælpetekst på ti sider.
///
/// Det, hjælpen bruger, er overskrifter, afsnit, punktopstillinger, fed
/// skrift, kode og citater. Det er halvfjerds linjer.
///
/// DET, DER IKKE FORSTÅS, VISES SOM TEKST. En hjælpefil, hvor nogen har
/// skrevet en tabel, må ikke ende som en tom side — den skal bare se lidt
/// kedelig ud.
/// </summary>
public static class Markdownvisning
{
    public static FlowDocument Byg(string markdown, ResourceDictionary ressourcer)
    {
        var tekstfarve = (Brush)ressourcer["Tekst"];
        var svag = (Brush)ressourcer["TekstSvag"];
        var meget = (Brush)ressourcer["TekstMeget"];
        var accent = (Brush)ressourcer["Accent"];
        var kant = (Brush)ressourcer["PanelKant"];

        var doku = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13.5,
            LineHeight = 21,
            Foreground = tekstfarve,
            Background = Brushes.Transparent,
            PagePadding = new Thickness(4, 0, 16, 24)
        };

        var linjer = (markdown ?? "").Replace("\r\n", "\n").Split('\n');

        List? liste = null;
        Paragraph? afsnit = null;

        void LukAfsnit() { afsnit = null; }
        void LukListe() { liste = null; }

        for (var i = 0; i < linjer.Length; i++)
        {
            var raa = linjer[i];
            var l = raa.Trim();

            // ---------- tom linje: luk det, der var i gang ----------
            if (l.Length == 0) { LukAfsnit(); LukListe(); continue; }

            // ---------- vandret streg ----------
            if (l is "---" or "***" or "___")
            {
                LukAfsnit(); LukListe();

                doku.Blocks.Add(new BlockUIContainer(new Border
                {
                    Height = 1,
                    Background = kant,
                    Margin = new Thickness(0, 14, 0, 14)
                }));
                continue;
            }

            // ---------- overskrifter ----------
            if (l.StartsWith("### ", StringComparison.Ordinal))
            {
                LukAfsnit(); LukListe();
                doku.Blocks.Add(Overskrift(l[4..], 14, FontWeights.SemiBold, tekstfarve, 16, 4, ressourcer));
                continue;
            }

            if (l.StartsWith("## ", StringComparison.Ordinal))
            {
                LukAfsnit(); LukListe();
                doku.Blocks.Add(Overskrift(l[3..], 16.5, FontWeights.SemiBold, tekstfarve, 22, 6, ressourcer));
                continue;
            }

            if (l.StartsWith("# ", StringComparison.Ordinal))
            {
                LukAfsnit(); LukListe();
                doku.Blocks.Add(Overskrift(l[2..], 21, FontWeights.SemiBold, tekstfarve, 0, 8, ressourcer));
                continue;
            }

            // ---------- citat ----------
            if (l.StartsWith("> ", StringComparison.Ordinal))
            {
                LukAfsnit(); LukListe();

                var p = new Paragraph
                {
                    Margin = new Thickness(0, 10, 0, 10),
                    Padding = new Thickness(14, 8, 10, 8),
                    BorderBrush = accent,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Foreground = svag,
                    FontStyle = FontStyles.Italic
                };

                // Et citat kan gaa over flere linjer.
                var samlet = l[2..];
                while (i + 1 < linjer.Length && linjer[i + 1].Trim().StartsWith("> ", StringComparison.Ordinal))
                {
                    i++;
                    samlet += " " + linjer[i].Trim()[2..];
                }

                Indhold(p.Inlines, samlet, accent);
                doku.Blocks.Add(p);
                continue;
            }

            // ---------- punktopstilling ----------
            if (l.StartsWith("- ", StringComparison.Ordinal) || l.StartsWith("* ", StringComparison.Ordinal))
            {
                LukAfsnit();

                if (liste is null)
                {
                    liste = new List
                    {
                        MarkerStyle = TextMarkerStyle.Disc,
                        Margin = new Thickness(0, 6, 0, 10),
                        Padding = new Thickness(20, 0, 0, 0)
                    };
                    doku.Blocks.Add(liste);
                }

                var punkt = new Paragraph { Margin = new Thickness(0, 0, 0, 5) };
                Indhold(punkt.Inlines, l[2..], accent);

                // En efterfoelgende indrykket linje hoerer til samme punkt.
                while (i + 1 < linjer.Length
                       && linjer[i + 1].StartsWith("  ", StringComparison.Ordinal)
                       && linjer[i + 1].Trim().Length > 0
                       && !linjer[i + 1].Trim().StartsWith("- ", StringComparison.Ordinal))
                {
                    i++;
                    punkt.Inlines.Add(new Run(" "));
                    Indhold(punkt.Inlines, linjer[i].Trim(), accent);
                }

                liste.ListItems.Add(new ListItem(punkt));
                continue;
            }

            // ---------- almindelig tekst ----------
            LukListe();

            if (afsnit is null)
            {
                afsnit = new Paragraph { Margin = new Thickness(0, 0, 0, 12) };
                doku.Blocks.Add(afsnit);
            }
            else
            {
                afsnit.Inlines.Add(new Run(" "));
            }

            Indhold(afsnit.Inlines, l, accent);
        }

        return doku;
    }

    private static Paragraph Overskrift(string tekst, double stoerrelse, FontWeight vaegt,
                                        Brush farve, double over, double under,
                                        ResourceDictionary ressourcer)
    {
        var p = new Paragraph
        {
            FontSize = stoerrelse,
            FontWeight = vaegt,
            Foreground = farve,
            Margin = new Thickness(0, over, 0, under)
        };

        Indhold(p.Inlines, tekst, (Brush)ressourcer["Accent"]);
        return p;
    }

    /// <summary>
    /// Fed skrift, kursiv og kode inde i en linje.
    ///
    /// Der læses ét tegn ad gangen frem for med et regulært udtryk. Udtrykket
    /// ville være kortere og fejle på det første ord, hvor der stod en enkelt
    /// stjerne midt i en sætning.
    /// </summary>
    /// <summary>
    /// Et klikbart link, der åbner i browseren.
    /// </summary>
    /// <remarks>
    /// UseShellExecute er nødvendig: uden den forsøger .NET at starte adressen
    /// som et program, og så sker der ingenting.
    ///
    /// En browser, der ikke vil åbne, må ikke vælte hjælpen. Der er ikke noget
    /// fornuftigt at gøre ved det — adressen står stadig på skærmen.
    /// </remarks>
    private static Hyperlink Henvisning(string vist, Uri maal)
    {
        var link = new Hyperlink(new Run(vist)) { NavigateUri = maal };

        link.RequestNavigate += (_, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true,
                });
            }
            catch (Exception)
            {
                // Ingen browser, eller en der naegtede. Adressen staar der
                // stadig; der er ikke noget at melde.
            }

            e.Handled = true;
        };

        return link;
    }

    private static void Indhold(InlineCollection ud, string tekst, Brush kodefarve)
    {
        var i = 0;
        var almindelig = new System.Text.StringBuilder();

        void Tom()
        {
            if (almindelig.Length == 0) return;
            ud.Add(new Run(almindelig.ToString()));
            almindelig.Clear();
        }

        while (i < tekst.Length)
        {
            // **fed**
            if (i + 1 < tekst.Length && tekst[i] == '*' && tekst[i + 1] == '*')
            {
                var slut = tekst.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (slut > 0)
                {
                    Tom();
                    ud.Add(new Run(tekst[(i + 2)..slut]) { FontWeight = FontWeights.SemiBold });
                    i = slut + 2;
                    continue;
                }
            }

            // [tekst](adresse)
            //
            // ============ EN ADRESSE, MAN SKAL SKRIVE AF, ER INGEN HENVISNING ============
            //
            // Hjaelpen henviser til Microsofts egne sider om Telefonlink, og de
            // adresser er lange. Stod de som raa tekst, skulle brugeren taste
            // dem af fra et vindue, han ikke kan kopiere fra med musen paa den
            // maade, han er vant til.
            //
            // KUN http og https. Hjaelpeteksterne er vores egne og ligger som
            // indlejrede ressourcer - men en aabning, der tager HVAD SOM HELST,
            // er en aabning, der en dag tager en file:- eller ms-settings:-sti,
            // som ingen havde taenkt over.
            if (tekst[i] == '[')
            {
                var slutTekst = tekst.IndexOf(']', i + 1);

                if (slutTekst > i
                    && slutTekst + 1 < tekst.Length
                    && tekst[slutTekst + 1] == '(')
                {
                    var slutAdresse = tekst.IndexOf(')', slutTekst + 2);

                    if (slutAdresse > slutTekst)
                    {
                        var vist = tekst[(i + 1)..slutTekst];
                        var adresse = tekst[(slutTekst + 2)..slutAdresse];

                        if (Uri.TryCreate(adresse, UriKind.Absolute, out var maal)
                            && (maal.Scheme == Uri.UriSchemeHttp || maal.Scheme == Uri.UriSchemeHttps))
                        {
                            Tom();
                            ud.Add(Henvisning(vist, maal));
                            i = slutAdresse + 1;
                            continue;
                        }
                    }
                }
            }

            // `kode`
            if (tekst[i] == '`')
            {
                var slut = tekst.IndexOf('`', i + 1);
                if (slut > 0)
                {
                    Tom();
                    ud.Add(new Run(tekst[(i + 1)..slut])
                    {
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12.5,
                        Foreground = kodefarve
                    });
                    i = slut + 1;
                    continue;
                }
            }

            // *kursiv* - kun naar der IKKE er tale om to stjerner
            if (tekst[i] == '*' && (i + 1 >= tekst.Length || tekst[i + 1] != '*'))
            {
                var slut = tekst.IndexOf('*', i + 1);
                if (slut > 0 && slut > i + 1)
                {
                    Tom();
                    ud.Add(new Run(tekst[(i + 1)..slut]) { FontStyle = FontStyles.Italic });
                    i = slut + 1;
                    continue;
                }
            }

            almindelig.Append(tekst[i]);
            i++;
        }

        Tom();
    }
}
