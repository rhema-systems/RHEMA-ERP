using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Data.Repositories;

public class EmployeePositionHistoryRepository
    : GenericRepository<EmployeePositionHistory>, IEmployeePositionHistoryRepository
{
    public EmployeePositionHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeePositionHistory>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.EmployeePositionHistories
            .Where(h => h.EmployeeId == employeeId && !h.IsDeleted)
            .Include(h => h.Position)
            .Include(h => h.OrganizationLevel)
            .Include(h => h.OrganizationUnit)
            .Include(h => h.LocationLevel)
            .Include(h => h.Location)
            .OrderByDescending(h => h.StartDate)
            .ToListAsync();
    }

    public async Task<EmployeePositionHistory?> GetCurrentAsync(Guid employeeId)
    {
        return await _context.EmployeePositionHistories
            .Where(h => h.EmployeeId == employeeId && !h.IsDeleted && h.EndDate == null)
            .Include(h => h.Position)
            .Include(h => h.OrganizationLevel)
            .Include(h => h.LocationLevel)
            .FirstOrDefaultAsync();
    }
}
