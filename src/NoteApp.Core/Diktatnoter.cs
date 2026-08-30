using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>En gemt diktering.</summary>
/// <param name="Tid">Hvornår den blev sagt.</param>
/// <param name="Tekst">Det, der kom ud — den pudsede tekst, ikke den rå.</param>
public sealed record Diktatnote(DateTime Tid, string Tekst)
{
    /// <summary>De første ord, så noten kan kendes igen på en liste.</summary>
    public string Overskrift
    {
        get
        {
            var t = Tekst.Replace('\n', ' ').Replace('\r', ' ').Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Length <= 70 ? t : t[..70].TrimEnd() + " …";
        }
    }
}

/// <summary>
/// Dikteringer, brugeren har valgt at gemme.
///
/// HVORFOR DE IKKE GEMMES AUTOMATISK
///
/// En diktering er som regel på vej et andet sted hen — ind i en mail, en
/// besked, et felt. Den er allerede landet dér, når teksten kommer. At gemme
/// hver eneste ville lave et arkiv af dubletter, som ingen har bedt om, og som
/// indeholder alt, hvad man har sagt hele dagen.
///
/// Derfor spørges der, og derfor gemmes der kun, når man siger ja.
///
/// FILEN LIGGER I DATAMAPPEN og er almindelig JSON, én linje pr. note. Den kan
/// åbnes, læses og slettes uden appen — det er brugerens tekst, ikke appens.
/// </summary>
public static class Diktatnoter
{
    public static string Sti => Path.Combine(UserDataPaths.Root, "diktatnoter.jsonl");

    /// <summary>
    /// Hvor mange der huskes.
    /// </summary>
    /// <remarks>
    /// Der er ingen oprydning ud over den her. En liste, der vokser i det
    /// uendelige, bliver til et arkiv, ingen har besluttet at have — og
    /// dikteringer indeholder alt fra en indkøbsseddel til noget fortroligt.
    /// De ældste falder ud, når der kommer nye.
    /// </remarks>
    public const int Maks = 200;

    private static readonly JsonSerializerOptions Opsaetning = new() { WriteIndented = false };

    /// <summary>Læser noterne — nyeste først.</summary>
    public static List<Diktatnote> Laes()
    {
        var ud = new List<Diktatnote>();

        try
        {
            if (!File.Exists(Sti)) return ud;

            foreach (var linje in File.ReadAllLines(Sti, Encoding.UTF8))
            {
                if (linje.Length == 0) continue;

                try
                {
                    var n = JsonSerializer.Deserialize<Diktatnote>(linje);
                    if (n is not null && n.Tekst.Length > 0) ud.Add(n);
                }
                catch (JsonException)
                {
                    // EN OEDELAGT LINJE KOSTER IKKE RESTEN. Filen er én linje
                    // pr. note netop derfor: en afbrudt skrivning rammer den
                    // sidste linje og ikke hele arkivet.
                }
            }
        }
        catch (IOException) { }

        ud.Reverse();
        return ud;
    }

    /// <summary>Lægger en note til. Tom tekst gemmes ikke.</summary>
    public static void Tilfoej(string tekst, DateTime? tid = null)
    {
        if (string.IsNullOrWhiteSpace(tekst)) return;

        var alle = Laes();
        alle.Insert(0, new Diktatnote(tid ?? DateTime.Now, tekst.Trim()));

        Skriv(alle);
    }

    /// <summary>Fjerner én note.</summary>
    public static void Slet(Diktatnote note) =>
        Skriv(Laes().Where(n => n != note).ToList());

    /// <summary>Fjerner dem alle.</summary>
    public static void Ryd() => Skriv(new List<Diktatnote>());

    private static void Skriv(List<Diktatnote> noter)
    {
        try
        {
            Directory.CreateDirectory(UserDataPaths.Root);

            // Nyeste ligger foerst i listen; i filen staar de aeldste foerst,
            // saa en ny note kan laegges til uden at skrive alt om.
            var beholdes = noter.Take(Maks).Reverse();

            var linjer = beholdes.Select(n => JsonSerializer.Serialize(n, Opsaetning));

            // Samme vaern som indstillingerne: der skrives til en kladde og
            // byttes om til sidst. Doer appen midt i, er den rigtige fil hel.
            var kladde = Sti + ".ny";
            File.WriteAllLines(kladde, linjer, Encoding.UTF8);

            if (File.Exists(Sti)) File.Replace(kladde, Sti, null, ignoreMetadataErrors: true);
            else File.Move(kladde, Sti);
        }
        catch (IOException) { }
    }
}
