using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Ehc;

[Table("EhcLegalHolds")]
public class EhcLegalHold : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    [StringLength(1000)]
    public string? Reason { get; set; }

    [StringLength(100)]
    public string? ReferenceNumber { get; set; }

    public DateTime? ReleasedAtUtc { get; set; }
    public Guid? ReleasedByUserId { get; set; }
}

[Table("EhcRetentionCategoryExceptions")]
public class EhcRetentionCategoryException : TenantEntity
{
    [Required]
    public Guid CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcTicketCategory Category { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Override retention for EHC ticket audit events in this category.
    /// </summary>
    public int AuditEventRetentionDays { get; set; } = 365;

    [StringLength(1000)]
    public string? Notes { get; set; }
}

[Table("EhcComplianceAuditExports")]
public class EhcComplianceAuditExport : TenantEntity
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    public Guid? TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket? Ticket { get; set; }

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcTicketCategory? Category { get; set; }

    [Required]
    public Guid FileUploadRecordId { get; set; }

    [ForeignKey(nameof(FileUploadRecordId))]
    public virtual FileUploadRecord FileUploadRecord { get; set; } = null!;

    [Required]
    [StringLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    public int RowCount { get; set; } = 0;
}

