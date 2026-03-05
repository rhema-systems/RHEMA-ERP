using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Organization Structure Service

public class OrganizationStructureService : IOrganizationStructureService
{
    private readonly IOrganizationStructureRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrganizationStructureService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public OrganizationStructureService(
        IOrganizationStructureRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<OrganizationStructureService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<OrganizationStructureDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization structure with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<OrganizationStructureDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization structure with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<OrganizationStructureDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<OrganizationStructureSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<OrganizationStructureDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrganizationStructureDto>
        {
            Items = pagedEntities.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<OrganizationStructureDto> CreateAsync(CreateOrganizationStructureDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate unique name
        if (await _repository.ExistsByNameAsync(createDto.Name))
            throw new InvalidOperationException($"Organization structure with name '{createDto.Name}' already exists.");

        // Validate unique code
        if (!string.IsNullOrEmpty(createDto.Code) && await _repository.ExistsByCodeAsync(createDto.Code))
            throw new InvalidOperationException($"Organization structure with code '{createDto.Code}' already exists.");

        var entity = createDto.ToEntity();

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization structure created: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<OrganizationStructureDto> UpdateAsync(UpdateOrganizationStructureDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Organization structure with ID '{updateDto.Id}' not found.");

        // Validate unique name
        if (await _repository.ExistsByNameAsync(updateDto.Name, updateDto.Id))
            throw new InvalidOperationException($"Organization structure with name '{updateDto.Name}' already exists.");

        // Validate unique code
        if (!string.IsNullOrEmpty(updateDto.Code) && await _repository.ExistsByCodeAsync(updateDto.Code, updateDto.Id))
            throw new InvalidOperationException($"Organization structure with code '{updateDto.Code}' already exists.");

        updateDto.UpdateEntity(entity);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization structure updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization structure with ID '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization structure deleted: {Id}", id);

        return true;
    }

    public async Task<OrganizationStructureDto?> GetDefaultStructureAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetDefaultStructureAsync(_currentUserProvider.TenantId);
        return entity?.ToDto();
    }

    public async Task<bool> SetAsDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization structure with ID '{id}' not found.");

        // Unset all defaults for this tenant
        var allStructures = await _repository.FindAsync(s => s.TenantId == entity.TenantId && s.IsDefault);
        foreach (var structure in allStructures)
        {
            structure.IsDefault = false;
        }

        // Set this one as default
        entity.IsDefault = true;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization structure set as default: {Id}", id);

        return true;
    }
}

#endregion

#region Organization Level Service

public class OrganizationLevelService : IOrganizationLevelService
{
    private readonly IOrganizationLevelRepository _repository;
    private readonly IOrganizationStructureRepository _structureRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrganizationLevelService> _logger;

    public OrganizationLevelService(
        IOrganizationLevelRepository repository,
        IOrganizationStructureRepository structureRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrganizationLevelService> logger)
    {
        _repository = repository;
        _structureRepository = structureRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OrganizationLevelDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization level with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<OrganizationLevelDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithUnitsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization level with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<OrganizationLevelDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<OrganizationLevelSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<OrganizationLevelDto>> GetByStructureIdAsync(Guid structureId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByStructureIdOrderedAsync(structureId);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<OrganizationLevelDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrganizationLevelDto>
        {
            Items = pagedEntities.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<OrganizationLevelDto> CreateAsync(CreateOrganizationLevelDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate structure exists
        var structure = await _structureRepository.GetByIdAsync(createDto.StructureId);
        if (structure == null)
            throw new ArgumentException($"Organization structure with ID '{createDto.StructureId}' not found.");

        // Validate unique name in structure
        if (await _repository.ExistsByNameAsync(createDto.StructureId, createDto.Name))
            throw new InvalidOperationException($"Organization level with name '{createDto.Name}' already exists in this structure.");

        // Validate unique code in structure
        if (!string.IsNullOrEmpty(createDto.Code) && await _repository.ExistsByCodeAsync(createDto.StructureId, createDto.Code))
            throw new InvalidOperationException($"Organization level with code '{createDto.Code}' already exists in this structure.");

        // Validate unique level number
        if (await _repository.LevelNumberExistsAsync(createDto.StructureId, createDto.LevelNumber))
            throw new InvalidOperationException($"Level number '{createDto.LevelNumber}' is already used in this structure.");

        var entity = createDto.ToEntity();

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization level created: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<OrganizationLevelDto> UpdateAsync(UpdateOrganizationLevelDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Organization level with ID '{updateDto.Id}' not found.");

        // Validate unique name in structure
        if (await _repository.ExistsByNameAsync(updateDto.StructureId, updateDto.Name, updateDto.Id))
            throw new InvalidOperationException($"Organization level with name '{updateDto.Name}' already exists in this structure.");

        // Validate unique code in structure
        if (!string.IsNullOrEmpty(updateDto.Code) && await _repository.ExistsByCodeAsync(updateDto.StructureId, updateDto.Code, updateDto.Id))
            throw new InvalidOperationException($"Organization level with code '{updateDto.Code}' already exists in this structure.");

        // Validate unique level number
        if (await _repository.LevelNumberExistsAsync(updateDto.StructureId, updateDto.LevelNumber, updateDto.Id))
            throw new InvalidOperationException($"Level number '{updateDto.LevelNumber}' is already used in this structure.");

        updateDto.UpdateEntity(entity);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization level updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization level with ID '{id}' not found.");

        // Check if level has units
        var unitCount = await _repository.GetUnitCountAsync(id);
        if (unitCount > 0)
            throw new InvalidOperationException($"Cannot delete organization level because it has {unitCount} unit(s).");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization level deleted: {Id}", id);

        return true;
    }
}

#endregion

#region Organization Unit Service

public class OrganizationUnitService : IOrganizationUnitService
{
    private readonly IOrganizationUnitRepository _repository;
    private readonly IOrganizationLevelRepository _levelRepository;
    private readonly IOrganizationUnitHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OrganizationUnitService> _logger;

    public OrganizationUnitService(
        IOrganizationUnitRepository repository,
        IOrganizationLevelRepository levelRepository,
        IOrganizationUnitHistoryRepository historyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork _unitOfWork,
        ILogger<OrganizationUnitService> logger)
    {
        _repository = repository;
        _levelRepository = levelRepository;
        _historyRepository = historyRepository;
        _currentUserProvider = currentUserProvider;
        this._unitOfWork = _unitOfWork;
        _logger = logger;
    }

    public async Task<OrganizationUnitDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization unit with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<OrganizationUnitDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithFullHierarchyAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization unit with ID '{id}' not found.");

        var dto = entity.ToDetailDto();
        dto.ChildCount = await _repository.GetChildCountAsync(id);
        dto.EmployeeCount = await _repository.GetEmployeeCountAsync(id);

        return dto;
    }

    public async Task<IEnumerable<OrganizationUnitDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<OrganizationUnitSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<OrganizationUnitDto>> GetByLevelIdAsync(Guid levelId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByLevelIdAsync(levelId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<OrganizationUnitDto>> GetChildUnitsAsync(Guid parentUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetChildUnitsAsync(parentUnitId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<OrganizationUnitDto>> GetRootUnitsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetRootUnitsAsync(_currentUserProvider.TenantId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<OrganizationUnitTreeDto>> GetHierarchyTreeAsync(CancellationToken cancellationToken = default)
    {
        // Load the full tenant hierarchy (so ChildUnits navigation is populated for every node),
        // then return only root nodes as the entry points for the tree.
        var allUnits = await _repository.GetHierarchyTreeAsync(_currentUserProvider.TenantId);

        var rootUnits = allUnits
            .Where(u => u.ParentUnitId == null)
            .OrderBy(u => u.Sequence)
            .ToList();

        return rootUnits.ToTreeDtoList();
    }

    public async Task<OrganizationUnitHierarchyDto> GetHierarchyFromUnitAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithFullHierarchyAsync(unitId);

        if (entity == null)
            throw new ArgumentException($"Organization unit with ID '{unitId}' not found.");

        return entity.ToHierarchyDto();
    }

    public async Task<PagedResult<OrganizationUnitDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrganizationUnitDto>
        {
            Items = pagedEntities.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<OrganizationUnitDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.SearchAsync(_currentUserProvider.TenantId, searchTerm);
        return entities.ToDtoList();
    }

    public async Task<OrganizationUnitDto> CreateAsync(CreateOrganizationUnitDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate level exists
        var level = await _levelRepository.GetByIdAsync(createDto.OrganizationLevelId);
        if (level == null)
            throw new ArgumentException($"Organization level with ID '{createDto.OrganizationLevelId}' not found.");

        if (level.RequiresHead && !createDto.HeadEmployeeId.HasValue)
            throw new InvalidOperationException("This level requires a unit head.");

        if (createDto.ParentUnitId.HasValue)
        {
            var parent = await _repository.GetWithDetailsAsync(createDto.ParentUnitId.Value);
            if (parent == null)
                throw new ArgumentException($"Parent organization unit with ID '{createDto.ParentUnitId}' not found.");

            if (parent.OrganizationLevel.StructureId != level.StructureId)
                throw new InvalidOperationException("Parent unit must belong to the same organization structure as the selected level.");

            var expectedChildLevelNumber = parent.OrganizationLevel.LevelNumber + 1;
            if (level.LevelNumber != expectedChildLevelNumber)
                throw new InvalidOperationException($"Invalid hierarchy: child level must be exactly one level below the parent (expected LevelNumber {expectedChildLevelNumber}).");
        }
        else
        {
            if (!level.IsRootLevel)
                throw new InvalidOperationException("Root units must be created under a root level.");

            var rootCount = await _repository.GetRootUnitCountByStructureAsync(_currentUserProvider.TenantId, level.StructureId);
            if (rootCount > 0)
                throw new InvalidOperationException("Only one root organization unit is allowed per structure.");
        }

        // Validate unique code
        if (!string.IsNullOrEmpty(createDto.Code) && await _repository.ExistsByCodeAsync(_currentUserProvider.TenantId, createDto.Code))
            throw new InvalidOperationException($"Organization unit with code '{createDto.Code}' already exists.");

        // Validate unique name in level
        if (await _repository.ExistsByNameInLevelAsync(createDto.OrganizationLevelId, createDto.Name))
            throw new InvalidOperationException($"Organization unit with name '{createDto.Name}' already exists in this level.");

        var entity = createDto.ToEntity();

        // Build path
        if (createDto.ParentUnitId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(createDto.ParentUnitId.Value);
            if (parent != null)
            {
                entity.Path = $"{parent.Path}/{entity.Id}";
            }
        }
        else
        {
            entity.Path = $"/{entity.Id}";
        }

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization unit created: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<OrganizationUnitDto> UpdateAsync(UpdateOrganizationUnitDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Organization unit with ID '{updateDto.Id}' not found.");

        // Level is immutable once a unit exists
        if (updateDto.OrganizationLevelId != entity.OrganizationLevelId)
            throw new InvalidOperationException("Organization level of an existing unit cannot be changed.");

        // Enforce deactivation rule
        if (entity.IsActive && !updateDto.IsActive)
        {
            var activeChildCount = await _repository.GetActiveChildCountAsync(entity.Id);
            if (activeChildCount > 0)
                throw new InvalidOperationException($"Cannot deactivate this unit because it has {activeChildCount} active child unit(s).");
        }

        // Parent change validation (including root rules and cycle prevention)
        var parentChanged = entity.ParentUnitId != updateDto.ParentUnitId;
        if (parentChanged)
        {
            if (updateDto.ParentUnitId.HasValue)
            {
                var newParentId = updateDto.ParentUnitId.Value;
                if (newParentId == entity.Id)
                    throw new InvalidOperationException("A unit cannot be its own parent.");

                var descendants = await _repository.GetDescendantsAsync(entity.Id);
                if (descendants.Any(d => d.Id == newParentId))
                    throw new InvalidOperationException("Invalid hierarchy: circular references are not allowed.");

                var parent = await _repository.GetWithDetailsAsync(newParentId);
                if (parent == null)
                    throw new ArgumentException($"Parent organization unit with ID '{newParentId}' not found.");

                if (parent.OrganizationLevel.StructureId != entity.OrganizationLevel.StructureId)
                    throw new InvalidOperationException("Parent unit must belong to the same organization structure.");

                var expectedChildLevelNumber = parent.OrganizationLevel.LevelNumber + 1;
                if (entity.OrganizationLevel.LevelNumber != expectedChildLevelNumber)
                    throw new InvalidOperationException($"Invalid hierarchy: child level must be exactly one level below the parent (expected LevelNumber {expectedChildLevelNumber}).");

                // Keep path in sync for this node (descendants are handled elsewhere if needed)
                entity.Path = $"{parent.Path}/{entity.Id}";
            }
            else
            {
                if (!entity.OrganizationLevel.IsRootLevel)
                    throw new InvalidOperationException("Only units at a root level can be moved to the root.");

                var rootCount = await _repository.GetRootUnitCountByStructureAsync(_currentUserProvider.TenantId, entity.OrganizationLevel.StructureId, entity.Id);
                if (rootCount > 0)
                    throw new InvalidOperationException("Only one root organization unit is allowed per structure.");

                entity.Path = $"/{entity.Id}";
            }
        }

        // Validate unique code
        if (!string.IsNullOrEmpty(updateDto.Code) && await _repository.ExistsByCodeAsync(entity.TenantId, updateDto.Code, updateDto.Id))
            throw new InvalidOperationException($"Organization unit with code '{updateDto.Code}' already exists.");

        // Validate unique name in level
        if (await _repository.ExistsByNameInLevelAsync(entity.OrganizationLevelId, updateDto.Name, updateDto.Id))
            throw new InvalidOperationException($"Organization unit with name '{updateDto.Name}' already exists in this level.");

        if (entity.OrganizationLevel.RequiresHead && !updateDto.HeadEmployeeId.HasValue)
            throw new InvalidOperationException("This level requires a unit head.");

        updateDto.UpdateEntity(entity);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization unit updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization unit with ID '{id}' not found.");

        // Check if has children
        var childCount = await _repository.GetChildCountAsync(id);
        if (childCount > 0)
            throw new InvalidOperationException($"Cannot delete organization unit because it has {childCount} child unit(s).");

        // Check if has employees
        var employeeCount = await _repository.GetEmployeeCountAsync(id);
        if (employeeCount > 0)
            throw new InvalidOperationException($"Cannot delete organization unit because it has {employeeCount} employee(s).");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization unit deleted: {Id}", id);

        return true;
    }

    public async Task<bool> MoveUnitAsync(Guid unitId, Guid? newParentId, string changeReason, CancellationToken cancellationToken = default)
    {
        var unit = await _repository.GetWithDetailsAsync(unitId);

        if (unit == null)
            throw new ArgumentException($"Organization unit with ID '{unitId}' not found.");

        if (newParentId.HasValue && newParentId.Value == unitId)
            throw new InvalidOperationException("A unit cannot be its own parent.");

        if (newParentId.HasValue)
        {
            var descendants = await _repository.GetDescendantsAsync(unitId);
            if (descendants.Any(d => d.Id == newParentId.Value))
                throw new InvalidOperationException("Invalid hierarchy: circular references are not allowed.");

            var parent = await _repository.GetWithDetailsAsync(newParentId.Value);
            if (parent == null)
                throw new ArgumentException($"Parent organization unit with ID '{newParentId}' not found.");

            if (parent.OrganizationLevel.StructureId != unit.OrganizationLevel.StructureId)
                throw new InvalidOperationException("Parent unit must belong to the same organization structure.");

            var expectedChildLevelNumber = parent.OrganizationLevel.LevelNumber + 1;
            if (unit.OrganizationLevel.LevelNumber != expectedChildLevelNumber)
                throw new InvalidOperationException($"Invalid hierarchy: unit level must be exactly one level below the parent (expected LevelNumber {expectedChildLevelNumber}).");
        }
        else
        {
            if (!unit.OrganizationLevel.IsRootLevel)
                throw new InvalidOperationException("Only units at a root level can be moved to the root.");

            var rootCount = await _repository.GetRootUnitCountByStructureAsync(_currentUserProvider.TenantId, unit.OrganizationLevel.StructureId, unit.Id);
            if (rootCount > 0)
                throw new InvalidOperationException("Only one root organization unit is allowed per structure.");
        }

        var oldParentId = unit.ParentUnitId;

        // Create history record
        var history = new OrganizationUnitHistory
        {
            OrganizationUnitId = unitId,
            PreviousParentId = oldParentId,
            NewParentId = newParentId,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            ChangeReason = changeReason
        };

        await _historyRepository.AddAsync(history);

        // Update unit
        unit.ParentUnitId = newParentId;

        // Update path
        if (newParentId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(newParentId.Value);
            if (parent != null)
            {
                unit.Path = $"{parent.Path}/{unit.Id}";
            }
        }
        else
        {
            unit.Path = $"/{unit.Id}";
        }

        await _repository.UpdateAsync(unit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization unit moved: {UnitId} from {OldParent} to {NewParent}", unitId, oldParentId, newParentId);

        return true;
    }

    public async Task<bool> ChangeHeadEmployeeAsync(Guid unitId, Guid? newHeadEmployeeId, string changeReason, CancellationToken cancellationToken = default)
    {
        var unit = await _repository.GetWithDetailsAsync(unitId);

        if (unit == null)
            throw new ArgumentException($"Organization unit with ID '{unitId}' not found.");

        if (unit.OrganizationLevel.RequiresHead && !newHeadEmployeeId.HasValue)
            throw new InvalidOperationException("This level requires a unit head.");

        var oldHeadEmployeeId = unit.HeadEmployeeId;

        // Create history record
        var history = new OrganizationUnitHistory
        {
            OrganizationUnitId = unitId,
            PreviousHeadEmployeeId = oldHeadEmployeeId,
            NewHeadEmployeeId = newHeadEmployeeId,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            ChangeReason = changeReason
        };

        await _historyRepository.AddAsync(history);

        // Update unit
        unit.HeadEmployeeId = newHeadEmployeeId;

        await _repository.UpdateAsync(unit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization unit head changed: {UnitId} from {OldHead} to {NewHead}", unitId, oldHeadEmployeeId, newHeadEmployeeId);

        return true;
    }
}

#endregion

#region Organization Unit History Service

public class OrganizationUnitHistoryService : IOrganizationUnitHistoryService
{
    private readonly IOrganizationUnitHistoryRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OrganizationUnitHistoryService> _logger;

    public OrganizationUnitHistoryService(
        IOrganizationUnitHistoryRepository repository,
        ICurrentUserProvider currentUserProvider,
        ILogger<OrganizationUnitHistoryService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<OrganizationUnitHistoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Organization unit history with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<OrganizationUnitHistoryDto>> GetByUnitIdAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByUnitIdAsync(unitId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<OrganizationUnitHistoryDto>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetRestructureHistoryAsync(_currentUserProvider.TenantId, startDate, endDate);
        return entities.ToDtoList();
    }

    public async Task<OrganizationUnitHistoryDto?> GetLatestByUnitIdAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetLatestByUnitIdAsync(unitId);
        return entity?.ToDto();
    }

    public async Task<OrganizationUnitHistoryDto?> GetActiveHistoryAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetActiveHistoryAsync(unitId);
        return entity?.ToDto();
    }

    public async Task<PagedResult<OrganizationUnitHistoryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .OrderByDescending(h => h.EffectiveFrom)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrganizationUnitHistoryDto>
        {
            Items = pagedEntities.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }
}

#endregion
