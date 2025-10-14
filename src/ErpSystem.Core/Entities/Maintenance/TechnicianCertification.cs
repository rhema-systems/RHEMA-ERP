using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Technician certification entity
/// </summary>
public class TechnicianCertification : TenantEntity
{
    /// <summary>
    /// Technician who owns this certification
    /// </summary>
    [Required]
    public Guid TechnicianId { get; set; }

    /// <summary>
    /// Name of the certification
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string CertificationName { get; set; } = string.Empty;

    /// <summary>
    /// Unique certification number or ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CertificationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Organization that issued the certification
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string IssuingOrganization { get; set; } = string.Empty;

    /// <summary>
    /// Date when certification was issued
    /// </summary>
    [Required]
    public DateTime IssueDate { get; set; }

    /// <summary>
    /// Date when certification expires (if applicable)
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Current status of the certification (Active, Expired, Suspended, Revoked)
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Level or grade of certification
    /// </summary>
    [MaxLength(50)]
    public string CertificationLevel { get; set; } = string.Empty;

    /// <summary>
    /// Category of certification (Safety, Technical, Professional, etc.)
    /// </summary>
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this certification covers
    /// </summary>
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Any specific requirements or conditions
    /// </summary>
    [MaxLength(500)]
    public string Requirements { get; set; } = string.Empty;

    /// <summary>
    /// Renewal requirements (if applicable)
    /// </summary>
    [MaxLength(500)]
    public string RenewalRequirements { get; set; } = string.Empty;

    /// <summary>
    /// Cost of obtaining/maintaining this certification
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Cost { get; set; }

    /// <summary>
    /// Whether this certification is mandatory for the technician's role
    /// </summary>
    public bool IsMandatory { get; set; } = false;

    /// <summary>
    /// Whether this certification is verified by a supervisor
    /// </summary>
    public bool IsVerified { get; set; } = false;

    /// <summary>
    /// User who verified this certification
    /// </summary>
    [MaxLength(100)]
    public string VerifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date when certification was verified
    /// </summary>
    public DateTime? VerificationDate { get; set; }

    /// <summary>
    /// Additional notes about the certification
    /// </summary>
    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// File path to certification document/certificate
    /// </summary>
    [MaxLength(500)]
    public string DocumentPath { get; set; } = string.Empty;

    // Computed properties for reporting
    /// <summary>
    /// Whether the certification is currently expired
    /// </summary>
    [NotMapped]
    public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value < DateTime.UtcNow.Date;

    /// <summary>
    /// Whether the certification is expiring soon (within 30 days)
    /// </summary>
    [NotMapped]
    public bool IsExpiringSoon => ExpirationDate.HasValue && 
                                   ExpirationDate.Value >= DateTime.UtcNow.Date && 
                                   ExpirationDate.Value <= DateTime.UtcNow.AddDays(30).Date;

    /// <summary>
    /// Days until expiration (negative if expired)
    /// </summary>
    [NotMapped]
    public int? DaysUntilExpiration => ExpirationDate?.Subtract(DateTime.UtcNow.Date).Days;

    // Navigation properties
    /// <summary>
    /// Associated technician (Employee)
    /// </summary>
    public virtual Employee? Technician { get; set; }
}