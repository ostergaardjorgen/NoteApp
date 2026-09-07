using System.Windows;
using NoteApp.Core;
using NoteApp.Core.Deling;

namespace NoteApp.Desktop.Deling;

/// <summary>
/// Spørgsmålet, der gør to computere til ét par: står der det samme tal på
/// begge skærme?
/// </summary>
/// <remarks>
/// ============ DEN SKAL KOMME AF SIG SELV ============
///
/// Godkendelsen skal ske på BEGGE maskiner — hver af dem fæstner den andens
/// nøgle. Før kunne den kun findes ét sted: inde under Indstillinger →
/// Deling, ved siden af computeren på listen. Den, der havde godkendt på sin
/// stationære, fik intet at vide på sin bærbare: ingen besked, intet
/// spørgsmål. Nøglen blev sendt, og den blev afvist i stilhed i den anden
/// ende.
///
/// Nu spørger den maskine, der mangler at sige ja, af sig selv — så snart den
/// kan se, at den anden har godkendt den. Se
/// <see cref="Maskinoplysning.Godkendte"/>.
///
/// ============ SPØRGSMÅLET SKAL VÆRE DET RIGTIGE ============
///
/// «Vil du parre?» er noget, man trykker ja til. «Står der 412 908 på den
/// anden skærm?» er noget, man bliver nødt til at se efter — og det er hele
/// beskyttelsen. Se <see cref="Parring"/>.
///
/// ============ DER SPØRGES ÉN GANG ============
///
/// Siger man «ikke nu», bliver der ikke spurgt igen, før appen startes forfra.
/// Et spørgsmål, der kommer hvert femte minut, er ikke en påmindelse — det er
/// noget, man klikker væk uden at læse, og så klikker man også den væk, der
/// betød noget.
/// </remarks>
public static class Parringsdialog
{
    private static readonly HashSet<string> Spurgt = new();

    /// <summary>Sat, mens dialogen står. Uret må ikke lægge en oven på en anden.</summary>
    private static bool _staarAaben;

    /// <summary>
    /// Viser koden og spørger, om den står ens begge steder.
    /// </summary>
    /// <returns>Sandt, hvis computeren blev godkendt.</returns>
    public static bool Spoerg(Window? ejer, Maskinoplysning m, bool afSigSelv = false)
    {
        if (m.Noegle.Length == 0)
        {
            Dialogs.AppDialog.Vis(ejer,
                Sprog.T("settingsview.deling_mangler_noegle_titel"),
                Sprog.T("settingsview.deling_mangler_noegle"), Dialogs.Slags.Pas_paa);

            return false;
        }

        string kode;

        try
        {
            kode = Parring.Kodevisning(Maskinid.Offentlignoegle(), m.Noegle);
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(ejer,
                Sprog.T("settingsview.deling_mangler_noegle_titel"), ex.Message, Dialogs.Slags.Fejl);

            return false;
        }

        _staarAaben = true;

        bool ja;

        try
        {
            ja = Dialogs.AppDialog.Spoerg(ejer,
                Sprog.T(afSigSelv ? "settingsview.deling_prompt_titel"
                                  : "settingsview.deling_par_titel", m.Navn),
                afSigSelv
                    ? Sprog.T("settingsview.deling_prompt_tekst", m.Navn, kode)
                    : Sprog.T("settingsview.deling_par_tekst", kode, m.Navn),
                godkend: Sprog.T("settingsview.deling_par_ja"),
                annuller: Sprog.T(afSigSelv ? "settingsview.deling_prompt_senere"
                                            : "settingsview.deling_par_nej"),
                slags: Dialogs.Slags.Valg, godkendErStandard: false);
        }
        finally
        {
            _staarAaben = false;
        }

        if (!ja) return false;

        Parring.Betro(m);

        // MELD MED DET SAMME. Den anden computer skal kunne se, at der er
        // sagt ja her - ellers staar den og venter paa noget, der er sket.
        Delt.Meld();

        Historik.Skriv(HaendelseType.Andet,
            Sprog.T("settingsview.deling_historik_parret", m.Navn),
            Sprog.T("settingsview.deling_historik_parret_detalje", kode), Udfald.Fuldført);

        return true;
    }

    /// <summary>
    /// Spørger, hvis en anden computer venter på et ja her.
    /// </summary>
    /// <remarks>
    /// DER SPØRGES KUN, NÅR DEN ANDEN HAR GODKENDT OS. Så er der et menneske,
    /// der har taget beslutningen på den anden skærm, og det eneste, der
    /// mangler, er den halvdel, der skal ske her. En maskine, der bare dukker
    /// op i mappen, giver ikke en dialog — den står på listen.
    /// </remarks>
    /// <returns>Sandt, hvis der blev godkendt noget.</returns>
    public static bool SpoergOmNoedvendigt(Window? ejer)
    {
        if (_staarAaben) return false;

        foreach (var m in Delt.Andre())
        {
            if (Parring.Tilstand(m) == Parringstilstand.Parret) continue;

            if (m.HarGodkendt(Maskinid.Id) != true) continue;

            if (!Spurgt.Add(m.Id)) continue;

            return Spoerg(ejer, m, afSigSelv: true);
        }

        return false;
    }
}
