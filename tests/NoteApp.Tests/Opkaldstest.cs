using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Telefonopkald: hvad der regnes som en telefon, og hvad der ikke gør.
/// </summary>
/// <remarks>
/// ============ HVORFOR EN LISTE HER ER I ORDEN ============
///
/// Andre steder i koden står der udtrykkeligt, at en liste over navne bliver
/// forkert i stilhed. Forskellen er, hvad en fejl KOSTER: dér afgør listen, om
/// appen tier eller spørger om noget forkert. Her afgør den kun, hvilken folder
/// optagelsen lander i — og den kan trækkes over i en anden med musen.
///
/// Prøverne her passer derfor på det, der ER dyrt: at et Teams-møde ikke
/// pludselig bliver til et telefonopkald, og at et navneskifte på udgiveren
/// ikke stopper det hele.
/// </remarks>
public class Opkaldstest
{
    [Theory]
    [InlineData("Microsoft.YourPhone_8wekyb3d8bbwe")]
    [InlineData("microsoft.yourphone_8wekyb3d8bbwe")]
    public void Telefonlink_er_en_telefon(string pakke) =>
        Assert.True(Opkaldsprogrammer.Er(pakke));

    [Fact]
    public void Udgiveren_maa_gerne_skifte()
    {
        // ============ DER SAMMENLIGNES PAA NAVNEDELEN ============
        //
        // Udgiver-id'et efter understregen kan skifte, hvis Microsoft udgiver
        // appen under en anden konto - og opkald ville saa stille og roligt
        // begynde at lande blandt moederne. Det er ikke en fejl, nogen leder
        // efter.
        Assert.True(Opkaldsprogrammer.Er("Microsoft.YourPhone_enhelrandenkonto"));
    }

    [Theory]
    [InlineData("MSTeams_8wekyb3d8bbwe")]                 // et moede, ikke en telefon
    [InlineData("us.zoom.pwa.videomeetings_v3vsq3knbhqfj")]
    [InlineData("Microsoft.WindowsSoundRecorder_8wekyb3d8bbwe")]
    [InlineData("windows.immersivecontrolpanel_cw5n1h2txyewy")]
    [InlineData("C:#Program Files#HeyPia#HeyPia.exe")]
    public void Alt_andet_er_et_moede(string pakke) =>
        Assert.False(Opkaldsprogrammer.Er(pakke));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ingen_noegle_er_ikke_en_telefon(string? pakke) =>
        Assert.False(Opkaldsprogrammer.Er(pakke));

}
