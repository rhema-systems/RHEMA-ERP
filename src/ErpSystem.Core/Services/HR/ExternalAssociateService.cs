using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
// JobInterviewExternalPanelist lives in its own Recruitment namespace, not alongside ExternalAssociate.
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The register of external associates — the people who sit on interview panels, assess candidates
/// or advise the organisation without holding an ERP login.
/// </summary>
/// <remarks>
/// <para>Until areas 19-23 slice 8 this service backed twelve endpoints of which exactly one had
/// ever been called: <c>search</c>, by the recruitment panel picker. The register, the paging, the
/// activate/deactivate pair and the lookup-by-number were unreachable from anywhere.</para>
///
/// <para>Three rules here exist because the store soft-deletes:</para>
/// <list type="bullet">
///   <item><b>The number generator reads deleted rows too.</b> It used to take the highest
///   <c>EXT-nnnn</c> through the soft-delete filter, so a removed associate's number became
///   invisible and was minted again — measured on DEFAULT as <b>38 rows carrying EXT-0008</b>, every
///   one of them deleted, while <c>GET number/{n}</c> resolved with a <c>FirstOrDefault</c>. A number
///   is now never reissued, and the filtered unique index behind it makes that claim enforceable.</item>
///   <item><b>An associate on an interview panel cannot be deleted.</b> The FK is
///   <c>OnDelete.Restrict</c>, but the delete is a soft delete, so the constraint never fires — and
///   the panel reads <c>.Include</c> a <i>required</i> navigation, so the soft-delete filter then
///   took the panelist out of the answer altogether. Measured: a panel of two answered one, and the
///   orphaned row could no longer be reached to be tidied up. Deactivate instead; the picker already
///   filters on <c>IsActive</c>, so history keeps its members.</item>
///   <item><b>An email is released by a delete and a number is not</b>, and both are deliberate. An
///   address belongs to a person and may legitimately come back; a reference number is the identity
///   under which a panel decision was recorded.</item>
/// </list>
/// </remarks>
public class ExternalAssociateService : IExternalAssociateService
{
    private const int MaxPageSize = 100;

    private readonly IExternalAssociateRepository _repo;
    private readonly IGenericRepository<JobInterviewExternalPanelist> _panelistRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork                  _unitOfWork;
    private readonly ILogger<ExternalAssociateService> _logger;

    public ExternalAssociateService(
        IExternalAssociateRepository repo,
        IGenericRepository<JobInterviewExternalPanelist> panelistRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ExternalAssociateService> logger)
    {
        _repo       = repo;
        _panelistRepo = panelistRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
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

    private async Task<ExternalAssociate> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null || entity.IsDeleted || entity.TenantId != GetTenantId())
            throw new ArgumentException($"External associate '{id}' not found.");
        return entity;
    }

    public async Task<ExternalAssociate?> GetEntityForPhotoAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id);
        return entity is null || entity.IsDeleted || entity.TenantId != GetTenantId() ? null : entity;
    }

    public async Task<ExternalAssociate> AttachPhotoAsync(
        Guid id, Guid fileUploadRecordId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        entity.PhotoFileUploadRecordId = fileUploadRecordId;

        // ⚠ PicturePath is deliberately NOT cleared. It is the ported location and may still be the
        // only copy of an older image; the download prefers the gated record and falls back to it,
        // so clearing it here would destroy the fallback for anyone who has not re-uploaded.
        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>How many interview panels this associate sits on today.</summary>
    private Task<int> CountPanelSeatsAsync(Guid associateId, Guid tenantId, CancellationToken ct) =>
        _panelistRepo.GetQueryable()
            .Where(p => p.AssociateId == associateId && p.TenantId == tenantId)
            .CountAsync(ct);

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<ExternalAssociateDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        var dto = entity.ToDto();
        // The detail screen has to be able to say WHY a delete will be refused before the user
        // presses it, so the count travels with the record rather than only in the refusal.
        dto.InterviewPanelCount = await CountPanelSeatsAsync(entity.Id, entity.TenantId, ct);
        return dto;
    }

    public async Task<ExternalAssociateDto> GetByAssociateNumberAsync(
        string associateNumber, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var number = (associateNumber ?? string.Empty).Trim();
        var entity = await _repo.GetQueryable()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AssociateNumber == number, ct);

        // ⚠ This used to answer 200 with a null body. "Not found" expressed as a null is a 404 the
        // caller has to discover by inspecting the payload, and every generated client reads it as a
        // successful empty record.
        if (entity is null)
            throw new ArgumentException($"No external associate carries the number '{number}'.");

        var dto = entity.ToDto();
        dto.InterviewPanelCount = await CountPanelSeatsAsync(entity.Id, entity.TenantId, ct);
        return dto;
    }

    public async Task<IEnumerable<ExternalAssociateSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // ⚠ Was `GetAllAsync()` then `.Where(tenant)` in memory — every tenant's associates were
        // read off the wire to serve one tenant's register.
        var entities = await _repo.GetQueryable()
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.LastName)
            .ThenBy(a => a.FirstName)
            .ToListAsync(ct);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<ExternalAssociateSummaryDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? searchTerm = null, bool? isActive = null,
        CancellationToken ct = default)
    {
        // ⚠ Neither bound was checked. `pageNumber=0` and `pageNumber=-1` both reached SQL as a
        // negative OFFSET and came back as a bare 500; `pageSize=0` answered 200 with a page that
        // could never contain a row; `pageSize=100000` was served in full.
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize   < 1) pageSize   = 20;
        if (pageSize   > MaxPageSize) pageSize = MaxPageSize;

        var tenantId = GetTenantId();
        var query = _repo.GetQueryable().Where(a => a.TenantId == tenantId);

        // ⚠ `isActive` is new. The register has an activate/deactivate pair and, before this slice,
        // no way to page the inactive half — a filter that is missing is at least visible; one that
        // is accepted and ignored is indistinguishable from a filter that matched everything.
        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.FirstName.ToLower().Contains(term)
                || a.LastName.ToLower().Contains(term)
                || a.Email.ToLower().Contains(term)
                || (a.CompanyName != null && a.CompanyName.ToLower().Contains(term))
                || a.AssociateNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.LastName)
            .ThenBy(a => a.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ExternalAssociateSummaryDto>
        {
            Items      = items.ToSummaryDtoList().ToList(),
            TotalCount = totalCount,
            Page       = pageNumber,
            PageSize   = pageSize,
        };
    }

    public async Task<IEnumerable<ExternalAssociateSummaryDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .OrderBy(a => a.LastName)
            .ThenBy(a => a.FirstName)
            .ToListAsync(ct);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ExternalAssociateSearchResultDto>> SearchAsync(
        string q, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Enumerable.Empty<ExternalAssociateSearchResultDto>();

        if (limit is < 1 or > 100) limit = 20;

        var tenantId = GetTenantId();
        var term = q.Trim().ToLower();
        var entities = await _repo.GetQueryable()
            .Where(a => a.TenantId == tenantId
                     && a.IsActive
                     && (a.FirstName.ToLower().Contains(term)
                      || a.LastName.ToLower().Contains(term)
                      || (a.FirstName.ToLower() + " " + a.LastName.ToLower()).Contains(term)
                      || a.Email.ToLower().Contains(term)
                      || (a.CompanyName != null && a.CompanyName.ToLower().Contains(term))
                      || a.AssociateNumber.ToLower().Contains(term)))
            .OrderBy(a => a.LastName)
            .ThenBy(a => a.FirstName)
            .Take(limit)
            .ToListAsync(ct);

        return entities.ToSearchResultDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<ExternalAssociateDto> CreateAsync(
        CreateExternalAssociateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var email = dto.Email.Trim().ToLowerInvariant();
        var emailExists = await _repo.GetQueryable()
            .AnyAsync(a => a.TenantId == tenantId && a.Email == email, ct);
        if (emailExists)
            throw new InvalidOperationException($"An associate with email '{dto.Email.Trim()}' already exists.");

        var number = await GenerateAssociateNumberAsync(tenantId, ct);
        var entity = dto.ToEntity(number, tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "External associate created: {Number} — {FirstName} {LastName}",
            entity.AssociateNumber, entity.FirstName, entity.LastName);

        return entity.ToDto();
    }

    public async Task<ExternalAssociateDto> UpdateAsync(
        UpdateExternalAssociateDto dto, Guid updatedByUserId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        var email = dto.Email.Trim().ToLowerInvariant();
        var emailExists = await _repo.GetQueryable()
            .AnyAsync(a => a.TenantId == entity.TenantId && a.Email == email && a.Id != dto.Id, ct);
        if (emailExists)
            throw new InvalidOperationException($"An associate with email '{dto.Email.Trim()}' already exists.");

        entity.ApplyUpdate(dto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("External associate updated: {Number}", entity.AssociateNumber);

        var result = entity.ToDto();
        result.InterviewPanelCount = await CountPanelSeatsAsync(entity.Id, entity.TenantId, ct);
        return result;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        // ⚠ The FK from JobInterviewExternalPanelists is OnDelete.Restrict, and it never fires,
        // because this is a soft delete. The panel read then .Includes a REQUIRED navigation whose
        // principal is filtered out, so the panelist row disappears from the interview instead —
        // together with every scorecard hanging off it — and can no longer be reached to be removed.
        // Measured before this slice: a panel of two answered one, and DELETE on the orphan 404'd.
        var seats = await CountPanelSeatsAsync(entity.Id, entity.TenantId, ct);
        if (seats > 0)
            throw new InvalidOperationException(
                $"{entity.FirstName} {entity.LastName} sits on {seats} interview " +
                $"{(seats == 1 ? "panel" : "panels")} and cannot be deleted. Deactivate them instead — " +
                "they will stop appearing in the panel picker while the interviews keep their record.");

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("External associate deleted: {Number}", entity.AssociateNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<ExternalAssociateDto> ActivateAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.IsActive)
            throw new InvalidOperationException("Associate is already active.");

        entity.IsActive   = true;
        entity.UpdatedAt  = DateTime.UtcNow;
        entity.UpdatedBy  = userId.ToString();

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        var dto = entity.ToDto();
        dto.InterviewPanelCount = await CountPanelSeatsAsync(entity.Id, entity.TenantId, ct);
        return dto;
    }

    public async Task<ExternalAssociateDto> DeactivateAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        if (!entity.IsActive)
            throw new InvalidOperationException("Associate is already inactive.");

        entity.IsActive  = false;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        var dto = entity.ToDto();
        dto.InterviewPanelCount = await CountPanelSeatsAsync(entity.Id, entity.TenantId, ct);
        return dto;
    }

    /// <summary>
    /// Mints the next <c>EXT-nnnn</c>, reading <b>deleted rows as well as live ones</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ This is D-10, the third occurrence in this bundle of one trap: a uniqueness claim over a
    /// soft-deleting store. The old version read through <c>GetQueryable()</c>, which welds
    /// <c>!IsDeleted</c> in, so a deleted associate's number was invisible and came back around.
    /// <c>GetQueryableIncludingDeleted</c> is the call that genuinely reads past a soft delete —
    /// <c>GetQueryable().IgnoreQueryFilters()</c> is not, because the predicate is not a global
    /// filter and <c>IgnoreQueryFilters</c> cannot lift it.
    /// <para>The maximum is computed by parsing rather than by ordering the string: a row that does
    /// not parse used to reset the sequence to 1 via <c>int.TryParse</c> on the single highest row.</para>
    /// </remarks>
    private async Task<string> GenerateAssociateNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var numbers = await _repo
            .GetQueryableIncludingDeleted(a => a.TenantId == tenantId && a.AssociateNumber.StartsWith("EXT-"))
            .Select(a => a.AssociateNumber)
            .ToListAsync(ct);

        var highest = 0;
        foreach (var n in numbers)
            if (int.TryParse(n.AsSpan(4), out var parsed) && parsed > highest)
                highest = parsed;

        return $"EXT-{highest + 1:D4}";
    }
}
