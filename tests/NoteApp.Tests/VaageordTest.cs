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
    private static Lyttesvar Svar(
        bool til = true, bool motor = true, bool optager = false, bool laast = false)
        => Vaageord.Skal(til, motor, optager, laast);

    // ============================ Hvornår ============================

    [Fact]
    public void Ved_en_laast_op_skaerm_lyttes_der()
    {
        // DET ER REGLEN. Man siger «Hej Pia», naar man har brug for det, og
        // det foelger ikke moedernes tidsplan.
        Assert.Equal(Lyttesvar.Lytter, Svar());
    }

    [Fact]
    public void Slukket_lytter_ikke()
    {
        Assert.Equal(Lyttesvar.Slukket, Svar(til: false));
    }

    [Fact]
    public void Uden_motor_lyttes_der_ikke()
    {
        // En besked om, at vaageordet er taendt, mens der ikke er noget at
        // lytte med, er en loegn man opdager ved at tale forgaeves.
        Assert.Equal(Lyttesvar.IngenMotor, Svar(motor: false));
    }

    [Fact]
    public void Under_en_optagelse_lyttes_der_ikke()
    {
        // Gaelder ogsaa et onlinemoede: dér optages der, og saa skal der ikke
        // lyttes efter kommandoer.
        Assert.Equal(Lyttesvar.Optager, Svar(optager: true));
    }

    [Fact]
    public void En_laast_maskine_lyttes_der_ikke_paa()
    {
        // Der sidder ingen. At lytte ville vaere at lytte til et tomt kontor.
        Assert.Equal(Lyttesvar.Laast, Svar(laast: true));
    }

    [Fact]
    public void Optagelse_vejer_tungere_end_laast()
    {
        // Begge dele kan vaere sande. Svaret skal vaere det samme hver gang -
        // ellers staar der skiftende begrundelser paa skaermen.
        Assert.Equal(Lyttesvar.Optager, Svar(optager: true, laast: true));
    }

    [Fact]
    public void Slukket_vejer_tungest_af_alt()
    {
        // Har man slaaet det fra, er begrundelsen «det er slaaet fra» - ikke
        // «der optages». Det er dét, man har gjort.
        Assert.Equal(Lyttesvar.Slukket, Svar(til: false, motor: false, optager: true, laast: true));
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
