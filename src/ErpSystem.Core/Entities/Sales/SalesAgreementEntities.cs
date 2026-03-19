using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Sales;

#region Sales Agreement

/// <summary>
/// Master Agreement with a customer — covers pricing commitments, volume targets,
/// lease/tenancy agreements, plot allocations, and service-level agreements.
/// For TDC: maps to property sale agreements, lease agreements, and tenancy contracts.
/// </summary>
public class SalesAgreement : DocumentEntity
{
    // ── Customer (via unified BusinessPartner) ──────────────────────────

    [Required]
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    // ── Agreement Classification ────────────────────────────────────────

    [Required]
    [MaxLength(200)]
    public string AgreementTitle { get; set; } = string.Empty;

    public SalesAgreementType AgreementType { get; set; } = SalesAgreementType.General;

    public SalesAgreementStatus AgreementStatus { get; set; } = SalesAgreementStatus.Draft;

    // ── Validity Period ─────────────────────────────────────────────────

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Number of days before expiry to flag as "Expiring"
    /// </summary>
    public int ExpiryWarningDays { get; set; } = 30;

    /// <summary>
    /// Whether the agreement auto-renews at expiry
    /// </summary>
    public bool AutoRenew { get; set; } = false;

    /// <summary>
    /// Renewal period in months (if auto-renew)
    /// </summary>
    public int? RenewalPeriodMonths { get; set; }

    // ── Financial ───────────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,2)")]
    public decimal AgreedValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinimumCommitment { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaximumCommitment { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UtilizedValue { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "GHS";

    // ── Pricing Model ───────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercentage { get; set; }

    [MaxLength(500)]
    public string? PricingTerms { get; set; }

    [MaxLength(500)]
    public string? PaymentSchedule { get; set; }

    // ── Property/Plot Reference (TDC-specific) ──────────────────────────

    [MaxLength(100)]
    public string? PropertyReference { get; set; }

    public PropertyType? PropertyType { get; set; }

    [MaxLength(500)]
    public string? PropertyDescription { get; set; }

    [MaxLength(200)]
    public string? PropertyLocation { get; set; }

    // ── Sales Rep Assignment ────────────────────────────────────────────

    public Guid? SalesRepId { get; set; }
    public virtual ApplicationUser? SalesRep { get; set; }

    // ── Approval ────────────────────────────────────────────────────────

    public Guid? ApprovedById { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    // ── Termination ─────────────────────────────────────────────────────

    public DateTime? TerminatedDate { get; set; }

    [MaxLength(1000)]
    public string? TerminationReason { get; set; }

    public Guid? TerminatedById { get; set; }
    public virtual ApplicationUser? TerminatedBy { get; set; }

    // ── Notes ────────────────────────────────────────────────────────────

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? InternalNotes { get; set; }

    [MaxLength(2000)]
    public string? TermsAndConditions { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation Properties ───────────────────────────────────────────

    public virtual ICollection<SalesAgreementLine> Lines { get; set; } = new List<SalesAgreementLine>();
    public virtual ICollection<SalesAgreementMilestone> Milestones { get; set; } = new List<SalesAgreementMilestone>();
    public virtual ICollection<SalesAgreementRenewal> Renewals { get; set; } = new List<SalesAgreementRenewal>();
    public virtual ICollection<SalesAgreementDocument> Documents { get; set; } = new List<SalesAgreementDocument>();
    public virtual ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
}

#endregion

#region Sales Agreement Line

/// <summary>
/// Product/property-level terms within an agreement.
/// Defines agreed pricing, volume commitments, and discount tiers.
/// </summary>
public class SalesAgreementLine : BaseEntity
{
    [Required]
    public Guid SalesAgreementId { get; set; }
    public virtual SalesAgreement SalesAgreement { get; set; } = null!;

    public int LineNumber { get; set; } = 1;

    // ── Product / Service ───────────────────────────────────────────────

    public Guid? ProductId { get; set; }
    public virtual Product? Product { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ProductCode { get; set; }

    // ── Agreed Terms ────────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,2)")]
    public decimal AgreedPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal MinimumQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal MaximumQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UtilizedQuantity { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    // ── Discount Tiers ──────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercentage { get; set; }

    [MaxLength(1000)]
    public string? DiscountTiersJson { get; set; } // JSON: [{minQty, maxQty, discount%}]

    // ── Notes ────────────────────────────────────────────────────────────

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Sales Agreement Milestone

/// <summary>
/// Payment milestone for staged agreements.
/// Critical for TDC property sales: deposit → foundation → handover payments.
/// </summary>
public class SalesAgreementMilestone : BaseEntity
{
    [Required]
    public Guid SalesAgreementId { get; set; }
    public virtual SalesAgreement SalesAgreement { get; set; } = null!;

    public int SequenceNumber { get; set; } = 1;

    [Required]
    [MaxLength(200)]
    public string MilestoneName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    // ── Payment Details ─────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal PaymentPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaymentAmount { get; set; }

    // ── Timing ──────────────────────────────────────────────────────────

    public DateTime? DueDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime? PaidDate { get; set; }

    // ── Status ──────────────────────────────────────────────────────────

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Completed, Paid, Overdue

    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    public Guid? InvoiceId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Sales Agreement Renewal

/// <summary>
/// Tracks renewal history of agreements. Critical for lease/tenancy management at TDC.
/// </summary>
public class SalesAgreementRenewal : BaseEntity
{
    [Required]
    public Guid SalesAgreementId { get; set; }
    public virtual SalesAgreement SalesAgreement { get; set; } = null!;

    public int RenewalNumber { get; set; } = 1;

    public DateTime PreviousStartDate { get; set; }
    public DateTime PreviousEndDate { get; set; }

    public DateTime NewStartDate { get; set; }
    public DateTime NewEndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PreviousValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NewValue { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? PriceChangePercentage { get; set; }

    [MaxLength(1000)]
    public string? RenewalTerms { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? RenewedById { get; set; }
    public virtual ApplicationUser? RenewedBy { get; set; }

    public DateTime RenewedDate { get; set; } = DateTime.UtcNow;

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Sales Agreement Document

/// <summary>
/// Attached contract files and supporting documents.
/// </summary>
public class SalesAgreementDocument : BaseEntity
{
    [Required]
    public Guid SalesAgreementId { get; set; }
    public virtual SalesAgreement SalesAgreement { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    [MaxLength(100)]
    public string DocumentType { get; set; } = "Contract"; // Contract, Amendment, Addendum, SupportDoc

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? UploadedById { get; set; }
    public virtual ApplicationUser? UploadedBy { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion
