using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Pricing;

/// <summary>
/// Customer Group management service implementation
/// </summary>
public class CustomerGroupService : ICustomerGroupService
{
    private readonly ICustomerGroupRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<CustomerGroupService> _logger;

    public CustomerGroupService(
        ICustomerGroupRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<CustomerGroupService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<CustomerGroupDto>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Select(MapToDto);
    }

    public async Task<CustomerGroupDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<CustomerGroupDto?> GetByCodeAsync(string code)
    {
        var entity = await _repository.GetByCodeAsync(code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<CustomerGroupDto> CreateAsync(CreateCustomerGroupDto dto)
    {
        var existing = await _repository.GetByCodeAsync(dto.GroupCode);
        if (existing != null)
            throw new InvalidOperationException($"Customer group with code '{dto.GroupCode}' already exists.");

        var entity = new CustomerGroup
        {
            GroupCode = dto.GroupCode,
            Name = dto.Name,
            Description = dto.Description,
            DefaultPriceListId = dto.DefaultPriceListId,
            DefaultDiscountPercent = dto.DefaultDiscountPercent,
            DefaultPaymentTerms = dto.DefaultPaymentTerms,
            DefaultCreditLimit = dto.DefaultCreditLimit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId
        };

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created customer group: {Code} - {Name}", entity.GroupCode, entity.Name);
        return MapToDto(entity);
    }

    public async Task<CustomerGroupDto> UpdateAsync(Guid id, UpdateCustomerGroupDto dto)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Customer group with ID {id} not found.");

        entity.Name = dto.Name ?? entity.Name;
        entity.Description = dto.Description;
        entity.DefaultPriceListId = dto.DefaultPriceListId;
        entity.DefaultDiscountPercent = dto.DefaultDiscountPercent;
        entity.DefaultPaymentTerms = dto.DefaultPaymentTerms;
        entity.DefaultCreditLimit = dto.DefaultCreditLimit;
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

    public async Task<IEnumerable<CustomerGroupDto>> GetActiveAsync()
    {
        var entities = await _repository.GetActiveGroupsAsync();
        return entities.Select(MapToDto);
    }

    private static CustomerGroupDto MapToDto(CustomerGroup entity)
    {
        return new CustomerGroupDto
        {
            Id = entity.Id,
            GroupCode = entity.GroupCode,
            Name = entity.Name,
            Description = entity.Description,
            DefaultPriceListId = entity.DefaultPriceListId,
            DefaultPriceListName = entity.DefaultPriceList?.Name,
            DefaultDiscountPercent = entity.DefaultDiscountPercent,
            DefaultPaymentTerms = entity.DefaultPaymentTerms,
            DefaultCreditLimit = entity.DefaultCreditLimit,
            IsActive = entity.IsActive
        };
    }
}

