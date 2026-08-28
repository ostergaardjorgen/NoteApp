using System.IO;
using System.Windows;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop;

/// <summary>
/// Binder holdet på genvejstasten sammen med dikteringen.
///
/// HOLD → OPTAG → SKRIV UD → PUDS AF → UDKLIPSHOLDEREN
///
/// Teksten lander i udklipsholderen, og det er med vilje i denne omgang. At
/// lægge den ind, hvor markøren står i et hvilket som helst andet program,
/// kræver, at appen skriver tastetryk ind i et fremmed vindue — det er en
/// opgave for sig, og den skal ikke laves halvt. Se etape 2 i
/// doc/diktering.md.
///
/// Indtil da: hold, tal, slip, og sæt ind hvor du var i gang.
/// </summary>
public sealed class Dikteringsvagt : IDisposable
{
    private ShortClipRecorder? _optager;
    private string? _klip;
    private bool _arbejder;

    /// <summary>Siger til undervejs, så bjælken kan vise, hvad der sker.</summary>
    public event Action<string>? Melder;

    /// <summary>Sandt, mens der optages eller skrives ud.</summary>
    public bool Igang => _optager is not null || _arbejder;

    /// <summary>Tasten er holdt nede længe nok. Begynd at lytte.</summary>
    public void Begynd()
    {
        // ============ ET DIKTAT MAA IKKE LIGGE OVEN I ET ANDET ============
        //
        // Slippet kan bliver spist af et andet program, og saa kommer der et
        // nyt hold, foer det foerste er afsluttet. Uden den her ville to
        // optagere skrive i den samme fil.
        if (Igang) return;

        var noegle = SkyNoegle.Hent();
        if (noegle is null)
        {
            Melder?.Invoke(Sprog.T("settingsview.diktering_kraever_noegle"));
            return;
        }

        try
        {
            _klip = Path.Combine(Path.GetTempPath(),
                                 "heypia-diktat-" + Guid.NewGuid().ToString("N")[..8] + ".wav");

            _optager = new ShortClipRecorder(_klip);
            _optager.Start(AppSettings.Current.MicrophoneId);

            Melder?.Invoke(Sprog.T("diktering.lytter"));
        }
        catch (Exception ex)
        {
            Ryd();
            Melder?.Invoke(Sprog.T("diktering.gik_galt", ex.Message));
        }
    }

    /// <summary>
    /// Tasten er sluppet — eller loftet blev nået.
    /// </summary>
    /// <param name="afbrudt">
    /// Sandt, når appen stoppede af sig selv. Så er man midt i en sætning og
    /// skal have det at vide; ellers taler man videre til noget, der er holdt
    /// op med at lytte.
    /// </param>
    public async Task SlutAsync(bool afbrudt)
    {
        if (_optager is null) return;

        var optager = _optager;
        var klip = _klip!;
        _optager = null;
        _arbejder = true;

        try
        {
            var sekunder = optager.Stop();
            optager.Dispose();

            // Under et halvt sekund er et fejltryk, ikke et diktat. At sende
            // det ville koste et kald og give en tom streng tilbage.
            if (sekunder < 0.5)
            {
                Melder?.Invoke(Sprog.T("diktering.for_kort"));
                return;
            }

            Melder?.Invoke(Sprog.T("diktering.skriver_ud"));

            var noegle = SkyNoegle.Hent();
            if (noegle is null)
            {
                Melder?.Invoke(Sprog.T("settingsview.diktering_kraever_noegle"));
                return;
            }

            var v = AppSettings.Current;
            var klient = new Dikteringsklient(noegle);

            var raa = await klient.SkrivUdAsync(klip, v.DikteringFagord ? Fagord() : null);

            if (raa.Raa.Length == 0)
            {
                Melder?.Invoke(Sprog.T("diktering.intet_hoert"));
                return;
            }

            var tekst = raa.Raa;

            if (v.DikteringPuds)
            {
                Melder?.Invoke(Sprog.T("diktering.rydder_op"));
                tekst = await klient.PudsAsync(raa.Raa, Formaal());
            }

            Læg(tekst);

            // BESKEDEN OM AFBRYDELSEN KOMMER TIL SIDST, efter teksten er i hus.
            // Kom den foerst, ville man tro, at det, man havde sagt, var tabt.
            Melder?.Invoke(afbrudt
                ? Sprog.T("settingsview.diktering_afbrudt")
                : Sprog.T("diktering.klar", tekst.Length));

            Historik.Skriv(HaendelseType.Transskription,
                Sprog.T("diktering.historik", (int)raa.Sekunder),
                tekst);
        }
        catch (Exception ex)
        {
            Melder?.Invoke(Sprog.T("diktering.gik_galt", ex.Message));
        }
        finally
        {
            _arbejder = false;
            Slet(klip);
        }
    }

    /// <summary>
    /// Lægger teksten i udklipsholderen.
    /// </summary>
    /// <remarks>
    /// UDKLIPSHOLDEREN KAN VÆRE OPTAGET. Et andet program kan holde den et
    /// øjeblik, og så fejler det første forsøg. Der prøves et par gange —
    /// ellers ville et diktat, der lykkedes hele vejen, gå tabt på det sidste
    /// skridt.
    /// </remarks>
    private static void Læg(string tekst)
    {
        for (var forsøg = 0; forsøg < 5; forsøg++)
        {
            try
            {
                Clipboard.SetText(tekst);
                return;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                System.Threading.Thread.Sleep(60);
            }
        }
    }

    private static Dikteringsformaal Formaal() =>
        Enum.TryParse<Dikteringsformaal>(AppSettings.Current.DikteringFormaal,
                                         ignoreCase: true, out var f)
            ? f
            : Dikteringsformaal.Note;

    /// <summary>Ordlisten, appen har lært. Højst 200, så anmodningen ikke svulmer.</summary>
    private static string[] Fagord()
    {
        try
        {
            return File.Exists(UserDataPaths.Vocabulary)
                ? File.ReadAllLines(UserDataPaths.Vocabulary)
                      .Select(l => l.Trim())
                      .Where(l => l.Length > 0 && !l.StartsWith('#'))
                      .Take(200)
                      .ToArray()
                : Array.Empty<string>();
        }
        catch (IOException)
        {
            // Uden ordlisten bliver udskriften en anelse ringere. Det er alt.
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Klippet er din stemme. Det slettes, så snart teksten er i hus.
    /// </summary>
    private static void Slet(string sti)
    {
        try { if (File.Exists(sti)) File.Delete(sti); }
        catch (IOException) { /* en laast fil rydder Windows selv op i temp. */ }
    }

    private void Ryd()
    {
        try { _optager?.Dispose(); } catch (Exception) { /* var aldrig startet. */ }
        _optager = null;

        if (_klip is not null) Slet(_klip);
        _klip = null;
    }

    public void Dispose() => Ryd();
}
