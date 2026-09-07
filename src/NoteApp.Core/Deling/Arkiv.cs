using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NoteApp.Core.Documents;
using NoteApp.Core.Llm;

namespace NoteApp.Core.Deling;

/// <summary>Hvad et arkiv fylder — og hvor meget af det, der er lyd.</summary>
/// <remarks>
/// LYDEN TÆLLES FOR SIG, fordi den er alt. Målt 07-09-2026 på den stationære:
/// 1.852 MB lyd i 55 filer mod 24,8 MB i de øvrige 297. Et tal for «det
/// hele» fortæller derfor ingenting om, hvad valget koster.
/// </remarks>
public sealed record Arkivtal(int Filer, long Byte, int Lydfiler, long Lydbyte)
{
    public static readonly Arkivtal Intet = new(0, 0, 0, 0);

    public double MB => Byte / 1048576.0;

    public double LydMB => Lydbyte / 1048576.0;

    /// <summary>Alt det, der ikke er lyd. Det er dét, der næsten intet fylder.</summary>
    public double LetMB => (Byte - Lydbyte) / 1048576.0;

    public bool Noget => Filer > 0;
}

/// <summary>Én fil på vej til eller fra arkivet.</summary>
public sealed record Arkivfil(string Kilde, string Maal, long Byte, bool Lyd, DateTimeOffset Roert);

/// <summary>Hvor langt arkiveringen er nået. Meldes til skærmen undervejs.</summary>
/// <param name="Fil">Filens navn, som mennesker kender det.</param>
/// <param name="Nummer">Hvilken fil i rækken — 1-baseret.</param>
/// <param name="Ialt">Hvor mange der er i alt.</param>
/// <param name="Sendt">Byte kopieret i ALT, ikke kun i den her fil.</param>
/// <param name="Byte">Byte i alt, der skal kopieres.</param>
public sealed record Arkivfremdrift(string Fil, int Nummer, int Ialt, long Sendt, long Byte)
{
    /// <summary>0–100. Regnet på byte og ikke på filer.</summary>
    /// <remarks>
    /// EN TÆLLER PÅ FILER STÅR STILLE I FEM MINUTTER, mens en lydfil på 200 MB
    /// bliver kopieret, og springer så tre streger på ét sekund, når tre
    /// tekstfiler følger efter. Byte bevæger sig jævnt, og det er dét, en
    /// fremdriftsviser er til for.
    /// </remarks>
    public double Procent => Byte <= 0 ? 100 : Math.Min(100, Sendt * 100.0 / Byte);
}

/// <summary>Et arkiv, som det ligger i den fælles mappe.</summary>
public sealed record Arkivmaerke(
    string Id,
    string Navn,
    Maskinrolle Rolle,
    DateTimeOffset Sidst,
    int Filer,
    long Byte,
    bool MedLyd,
    string Udgave)
{
    public double MB => Byte / 1048576.0;
}

/// <summary>
/// Arkivet: hver maskines historik, lagt et sted den kan hentes fra igen.
/// </summary>
/// <remarks>
/// ============ DET HER ER IKKE POSTKASSEN ============
///
/// <see cref="Arbejdskoe"/> og <see cref="Journal"/> er en postkasse: det, der
/// ligger i dem, er på vej et sted hen, og det ryddes, når det er kommet frem.
/// Arkivet er det modsatte. Det bliver liggende, og det er hele meningen med
/// det: en ny bærbar skal kunne se, hvad der er sket før den fandtes, og en
/// stationær, der brænder sammen, skal kunne komme hjem igen.
///
/// Appen ARBEJDER stadig ikke fra den fælles mappe. Datamappen ligger lokalt,
/// og arkivet er en kopi ved siden af — se <see cref="Delt"/>. En SQLite-fil
/// og en wav, der vokser, mens der optages, tåler ikke at ligge et sted, to
/// maskiner rører.
///
/// ============ DER ARKIVERES EFTER EN HVIDLISTE ============
///
/// Fire mapper, nævnt ved navn: optagelser, projekter, skabeloner og
/// dokumenter. Ikke «datamappen minus nogle undtagelser».
///
/// Forskellen er ikke en smagssag. I datamappen ligger <c>deling\noegle.txt</c>
/// — maskinens PRIVATE nøgle, den ene ting der aldrig må forlade maskinen. En
/// sortliste, der glemte den, ville lægge den i en mappe, alle på delingen kan
/// læse, og ingen ville opdage det. En hvidliste kan glemme at tage noget MED.
/// Det er den fejl, man vil have.
///
/// Udenfor står derfor også: <c>learning.db</c> (en åben SQLite-fil, der
/// kopieres i stykker), <c>indstillinger.json</c> (lydenheder og stier, der
/// hører til DEN maskine), <c>log\</c> og <c>motor\</c> (modellerne fylder
/// gigabyte og kan hentes igen).
///
/// ============ HVER MASKINE EJER SIT EGET TRÆ ============
///
/// <c>arkiv\&lt;maskinid&gt;\</c>. Der skrives aldrig i en andens. Det er den
/// samme regel som for maskinfilerne og journalen, og den er der af den samme
/// grund: to skrivere på den samme fil gennem et netværksdrev eller en
/// synkroniseringsklient bliver til noget, ingen opdager.
///
/// ============ EN SLETNING REJSER IKKE ============
///
/// Sletter du en optagelse hjemme hos dig selv, bliver den liggende i arkivet.
///
/// Det er med vilje, og det er den vigtigste beslutning her. Et arkiv, der
/// sletter det, du slettede, er ikke en sikkerhedskopi — det er en spejling,
/// og en spejling redder ingen, den dag noget bliver slettet ved en fejl.
/// Journalen for aftaler og opgaver gør det MODSATTE og skriver gravsten, og
/// det er rigtigt DÉR: en aflyst aftale, der bliver ved med at komme tilbage,
/// er en fejl. En optagelse, der stadig kan hentes hjem, er en redning.
///
/// Skal der ryddes i arkivet, er det en handling for sig — se
/// <see cref="Ryd"/>.
///
/// ============ ARKIVET ER IKKE FORSEGLET ============
///
/// Arbejdskøen mærkes med en HMAC under parringens nøgle, så ingen kan lægge
/// arbejde ind. Det kan arkivet ikke, og grunden er ikke sjusk:
///
/// DEN MASKINE, DER SKAL HENTE, FINDES IKKE ENDNU. En stationær, der er brændt
/// sammen, tog sin private nøgle med sig. Den nye installation er en FREMMED
/// for det arkiv, den skal gendanne fra — den kan per definition ikke have en
/// parring med en maskine, der er væk. Et segl ville gøre arkivet ubrugeligt
/// præcis den dag, det skulle bruges.
///
/// Det er derfor mappens egne rettigheder på NAS'en, der er grænsen, og det
/// står på delingsskærmen frem for at blive antaget. Den, der kan læse mappen,
/// kan læse det, der ligger i den — det gælder allerede lyden i arbejdskøen.
/// </remarks>
public static class Arkiv
{
    /// <summary>Mappen i delingen.</summary>
    public const string Mappenavn = "arkiv";

    /// <summary>Mærkefilen i hver maskines arkiv.</summary>
    public const string Maerkefil = "arkiv.json";

    /// <summary>Endelsen på en fil, der er ved at blive kopieret.</summary>
    /// <remarks>
    /// DER SKRIVES ALDRIG DIREKTE PÅ MÅLETS NAVN. Falder netværket ud midt i en
    /// wav på 200 MB, ville der stå en halv fil med det rigtige navn og den
    /// rigtige størrelse for enhver, der kigger — og næste kørsel ville springe
    /// den over, fordi den «var der». Halvdelen af et møde er værre end intet,
    /// for man opdager det ikke.
    /// </remarks>
    public const string Halvvejs = ".delvis";

    /// <summary>Læses og skrives i klumper af den her størrelse.</summary>
    /// <remarks>
    /// EN MEGABYTE ER VALGT FOR FREMDRIFTENS SKYLD, ikke for hastighedens.
    /// <c>File.Copy</c> er lige så hurtig, men den kommer først tilbage, når
    /// filen er ovre — og så står skærmen stille, mens den største fil i
    /// arkivet bliver sendt over et trådløst net.
    /// </remarks>
    private const int Klump = 1024 * 1024;

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ==================================================================== hvidlisten

    /// <summary>
    /// De fire mapper, der arkiveres — mappenavnet i arkivet og hvor de ligger
    /// hjemme.
    /// </summary>
    /// <remarks>
    /// Rækkefølgen er den, de sendes i, og den er ikke tilfældig: det, der
    /// fylder mindst og betyder mest, kommer først. Se <see cref="Plan"/>.
    /// </remarks>
    public static IReadOnlyList<(string Navn, string Sti)> Dele() => new[]
    {
        ("skabeloner", PromptTemplate.Directory),
        ("projekter", Projektlager.Rod),
        ("dokumenter", DocumentStore.Directory),
        ("optagelser", UserDataPaths.Meetings),
    };

    // ======================================================================= stierne

    public static string Rod(string delt) => Path.Combine(delt, Mappenavn);

    /// <summary>Den her maskines eget arkiv.</summary>
    public static string Mit(string delt) => Maskinmappe(delt, Maskinid.Id);

    public static string Maskinmappe(string delt, string id) => Path.Combine(Rod(delt), id);

    // ==================================================================== hvad mangler

    /// <summary>
    /// Filerne, der ville blive lagt op. Tom liste, når arkivet er ajour.
    /// </summary>
    /// <remarks>
    /// ============ DET LETTE FØRST, LYDEN BAGEFTER ============
    ///
    /// Skabeloner, projekter, dokumenter og alt det skrevne i optagelserne er
    /// 24,8 MB. Lyden er 1.852 MB. Sendes de imellem hinanden, er man en time
    /// inde i den første kørsel, før den første udskrift er nået frem.
    ///
    /// Sådan her har en afbrudt kørsel efterladt ALT det, man kan læse, og
    /// mangler kun lyden — og det er den rigtige halvdel at mangle.
    ///
    /// Inden for hver slags kommer det nyeste først. Det, man leder efter dagen
    /// efter et nedbrud, er sidste uge, ikke marts.
    /// </remarks>
    public static IReadOnlyList<Arkivfil> Plan(bool medLyd)
    {
        if (Delt.Mappe is not { } delt || !Delt.Slaaet_til) return Array.Empty<Arkivfil>();

        var mit = Mit(delt);
        var let = new List<Arkivfil>();
        var lyd = new List<Arkivfil>();

        foreach (var (navn, sti) in Dele())
        {
            if (!Directory.Exists(sti)) continue;

            foreach (var fil in Filer(navn, sti))
            {
                var relativ = Path.GetRelativePath(sti, fil);
                var maal = Path.Combine(mit, navn, relativ);

                FileInfo info;
                try { info = new FileInfo(fil); }
                catch (IOException) { continue; }

                if (Ajour(info, maal)) continue;

                var erLyd = BackupService.IsAudio(fil);

                if (erLyd && !medLyd) continue;

                var post = new Arkivfil(fil, maal, info.Length, erLyd, info.LastWriteTimeUtc);

                (erLyd ? lyd : let).Add(post);
            }
        }

        return let.OrderByDescending(f => f.Roert)
                  .Concat(lyd.OrderByDescending(f => f.Roert))
                  .ToList();
    }

    /// <summary>Hvad der mangler at komme op, gjort op i tal.</summary>
    public static Arkivtal Mangler(bool medLyd) => Tael(Plan(medLyd));

    private static Arkivtal Tael(IEnumerable<Arkivfil> filer)
    {
        int antal = 0, lydfiler = 0;
        long fylder = 0, lydbyte = 0;

        foreach (var f in filer)
        {
            antal++;
            fylder += f.Byte;

            if (!f.Lyd) continue;

            lydfiler++;
            lydbyte += f.Byte;
        }

        return new Arkivtal(antal, fylder, lydfiler, lydbyte);
    }

    /// <summary>
    /// Ligger filen der allerede i samme skikkelse?
    /// </summary>
    /// <remarks>
    /// STØRRELSEN AFGØR DET, ikke tidsstemplet. Et netværksdrev og en
    /// synkroniseringsklient sætter skrivetiden, som de har lyst — og en
    /// sammenligning på tid alene ville sende de samme 1,8 GB op igen, hver
    /// gang noget rørte ved en mappe.
    ///
    /// En rettet fil skifter næsten altid størrelse. Gør den ikke det, og er
    /// den samtidig nyere, sendes den også: tidsstemplet får lov at afgøre det,
    /// når størrelsen ikke kan.
    /// </remarks>
    private static bool Ajour(FileInfo kilde, string maal)
    {
        try
        {
            var m = new FileInfo(maal);

            if (!m.Exists) return false;
            if (m.Length != kilde.Length) return false;

            // Et sekunds slør: FAT, SMB og NTFS er ikke enige om opløsningen.
            return m.LastWriteTimeUtc >= kilde.LastWriteTimeUtc.AddSeconds(-1);
        }
        catch (IOException)
        {
            return false;
        }
    }

    // ================================================================ hvad der tælles med

    private static IEnumerable<string> Filer(string del, string rod)
    {
        if (del != "optagelser")
        {
            foreach (var f in Alle(rod)) yield return f;
            yield break;
        }

        // ============ EN OPTAGELSE, DER ER I GANG, RØRES IKKE ============
        //
        // Wav-filen vokser, mens der optages, og meeting.json faar sit
        // sluttidspunkt, naar der stoppes. Kopieres mappen imens, faar arkivet
        // en halv optagelse - og den er der stadig i morgen, for stoerrelsen
        // passer med det, der blev laest.
        foreach (var mappe in Directory.EnumerateDirectories(rod))
        {
            MeetingMetadata? meta;
            try { meta = MeetingStore.Load(mappe); }
            catch (IOException) { continue; }

            if (meta?.EndedAt is null) continue;

            foreach (var f in Alle(mappe)) yield return f;
        }
    }

    private static IEnumerable<string> Alle(string rod)
    {
        IEnumerable<string> fundet;

        try { fundet = Directory.EnumerateFiles(rod, "*", SearchOption.AllDirectories); }
        catch (IOException) { yield break; }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (var f in fundet)
        {
            if (Halvfaerdig(f)) continue;

            yield return f;
        }
    }

    /// <summary>
    /// Filer, der er noget andet undervejs. De hører ikke i et arkiv.
    /// </summary>
    /// <remarks>
    /// Appen skriver overalt til <c>.ny</c> og flytter bagefter — se
    /// <see cref="Delt.Klargoer"/>. Fanges den mellem de to, arkiveres en fil,
    /// der aldrig var noget. <c>.forrige</c> og <c>.gammel-*</c> er appens egne
    /// sikkerhedsnet fra en anden maskine og hører ikke hjemme her.
    /// </remarks>
    private static bool Halvfaerdig(string sti)
    {
        var navn = Path.GetFileName(sti);

        return navn.EndsWith(".ny", StringComparison.OrdinalIgnoreCase)
            || navn.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || navn.EndsWith(Halvvejs, StringComparison.OrdinalIgnoreCase)
            || navn.EndsWith(".forrige", StringComparison.OrdinalIgnoreCase)
            || navn.Contains(".gammel-", StringComparison.OrdinalIgnoreCase);
    }

    // ======================================================================== læg op

    /// <summary>
    /// Lægger det, der mangler, op i den her maskines eget arkiv.
    /// </summary>
    /// <remarks>
    /// AFBRYDELSE ER IKKE EN FEJL. Lukkes appen midt i 1,8 GB, er det, der nåede
    /// frem, gyldigt, og næste kørsel tager resten. Derfor kopieres én fil ad
    /// gangen til et halvt navn og flyttes på plads bagefter.
    /// </remarks>
    /// <returns>Hvad der faktisk kom op.</returns>
    public static Arkivtal Gem(bool medLyd,
                               Action<Arkivfremdrift>? melder = null,
                               CancellationToken stop = default)
    {
        if (Delt.Mappe is not { } delt || !Delt.Slaaet_til) return Arkivtal.Intet;

        var plan = Plan(medLyd);

        if (plan.Count == 0)
        {
            Maerk(delt, medLyd);
            return Arkivtal.Intet;
        }

        var ialt = plan.Sum(f => f.Byte);
        long sendt = 0;
        var kom = new List<Arkivfil>();

        for (var i = 0; i < plan.Count; i++)
        {
            if (stop.IsCancellationRequested) break;

            var f = plan[i];
            var start = sendt;

            melder?.Invoke(new Arkivfremdrift(Path.GetFileName(f.Kilde), i + 1, plan.Count, sendt, ialt));

            try
            {
                Kopier(f.Kilde, f.Maal, stop, nu => melder?.Invoke(
                    new Arkivfremdrift(Path.GetFileName(f.Kilde), i + 1, plan.Count, start + nu, ialt)));

                kom.Add(f);
                sendt = start + f.Byte;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // En fil, der er laast af noget andet, eller et drev, der faldt
                // ud. De oevrige skal stadig med, og den her er med naeste gang.
                sendt = start + f.Byte;
            }
        }

        Maerk(delt, medLyd);

        return Tael(kom);
    }

    /// <summary>
    /// Kopierer én fil gennem et halvt navn, med fremdrift undervejs.
    /// </summary>
    private static void Kopier(string fra, string til, CancellationToken stop, Action<long>? naaet)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(til)!);

        var delvis = til + Halvvejs;

        try
        {
            using (var ind = new FileStream(fra, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var ud = new FileStream(delvis, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var pude = new byte[Klump];
                long naaetIalt = 0;
                int laest;

                while ((laest = ind.Read(pude, 0, pude.Length)) > 0)
                {
                    stop.ThrowIfCancellationRequested();

                    ud.Write(pude, 0, laest);
                    naaetIalt += laest;
                    naaet?.Invoke(naaetIalt);
                }
            }

            // Skrivetiden foelger med. Uden den ville filen se nyere ud end
            // originalen, og «hent hjem» kunne ikke se, hvad der var hvad.
            File.SetLastWriteTimeUtc(delvis, File.GetLastWriteTimeUtc(fra));
            File.Move(delvis, til, overwrite: true);
        }
        catch (Exception)
        {
            try { if (File.Exists(delvis)) File.Delete(delvis); }
            catch (IOException) { /* den ryddes naeste gang. */ }

            throw;
        }
    }

    // ====================================================================== mærkefilen

    private static void Maerk(string delt, bool medLyd)
    {
        try
        {
            var mit = Mit(delt);
            Directory.CreateDirectory(mit);

            var tal = Ligger(mit);

            var maerke = new Arkivmaerke(
                Maskinid.Id,
                Maskinid.Navn,
                Maskinid.Rolle,
                DateTimeOffset.Now,
                tal.Filer,
                tal.Byte,
                medLyd,
                System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "");

            var fil = Path.Combine(mit, Maerkefil);
            var midlertidig = fil + ".ny";

            File.WriteAllText(midlertidig, JsonSerializer.Serialize(maerke, Format), new UTF8Encoding(false));
            File.Move(midlertidig, fil, overwrite: true);
        }
        catch (Exception)
        {
            // Maerket er en oplysning til skaermen, ikke en del af arkivet.
            // Kan den ikke skrives, er filerne der stadig.
        }
    }

    /// <summary>Arkiverne i den fælles mappe — også den her maskines eget.</summary>
    public static IReadOnlyList<Arkivmaerke> Alle()
    {
        if (Delt.Mappe is not { } delt || !Delt.Slaaet_til) return Array.Empty<Arkivmaerke>();

        var rod = Rod(delt);

        if (!Directory.Exists(rod)) return Array.Empty<Arkivmaerke>();

        var ud = new List<Arkivmaerke>();

        foreach (var mappe in Directory.EnumerateDirectories(rod))
        {
            var id = Path.GetFileName(mappe);

            try
            {
                var fil = Path.Combine(mappe, Maerkefil);

                if (File.Exists(fil)
                    && JsonSerializer.Deserialize<Arkivmaerke>(File.ReadAllText(fil, Encoding.UTF8), Format)
                       is { } m && m.Id.Length > 0)
                {
                    ud.Add(m);
                    continue;
                }

                // ============ ET ARKIV UDEN MAERKE ER STADIG ET ARKIV ============
                //
                // Maerket kan vaere skrevet halvt, eller mappen kan vaere kopieret
                // ind i haanden. Filerne er der, og de er det eneste, der
                // betyder noget - saa taelles de i stedet for at blive skjult.
                var tal = Ligger(mappe);

                if (tal.Noget)
                    ud.Add(new Arkivmaerke(id, id, Maskinrolle.Primaer, Directory.GetLastWriteTime(mappe),
                                           tal.Filer, tal.Byte, tal.Lydfiler > 0, ""));
            }
            catch (Exception)
            {
                // Naeste mappe.
            }
        }

        return ud.OrderByDescending(m => m.Sidst).ToList();
    }

    /// <summary>De andres arkiver. Dem, der er noget at hente i.</summary>
    public static IReadOnlyList<Arkivmaerke> Andres() =>
        Alle().Where(m => m.Id != Maskinid.Id).ToList();

    private static Arkivtal Ligger(string mappe)
    {
        int filer = 0, lydfiler = 0;
        long fylder = 0, lydbyte = 0;

        try
        {
            foreach (var f in Directory.EnumerateFiles(mappe, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(f) == Maerkefil || Halvfaerdig(f)) continue;

                long stoerrelse;
                try { stoerrelse = new FileInfo(f).Length; }
                catch (IOException) { continue; }

                filer++;
                fylder += stoerrelse;

                if (!BackupService.IsAudio(f)) continue;

                lydfiler++;
                lydbyte += stoerrelse;
            }
        }
        catch (IOException) { /* det, der naaede at blive taelt. */ }
        catch (UnauthorizedAccessException) { /* ditto. */ }

        return new Arkivtal(filer, fylder, lydfiler, lydbyte);
    }

    // ====================================================================== hent hjem

    /// <summary>
    /// Filerne, der ville blive hentet hjem fra en anden maskines arkiv.
    /// </summary>
    /// <remarks>
    /// ============ DER OVERSKRIVES ALDRIG NOGET ============
    ///
    /// Findes filen hjemme i forvejen, springes den over — også når arkivets er
    /// nyere. Det er hele forskellen på at hente hjem og at synkronisere.
    ///
    /// Den, der trykker «hent hjem», har enten en tom maskine eller en, der
    /// mangler noget. I begge tilfælde er den lokale fil den, der er i brug,
    /// og en gendannelse, der kunne skrive hen over dagens arbejde, er en, man
    /// ikke tør trykke på.
    /// </remarks>
    public static IReadOnlyList<Arkivfil> Hjemplan(string maskinid, bool medLyd)
    {
        if (Delt.Mappe is not { } delt || !Delt.Slaaet_til) return Array.Empty<Arkivfil>();
        if (maskinid.Length == 0) return Array.Empty<Arkivfil>();

        var fra = Maskinmappe(delt, maskinid);

        if (!Directory.Exists(fra)) return Array.Empty<Arkivfil>();

        var let = new List<Arkivfil>();
        var lyd = new List<Arkivfil>();

        foreach (var (navn, hjem) in Dele())
        {
            var kilde = Path.Combine(fra, navn);

            if (!Directory.Exists(kilde)) continue;

            foreach (var fil in Alle(kilde))
            {
                var maal = Path.Combine(hjem, Path.GetRelativePath(kilde, fil));

                if (File.Exists(maal)) continue;

                FileInfo info;
                try { info = new FileInfo(fil); }
                catch (IOException) { continue; }

                var erLyd = BackupService.IsAudio(fil);

                if (erLyd && !medLyd) continue;

                (erLyd ? lyd : let).Add(new Arkivfil(fil, maal, info.Length, erLyd, info.LastWriteTimeUtc));
            }
        }

        return let.OrderByDescending(f => f.Roert)
                  .Concat(lyd.OrderByDescending(f => f.Roert))
                  .ToList();
    }

    /// <summary>Hvad der kunne hentes hjem, gjort op i tal.</summary>
    public static Arkivtal Hjemme(string maskinid, bool medLyd) => Tael(Hjemplan(maskinid, medLyd));

    /// <summary>
    /// Henter en anden maskines arkiv hjem i datamappen.
    /// </summary>
    /// <returns>Hvad der faktisk kom hjem.</returns>
    public static Arkivtal Hent(string maskinid,
                                bool medLyd,
                                Action<Arkivfremdrift>? melder = null,
                                CancellationToken stop = default)
    {
        var plan = Hjemplan(maskinid, medLyd);

        if (plan.Count == 0) return Arkivtal.Intet;

        var ialt = plan.Sum(f => f.Byte);
        long hentet = 0;
        var kom = new List<Arkivfil>();

        for (var i = 0; i < plan.Count; i++)
        {
            if (stop.IsCancellationRequested) break;

            var f = plan[i];
            var start = hentet;

            melder?.Invoke(new Arkivfremdrift(Path.GetFileName(f.Kilde), i + 1, plan.Count, hentet, ialt));

            try
            {
                Kopier(f.Kilde, f.Maal, stop, nu => melder?.Invoke(
                    new Arkivfremdrift(Path.GetFileName(f.Kilde), i + 1, plan.Count, start + nu, ialt)));

                kom.Add(f);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Naeste fil.
            }

            hentet = start + f.Byte;
        }

        return Tael(kom);
    }

    // ============================================================================ ryd

    /// <summary>
    /// Fjerner en maskines arkiv fra den fælles mappe.
    /// </summary>
    /// <remarks>
    /// DET SKER KUN, NÅR NOGEN TRYKKER. Arkivet rydder ikke op efter sig selv,
    /// og det er meningen: det, der ligger her, er tit den sidste kopi af en
    /// optagelse, nogen har slettet hjemme hos sig selv.
    /// </remarks>
    public static bool Ryd(string maskinid)
    {
        if (Delt.Mappe is not { } delt || !Delt.Slaaet_til || maskinid.Length == 0) return false;

        var mappe = Maskinmappe(delt, maskinid);

        try
        {
            if (!Directory.Exists(mappe)) return false;

            Directory.Delete(mappe, recursive: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
