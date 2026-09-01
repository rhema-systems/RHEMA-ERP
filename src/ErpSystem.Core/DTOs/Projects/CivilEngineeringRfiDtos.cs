using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringRfiDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringRfiLookupsDto
{
    public Guid ProjectEngineerAssignmentId { get; init; }
    public string ProjectEngineerName { get; init; } = string.Empty;
    public Guid ProjectManagerUserId { get; init; }
    public string ProjectManagerName { get; init; } = string.Empty;
    public bool RequiresDmsEvidence { get; init; }
    public IReadOnlyList<CivilEngineeringRfiDocumentLookupDto> Documents { get; init; } = [];
}

public sealed class CivilEngineeringRfiEvidenceDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string EvidenceRole { get; init; } = string.Empty;
    public string DocumentReference { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringRfiResponseDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public string ResponseText { get; init; } = string.Empty;
    public Guid RespondedByUserId { get; init; }
    public string RespondedByName { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
}

public sealed class CivilEngineeringRfiRoutingDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectRfiId { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public DateTime RaisedDate { get; init; }
    public DateTime? ResponseDueDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid ProjectEngineerAssignmentId { get; init; }
    public string ProjectEngineerName { get; init; } = string.Empty;
    public Guid ProjectManagerUserId { get; init; }
    public string ProjectManagerName { get; init; } = string.Empty;
    public string ExternalBusinessPartnerName { get; init; } = string.Empty;
    public Guid? WorkflowInstanceId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringRfiEvidenceDto> Evidence { get; init; } = [];
    public IReadOnlyList<CivilEngineeringRfiResponseDto> Responses { get; init; } = [];
}

public sealed class CivilEngineeringRfiEvidenceRequest
{
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class CreateCivilEngineeringRfiRequest
{
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(100)] public string ReferenceNumber { get; set; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 3)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Question { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Priority { get; set; } = "Medium";
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    [MinLength(1)] public List<CivilEngineeringRfiEvidenceRequest> Evidence { get; set; } = [];
}

public sealed class SubmitCivilEngineeringRfiResponseRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string ResponseText { get; set; } = string.Empty;
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class ProcessCivilEngineeringRfiResponseRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
}
