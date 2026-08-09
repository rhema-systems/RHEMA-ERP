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

    /// <summary>
    /// Authorizes one persisted report identifier. Static module providers normally share one
    /// permission across every report and therefore inherit this default implementation. Providers
    /// with record-level visibility (for example, private Finance ad hoc definitions) override it
    /// so the shared Reports catalogue cannot disclose another user's report name or metadata.
    /// </summary>
    Task<bool> CanReadReportAsync(
        string reportQuery,
        bool isAdministrator,
        CancellationToken cancellationToken = default) =>
        CanReadAsync(isAdministrator, cancellationToken);
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
