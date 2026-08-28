using NoteApp.Core;

namespace NoteApp.Tests;

/// <summary>
/// En tom datamappe, prøven har for sig selv.
///
/// HVORFOR DEN FINDES
///
/// Næsten alt i kernen skriver i <c>UserDataPaths.Root</c>, og den peger på
/// C:\AppNoter, hvor der ligger rigtige optagelser. En prøve, der rammer den
/// mappe, er ikke en prøve — det er en risiko.
///
/// <c>NOTEAPP_DATA</c> vinder altid over alt andet og læses forfra hver gang,
/// så det er nok at sætte den. Den blev lagt ind i UserDataPaths netop med
/// prøver for øje.
///
/// Bruges med <c>using</c>, så mappen ryddes, også når prøven fejler.
/// </summary>
public sealed class Proevemappe : IDisposable
{
    private readonly string? _foer;

    public Proevemappe()
    {
        Sti = Path.Combine(Path.GetTempPath(), "heypia-proeve", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Sti);

        _foer = Environment.GetEnvironmentVariable(UserDataPaths.OverrideVariable);
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, Sti);

        UserDataPaths.EnsureCreated();

        // SPROGET LAESES FORFRA. Sprog holder teksterne i en statisk cache, og
        // den ville ellers pege paa den FOERSTE proeves datamappe resten af
        // koerslen - en faelde, der foerst viser sig, naar nogen laver en
        // proeve, der skifter sprog.
        AppSettings.Reload();
        Sprog.Genindlaes();

        // OG HJAELPEN. Den holder afsnittene i den samme slags cache og ville
        // ellers vise den FOERSTE proeves filer resten af koerslen. Proeverne
        // bestod hver for sig og faldt samlet - den slags er den vaerste, for
        // den ser ud som en fejl i koden.
        Hjaelp.Genindlaes();
    }

    public string Sti { get; }

    /// <summary>Mappen, optagelserne ligger i.</summary>
    public string Optagelser => UserDataPaths.Meetings;

    /// <summary>
    /// Laver en optagelsesmappe med lyd og eventuelt en udskrift.
    ///
    /// <paramref name="dageSiden"/> sætter udskriftens dato — det er DEN,
    /// oprydningen måler alderen på, ikke lydens.
    /// </summary>
    public string Optagelse(string navn,
                            int lydBytes = 4096,
                            bool medUdskrift = true,
                            int dageSiden = 0,
                            bool medLoopback = false)
    {
        var mappe = Path.Combine(Optagelser, navn);
        Directory.CreateDirectory(mappe);

        File.WriteAllBytes(Path.Combine(mappe, "mikrofon.wav"), new byte[lydBytes]);

        if (medLoopback)
            File.WriteAllBytes(Path.Combine(mappe, "loopback.wav"), new byte[lydBytes]);

        if (medUdskrift)
        {
            var sti = Path.Combine(mappe, "udskrift_large-v3.txt");
            File.WriteAllText(sti, "[00:00:00] Prøvetekst.");
            File.SetLastWriteTime(sti, DateTime.Now.AddDays(-dageSiden));
        }

        return mappe;
    }

    /// <summary>En fil, der ikke er lyd. Bruges til at vise, at den bliver liggende.</summary>
    public static string Fremmedfil(string mappe, string navn, int bytes = 512)
    {
        var sti = Path.Combine(mappe, navn);
        File.WriteAllBytes(sti, new byte[bytes]);
        return sti;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, _foer);

        try { if (Directory.Exists(Sti)) Directory.Delete(Sti, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
