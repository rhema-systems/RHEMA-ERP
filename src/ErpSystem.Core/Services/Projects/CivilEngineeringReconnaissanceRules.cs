namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringReconnaissanceStatuses
{
    public const string Draft = "Draft";
    public const string Completed = "Completed";
}

public enum CivilEngineeringReconnaissanceItemKind
{
    Constraint = 0,
    InformationSource = 1,
    Photo = 2
}

public enum CivilEngineeringConstraintCategory
{
    Access = 0,
    Topography = 1,
    Drainage = 2,
    Soil = 3,
    Utilities = 4,
    Environment = 5,
    Boundary = 6,
    ExistingStructure = 7,
    Safety = 8,
    Regulatory = 9
}

public enum CivilEngineeringConstraintSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum CivilEngineeringConstraintResolutionStatus
{
    Open = 0,
    Mitigated = 1,
    Accepted = 2
}

public sealed record CivilEngineeringReconnaissanceItemFacts(
    CivilEngineeringReconnaissanceItemKind Kind,
    Guid? InformationSourceSectionId,
    Guid? CentralDocumentRecordId,
    Guid? CentralDocumentVersionId,
    CivilEngineeringConstraintCategory? ConstraintCategory,
    CivilEngineeringConstraintSeverity? Severity,
    CivilEngineeringConstraintResolutionStatus? ResolutionStatus,
    bool BlocksDesign);

public static class CivilEngineeringReconnaissanceRules
{
    public static void EnsureMutableStage(string stage)
    {
        if (!string.Equals(stage, CivilEngineeringDesignStages.SceInformationGathering, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Site reconnaissance can be amended only while the design case is gathering information.");
    }

    public static void EnsureValidItems(
        IReadOnlyCollection<CivilEngineeringReconnaissanceItemFacts> items,
        bool completing)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Add at least one reconnaissance item.");

        foreach (var item in items)
        {
            var hasDocument = item.CentralDocumentRecordId is { } recordId && recordId != Guid.Empty
                              && item.CentralDocumentVersionId is { } versionId && versionId != Guid.Empty;
            switch (item.Kind)
            {
                case CivilEngineeringReconnaissanceItemKind.Constraint:
                    if (!item.ConstraintCategory.HasValue || !item.Severity.HasValue || !item.ResolutionStatus.HasValue)
                        throw new InvalidOperationException(
                            "Every constraint requires a controlled category, severity and resolution status.");
                    if (item.InformationSourceSectionId.HasValue || hasDocument)
                        throw new InvalidOperationException(
                            "Constraint items cannot impersonate a section input or DMS photo.");
                    break;
                case CivilEngineeringReconnaissanceItemKind.InformationSource:
                    if (item.InformationSourceSectionId is not { } sectionId || sectionId == Guid.Empty || !hasDocument)
                        throw new InvalidOperationException(
                            "Every information source must select an active section and a current Published DMS version.");
                    if (item.ConstraintCategory.HasValue || item.Severity.HasValue || item.ResolutionStatus.HasValue || item.BlocksDesign)
                        throw new InvalidOperationException("Information sources cannot contain constraint fields.");
                    break;
                case CivilEngineeringReconnaissanceItemKind.Photo:
                    if (!hasDocument)
                        throw new InvalidOperationException(
                            "Every reconnaissance photo must select a current Published DMS version.");
                    if (item.InformationSourceSectionId.HasValue || item.ConstraintCategory.HasValue
                        || item.Severity.HasValue || item.ResolutionStatus.HasValue || item.BlocksDesign)
                        throw new InvalidOperationException("Photo items cannot contain section or constraint fields.");
                    break;
                default:
                    throw new InvalidOperationException("The reconnaissance item type is not supported.");
            }
        }

        if (!completing) return;
        if (!items.Any(item => item.Kind == CivilEngineeringReconnaissanceItemKind.Photo))
            throw new InvalidOperationException("At least one current Published DMS photo is required before completion.");
        if (!items.Any(item => item.Kind == CivilEngineeringReconnaissanceItemKind.InformationSource))
            throw new InvalidOperationException("At least one controlled information source is required before completion.");
        if (items.Any(item => item.Kind == CivilEngineeringReconnaissanceItemKind.Constraint
                              && item.BlocksDesign
                              && item.ResolutionStatus == CivilEngineeringConstraintResolutionStatus.Open))
            throw new InvalidOperationException(
                "Resolve or formally accept every design-blocking constraint before completing reconnaissance.");
    }
}
