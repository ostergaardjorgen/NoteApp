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
            Udfald.Fuldført => ("✓ fuldført", new SolidColorBrush(Color.FromRgb(0x4C, 0xBE, 0x72))),
            Udfald.SeEfter => ("! se efter", new SolidColorBrush(Color.FromRgb(0xF0, 0xB2, 0x3C))),
            Udfald.Afbrudt => ("— afbrudt", new SolidColorBrush(Color.FromRgb(0x95, 0x9E, 0xAE))),
            _ => ("✕ fejlet", new SolidColorBrush(Color.FromRgb(0xF0, 0x50, 0x55)))
        };

        Slags = a.Slags;
    }

    public string Klokkeslet { get; }
    public string Dato { get; }
    public string Hvad { get; }
    public string Detaljer { get; }
    public string Model { get; }
    public string UdfaldTekst { get; }
    public Brush Kantfarve { get; }
    public HaendelseType Slags { get; }
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
