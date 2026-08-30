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

        Loaded += (_, _) => VisTyper();

        // ============ DET AFGØRES AF, OM RUMMET KAN SES ============
        //
        // Her stod Loaded og Unloaded. Det er ikke det samme som «fremme»:
        // en fane, man klikker væk fra, bliver siddende i træet, og så holdt
        // prøverummet fast i dikteringen, længe efter man var gået videre.
        //
        // Set 30-08-2026: brugeren stod på Noter-fanen, dikterede, og fik
        // «Prøvet som note — se resultatet ovenfor». Der var ingenting
        // ovenfor, teksten kom aldrig i udklipsholderen, og den blev aldrig
        // tilbudt som note. Prøverummet havde taget den, to faner væk.
        //
        // IsVisible følger fanevalget. Kan rummet ikke ses, er det ikke
        // rummet, der skal have teksten.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) Tag();
            else Slip();
        };

        // Baelte og seler: forlader man skaermen helt, slippes den ogsaa.
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
