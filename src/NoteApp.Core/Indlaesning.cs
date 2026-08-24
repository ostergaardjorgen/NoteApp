using NAudio.MediaFoundation;
using NAudio.Wave;

namespace NoteApp.Core;

/// <summary>Resultatet af en indlæsning: hvor optagelsen ligger, og hvor lang den blev.</summary>
public sealed record Indlaesningssvar(string Mappe, TimeSpan Laengde, string Kildefil);

/// <summary>
/// En lydfil udefra bliver til en optagelse.
///
/// HVORFOR DEN HØRER TIL HER OG IKKE I OPTAGELSEN
///
/// Møder holdes ikke altid ved pc'en. En telefon i lommen, en diktafon, et
/// webinar nogen har sendt en fil fra — materialet findes, og uden en vej ind
/// er appen kun noget værd for de møder, den selv var med til.
///
/// Resultatet er en helt almindelig optagelsesmappe: <c>meeting.json</c> og
/// <c>mikrofon.wav</c>, præcis som hvis der var trykket optag. Det er med
/// vilje. Transskriptionen, søgningen, dokumenterne og oprydningen skal ikke
/// vide, hvor lyden kom fra — og gør de ikke det, kan de heller ikke gå i
/// stykker på en fil, der kom ind ad en anden dør.
///
/// DER ER KUN ÉT SPOR, OG DET SKAL SIGES
///
/// En optagelse fra appen har to sider: HERFRA og DERFRA. En fil udefra har
/// én. Alt, hvad der bliver sagt, står som det samme spor, uanset hvem der
/// sagde det. Derfor sættes <see cref="MeetingMetadata.Indlaest"/>, så
/// skærmene kan skrive det frem for at lade brugeren opdage det.
///
/// KILDEFILEN RØRES ALDRIG
///
/// Der kopieres, aldrig flyttes. Filen ligger typisk i en synkroniseret mappe
/// — iCloud, Google Drev, OneDrive — og at flytte den ville slette den på
/// telefonen. Det er ikke appens fil.
/// </summary>
public static class Indlaesning
{
    /// <summary>
    /// Det, Windows kan afkode uden hjælp udefra.
    ///
    /// Listen er bevidst kort. Media Foundation kan mere, men det, der ikke er
    /// prøvet af, hører ikke i en filtypeliste — en fil, der kan vælges og så
    /// ikke kan læses, er værre end en, der ikke kunne vælges.
    /// </summary>
    public static readonly string[] Endelser =
        { ".m4a", ".mp3", ".wav", ".aac", ".mp4", ".m4b", ".wma", ".flac" };

    /// <summary>Filtret til åbn-dialogen.</summary>
    public static string Filter =>
        "Lydfiler (" + string.Join("; ", Endelser.Select(e => "*" + e)) + ")|"
        + string.Join(";", Endelser.Select(e => "*" + e))
        + "|Alle filer|*.*";

    public static bool Kendes(string sti) =>
        Endelser.Contains(Path.GetExtension(sti), StringComparer.OrdinalIgnoreCase);

    // EN FIL, DER KUN LIGGER I SKYEN.
    //
    // iCloud, OneDrive og Google Drev laegger en pladsholder paa disken og
    // henter foerst indholdet, naar nogen laeser filen. Attributten hedder
    // RECALL_ON_DATA_ACCESS og findes ikke i .NETs FileAttributes, saa tallet
    // staar her. Uden det ser en hentning ud som en app, der er gaaet i staa.
    private const int RecallOnDataAccess = 0x00400000;
    private const int RecallOnOpen = 0x00040000;

    /// <summary>
    /// Ligger filen kun i skyen? Så koster første læsning en hentning.
    /// </summary>
    public static bool KunISkyen(string sti)
    {
        try
        {
            var a = (int)File.GetAttributes(sti);
            return (a & RecallOnDataAccess) != 0
                || (a & RecallOnOpen) != 0
                || (a & (int)FileAttributes.Offline) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Læser lydfilen ind som en ny optagelse og returnerer mappen.
    ///
    /// Kaldes fra en baggrundstråd. <paramref name="melding"/> får korte
    /// tekster undervejs, som kan stå på skærmen uændret.
    /// </summary>
    /// <param name="kilde">Filen, der skal ind. Røres ikke.</param>
    /// <param name="titel">Optagelsens navn. Tom betyder filens eget navn.</param>
    /// <param name="mappe">Brugerens folder, eller null for ingen.</param>
    /// <param name="moedetype">Mødetypen, der skal bruges til dokumentet, eller null.</param>
    public static Indlaesningssvar Indlaes(string kilde,
                                           string? titel = null,
                                           string? mappe = null,
                                           string? moedetype = null,
                                           IProgress<string>? melding = null,
                                           CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kilde) || !File.Exists(kilde))
            throw new FileNotFoundException("Lydfilen findes ikke.", kilde);

        var oplysning = new FileInfo(kilde);
        if (oplysning.Length == 0)
            throw new InvalidDataException("Filen er tom.");

        if (KunISkyen(kilde))
            melding?.Report("Henter filen ned fra skyen …");

        UserDataPaths.EnsureCreated();

        var navn = string.IsNullOrWhiteSpace(titel)
            ? Path.GetFileNameWithoutExtension(kilde)
            : titel.Trim();

        var optaget = Optagetidspunkt(oplysning);
        var dir = LedigMappe(navn, optaget);

        try
        {
            melding?.Report("Læser lyden …");

            var maal = Path.Combine(dir, "mikrofon.wav");
            var laengde = Omsaet(kilde, maal, melding, ct);

            // EN TOM UDGANG ER EN FEJL, IKKE EN OPTAGELSE.
            //
            // Media Foundation kan finde paa at aabne en fil og saa levere
            // nul bytes - en video uden lydspor gaar praecis den vej. Bliver
            // mappen liggende, staar der en optagelse uden lyd, og det er
            // foerst naar man forsoeger at skrive den ud, at det opdages.
            if (laengde <= TimeSpan.Zero)
                throw new InvalidDataException(
                    "Der er ingen lyd i filen. Er det en video uden lydspor?");

            var meta = new MeetingMetadata
            {
                // ET INDLAEST MOEDE ER ALTID «FYSISK».
                //
                // Ikke fordi moedet var det, men fordi typen her betyder
                // «eet spor, ingen fletning». LoopbackDeviceName er null, og
                // det er praecis det flag, fase 2 laeser for at springe
                // sammenlaegningen af to spor over.
                Type = MeetingType.Physical,
                StartedAt = optaget,
                EndedAt = optaget + laengde,
                DurationSeconds = laengde.TotalSeconds,
                Title = navn,
                MicDeviceName = "Indlæst lydfil",
                LoopbackDeviceName = null,
                Mappe = string.IsNullOrWhiteSpace(mappe) ? null : mappe,
                Moedetype = string.IsNullOrWhiteSpace(moedetype) ? null : moedetype,
                Indlaest = new Indlaesningskilde
                {
                    Filnavn = Path.GetFileName(kilde),
                    Sti = kilde,
                    Tidspunkt = DateTimeOffset.Now,
                    Bytes = oplysning.Length
                }
            };

            meta.Tracks["mikrofon"] = "mikrofon.wav";

            MeetingStore.Save(dir, meta);

            melding?.Report("Klar til transskription");
            return new Indlaesningssvar(dir, laengde, Path.GetFileName(kilde));
        }
        catch (Exception)
        {
            // EN HALV OPTAGELSE ER VAERRE END INGEN.
            //
            // Gaar noget galt undervejs, ryddes mappen. Ellers ligger der en
            // post i listen med en halv wav-fil og ingen meeting.json, og den
            // kan hverken skrives ud eller forklares.
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            throw;
        }
    }

    /// <summary>
    /// Hvornår optagelsen blev lavet — ikke hvornår den blev lagt ind.
    ///
    /// En talememo fra i tirsdags skal sortere som i tirsdags. Filens egen
    /// dato er det tætteste, vi kommer, og både iCloud og Google Drev bevarer
    /// den gennem synkroniseringen.
    /// </summary>
    private static DateTimeOffset Optagetidspunkt(FileInfo fil)
    {
        var nu = DateTimeOffset.Now;

        DateTimeOffset[] bud;
        try { bud = new[] { new DateTimeOffset(fil.LastWriteTime), new DateTimeOffset(fil.CreationTime) }; }
        catch (Exception) { return nu; }

        // Den aeldste af de to, men kun hvis den er troværdig. En kopieret fil
        // kan have faaet dagens dato paa CreationTime, og en fil fra et
        // filsystem uden rigtige datoer kan staa i 1601.
        var grænse = new DateTimeOffset(new DateTime(2000, 1, 1));

        var gyldige = bud.Where(d => d > grænse && d <= nu.AddDays(1)).ToList();
        return gyldige.Count == 0 ? nu : gyldige.Min();
    }

    /// <summary>
    /// En mappe, der ikke er i brug. To filer indlæst i det samme minut må
    /// ikke lande oven i hinanden.
    /// </summary>
    private static string LedigMappe(string titel, DateTimeOffset optaget)
    {
        var grund = Path.Combine(UserDataPaths.Meetings, MeetingStore.Slug(titel, optaget));
        var sti = grund;

        for (var n = 2; Directory.Exists(sti) && n < 100; n++)
            sti = $"{grund}-{n}";

        Directory.CreateDirectory(sti);
        return sti;
    }

    /// <summary>
    /// Lyden om til det, whisper.cpp kan læse: 16 kHz, mono, 16 bit.
    ///
    /// Der bruges Windows' egne kodeks gennem Media Foundation. Det er svaret
    /// på det spørgsmål, roadmappen stillede — om der skulle følge en ffmpeg
    /// med i installationen. Det skal der ikke: maskinen kan i forvejen læse
    /// AAC, MP3 og WMA, og det er den samme vej, komprimeringsmålingen og
    /// lydoprydningen allerede går.
    /// </summary>
    private static TimeSpan Omsaet(string kilde, string maal,
                                   IProgress<string>? melding, CancellationToken ct)
    {
        MediaFoundationApi.Startup();

        using var ind = new MediaFoundationReader(kilde);

        var hele = ind.TotalTime;
        melding?.Report(hele > TimeSpan.Zero
            ? $"Omsætter {hele:hh\\:mm\\:ss} lyd …"
            : "Omsætter lyden …");

        var format = new WaveFormat(AudioFormat.SampleRate, AudioFormat.BitsPerSample, AudioFormat.Channels);

        using var omsaet = new MediaFoundationResampler(ind, format) { ResamplerQuality = 60 };
        using var ud = new WaveFileWriter(maal, format);

        var pude = new byte[format.AverageBytesPerSecond];
        var sidst = -1;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var laest = omsaet.Read(pude, 0, pude.Length);
            if (laest == 0) break;

            ud.Write(pude, 0, laest);

            // Meldingen kommer een gang pr. procent og ikke pr. sekund lyd.
            // En treminutters memo ville ellers sende 180 opdateringer til
            // skaermen for noget, der er overstaaet paa et oejeblik.
            if (hele <= TimeSpan.Zero) continue;

            var pct = (int)(ud.TotalTime.TotalSeconds / hele.TotalSeconds * 100);
            if (pct == sidst || pct > 100) continue;

            sidst = pct;
            melding?.Report($"Omsætter lyden … {pct} %");
        }

        ud.Flush();
        return ud.TotalTime;
    }
}
