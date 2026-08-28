using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, at appen stadig kan finde det, den hed NoteApp.
///
/// DEN HER FINDES PÅ GRUND AF ET SØG-OG-ERSTAT.
///
/// Appen skiftede navn til HeyPia den 28. august 2026. Navneskiftet blev
/// lavet med et gennemløb over 267 forekomster i 86 filer, og det ramte fire
/// steder, hvor navnet ikke var appens eget, men en ADRESSE PÅ NOGET, DER
/// ALLEREDE LÅ PÅ MASKINEN:
///
///   1. Pegefilen %APPDATA%\NoteApp\datasti.txt — den fortæller, hvor
///      brugerens optagelser ligger.
///   2. LegacyRoot — den peger med vilje på FORTIDEN og blev døbt om til
///      nutiden, hvilket er det modsatte af, hvad den er til.
///   3. Skymappen «NoteApp» i OneDrive og Dropbox — brugerens egen mappe.
///   4. «NoteApp-backup» i hjemmemappen — sikkerhedskopierne.
///
/// Ingen af dem ville have givet en fejl. Appen ville være startet, set
/// rigtig ud, og vist en tom liste. Det er den værste slags: den, der ikke
/// siger noget.
///
/// Prøverne her holder de gamle navne i live. Skal de nogensinde fjernes,
/// skal det være en beslutning, nogen tager — ikke noget, et gennemløb gør.
/// </summary>
public class NavneskiftTest
{
    [Fact]
    public void Pegefilens_gamle_placering_er_ikke_doebt_om()
    {
        // LegacyRoot beskriver, hvor data lå FØR. Står der «HeyPia», er
        // sætningen ikke laengere sand.
        Assert.EndsWith(
            Path.Combine("Local", "NoteApp"),
            UserDataPaths.LegacyRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Begge_miljoevariabler_kendes()
    {
        Assert.Equal("HEYPIA_DATA", UserDataPaths.OverrideVariable);
        Assert.Equal("NOTEAPP_DATA", UserDataPaths.GammelOverrideVariable);
    }

    [Fact]
    public void Den_gamle_skymappe_findes_stadig()
    {
        // Mappen tilhoerer brugeren. Appen omdoeber den ikke, fordi den selv
        // har skiftet navn.
        Assert.Contains("HeyPia", Overvaagning.Mappenavne);
        Assert.Contains("NoteApp", Overvaagning.Mappenavne);
        Assert.Equal("HeyPia", Overvaagning.Mappenavne[0]);
    }

    [Fact]
    public void Optageren_og_appen_leder_samme_sted()
    {
        // Fase0Recorder har sin EGEN kopi af opslaget, fordi projektet ikke
        // henviser til NoteApp.Core. En kopi, der siger «hold i takt», holder
        // ikke sig selv i takt.
        var kilde = File.ReadAllText(Path.Combine(Repomappe(), "src", "Fase0Recorder", "Program.cs"));

        Assert.Contains("HEYPIA_DATA", kilde);
        Assert.Contains("NOTEAPP_DATA", kilde);
        Assert.Contains("\"HeyPia\", \"NoteApp\"", kilde);
    }

    [Fact]
    public void Hjaelpen_siger_det_koden_goer()
    {
        // Sagde hjaelpen kun «HeyPia», ville den, der har en NoteApp-mappe
        // fuld af optagelser, tro, hun skulle flytte dem.
        foreach (var sprog in new[] { "da", "en" })
        {
            var tekst = File.ReadAllText(Path.Combine(
                Repomappe(), "src", "NoteApp.Core", "hjaelp", sprog, "60-filer-udefra.md"));

            Assert.Contains("HeyPia", tekst);
            Assert.Contains("NoteApp", tekst);
        }
    }

    private static string Repomappe()
    {
        var mappe = AppContext.BaseDirectory;

        while (mappe is not null && !Directory.Exists(Path.Combine(mappe, "src")))
            mappe = Path.GetDirectoryName(mappe);

        Assert.NotNull(mappe);
        return mappe!;
    }
}
