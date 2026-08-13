using System.IO.Compression;
using System.Security;
using System.Text;

namespace NoteApp.Core.Documents;

/// <summary>
/// Skriver et OpenDocument-tekstdokument (.odt).
///
/// HVORFOR NETOP DET FORMAT
///
/// OpenDocument er en åben ISO-standard (ISO/IEC 26300). Word kan åbne og
/// gemme det, LibreOffice bruger det som sit eget, og Google Docs kan begge
/// veje. Et referat, der er gemt sådan, kan læses om ti år af et program, vi
/// ikke har valgt endnu — og det kan sendes til en kollega uden at spørge,
/// hvad de har installeret.
///
/// HVORFOR DET SKRIVES I HÅNDEN
///
/// En .odt-fil er en ZIP med et par XML-filer i. Det kan .NET i forvejen.
/// Alternativet var et bibliotek, og et bibliotek er en licens mere, der skal
/// følge med, når appen sælges — for noget, der er halvfems linjer kode.
///
/// FORMATET INDENI
///
/// «mimetype» SKAL være den første post i arkivet og SKAL ligge ukomprimeret.
/// Det er den regel, strenge læsere bruger til at kende filen på, og bryder
/// man den, siger nogle programmer bare «filen er beskadiget».
/// </summary>
public static class OdtWriter
{
    private const string Mime = "application/vnd.oasis.opendocument.text";

    /// <summary>
    /// Skriver dokumentet. <paramref name="markdown"/> forstås som:
    /// «# » og «## » overskrifter, «- » punkter, **fed** indeni, og alt andet
    /// som almindelige afsnit.
    /// </summary>
    public static void Write(string path, string title, string markdown,
                             IReadOnlyList<(string Navn, string Værdi)>? forside = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Skriv til en midlertidig fil og flyt paa plads. Et afbrudt skriv maa
        // ikke efterlade en halv .odt, der ser ud som et faerdigt dokument.
        var midlertidig = path + ".ny";

        using (var fs = new FileStream(midlertidig, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            // Foerste post, ukomprimeret. Raekkefoelgen er ikke kosmetik.
            var mimePost = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var s = new StreamWriter(mimePost.Open(), new UTF8Encoding(false)))
                s.Write(Mime);

            Skriv(zip, "META-INF/manifest.xml", Manifest());
            Skriv(zip, "styles.xml", Styles());
            Skriv(zip, "meta.xml", Meta(title));
            Skriv(zip, "content.xml", Content(title, markdown, forside));
        }

        File.Move(midlertidig, path, overwrite: true);
    }

    private static void Skriv(ZipArchive zip, string navn, string indhold)
    {
        var post = zip.CreateEntry(navn, CompressionLevel.Optimal);
        using var s = new StreamWriter(post.Open(), new UTF8Encoding(false));
        s.Write(indhold);
    }

    private static string Manifest() =>
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2">
          <manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.text"/>
          <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/>
          <manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml"/>
          <manifest:file-entry manifest:full-path="meta.xml" manifest:media-type="text/xml"/>
        </manifest:manifest>
        """;

    private static string Meta(string title) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document-meta xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                              xmlns:dc="http://purl.org/dc/elements/1.1/"
                              xmlns:meta="urn:oasis:names:tc:opendocument:xmlns:meta:1.0"
                              office:version="1.2">
          <office:meta>
            <dc:title>{X(title)}</dc:title>
            <meta:generator>NoteApp</meta:generator>
            <meta:creation-date>{DateTime.Now:yyyy-MM-ddTHH:mm:ss}</meta:creation-date>
          </office:meta>
        </office:document-meta>
        """;

    private static string Styles() =>
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document-styles xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                                xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
                                xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
                                office:version="1.2">
          <office:styles>
            <style:style style:name="Standard" style:family="paragraph">
              <style:text-properties style:font-name="Calibri" fo:font-size="11pt"/>
              <style:paragraph-properties fo:margin-bottom="0.18cm"/>
            </style:style>
            <style:style style:name="Title" style:family="paragraph" style:parent-style-name="Standard">
              <style:text-properties fo:font-size="20pt" fo:font-weight="bold"/>
              <style:paragraph-properties fo:margin-bottom="0.5cm"/>
            </style:style>
            <style:style style:name="Heading_20_1" style:display-name="Heading 1" style:family="paragraph" style:parent-style-name="Standard">
              <style:text-properties fo:font-size="16pt" fo:font-weight="bold"/>
              <style:paragraph-properties fo:margin-top="0.5cm" fo:margin-bottom="0.2cm"/>
            </style:style>
            <style:style style:name="Heading_20_2" style:display-name="Heading 2" style:family="paragraph" style:parent-style-name="Standard">
              <style:text-properties fo:font-size="13pt" fo:font-weight="bold"/>
              <style:paragraph-properties fo:margin-top="0.4cm" fo:margin-bottom="0.15cm"/>
            </style:style>
            <style:style style:name="Kilde" style:family="paragraph" style:parent-style-name="Standard">
              <style:text-properties fo:font-size="9pt" fo:color="#666666"/>
            </style:style>
            <style:style style:name="Fed" style:family="text">
              <style:text-properties fo:font-weight="bold"/>
            </style:style>
          </office:styles>
        </office:document-styles>
        """;

    private static string Content(string title, string markdown,
                                  IReadOnlyList<(string Navn, string Værdi)>? forside)
    {
        var krop = new StringBuilder();

        krop.Append($"<text:p text:style-name=\"Title\">{X(title)}</text:p>");

        // Forsiden: hvor dokumentet kommer fra. Den staar OEVERST og i selve
        // dokumentet, ikke kun i appen — et referat, der er sendt videre, skal
        // stadig kunne svare paa, hvad det er lavet af.
        if (forside is { Count: > 0 })
        {
            foreach (var (navn, værdi) in forside)
                krop.Append($"<text:p text:style-name=\"Kilde\">{X(navn)}: {X(værdi)}</text:p>");

            krop.Append("<text:p text:style-name=\"Kilde\"/>");
        }

        var iListe = false;

        foreach (var rå in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var linje = rå.TrimEnd();

            if (linje.StartsWith("- ") || linje.StartsWith("* "))
            {
                if (!iListe) { krop.Append("<text:list>"); iListe = true; }
                krop.Append($"<text:list-item><text:p text:style-name=\"Standard\">{Indhold(linje[2..])}</text:p></text:list-item>");
                continue;
            }

            if (iListe) { krop.Append("</text:list>"); iListe = false; }

            if (linje.Length == 0) { krop.Append("<text:p text:style-name=\"Standard\"/>"); continue; }

            if (linje.StartsWith("### "))
            { krop.Append($"<text:h text:style-name=\"Heading_20_2\" text:outline-level=\"3\">{Indhold(linje[4..])}</text:h>"); continue; }

            if (linje.StartsWith("## "))
            { krop.Append($"<text:h text:style-name=\"Heading_20_1\" text:outline-level=\"2\">{Indhold(linje[3..])}</text:h>"); continue; }

            if (linje.StartsWith("# "))
            { krop.Append($"<text:h text:style-name=\"Heading_20_1\" text:outline-level=\"1\">{Indhold(linje[2..])}</text:h>"); continue; }

            krop.Append($"<text:p text:style-name=\"Standard\">{Indhold(linje)}</text:p>");
        }

        if (iListe) krop.Append("</text:list>");

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                                     xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
                                     xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
                                     xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
                                     office:version="1.2">
              <office:body><office:text>{krop}</office:text></office:body>
            </office:document-content>
            """;
    }

    /// <summary>**fed** oversættes; resten er ren tekst.</summary>
    private static string Indhold(string tekst)
    {
        var sb = new StringBuilder();
        var dele = tekst.Split("**");

        for (var i = 0; i < dele.Length; i++)
        {
            if (dele[i].Length == 0) continue;

            // Ulige stykker staar mellem to ** og er altsaa fede. Er der et
            // ulige antal **, bliver det sidste stykke almindelig tekst — det
            // er bedre end at taber tegnene.
            var fed = i % 2 == 1 && i < dele.Length - 1 || (i % 2 == 1 && dele.Length % 2 == 1);
            sb.Append(fed
                ? $"<text:span text:style-name=\"Fed\">{X(dele[i])}</text:span>"
                : X(dele[i]));
        }

        return sb.ToString();
    }

    private static string X(string s) => SecurityElement.Escape(s) ?? "";
}
