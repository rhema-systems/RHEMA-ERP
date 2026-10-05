using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 6: POLICY & VENDOR
// ============================================================================

#region Staff Travel Policy Repository

public class StaffTravelPolicyRepository : GenericRepository<StaffTravelPolicy>, IStaffTravelPolicyRepository
{
    public StaffTravelPolicyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelPolicy?> GetWithRulesAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Rules)
            .Include(p => p.AppliesToLevelFrom)
            .Include(p => p.AppliesToLevelTo)
            .Include(p => p.AppliesToOrganizationUnit)
            // The fourth read feeding a policy DTO. Without this the approve endpoint returns its
            // own response with a null approver — the one field the caller just created.
            .Include(p => p.ApprovedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelPolicy>> GetCurrentVersionsAsync()
    {
        return await _dbSet
            // Same includes as the other two reads that feed StaffTravelPolicySummaryDto: without
            // Rules the RuleCount is always 0, and without ApprovedBy the approver never resolves —
            // so "which policies are in force" showed no rules and no approver. Uneven siblings.
            .Include(p => p.Rules)
            .Include(p => p.ApprovedBy)
            .Where(p => p.IsCurrentVersion && !p.IsDeleted)
            .OrderBy(p => p.PolicyName)
            .ToListAsync();
    }

    /// <summary>
    /// Current policies covering a staff level, organisation unit and date, most specific first.
    /// </summary>
    /// <remarks>
    /// ⚠ <paramref name="staffLevelId"/> was previously accepted and <b>never used</b> — the method
    /// took the parameter, filtered on unit and date only, and then ordered by whether a level
    /// range happened to be set. So a policy written for senior management applied to everyone.
    /// It matters now that bookings are refused against these caps.
    ///
    /// <para>The range is compared on <see cref="StaffLevel.Rank"/>, not on the level ids: the
    /// policy names the two ends of a band, and the traveller sits inside it or does not. A
    /// traveller with no staff level (no position, or a position with no level) matches only
    /// unbanded policies — the organisation-wide default — rather than being excluded entirely.</para>
    ///
    /// <para>Tenant scoping is the caller's, per this area's convention (the DbContext's global
    /// filter is inert — see <c>ApplicationDbContext</c>). <c>StaffTravelPolicyGuard</c> filters
    /// the result before any cap is applied.</para>
    /// </remarks>
    /// <param name="unitChain">
    /// The traveller's unit followed by every unit above it, nearest first (<c>IHrAudienceResolver.UnitAncestryAsync</c>).
    /// ⚠ It was one unit matched exactly (lane 4, O-5): a directorate's policy did not cover its departments, and a
    /// policy applied only to the unit the request named. Empty for a traveller with no unit — org-wide policies only.
    /// </param>
    /// <remarks>
    /// ⚠ <b>The policies alone — no rules, no approver — and the unit filter applied here, not in SQL</b> (lane 4,
    /// measured 2026-10-02). With <c>Include(Rules)</c>, <c>Include(ApprovedBy)</c> (a whole <c>Employee</c> row) and the
    /// unit chain sent as a JSON list, SQL Server sized the query's memory grant at ~600 MB and used 16 KB; under load it
    /// queued for it (<c>RESOURCE_SEMAPHORE</c>) and a policy preview took 25 s. The guard reads only the policy's own
    /// columns, and a tenant holds a handful of policies in force on a date, so filtering the unit chain in memory
    /// costs nothing. The <c>applicable</c> endpoint, which shows the approver and a rule count, reads those narrowly
    /// itself. Untracked: nothing here is written back.
    /// </remarks>
    public async Task<IEnumerable<StaffTravelPolicy>> GetApplicablePoliciesAsync(Guid? staffLevelId, IReadOnlyList<Guid> unitChain, DateOnly onDate)
    {
        var chain = (unitChain ?? Array.Empty<Guid>()).ToList();
        var candidates = (await _dbSet
                .AsNoTracking()
                .Where(p => p.IsCurrentVersion && !p.IsDeleted
                         && p.EffectiveFrom <= onDate
                         && (p.EffectiveTo == null || p.EffectiveTo >= onDate))
                .ToListAsync())
            .Where(p => p.AppliesToOrganizationUnitId is not Guid unit || chain.Contains(unit))
            .ToList();

        if (candidates.Count == 0) return candidates;

        int? travellerRank = staffLevelId is Guid levelId
            ? await _context.Set<StaffLevel>()
                .Where(l => l.Id == levelId && !l.IsDeleted)
                .Select(l => (int?)l.Rank)
                .FirstOrDefaultAsync()
            : null;

        var bandIds = candidates
            .SelectMany(p => new[] { p.AppliesToLevelFromId, p.AppliesToLevelToId })
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var ranks = bandIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _context.Set<StaffLevel>()
                .Where(l => bandIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Rank);

        bool Covers(StaffTravelPolicy p)
        {
            if (p.AppliesToLevelFromId is null && p.AppliesToLevelToId is null) return true;
            if (travellerRank is not int rank) return false; // banded policy, unplaced traveller

            if (p.AppliesToLevelFromId is Guid from && ranks.TryGetValue(from, out var fromRank)
                && rank < fromRank) return false;
            if (p.AppliesToLevelToId is Guid to && ranks.TryGetValue(to, out var toRank)
                && rank > toRank) return false;
            return true;
        }

        // Most-specific first: the nearest unit up the chain, then org-wide; within one unit a banded policy above an
        // unbanded one; then the latest start.
        int Distance(StaffTravelPolicy p)
            => p.AppliesToOrganizationUnitId is Guid unit ? chain.IndexOf(unit) : int.MaxValue;
        return candidates
            .Where(Covers)
            .OrderBy(Distance)
            .ThenByDescending(p => p.AppliesToLevelFromId != null || p.AppliesToLevelToId != null ? 1 : 0)
            .ThenByDescending(p => p.EffectiveFrom)
            .ToList();
    }
}

#endregion

#region Staff Travel Policy Rule Repository

public class StaffTravelPolicyRuleRepository : GenericRepository<StaffTravelPolicyRule>, IStaffTravelPolicyRuleRepository
{
    public StaffTravelPolicyRuleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelPolicyRule>> GetByPolicyIdAsync(Guid policyId)
    {
        return await _dbSet
            .Where(r => r.PolicyId == policyId && !r.IsDeleted)
            .OrderBy(r => r.RuleCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelPolicyRule>> GetActiveRulesAsync(Guid policyId)
    {
        return await _dbSet
            .Where(r => r.PolicyId == policyId && r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.RuleCode)
            .ToListAsync();
    }

    public async Task<StaffTravelPolicyRule?> GetByRuleCodeAsync(Guid policyId, string ruleCode)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.PolicyId == policyId && r.RuleCode == ruleCode && !r.IsDeleted);
    }
}

#endregion

#region Staff Travel Policy Exception Repository

public class StaffTravelPolicyExceptionRepository : GenericRepository<StaffTravelPolicyException>, IStaffTravelPolicyExceptionRepository
{
    public StaffTravelPolicyExceptionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelPolicyException>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(e => e.PolicyRule)
            .Include(e => e.ApprovedBy)
            .Where(e => e.StaffTravelRequestId == requestId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelPolicyException>> GetByRuleIdAsync(Guid policyRuleId)
    {
        return await _dbSet
            .Where(e => e.PolicyRuleId == policyRuleId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelPolicyException>> GetPendingAsync()
    {
        return await _dbSet
            .Include(e => e.PolicyRule)
            .Where(e => e.Status == TravelPolicyExceptionStatus.Pending && !e.IsDeleted)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
    }
}

#endregion
