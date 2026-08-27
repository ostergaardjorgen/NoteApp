using System.IO;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Skriver mødet ud, MENS det kører.
///
/// HVAD DEN LØSER
///
/// Lyden ligger allerede i 30-sekunders segmenter — det gør den, fordi et
/// crash ikke må koste mere end et halvt minut. De segmenter er færdige filer
/// længe før mødet er slut, og der er ingen grund til, at de skal vente.
///
/// Køres der med undervejs, er det kun HALEN, der er tilbage, når mødet
/// stopper. Det samlede arbejde bliver ikke mindre; det meste af det er bare
/// gjort, inden nogen venter på det.
///
/// GEVINSTEN AFHÆNGER AF LÆNGDEN. Målt 26-08-2026: et webinar på 37 minutter
/// tog 2 minutter 53 sekunder bagefter, og der er ikke meget at hente. På et
/// møde på to timer er der. Se doc/maaling-stilhed.md.
///
/// ============ OPTAGELSE MÅ ALDRIG KUNNE BLOKERES ============
///
/// Det er appens vigtigste løfte, og den her er det farligste, der er lavet
/// ved det: noget tungt kører på grafikkortet, mens der optages.
///
/// Derfor er reglen ikke «håndtér fejl» men «FORSVIND VED FØRSTE FEJL». Går
/// noget galt — en fil kan ikke læses, motoren fejler, disken er fuld —
/// stopper medskrivningen sig selv og rører aldrig noget igen. Mødet bliver
/// skrevet ud bagefter på den almindelige måde, præcis som før den her fandtes.
///
/// Det, der er skrevet indtil da, smides væk. En halv transskription, der
/// bliver flettet med en hel, er værre end ingen: den ville se komplet ud.
///
/// DET ÅBNE SEGMENT RØRES ALDRIG. Det sidste bliver skrevet lige nu, og en
/// halv WAV giver enten en fejl eller et afhugget ord. Se Medskrift.Naeste.
/// </summary>
public sealed class Medskriver : IDisposable
{
    /// <summary>
    /// Hvor tit der kigges efter nye segmenter.
    ///
    /// Et halvt minut. Et segment er tredive sekunder, og en bid er ti af dem,
    /// så der er rigeligt tid — og et opslag, der kun tæller filer i en mappe,
    /// koster ingenting.
    /// </summary>
    private static readonly TimeSpan Kigger = TimeSpan.FromSeconds(30);

    private readonly DispatcherTimer _ur = new() { Interval = Kigger };

    private readonly string _sessionDir;
    private readonly string _spor;
    private readonly string _sprog;
    private readonly InstallState _install;

    private readonly List<Medskrift.Faerdigbid> _bidder = new();

    private int _faerdige;
    private bool _koerer;
    private bool _opgivet;

    /// <summary>
    /// Hvor segmenterne læses fra. Peger på optagelsens egne, indtil de er
    /// reddet — så peger den på kopien.
    /// </summary>
    private string _segmentmappe = "";

    private string Segmentmappe => _segmentmappe.Length > 0
        ? _segmentmappe
        : Path.Combine(_sessionDir, "segmenter", _spor);

    /// <summary>Er der noget brugbart? Falsk, hvis den har givet op.</summary>
    public bool Duer => !_opgivet && _bidder.Count > 0;

    /// <summary>Hvor mange segmenter der er skrevet ud indtil nu.</summary>
    public int Faerdige => _faerdige;

    public Medskriver(string sessionDir, string spor, string sprog, InstallState install)
    {
        _sessionDir = sessionDir;
        _spor = spor;
        _sprog = sprog;
        _install = install;

        _ur.Tick += async (_, _) => await Kig();
    }

    public void Start()
    {
        if (!_install.IsComplete) { _opgivet = true; return; }
        _ur.Start();
    }

    /// <summary>
    /// Redder de segmenter, der ikke er skrevet ud endnu. Falsk = intet at redde.
    /// </summary>
    /// <remarks>
    /// KALDES FØR OPTAGELSEN SAMLES. TrackRecorder.Assemble lægger segmenterne
    /// sammen til én WAV og SLETTER dem bagefter — resten af mødet ville
    /// forsvinde, mens medskrivningen stod og skulle bruge det.
    ///
    /// Der kopieres frem for at skrives ud her. En kopi af nogle få megabyte
    /// tager et øjeblik; at skrive dem ud tager op mod et minut, og det ville
    /// ske, mens brugeren venter på, at stopknappen gør noget.
    /// </remarks>
    public bool RedResten()
    {
        _ur.Stop();

        if (_opgivet) return false;

        try
        {
            // Er der intet tilbage, er der intet at redde - men bidderne skal
            // stadig flettes, saa der svares sandt.
            var mangler = Medskrift.Naeste(TaelSegmenter(), _faerdige, optagerStadig: false);
            if (mangler is null) return _bidder.Count > 0;

            var ly = Path.Combine(Path.GetTempPath(), $"noteapp_hale_{Guid.NewGuid():N}");
            Directory.CreateDirectory(ly);

            var fra = Path.Combine(_sessionDir, "segmenter", _spor);

            foreach (var f in Directory.EnumerateFiles(fra, "seg_*.wav"))
                File.Copy(f, Path.Combine(ly, Path.GetFileName(f)), overwrite: true);

            _segmentmappe = ly;
            _ly = ly;
            return true;
        }
        catch (Exception)
        {
            Opgiv();
            return false;
        }
    }

    /// <summary>Kopimappen, der skal ryddes op til sidst.</summary>
    private string? _ly;

    /// <summary>Hvor mange sekunder lyd der er skrevet ud, og hvor lang tid det tog.</summary>
    private double _skrevetSekunder;
    private double _brugtSekunder;

    /// <summary>
    /// Hvor lang tid halen tager — regnet ud af, hvad de foregående bidder kostede.
    /// </summary>
    /// <remarks>
    /// MÅLT, IKKE GÆTTET. Hastigheden svinger for meget til at kunne slås op i
    /// en tabel — målt til 17 sekunder lyd pr. sekund på det ene spor og 4 på
    /// det andet i samme møde. Men INDEN FOR ét møde på ét spor er den stabil,
    /// og bidderne har lige målt den.
    ///
    /// Har den ikke skrevet noget endnu, er der ingenting at regne på, og der
    /// svares nul. Så siger skærmen bare, at den gør resten færdig — det er
    /// ærligere end et tal, der er fundet på.
    /// </remarks>
    public double SekunderTilbage()
    {
        if (_opgivet || _brugtSekunder <= 0 || _skrevetSekunder <= 0) return 0;

        var mangler = Math.Max(0, TaelSegmenter() - _faerdige)
                      * AudioFormat.ChunkDuration.TotalSeconds;

        return mangler / (_skrevetSekunder / _brugtSekunder);
    }

    /// <summary>
    /// Stopper og skriver resten ud. Returnerer den samlede json — eller null.
    /// </summary>
    /// <remarks>
    /// NULL BETYDER «GLEM DET HER». Så skriver kalderen mødet ud på den
    /// almindelige måde. Det er dét, der gør medskrivningen ufarlig: den kan
    /// altid fravælges, og resultatet er det samme som før.
    /// </remarks>
    public async Task<string?> AfslutAsync(CancellationToken ct = default)
    {
        _ur.Stop();

        if (_opgivet) return null;

        // Vent paa den, der maatte koere. To whisper paa samme kort samtidig
        // er ikke noget, man skal proeve af paa et rigtigt moede.
        for (var i = 0; i < 600 && _koerer; i++)
            await Task.Delay(100, ct);

        try
        {
            // RESTEN MED, uanset hvor lidt der er tilbage. Ellers manglede
            // slutningen af hvert eneste moede.
            while (Medskrift.Naeste(TaelSegmenter(), _faerdige, optagerStadig: false) is { } bid)
                if (!await SkrivBidAsync(bid, ct)) return null;
        }
        catch (Exception)
        {
            return null;
        }

        return _bidder.Count > 0 ? Medskrift.Flet(_bidder) : null;
    }

    private async Task Kig()
    {
        if (_opgivet || _koerer) return;

        try
        {
            if (Medskrift.Naeste(TaelSegmenter(), _faerdige, optagerStadig: true) is not { } bid) return;

            await SkrivBidAsync(bid, CancellationToken.None);
        }
        catch (Exception)
        {
            Opgiv();
        }
    }

    /// <summary>Skriver én bid ud. Falsk = det gik galt, og alt er opgivet.</summary>
    private async Task<bool> SkrivBidAsync(Medskrift.Bid bid, CancellationToken ct)
    {
        _koerer = true;

        var ur = System.Diagnostics.Stopwatch.StartNew();
        var arbejde = Path.Combine(Path.GetTempPath(), $"noteapp_bid_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(arbejde);

            var wav = Path.Combine(arbejde, "bid.wav");

            if (!SamlSegmenter(bid, wav)) { Opgiv(); return false; }

            var udbase = Path.Combine(arbejde, "bid");

            var motor = new Transcriber(_install.WhisperCli!);

            await motor.RunAsync(
                new TranscriptionRequest(wav, _install.ModelPath!, udbase, _sprog),
                progress: null, ct);

            var json = udbase + ".json";
            if (!File.Exists(json)) { Opgiv(); return false; }

            _bidder.Add(new Medskrift.Faerdigbid(
                await File.ReadAllTextAsync(json, ct),
                bid.StartISekunder,
                bid.KastVaekSekunder));

            _faerdige = bid.Til;

            _skrevetSekunder += bid.Antal * AudioFormat.ChunkDuration.TotalSeconds;
            _brugtSekunder += ur.Elapsed.TotalSeconds;

            return true;
        }
        catch (Exception)
        {
            Opgiv();
            return false;
        }
        finally
        {
            _koerer = false;
            try { if (Directory.Exists(arbejde)) Directory.Delete(arbejde, recursive: true); }
            catch (Exception) { /* spildplads, ikke et tab */ }
        }
    }

    /// <summary>
    /// Lægger biddens segmenter sammen til én WAV. Falsk = en fil manglede.
    /// </summary>
    /// <remarks>
    /// MANGLER ET SEGMENT, GIVES DER OP. SegmentAssembler springer et ulæseligt
    /// segment over, og det er rigtigt DÉR: at miste tredive sekunder er bedre
    /// end at miste mødet. Her er det forkert — springes et segment over,
    /// passer tiderne ikke længere, og hele resten af mødet ville stå forskudt.
    /// </remarks>
    private bool SamlSegmenter(Medskrift.Bid bid, string maal)
    {
        var filer = new List<string>();

        for (var i = bid.LydFra; i < bid.Til; i++)
        {
            // Segmenterne taeller fra 1, bidderne fra 0.
            var sti = Path.Combine(Segmentmappe, $"seg_{i + 1:D5}.wav");
            if (!File.Exists(sti)) return false;
            filer.Add(sti);
        }

        if (filer.Count == 0) return false;

        using var skriver = new NAudio.Wave.WaveFileWriter(maal, SegmentAssembler.Format);

        foreach (var f in filer)
        {
            using var laeser = new NAudio.Wave.WaveFileReader(f);
            laeser.CopyTo(skriver);
        }

        return true;
    }

    private int TaelSegmenter() =>
        Directory.Exists(Segmentmappe)
            ? Directory.EnumerateFiles(Segmentmappe, "seg_*.wav").Count()
            : 0;

    /// <summary>
    /// Giver op og rører ikke noget igen.
    /// </summary>
    /// <remarks>
    /// Det, der er skrevet indtil nu, smides væk. En halv transskription, der
    /// bliver flettet med en hel, er værre end ingen — den ville se komplet ud.
    /// </remarks>
    private void Opgiv()
    {
        _opgivet = true;
        _ur.Stop();
        _bidder.Clear();
    }

    public void Dispose()
    {
        _ur.Stop();
        _bidder.Clear();

        try
        {
            if (_ly is not null && Directory.Exists(_ly)) Directory.Delete(_ly, recursive: true);
        }
        catch (Exception)
        {
            // Spildplads i tempmappen, ikke et tab. Windows rydder den selv.
        }

        _ly = null;
    }
}
