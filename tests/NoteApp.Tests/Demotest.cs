using NoteApp.Core;
using NoteApp.Core.Documents;
using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Demotilstanden: et andet datasæt, ikke en anden app.
/// </summary>
/// <remarks>
/// ============ DET, PRØVERNE HER PASSER PÅ ============
///
/// At brugerens egne data ALDRIG bliver rørt. Demoen er en mappe for sig, og
/// den eneste måde, appen kan komme til at skrive demooptagelser ned i nogens
/// rigtige optagelser, er hvis <see cref="UserDataPaths.Root"/> peger det
/// forkerte sted, mens <see cref="Demodata.Byg"/> kører.
///
/// Og at der faktisk ER noget at se på. En demo, der åbner en tom skærm,
/// er værre end ingen demo — den viser, at appen ikke kan noget.
/// </remarks>
public class Demotest
{
    /// <summary>En tom demomappe, prøven har for sig selv.</summary>
    private sealed class Demomappe : IDisposable
    {
        private readonly string? _foerDemo;
        private readonly string? _foerData;
        private readonly string? _foerGammel;

        public Demomappe()
        {
            Sti = Path.Combine(Path.GetTempPath(), "heypia-demoproeve", Guid.NewGuid().ToString("N"));

            _foerDemo = Environment.GetEnvironmentVariable(Demotilstand.RodVariabel);
            Environment.SetEnvironmentVariable(Demotilstand.RodVariabel, Sti);

            // ============ MILJOEVARIABLEN SKAL VAEK IMENS ============
            //
            // Proevemappen saetter HEYPIA_DATA for hele samlingen, og den
            // vinder over demoen - med vilje, saa en taendt demo paa maskinen
            // ikke kan flytte proeverne. Men netop DERFOR kan raekkefoelgen
            // ikke proeves, saa laenge den staar.
            //
            // Proeverne koerer een ad gangen (se AssemblyInfo), saa den kan
            // tages vaek her og saettes tilbage bagefter uden at forstyrre
            // nogen.
            _foerData = Environment.GetEnvironmentVariable(UserDataPaths.OverrideVariable);
            _foerGammel = Environment.GetEnvironmentVariable(UserDataPaths.GammelOverrideVariable);

            Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, null);
            Environment.SetEnvironmentVariable(UserDataPaths.GammelOverrideVariable, null);

            UserDataPaths.Glem();
        }

        public string Sti { get; }

        public void Dispose()
        {
            Demotilstand.Sluk();

            Environment.SetEnvironmentVariable(Demotilstand.RodVariabel, _foerDemo);
            Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, _foerData);
            Environment.SetEnvironmentVariable(UserDataPaths.GammelOverrideVariable, _foerGammel);

            UserDataPaths.Glem();

            try { if (Directory.Exists(Sti)) Directory.Delete(Sti, recursive: true); }
            catch (IOException) { /* prøvemapper i temp rydder Windows selv */ }
        }
    }

    [Fact]
    public void Demoen_er_slukket_indtil_nogen_taender_den()
    {
        using var d = new Demomappe();

        Assert.False(Demotilstand.Taendt);

        Demotilstand.Taend();
        Assert.True(Demotilstand.Taendt);

        Demotilstand.Sluk();
        Assert.False(Demotilstand.Taendt);
    }

    [Fact]
    public void En_proeve_med_sin_egen_datamappe_rammer_ikke_demoen()
    {
        using var d = new Demomappe();
        Demotilstand.Taend();
        UserDataPaths.Glem();

        // MILJOEVARIABLEN VINDER. Ellers ville hver eneste proeve i huset
        // pludselig koere mod demodata paa en maskine, hvor demoen var taendt
        // - og det ville se ud som om, proeverne var i stykker.
        using var p = new Proevemappe();

        Assert.Equal(p.Sti, UserDataPaths.Root);
        Assert.NotEqual(Demotilstand.Rod, UserDataPaths.Root);
    }

    [Fact]
    public void Datamappen_peger_paa_demoen_naar_den_er_taendt()
    {
        using var d = new Demomappe();

        // Uden en proevemappe: her er det netop RODEN, der skal skifte.
        var egne = UserDataPaths.Root;

        // GLEM() KALDES HER OG IKKE AF TAEND(). En app, der er paa vej ud, maa
        // ikke skifte mappe under sig selv - se Demotilstand. Det er den nye
        // proces, der slaar roden op forfra, og det er dét, der proeves her.
        Demotilstand.Taend();
        UserDataPaths.Glem();
        Assert.Equal(Demotilstand.Rod, UserDataPaths.Root);

        Demotilstand.Sluk();
        UserDataPaths.Glem();
        Assert.Equal(egne, UserDataPaths.Root);
    }

    /// <summary>
    /// Bygger demoen i en mappe, prøven ejer — og UDEN at læse i brugerens
    /// egne optagelser efter lydeksempler. Se Lydeksempeltest for dem.
    /// </summary>
    private static void Byg() => Byg2();

    /// <summary>Det samme, men med svaret om der blev bygget.</summary>
    private static bool Byg2(bool tvungen = false) =>
        Demodata.Byg(tvungen, lydfra: Path.Combine(Path.GetTempPath(), "heypia-ingen-data"));

    [Fact]
    public void Der_er_noget_at_se_paa_i_hver_ende_af_appen()
    {
        using var d = new Demomappe();
        Demotilstand.Taend();
        UserDataPaths.Glem();

        Byg();

        Assert.True(MeetingStore.Alle().Count() >= 5, "for få optagelser");
        Assert.True(DocumentStore.LoadAll().Count >= 3, "for få dokumenter");
        Assert.True(Kalender.Alle().Count >= 5, "for få aftaler");
        Assert.True(Opgavelager.Alle().Count >= 6, "for få opgaver");
        Assert.True(Projektlager.Alle().Count >= 2, "for få projekter");
        Assert.True(Diktatnoter.Laes().Count >= 3, "for få diktater");
        Assert.True(PromptTemplate.LoadAll().Count >= 3, "for få mødetyper");
        Assert.True(PromptTemplate.LoadAll(Skabelonslags.Projektoutput).Count >= 4, "for få projektskabeloner");
    }

    [Fact]
    public void Optagelserne_har_en_udskrift_der_kan_soeges_i()
    {
        using var d = new Demomappe();
        Demotilstand.Taend();
        UserDataPaths.Glem();
        Byg();

        // SOEGNINGEN ER DET FOERSTE, MAN PROEVER. Staar den tom, viser demoen
        // ingenting - Cockpittet ER en soegeskaerm.
        var fund = Soegning.Soeg("Bakkegården");

        Assert.NotEmpty(fund);
        Assert.Contains(fund, f => f.Slags == Fundtype.Udskrift);
        Assert.Contains(fund, f => f.Slags == Fundtype.Note);
        Assert.Contains(fund, f => f.Slags == Fundtype.Dokument);
        Assert.Contains(fund, f => f.Slags == Fundtype.Projektfil);
    }

    [Fact]
    public void Datoerne_regnes_ud_fra_i_dag()
    {
        using var d = new Demomappe();
        Demotilstand.Taend();
        UserDataPaths.Glem();
        Byg();

        var nu = DateTimeOffset.Now;

        // Moederne ligger BAG os, aftalerne foran. En demo med aftaler i
        // fortiden ser forladt ud, og det er selve pointen, den spolerer.
        Assert.All(MeetingStore.Alle(), m =>
            Assert.InRange(m.StartedAt, nu.AddDays(-30), nu));

        Assert.Contains(Kalender.Alle(), a => a.Start > nu);
    }

    [Fact]
    public void Motoren_og_modellerne_hentes_ikke_igen_til_demoen()
    {
        using var d = new Demomappe();

        var egen = UserDataPaths.Maskinrod;

        Demotilstand.Taend();
        UserDataPaths.Glem();

        // DATAMAPPEN SKIFTER. Motoren gør ikke.
        Assert.Equal(Demotilstand.Rod, UserDataPaths.Root);
        Assert.Equal(egen, UserDataPaths.Maskinrod);

        // Motor, sprogmodeller og nøgle er maskinens — installeret én gang af
        // den her bruger. Lå de i datamappen, ville demoen bede om at hente
        // 3,6 GB ned én gang til.
        Assert.StartsWith(egen, WhisperInstall.Root, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(egen, WhisperInstall.ModelDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(egen, NoteApp.Core.Llm.SkyNoegle.Fil, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(Demotilstand.Rod, WhisperInstall.Root, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Demoen_beder_ikke_om_at_blive_sat_op()
    {
        using var d = new Demomappe();
        Demotilstand.Taend();
        UserDataPaths.Glem();

        Byg();
        AppSettings.Reload();

        // Var den falsk, kom velkomstforloebet — med et tilbud om at hente
        // motoren ned til en demo, der ikke skal transskribere noget.
        Assert.True(AppSettings.Current.SetupCompleted);
    }

    [Fact]
    public void Den_bygges_ikke_igen_hver_gang()
    {
        using var d = new Demomappe();
        Demotilstand.Taend();
        UserDataPaths.Glem();

        Assert.True(Byg2(), "første gang skal den bygges");
        Assert.False(Byg2(), "anden gang skal den lades i fred");

        // EN AENDRING I DEMOEN SKAL KUNNE SES. Bygges der forfra, maa der ikke
        // ligge to af hver bagefter.
        var foer = MeetingStore.Alle().Count();
        Assert.True(Byg2(tvungen: true));
        Assert.Equal(foer, MeetingStore.Alle().Count());
    }

    [Fact]
    public void Flaget_overlever_at_demoen_bygges_forfra()
    {
        using var d = new Demomappe();
        Demotilstand.Taend();
        UserDataPaths.Glem();

        Byg2();
        Byg2(tvungen: true);

        // Blev flaget ryddet med resten, ville appen falde tilbage paa
        // brugerens egne data midt i, at demoen blev bygget.
        Assert.True(Demotilstand.Taendt);
    }
}
