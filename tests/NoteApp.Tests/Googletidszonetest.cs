using System.Globalization;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Tidspunktet, der sendes til Google Kalender. Se Googlekalender.Tidspunkt.
/// </summary>
/// <remarks>
/// Set 18-09-2026: Windows kalder zonen «Romance Standard Time», og Google
/// afviste hver ny aftale med 400, «Invalid time zone definition».
/// </remarks>
public class Googletidszonetest
{
    private static readonly TimeZoneInfo Romance = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    [Theory]
    [InlineData("DK", "Europe/Copenhagen")]
    [InlineData("FR", "Europe/Paris")]
    [InlineData(null, "Europe/Paris")]       // uden land: zonens hovedby
    public void Windowsnavnet_bliver_til_et_IANA_navn(string? region, string forventet) =>
        Assert.Equal(forventet, Googlekalender.Tidszone(Romance, region));

    [Fact]
    public void Et_IANA_navn_sendes_uaendret() =>
        Assert.Equal("Europe/Copenhagen",
            Googlekalender.Tidszone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"), "DK"));

    [Fact]
    public void Denne_maskines_zone_er_et_navn_Google_kender()
    {
        var zone = Googlekalender.Tidszone(TimeZoneInfo.Local, "DK");

        Assert.NotNull(zone);
        Assert.Contains('/', zone);
        Assert.DoesNotContain("Standard Time", zone);
    }

    [Fact]
    public void Tidspunktet_har_kolon_ogsaa_paa_en_dansk_maskine()
    {
        var foer = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("da-DK");

            var t = Googlekalender.Tidspunkt(
                new DateTimeOffset(2026, 9, 18, 9, 30, 0, TimeSpan.FromHours(2)), "Europe/Copenhagen");

            Assert.Equal("2026-09-18T09:30:00+02:00", t["dateTime"]);
            Assert.Equal("Europe/Copenhagen", t["timeZone"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = foer;
        }
    }

    [Fact]
    public void Uden_zone_sendes_kun_tidspunktet()
    {
        var t = Googlekalender.Tidspunkt(DateTimeOffset.Now, null);

        Assert.False(t.ContainsKey("timeZone"));
    }
}
