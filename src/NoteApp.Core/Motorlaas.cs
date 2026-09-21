namespace NoteApp.Core;

/// <summary>
/// Kun én udskrift ad gangen på hele maskinen.
/// </summary>
/// <remarks>
/// ============ HVAD DER SKETE ============
///
/// Målt 21-09-2026 under og efter et opkald på 41 minutter: TRE whisper
/// kørte på én gang, hver med large-v3 indlæst — medskrivningen af hvert af
/// de to spor og den fulde udskrift bagefter. Grafikkortet (RTX 2060, 6 GB)
/// stod på 5,8 GB, 92 % og 92 °C, og der var 0,7 GB RAM fri af 32.
///
/// To large-v3 kan ikke være på kortet samtidig. Resten flyder over i
/// maskinens hukommelse, alt bliver mange gange langsommere, og maskinen
/// holder op med at svare. Medskrivningen nåede derfor kun fire af de 41
/// minutter under samtalen — den sidste bid var 36,9 minutter.
///
/// ============ LÅSEN ============
///
/// En navngiven mutex, så det også gælder på tværs af to kørende HeyPia —
/// den installerede og udviklingsudgaven. En mutex og ikke en semafor: går
/// appen ned med låsen, frigiver Windows en mutex (den bliver «forladt»), og
/// den næste får den. En semafor ville blive stående optaget for altid.
///
/// En mutex hører til den tråd, der tog den, og en udskrift springer mellem
/// tråde. Derfor holdes den af sin egen tråd, der tager den, venter på
/// besked og slipper den igen.
/// </remarks>
public static class Motorlaas
{
    /// <summary>Navnet på låsen. Local = én pr. brugersession.</summary>
    public const string Navn = @"Local\HeyPia-whisper";

    /// <summary>Venter på låsen. Den slippes, når det returnerede lukkes.</summary>
    /// <param name="venter">Kaldes én gang, hvis der skal ventes — til at sige det på skærmen.</param>
    public static Task<IDisposable> TagAsync(Action? venter = null, CancellationToken ct = default) =>
        TagAsync(Navn, venter, ct);

    /// <summary>Samme, med et andet navn. Til prøverne.</summary>
    public static Task<IDisposable> TagAsync(string navn, Action? venter, CancellationToken ct)
    {
        var fik = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
        var slip = new ManualResetEventSlim(false);

        var holder = new Thread(() =>
        {
            using var laas = new Mutex(false, navn);
            var har = false;

            try
            {
                // Et kort forsøg først: er den fri, skal der ikke siges noget.
                try { har = laas.WaitOne(0); }
                catch (AbandonedMutexException) { har = true; }

                if (!har) venter?.Invoke();

                while (!har)
                {
                    if (ct.IsCancellationRequested) { fik.TrySetCanceled(ct); return; }

                    try { har = laas.WaitOne(200); }
                    catch (AbandonedMutexException) { har = true; }   // den forrige gik ned med den
                }

                fik.TrySetResult(new Slip(slip));

                slip.Wait();
            }
            catch (Exception ex)
            {
                fik.TrySetException(ex);
            }
            finally
            {
                if (har) laas.ReleaseMutex();
                slip.Dispose();
            }
        })
        {
            IsBackground = true,
            Name = "HeyPia motorlås",
        };

        holder.Start();
        return fik.Task;
    }

    private sealed class Slip(ManualResetEventSlim signal) : IDisposable
    {
        private int _sluppet;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _sluppet, 1) == 0) signal.Set();
        }
    }
}
