using System;
using System.IO;
using System.Runtime.CompilerServices;
using NoteApp.Core;

namespace NoteApp.Tests;

/// <summary>
/// Sørger for, at prøverne ALDRIG rører brugerens rigtige data.
/// </summary>
/// <remarks>
/// DEN HER FIL FINDES, FORDI PRØVERNE SLETTEDE BRUGERENS INDSTILLINGER.
///
/// NotifikationsvarslingTest kalder Notifikationer.MarkerAlleLaest, og den
/// gemmer i AppSettings. Prøven pegede ikke datamappen et andet sted hen, så
/// den skrev i den RIGTIGE fil — med standardværdier, fordi prøvens eget
/// objekt ikke havde brugerens valg i sig.
///
/// Resultatet: hver gang «dotnet test» blev kørt, mistede brugeren sin
/// mikrofon, sin genvejstast og sit gennemførte velkomstforløb. Det skete
/// altså ved hver eneste udgivelse, for prøverne køres først.
///
/// Det tog en dag at finde, fordi det så ud som en fejl i appen: værdier
/// forsvandt «af sig selv», og appen kunne ikke gøre for det. Sporet i
/// v1.2.0 skrev kaldsstakken med, og der stod prøvens navn.
///
/// HVORFOR EN MODULINITIALIZER OG IKKE EN RETTELSE I DEN ENE PRØVE
///
/// Én prøve kan rettes. Den næste, nogen skriver, kan ikke. Her sættes
/// datamappen om, FØR den første prøve overhovedet kører, og så gælder det
/// hele samlingen — også det, der bliver skrevet i morgen.
///
/// Mappen ligger under TEMP og har prøvekørslens eget navn, så to kørsler
/// ikke kan træde i hinandens data.
/// </remarks>
public static class Proevemappen
{
    /// <summary>Hvor prøverne har lov at skrive.</summary>
    public static string Sti { get; private set; } = "";

    [ModuleInitializer]
    public static void Saet()
    {
        Sti = Path.Combine(Path.GetTempPath(),
                           "heypia-proever-" + Guid.NewGuid().ToString("N")[..12]);

        Directory.CreateDirectory(Sti);

        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, Sti);

        // DEN GAMLE SKAL OGSAA VAEK. UserDataPaths laeser begge navne, og en
        // efterladt NOTEAPP_DATA ville pege proeverne tilbage paa noget
        // rigtigt.
        Environment.SetEnvironmentVariable(UserDataPaths.GammelOverrideVariable, null);

        AppSettings.Reload();
    }
}
