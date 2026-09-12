using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Simulates appraisal generation for a cycle and returns full coverage statistics.
/// Strictly read-only — no records are created or modified.
/// </summary>
public class CycleCoverageService : ICycleCoverageService
{
    private readonly IGenericRepository<AppraisalCycle> _cycleRepo;
    private readonly IGenericRepository<AppraisalCycleTarget> _targetRepo;
    private readonly IGenericRepository<AppraisalCycleTemplate> _cycleTemplateRepo;
    private readonly IGenericRepository<Employee> _employeeRepo;
    private readonly IGenericRepository<OrganizationUnit> _orgUnitRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<CycleCoverageService> _logger;

    public CycleCoverageService(
        IGenericRepository<AppraisalCycle> cycleRepo,
        IGenericRepository<AppraisalCycleTarget> targetRepo,
        IGenericRepository<AppraisalCycleTemplate> cycleTemplateRepo,
        IGenericRepository<Employee> employeeRepo,
        IGenericRepository<OrganizationUnit> orgUnitRepo,
        ICurrentUserProvider currentUserProvider,
        ILogger<CycleCoverageService> logger)
    {
        _cycleRepo = cycleRepo;
        _targetRepo = targetRepo;
        _cycleTemplateRepo = cycleTemplateRepo;
        _employeeRepo = employeeRepo;
        _orgUnitRepo = orgUnitRepo;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<CoveragePreviewDto> GetCoveragePreviewAsync(
        Guid cycleId,
        int pageNumber = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var cycle = await _cycleRepo.GetQueryable()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.TenantId == tenantId, cancellationToken);

        if (cycle == null)
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");

        // ──────────────────────────────────────────────────────────────
        // 1. Load active template assignments (with scope nav properties)
        // ──────────────────────────────────────────────────────────────
        var activeTemplates = await _cycleTemplateRepo.GetQueryable()
            .Where(ct => ct.AppraisalCycleId == cycleId && ct.TenantId == tenantId && ct.IsActive && !ct.IsDeleted)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .OrderByDescending(ct => ct.Priority)
            .ToListAsync(cancellationToken);

        // ──────────────────────────────────────────────────────────────
        // 2. Load active target groups
        // ──────────────────────────────────────────────────────────────
        var activeTargets = await _targetRepo.GetQueryable()
            .Where(t => t.AppraisalCycleId == cycleId && t.TenantId == tenantId && t.IsActive && !t.IsDeleted)
            .Include(t => t.Exclusions)
            .ToListAsync(cancellationToken);

        var result = new CoveragePreviewDto
        {
            CycleId = cycleId,
            CycleName = cycle.CycleName,
            HasActiveTemplates = activeTemplates.Count > 0,
            HasActiveTargets = activeTargets.Count > 0,
        };

        // ──────────────────────────────────────────────────────────────
        // 3. Resolve employee population from targets (inclusions then exclusions)
        // ──────────────────────────────────────────────────────────────
        // Resolved before the not-yet-configured bail below, because the scope overlaps only
        // need targets — and a cycle that has targets but no templates yet is exactly when a
        // clash with another cycle is most worth knowing about, while it is still being built.
        var rawIds = activeTargets.Count > 0
            ? await ResolveEmployeesInScopeAsync(activeTargets, tenantId, cancellationToken)
            : new HashSet<Guid>();
        var excludedWithReasons = await ApplyExclusionsAsync(activeTargets, rawIds, tenantId, cancellationToken);
        var includedIds = rawIds.Where(id => !excludedWithReasons.ContainsKey(id)).ToHashSet();
        var excludedIds = rawIds.Where(id => excludedWithReasons.ContainsKey(id)).ToHashSet();

        result.ScopeOverlaps = await ComputeScopeOverlapsAsync(cycle, includedIds, tenantId, cancellationToken);

        // Pre-flight: bail early if the cycle isn't configured yet
        if (!result.HasActiveTemplates || !result.HasActiveTargets || rawIds.Count == 0)
        {
            result.IsGenerationSafe = false;
            result.TotalTargetedEmployees = 0;
            result.PageNumber = pageNumber;
            result.PageSize = pageSize;
            result.TotalPages = 0;
            return result;
        }

        _logger.LogInformation(
            "CoveragePreview for cycle {CycleId}: {Included} included, {Excluded} excluded",
            cycleId, includedIds.Count, excludedIds.Count);

        // ──────────────────────────────────────────────────────────────
        // 4. Load employee details in a single batch query
        // ──────────────────────────────────────────────────────────────
        var employees = await _employeeRepo.GetQueryable()
            .Where(e => e.TenantId == tenantId && includedIds.Contains(e.Id) && !e.IsDeleted)
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.OrganizationLevel)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // ──────────────────────────────────────────────────────────────
        // 5. Simulate template resolution per included employee
        // ──────────────────────────────────────────────────────────────
        var coveredItems = employees
            .Select(emp => ResolveEmployeeCoverage(emp, activeTemplates))
            .ToList();

        // 5b. Build items for explicitly excluded employees
        List<EmployeeCoverageItemDto> excludedItems = new();
        if (excludedIds.Count > 0)
        {
            var excludedEmployees = await _employeeRepo.GetQueryable()
                .Where(e => e.TenantId == tenantId && excludedIds.Contains(e.Id) && !e.IsDeleted)
                .Include(e => e.Position)
                .Include(e => e.OrganizationUnit)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            excludedItems = excludedEmployees.Select(emp => new EmployeeCoverageItemDto
            {
                EmployeeId      = emp.Id,
                EmployeeName    = emp.FullName,
                EmployeeNumber  = emp.EmployeeNumber,
                PositionTitle   = emp.Position?.Title,
                UnitName        = emp.OrganizationUnit?.Name,
                Status          = EmployeeCoverageStatus.Excluded,
                ExclusionReason = excludedWithReasons.TryGetValue(emp.Id, out var r) ? r : "Excluded",
            }).ToList();
        }

        var allItems = coveredItems.Concat(excludedItems).ToList();

        // ──────────────────────────────────────────────────────────────
        // 6. Aggregate statistics
        // ──────────────────────────────────────────────────────────────
        result.TotalTargetedEmployees   = allItems.Count;
        result.EmployeesWithTemplate    = coveredItems.Count(i => i.Status == EmployeeCoverageStatus.Covered);
        result.EmployeesWithoutTemplate = coveredItems.Count(i => i.Status == EmployeeCoverageStatus.NoTemplate);
        result.ConflictCount            = coveredItems.Count(i => i.Status == EmployeeCoverageStatus.Conflict);
        result.ExcludedCount            = excludedItems.Count;
        var nonExcludedCount = result.TotalTargetedEmployees - result.ExcludedCount;
        result.CoveragePercentage = nonExcludedCount > 0
            ? Math.Round((decimal)result.EmployeesWithTemplate / nonExcludedCount * 100, 1)
            : 0m;
        result.IsGenerationSafe = result.EmployeesWithoutTemplate == 0 && result.ConflictCount == 0;

        // ──────────────────────────────────────────────────────────────
        // 7. Template breakdown (always full, not paged)
        // ──────────────────────────────────────────────────────────────
        result.TemplateBreakdown = BuildTemplateBreakdown(activeTemplates, allItems);

        // ──────────────────────────────────────────────────────────────
        // 8. Page the employee list
        // ──────────────────────────────────────────────────────────────
        result.PageNumber = pageNumber;
        result.PageSize = pageSize;
        result.TotalPages = (int)Math.Ceiling((double)allItems.Count / pageSize);
        result.Items = allItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Scope overlaps were computed up front, before the not-yet-configured bail.

        return result;
    }

    /// <summary>
    /// Other cycles of the same type and year competing for these employees.
    ///
    /// Opening is refused only by the Open / InProgress ones — that is what
    /// <c>AppraisalCycleService.OpenCycleAsync</c> enforces. Drafts are reported too but
    /// marked as non-blocking, so a clash with a colleague's half-built cycle is visible here
    /// while it is still cheap to fix, instead of stopping someone at the moment they open.
    /// </summary>
    private async Task<List<CycleScopeOverlapDto>> ComputeScopeOverlapsAsync(
        AppraisalCycle cycle,
        HashSet<Guid> includedIds,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var overlaps = new List<CycleScopeOverlapDto>();
        if (includedIds.Count == 0) return overlaps;

        var siblings = await _cycleRepo.GetQueryable()
            .Where(c => c.TenantId == tenantId
                     && c.Id != cycle.Id
                     && c.AppraisalType == cycle.AppraisalType
                     && c.Year == cycle.Year
                     && c.Status != AppraisalCycleStatus.Closed
                     && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var sibling in siblings)
        {
            var siblingTargets = await _targetRepo.GetQueryable()
                .Where(t => t.AppraisalCycleId == sibling.Id && t.TenantId == tenantId && t.IsActive && !t.IsDeleted)
                .Include(t => t.Exclusions)
                .ToListAsync(cancellationToken);

            if (siblingTargets.Count == 0) continue;

            var siblingRaw = await ResolveEmployeesInScopeAsync(siblingTargets, tenantId, cancellationToken);
            var siblingExcluded = await ApplyExclusionsAsync(siblingTargets, siblingRaw, tenantId, cancellationToken);
            var siblingScope = siblingRaw.Where(id => !siblingExcluded.ContainsKey(id));

            var shared = siblingScope.Count(includedIds.Contains);
            if (shared == 0) continue;

            overlaps.Add(new CycleScopeOverlapDto
            {
                CycleId = sibling.Id,
                CycleCode = sibling.CycleCode,
                CycleName = sibling.CycleName,
                Status = sibling.Status,
                SharedEmployeeCount = shared,
                BlocksOpening = sibling.Status is AppraisalCycleStatus.Open or AppraisalCycleStatus.InProgress,
            });
        }

        return overlaps
            .OrderByDescending(o => o.BlocksOpening)
            .ThenByDescending(o => o.SharedEmployeeCount)
            .ToList();
    }

    // ──────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Determines which template wins for a single employee using scope resolution:
    /// Position > OrgUnit > OrgLevel > Global.
    /// Within the winning scope, ties at the same priority level produce a Conflict.
    /// </summary>
    private static EmployeeCoverageItemDto ResolveEmployeeCoverage(
        Employee emp,
        List<AppraisalCycleTemplate> assignments)
    {
        var item = new EmployeeCoverageItemDto
        {
            EmployeeId = emp.Id,
            EmployeeName = emp.FullName,
            EmployeeNumber = emp.EmployeeNumber,
            PositionTitle = emp.Position?.Title,
            UnitName = emp.OrganizationUnit?.Name,
        };

        // Bucket by scope specificity
        var positionMatches = assignments
            .Where(ct => ct.AppraisalTemplate.PositionId.HasValue
                      && ct.AppraisalTemplate.PositionId == emp.PositionId)
            .ToList();

        var unitMatches = assignments
            .Where(ct => ct.AppraisalTemplate.OrganizationUnitId.HasValue
                      && ct.AppraisalTemplate.OrganizationUnitId == emp.OrganizationUnitId
                      && ct.AppraisalTemplate.PositionId == null)
            .ToList();

        var levelMatches = assignments
            .Where(ct => ct.AppraisalTemplate.OrganizationLevelId.HasValue
                      && ct.AppraisalTemplate.OrganizationUnitId == null
                      && ct.AppraisalTemplate.PositionId == null)
            .ToList();

        var globalMatches = assignments
            .Where(ct => ct.AppraisalTemplate.OrganizationLevelId == null
                      && ct.AppraisalTemplate.OrganizationUnitId == null
                      && ct.AppraisalTemplate.PositionId == null)
            .ToList();

        List<AppraisalCycleTemplate> bestMatches;
        string scopeType;

        if (positionMatches.Count > 0)        { bestMatches = positionMatches; scopeType = "Position"; }
        else if (unitMatches.Count > 0)        { bestMatches = unitMatches;     scopeType = "OrgUnit"; }
        else if (levelMatches.Count > 0)       { bestMatches = levelMatches;    scopeType = "OrgLevel"; }
        else if (globalMatches.Count > 0)      { bestMatches = globalMatches;   scopeType = "Global"; }
        else
        {
            item.Status = EmployeeCoverageStatus.NoTemplate;
            return item;
        }

        // Within the best scope bucket, check for priority ties
        var topPriority = bestMatches.Max(m => m.Priority);
        var topTied = bestMatches.Where(m => m.Priority == topPriority).ToList();

        if (topTied.Count > 1)
        {
            item.Status = EmployeeCoverageStatus.Conflict;
            item.SourceScope = scopeType;
            item.TemplatePriority = topPriority;
            item.ConflictingTemplateNames = topTied
                .Select(m => m.AppraisalTemplate.TemplateName)
                .ToList();
            return item;
        }

        var resolved = topTied[0];
        item.Status = EmployeeCoverageStatus.Covered;
        item.ResolvedTemplateId = resolved.AppraisalTemplateId;
        item.ResolvedTemplateName = resolved.AppraisalTemplate.TemplateName;
        item.SourceScope = scopeType;
        item.TemplatePriority = resolved.Priority;
        return item;
    }

    private static List<TemplateCoverageBreakdownDto> BuildTemplateBreakdown(
        List<AppraisalCycleTemplate> assignments,
        List<EmployeeCoverageItemDto> items)
    {
        return assignments.Select(ct =>
        {
            var t = ct.AppraisalTemplate;
            var scopeType = t.PositionId.HasValue      ? "Position"
                          : t.OrganizationUnitId.HasValue  ? "OrgUnit"
                          : t.OrganizationLevelId.HasValue ? "OrgLevel"
                          : "Global";
            var scopeName = t.Position?.Title
                          ?? t.OrganizationUnit?.Name
                          ?? t.OrganizationLevel?.Name
                          ?? "All Employees";

            return new TemplateCoverageBreakdownDto
            {
                TemplateId = ct.AppraisalTemplateId,
                TemplateName = t.TemplateName,
                Priority = ct.Priority,
                ScopeType = scopeType,
                ScopeName = scopeName,
                AssignedEmployeeCount = items.Count(i => i.ResolvedTemplateId == ct.AppraisalTemplateId),
            };
        })
        .OrderByDescending(b => b.Priority)
        .ThenBy(b => b.TemplateName)
        .ToList();
    }

    private async Task<HashSet<Guid>> ResolveEmployeesInScopeAsync(
        List<AppraisalCycleTarget> targets,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var set = new HashSet<Guid>();

        foreach (var target in targets)
        {
            switch (target.TargetType)
            {
                case AppraisalTargetType.Position:
                    if (target.PositionId.HasValue)
                    {
                        var ids = await _employeeRepo.GetQueryable()
                            .Where(e => e.TenantId == tenantId
                                     && e.PositionId == target.PositionId
                                     && !e.IsDeleted
                                     && e.IsActive)
                            .Select(e => e.Id)
                            .ToListAsync(cancellationToken);
                        foreach (var id in ids) set.Add(id);
                    }
                    break;

                case AppraisalTargetType.OrganizationUnit:
                    if (target.OrganizationUnitId.HasValue)
                    {
                        var childIds = await GetChildUnitIdsAsync(
                            target.OrganizationUnitId.Value, tenantId, cancellationToken);
                        childIds.Add(target.OrganizationUnitId.Value);

                        var ids = await _employeeRepo.GetQueryable()
                            .Where(e => e.TenantId == tenantId
                                     && e.OrganizationUnitId.HasValue
                                     && childIds.Contains(e.OrganizationUnitId.Value)
                                     && !e.IsDeleted
                                     && e.IsActive)
                            .Select(e => e.Id)
                            .ToListAsync(cancellationToken);
                        foreach (var id in ids) set.Add(id);
                    }
                    break;

                case AppraisalTargetType.OrganizationLevel:
                    if (target.OrganizationLevelId.HasValue)
                    {
                        var ids = await _employeeRepo.GetQueryable()
                            .Where(e => e.TenantId == tenantId
                                     && e.OrganizationLevelId == target.OrganizationLevelId
                                     && !e.IsDeleted
                                     && e.IsActive)
                            .Select(e => e.Id)
                            .ToListAsync(cancellationToken);
                        foreach (var id in ids) set.Add(id);
                    }
                    break;
            }
        }

        return set;
    }

    /// <summary>
    /// Returns a dictionary mapping employee IDs (from rawSet) to their exclusion reason
    /// based on the active exclusion rules on each target.
    /// </summary>
    private async Task<Dictionary<Guid, string>> ApplyExclusionsAsync(
        List<AppraisalCycleTarget> targets,
        HashSet<Guid> rawSet,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (rawSet.Count == 0)
            return new Dictionary<Guid, string>();

        var allExclusions = targets
            .SelectMany(t => t.Exclusions)
            .Where(e => e.IsActive && !e.IsDeleted)
            .ToList();

        if (allExclusions.Count == 0)
            return new Dictionary<Guid, string>();

        var excludedWithReasons = new Dictionary<Guid, string>();

        // 1. Direct employee exclusions
        foreach (var ex in allExclusions.Where(e => e.EmployeeId.HasValue))
        {
            if (rawSet.Contains(ex.EmployeeId!.Value))
                excludedWithReasons.TryAdd(ex.EmployeeId.Value, ex.Reason);
        }

        // 2. Position exclusions
        var posExclusions = allExclusions
            .Where(e => e.PositionId.HasValue && !e.EmployeeId.HasValue)
            .ToList();
        if (posExclusions.Count > 0)
        {
            var posIds = posExclusions.Select(e => e.PositionId!.Value).Distinct().ToHashSet();
            var matched = await _employeeRepo.GetQueryable()
                .Where(e => e.TenantId == tenantId && posIds.Contains(e.PositionId) && rawSet.Contains(e.Id) && !e.IsDeleted)
                .Select(e => new { e.Id, e.PositionId })
                .ToListAsync(cancellationToken);
            foreach (var emp in matched)
            {
                var reason = posExclusions.First(ex => ex.PositionId == emp.PositionId).Reason;
                excludedWithReasons.TryAdd(emp.Id, reason);
            }
        }

        // 3. Organization unit exclusions (including child units)
        var unitExclusions = allExclusions
            .Where(e => e.OrganizationUnitId.HasValue && !e.EmployeeId.HasValue && !e.PositionId.HasValue)
            .ToList();
        if (unitExclusions.Count > 0)
        {
            var unitIdToReason = new Dictionary<Guid, string>();
            foreach (var ex in unitExclusions)
            {
                var rootId = ex.OrganizationUnitId!.Value;
                unitIdToReason.TryAdd(rootId, ex.Reason);
                var childIds = await GetChildUnitIdsAsync(rootId, tenantId, cancellationToken);
                foreach (var childId in childIds)
                    unitIdToReason.TryAdd(childId, ex.Reason);
            }
            var allExcludedUnitIds = unitIdToReason.Keys.ToHashSet();
            var matched = await _employeeRepo.GetQueryable()
                .Where(e => e.TenantId == tenantId
                         && e.OrganizationUnitId.HasValue
                         && allExcludedUnitIds.Contains(e.OrganizationUnitId.Value)
                         && rawSet.Contains(e.Id)
                         && !e.IsDeleted)
                .Select(e => new { e.Id, e.OrganizationUnitId })
                .ToListAsync(cancellationToken);
            foreach (var emp in matched)
            {
                var reason = unitIdToReason.TryGetValue(emp.OrganizationUnitId!.Value, out var r)
                    ? r : unitExclusions[0].Reason;
                excludedWithReasons.TryAdd(emp.Id, reason);
            }
        }

        // 4. Organization level exclusions
        var levelExclusions = allExclusions
            .Where(e => e.OrganizationLevelId.HasValue
                     && !e.EmployeeId.HasValue
                     && !e.PositionId.HasValue
                     && !e.OrganizationUnitId.HasValue)
            .ToList();
        if (levelExclusions.Count > 0)
        {
            var levelIds = levelExclusions.Select(e => e.OrganizationLevelId!.Value).Distinct().ToHashSet();
            var matched = await _employeeRepo.GetQueryable()
                .Where(e => e.TenantId == tenantId
                         && e.OrganizationLevelId.HasValue
                         && levelIds.Contains(e.OrganizationLevelId.Value)
                         && rawSet.Contains(e.Id)
                         && !e.IsDeleted)
                .Select(e => new { e.Id, e.OrganizationLevelId })
                .ToListAsync(cancellationToken);
            foreach (var emp in matched)
            {
                var reason = levelExclusions.First(ex => ex.OrganizationLevelId == emp.OrganizationLevelId).Reason;
                excludedWithReasons.TryAdd(emp.Id, reason);
            }
        }

        return excludedWithReasons;
    }

    private async Task<HashSet<Guid>> GetChildUnitIdsAsync(
        Guid parentUnitId, Guid tenantId, CancellationToken cancellationToken)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentUnitId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = await _orgUnitRepo.GetQueryable()
                .Where(u => u.TenantId == tenantId && u.ParentUnitId == current && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var child in children)
            {
                if (result.Add(child))
                    queue.Enqueue(child);
            }
        }

        return result;
    }
}
