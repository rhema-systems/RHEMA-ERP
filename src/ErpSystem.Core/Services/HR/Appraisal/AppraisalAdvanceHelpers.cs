using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Resolves the current fine-grained <see cref="AppraisalSubStatus"/> from live entity state and
/// settings flags without requiring an extra database round-trip.
/// The caller must have loaded: Goals, EvaluatorEvaluations, PeerNominations,
/// HRReviews, Conversations on the appraisal.
/// </summary>
public static class AppraisalSubStatusResolver
{
    public static AppraisalSubStatus Resolve(PerformanceAppraisal appraisal, AppraisalSettings settings)
    {
        // ── Terminal / appeal states come first ──────────────────────────────
        switch (appraisal.Status)
        {
            case AppraisalStatus.Closed:
                return AppraisalSubStatus.Closed;

            case AppraisalStatus.Completed:
                return AppraisalSubStatus.Completed;

            case AppraisalStatus.Appealed:
                return appraisal.CurrentAppealStatus switch
                {
                    AppraisalAppealStatus.UnderReview
                    or AppraisalAppealStatus.Remanded => AppraisalSubStatus.AppealUnderReview,
                    AppraisalAppealStatus.Upheld
                    or AppraisalAppealStatus.Rejected => AppraisalSubStatus.AppealResolved,
                    _ => AppraisalSubStatus.AppealSubmitted,
                };
        }

        // ── Active / Governance / Draft: walk the pipeline gates ────────────

        // 1. Goal-setting gate
        if (settings.RequireGoalSetting)
        {
            var activeGoals = appraisal.Goals.Where(g => g.Status != GoalStatus.Rejected).ToList();

            // Must have at least the configured minimum number of goals (defaults to 1).
            int minGoals = settings.MinGoalsPerEmployee is int m && m > 0 ? m : 1;
            bool goalsReady = activeGoals.Count >= minGoals;

            // When manager approval is required, goals must be approved (or further along),
            // not merely created/awaiting approval.
            if (goalsReady && settings.RequireManagerGoalApproval)
            {
                goalsReady = activeGoals.All(g =>
                    g.Status != GoalStatus.Draft
                    && g.Status != GoalStatus.PendingApproval);
            }

            if (!goalsReady)
                return AppraisalSubStatus.GoalSetting;
        }

        // 1b. Required setup conversations (kick-off, mid-year) precede the year-end evaluation
        // pipeline. Mirror the final-conversation handling: block advancement until the matching
        // AppraisalConversation is completed. (Held at GoalSetting — the pre-evaluation phase.)
        if (settings.RequireKickOffConversation
            && !appraisal.Conversations.Any(c => c.Type == ConversationType.KickOff && c.IsCompleted))
            return AppraisalSubStatus.GoalSetting;

        if (settings.RequireMidYearConversation
            && !appraisal.Conversations.Any(c => c.Type == ConversationType.MidYear && c.IsCompleted))
            return AppraisalSubStatus.GoalSetting;

        // 2. Peer nomination gate (must have enough approved nominations before evals start)
        if (settings.RequirePeerReviews && settings.MinPeerEvaluators > 0)
        {
            int approvedNominations = appraisal.PeerNominations
                .Count(n => n.NominationStatus == PeerNominationStatus.Approved);

            if (approvedNominations < settings.MinPeerEvaluators)
                return AppraisalSubStatus.PeerNomination;
        }

        // 3. Self-evaluation gate
        if (settings.RequireSelfEvaluation)
        {
            bool selfSubmitted = appraisal.EvaluatorEvaluations
                .Any(e => e.EvaluatorRole == EvaluatorRole.Self && e.SubmittedDate != null);

            if (!selfSubmitted)
                return AppraisalSubStatus.SelfEvaluation;
        }

        // 4. Peer evaluation gate
        if (settings.RequirePeerReviews && settings.MinPeerEvaluators > 0)
        {
            int submittedPeerCount = appraisal.EvaluatorEvaluations
                .Count(e => e.EvaluatorRole == EvaluatorRole.Peer && e.SubmittedDate != null);

            if (submittedPeerCount < settings.MinPeerEvaluators)
                return AppraisalSubStatus.PeerEvaluation;
        }

        // 5. Manager evaluation gate
        if (settings.RequireManagerEvaluation)
        {
            bool managerSubmitted = appraisal.EvaluatorEvaluations
                .Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.SubmittedDate != null);

            if (!managerSubmitted)
                return AppraisalSubStatus.ManagerEvaluation;
        }

        // 6/7. Calibration + HR Review — order depends on HRReviewTiming
        if (settings.HRReviewTiming == HRReviewTiming.BeforeCalibration)
        {
            if (settings.RequireHRReview)
            {
                bool hrApproved = appraisal.HRReviews
                    .Any(r => r.ReviewCompletedDate != null && r.IsApproved);
                if (!hrApproved)
                {
                    bool hrStarted = appraisal.HRReviews.Any(r => r.ReviewCompletedDate == null);
                    return hrStarted ? AppraisalSubStatus.HRReviewInProgress : AppraisalSubStatus.PendingHRReview;
                }
            }

            if (settings.RequireCalibration && !appraisal.IsCalibrated)
            {
                bool calStarted = appraisal.CalibrationSessionId != null;
                return calStarted ? AppraisalSubStatus.CalibrationInProgress : AppraisalSubStatus.PendingCalibration;
            }
        }
        else // AfterCalibration (default)
        {
            if (settings.RequireCalibration && !appraisal.IsCalibrated)
            {
                bool calStarted = appraisal.CalibrationSessionId != null;
                return calStarted ? AppraisalSubStatus.CalibrationInProgress : AppraisalSubStatus.PendingCalibration;
            }

            if (settings.RequireHRReview)
            {
                bool hrApproved = appraisal.HRReviews
                    .Any(r => r.ReviewCompletedDate != null && r.IsApproved);
                if (!hrApproved)
                {
                    bool hrStarted = appraisal.HRReviews.Any(r => r.ReviewCompletedDate == null);
                    return hrStarted ? AppraisalSubStatus.HRReviewInProgress : AppraisalSubStatus.PendingHRReview;
                }
            }
        }

        // 8. Final conversation gate
        if (settings.RequireFinalConversation)
        {
            bool convCompleted = appraisal.Conversations
                .Any(c => c.Type == ConversationType.FinalReview && c.IsCompleted);
            if (!convCompleted)
                return AppraisalSubStatus.PendingConversation;
        }

        // 9. Employee acknowledgment gate
        if (settings.RequireEmployeeAcknowledgment && !appraisal.EmployeeAcknowledged)
            return AppraisalSubStatus.PendingAcknowledgment;

        // All pipeline gates passed — terminal
        return AppraisalSubStatus.Completed;
    }

    // ── Major-status transition helpers ─────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="AppraisalStatus"/> that the appraisal SHOULD be in given a resolved
    /// sub-status and the settings configuration. Used to decide whether to auto-transition after
    /// completing a sub-step.
    /// </summary>
    public static AppraisalStatus ExpectedMajorStatus(
        AppraisalSubStatus subStatus, AppraisalSettings settings)
    {
        return subStatus switch
        {
            // Pre-pipeline and Active evaluation phases
            AppraisalSubStatus.GoalSetting
            or AppraisalSubStatus.PeerNomination
            or AppraisalSubStatus.SelfEvaluation
            or AppraisalSubStatus.PeerEvaluation
            or AppraisalSubStatus.ManagerEvaluation => AppraisalStatus.Active,

            // Governance phases (calibration, HR review, conversation, acknowledgment)
            AppraisalSubStatus.PendingCalibration
            or AppraisalSubStatus.CalibrationInProgress
            or AppraisalSubStatus.PendingHRReview
            or AppraisalSubStatus.HRReviewInProgress
            or AppraisalSubStatus.PendingConversation
            or AppraisalSubStatus.PendingAcknowledgment => RequiresGovernance(settings)
                ? AppraisalStatus.Governance
                : AppraisalStatus.Active,

            // Terminal
            AppraisalSubStatus.Completed => AppraisalStatus.Completed,
            AppraisalSubStatus.Closed => AppraisalStatus.Closed,

            // Appeals keep current status — don't auto-transition
            _ => AppraisalStatus.Appealed,
        };
    }

    private static bool RequiresGovernance(AppraisalSettings settings) =>
        settings.RequireCalibration || settings.RequireHRReview
        || settings.RequireFinalConversation || settings.RequireEmployeeAcknowledgment;
}
