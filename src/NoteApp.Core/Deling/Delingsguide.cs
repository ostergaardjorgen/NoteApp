using NoteApp.Core.Llm;

namespace NoteApp.Core.Deling;

/// <summary>Hvor langt et trin er nået.</summary>
public enum Trinstand
{
    /// <summary>Gjort. Der er ikke mere at gøre her.</summary>
    Klar,

    /// <summary>Det, brugeren skal gøre nu.</summary>
    Naeste,

    /// <summary>Kan ikke gøres endnu, fordi et trin før mangler.</summary>
    Venter,
}

/// <summary>Ét trin i vejen mod to computere, der arbejder sammen.</summary>
public sealed record Delingstrin(int Nummer, string Overskrift, string Forklaring, Trinstand Stand);

/// <summary>
/// Vejen fra «appen er installeret» til «mine to computere arbejder sammen».
/// </summary>
/// <remarks>
/// ============ HVORFOR DEN FINDES ============
///
/// Delingsskærmen kunne alt, hvad der skulle til, og fortalte ikke, hvad man
/// skulle gøre først. Den, der lige har installeret appen på sin bærbare, ser
/// et navn, to valgmuligheder, en tom mappesti og en liste uden computere i —
/// og har ingen måde at vide, at rækkefølgen er: vælg mappen, åbn appen på den
/// anden computer, godkend koden, få nøglen sendt over.
///
/// Alt det stod i hovedet på den, der byggede det. Nu står det på skærmen.
///
/// ============ DEN SPØRGER, DEN FÅR IKKE FORTALT ============
///
/// Hvert trin læser den virkelige tilstand — er mappen der, har den anden
/// computer meldt sig, er den godkendt, er nøglen på plads. Et hak, der
/// sættes af den, der gjorde noget, står tilbage den dag nogen glemmer at
/// sætte det, og så viser skærmen noget, der ikke passer.
///
/// ============ DEN NAGER IKKE DEN, DER KUN HAR ÉN COMPUTER ============
///
/// <see cref="Paabegyndt"/> afgør, om trinnene overhovedet er relevante. En
/// primær maskine uden fælles mappe deler ikke med nogen og skal ikke have et
/// tal ved menupunktet for noget, den aldrig har bedt om.
/// </remarks>
public static class Delingsguide
{
    /// <summary>
    /// Er der overhovedet to computere i spil?
    /// </summary>
    /// <remarks>
    /// To ting røber det: at man har valgt en fælles mappe, eller at man har
    /// sagt, at den her computer er den sekundære. Det sidste er sagt i
    /// velkomstforløbet, længe før der er en mappe — og det er netop dér,
    /// vejledningen mangler mest.
    /// </remarks>
    public static bool Paabegyndt =>
        Maskinid.Deltmappe is not null || Maskinid.Rolle == Maskinrolle.Sekundaer;

    /// <summary>Trinnene i den rækkefølge, de skal gøres i.</summary>
    public static IReadOnlyList<Delingstrin> Trin()
    {
        var sekundaer = Maskinid.Rolle == Maskinrolle.Sekundaer;

        var mappeklar = Delt.Slaaet_til;

        var andre = mappeklar ? Delt.Andre().ToArray() : Array.Empty<Maskinoplysning>();
        var moedt = andre.Length > 0;

        // ============ BEGGE VEJE, ELLER INGEN AF DEM ============
        //
        // Vi skal have godkendt DEN, og den skal have godkendt OS. Stod der
        // hak, saa snart den ene halvdel var gjort, ville trinnet melde
        // faerdigt, mens intet kunne udveksles.
        //
        // En maskine paa en aeldre udgave siger ikke, hvem den har godkendt.
        // Saa maa der ikke staa, at den ikke har - se Maskinoplysning.Godkendte.
        var godkendt = andre.Any(m => Parring.MaaUdveksle(m)
                                      && m.HarGodkendt(Maskinid.Id) != false);

        var venterPaaDen = !godkendt && andre.Any(Parring.MaaUdveksle);

        var noegle = !string.IsNullOrWhiteSpace(SkyNoegle.Hent());

        var liste = new List<Delingstrin>
        {
            // ---------- 1: hvad er den her computer ----------
            new(1,
                T(sekundaer ? "guide_rolle_sekundaer" : "guide_rolle_primaer"),
                T(sekundaer ? "guide_rolle_sekundaer_under" : "guide_rolle_primaer_under"),
                Trinstand.Klar),

            // ---------- 2: den faelles mappe ----------
            new(2,
                T("guide_mappe"),
                T(mappeklar ? "guide_mappe_klar" : "guide_mappe_under"),
                mappeklar ? Trinstand.Klar : Trinstand.Naeste),

            // ---------- 3: den anden computer ----------
            new(3,
                T("guide_moed"),
                T(moedt ? "guide_moed_klar" : "guide_moed_under"),
                moedt ? Trinstand.Klar : mappeklar ? Trinstand.Naeste : Trinstand.Venter),

            // ---------- 4: godkendelsen ----------
            new(4,
                T("guide_godkend"),
                T(godkendt ? "guide_godkend_klar"
                  : venterPaaDen ? "guide_godkend_halvt"
                  : "guide_godkend_under"),
                godkendt ? Trinstand.Klar : moedt ? Trinstand.Naeste : Trinstand.Venter),

            // ---------- 5: noeglen til dokumenterne ----------
            new(5,
                T("guide_noegle"),
                Noegletekst(sekundaer, noegle, godkendt),
                noegle ? Trinstand.Klar
                       : sekundaer && !godkendt ? Trinstand.Venter
                       : Trinstand.Naeste),
        };

        return liste;
    }

    /// <summary>
    /// Nøgleteksten afhænger af, hvilken computer man sidder ved.
    /// </summary>
    /// <remarks>
    /// DEN SEKUNDÆRE SKAL IKKE OPRETTE EN KONTO TIL. Det var dét, skærmen
    /// sagde før: den samme vejledning om at oprette sig hos leverandøren stod
    /// på begge maskiner, også på den, der lige havde godkendt sin primære og
    /// kunne få nøglen sendt over på ét tryk.
    /// </remarks>
    private static string Noegletekst(bool sekundaer, bool noegle, bool godkendt)
    {
        if (noegle) return T("guide_noegle_klar");

        if (!sekundaer) return T("guide_noegle_under");

        return T(godkendt ? "guide_noegle_sekundaer" : "guide_noegle_sekundaer_vent");
    }

    /// <summary>Hvor mange trin der mangler.</summary>
    public static int Mangler() => Trin().Count(t => t.Stand != Trinstand.Klar);

    /// <summary>Er alt gjort?</summary>
    public static bool Faerdig() => Mangler() == 0;

    private static string T(string noegle) => Sprog.T("settingsview." + noegle);
}
