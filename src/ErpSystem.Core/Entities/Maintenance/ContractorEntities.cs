using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    // Navigation properties
    [ForeignKey("ContractorId")]
    public virtual MaintenanceContractor Contractor { get; set; } = null!;

    public virtual ICollection<ContractorExpense> Expenses { get; set; } = new List<ContractorExpense>();
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