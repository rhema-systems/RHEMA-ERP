using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Recruitment;

/// <summary>
/// An empty seat in the organisation — a <see cref="EmployeePosition"/> that has lost an occupant and
/// is tracked by HR from the moment it becomes (or is known to be about to become) vacant, through any
/// requisition, until it is filled or closed.
///
/// <para><b>Distinct from <c>JobVacancy</c>.</b> A <c>JobVacancy</c> is an <i>approved recruitment
/// opening being advertised</i>. A <c>PositionVacancy</c> is the upstream fact that a post fell empty —
/// it exists whether or not anyone decides to recruit for it, and is what the stakeholder asked the
/// system to "throw up" automatically when an employee leaves.</para>
///
/// <para><b>Rows are created two ways.</b> <c>PositionVacancyLog.LogDepartureAsync</c> writes one
/// when a post actually falls empty — a termination, a retirement, a separation completing, or a
/// movement taking someone to a different post — carrying the real <see cref="Reason"/>,
/// <see cref="VacatedByEmployeeId"/> and <see cref="VacatedDate"/>. <c>ReconcilePositionVacanciesAsync</c>
/// sweeps for gaps that exist but were never logged, and closes ones that have since been filled.
/// A departure is logged and then classified via <see cref="Classification"/> — never silently
/// dropped even when the position is still at headcount.</para>
///
/// <para>⚠ <b>This paragraph used to describe a <c>PositionVacancyInterceptor</c> that did not
/// exist</b> (G-3.2, corrected 2026-09-15). The name occurred in exactly two places in the
/// solution, both XML doc comments — this one and <c>IPositionVacancyService</c> — and there was no
/// such class; the only registered interceptor was <c>AuditInterceptor</c>. Reconcile was the sole
/// writer, so no departure was ever logged automatically and the guarantee stated here was not one
/// the system provided. The guarantee is now real, but it rests on <b>explicit call sites</b>
/// rather than on an interceptor: a new way to end employment must call
/// <c>PositionVacancyLog.LogDepartureAsync</c>, because nothing at SaveChanges time will catch it.
/// See that class for why the trade was made that way.</para>
/// </summary>
public class PositionVacancy : TenantEntity
{
    /// <summary>The position that became vacant.</summary>
    [Required]
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    /// <summary>
    /// Denormalised from the position at detection time so the "vacant positions" screen can filter and
    /// group by org unit without joining through the position on every row.
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    /// <summary>The employee whose departure/movement opened this vacancy. Null for anticipated or structural vacancies.</summary>
    public Guid? VacatedByEmployeeId { get; set; }

    [ForeignKey(nameof(VacatedByEmployeeId))]
    public virtual Employee? VacatedByEmployee { get; set; }

    /// <summary>Why the seat fell empty (reuses the shared <see cref="VacancyReason"/> vocabulary).</summary>
    public VacancyReason Reason { get; set; } = VacancyReason.Other;

    /// <summary>When the seat became (or is expected to become) vacant.</summary>
    public DateTime VacatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True when this was opened ahead of a known future departure (retirement date, contract expiry)
    /// rather than an actual departure that already happened.
    /// </summary>
    public bool IsAnticipated { get; set; }

    /// <summary>For anticipated vacancies, the date the seat is expected to fall empty.</summary>
    public DateTime? ExpectedVacancyDate { get; set; }

    public PositionVacancyStatus Status { get; set; } = PositionVacancyStatus.Open;

    public VacancyClassification Classification { get; set; } = VacancyClassification.WithinEstablishment;

    // --- Establishment snapshot at detection (for the "why is this classified so?" explanation) ---

    /// <summary>The position's ExpectedHeadcount at the moment the vacancy was detected.</summary>
    public int ExpectedHeadcount { get; set; }

    /// <summary>Count of active employees on the position immediately after the departure/movement.</summary>
    public int ActiveHeadcountAtDetection { get; set; }

    // --- Link to the requisition raised to fill it (if any) ---

    public Guid? StaffRequisitionId { get; set; }

    [ForeignKey(nameof(StaffRequisitionId))]
    public virtual StaffRequisition? StaffRequisition { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime? ClosedDate { get; set; }

    [MaxLength(500)]
    public string? ClosedReason { get; set; }

    /// <summary>Concurrency token — status transitions are load-check-mutate-save.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
