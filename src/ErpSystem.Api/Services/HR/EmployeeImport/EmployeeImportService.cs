using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR.EmployeeImport;

/// <summary>
/// Employee bulk import: template, check, review, commit. See <c>docs/HR/HR-EMPLOYEE-IMPORT-DESIGN.md</c>.
/// </summary>
/// <remarks>
/// <para><b>Reads</b> go straight to the DbContext (tenant-filtered, soft-delete aware) because the
/// checker wants dictionaries, not DTOs. <b>Writes to the register</b> go through
/// <see cref="IEmployeeService"/> only — one call per employee, then one per child record — so the
/// staff-number register, the payroll gate and tenant stamping apply as on the create form.</para>
/// <para>⚠ The committer clears the change tracker between rows. The employee service saves as it
/// goes, and a refused row would otherwise leave a poisoned <c>Added</c> entity that fails every
/// later save with the same error. Each row therefore reloads its session and row entities before
/// recording its outcome.</para>
/// </remarks>
public sealed class EmployeeImportService : IEmployeeImportService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IEmployeeService _employees;
    private readonly IStaffNumberService _staffNumbers;
    private readonly IHrControlledDocumentService _documents;
    private readonly ILogger<EmployeeImportService> _logger;

    public EmployeeImportService(
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IEmployeeService employees,
        IStaffNumberService staffNumbers,
        IHrControlledDocumentService documents,
        ILogger<EmployeeImportService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _employees = employees;
        _staffNumbers = staffNumbers;
        _documents = documents;
        _logger = logger;
    }

    private Guid RequireTenant()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty) throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ── Template ─────────────────────────────────────────────────────────────────────────────

    public async Task<byte[]> GenerateTemplateAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var refs = await LoadReferenceDataAsync(tenantId, cancellationToken);
        var columns = EmployeeImportColumns.Build(refs.IdentificationTypes);
        return EmployeeImportWorkbooks.BuildTemplate(refs, columns, tenantId.ToString("N"));
    }

    public async Task<List<EmployeeImportColumnGuideDto>> GetColumnGuideAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var types = await LoadIdentificationTypesAsync(tenantId, cancellationToken);
        return EmployeeImportColumns.Build(types).Select(c => new EmployeeImportColumnGuideDto
        {
            Key = c.Key, Header = c.Header, Required = c.Required, Kind = c.Kind.ToString(), Help = c.Help,
        }).ToList();
    }

    // ── Upload → session ─────────────────────────────────────────────────────────────────────

    public async Task<EmployeeImportSessionSummaryDto> CreateSessionAsync(
        Stream content, string fileName, string contentType, EmployeeImportMode mode, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Unknown import mode.");
        var userId = _currentUser.UserId;
        var safeName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "employees.xlsx" : fileName);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
            throw new EmployeeImportFileRejectedException([FileError("The uploaded file is empty.")]);
        if (buffer.Length > EmployeeImportColumns.MaxFileBytes)
            throw new EmployeeImportFileRejectedException([FileError($"The file is {buffer.Length / 1_048_576.0:N1} MB; the limit is {EmployeeImportColumns.MaxFileBytes / 1_048_576} MB.")]);
        if (!safeName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new EmployeeImportFileRejectedException([FileError("Only .xlsx workbooks are accepted. Save the file as 'Excel Workbook (*.xlsx)'.")]);

        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var refs = await LoadReferenceDataAsync(tenantId, cancellationToken);
        var columns = EmployeeImportColumns.Build(refs.IdentificationTypes);

        buffer.Position = 0;
        var raw = EmployeeImportWorkbookReader.ReadRaw(buffer, columns, refs.IdentificationTypes);
        if (raw.FileRejected)
            throw new EmployeeImportFileRejectedException(raw.FileFindings);

        // An update needs the register's current values — for exactly the numbers in the file.
        if (mode != EmployeeImportMode.CreateOnly)
            await LoadSnapshotsAsync(refs, raw.StaffNumbers.ToList(), cancellationToken);

        var parsed = EmployeeImportWorkbookReader.Resolve(raw, refs, columns, mode);
        if (parsed.FileRejected)
            throw new EmployeeImportFileRejectedException(parsed.FileFindings);

        var earlier = await _db.EmployeeImportSessions
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.FileHash == hash
                        && (s.Status == EmployeeImportSessionStatus.Committed || s.Status == EmployeeImportSessionStatus.CommittedWithErrors))
            .OrderByDescending(s => s.UploadedOn)
            .Select(s => s.Reference)
            .FirstOrDefaultAsync(cancellationToken);
        if (earlier != null)
            parsed.FileFindings.Add(FileWarning(mode == EmployeeImportMode.CreateOnly
                ? $"This exact file was already committed as import {earlier}. Committing it again will fail on every staff number."
                : $"This exact file was already committed as import {earlier}."));

        var session = new EmployeeImportSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = safeName,
            FileHash = hash,
            FileSizeBytes = bytes.LongLength,
            TemplateVersion = parsed.TemplateVersion,
            UploadedByUserId = userId,
            UploadedByName = Truncate(_currentUser.FullName, 200),
            UploadedOn = DateTime.UtcNow,
            Status = EmployeeImportSessionStatus.Validated,
            Mode = mode,
            TotalRows = parsed.Rows.Count,
            ReadyCount = parsed.Rows.Count(r => r.Outcome == EmployeeImportRowOutcome.Ready),
            WarningCount = parsed.Rows.Count(r => r.Outcome == EmployeeImportRowOutcome.Warning),
            ErrorCount = parsed.Rows.Count(r => r.Outcome == EmployeeImportRowOutcome.Error),
            CreateCount = parsed.Rows.Count(r => r.Outcome != EmployeeImportRowOutcome.Error && r.Action == EmployeeImportRowAction.Create),
            UpdateCount = parsed.Rows.Count(r => r.Outcome != EmployeeImportRowOutcome.Error && r.Action == EmployeeImportRowAction.Update),
            FileFindingsJson = JsonSerializer.Serialize(parsed.FileFindings, Json),
            CreatedById = userId,
        };
        session.Reference = $"EMPIMP-{session.UploadedOn:yyyyMMdd}-{session.Id.ToString("N")[..4].ToUpperInvariant()}";

        var rows = parsed.Rows.Select(r => new EmployeeImportRow
        {
            TenantId = tenantId,
            SessionId = session.Id,
            RowNumber = r.RowNumber,
            StaffNumber = Truncate(r.StaffNumber, 50),
            DisplayName = Truncate(r.DisplayName, 250),
            EmploymentType = r.EmploymentType,
            RawJson = JsonSerializer.Serialize(r.Raw, Json),
            ResolvedJson = r.Resolved == null ? null : JsonSerializer.Serialize(r.Resolved, Json),
            FindingsJson = JsonSerializer.Serialize(r.Findings, Json),
            Outcome = r.Outcome,
            Action = r.Action,
            TargetEmployeeId = r.TargetEmployeeId,
            ErrorCount = r.ErrorCount,
            WarningCount = r.WarningCount,
            ManagerRowNumber = r.ManagerRowNumber,
            CreatedById = userId,
        }).ToList();

        _db.EmployeeImportSessions.Add(session);
        _db.EmployeeImportRows.AddRange(rows);
        await _db.SaveChangesAsync(cancellationToken);

        await RetainSourceFileAsync(session, bytes, safeName, contentType, cancellationToken);

        _logger.LogInformation(
            "Employee import {Reference} checked: {Rows} rows, {Ready} ready, {Warnings} warnings, {Errors} errors",
            session.Reference, session.TotalRows, session.ReadyCount, session.WarningCount, session.ErrorCount);

        return Map(session);
    }

    /// <summary>
    /// Keeps the uploaded workbook through the controlled gate. A file the gate refuses on its
    /// content (type, size, malware) takes the session down with it; a gate that is merely
    /// unavailable (scanner down, quota service unreachable) does not stop HR's work — the rows were
    /// checked and can still be committed — but the session says the file was not retained.
    /// </summary>
    private async Task RetainSourceFileAsync(
        EmployeeImportSession session, byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes);
        var formFile = new FormFile(stream, 0, bytes.LongLength, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : contentType,
        };

        try
        {
            var document = await _documents.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = session.TenantId,
                ActorUserId = session.UploadedByUserId,
                ActorName = session.UploadedByName,
                Category = ControlledFileUploadCategories.HrEmployeeImportWorkbooks,
                File = formFile,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Employee import",
                    SourceEntityType = nameof(EmployeeImportSession),
                    SourceRecordId = session.Id,
                    SourceRecordReference = session.Reference,
                    Title = fileName,
                    DocumentType = "Employee import workbook",
                    ChangeSummary = $"{session.TotalRows} rows checked: {session.ReadyCount} ready, {session.WarningCount} warnings, {session.ErrorCount} errors",
                },
            }, cancellationToken);

            session.SourceFileUploadRecordId = document.FileUploadRecordId;
            session.SourceDocumentRecordId = document.DocumentRecordId;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (ControlledFileUploadException ex) when (ex.StatusCode >= 500)
        {
            _logger.LogWarning(ex, "Employee import {Reference}: source file not retained ({Code})", session.Reference, ex.Code);
            var findings = ReadFindings(session.FileFindingsJson);
            findings.Add(FileWarning($"The uploaded file could not be retained in the document store ({ex.Message}). The rows were checked and can still be committed."));
            session.FileFindingsJson = JsonSerializer.Serialize(findings, Json);
            session.Notes = Truncate($"Source file not retained: {ex.Code} — {ex.Message}", 1000);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (ControlledFileUploadException)
        {
            // Refused on content. No session for a file the gate would not hold.
            var rows = await _db.EmployeeImportRows.Where(r => r.SessionId == session.Id).ToListAsync(cancellationToken);
            _db.EmployeeImportRows.RemoveRange(rows);
            _db.EmployeeImportSessions.Remove(session);
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    // ── Sessions and rows ────────────────────────────────────────────────────────────────────

    public async Task<List<EmployeeImportSessionSummaryDto>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var sessions = await _db.EmployeeImportSessions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .OrderByDescending(s => s.UploadedOn)
            .Take(200)
            .ToListAsync(cancellationToken);
        return sessions.Select(s => Map(s)).ToList();
    }

    public async Task<EmployeeImportSessionSummaryDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: false, cancellationToken);
        return session == null ? null : Map(session);
    }

    public async Task<EmployeeImportRowPageDto> GetRowsAsync(
        Guid sessionId, EmployeeImportRowOutcome? outcome, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: false, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.EmployeeImportRows.AsNoTracking().Where(r => r.SessionId == session.Id);
        if (outcome.HasValue) query = query.Where(r => r.Outcome == outcome.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => (r.StaffNumber != null && r.StaffNumber.Contains(term))
                                     || (r.DisplayName != null && r.DisplayName.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(r => r.RowNumber).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new EmployeeImportRowPageDto
        {
            Items = rows.Select(r => Map(r)).ToList(), Total = total, Page = page, PageSize = pageSize,
        };
    }

    public async Task<EmployeeImportRowDto> SetRowSkipAsync(Guid sessionId, Guid rowId, bool skip, CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: true, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");
        if (session.Status != EmployeeImportSessionStatus.Validated)
            throw new InvalidOperationException($"Rows can only be skipped while the import is awaiting commit (it is {session.Status}).");

        var row = await _db.EmployeeImportRows.FirstOrDefaultAsync(r => r.Id == rowId && r.SessionId == session.Id, cancellationToken)
                  ?? throw new ArgumentException("Import row not found.");
        row.Skip = skip;
        row.LastModifiedById = _currentUser.UserId;
        session.SkippedCount = await _db.EmployeeImportRows.CountAsync(r => r.SessionId == session.Id && r.Id != row.Id && r.Skip, cancellationToken) + (skip ? 1 : 0);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<byte[]> BuildAnnotatedWorkbookAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: false, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");
        var rows = await _db.EmployeeImportRows.AsNoTracking()
            .Where(r => r.SessionId == session.Id).OrderBy(r => r.RowNumber).ToListAsync(cancellationToken);

        var refs = await LoadReferenceDataAsync(session.TenantId, cancellationToken);
        var columns = EmployeeImportColumns.Build(refs.IdentificationTypes);
        var annotated = rows.Select(r => new EmployeeImportAnnotatedRow(
            r.RowNumber,
            ReadValues(r.RawJson),
            r.Outcome,
            r.Skip,
            ReadFindings(r.FindingsJson),
            r.CommitMessage)).ToList();
        return EmployeeImportWorkbooks.BuildAnnotated(refs, columns, annotated, session.Reference, session.TenantId.ToString("N"));
    }

    // ── Commit ───────────────────────────────────────────────────────────────────────────────

    public async Task<EmployeeImportSessionSummaryDto> RequestCommitAsync(
        Guid sessionId, EmployeeImportCommitPolicy policy, CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: true, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");
        if (session.Status != EmployeeImportSessionStatus.Validated)
            throw new InvalidOperationException($"This import is {session.Status} and cannot be committed again.");

        var rows = await _db.EmployeeImportRows.Where(r => r.SessionId == session.Id).ToListAsync(cancellationToken);
        var live = rows.Where(r => !r.Skip).ToList();
        var errors = live.Count(r => r.Outcome == EmployeeImportRowOutcome.Error);
        var toCommit = live.Count(r => r.Outcome is EmployeeImportRowOutcome.Ready or EmployeeImportRowOutcome.Warning);

        if (policy == EmployeeImportCommitPolicy.AllOrNothing && errors > 0)
            throw new InvalidOperationException(
                $"{errors} row(s) still have errors. Fix them in the checked file and upload again, skip them, or commit valid rows only.");
        if (toCommit == 0)
            throw new InvalidOperationException("There is nothing to commit: every row has errors or has been skipped.");

        foreach (var row in rows.Where(r => r.Skip))
            row.Outcome = EmployeeImportRowOutcome.Skipped;

        session.Status = EmployeeImportSessionStatus.CommitRequested;
        session.CommitPolicy = policy;
        session.CommitRequestedByUserId = _currentUser.UserId;
        session.CommitRequestedOn = DateTime.UtcNow;
        session.SkippedCount = rows.Count(r => r.Skip);
        session.ErrorCount = errors;
        session.ReadyCount = live.Count(r => r.Outcome == EmployeeImportRowOutcome.Ready);
        session.WarningCount = live.Count(r => r.Outcome == EmployeeImportRowOutcome.Warning);
        session.CreateCount = live.Count(r => r.Outcome != EmployeeImportRowOutcome.Error && r.Action == EmployeeImportRowAction.Create);
        session.UpdateCount = live.Count(r => r.Outcome != EmployeeImportRowOutcome.Error && r.Action == EmployeeImportRowAction.Update);
        session.LastModifiedById = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee import {Reference}: commit requested ({Policy}), {Rows} rows to write",
            session.Reference, policy, toCommit);
        return Map(session);
    }

    public async Task<EmployeeImportProgressDto> GetProgressAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: false, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");
        var toCommit = await _db.EmployeeImportRows.CountAsync(r => r.SessionId == session.Id && !r.Skip
            && (r.Outcome == EmployeeImportRowOutcome.Ready || r.Outcome == EmployeeImportRowOutcome.Warning
                || r.Outcome == EmployeeImportRowOutcome.Committed || r.Outcome == EmployeeImportRowOutcome.CommittedWithIssues
                || r.Outcome == EmployeeImportRowOutcome.Failed), cancellationToken);
        return new EmployeeImportProgressDto
        {
            Status = session.Status,
            TotalRows = session.TotalRows,
            ToCommit = toCommit,
            CommittedCount = session.CommittedCount,
            CreatedCount = session.CreatedCount,
            UpdatedCount = session.UpdatedCount,
            FailedCount = session.FailedCount,
            Remaining = Math.Max(0, toCommit - session.CommittedCount - session.FailedCount),
            CommitStartedOn = session.CommitStartedOn,
            CommitCompletedOn = session.CommitCompletedOn,
            FailureMessage = session.FailureMessage,
        };
    }

    public async Task<EmployeeImportSessionSummaryDto> CancelSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: true, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");
        if (session.Status is not (EmployeeImportSessionStatus.Validated or EmployeeImportSessionStatus.CommitRequested))
            throw new InvalidOperationException($"An import that is {session.Status} cannot be cancelled.");
        session.Status = EmployeeImportSessionStatus.Cancelled;
        session.LastModifiedById = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(session);
    }

    public async Task<List<EmployeeImportFollowUpDto>> GetFollowUpAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: false, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");
        var rows = await _db.EmployeeImportRows.AsNoTracking()
            .Where(r => r.SessionId == session.Id && (r.CreatedEmployeeId != null || r.TargetEmployeeId != null)
                        && (r.Outcome == EmployeeImportRowOutcome.Committed || r.Outcome == EmployeeImportRowOutcome.CommittedWithIssues))
            .OrderBy(r => r.RowNumber)
            .ToListAsync(cancellationToken);

        var result = new List<EmployeeImportFollowUpDto>();
        foreach (var row in rows)
        {
            var items = ReadFindings(row.FindingsJson)
                .Where(f => f.Severity == EmployeeImportFindingSeverity.Warning)
                .Select(f => f.Message)
                .ToList();
            if (!string.IsNullOrWhiteSpace(row.CommitMessage)) items.Add(row.CommitMessage);
            if (items.Count == 0) continue;
            result.Add(new EmployeeImportFollowUpDto
            {
                EmployeeId = (row.CreatedEmployeeId ?? row.TargetEmployeeId)!.Value,
                StaffNumber = row.StaffNumber ?? string.Empty,
                DisplayName = row.DisplayName ?? string.Empty,
                RowNumber = row.RowNumber,
                Items = items,
            });
        }
        return result;
    }

    public async Task<byte[]> BuildFollowUpWorkbookAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, tracking: false, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");
        var items = await GetFollowUpAsync(sessionId, cancellationToken);
        return EmployeeImportWorkbooks.BuildFollowUp(items, session.Reference);
    }

    // ── Background committer ─────────────────────────────────────────────────────────────────

    public async Task<List<EmployeeImportPendingCommitDto>> FindSessionsAwaitingCommitAsync(CancellationToken cancellationToken = default)
    {
        return await _db.EmployeeImportSessions.AsNoTracking().IgnoreQueryFilters()
            .Where(s => !s.IsDeleted && (s.Status == EmployeeImportSessionStatus.CommitRequested || s.Status == EmployeeImportSessionStatus.Committing))
            .OrderBy(s => s.CommitRequestedOn)
            .Select(s => new EmployeeImportPendingCommitDto
            {
                SessionId = s.Id,
                TenantId = s.TenantId,
                RequestedByUserId = s.CommitRequestedByUserId ?? s.UploadedByUserId,
                RequestedByName = s.UploadedByName,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CommitBatchAsync(Guid sessionId, int batchSize, CancellationToken cancellationToken = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 200);
        var session = await _db.EmployeeImportSessions.FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, cancellationToken)
                      ?? throw new ArgumentException("Import session not found.");

        if (session.Status == EmployeeImportSessionStatus.CommitRequested)
        {
            session.Status = EmployeeImportSessionStatus.Committing;
            session.CommitStartedOn = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        if (session.Status != EmployeeImportSessionStatus.Committing) return false;

        var pending = await _db.EmployeeImportRows.AsNoTracking()
            .Where(r => r.SessionId == sessionId && !r.Skip
                        && (r.Outcome == EmployeeImportRowOutcome.Ready || r.Outcome == EmployeeImportRowOutcome.Warning))
            .OrderBy(r => r.RowNumber)
            .Select(r => r.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            await FinalizeCommitAsync(sessionId, cancellationToken);
            return false;
        }

        foreach (var rowId in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CommitRowAsync(sessionId, rowId, cancellationToken);
        }
        return true;
    }

    private async Task CommitRowAsync(Guid sessionId, Guid rowId, CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var snapshot = await _db.EmployeeImportRows.AsNoTracking().FirstAsync(r => r.Id == rowId, cancellationToken);
        var resolved = string.IsNullOrEmpty(snapshot.ResolvedJson) ? null
            : JsonSerializer.Deserialize<EmployeeImportResolvedRow>(snapshot.ResolvedJson, Json);

        Guid? createdId = null;
        Guid? writtenId = null;   // the employee the row ended up on: created, or the update's target
        string? failure = null;
        var issues = new List<string>();
        var isUpdate = resolved?.Action == EmployeeImportRowAction.Update;
        var noChanges = false;

        if (resolved == null)
        {
            failure = "The row has no resolved payload; upload the file again.";
        }
        else if (isUpdate)
        {
            if (resolved.TargetEmployeeId == null || resolved.Update == null)
                failure = "The update row has no target; upload the file again.";
            else if (resolved.Changes.Count == 0 && !resolved.SalaryChanged && resolved.Identifications.Count == 0
                     && resolved.Qualifications.Count == 0 && resolved.ManagerRowNumber == null)
            {
                writtenId = resolved.TargetEmployeeId;
                noChanges = true;
            }
            else
            {
                try
                {
                    // Only fields the sheet supplied are set; the service treats null as "leave alone".
                    // Child-record changes (level/notch, qualifications, new documents) are written
                    // below through their own services, not through the employee update.
                    var hasFieldChanges = resolved.Changes.Any(c =>
                        c.Field is not ("Salary Level / Notch" or "Qualification" or "Professional Qualification")
                        && !c.Field.EndsWith("(new document)", StringComparison.Ordinal));
                    if (hasFieldChanges || resolved.Update.ManagerId.HasValue)
                        await _employees.UpdateEmployeeAsync(resolved.TargetEmployeeId.Value, resolved.Update, cancellationToken);
                    writtenId = resolved.TargetEmployeeId;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    failure = ex.Message;
                }
                catch (DbUpdateException ex)
                {
                    failure = "The database refused the row: " + (ex.InnerException?.Message ?? ex.Message);
                }
            }
        }
        else
        {
            try
            {
                var created = await _employees.ImportEmployeeAsync(resolved.Employee, cancellationToken);
                createdId = created.Id;
                writtenId = created.Id;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                failure = ex.Message;
            }
            catch (DbUpdateException ex)
            {
                failure = "The database refused the row: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }

        if (resolved != null && writtenId.HasValue && !noChanges)
        {
            {   // children — each its own unit of work, each failure an issue on the row, not a failed row
                var employeeId = writtenId.Value;
                var effective = (isUpdate
                    ? resolved.Update?.DateEmployed ?? DateOnly.FromDateTime(DateTime.UtcNow)
                    : resolved.Employee.DateEmployed ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue);
                var onPayroll = isUpdate ? resolved.Update?.IsOnPayroll ?? true : resolved.Employee.IsOnPayroll;

                if (resolved.Salary != null && onPayroll && (!isUpdate || resolved.SalaryChanged))
                    await TryChildAsync(issues, "Salary level/notch not assigned", () => _employees.AssignSalaryAsync(new CreateEmployeeSalaryAssignmentDto
                    {
                        EmployeeId = employeeId,
                        GradeId = resolved.Salary.GradeId,
                        LevelId = resolved.Salary.LevelId,
                        NotchId = resolved.Salary.NotchId,
                        EffectiveDate = isUpdate ? DateTime.UtcNow.Date : effective,
                        AssignmentReason = isUpdate ? "Updated from the employee register" : "Imported from the employee register",
                    }, cancellationToken));

                if (resolved.Contract != null && !isUpdate)
                    await TryChildAsync(issues, "Contract not recorded", () => _employees.AddContractAsync(new CreateEmployeeContractDetailDto
                    {
                        EmployeeId = employeeId,
                        ContractNumber = Truncate(resolved.Contract.ContractNumber, 50)!,
                        EmploymentType = resolved.Employee.EmploymentType,
                        StartDate = resolved.Contract.StartDate,
                        EndDate = resolved.Contract.EndDate,
                        Salary = resolved.Contract.MonthlySalary,
                        PayFrequency = PayFrequency.Monthly,
                        CurrencyCode = "GHS",
                        Notes = "Imported from the employee register",
                    }, cancellationToken));

                foreach (var q in resolved.Qualifications)
                    await TryChildAsync(issues, $"{q.Kind} qualification not recorded", () => _employees.AddQualificationAsync(new CreateEmployeeQualificationDto
                    {
                        EmployeeId = employeeId,
                        QualificationId = q.QualificationId,
                        CustomQualificationName = q.CustomName,
                        Institution = q.Institution,
                        CompletionDate = q.YearCompleted.HasValue ? new DateOnly(q.YearCompleted.Value, 12, 31) : null,
                        Notes = $"{q.Kind} qualification, imported from the employee register",
                    }, cancellationToken));

                foreach (var id in resolved.Identifications)
                    await TryChildAsync(issues, $"{id.TypeName} not recorded", () => _employees.AddIdentificationCardAsync(new CreateEmployeeIdentificationCardDto
                    {
                        EmployeeId = employeeId,
                        IdentificationTypeId = id.IdentificationTypeId,
                        DocumentNumber = id.DocumentNumber,
                        ExpiryDate = id.ExpiryDate,
                        Notes = "Imported from the employee register",
                    }, cancellationToken));
            }
        }

        // Record the outcome on fresh entities: whatever the services left in the tracker is not ours.
        _db.ChangeTracker.Clear();
        var row = await _db.EmployeeImportRows.FirstAsync(r => r.Id == rowId, cancellationToken);
        var session = await _db.EmployeeImportSessions.FirstAsync(s => s.Id == sessionId, cancellationToken);
        row.CommittedOn = DateTime.UtcNow;
        if (writtenId.HasValue)
        {
            row.CreatedEmployeeId = createdId;
            row.Outcome = issues.Count == 0 ? EmployeeImportRowOutcome.Committed : EmployeeImportRowOutcome.CommittedWithIssues;
            row.CommitMessage = noChanges ? "No changes — the employee was left as it is."
                : issues.Count == 0 ? null : Truncate(string.Join(" | ", issues), 2000);
            session.CommittedCount++;
            if (isUpdate) session.UpdatedCount++; else session.CreatedCount++;
        }
        else
        {
            row.Outcome = EmployeeImportRowOutcome.Failed;
            row.CommitMessage = Truncate(failure, 2000);
            session.FailedCount++;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task TryChildAsync(List<string> issues, string label, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            issues.Add($"{label}: {(ex is DbUpdateException ? ex.InnerException?.Message ?? ex.Message : ex.Message)}");
            _db.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Manager links between rows of the same file, the counter reconcile for every register touched,
    /// and the final status. Idempotent: a restart mid-way simply runs it again.
    /// </summary>
    private async Task FinalizeCommitAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var rows = await _db.EmployeeImportRows.AsNoTracking()
            .Where(r => r.SessionId == sessionId).ToListAsync(cancellationToken);
        var byRowNumber = rows.ToDictionary(r => r.RowNumber);

        foreach (var row in rows.Where(r => r.ManagerRowNumber.HasValue && (r.CreatedEmployeeId.HasValue || r.TargetEmployeeId.HasValue)
                                             && r.Outcome is EmployeeImportRowOutcome.Committed or EmployeeImportRowOutcome.CommittedWithIssues))
        {
            string? issue = null;
            var employeeId = (row.CreatedEmployeeId ?? row.TargetEmployeeId)!.Value;
            var managerId = byRowNumber.TryGetValue(row.ManagerRowNumber!.Value, out var managerRow)
                ? managerRow.CreatedEmployeeId ?? managerRow.TargetEmployeeId
                : null;
            if (managerId == null)
                issue = $"Manager (row {row.ManagerRowNumber}) did not import; no manager set.";
            else
            {
                try
                {
                    await _employees.AssignManagerAsync(employeeId, managerId.Value, cancellationToken);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
                {
                    issue = "Manager not set: " + ex.Message;
                    _db.ChangeTracker.Clear();
                }
            }
            if (issue == null) continue;

            _db.ChangeTracker.Clear();
            var tracked = await _db.EmployeeImportRows.FirstAsync(r => r.Id == row.Id, cancellationToken);
            tracked.Outcome = EmployeeImportRowOutcome.CommittedWithIssues;
            tracked.CommitMessage = Truncate(string.IsNullOrEmpty(tracked.CommitMessage) ? issue : tracked.CommitMessage + " | " + issue, 2000);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // The counter: every row already advanced it through AcceptImportedAsync; the reconcile is
        // the belt to those braces, and the one that copes with numbers the rule could not read.
        var reconciled = new List<EmployeeImportCounterReconcileDto>();
        var types = rows.Where(r => r.CreatedEmployeeId != null && r.EmploymentType != null).Select(r => r.EmploymentType!.Value).Distinct();
        foreach (var type in types)
        {
            try
            {
                var rule = await _staffNumbers.GetRuleAsync(type, cancellationToken);
                if (rule == null || !rule.AutoGenerate) continue;
                if (reconciled.Any(r => r.FormatId == rule.Id)) continue;
                var before = await _staffNumbers.InspectCounterAsync(rule.Id, cancellationToken);
                var after = await _staffNumbers.ReconcileCounterAsync(rule.Id, cancellationToken);
                reconciled.Add(new EmployeeImportCounterReconcileDto
                {
                    FormatId = rule.Id,
                    Register = rule.Name,
                    CounterBefore = before.CounterStandsAt,
                    CounterAfter = after.CounterStandsAt,
                    Message = after.NextNumberIsInUse ? $"Next number {after.NextNumber} is still in use — inspect the register." : $"Next number: {after.NextNumber}",
                });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Employee import {Session}: counter reconcile for {Type} failed", sessionId, type);
                _db.ChangeTracker.Clear();
            }
        }

        _db.ChangeTracker.Clear();
        var session = await _db.EmployeeImportSessions.FirstAsync(s => s.Id == sessionId, cancellationToken);
        var anyIssue = session.FailedCount > 0 || rows.Any(r => r.Outcome == EmployeeImportRowOutcome.CommittedWithIssues);
        session.Status = anyIssue ? EmployeeImportSessionStatus.CommittedWithErrors : EmployeeImportSessionStatus.Committed;
        session.CommitCompletedOn = DateTime.UtcNow;
        session.CounterReconciliationJson = JsonSerializer.Serialize(reconciled, Json);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee import {Reference} finished: {Committed} created, {Failed} failed, status {Status}",
            session.Reference, session.CommittedCount, session.FailedCount, session.Status);
    }

    public async Task MarkCommitFailedAsync(Guid sessionId, string message, CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        var session = await _db.EmployeeImportSessions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null) return;
        session.Status = EmployeeImportSessionStatus.Failed;
        session.FailureMessage = Truncate(message, 2000);
        session.CommitCompletedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ── Reference data ───────────────────────────────────────────────────────────────────────

    private async Task<List<IdentificationTypeRef>> LoadIdentificationTypesAsync(Guid tenantId, CancellationToken ct)
    {
        return await _db.IdentificationTypes.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new IdentificationTypeRef(t.Id, t.Name, t.HasExpiryDate))
            .ToListAsync(ct);
    }

    private async Task<EmployeeImportReferenceData> LoadReferenceDataAsync(Guid tenantId, CancellationToken ct)
    {
        var departments = new LookupTable<DepartmentRef>();
        foreach (var d in await _db.Departments.AsNoTracking()
                     .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.IsActive)
                     .Select(d => new DepartmentRef(d.Id, d.Code, d.Name)).ToListAsync(ct))
            departments.Add(d, d.Name, d.Code, d.Name);

        var sections = new LookupTable<SectionRef>();
        foreach (var s in await _db.Sections.AsNoTracking()
                     .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive)
                     .Select(s => new SectionRef(s.Id, s.Code, s.Name, s.DepartmentId)).ToListAsync(ct))
            sections.Add(s, s.Name, s.Code, s.Name);

        var positions = new LookupTable<PositionRef>();
        foreach (var p in await _db.EmployeePositions.AsNoTracking()
                     .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsActive)
                     .Select(p => new PositionRef(p.Id, p.Code, p.Title, p.OrganizationUnitId, p.StaffLevelId)).ToListAsync(ct))
            positions.Add(p, p.Title, p.Code, p.Title);

        var locations = new LookupTable<LocationRef>();
        foreach (var l in await _db.Locations.AsNoTracking()
                     .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.IsActive)
                     .Select(l => new LocationRef(l.Id, l.Code, l.Name)).ToListAsync(ct))
            locations.Add(l, l.Name, l.Code, l.Name);

        // ── Administrative geography ────────────────────────────────────────────────────────────
        // The Region and City columns resolve to a GeoAreaId where the tree can place them. Both
        // lookups stay EMPTY when no scheme is seeded, and the reader then leaves the columns as
        // the free text they have always been.
        var geoRegions = new LookupTable<GeoAreaRef>();
        var geoSubAreas = new GeoAreaLookup();
        var geoRetired = new LookupTable<GeoAreaRef>();

        var defaultSchemeIds = await _db.GeoSchemes.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive && s.IsDefault)
            .Select(s => s.Id).ToListAsync(ct);

        if (defaultSchemeIds.Count > 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // ⚠ NOT filtered on IsActive. A dissolved area is inactive BY DEFINITION — the seeder
            // sets IsActive from whether it has an end date — so filtering here loaded none of
            // them and "Brong Ahafo" came back as a name nobody had heard of, which is the exact
            // failure the retired lookup exists to prevent. The live/retired split below is what
            // decides whether an area can be resolved TO; this query only decides what is known.
            var areas = await _db.GeoAreas.AsNoTracking()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted
                         && defaultSchemeIds.Contains(a.SchemeId))
                .Select(a => new
                {
                    a.Id, a.Code, a.Name, a.ParentAreaId, a.EffectiveTo, a.SupersededByGeoAreaId, a.IsActive,
                    TierName = a.GeoLevel.Name, a.GeoLevel.LevelNumber,
                })
                .ToListAsync(ct);

            // ⚠ Areas that have ceased to exist are kept OUT of the live lookups but loaded into a
            // separate one. A spreadsheet of old data saying "Brong Ahafo" must be told what
            // replaced it rather than that the name is unknown — but it must never be silently
            // placed, because that region became three and only a person knows which.
            // Resolvable: still exists AND still offered. Retired: has an end date in the past,
            // whatever its IsActive flag says — an area someone deliberately deactivated without
            // end-dating it is simply unknown, which is the honest answer.
            var live = areas.Where(a => a.IsActive && (a.EffectiveTo == null || a.EffectiveTo >= today)).ToList();
            var retired = areas.Where(a => a.EffectiveTo != null && a.EffectiveTo < today).ToList();

            var parentOf = areas.ToDictionary(a => a.Id, a => a.ParentAreaId);
            Guid RegionOf(Guid id)
            {
                var cursor = id;
                var seen = new HashSet<Guid>();
                while (seen.Add(cursor) && parentOf.TryGetValue(cursor, out var parent) && parent.HasValue)
                    cursor = parent.Value;
                return cursor;
            }

            var aliasesByArea = await _db.GeoAreaAliases.AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                .Select(x => new { x.GeoAreaId, x.Alias })
                .ToListAsync(ct);
            var aliasLookup = aliasesByArea
                .GroupBy(x => x.GeoAreaId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Alias).ToArray());

            var nameOf = areas.ToDictionary(a => a.Id, a => a.Name);

            foreach (var a in live)
            {
                var aliases = aliasLookup.GetValueOrDefault(a.Id) ?? Array.Empty<string>();
                var regionId = RegionOf(a.Id);
                var reference = new GeoAreaRef(
                    a.Id, a.Code, a.Name, regionId, nameOf.GetValueOrDefault(regionId, ""), a.TierName);

                if (a.LevelNumber == 1)
                    geoRegions.Add(reference, a.Name, new[] { a.Name, a.Code }.Concat(aliases).ToArray());
                else
                    geoSubAreas.Add(reference, new[] { a.Name, a.Code }.Concat(aliases).ToArray());
            }

            foreach (var a in retired)
            {
                var aliases = aliasLookup.GetValueOrDefault(a.Id) ?? Array.Empty<string>();
                var successor = a.SupersededByGeoAreaId.HasValue
                    ? nameOf.GetValueOrDefault(a.SupersededByGeoAreaId.Value)
                    : null;
                var regionId = RegionOf(a.Id);

                geoRetired.Add(
                    new GeoAreaRef(a.Id, a.Code, a.Name, regionId,
                        nameOf.GetValueOrDefault(regionId, ""), a.TierName, successor),
                    a.Name,
                    new[] { a.Name, a.Code }.Concat(aliases).ToArray());
            }
        }

        var grades = await _db.SalaryGrades.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Select(g => new { g.Id, g.Code }).ToDictionaryAsync(g => g.Id, g => g.Code, ct);

        var levels = new Dictionary<string, SalaryLevelRef>();
        foreach (var l in await _db.SalaryLevels.AsNoTracking()
                     .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.IsActive)
                     .Select(l => new { l.Id, l.Code, l.Name, l.SalaryGradeId }).ToListAsync(ct))
        {
            var level = new SalaryLevelRef(l.Id, l.Code, l.Name, l.SalaryGradeId, grades.GetValueOrDefault(l.SalaryGradeId, ""));
            levels.TryAdd(EmployeeImportColumns.NormalizeCode(l.Code), level);
            levels.TryAdd(EmployeeImportColumns.NormalizeCode(l.Name), level);
        }

        var notchesByLevel = (await _db.SalaryNotches.AsNoTracking()
                .Where(n => n.TenantId == tenantId && !n.IsDeleted && n.IsActive)
                .Select(n => new { n.SalaryLevelId, n.Id, n.NotchNumber, n.SalaryAmount }).ToListAsync(ct))
            .GroupBy(n => n.SalaryLevelId)
            .ToDictionary(g => g.Key, g => g.Select(n => new SalaryNotchRef(n.Id, n.NotchNumber, n.SalaryAmount)).OrderBy(n => n.Number).ToList());

        var qualifications = new LookupTable<QualificationRef>();
        foreach (var q in await _db.Qualifications.AsNoTracking()
                     .Where(q => q.TenantId == tenantId && !q.IsDeleted && q.IsActive)
                     .Select(q => new QualificationRef(q.Id, q.Name, q.ShortCode)).ToListAsync(ct))
            qualifications.Add(q, q.Name, q.Name, q.ShortCode);

        var identificationTypes = await LoadIdentificationTypesAsync(tenantId, ct);

        // Soft-deleted employees included on purpose: the staff-number and email indexes are not
        // filtered on IsDeleted, so a leaver's number and address are still taken.
        var employees = await _db.Employees.AsNoTracking().IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId)
            .Select(e => new { e.Id, e.EmployeeNumber, e.EmailAddress, e.SocialSecurityNumber, e.TINNumber, e.IsDeleted })
            .ToListAsync(ct);
        var numbers = new Dictionary<string, Guid>();
        var emails = new HashSet<string>();
        var ssnit = new HashSet<string>();
        var tin = new HashSet<string>();
        foreach (var e in employees)
        {
            var key = EmployeeImportColumns.NormalizeKey(e.EmployeeNumber);
            if (key.Length > 0)
            {
                // A live row wins over a tombstone with the same number (the index forbids two live ones).
                if (!numbers.TryGetValue(key, out var existing) || existing == Guid.Empty)
                    numbers[key] = e.IsDeleted ? Guid.Empty : e.Id;
            }
            if (!string.IsNullOrWhiteSpace(e.EmailAddress)) emails.Add(e.EmailAddress.Trim().ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(e.SocialSecurityNumber)) ssnit.Add(EmployeeImportColumns.NormalizeKey(e.SocialSecurityNumber));
            if (!string.IsNullOrWhiteSpace(e.TINNumber)) tin.Add(EmployeeImportColumns.NormalizeKey(e.TINNumber));
        }

        var idNumbers = (await _db.EmployeeIdentificationCards.AsNoTracking().IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId && !c.IsDeleted)
                .Select(c => new { c.IdentificationTypeId, c.DocumentNumber }).ToListAsync(ct))
            .GroupBy(c => c.IdentificationTypeId)
            .ToDictionary(g => g.Key, g => g.Select(c => EmployeeImportColumns.NormalizeKey(c.DocumentNumber)).Where(k => k.Length > 0).ToHashSet());

        var rules = new Dictionary<EmploymentType, StaffNumberFormat?>();
        foreach (var type in Enum.GetValues<EmploymentType>())
            rules[type] = await _staffNumbers.GetRuleAsync(type, ct);

        var orgUnitNames = await _db.OrganizationUnits.AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var staffLevelNames = await _db.Set<StaffLevel>().AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted)
            .Select(l => new { l.Id, l.Name }).ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        return new EmployeeImportReferenceData
        {
            TenantId = tenantId,
            Departments = departments,
            Sections = sections,
            Positions = positions,
            Locations = locations,
            GeoRegions = geoRegions,
            GeoSubAreas = geoSubAreas,
            GeoRetiredAreas = geoRetired,
            SalaryLevels = levels,
            NotchesByLevel = notchesByLevel,
            Qualifications = qualifications,
            IdentificationTypes = identificationTypes,
            ExistingEmployeeNumbers = numbers,
            ExistingEmails = emails,
            ExistingSsnitNumbers = ssnit,
            ExistingTinNumbers = tin,
            ExistingIdNumbersByType = idNumbers,
            Rules = rules,
            OrganizationUnitNames = orgUnitNames,
            StaffLevelNames = staffLevelNames,
        };
    }

    /// <summary>
    /// The register's current values for the live employees whose staff numbers are in the file —
    /// the "from" side of every update. Queried in chunks: SQL Server takes at most 2,100 parameters
    /// and a file may hold 10,000 numbers.
    /// </summary>
    private async Task LoadSnapshotsAsync(EmployeeImportReferenceData refs, List<string> staffNumbers, CancellationToken ct)
    {
        var wanted = staffNumbers.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (wanted.Count == 0) return;
        var tenantId = refs.TenantId;

        var employees = new List<Employee>();
        foreach (var chunk in wanted.Chunk(500))
        {
            var numbers = chunk.ToList();
            employees.AddRange(await _db.Employees.AsNoTracking()
                .Where(e => e.TenantId == tenantId && !e.IsDeleted && numbers.Contains(e.EmployeeNumber))
                .ToListAsync(ct));
        }
        if (employees.Count == 0) return;

        var ids = employees.Select(e => e.Id).ToList();

        // Names for the areas these employees already sit in, so an update's diff can say what the
        // address is changing FROM. One query rather than an Include, because the snapshot query
        // above is deliberately a bare entity load.
        var geoAreaIds = employees.Where(e => e.GeoAreaId.HasValue).Select(e => e.GeoAreaId!.Value).Distinct().ToList();
        var geoAreaNames = new Dictionary<Guid, string>();
        foreach (var chunk in geoAreaIds.Chunk(500))
        {
            var set = chunk.ToList();
            foreach (var a in await _db.GeoAreas.AsNoTracking()
                         .Where(a => set.Contains(a.Id)).Select(a => new { a.Id, a.Name }).ToListAsync(ct))
                geoAreaNames[a.Id] = a.Name;
        }

        var managerIds = employees.Where(e => e.ManagerId.HasValue).Select(e => e.ManagerId!.Value).Distinct().ToList();
        var managerNumbers = new Dictionary<Guid, string>();
        foreach (var chunk in managerIds.Chunk(500))
        {
            var set = chunk.ToList();
            foreach (var m in await _db.Employees.AsNoTracking().IgnoreQueryFilters()
                         .Where(e => set.Contains(e.Id)).Select(e => new { e.Id, e.EmployeeNumber }).ToListAsync(ct))
                managerNumbers[m.Id] = m.EmployeeNumber;
        }

        var assignments = new Dictionary<Guid, (Guid? LevelId, Guid? NotchId)>();
        var qualifications = new Dictionary<Guid, HashSet<string>>();
        var idCards = new Dictionary<Guid, Dictionary<Guid, HashSet<string>>>();
        foreach (var chunk in ids.Chunk(500))
        {
            var set = chunk.ToList();
            var current = await _db.EmployeeSalaryAssignments.AsNoTracking()
                .Where(a => set.Contains(a.EmployeeId) && !a.IsDeleted && a.EffectiveTo == null)
                .OrderBy(a => a.EmployeeId).ThenByDescending(a => a.EffectiveDate)
                .Select(a => new { a.EmployeeId, a.LevelId, a.NotchId })
                .ToListAsync(ct);
            foreach (var a in current) assignments.TryAdd(a.EmployeeId, (a.LevelId, a.NotchId));

            var quals = await _db.EmployeeQualifications.AsNoTracking()
                .Where(q => set.Contains(q.EmployeeId) && !q.IsDeleted)
                .Select(q => new { q.EmployeeId, q.CustomQualificationName, MasterName = q.Qualification != null ? q.Qualification.Name : null })
                .ToListAsync(ct);
            foreach (var q in quals)
            {
                var names = qualifications.TryGetValue(q.EmployeeId, out var n) ? n : qualifications[q.EmployeeId] = new HashSet<string>();
                foreach (var name in new[] { q.CustomQualificationName, q.MasterName })
                {
                    var key = EmployeeImportColumns.NormalizeKey(name);
                    if (key.Length > 0) names.Add(key);
                }
            }

            var cards = await _db.EmployeeIdentificationCards.AsNoTracking()
                .Where(c => set.Contains(c.EmployeeId) && !c.IsDeleted)
                .Select(c => new { c.EmployeeId, c.IdentificationTypeId, c.DocumentNumber })
                .ToListAsync(ct);
            foreach (var c in cards)
            {
                var perType = idCards.TryGetValue(c.EmployeeId, out var p) ? p : idCards[c.EmployeeId] = new Dictionary<Guid, HashSet<string>>();
                var numbersOfType = perType.TryGetValue(c.IdentificationTypeId, out var s) ? s : perType[c.IdentificationTypeId] = new HashSet<string>();
                var key = EmployeeImportColumns.NormalizeKey(c.DocumentNumber);
                if (key.Length > 0) numbersOfType.Add(key);
            }
        }

        var levelsById = refs.SalaryLevels.Values.GroupBy(l => l.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var e in employees)
        {
            assignments.TryGetValue(e.Id, out var assignment);
            var level = assignment.LevelId.HasValue ? levelsById.GetValueOrDefault(assignment.LevelId.Value) : null;
            var notch = assignment.NotchId.HasValue && level != null
                ? refs.NotchesByLevel.GetValueOrDefault(level.Id)?.FirstOrDefault(n => n.Id == assignment.NotchId.Value)
                : null;

            var snapshot = new EmployeeSnapshot
            {
                Id = e.Id,
                EmployeeNumber = e.EmployeeNumber,
                Title = e.Title,
                FirstName = e.FirstName,
                MiddleName = e.MiddleName,
                LastName = e.LastName,
                Gender = e.Gender,
                DateOfBirth = e.DateOfBirth,
                MaritalStatus = e.MaritalStatus,
                Religion = e.Religion,
                Hometown = e.Hometown,
                HasDisability = e.HasDisability,
                IsFullTime = e.IsFullTime,
                EmploymentType = e.EmploymentType,
                DateEmployed = e.DateEmployed,
                DepartmentId = e.DepartmentId,
                SectionId = e.SectionId,
                PositionId = e.PositionId,
                OrganizationUnitId = e.OrganizationUnitId,
                LocationId = e.LocationId,
                ManagerId = e.ManagerId,
                ManagerNumber = e.ManagerId.HasValue ? managerNumbers.GetValueOrDefault(e.ManagerId.Value) : null,
                IsOnPayroll = e.IsOnPayroll,
                OffPayrollReason = e.OffPayrollReason,
                Salary = e.Salary,
                Email = e.EmailAddress,
                MobileNumber = e.MobileNumber,
                TelephoneNumber = e.TelephoneNumber,
                SsnitNumber = e.SocialSecurityNumber,
                TinNumber = e.TINNumber,
                DigitalAddress = e.DigitalAddress,
                Address = e.Address,
                City = e.City,
                State = e.State,
                GeoAreaId = e.GeoAreaId,
                GeoAreaName = e.GeoAreaId.HasValue ? geoAreaNames.GetValueOrDefault(e.GeoAreaId.Value) : null,
                Notes = e.Notes,
                CurrentLevelId = assignment.LevelId,
                CurrentNotchId = assignment.NotchId,
                CurrentLevelCode = level?.Code,
                CurrentNotchNumber = notch?.Number,
                QualificationNames = qualifications.GetValueOrDefault(e.Id) ?? new HashSet<string>(),
                IdNumbers = idCards.GetValueOrDefault(e.Id) ?? new Dictionary<Guid, HashSet<string>>(),
            };
            refs.Snapshots[EmployeeImportColumns.NormalizeKey(e.EmployeeNumber)] = snapshot;
        }
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────────────────────

    private async Task<EmployeeImportSession?> FindSessionAsync(Guid sessionId, bool tracking, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var query = tracking ? _db.EmployeeImportSessions.AsQueryable() : _db.EmployeeImportSessions.AsNoTracking();
        return await query.FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == tenantId && !s.IsDeleted, ct);
    }

    private static EmployeeImportSessionSummaryDto Map(EmployeeImportSession s) => new()
    {
        Id = s.Id,
        Reference = s.Reference,
        FileName = s.FileName,
        FileSizeBytes = s.FileSizeBytes,
        TemplateVersion = s.TemplateVersion,
        UploadedByName = s.UploadedByName,
        UploadedOn = s.UploadedOn,
        Status = s.Status,
        Mode = s.Mode,
        CreateCount = s.CreateCount,
        UpdateCount = s.UpdateCount,
        CreatedCount = s.CreatedCount,
        UpdatedCount = s.UpdatedCount,
        CommitPolicy = s.CommitPolicy,
        CommitRequestedOn = s.CommitRequestedOn,
        CommitStartedOn = s.CommitStartedOn,
        CommitCompletedOn = s.CommitCompletedOn,
        TotalRows = s.TotalRows,
        ReadyCount = s.ReadyCount,
        WarningCount = s.WarningCount,
        ErrorCount = s.ErrorCount,
        SkippedCount = s.SkippedCount,
        CommittedCount = s.CommittedCount,
        FailedCount = s.FailedCount,
        FailureMessage = s.FailureMessage,
        FileFindings = ReadFindings(s.FileFindingsJson),
        CounterReconciliation = string.IsNullOrEmpty(s.CounterReconciliationJson)
            ? new List<EmployeeImportCounterReconcileDto>()
            : JsonSerializer.Deserialize<List<EmployeeImportCounterReconcileDto>>(s.CounterReconciliationJson, Json) ?? new(),
        SourceDocumentRecordId = s.SourceDocumentRecordId,
    };

    private static EmployeeImportRowDto Map(EmployeeImportRow r) => new()
    {
        Id = r.Id,
        RowNumber = r.RowNumber,
        StaffNumber = r.StaffNumber,
        DisplayName = r.DisplayName,
        EmploymentType = r.EmploymentType,
        Outcome = r.Outcome,
        Action = r.Action,
        TargetEmployeeId = r.TargetEmployeeId,
        Skip = r.Skip,
        ErrorCount = r.ErrorCount,
        WarningCount = r.WarningCount,
        Findings = ReadFindings(r.FindingsJson),
        Changes = ReadChanges(r.ResolvedJson),
        Values = new Dictionary<string, string?>(ReadValues(r.RawJson)),
        CreatedEmployeeId = r.CreatedEmployeeId,
        CommitMessage = r.CommitMessage,
        CommittedOn = r.CommittedOn,
    };

    private static List<EmployeeImportFindingDto> ReadFindings(string? json)
        => string.IsNullOrEmpty(json) ? new List<EmployeeImportFindingDto>()
            : JsonSerializer.Deserialize<List<EmployeeImportFindingDto>>(json, Json) ?? new List<EmployeeImportFindingDto>();

    private static List<EmployeeImportChangeDto> ReadChanges(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<EmployeeImportChangeDto>();
        try
        {
            return JsonSerializer.Deserialize<EmployeeImportResolvedRow>(json, Json)?.Changes ?? new List<EmployeeImportChangeDto>();
        }
        catch (JsonException)
        {
            return new List<EmployeeImportChangeDto>();
        }
    }

    private static Dictionary<string, string?> ReadValues(string? json)
        => string.IsNullOrEmpty(json) ? new Dictionary<string, string?>()
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(json, Json) ?? new Dictionary<string, string?>();

    private static EmployeeImportFindingDto FileError(string message) => new()
    {
        Severity = EmployeeImportFindingSeverity.Error, Message = message,
    };

    private static EmployeeImportFindingDto FileWarning(string message) => new()
    {
        Severity = EmployeeImportFindingSeverity.Warning, Message = message,
    };

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
