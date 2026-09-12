using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.ProfileChanges;

/// <summary>
/// An employee's request to correct their own personal data, and HR's answer to it
/// (area 25 slice 12, decision D6).
/// </summary>
/// <remarks>
/// <para><b>Why this exists at all.</b> An employee is the only person who knows their phone
/// number changed, and HR is the only party who should be able to change a bank account or a
/// date of birth. Splitting the difference is the whole feature: low-risk contact fields are
/// edited directly on the portal, and everything identity- or payment-bearing arrives here
/// as a request that a human approves. A wrong bank account is a payroll fraud vector and a
/// wrong date of birth moves a retirement date, so neither may be a silent self-service
/// write.</para>
///
/// <para><b>Approval APPLIES.</b> There is no separate apply step (unlike procurement's
/// master-data change requests, which revalidate against a drifting supplier record before
/// landing). An HR officer approving a name correction expects the name to be corrected;
/// a request that sat "approved but not applied" would be a promise with no visible effect.
/// The applier re-runs the same tenant-uniqueness checks
/// <c>EmployeeService.UpdateEmployeeAsync</c> runs, so this path cannot introduce a duplicate
/// email, SSNIT, TIN or tax number that the desk path would have refused.</para>
///
/// <para><b>The old value is snapshotted at filing time</b>, on each item. That is what makes
/// this an audit trail rather than a to-do list: months later the record still says what the
/// value WAS, what was asked for, and what actually landed — which is the question an auditor
/// asks about a bank-account change, and the one HR cannot answer today.</para>
///
/// <para><b>Evidence.</b> A name change carries a marriage certificate; a bank change carries
/// a bank letter. The file rides the shared controlled-upload gate like every other HR
/// attachment (scan, checksum, DMS registration) — see
/// <c>IControlledFileUploadService</c> and <c>HrAttachmentUpload</c>.</para>
/// </remarks>
public class EmployeeProfileChangeRequest : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RequestNumber { get; set; } = string.Empty;

    /// <summary>The employee whose record this changes. Always the token's employee — never
    /// supplied in a payload (an employee cannot file a change against a colleague).</summary>
    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public ProfileChangeRequestStatus Status { get; set; } = ProfileChangeRequestStatus.Pending;

    public DateTime SubmittedAt { get; set; }

    /// <summary>Why the employee is asking — the context HR needs to judge it.</summary>
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Set when the request changes bank-account fields: which of the employee's accounts it
    /// targets. Null for a request that only touches fields on the employee record itself.
    /// </summary>
    public Guid? BankDetailId { get; set; }

    [ForeignKey(nameof(BankDetailId))]
    public virtual EmployeeBankDetail? BankDetail { get; set; }

    // ── HR's answer ───────────────────────────────────────────────────────────
    public Guid? ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Required on a rejection: the employee reads this back, so "no" must say why.</summary>
    [MaxLength(1000)]
    public string? ReviewComments { get; set; }

    /// <summary>When the approved values were actually written onto the record.</summary>
    public DateTime? AppliedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    // ── Evidence (the shared controlled-upload gate) ──────────────────────────
    public Guid? EvidenceFileUploadRecordId { get; set; }
    public Guid? EvidenceDocumentRecordId { get; set; }
    public Guid? EvidenceDocumentVersionId { get; set; }

    /// <summary>Storage path — NOT a URL, and never rendered as one.</summary>
    [MaxLength(500)]
    public string? EvidenceFilePath { get; set; }

    [MaxLength(255)]
    public string? EvidenceFileName { get; set; }

    [MaxLength(100)]
    public string? EvidenceContentType { get; set; }

    public long? EvidenceFileSize { get; set; }

    public virtual ICollection<EmployeeProfileChangeItem> Items { get; set; }
        = new List<EmployeeProfileChangeItem>();
}

/// <summary>
/// One field within a change request: what it was, what was asked for, and what landed.
/// </summary>
/// <remarks>
/// Append-only in practice — items are written when the request is filed and only
/// <see cref="AppliedValue"/> is stamped later, on approval. Values are held as strings
/// because the set spans text, dates, GUIDs and enums; the applier parses per
/// <see cref="Field"/> and refuses a value it cannot parse rather than writing a default
/// (the area-25 slice-5 lesson: an unparsed value silently became year 0001).
/// </remarks>
public class EmployeeProfileChangeItem : TenantEntity
{
    [Required]
    public Guid RequestId { get; set; }

    [ForeignKey(nameof(RequestId))]
    public virtual EmployeeProfileChangeRequest Request { get; set; } = null!;

    public EmployeeProfileField Field { get; set; }

    /// <summary>The value at the moment of filing. Null when the field was empty.</summary>
    [MaxLength(500)]
    public string? OldValue { get; set; }

    [Required]
    [MaxLength(500)]
    public string NewValue { get; set; } = string.Empty;

    /// <summary>What was actually written on approval. Stamped by the applier, never sent.</summary>
    [MaxLength(500)]
    public string? AppliedValue { get; set; }
}
