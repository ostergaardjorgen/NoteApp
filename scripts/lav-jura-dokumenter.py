# -*- coding: utf-8 -*-
"""
Privatlivspolitikken som Word-dokument - bygget UD FRA HTML-SIDEN.

Hvorfor ikke skrive teksten ind i et Word-script i haanden?

Fordi der saa ville vaere TO kilder til den samme politik. Rettes den ene,
staar den anden tilbage og siger noget andet - og en privatlivspolitik, der
siger to ting, er vaerre end ingen. Naar sider og dokument kommer fra samme
fil, kan de ikke komme fra hinanden.

PowerShell 5.1 laeser en .ps1 UDEN BOM som ANSI, og saa bliver ae, oe og aa
til vroevl inde i dokumentet. Derfor skrives scriptet med utf-8-sig.

Word-stilarterne kaldes ved deres INDBYGGEDE NUMRE (-1 Normal, -2 Overskrift
1, -3 Overskrift 2, -4 Overskrift 3). Dansk Word hedder stilarterne noget
andet end engelsk, og «Heading 1» fejler paa denne maskine.
"""
import io
import os
import re
import subprocess
import tempfile
from html.parser import HTMLParser

JURA = r'C:\NoteApp\jura'

SIDER = [
    (r'C:\NoteApp\web\privatliv.html', os.path.join(JURA, 'Privatlivspolitik for HeyPia.docx')),
    (r'C:\NoteApp\web\privacy.html',   os.path.join(JURA, 'HeyPia Privacy Policy.docx')),
]

BLOKKE = {'h1', 'h2', 'h3', 'p', 'li'}


class Laeser(HTMLParser):
    """HTML ind, en flad liste af blokke ud. Hver blok er (slags, loeb)."""

    def __init__(self):
        super().__init__()
        self.blokke = []
        self.slags = None
        self.loeb = []           # [(tekst, fed)]
        self.fed = 0
        self.udfyld = 0
        self.i_stil = False
        self.tabel = None
        self.raekke = None
        self.celle = None

    # ---- hjaelp
    def _luk(self):
        if self.slags and self.loeb:
            tekst = ''.join(t for t, _ in self.loeb).strip()
            if tekst:
                self.blokke.append((self.slags, self._pak()))
        self.slags, self.loeb = None, []

    def _pak(self):
        """Slaa nabo-loeb med samme fedme sammen og trim enderne."""
        ud = []
        for tekst, fed in self.loeb:
            if ud and ud[-1][1] == fed:
                ud[-1] = (ud[-1][0] + tekst, fed)
            else:
                ud.append((tekst, fed))
        if ud:
            ud[0] = (ud[0][0].lstrip(), ud[0][1])
            ud[-1] = (ud[-1][0].rstrip(), ud[-1][1])
        return [(t, f) for t, f in ud if t]

    # ---- maerker
    def handle_starttag(self, t, attrs):
        a = dict(attrs)

        if t == 'style':
            self.i_stil = True
            return
        if t == 'div' and 'udfyld' in a.get('class', ''):
            # Boksens tekst ligger DIREKTE i div'en, uden <p> om sig. Uden det
            # her blev hele advarslen tabt, og dokumentet saa faerdigt ud, mens
            # det fortav at ejerens navn mangler.
            self._luk()
            self.udfyld += 1
            self.slags = 'udfyld'
        if t in ('strong', 'b'):
            self.fed += 1
        if t in BLOKKE:
            self._luk()
            self.slags = 'udfyld' if (t == 'p' and self.udfyld) else t
        if t == 'table':
            self._luk()
            self.tabel = []
        if t == 'tr' and self.tabel is not None:
            self.raekke = []
        if t in ('td', 'th') and self.raekke is not None:
            self.celle = []

    def handle_endtag(self, t):
        if t == 'style':
            self.i_stil = False
            return
        if t == 'div' and self.udfyld:
            self._luk()
            self.udfyld -= 1
        if t in ('strong', 'b') and self.fed:
            self.fed -= 1
        if t in BLOKKE:
            self._luk()
        if t in ('td', 'th') and self.celle is not None:
            self.raekke.append(re.sub(r'\s+', ' ', ''.join(self.celle)).strip())
            self.celle = None
        if t == 'tr' and self.raekke is not None:
            self.tabel.append(self.raekke)
            self.raekke = None
        if t == 'table' and self.tabel is not None:
            self.blokke.append(('tabel', self.tabel))
            self.tabel = None

    def handle_data(self, d):
        if self.i_stil:
            return
        if self.celle is not None:
            self.celle.append(d)
        elif self.slags:
            self.loeb.append((re.sub(r'\s+', ' ', d), self.fed > 0))


# --------------------------------------------------------------- PowerShell
def ps_tekst(s):
    """Enkeltcitat: kun ' skal fordobles. Ingen $ eller backtick udvides."""
    return "'" + s.replace("'", "''") + "'"


STIL = {'h1': -2, 'h2': -3, 'h3': -4, 'p': -1, 'li': -1, 'udfyld': -1}

linjer = [
    '$ErrorActionPreference = "Stop"',
    '$word = New-Object -ComObject Word.Application',
    '$word.Visible = $false',
]

for kilde, maal in SIDER:
    p = Laeser()
    p.feed(io.open(kilde, encoding='utf-8').read())

    linjer += ['', '# ---- ' + os.path.basename(maal), '$doc = $word.Documents.Add()']

    for slags, indhold in p.blokke:
        if slags == 'tabel':
            r, k = len(indhold), len(indhold[0])
            linjer += [
                '$t = $doc.Tables.Add($doc.Content.Paragraphs.Add().Range, %d, %d)' % (r, k),
                '$t.Borders.Enable = $true',
                '$t.Rows.Item(1).Range.Bold = $true',
            ]
            for i, raekke in enumerate(indhold, 1):
                for j, celle in enumerate(raekke, 1):
                    if celle:
                        linjer.append('$t.Cell(%d,%d).Range.Text = %s' % (i, j, ps_tekst(celle)))
            continue

        tekst = ''.join(t for t, _ in indhold)
        if slags == 'li':
            tekst = '\u2022 ' + tekst

        linjer += [
            '$p = $doc.Content.Paragraphs.Add()',
            '$p.Range.Text = %s' % ps_tekst(tekst),
            '$p.Range.Style = %d' % STIL[slags],
        ]

        if slags == 'li':
            linjer.append('$p.LeftIndent = 18')
        if slags == 'udfyld':
            # Sandfarvet flade. Feltet mangler, og det skal ikke kunne overses
            # af den, der laeser dokumentet i stedet for siden.
            linjer.append('$p.Range.Shading.BackgroundPatternColor = 13823999')

        # Fedt skal saettes BAGEFTER, paa et udsnit af afsnittet. Word kender
        # ikke loeb, foer teksten staar der.
        forskyd = 2 if slags == 'li' else 0
        for tekstbid, fed in indhold:
            if fed:
                linjer.append(
                    '$r = $doc.Range($p.Range.Start + %d, $p.Range.Start + %d); $r.Bold = $true'
                    % (forskyd, forskyd + len(tekstbid)))
            forskyd += len(tekstbid)

        linjer.append('$p.Range.InsertParagraphAfter()')

    linjer += [
        '$doc.SaveAs([ref]%s, [ref]16)' % ps_tekst(maal),
        '$doc.Close()',
    ]

linjer += ['$word.Quit()', 'Write-Output "faerdig"']

os.makedirs(JURA, exist_ok=True)
# Byggescriptet er et mellemled, ikke en leverance. Det hoerer i temp.
UD = os.path.join(tempfile.gettempdir(), 'lav-jura-dokumenter.ps1')
io.open(UD, 'w', encoding='utf-8-sig', newline='\r\n').write('\n'.join(linjer))

print('%d linjer PowerShell, %d dokumenter' % (len(linjer), len(SIDER)))

# Koer det med det samme. Et script, der skal huskes at koere bagefter,
# bliver ikke koert - og saa staar dokumenterne tilbage med gammel tekst.
subprocess.run(['powershell', '-File', UD], check=True)
