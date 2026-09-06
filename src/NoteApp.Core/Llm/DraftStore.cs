using System.Reflection;
using System.Text;

namespace NoteApp.Core.Llm;

/// <summary>
/// Gemmer de tekster, sprogmodellen laver — og hvad der lavede dem.
///
/// Proveniens står øverst i hver fil: model, kvantisering, motor og skabelon.
/// Det er samme princip som engine_id i ordbogen, hvor det allerede gælder, at
/// proveniens er proveniens — den fortæller, hvad der producerede en tekst, og
/// er aldrig en betingelse for om teksten må bruges.
///
/// Uden den kan man ikke svare på, hvorfor to referater af samme møde ser
/// forskellige ud. Med den kan man sammenligne modeller på sit eget materiale
/// frem for på en benchmark.
/// </summary>
public static class DraftStore
{
    public static string DirectoryFor(string meetingDir) => Path.Combine(meetingDir, "udkast");

    /// <summary>
    /// Gemmer et udkast med proveniens.
    ///
    /// <paramref name="motor"/> skal siges, når det ikke er llama.cpp. Et udkast
    /// lavet i skyen, der har «motor: llama.cpp» i hovedet, er en forkert
    /// oplysning i den ene fil, der findes for at kunne svare på, hvad der
    /// lavede teksten — og en forkert proveniens er værre end ingen.
    /// </summary>
    public static string Save(
        string meetingDir,
        PromptTemplate template,
        LlmResult result,
        string? engineVersion = null,
        string motor = "llama.cpp")
    {
        var mappe = DirectoryFor(meetingDir);
        Directory.CreateDirectory(mappe);

        // SEKUNDER MED. Uden dem havde to udkast af samme moede med samme model
        // inden for det samme MINUT noejagtig samme filnavn, og det andet
        // overskrev det foerste uden et ord.
        //
        // Opdaget 18-08-2026 under en maaling med tre koersler i traek: der kom
        // kun to filer ud af tre, og den tredje maaling saa ud som om den var
        // fejlet. Den var koert - resultatet var bare vaek.
        //
        // Det er ikke en teoretisk kant. En koersel i skyen tager tyve sekunder,
        // saa to i samme minut er det normale, naar man sammenligner noget.
        var navn = $"{Filnavn(template.Name)}_{Filnavn(Path.GetFileNameWithoutExtension(result.ModelFile))}" +
                   $"_{DateTime.Now:yyyy-MM-dd_HHmmss}.md";
        var sti = Path.Combine(mappe, navn);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"skabelon: {template.Name}");
        sb.AppendLine($"model: {result.ModelFile}");
        sb.AppendLine($"motor: {motor}{(engineVersion is null ? "" : " " + engineVersion)}");
        sb.AppendLine($"temperatur: {template.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        sb.AppendLine($"lavet: {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"tid: {result.Elapsed.TotalSeconds:0.0} sek");
        sb.AppendLine($"tokens: {result.PromptTokens} ind, {result.ResponseTokens} ud");
        sb.AppendLine($"hastighed: {result.TokensPerSecond:0.0} tokens/sek");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(result.Text);

        File.WriteAllText(sti, sb.ToString(), new UTF8Encoding(false));
        return sti;
    }

    public static IReadOnlyList<string> List(string meetingDir)
    {
        var mappe = DirectoryFor(meetingDir);
        if (!Directory.Exists(mappe)) return Array.Empty<string>();

        return Directory.GetFiles(mappe, "*.md")
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();
    }

    /// <summary>
    /// Lægger de indbyggede skabeloner i datamappen, hvis de ikke er der.
    /// De skal ligge som filer, brugeren kan rette — ikke inde i programmet.
    /// En skabelon, brugeren har ændret, overskrives aldrig.
    /// </summary>
    /// <remarks>
    /// ============ TO BIBLIOTEKER, TO MAPPER ============
    ///
    /// En mødetype skriver et dokument ud af én optagelse; et projektoutput
    /// skriver et ud af et helt projekt. De to kan ikke bruges i hinandens
    /// sted — et projektoutput beder om <c>{{kilder}}</c>, og dem er der ingen
    /// af, når man står med en udskrift.
    ///
    /// Ressourcenavnet bærer mappen med: «...Llm.skabeloner.tilbud.md» er en
    /// mødetype, «...Llm.skabeloner.projekt.tilbud.md» er et projektoutput.
    /// Derfor deles der på den sidste punktum-adskilte del: alt før filnavnet
    /// er mappen.
    ///
    /// EN NY SKABELON I EN NY UDGAVE LÆGGES OGSÅ IND. Der ses kun efter, om
    /// filen findes — ikke om mappen er tom. Ellers ville en bruger, der har
    /// haft appen i et halvt år, aldrig få en skabelon, der kom til
    /// undervejs.
    /// </remarks>
    public static int SeedTemplates()
    {
        var antal = 0;
        var asm = Assembly.GetExecutingAssembly();
        const string maerke = ".skabeloner.";

        foreach (var res in asm.GetManifestResourceNames().Where(n => n.Contains(maerke)))
        {
            var rest = res[(res.IndexOf(maerke, StringComparison.Ordinal) + maerke.Length)..];

            // «tilbud.md» er en moedetype; «projekt.tilbud.md» er et
            // projektoutput. Undermappen staar foran filnavnet.
            var skille = rest.LastIndexOf('.', rest.LastIndexOf('.') - 1);

            var mappe = skille < 0
                ? PromptTemplate.Skabelonmappe(Skabelonslags.Moedetype)
                : Path.Combine(PromptTemplate.Skabelonmappe(Skabelonslags.Moedetype),
                               rest[..skille].Replace('.', Path.DirectorySeparatorChar));

            var filnavn = skille < 0 ? rest : rest[(skille + 1)..];

            Directory.CreateDirectory(mappe);

            var sti = Path.Combine(mappe, filnavn);
            if (File.Exists(sti)) continue;

            using var s = asm.GetManifestResourceStream(res);
            if (s is null) continue;
            using var r = new StreamReader(s, Encoding.UTF8);
            File.WriteAllText(sti, r.ReadToEnd(), new UTF8Encoding(false));
            antal++;
        }

        return antal;
    }

    private static string Filnavn(string s)
    {
        var rent = new string(s.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        return string.Join('-', rent.Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
