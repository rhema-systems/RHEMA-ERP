using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// Slice 15 — SHE audit management (FRD §12 / FR-SHE-229) and stop-work
// authority (FR-SHE-200). Statutory submissions live on ISafetyIncidentService.
// ============================================================================

public interface ISheAuditService
{
    Task<IEnumerable<SheAuditSummaryDto>> GetAllAsync(SheAuditStatus? status = null, int? year = null, CancellationToken cancellationToken = default);
    Task<SheAuditDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheAuditDto?> GetByNumberAsync(string auditNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheAuditSummaryDto>> GetUpcomingAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    Task<SheAuditDto> CreateAsync(CreateSheAuditDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditDto> UpdateAsync(UpdateSheAuditDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditDto> StartAsync(StartSheAuditDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditDto> IssueReportAsync(IssueSheAuditReportDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditDto> CloseAsync(CloseSheAuditDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditDto> CancelAsync(CancelSheAuditDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheAuditTeamMemberDto> AddTeamMemberAsync(CreateSheAuditTeamMemberDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveTeamMemberAsync(Guid teamMemberId, CancellationToken cancellationToken = default);

    Task<SheAuditFindingDto> AddFindingAsync(CreateSheAuditFindingDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditFindingDto> UpdateFindingAsync(UpdateSheAuditFindingDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditFindingDto> VerifyFindingAsync(VerifySheAuditFindingDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditFindingDto> CloseFindingAsync(Guid findingId, Guid userId, CancellationToken cancellationToken = default);

    Task<SheAuditFindingActionDto> AddFindingActionAsync(CreateSheAuditFindingActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAuditFindingActionDto> UpdateFindingActionAsync(UpdateSheAuditFindingActionDto dto, Guid userId, CancellationToken cancellationToken = default);
}

public interface ISheStopWorkService
{
    Task<IEnumerable<SheStopWorkOrderDto>> GetAllAsync(SheStopWorkStatus? status = null, CancellationToken cancellationToken = default);
    Task<SheStopWorkOrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheStopWorkOrderDto?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>The caller's own raised orders — the open self-service read.</summary>
    Task<IEnumerable<SheStopWorkOrderDto>> GetMineAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raising is open to every employee. The controller resolves the raiser:
    /// non-HR callers are forced to the token's employee id.
    /// </summary>
    Task<SheStopWorkOrderDto> RaiseAsync(CreateSheStopWorkOrderDto dto, Guid raisedById, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<SheStopWorkOrderDto> RouteAsync(RouteSheStopWorkOrderDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheStopWorkOrderDto> ResolveAsync(ResolveSheStopWorkOrderDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheStopWorkOrderDto> ClearAsync(ClearSheStopWorkOrderDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheStopWorkOrderDto> CancelAsync(CancelSheStopWorkOrderDto dto, Guid cancelledById, Guid userId, CancellationToken cancellationToken = default);
}
