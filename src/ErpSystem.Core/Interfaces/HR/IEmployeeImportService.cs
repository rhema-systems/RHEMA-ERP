using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Bulk employee load from the system-generated workbook: template, check, review, commit.
/// </summary>
/// <remarks>
/// See <c>docs/HR/areas/employees/HR-EMPLOYEE-IMPORT-DESIGN.md</c>. Every employee is written through
/// <see cref="IEmployeeService"/>; this service only decides what to send and records what happened.
/// </remarks>
public interface IEmployeeImportService
{
    /// <summary>The template with the tenant's live reference sheets and a version stamp.</summary>
    Task<byte[]> GenerateTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>The column catalogue, for the wizard's guide page.</summary>
    Task<List<EmployeeImportColumnGuideDto>> GetColumnGuideAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses and checks an uploaded workbook and, if it passes the file-level checks, stores the
    /// file through the upload gate and persists a session with one row per data row.
    /// </summary>
    /// <exception cref="EmployeeImportFileRejectedException">The file itself is unusable.</exception>
    Task<EmployeeImportSessionSummaryDto> CreateSessionAsync(
        Stream content, string fileName, string contentType, EmployeeImportMode mode,
        CancellationToken cancellationToken = default);

    Task<List<EmployeeImportSessionSummaryDto>> ListSessionsAsync(CancellationToken cancellationToken = default);

    Task<EmployeeImportSessionSummaryDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<EmployeeImportRowPageDto> GetRowsAsync(
        Guid sessionId, EmployeeImportRowOutcome? outcome, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<EmployeeImportRowDto> SetRowSkipAsync(Guid sessionId, Guid rowId, bool skip, CancellationToken cancellationToken = default);

    /// <summary>The uploaded workbook with a Result column and a cell comment on every finding.</summary>
    Task<byte[]> BuildAnnotatedWorkbookAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Marks the session for the background committer. Refuses under AllOrNothing while errors remain.</summary>
    Task<EmployeeImportSessionSummaryDto> RequestCommitAsync(
        Guid sessionId, EmployeeImportCommitPolicy policy, CancellationToken cancellationToken = default);

    Task<EmployeeImportProgressDto> GetProgressAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<EmployeeImportSessionSummaryDto> CancelSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<List<EmployeeImportFollowUpDto>> GetFollowUpAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>The follow-up list as a workbook HR can hand to the people who own the gaps.</summary>
    Task<byte[]> BuildFollowUpWorkbookAsync(Guid sessionId, CancellationToken cancellationToken = default);

    // ── Background committer entry points (no current user; tenant comes from the session) ──

    /// <summary>Sessions waiting for the committer, oldest first, across every tenant.</summary>
    Task<List<EmployeeImportPendingCommitDto>> FindSessionsAwaitingCommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes up to <paramref name="batchSize"/> pending rows of one session and returns whether any
    /// remain. Each call is meant to run in a fresh DI scope, acting as the user who requested the
    /// commit, so the change tracker stays small and the employee service sees a tenant.
    /// </summary>
    Task<bool> CommitBatchAsync(Guid sessionId, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>The committer itself broke (not a row): record why and stop.</summary>
    Task MarkCommitFailedAsync(Guid sessionId, string message, CancellationToken cancellationToken = default);
}

/// <summary>The workbook cannot be used at all: wrong template, missing columns, empty, too big.</summary>
public sealed class EmployeeImportFileRejectedException : Exception
{
    public IReadOnlyList<EmployeeImportFindingDto> Findings { get; }

    public EmployeeImportFileRejectedException(IReadOnlyList<EmployeeImportFindingDto> findings)
        : base(findings.Count == 0 ? "The workbook was rejected." : findings[0].Message)
    {
        Findings = findings;
    }
}
