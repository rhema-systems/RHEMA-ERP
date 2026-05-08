using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Sales;

/// <summary>
/// Collection Activity — tracking calls, emails, and follow-ups for overdue invoices.
/// Ported from EzFMC Collections module.
/// </summary>
public class CollectionActivity : BusinessEntity
{
    public Guid CustomerId { get; set; }
    public virtual Customer Customer { get; set; } = null!;

    public Guid? InvoiceId { get; set; }
    public virtual Invoice? Invoice { get; set; }

    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(50)]
    public string ActivityType { get; set; } = "Call"; // Call, Email, Letter, Visit, LegalNotice

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    public DateTime? FollowUpDate { get; set; }

    [StringLength(50)]
    public string CollectionStatus { get; set; } = "Pending"; // Pending, Contacted, PromiseToPay, Escalated, Resolved, WrittenOff

    [StringLength(50)]
    public string? Outcome { get; set; } // Answered, NoAnswer, LeftMessage, Promised, Refused, Escalated

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PromisedAmount { get; set; }

    public DateTime? PromisedPayDate { get; set; }

    public Guid? AssignedToId { get; set; }
    public virtual ApplicationUser? AssignedTo { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// Payment Plan — structured debt repayment schedule for a customer.
/// Ported from EzFMC Collections module.
/// </summary>
public class PaymentPlan : BusinessEntity
{
    public Guid CustomerId { get; set; }
    public virtual Customer Customer { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string PlanName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDebt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPaid { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingBalance => TotalDebt - TotalPaid;

    [StringLength(50)]
    public string PlanStatus { get; set; } = "Draft"; // Draft, Active, Completed, Defaulted, Cancelled

    public int NumberOfInstallments { get; set; }
    public int InstallmentsPaid { get; set; }

    [StringLength(50)]
    public string Frequency { get; set; } = "Monthly"; // Weekly, Bi-Weekly, Monthly, Quarterly

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [StringLength(2000)]
    public string? Terms { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<PaymentPlanInstallment> Installments { get; set; } = new List<PaymentPlanInstallment>();
}

/// <summary>
/// Individual installment within a payment plan.
/// </summary>
public class PaymentPlanInstallment : BaseEntity
{
    public Guid PaymentPlanId { get; set; }
    public virtual PaymentPlan PaymentPlan { get; set; } = null!;

    public int InstallmentNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountDue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance => AmountDue - AmountPaid;

    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }

    [StringLength(50)]
    public string InstallmentStatus { get; set; } = "Pending"; // Pending, Paid, PartiallyPaid, Overdue, Defaulted

    [StringLength(100)]
    public string? PaymentReference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}
