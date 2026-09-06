using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// De indbyggede projektskabeloner — dem, «Projekt output» er fyldt med fra
/// første start.
/// </summary>
/// <remarks>
/// ============ ET TOMT BIBLIOTEK ER EN KNAP, DER IKKE VIRKER ============
///
/// «Projekt output» stod tomt, da det blev lavet. Man kunne oprette et
/// projekt, lægge dokumenter i det og trykke Byg — og så var der ingenting at
/// vælge. Prøven her holder fast i, at der ER noget at begynde med.
///
/// OG AT DET ER DEN RIGTIGE SLAGS. En projektskabelon, der beder om
/// <c>{{transskription}}</c>, får en tom streng: der er ingen optagelse i et
/// projekt. Dokumentet ville blive bygget på ingenting, og først den, der
/// læste det, ville opdage det.
/// </remarks>
public class Projektskabelontest
{
    private static IReadOnlyList<PromptTemplate> Indbyggede()
    {
        DraftStore.SeedTemplates();
        return PromptTemplate.LoadAll(Skabelonslags.Projektoutput);
    }

    [Fact]
    public void Der_er_projektskabeloner_fra_foerste_start()
    {
        using var p = new Proevemappe();

        var navne = Indbyggede().Select(t => t.Name).ToList();

        Assert.Contains("Projektbeskrivelse", navne);
        Assert.Contains("Tilbud", navne);
        Assert.Contains("Opsummering", navne);
        Assert.Contains("Studieplan", navne);
    }

    [Fact]
    public void De_lander_i_projektmappen_og_ikke_blandt_moedetyperne()
    {
        using var p = new Proevemappe();

        var projekt = Indbyggede().Select(t => t.Name).ToList();
        var moedetyper = PromptTemplate.LoadAll(Skabelonslags.Moedetype).Select(t => t.Name).ToList();

        // Baade «Opsummering» og «Dokumentation» kunne hedde det samme i de to
        // biblioteker. De maa ikke lande i den samme liste.
        Assert.DoesNotContain("Tilbud", moedetyper);
        Assert.DoesNotContain("Studieplan", moedetyper);

        // Og moedetyperne skal stadig komme med.
        Assert.Contains("Mødereferat", moedetyper);
        Assert.DoesNotContain("Mødereferat", projekt);
    }

    [Fact]
    public void De_beder_om_projektets_materiale_og_ikke_om_en_udskrift()
    {
        using var p = new Proevemappe();

        foreach (var t in Indbyggede())
        {
            Assert.Contains("{{kilder}}", t.UserPrompt);
            Assert.Contains("{{projekt}}", t.UserPrompt);

            // Feltet ville staa tomt. Se Felter(Skabelonslags).
            Assert.DoesNotContain("{{transskription}}", t.UserPrompt);

            // Et projekt har ikke deltagere, det har filer.
            Assert.False(t.TagDeltagerregler, t.Name);
        }
    }

    [Fact]
    public void Hver_af_dem_har_afsnit_at_soege_paa()
    {
        using var p = new Proevemappe();

        // AFSNITTENE ER OGSAA FREMSOEGNINGSSPOERGSMAALENE — se Projektkontekst.
        // En skabelon uden dem faar kun begyndelsen af hver fil at se.
        foreach (var t in Indbyggede())
            Assert.True(t.Afsnit().Count >= 5, $"{t.Name} har {t.Afsnit().Count} afsnit");
    }

    [Fact]
    public void Sproget_vaelges_naar_dokumentet_bygges()
    {
        using var p = new Proevemappe();

        foreach (var t in Indbyggede())
        {
            // Skabelonen maa ikke selv skrive et sprog. Feltet saettes ind,
            // naar dokumentet laves, og valget skal kunne aendre noget.
            Assert.Contains("{{sprogregler}}", t.SystemPrompt);
            Assert.Contains("in english", t.RenderSystem("en").ToLowerInvariant());
        }
    }

    [Fact]
    public void Et_projektoutput_faar_ikke_moedets_felter_at_vaelge_imellem()
    {
        var felter = PromptTemplate.Felter(Skabelonslags.Projektoutput).ToList();

        Assert.Contains("kilder", felter);
        Assert.Contains("projekt", felter);

        // «kilde» er LINKET TIL WEBINARET, ikke projektets kilder. Det ligner
        // det rigtige felt paa navnet og ville altid staa tomt.
        Assert.DoesNotContain("kilde", felter);
        Assert.DoesNotContain("transskription", felter);
        Assert.DoesNotContain("noter", felter);

        // Og den anden vej: en moedetype har ingen projektfelter.
        var moede = PromptTemplate.Felter(Skabelonslags.Moedetype).ToList();

        Assert.Contains("kilde", moede);
        Assert.Contains("transskription", moede);
        Assert.DoesNotContain("kilder", moede);
        Assert.DoesNotContain("projekt", moede);
    }

    [Fact]
    public void En_rettet_skabelon_overskrives_ikke_af_naeste_start()
    {
        using var p = new Proevemappe();

        var foerste = Indbyggede().Single(t => t.Name == "Tilbud");

        foerste.SystemPrompt = "## Mit eget afsnit\n\nSkriv noget andet.";
        foerste.Save();

        DraftStore.SeedTemplates();

        var igen = PromptTemplate.LoadAll(Skabelonslags.Projektoutput).Single(t => t.Name == "Tilbud");
        Assert.Contains("Mit eget afsnit", igen.SystemPrompt);
    }

    [Fact]
    public void En_ny_skabelon_kommer_med_selv_om_mappen_findes()
    {
        using var p = new Proevemappe();

        // Foerste start.
        DraftStore.SeedTemplates();

        // Brugeren sletter een af dem. Naeste start skal laegge den tilbage —
        // det er den samme mekanik, der giver en skabelon, som er kommet til i
        // en ny udgave, til en, der har haft appen laenge.
        var sti = PromptTemplate.LoadAll(Skabelonslags.Projektoutput)
            .Single(t => t.Name == "Studieplan").Path!;

        File.Delete(sti);
        Assert.Equal(1, DraftStore.SeedTemplates());

        Assert.Contains(PromptTemplate.LoadAll(Skabelonslags.Projektoutput), t => t.Name == "Studieplan");
    }
}
