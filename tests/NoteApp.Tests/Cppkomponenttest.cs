﻿using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Microsofts C++-komponent — den, Whisper er bygget med.
/// </summary>
/// <remarks>
/// DET ER DEN FEJL, DER IKKE SIGER SIG SELV. Manglede den, kom Windows' egen
/// kasse med «VCOMP140.DLL blev ikke fundet», og appen stod og skrev «Skriver
/// teksten ud …» i det uendelige. Set på en frisk Windows 07-09-2026.
///
/// Prøverne peger systemmappen et andet sted hen. Ellers ville de måle på den
/// maskine, de tilfældigvis kører på — og så ville de sige god for koden på en
/// maskine, hvor komponenten er der i forvejen.
/// </remarks>
public class Cppkomponenttest
{
    private static string Tommappe()
    {
        var sti = Path.Combine(Path.GetTempPath(), "heypia-cpp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sti);

        return sti;
    }

    [Fact]
    public void Alt_mangler_naar_der_ikke_ligger_noget()
    {
        var tom = Tommappe();

        var mangler = Cppkomponent.Mangler(vedSidenAf: tom, systemmappe: tom);

        Assert.Equal(Cppkomponent.Kraevede.Count, mangler.Count);
        Assert.Contains("vcomp140.dll", mangler);
        Assert.False(Cppkomponent.Klar(tom, tom));
    }

    [Fact]
    public void En_kopi_ved_siden_af_motoren_taeller()
    {
        var motor = Tommappe();
        var system = Tommappe();

        foreach (var fil in Cppkomponent.Kraevede)
            File.WriteAllText(Path.Combine(motor, fil), "");

        // WINDOWS LEDER I PROGRAMMETS EGEN MAPPE FOERST. En kopi lagt dér er
        // lige saa god som den, Microsofts pakke laegger i systemmappen.
        Assert.True(Cppkomponent.Klar(motor, system));
        Assert.Empty(Cppkomponent.Besked(motor, system));
    }

    [Fact]
    public void Beskeden_siger_hvad_man_goer_ved_det()
    {
        var tom = Tommappe();

        var besked = Cppkomponent.Besked(tom, tom);

        // DER SKAL STAA, HVAD MAN GOER - ikke bare hvad der mangler. Et
        // filnavn er sandt og ubrugeligt.
        Assert.Contains(Cppkomponent.Hentesti, besked);
        Assert.Contains(Cppkomponent.Navn, besked);
        Assert.Contains("vcomp140.dll", besked);
        Assert.DoesNotContain("Exception", besked);
    }

    [Fact]
    public void Kravlisten_naevner_den()
    {
        // Den hoerer paa «Krav til maskinen»: det er et krav til maskinen og
        // ikke en indstilling.
        var krav = Maskinkrav.Alle();

        Assert.Contains(krav, k => k.Hvad.Contains("C++"));
    }
}
