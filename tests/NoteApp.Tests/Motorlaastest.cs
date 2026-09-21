using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Kun én udskrift ad gangen. Se Motorlaas.
/// </summary>
public class Motorlaastest
{
    private static string Navn() => @"Local\HeyPia-proeve-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Den_anden_venter_til_den_foerste_slipper()
    {
        var navn = Navn();
        var ventede = false;

        var foerste = await Motorlaas.TagAsync(navn, null, default);

        var anden = Motorlaas.TagAsync(navn, () => ventede = true, default);

        await Task.Delay(400);
        Assert.False(anden.IsCompleted);          // den foerste har den stadig
        Assert.True(ventede);                     // og den anden har sagt, at den venter

        foerste.Dispose();

        using var fik = await anden.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task En_fri_laas_siger_ikke_at_den_venter()
    {
        var ventede = false;

        using var l = await Motorlaas.TagAsync(Navn(), () => ventede = true, default);

        Assert.False(ventede);
    }

    [Fact]
    public async Task Den_der_venter_kan_afbrydes()
    {
        var navn = Navn();
        using var foerste = await Motorlaas.TagAsync(navn, null, default);

        using var cts = new CancellationTokenSource(300);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Motorlaas.TagAsync(navn, null, cts.Token));
    }

    [Fact]
    public async Task En_laas_der_blev_forladt_kan_tages()
    {
        // Appen gik ned med laasen: traaden, der holdt den, er vaek uden at
        // slippe den. Windows goer den forladt, og den naeste faar den.
        var navn = Navn();

        var t = new Thread(() => new Mutex(false, navn).WaitOne());
        t.Start();
        t.Join();

        using var l = await Motorlaas.TagAsync(navn, null, default).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task At_slippe_to_gange_goer_ingen_skade()
    {
        var navn = Navn();

        var l = await Motorlaas.TagAsync(navn, null, default);
        l.Dispose();
        l.Dispose();

        using var igen = await Motorlaas.TagAsync(navn, null, default).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
