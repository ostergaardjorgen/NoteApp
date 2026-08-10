namespace NoteApp.Core;

public sealed record IndustryTemplate(string Id, string Name, string Description, IReadOnlyList<(string Term, string Category)> Terms)
{
    public int Count => Terms.Count;
}

/// <summary>
/// Startordforråd pr. fagområde.
///
/// Findes, fordi en tom ordbog gør de første møder dårligere end nødvendigt:
/// Whisper staver fagtermer forkert, indtil nogen har fortalt den, hvad de
/// hedder. Skabelonen er et forspring, ikke et facit — den skal rettes til,
/// og appen lærer resten af brugerens egne rettelser.
///
/// Navne er med vilje IKKE med i nogen skabelon. Kollegaer og kunder er dem,
/// Whisper oftest staver forkert, og dem kan kun brugeren selv indtaste.
/// </summary>
public static class IndustryTemplates
{
    public static readonly IReadOnlyList<IndustryTemplate> All = new[]
    {
        new IndustryTemplate("iam", "IT-sikkerhed og adgangsstyring",
            "Identitet, rettigheder, compliance. Entra ID, SCIM, attestering.",
            new (string, string)[]
            {
                ("Entra ID", TermCategories.Produkt),
                ("Active Directory", TermCategories.Produkt),
                ("Exchange Online", TermCategories.Produkt),
                ("Microsoft Graph", TermCategories.Produkt),
                ("MitID Erhverv", TermCategories.Produkt),
                ("SCIM", TermCategories.Forkortelse),
                ("IAM", TermCategories.Forkortelse),
                ("SSO", TermCategories.Forkortelse),
                ("MFA", TermCategories.Forkortelse),
                ("PIM", TermCategories.Forkortelse),
                ("NIS2", TermCategories.Forkortelse),
                ("provisionering og deprovisionering", TermCategories.Fagterm),
                ("rettighedsstyring", TermCategories.Fagterm),
                ("attestering", TermCategories.Fagterm),
                ("adgangsafstemning", TermCategories.Fagterm),
                ("onboarding og offboarding", TermCategories.Fagterm),
                ("funktionsroller", TermCategories.Fagterm),
                ("privilegerede roller", TermCategories.Fagterm),
                ("sikkerhedsgrupper", TermCategories.Fagterm),
                ("tenant", TermCategories.Fagterm)
            }),

        new IndustryTemplate("softwareudvikling", "Softwareudvikling",
            "Sprint, deployment, pull requests, teknisk gæld.",
            new (string, string)[]
            {
                ("pull request", TermCategories.Fagterm),
                ("deployment", TermCategories.Fagterm),
                ("staging", TermCategories.Fagterm),
                ("produktionsmiljø", TermCategories.Fagterm),
                ("teknisk gæld", TermCategories.Fagterm),
                ("refaktorering", TermCategories.Fagterm),
                ("regressionstest", TermCategories.Fagterm),
                ("backlog", TermCategories.Fagterm),
                ("sprintplanlægning", TermCategories.Fagterm),
                ("retrospektiv", TermCategories.Fagterm),
                ("API", TermCategories.Forkortelse),
                ("CI/CD", TermCategories.Forkortelse),
                ("SLA", TermCategories.Forkortelse),
                ("MVP", TermCategories.Forkortelse)
            }),

        new IndustryTemplate("projektledelse", "Projekt- og porteføljeledelse",
            "Styregruppe, milepæle, risici, leverancer.",
            new (string, string)[]
            {
                ("styregruppe", TermCategories.Fagterm),
                ("milepæl", TermCategories.Fagterm),
                ("risikolog", TermCategories.Fagterm),
                ("leveranceplan", TermCategories.Fagterm),
                ("interessentanalyse", TermCategories.Fagterm),
                ("business case", TermCategories.Fagterm),
                ("gevinstrealisering", TermCategories.Fagterm),
                ("scope-ændring", TermCategories.Fagterm),
                ("ressourceallokering", TermCategories.Fagterm),
                ("statusrapportering", TermCategories.Fagterm)
            }),

        new IndustryTemplate("oekonomi", "Økonomi og administration",
            "Budget, periodisering, revision, kontoplan.",
            new (string, string)[]
            {
                ("periodisering", TermCategories.Fagterm),
                ("kontoplan", TermCategories.Fagterm),
                ("debitorer", TermCategories.Fagterm),
                ("kreditorer", TermCategories.Fagterm),
                ("afstemning", TermCategories.Fagterm),
                ("budgetopfølgning", TermCategories.Fagterm),
                ("revisionsprotokol", TermCategories.Fagterm),
                ("momsafregning", TermCategories.Fagterm),
                ("anlægsaktiver", TermCategories.Fagterm),
                ("likviditetsbudget", TermCategories.Fagterm)
            }),

        new IndustryTemplate("ingen", "Ingen skabelon",
            "Start med tom ordbog og fyld selv ord i, efterhånden som de dukker op.",
            Array.Empty<(string, string)>())
    };

    public static IndustryTemplate? ById(string id) =>
        All.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Lægger skabelonens ord i ordbogen. Findes et ord i forvejen, opdateres
    /// det frem for at give en dublet — man skal kunne vælge en skabelon mere
    /// uden at ødelægge det, der allerede står der.
    /// </summary>
    public static int Apply(LearningStore store, IndustryTemplate template)
    {
        foreach (var (term, kategori) in template.Terms)
            store.AddTerm(term, kategori);
        return template.Count;
    }

    /// <summary>
    /// Læser navne fra et fritekstfelt — ét pr. linje eller adskilt af komma.
    /// Navne vejer 2,0, saa de altid vaelges foerst til Whispers prompt.
    /// </summary>
    public static int AddNames(LearningStore store, string text, string category = TermCategories.Person)
    {
        var navne = text
            .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var n in navne) store.AddTerm(n, category, weight: 2.0);
        return navne.Count;
    }
}
