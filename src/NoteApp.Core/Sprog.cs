using System.Reflection;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>Et sprog, appen kan vise. <c>Flag</c> er landekoden, skærmen tegner flaget ud fra.</summary>
public sealed record Sprogvalg(string Kode, string Navn, string Flag);

/// <summary>
/// Teksterne i brugerfladen, på det sprog brugeren har valgt.
///
/// HVORFOR JSON-FILER OG IKKE .RESX
///
/// .NET's egen vej er ressourcefiler og satellit-assemblies, og den er
/// udmærket — bortset fra ét sted, som er præcis dét, det her skal kunne: at
/// tilføje et sprog kræver Visual Studio og et nyt byg. En oversætter kan
/// ikke aflevere et nyt sprog uden en udvikler.
///
/// Med en JSON-fil pr. sprog er et nyt sprog én fil, der lægges i mappen. Det
/// er den samme beslutning som med mødetyperne: det, brugeren skal kunne
/// ændre, er filer, ikke kode.
///
/// Nøglerne er punktopdelte — <c>nav.cockpit</c>, <c>topbar.optag</c> — som
/// det er skik i i18n. Formen er den samme, uanset hvilket værktøj en
/// oversætter er vant til.
///
/// SÅDAN TILFØJER MAN ET SPROG
///
/// Kopiér <c>da.json</c> til fx <c>de.json</c> i sprogmappen, ret
/// <c>_sprog.kode</c>, <c>_sprog.navn</c> og <c>_sprog.flag</c>, og oversæt
/// resten. Appen finder filen ved næste start. Der skal intet bygges.
///
/// FALDER TILBAGE PÅ DANSK, IKKE PÅ NØGLEN
///
/// Mangler en nøgle i det valgte sprog, hentes den danske tekst. Dansk er
/// kildesproget og derfor det eneste, der altid er komplet. Et halvt oversat
/// sprog viser altså dansk dér, hvor der mangler noget — ikke
/// «nav.cockpit», som ingen kan bruge til noget.
/// </summary>
public static class Sprog
{
    /// <summary>Kildesproget. Det, der falders tilbage på, og det, der altid er komplet.</summary>
    public const string Kilde = "da";

    private static readonly object _laas = new();

    private static Dictionary<string, string>? _valgt;
    private static Dictionary<string, string>? _kilde;
    private static string _kode = Kilde;
    private static System.Globalization.CultureInfo? _kultur;

    /// <summary>Meldes, når sproget skiftes. Skærmene lytter med og skriver sig om.</summary>
    public static event Action? Aendret;

    /// <summary>Sprogmappen. Én fil pr. sprog.</summary>
    public static string Mappe => Path.Combine(UserDataPaths.Root, "sprog");

    /// <summary>
    /// Kulturen, datoer og tal skal skrives i.
    ///
    /// HVORFOR DEN HØRER SAMMEN MED SPROGET
    ///
    /// «Torsdag 27. august» er lige så meget dansk som «Optagelser». Skiftede
    /// kun teksterne, ville en engelsk brugerflade vise engelske knapper og
    /// danske ugedage — og det ser ud som en fejl, fordi det ER en.
    ///
    /// Kulturen kommer fra sprogfilens <c>_sprog.kultur</c>, så et nyt sprog
    /// selv siger, hvordan dets datoer ser ud. Mangler feltet, bruges koden —
    /// «de» giver tysk. Kender Windows ikke koden, bruges maskinens egen.
    /// </summary>
    public static System.Globalization.CultureInfo Kultur
    {
        get
        {
            lock (_laas)
            {
                Sikr();

                if (_kultur is not null) return _kultur;

                var navn = _valgt!.TryGetValue("_sprog.kultur", out var k) && k.Length > 0
                    ? k
                    : _kode;

                try { _kultur = System.Globalization.CultureInfo.GetCultureInfo(navn); }
                catch (System.Globalization.CultureNotFoundException)
                {
                    _kultur = System.Globalization.CultureInfo.CurrentCulture;
                }

                return _kultur;
            }
        }
    }

    /// <summary>Koden på det sprog, der vises nu.</summary>
    public static string Kode
    {
        get { lock (_laas) { Sikr(); return _kode; } }
    }

    /// <summary>
    /// Teksten bag nøglen.
    ///
    /// Findes nøglen ikke i nogen af de to filer, kommer nøglen selv tilbage.
    /// Det er med vilje synligt grimt: en manglende nøgle skal opdages, mens
    /// man bygger, ikke af en kunde.
    /// </summary>
    public static string T(string noegle)
    {
        if (string.IsNullOrWhiteSpace(noegle)) return "";

        lock (_laas)
        {
            Sikr();

            if (_valgt!.TryGetValue(noegle, out var tekst) && tekst.Length > 0) return tekst;
            if (_kilde!.TryGetValue(noegle, out var dansk) && dansk.Length > 0) return dansk;

            return noegle;
        }
    }

    /// <summary>
    /// Teksten bag nøglen, med indsatte værdier: <c>{0}</c>, <c>{1}</c> og så
    /// videre — som i en almindelig formatstreng.
    ///
    /// Går formateringen galt — en oversætter har skrevet <c>{2}</c> i en
    /// tekst med to værdier — kommer den uformaterede tekst tilbage. En
    /// forkert oversættelse må ikke kunne vælte skærmen.
    /// </summary>
    public static string T(string noegle, params object?[] vaerdier)
    {
        var tekst = T(noegle);

        try { return string.Format(tekst, vaerdier); }
        catch (FormatException) { return tekst; }
    }

    /// <summary>Skifter sprog og melder det. Ukendt kode gør ingenting.</summary>
    public static void Skift(string kode)
    {
        if (string.IsNullOrWhiteSpace(kode)) return;

        lock (_laas)
        {
            // SIKR FOERST.
            //
            // Uden den her sammenlignes der mod _kode, som er "da", indtil
            // noget har laest filerne - ogsaa hvis indstillingen siger noget
            // andet. Saa ville et skift til det sprog, der ALLEREDE var
            // valgt i indstillingerne, blive sprunget over, og valget aldrig
            // skrevet. Fanget af en proeve 24-08-2026.
            Sikr();

            if (string.Equals(kode, _kode, StringComparison.OrdinalIgnoreCase)) return;

            var fil = Filen(kode);
            if (fil is null) return;

            _valgt = Laes(fil);
            _kode = kode.ToLowerInvariant();
            _kultur = null;
        }

        var s = AppSettings.Current;
        s.Sprog = _kode;
        s.Save();

        Aendret?.Invoke();
    }

    /// <summary>
    /// Sprogene, der ligger i mappen.
    ///
    /// Hvert sprog beskriver sig selv med <c>_sprog.navn</c> og
    /// <c>_sprog.flag</c>, så en ny fil kommer med i menuen uden at nogen
    /// skal rette i en liste et andet sted.
    /// </summary>
    public static List<Sprogvalg> Tilgaengelige()
    {
        Udpak();

        var ud = new List<Sprogvalg>();

        try
        {
            foreach (var fil in Directory.EnumerateFiles(Mappe, "*.json").OrderBy(f => f))
            {
                var kode = Path.GetFileNameWithoutExtension(fil).ToLowerInvariant();
                var tekster = Laes(fil);

                if (tekster.Count == 0) continue;

                tekster.TryGetValue("_sprog.navn", out var navn);
                tekster.TryGetValue("_sprog.flag", out var flag);

                ud.Add(new Sprogvalg(
                    kode,
                    string.IsNullOrWhiteSpace(navn) ? kode.ToUpperInvariant() : navn,
                    string.IsNullOrWhiteSpace(flag) ? kode.ToUpperInvariant() : flag));
            }
        }
        catch (IOException) { }

        // Kildesproget foerst, resten alfabetisk. Dansk er det, appen er
        // skrevet paa, og det, alt andet maales imod.
        return ud.OrderByDescending(s => s.Kode == Kilde)
                 .ThenBy(s => s.Navn, StringComparer.CurrentCulture)
                 .ToList();
    }

    /// <summary>Tvinger næste opslag til at læse filerne forfra — efter en rettelse i hånden.</summary>
    public static void Genindlaes()
    {
        // KODEN SLIPPES OGSAA. Stod den tilbage, ville Sprog paastaa et andet
        // sprog end det, filerne og indstillingen siger - og Skift ville
        // springe over netop det sprog, der skulle vaelges.
        lock (_laas) { _valgt = null; _kilde = null; _kode = Kilde; _kultur = null; }
        Aendret?.Invoke();
    }

    // ===================== INDMADEN =====================

    private static void Sikr()
    {
        if (_valgt is not null && _kilde is not null) return;

        Udpak();

        var oensket = AppSettings.Current.Sprog;
        if (string.IsNullOrWhiteSpace(oensket)) oensket = Kilde;

        var fil = Filen(oensket) ?? Filen(Kilde);

        _kode = fil is null ? Kilde : Path.GetFileNameWithoutExtension(fil).ToLowerInvariant();
        _kultur = null;
        _valgt = fil is null ? new() : Laes(fil);

        var kildefil = Filen(Kilde);
        _kilde = kildefil is null ? new() : Laes(kildefil);
    }

    private static string? Filen(string kode)
    {
        try
        {
            var sti = Path.Combine(Mappe, kode.ToLowerInvariant() + ".json");
            return File.Exists(sti) ? sti : null;
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Lægger de sprog, der følger med appen, i mappen — én gang.
    ///
    /// Findes filen i forvejen, røres den ikke. Har nogen rettet i den danske
    /// tekst, skal en opdatering af appen ikke skrive rettelsen væk.
    /// </summary>
    private static void Udpak()
    {
        try
        {
            Directory.CreateDirectory(Mappe);

            var samling = Assembly.GetExecutingAssembly();

            foreach (var navn in samling.GetManifestResourceNames())
            {
                if (!navn.Contains(".sprog.", StringComparison.OrdinalIgnoreCase)) continue;
                if (!navn.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                var filnavn = navn[(navn.LastIndexOf(".sprog.", StringComparison.OrdinalIgnoreCase)
                                    + ".sprog.".Length)..];

                var maal = Path.Combine(Mappe, filnavn);
                if (File.Exists(maal)) continue;

                using var stroem = samling.GetManifestResourceStream(navn);
                if (stroem is null) continue;

                using var laeser = new StreamReader(stroem, Encoding.UTF8);
                File.WriteAllText(maal, laeser.ReadToEnd(), new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // Kan mappen ikke skrives, koerer appen paa noeglerne. Det er
            // grimt, men den starter - og en app, der ikke kan starte, fordi
            // en sprogfil ikke kunne skrives, er vaerre.
        }
    }

    /// <summary>
    /// Læser en sprogfil. Fladt eller indlejret JSON — begge dele virker.
    ///
    /// Indlejret er det, de fleste i18n-værktøjer skriver, og fladt er det,
    /// der er lettest at læse i hånden. At tage imod begge dele koster ti
    /// linjer og sparer en oversætter for en fejl, der ikke siger noget.
    /// </summary>
    private static Dictionary<string, string> Laes(string sti)
    {
        var ud = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doku = JsonDocument.Parse(File.ReadAllText(sti, Encoding.UTF8));
            Flad(doku.RootElement, "", ud);
        }
        catch (JsonException) { }
        catch (IOException) { }

        return ud;
    }

    private static void Flad(JsonElement element, string praefiks, Dictionary<string, string> ud)
    {
        if (element.ValueKind != JsonValueKind.Object) return;

        foreach (var felt in element.EnumerateObject())
        {
            var noegle = praefiks.Length == 0 ? felt.Name : praefiks + "." + felt.Name;

            switch (felt.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    Flad(felt.Value, noegle, ud);
                    break;

                case JsonValueKind.String:
                    ud[noegle] = felt.Value.GetString() ?? "";
                    break;

                // Tal og sandhedsvaerdier er ikke tekst og hoerer ikke til her.
                // De springes over frem for at blive lavet om til strenge, saa
                // en fejl i en fil ikke bliver til en maerkelig tekst.
            }
        }
    }
}
