using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Who may rule on an HR record whose entity type has <b>no published workflow definition</b>.
/// </summary>
/// <remarks>
/// <para><b>The defect this closes, and why it is one mechanism rather than several.</b>
/// <c>WorkflowIntegrationService.SubmitAsync</c> asks <c>HasActiveApprovalWorkflowAsync</c> and,
/// when the answer is no, returns early with <c>Success = true</c> and
/// <c>WorkflowOutcome.Approved</c>. That is a deliberate "approval is not configured for this
/// entity type, use the direct lifecycle" signal, and for some callers it is the right one. Every
/// HR status adapter, however, maps <c>Approved</c> to its own <i>approved</i> status — so for any
/// HR record wired to the engine, pressing <b>Submit</b> took it from Draft to Approved in one
/// step, with no approver, no <c>CanUserApproveAsync</c> check and no segregation of duties,
/// because those rules live in <c>ApproveAsync</c>, which was never called. Each service's history
/// row recorded the transition, so afterwards it was indistinguishable from a reviewed approval.
///
/// <para>⚠ <b>An earlier version of this comment said "no HR workflow definition is seeded
/// anywhere in the solution, so this was the out-of-the-box behaviour on every tenant". That is
/// false</b> (corrected 2026-09-16). <c>DatabaseSeedingService.EnsureHrWorkflowsSeededAsync</c>
/// seeds <b>28</b> HR definitions, <c>IsActive</c> and <c>Published</c>, covering every HR entity
/// type wired to the engine; names match through <c>NormalizeEntityTypeKey</c>. <b>On a seeded
/// tenant none of this fires.</b></para>
///
/// <para>So this is <b>defence in depth</b>, for the state where seeding has not run, a definition
/// was unpublished or deleted, or the tenant predates the seeder. That state is reachable and has
/// been hit: <c>EmployeeSalaryChangeRequestService</c> and <c>TeamActivityService</c> each wrote
/// their own guard after finding it, the latter "by the harness's no-definition assertion". The
/// failure mode is silent approval of pay changes and terminations with no trace that nobody was
/// asked, which is worth defending against even when the default configuration is sound.</para></para>
///
/// <para>It was found through recruitment (G-4.1 and G-10.1 of
/// <c>docs/HR/areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md</c>) and is not a recruitment defect: several HR
/// services carried a comment asserting the <i>opposite</i> — that submit and approve are
/// "inoperable by design until a definition is published". They were not inoperable. They
/// auto-approved, which is the dangerous direction to be wrong in, and the prose was the confident
/// half.</para>
///
/// <para><b>The shape of the fix, applied per service.</b> Four changes, and all four are needed
/// together — fixing only the first makes matters worse:</para>
/// <list type="number">
///   <item><description><b>Submit</b> asks <c>HasActiveApprovalWorkflowAsync</c> and, with no
///   definition, hands the adapter <c>WorkflowOutcome.Pending</c> instead of the engine's
///   <c>Approved</c>. The record lands at the module's own submitted/pending status.</description></item>
///   <item><description><b>Approve</b> takes a no-workflow branch, because
///   <c>CanUserApproveAsync</c> returns <c>false</c> when there is no active instance — so the
///   record would be unapprovable.</description></item>
///   <item><description><b>Reject</b> likewise, for the same reason.</description></item>
///   <item><description><b>Recall</b> likewise: <c>RecallWorkflowAsync</c> answers
///   <c>"No active workflow found"</c> without an instance.</description></item>
/// </list>
/// <para>Without 2–4, a submitted record would be stuck for ever with cancellation as its only
/// exit — a worse defect than the auto-approve it replaced.</para>
///
/// <para><b>Who decides, with no definition to name an approver.</b> The module's <c>.Approve</c>
/// tier — the block of permissions <see cref="HrPermissions"/> declares for exactly this purpose
/// and deletes once every entity type has a definition. The administer tier was the obvious
/// candidate and is the wrong one: those are destructive-operations permissions and several say so
/// in their own descriptions, so pinning approval to them would both misuse them and widen HR's
/// destructive reach as a side effect of letting HR approve. Pass the module's <c>.Approve</c>
/// constant.</para>
///
/// <para>⚠ <b>The calling service's own segregation-of-duties check is the stronger half of the
/// gate and still runs.</b> Holding the permission must never let you approve your own request.
/// This helper answers "may this person approve <i>anything</i> in this module", never "may this
/// person approve <i>this</i>".</para>
///
/// <para>⚠ Authority resolves from <see cref="HrPermissions.RoleGrants"/>, not from the database,
/// so a permission granted directly to a custom role is not seen — the limitation
/// <see cref="HrPermissions.RolesGrantAny"/> documents. That is acceptable because this is the
/// <i>unconfigured</i> path: a tenant wanting a named approver chain publishes a definition, and
/// then none of this code runs. If a tenant needs custom-role approvers, publish the definition
/// rather than widening this.</para>
///
/// <para><b>The shared fallback in <c>WorkflowIntegrationService</c> is deliberately left
/// alone.</b> It is used by procurement and quantity survey as well, whose callers were not
/// audited here. Changing it centrally would alter nine applications at once on the strength of an
/// HR finding. Each HR service opts out for itself, which is greppable: search for
/// <c>HasActiveApprovalWorkflowAsync</c> to see which services have been through this.</para>
/// </remarks>
public static class HrWorkflowFallbackAuthority
{
    /// <summary>
    /// True when the caller may rule on an unconfigured record in the module owning
    /// <paramref name="approvalPermissions"/>.
    /// </summary>
    public static bool CanRuleWithoutWorkflow(
        ICurrentUserProvider currentUser,
        params string[] approvalPermissions)
        => CanRuleWithoutWorkflow(currentUser.Roles, approvalPermissions);

    /// <summary>
    /// The role-list form. HR services hold one of two current-user abstractions —
    /// <see cref="ICurrentUserProvider"/> or <c>ICurrentUserService</c> — and both expose
    /// <c>Roles</c>, so the authority question is asked of the roles rather than of either
    /// interface. Saves injecting a second abstraction into a service that already has one.
    /// </summary>
    public static bool CanRuleWithoutWorkflow(
        IEnumerable<string>? roles,
        params string[] approvalPermissions)
    {
        var roleList = roles as IReadOnlyCollection<string> ?? roles?.ToList();

        return (roleList?.Any(r => string.Equals(r, Constants.Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)) ?? false)
               || HrPermissions.RolesGrantAny(roleList, approvalPermissions);
    }

    /// <summary>
    /// Refuses unless the caller may rule on an unconfigured record.
    /// </summary>
    /// <param name="currentUser">The caller.</param>
    /// <param name="action">Filled into the message, e.g. <c>"approve a staff movement"</c>.</param>
    /// <param name="approvalPermissions">The module's approval tier, e.g.
    /// <c>HrPermissions.ApproveMovements</c>.</param>
    public static void EnsureCanRuleWithoutWorkflow(
        ICurrentUserProvider currentUser,
        string action,
        params string[] approvalPermissions)
        => EnsureCanRuleWithoutWorkflow(currentUser.Roles, action, approvalPermissions);

    /// <inheritdoc cref="EnsureCanRuleWithoutWorkflow(ICurrentUserProvider, string, string[])"/>
    public static void EnsureCanRuleWithoutWorkflow(
        IEnumerable<string>? roles,
        string action,
        params string[] approvalPermissions)
    {
        if (CanRuleWithoutWorkflow(roles, approvalPermissions)) return;

        throw new UnauthorizedAccessException(
            $"No approval workflow is published for this record, so authority to {action} falls to " +
            "whoever holds the approve permission for this area. Ask them to decide, or publish an " +
            "approval workflow definition so the approver is named on the record.");
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // THE FOUR HELPERS
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //
    // 25 HR services carry this defect and each needs the same four coordinated changes. Doing it
    // by hand is ~20 lines per service and four chances to get one wrong; these make it three.
    //
    // ⚠ USE ALL FOUR OR NONE. A service with SubmitOutcomeAsync applied and RecallAsync not is
    // WORSE than one left alone: the record stops at a pending status and then cannot be recalled,
    // because RecallWorkflowAsync answers "No active workflow found" with no instance. Same for
    // approve and reject, which need an instance for CanUserApproveAsync to say yes to anybody.
    // The whole point of the fix is that a submitted record has somewhere to go.

    /// <summary>
    /// Submits to the engine and returns the outcome the adapter should be given.
    /// </summary>
    /// <remarks>
    /// <para>Replaces <c>workflow.SubmitAsync(...)</c> at the call site. Use
    /// <c>result.ExecutionResult</c> exactly as before for the success check; hand
    /// <c>result.Outcome</c> — <b>not</b> <c>result.Result.Outcome</c> — to
    /// <c>ApplySubmitOutcome</c>.</para>
    ///
    /// <para><b>What it changes.</b> When no definition is published, the engine returns
    /// <c>Approved</c> as its "approval is not configured" signal, and every HR adapter maps that
    /// to its own approved status — so Submit approved the record. This substitutes
    /// <c>Pending</c>, which every HR adapter's <c>default:</c> arm maps to the module's own
    /// pending status. Checked against all of them; if you add an adapter, check yours.</para>
    /// </remarks>
    public static async Task<(WorkflowIntegrationResult Result, WorkflowOutcome Outcome)> SubmitAsync(
        IWorkflowIntegrationService workflow,
        string entityType,
        Guid entityId)
    {
        var configured = await workflow.HasActiveApprovalWorkflowAsync(entityType);
        var result = await workflow.SubmitAsync(entityType, entityId);

        return (result, configured ? result.Outcome : WorkflowOutcome.Pending);
    }

    /// <summary>
    /// Approves or rejects, through the engine when one is configured and through the fallback
    /// when none is, and returns the outcome the adapter should be given.
    /// </summary>
    /// <param name="action"><c>"Approve"</c> or <c>"Reject"</c> — passed to the engine verbatim.</param>
    /// <param name="actingUserId">
    /// The <b>ApplicationUser</b> id. The engine resolves approvers by user, not by employee — see
    /// the note on <c>IWorkflowIntegrationService.ProcessApprovalAsync</c>.
    /// </param>
    /// <param name="actionDescription">
    /// Filled into the refusal, e.g. <c>"approve a leave request"</c>.
    /// </param>
    /// <param name="approvalPermissions">The module's <c>.Approve</c> permission.</param>
    /// <remarks>
    /// <para>⚠ <b>This answers "may this person approve anything of this kind", never "may they
    /// approve THIS one".</b> Segregation of duties — you cannot approve what you raised — stays
    /// in the calling service, where the requester field is, and must keep running on both
    /// branches. Do not move it here.</para>
    /// </remarks>
    public static Task<WorkflowOutcome> ProcessApprovalAsync(
        IWorkflowIntegrationService workflow,
        ICurrentUserProvider currentUser,
        string entityType,
        Guid entityId,
        Guid actingUserId,
        string action,
        string? comments,
        string actionDescription,
        params string[] approvalPermissions)
        => ProcessApprovalAsync(workflow, currentUser.Roles, entityType, entityId, actingUserId,
            action, comments, actionDescription, approvalPermissions);

    /// <inheritdoc cref="ProcessApprovalAsync(IWorkflowIntegrationService, ICurrentUserProvider, string, Guid, Guid, string, string?, string, string[])"/>
    public static async Task<WorkflowOutcome> ProcessApprovalAsync(
        IWorkflowIntegrationService workflow,
        IEnumerable<string>? actorRoles,
        string entityType,
        Guid entityId,
        Guid actingUserId,
        string action,
        string? comments,
        string actionDescription,
        params string[] approvalPermissions)
    {
        var isRejection = string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase);

        if (await workflow.HasActiveApprovalWorkflowAsync(entityType))
        {
            if (!await workflow.CanUserApproveAsync(entityType, entityId, actingUserId))
                throw new UnauthorizedAccessException(
                    "You are not assigned as an approver for the current workflow step.");

            var result = await workflow.ProcessApprovalAsync(entityType, entityId, actingUserId, action, comments);
            if (!result.ExecutionResult.Success)
                throw new InvalidOperationException(
                    result.ExecutionResult.Message
                    ?? (isRejection ? "Failed to process the rejection." : "Failed to process the approval."));

            return result.Outcome;
        }

        EnsureCanRuleWithoutWorkflow(actorRoles, actionDescription, approvalPermissions);
        return isRejection ? WorkflowOutcome.Rejected : WorkflowOutcome.Approved;
    }

    /// <summary>
    /// The form for a record whose decider the calling service names from the record itself, not from a
    /// permission: a pay or employment proposal is the Managing Director's (D-12, D-104), and a
    /// plan an HR officer put forward is the line manager's — people who hold no HR approve permission. Through the
    /// engine when a definition is published; with none, the caller's own rule — already run, and refusing everyone
    /// else — is the authority.
    /// </summary>
    /// <remarks>⚠ Call it only after the service's record-level rule has run on both branches.</remarks>
    public static async Task<WorkflowOutcome> ProcessApprovalDecidedByRecordAsync(
        IWorkflowIntegrationService workflow,
        string entityType,
        Guid entityId,
        Guid actingUserId,
        string action,
        string? comments)
    {
        var isRejection = string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase);

        if (!await workflow.HasActiveApprovalWorkflowAsync(entityType))
            return isRejection ? WorkflowOutcome.Rejected : WorkflowOutcome.Approved;

        if (!await workflow.CanUserApproveAsync(entityType, entityId, actingUserId))
            throw new UnauthorizedAccessException(
                "You are not assigned as an approver for the current workflow step.");

        var result = await workflow.ProcessApprovalAsync(entityType, entityId, actingUserId, action, comments);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(
                result.ExecutionResult.Message
                ?? (isRejection ? "Failed to process the rejection." : "Failed to process the approval."));

        return result.Outcome;
    }

    /// <summary>
    /// Runs the engine's recall when one is configured, and does nothing when none is.
    /// </summary>
    /// <returns>True when the engine ran; false when there was no workflow to recall.</returns>
    /// <remarks>
    /// <para>The caller still applies <c>ApplyRecallOutcome</c> either way — the record returns to
    /// Draft because the caller says so, not because the engine did.</para>
    ///
    /// <para>⚠ <b>The requester-only rule is the caller's.</b> On the configured path the engine
    /// enforces it; with no instance there is nothing to enforce it, so a service that relies on
    /// recall being requester-only must check that itself before calling this. Recruitment's
    /// offer recall is the worked example.</para>
    /// </remarks>
    public static async Task<bool> RecallAsync(
        IWorkflowIntegrationService workflow,
        string entityType,
        Guid entityId,
        Guid actingUserId,
        string? reason = null)
    {
        if (!await workflow.HasActiveApprovalWorkflowAsync(entityType)) return false;

        var result = await workflow.RecallAsync(entityType, entityId, actingUserId, reason);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(
                result.ExecutionResult.Message ?? "Failed to recall the record.");

        return true;
    }
}
