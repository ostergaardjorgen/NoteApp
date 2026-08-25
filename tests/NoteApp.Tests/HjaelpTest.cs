using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Hjælpen i appen.
///
/// Der prøves på det, der kan blive forkert uden at nogen opdager det: at
/// afsnittene findes på begge sprog, at søgningen svarer på noget, og at et
/// afsnit, der ikke er oversat, viser dansk frem for ingenting.
/// </summary>
public class HjaelpTest
{
    [Fact]
    public void Hjaelpen_laegges_i_datamappen_ved_foerste_brug()
    {
        using var p = new Proevemappe();

        var afsnit = Hjaelp.Alle();

        Assert.NotEmpty(afsnit);
        Assert.True(Directory.Exists(Path.Combine(Hjaelp.Mappe, "da")));
        Assert.True(Directory.Exists(Path.Combine(Hjaelp.Mappe, "en")));
    }

    [Fact]
    public void Hvert_afsnit_har_en_titel_og_en_tekst()
    {
        using var p = new Proevemappe();

        foreach (var a in Hjaelp.Alle())
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Titel), $"{a.Id} mangler titel");
            Assert.False(string.IsNullOrWhiteSpace(a.Tekst), $"{a.Id} mangler tekst");

            // Titlen maa ikke bare vaere filnavnet - saa er «# »-linjen glemt.
            Assert.NotEqual(a.Id, a.Titel);
        }
    }

    [Fact]
    public void Afsnittene_staar_i_den_raekkefoelge_filnavnet_siger()
    {
        using var p = new Proevemappe();

        var orden = Hjaelp.Alle().Select(a => a.Orden).ToList();

        Assert.Equal(orden.OrderBy(n => n), orden);

        // Foerste afsnit skal vaere det, man skal laese foerst.
        Assert.StartsWith("10-", Hjaelp.Alle()[0].Id, StringComparison.Ordinal);
    }

    // ===================== BEGGE SPROG =====================

    [Fact]
    public void Engelsk_har_de_samme_afsnit_som_dansk()
    {
        using var p = new Proevemappe();

        var danske = Hjaelp.Alle("da").Select(a => a.Id).OrderBy(s => s, StringComparer.Ordinal);
        var engelske = Hjaelp.Alle("en").Select(a => a.Id).OrderBy(s => s, StringComparer.Ordinal);

        Assert.Equal(danske, engelske);
    }

    [Fact]
    public void Hjaelpen_er_faktisk_oversat_og_ikke_bare_kopieret()
    {
        using var p = new Proevemappe();

        var danske = Hjaelp.Alle("da").ToDictionary(a => a.Id, a => a.Titel, StringComparer.Ordinal);

        // Den her fanger den fejl, en kopi-og-omdoeb giver: en engelsk fil,
        // der stadig indeholder dansk. Titlerne er korte og forskellige nok
        // til, at de kan bruges som proeve.
        var ens = Hjaelp.Alle("en")
            .Where(a => danske.TryGetValue(a.Id, out var dansk) && dansk == a.Titel)
            .Select(a => a.Id)
            .ToList();

        Assert.True(ens.Count == 0,
            "Disse afsnit har samme titel paa begge sprog: " + string.Join(", ", ens));
    }

    [Fact]
    public void Et_sprog_der_kun_har_nogle_faa_afsnit_fylder_op_med_dansk()
    {
        using var p = new Proevemappe();

        Hjaelp.Alle();   // saa mapperne findes

        // EN SLETTET FIL KAN IKKE BRUGES TIL DEN HER PROEVE.
        //
        // Foerste udgave slettede den engelske fil og ventede dansk. Den
        // fejlede - fordi Udpak laegger de filer, der FOELGER MED appen,
        // tilbage. Det er den rigtige opfoersel, og proeven var forkert.
        //
        // Et nyt sprog er derimod praecis det tilfaelde, reglen findes for:
        // en oversaetter, der er naaet halvvejs.
        var tysk = Path.Combine(Hjaelp.Mappe, "de");
        Directory.CreateDirectory(tysk);

        File.WriteAllText(Path.Combine(tysk, "10-kom-i-gang.md"),
            "# Erste Schritte" + Environment.NewLine + Environment.NewLine
            + "*Vom Meeting zum Protokoll*" + Environment.NewLine + Environment.NewLine
            + "Nur dieser Abschnitt ist übersetzt.");

        Hjaelp.Genindlaes();

        var alle = Hjaelp.Alle("de");

        // Det oversatte afsnit staar paa tysk.
        Assert.Equal("Erste Schritte", alle.Single(a => a.Id == "10-kom-i-gang").Titel);

        // Resten staar paa dansk - HALV HJAELP SLAAR INGEN HJAELP.
        Assert.Equal("Optagelse", alle.Single(a => a.Id == "20-optagelse").Titel);

        // Og der mangler ingen afsnit.
        Assert.Equal(Hjaelp.Alle("da").Count, alle.Count);
    }

    // ===================== SØGNINGEN =====================

    [Fact]
    public void Der_soeges_i_baade_titel_og_tekst()
    {
        using var p = new Proevemappe();

        Assert.NotEmpty(Hjaelp.Soeg("optagelse"));
        Assert.NotEmpty(Hjaelp.Soeg("mødetype"));
        Assert.NotEmpty(Hjaelp.Soeg("genvejstast"));
    }

    [Fact]
    public void Titlen_vejer_tungest()
    {
        using var p = new Proevemappe();

        // «Kom i gang» hedder afsnittet. Det skal ligge foerst, ogsaa selv om
        // ordene staar i andre afsnit ogsaa.
        var traef = Hjaelp.Soeg("kom i gang");

        Assert.NotEmpty(traef);
        Assert.Equal("10-kom-i-gang", traef[0].Afsnit.Id);
    }

    [Fact]
    public void Alle_ord_skal_vaere_der()
    {
        using var p = new Proevemappe();

        // «slet» findes; «bananmarmelade» goer ikke. Saa skal der ikke komme
        // et eneste traef - ellers ville en soegning paa to ord svare med alt,
        // hvor bare det ene stod.
        Assert.Empty(Hjaelp.Soeg("slet bananmarmelade"));
    }

    [Fact]
    public void Et_traef_har_et_uddrag_man_kan_laese()
    {
        using var p = new Proevemappe();

        var traef = Hjaelp.Soeg("grafikkort");

        Assert.NotEmpty(traef);

        foreach (var t in traef)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Uddrag));

            // Markdown-tegnene skal vaere renset vaek - uddraget staar som en
            // saetning, ikke som kildetekst.
            Assert.DoesNotContain("**", t.Uddrag, StringComparison.Ordinal);
            Assert.DoesNotContain("`", t.Uddrag, StringComparison.Ordinal);
            Assert.False(t.Uddrag.StartsWith('#'));
        }
    }

    [Fact]
    public void En_tom_soegning_giver_ingen_traef()
    {
        using var p = new Proevemappe();

        Assert.Empty(Hjaelp.Soeg(""));
        Assert.Empty(Hjaelp.Soeg("   "));

        // Eet bogstav er ikke en soegning - det ville ramme alt.
        Assert.Empty(Hjaelp.Soeg("a"));
    }

    [Fact]
    public void Der_soeges_paa_det_valgte_sprog()
    {
        using var p = new Proevemappe();

        Assert.NotEmpty(Hjaelp.Soeg("optagelse"));

        Sprog.Skift("en");

        Assert.NotEmpty(Hjaelp.Soeg("recording"));
    }
}
