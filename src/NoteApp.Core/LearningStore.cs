using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NoteApp.Core;

public sealed record AliasRule(string Normalized, string Canonical, int WordCount);

/// <summary>
/// Én rettelse, som den vises: «det her hørte appen» → «det her skal der stå».
///
/// Det er den eneste form for læring i appen, der er målt til at virke.
/// Ordlisten, der lå her før, gjorde beviseligt ingenting.
/// </summary>
public sealed record Rettelse(
    string Normalized,
    string Hørt,
    string Rigtigt,
    int Gange,
    string? SidstSet,
    int Ordantal);

/// <summary>
/// Kategorien på en term.
///
/// Der var engang fem, og de STYREDE, hvad der kom med i Whispers prompt, når
/// ordbogen var større end der var plads til. Prompten er væk — den blev målt
/// til ingen forskel — og dermed er kategorien uden konsekvens. Den ene
/// tilbage findes kun, fordi skemaets kolonne er NOT NULL.
/// </summary>
public static class TermCategories
{
    public const string Fagterm = "fagterm";
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
    //
    // HVAD DER BLEV AF ORDLISTEN
    //
    // Der var engang en ordbog her: man skrev sine fagord og navne ind, og de
    // blev sendt med til Whisper som en ledetråd. Det blev målt, og forskellen
    // var NUL — den samme optagelse gav den samme udskrift, med og uden. Værre
    // endnu skrev Whisper prompten ind i teksten under pauser: 372 af 388
    // linjer blev til den samme sætning om og om igen.
    //
    // Ordlisten er derfor væk som funktion. En app må ikke tilbyde et
    // håndtag, der ikke er forbundet til noget — 41 ord, der intet gjorde,
    // så ud som om appen blev bedre, hver gang man skrev et til.
    //
    // Termen findes stadig HERINDE, men kun som den ene halvdel af en
    // rettelse: den rigtige stavemåde, et alias peger hen på. Det, der virker,
    // er rettelserne, og det er dem, brugeren ser.

    /// <summary>
    /// Den rigtige stavemåde bag en rettelse. Oprettes af
    /// <see cref="LearnCorrection"/> og har ingen selvstændig brugerflade.
    /// </summary>
    internal long AddTerm(string canonical, string category, double weight = 1.0, string? scope = null, string? hint = null)
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
    /// Alle rettelser, nyeste først: hvad der blev hørt, hvad det bliver
    /// rettet til, og hvor mange gange den fejl er set.
    ///
    /// Det er hele det, brugeren har at skrue på. Alt andet i denne fil er
    /// enten historik eller den plumbing, rettelserne står på.
    /// </summary>
    public IReadOnlyList<Rettelse> ListRettelser(string? søg = null)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT a.normalized, a.heard, t.canonical, a.occurrences, a.last_seen_at, a.word_count
            FROM alias a JOIN term t ON t.id = a.term_id
            WHERE a.auto_apply = 1
              AND ($q IS NULL OR a.heard LIKE '%' || $q || '%' OR t.canonical LIKE '%' || $q || '%')
            ORDER BY a.last_seen_at DESC, t.canonical COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("$q", string.IsNullOrWhiteSpace(søg) ? DBNull.Value : søg);

        var liste = new List<Rettelse>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            liste.Add(new Rettelse(
                r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3),
                r.IsDBNull(4) ? null : r.GetString(4), r.GetInt32(5)));
        }
        return liste;
    }

    /// <summary>
    /// Slår en rettelse fra.
    ///
    /// Rækken slettes ikke — <c>auto_apply</c> sættes til 0. En rettelse, der
    /// viste sig at ramme forkert, er værd at kunne se, at man HAR haft; en
    /// tom database fortæller ikke, hvorfor man holdt op med at bruge den.
    /// Termen bliver også liggende, hvis den bærer andre rettelser.
    /// </summary>
    public void SletRettelse(string normalized)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE alias SET auto_apply = 0 WHERE normalized = $n;";
        cmd.Parameters.AddWithValue("$n", normalized);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Rydder de termer op, der ikke bærer nogen rettelse.
    ///
    /// De er efterladt fra dengang, ordbogen var en ordliste, man skrev i. De
    /// gør ingenting — hverken godt eller skidt — men de tæller med i
    /// «Termer: 41» og får det til at se ud, som om der er noget derinde, der
    /// virker. Returnerer, hvor mange der blev fjernet.
    /// </summary>
    public int RydOrdliste()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM term
             WHERE id NOT IN (SELECT DISTINCT term_id FROM alias);";
        return cmd.ExecuteNonQuery();
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
    /// <summary>
    /// Ord i ordbogen, der ligner det, man er ved at skrive.
    ///
    /// Findes for at forhindre dubletter. Ordbogen havde «Active Directory»,
    /// «adgangsafstemning» og «attestering» to gange hver — de blev oprettet
    /// under hver sin kategori, uden at nogen kunne se, at ordet var der i
    /// forvejen. Et opslag, der først sker ved gem, er for sent.
    /// </summary>
    public IReadOnlyList<string> Foreslaa(string begyndelse, int maks = 8)
    {
        if (string.IsNullOrWhiteSpace(begyndelse)) return Array.Empty<string>();

        using var cmd = _db.CreateCommand();

        // Ord der BEGYNDER med det skrevne foerst, derefter ord der indeholder
        // det. Man leder efter begyndelsen af et ord, ikke midten.
        cmd.CommandText = @"
            SELECT canonical FROM term
            WHERE canonical LIKE $b || '%' COLLATE NOCASE
            UNION
            SELECT canonical FROM term
            WHERE canonical LIKE '%' || $b || '%' COLLATE NOCASE
              AND canonical NOT LIKE $b || '%' COLLATE NOCASE
            LIMIT $n;";
        cmd.Parameters.AddWithValue("$b", begyndelse.Trim());
        cmd.Parameters.AddWithValue("$n", maks);

        var liste = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) liste.Add(r.GetString(0));
        return liste;
    }

    /// <summary>
    /// Slår ord sammen, der står flere gange med samme stavemåde.
    ///
    /// Dubletterne stammer fra kategorierne: det samme ord kunne oprettes én
    /// gang som fagterm og én gang som produkt. Kategorien er væk, og så er
    /// der ingen grund til, at ordet står to steder.
    ///
    /// Den ældste post beholdes — den har historikken — og de øvriges aliaser
    /// flyttes over, før de slettes. Returnerer hvor mange der blev slået
    /// sammen.
    /// </summary>
    public int SlaaDubletterSammen()
    {
        using var find = _db.CreateCommand();
        find.CommandText = @"
            SELECT LOWER(canonical) AS n, COUNT(*) AS antal
            FROM term GROUP BY n HAVING antal > 1;";

        var navne = new List<string>();
        using (var r = find.ExecuteReader())
            while (r.Read()) navne.Add(r.GetString(0));

        var sammenlagt = 0;

        foreach (var navn in navne)
        {
            using var hent = _db.CreateCommand();
            hent.CommandText = "SELECT id FROM term WHERE LOWER(canonical) = $n ORDER BY id;";
            hent.Parameters.AddWithValue("$n", navn);

            var ider = new List<long>();
            using (var r = hent.ExecuteReader())
                while (r.Read()) ider.Add(r.GetInt64(0));

            if (ider.Count < 2) continue;

            var beholdes = ider[0];

            foreach (var fjernes in ider.Skip(1))
            {
                using var flyt = _db.CreateCommand();

                // Aliaser flyttes med. En variant, der er hoert tre gange paa
                // den ene post, maa ikke gaa tabt, fordi posten forsvinder.
                // OR IGNORE: findes varianten allerede paa den beholdte, er
                // der intet at flytte.
                flyt.CommandText = @"
                    UPDATE OR IGNORE alias SET term_id = $b WHERE term_id = $f;
                    UPDATE correction SET term_id = $b WHERE term_id = $f;
                    DELETE FROM alias WHERE term_id = $f;
                    DELETE FROM term  WHERE id = $f;";
                flyt.Parameters.AddWithValue("$b", beholdes);
                flyt.Parameters.AddWithValue("$f", fjernes);
                flyt.ExecuteNonQuery();

                sammenlagt++;
            }
        }

        return sammenlagt;
    }

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

    // ---------------------------------------------- ordlisten til Whisper: væk
    //
    // Her stod BuildWhisperPrompt: den byggede en ledetråd af ordbogen og
    // sendte den med til Whisper, med et token-budget, en prioritering af
    // navne først, og en udluftning af dubletter pr. kunde.
    //
    // Den blev målt. Forskellen var NUL — samme lyd, samme udskrift, med og
    // uden. Og prompten kunne gøre skade: Whisper skrev den ind i teksten
    // under pauser, 372 af 388 linjer blev den samme sætning.
    //
    // Halvfjerds linjer omhyggelig kode, der ikke gjorde noget. Den er væk,
    // så ingen bygger videre på den i troen på, at den virker.

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
