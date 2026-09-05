using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringPlanningGisLookupOptionDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringPlanningGisDocumentLookupDto
{
    public Guid DevelopmentApprovalFileId { get; init; }
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringPlanningGisLookupsDto
{
    public Guid EstateManagedAssetId { get; init; }
    public string EstateManagedAssetLabel { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringPlanningGisLookupOptionDto> DevelopmentApprovalFiles { get; init; } = [];
    public IReadOnlyList<CivilEngineeringPlanningGisLookupOptionDto> PlanningConditions { get; init; } = [];
    public IReadOnlyList<CivilEngineeringPlanningGisLookupOptionDto> DevelopmentConstraints { get; init; } = [];
    public IReadOnlyList<CivilEngineeringPlanningGisLookupOptionDto> LandUseImpacts { get; init; } = [];
    public IReadOnlyList<CivilEngineeringPlanningGisDocumentLookupDto> EvidenceDocuments { get; init; } = [];
    public IReadOnlyList<CivilEngineeringLayoutConformity> LayoutConformities { get; init; } = [];
}

public sealed class CivilEngineeringPlanningGisValidationDto
{
    public Guid Id { get; init; }
    public Guid DesignCaseId { get; init; }
    public Guid EstateManagedAssetId { get; init; }
    public string EstateManagedAssetLabel { get; init; } = string.Empty;
    public Guid DevelopmentApprovalFileId { get; init; }
    public string DevelopmentApprovalFileLabel { get; init; } = string.Empty;
    public Guid PlanningConditionId { get; init; }
    public string PlanningConditionLabel { get; init; } = string.Empty;
    public Guid DevelopmentConstraintId { get; init; }
    public string DevelopmentConstraintLabel { get; init; } = string.Empty;
    public Guid LandUseImpactId { get; init; }
    public string LandUseImpactLabel { get; init; } = string.Empty;
    public CivilEngineeringLayoutConformity LayoutConformity { get; init; }
    public string SpatialReference { get; init; } = string.Empty;
    public string? BoundaryCoordinates { get; init; }
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string EvidenceReference { get; init; } = string.Empty;
    public CivilEngineeringPlanningGisValidationStatus Status { get; init; }
    public Guid PreparedByUserId { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public Guid? ReviewedByUserId { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateCivilEngineeringPlanningGisValidationRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid EstateManagedAssetId { get; set; }
    public Guid DevelopmentApprovalFileId { get; set; }
    public Guid PlanningConditionId { get; set; }
    public Guid DevelopmentConstraintId { get; set; }
    public Guid LandUseImpactId { get; set; }
    public CivilEngineeringLayoutConformity LayoutConformity { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public class CivilEngineeringPlanningGisSubmitRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CivilEngineeringPlanningGisDecisionRequest : CivilEngineeringPlanningGisSubmitRequest
{
    public CivilEngineeringPlanningGisValidationStatus Outcome { get; set; }
}

public sealed class CivilEngineeringPlanningGisValidationRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
