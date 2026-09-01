using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Ledetraaden, der skal holde udskriften paa dansk.
/// </summary>
/// <remarks>
/// MAALT 31-08-2026: en diktering paa dansk kom tilbage paa TYSK - «Hi Pia,
/// kannst du mir helfen ...». Prompten var bygget som «ledetraad, saa
/// ordliste», og ordlisten er fyrre fagord, hvoraf de fleste er engelske.
/// Fyrre stemmer for engelsk mod én for dansk.
/// </remarks>
public class SprogledetraadTest
{
    private static readonly string[] Fagord =
        { "SCIM", "SSO", "MFA", "Microsoft Graph", "IAM", "Omada", "NetIQ" };

    [Fact]
    public void Sproget_staar_i_begge_ender_naar_der_er_fagord()
    {
        var p = Voxtral.Prompt("da", sprogetErValgt: false, Fagord);
        var ledetraad = Voxtral.Ledetraad("da")!;

        Assert.StartsWith(ledetraad, p);
        Assert.EndsWith(ledetraad, p);
    }

    [Fact]
    public void Fagordene_staar_inde_i_en_dansk_saetning()
    {
        var p = Voxtral.Prompt("da", sprogetErValgt: false, Fagord);

        Assert.Contains("Disse ord kan forekomme: SCIM, SSO", p);
        Assert.Contains("NetIQ.", p);
    }

    /// <summary>
    /// DET SIDSTE, MODELLEN LAESER FOER LYDEN, SKAL VAERE DANSK.
    /// </summary>
    /// <remarks>
    /// Det var praecis dét, der gik galt: prompten SLUTTEDE paa «SSO, MFA,
    /// PIM», og saa gaettede modellen paa et andet sprog.
    /// </remarks>
    [Fact]
    public void Prompten_slutter_ikke_paa_et_engelsk_fagord()
    {
        var p = Voxtral.Prompt("da", sprogetErValgt: false, Fagord);

        Assert.False(p.TrimEnd('.', ' ').EndsWith("NetIQ"),
            "Prompten slutter paa et fagord. Saa er det sidste, modellen "
            + "laeser foer lyden, engelsk.");
    }

    [Fact]
    public void Uden_fagord_er_der_bare_ledetraaden()
    {
        Assert.Equal(Voxtral.Ledetraad("da"),
            Voxtral.Prompt("da", sprogetErValgt: false, null));
    }

    [Fact]
    public void Kan_sproget_vaelges_er_der_ingen_ledetraad()
    {
        // Engelsk staar paa Voxtrals egen liste og sendes som sprogvalg.
        // Saa skal ledetraaden ikke ogsaa fylde i prompten.
        var p = Voxtral.Prompt("en", sprogetErValgt: true, Fagord);

        Assert.DoesNotContain("Der tales dansk", p);
        Assert.Contains("SCIM", p);
    }

    [Fact]
    public void Uden_noget_som_helst_er_prompten_tom()
    {
        Assert.Equal("", Voxtral.Prompt("en", sprogetErValgt: true, null));
    }

    // ============ ORDBOGEN HOERER IKKE TIL I PUDSNINGEN ============

    /// <summary>
    /// Maalt mod den rigtige model 31-08-2026, samme input hver gang:
    ///
    ///   uden ordbog          «Omada, IBM, NetIQ, Starz Tech.»
    ///   hele ordbogen (42)   «Omada, NetIQ»
    ///   tre relevante ord    «Omada, StorageTek, NetIQ, Starz Tech.»
    ///
    /// Med hele ordbogen SLETTEDE den de ord, der ikke stod paa listen - ogsaa
    /// med «SLET ALDRIG ET ORD» i instruktionen. Med tre ord INDSATTE den et
    /// ord, der ikke blev sagt.
    ///
    /// En liste af ord i en instruktion er ikke en ordbog for en sprogmodel.
    /// Det er en ramme, den forsoeger at faa teksten til at passe ind i.
    /// </summary>
    [Fact]
    public void Pudseinstruktionen_naevner_ingen_ordbog()
    {
        foreach (var f in Enum.GetValues<Dikteringsformaal>())
        {
            var p = Voxtral.Pudseprompt(f);

            Assert.DoesNotContain("fagord kan forekomme", p);
            Assert.DoesNotContain("fejlstavning", p);
        }
    }
}
