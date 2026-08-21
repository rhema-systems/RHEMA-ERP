using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

#region Award Type Repositories

public class AwardTypeRepository : GenericRepository<AwardType>, IAwardTypeRepository
{
    public AwardTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AwardType>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AwardType>()
            .Where(at => at.TenantId == tenantId && !at.IsDeleted)
            .OrderBy(at => at.Name)
            .ToListAsync();
    }

    public async Task<AwardType?> GetByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Set<AwardType>()
            .FirstOrDefaultAsync(at => at.TenantId == tenantId && at.Code == code && !at.IsDeleted);
    }

    public async Task<AwardType?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<AwardType>()
            .Include(at => at.Levels)
            .Include(at => at.Targets)
            .Include(at => at.Budgets)
            .FirstOrDefaultAsync(at => at.Id == id && !at.IsDeleted);
    }

    public async Task<IEnumerable<AwardType>> GetActiveByCategoryAsync(Guid tenantId, AwardCategory category)
    {
        return await _context.Set<AwardType>()
            .Where(at => at.TenantId == tenantId && at.Category == category && at.IsActive && !at.IsDeleted)
            .OrderBy(at => at.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardType>> GetActiveByFrequencyAsync(Guid tenantId, AwardFrequency frequency)
    {
        return await _context.Set<AwardType>()
            .Where(at => at.TenantId == tenantId && at.Frequency == frequency && at.IsActive && !at.IsDeleted)
            .OrderBy(at => at.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardType>> GetWithLevelsAsync(Guid tenantId)
    {
        return await _context.Set<AwardType>()
            .Include(at => at.Levels.Where(l => l.IsActive))
            .Where(at => at.TenantId == tenantId && at.HasLevels && at.IsActive && !at.IsDeleted)
            .OrderBy(at => at.Name)
            .ToListAsync();
    }

    public async Task<int> GetAwardCountByTypeAsync(Guid awardTypeId)
    {
        return await _context.Set<EmployeeAward>()
            .CountAsync(ea => ea.AwardTypeId == awardTypeId && !ea.IsDeleted);
    }

    public async Task<bool> HasActiveNominationsAsync(Guid awardTypeId)
    {
        return await _context.Set<AwardNomination>()
            .AnyAsync(an => an.AwardTypeId == awardTypeId 
                && an.Status != AwardNominationStatus.Rejected
                && an.Status != AwardNominationStatus.Approved
                && !an.IsDeleted);
    }

    public async Task<bool> IsInUseAsync(Guid awardTypeId)
    {
        // “In use” means there is any historical/config/data record that references this award type.
        // Used for deactivation guardrails (not just deletion).
        var hasAwards = await _context.Set<EmployeeAward>()
            .AnyAsync(ea => ea.AwardTypeId == awardTypeId && !ea.IsDeleted);
        if (hasAwards) return true;

        var hasBudgets = await _context.Set<AwardBudget>()
            .AnyAsync(b => b.AwardTypeId == awardTypeId && !b.IsDeleted);
        if (hasBudgets) return true;

        var hasNominations = await _context.Set<AwardNomination>()
            .AnyAsync(n => n.AwardTypeId == awardTypeId && !n.IsDeleted);
        return hasNominations;
    }
}

public class AwardLevelRepository : GenericRepository<AwardLevel>, IAwardLevelRepository
{
    public AwardLevelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AwardLevel>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        return await _context.Set<AwardLevel>()
            .Where(al => al.AwardTypeId == awardTypeId && !al.IsDeleted)
            .OrderBy(al => al.Rank)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardLevel>> GetActiveByAwardTypeIdAsync(Guid awardTypeId)
    {
        return await _context.Set<AwardLevel>()
            .Where(al => al.AwardTypeId == awardTypeId && al.IsActive && !al.IsDeleted)
            .OrderBy(al => al.Rank)
            .ToListAsync();
    }

    public async Task<AwardLevel?> GetByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Set<AwardLevel>()
            .FirstOrDefaultAsync(al => al.TenantId == tenantId && al.Code == code && !al.IsDeleted);
    }

    public async Task<AwardLevel?> GetByRankAsync(Guid awardTypeId, int rank)
    {
        return await _context.Set<AwardLevel>()
            .FirstOrDefaultAsync(al => al.AwardTypeId == awardTypeId && al.Rank == rank && !al.IsDeleted);
    }
}

/// <summary>
/// Resolves the polymorphic <c>AwardTypeTarget.TargetId</c> to a display name, one query per kind.
/// </summary>
/// <remarks>
/// See <see cref="IAwardTargetNameResolver"/> for why this is needed at all. A row whose target has
/// since been deleted resolves to nothing rather than to a fabricated label — the caller renders
/// the raw id, which is honest about a dangling reference instead of hiding it.
/// </remarks>
public class AwardTargetNameResolver : IAwardTargetNameResolver
{
    private readonly ApplicationDbContext _context;

    public AwardTargetNameResolver(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IEnumerable<AwardTypeTarget> targets)
    {
        var names = new Dictionary<Guid, string>();

        var byKind = targets
            .Where(t => t.TargetId.HasValue)
            .GroupBy(t => t.TargetType)
            .ToDictionary(g => g.Key, g => g.Select(t => t.TargetId!.Value).Distinct().ToList());

        foreach (var (kind, ids) in byKind)
        {
            switch (kind)
            {
                case AwardTargetType.OrganizationUnit:
                    foreach (var row in await _context.Set<OrganizationUnit>()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync())
                        names[row.Id] = row.Name;
                    break;

                case AwardTargetType.Position:
                    foreach (var row in await _context.Set<EmployeePosition>()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Title }).ToListAsync())
                        names[row.Id] = row.Title;
                    break;

                case AwardTargetType.StaffLevel:
                    foreach (var row in await _context.Set<StaffLevel>()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync())
                        names[row.Id] = row.Name;
                    break;

                case AwardTargetType.Employee:
                    // Employee.FullName is a computed property, so it cannot be translated to SQL —
                    // the parts are projected and joined here instead.
                    foreach (var row in await _context.Set<Employee>()
                        .Where(x => ids.Contains(x.Id))
                        .Select(x => new { x.Id, x.FirstName, x.LastName }).ToListAsync())
                        names[row.Id] = $"{row.FirstName} {row.LastName}".Trim();
                    break;
            }
        }

        return names;
    }
}

public class AwardTypeTargetRepository : GenericRepository<AwardTypeTarget>, IAwardTypeTargetRepository
{
    public AwardTypeTargetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AwardTypeTarget>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        return await _context.Set<AwardTypeTarget>()
            .Where(att => att.AwardTypeId == awardTypeId && !att.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardTypeTarget>> GetByScopeAsync(Guid awardTypeId, AwardScope scope)
    {
        // Note: AwardScope is no longer directly on AwardTypeTarget
        // This method may need to be refactored based on new business logic
        var targets = await _context.Set<AwardTypeTarget>()
            .Where(att => att.AwardTypeId == awardTypeId && !att.IsDeleted)
            .ToListAsync();
        
        // Filter based on TargetType matching scope
        return targets.Where(t => 
            (scope == AwardScope.OrganizationUnit && t.TargetType == AwardTargetType.OrganizationUnit) ||
            (scope == AwardScope.Position && t.TargetType == AwardTargetType.Position) ||
            (scope == AwardScope.StaffLevel && t.TargetType == AwardTargetType.StaffLevel) ||
            (scope == AwardScope.Employee && t.TargetType == AwardTargetType.Employee)
        ).ToList();
    }

    public async Task<bool> IsEmployeeEligibleAsync(Guid awardTypeId, Guid employeeId)
    {
        var employee = await _context.Set<Employee>()
            .Include(e => e.Position)
                .ThenInclude(p => p.StaffLevel)
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null) return false;

        var targets = await GetByAwardTypeIdAsync(awardTypeId);

        // Check if there are any targets defined
        if (!targets.Any()) return true; // No restrictions = all eligible

        foreach (var target in targets)
        {
            if (target.IsExclusion)
            {
                // If this is an exclusion and matches, employee is not eligible
                if (MatchesTarget(target, employee)) return false;
            }
        }

        // Check if employee matches any inclusion target
        var inclusionTargets = targets.Where(t => !t.IsExclusion);
        if (!inclusionTargets.Any()) return true; // No inclusion rules = eligible

        return inclusionTargets.Any(target => MatchesTarget(target, employee));
    }

    private bool MatchesTarget(AwardTypeTarget target, Employee employee)
    {
        // Match based on TargetType and TargetId
        bool typeMatches = target.TargetType switch
        {
            AwardTargetType.OrganizationUnit => target.TargetId == employee.OrganizationUnitId,
            AwardTargetType.Position => target.TargetId == employee.PositionId,
            AwardTargetType.StaffLevel => target.TargetId == employee.Position?.StaffLevelId,
            AwardTargetType.Employee => target.TargetId == employee.Id,
            _ => false
        };

        if (!typeMatches) return false;

        // Check age range
        if (employee.DateOfBirth.HasValue)
        {
            var age = DateTime.UtcNow.Year - employee.DateOfBirth.Value.Year;
            if (target.MinAge.HasValue && age < target.MinAge) return false;
            if (target.MaxAge.HasValue && age > target.MaxAge) return false;
        }

        return true;
    }
}

public class AwardCycleRepository : GenericRepository<AwardCycle>, IAwardCycleRepository
{
    public AwardCycleRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<AwardCycle?> GetByIdAsync(Guid id)
    {
        return await _context.Set<AwardCycle>()
            .Include(c => c.AwardType)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task<IEnumerable<AwardCycle>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AwardCycle>()
            .Include(c => c.AwardType)
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .OrderByDescending(c => c.Year).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardCycle>> GetByAwardTypeIdAsync(Guid tenantId, Guid awardTypeId)
    {
        return await _context.Set<AwardCycle>()
            .Include(c => c.AwardType)
            .Where(c => c.TenantId == tenantId && c.AwardTypeId == awardTypeId && !c.IsDeleted)
            .OrderByDescending(c => c.Year).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardCycle>> GetPublishedAsync(Guid tenantId)
    {
        return await _context.Set<AwardCycle>()
            .Include(c => c.AwardType)
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.Status == AwardCycleStatus.Published)
            .ToListAsync();
    }

    public async Task<bool> CodeExistsAsync(Guid tenantId, string cycleCode)
    {
        return await _context.Set<AwardCycle>()
            .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.CycleCode == cycleCode);
    }
}

public class AwardBudgetRepository : GenericRepository<AwardBudget>, IAwardBudgetRepository
{
    public AwardBudgetRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<AwardBudget?> GetByIdAsync(Guid id)
    {
        return await _context.Set<AwardBudget>()
            .Include(ab => ab.AwardType)
            .FirstOrDefaultAsync(ab => ab.Id == id && !ab.IsDeleted);
    }

    public async Task<IEnumerable<AwardBudget>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        return await _context.Set<AwardBudget>()
            .Include(ab => ab.AwardType)
            .Where(ab => ab.AwardTypeId == awardTypeId && !ab.IsDeleted)
            .OrderByDescending(ab => ab.Year)
            .ToListAsync();
    }

    public async Task<AwardBudget?> GetByYearAsync(Guid awardTypeId, int year)
    {
        return await _context.Set<AwardBudget>()
            .Include(ab => ab.AwardType)
            .FirstOrDefaultAsync(ab => ab.AwardTypeId == awardTypeId && ab.Year == year && !ab.IsDeleted);
    }

    public async Task<AwardBudget?> GetByBudgetCodeAsync(Guid tenantId, string budgetCode)
    {
        return await _context.Set<AwardBudget>()
            .Include(ab => ab.AwardType)
            .FirstOrDefaultAsync(ab => ab.TenantId == tenantId && ab.BudgetCode == budgetCode && !ab.IsDeleted);
    }

    public async Task<decimal> GetAvailableBudgetAsync(Guid awardTypeId, int year)
    {
        var budget = await GetByYearAsync(awardTypeId, year);
        if (budget == null) return 0;

        return budget.BudgetAmount - budget.SpentAmount - budget.ReservedAmount;
    }

    public async Task<IEnumerable<AwardBudget>> GetByYearRangeAsync(Guid tenantId, int startYear, int endYear)
    {
        return await _context.Set<AwardBudget>()
            .Include(ab => ab.AwardType)
            .Where(ab => ab.TenantId == tenantId && ab.Year >= startYear && ab.Year <= endYear && !ab.IsDeleted)
            .OrderBy(ab => ab.Year)
            .ThenBy(ab => ab.AwardTypeId)
            .ToListAsync();
    }
}

#endregion

#region Employee Award Repositories

public class EmployeeAwardRepository : GenericRepository<EmployeeAward>, IEmployeeAwardRepository
{
    public EmployeeAwardRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeAward>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardType)
            .Include(ea => ea.AwardLevel)
            .Where(ea => ea.TenantId == tenantId && !ea.IsDeleted)
            .OrderByDescending(ea => ea.AwardDate)
            .ToListAsync();
    }

    public async Task<EmployeeAward?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardType)
            .Include(ea => ea.AwardLevel)
            .Include(ea => ea.PresentedBy)
            .Include(ea => ea.AwardNomination)
            .Include(ea => ea.Attachments)
            .Include(ea => ea.TeamRecipients)
                .ThenInclude(tr => tr.Employee)
            .FirstOrDefaultAsync(ea => ea.Id == id && !ea.IsDeleted);
    }

    public async Task<EmployeeAward?> GetByAwardNumberAsync(Guid tenantId, string awardNumber)
    {
        return await _context.Set<EmployeeAward>()
            .FirstOrDefaultAsync(ea => ea.TenantId == tenantId && ea.AwardNumber == awardNumber && !ea.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeAward>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.AwardType)
            .Include(ea => ea.AwardLevel)
            .Where(ea => ea.EmployeeId == employeeId && !ea.IsDeleted)
            .OrderByDescending(ea => ea.AwardDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeAward>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardLevel)
            .Where(ea => ea.AwardTypeId == awardTypeId && !ea.IsDeleted)
            .OrderByDescending(ea => ea.AwardDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeAward>> GetByAwardLevelIdAsync(Guid awardLevelId)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardType)
            .Where(ea => ea.AwardLevelId == awardLevelId && !ea.IsDeleted)
            .OrderByDescending(ea => ea.AwardDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeAward>> GetByNominationIdAsync(Guid nominationId)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardType)
            .Where(ea => ea.AwardNominationId == nominationId && !ea.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeAward>> GetByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardType)
            .Include(ea => ea.AwardLevel)
            .Where(ea => ea.TenantId == tenantId 
                && ea.AwardDate >= startDate 
                && ea.AwardDate <= endDate 
                && !ea.IsDeleted)
            .OrderBy(ea => ea.AwardDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeAward>> GetPendingPresentationsAsync(Guid tenantId)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardType)
            .Where(ea => ea.TenantId == tenantId 
                && ea.PresentationDate == null
                && !ea.IsDeleted)
            .OrderBy(ea => ea.AwardDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeAward>> GetPendingPaymentsAsync(Guid tenantId)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardType)
            .Where(ea => ea.TenantId == tenantId 
                && ea.MonetaryAmount > 0
                && !ea.PaymentProcessed
                && !ea.IsDeleted)
            .OrderBy(ea => ea.PresentationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeAward>> GetPendingLeaveProcessingAsync(Guid tenantId)
    {
        return await _context.Set<EmployeeAward>()
            .Include(ea => ea.Employee)
            .Include(ea => ea.AwardType)
            .Where(ea => ea.TenantId == tenantId 
                && ea.LeaveDaysAwarded > 0
                && !ea.LeaveProcessed
                && !ea.IsDeleted)
            .OrderBy(ea => ea.AwardDate)
            .ToListAsync();
    }

    public async Task<int> GetEmployeeAwardCountAsync(Guid employeeId, Guid awardTypeId)
    {
        return await _context.Set<EmployeeAward>()
            .CountAsync(ea => ea.EmployeeId == employeeId && ea.AwardTypeId == awardTypeId && !ea.IsDeleted);
    }

    public async Task<bool> HasReceivedAwardAsync(Guid employeeId, Guid awardTypeId, int year)
    {
        // Get nominations for the employee in the specified year and award type
        var nominations = await _context.Set<AwardNomination>()
            .Where(an => an.NomineeId == employeeId 
                && an.AwardTypeId == awardTypeId 
                && an.Year == year 
                && !an.IsDeleted)
            .Select(an => an.Id)
            .ToListAsync();

        return await _context.Set<EmployeeAward>()
            .AnyAsync(ea => ea.EmployeeId == employeeId 
                && ea.AwardTypeId == awardTypeId 
                && nominations.Contains(ea.AwardNominationId ?? Guid.Empty)
                && !ea.IsDeleted);
    }
}

public class AwardAttachmentRepository : GenericRepository<AwardAttachment>, IAwardAttachmentRepository
{
    public AwardAttachmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AwardAttachment>> GetByAwardIdAsync(Guid awardId)
    {
        return await _context.Set<AwardAttachment>()
            .Include(aa => aa.UploadedBy)
            .Where(aa => aa.AwardId == awardId && !aa.IsDeleted)
            .OrderByDescending(aa => aa.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardAttachment>> GetByTypeAsync(Guid awardId, AwardAttachmentType type)
    {
        return await _context.Set<AwardAttachment>()
            .Where(aa => aa.AwardId == awardId && aa.AttachmentType == type && !aa.IsDeleted)
            .OrderByDescending(aa => aa.UploadDate)
            .ToListAsync();
    }

    public async Task DeleteByAwardIdAsync(Guid awardId)
    {
        var attachments = await _context.Set<AwardAttachment>()
            .Where(aa => aa.AwardId == awardId)
            .ToListAsync();

        foreach (var attachment in attachments)
        {
            attachment.IsDeleted = true;
            attachment.DeletedAt = DateTime.UtcNow;
        }
    }
}

public class TeamAwardRecipientRepository : GenericRepository<TeamAwardRecipient>, ITeamAwardRecipientRepository
{
    public TeamAwardRecipientRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TeamAwardRecipient>> GetByAwardIdAsync(Guid awardId)
    {
        return await _context.Set<TeamAwardRecipient>()
            .Include(tar => tar.Employee)
            .Where(tar => tar.AwardId == awardId && !tar.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<TeamAwardRecipient>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Set<TeamAwardRecipient>()
            .Include(tar => tar.Award)
                .ThenInclude(a => a.AwardType)
            .Where(tar => tar.EmployeeId == employeeId && !tar.IsDeleted)
            .ToListAsync();
    }

    public async Task DeleteByAwardIdAsync(Guid awardId)
    {
        var recipients = await _context.Set<TeamAwardRecipient>()
            .Where(tar => tar.AwardId == awardId)
            .ToListAsync();

        foreach (var recipient in recipients)
        {
            recipient.IsDeleted = true;
            recipient.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Award Nomination Repositories

public class AwardNominationRepository : GenericRepository<AwardNomination>, IAwardNominationRepository
{
    public async Task<Dictionary<Guid, int>> GetCountsByCycleAsync(Guid tenantId)
    {
        return await _context.Set<AwardNomination>()
            .Where(n => n.TenantId == tenantId && !n.IsDeleted && n.AwardCycleId != null)
            .GroupBy(n => n.AwardCycleId!.Value)
            .Select(g => new { CycleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CycleId, x => x.Count);
    }

    public AwardNominationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AwardNomination>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.AwardLevel)
            .Include(an => an.Nominee)
            .Include(an => an.NominatedBy)
            .Where(an => an.TenantId == tenantId && !an.IsDeleted)
            .OrderByDescending(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<AwardNomination?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.AwardLevel)
            .Include(an => an.Nominee)
            .Include(an => an.NominatedBy)
            .Include(an => an.Committee)
            .Include(an => an.Award)
            .Include(an => an.TeamNominees)
                .ThenInclude(tan => tan.Employee)
            .Include(an => an.Reviews)
                .ThenInclude(cr => cr.Reviewer)
            .FirstOrDefaultAsync(an => an.Id == id && !an.IsDeleted);
    }

    public async Task<AwardNomination?> GetByNominationNumberAsync(Guid tenantId, string nominationNumber)
    {
        return await _context.Set<AwardNomination>()
            .FirstOrDefaultAsync(an => an.TenantId == tenantId && an.NominationNumber == nominationNumber && !an.IsDeleted);
    }

    public async Task<IEnumerable<AwardNomination>> GetByNomineeIdAsync(Guid nomineeId)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.AwardLevel)
            .Include(an => an.NominatedBy)
            .Where(an => an.NomineeId == nomineeId && !an.IsDeleted)
            .OrderByDescending(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNomination>> GetByNominatedByIdAsync(Guid nominatedById)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.AwardLevel)
            .Include(an => an.Nominee)
            .Where(an => an.NominatedById == nominatedById && !an.IsDeleted)
            .OrderByDescending(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNomination>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.Nominee)
            .Include(an => an.NominatedBy)
            .Include(an => an.AwardLevel)
            .Where(an => an.AwardTypeId == awardTypeId && !an.IsDeleted)
            .OrderByDescending(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNomination>> GetByYearAsync(Guid tenantId, int year)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.AwardLevel)
            .Include(an => an.Nominee)
            .Include(an => an.NominatedBy)
            .Where(an => an.TenantId == tenantId && an.Year == year && !an.IsDeleted)
            .OrderByDescending(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNomination>> GetByYearAndPeriodAsync(Guid tenantId, int year, int? quarter = null, int? month = null)
    {
        var query = _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.AwardLevel)
            .Include(an => an.Nominee)
            .Include(an => an.NominatedBy)
            .Where(an => an.TenantId == tenantId && an.Year == year && !an.IsDeleted);

        if (quarter.HasValue)
            query = query.Where(an => an.Quarter == quarter);

        if (month.HasValue)
            query = query.Where(an => an.Month == month);

        return await query
            .OrderByDescending(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNomination>> GetByStatusAsync(Guid tenantId, AwardNominationStatus status)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.AwardLevel)
            .Include(an => an.Nominee)
            .Include(an => an.NominatedBy)
            .Where(an => an.TenantId == tenantId && an.Status == status && !an.IsDeleted)
            .OrderByDescending(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNomination>> GetByCommitteeIdAsync(Guid committeeId)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.AwardLevel)
            .Include(an => an.Nominee)
            .Include(an => an.NominatedBy)
            .Where(an => an.CommitteeId == committeeId && !an.IsDeleted)
            .OrderByDescending(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNomination>> GetRequiringCommitteeReviewAsync(Guid tenantId)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.Nominee)
            .Include(an => an.NominatedBy)
            .Include(an => an.Committee)
            .Where(an => an.TenantId == tenantId 
                && an.Status == AwardNominationStatus.UnderReview
                && an.CommitteeId != null
                && !an.IsDeleted)
            .OrderBy(an => an.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNomination>> GetApprovedWithoutAwardAsync(Guid tenantId)
    {
        return await _context.Set<AwardNomination>()
            .Include(an => an.AwardType)
            .Include(an => an.AwardCycle)
            .Include(an => an.Nominee)
            .Where(an => an.TenantId == tenantId 
                && an.Status == AwardNominationStatus.Approved
                && an.EmployeeAwardId == null
                && !an.IsDeleted)
            .OrderBy(an => an.OutcomeDate)
            .ToListAsync();
    }

    public async Task<bool> HasNominationInPeriodAsync(Guid employeeId, Guid awardTypeId, int year, int? quarter = null, int? month = null)
    {
        var query = _context.Set<AwardNomination>()
            .Where(an => an.NomineeId == employeeId 
                && an.AwardTypeId == awardTypeId 
                && an.Year == year
                && !an.IsDeleted);

        if (quarter.HasValue)
            query = query.Where(an => an.Quarter == quarter);

        if (month.HasValue)
            query = query.Where(an => an.Month == month);

        return await query.AnyAsync();
    }
}

public class TeamAwardNomineeRepository : GenericRepository<TeamAwardNominee>, ITeamAwardNomineeRepository
{
    public TeamAwardNomineeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TeamAwardNominee>> GetByNominationIdAsync(Guid nominationId)
    {
        return await _context.Set<TeamAwardNominee>()
            .Include(tan => tan.Employee)
            .Where(tan => tan.NominationId == nominationId && !tan.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<TeamAwardNominee>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Set<TeamAwardNominee>()
            .Include(tan => tan.Nomination)
                .ThenInclude(n => n.AwardType)
            .Where(tan => tan.EmployeeId == employeeId && !tan.IsDeleted)
            .ToListAsync();
    }

    /// <summary>
    /// Overridden to load <c>Employee</c>, following <see cref="AwardBudgetRepository"/>. The
    /// inherited version loads no navigations, and <c>ToDto</c> renders an unloaded navigation as
    /// an empty string rather than failing — so a create that mapped its own just-saved entity
    /// returned a blank <c>EmployeeName</c> that a later read then filled in correctly.
    /// </summary>
    public override async Task<TeamAwardNominee?> GetByIdAsync(Guid id)
    {
        return await _context.Set<TeamAwardNominee>()
            .Include(tan => tan.Employee)
            .FirstOrDefaultAsync(tan => tan.Id == id && !tan.IsDeleted);
    }

    public async Task DeleteByNominationIdAsync(Guid nominationId)
    {
        var nominees = await _context.Set<TeamAwardNominee>()
            .Where(tan => tan.NominationId == nominationId)
            .ToListAsync();

        foreach (var nominee in nominees)
        {
            nominee.IsDeleted = true;
            nominee.DeletedAt = DateTime.UtcNow;
        }
    }
}

public class AwardNomineeContributionRepository : GenericRepository<AwardNomineeContribution>, IAwardNomineeContributionRepository
{
    public AwardNomineeContributionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AwardNomineeContribution>> GetByNominationIdAsync(Guid nominationId)
    {
        return await _context.Set<AwardNomineeContribution>()
            .Where(c => c.AwardNominationId == nominationId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteByNominationIdAsync(Guid nominationId)
    {
        var contributions = await _context.Set<AwardNomineeContribution>()
            .Where(c => c.AwardNominationId == nominationId)
            .ToListAsync();

        foreach (var c in contributions)
        {
            c.IsDeleted = true;
            c.DeletedAt = DateTime.UtcNow;
        }
    }
}

public class AwardNominationAttachmentRepository : GenericRepository<AwardNominationAttachment>, IAwardNominationAttachmentRepository
{
    public AwardNominationAttachmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AwardNominationAttachment>> GetByNominationIdAsync(Guid nominationId)
    {
        return await _context.Set<AwardNominationAttachment>()
            .Include(a => a.UploadedBy)
            .Where(a => a.AwardNominationId == nominationId && !a.IsDeleted)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync();
    }

    public async Task DeleteByNominationIdAsync(Guid nominationId)
    {
        var attachments = await _context.Set<AwardNominationAttachment>()
            .Where(a => a.AwardNominationId == nominationId)
            .ToListAsync();

        foreach (var a in attachments)
        {
            a.IsDeleted = true;
            a.DeletedAt = DateTime.UtcNow;
        }
    }
}

public class AwardCommitteeRepository : GenericRepository<AwardCommittee>, IAwardCommitteeRepository
{
    public AwardCommitteeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AwardCommittee>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AwardCommittee>()
            .Where(ac => ac.TenantId == tenantId && !ac.IsDeleted)
            .OrderBy(ac => ac.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardCommittee>> GetActiveCommitteesAsync(Guid tenantId)
    {
        return await _context.Set<AwardCommittee>()
            .Where(ac => ac.TenantId == tenantId && ac.IsActive && !ac.IsDeleted)
            .OrderBy(ac => ac.Name)
            .ToListAsync();
    }

    public async Task<AwardCommittee?> GetWithMembersAsync(Guid id)
    {
        return await _context.Set<AwardCommittee>()
            .Include(ac => ac.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.Employee)
            .FirstOrDefaultAsync(ac => ac.Id == id && !ac.IsDeleted);
    }

    public async Task<AwardCommittee?> GetActiveForDateAsync(Guid tenantId, DateTime date)
    {
        return await _context.Set<AwardCommittee>()
            .FirstOrDefaultAsync(ac => ac.TenantId == tenantId 
                && ac.IsActive
                && ac.EffectiveFrom <= date
                && (ac.EffectiveTo == null || ac.EffectiveTo >= date)
                && !ac.IsDeleted);
    }

    public async Task<bool> HasQuorumAsync(Guid committeeId)
    {
        var committee = await _context.Set<AwardCommittee>()
            .Include(ac => ac.Members)
            .FirstOrDefaultAsync(ac => ac.Id == committeeId && !ac.IsDeleted);

        if (committee == null) return false;

        var activeCount = committee.Members.Count(m => m.IsActive && !m.IsDeleted);
        return activeCount >= committee.QuorumRequired;
    }
}

public class AwardCommitteeMemberRepository : GenericRepository<AwardCommitteeMember>, IAwardCommitteeMemberRepository
{
    public AwardCommitteeMemberRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Overridden to load <c>Committee</c> and <c>Employee</c>, which <c>ToDto</c> renders as names.
    /// See the note on <see cref="TeamAwardNomineeRepository.GetByIdAsync"/> — an unloaded
    /// navigation maps to an empty string, so a write response came back with blank names.
    /// </summary>
    public override async Task<AwardCommitteeMember?> GetByIdAsync(Guid id)
    {
        return await _context.Set<AwardCommitteeMember>()
            .Include(acm => acm.Committee)
            .Include(acm => acm.Employee)
            .FirstOrDefaultAsync(acm => acm.Id == id && !acm.IsDeleted);
    }

    public async Task<IEnumerable<AwardCommitteeMember>> GetByCommitteeIdAsync(Guid committeeId)
    {
        return await _context.Set<AwardCommitteeMember>()
            .Include(acm => acm.Employee)
            .Where(acm => acm.CommitteeId == committeeId && !acm.IsDeleted)
            .OrderBy(acm => acm.Role)
            .ThenBy(acm => acm.Employee.FullName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardCommitteeMember>> GetActiveByCommitteeIdAsync(Guid committeeId)
    {
        return await _context.Set<AwardCommitteeMember>()
            .Include(acm => acm.Employee)
            .Where(acm => acm.CommitteeId == committeeId && acm.IsActive && !acm.IsDeleted)
            .OrderBy(acm => acm.Role)
            .ThenBy(acm => acm.Employee.FullName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardCommitteeMember>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Set<AwardCommitteeMember>()
            .Include(acm => acm.Committee)
            .Where(acm => acm.EmployeeId == employeeId && !acm.IsDeleted)
            .OrderByDescending(acm => acm.StartDate)
            .ToListAsync();
    }

    public async Task<bool> IsActiveMemberAsync(Guid committeeId, Guid employeeId)
    {
        return await _context.Set<AwardCommitteeMember>()
            .AnyAsync(acm => acm.CommitteeId == committeeId 
                && acm.EmployeeId == employeeId 
                && acm.IsActive 
                && !acm.IsDeleted);
    }

    public async Task<int> GetActiveCountAsync(Guid committeeId)
    {
        return await _context.Set<AwardCommitteeMember>()
            .CountAsync(acm => acm.CommitteeId == committeeId && acm.IsActive && !acm.IsDeleted);
    }
}

public class AwardNominationReviewRepository : GenericRepository<AwardNominationReview>, IAwardNominationReviewRepository
{
    public AwardNominationReviewRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Overridden to load <c>AwardNomination</c> and <c>Reviewer</c>, which <c>ToDto</c> renders as
    /// names. See the note on <see cref="TeamAwardNomineeRepository.GetByIdAsync"/>.
    /// </summary>
    public override async Task<AwardNominationReview?> GetByIdAsync(Guid id)
    {
        return await _context.Set<AwardNominationReview>()
            .Include(acr => acr.AwardNomination)
            .Include(acr => acr.Reviewer)
            .FirstOrDefaultAsync(acr => acr.Id == id && !acr.IsDeleted);
    }

    public async Task<IEnumerable<AwardNominationReview>> GetByNominationIdAsync(Guid nominationId)
    {
        return await _context.Set<AwardNominationReview>()
            .Include(acr => acr.Reviewer)
            .Where(acr => acr.AwardNominationId == nominationId && !acr.IsDeleted)
            .OrderBy(acr => acr.ReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNominationReview>> GetByReviewerIdAsync(Guid reviewerId)
    {
        return await _context.Set<AwardNominationReview>()
            .Include(acr => acr.AwardNomination)
                .ThenInclude(an => an.AwardType)
            .Include(acr => acr.AwardNomination)
                .ThenInclude(an => an.Nominee)
            .Where(acr => acr.ReviewerId == reviewerId && !acr.IsDeleted)
            .OrderByDescending(acr => acr.ReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AwardNominationReview>> GetPendingReviewsAsync(Guid reviewerId)
    {
        return await _context.Set<AwardNominationReview>()
            .Include(acr => acr.AwardNomination)
                .ThenInclude(an => an.AwardType)
            .Include(acr => acr.AwardNomination)
                .ThenInclude(an => an.Nominee)
            .Where(acr => acr.ReviewerId == reviewerId 
                && acr.ReviewDate == null 
                && !acr.IsDeleted)
            .OrderBy(acr => acr.CreatedAt)
            .ToListAsync();
    }

    public async Task<AwardNominationReview?> GetReviewAsync(Guid nominationId, Guid reviewerId)
    {
        return await _context.Set<AwardNominationReview>()
            .FirstOrDefaultAsync(acr => acr.AwardNominationId == nominationId 
                && acr.ReviewerId == reviewerId 
                && !acr.IsDeleted);
    }

    public async Task<int> GetApprovalCountAsync(Guid nominationId)
    {
        return await _context.Set<AwardNominationReview>()
            .CountAsync(acr => acr.AwardNominationId == nominationId 
                && acr.Approved == true 
                && !acr.IsDeleted);
    }

    public async Task<int> GetRejectionCountAsync(Guid nominationId)
    {
        return await _context.Set<AwardNominationReview>()
            .CountAsync(acr => acr.AwardNominationId == nominationId 
                && acr.Approved == false 
                && !acr.IsDeleted);
    }

    public async Task<bool> HasReviewedAsync(Guid nominationId, Guid reviewerId)
    {
        return await _context.Set<AwardNominationReview>()
            .AnyAsync(acr => acr.AwardNominationId == nominationId 
                && acr.ReviewerId == reviewerId 
                && acr.ReviewDate != null
                && !acr.IsDeleted);
    }
}

#endregion

#region Long Service Award Repositories

public class LongServiceAwardRepository : GenericRepository<LongServiceAward>, ILongServiceAwardRepository
{
    public LongServiceAwardRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LongServiceAward>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<LongServiceAward>()
            .Include(lsa => lsa.Employee)
            .Where(lsa => lsa.TenantId == tenantId && !lsa.IsDeleted)
            .OrderByDescending(lsa => lsa.MilestoneDate)
            .ToListAsync();
    }

    public async Task<LongServiceAward?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<LongServiceAward>()
            .Include(lsa => lsa.Employee)
            .FirstOrDefaultAsync(lsa => lsa.Id == id && !lsa.IsDeleted);
    }

    public async Task<IEnumerable<LongServiceAward>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Set<LongServiceAward>()
            .Where(lsa => lsa.EmployeeId == employeeId && !lsa.IsDeleted)
            .OrderByDescending(lsa => lsa.YearsOfService)
            .ToListAsync();
    }

    public async Task<IEnumerable<LongServiceAward>> GetByYearsOfServiceAsync(Guid tenantId, int yearsOfService)
    {
        return await _context.Set<LongServiceAward>()
            .Include(lsa => lsa.Employee)
            .Where(lsa => lsa.TenantId == tenantId && lsa.YearsOfService == yearsOfService && !lsa.IsDeleted)
            .OrderByDescending(lsa => lsa.MilestoneDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LongServiceAward>> GetUpcomingMilestonesAsync(Guid tenantId, int daysAhead = 90)
    {
        var futureDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _context.Set<LongServiceAward>()
            .Include(lsa => lsa.Employee)
            .Where(lsa => lsa.TenantId == tenantId 
                && lsa.MilestoneDate <= futureDate
                && lsa.MilestoneDate >= DateTime.UtcNow
                && !lsa.IsProcessed
                && !lsa.IsDeleted)
            .OrderBy(lsa => lsa.MilestoneDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LongServiceAward>> GetPendingProcessingAsync(Guid tenantId)
    {
        return await _context.Set<LongServiceAward>()
            .Include(lsa => lsa.Employee)
            .Where(lsa => lsa.TenantId == tenantId 
                && !lsa.IsProcessed
                && lsa.MilestoneDate <= DateTime.UtcNow
                && !lsa.IsDeleted)
            .OrderBy(lsa => lsa.MilestoneDate)
            .ToListAsync();
    }

    public async Task<LongServiceAward?> GetByEmployeeAndYearsAsync(Guid employeeId, int yearsOfService)
    {
        return await _context.Set<LongServiceAward>()
            .FirstOrDefaultAsync(lsa => lsa.EmployeeId == employeeId && lsa.YearsOfService == yearsOfService && !lsa.IsDeleted);
    }
}

#endregion



