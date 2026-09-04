using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Sprogstyringen.
///
/// Det, der prøves, er ikke oversættelserne — dem kan en prøve ikke bedømme.
/// Det er MEKANIKKEN: at et nyt sprog er én fil, at et halvt oversat sprog
/// falder tilbage på dansk frem for at vise nøgler, og at valget huskes.
/// </summary>
public class SprogTest
{
    [Fact]
    public void Sprogfilerne_laegges_i_mappen_ved_foerste_brug()
    {
        using var p = new Proevemappe();

        var sprog = Sprog.Tilgaengelige();

        Assert.Contains(sprog, s => s.Kode == "da");
        Assert.Contains(sprog, s => s.Kode == "en");

        Assert.True(File.Exists(Path.Combine(Sprog.Mappe, "da.json")));
        Assert.True(File.Exists(Path.Combine(Sprog.Mappe, "en.json")));
    }

    [Fact]
    public void Dansk_er_standard_og_staar_foerst()
    {
        using var p = new Proevemappe();

        Assert.Equal("da", Sprog.Kode);
        Assert.Equal("da", Sprog.Tilgaengelige()[0].Kode);
    }

    [Fact]
    public void Hvert_sprog_beskriver_sig_selv()
    {
        using var p = new Proevemappe();

        var da = Sprog.Tilgaengelige().Single(s => s.Kode == "da");
        var en = Sprog.Tilgaengelige().Single(s => s.Kode == "en");

        Assert.Equal("Dansk", da.Navn);
        Assert.Equal("DK", da.Flag);

        Assert.Equal("English", en.Navn);
        Assert.Equal("GB", en.Flag);
    }

    [Fact]
    public void Der_skiftes_sprog_og_teksten_foelger_med()
    {
        using var p = new Proevemappe();

        Assert.Equal("Optagelser", Sprog.T("nav.optagelser"));

        Sprog.Skift("en");

        Assert.Equal("en", Sprog.Kode);
        Assert.Equal("Recordings", Sprog.T("nav.optagelser"));

        Sprog.Skift("da");

        Assert.Equal("Optagelser", Sprog.T("nav.optagelser"));
    }

    [Fact]
    public void Valget_huskes()
    {
        using var p = new Proevemappe();

        Sprog.Skift("en");

        // Indstillingen skal staa paa disken, ikke kun i hukommelsen - ellers
        // er appen dansk igen ved naeste start.
        AppSettings.Reload();
        Assert.Equal("en", AppSettings.Current.Sprog);
    }

    [Fact]
    public void Et_ukendt_sprog_aendrer_ingenting()
    {
        using var p = new Proevemappe();

        Sprog.Skift("klingon");

        Assert.Equal("da", Sprog.Kode);
    }

    // ===================== ET NYT SPROG ER ÉN FIL =====================

    [Fact]
    public void Et_nyt_sprog_kraever_kun_en_fil_i_mappen()
    {
        using var p = new Proevemappe();

        Sprog.Tilgaengelige();   // saa mappen findes

        File.WriteAllText(Path.Combine(Sprog.Mappe, "de.json"), """
            {
              "_sprog": { "kode": "de", "navn": "Deutsch", "flag": "DE" },
              "nav": { "optagelser": "Aufnahmen" }
            }
            """);

        var tysk = Sprog.Tilgaengelige().SingleOrDefault(s => s.Kode == "de");

        Assert.NotNull(tysk);
        Assert.Equal("Deutsch", tysk!.Navn);
        Assert.Equal("DE", tysk.Flag);

        Sprog.Skift("de");
        Assert.Equal("Aufnahmen", Sprog.T("nav.optagelser"));
    }

    [Fact]
    public void Et_halvt_oversat_sprog_falder_tilbage_paa_dansk()
    {
        using var p = new Proevemappe();

        Sprog.Tilgaengelige();

        // Kun EEN noegle oversat. Resten skal vise dansk - ikke noegler, som
        // ingen kan bruge til noget.
        File.WriteAllText(Path.Combine(Sprog.Mappe, "de.json"), """
            {
              "_sprog": { "kode": "de", "navn": "Deutsch", "flag": "DE" },
              "nav": { "optagelser": "Aufnahmen" }
            }
            """);

        Sprog.Skift("de");

        Assert.Equal("Aufnahmen", Sprog.T("nav.optagelser"));

        // HER STOD «nav.dokumenter». Den noegle findes ikke mere: menupunktet
        // «Dokumenter» blev fjernet 04-09-2026, og dokumenterne staar nu som
        // en fane paa optagelsen. Proeven handler om TILBAGEFALDET og ikke om
        // dokumenter - den skal bare bruge en noegle, der ikke er oversat.
        Assert.Equal("Historik", Sprog.T("nav.historik"));
        Assert.Equal("Compliance", Sprog.T("nav.compliance"));
    }

    [Fact]
    public void En_noegle_der_slet_ikke_findes_kommer_tilbage_som_sig_selv()
    {
        using var p = new Proevemappe();

        // MED VILJE GRIMT. En manglende noegle skal opdages, mens der bygges -
        // ikke af en kunde.
        Assert.Equal("noget.der.ikke.findes", Sprog.T("noget.der.ikke.findes"));
    }

    // ===================== FORMEN PÅ FILEN =====================

    [Fact]
    public void Baade_indlejret_og_fladt_JSON_virker()
    {
        using var p = new Proevemappe();

        Sprog.Tilgaengelige();

        File.WriteAllText(Path.Combine(Sprog.Mappe, "de.json"), """
            {
              "_sprog": { "kode": "de", "navn": "Deutsch", "flag": "DE" },
              "nav.optagelser": "Aufnahmen"
            }
            """);

        Sprog.Skift("de");

        Assert.Equal("Aufnahmen", Sprog.T("nav.optagelser"));
    }

    [Fact]
    public void En_oedelagt_sprogfil_vaelter_ingenting()
    {
        using var p = new Proevemappe();

        Sprog.Tilgaengelige();

        File.WriteAllText(Path.Combine(Sprog.Mappe, "xx.json"), "{ det her er ikke JSON");

        // Den oedelagte springes over, og de oevrige staar tilbage.
        var sprog = Sprog.Tilgaengelige();

        Assert.Contains(sprog, s => s.Kode == "da");
        Assert.DoesNotContain(sprog, s => s.Kode == "xx");
    }

    // ===================== INDSATTE VÆRDIER =====================

    [Fact]
    public void Vaerdier_saettes_ind()
    {
        using var p = new Proevemappe();

        Assert.Equal("Kalender · 5", Sprog.T("kalender.titelmed", 5));
        Assert.Equal("3 skal gøres nu", Sprog.T("opgaver.haster", 3));
    }

    [Fact]
    public void En_forkert_formatstreng_vaelter_ikke_skaermen()
    {
        using var p = new Proevemappe();

        Sprog.Tilgaengelige();

        // Oversaetteren har skrevet {1} i en tekst, der kun faar een vaerdi.
        File.WriteAllText(Path.Combine(Sprog.Mappe, "de.json"), """
            {
              "_sprog": { "kode": "de", "navn": "Deutsch", "flag": "DE" },
              "kalender": { "titelmed": "Kalender {0} von {1}" }
            }
            """);

        Sprog.Skift("de");

        // Uformateret er grimt. Et nedbrud er vaerre.
        Assert.Equal("Kalender {0} von {1}", Sprog.T("kalender.titelmed", 5));
    }

    // ===================== DE TO FILER SKAL PASSE SAMMEN =====================

    [Fact]
    public void Engelsk_har_alle_de_noegler_dansk_har()
    {
        using var p = new Proevemappe();

        Sprog.Tilgaengelige();

        // DER SAMMENLIGNES NOEGLER, IKKE TEKSTER.
        //
        // Foerste udgave saa efter, om den engelske tekst var FORSKELLIG fra
        // den danske - og faldt paa «Trust Center», «EU», «Agenda» og fem
        // andre ord, der er ens paa begge sprog. Den slags kraevede en liste
        // over undtagelser, og en liste over undtagelser bliver aldrig
        // faerdig.
        //
        // Det rigtige spoergsmaal er, om noeglen STAAR i en.json. Staar den
        // der med den samme tekst, er det et valg. Mangler den, er det en
        // forglemmelse - og saa viser appen dansk midt i en engelsk skaerm.
        var danske = Noegler("da.json")
            .Where(n => !n.StartsWith("_sprog", StringComparison.Ordinal))
            .ToList();

        var engelske = Noegler("en.json").ToHashSet(StringComparer.Ordinal);

        var manglende = danske.Where(n => !engelske.Contains(n)).ToList();

        Assert.True(manglende.Count == 0,
            $"en.json mangler {manglende.Count} nøgler:\n  "
            + string.Join("\n  ", manglende.Take(40))
            + (manglende.Count > 40 ? $"\n  … og {manglende.Count - 40} mere" : ""));
    }

    [Fact]
    public void Dansk_har_ikke_noegler_der_er_glemt_paa_engelsk_omvendt()
    {
        using var p = new Proevemappe();

        Sprog.Tilgaengelige();

        // Den anden vej: en noegle i en.json, der IKKE findes i da.json, er
        // enten en stavefejl eller en rest fra noget, der er fjernet. Den
        // vises aldrig, og den staar og roder.
        var danske = Noegler("da.json").ToHashSet(StringComparer.Ordinal);

        var overskydende = Noegler("en.json")
            .Where(n => !n.StartsWith("_sprog", StringComparison.Ordinal))
            .Where(n => !danske.Contains(n))
            .ToList();

        Assert.True(overskydende.Count == 0,
            "en.json har nøgler, dansk ikke har: " + string.Join(", ", overskydende.Take(20)));
    }

    private static List<string> Noegler(string filnavn)
    {
        var ud = new List<string>();
        var doku = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Sprog.Mappe, filnavn)));

        void Gaa(System.Text.Json.JsonElement e, string praefiks)
        {
            foreach (var felt in e.EnumerateObject())
            {
                var noegle = praefiks.Length == 0 ? felt.Name : praefiks + "." + felt.Name;

                if (felt.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                    Gaa(felt.Value, noegle);
                else if (felt.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    ud.Add(noegle);
            }
        }

        Gaa(doku.RootElement, "");
        return ud;
    }
}
