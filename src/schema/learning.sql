-- NoteApp — lokal læring.
--
-- GARANTIEN: denne fil indeholder INGEN modelartefakter. Kun tekst.
-- Den kan kopieres til en maskine med en anden Whisper-version, en anden
-- ASR-motor eller ingen model overhovedet, og stadig give mening.
--
-- engine_id optræder flere steder. Det er ALTID proveniens — hvilken motor
-- lavede denne tekst, denne fejl — og ALDRIG en betingelse for om noget må
-- bruges. En rettelse lært under large-v3 gælder også under efterfølgeren.
-- Skriver man nogensinde kode der filtrerer på engine_id ved anvendelse af
-- rettelser, er garantien brudt.
--
-- Se doc\laering-og-vedligehold.md.

PRAGMA foreign_keys = ON;

-- ---------------------------------------------------------------------------
-- Ordbogen: de kanoniske former, appen skal kende
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS term (
    id                  INTEGER PRIMARY KEY,
    canonical           TEXT    NOT NULL,
    category            TEXT    NOT NULL
        CHECK (category IN ('person', 'organisation', 'produkt', 'fagterm', 'forkortelse')),

    -- Fritekst til Whispers prompt, fx "udtales 'skim'". Bruges kun hvis
    -- termen alene ikke er nok til at konditionere modellen.
    pronunciation_hint  TEXT,

    -- Styrer prioriteringen når promptbudgettet er for lille til alle termer.
    -- Højere vægt = kommer med først.
    weight              REAL    NOT NULL DEFAULT 1.0,

    -- Termer knyttet til en bestemt kunde eller organisation vælges kun til
    -- prompten når mødet handler om dem. NULL = altid relevant.
    scope               TEXT,

    active              INTEGER NOT NULL DEFAULT 1,
    created_at          TEXT    NOT NULL DEFAULT (datetime('now')),
    last_seen_at        TEXT,

    UNIQUE (canonical, scope)
);

CREATE INDEX IF NOT EXISTS ix_term_udvaelgelse ON term (active, weight DESC, last_seen_at DESC);

-- ---------------------------------------------------------------------------
-- Aliaser: hvad motoren FAKTISK skrev, når den mente en term
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alias (
    id              INTEGER PRIMARY KEY,
    term_id         INTEGER NOT NULL REFERENCES term(id) ON DELETE CASCADE,

    heard           TEXT    NOT NULL,   -- ordret, som det stod
    normalized      TEXT    NOT NULL,   -- små bogstaver, uden tegnsætning

    occurrences     INTEGER NOT NULL DEFAULT 1,

    -- 0 = foreslå kun, 1 = ret automatisk.
    -- Forfremmelse sker ALDRIG af sig selv. Appen spørger efter tredje
    -- forekomst; tavshed betyder nej. Blind soeg-og-erstat er farligere end
    -- den fejl den retter — "skim" er et rigtigt dansk ord.
    auto_apply      INTEGER NOT NULL DEFAULT 0,

    -- Sat når brugeren aktivt har afvist automatisk rettelse. Så spørger
    -- appen ikke igen.
    rejected_at     TEXT,

    -- Enkeltordsaliaser er farligere end flerords, fordi de oftere kolliderer
    -- med rigtige ord. Udregnes ved indsættelse, så anvendelseslaget kan være
    -- strengere med dem uden at skulle parse teksten igen.
    word_count      INTEGER NOT NULL DEFAULT 1,

    first_seen_at   TEXT    NOT NULL DEFAULT (datetime('now')),
    last_seen_at    TEXT    NOT NULL DEFAULT (datetime('now')),

    UNIQUE (term_id, normalized)
);

CREATE INDEX IF NOT EXISTS ix_alias_opslag ON alias (normalized, auto_apply);

-- ---------------------------------------------------------------------------
-- Rettelser: revisionsspor over hvad brugeren faktisk rettede
-- ---------------------------------------------------------------------------
-- Dette er kilden til aliaser, men bevares selvstændigt: en rettelse er en
-- kendsgerning om et bestemt møde og skal kunne læses tilbage, også hvis
-- ordbogen senere ryddes op.
CREATE TABLE IF NOT EXISTS correction (
    id              INTEGER PRIMARY KEY,
    meeting_id      TEXT    NOT NULL,
    segment_id      TEXT    NOT NULL,

    heard           TEXT    NOT NULL,
    corrected       TEXT    NOT NULL,

    -- Proveniens: hvilken motor lavede fejlen. Bruges til statistik og til
    -- regressionsmåling ved opdatering — ALDRIG til at afgøre om rettelsen
    -- må anvendes.
    engine_id       TEXT    NOT NULL,

    source          TEXT    NOT NULL
        CHECK (source IN ('manuel', 'foreslaaet-accepteret', 'auto')),

    term_id         INTEGER REFERENCES term(id) ON DELETE SET NULL,
    accepted_at     TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS ix_correction_moede  ON correction (meeting_id);
CREATE INDEX IF NOT EXISTS ix_correction_motor  ON correction (engine_id, accepted_at);

-- ---------------------------------------------------------------------------
-- Transskriptionskørsler: hvilken motor producerede hvad
-- ---------------------------------------------------------------------------
-- Samme møde kan transskriberes flere gange — fx når Whisper opdateres.
-- Den rå tekst overskrives ALDRIG; der kommer en ny række.
CREATE TABLE IF NOT EXISTS transcript_run (
    id              TEXT    PRIMARY KEY,
    meeting_id      TEXT    NOT NULL,

    -- Fx 'whisper.cpp/large-v3/v1.9.2'
    engine_id       TEXT    NOT NULL,

    -- Den prompt der faktisk blev sendt med, ordret. Uden den kan man ikke
    -- fejlsøge hvorfor en kørsel gik anderledes end en anden.
    prompt_used     TEXT,
    prompt_tokens   INTEGER,

    created_at      TEXT    NOT NULL DEFAULT (datetime('now')),
    is_current      INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS ix_run_moede ON transcript_run (meeting_id, is_current);

-- ---------------------------------------------------------------------------
-- Skemaversion
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS schema_version (
    version     INTEGER NOT NULL,
    applied_at  TEXT    NOT NULL DEFAULT (datetime('now'))
);

INSERT INTO schema_version (version) SELECT 1
WHERE NOT EXISTS (SELECT 1 FROM schema_version);

-- ---------------------------------------------------------------------------
-- Udvælgelse til Whispers prompt
-- ---------------------------------------------------------------------------
-- Whisper har plads til ca. 224 tokens, og ordbogen bliver større end det.
-- Rækkefølgen her afgør hvad der kommer med: personer og organisationer
-- først (navne er dem modellen oftest staver forkert), derefter vægt,
-- derefter hvad der senest har været i brug.
--
-- Claude bruger IKKE denne visning — den får hele ordbogen, fordi den ikke
-- har et token-loft der kan gøre skade.
CREATE VIEW IF NOT EXISTS prompt_kandidat AS
SELECT
    t.id,
    t.canonical,
    t.category,
    t.scope,
    t.weight,
    t.pronunciation_hint,
    (SELECT COUNT(*) FROM alias a WHERE a.term_id = t.id) AS antal_aliaser
FROM term t
WHERE t.active = 1
ORDER BY
    CASE t.category WHEN 'person' THEN 0 WHEN 'organisation' THEN 1 ELSE 2 END,
    t.weight DESC,
    t.last_seen_at DESC;

-- ---------------------------------------------------------------------------
-- Regressionsmåling ved motoropdatering
-- ---------------------------------------------------------------------------
-- Tæller hvor mange rettelser der er lært under hver motor. Køres et arkiv
-- af gamle møder igennem en ny Whisper-version, viser faldet i nødvendige
-- rettelser om opdateringen faktisk hjalp PÅ DIT domæne — ikke på en
-- generisk benchmark. Stiger tallet, er opdateringen en regression.
CREATE VIEW IF NOT EXISTS rettelser_pr_motor AS
SELECT
    engine_id,
    COUNT(*)                        AS antal_rettelser,
    COUNT(DISTINCT meeting_id)      AS antal_moeder,
    ROUND(COUNT(*) * 1.0 / NULLIF(COUNT(DISTINCT meeting_id), 0), 1)
                                    AS rettelser_pr_moede,
    MIN(accepted_at)                AS foerste,
    MAX(accepted_at)                AS seneste
FROM correction
GROUP BY engine_id;
