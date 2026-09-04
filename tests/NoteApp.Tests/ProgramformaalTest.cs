using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, at formen følger programmet.
///
/// ET FORKERT GÆT ER VÆRRE END INGEN. Rammer den ved siden af, får du en mail,
/// hvor du ville have en note — og du skal opdage det og skrive om. Derfor er
/// listen kort, og derfor svares der <c>null</c>, når programmet ikke er
/// genkendt.
///
/// Prøverne her handler mest om DÉT: at den holder sin mund, når den ikke ved
/// det.
/// </summary>
public class ProgramformaalTest
{
    [Theory]
    [InlineData("OUTLOOK", Dikteringsformaal.Mail)]
    [InlineData("outlook.exe", Dikteringsformaal.Mail)]
    [InlineData("olk", Dikteringsformaal.Mail)]
    [InlineData("thunderbird", Dikteringsformaal.Mail)]
    [InlineData("todoist", Dikteringsformaal.Opgave)]
    public void Programmet_alene_kan_afgoere_det(string proces, Dikteringsformaal ventet)
    {
        Assert.Equal(ventet, Programformaal.Gaet(proces, ""));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("Cursor")]
    [InlineData("chatgpt")]
    [InlineData("perplexity")]
    public void En_AI_assistent_giver_ikke_laengere_et_gaet(string proces)
    {
        // «Prompt» var en form for sig: en instruktion til en AI, uden
        // hoeflighed og indpakning. Den er fjernet 04-09-2026, fordi
        // assistenternes egne felter er bedre til netop dét end en
        // omskrivning her — og saa skal appen heller ikke gaette paa den.
        //
        // Det staar som en proeve og ikke bare som en sletning, fordi det er
        // let at komme til at laegge «claude» tilbage i listen med en anden
        // form. Der skal ikke gaettes paa et program, hvor brugeren selv
        // sidder med det rigtige felt foran sig.
        Assert.Null(Programformaal.Gaet(proces, ""));
    }

    [Theory]
    [InlineData("msedge", "Claude")]
    [InlineData("firefox", "Le Chat — Mistral AI")]
    public void En_AI_fane_giver_heller_ikke_et_gaet(string proces, string titel)
    {
        Assert.Null(Programformaal.Gaet(proces, titel));
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("devenv")]
    [InlineData("explorer")]
    [InlineData("HeyPia")]
    [InlineData("et-program-ingen-har-hoert-om")]
    public void Ukendte_programmer_giver_ingen_gaetning(string proces)
    {
        // Den skal holde sin mund. Bruges dit eget valg i stedet, er det i
        // det mindste dit.
        Assert.Null(Programformaal.Gaet(proces, "hvad som helst"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ingen_proces_giver_ingen_gaetning(string? proces)
    {
        Assert.Null(Programformaal.Gaet(proces, "Gmail"));
    }

    [Theory]
    [InlineData("chrome", "Indbakke (12) - Gmail", Dikteringsformaal.Mail)]
    [InlineData("chrome", "Mine opgaver - Google Tasks", Dikteringsformaal.Opgave)]
    public void I_en_browser_er_titlen_det_eneste_der_siger_noget(
        string proces, string titel, Dikteringsformaal ventet)
    {
        // Er browseren fremme, hedder processen «chrome», og det siger
        // ingenting om, hvad man er i gang med.
        Assert.Equal(ventet, Programformaal.Gaet(proces, titel));
    }

    [Fact]
    public void En_browser_paa_en_ukendt_side_gaetter_ikke()
    {
        Assert.Null(Programformaal.Gaet("chrome", "DR Nyheder"));
        Assert.Null(Programformaal.Gaet("chrome", ""));
    }

    [Fact]
    public void Browserens_titel_smitter_ikke_af_paa_andre_programmer()
    {
        // «Gmail» i titlen paa et tilfaeldigt program er ikke et signal. Et
        // dokument kan hedde hvad som helst - ogsaa «Gmail.docx».
        Assert.Null(Programformaal.Gaet("winword", "Gmail — noter.docx"));
    }

    [Fact]
    public void Vaelg_bruger_dit_eget_valg_naar_gaetningen_er_slaaet_fra()
    {
        var svar = Programformaal.Vaelg("outlook", "", Dikteringsformaal.Note, efterProgram: false);

        Assert.Equal(Dikteringsformaal.Note, svar);
    }

    [Fact]
    public void Vaelg_falder_tilbage_paa_dit_eget_valg()
    {
        var svar = Programformaal.Vaelg("notepad", "", Dikteringsformaal.Opgave, efterProgram: true);

        Assert.Equal(Dikteringsformaal.Opgave, svar);
    }

    [Fact]
    public void Vaelg_lader_programmet_vinde_naar_det_er_genkendt()
    {
        var svar = Programformaal.Vaelg("outlook", "", Dikteringsformaal.Note, efterProgram: true);

        Assert.Equal(Dikteringsformaal.Mail, svar);
    }

    [Fact]
    public void Hver_gaetning_peger_paa_et_formaal_der_findes()
    {
        // Et formaal, der ikke findes i Dikteringsformaal, ville kaste inde i
        // Pudseprompt - midt i et diktat, efter lyden er sendt.
        // «claude» stod her, indtil formen «Prompt» blev fjernet. Den giver
        // ikke laengere et gaet - se En_AI_assistent_giver_ikke_laengere_et_gaet.
        foreach (var proces in new[] { "outlook", "todoist" })
        {
            var f = Programformaal.Gaet(proces, "");

            Assert.NotNull(f);
            Assert.True(Enum.IsDefined(f!.Value));
            Assert.False(string.IsNullOrWhiteSpace(Voxtral.Pudseprompt(f.Value)));
        }
    }
}
