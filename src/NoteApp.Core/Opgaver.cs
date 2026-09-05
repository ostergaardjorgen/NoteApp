using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>Hvor en opgave kommer fra.</summary>
public enum Opgavekilde
{
    /// <summary>Lavet i appen — fra en transkription eller i hånden.</summary>
    Lokal,

    /// <summary>Hentet fra Google Tasks. Rettes dér, ikke her.</summary>
    Google
}

/// <summary>
/// En opgave, der kom ud af et møde.
///
/// FELTERNE ER FÅ MED VILJE
///
/// Der er hvad, hvem og hvornår-senest. Ikke estimat, ikke status, ikke
/// prioritet, ikke hvem der har uddelegeret den. En opgaveliste, der kræver
/// syv felter, bliver ikke udfyldt — og en, der ikke bliver udfyldt, findes
/// ikke. Deadline er en dato og ikke et klokkeslæt af samme grund.
/// </summary>
public sealed record Opgave
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Det korte navn — det, listen viser.
    ///
    /// HVORFOR DER ER TO FELTER OG IKKE ÉT
    ///
    /// En opgave, der kommer fra en transkription, er en hel sætning, som
    /// nogen sagde: «ja, jeg sender det reviderede budget til Tina, når hun
    /// har været igennem tallene fra Q3». Den sætning er guld værd, når man
    /// bagefter skal vide, hvad der egentlig blev aftalt — og ubrugelig som
    /// linje på en liste med tyve andre.
    ///
    /// Navnet er dét, man skimmer. Beskrivelsen er dét, man læser, når man
    /// standser op ved den ene.
    ///
    /// Tom på gamle opgaver, der blev lavet før feltet fandtes. Derfor
    /// udledes navnet af teksten, når det mangler — se <see cref="Visningsnavn"/>.
    /// Der skrives ikke et udledt navn ned i filen: gættet ville så blive til
    /// noget, der ser ud, som om nogen havde valgt det.
    /// </summary>
    public string Navn { get; set; } = "";

    /// <summary>Hele opgaven — det, der blev sagt, eller det, man selv skrev.</summary>
    public string Tekst { get; set; } = "";

    /// <summary>
    /// Navnet, som listen skal vise det.
    ///
    /// Mangler navnet, klippes teksten ved den første sætning eller ved et
    /// ordskel. Der klippes ikke midt i et ord — en afkortning, der ender i
    /// «budgettet til Ti», ser ud som en fejl og ikke som en forkortelse.
    /// </summary>
    public string Visningsnavn => Navn.Length > 0 ? Navn : Kort(Tekst);

    /// <summary>Har opgaven mere at fortælle, end navnet viser?</summary>
    public bool HarMere =>
        Tekst.Length > 0 && !string.Equals(Tekst.Trim(), Visningsnavn.Trim(),
                                           StringComparison.Ordinal);

    /// <summary>
    /// Klipper en tekst ned til et navn.
    ///
    /// Offentlig, fordi opgavevinduet skal kunne kende forskel på et navn,
    /// brugeren har valgt, og det udledte, feltet blev fyldt med. Uden den
    /// forskel ville et gæt blive gemt som noget, nogen havde besluttet.
    /// </summary>
    public static string Kort(string tekst, int maks = 70)
    {
        var t = tekst.Trim();
        if (t.Length == 0) return "";

        // Foerste saetning, hvis den er kort nok. Et punktum er et bedre
        // klippested end en tegntaelling, fordi nogen har sat det.
        var punkt = t.IndexOfAny(new[] { '.', '!', '?', '\n' });
        if (punkt > 0 && punkt <= maks) return t[..punkt].Trim();

        if (t.Length <= maks) return t;

        var skel = t.LastIndexOf(' ', maks);
        return (skel > maks / 2 ? t[..skel] : t[..maks]).TrimEnd() + "…";
    }

    /// <summary>
    /// Hvem der skal gøre det. Sat ud fra HVEM DER SAGDE DET — ikke gættet.
    ///
    /// Tom, når opgaven er oprettet i hånden til en, der ikke var med på
    /// mødet. Det sker, og det kan ingen model regne ud.
    /// </summary>
    public string Ejer { get; set; } = "";

    /// <summary>
    /// Hvor den står i listen, når man selv har flyttet rundt på den.
    /// </summary>
    /// <remarks>
    /// NUL BETYDER «IKKE FLYTTET». Så gælder den automatiske rækkefølge —
    /// hastighed, prioritet, frist — og det er den, der gælder, indtil man
    /// første gang trækker i noget.
    ///
    /// Den er en <c>double</c> og ikke en <c>int</c>, fordi en ny opgave skal
    /// kunne lægge sig FØR alt andet uden at hele listen skal skrives om: den
    /// får det mindste tal minus én. Havde det været heltal, ville de før
    /// eller siden støde sammen.
    /// </remarks>
    public double Raekkefoelge { get; set; }

    /// <summary>Senest-dato. Null betyder «ingen frist sat».</summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>
    /// Blev fristen sagt tydeligt, eller er den gættet ud af en vending?
    ///
    /// «på fredag» er entydig. «i næste uge» peger på syv dage, og appen har
    /// valgt mandag. Forskellen skal kunne ses på skærmen — en frist, appen
    /// har gættet, må aldrig se ud som en, nogen har sagt.
    /// </summary>
    public bool DeadlineUsikker { get; set; }

    /// <summary>
    /// 1, 2 eller 3 — hvor 1 er vigtigst. 0 betyder «ikke prioriteret».
    ///
    /// TRE TRIN OG IKKE FEM. Med fem bruger man kun tre af dem, og så er de to
    /// øvrige noget, man skal tage stilling til uden at få noget for det.
    ///
    /// Prioriteten sættes af MENNESKET. Den kan ikke udledes af, hvad der blev
    /// sagt i mødet — det, der lyder vigtigst, er tit bare det, der blev talt
    /// længst om.
    /// </summary>
    public int Prioritet { get; set; }

    public bool Faerdig { get; set; }

    /// <summary>
    /// Hvornår den blev krydset af. Null, hvis den aldrig er blevet det.
    ///
    /// DEN FINDES, FOR AT DAGENS AFKRYDSEDE KAN BLIVE STÅENDE DAGEN UD.
    ///
    /// En opgave, der forsvinder i det sekund, man sætter fluebenet, giver
    /// ingen kvittering — man ved ikke, om man ramte den rigtige, og man kan
    /// ikke fortryde uden at lede efter den. Den bliver stående, dæmpet, til
    /// dagen er slut. I morgen er den væk.
    ///
    /// SÆT DEN DER, HVOR FLUEBENET SÆTTES — ikke i Faerdig-egenskabens setter.
    /// System.Text.Json sætter egenskaber i den rækkefølge, de står i FILEN,
    /// og en setter, der skriver «nu» ind, ville overskrive den gemte dato,
    /// hver gang en gammel opgave blev læst ind i den forkerte rækkefølge.
    /// </summary>
    public DateTimeOffset? Faerdiggjort { get; set; }

    /// <summary>Blev den krydset af i dag?</summary>
    public bool FaerdigIDag(DateOnly idag) =>
        Faerdig && Faerdiggjort is { } t
                && DateOnly.FromDateTime(t.LocalDateTime) == idag;

    /// <summary>
    /// Sætter fluebenet og husker hvornår.
    ///
    /// Ét sted frem for tre. Fluebenet kan sættes i Cockpittet, i
    /// opgavevinduet og i transkriptionen, og datoen skal følge med alle tre
    /// steder — den slags glemmes det fjerde sted, det bliver brugt.
    /// </summary>
    public void SaetFaerdig(bool faerdig)
    {
        if (Faerdig == faerdig) return;

        Faerdig = faerdig;
        Faerdiggjort = faerdig ? DateTimeOffset.Now : null;
    }

    public DateTimeOffset Oprettet { get; init; } = DateTimeOffset.Now;

    /// <summary>Tidsstemplet i optagelsen, opgaven kom fra. Tom ved en manuel opgave.</summary>
    public string Kilde { get; init; } = "";

    /// <summary>
    /// Hvor opgaven kommer fra: appen selv eller en integration.
    ///
    /// SAMME MØNSTER SOM KALENDEREN. En opgave fra Google er ikke en anden
    /// slags opgave — den ligger i den samme liste og opfører sig ens. Det er
    /// hvor den kan RETTES, der er forskellen.
    /// </summary>
    public Opgavekilde Herkomst { get; set; } = Opgavekilde.Lokal;

    /// <summary>
    /// Opgavens id HOS leverandøren. Tom for en opgave, appen selv har lavet.
    ///
    /// Uden den ville hver hentning lave dubletter af alt — samme grund som
    /// på en aftale.
    /// </summary>
    public string FremmedId { get; set; } = "";

    /// <summary>Listen hos leverandøren. Google har flere, og de skal holdes fra hinanden.</summary>
    public string FremmedListe { get; set; } = "";

    /// <summary>Kan opgaven rettes her i appen?</summary>
    public bool KanRettes => Herkomst == Opgavekilde.Lokal;

    /// <summary>
    /// Optagelsen, opgaven kom fra. Tom, når den ikke kom fra et møde.
    ///
    /// HERKOMSTEN ER EN OPLYSNING PÅ OPGAVEN — IKKE DENS ADRESSE.
    ///
    /// Indtil 24-08-2026 lå opgaven i optagelsens mappe, og mappen VAR
    /// herkomsten. Det holdt, så længe alle opgaver kom fra et møde. En
    /// opgave, man skriver i hånden, gør ikke — og en, der hentes fra Google,
    /// slet ikke. De skulle så have et opdigtet hjem i en mappe, der lod som
    /// om, der var optaget noget.
    ///
    /// Nu ligger alle opgaver samlet, og de bærer selv, hvor de kom fra. Se
    /// <see cref="Opgavelager"/>.
    /// </summary>
    public string MoedeId { get; set; } = "";

    /// <summary>
    /// Mødets titel, som den var, da opgaven blev lavet.
    ///
    /// Den GEMMES frem for at blive slået op. Slettes optagelsen, kan
    /// herkomsten stadig læses — «Møde med Tina, 12. august» siger noget, hvor
    /// et id, der ikke findes, ikke siger noget.
    /// </summary>
    public string Moedetitel { get; set; } = "";
}

/// <summary>
/// Mødets opgaver — og de forslag, der er sagt nej til.
///
/// AFVISNINGERNE GEMMES.
///
/// Uden dem ville de samme otte forslag dukke op hver gang, man åbnede fanen,
/// og så holder man op med at kigge på den. Der gemmes selve teksten og ikke
/// et linjenummer: linjerne flytter sig, hvis optagelsen bliver skrevet ud
/// igen, og så ville afvisningerne pege på noget andet.
/// </summary>
public sealed class Opgaveliste
{
    /// <summary>
    /// Kun ved indlæsning af den GAMLE fil. Opgaver ligger i Opgavelager nu.
    /// Feltet bliver stående, så flytningen kan læse dem ud af de gamle filer.
    /// </summary>
    public List<Opgave> Opgaver { get; init; } = new();

    public List<string> Afvist { get; init; } = new();

    /// <summary>
    /// Afvisningerne ligger i «afvist.json» ved siden af optagelsen.
    ///
    /// EGET FILNAVN, FORDI OPGAVERNE ER FLYTTET UD. De lå begge i
    /// «opgaver.json», og den fil flyttes nu til det fælles lager. Blev de
    /// ved med at dele navn, ville flytningen og afvisningerne skiftes til at
    /// skrive oven i hinanden. Se <see cref="Opgavelager"/>.
    /// </summary>
    public static string Sti(string mappe) => Path.Combine(mappe, "afvist.json");

    /// <summary>Den gamle fil. Læses ved flytningen, skrives aldrig mere.</summary>
    public static string GammelSti(string mappe) => Path.Combine(mappe, "opgaver.json");

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Opgaveliste Hent(string mappe)
    {
        var sti = Sti(mappe);
        if (!File.Exists(sti)) return new Opgaveliste();

        try
        {
            return JsonSerializer.Deserialize<Opgaveliste>(File.ReadAllText(sti, Encoding.UTF8))
                   ?? new Opgaveliste();
        }
        catch (Exception)
        {
            return new Opgaveliste();
        }
    }

    public void Gem(string mappe)
    {
        Directory.CreateDirectory(mappe);
        File.WriteAllText(Sti(mappe), JsonSerializer.Serialize(this, Format), new UTF8Encoding(false));
    }

    /// <summary>Nøglen, en afvisning huskes på. Teksten renset for mellemrum og tegn.</summary>
    public static string Noegle(string tekst)
    {
        var sb = new StringBuilder();
        foreach (var c in tekst.ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);

        var s = sb.ToString();
        return s.Length <= 120 ? s : s[..120];
    }

    public bool ErAfvist(string tekst) => Afvist.Contains(Noegle(tekst));

    public void Afvis(string tekst)
    {
        var n = Noegle(tekst);
        if (!Afvist.Contains(n)) Afvist.Add(n);
    }

    /// <summary>
    /// Er der allerede oprettet en opgave af den her tekst?
    ///
    /// Opgaverne kommer udefra nu — listen kender dem ikke selv, siden de
    /// flyttede til det fælles lager.
    /// </summary>
    public static bool ErOprettet(string tekst, IEnumerable<Opgave> opgaver) =>
        opgaver.Any(o => Noegle(o.Tekst) == Noegle(tekst));
}
