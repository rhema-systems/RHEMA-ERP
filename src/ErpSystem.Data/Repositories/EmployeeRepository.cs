using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories
{
    public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
    {
        public EmployeeRepository(ApplicationDbContext context) : base(context)
        {
        }

        private IQueryable<Employee> BaseQuery(bool asNoTracking = true)
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();
            return query.Where(e => !e.IsDeleted);
        }

        private IQueryable<Employee> WithBasicIncludes(IQueryable<Employee> query)
        {
            return query
                .Include(e => e.Department)
                .Include(e => e.Position)
                    .ThenInclude(p => p.StaffLevel)
                .Include(e => e.Section)
                .Include(e => e.Manager);
        }

        private IQueryable<Employee> WithDetailsIncludes(IQueryable<Employee> query)
        {
            return WithBasicIncludes(query)
                .Include(e => e.OrganizationLevel)
                .Include(e => e.OrganizationUnit)
                .Include(e => e.LocationLevel)
                .Include(e => e.Location)
                .Include(e => e.Country)
                .Include(e => e.Shift)
                .Include(e => e.EmergencyContacts)
                .Include(e => e.Dependents)
                .Include(e => e.Qualifications)
                .Include(e => e.ContractDetails)
                .Include(e => e.Skills)
                    .ThenInclude(es => es.Skill);
        }

        private IQueryable<Employee> WithFullProfileIncludes(IQueryable<Employee> query)
        {
            return WithBasicIncludes(query)
                .Include(e => e.Country)
                .Include(e => e.Shift)
                .Include(e => e.LocationLevel)
                .Include(e => e.Location)
                .Include(e => e.OrganizationLevel)
                .Include(e => e.OrganizationUnit)

                // Contacts & family
                .Include(e => e.EmergencyContacts)
                    .ThenInclude(ec => ec.Country)
                .Include(e => e.Dependents)
                    .ThenInclude(d => d.EmployeeDependentBenefits)
                        .ThenInclude(db => db.BenefitPolicy)

                // Qualifications & skills
                .Include(e => e.Qualifications)
                    .ThenInclude(q => q.Qualification)
                .Include(e => e.Qualifications)
                    .ThenInclude(q => q.Country)
                .Include(e => e.Skills)
                    .ThenInclude(es => es.Skill)

                // Documents & history
                .Include(e => e.IdentificationCards)
                    .ThenInclude(ic => ic.IdentificationType)
                .Include(e => e.WorkHistories)

                // Contracts & expatriate assignments
                .Include(e => e.ContractDetails)
                .Include(e => e.ExpatriateAssignments)
                    .ThenInclude(a => a.Country)

                // Career & compensation
                .Include(e => e.PositionHistories)
                    .ThenInclude(ph => ph.Position)
                .Include(e => e.PositionHistories)
                    .ThenInclude(ph => ph.LocationLevel)
                .Include(e => e.PositionHistories)
                    .ThenInclude(ph => ph.Location)
                .Include(e => e.PositionHistories)
                    .ThenInclude(ph => ph.OrganizationLevel)
                .Include(e => e.PositionHistories)
                    .ThenInclude(ph => ph.OrganizationUnit)
                .Include(e => e.SalaryAssignments)
                    .ThenInclude(sa => sa.Grade)
                .Include(e => e.SalaryAssignments)
                    .ThenInclude(sa => sa.Level)
                .Include(e => e.SalaryAssignments)
                    .ThenInclude(sa => sa.Notch)

                // References & guarantors
                .Include(e => e.Referees)
                .Include(e => e.Guarantors)
                    .ThenInclude(g => g.Country)
                .Include(e => e.Guarantors)
                    .ThenInclude(g => g.VerifiedByEmployee);
        }

        public async Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber)
        {
            return await WithBasicIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.EmployeeNumber == employeeNumber);
        }

        public async Task<Employee?> GetByEmailAsync(string email)
        {
            return await WithBasicIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.EmailAddress == email);
        }

        public async Task<Employee?> GetByEmployeeNumberWithDetailsAsync(string employeeNumber)
        {
            return await WithDetailsIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.EmployeeNumber == employeeNumber);
        }

        public async Task<Employee?> GetByEmployeeNumberWithFullProfileAsync(string employeeNumber)
        {
            return await WithFullProfileIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.EmployeeNumber == employeeNumber);
        }

        public async Task<IEnumerable<Employee>> GetByDepartmentAsync(Guid departmentId)
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.DepartmentId == departmentId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByOrganizationUnitAsync(Guid organizationUnitId)
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.OrganizationUnitId == organizationUnitId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByOrganizationLevelAsync(Guid organizationLevelId)
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.OrganizationLevelId == organizationLevelId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByPositionAsync(Guid positionId)
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.PositionId == positionId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByManagerAsync(Guid managerId)
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.ManagerId == managerId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByStatusAsync(StaffStatus status)
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.StaffStatus == status)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetActiveEmployeesAsync()
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.IsActive)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByEmploymentTypeAsync(EmploymentType employmentType)
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.EmploymentType == employmentType)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> SearchEmployeesAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetActiveEmployeesAsync();
            }

            var term = searchTerm.Trim();
            var like = $"%{term}%";

            return await WithBasicIncludes(BaseQuery())
                .Where(e =>
                    EF.Functions.Like(e.FirstName, like) ||
                    EF.Functions.Like(e.LastName, like) ||
                    EF.Functions.Like(e.EmployeeNumber, like) ||
                    EF.Functions.Like(e.EmailAddress, like) ||
                    EF.Functions.Like((e.FirstName + " " + e.LastName), like))
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<bool> EmployeeNumberExistsAsync(string employeeNumber)
        {
            return await _dbSet.AnyAsync(e => e.EmployeeNumber == employeeNumber && !e.IsDeleted);
        }

        public async Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid excludeEmployeeId)
        {
            return await _dbSet.AnyAsync(e =>
                e.EmployeeNumber == employeeNumber &&
                e.Id != excludeEmployeeId &&
                !e.IsDeleted);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _dbSet.AnyAsync(e => e.EmailAddress == email && !e.IsDeleted);
        }

        public async Task<bool> EmailExistsAsync(string email, Guid excludeEmployeeId)
        {
            return await _dbSet.AnyAsync(e =>
                e.EmailAddress == email &&
                e.Id != excludeEmployeeId &&
                !e.IsDeleted);
        }

        public Task<bool> BadgeNumberExistsAsync(string badgeNumber)
        {
            if (string.IsNullOrWhiteSpace(badgeNumber))
            {
                return Task.FromResult(false);
            }

            var value = badgeNumber.Trim();
            return _dbSet.AnyAsync(e => e.BadgeNumber != null && e.BadgeNumber == value && !e.IsDeleted);
        }

        public Task<bool> BadgeNumberExistsAsync(string badgeNumber, Guid excludeEmployeeId)
        {
            if (string.IsNullOrWhiteSpace(badgeNumber))
            {
                return Task.FromResult(false);
            }

            var value = badgeNumber.Trim();
            return _dbSet.AnyAsync(e =>
                e.BadgeNumber != null &&
                e.BadgeNumber == value &&
                e.Id != excludeEmployeeId &&
                !e.IsDeleted);
        }

        public Task<bool> TaxNumberExistsAsync(string taxNumber)
        {
            if (string.IsNullOrWhiteSpace(taxNumber))
            {
                return Task.FromResult(false);
            }

            var value = taxNumber.Trim();
            return _dbSet.AnyAsync(e => e.TaxNumber != null && e.TaxNumber == value && !e.IsDeleted);
        }

        public Task<bool> TaxNumberExistsAsync(string taxNumber, Guid excludeEmployeeId)
        {
            if (string.IsNullOrWhiteSpace(taxNumber))
            {
                return Task.FromResult(false);
            }

            var value = taxNumber.Trim();
            return _dbSet.AnyAsync(e =>
                e.TaxNumber != null &&
                e.TaxNumber == value &&
                e.Id != excludeEmployeeId &&
                !e.IsDeleted);
        }

        public Task<bool> SocialSecurityNumberExistsAsync(string socialSecurityNumber)
        {
            if (string.IsNullOrWhiteSpace(socialSecurityNumber))
            {
                return Task.FromResult(false);
            }

            var value = socialSecurityNumber.Trim();
            return _dbSet.AnyAsync(e => e.SocialSecurityNumber != null && e.SocialSecurityNumber == value && !e.IsDeleted);
        }

        public Task<bool> SocialSecurityNumberExistsAsync(string socialSecurityNumber, Guid excludeEmployeeId)
        {
            if (string.IsNullOrWhiteSpace(socialSecurityNumber))
            {
                return Task.FromResult(false);
            }

            var value = socialSecurityNumber.Trim();
            return _dbSet.AnyAsync(e =>
                e.SocialSecurityNumber != null &&
                e.SocialSecurityNumber == value &&
                e.Id != excludeEmployeeId &&
                !e.IsDeleted);
        }

        public Task<bool> TinNumberExistsAsync(string tinNumber)
        {
            if (string.IsNullOrWhiteSpace(tinNumber))
            {
                return Task.FromResult(false);
            }

            var value = tinNumber.Trim();
            return _dbSet.AnyAsync(e => e.TINNumber != null && e.TINNumber == value && !e.IsDeleted);
        }

        public Task<bool> TinNumberExistsAsync(string tinNumber, Guid excludeEmployeeId)
        {
            if (string.IsNullOrWhiteSpace(tinNumber))
            {
                return Task.FromResult(false);
            }

            var value = tinNumber.Trim();
            return _dbSet.AnyAsync(e =>
                e.TINNumber != null &&
                e.TINNumber == value &&
                e.Id != excludeEmployeeId &&
                !e.IsDeleted);
        }

        public async Task<Employee?> GetByIdWithDetailsAsync(Guid id)
        {
            return await WithDetailsIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Employee?> GetByIdWithFullProfileAsync(Guid id)
        {
            return await WithFullProfileIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Employee?> GetByBadgeNumberAsync(string badgeNumber)
        {
            if (string.IsNullOrWhiteSpace(badgeNumber))
            {
                return null;
            }

            var value = badgeNumber.Trim();
            return await WithBasicIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.BadgeNumber != null && e.BadgeNumber == value);
        }

        public async Task<Employee?> GetByTaxNumberAsync(string taxNumber)
        {
            if (string.IsNullOrWhiteSpace(taxNumber))
            {
                return null;
            }

            var value = taxNumber.Trim();
            return await WithBasicIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.TaxNumber != null && e.TaxNumber == value);
        }

        public async Task<Employee?> GetBySocialSecurityNumberAsync(string socialSecurityNumber)
        {
            if (string.IsNullOrWhiteSpace(socialSecurityNumber))
            {
                return null;
            }

            var value = socialSecurityNumber.Trim();
            return await WithBasicIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.SocialSecurityNumber != null && e.SocialSecurityNumber == value);
        }

        public async Task<Employee?> GetByTinNumberAsync(string tinNumber)
        {
            if (string.IsNullOrWhiteSpace(tinNumber))
            {
                return null;
            }

            var value = tinNumber.Trim();
            return await WithBasicIncludes(BaseQuery())
                .FirstOrDefaultAsync(e => e.TINNumber != null && e.TINNumber == value);
        }

        public async Task<IEnumerable<Employee>> GetBySectionAsync(Guid sectionId)
        {
            return await WithBasicIncludes(BaseQuery())
                .Where(e => e.SectionId == sectionId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetEmployeesBySkillAsync(Guid skillId, SkillLevel? minLevel = null)
        {
            var query = WithBasicIncludes(BaseQuery())
                .Include(e => e.Skills)
                    .ThenInclude(es => es.Skill)
                .Where(e => e.IsActive && e.Skills.Any(es => es.SkillId == skillId));

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

        public async Task<string> GenerateEmployeeNumberAsync()
        {
            var currentYear = DateTime.UtcNow.Year.ToString();

            // EmployeeNumber is expected to be formatted as YYYY#### (zero-padded), so string Max works.
            var maxEmployeeNumber = await BaseQuery()
                .Where(e => e.EmployeeNumber.StartsWith(currentYear))
                .Select(e => e.EmployeeNumber)
                .DefaultIfEmpty()
                .MaxAsync();

            if (string.IsNullOrWhiteSpace(maxEmployeeNumber))
            {
                return $"{currentYear}0001";
            }

            var seqPart = maxEmployeeNumber.Length > 4 ? maxEmployeeNumber.Substring(4) : "0";
            var nextSequence = int.TryParse(seqPart, out var seq) ? seq + 1 : 1;
            return $"{currentYear}{nextSequence:D4}";
        }

        public async Task<Employee?> GetEmployeeWithPositionHistoryAsync(Guid employeeId)
        {
            return await WithBasicIncludes(BaseQuery())
                .Include(e => e.PositionHistories)
                    .ThenInclude(ph => ph.Position)
                .FirstOrDefaultAsync(e => e.Id == employeeId);
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

        public async Task<Employee?> GetByApplicationUserIdAsync(Guid applicationUserId)
        {
            // Query ApplicationUser table via the context's IdentityUsers table
            var context = _context as ApplicationDbContext;
            if (context == null)
            {
                return null;
            }

            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == applicationUserId);
            if (user?.EmployeeId == null)
            {
                return null;
            }

            return await GetByIdWithDetailsAsync(user.EmployeeId.Value);
        }

        public async Task<IEnumerable<Employee>> GetByStationAsync(Guid stationId)
        {
            return await _dbSet
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Include(e => e.Manager)
                .Where(e => e.StationId == stationId && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByDivisionAsync(Guid divisionId)
        {
            return await _dbSet
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Include(e => e.Manager)
                .Where(e => e.DivisionId == divisionId && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetByUnitAsync(Guid unitId)
        {
            return await _dbSet
                .Include(e => e.Position)
                .Include(e => e.Section)
                .Include(e => e.Manager)
                .Where(e => e.UnitId == unitId && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

    }
}
