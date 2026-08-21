using System.IO;
using NoteApp.Core;

namespace NoteApp.Desktop.Transcribe;

/// <summary>Hvor en optagelse hører hjemme.</summary>
public enum Gruppe
{
    /// <summary>Ligger fremme: holdt, men ikke færdigbehandlet.</summary>
    Moede,

    /// <summary>Lagt væk, fordi der ikke er mere at gøre ved det.</summary>
    Arkiv
}

/// <summary>
/// Afgør, hvilken gruppe en optagelse tilhører — ét sted.
///
/// HVORFOR DET SKAL VÆRE ÉT STED
///
/// To lister spørger om det samme: mødelisten og arkivet. Svarede de hver for
/// sig, ville en optagelse kunne stå to steder eller ingen steder, og det ville
/// først blive opdaget, når noget var væk. Reglen står her, og listerne spørger.
///
/// HER LÅ OGSÅ EN TREDJE GRUPPE: TRÆNING
///
/// Oplæsning af prøvetekster er fjernet 18-08-2026. Grunden var målt: hverken
/// ordlisten til Whisper, sprogmodellen over udskriften eller ordbogen til
/// sprogmodellen kunne påvises at flytte noget. Se doc/maaling-sky.md.
///
/// De optagelser, der allerede ER oplæsninger, ligger stadig på disken og
/// dukker nu op som almindelige møder. Det er med vilje: de bliver ikke
/// slettet, og de skal kunne arkiveres som alt andet.
/// </summary>
public static class Optagelsesgruppe
{
    public static Gruppe Af(string mappe, MeetingMetadata? meta) =>
        meta?.ArchivedAt is not null ? Gruppe.Arkiv : Gruppe.Moede;

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
            // Hovedsporet, ikke mikrofonen: et webinar har kun loopback.wav,
            // og saa ville laengden staa som nul paa en optagelse, der varede
            // en time.
            var wav = OptagelseVisning.Lydfilen(mappe);
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
