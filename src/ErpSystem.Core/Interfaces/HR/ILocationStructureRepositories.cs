using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

#region Location Structure Repository

/// <summary>
/// Repository interface for LocationStructure entity
/// </summary>
public interface ILocationStructureRepository : IGenericRepository<LocationStructure>
{
    /// <summary>
    /// Get location structure with all levels loaded
    /// </summary>
    Task<LocationStructure?> GetWithLevelsAsync(Guid id);

    /// <summary>
    /// Get the default location structure for a tenant
    /// </summary>
    Task<LocationStructure?> GetDefaultStructureAsync(Guid tenantId);

    /// <summary>
    /// Get location structure by code
    /// </summary>
    Task<LocationStructure?> GetByCodeAsync(Guid tenantId, string code);

    /// <summary>
    /// Check if a structure name already exists for a tenant
    /// </summary>
    Task<bool> ExistsByNameAsync(Guid tenantId, string name, Guid? excludeId = null);

    /// <summary>
    /// Check if a structure name already exists for the current tenant (resolved by global query filter)
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null);

    /// <summary>
    /// Check if a structure code already exists for a tenant
    /// </summary>
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null);

    /// <summary>
    /// Check if a structure code already exists for the current tenant (resolved by global query filter)
    /// </summary>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null);

    /// <summary>
    /// Get all location structures for a tenant
    /// </summary>
    Task<IEnumerable<LocationStructure>> GetByTenantAsync(Guid tenantId);

    /// <summary>
    /// Get location structure with full details including locations
    /// </summary>
    Task<LocationStructure?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Location Level Repository

/// <summary>
/// Repository interface for LocationLevel entity
/// </summary>
public interface ILocationLevelRepository : IGenericRepository<LocationLevel>
{
    /// <summary>
    /// Get all levels for a specific location structure
    /// </summary>
    Task<IEnumerable<LocationLevel>> GetByStructureIdAsync(Guid structureId);

    /// <summary>
    /// Get all levels for a structure ordered by level number
    /// </summary>
    Task<IEnumerable<LocationLevel>> GetByStructureIdOrderedAsync(Guid structureId);

    /// <summary>
    /// Get a specific level by structure and level number
    /// </summary>
    Task<LocationLevel?> GetByStructureAndLevelNumberAsync(Guid structureId, int levelNumber);

    /// <summary>
    /// Get the root level for a structure
    /// </summary>
    Task<LocationLevel?> GetRootLevelAsync(Guid structureId);

    /// <summary>
    /// Check if level name exists in a structure
    /// </summary>
    Task<bool> ExistsByNameAsync(Guid structureId, string name, Guid? excludeId = null);

    /// <summary>
    /// Check if level code exists in a structure
    /// </summary>
    Task<bool> ExistsByCodeAsync(Guid structureId, string code, Guid? excludeId = null);

    /// <summary>
    /// Check if level number is already used in a structure
    /// </summary>
    Task<bool> LevelNumberExistsAsync(Guid structureId, int levelNumber, Guid? excludeId = null);

    /// <summary>
    /// Get location level with all locations
    /// </summary>
    Task<LocationLevel?> GetWithLocationsAsync(Guid id);

    /// <summary>
    /// Count locations for a specific level
    /// </summary>
    Task<int> GetLocationCountAsync(Guid levelId);
}

#endregion

#region Location Repository

/// <summary>
/// Repository interface for Location entity
/// </summary>
public interface ILocationRepository : IGenericRepository<Location>
{
    /// <summary>
    /// Get location with all related data (structure, level, parent, country)
    /// </summary>
    Task<Location?> GetWithDetailsAsync(Guid id);

    /// <summary>
    /// Get location with full hierarchy details
    /// </summary>
    Task<Location?> GetWithFullHierarchyAsync(Guid id);

    /// <summary>
    /// Get all locations for a specific location level
    /// </summary>
    Task<IEnumerable<Location>> GetByLevelIdAsync(Guid levelId);

    /// <summary>
    /// Get all child locations of a parent location
    /// </summary>
    Task<IEnumerable<Location>> GetChildLocationsAsync(Guid parentLocationId);

    /// <summary>
    /// Get all root-level locations (locations with no parent)
    /// </summary>
    Task<IEnumerable<Location>> GetRootLocationsAsync(Guid structureId);

    /// <summary>
    /// Get location by code
    /// </summary>
    Task<Location?> GetByCodeAsync(Guid tenantId, string code);

    /// <summary>
    /// Check if location code exists
    /// </summary>
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null);

    /// <summary>
    /// Check if location name exists at the same level
    /// </summary>
    Task<bool> ExistsByNameInLevelAsync(Guid levelId, string name, Guid? excludeId = null);

    /// <summary>
    /// Get all locations for a specific structure
    /// </summary>
    Task<IEnumerable<Location>> GetByStructureIdAsync(Guid structureId);

    /// <summary>
    /// Get the full hierarchy tree for a structure
    /// </summary>
    Task<IEnumerable<Location>> GetHierarchyTreeAsync(Guid structureId);

    /// <summary>
    /// Get all descendants of a location (recursive)
    /// </summary>
    Task<IEnumerable<Location>> GetDescendantsAsync(Guid locationId);

    /// <summary>
    /// Get all ancestors of a location (recursive)
    /// </summary>
    Task<IEnumerable<Location>> GetAncestorsAsync(Guid locationId);

    /// <summary>
    /// Count employees at a location
    /// </summary>
    Task<int> GetEmployeeCountAsync(Guid locationId);

    /// <summary>
    /// Count child locations
    /// </summary>
    Task<int> GetChildCountAsync(Guid locationId);

    /// <summary>
    /// Count active child locations
    /// </summary>
    Task<int> GetActiveChildCountAsync(Guid locationId);

    /// <summary>
    /// Count root locations (no parent) within a structure
    /// </summary>
    Task<int> GetRootLocationCountAsync(Guid structureId, Guid? excludeLocationId = null);

    /// <summary>
    /// Count contacts for a location
    /// </summary>
    Task<int> GetContactCountAsync(Guid locationId);

    /// <summary>
    /// Get locations by parent with ordering
    /// </summary>
    Task<IEnumerable<Location>> GetByParentOrderedAsync(Guid? parentLocationId, Guid structureId);

    /// <summary>
    /// Get locations by country
    /// </summary>
    Task<IEnumerable<Location>> GetByCountryAsync(Guid countryId);

    /// <summary>
    /// Search locations by name, code, or city
    /// </summary>
    Task<IEnumerable<Location>> SearchAsync(Guid tenantId, string searchTerm);

    /// <summary>
    /// Get active locations for a tenant
    /// </summary>
    Task<IEnumerable<Location>> GetActiveLocationsAsync(Guid tenantId);

    /// <summary>
    /// Get locations with contacts
    /// </summary>
    Task<Location?> GetWithContactsAsync(Guid id);
}

#endregion

#region Location Contact Repository

/// <summary>
/// Repository interface for LocationContact entity
/// </summary>
public interface ILocationContactRepository : IGenericRepository<LocationContact>
{
    /// <summary>
    /// Get all contacts for a specific location
    /// </summary>
    Task<IEnumerable<LocationContact>> GetByLocationIdAsync(Guid locationId);

    /// <summary>
    /// Get contact with full details (location and employee)
    /// </summary>
    Task<LocationContact?> GetWithDetailsAsync(Guid id);

    /// <summary>
    /// Get the primary contact for a location
    /// </summary>
    Task<LocationContact?> GetPrimaryContactAsync(Guid locationId);

    /// <summary>
    /// Get contacts for a specific employee
    /// </summary>
    Task<IEnumerable<LocationContact>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>
    /// Check if a location already has a primary contact
    /// </summary>
    Task<bool> HasPrimaryContactAsync(Guid locationId, Guid? excludeId = null);

    /// <summary>
    /// Clear primary flag for all contacts at a location
    /// </summary>
    Task ClearPrimaryFlagsAsync(Guid locationId);
}

#endregion
