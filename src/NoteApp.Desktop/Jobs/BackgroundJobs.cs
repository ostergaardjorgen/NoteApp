using System.IO;
using System.Windows;
using NoteApp.Core;
using NoteApp.Core.Documents;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Jobs;

/// <summary>Hvad der sker lige nu, til den linje der altid er fremme.</summary>
public sealed record JobStatus(string Hvad, string Besked, bool Kører);

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

        Meld("Starter sprogmodellen …");

        try
        {
            var fremdrift = new Progress<LlmProgress>(p => Meld(p.Message));
            var runner = new LlmRunner(cli);
            var r = await runner.RunAsync(model, skabelon, skabelon.Render(felter), fremdrift, _afbryd.Token);

            // Udkastet gemmes raat ved siden af optagelsen — arbejdsdokumentet.
            // Dokumentet er det, man sender videre.
            DraftStore.Save(mødeMappe, skabelon, r);

            skabelonInfo.Markdown = r.Text.Trim();
            var odt = DocumentStore.Save(skabelonInfo);

            Meld($"Færdigt: {Path.GetFileName(odt)} · {r.Elapsed.TotalSeconds:0} sek", kører: false);
            DokumentFærdigt?.Invoke(skabelonInfo.Id);
        }
        catch (OperationCanceledException)
        {
            Meld("Afbrudt. Der blev ikke gemt noget dokument.", kører: false);
        }
        catch (Exception ex)
        {
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

    private static void Meld(string besked, bool kører = true) =>
        Ændret?.Invoke(new JobStatus("Dokument", besked, kører));
}
