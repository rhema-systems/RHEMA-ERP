using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

#region Contractor Management DTOs

/// <summary>
/// Main contractor information DTO
/// </summary>
public class MaintenanceContractorDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContractorCode { get; set; }
    public string? Description { get; set; }
    public string ContactInfo { get; set; } = "{}";
    public string Capabilities { get; set; } = "[]";
    public string ServiceAreas { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public double? Rating { get; set; }
    public string? LicenseInfo { get; set; }
    public string? InsuranceInfo { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }

    // Calculated fields
    public int ActiveWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public decimal TotalInvoicesPending { get; set; }
    public DateTime? LastWorkDate { get; set; }
}

/// <summary>
/// DTO for creating new contractors
/// </summary>
public class CreateMaintenanceContractorDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ContractorCode { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public string ContactInfo { get; set; } = "{}";
    public string Capabilities { get; set; } = "[]";
    public string ServiceAreas { get; set; } = "[]";

    [StringLength(20)]
    public string Status { get; set; } = "Active";

    public string? LicenseInfo { get; set; }
    public string? InsuranceInfo { get; set; }
}

/// <summary>
/// DTO for updating contractors
/// </summary>
public class UpdateMaintenanceContractorDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ContractorCode { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public string ContactInfo { get; set; } = "{}";
    public string Capabilities { get; set; } = "[]";
    public string ServiceAreas { get; set; } = "[]";

    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    public string? LicenseInfo { get; set; }
    public string? InsuranceInfo { get; set; }
}

/// <summary>
/// DTO for contractor work order assignments
/// </summary>
public class ContractorWorkOrderDto
{
    public Guid Id { get; set; }
    public Guid ContractorId { get; set; }
    public Guid WorkOrderId { get; set; }
    public string ContractorName { get; set; } = string.Empty;
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string WorkOrderTitle { get; set; } = string.Empty;
    public DateTime AssignedDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? WorkPerformed { get; set; }
    public string? PartsUsed { get; set; }
    public int? QualityRating { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for assigning work orders to contractors
/// </summary>
public class AssignWorkOrderToContractorDto
{
    [Required]
    public Guid ContractorId { get; set; }

    [Required]
    public Guid WorkOrderId { get; set; }

    public decimal? EstimatedCost { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating contractor work order progress
/// </summary>
public class UpdateContractorWorkOrderDto
{
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    public decimal? ActualCost { get; set; }

    [StringLength(2000)]
    public string? WorkPerformed { get; set; }

    public string? PartsUsed { get; set; }

    [Range(1, 5)]
    public int? QualityRating { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Contractor Invoice DTOs

/// <summary>
/// Contractor invoice DTO
/// </summary>
public class ContractorInvoiceDto
{
    public Guid Id { get; set; }
    public Guid ContractorId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string ContractorName { get; set; } = string.Empty;
    public string? WorkOrderNumber { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string LineItems { get; set; } = "[]";
    public string? AttachmentPaths { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? ApprovedByName { get; set; }

    // Calculated fields
    public int DaysOverdue { get; set; }
    public bool IsOverdue { get; set; }
    public int ExpenseCount { get; set; }
}

/// <summary>
/// DTO for creating contractor invoices
/// </summary>
public class CreateContractorInvoiceDto
{
    [Required]
    public Guid ContractorId { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Required]
    [StringLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    public DateTime InvoiceDate { get; set; }

    public DateTime? DueDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    public decimal? TaxAmount { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public string LineItems { get; set; } = "[]";
    public string? AttachmentPaths { get; set; }
}

/// <summary>
/// DTO for updating contractor invoices
/// </summary>
public class UpdateContractorInvoiceDto
{
    [Required]
    [StringLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    public DateTime InvoiceDate { get; set; }

    public DateTime? DueDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    public decimal? TaxAmount { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public string LineItems { get; set; } = "[]";
    public string? AttachmentPaths { get; set; }
}

/// <summary>
/// DTO for approving/rejecting invoices
/// </summary>
public class ProcessContractorInvoiceDto
{
    [Required]
    [StringLength(20)]
    public string Action { get; set; } = string.Empty; // Approve, Reject

    [StringLength(1000)]
    public string? Comments { get; set; }
}

#endregion

#region Contractor Expense DTOs

/// <summary>
/// Contractor expense DTO
/// </summary>
public class ContractorExpenseDto
{
    public Guid Id { get; set; }
    public Guid ContractorInvoiceId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string ExpenseType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitRate { get; set; }
    public string? ReceiptPath { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovedByName { get; set; }
}

/// <summary>
/// DTO for creating contractor expenses
/// </summary>
public class CreateContractorExpenseDto
{
    [Required]
    public Guid ContractorInvoiceId { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Required]
    [StringLength(50)]
    public string ExpenseType { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; }

    public decimal? Quantity { get; set; }

    [StringLength(20)]
    public string? Unit { get; set; }

    public decimal? UnitRate { get; set; }

    [StringLength(500)]
    public string? ReceiptPath { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating contractor expenses
/// </summary>
public class UpdateContractorExpenseDto
{
    [Required]
    [StringLength(50)]
    public string ExpenseType { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; }

    public decimal? Quantity { get; set; }

    [StringLength(20)]
    public string? Unit { get; set; }

    public decimal? UnitRate { get; set; }

    [StringLength(500)]
    public string? ReceiptPath { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for approving/rejecting expenses
/// </summary>
public class ProcessContractorExpenseDto
{
    [Required]
    [StringLength(20)]
    public string Action { get; set; } = string.Empty; // Approve, Reject

    [StringLength(500)]
    public string? Comments { get; set; }
}

#endregion

#region Contractor Performance DTOs

/// <summary>
/// Contractor performance review DTO
/// </summary>
public class ContractorPerformanceReviewDto
{
    public Guid Id { get; set; }
    public Guid ContractorId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string ContractorName { get; set; } = string.Empty;
    public string? WorkOrderNumber { get; set; }
    public DateTime ReviewDate { get; set; }
    public string ReviewedByName { get; set; } = string.Empty;
    public int OverallRating { get; set; }
    public int? QualityRating { get; set; }
    public int? TimelinessRating { get; set; }
    public int? CommunicationRating { get; set; }
    public int? CostRating { get; set; }
    public string? Comments { get; set; }
    public string? Recommendations { get; set; }
    public bool WouldRecommend { get; set; }
}

/// <summary>
/// DTO for creating performance reviews
/// </summary>
public class CreateContractorPerformanceReviewDto
{
    [Required]
    public Guid ContractorId { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Required]
    public DateTime ReviewDate { get; set; }

    [Required]
    [Range(1, 5)]
    public int OverallRating { get; set; }

    [Range(1, 5)]
    public int? QualityRating { get; set; }

    [Range(1, 5)]
    public int? TimelinessRating { get; set; }

    [Range(1, 5)]
    public int? CommunicationRating { get; set; }

    [Range(1, 5)]
    public int? CostRating { get; set; }

    [StringLength(2000)]
    public string? Comments { get; set; }

    [StringLength(1000)]
    public string? Recommendations { get; set; }

    public bool WouldRecommend { get; set; } = true;
}

#endregion

#region Filter and List DTOs

/// <summary>
/// Filter DTO for contractor queries
/// </summary>
public class ContractorFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? Capability { get; set; }
    public string? ServiceArea { get; set; }
    public double? MinRating { get; set; }
    public bool? HasActiveWork { get; set; }
    public string SortBy { get; set; } = "Name";
    public bool SortDescending { get; set; } = false;
}

/// <summary>
/// Filter DTO for contractor invoices
/// </summary>
public class ContractorInvoiceFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public Guid? ContractorId { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsOverdue { get; set; }
    public string SortBy { get; set; } = "InvoiceDate";
    public bool SortDescending { get; set; } = true;
}

#endregion

#region Summary and Analytics DTOs

/// <summary>
/// Contractor performance summary
/// </summary>
public class ContractorSummaryDto
{
    public int TotalContractors { get; set; }
    public int ActiveContractors { get; set; }
    public int ContractorsWithActiveWork { get; set; }
    public decimal TotalPendingInvoices { get; set; }
    public decimal TotalOverdueInvoices { get; set; }
    public double AverageContractorRating { get; set; }
    public int WorkOrdersInProgress { get; set; }
    public int WorkOrdersCompleted { get; set; }
}

/// <summary>
/// Contractor cost analysis
/// </summary>
public class ContractorCostAnalysisDto
{
    public decimal TotalCosts { get; set; }
    public decimal LaborCosts { get; set; }
    public decimal MaterialCosts { get; set; }
    public decimal TravelCosts { get; set; }
    public decimal FuelCosts { get; set; }
    public decimal OtherExpenses { get; set; }
    public Dictionary<string, decimal> CostsByContractor { get; set; } = new();
    public Dictionary<string, decimal> CostsByMonth { get; set; } = new();
}

#endregion
