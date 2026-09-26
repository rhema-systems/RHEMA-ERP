using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// A role is a capability of one canonical Business Partner identity. Do not create separate
/// supplier or customer master records when another role is required; activate another role on
/// the same partner instead.
/// </summary>
public enum BusinessPartnerRoleType
{
    Supplier = 1,
    Contractor = 2,
    Customer = 3
}

public enum BusinessPartnerRoleStatus
{
    Active = 1,
    Inactive = 2
}

public enum BusinessPartnerFinanceProfileStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Superseded = 5
}

/// <summary>
/// Independently activatable role owned by the canonical Business Partner. Inactivation prevents
/// new transactions but deliberately preserves identity and historical settlement/reversal use.
/// </summary>
public sealed class BusinessPartnerRole : TenantEntity
{
    public Guid BusinessPartnerId { get; set; }
    public BusinessPartner BusinessPartner { get; set; } = null!;

    public BusinessPartnerRoleType RoleType { get; set; }
    public BusinessPartnerRoleStatus Status { get; set; } = BusinessPartnerRoleStatus.Active;

    public DateTime ActiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? InactiveFromUtc { get; set; }

    [MaxLength(1000)]
    public string? StatusReason { get; set; }
}

/// <summary>
/// Governed, effective-dated AP defaults for Supplier or Contractor use. AP control-account
/// authority is intentionally absent: the tenant Finance settings remain authoritative.
/// </summary>
public sealed class BusinessPartnerApProfileVersion : TenantEntity
{
    public Guid BusinessPartnerRoleId { get; set; }
    public BusinessPartnerRole BusinessPartnerRole { get; set; } = null!;

    public int VersionNumber { get; set; }
    public BusinessPartnerFinanceProfileStatus Status { get; set; } = BusinessPartnerFinanceProfileStatus.Draft;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(50)]
    public string? ApReferenceNumber { get; set; }

    public Guid? PaymentTermId { get; set; }
    public PaymentTerm? PaymentTerm { get; set; }

    public Guid? DefaultTaxGroupId { get; set; }
    public TaxGroup? DefaultTaxGroup { get; set; }

    public Guid? DefaultExpenseAccountId { get; set; }
    public Account? DefaultExpenseAccount { get; set; }

    public bool SubjectToWithholding { get; set; }
    public Guid? DefaultWithholdingLineId { get; set; }

    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    [MaxLength(1000)]
    public string? DecisionReason { get; set; }

    public ICollection<BusinessPartnerApWhtDefault> WithholdingDefaults { get; set; } =
        new List<BusinessPartnerApWhtDefault>();
}

/// <summary>
/// One category-specific WHT default within an AP profile. The referenced Tax record supplies the
/// approved effective rate and payable account for the invoice accounting date; rates are not
/// duplicated on the partner master.
/// </summary>
public sealed class BusinessPartnerApWhtDefault : TenantEntity
{
    public Guid ApProfileVersionId { get; set; }
    public BusinessPartnerApProfileVersion ApProfileVersion { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string CategoryCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CategoryName { get; set; }

    public Guid WithholdingTaxId { get; set; }
    public Tax WithholdingTax { get; set; } = null!;

    public bool IsDefaultForAp { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Governed, effective-dated AR defaults for the Customer role. Actual customer-withheld amounts
/// and certificate evidence belong to the receipt, not this master profile.
/// </summary>
public sealed class BusinessPartnerArProfileVersion : TenantEntity
{
    public Guid BusinessPartnerRoleId { get; set; }
    public BusinessPartnerRole BusinessPartnerRole { get; set; } = null!;

    public int VersionNumber { get; set; }
    public BusinessPartnerFinanceProfileStatus Status { get; set; } = BusinessPartnerFinanceProfileStatus.Draft;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(50)]
    public string? ArReferenceNumber { get; set; }

    public Guid? PaymentTermId { get; set; }
    public PaymentTerm? PaymentTerm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CreditLimit { get; set; }

    public bool IsWithholdingAgent { get; set; }

    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    [MaxLength(1000)]
    public string? DecisionReason { get; set; }
}
