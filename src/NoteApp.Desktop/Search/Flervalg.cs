using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.Search;

/// <summary>Ét punkt i en flervalgsliste.</summary>
public sealed class Flervalgspunkt : INotifyPropertyChanged
{
    public required string Navn { get; init; }

    /// <summary>Det, filtret skal bruge. Null på «alt»-punktet.</summary>
    public required string? Vaerdi { get; init; }

    private bool _valgt;

    public bool Valgt
    {
        get => _valgt;
        set
        {
            if (_valgt == value) return;
            _valgt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Valgt)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// En afgrænsning, hvor man kan vælge flere.
/// </summary>
/// <remarks>
/// ============ HVORFOR DEN ER SKREVET SELV ============
///
/// WPF har ingen flervalgsliste. En ComboBox med hak indeni lukker sig selv,
/// hver gang man sætter et — og en afgrænsning, hvor man skal åbne listen
/// igen for hvert valg, bliver ikke brugt.
///
/// Her er det en knap og en popup. Popuppen bliver stående, til man klikker
/// uden for den, og hvert hak virker med det samme.
///
/// ============ HVAD DER STÅR PÅ KNAPPEN ============
///
///   intet valgt   «Alle mapper»   — afgrænsningen er ikke sat
///   ét valgt      «Møder»         — navnet, for det er kortere og klarere
///   flere valgt   «2 mapper»      — tallet, for navnene ville ikke kunne være
///
/// Teksten skal kunne læses i en spalte på 570 pixels sammen med fire andre.
/// Derfor tallet frem for en opremsning.
/// </remarks>
public sealed class Flervalg : ToggleButton
{
    private readonly Popup _popup = new();
    private readonly ItemsControl _liste = new();
    private readonly TextBlock _tekst = new();

    /// <summary>Teksten, når intet er valgt: «Alle mapper».</summary>
    public string Alt { get; set; } = "Alle";

    /// <summary>Ental og flertal til tallet: «mappe» / «mapper».</summary>
    public string Ental { get; set; } = "valgt";

    public string Flertal { get; set; } = "valgte";

    /// <summary>Rejses, når et hak ændres.</summary>
    public event Action? Aendret;

    private readonly List<Flervalgspunkt> _punkter = new();

    public Flervalg()
    {
        Padding = new Thickness(8, 4, 6, 4);
        FontSize = 11.5;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;

        var pil = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 1, 0, 0),
        };

        pil.SetResourceReference(ForegroundProperty, "TekstMeget");

        _tekst.VerticalAlignment = VerticalAlignment.Center;
        _tekst.TextTrimming = TextTrimming.CharacterEllipsis;

        var rk = new DockPanel();
        DockPanel.SetDock(pil, Dock.Right);
        rk.Children.Add(pil);
        rk.Children.Add(_tekst);

        Content = rk;

        // ============ POPUPPEN BLIVER STAAENDE ============
        //
        // StaysOpen=false lukker den, naar man klikker uden for - og kun dér.
        // Et hak inde i den lukker den ikke, og det er hele pointen: man
        // saetter tre hak i én bevaegelse.
        _popup.StaysOpen = false;
        _popup.Placement = PlacementMode.Bottom;
        _popup.PlacementTarget = this;
        _popup.AllowsTransparency = true;

        _liste.ItemTemplate = Skabelon();

        var ramme = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 3, 0, 0),
            MinWidth = 170,
            MaxHeight = 360,
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _liste,
            },
        };

        ramme.SetResourceReference(Border.BackgroundProperty, "Panel");
        ramme.SetResourceReference(Border.BorderBrushProperty, "PanelKant");

        _popup.Child = ramme;

        Checked += (_, _) => _popup.IsOpen = true;
        Unchecked += (_, _) => _popup.IsOpen = false;
        _popup.Closed += (_, _) => IsChecked = false;
    }

    private DataTemplate Skabelon()
    {
        var hak = new FrameworkElementFactory(typeof(CheckBox));

        hak.SetBinding(ToggleButton.IsCheckedProperty,
            new Binding(nameof(Flervalgspunkt.Valgt)) { Mode = BindingMode.TwoWay });

        hak.SetBinding(ContentProperty, new Binding(nameof(Flervalgspunkt.Navn)));
        hak.SetValue(MarginProperty, new Thickness(0, 0, 0, 6));
        hak.SetValue(FontSizeProperty, 12.0);

        return new DataTemplate { VisualTree = hak };
    }

    /// <summary>Fylder listen. Det, der var valgt, huskes, hvis det stadig findes.</summary>
    public void Fyld(IEnumerable<(string Navn, string? Vaerdi)> punkter)
    {
        var foer = Valgte.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var p in _punkter) p.PropertyChanged -= Punkt_Aendret;
        _punkter.Clear();

        foreach (var (navn, vaerdi) in punkter)
        {
            if (vaerdi is null) continue;   // «alt» er fraværet af hak, ikke et punkt

            var p = new Flervalgspunkt { Navn = navn, Vaerdi = vaerdi, Valgt = foer.Contains(vaerdi) };
            p.PropertyChanged += Punkt_Aendret;
            _punkter.Add(p);
        }

        _liste.ItemsSource = null;
        _liste.ItemsSource = _punkter;

        Visning();
    }

    private void Punkt_Aendret(object? sender, PropertyChangedEventArgs e)
    {
        Visning();
        Aendret?.Invoke();
    }

    /// <summary>De valgte værdier. Tom liste betyder «alt».</summary>
    public IReadOnlyList<string> Valgte =>
        _punkter.Where(p => p.Valgt).Select(p => p.Vaerdi!).ToList();

    /// <summary>Fjerner alle hak — uden at melde det for hvert enkelt.</summary>
    public void Ryd()
    {
        var noget = false;

        foreach (var p in _punkter)
        {
            if (!p.Valgt) continue;

            p.PropertyChanged -= Punkt_Aendret;
            p.Valgt = false;
            p.PropertyChanged += Punkt_Aendret;
            noget = true;
        }

        Visning();

        if (noget) Aendret?.Invoke();
    }

    private void Visning()
    {
        var valgte = _punkter.Where(p => p.Valgt).ToList();

        _tekst.Text = valgte.Count switch
        {
            0 => Alt,
            1 => valgte[0].Navn,
            _ => $"{valgte.Count} {Flertal}",
        };

        // DEN VALGTE AFGRAENSNING SKAL KUNNE SES PAA AFSTAND. Uden det er
        // forskellen paa «alle mapper» og «to mapper» et ord, man skal laese.
        if (valgte.Count > 0) _tekst.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        else _tekst.SetResourceReference(TextBlock.ForegroundProperty, "Tekst");

        FontWeight = valgte.Count > 0 ? FontWeights.SemiBold : FontWeights.Normal;
    }
}
