using System.Text;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>
/// Afgør, hvornår et afsnit er læst færdigt, ud fra hvad der bliver hørt.
///
/// Den vigtige indsigt: appen kender teksten i forvejen. Den skal derfor ikke
/// forstå fri tale, men kun genkende, at slutningen af det aktuelle afsnit er
/// blevet sagt. Det er en langt lettere opgave, og den tåler en dårlig
/// live-transskription — der skal blot være nok distinkte ord tilbage til at
/// matche.
///
/// Whisper hallucinerer i stilhed: "Tak.", "[Tekstet af ...]", "Undertekster
/// af ...". Den slags matcher aldrig afsnittets sidste ord, og derfor filtreres
/// det væk af sig selv frem for af en liste over kendte hallucinationer.
/// </summary>
public sealed class ScriptFollower
{
    /// <summary>Hvor mange af afsnittets sidste ord der lyttes efter.</summary>
    private const int TailWords = 6;

    /// <summary>Hvor mange af dem der skal genkendes, i rækkefølge, før vi går videre.</summary>
    private const int RequiredHits = 3;

    /// <summary>
    /// Kortest mulige tid i et afsnit, før der må skiftes. Uden den kunne et
    /// ekko fra forrige afsnit sende os videre, før brugeren er begyndt.
    /// </summary>
    public static readonly TimeSpan MinimumTimeInParagraph = TimeSpan.FromSeconds(4);

    private static readonly Regex AnsiKoder = new(@"\x1B\[[0-9;]*[A-Za-z]|\[\d*[A-Z]", RegexOptions.Compiled);

    private readonly StringBuilder _hoert = new();
    private string[] _hale = Array.Empty<string>();
    private DateTimeOffset _afsnitStartet = DateTimeOffset.MinValue;

    /// <summary>Sættes når et nyt afsnit vises. Nulstiller det hørte.</summary>
    public void SetParagraph(string text)
    {
        var ord = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _hale = ord.Length <= TailWords ? ord : ord[^TailWords..];

        _hoert.Clear();
        _afsnitStartet = DateTimeOffset.Now;
    }

    /// <summary>
    /// Fodres med det, live-lytningen har hørt. Returnerer sand, når afsnittet
    /// vurderes færdigt.
    /// </summary>
    public bool Feed(string heard)
    {
        if (_hale.Length == 0) return false;

        var rent = Normalize(heard);
        if (rent.Length == 0) return false;

        _hoert.Append(' ').Append(rent);

        // Kun de sidste par saetninger er interessante. Uden loftet vokser
        // straengen gennem hele oplaesningen, og et ord fra minut to kunne
        // pludselig taelle med i minut ti.
        if (_hoert.Length > 400) _hoert.Remove(0, _hoert.Length - 400);

        if (DateTimeOffset.Now - _afsnitStartet < MinimumTimeInParagraph) return false;

        return TailHeard(_hoert.ToString());
    }

    /// <summary>
    /// Sand hvis mindst <see cref="RequiredHits"/> af halens ord optræder i
    /// rækkefølge i det hørte. Rækkefølgen er det, der gør det robust: et
    /// tilfældigt sammenfald af enkeltord sker hele tiden, tre i træk gør ikke.
    /// </summary>
    private bool TailHeard(string hoert)
    {
        var ord = hoert.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (ord.Length == 0) return false;

        var traf = 0;
        var i = 0;

        foreach (var forventet in _hale)
        {
            // Find naeste forekomst fra hvor vi slap — det er dét, der goer
            // matchet til en raekkefoelge og ikke bare en maengde.
            var fundet = -1;
            for (var j = i; j < ord.Length; j++)
            {
                if (Ligner(ord[j], forventet)) { fundet = j; break; }
            }

            if (fundet >= 0)
            {
                traf++;
                i = fundet + 1;
                if (traf >= RequiredHits) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// To ord regnes som ens, hvis de er identiske, eller hvis det ene er en
    /// forkortet/forlænget udgave af det andet. Live-transskription på dansk
    /// rammer tit stammen rigtigt og endelsen forkert.
    /// </summary>
    private static bool Ligner(string a, string b)
    {
        if (a == b) return true;
        if (a.Length < 4 || b.Length < 4) return false;

        var kortest = Math.Min(a.Length, b.Length);
        var praefiks = Math.Max(4, kortest - 2);

        return a.Length >= praefiks && b.Length >= praefiks &&
               a.AsSpan(0, praefiks).SequenceEqual(b.AsSpan(0, praefiks));
    }

    /// <summary>
    /// Renser tekst til sammenligning: ANSI-koder fra terminaludgangen væk,
    /// små bogstaver, ingen tegnsætning, ét mellemrum mellem ord.
    /// </summary>
    public static string Normalize(string text)
    {
        var uden = AnsiKoder.Replace(text, " ");

        var sb = new StringBuilder(uden.Length);
        foreach (var c in uden.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else sb.Append(' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
