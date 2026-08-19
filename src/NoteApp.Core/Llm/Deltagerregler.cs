namespace NoteApp.Core.Llm;

/// <summary>
/// De regler, enhver skabelon skal følge om deltagere.
///
/// HVORFOR DE STÅR I KODEN OG IKKE I HVER SKABELON
///
/// De stod skrevet af i både «Mødereferat» og «Dokumentation». En regel, der
/// er formuleret to steder, bliver to regler den dag den ene rettes — og den
/// slags opdages ikke, før et dokument har en deltager for meget.
///
/// Reglerne er skrevet 19-08-2026 efter en fejl, hvor to opdigtede deltagere
/// endte i et referat: «Mia», som aldrig deltog, og «Esben», som var Espen
/// hørt forkert. Begge stammede fra hilsner i den engelske småsnak, mens folk
/// loggede på. Se doc/maaling-sky.md.
///
/// Skabeloner skriver <c>{{deltagerregler}}</c> i systemprompten, og teksten
/// herunder bliver sat ind. Nye skabeloner får den med af sig selv.
/// </summary>
public static class Deltagerregler
{
    /// <summary>Feltet, en skabelon skriver for at få reglerne ind.</summary>
    public const string Felt = "deltagerregler";

    public const string Tekst = @"SÅDAN AFGØR DU, HVEM DER ER DELTAGERE

Udskriften er lavet af en talegenkendelse. Den hører navne forkert, og den
skriver løsrevne stumper ned fra begyndelsen af mødet, mens folk logger på og
siger hej. Det er DÉR, opdigtede deltagere kommer fra.

Følg disse regler, og fravig dem ikke:

1. En person er kun deltager, hvis vedkommende SIGER noget i udskriften eller
   udtrykkeligt bliver præsenteret som til stede. En hilsen alene — «hi
   Esben», «hej Mia» — er ikke nok.

2. Optræder et navn kun ÉN gang i hele udskriften, mens de øvrige navne
   optræder flere gange, så er det næsten altid hørt forkert. Tag det ikke med
   som deltager.

3. Ligner to navne hinanden — Espen og Esben, Mia og Maja — er det det samme
   menneske, hørt forskelligt. Vælg den stavemåde, der optræder flest gange,
   og skriv kun én linje.

4. Skriv ALDRIG en deltager med ukendt rolle. Kan du ikke sige, hvem
   vedkommende er, hører navnet ikke hjemme på listen. «Rolle ukendt» er ikke
   en oplysning; det er et gæt, der ser ud som en oplysning.

5. Er du i tvivl om en person, så lad være med at nævne vedkommende. En
   deltager for lidt bliver opdaget af den, der var med. En deltager for meget
   bliver det ikke — den bliver troet.

6. HERFRA og DERFRA ER IKKE NAVNE. Står de foran replikkerne, er udskriften
   lavet af to lydspor, og mærkatet siger, hvilken SIDE af mødet der talte —
   ikke hvem. Skriv dem aldrig som deltagere, og brug dem aldrig som roller.
   Der kan sagtens være flere personer bag hver af dem.

   Brug dem til det, de duer til: en replik mærket HERFRA er sagt af den, der
   optog, eller af nogen i det samme lokale. En mærket DERFRA er sagt af en af
   de øvrige. Det gør det muligt at holde styr på, hvem der lovede hvad, uden
   at gætte.";
}
