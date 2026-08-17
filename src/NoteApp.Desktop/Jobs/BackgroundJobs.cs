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
    public static async void LavDokumentISkyen(
        SkyModel model, PromptTemplate skabelon,
        IReadOnlyDictionary<string, string?> felter,
        DocumentInfo skabelonInfo, string mødeMappe)
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
                    $"startet {startet:HH:mm} · lander under «Dokumenter»";

        Meld($"Sender til {model.Hjemland} …");

        try
        {
            var fremdrift = new Progress<LlmProgress>(p =>
                Meld($"{p.Message} · {(DateTime.Now - startet).TotalSeconds:0} sek"));

            var r = await new SkyRunner(noegle).KoerAsync(
                model, skabelon, skabelon.Render(felter), fremdrift, _afbryd.Token);

            DraftStore.Save(mødeMappe, skabelon, r.SomLlmResult(),
                motor: $"{model.Leverandoer} ({model.Hjemland})");

            skabelonInfo.Markdown = r.Tekst.Trim();
            var odt = DocumentStore.Save(skabelonInfo);

            // PROVENIENSEN SKAL VISE, AT DET GIK UD AF HUSET. Historikken er
            // det eneste sted, man bagefter kan svare paa, hvilke moeder der er
            // sendt afsted - og det spoergsmaal kommer, den dag nogen spoerger.
            Historik.Skriv(HaendelseType.Dokument, $"Dokument oprettet i {model.Hjemland}: {skabelonInfo.Title}",
                $"Skabelon «{skabelon.Name}» · {model.Navn} hos {model.Leverandoer} · " +
                $"{r.TokensInd} tokens sendt, {r.TokensUd} modtaget · ${r.PrisUsd:0.0000}",
                Udfald.Fuldført, model.Navn, odt, r.Forloebet.TotalSeconds);
            Notifikationer.Meld();

            Meld($"Færdigt: {Path.GetFileName(odt)} · {r.Forloebet.TotalSeconds:0} sek", kører: false);
            DokumentFærdigt?.Invoke(skabelonInfo.Id);
        }
        catch (OperationCanceledException)
        {
            Historik.Skriv(HaendelseType.Dokument, $"Dokument afbrudt: {skabelonInfo.Title}",
                "Brugeren stoppede kørslen", Udfald.Afbrudt, model.Navn);
            Notifikationer.Meld();

            Meld("Afbrudt. Der blev ikke gemt noget dokument.", kører: false);
        }
        catch (Exception ex)
        {
            Historik.Skriv(HaendelseType.Dokument, $"Dokument fejlede: {skabelonInfo.Title}",
                ex.Message, Udfald.Fejlet, model.Navn);
            Notifikationer.Meld();

            Meld($"Det gik galt: {ex.Message}", kører: false);
        }
        finally
        {
            _afbryd?.Dispose();
            _afbryd = null;
            HvadKører = null;
        }
    }

    /// <summary>
    /// Laver et dokument. Kaldes og glemmes — resultatet kommer via
    /// <see cref="DokumentFærdigt"/>.
    ///
    /// Der køres kun ét ad gangen. To sprogmodeller samtidig ville ikke være
    /// dobbelt så hurtigt; de ville slås om det samme grafikkort og begge
    /// blive langsommere.
    /// </summary>
    public static async void LavDokument(
        string cli, string model, PromptTemplate skabelon,
        IReadOnlyDictionary<string, string?> felter,
        DocumentInfo skabelonInfo, string mødeMappe)
    {
        if (Kører)
        {
            MessageBox.Show(
                $"Der kører allerede en opgave: {HvadKører}\n\n" +
                "Der køres kun én ad gangen — to sprogmodeller samtidig ville ikke være dobbelt så " +
                "hurtigt, de ville slås om det samme grafikkort og begge blive langsommere.",
                "Én ad gangen", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _afbryd = new CancellationTokenSource();
        HvadKører = $"«{skabelonInfo.Title}»";

        var startet = DateTime.Now;
        _detaljer =
            $"«{skabelonInfo.Title}» · skabelon: {skabelon.Name} · startet {startet:HH:mm} · " +
            "lander under «Dokumenter»";

        Meld("Starter sprogmodellen …");

        try
        {
            // Tiden staar i beskeden. En besked, der har staaet uaendret i ti
            // minutter, kan ikke skelnes fra en, der haenger — med et ur, der
            // taeller, kan den.
            var fremdrift = new Progress<LlmProgress>(p =>
            {
                var gaaet = DateTime.Now - startet;
                var ur = gaaet.TotalMinutes < 1
                    ? $"{gaaet.TotalSeconds:0} sek"
                    : $"{gaaet.TotalMinutes:0} min";

                var besked = p.Ord > 0
                    ? $"{p.Ord} ord skrevet · {ur}"
                    : $"{p.Message} · {ur}";

                Meld(besked, procent: p.Ord > 0 ? p.Percent : p.Percent > 0 ? p.Percent : -1);
            });

            // Referatbygger frem for LlmRunner direkte. Den deler lange møder
            // op, saa de kan ligge paa grafikkortet — et 61-minutters moede tog
            // 28 minutter i een koersel, fordi modellen blev skubbet over paa
            // processoren. Korte moeder koerer den stadig i een omgang.
            //
            // Brugeren ser ikke opdelingen: fremdriften er eet tal, og
            // beskederne siger hvad der sker, ikke hvilket stykke der arbejdes
            // paa.
            var bygger = new Referatbygger(new LlmRunner(cli));
            var r = await bygger.ByggAsync(model, skabelon, felter, fremdrift, _afbryd.Token);

            // Udkastet gemmes raat ved siden af optagelsen — arbejdsdokumentet.
            // Dokumentet er det, man sender videre.
            DraftStore.Save(mødeMappe, skabelon, r);

            skabelonInfo.Markdown = r.Text.Trim();
            var odt = DocumentStore.Save(skabelonInfo);

            Historik.Skriv(HaendelseType.Dokument, $"Dokument oprettet: {skabelonInfo.Title}",
                $"Skabelon «{skabelon.Name}» · {r.ResponseTokens} tokens · {r.TokensPerSecond:0.0}/sek",
                Udfald.Fuldført, skabelonInfo.Model, odt, r.Elapsed.TotalSeconds);
            Notifikationer.Meld();

            Meld($"Færdigt: {Path.GetFileName(odt)} · {r.Elapsed.TotalSeconds:0} sek", kører: false);
            DokumentFærdigt?.Invoke(skabelonInfo.Id);
        }
        catch (OperationCanceledException)
        {
            Historik.Skriv(HaendelseType.Dokument, $"Dokument afbrudt: {skabelonInfo.Title}",
                "Brugeren stoppede kørslen", Udfald.Afbrudt, skabelonInfo.Model);
            Notifikationer.Meld();

            Meld("Afbrudt. Der blev ikke gemt noget dokument.", kører: false);
        }
        catch (Exception ex)
        {
            Historik.Skriv(HaendelseType.Dokument, $"Dokument fejlede: {skabelonInfo.Title}",
                ex.Message, Udfald.Fejlet, skabelonInfo.Model);
            Notifikationer.Meld();

            Meld($"Dokumentet blev ikke lavet: {ex.Message}", kører: false);

            MessageBox.Show(
                $"Dokumentet kunne ikke laves.\n\n{ex.Message}",
                "Kunne ikke lave dokument", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _afbryd?.Dispose();
            _afbryd = null;
            HvadKører = null;
        }
    }

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

    private static void Meld(string besked, bool kører = true, double procent = -1) =>
        Ændret?.Invoke(new JobStatus("Dokument", besked, kører, procent, _detaljer));
}
