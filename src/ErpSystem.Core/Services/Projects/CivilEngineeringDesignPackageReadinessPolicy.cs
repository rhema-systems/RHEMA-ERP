using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public sealed record CivilEngineeringGovernedPackageDocument(
    CivilEngineeringDesignDiscipline Discipline,
    CivilEngineeringDocumentStatus Status,
    Guid CentralDocumentVersionId,
    bool IsCurrentPublished,
    DateTime? ReviewedAt);

public sealed record CivilEngineeringPackageEvidence(
    string EvidenceType,
    Guid CentralDocumentVersionId);

public sealed record CivilEngineeringPackageReadinessResult(
    IReadOnlyList<string> Errors,
    DateTime? LatestGovernedApprovalAt)
{
    public bool IsReady => Errors.Count == 0;
}

public static class CivilEngineeringDesignPackageReadinessPolicy
{
    private static readonly string[] RequiredEvidenceTypes =
    [
        CivilEngineeringDesignEvidenceTypes.Design,
        CivilEngineeringDesignEvidenceTypes.Drawing,
        CivilEngineeringDesignEvidenceTypes.SubmissionPackage
    ];

    public static CivilEngineeringPackageReadinessResult Validate(
        IReadOnlyCollection<CivilEngineeringDesignDiscipline> requiredDisciplines,
        IReadOnlyCollection<CivilEngineeringGovernedPackageDocument> documents,
        IReadOnlyCollection<CivilEngineeringPackageEvidence> evidence)
    {
        var errors = new List<string>();
        var required = requiredDisciplines.Distinct().ToList();
        if (required.Count == 0)
            errors.Add("CIV-CFG-003 must configure at least one required design discipline.");

        var approved = documents.Where(value =>
                value.Status == CivilEngineeringDocumentStatus.Approved
                && value.IsCurrentPublished
                && value.ReviewedAt.HasValue)
            .ToList();
        foreach (var discipline in required.Where(discipline =>
                     approved.All(document => document.Discipline != discipline)))
            errors.Add($"Approve a current Published governed {DisciplineLabel(discipline)} engineering file before submitting the package to HOD.");

        foreach (var evidenceType in RequiredEvidenceTypes)
        {
            var selected = evidence.Where(value =>
                    string.Equals(value.EvidenceType?.Trim(), evidenceType, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (selected.Count == 0)
            {
                errors.Add($"Current {evidenceType} evidence is required before submitting the package to HOD.");
                continue;
            }

            if (selected.Any(item => approved.All(document =>
                    document.CentralDocumentVersionId != item.CentralDocumentVersionId)))
                errors.Add($"Every {evidenceType} evidence version must be approved in the governed engineering document register.");
        }

        var latestApproval = approved.Select(value => value.ReviewedAt)
            .Where(value => value.HasValue)
            .Select(value => value!.Value.ToUniversalTime())
            .DefaultIfEmpty()
            .Max();
        return new CivilEngineeringPackageReadinessResult(
            errors,
            latestApproval == default ? null : latestApproval);
    }

    private static string DisciplineLabel(CivilEngineeringDesignDiscipline discipline) => discipline switch
    {
        CivilEngineeringDesignDiscipline.TownPlanning => "Town Planning",
        _ => discipline.ToString()
    };
}
