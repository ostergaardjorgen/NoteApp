# Hvor taet kommer en ALMINDELIG saetning paa en kommando?
#
# Det er sikkerhedsmarginen. Ligger den taettest paa 0,9, kan graensen saettes
# hoejt uden risiko. Ligger den paa 0,35, er 0,30 en kant og ikke en margen.

import csv, io, re, sys, unicodedata

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

CSV = r"C:\AppNoter\Optagelser\2026-08-21_07-14\kommandoer\resultat.csv"


def rens(s):
    if not s:
        return []
    s = unicodedata.normalize("NFC", s).lower()
    s = re.sub(r"[^\wæøå ]", " ", s)
    return [o for o in s.split() if o]


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


raekker = list(csv.DictReader(open(CSV, encoding="utf-8-sig")))
kommandoer = [r["Facit"] for r in raekker if r["Kommando"] == "True"]

print("De almindelige sætninger, der kommer TÆTTEST på en kommando:\n")

ud = []
for r in raekker:
    if r["Kommando"] == "True":
        continue
    fri = r["Fri"]
    if not fri:
        continue

    bedst, afstand = None, 9.0
    for k in kommandoer:
        t = tegnafstand(k, fri)
        if t < afstand:
            bedst, afstand = k, t

    ud.append((afstand, fri, bedst))

ud.sort()
for a, fri, b in ud[:6]:
    print(f"  {a:.2f}  {fri[:50]:<50} → {b}")

print(f"\n  tættest overhovedet : {ud[0][0]:.2f}")
print(f"  brugbar grænse      : op til {ud[0][0] - 0.01:.2f} uden falske accepter")
