using System.IO;
using NoteApp.Core;
using NoteApp.Desktop.ReadAloud;

namespace NoteApp.Desktop.Transcribe;

/// <summary>Hvor en optagelse hører hjemme.</summary>
public enum Gruppe
{
    /// <summary>Ligger fremme: holdt, men ikke færdigbehandlet.</summary>
    Moede,

    /// <summary>Lagt væk, fordi der ikke er mere at gøre ved det.</summary>
    Arkiv,

    /// <summary>En oplæsning af en af appens prøvetekster. Ikke et møde.</summary>
    Traening
}

/// <summary>
/// Afgør, hvilken gruppe en optagelse tilhører — ét sted.
///
/// HVORFOR DET SKAL VÆRE ÉT STED
///
/// Tre lister spørger om det samme: mødelisten, arkivet og træningen. Svarede
/// de hver for sig, ville en optagelse kunne stå to steder eller ingen steder,
/// og det ville først blive opdaget, når noget var væk. Reglen står her, og
/// listerne spørger.
///
/// HVORFOR TRÆNING KENDES PÅ ET FELT OG IKKE PÅ NAVNET
///
/// Første udgave læste teksten ud af TITLEN. Da den første oplæsning blev
/// omdøbt fra «Fase0-oplaesning» til «Første oplæsning», forsvandt den ud af
/// tællingen — den lå der stadig, men appen kunne ikke længere se det. Titlen
/// er brugerens og skal kunne hedde hvad som helst.
///
/// Navnegætteriet står stadig tilbage som NØDSPOR for de optagelser, der blev
/// lavet, før feltet fandtes. Det skal ikke bruges til nye.
/// </summary>
public static class Optagelsesgruppe
{
    public static Gruppe Af(string mappe, MeetingMetadata? meta)
    {
        if (Traeningsnoegle(mappe, meta) is not null) return Gruppe.Traening;
        return meta?.ArchivedAt is not null ? Gruppe.Arkiv : Gruppe.Moede;
    }

    /// <summary>
    /// Hvilken prøvetekst der blev læst op — «dansk», «blandet», «engelsk» —
    /// eller null, hvis optagelsen er et rigtigt møde.
    /// </summary>
    public static string? Traeningsnoegle(string mappe, MeetingMetadata? meta)
    {
        if (meta?.ReadAloudScript is { Length: > 0 } noegle) return noegle;

        var navn = (meta?.Title ?? "") + " " + Path.GetFileName(mappe);

        var erOplaesning =
            navn.Contains("Oplæsning", StringComparison.OrdinalIgnoreCase) ||
            navn.Contains("Oplaesning", StringComparison.OrdinalIgnoreCase);

        if (!erOplaesning) return null;

        // Findes ingen nøgle i navnet, er det den gamle enkelttekst — dengang
        // fandtes kun den danske.
        return ScriptDocument.Available
            .Where(t => navn.Contains(t.Key, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Key)
            .FirstOrDefault() ?? "dansk";
    }

    /// <summary>Filen med den tekst, der blev læst op. Null hvis nøglen er ukendt.</summary>
    public static string? Manuskriptfil(string? noegle) =>
        noegle is null ? null
        : ScriptDocument.Available.FirstOrDefault(t => t.Key == noegle).File;

    /// <summary>Tekstens navn, som det står i menuen.</summary>
    public static string Tekstnavn(string? noegle) =>
        ScriptDocument.Available.FirstOrDefault(t => t.Key == noegle).Name ?? noegle ?? "ukendt";

    /// <summary>
    /// Lægger et møde i arkivet, eller henter det frem igen.
    ///
    /// Der flyttes ingen filer. Mappen bliver liggende, hvor den er, og kun
    /// meeting.json ændrer sig — så kan en arkivering ikke gå galt halvvejs og
    /// efterlade en optagelse et sted, ingen leder.
    /// </summary>
    public static bool Arkiver(string mappe, bool arkiveret)
    {
        var meta = MeetingStore.Load(mappe) ?? Nødmetadata(mappe);

        meta.ArchivedAt = arkiveret ? DateTimeOffset.Now : null;
        MeetingStore.Save(mappe, meta);
        return true;
    }

    /// <summary>
    /// Bygger en meeting.json til en mappe, der ikke har en.
    ///
    /// De findes: optagelser fra dengang appen var et konsolprogram, og
    /// mapper, hvor programmet døde, før filen blev skrevet. Uden den kunne de
    /// hverken arkiveres eller omdøbes — «Optagelsen har ingen meeting.json og
    /// kan ikke arkiveres» er en fejlmeddelelse, der beder brugeren om at
    /// forstå appens filformat for at rydde op i sin egen liste.
    ///
    /// Datoen tages fra mappen. Den er ikke præcis, men den er rigtig nok til
    /// at sortere efter — og et gæt, der står ved siden af de andre, er bedre
    /// end en optagelse, man ikke kan røre.
    /// </summary>
    public static MeetingMetadata Nødmetadata(string mappe)
    {
        var tid = new DateTimeOffset(Directory.GetLastWriteTime(mappe));

        double sekunder = 0;
        try
        {
            var wav = Path.Combine(mappe, "mikrofon.wav");
            if (File.Exists(wav)) sekunder = Transcriber.WavSeconds(wav);
        }
        catch (Exception) { }

        return new MeetingMetadata
        {
            StartedAt = tid,
            EndedAt = tid,
            DurationSeconds = Math.Round(sekunder, 1),
            Title = Path.GetFileName(mappe),
            Type = MeetingType.Physical
        };
    }
}
