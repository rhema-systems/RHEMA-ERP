using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class ExternalAssociateService : IExternalAssociateService
{
    private readonly IExternalAssociateRepository _repo;
    private readonly IUnitOfWork                  _unitOfWork;
    private readonly ILogger<ExternalAssociateService> _logger;

    public ExternalAssociateService(
        IExternalAssociateRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<ExternalAssociateService> logger)
    {
        _repo       = repo;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<ExternalAssociateDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null)
            throw new ArgumentException($"External associate '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<ExternalAssociateDto?> GetByAssociateNumberAsync(
        string associateNumber, CancellationToken ct = default)
    {
        var entity = await _repo.GetByAssociateNumberAsync(associateNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<ExternalAssociateSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repo.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<ExternalAssociateSummaryDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _repo.GetQueryable();

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
        var entities = await _repo.GetActiveAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ExternalAssociateSearchResultDto>> SearchAsync(
        string q, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Enumerable.Empty<ExternalAssociateSearchResultDto>();

        if (limit is < 1 or > 100) limit = 20;

        var entities = await _repo.SearchAsync(q.Trim(), limit);
        return entities.ToSearchResultDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<ExternalAssociateDto> CreateAsync(
        CreateExternalAssociateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default)
    {
        if (await _repo.EmailExistsAsync(dto.Email))
            throw new InvalidOperationException($"An associate with email '{dto.Email}' already exists.");

        var number = await _repo.GenerateAssociateNumberAsync();
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
        var entity = await _repo.GetByIdAsync(dto.Id);
        if (entity is null)
            throw new ArgumentException($"External associate '{dto.Id}' not found.");

        if (await _repo.EmailExistsAsync(dto.Email, excludeId: dto.Id))
            throw new InvalidOperationException($"An associate with email '{dto.Email}' already exists.");

        entity.ApplyUpdate(dto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("External associate updated: {Number}", entity.AssociateNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null)
            throw new ArgumentException($"External associate '{id}' not found.");

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("External associate deleted: {Number}", entity.AssociateNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<ExternalAssociateDto> ActivateAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null)
            throw new ArgumentException($"External associate '{id}' not found.");

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
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null)
            throw new ArgumentException($"External associate '{id}' not found.");

        if (!entity.IsActive)
            throw new InvalidOperationException("Associate is already inactive.");

        entity.IsActive  = false;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }
}
