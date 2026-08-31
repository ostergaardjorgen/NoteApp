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

    /// <summary>
    /// Motoren skal reagere paa de smaa ophold i almindelig tale.
    /// </summary>
    /// <remarks>
    /// whisper-command afgoer foerst noget, naar den mener, du er holdt op
    /// med at tale. Standarden 0,60 kraever et tydeligt ophold - og siger man
    /// «Hej Pia» og taler videre, kommer det ophold aldrig. Saa bedoemmes
    /// slutningen af saetningen i stedet for vaageordet, og der sker
    /// ingenting. Maalt 31-08-2026.
    ///
    /// Et HOEJERE tal betyder, at der skal mindre til, foer det kaldes en
    /// pause.
    /// </remarks>
    /// <summary>
    /// Taersklen maa ikke saettes op igen uden en maaling.
    /// </summary>
    /// <remarks>
    /// 0,8 blev proevet 31-08-2026 for at goere de naturlige mikropauser nok.
    /// Det, der skete, var, at motoren bedoemte konstant: TI bedoemmelser paa
    /// sytten sekunder, mens der ikke blev sagt noget - hver gang med det
    /// samme lokkeord og hver gang med en koeretur paa grafikkortet.
    ///
    /// Ved 0,60 blev der maalt NUL bedoemmelser paa fyrre sekunders stilhed.
    /// </remarks>
    [Fact]
    public void Pausetaersklen_er_ikke_saa_hoej_at_den_bedoemmer_konstant()
    {
        Assert.True(Vaageord.Pausetaerskel <= 0.60,
            "Ved 0,8 bedoemte motoren ti gange paa sytten sekunders stilhed. "
            + "Saettes tallet op igen, skal det maales foerst.");
    }

    [Fact]
    public void Pausetaersklen_er_stadig_en_taerskel()
    {
        // 0 ville betyde, at der ALDRIG blev regnet som en pause, og saa
        // bedoemmes der aldrig.
        Assert.True(Vaageord.Pausetaerskel > 0.0);
    }
}
