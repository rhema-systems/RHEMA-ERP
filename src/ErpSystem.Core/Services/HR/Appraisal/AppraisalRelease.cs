using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// When an appraisal's outcome is the appraisee's to see (performance closure P2).
/// </summary>
/// <remarks>
/// The appraisee could read their own appraisal's outcome at any stage: the manager's
/// recommendations (PIP and termination among them), the manager's narrative, the ranks, and the
/// pre-calibration, calibrated and overall scores and grade — before the panel or HR had seen any
/// of it. The outcome is released when every governance gate is behind it:
/// <list type="bullet">
///   <item>Completed or Closed — or Appealed, which only a completed appraisal can become;</item>
///   <item>in governance with calibration committed (when required) and HR's sign-off given (when
///         required), so only the employee's acknowledgment is outstanding — they acknowledge what
///         they can read.</item>
/// </list>
/// A remanded appraisal is not released: the re-evaluated score is provisional until HR's
/// post-remand decision, and the appeal pages carry the score the appeal was filed against. A remand
/// keeps the appraisal Appealed (performance closure C3), so Appealed is released only when no
/// remand is pending.
/// </remarks>
public static class AppraisalRelease
{
    public static bool IsReleased(
        AppraisalStatus status,
        bool isCalibrated,
        bool hrApproved,
        bool remanded,
        bool requireCalibration,
        bool requireHrReview) => status switch
    {
        AppraisalStatus.Completed or AppraisalStatus.Closed => true,
        AppraisalStatus.Appealed => !remanded,
        AppraisalStatus.Governance =>
            !remanded
            && (!requireCalibration || isCalibrated)
            && (!requireHrReview || hrApproved),
        _ => false,
    };

    /// <summary>
    /// Whether an appraisal's outcome stands for a withdrawal (performance closure E-g2, D-84): released to the employee.
    /// The withdrawal read the settle's "final", which waits for HR's sign-off — so in a cycle without HR review an
    /// outcome the employee could already read was never final, and a leaver's exit withdrew it. Every final appraisal is
    /// released. Reads the HR reviews and the cycle's settings; with the settings not loaded it falls back to "final".
    /// </summary>
    public static bool StandsForWithdrawal(PerformanceAppraisal appraisal)
    {
        if (AppraisalScoreService.IsFinal(appraisal)) return true;
        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null) return false;
        return IsReleased(
            appraisal.Status,
            appraisal.IsCalibrated,
            appraisal.HRReviews.Any(r => r.ReviewCompletedDate != null && r.IsApproved),
            appraisal.AppealRemandedDate != null,
            settings.RequireCalibration,
            settings.RequireHRReview);
    }

    /// <summary>
    /// Withholds the outcome from the appraisee's own view of an unreleased appraisal: every
    /// score, the grade, the ranks, the manager's recommendations and the manager's narrative.
    /// The recommendation flags are booleans, so they read false; <c>OutcomeReleased</c> is what
    /// tells a screen they were withheld rather than not made.
    /// </summary>
    public static void WithholdOutcome(PerformanceAppraisalDto dto)
    {
        WithholdScores(dto);

        dto.RecommendPromotion = false;
        dto.RecommendIncrement = false;
        dto.RecommendTraining = false;
        dto.RecommendPIP = false;
        dto.RecommendTermination = false;
        dto.RecommendAward = false;
        dto.RecommendationNotes = null;

        // The manager wrote these on the manager's evaluation form; they are part of the outcome.
        dto.OverallComments = null;
        dto.StrengthsIdentified = null;
        dto.AreasForImprovement = null;
        dto.TrainingNeeds = null;
        dto.CareerAspirations = null;
    }

    /// <summary>
    /// Every score, the grade and the ranks. On its own, for a withdrawn appraisal (performance
    /// closure E-d1): its numbers are not a result, whoever reads it, while what was written stays
    /// readable as the record of how far it got.
    /// </summary>
    public static void WithholdScores(PerformanceAppraisalDto dto)
    {
        dto.OverallScore = null;
        dto.AdjustedScore = null;
        dto.PreCalibrationScore = null;
        dto.CalibratedOverallScore = null;
        dto.OverallGradeDefinitionId = null;
        dto.RankInPosition = null;
        dto.RankInUnit = null;
    }
}
