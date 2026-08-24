namespace NoteApp.Core;

/// <summary>Hvad pladsen bruges til — og hvor meget der er tilbage.</summary>
/// <param name="DrevNavn">Drevet, datamappen ligger på.</param>
/// <param name="DrevIAlt">Diskens samlede størrelse i bytes.</param>
/// <param name="DrevFrit">Ledig plads i bytes.</param>
/// <param name="LydBytes">Det, appens lydfiler fylder.</param>
/// <param name="LydFiler">Hvor mange lydfiler der er.</param>
/// <param name="RestBytes">Alt det andet i datamappen: tekst, dokumenter, modeller.</param>
public sealed record Pladsopgoerelse(
    string DrevNavn,
    long DrevIAlt,
    long DrevFrit,
    long LydBytes,
    int LydFiler,
    long RestBytes)
{
    public double LydGb => LydBytes / 1024.0 / 1024.0 / 1024.0;
    public double FritGb => DrevFrit / 1024.0 / 1024.0 / 1024.0;
    public double IAltGb => DrevIAlt / 1024.0 / 1024.0 / 1024.0;
    public double BrugtGb => (DrevIAlt - DrevFrit) / 1024.0 / 1024.0 / 1024.0;

    /// <summary>Hvor stor en del af HELE disken er lyd fra appen?</summary>
    public double LydAndelAfDisk => DrevIAlt <= 0 ? 0 : LydBytes * 100.0 / DrevIAlt;

    /// <summary>Hvor stor en del af det, der ER brugt, er lyd fra appen?</summary>
    public double LydAndelAfBrugt
    {
        get
        {
            var brugt = DrevIAlt - DrevFrit;
            return brugt <= 0 ? 0 : LydBytes * 100.0 / brugt;
        }
    }

    public double FriAndel => DrevIAlt <= 0 ? 0 : DrevFrit * 100.0 / DrevIAlt;
}

/// <summary>Én optagelse, hvis lyd kan ryddes.</summary>
public sealed record Lydkandidat(string Mappe, string Titel, long Bytes, int AlderIDage);

/// <summary>
/// Rydder lyden op, når teksten har været i hus længe nok.
///
/// HVORFOR DET OVERHOVEDET GIVES SOM MULIGHED
///
/// En times møde fylder 220 MB som lyd og 56 KB som tekst. Efter et års brug
/// er lyden titusind gange så meget som det, man faktisk slår op i — og det er
/// målt, at lyden ikke kan komprimeres uden at ødelægge udskriften af møder
/// med flere sprog (se findings 9.2). Så er det eneste tilbageværende
/// håndtag at lade den gå.
///
/// HVAD DER GÅR TABT, OG DET SKAL STÅ TYDELIGT
///
/// Uden lydfilen kan man ikke:
///
///   - skrive optagelsen ud igen med en bedre model
///   - køre talergenkendelsen om
///   - høre efter, om maskinen hørte rigtigt
///
/// Det sidste er det vigtigste, og det er ikke bygget endnu: at klikke på en
/// sætning og høre den. Tidsstemplerne findes allerede, funktionen står på
/// listen — og den kan kun bruges på optagelser, hvor lyden stadig er der.
/// Derfor er standarden et helt år og ikke en måned.
///
/// TRE TING RYDDES ALDRIG
///
///   1. Lyd uden en udskrift. Så er der ikke noget tilbage bagefter, og det
///      er ikke oprydning — det er sletning.
///   2. Noget som helst, hvis brugeren har sat antal dage til nul. Nul
///      betyder «lad være», ikke «med det samme».
///   3. Andet end lyd. Tekst, noter, dokumenter og opgaver bliver liggende;
///      de fylder ingenting og er dét, arkivet er til for.
///
/// DER SPØRGES IKKE HVER GANG. Et spørgsmål, man siger ja til hver uge i et
/// år, er ikke et samtykke — det er en vane. Valget træffes ét sted, under
/// Indstillinger, og står der.
/// </summary>
public static class Lydoprydning
{
    /// <summary>Filnavnene, der regnes som lyd. Kun de spor, appen selv optager.</summary>
    private static readonly string[] Lydfiler = { "mikrofon.wav", "loopback.wav" };

    /// <summary>
    /// Hvad der ville blive ryddet ved den her grænse.
    ///
    /// Alderen regnes fra UDSKRIFTENS dato og ikke fra optagelsens. Skriver
    /// man et to år gammelt møde ud i dag, skal lyden ikke være væk i morgen —
    /// man har lige fået brug for den.
    /// </summary>
    /// <param name="dage">Nul eller derunder betyder «ryd aldrig».</param>
    public static List<Lydkandidat> Kandidater(int dage)
    {
        var ud = new List<Lydkandidat>();

        if (dage <= 0) return ud;
        if (!Directory.Exists(UserDataPaths.Meetings)) return ud;

        var nu = DateTime.Now;

        foreach (var mappe in Directory.EnumerateDirectories(UserDataPaths.Meetings))
        {
            try
            {
                var lyd = Lydfiler.Select(f => Path.Combine(mappe, f))
                                  .Where(File.Exists)
                                  .ToList();

                if (lyd.Count == 0) continue;

                // INGEN UDSKRIFT, INGEN OPRYDNING. Uden teksten er der ikke
                // noget tilbage bagefter, og saa er det ikke oprydning.
                var udskrifter = Directory.GetFiles(mappe, "udskrift_*.txt");
                if (udskrifter.Length == 0) continue;

                var skrevet = udskrifter.Max(File.GetLastWriteTime);
                var alder = (int)(nu - skrevet).TotalDays;

                if (alder < dage) continue;

                MeetingMetadata? meta = null;
                try { meta = MeetingStore.Load(mappe); } catch (Exception) { }

                ud.Add(new Lydkandidat(
                    mappe,
                    meta?.Title ?? Path.GetFileName(mappe),
                    lyd.Sum(f => new FileInfo(f).Length),
                    alder));
            }
            catch (Exception)
            {
                // Én mappe, der ikke kan laeses, maa ikke stoppe de oevrige.
            }
        }

        return ud.OrderByDescending(k => k.Bytes).ToList();
    }

    /// <summary>
    /// Rydder lyden. Svarer med, hvor mange filer og hvor mange bytes.
    ///
    /// Fejler en enkelt fil — den er i brug, eller mappen er skrivebeskyttet —
    /// fortsættes der med de øvrige. En oprydning, der stopper ved den første
    /// låste fil, rydder ingenting den dag, appen har en optagelse åben.
    /// </summary>
    public static (int Filer, long Bytes) Ryd(int dage)
    {
        var filer = 0;
        long bytes = 0;

        foreach (var k in Kandidater(dage))
        {
            foreach (var navn in Lydfiler)
            {
                var sti = Path.Combine(k.Mappe, navn);

                try
                {
                    if (!File.Exists(sti)) continue;

                    var stoerrelse = new FileInfo(sti).Length;

                    File.Delete(sti);

                    filer++;
                    bytes += stoerrelse;
                }
                catch (Exception)
                {
                    // Filen er i brug eller kan ikke slettes. Proeves igen
                    // naeste gang.
                }
            }
        }

        return (filer, bytes);
    }

    /// <summary>
    /// Rydder lyden på ÉN optagelse. Svarer med, hvor mange bytes der blev frigivet.
    ///
    /// SPØRGSMÅLET STILLES ET ANDET STED. Den her sletter, når nogen har sagt
    /// ja — den spørger ikke selv. En metode, der både spørger og sletter,
    /// kan ikke kaldes fra en oprydning, der kører uden nogen til stede.
    ///
    /// DER RYDDES IKKE UDEN EN UDSKRIFT, heller ikke her. Det ville ikke være
    /// oprydning, det ville være at kassere mødet.
    /// </summary>
    public static long RydEn(string mappe)
    {
        try
        {
            if (Directory.GetFiles(mappe, "udskrift_*.txt").Length == 0) return 0;
        }
        catch (Exception)
        {
            return 0;
        }

        long bytes = 0;

        foreach (var navn in Lydfiler)
        {
            var sti = Path.Combine(mappe, navn);

            try
            {
                if (!File.Exists(sti)) continue;

                var n = new FileInfo(sti).Length;

                File.Delete(sti);
                bytes += n;
            }
            catch (Exception)
            {
                // Filen er i brug. Den bliver liggende, og det er i orden.
            }
        }

        return bytes;
    }

    /// <summary>Hvad lyden fylder på én optagelse. Nul, hvis der ikke er nogen.</summary>
    public static long Lydstoerrelse(string mappe)
    {
        long bytes = 0;

        foreach (var navn in Lydfiler)
        {
            try
            {
                var sti = Path.Combine(mappe, navn);
                if (File.Exists(sti)) bytes += new FileInfo(sti).Length;
            }
            catch (Exception) { }
        }

        return bytes;
    }

    /// <summary>
    /// Hvad pladsen går til. Læses fra disken, ikke fra et regnskab.
    ///
    /// Et tal, der holdes ved lige, kommer ud af trit med virkeligheden — og
    /// det her tal er præcis dét, man åbner skærmen for at få at vide.
    /// </summary>
    public static Pladsopgoerelse Opgoer()
    {
        long lyd = 0;
        var antal = 0;
        long rest = 0;

        try
        {
            if (Directory.Exists(UserDataPaths.Root))
            {
                foreach (var f in Directory.EnumerateFiles(UserDataPaths.Root, "*",
                                                           SearchOption.AllDirectories))
                {
                    long n;
                    try { n = new FileInfo(f).Length; } catch (Exception) { continue; }

                    if (f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        lyd += n;
                        antal++;
                    }
                    else
                    {
                        rest += n;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Kan mappen ikke gennemgaas, staar der nul - og skaermen siger
            // det som det er frem for at gaette.
        }

        var navn = "";
        long iAlt = 0, frit = 0;

        try
        {
            var drev = new DriveInfo(Path.GetPathRoot(UserDataPaths.Root)!);

            navn = drev.Name;
            iAlt = drev.TotalSize;
            frit = drev.AvailableFreeSpace;
        }
        catch (Exception)
        {
            // Samme: nul frem for et gaet.
        }

        return new Pladsopgoerelse(navn, iAlt, frit, lyd, antal, rest);
    }
}
