using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Read/manage the position vacancies that <c>PositionVacancyLog.LogDepartureAsync</c> writes from
/// the exit paths, and drive the "raise a requisition from a vacancy" and reconcile flows.
///
/// <para>⚠ This used to name a <c>PositionVacancyInterceptor</c> that did not exist (G-3.2,
/// corrected 2026-09-15) — see <c>PositionVacancy</c>'s own remarks.</para>
/// </summary>
public interface IPositionVacancyService
{
    /// <summary>All active positions with filled/vacant headcount and any open tracking vacancy.</summary>
    Task<IEnumerable<PositionEstablishmentDto>> GetEstablishmentOverviewAsync(
        Guid? organizationUnitId = null, bool onlyVacant = false, CancellationToken cancellationToken = default);

    /// <summary>Filtered list of position vacancies.</summary>
    Task<IEnumerable<PositionVacancySummaryDto>> GetVacanciesAsync(
        PositionVacancyStatus? status = null,
        Guid? organizationUnitId = null,
        VacancyReason? reason = null,
        VacancyClassification? classification = null,
        bool includeClosed = false,
        CancellationToken cancellationToken = default);

    /// <summary>Full detail of a single vacancy (with a live current-headcount figure). Null if not found.</summary>
    Task<PositionVacancyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Summary counts for the dashboard cards.</summary>
    Task<PositionVacancyStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Changes a vacancy's status (e.g. Open → UnderReview / Frozen) and optionally appends a note.</summary>
    Task<PositionVacancyDto> UpdateStatusAsync(UpdatePositionVacancyStatusDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Updates the free-text notes on a vacancy.</summary>
    Task<PositionVacancyDto> UpdateNotesAsync(Guid vacancyId, string? notes, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Closes a vacancy without filling it (e.g. the post was eliminated).</summary>
    Task<PositionVacancyDto> CloseAsync(ClosePositionVacancyDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises a Draft <c>StaffRequisition</c> pre-filled from the vacancy, links it back, and flips the
    /// vacancy to <c>RequisitionRaised</c>. Returns the new requisition id so the UI can open it for editing.
    /// </summary>
    Task<RaiseRequisitionResultDto> RaiseRequisitionAsync(
        Guid vacancyId, RaiseRequisitionFromVacancyDto dto, Guid tenantId, Guid requestedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes vacancies from live headcount for the current tenant: opens a structural vacancy where a
    /// position is short with none logged, and closes an open vacancy where the position is back at
    /// establishment. Backfills history and self-heals any gaps left by the interceptor.
    /// </summary>
    Task<ReconcileVacanciesResultDto> ReconcilePositionVacanciesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}
