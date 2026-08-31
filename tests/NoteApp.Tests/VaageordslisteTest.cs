using System.Linq;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af den liste, motoren får lov at vælge imellem.
///
/// DEN HER PRØVE FINDES PÅ GRUND AF EN MÅLING, DER SÅ RIGTIG UD.
///
/// whisper-command i «guided mode» binder afkodningen til de udtryk, den får
/// udleveret — men den kan ikke svare «ingenting». Den VÆLGER altid et.
///
/// Målt 30-08-2026 med kun to udtryk på listen:
///
///   hej pia = 0.584   hey pia = 0.416
///
/// De to lægger sammen til ét. Med to muligheder er 50 % ren gætning, og der
/// kom en «detektion» hvert 60. millisekund — også når der ikke blev sagt
/// noget. Derfor lokkeord, og derfor en grænse, der følger listens længde.
/// </summary>
public class VaageordslisteTest
{
    [Fact]
    public void Vaageordene_staar_foerst_og_lokkeordene_efter()
    {
        var liste = Vaageordsliste.Byg(new[] { "hej pia", "hey pia" });

        Assert.Equal("hej pia", liste[0]);
        Assert.Equal("hey pia", liste[1]);
        Assert.True(liste.Count > 10, "der skal vaere lokkeord nok til at gaetning ikke betaler sig");
    }

    [Fact]
    public void En_tom_liste_falder_tilbage_paa_standardordet()
    {
        Assert.Contains("hej pia", Vaageordsliste.Byg(new string[0], "da"));
        Assert.Contains("hey pia", Vaageordsliste.Byg(new string[0], "en"));
    }

    /// <summary>
    /// ÉT vaageord ad gangen, ikke begge stavemaader.
    /// </summary>
    /// <remarks>
    /// De to delte sandsynligheden mellem sig - det samme «Hej Pia» gav «hej
    /// pia» 0,584 og «hey pia» 0,416 - og «hey pia» gav det eneste tvivlsomme
    /// udslag, der er maalt: en optagelse, der startede af sig selv paa 0,497,
    /// mens der spillede radio.
    ///
    /// Man siger i forvejen sit eget sprogs udgave. Det var kun appen, der
    /// lyttede efter begge.
    /// </remarks>
    [Fact]
    public void Der_lyttes_kun_efter_ét_vaageord()
    {
        Assert.DoesNotContain("hey pia", Vaageordsliste.Byg(new string[0], "da"));
        Assert.DoesNotContain("hej pia", Vaageordsliste.Byg(new string[0], "en"));
    }

    /// <summary>Norsk og svensk siger det samme som dansk.</summary>
    [Theory]
    [InlineData("no")]
    [InlineData("sv")]
    [InlineData(null)]
    public void De_skandinaviske_sprog_faar_hej(string? sprog)
    {
        Assert.Contains("hej pia", Vaageord.Standardord(sprog));
    }

    [Fact]
    public void Et_lokkeord_der_ligner_et_vaageord_kommer_ikke_med_to_gange()
    {
        var liste = Vaageordsliste.Byg(new[] { "hej med dig" });

        Assert.Single(liste.Where(o => o == "hej med dig"));
    }

    // ======================= graensen =======================

    [Fact]
    public void Graensen_foelger_faktoren()
    {
        // Forholdsreglen gaelder, saa laenge den ligger inde i det maalte
        // spring - se Vaageordsliste.Mindst og Hoejst.
        Assert.Equal(Vaageordsliste.Faktor / 19, Vaageordsliste.Graense(19), 5);
    }

    /// <summary>
    /// Graensen skal have luft til en tilfaeldig stemme i rummet.
    /// </summary>
    /// <remarks>
    /// Ved 1,5 gange gaetning - 0,11 med fjorten udtryk - begyndte appen at
    /// optage af sig selv, mens der spillede radio. Maalt 31-08-2026.
    ///
    /// EN FALSK START ER VAERRE END EN, DER MANGLER. Siger man «Hej Pia»
    /// igen, koster det to sekunder. En optagelse, ingen har bedt om, sender
    /// lyd ud af huset.
    /// </remarks>
    [Fact]
    public void Graensen_har_luft_til_stoej_i_rummet()
    {
        // MAALT: baggrundsstoej rammer vaageordet paa 0,113-0,157. Graensen
        // skal ligge tydeligt over det, uanset hvor lang listen er.
        const double stoejens_top = 0.157;

        foreach (var antal in new[] { 13, 19, 25, 40 })
            Assert.True(Vaageordsliste.Graense(antal) > stoejens_top,
                $"Med {antal} udtryk er graensen {Vaageordsliste.Graense(antal):0.000} "
                + "- og stoejen naar 0,157.");
    }

    /// <summary>
    /// Graensen skal ogsaa slippe de RIGTIGE traef igennem.
    /// </summary>
    /// <remarks>
    /// Maalt paa brugerens maskine: 0,287, 0,308, 0,350, 0,361, 0,366, 0,497.
    /// Springet fra stoejens 0,157 til 0,287 er dét, graensen skal ligge i.
    /// </remarks>
    [Fact]
    public void Graensen_ligger_i_springet_mellem_stoej_og_traef()
    {
        const double svageste_rigtige_traef = 0.287;

        foreach (var antal in new[] { 13, 19, 25, 40 })
            Assert.True(Vaageordsliste.Graense(antal) < svageste_rigtige_traef,
                $"Med {antal} udtryk afviser graensen et rigtigt vaageord.");
    }

    /// <summary>
    /// Det MAALTE vaageord skal komme igennem.
    /// </summary>
    /// <remarks>
    /// 31-08-2026 paa brugerens maskine: «hej pia», sagt tydeligt og hoert
    /// korrekt, fik 0,36. Med den gamle graense paa 0,21 slap den lige
    /// igennem - og et vaageord sagt en anelse svagere gjorde ikke.
    ///
    /// Tallet staar her, saa graensen ikke kan strammes tilbage uden at
    /// nogen ser, hvad den saa afviser.
    /// </remarks>
    [Theory]
    [InlineData(0.497)]  // maalt: de rigtige traef ligger 0,287 og opefter
    [InlineData(0.366)]
    [InlineData(0.287)]  // det svageste maalte rigtige traef
    public void Et_rigtigt_vaageord_i_den_maalte_styrke_taeller(double sikkerhed)
    {
        var fund = new Vaageordsfund("hej pia", sikkerhed);

        Assert.True(Vaageordsliste.Taeller(fund, Vaageord.Standardord("da"), 19));
    }

    /// <summary>
    /// Et lokkeord taeller ikke, uanset hvor sikker motoren er.
    /// </summary>
    /// <remarks>
    /// «nej ikke lige nu» blev maalt til 0,54 - hoejere end det rigtige
    /// vaageord nogensinde naaede. Det er derfor, graensen ikke er det, der
    /// holder lokkeordene ude; det er navnet.
    /// </remarks>
    [Fact]
    public void Et_lokkeord_taeller_ikke_selv_ved_hoej_sikkerhed()
    {
        var fund = new Vaageordsfund("nej ikke lige nu", 0.54);

        Assert.False(Vaageordsliste.Taeller(fund, Vaageord.Standardord("da"), 19));
    }

    [Fact]
    public void Med_kun_eet_udtryk_kan_intet_taelle()
    {
        // Der ville hvert eneste lyd blive til et vaageord. Saa hellere
        // ingenting end alt.
        Assert.Equal(1.0, Vaageordsliste.Graense(1));
    }

    [Fact]
    public void Det_maalte_gaet_paa_to_udtryk_taeller_ikke()
    {
        // PRAECIS DE TAL, DER BLEV MAALT. Med to udtryk paa listen skal der
        // 90 % til, og 0,584 er ikke i naerheden.
        var fund = new Vaageordsfund("hej pia", 0.584216);

        Assert.False(Vaageordsliste.Taeller(fund, new[] { "hej pia" }, antalUdtryk: 2));
    }

    [Fact]
    public void Det_samme_tal_taeller_paa_den_fulde_liste()
    {
        // Med fjorten udtryk er 0,584 langt over graensen paa 0,21 - og saa
        // ER det et vaageord.
        var fund = new Vaageordsfund("hej pia", 0.584216);

        Assert.True(Vaageordsliste.Taeller(fund, new[] { "hej pia" }, antalUdtryk: 14));
    }

    [Fact]
    public void Et_lokkeord_udloeser_ingenting()
    {
        // Ogsaa selv om motoren er meget sikker paa det.
        var fund = new Vaageordsfund("det ved jeg ikke", 0.95);

        Assert.False(Vaageordsliste.Taeller(fund, new[] { "hej pia" }, antalUdtryk: 14));
    }

    // ======================= linjen fra motoren =======================

    [Fact]
    public void Linjen_fra_motoren_kan_laeses()
    {
        // Ordret fra den rigtige udskrift, med ANSI-koderne som motoren
        // skriver dem.
        var linje = "process_command_list: detected command: [1mhej pia[0m | p = 0.584216 | t = 555 ms";

        var fund = Vaageordsliste.Laes(linje);

        Assert.NotNull(fund);
        Assert.Equal("hej pia", fund!.Udtryk);
        Assert.Equal(0.584216, fund.Sikkerhed, 5);
    }

    [Fact]
    public void Andre_linjer_giver_ingenting()
    {
        Assert.Null(Vaageordsliste.Laes(null));
        Assert.Null(Vaageordsliste.Laes(""));
        Assert.Null(Vaageordsliste.Laes("process_command_list: Speech detected! Processing ..."));
        Assert.Null(Vaageordsliste.Laes("process_command_list: listening for a command ..."));
    }

    [Fact]
    public void Farvekoder_skaeres_vaek()
    {
        Assert.Equal("hej pia", Vaageordsliste.Udenfarver("[1mhej pia[0m"));
        Assert.Equal("uden koder", Vaageordsliste.Udenfarver("uden koder"));
    }

    // ============ VARIANTERNE LAGT SAMMEN ============

    /// <summary>
    /// To stavemaader af det samme ord deler sikkerheden.
    /// </summary>
    /// <remarks>
    /// Maalt 30-08-2026: det SAMME «Hej Pia» gav «hej pia» 0,584 og «hey pia»
    /// 0,416. Hver for sig ser de svage ud; lagt sammen er de 1,0.
    /// </remarks>
    [Fact]
    public void To_stavemaader_af_samme_ord_lgges_sammen()
    {
        var fund = new[]
        {
            new Vaageordsfund("hej pia", 0.13),
            new Vaageordsfund("hej pia", 0.11),
        };

        // Hver for sig er de under graensen; sammen er de over.
        Assert.False(Vaageordsliste.Taeller(fund[0], Vaageord.Standardord("da"), 13));
        Assert.False(Vaageordsliste.Taeller(fund[1], Vaageord.Standardord("da"), 13));

        // Sammen er de over.
        Assert.True(Vaageordsliste.Taeller(fund, Vaageord.Standardord("da"), 19));
    }

    /// <summary>
    /// Lokkeord taeller ikke med i summen, uanset hvor hoejt de scorer.
    /// </summary>
    /// <remarks>
    /// Det er dét, der goer sammenlaegningen ufarlig. Kunne lokkeordene
    /// taelle med, ville summen altid naa en hvilken som helst graense, og
    /// saa ville alt udloese vaageordet.
    /// </remarks>
    [Fact]
    public void Lokkeord_taeller_ikke_med_i_summen()
    {
        var fund = new[]
        {
            new Vaageordsfund("nej ikke lige nu", 0.54),
            new Vaageordsfund("det lyder godt", 0.30),
            new Vaageordsfund("hej pia", 0.02),
        };

        Assert.False(Vaageordsliste.Taeller(fund, Vaageord.Standardord("da"), 19));
    }

    [Fact]
    public void Ingen_fund_udloeser_ingenting()
    {
        Assert.False(Vaageordsliste.Taeller(
            Array.Empty<Vaageordsfund>(), Vaageord.Standardord("da"), 13));
    }

    // ============ DEN GEMTE LISTE ============

    /// <summary>
    /// En gemt liste magen til den gamle standard er ikke et valg.
    /// </summary>
    /// <remarks>
    /// «hej pia, hey pia» stod i brugerens indstillinger, fordi det VAR
    /// standarden dengang - ikke fordi nogen havde skrevet den. En gemt liste
    /// vinder over standarden, saa skiftet til ét ord pr. sprog naaede aldrig
    /// frem til den, der havde brugt appen inden.
    ///
    /// Og det var netop «hey pia», der udloeste hvert eneste falske udslag,
    /// der er maalt 31-08-2026: radioen paa 0,497 og et NYS paa 0,367. Alle
    /// de rigtige traef kom via «hej pia».
    /// </remarks>
    [Fact]
    public void Den_gamle_standard_giver_plads_til_den_nye()
    {
        var gemt = new[] { "hej pia", "hey pia" };

        Assert.Equal(new[] { "hej pia" }, Vaageord.Valgte(gemt, "da"));
        Assert.Equal(new[] { "hey pia" }, Vaageord.Valgte(gemt, "en"));
    }

    [Fact]
    public void Raekkefoelgen_i_den_gemte_liste_er_ligegyldig()
    {
        Assert.Equal(new[] { "hej pia" },
            Vaageord.Valgte(new[] { "Hey Pia", "HEJ PIA" }, "da"));
    }

    /// <summary>
    /// Men et rigtigt valg staar ved magt.
    /// </summary>
    /// <remarks>
    /// Har man selv skrevet noget, maa appen ikke lave det om. Det er kun det
    /// gamle standardpar, der giver plads.
    /// </remarks>
    [Fact]
    public void Et_selvvalgt_ord_staar_ved_magt()
    {
        Assert.Equal(new[] { "goddag pia" },
            Vaageord.Valgte(new[] { "goddag pia" }, "da"));

        Assert.Equal(new[] { "hej pia", "goddag pia" },
            Vaageord.Valgte(new[] { "hej pia", "goddag pia" }, "da"));
    }

    [Fact]
    public void Ingen_gemt_liste_giver_sprogets_ord()
    {
        Assert.Equal(new[] { "hej pia" }, Vaageord.Valgte(null, "da"));
        Assert.Equal(new[] { "hej pia" }, Vaageord.Valgte(new string[0], "da"));
    }
}
