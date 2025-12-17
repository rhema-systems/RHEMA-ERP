using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories
{
    public class SectionRepository : GenericRepository<Section>, ISectionRepository
    {
        public SectionRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Section?> GetByCodeAsync(string code)
        {
            return await _dbSet
                .Include(s => s.Department)
                .Include(s => s.SectionHead)
                .FirstOrDefaultAsync(s => s.Code == code && !s.IsDeleted);
        }

        public async Task<IEnumerable<Section>> GetByDepartmentAsync(Guid departmentId)
        {
            return await _dbSet
                .Include(s => s.SectionHead)
                .Where(s => s.DepartmentId == departmentId && !s.IsDeleted)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Section>> GetActiveSectionsAsync()
        {
            return await _dbSet
                .Include(s => s.Department)
                .Include(s => s.SectionHead)
                .Where(s => s.IsActive && !s.IsDeleted)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<Section?> GetWithEmployeesAsync(Guid id)
        {
            return await _dbSet
                .Include(s => s.Department)
                .Include(s => s.SectionHead)
                .Include(s => s.Employees)
                    .ThenInclude(e => e.Position)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        }

        public async Task<bool> CodeExistsAsync(string code)
        {
            return await _dbSet.AnyAsync(s => s.Code == code && !s.IsDeleted);
        }
    }
}
