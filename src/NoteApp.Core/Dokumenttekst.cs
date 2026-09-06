using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace NoteApp.Core;

/// <summary>Teksten ud af ét dokument — og en bemærkning, hvis der næsten ingen var.</summary>
/// <param name="Advarsel">
/// Null, når alt gik godt. Ellers en sætning, der kan stå på skærmen.
/// </param>
public sealed record Udtraek(string Tekst, string? Advarsel);

/// <summary>
/// Læser teksten ud af de dokumenter, et projekt bygger på.
/// </summary>
/// <remarks>
/// ============ INTET OFFICE, INGEN COM ============
///
/// .docx, .xlsx og .pptx er ZIP-filer med XML indeni. Det samme er ODF —
/// .odt, .ods og .odp. De læses direkte, og det har tre følger, der alle er
/// vigtige:
///
///   Office behøver ikke være installeret. En pc uden Word kan læse .docx.
///   Der startes ikke et program pr. fil. COM ville åbne Word for hver af
///   dem, og på fem hundrede sider er det ikke en detalje.
///   Det virker på en maskine uden skærm — altså i en prøve.
///
/// PDF ER DEN ENESTE, DER KRÆVER ET BIBLIOTEK. PdfPig, Apache-2.0, ren .NET
/// og uden indfødte afhængigheder. Licensteksten ligger i `licenser\`.
///
/// ============ DER GÆTTES IKKE PÅ FORMATET ============
///
/// Endelsen afgør. En fil, der hedder .docx og ikke er en ZIP, giver en
/// advarsel frem for at blive læst som noget andet — en tekst, der er
/// halvvejs rigtig, er værre end ingen tekst, fordi den ikke kan ses.
/// </remarks>
public static class Dokumenttekst
{
    /// <summary>
    /// Så få tegn i en PDF med sider betyder, at den er scannet.
    /// </summary>
    /// <remarks>
    /// EN PDF, DER ER ET FOTOGRAFI AF PAPIR, har ingen bogstaver i sig — kun
    /// pixels. Det kan først ses, når man har forsøgt at læse den, og derfor
    /// står grænsen her og ikke ved filnavnet.
    ///
    /// Hundrede tegn pr. side er lavt sat med vilje. En rigtig tekstside har
    /// tusinder; en scannet side har nul, bortset fra et sidetal, en
    /// scannerens stempel eller et vandmærke, der tilfældigvis er tekst.
    /// </remarks>
    private const int TegnPrSide = 100;

    public static Udtraek Laes(string sti)
    {
        try
        {
            return Path.GetExtension(sti).ToLowerInvariant() switch
            {
                ".txt" or ".md" or ".csv" => new(File.ReadAllText(sti, Encoding.UTF8), null),

                ".docx" => new(Ooxml(sti, "word/document.xml", "t"), null),
                ".pptx" => new(Ooxml(sti, "ppt/slides/", "t"), null),
                ".xlsx" => new(Ooxml(sti, "xl/", "t"), null),

                ".odt" or ".ods" or ".odp" => new(Odf(sti), null),

                ".pdf" => Pdf(sti),

                _ => new("", "Formatet kan ikke læses."),
            };
        }
        catch (InvalidDataException)
        {
            return new("", "Filen kunne ikke pakkes op. Den er nok i stykker.");
        }
        catch (IOException)
        {
            return new("", "Filen kunne ikke læses. Den er måske åben i et andet program.");
        }
        catch (UnauthorizedAccessException)
        {
            return new("", "Der er ikke adgang til filen.");
        }
    }

    // ------------------------------------------------------------ OOXML

    /// <summary>
    /// Teksten ud af en Office-fil.
    /// </summary>
    /// <param name="del">
    /// Enten én bestemt del («word/document.xml») eller et præfiks
    /// («ppt/slides/»), når teksten er spredt over flere dele.
    /// </param>
    /// <param name="navn">
    /// Elementets lokale navn. Det er `w:t`, `a:t` og `t` i de tre formater —
    /// altså «t» i dem alle, når navnerummet skæres væk. DET ER MED VILJE, at
    /// der ses på det lokale navn: navnerummet skifter mellem udgaver af
    /// Office, og en fil fra 2010 skal kunne læses.
    /// </param>
    private static string Ooxml(string sti, string del, string navn)
    {
        using var zip = ZipFile.OpenRead(sti);

        var dele = zip.Entries
            .Where(e => e.FullName.StartsWith(del, StringComparison.OrdinalIgnoreCase)
                        && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();

        foreach (var e in dele)
        {
            using var stroem = e.Open();

            XDocument doc;
            try { doc = XDocument.Load(stroem); }
            catch (System.Xml.XmlException) { continue; }

            foreach (var el in doc.Descendants().Where(x => x.Name.LocalName == navn))
            {
                sb.Append(el.Value);
                sb.Append(' ');
            }

            sb.AppendLine();
        }

        return Ryd(sb.ToString());
    }

    // ------------------------------------------------------------ ODF

    private static string Odf(string sti)
    {
        using var zip = ZipFile.OpenRead(sti);

        var indhold = zip.GetEntry("content.xml");
        if (indhold is null) return "";

        using var stroem = indhold.Open();

        XDocument doc;
        try { doc = XDocument.Load(stroem); }
        catch (System.Xml.XmlException) { return ""; }

        var sb = new StringBuilder();

        // ODF har teksten i «text:p» og «text:h». Igen paa lokalt navn, saa
        // navnerummet ikke afgoer det.
        foreach (var el in doc.Descendants()
                     .Where(x => x.Name.LocalName is "p" or "h"))
        {
            sb.AppendLine(el.Value);
        }

        return Ryd(sb.ToString());
    }

    // ------------------------------------------------------------ PDF

    private static Udtraek Pdf(string sti)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(sti);

        var sb = new StringBuilder();
        var sider = 0;

        foreach (var side in doc.GetPages())
        {
            // ORD FOR ORD, IKKE side.Text.
            //
            // En PDF gemmer ikke mellemrum; den gemmer bogstaver med hver sin
            // plads paa siden. side.Text limer dem sammen, og saa staar der
            // «StudieplanProjektDatateknikeruddannelsen» - én lang streng, som
            // ingen soegning kan finde et ord i.
            //
            // Maalt 06-09-2026 paa en PDF, appen selv havde skrevet: teksten
            // var der, og den kunne ikke soeges i. GetWords grupperer
            // bogstaverne efter deres placering, og saa er der ord igen.
            sb.AppendLine(string.Join(' ', side.GetWords().Select(o => o.Text)));
            sider++;
        }

        var tekst = Ryd(sb.ToString());

        if (sider > 0 && tekst.Length < sider * TegnPrSide)
        {
            return new(tekst,
                "PDF'en ser ud til at være scannet — der er billeder, men næsten ingen "
                + "tekst. Den kan ikke søges i. Scan den med tekstgenkendelse, eller "
                + "brug telefonens scanner, som laver tekstlaget selv.");
        }

        return new(tekst, null);
    }

    // ------------------------------------------------------------ oprydning

    /// <summary>
    /// Fjerner de tomme linjer og de dobbelte mellemrum.
    /// </summary>
    /// <remarks>
    /// Et regneark giver én celle pr. element, og en præsentation giver ét
    /// tekstfelt pr. linje. Uden det her ville halvdelen af teksten være
    /// mellemrum — og søgningen leder efter ord, der står tæt på hinanden.
    /// Luft, der ikke stod i dokumentet, ville skubbe dem fra hinanden.
    /// </remarks>
    private static string Ryd(string raa)
    {
        var linjer = raa.Split('\n')
            .Select(l => string.Join(' ', l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(l => l.Length > 0);

        return string.Join('\n', linjer);
    }
}

/// <summary>
/// Teksten fra et dokument, gemt så den kun trækkes ud én gang.
/// </summary>
/// <remarks>
/// ============ HVORFOR DEN ER NØDVENDIG ============
///
/// Søgningen læser sine kilder fra filerne ved hvert opslag. Det går fint for
/// udskrifter og noter, som allerede ER tekst. En PDF på fire hundrede sider
/// skal pakkes op og tolkes, og det må ikke ske, hver gang nogen skriver et
/// ord i søgefeltet.
///
/// NØGLEN ER STI, STØRRELSE OG DATO. Samme nøgle som overvågningen bruger til
/// «hvad er set før», og af samme grund: rettes filen, er den ikke den samme
/// tekst mere, og så skal den læses igen. Kun stien ville betyde, at en
/// rettelse aldrig blev opdaget.
///
/// CACHEN LIGGER FÆLLES og ikke pr. projekt. To projekter, der peger på den
/// samme udleverede mappe, skal ikke pakke de samme filer op hver for sig.
/// </remarks>
public static class Tekstcache
{
    public static string Mappe => Path.Combine(UserDataPaths.Root, "tekstcache");

    /// <summary>Teksten — fra cachen, hvis den er der, ellers trukket ud nu.</summary>
    public static Udtraek Hent(string sti)
    {
        string noegle;

        try
        {
            var info = new FileInfo(sti);
            if (!info.Exists) return new("", "Filen findes ikke længere.");

            noegle = Noegle(sti, info.Length, info.LastWriteTimeUtc);
        }
        catch (IOException)
        {
            return new("", "Filen kunne ikke læses.");
        }

        var fil = Path.Combine(Mappe, noegle + ".txt");
        var advarsel = Path.Combine(Mappe, noegle + ".besked");

        try
        {
            if (File.Exists(fil))
            {
                return new(File.ReadAllText(fil, Encoding.UTF8),
                    File.Exists(advarsel) ? File.ReadAllText(advarsel, Encoding.UTF8) : null);
            }
        }
        catch (IOException)
        {
            // En cache, der ikke kan laeses, er ikke en fejl. Der laeses forfra.
        }

        var udtraek = Dokumenttekst.Laes(sti);

        try
        {
            Directory.CreateDirectory(Mappe);
            File.WriteAllText(fil, udtraek.Tekst, new UTF8Encoding(false));

            if (udtraek.Advarsel is not null)
                File.WriteAllText(advarsel, udtraek.Advarsel, new UTF8Encoding(false));
        }
        catch (IOException)
        {
            // Kan cachen ikke skrives, er teksten stadig rigtig. Den koster
            // bare det samme naeste gang.
        }

        return udtraek;
    }

    private static string Noegle(string sti, long bytes, DateTime aendret)
    {
        var raa = $"{sti.ToLowerInvariant()}|{bytes}|{aendret.Ticks}";
        var sum = SHA256.HashData(Encoding.UTF8.GetBytes(raa));

        return Convert.ToHexString(sum)[..32].ToLowerInvariant();
    }

    /// <summary>Rydder cachen. Teksten trækkes ud igen ved næste opslag.</summary>
    public static void Ryd()
    {
        try
        {
            if (Directory.Exists(Mappe)) Directory.Delete(Mappe, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
