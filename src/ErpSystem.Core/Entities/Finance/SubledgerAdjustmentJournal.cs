using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Finance;

public static class SubledgerModules
{
    public const string AccountsReceivable = "AR";
    public const string AccountsPayable = "AP";
}

public static class SubledgerAdjustmentTypes
{
    public const string Debit = "Debit";
    public const string Credit = "Credit";
}

public static class SubledgerAdjustmentStatuses
{
    public const string Posted = "Posted";
    public const string Reversed = "Reversed";
}

public static class SubledgerAdjustmentPurposes
{
    public const string StandardAdjustment = "StandardAdjustment";
    public const string OpeningBalance = "OpeningBalance";
}

public class SubledgerAdjustmentJournal : TenantEntity
{
    [Required]
    [MaxLength(2)]
    public string Module { get; set; } = SubledgerModules.AccountsReceivable;

    [Required]
    [MaxLength(50)]
    public string AdjustmentNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Purpose { get; set; } = SubledgerAdjustmentPurposes.StandardAdjustment;

    [Required]
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    [Required]
    public Guid BusinessPartnerRoleId { get; set; }
    public virtual BusinessPartnerRole BusinessPartnerRole { get; set; } = null!;

    public Guid? BusinessPartnerApProfileVersionId { get; set; }
    public virtual BusinessPartnerApProfileVersion? BusinessPartnerApProfileVersion { get; set; }

    public Guid? BusinessPartnerArProfileVersionId { get; set; }
    public virtual BusinessPartnerArProfileVersion? BusinessPartnerArProfileVersion { get; set; }

    [Required]
    [MaxLength(50)]
    public string BusinessPartnerCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string BusinessPartnerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? BusinessPartnerLegalName { get; set; }

    [MaxLength(100)]
    public string? BusinessPartnerTaxIdentificationNumber { get; set; }

    [Required]
    public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;

    public DateTime? DueDate { get; set; }

    [Required]
    [MaxLength(10)]
    public string AdjustmentType { get; set; } = SubledgerAdjustmentTypes.Debit;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseCurrencyAmount { get; set; }

    [Required]
    public Guid ContraAccountId { get; set; }
    public virtual Account ContraAccount { get; set; } = null!;

    public Guid? JournalEntryId { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = SubledgerAdjustmentStatuses.Posted;

    [MaxLength(100)]
    public string? Reference { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Guid? OriginalAdjustmentId { get; set; }
    public virtual SubledgerAdjustmentJournal? OriginalAdjustment { get; set; }

    public Guid? ReversalAdjustmentId { get; set; }
    public virtual SubledgerAdjustmentJournal? ReversalAdjustment { get; set; }

    [MaxLength(500)]
    public string? ReversalReason { get; set; }

    public DateTime? ReversedAt { get; set; }

    [NotMapped]
    public decimal SignedSubledgerAmount
    {
        get
        {
            var isDebit = string.Equals(AdjustmentType, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase);
            return string.Equals(Module, SubledgerModules.AccountsReceivable, StringComparison.OrdinalIgnoreCase)
                ? (isDebit ? Amount : -Amount)
                : (isDebit ? -Amount : Amount);
        }
    }
}
