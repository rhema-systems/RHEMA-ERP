using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// What each shortlisting-criterion type is made of (round 3, lane K; register rows R-5, R-8;
/// decision D-7). ONE table, server-side: the value source (which catalogue, the gender enum,
/// numeric bounds or plain text), whether it may be mandatory, and whether it names a protected
/// characteristic. Served as <c>GET api/job-vacancies/criteria/shapes</c> so the criteria panel
/// stops carrying a hand-copied map that drifted from the engine — and read by
/// <c>JobVacancyService</c> to refuse what the panel would also refuse.
/// </summary>
public sealed class ShortlistingCriteriaShape
{
    public required JobShortlistingCriteriaType Type { get; init; }
    public required string Label { get; init; }

    /// <summary>What an accepted value refers to; null for numeric types, which carry bounds instead.</summary>
    public ShortlistingValueKind? ValueKind { get; init; }

    /// <summary>Several accepted values, matched with the mode and strategy.</summary>
    public bool IsList { get; init; }

    /// <summary>Min / max bounds and a comparison operator.</summary>
    public bool IsNumeric { get; init; }

    /// <summary>The engine reads a value for this type, so a blank one is refused on save.</summary>
    public bool RequiresValues { get; init; }

    /// <summary>
    /// Whether a criterion of this type may disqualify. Gender and Age (D-7) may inform, never
    /// disqualify; Other is not auto-evaluated and would disqualify nobody and everybody.
    /// </summary>
    public bool AllowsMandatory { get; init; } = true;

    /// <summary>Gender and Age — the vacancy is flagged when it carries one (D-7).</summary>
    public bool IsProtectedCharacteristic { get; init; }

    /// <summary>Whether the engine scores this type at all (Other is a person's judgement).</summary>
    public bool IsAutoEvaluated { get; init; } = true;

    /// <summary>Comparison operators the engine honours for this type; empty = none.</summary>
    public IReadOnlyList<ShortlistingComparisonOperator> Operators { get; init; } = Array.Empty<ShortlistingComparisonOperator>();

    public required string Hint { get; init; }

    /// <summary>Why a mandatory tick is refused, when it is.</summary>
    public string? MandatoryRefusal { get; init; }

    public ShortlistingCriteriaShapeDto ToDto() => new()
    {
        Type = Type,
        Label = Label,
        ValueKind = ValueKind,
        IsList = IsList,
        IsNumeric = IsNumeric,
        RequiresValues = RequiresValues,
        AllowsMandatory = AllowsMandatory,
        IsProtectedCharacteristic = IsProtectedCharacteristic,
        IsAutoEvaluated = IsAutoEvaluated,
        Operators = Operators.Select(o => o.ToString()).ToList(),
        Hint = Hint,
        MandatoryRefusal = MandatoryRefusal,
    };
}

public static class ShortlistingCriteriaShapes
{
    private static readonly ShortlistingComparisonOperator[] NumericOperators =
    {
        ShortlistingComparisonOperator.GreaterThanOrEqual,
        ShortlistingComparisonOperator.GreaterThan,
        ShortlistingComparisonOperator.LessThan,
        ShortlistingComparisonOperator.LessThanOrEqual,
        ShortlistingComparisonOperator.Equals,
        ShortlistingComparisonOperator.Between,
    };

    private const string ProtectedRefusal =
        "A Gender or Age criterion cannot be mandatory. It is a protected characteristic: it may inform a score, never disqualify a candidate (decision D-7).";

    public static readonly IReadOnlyList<ShortlistingCriteriaShape> All = new[]
    {
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.Qualification, Label = "Qualification",
            ValueKind = ShortlistingValueKind.Qualification, IsList = true, RequiresValues = true,
            Hint = "Pick the accepted qualifications from the catalogue; a candidate holding one of them matches by the catalogue row first, then by name.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.EducationLevel, Label = "Education level",
            ValueKind = ShortlistingValueKind.Qualification, IsList = true, RequiresValues = true,
            Hint = "Scored exactly like Qualification: the catalogue row first, then the name.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.Skill, Label = "Skill",
            ValueKind = ShortlistingValueKind.Skill, IsList = true, RequiresValues = true,
            Hint = "Pick the accepted skills from the catalogue; a candidate's catalogue-linked skill matches by row, a typed one by name.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.Certification, Label = "Certification",
            ValueKind = ShortlistingValueKind.Certification, IsList = true, RequiresValues = true,
            Hint = "Pick the accepted certifications from the catalogue. A candidate's certificate is matched by its name, so the catalogue name is what counts.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.Language, Label = "Language",
            ValueKind = ShortlistingValueKind.Language, IsList = true, RequiresValues = true,
            Hint = "Pick the accepted languages from the catalogue; a candidate's catalogue-linked language matches by row, a typed one by name.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.YearsOfExperience, Label = "Years of experience",
            IsNumeric = true, Operators = NumericOperators,
            Hint = "Between uses both bounds; the greater/less operators use one. A near miss on either side still scores partially.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.Age, Label = "Age",
            IsNumeric = true, Operators = NumericOperators,
            AllowsMandatory = false, IsProtectedCharacteristic = true, MandatoryRefusal = ProtectedRefusal,
            Hint = "Computed from the date of birth at scoring time. Never mandatory: a protected characteristic informs, it does not disqualify.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.Gender, Label = "Gender",
            ValueKind = ShortlistingValueKind.Gender, IsList = true, RequiresValues = true,
            AllowsMandatory = false, IsProtectedCharacteristic = true, MandatoryRefusal = ProtectedRefusal,
            Hint = "Tick the genders the role is open to. Never mandatory: a protected characteristic informs, it does not disqualify.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.Location, Label = "Location",
            ValueKind = ShortlistingValueKind.Text, IsList = true, RequiresValues = true,
            Operators = new[] { ShortlistingComparisonOperator.Contains, ShortlistingComparisonOperator.Equals },
            Hint = "Matched against the candidate's city. Contains is the default; Equals demands the whole city name. Several cities may be listed.",
        },
        new ShortlistingCriteriaShape
        {
            Type = JobShortlistingCriteriaType.Other, Label = "Other",
            ValueKind = ShortlistingValueKind.Text, IsList = true, RequiresValues = false,
            AllowsMandatory = false, IsAutoEvaluated = false,
            MandatoryRefusal = "An 'Other' criterion is not auto-evaluated and cannot be mandatory: it would disqualify nobody, or everybody.",
            Hint = "Not auto-evaluated. It contributes nothing to the score and is judged by a person off-system.",
        },
    };

    private static readonly Dictionary<JobShortlistingCriteriaType, ShortlistingCriteriaShape> ByType =
        All.ToDictionary(s => s.Type);

    /// <summary>The shape for a type, or null for a value that is not a member (the legacy 0 rows).</summary>
    public static ShortlistingCriteriaShape? Of(JobShortlistingCriteriaType type) =>
        ByType.TryGetValue(type, out var shape) ? shape : null;
}
