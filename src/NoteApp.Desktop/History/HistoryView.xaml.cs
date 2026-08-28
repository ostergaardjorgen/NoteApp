using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.History;

/// <summary>En post, som listen kan vise.</summary>
public sealed class PostVisning
{
    public PostVisning(Haendelse a)
    {
        Klokkeslet = a.Tid.ToString("HH:mm:ss");
        Dato = a.Tid.ToString("d. MMM yyyy");
        Hvad = a.Hvad;
        Model = a.Model.Length > 0 ? $"Model: {a.Model}" : "";

        var varighed = a.Sekunder > 0
            ? $" · tog {TimeSpan.FromSeconds(a.Sekunder):mm\\:ss}"
            : "";
        Detaljer = a.Detaljer + varighed;

        (UdfaldTekst, Kantfarve) = a.Udfald switch
        {
            Udfald.Fuldført => ("✓ fuldført", Temaskift.Pensel("Godkendt")),
            Udfald.SeEfter => ("! se efter", Temaskift.Pensel("Advarsel")),
            Udfald.Afbrudt => ("— afbrudt", Temaskift.Pensel("Slukket")),
            _ => ("✕ fejlet", Temaskift.Pensel("FejlTekst"))
        };

        Slags = a.Slags;
        Kilde = a.Kilde;

        // NAVNET SLAAS OP NU, IKKE DA LINJEN BLEV SKREVET.
        //
        // Overskriften baerer det navn, tingen havde dengang. Omdoebes den,
        // peger linjen paa noget, der ikke findes - og saa leder man efter en
        // fil, der ligger lige der, under et andet navn.
        //
        // Id'et aendrer sig aldrig. Findes kilden, staar dens NUVAERENDE navn
        // som et link. Findes den ikke, staar der, at den er slettet - og det
        // er ogsaa et svar: linjen bliver staaende som det eneste spor af, at
        // noget fandtes.
        (LinkTekst, Findes) = SlaaOp(a);

        HarLink = LinkTekst.Length > 0;
        LinkFarve = Findes
            ? Temaskift.Pensel("Accent")
            : Temaskift.Pensel("Slukket");
    }

    /// <summary>
    /// Finder kilden frem på id'et og giver den tekst, linjen skal vise.
    ///
    /// Returnerer tom tekst for hændelser uden en kilde at gå til — en
    /// hentning, en backup, et manglende opsætningstrin.
    /// </summary>
    private static (string Tekst, bool Findes) SlaaOp(Haendelse a)
    {
        if (a.Kilde.Length == 0) return ("", false);

        try
        {
            switch (a.Slags)
            {
                case HaendelseType.Dokument:
                case HaendelseType.Slettet when NoteApp.Core.Documents.DocumentStore.LoadAll().Any(d => d.Id == a.Kilde):
                case HaendelseType.Flyttet:
                {
                    var d = NoteApp.Core.Documents.DocumentStore.LoadAll().FirstOrDefault(x => x.Id == a.Kilde);
                    return d is null
                        ? ("dokumentet er slettet", false)
                        : ($"Vis «{d.Title}»", true);
                }

                case HaendelseType.Optagelse:
                case HaendelseType.Transskription:
                {
                    var m = MeetingStore.FindById(a.Kilde);
                    return m is null
                        ? ("optagelsen er slettet", false)
                        : ($"Vis «{m.Value.Meta.Title}»", true);
                }

                default:
                    return ("", false);
            }
        }
        catch (Exception)
        {
            // Kan kilden ikke slaas op, er linjen stadig laesbar uden link.
            return ("", false);
        }
    }

    public string Klokkeslet { get; }
    public string Dato { get; }
    public string Hvad { get; }
    public string Detaljer { get; }
    public string Model { get; }
    public string UdfaldTekst { get; }
    public Brush Kantfarve { get; }
    public HaendelseType Slags { get; }

    /// <summary>Id'et på kilden. Tom, hvis der ikke er en at gå til.</summary>
    public string Kilde { get; }

    /// <summary>«Vis «navn»» eller «optagelsen er slettet». Tom uden kilde.</summary>
    public string LinkTekst { get; }

    /// <summary>Findes kilden stadig? Afgør om der kan klikkes.</summary>
    public bool Findes { get; }

    public bool HarLink { get; }
    public Brush LinkFarve { get; }
}

/// <summary>
/// Historikken.
///
/// TO FORMÅL, OG DE ER LIGE VIGTIGE
///
/// Det praktiske: en transskription tager tyve minutter, og var man et andet
/// sted, da den blev færdig, er der ingen kvittering tilbage at kigge på.
/// «Kørte den overhovedet?» skal kunne besvares uden at lede i mapper.
///
/// Det formelle: hele pointen med at køre modellerne lokalt er, at mødet ikke
/// forlader maskinen. Den påstand er kun noget værd, hvis den kan efterprøves.
/// Derfor står tallet for udgående data øverst og som ét tal — et svar, der
/// skal udledes ved at læse en liste igennem, er ikke et svar.
/// </summary>
public partial class HistoryView : UserControl
{
    private IReadOnlyList<Haendelse> _alle = Array.Empty<Haendelse>();

    private static readonly (string Navn, HaendelseType? Slags)[] Filtre =
    {
        ("Alt", null),
        ("Optagelser", HaendelseType.Optagelse),
        ("Transskriptioner", HaendelseType.Transskription),
        ("Dokumenter", HaendelseType.Dokument),
        ("Hentninger", HaendelseType.Hentning),
        ("Sikkerhedskopier", HaendelseType.Backup)
    };

    public HistoryView()
    {
        InitializeComponent();

        Filter.ItemsSource = Filtre.Select(f => f.Navn).ToList();
        Filter.SelectedIndex = 0;

        Indlæs();
    }

    private void Indlæs()
    {
        _alle = Historik.Laes();

        // HER BLEV «Intet har forladt denne pc» regnet ud og skrevet.
        //
        // Panelet er fjernet fra skaermen 18-08-2026. Paastanden holdt kun,
        // saa laenge appen ikke havde et netvaerkskald i sig; dokumenter
        // laves nu hos Mistral i Europa, og teksten SENDES.
        //
        // Feltet DataForlodMaskinen staar stadig paa hver post, og listen
        // nedenfor viser det post for post. Det er dét, en revision kan
        // bruge - en overskrift, der opsummerer, kan kun tage fejl.

        Vis();
        Status.Text = Historik.Path;
    }

    private void Vis()
    {
        var valgt = Filter.SelectedIndex >= 0 ? Filtre[Filter.SelectedIndex].Slags : null;

        var poster = (valgt is null ? _alle : _alle.Where(a => a.Slags == valgt))
            .Select(a => new PostVisning(a))
            .ToList();

        Liste.ItemsSource = poster;

        Antal.Text = poster.Count switch
        {
            0 => "Ingen poster",
            1 => "1 post",
            _ => $"{poster.Count} poster"
        };

        TomTekst.Visibility = poster.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized) Vis();
    }

    private void Genindlaes_Click(object sender, RoutedEventArgs e) => Indlæs();

    /// <summary>
    /// Går til det, linjen handler om.
    ///
    /// Kilden slås op på id'et her og nu. Er den forsvundet mellem visningen
    /// og klikket, siges det — frem for at skifte skærm og vise ingenting.
    /// </summary>
    private void Kilde_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not PostVisning post) return;
        if (post.Kilde.Length == 0 || !post.Findes) return;

        var hoved = Application.Current.MainWindow as MainWindow;
        if (hoved is null) return;

        var gik = post.Slags switch
        {
            HaendelseType.Optagelse or HaendelseType.Transskription
                => hoved.GaaTilOptagelse(post.Kilde),
            _ => Gaa(() => hoved.GaaTilDokumenter(post.Kilde))
        };

        if (!gik)
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den findes ikke længere",
                "Det, linjen handler om, er slettet eller flyttet uden for appen. " +
                "Linjen bliver stående som spor af, at det fandtes.",
                Dialogs.Slags.Valg);
    }

    private static bool Gaa(Action a) { a(); return true; }

    private void Aabn_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(Historik.Path))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Ingen logfil", "Der er ingen logfil endnu — der er ikke sket noget at skrive ned.", Dialogs.Slags.Valg);
            return;
        }

        // Mappen frem for filen: .jsonl har sjaeldent et program tilknyttet, og
        // en dialog om "hvad skal aabne denne fil" er ikke et svar.
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Historik.Path}\"")
        { UseShellExecute = true });
    }
}
