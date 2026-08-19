using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoteApp.Core;

public enum HaendelseType
{
    Optagelse,
    Transskription,
    Dokument,
    Rettelser,
    Hentning,
    Backup,

    /// <summary>Et møde er lagt væk som færdigbehandlet.</summary>
    Arkiveret,

    /// <summary>Et arkiveret møde er hentet frem igen.</summary>
    HentetFrem,

    /// <summary>
    /// Et dokument eller en optagelse er slettet.
    ///
    /// Der er ingen papirkurv i appen. Linjen her er derfor det eneste spor
    /// af, at noget fandtes — hvad det hed, hvornår det forsvandt, og hvor
    /// stort det var. Uden den kan man ikke bagefter afgøre, om en fil er
    /// slettet eller aldrig har været der.
    /// </summary>
    Slettet,

    /// <summary>
    /// Et dokument eller en optagelse er flyttet til en anden mappe.
    ///
    /// Flytningen er grunden til, at <see cref="Haendelse.Sti"/> ikke kan stå
    /// alene: stien i en ældre linje peger på det sted, filen lå dengang.
    /// Id'et i <see cref="Haendelse.Kilde"/> holder.
    /// </summary>
    Flyttet,

    /// <summary>
    /// Træning: en sætning læst op igen, en fejl markeret.
    ///
    /// Står for sig selv frem for som <see cref="Transskription"/>, selvom der
    /// også skrives tekst ud undervejs. Forskellen er ikke teknisk, den er,
    /// hvem der venter: en træningskørsel står man selv og ser på i et vindue,
    /// man selv har åbnet. Den skal i historikken, men den skal ikke ringe med
    /// en klokke.
    /// </summary>
    Traening,

    /// <summary>
    /// Et trin i opsætningen, der mangler.
    ///
    /// Den eneste hændelsestype, der handler om noget, der IKKE er sket. Den
    /// findes, fordi en ny installation ellers er tavs om, at appen kun kan
    /// det halve: optage og skrive ud, men ikke lave dokumenter. Det opdager
    /// man først den dag, man trykker på knappen — typisk lige efter et møde,
    /// hvor man skal bruge referatet nu.
    ///
    /// Skrives én gang. Se <see cref="Opsaetning"/>.
    /// </summary>
    Opsaetning,

    Andet
}

public enum Udfald
{
    /// <summary>Gik som det skulle.</summary>
    Fuldført,

    /// <summary>
    /// Sat i gang, men ikke gjort færdigt — og brugeren kan gøre noget ved det.
    ///
    /// BEGGE DELE SKAL VÆRE OPFYLDT. Værdien blev før også brugt om kørsler,
    /// der var færdige, men hvor noget var værd at bemærke: et usikkert
    /// sprogvalg, en meget kort optagelse. Det var forkert. Mærkatet beder om
    /// en handling, og fandtes den ikke, lærte man at se bort fra mærkatet —
    /// og så virkede det heller ikke den dag, der var noget at gøre.
    ///
    /// Er kørslen færdig, er den <see cref="Fuldført"/>, uanset hvor meget
    /// der er værd at vide om den. Det, der er værd at vide, hører i
    /// overskriften og detaljelinjen, hvor det kan læses uden at ligne en
    /// opgave.
    ///
    /// Eksempel på korrekt brug: opsætningen mangler et trin — den er
    /// påbegyndt, ikke gjort færdig, og der er en knap at trykke på.
    /// </summary>
    SeEfter,

    /// <summary>Brugeren stoppede det.</summary>
    Afbrudt,

    /// <summary>Fejlede.</summary>
    Fejlet
}

/// <summary>
/// Én ting, appen har gjort.
///
/// Felterne er valgt, så en post kan svare på det, en revision spørger om:
/// hvad blev gjort, hvornår, med hvilken model, og forlod noget maskinen.
/// </summary>
public sealed class Haendelse
{
    public DateTimeOffset Tid { get; init; } = DateTimeOffset.Now;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HaendelseType Slags { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Udfald Udfald { get; init; } = Udfald.Fuldført;

    /// <summary>Kort linje: «Møde optaget», «Transskription færdig».</summary>
    public string Hvad { get; init; } = "";

    /// <summary>Det, der gør posten brugbar bagefter: længde, model, sprog, fil.</summary>
    public string Detaljer { get; init; } = "";

    /// <summary>Hvilken model gjorde arbejdet. Tom hvis ingen var involveret.</summary>
    public string Model { get; init; } = "";

    /// <summary>Mappen eller filen, det handler om.</summary>
    public string Sti { get; init; } = "";

    /// <summary>
    /// Id'et paa det, hændelsen handler om — mødets id eller dokumentets id.
    ///
    /// HVORFOR ID OG IKKE NAVN
    ///
    /// Overskriften siger «Dokument oprettet: Møde med Cloudworks». Omdøbes
    /// dokumentet dagen efter, står der stadig det gamle navn, og linjen
    /// peger på noget, der ikke findes mere. Det samme gælder <see cref="Sti"/>:
    /// en fil, der flyttes til en mappe, kan ikke findes på sin gamle sti.
    ///
    /// Id'et ændrer sig aldrig. Navnet slås op, når linjen vises, så
    /// historikken følger med af sig selv.
    ///
    /// Tom for hændelser uden en kilde at gå til — en hentning, en backup.
    /// </summary>
    public string Kilde { get; init; } = "";

    public double Sekunder { get; init; }

    /// <summary>
    /// Forlod data maskinen? Er ALTID falsk for alt andet end hentning af
    /// motor og modeller — og hentning sender intet, den modtager.
    ///
    /// Feltet står her, fordi det er dét, en revision spørger om, og fordi et
    /// svar, der skal udledes af koden, ikke er et svar.
    /// </summary>
    public bool DataForlodMaskinen { get; init; }
}

/// <summary>
/// Historikken: hvad appen har gjort, hvornår, og hvordan det gik.
///
/// HVORFOR DEN FINDES
///
/// To grunde, og de er lige vigtige.
///
/// Den ene er praktisk: en transskription tager tyve minutter, og var man et
/// andet sted, mens den blev færdig, er der ingen kvittering tilbage at kigge
/// på. «Kørte den overhovedet?» skal kunne besvares uden at lede i mapper.
///
/// Den anden er compliance. Hele pointen med at køre modellerne lokalt er, at
/// mødet ikke forlader maskinen. Den påstand er kun noget værd, hvis den kan
/// efterprøves — og så skal der findes en liste over, hvad der blev gjort,
/// hvornår og med hvilken model.
///
/// FORMATET
///
/// JSONL, én linje pr. post, tilføjet i enden. Filen kan læses af mennesker og
/// af et regneark, den kan ikke ødelægges af en halv skrivning, og den kræver
/// ikke, at hele historikken holdes i hukommelsen.
///
/// Den ligger i datamappen og indgår i backup som alt andet.
/// </summary>
public static class Historik
{
    private static readonly object Laas = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Path => System.IO.Path.Combine(UserDataPaths.Root, "log", "historik.jsonl");

    /// <summary>
    /// Skriver en post. Fejler aldrig udad: en historik, der kan vælte det,
    /// den skulle beskrive, er værre end ingen historik.
    /// </summary>
    public static void Skriv(Haendelse post)
    {
        try
        {
            var mappe = System.IO.Path.GetDirectoryName(Path)!;
            Directory.CreateDirectory(mappe);

            lock (Laas)
                File.AppendAllText(Path, JsonSerializer.Serialize(post, Options) + "\n", new UTF8Encoding(false));
        }
        catch (Exception)
        {
            // Med vilje tavs.
        }
    }

    public static void Skriv(HaendelseType slags, string hvad, string detaljer = "",
                             Udfald udfald = Udfald.Fuldført,
                             string model = "", string sti = "", double sekunder = 0,
                             bool dataForlodMaskinen = false, string kilde = "") =>
        Skriv(new Haendelse
        {
            Slags = slags,
            Hvad = hvad,
            Detaljer = detaljer,
            Udfald = udfald,
            Model = model,
            Sti = sti,
            Sekunder = sekunder,
            DataForlodMaskinen = dataForlodMaskinen,
            Kilde = kilde
        });

    /// <summary>
    /// Historikken, nyeste først. <paramref name="maks"/> begrænser, hvor langt
    /// tilbage der læses — filen vokser, og skærmen skal ikke vente på den.
    /// </summary>
    public static IReadOnlyList<Haendelse> Laes(int maks = 500)
    {
        if (!File.Exists(Path)) return Array.Empty<Haendelse>();

        var liste = new List<Haendelse>();

        try
        {
            foreach (var linje in File.ReadLines(Path, Encoding.UTF8))
            {
                if (linje.Length == 0) continue;

                try
                {
                    var a = JsonSerializer.Deserialize<Haendelse>(linje, Options);
                    if (a is not null) liste.Add(a);
                }
                catch (JsonException)
                {
                    // En oedelagt linje maa ikke skjule resten.
                }
            }
        }
        catch (IOException) { }

        liste.Reverse();
        return liste.Count > maks ? liste.Take(maks).ToList() : liste;
    }

    /// <summary>
    /// Er der noget i historikken, der siger, at data har forladt maskinen?
    ///
    /// Svaret skal kunne gives med ét tal, ikke ved at læse en liste igennem.
    /// </summary>
    public static (int Poster, int MedUdgåendeData) Opsummer()
    {
        var alle = Laes(int.MaxValue);
        return (alle.Count, alle.Count(a => a.DataForlodMaskinen));
    }
}
