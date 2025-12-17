using AutoMapper;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Maintenance;

public class WorkOrderTypeService : IWorkOrderTypeService
{
    private readonly IWorkOrderTypeRepository _workOrderTypeRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<WorkOrderTypeService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public WorkOrderTypeService(
        IWorkOrderTypeRepository workOrderTypeRepository,
        IMapper mapper,
        ILogger<WorkOrderTypeService> logger,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _workOrderTypeRepository = workOrderTypeRepository;
        _mapper = mapper;
        _logger = logger;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<WorkOrderTypeDto> CreateWorkOrderTypeAsync(CreateWorkOrderTypeDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating new work order type: {Name}", createDto.Name);

            // Validate code uniqueness
            if (!await IsWorkOrderTypeCodeUniqueAsync(createDto.Code))
            {
                throw new ArgumentException($"Work order type with code '{createDto.Code}' already exists");
            }

            // Validate name uniqueness
            var existingByName = await _workOrderTypeRepository.GetQueryable()
                .Where(w => w.Name == createDto.Name && !w.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingByName != null)
            {
                throw new ArgumentException($"Work order type with name '{createDto.Name}' already exists");
            }

            var workOrderType = _mapper.Map<WorkOrderType>(createDto);
            workOrderType.TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");

            var createdWorkOrderType = await _workOrderTypeRepository.AddAsync(workOrderType);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully created work order type with ID: {Id}", createdWorkOrderType.Id);

            return _mapper.Map<WorkOrderTypeDto>(createdWorkOrderType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order type: {Name}", createDto.Name);
            throw;
        }
    }

    public async Task<WorkOrderTypeDto> UpdateWorkOrderTypeAsync(Guid id, UpdateWorkOrderTypeDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating work order type: {Id}", id);

            var existingWorkOrderType = await _workOrderTypeRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order type with ID {id} not found");

            // Validate code uniqueness (exclude current record)
            var existingByCode = await _workOrderTypeRepository.GetQueryable()
                .Where(w => w.Code == updateDto.Code && w.Id != id && !w.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingByCode != null)
            {
                throw new ArgumentException($"Work order type with code '{updateDto.Code}' already exists");
            }

            // Validate name uniqueness
            var existingByName = await _workOrderTypeRepository.GetQueryable()
                .Where(w => w.Name == updateDto.Name && w.Id != id && !w.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingByName != null)
            {
                throw new ArgumentException($"Work order type with name '{updateDto.Name}' already exists");
            }

            _mapper.Map(updateDto, existingWorkOrderType);
            await _workOrderTypeRepository.UpdateAsync(existingWorkOrderType);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully updated work order type: {Id}", id);

            return _mapper.Map<WorkOrderTypeDto>(existingWorkOrderType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order type: {Id}", id);
            throw;
        }
    }

    public async Task DeleteWorkOrderTypeAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting work order type: {Id}", id);

            var workOrderType = await _workOrderTypeRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order type with ID {id} not found");

            // Check if work order type is being used by work orders
            var workOrderCount = await _workOrderTypeRepository.GetWorkOrderCountByTypeAsync(id);
            if (workOrderCount > 0)
            {
                throw new InvalidOperationException($"Cannot delete work order type. It is being used by {workOrderCount} work order(s).");
            }

            await _workOrderTypeRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted work order type: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting work order type: {Id}", id);
            throw;
        }
    }

    public async Task<WorkOrderTypeDto?> GetWorkOrderTypeByIdAsync(Guid id)
    {
        try
        {
            var workOrderType = await _workOrderTypeRepository.GetByIdAsync(id);
            return workOrderType != null ? _mapper.Map<WorkOrderTypeDto>(workOrderType) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order type: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderTypeDto>> GetAllWorkOrderTypesAsync()
    {
        try
        {
            var workOrderTypes = await _workOrderTypeRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<WorkOrderTypeDto>>(workOrderTypes.OrderBy(w => w.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all work order types");
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderTypeDto>> GetActiveWorkOrderTypesAsync()
    {
        try
        {
            var workOrderTypes = await _workOrderTypeRepository.GetActiveAsync();
            return _mapper.Map<IEnumerable<WorkOrderTypeDto>>(workOrderTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active work order types");
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderTypeDto>> GetWorkOrderTypesByCategoryAsync(string category)
    {
        try
        {
            // Since WorkOrderType doesn't have a Category property in the entity, 
            // we'll filter by a related property or return all types
            // This method might need to be adjusted based on the actual requirements
            var workOrderTypes = await _workOrderTypeRepository.GetActiveAsync();
            return _mapper.Map<IEnumerable<WorkOrderTypeDto>>(workOrderTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order types by category: {Category}", category);
            throw;
        }
    }

    public async Task<PagedResult<WorkOrderTypeDto>> GetWorkOrderTypesPagedAsync(WorkOrderTypeFilterDto filter)
    {
        try
        {
            var query = _workOrderTypeRepository.GetQueryable().Where(w => !w.IsDeleted);

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(w =>
                    w.Name.Contains(filter.SearchTerm) ||
                    w.Code.Contains(filter.SearchTerm) ||
                    (w.Description != null && w.Description.Contains(filter.SearchTerm)));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(w => w.IsActive == filter.IsActive.Value);
            }

            if (filter.RequiresApproval.HasValue)
            {
                query = query.Where(w => w.RequiresApproval == filter.RequiresApproval.Value);
            }

            var totalCount = await query.CountAsync();
            var workOrderTypes = await query
                .OrderBy(w => w.Name)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PagedResult<WorkOrderTypeDto>
            {
                Items = _mapper.Map<IEnumerable<WorkOrderTypeDto>>(workOrderTypes),
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged work order types");
            throw;
        }
    }

    public async Task<WorkOrderTypeDto> ToggleWorkOrderTypeStatusAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Toggling work order type status: {Id}", id);

            var workOrderType = await _workOrderTypeRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order type with ID {id} not found");
            workOrderType.IsActive = !workOrderType.IsActive;
            await _workOrderTypeRepository.UpdateAsync(workOrderType);

            _logger.LogInformation("Successfully toggled work order type status: {Id} to {Status}", id, workOrderType.IsActive);

            return _mapper.Map<WorkOrderTypeDto>(workOrderType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling work order type status: {Id}", id);
            throw;
        }
    }

    public async Task<bool> IsWorkOrderTypeCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        try
        {
            return await _workOrderTypeRepository.IsCodeUniqueAsync(code, excludeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking work order type code uniqueness: {Code}", code);
            throw;
        }
    }
}
