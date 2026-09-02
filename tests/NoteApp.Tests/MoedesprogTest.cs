using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Et tomt sprog er ikke et fravalg.
/// </summary>
/// <remarks>
/// MEDSKRIVNINGEN KOERTE ALDRIG, OG DET KOSTEDE EN HEL TIME.
///
/// Den starter kun for et spor, der har et sprog. Blev der ikke valgt et, da
/// moedet begyndte, stod der en TOM streng i moedefilen - ikke null, ikke
/// «auto», bare tom - og saa sprang den over. Begge spor.
///
/// Maalt 02-09-2026 paa et moede paa 62 minutter: ValgtSprogMik og
/// ValgtSprogLoop stod begge tomme, mappen «segmenter» var tom, og moedet
/// stod som «ikke skrevet ud». Lyden var der - to spor, 119 MB hver, med tale
/// paa begge - men intet var skrevet ned undervejs.
///
/// Det er den SAMME faelde som i dikteringen samme dag.
/// </remarks>
public class MoedesprogTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void De_oevriges_sprog_falder_tilbage_paa_mit(string? gemt)
    {
        var v = AppSettings.Current;
        v.MitSprog = "da";
        v.DeresSprog = gemt;

        Assert.Equal("da", v.Deresprog);
    }

    [Fact]
    public void Et_valgt_sprog_staar_ved_magt()
    {
        var v = AppSettings.Current;
        v.MitSprog = "da";
        v.DeresSprog = "en";

        Assert.Equal("en", v.Deresprog);
    }

    /// <summary>
    /// «auto» er et VALG og skal blive staaende.
    /// </summary>
    /// <remarks>
    /// Man kan med vilje lade modellen gaette paa modpartens sprog - et
    /// dansk-norsk moede giver dansk paa det ene spor og norsk paa det andet,
    /// og det er det rigtige svar. Det maa ikke laves om til dansk.
    /// </remarks>
    [Fact]
    public void Auto_er_et_valg_og_ikke_et_manglende_svar()
    {
        var v = AppSettings.Current;
        v.MitSprog = "da";
        v.DeresSprog = "auto";

        Assert.Equal("auto", v.Deresprog);
    }

    [Fact]
    public void Begge_spor_har_altid_et_sprog_at_skrive_med()
    {
        var v = AppSettings.Current;
        v.MitSprog = null;
        v.DeresSprog = null;

        // Uden det springer medskrivningen sporet over, og der bliver
        // ingenting skrevet ned undervejs.
        Assert.NotEmpty(v.Talesprog);
        Assert.NotEmpty(v.Deresprog);
    }
}
