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

    /// <summary>The placement in force today, or null.</summary>
    /// <remarks>
    /// ⚠ <b>Had two holes and no callers</b>, which is the only reason neither was ever seen. It
    /// asked whether the placement had ENDED but never whether it had STARTED, so a placement dated
    /// next month was "current" now; and it knew nothing of withdrawal, so a withdrawn future
    /// placement — whose <c>EffectiveTo</c> is its own start date — satisfied
    /// <c>EffectiveTo &gt; UtcNow</c> from the moment it was withdrawn. Corrected in lane E1b to
    /// the same predicate <c>ActiveAssignments</c> and <c>EmolumentService</c> use.
    /// </remarks>
    public async Task<EmployeeSalaryAssignment?> GetCurrentAsync(Guid employeeId)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.EmployeeSalaryAssignments
            .Where(a => a.EmployeeId == employeeId
                     && !a.IsDeleted
                     && a.WithdrawnAt == null
                     && a.EffectiveDate <= today
                     && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Include(a => a.Grade)
            .Include(a => a.Level)
            .Include(a => a.Notch)
            .OrderByDescending(a => a.EffectiveDate)
            .FirstOrDefaultAsync();
    }
}
