namespace NoteApp.Core;

/// <summary>
/// Hvor god er mikrofonen — målt, ikke gættet.
///
/// HVORFOR DEN FINDES
///
/// Træning og ordbog er fjernet, fordi tre håndtag i træk målte nul. Det ene
/// spørgsmål, træningen faktisk kunne svare på, var et andet: <em>er min
/// mikrofon god nok til, at det her kan lade sig gøre?</em> Det er dyrt at
/// finde ud af efter et møde på en time.
///
/// Testen er derfor ikke en rest af træningen. Den er det, der var værd at
/// beholde: en fast tekst, appen kender, læst op i tredive sekunder, målt ord
/// for ord mod udskriften. Ét tal, ét råd, ingen indlæring.
///
/// HVORFOR SÆTNINGER OG IKKE LØSE ORD
///
/// Whisper er en sprogmodel og ikke en ordbog. Løse ord uden sammenhæng
/// rammer den dårligt, uanset hvor god mikrofonen er — så ville tallet måle
/// Whisper frem for mikrofonen. Sætninger giver modellen den sammenhæng, den
/// har i et rigtigt møde, og så er det, der bliver tilbage af fejl, lyden.
///
/// HVORFOR TEKSTEN SER UD, SOM DEN GØR
///
/// Ingen fagord, ingen sjældne navne, ingen tal skrevet med bogstaver, der kan
/// blive til cifre. Alt det ville måle noget andet end mikrofonen. Til gengæld
/// er der hvæselyde (s, f), lukkelyde (p, t, k) og de danske vokaler, fordi
/// det er dem, en dårlig mikrofon eller et rum med rumklang først taber.
/// </summary>
public static class Mikrofontest
{
    /// <summary>
    /// Prøveteksten. Cirka halvfjerds ord — omtrent tredive sekunder i et
    /// almindeligt taletempo.
    /// </summary>
    public const string Proevetekst =
        "Vi skal have styr på planen, inden vi går videre med det næste punkt.\n" +
        "Sofie samler op på det, vi talte om sidst, og fortæller hvad der mangler.\n" +
        "Bagefter tager vi en runde, hvor alle siger, hvad de står med lige nu.\n" +
        "Er der spørgsmål undervejs, så stil dem med det samme frem for at vente.\n" +
        "Til sidst skriver vi ned, hvem der gør hvad, og hvornår det skal være klar.";

    /// <summary>Hvor lang tid oplæsningen må tage, før den stoppes af sig selv.</summary>
    public static readonly TimeSpan Maksimum = TimeSpan.FromSeconds(60);

    /// <summary>Under dette er der for lidt lyd til at måle på.</summary>
    public static readonly TimeSpan Mindstelaengde = TimeSpan.FromSeconds(8);

    /// <summary>Antal ord i prøveteksten. Bruges til at vise, hvor langt man er.</summary>
    public static int Ordantal => ReadAloudScore.Ord(Proevetekst).Count;

    /// <summary>
    /// Resultatet af en test.
    /// </summary>
    /// <param name="Ialt">Ord i prøveteksten.</param>
    /// <param name="Ramt">Ord, Whisper skrev rigtigt.</param>
    /// <param name="Sekunder">Klippets længde.</param>
    /// <param name="Udskrift">Det, Whisper hørte. Vises, så tallet kan efterprøves.</param>
    /// <param name="Afvigelser">De enkelte fejl, i tekstens rækkefølge.</param>
    public sealed record Resultat(
        int Ialt,
        int Ramt,
        double Sekunder,
        string Udskrift,
        IReadOnlyList<Afvigelse> Afvigelser)
    {
        public double Procent => Ialt == 0 ? 0 : 100.0 * Ramt / Ialt;

        /// <summary>
        /// GRÆNSERNE ER KALIBRERET, IKKE GÆTTET.
        ///
        /// Teksten er 71 ord, så ét ord vejer 1,4 procentpoint. Første udgave
        /// satte «god» ved 95 %, og det viste sig at være en fælde: fire små
        /// hørefejl — «Sofie» til «Sofia», «undervejs» til «under vejs» — gav
        /// 94,4 % og dermed dommen «brugbar». Whisper laver selv den slags
        /// fejl på studiekvalitet, så grænsen ville have dømt et godt headset
        /// for noget, mikrofonen ikke havde skyld i.
        ///
        /// 92 % giver plads til fem fejl, 80 % til fjorten. Målt på simulerede
        /// udskrifter: hvert tiende ord tabt giver 89 % (brugbar), hvert
        /// tredje 66 % (dårlig). Det passer med, hvad de to niveauer skal
        /// betyde.
        /// </summary>
        public Bedoemmelse Karakter => Procent switch
        {
            >= 92 => Bedoemmelse.God,
            >= 80 => Bedoemmelse.Brugbar,
            _ => Bedoemmelse.Daarlig
        };

        /// <summary>
        /// Rådet. Det står her frem for i skærmen, fordi grænserne og teksten
        /// hører sammen — flyttes den ene, skal den anden med.
        /// </summary>
        public string Raad => Karakter switch
        {
            Bedoemmelse.God =>
                "Mikrofonen er god nok. Udskriften af et rigtigt møde bliver dårligere end " +
                "det her, fordi folk taler i munden på hinanden og vender sig væk — men " +
                "lyden er ikke det, der står i vejen.",

            Bedoemmelse.Brugbar =>
                "Det kan bruges, men der er noget at hente. Prøv at komme tættere på " +
                "mikrofonen, luk vinduet, og sluk for en ventilator eller en maskine, der " +
                "kører i baggrunden. Kør testen igen bagefter og se, om tallet flytter sig.",

            _ =>
                "Så mange fejl på en tekst, der læses højt i et roligt tempo, betyder at " +
                "et rigtigt møde bliver svært at bruge. Tjek at den rigtige mikrofon er " +
                "valgt ovenfor, at der ikke ligger noget hen over den, og at rummet ikke " +
                "giver rumklang. Et headset er den billigste store forbedring."
        };
    }

    public enum Bedoemmelse { Daarlig, Brugbar, God }

    /// <summary>
    /// Bedømmer et klip, der allerede er skrevet ud.
    ///
    /// Sproget er låst til dansk her — modsat møder, hvor det er «auto». Her
    /// VED vi, hvad der blev sagt, og et fejlgættet sprog ville måle Whispers
    /// sproggenkendelse frem for mikrofonen.
    /// </summary>
    public static Resultat Bedoem(string udskrift, double sekunder)
    {
        var (ialt, ramt, afvigelser) = ReadAloudScore.Sammenlign(Proevetekst, udskrift);
        return new Resultat(ialt, ramt, sekunder, udskrift.Trim(), afvigelser);
    }
}
