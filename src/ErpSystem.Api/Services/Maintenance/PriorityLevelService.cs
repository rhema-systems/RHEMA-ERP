using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Entities;

namespace ErpSystem.Api.Services.Maintenance;

public class PriorityLevelService : IPriorityLevelService
{
    private readonly IPriorityLevelRepository _priorityLevelRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<PriorityLevelService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public PriorityLevelService(
        IPriorityLevelRepository priorityLevelRepository,
        IMapper mapper,
        ILogger<PriorityLevelService> logger,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _priorityLevelRepository = priorityLevelRepository;
        _mapper = mapper;
        _logger = logger;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PriorityLevelDto> CreatePriorityLevelAsync(CreatePriorityLevelDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating new priority level: {Name}", createDto.Name);

            // Validate name uniqueness
            var existingByName = await _priorityLevelRepository.GetQueryable()
                .Where(p => p.Name == createDto.Name && !p.IsDeleted)
                .FirstOrDefaultAsync();
            
            if (existingByName != null)
            {
                throw new ArgumentException($"Priority level with name '{createDto.Name}' already exists");
            }

            // Validate level uniqueness
            if (!await _priorityLevelRepository.IsLevelUniqueAsync(createDto.Level))
            {
                throw new ArgumentException($"Priority level {createDto.Level} already exists");
            }

            var priorityLevel = _mapper.Map<PriorityLevel>(createDto);
            // For now, use a default tenant ID since authentication is not fully set up
            // TODO: Restore proper tenant assignment when authentication is fixed
            priorityLevel.TenantId = _currentUserService.TenantId ?? Guid.NewGuid();
            priorityLevel.ResponseTimeHours = createDto.ResponseTime / 60; // Convert minutes to hours
            
            var createdPriorityLevel = await _priorityLevelRepository.AddAsync(priorityLevel);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Successfully created priority level with ID: {Id}", createdPriorityLevel.Id);

            return _mapper.Map<PriorityLevelDto>(createdPriorityLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating priority level: {Name}", createDto.Name);
            throw;
        }
    }

    public async Task<PriorityLevelDto> UpdatePriorityLevelAsync(Guid id, UpdatePriorityLevelDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating priority level: {Id}", id);

            var existingPriorityLevel = await _priorityLevelRepository.GetByIdAsync(id);
            if (existingPriorityLevel == null)
            {
                throw new ArgumentException($"Priority level with ID {id} not found");
            }

            // Validate name uniqueness (exclude current record)
            var existingByName = await _priorityLevelRepository.GetQueryable()
                .Where(p => p.Name == updateDto.Name && p.Id != id && !p.IsDeleted)
                .FirstOrDefaultAsync();
            
            if (existingByName != null)
            {
                throw new ArgumentException($"Priority level with name '{updateDto.Name}' already exists");
            }

            // Validate level uniqueness
            if (!await _priorityLevelRepository.IsLevelUniqueAsync(updateDto.Level, id))
            {
                throw new ArgumentException($"Priority level {updateDto.Level} already exists");
            }

            _mapper.Map(updateDto, existingPriorityLevel);
            existingPriorityLevel.ResponseTimeHours = updateDto.ResponseTime / 60; // Convert minutes to hours
            
            await _priorityLevelRepository.UpdateAsync(existingPriorityLevel);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully updated priority level: {Id}", id);

            return _mapper.Map<PriorityLevelDto>(existingPriorityLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating priority level: {Id}", id);
            throw;
        }
    }

    public async Task DeletePriorityLevelAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting priority level: {Id}", id);

            var priorityLevel = await _priorityLevelRepository.GetByIdAsync(id);
            if (priorityLevel == null)
            {
                throw new ArgumentException($"Priority level with ID {id} not found");
            }

            // Check if priority level is being used by work orders
            var workOrderCount = await _priorityLevelRepository.GetWorkOrderCountByPriorityAsync(id);
            if (workOrderCount > 0)
            {
                throw new InvalidOperationException($"Cannot delete priority level. It is being used by {workOrderCount} work order(s).");
            }

            await _priorityLevelRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted priority level: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting priority level: {Id}", id);
            throw;
        }
    }

    public async Task<PriorityLevelDto?> GetPriorityLevelByIdAsync(Guid id)
    {
        try
        {
            var priorityLevel = await _priorityLevelRepository.GetByIdAsync(id);
            return priorityLevel != null ? _mapper.Map<PriorityLevelDto>(priorityLevel) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving priority level: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<PriorityLevelDto>> GetAllPriorityLevelsAsync()
    {
        try
        {
            var priorityLevels = await _priorityLevelRepository.GetOrderedByLevelAsync();
            return _mapper.Map<IEnumerable<PriorityLevelDto>>(priorityLevels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all priority levels");
            throw;
        }
    }

    public async Task<IEnumerable<PriorityLevelDto>> GetActivePriorityLevelsAsync()
    {
        try
        {
            var priorityLevels = await _priorityLevelRepository.GetActiveAsync();
            return _mapper.Map<IEnumerable<PriorityLevelDto>>(priorityLevels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active priority levels");
            throw;
        }
    }

    public async Task<PagedResult<PriorityLevelDto>> GetPriorityLevelsPagedAsync(PriorityLevelFilterDto filter)
    {
        try
        {
            var query = _priorityLevelRepository.GetQueryable().Where(p => !p.IsDeleted);

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(p => 
                    p.Name.Contains(filter.SearchTerm) ||
                    (p.Description != null && p.Description.Contains(filter.SearchTerm)));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(p => p.IsActive == filter.IsActive.Value);
            }

            if (filter.Level.HasValue)
            {
                query = query.Where(p => p.Level == filter.Level.Value);
            }

            var totalCount = await query.CountAsync();
            var priorityLevels = await query
                .OrderBy(p => p.Level)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PagedResult<PriorityLevelDto>
            {
                Items = _mapper.Map<IEnumerable<PriorityLevelDto>>(priorityLevels),
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged priority levels");
            throw;
        }
    }

    public async Task<PriorityLevelDto> TogglePriorityLevelStatusAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Toggling priority level status: {Id}", id);

            var priorityLevel = await _priorityLevelRepository.GetByIdAsync(id);
            if (priorityLevel == null)
            {
                throw new ArgumentException($"Priority level with ID {id} not found");
            }

            priorityLevel.IsActive = !priorityLevel.IsActive;
            await _priorityLevelRepository.UpdateAsync(priorityLevel);

            _logger.LogInformation("Successfully toggled priority level status: {Id} to {Status}", id, priorityLevel.IsActive);

            return _mapper.Map<PriorityLevelDto>(priorityLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling priority level status: {Id}", id);
            throw;
        }
    }

    public async Task<bool> IsPriorityLevelCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        try
        {
            // Since PriorityLevel doesn't have Code property, we'll check by generated code pattern
            // Code is generated as P{Level:D2}, so we extract the level from the code
            if (code.StartsWith("P") && code.Length >= 2 && int.TryParse(code.Substring(1), out int level))
            {
                var query = _priorityLevelRepository.GetQueryable()
                    .Where(p => p.Level == level && !p.IsDeleted);
                
                if (excludeId.HasValue)
                {
                    query = query.Where(p => p.Id != excludeId.Value);
                }

                return !await query.AnyAsync();
            }
            
            return true; // If code format is invalid, consider it unique
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking priority level code uniqueness: {Code}", code);
            throw;
        }
    }
}