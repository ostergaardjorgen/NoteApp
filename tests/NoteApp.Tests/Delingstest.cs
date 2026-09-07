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
    // MASKINEN OG DEN FAELLES MAPPE LIGGER I Proevemaskine.cs. De bruges af
    // arbejdskoeens proever ogsaa, og to kopier ville drive fra hinanden.

    private static string Nydeltmappe() => Proevemaskine.Nydeltmappe();

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

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        stationaer.Tag();
        Assert.True(Delt.Meld());

        baerbar.Tag();
        Assert.True(Delt.Meld());

        // Den bærbare ser den stationære, og ikke sig selv i «de andre».
        var andre = Delt.Andre();
        var set = Assert.Single(andre);

        Assert.Equal("Stationær", set.Navn);
        Assert.Equal(Maskinrolle.Primaer, set.Rolle);
        Assert.True(set.ILive);

        Assert.Equal(2, Delt.Alle().Count);
    }

    [Fact]
    public void To_maskiner_faar_ikke_det_samme_id()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Proevemaskine("A", delt);
        using var b = new Proevemaskine("B", delt);

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

        using var a = new Proevemaskine("A", delt);
        using var b = new Proevemaskine("B", delt);

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

        using var a = new Proevemaskine("A", delt);
        using var b = new Proevemaskine("B", delt);
        using var c = new Proevemaskine("C", delt);

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

        using var a = new Proevemaskine("A", delt);
        using var b = new Proevemaskine("B", delt);

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

        using var a = new Proevemaskine("A", delt);
        using var b = new Proevemaskine("B", delt);
        using var tyv = new Proevemaskine("Tyven", delt);

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

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

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

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

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

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        stationaer.Tag();
        Delt.Meld();

        baerbar.Tag();
        Delt.Meld();

        var den = Delt.Andre().Single();
        Parring.Betro(den);

        // Nogen retter i filen i den faelles mappe og saetter sin EGEN
        // offentlige noegle ind under det parrede id. Det er praecis dét,
        // pinningen er til for.
        using var tyv = new Proevemaskine("Tyven", delt);
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

        using var a = new Proevemaskine("A", delt);
        using var b = new Proevemaskine("B", delt);

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

    // ================================================================ nøglen over

    /// <summary>Sætter en API-nøgle på den maskine, koden står på.</summary>
    private static void SaetNoegle(string? noegle)
    {
        // Miljoevariablen vinder over filen - se SkyNoegle.Hent. Proeven maa
        // derfor tage den vaek, mens den koerer, ellers maaler den paa
        // udviklingsmaskinens egen noegle.
        Environment.SetEnvironmentVariable(NoteApp.Core.Llm.SkyNoegle.Miljoevariabel, null);

        if (noegle is null) NoteApp.Core.Llm.SkyNoegle.Slet();
        else NoteApp.Core.Llm.SkyNoegle.Gem(noegle);
    }

    [Fact]
    public void Noeglen_kan_sendes_til_en_parret_computer()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        stationaer.Tag();
        SaetNoegle("hemmelig-noegle-1234567890");
        Delt.Meld();

        baerbar.Tag();
        SaetNoegle(null);
        Delt.Meld();

        // Begge godkender hinanden — som når et menneske har set koden.
        Parring.Betro(Delt.Andre().Single());

        stationaer.Tag();
        Parring.Betro(Delt.Andre().Single());

        Assert.True(Noegledeling.Send(Delt.Andre().Single()));

        // ============ DEN LIGGER IKKE I KLARTEKST ============
        //
        // Den, der kan laese den faelles mappe, skal se et tilfaeldigt tal.
        var kuvert = Directory.EnumerateFiles(Path.Combine(delt, "noegler"), "*.json").Single();
        var tekst = File.ReadAllText(kuvert);

        Assert.DoesNotContain("hemmelig-noegle", tekst);

        baerbar.Tag();
        SaetNoegle(null);

        Assert.Equal(Noegledeling.Udfald.Hentet, Noegledeling.Hent());
        Assert.Equal("hemmelig-noegle-1234567890", NoteApp.Core.Llm.SkyNoegle.Hent());

        // KUVERTEN ER RYDDET. En hemmelighed, der bliver liggende, er en
        // hemmelighed mere, nogen skal huske at rydde op efter.
        Assert.False(File.Exists(kuvert));
        Assert.Equal(Noegledeling.Udfald.Ingenting, Noegledeling.Hent());
    }

    [Fact]
    public void Der_sendes_ikke_til_en_der_ikke_er_godkendt()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Proevemaskine("A", delt);
        using var b = new Proevemaskine("B", delt);

        a.Tag();
        SaetNoegle("hemmelig-noegle-1234567890");
        Delt.Meld();

        b.Tag();
        Delt.Meld();

        a.Tag();

        // PARRINGEN ER TILLADELSEN. Uden den ved vi ikke, hvis noegle vi
        // krypterer til - og en API-noegle sendt til den forkerte kan ikke
        // kaldes tilbage.
        Assert.Throws<InvalidOperationException>(() => Noegledeling.Send(Delt.Andre().Single()));
    }

    [Fact]
    public void En_tredje_computer_kan_ikke_aabne_kuverten()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);
        using var tyv = new Proevemaskine("Tyven", delt);

        stationaer.Tag();
        SaetNoegle("hemmelig-noegle-1234567890");
        Delt.Meld();

        baerbar.Tag();
        SaetNoegle(null);
        Delt.Meld();
        Parring.Betro(Delt.Andre().Single(m => m.Navn == "Stationær"));

        stationaer.Tag();
        var tilBaerbar = Delt.Andre().Single(m => m.Navn == "Bærbar");
        Parring.Betro(tilBaerbar);
        Noegledeling.Send(tilBaerbar);

        // Tyven doeber sin egen fil om til den baerbares id og haaber paa det
        // bedste. Kuverten er laast med en noegle, kun de to kan regne ud.
        tyv.Tag();
        SaetNoegle(null);

        // HAN GODKENDER ENDDA AFSENDEREN. Det staar ham frit - en godkendelse
        // er noget, han giver paa sin egen maskine, og den gaelder kun dér.
        // Kuverten er stadig krypteret til den baerbares noegle.
        Parring.Betro(Delt.Andre().Single(m => m.Navn == "Stationær"));

        var kuvert = Path.Combine(delt, "noegler", tilBaerbar.Id + ".json");
        File.Copy(kuvert, Path.Combine(delt, "noegler", Maskinid.Id + ".json"));

        Assert.Equal(Noegledeling.Udfald.Afvist, Noegledeling.Hent());
        Assert.Null(NoteApp.Core.Llm.SkyNoegle.Hent());
    }

    [Fact]
    public void En_noegle_der_allerede_findes_bliver_ikke_byttet_ud()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var a = new Proevemaskine("A", delt);
        using var b = new Proevemaskine("B", delt);

        a.Tag();
        SaetNoegle("noegle-fra-A-0123456789");
        Delt.Meld();

        b.Tag();
        SaetNoegle("min-egen-noegle-9876543210");
        Delt.Meld();
        Parring.Betro(Delt.Andre().Single());

        a.Tag();
        Parring.Betro(Delt.Andre().Single());
        Noegledeling.Send(Delt.Andre().Single());

        b.Tag();

        // HAR MAN SELV TASTET EN IND, ER DÉT VALGET. En kuvert fra i
        // forgaars maa ikke stille og roligt bytte den ud.
        Assert.Equal(Noegledeling.Udfald.Ingenting, Noegledeling.Hent());
        Assert.Equal("min-egen-noegle-9876543210", NoteApp.Core.Llm.SkyNoegle.Hent());
    }

    // ====================================================== google-forbindelsen

    [Fact]
    public void Google_forbindelsen_kan_sendes_til_en_parret_computer()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        stationaer.Tag();
        SaetNoegle(null);
        Integrationsfiler.Gem(Googlekalender.Id,
            new Integrationsopsaetning { Opdateringsnoegle = "opdateringsnoegle-fra-google-42" });
        Delt.Meld();

        baerbar.Tag();
        SaetNoegle(null);
        Delt.Meld();
        Parring.Betro(Delt.Andre().Single());

        stationaer.Tag();
        Parring.Betro(Delt.Andre().Single());

        Assert.True(Noegledeling.Send(Delt.Andre().Single(), Kuvertslags.Googlekalender));

        // DEN LIGGER IKKE I KLARTEKST. Forbindelsen er hele adgangen til
        // brugerens kalender, og den udloeber ikke af sig selv.
        var kuvert = Path.Combine(delt, "noegler",
            $"{Delt.Andre().Single().Id}-{Googlekalender.Id}.json");

        Assert.DoesNotContain("opdateringsnoegle-fra-google", File.ReadAllText(kuvert));

        baerbar.Tag();

        var hentet = Noegledeling.HentAlle();

        Assert.Equal((Kuvertslags.Googlekalender, Noegledeling.Udfald.Hentet), Assert.Single(hentet));
        Assert.Equal("opdateringsnoegle-fra-google-42",
                     Integrationsfiler.Hent(Googlekalender.Id).Opdateringsnoegle);

        // MAN SKAL IKKE LOGGE IND HOS GOOGLE EEN GANG TIL. Og kuverten er
        // ryddet, saa adgangen ikke bliver liggende i en mappe.
        Assert.False(File.Exists(kuvert));
    }

    [Fact]
    public void En_kuvert_kan_ikke_laeses_som_en_anden_slags()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        stationaer.Tag();
        SaetNoegle(null);
        Integrationsfiler.Gem(Googlekalender.Id,
            new Integrationsopsaetning { Opdateringsnoegle = "opdateringsnoegle-fra-google-42" });
        Delt.Meld();

        baerbar.Tag();
        SaetNoegle(null);
        Delt.Meld();
        Parring.Betro(Delt.Andre().Single());

        stationaer.Tag();
        var tilBaerbar = Delt.Andre().Single();
        Parring.Betro(tilBaerbar);
        Noegledeling.Send(tilBaerbar, Kuvertslags.Googlekalender);

        // ============ FORMAALET ER BUNDET TIL KRYPTERINGEN ============
        //
        // Kuverten doebes om til den, API-noeglen ville have. Uden formaalet i
        // udledningen ville den kunne aabnes og en Google-adgang ende som
        // appens noegle til sprogmodellen.
        var mappe = Path.Combine(delt, "noegler");
        File.Move(Path.Combine(mappe, $"{tilBaerbar.Id}-{Googlekalender.Id}.json"),
                  Path.Combine(mappe, $"{tilBaerbar.Id}.json"));

        baerbar.Tag();

        Assert.Equal(Noegledeling.Udfald.Afvist, Noegledeling.Hent(Kuvertslags.Apinoegle));
        Assert.Null(NoteApp.Core.Llm.SkyNoegle.Hent());
    }

    [Fact]
    public void En_kuvert_bliver_liggende_til_der_er_sagt_ja()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        stationaer.Tag();
        SaetNoegle("hemmelig-noegle-1234567890");
        Delt.Meld();

        baerbar.Tag();
        SaetNoegle(null);
        Delt.Meld();
        Parring.Betro(Delt.Andre().Single());
        Delt.Meld();

        stationaer.Tag();
        Parring.Betro(Delt.Andre().Single());
        Noegledeling.Send(Delt.Andre().Single());

        // Den baerbare har godkendt den stationaere - men her spilles den
        // anden vej: den stationaere har IKKE godkendt afsenderen af en
        // kuvert til sig selv. Vi tager derfor kuverten fra den baerbares
        // side og glemmer godkendelsen dér.
        baerbar.Tag();
        Parring.Glem(Delt.Andre().Single().Id);

        var kuvert = Path.Combine(delt, "noegler", Maskinid.Id + ".json");

        Assert.True(File.Exists(kuvert));

        // ============ DEN SMIDES IKKE VAEK ============
        //
        // Foer blev den ryddet som «afvist», og saa var noeglen vaek, uden at
        // nogen havde gjort noget forkert. Det eneste, der mangler, er et ja
        // paa den her skaerm.
        Assert.Equal(Noegledeling.Udfald.Ikkegodkendt, Noegledeling.Hent());
        Assert.True(File.Exists(kuvert));
        Assert.Null(NoteApp.Core.Llm.SkyNoegle.Hent());

        // Og saa siger man ja.
        Parring.Betro(Delt.Andre().Single());

        Assert.Equal(Noegledeling.Udfald.Hentet, Noegledeling.Hent());
        Assert.Equal("hemmelig-noegle-1234567890", NoteApp.Core.Llm.SkyNoegle.Hent());
        Assert.False(File.Exists(kuvert));
    }

    [Fact]
    public void Maskinen_siger_hvem_den_har_godkendt()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        stationaer.Tag();
        Delt.Meld();
        var mitId = Maskinid.Id;

        baerbar.Tag();
        Delt.Meld();

        stationaer.Tag();

        // Endnu ingen godkendelse: listen er tom, og det er et svar - ikke
        // et «ved ikke».
        Assert.False(Delt.Andre().Single().HarGodkendt(mitId));

        baerbar.Tag();
        Parring.Betro(Delt.Andre().Single());
        Delt.Meld();

        stationaer.Tag();

        // DEN ANDEN SIDE KAN NU SES. Det er dét, der goer, at den maskine,
        // der mangler at sige ja, kan spoerge af sig selv.
        Assert.True(Delt.Andre().Single().HarGodkendt(mitId));
    }

    // ================================================================ vejledningen

    [Fact]
    public void Den_der_kun_har_een_computer_faar_ingen_vejledning()
    {
        var delt = Nydeltmappe();

        using var alene = new Proevemaskine("Alene", delt);

        alene.Tag();
        Maskinid.Deltmappe = null;

        // TALLET VED MENUPUNKTET STAAR KUN FOR DEN, DER HAR TO COMPUTERE. En
        // primaer maskine uden faelles mappe deler ikke med nogen.
        Assert.False(Delingsguide.Paabegyndt);
    }

    [Fact]
    public void Vejledningen_peger_paa_det_naeste_trin()
    {
        var delt = Nydeltmappe();

        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        baerbar.Tag();
        SaetNoegle(null);
        Maskinid.Deltmappe = null;

        // Rollen alene raekker: den, der har sagt «det her er min anden
        // computer», skal vejledes, ogsaa foer der er en mappe.
        Assert.True(Delingsguide.Paabegyndt);

        var trin = Delingsguide.Trin();

        Assert.Equal(5, trin.Count);
        Assert.Equal(Trinstand.Klar, trin[0].Stand);
        Assert.Equal(Trinstand.Naeste, trin[1].Stand);
        Assert.Equal(Trinstand.Venter, trin[2].Stand);
        Assert.Equal(Trinstand.Venter, trin[3].Stand);
        Assert.Equal(4, Delingsguide.Mangler());
    }

    [Fact]
    public void Vejledningen_er_faerdig_naar_alt_er_paa_plads()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Proevemaskine("Stationær", delt);
        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        stationaer.Tag();
        SaetNoegle("hemmelig-noegle-1234567890");
        Delt.Meld();

        baerbar.Tag();
        SaetNoegle(null);
        Delt.Meld();

        // Mappen er der, og den anden computer har meldt sig - men de er ikke
        // godkendt endnu. Saa er der to trin tilbage: godkendelsen og
        // noeglen, der foelger med den.
        Assert.Equal(2, Delingsguide.Mangler());

        Parring.Betro(Delt.Andre().Single());
        Delt.Meld();
        SaetNoegle("hemmelig-noegle-1234567890");

        // ============ EEN HALVDEL ER IKKE NOK ============
        //
        // Vi har godkendt den anden. Den har ikke godkendt os, og saa kan der
        // ikke udveksles noget. Stod der hak her, ville skaermen melde
        // faerdigt, mens ingenting virkede - og det var praecis dét, der
        // skete 07-09-2026: noeglen blev sendt og afvist i stilhed.
        Assert.False(Delingsguide.Faerdig());

        stationaer.Tag();
        Parring.Betro(Delt.Andre().Single());
        Delt.Meld();

        baerbar.Tag();

        Assert.True(Delingsguide.Faerdig());
    }

    // ============================================================ maskinen i loggen

    [Fact]
    public void Historikken_siger_hvilken_computer_der_gjorde_det()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var stationaer = new Proevemaskine("Stationær", delt);

        stationaer.Tag();

        Historik.Skriv(HaendelseType.Dokument, "Referat oprettet", "prøve",
            NoteApp.Core.Udfald.Fuldført, "Mistral Medium 3.5");

        var post = Historik.Laes().First();

        // NAAR TO COMPUTERE SENDER TIL DEN SAMME KONTO, er «hvem gjorde det»
        // det foerste, en revision spoerger om.
        Assert.Equal("Stationær", post.Maskine);
    }

    [Fact]
    public void Kvitteringen_siger_hvilken_computer_der_sendte()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        baerbar.Tag();

        Kvitteringer.Skriv(new Kvittering
        {
            Endepunkt = "https://api.eu.mistral.ai/v1/chat/completions",
            Model = "mistral-medium-latest",
            Skabelon = "Mødereferat",
            Tegn = 1234,
        });

        Assert.Equal("Bærbar", Kvitteringer.Laes().First().Maskine);
    }

    // =================================================================== demoen

    [Fact]
    public void Maskinens_identitet_ligger_hos_maskinen_og_ikke_i_datasaettet()
    {
        var delt = Nydeltmappe();
        Delt.Klargoer(delt);

        using var m = new Proevemaskine("Stationær", delt);
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
