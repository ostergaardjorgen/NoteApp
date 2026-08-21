namespace NoteApp.Core.Llm;

/// <summary>
/// Hvilket sprog dokumentet skal skrives på — valgt pr. dokument.
///
/// HVORFOR DET IKKE STÅR I SKABELONEN
///
/// Det gjorde det. Hver skabelon havde sin egen linje om at skrive på dansk,
/// og resultatet var, at sproget lå fast: et engelsk webinar blev til et dansk
/// dokument, hver gang, uanset hvad det skulle bruges til.
///
/// Behovet er ikke det samme hver gang. Et referat til en selv skal være på
/// dansk; et resumé af et engelsk webinar, der skal videresendes eller ligge
/// sammen med engelsk kildemateriale, skal ikke oversættes frem og tilbage.
///
/// Reglen hører derfor til VALGET og ikke til skabelonen — på samme måde som
/// <see cref="Deltagerregler"/> hører til koden og ikke til hver enkelt fil.
///
/// Skabeloner skriver <c>{{sprogregler}}</c> i systemprompten. Gør de det
/// ikke — en skabelon, brugeren selv har skrevet — bliver teksten lagt til
/// sidst i systemprompten i stedet. Valget skal virke uanset hvad; en
/// indstilling, der kun virker i de skabeloner, der er skrevet rigtigt, er
/// værre end ingen indstilling.
/// </summary>
public static class Sprogregler
{
    /// <summary>Feltet, en skabelon skriver for at få reglen ind.</summary>
    public const string Felt = "sprogregler";

    /// <summary>Sprogene, et dokument kan skrives på.</summary>
    public static readonly (string Kode, string Navn)[] Sprog =
    {
        ("da", "Dansk"),
        ("en", "Engelsk")
    };

    public const string Standard = "da";

    public static string Navn(string kode) =>
        Sprog.FirstOrDefault(s => s.Kode == kode).Navn ?? "Dansk";

    /// <summary>
    /// Reglen, som den står i systemprompten.
    ///
    /// Den er skrevet i bydeform og gentager sig selv med vilje. En model, der
    /// læser tyve tusind tokens engelsk udskrift bagefter, glider over i
    /// engelsk, hvis den kun har fået at vide én gang, hvad sproget er — og
    /// den fejl viser sig som en overskrift midt i dokumentet, ikke som en
    /// fejlmeddelelse.
    /// </summary>
    public static string Tekst(string kode) => kode == "en"
        ? @"WRITE THE ENTIRE DOCUMENT IN ENGLISH.

This applies to every part of it: headings, bullet points, and the body text.
Do not switch to Danish at any point, not even for a single heading, and not
even if the transcript is in Danish. If the transcript is in Danish, translate
what you need into English as you write.

Proper nouns — names of people, companies and products — are kept as they are."
        : @"SKRIV HELE DOKUMENTET PÅ DANSK.

Det gælder alle dele af det: overskrifter, punktopstillinger og brødtekst.
Skift aldrig til engelsk undervejs, heller ikke i en enkelt overskrift, og
heller ikke hvis udskriften er på engelsk. Er udskriften på engelsk, så oversæt
undervejs, mens du skriver.

Egennavne — navne på personer, virksomheder og produkter — beholdes, som de er.
Det samme gælder faste fagudtryk, hvor en dansk oversættelse ville gøre dem
umulige at genkende.";
}
