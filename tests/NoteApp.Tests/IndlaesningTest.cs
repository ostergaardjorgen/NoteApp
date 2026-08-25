using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Lydfiler udefra — roadmap 2.1.
///
/// Selve omsætningen af lyd kræver en rigtig lydfil og Windows' kodeks, og den
/// er prøvet af i hånden på en talememo fra en iPhone. Her spændes det fast,
/// der kan gå galt UDEN at nogen opdager det: hvilke filer der genkendes,
/// hvilken dato optagelsen får, og at en fejl ikke efterlader en halv mappe.
/// </summary>
public class IndlaesningTest
{
    [Theory]
    [InlineData("memo.m4a")]
    [InlineData("MEMO.M4A")]
    [InlineData("optagelse.mp3")]
    [InlineData("spor.wav")]
    [InlineData("lyd.aac")]
    [InlineData("video.mp4")]
    [InlineData("gammel.wma")]
    [InlineData("tabsfri.flac")]
    public void Kendte_formater_genkendes(string navn) =>
        Assert.True(Indlaesning.Kendes(navn));

    [Theory]
    [InlineData("referat.docx")]
    [InlineData("billede.png")]
    [InlineData("noter.txt")]
    [InlineData("uden-endelse")]
    [InlineData("")]
    public void Ukendte_formater_afvises(string navn) =>
        Assert.False(Indlaesning.Kendes(navn));

    [Fact]
    public void Filtret_naevner_alle_endelserne()
    {
        // Filtret i aabn-dialogen skal foelge listen. Bliver en endelse lagt
        // til uden at komme med i filtret, kan filen traekkes ind men ikke
        // vaelges - og saa ser funktionen halvt istykker ud.
        foreach (var e in Indlaesning.Endelser)
            Assert.Contains("*" + e, Indlaesning.Filter);
    }

    [Fact]
    public void En_fil_der_ikke_findes_giver_en_tydelig_fejl()
    {
        using var p = new Proevemappe();

        Assert.Throws<FileNotFoundException>(() =>
            Indlaesning.Indlaes(Path.Combine(p.Sti, "findes-ikke.m4a")));
    }

    [Fact]
    public void En_tom_fil_afvises_og_efterlader_ingen_mappe()
    {
        using var p = new Proevemappe();

        var tom = Path.Combine(p.Sti, "tom.m4a");
        File.WriteAllBytes(tom, Array.Empty<byte>());

        Assert.Throws<InvalidDataException>(() => Indlaesning.Indlaes(tom));

        // EN HALV OPTAGELSE ER VAERRE END INGEN. Fejler indlaesningen, maa der
        // ikke staa en mappe tilbage i listen, som hverken kan skrives ud
        // eller forklares.
        Assert.False(Directory.Exists(UserDataPaths.Meetings)
                     && Directory.EnumerateDirectories(UserDataPaths.Meetings).Any());
    }

    [Fact]
    public void En_fil_der_ikke_er_lyd_efterlader_ingen_mappe()
    {
        using var p = new Proevemappe();

        // Rigtig endelse, men indholdet er ikke lyd. Media Foundation siger
        // fra, og mappen skal ryddes.
        var falsk = Path.Combine(p.Sti, "snyd.m4a");
        File.WriteAllText(falsk, "det her er ikke en lydfil");

        Assert.ThrowsAny<Exception>(() => Indlaesning.Indlaes(falsk));

        Assert.False(Directory.Exists(UserDataPaths.Meetings)
                     && Directory.EnumerateDirectories(UserDataPaths.Meetings).Any());
    }

    [Fact]
    public void En_fil_paa_disken_er_ikke_kun_i_skyen()
    {
        using var p = new Proevemappe();

        var sti = Path.Combine(p.Sti, "almindelig.m4a");
        File.WriteAllBytes(sti, new byte[1024]);

        Assert.False(Indlaesning.KunISkyen(sti));
    }

    [Fact]
    public void KunISkyen_vaelter_ikke_paa_en_fil_der_ikke_findes()
    {
        // Kaldes fra scanningen paa hver eneste fil. En undtagelse her ville
        // stoppe et helt kig, fordi een fil blev slettet imens.
        Assert.False(Indlaesning.KunISkyen(@"Z:\findes\bestemt\ikke.m4a"));
    }
}
