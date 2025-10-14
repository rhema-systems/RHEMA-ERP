using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for technician operations with HR module integration
/// </summary>
public class TechnicianRepository : GenericRepository<Employee>, ITechnicianRepository
{
    public TechnicianRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Employee>> GetTechniciansAsync()
    {
        return await _context.Employees
            .Where(e => e.Department != null && e.Department.Name.Contains("Maintenance") && 
                       e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Employee>> GetActiveTechniciansAsync()
    {
        return await _context.Employees
            .Where(e => e.Department != null && e.Department.Name.Contains("Maintenance") && 
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
            .Where(e => e.Department != null && e.Department.Name == department && 
                       e.Department.Name.Contains("Maintenance") && 
                       !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetBySpecializationAsync(string specialization)
    {
        return await _context.Employees
            .Where(e => e.Specialization == specialization && 
                       e.Department != null && e.Department.Name.Contains("Maintenance") && 
                       !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Employee>> GetAvailableAsync(DateTime startTime, DateTime endTime)
    {
        // Basic implementation - in a real system this would check scheduling/availability
        return await _context.Employees
            .Where(e => e.Department != null && e.Department.Name.Contains("Maintenance") && 
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
            .Where(e => e != null && e.Department != null && e.Department.Name.Contains("Maintenance"))
            .Cast<Employee>()
            .ToListAsync();
    }
    
    public async Task<Employee?> GetTechnicianWithSkillsAsync(Guid technicianId)
    {
        return await _context.Employees
            .Where(e => e.Id == technicianId && e.Department != null && 
                       e.Department.Name.Contains("Maintenance") && !e.IsDeleted)
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
        return await _context.TechnicianTeamMembers
            .Where(ttm => ttm.TeamId == teamId && !ttm.IsDeleted)
            .Select(ttm => ttm.Technician)
            .Where(e => e != null)
            .Cast<Employee>()
            .ToListAsync();
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
        // Basic implementation - filter by department containing location info
        return await _context.Employees
            .Where(e => e.Department != null && e.Department.Name.Contains("Maintenance") && 
                       e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.FirstName)
            .ToListAsync();
    }
    
    public async Task<Employee?> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Employees
            .Where(e => e.Id == employeeId && e.Department != null && 
                       e.Department.Name.Contains("Maintenance") && !e.IsDeleted)
            .FirstOrDefaultAsync();
    }
    
    public async Task<IEnumerable<Employee>> GetFromHRModuleAsync()
    {
        // Mock data for HR module integration
        var mockEmployees = new List<Employee>
        {
            new Employee
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                FirstName = "John",
                LastName = "Smith",
                EmailAddress = "john.smith@company.com",
                MobileNumber = "555-0101",
                DepartmentId = Guid.Parse("33333333-3333-3333-3333-333333333333"), // Mock Department ID
                PositionId = Guid.Parse("44444444-4444-4444-4444-444444444444"), // Mock Position ID
                Specialization = "Pumps & Motors",
                DateEmployed = DateOnly.Parse("2020-01-15"),
                IsActive = true,
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                CreatedAt = DateTime.UtcNow
            },
            new Employee
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                FirstName = "Sarah",
                LastName = "Johnson",
                EmailAddress = "sarah.johnson@company.com",
                MobileNumber = "555-0102",
                DepartmentId = Guid.Parse("33333333-3333-3333-3333-333333333333"), // Mock Department ID
                PositionId = Guid.Parse("55555555-5555-5555-5555-555555555555"), // Mock Position ID
                Specialization = "Control Systems",
                DateEmployed = DateOnly.Parse("2021-03-10"),
                IsActive = true,
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                CreatedAt = DateTime.UtcNow
            }
        };

        await Task.Delay(100); // Simulate API call delay
        return mockEmployees;
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
                existingEmployee.DepartmentId = hrEmployee.DepartmentId;
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
