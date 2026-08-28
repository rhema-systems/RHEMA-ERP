using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Entities.Procurement;

#region Procurement Plan

/// <summary>
/// Annual or multi-year procurement plan for a department
/// </summary>
public class ProcurementPlan : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PlanNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    /// <summary>
    /// Fiscal year for this plan (e.g., 2024)
    /// </summary>
    [Required]
    public int FiscalYear { get; set; }

    /// <summary>
    /// Planning cycle: Annual, Quarterly, MultiYear
    /// </summary>
    [MaxLength(20)]
    public string PlanningCycle { get; set; } = "Annual";

    /// <summary>
    /// Applicable quarter for quarterly plans (Q1, Q2, Q3, Q4)
    /// </summary>
    [MaxLength(10)]
    public string? PlanningQuarter { get; set; }

    /// <summary>
    /// Start date of the planning period
    /// </summary>
    public DateTime PlanStartDate { get; set; }

    /// <summary>
    /// End date of the planning period
    /// </summary>
    public DateTime PlanEndDate { get; set; }

    /// <summary>
    /// For multi-year planning - number of years covered
    /// </summary>
    public int PlanDurationYears { get; set; } = 1;

    [MaxLength(30)]
    public string Status { get; set; } = "Draft"; // Draft, Submitted, UnderReview, Approved, Rejected, Revised, Active, Completed, Cancelled

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalEstimatedBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ApprovedBudget { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public Guid? PreparedById { get; set; }
    public DateTime? PreparedDate { get; set; }

    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewComments { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovalComments { get; set; }

    public Guid? PublishedById { get; set; }
    public DateTime? PublishedDate { get; set; }

    [MaxLength(2000)]
    public string? PublishComments { get; set; }

    public int RevisionNumber { get; set; } = 1;
    public Guid? PreviousVersionId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Department Department { get; set; } = null!;
    public virtual ApplicationUser? PreparedBy { get; set; }
    public virtual ApplicationUser? ReviewedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? PublishedBy { get; set; }
    public virtual ProcurementPlan? PreviousVersion { get; set; }
    public virtual ICollection<ProcurementPlanItem> Items { get; set; } = new List<ProcurementPlanItem>();
    public virtual ICollection<ProcurementBudget> Budgets { get; set; } = new List<ProcurementBudget>();
    public virtual ICollection<ProcurementSchedule> Schedules { get; set; } = new List<ProcurementSchedule>();
}

/// <summary>
/// Individual item in a procurement plan
/// </summary>
public class ProcurementPlanItem : TenantEntity
{
    [Required]
    public Guid ProcurementPlanId { get; set; }

    /// <summary>
    /// Optional reference to inventory item (null for non-inventory items)
    /// </summary>
    public Guid? InventoryItemId { get; set; }

    public Guid? ProcurementBudgetId { get; set; }

    public Guid? ProcurementBudgetAllocationId { get; set; }

    public Guid? MarketAnalysisId { get; set; }

    [MaxLength(50)]
    public string? BudgetLineCode { get; set; }

    [MaxLength(100)]
    public string? BudgetCategoryName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ApprovedBudgetAmount { get; set; }

    [MaxLength(500)]
    public string? BudgetNotes { get; set; }

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

    [Column(TypeName = "decimal(18,4)")]
    public decimal EstimatedUnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedTotalCost { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Priority level: Critical, High, Medium, Low
    /// </summary>
    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Is this item critical for operations?
    /// </summary>
    public bool IsCritical { get; set; } = false;

    /// <summary>
    /// Required delivery date
    /// </summary>
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// Planned procurement month (1-12)
    /// </summary>
    public int? PlannedProcurementMonth { get; set; }

    /// <summary>
    /// Quarter (Q1, Q2, Q3, Q4)
    /// </summary>
    [MaxLength(10)]
    public string? PlannedQuarter { get; set; }

    /// <summary>
    /// Preferred supplier if known
    /// </summary>
    public Guid? PreferredSupplierId { get; set; }

    [MaxLength(500)]
    public string? PreferredSupplierName { get; set; }

    [MaxLength(1000)]
    public string? AlternativeSuppliers { get; set; } // JSON array

    [MaxLength(2000)]
    public string? Justification { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Planned"; // Planned, Approved, InProgress, Procured, Cancelled

    /// <summary>
    /// Procurement method: DirectPurchase, RFQ, Tender, Contract
    /// </summary>
    [MaxLength(30)]
    public string? ProcurementMethod { get; set; }

    /// <summary>
    /// Link to actual purchase order or tender when procured
    /// </summary>
    public Guid? PurchaseOrderId { get; set; }
    public Guid? TenderId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual ProcurementPlan ProcurementPlan { get; set; } = null!;
    public virtual ProcurementBudget? ProcurementBudget { get; set; }
    public virtual ProcurementBudgetAllocation? ProcurementBudgetAllocation { get; set; }
    public virtual MarketAnalysis? MarketAnalysis { get; set; }
    public virtual Supplier? PreferredSupplier { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }
    public virtual ICollection<ProcurementPlanItemSupplier> ItemSuppliers { get; set; } = new List<ProcurementPlanItemSupplier>();
}

/// <summary>
/// Many-to-many relationship between ProcurementPlanItem and Supplier
/// Allows multiple suppliers per plan item for budgeting purposes
/// </summary>
public class ProcurementPlanItemSupplier : TenantEntity
{
    [Required]
    public Guid ProcurementPlanItemId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    /// <summary>
    /// Is this the preferred/primary supplier for this item?
    /// </summary>
    public bool IsPreferred { get; set; } = false;

    /// <summary>
    /// Priority order (1 = highest priority)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Quoted or estimated unit price from this supplier
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? QuotedUnitPrice { get; set; }

    /// <summary>
    /// Lead time in days from this supplier
    /// </summary>
    public int? LeadTimeDays { get; set; }

    /// <summary>
    /// Supplier's item code/SKU for this item
    /// </summary>
    [MaxLength(100)]
    public string? SupplierItemCode { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual ProcurementPlanItem ProcurementPlanItem { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
}

#endregion

#region Procurement Budget

/// <summary>
/// Departmental procurement budget allocation
/// </summary>
public class ProcurementBudget : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string BudgetCode { get; set; } = string.Empty;

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

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UtilizedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommittedAmount { get; set; } // POs issued but not yet received

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReservedAmount { get; set; } // Approved PR demand not yet issued as a PO/contract

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(30)]
    public string Status { get; set; } = "Draft"; // Draft, Submitted, Approved, Active, Frozen, Closed

    /// <summary>
    /// Budget control level: Strict, Warning, Advisory
    /// </summary>
    [MaxLength(20)]
    public string ControlLevel { get; set; } = "Warning";

    /// <summary>
    /// Threshold percentage for budget warning alerts
    /// </summary>
    public decimal WarningThresholdPercent { get; set; } = 80;

    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Department Department { get; set; } = null!;
    public virtual ProcurementPlan? ProcurementPlan { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ICollection<ProcurementBudgetAllocation> Allocations { get; set; } = new List<ProcurementBudgetAllocation>();
    public virtual ICollection<ProcurementBudgetRevision> Revisions { get; set; } = new List<ProcurementBudgetRevision>();
    public virtual ICollection<ProcurementBudgetCommitment> Commitments { get; set; } = new List<ProcurementBudgetCommitment>();

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Budget allocation by item category
/// </summary>
public class ProcurementBudgetAllocation : TenantEntity
{
    [Required]
    public Guid ProcurementBudgetId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? CategoryDescription { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UtilizedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual ProcurementBudget ProcurementBudget { get; set; } = null!;
}

/// <summary>
/// Budget revision history
/// </summary>
public class ProcurementBudgetRevision : TenantEntity
{
    [Required]
    public Guid ProcurementBudgetId { get; set; }

    public int RevisionNumber { get; set; }

    [MaxLength(30)]
    public string RevisionType { get; set; } = "Increase"; // Increase, Decrease, Reallocation, Transfer

    [Column(TypeName = "decimal(18,2)")]
    public decimal PreviousAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NewAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ChangeAmount { get; set; }

    [MaxLength(2000)]
    public string? Reason { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

    // Navigation Properties
    public virtual ProcurementBudget ProcurementBudget { get; set; } = null!;
    public virtual ApplicationUser? ApprovedBy { get; set; }
}

#endregion

#region Procurement Scheduling

/// <summary>
/// Procurement calendar and scheduling
/// </summary>
public class ProcurementSchedule : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ScheduleCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? ProcurementPlanId { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Schedule type: Tender, RFQ, DirectPurchase, Contract, Delivery
    /// </summary>
    [MaxLength(30)]
    public string ScheduleType { get; set; } = "Tender";

    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    /// <summary>
    /// Is this timing optimal based on market conditions?
    /// </summary>
    public bool IsOptimalTiming { get; set; } = true;

    [MaxLength(500)]
    public string? TimingRationale { get; set; }

    /// <summary>
    /// Consider seasonal pricing variations
    /// </summary>
    public bool ConsiderSeasonalPricing { get; set; } = false;

    [MaxLength(500)]
    public string? SeasonalNotes { get; set; }

    /// <summary>
    /// Consider cash flow for timing
    /// </summary>
    public bool ConsiderCashFlow { get; set; } = false;

    [MaxLength(500)]
    public string? CashFlowNotes { get; set; }

    /// <summary>
    /// Storage/inventory limitations to consider
    /// </summary>
    [MaxLength(500)]
    public string? StorageLimitations { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Planned"; // Planned, InProgress, Completed, Postponed, Cancelled

    /// <summary>
    /// Can this be consolidated with other department needs?
    /// </summary>
    public bool ConsolidationOpportunity { get; set; } = false;

    [MaxLength(500)]
    public string? ConsolidationNotes { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual ProcurementPlan? ProcurementPlan { get; set; }
    public virtual ProcurementPlanItem? ProcurementPlanItem { get; set; }
    public virtual Department? Department { get; set; }
}

#endregion

#region Market Analysis

/// <summary>
/// Market analysis and price forecasting
/// </summary>
public class MarketAnalysis : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string AnalysisCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    [MaxLength(200)]
    public string? ItemDescription { get; set; }

    /// <summary>
    /// Analysis period start
    /// </summary>
    public DateTime AnalysisPeriodStart { get; set; }

    /// <summary>
    /// Analysis period end
    /// </summary>
    public DateTime AnalysisPeriodEnd { get; set; }

    /// <summary>
    /// Historical average price
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal HistoricalAveragePrice { get; set; }

    /// <summary>
    /// Previous procurement or market price used for variance analysis
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? PreviousPrice { get; set; }

    /// <summary>
    /// Current market price
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal CurrentMarketPrice { get; set; }

    /// <summary>
    /// Forecasted price
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal ForecastedPrice { get; set; }

    /// <summary>
    /// Price trend: Increasing, Decreasing, Stable, Volatile
    /// </summary>
    [MaxLength(20)]
    public string PriceTrend { get; set; } = "Stable";

    /// <summary>
    /// Price change percentage over analysis period
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal PriceChangePercent { get; set; }

    /// <summary>
    /// Variance between current market price and previous price
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? PriceVariancePercent { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Estimated delivery lead time in days
    /// </summary>
    public int? LeadTimeDays { get; set; }

    /// <summary>
    /// Market availability: High, Medium, Low
    /// </summary>
    [MaxLength(20)]
    public string MarketAvailability { get; set; } = "Medium";

    /// <summary>
    /// Supply risk level: Low, Medium, High
    /// </summary>
    [MaxLength(20)]
    public string SupplyRiskLevel { get; set; } = "Medium";

    /// <summary>
    /// Estimated inflation impact on the planning price
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal InflationImpactPercent { get; set; }

    /// <summary>
    /// Recommended budget adjustment from market analysis
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal RecommendedBudgetAdjustmentPercent { get; set; }

    /// <summary>
    /// Market risk level: Low, Medium, High
    /// </summary>
    [MaxLength(20)]
    public string MarketRiskLevel { get; set; } = "Medium";

    [MaxLength(2000)]
    public string? RiskFactors { get; set; }

    [MaxLength(2000)]
    public string? Opportunities { get; set; }

    /// <summary>
    /// Recommended strategy: BulkPurchase, LongTermContract, SpotPurchase, DeferPurchase
    /// </summary>
    [MaxLength(50)]
    public string? RecommendedStrategy { get; set; }

    [MaxLength(2000)]
    public string? StrategyRationale { get; set; }

    /// <summary>
    /// Best month to purchase based on historical data
    /// </summary>
    public int? OptimalPurchaseMonth { get; set; }

    [MaxLength(500)]
    public string? SeasonalPattern { get; set; }

    public Guid? PreparedById { get; set; }
    public DateTime? PreparedDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft"; // Draft, Published, Archived

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual ApplicationUser? PreparedBy { get; set; }
    public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}

/// <summary>
/// Historical price data for market analysis
/// </summary>
public class PriceHistory : TenantEntity
{
    public Guid? MarketAnalysisId { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    [MaxLength(200)]
    public string? ItemDescription { get; set; }

    public Guid? SupplierId { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    public DateTime PriceDate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Source of price: Quote, Invoice, Contract, MarketData
    /// </summary>
    [MaxLength(30)]
    public string PriceSource { get; set; } = "Quote";

    public Guid? PurchaseOrderId { get; set; }
    public Guid? TenderId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual MarketAnalysis? MarketAnalysis { get; set; }
    public virtual Supplier? Supplier { get; set; }
}

#endregion

#region Supplier Consolidation

/// <summary>
/// Supplier consolidation analysis and strategy
/// </summary>
public class SupplierConsolidation : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ConsolidationCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    /// <summary>
    /// Analysis period start
    /// </summary>
    public DateTime AnalysisPeriodStart { get; set; }

    /// <summary>
    /// Analysis period end
    /// </summary>
    public DateTime AnalysisPeriodEnd { get; set; }

    /// <summary>
    /// Current number of suppliers for this category
    /// </summary>
    public int CurrentSupplierCount { get; set; }

    /// <summary>
    /// Recommended number of suppliers after consolidation
    /// </summary>
    public int RecommendedSupplierCount { get; set; }

    /// <summary>
    /// Total spend in analysis period
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalSpend { get; set; }

    /// <summary>
    /// Potential savings from consolidation
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PotentialSavings { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Consolidation opportunity level: High, Medium, Low
    /// </summary>
    [MaxLength(20)]
    public string OpportunityLevel { get; set; } = "Medium";

    /// <summary>
    /// Recommended strategy: Consolidate, Maintain, Diversify
    /// </summary>
    [MaxLength(30)]
    public string RecommendedStrategy { get; set; } = "Maintain";

    [MaxLength(2000)]
    public string? StrategyRationale { get; set; }

    /// <summary>
    /// JSON array of preferred supplier IDs
    /// </summary>
    public string? PreferredSupplierIds { get; set; }

    /// <summary>
    /// JSON array of suppliers to phase out
    /// </summary>
    public string? SuppliersToPhaseOut { get; set; }

    [MaxLength(2000)]
    public string? ImplementationPlan { get; set; }

    public Guid? PreparedById { get; set; }
    public DateTime? PreparedDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft"; // Draft, UnderReview, Approved, Implementing, Implemented, Completed

    /// <summary>
    /// Date when consolidation was implemented
    /// </summary>
    public DateTime? ImplementationDate { get; set; }

    /// <summary>
    /// Actual savings achieved after implementation
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualSavings { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual ApplicationUser? PreparedBy { get; set; }
}

#endregion

#region Emergency Planning

/// <summary>
/// Emergency procurement contingency plan
/// </summary>
public class EmergencyProcurementPlan : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PlanCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Type of emergency: SupplyShortage, EquipmentFailure, NaturalDisaster, Pandemic, Other
    /// </summary>
    [MaxLength(50)]
    public string EmergencyType { get; set; } = "SupplyShortage";

    /// <summary>
    /// Criticality level: Critical, High, Medium
    /// </summary>
    [MaxLength(20)]
    public string CriticalityLevel { get; set; } = "High";

    /// <summary>
    /// Budget reserve for emergencies
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetReserve { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UtilizedReserve { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Maximum approval authority for emergency purchases
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxApprovalLimit { get; set; }

    /// <summary>
    /// Rapid procurement process description
    /// </summary>
    [MaxLength(4000)]
    public string? RapidProcurementProcess { get; set; }

    /// <summary>
    /// Escalation contacts (JSON)
    /// </summary>
    public string? EscalationContacts { get; set; }

    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft"; // Draft, Active, UnderReview, Expired, Superseded

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Governed emergency-purchase exception lifecycle. The plan remains the
    // aggregate root; requisition, policy, workflow and DMS remain the owners
    // of their respective records.
    public Guid? PurchaseRequisitionId { get; set; }
    public Guid? ExceptionRuleId { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public Guid? ExceptionalSourcingTenderId { get; set; }

    [MaxLength(2000)]
    public string? ExceptionJustification { get; set; }

    [MaxLength(500)]
    public string? EvidenceReference { get; set; }

    public Guid? PreparedById { get; set; }
    public DateTime? PreparedAtUtc { get; set; }
    public DateTime? SubmittedForAuditAtUtc { get; set; }
    public Guid? InternalAuditVouchedById { get; set; }
    public DateTime? InternalAuditVouchedAtUtc { get; set; }

    [MaxLength(1000)]
    public string? InternalAuditVouchNote { get; set; }

    public DateTime? SubmittedForApprovalAtUtc { get; set; }

    [MaxLength(30)]
    public string? ApprovalAuthority { get; set; }

    [MaxLength(200)]
    public string? ApprovalReference { get; set; }

    [MaxLength(2000)]
    public string? PostAwardJustification { get; set; }

    public Guid? PostAwardCentralDocumentVersionId { get; set; }
    public Guid? PostAwardFileUploadRecordId { get; set; }

    [MaxLength(500)]
    public string? PostAwardEvidenceReference { get; set; }

    public Guid? FiledById { get; set; }
    public DateTime? FiledAtUtc { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string LifecycleSnapshotJson { get; set; } = string.Empty;

    [MaxLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation Properties
    public virtual Department? Department { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual PurchaseRequisition? PurchaseRequisition { get; set; }
    public virtual ProcurementPolicyExceptionRule? ExceptionRule { get; set; }
    public virtual WorkflowDefinition? WorkflowDefinition { get; set; }
    public virtual WorkflowInstance? WorkflowInstance { get; set; }
    public virtual CentralDocumentVersion? CentralDocumentVersion { get; set; }
    public virtual CentralDocumentVersion? PostAwardCentralDocumentVersion { get; set; }
    public virtual ICollection<EmergencyProcurementItem> CriticalItems { get; set; } = new List<EmergencyProcurementItem>();
    public virtual ICollection<EmergencySupplier> EmergencySuppliers { get; set; } = new List<EmergencySupplier>();
}

/// <summary>
/// Critical items for emergency procurement
/// </summary>
public class EmergencyProcurementItem : TenantEntity
{
    [Required]
    public Guid EmergencyProcurementPlanId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemDescription { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Specifications { get; set; }

    [MaxLength(100)]
    public string? ItemCategory { get; set; }

    /// <summary>
    /// Minimum stock level to maintain
    /// </summary>
    public decimal MinimumStockLevel { get; set; }

    /// <summary>
    /// Current stock level
    /// </summary>
    public decimal CurrentStockLevel { get; set; }

    /// <summary>
    /// Emergency order quantity
    /// </summary>
    public decimal EmergencyOrderQuantity { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Maximum acceptable lead time in days
    /// </summary>
    public int MaxLeadTimeDays { get; set; } = 3;

    /// <summary>
    /// Criticality level: Critical, High, Medium
    /// </summary>
    [MaxLength(20)]
    public string CriticalityLevel { get; set; } = "Critical";

    [MaxLength(1000)]
    public string? AlternativeItems { get; set; } // JSON

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual EmergencyProcurementPlan EmergencyProcurementPlan { get; set; } = null!;
}

/// <summary>
/// Emergency supplier list
/// </summary>
public class EmergencySupplier : TenantEntity
{
    [Required]
    public Guid EmergencyProcurementPlanId { get; set; }

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

    /// <summary>
    /// Items this supplier can provide (JSON)
    /// </summary>
    public string? ItemsProvided { get; set; }

    /// <summary>
    /// Maximum response time in hours
    /// </summary>
    public int ResponseTimeHours { get; set; } = 24;

    /// <summary>
    /// Priority order (1 = first choice)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Has contract in place for emergency supplies
    /// </summary>
    public bool HasEmergencyContract { get; set; } = false;

    public DateTime? ContractExpiryDate { get; set; }

    /// <summary>
    /// Payment terms for emergency orders
    /// </summary>
    [MaxLength(100)]
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Last verification date
    /// </summary>
    public DateTime? LastVerifiedDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual EmergencyProcurementPlan EmergencyProcurementPlan { get; set; } = null!;
    public virtual Supplier? Supplier { get; set; }
}

#endregion
