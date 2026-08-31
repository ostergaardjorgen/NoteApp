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
}
