using System;
using System.IO;
using System.Text;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af de dikteringer, brugeren vælger at gemme.
///
/// DE GEMMES KUN, NÅR MAN SIGER JA. En diktering er som regel på vej et andet
/// sted hen og er allerede landet dér. Gemte appen hver eneste, ville den lave
/// et arkiv af alt, hvad man havde sagt hele dagen — og det ville ingen have
/// bedt om.
/// </summary>
public class DiktatnoterTest : IDisposable
{
    private readonly string _mappe;
    private readonly string? _foer;

    public DiktatnoterTest()
    {
        _mappe = Path.Combine(Path.GetTempPath(), "heypia-noter-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_mappe);

        _foer = Environment.GetEnvironmentVariable(UserDataPaths.OverrideVariable);
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, _mappe);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, _foer);
        try { Directory.Delete(_mappe, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Ingen_fil_er_ingen_noter()
    {
        Assert.Empty(Diktatnoter.Laes());
    }

    [Fact]
    public void Den_nyeste_staar_oeverst()
    {
        // Man leder efter den, man lige har sagt - ikke efter den foerste
        // nogensinde.
        Diktatnoter.Tilfoej("den første");
        Diktatnoter.Tilfoej("den anden");
        Diktatnoter.Tilfoej("den tredje");

        var alle = Diktatnoter.Laes();

        Assert.Equal(3, alle.Count);
        Assert.Equal("den tredje", alle[0].Tekst);
        Assert.Equal("den første", alle[2].Tekst);
    }

    [Fact]
    public void Tom_tekst_gemmes_ikke()
    {
        Diktatnoter.Tilfoej("");
        Diktatnoter.Tilfoej("   ");
        Diktatnoter.Tilfoej(null!);

        Assert.Empty(Diktatnoter.Laes());
    }

    [Fact]
    public void En_note_kan_slettes()
    {
        Diktatnoter.Tilfoej("bliver");
        Diktatnoter.Tilfoej("ryger ud");

        var ud = Diktatnoter.Laes()[0];
        Diktatnoter.Slet(ud);

        var tilbage = Diktatnoter.Laes();
        Assert.Single(tilbage);
        Assert.Equal("bliver", tilbage[0].Tekst);
    }

    [Fact]
    public void En_oedelagt_linje_koster_ikke_resten()
    {
        // Derfor EEN linje pr. note: en afbrudt skrivning rammer den sidste
        // linje, ikke hele arkivet.
        Diktatnoter.Tilfoej("den gode");

        File.AppendAllText(Diktatnoter.Sti, "{ dette er ikke json" + Environment.NewLine,
                           Encoding.UTF8);

        var alle = Diktatnoter.Laes();
        Assert.Single(alle);
        Assert.Equal("den gode", alle[0].Tekst);
    }

    [Fact]
    public void De_aeldste_falder_ud()
    {
        // Ellers bliver listen til et arkiv, ingen har besluttet at have.
        for (var i = 0; i < Diktatnoter.Maks + 12; i++) Diktatnoter.Tilfoej($"note {i}");

        var alle = Diktatnoter.Laes();

        Assert.Equal(Diktatnoter.Maks, alle.Count);
        Assert.Equal($"note {Diktatnoter.Maks + 11}", alle[0].Tekst);
    }

    [Fact]
    public void Overskriften_er_de_foerste_ord_paa_een_linje()
    {
        // Listen skal kunne skimmes. Linjeskift og dobbelte mellemrum ville
        // goere hver post lige saa hoej som teksten selv.
        Diktatnoter.Tilfoej("Første linje\nAnden  linje");

        var n = Diktatnoter.Laes()[0];

        Assert.Equal("Første linje Anden linje", n.Overskrift);
        Assert.DoesNotContain("\n", n.Overskrift);
    }

    [Fact]
    public void En_lang_note_forkortes_i_overskriften()
    {
        Diktatnoter.Tilfoej(new string('a', 200));

        var n = Diktatnoter.Laes()[0];

        Assert.True(n.Overskrift.Length < 80);
        Assert.EndsWith("…", n.Overskrift);
        Assert.Equal(200, n.Tekst.Length);   // selve teksten er hel
    }
}
