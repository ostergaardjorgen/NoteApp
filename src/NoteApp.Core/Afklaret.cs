using System.IO;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Fejl, brugeren har set på og afgjort, at der ikke er noget at gøre ved.
///
/// HVORFOR DET SKAL KUNNE LADE SIG GØRE
///
/// Tre fjerdedele af fejlene er almindelige ord: «går» hvor der stod «gik»,
/// «at» hvor der stod «af». De kan ikke rettes med en regel — reglen ville
/// ramme hver gang ordet bliver sagt, også de fyrre gange det var rigtigt. Og
/// en engangsrettelse i teksten ville ikke lære appen noget.
///
/// Uden en måde at lukke dem på ville listen stå med fyrre uafklarede
/// sætninger for altid. Man kan ikke se, hvad man har været igennem, og hvad
/// man ikke har — og så holder man op med at bruge listen.
///
/// AT AFKLARE ER IKKE AT RETTE. Der ændres ingenting: hverken i udskriften, i
/// reglerne eller i tallet. Det er en note om, at der er kigget på den.
/// </summary>
public static class Afklaret
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Fil(string optagelse) => Path.Combine(optagelse, "afklaret.json");

    /// <summary>De hørte former, der er afklaret i denne optagelse.</summary>
    public static HashSet<string> Laes(string optagelse)
    {
        var sæt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fil = Fil(optagelse);

        if (!File.Exists(fil)) return sæt;

        try
        {
            var liste = JsonSerializer.Deserialize<List<string>>(
                File.ReadAllText(fil, System.Text.Encoding.UTF8));

            if (liste is not null) foreach (var s in liste) sæt.Add(s);
        }
        catch (JsonException) { /* en oedelagt fil maa ikke vaelte listen */ }

        return sæt;
    }

    /// <summary>
    /// Markerer en fejl som afklaret. Nøglen er sætningsnummeret plus det
    /// hørte — det samme ord kan være hørt forkert i én sætning og rigtigt i
    /// en anden, og de er ikke det samme spørgsmål.
    /// </summary>
    public static void Gem(string optagelse, int saetning, string hoert)
    {
        var sæt = Laes(optagelse);
        sæt.Add(Noegle(saetning, hoert));

        try
        {
            File.WriteAllText(Fil(optagelse),
                JsonSerializer.Serialize(sæt.ToList(), Options), System.Text.Encoding.UTF8);
        }
        catch (IOException) { /* skrivebeskyttet mappe — fejlen staar bare aaben */ }
    }

    /// <summary>Fortryder en afklaring.</summary>
    public static void Fjern(string optagelse, int saetning, string hoert)
    {
        var sæt = Laes(optagelse);
        if (!sæt.Remove(Noegle(saetning, hoert))) return;

        try
        {
            File.WriteAllText(Fil(optagelse),
                JsonSerializer.Serialize(sæt.ToList(), Options), System.Text.Encoding.UTF8);
        }
        catch (IOException) { }
    }

    public static string Noegle(int saetning, string hoert) => $"{saetning}|{hoert.Trim()}";
}
