// Bygger HeyPia-produktark.docx - praecis een A4-side.
// Kor:  node build-produktark.js
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, ImageRun,
  Table, TableRow, TableCell, WidthType, HeightRule, ShadingType,
  BorderStyle, AlignmentType, VerticalAlign, convertMillimetersToTwip,
} = require("docx");

const OUT = "C:\\NoteApp\\Salg\\HeyPia-produktark.docx";
const IMGDIR = "C:\\NoteApp\\Salg\\billeder";

// ---- farver ----
const ACCENT = "00A884";
const ACCENT_DARK = "00705A";
const INK = "1B2A2A";
const MUTED = "5B6C6C";
const TINT = "EAF7F2";
const LINE = "BFE3D8";

// ---- sidemaal ----
const MARGIN_X = 907;   // ca. 16 mm
const MARGIN_Y = 850;   // ca. 15 mm
const PAGE_W = 11906;
const CONTENT_W = PAGE_W - 2 * MARGIN_X; // 10092

const FONT = "Segoe UI";

const NONE = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };
const noBorders = { top: NONE, bottom: NONE, left: NONE, right: NONE };

function txt(text, o = {}) {
  return new TextRun({
    text,
    font: FONT,
    size: o.size || 18,
    bold: !!o.bold,
    italics: !!o.italics,
    color: o.color || INK,
    allCaps: !!o.caps,
    characterSpacing: o.spacing,
  });
}

function p(runs, o = {}) {
  return new Paragraph({
    children: Array.isArray(runs) ? runs : [runs],
    alignment: o.align,
    spacing: { before: o.before || 0, after: o.after === undefined ? 60 : o.after, line: o.line || 250, lineRule: "auto" },
    indent: o.indent,
    border: o.border,
  });
}

function cell(children, o = {}) {
  return new TableCell({
    children,
    width: { size: o.w, type: WidthType.DXA },
    columnSpan: o.span,
    shading: o.fill ? { type: ShadingType.CLEAR, fill: o.fill, color: "auto" } : undefined,
    borders: o.borders || noBorders,
    verticalAlign: o.valign || VerticalAlign.TOP,
    margins: o.margins || { top: 60, bottom: 60, left: 90, right: 90 },
  });
}

function table(rows, widths, o = {}) {
  return new Table({
    rows,
    columnWidths: widths,
    width: { size: widths.reduce((a, b) => a + b, 0), type: WidthType.DXA },
    borders: o.borders || {
      top: NONE, bottom: NONE, left: NONE, right: NONE,
      insideHorizontal: NONE, insideVertical: NONE,
    },
    layout: "fixed",
  });
}

// ---------- PNG-maal ----------
function pngSize(file) {
  const b = fs.readFileSync(file);
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) };
}

// ---------- skaermbillede eller pladsholder ----------
const SGAP = 228;                                       // luft mellem billeder
const SHOT_W_DXA = Math.floor((CONTENT_W - 2 * SGAP) / 3); // kolonnebredde, ca. 56,6 mm
const SHOT_H_DXA = 2720;                                // rammehojde, ca. 48,0 mm
const SHOT_W_PT = SHOT_W_DXA / 20 - 10;
const SHOT_H_PT = SHOT_H_DXA / 20 - 6;
const missing = [];

function shotCell(filename) {
  const full = path.join(IMGDIR, filename);
  if (fs.existsSync(full)) {
    let w = SHOT_W_PT, h = SHOT_H_PT;
    try {
      const s = pngSize(full);
      const scale = Math.min(SHOT_W_PT / s.w, SHOT_H_PT / s.h);
      w = Math.round(s.w * scale); h = Math.round(s.h * scale);
    } catch (e) { /* fald tilbage til rammen */ }
    return cell([
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 0, after: 0, line: 240, lineRule: "auto" },
        children: [new ImageRun({ type: "png", data: fs.readFileSync(full), transformation: { width: w, height: h } })],
      }),
    ], {
      w: SHOT_W_DXA, valign: VerticalAlign.CENTER,
      margins: { top: 30, bottom: 30, left: 30, right: 30 },
      borders: {
        top: { style: BorderStyle.SINGLE, size: 4, color: LINE },
        bottom: { style: BorderStyle.SINGLE, size: 4, color: LINE },
        left: { style: BorderStyle.SINGLE, size: 4, color: LINE },
        right: { style: BorderStyle.SINGLE, size: 4, color: LINE },
      },
    });
  }
  missing.push(filename);
  return cell([
    p(txt("SKÆRMBILLEDE MANGLER", { size: 15, bold: true, color: ACCENT_DARK, caps: true }), { align: AlignmentType.CENTER, after: 50 }),
    p(txt(filename, { size: 14, color: MUTED }), { align: AlignmentType.CENTER, after: 50 }),
    p(txt("Rammen holder pladsen. Læg billedet i mappen og byg arket igen.", { size: 13, italics: true, color: MUTED }), { align: AlignmentType.CENTER, after: 0 }),
  ], {
    w: SHOT_W_DXA, fill: TINT, valign: VerticalAlign.CENTER,
    margins: { top: 40, bottom: 40, left: 60, right: 60 },
    borders: {
      top: { style: BorderStyle.DASHED, size: 6, color: ACCENT },
      bottom: { style: BorderStyle.DASHED, size: 6, color: ACCENT },
      left: { style: BorderStyle.DASHED, size: 6, color: ACCENT },
      right: { style: BorderStyle.DASHED, size: 6, color: ACCENT },
    },
  });
}

// =====================================================================
// 1. Titel
// =====================================================================
const titel = table([
  new TableRow({
    children: [
      cell([
        p([txt("HeyPia", { size: 44, bold: true, color: ACCENT_DARK })], { after: 20, line: 240 }),
        p([txt("Optager lyden af m\u00f8der, webinarer og samtaler \u2014 og g\u00f8r dem til tekst, du kan s\u00f8ge i. P\u00e5 din egen maskine.", { size: 19, color: MUTED })], { after: 0 }),
      ], { w: CONTENT_W }),
    ],
  }),
], [CONTENT_W]);

const streg = new Paragraph({
  children: [txt("", { size: 4 })],
  spacing: { before: 60, after: 140, line: 40, lineRule: "auto" },
  border: { bottom: { style: BorderStyle.SINGLE, size: 12, color: ACCENT } },
});

// =====================================================================
// 2. Hovedbudskab
// =====================================================================
const citat = table([
  new TableRow({
    children: [
      cell([
        p([txt("Hold op med at videooptage dine m\u00f8der. Det, du har brug for, er en transskription af det, der blev sagt.", { size: 24, bold: true, color: ACCENT_DARK })], { after: 70, line: 260 }),
        p([txt("Ingen ser en times video igen. Man leder efter \u00e9n s\u00e6tning: hvad blev der aftalt om prisen, og hvem skulle sende hvad. Den s\u00e6tning st\u00e5r i teksten \u2014 og teksten kan man s\u00f8ge i.", { size: 17, color: INK })], { after: 0 }),
      ], {
        w: CONTENT_W, fill: TINT,
        margins: { top: 220, bottom: 220, left: 260, right: 240 },
        borders: { top: NONE, bottom: NONE, right: NONE, left: { style: BorderStyle.SINGLE, size: 24, color: ACCENT } },
      }),
    ],
  }),
], [CONTENT_W]);

// =====================================================================
// 3. Fire punkter (2 x 2)
// =====================================================================
const GAP = 318;
const COL2 = (CONTENT_W - GAP) / 2; // 5000

function punkt(overskrift, brod) {
  return cell([
    p([txt(overskrift, { size: 19, bold: true, color: ACCENT_DARK })], { after: 40, line: 240 }),
    p([txt(brod, { size: 17, color: INK })], { after: 0, line: 250 }),
  ], { w: COL2, margins: { top: 0, bottom: 0, left: 0, right: 0 } });
}

const spacerRow = (h) => new TableRow({
  height: { value: h, rule: HeightRule.EXACT },
  children: [
    cell([p(txt("", { size: 2 }), { after: 0, line: 20 })], { w: COL2, margins: { top: 0, bottom: 0, left: 0, right: 0 } }),
    cell([p(txt("", { size: 2 }), { after: 0, line: 20 })], { w: GAP, margins: { top: 0, bottom: 0, left: 0, right: 0 } }),
    cell([p(txt("", { size: 2 }), { after: 0, line: 20 })], { w: COL2, margins: { top: 0, bottom: 0, left: 0, right: 0 } }),
  ],
});

const gapCell = cell([p(txt("", { size: 2 }), { after: 0, line: 20 })], { w: GAP, margins: { top: 0, bottom: 0, left: 0, right: 0 } });

const punkter = table([
  new TableRow({
    children: [
      punkt("Lyden bliver p\u00e5 maskinen",
        "Der optages kun lyd \u2014 aldrig video, aldrig sk\u00e6rm, aldrig kamera. Udskrift, s\u00f8gning, opgavefund og den korte opsummering k\u00f8rer lokalt."),
      gapCell,
      punkt("\u00c9n s\u00f8gning p\u00e5 tv\u00e6rs af alt",
        "Transskriptioner, noter og dokumenter i \u00e9n s\u00f8gning, hvor hvert tr\u00e6f peger p\u00e5 stedet i teksten. M\u00e5lt p\u00e5 tyve sp\u00f8rgsm\u00e5l med kendt facit: 90 % p\u00e5 f\u00f8rstepladsen, 100 % i top tre. Der sendes intet for at s\u00f8ge."),
    ],
  }),
  spacerRow(280),
  new TableRow({
    children: [
      punkt("Teksten fylder ingenting",
        "M\u00e5lt 24-08-2026: et kundem\u00f8de p\u00e5 61 minutter gav 223 MB lyd og en udskrift p\u00e5 56,5 KB \u2014 omkring en firetusindedel. F\u00e6rre gigabyte at gemme og flytte peger den rigtige vej."),
      gapCell,
      punkt("Virker uden konto",
        "Ingen oprettelse, intet login for at komme i gang, og den lokale vej koster ikke andet end str\u00f8m. M\u00f8der p\u00e5 pc'en, webinarer, du ikke selv deltager i, og lydfiler fra telefonen ligger samlet \u00e9t sted."),
    ],
  }),
], [COL2, GAP, COL2]);

// =====================================================================
// 4. Datastrom-grafik
// =====================================================================
const D1 = 2750, D2 = 3400, D3 = CONTENT_W - D1 - D2; // 4168

function flowLabel(t, o = {}) {
  return p([txt(t, { size: 17, bold: o.bold, color: o.color || INK })], { after: o.after === undefined ? 0 : o.after, line: 240 });
}

const flowHeadBorders = {
  top: { style: BorderStyle.SINGLE, size: 4, color: LINE },
  bottom: { style: BorderStyle.SINGLE, size: 4, color: LINE },
  left: { style: BorderStyle.SINGLE, size: 4, color: LINE },
  right: { style: BorderStyle.SINGLE, size: 4, color: LINE },
};

const flow = table([
  new TableRow({
    children: [
      cell([p([txt("Hvor tingene ligger \u2014 og hvorn\u00e5r noget forlader maskinen", { size: 17, bold: true, color: "FFFFFF" })], { after: 0, line: 240 })],
        { w: CONTENT_W, span: 3, fill: ACCENT_DARK, margins: { top: 70, bottom: 70, left: 140, right: 140 }, borders: flowHeadBorders }),
    ],
  }),
  new TableRow({
    children: [
      cell([flowLabel("Lyd", { bold: true, color: ACCENT_DARK, after: 20 }), flowLabel("m\u00f8de, webinar eller telefonoptagelse")],
        { w: D1, fill: TINT, valign: VerticalAlign.CENTER, margins: { top: 130, bottom: 130, left: 150, right: 110 }, borders: flowHeadBorders }),
      cell([p([txt("\u2500\u2500\u2500\u2500\u25b6  altid", { size: 17, bold: true, color: ACCENT_DARK })], { after: 0, line: 240 })],
        { w: D2, fill: TINT, valign: VerticalAlign.CENTER, margins: { top: 130, bottom: 130, left: 150, right: 110 }, borders: flowHeadBorders }),
      cell([flowLabel("Bliver p\u00e5 maskinen", { bold: true, color: ACCENT_DARK, after: 20 }), flowLabel("og forlader den aldrig")],
        { w: D3, fill: TINT, valign: VerticalAlign.CENTER, margins: { top: 130, bottom: 130, left: 150, right: 150 }, borders: flowHeadBorders }),
    ],
  }),
  new TableRow({
    children: [
      cell([flowLabel("Den udskrevne tekst", { bold: true, color: ACCENT_DARK, after: 20 }), flowLabel("\u2014 aldrig lyden")],
        { w: D1, valign: VerticalAlign.CENTER, margins: { top: 130, bottom: 130, left: 150, right: 110 }, borders: flowHeadBorders }),
      cell([p([txt("\u254c\u254c\u254c\u254c\u25b7  kun n\u00e5r du selv beder om et f\u00e6rdigt dokument", { size: 17, bold: true, color: ACCENT_DARK })], { after: 0, line: 240 })],
        { w: D2, valign: VerticalAlign.CENTER, margins: { top: 130, bottom: 130, left: 150, right: 110 }, borders: flowHeadBorders }),
      cell([flowLabel("Mistral, Frankrig", { bold: true, color: ACCENT_DARK, after: 20 }), flowLabel("EU-endepunktet. Et valg, du tr\u00e6ffer i situationen \u2014 aldrig en automatik.")],
        { w: D3, valign: VerticalAlign.CENTER, margins: { top: 130, bottom: 130, left: 150, right: 150 }, borders: flowHeadBorders }),
    ],
  }),
], [D1, D2, D3], { borders: { top: NONE, bottom: NONE, left: NONE, right: NONE, insideHorizontal: NONE, insideVertical: NONE } });

// =====================================================================
// 5. Skaermbilleder
// =====================================================================
function capCell(t) {
  return cell([p([txt(t, { size: 16, color: MUTED })], { after: 0, line: 235 })],
    { w: SHOT_W_DXA, margins: { top: 80, bottom: 0, left: 0, right: 40 } });
}
const sgapCell = () => cell([p(txt("", { size: 2 }), { after: 0, line: 20 })], { w: SGAP, margins: { top: 0, bottom: 0, left: 0, right: 0 } });

const SHOT_COLS = [SHOT_W_DXA, SGAP, SHOT_W_DXA, SGAP, SHOT_W_DXA];

// Rammerne og billedteksterne ligger i hver sin tabel, saa den ene raekkes
// rammelinjer ikke forskyder den anden i forhold til margenen.
const billeder = table([
  new TableRow({
    height: { value: SHOT_H_DXA, rule: HeightRule.EXACT },
    children: [shotCell("01-optagelser-udskrift.png"), sgapCell(), shotCell("04-soegning.png"), sgapCell(), shotCell("02-compliance.png")],
  }),
], SHOT_COLS);

const billedtekster = table([
  new TableRow({
    children: [
      capCell("Optagelsen og den f\u00e6rdige udskrift side om side. Teksten kan rettes i appen."),
      sgapCell(),
      capCell("\u00c9n s\u00f8gning p\u00e5 tv\u00e6rs af alt. Hvert tr\u00e6f peger p\u00e5 stedet i teksten."),
      sgapCell(),
      capCell("Kvitteringer for hvert kald: tidspunkt, adresse, model, tegn, pris og SHA-256."),
    ],
  }),
], SHOT_COLS);

// =====================================================================
// 6. Ligeud-boks
// =====================================================================
function bullet(fed, rest) {
  return p([
    txt("\u25aa  ", { size: 17, bold: true, color: ACCENT }),
    txt(fed, { size: 17, bold: true, color: ACCENT_DARK }),
    txt(rest, { size: 17, color: INK }),
  ], { after: 55, line: 245 });
}

const ligeud = table([
  new TableRow({
    children: [
      cell([
        p([txt("Sagt ligeud", { size: 18, bold: true, color: ACCENT_DARK })], { after: 70, line: 240 }),
        bullet("Udskriften er ikke perfekt. ", "M\u00e5lt: 92,3 % p\u00e5 dansk, 90,5 % p\u00e5 engelsk og 83,1 % n\u00e5r dansk og engelsk blandes. Navne og fagord rammes oftest forkert. Teksten kan rettes i appen."),
        bullet("Hver gang tekst forlader maskinen, ligger der en kvittering. ", "Tidspunkt, adresse, model, antal tegn, pris og en SHA-256 af det sendte \u2014 ogs\u00e5 n\u00e5r kaldet fejler."),
        bullet("Fort\u00e6l altid m\u00f8dedeltagerne, at der optages. ", "Netop fordi lyden bliver p\u00e5 maskinen, kan man se de andre i \u00f8jnene og sige, hvor optagelsen ender."),
      ], {
        w: CONTENT_W, fill: "FFFFFF",
        margins: { top: 200, bottom: 190, left: 220, right: 220 },
        borders: {
          top: { style: BorderStyle.SINGLE, size: 4, color: LINE },
          bottom: { style: BorderStyle.SINGLE, size: 4, color: LINE },
          left: { style: BorderStyle.SINGLE, size: 4, color: LINE },
          right: { style: BorderStyle.SINGLE, size: 4, color: LINE },
        },
      }),
    ],
  }),
], [CONTENT_W]);

// =====================================================================
// 7. Bundlinje
// =====================================================================
const bund = new Paragraph({
  children: [txt("HeyPia  \u00b7  Der optages kun lyd \u2014 aldrig video, aldrig sk\u00e6rm, aldrig kamera.  \u00b7  Alle tal p\u00e5 arket er m\u00e5lt.", { size: 14, color: MUTED })],
  spacing: { before: 90, after: 0, line: 240, lineRule: "auto" },
  border: { top: { style: BorderStyle.SINGLE, size: 6, color: LINE } },
});

const gap = (h) => new Paragraph({ children: [txt("", { size: 2 })], spacing: { before: 0, after: 0, line: h, lineRule: "exact" } });

// =====================================================================
const doc = new Document({
  creator: "HeyPia",
  title: "HeyPia \u2013 produktark",
  styles: { default: { document: { run: { font: FONT, size: 17, color: INK } } } },
  sections: [{
    properties: {
      page: {
        size: { width: PAGE_W, height: 16838 },
        margin: { top: MARGIN_Y, bottom: MARGIN_Y, left: MARGIN_X, right: MARGIN_X },
      },
    },
    children: [
      titel, streg,
      citat, gap(340),
      punkter, gap(380),
      flow, gap(380),
      billeder, billedtekster, gap(340),
      ligeud,
      bund,
    ],
  }],
});

Packer.toBuffer(doc).then((buf) => {
  fs.writeFileSync(OUT, buf);
  console.log("SKREVET: " + OUT);
  console.log("MANGLENDE BILLEDER: " + (missing.length ? missing.join(", ") : "ingen"));
});
