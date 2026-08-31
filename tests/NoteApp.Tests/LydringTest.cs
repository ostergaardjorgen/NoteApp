using System;
using System.Linq;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af ringen, der holder de sidste sekunders lyd.
///
/// DEN FINDES FOR AT FJERNE VENTETIDEN. Vågeordet høres af én motor, og først
/// NÅR den har meldt det, blev mikrofonen åbnet. Målt 30-08-2026 brugte
/// motoren mellem 60 og 555 millisekunder på at afgøre ordet, og oven i lå
/// åbningen af lydenheden. De første ord efter «Hej Pia» blev klippet.
///
/// Ringen vender det om: lyden er der allerede, og når ordet høres, tages den
/// med tilbage i tiden. Regner den forkert, kommer stavelserne i forkert
/// rækkefølge — og så er udskriften volapyk uden at nogen kan se hvorfor.
/// </summary>
public class LydringTest
{
    private static short[] Tal(int fra, int antal) =>
        Enumerable.Range(fra, antal).Select(i => (short)i).ToArray();

    [Fact]
    public void En_tom_ring_har_ingenting()
    {
        var r = new Lydring(10);

        Assert.Equal(0, r.Antal);
        Assert.Empty(r.Laes());
    }

    [Fact]
    public void Foer_den_er_fuld_kommer_det_hele_ud_i_orden()
    {
        var r = new Lydring(10);
        r.Skriv(Tal(1, 4));

        Assert.Equal(4, r.Antal);
        Assert.Equal(new short[] { 1, 2, 3, 4 }, r.Laes());
    }

    [Fact]
    public void Naar_den_er_fuld_falder_de_aeldste_ud()
    {
        var r = new Lydring(5);
        r.Skriv(Tal(1, 8));

        // De fem nyeste, i den raekkefoelge de blev sagt.
        Assert.Equal(5, r.Antal);
        Assert.Equal(new short[] { 4, 5, 6, 7, 8 }, r.Laes());
    }

    [Fact]
    public void Raekkefoelgen_holder_naar_der_er_koert_rundt_flere_gange()
    {
        // DET ER HER, DET GAAR GALT, HVIS DET GAAR GALT. Kommer stavelserne
        // i forkert raekkefoelge, er udskriften volapyk - og der er ingenting
        // at se paa lyden.
        var r = new Lydring(4);

        r.Skriv(Tal(1, 3));    // 1 2 3
        r.Skriv(Tal(4, 3));    // 4 5 6  -> ringen: 3 4 5 6
        r.Skriv(Tal(7, 2));    // 7 8    -> ringen: 5 6 7 8

        Assert.Equal(new short[] { 5, 6, 7, 8 }, r.Laes());
    }

    [Fact]
    public void En_blok_stoerre_end_ringen_efterlader_kun_halen()
    {
        var r = new Lydring(3);
        r.Skriv(Tal(1, 100));

        Assert.Equal(new short[] { 98, 99, 100 }, r.Laes());
    }

    [Fact]
    public void En_blok_praecis_saa_stor_som_ringen_passer()
    {
        var r = new Lydring(4);
        r.Skriv(Tal(1, 4));

        Assert.Equal(new short[] { 1, 2, 3, 4 }, r.Laes());
    }

    [Fact]
    public void Ryd_toemmer_den()
    {
        var r = new Lydring(4);
        r.Skriv(Tal(1, 4));
        r.Ryd();

        Assert.Equal(0, r.Antal);
        Assert.Empty(r.Laes());
    }

    [Fact]
    public void Ringen_maales_i_sekunder()
    {
        // To sekunder ved 16 kHz er 32.000 proever.
        var r = Lydring.Til(2.0, 16000);
        Assert.Equal(32000, r.Plads);
    }

    [Fact]
    public void En_ring_paa_nul_findes_ikke()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lydring(0));
    }
}
