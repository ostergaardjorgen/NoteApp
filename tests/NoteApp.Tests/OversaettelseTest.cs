using System.Text.RegularExpressions;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Er brugerfladen faktisk oversat — eller står der stadig dansk i XAML'en?
///
/// HVORFOR DEN HER PRØVE ER MERE VÆRD END DE ANDRE SPROGPRØVER
///
/// De andre prøver siger, at mekanikken virker. Den her siger, om den er
/// BRUGT. Uden den er «appen er oversat» en påstand, nogen skal tro på — og
/// en påstand, der bliver forkert igen, første gang nogen skriver en ny knap.
///
/// Falder prøven, er svaret ikke at føje teksten til listen nedenfor. Svaret
/// er at lægge nøglen i sprogfilerne og binde teksten.
/// </summary>
public class OversaettelseTest
{
    /// <summary>
    /// De egenskaber, en bruger LÆSER. Der er mange flere strenge i en XAML —
    /// farver, stier, navne på ting — og de skal ikke oversættes.
    /// </summary>
    private static readonly string[] Egenskaber =
    {
        "Text", "Content", "ToolTip", "Header", "AutomationProperties.Name"
    };

    /// <summary>
    /// Det, der ikke er tekst, selv om det står i en Text-egenskab.
    ///
    /// Tegnene fra Segoe MDL2 Assets er ikoner, ikke ord. Det samme gælder
    /// tal, symboler og appens eget navn — «HeyPia» hedder det samme på
    /// ethvert sprog.
    /// </summary>
    private static bool ErIkkeTekst(string vaerdi)
    {
        var t = vaerdi.Trim();

        if (t.Length == 0) return true;

        // Ikoner: &#xE8F4; og lignende.
        if (Regex.IsMatch(t, @"^(&#x[0-9A-Fa-f]{2,6};\s*)+$")) return true;

        // Tal, tegnsætning, symboler og enkeltbogstaver. «1», «·», «❚❚», «▶».
        if (!Regex.IsMatch(t, @"\p{L}\p{L}")) return true;

        // Appens navn og produktnavne, der ikke oversættes.
        string[] navne =
        {
            // «Hey Pia» med mellemrum staar i sidebjaelken - maerket, som det
            // siges, og det samme som vaageordet. Det er stadig appens navn,
            // ikke en tekst, der skal oversaettes.
            "HeyPia", "Hey Pia", "HeyPia optager", "Google", "Google Kalender", "Google Tasks", "Microsoft",
            "Microsoft 365", "Mistral", "whisper.cpp", "Qwen3", "sherpa-onnx",
            "NVIDIA", "OpenAI", "Windows", "Segoe MDL2 Assets", "Consolas",
            "Teams", "Zoom", "iCloud", "OneDrive", "Dropbox", "Nextcloud"
        };

        return navne.Any(n => string.Equals(t, n, StringComparison.Ordinal));
    }

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

        throw new DirectoryNotFoundException(
            "Fandt ikke repoets rod fra " + AppContext.BaseDirectory);
    }

    /// <summary>Alle XAML-filer i skærmprojektet, uden det byggede.</summary>
    private static IEnumerable<string> Xamlfiler()
    {
        var skaerme = Path.Combine(Rod(), "src", "NoteApp.Desktop");

        return Directory.EnumerateFiles(skaerme, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Ingen_haardkodet_tekst_i_XAML()
    {
        var fundet = new List<string>();

        foreach (var fil in Xamlfiler())
        {
            var indhold = File.ReadAllText(fil);
            var kort = Path.GetRelativePath(Rod(), fil);

            foreach (var egenskab in Egenskaber)
            {
                // Egenskaben med en almindelig streng i - altsaa IKKE en
                // binding, som begynder med en tuborgklamme.
                // GRAENSEN FORAN NAVNET ER IKKE PYNT.
                //
                // Uden den rammer «Content=» inde i «SizeToContent=», og saa
                // staar der ni falske fund om ordet «Height». Den fejl blev
                // begaaet baade her og i det vaerktoej, der traak teksterne ud
                // - saa den er skrevet ned begge steder.
                var moenster = @"(?<![A-Za-z0-9_.])" + Regex.Escape(egenskab)
                             + @"=""([^""{][^""]*)""";

                foreach (Match m in Regex.Matches(indhold, moenster))
                {
                    var vaerdi = m.Groups[1].Value;
                    if (ErIkkeTekst(vaerdi)) continue;

                    var linje = indhold.Take(m.Index).Count(c => c == '\n') + 1;

                    fundet.Add($"{kort}:{linje}  {egenskab}=\"{Kort(vaerdi)}\"");
                }
            }
        }

        Assert.True(fundet.Count == 0,
            $"{fundet.Count} tekster i XAML er ikke oversat. Læg nøglen i sprogfilerne "
            + "og brug {{local:Oversat noegle}}:\n\n  " + string.Join("\n  ", fundet.Take(60))
            + (fundet.Count > 60 ? $"\n  … og {fundet.Count - 60} mere" : ""));
    }

    private static string Kort(string s) =>
        s.Length <= 60 ? s : s[..60].TrimEnd() + "…";
}
