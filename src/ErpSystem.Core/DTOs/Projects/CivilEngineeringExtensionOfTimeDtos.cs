using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringExtensionOfTimeLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringExtensionOfTimeDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringExtensionOfTimeLookupsDto
{
    public IReadOnlyList<CivilEngineeringExtensionOfTimeLookupDto> Contracts { get; init; } = [];
    public IReadOnlyList<CivilEngineeringExtensionOfTimeLookupDto> QuantitySurveyVariations { get; init; } = [];
    public IReadOnlyList<CivilEngineeringExtensionOfTimeDocumentLookupDto> Documents { get; init; } = [];
    public bool RequireQuantitySurveyVariationForCostImpact { get; init; }
    public bool RequireFinanceBudgetRevalidation { get; init; }
    public bool RequireProcurementContractRevalidation { get; init; }
}

public sealed class CivilEngineeringExtensionOfTimeControlDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectExtensionOfTimeId { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string ScopeSummary { get; init; } = string.Empty;
    public Guid ContractId { get; init; }
    public string ContractLabel { get; init; } = string.Empty;
    public bool HasCostImpact { get; init; }
    public Guid? QuantitySurveyVariationOrderId { get; init; }
    public string? QuantitySurveyVariationLabel { get; init; }
    public int DaysRequested { get; init; }
    public int? DaysApproved { get; init; }
    public DateTime RequestedDate { get; init; }
    public DateTime? DecisionDate { get; init; }
    public DateTime? ProposedRevisedCompletionDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string? RejectionReason { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid EvidenceDocumentRecordId { get; init; }
    public Guid EvidenceDocumentVersionId { get; init; }
    public string EvidenceDocumentReference { get; init; } = string.Empty;
    public string EvidenceDocumentTitle { get; init; } = string.Empty;
    public string EvidenceVersionNumber { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CivilEngineeringExtensionOfTimeRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CreateCivilEngineeringExtensionOfTimeRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid ContractId { get; set; }
    public Guid? QuantitySurveyVariationOrderId { get; set; }
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 3)] public string ScopeSummary { get; set; } = string.Empty;
    [Range(1, 3650)] public int DaysRequested { get; set; }
    public DateTime? ProposedRevisedCompletionDate { get; set; }
    public bool HasCostImpact { get; set; }
    public Guid EvidenceDocumentRecordId { get; set; }
    public Guid EvidenceDocumentVersionId { get; set; }
}

public sealed class ReviewCivilEngineeringExtensionOfTimeRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Comment { get; set; } = string.Empty;
}
