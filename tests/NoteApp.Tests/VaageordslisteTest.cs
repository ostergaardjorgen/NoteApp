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
    public void En_tom_liste_falder_tilbage_paa_standardordene()
    {
        var liste = Vaageordsliste.Byg(new string[0]);

        Assert.Contains("hej pia", liste);
        Assert.Contains("hey pia", liste);
    }

    [Fact]
    public void Et_lokkeord_der_ligner_et_vaageord_kommer_ikke_med_to_gange()
    {
        var liste = Vaageordsliste.Byg(new[] { "hej med dig" });

        Assert.Single(liste.Where(o => o == "hej med dig"));
    }

    // ======================= graensen =======================

    [Fact]
    public void Graensen_er_tre_gange_gaetning()
    {
        // Fjorten udtryk: gaetning er 7 %, graensen 21 %.
        Assert.Equal(3.0 / 14, Vaageordsliste.Graense(14), 5);
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
}
