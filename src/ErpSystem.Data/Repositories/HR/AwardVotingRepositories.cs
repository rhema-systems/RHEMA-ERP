using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

public class AwardVoteRepository : GenericRepository<AwardVote>, IAwardVoteRepository
{
    public AwardVoteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<AwardVote?> GetByVoterAsync(Guid cycleId, Guid voterId)
    {
        return await _context.Set<AwardVote>()
            .FirstOrDefaultAsync(v => v.AwardCycleId == cycleId && v.VoterId == voterId && !v.IsDeleted);
    }

    public async Task<IEnumerable<AwardVote>> GetByCycleAsync(Guid cycleId)
    {
        return await _context.Set<AwardVote>()
            .Where(v => v.AwardCycleId == cycleId && !v.IsDeleted)
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, int>> GetCountsByNominationAsync(Guid cycleId)
    {
        return await _context.Set<AwardVote>()
            .Where(v => v.AwardCycleId == cycleId && !v.IsDeleted)
            .GroupBy(v => v.AwardNominationId)
            .Select(g => new { NominationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.NominationId, x => x.Count);
    }
}

/// <summary>
/// Decides whether an employee is in an award's electorate.
/// </summary>
/// <remarks>
/// <para>The rule mirrors eligibility deliberately, because TDC describes both the same way — a
/// section of the organisation, named by unit, position, staff level or person. Sharing the target
/// table and the matching logic means the two cannot drift into behaving differently, which would
/// be confusing precisely where it matters: an employee told they may not vote, on the same screen
/// as a colleague who may.</para>
///
/// <para><b>No electorate targets means everybody votes.</b> That is the "or all of them" half of
/// <i>"a section of the employees or all of them can vote"</i>, and it is the right default: an
/// award nobody has scoped should not silently disenfranchise the whole company.</para>
///
/// <para>A terminated employee never votes, whatever the targets say.</para>
/// </remarks>
public class AwardElectorateEvaluator : IAwardElectorateEvaluator
{
    private readonly ApplicationDbContext _context;

    public AwardElectorateEvaluator(ApplicationDbContext context) => _context = context;

    public async Task<bool> CanVoteAsync(Guid awardTypeId, Guid employeeId, Guid tenantId, DateTime asOf)
    {
        var employee = await _context.Set<Employee>()
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted);

        if (employee == null || employee.TerminationDate != null)
            return false;

        var targets = await _context.Set<AwardTypeTarget>()
            .Where(t => t.AwardTypeId == awardTypeId && t.TenantId == tenantId && !t.IsDeleted
                && t.Purpose == AwardTargetPurpose.Electorate
                && (t.EffectiveFrom == null || t.EffectiveFrom <= asOf)
                && (t.EffectiveTo == null || t.EffectiveTo >= asOf))
            .ToListAsync();

        // "…or all of them."
        if (targets.Count == 0)
            return true;

        // An exclusion beats an inclusion, as it does for eligibility.
        if (targets.Any(t => t.IsExclusion && Matches(t, employee)))
            return false;

        var inclusions = targets.Where(t => !t.IsExclusion).ToList();
        return inclusions.Count == 0 || inclusions.Any(t => Matches(t, employee));
    }

    private static bool Matches(AwardTypeTarget target, Employee employee) => target.TargetType switch
    {
        AwardTargetType.OrganizationUnit => target.TargetId == employee.OrganizationUnitId,
        AwardTargetType.Position => target.TargetId == employee.PositionId,
        AwardTargetType.StaffLevel => target.TargetId == employee.Position?.StaffLevelId,
        AwardTargetType.Employee => target.TargetId == employee.Id,
        _ => false
    };
}
