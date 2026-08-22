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

    // The ApplicationDbContext is registered without a tenant, so its global tenant
    // query-filter and TenantId auto-stamp are inert. Following the RHEMA convention,
    // this service scopes reads/writes to the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
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
        var tenantId = GetTenantId();
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).OrderBy(e => e.Name).ToDtoList();
    }

    public async Task<IEnumerable<OrganizationStructureSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).OrderBy(e => e.Name).ToSummaryDtoList();
    }

    public async Task<PagedResult<OrganizationStructureDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(e => e.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .OrderBy(e => e.Name)
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
        entity.TenantId = GetTenantId();

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization structure created: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<OrganizationStructureDto> UpdateAsync(UpdateOrganizationStructureDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(updateDto.Id);

        if (entity == null || entity.TenantId != GetTenantId())
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

        if (entity == null || entity.TenantId != GetTenantId())
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

        if (entity == null || entity.TenantId != GetTenantId())
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OrganizationLevelService> _logger;

    public OrganizationLevelService(
        IOrganizationLevelRepository repository,
        IOrganizationStructureRepository structureRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<OrganizationLevelService> logger)
    {
        _repository = repository;
        _structureRepository = structureRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global
    // tenant query-filter and TenantId auto-stamp are inert. Following the
    // RHEMA convention (see finance services), this service scopes reads/writes
    // to the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
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
        var tenantId = GetTenantId();
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.LevelNumber).ThenBy(e => e.Name).ToDtoList();
    }

    public async Task<IEnumerable<OrganizationLevelSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.LevelNumber).ThenBy(e => e.Name).ToSummaryDtoList();
    }

    public async Task<IEnumerable<OrganizationLevelDto>> GetByStructureIdAsync(Guid structureId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetByStructureIdOrderedAsync(structureId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<PagedResult<OrganizationLevelDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(e => e.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        // Include the structure so the list DTO can populate StructureName
        // (GetQueryable() is bare — no includes).
        var pagedEntities = await query
            .Include(l => l.OrganizationStructure)
            .OrderBy(l => l.LevelNumber)
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
        entity.TenantId = GetTenantId();

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

    // The ApplicationDbContext is registered without a tenant, so its global
    // tenant query-filter and TenantId auto-stamp are inert. Following the
    // RHEMA convention, this service scopes reads/writes to the current tenant
    // explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
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
        var tenantId = GetTenantId();
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).OrderBy(e => e.Name).ToDtoList();
    }

    public async Task<IEnumerable<OrganizationUnitSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // Include the level/parent navigations so the summary DTO can populate
        // LevelName / ParentUnitName (GetAllAsync is bare — no includes).
        var entities = await _repository.GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.ParentUnit)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<OrganizationUnitDto>> GetByLevelIdAsync(Guid levelId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetByLevelIdAsync(levelId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
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
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(e => e.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        // Include navigations so the list DTO can populate LevelName / ParentUnitName /
        // HeadEmployeeName (GetQueryable() is bare — no includes). These are all
        // single-valued references, so no cartesian explosion with paging.
        var pagedEntities = await query
            .Include(u => u.OrganizationLevel)
            .Include(u => u.ParentUnit)
            .Include(u => u.HeadEmployee)
            .OrderBy(u => u.Name)
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

        // Head is optional at creation (org structure is typically set up before
        // employees exist); a head can be assigned later via change-head.

        if (createDto.ParentUnitId.HasValue)
        {
            var parent = await _repository.GetWithDetailsAsync(createDto.ParentUnitId.Value);
            if (parent == null)
                throw new ArgumentException($"Parent organization unit with ID '{createDto.ParentUnitId}' not found.");

            if (parent.OrganizationLevel.StructureId != level.StructureId)
                throw new InvalidOperationException("Parent unit must belong to the same organization structure as the selected level.");

            // Parent must sit at a higher tier (lower LevelNumber). Level-skipping
            // is allowed to accommodate real-world structures; same-level or
            // inverted nesting is rejected.
            if (level.LevelNumber <= parent.OrganizationLevel.LevelNumber)
                throw new InvalidOperationException("Invalid hierarchy: a unit's level must be below its parent's level.");
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
        entity.TenantId = GetTenantId();

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

                // Parent must sit at a higher tier (lower LevelNumber); level-skipping allowed.
                if (entity.OrganizationLevel.LevelNumber <= parent.OrganizationLevel.LevelNumber)
                    throw new InvalidOperationException("Invalid hierarchy: a unit's level must be below its parent's level.");

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

        // ⚠ Narrowed in areas 19-23 slice 3: this fires only when the update REMOVES a head that a
        // level requires, not whenever one happens to be absent.
        //
        // Measured on DEFAULT 2026-08-22: every level except Section carries RequiresHead, and
        // **18 of 41 live units sit at such a level with no head at all** — because `CreateAsync`
        // never checked this rule and `UpdateAsync` always did. The asymmetry made those 18 units
        // permanently un-editable: renaming one, or moving it, was refused for a field the edit was
        // not touching. (Until this slice's error-contract fix the refusal arrived as a canned 500,
        // so the screen showed a server error rather than a reason.)
        //
        // The rule should constrain the operation it is about. Stripping a required head is still
        // refused; inheriting an absent one no longer freezes the record. The create-side gap is
        // recorded rather than closed — enforcing it there would block unit creation outright for a
        // tenant that has no unit-head data at all, which is the org-authority gap, not this rule's
        // to solve.
        // `UpdateEntity` has not run yet, so `entity.HeadEmployeeId` still holds the current head.
        if (entity.OrganizationLevel.RequiresHead
            && entity.HeadEmployeeId.HasValue
            && !updateDto.HeadEmployeeId.HasValue)
            throw new InvalidOperationException(
                "This level requires a unit head, so the existing head cannot be removed.");

        // ⚠ The audit trail used to have two recording endpoints that could not save and one silent
        // path that could — and the silent one is this, the path the units edit screen calls. Both
        // operations the trail exists to record are assignable straight off `UpdateOrganizationUnitDto`
        // (see UpdateEntity), so a reparent or a change of head through the ordinary form left no
        // record at all. Capture the before-values and record the same rows the dedicated endpoints do.
        var previousParentId = entity.ParentUnitId;
        var previousHeadEmployeeId = entity.HeadEmployeeId;
        var headChanged = previousHeadEmployeeId != updateDto.HeadEmployeeId;

        updateDto.UpdateEntity(entity);

        if (parentChanged)
            await RecordHistoryAsync(
                entity.Id,
                previousParentId: previousParentId, newParentId: entity.ParentUnitId,
                previousHeadEmployeeId: null, newHeadEmployeeId: null,
                changeReason: updateDto.ChangeReason);

        if (headChanged)
            await RecordHistoryAsync(
                entity.Id,
                previousParentId: null, newParentId: null,
                previousHeadEmployeeId: previousHeadEmployeeId, newHeadEmployeeId: entity.HeadEmployeeId,
                changeReason: updateDto.ChangeReason);

        await _repository.UpdateAsync(entity);

        if (parentChanged)
            await CascadePathToDescendantsAsync(entity);

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

            // ⚠ This rule used to demand the unit sit EXACTLY one level below its new parent, while
            // `UpdateAsync` — the other way to perform the identical move — allowed level-skipping and
            // only required the parent to be at a higher tier. The same operation gave different
            // answers depending on which endpoint you reached it through.
            //
            // Reconciled onto the permissive rule, deliberately: it is the one the live data can
            // satisfy. TDC's structure skips levels in places, so the strict rule would refuse moves
            // the edit screen performs happily today, and "fixing" the divergence by tightening
            // `UpdateAsync` would break a working path to make a dead one consistent with it.
            if (unit.OrganizationLevel.LevelNumber <= parent.OrganizationLevel.LevelNumber)
                throw new InvalidOperationException("Invalid hierarchy: a unit's level must be below its parent's level.");
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

        // ⚠ A move to the parent the unit already has records nothing. `UpdateAsync` has always
        // guarded its history writes on `parentChanged`/`headChanged`; these two endpoints did not,
        // so re-sending the current parent stamped the trail with "moved from A to A". Slice 5 found
        // nine such rows live on DEFAULT — three of them against the real `Administration` unit —
        // and they are indelible, because a change log deliberately has no delete. They are also the
        // only reason the `Other` classification, which no real change can produce, has any rows.
        //
        // Returning true rather than refusing: the caller asked for a state that already holds, and
        // an idempotent no-op is the honest answer to that. What it must not do is claim something
        // happened.
        if (oldParentId == newParentId)
        {
            _logger.LogInformation(
                "Organization unit move is a no-op: {UnitId} is already under {Parent}", unitId, newParentId);
            return true;
        }

        await RecordHistoryAsync(
            unitId,
            previousParentId: oldParentId, newParentId: newParentId,
            previousHeadEmployeeId: null, newHeadEmployeeId: null,
            changeReason: changeReason);

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
        await CascadePathToDescendantsAsync(unit);
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

        // The same no-op guard as MoveUnitAsync — reappointing the sitting head is not a change of
        // leadership, and the log should not say it was.
        if (oldHeadEmployeeId == newHeadEmployeeId)
        {
            _logger.LogInformation(
                "Organization unit head change is a no-op: {UnitId} is already headed by {Head}", unitId, newHeadEmployeeId);
            return true;
        }

        await RecordHistoryAsync(
            unitId,
            previousParentId: null, newParentId: null,
            previousHeadEmployeeId: oldHeadEmployeeId, newHeadEmployeeId: newHeadEmployeeId,
            changeReason: changeReason);

        // Update unit
        unit.HeadEmployeeId = newHeadEmployeeId;

        await _repository.UpdateAsync(unit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization unit head changed: {UnitId} from {OldHead} to {NewHead}", unitId, oldHeadEmployeeId, newHeadEmployeeId);

        return true;
    }

    /// <summary>
    /// Queues one <see cref="OrganizationUnitHistory"/> row. The caller saves.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>The tenant stamp is the whole reason this helper exists.</b> Both writers used to
    /// build the entity inline and neither stamped <c>TenantId</c>. <c>OrganizationUnitHistory</c> is a
    /// <c>TenantEntity</c> and the DbContext auto-stamp is inert, so every save took a foreign-key
    /// violation and answered 500 — which is why the table held zero rows against 41 live units, and
    /// why all six read endpoints could only ever return nothing. Neither endpoint had a frontend
    /// caller, so nobody ever saw the failure.</para>
    ///
    /// <para>Three call sites write history now rather than two. Building the row in one place is
    /// what stops them drifting apart again.</para>
    /// </remarks>
    private async Task RecordHistoryAsync(
        Guid unitId,
        Guid? previousParentId,
        Guid? newParentId,
        Guid? previousHeadEmployeeId,
        Guid? newHeadEmployeeId,
        string? changeReason)
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");

        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow);

        // A parent row leaves both head ids null and a head row leaves both parent ids null, so which
        // series this change belongs to is readable off the arguments.
        var isParentChange = previousParentId.HasValue || newParentId.HasValue;

        // ⚠ Close the arrangement this change replaces. `EffectiveTo` is modelled on the entity, was
        // never written by anything, and slice 5's payload probe found it null on every row — a column
        // the register would render blank for ever, and an effective-dated log that only ever says
        // "from". Closing it per SERIES rather than per unit is the part that has to be right: a unit's
        // reporting line and its leadership move independently, and ending the head record because
        // somebody reparented the unit would make the log state something that never happened.
        var openQuery = _historyRepository.GetQueryable()
            .Where(h => h.TenantId == tenantId
                        && !h.IsDeleted
                        && h.OrganizationUnitId == unitId
                        && h.EffectiveTo == null);

        openQuery = isParentChange
            ? openQuery.Where(h => h.PreviousParentId != null || h.NewParentId != null)
            : openQuery.Where(h => h.PreviousHeadEmployeeId != null || h.NewHeadEmployeeId != null);

        var previous = await openQuery
            .OrderByDescending(h => h.EffectiveFrom)
            .ThenByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync();

        if (previous != null)
        {
            previous.EffectiveTo = effectiveFrom;
            await _historyRepository.UpdateAsync(previous);
        }

        await _historyRepository.AddAsync(new OrganizationUnitHistory
        {
            TenantId = tenantId,
            OrganizationUnitId = unitId,
            PreviousParentId = previousParentId,
            NewParentId = newParentId,
            PreviousHeadEmployeeId = previousHeadEmployeeId,
            NewHeadEmployeeId = newHeadEmployeeId,
            EffectiveFrom = effectiveFrom,
            ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim(),
            // ⚠ Nothing stamps CreatedBy in this codebase — there is no global auditing interceptor,
            // each service does it — so every row written since slice 3 carries an empty author.
            // A change log that records what changed and why but not WHO is missing the column the
            // question is usually asked about. Measured, not assumed: slice 5's probe read
            // `createdBy: ""` on every live row.
            CreatedBy = string.IsNullOrWhiteSpace(_currentUserProvider.FullName)
                ? _currentUserProvider.Username
                : _currentUserProvider.FullName,
            CreatedById = _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId,
        });
    }

    /// <summary>
    /// Rewrites <c>Path</c> for every descendant of a unit that has just moved.
    /// </summary>
    /// <remarks>
    /// <para>Both writers recomputed <c>Path</c> for the moved node only, and <c>UpdateAsync</c> said
    /// so in a comment — <i>"descendants are handled elsewhere if needed"</i> — where nothing, anywhere,
    /// handled them.</para>
    ///
    /// <para>The blast radius is smaller than it looks and worth stating exactly, so nobody later
    /// mistakes this for a hierarchy fix: <c>GetDescendantsAsync</c> and <c>GetAncestorsAsync</c> walk
    /// <c>ParentUnitId</c> recursively and never consult <c>Path</c>. <c>Path</c> feeds <c>Depth</c> on
    /// the read models and nothing else. So a stale descendant path made a unit report the wrong depth,
    /// not the wrong parent.</para>
    /// </remarks>
    private async Task CascadePathToDescendantsAsync(OrganizationUnit moved)
    {
        var descendants = (await _repository.GetDescendantsAsync(moved.Id)).ToList();
        if (descendants.Count == 0)
            return;

        // Index by parent so each node is repathed from its own parent's new path, breadth-first
        // from the moved node. Recomputing from the moved node's path alone would flatten the tree.
        var byParent = descendants
            .Where(d => d.ParentUnitId.HasValue)
            .GroupBy(d => d.ParentUnitId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var queue = new Queue<OrganizationUnit>();
        queue.Enqueue(moved);

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            if (!byParent.TryGetValue(parent.Id, out var children))
                continue;

            foreach (var child in children)
            {
                child.Path = $"{parent.Path}/{child.Id}";
                await _repository.UpdateAsync(child);
                queue.Enqueue(child);
            }
        }

        _logger.LogInformation(
            "Repathed {Count} descendant unit(s) after moving {UnitId}", descendants.Count, moved.Id);
    }
}

#endregion

#region Organization Unit History Service

/// <summary>
/// Reads the organisation-unit change log.
/// </summary>
/// <remarks>
/// <para>⚠ <b>Rewritten in areas 19-23 slice 3, and the reason is worth keeping.</b> Every method
/// here except <c>GetByDateRangeAsync</c> was tenant-blind — <c>GetPagedAsync</c> in particular took
/// <c>GetQueryable()</c> whole, counted every row in the table and paged across all tenants. That was
/// invisible while the table was empty, and the table was empty because the two writers could not
/// save (D-1). <b>Making the writes work is what would have turned these reads into a live
/// cross-tenant leak</b>, so the two changes belong in the same slice: the area-13 lesson running in
/// the other direction, where fixing a writer turns its readers into defects.</para>
///
/// <para>The four <c>*Name</c> fields on the DTO were also hardcoded to <c>null</c> with the comment
/// "Would need to load separately if needed". They are needed: this is a change log whose whole job is
/// to say <i>moved from A to B</i> and <i>head changed from X to Y</i>, and without them it is a list
/// of GUIDs. They are resolved here in two batched lookups rather than per row.</para>
/// </remarks>
public class OrganizationUnitHistoryService : IOrganizationUnitHistoryService
{
    private readonly IOrganizationUnitHistoryRepository _repository;
    private readonly IGenericRepository<OrganizationUnit> _units;
    private readonly IGenericRepository<Employee> _employees;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OrganizationUnitHistoryService> _logger;

    public OrganizationUnitHistoryService(
        IOrganizationUnitHistoryRepository repository,
        IGenericRepository<OrganizationUnit> units,
        IGenericRepository<Employee> employees,
        ICurrentUserProvider currentUserProvider,
        ILogger<OrganizationUnitHistoryService> logger)
    {
        _repository = repository;
        _units = units;
        _employees = employees;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter is
    // inert. Following the RHEMA convention, this service scopes every read to the authenticated
    // tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <remarks>
    /// ⚠ <b>There is deliberately no <c>Include</c> here, and putting one back would hide 86% of the
    /// log.</b> <c>OrganizationUnitHistory.OrganizationUnit</c> is a <i>required</i> navigation and
    /// every <c>BaseEntity</c> carries a global <c>!IsDeleted</c> query filter, so EF composes the
    /// include as an INNER JOIN against a filtered principal — and silently drops every history row
    /// whose unit has since been dissolved. <c>CountAsync</c> strips includes, so the paged envelope
    /// went on counting them: measured on DEFAULT 2026-08-22, <c>totalCount</c> said 66 while the page
    /// carried 9 — one visible unit out of the 40 the table holds rows for.
    ///
    /// <para>The rows an audit trail exists for are precisely the ones about things that no longer
    /// exist, so the unit's name is resolved in <see cref="ResolveNamesAsync"/> with the filters
    /// ignored instead.</para>
    /// </remarks>
    private IQueryable<OrganizationUnitHistory> Scoped(Guid tenantId) =>
        _repository.GetQueryable()
            .Where(h => h.TenantId == tenantId && !h.IsDeleted);

    public async Task<OrganizationUnitHistoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await Scoped(tenantId).FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Organization unit history with ID '{id}' not found.");

        return (await ResolveNamesAsync(new[] { entity }, tenantId, cancellationToken)).Single();
    }

    public async Task<IEnumerable<OrganizationUnitHistoryDto>> GetByUnitIdAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await Scoped(tenantId)
            .Where(h => h.OrganizationUnitId == unitId)
            .OrderByDescending(h => h.EffectiveFrom)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return await ResolveNamesAsync(entities, tenantId, cancellationToken);
    }

    public async Task<IEnumerable<OrganizationUnitHistoryDto>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await Scoped(tenantId)
            .Where(h => h.EffectiveFrom >= startDate && h.EffectiveFrom <= endDate)
            .OrderByDescending(h => h.EffectiveFrom)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return await ResolveNamesAsync(entities, tenantId, cancellationToken);
    }

    public async Task<OrganizationUnitHistoryDto?> GetLatestByUnitIdAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await Scoped(tenantId)
            .Where(h => h.OrganizationUnitId == unitId)
            .OrderByDescending(h => h.EffectiveFrom)
            .ThenByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : (await ResolveNamesAsync(new[] { entity }, tenantId, cancellationToken)).Single();
    }

    public async Task<OrganizationUnitHistoryDto?> GetActiveHistoryAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await Scoped(tenantId)
            .Where(h => h.OrganizationUnitId == unitId && h.EffectiveTo == null)
            .OrderByDescending(h => h.EffectiveFrom)
            .ThenByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : (await ResolveNamesAsync(new[] { entity }, tenantId, cancellationToken)).Single();
    }

    public async Task<PagedResult<OrganizationUnitHistoryDto>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        OrganizationUnitHistoryFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = Scoped(tenantId);

        // ⚠ Until slice 5 this method took a page number and nothing else, so the register could only
        // scroll: "what changed in this unit last quarter" meant paging the whole table and filtering
        // in the browser. Each clause below is one control on that screen. The count is taken AFTER
        // them, so a filtered page reports how many rows match rather than how many rows exist —
        // getting that backwards is how a register ends up claiming twelve pages of one row.
        if (filter is not null)
        {
            if (filter.UnitId.HasValue)
                query = query.Where(h => h.OrganizationUnitId == filter.UnitId.Value);

            if (filter.StartDate.HasValue)
                query = query.Where(h => h.EffectiveFrom >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(h => h.EffectiveFrom <= filter.EndDate.Value);

            if (!string.IsNullOrWhiteSpace(filter.ChangeType))
            {
                // The controller resolves the caller's string onto one of the three constants and
                // refuses anything else, so an unrecognised value never reaches here as "no filter".
                query = query.Where(OrganizationUnitChangeTypes.Predicate(filter.ChangeType));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .OrderByDescending(h => h.EffectiveFrom)
            // EffectiveFrom is a date, so a day's worth of changes ties. Without a tiebreaker the
            // engine is free to answer differently on different calls, and a row can appear on two
            // pages or none — the area 17/18 lesson about an unordered FirstOrDefault, one page wider.
            .ThenByDescending(h => h.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrganizationUnitHistoryDto>
        {
            Items = (await ResolveNamesAsync(pagedEntities, tenantId, cancellationToken)).ToList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Fills the four <c>*Name</c> fields the mapper leaves null, in two queries for the whole batch.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>Employee.FullName</c> is <c>[NotMapped]</c> and cannot be translated to SQL — projecting it
    /// server-side throws, as area 14 found the hard way. The name parts are selected and composed in
    /// memory instead.
    /// </remarks>
    private async Task<List<OrganizationUnitHistoryDto>> ResolveNamesAsync(
        IReadOnlyCollection<OrganizationUnitHistory> entities,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var dtos = entities.Select(e => e.ToDto()).ToList();
        if (dtos.Count == 0)
            return dtos;

        // The unit's own id joins the parent ids: with the Include gone (see Scoped) this lookup is
        // where OrganizationUnitName comes from too.
        var unitIds = entities
            .SelectMany(e => new[] { (Guid?)e.OrganizationUnitId, e.PreviousParentId, e.NewParentId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var employeeIds = entities
            .SelectMany(e => new[] { e.PreviousHeadEmployeeId, e.NewHeadEmployeeId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        // ⚠ Both lookups read THROUGH the soft delete, on purpose. A change log is about arrangements
        // that have ended: the unit a department was moved out of may since have been dissolved, and
        // the head it replaced may since have left. Resolved through the ordinary reads, those names
        // come back null and the log renders "moved from  to Operations Directorate" — a sentence
        // with a hole in it, and no way for a reader to tell a missing name from a name that was
        // never recorded.
        //
        // ⚠⚠ It must be `GetQueryableIncludingDeleted`, and `GetQueryable().IgnoreQueryFilters()` is
        // NOT the same thing — that was slice 5's own bug, caught by the harness rather than by
        // reading. `GetQueryable()` welds `.Where(e => !e.IsDeleted)` in as an ORDINARY predicate;
        // `IgnoreQueryFilters` lifts the DbContext's global filter and leaves the repository's own
        // `Where` standing, so the call compiles, reads exactly as if it worked, and changes nothing.
        //
        // Neither form widens the tenant boundary: both queries state `TenantId == tenantId`
        // themselves, which is the RHEMA convention this whole service already follows.
        var unitNames = unitIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _units.GetQueryableIncludingDeleted(u => u.TenantId == tenantId && unitIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var employeeNames = employeeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _employees.GetQueryableIncludingDeleted(e => e.TenantId == tenantId && employeeIds.Contains(e.Id))
                    .Select(e => new { e.Id, e.FirstName, e.MiddleName, e.LastName })
                    .ToListAsync(cancellationToken))
                .ToDictionary(
                    e => e.Id,
                    e => string.IsNullOrWhiteSpace(e.MiddleName)
                        ? $"{e.FirstName} {e.LastName}".Trim()
                        : $"{e.FirstName} {e.MiddleName} {e.LastName}".Trim());

        string? Unit(Guid? id) => id.HasValue && unitNames.TryGetValue(id.Value, out var n) ? n : null;
        string? Person(Guid? id) => id.HasValue && employeeNames.TryGetValue(id.Value, out var n) ? n : null;

        foreach (var dto in dtos)
        {
            dto.OrganizationUnitName = Unit(dto.OrganizationUnitId) ?? dto.OrganizationUnitName;
            dto.PreviousParentName = Unit(dto.PreviousParentId);
            dto.NewParentName = Unit(dto.NewParentId);
            dto.PreviousHeadEmployeeName = Person(dto.PreviousHeadEmployeeId);
            dto.NewHeadEmployeeName = Person(dto.NewHeadEmployeeId);
        }

        return dtos;
    }
}

#endregion
