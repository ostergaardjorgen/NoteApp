using NoteApp.Core;
using NoteApp.Core.Deling;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// En maskine: sin egen maskinrod, sit eget nøglepar.
/// </summary>
/// <remarks>
/// Maskinrod udledes af datamappen, og prøvernes datamappe kommer fra
/// miljøvariablen. Ved at skifte den skifter vi maskine — det er dét, der
/// gør, at to installationer kan spilles på én computer uden at snyde:
/// de deler ingen filer ud over den fælles mappe.
///
/// Den lå inde i Delingstest, indtil arbejdskøen skulle prøves af med de
/// samme to maskiner. To kopier af den her ville drive fra hinanden.
/// </remarks>
/// <summary>
/// Navnet på den gruppe, alle prøver med <see cref="Proevemaskine"/> hører i.
/// </summary>
/// <remarks>
/// ============ DE MÅ IKKE KØRE SIDE OM SIDE ============
///
/// <see cref="Proevemaskine.Tag"/> skifter en MILJØVARIABEL, og en
/// miljøvariabel hører til processen — ikke til tråden. xUnit kører
/// prøveklasser samtidig som standard, og så sætter den ene maskines
/// oprydning variablen tilbage, mens en anden klasse stadig regner med sin.
/// Den klasse skriver derefter i den RIGTIGE datamappe.
///
/// DET ER SKET. Den 07-09-2026 kom Arkivtest til som den fjerde klasse, og
/// prøverne lagde en tom «journal-laest.json» i C:\AppNoter. Værnet i
/// proev.ps1 fangede den; ingen prøve fejlede.
///
/// Hører klasserne til den samme gruppe, kører xUnit dem efter hinanden. Resten
/// af prøverne kører stadig side om side.
/// </remarks>
[CollectionDefinition(Maskinhold.Navn)]
public sealed class Maskinhold
{
    public const string Navn = "Maskiner";
}

internal sealed class Proevemaskine : IDisposable
{
    private readonly string? _foer;

    public Proevemaskine(string navn, string delt, Maskinrolle rolle = Maskinrolle.Primaer)
    {
        Sti = Path.Combine(Path.GetTempPath(), "heypia-maskine", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Sti);

        _foer = Environment.GetEnvironmentVariable(UserDataPaths.OverrideVariable);

        Tag();

        Maskinid.Navn = navn;
        Maskinid.Rolle = rolle;
        Maskinid.Deltmappe = delt;

        Navn = navn;
        Slip();
    }

    public string Sti { get; }
    public string Navn { get; }

    /// <summary>Sætter den her maskine som den, koden kører på.</summary>
    public void Tag()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, Sti);
        UserDataPaths.Glem();
    }

    private void Slip()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, _foer);
        UserDataPaths.Glem();
    }

    public void Dispose()
    {
        Slip();

        try { if (Directory.Exists(Sti)) Directory.Delete(Sti, recursive: true); }
        catch (IOException) { /* temp rydder Windows selv */ }
    }

    /// <summary>En frisk fælles mappe.</summary>
    public static string Nydeltmappe()
    {
        var sti = Path.Combine(Path.GetTempPath(), "heypia-delt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sti);

        return sti;
    }

    /// <summary>To maskiner, der har godkendt hinanden begge veje.</summary>
    /// <remarks>
    /// Det er den tilstand, alt arbejde kræver — og den, der skal skrives fem
    /// linjer for hver gang, hvis den ikke står ét sted.
    /// </remarks>
    public static void Godkend(Proevemaskine a, Proevemaskine b)
    {
        // Der slaas op paa ID og ikke paa «den anden»: der kan vaere tre
        // maskiner i mappen, og saa er «den anden» ikke et svar.
        a.Tag();
        Delt.Meld();
        var aId = Maskinid.Id;

        b.Tag();
        Delt.Meld();
        var bId = Maskinid.Id;

        Parring.Betro(Delt.Alle().Single(m => m.Id == aId));
        Delt.Meld();

        a.Tag();
        Parring.Betro(Delt.Alle().Single(m => m.Id == bId));
        Delt.Meld();
    }
}
