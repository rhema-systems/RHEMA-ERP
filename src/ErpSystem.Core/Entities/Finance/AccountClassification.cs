using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// A tenant-owned detailed GL classification within one accounting book. Accounting-book code
/// selects the ledger basis; this entity describes an account's presentation and default policy
/// inside that book and is never selected by an external posting producer.
/// </summary>
public sealed class AccountClassification : TenantEntity
{
    public Guid AccountingBookId { get; set; }
    public Guid? ParentClassificationId { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public AccountType CoreAccountType { get; set; }
    public RevaluationTreatment DefaultRevaluationTreatment { get; set; } = RevaluationTreatment.Exclude;
    public AccountClassificationSystemRole? SystemRole { get; set; }
    public bool IsPostingClassification { get; set; }
    public AccountClassificationStatus Status { get; set; } = AccountClassificationStatus.Draft;
    public int DisplayOrder { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [MaxLength(500)]
    public string? RetirementReason { get; set; }
    public Guid? RetiredByUserId { get; set; }
    public DateTime? RetiredAtUtc { get; set; }

    public AccountingBook AccountingBook { get; set; } = null!;
    public AccountClassification? ParentClassification { get; set; }
    public ICollection<AccountClassification> Children { get; set; } = new List<AccountClassification>();
    public ICollection<AccountAccountingBook> AccountMappings { get; set; } = new List<AccountAccountingBook>();
}
