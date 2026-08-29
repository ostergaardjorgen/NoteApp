using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, hvornår mikrofonen må være åben — og af, hvad der blev sagt.
///
/// DET ER HER, LØSNINGEN BLIVER BILLIG. En mikrofon, der er åben døgnet rundt,
/// er problemet; en, der er åben omkring aftalerne, er den samme funktion til
/// en brøkdel. Vinduet er derfor ikke pynt, og det skal prøves af: et vindue,
/// der aldrig lukker, er lige så galt som et, der aldrig åbner.
///
/// Og kommandoerne: en fejl i ordbogens retter koster et forkert ord i en
/// tekst, man læser igennem. En fejl her STARTER ET PROGRAM.
/// </summary>
public class VaageordTest
{
    private static readonly DateTimeOffset Nu = new(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);

    private static Lyttesvar Svar(
        bool til = true, bool kunVedMoeder = true, bool motor = true,
        bool optager = false, bool laast = false, DateTimeOffset? aftale = null)
        => Vaageord.Skal(til, kunVedMoeder, motor, optager, laast, Nu, aftale);

    // ============================ Hvornår ============================

    [Fact]
    public void Slukket_lytter_ikke()
    {
        Assert.Equal(Lyttesvar.Slukket, Svar(til: false, aftale: Nu));
    }

    [Fact]
    public void Uden_motor_lyttes_der_ikke()
    {
        // En besked om, at vaageordet er taendt, mens der ikke er noget at
        // lytte med, er en loegn man opdager ved at tale forgaeves.
        Assert.Equal(Lyttesvar.IngenMotor, Svar(motor: false, aftale: Nu));
    }

    [Fact]
    public void Under_en_optagelse_lyttes_der_ikke()
    {
        // Optagelsen vinder: vaageordet har intet at starte, og mikrofonen er
        // i brug til noget vigtigere.
        Assert.Equal(Lyttesvar.Optager, Svar(optager: true, aftale: Nu));
    }

    [Fact]
    public void En_laast_maskine_lyttes_der_ikke_paa()
    {
        Assert.Equal(Lyttesvar.Laast, Svar(laast: true, aftale: Nu));
    }

    [Fact]
    public void Optagelse_vejer_tungere_end_laast()
    {
        // Begge dele kan vaere sande. Svaret skal vaere det samme hver gang -
        // ellers staar der skiftende begrundelser paa skaermen.
        Assert.Equal(Lyttesvar.Optager, Svar(optager: true, laast: true, aftale: Nu));
    }

    [Theory]
    [InlineData(-11, false)]   // elleve minutter foer aftalen: for tidligt
    [InlineData(-10, true)]    // praecis paa vinduets kant
    [InlineData(-5, true)]
    [InlineData(0, true)]      // aftalen begynder nu
    [InlineData(5, true)]
    [InlineData(10, true)]     // praecis paa den anden kant
    [InlineData(11, false)]    // elleve minutter efter: for sent
    public void Vinduet_om_aftalen_aabner_og_lukker(int minutterTilAftalen, bool lytter)
    {
        var aftale = Nu.AddMinutes(-minutterTilAftalen);
        var svar = Svar(aftale: aftale);

        Assert.Equal(lytter ? Lyttesvar.Lytter : Lyttesvar.UdenforVindue, svar);
    }

    [Fact]
    public void Ingen_aftale_giver_intet_vindue()
    {
        Assert.Equal(Lyttesvar.UdenforVindue, Svar(aftale: null));
    }

    [Fact]
    public void Uden_vinduet_lyttes_der_altid()
    {
        // Den mulighed skal findes - men den er dyr, og det er dét, resten af
        // proeverne handler om.
        Assert.Equal(Lyttesvar.Lytter, Svar(kunVedMoeder: false, aftale: null));
    }

    [Theory]
    [InlineData(0, Vaageord.StandardFoerMinutter)]
    [InlineData(-5, Vaageord.StandardFoerMinutter)]
    [InlineData(3, 3)]
    [InlineData(9999, Vaageord.StoersteMinutter)]
    public void Et_umuligt_vindue_bliver_til_et_muligt(int valgt, int ventet)
    {
        Assert.Equal(ventet, Vaageord.Minutter(valgt, Vaageord.StandardFoerMinutter));
    }

    // ============================ Selve ordet ============================

    [Theory]
    [InlineData("Hey Pia", "hey pia")]
    [InlineData("  hey   pia  ", "hey pia")]
    [InlineData("HEJ PIA", "hej pia")]
    public void Vaageordet_renses(string ind, string ventet)
    {
        Assert.Equal(ventet, Vaageord.Rens(ind));
    }

    [Theory]
    [InlineData("ok pia", "ok pia")]      // to ord er nok, ogsaa korte
    [InlineData("hey pia", "hey pia")]
    public void To_ord_er_nok(string ind, string ventet)
    {
        Assert.Equal(ventet, Vaageord.Rens(ind));
    }

    [Theory]
    [InlineData("pia")]        // eet ord
    [InlineData("hey")]        // eet ord
    [InlineData("heypia")]     // eet ord, ogsaa selv om det er langt
    [InlineData("")]
    [InlineData(null)]
    public void Et_vaageord_paa_eet_ord_afvises(string? ind)
    {
        // «Pia» alene ville udloese sig selv, hver gang nogen naevner et navn -
        // og en optagelse, der starter af sig selv midt i et moede, er vaerre
        // end ingen vaageord.
        Assert.Equal("", Vaageord.Rens(ind));
    }

    // ============================ Kommandoerne ============================

    private static readonly Kommando[] Liste =
    {
        new("optag møde", Kommandotype.Optag),
        new("optag webinar", Kommandotype.Webinar),
        new("åbn kalenderen", Kommandotype.AabnSkaerm, "cockpit"),
    };

    [Theory]
    [InlineData("optag møde", Kommandotype.Optag)]
    [InlineData("Optag møde.", Kommandotype.Optag)]
    [InlineData("optag webinar", Kommandotype.Webinar)]
    [InlineData("åbn kalenderen", Kommandotype.AabnSkaerm)]
    public void En_kommando_der_blev_sagt_findes(string sagt, Kommandotype ventet)
    {
        var k = Kommandotolk.Find(sagt, Liste);

        Assert.NotNull(k);
        Assert.Equal(ventet, k!.Type);
    }

    [Fact]
    public void Vaageordet_skaeres_vaek_foerst()
    {
        var k = Kommandotolk.Find("hey pia optag møde", Liste, "hey pia");

        Assert.NotNull(k);
        Assert.Equal(Kommandotype.Optag, k!.Type);
    }

    [Fact]
    public void Et_ord_galt_i_et_langt_udtryk_gaar_an()
    {
        var k = Kommandotolk.Find("åbn kalendern", Liste);

        Assert.NotNull(k);
        Assert.Equal("åbn kalenderen", k!.Udtryk);
    }

    [Theory]
    [InlineData("vi skal have optaget mødet på tirsdag")]
    [InlineData("hvad sagde du om kalenderen")]
    [InlineData("god fornøjelse")]
    [InlineData("")]
    public void En_saetning_i_et_moede_bliver_ikke_til_en_kommando(string sagt)
    {
        // DET FARLIGSTE, DER KAN SKE. En sætning, der tilfaeldigvis indeholder
        // ordene, maa ikke starte noget - derfor maales der paa HELE udtrykket
        // og ikke paa, om ordene staar i det.
        Assert.Null(Kommandotolk.Find(sagt, Liste));
    }

    [Fact]
    public void To_kommandoer_der_passer_lige_godt_giver_ingen()
    {
        // Vaelges den foerste, startes det forkerte program halvdelen af
        // gangene.
        var to = new[]
        {
            new Kommando("åbn maler", Kommandotype.AabnProgram, "mspaint"),
            new Kommando("åbn kaler", Kommandotype.AabnProgram, "calc"),
        };

        Assert.Null(Kommandotolk.Find("åbn haler", to));
    }

    [Fact]
    public void Et_kort_udtryk_taaler_mindre_end_et_langt()
    {
        // Et kort udtryk ligner alt for meget.
        Assert.Equal(1, Kommandotolk.Taerskel(5));
        Assert.Equal(2, Kommandotolk.Taerskel(14));
        Assert.True(Kommandotolk.Taerskel(5) < Kommandotolk.Taerskel(30));
    }

    [Fact]
    public void Standardlisten_kan_tolkes()
    {
        // Et udtryk i standardlisten, der ikke kan findes igen, ville vaere en
        // kommando, appen selv har lagt ind, og som aldrig virker.
        foreach (var k in Kommandotolk.Standard)
        {
            var fundet = Kommandotolk.Find(k.Udtryk, Kommandotolk.Standard);

            Assert.NotNull(fundet);
            Assert.Equal(k.Udtryk, fundet!.Udtryk);
        }
    }
}
