namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// What a performance definition is used by (performance closure E-g1, D-78). Every delete is soft, so no foreign key
/// refused one: a grade, KPI, competency, library item, company goal or unit goal deleted while something pointed at it
/// took that thing's meaning with it — a band dropped from generation and the forms, an evidence rule that stopped
/// applying, a check-in's objective link that vanished. So a definition in use is not deleted, and the fields that
/// decide results do not change underneath what uses it; its name and description stay editable. The refusals are
/// <see cref="InvalidOperationException"/>s, which the controllers answer 422 with the reason.
/// </summary>
public static class DefinitionUse
{
    /// <summary>One kind of use: how many, and how one and several are named.</summary>
    public readonly record struct Use(int Count, string One, string Many);

    /// <summary>"bands on 3 template criteria and an appraisal", or null when nothing uses it.</summary>
    public static string? Describe(params Use[] uses)
    {
        var parts = uses.Where(u => u.Count > 0)
            .Select(u => u.Count == 1 ? u.One : $"{u.Count} {u.Many}")
            .ToList();
        return parts.Count switch
        {
            0 => null,
            1 => parts[0],
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
        };
    }

    /// <summary>The refusal of a delete, naming what uses the definition.</summary>
    public static InvalidOperationException DeleteRefused(string what, string name, string uses, string? instead = null)
        => new($"The {what} \"{name}\" is used by {uses}, so it cannot be deleted.{(instead == null ? "" : " " + instead)}");

    /// <summary>The refusal of a change to a field that decides results, naming what uses the definition.</summary>
    public static InvalidOperationException ChangeRefused(string what, string name, string uses, string fields)
        => new($"The {what} \"{name}\" is used by {uses}, so its {fields} cannot change — that would change what they "
             + $"already mean. Its name and description can; for something different, create a new {what}.");
}
