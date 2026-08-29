using System.Collections.Generic;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, hvilken model vågeordet lytter med.
///
/// DEN HER PRØVE FINDES, FORDI FEJLEN VAR USYNLIG. Vågeordet hentede
/// mødemodellen — den, der er valgt til at være den BEDSTE, der findes. På
/// udviklingsmaskinen er det knap 3 GB, og så lå den og lyttede efter to ord
/// hele tiden. Intet gik i stykker; det blev bare dyrt, og tallet på skærmen
/// om, hvad lytningen koster, ville have været forkert.
///
/// Reglen er omvendt af mødets: her vinder den MINDSTE.
/// </summary>
public class VaageordsmodelTest
{
    private const long MB = 1_048_576L;

    private static List<(string Sti, long Bytes)> Filer(params (string, long)[] f)
    {
        var liste = new List<(string, long)>();
        foreach (var (navn, mb) in f) liste.Add((@"C:\modeller\" + navn, mb * MB));
        return liste;
    }

    [Fact]
    public void Den_mindste_vinder()
    {
        var valgt = WhisperInstall.MindsteModel(Filer(
            ("ggml-large-v3.bin", 2952),
            ("ggml-medium.bin", 1463),
            ("ggml-small.bin", 465)));

        Assert.Equal(@"C:\modeller\ggml-small.bin", valgt);
    }

    [Fact]
    public void Stilhedsmodellen_taeller_ikke_med()
    {
        // Silero afgoer, OM der bliver talt. Den kan ikke genkende et ord -
        // vaelges den, hoerer appen aldrig noget. Den er ogsaa langt den
        // mindste fil, saa uden reglen ville den vinde hver gang.
        var valgt = WhisperInstall.MindsteModel(Filer(
            (WhisperInstall.VadFilnavn, 1),
            ("ggml-small.bin", 465)));

        Assert.Equal(@"C:\modeller\ggml-small.bin", valgt);
    }

    [Fact]
    public void En_halv_fil_vinder_ikke()
    {
        // En afbrudt hentning er ikke en model. Den ville vinde hver gang,
        // netop fordi den er lille - og saa lytter appen med en stump.
        var valgt = WhisperInstall.MindsteModel(Filer(
            ("ggml-small.bin", 12),
            ("ggml-large-v3.bin", 2952)));

        Assert.Equal(@"C:\modeller\ggml-large-v3.bin", valgt);
    }

    [Fact]
    public void Ingen_modeller_giver_ingenting()
    {
        // Der skal svares «ingen» og ikke gaettes. Skaermen siger saa, at der
        // ikke er noget at lytte med, i stedet for «lytter» uden at goere det.
        Assert.Null(WhisperInstall.MindsteModel(Filer()));
        Assert.Null(WhisperInstall.MindsteModel(Filer((WhisperInstall.VadFilnavn, 1))));
    }

    [Fact]
    public void To_lige_store_giver_altid_det_samme_svar()
    {
        // Uden en fast anden noegle kunne svaret skifte med mappens
        // raekkefoelge - og saa lyttede appen med sin egen model i dag og en
        // anden i morgen, uden at nogen havde aendret noget.
        var a = WhisperInstall.MindsteModel(Filer(("ggml-b.bin", 465), ("ggml-a.bin", 465)));
        var b = WhisperInstall.MindsteModel(Filer(("ggml-a.bin", 465), ("ggml-b.bin", 465)));

        Assert.Equal(a, b);
        Assert.Equal(@"C:\modeller\ggml-a.bin", a);
    }
}
