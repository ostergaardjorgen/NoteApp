using System.Text.Json.Serialization;

namespace NoteApp.Core;

/// <summary>
/// Mødetypen styrer hvilke spor der optages, hvordan de flettes i fase 2, og
/// hvilken kontekstblok eksporten får med. Der er bevidst ingen standardværdi:
/// vælges der forkert på et onlinemøde, opdages det først når mødet er slut og
/// loopback-sporet er tomt.
/// </summary>
public enum MeetingType
{
    Physical,
    Online,

    /// <summary>
    /// Et webinar: envejs, og derfor kun ÉT spor.
    ///
    /// Du lytter; du taler ikke. Optages kun højttalersporet, fylder
    /// optagelsen det halve, skrives ud på det halve af tiden — og bliver
    /// bedre: der er intet ekko fra din egen mikrofon, ingen overlappende
    /// tale, og ingen tvivl om hvilken side der talte.
    ///
    /// Det er den eneste optagelsestype, brugeren VÆLGER på forhånd. Fysisk
    /// mod online afgøres bagefter ved at måle, om der kom lyd på
    /// højttalersporet — men et webinar kan ikke kendes fra et onlinemøde på
    /// lyden alene, for de lyder ens. Forskellen er, om DU siger noget.
    /// </summary>
    Webinar
}

public enum TrackKind
{
    Microphone,
    Loopback
}

/// <summary>
/// Metadata for ét møde. Skrives til meeting.json ved start og opdateres ved
/// stop, så en afbrudt optagelse stadig efterlader noget læsbart.
/// </summary>
public sealed class MeetingMetadata
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MeetingType Type { get; set; }

    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; set; }
    public double DurationSeconds { get; set; }
    public string? Title { get; set; }

    public string MicDeviceName { get; init; } = "";

    /// <summary>Null ved fysiske møder — det er også flaget fase 2 læser for at springe fletningen over.</summary>
    public string? LoopbackDeviceName { get; set; }

    public int SampleRate { get; init; } = AudioFormat.SampleRate;
    public int Channels { get; init; } = 1;

    /// <summary>
    /// Adressen på det, der blev optaget. Sat ved webinarer.
    ///
    /// HVORFOR DEN GEMMES
    ///
    /// Et webinar ligger tit online bagefter. Har man udskriften og finder et
    /// sted, hvor man vil se, hvad der blev vist på skærmen, er linket den
    /// eneste vej tilbage — og det er væk fra indbakken en måned senere.
    ///
    /// Den hører til OPTAGELSEN og ikke til opsummeringen. En opsummering kan
    /// laves om; linket skal overleve det.
    /// </summary>
    public string? Kilde { get; set; }

    /// <summary>Sat af genopretningen når mødet blev samlet fra efterladte segmenter.</summary>
    public bool RecoveredAfterCrash { get; set; }

    /// <summary>
    /// Hvilken prøvetekst der blev læst op: «dansk», «blandet», «engelsk».
    /// Null for rigtige møder.
    ///
    /// Feltet findes, fordi tællingen på «Start her» før læste teksten ud af
    /// TITLEN. Da en optagelse blev omdøbt til noget andet, forsvandt den fra
    /// tællingen — den var der, men appen kunne ikke længere se det.
    ///
    /// Titlen er brugerens; den skal kunne hedde hvad som helst. Nøglen er
    /// appens og hører i sit eget felt.
    /// </summary>
    public string? ReadAloudScript { get; set; }

    /// <summary>
    /// Hvornår mødet blev lagt i arkivet. Null, så længe det ligger fremme.
    ///
    /// Arkivet er ikke en papirkurv. Det er stedet, et FÆRDIGBEHANDLET møde
    /// flyttes hen, så listen forrest kun viser det, der stadig mangler noget.
    /// Uden den opdeling vokser listen for hvert møde, og den, der er skrevet
    /// ud men mangler et referat, drukner mellem tres, der er helt færdige.
    ///
    /// Datoen gemmes frem for et flag, fordi «hvornår blev det lagt væk» er
    /// det, man spørger om, når noget skal findes frem igen.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>
    /// Sproget mødet blev holdt på, som Whisper fandt det. Null indtil mødet
    /// er transskriberet.
    ///
    /// Gemmes, fordi skabelonerne skal kunne forholde sig til sproget bagefter
    /// — et referat af et engelsk møde skal skrives på dansk, og det kan kun
    /// lade sig gøre, hvis det står et sted, at mødet var engelsk.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Hvor sikker sprogdetekteringen var (0-1). Null når sproget var valgt
    /// på forhånd. Dansk, norsk og svensk ligner hinanden nok til, at tallet
    /// er værd at kunne slå op, når et referat ser forkert ud.
    /// </summary>
    public double? LanguageProbability { get; set; }

    /// <summary>
    /// Brugerens egen mappe. Null eller tom betyder «uden mappe».
    ///
    /// Findes, fordi kundemateriale ikke skal ligge blandet med interne møder
    /// i én lang liste. Det er et felt og ikke en rigtig mappe på disken — se
    /// <see cref="NoteApp.Core.Mapper"/> for hvorfor.
    /// </summary>
    public string? Mappe { get; set; }

    /// <summary>
    /// Mødetypen, valgt da optagelsen blev startet. Null betyder «ikke valgt».
    ///
    /// Værdien er NAVNET på en mødetype — den samme tekst, brugeren ser — og
    /// ikke et id eller en filsti. Mødetyperne er filer i datamappen, som
    /// brugeren selv kan rette, kopiere og slette; et id ville pege på noget,
    /// der kan forsvinde, og en filsti ville brække, første gang datamappen
    /// blev flyttet.
    ///
    /// Findes typen ikke længere, står navnet stadig. Det er den rigtige
    /// opførsel: «det her var et statusmøde» er en oplysning om mødet, også
    /// efter at skabelonen bag er slettet.
    ///
    /// HVORFOR DEN VÆLGES FØR MØDET OG IKKE BAGEFTER: den er kendt på forhånd.
    /// Bagefter skal man oversætte sit møde til en skabelon for at komme
    /// videre, og det er dér, dokumentdelen bliver svær at finde ud af.
    /// </summary>
    public string? Moedetype { get; set; }

    /// <summary>
    /// Sproget, der blev VALGT til mikrofonsporet, sidst der blev skrevet ud.
    ///
    /// Huskes pr. møde og ikke som en global indstilling. Man holder ikke
    /// alle sine møder på samme sprog, men et bestemt møde har det samme
    /// sprog, hver gang det skrives ud igen — og så skal man ikke tage
    /// stilling forfra, bare fordi man gør det om.
    ///
    /// Forskellig fra <see cref="Language"/>, som er det, Whisper LANDEDE på.
    /// Det ene er et valg, det andet er et udfald.
    /// </summary>
    public string? ValgtSprogMik { get; set; }

    /// <summary>Det samme for gæsternes spor. Null når mødet kun har ét spor.</summary>
    public string? ValgtSprogLoop { get; set; }

    /// <summary>
    /// Navnene på talerne, pr. spor: HERFRA og DERFRA.
    ///
    /// Ligger HER og ikke inde i udskriften. Navnene sættes på, når teksten
    /// vises, så en omdøbning ikke kræver, at udskriften skrives om — og så
    /// de overlever, at transskriptionen køres om.
    ///
    /// Det er det samme princip, der bærer resten af appen: gem id'et, slå
    /// navnet op.
    /// </summary>
    public Dictionary<string, string> Talere { get; init; } = new();

    public Dictionary<string, string> Tracks { get; init; } = new();

    /// <summary>
    /// Sat, når optagelsen kom ind som en fil udefra — en talememo fra
    /// telefonen, en diktafon, et webinar nogen sendte. Null for alt, appen
    /// selv har optaget.
    ///
    /// Feltet bærer én oplysning, som skærmene har brug for: der er kun ÉT
    /// spor. En optagelse herfra har to sider, HERFRA og DERFRA, og hele
    /// fletningen bygger på det. En fil udefra har alt på én stribe, og hvem
    /// der sagde hvad, kan ikke skilles ad bagefter. Det skal stå, ikke
    /// opdages.
    /// </summary>
    public Indlaesningskilde? Indlaest { get; set; }
}

/// <summary>Hvor en indlæst lydfil kom fra. Ren dokumentation — intet i appen handler på den.</summary>
public sealed class Indlaesningskilde
{
    public string Filnavn { get; set; } = "";

    /// <summary>
    /// Hele stien, filen blev læst fra. Gemmes, fordi spørgsmålet
    /// «hvor kom den her fra» kommer måneder senere, hvor filnavnet alene
    /// ikke er nok. Kildefilen røres aldrig; stien er et spor, ikke en
    /// forbindelse.
    /// </summary>
    public string Sti { get; set; } = "";

    public DateTimeOffset Tidspunkt { get; set; }

    /// <summary>Størrelsen på den fil, der blev læst — ikke på den wav, der kom ud.</summary>
    public long Bytes { get; set; }
}

/// <summary>
/// En note skrevet under mødet, eller et bogmærke sat med genvejstasten.
/// Tidsstemplet er relativt til optagelsens start, så noten kan flettes ind i
/// transskriptionen det rigtige sted.
/// </summary>
public sealed class MeetingNote
{
    public double AtSeconds { get; init; }
    public DateTimeOffset WallClock { get; init; }

    /// <summary>Tom ved et rent bogmærke fra genvejstasten.</summary>
    public string Text { get; init; } = "";

    public bool IsMarker { get; init; }

    public string Timecode => TimeSpan.FromSeconds(AtSeconds).ToString(@"hh\:mm\:ss");
}

public static class AudioFormat
{
    /// <summary>whisper.cpp kræver 16 kHz mono. Alt konverteres undervejs, ikke bagefter.</summary>
    public const int SampleRate = 16_000;

    public const int Channels = 1;
    public const int BitsPerSample = 16;
    public const int BytesPerSecond = SampleRate * Channels * (BitsPerSample / 8);

    /// <summary>
    /// Længden på hvert autosave-segment. Crasher appen eller løber batteriet
    /// tør, mister du højst dette — ikke hele mødet. Det er ikke en feature,
    /// det er forudsætningen for at turde bruge værktøjet til noget.
    /// </summary>
    public static readonly TimeSpan ChunkDuration = TimeSpan.FromSeconds(30);
}
