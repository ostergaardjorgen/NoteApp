using NoteApp.Core;
using NoteApp.Core.Documents;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Forbindelsen mellem en optagelse og de dokumenter, der er lavet ud af den.
/// </summary>
/// <remarks>
/// HVORFOR DEN SKAL SPAENDES FAST
///
/// Dokumenterne laa paa et menupunkt for sig. De ligger nu som en FANE inde
/// paa optagelsen, og hele vaerdien af det er, at fanen viser de RIGTIGE
/// dokumenter. Viser den for mange, hoerer et referat fra et andet moede
/// pludselig til her; viser den for faa, tror man, arbejdet ikke blev gemt.
///
/// Begge fejl ser ud som en tom eller fyldt liste og siger ingenting om, at
/// noget er galt. Derfor proeves selve opslaget - ikke skaermen.
/// </remarks>
public class DokumentsammenhaengTest
{
    private static DocumentInfo Dokument(
        string titel, string moedeId, string mappe, DateTimeOffset? lavet = null) => new()
    {
        Title = titel,
        SourceMeetingId = moedeId,
        SourceRecording = mappe,
        SourceTitle = titel,
        Template = "Mødereferat",
        Model = "Mistral Medium 3.5",
        FileName = DocumentStore.FileNameFor(titel, "Mødereferat"),
        Markdown = "# " + titel,
        Created = lavet ?? DateTimeOffset.Now
    };

    /// <summary>
    /// Skriver KUN metadata. Word-filen er uden betydning for opslaget, og en
    /// .docx pr. prøve ville gøre kørslen langsom uden at prøve mere.
    /// </summary>
    private static void Gem(DocumentInfo d) => DocumentStore.GemMetadata(d);

    // ==================== ID'ET ER FORBINDELSEN ====================

    [Fact]
    public void Kun_dokumenterne_fra_den_valgte_optagelse_kommer_med()
    {
        using var mappe = new Proevemappe();

        var mit = Guid.NewGuid().ToString();
        var andet = Guid.NewGuid().ToString();

        Gem(Dokument("Referat af mødet", mit, @"C:\AppNoter\Optagelser\moede-a"));
        Gem(Dokument("Beslutninger", mit, @"C:\AppNoter\Optagelser\moede-a"));
        Gem(Dokument("Et helt andet møde", andet, @"C:\AppNoter\Optagelser\moede-b"));

        var fundet = DocumentStore.ForMoede(mit, @"C:\AppNoter\Optagelser\moede-a");

        Assert.Equal(2, fundet.Count);
        Assert.All(fundet, d => Assert.Equal(mit, d.SourceMeetingId));
    }

    [Fact]
    public void Nyeste_dokument_staar_foerst()
    {
        using var mappe = new Proevemappe();

        var id = Guid.NewGuid().ToString();
        var sti = @"C:\AppNoter\Optagelser\moede";

        Gem(Dokument("Det gamle", id, sti, DateTimeOffset.Now.AddDays(-3)));
        Gem(Dokument("Det nye", id, sti, DateTimeOffset.Now));

        var fundet = DocumentStore.ForMoede(id, sti);

        Assert.Equal("Det nye", fundet[0].Title);
        Assert.Equal("Det gamle", fundet[1].Title);
    }

    [Fact]
    public void En_omdoebt_eller_flyttet_optagelse_beholder_sine_dokumenter()
    {
        using var mappe = new Proevemappe();

        var id = Guid.NewGuid().ToString();

        // Dokumentet blev lavet, da optagelsen laa ET sted og hed noget andet.
        Gem(Dokument("Referat", id, @"C:\AppNoter\Optagelser\det-gamle-navn"));

        // I dag ligger den et andet sted. ID'ET ER DET SAMME - og det er hele
        // grunden til, at forbindelsen gaar paa id og ikke paa sti.
        var fundet = DocumentStore.ForMoede(id, @"D:\Flyttet\ny-mappe");

        Assert.Single(fundet);
        Assert.Equal("Referat", fundet[0].Title);
    }

    [Fact]
    public void To_optagelser_i_samme_mappe_blandes_ikke_sammen()
    {
        using var mappe = new Proevemappe();

        var a = Guid.NewGuid().ToString();
        var b = Guid.NewGuid().ToString();
        var sti = @"C:\AppNoter\Optagelser\samme-mappe";

        Gem(Dokument("Mit referat", a, sti));
        Gem(Dokument("Ikke mit referat", b, sti));

        var fundet = DocumentStore.ForMoede(a, sti);

        Assert.Single(fundet);
        Assert.Equal("Mit referat", fundet[0].Title);
    }

    // =============== STIEN ER RESERVEN, NAAR ID'ET MANGLER ===============

    [Fact]
    public void Et_dokument_uden_moedeid_findes_paa_stien()
    {
        using var mappe = new Proevemappe();

        // SourceMeetingId skrives af «meta?.Id.ToString() ?? ""». Er
        // meeting.json ulaeselig, naar dokumentet laves, er det TOMT - og saa
        // er stien det eneste spor tilbage. Uden reserven ville dokumentet
        // aldrig kunne findes frem igen.
        var sti = @"C:\AppNoter\Optagelser\gammel-optagelse";

        Gem(Dokument("Referat uden id", "", sti));

        var fundet = DocumentStore.ForMoede(Guid.NewGuid().ToString(), sti);

        Assert.Single(fundet);
        Assert.Equal("Referat uden id", fundet[0].Title);
    }

    [Fact]
    public void Stien_sammenlignes_uden_hensyn_til_store_bogstaver_og_backslash()
    {
        using var mappe = new Proevemappe();

        Gem(Dokument("Referat", "", @"C:\AppNoter\Optagelser\Moede"));

        Assert.Single(DocumentStore.ForMoede("", @"c:\appnoter\optagelser\moede"));
        Assert.Single(DocumentStore.ForMoede("", @"C:\AppNoter\Optagelser\Moede\"));
    }

    [Fact]
    public void Et_dokument_med_id_bliver_ikke_fundet_paa_en_fremmed_sti()
    {
        using var mappe = new Proevemappe();

        var id = Guid.NewGuid().ToString();
        Gem(Dokument("Referat", id, @"C:\AppNoter\Optagelser\moede-a"));

        // Begge har et id, og de er forskellige. Saa hoerer dokumentet til en
        // anden optagelse - ogsaa selv om stien tilfaeldigvis passer i dag.
        var fundet = DocumentStore.ForMoede(
            Guid.NewGuid().ToString(), @"C:\AppNoter\Optagelser\moede-a");

        Assert.Empty(fundet);
    }

    // ======================== INGENTING VALGT ========================

    [Fact]
    public void Uden_optagelse_findes_der_ingen_dokumenter()
    {
        using var mappe = new Proevemappe();

        Gem(Dokument("Referat", Guid.NewGuid().ToString(), @"C:\AppNoter\Optagelser\moede"));

        // Er der ikke valgt en optagelse, maa fanen ikke vise ALLE
        // dokumenter. Saa ville de se ud, som om de hoerte til noget, de ikke
        // hoerer til.
        Assert.Empty(DocumentStore.ForMoede(null, null));
        Assert.Empty(DocumentStore.ForMoede("", ""));
        Assert.Empty(DocumentStore.ForMoede("   ", "  "));
    }

    [Fact]
    public void En_optagelse_uden_dokumenter_giver_en_tom_liste_og_ikke_en_fejl()
    {
        using var mappe = new Proevemappe();

        Assert.Empty(DocumentStore.ForMoede(
            Guid.NewGuid().ToString(), @"C:\AppNoter\Optagelser\helt-ny"));
    }

    // ============ FORBINDELSEN SKAL OVERLEVE EN OMDOEBNING ============

    [Fact]
    public void Titlen_paa_kilden_friskes_op_naar_optagelsen_stadig_findes()
    {
        using var mappe = new Proevemappe();

        // En rigtig optagelse paa disken, saa Opfrisk har noget at slaa op.
        var optagelse = mappe.Optagelse("moedet");

        var meta = new MeetingMetadata
        {
            StartedAt = DateTimeOffset.Now,
            Title = "Det nye navn"
        };
        MeetingStore.Save(optagelse, meta);

        // Dokumentet husker det GAMLE navn.
        var gammelt = Dokument("Referat", meta.Id.ToString(), optagelse);
        gammelt.SourceTitle = "Det gamle navn";
        Gem(gammelt);

        var fundet = DocumentStore.ForMoede(meta.Id.ToString(), optagelse);

        var d = Assert.Single(fundet);

        // Uden opfriskningen ville der staa to navne paa den samme optagelse,
        // og man kan ikke se, om det er det samme moede.
        Assert.Equal("Det nye navn", d.SourceTitle);
    }
}
