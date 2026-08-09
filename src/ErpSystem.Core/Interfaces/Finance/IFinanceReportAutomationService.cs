using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned lifecycle over the shared report scheduling entities. Keeping
/// this contract in Core allows the API worker and controllers to share the same
/// tenant-safe operations without introducing a second reporting engine.
/// </summary>
public interface IFinanceReportAutomationService
{
    Task<FinanceReportAutomationWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<FinanceReportScheduleDto> CreateAsync(CreateFinanceReportScheduleDto request, CancellationToken cancellationToken = default);
    Task<FinanceReportScheduleDto> UpdateAsync(Guid id, UpdateFinanceReportScheduleDto request, CancellationToken cancellationToken = default);
    Task<FinanceReportScheduleDto> PauseAsync(Guid id, FinanceReportScheduleDecisionDto request, CancellationToken cancellationToken = default);
    Task<FinanceReportScheduleDto> ResumeAsync(Guid id, FinanceReportScheduleDecisionDto request, CancellationToken cancellationToken = default);
    Task<FinanceReportAutomationProcessResultDto> RunNowAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FinanceReportAutomationProcessResultDto> ProcessDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task<FinanceReportArtifactDto> DownloadArtifactAsync(Guid exportId, CancellationToken cancellationToken = default);
}
