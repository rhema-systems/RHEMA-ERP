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

        private async Task<IEnumerable<Employee>> GetEmployeesForMaintenanceAsync()
        {
            // Prefer explicit flag, but keep the department-code fallback for backward compatibility.
            return await WithBasicIncludes(BaseQuery())
                .Include(e => e.Location)
                .Where(e =>
                    e.IsActive &&
                    (e.StaffStatus == StaffStatus.Active || e.StaffStatus == StaffStatus.Probation) &&
                    (e.CanBeAssignedToMaintenance || (e.Department != null && e.Department.Code == "MAINT")))
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
            return await WithBasicIncludes(BaseQuery())
                .Where(e =>
                    e.IsActive &&
                    e.StaffStatus == StaffStatus.Active &&
                    (e.CanBeAssignedToMaintenance || (e.Department != null && e.Department.Code == "MAINT")))
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

        /// <summary>
        /// The original staff-number generator. <b>Superseded — do not call it for new employees.</b>
        /// </summary>
        /// <remarks>
        /// <para>⚠ <c>IStaffNumberService</c> is the way in now. This method hardcodes one format
        /// (<c>{year}{sequence:D4}</c>) for every register, which cannot express an organisation that
        /// numbers permanent staff as bare digits and contract staff with a prefix — and it is a
        /// max+1 scan, so it is not atomic under concurrent creates.</para>
        ///
        /// <para>Kept because it is still reachable through <c>IEmployeeService</c> and removing it is
        /// a separate change; both callers that mattered (employee create and the recruitment hire
        /// path) now go through the register's rule instead.</para>
        /// </remarks>
        public async Task<string> GenerateEmployeeNumberAsync()
        {
            var currentYear = DateTime.UtcNow.Year.ToString();

            // ⚠ Scans EVERY row, tombstones included — deliberately, and NOT through BaseQuery().
            //
            // BaseQuery() filters `!IsDeleted`, while IX_Employee_Tenant_EmployeeNumber is NOT
            // filtered and therefore still holds the numbers of soft-deleted employees. Scanning
            // only live rows handed the newest deleted employee's number straight back to the next
            // create, which the index then rejected — as a 500, from SaveChanges, on the most
            // ordinary path in HR.
            //
            // It is deterministic, not a race: delete the most recently created employee and NO
            // further employee can be created for the rest of the calendar year. Reproduced
            // 2026-09-01 by a probe that created one employee and deleted it; the next create broke.
            //
            // This makes the scan agree with the index. It does not make it atomic — two
            // simultaneous creates can still pick the same number. The durable fix is
            // INumberSequenceService, which is atomic per tenant and is lane 3b's work.
            var maxEmployeeNumber = await _dbSet
                .AsNoTracking()
                .IgnoreQueryFilters()
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
    }

    public class EmployeeContractDetailRepository
        : GenericRepository<EmployeeContractDetail>, IEmployeeContractDetailRepository
    {
        public EmployeeContractDetailRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<EmployeeContractDetail>> GetByEmployeeAsync(Guid employeeId)
            => await _dbSet
                .Where(c => c.EmployeeId == employeeId && !c.IsDeleted)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

        public async Task<EmployeeContractDetail?> GetActiveContractAsync(Guid employeeId)
            => await _dbSet
                .Where(c => c.EmployeeId == employeeId && !c.IsDeleted
                         && (c.EndDate == null || c.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow)))
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();

        public async Task<IEnumerable<EmployeeContractDetail>> GetExpiringContractsAsync(DateTime withinDate)
        {
            var threshold = DateOnly.FromDateTime(withinDate);
            return await _dbSet
                .Where(c => !c.IsDeleted && c.EndDate != null && c.EndDate <= threshold)
                .ToListAsync();
        }

        public async Task<EmployeeContractDetail?> GetByContractNumberAsync(string contractNumber)
            => await _dbSet.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber && !c.IsDeleted);

        public async Task<bool> ContractNumberExistsAsync(string contractNumber)
            => await _dbSet.AnyAsync(c => c.ContractNumber == contractNumber && !c.IsDeleted);
    }
}
