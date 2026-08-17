using System.IO.Compression;
using System.Security;
using System.Text;

namespace NoteApp.Core.Documents;

/// <summary>
/// Skriver et Word-dokument (.docx).
///
/// HVORFOR WORD FREM FOR OPENDOCUMENT
///
/// Her lå en .odt-skriver, og begrundelsen var åbenhed: OpenDocument er en
/// ISO-standard, og Word kan læse den. Det er rigtigt, men det var det
/// forkerte hensyn. Modtageren af et mødereferat åbner det i Word, og et
/// dokument, der er KONVERTERET på vej ind, taber det, konverteringen ikke kan
/// oversætte — typografier, der hedder noget andet, afsnit, der falder
/// anderledes, en indholdsfortegnelse, der ikke virker.
///
/// Ved at skrive Words eget format direkte, og bruge Words EGNE indbyggede
/// typografinavne (Title, Heading1, Heading2, ListParagraph), får dokumentet
/// den formatering, brugerens Word i forvejen er sat op til. Overskrifterne
/// bliver rigtige overskrifter: de kan foldes sammen, de kommer med i
/// navigationsruden, og en indholdsfortegnelse kan indsættes med to klik.
/// Det kunne den .odt-fil, vi lavede før, ikke.
///
/// HVORFOR DET STADIG SKRIVES I HÅNDEN
///
/// En .docx er en ZIP med et par XML-filer i, og det kan .NET i forvejen. Et
/// bibliotek ville være en licens mere, der skal følge med, når appen sælges —
/// for noget, der er et par hundrede linjer.
///
/// FORMATET INDENI
///
/// Fem dele skal være der, og de peger på hinanden:
///
///   [Content_Types].xml        hvad hver del er for en type
///   _rels/.rels                peger på hoveddokumentet
///   word/document.xml          selve teksten
///   word/_rels/document.xml.rels  peger på typografier og punktopstilling
///   word/styles.xml            typografierne
///   word/numbering.xml         punkttegnene
///
/// Mangler en af relationsfilerne, siger Word «filen kan ikke åbnes» uden at
/// sige hvorfor. Der er ingen delvis åbning.
/// </summary>
public static class DocxWriter
{
    private const string NsW = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>
    /// Skriver dokumentet. <paramref name="markdown"/> forstås som:
    /// «# », «## » og «### » overskrifter, «- » punkter, **fed** indeni, og
    /// alt andet som almindelige afsnit.
    /// </summary>
    public static void Write(string path, string title, string markdown,
                             IReadOnlyList<(string Navn, string Værdi)>? forside = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Skriv til en midlertidig fil og flyt paa plads. Et afbrudt skriv maa
        // ikke efterlade en halv fil, der ser ud som et faerdigt dokument.
        var midlertidig = path + ".ny";

        using (var fs = new FileStream(midlertidig, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            Skriv(zip, "[Content_Types].xml", ContentTypes());
            Skriv(zip, "_rels/.rels", Rels());
            Skriv(zip, "docProps/core.xml", Core(title));
            Skriv(zip, "word/_rels/document.xml.rels", DokumentRels());
            Skriv(zip, "word/settings.xml", Settings());
            Skriv(zip, "word/styles.xml", Styles());
            Skriv(zip, "word/numbering.xml", Numbering());
            Skriv(zip, "word/document.xml", Document(title, markdown, forside));
        }

        File.Move(midlertidig, path, overwrite: true);
    }

    private static void Skriv(ZipArchive zip, string navn, string indhold)
    {
        var post = zip.CreateEntry(navn, CompressionLevel.Optimal);
        using var s = new StreamWriter(post.Open(), new UTF8Encoding(false));
        s.Write(indhold);
    }

    private static string ContentTypes() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
          <Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
          <Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>
          <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
        </Types>
        """;

    private static string Rels() => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="{NsRel}/officeDocument" Target="word/document.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
        </Relationships>
        """;

    private static string DokumentRels() => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="{NsRel}/styles" Target="styles.xml"/>
          <Relationship Id="rId2" Type="{NsRel}/numbering" Target="numbering.xml"/>
          <Relationship Id="rId3" Type="{NsRel}/settings" Target="settings.xml"/>
        </Relationships>
        """;

    /// <summary>
    /// Indstillingerne — og dermed hvilken Word-udgave dokumentet ER.
    ///
    /// UDEN DENNE FIL SPØRGER WORD, OM DU VIL OPDATERE FILFORMATET.
    ///
    /// Mangler settings.xml, antager Word kompatibilitetstilstand 12 — altså
    /// Word 2007. Dokumentet åbner fint, men i «kompatibilitetstilstand», og
    /// første gang man gemmer, bliver man spurgt, om det skal opdateres. Det
    /// er et spørgsmål, ingen kan svare rigtigt på, om et referat, de lige har
    /// fået lavet — og det ser ud, som om filen er gammel eller forkert.
    ///
    /// compatibilityMode 15 er Word 2013 og frem. Så er den ny fra begyndelsen,
    /// og der bliver ikke spurgt om noget.
    /// </summary>
    private static string Settings() => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:settings xmlns:w="{NsW}">
          <w:compat>
            <w:compatSetting w:name="compatibilityMode"
                             w:uri="http://schemas.microsoft.com/office/word"
                             w:val="15"/>
          </w:compat>
        </w:settings>
        """;

    private static string Core(string title) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
                           xmlns:dc="http://purl.org/dc/elements/1.1/"
                           xmlns:dcterms="http://purl.org/dc/terms/"
                           xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <dc:title>{X(title)}</dc:title>
          <dc:creator>NoteApp</dc:creator>
          <cp:lastModifiedBy>NoteApp</cp:lastModifiedBy>
          <dcterms:created xsi:type="dcterms:W3CDTF">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created>
        </cp:coreProperties>
        """;

    /// <summary>
    /// Typografierne.
    ///
    /// Navnene er Words EGNE (Title, Heading1, Heading2, Heading3,
    /// ListParagraph). Det er hele pointen: bruger man sine egne navne, får man
    /// sin egen formatering og et dokument, der ser fremmed ud i Word. Bruger
    /// man Words, arver dokumentet det tema, modtageren har — og
    /// navigationsruden og indholdsfortegnelsen virker, fordi Word genkender
    /// overskrifterne som overskrifter.
    ///
    /// «Kilde» er vores egen og skal være det: der findes ikke en indbygget
    /// typografi for «hvor kommer det her dokument fra».
    /// </summary>
    private static string Styles() => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="{NsW}">
          <w:docDefaults>
            <w:rPrDefault><w:rPr>
              <w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>
              <w:sz w:val="22"/><w:szCs w:val="22"/>
              <w:lang w:val="da-DK"/>
            </w:rPr></w:rPrDefault>
            <w:pPrDefault><w:pPr>
              <w:spacing w:after="160" w:line="259" w:lineRule="auto"/>
            </w:pPr></w:pPrDefault>
          </w:docDefaults>

          <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
            <w:name w:val="Normal"/><w:qFormat/>
          </w:style>

          <w:style w:type="paragraph" w:styleId="Title">
            <w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:qFormat/>
            <w:pPr><w:spacing w:after="240"/><w:contextualSpacing/></w:pPr>
            <w:rPr><w:sz w:val="56"/><w:szCs w:val="56"/><w:color w:val="1F3864"/></w:rPr>
          </w:style>

          <w:style w:type="paragraph" w:styleId="Heading1">
            <w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/>
            <w:qFormat/><w:uiPriority w:val="9"/>
            <w:pPr><w:outlineLvl w:val="0"/><w:keepNext/><w:spacing w:before="360" w:after="120"/></w:pPr>
            <w:rPr><w:b/><w:sz w:val="32"/><w:szCs w:val="32"/><w:color w:val="1F3864"/></w:rPr>
          </w:style>

          <w:style w:type="paragraph" w:styleId="Heading2">
            <w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/>
            <w:qFormat/><w:uiPriority w:val="9"/>
            <w:pPr><w:outlineLvl w:val="1"/><w:keepNext/><w:spacing w:before="280" w:after="100"/></w:pPr>
            <w:rPr><w:b/><w:sz w:val="26"/><w:szCs w:val="26"/><w:color w:val="2E5496"/></w:rPr>
          </w:style>

          <w:style w:type="paragraph" w:styleId="Heading3">
            <w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/>
            <w:qFormat/><w:uiPriority w:val="9"/>
            <w:pPr><w:outlineLvl w:val="2"/><w:keepNext/><w:spacing w:before="240" w:after="80"/></w:pPr>
            <w:rPr><w:b/><w:sz w:val="24"/><w:szCs w:val="24"/><w:color w:val="2E5496"/></w:rPr>
          </w:style>

          <w:style w:type="paragraph" w:styleId="ListParagraph">
            <w:name w:val="List Paragraph"/><w:basedOn w:val="Normal"/><w:qFormat/>
            <w:uiPriority w:val="34"/>
            <w:pPr><w:ind w:left="720"/><w:contextualSpacing/><w:spacing w:after="60"/></w:pPr>
          </w:style>

          <!-- Forsidelinjerne staar taet sammen. De er een blok oplysninger,
               ikke fem afsnit, og med almindelig afstand fyldte de en
               tredjedel af foerste side. contextualSpacing slaar afstanden fra
               MELLEM linjer af samme slags, men beholder den efter den sidste
               - saa blokken haenger sammen og slipper teksten under sig. -->
          <w:style w:type="paragraph" w:styleId="Kilde">
            <w:name w:val="Kilde"/><w:basedOn w:val="Normal"/>
            <w:pPr><w:spacing w:after="200"/><w:contextualSpacing/></w:pPr>
            <w:rPr><w:sz w:val="18"/><w:szCs w:val="18"/><w:color w:val="666666"/></w:rPr>
          </w:style>
        </w:styles>
        """;

    /// <summary>
    /// Punktopstillingen. Uden denne fil bliver «- » til et bindestregs-tegn
    /// i almindelig tekst frem for et rigtigt punkt, man kan rykke ind og ud.
    /// </summary>
    private static string Numbering() => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:numbering xmlns:w="{NsW}">
          <w:abstractNum w:abstractNumId="0">
            <w:multiLevelType w:val="hybridMultilevel"/>
            <w:lvl w:ilvl="0">
              <w:start w:val="1"/>
              <w:numFmt w:val="bullet"/>
              <w:lvlText w:val="&#xF0B7;"/>
              <w:lvlJc w:val="left"/>
              <w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr>
              <w:rPr><w:rFonts w:ascii="Symbol" w:hAnsi="Symbol" w:hint="default"/></w:rPr>
            </w:lvl>
          </w:abstractNum>
          <w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>
        </w:numbering>
        """;

    private static string Document(string title, string markdown,
                                   IReadOnlyList<(string Navn, string Værdi)>? forside)
    {
        var krop = new StringBuilder();

        krop.Append(Afsnit("Title", Løb(title)));

        // Forsiden: hvor dokumentet kommer fra. Den staar OEVERST og i selve
        // dokumentet, ikke kun i appen — et referat, der er sendt videre, skal
        // stadig kunne svare paa, hvad det er lavet af.
        if (forside is { Count: > 0 })
            foreach (var (navn, værdi) in forside)
                krop.Append(Afsnit("Kilde", Løb($"{navn}: {værdi}")));

        // TOMME LINJER BLIVER IKKE TIL TOMME AFSNIT.
        //
        // Det gjorde de, og resultatet var et dokument, der var alt for luftigt.
        // Markdown bruger den tomme linje som skilletegn mellem afsnit; Word
        // bruger afstand EFTER hvert afsnit. Oversaetter man den ene til den
        // anden, faar man begge dele — et helt tomt afsnit oveni afstanden,
        // altsaa dobbelt luft hele vejen ned.
        //
        // Den tomme linje har gjort sit arbejde, naar den har afsluttet
        // afsnittet. Den skal ikke med over.
        //
        // Vandrette streger (---) ryger ogsaa. En sprogmodel saetter dem som
        // afsnitsskel i markdown, og i et Word-dokument med rigtige
        // overskrifter er de stoej.
        foreach (var rå in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var linje = rå.Trim();

            if (linje.Length == 0) continue;
            if (linje.All(c => c == '-' || c == '*' || c == '_') && linje.Length >= 3) continue;

            if (linje.StartsWith("- ") || linje.StartsWith("* "))
            {
                krop.Append(Punkt(Indhold(linje[2..])));
                continue;
            }

            if (linje.StartsWith("### ")) { krop.Append(Afsnit("Heading3", Indhold(linje[4..]))); continue; }
            if (linje.StartsWith("## ")) { krop.Append(Afsnit("Heading2", Indhold(linje[3..]))); continue; }
            if (linje.StartsWith("# ")) { krop.Append(Afsnit("Heading1", Indhold(linje[2..]))); continue; }

            krop.Append(Afsnit("Normal", Indhold(linje)));
        }

        // sectPr til sidst: A4 med to en halv centimeters margener. Uden den
        // bruger Word sin egen standard, som paa en dansk maskine kan vaere
        // Letter — og saa falder sidebrud et andet sted end forventet.
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="{NsW}">
              <w:body>{krop}
                <w:sectPr>
                  <w:pgSz w:w="11906" w:h="16838"/>
                  <w:pgMar w:top="1418" w:right="1418" w:bottom="1418" w:left="1418"
                           w:header="709" w:footer="709" w:gutter="0"/>
                </w:sectPr>
              </w:body>
            </w:document>
            """;
    }

    private static string Afsnit(string typografi, string løb) =>
        $"<w:p><w:pPr><w:pStyle w:val=\"{typografi}\"/></w:pPr>{løb}</w:p>";

    private static string Punkt(string løb) =>
        "<w:p><w:pPr><w:pStyle w:val=\"ListParagraph\"/>" +
        "<w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr>" +
        løb + "</w:p>";

    /// <summary>
    /// Ét stykke tekst. <c>xml:space="preserve"</c> skal med: uden den æder
    /// Word mellemrum i begyndelsen og slutningen af hvert stykke, og så
    /// klistrer ordene omkring **fed** sammen.
    /// </summary>
    private static string Løb(string tekst, bool fed = false) =>
        tekst.Length == 0
            ? ""
            : $"<w:r>{(fed ? "<w:rPr><w:b/></w:rPr>" : "")}" +
              $"<w:t xml:space=\"preserve\">{X(tekst)}</w:t></w:r>";

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
            // er bedre end at tabe tegnene.
            var fed = i % 2 == 1 && i < dele.Length - 1 || (i % 2 == 1 && dele.Length % 2 == 1);
            sb.Append(Løb(dele[i], fed));
        }

        return sb.ToString();
    }

    private static string X(string s) => SecurityElement.Escape(s) ?? "";
}
