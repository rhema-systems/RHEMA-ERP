using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Sales;

// ═════════════════════════════════════════════
//  COLLECTION ACTIVITY DTOs
// ═════════════════════════════════════════════

public class CollectionActivitySummaryDto
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string CollectionStatus { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal PromisedAmount { get; set; }
    public DateTime? PromisedPayDate { get; set; }
    public string? Outcome { get; set; }
    public DateTime ActivityDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CollectionActivityDetailDto : CollectionActivitySummaryDto
{
    public Guid CustomerId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? Notes { get; set; }
}

public class CreateCollectionActivityDto
{
    [Required]
    public Guid CustomerId { get; set; }
    public Guid? InvoiceId { get; set; }
    [Required]
    public string Subject { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "Call";
    public string? Description { get; set; }
    public DateTime? ActivityDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public decimal OutstandingAmount { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? Notes { get; set; }
}

public class UpdateCollectionActivityDto
{
    public string? CollectionStatus { get; set; }
    public string? Outcome { get; set; }
    public decimal? PromisedAmount { get; set; }
    public DateTime? PromisedPayDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? Notes { get; set; }
}

// ═════════════════════════════════════════════
//  PAYMENT PLAN DTOs
// ═════════════════════════════════════════════

public class PaymentPlanSummaryDto
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanStatus { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal TotalDebt { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public int NumberOfInstallments { get; set; }
    public int InstallmentsPaid { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentPlanDetailDto : PaymentPlanSummaryDto
{
    public Guid CustomerId { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? Terms { get; set; }
    public string? Notes { get; set; }
    public List<PaymentPlanInstallmentDto> Installments { get; set; } = new();
}

public class PaymentPlanInstallmentDto
{
    public Guid Id { get; set; }
    public int InstallmentNumber { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public string InstallmentStatus { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}

public class CreatePaymentPlanDto
{
    [Required]
    public Guid CustomerId { get; set; }
    [Required]
    public string PlanName { get; set; } = string.Empty;
    public decimal TotalDebt { get; set; }
    public int NumberOfInstallments { get; set; }
    public string Frequency { get; set; } = "Monthly";
    public DateTime StartDate { get; set; }
    public string? Terms { get; set; }
    public string? Notes { get; set; }
}

public class RecordInstallmentPaymentDto
{
    public decimal AmountPaid { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}
