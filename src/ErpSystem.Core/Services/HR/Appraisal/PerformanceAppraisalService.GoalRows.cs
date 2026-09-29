using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Goal-driven scoring on the appraisal's forms (performance closure lane L3): a score found by its
/// criterion rather than its template item, and the year-end goal assessments fed from the goal
/// rows. The rows a form shows are <see cref="CriterionTemplateKey.FormRows"/>.
/// </summary>
public partial class PerformanceAppraisalService
{
    /// <summary>
    /// An evaluation's score for one criterion: by template item on a template row — the key every
    /// score written before lane L carries — and by snapshot row on a goal row, which has no
    /// template item.
    /// </summary>
    private Task<CriterionScore?> FindScoreAsync(Guid evaluationId, CriterionRef criterion, CancellationToken cancellationToken)
    {
        var query = TenantCriterionScoreQuery().Where(cs => cs.EvaluatorEvaluationId == evaluationId);
        query = criterion.TemplateItemId is Guid templateItemId
            ? query.Where(cs => cs.TemplateItemId == templateItemId)
            : query.Where(cs => cs.CriterionConfigId == criterion.CriterionConfigId);
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Saves one side's year-end goal assessments — the employee's or the manager's — and mirrors
    /// into them the goal rows that side scored in this save. Returns the message to show when an
    /// assessment names a goal outside the employee's goal set for the cycle, or null. Adds and
    /// changes rows on the unit of work; the caller saves.
    /// </summary>
    /// <remarks>
    /// <para>A goal row's achievement is entered once, as its score (lane L3), and the assessment
    /// carries it: the actual and the percentage come from the score and win over the panel's; the
    /// status, notes and evidence stay the panel's.</para>
    /// <para>⚠ The assessments used to be saved for any goal id in the payload, another
    /// employee's included — the goal set is by employee and cycle, as the goal rows are.</para>
    /// </remarks>
    private async Task<string?> SaveGoalAssessmentsAsync(
        PerformanceAppraisal appraisal,
        bool managerSide,
        IReadOnlyCollection<GoalAssessmentInputDto>? inputs,
        IReadOnlyCollection<CriterionScore> scoredThisSave,
        AppraisalCriterionScoring scoring,
        CancellationToken cancellationToken)
    {
        inputs ??= Array.Empty<GoalAssessmentInputDto>();
        var goalRowScores = scoredThisSave
            .Select(score => (Score: score, Row: score.CriterionConfigId is Guid id ? scoring.GoalRow(id) : null))
            .Where(x => x.Row?.EmployeeGoalId != null)
            .Select(x => (x.Score, Row: x.Row!, GoalId: x.Row!.EmployeeGoalId!.Value))
            .ToList();
        if (inputs.Count == 0 && goalRowScores.Count == 0)
            return null;

        var goals = await _goalRepository.GetQueryable()
            .Where(g => g.TenantId == appraisal.TenantId
                     && g.EmployeeId == appraisal.EmployeeId
                     && g.AppraisalCycleId == appraisal.AppraisalCycleId)
            .AsNoTracking()
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        if (inputs.Any(input => !goals.ContainsKey(input.GoalId)))
            return "A goal assessment names a goal that is not in this employee's goal set for the cycle.";

        var goalIds = inputs.Select(input => input.GoalId).Concat(goalRowScores.Select(x => x.GoalId)).Distinct().ToList();
        var assessments = await _goalAssessmentRepository.GetQueryable()
            .Where(a => a.PerformanceAppraisalId == appraisal.Id && goalIds.Contains(a.EmployeeGoalId))
            .ToDictionaryAsync(a => a.EmployeeGoalId, cancellationToken);

        // A row that existed is marked updated once; a new one is only added — calling UpdateAsync
        // on it would turn its insert into an update of a row that is not there.
        var existingIds = assessments.Keys.ToHashSet();
        async Task<EmployeeGoalAppraisalAssessment> AssessmentFor(Guid goalId)
        {
            if (assessments.TryGetValue(goalId, out var existing))
                return existing;
            var created = new EmployeeGoalAppraisalAssessment
            {
                TenantId = appraisal.TenantId,
                EmployeeGoalId = goalId,
                PerformanceAppraisalId = appraisal.Id,
            };
            await _goalAssessmentRepository.AddAsync(created);
            assessments[goalId] = created;
            return created;
        }

        foreach (var input in inputs)
        {
            // FinalProgressPercent is derived on the server for a KPI goal, so the stored value
            // reflects the goal's measurement semantics rather than where a client slider sat.
            var goal = goals[input.GoalId];
            var progress = input.FinalProgressPercent;
            if (goal.KpiDefinitionId.HasValue && goal.MeasurementType != MeasurementType.Boolean && input.FinalActualValue.HasValue)
                progress = CalculateKpiAchievement(input.FinalActualValue.Value, goal.TargetValue, goal.MinValue, goal.MaxValue);

            var assessment = await AssessmentFor(input.GoalId);
            if (managerSide)
            {
                assessment.ManagerFinalProgressPercent = progress;
                assessment.ManagerFinalStatus = input.FinalStatus;
                assessment.ManagerFinalActualValue = input.FinalActualValue;
                assessment.ManagerAssessmentNotes = input.AssessmentNotes;
                assessment.ManagerEvidenceLinks = input.EvidenceLinks;
            }
            else
            {
                assessment.SelfFinalProgressPercent = progress;
                assessment.SelfFinalStatus = input.FinalStatus;
                assessment.SelfFinalActualValue = input.FinalActualValue;
                assessment.SelfAssessmentNotes = input.AssessmentNotes;
                assessment.SelfEvidenceLinks = input.EvidenceLinks;
            }
        }

        foreach (var (score, row, goalId) in goalRowScores)
        {
            var achievement = scoring.AchievementPercent(score) is decimal percent
                ? Math.Round(percent, 2, MidpointRounding.AwayFromZero)
                : (decimal?)null;

            // A rated goal has no actual; the panel's stands.
            var assessment = await AssessmentFor(goalId);
            if (managerSide)
            {
                if (row.IsMeasured()) assessment.ManagerFinalActualValue = score.ActualValue;
                assessment.ManagerFinalProgressPercent = achievement;
            }
            else
            {
                if (row.IsMeasured()) assessment.SelfFinalActualValue = score.ActualValue;
                assessment.SelfFinalProgressPercent = achievement;
            }
        }

        foreach (var goalId in existingIds)
            await _goalAssessmentRepository.UpdateAsync(assessments[goalId]);

        return null;
    }
}
