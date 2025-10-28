using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Entities;

namespace ErpSystem.Api.Services.Maintenance;

public class MaintenanceTypeService : IMaintenanceTypeService
{
    private readonly IMaintenanceTypeRepository _maintenanceTypeRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<MaintenanceTypeService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public MaintenanceTypeService(
        IMaintenanceTypeRepository maintenanceTypeRepository,
        IMapper mapper,
        ILogger<MaintenanceTypeService> logger,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _maintenanceTypeRepository = maintenanceTypeRepository;
        _mapper = mapper;
        _logger = logger;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceTypeDto> CreateMaintenanceTypeAsync(CreateMaintenanceTypeDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating new maintenance type: {Name}", createDto.Name);

            // Validate code uniqueness
            if (!await IsMaintenanceTypeCodeUniqueAsync(createDto.Code))
            {
                throw new ArgumentException($"Maintenance type with code '{createDto.Code}' already exists");
            }

            // Validate name uniqueness
            if (!await IsMaintenanceTypeNameUniqueAsync(createDto.Name))
            {
                throw new ArgumentException($"Maintenance type with name '{createDto.Name}' already exists");
            }

            var maintenanceType = _mapper.Map<MaintenanceType>(createDto);
            maintenanceType.TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
            
            var createdMaintenanceType = await _maintenanceTypeRepository.AddAsync(maintenanceType);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Successfully created maintenance type with ID: {Id}", createdMaintenanceType.Id);

            return _mapper.Map<MaintenanceTypeDto>(createdMaintenanceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance type: {Name}", createDto.Name);
            throw;
        }
    }

    public async Task<MaintenanceTypeDto> UpdateMaintenanceTypeAsync(Guid id, UpdateMaintenanceTypeDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating maintenance type: {Id}", id);

            var existingMaintenanceType = await _maintenanceTypeRepository.GetByIdAsync(id);
            if (existingMaintenanceType == null)
            {
                throw new ArgumentException($"Maintenance type with ID {id} not found");
            }

            // Validate code uniqueness (exclude current record)
            if (!await IsMaintenanceTypeCodeUniqueAsync(updateDto.Code, id))
            {
                throw new ArgumentException($"Maintenance type with code '{updateDto.Code}' already exists");
            }

            // Validate name uniqueness (exclude current record)
            if (!await IsMaintenanceTypeNameUniqueAsync(updateDto.Name, id))
            {
                throw new ArgumentException($"Maintenance type with name '{updateDto.Name}' already exists");
            }

            _mapper.Map(updateDto, existingMaintenanceType);
            await _maintenanceTypeRepository.UpdateAsync(existingMaintenanceType);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully updated maintenance type: {Id}", id);

            return _mapper.Map<MaintenanceTypeDto>(existingMaintenanceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance type: {Id}", id);
            throw;
        }
    }

    public async Task DeleteMaintenanceTypeAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting maintenance type: {Id}", id);

            var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(id);
            if (maintenanceType == null)
            {
                throw new ArgumentException($"Maintenance type with ID {id} not found");
            }

            // Check if maintenance type is being used by work orders
            var workOrderCount = await _maintenanceTypeRepository.GetWorkOrderCountByMaintenanceTypeAsync(id);
            if (workOrderCount > 0)
            {
                throw new InvalidOperationException($"Cannot delete maintenance type. It is being used by {workOrderCount} work order(s).");
            }

            await _maintenanceTypeRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted maintenance type: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance type: {Id}", id);
            throw;
        }
    }

    public async Task<MaintenanceTypeDto?> GetMaintenanceTypeByIdAsync(Guid id)
    {
        try
        {
            var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(id);
            return maintenanceType != null ? _mapper.Map<MaintenanceTypeDto>(maintenanceType) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance type: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceTypeDto>> GetAllMaintenanceTypesAsync()
    {
        try
        {
            var currentTenantId = _currentUserService.TenantId;
            var maintenanceTypes = await _maintenanceTypeRepository.GetQueryable()
                .Where(m => !m.IsDeleted && m.TenantId == currentTenantId)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return _mapper.Map<IEnumerable<MaintenanceTypeDto>>(maintenanceTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all maintenance types");
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceTypeDto>> GetActiveMaintenanceTypesAsync()
    {
        try
        {
            var currentTenantId = _currentUserService.TenantId;
            var maintenanceTypes = await _maintenanceTypeRepository.GetQueryable()
                .Where(m => !m.IsDeleted && m.IsActive && m.TenantId == currentTenantId)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return _mapper.Map<IEnumerable<MaintenanceTypeDto>>(maintenanceTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active maintenance types");
            throw;
        }
    }

    public async Task<PagedResult<MaintenanceTypeDto>> GetMaintenanceTypesPagedAsync(MaintenanceTypeFilterDto filter)
    {
        try
        {
            var currentTenantId = _currentUserService.TenantId;
            var query = _maintenanceTypeRepository.GetQueryable().Where(m => !m.IsDeleted && m.TenantId == currentTenantId);

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(m => 
                    m.Name.Contains(filter.SearchTerm) ||
                    m.Code.Contains(filter.SearchTerm) ||
                    (m.Description != null && m.Description.Contains(filter.SearchTerm)) ||
                    m.Category.Contains(filter.SearchTerm));
            }

            if (!string.IsNullOrEmpty(filter.Category))
            {
                query = query.Where(m => m.Category == filter.Category);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(m => m.IsActive == filter.IsActive.Value);
            }

            var totalCount = await query.CountAsync();
            var maintenanceTypes = await query
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Name)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PagedResult<MaintenanceTypeDto>
            {
                Items = _mapper.Map<IEnumerable<MaintenanceTypeDto>>(maintenanceTypes),
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged maintenance types");
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceTypeDto>> GetMaintenanceTypesByCategoryAsync(string category)
    {
        try
        {
            var maintenanceTypes = await _maintenanceTypeRepository.GetByCategoryAsync(category);
            return _mapper.Map<IEnumerable<MaintenanceTypeDto>>(maintenanceTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance types by category: {Category}", category);
            throw;
        }
    }

    public async Task<bool> IsMaintenanceTypeCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        try
        {
            return await _maintenanceTypeRepository.IsCodeUniqueAsync(code, excludeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking maintenance type code uniqueness: {Code}", code);
            throw;
        }
    }

    public async Task<bool> IsMaintenanceTypeNameUniqueAsync(string name, Guid? excludeId = null)
    {
        try
        {
            var query = _maintenanceTypeRepository.GetQueryable()
                .Where(m => m.Name == name && !m.IsDeleted);
            
            if (excludeId.HasValue)
            {
                query = query.Where(m => m.Id != excludeId.Value);
            }

            return !await query.AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking maintenance type name uniqueness: {Name}", name);
            throw;
        }
    }

    public async Task<MaintenanceTypeDto> ToggleMaintenanceTypeStatusAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Toggling maintenance type status: {Id}", id);

            var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(id);
            if (maintenanceType == null)
            {
                throw new ArgumentException($"Maintenance type with ID {id} not found");
            }

            maintenanceType.IsActive = !maintenanceType.IsActive;
            await _maintenanceTypeRepository.UpdateAsync(maintenanceType);

            _logger.LogInformation("Successfully toggled maintenance type status: {Id} to {Status}", id, maintenanceType.IsActive);

            return _mapper.Map<MaintenanceTypeDto>(maintenanceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling maintenance type status: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceTypeDto>> GetMaintenanceTypesForAssetCategoryAsync(Guid assetCategoryId)
    {
        try
        {
            // This would require additional logic to filter by asset category association
            // For now, return active maintenance types
            var maintenanceTypes = await _maintenanceTypeRepository.GetActiveAsync();
            return _mapper.Map<IEnumerable<MaintenanceTypeDto>>(maintenanceTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance types for asset category: {AssetCategoryId}", assetCategoryId);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetMaintenanceTypeCategoriesAsync()
    {
        try
        {
            var categories = await _maintenanceTypeRepository.GetQueryable()
                .Where(m => m.IsActive && !m.IsDeleted)
                .Select(m => m.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance type categories");
            throw;
        }
    }
}