using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Pricing;

/// <summary>
/// Supplier Group management service implementation
/// </summary>
public class SupplierGroupService : ISupplierGroupService
{
    private readonly ISupplierGroupRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SupplierGroupService> _logger;

    public SupplierGroupService(
        ISupplierGroupRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<SupplierGroupService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<SupplierGroupDto>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Select(MapToDto);
    }

    public async Task<SupplierGroupDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<SupplierGroupDto?> GetByCodeAsync(string code)
    {
        var entity = await _repository.GetByCodeAsync(code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<SupplierGroupDto> CreateAsync(CreateSupplierGroupDto dto)
    {
        var existing = await _repository.GetByCodeAsync(dto.GroupCode);
        if (existing != null)
            throw new InvalidOperationException($"Supplier group with code '{dto.GroupCode}' already exists.");

        var entity = new SupplierGroup
        {
            GroupCode = dto.GroupCode,
            Name = dto.Name,
            Description = dto.Description,
            DefaultPriceListId = dto.DefaultPriceListId,
            DefaultPaymentTerms = dto.DefaultPaymentTerms,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId
        };

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created supplier group: {Code} - {Name}", entity.GroupCode, entity.Name);
        return MapToDto(entity);
    }

    public async Task<SupplierGroupDto> UpdateAsync(Guid id, UpdateSupplierGroupDto dto)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Supplier group with ID {id} not found.");

        entity.Name = dto.Name ?? entity.Name;
        entity.Description = dto.Description;
        entity.DefaultPriceListId = dto.DefaultPriceListId;
        entity.DefaultPaymentTerms = dto.DefaultPaymentTerms;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<SupplierGroupDto>> GetActiveAsync()
    {
        var entities = await _repository.GetActiveGroupsAsync();
        return entities.Select(MapToDto);
    }

    private static SupplierGroupDto MapToDto(SupplierGroup entity)
    {
        return new SupplierGroupDto
        {
            Id = entity.Id,
            GroupCode = entity.GroupCode,
            Name = entity.Name,
            Description = entity.Description,
            DefaultPriceListId = entity.DefaultPriceListId,
            DefaultPriceListName = entity.DefaultPriceList?.Name,
            DefaultPaymentTerms = entity.DefaultPaymentTerms,
            IsActive = entity.IsActive
        };
    }
}

