using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NoteApp.Core;

public sealed record PromptTerm(string Canonical, string Category, string? Hint);

public sealed record AliasRule(string Normalized, string Canonical, int WordCount);

/// <summary>En række i ordbogen, som den vises og redigeres i UI'et.</summary>
public sealed record TermRow(
    long Id,
    string Canonical,
    string Category,
    double Weight,
    string? Scope,
    string? Hint,
    bool Active,
    int AliasCount);

/// <summary>
/// De fem kategorier fra skemaet, med de navne et menneske genkender.
///
/// Kategorien er ikke en etiket til at sortere efter — den STYRER, hvad der
/// kommer med i Whispers prompt, når ordbogen er større end de ca. 224 tokens
/// der er plads til. Personer og organisationer vælges først, fordi det er
/// navne, modellen oftest staver forkert. Derfor er der heller ikke flere
/// kategorier end disse: en kategori uden konsekvens for udvælgelsen ville
/// kun være ekstra arbejde ved indtastning.
/// </summary>
public static class TermCategories
{
    public const string Person = "person";
    public const string Organisation = "organisation";
    public const string Produkt = "produkt";
    public const string Fagterm = "fagterm";
    public const string Forkortelse = "forkortelse";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Person, Organisation, Produkt, Fagterm, Forkortelse
    };

    public static string Label(string category) => category switch
    {
        Person => "Person",
        Organisation => "Organisation",
        Produkt => "Produkt",
        Fagterm => "Fagterm",
        Forkortelse => "Forkortelse",
        _ => category
    };

    public static string Explanation(string category) => category switch
    {
        Person => "Kollegaer, kunder, mødedeltagere. Vælges FØRST til prompten — navne er dem, Whisper oftest staver forkert.",
        Organisation => "Firmaer, myndigheder, afdelinger. Vælges næst efter personer.",
        Produkt => "Produkt- og systemnavne, fx Entra ID.",
        Fagterm => "Fagudtryk i din branche, fx provisionering, attestering.",
        Forkortelse => "SCIM, IAM, JML. Udtales tit som ord og bliver derfor stavet forkert.",
        _ => ""
    };
}

/// <summary>
/// Den lokale læring: ordbogen, de hørte varianter og rettelseshistorikken.
///
/// Klassen kender intet til Whisper, modelfiler eller versioner. engine_id
/// gemmes som proveniens — hvilken motor lavede fejlen — men bruges aldrig
/// som betingelse for om en rettelse må anvendes. Det er dét, der gør
/// Whisper udskiftelig uden tab af læring. Se doc\laering-og-vedligehold.md.
/// </summary>
public sealed class LearningStore : IDisposable
{
    private readonly SqliteConnection _db;

    public string Path { get; }

    public LearningStore(string? path = null)
    {
        Path = path ?? UserDataPaths.LearningDatabase;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

        _db = new SqliteConnection($"Data Source={Path}");
        _db.Open();
        ApplySchema();
    }

    private void ApplySchema()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("NoteApp.Core.learning.sql")
            ?? throw new InvalidOperationException("learning.sql er ikke indlejret i assemblyen.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        Execute(reader.ReadToEnd());
    }

    private void Execute(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ------------------------------------------------------------- ordbogen

    public long AddTerm(string canonical, string category, double weight = 1.0, string? scope = null, string? hint = null)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO term (canonical, category, weight, scope, pronunciation_hint)
            VALUES ($c, $k, $w, $s, $h)
            ON CONFLICT (canonical, scope) DO UPDATE SET
                weight = excluded.weight,
                pronunciation_hint = COALESCE(excluded.pronunciation_hint, term.pronunciation_hint),
                active = 1
            RETURNING id;";
        cmd.Parameters.AddWithValue("$c", canonical);
        cmd.Parameters.AddWithValue("$k", category);
        cmd.Parameters.AddWithValue("$w", weight);
        cmd.Parameters.AddWithValue("$s", (object?)scope ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$h", (object?)hint ?? DBNull.Value);
        return (long)cmd.ExecuteScalar()!;
    }

    /// <summary>
    /// Hele ordbogen til redigering. Inaktive termer kommer med, så de kan
    /// aktiveres igen — en term man har slået fra, er sjældent en fejl man
    /// vil have slettet.
    /// </summary>
    public IReadOnlyList<TermRow> ListTerms(string? search = null, string? category = null)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT t.id, t.canonical, t.category, t.weight, t.scope,
                   t.pronunciation_hint, t.active,
                   (SELECT COUNT(*) FROM alias a WHERE a.term_id = t.id)
            FROM term t
            WHERE ($q IS NULL OR t.canonical LIKE '%' || $q || '%')
              AND ($k IS NULL OR t.category = $k)
            ORDER BY
                CASE t.category
                    WHEN 'person' THEN 0 WHEN 'organisation' THEN 1 ELSE 2 END,
                t.canonical COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("$q", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search);
        cmd.Parameters.AddWithValue("$k", string.IsNullOrWhiteSpace(category) ? DBNull.Value : category);

        var liste = new List<TermRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            liste.Add(new TermRow(
                r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetDouble(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetInt32(6) == 1,
                r.GetInt32(7)));
        }
        return liste;
    }

    public void UpdateTerm(long id, string canonical, string category, double weight,
                           string? scope, string? hint, bool active)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            UPDATE term
               SET canonical = $c, category = $k, weight = $w,
                   scope = $s, pronunciation_hint = $h, active = $a
             WHERE id = $id;";
        cmd.Parameters.AddWithValue("$c", canonical);
        cmd.Parameters.AddWithValue("$k", category);
        cmd.Parameters.AddWithValue("$w", weight);
        cmd.Parameters.AddWithValue("$s", (object?)scope ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$h", (object?)hint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$a", active ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Sletter en term og dens aliaser. Rettelseshistorikken bevares —
    /// correction.term_id er ON DELETE SET NULL, fordi en rettelse er en
    /// kendsgerning om et bestemt møde og skal kunne læses tilbage, også
    /// efter ordbogen er ryddet op.
    /// </summary>
    public void DeleteTerm(long id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM term WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyDictionary<string, int> CountByCategory()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT category, COUNT(*) FROM term WHERE active = 1 GROUP BY category;";
        var d = new Dictionary<string, int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) d[r.GetString(0)] = r.GetInt32(1);
        return d;
    }

    /// <summary>
    /// Skriver den prompt, Whisper faktisk skal bruge, til en fil. Det er
    /// filen, Fase 0-scriptet læser — ordbogen er kilden, filen er et
    /// øjebliksbillede af den.
    /// </summary>
    public string ExportVocabularyFile(string? path = null, string? scope = null, int tokenBudget = 200)
    {
        var mål = path ?? UserDataPaths.Vocabulary;
        var prompt = BuildWhisperPrompt(scope, tokenBudget);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(mål)!);
        File.WriteAllText(mål, prompt, new UTF8Encoding(false));
        return mål;
    }

    /// <summary>
    /// Registrerer at motoren skrev <paramref name="heard"/>, hvor der skulle
    /// have stået termen. Tæller op, hvis varianten er set før.
    /// </summary>
    public void RecordAlias(long termId, string heard)
    {
        var normalized = Normalize(heard);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO alias (term_id, heard, normalized, word_count)
            VALUES ($t, $h, $n, $w)
            ON CONFLICT (term_id, normalized) DO UPDATE SET
                occurrences  = alias.occurrences + 1,
                last_seen_at = datetime('now');";
        cmd.Parameters.AddWithValue("$t", termId);
        cmd.Parameters.AddWithValue("$h", heard);
        cmd.Parameters.AddWithValue("$n", normalized);
        cmd.Parameters.AddWithValue("$w", words);
        cmd.ExecuteNonQuery();
    }

    public void RecordCorrection(string meetingId, string segmentId, string heard, string corrected,
                                 string engineId, string source, long? termId = null)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO correction (meeting_id, segment_id, heard, corrected, engine_id, source, term_id)
            VALUES ($m, $s, $h, $c, $e, $src, $t);";
        cmd.Parameters.AddWithValue("$m", meetingId);
        cmd.Parameters.AddWithValue("$s", segmentId);
        cmd.Parameters.AddWithValue("$h", heard);
        cmd.Parameters.AddWithValue("$c", corrected);
        cmd.Parameters.AddWithValue("$e", engineId);
        cmd.Parameters.AddWithValue("$src", source);
        cmd.Parameters.AddWithValue("$t", (object?)termId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Varianter der er set nok gange til at appen bør spørge, om den må rette
    /// dem automatisk fremover. Forfremmelse sker aldrig af sig selv.
    /// </summary>
    public IReadOnlyList<AliasRule> AliasesReadyToPromote(int threshold = 3)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT a.normalized, t.canonical, a.word_count
            FROM alias a JOIN term t ON t.id = a.term_id
            WHERE a.auto_apply = 0 AND a.rejected_at IS NULL AND a.occurrences >= $n
            ORDER BY a.occurrences DESC;";
        cmd.Parameters.AddWithValue("$n", threshold);
        return ReadAliases(cmd);
    }

    /// <summary>
    /// Godkender en variant til automatisk rettelse fremover — eller afviser
    /// den, så appen holder op med at spørge om den.
    ///
    /// Forfremmelse sker ALDRIG af sig selv. En regel, appen har fundet på,
    /// ville rette i noget, ingen har set efter, og en forkert regel er værre
    /// end den hørefejl, den skulle rette: hørefejlen ser man, rettelsen
    /// ligner det rigtige ord.
    /// </summary>
    public void SetAliasAutoApply(long termId, string heard, bool godkendt)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = godkendt
            ? "UPDATE alias SET auto_apply = 1, rejected_at = NULL WHERE term_id = $t AND normalized = $n;"
            : "UPDATE alias SET auto_apply = 0, rejected_at = datetime('now') WHERE term_id = $t AND normalized = $n;";
        cmd.Parameters.AddWithValue("$t", termId);
        cmd.Parameters.AddWithValue("$n", Normalize(heard));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Slår en term op på dens kanoniske form. Bruges når en rettelse skrives
    /// ind: «det her ord skal hedde det her», hvor termen findes i forvejen.
    /// </summary>
    public long? FindTermId(string canonical)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id FROM term WHERE canonical = $c COLLATE NOCASE LIMIT 1;";
        cmd.Parameters.AddWithValue("$c", canonical);
        var r = cmd.ExecuteScalar();
        return r is null or DBNull ? null : Convert.ToInt64(r);
    }

    /// <summary>
    /// Registrerer en rettelse og gør den til en regel med det samme.
    ///
    /// Bruges, når rettelsen kommer fra et menneske, der har set både det
    /// hørte og det rigtige — dér er der ikke noget at være i tvivl om.
    /// Termen oprettes, hvis den ikke findes i forvejen.
    /// </summary>
    public long LearnCorrection(string heard, string canonical, string category = "fagterm",
                                string engineId = "manuel", string meetingId = "")
    {
        var termId = FindTermId(canonical) ?? AddTerm(canonical, category);
        RecordAlias(termId, heard);
        SetAliasAutoApply(termId, heard, true);
        RecordCorrection(meetingId, "", heard, canonical, engineId, "manuel", termId);
        return termId;
    }

    public IReadOnlyList<AliasRule> AutoApplyRules()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT a.normalized, t.canonical, a.word_count
            FROM alias a JOIN term t ON t.id = a.term_id
            WHERE a.auto_apply = 1
            ORDER BY a.word_count DESC;";   // længste match først
        return ReadAliases(cmd);
    }

    private static List<AliasRule> ReadAliases(SqliteCommand cmd)
    {
        var liste = new List<AliasRule>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) liste.Add(new AliasRule(r.GetString(0), r.GetString(1), r.GetInt32(2)));
        return liste;
    }

    // ------------------------------------------------------- Whispers prompt

    /// <summary>
    /// Bygger ordlisten til Whispers initial_prompt.
    ///
    /// Whisper har kun plads til omkring 224 tokens, og overfyldes prompten,
    /// begynder modellen at skrive den ind i transskriptionen under pauser.
    /// Derfor vælges der: navne først, så vægt, så hvad der senest har været
    /// i brug — indtil budgettet er brugt.
    ///
    /// Token-loftet gælder kun denne vej ud af ordbogen. Lokale forbrugere —
    /// efterbehandling, autocomplete i navngivnings-UI'et — kan læse hele
    /// datalaget direkte og skal ikke igennem prioriteringen her.
    /// </summary>
    public string BuildWhisperPrompt(string? scope = null, int tokenBudget = 200)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT canonical, category, pronunciation_hint, scope
            FROM prompt_kandidat
            WHERE scope IS NULL OR scope = $s;";
        cmd.Parameters.AddWithValue("$s", (object?)scope ?? DBNull.Value);

        var kandidater = new List<(string Canonical, string Category, string? Hint, string? Scope)>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                kandidater.Add((r.GetString(0), r.GetString(1),
                                r.IsDBNull(2) ? null : r.GetString(2),
                                r.IsDBNull(3) ? null : r.GetString(3)));
        }

        // En term kan findes både globalt og pr. kunde. Uden denne udluftning
        // optager samme ord to pladser i et budget, der i forvejen er for
        // lille. Den kundespecifikke vinder.
        var valgte = kandidater
            .GroupBy(k => k.Canonical, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(k => k.Scope is not null).First())
            .ToList();

        var sb = new StringBuilder("Vi taler om ");
        var brugt = EstimateTokens(sb.ToString());
        var tilføjet = 0;

        foreach (var k in valgte)
        {
            var stykke = k.Hint is null ? k.Canonical : $"{k.Canonical} ({k.Hint})";
            var pris = EstimateTokens(stykke) + 1;
            if (brugt + pris > tokenBudget) break;

            if (tilføjet > 0) sb.Append(", ");
            sb.Append(stykke);
            brugt += pris;
            tilføjet++;
        }

        if (tilføjet == 0) return string.Empty;
        sb.Append('.');
        return sb.ToString();
    }

    /// <summary>
    /// Groft skøn: dansk tekst lander omkring 3 tegn pr. token hos Whisper.
    /// Bevidst konservativt — at underudnytte budgettet koster lidt kvalitet,
    /// at overskride det kan få modellen til at hallucinere prompten.
    /// </summary>
    public static int EstimateTokens(string text) => (int)Math.Ceiling(text.Length / 3.0);

    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-' or '/') sb.Append(' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // ------------------------------------------------------------ statistik

    /// <summary>
    /// Rettelser pr. motor. Grundlaget for at måle om en Whisper-opdatering
    /// faktisk hjalp på dit domæne: kør gamle møder igennem igen og se, om
    /// tallet falder.
    /// </summary>
    public IReadOnlyList<(string EngineId, int Corrections, int Meetings)> CorrectionsByEngine()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT engine_id, antal_rettelser, antal_moeder FROM rettelser_pr_motor;";
        var liste = new List<(string, int, int)>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) liste.Add((r.GetString(0), r.GetInt32(1), r.GetInt32(2)));
        return liste;
    }

    public int TermCount()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM term WHERE active = 1;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Læser en ordliste i fritekst ind som fagtermer. Bruges til at flytte
    /// den statiske ordliste.txt ind i ordbogen første gang.
    /// </summary>
    public int ImportVocabularyFile(string path)
    {
        if (!File.Exists(path)) return 0;

        var tekst = File.ReadAllText(path, Encoding.UTF8);
        var kandidater = tekst
            .Split(new[] { ',', '.', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 2 && !s.StartsWith("Vi taler om", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var antal = 0;
        foreach (var k in kandidater)
        {
            var kategori = k.All(c => char.IsUpper(c) || char.IsDigit(c)) ? "forkortelse" : "fagterm";
            AddTerm(k, kategori);
            antal++;
        }
        return antal;
    }

    public void Dispose()
    {
        _db.Close();
        _db.Dispose();
        SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path}"));
    }
}
