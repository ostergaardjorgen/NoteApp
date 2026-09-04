using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af de to paletter.
///
/// De findes, fordi en farve, der er valgt med øjet, kan være for svag uden at
/// se for svag ud for den, der valgte den. Første palet lå omkring 4:1 på
/// brødteksten og blev opdaget i hånden en måned senere. Anden gang blev to
/// farver i det mørke tema fanget her, før nogen så dem.
///
/// Kravene er WCAG 2.1 AA: 4,5:1 for brødtekst, 3:1 for grafiske skel. Tekst
/// holdes over 7:1, som er AAA — der læses lange stræk i den her app.
/// </summary>
public sealed class TemaTest
{
    public static TheoryData<string, IReadOnlyDictionary<string, string>> Paletterne => new()
    {
        { "lys", Tema.Lys },
        { "mørk", Tema.Moerk }
    };

    // ----------------------------------------------------------- formlen

    [Fact]
    public void Sort_mod_hvid_er_enogtyve()
    {
        // Yderpunktet. Rammer den ikke det, er formlen forkert, og alt
        // herunder maaler ingenting.
        Assert.Equal(21.0, Tema.Kontrast("#FF000000", "#FFFFFFFF"), 1);
    }

    [Fact]
    public void Samme_farve_mod_sig_selv_er_een()
    {
        Assert.Equal(1.0, Tema.Kontrast("#FF3A6EA5", "#FF3A6EA5"), 3);
    }

    [Fact]
    public void Raekkefoelgen_er_ligegyldig()
    {
        Assert.Equal(Tema.Kontrast("#FF14161A", "#FFF4F6FA"),
                     Tema.Kontrast("#FFF4F6FA", "#FF14161A"), 6);
    }

    [Fact]
    public void Farven_kan_skrives_med_og_uden_alfa()
    {
        Assert.Equal(Tema.Kanaler("#FFAABBCC"), Tema.Kanaler("#AABBCC"));
    }

    // ------------------------------------------------- begge paletter

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Paletten_har_alle_noeglerne(string navn, IReadOnlyDictionary<string, string> p)
    {
        // MANGLER EN NOEGLE, VAELTER TEMAET. Skaermbillederne slaar op paa
        // navn, og WPF kaster paa en noegle, der ikke findes - det tog et helt
        // vindue ned 18-08-2026.
        foreach (var n in Tema.Nøglerne)
            Assert.True(p.ContainsKey(n), $"{navn}-paletten mangler «{n}»");

        Assert.Equal(Tema.Nøglerne.Count, p.Count);
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Ingen_farve_er_gennemsigtig(string navn, IReadOnlyDictionary<string, string> p)
    {
        foreach (var (n, f) in p)
            Assert.True(Tema.ErUigennemsigtig(f), $"{navn}/{n} er gennemsigtig: {f}");
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Broedteksten_kan_laeses_paa_begge_flader(string navn, IReadOnlyDictionary<string, string> p)
    {
        // MOD BEGGE FLADER. Teksten staar baade paa baggrunden og oven paa et
        // panel, og den svageste af de to er den, der gaelder. Det var praecis
        // den skelnen, der manglede, da Optager lige akkurat klarede
        // baggrunden og faldt igennem paa panelet.
        foreach (var n in new[] { "TekstSvag", "TekstMeget", "Accent", "Optager", "Godkendt", "Advarsel", "FejlTekst" })
        {
            var vaerst = Math.Min(Tema.Kontrast(p[n], p["Baggrund"]),
                                  Tema.Kontrast(p[n], p["Panel"]));

            Assert.True(vaerst >= 4.5, $"{navn}/{n} er {vaerst:0.00}:1 — kravet er 4,5:1");
        }
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Den_almindelige_tekst_gaar_efter_syv(string navn, IReadOnlyDictionary<string, string> p)
    {
        // AAA og ikke AA. Det er den farve, referaterne staar i.
        var vaerst = Math.Min(Tema.Kontrast(p["Tekst"], p["Baggrund"]),
                              Tema.Kontrast(p["Tekst"], p["Panel"]));

        Assert.True(vaerst >= 7.0, $"{navn}/Tekst er {vaerst:0.00}:1 — kravet er 7:1");
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Feltkanterne_kan_findes(string navn, IReadOnlyDictionary<string, string> p)
    {
        // Kanten om et skrivefelt er det eneste, der siger, at man kan skrive
        // der. WCAG 1.4.11 kraever 3:1 for netop den slags — OGSAA hvilekanten,
        // som er den daempede af de to. Det var hele grunden til, at der
        // pludselig var to: én farve paa alle felter hele tiden er ingen
        // besked, og saa kan man ikke se, hvor markoeren staar.
        foreach (var n in new[] { "InputKant", "InputFokusKant" })
        {
            var vaerst = Math.Min(Tema.Kontrast(p[n], p["Baggrund"]),
                                  Tema.Kontrast(p[n], p["Panel"]));

            Assert.True(vaerst >= 3.0, $"{navn}/{n} er {vaerst:0.00}:1 — kravet er 3:1");
        }
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Hvilekanten_er_daempet_i_forhold_til_fokuskanten(string navn,
        IReadOnlyDictionary<string, string> p)
    {
        // Pointen med to kanter er FORSKELLEN. Er de lige tydelige, er der
        // ingen besked i, at den ene lyser op — og saa er vi tilbage ved den
        // gamle FeltKant, der stod paa alt hele tiden.
        Assert.NotEqual(p["InputKant"], p["InputFokusKant"]);
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Handling_og_succes_er_ikke_den_samme_farve(string navn,
        IReadOnlyDictionary<string, string> p)
    {
        // DEN HER FANDTES IKKE, OG DET KOSTEDE HELE FARVESPROGET.
        //
        // I det moerke tema var Accent #02C39A og Godkendt #02C59B. Groen
        // betoed derfor paa een gang «tryk her», «du staar her», «den
        // arbejder» og «det gik godt» — og en farve, der betyder fire ting,
        // betyder ingenting.
        //
        // Kravet er ikke stort: de to skal bare kunne SES som to farver.
        // 1,3:1 i lysstyrke er nok, naar de ogsaa har hver sin kuloer, og
        // hoejere ville tvinge den ene til at vaere markant moerkere end den
        // anden uden grund.
        var v = Tema.Kontrast(p["Accent"], p["Godkendt"]);
        var kuloer = Kulørafstand(p["Accent"], p["Godkendt"]);

        Assert.True(v >= 1.3 || kuloer >= 0.20,
            $"{navn}: Accent og Godkendt er for ens — {v:0.00}:1 i lysstyrke, "
            + $"{kuloer:0.00} i kuloer. Accent er handling, Godkendt er succes.");
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Teksten_paa_de_farvede_flader_kan_laeses(string navn,
        IReadOnlyDictionary<string, string> p)
    {
        // En knap i accentfarve med tekst ovenpaa. Det var netop dét,
        // regnestykket afviste, da maerkets groenne skulle have vaeret Accent
        // i det lyse tema: hvid skrift paa 2,3:1 kan ikke laeses.
        foreach (var (paa, flade) in new[]
                 {
                     ("PaaAccent", "Accent"),
                     ("PaaOptager", "Optager"),
                     ("PaaFremhaev", "Fremhaev")
                 })
        {
            var v = Tema.Kontrast(p[paa], p[flade]);
            Assert.True(v >= 4.5, $"{navn}: {paa} paa {flade} er {v:0.00}:1 — kravet er 4,5:1");
        }
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Sidebjaelken_kan_laeses(string navn, IReadOnlyDictionary<string, string> p)
    {
        // SIDEBJAELKEN ER MOERKEBLAA I BEGGE TEMAER, ogsaa det lyse. Derfor
        // kan teksten derinde ikke hente sin farve i «Tekst» — den er naesten
        // sort paa lyst og ville vaere usynlig paa bjaelken.
        //
        // Der maales mod BEGGE flader: bjaelken selv og det valgte punkt.
        // Det valgte punkt er lysere, og det er dér, en for lys tekst falder
        // igennem foerst.
        foreach (var flade in new[] { "NavigationFlade", "NavigationValgt", "NavigationKant" })
        {
            var fuld = Tema.Kontrast(p["PaaNavigation"], p[flade]);
            Assert.True(fuld >= 4.5,
                $"{navn}: PaaNavigation paa {flade} er {fuld:0.00}:1 — kravet er 4,5:1");

            var svag = Tema.Kontrast(p["PaaNavigationSvag"], p[flade]);
            Assert.True(svag >= 4.5,
                $"{navn}: PaaNavigationSvag paa {flade} er {svag:0.00}:1 — kravet er 4,5:1");
        }
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Det_valgte_menupunkt_kan_ses(string navn, IReadOnlyDictionary<string, string> p)
    {
        // «Hvor er jeg?» er sidebjaelkens eneste opgave. Ligner det valgte
        // punkt bjaelken, er den opgave ikke loest.
        Assert.NotEqual(p["NavigationFlade"], p["NavigationValgt"]);
        Assert.NotEqual(p["NavigationValgt"], p["NavigationKant"]);
    }

    /// <summary>
    /// Hvor langt de to farver ligger fra hinanden i kulør — 0 er samme,
    /// 1 er modsat.
    /// </summary>
    /// <remarks>
    /// Kontrast måler LYSSTYRKE og kun det. Blå og grøn kan ligge på samme
    /// lysstyrke og stadig være to tydeligt forskellige farver, og et krav om
    /// kontrast alene ville derfor tvinge den ene til at være mørkere end den
    /// anden uden grund. Her måles afstanden i selve farven i stedet.
    /// </remarks>
    private static double Kulørafstand(string a, string b)
    {
        var (ar, ag, ab) = Tema.Kanaler(a);
        var (br, bg, bb) = Tema.Kanaler(b);

        return Math.Sqrt(((ar - br) * (ar - br)
                        + (ag - bg) * (ag - bg)
                        + (ab - bb) * (ab - bb)) / 3.0);
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Teksten_kan_laeses_paa_de_tre_tilstandsflader(string navn, IReadOnlyDictionary<string, string> p)
    {
        // Musen over en raekke, en knap trykket ned, en valgt fane: teksten
        // staar oven paa alle tre, og en flade, der er valgt for at fremhaeve
        // noget, maa ikke skjule det, den fremhaever.
        foreach (var flade in new[] { "Svaev", "Trykket", "Valgt" })
        {
            var v = Tema.Kontrast(p["Tekst"], p[flade]);
            Assert.True(v >= 4.5, $"{navn}: Tekst paa {flade} er {v:0.00}:1 — kravet er 4,5:1");
        }
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Slukket_tekst_kan_stadig_laeses(string navn, IReadOnlyDictionary<string, string> p)
    {
        // WCAG undtager deaktiverede kontroller. Vi goer det ikke: kan man
        // ikke laese, hvad knappen hedder, kan man heller ikke regne ud,
        // hvorfor den er graa. 3:1 er nok til at kunne laese den og stadig se,
        // at den er slaaet fra.
        var vaerst = Math.Min(Tema.Kontrast(p["Slukket"], p["Baggrund"]),
                              Tema.Kontrast(p["Slukket"], p["Panel"]));

        Assert.True(vaerst >= 3.0, $"{navn}/Slukket er {vaerst:0.00}:1 — kravet er 3:1");
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Tilstandsfladerne_kan_skelnes_fra_panelet(string navn, IReadOnlyDictionary<string, string> p)
    {
        // En svaeveflade, der ligner panelet, er ingen svaeveflade. Kravet er
        // ikke stort - man skal kunne SE, at musen er der, ikke laese det.
        foreach (var flade in new[] { "Svaev", "Trykket", "Valgt" })
            Assert.True(p[flade] != p["Panel"], $"{navn}/{flade} er den samme som Panel");
    }

    [Theory]
    [MemberData(nameof(Paletterne))]
    public void Panelet_kan_ses_at_ligge_oven_paa_baggrunden(string _, IReadOnlyDictionary<string, string> p)
    {
        // Ikke et WCAG-krav, men et designkrav: er de to flader ens, er der
        // ingen inddeling, og siden bliver én stor plade.
        Assert.NotEqual(p["Baggrund"], p["Panel"]);
    }

    // --------------------------------------------- de to er forskellige

    [Fact]
    public void Det_lyse_tema_er_lyst_og_det_moerke_moerkt()
    {
        // Lyder tosset at proeve. Men paletterne er to ordboeger med de samme
        // noegler, og en fejl, hvor den ene bliver kopieret ind over den
        // anden, ser ud som ingenting i en diff.
        Assert.True(Tema.Kontrast(Tema.Lys["Baggrund"], "#FF000000")
                  > Tema.Kontrast(Tema.Moerk["Baggrund"], "#FF000000"));

        Assert.True(Tema.Kontrast(Tema.Lys["Tekst"], "#FFFFFFFF")
                  > Tema.Kontrast(Tema.Moerk["Tekst"], "#FFFFFFFF"));
    }

    [Fact]
    public void Palet_vaelger_den_rigtige()
    {
        Assert.Same(Tema.Lys, Tema.Palet(lyst: true));
        Assert.Same(Tema.Moerk, Tema.Palet(lyst: false));
    }
}
