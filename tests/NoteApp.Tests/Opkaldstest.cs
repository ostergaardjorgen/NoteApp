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

    // ============ TELEFONENS LYDENHED ============
    //
    // Maalt 10-09-2026 paa en parret iPhone. Telefonlink stod aldrig paa
    // mikrofonlisten. Et opkald kendes paa, at Windows' lydtjeneste optager
    // fra mikrofonen - og kun naar en telefon er parret. Det er den sidste
    // halvdel, der proeves her. Se Telefonlyd.

    [Theory]
    [InlineData(@"BTHENUM\{0000111f-0000-1000-8000-00805f9b34fb}_VID&0001004c_PID&7807\7&2a22be62&0&44A10EB22CB4_C00000000")]
    [InlineData(@"BTHENUM\{0000111F-0000-1000-8000-00805F9B34FB}_VID&0001004c_PID&7807\7&2a22be62&0&44A10EB22CB4_C00000000")]
    public void En_telefon_kendes_paa_sin_bluetoothtjeneste(string instans) =>
        Assert.True(Telefonlyd.ErTelefonsti(instans));

    [Theory]
    // Et headset udbyder «Hands-Free» (0x111E), ikke gatewayen. Et Teams-moede
    // i et Bluetooth-headset er et moede.
    [InlineData(@"BTHENUM\{0000111e-0000-1000-8000-00805f9b34fb}_VID&0002000a_PID&0001\7&1&0&001122334455_C00000000")]
    // Lydenheden og dens foraelder har ikke tjenesten i sig - den staar to led oppe.
    [InlineData(@"SWD\MMDEVAPI\{0.0.1.00000000}.{79F10930-868E-4F39-9067-545058A5B794}")]
    [InlineData(@"BTHHFENUM\BthHFPAudio\8&2f8a23d0&0&97")]
    [InlineData(@"USB\VID_0B0E&PID_0422\0000")]          // Jabra SPEAK 510
    public void Andre_enheder_er_ikke_en_telefon(string instans) =>
        Assert.False(Telefonlyd.ErTelefonsti(instans));
}
