using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// A kind of document an employee's file may hold — contract, ID scan, certificate, permit.
/// Tenant reference data, administered by HR.
/// </summary>
/// <remarks>
/// <para><b>A lookup rather than an enum, because two features have to speak the same language.</b>
/// An employee HOLDS documents and a position REQUIRES them; if the requirement named a value from
/// a compiled enum, TDC could never add "Professional indemnity certificate" without a release —
/// and the whole point of the requirement checklist is that HR maintains it. Every other HR
/// document vocabulary in the codebase (<c>MedicalDocumentType</c>, <c>TravelDocumentType</c>,
/// <c>JobCandidateDocumentType</c>) is an enum precisely because nothing else reads it.</para>
///
/// <para><b>Expiry is a property of the TYPE, not of the row.</b> A passport expires and a signed
/// contract does not, so whether the upload form asks for an expiry date is decided here once
/// rather than guessed per document.</para>
/// </remarks>
public class EmployeeDocumentType : TenantEntity
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Whether documents of this kind carry an expiry date the form must ask for.</summary>
    public bool HasExpiry { get; set; }

    /// <summary>
    /// Days before expiry at which this document should start being chased.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Nothing reads this yet.</b> It is stored so the vocabulary is complete when a sweep is
    /// built, and it is deliberately NOT surfaced as a promise — no screen presents it as an active
    /// reminder setting, because a lead time that chases nobody is the D-29 shape: a control that
    /// does not control. The finish plan's lane 3b carries the matching gap on
    /// <c>IdentificationType</c>; both should be met by one expiry engine rather than two.
    /// </remarks>
    public int? ExpiryReminderLeadDays { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
}

/// <summary>
/// A file on an employee's record — the contract, the ID scan, the certificate (FRD, Employee
/// Master feedback: "no employee document attachments").
/// </summary>
/// <remarks>
/// <para><b>Why this did not exist.</b> The controlled upload gate is wired into more than thirty HR
/// controllers — discipline, medical, travel, succession, awards, separation — and the one entity
/// every other one hangs off could not hold a file at all. Measured 2026-08-31: no
/// <c>EmployeeDocument</c> type anywhere, and no upload route on <c>EmployeesController</c>. So the
/// most-used record in the module could not carry a signed contract.</para>
///
/// <para><b>Three ids, never a path.</b> <c>FilePath</c> as a JSON field is the injection sink area
/// 16 replaced and D-10, D-14 and D-39 each fixed again; a new table gets the gate from the start.
/// The upload endpoint is the only route by which a file reaches this row.</para>
///
/// <para><b>Several of the same type is normal</b> — a renewed passport, a re-signed contract — so
/// there is no unique index on (employee, type). The current one is the latest by
/// <see cref="IssuedOn"/>, and the compliance read treats an unexpired document as satisfying a
/// requirement.</para>
/// </remarks>
public class EmployeeDocument : TenantEntity
{
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid DocumentTypeId { get; set; }

    [ForeignKey(nameof(DocumentTypeId))]
    public virtual EmployeeDocumentType DocumentType { get; set; } = null!;

    [MaxLength(250)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>When the document itself was issued — not when it was uploaded.</summary>
    public DateOnly? IssuedOn { get; set; }

    /// <summary>
    /// When it stops being valid. Null where the type does not expire.
    /// </summary>
    /// <remarks>
    /// ⚠ The compliance read treats a document expiring TODAY as still valid and one that expired
    /// yesterday as not held. Stating it here because "expired" is a boundary somebody will
    /// otherwise re-decide differently in a report.
    /// </remarks>
    public DateOnly? ExpiresOn { get; set; }

    // ── The file, through the gate ────────────────────────────────────────────

    /// <summary>The controlled (virus-scanned) upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(150)]
    public string? MimeType { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// Who put it there, stamped from the token.
    /// </summary>
    /// <remarks>
    /// ⚠ An explicit service parameter, never a DTO field — five earlier instances of the D-05
    /// shape were exactly this: an actor a caller could assert. `CreatedBy` carries the user, this
    /// carries the employee the audit trail can resolve to a person.
    /// </remarks>
    public Guid? UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee? UploadedBy { get; set; }
}

/// <summary>
/// A file that belongs to a GUARANTOR — the signed guarantor form, an ID scan, a payslip, a letter
/// of undertaking (demo feedback round 2, E-12: "upload picture of the guarantor and any documents
/// pertaining to the guarantor").
/// </summary>
/// <remarks>
/// <para><b>Why a collection, when the photograph is six columns on the row.</b> A guarantor has
/// exactly one face and any number of papers. The photo is one-per-row; the papers are many, each
/// with a kind and possibly an expiry (an ID scan expires, a signed undertaking does not), which is
/// the same shape as <see cref="EmployeeDocument"/> — so this table mirrors it and speaks the same
/// <see cref="EmployeeDocumentType"/> vocabulary rather than minting a second one.</para>
///
/// <para><b>Why not rows on <see cref="EmployeeDocument"/> with a guarantor id.</b> The employee's
/// file is the employee's; a guarantor's payslip on it would answer "what does this employee hold"
/// with another person's papers, and the compliance read would count it. Different subject,
/// different table.</para>
///
/// <para><b>Three ids, never a path.</b> <c>EmployeeGuarantor.GuarantorFormPath</c> is the legacy
/// caller-supplied sink this table retires; it stays readable and is no longer writable.</para>
/// </remarks>
public class EmployeeGuarantorDocument : TenantEntity
{
    public Guid GuarantorId { get; set; }

    [ForeignKey(nameof(GuarantorId))]
    public virtual EmployeeGuarantor Guarantor { get; set; } = null!;

    public Guid DocumentTypeId { get; set; }

    [ForeignKey(nameof(DocumentTypeId))]
    public virtual EmployeeDocumentType DocumentType { get; set; } = null!;

    [MaxLength(250)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    // ── The file, through the gate ────────────────────────────────────────────

    public Guid? FileUploadRecordId { get; set; }

    public Guid? DocumentRecordId { get; set; }

    public Guid? DocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(150)]
    public string? MimeType { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>Who put it there, stamped from the token — a parameter, never a DTO field.</summary>
    public Guid? UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee? UploadedBy { get; set; }
}

/// <summary>
/// A document a position requires its holder to have on file (FRD, Employee Master feedback: "no
/// mandatory documents against a position").
/// </summary>
/// <remarks>
/// <para><b>This is what makes the document surface enforceable rather than decorative.</b> Files
/// nobody checks for are a filing cabinet; the requirement is what turns them into a compliance
/// question — "which of my drivers has no licence on file?"</para>
///
/// <para><b>It reports, it does not block.</b> Nothing here refuses an appointment or a payroll run
/// for a missing document. Area 8's FR-HR-173 vacancy rule was built as a hard block exactly as
/// specified and refused nearly every movement, and area 9's deadline advisories were built to
/// report for the same reason. A missing document is a fact to chase, and blocking on it would
/// stop the very transaction that gets somebody employed and able to supply it.</para>
/// </remarks>
public class PositionDocumentRequirement : TenantEntity
{
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    public Guid DocumentTypeId { get; set; }

    [ForeignKey(nameof(DocumentTypeId))]
    public virtual EmployeeDocumentType DocumentType { get; set; } = null!;

    /// <summary>
    /// Mandatory rather than merely expected. Both are reported; only mandatory ones count as a
    /// compliance failure.
    /// </summary>
    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
