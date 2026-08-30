using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, at en frisk installation taler dansk.
///
/// DEN STOD SOM NULL FØR, og null blev læst som dansk de fleste steder — men
/// ikke alle. Den, der åbnede rullelisten, så et tomt felt og kunne ikke se,
/// hvad appen egentlig gjorde. Og dikteringen sendte slet intet sprog med, så
/// modellen gættede.
///
/// Målt 30-08-2026: «hallo, hallo, hallo» sagt på dansk kom tilbage som
/// «Alors, alors, alors ?». Fransk.
///
/// En standard, der står skrevet, kan ses og laves om. En, der kun findes som
/// en antagelse i koden, kan ingen af delene.
/// </summary>
public class SprogstandardTest
{
    [Fact]
    public void En_frisk_installation_taler_dansk()
    {
        Assert.Equal("da", new AppSettings().MitSprog);
    }

    [Fact]
    public void Brugerfladen_er_ogsaa_dansk_fra_start()
    {
        // Null betyder dansk for brugerfladen - det er den gamle aftale, og
        // den staar beskrevet paa egenskaben. De to skal foelges ad.
        var frisk = new AppSettings();

        Assert.True(frisk.Sprog is null or "da",
            $"brugerfladens sprog var «{frisk.Sprog}» - forventede dansk");
    }

    [Fact]
    public void Et_valgt_sprog_overskrives_ikke_af_standarden()
    {
        var v = new AppSettings { MitSprog = "en" };
        Assert.Equal("en", v.MitSprog);
    }

    [Fact]
    public void Auto_er_stadig_muligt()
    {
        // Den, der holder moeder paa skiftende sprog, skal kunne slaa
        // gaetningen til med vilje. Standarden er bare ikke den.
        var v = new AppSettings { MitSprog = "auto" };
        Assert.Equal("auto", v.MitSprog);
    }
}
