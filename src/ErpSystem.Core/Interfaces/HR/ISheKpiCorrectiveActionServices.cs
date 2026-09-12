using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// Slice 14 — unified corrective-action tracker (FR-SHE-245) and computed KPIs
// (FR-SHE-248/230/232, FR-CON-001).
// ============================================================================

public interface ISheCorrectiveActionTrackerService
{
    /// <summary>
    /// The union read-model over the four live corrective-action silos (incident,
    /// inspection-hazard, equipment-inspection, committee meeting), filtered and
    /// sorted (overdue first, then due date). Reads the authenticated tenant.
    /// </summary>
    Task<IEnumerable<SheUnifiedCorrectiveActionDto>> GetAllAsync(
        SheCorrectiveActionSource? source = null,
        SheUnifiedActionStatus? status = null,
        Guid? assignedToId = null,
        bool overdueOnly = false,
        DateTime? dueFrom = null,
        DateTime? dueTo = null,
        CancellationToken cancellationToken = default);

    /// <summary>Counts by unified status, overdue escalation tier and source. Reads the authenticated tenant.</summary>
    Task<SheUnifiedCorrectiveActionSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Open (not completed/cancelled) union rows for an explicit tenant — the
    /// reminder engine's entry point; does not read the current user.
    /// </summary>
    Task<IReadOnlyList<SheUnifiedCorrectiveActionDto>> GetOpenForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every union row regardless of status for an explicit tenant — the KPI
    /// engine's entry point for issued/completed period figures; does not read
    /// the current user.
    /// </summary>
    Task<IReadOnlyList<SheUnifiedCorrectiveActionDto>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface ISheKpiComputationService
{
    /// <summary>
    /// Computes every derivable KPI for a period + optional location without
    /// persisting anything. <paramref name="manHours"/> feeds the frequency
    /// rates (LTIFR/TRIR/near-miss) — pass 0/omit to leave them null.
    /// </summary>
    Task<SheComputedKpisDto> PreviewAsync(
        SheSnapshotPeriodType periodType,
        int year,
        int? periodNumber,
        Guid? locationId,
        long manHours = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes the snapshot's derivable figures from live data (using its
    /// hand-entered man-hours) and stamps KpisComputedAt/By. Refused with 422
    /// once the snapshot is reviewed — same lock as the figure edit path.
    /// Hand-entered-only inputs (man-hours, inspections planned, drills planned)
    /// are preserved; PPE compliance is preserved when the requirement matrix
    /// matches no position codes.
    /// </summary>
    Task<ShePerformanceSnapshotDto> ComputeSnapshotAsync(Guid snapshotId, Guid computedById, CancellationToken cancellationToken = default);

    /// <summary>Per-organization-unit compliance for a period (FR-SHE-230).</summary>
    Task<IEnumerable<SheDepartmentalComplianceDto>> GetDepartmentalComplianceAsync(
        SheSnapshotPeriodType periodType, int year, int? periodNumber, CancellationToken cancellationToken = default);

    /// <summary>Contractor SHE ranking for a period (FR-CON-001), best average inspection score first.</summary>
    Task<IEnumerable<SheContractorRankingDto>> GetContractorRankingAsync(
        SheSnapshotPeriodType periodType, int year, int? periodNumber, CancellationToken cancellationToken = default);

    /// <summary>5×5 likelihood × severity counts over the active hazard register (FR-SHE-232).</summary>
    Task<SheHazardHeatmapDto> GetHazardHeatmapAsync(CancellationToken cancellationToken = default);
}
