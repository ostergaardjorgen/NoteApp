using System.IO;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Et spor over, hvad genvejen og dikteringen gør — til fejlfinding.
/// </summary>
/// <remarks>
/// DEN FANDT TO FEJL, DER ELLERS IKKE KUNNE SES.
///
/// Da genvejen ikke virkede, var det sporet, der afgjorde sagen: der stod
/// ingenting. Beskeden nåede aldrig frem til appen, og så var det ikke
/// hold-logikken, der fejlede — det var tasten, der aldrig blev hørt.
///
/// Den skriver til datamappen og ikke i historikken. En linje pr. tastetryk
/// hører ikke hjemme på en skærm, brugeren læser.
///
/// Der skrives HVAD der skete, aldrig hvad der blev sagt eller skrevet.
/// </remarks>
public static class Spor
{
    private static string Fil => Path.Combine(UserDataPaths.Root, "log", "genvej-spor.log");

    public static void Skriv(string tekst)
    {
        try
        {
            var sti = Fil;
            Directory.CreateDirectory(Path.GetDirectoryName(sti)!);
            File.AppendAllText(sti, $"{DateTime.Now:HH:mm:ss.fff}  {tekst}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Et spor maa aldrig kunne vaelte en genvej.
        }
    }
}
