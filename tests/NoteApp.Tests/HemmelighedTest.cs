using System.IO;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Hemmeligheder maa ikke ligge som ren tekst i datamappen.
/// </summary>
/// <remarks>
/// Mistral-noeglen og Googles opdateringsnoegle laa i klartekst. Filerne
/// ligger uden for git og uden for OneDrive - det beskytter mod at DELE dem
/// ved et uheld, ikke mod at nogen laeser dem. Enhver proces, der koerer som
/// brugeren, kunne aabne dem; det samme kunne enhver, der fik fat i en
/// sikkerhedskopi af mappen.
///
/// Proeverne koerer ogsaa paa systemer uden DPAPI. Derfor er de skrevet, saa
/// de proever OPFOERSLEN - at det, der blev gemt, kan laeses igen, at en
/// gammel klartekstfil ikke gaar tabt, og at intet af det slipper ud i en
/// besked - og ikke selve krypteringen, som Windows staar for.
/// </remarks>
public class HemmelighedTest
{
    private static string Ny() =>
        Path.Combine(Path.GetTempPath(), "proeve-" + Guid.NewGuid().ToString("N")[..10] + ".txt");

    [Fact]
    public void Det_der_gemmes_kan_laeses_igen()
    {
        var sti = Ny();

        try
        {
            Hemmelighed.Skriv(sti, "min-hemmelige-noegle-1234");

            Assert.Equal("min-hemmelige-noegle-1234", Hemmelighed.Laes(sti));
        }
        finally { File.Delete(sti); }
    }

    /// <summary>
    /// Paa Windows maa noeglen ikke kunne laeses ud af filen med det blotte oeje.
    /// </summary>
    [Fact]
    public void Noeglen_staar_ikke_i_klartekst_i_filen()
    {
        if (!Hemmelighed.Kan) return;

        var sti = Ny();

        try
        {
            Hemmelighed.Skriv(sti, "min-hemmelige-noegle-1234");

            var paaDisken = File.ReadAllText(sti);

            Assert.DoesNotContain("min-hemmelige-noegle", paaDisken);
            Assert.StartsWith(Hemmelighed.Maerke, paaDisken);
        }
        finally { File.Delete(sti); }
    }

    /// <summary>
    /// EN GAMMEL KLARTEKSTFIL SKAL STADIG KUNNE LAESES.
    /// </summary>
    /// <remarks>
    /// Brugeren har sat noeglen ind én gang og skal ikke goere det igen, fordi
    /// lagringen blev bedre. En migrering, der tager adgangen, er vaerre end
    /// ingen migrering.
    /// </remarks>
    [Fact]
    public void En_gammel_klartekstfil_laeses_stadig()
    {
        var sti = Ny();

        try
        {
            File.WriteAllText(sti, "gammel-noegle-uden-beskyttelse");

            Assert.Equal("gammel-noegle-uden-beskyttelse", Hemmelighed.Laes(sti));
        }
        finally { File.Delete(sti); }
    }

    [Fact]
    public void Et_skift_bevarer_noeglen_og_fjerner_klarteksten()
    {
        if (!Hemmelighed.Kan) return;

        var sti = Ny();

        try
        {
            File.WriteAllText(sti, "gammel-noegle-uden-beskyttelse");

            Assert.True(Hemmelighed.Skift(sti));

            // Den skal stadig kunne bruges ...
            Assert.Equal("gammel-noegle-uden-beskyttelse", Hemmelighed.Laes(sti));

            // ... men ikke laeses ud af filen.
            Assert.DoesNotContain("gammel-noegle", File.ReadAllText(sti));
        }
        finally { File.Delete(sti); }
    }

    [Fact]
    public void Et_skift_af_en_allerede_beskyttet_fil_goer_ingenting()
    {
        if (!Hemmelighed.Kan) return;

        var sti = Ny();

        try
        {
            Hemmelighed.Skriv(sti, "noegle");
            var foer = File.ReadAllText(sti);

            Assert.False(Hemmelighed.Skift(sti));
            Assert.Equal(foer, File.ReadAllText(sti));
        }
        finally { File.Delete(sti); }
    }

    [Fact]
    public void En_fil_der_ikke_findes_giver_ingenting()
    {
        Assert.Null(Hemmelighed.Laes(Ny()));
        Assert.False(Hemmelighed.Skift(Ny()));
    }

    [Fact]
    public void En_oedelagt_beskyttet_fil_vaelter_ingenting()
    {
        var sti = Ny();

        try
        {
            File.WriteAllText(sti, Hemmelighed.Maerke + "det-her-er-ikke-base64!!");

            // Null betyder «ingen noegle» - det samme som en tom fil. Appen
            // siger saa det samme, som hvis der aldrig havde vaeret en.
            Assert.Null(Hemmelighed.Laes(sti));
        }
        finally { File.Delete(sti); }
    }

    [Fact]
    public void En_tom_vaerdi_sletter_filen()
    {
        var sti = Ny();

        Hemmelighed.Skriv(sti, "noegle");
        Assert.True(File.Exists(sti));

        Hemmelighed.Skriv(sti, "");
        Assert.False(File.Exists(sti));
    }

    // ============ DET, DER MAA STAA PAA SKAERMEN ============

    /// <summary>
    /// ALDRIG HELE NOEGLEN i en log, en fejl eller paa skaermen.
    /// </summary>
    /// <remarks>
    /// En fejlbesked havner i en historik, et skaermbillede eller en
    /// supportmail, og en noegle, der er set af én, der ikke skulle se den,
    /// skal skiftes.
    /// </remarks>
    [Fact]
    public void En_maskeret_noegle_roeber_ikke_noeglen()
    {
        const string noegle = "sk-hemmelig-noegle-abcd1234";

        var vist = Hemmelighed.Maskeret(noegle);

        Assert.DoesNotContain("hemmelig", vist);
        Assert.True(vist.Length < 12, $"«{vist}» er for lang til at vaere maskeret.");
    }

    [Fact]
    public void En_kort_noegle_roeber_slet_ingenting()
    {
        // Med faa tegn ville selv fire sidste tegn vaere det meste af noeglen.
        Assert.Equal("…", Hemmelighed.Maskeret("kort"));
        Assert.Equal("(ingen)", Hemmelighed.Maskeret(null));
        Assert.Equal("(ingen)", Hemmelighed.Maskeret("   "));
    }

    // ============ ÉT FELT I EN FIL, DER ELLERS IKKE ER HEMMELIG ============

    [Fact]
    public void Et_laast_felt_kan_aabnes_igen()
    {
        var laast = Hemmelighed.Lukfelt("1//opdateringsnoegle-fra-google");

        Assert.Equal("1//opdateringsnoegle-fra-google", Hemmelighed.Aabnfelt(laast));
    }

    [Fact]
    public void Et_felt_i_gammel_klartekst_gaar_uaendret_igennem()
    {
        Assert.Equal("gammel", Hemmelighed.Aabnfelt("gammel"));
    }

    [Fact]
    public void Et_tomt_felt_bliver_tomt()
    {
        Assert.Equal("", Hemmelighed.Lukfelt(null));
        Assert.Equal("", Hemmelighed.Lukfelt(""));
        Assert.Equal("", Hemmelighed.Aabnfelt(null));
    }

    [Fact]
    public void Et_felt_laases_ikke_to_gange()
    {
        if (!Hemmelighed.Kan) return;

        var én = Hemmelighed.Lukfelt("noegle");

        Assert.Equal(én, Hemmelighed.Lukfelt(én));
    }
}
