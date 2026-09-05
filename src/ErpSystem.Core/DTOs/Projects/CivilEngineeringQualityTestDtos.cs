using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringQualityTestDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringQualityTestLookupOptionDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringQualityTestCategoryLookupDto
{
    public CivilEngineeringQualityTestCategory Value { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringQualityTestLookupsDto
{
    public IReadOnlyList<CivilEngineeringQualityTestCategoryLookupDto> TestCategories { get; init; } = [];
    public IReadOnlyList<string> SourceTypes { get; init; } = [];
    public IReadOnlyList<string> ResultStatuses { get; init; } = [];
    public IReadOnlyList<CivilEngineeringQualityTestLookupOptionDto> Reviewers { get; init; } = [];
    public IReadOnlyList<CivilEngineeringQualityTestLookupOptionDto> SourcePartners { get; init; } = [];
    public IReadOnlyList<CivilEngineeringQualityTestLookupOptionDto> ProjectPackages { get; init; } = [];
    public IReadOnlyList<CivilEngineeringQualityTestLookupOptionDto> PaymentCertificates { get; init; } = [];
    public IReadOnlyList<CivilEngineeringQualityTestDocumentLookupDto> Documents { get; init; } = [];
    public bool RequiresEndorsementEvidence { get; init; }
}

public sealed class CivilEngineeringQualityTestReportDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid? ProjectPackageId { get; init; }
    public string? ProjectPackageName { get; init; }
    public Guid? ProjectPaymentCertificateId { get; init; }
    public string? PaymentCertificateNumber { get; init; }
    public CivilEngineeringQualityTestCategory TestCategory { get; init; }
    public string TestCategoryLabel { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public Guid? SourceBusinessPartnerId { get; init; }
    public string? SourceBusinessPartnerName { get; init; }
    public string ReportReference { get; init; } = string.Empty;
    public DateTime TestedAt { get; init; }
    public string ResultStatus { get; init; } = string.Empty;
    public string ResultSummary { get; init; } = string.Empty;
    public Guid ReviewerUserId { get; init; }
    public string ReviewerName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public bool AcceptanceBlocked { get; init; }
    public string? RejectionReason { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public Guid? EndorsementDocumentRecordId { get; init; }
    public Guid? EndorsementDocumentVersionId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateCivilEngineeringQualityTestReportRequest
{
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(100)] public string ReportReference { get; set; } = string.Empty;
    public CivilEngineeringQualityTestCategory TestCategory { get; set; }
    [Required, StringLength(30)] public string SourceType { get; set; } = string.Empty;
    public Guid? SourceBusinessPartnerId { get; set; }
    public DateTime TestedAt { get; set; }
    [Required, StringLength(30)] public string ResultStatus { get; set; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 3)] public string ResultSummary { get; set; } = string.Empty;
    public Guid ReviewerUserId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ProjectPaymentCertificateId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class ProcessCivilEngineeringQualityTestReportRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
    public Guid? EndorsementDocumentRecordId { get; set; }
    public Guid? EndorsementDocumentVersionId { get; set; }
}
