using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Base;

/// <summary>
/// Base class for all business entities across ERP modules
/// Provides common properties and audit trails
/// </summary>
public abstract class BusinessEntity : BaseEntity
{
    /// <summary>
    /// Business reference number (auto-generated, human-readable)
    /// </summary>
    [StringLength(50)]
    public string ReferenceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Entity status (Active, Inactive, Draft, Cancelled, etc.)
    /// </summary>
    [StringLength(50)]
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Business effective date (when the entity becomes effective)
    /// </summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Business expiration date (when the entity expires)
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Additional metadata in JSON format
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Tags for categorization and search
    /// </summary>
    [StringLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Priority level (1-10, where 10 is highest)
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Check if entity is currently active based on status and dates
    /// </summary>
    public bool IsActive =>
        Status == "Active" &&
        (EffectiveDate == null || EffectiveDate <= DateTime.UtcNow) &&
        (ExpirationDate == null || ExpirationDate > DateTime.UtcNow);
}

/// <summary>
/// Base class for entities with approval workflows
/// </summary>
public abstract class ApprovableEntity : BusinessEntity
{
    /// <summary>
    /// Current approval status
    /// </summary>
    [StringLength(50)]
    public string ApprovalStatus { get; set; } = "Draft";

    /// <summary>
    /// User who submitted for approval
    /// </summary>
    public Guid? SubmittedById { get; set; }

    /// <summary>
    /// Date submitted for approval
    /// </summary>
    public DateTime? SubmittedDate { get; set; }

    /// <summary>
    /// User who approved
    /// </summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>
    /// Date approved
    /// </summary>
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// Approval comments
    /// </summary>
    [StringLength(1000)]
    public string? ApprovalComments { get; set; }

    /// <summary>
    /// Workflow instance ID if using workflow engine
    /// </summary>
    public Guid? WorkflowInstanceId { get; set; }
}

/// <summary>
/// Base class for financial entities with amounts
/// </summary>
public abstract class FinancialEntity : ApprovableEntity
{
    /// <summary>
    /// Currency code (USD, EUR, etc.)
    /// </summary>
    [StringLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Exchange rate to base currency
    /// </summary>
    [Range(0.0001, 999999.9999)]
    public decimal ExchangeRate { get; set; } = 1.0m;

    /// <summary>
    /// Total amount in original currency
    /// </summary>
    [Range(0, 999999999.99)]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Total amount in base currency
    /// </summary>
    [Range(0, 999999999.99)]
    public decimal BaseCurrencyAmount => TotalAmount * ExchangeRate;

    /// <summary>
    /// Tax amount
    /// </summary>
    [Range(0, 999999999.99)]
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Net amount (before tax)
    /// </summary>
    [Range(0, 999999999.99)]
    public decimal NetAmount => TotalAmount - TaxAmount;
}

/// <summary>
/// Base class for entities with line items
/// </summary>
public abstract class DocumentEntity : FinancialEntity
{
    /// <summary>
    /// Document number (invoice number, PO number, etc.)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Document date
    /// </summary>
    public DateTime DocumentDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Due date (for payments, deliveries, etc.)
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Terms and conditions
    /// </summary>
    [StringLength(2000)]
    public string? Terms { get; set; }

    /// <summary>
    /// Internal notes
    /// </summary>
    [StringLength(2000)]
    public string? InternalNotes { get; set; }

    /// <summary>
    /// External notes (visible to customers/vendors)
    /// </summary>
    [StringLength(2000)]
    public string? ExternalNotes { get; set; }
}
