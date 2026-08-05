using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Adopts HR files written before the controlled-upload boundary existed into scanned, private
/// storage and the central DMS.
/// </summary>
/// <remarks>
/// <para>Driven from database rows, not from a filesystem walk. Only the owning row knows which
/// tenant a file belongs to and which record the DMS entry should point at — a bare directory
/// listing has neither.</para>
///
/// <para>Nothing here is destructive by default. Originals are removed only on an explicit
/// follow-up run, and a file the upload gate rejects is <b>quarantined, never deleted</b>:
/// destroying evidence attached to a live disciplinary case or a medical examination is not a
/// migration utility's decision to make.</para>
/// </remarks>
public sealed class HrLegacyFileMigrationService
{
    /// <summary>Describes one family of legacy files to sweep.</summary>
    private sealed record Family(
        string EntityType,
        string Category,
        string SourceLabel,
        string SourceEntityType,
        string DocumentType,
        bool RegisterInDms);

    /// <summary>One legacy file awaiting adoption.</summary>
    private sealed record Candidate(
        Family Family,
        Guid TenantId,
        Guid EntityId,
        string LegacyPath,
        string FileName,
        Guid SourceRecordId);

    private const long BufferThresholdBytes = 10 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ILogger<HrLegacyFileMigrationService> _logger;

    public HrLegacyFileMigrationService(
        ApplicationDbContext db,
        IFileStorageService storage,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ILogger<HrLegacyFileMigrationService> logger)
    {
        _db = db;
        _storage = storage;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _logger = logger;
    }

    public async Task<HrLegacyFileMigrationReport> ScanAsync(
        Guid? tenantId, CancellationToken cancellationToken)
    {
        var candidates = await CollectAsync(tenantId, cancellationToken);
        return new HrLegacyFileMigrationReport
        {
            TotalPending = candidates.Count,
            ByEntityType = candidates
                .GroupBy(item => item.Family.EntityType)
                .ToDictionary(group => group.Key, group => group.Count()),
            ByTenant = candidates
                .GroupBy(item => item.TenantId)
                .ToDictionary(group => group.Key, group => group.Count())
        };
    }

    public async Task<HrLegacyFileMigrationRunResult> RunAsync(
        Guid? tenantId,
        bool dryRun,
        bool deleteOriginals,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var candidates = await CollectAsync(tenantId, cancellationToken);
        var result = new HrLegacyFileMigrationRunResult { DryRun = dryRun };

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (dryRun)
            {
                result.WouldProcess++;
                continue;
            }

            try
            {
                var status = await MigrateOneAsync(candidate, deleteOriginals, cancellationToken);
                switch (status)
                {
                    case HrLegacyFileMigrationStatus.Migrated: result.Migrated++; break;
                    case HrLegacyFileMigrationStatus.Missing: result.Missing++; break;
                    case HrLegacyFileMigrationStatus.Quarantined: result.Quarantined++; break;
                    default: result.Failed++; break;
                }
            }
            catch (Exception exception)
            {
                // One bad row must never abort the batch; the ledger keeps it retryable.
                _logger.LogError(exception,
                    "Legacy HR file migration failed for {EntityType} {EntityId}.",
                    candidate.Family.EntityType, candidate.EntityId);
                await RecordAsync(candidate, HrLegacyFileMigrationStatus.Failed,
                    errorCode: "UNEXPECTED", errorMessage: exception.Message,
                    uploadId: null, cancellationToken);
                result.Failed++;
            }
        }

        return result;
    }

    private async Task<HrLegacyFileMigrationStatus> MigrateOneAsync(
        Candidate candidate, bool deleteOriginals, CancellationToken cancellationToken)
    {
        if (!await _storage.FileExistsAsync(candidate.LegacyPath))
        {
            await RecordAsync(candidate, HrLegacyFileMigrationStatus.Missing,
                errorCode: "FILE_NOT_FOUND",
                errorMessage: "The stored path no longer resolves to a file.",
                uploadId: null, cancellationToken);
            return HrLegacyFileMigrationStatus.Missing;
        }

        // The upload gate opens the stream three times — scan, checksum, store — so it needs a
        // factory that yields a FRESH readable stream per call. Handing back one stream three
        // times is the classic adoption bug here and stores silent zero-byte files.
        byte[] buffer;
        await using (var source = await _storage.DownloadFileAsync(candidate.LegacyPath, Guid.Empty))
        await using (var memory = new MemoryStream())
        {
            await source.CopyToAsync(memory, cancellationToken);
            buffer = memory.ToArray();
        }

        if (buffer.Length == 0)
        {
            await RecordAsync(candidate, HrLegacyFileMigrationStatus.Missing,
                errorCode: "FILE_EMPTY", errorMessage: "The stored file is empty.",
                uploadId: null, cancellationToken);
            return HrLegacyFileMigrationStatus.Missing;
        }

        if (buffer.Length > BufferThresholdBytes)
        {
            _logger.LogWarning(
                "Legacy HR file {Path} is {Bytes} bytes and is being buffered in memory.",
                candidate.LegacyPath, buffer.Length);
        }

        ControlledFileUploadResult upload;
        try
        {
            upload = await _controlledFiles.UploadAsync(new ControlledFileUploadRequest
            {
                TenantId = candidate.TenantId,
                ActorUserId = ControlledFileUploadActors.LegacyFileMigration,
                ActorName = "hr-legacy-file-migration",
                Category = candidate.Family.Category,
                FileName = Path.GetFileName(candidate.FileName),
                ContentType = GuessContentType(candidate.FileName),
                FileSize = buffer.Length,
                // Fresh non-writable view per call, over one buffered copy.
                OpenReadStream = () => new MemoryStream(buffer, writable: false)
            }, cancellationToken);
        }
        catch (ControlledFileUploadException exception)
        {
            await QuarantineAsync(candidate, exception, cancellationToken);
            return HrLegacyFileMigrationStatus.Quarantined;
        }

        Guid? documentRecordId = null;
        Guid? documentVersionId = null;
        if (candidate.Family.RegisterInDms)
        {
            var link = await _centralDocuments.RegisterAsync(
                new CentralDocumentRepositoryRegistration
                {
                    TenantId = candidate.TenantId,
                    ActorUserId = ControlledFileUploadActors.LegacyFileMigration,
                    ActorName = "hr-legacy-file-migration",
                    FileUploadRecordId = upload.Record.Id,
                    SourceModule = "HR",
                    SourceLabel = candidate.Family.SourceLabel,
                    SourceEntityType = candidate.Family.SourceEntityType,
                    SourceRecordId = candidate.SourceRecordId,
                    Title = candidate.FileName,
                    DocumentType = candidate.Family.DocumentType,
                    AccessProfile = "HR restricted",
                    ChangeSummary = "Adopted from pre-migration public storage."
                }, cancellationToken);

            documentRecordId = link.DocumentRecordId;
            documentVersionId = link.DocumentVersionId;
        }

        await ApplyLinkAsync(candidate, upload.Record.Id, documentRecordId, documentVersionId,
            cancellationToken);

        if (deleteOriginals)
        {
            try
            {
                await _storage.DeleteFileAsync(candidate.LegacyPath);
            }
            catch (Exception exception)
            {
                // The row already points at the adopted copy, so a stale original is untidy,
                // not harmful — and it is already unreachable via the blocked static paths.
                _logger.LogWarning(exception,
                    "Could not remove the original legacy file {Path}.", candidate.LegacyPath);
            }
        }

        await RecordAsync(candidate, HrLegacyFileMigrationStatus.Migrated,
            errorCode: null, errorMessage: null, upload.Record.Id, cancellationToken);
        return HrLegacyFileMigrationStatus.Migrated;
    }

    /// <summary>
    /// Moves a rejected file out of the public tree and raises an alert, leaving the owning row
    /// untouched so the original reference survives for triage.
    /// </summary>
    private async Task QuarantineAsync(
        Candidate candidate, ControlledFileUploadException exception, CancellationToken cancellationToken)
    {
        var quarantinePath =
            $"quarantine/{candidate.TenantId:N}/{candidate.Family.EntityType}/{candidate.EntityId:N}-{Path.GetFileName(candidate.LegacyPath)}";

        try
        {
            await _storage.MoveFileAsync(candidate.LegacyPath, quarantinePath);
        }
        catch (Exception moveException)
        {
            _logger.LogError(moveException,
                "Could not quarantine rejected legacy file {Path}.", candidate.LegacyPath);
        }

        _db.Set<SecurityAlert>().Add(new SecurityAlert
        {
            TenantId = candidate.TenantId,
            Type = exception.Code == "FILE_VIRUS_DETECTED" ? "critical" : "warning",
            Title = "Legacy HR file rejected during migration",
            Message =
                $"{candidate.Family.EntityType} {candidate.EntityId}: {exception.Code} — {exception.Message}. " +
                $"Moved to {quarantinePath} for review; the original record was left unchanged.",
            Severity = exception.Code == "FILE_VIRUS_DETECTED" ? 9 : 5,
            Category = "data",
            Source = "HrLegacyFileMigrationService",
            Timestamp = DateTime.UtcNow
        });

        await RecordAsync(candidate, HrLegacyFileMigrationStatus.Quarantined,
            exception.Code, exception.Message, uploadId: null, cancellationToken);

        _logger.LogWarning(
            "Quarantined legacy HR file {Path} ({Code}).", candidate.LegacyPath, exception.Code);
    }

    /// <summary>Writes the adopted identifiers back onto the owning domain row.</summary>
    private async Task ApplyLinkAsync(
        Candidate candidate, Guid uploadId, Guid? recordId, Guid? versionId,
        CancellationToken cancellationToken)
    {
        switch (candidate.Family.EntityType)
        {
            case nameof(JobCandidate) + ".Cv":
            {
                var row = await _db.JobCandidates.SingleAsync(x => x.Id == candidate.EntityId, cancellationToken);
                row.CvFileUploadRecordId = uploadId;
                row.CvDocumentRecordId = recordId;
                row.CvDocumentVersionId = versionId;
                break;
            }
            case nameof(JobCandidate) + ".Photo":
            {
                var row = await _db.JobCandidates.SingleAsync(x => x.Id == candidate.EntityId, cancellationToken);
                row.ProfilePhotoFileUploadRecordId = uploadId;
                row.ProfilePhotoUrl = null;
                break;
            }
            case nameof(JobCandidateDocument):
            {
                var row = await _db.JobCandidateDocuments.SingleAsync(x => x.Id == candidate.EntityId, cancellationToken);
                Assign(() => { row.FileUploadRecordId = uploadId; row.DocumentRecordId = recordId; row.DocumentVersionId = versionId; });
                break;
            }
            case nameof(LeaveRequestAttachment):
            {
                var row = await _db.Set<LeaveRequestAttachment>().SingleAsync(x => x.Id == candidate.EntityId, cancellationToken);
                row.FileUploadRecordId = uploadId; row.DocumentRecordId = recordId; row.DocumentVersionId = versionId;
                break;
            }
            case nameof(AppraisalAttachment):
            {
                var row = await _db.Set<AppraisalAttachment>().SingleAsync(x => x.Id == candidate.EntityId, cancellationToken);
                row.FileUploadRecordId = uploadId; row.DocumentRecordId = recordId; row.DocumentVersionId = versionId;
                break;
            }
            case nameof(StaffDisciplineDocument):
            {
                var row = await _db.Set<StaffDisciplineDocument>().SingleAsync(x => x.Id == candidate.EntityId, cancellationToken);
                row.FileUploadRecordId = uploadId; row.DocumentRecordId = recordId; row.DocumentVersionId = versionId;
                break;
            }
            case nameof(StaffMovementAttachment):
            {
                var row = await _db.Set<StaffMovementAttachment>().SingleAsync(x => x.Id == candidate.EntityId, cancellationToken);
                row.FileUploadRecordId = uploadId; row.DocumentRecordId = recordId; row.DocumentVersionId = versionId;
                break;
            }
            case nameof(EmployeeMedicalExamDocument):
            {
                var row = await _db.Set<EmployeeMedicalExamDocument>().SingleAsync(x => x.Id == candidate.EntityId, cancellationToken);
                row.FileUploadRecordId = uploadId; row.DocumentRecordId = recordId; row.DocumentVersionId = versionId;
                break;
            }
            default:
                throw new InvalidOperationException(
                    $"No link handler for entity type '{candidate.Family.EntityType}'.");
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void Assign(Action assign) => assign();

    private async Task RecordAsync(
        Candidate candidate,
        HrLegacyFileMigrationStatus status,
        string? errorCode,
        string? errorMessage,
        Guid? uploadId,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Set<HrLegacyFileMigrationEntry>()
            .SingleOrDefaultAsync(item =>
                    item.TenantId == candidate.TenantId &&
                    item.EntityType == candidate.Family.EntityType &&
                    item.EntityId == candidate.EntityId,
                cancellationToken);

        if (existing is null)
        {
            existing = new HrLegacyFileMigrationEntry
            {
                // Explicit: this runs from an admin request whose tenant may differ from the
                // row being migrated when a SuperAdmin sweeps every tenant.
                TenantId = candidate.TenantId,
                EntityType = candidate.Family.EntityType,
                EntityId = candidate.EntityId,
                CreatedBy = "hr-legacy-file-migration"
            };
            _db.Set<HrLegacyFileMigrationEntry>().Add(existing);
        }

        existing.LegacyPath = Truncate(candidate.LegacyPath, 1000);
        existing.Status = status;
        existing.ErrorCode = Truncate(errorCode, 100);
        existing.ErrorMessage = Truncate(errorMessage, 2000);
        existing.FileUploadRecordId = uploadId;
        existing.ProcessedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Collects every row still holding a legacy path and not yet adopted, skipping anything the
    /// ledger already resolved.
    /// </summary>
    private async Task<List<Candidate>> CollectAsync(
        Guid? tenantId, CancellationToken cancellationToken)
    {
        var done = await _db.Set<HrLegacyFileMigrationEntry>()
            .AsNoTracking()
            .Where(item => item.Status == HrLegacyFileMigrationStatus.Migrated ||
                           item.Status == HrLegacyFileMigrationStatus.Quarantined ||
                           item.Status == HrLegacyFileMigrationStatus.Missing)
            .Select(item => new { item.EntityType, item.EntityId })
            .ToListAsync(cancellationToken);
        var resolved = done
            .Select(item => (item.EntityType, item.EntityId))
            .ToHashSet();

        var results = new List<Candidate>();

        var cvFamily = new Family(nameof(JobCandidate) + ".Cv",
            ControlledFileUploadCategories.HrCandidateCv,
            "Recruitment candidate CV", nameof(JobCandidate), "CV", true);
        var photoFamily = new Family(nameof(JobCandidate) + ".Photo",
            ControlledFileUploadCategories.HrCandidatePhotos,
            "Candidate profile photo", nameof(JobCandidate), "ProfilePhoto", false);

        var candidates = await _db.JobCandidates.AsNoTracking()
            .Where(item => !item.IsDeleted &&
                           (tenantId == null || item.TenantId == tenantId) &&
                           ((item.CvFileUploadRecordId == null && item.CvFilePath != null && item.CvFilePath != "") ||
                            (item.ProfilePhotoFileUploadRecordId == null && item.ProfilePhotoUrl != null && item.ProfilePhotoUrl != "")))
            .Select(item => new
            {
                item.Id, item.TenantId, item.CvFilePath, item.ProfilePhotoUrl,
                item.CvFileUploadRecordId, item.ProfilePhotoFileUploadRecordId
            })
            .ToListAsync(cancellationToken);

        foreach (var row in candidates)
        {
            if (row.CvFileUploadRecordId is null && Normalize(row.CvFilePath) is string cv &&
                !resolved.Contains((cvFamily.EntityType, row.Id)))
            {
                results.Add(new Candidate(cvFamily, row.TenantId, row.Id, cv, Path.GetFileName(cv), row.Id));
            }

            if (row.ProfilePhotoFileUploadRecordId is null && Normalize(row.ProfilePhotoUrl) is string photo &&
                !resolved.Contains((photoFamily.EntityType, row.Id)))
            {
                results.Add(new Candidate(photoFamily, row.TenantId, row.Id, photo, Path.GetFileName(photo), row.Id));
            }
        }

        await AddAsync(results, resolved, tenantId, cancellationToken,
            new Family(nameof(JobCandidateDocument),
                ControlledFileUploadCategories.HrCandidateDocuments,
                "Candidate document", nameof(JobCandidate), "CandidateDocument", true),
            _db.JobCandidateDocuments.AsNoTracking()
                .Select(item => new LegacyRow(item.Id, item.TenantId, item.FilePath,
                    item.FileName, item.JobCandidateId, item.FileUploadRecordId, item.IsDeleted)));

        await AddAsync(results, resolved, tenantId, cancellationToken,
            new Family(nameof(LeaveRequestAttachment),
                ControlledFileUploadCategories.HrLeaveAttachments,
                "Leave request attachment", "LeaveRequest", "LeaveAttachment", true),
            _db.Set<LeaveRequestAttachment>().AsNoTracking()
                .Select(item => new LegacyRow(item.Id, item.TenantId, item.FilePath,
                    item.FileName, item.LeaveRequestId, item.FileUploadRecordId, item.IsDeleted)));

        await AddAsync(results, resolved, tenantId, cancellationToken,
            new Family(nameof(AppraisalAttachment),
                ControlledFileUploadCategories.HrPipAttachments,
                "Performance improvement plan attachment",
                "PerformanceImprovementPlan", "PipAttachment", true),
            _db.Set<AppraisalAttachment>().AsNoTracking()
                .Where(item => item.PipId != null)
                .Select(item => new LegacyRow(item.Id, item.TenantId, item.FilePath,
                    item.FileName, item.PipId!.Value, item.FileUploadRecordId, item.IsDeleted)));

        await AddAsync(results, resolved, tenantId, cancellationToken,
            new Family(nameof(StaffDisciplineDocument),
                ControlledFileUploadCategories.HrDisciplineDocuments,
                "Disciplinary case document", "StaffDisciplinaryAction", "Evidence", true),
            _db.Set<StaffDisciplineDocument>().AsNoTracking()
                .Select(item => new LegacyRow(item.Id, item.TenantId, item.FilePath,
                    item.FileName, item.DisciplinaryActionId, item.FileUploadRecordId, item.IsDeleted)));

        await AddAsync(results, resolved, tenantId, cancellationToken,
            new Family(nameof(StaffMovementAttachment),
                ControlledFileUploadCategories.HrStaffMovementAttachments,
                "Staff movement attachment", "StaffMovement", "MovementAttachment", true),
            _db.Set<StaffMovementAttachment>().AsNoTracking()
                .Select(item => new LegacyRow(item.Id, item.TenantId, item.FilePath,
                    item.FileName, item.MovementId, item.FileUploadRecordId, item.IsDeleted)));

        await AddAsync(results, resolved, tenantId, cancellationToken,
            new Family(nameof(EmployeeMedicalExamDocument),
                ControlledFileUploadCategories.HrMedicalExamDocuments,
                "Employee medical exam document", "EmployeeMedicalExam",
                "MedicalExamDocument", true),
            _db.Set<EmployeeMedicalExamDocument>().AsNoTracking()
                .Select(item => new LegacyRow(item.Id, item.TenantId, item.FilePath,
                    item.FileName, item.ExamId, item.FileUploadRecordId, item.IsDeleted)));

        return results;
    }

    private sealed record LegacyRow(
        Guid Id, Guid TenantId, string? FilePath, string FileName,
        Guid SourceRecordId, Guid? FileUploadRecordId, bool IsDeleted);

    private static async Task AddAsync(
        List<Candidate> results,
        HashSet<(string, Guid)> resolved,
        Guid? tenantId,
        CancellationToken cancellationToken,
        Family family,
        IQueryable<LegacyRow> query)
    {
        var rows = await query
            .Where(item => !item.IsDeleted &&
                           item.FileUploadRecordId == null &&
                           item.FilePath != null && item.FilePath != "" &&
                           (tenantId == null || item.TenantId == tenantId))
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (resolved.Contains((family.EntityType, row.Id)))
                continue;
            if (Normalize(row.FilePath) is not string path)
                continue;

            results.Add(new Candidate(
                family, row.TenantId, row.Id, path,
                string.IsNullOrWhiteSpace(row.FileName) ? Path.GetFileName(path) : row.FileName,
                row.SourceRecordId));
        }
    }

    /// <summary>
    /// Reduces a stored value to a storage-relative path, or null when it is not one we wrote.
    /// </summary>
    /// <remarks>
    /// Legacy values are a mix of relative paths and public URLs (profile photos stored the URL).
    /// Anything absolute, rooted or containing traversal segments is refused rather than guessed
    /// at — this utility reads whatever the string names, so it must not be steerable.
    /// </remarks>
    private static string? Normalize(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        var value = stored.Trim().Replace('\\', '/');

        // Strip a leading public base URL such as "/uploads/".
        const string uploadsPrefix = "/uploads/";
        if (value.StartsWith(uploadsPrefix, StringComparison.OrdinalIgnoreCase))
            value = value[uploadsPrefix.Length..];

        value = value.TrimStart('/');

        if (value.Length == 0 ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("://", StringComparison.Ordinal) ||
            Path.IsPathRooted(value))
            return null;

        return value;
    }

    private static string GuessContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".rtf" => "application/rtf",
            _ => "application/octet-stream"
        };

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= max ? value : value[..max];
}

public sealed class HrLegacyFileMigrationReport
{
    public int TotalPending { get; init; }
    public Dictionary<string, int> ByEntityType { get; init; } = new();
    public Dictionary<Guid, int> ByTenant { get; init; } = new();
}

public sealed class HrLegacyFileMigrationRunResult
{
    public bool DryRun { get; init; }
    public int WouldProcess { get; set; }
    public int Migrated { get; set; }
    public int Missing { get; set; }
    public int Quarantined { get; set; }
    public int Failed { get; set; }
}
