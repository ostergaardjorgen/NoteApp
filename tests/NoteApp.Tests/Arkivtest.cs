using NoteApp.Core;
using NoteApp.Core.Deling;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Arkivet: historikken lagt op i den fælles mappe, så den kan hentes hjem.
/// </summary>
/// <remarks>
/// ============ DET, PRØVERNE HER PASSER PÅ ============
///
/// At den PRIVATE NØGLE aldrig kommer med. Den ligger i datamappen ved siden af
/// alt det, der skal arkiveres, og en dag, nogen laver arkivet om til «hele
/// datamappen minus nogle undtagelser», er den her prøven, der siger fra.
///
/// At en optagelse, DER ER I GANG, ikke bliver arkiveret halvt. En halv wav i
/// arkivet ser hel ud og bliver aldrig hentet igen.
///
/// At en SLETNING ikke rejser. Det er hele forskellen på en sikkerhedskopi og
/// en spejling, og det er den, man opdager for sent.
///
/// At «hent hjem» ALDRIG overskriver noget lokalt.
/// </remarks>
[Collection(Maskinhold.Navn)]
public class Arkivtest
{
    private static (Proevemaskine A, Proevemaskine B, string Delt) Toparrede()
    {
        var delt = Proevemaskine.Nydeltmappe();
        Delt.Klargoer(delt);

        var a = new Proevemaskine("Stationær", delt);
        var b = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        Proevemaskine.Godkend(a, b);

        return (a, b, delt);
    }

    /// <summary>Et færdigt møde med lyd, udskrift og en note.</summary>
    private static string Moede(string titel, bool faerdig = true, int lydbyte = 5000)
    {
        var mappe = MeetingStore.CreateSessionDirectory(titel, DateTimeOffset.Now);

        var meta = new MeetingMetadata
        {
            Title = titel,
            StartedAt = DateTimeOffset.Now.AddMinutes(-10),
            EndedAt = faerdig ? DateTimeOffset.Now : null,
            DurationSeconds = 600,
        };

        MeetingStore.Save(mappe, meta);

        File.WriteAllBytes(Path.Combine(mappe, "mikrofon.wav"), new byte[lydbyte]);
        File.WriteAllText(Path.Combine(mappe, "udskrift_large-v3.txt"), "Det, der blev sagt.");

        return mappe;
    }

    // ==================================================================== det, der kommer op

    [Fact]
    public void Et_faerdigt_moede_kommer_op_i_arkivet()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Moede("Tandlæge");

        var kom = Arkiv.Gem(medLyd: true);

        Assert.True(kom.Filer >= 3);          // meeting.json, wav og udskrift
        Assert.Equal(1, kom.Lydfiler);

        var mit = Arkiv.Mit(delt);

        Assert.True(File.Exists(Path.Combine(mit, Arkiv.Maerkefil)));

        var lyd = Directory.GetFiles(Path.Combine(mit, "optagelser"), "mikrofon.wav",
                                     SearchOption.AllDirectories);

        Assert.Single(lyd);

        // ============ ANDEN KOERSEL SENDER IKKE DET SAMME IGEN ============
        //
        // Uden det ville 1,8 GB gaa over netvaerket hvert kvarter, hele dagen.
        Assert.False(Arkiv.Gem(medLyd: true).Noget);
    }

    [Fact]
    public void Den_private_noegle_kommer_aldrig_med()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Moede("Et møde");

        // Noeglen ligger i datamappen ved siden af alt det, der SKAL med.
        var noeglen = Path.Combine(UserDataPaths.Root, "deling", "noegle.txt");

        Assert.True(File.Exists(noeglen), "prøven giver kun mening, hvis nøglen findes");

        var hemmelig = File.ReadAllText(noeglen);

        Arkiv.Gem(medLyd: true);

        // ============ INTET STED I ARKIVET ============
        //
        // Hverken filen eller dens indhold. En hvidliste kan glemme at tage
        // noget MED; en sortliste kan glemme at holde noget UDE, og det her er
        // den ene fil, hvor det ville vaere alvorligt.
        foreach (var f in Directory.EnumerateFiles(Arkiv.Rod(delt), "*", SearchOption.AllDirectories))
        {
            Assert.NotEqual("noegle.txt", Path.GetFileName(f));

            if (new FileInfo(f).Length > 100_000) continue;

            Assert.DoesNotContain(hemmelig, File.ReadAllText(f));
        }
    }

    [Fact]
    public void En_optagelse_der_er_i_gang_arkiveres_ikke()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Moede("Kører lige nu", faerdig: false);

        // ============ EN HALV WAV SER HEL UD ============
        //
        // Stoerrelsen passer med det, der blev laest, saa naeste koersel
        // springer den over. Moedet ville staa i arkivet for evigt med de
        // foerste to minutter af en time.
        Assert.False(Arkiv.Gem(medLyd: true).Noget);

        Assert.False(Directory.Exists(Path.Combine(Arkiv.Mit(delt), "optagelser")));
    }

    [Fact]
    public void Uden_lyd_kommer_alt_det_skrevne_stadig_med()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Moede("Med en udskrift");

        var kom = Arkiv.Gem(medLyd: false);

        Assert.True(kom.Noget);
        Assert.Equal(0, kom.Lydfiler);

        var i_arkivet = Directory.GetFiles(Arkiv.Mit(delt), "*", SearchOption.AllDirectories);

        Assert.Contains(i_arkivet, f => Path.GetFileName(f) == "udskrift_large-v3.txt");
        Assert.DoesNotContain(i_arkivet, f => Path.GetFileName(f) == "mikrofon.wav");

        // Og lyden kommer med, naar hakket saettes - uden at det skrevne
        // sendes een gang til.
        var bagefter = Arkiv.Gem(medLyd: true);

        Assert.Equal(1, bagefter.Filer);
        Assert.Equal(1, bagefter.Lydfiler);
    }

    // ==================================================================== sletningen

    [Fact]
    public void En_slettet_optagelse_bliver_liggende_i_arkivet()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        var mappe = Moede("Slettet ved et uheld");

        Arkiv.Gem(medLyd: true);

        Directory.Delete(mappe, recursive: true);

        Arkiv.Gem(medLyd: true);

        // ============ ET ARKIV, DER SLETTER, ER EN SPEJLING ============
        //
        // Og en spejling redder ingen den dag, noget bliver slettet ved en
        // fejl. Journalen for aftaler skriver gravsten og goer det MODSATTE -
        // se Delingsjournal. Det er rigtigt hver sit sted.
        var lyd = Directory.GetFiles(Arkiv.Mit(delt), "mikrofon.wav", SearchOption.AllDirectories);

        Assert.Single(lyd);
    }

    // ==================================================================== hent hjem

    [Fact]
    public void Den_anden_maskine_kan_hente_historikken_hjem()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Moede("Fra den stationære");
        Arkiv.Gem(medLyd: true);

        var fra = Maskinid.Id;

        // Den baerbare er tom - som en helt ny maskine.
        b.Tag();

        Assert.Empty(MeetingStore.Alle());

        var arkiver = Arkiv.Andres();

        Assert.Single(arkiver);
        Assert.Equal(fra, arkiver[0].Id);

        var kom = Arkiv.Hent(fra, medLyd: true);

        Assert.True(kom.Filer >= 3);

        var moede = Assert.Single(MeetingStore.Alle());

        Assert.Equal("Fra den stationære", moede.Title);

        // Anden gang er der intet at hente. Uden det ville knappen blive ved
        // med at love noget, den ikke har.
        Assert.False(Arkiv.Hent(fra, medLyd: true).Noget);
    }

    [Fact]
    public void Der_overskrives_aldrig_noget_hjemme()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        var hos_a = Moede("Det samme møde");
        Arkiv.Gem(medLyd: true);

        var fra = Maskinid.Id;
        var navn = Path.GetFileName(hos_a);

        // Den baerbare har ALLEREDE en mappe med det navn - og noget andet i.
        b.Tag();

        var hos_b = Path.Combine(UserDataPaths.Meetings, navn);
        Directory.CreateDirectory(hos_b);
        File.WriteAllText(Path.Combine(hos_b, "udskrift_large-v3.txt"), "Dagens arbejde.");

        Arkiv.Hent(fra, medLyd: true);

        // ============ DET LOKALE ER DET, DER ER I BRUG ============
        //
        // En gendannelse, der kan skrive hen over dagens arbejde, er en, man
        // ikke toer trykke paa.
        Assert.Equal("Dagens arbejde.",
                     File.ReadAllText(Path.Combine(hos_b, "udskrift_large-v3.txt")));

        // Det, der MANGLEDE, kom hjem.
        Assert.True(File.Exists(Path.Combine(hos_b, "mikrofon.wav")));
    }

    // ================================================================== halve filer

    [Fact]
    public void En_halv_kopi_bliver_ikke_taget_for_en_hel()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Moede("Afbrudt undervejs");
        Arkiv.Gem(medLyd: true);

        // Saadan ser en afbrudt kopi ud: en fil med endelsen paa.
        var mit = Arkiv.Mit(delt);
        var halv = Path.Combine(mit, "optagelser", "halvvejs.wav" + Arkiv.Halvvejs);

        File.WriteAllBytes(halv, new byte[9999]);

        var fra = Maskinid.Id;

        b.Tag();

        // Den taeller ikke med, og den hentes ikke hjem. Havde den gjort det,
        // ville en afbrudt kopi blive til en «hel» fil paa den anden maskine.
        var plan = Arkiv.Hjemplan(fra, medLyd: true);

        Assert.DoesNotContain(plan, f => f.Kilde.EndsWith(Arkiv.Halvvejs));
    }
}
