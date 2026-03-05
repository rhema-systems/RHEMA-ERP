using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

#region Organization Structure Repository

/// <summary>
/// Repository interface for OrganizationStructure entity
/// </summary>
public interface IOrganizationStructureRepository : IGenericRepository<OrganizationStructure>
{
    /// <summary>
    /// Get organization structure with all levels loaded
    /// </summary>
    Task<OrganizationStructure?> GetWithLevelsAsync(Guid id);

    /// <summary>
    /// Get the default organization structure for a tenant
    /// </summary>
    Task<OrganizationStructure?> GetDefaultStructureAsync(Guid tenantId);

    /// <summary>
    /// Get organization structure by code
    /// </summary>
    Task<OrganizationStructure?> GetByCodeAsync(Guid tenantId, string code);

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
    /// Get all active organization structures for a tenant
    /// </summary>
    Task<IEnumerable<OrganizationStructure>> GetActiveStructuresAsync(Guid tenantId);

    /// <summary>
    /// Get organization structure with full details including units
    /// </summary>
    Task<OrganizationStructure?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Organization Level Repository

/// <summary>
/// Repository interface for OrganizationLevel entity
/// </summary>
public interface IOrganizationLevelRepository : IGenericRepository<OrganizationLevel>
{
    /// <summary>
    /// Get all levels for a specific organization structure
    /// </summary>
    Task<IEnumerable<OrganizationLevel>> GetByStructureIdAsync(Guid structureId);

    /// <summary>
    /// Get all levels for a structure ordered by level number
    /// </summary>
    Task<IEnumerable<OrganizationLevel>> GetByStructureIdOrderedAsync(Guid structureId);

    /// <summary>
    /// Get a specific level by structure and level number
    /// </summary>
    Task<OrganizationLevel?> GetByStructureAndLevelNumberAsync(Guid structureId, int levelNumber);

    /// <summary>
    /// Get the root level for a structure
    /// </summary>
    Task<OrganizationLevel?> GetRootLevelAsync(Guid structureId);

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
    /// Get organization level with all units
    /// </summary>
    Task<OrganizationLevel?> GetWithUnitsAsync(Guid id);

    /// <summary>
    /// Count units for a specific level
    /// </summary>
    Task<int> GetUnitCountAsync(Guid levelId);
}

#endregion

#region Organization Unit Repository

/// <summary>
/// Repository interface for OrganizationUnit entity
/// </summary>
public interface IOrganizationUnitRepository : IGenericRepository<OrganizationUnit>
{
    /// <summary>
    /// Get organization unit with all related data (level, parent, head employee)
    /// </summary>
    Task<OrganizationUnit?> GetWithDetailsAsync(Guid id);

    /// <summary>
    /// Get organization unit with full hierarchy details
    /// </summary>
    Task<OrganizationUnit?> GetWithFullHierarchyAsync(Guid id);

    /// <summary>
    /// Get all units for a specific organization level
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetByLevelIdAsync(Guid levelId);

    /// <summary>
    /// Get all child units of a parent unit
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetChildUnitsAsync(Guid parentUnitId);

    /// <summary>
    /// Get all root-level units (units with no parent)
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetRootUnitsAsync(Guid tenantId);

    /// <summary>
    /// Get organization unit by code
    /// </summary>
    Task<OrganizationUnit?> GetByCodeAsync(Guid tenantId, string code);

    /// <summary>
    /// Check if unit code exists
    /// </summary>
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null);

    /// <summary>
    /// Check if unit name exists at the same level
    /// </summary>
    Task<bool> ExistsByNameInLevelAsync(Guid levelId, string name, Guid? excludeId = null);

    /// <summary>
    /// Get units headed by a specific employee
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetUnitsByHeadEmployeeAsync(Guid employeeId);

    /// <summary>
    /// Get the full hierarchy tree for a tenant
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetHierarchyTreeAsync(Guid tenantId);

    /// <summary>
    /// Get all descendants of a unit (recursive)
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetDescendantsAsync(Guid unitId);

    /// <summary>
    /// Get all ancestors of a unit (recursive)
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetAncestorsAsync(Guid unitId);

    /// <summary>
    /// Count employees in a unit
    /// </summary>
    Task<int> GetEmployeeCountAsync(Guid unitId);

    /// <summary>
    /// Count child units
    /// </summary>
    Task<int> GetChildCountAsync(Guid unitId);

    /// <summary>
    /// Count active child units
    /// </summary>
    Task<int> GetActiveChildCountAsync(Guid unitId);

    /// <summary>
    /// Count root units (no parent) within a specific structure
    /// </summary>
    Task<int> GetRootUnitCountByStructureAsync(Guid tenantId, Guid structureId, Guid? excludeUnitId = null);

    /// <summary>
    /// Get units by parent with ordering
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetByParentOrderedAsync(Guid? parentUnitId, Guid tenantId);

    /// <summary>
    /// Search units by name or code
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> SearchAsync(Guid tenantId, string searchTerm);

    /// <summary>
    /// Get active units for a tenant
    /// </summary>
    Task<IEnumerable<OrganizationUnit>> GetActiveUnitsAsync(Guid tenantId);
}

#endregion

#region Organization Unit History Repository

/// <summary>
/// Repository interface for OrganizationUnitHistory entity
/// </summary>
public interface IOrganizationUnitHistoryRepository : IGenericRepository<OrganizationUnitHistory>
{
    /// <summary>
    /// Get all history records for a specific unit
    /// </summary>
    Task<IEnumerable<OrganizationUnitHistory>> GetByUnitIdAsync(Guid unitId);

    /// <summary>
    /// Get history records for a unit within a date range
    /// </summary>
    Task<IEnumerable<OrganizationUnitHistory>> GetByUnitIdAndDateRangeAsync(Guid unitId, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Get the latest history record for a unit
    /// </summary>
    Task<OrganizationUnitHistory?> GetLatestByUnitIdAsync(Guid unitId);

    /// <summary>
    /// Get active history record for a unit (where EffectiveTo is null)
    /// </summary>
    Task<OrganizationUnitHistory?> GetActiveHistoryAsync(Guid unitId);

    /// <summary>
    /// Get all history records for units under a specific head employee
    /// </summary>
    Task<IEnumerable<OrganizationUnitHistory>> GetByHeadEmployeeAsync(Guid employeeId);

    /// <summary>
    /// Get restructure history for a tenant within a date range
    /// </summary>
    Task<IEnumerable<OrganizationUnitHistory>> GetRestructureHistoryAsync(Guid tenantId, DateOnly startDate, DateOnly endDate);
}

#endregion
