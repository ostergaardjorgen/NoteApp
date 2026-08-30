using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, at der kun bliver læst ÉT sæt indstillinger — også når flere
/// tråde spørger på én gang.
///
/// DET VAR HER, VALGENE FORSVANDT.
///
/// «_current ??= Load()» er ikke sikker, når flere tråde rammer den samtidig.
/// Ved opstart gør de netop det: skærmen, mødevagten og notifikationerne
/// starter i samme øjeblik. Alle ser null, alle kalder Load, og alle får HVER
/// SIT objekt. Det sidst tildelte er det, alle andre får bagefter — men de
/// første er allerede delt ud og lever videre.
///
/// Så skrev brugeren sin mikrofon i det ene, og et af de andre gemte sit eget
/// oven i lidt senere. Valget var væk, uden at nogen havde rørt noget.
///
/// Målt 30-08-2026 med sporet: TRE indlæsninger i den samme proces inden for
/// 25 millisekunder.
/// </summary>
public class IndstillingskaploebTest : IDisposable
{
    private readonly string _mappe;
    private readonly string? _foer;

    public IndstillingskaploebTest()
    {
        _mappe = Path.Combine(Path.GetTempPath(), "heypia-kap-" + Guid.NewGuid().ToString("N")[..8]);
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
    public void Mange_traade_paa_een_gang_faar_det_samme_objekt()
    {
        // Praecis situationen ved opstart. Faar to traade hvert sit objekt,
        // skriver den ene den andens valg vaek senere.
        AppSettings.Reload();

        var set = new ConcurrentBag<AppSettings>();

        Parallel.For(0, 64, _ => set.Add(AppSettings.Current));

        var foerste = AppSettings.Current;
        Assert.All(set, s => Assert.Same(foerste, s));
    }

    [Fact]
    public void Et_valg_skrevet_af_een_traad_ses_af_alle()
    {
        AppSettings.Reload();

        Parallel.For(0, 32, i =>
        {
            var s = AppSettings.Current;
            if (i == 0) s.MicrophoneId = "jabra";
        });

        Assert.Equal("jabra", AppSettings.Current.MicrophoneId);
    }
}
