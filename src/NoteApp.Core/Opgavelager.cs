using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Alle opgaver, ét sted.
///
/// HVORFOR DE FLYTTEDE UD AF OPTAGELSERNES MAPPER
///
/// Indtil 24-08-2026 lå hver optagelses opgaver i `opgaver.json` ved siden af
/// lyden. Begrundelsen var god: sletter man et møde, forsvinder dets opgaver
/// med det, og der er ingen central fil, der kan blive uenig med
/// virkeligheden.
///
/// Men den holdt kun, så længe ALLE opgaver kom fra et møde. En opgave, man
/// skriver i hånden, hører ikke til en optagelse — og en opgave, der hentes
/// fra Google, gør det slet ikke. De skulle så have et opdigtet hjem i en
/// mappe, der lod som om, der var optaget noget.
///
/// En opgave er en opgave. Hvor den kom fra, er en OPLYSNING på den, ikke
/// dens adresse.
///
/// HVAD DER GIK TABT VED FLYTNINGEN, OG HVAD DER KOM I STEDET
///
/// Sletter man en optagelse nu, bliver dens opgaver stående. Det er med
/// vilje: det, der blev aftalt, holder op med at gælde, fordi lydfilen blev
/// ryddet op. Herkomsten peger så på noget, der ikke findes, og det siges —
/// se hvordan Cockpittet håndterer et tomt mødelink.
///
/// ÉN FIL OG IKKE ÉN PR. OPGAVE. Samme afvejning som kalenderen, og samme
/// svar: en fil pr. opgave giver tusind filer for at undgå én, der kan blive
/// ødelagt. Filen skrives helt, hver gang, og der er tale om hundredvis af
/// opgaver — ikke millioner.
/// </summary>
public static class Opgavelager
{
    /// <summary>
    /// Mappen med opgaver. Egen mappe frem for en fil i roden, fordi der
    /// kommer mere: en fil pr. integration, den dag opgaver hentes udefra.
    /// </summary>
    public static string Mappe => Path.Combine(UserDataPaths.Root, "Opgaver");

    public static string Fil => Path.Combine(Mappe, "opgaver.json");

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Alle opgaver. Læses fra disken hver gang.
    ///
    /// En liste, der holdes i hukommelsen, kommer ud af trit med filen — og så
    /// viser Cockpittet en opgave, man netop har krydset af et andet sted i
    /// appen.
    /// </summary>
    public static List<Opgave> Alle()
    {
        Flyt();

        try
        {
            if (!File.Exists(Fil)) return new List<Opgave>();

            return JsonSerializer.Deserialize<List<Opgave>>(
                       File.ReadAllText(Fil, Encoding.UTF8)) ?? new List<Opgave>();
        }
        catch (Exception)
        {
            // En oedelagt fil betyder «ingen opgaver» frem for et nedbrud. Den
            // bliver liggende, saa den kan reddes i haanden - der skrives ikke
            // oven i den, foer nogen gemmer noget.
            return new List<Opgave>();
        }
    }

    public static void Gem(List<Opgave> opgaver)
    {
        Directory.CreateDirectory(Mappe);
        File.WriteAllText(Fil, JsonSerializer.Serialize(opgaver, Format), Encoding.UTF8);
    }

    /// <summary>Lægger en opgave ind — eller retter den, hvis den findes.</summary>
    public static void Gem(Opgave o)
    {
        var alle = Alle();
        var nr = alle.FindIndex(x => x.Id == o.Id);

        if (nr < 0) alle.Add(o);
        else alle[nr] = o;

        Gem(alle);
    }

    public static void Slet(Guid id)
    {
        var alle = Alle();
        alle.RemoveAll(o => o.Id == id);
        Gem(alle);
    }

    /// <summary>
    /// Erstatter alt fra én kilde med det, der lige er hentet.
    ///
    /// DET ER EN AFLØSNING, IKKE EN SAMMENFLETNING — samme mønster som
    /// kalenderen. Alt fra kilden erstattes, så en opgave, der er slettet hos
    /// Google, også forsvinder her. Alternativet — kun at lægge til — ville
    /// betyde, at en slettet opgave blev stående for evigt, og så holder man
    /// op med at stole på listen.
    ///
    /// DET, BRUGEREN HAR SAT PÅ HER, OVERLEVER. Prioritet findes ikke i Google
    /// Tasks; den er appens eget felt og bæres over på den nye udgave af den
    /// samme opgave, genkendt på fremmed-id'et.
    ///
    /// LOKALE OPGAVER RØRES ALDRIG. De hører til appen og har intet med
    /// hentningen at gøre.
    /// </summary>
    public static int Afloes(Opgavekilde kilde, IEnumerable<Opgave> hentede)
    {
        if (kilde == Opgavekilde.Lokal)
            throw new ArgumentException("Lokale opgaver afløses ikke.", nameof(kilde));

        var alle = Alle();

        var gamle = alle.Where(o => o.Herkomst == kilde && o.FremmedId.Length > 0)
                        .GroupBy(o => o.FremmedId, StringComparer.Ordinal)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        alle.RemoveAll(o => o.Herkomst == kilde);

        var n = 0;

        // ============ EN NY OPGAVE SKAL IKKE FORSVINDE ============
        //
        // Har man selv sat raekkefoelgen, har alt andet et tal fra 1 og opad.
        // En hentet opgave uden tal ville faa nul og lande oeverst - hvilket
        // er rigtigt, men de ville alle sammen faa nul og altsaa staa i en
        // klump uden indbyrdes orden. Her taelles der nedad i stedet, saa
        // den nyeste hentning staar oeverst og i den raekkefoelge, den kom.
        var plads = AppSettings.Current.OpgaverManueltSorteret
            ? alle.Select(o => o.Raekkefoelge).DefaultIfEmpty(1).Min() - 1
            : 0;

        foreach (var nyOpgave in hentede)
        {
            if (gamle.TryGetValue(nyOpgave.FremmedId, out var gammel))
            {
                // Appens egne felter. De findes ikke hos Google og ville
                // ellers blive nulstillet ved hver eneste hentning.
                nyOpgave.Prioritet = gammel.Prioritet;
                nyOpgave.Ejer = gammel.Ejer;

                // ============ OGSAA PLADSEN I LISTEN ============
                //
                // Den stod ikke her, og foelgen var, at hver eneste hentning
                // nulstillede den haandsatte raekkefoelge paa ALLE opgaver fra
                // Google: de fik nul og sprang til tops. Maalt 05-09-2026 -
                // to Google-opgaver uden frist laa oeverst over fire med
                // frist, fordi nul er mindre end et.
                nyOpgave.Raekkefoelge = gammel.Raekkefoelge;
            }
            else if (plads < 0)
            {
                nyOpgave.Raekkefoelge = plads--;
            }

            alle.Add(nyOpgave);
            n++;
        }

        Gem(alle);
        return n;
    }

    /// <summary>Fjerner alt fra én kilde. Kaldes, når en integration slås fra.</summary>
    public static void Fjern(Opgavekilde kilde)
    {
        if (kilde == Opgavekilde.Lokal) return;

        var alle = Alle();
        alle.RemoveAll(o => o.Herkomst == kilde);
        Gem(alle);
    }

    // ------------------------------------------------------------ flytningen

    private static bool _flyttet;

    /// <summary>
    /// Henter de gamle opgaver ud af optagelsernes mapper. Kører én gang.
    ///
    /// DEN GAMLE FIL SLETTES IKKE — den omdøbes til `opgaver-flyttet.json`.
    /// To grunde: den er brugerens data, og en flytning, der sletter kilden,
    /// kan ikke fortrydes, hvis den gik galt. Og omdøbningen er dét, der
    /// forhindrer, at den samme opgave kommer med igen ved en senere kørsel.
    ///
    /// Går flytningen galt for ÉN mappe, fortsættes der med de øvrige. En
    /// enkelt ulæselig fil må ikke betyde, at ingen opgaver kommer med.
    /// </summary>
    private static void Flyt()
    {
        if (_flyttet) return;
        _flyttet = true;

        try
        {
            if (!Directory.Exists(UserDataPaths.Meetings)) return;

            var fundne = new List<Opgave>();
            var gamle = new List<string>();

            foreach (var mappe in Directory.EnumerateDirectories(UserDataPaths.Meetings))
            {
                var sti = Opgaveliste.GammelSti(mappe);
                if (!File.Exists(sti)) continue;

                try
                {
                    MeetingMetadata? meta = null;
                    try { meta = MeetingStore.Load(mappe); } catch (Exception) { }

                    var liste = JsonSerializer.Deserialize<Opgaveliste>(
                        File.ReadAllText(sti, Encoding.UTF8));

                    if (liste is null) continue;

                    foreach (var o in liste.Opgaver)
                    {
                        // HERKOMSTEN SKAL MED, ellers mister opgaven vejen
                        // tilbage til det, der blev sagt - og dét link er hele
                        // grunden til, at appen findes.
                        o.MoedeId = meta?.Id.ToString() ?? "";
                        o.Moedetitel = meta?.Title ?? Path.GetFileName(mappe);

                        fundne.Add(o);
                    }

                    // AFVISNINGERNE SKAL OGSAA MED. De laa i den samme fil
                    // og hoerer stadig til moedet - uden dem dukker de samme
                    // otte forslag op igen, foerste gang fanen aabnes.
                    if (liste.Afvist.Count > 0)
                    {
                        var ny = Opgaveliste.Hent(mappe);

                        foreach (var a in liste.Afvist)
                            if (!ny.Afvist.Contains(a)) ny.Afvist.Add(a);

                        try { ny.Gem(mappe); } catch (Exception) { }
                    }

                    gamle.Add(sti);
                }
                catch (Exception)
                {
                    // Denne ene mappe kunne ikke laeses. De oevrige skal med.
                }
            }

            if (gamle.Count == 0) return;

            // De nye foerst, hvis der allerede ligger noget. Saa gaar intet
            // tabt, hvis flytningen af en eller anden grund koeres to gange.
            var nu = Alle_UdenFlytning();
            var kendte = nu.Select(o => o.Id).ToHashSet();

            nu.AddRange(fundne.Where(o => !kendte.Contains(o.Id)));
            Gem(nu);

            foreach (var sti in gamle)
            {
                try { File.Move(sti, sti.Replace("opgaver.json", "opgaver-flyttet.json"), true); }
                catch (Exception) { }
            }
        }
        catch (Exception)
        {
            // Flytningen maa ikke kunne forhindre appen i at aabne. Gaar den
            // galt, staar de gamle filer der stadig, og der kan proeves igen.
        }
    }

    private static List<Opgave> Alle_UdenFlytning()
    {
        try
        {
            if (!File.Exists(Fil)) return new List<Opgave>();

            return JsonSerializer.Deserialize<List<Opgave>>(
                       File.ReadAllText(Fil, Encoding.UTF8)) ?? new List<Opgave>();
        }
        catch (Exception)
        {
            return new List<Opgave>();
        }
    }
}
