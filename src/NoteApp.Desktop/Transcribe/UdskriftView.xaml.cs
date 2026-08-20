using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Transcribe;

/// <summary>Én replik, som skærmen viser og redigerer den.</summary>
public sealed class Replikvisning : INotifyPropertyChanged
{
    private readonly Udskriftslinje _linje;
    private readonly Action _aendret;

    public Replikvisning(Udskriftslinje linje, string navn, bool visNavne, Action aendret)
    {
        _linje = linje;
        _aendret = aendret;
        Navn = navn;

        var gaester = linje.Spor == Samtale.Derfra;

        // ÉN PERSON MOD FLERE.
        //
        // Ikonet siger det, et maerkat skulle bruge et ord paa, og det fylder
        // en broekdel. Bag «gaester» kan der sagtens vaere flere personer -
        // det er netop dét, det andet ikon viser uden at skulle forklares.
        //
        // E77B = Contact, E716 = People. Begge efterproevet mod skrifttypens
        // egen tegntabel; et gaet giver en tom firkant.
        Ikon = gaester ? "" : "";

        Farve = gaester
            ? new SolidColorBrush(Color.FromRgb(0xC9, 0x8C, 0xF0))
            : new SolidColorBrush(Color.FromRgb(0x5B, 0x9D, 0xF0));

        Hjaelp = gaester ? "Gæsterne — de øvrige mødedeltagere" : "Dig og dem i samme lokale";

        IkonSynlig = linje.Spor.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NavnSynlig = visNavne && navn.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public Udskriftslinje Linje => _linje;

    public string Tid => _linje.Tid;
    public string Ikon { get; }
    public string Navn { get; }
    public string Hjaelp { get; }
    public Brush Farve { get; }

    public Visibility IkonSynlig { get; }
    public Visibility NavnSynlig { get; }

    public bool Rettet => _linje.Rettet;

    public string Tekst
    {
        get => _linje.Tekst;
        set
        {
            if (_linje.Tekst == value) return;

            // MASKINENS ORD GEMMES, FOERSTE GANG DER RETTES.
            //
            // Ikke for at kunne fortryde - det klarer redigeringen selv - men
            // fordi det er vaerd at kunne se, hvad der stod, naar man et halvt
            // aar senere er i tvivl om, hvorvidt en formulering er ens egen
            // eller maskinens.
            _linje.Oprindelig ??= _linje.Tekst;

            _linje.Tekst = value;
            _linje.Rettet = _linje.Oprindelig != value;

            Meld(nameof(Tekst));
            Meld(nameof(Rettet));

            _aendret();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Meld(string navn) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(navn));
}

/// <summary>
/// Ét felt i navngivningen — én side af mødet, eller én stemme.
///
/// Nøglen er det, navnet gemmes under: «HERFRA», «DERFRA» eller en stemme som
/// «DERFRA#1». Mærkatet er det, der står på skærmen, når man ikke selv har
/// sat et navn — «Mig», «Gæster», «Gæst 2».
/// </summary>
public sealed class Navnefelt : INotifyPropertyChanged
{
    private readonly Action<string, string> _sat;
    private string _navn;

    public Navnefelt(string noegle, string maerkat, string hjaelp, string navn,
                     Action<string, string> sat)
    {
        Noegle = noegle;
        Maerkat = maerkat;
        Hjaelp = hjaelp;
        _navn = navn;
        _sat = sat;
    }

    public string Noegle { get; }
    public string Maerkat { get; }
    public string Hjaelp { get; }

    public string Navn
    {
        get => _navn;
        set
        {
            if (_navn == value) return;

            _navn = value;
            _sat(Noegle, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Navn)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Udskriften, som man kan rette i.
///
/// HVORFOR REDIGERING ER MERE END BEKVEMMELIGHED
///
/// Kan man finpudse teksten selv, kan man i mange tilfælde nøjes med at
/// kopiere det, man skal bruge — uden at bede en sprogmodel om noget, og
/// dermed uden at sende noget ud af maskinen. Det er den eneste funktion i
/// appen, der kan gøre skyen unødvendig for en del af arbejdet.
///
/// HVORDAN RETTELSER OG NYE KØRSLER LEVER SIDE OM SIDE
///
/// Rettelserne ligger i deres egen fil. En ny transskription skriver maskinens
/// udgave og rører aldrig den rettede.
/// </summary>
public partial class UdskriftView : UserControl
{
    private readonly DispatcherTimer _gemSenere;

    private Udskrift? _udskrift;
    private string? _mappe;
    private string _model = "";
    private MeetingMetadata? _meta;
    private bool _indlæser;

    public UdskriftView()
    {
        InitializeComponent();

        // DER GEMMES EFTER EN PAUSE, IKKE VED HVERT TASTETRYK.
        //
        // En udskrift paa en time er hundredvis af replikker. At skrive hele
        // filen ved hvert bogstav ville betyde en diskskrivning i sekundet -
        // og en fil, der konstant er halvvejs skrevet.
        _gemSenere = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _gemSenere.Tick += (_, _) => { _gemSenere.Stop(); Gem(); };
    }

    /// <summary>Den gengivne tekst — det, der kopieres og sendes videre.</summary>
    public string Tekst => _udskrift?.SomTekst(_meta?.Talere) ?? "";

    // -------------------------------------------------------------- visning

    public void Vis(string mappe, string model)
    {
        _indlæser = true;

        _mappe = mappe;
        _model = model;
        _meta = MeetingStore.Load(mappe);
        _udskrift = Udskrift.HentEllerByg(mappe, model);

        if (_udskrift is null)
        {
            Liste.ItemsSource = null;
            RaaTekst.Text = "";
            Hoved.Visibility = Visibility.Collapsed;
            Navnerude.Visibility = Visibility.Collapsed;
            Meld("");
            _indlæser = false;
            return;
        }

        Hoved.Visibility = Visibility.Visible;
        Navnerude.Visibility = Visibility.Collapsed;

        ByggNavnefelter();

        Byg();
        RaaTekst.Text = Raa();
        VisOpsummering();

        Meld(_udskrift.ErRettet ? "Rettet" : "");
        _indlæser = false;
    }

    public void Ryd()
    {
        _gemSenere.Stop();

        _udskrift = null;
        _mappe = null;

        Liste.ItemsSource = null;
        RaaTekst.Text = "";
        OpsumTekst.Text = "";
        OpsumRude.Visibility = Visibility.Collapsed;
        OpsumTom.Visibility = Visibility.Visible;
        Hoved.Visibility = Visibility.Collapsed;
        Navnerude.Visibility = Visibility.Collapsed;
        Meld("");
    }

    private string GemtNavn(string spor) =>
        _meta?.Talere.TryGetValue(spor, out var n) == true ? n : "";

    private void Meld(string tekst) => SoegStatus.Text = tekst;

    /// <summary>Bygger listen — filtreret, hvis der er søgt.</summary>
    private void Byg()
    {
        if (_udskrift is null) return;

        var navne = _meta?.Talere;
        var soeg = Soeg.Text.Trim();

        var linjer = soeg.Length == 0
            ? _udskrift.Linjer
            : _udskrift.Linjer
                .Where(l => l.Tekst.Contains(soeg, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // Navnet vises kun, naar man selv har sat et. Ellers staar ikonet
        // alene - det siger allerede «mig» eller «gaesterne», og et maerkat
        // med det samme ord ville bare fylde.
        var harNavne = navne is not null && navne.Values.Any(v => v.Length > 0);

        Liste.ItemsSource = linjer
            .Select(l => new Replikvisning(l, Udskrift.Navn(l.Spor, navne), harNavne, PaaAendring))
            .ToList();

        IntetFundet.Visibility = linjer.Count == 0 && soeg.Length > 0
            ? Visibility.Visible : Visibility.Collapsed;

        IntetFundet.Text = $"Ingen replikker indeholder «{soeg}».";
    }

    /// <summary>
    /// Maskinens egne ord, uden rettelser.
    ///
    /// Læses fra maskinfilen og ikke fra den rettede. Det er hele pointen: har
    /// man rettet, er spørgsmålet et halvt år senere, om en formulering er ens
    /// egen eller maskinens — og det kan kun besvares, hvis begge findes.
    /// </summary>
    private string Raa()
    {
        if (_mappe is null) return "";

        var maskin = Udskrift.MaskinSti(_mappe, _model);
        if (!File.Exists(maskin)) return "Maskinens udgave findes ikke for denne optagelse.";

        try
        {
            var linjer = System.Text.Json.JsonSerializer
                .Deserialize<List<Udskriftslinje>>(File.ReadAllText(maskin, Encoding.UTF8));

            return linjer is null ? "" : new Udskrift { Linjer = linjer }.SomTekst(_meta?.Talere);
        }
        catch (Exception)
        {
            return "Maskinens udgave kunne ikke læses.";
        }
    }

    // ------------------------------------------------------------- søgning

    /// <summary>
    /// Søgning INDE i udskriften.
    ///
    /// Der filtreres frem for at fremhæve. En udskrift på en time er
    /// hundredvis af replikker, og en markering et sted i den mur kræver, at
    /// man alligevel ruller. Vises kun de replikker, ordet står i, er svaret
    /// på skærmen med det samme — og de kan rettes, mens de står der.
    ///
    /// Filtreringen rører ikke teksten. Ryddes feltet, står alt igen.
    /// </summary>
    private void Soeg_Aendret(object sender, TextChangedEventArgs e)
    {
        SoegPladsholder.Visibility = Soeg.Text.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;

        if (_udskrift is null) return;

        var foer = _indlæser;
        _indlæser = true;
        Byg();
        _indlæser = foer;

        var soeg = Soeg.Text.Trim();

        if (soeg.Length == 0)
        {
            Meld(_udskrift.ErRettet ? "Rettet" : "");
            return;
        }

        var n = Liste.ItemsSource is IEnumerable<Replikvisning> v ? v.Count() : 0;
        Meld(n == 1 ? "1 replik" : $"{n} replikker");
    }

    // --------------------------------------------------------- navngivning

    private void Navne_Klik(object sender, RoutedEventArgs e) =>
        Navnerude.Visibility = Navnerude.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

    /// <summary>
    /// Bygger felterne til navngivningen.
    ///
    /// ANTALLET ER IKKE GIVET PÅ FORHÅND.
    ///
    /// Før var der to felter: «Mig» og «Gæster». Det svarede til de to spor,
    /// og det var alt, appen kunne vide. Har talergenkendelsen skilt stemmerne
    /// ad, er der i stedet ét felt pr. stemme — «Gæst 1», «Gæst 2» — og så
    /// giver ét samlet felt til gæstesiden ingen mening længere.
    ///
    /// Nøglen er stemmen selv («DERFRA#1»), ikke pladsen i rækken. Skrives
    /// udskriften ud igen, hedder stemmerne det samme, og navnene bliver
    /// stående.
    /// </summary>
    private void ByggNavnefelter()
    {
        if (_udskrift is null)
        {
            Navnefelter.ItemsSource = null;
            NavneKnap.Visibility = Visibility.Collapsed;
            return;
        }

        var felter = new List<Navnefelt>();

        var toSpor = _udskrift.Linjer.Any(l => l.Spor == Samtale.Herfra)
                     && _udskrift.Linjer.Any(l => l.Spor == Samtale.Derfra);

        if (toSpor)
            felter.Add(Felt(Samtale.Herfra, "Mig", "Dig — og dem, der sad i samme lokale"));

        var stemmer = _udskrift.Linjer
            .Where(l => l.Stemme is { Length: > 0 })
            .Select(l => l.Stemme!)
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        foreach (var stemme in stemmer)
        {
            var linje = _udskrift.Linjer.First(l => l.Stemme == stemme);

            felter.Add(Felt(stemme,
                Udskrift.Navn(new Udskriftslinje { Spor = linje.Spor, Stemme = stemme }, null),
                "En stemme, maskinen har skilt ud. Sæt navnet på den, du kan høre det er"));
        }

        // Kun naar stemmerne IKKE er skilt ad, giver eet felt til hele
        // gaestesiden mening. Ellers ville det staa og konkurrere med dem.
        if (stemmer.Count == 0 && toSpor)
            felter.Add(Felt(Samtale.Derfra, "Gæster", "De øvrige deltagere. Skriv gerne flere navne"));

        Navnefelter.ItemsSource = felter;
        NavneKnap.Visibility = felter.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        Navneforklaring.Text = stemmer.Count > 0
            ? "Maskinen har skilt stemmerne ad efter, hvordan de lyder — den ved ikke, " +
              "hvem de er. Sæt navn på hver enkelt, så står navnene i udskriften og i " +
              "alt, hvad der laves ud fra den. Den kan tage fejl; ret det, hvor det er galt."
            : "Navnene sættes af dig — maskinen har ikke skilt stemmerne ad her. De " +
              "siger, hvilken SIDE af mødet der talte, og der kan være flere personer " +
              "bag hver af dem.";
    }

    private Navnefelt Felt(string noegle, string maerkat, string hjaelp) =>
        new(noegle, maerkat, hjaelp, GemtNavn(noegle), Navn_Sat);

    private void Navn_Sat(string noegle, string navn)
    {
        if (_indlæser || _meta is null || _mappe is null) return;

        _meta.Talere[noegle] = navn.Trim();

        Byg();
        RaaTekst.Text = Raa();
        PaaAendring();
    }

    // ------------------------------------------------------------ kopiering

    /// <summary>
    /// Knappen åbner menuen. Der er ikke ét rigtigt svar på, hvad «kopiér»
    /// betyder: skal tidsstemplerne med i en mail? Skal taleren med i et
    /// citat? Det afhænger af, hvor teksten skal hen.
    /// </summary>
    private void Kopier_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.ContextMenu is null) return;

        b.ContextMenu.PlacementTarget = b;
        b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        b.ContextMenu.IsOpen = true;
    }

    private void KopierAlt_Klik(object sender, RoutedEventArgs e) => Kopier(true, true);
    private void KopierTid_Klik(object sender, RoutedEventArgs e) => Kopier(true, false);
    private void KopierRen_Klik(object sender, RoutedEventArgs e) => Kopier(false, false);

    private void Kopier(bool medTid, bool medTaler)
    {
        if (_udskrift is null) return;

        var navne = _meta?.Talere;
        var sb = new StringBuilder();

        // Der kopieres DET, DER STAAR PAA SKAERMEN. Har man soegt, er det de
        // fundne replikker - ellers ville knappen goere noget andet end det,
        // man kigger paa.
        var linjer = Liste.ItemsSource is IEnumerable<Replikvisning> vist
            ? vist.Select(v => v.Linje).ToList()
            : _udskrift.Linjer;

        foreach (var l in linjer)
        {
            if (medTid) sb.Append('[').Append(l.Tid).Append("] ");

            if (medTaler)
            {
                var hvem = Udskrift.Navn(l.Spor, navne);
                if (hvem.Length > 0) sb.Append(hvem).Append(": ");
            }

            sb.AppendLine(l.Tekst.Trim());
            if (medTid || medTaler) sb.AppendLine();
        }

        try
        {
            Clipboard.SetText(sb.ToString().TrimEnd() + Environment.NewLine);
            Meld(linjer.Count == 1 ? "1 replik kopieret" : $"{linjer.Count} replikker kopieret");
        }
        catch (Exception)
        {
            // Udklipsholderen kan vaere laast af et andet program et oejeblik.
            Meld("Kunne ikke kopiere — prøv igen");
        }
    }

    // ------------------------------------------------------------- gemning

    private void PaaAendring()
    {
        if (_indlæser) return;

        Meld("Gemmer …");
        _gemSenere.Stop();
        _gemSenere.Start();
    }

    private void Gem()
    {
        if (_udskrift is null || _mappe is null) return;

        try
        {
            // Rettelserne i deres egen fil. Maskinens udgave roeres aldrig.
            _udskrift.GemRettet(_mappe);

            if (_meta is not null) MeetingStore.Save(_mappe, _meta);

            // Gengivelsen skrives om, saa dokumenter og soegning laeser det
            // rettede - ikke det, maskinen sagde.
            var sti = Path.Combine(_mappe, $"udskrift_{_model}.txt");
            File.WriteAllText(sti, _udskrift.SomTekst(_meta?.Talere), new UTF8Encoding(false));

            Meld(_udskrift.ErRettet
                ? $"Rettet · gemt {DateTime.Now:HH:mm:ss}"
                : $"Gemt {DateTime.Now:HH:mm:ss}");
        }
        catch (IOException ex)
        {
            Meld("Kunne ikke gemme");

            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Rettelsen blev ikke gemt",
                $"{ex.Message}\n\nRet igen om lidt — teksten på skærmen er stadig din.",
                Dialogs.Slags.Pas_paa);
        }
    }

    // --------------------------------------------------------- opsummering

    /// <summary>
    /// Viser opsummeringen, hvis der er lavet en.
    ///
    /// ER UDSKRIFTEN RETTET SIDEN, SIGES DET.
    ///
    /// Opsummeringen blev lavet af den tekst, der stod dengang. Har man
    /// rettet i udskriften bagefter, passer de to ikke længere sammen — og
    /// det er ikke til at se på selve teksten. Kontrolsummen kan se det.
    /// </summary>
    private void VisOpsummering()
    {
        if (_mappe is null) { OpsumTom.Visibility = Visibility.Visible; return; }

        var o = Opsummering.Hent(_mappe);

        OpsumRude.Visibility = o is null ? Visibility.Collapsed : Visibility.Visible;
        OpsumTom.Visibility = o is null ? Visibility.Visible : Visibility.Collapsed;

        if (o is null) return;

        OpsumTekst.Text = o.Tekst;
        OpsumHoved.Text = $"Lavet {o.Lavet.LocalDateTime:d. MMMM yyyy 'kl.' HH:mm} · {o.Model}";

        var nu = Kvitteringer.Kontrolsum(_udskrift?.SomTekst(_meta?.Talere) ?? "");
        var uenig = o.UdskriftSum.Length > 0 && o.UdskriftSum != nu;

        OpsumAdvarsel.Visibility = uenig ? Visibility.Visible : Visibility.Collapsed;
        OpsumAdvarsel.Text = uenig
            ? "Udskriften er ændret, siden opsummeringen blev lavet. De to siger ikke nødvendigvis det samme længere — lav den om, hvis rettelserne betyder noget."
            : "";
    }

    private void OpsumKopier_Klik(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(OpsumTekst.Text); Meld("Opsummeringen er kopieret"); }
        catch (Exception) { Meld("Kunne ikke kopiere — prøv igen"); }
    }

    /// <summary>
    /// Laver opsummeringen.
    ///
    /// Der spørges ikke om lov: knappen står under en tekst, der siger, hvad
    /// den gør, og hvor teksten går hen. En dialog oveni ville være at
    /// spørge to gange om det samme.
    /// </summary>
    private async void OpsumLav_Klik(object sender, RoutedEventArgs e)
    {
        if (_mappe is null || _udskrift is null) return;

        var noegle = SkyNoegle.Hent();
        if (noegle is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Sprogmodellen er ikke sat op",
                "En opsummering laves af en sprogmodel hos Mistral. Sæt din API-nøgle ind " +
                "under «AI-modeller» først.\n\nOptagelse og udskrift virker uden.",
                Dialogs.Slags.Valg);
            return;
        }

        var tekst = _udskrift.SomTekst(_meta?.Talere);

        if (tekst.Trim().Length < 200)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Der er ikke nok tekst",
                "Udskriften er for kort til, at en opsummering siger mere end teksten selv.",
                Dialogs.Slags.Valg);
            return;
        }

        OpsumKnap.IsEnabled = false;
        OpsumStatus.Text = "Mistral læser mødet igennem …";

        try
        {
            var titel = _meta?.Title ?? "mødet";

            var svar = await new SkyRunner(noegle).KoerAsync(
                SkyKatalog.Standard,
                Opsummering.Opskrift(),
                $"Mødet hedder «{titel}».\n\nUdskrift:\n{tekst}",
                kilde: _meta?.Id.ToString() ?? "",
                kildeTitel: titel);

            Opsummering.Gem(_mappe, new Opsummeringsdata
            {
                Tekst = svar.Tekst.Trim(),
                Model = SkyKatalog.Standard.Navn,
                UdskriftSum = Kvitteringer.Kontrolsum(tekst)
            });

            VisOpsummering();
            OpsumStatus.Text = "";
            Meld("Opsummeringen er lavet");
        }
        catch (Exception ex)
        {
            OpsumStatus.Text = "";

            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Opsummeringen blev ikke lavet",
                ex.Message + "\n\nAfsendelsen er bogført under Compliance, uanset om den " +
                "nåede frem.", Dialogs.Slags.Pas_paa);
        }
        finally
        {
            OpsumKnap.IsEnabled = true;
        }
    }

    // ---------------------------------------------------------- springet hen

    /// <summary>
    /// Ruller hen til et bestemt tegnnummer i den gengivne tekst.
    ///
    /// Søgningen på tværs kender teksten som én streng; skærmen viser den som
    /// replikker. Her regnes det ene om til det andet.
    /// </summary>
    public void SpringTil(int position)
    {
        if (_udskrift is null) return;

        var navne = _meta?.Talere;

        var toSpor = _udskrift.Linjer.Any(l => l.Spor == Samtale.Herfra)
                     && _udskrift.Linjer.Any(l => l.Spor == Samtale.Derfra);

        var nu = toSpor ? Samtale.Forklaring(navne).Length : 0;

        for (var i = 0; i < _udskrift.Linjer.Count; i++)
        {
            var l = _udskrift.Linjer[i];
            var hvem = Udskrift.Navn(l.Spor, navne);

            var laengde = 1 + l.Tid.Length + 1
                          + (hvem.Length > 0 ? 1 + hvem.Length + 1 : 0)
                          + 1 + l.Tekst.Trim().Length
                          + Environment.NewLine.Length * 2;

            if (position >= nu && position < nu + laengde)
            {
                if (Liste.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement raekke)
                    raekke.BringIntoView();
                return;
            }

            nu += laengde;
        }
    }
}
