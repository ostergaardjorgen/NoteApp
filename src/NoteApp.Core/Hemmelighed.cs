using System.Security.Cryptography;
using System.Text;

namespace NoteApp.Core;

/// <summary>
/// Gemmer en hemmelighed, så kun den Windows-bruger, der gemte den, kan læse
/// den igen.
/// </summary>
/// <remarks>
/// EN API-NØGLE I KLARTEKST ER EN NØGLE, ALLE PÅ MASKINEN HAR.
///
/// Nøglen til Mistral og Googles opdateringsnøgler lå som ren tekst i
/// datamappen. Filerne ligger uden for git og uden for OneDrive, og det er
/// rigtigt — men det beskytter mod at DELE dem ved et uheld, ikke mod at
/// nogen læser dem. Enhver proces, der kører som brugeren, kunne åbne dem;
/// det samme kunne enhver, der fik fat i en sikkerhedskopi af mappen.
///
/// DPAPI binder teksten til Windows-brugerens egen nøgle. Kopieres filen til
/// en anden maskine eller en anden konto, kan den ikke læses — heller ikke
/// af en administrator på den maskine.
///
/// HVAD DEN IKKE BESKYTTER MOD. Kører der ondsindet kode SOM brugeren, kan
/// den kalde det samme kald og få klarteksten. Det er ikke en mangel her; det
/// er grænsen for, hvad DPAPI er. Alternativet — at bede om en adgangskode,
/// hver gang appen starter — ville ingen bruge, og en funktion, ingen bruger,
/// beskytter ingenting.
///
/// UDEN FOR WINDOWS falder den tilbage til klartekst frem for at fejle.
/// Prøverne kører også på andre systemer, og en app, der ikke kan starte,
/// er værre end en, der beskytter mindre end den kunne.
/// </remarks>
public static class Hemmelighed
{
    /// <summary>Sådan kan en beskyttet fil kendes fra en gammel klartekstfil.</summary>
    /// <remarks>
    /// Der skal kunne skelnes UDEN at gætte: en base64-streng og en API-nøgle
    /// ligner hinanden nok til, at en gætteregel ville tage fejl før eller
    /// siden. Mærket står forrest og siger det.
    /// </remarks>
    public const string Maerke = "DPAPI1:";

    /// <summary>Kan hemmeligheder beskyttes på det her system?</summary>
    public static bool Kan => OperatingSystem.IsWindows();

    /// <summary>
    /// Læser en hemmelighed — beskyttet eller gammel klartekst.
    /// </summary>
    /// <remarks>
    /// EN GAMMEL FIL SKAL STADIG KUNNE LÆSES. Brugeren har forbundet sine
    /// tjenester én gang og skal ikke gøre det igen, fordi lagringen blev
    /// bedre. Se <see cref="Skift"/> for, hvordan den skiftes over.
    /// </remarks>
    public static string? Laes(string sti)
    {
        try
        {
            if (!File.Exists(sti)) return null;

            var raa = File.ReadAllText(sti, Encoding.UTF8).Trim();
            if (raa.Length == 0) return null;

            if (!raa.StartsWith(Maerke, StringComparison.Ordinal)) return raa;

            return Aabn(raa[Maerke.Length..]);
        }
        catch (IOException)
        {
            // En ulaeselig fil maa ikke vaelte noget. Kalderen faar null og
            // siger det samme, som hvis der slet ingen hemmelighed var.
            return null;
        }
        catch (CryptographicException)
        {
            // Filen er beskyttet for en ANDEN bruger eller maskine. Det er
            // ikke en fejl i appen - det er beskyttelsen, der virker.
            return null;
        }
        catch (FormatException)
        {
            // Maerket staar der, men resten er ikke base64 - filen er
            // oedelagt. Fundet af proeven, ikke i drift: uden det her ville en
            // halvskrevet fil vaelte appen ved opstart, hvor der laeses.
            //
            // Null betyder «ingen hemmelighed», og saa siger appen det samme,
            // som hvis der aldrig havde vaeret en. Det er det aerlige svar:
            // noeglen ER vaek, og brugeren skal saette den ind igen.
            return null;
        }
    }

    /// <summary>
    /// Skriver en hemmelighed beskyttet. Tom værdi sletter filen.
    /// </summary>
    /// <remarks>
    /// ATOMISK, som alt andet der skrives i datamappen: kladde først, så
    /// byttes filerne. Går strømmen midt i, står der enten den gamle eller
    /// den nye hemmelighed — aldrig en halv.
    /// </remarks>
    public static void Skriv(string sti, string? hemmelighed)
    {
        if (string.IsNullOrWhiteSpace(hemmelighed))
        {
            Slet(sti);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(sti)!);

        var indhold = Kan
            ? Maerke + Luk(hemmelighed.Trim())
            : hemmelighed.Trim();

        var kladde = sti + ".kladde";

        File.WriteAllText(kladde, indhold, new UTF8Encoding(false));

        if (File.Exists(sti)) File.Replace(kladde, sti, null);
        else File.Move(kladde, sti);
    }

    /// <summary>
    /// Skifter en klartekstfil over til beskyttet form, én gang.
    /// </summary>
    /// <remarks>
    /// DEN GAMLE TEKST SKAL OVERSKRIVES, IKKE BARE ERSTATTES. En fil, der
    /// slettes, ligger stadig på disken, til pladsen bruges igen — og en
    /// API-nøgle, der kan graves op af et slettet område, er ikke væk.
    /// Derfor skrives der hen over indholdet først.
    ///
    /// Det er ikke en garanti på et moderne SSD, hvor kontrolleren selv
    /// bestemmer, hvor blokke lander. Det siges derfor som det er i
    /// dokumentationen frem for at love mere.
    ///
    /// Svarer sandt, hvis der faktisk blev skiftet noget.
    /// </remarks>
    public static bool Skift(string sti)
    {
        if (!Kan || !File.Exists(sti)) return false;

        string raa;

        try
        {
            raa = File.ReadAllText(sti, Encoding.UTF8).Trim();
        }
        catch (IOException)
        {
            return false;
        }

        if (raa.Length == 0 || raa.StartsWith(Maerke, StringComparison.Ordinal)) return false;

        // Den nye fil skrives FOERST. Gaar noget galt undervejs, er den gamle
        // stadig laesbar - en migrering maa ikke kunne koste adgangen.
        Skriv(sti, raa);

        return true;
    }

    /// <summary>Sletter en hemmelighed og skriver hen over det, der stod.</summary>
    public static void Slet(string sti)
    {
        try
        {
            if (!File.Exists(sti)) return;

            var laengde = (int)new FileInfo(sti).Length;
            if (laengde > 0)
                File.WriteAllBytes(sti, new byte[laengde]);

            File.Delete(sti);
        }
        catch (IOException)
        {
            // Kan filen ikke slettes, er der ikke mere at goere her.
        }
    }

    private static string Luk(string klartekst)
    {
        if (!Kan) return klartekst;

        var beskyttet = System.Security.Cryptography.ProtectedData.Protect(
            Encoding.UTF8.GetBytes(klartekst), null, DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(beskyttet);
    }

    private static string? Aabn(string base64)
    {
        if (!Kan) return base64;

        var klar = System.Security.Cryptography.ProtectedData.Unprotect(
            Convert.FromBase64String(base64), null, DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(klar);
    }

    /// <summary>
    /// Låser ét felt inde i en fil, der ellers ikke er hemmelig.
    /// </summary>
    /// <remarks>
    /// Integrationsfilerne indeholder én hemmelighed og en håndfuld
    /// oplysninger, der ikke er det: hvornår der sidst blev hentet, hvor
    /// mange, den sidste fejl. De skal kunne læses, også når nøglen ikke kan
    /// — ellers ville en fil fra en anden maskine se ud som «aldrig sat op»
    /// i stedet for «nøglen kan ikke bruges her».
    /// </remarks>
    public static string Lukfelt(string? vaerdi)
    {
        if (string.IsNullOrWhiteSpace(vaerdi)) return "";
        if (vaerdi.StartsWith(Maerke, StringComparison.Ordinal)) return vaerdi;

        return Kan ? Maerke + Luk(vaerdi.Trim()) : vaerdi.Trim();
    }

    /// <summary>Åbner et felt låst med <see cref="Lukfelt"/>. Gammel klartekst går uændret igennem.</summary>
    public static string Aabnfelt(string? vaerdi)
    {
        if (string.IsNullOrWhiteSpace(vaerdi)) return "";
        if (!vaerdi.StartsWith(Maerke, StringComparison.Ordinal)) return vaerdi;

        try
        {
            return Aabn(vaerdi[Maerke.Length..]) ?? "";
        }
        catch (CryptographicException)
        {
            // Laast for en anden bruger eller maskine. Det er ikke en fejl -
            // det er beskyttelsen, der virker. Resten af filen laeses stadig.
            return "";
        }
        catch (FormatException)
        {
            return "";
        }
    }

    /// <summary>
    /// En hemmelighed, som den må stå i en log, en fejl eller på skærmen.
    /// </summary>
    /// <remarks>
    /// ALDRIG HELE NØGLEN. En fejlbesked havner i en historik, et
    /// skærmbillede eller en supportmail, og en nøgle, der er set af én, der
    /// ikke skulle se den, skal skiftes.
    ///
    /// De fire sidste tegn er nok til at kende to nøgler fra hinanden — «er
    /// det den, jeg lige satte ind?» — og for lidt til at bruge til noget.
    /// </remarks>
    public static string Maskeret(string? hemmelighed)
    {
        if (string.IsNullOrWhiteSpace(hemmelighed)) return "(ingen)";

        var s = hemmelighed.Trim();

        return s.Length <= 8 ? "…" : "… " + s[^4..];
    }
}
