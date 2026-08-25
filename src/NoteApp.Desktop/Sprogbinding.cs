using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Teksterne som noget, XAML kan binde sig til — og som skifter, mens appen
/// kører.
///
/// HVORFOR EN INDEKSER OG IKKE BARE ET OPSLAG
///
/// Et almindeligt opslag ville sætte teksten én gang, da vinduet blev bygget.
/// Skiftede man sprog, ville alt stå på det gamle sprog, indtil man genstartede
/// — og en sprogknap, der kræver en genstart, er ikke en sprogknap.
///
/// En indekser på et objekt, der melder <c>Item[]</c> ændret, får WPF til at
/// spørge om alle bundne tekster igen. Ét kald, og hele brugerfladen skifter.
/// </summary>
public sealed class Oversaettelser : INotifyPropertyChanged
{
    public static Oversaettelser Nu { get; } = new();

    private Oversaettelser()
    {
        Sprog.Aendret += () =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    /// <summary>Teksten bag nøglen. Bruges af bindingen, ikke direkte fra kode.</summary>
    public string this[string noegle] => Sprog.T(noegle);

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// <c>Text="{local:Oversat nav.cockpit}"</c> — en tekst, der følger sproget.
///
/// Den laver en helt almindelig binding til <see cref="Oversaettelser.Nu"/>,
/// så alt det, WPF kan i forvejen, virker: den kan stå på Text, Content,
/// ToolTip, Header og alt andet, der tager en streng.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class OversatExtension : MarkupExtension
{
    public OversatExtension() { }

    public OversatExtension(string noegle) => Noegle = noegle;

    /// <summary>Nøglen, fx <c>nav.cockpit</c>.</summary>
    [ConstructorArgument("noegle")]
    public string Noegle { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // Bindingen peger paa indekseren. Ét enkelt "Item[]"-signal faar WPF
        // til at spoerge om dem alle igen.
        var binding = new Binding($"[{Noegle}]")
        {
            Source = Oversaettelser.Nu,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
