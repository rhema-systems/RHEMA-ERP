using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public class FinancialStatementLayoutSummaryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FinancialStatementType StatementType { get; set; }
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public string AccountingBookName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public bool IsProtectedStandard { get; set; }
    public Guid? StandardSourceLayoutId { get; set; }
    public int Revision { get; set; }
    public int LatestVersionNumber { get; set; }
    public int? PublishedVersionNumber { get; set; }
}

public sealed class FinancialStatementLayoutDto : FinancialStatementLayoutSummaryDto
{
    public IReadOnlyList<FinancialStatementLayoutVersionDto> Versions { get; set; }
        = Array.Empty<FinancialStatementLayoutVersionDto>();
}

public sealed class FinancialStatementLayoutVersionDto
{
    public Guid Id { get; set; }
    public Guid FinancialStatementLayoutId { get; set; }
    public int VersionNumber { get; set; }
    public FinancialStatementLayoutVersionStatus Status { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public string? PublishedByName { get; set; }
    public string? Notes { get; set; }
    public int Revision { get; set; }
    public string? PublicationSnapshotSchemaVersion { get; set; }
    public Guid? PublishedAccountingBookId { get; set; }
    public string? PublishedAccountingBookCode { get; set; }
    public string? PublishedAccountingBookName { get; set; }
    public string? HierarchyFingerprint { get; set; }
    public string? ResolutionFingerprint { get; set; }
    public int PublicationAccountCount { get; set; }
    public IReadOnlyList<FinancialStatementRowDto> Rows { get; set; }
        = Array.Empty<FinancialStatementRowDto>();
}

public sealed class FinancialStatementRowDto
{
    public Guid Id { get; set; }
    public string RowCode { get; set; } = string.Empty;
    public string? ParentRowCode { get; set; }
    public string Label { get; set; } = string.Empty;
    public FinancialStatementRowType RowType { get; set; }
    public int DisplayOrder { get; set; }
    public string? Formula { get; set; }
    public int SignMultiplier { get; set; }
    public bool IsVisible { get; set; }
    public bool SuppressIfZero { get; set; }
    public bool ShowAccountDetails { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderlined { get; set; }
    public int IndentLevel { get; set; }
    public IReadOnlyList<FinancialStatementRowMappingDto> Mappings { get; set; }
        = Array.Empty<FinancialStatementRowMappingDto>();
}

public sealed class FinancialStatementRowMappingDto
{
    public Guid Id { get; set; }
    public FinancialStatementRowMappingType MappingType { get; set; }
    public Guid? AccountId { get; set; }

    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? FromAccountNumber { get; set; }
    public string? ToAccountNumber { get; set; }
    public Guid? AccountClassificationId { get; set; }
    public string? AccountClassificationCode { get; set; }
    public string? AccountClassificationName { get; set; }
    public bool IncludeClassificationDescendants { get; set; }
}

public sealed class CreateFinancialStatementLayoutDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public FinancialStatementType StatementType { get; set; }

    [Required]
    public Guid AccountingBookId { get; set; }

    public bool IsDefault { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public sealed class UpdateFinancialStatementLayoutDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(1, int.MaxValue)]
    public int ExpectedRevision { get; set; }
}

public sealed class CreateFinancialStatementLayoutVersionDto
{
    public Guid? SourceVersionId { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public sealed class CloneFinancialStatementLayoutDto
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public Guid AccountingBookId { get; set; }
}

public sealed class ReplaceFinancialStatementRowsDto
{
    [Range(1, int.MaxValue)]
    public int ExpectedVersionRevision { get; set; }

    [Required]
    public List<FinancialStatementRowInputDto> Rows { get; set; } = new();
}

public sealed class FinancialStatementRowInputDto
{
    [Required]
    [MaxLength(50)]
    public string RowCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ParentRowCode { get; set; }

    [Required]
    [MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    public FinancialStatementRowType RowType { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(1000)]
    public string? Formula { get; set; }

    [Range(-1, 1)]
    public int SignMultiplier { get; set; } = 1;

    public bool IsVisible { get; set; } = true;

    public bool SuppressIfZero { get; set; }

    public bool ShowAccountDetails { get; set; }

    public bool IsBold { get; set; }

    public bool IsItalic { get; set; }

    public bool IsUnderlined { get; set; }

    [Range(0, 20)]
    public int IndentLevel { get; set; }

    public List<FinancialStatementRowMappingInputDto> Mappings { get; set; } = new();
}

public sealed class FinancialStatementRowMappingInputDto
{
    public FinancialStatementRowMappingType MappingType { get; set; }

    public Guid? AccountId { get; set; }

    [MaxLength(100)]
    public string? AccountNumber { get; set; }

    [MaxLength(100)]
    public string? FromAccountNumber { get; set; }

    [MaxLength(100)]
    public string? ToAccountNumber { get; set; }
    public Guid? AccountClassificationId { get; set; }
    [MaxLength(50)]
    public string? AccountClassificationCode { get; set; }
    public bool IncludeClassificationDescendants { get; set; } = true;
}

public sealed class PublishFinancialStatementLayoutVersionDto
{
    [Range(1, int.MaxValue)]
    public int ExpectedVersionRevision { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }
}

public sealed class FinancialStatementLayoutValidationResultDto
{
    public bool IsValid => Issues.All(issue =>
        issue.Severity != FinancialStatementLayoutValidationSeverity.Error);

    public List<FinancialStatementLayoutValidationIssueDto> Issues { get; set; } = new();
}

public sealed class FinancialStatementLayoutValidationIssueDto
{
    public FinancialStatementLayoutValidationSeverity Severity { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RowCode { get; set; }
}

public enum FinancialStatementLayoutValidationSeverity
{
    Information = 1,
    Warning = 2,
    Error = 3
}

public sealed class FinancialStatementLayoutPreviewRequestDto
{
    public DateTime? PeriodStart { get; set; }

    [Required]
    public DateTime PeriodEnd { get; set; }

    public bool IncludeAccountDetails { get; set; } = true;

    public bool IncludeHiddenRows { get; set; } = true;

    public List<Guid> AccountIds { get; set; } = new();

    public List<FinanceSegmentFilterDto> SegmentFilters { get; set; } = new();
    public List<FinanceDimensionFilterDto> DimensionFilters { get; set; } = new();
}

public sealed class FinancialStatementLayoutExecutionRequestDto
{
    public Guid? LayoutId { get; set; }

    [Required]
    public FinancialStatementType StatementType { get; set; }

    [Required]
    public Guid AccountingBookId { get; set; }

    public DateTime? PeriodStart { get; set; }

    [Required]
    public DateTime PeriodEnd { get; set; }

    public bool IncludeAccountDetails { get; set; }

    public bool IncludeHiddenRows { get; set; }

    public List<Guid> AccountIds { get; set; } = new();

    public List<FinanceSegmentFilterDto> SegmentFilters { get; set; } = new();
    public List<FinanceDimensionFilterDto> DimensionFilters { get; set; } = new();
}

public sealed class FinancialStatementLayoutExecutionDto
{
    public Guid LayoutId { get; set; }
    public string LayoutCode { get; set; } = string.Empty;
    public string LayoutName { get; set; } = string.Empty;
    public Guid VersionId { get; set; }
    public int VersionNumber { get; set; }
    public FinancialStatementLayoutVersionStatus VersionStatus { get; set; }
    public FinancialStatementType StatementType { get; set; }
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public string AccountingBookName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime? PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }
    public bool IsPreview { get; set; }
    public IReadOnlyList<FinancialStatementLayoutExecutionRowDto> Rows { get; set; }
        = Array.Empty<FinancialStatementLayoutExecutionRowDto>();
    public FinancialStatementLayoutReconciliationDto Reconciliation { get; set; }
        = new();
    public IReadOnlyList<FinancialStatementLayoutValidationIssueDto> Warnings { get; set; }
        = Array.Empty<FinancialStatementLayoutValidationIssueDto>();
}

public sealed class FinancialStatementLayoutExecutionRowDto
{
    public Guid RowId { get; set; }
    public string RowCode { get; set; } = string.Empty;
    public string? ParentRowCode { get; set; }
    public string Label { get; set; } = string.Empty;
    public FinancialStatementRowType RowType { get; set; }
    public int DisplayOrder { get; set; }
    public int Sequence { get; set; }
    public string? Formula { get; set; }
    public decimal Amount { get; set; }
    public bool IsDisplayed { get; set; }
    public bool SuppressIfZero { get; set; }
    public bool ShowAccountDetails { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderlined { get; set; }
    public int IndentLevel { get; set; }
    public IReadOnlyList<FinancialStatementLayoutAccountDetailDto> Accounts { get; set; }
        = Array.Empty<FinancialStatementLayoutAccountDetailDto>();
}

public sealed class FinancialStatementLayoutAccountDetailDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal NormalBalance { get; set; }
    public decimal PresentedAmount { get; set; }
}

public sealed class FinancialStatementLayoutReconciliationDto
{
    public int EligibleAccountCount { get; set; }
    public int MappedAccountCount { get; set; }
    public int MappedNonZeroAccountCount { get; set; }
    public int UnmappedAccountCount { get; set; }
    public int UnmappedNonZeroAccountCount { get; set; }
    public decimal MappedNormalBalance { get; set; }
    public decimal UnmappedNormalBalance { get; set; }
    public decimal AccountCoveragePercent { get; set; }
    public IReadOnlyList<FinancialStatementLayoutUnmappedAccountDto> UnmappedAccounts { get; set; }
        = Array.Empty<FinancialStatementLayoutUnmappedAccountDto>();
}

public sealed class FinancialStatementLayoutUnmappedAccountDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal NormalBalance { get; set; }
}

public sealed class FinancialStatementLayoutImportDefinitionDto
{
    public string TemplateVersion { get; set; } = "2";

    public Guid? TargetLayoutId { get; set; }

    public Guid? TargetVersionId { get; set; }

    public Guid? SourceVersionId { get; set; }

    public int? ExpectedTargetVersionRevision { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public FinancialStatementType StatementType { get; set; }

    [Required]
    public Guid AccountingBookId { get; set; }

    public bool IsDefault { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public List<FinancialStatementRowInputDto> Rows { get; set; } = new();
}

public sealed class FinancialStatementLayoutImportCommitDto
{
    [Required]
    public FinancialStatementLayoutImportDefinitionDto Definition { get; set; } = new();

    [Required]
    [MaxLength(64)]
    public string ExpectedDefinitionHash { get; set; } = string.Empty;
}

public sealed class FinancialStatementLayoutImportPreviewDto
{
    public string DefinitionHash { get; set; } = string.Empty;
    public bool WillCreateLayout { get; set; }
    public Guid? TargetLayoutId { get; set; }
    public Guid? TargetVersionId { get; set; }
    public int RowCount { get; set; }
    public int MappingCount { get; set; }
    public FinancialStatementLayoutImportDefinitionDto Definition { get; set; } = new();
    public FinancialStatementLayoutValidationResultDto Validation { get; set; } = new();
}

public sealed class FinancialStatementLayoutImportResultDto
{
    public string DefinitionHash { get; set; } = string.Empty;
    public bool CreatedLayout { get; set; }
    public Guid LayoutId { get; set; }
    public Guid DraftVersionId { get; set; }
    public int DraftVersionNumber { get; set; }
    public int DraftVersionRevision { get; set; }
    public FinancialStatementLayoutDto Layout { get; set; } = new();
}

public sealed class FinancialStatementLayoutFileDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; }
        = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public sealed class LegacyFinancialStatementLayoutMigrationRequestDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public FinancialStatementType StatementType { get; set; }

    [Required]
    public Guid AccountingBookId { get; set; }

    public bool IsDefault { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public sealed class LegacyFinancialStatementLayoutMigrationCommitDto
{
    [Required]
    public LegacyFinancialStatementLayoutMigrationRequestDto Request { get; set; } = new();

    [Required]
    [MaxLength(64)]
    public string ExpectedDefinitionHash { get; set; } = string.Empty;
}

public sealed class FinancialStatementLayoutAuditEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? DetailsJson { get; set; }
}
