using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Implements criteria-based candidate search with fit scoring and gap analysis. Filters
/// employees in SQL, bounds the working set, then computes gaps/fit/demographics in memory
/// from bulk-loaded competency, skill and appraisal data.
/// </summary>
public class SuccessionCandidateSearchService : ISuccessionCandidateSearchService
{
    // Upper bound on the pre-scoring working set, to keep in-memory scoring cheap.
    private const int WorkingSetCap = 500;

    private readonly IGenericRepository<Employee> _employees;
    private readonly IGenericRepository<EmployeeCompetency> _employeeCompetencies;
    private readonly IGenericRepository<PositionCompetency> _positionCompetencies;
    private readonly IGenericRepository<EmployeeSkill> _employeeSkills;
    private readonly IGenericRepository<PositionSkillRequirement> _positionSkills;
    private readonly IPositionNamedSetService _namedSets;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisals;
    private readonly IGenericRepository<OrganizationUnit> _orgUnits;
    private readonly IGenericRepository<TalentPoolMember> _poolMembers;
    private readonly IGenericRepository<SuccessionCandidate> _candidates;
    private readonly IGenericRepository<SuccessionPlan> _plans;
    private readonly ICompanyHrPolicyProvider _hrPolicy;
    private readonly ICurrentUserProvider _currentUserProvider;

    public SuccessionCandidateSearchService(
        IGenericRepository<Employee> employees,
        IGenericRepository<EmployeeCompetency> employeeCompetencies,
        IGenericRepository<PositionCompetency> positionCompetencies,
        IGenericRepository<EmployeeSkill> employeeSkills,
        IGenericRepository<PositionSkillRequirement> positionSkills,
        IPositionNamedSetService namedSets,
        IGenericRepository<PerformanceAppraisal> appraisals,
        IGenericRepository<OrganizationUnit> orgUnits,
        IGenericRepository<TalentPoolMember> poolMembers,
        IGenericRepository<SuccessionCandidate> candidates,
        IGenericRepository<SuccessionPlan> plans,
        ICompanyHrPolicyProvider hrPolicy,
        ICurrentUserProvider currentUserProvider)
    {
        _employees = employees;
        _employeeCompetencies = employeeCompetencies;
        _positionCompetencies = positionCompetencies;
        _employeeSkills = employeeSkills;
        _positionSkills = positionSkills;
        _namedSets = namedSets;
        _appraisals = appraisals;
        _orgUnits = orgUnits;
        _poolMembers = poolMembers;
        _candidates = candidates;
        _plans = plans;
        _hrPolicy = hrPolicy;
        _currentUserProvider = currentUserProvider;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<IReadOnlyList<CandidateFitScoreDto>> ScorePlanCandidatesAsync(
        Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var plan = await _plans.GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == planId && p.TenantId == tenantId && !p.IsDeleted, cancellationToken);
        if (plan is null)
            return new List<CandidateFitScoreDto>();

        var candidates = await _candidates.GetQueryable()
            .Include(c => c.Employee)
            .Where(c => c.TenantId == tenantId && c.SuccessionPlanId == planId && !c.IsDeleted)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
            return new List<CandidateFitScoreDto>();

        var weights = WeightsFrom(await _hrPolicy.GetAsync(cancellationToken));
        var empIds = candidates.Select(c => c.EmployeeId).Distinct().ToList();

        var requiredCompetencies = await _positionCompetencies.GetQueryable()
            .Where(pc => pc.TenantId == tenantId && pc.PositionId == plan.PositionId && !pc.IsDeleted)
            .ToDictionaryAsync(pc => pc.CompetencyId, pc => pc.RequiredProficiencyLevel, cancellationToken);
        // ⚠ Round 2, lane C3 — the EFFECTIVE requirement, not the individual table. A skill the
        // post requires through an attached skill set counts towards a candidate's match exactly as
        // an individually-listed one does; reading the table directly would have scored candidates
        // against a shorter list than the post actually has.
        var requiredSkills = (await _namedSets.GetEffectiveSkillsAsync(plan.PositionId, tenantId, cancellationToken))
            .ToDictionary(ps => ps.SkillId, ps => (int)ps.RequiredLevel);

        var employeeCompetencies = (await _employeeCompetencies.GetQueryable()
                .Where(ec => ec.TenantId == tenantId && empIds.Contains(ec.EmployeeId) && !ec.IsDeleted)
                .Select(ec => new { ec.EmployeeId, ec.CompetencyId, ec.CurrentProficiencyLevel })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.CompetencyId, x => x.CurrentProficiencyLevel));

        var employeeSkills = (await _employeeSkills.GetQueryable()
                .Where(es => es.TenantId == tenantId && empIds.Contains(es.EmployeeId) && !es.IsDeleted)
                .Select(es => new { es.EmployeeId, es.SkillId, es.SkillLevel })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.SkillId, x => (int)x.SkillLevel));

        var latestAppraisal = (await _appraisals.GetQueryable()
                .Where(a => a.TenantId == tenantId && empIds.Contains(a.EmployeeId) && !a.IsDeleted && a.OverallScore != null
                            // A withdrawn appraisal has no result (performance closure E-d1).
                            && a.Status != ErpSystem.Core.Enums.AppraisalStatus.Withdrawn)
                .Select(a => new { a.EmployeeId, a.Year, a.OverallScore })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Year).First());

        var scored = new List<(SuccessionCandidate candidate, FitScoreResult fit)>();
        foreach (var c in candidates)
        {
            var comps = employeeCompetencies.GetValueOrDefault(c.EmployeeId) ?? new Dictionary<Guid, int>();
            var skills = employeeSkills.GetValueOrDefault(c.EmployeeId) ?? new Dictionary<Guid, int>();

            var fitPairs = new List<(int required, int current)>();
            foreach (var kv in requiredCompetencies) fitPairs.Add((kv.Value, comps.GetValueOrDefault(kv.Key, 0)));
            foreach (var kv in requiredSkills) fitPairs.Add((kv.Value, skills.GetValueOrDefault(kv.Key, 0)));

            latestAppraisal.TryGetValue(c.EmployeeId, out var appraisal);
            var fit = SuccessionFitScoring.Score(new FitScoreInputs(
                Performance: SuccessionFitScoring.NormalizePerformance(appraisal?.OverallScore),
                CompetencyFit: SuccessionFitScoring.CompetencyFitFraction(fitPairs),
                Potential: null,
                Tenure: SuccessionFitScoring.NormalizeTenure(c.Employee?.YearsOfService)), weights);

            scored.Add((c, fit));
        }

        var ordered = scored
            .OrderByDescending(s => s.fit.Score)
            .ThenBy(s => s.candidate.Employee?.LastName)
            .ToList();

        var results = new List<CandidateFitScoreDto>();
        for (int i = 0; i < ordered.Count; i++)
        {
            var (candidate, fit) = ordered[i];
            results.Add(new CandidateFitScoreDto
            {
                CandidateId = candidate.Id,
                EmployeeId = candidate.EmployeeId,
                EmployeeName = candidate.Employee?.FullName ?? string.Empty,
                CurrentRank = candidate.Rank,
                FitScore = fit.Score,
                FitBand = fit.Band.ToString(),
                SuggestedRank = i + 1,
            });
        }
        return results;
    }

    public async Task<IReadOnlyList<SuccessionCandidateSearchResultDto>> SearchAsync(
        SuccessionCandidateSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var settings = await _hrPolicy.GetAsync(cancellationToken);
        var weights = WeightsFrom(settings);
        var today = DateOnly.FromDateTime(DateTime.Today);

        // ── 1. Base employee query (SQL-side filters) ──────────────────────────────
        var query = _employees.GetQueryable()
            .Include(e => e.Position)
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive);

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim();
            query = query.Where(e =>
                e.FirstName.Contains(term) || e.LastName.Contains(term) || e.EmployeeNumber.Contains(term));
        }
        if (criteria.OrganizationUnitId.HasValue)
            query = query.Where(e => e.OrganizationUnitId == criteria.OrganizationUnitId);
        if (criteria.DepartmentId.HasValue)
            query = query.Where(e => e.DepartmentId == criteria.DepartmentId);
        if (criteria.MinYearsOfService.HasValue)
        {
            var cutoff = today.AddYears(-criteria.MinYearsOfService.Value);
            query = query.Where(e => e.DateEmployed != null && e.DateEmployed <= cutoff);
        }
        if (criteria.MinAge.HasValue)
        {
            var maxDob = today.AddYears(-criteria.MinAge.Value);
            query = query.Where(e => e.DateOfBirth != null && e.DateOfBirth <= maxDob);
        }
        if (criteria.MaxAge.HasValue)
        {
            var minDob = today.AddYears(-(criteria.MaxAge.Value + 1));
            query = query.Where(e => e.DateOfBirth != null && e.DateOfBirth > minDob);
        }

        var employees = await query.OrderBy(e => e.LastName).Take(WorkingSetCap).ToListAsync(cancellationToken);

        // ── 2. Exclusions ─────────────────────────────────────────────────────────
        if (criteria.ExcludePoolId.HasValue)
        {
            var memberIds = (await _poolMembers.GetQueryable()
                .Where(m => m.TenantId == tenantId && m.TalentPoolId == criteria.ExcludePoolId && m.IsActive && !m.IsDeleted)
                .Select(m => m.EmployeeId).ToListAsync(cancellationToken)).ToHashSet();
            employees = employees.Where(e => !memberIds.Contains(e.Id)).ToList();
        }
        if (criteria.ExcludePlanId.HasValue)
        {
            var candidateIds = (await _candidates.GetQueryable()
                .Where(c => c.TenantId == tenantId && c.SuccessionPlanId == criteria.ExcludePlanId && !c.IsDeleted)
                .Select(c => c.EmployeeId).ToListAsync(cancellationToken)).ToHashSet();
            employees = employees.Where(e => !candidateIds.Contains(e.Id)).ToList();
        }

        if (employees.Count == 0)
            return new List<SuccessionCandidateSearchResultDto>();

        var empIds = employees.Select(e => e.Id).ToList();

        // ── 3. Target position requirements ────────────────────────────────────────
        var requiredCompetencies = new Dictionary<Guid, int>();
        var requiredSkills = new Dictionary<Guid, int>();
        if (criteria.TargetPositionId.HasValue)
        {
            requiredCompetencies = await _positionCompetencies.GetQueryable()
                .Where(pc => pc.TenantId == tenantId && pc.PositionId == criteria.TargetPositionId && !pc.IsDeleted)
                .ToDictionaryAsync(pc => pc.CompetencyId, pc => pc.RequiredProficiencyLevel, cancellationToken);

            requiredSkills = await _positionSkills.GetQueryable()
                .Where(ps => ps.TenantId == tenantId && ps.PositionId == criteria.TargetPositionId && !ps.IsDeleted)
                .ToDictionaryAsync(ps => ps.SkillId, ps => (int)ps.RequiredLevel, cancellationToken);
        }

        // ── 4. Bulk-load employee competencies & skills ────────────────────────────
        var employeeCompetencies = (await _employeeCompetencies.GetQueryable()
                .Where(ec => ec.TenantId == tenantId && empIds.Contains(ec.EmployeeId) && !ec.IsDeleted)
                .Select(ec => new { ec.EmployeeId, ec.CompetencyId, ec.CurrentProficiencyLevel })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.CompetencyId, x => x.CurrentProficiencyLevel));

        var employeeSkills = (await _employeeSkills.GetQueryable()
                .Where(es => es.TenantId == tenantId && empIds.Contains(es.EmployeeId) && !es.IsDeleted)
                .Select(es => new { es.EmployeeId, es.SkillId, es.SkillLevel })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.SkillId, x => (int)x.SkillLevel));

        // ── 5. Latest appraisal per employee ───────────────────────────────────────
        var latestAppraisal = (await _appraisals.GetQueryable()
                .Where(a => a.TenantId == tenantId && empIds.Contains(a.EmployeeId) && !a.IsDeleted && a.OverallScore != null
                            // A withdrawn appraisal has no result (performance closure E-d1).
                            && a.Status != ErpSystem.Core.Enums.AppraisalStatus.Withdrawn)
                .Select(a => new { a.EmployeeId, a.Year, a.OverallScore })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Year).First());

        // ── 6. Org unit names ──────────────────────────────────────────────────────
        var orgUnitIds = employees.Where(e => e.OrganizationUnitId.HasValue)
            .Select(e => e.OrganizationUnitId!.Value).Distinct().ToList();
        var orgUnitNames = orgUnitIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _orgUnits.GetQueryable()
                .Where(o => o.TenantId == tenantId && orgUnitIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        // ── 7. Score each candidate ────────────────────────────────────────────────
        var results = new List<SuccessionCandidateSearchResultDto>();
        foreach (var e in employees)
        {
            var comps = employeeCompetencies.GetValueOrDefault(e.Id) ?? new Dictionary<Guid, int>();
            var skills = employeeSkills.GetValueOrDefault(e.Id) ?? new Dictionary<Guid, int>();

            // Required-competency gate.
            if (criteria.RequiredCompetencyId.HasValue)
            {
                var have = comps.GetValueOrDefault(criteria.RequiredCompetencyId.Value, 0);
                if (have < (criteria.RequiredCompetencyMinLevel ?? 1))
                    continue;
            }

            latestAppraisal.TryGetValue(e.Id, out var appraisal);
            if (criteria.MinPerformanceScore.HasValue &&
                (appraisal?.OverallScore ?? decimal.MinValue) < criteria.MinPerformanceScore.Value)
                continue;

            // Gaps + fit pairs against the target position.
            int requirementCount = 0, gapCount = 0;
            var fitPairs = new List<(int required, int current)>();
            foreach (var kv in requiredCompetencies)
            {
                requirementCount++;
                var current = comps.GetValueOrDefault(kv.Key, 0);
                if (current < kv.Value) gapCount++;
                fitPairs.Add((kv.Value, current));
            }
            foreach (var kv in requiredSkills)
            {
                var current = skills.GetValueOrDefault(kv.Key, 0);
                fitPairs.Add((kv.Value, current));
            }
            var competencyFit = SuccessionFitScoring.CompetencyFitFraction(fitPairs);

            var age = HrPolicyCalculations.Age(e.DateOfBirth);
            var yearsLeft = HrPolicyCalculations.ServiceYearsLeft(settings, e);
            var retire = HrPolicyCalculations.RetirementDate(settings, e);
            var yearsOfService = e.YearsOfService;

            if (criteria.MinServiceYearsLeft.HasValue &&
                (yearsLeft ?? int.MaxValue) < criteria.MinServiceYearsLeft.Value)
                continue;

            var fit = SuccessionFitScoring.Score(new FitScoreInputs(
                Performance: SuccessionFitScoring.NormalizePerformance(appraisal?.OverallScore),
                CompetencyFit: competencyFit,
                Potential: null,
                Tenure: SuccessionFitScoring.NormalizeTenure(yearsOfService)), weights);

            results.Add(new SuccessionCandidateSearchResultDto
            {
                EmployeeId = e.Id,
                EmployeeName = e.FullName,
                EmployeeNumber = e.EmployeeNumber,
                PositionTitle = e.Position?.Title,
                OrganizationUnitName = e.OrganizationUnitId.HasValue
                    ? orgUnitNames.GetValueOrDefault(e.OrganizationUnitId.Value)
                    : null,
                Age = age,
                YearsOfService = yearsOfService,
                ServiceYearsLeft = yearsLeft,
                RetirementDate = retire?.ToDateTime(TimeOnly.MinValue),
                LatestPerformanceScore = appraisal?.OverallScore,
                LatestPerformanceYear = appraisal?.Year,
                FitScore = fit.Score,
                FitBand = fit.Band.ToString(),
                RequirementCount = requirementCount,
                GapCount = gapCount,
                CompetencyFitPercent = competencyFit.HasValue ? Math.Round(competencyFit.Value * 100, 1) : null,
            });
        }

        return results
            .OrderByDescending(r => r.FitScore)
            .ThenBy(r => r.EmployeeName)
            .Take(criteria.MaxResults)
            .ToList();
    }

    /// <summary>Builds fit-score weights from HR policy settings, falling back to defaults if all are zero.</summary>
    private static FitScoreWeights WeightsFrom(Entities.HR.CompanyHrPolicySettings settings)
    {
        if (settings.FitWeightPerformance + settings.FitWeightCompetency
            + settings.FitWeightPotential + settings.FitWeightTenure <= 0)
            return FitScoreWeights.Default;

        return new FitScoreWeights(
            Performance: settings.FitWeightPerformance,
            CompetencyFit: settings.FitWeightCompetency,
            Potential: settings.FitWeightPotential,
            Tenure: settings.FitWeightTenure);
    }
}
