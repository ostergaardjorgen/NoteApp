using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Bjaelken skal beskrive dét, appen goer.
/// </summary>
/// <remarks>
/// EN BESKED, DER PASSER PAA EN ANDEN FREMGANGSMAADE, ER VAERRE END INGEN.
///
/// Vaageordet brugte genvejstastens besked: «Lytter - tal, og slip TASTEN
/// naar du er faerdig». Der er ingen tast efter et vaageord; dikteringen
/// slutter af sig selv paa en pause.
///
/// Brugeren skrev det ind som en note 31-08-2026: «det virker faktisk, som om
/// den reagerer rigtigt, men man kan ikke se det paa den groenne bar».
/// </remarks>
public class DikteringsbeskedTest
{
    [Theory]
    [InlineData("da")]
    [InlineData("en")]
    public void Vaageordet_har_sin_egen_besked(string sprog)
    {
        Sprog.Skift(sprog);

        var tast = Sprog.T("diktering.lytter");
        var vaage = Sprog.T("diktering.lytter_vaageord");

        Assert.NotEqual(tast, vaage);
        Assert.NotEqual("diktering.lytter_vaageord", vaage);
    }

    /// <summary>
    /// Vaageordets besked maa ikke naevne en tast, der ikke er der.
    /// </summary>
    [Theory]
    [InlineData("da", "tast")]
    [InlineData("en", "key")]
    public void Vaageordets_besked_naevner_ingen_tast(string sprog, string ord)
    {
        Sprog.Skift(sprog);

        Assert.DoesNotContain(ord, Sprog.T("diktering.lytter_vaageord"),
            System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Og den skal sige, hvordan den saa slutter.
    /// </summary>
    /// <remarks>
    /// Uden det ved man ikke, hvornaar man er faerdig - og saa staar man og
    /// taler videre, mens appen for laengst er gaaet i gang med at skrive ud.
    /// </remarks>
    [Theory]
    [InlineData("da", "pause")]
    [InlineData("en", "pause")]
    public void Vaageordets_besked_siger_hvordan_den_slutter(string sprog, string ord)
    {
        Sprog.Skift(sprog);

        Assert.Contains(ord, Sprog.T("diktering.lytter_vaageord"),
            System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Hvileteksten skal sige, at man ikke behoever vente.
    /// </summary>
    /// <remarks>
    /// «jeg begynder at tale uden at vide, om den er klar til at modtage» -
    /// brugerens egen note 31-08-2026.
    ///
    /// Svaret er, at den ER klar: foroptageren husker femten sekunder bagud,
    /// saa det, man siger FOER beskeden skifter, er med alligevel. Det stod
    /// bare ikke nogen steder, og saa staar man og venter paa en besked, man
    /// ikke behoever.
    /// </remarks>
    [Fact]
    public void Hvileteksten_siger_at_det_sagte_er_med()
    {
        Sprog.Skift("da");

        var t = Sprog.T("vaageord.klar");

        Assert.Contains("er med", t);
        Assert.Contains("Hej Pia", t);
    }

    /// <summary>
    /// Og det skal vaere SANDT: ringen skal raekke.
    /// </summary>
    /// <remarks>
    /// Loeftet i teksten hviler paa foroptagerens laengde. Skaeres den ned,
    /// bliver teksten en paastand, der ikke holder - og saa mister man tekst
    /// uden at vide det.
    /// </remarks>
    [Fact]
    public void Loeftet_hviler_paa_ringen_og_ringen_raekker()
    {
        // Vaageordet hoeres foerst, naar man holder pause, og motoren
        // bedoemmer et vindue paa 1,5 sekund. Selv en lang saetning skal
        // kunne staa i ringen imens.
        Assert.True(Foroptager.Sekunder >= 10);
    }
}
