using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

#region Location Structure Repository

public class LocationStructureRepository : GenericRepository<LocationStructure>, ILocationStructureRepository
{
    public LocationStructureRepository(ApplicationDbContext context) : base(context) { }

    public async Task<LocationStructure?> GetWithLevelsAsync(Guid id)
    {
        return await _context.Set<LocationStructure>()
            .Include(ls => ls.LocationLevels.OrderBy(l => l.LevelNumber))
            .FirstOrDefaultAsync(ls => ls.Id == id && !ls.IsDeleted);
    }

    public async Task<LocationStructure?> GetDefaultStructureAsync(Guid tenantId)
    {
        return await _context.Set<LocationStructure>()
            .Include(ls => ls.LocationLevels.OrderBy(l => l.LevelNumber))
            .FirstOrDefaultAsync(ls => ls.TenantId == tenantId && ls.IsDefault && !ls.IsDeleted);
    }

    public async Task<LocationStructure?> GetByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Set<LocationStructure>()
            .FirstOrDefaultAsync(ls => ls.TenantId == tenantId && ls.Code == code && !ls.IsDeleted);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string name, Guid? excludeId = null)
    {
        var query = _context.Set<LocationStructure>()
            .Where(ls => ls.TenantId == tenantId && ls.Name == name && !ls.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ls => ls.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null)
    {
        var query = _context.Set<LocationStructure>()
            .Where(ls => ls.Name == name && !ls.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ls => ls.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null)
    {
        var query = _context.Set<LocationStructure>()
            .Where(ls => ls.TenantId == tenantId && ls.Code == code && !ls.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ls => ls.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null)
    {
        var query = _context.Set<LocationStructure>()
            .Where(ls => ls.Code == code && !ls.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ls => ls.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<LocationStructure>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<LocationStructure>()
            .Where(ls => ls.TenantId == tenantId && !ls.IsDeleted)
            .OrderBy(ls => ls.Name)
            .ToListAsync();
    }

    public async Task<LocationStructure?> GetWithFullDetailsAsync(Guid id)
    {
        return await _context.Set<LocationStructure>()
            .Include(ls => ls.LocationLevels.OrderBy(l => l.LevelNumber))
                .ThenInclude(l => l.Locations.Where(loc => !loc.IsDeleted))
            .Include(ls => ls.Locations.Where(loc => !loc.IsDeleted))
            .FirstOrDefaultAsync(ls => ls.Id == id && !ls.IsDeleted);
    }
}

#endregion

#region Location Level Repository

public class LocationLevelRepository : GenericRepository<LocationLevel>, ILocationLevelRepository
{
    public LocationLevelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LocationLevel>> GetByStructureIdAsync(Guid structureId)
    {
        return await _context.Set<LocationLevel>()
            .Include(ll => ll.Structure)
            .Where(ll => ll.StructureId == structureId && !ll.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<LocationLevel>> GetByStructureIdOrderedAsync(Guid structureId)
    {
        return await _context.Set<LocationLevel>()
            .Include(ll => ll.Structure)
            .Where(ll => ll.StructureId == structureId && !ll.IsDeleted)
            .OrderBy(ll => ll.LevelNumber)
            .ToListAsync();
    }

    public async Task<LocationLevel?> GetByStructureAndLevelNumberAsync(Guid structureId, int levelNumber)
    {
        return await _context.Set<LocationLevel>()
            .Include(ll => ll.Structure)
            .FirstOrDefaultAsync(ll => ll.StructureId == structureId && ll.LevelNumber == levelNumber && !ll.IsDeleted);
    }

    public async Task<LocationLevel?> GetRootLevelAsync(Guid structureId)
    {
        // Get all levels for the structure to compute root level dynamically
        var levels = await _context.Set<LocationLevel>()
            .Include(ll => ll.Structure)
                .ThenInclude(s => s.LocationLevels.Where(l => l.IsActive && !l.IsDeleted))
            .Where(ll => ll.StructureId == structureId && ll.IsActive && !ll.IsDeleted)
            .ToListAsync();

        if (!levels.Any())
            return null;

        var minLevelNumber = levels.Min(l => l.LevelNumber);
        return levels.FirstOrDefault(l => l.LevelNumber == minLevelNumber);
    }

    public async Task<bool> ExistsByNameAsync(Guid structureId, string name, Guid? excludeId = null)
    {
        var query = _context.Set<LocationLevel>()
            .Where(ll => ll.StructureId == structureId && ll.Name == name && !ll.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ll => ll.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByCodeAsync(Guid structureId, string code, Guid? excludeId = null)
    {
        var query = _context.Set<LocationLevel>()
            .Where(ll => ll.StructureId == structureId && ll.Code == code && !ll.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ll => ll.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> LevelNumberExistsAsync(Guid structureId, int levelNumber, Guid? excludeId = null)
    {
        var query = _context.Set<LocationLevel>()
            .Where(ll => ll.StructureId == structureId && ll.LevelNumber == levelNumber && !ll.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(ll => ll.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<LocationLevel?> GetWithLocationsAsync(Guid id)
    {
        return await _context.Set<LocationLevel>()
            .Include(ll => ll.Structure)
            .Include(ll => ll.Locations.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(ll => ll.Id == id && !ll.IsDeleted);
    }

    public async Task<int> GetLocationCountAsync(Guid levelId)
    {
        return await _context.Set<Location>()
            .CountAsync(l => l.LocationLevelId == levelId && !l.IsDeleted);
    }
}

#endregion

#region Location Repository

public class LocationRepository : GenericRepository<Location>, ILocationRepository
{
    public LocationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Location?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<Location>()
            .Include(l => l.Structure)
            .Include(l => l.LocationLevel)
            .Include(l => l.ParentLocation)
            .Include(l => l.Country)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
    }

    public async Task<Location?> GetWithFullHierarchyAsync(Guid id)
    {
        return await _context.Set<Location>()
            .Include(l => l.Structure)
            .Include(l => l.LocationLevel)
            .Include(l => l.ParentLocation)
            .Include(l => l.Country)
            .Include(l => l.ChildLocations.Where(c => !c.IsDeleted))
            .Include(l => l.LocationContacts.Where(c => !c.IsDeleted))
            .Include(l => l.Employees.Where(e => !e.IsDeleted))
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
    }

    public async Task<IEnumerable<Location>> GetByLevelIdAsync(Guid levelId)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.ParentLocation)
            .Include(l => l.Country)
            .Where(l => l.LocationLevelId == levelId && !l.IsDeleted)
            .OrderBy(l => l.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> GetChildLocationsAsync(Guid parentLocationId)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.Country)
            .Where(l => l.ParentLocationId == parentLocationId && !l.IsDeleted)
            .OrderBy(l => l.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> GetRootLocationsAsync(Guid structureId)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.Country)
            .Where(l => l.StructureId == structureId && l.ParentLocationId == null && !l.IsDeleted)
            .OrderBy(l => l.Sequence)
            .ToListAsync();
    }

    public async Task<Location?> GetByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Code == code && !l.IsDeleted);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null)
    {
        var query = _context.Set<Location>()
            .Where(l => l.TenantId == tenantId && l.Code == code && !l.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(l => l.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByNameInLevelAsync(Guid levelId, string name, Guid? excludeId = null)
    {
        var query = _context.Set<Location>()
            .Where(l => l.LocationLevelId == levelId && l.Name == name && !l.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(l => l.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<Location>> GetByStructureIdAsync(Guid structureId)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.ParentLocation)
            .Include(l => l.Country)
            .Where(l => l.StructureId == structureId && !l.IsDeleted)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> GetHierarchyTreeAsync(Guid structureId)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.Country)
            .Include(l => l.ChildLocations.Where(c => !c.IsDeleted))
            .Where(l => l.StructureId == structureId && !l.IsDeleted)
            .OrderBy(l => l.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> GetDescendantsAsync(Guid locationId)
    {
        var descendants = new List<Location>();
        var childLocations = await GetChildLocationsAsync(locationId);

        foreach (var child in childLocations)
        {
            descendants.Add(child);
            var childDescendants = await GetDescendantsAsync(child.Id);
            descendants.AddRange(childDescendants);
        }

        return descendants;
    }

    public async Task<IEnumerable<Location>> GetAncestorsAsync(Guid locationId)
    {
        var ancestors = new List<Location>();
        var location = await GetByIdAsync(locationId);

        while (location?.ParentLocationId != null)
        {
            var parent = await GetByIdAsync(location.ParentLocationId.Value);
            if (parent != null)
            {
                ancestors.Add(parent);
                location = parent;
            }
            else
            {
                break;
            }
        }

        return ancestors;
    }

    public async Task<int> GetEmployeeCountAsync(Guid locationId)
    {
        return await _context.Set<Employee>()
            .CountAsync(e => e.LocationId == locationId && !e.IsDeleted);
    }

    public async Task<int> GetChildCountAsync(Guid locationId)
    {
        return await _context.Set<Location>()
            .CountAsync(l => l.ParentLocationId == locationId && !l.IsDeleted);
    }

    public async Task<int> GetActiveChildCountAsync(Guid locationId)
    {
        return await _context.Set<Location>()
            .CountAsync(l => l.ParentLocationId == locationId && l.IsActive && !l.IsDeleted);
    }

    public async Task<int> GetRootLocationCountAsync(Guid structureId, Guid? excludeLocationId = null)
    {
        var query = _context.Set<Location>()
            .Where(l => l.StructureId == structureId && l.ParentLocationId == null && !l.IsDeleted);

        if (excludeLocationId.HasValue)
        {
            query = query.Where(l => l.Id != excludeLocationId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<int> GetContactCountAsync(Guid locationId)
    {
        return await _context.Set<LocationContact>()
            .CountAsync(lc => lc.LocationId == locationId && !lc.IsDeleted);
    }

    public async Task<IEnumerable<Location>> GetByParentOrderedAsync(Guid? parentLocationId, Guid structureId)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.Country)
            .Where(l => l.ParentLocationId == parentLocationId && l.StructureId == structureId && !l.IsDeleted)
            .OrderBy(l => l.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> GetByCountryAsync(Guid countryId)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.Country)
            .Where(l => l.CountryId == countryId && !l.IsDeleted)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> SearchAsync(Guid tenantId, string searchTerm)
    {
        var lowerSearchTerm = searchTerm.ToLower();

        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.ParentLocation)
            .Include(l => l.Country)
            .Where(l => l.TenantId == tenantId && 
                       (l.Name.ToLower().Contains(lowerSearchTerm) || 
                        l.Code.ToLower().Contains(lowerSearchTerm) || 
                        (l.City != null && l.City.ToLower().Contains(lowerSearchTerm))) && 
                       !l.IsDeleted)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> GetActiveLocationsAsync(Guid tenantId)
    {
        return await _context.Set<Location>()
            .Include(l => l.LocationLevel)
            .Include(l => l.Country)
            .Where(l => l.TenantId == tenantId && l.IsActive && !l.IsDeleted)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    public async Task<Location?> GetWithContactsAsync(Guid id)
    {
        return await _context.Set<Location>()
            .Include(l => l.Structure)
            .Include(l => l.LocationLevel)
            .Include(l => l.ParentLocation)
            .Include(l => l.Country)
            .Include(l => l.LocationContacts.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Employee)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
    }
}

#endregion

#region Location Contact Repository

public class LocationContactRepository : GenericRepository<LocationContact>, ILocationContactRepository
{
    public LocationContactRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LocationContact>> GetByLocationIdAsync(Guid locationId)
    {
        return await _context.Set<LocationContact>()
            .Include(lc => lc.Location)
            .Include(lc => lc.Employee)
            .Where(lc => lc.LocationId == locationId && !lc.IsDeleted)
            .OrderByDescending(lc => lc.IsPrimary)
            .ThenBy(lc => lc.ContactName)
            .ToListAsync();
    }

    public async Task<LocationContact?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<LocationContact>()
            .Include(lc => lc.Location)
                .ThenInclude(l => l.LocationLevel)
            .Include(lc => lc.Employee)
            .FirstOrDefaultAsync(lc => lc.Id == id && !lc.IsDeleted);
    }

    public async Task<LocationContact?> GetPrimaryContactAsync(Guid locationId)
    {
        return await _context.Set<LocationContact>()
            .Include(lc => lc.Location)
            .Include(lc => lc.Employee)
            .FirstOrDefaultAsync(lc => lc.LocationId == locationId && lc.IsPrimary && !lc.IsDeleted);
    }

    public async Task<IEnumerable<LocationContact>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Set<LocationContact>()
            .Include(lc => lc.Location)
            .Include(lc => lc.Employee)
            .Where(lc => lc.EmployeeId == employeeId && !lc.IsDeleted)
            .ToListAsync();
    }

    public async Task<bool> HasPrimaryContactAsync(Guid locationId, Guid? excludeId = null)
    {
        var query = _context.Set<LocationContact>()
            .Where(lc => lc.LocationId == locationId && lc.IsPrimary && !lc.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(lc => lc.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task ClearPrimaryFlagsAsync(Guid locationId)
    {
        var contacts = await _context.Set<LocationContact>()
            .Where(lc => lc.LocationId == locationId && lc.IsPrimary && !lc.IsDeleted)
            .ToListAsync();

        foreach (var contact in contacts)
        {
            contact.IsPrimary = false;
        }

        await _context.SaveChangesAsync();
    }
}

#endregion
