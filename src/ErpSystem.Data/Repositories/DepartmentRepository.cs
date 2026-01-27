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
    public class DepartmentRepository : GenericRepository<Department>, IDepartmentRepository
    {
        public DepartmentRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Department?> GetByCodeAsync(string code)
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.ParentDepartment)
                .FirstOrDefaultAsync(d => d.Code == code && !d.IsDeleted);
        }

        // This method was replaced by GetByTypeAsync(DepartmentType departmentType) to match HR interface

        public async Task<IEnumerable<Department>> GetSubDepartmentsAsync(Guid parentDepartmentId)
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.Sections)
                .Where(d => d.ParentDepartmentId == parentDepartmentId && !d.IsDeleted)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Department>> GetRootDepartmentsAsync()
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.SubDepartments)
                .Where(d => d.ParentDepartmentId == null && !d.IsDeleted)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Department>> GetActiveDepartmentsAsync()
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.ParentDepartment)
                .Where(d => d.IsActive && !d.IsDeleted)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<Department?> GetWithEmployeesAsync(Guid id)
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.ParentDepartment)
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
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.ParentDepartment)
                .FirstOrDefaultAsync(d => d.DepartmentType == departmentType && !d.IsDeleted);
        }

        public async Task<IEnumerable<Department>> GetByTypeAsync(IEnumerable<DepartmentType> departmentTypes)
        {
            return await _dbSet
                .Include(d => d.DepartmentHead)
                .Include(d => d.ParentDepartment)
                .Where(d => departmentTypes.Contains(d.DepartmentType) && !d.IsDeleted)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<bool> CanDeleteDepartmentAsync(Guid departmentId)
        {
            // Check if department has employees
            var hasEmployees = await _context.Set<Employee>()
                .AnyAsync(e => e.DepartmentId == departmentId && !e.IsDeleted);

            if (hasEmployees)
            {
                return false;
            }

            // Check if department has sub-departments
            var hasSubDepartments = await _dbSet
                .AnyAsync(d => d.ParentDepartmentId == departmentId && !d.IsDeleted);

            if (hasSubDepartments)
            {
                return false;
            }

            // Check if department has sections
            var hasSections = await _context.Set<Section>()
                .AnyAsync(s => s.DepartmentId == departmentId && !s.IsDeleted);

            return !hasSections;
        }
    }
}
