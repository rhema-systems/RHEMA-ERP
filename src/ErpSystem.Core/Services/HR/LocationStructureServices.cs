using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Location Structure Service

public class LocationStructureService : ILocationStructureService
{
    private readonly ILocationStructureRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LocationStructureService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public LocationStructureService(
        ILocationStructureRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<LocationStructureService> logger)
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

    public async Task<LocationStructureDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location structure with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<LocationStructureDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location structure with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<LocationStructureDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).OrderBy(e => e.Name).ToDtoList();
    }

    public async Task<IEnumerable<LocationStructureSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).OrderBy(e => e.Name).ToSummaryDtoList();
    }

    public async Task<PagedResult<LocationStructureDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(e => e.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .OrderBy(e => e.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LocationStructureDto>
        {
            Items = pagedEntities.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<LocationStructureDto> CreateAsync(CreateLocationStructureDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate unique name
        if (await _repository.ExistsByNameAsync(createDto.Name))
            throw new InvalidOperationException($"Location structure with name '{createDto.Name}' already exists.");

        // Validate unique code
        if (!string.IsNullOrEmpty(createDto.Code) && await _repository.ExistsByCodeAsync(createDto.Code))
            throw new InvalidOperationException($"Location structure with code '{createDto.Code}' already exists.");

        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location structure created: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<LocationStructureDto> UpdateAsync(UpdateLocationStructureDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(updateDto.Id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Location structure with ID '{updateDto.Id}' not found.");

        // Validate unique name
        if (await _repository.ExistsByNameAsync(updateDto.Name, updateDto.Id))
            throw new InvalidOperationException($"Location structure with name '{updateDto.Name}' already exists.");

        // Validate unique code
        if (!string.IsNullOrEmpty(updateDto.Code) && await _repository.ExistsByCodeAsync(updateDto.Code, updateDto.Id))
            throw new InvalidOperationException($"Location structure with code '{updateDto.Code}' already exists.");

        updateDto.UpdateEntity(entity);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location structure updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Location structure with ID '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location structure deleted: {Id}", id);

        return true;
    }

    public async Task<LocationStructureDto?> GetDefaultStructureAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetDefaultStructureAsync(_currentUserProvider.TenantId);
        return entity?.ToDto();
    }

    public async Task<bool> SetAsDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Location structure with ID '{id}' not found.");

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

        _logger.LogInformation("Location structure set as default: {Id}", id);

        return true;
    }
}

#endregion

#region Location Level Service

public class LocationLevelService : ILocationLevelService
{
    private readonly ILocationLevelRepository _repository;
    private readonly ILocationStructureRepository _structureRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<LocationLevelService> _logger;

    public LocationLevelService(
        ILocationLevelRepository repository,
        ILocationStructureRepository structureRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<LocationLevelService> logger)
    {
        _repository = repository;
        _structureRepository = structureRepository;
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

    public async Task<LocationLevelDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location level with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<LocationLevelDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithLocationsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location level with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<LocationLevelDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // Include the structure so the list DTO can populate StructureName.
        var entities = await _repository.GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .Include(e => e.Structure)
            .OrderBy(e => e.LevelNumber).ThenBy(e => e.Name)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<LocationLevelSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.LevelNumber).ThenBy(e => e.Name).ToSummaryDtoList();
    }

    public async Task<IEnumerable<LocationLevelDto>> GetByStructureIdAsync(Guid structureId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByStructureIdOrderedAsync(structureId);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<LocationLevelDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(e => e.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .OrderBy(e => e.LevelNumber).ThenBy(e => e.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LocationLevelDto>
        {
            Items = pagedEntities.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<LocationLevelDto> CreateAsync(CreateLocationLevelDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate structure exists
        var structure = await _structureRepository.GetByIdAsync(createDto.StructureId);
        if (structure == null)
            throw new ArgumentException($"Location structure with ID '{createDto.StructureId}' not found.");

        // Validate unique name in structure
        if (await _repository.ExistsByNameAsync(createDto.StructureId, createDto.Name))
            throw new InvalidOperationException($"Location level with name '{createDto.Name}' already exists in this structure.");

        // Validate unique code in structure
        if (!string.IsNullOrEmpty(createDto.Code) && await _repository.ExistsByCodeAsync(createDto.StructureId, createDto.Code))
            throw new InvalidOperationException($"Location level with code '{createDto.Code}' already exists in this structure.");

        // Validate unique level number
        if (await _repository.LevelNumberExistsAsync(createDto.StructureId, createDto.LevelNumber))
            throw new InvalidOperationException($"Level number '{createDto.LevelNumber}' is already used in this structure.");

        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location level created: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<LocationLevelDto> UpdateAsync(UpdateLocationLevelDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(updateDto.Id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Location level with ID '{updateDto.Id}' not found.");

        // Validate unique name in structure
        if (await _repository.ExistsByNameAsync(updateDto.StructureId, updateDto.Name, updateDto.Id))
            throw new InvalidOperationException($"Location level with name '{updateDto.Name}' already exists in this structure.");

        // Validate unique code in structure
        if (!string.IsNullOrEmpty(updateDto.Code) && await _repository.ExistsByCodeAsync(updateDto.StructureId, updateDto.Code, updateDto.Id))
            throw new InvalidOperationException($"Location level with code '{updateDto.Code}' already exists in this structure.");

        // Validate unique level number
        if (await _repository.LevelNumberExistsAsync(updateDto.StructureId, updateDto.LevelNumber, updateDto.Id))
            throw new InvalidOperationException($"Level number '{updateDto.LevelNumber}' is already used in this structure.");

        updateDto.UpdateEntity(entity);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location level updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location level with ID '{id}' not found.");

        // Check if level has locations
        var locationCount = await _repository.GetLocationCountAsync(id);
        if (locationCount > 0)
            throw new InvalidOperationException($"Cannot delete location level because it has {locationCount} location(s).");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location level deleted: {Id}", id);

        return true;
    }
}

#endregion

#region Location Service

public class LocationService : ILocationService
{
    private readonly ILocationRepository _repository;
    private readonly ILocationLevelRepository _levelRepository;
    private readonly IGeofenceZoneRepository _geofenceZoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<LocationService> _logger;

    // Shared reference data, not HR's. A site says which administrative area it stands in; this
    // resolves that into the City text the address prints, so the two cannot disagree.
    private readonly ErpSystem.Core.Services.Reference.IGeographyService _geography;

    public LocationService(
        ILocationRepository repository,
        ILocationLevelRepository levelRepository,
        IGeofenceZoneRepository geofenceZoneRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ErpSystem.Core.Services.Reference.IGeographyService geography,
        ILogger<LocationService> logger)
    {
        _repository = repository;
        _levelRepository = levelRepository;
        _geofenceZoneRepository = geofenceZoneRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _geography = geography;
        _logger = logger;
    }

    /// <summary>
    /// The map pin and the attendance zone. Both coordinates travel together, and the zone must be one
    /// of this tenant's. Until 2026-09-03 none of the three reached the entity from any DTO, so
    /// GeofenceVerificationService could never find a zone for any employee: the geofence feature
    /// had no door.
    /// </summary>
    private async Task ValidateGeoAsync(double? latitude, double? longitude, Guid? geofenceZoneId, Guid tenantId)
    {
        // A rule, not a lookup: InvalidOperationException reaches the client as a 400 with this message.
        // ArgumentException is the service's "not found" idiom and the update endpoint answers it 404.
        if (latitude.HasValue != longitude.HasValue)
            throw new InvalidOperationException("Latitude and longitude must be supplied together.");

        if (geofenceZoneId.HasValue)
        {
            var zone = await _geofenceZoneRepository.GetByIdAsync(geofenceZoneId.Value);
            if (zone == null || zone.IsDeleted || zone.TenantId != tenantId)
                throw new ArgumentException($"Geofence zone with ID '{geofenceZoneId}' not found.");
        }
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

    public async Task<LocationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<LocationDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithContactsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location with ID '{id}' not found.");

        var dto = entity.ToDetailDto();
        dto.ChildLocationCount = await _repository.GetChildCountAsync(id);
        dto.EmployeeCount = await _repository.GetEmployeeCountAsync(id);
        dto.ContactCount = await _repository.GetContactCountAsync(id);

        return dto;
    }

    public async Task<IEnumerable<LocationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // Include navigations so the list DTO can populate LevelName / StructureName /
        // ParentLocationName.
        var entities = await _repository.GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .Include(e => e.Structure)
            .Include(e => e.LocationLevel)
            .Include(e => e.ParentLocation)
            .Include(e => e.GeofenceZone)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<LocationSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // ⚠ Was the bare GetAllAsync, so `LevelName` was null and — once lane B1 of demo feedback
        // round 2 put `LevelNumber` on this DTO — every location ranked at tier 0. The level and
        // the country are what the summary maps; load them.
        var entities = await _repository.GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .Include(e => e.LocationLevel)
            .Include(e => e.Country)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<LocationDto>> GetByLevelIdAsync(Guid levelId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByLevelIdAsync(levelId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<LocationDto>> GetChildLocationsAsync(Guid parentLocationId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetChildLocationsAsync(parentLocationId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<LocationDto>> GetRootLocationsAsync(Guid structureId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetRootLocationsAsync(structureId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<LocationDto>> GetByStructureIdAsync(Guid structureId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByStructureIdAsync(structureId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<LocationTreeDto>> GetHierarchyTreeAsync(Guid structureId, CancellationToken cancellationToken = default)
    {
        var allLocations = await _repository.GetHierarchyTreeAsync(structureId);
        var rootLocations = allLocations
            .Where(l => l.ParentLocationId == null)
            .OrderBy(l => l.Sequence);

        return rootLocations.ToTreeDtoList();
    }

    public async Task<LocationHierarchyDto> GetHierarchyFromLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithFullHierarchyAsync(locationId);

        if (entity == null)
            throw new ArgumentException($"Location with ID '{locationId}' not found.");

        return entity.ToHierarchyDto();
    }

    public async Task<IEnumerable<LocationDto>> GetByCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByCountryAsync(countryId);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<LocationDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(e => e.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .OrderBy(e => e.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LocationDto>
        {
            Items = pagedEntities.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<LocationDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.SearchAsync(Guid.Empty, searchTerm); // TenantId from context
        return entities.ToDtoList();
    }

    public async Task<LocationDto> CreateAsync(CreateLocationDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Validate level exists
        var level = await _levelRepository.GetByIdAsync(createDto.LocationLevelId);
        if (level == null)
            throw new ArgumentException($"Location level with ID '{createDto.LocationLevelId}' not found.");

        if (level.StructureId != createDto.StructureId)
            throw new InvalidOperationException("Selected level must belong to the specified location structure.");

        if (createDto.ParentLocationId.HasValue)
        {
            var parent = await _repository.GetWithDetailsAsync(createDto.ParentLocationId.Value);
            if (parent == null)
                throw new ArgumentException($"Parent location with ID '{createDto.ParentLocationId}' not found.");

            if (parent.StructureId != createDto.StructureId)
                throw new InvalidOperationException("Parent location must belong to the same structure.");

            var expectedChildLevelNumber = parent.LocationLevel.LevelNumber + 1;
            if (level.LevelNumber != expectedChildLevelNumber)
                throw new InvalidOperationException($"Invalid hierarchy: child level must be exactly one level below the parent (expected LevelNumber {expectedChildLevelNumber}).");
        }
        else
        {
            if (level.LevelNumber != 1)
                throw new InvalidOperationException("Root locations must be created under a root level.");

            var rootCount = await _repository.GetRootLocationCountAsync(createDto.StructureId);
            if (rootCount > 0)
                throw new InvalidOperationException("Only one root location is allowed per structure.");
        }

        // Validate unique code
        if (!string.IsNullOrEmpty(createDto.Code) && await _repository.ExistsByCodeAsync(tenantId, createDto.Code))
            throw new InvalidOperationException($"Location with code '{createDto.Code}' already exists.");

        // Validate unique name in level
        if (await _repository.ExistsByNameInLevelAsync(createDto.LocationLevelId, createDto.Name))
            throw new InvalidOperationException($"Location with name '{createDto.Name}' already exists in this level.");

        await ValidateGeoAsync(createDto.Latitude, createDto.Longitude, createDto.GeofenceZoneId, tenantId);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        // Build path
        if (createDto.ParentLocationId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(createDto.ParentLocationId.Value);
            if (parent != null)
            {
                entity.Path = $"{parent.Path}/{entity.Id}";
            }
        }
        else
        {
            entity.Path = $"/{entity.Id}";
        }

        await ApplyGeoAreaSnapshotAsync(entity, cancellationToken);

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location created: {Id}", entity.Id);

        return entity.ToDto();
    }

    /// <summary>
    /// Rewrites <c>City</c> from the site's administrative area, so the printed address and the
    /// structured link cannot disagree. A null area leaves the text exactly as it was — most sites
    /// predate the tree and that text is the only address they have.
    /// </summary>
    private async Task ApplyGeoAreaSnapshotAsync(Location entity, CancellationToken cancellationToken)
    {
        if (entity.GeoAreaId is not { } areaId) return;

        var (_, city) = await _geography.GetAddressSnapshotAsync(areaId, cancellationToken);

        // (null, null) means the area could not be read — another tenant's, or removed between the
        // form loading and the save. Leave what the record said rather than blanking it.
        if (city is null)
        {
            _logger.LogWarning(
                "Location {LocationId} references geo area {GeoAreaId}, which could not be resolved to a "
                + "city; the address was left unchanged.", entity.Id, areaId);
            return;
        }

        entity.City = city;
    }

    public async Task<LocationDto> UpdateAsync(UpdateLocationDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(updateDto.Id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Location with ID '{updateDto.Id}' not found.");

        // Structure + level are immutable once a location exists
        if (updateDto.StructureId != entity.StructureId)
            throw new InvalidOperationException("Location structure of an existing location cannot be changed.");

        if (updateDto.LocationLevelId != entity.LocationLevelId)
            throw new InvalidOperationException("Location level of an existing location cannot be changed.");

        // Enforce deactivation rule
        if (entity.IsActive && !updateDto.IsActive)
        {
            var activeChildCount = await _repository.GetActiveChildCountAsync(entity.Id);
            if (activeChildCount > 0)
                throw new InvalidOperationException($"Cannot deactivate this location because it has {activeChildCount} active child location(s).");
        }

        // Parent change validation (including root rules and cycle prevention)
        var parentChanged = entity.ParentLocationId != updateDto.ParentLocationId;
        if (parentChanged)
        {
            if (updateDto.ParentLocationId.HasValue)
            {
                var newParentId = updateDto.ParentLocationId.Value;
                if (newParentId == entity.Id)
                    throw new InvalidOperationException("A location cannot be its own parent.");

                var descendants = await _repository.GetDescendantsAsync(entity.Id);
                if (descendants.Any(d => d.Id == newParentId))
                    throw new InvalidOperationException("Invalid hierarchy: circular references are not allowed.");

                var parent = await _repository.GetWithDetailsAsync(newParentId);
                if (parent == null)
                    throw new ArgumentException($"Parent location with ID '{newParentId}' not found.");

                if (parent.StructureId != entity.StructureId)
                    throw new InvalidOperationException("Parent location must belong to the same structure.");

                var expectedChildLevelNumber = parent.LocationLevel.LevelNumber + 1;
                if (entity.LocationLevel.LevelNumber != expectedChildLevelNumber)
                    throw new InvalidOperationException($"Invalid hierarchy: child level must be exactly one level below the parent (expected LevelNumber {expectedChildLevelNumber}).");

                entity.Path = $"{parent.Path}/{entity.Id}";
            }
            else
            {
                if (!entity.LocationLevel.IsRootLevel)
                    throw new InvalidOperationException("Only locations at a root level can be moved to the root.");

                var rootCount = await _repository.GetRootLocationCountAsync(entity.StructureId, entity.Id);
                if (rootCount > 0)
                    throw new InvalidOperationException("Only one root location is allowed per structure.");

                entity.Path = $"/{entity.Id}";
            }
        }

        // Validate unique code
        if (!string.IsNullOrEmpty(updateDto.Code) && await _repository.ExistsByCodeAsync(entity.TenantId, updateDto.Code, updateDto.Id))
            throw new InvalidOperationException($"Location with code '{updateDto.Code}' already exists.");

        // Validate unique name in level
        if (await _repository.ExistsByNameInLevelAsync(updateDto.LocationLevelId, updateDto.Name, updateDto.Id))
            throw new InvalidOperationException($"Location with name '{updateDto.Name}' already exists in this level.");

        await ValidateGeoAsync(updateDto.Latitude, updateDto.Longitude, updateDto.GeofenceZoneId, entity.TenantId);

        updateDto.UpdateEntity(entity);

        // ⚠ AFTER UpdateEntity, which has just written whatever City the caller sent. The tree wins.
        await ApplyGeoAreaSnapshotAsync(entity, cancellationToken);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location with ID '{id}' not found.");

        // Check if has children
        var childCount = await _repository.GetChildCountAsync(id);
        if (childCount > 0)
            throw new InvalidOperationException($"Cannot delete location because it has {childCount} child location(s).");

        // Check if has employees
        var employeeCount = await _repository.GetEmployeeCountAsync(id);
        if (employeeCount > 0)
            throw new InvalidOperationException($"Cannot delete location because it has {employeeCount} employee(s).");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location deleted: {Id}", id);

        return true;
    }

    public async Task<bool> MoveLocationAsync(Guid locationId, Guid? newParentId, CancellationToken cancellationToken = default)
    {
        var location = await _repository.GetWithDetailsAsync(locationId);

        if (location == null)
            throw new ArgumentException($"Location with ID '{locationId}' not found.");

        if (newParentId.HasValue && newParentId.Value == locationId)
            throw new InvalidOperationException("A location cannot be its own parent.");

        if (newParentId.HasValue)
        {
            var descendants = await _repository.GetDescendantsAsync(locationId);
            if (descendants.Any(d => d.Id == newParentId.Value))
                throw new InvalidOperationException("Invalid hierarchy: circular references are not allowed.");

            var parent = await _repository.GetWithDetailsAsync(newParentId.Value);
            if (parent == null)
                throw new ArgumentException($"Parent location with ID '{newParentId}' not found.");

            if (parent.StructureId != location.StructureId)
                throw new InvalidOperationException("Parent location must belong to the same structure.");

            var expectedChildLevelNumber = parent.LocationLevel.LevelNumber + 1;
            if (location.LocationLevel.LevelNumber != expectedChildLevelNumber)
                throw new InvalidOperationException($"Invalid hierarchy: location level must be exactly one level below the parent (expected LevelNumber {expectedChildLevelNumber}).");
        }
        else
        {
            if (!location.LocationLevel.IsRootLevel)
                throw new InvalidOperationException("Only locations at a root level can be moved to the root.");

            var rootCount = await _repository.GetRootLocationCountAsync(location.StructureId, location.Id);
            if (rootCount > 0)
                throw new InvalidOperationException("Only one root location is allowed per structure.");
        }

        // Update location
        location.ParentLocationId = newParentId;

        // Update path
        if (newParentId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(newParentId.Value);
            if (parent != null)
            {
                location.Path = $"{parent.Path}/{location.Id}";
            }
        }
        else
        {
            location.Path = $"/{location.Id}";
        }

        await _repository.UpdateAsync(location);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location moved: {LocationId} to parent {NewParentId}", locationId, newParentId);

        return true;
    }
}

#endregion

#region Location Contact Service

public class LocationContactService : ILocationContactService
{
    private readonly ILocationContactRepository _repository;
    private readonly ILocationRepository _locationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<LocationContactService> _logger;

    public LocationContactService(
        ILocationContactRepository repository,
        ILocationRepository locationRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<LocationContactService> logger)
    {
        _repository = repository;
        _locationRepository = locationRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Every sibling service in this file scopes explicitly; this one
    // did not, which is why its create had never worked. See CreateAsync.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<LocationContactDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location contact with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<LocationContactDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location contact with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<LocationContactDto>> GetByLocationIdAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByLocationIdAsync(locationId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<LocationContactSummaryDto>> GetSummaryByLocationIdAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByLocationIdAsync(locationId);
        return entities.ToSummaryDtoList();
    }

    public async Task<LocationContactDto?> GetPrimaryContactAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetPrimaryContactAsync(locationId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<LocationContactDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToDtoList();
    }

    public async Task<PagedResult<LocationContactDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var pagedEntities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LocationContactDto>
        {
            Items = pagedEntities.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<LocationContactDto> CreateAsync(CreateLocationContactDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate location exists
        var location = await _locationRepository.GetByIdAsync(createDto.LocationId);
        if (location == null)
            throw new ArgumentException($"Location with ID '{createDto.LocationId}' not found.");

        // If setting as primary, check if there's already a primary contact
        if (createDto.IsPrimary && await _repository.HasPrimaryContactAsync(createDto.LocationId))
        {
            // Clear existing primary flags
            await _repository.ClearPrimaryFlagsAsync(createDto.LocationId);
        }

        var entity = createDto.ToEntity();

        // ⚠ **This endpoint had never once succeeded.** The entity went in with no TenantId, so the
        // insert died on FK_LocationContacts_Tenants_TenantId (error 547) and the controller turned
        // that into a bare 500 naming nothing. Nothing in the frontend has ever called it, which is
        // exactly why the defect survived the port — a dead path cannot fail visibly. Found by the
        // lane-2 payload probe running it for the first time.
        entity.TenantId = GetTenantId();

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location contact created: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<LocationContactDto> UpdateAsync(UpdateLocationContactDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Location contact with ID '{updateDto.Id}' not found.");

        // If setting as primary, check if there's already a primary contact
        if (updateDto.IsPrimary && !entity.IsPrimary && await _repository.HasPrimaryContactAsync(updateDto.LocationId, updateDto.Id))
        {
            // Clear existing primary flags
            await _repository.ClearPrimaryFlagsAsync(updateDto.LocationId);
        }

        updateDto.UpdateEntity(entity);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location contact updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location contact with ID '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location contact deleted: {Id}", id);

        return true;
    }

    public async Task<bool> SetAsPrimaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Location contact with ID '{id}' not found.");

        // Clear existing primary flags for this location
        await _repository.ClearPrimaryFlagsAsync(entity.LocationId);

        // Set this one as primary
        entity.IsPrimary = true;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Location contact set as primary: {Id}", id);

        return true;
    }
}

#endregion
