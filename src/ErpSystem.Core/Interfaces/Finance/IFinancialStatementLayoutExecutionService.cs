using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinancialStatementLayoutExecutionService
{
    Task<FinancialStatementLayoutExecutionDto> PreviewVersionAsync(
        Guid versionId,
        FinancialStatementLayoutPreviewRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutExecutionDto> ExecutePublishedAsync(
        FinancialStatementLayoutExecutionRequestDto request,
        CancellationToken cancellationToken = default);
}
