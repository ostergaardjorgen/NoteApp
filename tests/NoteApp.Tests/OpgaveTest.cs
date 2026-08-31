using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// En opgave, brugeren selv skriver — vejen fra «Ny opgave» og ind i
/// Cockpittet.
///
/// DEN FINDES, FORDI OPGAVER INDTIL NU KUN KOM UDEFRA.
///
/// Appen kunne vise dem, den selv havde fundet i en transkription, og dem, der
/// lå i Google Tasks. Alt andet skulle skrives et andet sted — og så er listen
/// i Cockpittet ikke længere den ene liste, den er bygget for at være. Da
/// «Ny opgave» kom til, blev den håndskrevne opgave en rigtig vej ind i
/// lageret, og de regler, den vej hviler på, står ingen andre steder end her.
///
/// Skærmen selv er WPF og kan ikke prøves. Det, der KAN, er det, den bygger
/// på: at opgaven bliver liggende, at den kan læses igen, at den lander det
/// rigtige sted i rækkefølgen, og at en hentning fra Google ikke fejer den væk.
/// </summary>
public class OpgaveTest
{
    /// <summary>Det, «Ny opgave» laver: en tom opgave uden et møde bag sig.</summary>
    private static Opgave Haandskrevet(string navn, DateTimeOffset? frist = null) =>
        new() { Navn = navn, Tekst = navn, Deadline = frist };

    // ======================= AT DEN OVERHOVEDET BLIVER LIGGENDE =======================

    [Fact]
    public void En_opgave_skrevet_i_haanden_kan_laeses_igen()
    {
        using var p = new Proevemappe();

        Opgavelager.Gem(Haandskrevet("Ring til banken"));

        var alle = Opgavelager.Alle();

        Assert.Single(alle);
        Assert.Equal("Ring til banken", alle[0].Navn);

        // DEN ER LOKAL, OG DET ER IKKE EN DETALJE. Herkomsten afgoer, om
        // opgaven kan rettes i appen - og en opgave, man lige har skrevet, som
        // man ikke kan rette, ville vaere den mest overraskende fejl af dem alle.
        Assert.Equal(Opgavekilde.Lokal, alle[0].Herkomst);
        Assert.True(alle[0].KanRettes);
    }

    [Fact]
    public void Den_staar_i_cockpittets_liste_med_det_samme()
    {
        using var p = new Proevemappe();

        Opgavelager.Gem(Haandskrevet("Køb kaffe"));

        var aabne = Opgaveregister.Aabne(DateOnly.FromDateTime(DateTime.Today));

        Assert.Single(aabne);
        Assert.Equal("Køb kaffe", aabne[0].Opgave.Visningsnavn);
    }

    [Fact]
    public void Uden_et_moede_bag_sig_siges_det_at_den_er_skrevet_i_haanden()
    {
        // HERKOMSTEN MAA IKKE STAA TOM. Kortet i Cockpittet viser den under
        // navnet, og en tom linje dér ligner en opgave, der har mistet noget.
        var r = new Registeropgave(Haandskrevet("Book mødelokale"));

        Assert.Equal("Skrevet i hånden", r.Moedetitel);
        Assert.Equal("", r.MoedeId);
    }

    // ============================ HVOR DEN LANDER I LISTEN ============================

    [Fact]
    public void Fristen_afgoer_raekkefoelgen_ikke_hvornaar_den_blev_skrevet()
    {
        using var p = new Proevemappe();

        var idag = DateOnly.FromDateTime(DateTime.Today);
        var nu = DateTimeOffset.Now;

        // Skrevet i den raekkefoelge, der er den OMVENDTE af den, listen skal
        // vise. Uden sorteringen ville den nyeste staa oeverst.
        Opgavelager.Gem(Haandskrevet("Uden frist"));
        Opgavelager.Gem(Haandskrevet("Om en uge", nu.AddDays(6)));
        Opgavelager.Gem(Haandskrevet("I dag", nu));
        Opgavelager.Gem(Haandskrevet("Skulle have været i mandags", nu.AddDays(-3)));

        var navne = Opgaveregister.Aabne(idag).Select(r => r.Opgave.Visningsnavn).ToList();

        Assert.Equal(
            new[] { "Skulle have været i mandags", "I dag", "Om en uge", "Uden frist" },
            navne);
    }

    [Fact]
    public void En_opgave_uden_frist_haster_ikke_men_forsvinder_heller_ikke()
    {
        var idag = DateOnly.FromDateTime(DateTime.Today);

        Assert.Equal(Hastighed.Uden_frist,
            new Registeropgave(Haandskrevet("Læs op til eksamen")).Hastighed(idag));
    }

    // =============================== NAVNET PAA LISTEN ===============================

    [Fact]
    public void Skriver_man_kun_en_beskrivelse_bliver_navnet_udledt_af_den()
    {
        // «Ny opgave» kraever et navn ELLER en beskrivelse. Skrives der kun det
        // sidste, skal listen alligevel kunne vise noget - ellers staar der en
        // tom raekke, der ligner en fejl i appen.
        var o = new Opgave { Tekst = "Sende det reviderede budget til bogholderiet inden fredag." };

        Assert.Equal("", o.Navn);
        Assert.Equal("Sende det reviderede budget til bogholderiet inden fredag", o.Visningsnavn);
    }

    [Fact]
    public void Et_navn_man_selv_har_skrevet_staar_uroert()
    {
        var o = new Opgave { Navn = "Budget", Tekst = "Sende det reviderede budget til bogholderiet." };

        Assert.Equal("Budget", o.Visningsnavn);
        Assert.True(o.HarMere);
    }

    // ===================== AT GOOGLE IKKE FEJER DEN VAEK IGEN =====================

    [Fact]
    public void En_hentning_fra_Google_roerer_ikke_det_man_selv_har_skrevet()
    {
        using var p = new Proevemappe();

        Opgavelager.Gem(Haandskrevet("Min egen"));

        Opgavelager.Afloes(Opgavekilde.Google, new[]
        {
            new Opgave
            {
                Navn = "Fra telefonen",
                Tekst = "Fra telefonen",
                Herkomst = Opgavekilde.Google,
                FremmedId = "g1",
                FremmedListe = "liste1"
            }
        });

        var alle = Opgavelager.Alle();

        Assert.Equal(2, alle.Count);
        Assert.Single(alle, o => o.Navn == "Min egen" && o.Herkomst == Opgavekilde.Lokal);
    }

    [Fact]
    public void Et_valg_af_liste_goer_ikke_opgaven_til_en_Google_opgave()
    {
        using var p = new Proevemappe();

        // VALGET AF LISTE ER ET OENSKE, IKKE EN HERKOMST.
        //
        // «Ny opgave» kan vaelge, hvilken af brugerens lister hos Google en
        // opgave skal op i, og valget saettes paa opgaven, foer den sendes.
        // Gaar afsendelsen galt, staar den tilbage som en LOKAL opgave med et
        // listenavn paa - og den maa ikke af den grund blive fejet vaek ved
        // naeste hentning, som var den en Google-opgave, der var forsvundet.
        var min = Haandskrevet("Skal op i «Studiet»");
        min.FremmedListe = "liste-studiet";

        Opgavelager.Gem(min);

        Opgavelager.Afloes(Opgavekilde.Google, Array.Empty<Opgave>());

        var alle = Opgavelager.Alle();

        Assert.Single(alle);
        Assert.Equal(Opgavekilde.Lokal, alle[0].Herkomst);
        Assert.Equal("liste-studiet", alle[0].FremmedListe);
    }

    // ========================== AT DEN KAN KRYDSES AF IGEN ==========================

    [Fact]
    public void Dagens_afkrydsede_bliver_staaende_dagen_ud()
    {
        using var p = new Proevemappe();

        var o = Haandskrevet("Hent pakken");
        o.SaetFaerdig(true);

        Opgavelager.Gem(o);

        var idag = DateOnly.FromDateTime(DateTime.Today);

        // Den er der endnu i dag - ellers er der ingen kvittering, og ingen vej
        // tilbage, hvis man ramte den forkerte.
        Assert.Single(Opgaveregister.Aabne(idag));

        // I morgen er den vaek.
        Assert.Empty(Opgaveregister.Aabne(idag.AddDays(1)));
    }
}
