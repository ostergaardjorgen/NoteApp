using NoteApp.Core.Deling;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Køen: den bærbare optager, den kraftige skriver ud.
/// </summary>
/// <remarks>
/// ============ DET, PRØVERNE HER PASSER PÅ ============
///
/// At der ikke kan lægges arbejde ind af en, der ikke er godkendt — heller
/// ikke af en, der kan skrive i mappen og skriver et andet navn på opgaven.
/// Og at lyden ikke kan byttes ud, efter opgaven er lagt: seglet dækker
/// filens sum, ikke bare teksten i den.
///
/// Maskinerne spilles af hver sin datamappe, nøjagtig som i Delingstest.
/// </remarks>
public class Arbejdskoetest
{
    /// <summary>En lydfil med indhold, så summen betyder noget.</summary>
    private static string Lyd(string tekst = "det her er lyd")
    {
        var sti = Path.Combine(Path.GetTempPath(), "heypia-lyd", Guid.NewGuid().ToString("N") + ".wav");
        Directory.CreateDirectory(Path.GetDirectoryName(sti)!);
        File.WriteAllText(sti, tekst);

        return sti;
    }

    private static (Proevemaskine Baerbar, Proevemaskine Stationaer, string Delt) Toparrede()
    {
        var delt = Proevemaskine.Nydeltmappe();
        Delt.Klargoer(delt);

        var stationaer = new Proevemaskine("Stationær", delt);
        var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        Proevemaskine.Godkend(stationaer, baerbar);

        return (baerbar, stationaer, delt);
    }

    // ================================================================ hele vejen rundt

    [Fact]
    public void Lyden_gaar_ud_og_udskriften_kommer_hjem()
    {
        var (baerbar, stationaer, delt) = Toparrede();

        using var a = baerbar;
        using var b = stationaer;

        // ---------- den baerbare laegger sit spor ----------
        baerbar.Tag();

        var opgave = Arbejdskoe.Laeg(
            Delt.Andre().Single(), moede: "moede-1", spor: "mikrofon",
            lydfil: Lyd(), sprog: "da", model: "large-v3", ledetraad: "", sekunder: 92.5);

        Assert.True(File.Exists(Arbejdskoe.Lydsti(delt, opgave.Id)));
        Assert.Single(Arbejdskoe.Mine());
        Assert.Empty(Arbejdskoe.Venter());   // ens egen opgave er ikke ens eget arbejde

        // ---------- den stationaere tager den ----------
        stationaer.Tag();

        var fundet = Assert.Single(Arbejdskoe.Venter());

        Assert.Equal("moede-1", fundet.Moede);
        Assert.Equal("da", fundet.Sprog);
        Assert.Equal(92.5, fundet.Sekunder);
        Assert.True(Arbejdskoe.Lyden_passer(delt, fundet));

        Assert.True(Arbejdskoe.Tag(fundet));

        // TAGET ER TAGET. Den staar ikke laengere og venter paa nogen.
        Assert.Empty(Arbejdskoe.Venter());

        // ---------- og svarer ----------
        var tekst = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tekst, "Her er det, der blev sagt.");

        Arbejdskoe.Svar(fundet, tekst, null, sprog: "da", motor: "large-v3", sekunder: 41.2);

        // LYDEN ER RYDDET. Den har gjort sit, den fylder mest, og den er det
        // eneste i mappen, nogen kan lytte til.
        Assert.False(File.Exists(Arbejdskoe.Lydsti(delt, opgave.Id)));

        // ---------- den baerbare henter det hjem ----------
        baerbar.Tag();

        var svar = Arbejdskoe.Svar(opgave.Id);

        Assert.NotNull(svar);
        Assert.Equal("", svar!.Fejl);
        Assert.Equal("large-v3", svar.Motor);
        Assert.Equal("Her er det, der blev sagt.",
                     File.ReadAllText(Arbejdskoe.Udskriftsti(delt, opgave.Id, ".txt")));

        Arbejdskoe.Fjern(opgave.Id);

        Assert.Empty(Arbejdskoe.Mine());
    }

    // ==================================================================== seglet

    [Fact]
    public void En_fremmed_kan_ikke_laegge_arbejde_ind()
    {
        var (baerbar, stationaer, delt) = Toparrede();

        using var a = baerbar;
        using var b = stationaer;

        using var fremmed = new Proevemaskine("Fremmed", delt);

        fremmed.Tag();
        Delt.Meld();

        // Den fremmede kan skrive i mappen - det kan alle, der har adgang til
        // drevet. Den skriver den baerbares id paa opgaven og haaber paa det
        // bedste.
        stationaer.Tag();
        var baerbarsId = Delt.Andre().Single(m => m.Navn == "Bærbar").Id;

        fremmed.Tag();

        var lyd = Lyd("fremmed lyd");
        var id = Guid.NewGuid().ToString("N");
        var mappe = Arbejdskoe.Mappe(delt, id);

        Directory.CreateDirectory(mappe);
        File.Copy(lyd, Arbejdskoe.Lydsti(delt, id));

        var falsk = new Arbejdsopgave(
            id, baerbarsId, "", "moede-x", "mikrofon", "da", "large-v3", "",
            Arbejdskoe.Filsum(Arbejdskoe.Lydsti(delt, id)), 10, DateTimeOffset.Now,
            Maerke: "det-her-er-ikke-et-segl");

        File.WriteAllText(Path.Combine(mappe, "opgave.json"),
            System.Text.Json.JsonSerializer.Serialize(falsk));

        // ============ DEN STATIONAERE MAA IKKE GAA I GANG ============
        //
        // Uden seglet ville den skrive en fremmeds lyd ud og laegge svaret et
        // sted, hvor det kan laeses.
        stationaer.Tag();

        Assert.Empty(Arbejdskoe.Venter());
        Assert.Null(Arbejdskoe.Afsender(falsk));
    }

    [Fact]
    public void Lyden_kan_ikke_byttes_ud_bagefter()
    {
        var (baerbar, stationaer, delt) = Toparrede();

        using var a = baerbar;
        using var b = stationaer;

        baerbar.Tag();

        var opgave = Arbejdskoe.Laeg(
            Delt.Andre().Single(), "moede-2", "mikrofon", Lyd("den rigtige lyd"),
            "da", "large-v3", "", 30);

        // Nogen skriver en anden lyd ind under den samme opgave.
        File.WriteAllText(Arbejdskoe.Lydsti(delt, opgave.Id), "en helt anden lyd");

        stationaer.Tag();

        var fundet = Assert.Single(Arbejdskoe.Venter());

        // Opgaven ER fra den baerbare - teksten er uroert. Men lyden er en
        // anden, og DET er summen i seglet til for at fange.
        Assert.False(Arbejdskoe.Lyden_passer(delt, fundet));
    }

    [Fact]
    public void Et_svar_fra_en_fremmed_hentes_ikke_hjem()
    {
        var (baerbar, stationaer, delt) = Toparrede();

        using var a = baerbar;
        using var b = stationaer;

        baerbar.Tag();

        var opgave = Arbejdskoe.Laeg(
            Delt.Andre().Single(), "moede-3", "mikrofon", Lyd(), "da", "large-v3", "", 30);

        // En fremmed lægger et svar med den stationaeres id paa.
        stationaer.Tag();
        var stationaersId = NoteApp.Core.Deling.Maskinid.Id;

        var falsk = new Arbejdssvar(
            opgave.Id, stationaersId, opgave.Fra, "da", "large-v3", 1, "", DateTimeOffset.Now,
            Maerke: "heller-ikke-et-segl");

        File.WriteAllText(Path.Combine(Arbejdskoe.Mappe(delt, opgave.Id), "svar.json"),
            System.Text.Json.JsonSerializer.Serialize(falsk));

        baerbar.Tag();

        // EN UDSKRIFT ER OGSAA NOGET, DER SKAL VAERE TIL AT STOLE PAA. Den
        // ender i et moede, i et referat og i et dokument.
        Assert.Null(Arbejdskoe.Svar(opgave.Id));
    }

    // ==================================================================== kravet

    [Fact]
    public void To_maskiner_kan_ikke_tage_den_samme_opgave()
    {
        var delt = Proevemaskine.Nydeltmappe();
        Delt.Klargoer(delt);

        using var baerbar = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);
        using var en = new Proevemaskine("Første", delt);
        using var to = new Proevemaskine("Anden", delt);

        Proevemaskine.Godkend(en, baerbar);
        Proevemaskine.Godkend(to, baerbar);

        baerbar.Tag();

        // Uden modtager: «den, der kan».
        var lyd = Lyd();
        var modtager = Delt.Andre().First(m => m.Navn == "Første");

        var opgave = Arbejdskoe.Laeg(modtager, "moede-4", "mikrofon", lyd, "da", "large-v3", "", 30);

        en.Tag();
        Assert.True(Arbejdskoe.Tag(opgave));

        to.Tag();

        // FILSYSTEMET AFGOER DET, ikke en aftale mellem to programmer.
        Assert.False(Arbejdskoe.Tag(opgave));
    }

    [Fact]
    public void En_opgave_bliver_ledig_igen_naar_maskinen_gik_ned()
    {
        var (baerbar, stationaer, delt) = Toparrede();

        using var a = baerbar;
        using var b = stationaer;

        baerbar.Tag();

        var opgave = Arbejdskoe.Laeg(
            Delt.Andre().Single(), "moede-5", "mikrofon", Lyd(), "da", "large-v3", "", 30);

        stationaer.Tag();
        Assert.True(Arbejdskoe.Tag(opgave));

        // Maskinen gik ned midt i det: kravet ligger, men hjerteslaget er
        // gammelt. Skrives i haanden - proeven skal ikke vente en halv time.
        var gammelt = new Arbejdskrav(
            "en-maskine-der-er-vaek",
            DateTimeOffset.Now - TimeSpan.FromHours(2),
            DateTimeOffset.Now - TimeSpan.FromHours(2));

        File.WriteAllText(Path.Combine(Arbejdskoe.Mappe(delt, opgave.Id), "krav.json"),
            System.Text.Json.JsonSerializer.Serialize(gammelt));

        // ============ EN BRUGER SKAL IKKE VENTE PAA EN SLUKKET MASKINE ============
        Assert.Single(Arbejdskoe.Venter());
        Assert.True(Arbejdskoe.Tag(opgave));

        Assert.Equal(NoteApp.Core.Deling.Maskinid.Id, Arbejdskoe.Taget(delt, opgave.Id)!.Maskine);
    }

    [Fact]
    public void Gamle_opgaver_ryddes()
    {
        var (baerbar, stationaer, delt) = Toparrede();

        using var a = baerbar;
        using var b = stationaer;

        baerbar.Tag();

        var opgave = Arbejdskoe.Laeg(
            Delt.Andre().Single(), "moede-6", "mikrofon", Lyd(), "da", "large-v3", "", 30);

        // Skrevet tilbage i tiden. En postkasse, der aldrig toemmes, er et
        // lager - og lyd fylder.
        var fil = Path.Combine(Arbejdskoe.Mappe(delt, opgave.Id), "opgave.json");
        var gammel = opgave with { Oprettet = DateTimeOffset.Now - TimeSpan.FromDays(5) };

        File.WriteAllText(fil, System.Text.Json.JsonSerializer.Serialize(gammel));

        Assert.Equal(1, Arbejdskoe.Ryd_gamle());
        Assert.False(Directory.Exists(Arbejdskoe.Mappe(delt, opgave.Id)));
    }
}
