using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Tracks supplier/contractor performance metrics over time
/// </summary>
public class SupplierPerformanceMetric : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string MetricPeriod { get; set; } = string.Empty; // Monthly, Quarterly, Yearly

    public int Year { get; set; }
    public int? Month { get; set; } // 1-12 for monthly, null for yearly
    public int? Quarter { get; set; } // 1-4 for quarterly, null for others

    // Delivery Performance Metrics
    public int TotalOrders { get; set; } = 0;
    public int OnTimeDeliveries { get; set; } = 0;
    public int LateDeliveries { get; set; } = 0;
    public int EarlyDeliveries { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal OnTimeDeliveryRate { get; set; } = 0; // Percentage 0-100

    [Column(TypeName = "decimal(10,2)")]
    public decimal AverageDeliveryDelayDays { get; set; } = 0;

    // Quality Metrics
    public int TotalItemsReceived { get; set; } = 0;
    public int DefectiveItems { get; set; } = 0;
    public int RejectedItems { get; set; } = 0;
    public int ReturnedItems { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal QualityAcceptanceRate { get; set; } = 100; // Percentage 0-100

    [Column(TypeName = "decimal(5,2)")]
    public decimal DefectRate { get; set; } = 0; // Percentage 0-100

    // Cost Competitiveness
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPurchaseValue { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AveragePriceVariance { get; set; } = 0; // Variance from market/budget price

    [Column(TypeName = "decimal(5,2)")]
    public decimal CostCompetitivenessScore { get; set; } = 0; // 0-100

    // Service & Responsiveness
    [Column(TypeName = "decimal(10,2)")]
    public decimal AverageResponseTimeHours { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal CustomerServiceRating { get; set; } = 0; // 0-5

    public int ComplaintsReceived { get; set; } = 0;
    public int ComplaintsResolved { get; set; } = 0;

    // Contract Compliance
    public int ContractViolations { get; set; } = 0;
    public int TermsBreaches { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal ComplianceScore { get; set; } = 100; // Percentage 0-100

    // Innovation & Value Add
    public int InnovationSuggestions { get; set; } = 0;
    public int CostSavingInitiatives { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCostSavings { get; set; } = 0;

    // Overall Performance Score (weighted average)
    [Column(TypeName = "decimal(5,2)")]
    public decimal OverallPerformanceScore { get; set; } = 0; // 0-100

    [MaxLength(20)]
    public string? PerformanceGrade { get; set; } // A+, A, B+, B, C, D, F

    // Metadata
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CalculatedById { get; set; }

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? CalculatedBy { get; set; }
}

/// <summary>
/// Tracks quality incidents and issues with suppliers/contractors
/// </summary>
public class QualityIncident : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    [Required]
    [MaxLength(50)]
    public string IncidentNumber { get; set; } = string.Empty;

    [Required]
    public DateTime IncidentDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string IncidentType { get; set; } = string.Empty; // Defect, Damage, Wrong Item, Short Delivery, etc.

    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical

    [Required]
    public string Description { get; set; } = string.Empty;

    public int QuantityAffected { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FinancialImpact { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed

    public DateTime? ReportedDate { get; set; }
    public Guid? ReportedById { get; set; }

    public DateTime? AcknowledgedDate { get; set; }
    public Guid? AcknowledgedById { get; set; }

    public DateTime? ResolvedDate { get; set; }
    public Guid? ResolvedById { get; set; }

    public string? Resolution { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }

    public bool RequiresSupplierResponse { get; set; } = true;
    public DateTime? SupplierResponseDate { get; set; }
    public string? SupplierResponse { get; set; }

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? ReportedBy { get; set; }
    public virtual ApplicationUser? AcknowledgedBy { get; set; }
    public virtual ApplicationUser? ResolvedBy { get; set; }
}

/// <summary>
/// Periodic performance reviews and evaluations
/// </summary>
public class PerformanceReview : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReviewNumber { get; set; } = string.Empty;

    [Required]
    public DateTime ReviewDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string ReviewPeriod { get; set; } = string.Empty; // Q1 2024, 2024, etc.

    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }

    [Required]
    public Guid ReviewedById { get; set; }

    // Review Scores (0-5 scale)
    [Column(TypeName = "decimal(3,2)")]
    public decimal DeliveryPerformanceScore { get; set; } = 0;

    [Column(TypeName = "decimal(3,2)")]
    public decimal QualityScore { get; set; } = 0;

    [Column(TypeName = "decimal(3,2)")]
    public decimal CostCompetitivenessScore { get; set; } = 0;

    [Column(TypeName = "decimal(3,2)")]
    public decimal CustomerServiceScore { get; set; } = 0;

    [Column(TypeName = "decimal(3,2)")]
    public decimal ComplianceScore { get; set; } = 0;

    [Column(TypeName = "decimal(3,2)")]
    public decimal InnovationScore { get; set; } = 0;

    [Column(TypeName = "decimal(3,2)")]
    public decimal OverallScore { get; set; } = 0;

    [MaxLength(20)]
    public string? OverallGrade { get; set; } // A+, A, B+, B, C, D, F

    // Review Content
    [Required]
    public string Strengths { get; set; } = string.Empty;

    [Required]
    public string AreasForImprovement { get; set; } = string.Empty;

    public string? Recommendations { get; set; }
    public string? ActionItems { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Draft"; // Draft, Submitted, Acknowledged, Disputed, Finalized

    public DateTime? SubmittedDate { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public Guid? AcknowledgedById { get; set; }

    public string? SupplierComments { get; set; }
    public DateTime? SupplierCommentsDate { get; set; }

    public bool RequiresFollowUp { get; set; } = false;
    public DateTime? FollowUpDate { get; set; }

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser ReviewedBy { get; set; } = null!;
    public virtual ApplicationUser? AcknowledgedBy { get; set; }
}

