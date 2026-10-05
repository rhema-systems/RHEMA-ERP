using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// The goals section of an appraisal (performance closure L2, D-15, D-16): one criterion snapshot
/// row per locked goal, built from the employee's goal set for the cycle, so what the employee and
/// the manager agreed is what the appraisal scores.
/// </summary>
public interface IAppraisalGoalRowService
{
    /// <summary>
    /// Brings the goal rows of every appraisal the employee has in the cycle, whose template has a
    /// goals section, into line with the locked goal set: rows added, re-weighted and removed. An
    /// appraisal with a goal row scored in a submitted evaluation is left as it is. Saves its own
    /// changes and returns how many rows it added, changed or removed.
    /// </summary>
    Task<int> RebuildAsync(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the goal's row has been scored in a submitted evaluation of any appraisal — such a
    /// goal cannot be unlocked.
    /// </summary>
    Task<bool> IsScoredAsync(Guid goalId, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AppraisalGoalRowService : IAppraisalGoalRowService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfig> _configRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfigGradeRange> _rangeRepository;
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IGenericRepository<CriterionScore> _scoreRepository;
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalGoalRowService> _logger;

    public AppraisalGoalRowService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfig> configRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfigGradeRange> rangeRepository,
        IGenericRepository<EmployeeGoal> goalRepository,
        IGenericRepository<CriterionScore> scoreRepository,
        IGenericRepository<AppraisalGradeDefinition> gradeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalGoalRowService> logger)
    {
        _appraisalRepository = appraisalRepository;
        _configRepository = configRepository;
        _rangeRepository = rangeRepository;
        _goalRepository = goalRepository;
        _scoreRepository = scoreRepository;
        _gradeRepository = gradeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// The periods a year-end appraisal scores: the full cycle, and the half and quarter that end
    /// with it (closure plan L7). A goal scoped to a period that ends earlier — Q1, Q2, H1, Q3 — is
    /// appraised at that period's interim review, and is not counted again at year end.
    /// </summary>
    internal static readonly GoalPeriod[] YearEndPeriods = [GoalPeriod.FullCycle, GoalPeriod.H2, GoalPeriod.Q4];

    /// <summary>
    /// Goal rows scored in a submitted evaluation. A draft does not count: a self-evaluation can be
    /// saved as a draft during goal setting, and a draft on the first locked goal must not stop the
    /// rest of the set reaching the appraisal.
    /// </summary>
    private IQueryable<CriterionScore> SubmittedScoresOn(IReadOnlyCollection<Guid> rowIds) =>
        _scoreRepository.GetQueryable()
            .Where(s => s.CriterionConfigId != null && rowIds.Contains(s.CriterionConfigId.Value)
                     && s.EvaluatorEvaluation.SubmittedDate != null && !s.EvaluatorEvaluation.IsDeleted);

    /// <inheritdoc />
    public async Task<int> RebuildAsync(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // The employee's appraisals in the cycle with a goals section on their template — not a
        // withdrawn one, whose goals section is the record of how far it got (performance closure E-d1).
        var appraisals = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.EmployeeId == employeeId && a.AppraisalCycleId == cycleId
                     && a.Status != AppraisalStatus.Withdrawn
                     && a.Template != null
                     && a.Template.Sections.Any(s => !s.IsDeleted && s.Kind == AppraisalSectionKind.EmployeeGoals))
            .Select(a => new
            {
                a.Id,
                Section = a.Template!.Sections
                    .Where(s => !s.IsDeleted && s.Kind == AppraisalSectionKind.EmployeeGoals)
                    .OrderBy(s => s.DisplayOrder)
                    .Select(s => new { s.Id, s.Weight })
                    .First(),
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (appraisals.Count == 0)
            return 0;

        // The locked set: live goals (not rejected) carrying the lock (D-29; the old status counts),
        // in the periods the year end scores (L7).
        var goals = (await _goalRepository.GetQueryable()
                .Where(g => g.TenantId == tenantId && g.EmployeeId == employeeId && g.AppraisalCycleId == cycleId
                         && g.Status != GoalStatus.Rejected
                         && (g.IsLocked || g.Status == GoalStatus.Locked)
                         && YearEndPeriods.Contains(g.Period))
                // A KPI-measured goal's tolerance is its KPI definition's (D-32, L6).
                .Include(g => g.KpiDefinition)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .OrderBy(g => g.CreatedAt)
            .ThenBy(g => g.Title)
            .ToList();

        var weights = NormalisedWeights(goals.Select(g => g.Weight).ToList());
        var scale = await TenantScaleAsync(tenantId, cancellationToken);

        var written = 0;
        foreach (var appraisal in appraisals)
        {
            var rows = await _configRepository.GetQueryable()
                .Include(c => c.GradeRanges)
                .Where(c => c.TenantId == tenantId && c.PerformanceAppraisalId == appraisal.Id && c.EmployeeGoalId != null)
                .ToListAsync(cancellationToken);

            // Once a goal row is scored in a submitted evaluation, the section is what was scored:
            // nothing is added, removed or re-weighted under it.
            var rowIds = rows.Select(r => r.Id).ToList();
            if (rowIds.Count > 0 && await SubmittedScoresOn(rowIds).AnyAsync(cancellationToken))
            {
                _logger.LogInformation(
                    "Goal rows of appraisal {AppraisalId} are scored; the goals section is left as it is", appraisal.Id);
                continue;
            }

            var rowsByGoal = rows.ToDictionary(r => r.EmployeeGoalId!.Value);
            for (var i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var measured = goal.TargetValue.HasValue;
                var isNew = !rowsByGoal.Remove(goal.Id, out var row);
                if (isNew)
                {
                    row = new PerformanceAppraisalCriterionConfig
                    {
                        TenantId = tenantId,
                        PerformanceAppraisalId = appraisal.Id,
                        EmployeeGoalId = goal.Id,
                    };
                    await _configRepository.AddAsync(row);
                }

                RowShape? before = isNew ? null : Shape(row!);
                row!.TemplateItemId = null;
                row.AppraisalTemplateSectionId = appraisal.Section.Id;
                row.SectionWeightUsed = appraisal.Section.Weight;
                row.WeightUsed = weights[i];
                row.ItemLabel = goal.Title.Length > 300 ? goal.Title[..300] : goal.Title;
                row.ScoringMethod = measured ? CriterionScoringMethod.Measured : CriterionScoringMethod.Rated;
                row.MeasurementType = goal.MeasurementType;
                row.Unit = goal.Unit;
                row.DisplayOrder = i + 1;
                row.KpiTargetValue = measured ? goal.TargetValue : null;
                row.KpiMinValue = measured ? goal.MinValue : null;
                row.KpiMaxValue = measured ? goal.MaxValue : null;
                // Kept with the target, as a template row keeps its KPI's (D-32): a goal measured on a KPI that
                // tolerates a 5 % miss scores a 96 against 100 as met; a goal with no KPI has an exact target.
                row.KpiTolerancePercent = measured ? goal.KpiDefinition?.TolerancePercent : null;
                row.KpiTargetSource = measured ? KpiTargetSource.Goal : null;

                // A rated goal is scored on the tenant's overall grade scale (D-16); a measured one
                // by its achievement against the target, shown against the same bands. The bands
                // are rewritten only when the scale moved, so a rebuild that changes nothing
                // leaves nothing behind.
                var liveRanges = row.GradeRanges.Where(r => !r.IsDeleted).ToList();
                var bandsChanged = isNew || !liveRanges
                    .Select(r => new Band(r.GradeDefinitionId, r.LowScore, r.HighScore))
                    .OrderBy(b => b.Low).ThenBy(b => b.GradeDefinitionId)
                    .SequenceEqual(scale.OrderBy(b => b.Low).ThenBy(b => b.GradeDefinitionId));
                if (bandsChanged)
                {
                    foreach (var range in liveRanges)
                        await _rangeRepository.DeleteAsync(range);
                    foreach (var band in scale)
                    {
                        await _rangeRepository.AddAsync(new PerformanceAppraisalCriterionConfigGradeRange
                        {
                            TenantId = tenantId,
                            CriterionConfig = row,
                            GradeDefinitionId = band.GradeDefinitionId,
                            LowScore = band.Low,
                            HighScore = band.High,
                        });
                    }
                }

                if (isNew || bandsChanged || before != Shape(row))
                    written++;
            }

            // Rows whose goal is no longer in the locked set, and any draft scored on them: a draft
            // is the only score such a row can carry here, and a score on a removed row would count
            // for nothing.
            foreach (var stale in rowsByGoal.Values)
            {
                foreach (var range in stale.GradeRanges.Where(r => !r.IsDeleted).ToList())
                    await _rangeRepository.DeleteAsync(range);
                foreach (var draft in await _scoreRepository.GetQueryable()
                             .Where(s => s.CriterionConfigId == stale.Id).ToListAsync(cancellationToken))
                    await _scoreRepository.DeleteAsync(draft);
                await _configRepository.DeleteAsync(stale);
                written++;
            }
        }

        if (written > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Goal rows rebuilt for employee {EmployeeId} in cycle {CycleId}: {Written} row(s) written across {Appraisals} appraisal(s)",
            employeeId, cycleId, written, appraisals.Count);
        return written;
    }

    /// <inheritdoc />
    public async Task<bool> IsScoredAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var rowIds = await _configRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && c.EmployeeGoalId == goalId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        return rowIds.Count > 0 && await SubmittedScoresOn(rowIds).AnyAsync(cancellationToken);
    }

    /// <summary>What a rebuild can change on a goal row, compared by value to tell a real change from a repeat.</summary>
    private readonly record struct RowShape(
        Guid? SectionId, int? SectionWeight, int Weight, string? Label, CriterionScoringMethod? Method,
        MeasurementType? Measure, string? Unit, int? Order, decimal? Target, decimal? Min, decimal? Max,
        decimal? Tolerance, KpiTargetSource? Source);

    private static RowShape Shape(PerformanceAppraisalCriterionConfig row) => new(
        row.AppraisalTemplateSectionId, row.SectionWeightUsed, row.WeightUsed, row.ItemLabel, row.ScoringMethod,
        row.MeasurementType, row.Unit, row.DisplayOrder, row.KpiTargetValue, row.KpiMinValue, row.KpiMaxValue,
        row.KpiTolerancePercent, row.KpiTargetSource);

    /// <summary>
    /// Goal weights normalised to 100 within the section, by largest remainder so they add to
    /// exactly 100. A locked set's weights already add to 100 (the set lock requires it); this
    /// matters for goals locked one by one. No weight at all splits evenly.
    /// </summary>
    internal static List<int> NormalisedWeights(IReadOnlyList<int> weights)
    {
        if (weights.Count == 0) return new List<int>();
        var total = weights.Sum(w => Math.Max(w, 0));
        var raw = total > 0
            ? weights.Select(w => Math.Max(w, 0) * 100m / total).ToList()
            : weights.Select(_ => 100m / weights.Count).ToList();

        var result = raw.Select(r => (int)Math.Floor(r)).ToList();
        var remainder = 100 - result.Sum();
        foreach (var index in raw
                     .Select((r, i) => (Fraction: r - Math.Floor(r), Index: i))
                     .OrderByDescending(x => x.Fraction).ThenBy(x => x.Index)
                     .Take(remainder)
                     .Select(x => x.Index))
        {
            result[index]++;
        }
        return result;
    }

    private sealed record Band(Guid GradeDefinitionId, int Low, int High);

    /// <summary>The tenant's overall grade scale as whole-number bands, lowest first; empty when none is set.</summary>
    private async Task<List<Band>> TenantScaleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var grades = await _gradeRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId && g.IsActive && g.OverallMinScore != null)
            .OrderBy(g => g.OverallMinScore)
            .Select(g => new { g.Id, g.OverallMinScore, g.OverallMaxScore })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return grades
            .Select(g => new Band(
                g.Id,
                (int)Math.Ceiling(g.OverallMinScore!.Value),
                (int)Math.Floor(g.OverallMaxScore ?? AppraisalScoring.MaxScore)))
            .Where(b => b.High >= b.Low)
            .ToList();
    }
}
