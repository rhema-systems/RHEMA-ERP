using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SLICE 17 — PART D ENVIRONMENTAL CORE (X, Y, Z, AA).
//
// SheEnvironmentalPermitService — the FR-ENV-017 Permit Register. The permit
// document rides the controlled-upload gate onto the central DMS exactly like
// the slice-16 register (no SHE-local file store, no bare string paths); the
// renewal ladder and expiry flip live in the reminder engine (FR-ENV-018/019).
//
// SheEnvironmentalGovernanceService — monitoring schedules (FR-ENV-023/024),
// the regulatory updates register (FR-ENV-030–032, built once with FR-SHE-182),
// and sustainability initiatives (FR-ENV-028/029). Management notification
// publishes through the notification-topic pipeline; the topics are seeded by
// the reminder engine's self-heal.
// ============================================================================

#region Environmental Permit Register

public class SheEnvironmentalPermitService : ISheEnvironmentalPermitService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ILogger<SheEnvironmentalPermitService> _logger;

    public SheEnvironmentalPermitService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ILogger<SheEnvironmentalPermitService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _logger = logger;
    }

    /// <summary>Statuses the renewal ladder treats as live authorisation.</summary>
    private static readonly SheEnvironmentalPermitStatus[] LiveStatuses =
    {
        SheEnvironmentalPermitStatus.Active,
        SheEnvironmentalPermitStatus.RenewalInProgress,
        SheEnvironmentalPermitStatus.Suspended,
    };

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

    private async Task<SheEnvironmentalPermit> GetOwnedPermitAsync(Guid id)
    {
        var permit = await _unitOfWork.Repository<SheEnvironmentalPermit>().GetByIdAsync(id);
        if (permit == null || permit.TenantId != GetTenantId() || permit.IsDeleted)
            throw new ArgumentException($"Environmental permit with ID '{id}' not found.");
        return permit;
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

    private async Task GuardOptionalRegulatoryBodyAsync(Guid? bodyId)
    {
        if (bodyId == null) return;
        var body = await _unitOfWork.Repository<SheRegulatoryBody>().GetByIdAsync(bodyId.Value);
        if (body == null || body.TenantId != GetTenantId() || body.IsDeleted)
            throw new ArgumentException($"Regulatory body with ID '{bodyId}' not found.");
    }

    private IQueryable<SheEnvironmentalPermit> FullDetail(Guid tenantId) =>
        _unitOfWork.Repository<SheEnvironmentalPermit>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted)
            .Include(p => p.IssuingBody)
            .Include(p => p.ResponsibleOfficer)
            .Include(p => p.Location)
            .Include(p => p.LastRenewedBy);

    private async Task<SheEnvironmentalPermitDto> ReadDtoAsync(Guid id, CancellationToken ct)
    {
        var entity = await FullDetail(GetTenantId()).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity == null)
            throw new ArgumentException($"Environmental permit with ID '{id}' not found.");

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

    private static SheEnvironmentalPermitDto ToDto(SheEnvironmentalPermit e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        RegisterNumber = e.RegisterNumber,
        PermitName = e.PermitName,
        PermitType = e.PermitType,
        AuthorityReferenceNumber = e.AuthorityReferenceNumber,
        IssuingBodyId = e.IssuingBodyId,
        IssuingBodyName = e.IssuingBody?.Name,
        ResponsibleOfficerId = e.ResponsibleOfficerId,
        ResponsibleOfficerName = e.ResponsibleOfficer?.FullName ?? string.Empty,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        Description = e.Description,
        Conditions = e.Conditions,
        IssueDate = e.IssueDate,
        ExpiryDate = e.ExpiryDate,
        RenewalPeriodMonths = e.RenewalPeriodMonths,
        Status = e.Status,
        DocumentRecordId = e.DocumentRecordId,
        CurrentVersionLabel = e.CurrentVersionLabel,
        LastRenewedDate = e.LastRenewedDate,
        LastRenewedById = e.LastRenewedById,
        LastRenewedByName = e.LastRenewedBy?.FullName,
        Notes = e.Notes,
    };

    private async Task<string> NextNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var prefix = $"EPR-{DateTime.UtcNow.Year}-";
        // Numeric max including soft-deleted rows — the string-ordering re-issue trap.
        var numbers = await _unitOfWork.Repository<SheEnvironmentalPermit>()
            .GetQueryableIncludingDeleted(p => p.TenantId == tenantId && p.RegisterNumber.StartsWith(prefix))
            .Select(p => p.RegisterNumber)
            .ToListAsync(ct);
        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{max + 1:D4}";
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SheEnvironmentalPermitSummaryDto>> GetAllAsync(
        SheEnvironmentalPermitStatus? status = null,
        SheEnvironmentalPermitType? type = null,
        string? search = null,
        int? expiringInDays = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var today = DateTime.UtcNow.Date;
        var query = _unitOfWork.Repository<SheEnvironmentalPermit>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted);

        if (status != null) query = query.Where(p => p.Status == status);
        if (type != null) query = query.Where(p => p.PermitType == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.RegisterNumber.Contains(term) ||
                p.PermitName.Contains(term) ||
                (p.AuthorityReferenceNumber != null && p.AuthorityReferenceNumber.Contains(term)));
        }
        if (expiringInDays is int days)
        {
            var cutoff = today.AddDays(days);
            query = query.Where(p => LiveStatuses.Contains(p.Status) &&
                                     p.ExpiryDate >= today && p.ExpiryDate <= cutoff);
        }

        var rows = await query
            .Include(p => p.IssuingBody)
            .Include(p => p.ResponsibleOfficer)
            .OrderBy(p => p.ExpiryDate)
            .ToListAsync(cancellationToken);

        return rows.Select(p => new SheEnvironmentalPermitSummaryDto
        {
            Id = p.Id,
            RegisterNumber = p.RegisterNumber,
            PermitName = p.PermitName,
            PermitType = p.PermitType,
            AuthorityReferenceNumber = p.AuthorityReferenceNumber,
            IssuingBodyName = p.IssuingBody?.Name,
            ResponsibleOfficerName = p.ResponsibleOfficer?.FullName ?? string.Empty,
            IssueDate = p.IssueDate,
            ExpiryDate = p.ExpiryDate,
            Status = p.Status,
            CurrentVersionLabel = p.CurrentVersionLabel,
            DaysToExpiry = (p.ExpiryDate.Date - today).Days,
        }).ToList();
    }

    public Task<SheEnvironmentalPermitDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ReadDtoAsync(id, cancellationToken);

    public async Task<int> CountExpiredAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var today = DateTime.UtcNow.Date;
        // Status is authoritative once the sweep has flipped it; the date clause
        // keeps the count honest between sweeps.
        return await _unitOfWork.Repository<SheEnvironmentalPermit>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted &&
                               (p.Status == SheEnvironmentalPermitStatus.Expired ||
                                (LiveStatuses.Contains(p.Status) && p.ExpiryDate < today)))
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountExpiringAsync(int daysAhead, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var today = DateTime.UtcNow.Date;
        var cutoff = today.AddDays(daysAhead);
        return await _unitOfWork.Repository<SheEnvironmentalPermit>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted &&
                               LiveStatuses.Contains(p.Status) &&
                               p.ExpiryDate >= today && p.ExpiryDate <= cutoff)
            .CountAsync(cancellationToken);
    }

    // ── register writes ──────────────────────────────────────────────────────

    public async Task<SheEnvironmentalPermitDto> CreateAsync(
        CreateSheEnvironmentalPermitDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        if (dto.ExpiryDate.Date <= dto.IssueDate.Date)
            throw new InvalidOperationException("The permit's expiry date must fall after its issue date.");
        await GetOwnedEmployeeAsync(dto.ResponsibleOfficerId);
        await GuardOptionalRegulatoryBodyAsync(dto.IssuingBodyId);
        await GuardOptionalLocationAsync(dto.LocationId);

        string registerNumber;
        if (!string.IsNullOrWhiteSpace(dto.RegisterNumber))
        {
            registerNumber = dto.RegisterNumber.Trim();
            var taken = await _unitOfWork.Repository<SheEnvironmentalPermit>()
                .GetQueryableIncludingDeleted(p => p.TenantId == tenantId && p.RegisterNumber == registerNumber)
                .AnyAsync(cancellationToken);
            if (taken)
                throw new InvalidOperationException($"Register number '{registerNumber}' is already in use.");
        }
        else
        {
            registerNumber = await NextNumberAsync(tenantId, cancellationToken);
        }

        var entity = new SheEnvironmentalPermit
        {
            TenantId = tenantId,
            RegisterNumber = registerNumber,
            PermitName = dto.PermitName.Trim(),
            PermitType = dto.PermitType,
            AuthorityReferenceNumber = dto.AuthorityReferenceNumber,
            IssuingBodyId = dto.IssuingBodyId,
            ResponsibleOfficerId = dto.ResponsibleOfficerId,
            LocationId = dto.LocationId,
            Description = dto.Description,
            Conditions = dto.Conditions,
            IssueDate = dto.IssueDate.Date,
            ExpiryDate = dto.ExpiryDate.Date,
            RenewalPeriodMonths = dto.RenewalPeriodMonths,
            Status = SheEnvironmentalPermitStatus.Active,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _unitOfWork.Repository<SheEnvironmentalPermit>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Environmental permit registered: {RegisterNumber}", entity.RegisterNumber);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalPermitDto> UpdateAsync(
        UpdateSheEnvironmentalPermitDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(dto.Id);
        if (entity.Status == SheEnvironmentalPermitStatus.Archived)
            throw new InvalidOperationException("An archived permit cannot be edited.");

        await GetOwnedEmployeeAsync(dto.ResponsibleOfficerId);
        await GuardOptionalRegulatoryBodyAsync(dto.IssuingBodyId);
        await GuardOptionalLocationAsync(dto.LocationId);

        entity.PermitName = dto.PermitName.Trim();
        entity.PermitType = dto.PermitType;
        entity.AuthorityReferenceNumber = dto.AuthorityReferenceNumber;
        entity.IssuingBodyId = dto.IssuingBodyId;
        entity.ResponsibleOfficerId = dto.ResponsibleOfficerId;
        entity.LocationId = dto.LocationId;
        entity.Description = dto.Description;
        entity.Conditions = dto.Conditions;
        entity.RenewalPeriodMonths = dto.RenewalPeriodMonths;
        entity.Notes = dto.Notes;
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(id);
        if (entity.DocumentRecordId != null)
            throw new InvalidOperationException(
                "A permit with an uploaded document cannot be deleted — archive it instead.");

        await _unitOfWork.Repository<SheEnvironmentalPermit>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── lifecycle ────────────────────────────────────────────────────────────

    public async Task<SheEnvironmentalPermitDto> MarkRenewalInProgressAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(id);
        if (entity.Status is not (SheEnvironmentalPermitStatus.Active or SheEnvironmentalPermitStatus.Expired))
            throw new InvalidOperationException("Only an active or expired permit can be marked as under renewal.");

        entity.Status = SheEnvironmentalPermitStatus.RenewalInProgress;
        Touch(entity, userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalPermitDto> RenewAsync(
        Guid id, RenewSheEnvironmentalPermitDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(id);
        if (entity.Status == SheEnvironmentalPermitStatus.Archived)
            throw new InvalidOperationException("An archived permit cannot be renewed.");
        if (dto.NewExpiryDate.Date <= dto.NewIssueDate.Date)
            throw new InvalidOperationException("The renewed permit's expiry date must fall after its issue date.");
        if (dto.NewExpiryDate.Date <= DateTime.UtcNow.Date)
            throw new InvalidOperationException("A renewal must extend the permit into the future.");

        var renewer = await GetOwnedEmployeeAsync(userId);

        entity.IssueDate = dto.NewIssueDate.Date;
        entity.ExpiryDate = dto.NewExpiryDate.Date;
        if (!string.IsNullOrWhiteSpace(dto.NewAuthorityReferenceNumber))
            entity.AuthorityReferenceNumber = dto.NewAuthorityReferenceNumber.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        entity.Status = SheEnvironmentalPermitStatus.Active;
        entity.LastRenewedDate = DateTime.UtcNow;
        entity.LastRenewedById = renewer.Id;
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Environmental permit renewed: {RegisterNumber} until {ExpiryDate:yyyy-MM-dd}",
            entity.RegisterNumber, entity.ExpiryDate);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalPermitDto> SuspendAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(id);
        if (entity.Status is not (SheEnvironmentalPermitStatus.Active or SheEnvironmentalPermitStatus.RenewalInProgress))
            throw new InvalidOperationException("Only an active or under-renewal permit can be suspended.");

        entity.Status = SheEnvironmentalPermitStatus.Suspended;
        Touch(entity, userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalPermitDto> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(id);
        if (entity.Status == SheEnvironmentalPermitStatus.Archived)
            throw new InvalidOperationException("The permit is already archived.");

        entity.Status = SheEnvironmentalPermitStatus.Archived;
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

    // ── permit document (controlled-upload gate → central DMS) ───────────────

    public async Task<SheEnvironmentalPermitDto> UploadDocumentAsync(
        SheEnvironmentalPermitDocumentUpload upload, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(upload.PermitId);
        if (entity.Status == SheEnvironmentalPermitStatus.Archived)
            throw new InvalidOperationException("An archived permit cannot receive new documents.");

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
                        SourceLabel = "SHE environmental permit register",
                        SourceEntityType = nameof(SheEnvironmentalPermit),
                        SourceRecordId = entity.Id,
                        SourceRecordReference = entity.RegisterNumber,
                        Title = entity.PermitName,
                        DocumentType = entity.PermitType.ToString(),
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
                        "The permit's central DMS record could not be found; its document history is unavailable.");

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
                    "Failed to remove controlled upload {UploadId} after a permit document write failed.",
                    stored.Record.Id);
            }
            throw;
        }

        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalPermitDocumentFile> GetDocumentFileAsync(
        Guid permitId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(permitId);
        if (entity.DocumentRecordId is not Guid recordId)
            throw new ArgumentException($"Version with ID '{versionId}' not found on this permit.");

        var version = await _unitOfWork.Repository<CentralDocumentVersion>()
            .GetQueryable(v => v.Id == versionId && v.TenantId == entity.TenantId &&
                               v.DocumentRecordId == recordId && !v.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (version == null)
            throw new ArgumentException($"Version with ID '{versionId}' not found on this permit.");

        return new SheEnvironmentalPermitDocumentFile
        {
            DocumentRecordId = recordId,
            VersionId = version.Id,
            FileUploadRecordId = version.FileUploadRecordId,
            FileName = version.FileName ?? entity.RegisterNumber,
            ContentType = version.ContentType,
        };
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
}

#endregion

#region Environmental Governance (schedules, regulatory updates, sustainability)

public class SheEnvironmentalGovernanceService : ISheEnvironmentalGovernanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAppEventBus _appEventBus;
    private readonly ILogger<SheEnvironmentalGovernanceService> _logger;

    public SheEnvironmentalGovernanceService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IAppEventBus appEventBus,
        ILogger<SheEnvironmentalGovernanceService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _appEventBus = appEventBus;
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

    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId() || employee.IsDeleted)
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private async Task GuardOptionalEmployeeAsync(Guid? employeeId)
    {
        if (employeeId != null) await GetOwnedEmployeeAsync(employeeId.Value);
    }

    private async Task GuardOptionalLocationAsync(Guid? locationId)
    {
        if (locationId == null) return;
        var location = await _unitOfWork.Repository<Location>().GetByIdAsync(locationId.Value);
        if (location == null || location.TenantId != GetTenantId() || location.IsDeleted)
            throw new ArgumentException($"Location with ID '{locationId}' not found.");
    }

    private async Task GuardOptionalRegulatoryBodyAsync(Guid? bodyId)
    {
        if (bodyId == null) return;
        var body = await _unitOfWork.Repository<SheRegulatoryBody>().GetByIdAsync(bodyId.Value);
        if (body == null || body.TenantId != GetTenantId() || body.IsDeleted)
            throw new ArgumentException($"Regulatory body with ID '{bodyId}' not found.");
    }

    private async Task GuardOptionalObligationAsync(Guid? obligationId)
    {
        if (obligationId == null) return;
        var obligation = await _unitOfWork.Repository<SheRegulatoryObligation>().GetByIdAsync(obligationId.Value);
        if (obligation == null || obligation.TenantId != GetTenantId() || obligation.IsDeleted)
            throw new ArgumentException($"Regulatory obligation with ID '{obligationId}' not found.");
    }

    // Number generation is inlined per register (numeric max including
    // soft-deleted rows — the string-ordering re-issue trap).

    // ── monitoring schedules (FR-ENV-023/024) ────────────────────────────────

    private async Task<SheEnvironmentalMonitoringSchedule> GetOwnedScheduleAsync(Guid id)
    {
        var schedule = await _unitOfWork.Repository<SheEnvironmentalMonitoringSchedule>().GetByIdAsync(id);
        if (schedule == null || schedule.TenantId != GetTenantId() || schedule.IsDeleted)
            throw new ArgumentException($"Monitoring schedule with ID '{id}' not found.");
        return schedule;
    }

    private async Task<SheEnvironmentalMonitoringScheduleDto> ReadScheduleDtoAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<SheEnvironmentalMonitoringSchedule>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted && s.Id == id)
            .Include(s => s.Location)
            .Include(s => s.ResponsibleOfficer)
            .FirstOrDefaultAsync(ct);
        if (entity == null)
            throw new ArgumentException($"Monitoring schedule with ID '{id}' not found.");
        return ToScheduleDto(entity);
    }

    private static SheEnvironmentalMonitoringScheduleDto ToScheduleDto(SheEnvironmentalMonitoringSchedule e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ScheduleNumber = e.ScheduleNumber,
        MonitoringType = e.MonitoringType,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        MonitoringPoint = e.MonitoringPoint,
        Description = e.Description,
        FrequencyDays = e.FrequencyDays,
        NextDueDate = e.NextDueDate,
        LastPerformedDate = e.LastPerformedDate,
        ResponsibleOfficerId = e.ResponsibleOfficerId,
        ResponsibleOfficerName = e.ResponsibleOfficer?.FullName,
        IsActive = e.IsActive,
        DaysToDue = (e.NextDueDate.Date - DateTime.UtcNow.Date).Days,
    };

    public async Task<IEnumerable<SheEnvironmentalMonitoringScheduleDto>> GetSchedulesAsync(
        bool? activeOnly = null,
        SheEnvironmentalMonitoringType? type = null,
        int? dueInDays = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _unitOfWork.Repository<SheEnvironmentalMonitoringSchedule>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted);

        if (activeOnly == true) query = query.Where(s => s.IsActive);
        if (type != null) query = query.Where(s => s.MonitoringType == type);
        if (dueInDays is int days)
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(days);
            query = query.Where(s => s.IsActive && s.NextDueDate <= cutoff);
        }

        var rows = await query
            .Include(s => s.Location)
            .Include(s => s.ResponsibleOfficer)
            .OrderBy(s => s.NextDueDate)
            .ToListAsync(cancellationToken);
        return rows.Select(ToScheduleDto).ToList();
    }

    public Task<SheEnvironmentalMonitoringScheduleDto> GetScheduleAsync(Guid id, CancellationToken cancellationToken = default)
        => ReadScheduleDtoAsync(id, cancellationToken);

    public async Task<SheEnvironmentalMonitoringScheduleDto> CreateScheduleAsync(
        CreateSheEnvironmentalMonitoringScheduleDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GuardOptionalLocationAsync(dto.LocationId);
        await GuardOptionalEmployeeAsync(dto.ResponsibleOfficerId);

        string scheduleNumber;
        if (!string.IsNullOrWhiteSpace(dto.ScheduleNumber))
        {
            scheduleNumber = dto.ScheduleNumber.Trim();
            var taken = await _unitOfWork.Repository<SheEnvironmentalMonitoringSchedule>()
                .GetQueryableIncludingDeleted(s => s.TenantId == tenantId && s.ScheduleNumber == scheduleNumber)
                .AnyAsync(cancellationToken);
            if (taken)
                throw new InvalidOperationException($"Schedule number '{scheduleNumber}' is already in use.");
        }
        else
        {
            var prefix = $"EMS-{DateTime.UtcNow.Year}-";
            var numbers = await _unitOfWork.Repository<SheEnvironmentalMonitoringSchedule>()
                .GetQueryableIncludingDeleted(s => s.TenantId == tenantId && s.ScheduleNumber.StartsWith(prefix))
                .Select(s => s.ScheduleNumber)
                .ToListAsync(cancellationToken);
            var max = numbers
                .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();
            scheduleNumber = $"{prefix}{max + 1:D4}";
        }

        var entity = new SheEnvironmentalMonitoringSchedule
        {
            TenantId = tenantId,
            ScheduleNumber = scheduleNumber,
            MonitoringType = dto.MonitoringType,
            LocationId = dto.LocationId,
            MonitoringPoint = dto.MonitoringPoint,
            Description = dto.Description,
            FrequencyDays = dto.FrequencyDays,
            NextDueDate = dto.NextDueDate.Date,
            ResponsibleOfficerId = dto.ResponsibleOfficerId,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _unitOfWork.Repository<SheEnvironmentalMonitoringSchedule>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadScheduleDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalMonitoringScheduleDto> UpdateScheduleAsync(
        UpdateSheEnvironmentalMonitoringScheduleDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScheduleAsync(dto.Id);
        await GuardOptionalLocationAsync(dto.LocationId);
        await GuardOptionalEmployeeAsync(dto.ResponsibleOfficerId);

        entity.MonitoringType = dto.MonitoringType;
        entity.LocationId = dto.LocationId;
        entity.MonitoringPoint = dto.MonitoringPoint;
        entity.Description = dto.Description;
        entity.FrequencyDays = dto.FrequencyDays;
        entity.NextDueDate = dto.NextDueDate.Date;
        entity.ResponsibleOfficerId = dto.ResponsibleOfficerId;
        entity.IsActive = dto.IsActive;
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadScheduleDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScheduleAsync(id);
        var hasRecords = await _unitOfWork.Repository<SheEnvironmentalMonitoringRecord>()
            .GetQueryable(r => r.ScheduleId == id && !r.IsDeleted)
            .AnyAsync(cancellationToken);
        if (hasRecords)
            throw new InvalidOperationException(
                "A schedule with linked monitoring records cannot be deleted — deactivate it instead.");

        await _unitOfWork.Repository<SheEnvironmentalMonitoringSchedule>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheEnvironmentalMonitoringScheduleDto> CompleteScheduleCycleAsync(
        Guid id, CompleteSheMonitoringScheduleDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScheduleAsync(id);
        if (!entity.IsActive)
            throw new InvalidOperationException("An inactive schedule cannot record a completed cycle.");

        if (dto.MonitoringRecordId is Guid recordId)
        {
            var record = await _unitOfWork.Repository<SheEnvironmentalMonitoringRecord>().GetByIdAsync(recordId);
            if (record == null || record.TenantId != GetTenantId() || record.IsDeleted)
                throw new ArgumentException($"Environmental monitoring record with ID '{recordId}' not found.");
            record.ScheduleId = entity.Id;
        }

        var performed = dto.PerformedDate.Date;
        entity.LastPerformedDate = performed;
        // Advance from the performed date, not the old due date — a late cycle
        // must not leave the next one already overdue.
        entity.NextDueDate = performed.AddDays(entity.FrequencyDays);
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadScheduleDtoAsync(entity.Id, cancellationToken);
    }

    // ── regulatory updates register (FR-ENV-030–032 / FR-SHE-182) ────────────

    private async Task<SheRegulatoryUpdate> GetOwnedUpdateAsync(Guid id)
    {
        var update = await _unitOfWork.Repository<SheRegulatoryUpdate>().GetByIdAsync(id);
        if (update == null || update.TenantId != GetTenantId() || update.IsDeleted)
            throw new ArgumentException($"Regulatory update with ID '{id}' not found.");
        return update;
    }

    private async Task<SheRegulatoryUpdateDto> ReadUpdateDtoAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<SheRegulatoryUpdate>()
            .GetQueryable(u => u.TenantId == tenantId && !u.IsDeleted && u.Id == id)
            .Include(u => u.RegulatoryBody)
            .Include(u => u.LinkedObligation)
            .Include(u => u.RecordedBy)
            .Include(u => u.ManagementNotifiedBy)
            .Include(u => u.ClosedBy)
            .FirstOrDefaultAsync(ct);
        if (entity == null)
            throw new ArgumentException($"Regulatory update with ID '{id}' not found.");

        return new SheRegulatoryUpdateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            UpdateNumber = entity.UpdateNumber,
            Title = entity.Title,
            RegulationReference = entity.RegulationReference,
            RegulatoryBodyId = entity.RegulatoryBodyId,
            RegulatoryBodyName = entity.RegulatoryBody?.Name,
            AuthorityName = entity.AuthorityName,
            Domain = entity.Domain,
            Summary = entity.Summary,
            IssueDate = entity.IssueDate,
            EffectiveDate = entity.EffectiveDate,
            AffectedDepartments = entity.AffectedDepartments,
            ComplianceDeadline = entity.ComplianceDeadline,
            RiskLevel = entity.RiskLevel,
            RequiredActions = entity.RequiredActions,
            Status = entity.Status,
            ComplianceStatus = entity.ComplianceStatus,
            ReviewDate = entity.ReviewDate,
            OfficerComments = entity.OfficerComments,
            LinkedObligationId = entity.LinkedObligationId,
            LinkedObligationCode = entity.LinkedObligation?.ObligationCode,
            ManagementNotifiedAt = entity.ManagementNotifiedAt,
            ManagementNotifiedById = entity.ManagementNotifiedById,
            ManagementNotifiedByName = entity.ManagementNotifiedBy?.FullName,
            RecordedById = entity.RecordedById,
            RecordedByName = entity.RecordedBy?.FullName ?? string.Empty,
            ClosedAt = entity.ClosedAt,
            ClosedById = entity.ClosedById,
            ClosedByName = entity.ClosedBy?.FullName,
            ClosureNotes = entity.ClosureNotes,
        };
    }

    public async Task<IEnumerable<SheRegulatoryUpdateSummaryDto>> GetRegulatoryUpdatesAsync(
        SheRegulatoryUpdateStatus? status = null,
        SheRegulatoryDomain? domain = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _unitOfWork.Repository<SheRegulatoryUpdate>()
            .GetQueryable(u => u.TenantId == tenantId && !u.IsDeleted);

        if (status != null) query = query.Where(u => u.Status == status);
        if (domain != null) query = query.Where(u => u.Domain == domain);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.UpdateNumber.Contains(term) ||
                u.Title.Contains(term) ||
                (u.RegulationReference != null && u.RegulationReference.Contains(term)));
        }

        return await query
            .Include(u => u.RegulatoryBody)
            .OrderByDescending(u => u.IssueDate)
            .Select(u => new SheRegulatoryUpdateSummaryDto
            {
                Id = u.Id,
                UpdateNumber = u.UpdateNumber,
                Title = u.Title,
                RegulationReference = u.RegulationReference,
                AuthorityName = u.RegulatoryBody != null ? u.RegulatoryBody.Name : u.AuthorityName,
                Domain = u.Domain,
                IssueDate = u.IssueDate,
                ComplianceDeadline = u.ComplianceDeadline,
                RiskLevel = u.RiskLevel,
                Status = u.Status,
                ComplianceStatus = u.ComplianceStatus,
                ManagementNotified = u.ManagementNotifiedAt != null,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<SheRegulatoryUpdateDto> GetRegulatoryUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => ReadUpdateDtoAsync(id, cancellationToken);

    public async Task<SheRegulatoryUpdateDto> CreateRegulatoryUpdateAsync(
        CreateSheRegulatoryUpdateDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var recorder = await GetOwnedEmployeeAsync(userId);
        await GuardOptionalRegulatoryBodyAsync(dto.RegulatoryBodyId);
        await GuardOptionalObligationAsync(dto.LinkedObligationId);

        string updateNumber;
        if (!string.IsNullOrWhiteSpace(dto.UpdateNumber))
        {
            updateNumber = dto.UpdateNumber.Trim();
            var taken = await _unitOfWork.Repository<SheRegulatoryUpdate>()
                .GetQueryableIncludingDeleted(u => u.TenantId == tenantId && u.UpdateNumber == updateNumber)
                .AnyAsync(cancellationToken);
            if (taken)
                throw new InvalidOperationException($"Update number '{updateNumber}' is already in use.");
        }
        else
        {
            var prefix = $"REG-{DateTime.UtcNow.Year}-";
            var numbers = await _unitOfWork.Repository<SheRegulatoryUpdate>()
                .GetQueryableIncludingDeleted(u => u.TenantId == tenantId && u.UpdateNumber.StartsWith(prefix))
                .Select(u => u.UpdateNumber)
                .ToListAsync(cancellationToken);
            var max = numbers
                .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();
            updateNumber = $"{prefix}{max + 1:D4}";
        }

        var entity = new SheRegulatoryUpdate
        {
            TenantId = tenantId,
            UpdateNumber = updateNumber,
            Title = dto.Title.Trim(),
            RegulationReference = dto.RegulationReference,
            RegulatoryBodyId = dto.RegulatoryBodyId,
            AuthorityName = dto.AuthorityName,
            Domain = dto.Domain,
            Summary = dto.Summary,
            IssueDate = dto.IssueDate.Date,
            EffectiveDate = dto.EffectiveDate,
            AffectedDepartments = dto.AffectedDepartments,
            ComplianceDeadline = dto.ComplianceDeadline,
            RiskLevel = dto.RiskLevel,
            RequiredActions = dto.RequiredActions,
            Status = SheRegulatoryUpdateStatus.Recorded,
            ComplianceStatus = SheComplianceStatus.NotAssessed,
            ReviewDate = dto.ReviewDate,
            OfficerComments = dto.OfficerComments,
            LinkedObligationId = dto.LinkedObligationId,
            RecordedById = recorder.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _unitOfWork.Repository<SheRegulatoryUpdate>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Regulatory update recorded: {UpdateNumber}", entity.UpdateNumber);
        return await ReadUpdateDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheRegulatoryUpdateDto> UpdateRegulatoryUpdateAsync(
        UpdateSheRegulatoryUpdateDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUpdateAsync(dto.Id);
        if (entity.Status == SheRegulatoryUpdateStatus.Closed)
            throw new InvalidOperationException("A closed regulatory update cannot be edited.");
        if (dto.Status == SheRegulatoryUpdateStatus.Closed)
            throw new InvalidOperationException("A regulatory update must be closed via the close endpoint, which records who closed it.");

        await GuardOptionalRegulatoryBodyAsync(dto.RegulatoryBodyId);
        await GuardOptionalObligationAsync(dto.LinkedObligationId);

        entity.Title = dto.Title.Trim();
        entity.RegulationReference = dto.RegulationReference;
        entity.RegulatoryBodyId = dto.RegulatoryBodyId;
        entity.AuthorityName = dto.AuthorityName;
        entity.Domain = dto.Domain;
        entity.Summary = dto.Summary;
        entity.IssueDate = dto.IssueDate.Date;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.AffectedDepartments = dto.AffectedDepartments;
        entity.ComplianceDeadline = dto.ComplianceDeadline;
        entity.RiskLevel = dto.RiskLevel;
        entity.RequiredActions = dto.RequiredActions;
        entity.Status = dto.Status;
        entity.ComplianceStatus = dto.ComplianceStatus;
        entity.ReviewDate = dto.ReviewDate;
        entity.OfficerComments = dto.OfficerComments;
        entity.LinkedObligationId = dto.LinkedObligationId;
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadUpdateDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteRegulatoryUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUpdateAsync(id);
        if (entity.Status == SheRegulatoryUpdateStatus.Closed || entity.ManagementNotifiedAt != null)
            throw new InvalidOperationException(
                "A regulatory update that has been closed or communicated to management is history and cannot be deleted.");

        await _unitOfWork.Repository<SheRegulatoryUpdate>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheRegulatoryUpdateDto> NotifyManagementAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUpdateAsync(id);
        if (entity.Status == SheRegulatoryUpdateStatus.Closed)
            throw new InvalidOperationException("A closed regulatory update cannot be re-communicated.");
        if (entity.ManagementNotifiedAt != null)
            throw new InvalidOperationException("Management has already been notified of this regulatory update.");

        var notifier = await GetOwnedEmployeeAsync(userId);
        entity.ManagementNotifiedAt = DateTime.UtcNow;
        entity.ManagementNotifiedById = notifier.Id;
        Touch(entity, userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // FR-ENV-031 — the escalated (management) audience. Topic seeded by the
        // reminder engine's self-heal; a publish before the first sweep is a
        // documented no-op.
        await _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = entity.TenantId,
            EntityType = "SafetyCompliance",
            Activity = "EnvironmentalManagementNotice",
            Audience = "Internal",
            EntityId = entity.Id,
            TriggeredByUserId = null,
            Data = new Dictionary<string, object>
            {
                ["ItemType"] = "Regulatory update",
                ["Reference"] = $"{entity.UpdateNumber} — {entity.Title}",
                ["Detail"] = $"risk {entity.RiskLevel}, compliance deadline {entity.ComplianceDeadline:yyyy-MM-dd}",
                ["ActionPath"] = "/hr/safety/environmental/regulatory-updates",
            },
        }, cancellationToken);

        return await ReadUpdateDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheRegulatoryUpdateDto> CloseRegulatoryUpdateAsync(
        Guid id, CloseSheRegulatoryUpdateDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUpdateAsync(id);
        if (entity.Status == SheRegulatoryUpdateStatus.Closed)
            throw new InvalidOperationException("This regulatory update is already closed.");

        var closer = await GetOwnedEmployeeAsync(userId);
        entity.Status = SheRegulatoryUpdateStatus.Closed;
        entity.ComplianceStatus = dto.ComplianceStatus;
        entity.ClosedAt = DateTime.UtcNow;
        entity.ClosedById = closer.Id;
        entity.ClosureNotes = dto.ClosureNotes;
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadUpdateDtoAsync(entity.Id, cancellationToken);
    }

    // ── sustainability initiatives (FR-ENV-028/029) ──────────────────────────

    private async Task<SheSustainabilityInitiative> GetOwnedInitiativeAsync(Guid id)
    {
        var initiative = await _unitOfWork.Repository<SheSustainabilityInitiative>().GetByIdAsync(id);
        if (initiative == null || initiative.TenantId != GetTenantId() || initiative.IsDeleted)
            throw new ArgumentException($"Sustainability initiative with ID '{id}' not found.");
        return initiative;
    }

    private async Task<SheSustainabilityInitiativeDto> ReadInitiativeDtoAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<SheSustainabilityInitiative>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted && i.Id == id)
            .Include(i => i.Location)
            .Include(i => i.Owner)
            .FirstOrDefaultAsync(ct);
        if (entity == null)
            throw new ArgumentException($"Sustainability initiative with ID '{id}' not found.");
        return ToInitiativeDto(entity);
    }

    private static SheSustainabilityInitiativeDto ToInitiativeDto(SheSustainabilityInitiative e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InitiativeNumber = e.InitiativeNumber,
        Title = e.Title,
        Category = e.Category,
        Description = e.Description,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        OwnerId = e.OwnerId,
        OwnerName = e.Owner?.FullName,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        Status = e.Status,
        TargetValue = e.TargetValue,
        ActualValue = e.ActualValue,
        MeasurementUnit = e.MeasurementUnit,
        EstimatedCostSavings = e.EstimatedCostSavings,
        Notes = e.Notes,
    };

    public async Task<IEnumerable<SheSustainabilityInitiativeDto>> GetInitiativesAsync(
        SheSustainabilityCategory? category = null,
        SheSustainabilityStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _unitOfWork.Repository<SheSustainabilityInitiative>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted);

        if (category != null) query = query.Where(i => i.Category == category);
        if (status != null) query = query.Where(i => i.Status == status);

        var rows = await query
            .Include(i => i.Location)
            .Include(i => i.Owner)
            .OrderByDescending(i => i.StartDate)
            .ToListAsync(cancellationToken);
        return rows.Select(ToInitiativeDto).ToList();
    }

    public Task<SheSustainabilityInitiativeDto> GetInitiativeAsync(Guid id, CancellationToken cancellationToken = default)
        => ReadInitiativeDtoAsync(id, cancellationToken);

    public async Task<SheSustainabilityInitiativeDto> CreateInitiativeAsync(
        CreateSheSustainabilityInitiativeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GuardOptionalLocationAsync(dto.LocationId);
        await GuardOptionalEmployeeAsync(dto.OwnerId);

        string initiativeNumber;
        if (!string.IsNullOrWhiteSpace(dto.InitiativeNumber))
        {
            initiativeNumber = dto.InitiativeNumber.Trim();
            var taken = await _unitOfWork.Repository<SheSustainabilityInitiative>()
                .GetQueryableIncludingDeleted(i => i.TenantId == tenantId && i.InitiativeNumber == initiativeNumber)
                .AnyAsync(cancellationToken);
            if (taken)
                throw new InvalidOperationException($"Initiative number '{initiativeNumber}' is already in use.");
        }
        else
        {
            var prefix = $"SUS-{DateTime.UtcNow.Year}-";
            var numbers = await _unitOfWork.Repository<SheSustainabilityInitiative>()
                .GetQueryableIncludingDeleted(i => i.TenantId == tenantId && i.InitiativeNumber.StartsWith(prefix))
                .Select(i => i.InitiativeNumber)
                .ToListAsync(cancellationToken);
            var max = numbers
                .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();
            initiativeNumber = $"{prefix}{max + 1:D4}";
        }

        var entity = new SheSustainabilityInitiative
        {
            TenantId = tenantId,
            InitiativeNumber = initiativeNumber,
            Title = dto.Title.Trim(),
            Category = dto.Category,
            Description = dto.Description,
            LocationId = dto.LocationId,
            OwnerId = dto.OwnerId,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate,
            Status = dto.Status,
            TargetValue = dto.TargetValue,
            ActualValue = dto.ActualValue,
            MeasurementUnit = dto.MeasurementUnit,
            EstimatedCostSavings = dto.EstimatedCostSavings,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _unitOfWork.Repository<SheSustainabilityInitiative>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadInitiativeDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheSustainabilityInitiativeDto> UpdateInitiativeAsync(
        UpdateSheSustainabilityInitiativeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInitiativeAsync(dto.Id);
        await GuardOptionalLocationAsync(dto.LocationId);
        await GuardOptionalEmployeeAsync(dto.OwnerId);

        entity.Title = dto.Title.Trim();
        entity.Category = dto.Category;
        entity.Description = dto.Description;
        entity.LocationId = dto.LocationId;
        entity.OwnerId = dto.OwnerId;
        entity.StartDate = dto.StartDate.Date;
        entity.EndDate = dto.EndDate;
        entity.Status = dto.Status;
        entity.TargetValue = dto.TargetValue;
        entity.ActualValue = dto.ActualValue;
        entity.MeasurementUnit = dto.MeasurementUnit;
        entity.EstimatedCostSavings = dto.EstimatedCostSavings;
        entity.Notes = dto.Notes;
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadInitiativeDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteInitiativeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInitiativeAsync(id);
        await _unitOfWork.Repository<SheSustainabilityInitiative>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheSustainabilityKpiDto> GetSustainabilityKpisAsync(int? year = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _unitOfWork.Repository<SheSustainabilityInitiative>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted);

        if (year is int y)
        {
            var start = new DateTime(y, 1, 1);
            var end = start.AddYears(1);
            // Active during the year: started before it ended and not finished before it began.
            query = query.Where(i => i.StartDate < end && (i.EndDate == null || i.EndDate >= start));
        }

        var rows = await query.ToListAsync(cancellationToken);

        var categories = rows
            .GroupBy(i => i.Category)
            .OrderBy(g => g.Key)
            .Select(g => new SheSustainabilityCategorySummaryDto
            {
                Category = g.Key,
                Initiatives = g.Count(),
                Completed = g.Count(i => i.Status == SheSustainabilityStatus.Completed),
                TargetTotal = g.Sum(i => i.TargetValue ?? 0m),
                ActualTotal = g.Sum(i => i.ActualValue ?? 0m),
                CostSavings = g.Sum(i => i.EstimatedCostSavings ?? 0m),
            })
            .ToList();

        return new SheSustainabilityKpiDto
        {
            Year = year,
            ActiveInitiatives = rows.Count(i => i.Status == SheSustainabilityStatus.InProgress),
            CompletedInitiatives = rows.Count(i => i.Status == SheSustainabilityStatus.Completed),
            TotalCostSavings = rows.Sum(i => i.EstimatedCostSavings ?? 0m),
            Categories = categories,
        };
    }

    private static void Touch(TenantEntity entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }
}

#endregion
