using NoteApp.Core;
using NoteApp.Core.Deling;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Aftaler og opgaver, der rejser mellem to computere.
/// </summary>
/// <remarks>
/// ============ DET, PRØVERNE HER PASSER PÅ ============
///
/// At en slettet aftale IKKE kommer tilbage — det er den fejl, enhver
/// synkronisering laver først, og den, brugeren opdager sidst.
///
/// At det, der kommer fra Google, ikke rejser med. Begge maskiner henter fra
/// det samme sted; en aftale, der kom to veje, ville blive til to.
///
/// Og at en, der kan skrive i mappen, ikke kan lægge en aftale ind i en andens
/// kalender.
/// </remarks>
public class Journaltest
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

    private static Aftale Nyaftale(string titel) => new()
    {
        Titel = titel,
        Start = DateTimeOffset.Now.AddDays(1),
        Slut = DateTimeOffset.Now.AddDays(1).AddHours(1),
    };

    // ================================================================== den enkle vej

    [Fact]
    public void En_aftale_kommer_over_paa_den_anden_computer()
    {
        var (a, b, _) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        var aftale = Nyaftale("Tandlæge");
        Kalender.Gem(aftale);

        b.Tag();

        Assert.Empty(Kalender.Alle());
        Assert.Equal(1, Delingsjournal.Hent());

        var kom = Assert.Single(Kalender.Alle());

        Assert.Equal("Tandlæge", kom.Titel);
        Assert.Equal(aftale.Id, kom.Id);

        // ============ DER LAESES FREM, IKKE FORFRA ============
        //
        // Uden det ville hver koersel laegge hele historikken ind igen - og en
        // aftale, man havde slettet bagefter, ville komme tilbage hver gang.
        Assert.Equal(0, Delingsjournal.Hent());
        Assert.Single(Kalender.Alle());
    }

    [Fact]
    public void En_slettet_aftale_kommer_ikke_tilbage()
    {
        var (a, b, _) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        var aftale = Nyaftale("Møde, der blev aflyst");
        Kalender.Gem(aftale);

        b.Tag();
        Delingsjournal.Hent();
        Assert.Single(Kalender.Alle());

        a.Tag();
        Kalender.Slet(aftale.Id);

        b.Tag();

        // GRAVSTENEN. Uden den ville aftalen blive staaende her for evigt -
        // og blive skrevet tilbage, naeste gang der skete noget.
        Assert.Equal(1, Delingsjournal.Hent());
        Assert.Empty(Kalender.Alle());
    }

    [Fact]
    public void En_opgave_rejser_ogsaa()
    {
        var (a, b, _) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();

        var opgave = new Opgave { Navn = "Ring til Paludan", Tekst = "Ring til Paludan om tilbuddet" };
        Opgavelager.Gem(opgave);

        b.Tag();

        Assert.Equal(1, Delingsjournal.Hent());

        var kom = Assert.Single(Opgavelager.Alle());

        Assert.Equal("Ring til Paludan", kom.Navn);
    }

    // ================================================================== det, der ikke rejser

    [Fact]
    public void Google_aftaler_rejser_ikke()
    {
        var (a, b, _) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();

        var fraGoogle = Nyaftale("Fra Google") with { };
        fraGoogle.Kilde = Kalenderkilde.Google;
        fraGoogle.FremmedId = "abc123";

        Kalender.Gem(fraGoogle);

        b.Tag();

        // BEGGE MASKINER HENTER FRA DET SAMME STED. En aftale, der ogsaa kom
        // den her vej, ville blive til to - eller til en, der blev skrevet
        // frem og tilbage, hver gang den ene hentede.
        Assert.Equal(0, Delingsjournal.Hent());
        Assert.Empty(Kalender.Alle());
    }

    [Fact]
    public void En_fremmed_kan_ikke_skrive_i_kalenderen()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        using var fremmed = new Proevemaskine("Fremmed", delt);

        fremmed.Tag();
        Delt.Meld();

        b.Tag();
        var bId = Maskinid.Id;

        // Den fremmede skriver en linje i journalen fra sig selv til den
        // baerbare. Den kan skrive i mappen - det kan alle, der har adgang.
        fremmed.Tag();

        var mappe = Path.Combine(delt, "journal", Maskinid.Id);
        Directory.CreateDirectory(mappe);

        var post = new Journalpost(
            "aftale", "indsat-udefra", DateTimeOffset.Now, false,
            System.Text.Json.JsonSerializer.Serialize(Nyaftale("Betal her")),
            Maerke: "det-her-er-ikke-et-segl");

        File.WriteAllText(Path.Combine(mappe, bId + ".jsonl"),
            System.Text.Json.JsonSerializer.Serialize(post) + "\n");

        b.Tag();

        // ============ INTET KOMMER IND ============
        //
        // Den fremmede er ikke godkendt, og seglet passer ikke. To grunde, og
        // der skal kun een til.
        Assert.Equal(0, Delingsjournal.Hent());
        Assert.Empty(Kalender.Alle());
    }

    // ==================================================================== konflikten

    [Fact]
    public void Den_nyeste_rettelse_vinder()
    {
        var (a, b, _) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        var aftale = Nyaftale("Første udgave");
        Kalender.Gem(aftale);

        b.Tag();
        Delingsjournal.Hent();

        // Begge retter den samme aftale, foer de har talt sammen. Den
        // baerbare retter SIDST.
        a.Tag();
        var minA = Kalender.Alle().Single();
        minA.Titel = "Rettet på den stationære";
        Kalender.Gem(minA);

        Thread.Sleep(20);

        b.Tag();
        var minB = Kalender.Alle().Single();
        minB.Titel = "Rettet på den bærbare";
        Kalender.Gem(minB);

        // Den stationaeres rettelse kommer ind bagefter - men den er AELDRE.
        Delingsjournal.Hent();

        Assert.Equal("Rettet på den bærbare", Kalender.Alle().Single().Titel);

        // Og den anden vej: den baerbares rettelse er nyere og vinder ogsaa
        // derovre.
        a.Tag();
        Delingsjournal.Hent();

        Assert.Equal("Rettet på den bærbare", Kalender.Alle().Single().Titel);
    }

    [Fact]
    public void En_aendring_fra_den_anden_skrives_ikke_tilbage()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Kalender.Gem(Nyaftale("Frokost"));

        b.Tag();
        Delingsjournal.Hent();

        // ============ INGEN EKKO ============
        //
        // Den baerbare gemte lige en aftale - men den kom fra den anden
        // maskine. Skrev den den tilbage i sin egen journal, ville de to kaste
        // den samme aftale frem og tilbage, saa laenge de begge var taendt.
        var min = Path.Combine(delt, "journal", Maskinid.Id);

        Assert.False(Directory.Exists(min) && Directory.EnumerateFiles(min).Any());
    }
}
