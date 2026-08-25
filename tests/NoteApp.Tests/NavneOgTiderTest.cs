using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Mappenavne og tidsangivelser — de små funktioner, brugeren ser resultatet
/// af hver eneste dag.
/// </summary>
public class NavneOgTiderTest
{
    private static readonly DateTimeOffset Tidspunkt =
        new(2026, 8, 24, 14, 30, 0, TimeSpan.FromHours(2));

    // ===================== MAPPENAVNET =====================

    [Fact]
    public void Uden_titel_er_navnet_bare_tidsstemplet() =>
        Assert.Equal("2026-08-24_14-30", MeetingStore.Slug(null, Tidspunkt));

    [Fact]
    public void Tomme_titler_giver_ogsaa_bare_tidsstemplet() =>
        Assert.Equal("2026-08-24_14-30", MeetingStore.Slug("   ", Tidspunkt));

    [Fact]
    public void Danske_bogstaver_overlever()
    {
        // æ, ø og aa er ikke tegn, der skal renses vaek. Et moede om
        // «Ærøfærgen» skal hedde det paa disken ogsaa.
        var navn = MeetingStore.Slug("Ærøfærgen og Blåvand", Tidspunkt);

        Assert.Contains("Ærøfærgen", navn);
        Assert.Contains("Blåvand", navn);
    }

    [Theory]
    [InlineData("Møde: strategi/2026")]
    [InlineData("Hvad? Nu! Igen*")]
    [InlineData("A\\B|C<D>E")]
    public void Tegn_der_ikke_maa_staa_i_et_filnavn_bliver_til_bindestreger(string titel)
    {
        var navn = MeetingStore.Slug(titel, Tidspunkt);

        // DER SAMMENLIGNES PAA TEGN, IKKE PAA STRENGE.
        //
        // Assert.DoesNotContain(string, string) sammenligner KULTURFOELSOMT,
        // og ICU regner styretegn som ignorerbare. En soegning efter "\0"
        // rammer derfor i en hvilken som helst streng, og proeven fejlede paa
        // en Slug, der var helt ren. Fejlen var proevens, ikke appens -
        // efterproevet 24-08-2026.
        foreach (var ulovligt in Path.GetInvalidFileNameChars())
            Assert.True(navn.IndexOf(ulovligt) < 0,
                        $"navnet «{navn}» indeholder U+{(int)ulovligt:X4}");
    }

    [Fact]
    public void Meget_lange_titler_klippes()
    {
        var navn = MeetingStore.Slug(new string('a', 300), Tidspunkt);

        // Windows har en graense; en titel paa 300 tegn maa ikke goere mappen
        // umulig at oprette.
        Assert.True(navn.Length < 100, $"navnet blev {navn.Length} tegn");
    }

    [Fact]
    public void To_moeder_i_samme_minut_faar_samme_navn()
    {
        // Det er MED VILJE og haandteres af den, der opretter mappen -
        // Indlaesning.LedigMappe laegger «-2» paa. Proeven staar her, saa
        // ingen «retter» Slug til at vaere unik og dermed goer datoen forkert.
        Assert.Equal(
            MeetingStore.Slug("Statusmøde", Tidspunkt),
            MeetingStore.Slug("Statusmøde", Tidspunkt));
    }

    // ===================== «SIDST HENTET» =====================

    [Fact]
    public void Aldrig_hentet_siges_ligeud() =>
        Assert.Equal("aldrig hentet", Synkronisering.Siden(null));

    [Fact]
    public void Lige_hentet_siges_som_lige_nu() =>
        Assert.Equal("hentet lige nu", Synkronisering.Siden(DateTimeOffset.Now.AddSeconds(-5)));

    [Fact]
    public void Inden_for_den_foerste_time_taelles_der_i_minutter()
    {
        var svar = Synkronisering.Siden(DateTimeOffset.Now.AddMinutes(-12));

        Assert.Contains("12 min", svar);
    }

    [Fact]
    public void Senere_paa_dagen_staar_klokkeslaettet()
    {
        var svar = Synkronisering.Siden(DateTimeOffset.Now.AddHours(-3));

        Assert.StartsWith("hentet kl.", svar);
    }

    [Fact]
    public void I_gaar_siges_som_i_gaar()
    {
        var i_gaar = DateTime.Today.AddDays(-1).AddHours(14);

        Assert.Contains("i går", Synkronisering.Siden(new DateTimeOffset(i_gaar)));
    }

    [Fact]
    public void Laengere_tilbage_faar_en_dato()
    {
        var svar = Synkronisering.Siden(DateTimeOffset.Now.AddDays(-9));

        Assert.DoesNotContain("i går", svar);
        Assert.Contains("kl.", svar);
    }
}
