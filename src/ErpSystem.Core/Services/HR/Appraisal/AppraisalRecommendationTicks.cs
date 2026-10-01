using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The line manager's recommendation ticks, turned into outcome recommendation rows (performance closure F2, D-92,
/// D-100). A tick on its own was a flag nobody acted on — the dashboards counted it and nothing followed; the row is
/// what HR decides and what dispatches to the owning module.
/// </summary>
/// <remarks>
/// <para>Run at every submission of the manager's evaluation — the first, a re-submission after HR's return or an
/// appeal remand, and HR's advance of the manager's draft — and staged into the caller's unit of work, so the rows
/// commit with the evaluation:</para>
/// <list type="bullet">
/// <item>a ticked type with no open row (Proposed, Approved or Actioned — the unique index's set) gets a Proposed row
/// recommended by the manager;</item>
/// <item>a type the manager has unticked since loses its row only while that row is still Proposed and the manager
/// recommended it: dismissed, with the reason. An approved or actioned row has been decided, and a row HR proposed by
/// hand is HR's, so neither is touched.</item>
/// </list>
/// </remarks>
public static class AppraisalRecommendationTicks
{
    /// <summary>Each tick and the recommendation type it raises (D-92).</summary>
    public static readonly IReadOnlyList<(Func<PerformanceAppraisal, bool> Ticked, RecommendationType Type)> Map =
    [
        (a => a.RecommendPromotion, RecommendationType.Promotion),
        (a => a.RecommendIncrement, RecommendationType.MeritIncrease),
        (a => a.RecommendTraining, RecommendationType.TrainingNomination),
        (a => a.RecommendPIP, RecommendationType.PerformanceImprovementPlan),
        (a => a.RecommendTermination, RecommendationType.Termination),
        (a => a.RecommendAward, RecommendationType.Recognition),
    ];

    /// <summary>The statuses that hold a type on an appraisal — batch 2's unique index, <c>Status IN (1, 2, 3)</c>.</summary>
    public static bool IsOpen(RecommendationStatus status)
        => status is RecommendationStatus.Proposed or RecommendationStatus.Approved or RecommendationStatus.Actioned;

    private const int NotesMaxLength = 2000;
    private const int ResolutionNotesMaxLength = 1000;

    /// <summary>
    /// Stages the rows the appraisal's ticks call for. The caller saves. Returns how many rows were added and dismissed.
    /// </summary>
    public static async Task<(int Added, int Dismissed)> StageAsync(
        IGenericRepository<AppraisalOutcomeRecommendation> repository,
        PerformanceAppraisal appraisal,
        Guid managerId,
        CancellationToken cancellationToken)
    {
        // A withdrawn appraisal has no outcome to recommend (E-d1); it cannot be submitted either.
        if (appraisal.Status == AppraisalStatus.Withdrawn || managerId == Guid.Empty)
            return (0, 0);

        var existing = await repository.GetQueryable()
            .Where(r => r.TenantId == appraisal.TenantId && r.PerformanceAppraisalId == appraisal.Id)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        int added = 0, dismissed = 0;

        foreach (var (ticked, type) in Map)
        {
            var ofType = existing.Where(r => r.RecommendationType == type).ToList();

            if (ticked(appraisal))
            {
                if (ofType.Any(r => IsOpen(r.Status)))
                    continue;

                var notes = string.IsNullOrWhiteSpace(appraisal.RecommendationNotes)
                    ? "Recommended in the line manager's evaluation."
                    : appraisal.RecommendationNotes!;
                await repository.AddAsync(new AppraisalOutcomeRecommendation
                {
                    TenantId = appraisal.TenantId,
                    PerformanceAppraisalId = appraisal.Id,
                    RecommendationType = type,
                    Status = RecommendationStatus.Proposed,
                    RecommendedById = managerId,
                    RecommendedDate = now,
                    Notes = notes.Length > NotesMaxLength ? notes[..NotesMaxLength] : notes,
                });
                added++;
                continue;
            }

            foreach (var row in ofType.Where(r => r.Status == RecommendationStatus.Proposed && r.RecommendedById == managerId))
            {
                const string note = "Dismissed: the line manager no longer recommends this — the tick was cleared when the evaluation was submitted again.";
                row.Status = RecommendationStatus.Dismissed;
                row.DecidedById = managerId;
                row.DecidedDate = now;
                row.ResolutionNotes = note.Length > ResolutionNotesMaxLength ? note[..ResolutionNotesMaxLength] : note;
                dismissed++;
            }
        }

        return (added, dismissed);
    }
}
