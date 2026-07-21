using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Data.Repositories;

public class EmployeeQualificationRepository
    : GenericRepository<EmployeeQualification>, IEmployeeQualificationRepository
{
    public EmployeeQualificationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeQualification>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.EmployeeQualifications
            .Where(q => q.EmployeeId == employeeId && !q.IsDeleted)
            .Include(q => q.Qualification)
            .Include(q => q.Country)
            .OrderByDescending(q => q.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeQualification>> GetVerifiedQualificationsAsync(Guid employeeId)
    {
        return await _context.EmployeeQualifications
            .Where(q => q.EmployeeId == employeeId && !q.IsDeleted && q.IsVerified)
            .Include(q => q.Qualification)
            .OrderByDescending(q => q.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeQualification>> GetByInstitutionAsync(string institution)
    {
        return await _context.EmployeeQualifications
            .Where(q => !q.IsDeleted && q.Institution.Contains(institution))
            .Include(q => q.Employee)
            .Include(q => q.Qualification)
            .OrderBy(q => q.Institution)
            .ToListAsync();
    }
}
