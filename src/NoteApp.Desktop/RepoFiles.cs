using System.IO;

namespace NoteApp.Desktop;

/// <summary>
/// Finder filer i kode-repoet, når appen kører fra det.
///
/// Appen skal virke begge steder: fra en build-mappe dybt nede i src\, og fra
/// den publicerede exe i C:\NoteApp\app. De to har vidt forskellig afstand til
/// repo-roden, så en fast optælling af ".." rammer kun det ene tilfælde — det
/// var netop fejlen her første gang. Derfor gås der opad, indtil filen findes.
///
/// Findes repoet ikke — appen er kopieret til en anden maskine — er svaret
/// null, og kalderen falder tilbage på den indlejrede kopi. Det er med vilje
/// ikke en fejl: appen må ikke kræve, at kodemappen er der.
/// </summary>
public static class RepoFiles
{
    private const int MaksNiveauer = 8;

    public static string? Find(params string[] relativeParts)
    {
        var mappe = new DirectoryInfo(AppContext.BaseDirectory);

        for (var i = 0; i < MaksNiveauer && mappe is not null; i++)
        {
            var kandidat = Path.Combine(new[] { mappe.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(kandidat)) return kandidat;
            mappe = mappe.Parent;
        }

        return null;
    }
}
