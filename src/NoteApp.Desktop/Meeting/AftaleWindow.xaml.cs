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

    /// <summary>
    /// Sat, hvis der skal optages — eller gås til optagelsen, hvis den findes.
    ///
    /// Vinduet starter ikke selv optagelsen. Det ejer ikke optageren, og en
    /// dialog, der lukker sig selv OG sætter tyve minutters optagelse i gang,
    /// er svær at følge med i. Den, der åbnede vinduet, gør det.
    /// </summary>
    public bool SkalOptage { get; private set; }

    /// <summary>
    /// Skal aftalen også oprettes hos Google?
    ///
    /// DEN LÆSES AF DEN, DER GEMMER — ikke her. Et kald ud på nettet midt i et
    /// gem ville låse vinduet, mens der ventes, og en aftale, der ikke kan
    /// gemmes lokalt, fordi Google er nede, er den forkerte afhængighed.
    /// Aftalen gemmes først; oplægningen er noget, der sker bagefter og kan
    /// gå galt for sig.
    /// </summary>
    public bool SkalOpHosGoogle { get; private set; }

    /// <summary>Skal aftalen have et Google Meet-link?</summary>
    public bool SkalHaveMeet { get; private set; }

    /// <summary>
    /// Skal aftalen åbnes hos Google bagefter, så der kan inviteres gæster?
    ///
    /// GÆSTER INVITERES IKKE HERFRA, og det er et bevidst fravalg. Kontakterne
    /// ligger hos Google, invitationen sendes af Google, og svarene lander hos
    /// Google. En deltagerliste bygget her ville være en dårligere kopi af en
    /// skærm, brugeren kender — og hver eneste adresse skulle tastes forfra.
    /// </summary>
    public bool SkalInvitere { get; private set; }

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

        VisGooglehakket(ny);

        Fyld();
        Loaded += (_, _) => Titel.Focus();
    }

    /// <summary>
    /// Hakket «Opret også i Google Kalender».
    ///
    /// DET VISES KUN, NÅR DER ER EN FORBINDELSE. Et felt, man ikke kan bruge,
    /// er et spørgsmål om hvorfor — og svaret ville være en henvisning til en
    /// anden skærm, midt i en aftale, man er ved at lægge ind.
    ///
    /// DET ER SLÅET FRA SOM UDGANGSPUNKT. At sende noget op er et valg, og et
    /// forudsat ja er ikke et valg. Den, der vil have alle sine aftaler i
    /// Google, sætter hakket hver gang; det er billigere end at opdage, at
    /// noget stod i en delt kalender, man ikke havde tænkt over.
    ///
    /// EN HENTET AFTALE LIGGER DER I FORVEJEN, og en aftale, der allerede er
    /// lagt op, skal ikke op igen — så ville der komme to.
    /// </summary>
    private void VisGooglehakket(bool erNy)
    {
        var kan = false;

        try
        {
            kan = Googleklient.ErSatOp
               && Integrationsfiler.Hent("google").ErForbundet
               && Aftalen.Kilde == Kalenderkilde.Lokal
               && Aftalen.FremmedId.Length == 0;
        }
        catch (Exception)
        {
            // Kan opsaetningen ikke laeses, er svaret «nej». En aftale skal
            // kunne laegges ind, ogsaa naar integrationen driller.
        }

        if (!kan)
        {
            // Er den allerede lagt op, siges det - ellers ser det ud, som om
            // valget forsvandt.
            if (Aftalen.Kilde == Kalenderkilde.Lokal && Aftalen.FremmedId.Length > 0)
            {
                GoogleForklaring.Text = "Aftalen ligger også i Google Kalender.";
                GoogleForklaring.Visibility = Visibility.Visible;
            }

            return;
        }

        OpretHosGoogle.Visibility = Visibility.Visible;
        GoogleForklaring.Visibility = Visibility.Visible;

        if (!erNy) OpretHosGoogle.Content = "Opret den i Google Kalender nu";
    }

    /// <summary>
    /// Betingelsen siges, når hakket sættes — ikke som en advarsel hele tiden.
    ///
    /// At appen skal køre, er kun interessant for den, der faktisk vælger
    /// automatisk optagelse. Står det altid, læses det aldrig.
    /// </summary>
    private void Automatisk_Skiftet(object sender, RoutedEventArgs e)
    {
        AutomatiskNote.Visibility = OptagAutomatisk.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Lægger et Google Meet-link på en aftale, der allerede findes hos Google.
    ///
    /// Aftalen gemmes med det samme. Går man ud af vinduet med Fortryd
    /// bagefter, er linket der stadig — det ER oprettet hos Google, og et
    /// felt, der siger noget andet, ville være forkert.
    /// </summary>
    private async void TilfoejMeet_Klik(object sender, RoutedEventArgs e)
    {
        TilfoejMeetKnap.IsEnabled = false;
        Fejl.Text = "";

        try
        {
            var noegle = Integrationsfiler.Hent("google").Opdateringsnoegle;

            if (noegle.Length == 0)
            {
                Fejl.Text = "Der er ikke forbindelse til Google lige nu.";
                return;
            }

            // Noten kun naar moedet er markeret til at blive optaget.
            // Et moedelink er ikke i sig selv en beslutning om at optage.
            var link = await Googlekalender.TilfoejMeetAsync(
                Aftalen.FremmedId, noegle,
                medNote: OptagAutomatisk.IsChecked == true,
                sprogkode: (Sprogvalg.SelectedItem as Punkt)?.Vaerdi ?? "");

            Aftalen.Link = link;
            Kalender.Gem(Aftalen);

            Link.Text = link;
            VisLinkknap();

            TilfoejMeetKnap.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Fejl.Text = ex.Message;
        }
        finally
        {
            TilfoejMeetKnap.IsEnabled = true;
        }
    }

    /// <summary>
    /// Undervalgene hører til hakket ovenfor og vises kun sammen med det.
    /// </summary>
    private void Google_Skiftet(object sender, RoutedEventArgs e)
    {
        Googlevalg.Visibility = OpretHosGoogle.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// «Åbn mødet» står kun, når der ER et link at åbne.
    ///
    /// Uden den skulle man over i Google Kalender for at trykke på det samme
    /// link — og så er kalenderen her en visning, man alligevel forlader.
    /// </summary>
    private void VisLinkknap()
    {
        var t = Link.Text.Trim();

        AabnLinkKnap.Visibility =
            t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AabnLink_Klik(object sender, RoutedEventArgs e)
    {
        var adresse = Link.Text.Trim();
        if (adresse.Length == 0) return;

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(adresse) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Fejl.Text = "Kunne ikke åbne linket: " + ex.Message;
        }
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

        // KLOKKESLAETTET SAETTES, NAAR VINDUET ER TEGNET.
        //
        // Fra og Til er redigerbare rullelister, og tekstfeltet inde i dem
        // findes foerst, naar skabelonen er anvendt. Saettes .Text i
        // konstruktoeren, forsvinder den igen - og saa stod tiderne TOMME paa
        // en aftale hentet fra Google, hvor de heller ikke kunne rettes.
        // Fundet 24-08-2026.
        var fraTekst = Aftalen.Start.LocalDateTime.ToString("HH:mm");
        var tilTekst = Aftalen.Slutter.LocalDateTime.ToString("HH:mm");

        Loaded += (_, _) =>
        {
            Fra.Text = fraTekst;
            Til.Text = tilTekst;
        };

        // Passer tiden paa en halv time, kan den vaelges direkte. Det virker
        // ogsaa foer skabelonen er anvendt, og saa staar der noget med det
        // samme frem for et blink.
        if (tider.Contains(fraTekst)) Fra.SelectedItem = fraTekst;
        if (tider.Contains(tilTekst)) Til.SelectedItem = tilTekst;

        // EN HENTET AFTALE VISER TIDEN SOM TEKST. Felterne var laaste
        // alligevel, og en laast datovaelger ser ud som noget, der er gaaet i
        // stykker - se den hvide kasse, der gav anledning til det her.
        if (!Aftalen.KanRettes)
        {
            Tidsfelter.Visibility = Visibility.Collapsed;
            Laasttid.Visibility = Visibility.Visible;

            var dag = Aftalen.Start.LocalDateTime;

            Tidslinje.Text = Aftalen.Slut is null
                ? $"{dag:dddd d. MMMM}  ·  {fraTekst}"
                : $"{dag:dddd d. MMMM}  ·  {fraTekst} – {tilTekst}";
        }

        // ARRANGOEREN. Kun naar der ER en - en aftale, man selv har lavet i
        // appen, har ingen.
        if (Aftalen.Arrangoer.Length > 0)
        {
            Arrangoerrude.Visibility = Visibility.Visible;

            Arrangoer.Text = Aftalen.ErEgetMoede
                ? Aftalen.Arrangoer + "  (dig)"
                : Aftalen.Arrangoer;

            // ER MAN SELV ARRANGOER, ER DET ENS EGET ANSVAR AT NAEVNE
            // OPTAGELSEN. Det staar her, hvor man traeffer valget om at optage
            // - ikke i en vejledning, ingen laeser.
            Arrangoernote.Visibility = Aftalen.ErEgetMoede
                ? Visibility.Visible : Visibility.Collapsed;
        }

        Link.Text = Aftalen.Link.Length > 0 ? Aftalen.Link : Aftalen.Sted;

        VisLinkknap();

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

        OptagAutomatisk.IsChecked = Aftalen.OptagAutomatisk;
        AutomatiskNote.Visibility = Aftalen.OptagAutomatisk
            ? Visibility.Visible : Visibility.Collapsed;

        // Meet-knappen: kun paa en hentet aftale, der ikke allerede har et
        // link, og kun naar der ER forbindelse at gaa igennem.
        var kanMeet = false;

        try
        {
            kanMeet = Aftalen.Kilde == Kalenderkilde.Google
                   && Aftalen.FremmedId.Length > 0
                   && Aftalen.Link.Length == 0
                   && Integrationsfiler.Hent("google").ErForbundet;
        }
        catch (Exception)
        {
            // Kan opsaetningen ikke laeses, vises knappen ikke. En knap, der
            // fejler, naar man trykker, er vaerre end en, der ikke er der.
        }

        TilfoejMeetKnap.Visibility = kanMeet ? Visibility.Visible : Visibility.Collapsed;

        // Optageknappen staar ikke paa en ny aftale, man er ved at lave. Man
        // laegger en aftale ind til paa torsdag; skal der optages NU, er der
        // «Optag moede» oeverst i appen.
        OptagKnap.Visibility = Aftalen.Titel.Length > 0
            ? Visibility.Visible : Visibility.Collapsed;

        OptagKnap.Content = Aftalen.MoedeId.Length > 0 ? "Vis optagelsen" : "Optag mødet";
    }

    /// <summary>
    /// Gemmer og beder om at få optagelsen startet.
    ///
    /// DER GEMMES FØRST. Har man rettet mappen og derefter trykket optag,
    /// skal optagelsen bruge den nye mappe — ikke den, der stod, da vinduet
    /// blev åbnet.
    /// </summary>
    private void Optag_Klik(object sender, RoutedEventArgs e)
    {
        SkalOptage = true;

        // Gem_Klik saetter DialogResult og lukker. Fejler valideringen, staar
        // vinduet aabent - og saa skal flaget tages tilbage, ellers ville et
        // senere «Gem aftalen» ogsaa starte en optagelse.
        Gem_Klik(sender, e);

        if (DialogResult != true) SkalOptage = false;
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

        // SLAAS DEN TIL PAA NY, ER DET EN NY LEJLIGHED. «Startet» ryddes, saa
        // vagten optager igen - ellers ville et hak, man saetter tilbage efter
        // en kasseret optagelse, ingenting goere.
        var automatisk = OptagAutomatisk.IsChecked == true;

        if (automatisk && !Aftalen.OptagAutomatisk) Aftalen.Startet = null;

        Aftalen.OptagAutomatisk = automatisk;

        SkalOpHosGoogle = OpretHosGoogle.Visibility == Visibility.Visible
                       && OpretHosGoogle.IsChecked == true;

        SkalHaveMeet = SkalOpHosGoogle && MedMeet.IsChecked == true;
        SkalInvitere = SkalOpHosGoogle && InviterGaester.IsChecked == true;

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
