using System.Net;
using System.Security.Cryptography;
using System.Text;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Filstoerrelse alene er ikke integritetskontrol.
/// </summary>
/// <remarks>
/// DET ER MAALT, IKKE EN TEORETISK INDVENDING. ggml-silero-v5.1.2.bin og
/// ggml-silero-v6.2.0.bin er BEGGE paa noejagtig 885.098 byte og er to
/// forskellige modeller - slaaet op hos Hugging Face 03-09-2026. Kontrollen,
/// appen havde indtil da, kunne ikke se forskel paa dem.
///
/// En forkert modelfil fejler ikke med det samme. Den giver volapyk i
/// udskriften, og det opdages langt fra sin aarsag - hvis det opdages.
/// </remarks>
public class KomponentsumTest
{
    // ======================== MANIFESTET ========================

    [Fact]
    public void Manifestet_kan_laeses_og_er_ikke_tomt()
    {
        Assert.NotEmpty(Komponentmanifest.Alle);
    }

    [Fact]
    public void Hver_komponent_har_en_rigtig_SHA256_og_en_kilde()
    {
        foreach (var k in Komponentmanifest.Alle)
        {
            // 64 hextegn med smaa bogstaver - samme form, som
            // Downloader.Sha256Async giver. Staar den med store bogstaver,
            // ville sammenligningen fejle paa en fil, der var i orden.
            Assert.True(k.Sha256.Length == 64,
                $"{k.Filnavn}: summen er {k.Sha256.Length} tegn, ikke 64.");

            Assert.Matches("^[0-9a-f]{64}$", k.Sha256);

            Assert.False(string.IsNullOrWhiteSpace(k.Kilde), $"{k.Filnavn} mangler kilde.");
            Assert.False(string.IsNullOrWhiteSpace(k.Sumkilde), $"{k.Filnavn} mangler sumkilde.");
            Assert.True(k.Bytes > 0, $"{k.Filnavn} mangler stoerrelse.");
        }
    }

    [Fact]
    public void Alt_i_modelkataloget_har_en_kendt_sum()
    {
        // Kataloget maa ikke kunne faa en model til, uden at summen foelger
        // med. Sker det, hentes den uden verifikation - og det ville ingen
        // opdage, for den ville virke.
        foreach (var model in WhisperInstall.Models)
        {
            Assert.True(Komponentmanifest.Kendt(model.FileName),
                $"{model.FileName} staar i kataloget uden en kendt kontrolsum.");
        }
    }

    [Fact]
    public void Manifestets_stoerrelse_stemmer_med_katalogets()
    {
        // To tal om den samme fil to steder. Er de uenige, er mindst det ene
        // forkert - og en forkert stoerrelse faar hentningen til at starte
        // forfra hver eneste gang.
        foreach (var model in WhisperInstall.Models)
        {
            var sum = Komponentmanifest.For(model.FileName)!;

            Assert.Equal(model.Bytes, sum.Bytes);
        }
    }

    [Fact]
    public void Stemmevagten_har_en_kendt_sum_og_den_stemmer_med_stoerrelsen()
    {
        var vad = Komponentmanifest.For(WhisperInstall.VadFilnavn);

        Assert.NotNull(vad);
        Assert.Equal(WhisperInstall.VadStoerrelse, vad!.Bytes);
        Assert.Equal(vad.Sha256, WhisperInstall.VadSum);
    }

    [Fact]
    public void En_ukendt_fil_har_ingen_sum_og_det_siges_ligeud()
    {
        Assert.Null(Komponentmanifest.For("noget-ingen-har-hoert-om.bin"));
        Assert.False(Komponentmanifest.Kendt("noget-ingen-har-hoert-om.bin"));

        // Teksten maa ikke kunne laeses som en beroligelse.
        Assert.Contains("forsyningskæderisiko", Komponentmanifest.Ukendt);
        Assert.Contains("ikke", Komponentmanifest.Ukendt);
    }

    [Fact]
    public void Manifestets_tekst_staar_paa_rigtigt_dansk()
    {
        // TEKSTEN HER LAESES AF BRUGEREN paa fanen «Modeller og licenser».
        // Kodekommentarer er ASCII i det her repo; det, brugeren ser, er det
        // ikke. Foerste udgave af manifestet blev skrevet som om det var kode,
        // og saa stod der «afgoer» og «Efterproevet» paa skaermen (set paa et
        // skaermbillede 03-09-2026).
        //
        // Listen er de erstatninger, der faktisk stod der. Kommer der flere,
        // hoerer de til her.
        string[] erstatninger =
        {
            "oe", "aa", "ae"
        };

        string[] undtagelser =
        {
            // Rigtige ord og navne, der indeholder bogstavparrene.
            "Hugging Face", "aktiv", "Whisper", "whisper", "bin", "zip", "ggml"
        };

        foreach (var k in Komponentmanifest.Alle)
        {
            foreach (var (felt, tekst) in new[]
                     {
                         ("rolle", k.Rolle),
                         ("sumkilde", k.Sumkilde),
                         ("bekraeftelse", k.Bekraeftelse)
                     })
            {
                var renset = undtagelser.Aggregate(tekst, (t, u) => t.Replace(u, " "));

                foreach (var e in erstatninger)
                {
                    Assert.False(renset.Contains(e, StringComparison.Ordinal),
                        $"{k.Filnavn}.{felt} indeholder «{e}» — teksten vises for brugeren "
                        + $"og skal have æ, ø og å:\n  {tekst}");
                }
            }
        }
    }

    [Fact]
    public void Filnavnet_slaas_op_uanset_sti_og_store_bogstaver()
    {
        Assert.NotNull(Komponentmanifest.For(@"C:\AppNoter\motor\modeller\ggml-large-v3.bin"));
        Assert.NotNull(Komponentmanifest.For("GGML-LARGE-V3.BIN"));
    }

    // ================ STOERRELSE ER IKKE NOK - MAALINGEN ================

    [Fact]
    public void To_forskellige_filer_kan_have_samme_stoerrelse()
    {
        // Det her er hele begrundelsen for punktet, skrevet som en proeve.
        // Tallene er stemmevagtens to udgaver, slaaet op hos Hugging Face
        // 03-09-2026: samme stoerrelse, forskellige summer.
        const long stoerrelse = 885_098L;

        const string v512 = "29940d98d42b91fbd05ce489f3ecf7c72f0a42f027e4875919a28fb4c04ea2cf";
        const string v620 = "2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987";

        Assert.NotEqual(v512, v620);

        var vad = Komponentmanifest.For(WhisperInstall.VadFilnavn)!;

        Assert.Equal(stoerrelse, vad.Bytes);
        Assert.Equal(v512, vad.Sha256);
    }

    [Fact]
    public void Stemmevagten_afvises_naar_summen_ikke_passer()
    {
        // Rigtig stoerrelse, forkert indhold. Den gamle kontrol - kun
        // stoerrelse - ville have sagt ja.
        var forkert = new byte[WhisperInstall.VadStoerrelse];
        Assert.Equal(WhisperInstall.VadStoerrelse, forkert.Length);

        Assert.False(WhisperInstall.VadPasser(forkert));
    }

    [Fact]
    public void Stemmevagten_afvises_ogsaa_paa_forkert_stoerrelse()
    {
        Assert.False(WhisperInstall.VadPasser(new byte[10]));
    }

    // ==================== SUMMEN AF EN FIL ====================

    private static string Sum(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    [Fact]
    public async Task Sha256_af_en_fil_er_den_samme_som_af_dens_bytes()
    {
        using var mappe = new Proevemappe();

        var sti = Path.Combine(mappe.Sti, "en-fil.bin");
        var data = Encoding.UTF8.GetBytes("prøvetekst med æøå");
        await File.WriteAllBytesAsync(sti, data);

        Assert.Equal(Sum(data), await Downloader.Sha256Async(sti));
    }

    [Fact]
    public async Task Summen_af_en_stor_fil_regnes_i_bidder_og_giver_det_samme()
    {
        using var mappe = new Proevemappe();

        // Over en bid paa 1 MB, saa loekken koerer mere end en gang. Det var
        // dér, en tidligere udgave af den slags kode tog fejl.
        var data = new byte[3 * (1 << 20) + 7];
        new Random(42).NextBytes(data);

        var sti = Path.Combine(mappe.Sti, "stor.bin");
        await File.WriteAllBytesAsync(sti, data);

        Assert.Equal(Sum(data), await Downloader.Sha256Async(sti));
    }

    // ============ HENTNINGEN: RIGTIG SUM, FORKERT SUM, AFBRUDT ============
    //
    // Der hentes fra en HttpListener paa localhost. Ikke fra nettet: en
    // proeve, der kraever internet, er en proeve, der falder af grunde, den
    // ikke handler om.

    private sealed class Proeveserver : IDisposable
    {
        private readonly HttpListener _lytter = new();
        private readonly CancellationTokenSource _stop = new();

        public Proeveserver(byte[] indhold, bool afbrydEfterHalvdelen = false)
        {
            Adresse = $"http://127.0.0.1:{LedigPort()}/";
            _lytter.Prefixes.Add(Adresse);
            _lytter.Start();

            _ = Task.Run(async () =>
            {
                while (!_stop.IsCancellationRequested)
                {
                    HttpListenerContext ctx;
                    try { ctx = await _lytter.GetContextAsync(); }
                    catch (Exception) { return; }

                    try
                    {
                        // Range-anmodninger besvares som 200 med hele filen.
                        // Fortsaettelsen har sin egen proeve i Downloader; det,
                        // der proeves her, er kontrolsummen.
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentLength64 = indhold.Length;

                        if (afbrydEfterHalvdelen)
                        {
                            // HALVDELEN, OG SAA RIVES FORBINDELSEN OVER.
                            //
                            // Foerste udgave af den her proeve skrev halvdelen
                            // og LUKKEDE stroemmen paenet. Det gjorde den ikke:
                            // svaret lovede den fulde laengde i sin header, saa
                            // klienten blev staaende og ventede paa resten -
                            // og Downloader har uendelig tidsgraense, fordi en
                            // langsom forbindelse ikke er en fejl. Hele
                            // proevekoerslen hang.
                            //
                            // Abort river TCP-forbindelsen over, og det er
                            // ogsaa det, der SKER, naar et net falder ud.
                            await ctx.Response.OutputStream.WriteAsync(
                                indhold.AsMemory(0, indhold.Length / 2));
                            await ctx.Response.OutputStream.FlushAsync();
                            ctx.Response.Abort();
                        }
                        else
                        {
                            await ctx.Response.OutputStream.WriteAsync(indhold);
                            ctx.Response.OutputStream.Close();
                        }
                    }
                    catch (Exception)
                    {
                        // En afbrudt forbindelse er netop det, proeven vil have.
                    }
                }
            });
        }

        public string Adresse { get; }

        private static int LedigPort()
        {
            var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            l.Start();
            var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void Dispose()
        {
            _stop.Cancel();
            try { _lytter.Stop(); } catch (Exception) { }
            _lytter.Close();
            _stop.Dispose();
        }
    }

    /// <summary>
    /// En tidsgraense, saa en proeve aldrig kan haenge.
    /// </summary>
    /// <remarks>
    /// Downloader har UENDELIG tidsgraense, og det er med vilje: en langsom
    /// forbindelse er ikke en fejl, naar filen er 2,9 GB. I en proeve er den
    /// samme egenskab en faelde - en server, der ikke svarer faerdigt, ville
    /// laase hele koerslen. Det skete 03-09-2026.
    /// </remarks>
    private static CancellationToken Frist => new CancellationTokenSource(
        TimeSpan.FromSeconds(30)).Token;

    [Fact]
    public async Task Rigtig_sum_bliver_hentet_faerdig()
    {
        using var mappe = new Proevemappe();

        var data = Encoding.UTF8.GetBytes("det rigtige indhold");
        using var server = new Proeveserver(data);

        var maal = Path.Combine(mappe.Sti, "hentet.bin");

        var sti = await new Downloader().DownloadAsync(
            server.Adresse, maal, data.Length, null, Frist, Sum(data));

        Assert.True(File.Exists(sti));
        Assert.Equal(Sum(data), await Downloader.Sha256Async(sti));
        Assert.False(File.Exists(maal + ".delvis"));
    }

    [Fact]
    public async Task Forkert_sum_giver_en_fejl_og_filen_bliver_slettet()
    {
        using var mappe = new Proevemappe();

        var data = Encoding.UTF8.GetBytes("noget helt andet end det, der blev bestilt");
        using var server = new Proeveserver(data);

        var maal = Path.Combine(mappe.Sti, "hentet.bin");
        var forventet = Sum(Encoding.UTF8.GetBytes("det, der blev bestilt"));

        var fejl = await Assert.ThrowsAsync<IOException>(() =>
            new Downloader().DownloadAsync(server.Adresse, maal, data.Length, null, Frist, forventet));

        // DEN SKAL SIGE BEGGE SUMMER. En fejl, der kun siger «forkert sum»,
        // kan ingen goere noget ved.
        Assert.Contains(forventet, fejl.Message);

        // OG DEN MAA IKKE LIGGE TILBAGE. Hverken som fil eller som .delvis:
        // en fortsaettelse paa en forkert fil giver en forkert fil igen.
        Assert.False(File.Exists(maal));
        Assert.False(File.Exists(maal + ".delvis"));
    }

    [Fact]
    public async Task Et_afbrudt_download_bliver_ikke_omdoebt_til_en_hel_fil()
    {
        using var mappe = new Proevemappe();

        var data = Encoding.UTF8.GetBytes(new string('x', 4096));
        using var server = new Proeveserver(data, afbrydEfterHalvdelen: true);

        var maal = Path.Combine(mappe.Sti, "hentet.bin");

        await Assert.ThrowsAsync<IOException>(() =>
            new Downloader().DownloadAsync(server.Adresse, maal, data.Length, null, Frist, Sum(data)));

        // Maalet findes ikke - det halve blev aldrig omdoebt.
        Assert.False(File.Exists(maal));

        // Det hentede er GEMT, saa naeste forsoeg kan fortsaette. Det er en
        // anden beslutning end ved en forkert sum, og med vilje.
        Assert.True(File.Exists(maal + ".delvis"));
    }

    [Fact]
    public async Task En_eksisterende_oedelagt_fil_bliver_hentet_om()
    {
        using var mappe = new Proevemappe();

        var rigtigt = Encoding.UTF8.GetBytes("det rigtige indhold");
        using var server = new Proeveserver(rigtigt);

        var maal = Path.Combine(mappe.Sti, "hentet.bin");

        // En fil med den RIGTIGE stoerrelse og det FORKERTE indhold. Det er
        // netop den, en stoerrelseskontrol lukker igennem.
        var oedelagt = new byte[rigtigt.Length];
        Array.Fill(oedelagt, (byte)'z');
        await File.WriteAllBytesAsync(maal, oedelagt);

        Assert.Equal(rigtigt.Length, new FileInfo(maal).Length);

        var sti = await new Downloader().DownloadAsync(
            server.Adresse, maal, rigtigt.Length, null, Frist, Sum(rigtigt));

        Assert.Equal(Sum(rigtigt), await Downloader.Sha256Async(sti));
    }

    [Fact]
    public async Task En_eksisterende_rigtig_fil_hentes_ikke_igen()
    {
        using var mappe = new Proevemappe();

        var data = Encoding.UTF8.GetBytes("det rigtige indhold");

        // Ingen server. Naaede kaldet ud paa nettet, ville det fejle - og
        // dét er netop, hvad proeven vil vide, at det ikke goer.
        var maal = Path.Combine(mappe.Sti, "hentet.bin");
        await File.WriteAllBytesAsync(maal, data);

        var sti = await new Downloader().DownloadAsync(
            "http://127.0.0.1:1/findes-ikke", maal, data.Length, null, Frist, Sum(data));

        Assert.Equal(maal, sti);
    }

    [Fact]
    public async Task Manifestets_sum_gaelder_ogsaa_naar_kalderen_ikke_sender_en()
    {
        using var mappe = new Proevemappe();

        // Filen faar et navn, manifestet KENDER. Saa skal summen slaa til,
        // selv om kaldstedet ikke sendte nogen med - det er hele grunden til,
        // at opslaget ligger i Downloader og ikke ved kaldstederne.
        var data = Encoding.UTF8.GetBytes("noget, der bestemt ikke er stemmevagten");
        using var server = new Proeveserver(data);

        var maal = Path.Combine(mappe.Sti, WhisperInstall.VadFilnavn);

        await Assert.ThrowsAsync<IOException>(() =>
            new Downloader().DownloadAsync(server.Adresse, maal, data.Length, null, Frist));

        Assert.False(File.Exists(maal));
    }
}
