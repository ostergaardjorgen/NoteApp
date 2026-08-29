using System;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af regnestykket bag forbrugsmålingen.
///
/// HVORFOR DET SKAL PRØVES AF: et tal om ressourcer er ikke pynt. Det er dét,
/// man bruger til at afgøre, om appen er skyld i, at maskinen er langsom.
/// Er tallet forkert, leder man det forkerte sted i timevis — eller man
/// frikender appen, der faktisk stod og åd maskinen.
///
/// Målt 29-08-2026: tre efterladte hjælpeprogrammer tog 2,4 GB og 94 % af
/// grafikkortet, mens den funktion, der havde startet dem, var slået fra.
/// </summary>
public class RessourcerTest
{
    private static TimeSpan S(double sekunder) => TimeSpan.FromSeconds(sekunder);

    [Fact]
    public void En_kerne_fuldt_i_brug_er_hundrede_procent()
    {
        // Ét sekunds processortid paa ét sekunds vaegur.
        Assert.Equal(100, Ressourcer.Procent(S(10), S(11), S(1))!.Value, 3);
    }

    [Fact]
    public void To_kerner_giver_over_hundrede()
    {
        // DET ER IKKE EN FEJL, og det staar ogsaa paa skaermen. To sekunders
        // processortid paa ét sekund betyder to kerner i brug.
        Assert.Equal(200, Ressourcer.Procent(S(10), S(12), S(1))!.Value, 3);
    }

    [Fact]
    public void En_proces_der_intet_laver_er_nul()
    {
        Assert.Equal(0, Ressourcer.Procent(S(42), S(42), S(2))!.Value, 3);
    }

    [Fact]
    public void Uden_tid_regnes_der_ikke()
    {
        // Der divideres ikke med nul for at kunne skrive et tal. Null er det
        // aerlige svar - og skaermen viser saa ingenting frem for noget forkert.
        Assert.Null(Ressourcer.Procent(S(1), S(2), TimeSpan.Zero));
        Assert.Null(Ressourcer.Procent(S(1), S(2), S(-1)));
    }

    [Fact]
    public void En_udskiftet_proces_giver_ikke_et_negativt_tal()
    {
        // Doer processen og faar en ny det samme pid, kan den anden aflaesning
        // vaere MINDRE end den foerste. Et negativt forbrug er ikke et tal,
        // nogen kan bruge til noget.
        Assert.Null(Ressourcer.Procent(S(50), S(2), S(1)));
    }

    [Fact]
    public void Hjaelpeprogrammerne_er_dem_appen_selv_starter()
    {
        // Listen afgoer, hvad der overhovedet bliver maalt paa. Mangler et
        // navn, er forbruget usynligt - og det var praecis problemet.
        Assert.Contains("whisper-command", Ressourcer.Hjaelpere);
        Assert.Contains("whisper-cli", Ressourcer.Hjaelpere);
    }
}
