namespace NoteApp.Core;

/// <summary>Ét krav, og om maskinen her lever op til det.</summary>
/// <param name="Hvad">Kravet, som brugeren skal kunne læse det.</param>
/// <param name="Har">Det, maskinen faktisk har. Tom, hvis det ikke kan aflæses.</param>
/// <param name="Slags">Grønt, gult eller «det ved vi ikke».</param>
/// <param name="Hvorfor">Hvad kravet er der for. Vises under linjen.</param>
public sealed record Krav(string Hvad, string Har, Kravsvar Slags, string Hvorfor);

public enum Kravsvar
{
    /// <summary>Maskinen lever op til det.</summary>
    Opfyldt,

    /// <summary>Appen virker, men noget bliver langsomt eller er slået fra.</summary>
    Mangler,

    /// <summary>Kan ikke aflæses. Siges som det er.</summary>
    Ukendt
}

/// <summary>
/// Hvad maskinen skal kunne — og hvad DEN HER maskine har.
///
/// HVORFOR DET IKKE BARE ER EN LISTE
///
/// En liste over anbefalede specifikationer er noget, man læser én gang og
/// glemmer. Spørgsmålet, folk faktisk har, er «virker det på MIN maskine» —
/// og det kan appen svare på, fordi den kører på den.
///
/// DER KONKLUDERES IKKE «FOR LANGSOM». Appen kan optage og skrive ud på næsten
/// hvad som helst; forskellen er, hvor længe man venter. Et rødt kryds ville
/// sige, at noget ikke virker, og det ville være forkert.
///
/// KAN NOGET IKKE AFLÆSES, STÅR DER «ikke efterprøvet». Et gæt, der ser ud som
/// en måling, er værre end ingen oplysning.
/// </summary>
public static class Maskinkrav
{
    public static List<Krav> Alle()
    {
        var ud = new List<Krav>
        {
            Windows(),
            Hukommelse(),
            Grafikkort(),
            Plads(),
            Cppdelen()
        };

        return ud;
    }

    /// <summary>
    /// Microsofts C++-komponent, som Whisper er bygget med.
    /// </summary>
    /// <remarks>
    /// DEN HØRER TIL HER, FORDI DEN ER ET KRAV TIL MASKINEN OG IKKE EN
    /// INDSTILLING. Den mangler kun på en ny Windows, og når den mangler,
    /// virker udskrivningen slet ikke — det er den eneste linje på listen,
    /// hvor «mangler» betyder «virker ikke» og ikke «bliver langsomt».
    /// Se <see cref="Cppkomponent"/>.
    /// </remarks>
    private static Krav Cppdelen()
    {
        var mangler = Cppkomponent.Mangler(Motormappen());

        return new Krav(
            "Microsofts C++-komponent",
            mangler.Count == 0 ? "installeret" : "mangler",
            mangler.Count == 0 ? Kravsvar.Opfyldt : Kravsvar.Mangler,
            mangler.Count == 0
                ? "Den følger med de fleste programmer og er på plads her. Whisper er bygget med den."
                : "Uden den kan Whisper ikke starte, og optagelser kan ikke skrives ud. "
                  + $"Hent «{Cppkomponent.Navn}» hos Microsoft: {Cppkomponent.Hentesti}");
    }

    /// <summary>Mappen, whisper-cli ligger i — hvis den er hentet.</summary>
    private static string? Motormappen()
    {
        try { return Path.GetDirectoryName(WhisperInstall.Locate().WhisperCli); }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Windows 11, holdt opdateret.
    ///
    /// Windows 10 virker: optagelse, udskrift og søgning bruger ingenting,
    /// der kun findes i 11. Men to ting er bedre i 11 — vinduets titellinje
    /// kan få appens farve (build 22000 og frem), og Windows' egen liste over
    /// programmer, der bruger mikrofonen, er mere retvisende. Og Windows 10
    /// får ikke længere sikkerhedsopdateringer, hvilket er et større problem
    /// end noget i den her app.
    /// </summary>
    private static Krav Windows()
    {
        var v = Environment.OSVersion.Version;
        var elleve = v.Build >= 22000;

        return new Krav(
            "Windows 11, holdt opdateret",
            $"Windows {(elleve ? "11" : "10")} (build {v.Build})",
            elleve ? Kravsvar.Opfyldt : Kravsvar.Mangler,
            elleve
                ? "Opdater den, som Windows selv foreslår. Sikkerhedsopdateringer betyder mere for dine optagelser end noget, appen kan gøre."
                : "Appen virker på Windows 10, men Microsoft udsender ikke længere sikkerhedsopdateringer til den. Optagelserne ligger på maskinen — den skal kunne holdes lukket.");
    }

    /// <summary>
    /// 16 GB RAM anbefalet, 8 GB nok til optagelse og udskrift.
    ///
    /// Målt: en sprogmodel på 14 GB kan IKKE køre på en maskine med 32 GB —
    /// under kørslen bruges 5,8 GB på kortet og 14,1 GB i RAM. Det er derfor,
    /// den lokale opsummering kører på en lille model.
    /// </summary>
    private static Krav Hukommelse()
    {
        var gb = Gigabyte();

        if (gb <= 0)
            return new Krav("16 GB RAM", "", Kravsvar.Ukendt,
                "Kunne ikke aflæses på den her maskine.");

        return new Krav(
            "16 GB RAM anbefalet",
            $"{gb:0} GB",
            gb >= 15 ? Kravsvar.Opfyldt : Kravsvar.Mangler,
            gb >= 15
                ? "Rigeligt til optagelse, udskrift og en lokal sprogmodel."
                : "Optagelse og udskrift virker. Den lokale opsummering bliver langsom, og en større sprogmodel kan ikke køre.");
    }

    /// <summary>
    /// Et NVIDIA-kort med mindst 4 GB.
    ///
    /// Målt på et RTX 2060: femten minutters lyd tager fire et halvt minut med
    /// whisper large-v3. Uden kort kører den på processoren, og så tager den
    /// længere tid end mødet selv varede.
    ///
    /// Kortets hukommelse aflæses IKKE. Windows oplyser den upålideligt for
    /// kort med delt hukommelse, og et forkert tal ville være værre end
    /// ingenting.
    /// </summary>
    private static Krav Grafikkort()
    {
        var har = EngineInstaller.HasNvidiaGpu();

        return new Krav(
            "NVIDIA-grafikkort med mindst 4 GB",
            har ? "NVIDIA-kort fundet" : "intet NVIDIA-kort fundet",
            har ? Kravsvar.Opfyldt : Kravsvar.Mangler,
            har
                ? "Målt på et RTX 2060: femten minutters lyd skrives ud på fire et halvt minut. Kortets hukommelse aflæses ikke — Windows oplyser den upålideligt."
                : "Uden kort kører udskriften på processoren, og så tager den længere tid end mødet varede. Optagelse virker uændret; det er ventetiden, der bliver lang.");
    }

    /// <summary>
    /// Plads til modeller og optagelser.
    ///
    /// Målt: whisper large-v3 fylder 2,9 GB, og en times møde fylder 220 MB
    /// som to spor ukomprimeret lyd. Femten GB rækker til modellerne og et par
    /// måneders møder.
    /// </summary>
    private static Krav Plads()
    {
        try
        {
            var drev = new DriveInfo(Path.GetPathRoot(UserDataPaths.Root)!);
            var fri = drev.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

            return new Krav(
                "15 GB fri plads",
                $"{fri:0} GB fri på {drev.Name}",
                fri >= 15 ? Kravsvar.Opfyldt : Kravsvar.Mangler,
                "Modellerne fylder op til 4,5 GB. En times møde fylder 220 MB som lyd og 56 KB som tekst — det er lyden, der fylder, og den kan slettes, når teksten er i hus.");
        }
        catch (Exception)
        {
            return new Krav("15 GB fri plads", "", Kravsvar.Ukendt,
                "Kunne ikke aflæses.");
        }
    }

    private static double Gigabyte()
    {
        try
        {
            using var søger = new System.Management.ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");

            foreach (var enhed in søger.Get())
                if (enhed["TotalPhysicalMemory"] is { } v)
                    return Convert.ToDouble(v) / 1024 / 1024 / 1024;
        }
        catch (Exception)
        {
            // Kan den ikke aflaeses, siges det - der gaettes ikke.
        }

        return 0;
    }
}
