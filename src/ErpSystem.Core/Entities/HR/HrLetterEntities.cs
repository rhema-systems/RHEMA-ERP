using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Letters;

/// <summary>
/// An employee's request for an HR letter, and the letter HR issued for it
/// (area 25 slice 12b, decision D7).
/// </summary>
/// <remarks>
/// <para><b>Two ways to fulfil it, deliberately.</b> Most letters can be GENERATED: every fact
/// in an employment confirmation already lives in the system, and HR editing the wording once
/// in the template beats HR retyping it per employee forever. But some letters need a wet
/// signature, a stamp, or an embassy's own form — so HR may instead UPLOAD a signed scan
/// through the controlled gate. A request carries whichever arrived; the employee downloads
/// the same way either way.</para>
///
/// <para><b>The generated letter is FROZEN at issue.</b> <see cref="IssuedDocumentHtml"/>
/// stores the rendered document rather than re-rendering it on each read — the opposite of the
/// asset terms letter, which is deliberately generated on demand because it describes a live
/// assignment. A letter of employment is a statement made on a date to a third party who may
/// still be holding it a year later: it must say tomorrow exactly what it said when it was
/// handed over, even after a promotion changes the position it names. Same reasoning as the
/// payroll payslip snapshot (slice 10).</para>
///
/// <para><b>Why the purpose is required.</b> HR is being asked to make a statement to somebody
/// outside the organisation. What it is for decides whether to issue at all, which template
/// fits, and how to address it — so it is not an optional note.</para>
/// </remarks>
public class HrLetterRequest : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RequestNumber { get; set; } = string.Empty;

    /// <summary>The employee the letter is about. Always the token's employee — never a payload.</summary>
    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public HrLetterType LetterType { get; set; }

    /// <summary>What the employee needs it for — HR's basis for issuing, and for the wording.</summary>
    [Required]
    [MaxLength(1000)]
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Who it should be addressed to. Null means the template's own default, which is
    /// "To whom it may concern" — a letter to nobody in particular is a real, common case.
    /// </summary>
    [MaxLength(300)]
    public string? AddressedTo { get; set; }

    public HrLetterRequestStatus Status { get; set; } = HrLetterRequestStatus.Pending;

    public DateTime RequestedAt { get; set; }

    // ── HR's answer ───────────────────────────────────────────────────────────
    public Guid? IssuedById { get; set; }

    [ForeignKey(nameof(IssuedById))]
    public virtual Employee? IssuedBy { get; set; }

    public DateTime? IssuedAt { get; set; }

    /// <summary>The reference printed on the letter itself, distinct from the request number.</summary>
    [MaxLength(50)]
    public string? LetterNumber { get; set; }

    /// <summary>
    /// The generated letter, frozen at issue. Null when HR uploaded a signed scan instead.
    /// </summary>
    public string? IssuedDocumentHtml { get; set; }

    /// <summary>Required on a refusal — the employee reads it back.</summary>
    [MaxLength(1000)]
    public string? DecisionComments { get; set; }

    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // ── The uploaded alternative (the shared controlled-upload gate) ──────────
    public Guid? FileUploadRecordId { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }

    /// <summary>Storage path — NOT a URL, and never rendered as one.</summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(100)]
    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    /// <summary>True once either fulfilment route has produced something to hand over.</summary>
    [NotMapped]
    public bool HasDocument => !string.IsNullOrWhiteSpace(IssuedDocumentHtml) || FileUploadRecordId.HasValue;
}
