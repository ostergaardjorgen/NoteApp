using System.Windows;
using NoteApp.Core;
using NoteApp.Core.Llm;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// En aftale, man selv lægger ind — eller retter.
///
/// KALENDEREN SKAL VIRKE UDEN EN INTEGRATION, og det er ikke en
/// overgangsløsning. Målgruppen er studerende og mindre selvstændige, og en
/// del af dem har hverken Google Workspace eller Microsoft 365.
///
/// De tre valg — mappe, mødetype og sprog — er de samme som i
/// opstartsdialogen, og de står her, fordi de kan besvares i ro, når aftalen
/// lægges ind. Trykker man optag på aftalen bagefter, følger de med, og så
/// skal der ikke svares på noget, mens mødet går i gang.
/// </summary>
public partial class AftaleWindow : Window
{
    public Aftale Aftalen { get; private set; }

    /// <summary>Sat, hvis brugeren valgte at slette aftalen.</summary>
    public bool Slettet { get; private set; }

    private sealed record Punkt(string Navn, string? Vaerdi);

    public AftaleWindow(Aftale? aftale)
    {
        InitializeComponent();

        var ny = aftale is null;

        Aftalen = aftale ?? new Aftale
        {
            // Naeste hele halve time. En aftale, man laegger ind nu, ligger
            // naesten altid frem i tiden - og et klokkeslaet, der allerede er
            // passeret, skal rettes hver eneste gang.
            Start = Naeste()
        };

        Title = ny ? "Ny aftale" : "Aftale";
        Overskrift.Text = ny ? "Ny aftale" : "Ret aftalen";

        Underskrift.Text = ny
            ? "Den står i Cockpittet, og du kan trykke optag direkte på den."
            : Aftalen.KanRettes
                ? "Den står i Cockpittet, og du kan trykke optag direkte på den."
                : $"Aftalen kommer fra {Aftalen.Kilde}. Titel og tidspunkt rettes dér — " +
                  "det, du vælger her, gælder kun optagelsen.";

        SletKnap.Visibility = ny ? Visibility.Collapsed : Visibility.Visible;

        // EN HENTET AFTALE KAN IKKE RETTES HER.
        //
        // Titlen og tidspunktet kommer fra Google, og de bliver overskrevet
        // ved naeste hentning. Et felt, man kan skrive i, og som bliver rullet
        // tilbage en time senere, er vaerre end et, der er laast.
        Titel.IsEnabled = Aftalen.KanRettes;
        Dato.IsEnabled = Aftalen.KanRettes;
        Fra.IsEnabled = Aftalen.KanRettes;
        Til.IsEnabled = Aftalen.KanRettes;
        Link.IsEnabled = Aftalen.KanRettes;

        Fyld();
        Loaded += (_, _) => Titel.Focus();
    }

    private static DateTimeOffset Naeste()
    {
        var nu = DateTimeOffset.Now;
        var minutter = nu.Minute < 30 ? 30 - nu.Minute : 60 - nu.Minute;

        return nu.AddMinutes(minutter).AddSeconds(-nu.Second).AddMilliseconds(-nu.Millisecond);
    }

    private void Fyld()
    {
        Titel.Text = Aftalen.Titel;
        Dato.SelectedDate = Aftalen.Start.LocalDateTime.Date;

        var tider = Tider();
        Fra.ItemsSource = tider;
        Til.ItemsSource = tider;

        Fra.Text = Aftalen.Start.LocalDateTime.ToString("HH:mm");
        Til.Text = Aftalen.Slutter.LocalDateTime.ToString("HH:mm");

        Link.Text = Aftalen.Link.Length > 0 ? Aftalen.Link : Aftalen.Sted;

        // ---- mappen
        var mapper = new List<Punkt> { new("Vælg en mappe", null) };
        foreach (var m in Mapper.Alle(Mapper.Slags.Optagelser)) mapper.Add(new Punkt(m, m));

        Mappevalg.ItemsSource = mapper;
        Mappevalg.SelectedItem = mapper.FirstOrDefault(p => p.Vaerdi == Aftalen.Mappe) ?? mapper[0];

        // ---- moedetypen
        var typer = new List<Punkt> { new("Ikke valgt", null) };
        foreach (var t in PromptTemplate.LoadAll()) typer.Add(new Punkt(t.Name, t.Name));

        Typevalg.ItemsSource = typer;
        Typevalg.SelectedItem = typer.FirstOrDefault(p => p.Vaerdi == Aftalen.Moedetype) ?? typer[0];

        // ---- sproget
        var sprog = new List<Punkt> { new("Spørg mig", null) };
        foreach (var s in SprogvalgWindow.Sprog) sprog.Add(new Punkt(s.Navn, s.Kode));

        Sprogvalg.ItemsSource = sprog;
        Sprogvalg.SelectedItem = sprog.FirstOrDefault(p => p.Vaerdi == Aftalen.Sprog) ?? sprog[0];

        ErWebinar.IsChecked = Aftalen.ErWebinar;
    }

    /// <summary>Halve timer hele døgnet. Feltet kan skrives i, hvis noget andet skal bruges.</summary>
    private static List<string> Tider()
    {
        var ud = new List<string>();

        for (var t = 0; t < 24; t++)
        for (var m = 0; m < 60; m += 30)
            ud.Add($"{t:00}:{m:00}");

        return ud;
    }

    private void Gem_Klik(object sender, RoutedEventArgs e)
    {
        Fejl.Text = "";

        if (Titel.Text.Trim().Length == 0 && Aftalen.KanRettes)
        {
            Fejl.Text = "Aftalen skal have et navn.";
            Titel.Focus();
            return;
        }

        if (Aftalen.KanRettes)
        {
            if (Dato.SelectedDate is not { } dag)
            {
                Fejl.Text = "Vælg en dato.";
                return;
            }

            if (!TimeOnly.TryParse(Fra.Text, out var fra))
            {
                Fejl.Text = "«Fra» skal være et klokkeslæt, fx 09:30.";
                return;
            }

            // SLUT MÅ GERNE MANGLE. Så regnes der med en time — det er bedre
            // end at kraeve et svar, ingen har.
            TimeOnly? til = TimeOnly.TryParse(Til.Text, out var t) ? t : null;

            if (til is { } s && s <= fra)
            {
                Fejl.Text = "Aftalen slutter, før den begynder.";
                return;
            }

            var start = new DateTimeOffset(dag.Date.Add(fra.ToTimeSpan()),
                                           TimeZoneInfo.Local.GetUtcOffset(dag.Date));

            Aftalen.Titel = Titel.Text.Trim();
            Aftalen.Start = start;
            Aftalen.Slut = til is { } u
                ? new DateTimeOffset(dag.Date.Add(u.ToTimeSpan()),
                                     TimeZoneInfo.Local.GetUtcOffset(dag.Date))
                : null;

            // Et link genkendes paa, at det ligner et. Alt andet er et sted.
            var linje = Link.Text.Trim();
            var erLink = linje.StartsWith("http", StringComparison.OrdinalIgnoreCase);

            Aftalen.Link = erLink ? linje : "";
            Aftalen.Sted = erLink ? "" : linje;
        }

        Aftalen.Mappe = (Mappevalg.SelectedItem as Punkt)?.Vaerdi ?? "";
        Aftalen.Moedetype = (Typevalg.SelectedItem as Punkt)?.Vaerdi ?? "";
        Aftalen.Sprog = (Sprogvalg.SelectedItem as Punkt)?.Vaerdi ?? "";
        Aftalen.ErWebinar = ErWebinar.IsChecked == true;

        DialogResult = true;
    }

    /// <summary>
    /// Sletter aftalen. Der spørges — også selv om en aftale er let at lave
    /// igen: har man skrevet mødetype, mappe og sprog på, er det ikke ét felt,
    /// der går tabt.
    /// </summary>
    private void Slet_Klik(object sender, RoutedEventArgs e)
    {
        var besked = Aftalen.KanRettes
            ? "Aftalen forsvinder fra kalenderen. Har du optaget mødet, bliver optagelsen liggende."
            : $"Aftalen kommer fra {Aftalen.Kilde} og kommer igen ved næste hentning. " +
              "Vil du af med den for alvor, skal den slettes dér.";

        var ja = Dialogs.AppDialog.Spoerg(this, "Slet aftalen?", besked,
            godkend: "Slet den", annuller: "Behold den",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        Slettet = true;
        DialogResult = true;
    }

    private void Fortryd_Klik(object sender, RoutedEventArgs e) => DialogResult = false;
}
