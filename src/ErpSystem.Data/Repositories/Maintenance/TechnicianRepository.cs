using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for technician operations with HR module integration
/// </summary>
public class TechnicianRepository : GenericRepository<Employee>, ITechnicianRepository
{
    public TechnicianRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<Employee?> GetByIdAsync(Guid id)
    {
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
    }

    public async Task<IEnumerable<Employee>> GetTechniciansAsync()
    {
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .Where(e => e.OrganizationUnit != null && e.OrganizationUnit.Name.Contains("Maintenance") &&
                       e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetActiveTechniciansAsync()
    {
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .Where(e => e.OrganizationUnit != null && e.OrganizationUnit.Name.Contains("Maintenance") &&
                       e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetActiveAsync()
    {
        return await GetActiveTechniciansAsync();
    }

    public async Task<IEnumerable<Employee>> GetByDepartmentAsync(string department)
    {
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .Where(e => e.OrganizationUnit != null && e.OrganizationUnit.Name == department &&
                       e.OrganizationUnit.Name.Contains("Maintenance") &&
                       !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetBySpecializationAsync(string specialization)
    {
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .Where(e => e.Specialization == specialization &&
                       e.OrganizationUnit != null && e.OrganizationUnit.Name.Contains("Maintenance") &&
                       !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetAvailableAsync(DateTime startTime, DateTime endTime)
    {
        // Basic implementation - in a real system this would check scheduling/availability
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .Where(e => e.OrganizationUnit != null && e.OrganizationUnit.Name.Contains("Maintenance") &&
                       e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetAvailableTechniciansAsync(DateTime startTime, DateTime endTime)
    {
        return await GetAvailableAsync(startTime, endTime);
    }

    public async Task<IEnumerable<Employee>> GetTechniciansBySkillAsync(Guid skillId)
    {
        return await _context.TechnicianSkillAssignments
            .Where(tsa => tsa.SkillId == skillId && !tsa.IsDeleted)
            .Select(tsa => tsa.Technician)
            .Where(e => e != null && e.OrganizationUnit != null && e.OrganizationUnit.Name.Contains("Maintenance"))
            .Cast<Employee>()
            .ToListAsync();
    }

    public async Task<Employee?> GetTechnicianWithSkillsAsync(Guid technicianId)
    {
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .Where(e => e.Id == technicianId && e.OrganizationUnit != null &&
                       e.OrganizationUnit.Name.Contains("Maintenance") && !e.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsTechnicianAvailableAsync(Guid technicianId, DateTime startTime, DateTime endTime)
    {
        // Basic implementation - in a real system this would check work orders, schedules, etc.
        var technician = await GetByIdAsync(technicianId);
        return technician != null && technician.IsActive;
    }

    public async Task<IEnumerable<Employee>> GetTechniciansByTeamAsync(Guid teamId)
    {
        // TechnicianId now references ApplicationUser (Users table) instead of Employee
        // This method returns an empty list as technician info is now in Users table
        await Task.CompletedTask;
        return new List<Employee>();
    }

    public async Task<double> GetTechnicianWorkloadAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        // Basic implementation - count work orders in date range
        var workOrderCount = await _context.WorkOrders
            .Where(wo => wo.AssignedTechnicianId == technicianId &&
                        wo.CreatedAt >= startDate && wo.CreatedAt <= endDate &&
                        !wo.IsDeleted)
            .CountAsync();
        return workOrderCount;
    }

    public async Task<IEnumerable<Employee>> GetTechniciansByLocationAsync(Guid locationId)
    {
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .Where(e => e.OrganizationUnit != null && e.OrganizationUnit.Name.Contains("Maintenance") &&
                       e.LocationId == locationId &&
                       e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
    }

    public async Task<Employee?> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Employees
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Location)
            .Where(e => e.Id == employeeId && e.OrganizationUnit != null &&
                       e.OrganizationUnit.Name.Contains("Maintenance") && !e.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Employee>> GetFromHRModuleAsync()
    {
        // TODO: Implement actual HR module integration
        // For now, return empty collection until HR integration is implemented
        await Task.CompletedTask;
        return new List<Employee>();
    }

    public async Task<IEnumerable<Employee>> SyncFromHRAsync()
    {
        var hrEmployees = await GetFromHRModuleAsync();
        var syncedEmployees = new List<Employee>();

        foreach (var hrEmployee in hrEmployees)
        {
            var existingEmployee = await GetByIdAsync(hrEmployee.Id);
            if (existingEmployee != null)
            {
                existingEmployee.FirstName = hrEmployee.FirstName;
                existingEmployee.LastName = hrEmployee.LastName;
                existingEmployee.EmailAddress = hrEmployee.EmailAddress;
                existingEmployee.MobileNumber = hrEmployee.MobileNumber;
                existingEmployee.OrganizationUnitId = hrEmployee.OrganizationUnitId;
                existingEmployee.PositionId = hrEmployee.PositionId;
                existingEmployee.IsActive = hrEmployee.IsActive;
                existingEmployee.UpdatedAt = DateTime.UtcNow;

                await UpdateAsync(existingEmployee);
                syncedEmployees.Add(existingEmployee);
            }
            else
            {
                await AddAsync(hrEmployee);
                syncedEmployees.Add(hrEmployee);
            }
        }

        return syncedEmployees;
    }
}
