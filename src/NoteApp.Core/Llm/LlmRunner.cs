using System.Diagnostics;
using System.Text;

namespace NoteApp.Core.Llm;

public sealed record LlmResult(
    string Text,
    TimeSpan Elapsed,
    string ModelFile,
    int PromptTokens,
    int ResponseTokens)
{
    public double TokensPerSecond => Elapsed.TotalSeconds <= 0 ? 0 : ResponseTokens / Elapsed.TotalSeconds;
}

public sealed record LlmProgress(string Message, string? PartialText = null);

/// <summary>
/// Kører en lokal sprogmodel gennem llama.cpp.
///
/// Der bruges engangskørsler med llama-cli frem for llama-server. Serveren
/// ville lytte på en port, også når man ikke bruger den — og selvom 127.0.0.1
/// ikke er "ud af maskinen", er "der lytter ingenting" en nemmere påstand at
/// stå på. Et referat laves én gang pr. møde; der er ingen grund til en server.
///
/// Modellen ligger på disken, og der er intet netværkskald i denne klasse.
/// </summary>
public sealed class LlmRunner
{
    private readonly string _cli;

    public LlmRunner(string llamaCli) => _cli = llamaCli;

    /// <summary>Finder llama-cli.exe, hvis den er hentet.</summary>
    public static string? FindCli()
    {
        var rod = Path.Combine(UserDataPaths.Root, "motor", "llama");
        if (!Directory.Exists(rod)) return null;

        return Directory.EnumerateFiles(rod, "llama-cli.exe", SearchOption.AllDirectories).FirstOrDefault();
    }

    public static string ModelDirectory => Path.Combine(UserDataPaths.Root, "motor", "sprogmodeller");

    public static IReadOnlyList<string> InstalledModels()
    {
        if (!Directory.Exists(ModelDirectory)) return Array.Empty<string>();
        return Directory.GetFiles(ModelDirectory, "*.gguf").OrderBy(f => f).ToList();
    }

    /// <summary>
    /// Kører én prompt og venter på hele svaret.
    ///
    /// Konteksten sættes efter promptens længde frem for til et fast tal: et
    /// 20-minutters møde fylder omkring 4.000 tokens, og at reservere plads
    /// til 32.000 ville koste hukommelse, der ikke er på et 6 GB-kort.
    /// </summary>
    public async Task<LlmResult> RunAsync(
        string modelPath,
        PromptTemplate template,
        string userPrompt,
        IProgress<LlmProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(modelPath)) throw new FileNotFoundException("Sprogmodellen findes ikke", modelPath);

        // Groft skoen: dansk tekst lander omkring 3 tegn pr. token. Bevidst
        // rundhaandet, saa prompten ikke bliver klippet over.
        var anslaaetPrompt = (template.SystemPrompt.Length + userPrompt.Length) / 3 + 200;
        var kontekst = Naermeste2Potens(anslaaetPrompt + template.MaxTokens + 512);

        var args = new List<string>
        {
            "-m", modelPath,
            "-sys", template.SystemPrompt,
            "-p", userPrompt,
            "-n", template.MaxTokens.ToString(),
            "-c", kontekst.ToString(),
            "--temp", template.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-ngl", "999",          // laeg alt paa GPU'en; llama.cpp falder selv tilbage
            "--no-warmup",
            "-no-cnv"               // eet svar, ikke en samtale
        };

        var psi = new ProcessStartInfo(_cli)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var svar = new StringBuilder();
        var log = new StringBuilder();
        var ur = Stopwatch.StartNew();

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // llama-cli skriver svaret paa stdout og alt om modelindlaesning,
        // hastighed og hukommelse paa stderr. Begge skal laeses, ellers
        // fyldes bufferen og processen gaar i staa.
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            svar.AppendLine(e.Data);
            progress?.Report(new LlmProgress("Skriver …", e.Data));
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            log.AppendLine(e.Data);
            if (e.Data.Contains("llama_model_loader", StringComparison.Ordinal))
                progress?.Report(new LlmProgress("Indlæser modellen …"));
        };

        progress?.Report(new LlmProgress("Starter modellen …"));

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        ur.Stop();

        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"llama-cli afsluttede med kode {p.ExitCode}.\n{Sidste(log.ToString(), 8)}");

        var tekst = svar.ToString().Trim();
        var (prompt, respons) = LaesTokental(log.ToString());

        return new LlmResult(tekst, ur.Elapsed, Path.GetFileName(modelPath), prompt, respons);
    }

    /// <summary>
    /// Tokentallene står i llama.cpp's egen opsummering på stderr. De bruges
    /// til at måle, hvad en kørsel koster — ikke til noget funktionelt.
    /// </summary>
    private static (int Prompt, int Respons) LaesTokental(string log)
    {
        var prompt = System.Text.RegularExpressions.Regex.Match(log, @"prompt eval time.*?/\s*(\d+) tokens");
        var respons = System.Text.RegularExpressions.Regex.Match(log, @"\beval time.*?/\s*(\d+) runs");

        return (prompt.Success ? int.Parse(prompt.Groups[1].Value) : 0,
                respons.Success ? int.Parse(respons.Groups[1].Value) : 0);
    }

    private static string Sidste(string tekst, int linjer) =>
        string.Join('\n', tekst.Split('\n').Where(l => l.Trim().Length > 0).TakeLast(linjer));

    private static int Naermeste2Potens(int n)
    {
        var v = 2048;
        while (v < n && v < 131072) v *= 2;
        return v;
    }
}
