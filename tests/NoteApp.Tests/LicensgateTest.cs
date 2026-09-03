using System.Diagnostics;
using System.Text;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Gaten skal FEJLE, naar en licensfil eller en taleradskillelseskomponent
/// mangler.
/// </summary>
/// <remarks>
/// HVORFOR DEN PROEVES OG IKKE BARE SKRIVES.
///
/// Compliance-skaermen lovede allerede, at licensteksten «ligger i
/// installationsmappen» - for sherpa-onnx (Apache-2.0) og for krediteringen
/// af NVIDIA (CC-BY-4.0). Der laa ingen licensfiler nogen steder i repoet
/// 03-09-2026. En paastand uden en kontrol bag sig bliver forkert, uden at
/// nogen ser det.
///
/// En gate, der aldrig er set fejle, er heller ikke en gate. Proeverne her
/// bygger en app-mappe, fjerner én fil ad gangen og kontrollerer, at
/// scriptet siger nej - og at det siger ja, naar alt er der.
///
/// Der koeres MOD DET RIGTIGE SCRIPT. En kopi af logikken i C# ville proeve
/// kopien, ikke det, der faktisk koerer ved en udgivelse.
/// </remarks>
public class LicensgateTest
{
    /// <summary>Repoets rod, fundet ved at gå opad fra der, hvor prøven kører.</summary>
    private static string Rod()
    {
        var mappe = new DirectoryInfo(AppContext.BaseDirectory);

        while (mappe is not null)
        {
            if (Directory.Exists(Path.Combine(mappe.FullName, "src", "NoteApp.Desktop")))
                return mappe.FullName;

            mappe = mappe.Parent;
        }

        throw new DirectoryNotFoundException("Fandt ikke repoets rod fra " + AppContext.BaseDirectory);
    }

    private static string Script => Path.Combine(Rod(), "scripts", "tjek-licenser.ps1");

    /// <summary>De filer, gaten kræver. Læst ud af scriptet selv.</summary>
    /// <remarks>
    /// LISTEN SKRIVES IKKE AF HER. Gjorde den det, ville proeven bestaa, mens
    /// scriptet kraevede noget andet - og saa proever den sin egen kopi.
    /// </remarks>
    private static string[] Kraevede()
    {
        var tekst = File.ReadAllText(Script, Encoding.UTF8);

        return System.Text.RegularExpressions.Regex
            .Matches(tekst, @"'((?:licenser|talere)\\[^']+)'")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();
    }

    /// <summary>Kører gaten mod en mappe. Giver exitkoden og det, den skrev.</summary>
    private static (int Kode, string Ud) Koer(string mappe)
    {
        var psi = new ProcessStartInfo("powershell")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(Script);
        psi.ArgumentList.Add("-Mappe");
        psi.ArgumentList.Add(mappe);

        using var p = Process.Start(psi)!;

        // BEGGE STROEMME LAESES SAMTIDIG. Laeses den ene til ende foerst, gaar
        // processen i staa, naar den andens buffer loeber fuld. Den doedvande
        // er ramt foer i det her repo - se WhisperInstall.ReadVersion.
        var ud = p.StandardOutput.ReadToEndAsync();
        var fejl = p.StandardError.ReadToEndAsync();

        if (!p.WaitForExit(60_000))
        {
            try { p.Kill(entireProcessTree: true); } catch (Exception) { }
            throw new TimeoutException("tjek-licenser.ps1 svarede ikke inden for et minut.");
        }

        return (p.ExitCode, ud.Result + fejl.Result);
    }

    /// <summary>Bygger en app-mappe, hvor alt det krævede er på plads.</summary>
    private static void Byg(string mappe, IEnumerable<string> filer)
    {
        foreach (var f in filer)
        {
            var sti = Path.Combine(mappe, f);
            Directory.CreateDirectory(Path.GetDirectoryName(sti)!);
            File.WriteAllText(sti, "prøveindhold", new UTF8Encoding(false));
        }
    }

    private sealed class Sandkasse : IDisposable
    {
        public Sandkasse()
        {
            Sti = Path.Combine(Path.GetTempPath(), "licensgate-" + Guid.NewGuid().ToString("N")[..10]);
            Directory.CreateDirectory(Sti);
        }

        public string Sti { get; }

        public void Dispose()
        {
            try { Directory.Delete(Sti, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // ========================== PROEVERNE ==========================

    [Fact]
    public void Gaten_kraever_baade_licensfiler_og_taleradskillelse()
    {
        var krav = Kraevede();

        Assert.Contains(@"licenser\NOTICE.md", krav);
        Assert.Contains(@"licenser\tekster\sherpa-onnx-Apache-2.0.txt", krav);
        Assert.Contains(@"licenser\tekster\nvidia-titanet-CC-BY-4.0.txt", krav);
        Assert.Contains(@"talere\stemmer.onnx", krav);
        Assert.Contains(@"talere\bin\sherpa-onnx-offline-speaker-diarization.exe", krav);
    }

    [Fact]
    public void En_komplet_mappe_gaar_igennem()
    {
        using var s = new Sandkasse();

        Byg(s.Sti, Kraevede());

        var (kode, ud) = Koer(s.Sti);

        Assert.True(kode == 0, $"Gaten fejlede paa en komplet mappe:\n{ud}");
    }

    [Fact]
    public void En_tom_mappe_bliver_afvist()
    {
        using var s = new Sandkasse();

        var (kode, _) = Koer(s.Sti);

        Assert.NotEqual(0, kode);
    }

    [Fact]
    public void Enhver_manglende_fil_faar_gaten_til_at_fejle()
    {
        var krav = Kraevede();

        Assert.True(krav.Length >= 10, $"Forventede mindst ti krav, fandt {krav.Length}.");

        // ÉN AD GANGEN. En proeve, der kun fjerner den foerste, ville lade en
        // regel skride for alle de andre uden at nogen saa det.
        foreach (var udeladt in krav)
        {
            using var s = new Sandkasse();

            Byg(s.Sti, krav.Where(k => k != udeladt));

            var (kode, ud) = Koer(s.Sti);

            Assert.True(kode != 0,
                $"Gaten gik igennem, selv om «{udeladt}» manglede.");

            Assert.Contains(udeladt, ud);
        }
    }

    // ============ OG FILERNE SKAL FAKTISK LIGGE I REPOET ============

    [Fact]
    public void Alt_det_kraevede_ligger_i_repoet()
    {
        // Gaten kan vaere nok saa skarp: er filerne der ikke, kan der ikke
        // udgives. Det var praecis tilstanden 03-09-2026, hvor skaermen lovede
        // licenstekster, der ikke fandtes.
        foreach (var f in Kraevede())
        {
            var sti = Path.Combine(Rod(), f);

            Assert.True(File.Exists(sti), $"{f} findes ikke i repoet.");
            Assert.True(new FileInfo(sti).Length > 0, $"{f} er tom.");
        }
    }

    [Fact]
    public void Licensteksterne_er_ikke_tomme_skabeloner()
    {
        // En licenstekst paa to linjer er ikke en licenstekst. Tallene er
        // laveste rimelige laengde for de to former, der er i brug.
        var tekster = Path.Combine(Rod(), "licenser", "tekster");

        foreach (var fil in Directory.GetFiles(tekster, "*.txt"))
        {
            var indhold = File.ReadAllText(fil);
            var navn = Path.GetFileName(fil);

            Assert.True(indhold.Length > 900, $"{navn} er kun {indhold.Length} tegn.");

            if (navn.Contains("Apache-2.0"))
                Assert.Contains("Apache License", indhold);
            else if (navn.Contains("CC-BY-4.0"))
                Assert.Contains("Attribution 4.0 International", indhold);
            else if (navn.Contains("MIT"))
                Assert.Contains("WITHOUT WARRANTY OF ANY KIND", indhold);
        }
    }

    [Fact]
    public void NOTICE_krediterer_NVIDIA_som_CC_BY_4_0_kraever()
    {
        var notice = File.ReadAllText(Path.Combine(Rod(), "licenser", "NOTICE.md"), Encoding.UTF8);

        // KREDITERINGEN ER SELVE BETINGELSEN i CC-BY-4.0. Forsvinder den, er
        // modellen ikke laengere lovligt redistribueret.
        Assert.Contains("NVIDIA Corporation", notice);
        Assert.Contains("CC-BY-4.0", notice);

        // Apache-2.0 kraever, at aendringer oplyses. Der er ingen, og det skal
        // staa - ikke udelades.
        Assert.Contains("ikke ændret", notice);
    }
}
