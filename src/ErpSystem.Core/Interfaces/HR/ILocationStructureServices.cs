using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

#region Location Structure Service

/// <summary>
/// Service interface for managing LocationStructure
/// </summary>
public interface ILocationStructureService
{
    /// <summary>
    /// Get location structure by ID
    /// </summary>
    Task<LocationStructureDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get location structure with detailed information
    /// </summary>
    Task<LocationStructureDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all location structures
    /// </summary>
    Task<IEnumerable<LocationStructureDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all location structures as summary
    /// </summary>
    Task<IEnumerable<LocationStructureSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged location structures
    /// </summary>
    Task<PagedResult<LocationStructureDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new location structure
    /// </summary>
    Task<LocationStructureDto> CreateAsync(CreateLocationStructureDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing location structure
    /// </summary>
    Task<LocationStructureDto> UpdateAsync(UpdateLocationStructureDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete location structure
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the default location structure for current tenant
    /// </summary>
    Task<LocationStructureDto?> GetDefaultStructureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Set structure as default (unsets others)
    /// </summary>
    Task<bool> SetAsDefaultAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

#region Location Level Service

/// <summary>
/// Service interface for managing LocationLevel
/// </summary>
public interface ILocationLevelService
{
    /// <summary>
    /// Get location level by ID
    /// </summary>
    Task<LocationLevelDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get location level with detailed information
    /// </summary>
    Task<LocationLevelDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all location levels
    /// </summary>
    Task<IEnumerable<LocationLevelDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all location levels as summary
    /// </summary>
    Task<IEnumerable<LocationLevelSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get levels by structure ID
    /// </summary>
    Task<IEnumerable<LocationLevelDto>> GetByStructureIdAsync(Guid structureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged location levels
    /// </summary>
    Task<PagedResult<LocationLevelDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new location level
    /// </summary>
    Task<LocationLevelDto> CreateAsync(CreateLocationLevelDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing location level
    /// </summary>
    Task<LocationLevelDto> UpdateAsync(UpdateLocationLevelDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete location level
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

#region Location Service

/// <summary>
/// Service interface for managing Location
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Get location by ID
    /// </summary>
    Task<LocationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get location with detailed information
    /// </summary>
    Task<LocationDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all locations
    /// </summary>
    Task<IEnumerable<LocationDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all locations as summary
    /// </summary>
    Task<IEnumerable<LocationSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get locations by level ID
    /// </summary>
    Task<IEnumerable<LocationDto>> GetByLevelIdAsync(Guid levelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get child locations of a parent location
    /// </summary>
    Task<IEnumerable<LocationDto>> GetChildLocationsAsync(Guid parentLocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get root locations for a structure
    /// </summary>
    Task<IEnumerable<LocationDto>> GetRootLocationsAsync(Guid structureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get locations by structure ID
    /// </summary>
    Task<IEnumerable<LocationDto>> GetByStructureIdAsync(Guid structureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get full hierarchy tree for a structure
    /// </summary>
    Task<IEnumerable<LocationTreeDto>> GetHierarchyTreeAsync(Guid structureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get hierarchy starting from a specific location
    /// </summary>
    Task<LocationHierarchyDto> GetHierarchyFromLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get locations by country
    /// </summary>
    Task<IEnumerable<LocationDto>> GetByCountryAsync(Guid countryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged locations
    /// </summary>
    Task<PagedResult<LocationDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search locations by name, code, or city
    /// </summary>
    Task<IEnumerable<LocationDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new location
    /// </summary>
    Task<LocationDto> CreateAsync(CreateLocationDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing location
    /// </summary>
    Task<LocationDto> UpdateAsync(UpdateLocationDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete location
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move location to a new parent
    /// </summary>
    Task<bool> MoveLocationAsync(Guid locationId, Guid? newParentId, CancellationToken cancellationToken = default);
}

#endregion

#region Location Contact Service

/// <summary>
/// Service interface for managing LocationContact
/// </summary>
public interface ILocationContactService
{
    /// <summary>
    /// Get location contact by ID
    /// </summary>
    Task<LocationContactDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get location contact with detailed information
    /// </summary>
    Task<LocationContactDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all contacts for a location
    /// </summary>
    Task<IEnumerable<LocationContactDto>> GetByLocationIdAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all contacts for a location as summary
    /// </summary>
    Task<IEnumerable<LocationContactSummaryDto>> GetSummaryByLocationIdAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get primary contact for a location
    /// </summary>
    Task<LocationContactDto?> GetPrimaryContactAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all location contacts
    /// </summary>
    Task<IEnumerable<LocationContactDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged location contacts
    /// </summary>
    Task<PagedResult<LocationContactDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new location contact
    /// </summary>
    Task<LocationContactDto> CreateAsync(CreateLocationContactDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing location contact
    /// </summary>
    Task<LocationContactDto> UpdateAsync(UpdateLocationContactDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete location contact
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set contact as primary (unsets others for that location)
    /// </summary>
    Task<bool> SetAsPrimaryAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
