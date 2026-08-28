using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af listen over genvejstaster.
///
/// Listen er data, og rækkefølgen ER valget: den første, der er ledig, bliver
/// standarden. En dublet eller en forkert rækkefølge ville derfor ændre,
/// hvilken tast alle nye brugere får — uden at nogen ændrede en indstilling.
///
/// Selve registreringen kan ikke prøves her. Den kræver et vindue og en
/// beskedkø, og resultatet afhænger af, hvad der ellers kører på maskinen.
/// Den er efterprøvet i hånden 28-08-2026: hver kandidat blev registreret,
/// tastetrykket sendt, og det blev efterset, at WM_HOTKEY kom frem.
/// </summary>
public sealed class GenvejTest
{
    [Fact]
    public void Ctrl_space_er_standarden()
    {
        // Foerst paa listen = standard. Staar der noget andet, har nogen
        // flyttet rundt uden at vide, at raekkefoelgen er valget.
        Assert.Equal("ctrl-space", Genvejstaster.Muligheder[0].Id);
    }

    [Fact]
    public void Ingen_id_gaar_igen()
    {
        // HotkeyId gemmes i indstillingerne. To poster med samme id ville
        // betyde, at brugerens valg pegede paa to ting.
        var ider = Genvejstaster.Muligheder.Select(m => m.Id).ToList();
        Assert.Equal(ider.Count, ider.Distinct().Count());
    }

    [Fact]
    public void Ingen_kombination_gaar_igen()
    {
        // To poster med samme taster ville staa som to valg, der goer det
        // samme - og den ene ville aldrig kunne vaelges.
        var taster = Genvejstaster.Muligheder.Select(m => (m.Modifiers, m.Key)).ToList();
        Assert.Equal(taster.Count, taster.Distinct().Count());
    }

    [Fact]
    public void Alle_har_navn_og_begrundelse()
    {
        // Begge staar paa skaermen under Indstillinger. En tom begrundelse er
        // en raekke, brugeren ikke kan vaelge ud fra.
        foreach (var m in Genvejstaster.Muligheder)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Navn), $"{m.Id} mangler navn");
            Assert.False(string.IsNullOrWhiteSpace(m.Hvorfor), $"{m.Id} mangler begrundelse");
        }
    }

    [Fact]
    public void Alle_kraever_mindst_to_taster()
    {
        // En genvej paa én tast ville gaa i gang, hver gang man skrev.
        // Modifikatorerne er Alt=1, Ctrl=2, Shift=4.
        foreach (var m in Genvejstaster.Muligheder)
            Assert.True(m.Modifiers != 0, $"{m.Id} har ingen modifikatortast");
    }

    [Fact]
    public void Den_der_fraraades_staar_sidst()
    {
        // Ctrl+Shift+pil oedelaegger ordmarkering i hele Windows. Den er med,
        // fordi valget er brugerens - men den maa aldrig kunne blive
        // standarden, og derfor skal den ligge nederst.
        var sidste = Genvejstaster.Muligheder[^1];
        Assert.Equal("ctrl-shift-hoejre", sidste.Id);
        Assert.Contains("FRARÅDES", sidste.Hvorfor);
    }

    [Fact]
    public void Prisen_ved_ctrl_space_staar_i_teksten()
    {
        // Den vinder over Word, Excel og enhver kodeeditor. Det er et rimeligt
        // valg at traeffe - men ikke et, man skal opdage bagefter.
        var s = Genvejstaster.Muligheder[0].Hvorfor;

        Assert.Contains("Word", s);
        Assert.Contains("Excel", s);
    }
}
