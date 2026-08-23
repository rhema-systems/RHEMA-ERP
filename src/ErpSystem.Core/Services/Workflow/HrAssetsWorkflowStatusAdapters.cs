using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapters for the two staff-asset surfaces that carry an approval —
/// <see cref="AssetRequisition"/> (somebody asks for an asset) and <see cref="AssetTransfer"/>
/// (an asset moves between people, places or units). Area 16, slice 3b, decision D3.
/// </summary>
/// <remarks>
/// <para><b>Why the engine at all.</b> Both surfaces arrived from the port with their own
/// <c>ApproveAsync</c>/<c>RejectAsync</c> — a status write and nothing else: no route, no
/// delegation, no inbox, no history, and no way for a tenant to say who signs. Assets are the
/// fifth HR area to need an approval chain, and a fifth bespoke one is a fifth thing to fix later.
/// D3 put both on the generic engine using the same four-step recipe as leave, attendance,
/// movements, discipline and separations.</para>
///
/// <para><b>⚠ The entity-type names are <c>HrAssetRequisition</c> and <c>HrAssetTransfer</c>, not
/// the bare class names.</b> <c>AssetTransfer</c> is declared twice in this solution — once here
/// under HR and once under Finance's fixed assets — and <c>WorkflowEntityDisplayService</c>
/// already resolves the key "AssetTransfer" to the Finance one. Registering HR's transfer under
/// the same key would have sent an HR approval notification to a fixed-asset screen, silently.
/// The build plan's §3.3 records the same collision at the entity, table and interface levels;
/// this is the fourth level it reaches.</para>
///
/// <para><b>Where the engine's authority stops.</b> On both records the engine owns exactly the
/// states before the decision and nothing after. Fulfilling a requisition — issuing the assets and
/// creating the assignments — and completing a transfer — actually moving the asset — are the
/// module doing the work that was authorised, not somebody approving it, so they stay direct
/// actions guarded by "only from Approved". That is the same boundary drawn for the two outcome
/// proposals and for PIP.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </remarks>
public sealed class AssetRequisitionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "HrAssetRequisition",
        "HR Asset Requisition",
        "HR_ASSET_REQUISITION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    /// <summary>
    /// Maps all four outcomes onto <see cref="AssetRequisitionStatus"/>.
    /// </summary>
    /// <remarks>
    /// <para>The enum already carried <c>Submitted</c>, which means exactly "out for approval", so
    /// unlike <c>PipStatus</c> no new member was needed — the rule from the sixth application of
    /// this recipe is to look before adding one.</para>
    ///
    /// <para><c>Rejected</c> is terminal here and does <b>not</b> return to Draft, because slice 2
    /// already settled that a decided requisition can be neither edited nor withdrawn: somebody
    /// ruled on it and the record of that ruling is the point. A requester who still wants the
    /// asset raises a new request. <c>Recalled</c> is the requester withdrawing before anyone
    /// ruled, so that <i>does</i> go back to Draft and the request becomes theirs to edit again.</para>
    ///
    /// <para><c>UnderReview</c> is left alone: it is a state HR sets by hand while it looks for
    /// stock, and the engine never writes it. Nothing here writes <c>Fulfilled</c> or
    /// <c>Cancelled</c> — issuing the assets is a later, separate act.</para>
    /// </remarks>
    private static void Apply(AssetRequisition requisition, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                requisition.Status = AssetRequisitionStatus.Approved;
                requisition.ApprovalDate = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                requisition.Status = AssetRequisitionStatus.Rejected;
                requisition.RejectedDate = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(reason))
                    requisition.RejectionReason = Trim(reason);
                break;
            case WorkflowOutcome.Recalled:
                requisition.Status = AssetRequisitionStatus.Draft;
                break;
            default:
                requisition.Status = AssetRequisitionStatus.Submitted;
                break;
        }
    }

    private static string Trim(string value)
        => value.Trim().Length > 1000 ? value.Trim()[..1000] : value.Trim();

    private static AssetRequisition Require(object entity)
        => entity as AssetRequisition ?? throw new InvalidOperationException("Expected AssetRequisition entity.");
}

/// <summary>
/// Workflow status adapter for <see cref="AssetTransfer"/> — the HR one. See the collision warning
/// on <see cref="AssetRequisitionWorkflowStatusAdapter"/> before touching the entity-type names.
/// </summary>
public sealed class HrAssetTransferWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "HrAssetTransfer",
        "HR Asset Transfer",
        "HR_ASSET_TRANSFER"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    /// <summary>
    /// Maps all four outcomes onto <see cref="HRAssetTransferStatus"/>.
    /// </summary>
    /// <remarks>
    /// <para><c>Pending</c> is the "out for approval" state and always was — what the enum lacked
    /// was a state before it, so slice 3b added <c>Draft</c>. Without that split "pending" meant
    /// both "nobody has sent this yet" and "an approver is holding it", and there was no state a
    /// recall could return to.</para>
    ///
    /// <para><c>InTransit</c>, <c>Completed</c> and <c>Cancelled</c> are never written here.
    /// Completing a transfer moves the asset; that is the module carrying out what was approved,
    /// and it stays a direct action allowed only from <c>Approved</c>.</para>
    /// </remarks>
    private static void Apply(AssetTransfer transfer, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                transfer.Status = HRAssetTransferStatus.Approved;
                transfer.ApprovalDate = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                transfer.Status = HRAssetTransferStatus.Rejected;
                if (!string.IsNullOrWhiteSpace(reason))
                    transfer.Notes = Append(transfer.Notes, $"Approval rejected: {reason.Trim()}");
                break;
            case WorkflowOutcome.Recalled:
                transfer.Status = HRAssetTransferStatus.Draft;
                break;
            default:
                transfer.Status = HRAssetTransferStatus.Pending;
                break;
        }
    }

    /// <summary>
    /// The rejection reason is appended to the transfer's notes as well as recorded on the history
    /// row, because the notes are what the initiator reads on the record they are about to redo. A
    /// transfer refused with no stated reason is the one that gets raised again unchanged. Trimmed
    /// to the 1000-character column.
    /// </summary>
    private static string Append(string? existing, string addition)
    {
        var combined = string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n{addition}";
        return combined.Length > 1000 ? combined[..1000] : combined;
    }

    private static AssetTransfer Require(object entity)
        => entity as AssetTransfer ?? throw new InvalidOperationException("Expected AssetTransfer entity.");
}
