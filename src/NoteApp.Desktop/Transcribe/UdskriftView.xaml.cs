using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Transcribe;

/// <summary>Én replik, som skærmen viser og redigerer den.</summary>
public sealed class Replikvisning : INotifyPropertyChanged
{
    private readonly Udskriftslinje _linje;
    private readonly Action _aendret;

    public Replikvisning(Udskriftslinje linje, string hvem, Action aendret)
    {
        _linje = linje;
        _aendret = aendret;
        Hvem = hvem;

        Farve = linje.Spor == Samtale.Derfra
            ? new SolidColorBrush(Color.FromRgb(0xC9, 0x8C, 0xF0))
            : new SolidColorBrush(Color.FromRgb(0x5B, 0x9D, 0xF0));
    }

    public Udskriftslinje Linje => _linje;

    public string Tid => _linje.Tid;
    public string Hvem { get; }
    public Brush Farve { get; }

    public Visibility HvemSynlig => Hvem.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

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
/// udgave og rører aldrig den rettede. Bliver de to uenige, er det et
/// spørgsmål til brugeren — ikke noget appen afgør selv.
///
/// TEKSTFILEN ER EN GENGIVELSE
///
/// Dokumenter, søgning og kopiering læser en txt-fil. Den skrives om, hver
/// gang der rettes, ud fra den strukturerede udskrift og de navne, der er sat.
/// Så er der ét sted, sandheden står, og ét sted, den bliver læst.
/// </summary>
public partial class UdskriftView : UserControl
{
    private readonly DispatcherTimer _gemSenere;

    private Udskrift? _udskrift;
    private string? _mappe;
    private string _model = "";
    private MeetingMetadata? _meta;
    private bool _indlæser;

    /// <summary>Hvor hver replik begynder i den gengivne tekst. Til at springe hen til et sted.</summary>
    private readonly List<(int Fra, int Til)> _positioner = new();

    public UdskriftView()
    {
        InitializeComponent();

        // DER GEMMES EFTER EN PAUSE, IKKE VED HVERT TASTETRYK.
        //
        // En udskrift paa en time er hundredvis af replikker. At skrive hele
        // filen ved hvert bogstav ville betyde en diskskrivning i sekundet,
        // mens man skriver - og en fil, der konstant er halvvejs skrevet.
        //
        // 900 ms er laenge nok til, at en saetning bliver faerdig, og kort nok
        // til at man ikke naar at lukke vinduet foerst.
        _gemSenere = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _gemSenere.Tick += (_, _) => { _gemSenere.Stop(); Gem(); };
    }

    /// <summary>Findes der en udskrift at vise?</summary>
    public bool HarUdskrift => _udskrift is not null && _udskrift.Linjer.Count > 0;

    /// <summary>Den gengivne tekst — det, der kopieres og sendes videre.</summary>
    public string Tekst => _udskrift?.SomTekst(_meta?.Talere) ?? "";

    /// <summary>
    /// Viser udskriften for et møde.
    /// </summary>
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
            Hoved.Visibility = Visibility.Collapsed;
            Status.Text = "";
            _indlæser = false;
            return;
        }

        var toSpor = _udskrift.Linjer.Any(l => l.Spor == Samtale.Herfra)
                     && _udskrift.Linjer.Any(l => l.Spor == Samtale.Derfra);

        Hoved.Visibility = Visibility.Visible;
        Navnefelter.Visibility = toSpor ? Visibility.Visible : Visibility.Collapsed;

        NavnHerfra.Text = Navn(Samtale.Herfra);
        NavnDerfra.Text = Navn(Samtale.Derfra);

        Byg();

        Status.Text = _udskrift.ErRettet ? "Rettet" : "Som maskinen skrev den";
        _indlæser = false;
    }

    public void Ryd()
    {
        _gemSenere.Stop();
        _udskrift = null;
        _mappe = null;
        Liste.ItemsSource = null;
        Hoved.Visibility = Visibility.Collapsed;
        Status.Text = "";
    }

    private string Navn(string spor) =>
        _meta?.Talere.TryGetValue(spor, out var n) == true ? n : "";

    private void Byg()
    {
        if (_udskrift is null) return;

        var navne = _meta?.Talere;

        Liste.ItemsSource = _udskrift.Linjer
            .Select(l => new Replikvisning(l, Udskrift.Navn(l.Spor, navne), PaaAendring))
            .ToList();
    }

    // ------------------------------------------------------------- gemning

    private void PaaAendring()
    {
        if (_indlæser) return;

        Status.Text = "Gemmer …";
        _gemSenere.Stop();
        _gemSenere.Start();
    }

    private void Navn_Aendret(object sender, TextChangedEventArgs e)
    {
        if (_indlæser || _meta is null || _mappe is null) return;

        _meta.Talere[Samtale.Herfra] = NavnHerfra.Text.Trim();
        _meta.Talere[Samtale.Derfra] = NavnDerfra.Text.Trim();

        // Navnene staar paa hver replik, saa listen skal bygges om. Det er
        // billigt: der oprettes visningsobjekter, ikke filer.
        Byg();
        PaaAendring();
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
            File.WriteAllText(sti, _udskrift.SomTekst(_meta?.Talere),
                              new System.Text.UTF8Encoding(false));

            Status.Text = _udskrift.ErRettet
                ? $"Rettet · gemt {DateTime.Now:HH:mm:ss}"
                : $"Gemt {DateTime.Now:HH:mm:ss}";
        }
        catch (IOException ex)
        {
            Status.Text = "Kunne ikke gemme";

            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Rettelsen blev ikke gemt",
                $"{ex.Message}\n\nRet igen om lidt — teksten på skærmen er stadig din.",
                Dialogs.Slags.Pas_paa);
        }
    }

    // ---------------------------------------------------------- springet hen

    /// <summary>
    /// Ruller hen til et bestemt tegnnummer i den gengivne tekst.
    ///
    /// Søgningen kender teksten som én streng. Skærmen viser den som replikker.
    /// Her regnes det ene om til det andet: hvilken replik indeholder det
    /// tegnnummer?
    /// </summary>
    public void SpringTil(int position)
    {
        if (_udskrift is null) return;

        BeregnPositioner();

        var nr = _positioner.FindIndex(p => position >= p.Fra && position < p.Til);
        if (nr < 0) return;

        if (Liste.ItemContainerGenerator.ContainerFromIndex(nr) is FrameworkElement raekke)
            raekke.BringIntoView();
    }

    /// <summary>
    /// Hvor hver replik begynder og slutter i den gengivne tekst.
    ///
    /// Skal regnes på nøjagtig samme måde som <see cref="Udskrift.SomTekst"/>
    /// skriver den — ellers peger søgningen ved siden af.
    /// </summary>
    private void BeregnPositioner()
    {
        _positioner.Clear();
        if (_udskrift is null) return;

        var navne = _meta?.Talere;

        var toSpor = _udskrift.Linjer.Any(l => l.Spor == Samtale.Herfra)
                     && _udskrift.Linjer.Any(l => l.Spor == Samtale.Derfra);

        var nu = toSpor ? Samtale.Forklaring(navne).Length : 0;

        foreach (var l in _udskrift.Linjer)
        {
            var hvem = Udskrift.Navn(l.Spor, navne);

            var laengde = 1 + l.Tid.Length + 1                       // [hh:mm:ss]
                          + (hvem.Length > 0 ? 1 + hvem.Length + 1 : 0)
                          + 1 + l.Tekst.Trim().Length
                          + Environment.NewLine.Length * 2;

            _positioner.Add((nu, nu + laengde));
            nu += laengde;
        }
    }
}
