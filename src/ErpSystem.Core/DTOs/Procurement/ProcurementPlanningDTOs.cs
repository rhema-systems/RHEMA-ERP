using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

#region Procurement Plan DTOs

/// <summary>
/// Procurement plan list DTO
/// </summary>
public class ProcurementPlanDto
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int FiscalYear { get; set; }
    public DateTime PlanStartDate { get; set; }
    public DateTime PlanEndDate { get; set; }
    public int PlanDurationYears { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal TotalEstimatedBudget { get; set; }
    public decimal ApprovedBudget { get; set; }
    public string Currency { get; set; } = "USD";
    public string? PreparedByName { get; set; }
    public DateTime? PreparedDate { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public int RevisionNumber { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Procurement plan detail DTO
/// </summary>
public class ProcurementPlanDetailDto : ProcurementPlanDto
{
    public Guid? PreparedById { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewComments { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovalComments { get; set; }
    public Guid? PreviousVersionId { get; set; }
    public string? Notes { get; set; }
    public List<ProcurementPlanItemDto> Items { get; set; } = new();
    public List<ProcurementBudgetDto> Budgets { get; set; } = new();
    public List<ProcurementScheduleDto> Schedules { get; set; } = new();
}

/// <summary>
/// Create procurement plan DTO
/// </summary>
public class CreateProcurementPlanDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public int FiscalYear { get; set; }

    [Required]
    public DateTime PlanStartDate { get; set; }

    [Required]
    public DateTime PlanEndDate { get; set; }

    public int PlanDurationYears { get; set; } = 1;

    public decimal TotalEstimatedBudget { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<CreateProcurementPlanItemDto> Items { get; set; } = new();
}

/// <summary>
/// Update procurement plan DTO
/// </summary>
public class UpdateProcurementPlanDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public int FiscalYear { get; set; }

    [Required]
    public DateTime PlanStartDate { get; set; }

    [Required]
    public DateTime PlanEndDate { get; set; }

    public int PlanDurationYears { get; set; } = 1;

    public decimal TotalEstimatedBudget { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Submit procurement plan for approval
/// </summary>
public class SubmitProcurementPlanDto
{
    public Guid? ReviewerId { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

/// <summary>
/// Approve/Reject procurement plan
/// </summary>
public class ApproveProcurementPlanDto
{
    [Required]
    public bool IsApproved { get; set; }

    public decimal? ApprovedBudget { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }

    /// <summary>
    /// If true, automatically generates procurement schedules for each plan item when approved
    /// </summary>
    public bool AutoGenerateSchedules { get; set; } = true;

    /// <summary>
    /// Optional: Specific budget ID to link. If null, system auto-matches by department + fiscal year
    /// </summary>
    public Guid? BudgetId { get; set; }

    /// <summary>
    /// If true, automatically links to matching budget on approval
    /// </summary>
    public bool AutoLinkBudget { get; set; } = true;
}

#endregion

#region Procurement Plan Item DTOs

/// <summary>
/// Procurement plan item DTO
/// </summary>
public class ProcurementPlanItemDto
{
    public Guid Id { get; set; }
    public Guid ProcurementPlanId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string? InventoryItemCode { get; set; }
    public string? InventoryItemName { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public string? Specifications { get; set; }
    public string? ItemCategory { get; set; }
    public decimal EstimatedQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal EstimatedUnitPrice { get; set; }
    public decimal EstimatedTotalCost { get; set; }
    public string Currency { get; set; } = "USD";
    public string Priority { get; set; } = "Medium";
    public bool IsCritical { get; set; }
    public DateTime? RequiredDate { get; set; }
    public int? PlannedProcurementMonth { get; set; }
    public string? PlannedQuarter { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public string? AlternativeSuppliers { get; set; }
    public string? Justification { get; set; }
    public string Status { get; set; } = "Planned";
    public string? ProcurementMethod { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? TenderId { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// List of suppliers for this plan item
    /// </summary>
    public List<ProcurementPlanItemSupplierDto> ItemSuppliers { get; set; } = new();
}

/// <summary>
/// Create procurement plan item DTO
/// </summary>
public class CreateProcurementPlanItemDto
{
    /// <summary>
    /// Optional reference to inventory item (null for non-inventory items)
    /// </summary>
    public Guid? InventoryItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemDescription { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Specifications { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    [Required]
    public decimal EstimatedQuantity { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    public decimal EstimatedUnitPrice { get; set; }

    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    public bool IsCritical { get; set; } = false;

    public DateTime? RequiredDate { get; set; }

    public int? PlannedProcurementMonth { get; set; }

    [MaxLength(10)]
    public string? PlannedQuarter { get; set; }

    public Guid? PreferredSupplierId { get; set; }

    [MaxLength(500)]
    public string? PreferredSupplierName { get; set; }

    [MaxLength(1000)]
    public string? AlternativeSuppliers { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    [MaxLength(30)]
    public string? ProcurementMethod { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// List of suppliers for this plan item
    /// </summary>
    public List<CreateProcurementPlanItemSupplierDto> ItemSuppliers { get; set; } = new();
}

/// <summary>
/// Update procurement plan item DTO
/// </summary>
public class UpdateProcurementPlanItemDto : CreateProcurementPlanItemDto
{
    public Guid Id { get; set; }
}

/// <summary>
/// Update item status DTO
/// </summary>
public class UpdateItemStatusDto
{
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO for procurement plan item supplier (many-to-many relationship)
/// </summary>
public class ProcurementPlanItemSupplierDto
{
    public Guid Id { get; set; }
    public Guid ProcurementPlanItemId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public int Priority { get; set; }
    public decimal? QuotedUnitPrice { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? SupplierItemCode { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating/updating procurement plan item supplier
/// </summary>
public class CreateProcurementPlanItemSupplierDto
{
    [Required]
    public Guid SupplierId { get; set; }

    public bool IsPreferred { get; set; } = false;
    public int Priority { get; set; } = 1;
    public decimal? QuotedUnitPrice { get; set; }
    public int? LeadTimeDays { get; set; }

    [MaxLength(100)]
    public string? SupplierItemCode { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for converting a plan item to a tender
/// </summary>
public class ConvertPlanItemToTenderDto
{
    [Required]
    public Guid PlanItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TenderTitle { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? TenderDescription { get; set; }

    /// <summary>
    /// Tender type: RFQ, RFP, ITB
    /// </summary>
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ";

    public DateTime? SubmissionDeadline { get; set; }

    public DateTime? OpeningDate { get; set; }

    /// <summary>
    /// If true, auto-generate procurement schedule for this tender
    /// </summary>
    public bool CreateSchedule { get; set; } = true;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for converting a plan item to a purchase order (direct purchase)
/// </summary>
public class ConvertPlanItemToPurchaseOrderDto
{
    [Required]
    public Guid PlanItemId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    public DateTime? RequiredDate { get; set; }

    public string? PaymentTerms { get; set; }

    public string? ShippingTerms { get; set; }

    public Guid? DeliveryWarehouseId { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? DeliveryInstructions { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// If true, auto-generate procurement schedule for this PO
    /// </summary>
    public bool CreateSchedule { get; set; } = true;
}

/// <summary>
/// Result DTO for plan item conversion
/// </summary>
public class PlanItemConversionResultDto
{
    public Guid PlanItemId { get; set; }
    public string PlanItemDescription { get; set; } = string.Empty;
    public string ConversionType { get; set; } = string.Empty; // "Tender", "PurchaseOrder"
    public Guid? TenderId { get; set; }
    public string? TenderNumber { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public Guid? ScheduleId { get; set; }
    public string? ScheduleCode { get; set; }
    public string NewItemStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

#endregion

#region Procurement Budget DTOs

/// <summary>
/// Procurement budget DTO
/// </summary>
public class ProcurementBudgetDto
{
    public Guid Id { get; set; }
    public string BudgetCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? ProcurementPlanId { get; set; }
    public int FiscalYear { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UtilizedAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Draft";
    public string ControlLevel { get; set; } = "Warning";
    public decimal WarningThresholdPercent { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public decimal UtilizationPercent { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Procurement budget detail DTO
/// </summary>
public class ProcurementBudgetDetailDto : ProcurementBudgetDto
{
    public Guid? ApprovedById { get; set; }
    public string? Notes { get; set; }
    public List<ProcurementBudgetAllocationDto> Allocations { get; set; } = new();
    public List<ProcurementBudgetRevisionDto> Revisions { get; set; } = new();
}

/// <summary>
/// Create procurement budget DTO
/// </summary>
public class CreateProcurementBudgetDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? ProcurementPlanId { get; set; }

    [Required]
    public int FiscalYear { get; set; }

    [Required]
    public decimal AllocatedAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(20)]
    public string ControlLevel { get; set; } = "Warning";

    public decimal WarningThresholdPercent { get; set; } = 80;

    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<CreateProcurementBudgetAllocationDto> Allocations { get; set; } = new();
}

/// <summary>
/// Procurement budget allocation DTO
/// </summary>
public class ProcurementBudgetAllocationDto
{
    public Guid Id { get; set; }
    public Guid ProcurementBudgetId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryDescription { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UtilizedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal UtilizationPercent { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Create budget allocation DTO
/// </summary>
public class CreateProcurementBudgetAllocationDto
{
    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? CategoryDescription { get; set; }

    [Required]
    public decimal AllocatedAmount { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Procurement budget revision DTO
/// </summary>
public class ProcurementBudgetRevisionDto
{
    public Guid Id { get; set; }
    public Guid ProcurementBudgetId { get; set; }
    public int RevisionNumber { get; set; }
    public string RevisionType { get; set; } = "Increase";
    public decimal PreviousAmount { get; set; }
    public decimal NewAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string? Reason { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create budget revision DTO
/// </summary>
public class CreateProcurementBudgetRevisionDto
{
    [Required]
    [MaxLength(30)]
    public string RevisionType { get; set; } = "Increase";

    [Required]
    public decimal NewAmount { get; set; }

    [MaxLength(2000)]
    public string? Reason { get; set; }
}

#endregion

#region Procurement Schedule DTOs

/// <summary>
/// Procurement schedule DTO
/// </summary>
public class ProcurementScheduleDto
{
    public Guid Id { get; set; }
    public string ScheduleCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProcurementPlanId { get; set; }
    public string? ProcurementPlanNumber { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string ScheduleType { get; set; } = "Tender";
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public bool IsOptimalTiming { get; set; }
    public string? TimingRationale { get; set; }
    public bool ConsiderSeasonalPricing { get; set; }
    public bool ConsiderCashFlow { get; set; }
    public string Status { get; set; } = "Planned";
    public bool ConsolidationOpportunity { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Procurement schedule detail DTO with additional information
/// </summary>
public class ProcurementScheduleDetailDto : ProcurementScheduleDto
{
    public string? SeasonalNotes { get; set; }
    public string? CashFlowNotes { get; set; }
    public string? StorageLimitations { get; set; }
    public string? ConsolidationNotes { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Create procurement schedule DTO
/// </summary>
public class CreateProcurementScheduleDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? ProcurementPlanId { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public Guid? DepartmentId { get; set; }

    [MaxLength(30)]
    public string ScheduleType { get; set; } = "Tender";

    [Required]
    public DateTime PlannedStartDate { get; set; }

    [Required]
    public DateTime PlannedEndDate { get; set; }

    public bool IsOptimalTiming { get; set; } = true;

    [MaxLength(500)]
    public string? TimingRationale { get; set; }

    public bool ConsiderSeasonalPricing { get; set; } = false;

    [MaxLength(500)]
    public string? SeasonalNotes { get; set; }

    public bool ConsiderCashFlow { get; set; } = false;

    [MaxLength(500)]
    public string? CashFlowNotes { get; set; }

    [MaxLength(500)]
    public string? StorageLimitations { get; set; }

    public bool ConsolidationOpportunity { get; set; } = false;

    [MaxLength(500)]
    public string? ConsolidationNotes { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Market Analysis DTOs

/// <summary>
/// Market analysis DTO
/// </summary>
public class MarketAnalysisDto
{
    public Guid Id { get; set; }
    public string AnalysisCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ItemCategory { get; set; }
    public string? ItemDescription { get; set; }
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public decimal HistoricalAveragePrice { get; set; }
    public decimal CurrentMarketPrice { get; set; }
    public decimal ForecastedPrice { get; set; }
    public string PriceTrend { get; set; } = "Stable";
    public decimal PriceChangePercent { get; set; }
    public string Currency { get; set; } = "USD";
    public string MarketRiskLevel { get; set; } = "Medium";
    public string? RiskFactors { get; set; }
    public string? Opportunities { get; set; }
    public string? RecommendedStrategy { get; set; }
    public string? StrategyRationale { get; set; }
    public int? OptimalPurchaseMonth { get; set; }
    public string? SeasonalPattern { get; set; }
    public string? PreparedByName { get; set; }
    public DateTime? PreparedDate { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Market analysis detail DTO
/// </summary>
public class MarketAnalysisDetailDto : MarketAnalysisDto
{
    public Guid? PreparedById { get; set; }
    public string? Notes { get; set; }
    public List<PriceHistoryDto> PriceHistories { get; set; } = new();
}

/// <summary>
/// Price trend analysis DTO
/// </summary>
public class PriceTrendDto
{
    public Guid MarketAnalysisId { get; set; }
    public string? ItemCategory { get; set; }
    public string? ItemDescription { get; set; }
    public string Trend { get; set; } = "Stable"; // Increasing, Decreasing, Stable, Volatile
    public decimal ChangePercent { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal ForecastedPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int AnalysisPeriodMonths { get; set; }
    public DateTime AnalysisDate { get; set; }
    public List<PriceHistoryDto> PriceHistory { get; set; } = new();
}

/// <summary>
/// Create market analysis DTO
/// </summary>
public class CreateMarketAnalysisDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    [MaxLength(200)]
    public string? ItemDescription { get; set; }

    [Required]
    public DateTime AnalysisPeriodStart { get; set; }

    [Required]
    public DateTime AnalysisPeriodEnd { get; set; }

    public decimal HistoricalAveragePrice { get; set; }
    public decimal CurrentMarketPrice { get; set; }
    public decimal ForecastedPrice { get; set; }

    [MaxLength(20)]
    public string PriceTrend { get; set; } = "Stable";

    public decimal PriceChangePercent { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(20)]
    public string MarketRiskLevel { get; set; } = "Medium";

    [MaxLength(2000)]
    public string? RiskFactors { get; set; }

    [MaxLength(2000)]
    public string? Opportunities { get; set; }

    [MaxLength(50)]
    public string? RecommendedStrategy { get; set; }

    [MaxLength(2000)]
    public string? StrategyRationale { get; set; }

    public int? OptimalPurchaseMonth { get; set; }

    [MaxLength(500)]
    public string? SeasonalPattern { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Price history DTO
/// </summary>
public class PriceHistoryDto
{
    public Guid Id { get; set; }
    public Guid? MarketAnalysisId { get; set; }
    public string? ItemCategory { get; set; }
    public string? ItemDescription { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime PriceDate { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string UnitOfMeasure { get; set; } = "EA";
    public string PriceSource { get; set; } = "Quote";
    public Guid? PurchaseOrderId { get; set; }
    public Guid? TenderId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Create price history DTO
/// </summary>
public class CreatePriceHistoryDto
{
    public Guid? MarketAnalysisId { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    [MaxLength(200)]
    public string? ItemDescription { get; set; }

    public Guid? SupplierId { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [Required]
    public DateTime PriceDate { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    [MaxLength(30)]
    public string PriceSource { get; set; } = "Quote";

    public Guid? PurchaseOrderId { get; set; }
    public Guid? TenderId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Supplier Consolidation DTOs

/// <summary>
/// Supplier consolidation DTO
/// </summary>
public class SupplierConsolidationDto
{
    public Guid Id { get; set; }
    public string ConsolidationCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ItemCategory { get; set; }
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public int CurrentSupplierCount { get; set; }
    public int RecommendedSupplierCount { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal PotentialSavings { get; set; }
    public string Currency { get; set; } = "USD";
    public string OpportunityLevel { get; set; } = "Medium";
    public string RecommendedStrategy { get; set; } = "Maintain";
    public string? StrategyRationale { get; set; }
    public string? PreparedByName { get; set; }
    public DateTime? PreparedDate { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Supplier consolidation detail DTO
/// </summary>
public class SupplierConsolidationDetailDto : SupplierConsolidationDto
{
    public Guid? PreparedById { get; set; }
    public string? PreferredSupplierIds { get; set; }
    public string? SuppliersToPhaseOut { get; set; }
    public string? ImplementationPlan { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Create supplier consolidation DTO
/// </summary>
public class CreateSupplierConsolidationDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    [Required]
    public DateTime AnalysisPeriodStart { get; set; }

    [Required]
    public DateTime AnalysisPeriodEnd { get; set; }

    public int CurrentSupplierCount { get; set; }
    public int RecommendedSupplierCount { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal PotentialSavings { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(20)]
    public string OpportunityLevel { get; set; } = "Medium";

    [MaxLength(30)]
    public string RecommendedStrategy { get; set; } = "Maintain";

    [MaxLength(2000)]
    public string? StrategyRationale { get; set; }

    public string? PreferredSupplierIds { get; set; }
    public string? SuppliersToPhaseOut { get; set; }

    [MaxLength(2000)]
    public string? ImplementationPlan { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Emergency Planning DTOs

/// <summary>
/// Emergency procurement plan DTO
/// </summary>
public class EmergencyProcurementPlanDto
{
    public Guid Id { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string EmergencyType { get; set; } = "SupplyShortage";
    public string CriticalityLevel { get; set; } = "High";
    public decimal BudgetReserve { get; set; }
    public decimal UtilizedReserve { get; set; }
    public decimal RemainingReserve { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal MaxApprovalLimit { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string Status { get; set; } = "Draft";
    public int CriticalItemCount { get; set; }
    public int EmergencySupplierCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Emergency procurement plan detail DTO
/// </summary>
public class EmergencyProcurementPlanDetailDto : EmergencyProcurementPlanDto
{
    public Guid? ApprovedById { get; set; }
    public string? RapidProcurementProcess { get; set; }
    public string? EscalationContacts { get; set; }
    public string? Notes { get; set; }
    public List<EmergencyProcurementItemDto> CriticalItems { get; set; } = new();
    public List<EmergencySupplierDto> EmergencySuppliers { get; set; } = new();
}

/// <summary>
/// Create emergency procurement plan DTO
/// </summary>
public class CreateEmergencyProcurementPlanDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? DepartmentId { get; set; }

    [MaxLength(50)]
    public string EmergencyType { get; set; } = "SupplyShortage";

    [MaxLength(20)]
    public string CriticalityLevel { get; set; } = "High";

    public decimal BudgetReserve { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public decimal MaxApprovalLimit { get; set; }

    [MaxLength(4000)]
    public string? RapidProcurementProcess { get; set; }

    public string? EscalationContacts { get; set; }

    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? NextReviewDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<CreateEmergencyProcurementItemDto> CriticalItems { get; set; } = new();
    public List<CreateEmergencySupplierDto> EmergencySuppliers { get; set; } = new();
}

/// <summary>
/// Emergency procurement item DTO
/// </summary>
public class EmergencyProcurementItemDto
{
    public Guid Id { get; set; }
    public Guid EmergencyProcurementPlanId { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public string? Specifications { get; set; }
    public string? ItemCategory { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public decimal CurrentStockLevel { get; set; }
    public decimal EmergencyOrderQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public int MaxLeadTimeDays { get; set; }
    public string CriticalityLevel { get; set; } = "Critical";
    public string? AlternativeItems { get; set; }
    public string? Notes { get; set; }
    public bool IsStockLow { get; set; }
}

/// <summary>
/// Create emergency procurement item DTO
/// </summary>
public class CreateEmergencyProcurementItemDto
{
    [Required]
    [MaxLength(200)]
    public string ItemDescription { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Specifications { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    public decimal MinimumStockLevel { get; set; }
    public decimal CurrentStockLevel { get; set; }
    public decimal EmergencyOrderQuantity { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    public int MaxLeadTimeDays { get; set; } = 3;

    [MaxLength(20)]
    public string CriticalityLevel { get; set; } = "Critical";

    [MaxLength(1000)]
    public string? AlternativeItems { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Emergency supplier DTO
/// </summary>
public class EmergencySupplierDto
{
    public Guid Id { get; set; }
    public Guid EmergencyProcurementPlanId { get; set; }
    public Guid? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Address { get; set; }
    public string? ItemsProvided { get; set; }
    public int ResponseTimeHours { get; set; }
    public int Priority { get; set; }
    public bool HasEmergencyContract { get; set; }
    public DateTime? ContractExpiryDate { get; set; }
    public string? PaymentTerms { get; set; }
    public DateTime? LastVerifiedDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Create emergency supplier DTO
/// </summary>
public class CreateEmergencySupplierDto
{
    public Guid? SupplierId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ContactPerson { get; set; }

    [MaxLength(100)]
    public string? ContactPhone { get; set; }

    [MaxLength(100)]
    public string? ContactEmail { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public string? ItemsProvided { get; set; }

    public int ResponseTimeHours { get; set; } = 24;

    public int Priority { get; set; } = 1;

    public bool HasEmergencyContract { get; set; } = false;

    public DateTime? ContractExpiryDate { get; set; }

    [MaxLength(100)]
    public string? PaymentTerms { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Budget Validation DTOs

/// <summary>
/// Result of budget validation before procurement execution
/// </summary>
public class BudgetValidationResultDto
{
    public bool IsValid { get; set; }
    public bool HasBudget { get; set; }
    public string? BudgetCode { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UtilizedAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal RequestedAmount { get; set; }
    public string? Currency { get; set; }
    public string? ControlLevel { get; set; }
    public string? Message { get; set; }
    public List<string> Warnings { get; set; } = new();
}

#endregion

