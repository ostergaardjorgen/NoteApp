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

    /// <summary>
    /// Saa meget ro skal der til, foer motoren mener, man er holdt op.
    /// </summary>
    /// <remarks>
    /// Vinduet regnes BAGUD fra det oejeblik, motoren bedoemmer. De sidste
    /// 1000 ms af det er altsaa selve stilheden, der udloeste bedoemmelsen -
    /// ikke ord.
    /// </remarks>
    private const int StilhedenMs = 1000;

    [Fact]
    public void Vinduet_er_kortere_end_motorens_standard()
    {
        const int standard = 8000;

        Assert.True(Vaageord.Kommandovindue < standard,
            "Vinduet er tilbage paa motorens standard. Saa bedoemmes syv "
            + "sekunders stilhed mod listen, og lokkeordene vinder.");
    }

    /// <summary>
    /// Vinduet skal rumme stilheden OG hele ordet.
    /// </summary>
    /// <remarks>
    /// Med 1500 ms var der 500 ms tilbage til ordet, naar stilhedens 1000 var
    /// trukket fra. «Hej Pia» tager omkring 900 ms. Ordet blev klippet midt
    /// over, og modellen skulle genkende to ord ud fra den sidste halvdel af
    /// det ene.
    ///
    /// Maalt 31-08-2026: brugerens «Hej Pia» blev bedoemt til 0,109 og 0,141,
    /// mens de gange det ramte laa paa 0,287 og opefter.
    /// </remarks>
    [Fact]
    public void Vinduet_rummer_baade_stilheden_og_ordet()
    {
        Assert.True(Vaageord.Kommandovindue >= StilhedenMs + VaageordetsLaengdeMs,
            $"Vinduet er {Vaageord.Kommandovindue} ms. Traekkes stilhedens "
            + $"{StilhedenMs} ms fra, er der kun "
            + $"{Vaageord.Kommandovindue - StilhedenMs} ms tilbage til et ord, "
            + $"der tager {VaageordetsLaengdeMs} ms at sige.");
    }

    /// <summary>
    /// Men det maa ikke blive saa stort, at stilheden overdoever ordet.
    /// </summary>
    /// <remarks>
    /// Ved motorens standard paa 8000 skulle to ord forklare syv sekunders
    /// stilhed, og saa vandt det mest almindelige udtryk paa listen hver gang.
    ///
    /// Vinduet koster ingen ventetid - det er lyd, der ligger BAG os - men
    /// hvert millisekund stilhed mere er stoej i bedoemmelsen.
    /// </remarks>
    [Fact]
    public void Ordet_fylder_mindst_en_tredjedel_af_vinduet()
    {
        var tilOrd = Vaageord.Kommandovindue - StilhedenMs;

        Assert.True(tilOrd >= Vaageord.Kommandovindue / 3.0,
            $"Kun {tilOrd} ms af vinduets {Vaageord.Kommandovindue} er ord.");
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
    /// Motoren skal faa lov at kigge tit nok.
    /// </summary>
    /// <remarks>
    /// Motoren bedoemmer foerst, naar den mener, man er holdt op med at tale.
    /// Ved 0,60 skete det kun hvert tiende til femtende sekund - maalt
    /// 31-08-2026 kl. 16:12-16:14, mens brugeren sagde «Hej Pia» gang paa
    /// gang: 13 bedoemmelser paa to minutter.
    ///
    /// Vaageordet skal ramme praecis det ene vindue paa halvandet sekund,
    /// motoren tilfaeldigvis kigger paa. Gjorde det ikke det, blev det aldrig
    /// hoert.
    ///
    /// FALSKE UDSLAG HOLDES UDE AF GRAENSEN og ikke af, hvor sjaeldent der
    /// bedoemmes - se Graensen_har_luft_til_stoej_i_rummet.
    /// </remarks>
    [Fact]
    public void Pausetaersklen_lader_motoren_kigge_tit()
    {
        Assert.True(Vaageord.Pausetaerskel > 0.60,
            "Ved 0,60 bedoemte motoren kun 13 gange paa to minutter, mens "
            + "brugeren sagde «Hej Pia» gang paa gang.");
    }

    [Fact]
    public void Pausetaersklen_er_stadig_en_taerskel()
    {
        // 1,0 ville betyde, at ALT regnes som en pause, ogsaa midt i en
        // saetning - og saa bedoemmes der paa halve ord.
        Assert.True(Vaageord.Pausetaerskel < 1.0);
    }
}
