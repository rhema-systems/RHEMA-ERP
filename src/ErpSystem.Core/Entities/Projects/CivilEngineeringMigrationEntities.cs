using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Projects;

public enum CivilEngineeringMigrationBatchStatus
{
    ValidationFailed = 0,
    AwaitingReconciliation = 1,
    Reconciled = 2,
    ReadyForOwnerPosting = 3
}

public enum CivilEngineeringMigrationRecordType
{
    Drawing = 0,
    Specification = 1,
    SiteReport = 2,
    Permit = 3,
    MaintenanceScope = 4,
    Complaint = 5,
    TestReport = 6,
    PhysicalFileReference = 7
}

/// <summary>
/// CIV-0605 staging boundary. The batch never stores physical files or posts historical
/// records into owner modules; central DMS retains files and the receiving owner controls
/// any later, separately authorized posting.
/// </summary>
[Table("ProjectCivilMigrationBatches")]
public sealed class ProjectCivilMigrationBatch : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public CivilEngineeringMigrationSource SourceType { get; set; }
    [Required, StringLength(180)] public string SourceRegisterReference { get; set; } = string.Empty;
    public CivilEngineeringMigrationBatchStatus Status { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid ReconciliationEvidenceTemplateId { get; set; }
    [Required, StringLength(120)] public string ReconciliationEvidenceTemplateCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int ErrorCount { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public Guid? ReconciledByUserId { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public Guid? ReconciliationDocumentRecordId { get; set; }
    public Guid? ReconciliationDocumentVersionId { get; set; }
    [StringLength(2000)] public string? ReconciliationDeclaration { get; set; }
    public Guid? SignedOffByUserId { get; set; }
    public DateTime? SignedOffAt { get; set; }
    [StringLength(2000)] public string? SignOffDeclaration { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentMetadataTemplate ReconciliationEvidenceTemplate { get; set; } = null!;
    public CentralDocumentRecord? ReconciliationDocumentRecord { get; set; }
    public CentralDocumentVersion? ReconciliationDocumentVersion { get; set; }
    public ICollection<ProjectCivilMigrationRecord> Records { get; set; } = [];
    public ICollection<ProjectCivilMigrationValidationIssue> ValidationIssues { get; set; } = [];
    public ICollection<ProjectCivilMigrationRevision> Revisions { get; set; } = [];
}

[Table("ProjectCivilMigrationRecords")]
public sealed class ProjectCivilMigrationRecord : TenantEntity
{
    public Guid MigrationBatchId { get; set; }
    public int Sequence { get; set; }
    public CivilEngineeringMigrationRecordType RecordType { get; set; }
    [Required, StringLength(180)] public string SourceReference { get; set; } = string.Empty;
    [Required, StringLength(250)] public string Title { get; set; } = string.Empty;
    public DateTime? RecordDate { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [StringLength(500)] public string? PhysicalFileReference { get; set; }
    [Required, StringLength(24)] public string ValidationStatus { get; set; } = "Valid";
    [StringLength(2000)] public string? ValidationMessage { get; set; }

    public ProjectCivilMigrationBatch MigrationBatch { get; set; } = null!;
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
}

[Table("ProjectCivilMigrationValidationIssues")]
public sealed class ProjectCivilMigrationValidationIssue : TenantEntity
{
    public Guid MigrationBatchId { get; set; }
    public Guid? MigrationRecordId { get; set; }
    public int? Sequence { get; set; }
    [Required, StringLength(80)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(24)] public string Severity { get; set; } = "Error";
    [Required, StringLength(2000)] public string Message { get; set; } = string.Empty;

    public ProjectCivilMigrationBatch MigrationBatch { get; set; } = null!;
    public ProjectCivilMigrationRecord? MigrationRecord { get; set; }
}

[Table("ProjectCivilMigrationRevisions")]
public sealed class ProjectCivilMigrationRevision : TenantEntity
{
    public Guid MigrationBatchId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilMigrationBatch MigrationBatch { get; set; } = null!;
}
