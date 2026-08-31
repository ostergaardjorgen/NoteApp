using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Hukommelsen, der goer at man ikke skal vente paa vaageordet.
/// </summary>
public class ForoptagerTest
{
    /// <summary>
    /// Ringen skal daekke den MAALTE ventetid paa motoren.
    /// </summary>
    /// <remarks>
    /// Maalt 31-08-2026: der gik omkring syv sekunder fra «Hej Pia» blev
    /// sagt, til appen reagerede. Med fire sekunders hukommelse var baade
    /// vaageordet og de foerste sekunder af det, brugeren sagde, faldet ud af
    /// ringen, INDEN appen fik besked paa at beholde noget.
    ///
    /// Tallet staar her, saa ingen kan skaere det ned igen uden at se, hvad
    /// det saa smider vaek.
    /// </remarks>
    [Fact]
    public void Ringen_daekker_den_maalte_ventetid_med_margen()
    {
        const double maaltVentetid = 7.0;

        Assert.True(Foroptager.Sekunder >= maaltVentetid * 2,
            $"Ringen er {Foroptager.Sekunder} sek. Den maalte ventetid paa "
            + $"vaageordet er {maaltVentetid} sek, og der skal vaere margen: "
            + "det, der falder ud af ringen, findes ingen steder.");
    }

    [Fact]
    public void Hukommelsen_er_stadig_smaa_penge()
    {
        // 16 kHz i 16 bit er 32 kB pr. sekund. En laengere ring maa ikke
        // blive en undskyldning for at lade mikrofonen fylde i hukommelsen.
        var kb = Foroptager.Sekunder * 32;

        Assert.True(kb <= 1024, $"Ringen fylder {kb:0} kB.");
    }

    [Fact]
    public void Ringen_rummer_det_antal_proever_den_lover()
    {
        var ring = Lydring.Til(Foroptager.Sekunder, 16000);

        Assert.Equal((int)(Foroptager.Sekunder * 16000), ring.Plads);
    }

    /// <summary>
    /// Ringens laengde og det, der TAGES MED, er to ting.
    /// </summary>
    /// <remarks>
    /// Ringen er lang, saa intet kan naa at falde ud, mens motoren taenker.
    /// Men da Behold() tog HELE ringen med, kom der femten sekunders gammel
    /// tale med i hver eneste diktering.
    ///
    /// Maalt 31-08-2026: «Hop, saa er du. Hvorfor skaerer du noget? Hej Pia.
    /// Skaerer du noget nu». Vaageordet staar MIDT i teksten, og alt foer det
    /// er noget, der blev sagt til en anden.
    /// </remarks>
    [Fact]
    public void Der_tages_mindre_med_end_ringen_rummer()
    {
        Assert.True(Foroptager.Bagudsekunder < Foroptager.Sekunder,
            "Tages hele ringen med, kommer gammel tale med i hver diktering.");
    }

    [Fact]
    public void Der_tages_nok_med_til_at_daekke_vaageordet()
    {
        // Vaageordet fylder omkring et sekund, og motoren bedoemmer de sidste
        // halvandet - plus den tid, bedoemmelsen selv tager.
        Assert.True(Foroptager.Bagudsekunder >= 3.0);
    }

    /// <summary>
    /// «Stille» skal maales mod DET rum, man staar i.
    /// </summary>
    /// <remarks>
    /// MAALT 31-08-2026 med radio i rummet: 91 sekunders optagelse gav
    /// teksten «Korsus. Tak.», og 52 sekunder gav «Hvad er Sarah?». De to ord
    /// er ikke en afkortning - det er alt, modellen kunne finde i et minut,
    /// der mest var radio.
    ///
    /// Graensen stod fast paa 0,02. Med en radio koerende laa rummet hele
    /// tiden over det, saa der blev aldrig stille, og dikteringen sluttede
    /// aldrig.
    /// </remarks>
    [Fact]
    public void I_et_tyst_rum_gaelder_det_faste_tal()
    {
        Assert.Equal(Stilhed.Graense, Stilhed.Graensen(0f));
        Assert.Equal(Stilhed.Graense, Stilhed.Graensen(0.001f));
    }

    [Fact]
    public void I_et_rum_med_radio_stiger_graensen_med()
    {
        // Et rum med radio ligger let paa 0,05. Saa skal der mere end 0,02
        // til, foer det er tale - ellers slutter dikteringen aldrig.
        var medRadio = Stilhed.Graensen(0.05f);

        Assert.True(medRadio > Stilhed.Graense);
        Assert.Equal(0.05f * Stilhed.OverGulvet, medRadio, 4);
    }

    [Fact]
    public void Graensen_kan_aldrig_falde_under_det_faste_tal()
    {
        // Et gulv paa nul er ikke en invitation til at hoere alt.
        foreach (var gulv in new[] { 0f, 0.0001f, 0.005f })
            Assert.True(Stilhed.Graensen(gulv) >= Stilhed.Graense);
    }
}
