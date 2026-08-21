# Retter sammenstillingen mellem klip og facit, og maaler saa enighedsreglen.
#
# HVORFOR DER SKAL RETTES
#
# Opdelingen paa stilhed ramte 50 klip, men ikke de RIGTIGE 50: een saetning
# blev delt i to tidligt, og saa er alt derefter forskudt med een. Maalingen
# sammenlignede klip 14 med saetning 14, mens klippet indeholdt saetning 13.
#
# Klippene ligger i den rigtige raekkefoelge, og de frie udskrifter er gode.
# Saa kan de stilles paa plads med en monoton sammenstilling: find den
# parring, der samlet ligner mest, og som ikke bytter om paa raekkefoelgen.
#
# Det er en maalefejl, ikke en fejl i det, der maales. I appen findes
# problemet ikke - dér siger man een kommando, og appen svarer paa den.

import csv, io, re, sys, unicodedata

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

CSV = r"C:\AppNoter\Optagelser\2026-08-21_07-14\kommandoer\resultat.csv"


def rens(s):
    if not s:
        return []
    s = unicodedata.normalize("NFC", s).lower()
    s = re.sub(r"[^\wæøå ]", " ", s)
    return [o for o in s.split() if o]


def wer(facit, hoert):
    a, b = rens(facit), rens(hoert)
    if not a:
        return 1.0
    d = [[0] * (len(b) + 1) for _ in range(len(a) + 1)]
    for i in range(len(a) + 1):
        d[i][0] = i
    for j in range(len(b) + 1):
        d[0][j] = j
    for i in range(1, len(a) + 1):
        for j in range(1, len(b) + 1):
            p = 0 if a[i - 1] == b[j - 1] else 1
            d[i][j] = min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + p)
    return d[len(a)][len(b)] / len(a)


raekker = list(csv.DictReader(open(CSV, encoding="utf-8-sig")))
klip = [(int(r["Nr"]), r["Fri"], r["Bundet"]) for r in raekker]
klip.sort()

facit = [(int(r["Nr"]), r["Facit"], r["Kommando"] == "True") for r in raekker]
facit.sort()

print(f"klip: {len(klip)}   facit: {len(facit)}\n")

# --- monoton sammenstilling: hvert klip til hoejst een saetning, i raekkefoelge
n, m = len(klip), len(facit)
INF = float("inf")
d = [[INF] * (m + 1) for _ in range(n + 1)]
fra = [[None] * (m + 1) for _ in range(n + 1)]
d[0][0] = 0.0

for i in range(n + 1):
    for j in range(m + 1):
        if d[i][j] == INF:
            continue
        if i < n and j < m:                      # par klip i med saetning j
            k = d[i][j] + wer(facit[j][1], klip[i][1])
            if k < d[i + 1][j + 1]:
                d[i + 1][j + 1] = k
                fra[i + 1][j + 1] = ("par", i, j)
        if i < n:                                 # klippet hoerer ikke til nogen
            k = d[i][j] + 1.0
            if k < d[i + 1][j]:
                d[i + 1][j] = k
                fra[i + 1][j] = ("spring-klip", i, j)
        if j < m:                                 # saetningen blev ikke fanget
            k = d[i][j] + 1.0
            if k < d[i][j + 1]:
                d[i][j + 1] = k
                fra[i][j + 1] = ("spring-facit", i, j)

par = []
i, j = n, m
while fra[i][j]:
    hvad, pi, pj = fra[i][j]
    if hvad == "par":
        par.append((pi, pj))
    i, j = pi, pj
par.reverse()

print(f"sammenstillet: {len(par)} par\n")

# --- maal enighedsreglen ved flere graenser
print(f"{'graense':<9} {'rigtigt':>8} {'FORKERT':>8} {'afvist':>7} {'FALSK ACCEPT':>14}")
print("-" * 52)

for graense in (0.0, 0.1, 0.2, 0.34, 0.5):
    rigtigt = forkert = afvist = falsk = 0
    ignoreret = 0

    for ki, fj in par:
        _, fri, bundet = klip[ki]
        _, tekst, erKommando = facit[fj]

        enige = bool(bundet) and wer(bundet, fri) <= graense
        handling = bundet if enige else None

        if erKommando:
            if not handling:
                afvist += 1
            elif wer(tekst, handling) == 0:
                rigtigt += 1
            else:
                forkert += 1
        else:
            if handling:
                falsk += 1
            else:
                ignoreret += 1

    print(f"{graense:<9} {rigtigt:>8} {forkert:>8} {afvist:>7} {falsk:>14}")

# --- UDEN GRAMMATIK: match den frie udskrift direkte mod kommandolisten
#
# Detaljerne viste, at det er GRAMMATIKKEN, der afviser - ikke genkendelsen.
# "Start optagelsen." kom ordret rigtigt ud af den frie udskrift og blev
# alligevel forkastet, fordi den bundne intet gav.
#
# Saa proev reglen uden den: find den kommando, den frie udskrift ligner mest,
# og handl kun, hvis den ligner NOK. Sikkerheden skal komme fra graensen i
# stedet for fra enigheden.
kommandoliste = [t for _, t, k in facit if k]

print(f"\n=== UDEN GRAMMATIK: kun den frie udskrift mod kommandolisten ===\n")
print(f"{'graense':<9} {'rigtigt':>8} {'FORKERT':>8} {'afvist':>7} {'FALSK ACCEPT':>14}")
print("-" * 52)

for graense in (0.0, 0.1, 0.2, 0.34, 0.5, 0.7):
    rigtigt = forkert = afvist = falsk = 0

    for ki, fj in par:
        _, fri, _ = klip[ki]
        _, tekst, erKommando = facit[fj]

        bedst, afstand = None, 9.0
        for k in kommandoliste:
            a = wer(k, fri)
            if a < afstand:
                bedst, afstand = k, a

        handling = bedst if afstand <= graense else None

        if erKommando:
            if not handling:
                afvist += 1
            elif wer(tekst, handling) == 0:
                rigtigt += 1
            else:
                forkert += 1
        elif handling:
            falsk += 1

    print(f"{graense:<9} {rigtigt:>8} {forkert:>8} {afvist:>7} {falsk:>14}")

# --- PAA BOGSTAVER I STEDET FOR ORD
#
# "Fortsat optagelsen" mod "Fortsaet optagelsen" er EET bogstav galt. Paa ord
# er det halvdelen forkert, og saa bliver kommandoen afvist. Paa bogstaver er
# det 1 af 18.
#
# Det farlige ville vaere, hvis almindelige saetninger ogsaa rykkede taettere
# paa. Det goer de ikke: "Vi holder pause om et kvarter" og "Hold pause" er
# forskellige i baade laengde og indhold, uanset hvordan der maales.
def tegnafstand(a, b):
    a = " ".join(rens(a))
    b = " ".join(rens(b))
    if not a:
        return 1.0
    d = [[0] * (len(b) + 1) for _ in range(len(a) + 1)]
    for i in range(len(a) + 1):
        d[i][0] = i
    for j in range(len(b) + 1):
        d[0][j] = j
    for i in range(1, len(a) + 1):
        for j in range(1, len(b) + 1):
            p = 0 if a[i - 1] == b[j - 1] else 1
            d[i][j] = min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + p)
    return d[len(a)][len(b)] / len(a)


print(f"\n=== PAA BOGSTAVER: fri udskrift mod kommandolisten ===\n")
print(f"{'graense':<9} {'rigtigt':>8} {'FORKERT':>8} {'afvist':>7} {'FALSK ACCEPT':>14}")
print("-" * 52)

for graense in (0.05, 0.10, 0.15, 0.20, 0.25, 0.30, 0.40):
    rigtigt = forkert = afvist = falsk = 0

    for ki, fj in par:
        _, fri, _ = klip[ki]
        _, tekst, erKommando = facit[fj]

        bedst, afstand = None, 9.0
        for k in kommandoliste:
            a = tegnafstand(k, fri)
            if a < afstand:
                bedst, afstand = k, a

        handling = bedst if afstand <= graense else None

        if erKommando:
            if not handling:
                afvist += 1
            elif wer(tekst, handling) == 0:
                rigtigt += 1
            else:
                forkert += 1
        elif handling:
            falsk += 1

    print(f"{graense:<9} {rigtigt:>8} {forkert:>8} {afvist:>7} {falsk:>14}")

# --- vis den bedste graense i detaljer
print("\n=== ved graense 0,2 ===\n")
for ki, fj in par:
    _, fri, bundet = klip[ki]
    _, tekst, erKommando = facit[fj]
    enige = bool(bundet) and wer(bundet, fri) <= 0.2
    handling = bundet if enige else None

    if not erKommando and not handling:
        continue

    if erKommando:
        m2 = "ok     " if handling and wer(tekst, handling) == 0 else \
             "FORKERT" if handling else "afvist "
    else:
        m2 = "FALSK  "

    print(f"{m2} facit: {tekst[:44]:<44} fri: {fri[:44]}")
