using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Sales;

/// <summary>
/// Controlled values shared by the Sales collection screens and the Finance AR
/// follow-up workspace. Keeping the values beside the existing aggregate avoids
/// two modules persisting subtly different status/type strings in the same table.
/// </summary>
public static class CollectionActivityValues
{
    public const string SalesContext = "Sales";
    public const string FinanceArContext = "FinanceAR";

    public const string FollowUpTaskType = "FollowUpTask";
    public const string ReminderType = "Reminder";

    public const string PendingStatus = "Pending";
    public const string InProgressStatus = "InProgress";
    public const string PromiseToPayStatus = "PromiseToPay";
    public const string EscalatedStatus = "Escalated";
    public const string ResolvedStatus = "Resolved";
    public const string WrittenOffStatus = "WrittenOff";

    public static readonly IReadOnlySet<string> TaskStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PendingStatus,
        InProgressStatus,
        PromiseToPayStatus,
        EscalatedStatus,
        ResolvedStatus,
        WrittenOffStatus
    };

    public static readonly IReadOnlySet<string> ReminderChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Email",
        "SMS",
        "Letter",
        "Phone",
        "Visit",
        "Internal"
    };
}

/// <summary>
/// Collection Activity — tracking calls, emails, and follow-ups for overdue invoices.
/// Ported from EzFMC Collections module.
/// </summary>
public class CollectionActivity : BusinessEntity
{
    /// <summary>
    /// Canonical counterparty identity. Collection work must never point to the retired Sales
    /// Customer master; the Customer role/profile determines transaction readiness separately.
    /// </summary>
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    public Guid? InvoiceId { get; set; }
    public virtual Invoice? Invoice { get; set; }

    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(50)]
    public string ActivityType { get; set; } = "Call"; // Call, Email, Letter, Visit, LegalNotice

    /// <summary>
    /// Identifies the operational surface that owns this activity. Existing
    /// Sales collection records remain valid, while Finance AR can safely query
    /// only its controlled work items from the shared collection aggregate.
    /// </summary>
    [Required]
    [StringLength(30)]
    public string CollectionContext { get; set; } = CollectionActivityValues.SalesContext;

    /// <summary>
    /// The durable task at the head of a Finance collection case. Reminder and
    /// contact-history rows point back to this task instead of overwriting it.
    /// </summary>
    public bool IsPrimaryTask { get; set; }

    public Guid? ParentActivityId { get; set; }
    public virtual CollectionActivity? ParentActivity { get; set; }
    public virtual ICollection<CollectionActivity> History { get; set; } = new List<CollectionActivity>();

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

    /// <summary>
    /// Channel and recipient are retained as evidence of a prepared or recorded
    /// reminder. Finance deliberately records external reminders without assuming
    /// that an email/SMS provider is configured in every TDC environment.
    /// </summary>
    [StringLength(20)]
    public string? ReminderChannel { get; set; }

    [StringLength(250)]
    public string? ReminderRecipient { get; set; }

    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public virtual ApplicationUser? CompletedBy { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Optimistic concurrency protects task assignment and collection outcomes
    /// when two officers update the same overdue exposure at the same time.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

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
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

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
