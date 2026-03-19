using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFixedAssetReportsService
{
    // Data Retrieval
    Task<FixedAssetRegisterDto> GetAssetRegisterAsync(FixedAssetReportQueryDto query);
    Task<List<AssetDisposalReportDto>> GetDisposalReportAsync(FixedAssetReportQueryDto query);
    Task<List<AssetTransferReportDto>> GetTransferReportAsync(FixedAssetReportQueryDto query);
    Task<VerificationSummaryDto> GetVerificationSummaryAsync(Guid sessionId);

    // Export
    Task<byte[]> ExportToExcelAsync(string reportType, FixedAssetReportQueryDto query);
    Task<byte[]> ExportToPdfAsync(string reportType, FixedAssetReportQueryDto query);
}
