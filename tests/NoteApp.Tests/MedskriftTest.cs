using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af løbende transskription: planlægningen og sammenfletningen.
///
/// De to fejl, der betyder noget, er begge stille. Planlægger den forkert,
/// bliver et halvskrevet segment læst, og så mangler der ord midt i mødet.
/// Fletter den forkert, står hele mødet oven i hinanden i de første minutter —
/// og DET opdages først, når nogen klikker på en replik og hører noget andet.
/// </summary>
public sealed class MedskriftTest
{
    // ------------------------------------------------------------ planlægning

    [Fact]
    public void Venter_til_der_er_en_hel_bid()
    {
        // Ni faerdige segmenter plus eet aabent. Under en hel bid - vent.
        Assert.Null(Medskrift.Naeste(skrevneSegmenter: 10, faerdige: 0, optagerStadig: true));
    }

    [Fact]
    public void Tager_en_bid_naar_der_er_nok()
    {
        // Elleve skrevne: ti lukkede, eet aabent. Nu er der en hel bid.
        var bid = Medskrift.Naeste(11, 0, optagerStadig: true);

        Assert.NotNull(bid);
        Assert.Equal(0, bid!.Value.TekstFra);
        Assert.Equal(10, bid.Value.Til);
    }

    [Fact]
    public void Det_aabne_segment_roeres_aldrig()
    {
        // Det sidste segment bliver skrevet lige nu. Laeses det, faar man en
        // halv WAV - enten en fejl eller et afhugget ord.
        for (var skrevne = 1; skrevne < 40; skrevne++)
        {
            var bid = Medskrift.Naeste(skrevne, 0, optagerStadig: true);
            if (bid is null) continue;

            Assert.True(bid.Value.Til <= skrevne - 1,
                $"bid gik til {bid.Value.Til} af {skrevne} skrevne - det aabne segment blev laest");
        }
    }

    [Fact]
    public void Naar_moedet_er_stoppet_tages_resten_uanset_hvor_lidt()
    {
        // Tre segmenter tilbage er under en bid, men moedet er slut, og saa
        // skal de med. Ellers manglede slutningen af hvert eneste moede.
        var bid = Medskrift.Naeste(23, 20, optagerStadig: false);

        Assert.NotNull(bid);
        Assert.Equal(20, bid!.Value.TekstFra);
        Assert.Equal(23, bid.Value.Til);
    }

    [Fact]
    public void Intet_nyt_giver_ingen_bid()
    {
        Assert.Null(Medskrift.Naeste(20, 20, optagerStadig: false));
        Assert.Null(Medskrift.Naeste(0, 0, optagerStadig: true));
    }

    [Fact]
    public void Der_tages_tilloeb_undtagen_paa_den_foerste()
    {
        // Foerste bid har intet foer sig at tage tilloeb fra.
        var foerste = Medskrift.Naeste(11, 0, optagerStadig: true)!.Value;
        Assert.Equal(0, foerste.LydFra);
        Assert.Equal(0, foerste.KastVaekSekunder);

        // De naeste faar eet segments lyd med fra foer - som tilloeb, ikke tekst.
        var naeste = Medskrift.Naeste(21, 10, optagerStadig: true)!.Value;
        Assert.Equal(9, naeste.LydFra);
        Assert.Equal(10, naeste.TekstFra);
        Assert.Equal(30, naeste.KastVaekSekunder);
    }

    [Fact]
    public void Bidderne_haenger_sammen_uden_huller()
    {
        // Koer et helt moede igennem og se, at hvert eneste segment er med
        // een og kun een gang. Et hul her er tabt tale midt i et moede.
        var faerdige = 0;
        var daekket = new List<int>();

        for (var skrevne = 1; skrevne <= 47; skrevne++)
        {
            while (Medskrift.Naeste(skrevne, faerdige, optagerStadig: true) is { } b)
            {
                for (var i = b.TekstFra; i < b.Til; i++) daekket.Add(i);
                faerdige = b.Til;
            }
        }

        // Moedet stopper: resten med.
        if (Medskrift.Naeste(47, faerdige, optagerStadig: false) is { } sidste)
        {
            for (var i = sidste.TekstFra; i < sidste.Til; i++) daekket.Add(i);
            faerdige = sidste.Til;
        }

        Assert.Equal(47, faerdige);
        Assert.Equal(Enumerable.Range(0, 47).ToList(), daekket);
    }

    // ---------------------------------------------------------- sammenfletning

    private static string Bid(params (int FraMs, int TilMs, string Tekst)[] poster)
    {
        var p = string.Join(",", poster.Select(x => $$"""
            {
              "timestamps": { "from": "{{Medskrift.Stempel(x.FraMs)}}", "to": "{{Medskrift.Stempel(x.TilMs)}}" },
              "offsets": { "from": {{x.FraMs}}, "to": {{x.TilMs}} },
              "text": "{{x.Tekst}}"
            }
            """));

        return $$"""
            { "model": { "type": "large" },
              "params": { "language": "da" },
              "result": { "language": "da" },
              "transcription": [ {{p}} ] }
            """;
    }

    private static List<(long Fra, string Tekst)> Poster(string json)
    {
        using var d = System.Text.Json.JsonDocument.Parse(json);
        return d.RootElement.GetProperty("transcription").EnumerateArray()
            .Select(e => (e.GetProperty("offsets").GetProperty("from").GetInt64(),
                          e.GetProperty("text").GetString() ?? ""))
            .ToList();
    }

    [Fact]
    public void Tiderne_forskydes_saa_moedet_staar_i_raekkefoelge()
    {
        var flettet = Medskrift.Flet(new[]
        {
            new Medskrift.Faerdigbid(Bid((0, 2000, "foerste")), 0, 0),
            new Medskrift.Faerdigbid(Bid((0, 2000, "anden")), 300, 0),
        });

        var poster = Poster(flettet);
        Assert.Equal(2, poster.Count);
        Assert.Equal(0, poster[0].Fra);
        Assert.Equal(300_000, poster[1].Fra);   // fem minutter inde
    }

    [Fact]
    public void Tillobet_smides_vaek_saa_intet_staar_to_gange()
    {
        // Anden bid har tredive sekunders tilloeb. Det, der begynder deri, er
        // allerede skrevet af den foerste.
        var flettet = Medskrift.Flet(new[]
        {
            new Medskrift.Faerdigbid(Bid((0, 30_000, "foerste bid")), 0, 0),
            new Medskrift.Faerdigbid(
                Bid((5_000, 20_000, "det her er tillob"), (31_000, 35_000, "det her er nyt")),
                300, 30),
        });

        var tekster = Poster(flettet).Select(p => p.Tekst).ToList();
        Assert.Contains("foerste bid", tekster);
        Assert.Contains("det her er nyt", tekster);
        Assert.DoesNotContain("det her er tillob", tekster);
    }

    [Fact]
    public void Tiden_regnes_fra_biddens_start_ikke_fra_tillobet()
    {
        // Bidden begynder 300 s inde, og de foerste 30 s er tilloeb. En replik
        // 31 s inde i lyden er altsaa 301 s inde i moedet.
        var flettet = Medskrift.Flet(new[]
        {
            new Medskrift.Faerdigbid(Bid((31_000, 33_000, "her")), 300, 30),
        });

        Assert.Equal(301_000, Poster(flettet)[0].Fra);
    }

    [Fact]
    public void Sproget_og_modellen_foelger_med()
    {
        // Resten af appen laeser de felter. Forsvinder de, kan en flettet
        // udskrift ikke genbruges - se Udskriftssprog.
        var flettet = Medskrift.Flet(new[]
        {
            new Medskrift.Faerdigbid(Bid((0, 1000, "noget")), 0, 0),
        });

        using var d = System.Text.Json.JsonDocument.Parse(flettet);
        Assert.Equal("da", d.RootElement.GetProperty("result").GetProperty("language").GetString());
        Assert.Equal("da", d.RootElement.GetProperty("params").GetProperty("language").GetString());
    }

    [Fact]
    public void Stemplet_skrives_som_whisper_goer_det()
    {
        Assert.Equal("00:00:00,000", Medskrift.Stempel(0));
        Assert.Equal("00:00:01,970", Medskrift.Stempel(1970));
        Assert.Equal("01:02:03,004", Medskrift.Stempel(3_723_004));
    }

    [Fact]
    public void Teksten_kommer_ud_i_raekkefoelge()
    {
        var flettet = Medskrift.Flet(new[]
        {
            new Medskrift.Faerdigbid(Bid((0, 1000, "en"), (1000, 2000, "to")), 0, 0),
            new Medskrift.Faerdigbid(Bid((0, 1000, "tre")), 300, 0),
        });

        Assert.Equal("en\nto\ntre", Medskrift.SomTekst(flettet));
    }

    [Fact]
    public void Ingen_bidder_er_en_fejl_og_ikke_en_tom_udskrift()
    {
        // En tom udskrift ser ud som et moede, hvor ingen sagde noget. Det er
        // vaerre end en fejl, for saa leder man ikke efter lyden.
        Assert.Throws<ArgumentException>(() => Medskrift.Flet(Array.Empty<Medskrift.Faerdigbid>()));
    }
}
