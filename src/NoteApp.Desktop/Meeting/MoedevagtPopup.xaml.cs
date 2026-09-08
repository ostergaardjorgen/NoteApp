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

        Visadvarsel();

        Loaded += (_, _) => Placer();
    }

    /// <summary>
    /// Advarer, hvis opkaldet kører på en anden enhed end den, der optages fra.
    /// </summary>
    /// <remarks>
    /// ============ DET ER HER, DEN FEJL SKAL FANGES ============
    ///
    /// Windows har to standardhøjttalere: en til musik og en til opkald. Peger
    /// de hvert sit sted — og det gør de tit, skærmens højttalere til musik og
    /// headsettet til møder — optager appen loopback fra den forkerte.
    ///
    /// Så optages din egen stemme fint, og modparten findes ikke. Udskriften
    /// ser hel ud. Man opdager det først, når man leder efter noget, der blev
    /// sagt, og det er for sent.
    ///
    /// Beskeden står PRÆCIS her, hvor der skal trykkes — ikke inde under
    /// indstillinger, hvor ingen kigger, mens telefonen ringer.
    /// </remarks>
    private void Visadvarsel()
    {
        var i = AppSettings.Current;

        var (hoejttaler, mikrofon) = AudioDevices.Opkaldsafvigelse(i.SpeakerId, i.MicrophoneId);

        if (hoejttaler is null && mikrofon is null) return;

        // HOEJTTALEREN NAEVNES FOERST. Er den forkert, mangler MODPARTEN i
        // optagelsen, og det er det dyre. En forkert mikrofon koster din egen
        // stemme, og det opdager man med det samme.
        var hvad = hoejttaler is not null && mikrofon is not null
            ? Sprog.T("moedevagtpopup.enhed_begge", hoejttaler, mikrofon)
            : hoejttaler is not null
                ? Sprog.T("moedevagtpopup.enhed_hoejttaler", hoejttaler)
                : Sprog.T("moedevagtpopup.enhed_mikrofon", mikrofon!);

        Enhedsadvarseltekst.Text = hvad;
        Enhedsadvarsel.Visibility = Visibility.Visible;
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
