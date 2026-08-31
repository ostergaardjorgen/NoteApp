using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Den ene mikrofon, hele appen spoerger om.
/// </summary>
/// <remarks>
/// PROEVERNE HANDLER OM NUMMERET, ikke om at finde enheden. Selve opslaget
/// kraever Windows' lydenheder og kan ikke prøves af her; det, der KAN, er
/// reglen om, hvornaar motorens eget nummer stadig gaelder - og det er
/// praecis dér, mikrofonen blev vaek.
/// </remarks>
public class MikrofonTest
{
    [Fact]
    public void Uden_gemt_nummer_er_der_intet_nummer()
    {
        var v = AppSettings.Current;
        v.VaageordMikrofonNummer = null;
        v.VaageordMikrofonNavn = null;

        Assert.Equal(-1, Mikrofon.Nummer());
    }

    [Fact]
    public void Et_nummer_uden_navn_gaelder_ikke()
    {
        // Navnet er broen mellem Windows' id og motorens taelling. Uden det
        // kan nummeret ikke efterproeves, og et uefterproevet nummer peger
        // maaske paa en helt anden mikrofon.
        var v = AppSettings.Current;
        v.VaageordMikrofonNummer = 1;
        v.VaageordMikrofonNavn = null;

        Assert.Equal(-1, Mikrofon.Nummer());
    }

    [Fact]
    public void Et_nummer_med_et_navn_der_ikke_passer_gaelder_ikke()
    {
        var v = AppSettings.Current;
        v.VaageordMikrofonNummer = 1;
        v.VaageordMikrofonNavn = "En mikrofon, der ikke er valgt";

        // Enten findes der ingen mikrofon i proevemiljoeet, eller ogsaa
        // hedder den noget andet. Begge dele skal give -1.
        Assert.Equal(-1, Mikrofon.Nummer());
    }

    [Fact]
    public void Et_skift_rydder_motorens_nummer()
    {
        var v = AppSettings.Current;
        v.MicrophoneId = "gammel-enhed";
        v.VaageordMikrofonNummer = 1;
        v.VaageordMikrofonNavn = "Gammel mikrofon";
        v.Save();

        Mikrofon.Vaelg("ny-enhed");

        Assert.Equal("ny-enhed", AppSettings.Current.MicrophoneId);
        Assert.Null(AppSettings.Current.VaageordMikrofonNummer);
        Assert.Null(AppSettings.Current.VaageordMikrofonNavn);
    }

    [Fact]
    public void Det_samme_valg_igen_rydder_ingenting()
    {
        // Et valg, der ikke er et skift, maa ikke koste en genstart af
        // lytningen. Rullelisten melder sit valg hver gang, den fyldes.
        var v = AppSettings.Current;
        v.MicrophoneId = "samme-enhed";
        v.VaageordMikrofonNummer = 2;
        v.VaageordMikrofonNavn = "Samme mikrofon";
        v.Save();

        var kaldt = false;
        Mikrofon.Skiftet = () => kaldt = true;
        try { Mikrofon.Vaelg("samme-enhed"); }
        finally { Mikrofon.Skiftet = null; }

        Assert.False(kaldt);
        Assert.Equal(2, AppSettings.Current.VaageordMikrofonNummer);
    }
}
