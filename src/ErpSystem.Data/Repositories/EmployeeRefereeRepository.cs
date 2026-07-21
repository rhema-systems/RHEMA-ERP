using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Data.Repositories;

public class EmployeeRefereeRepository
    : GenericRepository<EmployeeReferee>, IEmployeeRefereeRepository
{
    public EmployeeRefereeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeReferee>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.EmployeeReferees
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.IsPrimary)
            .ThenBy(r => r.FullName)
            .ToListAsync();
    }
}
