using NoteApp.Core;
using NoteApp.Core.Deling;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Demodata må ikke rejse til den anden computer.
/// </summary>
/// <remarks>
/// ============ DEN HER PRØVE ER SKREVET EFTER SKADEN ============
///
/// 07-09-2026 stod der 756 demoaftaler i brugerens rigtige kalender på den
/// bærbare. «Bakkegården — opfølgning på tilbud», «Ugentlig planlægning» og
/// fire andre, 126 gange hver — én gang for hver prøvekørsel siden journalen
/// blev bygget.
///
/// Fælden er, at <see cref="UserDataPaths.Root"/> og
/// <see cref="UserDataPaths.Maskinrod"/> peger hver sin vej i demotilstand.
/// Root følger det datasæt, der VISES; maskinroden følger mennesket — dens
/// nøgle, dens parring, dens fælles mappe. Alt, der læser det ene og skriver
/// til det andet, sender demodata af sted underskrevet med den rigtige
/// maskines nøgle, og modtageren har ingen måde at se forskel på.
///
/// Det var ikke til at opdage her hjemme: den maskine, der SENDER, lægger
/// aldrig noget i sin egen kalender. Det viste sig først på den anden
/// computer.
/// </remarks>
public class Demolaektest
{
    private static (Proevemaskine A, Proevemaskine B, string Delt) Toparrede()
    {
        var delt = Proevemaskine.Nydeltmappe();
        Delt.Klargoer(delt);

        var a = new Proevemaskine("Stationær", delt);
        var b = new Proevemaskine("Bærbar", delt, Maskinrolle.Sekundaer);

        Proevemaskine.Godkend(a, b);

        return (a, b, delt);
    }

    /// <summary>Sådan ser demotilstanden ud: Root peger væk, maskinroden bliver.</summary>
    private sealed class Demoen : IDisposable
    {
        private readonly string? _foer;

        public Demoen()
        {
            Sti = Path.Combine(Path.GetTempPath(), "heypia-demo", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Sti);

            _foer = Environment.GetEnvironmentVariable(Demotilstand.RodVariabel);

            Environment.SetEnvironmentVariable(Demotilstand.RodVariabel, Sti);
            Demotilstand.Taend();
            UserDataPaths.Glem();
        }

        public string Sti { get; }

        public void Dispose()
        {
            try { Demotilstand.Sluk(); } catch (Exception) { }

            Environment.SetEnvironmentVariable(Demotilstand.RodVariabel, _foer);
            UserDataPaths.Glem();

            try { if (Directory.Exists(Sti)) Directory.Delete(Sti, recursive: true); }
            catch (IOException) { }
        }
    }

    private static Aftale Nyaftale(string titel) => new()
    {
        Titel = titel,
        Start = DateTimeOffset.Now.AddDays(1),
        Slut = DateTimeOffset.Now.AddDays(1).AddHours(1),
    };

    // ==================================================================== ud af huset

    [Fact]
    public void En_demoaftale_kommer_ikke_over_paa_den_anden_computer()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();

        using (new Demoen())
        {
            // Praecis det, Demodata.Aftalerne goer.
            Kalender.Gem(Nyaftale("Bakkegården — opfølgning på tilbud"));
        }

        // ============ INTET MAA VAERE LAGT I JOURNALEN ============
        //
        // Ikke «det bliver filtreret fra i den anden ende» - der maa ikke
        // vaere skrevet en linje. Modtageren kan ikke se forskel: posten er
        // underskrevet med den rigtige maskines noegle.
        var journal = Path.Combine(delt, "journal");

        Assert.False(Directory.Exists(journal) && Directory.EnumerateFiles(
            journal, "*.jsonl", SearchOption.AllDirectories).Any());

        b.Tag();

        Assert.Equal(0, Delingsjournal.Hent());
        Assert.Empty(Kalender.Alle());
    }

    [Fact]
    public void En_rigtig_aftale_rejser_stadig()
    {
        // Vaernet maa ikke slaa journalen fra i almindelighed. Uden den her
        // ville «ingenting rejser» bestaa den foerste proeve.
        var (a, b, _) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Kalender.Gem(Nyaftale("Et rigtigt møde"));

        b.Tag();

        Assert.Equal(1, Delingsjournal.Hent());
        Assert.Equal("Et rigtigt møde", Kalender.Alle().Single().Titel);
    }

    // ==================================================================== ind i huset

    [Fact]
    public void Demoen_rykker_ikke_brugerens_laeseposition()
    {
        var (a, b, _) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();
        Kalender.Gem(Nyaftale("Fra den stationære"));

        b.Tag();

        // Demoen koerer og ser efter nyt. Laesepositionen ligger i BRUGERENS
        // maskinmappe, ogsaa nu.
        using (new Demoen())
        {
            Assert.Equal(0, Delingsjournal.Hent());
        }

        // ============ AFTALEN SKAL STADIG VAERE DER ============
        //
        // Havde demoen faaet lov at laese, ville positionen staa paa 1, og den
        // rigtige app ville springe aftalen over FOR ALTID. En aftale, der
        // aldrig kom frem, og ingen fejl at lede efter.
        Assert.Equal(1, Delingsjournal.Hent());
        Assert.Equal("Fra den stationære", Kalender.Alle().Single().Titel);
    }

    // ==================================================================== arkivet

    [Fact]
    public void Demoens_optagelser_arkiveres_ikke()
    {
        var (a, b, delt) = Toparrede();

        using var _a = a;
        using var _b = b;

        a.Tag();

        using (new Demoen())
        {
            var mappe = MeetingStore.CreateSessionDirectory("Demomøde", DateTimeOffset.Now);

            MeetingStore.Save(mappe, new MeetingMetadata
            {
                Title = "Demomøde",
                StartedAt = DateTimeOffset.Now.AddMinutes(-10),
                EndedAt = DateTimeOffset.Now,
            });

            File.WriteAllText(Path.Combine(mappe, "udskrift_large-v3.txt"), "Demotekst.");

            // Arkivet ligger under den RIGTIGE maskines id. Uden vaernet ville
            // demoens moeder blive lagt op som brugerens egne.
            Assert.False(Arkiv.Gem(medLyd: true).Noget);
        }

        Assert.False(Directory.Exists(Arkiv.Rod(delt)));
    }
}
