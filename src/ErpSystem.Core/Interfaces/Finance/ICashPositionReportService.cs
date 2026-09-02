using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Canonical read boundary for the current cash position. API and printable output must consume
/// this same DTO so totals, currency semantics and the as-of timestamp cannot drift.
/// </summary>
public interface ICashPositionReportService
{
    Task<CashPositionSummaryDto> GetCurrentPositionAsync(CancellationToken cancellationToken = default);
}
