using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af det opslag, der afgør, HVILKET møde man er ved at optage.
///
/// Rammer det forkert, arver optagelsen en anden aftales mappe, mødetype og
/// sprog. Sproget er det farlige: rammer det forkert, bliver hele
/// transskriptionen vrøvl, og det opdages først i referatet.
///
/// Rammer det INTET, er man tilbage ved at blive spurgt om det samme, man
/// allerede har skrevet — og det var hele grunden til, at opslaget kom.
/// </summary>
public sealed class IGangNuTest
{
    private static readonly DateTimeOffset Nu = new(2026, 8, 27, 10, 15, 0, TimeSpan.FromHours(2));

    private static Aftale A(string titel, int startMin, int laengdeMin, string moedeId = "") =>
        new()
        {
            Titel = titel,
            Start = Nu.AddMinutes(startMin),
            Slut = Nu.AddMinutes(startMin + laengdeMin),
            MoedeId = moedeId
        };

    [Fact]
    public void Et_moede_der_koerer_findes()
    {
        var a = A("Tina", -15, 30);          // begyndt for et kvarter siden, slutter om 15
        Assert.Equal("Tina", Kalender.IGangNu(new[] { a }, Nu)?.Titel);
    }

    [Fact]
    public void Man_maa_trykke_optag_fem_minutter_foer()
    {
        // Man trykker optag, mens folk kommer ind.
        var a = A("Tina", 4, 30);
        Assert.Equal("Tina", Kalender.IGangNu(new[] { a }, Nu)?.Titel);
    }

    [Fact]
    public void Seks_minutter_foer_er_for_tidligt()
    {
        var a = A("Tina", 6, 30);
        Assert.Null(Kalender.IGangNu(new[] { a }, Nu));
    }

    [Fact]
    public void Et_kvarter_efter_slut_taeller_stadig()
    {
        // Et moede traekker tit ud, og man kan komme til at trykke sent.
        var a = A("Tina", -40, 45);          // sluttede for fem minutter siden
        Assert.Equal("Tina", Kalender.IGangNu(new[] { a }, Nu)?.Titel);
    }

    [Fact]
    public void En_halv_time_efter_slut_taeller_ikke()
    {
        var a = A("Tina", -60, 30);          // sluttede for en halv time siden
        Assert.Null(Kalender.IGangNu(new[] { a }, Nu));
    }

    [Fact]
    public void Et_moede_der_allerede_er_optaget_taeller_ikke()
    {
        // Trykker man optag igen, er det en NY optagelse - ikke en
        // fortsaettelse. Saa skal den ikke arve noget.
        var a = A("Tina", -15, 30, moedeId: "abc123");
        Assert.Null(Kalender.IGangNu(new[] { a }, Nu));
    }

    [Fact]
    public void Af_to_der_koerer_vinder_den_der_begyndte_senest()
    {
        // Det er som regel det, man er paa vej ind i; det forrige er ved at
        // vaere forbi.
        var tidlig = A("Formiddagsmøde", -50, 60);
        var sen = A("Tina", -5, 30);

        Assert.Equal("Tina", Kalender.IGangNu(new[] { tidlig, sen }, Nu)?.Titel);
    }

    [Fact]
    public void Et_begyndt_moede_slaar_et_der_lige_skal_til()
    {
        var begyndt = A("Tina", -5, 30);
        var straks = A("Næste", 3, 30);

        Assert.Equal("Tina", Kalender.IGangNu(new[] { begyndt, straks }, Nu)?.Titel);
    }

    [Fact]
    public void Ingen_aftaler_giver_ingenting()
    {
        Assert.Null(Kalender.IGangNu(Array.Empty<Aftale>(), Nu));
    }

    [Fact]
    public void Et_moede_i_morgen_taeller_ikke()
    {
        var a = A("I morgen", 24 * 60, 30);
        Assert.Null(Kalender.IGangNu(new[] { a }, Nu));
    }
}
