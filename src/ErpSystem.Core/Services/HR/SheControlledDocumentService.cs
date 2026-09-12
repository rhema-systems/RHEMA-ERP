using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SLICE 16 — SHE CONTROLLED DOCUMENT REGISTER (W).
//
// FR-SHE-170 (filing and retrieval) + FR-SHE-246 (version control: revisions,
// approval workflow, effective date, document history). The register row is
// SHE's: classification, custodian, lifecycle, review cycle. The FILES are the
// platform's: every revision goes through the controlled-upload gate (policy,
// quota, malware scan) and lands as a CentralDocumentVersion on one central-DMS
// record per register row — deliberately no SHE-local file or version store,
// and no bare string paths like the area's legacy document columns.
//
// Lifecycle: Draft → Active (activation IS the approval step; refused with no
// uploaded version) → UnderReview → Active again on re-approval; Archived from
// any state. Archived documents refuse edits, new versions and lifecycle moves.
// A register row with history refuses deletion (same stance as executed
// audits) — obsolete documents are archived, not erased.
// ============================================================================

public class SheControlledDocumentService : ISheControlledDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ILogger<SheControlledDocumentService> _logger;

    public SheControlledDocumentService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ILogger<SheControlledDocumentService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Reads scope to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheControlledDocument> GetOwnedDocumentAsync(Guid id)
    {
        var document = await _unitOfWork.Repository<SheControlledDocument>().GetByIdAsync(id);
        if (document == null || document.TenantId != GetTenantId() || document.IsDeleted)
            throw new ArgumentException($"Controlled document with ID '{id}' not found.");
        return document;
    }

    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId() || employee.IsDeleted)
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private async Task GuardOptionalLocationAsync(Guid? locationId)
    {
        if (locationId == null) return;
        var location = await _unitOfWork.Repository<Location>().GetByIdAsync(locationId.Value);
        if (location == null || location.TenantId != GetTenantId() || location.IsDeleted)
            throw new ArgumentException($"Location with ID '{locationId}' not found.");
    }

    private async Task GuardOptionalOrgUnitAsync(Guid? orgUnitId)
    {
        if (orgUnitId == null) return;
        var unit = await _unitOfWork.Repository<OrganizationUnit>().GetByIdAsync(orgUnitId.Value);
        if (unit == null || unit.TenantId != GetTenantId() || unit.IsDeleted)
            throw new ArgumentException($"Organization unit with ID '{orgUnitId}' not found.");
    }

    private IQueryable<SheControlledDocument> FullDetail(Guid tenantId) =>
        _unitOfWork.Repository<SheControlledDocument>()
            .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted)
            .Include(d => d.Owner)
            .Include(d => d.OrganizationUnit)
            .Include(d => d.Location)
            .Include(d => d.ApprovedBy)
            .Include(d => d.ArchivedBy);

    private async Task<SheControlledDocumentDto> ReadDtoAsync(Guid id, CancellationToken ct)
    {
        var entity = await FullDetail(GetTenantId()).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity == null)
            throw new ArgumentException($"Controlled document with ID '{id}' not found.");

        var dto = ToDto(entity);
        if (entity.DocumentRecordId is Guid recordId)
        {
            dto.Versions = await _unitOfWork.Repository<CentralDocumentVersion>()
                .GetQueryable(v => v.TenantId == entity.TenantId &&
                                   v.DocumentRecordId == recordId && !v.IsDeleted)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new SheControlledDocumentVersionDto
                {
                    Id = v.Id,
                    VersionNumber = v.VersionNumber,
                    FileName = v.FileName,
                    ContentType = v.ContentType,
                    FileSize = v.FileSize,
                    ChangeSummary = v.ChangeSummary,
                    UploadedAt = v.CreatedAt,
                    UploadedByName = v.CreatedBy ?? string.Empty,
                })
                .ToListAsync(ct);
        }
        return dto;
    }

    // ── reads (FR-SHE-170 filing & retrieval) ────────────────────────────────

    public async Task<IEnumerable<SheControlledDocumentSummaryDto>> GetAllAsync(
        SheControlledDocumentCategory? category = null,
        SheControlledDocumentStatus? status = null,
        string? search = null,
        int? dueForReviewInDays = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _unitOfWork.Repository<SheControlledDocument>()
            .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted);

        if (category != null) query = query.Where(d => d.Category == category);
        if (status != null) query = query.Where(d => d.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d =>
                d.DocumentNumber.Contains(term) ||
                d.Title.Contains(term) ||
                (d.Keywords != null && d.Keywords.Contains(term)));
        }
        if (dueForReviewInDays is int days)
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(days);
            query = query.Where(d => d.Status == SheControlledDocumentStatus.Active &&
                                     d.NextReviewDate != null && d.NextReviewDate <= cutoff);
        }

        return await query
            .Include(d => d.Owner)
            .Include(d => d.OrganizationUnit)
            .OrderBy(d => d.DocumentNumber)
            .Select(d => new SheControlledDocumentSummaryDto
            {
                Id = d.Id,
                DocumentNumber = d.DocumentNumber,
                Title = d.Title,
                Category = d.Category,
                Status = d.Status,
                OwnerName = d.Owner.FullName,
                OrganizationUnitName = d.OrganizationUnit != null ? d.OrganizationUnit.Name : null,
                CurrentVersionLabel = d.CurrentVersionLabel,
                EffectiveDate = d.EffectiveDate,
                NextReviewDate = d.NextReviewDate,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<SheControlledDocumentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ReadDtoAsync(id, cancellationToken);

    // ── register writes ──────────────────────────────────────────────────────

    public async Task<SheControlledDocumentDto> CreateAsync(
        CreateSheControlledDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedEmployeeAsync(dto.OwnerId);
        await GuardOptionalOrgUnitAsync(dto.OrganizationUnitId);
        await GuardOptionalLocationAsync(dto.LocationId);

        string documentNumber;
        if (!string.IsNullOrWhiteSpace(dto.DocumentNumber))
        {
            documentNumber = dto.DocumentNumber.Trim();
            var taken = await _unitOfWork.Repository<SheControlledDocument>()
                .GetQueryableIncludingDeleted(d => d.TenantId == tenantId && d.DocumentNumber == documentNumber)
                .AnyAsync(cancellationToken);
            if (taken)
                throw new InvalidOperationException($"Document number '{documentNumber}' is already in use.");
        }
        else
        {
            documentNumber = await NextNumberAsync(tenantId, cancellationToken);
        }

        var entity = new SheControlledDocument
        {
            TenantId = tenantId,
            DocumentNumber = documentNumber,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            Category = dto.Category,
            Status = SheControlledDocumentStatus.Draft,
            Keywords = dto.Keywords,
            OwnerId = dto.OwnerId,
            OrganizationUnitId = dto.OrganizationUnitId,
            LocationId = dto.LocationId,
            ReviewFrequencyMonths = dto.ReviewFrequencyMonths,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _unitOfWork.Repository<SheControlledDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheControlledDocumentDto> UpdateAsync(
        UpdateSheControlledDocumentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(dto.Id);
        if (entity.Status == SheControlledDocumentStatus.Archived)
            throw new InvalidOperationException("An archived document cannot be edited.");

        await GetOwnedEmployeeAsync(dto.OwnerId);
        await GuardOptionalOrgUnitAsync(dto.OrganizationUnitId);
        await GuardOptionalLocationAsync(dto.LocationId);

        entity.Title = dto.Title.Trim();
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.Keywords = dto.Keywords;
        entity.OwnerId = dto.OwnerId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.LocationId = dto.LocationId;
        entity.ReviewFrequencyMonths = dto.ReviewFrequencyMonths;
        entity.Notes = dto.Notes;
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(id);
        if (entity.DocumentRecordId != null)
            throw new InvalidOperationException(
                "A controlled document with uploaded versions cannot be deleted — archive it instead.");

        await _unitOfWork.Repository<SheControlledDocument>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── version control (FR-SHE-246) ─────────────────────────────────────────

    public async Task<SheControlledDocumentDto> UploadVersionAsync(
        SheControlledDocumentVersionUpload upload, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(upload.DocumentId);
        if (entity.Status == SheControlledDocumentStatus.Archived)
            throw new InvalidOperationException("An archived document cannot receive new versions.");

        var tenantId = entity.TenantId;
        var stored = await _controlledFiles.UploadAsync(
            new ControlledFileUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = upload.ActorUserId,
                ActorName = upload.ActorName,
                Category = ControlledFileUploadCategories.HrSheControlledDocuments,
                FileName = Path.GetFileName(upload.FileName),
                ContentType = string.IsNullOrWhiteSpace(upload.ContentType)
                    ? "application/octet-stream"
                    : upload.ContentType,
                FileSize = upload.FileSize,
                OpenReadStream = upload.OpenReadStream,
            },
            cancellationToken);

        try
        {
            if (entity.DocumentRecordId is null)
            {
                var link = await _centralDocuments.RegisterAsync(
                    new CentralDocumentRepositoryRegistration
                    {
                        TenantId = tenantId,
                        ActorUserId = upload.ActorUserId,
                        ActorName = upload.ActorName,
                        FileUploadRecordId = stored.Record.Id,
                        SourceModule = "HR",
                        SourceLabel = "SHE controlled document register",
                        SourceEntityType = nameof(SheControlledDocument),
                        SourceRecordId = entity.Id,
                        SourceRecordReference = entity.DocumentNumber,
                        Title = entity.Title,
                        DocumentType = entity.Category.ToString(),
                        AccessProfile = "HR restricted",
                        ChangeSummary = upload.ChangeSummary,
                    },
                    cancellationToken);

                entity.DocumentRecordId = link.DocumentRecordId;
                entity.CurrentVersionLabel = "v1.0";
            }
            else
            {
                var record = await _unitOfWork.Repository<CentralDocumentRecord>()
                    .GetQueryable(r => r.Id == entity.DocumentRecordId && r.TenantId == tenantId && !r.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken);
                if (record == null)
                    throw new InvalidOperationException(
                        "The document's central DMS record could not be found; its version history is unavailable.");

                var now = DateTime.UtcNow;
                var actorName = string.IsNullOrWhiteSpace(upload.ActorName)
                    ? upload.ActorUserId.ToString()
                    : upload.ActorName.Trim();
                var version = new CentralDocumentVersion
                {
                    TenantId = tenantId,
                    DocumentRecordId = record.Id,
                    VersionNumber = NextVersionNumber(record.CurrentVersion),
                    Status = "Submitted",
                    RepositoryPath = stored.Record.FilePath,
                    FileName = stored.Record.OriginalFileName,
                    ContentType = string.IsNullOrWhiteSpace(stored.Record.ContentType)
                        ? "application/octet-stream"
                        : stored.Record.ContentType,
                    FileSize = stored.Record.FileSize,
                    FileUploadRecordId = stored.Record.Id,
                    ChangeSummary = upload.ChangeSummary,
                    CreatedByUserId = upload.ActorUserId,
                    CreatedAt = now,
                    CreatedBy = actorName,
                    CreatedById = upload.ActorUserId,
                };

                record.CurrentVersion = version.VersionNumber;
                record.VersionStatus = version.Status;
                record.RepositoryPath = version.RepositoryPath;
                record.RepositoryStatus = "Linked";
                record.AnnotationStatus = "PDF rendition required";
                record.UpdatedAt = now;
                record.UpdatedBy = actorName;

                await _unitOfWork.Repository<CentralDocumentVersion>().AddAsync(version);
                entity.CurrentVersionLabel = version.VersionNumber;
            }

            Touch(entity, userId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The bytes are already stored; leave no upload behind that nothing references.
            try
            {
                await _controlledFiles.DeleteAsync(tenantId, stored.Record.Id, upload.ActorUserId, cancellationToken);
            }
            catch (Exception cleanup)
            {
                _logger.LogError(cleanup,
                    "Failed to remove controlled upload {UploadId} after a SHE document version write failed.",
                    stored.Record.Id);
            }
            throw;
        }

        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheControlledDocumentVersionFile> GetVersionFileAsync(
        Guid documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);
        if (entity.DocumentRecordId is not Guid recordId)
            throw new ArgumentException($"Version with ID '{versionId}' not found on this document.");

        var version = await _unitOfWork.Repository<CentralDocumentVersion>()
            .GetQueryable(v => v.Id == versionId && v.TenantId == entity.TenantId &&
                               v.DocumentRecordId == recordId && !v.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (version == null)
            throw new ArgumentException($"Version with ID '{versionId}' not found on this document.");

        return new SheControlledDocumentVersionFile
        {
            DocumentRecordId = recordId,
            VersionId = version.Id,
            FileUploadRecordId = version.FileUploadRecordId,
            FileName = version.FileName ?? entity.DocumentNumber,
            ContentType = version.ContentType,
        };
    }

    // ── lifecycle (FR-SHE-246 approval workflow + effective date) ────────────

    public async Task<SheControlledDocumentDto> ActivateAsync(
        ActivateSheControlledDocumentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(dto.DocumentId);
        if (entity.Status == SheControlledDocumentStatus.Archived)
            throw new InvalidOperationException("An archived document cannot be activated.");
        if (entity.Status == SheControlledDocumentStatus.Active)
            throw new InvalidOperationException("The document is already active.");
        if (entity.DocumentRecordId is null)
            throw new InvalidOperationException(
                "The document cannot be approved before a version has been uploaded.");

        var approver = await GetOwnedEmployeeAsync(userId);
        var effective = (dto.EffectiveDate ?? DateTime.UtcNow).Date;

        entity.Status = SheControlledDocumentStatus.Active;
        entity.ApprovedById = approver.Id;
        entity.ApprovedDate = DateTime.UtcNow;
        entity.EffectiveDate = effective;
        entity.NextReviewDate = dto.NextReviewDate
            ?? (entity.ReviewFrequencyMonths is int months ? effective.AddMonths(months) : null);
        Touch(entity, userId);

        // Reflect the approval on the DMS side so the repository view agrees.
        await StampDmsApprovalAsync(entity, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheControlledDocumentDto> StartReviewAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(id);
        if (entity.Status != SheControlledDocumentStatus.Active)
            throw new InvalidOperationException("Only an active document can be sent for review.");

        entity.Status = SheControlledDocumentStatus.UnderReview;
        Touch(entity, userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheControlledDocumentDto> ArchiveAsync(
        ArchiveSheControlledDocumentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(dto.DocumentId);
        if (entity.Status == SheControlledDocumentStatus.Archived)
            throw new InvalidOperationException("The document is already archived.");

        var archiver = await GetOwnedEmployeeAsync(userId);
        entity.Status = SheControlledDocumentStatus.Archived;
        entity.ArchivedById = archiver.Id;
        entity.ArchivedDate = DateTime.UtcNow;
        entity.ArchiveReason = dto.Reason;
        Touch(entity, userId);

        if (entity.DocumentRecordId is Guid recordId)
        {
            var record = await _unitOfWork.Repository<CentralDocumentRecord>()
                .GetQueryable(r => r.Id == recordId && r.TenantId == entity.TenantId && !r.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (record != null)
            {
                record.LifecycleStatus = "Archived";
                record.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task StampDmsApprovalAsync(SheControlledDocument entity, CancellationToken ct)
    {
        if (entity.DocumentRecordId is not Guid recordId) return;

        var record = await _unitOfWork.Repository<CentralDocumentRecord>()
            .GetQueryable(r => r.Id == recordId && r.TenantId == entity.TenantId && !r.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (record == null) return;

        var now = DateTime.UtcNow;
        record.VersionStatus = "Approved";
        record.EffectiveDate = entity.EffectiveDate;
        record.ReviewDate = entity.NextReviewDate;
        record.PublishedAt = now;
        record.UpdatedAt = now;

        var current = await _unitOfWork.Repository<CentralDocumentVersion>()
            .GetQueryable(v => v.TenantId == entity.TenantId && v.DocumentRecordId == recordId &&
                               !v.IsDeleted && v.VersionNumber == record.CurrentVersion)
            .FirstOrDefaultAsync(ct);
        if (current != null)
        {
            current.Status = "Approved";
            current.PublishedAt = now;
        }
    }

    private async Task<string> NextNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var prefix = $"SHE-DOC-{DateTime.UtcNow.Year}-";
        // Numeric max including soft-deleted rows — the string-ordering re-issue trap.
        var numbers = await _unitOfWork.Repository<SheControlledDocument>()
            .GetQueryableIncludingDeleted(d => d.TenantId == tenantId && d.DocumentNumber.StartsWith(prefix))
            .Select(d => d.DocumentNumber)
            .ToListAsync(ct);
        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{max + 1:D4}";
    }

    // Same bump rule as the DMS's own version-upload endpoint (v1.0 → v1.1).
    private static string NextVersionNumber(string? currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
            return "v1.0";

        var normalized = currentVersion.Trim().TrimStart('v', 'V');
        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var major))
            return "v1.0";

        var minor = parts.Length > 1 && int.TryParse(parts[1], out var parsedMinor)
            ? parsedMinor + 1
            : 1;
        return $"v{major}.{minor}";
    }

    private static void Touch(TenantEntity entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    private static SheControlledDocumentDto ToDto(SheControlledDocument e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        DocumentNumber = e.DocumentNumber,
        Title = e.Title,
        Description = e.Description,
        Category = e.Category,
        Status = e.Status,
        Keywords = e.Keywords,
        OwnerId = e.OwnerId,
        OwnerName = e.Owner?.FullName ?? string.Empty,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        DocumentRecordId = e.DocumentRecordId,
        CurrentVersionLabel = e.CurrentVersionLabel,
        EffectiveDate = e.EffectiveDate,
        ReviewFrequencyMonths = e.ReviewFrequencyMonths,
        NextReviewDate = e.NextReviewDate,
        ApprovedById = e.ApprovedById,
        ApprovedByName = e.ApprovedBy?.FullName,
        ApprovedDate = e.ApprovedDate,
        ArchivedById = e.ArchivedById,
        ArchivedByName = e.ArchivedBy?.FullName,
        ArchivedDate = e.ArchivedDate,
        ArchiveReason = e.ArchiveReason,
        Notes = e.Notes,
    };
}
