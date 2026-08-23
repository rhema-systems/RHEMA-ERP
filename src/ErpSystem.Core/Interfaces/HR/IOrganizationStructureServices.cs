using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

#region Organization Structure Service

/// <summary>
/// Service interface for managing OrganizationStructure
/// </summary>
public interface IOrganizationStructureService
{
    /// <summary>
    /// Get organization structure by ID
    /// </summary>
    Task<OrganizationStructureDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get organization structure with detailed information
    /// </summary>
    Task<OrganizationStructureDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all organization structures
    /// </summary>
    Task<IEnumerable<OrganizationStructureDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all organization structures as summary
    /// </summary>
    Task<IEnumerable<OrganizationStructureSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged organization structures
    /// </summary>
    Task<PagedResult<OrganizationStructureDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new organization structure
    /// </summary>
    Task<OrganizationStructureDto> CreateAsync(CreateOrganizationStructureDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing organization structure
    /// </summary>
    Task<OrganizationStructureDto> UpdateAsync(UpdateOrganizationStructureDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete organization structure
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the default organization structure for current tenant
    /// </summary>
    Task<OrganizationStructureDto?> GetDefaultStructureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Set structure as default (unsets others)
    /// </summary>
    Task<bool> SetAsDefaultAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

#region Organization Level Service

/// <summary>
/// Service interface for managing OrganizationLevel
/// </summary>
public interface IOrganizationLevelService
{
    /// <summary>
    /// Get organization level by ID
    /// </summary>
    Task<OrganizationLevelDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get organization level with detailed information
    /// </summary>
    Task<OrganizationLevelDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all organization levels
    /// </summary>
    Task<IEnumerable<OrganizationLevelDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all organization levels as summary
    /// </summary>
    Task<IEnumerable<OrganizationLevelSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get levels by structure ID
    /// </summary>
    Task<IEnumerable<OrganizationLevelDto>> GetByStructureIdAsync(Guid structureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged organization levels
    /// </summary>
    Task<PagedResult<OrganizationLevelDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new organization level
    /// </summary>
    Task<OrganizationLevelDto> CreateAsync(CreateOrganizationLevelDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing organization level
    /// </summary>
    Task<OrganizationLevelDto> UpdateAsync(UpdateOrganizationLevelDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete organization level
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

#region Organization Unit Service

/// <summary>
/// Service interface for managing OrganizationUnit
/// </summary>
public interface IOrganizationUnitService
{
    /// <summary>
    /// Get organization unit by ID
    /// </summary>
    Task<OrganizationUnitDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get organization unit with detailed information
    /// </summary>
    Task<OrganizationUnitDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all organization units
    /// </summary>
    Task<IEnumerable<OrganizationUnitDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all organization units as summary
    /// </summary>
    Task<IEnumerable<OrganizationUnitSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get units by level ID
    /// </summary>
    Task<IEnumerable<OrganizationUnitDto>> GetByLevelIdAsync(Guid levelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get child units of a parent unit
    /// </summary>
    Task<IEnumerable<OrganizationUnitDto>> GetChildUnitsAsync(Guid parentUnitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get root units (no parent)
    /// </summary>
    Task<IEnumerable<OrganizationUnitDto>> GetRootUnitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get full hierarchy tree
    /// </summary>
    Task<IEnumerable<OrganizationUnitTreeDto>> GetHierarchyTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get hierarchy starting from a specific unit
    /// </summary>
    Task<OrganizationUnitHierarchyDto> GetHierarchyFromUnitAsync(Guid unitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged organization units
    /// </summary>
    Task<PagedResult<OrganizationUnitDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search organization units by name or code
    /// </summary>
    Task<IEnumerable<OrganizationUnitDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new organization unit
    /// </summary>
    Task<OrganizationUnitDto> CreateAsync(CreateOrganizationUnitDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing organization unit
    /// </summary>
    Task<OrganizationUnitDto> UpdateAsync(UpdateOrganizationUnitDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete organization unit
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move unit to a new parent (restructure)
    /// </summary>
    Task<bool> MoveUnitAsync(Guid unitId, Guid? newParentId, string changeReason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Change unit head employee
    /// </summary>
    Task<bool> ChangeHeadEmployeeAsync(Guid unitId, Guid? newHeadEmployeeId, string changeReason, CancellationToken cancellationToken = default);
}

#endregion

#region Organization Unit History Service

/// <summary>
/// Service interface for managing OrganizationUnitHistory
/// </summary>
public interface IOrganizationUnitHistoryService
{
    /// <summary>
    /// Get organization unit history by ID
    /// </summary>
    Task<OrganizationUnitHistoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all history records for a unit
    /// </summary>
    Task<IEnumerable<OrganizationUnitHistoryDto>> GetByUnitIdAsync(Guid unitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get history records within a date range
    /// </summary>
    Task<IEnumerable<OrganizationUnitHistoryDto>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    // GetLatestByUnitIdAsync / GetActiveHistoryAsync deleted in areas 19–23 slice 12: since slice 3
    // made the log effective-dated per series, the newest row in a series is always the open one,
    // so the two returned the same row — and one row cannot state an arrangement that is up to two
    // rows. Use GetByUnitIdAsync and read both series. See OrganizationUnitHistoryController.

    /// <summary>
    /// Get paged organization unit history, optionally narrowed to a unit, a date range or a change type.
    /// </summary>
    /// <remarks>
    /// The filter is optional and omitting it is the pre-slice-5 behaviour exactly.
    /// </remarks>
    Task<PagedResult<OrganizationUnitHistoryDto>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        OrganizationUnitHistoryFilterDto? filter = null,
        CancellationToken cancellationToken = default);
}

#endregion
