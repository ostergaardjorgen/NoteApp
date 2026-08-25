namespace NoteApp.Core;

/// <summary>Hvad en hentning endte med. <c>Antal</c> er, hvor mange der kom ind.</summary>
public sealed record Synksvar(bool Lykkedes, int Antal, string Besked);

/// <summary>
/// Hentningen af aftaler og opgaver fra de forbundne tjenester.
///
/// HVORFOR DEN LIGGER FOR SIG
///
/// Den lå inde i Indstillinger, og det var også det eneste sted, den kunne
/// startes fra. Det gjorde funktionen usynlig: en aftale, der blev oprettet i
/// Google, dukkede ikke op i Cockpittet, og den eneste vej frem var at gå to
/// skærme væk og trykke på en knap, man skulle vide fandtes.
///
/// Nu kan både Cockpittet og Indstillinger kalde det samme. Ét sted, der
/// henter, betyder også, at bogføringen — hvornår sidst, hvor mange, hvad gik
/// galt — er den samme, uanset hvor man trykkede.
///
/// DER HENTES KUN NED. Der sendes intet op af sig selv; en aftale, du laver
/// her, kommer kun i Google, hvis du sætter hak ved det. Se Integrationer.
/// </summary>
public static class Synkronisering
{
    /// <summary>Er der en kalender at hente fra?</summary>
    public static bool Kalenderklar => Kalenderintegrationer().Count > 0;

    /// <summary>Er opgaverne forbundet?</summary>
    public static bool Opgaverklar
    {
        get
        {
            try { return Integrationsfiler.Hent(Googleopgaver.Id).ErForbundet; }
            catch (Exception) { return false; }
        }
    }

    /// <summary>De kalenderintegrationer, der faktisk er forbundet.</summary>
    private static List<Integration> Kalenderintegrationer()
    {
        var klar = new List<Integration>();

        foreach (var i in Integrationer.Alle.Where(i => i.Klar && i.Id != Googleopgaver.Id))
        {
            try { if (Integrationsfiler.Hent(i.Id).ErForbundet) klar.Add(i); }
            catch (Exception) { }
        }

        return klar;
    }

    /// <summary>
    /// Henter aftalerne fra alle forbundne kalendere.
    ///
    /// FEJLER ÉN, FORTSÆTTER DE ANDRE. To kalendere er ikke to halvdele af
    /// det samme; at lade den ene falde, fordi den anden ikke svarede, ville
    /// skjule aftaler, der var lige ved hånden.
    /// </summary>
    public static async Task<Synksvar> Kalenderen(CancellationToken ct = default)
    {
        var klar = Kalenderintegrationer();

        if (klar.Count == 0)
            return new Synksvar(false, 0, Sprog.T("synk.ingenkalender"));

        var i_alt = 0;
        var fejl = new List<string>();

        foreach (var i in klar)
        {
            var o = Integrationsfiler.Hent(i.Id);

            try
            {
                var aftaler = await Googlekalender.HentAsync(o.Opdateringsnoegle, ct: ct);
                var n = Kalender.Afloes(i.Kilde, aftaler);

                i_alt += n;

                o.SidstHentet = DateTimeOffset.Now;
                o.SidsteAntal = n;
                o.SidsteFejl = "";
            }
            catch (Exception ex)
            {
                o.SidsteFejl = ex.Message;
                fejl.Add($"{i.Navn}: {Kort(ex.Message)}");
            }

            Integrationsfiler.Gem(i.Id, o);
        }

        if (fejl.Count == klar.Count)
            return new Synksvar(false, 0, string.Join("  ·  ", fejl));

        var besked = i_alt == 1 ? Sprog.T("synk.enaftalehentet") : Sprog.T("synk.aftalerhentet", i_alt);
        if (fejl.Count > 0) besked += "  ·  " + string.Join("  ·  ", fejl);

        return new Synksvar(true, i_alt, besked);
    }

    /// <summary>Henter opgaverne fra Google Tasks.</summary>
    public static async Task<Synksvar> Opgaverne(CancellationToken ct = default)
    {
        var o = Integrationsfiler.Hent(Googleopgaver.Id);

        if (!o.ErForbundet)
            return new Synksvar(false, 0, Sprog.T("synk.ingenopgaver"));

        try
        {
            var hentede = await Googleopgaver.HentAsync(o.Opdateringsnoegle, ct);
            var n = Opgavelager.Afloes(Opgavekilde.Google, hentede);

            o.SidstHentet = DateTimeOffset.Now;
            o.SidsteAntal = n;
            o.SidsteFejl = "";
            Integrationsfiler.Gem(Googleopgaver.Id, o);

            return new Synksvar(true, n, n == 1 ? Sprog.T("synk.enopgavehentet") : Sprog.T("synk.opgaverhentet", n));
        }
        catch (Exception ex)
        {
            o.SidsteFejl = ex.Message;
            Integrationsfiler.Gem(Googleopgaver.Id, o);

            return new Synksvar(false, 0, Kort(ex.Message));
        }
    }

    /// <summary>
    /// Hvornår der sidst blev hentet. Null betyder «aldrig».
    ///
    /// Bruges til linjen under ikonet. «Sidst hentet» er den oplysning, der
    /// afgør, om man skal trykke — uden den er et synkroniseringsikon bare en
    /// knap, man trykker på for en sikkerheds skyld.
    /// </summary>
    public static DateTimeOffset? SidstKalender() =>
        Kalenderintegrationer()
            .Select(i => Integrationsfiler.Hent(i.Id).SidstHentet)
            .Where(d => d is not null)
            .DefaultIfEmpty(null)
            .Max();

    public static DateTimeOffset? SidstOpgaver()
    {
        try { return Integrationsfiler.Hent(Googleopgaver.Id).SidstHentet; }
        catch (Exception) { return null; }
    }

    /// <summary>«for et øjeblik siden», «for 12 min. siden», «i går kl. 14:30».</summary>
    public static string Siden(DateTimeOffset? hvornaar)
    {
        if (hvornaar is not { } d) return Sprog.T("synk.aldrig");

        var gaaet = DateTimeOffset.Now - d;

        if (gaaet < TimeSpan.FromMinutes(1)) return Sprog.T("synk.ligenu");
        if (gaaet < TimeSpan.FromMinutes(60)) return Sprog.T("synk.minutter", (int)gaaet.TotalMinutes);

        if (d.LocalDateTime.Date == DateTime.Today)
            return Sprog.T("synk.idag", d.LocalDateTime.ToString("HH:mm"));

        if (d.LocalDateTime.Date == DateTime.Today.AddDays(-1))
            return Sprog.T("synk.igaar", d.LocalDateTime.ToString("HH:mm"));

        return Sprog.T("synk.dato", d.LocalDateTime.ToString("dd-MM"), d.LocalDateTime.ToString("HH:mm"));
    }

    private static string Kort(string besked) =>
        besked.Length <= 90 ? besked : besked[..90].TrimEnd() + "…";
}
