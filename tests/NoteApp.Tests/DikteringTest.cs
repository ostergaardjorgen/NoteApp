using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af dikteringen.
///
/// Svarformen herunder er ikke opfundet. Den er KOPIERET FRA ET RIGTIGT SVAR
/// fra api.eu.mistral.ai den 28-08-2026, hvor et klip på 12 sekunder blev
/// sendt til voxtral-mini-latest. En prøve på en form, man har gættet, går
/// igennem lige indtil den dag, den skal bruges.
/// </summary>
public class DikteringTest
{
    [Fact]
    public void Vejen_ud_er_det_europaeiske_endepunkt()
    {
        // Loeftet om europaeisk bearbejdning staar og falder med vaerten.
        Assert.StartsWith("https://api.eu.mistral.ai/", Voxtral.Endepunkt);
        Assert.EndsWith("/v1/audio/transcriptions", Voxtral.Endepunkt);

        // Og den skal AFVISE alt andet - ikke bare tilfaeldigvis pege rigtigt.
        Assert.Throws<InvalidOperationException>(
            () => SkyKatalog.KraevEuropa("https://api.mistral.ai/v1/audio/transcriptions"));
    }

    /// <summary>
    /// Svaret herunder er KLIPPET UD AF ET RIGTIGT SVAR, felt for felt.
    ///
    /// Den første udgave af den her prøve var opdigtet. Der stod
    /// «audio_seconds», fordi det lød rigtigt, og prøven bestod. Feltet hedder
    /// «prompt_audio_seconds», og fejlen viste sig først, da kæden blev kørt
    /// på et klip og skrev «0 sek lyd» om tolv sekunders tale.
    ///
    /// «language» kommer tilbage som null fra denne model. Det er ikke en
    /// fejl, og det må ikke vælte noget.
    /// </summary>
    [Fact]
    public void Svaret_laeses_som_det_faktisk_kom()
    {
        var json = """
        {
          "model": "voxtral-mini-latest",
          "text": " Ja. Men jeg tror, du kan være helt sikker på.",
          "language": null,
          "segments": [],
          "usage": {
            "prompt_audio_seconds": 12,
            "prompt_tokens": 3,
            "total_tokens": 419,
            "completion_tokens": 41,
            "prompt_tokens_details": { "cached_tokens": 0, "audio_tokens": 375 },
            "service_tier": "standard"
          },
          "finish_reason": "stop"
        }
        """;

        var r = Dikteringsklient.Laes(json);

        Assert.Equal("Ja. Men jeg tror, du kan være helt sikker på.", r.Raa);
        Assert.Equal("", r.Sprog);
        Assert.Equal(12, r.Sekunder);
    }

    [Fact]
    public void Det_gamle_feltnavn_rammes_ogsaa()
    {
        // Skifter leverandoeren navn igen, er det bedre at ramme det gamle
        // end at vise nul sekunder om et helt minuts tale.
        var r = Dikteringsklient.Laes("""{"text":"hej","usage":{"audio_seconds":7}}""");

        Assert.Equal(7, r.Sekunder);
    }

    [Fact]
    public void Manglende_sprog_vaelter_ingenting()
    {
        // «language» kom tilbage som null i den foerste proeve. Et diktat maa
        // ikke gaa tabt, fordi modellen ikke ville sige, hvilket sprog det var.
        var r = Dikteringsklient.Laes("""{"text":"hej","language":null}""");

        Assert.Equal("hej", r.Raa);
        Assert.Equal("", r.Sprog);
        Assert.Equal(0, r.Sekunder);
    }

    [Theory]
    [InlineData(Dikteringsformaal.Note)]
    [InlineData(Dikteringsformaal.Mail)]
    [InlineData(Dikteringsformaal.Prompt)]
    [InlineData(Dikteringsformaal.Opgave)]
    public void Hvert_formaal_forbyder_at_der_findes_paa(Dikteringsformaal formaal)
    {
        // En sprogmodel, der faar lov, skriver en indledning, du ikke har sagt
        // - og i en mail, du sender videre, staar din underskrift under den.
        //
        // FORBUDDET LAA I HVER ENKELT INSTRUKTION FOER. Det holdt ikke: mail-
        // instruktionen bad samtidig om en indledning og en afslutning, og
        // saa blev formen fyldt ud med en opdigtet opgaveliste. Nu staar
        // reglen ét sted og saettes foran dem alle - se Voxtral.Grundregel.
        var p = Voxtral.Pudseprompt(formaal);

        Assert.StartsWith(Voxtral.Grundregel, p);
        Assert.Contains("må ikke tilføje oplysninger", p);
        Assert.Contains("RÅ UDSKRIFT ER DEN ENESTE KILDE", p);
    }

    [Fact]
    public void Formaalene_giver_forskellige_instruktioner()
    {
        // Hele pointen med diktering frem for transskription er, at formen
        // foelger opgaven. Giver to formaal samme instruktion, er der en, der
        // ikke virker - og det ses ikke paa teksten, foer den er sendt.
        var alle = Enum.GetValues<Dikteringsformaal>()
                       .Select(f => Voxtral.Pudseprompt(f))
                       .ToList();

        Assert.Equal(alle.Count, alle.Distinct().Count());
    }

    [Fact]
    public void Opgaven_bliver_een_linje_i_bydeform()
    {
        var p = Voxtral.Pudseprompt(Dikteringsformaal.Opgave);

        Assert.Contains("bydeform", p);
        Assert.Contains("80 tegn", p);
    }

    [Theory]
    [InlineData("diktat.wav", "audio/wav")]
    [InlineData("Diktat.WAV", "audio/wav")]
    [InlineData("klip.m4a", "audio/mp4")]
    [InlineData("klip.mp3", "audio/mpeg")]
    [InlineData("uden-endelse", "audio/wav")]
    public void Medietypen_foelger_endelsen(string fil, string ventet)
    {
        // Sendes der application/octet-stream, afviser endepunktet filen uden
        // at sige hvorfor.
        Assert.Equal(ventet, Dikteringsklient.Medietype(fil));
    }

    [Fact]
    public async Task En_lydfil_der_ikke_findes_siges_lige_ud()
    {
        var klient = new Dikteringsklient("proeve-noegle");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => klient.SkrivUdAsync(Path.Combine(Path.GetTempPath(), "findes-ikke-heypia.wav")));
    }
}
