using System.Windows.Controls;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Diktering;

/// <summary>
/// Prøverummet: tal, og se hvad der kommer ud — før det lander et sted.
///
/// Man kan læse sig til, hvad en teksttype gør. Man kan ikke læse sig til,
/// hvordan éns egen måde at tale på ser ud, når den er skrevet ud og pudset
/// af — og det er dét, der afgør, om instruktionen skal rettes.
/// </summary>
public partial class ProeverumView : UserControl
{
    /// <summary>Én type, som knapperækken viser den.</summary>
    private sealed record Typevalg(Dikteringsformaal Vaerdi, string Navn, string Hvornaar, bool Valgt);

    private Dikteringsformaal _valgt = Dikteringsformaal.Note;

    public ProeverumView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            VisTyper();
            Tag();
        };

        // ============ PRØVERUMMET SKAL SLIPPES IGEN ============
        //
        // Bliver det siddende, naar man gaar til en anden skaerm, ville hvert
        // eneste diktat resten af dagen lande i et vindue, ingen kigger paa -
        // og aldrig i den mail, man sad og skrev.
        Unloaded += (_, _) => Slip();
    }

    private void VisTyper()
    {
        Typer.ItemsSource = Teksttyper.Alle.Select(f => new Typevalg(
            f,
            Sprog.T(Teksttyper.Noegletekst(f)),
            Sprog.T(Teksttyper.Noegletekst(f) + "_hvornaar"),
            f == _valgt)).ToList();
    }

    private void Type_Valgt(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: Dikteringsformaal f }) return;

        _valgt = f;
        Tag();
    }

    /// <summary>Tager dikteringen til sig, så teksten lander her.</summary>
    private void Tag()
    {
        Dikteringsvagt.Proeve = new Dikteringsvagt.Proeverum(_valgt, Vis);
        Besked.Text = Sprog.T("proeverum.klar", Sprog.T(Teksttyper.Noegletekst(_valgt)).ToLowerInvariant());
    }

    private void Slip()
    {
        // Kun vores egen slippes. Er en anden skaerm naaet at tage den, er den
        // ikke vores at fjerne.
        if (Dikteringsvagt.Proeve?.Vis == (Action<string, string>)Vis) Dikteringsvagt.Proeve = null;
    }

    private void Vis(string raa, string pudset) => Dispatcher.Invoke(() =>
    {
        Raa.Text = raa;
        Pudset.Text = pudset;
    });
}
