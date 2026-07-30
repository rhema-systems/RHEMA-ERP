using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class ExternalAssociateService : IExternalAssociateService
{
    private readonly IExternalAssociateRepository _repo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork                  _unitOfWork;
    private readonly ILogger<ExternalAssociateService> _logger;

    public ExternalAssociateService(
        IExternalAssociateRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ExternalAssociateService> logger)
    {
        _repo       = repo;
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
        if (entity is null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"External associate '{id}' not found.");
        return entity;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<ExternalAssociateDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<ExternalAssociateDto?> GetByAssociateNumberAsync(
        string associateNumber, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByAssociateNumberAsync(associateNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<ExternalAssociateSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PagedResult<ExternalAssociateSummaryDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repo.GetQueryable().Where(a => a.TenantId == tenantId);

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
        var entities = await _repo.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
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
                     && !a.IsDeleted
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
            .AnyAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.Email == email, ct);
        if (emailExists)
            throw new InvalidOperationException($"An associate with email '{dto.Email}' already exists.");

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
            .AnyAsync(a => a.TenantId == entity.TenantId && !a.IsDeleted && a.Email == email && a.Id != dto.Id, ct);
        if (emailExists)
            throw new InvalidOperationException($"An associate with email '{dto.Email}' already exists.");

        entity.ApplyUpdate(dto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("External associate updated: {Number}", entity.AssociateNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

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

        return entity.ToDto();
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

        return entity.ToDto();
    }

    private async Task<string> GenerateAssociateNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var last = await _repo.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AssociateNumber.StartsWith("EXT-"))
            .OrderByDescending(a => a.AssociateNumber)
            .Select(a => a.AssociateNumber)
            .FirstOrDefaultAsync(ct);

        int next = 1;
        if (last != null && int.TryParse(last.Replace("EXT-", ""), out var parsed))
            next = parsed + 1;

        return $"EXT-{next:D4}";
    }
}
