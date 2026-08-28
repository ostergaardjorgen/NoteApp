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
    public void Feltkanten_kan_findes(string navn, IReadOnlyDictionary<string, string> p)
    {
        // Kanten om et skrivefelt er det eneste, der siger, at man kan skrive
        // der. WCAG 1.4.11 kraever 3:1 for netop den slags.
        var vaerst = Math.Min(Tema.Kontrast(p["FeltKant"], p["Baggrund"]),
                              Tema.Kontrast(p["FeltKant"], p["Panel"]));

        Assert.True(vaerst >= 3.0, $"{navn}/FeltKant er {vaerst:0.00}:1 — kravet er 3:1");
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
