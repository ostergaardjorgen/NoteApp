namespace NoteApp.Core;

/// <summary>Én fil i et projekts fundament.</summary>
/// <param name="Sti">Hele stien. Filen røres aldrig — der læses.</param>
/// <param name="Egen">
/// Ligger den i projektets egen mappe? Så er projektet dens hjem, og
/// sikkerhedskopien tager den med. Ellers er den tilknyttet udefra.
/// </param>
/// <param name="Afvist">
/// Hvorfor filen ikke kan læses — eller null, når den kan.
/// </param>
public sealed record Projektfil(string Sti, long Bytes, DateTime Aendret, bool Egen, string? Afvist)
{
    public string Filnavn => Path.GetFileName(Sti);
    public bool Laesbar => Afvist is null;
}

/// <summary>
/// Filerne i et projekts fundament — og hvad der kan læses af dem.
/// </summary>
/// <remarks>
/// ============ DET, DER IKKE KAN LÆSES, SKAL SIGES ============
///
/// En fil, der ligger i mappen og aldrig kommer med i søgningen, er værre end
/// en fejl: alt ser rigtigt ud, og svaret mangler bare. Derfor har hver
/// afvist fil en grund, der kan stå på skærmen.
///
/// TO SLAGS AFVISNING, OG DE HAR HVER SIT SVAR:
///
///   GAMLE OFFICE-FORMATER (.doc, .xls, .ppt) er binære og fra før 2007. At
///   læse dem kræver en tung afhængighed for noget, der i praksis aldrig
///   dukker op. Svaret er: gem den som .docx.
///
///   GOOGLE-STUMPER (.gdoc, .gsheet, .gslides) er slet ikke dokumenter. Google
///   Drev lægger en fil på et par hundrede bytes, der kun indeholder et link;
///   selve teksten ligger på Googles server. Der er bogstavelig talt intet at
///   læse. Svaret er: eksportér til .docx eller .pdf.
///
/// SCANNEDE PDF'ER ER IKKE PÅ LISTEN. En PDF, der er et fotografi af papir,
/// har ingen bogstaver i sig — men det kan først ses, når man har forsøgt at
/// læse den. Den afvisning hører hjemme dér, hvor teksten trækkes ud, ikke
/// her ved filnavnet.
/// </remarks>
public static class Projektkilder
{
    /// <summary>Det, der kan læses tekst ud af.</summary>
    /// <remarks>
    /// OOXML (.docx, .xlsx, .pptx) og ODF (.odt, .ods, .odp) er begge ZIP med
    /// XML indeni og koster det samme. PDF kræver et bibliotek. Resten er ren
    /// tekst.
    /// </remarks>
    public static readonly IReadOnlySet<string> Laesbare = new HashSet<string>(
        new[]
        {
            ".docx", ".xlsx", ".pptx",
            ".odt", ".ods", ".odp",
            ".pdf",
            ".md", ".txt", ".csv",
        },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Det, der ikke kan læses — og hvad man gør ved det.</summary>
    public static readonly IReadOnlyDictionary<string, string> Afvist =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".doc"] = "Gammelt Word-format. Åbn den og gem den som .docx.",
            [".xls"] = "Gammelt Excel-format. Åbn den og gem den som .xlsx.",
            [".ppt"] = "Gammelt PowerPoint-format. Åbn den og gem den som .pptx.",

            [".gdoc"] = "Et link til Google Docs, ikke et dokument. "
                        + "Vælg «Filer → Download → Word» og læg filen her.",
            [".gsheet"] = "Et link til Google Sheets, ikke et regneark. "
                          + "Vælg «Filer → Download → Excel» og læg filen her.",
            [".gslides"] = "Et link til Google Slides, ikke en præsentation. "
                           + "Vælg «Filer → Download → PowerPoint» og læg filen her.",
        };

    /// <summary>
    /// Alle filer i projektets fundament — egne først, så de tilknyttede.
    /// </summary>
    /// <remarks>
    /// DER LÆSES ALDRIG I EN FIL HER. Kun navn, størrelse og dato. En
    /// tilknyttet mappe ligger tit i en cloud-tjeneste, hvor filerne kun
    /// findes som pladsholdere, og at læse dem ville hente hele mappen ned —
    /// bare fordi appen kiggede efter. Samme regel som i Overvaagning.
    /// </remarks>
    public static List<Projektfil> Filer(Projekt p)
    {
        var ud = new List<Projektfil>();

        Saml(p.Dokumentmappe, egen: true, ud);

        foreach (var mappe in p.Mapper)
            Saml(mappe, egen: false, ud);

        return ud;
    }

    private static void Saml(string mappe, bool egen, List<Projektfil> ud)
    {
        try
        {
            if (!Directory.Exists(mappe)) return;

            foreach (var sti in Directory.EnumerateFiles(mappe, "*", SearchOption.AllDirectories))
            {
                var endelse = Path.GetExtension(sti);

                // Alt andet end de kendte endelser springes over UDEN en
                // bemaerkning. En mappe med billeder, lyd og programfiler
                // ville ellers give en liste af afvisninger, ingen har bedt
                // om - og de fortaeller intet.
                var kendt = Laesbare.Contains(endelse) || Afvist.ContainsKey(endelse);
                if (!kendt) continue;

                var info = new FileInfo(sti);

                ud.Add(new Projektfil(
                    sti, info.Length, info.LastWriteTime, egen,
                    Afvist.TryGetValue(endelse, out var grund) ? grund : null));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
