using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, hvornår en besked er læst.
///
/// DEN HER FINDES, FORDI AT ÅBNE KLOKKEN GJORDE ALT LÆST.
///
/// Indtil 28-08-2026 satte klokken et vandmærke, i det øjeblik den blev
/// åbnet. Kiggede man efter den ene besked, man ventede på, forsvandt de to
/// under den — man havde aldrig set dem, og der var ingen vej tilbage.
///
/// Nu skal der trykkes. Enten på beskeden, eller på «Alle læst».
/// </summary>
public class NotifikationsvarslingTest : IDisposable
{
    private readonly DateTimeOffset _vandmaerkeFoer;
    private readonly List<DateTimeOffset> _laesteFoer;

    public NotifikationsvarslingTest()
    {
        // Proeverne skriver i de rigtige indstillinger. De saettes tilbage
        // bagefter - ellers ville en proeve markere brugerens beskeder laest.
        _vandmaerkeFoer = AppSettings.Current.NotifikationerSetTil;
        _laesteFoer = new List<DateTimeOffset>(AppSettings.Current.NotifikationerLaeste);
    }

    public void Dispose()
    {
        AppSettings.Current.NotifikationerSetTil = _vandmaerkeFoer;
        AppSettings.Current.NotifikationerLaeste = _laesteFoer;
    }

    private static Haendelse Besked(DateTimeOffset tid) => new()
    {
        Tid = tid,
        Slags = HaendelseType.Transskription,
        Hvad = "prøve",
    };

    [Fact]
    public void En_besked_nyere_end_vandmaerket_er_ulaest()
    {
        AppSettings.Current.NotifikationerSetTil = DateTimeOffset.Now.AddHours(-1);
        AppSettings.Current.NotifikationerLaeste.Clear();

        Assert.True(Notifikationer.ErNy(Besked(DateTimeOffset.Now)));
    }

    [Fact]
    public void En_besked_under_vandmaerket_er_laest()
    {
        AppSettings.Current.NotifikationerSetTil = DateTimeOffset.Now;
        AppSettings.Current.NotifikationerLaeste.Clear();

        Assert.False(Notifikationer.ErNy(Besked(DateTimeOffset.Now.AddHours(-1))));
    }

    [Fact]
    public void EEN_besked_kan_laeses_uden_at_de_andre_bliver_det()
    {
        // HELE POINTEN. Vandmaerket alene kunne ikke sige «den her, men ikke
        // den under».
        AppSettings.Current.NotifikationerSetTil = DateTimeOffset.Now.AddHours(-2);
        AppSettings.Current.NotifikationerLaeste.Clear();

        var gammel = Besked(DateTimeOffset.Now.AddHours(-1));
        var ny = Besked(DateTimeOffset.Now);

        AppSettings.Current.NotifikationerLaeste.Add(ny.Tid);

        Assert.False(Notifikationer.ErNy(ny));
        Assert.True(Notifikationer.ErNy(gammel));
    }

    [Fact]
    public void Alle_laest_flytter_vandmaerket_og_rydder_listen()
    {
        AppSettings.Current.NotifikationerSetTil = DateTimeOffset.Now.AddHours(-2);
        AppSettings.Current.NotifikationerLaeste.Add(DateTimeOffset.Now.AddMinutes(-5));

        var foer = Besked(DateTimeOffset.Now.AddSeconds(-1));

        Notifikationer.MarkerAlleLaest();

        Assert.False(Notifikationer.ErNy(foer));
        Assert.Empty(AppSettings.Current.NotifikationerLaeste);
    }

    [Fact]
    public void Listen_vokser_ikke_uden_ende()
    {
        // De enkeltvis laeste, der er kommet UNDER vandmaerket, siger ikke
        // laengere noget, vandmaerket ikke selv siger. Uden oprydningen ville
        // indstillingsfilen vokse med hver besked, man trykkede paa.
        AppSettings.Current.NotifikationerSetTil = DateTimeOffset.Now.AddHours(-2);
        AppSettings.Current.NotifikationerLaeste.Clear();

        var ny = Besked(DateTimeOffset.Now.AddMinutes(-30));
        Notifikationer.MarkerLaest(ny);

        Assert.Single(AppSettings.Current.NotifikationerLaeste);

        Notifikationer.MarkerAlleLaest();

        Assert.Empty(AppSettings.Current.NotifikationerLaeste);
    }

    [Fact]
    public void At_markere_den_samme_to_gange_giver_ikke_to_poster()
    {
        AppSettings.Current.NotifikationerSetTil = DateTimeOffset.Now.AddHours(-2);
        AppSettings.Current.NotifikationerLaeste.Clear();

        var h = Besked(DateTimeOffset.Now.AddMinutes(-10));

        Notifikationer.MarkerLaest(h);
        Notifikationer.MarkerLaest(h);

        Assert.Single(AppSettings.Current.NotifikationerLaeste);
    }
}
