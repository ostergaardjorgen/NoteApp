using System.IO;
using System.Windows.Threading;
using NoteApp.Core;
using NoteApp.Core.Deling;

namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Etape 2: den bærbare optager, den kraftige skriver ud.
/// </summary>
/// <remarks>
/// ============ TO SIDER AF DEN SAMME MAPPE ============
///
/// Vagten kører på begge maskiner og gør to ting:
///
///   TAGER ARBEJDE. Ligger der et spor fra en godkendt computer, hentes lyden
///   hjem, whisper kører, og udskriften lægges tilbage. Kun én ad gangen, og
///   kun bag den samme lås som appens egne tunge kørsler — se
///   <see cref="HeavyJobLock"/>: to whisper-kørsler på ét grafikkort er ikke
///   dobbelt så hurtigt, det er to, der løber tør for hukommelse.
///
///   HENTER SVAR HJEM. Er alle spor i et møde skrevet ud, samles de til den
///   udskrift, mødet ville have fået, hvis den var lavet her.
///
/// ============ DEN MÅ IKKE STÅ I VEJEN ============
///
/// Alt arbejde sker på en baggrundstråd. Brugeren skal kunne optage, læse og
/// skrive, mens den anden computers møde bliver skrevet ud — det er hele
/// pointen med at have to.
///
/// ============ DEN TAGER KUN, HVAD DEN KAN NÅ ============
///
/// Er den her maskine selv i gang med noget tungt, tages der ingen opgave.
/// Den bliver liggende til næste gang; en bærbar, der venter fem minutter
/// ekstra, er bedre end en stationær, der kører to modeller mod hinanden.
/// </remarks>
public static class Arbejdsvagt
{
    /// <summary>Hvor tit der ses efter arbejde og svar.</summary>
    public static readonly TimeSpan Mellemrum = TimeSpan.FromSeconds(60);

    private static DispatcherTimer? _ur;

    /// <summary>Sat, mens der arbejdes. Uret må ikke starte den samme opgave to gange.</summary>
    private static bool _travlt;

    /// <summary>Melder fremdrift til skærmen. Sat af hovedvinduet.</summary>
    public static Action<string>? Melder { get; set; }

    public static void Start()
    {
        if (_ur is not null) return;

        _ur = new DispatcherTimer(DispatcherPriority.Background) { Interval = Mellemrum };
        _ur.Tick += (_, _) => Kig();
        _ur.Start();

        Kig();
    }

    private static void Kig()
    {
        if (_travlt) return;

        if (!Delt.Slaaet_til) return;

        _travlt = true;

        _ = Task.Run(() =>
        {
            try
            {
                Hentsvar();
                Tagarbejde();
                Arbejdskoe.Ryd_gamle();
            }
            catch (Exception)
            {
                // Et drev, der ikke svarer lige nu. Naeste gang om et minut.
            }
            finally
            {
                _travlt = false;
            }
        });
    }

    // ============================================================== tag arbejde

    private static void Tagarbejde()
    {
        var venter = Arbejdskoe.Venter();

        if (venter.Count == 0) return;

        if (Delt.Mappe is not { } delt) return;

        foreach (var opgave in venter)
        {
            // ============ HALVT KOPIERET ER IKKE KLAR ============
            //
            // Lyden kan stadig vaere paa vej gennem et netvaerksdrev. Summen
            // fanger baade dét og en fil, der er byttet ud.
            if (!Arbejdskoe.Lyden_passer(delt, opgave)) continue;

            if (WhisperInstall.Locate().WhisperCli is null) return;

            // Er maskinen selv i gang med noget tungt, lades opgaven ligge.
            if (HeavyJobLock.Current() is not null) return;

            if (!Arbejdskoe.Tag(opgave)) continue;

            Koer(delt, opgave);

            // EEN AD GANGEN. Resten ligger der stadig om et minut.
            return;
        }
    }

    private static void Koer(string delt, Arbejdsopgave opgave)
    {
        var navn = Delt.Alle().FirstOrDefault(m => m.Id == opgave.Fra)?.Navn ?? "en anden computer";

        // Lyden hentes HJEM foerst. At koere whisper direkte paa et
        // netvaerksdrev er at laese en fil paa hundrede megabyte gennem
        // netvaerket, mens modellen arbejder.
        var arbejdsmappe = Path.Combine(Path.GetTempPath(), "heypia-arbejde", opgave.Id);
        Directory.CreateDirectory(arbejdsmappe);

        var lokal = Path.Combine(arbejdsmappe, "lyd.wav");

        HeavyJobLock? laas = null;

        try
        {
            Melder?.Invoke(Sprog.T("deling.henter_lyd", navn));

            File.Copy(Arbejdskoe.Lydsti(delt, opgave.Id), lokal, overwrite: true);

            Arbejdskoe.Slaa(opgave.Id);

            // ============ AFSENDERENS MODEL ER ET OENSKE ============
            //
            // Har den her maskine den, bruges den - saa ligner udskriften
            // den, den baerbare selv ville have lavet. Har den den ikke,
            // tages den bedste, der ER her. En opgave, der falder paa en
            // model, den anden maskine ikke har hentet, er ikke til nogen
            // nytte.
            var install = WhisperInstall.Locate(opgave.Model);

            if (install.ModelPath is null) install = WhisperInstall.Locate();

            if (install.WhisperCli is null || install.ModelPath is null)
                throw new InvalidOperationException(
                    "Der er ingen motor eller sprogmodel på den her computer.");

            if (!HeavyJobLock.TryAcquire(HeavyJobKind.Transskription,
                                         $"{navn}: {opgave.Spor}", null, out laas, out _))
                return;   // en anden tung koersel naaede foerst. Naeste gang.

            var motor = new Transcriber(install.WhisperCli!);

            var svar = motor.RunAsync(new TranscriptionRequest(
                    lokal, install.ModelPath!, Path.Combine(arbejdsmappe, "ud"),
                    opgave.Sprog.Length == 0 ? "auto" : opgave.Sprog,
                    opgave.Ledetraad.Length == 0 ? null : opgave.Ledetraad,
                    ForceCpu: false,
                    SendPromptTilWhisper: opgave.Ledetraad.Length > 0),
                new Progress<TranscriptionProgress>(p =>
                    Melder?.Invoke(Sprog.T("deling.skriver_ud", navn, (int)p.Percent))),
                CancellationToken.None)
                .GetAwaiter().GetResult();

            Arbejdskoe.Svar(opgave, svar.TextPath, svar.JsonPath,
                            svar.DetectedLanguage, svar.EngineId, svar.ElapsedSeconds);

            Historik.Skriv(HaendelseType.Transskription,
                Sprog.T("deling.historik_skrevet_ud", navn),
                Sprog.T("deling.historik_skrevet_ud_detalje",
                        Math.Round(opgave.Sekunder / 60, 1), Math.Round(svar.ElapsedSeconds / 60, 1)),
                Udfald.Fuldført);

            Notifikationer.Meld();
            Melder?.Invoke("");
        }
        catch (Exception ex)
        {
            // ============ EN FEJL SKAL OGSAA HJEM ============
            //
            // Den anden computer sidder og venter. Uden et svar ville den
            // vente til opgaven blev ryddet om to dage - og aldrig faa at
            // vide hvorfor.
            try { Arbejdskoe.Svar(opgave, null, null, "", "", 0, ex.Message); }
            catch (Exception) { /* saa maa den falde for tiden. */ }

            Historik.Skriv(HaendelseType.Transskription,
                Sprog.T("deling.historik_gik_galt", navn), ex.Message, Udfald.Fejlet);

            Melder?.Invoke("");
        }
        finally
        {
            laas?.Dispose();

            try { Directory.Delete(arbejdsmappe, recursive: true); }
            catch (IOException) { /* temp rydder Windows selv. */ }
        }
    }

    // ============================================================== hent svar hjem

    /// <summary>
    /// Henter de udskrifter hjem, der er blevet lavet på den anden computer.
    /// </summary>
    /// <remarks>
    /// ET MØDE KAN HAVE TO SPOR — mikrofonen og højttaleren. De sendes hver
    /// for sig, og de kommer hjem hver for sig. Der samles først, når begge
    /// er der: en udskrift af det halve møde ser færdig ud og er det ikke.
    /// </remarks>
    private static void Hentsvar()
    {
        if (Delt.Mappe is not { } delt) return;

        var mine = Arbejdskoe.Mine();

        if (mine.Count == 0) return;

        foreach (var moede in mine.GroupBy(o => o.Moede))
        {
            var svar = moede.ToDictionary(o => o, o => Arbejdskoe.Svar(o.Id));

            // Mangler der svar paa et af sporene, ventes der.
            if (svar.Values.Any(s => s is null)) continue;

            var fejl = svar.Values.FirstOrDefault(s => s!.Fejl.Length > 0);

            if (fejl is not null)
            {
                Historik.Skriv(HaendelseType.Transskription,
                    Sprog.T("deling.historik_svar_fejl"), fejl.Fejl, Udfald.Fejlet);

                foreach (var o in moede) Arbejdskoe.Fjern(o.Id);

                Notifikationer.Meld();
                continue;
            }

            try
            {
                Saml(delt, moede.Key, svar!);
            }
            catch (Exception ex)
            {
                Historik.Skriv(HaendelseType.Transskription,
                    Sprog.T("deling.historik_kunne_ikke_samle"), ex.Message, Udfald.SeEfter);
            }

            foreach (var o in moede) Arbejdskoe.Fjern(o.Id);

            Notifikationer.Meld();
        }
    }

    /// <summary>
    /// Lægger sporene ind i mødet og samler dem til én udskrift.
    /// </summary>
    /// <remarks>
    /// FILERNE FAAR DE NAVNE, EN LOKAL KØRSEL VILLE HAVE GIVET DEM. Resten af
    /// appen — gennemlæsningen, dokumenterne, søgningen — leder efter dem dér,
    /// og en udskrift, der ligger et andet sted, findes ikke.
    /// </remarks>
    private static void Saml(string delt, string moede,
                             IReadOnlyDictionary<Arbejdsopgave, Arbejdssvar> svar)
    {
        var fundet = MeetingStore.FindById(moede)
                     ?? throw new InvalidOperationException(
                         "Mødet findes ikke længere på den her computer.");

        var mappe = fundet.Mappe;
        var model = Modelnavn(svar.Values.First().Motor);

        string? mikJson = null;
        string? loopJson = null;

        foreach (var (opgave, s) in svar)
        {
            var grund = Path.Combine(mappe, $"{opgave.Spor}_{model}");

            foreach (var endelse in new[] { ".txt", ".json" })
            {
                var kilde = Arbejdskoe.Udskriftsti(delt, opgave.Id, endelse);

                if (File.Exists(kilde)) File.Copy(kilde, grund + endelse, overwrite: true);
            }

            if (opgave.Spor == "loopback") loopJson = grund + ".json";
            else mikJson = grund + ".json";
        }

        // Et webinar har kun hoejttalersporet. Saa ER det hovedsporet.
        mikJson ??= loopJson;
        if (mikJson == loopJson) loopJson = null;

        if (mikJson is null || !File.Exists(mikJson))
            throw new InvalidOperationException("Udskriften kom hjem uden indhold.");

        var replikker = Samtale.Flet(mikJson, loopJson);
        var udskrift = Udskrift.Af(replikker);

        udskrift.GemMaskin(mappe, model);

        File.WriteAllText(Path.Combine(mappe, $"udskrift_{model}.txt"),
                          udskrift.SomTekst(MeetingStore.Load(mappe)?.Talere),
                          new System.Text.UTF8Encoding(false));

        var navn = MeetingStore.Load(mappe)?.Title ?? Path.GetFileName(mappe);

        Historik.Skriv(HaendelseType.Transskription,
            Sprog.T("deling.historik_kom_hjem", navn),
            Sprog.T("deling.historik_kom_hjem_detalje",
                    Math.Round(svar.Values.Sum(s => s.Sekunder) / 60, 1)),
            Udfald.Fuldført);
    }

    /// <summary>Modellens navn, som filerne hedder efter den.</summary>
    /// <remarks>
    /// Motorens id ser ud som «whisper-cli large-v3 cuda». Filnavnet skal kun
    /// bruge modellen, praecis som en lokal koersel gemmer den.
    /// </remarks>
    private static string Modelnavn(string motor)
    {
        foreach (var del in motor.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (del.StartsWith("large", StringComparison.OrdinalIgnoreCase)
                || del.StartsWith("medium", StringComparison.OrdinalIgnoreCase)
                || del.StartsWith("small", StringComparison.OrdinalIgnoreCase)
                || del.StartsWith("base", StringComparison.OrdinalIgnoreCase)
                || del.StartsWith("tiny", StringComparison.OrdinalIgnoreCase))
                return del;

        return "model";
    }
}
