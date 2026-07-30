using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class ReasonCodeService : IReasonCodeService
{
    private readonly IGenericRepository<ReasonCode> _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReasonCodeService> _logger;

    public ReasonCodeService(
        IGenericRepository<ReasonCode> repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ReasonCodeService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

    private async Task<ReasonCode> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Reason code '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<ReasonCodeDto>> GetAllAsync(ReasonCodeCategory? category = null, bool activeOnly = false)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(r => r.TenantId == tenantId);

        if (category.HasValue)
            query = query.Where(r => r.Category == category.Value);
        if (activeOnly)
            query = query.Where(r => r.IsActive);

        var items = await query
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToListAsync();

        return items.Select(ToDto);
    }

    public async Task<ReasonCodeDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity != null && entity.TenantId == GetTenantId() ? ToDto(entity) : null;
    }

    public async Task<ReasonCodeDto> CreateAsync(CreateReasonCodeDto dto)
    {
        var tenantId = GetTenantId();

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
            var exists = await _repository.GetQueryable()
                .AnyAsync(r => r.TenantId == tenantId && r.Code == dto.Code);
            if (exists)
                throw new InvalidOperationException($"A reason code '{dto.Code}' already exists.");
        }

        var entity = new ReasonCode
        {
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            IsActive = dto.IsActive
        };

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Reason code created: {Code} ({Category})", entity.Code, entity.Category);
        return ToDto(entity);
    }

    public async Task<ReasonCodeDto> UpdateAsync(Guid id, UpdateReasonCodeDto dto)
    {
        var entity = await GetOwnedAsync(id);

        if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code != entity.Code)
        {
            var clash = await _repository.GetQueryable()
                .AnyAsync(r => r.TenantId == entity.TenantId && r.Code == dto.Code && r.Id != id);
            if (clash)
                throw new InvalidOperationException($"A reason code '{dto.Code}' already exists.");
        }

        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.IsActive = dto.IsActive;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var entity = await GetOwnedAsync(id);

        entity.IsActive = false;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private static ReasonCodeDto ToDto(ReasonCode e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Name = e.Name,
        Description = e.Description,
        Category = e.Category,
        IsActive = e.IsActive
    };
}
