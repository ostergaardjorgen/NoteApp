using System.Security.Cryptography;

namespace NoteApp.Core;

public sealed record DownloadProgress(long BytesDone, long BytesTotal, double BytesPerSecond)
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
        klient.DefaultRequestHeaders.UserAgent.ParseAdd("NoteApp");
        return klient;
    }

    /// <summary>
    /// Henter én fil. Skriver til en .delvis-fil undervejs og omdøber først
    /// til sidst — et afbrudt download må aldrig efterlade noget, der ligner
    /// en brugbar model. Findes målet allerede med rigtig størrelse, gøres
    /// der ingenting.
    /// </summary>
    public async Task<string> DownloadAsync(
        string url,
        string destination,
        long? expectedBytes = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (File.Exists(destination))
        {
            var findes = new FileInfo(destination).Length;
            if (expectedBytes is null || findes == expectedBytes) return destination;
            File.Delete(destination);   // halv fil fra en tidligere kørsel
        }

        var midlertidig = destination + ".delvis";

        using var svar = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        svar.EnsureSuccessStatusCode();

        var total = svar.Content.Headers.ContentLength ?? expectedBytes ?? -1;

        await using (var netværk = await svar.Content.ReadAsStreamAsync(ct))
        await using (var fil = File.Create(midlertidig))
        {
            var buffer = new byte[1 << 20];   // 1 MB
            long hentet = 0;
            var ur = System.Diagnostics.Stopwatch.StartNew();
            var sidsteMelding = TimeSpan.Zero;

            int læst;
            while ((læst = await netværk.ReadAsync(buffer, ct)) > 0)
            {
                await fil.WriteAsync(buffer.AsMemory(0, læst), ct);
                hentet += læst;

                // Meld hvert kvarte sekund. Oftere gør kun UI-tråden travl.
                if (ur.Elapsed - sidsteMelding > TimeSpan.FromMilliseconds(250))
                {
                    sidsteMelding = ur.Elapsed;
                    progress?.Report(new DownloadProgress(
                        hentet, total, hentet / Math.Max(0.001, ur.Elapsed.TotalSeconds)));
                }
            }

            progress?.Report(new DownloadProgress(hentet, total <= 0 ? hentet : total,
                hentet / Math.Max(0.001, ur.Elapsed.TotalSeconds)));
        }

        // Omdøbningen er det, der gør filen "rigtig". Sker den ikke, findes
        // der kun en .delvis, og næste kørsel starter forfra i stedet for at
        // bruge en halv model og fejle uforståeligt langt senere.
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
    public static async Task<string> Sha256Async(string path, CancellationToken ct = default)
    {
        await using var fil = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(fil, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
