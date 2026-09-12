using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Employee-relations analytics — area 9c slice 8.
/// </summary>
/// <remarks>
/// <b>One method, one window, one materialised set.</b> Deliberately not a family of per-figure
/// methods: area 7 shipped a dashboard and an analytics page that disagreed about the same number
/// because each ran its own query over a slightly different set. Every figure here is computed from
/// the same list, so the page cannot contradict itself — and a caller cannot assemble a screen from
/// figures taken over different windows.
/// </remarks>
public interface IEmployeeRelationsAnalyticsService
{
    /// <param name="from">Filed on or after. Null for all time.</param>
    /// <param name="to">Filed on or before. Null for all time.</param>
    Task<EmployeeRelationsAnalyticsDto> GetAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
