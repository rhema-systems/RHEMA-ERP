using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Data.Repositories;

public class EmployeeWorkHistoryRepository
    : GenericRepository<EmployeeWorkHistory>, IEmployeeWorkHistoryRepository
{
    public EmployeeWorkHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeWorkHistory>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.EmployeeWorkHistories
            .Where(w => w.EmployeeId == employeeId && !w.IsDeleted)
            .OrderByDescending(w => w.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeWorkHistory>> GetByCompanyAsync(string companyName)
    {
        return await _context.EmployeeWorkHistories
            .Where(w => !w.IsDeleted && w.CompanyName.Contains(companyName))
            .Include(w => w.Employee)
            .OrderBy(w => w.CompanyName)
            .ToListAsync();
    }
}
