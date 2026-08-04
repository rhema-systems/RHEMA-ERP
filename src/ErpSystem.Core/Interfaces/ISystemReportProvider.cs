using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Executes protected, application-owned report definitions through the shared report engine.
/// Implementations reconcile authoritative module owners and never accept arbitrary SQL.
/// </summary>
public interface ISystemReportProvider
{
    bool CanHandle(string? reportQuery);
    bool OwnsIdentifier(string? reportQuery);
    string? ResolveCode(string? reportQuery);
    Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default);
    Task<ReportResultDto> ExecuteAsync(
        string reportQuery,
        ExecuteReportDto request,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
    Task AuthorizeExportAsync(
        string reportQuery,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}
