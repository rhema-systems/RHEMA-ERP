using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringDevelopmentApprovalLookupOptionDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDevelopmentApprovalDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDevelopmentApprovalLookupsDto
{
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalLookupOptionDto> Applicants { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalLookupOptionDto> Projects { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalLookupOptionDto> Properties { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalDocumentLookupDto> Documents { get; init; } = [];
}

public sealed class CivilEngineeringDevelopmentApprovalEvidenceRequest
{
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class CreateCivilEngineeringDevelopmentApprovalFileRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid? ApplicantBusinessPartnerId { get; set; }
    [StringLength(250)] public string? ApplicantName { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EstateManagedAssetId { get; set; }
    [Required, StringLength(120, MinimumLength = 3)] public string ApplicationReference { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public DateTime? SiteInspectionDueDate { get; set; }
    [MinLength(1)] public List<CivilEngineeringDevelopmentApprovalEvidenceRequest> ApplicationEvidence { get; set; } = [];
}

public sealed class RecordCivilEngineeringSiteInspectionRequest
{
    public Guid ClientRequestId { get; set; }
    public DateTime SiteInspectedAt { get; set; }
    [MinLength(1)] public List<CivilEngineeringDevelopmentApprovalEvidenceRequest> Evidence { get; set; } = [];
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CivilEngineeringDevelopmentApprovalEvidenceDto
{
    public CivilEngineeringDevelopmentApprovalEvidenceKind Kind { get; init; }
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string? DocumentReference { get; init; }
}

public sealed class CivilEngineeringDevelopmentApprovalFileDto
{
    public Guid Id { get; init; }
    public string FileNumber { get; init; } = string.Empty;
    public Guid? ApplicantBusinessPartnerId { get; init; }
    public string ApplicantName { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public string ProjectLabel { get; init; } = string.Empty;
    public Guid EstateManagedAssetId { get; init; }
    public string PropertyLabel { get; init; } = string.Empty;
    public string ApplicationReference { get; init; } = string.Empty;
    public string CurrentSection { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
    public CivilEngineeringDevelopmentApprovalFileStatus Status { get; init; }
    public DateTime? SiteInspectionDueDate { get; init; }
    public DateTime? SiteInspectedAt { get; init; }
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalEvidenceDto> Evidence { get; init; } = [];
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDevelopmentApprovalFileRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
