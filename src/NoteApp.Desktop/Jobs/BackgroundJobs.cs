using System.IO;
using System.Windows;
using NoteApp.Core;
using NoteApp.Core.Documents;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Hvad der sker lige nu, til den linje der altid er fremme.
///
/// <paramref name="Procent"/> er negativ, når der ikke er noget at måle endnu
/// — under modelindlæsningen ved ingen, hvor lang tid der er tilbage, og en
/// bjælke, der står på nul i et halvt minut, ligner en, der har hængt sig.
/// </summary>
public sealed record JobStatus(string Hvad, string Besked, bool Kører, double Procent = -1,
                               string Detaljer = "");

/// <summary>
/// De lange kørsler, der skal overleve, at man går et andet sted hen i appen.
///
/// HVORFOR DET IKKE KUNNE LIGGE I SKÆRMEN
///
/// Skærmene bygges om, hver gang man skifter menupunkt — det er med vilje, så
/// listen over optagelser er frisk. Men et dokument tager to til fire
/// minutter, og i den tid vil man gerne kunne kigge på noget andet.
///
/// Lå jobbet i skærmen, havde man to dårlige valg: enten stå og se på en
/// fremdriftslinje i fire minutter, eller miste arbejdet ved at klikke et
/// andet sted hen. Derfor ligger det her, hvor det ikke hører til nogen skærm.
///
/// Fremdriften vises i MainWindow, som altid er fremme.
/// </summary>
public static class BackgroundJobs
{
    private static CancellationTokenSource? _afbryd;

    /// <summary>Rejses ved hver melding fra en kørsel — og når den slutter.</summary>
    public static event Action<JobStatus>? Ændret;

    /// <summary>Rejses når et dokument er færdigt. Bærer dokumentets id.</summary>
    public static event Action<string>? DokumentFærdigt;

    public static bool Kører => _afbryd is not null;

    /// <summary>Hvad der kører lige nu, til advarslen ved lukning.</summary>
    public static string? HvadKører { get; private set; }

    // HER LAA Moedetype - «Moedereferat», «Opgaveliste» - saa dokumentfanen
    // kunne skrive «Moedereferat er ved at blive oprettet».
    //
    // Den var IKKE til at laese rigtigt: den blev ryddet i et finally, altsaa
    // EFTER den sidste Meld. Alt, der taendte paa den, fik en sidste besked, der
    // sagde «i gang», og aldrig en efter den. Fanen og en bjaelke inde i ruden
    // blev derfor haengende, mens de to andre visninger sagde «faerdigt».
    //
    // Begge er fjernet 04-09-2026 - de var den tredje og fjerde visning af den
    // samme koersel - og saa var der ingen tilbage til at laese den her.
    // Jobbjaelken nederst i vinduet melder gennem Aendret hele vejen, ogsaa
    // afslutningen, og staar paa alle skaerme.

    public static void Afbryd() => _afbryd?.Cancel();

    /// <summary>
    /// Laver et dokument hos den europæiske leverandør i stedet for lokalt.
    ///
    /// HVORFOR DEN IKKE BARE ER ET FLAG PÅ <see cref="LavDokument"/>
    ///
    /// De to kørsler har intet til fælles ud over resultatet. Den lokale
    /// beslaglægger grafikkortet, deler mødet i blokke og tager en halv time.
    /// Denne sender én anmodning ud af huset og er tilbage på tyve sekunder.
    ///
    /// Et flag ville have skjult, at det er dét, der er forskellen — og den
    /// linje er ikke en teknisk detalje, det er den, hele produktløftet står
    /// på. Den skal kunne ses i koden uden at følge en boolsk værdi gennem tre
    /// metoder.
    /// </summary>
    /// <param name="dokumentsprog">
    /// «da» eller «en» — valgt i dialogen, ikke i skabelonen. Se
    /// <see cref="Sprogregler"/> for hvorfor.
    /// </param>
    public static async void LavDokumentISkyen(
        SkyModel model, PromptTemplate skabelon,
        IReadOnlyDictionary<string, string?> felter,
        DocumentInfo skabelonInfo, string mødeMappe,
        string dokumentsprog = Sprogregler.Standard)
    {
        if (Kører)
        {
            Dialogs.AppDialog.Vis(null, "Én ad gangen",
                $"Der kører allerede en opgave: {HvadKører}", Dialogs.Slags.Valg);
            return;
        }

        var noegle = SkyNoegle.Hent();
        if (noegle is null)
        {
            Dialogs.AppDialog.Vis(null, "Der mangler en nøgle", SkyNoegle.Vejledning, Dialogs.Slags.Valg);
            return;
        }

        _afbryd = new CancellationTokenSource();
        HvadKører = $"«{skabelonInfo.Title}»";

        var startet = DateTime.Now;
        _detaljer = $"«{skabelonInfo.Title}» · {model.Navn} i {model.Hjemland} · " +
                    // «lander under «Dokumenter»» pegede paa et MENUPUNKT, der
                    // ikke findes mere: dokumenterne staar nu som en fane paa
                    // den optagelse, de er lavet af.
                    $"startet {startet:HH:mm} · lander på optagelsens dokumentfane";

        // GEOGRAFIEN STAAR IKKE HER.
        //
        // Der stod "Sender til Frankrig". Det er rigtigt, men det er ikke
        // nyt: hvor bearbejdningen sker, er afgjort under opsaetningen og
        // staar i sidebjaelken. Gentaget ved hver koersel bliver det en
        // paamindelse om noget, brugeren allerede har taget stilling til -
        // og en paamindelse, ingen kan handle paa, er stoej.

        // ============ URET SKAL GAA, MENS DER VENTES ============
        //
        // Sekunderne stod bagt ind i den enkelte melding. Modellen melder én
        // gang - «Sender til Mistral Medium 3.5 (Frankrig) …» - og saa ikke
        // mere, foer den er faerdig. Linjen stod derfor paa «· 0 sek» i 53
        // sekunder og sprang saa til 53.
        //
        // Det er vaerre end slet ingen tid: et tal, der staar stille, siger
        // ikke «det tager lidt», det siger «her sker ingenting». Uret gaar nu
        // for sig selv hvert sekund, uafhaengigt af hvor tit modellen melder.
        _sidsteBesked = "Skriver referatet …";
        _urFaerdig = false;
        Meld($"{_sidsteBesked} · 0 sek");

        var ur = new System.Timers.Timer(1000) { AutoReset = true };
        ur.Elapsed += (_, _) =>
        {
            // Laasen holder uret ude, EFTER den afsluttende melding er sendt.
            // Uden den kunne et tik naa at overskrive «Færdigt: …» med en
            // linje om noget, der ikke koerer laengere - og den ville blive
            // staaende, for der kommer ikke flere meldinger.
            lock (_urLaas)
            {
                if (_urFaerdig) return;
                Meld($"{_sidsteBesked} · {(DateTime.Now - startet).TotalSeconds:0} sek");
            }
        };
        ur.Start();

        // Uret standses FOER hver afsluttende melding, saa den faar lov at
        // blive staaende. Se laasen i tikket ovenfor.
        void StopUret()
        {
            lock (_urLaas) { _urFaerdig = true; }
            ur.Stop();
        }

        try
        {
            var fremdrift = new Progress<LlmProgress>(p =>
            {
                lock (_urLaas)
                {
                    if (_urFaerdig) return;
                    _sidsteBesked = p.Message;
                    Meld($"{p.Message} · {(DateTime.Now - startet).TotalSeconds:0} sek");
                }
            });

            // Moedets id og titel foelger med i kvitteringen. Uden dem kan
            // en afsendelse ikke spores tilbage til, hvad den handlede om -
            // og det er netop dét, en revision spoerger om.
            var r = await new SkyRunner(noegle).KoerAsync(
                model, skabelon, skabelon.Render(felter), fremdrift, _afbryd.Token,
                kilde: skabelonInfo.SourceMeetingId,
                kildeTitel: skabelonInfo.SourceTitle,
                dokumentsprog: dokumentsprog);

            DraftStore.Save(mødeMappe, skabelon, r.SomLlmResult(),
                motor: $"{model.Leverandoer} ({model.Hjemland})");

            skabelonInfo.Markdown = r.Tekst.Trim();
            var odt = DocumentStore.Save(skabelonInfo);

            // MODELLEN staar der - det er proveniens, praecis som gguf-navnet
            // er det for en lokal koersel. LANDET goer ikke: det er afgjort
            // under opsaetningen og gaelder alle koersler, saa det ville staa
            // paa hver eneste linje uden at skille dem fra hinanden.
            Historik.Skriv(HaendelseType.Dokument, $"Dokument oprettet: {skabelonInfo.Title}",
                $"Mødetype «{skabelon.Name}» · {model.Navn} · " +
                $"{r.TokensInd} tokens sendt, {r.TokensUd} modtaget · €{r.PrisEur:0.0000}",
                Udfald.Fuldført, model.Navn, odt, r.Forloebet.TotalSeconds,
                kilde: skabelonInfo.Id);
            Notifikationer.Meld();

            StopUret();
            Meld($"Færdigt: {Path.GetFileName(odt)} · {r.Forloebet.TotalSeconds:0} sek", kører: false);
            DokumentFærdigt?.Invoke(skabelonInfo.Id);
        }
        catch (OperationCanceledException)
        {
            Historik.Skriv(HaendelseType.Dokument, $"Dokument afbrudt: {skabelonInfo.Title}",
                "Brugeren stoppede kørslen", Udfald.Afbrudt, model.Navn);
            Notifikationer.Meld();

            StopUret();
            Meld("Afbrudt. Der blev ikke gemt noget dokument.", kører: false);
        }
        catch (Exception ex)
        {
            Historik.Skriv(HaendelseType.Dokument, $"Dokument fejlede: {skabelonInfo.Title}",
                ex.Message, Udfald.Fejlet, model.Navn);
            Notifikationer.Meld();

            StopUret();
            Meld($"Det gik galt: {ex.Message}", kører: false);
        }
        finally
        {
            StopUret();
            ur.Dispose();

            _afbryd?.Dispose();
            _afbryd = null;
            HvadKører = null;
        }
    }

    // HER LAA LavDokument - den lokale vej.
    //
    // Den koerte llama.cpp paa denne maskine gennem Referatbygger, med
    // opdeling i blokke, fordi et 61-minutters moede ellers blev skubbet over
    // paa processoren. Fjernet 18-08-2026: maalt paa det samme moede tabte
    // den 72 % af navnene og brugte 59 minutter, hvor LavDokumentISkyen
    // ovenfor bruger 25 sekunder og taber 23 %. Se doc/maaling-sky.md.
    //
    // LlmRunner og Referatbygger ligger stadig i Core og bruges fra
    // kommandolinjen (heypia referat). Det er dér, sammenligningen mellem
    // lokalt og Europa skal kunne koeres igen - en maaling, man ikke kan
    // gentage, er en paastand.

    /// <summary>
    /// Hvad kørslen laver, hvornår den begyndte, og hvor resultatet lander.
    ///
    /// Det står fast under fremdriftslinjen, mens der arbejdes. Grunden er
    /// målt: en kørsel, der var skønnet til ti minutter, tog femogtyve — og
    /// uden noget at holde fast i så det ud, som om den var gået i stå.
    ///
    /// Starttidspunktet er det vigtigste af det. Med det kan man selv se, at
    /// der ER gået fire minutter og ikke fyrre, og det er dét spørgsmål, man
    /// stiller, når man har kigget væk et stykke tid.
    /// </summary>
    private static string _detaljer = "";

    /// <summary>Den seneste melding UDEN sekunder — uret saetter dem paa.</summary>
    private static string _sidsteBesked = "";

    /// <summary>Sikrer, at et tik ikke kan overskrive den afsluttende melding.</summary>
    private static readonly object _urLaas = new();

    private static bool _urFaerdig;

    private static void Meld(string besked, bool kører = true, double procent = -1) =>
        Ændret?.Invoke(new JobStatus("Dokument", besked, kører, procent, _detaljer));
}
