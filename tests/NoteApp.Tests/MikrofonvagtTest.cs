using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, hvem mikrofonvagten regner for et fremmed program.
///
/// DEN HER PRØVE FINDES PÅ GRUND AF EN BOBLE PÅ SKÆRMEN. Vågeordet lytter med
/// whisper-command.exe — en anden proces, som appen selv starter. Vagten så et
/// fremmed program åbne mikrofonen og spurgte, om mødet skulle optages.
///
/// Appen spurgte altså om sig selv, og det stod i selve boblen:
/// «whisper-command bruger din mikrofon».
///
/// Det er værre end en skønhedsfejl. Vagten er det, der skal fange et RIGTIGT
/// møde. Spørger den også om appens egen lytning, lærer man at klikke den væk
/// — og så er den væk, den dag der er noget at spørge om.
/// </summary>
public class MikrofonvagtTest
{
    private const string Mig = @"C:\NoteApp\app\HeyPia.exe";
    private const string Motor = @"C:\AppNoter\motor";

    /// <summary>Nøglerne i registret skriver stien med # i stedet for \.</summary>
    private static string Noegle(string sti) => sti.Replace('\\', '#');

    [Fact]
    public void Appen_selv_er_ikke_et_fremmed_program()
    {
        Assert.True(Mikrofonvagt.ErEgetProgram(Noegle(Mig), Mig, Motor));
    }

    [Fact]
    public void Vaageordets_motor_er_heller_ikke()
    {
        // DET VAR DEN, DER SPURGTE. whisper-command ligger i appens egen
        // motormappe, og den er startet af appen selv.
        var sti = Motor + @"\whisper\bin\Release\whisper-command.exe";
        Assert.True(Mikrofonvagt.ErEgetProgram(Noegle(sti), Mig, Motor));
    }

    [Fact]
    public void Det_gaelder_alt_i_motormappen()
    {
        // Der sammenlignes med mappen og ikke med et filnavn. En navneliste
        // bliver forkert den dag, motoren skifter navn.
        var sti = Motor + @"\en-helt-anden-motor.exe";
        Assert.True(Mikrofonvagt.ErEgetProgram(Noegle(sti), Mig, Motor));
    }

    [Fact]
    public void Et_rigtigt_moedeprogram_slipper_igennem()
    {
        // Hele pointen med vagten. Bliver den for ivrig efter at sortere fra,
        // tier den om det, den er sat i verden for at fange.
        var teams = @"C:\Users\x\AppData\Local\Microsoft\Teams\current\Teams.exe";
        Assert.False(Mikrofonvagt.ErEgetProgram(Noegle(teams), Mig, Motor));
    }

    [Fact]
    public void En_mappe_der_bare_begynder_ens_er_ikke_vores()
    {
        // «C:\AppNoter\motorvej\...» maa ikke tages for motormappen. Uden
        // skilletegnet ville et program med et navn, der tilfaeldigvis
        // begynder ens, forsvinde ud af vagten.
        var sti = @"C:\AppNoter\motorvej\program.exe";
        Assert.False(Mikrofonvagt.ErEgetProgram(Noegle(sti), Mig, Motor));
    }

    [Fact]
    public void En_pakket_app_er_ikke_vores()
    {
        // Pakkenavne har hverken # eller \. De maa ikke ligne en sti under
        // motormappen ved et uheld.
        Assert.False(Mikrofonvagt.ErEgetProgram("Microsoft.Teams_8wekyb3d8bbwe", Mig, Motor));
    }

    [Fact]
    public void Uden_egen_sti_sorteres_der_stadig_paa_motormappen()
    {
        // ProcessPath kan vaere null. Saa skal motormappen stadig gaelde -
        // ellers kommer boblen tilbage netop dér.
        var sti = Motor + @"\whisper\bin\Release\whisper-command.exe";
        Assert.True(Mikrofonvagt.ErEgetProgram(Noegle(sti), null, Motor));
    }

    // ============================================================ Windows selv

    [Theory]
    [InlineData("windows.immersivecontrolpanel_cw5n1h2txyewy")]   // Indstillinger
    [InlineData("Microsoft.Windows.Cortana_cw5n1h2txyewy")]
    [InlineData("MicrosoftWindows.Client.CBS_cw5n1h2txyewy")]
    public void Windows_egne_apps_er_ikke_et_moede(string pakke)
    {
        // ============ INDSTILLINGER AABNER MIKROFONEN ============
        //
        // Maalt 08-09-2026: staar man paa lydsiden i Indstillinger, holder den
        // mikrofonen aaben, saa laenge siden er fremme - og vagten spurgte, om
        // moedet skulle optages, mens brugeren kiggede paa sine lydenheder.
        Assert.True(Mikrofonvagt.ErWindowsselv(pakke));
    }

    [Theory]
    [InlineData("MSTeams_8wekyb3d8bbwe")]                    // et rigtigt moede
    [InlineData("Microsoft.YourPhone_8wekyb3d8bbwe")]        // Telefonlink: et opkald
    [InlineData("Microsoft.WindowsSoundRecorder_8wekyb3d8bbwe")]
    [InlineData("us.zoom.pwa.videomeetings_v3vsq3knbhqfj")]
    public void Microsofts_oevrige_apps_slipper_igennem(string pakke)
    {
        // ============ DET ER UDGIVEREN, IKKE NAVNET ============
        //
        // «Microsoft» i navnet betyder ingenting. Teams og Telefonlink er
        // Butikkens udgiver-id og ikke styresystemets - og et Teams-moede er
        // praecis dét, vagten er til for at opdage.
        Assert.False(Mikrofonvagt.ErWindowsselv(pakke));
    }

    [Theory]
    [InlineData("")]
    [InlineData("uden-understreg")]
    [InlineData("slutter_med_understreg_")]
    public void En_noegle_uden_udgiver_slipper_igennem(string pakke)
    {
        // Et spoergsmaal for meget er til at leve med. En vagt, der tier paa en
        // noegle, den ikke forstod, er ikke.
        Assert.False(Mikrofonvagt.ErWindowsselv(pakke));
    }
}
