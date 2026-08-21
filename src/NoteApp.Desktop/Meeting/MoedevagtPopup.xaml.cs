using System.Windows;
using NoteApp.Core;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// Beskeden ved uret: et program har åbnet din mikrofon — er der startet et
/// møde?
///
/// Den ejer ingenting. Den siger, hvad der blev trykket på, og lukker sig
/// selv. Optagelsen hører til <see cref="MeetingView"/>, præcis som med
/// optagebåndet — ellers ville der være to steder, der kunne starte et møde.
/// </summary>
public partial class MoedevagtPopup : Window
{
    /// <summary>Der blev sagt ja. Optagelsen startes af den, der lyttede.</summary>
    public event Action? Optag;

    /// <summary>Der blev sagt «spørg aldrig for det her program».</summary>
    public event Action<string>? Aldrig;

    private readonly Mikrofonbruger _bruger;

    public MoedevagtPopup(Mikrofonbruger bruger)
    {
        InitializeComponent();

        _bruger = bruger;

        Overskrift.Text = $"{bruger.Navn} bruger din mikrofon";
        AldrigKnap.Content = $"Spørg aldrig for {bruger.Navn}";

        Loaded += (_, _) => Placer();
    }

    /// <summary>
    /// Nede i hjørnet ved uret, hvor Windows' egne beskeder kommer.
    ///
    /// Der regnes på arbejdsområdet og ikke på skærmen: står proceslinjen i
    /// bunden, ville beskeden ellers ligge bag den.
    /// </summary>
    private void Placer()
    {
        UpdateLayout();

        var plads = SystemParameters.WorkArea;
        Left = plads.Right - ActualWidth - 18;
        Top = plads.Bottom - ActualHeight - 18;
    }

    private void Optag_Klik(object sender, RoutedEventArgs e)
    {
        Optag?.Invoke();
        Close();
    }

    private void Aldrig_Klik(object sender, RoutedEventArgs e)
    {
        Aldrig?.Invoke(_bruger.Noegle);
        Close();
    }

    private void Luk_Klik(object sender, RoutedEventArgs e) => Close();
}
