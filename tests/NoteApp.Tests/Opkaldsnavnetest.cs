using System.Globalization;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Navnet på et telefonopkald.
/// </summary>
/// <remarks>
/// ============ ET OPKALD HAR INGEN TITEL ============
///
/// Man tager telefonen; man sidder ikke og navngiver den først. Uden et navn
/// stod de alle sammen som «Uden navn», og så kunne to opkald ikke skelnes.
///
/// ============ DET, DER ER LET AT TAGE FEJL AF ============
///
/// «:» i en formatstreng er IKKE et kolon. Det er en pladsholder for kulturens
/// egen tidsadskiller, og dansk bruger PUNKTUM. Uden anførselstegn ville navnet
/// blive «22.45» på en dansk maskine og «22:45» på en engelsk — samme kode, to
/// resultater, og kun det ene er dét, der blev bedt om.
///
/// Det samme gælder «_», som ellers er tavs i en formatstreng.
/// </remarks>
public class Opkaldsnavnetest
{
    private static readonly DateTimeOffset Tidspunkt =
        new(2026, 9, 8, 22, 45, 30, TimeSpan.FromHours(2));

    [Fact]
    public void Navnet_er_dansk_dato_og_klokkeslaet()
    {
        Assert.Equal("08-09-2026_22:45", RecordingSession.Opkaldsnavn(Tidspunkt));
    }

    [Theory]
    [InlineData("da-DK")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Maskinens_sprog_aendrer_ikke_navnet(string kultur)
    {
        var foer = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(kultur);

            Assert.Equal("08-09-2026_22:45", RecordingSession.Opkaldsnavn(Tidspunkt));
        }
        finally
        {
            CultureInfo.CurrentCulture = foer;
        }
    }

    [Fact]
    public void Sekunderne_kommer_ikke_med()
    {
        // «kun timer og minutter». Sekunder er der ingen, der leder efter, og
        // de goer navnet en tredjedel laengere.
        Assert.DoesNotContain("30", RecordingSession.Opkaldsnavn(Tidspunkt));
    }

    [Fact]
    public void Mappenavnet_paa_disken_er_uaendret()
    {
        // ============ NAVNET ER TITLEN, IKKE MAPPEN ============
        //
        // Mapperne paa disken sorteres efter navn, og de skal blive ved med at
        // staa i tidsraekkefoelge. Blev titlen brugt som mappenavn, ville
        // sorteringen skifte til dag-foerst - og en mappe fra 08-09 ville
        // ligge foer en fra 01-10.
        Assert.Equal("2026-09-08_22-45", MeetingStore.Slug(null, Tidspunkt));
    }
}
