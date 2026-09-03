namespace NoteApp.Core;

/// <summary>
/// Skifter hemmeligheder fra klartekst til beskyttet form — én gang.
/// </summary>
/// <remarks>
/// DEN KØRER VED HVER OPSTART OG GØR INTET, NÅR DER IKKE ER NOGET AT GØRE.
///
/// Det er med vilje: en migrering, der kun kører «den ene gang», skal vide,
/// om den har kørt — og den viden er en fil mere, der kan blive forkert. Her
/// er svaret i stedet skrevet i selve dataene: en beskyttet fil kendes på sit
/// mærke og bliver sprunget over.
///
/// Rækkefølgen betyder noget. Den nye fil skrives, før den gamle er væk, og
/// den gamle overskrives, før den slettes. Går strømmen midt i, står der
/// enten den gamle eller den nye hemmelighed — aldrig ingen af delene.
/// </remarks>
public static class Opstartsbeskyttelse
{
    /// <summary>Integrationer, der har en nøgle at beskytte.</summary>
    private static readonly string[] Integrationer = { "google", "google-opgaver" };

    /// <summary>
    /// Kører skiftet. Svarer hvor mange hemmeligheder der blev beskyttet.
    /// </summary>
    public static int Koer()
    {
        if (!Hemmelighed.Kan) return 0;

        var skiftet = 0;

        // MISTRAL-NOEGLEN ligger i sin egen fil og skiftes i sin helhed.
        try
        {
            if (Llm.SkyNoegle.Beskyt()) skiftet++;
        }
        catch (Exception)
        {
            // En noegle, der ikke kan skiftes, skal blive liggende laesbar.
        }

        // GOOGLES OPDATERINGSNOEGLER ligger som ét felt i en fil, hvor resten
        // ikke er hemmeligt. Der laeses og gemmes igennem Integrationsfiler,
        // som selv laaser feltet - saa en gemning er nok.
        foreach (var id in Integrationer)
        {
            try
            {
                var o = Integrationsfiler.Hent(id);

                if (o.Opdateringsnoegle.Length == 0) continue;
                if (!ErKlartekst(id)) continue;

                Integrationsfiler.Gem(id, o);
                skiftet++;
            }
            catch (Exception)
            {
                // Se ovenfor: en integration, der ikke kan skiftes, bliver
                // liggende som den er, og virker videre.
            }
        }

        if (skiftet > 0) Skrivned(skiftet);

        return skiftet;
    }

    /// <summary>Står nøglen stadig i klartekst på disken?</summary>
    /// <remarks>
    /// Der ses på FILEN og ikke på det indlæste objekt: Hent åbner allerede
    /// feltet, så et beskyttet og et ubeskyttet felt ser ens ud bagefter.
    /// </remarks>
    private static bool ErKlartekst(string id)
    {
        try
        {
            var sti = Path.Combine(UserDataPaths.Root, $"integration-{id}.json");
            if (!File.Exists(sti)) return false;

            return !File.ReadAllText(sti, System.Text.Encoding.UTF8)
                        .Contains(Hemmelighed.Maerke, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Skriver det i historikken — uden at nævne hvad der blev beskyttet.
    /// </summary>
    /// <remarks>
    /// ANTALLET, IKKE NAVNENE OG ALDRIG VÆRDIERNE. En linje, der siger «din
    /// Google-nøgle blev flyttet», fortæller en, der læser over skulderen,
    /// hvor der er noget at hente. Tallet er nok til at kunne se, at det
    /// skete.
    /// </remarks>
    private static void Skrivned(int antal)
    {
        try
        {
            Historik.Skriv(HaendelseType.Andet, "Hemmeligheder beskyttet",
                $"{antal} gemt med Windows' egen brugerbeskyttelse i stedet for klartekst.",
                Udfald.Fuldført);
        }
        catch (Exception)
        {
            // En historik, der ikke kan skrives, maa ikke vaelte opstarten.
        }
    }
}
