using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinanceReportExportService
{
    Task<FinanceReportExportResultDto> ExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FinanceReportExportResultDto> PrintAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken = default);
}
