using System.Text;
using System.Text.Json;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Compliance-skaermen maa ikke paastaa at have verificeret noget, den ikke
/// kan verificere.
/// </summary>
/// <remarks>
/// Tre ting afgoer, om bearbejdningen hos leverandoeren holder. Appen kan
/// efterproeve DEN ENE: adressen, den sender til. Zero Data Retention og
/// fravalg af modeltraening er kontoindstillinger hos Mistral, og der findes
/// intet endepunkt at spoerge - appen kan hverken saette dem eller laese dem.
///
/// Den forskel er hele grunden til, at fanen findes, og det er den, proeverne
/// her holder fast i. Bliver de tre linjer en dag lige groenne, falder den
/// foerste proeve nedenfor - og saa skal nogen tage stilling frem for at
/// opdage det hos en revisor.
/// </remarks>
public class KontoattestTest
{
    // =================== GRUNDLAGET FOR DE TRE LINJER ===================

    [Fact]
    public void Kun_EU_linjen_er_verificeret()
    {
        var verificerede = Kontoattester.Kontrolpunkter
            .Where(k => k.Grundlag == Grundlag.Verificeret)
            .Select(k => k.Id)
            .ToArray();

        Assert.Equal(new[] { "eu" }, verificerede);
    }

    [Fact]
    public void ZDR_og_traening_kraever_manuel_aktivering()
    {
        foreach (var id in new[] { "zdr", "traening" })
        {
            var punkt = Kontoattester.Kontrolpunkter.Single(k => k.Id == id);

            Assert.Equal(Grundlag.KraeverManuel, punkt.Grundlag);
        }
    }

    [Fact]
    public void Der_er_tre_kontrolpunkter_og_ikke_flere()
    {
        Assert.Equal(3, Kontoattester.Kontrolpunkter.Count);
    }

    // ===================== ATTESTEN GEMMES OG LAESES =====================

    [Fact]
    public void Intet_registreret_naar_der_ikke_er_gemt_noget()
    {
        using var mappe = new Proevemappe();

        var a = Kontoattester.Hent();

        Assert.False(a.Zdr.ErRegistreret);
        Assert.False(a.Traening.ErRegistreret);
    }

    [Fact]
    public void Det_der_registreres_kan_laeses_igen()
    {
        using var mappe = new Proevemappe();

        Kontoattester.Gem(new Kontoattest
        {
            Zdr = new Attest
            {
                Dato = "2026-09-03",
                Ansvarlig = "Jørgen Østergaard",
                Reference = "SAG-2026-0142, bilag 4"
            },
            Traening = new Attest
            {
                Dato = "2026-09-01",
                Ansvarlig = "Databeskyttelsesrådgiveren",
                Reference = "bilag-4.pdf"
            }
        });

        var a = Kontoattester.Hent();

        Assert.True(a.Zdr.ErRegistreret);
        Assert.Equal("2026-09-03", a.Zdr.Dato);
        Assert.Equal("Jørgen Østergaard", a.Zdr.Ansvarlig);
        Assert.Equal("SAG-2026-0142, bilag 4", a.Zdr.Reference);

        Assert.True(a.Traening.ErRegistreret);
        Assert.Equal("bilag-4.pdf", a.Traening.Reference);
    }

    [Fact]
    public void En_dato_uden_en_ansvarlig_er_ikke_en_registrering()
    {
        // En attest uden et navn kan ingen staa inde for. Halvt udfyldt maa
        // derfor ikke taelle som «registreret» - det er praecis den slags, en
        // revisor faar at se.
        var a = new Attest { Dato = "2026-09-03" };

        Assert.False(a.ErRegistreret);
    }

    [Fact]
    public void En_oedelagt_fil_betyder_ikke_registreret()
    {
        using var mappe = new Proevemappe();

        Directory.CreateDirectory(UserDataPaths.Root);
        File.WriteAllText(Kontoattester.Fil, "{ det her er ikke json", Encoding.UTF8);

        var a = Kontoattester.Hent();

        Assert.False(a.Zdr.ErRegistreret);
        Assert.False(a.Traening.ErRegistreret);
    }

    // ============ DER GEMMES INTET FRA KONTOEN HOS LEVERANDOEREN ============

    [Theory]
    [InlineData("2026-09-03")]
    [InlineData("Jørgen Østergaard")]
    [InlineData("SAG-2026-0142, bilag 4")]
    [InlineData("bilag-4.pdf")]
    [InlineData("C:\\Sager\\2026\\databehandling\\zdr-kvittering.pdf")]
    [InlineData("")]
    public void Almindelige_henvisninger_maa_gemmes(string vaerdi)
    {
        Assert.Null(Kontoattester.Fejl(vaerdi));
    }

    [Theory]
    // En Mistral-noegle: 32 tegn i én ubrudt blok.
    [InlineData("aB3xY9kLmN2pQ7rS4tU6vW8zA1bC5dE0")]
    // Et workspace-id uden bindestreger.
    [InlineData("9f8e7d6c5b4a39281706f5e4d3c2b1a0")]
    public void Noget_der_ligner_en_noegle_afvises(string vaerdi)
    {
        Assert.NotNull(Kontoattester.Fejl(vaerdi));
    }

    [Fact]
    public void En_noegle_i_et_felt_bliver_ikke_gemt()
    {
        using var mappe = new Proevemappe();

        var med = new Kontoattest
        {
            Zdr = new Attest
            {
                Dato = "2026-09-03",
                Ansvarlig = "Jørgen",
                Reference = "aB3xY9kLmN2pQ7rS4tU6vW8zA1bC5dE0"
            }
        };

        Assert.Throws<InvalidOperationException>(() => Kontoattester.Gem(med));

        // OG DER MAA IKKE LIGGE EN HALV FIL BAGEFTER. Kastede den midt i
        // skrivningen, ville noeglen staa paa disken alligevel.
        Assert.False(File.Exists(Kontoattester.Fil));
    }

    [Fact]
    public void Et_alt_for_langt_felt_afvises()
    {
        Assert.NotNull(Kontoattester.Fejl(new string('a', 201).Insert(50, " ")));
    }

    // ================= ANMODNINGS-ID'ET FRA LEVERANDOEREN =================

    private static IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headere(
        params (string Navn, string Vaerdi)[] par) =>
        par.Select(p => new KeyValuePair<string, IEnumerable<string>>(p.Navn, new[] { p.Vaerdi }));

    [Fact]
    public void Anmodningsid_laeses_af_headeren_Mistral_sender()
    {
        var id = Kvitteringer.LaesAnmodningsid(Headere(
            ("Content-Type", "application/json"),
            ("x-kong-request-id", "b1f2c3d4e5")));

        Assert.Equal("b1f2c3d4e5", id);
    }

    [Fact]
    public void Headernavnet_laeses_uden_hensyn_til_store_bogstaver()
    {
        Assert.Equal("abc123", Kvitteringer.LaesAnmodningsid(Headere(("X-Request-Id", "abc123"))));
    }

    [Fact]
    public void Kom_der_intet_id_staar_feltet_tomt()
    {
        // Det er DET RIGTIGE SVAR, ikke en mangel. Et id, appen selv fandt
        // paa, kunne slaas op nul steder og ville ligne noget, det ikke er.
        Assert.Equal("", Kvitteringer.LaesAnmodningsid(Headere(("Date", "Wed, 03 Sep 2026 09:00:00 GMT"))));
        Assert.Equal("", Kvitteringer.LaesAnmodningsid(Headere(("x-request-id", "   "))));
        Assert.Equal("", Kvitteringer.LaesAnmodningsid(Headere()));
    }

    [Fact]
    public void Et_urimeligt_langt_id_skaeres_ned()
    {
        var id = Kvitteringer.LaesAnmodningsid(Headere(("x-request-id", new string('x', 500))));

        Assert.Equal(120, id.Length);
    }

    [Fact]
    public void En_kvittering_uden_id_kan_stadig_laeses()
    {
        using var mappe = new Proevemappe();

        // Sådan så en kvittering ud FØR 03-09-2026. Den skal stadig kunne
        // læses — ellers ville feltet gøre historikken ulæselig bagud.
        Directory.CreateDirectory(Kvitteringer.Directory);
        File.WriteAllText(
            Path.Combine(Kvitteringer.Directory, "2026-08.jsonl"),
            """{"Id":"abc123def456","Tidspunkt":"2026-08-14T10:00:00+02:00","Endepunkt":"https://api.eu.mistral.ai/v1/chat/completions","Model":"Mistral Medium 3.5","Skabelon":"referat","Tegn":4200,"Sum":"deadbeef","TokensInd":1200,"TokensUd":600,"PrisEur":0.0054,"Sekunder":8.2,"Lykkedes":true,"Fejl":""}"""
                + "\n",
            new UTF8Encoding(false));

        var k = Assert.Single(Kvitteringer.Laes());

        Assert.Equal("", k.Anmodningsid);
        Assert.Equal(4200, k.Tegn);
    }

    [Fact]
    public void Kvitteringen_baerer_endepunkt_model_tidspunkt_og_id()
    {
        using var mappe = new Proevemappe();

        var skrevet = new Kvittering
        {
            Endepunkt = "https://api.eu.mistral.ai/v1/chat/completions",
            Model = "Mistral Medium 3.5 (mistral-medium-latest)",
            Skabelon = "referat",
            Tegn = 4200,
            Sum = Kvitteringer.Kontrolsum("noget tekst"),
            Anmodningsid = "b1f2c3d4e5"
        };

        Kvitteringer.Skriv(skrevet);

        var k = Assert.Single(Kvitteringer.Laes());

        Assert.Equal("https://api.eu.mistral.ai/v1/chat/completions", k.Endepunkt);
        Assert.Equal("Mistral Medium 3.5 (mistral-medium-latest)", k.Model);
        Assert.Equal("b1f2c3d4e5", k.Anmodningsid);
        Assert.NotEqual(default, k.Tidspunkt);

        // ANMODNINGSTEKSTEN GEMMES ALDRIG. Kun summen af den.
        var raa = File.ReadAllText(
            Directory.GetFiles(Kvitteringer.Directory, "*.jsonl").Single());

        Assert.DoesNotContain("noget tekst", raa);
        Assert.Contains(Kvitteringer.Kontrolsum("noget tekst"), raa);
    }

    [Fact]
    public void Kvitteringen_kan_ikke_komme_til_at_baere_selve_teksten()
    {
        // Feltet findes ikke, og det er med vilje. Falder den her, har nogen
        // foejet et tekstfelt til kvitteringen - og saa ligger udskriften i
        // to mapper i stedet for én.
        var felter = typeof(Kvittering).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain("Krop", felter);
        Assert.DoesNotContain("Tekst", felter);
        Assert.DoesNotContain("Noegle", felter);
        Assert.Contains("Sum", felter);
        Assert.Contains("Anmodningsid", felter);
    }

    [Fact]
    public void Attesten_gemmes_som_laesbar_JSON_uden_konto_oplysninger()
    {
        using var mappe = new Proevemappe();

        Kontoattester.Gem(new Kontoattest
        {
            Zdr = new Attest { Dato = "2026-09-03", Ansvarlig = "Jørgen", Reference = "SAG-2026-0142" }
        });

        var raa = File.ReadAllText(Kontoattester.Fil, Encoding.UTF8);

        using var doc = JsonDocument.Parse(raa);

        Assert.Equal("2026-09-03", doc.RootElement.GetProperty("Zdr").GetProperty("Dato").GetString());

        // Filen har PRAECIS de to grene og ikke andet. Et felt mere ville
        // vaere et sted, konto-oplysninger kunne lande.
        Assert.Equal(2, doc.RootElement.EnumerateObject().Count());
    }
}
