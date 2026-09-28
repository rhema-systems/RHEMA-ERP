using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// A tenant-owned financial statement presentation that is independent of its accounting book.
/// </summary>
public class FinancialStatementLayout : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public FinancialStatementType StatementType { get; set; }

    public Guid AccountingBookId { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Protected standards are clone-only templates and cannot be edited or retired in place.</summary>
    public bool IsProtectedStandard { get; set; }

    public Guid? StandardSourceLayoutId { get; set; }

    public int Revision { get; set; } = 1;

    public virtual AccountingBook AccountingBook { get; set; } = null!;

    public virtual FinancialStatementLayout? StandardSourceLayout { get; set; }

    public virtual ICollection<FinancialStatementLayoutVersion> Versions { get; set; }
        = new List<FinancialStatementLayoutVersion>();
}

/// <summary>
/// An effective-dated, publishable snapshot of a financial statement layout.
/// Published and retired versions are immutable.
/// </summary>
public class FinancialStatementLayoutVersion : TenantEntity
{
    public Guid FinancialStatementLayoutId { get; set; }

    public int VersionNumber { get; set; }

    public FinancialStatementLayoutVersionStatus Status { get; set; }
        = FinancialStatementLayoutVersionStatus.Draft;

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Guid? PublishedById { get; set; }

    [MaxLength(200)]
    public string? PublishedByName { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? SubmittedById { get; set; }

    [MaxLength(200)]
    public string? SubmittedByName { get; set; }

    public DateTime? LastDecisionAt { get; set; }

    public Guid? LastDecisionById { get; set; }

    [MaxLength(200)]
    public string? LastDecisionByName { get; set; }

    [MaxLength(500)]
    public string? LastDecisionReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public int Revision { get; set; } = 1;

    [MaxLength(20)]
    public string? PublicationSnapshotSchemaVersion { get; set; }

    public Guid? PublishedAccountingBookId { get; set; }

    [MaxLength(20)]
    public string? PublishedAccountingBookCode { get; set; }

    [MaxLength(100)]
    public string? PublishedAccountingBookName { get; set; }

    [MaxLength(64)]
    public string? HierarchyFingerprint { get; set; }

    [MaxLength(64)]
    public string? ResolutionFingerprint { get; set; }

    public virtual FinancialStatementLayout FinancialStatementLayout { get; set; } = null!;

    public virtual ICollection<FinancialStatementRow> Rows { get; set; }
        = new List<FinancialStatementRow>();

    public virtual ICollection<FinancialStatementPublicationAccount> PublicationAccounts { get; set; }
        = new List<FinancialStatementPublicationAccount>();
}

/// <summary>
/// A coded row in a single layout version.
/// </summary>
public class FinancialStatementRow : TenantEntity
{
    public Guid FinancialStatementLayoutVersionId { get; set; }

    public Guid? ParentRowId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RowCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    public FinancialStatementRowType RowType { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(1000)]
    public string? Formula { get; set; }

    public int SignMultiplier { get; set; } = 1;

    public bool IsVisible { get; set; } = true;

    public bool SuppressIfZero { get; set; }

    public bool ShowAccountDetails { get; set; }

    public bool IsBold { get; set; }

    public bool IsItalic { get; set; }

    public bool IsUnderlined { get; set; }

    public int IndentLevel { get; set; }

    public virtual FinancialStatementLayoutVersion FinancialStatementLayoutVersion { get; set; } = null!;

    public virtual FinancialStatementRow? ParentRow { get; set; }

    public virtual ICollection<FinancialStatementRow> ChildRows { get; set; }
        = new List<FinancialStatementRow>();

    public virtual ICollection<FinancialStatementRowMapping> Mappings { get; set; }
        = new List<FinancialStatementRowMapping>();
}

/// <summary>
/// Selects the GL accounts that contribute directly to an account row.
/// </summary>
public class FinancialStatementRowMapping : TenantEntity
{
    public Guid FinancialStatementRowId { get; set; }

    public FinancialStatementRowMappingType MappingType { get; set; }

    /// <summary>
    /// Exact account for Account mappings, or root account for AccountHierarchy mappings.
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>Stable classification selector for Classification mappings.</summary>
    public Guid? AccountClassificationId { get; set; }

    public bool IncludeClassificationDescendants { get; set; } = true;

    [MaxLength(100)]
    public string? FromAccountNumber { get; set; }

    [MaxLength(100)]
    public string? ToAccountNumber { get; set; }

    public virtual FinancialStatementRow FinancialStatementRow { get; set; } = null!;

    public virtual Account? Account { get; set; }

    public virtual AccountClassification? AccountClassification { get; set; }
}

/// <summary>
/// Immutable resolved membership captured when a layout version is published. Published execution
/// never re-resolves live account, hierarchy, or classification membership.
/// </summary>
public sealed class FinancialStatementPublicationAccount : TenantEntity
{
    public Guid FinancialStatementLayoutVersionId { get; set; }
    public Guid FinancialStatementRowId { get; set; }
    public Guid FinancialStatementRowMappingId { get; set; }
    public FinancialStatementRowMappingType MappingType { get; set; }
    public Guid AccountId { get; set; }

    [Required, MaxLength(50)]
    public string RowCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string AccountName { get; set; } = string.Empty;

    public AccountType AccountType { get; set; }
    public Guid AccountingBookId { get; set; }

    [Required, MaxLength(20)]
    public string AccountingBookCode { get; set; } = string.Empty;

    public Guid? AccountClassificationId { get; set; }

    [MaxLength(50)]
    public string? ClassificationCode { get; set; }

    [MaxLength(200)]
    public string? ClassificationName { get; set; }

    [MaxLength(1000)]
    public string? ClassificationPath { get; set; }

    [MaxLength(500)]
    public string? MappingSelector { get; set; }

    public FinancialStatementLayoutVersion FinancialStatementLayoutVersion { get; set; } = null!;
    public FinancialStatementRow FinancialStatementRow { get; set; } = null!;
    public FinancialStatementRowMapping FinancialStatementRowMapping { get; set; } = null!;
}
