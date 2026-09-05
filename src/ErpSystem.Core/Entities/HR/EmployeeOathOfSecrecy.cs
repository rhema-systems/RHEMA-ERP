using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Recruitment;

/// <summary>
/// An oath of secrecy sworn by an employee (FRD FR-HR-030, priority M).
/// </summary>
/// <remarks>
/// <para><b>Why a record of its own, rather than an onboarding task or an orientation
/// acknowledgement.</b> <c>OnboardingTask</c> tracks completion by whoever was assigned it — an
/// oath marked "done" by HR is not an oath. <c>OrientationAcknowledgement</c> has exactly the right
/// shape and is modelled here (immutable text, signing timestamp, IP, tamper hash), but it hangs
/// off an <c>EmployeeOrientation</c>: an oath must be findable for an employee for the life of
/// their employment, including for staff who never had an orientation enrolment, and answering
/// "show me this person's oath" should not require walking a training record.</para>
///
/// <para><b>The text is stored, not referenced.</b> What matters legally is the wording the person
/// actually swore. Pointing at a template would mean a later reword silently rewrote history.</para>
///
/// <para><b>Several per employee is normal</b> — a rehire swears again — so there is deliberately
/// no "one per employee" unique index. The current oath is the latest by <see cref="SwornOn"/>.</para>
/// </remarks>
public class EmployeeOathOfSecrecy : TenantEntity
{
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    /// <summary>How the oath reached the record — affirmed in the system, or sworn on paper.</summary>
    public OathAdministrationMethod Method { get; set; } = OathAdministrationMethod.Affirmed;

    /// <summary>The exact wording sworn, snapshotted at the time.</summary>
    [Required]
    [MaxLength(4000)]
    public string OathText { get; set; } = string.Empty;

    /// <summary>The date the oath was sworn — which for a paper oath is not the date it was keyed in.</summary>
    public DateOnly SwornOn { get; set; }

    /// <summary>When the row was created, server-stamped.</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who witnessed it. Required for a paper oath: "sworn before" is what makes it an oath rather
    /// than a note.
    /// </summary>
    public Guid? WitnessedById { get; set; }

    [ForeignKey(nameof(WitnessedById))]
    public virtual Employee? WitnessedBy { get; set; }

    /// <summary>The employee who entered or affirmed this record.</summary>
    public Guid RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee RecordedBy { get; set; } = null!;

    /// <summary>IP address at the time of affirming. Null for a paper oath.</summary>
    [MaxLength(64)]
    public string? SignatureIpAddress { get; set; }

    /// <summary>Hash of (EmployeeId + text + timestamp) for tamper detection. Null for a paper oath.</summary>
    [MaxLength(128)]
    public string? SignatureHash { get; set; }

    // ── The scanned signed copy ───────────────────────────────────────────────
    // ⚠ Three ids, not a path. A caller-supplied file path is the injection sink the medical exam
    // documents, the medical claim documents and the travel attachments were each fixed for, and
    // an oath's scan is the evidence the whole record rests on — it is the last thing that should
    // sit outside the virus-scanned upload gate and the central DMS. Same shape as
    // StaffTravelRequestAttachment, minus the separate table: an oath has one scan, not many.

    /// <summary>The controlled (virus-scanned) upload backing the scan.</summary>
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

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
