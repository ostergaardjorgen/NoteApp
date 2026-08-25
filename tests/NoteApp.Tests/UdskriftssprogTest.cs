using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af det opslag, der afgør, om et færdigt spor kan genbruges.
///
/// Fejler det til den forkerte side, sker der to forskellige ting: siger den
/// «ved ikke», hvor den kunne have svaret, koster det en hel transskription
/// om — målt til otte minutter på et rigtigt møde. Siger den et sprog, den
/// ikke har belæg for, genbruges en tekst på det forkerte sprog, og dét
/// opdages først i referatet.
/// </summary>
public sealed class UdskriftssprogTest
{
    private static string Skriv(string indhold)
    {
        var sti = Path.Combine(Path.GetTempPath(), $"proeve_{Guid.NewGuid():N}.json");
        File.WriteAllText(sti, indhold);
        return sti;
    }

    [Fact]
    public void Laeser_sproget_fra_resultatet()
    {
        var f = Skriv("""
            { "params": { "language": "da" }, "result": { "language": "da" } }
            """);
        try { Assert.Equal("da", Udskriftssprog.Hent(f)); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Resultatet_vinder_over_det_der_blev_bedt_om()
    {
        // Ved «auto» beder man om auto og faar et rigtigt sprog. Det er dét,
        // teksten er skrevet paa, og dermed dét, der afgoer genbrug.
        var f = Skriv("""
            { "params": { "language": "auto" }, "result": { "language": "en" } }
            """);
        try { Assert.Equal("en", Udskriftssprog.Hent(f)); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Falder_tilbage_paa_params_naar_resultatet_mangler()
    {
        var f = Skriv("""{ "params": { "language": "sv" } }""");
        try { Assert.Equal("sv", Udskriftssprog.Hent(f)); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Ukendt_fil_giver_null_og_ikke_et_gaet()
    {
        Assert.Null(Udskriftssprog.Hent(Path.Combine(Path.GetTempPath(), "findes-ikke-42.json")));
    }

    [Fact]
    public void Oedelagt_json_giver_null()
    {
        // Et gaet her ville genbruge en tekst paa et sprog, ingen har
        // efterproevet - praecis den fejl, reglen findes for at undgaa.
        var f = Skriv("{ dette er ikke json");
        try { Assert.Null(Udskriftssprog.Hent(f)); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Json_uden_sprog_giver_null()
    {
        var f = Skriv("""{ "params": { "model": "ggml-large-v3.bin" }, "result": {} }""");
        try { Assert.Null(Udskriftssprog.Hent(f)); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Tomt_sprog_taeller_ikke_som_svar()
    {
        var f = Skriv("""{ "params": { "language": "" }, "result": { "language": "" } }""");
        try { Assert.Null(Udskriftssprog.Hent(f)); }
        finally { File.Delete(f); }
    }
}
