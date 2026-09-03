using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace NoteApp.Core;

/// <summary>
/// Hvor langt en hentning er. <paramref name="Kontrollerer"/> er sat, mens
/// kontrolsummen regnes ud i stedet for, mens der hentes.
/// </summary>
/// <remarks>
/// FELTET FINDES, FORDI TEKSTEN ELLERS LYVER. Bjælken bruges til begge dele
/// — kontrollen af en fil på 2,9 GB tager sekunder nok til, at en skærm uden
/// bevægelse ligner en app, der er gået i stå. Men står der «Henter …», mens
/// der ikke hentes noget, er det en forkert oplysning, og de er værre end
/// ingen. Skærmen kan se forskel på det her felt.
/// </remarks>
public sealed record DownloadProgress(
    long BytesDone, long BytesTotal, double BytesPerSecond, bool Kontrollerer = false)
{
    public double Percent => BytesTotal <= 0 ? 0 : BytesDone * 100.0 / BytesTotal;

    public TimeSpan? Remaining => BytesPerSecond <= 0 || BytesTotal <= 0
        ? null
        : TimeSpan.FromSeconds((BytesTotal - BytesDone) / BytesPerSecond);
}

/// <summary>
/// DEN ENESTE NETVÆRKSKODE I HELE APPEN.
///
/// Retningen er det afgørende, jf. doc\mine-data.md: der hentes NED, aldrig
/// sendes OP. Klassen kan kun én ting — hente en navngiven fil fra en
/// navngiven adresse til disk — og den har med vilje ingen metode, der tager
/// et indhold med. Der er ingen POST, ingen krop, ingen headere med noget om
/// maskinen eller brugeren.
///
/// Ligger al netværkskode ét sted, kan spørgsmålet "sender appen noget?"
/// besvares ved at læse én fil. Det er hele grunden til, at den ikke er
/// spredt ud, hvor den skal bruges.
/// </summary>
public sealed class Downloader
{
    private static readonly HttpClient Http = Opret();

    private static HttpClient Opret()
    {
        var klient = new HttpClient
        {
            // Modelfiler er store, og en langsom forbindelse er ikke en fejl.
            Timeout = Timeout.InfiniteTimeSpan
        };

        // Et neutralt User-Agent. Ingen maskinnavn, intet brugernavn, ingen
        // version af noget, der kunne identificere installationen.
        klient.DefaultRequestHeaders.UserAgent.ParseAdd("HeyPia");
        return klient;
    }

    /// <summary>
    /// Henter én fil. Skriver til en .delvis-fil undervejs og omdøber først
    /// til sidst — et afbrudt download må aldrig efterlade noget, der ligner
    /// en brugbar model. Findes målet allerede med rigtig størrelse, gøres
    /// der ingenting.
    ///
    /// Et afbrudt download fortsættes, hvor det slap. En sprogmodel fylder
    /// 13 GB, og på en almindelig forbindelse tager den tyve minutter. Skal
    /// de tyve minutter tages om, hver gang nettet hikker eller maskinen
    /// genstarter, bliver modellen i praksis ikke hentet.
    ///
    /// Fortsættelsen er kun sikker, fordi den kontrolleres: serveren skal
    /// svare 206 og oplyse den samme samlede størrelse, og den færdige fil
    /// skal have den størrelse. Ellers hentes forfra. En halv model, der
    /// bliver omdøbt til en hel, fejler først langt senere og uforståeligt.
    /// </summary>
    public async Task<string> DownloadAsync(
        string url,
        string destination,
        long? expectedBytes = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default,
        string? forventetSum = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        // ============ HVOR SUMMEN KOMMER FRA ============
        //
        // Manifestet slaas op HER og ikke ved kaldstederne. Et nyt kaldsted
        // kan glemme en kontrol; det kan ikke undgaa den her. Samme opbygning
        // som SkyKatalog.KraevEuropa, og af samme grund.
        //
        // Kalderen kan sende en sum med - det goer proeverne - men kan ikke
        // slaa manifestets fra. Findes der en kendt sum for filnavnet,
        // gaelder den.
        var sum = (Komponentmanifest.For(destination)?.Sha256 ?? forventetSum)?.ToLowerInvariant();

        if (File.Exists(destination))
        {
            var findes = new FileInfo(destination).Length;

            // STOERRELSEN ER IKKE NOK, OG DET ER MAALT.
            //
            // ggml-silero-v5.1.2.bin og ggml-silero-v6.2.0.bin er BEGGE paa
            // noejagtig 885.098 byte og er to forskellige modeller. Indtil
            // 03-09-2026 stod der kun en stoerrelseskontrol her, og den kunne
            // ikke se forskel paa dem.
            var stoerrelsenPasser = expectedBytes is null || findes == expectedBytes;

            if (stoerrelsenPasser && sum is not null)
            {
                var fundet = await Sha256Async(destination, progress, ct);

                if (fundet == sum) return destination;

                // EN OEDELAGT FIL, DER ALLEREDE LIGGER DER, SKAL VAEK.
                // Bliver den staaende, koerer motoren videre paa den og giver
                // volapyk - en fejl, der viser sig langt fra sin aarsag.
                File.Delete(destination);
            }
            else if (stoerrelsenPasser)
            {
                return destination;
            }
            else
            {
                File.Delete(destination);   // halv fil fra en tidligere kørsel
            }
        }

        var midlertidig = destination + ".delvis";
        long alleredeHentet = File.Exists(midlertidig) ? new FileInfo(midlertidig).Length : 0;

        var anmodning = new HttpRequestMessage(HttpMethod.Get, url);
        if (alleredeHentet > 0) anmodning.Headers.Range = new RangeHeaderValue(alleredeHentet, null);

        using var svar = await Http.SendAsync(anmodning, HttpCompletionOption.ResponseHeadersRead, ct);

        // 416 betyder, at .delvis er mindst lige så stor som filen på serveren
        // — altså rester fra noget andet. Kast den væk og hent forfra.
        if (svar.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(midlertidig);
            return await DownloadAsync(url, destination, expectedBytes, progress, ct, forventetSum);
        }

        svar.EnsureSuccessStatusCode();

        // Serveren bestemmer. Svarer den 200 på en Range-anmodning, kan den
        // ikke fortsætte, og så sendes hele filen fra begyndelsen.
        bool fortsætter = svar.StatusCode == System.Net.HttpStatusCode.PartialContent;
        long startFra = fortsætter ? alleredeHentet : 0;

        long total = fortsætter
            ? svar.Content.Headers.ContentRange?.Length ?? -1
            : svar.Content.Headers.ContentLength ?? expectedBytes ?? -1;

        // Bemærk: der sammenlignes IKKE med expectedBytes her. Den værdi er
        // katalogets, og katalogets tal har taget fejl før — Mistral stod
        // 793 KB forkert. En kontrol mod et tal, vi selv har gættet, ville
        // smide et halvt hentet download på 5 GB væk, hver gang gættet var
        // skævt. Det, der tæller, er serverens eget tal, og det kontrolleres
        // mod den færdige fil nedenfor.

        await using (var netværk = await svar.Content.ReadAsStreamAsync(ct))
        await using (var fil = new FileStream(
            midlertidig,
            fortsætter ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[1 << 20];   // 1 MB
            long hentet = startFra;
            long iDenneKørsel = 0;
            var ur = System.Diagnostics.Stopwatch.StartNew();
            var sidsteMelding = TimeSpan.Zero;

            int læst;
            while ((læst = await netværk.ReadAsync(buffer, ct)) > 0)
            {
                await fil.WriteAsync(buffer.AsMemory(0, læst), ct);
                hentet += læst;
                iDenneKørsel += læst;

                // Meld hvert kvarte sekund. Oftere gør kun UI-tråden travl.
                // Hastigheden regnes på det, der er hentet NU — ellers ser en
                // fortsættelse ud til at køre absurd hurtigt.
                if (ur.Elapsed - sidsteMelding > TimeSpan.FromMilliseconds(250))
                {
                    sidsteMelding = ur.Elapsed;
                    progress?.Report(new DownloadProgress(
                        hentet, total, iDenneKørsel / Math.Max(0.001, ur.Elapsed.TotalSeconds)));
                }
            }

            progress?.Report(new DownloadProgress(hentet, total <= 0 ? hentet : total,
                iDenneKørsel / Math.Max(0.001, ur.Elapsed.TotalSeconds)));
        }

        // Sidste kontrol før omdøbningen: har filen den størrelse, den skal?
        var endelig = new FileInfo(midlertidig).Length;
        if (total > 0 && endelig != total)
            throw new IOException(
                $"Hentningen af {Path.GetFileName(destination)} blev afbrudt: " +
                $"{endelig:N0} af {total:N0} byte. Det hentede er gemt, så næste " +
                "forsøg fortsætter, hvor dette slap.");

        // ============ KONTROLSUMMEN, FOER OMDOEBNINGEN ============
        //
        // Stoerrelsen ovenfor siger, at hentningen ikke blev afbrudt. Den
        // siger ikke, at det er den rigtige fil. To forskellige modeller kan
        // have samme stoerrelse - stemmevagtens v5.1.2 og v6.2.0 er begge
        // paa 885.098 byte.
        //
        // .DELVIS SLETTES VED FORKERT SUM, og det er en anden beslutning end
        // ved en afbrudt hentning. Der beholdes filen, saa naeste forsoeg kan
        // fortsaette. Her ville det vaere det forkerte: en fortsaettelse paa
        // en forkert fil giver en forkert fil igen, hver gang, indtil nogen
        // sletter den i haanden.
        if (sum is not null)
        {
            var fundet = await Sha256Async(midlertidig, progress, ct);

            if (fundet != sum)
            {
                try { File.Delete(midlertidig); } catch (IOException) { }

                throw new IOException(
                    $"{Path.GetFileName(destination)} er ikke den fil, den skulle være.\n\n" +
                    $"Forventet SHA-256: {sum}\n" +
                    $"Hentet:            {fundet}\n\n" +
                    "Filen er slettet og bliver ikke brugt. Prøv igen — sker det samme, " +
                    "er filen hos leverandøren en anden end den, appen er efterprøvet mod.");
            }
        }

        // Omdøbningen er det, der gør filen "rigtig". Sker den ikke, findes
        // der kun en .delvis, som næste kørsel fortsætter på.
        File.Move(midlertidig, destination, overwrite: true);
        return destination;
    }

    /// <summary>
    /// Henter en kort tekst — bruges KUN til at slå op, hvilken version af
    /// motoren der er den nyeste. Der sendes intet med om maskinen, brugeren
    /// eller den installerede version; svaret sammenlignes lokalt.
    ///
    /// Må kun kaldes, fordi brugeren har trykket "søg efter opdatering".
    /// Aldrig automatisk ved opstart: en app, der ringer hjem af sig selv,
    /// er præcis det, princippet i doc\mine-data.md findes for at undgå.
    /// </summary>
    public async Task<string> FetchTextAsync(string url, CancellationToken ct = default)
    {
        using var svar = await Http.GetAsync(url, ct);
        svar.EnsureSuccessStatusCode();
        return await svar.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// SHA-256 af en fil. Bruges til at afgøre, om det hentede faktisk er
    /// det, der var meningen — en afbrudt eller ombyttet modelfil giver
    /// ellers volapyk-transskriptioner, som er svære at spore tilbage hertil.
    /// </summary>
    public static async Task<string> Sha256Async(string path, CancellationToken ct = default) =>
        await Sha256Async(path, null, ct);

    /// <summary>
    /// SHA-256 med fremdrift undervejs.
    /// </summary>
    /// <remarks>
    /// FREMDRIFTEN ER IKKE PYNT. large-v3 fylder 2,9 GB, og at laese den
    /// igennem tager sekunder nok til, at en skaerm uden nogen bevaegelse
    /// ligner en app, der er gaaet i staa. Uden det ville kontrollen blive
    /// oplevet som en fejl, den foerste gang nogen saa den.
    ///
    /// Der meldes med det samme tal, hentningen bruger, saa bjaelken bare
    /// koerer forbi en gang mere. Et andet fremdriftsbegreb ville kraeve en
    /// anden bjaelke.
    /// </remarks>
    public static async Task<string> Sha256Async(
        string path, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        await using var fil = File.OpenRead(path);

        using var sha = SHA256.Create();
        var buffer = new byte[1 << 20];   // 1 MB, som i hentningen
        long laest = 0;
        var ur = System.Diagnostics.Stopwatch.StartNew();
        var sidsteMelding = TimeSpan.Zero;

        int n;
        while ((n = await fil.ReadAsync(buffer, ct)) > 0)
        {
            sha.TransformBlock(buffer, 0, n, null, 0);
            laest += n;

            if (progress is not null && ur.Elapsed - sidsteMelding > TimeSpan.FromMilliseconds(250))
            {
                sidsteMelding = ur.Elapsed;
                progress.Report(new DownloadProgress(
                    laest, fil.Length, laest / Math.Max(0.001, ur.Elapsed.TotalSeconds),
                    Kontrollerer: true));
            }
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}
