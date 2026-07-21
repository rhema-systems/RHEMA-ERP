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
    public class DepartmentRepository : GenericRepository<Department>, IDepartmentRepository
    {
        public DepartmentRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Department?> GetByCodeAsync(string code)
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.Division)
                .Include(d => d.Sections)
                .FirstOrDefaultAsync(d => d.Code == code && !d.IsDeleted);
        }

        // This method was replaced by GetByTypeAsync(DepartmentType departmentType) to match HR interface

        public async Task<IEnumerable<Department>> GetSubDepartmentsAsync(Guid parentDepartmentId)
        {
            // Department no longer has a parent/child hierarchy in the current model.
            return await Task.FromResult(Enumerable.Empty<Department>());
        }

        public async Task<IEnumerable<Department>> GetRootDepartmentsAsync()
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.Division)
                .Include(d => d.Sections)
                .Where(d => !d.IsDeleted)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Department>> GetActiveDepartmentsAsync()
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.Division)
                .Include(d => d.Sections)
                .Where(d => d.IsActive && !d.IsDeleted)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<Department?> GetWithEmployeesAsync(Guid id)
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.Division)
                .Include(d => d.Employees)
                    .ThenInclude(e => e.Position)
                .Include(d => d.Sections)
                    .ThenInclude(s => s.Employees)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
        }

        public async Task<bool> CodeExistsAsync(string code)
        {
            return await _dbSet.AnyAsync(d => d.Code == code && !d.IsDeleted);
        }

        public async Task<Department?> GetByTypeAsync(DepartmentType departmentType)
        {
            var code = departmentType switch
            {
                DepartmentType.Operations => "OPS",
                DepartmentType.Administration => "ADMIN",
                DepartmentType.HumanResources => "HR",
                DepartmentType.Finance => "FIN",
                DepartmentType.IT => "IT",
                DepartmentType.Maintenance => "MAINT",
                DepartmentType.Safety => "SAFE",
                DepartmentType.QualityAssurance => "QA",
                DepartmentType.RnD => "RND",
                DepartmentType.Marketing => "MKTG",
                DepartmentType.Sales => "SALES",
                _ => null
            };

            if (string.IsNullOrWhiteSpace(code))
                return null;

            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.Division)
                .FirstOrDefaultAsync(d => d.Code == code && !d.IsDeleted);
        }

        public async Task<IEnumerable<Department>> GetByTypeAsync(IEnumerable<DepartmentType> departmentTypes)
        {
            var codes = departmentTypes
                .Select(t => t switch
                {
                    DepartmentType.Operations => "OPS",
                    DepartmentType.Administration => "ADMIN",
                    DepartmentType.HumanResources => "HR",
                    DepartmentType.Finance => "FIN",
                    DepartmentType.IT => "IT",
                    DepartmentType.Maintenance => "MAINT",
                    DepartmentType.Safety => "SAFE",
                    DepartmentType.QualityAssurance => "QA",
                    DepartmentType.RnD => "RND",
                    DepartmentType.Marketing => "MKTG",
                    DepartmentType.Sales => "SALES",
                    _ => null
                })
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (codes.Count == 0)
                return Enumerable.Empty<Department>();

            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.Division)
                .Where(d => codes.Contains(d.Code) && !d.IsDeleted)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<bool> CanDeleteDepartmentAsync(Guid departmentId)
        {
            // Check if department has employees
            var hasEmployees = await _context.Set<Employee>()
                .AnyAsync(e => e.DepartmentId == departmentId && !e.IsDeleted);

            if (hasEmployees)
                return false;

            // Check if department has sections
            var hasSections = await _context.Set<Section>()
                .AnyAsync(s => s.DepartmentId == departmentId && !s.IsDeleted);

            return !hasSections;
        }
    }
}