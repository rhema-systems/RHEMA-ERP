using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>One of the employee's goals in the cycle, as the goal-setting gate reads it.</summary>
public sealed record AppraisalGateGoal(GoalStatus Status, bool IsLocked)
{
    public bool IsLive => GoalSetRules.IsLive(Status);
    public bool IsSetLocked => GoalSetRules.IsLocked(IsLocked, Status);
}

/// <summary>An evaluation, as the gates read it.</summary>
public sealed record AppraisalGateEvaluation(EvaluatorRole Role, DateTime? SubmittedDate);

/// <summary>An HR review record, as the gates read it.</summary>
public sealed record AppraisalGateHrReview(DateTime? CompletedDate, bool IsApproved);

/// <summary>
/// Everything the gates read about one appraisal. Built only by
/// <see cref="IAppraisalLifecycleService"/>, from what is saved, so every reader —
/// the write paths, the phase endpoint, the HR dashboard, the lists — resolves the same facts.
/// </summary>
public sealed record AppraisalGateFacts
{
    public required Guid AppraisalId { get; init; }
    public required AppraisalStatus Status { get; init; }
    public AppraisalAppealStatus? CurrentAppealStatus { get; init; }

    /// <summary><c>AppealRemandedDate</c> is set: the manager owes a re-evaluation.</summary>
    public bool Remanded { get; init; }

    public bool HasAppeal { get; init; }
    public bool IsCalibrated { get; init; }

    /// <summary>A calibration session that is still sitting (pending or in progress) has the appraisal.</summary>
    public bool CalibrationStarted { get; init; }

    public bool EmployeeAcknowledged { get; init; }
    public DateTime? EmployeeAcknowledgedDate { get; init; }

    /// <summary><c>UpdatedAt ?? CreatedAt</c> — the appeal window's last resort.</summary>
    public DateTime LastChangedAt { get; init; }

    public IReadOnlyList<AppraisalGateEvaluation> Evaluations { get; init; } = [];
    public IReadOnlyList<PeerNominationStatus> Nominations { get; init; } = [];
    public IReadOnlyList<AppraisalGateHrReview> HrReviews { get; init; } = [];

    /// <summary>The conversation types held (completed) on the appraisal.</summary>
    public IReadOnlyCollection<ConversationType> ConversationsHeld { get; init; } = [];

    /// <summary>
    /// The employee's goals in the appraisal's cycle — by employee and cycle, not by the goal's
    /// appraisal link, which is set only when the goal is created after its appraisal exists
    /// (performance closure L2): goals agreed before generation used to be invisible to the gate.
    /// </summary>
    public IReadOnlyList<AppraisalGateGoal> Goals { get; init; } = [];

    /// <summary>
    /// The template has a goals section (<see cref="AppraisalSectionKind.EmployeeGoals"/>), so the
    /// goal set must be locked before evaluation — its rows are built at the lock (lane L).
    /// </summary>
    public bool GoalSetMustBeLocked { get; init; }

    /// <summary>The steps HR's audited advance has moved the appraisal past (the advance log).</summary>
    public IReadOnlyCollection<AppraisalSubStatus> AdvancedPast { get; init; } = [];

    public bool SelfSubmitted => Evaluations.Any(e => e.Role == EvaluatorRole.Self && e.SubmittedDate != null);
    public bool ManagerSubmitted => Evaluations.Any(e => e.Role == EvaluatorRole.Manager && e.SubmittedDate != null);
    public int PeersSubmitted => Evaluations.Count(e => e.Role == EvaluatorRole.Peer && e.SubmittedDate != null);
    public bool HrApproved => HrReviews.Any(r => r.CompletedDate != null && r.IsApproved);
    public bool HrReviewOpen => HrReviews.Any(r => r.CompletedDate == null);

    /// <summary>When HR signed it off — the latest approved review.</summary>
    public DateTime? HrSignedOffAt => HrReviews.Where(r => r.IsApproved).Max(r => r.CompletedDate);

    /// <summary>The latest submission of any evaluation — when an appraisal with no HR step completed.</summary>
    public DateTime? LastSubmittedAt => Evaluations.Max(e => e.SubmittedDate);
}

/// <summary>Where the gates put an appraisal: the step it is at, and why it has not passed it.</summary>
public readonly record struct AppraisalGateBlock(AppraisalSubStatus Step, string? Reason);

/// <summary>
/// A write refused because the appraisal is not at the step it belongs to. An
/// <see cref="InvalidOperationException"/>, so every controller answers it 422 with the message,
/// which names the step the appraisal is at (performance closure B1).
/// </summary>
public sealed class AppraisalGateException : InvalidOperationException
{
    public AppraisalGateException(AppraisalSubStatus step, string message) : base(message)
    {
        Step = step;
    }

    /// <summary>The step the appraisal is at.</summary>
    public AppraisalSubStatus Step { get; }
}

/// <summary>
/// The appraisal pipeline, in one place (performance closure lane B1). Pure: it reads
/// <see cref="AppraisalGateFacts"/> and the cycle's settings, and decides where an appraisal is,
/// which write it is open to, which major status that means, and whether an appeal may be filed.
///
/// <para>It replaces <c>AppraisalSubStatusResolver</c> and the second copy of the same walk in
/// <c>AppraisalWorkflowService.GetCurrentPhase</c>. The two disagreed — the phase had no
/// nomination or conversation gate and counted goals differently — and no write path consulted
/// either: a manager could submit before the self-evaluation, HR could finalise before
/// calibration, and an employee could acknowledge before the final conversation, whatever the
/// settings said.</para>
/// </summary>
public static class AppraisalGates
{
    /// <summary>
    /// The steps HR's audited advance can waive — the ones before the manager's evaluation. A
    /// waiver is the advance-log row itself (<c>FromSubStatus</c> names the step; the same rule
    /// performance closure batch 1 wrote calibration waivers under).
    ///
    /// <para>Only these, because nothing re-opens them. The manager's evaluation, calibration and
    /// HR's sign-off are re-opened by a return to the manager or a remand, and a waiver that
    /// outlived the re-opening would wave the redo through. HR's advance past those steps still
    /// satisfies them with the records it writes, which the re-opening resets.</para>
    /// </summary>
    private static readonly HashSet<AppraisalSubStatus> Waivable =
    [
        AppraisalSubStatus.GoalSetting,
        AppraisalSubStatus.PeerNomination,
        AppraisalSubStatus.SelfEvaluation,
        AppraisalSubStatus.PeerEvaluation,
    ];

    // ── The pipeline ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Goal setting is a step when goals or the kick-off conversation are required. The mid-year
    /// conversation is not part of it: it holds the manager's submission, not the self-evaluation
    /// (performance closure B2 — B1 had put it beside the kick-off).
    /// </summary>
    private static bool HasGoalSettingStep(AppraisalSettings s)
        => s.RequireGoalSetting || s.RequireKickOffConversation;

    private static bool HasPeerSteps(AppraisalSettings s)
        => s.RequirePeerReviews && s.MinPeerEvaluators > 0;

    /// <summary>
    /// The final conversation is a gate unless the employee's acknowledgment may go ahead without
    /// it: <c>AllowAcknowledgmentWithoutConversation</c> relaxes the acknowledgment, so with no
    /// acknowledgment step it relaxes nothing and the conversation still stands before completion.
    /// </summary>
    private static bool HasConversationGate(AppraisalSettings s)
        => s.RequireFinalConversation
           && !(s.RequireEmployeeAcknowledgment && s.AllowAcknowledgmentWithoutConversation);

    /// <summary>
    /// The steps these settings require, in the order they run. Calibration and HR's review swap
    /// with <see cref="HRReviewTiming"/>. The "in progress" variants of calibration and HR review
    /// are the same step as their pending forms.
    /// </summary>
    public static IReadOnlyList<AppraisalSubStatus> Pipeline(AppraisalSettings s)
    {
        var steps = new List<AppraisalSubStatus>();

        if (HasGoalSettingStep(s)) steps.Add(AppraisalSubStatus.GoalSetting);
        if (HasPeerSteps(s)) steps.Add(AppraisalSubStatus.PeerNomination);
        if (s.RequireSelfEvaluation) steps.Add(AppraisalSubStatus.SelfEvaluation);
        if (HasPeerSteps(s)) steps.Add(AppraisalSubStatus.PeerEvaluation);
        if (s.RequireManagerEvaluation) steps.Add(AppraisalSubStatus.ManagerEvaluation);

        if (s.HRReviewTiming == HRReviewTiming.BeforeCalibration)
        {
            if (s.RequireHRReview) steps.Add(AppraisalSubStatus.PendingHRReview);
            if (s.RequireCalibration) steps.Add(AppraisalSubStatus.PendingCalibration);
        }
        else
        {
            if (s.RequireCalibration) steps.Add(AppraisalSubStatus.PendingCalibration);
            if (s.RequireHRReview) steps.Add(AppraisalSubStatus.PendingHRReview);
        }

        if (HasConversationGate(s)) steps.Add(AppraisalSubStatus.PendingConversation);
        if (s.RequireEmployeeAcknowledgment) steps.Add(AppraisalSubStatus.PendingAcknowledgment);

        return steps;
    }

    /// <summary>The pending form of a step: an "in progress" sub-status is the same step.</summary>
    public static AppraisalSubStatus StepOf(AppraisalSubStatus sub) => sub switch
    {
        AppraisalSubStatus.CalibrationInProgress => AppraisalSubStatus.PendingCalibration,
        AppraisalSubStatus.HRReviewInProgress => AppraisalSubStatus.PendingHRReview,
        _ => sub,
    };

    // ── Resolve ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Where the appraisal is. A terminal status is its own answer; an appeal is resolved from its
    /// appeal status — a remanded one is under review whatever the major status says, because the
    /// remand moved it back to Active; otherwise the first required step not yet passed (and not
    /// waived by HR's advance), with the reason; <see cref="AppraisalSubStatus.Completed"/> when
    /// every step has passed.
    /// </summary>
    public static AppraisalGateBlock Resolve(AppraisalGateFacts f, AppraisalSettings s)
    {
        switch (f.Status)
        {
            case AppraisalStatus.Withdrawn:
                return new(AppraisalSubStatus.Withdrawn, "it was withdrawn from the cycle");
            case AppraisalStatus.Closed:
                return new(AppraisalSubStatus.Closed, null);
            case AppraisalStatus.Completed:
                return new(AppraisalSubStatus.Completed, null);
        }

        if (f.CurrentAppealStatus == AppraisalAppealStatus.Remanded && f.Remanded)
            return new(AppraisalSubStatus.AppealUnderReview, "the appeal was remanded: the manager re-evaluates, then HR decides");

        if (f.Status == AppraisalStatus.Appealed)
        {
            return f.CurrentAppealStatus switch
            {
                AppraisalAppealStatus.UnderReview or AppraisalAppealStatus.Remanded
                    => new(AppraisalSubStatus.AppealUnderReview, "HR is reviewing the appeal"),
                AppraisalAppealStatus.Upheld or AppraisalAppealStatus.Rejected
                    => new(AppraisalSubStatus.AppealResolved, null),
                _ => new(AppraisalSubStatus.AppealSubmitted, "the appeal waits for HR"),
            };
        }

        foreach (var step in Pipeline(s))
        {
            var reason = Blocker(step, f, s);
            if (reason != null && !IsWaived(f, step))
                return new(InProgressForm(step, f), reason);
        }

        return new(AppraisalSubStatus.Completed, null);
    }

    /// <summary>Why the appraisal has not passed <paramref name="step"/>, or null when it has.</summary>
    private static string? Blocker(AppraisalSubStatus step, AppraisalGateFacts f, AppraisalSettings s)
    {
        switch (step)
        {
            case AppraisalSubStatus.GoalSetting:
            {
                if (s.RequireGoalSetting)
                {
                    var live = f.Goals.Where(g => g.IsLive).ToList();
                    var min = s.MinGoalsPerEmployee is int m && m > 0 ? m : 1;

                    if (live.Count < min)
                        return live.Count == 0
                            ? "no goals are set for this cycle yet"
                            : $"{live.Count} of the {min} goals this cycle requires are set";

                    if (s.RequireManagerGoalApproval)
                    {
                        var waiting = live.Count(g => g.Status == GoalStatus.PendingApproval);
                        if (waiting > 0)
                            return waiting == 1
                                ? "one goal still waits for the manager's approval"
                                : $"{waiting} goals still wait for the manager's approval";
                    }

                    // A draft is the employee's unfinished work whether or not the manager approves
                    // goals (B2): with approval off the submission is the agreement, so an
                    // unsubmitted goal agrees to nothing. It used to count toward the minimum.
                    var drafts = live.Count(g => g.Status == GoalStatus.Draft);
                    if (drafts > 0)
                        return s.RequireManagerGoalApproval
                            ? drafts == 1
                                ? "one goal is still a draft, not yet submitted for the manager's approval"
                                : $"{drafts} goals are still drafts, not yet submitted for the manager's approval"
                            : drafts == 1
                                ? "one goal is still a draft, not yet submitted"
                                : $"{drafts} goals are still drafts, not yet submitted";

                    if (f.GoalSetMustBeLocked && live.Any(g => !g.IsSetLocked))
                        return "the goal set is not locked";
                }

                if (s.RequireKickOffConversation && !f.ConversationsHeld.Contains(ConversationType.KickOff))
                    return "the kick-off conversation has not been held";

                return null;
            }

            case AppraisalSubStatus.PeerNomination:
            {
                // Pending nominations count: nominating is the employee's step, and approving them is
                // the manager's, which the peer evaluations then wait on. A rejected one does not, so a
                // replacement can be nominated.
                var live = f.Nominations.Count(n => n != PeerNominationStatus.Rejected);
                return live >= s.MinPeerEvaluators
                    ? null
                    : $"{live} of the {s.MinPeerEvaluators} peer nominations this cycle requires are in";
            }

            case AppraisalSubStatus.SelfEvaluation:
                return f.SelfSubmitted ? null : "the employee has not submitted their self-evaluation";

            case AppraisalSubStatus.PeerEvaluation:
            {
                var submitted = f.PeersSubmitted;
                return submitted >= s.MinPeerEvaluators
                    ? null
                    : $"{submitted} of the {s.MinPeerEvaluators} peer evaluations this cycle requires are submitted";
            }

            case AppraisalSubStatus.ManagerEvaluation:
                if (f.ManagerSubmitted) return null;
                // The mid-year conversation holds the manager's submission (B2), so while it is
                // missing it is why the appraisal waits here — the manager's to hold.
                return MidYearMissing(f, s)
                    ? "the mid-year conversation has not been held"
                    : "the manager has not submitted their evaluation";

            case AppraisalSubStatus.PendingCalibration:
                return f.IsCalibrated
                    ? null
                    : f.CalibrationStarted
                        ? "its calibration session has not been committed"
                        : "no calibration session has committed it";

            case AppraisalSubStatus.PendingHRReview:
                return f.HrApproved ? null : "HR has not signed it off";

            case AppraisalSubStatus.PendingConversation:
                return f.ConversationsHeld.Contains(ConversationType.FinalReview)
                    ? null
                    : "the final review conversation has not been held";

            case AppraisalSubStatus.PendingAcknowledgment:
                return f.EmployeeAcknowledged ? null : "the employee has not acknowledged it";

            default:
                return null;
        }
    }

    private static bool MidYearMissing(AppraisalGateFacts f, AppraisalSettings s)
        => s.RequireMidYearConversation && !f.ConversationsHeld.Contains(ConversationType.MidYear);

    /// <summary>
    /// Refuses the manager's submission while the mid-year conversation the profile requires has not
    /// been held (performance closure B2). The appraisal is <i>at</i> the manager-evaluation step
    /// while it waits — the conversation is the manager's own to hold — so the step check alone lets
    /// the submission through. The self-evaluation and the peers are not held by it: B1 had put the
    /// mid-year in goal setting, where it held everything after it.
    /// </summary>
    public static void EnsureManagerMaySubmit(AppraisalGateFacts f, AppraisalSettings s, string action)
    {
        if (MidYearMissing(f, s))
            throw new AppraisalGateException(
                AppraisalSubStatus.ManagerEvaluation,
                Refusal(action, new AppraisalGateBlock(AppraisalSubStatus.ManagerEvaluation, "the mid-year conversation has not been held")));
    }

    private static AppraisalSubStatus InProgressForm(AppraisalSubStatus step, AppraisalGateFacts f) => step switch
    {
        AppraisalSubStatus.PendingCalibration when f.CalibrationStarted => AppraisalSubStatus.CalibrationInProgress,
        AppraisalSubStatus.PendingHRReview when f.HrReviewOpen => AppraisalSubStatus.HRReviewInProgress,
        _ => step,
    };

    /// <summary>HR's audited advance has moved the appraisal past <paramref name="step"/>, and the step is one a waiver can pass.</summary>
    public static bool IsWaived(AppraisalGateFacts f, AppraisalSubStatus step)
        => Waivable.Contains(StepOf(step)) && f.AdvancedPast.Contains(StepOf(step));

    /// <summary>Whether a step can be waived by HR's advance (the steps before the manager's evaluation).</summary>
    public static bool CanBeWaived(AppraisalSubStatus step) => Waivable.Contains(StepOf(step));

    /// <summary>
    /// What is recorded for steps the pipeline puts after <paramref name="at"/> — the transition
    /// report (performance closure B8). Empty when the records agree with the gates, or when the
    /// appraisal is past the pipeline (completed, appealed, withdrawn). Only the profile's own
    /// steps count: a self-evaluation on a profile that does not require one contradicts nothing.
    /// </summary>
    public static IReadOnlyList<string> RecordedAhead(AppraisalGateFacts f, AppraisalSettings s, AppraisalSubStatus at)
    {
        var pipeline = Pipeline(s);
        var atIndex = IndexOf(pipeline, StepOf(at));
        if (atIndex < 0) return [];

        var ahead = new List<string>();
        void Check(AppraisalSubStatus step, bool recorded, string what)
        {
            if (recorded && IndexOf(pipeline, step) > atIndex) ahead.Add(what);
        }

        Check(AppraisalSubStatus.SelfEvaluation, f.SelfSubmitted, "the self-evaluation is submitted");
        Check(AppraisalSubStatus.PeerEvaluation, f.PeersSubmitted > 0,
            f.PeersSubmitted == 1 ? "a peer evaluation is submitted" : $"{f.PeersSubmitted} peer evaluations are submitted");
        Check(AppraisalSubStatus.ManagerEvaluation, f.ManagerSubmitted, "the manager's evaluation is submitted");
        Check(AppraisalSubStatus.PendingCalibration, f.IsCalibrated, "a calibration session has committed it");
        Check(AppraisalSubStatus.PendingHRReview, f.HrApproved, "HR has signed it off");
        Check(AppraisalSubStatus.PendingConversation, f.ConversationsHeld.Contains(ConversationType.FinalReview),
            "the final conversation is held");
        Check(AppraisalSubStatus.PendingAcknowledgment, f.EmployeeAcknowledged, "the employee has acknowledged it");
        return ahead;

        static int IndexOf(IReadOnlyList<AppraisalSubStatus> steps, AppraisalSubStatus step)
        {
            for (var i = 0; i < steps.Count; i++)
                if (steps[i] == step) return i;
            return -1;
        }
    }

    /// <summary>Reads an advance-log <c>FromSubStatus</c> back as a step; null for anything unrecognised.</summary>
    public static AppraisalSubStatus? ParseLoggedStep(string? loggedSubStatus)
        => Enum.TryParse<AppraisalSubStatus>(loggedSubStatus, out var sub) ? StepOf(sub) : null;

    // ── Refusing a write ────────────────────────────────────────────────────────────────────

    /// <summary>Whether the appraisal is at one of <paramref name="steps"/> (in-progress forms included).</summary>
    public static bool Check(AppraisalGateBlock block, params AppraisalSubStatus[] steps)
        => steps.Any(step => StepOf(step) == StepOf(block.Step));

    /// <summary>
    /// Refuses <paramref name="action"/> unless the appraisal is at one of <paramref name="steps"/>:
    /// throws <see cref="AppraisalGateException"/> naming the step it is at and why.
    /// </summary>
    public static AppraisalGateBlock EnsureAt(
        AppraisalGateFacts f, AppraisalSettings s, string action, params AppraisalSubStatus[] steps)
    {
        var block = Resolve(f, s);
        if (Check(block, steps)) return block;
        throw new AppraisalGateException(block.Step, Refusal(action, block));
    }

    /// <summary>"The manager evaluation cannot be submitted yet: this appraisal is at Self-Evaluation — …".</summary>
    public static string Refusal(string action, AppraisalGateBlock block)
    {
        var at = $"{action}: this appraisal is at {Label(block.Step)}";
        var reason = block.Reason is { Length: > 0 } r ? $" — {r}." : ".";
        var waiver = CanBeWaived(block.Step)
            ? " If it cannot be completed, HR can advance the appraisal past this step with a recorded reason."
            : string.Empty;
        return at + reason + waiver;
    }

    // ── What the step means ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The step's name, in every place it is shown — the phase endpoint, the HR dashboard and the
    /// refusal text use this one list, so the three always name the same step.
    /// </summary>
    public static string Label(AppraisalSubStatus sub) => sub switch
    {
        AppraisalSubStatus.GoalSetting => "Goal Setting",
        AppraisalSubStatus.PeerNomination => "Peer Nomination",
        AppraisalSubStatus.SelfEvaluation => "Self-Evaluation",
        AppraisalSubStatus.PeerEvaluation => "Peer Evaluation",
        AppraisalSubStatus.ManagerEvaluation => "Manager Evaluation",
        AppraisalSubStatus.PendingCalibration => "Calibration",
        AppraisalSubStatus.CalibrationInProgress => "Calibration (In Progress)",
        AppraisalSubStatus.PendingHRReview => "HR Review",
        AppraisalSubStatus.HRReviewInProgress => "HR Review (In Progress)",
        AppraisalSubStatus.PendingConversation => "Final Conversation",
        AppraisalSubStatus.PendingAcknowledgment => "Acknowledgment",
        AppraisalSubStatus.AppealSubmitted => "Appeal Submitted",
        AppraisalSubStatus.AppealUnderReview => "Appeal Under Review",
        AppraisalSubStatus.AppealResolved => "Appeal Resolved",
        AppraisalSubStatus.Completed => "Completed",
        AppraisalSubStatus.Closed => "Closed",
        AppraisalSubStatus.Withdrawn => "Withdrawn",
        _ => sub.ToString(),
    };

    /// <summary>The coarse phase a step belongs to, for the progress rail.</summary>
    public static AppraisalPhase ToPhase(AppraisalSubStatus sub) => sub switch
    {
        AppraisalSubStatus.GoalSetting => AppraisalPhase.GoalSetting,
        AppraisalSubStatus.PeerNomination => AppraisalPhase.PeerNomination,
        AppraisalSubStatus.SelfEvaluation => AppraisalPhase.SelfEvaluation,
        AppraisalSubStatus.PeerEvaluation => AppraisalPhase.PeerEvaluation,
        AppraisalSubStatus.ManagerEvaluation => AppraisalPhase.ManagerEvaluation,
        AppraisalSubStatus.PendingCalibration or AppraisalSubStatus.CalibrationInProgress => AppraisalPhase.Calibration,
        AppraisalSubStatus.PendingHRReview or AppraisalSubStatus.HRReviewInProgress => AppraisalPhase.HRReview,
        AppraisalSubStatus.PendingConversation or AppraisalSubStatus.PendingAcknowledgment => AppraisalPhase.EmployeeReview,
        _ => AppraisalPhase.Closed,
    };

    /// <summary>
    /// The major status a step belongs to: the evaluation steps are Active, the steps after the
    /// manager's evaluation are Governance. Appeals keep their own status — their actions move it.
    /// </summary>
    public static AppraisalStatus ExpectedMajorStatus(AppraisalSubStatus sub) => sub switch
    {
        AppraisalSubStatus.GoalSetting
            or AppraisalSubStatus.PeerNomination
            or AppraisalSubStatus.SelfEvaluation
            or AppraisalSubStatus.PeerEvaluation
            or AppraisalSubStatus.ManagerEvaluation => AppraisalStatus.Active,

        AppraisalSubStatus.PendingCalibration
            or AppraisalSubStatus.CalibrationInProgress
            or AppraisalSubStatus.PendingHRReview
            or AppraisalSubStatus.HRReviewInProgress
            or AppraisalSubStatus.PendingConversation
            or AppraisalSubStatus.PendingAcknowledgment => AppraisalStatus.Governance,

        AppraisalSubStatus.Completed => AppraisalStatus.Completed,
        AppraisalSubStatus.Closed => AppraisalStatus.Closed,
        AppraisalSubStatus.Withdrawn => AppraisalStatus.Withdrawn,

        _ => AppraisalStatus.Appealed,
    };

    /// <summary>The employee's end of the pipeline: every scoring step is behind it.</summary>
    public static bool IsEmployeeEnd(AppraisalSubStatus sub)
        => sub is AppraisalSubStatus.PendingConversation or AppraisalSubStatus.PendingAcknowledgment;

    /// <summary>
    /// The steps a peer may write at: from the peer step's opening — alongside the
    /// self-evaluation, or after it in <see cref="PeerEvaluationOpenMode.AfterSelfEval"/> — until
    /// the manager has submitted. A peer past the minimum may still add their view while the
    /// manager evaluates; once the manager has submitted, the judgement has been made.
    /// </summary>
    public static AppraisalSubStatus[] PeerWindow(AppraisalSettings s)
        => s.PeerEvaluationOpenMode == PeerEvaluationOpenMode.AfterSelfEval
            ? [AppraisalSubStatus.PeerEvaluation, AppraisalSubStatus.ManagerEvaluation]
            : [AppraisalSubStatus.SelfEvaluation, AppraisalSubStatus.PeerEvaluation, AppraisalSubStatus.ManagerEvaluation];

    // ── Appeals ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the employee may file an appeal now, and why not when they may not: the appraisal
    /// is Completed, carries no appeal yet, the cycle's settings allow appeals, and the window —
    /// <c>AppealWindowDays</c> from the acknowledgment, else HR's sign-off, else the last
    /// submission — is still open. The submit, the appeal page and the employee's list all ask
    /// here; the submit used to check the status alone, and the list measured the window from the
    /// end of the cycle.
    /// </summary>
    public static (bool Allowed, string? Reason, DateOnly? LastDay) CanFileAppeal(
        AppraisalGateFacts f, AppraisalSettings s, DateTime nowUtc)
    {
        if (f.Status != AppraisalStatus.Completed)
            return (false, "An appeal can be filed only once the appraisal is completed.", null);

        if (f.HasAppeal)
            return (false, "An appeal has already been filed for this appraisal.", null);

        if (!s.EnableAppeals)
            return (false, "Appeals are not enabled for this appraisal cycle.", null);

        var (from, what) = f.EmployeeAcknowledgedDate is DateTime acknowledged
            ? (acknowledged, "you acknowledged it")
            : f.HrSignedOffAt is DateTime signedOff
                ? (signedOff, "HR signed it off")
                : (f.LastSubmittedAt ?? f.LastChangedAt, "it was completed");

        var lastDay = DateOnly.FromDateTime(from).AddDays(Math.Max(0, s.AppealWindowDays));
        if (DateOnly.FromDateTime(nowUtc) > lastDay)
        {
            return (false,
                $"The appeal window closed on {lastDay:d MMM yyyy}: appeals are open for {s.AppealWindowDays} day(s) after {what}.",
                lastDay);
        }

        return (true, null, lastDay);
    }
}
