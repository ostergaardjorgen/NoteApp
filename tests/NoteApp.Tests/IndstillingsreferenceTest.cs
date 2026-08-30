using System;
using System.IO;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, at der kun findes ÉT indstillingsobjekt.
///
/// DEN HER PRØVE FINDES, FORDI VALGTE ENHEDER FORSVANDT.
///
/// Reload() satte «_current = null», så næste opslag lavede et nyt objekt.
/// Det så uskyldigt ud. Men halvdelen af appen gemmer en reference — «var s =
/// AppSettings.Current» — og lever videre med den; mødevagten holder sin fra
/// appen starter til den lukkes.
///
/// Efter en Reload sad de med det GAMLE objekt. Skrev brugeren en ny mikrofon
/// i det nye, og gemte mødevagten bagefter sit gamle, blev mikrofonen skrevet
/// væk igen — tilfældigt, alt efter hvem der gemte sidst.
///
/// Målt 30-08-2026: mikrofonen stod gemt kl. 14:23:55 og var væk kl. 14:24:00,
/// uden at nogen havde rørt noget.
/// </summary>
public class IndstillingsreferenceTest : IDisposable
{
    private readonly string _mappe;
    private readonly string? _foer;

    public IndstillingsreferenceTest()
    {
        _mappe = Path.Combine(Path.GetTempPath(), "heypia-ref-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_mappe);

        _foer = Environment.GetEnvironmentVariable(UserDataPaths.OverrideVariable);
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, _mappe);
        AppSettings.Reload();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, _foer);
        AppSettings.Reload();
        try { Directory.Delete(_mappe, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Reload_bytter_ikke_objektet_ud()
    {
        // Det er hele rettelsen. Er det det samme objekt, kan ingen sidde med
        // et foraeldet.
        var foer = AppSettings.Current;
        AppSettings.Reload();

        Assert.Same(foer, AppSettings.Current);
    }

    [Fact]
    public void En_gammel_reference_ser_de_nye_vaerdier()
    {
        // PRAECIS DET, DER SKETE. Moedevagten holder sin reference fra appen
        // starter. Efter en Reload skal den se det samme som alle andre.
        var moedevagtens = AppSettings.Current;

        AppSettings.Current.MicrophoneId = "gammel";
        AppSettings.Current.Save();

        // Noget udefra retter filen - en gendannelse fra en sikkerhedskopi.
        var fil = Path.Combine(_mappe, "indstillinger.json");
        File.WriteAllText(fil,
            File.ReadAllText(fil).Replace("\"gammel\"", "\"ny\""));

        AppSettings.Reload();

        Assert.Equal("ny", moedevagtens.MicrophoneId);
        Assert.Equal("ny", AppSettings.Current.MicrophoneId);
    }

    [Fact]
    public void En_gemning_fra_en_gammel_reference_skriver_ikke_noget_vaek()
    {
        // Det var saadan mikrofonen forsvandt: brugeren valgte en, og
        // moedevagten gemte bagefter sit gamle objekt oven i.
        var moedevagtens = AppSettings.Current;

        AppSettings.Reload();

        // Brugeren vaelger sin mikrofon i det "nye" objekt.
        AppSettings.Current.MicrophoneId = "jabra";
        AppSettings.Current.Save();

        // Moedevagten gemmer sit - som skal vaere det SAMME objekt.
        moedevagtens.Save();

        AppSettings.Reload();
        Assert.Equal("jabra", AppSettings.Current.MicrophoneId);
    }
}
