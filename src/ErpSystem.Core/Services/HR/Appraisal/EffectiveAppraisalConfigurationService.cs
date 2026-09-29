using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Resolves the effective appraisal configuration (criteria, weights, grade bands) for
/// an employee in a given cycle from the appraisal template.
///
/// A template KPI item's target is the template's, the same for everyone on it. A locked goal
/// on the same KPI no longer replaces it (performance closure L0): goals are scored as their own
/// rows under goal-driven scoring, and the override would count them twice.
/// </summary>
public class EffectiveAppraisalConfigurationService : IEffectiveAppraisalConfigurationService
{
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<AppraisalCycleTemplate> _cycleTemplateRepository;
    private readonly IGenericRepository<AppraisalTemplate> _templateRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfig> _criterionConfigRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EffectiveAppraisalConfigurationService> _logger;

    public EffectiveAppraisalConfigurationService(
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<AppraisalCycleTemplate> cycleTemplateRepository,
        IGenericRepository<AppraisalTemplate> templateRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfig> criterionConfigRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EffectiveAppraisalConfigurationService> logger)
    {
        _employeeRepository = employeeRepository;
        _cycleRepository = cycleRepository;
        _cycleTemplateRepository = cycleTemplateRepository;
        _templateRepository = templateRepository;
        _appraisalRepository = appraisalRepository;
        _criterionConfigRepository = criterionConfigRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<Employee> GetOwnedEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var employee = await _employeeRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId, cancellationToken);
        if (employee == null)
            throw new ArgumentException($"Employee {employeeId} not found");
        return employee;
    }

    private async Task<AppraisalCycle> GetOwnedCycleAsync(Guid cycleId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var cycle = await _cycleRepository.GetQueryable()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.TenantId == tenantId, cancellationToken);
        if (cycle == null)
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");
        return cycle;
    }

    /// <inheritdoc/>
    public async Task<EffectiveAppraisalConfigDto> ResolveForEmployeeAsync(
        Guid employeeId,
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId, cancellationToken);

        // 1. Load employee (need PositionId, OrganizationUnitId, OrganizationLevelId)
        var employee = await GetOwnedEmployeeAsync(employeeId, cancellationToken);

        // 2. Resolve the template for this employee in this cycle
        var resolvedTemplate = await ResolveTemplateAsync(employee, cycleId, cancellationToken);

        // 3. Load the template with its full hierarchy
        var template = await LoadTemplateAsync(resolvedTemplate.AppraisalTemplateId, cancellationToken);

        // 4. Build the effective config
        return BuildEffectiveConfig(employeeId, cycleId, template);
    }

    /// <inheritdoc/>
    public async Task SnapshotConfigAsync(
        Guid performanceAppraisalId,
        Guid employeeId,
        Guid cycleId,
        Guid? templateId = null,
        CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId, cancellationToken);
        var employee = await GetOwnedEmployeeAsync(employeeId, cancellationToken);
        var tenantId = GetTenantId();

        // If the caller supplies a templateId (e.g. from GenerateAppraisalsAsync), use it
        // directly to anchor the snapshot.  Otherwise fall back to full resolution.
        Guid effectiveTemplateId;
        if (templateId.HasValue)
        {
            effectiveTemplateId = templateId.Value;
            _logger.LogDebug("SnapshotConfig: using caller-supplied template {templateId} for appraisal {appraisalId}",
                effectiveTemplateId, performanceAppraisalId);
        }
        else
        {
            var resolved = await ResolveTemplateAsync(employee, cycleId, cancellationToken);
            effectiveTemplateId = resolved.AppraisalTemplateId;
        }

        var template = await LoadTemplateAsync(effectiveTemplateId, cancellationToken);

        var effectiveConfig = BuildEffectiveConfig(employeeId, cycleId, template);

        foreach (var criterion in effectiveConfig.Criteria)
        {
            // TenantId has to be stamped explicitly on both the config and its grade ranges:
            // the DbContext is registered without a tenant, so its auto-stamp is inert and the
            // rows would go in with Guid.Empty, failing the FK to Tenants with SQL 547. This
            // is the only write path for a criterion-config snapshot, so every generation ran
            // into it.
            var config = new PerformanceAppraisalCriterionConfig
            {
                TenantId = tenantId,
                PerformanceAppraisalId = performanceAppraisalId,
                TemplateItemId = criterion.TemplateItemId,
                WeightUsed = criterion.Weight,
                // Frozen with the item weight (performance closure A0): a section weight edited
                // after generation must not re-score an appraisal already under way.
                AppraisalTemplateSectionId = criterion.AppraisalTemplateSectionId,
                SectionWeightUsed = criterion.SectionWeight,
                KpiTargetValue = criterion.KpiTargetValue,
                KpiMinValue = criterion.KpiMinValue,
                KpiMaxValue = criterion.KpiMaxValue,
                KpiTargetSource = criterion.KpiTargetSource,
                GradeRanges = criterion.GradeRanges.Select(gr => new PerformanceAppraisalCriterionConfigGradeRange
                {
                    TenantId = tenantId,
                    GradeDefinitionId = gr.GradeDefinitionId,
                    LowScore = gr.LowScore,
                    HighScore = gr.HighScore
                }).ToList()
            };

            await _criterionConfigRepository.AddAsync(config);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Criterion config snapshot created for appraisal {appraisalId}", performanceAppraisalId);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<AppraisalTemplate> LoadTemplateAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return await _templateRepository.GetQueryable()
            .Where(t => t.Id == templateId && t.TenantId == tenantId)
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
                    .ThenInclude(ti => ti.Competency)
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
                    .ThenInclude(ti => ti.GradeRanges)
                        .ThenInclude(gr => gr.GradeDefinition)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Template {templateId} not found");
    }

    /// <summary>
    /// Resolves the highest-priority matching AppraisalCycleTemplate for this employee.
    /// Scope is read from the linked AppraisalTemplate (not from the CycleTemplate record).
    /// Priority field wins. Ties at the same scope level throw.
    /// </summary>
    private async Task<AppraisalCycleTemplate> ResolveTemplateAsync(
        Employee employee,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        // Load all active templates for the cycle with their scope navigations.
        var allForCycle = await _cycleTemplateRepository
            .GetQueryable()
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId && t.IsActive)
            .Include(t => t.AppraisalTemplate)
            .ToListAsync(cancellationToken);

        // Scope cascade: Position > OrganizationUnit > OrganizationLevel > Global
        var positionMatches = allForCycle.Where(t =>
            t.AppraisalTemplate.PositionId.HasValue &&
            t.AppraisalTemplate.PositionId == employee.PositionId).ToList();

        var unitMatches = allForCycle.Where(t =>
            t.AppraisalTemplate.OrganizationUnitId.HasValue &&
            t.AppraisalTemplate.OrganizationUnitId == employee.OrganizationUnitId &&
            t.AppraisalTemplate.PositionId == null).ToList();

        var levelMatches = allForCycle.Where(t =>
            t.AppraisalTemplate.OrganizationLevelId.HasValue &&
            t.AppraisalTemplate.OrganizationLevelId == employee.OrganizationLevelId &&
            t.AppraisalTemplate.OrganizationUnitId == null &&
            t.AppraisalTemplate.PositionId == null).ToList();

        var globalMatches = allForCycle.Where(t =>
            t.AppraisalTemplate.PositionId == null &&
            t.AppraisalTemplate.OrganizationUnitId == null &&
            t.AppraisalTemplate.OrganizationLevelId == null).ToList();

        var bestTier = positionMatches.Count > 0 ? positionMatches
                     : unitMatches.Count > 0     ? unitMatches
                     : levelMatches.Count > 0    ? levelMatches
                     : globalMatches.Count > 0   ? globalMatches
                     : null;

        if (bestTier == null)
            throw new InvalidOperationException(
                $"No active AppraisalCycleTemplate found for employee {employee.Id} in cycle {cycleId}.");

        // Within the winning scope tier, use Priority as tiebreaker.
        var topPriority = bestTier.Max(t => t.Priority);
        var topTied = bestTier.Where(t => t.Priority == topPriority).ToList();

        if (topTied.Count > 1)
            throw new InvalidOperationException(
                $"Ambiguous template: {topTied.Count} AppraisalCycleTemplates match employee {employee.Id} " +
                $"at the same scope tier and priority ({topPriority}) in cycle {cycleId}.");

        _logger.LogDebug("Resolved template {templateId} for employee {employeeId}",
            topTied[0].AppraisalTemplateId, employee.Id);

        return topTied[0];
    }

    /// <summary>
    /// Flattens the template items into an EffectiveAppraisalConfigDto, resolving each
    /// KPI target from the employee's locked goal first, then the template item default.
    /// </summary>
    private static EffectiveAppraisalConfigDto BuildEffectiveConfig(
        Guid employeeId,
        Guid cycleId,
        AppraisalTemplate template)
    {
        var allItems = template.Sections
            .SelectMany(s => s.TemplateItems.Select(item => (Section: s, Item: item)))
            .ToList();

        var criteria = new List<EffectiveAppraisalCriterionDto>();

        foreach (var (section, item) in allItems)
        {
            // Skip items with no source (e.g. pure custom questions with no competency or KPI)
            if (item.CompetencyId == null && item.KpiDefinitionId == null) continue;

            var gradeRanges = item.GradeRanges.Select(g => new EffectiveGradeRangeDto
            {
                GradeDefinitionId = g.GradeDefinitionId,
                GradeName = g.GradeDefinition.GradeName,
                LowScore = g.LowScore,
                HighScore = g.HighScore
            }).ToList();

            // KPI target resolution (only applies when item is linked to a KPI definition)
            decimal? kpiTargetValue = null;
            decimal? kpiMinValue = null;
            decimal? kpiMaxValue = null;
            KpiTargetSource? kpiTargetSource = null;

            // A template KPI item is the same KPI with the same target for everyone on the template
            // (closure plan L0, D-15). A locked goal on the same KPI used to replace the target
            // here ("Tier 1"); under goal-driven scoring that goal is scored as its own row, so it
            // would count twice. Personal targets are goals.
            if (item.KpiDefinitionId.HasValue)
            {
                if (item.KpiTargetValue != null)
                {
                    kpiTargetValue = item.KpiTargetValue;
                    kpiMinValue = item.KpiMinValue;
                    kpiMaxValue = item.KpiMaxValue;
                    kpiTargetSource = KpiTargetSource.Template;
                }
            }

            criteria.Add(new EffectiveAppraisalCriterionDto
            {
                TemplateItemId = item.Id,
                CompetencyId = item.CompetencyId,
                KpiDefinitionId = item.KpiDefinitionId,
                ItemName = item.Competency?.CriteriaName ?? item.KpiDefinition?.KpiName ?? string.Empty,
                Weight = item.Weight,
                AppraisalTemplateSectionId = section.Id,
                SectionWeight = section.Weight,
                KpiTargetValue = kpiTargetValue,
                KpiMinValue = kpiMinValue,
                KpiMaxValue = kpiMaxValue,
                KpiTargetSource = kpiTargetSource,
                GradeRanges = gradeRanges
            });
        }

        return new EffectiveAppraisalConfigDto
        {
            EmployeeId = employeeId,
            AppraisalCycleId = cycleId,
            ResolvedTemplateId = template.Id,
            ResolvedTemplateName = template.TemplateName,
            Criteria = criteria
        };
    }
}
