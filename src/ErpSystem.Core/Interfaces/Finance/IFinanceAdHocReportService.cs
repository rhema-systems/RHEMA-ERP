using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned lifecycle for safe ad hoc definitions. Execution/export remain
/// on <see cref="ISystemReportProvider"/> so the shared report engine continues
/// to create the standard execution and export evidence.
/// </summary>
public interface IFinanceAdHocReportService : ISystemReportProvider
{
    Task<FinanceAdHocReportWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<FinanceAdHocReportDefinitionDto> GetAsync(
        Guid id, CancellationToken cancellationToken = default);
    Task<FinanceAdHocReportDefinitionDto> CreateAsync(
        CreateFinanceAdHocReportDto request, CancellationToken cancellationToken = default);
    Task<FinanceAdHocReportDefinitionDto> UpdateAsync(
        Guid id, UpdateFinanceAdHocReportDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, string rowVersion, CancellationToken cancellationToken = default);
}
