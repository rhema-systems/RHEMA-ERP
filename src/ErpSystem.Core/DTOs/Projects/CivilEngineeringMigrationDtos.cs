using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringMigrationDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringMigrationLookupsDto
{
    public IReadOnlyList<CivilEngineeringMigrationSource> SourceTypes { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMigrationRecordType> RecordTypes { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMigrationDocumentLookupDto> Documents { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMigrationDocumentLookupDto> ReconciliationDocuments { get; init; } = [];
    public bool PreservePhysicalFileReference { get; init; }
    public bool RequireReconciliation { get; init; }
    public bool RequireSignedAcceptance { get; init; }
}

public sealed class CivilEngineeringMigrationRecordInputDto
{
    public CivilEngineeringMigrationRecordType RecordType { get; set; }
    [Required, StringLength(180, MinimumLength = 3)] public string SourceReference { get; set; } = string.Empty;
    [Required, StringLength(250, MinimumLength = 3)] public string Title { get; set; } = string.Empty;
    public DateTime? RecordDate { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [StringLength(500)] public string? PhysicalFileReference { get; set; }
}

public sealed class StageCivilEngineeringMigrationBatchRequest
{
    public Guid ClientRequestId { get; set; }
    public CivilEngineeringMigrationSource SourceType { get; set; }
    [Required, StringLength(180, MinimumLength = 3)] public string SourceRegisterReference { get; set; } = string.Empty;
    [Required, MinLength(1), MaxLength(1000)] public List<CivilEngineeringMigrationRecordInputDto> Records { get; set; } = [];
}

public sealed class ReconcileCivilEngineeringMigrationBatchRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public Guid ReconciliationDocumentRecordId { get; set; }
    public Guid ReconciliationDocumentVersionId { get; set; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Declaration { get; set; } = string.Empty;
}

public sealed class SignOffCivilEngineeringMigrationBatchRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Declaration { get; set; } = string.Empty;
}

public sealed class CivilEngineeringMigrationIssueDto
{
    public int? Sequence { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class CivilEngineeringMigrationRecordDto
{
    public int Sequence { get; init; }
    public CivilEngineeringMigrationRecordType RecordType { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime? RecordDate { get; init; }
    public string? DocumentReference { get; init; }
    public string? PhysicalFileReference { get; init; }
    public string ValidationStatus { get; init; } = string.Empty;
    public string? ValidationMessage { get; init; }
}

public sealed class CivilEngineeringMigrationBatchDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public CivilEngineeringMigrationSource SourceType { get; init; }
    public string SourceRegisterReference { get; init; } = string.Empty;
    public CivilEngineeringMigrationBatchStatus Status { get; init; }
    public int RecordCount { get; init; }
    public int ErrorCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReconciledAt { get; init; }
    public DateTime? SignedOffAt { get; init; }
    public bool IsReadyForOwnerPosting { get; init; }
    public string PostingBoundary { get; init; } = "Owner posting is intentionally unavailable from the Civil migration workbench.";
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringMigrationRecordDto> Records { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMigrationIssueDto> Issues { get; init; } = [];
}

public sealed class CivilEngineeringMigrationRevisionDto
{
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
