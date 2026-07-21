using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for employee skill operations
/// </summary>
public class EmployeeSkillRepository : GenericRepository<EmployeeSkill>, IEmployeeSkillRepository
{
    public EmployeeSkillRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EmployeeSkill>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.EmployeeSkills
            .Where(es => es.EmployeeId == employeeId)
            .Include(es => es.Skill)
            .Include(es => es.Employee)
            .OrderBy(es => es.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeSkill>> GetBySkillAsync(Guid skillId)
    {
        return await _context.EmployeeSkills
            .Where(es => es.SkillId == skillId)
            .Include(es => es.Employee)
            .Include(es => es.Skill)
            .OrderBy(es => es.Employee.FirstName)
            .ThenBy(es => es.Employee.LastName)
            .ToListAsync();
    }

    public async Task<EmployeeSkill?> GetByEmployeeAndSkillAsync(Guid employeeId, Guid skillId)
    {
        return await _context.EmployeeSkills
            .Where(es => es.EmployeeId == employeeId && es.SkillId == skillId)
            .Include(es => es.Skill)
            .Include(es => es.Employee)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<EmployeeSkill>> GetVerifiedSkillsAsync(Guid employeeId)
    {
        return await _context.EmployeeSkills
            .Where(es => es.EmployeeId == employeeId && es.IsVerified)
            .Include(es => es.Skill)
            .Include(es => es.Employee)
            .OrderBy(es => es.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeSkill>> GetExpiringCertificationsAsync(DateTime withinDate)
    {
        return await _context.EmployeeSkills
            .Where(es => es.CertificationExpiryDate.HasValue && 
                        es.CertificationExpiryDate.Value <= DateOnly.FromDateTime(withinDate))
            .Include(es => es.Employee)
            .Include(es => es.Skill)
            .OrderBy(es => es.CertificationExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeSkill>> GetBySkillLevelAsync(SkillLevel level)
    {
        return await _context.EmployeeSkills
            .Where(es => es.SkillLevel == level)
            .Include(es => es.Employee)
            .Include(es => es.Skill)
            .OrderBy(es => es.Employee.FirstName)
            .ThenBy(es => es.Employee.LastName)
            .ThenBy(es => es.Skill.Name)
            .ToListAsync();
    }
}