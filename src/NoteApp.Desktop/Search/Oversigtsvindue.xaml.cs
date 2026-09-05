using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.Search;

/// <summary>Ét punkt i oversigten — en aftale eller en opgave.</summary>
/// <param name="Gruppe">Måneden, punktet hører til. Vises én gang pr. gruppe.</param>
/// <param name="Naar">Datoen eller klokkeslættet, som listen viser det.</param>
/// <param name="Titel">Det, man skimmer efter.</param>
/// <param name="Under">Hvor den kom fra, og hvad der ellers er værd at vide.</param>
/// <param name="Boble">Hele historien, når musen hviler.</param>
/// <param name="Farve">Striben i venstre kant — samme sprog som i Cockpittet.</param>
/// <param name="Kilde">Den rigtige aftale eller opgave. Vinduet rører den ikke.</param>
public sealed record Oversigtspunkt(
    string Gruppe, string Naar, string Titel, string Under, string Boble,
    Brush Farve, object Kilde)
{
    /// <summary>Sat af vinduet: er det her det første punkt i sin måned?</summary>
    public bool Foerst { get; set; }

    public Visibility Gruppevis => Foerst ? Visibility.Visible : Visibility.Collapsed;

    public Visibility Undervis =>
        Under.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Det, søgningen leder i.</summary>
    public string Soegbar => $"{Titel} {Under} {Gruppe} {Naar}";
}

/// <summary>
/// Hele listen — alle aftaler eller alle opgaver, grupperet efter måned.
/// </summary>
/// <remarks>
/// HVORFOR DEN FINDES
///
/// Kalenderen og opgaverne i Cockpittet viser de nærmeste. Kalenderen viste
/// endda kun seks, og det var et tal, ingen kunne se — listen sluttede bare,
/// og der stod ikke, at der var mere. Man kunne ikke komme til resten uden at
/// gå til Google.
///
/// ÉT VINDUE TIL BEGGE. Aftaler og opgaver er forskellige ting, men
/// spørgsmålet er det samme — «hvad har jeg, og hvornår» — og et vindue, der
/// ser ens ud begge steder, skal kun læres én gang. Kalderen laver punkterne;
/// vinduet ved ikke, hvad de er.
/// </remarks>
public partial class Oversigtsvindue : Window
{
    private readonly IReadOnlyList<Oversigtspunkt> _alle;
    private readonly Action<object>? _aabn;

    public Oversigtsvindue(string overskrift, string underskrift,
                           IReadOnlyList<Oversigtspunkt> punkter,
                           Action<object>? aabn = null)
    {
        InitializeComponent();

        _alle = punkter;
        _aabn = aabn;

        Title = overskrift;
        Overskrift.Text = overskrift;
        Underskrift.Text = underskrift;

        Glid.Vindue(this);

        Loaded += (_, _) => Felt.Focus();

        Vis("");
    }

    private void Felt_Aendret(object sender, TextChangedEventArgs e)
    {
        Pladsholder.Visibility = Felt.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Vis(Felt.Text);
    }

    /// <summary>
    /// Tegner listen — filtreret, og med en månedsoverskrift pr. gruppe.
    /// </summary>
    /// <remarks>
    /// OVERSKRIFTEN SÆTTES HER OG IKKE I DATAEN. Søger man, falder punkter
    /// væk, og så kan den første i en måned blive en anden. Stod flaget fast
    /// på punktet, ville en måned kunne stå uden overskrift — eller få to.
    ///
    /// ALLE ORD SKAL PASSE, ikke bare ét. «møde tina» skal finde mødet med
    /// Tina og ikke alt, hvad der hedder møde.
    /// </remarks>
    private void Vis(string soeg)
    {
        var ord = soeg.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var vist = _alle
            .Where(p => ord.All(o => p.Soegbar.Contains(o, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();

        var sidste = "";

        foreach (var p in vist)
        {
            p.Foerst = p.Gruppe != sidste;
            sidste = p.Gruppe;
        }

        Rude.ItemsSource = null;
        Rude.ItemsSource = vist;

        IngenTraef.Visibility = vist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        Antal.Text = vist.Count == _alle.Count
            ? Sprog.T("oversigt.antal", vist.Count.ToString())
            : Sprog.T("oversigt.antal_af", vist.Count.ToString(), _alle.Count.ToString());
    }

    private void Punkt_Klik(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Oversigtspunkt p }) return;
        if (_aabn is null) return;

        // VINDUET LUKKER FØRST. Den, der åbnes, er et andet vindue oven på
        // det her, og to vinduer om den samme aftale er én for meget: retter
        // man i det øverste, står det forkerte tilbage i det nederste.
        Close();

        _aabn(p.Kilde);
    }

    private void Luk_Klik(object sender, RoutedEventArgs e) => Close();
}
