using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Documents;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Append-only issuance evidence for a controlled Finance document.
///
/// The source document remains the accounting authority; this row records only which rendered
/// copy left the system, who requested it, and the hash of the exact emitted bytes. Keeping this
/// evidence separate from CashTransaction and CustomerPayment lets the shared document-output
/// pipeline support both records without adding duplicate accounting or posting state.
/// </summary>
public class FinanceControlledDocumentIssue : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string SourceDocumentType { get; set; } = string.Empty;

    public Guid SourceDocumentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// One-based sequence within a document type and source record. Copy 1 is always the single
    /// original; all later values are explicitly watermarked replacement copies.
    /// </summary>
    public int CopyNumber { get; set; }

    [Required]
    [MaxLength(20)]
    public string CopyType { get; set; } = ControlledDocumentCopyTypes.Original;

    /// <summary>
    /// Required for every replacement. The original intentionally has no replacement reason.
    /// </summary>
    [MaxLength(1000)]
    public string? ReplacementReason { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public Guid IssuedById { get; set; }

    [Required]
    [MaxLength(255)]
    public string IssuedByName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string ContentSha256 { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = "application/pdf";

    public Guid? JournalEntryId { get; set; }

    public virtual ApplicationUser IssuedBy { get; set; } = null!;
    public virtual JournalEntry? JournalEntry { get; set; }
}
