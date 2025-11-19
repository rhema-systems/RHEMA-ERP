using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories
{
    public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
    {
        public EmployeeRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber)
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.EmployeeNumber == employeeNumber && !e.IsDeleted);
        }

        public async Task<Employee?> GetByEmailAsync(string email)
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.EmailAddress == email && !e.IsDeleted);
        }

        public async Task<IEnumerable<Employee>> GetByDepartmentAsync(Guid departmentId)
        {
            return await _dbSet
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Include(e => e.Manager)
                .Where(e => e.DepartmentId == departmentId && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByPositionAsync(Guid positionId)
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Section)
                .Where(e => e.PositionId == positionId && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByManagerAsync(Guid managerId)
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Where(e => e.ManagerId == managerId && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByStatusAsync(StaffStatus status)
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Where(e => e.StaffStatus == status && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetActiveEmployeesAsync()
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Where(e => e.IsActive && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetEmployeesForMaintenanceAsync()
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Where(e => e.IsActive && 
                           !e.IsDeleted && 
                           (e.StaffStatus == StaffStatus.Active || e.StaffStatus == StaffStatus.Probation) &&
                           e.Department.DepartmentType == DepartmentType.Maintenance)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> SearchEmployeesAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetActiveEmployeesAsync();

            searchTerm = searchTerm.ToLower();

            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Where(e => !e.IsDeleted && (
                    e.FirstName.ToLower().Contains(searchTerm) ||
                    e.LastName.ToLower().Contains(searchTerm) ||
                    e.EmployeeNumber.ToLower().Contains(searchTerm) ||
                    e.EmailAddress.ToLower().Contains(searchTerm) ||
                    (e.FirstName + " " + e.LastName).ToLower().Contains(searchTerm)))
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<bool> EmployeeNumberExistsAsync(string employeeNumber)
        {
            return await _dbSet.AnyAsync(e => e.EmployeeNumber == employeeNumber && !e.IsDeleted);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _dbSet.AnyAsync(e => e.EmailAddress == email && !e.IsDeleted);
        }

        public async Task<Employee?> GetByIdWithDetailsAsync(Guid id)
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }

        public async Task<IEnumerable<Employee>> GetBySectionAsync(Guid sectionId)
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Include(e => e.Manager)
                .Where(e => e.SectionId == sectionId && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetMaintenanceTechniciansAsync()
        {
            return await GetEmployeesForMaintenanceAsync();
        }

        public async Task<IEnumerable<Employee>> GetAvailableTechniciansAsync()
        {
            return await _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Where(e => e.IsActive && 
                           !e.IsDeleted && 
                           e.StaffStatus == StaffStatus.Active &&
                           e.Department.DepartmentType == DepartmentType.Maintenance)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetEmployeesBySkillAsync(Guid skillId, SkillLevel? minLevel = null)
        {
            var query = _dbSet
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Include(e => e.Skills)
                    .ThenInclude(es => es.Skill)
                .Where(e => !e.IsDeleted && e.IsActive &&
                           e.Skills.Any(es => es.SkillId == skillId));

            if (minLevel.HasValue)
            {
                query = query.Where(e => e.Skills
                    .Any(es => es.SkillId == skillId && es.SkillLevel >= minLevel.Value));
            }

            return await query
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<Employee?> GetByApplicationUserIdAsync(Guid applicationUserId)
        {
            // Query ApplicationUser table via the context's IdentityUsers table
            var context = _context as ApplicationDbContext;
            if (context == null)
                return null;
                
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == applicationUserId);
            if (user?.EmployeeId == null)
                return null;
            
            return await GetByIdWithDetailsAsync(user.EmployeeId.Value);
        }

        public async Task<string> GenerateEmployeeNumberAsync()
        {
            var currentYear = DateTime.Now.Year.ToString();
            
            // Get all employee numbers for the current year to find max sequence
            var employeeNumbersInYear = await _dbSet
                .Where(e => e.EmployeeNumber.StartsWith(currentYear) && !e.IsDeleted)
                .Select(e => e.EmployeeNumber)
                .ToListAsync();

            int nextSequence = 1;
            if (employeeNumbersInYear.Any())
            {
                // Parse all sequence numbers and find the maximum
                var maxSequence = employeeNumbersInYear
                    .Select(empNum => {
                        if (empNum.Length > 4 && int.TryParse(empNum.Substring(4), out var seq))
                            return seq;
                        return 0;
                    })
                    .Max();
                
                nextSequence = maxSequence + 1;
            }

            return $"{currentYear}{nextSequence:D4}";
        }
    }
}
