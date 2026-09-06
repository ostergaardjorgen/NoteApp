using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Lydeksemplerne i demoen — og selve klippet, de laves med.
/// </summary>
/// <remarks>
/// ============ LYDEN OG TEKSTEN SKAL FØLGES AD ============
///
/// Et klip, der siger ét, under en udskrift, der siger noget andet, er ikke et
/// eksempel. Det er en fejl, man ikke kan se er en fejl — og den slags må ikke
/// stå i det, appen viser frem.
///
/// Prøverne bygger deres egen «brugerdatamappe» med et opdigtet webinar i, så
/// de aldrig læser i noget rigtigt.
/// </remarks>
public class Lydeksempeltest
{
    /// <summary>Sekunder wav, 16 kHz mono 16 bit, med en tone i.</summary>
    private static void SkrivWav(string sti, double sekunder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sti)!);

        const int frekvens = 16_000;
        var proever = (int)(frekvens * sekunder);
        var data = proever * 2;

        using var f = new FileStream(sti, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(f);

        w.Write("RIFF"u8.ToArray());
        w.Write(36 + data);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);                     // fmt-blokkens laengde
        w.Write((short)1);               // PCM
        w.Write((short)1);               // mono
        w.Write(frekvens);
        w.Write(frekvens * 2);           // bytes pr. sekund
        w.Write((short)2);               // blokjustering
        w.Write((short)16);              // bit pr. proeve
        w.Write("data"u8.ToArray());
        w.Write(data);

        // En tone, saa filen ikke er tavs. Proeverne maaler laengder, men en
        // tavs fil ville skjule, hvis der blev kopieret nuller.
        for (var i = 0; i < proever; i++)
            w.Write((short)(8000 * Math.Sin(i * 2 * Math.PI * 440 / frekvens)));
    }

    [Fact]
    public void Et_klip_faar_den_laengde_der_blev_bedt_om()
    {
        using var p = new Proevemappe();

        var kilde = Path.Combine(p.Sti, "hel.wav");
        var maal = Path.Combine(p.Sti, "klip.wav");

        SkrivWav(kilde, 30);

        var sek = Lyduddrag.Klip(kilde, maal, fra: 10, sekunder: 5);

        Assert.Equal(5, sek, 1);

        // OG FILEN SKAL SIGE DET SAMME. En wav med en forkert header spiller
        // stadig i de fleste programmer, men laengden er forkert - og en
        // optagelse, der siger 40 minutter og varer 5 sekunder, er vaerre end
        // en, der ikke spiller.
        Assert.Equal(5, Transcriber.WavSeconds(maal), 1);

        // 5 sekunder ved 32.000 bytes/sek plus en header paa 44.
        Assert.Equal(5 * 32_000 + 44, new FileInfo(maal).Length);
    }

    [Fact]
    public void Der_klippes_ikke_ud_over_filens_ende()
    {
        using var p = new Proevemappe();

        var kilde = Path.Combine(p.Sti, "hel.wav");
        var maal = Path.Combine(p.Sti, "klip.wav");

        SkrivWav(kilde, 8);

        // Der bedes om 75 sekunder fra sekund 6. Der er 2 tilbage.
        Assert.Equal(2, Lyduddrag.Klip(kilde, maal, fra: 6, sekunder: 75), 1);
    }

    [Fact]
    public void En_fil_der_ikke_findes_giver_nul_og_ingen_fil()
    {
        using var p = new Proevemappe();

        var maal = Path.Combine(p.Sti, "klip.wav");

        Assert.Equal(0, Lyduddrag.Klip(Path.Combine(p.Sti, "findes-ikke.wav"), maal, 0, 10));
        Assert.False(File.Exists(maal));
    }

    // ================================================================= demoen

    /// <summary>En datamappe med ét opdigtet webinar i — lyd og udskrift.</summary>
    private static string Egendata(string rod, double sekunder = 300)
    {
        var mappe = Path.Combine(rod, "Optagelser", "2026-01-01_10-00_Proevewebinar");
        Directory.CreateDirectory(mappe);

        SkrivWav(Path.Combine(mappe, "loopback.wav"), sekunder);

        MeetingStore.Save(mappe, new MeetingMetadata
        {
            StartedAt = DateTimeOffset.Now.AddDays(-40),
            DurationSeconds = sekunder,
            Title = "Prøvewebinar om adgangsstyring",
            Type = MeetingType.Webinar,
            Mappe = "Webinarer",
            Language = "da",
        });

        var u = new Udskrift();

        // Een linje i sekundet. Saa er det til at regne efter, hvilke linjer
        // der hoerer til et klip.
        for (var i = 0; i < (int)sekunder; i++)
        {
            u.Linjer.Add(new Udskriftslinje
            {
                FraMs = i * 1000,
                TilMs = i * 1000 + 900,
                Spor = Samtale.Derfra,
                Tekst = $"Linje nummer {i} om adgangsstyring.",
            });
        }

        u.GemMaskin(mappe, "large-v3");
        return mappe;
    }

    [Fact]
    public void Demoen_klipper_et_eksempel_ud_af_et_webinar()
    {
        using var p = new Proevemappe();

        var egne = Path.Combine(p.Sti, "egne");
        Egendata(egne);

        var demo = Path.Combine(p.Sti, "demo");
        Directory.CreateDirectory(demo);

        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, demo);
        UserDataPaths.Glem();

        try
        {
            Demodata.Byg(lydfra: egne);

            var uddrag = MeetingStore.Alle()
                .Where(m => (m.Title ?? "").Contains("uddrag"))
                .ToList();

            var m = Assert.Single(uddrag);

            Assert.Contains("Prøvewebinar", m.Title);
            Assert.Equal(MeetingType.Webinar, m.Type);

            // Klippet skal vaere kort. Hele demoen skal kunne ligge under ti
            // megabyte, og een lang optagelse alene ville sprænge det.
            Assert.InRange(m.DurationSeconds, 60, 90);

            var mappe = MeetingStore.FindById(m.Id.ToString())!.Value.Mappe;
            var wav = Path.Combine(mappe, "loopback.wav");

            Assert.True(File.Exists(wav), "klippet blev ikke skrevet");
            Assert.Equal(m.DurationSeconds, Transcriber.WavSeconds(wav), 1);

            // ============ OG TEKSTEN SKAL PASSE TIL LYDEN ============
            //
            // Udskriften begynder ved nul og daekker praecis det stykke, der
            // blev klippet. Staar der linjer med tider ud over klippets
            // laengde, hoerer de til noget, man ikke kan hoere.
            var u = Udskrift.HentEllerByg(mappe, "large-v3")!;

            Assert.NotEmpty(u.Linjer);
            Assert.Equal(0, u.Linjer[0].FraMs);
            Assert.True(u.Linjer[^1].FraMs <= m.DurationSeconds * 1000,
                $"sidste linje ligger ved {u.Linjer[^1].FraMs} ms i et klip paa "
                + $"{m.DurationSeconds} sekunder");
        }
        finally
        {
            Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, p.Sti);
            UserDataPaths.Glem();
        }
    }

    [Fact]
    public void Uden_webinarer_bygges_demoen_alligevel()
    {
        using var p = new Proevemappe();

        var tom = Path.Combine(p.Sti, "ingen-data");
        Directory.CreateDirectory(tom);

        // PAA EN FRISK INSTALLATION ER DER INGENTING AT KLIPPE AF. Det er
        // ikke en fejl - demoen har bare ingen lyd, og alt det oevrige
        // virker.
        Assert.True(Demodata.Byg(lydfra: tom));

        Assert.True(MeetingStore.Alle().Count() >= 5);
        Assert.DoesNotContain(MeetingStore.Alle(), m => (m.Title ?? "").Contains("uddrag"));
    }
}
