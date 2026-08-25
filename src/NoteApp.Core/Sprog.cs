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
    /// Lægger de sprog, der følger med appen, i mappen — og FLETTER nye
    /// nøgler ind i en fil, der allerede er der.
    ///
    /// HVORFOR DET IKKE ER NOK AT SKRIVE FILEN ÉN GANG
    ///
    /// Første udgave sprang over, hvis filen fandtes. Det beskyttede
    /// brugerens rettelser — og frøs samtidig sproget fast ved den udgave,
    /// appen blev installeret med. En opdatering med 612 nye tekster nåede
    /// aldrig frem, og skærmen viste nøglerne: «faelles.mappe» i stedet for
    /// «MAPPE». Opdaget 25-08-2026 på en rigtig installation.
    ///
    /// SÅDAN VIRKER FLETNINGEN
    ///
    /// Nøgler, der mangler i filen på disken, lægges ind. Nøgler, der er der
    /// i forvejen, røres ALDRIG — så en rettet tekst bliver stående, også
    /// efter en opdatering.
    ///
    /// Det følger af, at en sprogfil er mange små ting frem for ét dokument:
    /// man retter én tekst, ikke filen. Hjælpen håndteres derfor anderledes
    /// — se Hjaelp.Udpak.
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

                using var stroem = samling.GetManifestResourceStream(navn);
                if (stroem is null) continue;

                using var laeser = new StreamReader(stroem, Encoding.UTF8);
                var indhold = laeser.ReadToEnd();

                var maal = Path.Combine(Mappe, filnavn);

                if (!File.Exists(maal))
                {
                    File.WriteAllText(maal, indhold, new UTF8Encoding(false));
                    continue;
                }

                Flet(maal, indhold);
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
    /// Bringer filen på disken i overensstemmelse med den udgivne.
    /// </summary>
    /// <remarks>
    /// DEN TILFØJEDE FØR KUN DET, DER MANGLEDE — og sprang over alt, den
    /// kendte i forvejen. Det betød, at en RETTET tekst aldrig kom frem: nye
    /// nøgler landede, men en formulering, der var lavet om, blev stående som
    /// den var, i al fremtid.
    ///
    /// Set 25-08-2026: appen skrev «kun dokumenter laves i Europa» på skærmen,
    /// mens den udgivne fil havde sagt «i skyen» i flere udgivelser. Filen på
    /// disken var vokset til 84 KB mod den udgivnes 77 — den samlede nøgler op
    /// og slap aldrig af med noget.
    ///
    /// DER ER INTET AT BESKYTTE. Sprogfilerne kan ikke redigeres i
    /// brugerfladen; det er en truffet beslutning, ikke en mangel. Filen på
    /// disken er en KOPI af den udgivne, ikke brugerens ejendom, og så skal
    /// den ligne den.
    ///
    /// Derfor: manglende nøgler lægges ind, ændrede rettes, og nøgler, der
    /// ikke længere udgives, ryddes væk. Filen skrives kun, hvis noget faktisk
    /// blev anderledes — ellers ville hver opstart give den en ny dato uden
    /// grund.
    /// </remarks>
    private static void Flet(string sti, string udgivet)
    {
        try
        {
            var paaDisken = System.Text.Json.Nodes.JsonNode.Parse(
                File.ReadAllText(sti, Encoding.UTF8)) as System.Text.Json.Nodes.JsonObject;

            if (paaDisken is null) return;

            var nyeste = Laes2(udgivet);
            var kendte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            FladNode(paaDisken, "", kendte);

            var aendret = 0;

            foreach (var (noegle, tekst) in nyeste)
            {
                // Uaendret? Lad den vaere. Ellers skrives filen ved hver
                // opstart, uden at noget er anderledes.
                if (kendte.TryGetValue(noegle, out var paaDisk) && paaDisk == tekst) continue;

                Saet(paaDisken, noegle, tekst);
                aendret++;
            }

            // NOEGLER, DER IKKE UDGIVES LAENGERE, RYDDES VAEK.
            //
            // Uden det bliver filen ved med at vokse med hver omdoebning, der
            // nogensinde er lavet - og saa kan man ikke se paa den, hvad appen
            // faktisk bruger. Brugerens fil var 84 KB mod den udgivnes 77.
            foreach (var doed in kendte.Keys.Where(k => !nyeste.ContainsKey(k)).ToList())
            {
                if (Fjern(paaDisken, doed)) aendret++;
            }

            if (aendret == 0) return;

            File.WriteAllText(sti,
                paaDisken.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
        }
        catch (Exception)
        {
            // En oedelagt fil paa disken kan ikke flettes. Den bliver liggende
            // som den er - og Laes springer den over, saa der faldes tilbage
            // paa noeglerne. At skrive oven i en fil, man ikke kunne laese,
            // ville vaere at gaette paa, hvad der stod i den.
        }
    }

    /// <summary>Fjerner en nøgle. Sandt, hvis der faktisk blev fjernet noget.</summary>
    private static bool Fjern(System.Text.Json.Nodes.JsonObject rod, string noegle)
    {
        var dele = noegle.Split('.');
        var p = rod;

        for (var i = 0; i < dele.Length - 1; i++)
        {
            if (p[dele[i]] is not System.Text.Json.Nodes.JsonObject naeste) return false;
            p = naeste;
        }

        return p.Remove(dele[^1]);
    }

    private static void Saet(System.Text.Json.Nodes.JsonObject rod, string noegle, string vaerdi)
    {
        var dele = noegle.Split('.');
        var p = rod;

        for (var i = 0; i < dele.Length - 1; i++)
        {
            if (p[dele[i]] is System.Text.Json.Nodes.JsonObject naeste) { p = naeste; continue; }

            var ny = new System.Text.Json.Nodes.JsonObject();
            p[dele[i]] = ny;
            p = ny;
        }

        p[dele[^1]] = vaerdi;
    }

    private static void FladNode(System.Text.Json.Nodes.JsonObject o, string praefiks,
                                 Dictionary<string, string> ud)
    {
        foreach (var (navn, vaerdi) in o)
        {
            var noegle = praefiks.Length == 0 ? navn : praefiks + "." + navn;

            if (vaerdi is System.Text.Json.Nodes.JsonObject under) FladNode(under, noegle, ud);
            else if (vaerdi is not null) ud[noegle] = vaerdi.ToString();
        }
    }

    /// <summary>Den udgivne fil som flade nøgler. Samme form som Laes, men fra en streng.</summary>
    private static Dictionary<string, string> Laes2(string json)
    {
        var ud = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doku = JsonDocument.Parse(json);
            Flad(doku.RootElement, "", ud);
        }
        catch (JsonException) { }

        return ud;
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
