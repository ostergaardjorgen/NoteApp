using System.Text.Json;

namespace NoteApp.Core.Deling;

/// <summary>
/// Aftaler og opgaver, der rejser mellem to computere.
/// </summary>
/// <remarks>
/// ============ DEN LIGGER MELLEM DE TO ============
///
/// <see cref="Journal"/> bærer poster og ved ikke, hvad de betyder. Kalenderen
/// og opgavelisten ved ikke, at der findes en anden computer. Her mødes de:
/// hvad der skal skrives ned, når noget ændres, og hvad der skal ske, når der
/// kommer noget ind.
///
/// ============ KUN DET, DER ER VORES EGET ============
///
/// Aftaler og opgaver fra Google rejser IKKE. Begge maskiner henter dem fra
/// det samme sted, og en aftale, der kom to veje, ville blive til to aftaler —
/// eller til en, der blev skrevet frem og tilbage, hver gang den ene hentede.
///
/// ============ DEN NYESTE VINDER ============
///
/// Er den samme aftale rettet begge steder, før de har talt sammen, afgør
/// tidsstemplet det. Det står på posten selv. En aftale uden tidsstempel er
/// fra før feltet fandtes; den taber, og det er den rigtige vej: det, der er
/// skrevet af et menneske for nylig, skal ikke vige for noget gammelt.
/// </remarks>
public static class Delingsjournal
{
    private static readonly JsonSerializerOptions Format = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public const string Aftaleslags = "aftale";
    public const string Opgaveslags = "opgave";

    // ==================================================================== skriv

    /// <summary>Skriver en aftale til de andre computere.</summary>
    public static void Gemt(Aftale a)
    {
        if (a.Kilde != Kalenderkilde.Lokal) return;

        Journal.Skriv(Aftaleslags, a.Id, JsonSerializer.Serialize(a, Format), slettet: false);
    }

    /// <summary>Skriver en opgave til de andre computere.</summary>
    public static void Gemt(Opgave o)
    {
        if (o.Herkomst != Opgavekilde.Lokal) return;

        Journal.Skriv(Opgaveslags, o.Id.ToString(), JsonSerializer.Serialize(o, Format), slettet: false);
    }

    /// <summary>Gravstenen. Uden den dukker det slettede op igen.</summary>
    public static void Slettet(string slags, string id) => Journal.Skriv(slags, id, null, slettet: true);

    // ====================================================================== hent

    /// <summary>
    /// Lægger de andre computeres ændringer ind. Returnerer hvor mange.
    /// </summary>
    public static int Hent()
    {
        var nyt = Journal.Nyt();

        if (nyt.Count == 0) return 0;

        var antal = 0;

        foreach (var post in nyt)
        {
            try
            {
                if (post.Slags == Aftaleslags) antal += Aftale(post) ? 1 : 0;
                else if (post.Slags == Opgaveslags) antal += Opgave(post) ? 1 : 0;
            }
            catch (Exception)
            {
                // En post, vi ikke forstaar. Den springes over - resten skal
                // ind. En enkelt oedelagt linje maa ikke standse alt.
            }
        }

        return antal;
    }

    private static bool Aftale(Journal.Aendring post)
    {
        var alle = Kalender.Alle();
        var findes = alle.FirstOrDefault(a => a.Id == post.Id);

        if (post.Slettet)
        {
            if (findes is null) return false;

            Journal.Anvend(() => Kalender.Slet(post.Id));
            return true;
        }

        var kommer = JsonSerializer.Deserialize<Aftale>(post.Data);

        if (kommer is null) return false;

        // ============ DEN NYESTE VINDER ============
        if (findes is not null && Nyere(findes.Aendret, kommer.Aendret)) return false;

        Journal.Anvend(() => Kalender.Gem(kommer));
        return true;
    }

    private static bool Opgave(Journal.Aendring post)
    {
        if (!Guid.TryParse(post.Id, out var id)) return false;

        var alle = Opgavelager.Alle();
        var findes = alle.FirstOrDefault(o => o.Id == id);

        if (post.Slettet)
        {
            if (findes is null) return false;

            Journal.Anvend(() => Opgavelager.Slet(id));
            return true;
        }

        var kommer = JsonSerializer.Deserialize<Opgave>(post.Data);

        if (kommer is null) return false;

        if (findes is not null && Nyere(findes.Aendret, kommer.Aendret)) return false;

        Journal.Anvend(() => Opgavelager.Gem(kommer));
        return true;
    }

    /// <summary>Er vores udgave nyere end den, der kommer?</summary>
    /// <remarks>
    /// EN POST UDEN TIDSSTEMPEL ER FRA FOER FELTET FANDTES. Den taber, og det
    /// er den rigtige vej: det, et menneske har skrevet for nylig, skal ikke
    /// vige for noget, ingen kan datere.
    /// </remarks>
    private static bool Nyere(DateTimeOffset? vores, DateTimeOffset? kommer)
    {
        if (vores is null) return false;
        if (kommer is null) return true;

        return vores > kommer;
    }
}
