using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Flere valg pr. afgrænsning.
/// </summary>
/// <remarks>
/// VALGENE INDEN FOR ÉT FELT ER ELLER; MELLEM FELTER ER DET OG. «Møder eller
/// Webinarer» OG «på dansk». Det er den måde, man selv tænker et filter — og
/// den anden vej rundt ville et ekstra hak gøre listen kortere i stedet for
/// længere.
/// </remarks>
public class Flervalgsfiltertest
{
    private static void Moede(string titel, string mappe, string type, string udskrift)
    {
        var m = MeetingStore.CreateSessionDirectory(titel, DateTimeOffset.Now);

        MeetingStore.Save(m, new MeetingMetadata
        {
            StartedAt = DateTimeOffset.Now,
            Title = titel,
            Mappe = mappe,
            Moedetype = type,
        });

        File.WriteAllText(Path.Combine(m, "transskription.txt"), udskrift);
    }

    private static void Tre()
    {
        Moede("Et", "Møder", "Mødereferat", "Vi talte om adgangsstyring.");
        Moede("To", "Webinarer", "Webinar", "Der blev sagt adgangsstyring.");
        Moede("Tre", "Arkiv", "Dokumentation", "Igen adgangsstyring.");
    }

    [Fact]
    public void Et_tomt_filter_betyder_alt()
    {
        using var p = new Proevemappe();
        Tre();

        Assert.Equal(3, Soegning.Soeg("adgangsstyring", new Soegefilter()).Count);

        // En TOM liste er det samme som ingen liste. Ellers ville en skaerm
        // uden hak give nul svar, og det er ikke, hvad «alle mapper» betyder.
        Assert.Equal(3, Soegning.Soeg("adgangsstyring",
            new Soegefilter(Mappe: Array.Empty<string>())).Count);
    }

    [Fact]
    public void To_valg_i_samme_felt_er_ELLER()
    {
        using var p = new Proevemappe();
        Tre();

        var fund = Soegning.Soeg("adgangsstyring",
            new Soegefilter(Mappe: new[] { "Møder", "Webinarer" }));

        Assert.Equal(2, fund.Count);
        Assert.Contains(fund, f => f.Overskrift == "Et");
        Assert.Contains(fund, f => f.Overskrift == "To");
    }

    [Fact]
    public void To_felter_er_OG()
    {
        using var p = new Proevemappe();
        Tre();

        // «Moeder eller Webinarer» OG «af typen Webinar» giver kun den ene.
        var fund = Soegning.Soeg("adgangsstyring",
            new Soegefilter(Mappe: new[] { "Møder", "Webinarer" },
                            Moedetype: new[] { "Webinar" }));

        Assert.Equal("To", Assert.Single(fund).Overskrift);
    }

    // ============ TYPER: HVAD OPTAGELSEN VAR ============

    private static void Optagelse(string titel, MeetingType type, bool opkald, string udskrift, string? note = null)
    {
        var m = MeetingStore.CreateSessionDirectory(titel, DateTimeOffset.Now);

        MeetingStore.Save(m, new MeetingMetadata
        {
            StartedAt = DateTimeOffset.Now, Title = titel, Type = type, Opkald = opkald,
        });

        File.WriteAllText(Path.Combine(m, "transskription.txt"), udskrift);

        if (note is not null)
            File.WriteAllText(Path.Combine(m, "notes.jsonl"), note);
    }

    private static void Fire()
    {
        Optagelse("Moedet", MeetingType.Online, false, "Om adgangsstyring.");
        Optagelse("Webinaret", MeetingType.Webinar, false, "Om adgangsstyring.");
        Optagelse("Opkaldet", MeetingType.Online, true, "Om adgangsstyring.",
                  "{\"Tekst\":\"adgangsstyring noteret\"}");
    }

    [Theory]
    [InlineData(Soegetype.Moede, "Moedet")]
    [InlineData(Soegetype.Webinar, "Webinaret")]
    [InlineData(Soegetype.Opkald, "Opkaldet")]
    public void En_type_giver_kun_den_slags_optagelse(Soegetype type, string titel)
    {
        using var p = new Proevemappe();
        Fire();

        var fund = Soegning.Soeg("adgangsstyring", new Soegefilter(Typer: new[] { type }));

        Assert.All(fund, f => Assert.Equal(titel, f.Overskrift));
        Assert.Contains(fund, f => f.Slags == Fundtype.Udskrift);
    }

    [Fact]
    public void Noter_giver_kun_noterne()
    {
        using var p = new Proevemappe();
        Fire();

        var fund = Soegning.Soeg("adgangsstyring", new Soegefilter(Typer: new[] { Soegetype.Note }));

        Assert.Equal(Fundtype.Note, Assert.Single(fund).Slags);
    }

    [Fact]
    public void Et_opkald_er_ikke_ogsaa_et_moede()
    {
        // Et opkald optages som et onlinemoede - to spor. Det maa ikke dukke
        // op, naar man beder om moeder.
        using var p = new Proevemappe();
        Fire();

        var fund = Soegning.Soeg("adgangsstyring", new Soegefilter(Typer: new[] { Soegetype.Moede }));

        Assert.DoesNotContain(fund, f => f.Overskrift == "Opkaldet");
        Assert.False(new Soegefilter(Typer: new[] { Soegetype.Moede }).Tomt);
    }

    [Fact]
    public void Et_valg_der_ikke_findes_giver_ingenting()
    {
        using var p = new Proevemappe();
        Tre();

        Assert.Empty(Soegning.Soeg("adgangsstyring",
            new Soegefilter(Mappe: new[] { "Findes ikke" })));
    }

    [Fact]
    public void Filtret_er_tomt_naar_ingen_lister_har_noget_i_sig()
    {
        Assert.True(new Soegefilter().Tomt);
        Assert.True(new Soegefilter(Mappe: Array.Empty<string>()).Tomt);

        Assert.False(new Soegefilter(Mappe: new[] { "Møder" }).Tomt);
        Assert.False(new Soegefilter(Projekt: new[] { "abc" }).Tomt);
    }

    [Fact]
    public void Flere_projekter_kan_vaelges_paa_en_gang()
    {
        using var p = new Proevemappe();

        var a = Projektlager.Opret("Uddannelsen");
        var b = Projektlager.Opret("Kunden");
        var c = Projektlager.Opret("Det tredje");

        foreach (var pr in new[] { a, b, c })
            File.WriteAllText(Path.Combine(pr.Dokumentmappe, "note.md"), "Om adgangsstyring.");

        Assert.Equal(3, Soegning.Soeg("adgangsstyring").Count);

        var fund = Soegning.Soeg("adgangsstyring",
            new Soegefilter(Projekt: new[] { a.Id, b.Id }));

        Assert.Equal(2, fund.Count);
        Assert.DoesNotContain(fund, f => f.Overskrift.Contains("Det tredje"));
    }
}
