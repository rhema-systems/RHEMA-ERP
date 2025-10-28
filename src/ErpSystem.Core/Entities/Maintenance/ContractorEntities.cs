using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Represents a maintenance contractor or external service provider
/// </summary>
public class MaintenanceContractor
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ContractorCode { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Contact information (JSON)
    /// </summary>
    public string ContactInfo { get; set; } = "{}";

    /// <summary>
    /// Contractor capabilities and specializations (JSON)
    /// </summary>
    public string Capabilities { get; set; } = "[]";

    /// <summary>
    /// Service areas or locations covered (JSON)
    /// </summary>
    public string ServiceAreas { get; set; } = "[]";

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active, Inactive, Suspended

    /// <summary>
    /// Contractor rating (1-5 stars)
    /// </summary>
    [Range(1, 5)]
    public double? Rating { get; set; }

    /// <summary>
    /// License numbers and certifications (JSON)
    /// </summary>
    public string? LicenseInfo { get; set; }

    /// <summary>
    /// Insurance information (JSON)
    /// </summary>
    public string? InsuranceInfo { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? LastModifiedById { get; set; }
    public Guid TenantId { get; set; }

    // Navigation properties
    public virtual ICollection<ContractorWorkOrder> WorkOrders { get; set; } = new List<ContractorWorkOrder>();
    public virtual ICollection<ContractorInvoice> Invoices { get; set; } = new List<ContractorInvoice>();
    public virtual ICollection<ContractorPerformanceReview> PerformanceReviews { get; set; } = new List<ContractorPerformanceReview>();
}

/// <summary>
/// Links work orders to external contractors
/// </summary>
public class ContractorWorkOrder
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid ContractorId { get; set; }

    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Assigned"; // Assigned, InProgress, Completed, Cancelled

    /// <summary>
    /// Estimated cost provided by contractor
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Actual cost after completion
    /// </summary>
    public decimal? ActualCost { get; set; }

    /// <summary>
    /// Work performed description
    /// </summary>
    [MaxLength(2000)]
    public string? WorkPerformed { get; set; }

    /// <summary>
    /// Parts used by contractor (JSON)
    /// </summary>
    public string? PartsUsed { get; set; }

    /// <summary>
    /// Quality rating for this work (1-5)
    /// </summary>
    [Range(1, 5)]
    public int? QualityRating { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("ContractorId")]
    public virtual MaintenanceContractor Contractor { get; set; } = null!;
    
    // Note: WorkOrder navigation handled by existing WorkOrder entity
}

/// <summary>
/// Tracks contractor invoices without full finance integration
/// </summary>
public class ContractorInvoice
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid ContractorId { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    public DateTime InvoiceDate { get; set; }

    public DateTime? DueDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxAmount { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Paid

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Invoice line items (JSON)
    /// </summary>
    public string LineItems { get; set; } = "[]";

    /// <summary>
    /// Supporting documents and receipts (JSON)
    /// </summary>
    public string? AttachmentPaths { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public Guid TenantId { get; set; }
}

/// <summary>
/// Enhanced contractor performance tracking and analytics
/// </summary>
public class ContractorPerformanceMetrics : TenantEntity
{
    [Required]
    public Guid ContractorId { get; set; }

    [Required]
    public DateTime PeriodStart { get; set; }

    [Required]
    public DateTime PeriodEnd { get; set; }

    [MaxLength(20)]
    public string PeriodType { get; set; } = "Monthly"; // Weekly, Monthly, Quarterly, Yearly

    // Performance metrics
    public int TotalWorkOrders { get; set; } = 0;
    public int CompletedWorkOrders { get; set; } = 0;
    public int CancelledWorkOrders { get; set; } = 0;
    public int OverdueWorkOrders { get; set; } = 0;

    // Quality metrics
    public double AverageQualityRating { get; set; } = 0;
    public int QualityRatingsCount { get; set; } = 0;
    public int ReworkRequired { get; set; } = 0;
    public int CustomerComplaints { get; set; } = 0;

    // Time metrics
    public double AverageCompletionDays { get; set; } = 0;
    public double OnTimeCompletionRate { get; set; } = 0;
    public double ResponseTimeHours { get; set; } = 0;

    // Financial metrics
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalInvoiceAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageWorkOrderValue { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostVariance { get; set; } = 0; // Difference between estimated and actual costs

    // Safety and compliance
    public int SafetyIncidents { get; set; } = 0;
    public int ComplianceViolations { get; set; } = 0;
    public bool CertificationsUpToDate { get; set; } = true;
    public bool InsuranceUpToDate { get; set; } = true;

    // Overall performance score (calculated)
    public double PerformanceScore { get; set; } = 0; // 0-100 score
    
    [MaxLength(20)]
    public string PerformanceRating { get; set; } = "Satisfactory"; // Excellent, Good, Satisfactory, Poor, Unsatisfactory

    // Additional metrics
    public double UtilizationRate { get; set; } = 0; // Percentage of time contractor is active
    public int PreferredContractorSelections { get; set; } = 0; // Times selected as preferred contractor

    // Navigation properties
    [ForeignKey("ContractorId")]
    public virtual MaintenanceContractor Contractor { get; set; } = null!;
}

/// <summary>
/// Contractor invoice workflow and approval tracking
/// </summary>
public class ContractorInvoiceApproval : TenantEntity
{
    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    public int ApprovalLevel { get; set; }

    [Required]
    [MaxLength(100)]
    public string ApprovalRole { get; set; } = string.Empty; // Supervisor, Manager, Finance, etc.

    [Required]
    public Guid ApproverId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Delegated

    public DateTime? ActionDate { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Delegation tracking
    public Guid? DelegatedToId { get; set; }
    public DateTime? DelegatedDate { get; set; }
    
    [MaxLength(500)]
    public string? DelegationReason { get; set; }

    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Navigation properties
    [ForeignKey("InvoiceId")]
    public virtual ContractorInvoice Invoice { get; set; } = null!;
    
    public virtual Employee Approver { get; set; } = null!;
    public virtual Employee? DelegatedTo { get; set; }
}

/// <summary>
/// Contractor logistics and travel management
/// </summary>
public class ContractorLogistics : TenantEntity
{
    [Required]
    public Guid ContractorWorkOrderId { get; set; }

    // Travel and transportation
    [MaxLength(200)]
    public string? DepartureLocation { get; set; }

    [MaxLength(200)]
    public string? ArrivalLocation { get; set; }

    public DateTime? ScheduledDepartureTime { get; set; }
    public DateTime? ScheduledArrivalTime { get; set; }
    public DateTime? ActualDepartureTime { get; set; }
    public DateTime? ActualArrivalTime { get; set; }

    [MaxLength(50)]
    public string? TransportationMethod { get; set; } // Vehicle, Public Transport, Walking, etc.

    [MaxLength(50)]
    public string? VehicleType { get; set; }

    [MaxLength(20)]
    public string? LicensePlate { get; set; }

    // Distance and routing
    public decimal? EstimatedDistance { get; set; }
    public decimal? ActualDistance { get; set; }
    public int? EstimatedTravelMinutes { get; set; }
    public int? ActualTravelMinutes { get; set; }

    // GPS tracking
    public decimal? DepartureLatitude { get; set; }
    public decimal? DepartureLongitude { get; set; }
    public decimal? ArrivalLatitude { get; set; }
    public decimal? ArrivalLongitude { get; set; }

    // Route optimization
    [Column(TypeName = "nvarchar(max)")]
    public string? OptimalRoute { get; set; } // JSON: GPS coordinates or waypoints

    [Column(TypeName = "nvarchar(max)")]
    public string? ActualRoute { get; set; } // JSON: Actual path taken

    // Costs and expenses
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedTravelCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualTravelCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FuelCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TollCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ParkingCost { get; set; } = 0;

    // Accommodation (for multi-day jobs)
    public bool RequiresAccommodation { get; set; } = false;
    
    [MaxLength(200)]
    public string? AccommodationLocation { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AccommodationCost { get; set; } = 0;

    // Equipment and tools transportation
    [Column(TypeName = "nvarchar(max)")]
    public string? EquipmentList { get; set; } // JSON: List of tools/equipment transported

    public bool SpecialEquipmentRequired { get; set; } = false;
    
    [MaxLength(1000)]
    public string? SpecialRequirements { get; set; }

    // Status tracking
    [MaxLength(20)]
    public string Status { get; set; } = "Planned"; // Planned, InTransit, Arrived, Completed, Cancelled

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("ContractorWorkOrderId")]
    public virtual ContractorWorkOrder ContractorWorkOrder { get; set; } = null!;

    public virtual ICollection<ContractorLogisticsExpense> LogisticsExpenses { get; set; } = new List<ContractorLogisticsExpense>();
}

/// <summary>
/// Detailed expense tracking for contractor logistics
/// </summary>
public class ContractorLogisticsExpense : TenantEntity
{
    [Required]
    public Guid LogisticsId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ExpenseType { get; set; } = string.Empty; // Fuel, Toll, Parking, Accommodation, Meals, Equipment

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ReceiptPath { get; set; }

    [MaxLength(100)]
    public string? VendorName { get; set; }

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    // Approval status
    [MaxLength(20)]
    public string ApprovalStatus { get; set; } = "Pending"; // Pending, Approved, Rejected

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    
    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    // Reimbursement tracking
    public bool IsReimbursable { get; set; } = true;
    public bool IsReimbursed { get; set; } = false;
    public DateTime? ReimbursedDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("LogisticsId")]
    public virtual ContractorLogistics Logistics { get; set; } = null!;
    
    public virtual Employee? ApprovedBy { get; set; }
}

/// <summary>
/// Tracks contractor expenses (fuel, travel, materials)
/// </summary>
public class ContractorExpense
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid ContractorInvoiceId { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ExpenseType { get; set; } = string.Empty; // Fuel, Travel, Materials, Labor, Other

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; }

    /// <summary>
    /// Quantity or units (e.g., miles, hours, gallons)
    /// </summary>
    public decimal? Quantity { get; set; }

    [MaxLength(20)]
    public string? Unit { get; set; }

    /// <summary>
    /// Rate per unit
    /// </summary>
    public decimal? UnitRate { get; set; }

    /// <summary>
    /// Receipt or supporting document path
    /// </summary>
    [MaxLength(500)]
    public string? ReceiptPath { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("ContractorInvoiceId")]
    public virtual ContractorInvoice ContractorInvoice { get; set; } = null!;
}

/// <summary>
/// Performance reviews for contractors
/// </summary>
public class ContractorPerformanceReview
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid ContractorId { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Required]
    public DateTime ReviewDate { get; set; }

    [Required]
    public Guid ReviewedById { get; set; }

    /// <summary>
    /// Overall rating (1-5 stars)
    /// </summary>
    [Required]
    [Range(1, 5)]
    public int OverallRating { get; set; }

    /// <summary>
    /// Quality of work rating (1-5)
    /// </summary>
    [Range(1, 5)]
    public int? QualityRating { get; set; }

    /// <summary>
    /// Timeliness rating (1-5)
    /// </summary>
    [Range(1, 5)]
    public int? TimelinessRating { get; set; }

    /// <summary>
    /// Communication rating (1-5)
    /// </summary>
    [Range(1, 5)]
    public int? CommunicationRating { get; set; }

    /// <summary>
    /// Cost effectiveness rating (1-5)
    /// </summary>
    [Range(1, 5)]
    public int? CostRating { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }

    [MaxLength(1000)]
    public string? Recommendations { get; set; }

    /// <summary>
    /// Would recommend for future work
    /// </summary>
    public bool WouldRecommend { get; set; } = true;

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("ContractorId")]
    public virtual MaintenanceContractor Contractor { get; set; } = null!;
}