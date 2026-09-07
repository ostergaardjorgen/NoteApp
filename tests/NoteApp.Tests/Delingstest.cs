using NoteApp.Core;
using NoteApp.Core.Deling;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// To maskiner, én fælles mappe — og spørgsmålet om, hvordan de ved, at det er
/// hinanden.
/// </summary>
/// <remarks>
/// ============ DET, PRØVERNE HER PASSER PÅ ============
///
/// At en tredje maskine ikke kan melde sig ind og få lagt arbejde igennem, og
/// at en, der skifter nøgle bagefter, IKKE bliver godkendt i stilhed. Det er
/// den eneste af de to, der er svær: en angriber, der kan skrive i mappen, kan
/// bytte en offentlig nøgle ud, og så skal det opdages.
///
/// Maskinerne spilles af hver sin «Maskinrod» — nøjagtig som to rigtige PC'er,
/// der deler en mappe men ikke har adgang til hinandens diske.
/// </remarks>
public class Delingstest
{
    /// <summary>
    /// En maskine: sin egen maskinrod, sit eget nøglepar.
    /// </summary>
    /// <remarks>
    /// Maskinrod udledes af datamappen, og prøvernes datamappe kommer fra
    /// miljøvariablen. Ved at skifte den skifter vi maskine — det er dét, der
    /// gør, at to installationer kan spilles på én computer uden at snyde:
    /// de deler ingen filer ud over den fælles mappe.
    /// </remarks>
    private sealed class Maskine : IDisposable
    {
        private readonly string? _foer;

        public Maskine(string navn, string delt, Maskinrolle rolle = Maskinrolle.Arbejdsstation)
        {
            Sti = Path.Combine(Path.GetTempPath(), "heypia-maskine", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Sti);

            _foer = Environment.GetEnvironmentVariable(UserDataPaths.OverrideVariable);

            Tag();

            Maskinid.Navn = navn;
            Maskinid.Rolle = rolle;
            Maskinid.Deltmappe = delt;

            Navn = navn;
            Slip();
        }

        public string Sti { get; }
        public string Navn { get; }

        /// <summary>Sætter den her maskine som den, koden kører på.</summary>
        public void Tag()
        {
            Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, Sti);
            UserDataPaths.Glem();
        }

        private void Slip()
        {
            Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, _foer);
            UserDataPaths.Glem();
        }

        public void Dispose()
        {
            Slip();

            try { if (Directory.Exists(Sti)) Directory.Delete(Sti, recursive: true); }
            catch (IOException) { /* temp rydder Windows selv */ }
        }
    }

    private static string Nydeltmappe()
    {
        var sti = Path.Combine(Path.GetTempPath(), "heypia-delt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sti);
        return sti;
    }

    // ================================================================ selve mappen

    [Fact]
    public void En_mappe_bliver_til_en_deling_een_gang()
    {
        var delt = Nydeltmappe();

        Assert.False(Delt.Er(delt));

        var f = Delt.Klargoer(delt, "Hjemmenettet");
        Assert.True(Delt.Er(delt));

        // DEN ANDEN MASKINE OPRETTER IKKE NOGET. Gjorde den det, ville den
        // overskrive den førstes id, og de to ville tro, de var i hver sin
        // deling.
        var igen = Delt.Klargoer(delt, "Noget andet");

        Assert.Equal(f.Id, igen.Id);
        Assert.Equal("Hjemmenettet", igen.Navn);
    }

    [Fact]
    public void To_maskiner_kan_se_hinanden()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Maskine("Stationær", delt);
        using var baerbar = new Maskine("Bærbar", delt, Maskinrolle.Let);

        stationaer.Tag();
        Assert.True(Delt.Meld());

        baerbar.Tag();
        Assert.True(Delt.Meld());

        // Den bærbare ser den stationære, og ikke sig selv i «de andre».
        var andre = Delt.Andre();
        var set = Assert.Single(andre);

        Assert.Equal("Stationær", set.Navn);
        Assert.Equal(Maskinrolle.Arbejdsstation, set.Rolle);
        Assert.True(set.ILive);

        Assert.Equal(2, Delt.Alle().Count);
    }

    [Fact]
    public void To_maskiner_faar_ikke_det_samme_id()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Maskine("A", delt);
        using var b = new Maskine("B", delt);

        a.Tag();
        var idA = Maskinid.Id;

        b.Tag();
        Assert.NotEqual(idA, Maskinid.Id);
    }

    // ===================================================================== nøglen

    [Fact]
    public void Begge_regner_sig_frem_til_den_samme_faellesnoegle()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Maskine("A", delt);
        using var b = new Maskine("B", delt);

        a.Tag();
        var offentligA = Maskinid.Offentlignoegle();

        b.Tag();
        var offentligB = Maskinid.Offentlignoegle();
        var fraB = Maskinid.Faellesnoegle(offentligA);

        a.Tag();
        var fraA = Maskinid.Faellesnoegle(offentligB);

        // DEN UDVEKSLES ALDRIG. Begge regner den ud af sin egen private nøgle
        // og modpartens offentlige — den står ingen steder i mappen, så den,
        // der kan læse mappen, kan ikke regne den ud.
        Assert.Equal(fraA, fraB);
        Assert.Equal(32, fraA.Length);
    }

    [Fact]
    public void En_tredje_maskine_faar_en_anden_noegle()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Maskine("A", delt);
        using var b = new Maskine("B", delt);
        using var c = new Maskine("C", delt);

        a.Tag();
        var offentligA = Maskinid.Offentlignoegle();

        b.Tag();
        var abFraB = Maskinid.Faellesnoegle(offentligA);

        c.Tag();
        var acFraC = Maskinid.Faellesnoegle(offentligA);

        Assert.NotEqual(abFraB, acFraC);
    }

    // ===================================================================== koden

    [Fact]
    public void Koden_er_den_samme_paa_begge_skaerme()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Maskine("A", delt);
        using var b = new Maskine("B", delt);

        a.Tag();
        var pa = Maskinid.Offentlignoegle();

        b.Tag();
        var pb = Maskinid.Offentlignoegle();

        // REKKEFØLGEN MÅ IKKE TÆLLE. De to maskiner kender ikke hinandens
        // «tur»; var koden forskellig, ville den se ud som om nogen sad i
        // midten hver eneste gang.
        Assert.Equal(Parring.Kode(pa, pb), Parring.Kode(pb, pa));
        Assert.Equal(6, Parring.Kode(pa, pb).Length);
        Assert.Contains(" ", Parring.Kodevisning(pa, pb));
    }

    [Fact]
    public void En_der_saetter_sig_i_midten_faar_ikke_koden_til_at_passe()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Maskine("A", delt);
        using var b = new Maskine("B", delt);
        using var tyv = new Maskine("Tyven", delt);

        a.Tag();
        var pa = Maskinid.Offentlignoegle();

        b.Tag();
        var pb = Maskinid.Offentlignoegle();

        tyv.Tag();
        var pt = Maskinid.Offentlignoegle();

        // Tyven har byttet B's noegle ud med sin egen i mappen. A ser altsaa
        // «A + Tyven», mens B stadig ser «A + B» - og de to koder er
        // forskellige. Det er dét, oejnene fanger.
        Assert.NotEqual(Parring.Kode(pa, pt), Parring.Kode(pa, pb));
    }

    // ================================================================== parringen

    [Fact]
    public void Der_udveksles_intet_med_en_maskine_der_ikke_er_parret()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Maskine("Stationær", delt);
        using var baerbar = new Maskine("Bærbar", delt, Maskinrolle.Let);

        stationaer.Tag();
        Delt.Meld();

        baerbar.Tag();
        Delt.Meld();

        var den = Delt.Andre().Single();

        Assert.Equal(Parringstilstand.Ukendt, Parring.Tilstand(den));
        Assert.False(Parring.MaaUdveksle(den));

        // Brugeren har set koden og trykket godkend.
        Parring.Betro(den);

        Assert.Equal(Parringstilstand.Parret, Parring.Tilstand(den));
        Assert.True(Parring.MaaUdveksle(den));
    }

    [Fact]
    public void En_maskine_der_skifter_noegle_bliver_ikke_godkendt_i_stilhed()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Maskine("Stationær", delt);
        using var baerbar = new Maskine("Bærbar", delt, Maskinrolle.Let);

        baerbar.Tag();
        Delt.Meld();

        stationaer.Tag();
        Delt.Meld();

        baerbar.Tag();
        Parring.Betro(Delt.Andre().Single());
        Assert.True(Parring.MaaUdveksle(Delt.Andre().Single()));

        // ============ OG SAA SKIFTER DEN NOEGLE ============
        //
        // Enten er appen installeret forfra paa den stationaere, eller ogsaa
        // staar der en anden maskine og udgiver sig for den. Appen kan ikke se
        // forskel - og derfor goer den ingen af delene.
        stationaer.Tag();
        Maskinid.Nulstil();
        Maskinid.Navn = "Stationær";
        Maskinid.Deltmappe = delt;
        Delt.Meld();

        baerbar.Tag();

        // ============ DEN NYE ER UKENDT ============
        //
        // Den gamle fil bliver liggende med sit gamle hjerteslag, og den er
        // stadig «parret» - det er jo den maskine, vi parrede med. Den er
        // uskadelig: ingen har dens private noegle laengere, saa der kan ikke
        // udveksles med den.
        //
        // Det, der betyder noget, er, at den NYE ikke bliver godkendt af sig
        // selv, fordi den baerer det samme navn.
        var alle = Delt.Andre().Where(m => m.Navn == "Stationær").ToList();

        Assert.Equal(2, alle.Count);
        Assert.Contains(alle, m => Parring.Tilstand(m) == Parringstilstand.Ukendt);

        // Og den foraeldreloese kan ryddes, naar brugeren har set den.
        var gammel = alle.Single(m => Parring.Tilstand(m) == Parringstilstand.Parret);

        Assert.True(Delt.Fjern(gammel.Id));
        Assert.Single(Delt.Andre().Where(m => m.Navn == "Stationær"));
    }

    [Fact]
    public void En_byttet_noegle_paa_det_samme_id_opdages()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Maskine("Stationær", delt);
        using var baerbar = new Maskine("Bærbar", delt, Maskinrolle.Let);

        stationaer.Tag();
        Delt.Meld();

        baerbar.Tag();
        Delt.Meld();

        var den = Delt.Andre().Single();
        Parring.Betro(den);

        // Nogen retter i filen i den faelles mappe og saetter sin EGEN
        // offentlige noegle ind under det parrede id. Det er praecis dét,
        // pinningen er til for.
        using var tyv = new Maskine("Tyven", delt);
        tyv.Tag();
        var tyvensNoegle = Maskinid.Offentlignoegle();

        baerbar.Tag();

        var falsk = new Maskinoplysning
        {
            Id = den.Id,
            Navn = den.Navn,
            Rolle = den.Rolle,
            Noegle = tyvensNoegle,
            SidstSet = DateTimeOffset.Now,
        };

        Assert.Equal(Parringstilstand.Nyngle, Parring.Tilstand(falsk));
        Assert.False(Parring.MaaUdveksle(falsk));
    }

    [Fact]
    public void En_parring_kan_fortrydes()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Maskine("A", delt);
        using var b = new Maskine("B", delt);

        a.Tag();
        Delt.Meld();

        b.Tag();
        Delt.Meld();

        var den = Delt.Andre().Single();

        Parring.Betro(den);
        Assert.True(Parring.MaaUdveksle(den));

        Parring.Glem(den.Id);
        Assert.False(Parring.MaaUdveksle(den));
    }

    // =================================================================== demoen

    [Fact]
    public void Maskinens_identitet_ligger_hos_maskinen_og_ikke_i_datasaettet()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var m = new Maskine("Stationær", delt);
        m.Tag();

        Delt.Meld();

        // ============ DERFOR ARVER DEMOEN DEN IKKE ============
        //
        // Id, navn, rolle og noeglen ligger under Maskinrod - ikke i
        // indstillinger.json, som demoen kopierer. Laa de dér, ville demoen
        // melde sig i den faelles mappe med det SAMME id som maskinen selv,
        // og de to ville skrive i hinandens fil.
        var deling = Path.Combine(UserDataPaths.Maskinrod, "deling");

        Assert.True(File.Exists(Path.Combine(deling, "maskin.json")), "maskin.json");
        Assert.True(File.Exists(Path.Combine(deling, "noegle.txt")), "noegle.txt");

        // Og der staar ingenting om deling i det, der foelger datasaettet.
        var indstillinger = Path.Combine(UserDataPaths.Root, "indstillinger.json");

        if (File.Exists(indstillinger))
        {
            var tekst = File.ReadAllText(indstillinger);

            Assert.DoesNotContain(Maskinid.Id, tekst, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Deltmappe", tekst, StringComparison.OrdinalIgnoreCase);
        }
    }
}
