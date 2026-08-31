using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Vinduet, motoren bedoemmer paa.
/// </summary>
/// <remarks>
/// TALLET KOSTEDE BAADE HASTIGHEDEN OG TRAEFSIKKERHEDEN.
///
/// whisper-command optager «-cms» millisekunder, foer den bedoemmer mod
/// listen. Standarden er 8000, og den stod uroert.
///
/// Maalt 31-08-2026: der gik ca. syv sekunder fra «Hej Pia» blev sagt, til
/// der skete noget - og hver eneste bedoemmelse landede paa lokkeordet «vi
/// ses i morgen» med 0,15-0,24. «hej pia» optraadte ikke ÉN gang. To ord
/// skulle forklare syv sekunders stilhed, og saa vandt den mest almindelige
/// saetning paa listen.
///
/// Proeven staar her, saa tallet ikke kan skride tilbage mod standarden uden
/// at nogen ser hvorfor.
/// </remarks>
public class KommandovindueTest
{
    /// <summary>Saa lang tid tager det at sige «Hej Pia», rundt regnet.</summary>
    private const int VaageordetsLaengdeMs = 900;

    [Fact]
    public void Vinduet_er_kortere_end_motorens_standard()
    {
        const int standard = 8000;

        Assert.True(Vaageord.Kommandovindue < standard,
            "Vinduet er tilbage paa motorens standard. Saa bedoemmes syv "
            + "sekunders stilhed mod listen, og lokkeordene vinder.");
    }

    [Fact]
    public void Vinduet_er_langt_nok_til_at_rumme_vaageordet()
    {
        // Et vindue, der er kortere end det, der skal siges, klipper ordet
        // midt over - og saa er der intet at genkende.
        Assert.True(Vaageord.Kommandovindue >= VaageordetsLaengdeMs,
            $"Vinduet er {Vaageord.Kommandovindue} ms, og "
            + $"«Hej Pia» tager omkring {VaageordetsLaengdeMs} ms at sige.");
    }

    [Fact]
    public void Ventetiden_bliver_under_to_sekunder()
    {
        // Det er dét, hele aendringen handler om: syv sekunder er ubrugeligt,
        // og under to er til at leve med.
        Assert.True(Vaageord.Kommandovindue <= 2000);
    }
}
