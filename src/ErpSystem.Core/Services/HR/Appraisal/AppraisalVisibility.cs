using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>Who is reading an appraisal, as <see cref="AppraisalVisibility"/> sees them.</summary>
public enum AppraisalReader
{
    /// <summary>
    /// The employee being appraised — even when they hold the HR desk: an officer who is the subject
    /// is the subject (the two-actor rule, performance closure lane P).
    /// </summary>
    Appraisee,

    /// <summary>
    /// The appraisee's line manager, whose evaluation the profile's anchoring switches protect —
    /// even when they hold the desk.
    /// </summary>
    LineManager,

    /// <summary>
    /// Anyone else the read admitted. The appraisal controllers admit only the two parties and holders
    /// of the performance Read policy, so this is the HR desk (an internal caller passes no viewer).
    /// </summary>
    Desk,
}

/// <summary>What the visibility rule reads about one appraisal.</summary>
public sealed record AppraisalVisibilityFacts(
    Guid EmployeeId,
    Guid? LineManagerId,
    bool SelfSubmitted,
    bool ManagerSubmitted,
    bool OutcomeReleased,
    bool ShowSelfScoreToManager,
    bool ShowPeerScoresToManager,
    bool ShowScoreBreakdownToEmployee);

/// <summary>What one reader may see of one appraisal.</summary>
/// <param name="SelfEntries">
/// What the employee entered on their self-evaluation — scores, actuals, notes, evidence, custom
/// answers, and the self side of the goal assessments — with the self total.
/// </param>
/// <param name="PeerScores">Submitted peers' criterion entries and totals. A peer's draft is nobody's to read.</param>
/// <param name="ManagerScores">The manager's criterion entries, their total, and the manager side of the goal assessments.</param>
/// <param name="ManagerNarrative">The manager's general comments on their evaluation.</param>
public sealed record AppraisalView(
    AppraisalReader Reader,
    bool SelfEntries,
    bool PeerScores,
    bool ManagerScores,
    bool ManagerNarrative);

/// <summary>
/// What a reader of an appraisal may see of the evaluations on it, in one place (performance closure
/// B2, slice B-v). Every read that returns an evaluator's entries asks here, so the manager's form,
/// the HR review, the goal assessments and the appeal pages cannot disagree.
/// </summary>
/// <remarks>
/// <para>The three profile switches did nothing on the server. <c>ShowSelfScoreToManager</c> and
/// <c>ShowPeerScoresToManager</c> were a React check on one screen while four reads handed the line
/// manager the self and peer scores anyway; <c>ShowScoreBreakdownToEmployee</c> hid one card while
/// the HR review, the goal assessments and the three appeal reads carried every criterion. Two of
/// those reads also ignored the release rule (<see cref="AppraisalRelease"/>): the appeal page and
/// the goal assessments gave the appraisee the manager's scores before HR's sign-off, and the goal
/// assessments gave the manager the employee's self-assessment while it was still a draft.</para>
/// <list type="bullet">
///   <item><b>The appraisee</b> sees everything they entered. The manager's and peers' legs reach
///         them only once the outcome is released — and then criterion by criterion only when the
///         profile shows the breakdown; otherwise the overall, the grade and the narrative.</item>
///   <item><b>The line manager</b> sees their own evaluation. The employee's entries reach them once
///         the self-evaluation is submitted, and then only when the profile shows self scores to the
///         manager <i>or</i> they have submitted their own evaluation — the switch's own contract is
///         "until after they submit"; lane P's first cut hid them for good. Peer scores likewise.</item>
///   <item><b>The desk</b> sees every submitted entry. Nobody but the employee reads a self-evaluation
///         draft (P12), and nobody reads a peer's draft.</item>
/// </list>
/// </remarks>
public static class AppraisalVisibility
{
    public static AppraisalReader ReaderOf(AppraisalVisibilityFacts facts, Guid? viewerEmployeeId)
    {
        if (viewerEmployeeId is not Guid viewer || viewer == Guid.Empty)
            return AppraisalReader.Desk;
        if (viewer == facts.EmployeeId)
            return AppraisalReader.Appraisee;
        if (facts.LineManagerId == viewer)
            return AppraisalReader.LineManager;
        return AppraisalReader.Desk;
    }

    public static AppraisalView For(AppraisalVisibilityFacts facts, Guid? viewerEmployeeId)
        => ReaderOf(facts, viewerEmployeeId) switch
        {
            AppraisalReader.Appraisee => new AppraisalView(
                AppraisalReader.Appraisee,
                SelfEntries: true,
                PeerScores: facts.OutcomeReleased && facts.ShowScoreBreakdownToEmployee,
                ManagerScores: facts.OutcomeReleased && facts.ShowScoreBreakdownToEmployee,
                ManagerNarrative: facts.OutcomeReleased),
            AppraisalReader.LineManager => ForManager(facts),
            _ => new AppraisalView(
                AppraisalReader.Desk,
                SelfEntries: facts.SelfSubmitted,
                PeerScores: true,
                ManagerScores: true,
                ManagerNarrative: true),
        };

    /// <summary>
    /// The line manager's view — also the manager's evaluation form's, whoever opens it: the form is
    /// the manager's, so the desk reading it through the manager's route sees what the manager sees.
    /// </summary>
    public static AppraisalView ForManager(AppraisalVisibilityFacts facts) => new(
        AppraisalReader.LineManager,
        SelfEntries: facts.SelfSubmitted && (facts.ShowSelfScoreToManager || facts.ManagerSubmitted),
        PeerScores: facts.ShowPeerScoresToManager || facts.ManagerSubmitted,
        ManagerScores: true,
        ManagerNarrative: true);

    /// <summary>
    /// The facts from an appraisal loaded with its employee, evaluations, HR reviews and cycle
    /// settings.
    /// </summary>
    public static AppraisalVisibilityFacts From(PerformanceAppraisal appraisal, AppraisalSettings settings)
        => new(
            appraisal.EmployeeId,
            appraisal.Employee?.ManagerId,
            appraisal.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Self && e.SubmittedDate != null),
            appraisal.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.SubmittedDate != null),
            AppraisalRelease.IsReleased(
                appraisal.Status,
                appraisal.IsCalibrated,
                appraisal.HRReviews.Any(r => r.ReviewCompletedDate != null && r.IsApproved),
                appraisal.AppealRemandedDate != null,
                settings.RequireCalibration,
                settings.RequireHRReview),
            settings.ShowSelfScoreToManager,
            settings.ShowPeerScoresToManager,
            settings.ShowScoreBreakdownToEmployee);

    /// <summary>The facts from the gate state a read has already loaded, and the appraisee's line manager.</summary>
    public static AppraisalVisibilityFacts From(AppraisalGateState state, Guid? lineManagerId)
        => new(
            state.EmployeeId,
            lineManagerId,
            state.Facts.SelfSubmitted,
            state.Facts.ManagerSubmitted,
            AppraisalRelease.IsReleased(
                state.Facts.Status,
                state.Facts.IsCalibrated,
                state.Facts.HrApproved,
                state.Facts.Remanded,
                state.Settings.RequireCalibration,
                state.Settings.RequireHRReview),
            state.Settings.ShowSelfScoreToManager,
            state.Settings.ShowPeerScoresToManager,
            state.Settings.ShowScoreBreakdownToEmployee);

    /// <summary>
    /// The facts for one appraisal in a single projection, for a read that has not loaded them; null
    /// when the appraisal is not in <paramref name="appraisals"/>.
    /// </summary>
    public static async Task<AppraisalVisibilityFacts?> LoadAsync(
        IQueryable<PerformanceAppraisal> appraisals, Guid appraisalId, CancellationToken cancellationToken)
    {
        var row = await appraisals
            .AsNoTracking()
            .Where(a => a.Id == appraisalId)
            .Select(a => new
            {
                a.EmployeeId,
                LineManagerId = a.Employee.ManagerId,
                a.Status,
                a.IsCalibrated,
                Remanded = a.AppealRemandedDate != null,
                HrApproved = a.HRReviews.Any(r => r.ReviewCompletedDate != null && r.IsApproved),
                SelfSubmitted = a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Self && e.SubmittedDate != null),
                ManagerSubmitted = a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.SubmittedDate != null),
                a.AppraisalCycle.AppraisalSettings.RequireCalibration,
                a.AppraisalCycle.AppraisalSettings.RequireHRReview,
                a.AppraisalCycle.AppraisalSettings.ShowSelfScoreToManager,
                a.AppraisalCycle.AppraisalSettings.ShowPeerScoresToManager,
                a.AppraisalCycle.AppraisalSettings.ShowScoreBreakdownToEmployee,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null) return null;

        return new AppraisalVisibilityFacts(
            row.EmployeeId,
            row.LineManagerId,
            row.SelfSubmitted,
            row.ManagerSubmitted,
            AppraisalRelease.IsReleased(row.Status, row.IsCalibrated, row.HrApproved, row.Remanded,
                row.RequireCalibration, row.RequireHRReview),
            row.ShowSelfScoreToManager,
            row.ShowPeerScoresToManager,
            row.ShowScoreBreakdownToEmployee);
    }
}
