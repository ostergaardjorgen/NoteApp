using System.Windows;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// Starter optagelsen af sig selv, når et møde begynder.
///
/// KUN DE AFTALER, DER ER MARKERET. Vagten går ikke i gang, fordi der står et
/// møde i kalenderen — den gør det, fordi nogen har sat hak ved «Optag
/// automatisk» på netop den aftale. En kalender, der begynder at optage af sig
/// selv, er ikke en hjælp; det er noget, man slår fra.
///
/// DEN KRÆVER, AT APPEN KØRER, og det kan ikke laves om.
///
/// En optagelse er en proces, der skal være i gang. Et program, der ikke
/// kører, kan ikke starte den — og en planlagt opgave i Windows, der startede
/// appen på klokkeslættet, ville starte den UDEN lyden fra de første
/// sekunder, mens motoren blev hentet ind. Derfor står det i indstillingerne
/// som en betingelse frem for at være noget, man opdager den dag, et møde ikke
/// blev optaget.
///
/// FLYTTES MØDET, FLYTTER OPTAGELSEN MED. Vagten spørger kalenderen om, hvad
/// der gælder NU — den gemmer ikke et klokkeslæt, den fik at vide en gang. Så
/// snart en hentning fra Google har rykket aftalen, kigger vagten på det nye
/// tidspunkt.
///
/// OPTAGELSE MÅ ALDRIG KUNNE BLOKERES. Vagten starter kun; den stopper
/// aldrig, spørger aldrig og kan ikke stå i vejen. Går noget galt i den, går
/// det galt i stilhed, og alt andet kører videre.
/// </summary>
public sealed class Kalendervagt
{
    /// <summary>
    /// Hvor tidligt før mødet der startes.
    ///
    /// To minutter, fordi folk møder op før tid, og fordi de første replikker
    /// er dem, hvor man siger, hvad mødet handler om. Et møde, hvor optagelsen
    /// begynder præcis på slaget, mangler netop dét.
    /// </summary>
    private static readonly TimeSpan Foer = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Hvor længe efter starten vagten stadig vil gå i gang.
    ///
    /// Ti minutter. Kommer man tilbage til maskinen en time inde i et møde,
    /// skal appen ikke pludselig begynde at optage — så er mødet halvvejs, og
    /// en optagelse, der starter dér, ligner en fejl. Grænsen er også det, der
    /// forhindrer, at alle dagens forbigåede møder går i gang på én gang, når
    /// appen åbnes om eftermiddagen.
    /// </summary>
    private static readonly TimeSpan Efter = TimeSpan.FromMinutes(10);

    private readonly Func<bool> _optagerAllerede;
    private readonly Action<Aftale> _start;

    // Et halvt minut. Fint nok, naar der startes to minutter foer - og
    // billigt: der laeses én fil.
    private readonly DispatcherTimer _ur = new() { Interval = TimeSpan.FromSeconds(30) };

    public Kalendervagt(Func<bool> optagerAllerede, Action<Aftale> start)
    {
        _optagerAllerede = optagerAllerede;
        _start = start;

        _ur.Tick += (_, _) => Kig();
        _ur.Start();
    }

    /// <summary>
    /// Er der en aftale, der skal i gang lige nu?
    ///
    /// Hele metoden er pakket ind. En fejl her må ikke kunne rive appen ned —
    /// og slet ikke midt i en optagelse, der kører i forvejen.
    /// </summary>
    private void Kig()
    {
        try
        {
            // OPTAGER DER ALLEREDE, SKER DER INGENTING. To møder, der
            // overlapper, er almindeligt; at afbryde det, der koerer, for at
            // starte det naeste ville vaere den vaerst taenkelige adfaerd.
            if (_optagerAllerede()) return;

            var nu = DateTimeOffset.Now;

            var aftaler = Kalender.Alle();

            var klar = aftaler.FirstOrDefault(a =>
                a.OptagAutomatisk
                && a.Startet is null
                && a.MoedeId.Length == 0
                && nu >= a.Start - Foer
                && nu <= a.Start + Efter);

            if (klar is null) return;

            // MAERKET FOERST, START BAGEFTER.
            //
            // Skrives det bagefter, og gaar noget galt undervejs, staar
            // aftalen umaerket - og saa proever vagten igen om et halvt
            // minut. Og igen. En moedetype, der starter en optagelse hvert
            // halve minut, er en vaerre fejl end en optagelse, der ikke kom i
            // gang.
            klar.Startet = nu;
            Kalender.Gem(klar);

            _start(klar);
        }
        catch (Exception)
        {
            // I stilhed, og med vilje. Vagten er en hjaelp; naar den ikke kan
            // hjaelpe, skal den lade vaere med at goere opmaerksom paa sig
            // selv midt i noget andet.
        }
    }

    /// <summary>Standser vagten. Kaldes, når appen lukker.</summary>
    public void Stop() => _ur.Stop();
}
