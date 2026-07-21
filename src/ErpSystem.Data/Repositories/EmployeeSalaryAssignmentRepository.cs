using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Data.Repositories;

public class EmployeeSalaryAssignmentRepository
    : GenericRepository<EmployeeSalaryAssignment>, IEmployeeSalaryAssignmentRepository
{
    public EmployeeSalaryAssignmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeSalaryAssignment>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.EmployeeSalaryAssignments
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .Include(a => a.Grade)
            .Include(a => a.Level)
            .Include(a => a.Notch)
            .OrderByDescending(a => a.EffectiveDate)
            .ToListAsync();
    }

    public async Task<EmployeeSalaryAssignment?> GetCurrentAsync(Guid employeeId)
    {
        return await _context.EmployeeSalaryAssignments
            .Where(a => a.EmployeeId == employeeId
                     && !a.IsDeleted
                     && (a.EffectiveTo == null || a.EffectiveTo > DateTime.UtcNow))
            .Include(a => a.Grade)
            .Include(a => a.Level)
            .Include(a => a.Notch)
            .OrderByDescending(a => a.EffectiveDate)
            .FirstOrDefaultAsync();
    }
}
