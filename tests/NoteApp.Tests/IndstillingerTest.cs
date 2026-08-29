using System;
using System.IO;
using System.Text;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, at indstillingerne overlever en afbrydelse.
///
/// DEN HER PRØVE FINDES PÅ GRUND AF ET TAB. Gemningen var ét kald til
/// File.WriteAllText, og det er ikke én handling: filen tømmes først og
/// skrives bagefter. Dør programmet derimellem — og det gør det, hver gang
/// der udgives, for udgivelsen lukker en kørende app med magt — ligger der en
/// halv fil tilbage.
///
/// Indlæsningen svarede så med standardværdier, og næste gemning skrev dem
/// oven i. Mikrofonen, sproget, genvejstasten, mapperne: væk, uden en linje
/// nogen steder. Man opdagede det ved, at velkomstforløbet kom igen.
///
/// Set 29-08-2026 på udviklingsmaskinen.
/// </summary>
public class IndstillingerTest : IDisposable
{
    private readonly string _mappe;
    private readonly string? _foer;

    public IndstillingerTest()
    {
        _mappe = Path.Combine(Path.GetTempPath(), "heypia-inds-" + Guid.NewGuid().ToString("N")[..8]);
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

    private string Fil => Path.Combine(_mappe, "indstillinger.json");
    private string Kopi => Fil + ".forrige";

    [Fact]
    public void Det_gemte_kan_laeses_igen()
    {
        AppSettings.Current.Sprog = "da";
        AppSettings.Current.SetupCompleted = true;
        AppSettings.Current.Save();

        AppSettings.Reload();

        Assert.Equal("da", AppSettings.Current.Sprog);
        Assert.True(AppSettings.Current.SetupCompleted);
        Assert.Null(AppSettings.Indlaesningsfejl);
    }

    [Fact]
    public void Anden_gemning_lader_den_forrige_udgave_ligge()
    {
        AppSettings.Current.Sprog = "da";
        AppSettings.Current.Save();

        AppSettings.Current.Sprog = "en";
        AppSettings.Current.Save();

        Assert.True(File.Exists(Kopi));
        Assert.Contains("\"da\"", File.ReadAllText(Kopi, Encoding.UTF8));
    }

    [Fact]
    public void En_halv_fil_koster_ikke_indstillingerne()
    {
        // PRAECIS DET, DER SKETE. Appen blev draebt midt i en skrivning.
        AppSettings.Current.Sprog = "da";
        AppSettings.Current.SetupCompleted = true;
        AppSettings.Current.Save();

        AppSettings.Current.Sprog = "en";
        AppSettings.Current.Save();          // nu findes .forrige med "da"

        File.WriteAllText(Fil, "{ \"Sprog\": \"en", Encoding.UTF8);   // halv fil
        AppSettings.Reload();

        Assert.Equal("da", AppSettings.Current.Sprog);
        Assert.True(AppSettings.Current.SetupCompleted);
        Assert.NotNull(AppSettings.Indlaesningsfejl);
    }

    [Fact]
    public void En_tom_fil_koster_dem_heller_ikke()
    {
        AppSettings.Current.SetupCompleted = true;
        AppSettings.Current.Save();
        AppSettings.Current.Save();          // .forrige findes nu

        File.WriteAllText(Fil, "", Encoding.UTF8);
        AppSettings.Reload();

        Assert.True(AppSettings.Current.SetupCompleted);
        Assert.NotNull(AppSettings.Indlaesningsfejl);
    }

    [Fact]
    public void Uden_nogen_filer_siges_der_ingenting()
    {
        // Foerste start. Standardvaerdier er det rigtige svar her, og saa skal
        // der ikke staa en fejl paa skaermen.
        AppSettings.Reload();

        Assert.False(AppSettings.Current.SetupCompleted);
        Assert.Null(AppSettings.Indlaesningsfejl);
    }

    [Fact]
    public void Er_alt_vaek_bliver_det_sagt()
    {
        // Ingen forrige udgave at falde tilbage paa. Saa ER indstillingerne
        // tabt - og saa skal det staa et sted, ikke opdages ved at
        // velkomstforloebet kommer igen.
        AppSettings.Current.SetupCompleted = true;
        AppSettings.Current.Save();

        File.WriteAllText(Fil, "ikke json", Encoding.UTF8);
        if (File.Exists(Kopi)) File.Delete(Kopi);

        AppSettings.Reload();

        Assert.False(AppSettings.Current.SetupCompleted);
        Assert.NotNull(AppSettings.Indlaesningsfejl);
        Assert.Contains("standardværdier", AppSettings.Indlaesningsfejl!);
    }
}
