using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

#region Organization Structure Repository

public class OrganizationStructureRepository : GenericRepository<OrganizationStructure>, IOrganizationStructureRepository
{
    public OrganizationStructureRepository(ApplicationDbContext context) : base(context) { }

    public async Task<OrganizationStructure?> GetWithLevelsAsync(Guid id)
    {
        return await _context.Set<OrganizationStructure>()
            .Include(os => os.Levels.OrderBy(l => l.LevelNumber))
            .FirstOrDefaultAsync(os => os.Id == id && !os.IsDeleted);
    }

    public async Task<OrganizationStructure?> GetDefaultStructureAsync(Guid tenantId)
    {
        return await _context.Set<OrganizationStructure>()
            .Include(os => os.Levels.OrderBy(l => l.LevelNumber))
            .FirstOrDefaultAsync(os => os.TenantId == tenantId && os.IsDefault && !os.IsDeleted);
    }

    public async Task<OrganizationStructure?> GetByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Set<OrganizationStructure>()
            .FirstOrDefaultAsync(os => os.TenantId == tenantId && os.Code == code && !os.IsDeleted);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string name, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationStructure>()
            .Where(os => os.TenantId == tenantId && os.Name == name && !os.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(os => os.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationStructure>()
            .Where(os => os.Name == name && !os.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(os => os.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationStructure>()
            .Where(os => os.TenantId == tenantId && os.Code == code && !os.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(os => os.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationStructure>()
            .Where(os => os.Code == code && !os.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(os => os.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<OrganizationStructure>> GetActiveStructuresAsync(Guid tenantId)
    {
        return await _context.Set<OrganizationStructure>()
            .Where(os => os.TenantId == tenantId && os.IsActive && !os.IsDeleted)
            .OrderBy(os => os.Name)
            .ToListAsync();
    }

    public async Task<OrganizationStructure?> GetWithFullDetailsAsync(Guid id)
    {
        return await _context.Set<OrganizationStructure>()
            .Include(os => os.Levels.OrderBy(l => l.LevelNumber))
                .ThenInclude(l => l.OrganizationUnits)
            .FirstOrDefaultAsync(os => os.Id == id && !os.IsDeleted);
    }
}

#endregion

#region Organization Level Repository

public class OrganizationLevelRepository : GenericRepository<OrganizationLevel>, IOrganizationLevelRepository
{
    public OrganizationLevelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrganizationLevel>> GetByStructureIdAsync(Guid structureId)
    {
        return await _context.Set<OrganizationLevel>()
            .Include(ol => ol.OrganizationStructure)
            .Where(ol => ol.StructureId == structureId && !ol.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationLevel>> GetByStructureIdOrderedAsync(Guid structureId)
    {
        return await _context.Set<OrganizationLevel>()
            .Include(ol => ol.OrganizationStructure)
            .Where(ol => ol.StructureId == structureId && !ol.IsDeleted)
            .OrderBy(ol => ol.LevelNumber)
            .ToListAsync();
    }

    public async Task<OrganizationLevel?> GetByStructureAndLevelNumberAsync(Guid structureId, int levelNumber)
    {
        return await _context.Set<OrganizationLevel>()
            .Include(ol => ol.OrganizationStructure)
            .FirstOrDefaultAsync(ol => ol.StructureId == structureId && ol.LevelNumber == levelNumber && !ol.IsDeleted);
    }

    public async Task<OrganizationLevel?> GetRootLevelAsync(Guid structureId)
    {
        // Get all levels for the structure to compute root level dynamically
        var levels = await _context.Set<OrganizationLevel>()
            .Include(ol => ol.OrganizationStructure)
                .ThenInclude(os => os.Levels.Where(l => l.IsActive && !l.IsDeleted))
            .Where(ol => ol.StructureId == structureId && ol.IsActive && !ol.IsDeleted)
            .ToListAsync();

        if (!levels.Any())
            return null;

        var minLevelNumber = levels.Min(l => l.LevelNumber);
        return levels.FirstOrDefault(l => l.LevelNumber == minLevelNumber);
    }

    public async Task<bool> ExistsByNameAsync(Guid structureId, string name, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationLevel>()
            .Where(ol => ol.StructureId == structureId && ol.Name == name && !ol.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ol => ol.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByCodeAsync(Guid structureId, string code, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationLevel>()
            .Where(ol => ol.StructureId == structureId && ol.Code == code && !ol.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ol => ol.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> LevelNumberExistsAsync(Guid structureId, int levelNumber, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationLevel>()
            .Where(ol => ol.StructureId == structureId && ol.LevelNumber == levelNumber && !ol.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ol => ol.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<OrganizationLevel?> GetWithUnitsAsync(Guid id)
    {
        return await _context.Set<OrganizationLevel>()
            .Include(ol => ol.OrganizationStructure)
            .Include(ol => ol.OrganizationUnits.Where(u => !u.IsDeleted))
            .FirstOrDefaultAsync(ol => ol.Id == id && !ol.IsDeleted);
    }

    public async Task<int> GetUnitCountAsync(Guid levelId)
    {
        return await _context.Set<OrganizationUnit>()
            .CountAsync(ou => ou.OrganizationLevelId == levelId && !ou.IsDeleted);
    }
}

#endregion

#region Organization Unit Repository

public class OrganizationUnitRepository : GenericRepository<OrganizationUnit>, IOrganizationUnitRepository
{
    public OrganizationUnitRepository(ApplicationDbContext context) : base(context) { }

    public async Task<OrganizationUnit?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
                .ThenInclude(ol => ol.OrganizationStructure)
            .Include(ou => ou.ParentUnit)
            .Include(ou => ou.HeadEmployee)
            .FirstOrDefaultAsync(ou => ou.Id == id && !ou.IsDeleted);
    }

    public async Task<OrganizationUnit?> GetWithFullHierarchyAsync(Guid id)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
                .ThenInclude(ol => ol.OrganizationStructure)
            .Include(ou => ou.ParentUnit)
            .Include(ou => ou.HeadEmployee)
            .Include(ou => ou.ChildUnits.Where(c => !c.IsDeleted))
            .Include(ou => ou.Employees.Where(e => !e.IsDeleted))
            .FirstOrDefaultAsync(ou => ou.Id == id && !ou.IsDeleted);
    }

    public async Task<IEnumerable<OrganizationUnit>> GetByLevelIdAsync(Guid levelId)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Include(ou => ou.ParentUnit)
            .Include(ou => ou.HeadEmployee)
            .Where(ou => ou.OrganizationLevelId == levelId && !ou.IsDeleted)
            .OrderBy(ou => ou.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationUnit>> GetChildUnitsAsync(Guid parentUnitId)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Include(ou => ou.HeadEmployee)
            .Where(ou => ou.ParentUnitId == parentUnitId && !ou.IsDeleted)
            .OrderBy(ou => ou.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationUnit>> GetRootUnitsAsync(Guid tenantId)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Include(ou => ou.HeadEmployee)
            .Where(ou => ou.TenantId == tenantId && ou.ParentUnitId == null && !ou.IsDeleted)
            .OrderBy(ou => ou.Sequence)
            .ToListAsync();
    }

    public async Task<OrganizationUnit?> GetByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .FirstOrDefaultAsync(ou => ou.TenantId == tenantId && ou.Code == code && !ou.IsDeleted);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationUnit>()
            .Where(ou => ou.TenantId == tenantId && ou.Code == code && !ou.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ou => ou.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByNameInLevelAsync(Guid levelId, string name, Guid? excludeId = null)
    {
        var query = _context.Set<OrganizationUnit>()
            .Where(ou => ou.OrganizationLevelId == levelId && ou.Name == name && !ou.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ou => ou.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<OrganizationUnit>> GetUnitsByHeadEmployeeAsync(Guid employeeId)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Include(ou => ou.HeadEmployee)
            .Where(ou => ou.HeadEmployeeId == employeeId && !ou.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationUnit>> GetHierarchyTreeAsync(Guid tenantId)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Include(ou => ou.HeadEmployee)
            .Include(ou => ou.ChildUnits.Where(c => !c.IsDeleted))
            .Where(ou => ou.TenantId == tenantId && !ou.IsDeleted)
            .OrderBy(ou => ou.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationUnit>> GetDescendantsAsync(Guid unitId)
    {
        var descendants = new List<OrganizationUnit>();
        var childUnits = await GetChildUnitsAsync(unitId);

        foreach (var child in childUnits)
        {
            descendants.Add(child);
            var childDescendants = await GetDescendantsAsync(child.Id);
            descendants.AddRange(childDescendants);
        }

        return descendants;
    }

    public async Task<IEnumerable<OrganizationUnit>> GetAncestorsAsync(Guid unitId)
    {
        var ancestors = new List<OrganizationUnit>();
        var unit = await GetByIdAsync(unitId);

        while (unit?.ParentUnitId != null)
        {
            var parent = await GetByIdAsync(unit.ParentUnitId.Value);
            if (parent != null)
            {
                ancestors.Add(parent);
                unit = parent;
            }
            else
            {
                break;
            }
        }

        return ancestors;
    }

    public async Task<int> GetEmployeeCountAsync(Guid unitId)
    {
        return await _context.Set<Employee>()
            .CountAsync(e => e.OrganizationUnitId == unitId && !e.IsDeleted);
    }

    public async Task<int> GetChildCountAsync(Guid unitId)
    {
        return await _context.Set<OrganizationUnit>()
            .CountAsync(ou => ou.ParentUnitId == unitId && !ou.IsDeleted);
    }

    public async Task<int> GetActiveChildCountAsync(Guid unitId)
    {
        return await _context.Set<OrganizationUnit>()
            .CountAsync(ou => ou.ParentUnitId == unitId && ou.IsActive && !ou.IsDeleted);
    }

    public async Task<int> GetRootUnitCountByStructureAsync(Guid tenantId, Guid structureId, Guid? excludeUnitId = null)
    {
        var query = _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Where(ou => ou.TenantId == tenantId &&
                         ou.ParentUnitId == null &&
                         ou.OrganizationLevel.StructureId == structureId &&
                         !ou.IsDeleted);

        if (excludeUnitId.HasValue)
        {
            query = query.Where(ou => ou.Id != excludeUnitId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<IEnumerable<OrganizationUnit>> GetByParentOrderedAsync(Guid? parentUnitId, Guid tenantId)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Include(ou => ou.HeadEmployee)
            .Where(ou => ou.ParentUnitId == parentUnitId && ou.TenantId == tenantId && !ou.IsDeleted)
            .OrderBy(ou => ou.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationUnit>> SearchAsync(Guid tenantId, string searchTerm)
    {
        var lowerSearchTerm = searchTerm.ToLower();

        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Include(ou => ou.ParentUnit)
            .Include(ou => ou.HeadEmployee)
            .Where(ou => ou.TenantId == tenantId && 
                        (ou.Name.ToLower().Contains(lowerSearchTerm) || 
                         ou.Code.ToLower().Contains(lowerSearchTerm)) && 
                        !ou.IsDeleted)
            .OrderBy(ou => ou.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationUnit>> GetActiveUnitsAsync(Guid tenantId)
    {
        return await _context.Set<OrganizationUnit>()
            .Include(ou => ou.OrganizationLevel)
            .Include(ou => ou.HeadEmployee)
            .Where(ou => ou.TenantId == tenantId && ou.IsActive && !ou.IsDeleted)
            .OrderBy(ou => ou.Name)
            .ToListAsync();
    }
}

#endregion

#region Organization Unit History Repository

public class OrganizationUnitHistoryRepository : GenericRepository<OrganizationUnitHistory>, IOrganizationUnitHistoryRepository
{
    public OrganizationUnitHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrganizationUnitHistory>> GetByUnitIdAsync(Guid unitId)
    {
        return await _context.Set<OrganizationUnitHistory>()
            .Include(ouh => ouh.OrganizationUnit)
            .Where(ouh => ouh.OrganizationUnitId == unitId && !ouh.IsDeleted)
            .OrderByDescending(ouh => ouh.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationUnitHistory>> GetByUnitIdAndDateRangeAsync(Guid unitId, DateOnly startDate, DateOnly endDate)
    {
        return await _context.Set<OrganizationUnitHistory>()
            .Include(ouh => ouh.OrganizationUnit)
            .Where(ouh => ouh.OrganizationUnitId == unitId && 
                         ouh.EffectiveFrom >= startDate && 
                         ouh.EffectiveFrom <= endDate && 
                         !ouh.IsDeleted)
            .OrderByDescending(ouh => ouh.EffectiveFrom)
            .ToListAsync();
    }

    // GetLatestByUnitIdAsync / GetActiveHistoryAsync deleted in areas 19–23 slice 12 with the two
    // endpoints they served. See IOrganizationStructureRepositories for why.

    public async Task<IEnumerable<OrganizationUnitHistory>> GetByHeadEmployeeAsync(Guid employeeId)
    {
        return await _context.Set<OrganizationUnitHistory>()
            .Include(ouh => ouh.OrganizationUnit)
            .Where(ouh => (ouh.PreviousHeadEmployeeId == employeeId || ouh.NewHeadEmployeeId == employeeId) && !ouh.IsDeleted)
            .OrderByDescending(ouh => ouh.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationUnitHistory>> GetRestructureHistoryAsync(Guid tenantId, DateOnly startDate, DateOnly endDate)
    {
        return await _context.Set<OrganizationUnitHistory>()
            .Include(ouh => ouh.OrganizationUnit)
            .Where(ouh => ouh.TenantId == tenantId && 
                         ouh.EffectiveFrom >= startDate && 
                         ouh.EffectiveFrom <= endDate && 
                         !ouh.IsDeleted)
            .OrderByDescending(ouh => ouh.EffectiveFrom)
            .ToListAsync();
    }
}

#endregion
