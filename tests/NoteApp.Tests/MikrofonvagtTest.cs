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
}
