using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>Det, en færdig diktering gav.</summary>
/// <param name="Tekst">Den pudsede tekst — den, der lander et sted.</param>
/// <param name="Raa">
/// Udskriften før pudsningen, med ordbogens rettelser i.
/// </param>
/// <param name="Formaal">Hvilken type den blev pudset som.</param>
/// <remarks>
/// DEN RÅ TEKST FØLGER MED HELE VEJEN. Uden den kan en note ikke laves om til
/// en mail: pudsningen har allerede kastet fyldordene væk og valgt en form,
/// og en omskrivning af en omskrivning driver længere og længere væk fra det,
/// der faktisk blev sagt.
/// </remarks>
public sealed record Diktatudfald(string Tekst, string Raa, string Formaal);

/// <summary>En gemt diktering.</summary>
/// <param name="Tid">Hvornår den blev sagt.</param>
/// <param name="Tekst">Det, der kom ud — den pudsede tekst.</param>
/// <param name="Raa">
/// Den RÅ udskrift, før den blev pudset af.
/// </param>
/// <param name="Formaal">Hvilken type teksten er pudset som.</param>
/// <remarks>
/// DEN RÅ TEKST GEMMES MED, OG DET ER DET, DER GØR NOTEN OM.
///
/// En pudset note kan ikke laves om til en mail: pudsningen har allerede
/// kastet fyldordene væk og valgt en form. Skulle den skrives om, ville
/// modellen skrive om på sin egen tekst og drive længere og længere væk fra
/// det, der faktisk blev sagt.
///
/// Med den rå udskrift kan enhver type laves forfra ud fra det samme
/// grundlag — og det er også dét, der gør, at man kan komme tilbage til det,
/// man rent faktisk sagde.
///
/// Tom på noter fra før feltet fandtes. Så kan typen ikke skiftes, og det
/// siges i stedet for at gætte.
/// </remarks>
public sealed record Diktatnote(DateTime Tid, string Tekst, string Raa = "", string Formaal = "")
{
    /// <summary>Kan noten skrives om til en anden type?</summary>
    public bool KanSkiftes => Raa.Trim().Length > 0;

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
    /// <param name="raa">
    /// Den rå udskrift. Uden den kan noten ikke laves om til en anden type —
    /// se <see cref="Diktatnote"/>.
    /// </param>
    public static Diktatnote? Tilfoej(string tekst, string raa = "",
                                      string formaal = "", DateTime? tid = null)
    {
        if (string.IsNullOrWhiteSpace(tekst)) return null;

        var note = new Diktatnote(tid ?? DateTime.Now, tekst.Trim(), (raa ?? "").Trim(), formaal);

        var alle = Laes();
        alle.Insert(0, note);

        Skriv(alle);
        return note;
    }

    /// <summary>Erstatter en note med en ny udgave — fx en anden teksttype.</summary>
    public static void Erstat(Diktatnote gammel, Diktatnote ny)
    {
        var alle = Laes();

        var i = alle.FindIndex(n => n == gammel);
        if (i < 0) return;

        alle[i] = ny;
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
