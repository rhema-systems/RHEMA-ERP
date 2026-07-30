using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementRequisitionBudgetControlService : IProcurementRequisitionBudgetControlService
{
    private const string EventType = "PurchaseRequisitionBudgetControl";
    private const string SourceType = "PurchaseRequisition";
    private const string SubmitPermission = "procurement.requisition.create";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementBudgetReservationStore _reservationStore;

    public ProcurementRequisitionBudgetControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        IProcurementBudgetReservationStore reservationStore)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _reservationStore = reservationStore;
    }

    private IGenericRepository<PurchaseRequisition> Requisitions => _unitOfWork.Repository<PurchaseRequisition>();
    private IGenericRepository<ProcurementBudget> Budgets => _unitOfWork.Repository<ProcurementBudget>();
    private IGenericRepository<ProcurementBudgetCommitment> Commitments => _unitOfWork.Repository<ProcurementBudgetCommitment>();
    private IGenericRepository<ProcurementPolicyExceptionRule> ExceptionRules => _unitOfWork.Repository<ProcurementPolicyExceptionRule>();
    private IGenericRepository<WorkflowInstance> Workflows => _unitOfWork.Repository<WorkflowInstance>();
    private IGenericRepository<ProcurementControlEvent> ControlEventRows => _unitOfWork.Repository<ProcurementControlEvent>();

    public async Task<PurchaseRequisitionBudgetReadinessDto> GetReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var requisition = await Requisitions.GetQueryable(item =>
                item.Id == requisitionId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementRequisitionBudgetNotFoundException(
                "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");

        var budget = requisition.BudgetId.HasValue
            ? await Budgets.GetQueryable(item => item.Id == requisition.BudgetId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            : null;
        var commitment = await Commitments.GetQueryable(item => item.PurchaseRequisitionId == requisition.Id &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);

        return await EvaluateAsync(requisition, budget, commitment, cancellationToken);
    }

    public async Task<PurchaseRequisitionBudgetReadinessDto> ReserveAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureRequisition(requisition);
        await EnsureCapabilityAsync(SubmitPermission, requisition.RequisitionNumber, correlationId, cancellationToken);
        if (!_reservationStore.HasRequiredTransaction)
            throw new ProcurementRequisitionBudgetConflictException(
                "PR_BUDGET_TRANSACTION_REQUIRED",
                "Budget reservation must execute inside the purchase-requisition submission transaction.");

        ProcurementBudget? budget = null;
        if (requisition.BudgetId.HasValue)
            budget = await _reservationStore.GetBudgetForUpdateAsync(
                _currentUser.TenantId, requisition.BudgetId.Value, cancellationToken);

        var commitment = await Commitments.GetQueryable(item => item.PurchaseRequisitionId == requisition.Id &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);
        var readiness = await EvaluateAsync(requisition, budget, commitment, cancellationToken);

        if (!readiness.CanReserve)
        {
            await RecordAsync(requisition, readiness, "BudgetReservationBlocked",
                ProcurementControlEventResult.Denied, commitment, null, correlationId, cancellationToken);
            return readiness;
        }

        if (commitment?.Status == ProcurementBudgetCommitmentStatus.Reserved)
        {
            var snapshot = Snapshot(commitment);
            await RecordAsync(requisition, readiness, "BudgetReservationReused",
                ProcurementControlEventResult.Allowed, snapshot, snapshot, correlationId, cancellationToken);
            return readiness;
        }

        if (budget is null)
            throw new ProcurementRequisitionBudgetNotFoundException(
                "PR_BUDGET_NOT_FOUND", "The linked procurement budget was not found in the current tenant.");
        if (commitment?.Status == ProcurementBudgetCommitmentStatus.Consumed)
            throw new ProcurementRequisitionBudgetConflictException(
                "PR_BUDGET_COMMITMENT_CONSUMED", "The requisition budget commitment has already been consumed.");

        var now = DateTime.UtcNow;
        var beforeCommitted = budget.CommittedAmount;
        var beforeAvailable = Available(budget);
        budget.CommittedAmount += requisition.TotalAmount;
        budget.RemainingAmount = Available(budget);
        budget.UpdatedAt = now;

        var before = commitment is null ? null : Snapshot(commitment);
        var overrideRule = readiness.OverrideRuleId.HasValue
            ? await ExceptionRules.GetQueryable(item => item.Id == readiness.OverrideRuleId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        if (commitment is null)
        {
            commitment = new ProcurementBudgetCommitment
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                PurchaseRequisitionId = requisition.Id,
                ReservationReference = BuildReference(requisition),
                ReservationSequence = 1,
                CreatedAt = now,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId
            };
            await Commitments.AddAsync(commitment);
        }
        else
        {
            commitment.ReservationSequence += 1;
            await Commitments.UpdateAsync(commitment);
        }

        commitment.ProcurementBudgetId = budget.Id;
        commitment.Status = ProcurementBudgetCommitmentStatus.Reserved;
        commitment.ReservedAmount = requisition.TotalAmount;
        commitment.Currency = budget.Currency;
        commitment.BudgetAllocatedSnapshot = budget.AllocatedAmount;
        commitment.BudgetUtilizedSnapshot = budget.UtilizedAmount;
        commitment.BudgetCommittedBefore = beforeCommitted;
        commitment.BudgetAvailableBefore = beforeAvailable;
        commitment.BudgetCommittedAfter = budget.CommittedAmount;
        commitment.BudgetAvailableAfter = budget.RemainingAmount;
        commitment.IsOverride = readiness.IsOverride;
        commitment.OverrideRuleId = overrideRule?.Id;
        commitment.OverrideRuleCode = overrideRule?.RuleCode;
        commitment.OverrideWorkflowInstanceId = readiness.OverrideWorkflowInstanceId;
        commitment.OverrideApprovalReference = readiness.OverrideApprovalReference;
        commitment.OverrideEvidenceReference = readiness.OverrideEvidenceReference;
        commitment.OverrideApprovedAtUtc = readiness.OverrideApprovedAtUtc;
        commitment.ReservedAtUtc = now;
        commitment.ReservedById = _currentUser.UserId;
        commitment.ReservedByName = ActorName();
        commitment.ReleasedAtUtc = null;
        commitment.ReleasedById = null;
        commitment.ReleasedByName = null;
        commitment.ReleaseReason = null;
        commitment.CorrelationId = NormalizeCorrelation(correlationId);
        commitment.UpdatedAt = now;
        commitment.UpdatedBy = _currentUser.Username;
        commitment.LastModifiedById = _currentUser.UserId;

        requisition.BudgetValidated = true;
        requisition.BudgetAllocated = budget.AllocatedAmount;
        requisition.BudgetRemaining = budget.RemainingAmount;
        requisition.UpdatedAt = now;

        await Budgets.UpdateAsync(budget);
        readiness = PopulateCommitment(readiness, commitment, budget);
        await RecordAsync(requisition, readiness, "BudgetCommitmentReserved",
            ProcurementControlEventResult.Allowed, before, Snapshot(commitment), correlationId, cancellationToken);
        return readiness;
    }

    public async Task<PurchaseRequisitionBudgetReleaseDto> ReleaseAsync(
        PurchaseRequisition requisition,
        string reason,
        string requiredPermissionCode,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureRequisition(requisition, requireDraft: false);
        if (string.IsNullOrWhiteSpace(reason))
            throw new ProcurementRequisitionBudgetValidationException(
                "PR_BUDGET_RELEASE_REASON_REQUIRED", "A budget commitment release reason is required.");
        await EnsureCapabilityAsync(requiredPermissionCode, requisition.RequisitionNumber, correlationId, cancellationToken);
        if (!_reservationStore.HasRequiredTransaction)
            throw new ProcurementRequisitionBudgetConflictException(
                "PR_BUDGET_TRANSACTION_REQUIRED",
                "Budget release must execute inside the purchase-requisition status transaction.");

        var commitment = await Commitments.GetQueryable(item => item.PurchaseRequisitionId == requisition.Id &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);
        if (commitment is null || commitment.Status != ProcurementBudgetCommitmentStatus.Reserved)
        {
            return new PurchaseRequisitionBudgetReleaseDto
            {
                RequisitionId = requisition.Id,
                CommitmentId = commitment?.Id,
                CommitmentReference = commitment?.ReservationReference,
                Message = commitment is null
                    ? "The requisition has no budget commitment to release."
                    : $"The budget commitment is already {commitment.Status}."
            };
        }

        var budget = await _reservationStore.GetBudgetForUpdateAsync(
                _currentUser.TenantId, commitment.ProcurementBudgetId, cancellationToken)
            ?? throw new ProcurementRequisitionBudgetNotFoundException(
                "PR_BUDGET_NOT_FOUND", "The committed procurement budget was not found in the current tenant.");

        var before = Snapshot(commitment);
        var now = DateTime.UtcNow;
        budget.CommittedAmount = Math.Max(0, budget.CommittedAmount - commitment.ReservedAmount);
        budget.RemainingAmount = Available(budget);
        budget.UpdatedAt = now;
        commitment.Status = ProcurementBudgetCommitmentStatus.Released;
        commitment.ReleasedAtUtc = now;
        commitment.ReleasedById = _currentUser.UserId;
        commitment.ReleasedByName = ActorName();
        commitment.ReleaseReason = Truncate(reason.Trim(), 500);
        commitment.CorrelationId = NormalizeCorrelation(correlationId);
        commitment.UpdatedAt = now;
        commitment.UpdatedBy = _currentUser.Username;
        commitment.LastModifiedById = _currentUser.UserId;
        requisition.BudgetValidated = false;
        requisition.BudgetRemaining = budget.RemainingAmount;
        requisition.UpdatedAt = now;

        await Budgets.UpdateAsync(budget);
        await Commitments.UpdateAsync(commitment);
        var readiness = PopulateCommitment(BaseReadiness(requisition), commitment, budget);
        readiness.IsCompliant = false;
        readiness.CanReserve = false;
        readiness.DecisionCode = "PR_BUDGET_COMMITMENT_RELEASED";
        readiness.Message = commitment.ReleaseReason;
        readiness.Basis = "ReleasedCommitment";
        await RecordAsync(requisition, readiness, "BudgetCommitmentReleased",
            ProcurementControlEventResult.Succeeded, before, Snapshot(commitment), correlationId, cancellationToken);

        return new PurchaseRequisitionBudgetReleaseDto
        {
            RequisitionId = requisition.Id,
            Released = true,
            CommitmentId = commitment.Id,
            CommitmentReference = commitment.ReservationReference,
            ReleasedAmount = commitment.ReservedAmount,
            AvailableAmount = budget.RemainingAmount,
            Message = "The purchase-requisition budget commitment was released."
        };
    }

    public async Task<IReadOnlyList<PurchaseRequisitionBudgetControlHistoryDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var exists = await Requisitions.GetQueryable(item => item.Id == requisitionId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().AnyAsync(cancellationToken);
        if (!exists)
            throw new ProcurementRequisitionBudgetNotFoundException(
                "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");

        return await ControlEventRows.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.EventType == EventType && item.SourceId == requisitionId && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => new PurchaseRequisitionBudgetControlHistoryDto
            {
                Id = item.Id,
                Action = item.Action,
                Result = item.Result.ToString(),
                ActorName = item.ActorName,
                RuleCode = item.RuleCode,
                Reason = item.Reason,
                OccurredAtUtc = item.OccurredAtUtc,
                IntegrityHash = item.IntegrityHash
            }).ToListAsync(cancellationToken);
    }

    private async Task<PurchaseRequisitionBudgetReadinessDto> EvaluateAsync(
        PurchaseRequisition requisition,
        ProcurementBudget? budget,
        ProcurementBudgetCommitment? commitment,
        CancellationToken cancellationToken)
    {
        var result = BaseReadiness(requisition);
        if (commitment?.Status == ProcurementBudgetCommitmentStatus.Reserved)
        {
            var latestAmendmentAdjustment = await _unitOfWork
                .Repository<ProcurementPurchaseOrderCommitmentAdjustment>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseRequisitionId == requisition.Id &&
                    !item.IsDeleted &&
                    item.Amendment.Status !=
                    ProcurementPurchaseOrderAmendmentStatus.Rejected &&
                    item.Amendment.Status !=
                    ProcurementPurchaseOrderAmendmentStatus.Cancelled &&
                    !item.Amendment.IsDeleted)
                .AsNoTracking()
                .OrderByDescending(item => item.AppliedAtUtc)
                .ThenByDescending(item => item.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            var expectedReservedAmount =
                latestAmendmentAdjustment?.CommitmentAmountAfter ??
                requisition.TotalAmount;
            if (commitment.ProcurementBudgetId != requisition.BudgetId ||
                commitment.ReservedAmount != expectedReservedAmount)
                return Block(result, "PR_BUDGET_COMMITMENT_MISMATCH",
                    "The active reservation does not match the approved requisition or latest applied PO-amendment commitment adjustment.",
                    "Reconcile the budget commitment and immutable amendment-adjustment ledger before progressing procurement.");
            result.IsCompliant = true;
            result.CanReserve = true;
            result.DecisionCode = latestAmendmentAdjustment is null
                ? "PR_BUDGET_COMMITMENT_ACTIVE"
                : "PR_BUDGET_COMMITMENT_AMENDMENT_ADJUSTED";
            result.Message = latestAmendmentAdjustment is null
                ? "An idempotent active budget commitment already protects this requisition."
                : $"The active budget commitment is reconciled to PO amendment {latestAmendmentAdjustment.Amendment.AmendmentNumber}.";
            result.Basis = latestAmendmentAdjustment is null
                ? "ExistingCommitment"
                : "ApprovedPurchaseOrderAmendment";
            result.RequestedAmount = commitment.ReservedAmount;
            return PopulateCommitment(result, commitment, budget);
        }

        if (!string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            return Block(result, "PR_BUDGET_RESERVATION_NOT_AVAILABLE",
                "Budget reservation is available only while the purchase requisition is Draft.",
                "Return the requisition to its authorized Draft correction path before resubmitting.");
        if (requisition.TotalAmount <= 0)
            return Block(result, "PR_AMOUNT_REQUIRED", "The requisition total must be greater than zero.",
                "Add valid requisition lines with a positive total amount.");
        if (!requisition.BudgetId.HasValue)
            return Block(result, "PR_BUDGET_REQUIRED", "No approved procurement budget is linked.",
                "Link the Draft to an approved, currently effective procurement budget.");
        if (budget is null)
            return Block(result, "PR_BUDGET_NOT_FOUND",
                "The linked procurement budget is unavailable in the current tenant.",
                "Select an approved budget from the current tenant.");

        PopulateBudget(result, budget);
        if (!string.IsNullOrWhiteSpace(requisition.BudgetCode) &&
            !string.Equals(requisition.BudgetCode, budget.BudgetCode, StringComparison.OrdinalIgnoreCase))
            return Block(result, "PR_BUDGET_SNAPSHOT_MISMATCH",
                "The requisition budget snapshot no longer matches the linked budget.",
                "Refresh the Draft governance linkage before submission.");
        if (!string.Equals(budget.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(budget.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return Block(result, "PR_BUDGET_NOT_APPROVED",
                $"Budget {budget.BudgetCode} is {budget.Status} and cannot accept commitments.",
                "Select a Finance-approved budget.");
        if (!budget.ApprovedById.HasValue || !budget.ApprovedDate.HasValue)
            return Block(result, "PR_BUDGET_APPROVAL_INCOMPLETE",
                "The linked budget lacks Finance approval lineage.",
                "Complete budget approval with approver and approval timestamp.");
        var now = DateTime.UtcNow;
        if (budget.EffectiveDate.HasValue && budget.EffectiveDate.Value > now)
            return Block(result, "PR_BUDGET_NOT_EFFECTIVE", "The linked budget is not yet effective.",
                "Use a currently effective approved budget.");
        if (budget.ExpiryDate.HasValue && budget.ExpiryDate.Value < now)
            return Block(result, "PR_BUDGET_EXPIRED", "The linked budget has expired.",
                "Use a currently effective approved budget.");
        if (!string.Equals(budget.Currency, requisition.Currency, StringComparison.OrdinalIgnoreCase))
            return Block(result, "PR_BUDGET_CURRENCY_MISMATCH",
                $"Budget currency {budget.Currency} does not match requisition currency {requisition.Currency}.",
                "Link a budget in the requisition currency.");

        var available = Available(budget);
        result.AvailableAmount = available;
        result.ShortfallAmount = Math.Max(0, requisition.TotalAmount - available);
        if (available >= requisition.TotalAmount)
        {
            result.IsCompliant = true;
            result.CanReserve = true;
            result.DecisionCode = "PR_BUDGET_AVAILABLE";
            result.Message = $"Budget {budget.BudgetCode} has sufficient available funds for an atomic commitment.";
            result.Basis = "ApprovedBudget";
            return result;
        }

        var budgetOverride = await EvaluateOverrideAsync(requisition, cancellationToken);
        if (!budgetOverride.Allowed)
            return Block(result, budgetOverride.Code,
                $"Budget {budget.BudgetCode} is short by {result.ShortfallAmount:N2} {budget.Currency}. {budgetOverride.Message}",
                budgetOverride.Action);

        result.IsCompliant = true;
        result.CanReserve = true;
        result.DecisionCode = "PR_BUDGET_OVERRIDE_APPROVED";
        result.Message = $"An approved budget override authorizes the {result.ShortfallAmount:N2} {budget.Currency} shortfall.";
        result.Basis = "AuthorizedOverride";
        result.IsOverride = true;
        result.OverrideRuleId = budgetOverride.Rule!.Id;
        result.OverrideRuleCode = budgetOverride.Rule.RuleCode;
        result.OverrideWorkflowInstanceId = budgetOverride.Workflow!.Id;
        result.OverrideApprovalReference = requisition.ExceptionApprovalReference;
        result.OverrideEvidenceReference = requisition.ExceptionEvidenceReference;
        result.OverrideApprovedAtUtc = requisition.ExceptionApprovedAtUtc;
        return result;
    }

    private async Task<BudgetOverridePath> EvaluateOverrideAsync(
        PurchaseRequisition requisition,
        CancellationToken cancellationToken)
    {
        const string action = "Obtain a current approved budget-shortfall exception with completed workflow, justification, and evidence, or reduce the requisition amount.";
        if (!requisition.ApprovedExceptionRuleId.HasValue)
            return BudgetOverridePath.Deny("PR_BUDGET_INSUFFICIENT", "No budget-shortfall override is linked.", action);

        var now = DateTime.UtcNow;
        var rule = await ExceptionRules.GetQueryable(item => item.Id == requisition.ApprovedExceptionRuleId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.PolicySet).SingleOrDefaultAsync(cancellationToken);
        if (rule is null)
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_RULE_NOT_FOUND", "The override rule is unavailable in the current tenant.", action);
        if (rule.PolicySet.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published ||
            rule.PolicySet.PublishedAt is null || rule.PolicySet.EffectiveFrom > now ||
            (rule.PolicySet.EffectiveTo.HasValue && rule.PolicySet.EffectiveTo.Value < now) ||
            !rule.IsEnabled || rule.OverrideAction == ProcurementPolicyOverrideAction.Disable ||
            rule.EffectiveFrom > now || (rule.EffectiveTo.HasValue && rule.EffectiveTo.Value < now))
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_NOT_EFFECTIVE", "The linked override rule is not currently effective.", action, rule);
        if (rule.Disposition == ProcurementExceptionDisposition.Prohibited ||
            !rule.ExceptionType.Contains("budget", StringComparison.OrdinalIgnoreCase))
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_TYPE_INVALID", "The linked exception does not authorize a budget shortfall.", action, rule);
        if (rule.Category.HasValue && rule.Category != requisition.ProcurementCategory)
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_CATEGORY_MISMATCH", "The override does not cover this requisition category.", action, rule);
        if (string.IsNullOrWhiteSpace(requisition.Justification))
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_JUSTIFICATION_REQUIRED", "Override justification is missing.", action, rule);
        if (string.IsNullOrWhiteSpace(requisition.ExceptionEvidenceReference))
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_EVIDENCE_REQUIRED", "Override evidence is missing.", action, rule);
        if (!requisition.ExceptionWorkflowInstanceId.HasValue)
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_WORKFLOW_REQUIRED", "The budget override workflow is missing.", action, rule);

        var workflow = await Workflows.GetQueryable(item => item.Id == requisition.ExceptionWorkflowInstanceId.Value &&
                item.TenantId == _currentUser.TenantId && item.EntityId == requisition.Id && !item.IsDeleted)
            .Include(item => item.EntityType).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (workflow is null)
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_WORKFLOW_NOT_FOUND", "The same-requisition override workflow was not found.", action, rule);
        if (!string.Equals(workflow.EntityType.Code, "PROCUREMENT_EXCEPTION", StringComparison.OrdinalIgnoreCase) ||
            workflow.Status != WorkflowInstanceStatus.Completed || !workflow.CompletedDate.HasValue)
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_WORKFLOW_NOT_APPROVED", "The budget override workflow has not completed with approval.", action, rule, workflow);
        if (rule.WorkflowDefinitionId.HasValue && workflow.WorkflowDefinitionId != rule.WorkflowDefinitionId.Value)
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_WORKFLOW_DEFINITION_MISMATCH", "The workflow does not use the definition configured by the rule.", action, rule, workflow);
        if (rule.MaximumDurationDays.HasValue && workflow.CompletedDate.Value.AddDays(rule.MaximumDurationDays.Value) < now)
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_EXPIRED", "The budget override approval has expired.", action, rule, workflow);
        if (string.IsNullOrWhiteSpace(requisition.ExceptionApprovalReference) || !requisition.ExceptionApprovedAtUtc.HasValue)
            return BudgetOverridePath.Deny("PR_BUDGET_OVERRIDE_APPROVAL_INCOMPLETE", "The override approval reference or timestamp is missing.", action, rule, workflow);
        return BudgetOverridePath.Allow(rule, workflow);
    }

    private async Task RecordAsync(
        PurchaseRequisition requisition,
        PurchaseRequisitionBudgetReadinessDto readiness,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var evidence = new List<ProcurementControlEventEvidenceReference>();
        if (readiness.OverrideWorkflowInstanceId.HasValue)
            evidence.Add(External($"workflow:{readiness.OverrideWorkflowInstanceId.Value:N}", "Budget override workflow", "BUDGET_OVERRIDE_APPROVAL"));
        if (!string.IsNullOrWhiteSpace(readiness.OverrideApprovalReference))
            evidence.Add(External(readiness.OverrideApprovalReference, "Budget override approval", "BUDGET_OVERRIDE_APPROVAL"));
        if (!string.IsNullOrWhiteSpace(readiness.OverrideEvidenceReference))
            evidence.Add(External(readiness.OverrideEvidenceReference, "Budget override evidence", "BUDGET_OVERRIDE_EVIDENCE"));
        var decisionKeys = readiness.IsOverride ? new List<string> { "DEC-006" } : new List<string>();

        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("pr-budget-control", requisition.TenantId,
                requisition.Id, action.ToLowerInvariant(), Guid.NewGuid()),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = readiness.OverrideRuleCode ?? "TDC-0105",
            RuleId = readiness.OverrideRuleId,
            DecisionKeys = decisionKeys,
            SourceType = SourceType,
            SourceId = requisition.Id,
            SourceReference = requisition.RequisitionNumber,
            Reason = readiness.Message,
            InputValues = new
            {
                requisition.BudgetId,
                requisition.BudgetCode,
                requisition.TotalAmount,
                requisition.Currency,
                requisition.ApprovedExceptionRuleId,
                requisition.ExceptionWorkflowInstanceId,
                requisition.Justification,
                requisition.ExceptionApprovalReference,
                requisition.ExceptionEvidenceReference,
                requisition.ExceptionApprovedAtUtc
            },
            ResultValues = readiness,
            Before = before,
            After = after,
            CorrelationId = NormalizeCorrelation(correlationId),
            CausationId = NormalizeCorrelation(correlationId),
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = evidence
        }, cancellationToken);
    }

    private static PurchaseRequisitionBudgetReadinessDto BaseReadiness(PurchaseRequisition requisition) => new()
    {
        RequisitionId = requisition.Id,
        RequisitionNumber = requisition.RequisitionNumber,
        Status = requisition.Status,
        BudgetId = requisition.BudgetId,
        BudgetCode = requisition.BudgetCode,
        Currency = requisition.Currency,
        RequestedAmount = requisition.TotalAmount
    };

    private static PurchaseRequisitionBudgetReadinessDto Block(
        PurchaseRequisitionBudgetReadinessDto result,
        string code,
        string message,
        string action)
    {
        result.IsCompliant = false;
        result.CanReserve = false;
        result.DecisionCode = code;
        result.Message = message;
        result.RequiredActions = [action];
        return result;
    }

    private static void PopulateBudget(PurchaseRequisitionBudgetReadinessDto result, ProcurementBudget budget)
    {
        result.BudgetId = budget.Id;
        result.BudgetCode = budget.BudgetCode;
        result.BudgetStatus = budget.Status;
        result.Currency = budget.Currency;
        result.AllocatedAmount = budget.AllocatedAmount;
        result.UtilizedAmount = budget.UtilizedAmount;
        result.CommittedAmount = budget.CommittedAmount;
        result.AvailableAmount = Available(budget);
    }

    private static PurchaseRequisitionBudgetReadinessDto PopulateCommitment(
        PurchaseRequisitionBudgetReadinessDto result,
        ProcurementBudgetCommitment commitment,
        ProcurementBudget? budget)
    {
        if (budget is not null) PopulateBudget(result, budget);
        result.CommitmentId = commitment.Id;
        result.CommitmentReference = commitment.ReservationReference;
        result.CommitmentStatus = commitment.Status.ToString();
        result.ReservationSequence = commitment.ReservationSequence;
        result.ReservedAtUtc = commitment.ReservedAtUtc;
        result.IsOverride = commitment.IsOverride;
        result.OverrideRuleId = commitment.OverrideRuleId;
        result.OverrideRuleCode = commitment.OverrideRuleCode;
        result.OverrideWorkflowInstanceId = commitment.OverrideWorkflowInstanceId;
        result.OverrideApprovalReference = commitment.OverrideApprovalReference;
        result.OverrideEvidenceReference = commitment.OverrideEvidenceReference;
        result.OverrideApprovedAtUtc = commitment.OverrideApprovedAtUtc;
        result.AvailableAmount = commitment.Status == ProcurementBudgetCommitmentStatus.Reserved
            ? commitment.BudgetAvailableAfter
            : result.AvailableAmount;
        result.ShortfallAmount = Math.Max(0, result.RequestedAmount - commitment.BudgetAvailableBefore);
        return result;
    }

    private static object Snapshot(ProcurementBudgetCommitment commitment) => new
    {
        commitment.Id,
        commitment.ProcurementBudgetId,
        commitment.PurchaseRequisitionId,
        commitment.ReservationReference,
        commitment.ReservationSequence,
        Status = commitment.Status.ToString(),
        commitment.ReservedAmount,
        commitment.Currency,
        commitment.BudgetCommittedBefore,
        commitment.BudgetAvailableBefore,
        commitment.BudgetCommittedAfter,
        commitment.BudgetAvailableAfter,
        commitment.IsOverride,
        commitment.OverrideRuleCode,
        commitment.OverrideApprovalReference,
        commitment.ReservedAtUtc,
        commitment.ReleasedAtUtc,
        commitment.ReleaseReason
    };

    private async Task EnsureCapabilityAsync(
        string permissionCode,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permissionCode,
            SourceType = SourceType,
            SourceReference = sourceReference
        }, NormalizeCorrelation(correlationId), cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementRequisitionBudgetAuthorizationException(decision.Message);
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator() || _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role => ProcurementAccessControlRegistry.FindRole(role) is not null)) return;
        throw new ProcurementRequisitionBudgetAuthorizationException(
            "A TDC procurement role or tenant-administration role is required.");
    }

    private void EnsureRequisition(PurchaseRequisition requisition, bool requireDraft = true)
    {
        EnsureAuthenticatedTenant();
        if (requisition.TenantId != _currentUser.TenantId || requisition.IsDeleted)
            throw new ProcurementRequisitionBudgetNotFoundException(
                "PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.");
        if (requireDraft && !string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new ProcurementRequisitionBudgetConflictException(
                "PR_NOT_DRAFT", "Only a Draft purchase requisition can reserve budget.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementRequisitionBudgetAuthorizationException("An authenticated tenant context is required.");
    }

    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName)
        ? _currentUser.Username : _currentUser.FullName, 300);
    private static decimal Available(ProcurementBudget budget) =>
        budget.AllocatedAmount - budget.UtilizedAmount - budget.CommittedAmount;
    private static string BuildReference(PurchaseRequisition requisition) =>
        Truncate($"BCR-{requisition.RequisitionNumber}", 100);
    private static string NormalizeCorrelation(string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : Truncate(correlationId.Trim(), 100);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    private static ProcurementControlEventEvidenceReference External(
        string reference,
        string label,
        string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference,
        Label = label,
        RequirementKey = requirement
    };

    private sealed record BudgetOverridePath(
        bool Allowed,
        string Code,
        string Message,
        string Action,
        ProcurementPolicyExceptionRule? Rule,
        WorkflowInstance? Workflow)
    {
        public static BudgetOverridePath Deny(
            string code,
            string message,
            string action,
            ProcurementPolicyExceptionRule? rule = null,
            WorkflowInstance? workflow = null) => new(false, code, message, action, rule, workflow);

        public static BudgetOverridePath Allow(
            ProcurementPolicyExceptionRule rule,
            WorkflowInstance workflow) => new(true, "PR_BUDGET_OVERRIDE_APPROVED",
            "The budget-shortfall override is approved.", string.Empty, rule, workflow);
    }
}
