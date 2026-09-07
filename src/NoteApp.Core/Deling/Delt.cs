using System.Text;
using System.Text.Json;

namespace NoteApp.Core.Deling;

/// <summary>Den fælles mappe, set udefra.</summary>
public sealed record Delteoplysning(string Id, string Navn, DateTimeOffset Oprettet, int Udgave);

/// <summary>Én maskine, som den melder sig i den fælles mappe.</summary>
public sealed class Maskinoplysning
{
    public string Id { get; init; } = "";
    public string Navn { get; set; } = "";
    public Maskinrolle Rolle { get; set; }

    /// <summary>Den offentlige nøgle, base64. Det er den, der parres på.</summary>
    public string Noegle { get; set; } = "";

    public DateTimeOffset SidstSet { get; set; }

    /// <summary>Appens version. Til at kunne se, om den ene er bagud.</summary>
    public string Udgave { get; set; } = "";

    /// <summary>
    /// Id'erne på de maskiner, den her computer har godkendt.
    /// </summary>
    /// <remarks>
    /// ============ GODKENDELSEN SKAL SES FRA BEGGE SIDER ============
    ///
    /// Parringen skal gøres på BEGGE skærme — hver maskine fæstner den andens
    /// nøgle. Før stod den ene og sagde «godkendt», mens den anden intet
    /// vidste og intet spurgte om: nøglen blev sendt, og den blev afvist i
    /// stilhed i den anden ende.
    ///
    /// Listen her er offentlig af natur — id'erne ligger allerede i mappen —
    /// og den gør to ting mulige: den maskine, der mangler at godkende, kan
    /// SELV spørge, og den, der har sendt noget, kan skrive på skærmen, at
    /// den venter på den anden.
    ///
    /// NULL BETYDER «VED IKKE». En maskine på en ældre udgave skriver ikke
    /// feltet, og så må der ikke stå, at den ikke har godkendt.
    /// </remarks>
    public List<string>? Godkendte { get; set; }

    /// <summary>Har den her maskine godkendt os? Null når den ikke siger det.</summary>
    public bool? HarGodkendt(string id) => Godkendte?.Contains(id);

    /// <summary>Hvor længe siden maskinen sidst meldte sig.</summary>
    public TimeSpan Siden => DateTimeOffset.Now - SidstSet;

    /// <summary>
    /// Er den i live?
    /// </summary>
    /// <remarks>
    /// Hjerteslaget kommer hvert femte minut, og en synkronisering må gerne
    /// være et kvarter om at nå frem. Tyve minutter er derfor «lige nu», og
    /// alt derover er noget, skærmen skal skrive med tidspunkt: «sidst set i
    /// går kl. 21» er en oplysning, man kan handle på.
    /// </remarks>
    public bool ILive => Siden < TimeSpan.FromMinutes(20);
}

/// <summary>
/// Den fælles mappe: hvor to maskiner møder hinanden.
/// </summary>
/// <remarks>
/// ============ DET ER EN POSTKASSE, IKKE EN DATAMAPPE ============
///
/// Datamappen bliver liggende lokalt på hver maskine. Her ligger kun det, der
/// er på vej fra den ene til den anden — og det ryddes, når det er kommet
/// frem. Grunden står i <see cref="UserDataPaths"/>: en SQLite-database og en
/// wav-fil, der vokser, mens der optages, tåler ikke at ligge i en mappe, to
/// maskiner synkroniserer.
///
/// ============ ADGANGEN TIL MAPPEN ER DEN RIGTIGE GRÆNSE ============
///
/// Appen kan sikre, at de to maskiner er dem, de siger de er, og at ingen
/// tredje kan lægge arbejde ind — se <see cref="Parring"/>. Den kan ikke
/// gøre en mappe privat, som andre har adgang til. Det er delingens egne
/// rettigheder på NAS'en eller i skytjenesten, der afgør, hvem der kan læse
/// med, og det skal stå på skærmen frem for at blive antaget.
///
/// ============ DER SKRIVES ALDRIG I ANDRES FILER ============
///
/// Hver maskine ejer sin egen fil under <c>maskiner\</c> og rører ikke de
/// andres. To skrivere på den samme fil gennem en synkroniseringsklient
/// bliver til en «conflicted copy», og den slags opdager ingen.
/// </remarks>
public static class Delt
{
    /// <summary>Filen, der gør en mappe til en HeyPia-delemappe.</summary>
    public const string Maerkefil = "heypia-delt.json";

    /// <summary>Formatets udgave. Tælles op, hvis mappens indhold ændrer form.</summary>
    public const int Udgave = 1;

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Den fælles mappe, den her maskine bruger. Null når der ikke deles.</summary>
    public static string? Mappe => Maskinid.Deltmappe;

    /// <summary>Deles der overhovedet noget?</summary>
    public static bool Slaaet_til => Mappe is { } m && Directory.Exists(m) && Er(m);

    private static string Maskinmappe(string rod) => Path.Combine(rod, "maskiner");

    // ================================================================ selve mappen

    /// <summary>Er der en HeyPia-deling i mappen?</summary>
    public static bool Er(string sti)
    {
        try { return File.Exists(Path.Combine(sti, Maerkefil)); }
        catch (Exception) { return false; }
    }

    /// <summary>Læser mappens mærke. Null hvis der ikke er nogen deling.</summary>
    public static Delteoplysning? Oplysning(string sti)
    {
        try
        {
            var fil = Path.Combine(sti, Maerkefil);
            if (!File.Exists(fil)) return null;

            return JsonSerializer.Deserialize<Delteoplysning>(File.ReadAllText(fil, Encoding.UTF8));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Klargør mappen som deling. Findes der en i forvejen, røres den ikke.
    /// </summary>
    /// <remarks>
    /// DEN ANDEN MASKINE SKAL IKKE OPRETTE NOGET. Den peger bare på den samme
    /// mappe og finder mærket. Var det omvendt — at begge oprettede — ville
    /// den ene overskrive den andens id, og de to ville tro, de var i hver sin
    /// deling.
    /// </remarks>
    public static Delteoplysning Klargoer(string sti, string? navn = null)
    {
        Directory.CreateDirectory(sti);

        if (Oplysning(sti) is { } findes) return findes;

        var ny = new Delteoplysning(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(navn) ? "HeyPia-deling" : navn.Trim(),
            DateTimeOffset.Now,
            Udgave);

        var fil = Path.Combine(sti, Maerkefil);
        var midlertidig = fil + ".ny";

        File.WriteAllText(midlertidig, JsonSerializer.Serialize(ny, Format), new UTF8Encoding(false));
        File.Move(midlertidig, fil, overwrite: true);

        Directory.CreateDirectory(Maskinmappe(sti));
        return ny;
    }

    // ================================================================ hjerteslaget

    /// <summary>
    /// Melder den her maskine i den fælles mappe.
    /// </summary>
    /// <remarks>
    /// KALDES OGSÅ, NÅR DER IKKE ER SKET NOGET. Det er dét, der gør, at den
    /// anden maskine kan skrive «sidst set i går kl. 21» frem for bare at
    /// vente. En bruger, der ikke kan se, at den anden PC er slukket, tror,
    /// appen er i stykker.
    /// </remarks>
    public static bool Meld()
    {
        if (Mappe is not { } rod) return false;

        try
        {
            if (!Er(rod)) return false;

            Directory.CreateDirectory(Maskinmappe(rod));

            var mig = new Maskinoplysning
            {
                Id = Maskinid.Id,
                Navn = Maskinid.Navn,
                Rolle = Maskinid.Rolle,
                Noegle = Maskinid.Offentlignoegle(),
                SidstSet = DateTimeOffset.Now,
                Udgave = Appversion(),
                Godkendte = Parring.Alle().Select(b => b.Id).ToList(),
            };

            var fil = Path.Combine(Maskinmappe(rod), mig.Id + ".json");
            var midlertidig = fil + ".ny";

            File.WriteAllText(midlertidig, JsonSerializer.Serialize(mig, Format), new UTF8Encoding(false));
            File.Move(midlertidig, fil, overwrite: true);

            return true;
        }
        catch (Exception)
        {
            // Er drevet væk et øjeblik, prøver vi igen om fem minutter. Et
            // hjerteslag, der ikke nåede frem, må ikke kunne vælte noget.
            return false;
        }
    }

    /// <summary>Alle maskiner i delingen — også den her.</summary>
    public static IReadOnlyList<Maskinoplysning> Alle()
    {
        if (Mappe is not { } rod) return Array.Empty<Maskinoplysning>();

        var mappe = Maskinmappe(rod);
        if (!Directory.Exists(mappe)) return Array.Empty<Maskinoplysning>();

        var ud = new List<Maskinoplysning>();

        foreach (var fil in Directory.EnumerateFiles(mappe, "*.json"))
        {
            try
            {
                var m = JsonSerializer.Deserialize<Maskinoplysning>(File.ReadAllText(fil, Encoding.UTF8));
                if (m is not null && m.Id.Length > 0) ud.Add(m);
            }
            catch (Exception)
            {
                // En halvskrevet fil fra en synkronisering, der er i gang.
                // Den er hel om lidt; de øvrige skal stadig kunne vises.
            }
        }

        return ud.OrderBy(m => m.Navn, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Fjerner en maskine fra delingen.
    /// </summary>
    /// <remarks>
    /// EN INSTALLATION FORFRA EFTERLADER EN FORÆLDRELØS. Maskinen får et nyt
    /// id, og den gamle fil bliver liggende med sit gamle hjerteslag. Den er
    /// ikke farlig — der kan ikke udveksles med den, for der er ingen, der
    /// har dens private nøgle længere — men den står i listen og forvirrer.
    ///
    /// Det er brugerens beslutning at rydde den, ikke appens: en maskine, der
    /// har været slukket i tre uger, er ikke det samme som en, der er væk.
    /// </remarks>
    public static bool Fjern(string id)
    {
        if (Mappe is not { } rod || id.Length == 0) return false;

        try
        {
            var fil = Path.Combine(Maskinmappe(rod), id + ".json");
            if (!File.Exists(fil)) return false;

            File.Delete(fil);
            Parring.Glem(id);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>De andre maskiner. Dem, der er noget at forholde sig til.</summary>
    public static IReadOnlyList<Maskinoplysning> Andre() =>
        Alle().Where(m => m.Id != Maskinid.Id).ToList();

    private static string Appversion() =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "";
}
