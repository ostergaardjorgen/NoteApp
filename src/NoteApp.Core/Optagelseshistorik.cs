using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>Hvad der skete med optagelsen.</summary>
public enum Optagelsesskift
{
    /// <summary>Optagelsen blev til.</summary>
    Oprettet,

    /// <summary>Navnet blev et andet.</summary>
    Omdoebt,

    /// <summary>Lyden blev skrevet ud som tekst.</summary>
    Transskriberet,

    /// <summary>Der blev lavet et dokument ud af den.</summary>
    Dokument,

    /// <summary>Et dokument blev slettet.</summary>
    Dokumentslettet,

    /// <summary>Den blev flyttet til en anden mappe.</summary>
    Flyttet,

    /// <summary>Den blev lagt væk som færdigbehandlet — eller hentet frem igen.</summary>
    Arkiveret,
}

/// <summary>Én linje i optagelsens egen historik.</summary>
/// <param name="Tid">Hvornår det skete.</param>
/// <param name="Skift">Hvad der skete.</param>
/// <param name="Hvad">Den korte sætning, listen viser.</param>
/// <param name="Fra">Det, der stod før — tomt, når intet blev ændret.</param>
/// <param name="Til">Det, der står nu.</param>
public sealed record Optagelsesskridt(
    DateTimeOffset Tid,
    Optagelsesskift Skift,
    string Hvad,
    string Fra = "",
    string Til = "");

/// <summary>
/// Optagelsens egen historik — hvad der er sket med netop den.
///
/// HVORFOR DEN IKKE ER DEN GLOBALE HISTORIK
///
/// <see cref="Historik"/> findes og skriver allerede «Dokument oprettet» og
/// «Møde optaget». Den kunne ikke bruges her, og det blev målt: af de linjer,
/// der er skrevet indtil nu, peger «Dokument oprettet» sin <c>Sti</c> på
/// DOKUMENTETS mappe og ikke på optagelsens, og et navneskift blev slet ikke
/// skrevet ned. Der er altså ingen nøgle at samle på.
///
/// Den globale historik er et driftslog: hvad appen har lavet, hvor længe det
/// tog, og om det gik galt. Den her er en sagsmappe: hvad der er sket med
/// DETTE møde, i ord man kan læse et halvt år efter. De to spørgsmål er ikke
/// det samme, og et forsøg på at lade én fil svare på begge ender med at
/// svare dårligt på begge.
///
/// EN FIL PR. OPTAGELSE, VED SIDEN AF LYDEN. Så følger historikken med, når
/// mappen flyttes eller arkiveres — og den forsvinder med optagelsen, når den
/// slettes, hvilket er det rigtige: der er ikke noget tilbage at fortælle om.
///
/// DEN MÅ ALDRIG VÆLTE NOGET. At skrive en linje ned om, at et dokument blev
/// lavet, er mindre vigtigt end dokumentet. Derfor sluges alle fejl — både
/// ved skrivning og ved læsning.
/// </summary>
public static class Optagelseshistorik
{
    private const string Filnavn = "historik.jsonl";

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = false };

    private static readonly object Laas = new();

    public static string Fil(string mappe) => Path.Combine(mappe, Filnavn);

    /// <summary>
    /// Skriver en linje. Gør ingenting, hvis mappen ikke findes.
    /// </summary>
    public static void Skriv(string? mappe, Optagelsesskift skift, string hvad,
                             string fra = "", string til = "")
    {
        if (string.IsNullOrWhiteSpace(mappe)) return;

        try
        {
            if (!Directory.Exists(mappe)) return;

            var skridt = new Optagelsesskridt(DateTimeOffset.Now, skift, hvad, fra, til);

            lock (Laas)
                File.AppendAllText(Fil(mappe),
                    JsonSerializer.Serialize(skridt, Format) + "\n", new UTF8Encoding(false));
        }
        catch (Exception)
        {
            // Med vilje tavs. Se noten på klassen.
        }
    }

    /// <summary>
    /// Læser historikken, nyeste først.
    /// </summary>
    /// <remarks>
    /// EN ØDELAGT LINJE SPRINGES OVER, og resten læses færdig. Filen skrives
    /// linje for linje, og en app, der lukkes midt i en skrivning, kan efterlade
    /// en halv linje. Den må ikke koste hele historikken.
    /// </remarks>
    public static IReadOnlyList<Optagelsesskridt> Laes(string? mappe)
    {
        if (string.IsNullOrWhiteSpace(mappe)) return Array.Empty<Optagelsesskridt>();

        var sti = Fil(mappe);

        if (!File.Exists(sti)) return Array.Empty<Optagelsesskridt>();

        var liste = new List<Optagelsesskridt>();

        try
        {
            foreach (var linje in File.ReadLines(sti))
            {
                if (linje.Length == 0) continue;

                try
                {
                    var s = JsonSerializer.Deserialize<Optagelsesskridt>(linje);
                    if (s is not null) liste.Add(s);
                }
                catch (JsonException)
                {
                    // Se noten. Videre til næste linje.
                }
            }
        }
        catch (Exception)
        {
            // Kan filen ikke læses, er svaret det, der nåede at komme med.
        }

        liste.Reverse();
        return liste;
    }
}
