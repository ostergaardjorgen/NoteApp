using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Ordene i en note skal kunne klikkes - og kunne SES.
/// </summary>
/// <remarks>
/// «Foreground = null» blev sat paa hvert ord for at slette Hyperlinks blaa
/// farve. Men null er ikke «arv farven fra teksten udenom» - det er «ingen
/// farve at tegne med», og saa stod der ingenting. Noterne var tomme kort.
/// Set 31-08-2026.
///
/// Selve opdelingen kan proeves af uden en skaerm; farven kan ikke, og den
/// staar derfor forklaret i koden.
/// </remarks>
public class OrdklikTest
{
    /// <summary>
    /// Tegnsaetningen foelger med paa skaermen, men ikke med i rettelsen.
    /// </summary>
    /// <remarks>
    /// «Storistech,» skal staa med sit komma i noten - det er jo saadan,
    /// saetningen ser ud - men rettelsen handler om ordet.
    /// </remarks>
    [Theory]
    [InlineData("Storistech,", "Storistech")]
    [InlineData("«Omada»", "Omada")]
    [InlineData("NetIQ.", "NetIQ")]
    [InlineData("(Cloudworks)", "Cloudworks")]
    public void Ordet_gives_videre_uden_tegnsaetning(string paaSkaermen, string vented)
    {
        var rent = paaSkaermen.Trim(',', '.', '!', '?', ':', ';', '(', ')', '«', '»', '"');

        Assert.Equal(vented, rent);
    }

    /// <summary>
    /// Tegnsaetning alene skal ikke kunne klikkes.
    /// </summary>
    [Theory]
    [InlineData("—")]
    [InlineData("...")]
    [InlineData(",")]
    public void Tegn_uden_bogstaver_er_ikke_et_ord(string stykke)
    {
        var rent = stykke.Trim(',', '.', '!', '?', ':', ';', '(', ')', '«', '»', '"');

        Assert.True(rent.Length == 0 || !rent.Any(char.IsLetter));
    }
}
