using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementPurchaseOrderAmendmentService :
    IProcurementPurchaseOrderAmendmentService
{
    private const string EventType = "ProcurementPurchaseOrderAmendment";
    private const string SourceType = "PurchaseOrder";
    private const string WorkflowEntityType = "PurchaseOrder";
    private const string ManagePermission = "procurement.purchase-order.create";
    private const string ApprovePermission = "procurement.purchase-order.approve";
    private const string ReadPermission = "procurement.records.read";
    private const string PendingStatus = "Amendment Pending Approval";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementPurchaseOrderSourceService _sources;
    private readonly IProcurementPurchaseOrderSodService _sod;
    private readonly IProcurementPurchaseOrderComplianceService _compliance;
    private readonly IProcurementFrameworkCallOffService _frameworkCallOffs;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IProcurementBudgetReservationStore _budgetStore;
    private readonly IProcurementPurchaseOrderAmendmentStore _amendmentStore;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly ILogger<ProcurementPurchaseOrderAmendmentService> _logger;

    public ProcurementPurchaseOrderAmendmentService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementPurchaseOrderSourceService sources,
        IProcurementPurchaseOrderSodService sod,
        IProcurementPurchaseOrderComplianceService compliance,
        IProcurementFrameworkCallOffService frameworkCallOffs,
        IWorkflowIntegrationService workflow,
        IProcurementBudgetReservationStore budgetStore,
        IProcurementPurchaseOrderAmendmentStore amendmentStore,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        ILogger<ProcurementPurchaseOrderAmendmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sources = sources;
        _sod = sod;
        _compliance = compliance;
        _frameworkCallOffs = frameworkCallOffs;
        _workflow = workflow;
        _budgetStore = budgetStore;
        _amendmentStore = amendmentStore;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _logger = logger;
    }

    private IGenericRepository<PurchaseOrder> PurchaseOrders =>
        _unitOfWork.Repository<PurchaseOrder>();
    private IGenericRepository<PurchaseOrderItem> PurchaseOrderItems =>
        _unitOfWork.Repository<PurchaseOrderItem>();
    private IGenericRepository<PurchaseOrderReceipt> Receipts =>
        _unitOfWork.Repository<PurchaseOrderReceipt>();
    private IGenericRepository<ProcurementPurchaseOrderAmendment> Amendments =>
        _unitOfWork.Repository<ProcurementPurchaseOrderAmendment>();
    private IGenericRepository<ProcurementPurchaseOrderCommitmentAdjustment> Adjustments =>
        _unitOfWork.Repository<ProcurementPurchaseOrderCommitmentAdjustment>();
    private IGenericRepository<ProcurementPurchaseOrderAmendmentDispatch> Dispatches =>
        _unitOfWork.Repository<ProcurementPurchaseOrderAmendmentDispatch>();
    private IGenericRepository<ProcurementPurchaseOrderAmendmentAcknowledgement> Acknowledgements =>
        _unitOfWork.Repository<ProcurementPurchaseOrderAmendmentAcknowledgement>();
    private IGenericRepository<ProcurementBudgetCommitment> Commitments =>
        _unitOfWork.Repository<ProcurementBudgetCommitment>();

    public async Task<ProcurementPurchaseOrderAmendmentOverviewDto> GetOverviewAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var purchaseOrder = await PurchaseOrderQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == purchaseOrderId, cancellationToken)
            ?? throw NotFound("PO_AMENDMENT_PURCHASE_ORDER_NOT_FOUND",
                "The purchase order was not found in the current tenant.");
        await EnsureCapabilityAsync(
            ReadPermission, purchaseOrder.OrderNumber,
            Guid.NewGuid().ToString("N"), cancellationToken);

        var rows = await AmendmentQuery()
            .Where(item => item.PurchaseOrderId == purchaseOrderId)
            .AsNoTracking()
            .OrderByDescending(item => item.AmendmentSequence)
            .ToListAsync(cancellationToken);
        var hasReceipts = await Receipts.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrderId &&
                !item.IsDeleted)
            .AsNoTracking()
            .AnyAsync(cancellationToken);
        var isFramework = await _frameworkCallOffs.IsFrameworkCallOffPurchaseOrderAsync(
            purchaseOrderId, cancellationToken);
        var hasOpen = rows.Any(item =>
            !ProcurementPurchaseOrderAmendmentRules.IsTerminal(item.Status));
        var canCreate = !isFramework &&
                        ProcurementPurchaseOrderAmendmentRules.CanCreate(
                            purchaseOrder.Status, hasReceipts, hasOpen);

        return new ProcurementPurchaseOrderAmendmentOverviewDto
        {
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrderNumber = purchaseOrder.OrderNumber,
            CurrentRevisionNumber = purchaseOrder.RevisionNumber,
            IsFrameworkCallOff = isFramework,
            CanCreateAmendment = canCreate,
            BlockedReason = canCreate
                ? null
                : BlockedReason(purchaseOrder.Status, isFramework, hasReceipts, hasOpen),
            DecisionKeys = DecisionKeys,
            Amendments = rows.Select(Map).ToList()
        };
    }

    public async Task<IReadOnlyList<ProcurementPurchaseOrderAmendmentDto>>
        GetExternalOverviewAsync(
            CancellationToken cancellationToken = default)
    {
        EnsureExternalReader();
        var partnerIds = await GetExternalPartnerIdsAsync(cancellationToken);
        if (partnerIds.Count == 0)
            throw Authorization(
                "The current user is not linked to an active approved supplier account.");

        var rows = await AmendmentQuery()
            .Where(item =>
                partnerIds.Contains(item.ProposedBusinessPartnerId) &&
                item.Dispatches.Any(dispatch => !dispatch.IsDeleted))
            .AsNoTracking()
            .OrderByDescending(item => item.AmendmentSequence)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProcurementPurchaseOrderAmendmentDto> CreateAsync(
        Guid purchaseOrderId,
        CreateProcurementPurchaseOrderAmendmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.Reason, "PO_AMENDMENT_REASON_REQUIRED",
            "A documented amendment reason is required.");
        Require(request.ChangeScope, "PO_AMENDMENT_SCOPE_REQUIRED",
            "The amendment change scope is required.");
        Require(request.EvidenceReference, "PO_AMENDMENT_EVIDENCE_REQUIRED",
            "Amendment request evidence is required.");
        RequireIdempotency(request.IdempotencyKey);
        ProcurementPurchaseOrderAmendmentRules.ValidateItems(request.Items);
        await ValidateEvidenceAsync(
            request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId,
            cancellationToken);

        ProcurementPurchaseOrderAmendment? created = null;
        var inserted = false;
        await ExecuteAsync(async () =>
        {
            await _unitOfWork.AcquireTransactionLockAsync(
                $"procurement:po-amendment:{_currentUser.TenantId:N}:{purchaseOrderId:N}",
                cancellationToken);
            var purchaseOrder = await _amendmentStore.GetPurchaseOrderForUpdateAsync(
                _currentUser.TenantId, purchaseOrderId, cancellationToken)
                ?? throw NotFound("PO_AMENDMENT_PURCHASE_ORDER_NOT_FOUND",
                    "The purchase order was not found in the current tenant.");
            await EnsureCapabilityAsync(
                ManagePermission, purchaseOrder.OrderNumber,
                correlation, cancellationToken);
            var concurrent = await AmendmentQuery()
                .SingleOrDefaultAsync(item =>
                    item.IdempotencyKey == request.IdempotencyKey.Trim(),
                    cancellationToken);
            if (concurrent is not null)
            {
                if (concurrent.PurchaseOrderId != purchaseOrderId)
                    throw Conflict("PO_AMENDMENT_IDEMPOTENCY_CONFLICT",
                        "The idempotency key belongs to another purchase order.");
                created = concurrent;
                return;
            }
            await EnsureAmendableAsync(purchaseOrder, cancellationToken);

            var source = await ResolveProposedSourceAsync(
                purchaseOrder, request, correlation, cancellationToken);
            if (source.PurchaseRequisitionId != purchaseOrder.SourceRequisitionId)
                throw Validation("PO_AMENDMENT_REQUISITION_CHANGE_FORBIDDEN",
                    "A PO amendment must remain bound to the same approved requisition demand.");

            var proposal = BuildProposal(purchaseOrder, request, source);
            var sourceLines = ToSourceLines(proposal.Items);
            await _sources.ValidateOrderAsync(
                source,
                sourceLines,
                proposal.TotalAmount,
                proposal.Currency,
                correlation,
                cancellationToken);

            var before = Capture(purchaseOrder);
            var diffs = BuildDiff(before, proposal);
            if (diffs.Count == 0)
                throw Validation("PO_AMENDMENT_CHANGE_REQUIRED",
                    "The proposal does not change the current purchase order.");
            var beforeJson = Serialize(before);
            var proposalJson = Serialize(proposal);
            var diffJson = Serialize(diffs);
            var sequence = (await Amendments
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.PurchaseOrderId == purchaseOrder.Id &&
                        !item.IsDeleted)
                    .Select(item => (int?)item.AmendmentSequence)
                    .MaxAsync(cancellationToken) ?? 0) + 1;
            var now = DateTime.UtcNow;
            created = new ProcurementPurchaseOrderAmendment
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                PurchaseOrderId = purchaseOrder.Id,
                AmendmentNumber = Truncate(
                    $"{purchaseOrder.OrderNumber}-AMD-{sequence:000}", 100),
                AmendmentSequence = sequence,
                BaseRevisionNumber = purchaseOrder.RevisionNumber,
                ProposedRevisionNumber = purchaseOrder.RevisionNumber + 1,
                Status = ProcurementPurchaseOrderAmendmentStatus.Draft,
                Reason = request.Reason.Trim(),
                ChangeScope = request.ChangeScope.Trim(),
                BeforeSnapshotJson = beforeJson,
                BeforeIntegrityHash = Hash(beforeJson),
                ProposedSnapshotJson = proposalJson,
                ProposedIntegrityHash = Hash(proposalJson),
                DiffJson = diffJson,
                DiffIntegrityHash = Hash(diffJson),
                ProposedSourceType = source.SourceType,
                ProposedSourceId = source.SourceId,
                ProposedSourceReference = source.SourceReference,
                ProposedBusinessPartnerId = source.BusinessPartnerId,
                ProposedSourceRequisitionId = source.PurchaseRequisitionId,
                ProposedSourcingReleaseId = source.SourcingReleaseId,
                ProposedSourcingCaseId = source.SourcingCaseId,
                ProposedAwardReadinessDecisionId =
                    source.AwardReadinessDecisionId,
                ProposedSourceIntegrityHash = source.SourceIntegrityHash,
                BeforeTotalAmount = before.TotalAmount,
                ProposedTotalAmount = proposal.TotalAmount,
                CommitmentDelta = ProcurementPurchaseOrderAmendmentRules.Round(
                    proposal.TotalAmount - before.TotalAmount),
                Currency = proposal.Currency,
                PurchaseOrderStatusBefore = purchaseOrder.Status,
                RequestEvidenceReference = request.EvidenceReference.Trim(),
                RequestEvidenceWorkflowDocumentId =
                    request.EvidenceWorkflowDocumentId,
                RequestEvidenceFileUploadRecordId =
                    request.EvidenceFileUploadRecordId,
                IdempotencyKey = request.IdempotencyKey.Trim(),
                CorrelationId = correlation,
                CreatedAt = now,
                CreatedBy = ActorName(),
                CreatedById = _currentUser.UserId
            };
            inserted = true;
            await Amendments.AddAsync(created);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                created, "AmendmentCreated",
                ProcurementControlEventResult.Allowed,
                before, proposal, request.Reason, correlation, now,
                Evidence(
                    request.EvidenceReference,
                    request.EvidenceWorkflowDocumentId,
                    request.EvidenceFileUploadRecordId,
                    "PO amendment request",
                    "PO-005"),
                cancellationToken);
        }, cancellationToken);

        if (inserted)
            await PublishAsync(
                "procurement.purchase-order.amendment-created",
                created!,
                cancellationToken);
        return await GetAmendmentDtoAsync(created!.Id, cancellationToken);
    }

    public async Task<ProcurementPurchaseOrderAmendmentDto> SubmitAsync(
        Guid amendmentId,
        ProcurementPurchaseOrderAmendmentLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.Comment, "PO_AMENDMENT_SUBMISSION_COMMENT_REQUIRED",
            "A submission comment is required.");
        Require(request.EvidenceReference, "PO_AMENDMENT_SUBMISSION_EVIDENCE_REQUIRED",
            "Submission evidence is required.");
        await ValidateEvidenceAsync(
            request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId,
            cancellationToken);

        ProcurementPurchaseOrderAmendment? submitted = null;
        PurchaseOrder? automaticApprovalPurchaseOrder = null;
        try
        {
            await ExecuteAsync(async () =>
            {
                var amendment = await LoadAmendmentForUpdateAsync(
                    amendmentId, cancellationToken);
                var purchaseOrder = await _amendmentStore.GetPurchaseOrderForUpdateAsync(
                    _currentUser.TenantId,
                    amendment.PurchaseOrderId,
                    cancellationToken)
                    ?? throw NotFound("PO_AMENDMENT_PURCHASE_ORDER_NOT_FOUND",
                        "The purchase order was not found in the current tenant.");
                await EnsureCapabilityAsync(
                    ManagePermission, purchaseOrder.OrderNumber,
                    correlation, cancellationToken);
                EnsureStatus(
                    amendment,
                    ProcurementPurchaseOrderAmendmentStatus.Draft,
                    "Only a Draft amendment can be submitted.");
                EnsureRowVersion(amendment.RowVersion, request.RowVersion);
                await EnsureBaseStillCurrentAsync(
                    amendment, purchaseOrder, cancellationToken);
                await RevalidateProposalAsync(
                    amendment, purchaseOrder, correlation, cancellationToken);

                var result = await _workflow.SubmitAsync(
                    WorkflowEntityType, purchaseOrder.Id);
                if (!result.ExecutionResult.Success)
                    throw Conflict("PO_AMENDMENT_WORKFLOW_SUBMIT_FAILED",
                        result.ExecutionResult.Message ??
                        "The shared purchase-order workflow could not be started.");
                if (ProcurementPurchaseOrderAmendmentRules.IsAutomaticApproval(
                        result.Outcome))
                {
                    automaticApprovalPurchaseOrder = purchaseOrder;
                    throw new AutomaticApprovalDetectedException();
                }
                if (result.Outcome != WorkflowOutcome.Pending)
                    throw Conflict("PO_AMENDMENT_WORKFLOW_SUBMIT_OUTCOME_INVALID",
                        $"The shared workflow returned {result.Outcome}; a submitted amendment must remain pending for an independent decision.");
                if (!result.ExecutionResult.WorkflowInstanceId.HasValue)
                    throw Conflict("PO_AMENDMENT_WORKFLOW_INSTANCE_REQUIRED",
                        "The shared workflow did not return an exact workflow-instance binding.");
                var workflowInstance = await _unitOfWork
                    .Repository<WorkflowInstance>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == result.ExecutionResult.WorkflowInstanceId.Value &&
                        item.EntityId == purchaseOrder.Id &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw Conflict("PO_AMENDMENT_WORKFLOW_INSTANCE_INVALID",
                        "The shared workflow instance does not belong to this purchase order in the current tenant.");

                var now = DateTime.UtcNow;
                amendment.Status =
                    ProcurementPurchaseOrderAmendmentStatus.PendingApproval;
                amendment.WorkflowInstanceId =
                    result.ExecutionResult.WorkflowInstanceId;
                amendment.WorkflowDefinitionId =
                    workflowInstance.WorkflowDefinitionId;
                amendment.SubmittedAtUtc = now;
                amendment.SubmittedById = _currentUser.UserId;
                amendment.SubmittedByName = ActorName();
                amendment.DecisionComment = request.Comment.Trim();
                amendment.CorrelationId = correlation;
                amendment.UpdatedAt = now;
                amendment.UpdatedBy = ActorName();
                amendment.LastModifiedById = _currentUser.UserId;
                purchaseOrder.Status = PendingStatus;
                purchaseOrder.UpdatedAt = now;
                purchaseOrder.UpdatedBy = ActorName();
                purchaseOrder.LastModifiedById = _currentUser.UserId;
                await Amendments.UpdateAsync(amendment);
                await PurchaseOrders.UpdateAsync(purchaseOrder);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(
                    amendment, "AmendmentSubmitted",
                    ProcurementControlEventResult.ReviewRequired,
                    null,
                    new
                    {
                        amendment.Status,
                        amendment.WorkflowInstanceId,
                        PurchaseOrderStatus = purchaseOrder.Status
                    },
                    request.Comment, correlation, now,
                    Evidence(
                        request.EvidenceReference,
                        request.EvidenceWorkflowDocumentId,
                        request.EvidenceFileUploadRecordId,
                        "PO amendment submission",
                        "PO-005"),
                    cancellationToken);
                submitted = amendment;
            }, cancellationToken);
        }
        catch (AutomaticApprovalDetectedException)
        {
            await _sod.RejectApprovalBypassAsync(
                automaticApprovalPurchaseOrder!,
                "AmendmentWorkflowAutoApprove",
                correlation,
                cancellationToken);
            throw;
        }

        await PublishAsync(
            "procurement.purchase-order.amendment-submitted",
            submitted!,
            cancellationToken);
        return await GetAmendmentDtoAsync(submitted!.Id, cancellationToken);
    }

    public async Task<ProcurementPurchaseOrderAmendmentDto> DecideAsync(
        Guid amendmentId,
        DecideProcurementPurchaseOrderAmendmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.Comment, "PO_AMENDMENT_DECISION_COMMENT_REQUIRED",
            "A decision comment is required.");
        Require(request.EvidenceReference, "PO_AMENDMENT_DECISION_EVIDENCE_REQUIRED",
            "Decision evidence is required.");
        await ValidateEvidenceAsync(
            request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId,
            cancellationToken);

        ProcurementPurchaseOrderAmendment? decided = null;
        await ExecuteAsync(async () =>
        {
            var amendment = await LoadAmendmentForUpdateAsync(
                amendmentId, cancellationToken);
            var purchaseOrder = await _amendmentStore.GetPurchaseOrderForUpdateAsync(
                _currentUser.TenantId,
                amendment.PurchaseOrderId,
                cancellationToken)
                ?? throw NotFound("PO_AMENDMENT_PURCHASE_ORDER_NOT_FOUND",
                    "The purchase order was not found in the current tenant.");
            await EnsureCapabilityAsync(
                ApprovePermission, purchaseOrder.OrderNumber,
                correlation, cancellationToken);
            EnsureStatus(
                amendment,
                ProcurementPurchaseOrderAmendmentStatus.PendingApproval,
                "Only a Pending Approval amendment can be decided.");
            EnsureRowVersion(amendment.RowVersion, request.RowVersion);
            if (!amendment.WorkflowInstanceId.HasValue)
                throw Conflict("PO_AMENDMENT_WORKFLOW_BINDING_REQUIRED",
                    "The amendment has no exact shared workflow binding.");
            if (!await _workflow.CanUserApproveAsync(
                    WorkflowEntityType, purchaseOrder.Id, _currentUser.UserId))
                throw Authorization(
                    "The current actor is not assigned to the active purchase-order workflow step.");
            if (request.Approved)
                await _sod.EnforceApprovalAsync(
                    purchaseOrder, correlation, cancellationToken);

            var result = await _workflow.ProcessApprovalAsync(
                WorkflowEntityType,
                purchaseOrder.Id,
                _currentUser.UserId,
                request.Approved ? "approve" : "reject",
                request.Comment);
            if (!result.ExecutionResult.Success)
                throw Conflict("PO_AMENDMENT_WORKFLOW_DECISION_FAILED",
                    result.ExecutionResult.Message ??
                    "The shared purchase-order workflow decision failed.");
            if (result.ExecutionResult.WorkflowInstanceId !=
                amendment.WorkflowInstanceId)
                throw Conflict("PO_AMENDMENT_WORKFLOW_INSTANCE_CHANGED",
                    "The workflow decision did not target the amendment's exact retained workflow instance.");
            if (request.Approved && result.Outcome == WorkflowOutcome.Rejected)
                throw Conflict("PO_AMENDMENT_WORKFLOW_APPROVAL_CONTRADICTED",
                    "The shared workflow did not approve this amendment.");
            if (!request.Approved && result.Outcome == WorkflowOutcome.Approved)
                throw Conflict("PO_AMENDMENT_WORKFLOW_REJECTION_CONTRADICTED",
                    "A Completed shared workflow cannot be recorded as a rejected amendment.");
            if (result.Outcome == WorkflowOutcome.Recalled)
                throw Conflict("PO_AMENDMENT_WORKFLOW_OUTCOME_INVALID",
                    "A recalled workflow cannot be recorded as an amendment approval or rejection decision.");

            var now = DateTime.UtcNow;
            amendment.DecisionComment = request.Comment.Trim();
            amendment.CorrelationId = correlation;
            amendment.UpdatedAt = now;
            amendment.UpdatedBy = ActorName();
            amendment.LastModifiedById = _currentUser.UserId;
            if (result.Outcome == WorkflowOutcome.Pending)
            {
                await Amendments.UpdateAsync(amendment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(
                    amendment, "AmendmentApprovalProgressed",
                    ProcurementControlEventResult.ReviewRequired,
                    null,
                    new { amendment.Status, WorkflowOutcome = result.Outcome },
                    request.Comment, correlation, now,
                    Evidence(
                        request.EvidenceReference,
                        request.EvidenceWorkflowDocumentId,
                        request.EvidenceFileUploadRecordId,
                        "PO amendment approval progression",
                        "PO-005"),
                    cancellationToken);
                decided = amendment;
                return;
            }

            amendment.DecidedAtUtc = now;
            amendment.DecidedById = _currentUser.UserId;
            amendment.DecidedByName = ActorName();
            amendment.ApprovalEvidenceReference =
                request.EvidenceReference.Trim();
            amendment.ApprovalEvidenceWorkflowDocumentId =
                request.EvidenceWorkflowDocumentId;
            amendment.ApprovalEvidenceFileUploadRecordId =
                request.EvidenceFileUploadRecordId;
            if (result.Outcome == WorkflowOutcome.Rejected)
            {
                amendment.Status =
                    ProcurementPurchaseOrderAmendmentStatus.Rejected;
                purchaseOrder.Status = amendment.PurchaseOrderStatusBefore;
                purchaseOrder.UpdatedAt = now;
                purchaseOrder.UpdatedBy = ActorName();
                purchaseOrder.LastModifiedById = _currentUser.UserId;
                await Amendments.UpdateAsync(amendment);
                await PurchaseOrders.UpdateAsync(purchaseOrder);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(
                    amendment, "AmendmentRejected",
                    ProcurementControlEventResult.Rejected,
                    null,
                    new
                    {
                        amendment.Status,
                        PurchaseOrderStatus = purchaseOrder.Status
                    },
                    request.Comment, correlation, now,
                    Evidence(
                        request.EvidenceReference,
                        request.EvidenceWorkflowDocumentId,
                        request.EvidenceFileUploadRecordId,
                        "PO amendment rejection",
                        "PO-005"),
                    cancellationToken);
                decided = amendment;
                return;
            }

            await ApplyApprovedAmendmentAsync(
                amendment,
                purchaseOrder,
                request,
                correlation,
                now,
                cancellationToken);
            decided = amendment;
        }, cancellationToken);

        await PublishAsync(
            decided!.Status ==
            ProcurementPurchaseOrderAmendmentStatus.Rejected
                ? "procurement.purchase-order.amendment-rejected"
                : decided.Status ==
                  ProcurementPurchaseOrderAmendmentStatus.Applied
                    ? "procurement.purchase-order.amendment-applied"
                    : "procurement.purchase-order.amendment-approval-progressed",
            decided,
            cancellationToken);
        return await GetAmendmentDtoAsync(decided.Id, cancellationToken);
    }

    public async Task<ProcurementPurchaseOrderAmendmentDispatchDto> DispatchAsync(
        Guid amendmentId,
        DispatchProcurementPurchaseOrderAmendmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.Destination, "PO_AMENDMENT_DISPATCH_DESTINATION_REQUIRED",
            "A dispatch destination is required.");
        Require(request.DispatchReference, "PO_AMENDMENT_DISPATCH_REFERENCE_REQUIRED",
            "A dispatch reference is required.");
        Require(request.DocumentReference, "PO_AMENDMENT_DOCUMENT_REQUIRED",
            "The exact approved amendment document reference is required.");
        Require(request.OrganizationSignatureEvidenceReference,
            "PO_AMENDMENT_SIGNATURE_EVIDENCE_REQUIRED",
            "Approved organization signature evidence is required.");
        Require(request.DispatchEvidenceReference,
            "PO_AMENDMENT_DISPATCH_EVIDENCE_REQUIRED",
            "Dispatch evidence is required.");
        RequireIdempotency(request.IdempotencyKey);
        await ValidateEvidenceAsync(
            request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId,
            cancellationToken);

        ProcurementPurchaseOrderAmendmentDispatch? created = null;
        var inserted = false;
        await ExecuteAsync(async () =>
        {
            var amendment = await LoadAmendmentForUpdateAsync(
                amendmentId, cancellationToken);
            await EnsureCapabilityAsync(
                ManagePermission, amendment.PurchaseOrder.OrderNumber,
                correlation, cancellationToken);
            var concurrent = await DispatchQuery()
                .SingleOrDefaultAsync(item =>
                    item.IdempotencyKey == request.IdempotencyKey.Trim(),
                    cancellationToken);
            if (concurrent is not null)
            {
                if (concurrent.AmendmentId != amendmentId)
                    throw Conflict("PO_AMENDMENT_DISPATCH_IDEMPOTENCY_CONFLICT",
                        "The idempotency key belongs to another amendment.");
                created = concurrent;
                return;
            }
            if (amendment.Status is not (
                    ProcurementPurchaseOrderAmendmentStatus.Applied or
                    ProcurementPurchaseOrderAmendmentStatus.Dispatched or
                    ProcurementPurchaseOrderAmendmentStatus.Acknowledged))
                throw Conflict("PO_AMENDMENT_DISPATCH_NOT_ALLOWED",
                    "Only an applied amendment revision can be dispatched.");
            if (amendment.PurchaseOrder.RevisionNumber !=
                amendment.ProposedRevisionNumber)
                throw Conflict("PO_AMENDMENT_REVISION_SUPERSEDED",
                    "The approved revision is no longer the current purchase-order version.");
            ValidateDestination(
                request.Channel,
                request.Destination,
                amendment.ProposedBusinessPartnerId);

            var sequence = (await Dispatches.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.AmendmentId == amendment.Id &&
                        !item.IsDeleted)
                    .Select(item => (int?)item.Sequence)
                    .MaxAsync(cancellationToken) ?? 0) + 1;
            var previous = await Dispatches.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.AmendmentId == amendment.Id &&
                    !item.IsDeleted)
                .AsNoTracking()
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            var now = DateTime.UtcNow;
            created = new ProcurementPurchaseOrderAmendmentDispatch
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                AmendmentId = amendment.Id,
                PurchaseOrderId = amendment.PurchaseOrderId,
                RevisionNumber = amendment.ProposedRevisionNumber,
                Sequence = sequence,
                Channel = request.Channel,
                Destination = request.Destination.Trim(),
                DispatchReference = request.DispatchReference.Trim(),
                DocumentReference = request.DocumentReference.Trim(),
                OrganizationSignatureEvidenceReference =
                    request.OrganizationSignatureEvidenceReference.Trim(),
                DispatchEvidenceReference =
                    request.DispatchEvidenceReference.Trim(),
                EvidenceWorkflowDocumentId =
                    request.EvidenceWorkflowDocumentId,
                EvidenceFileUploadRecordId =
                    request.EvidenceFileUploadRecordId,
                DispatchedAtUtc = now,
                DispatchedById = _currentUser.UserId,
                DispatchedByName = ActorName(),
                IdempotencyKey = request.IdempotencyKey.Trim(),
                CorrelationId = correlation,
                CreatedAt = now,
                CreatedBy = ActorName(),
                CreatedById = _currentUser.UserId
            };
            inserted = true;
            created.IntegrityHash = Hash(Serialize(new
            {
                schemaVersion = "tdc.po-amendment-dispatch.v1",
                created.Id,
                created.TenantId,
                created.AmendmentId,
                created.PurchaseOrderId,
                created.RevisionNumber,
                created.Sequence,
                created.Channel,
                created.Destination,
                created.DispatchReference,
                created.DocumentReference,
                created.OrganizationSignatureEvidenceReference,
                created.DispatchEvidenceReference,
                created.DispatchedAtUtc,
                created.DispatchedById,
                created.IdempotencyKey,
                previousHash = previous?.IntegrityHash,
                amendment.ProposedIntegrityHash
            }));
            await Dispatches.AddAsync(created);
            amendment.Status =
                ProcurementPurchaseOrderAmendmentStatus.Dispatched;
            amendment.PurchaseOrder.Status = "Sent";
            amendment.UpdatedAt = now;
            amendment.UpdatedBy = ActorName();
            amendment.LastModifiedById = _currentUser.UserId;
            amendment.PurchaseOrder.UpdatedAt = now;
            amendment.PurchaseOrder.UpdatedBy = ActorName();
            amendment.PurchaseOrder.LastModifiedById =
                _currentUser.UserId;
            await Amendments.UpdateAsync(amendment);
            await PurchaseOrders.UpdateAsync(amendment.PurchaseOrder);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                amendment, "AmendmentDispatched",
                ProcurementControlEventResult.Allowed,
                null,
                new
                {
                    dispatchId = created.Id,
                    created.Sequence,
                    created.Channel,
                    created.Destination,
                    created.DispatchReference,
                    created.DocumentReference,
                    created.OrganizationSignatureEvidenceReference,
                    created.IntegrityHash
                },
                request.DispatchReference, correlation, now,
                Evidence(
                    request.DispatchEvidenceReference,
                    request.EvidenceWorkflowDocumentId,
                    request.EvidenceFileUploadRecordId,
                    "Signed PO amendment dispatch",
                    "PO-006"),
                cancellationToken);
        }, cancellationToken);

        if (inserted)
            await PublishAsync(
                "procurement.purchase-order.amendment-dispatched",
                await LoadAmendmentAsync(amendmentId, cancellationToken),
                cancellationToken);
        return await GetDispatchDtoAsync(created!.Id, cancellationToken);
    }

    public async Task<ProcurementPurchaseOrderAmendmentAcknowledgementDto>
        AcknowledgeAsync(
            Guid dispatchId,
            AcknowledgeProcurementPurchaseOrderAmendmentRequest request,
            string correlationId,
            bool external,
            CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        Require(request.AcknowledgementChannel,
            "PO_AMENDMENT_ACKNOWLEDGEMENT_CHANNEL_REQUIRED",
            "An acknowledgement channel is required.");
        Require(request.AcknowledgementReference,
            "PO_AMENDMENT_ACKNOWLEDGEMENT_REFERENCE_REQUIRED",
            "An acknowledgement reference is required.");
        Require(request.EvidenceReference,
            "PO_AMENDMENT_ACKNOWLEDGEMENT_EVIDENCE_REQUIRED",
            "Acknowledgement evidence is required.");
        RequireIdempotency(request.IdempotencyKey);
        await ValidateEvidenceAsync(
            request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId,
            cancellationToken);

        ProcurementPurchaseOrderAmendmentAcknowledgement? created = null;
        var inserted = false;
        await ExecuteAsync(async () =>
        {
            var dispatch = await LoadDispatchAsync(
                dispatchId, cancellationToken);
            var amendment = dispatch.Amendment;
            Guid? externalPartnerId = null;
            if (external)
            {
                EnsureExternalReader();
                var partnerIds =
                    await GetExternalPartnerIdsAsync(cancellationToken);
                if (!partnerIds.Contains(
                        amendment.ProposedBusinessPartnerId))
                    throw Authorization(
                        "The dispatch does not belong to the current supplier account.");
                externalPartnerId =
                    amendment.ProposedBusinessPartnerId;
            }
            else
            {
                await EnsureCapabilityAsync(
                    ManagePermission,
                    amendment.PurchaseOrder.OrderNumber,
                    correlation,
                    cancellationToken);
            }
            var concurrent = await AcknowledgementQuery()
                .SingleOrDefaultAsync(item =>
                    item.IdempotencyKey == request.IdempotencyKey.Trim(),
                    cancellationToken);
            if (concurrent is not null)
            {
                if (concurrent.DispatchId != dispatchId)
                    throw Conflict("PO_AMENDMENT_ACKNOWLEDGEMENT_IDEMPOTENCY_CONFLICT",
                        "The idempotency key belongs to another dispatch.");
                created = concurrent;
                return;
            }
            if (dispatch.RevisionNumber !=
                amendment.ProposedRevisionNumber)
                throw Conflict("PO_AMENDMENT_ACKNOWLEDGEMENT_REVISION_MISMATCH",
                    "The acknowledgement does not target the exact dispatched revision.");
            var sequence = (await Acknowledgements.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.DispatchId == dispatch.Id &&
                        !item.IsDeleted)
                    .Select(item => (int?)item.Sequence)
                    .MaxAsync(cancellationToken) ?? 0) + 1;
            var previous = await Acknowledgements.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.DispatchId == dispatch.Id &&
                    !item.IsDeleted)
                .AsNoTracking()
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            var now = DateTime.UtcNow;
            created =
                new ProcurementPurchaseOrderAmendmentAcknowledgement
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUser.TenantId,
                    DispatchId = dispatch.Id,
                    Sequence = sequence,
                    Outcome = request.Outcome,
                    AcknowledgedAtUtc = now,
                    AcknowledgedByUserId = _currentUser.UserId,
                    AcknowledgedByBusinessPartnerId =
                        externalPartnerId,
                    AcknowledgementChannel =
                        request.AcknowledgementChannel.Trim(),
                    AcknowledgementReference =
                        request.AcknowledgementReference.Trim(),
                    EvidenceReference =
                        request.EvidenceReference.Trim(),
                    EvidenceWorkflowDocumentId =
                        request.EvidenceWorkflowDocumentId,
                    EvidenceFileUploadRecordId =
                        request.EvidenceFileUploadRecordId,
                    Comments = Trim(request.Comments, 1000),
                    IdempotencyKey = request.IdempotencyKey.Trim(),
                    CorrelationId = correlation,
                    CreatedAt = now,
                    CreatedBy = ActorName(),
                    CreatedById = _currentUser.UserId
                };
            inserted = true;
            created.IntegrityHash = Hash(Serialize(new
            {
                schemaVersion =
                    "tdc.po-amendment-acknowledgement.v1",
                created.Id,
                created.TenantId,
                created.DispatchId,
                created.Sequence,
                created.Outcome,
                created.AcknowledgedAtUtc,
                created.AcknowledgedByUserId,
                created.AcknowledgedByBusinessPartnerId,
                created.AcknowledgementChannel,
                created.AcknowledgementReference,
                created.EvidenceReference,
                created.Comments,
                created.IdempotencyKey,
                previousHash = previous?.IntegrityHash,
                dispatch.IntegrityHash
            }));
            await Acknowledgements.AddAsync(created);
            amendment.Status =
                ProcurementPurchaseOrderAmendmentStatus.Acknowledged;
            amendment.PurchaseOrder.Status = "Acknowledged";
            amendment.UpdatedAt = now;
            amendment.UpdatedBy = ActorName();
            amendment.LastModifiedById = _currentUser.UserId;
            amendment.PurchaseOrder.UpdatedAt = now;
            amendment.PurchaseOrder.UpdatedBy = ActorName();
            amendment.PurchaseOrder.LastModifiedById =
                _currentUser.UserId;
            await Amendments.UpdateAsync(amendment);
            await PurchaseOrders.UpdateAsync(amendment.PurchaseOrder);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                amendment, "AmendmentAcknowledged",
                ProcurementControlEventResult.Allowed,
                null,
                new
                {
                    dispatchId = dispatch.Id,
                    acknowledgementId = created.Id,
                    created.Sequence,
                    created.Outcome,
                    created.AcknowledgedByBusinessPartnerId,
                    created.IntegrityHash
                },
                request.AcknowledgementReference, correlation, now,
                Evidence(
                    request.EvidenceReference,
                    request.EvidenceWorkflowDocumentId,
                    request.EvidenceFileUploadRecordId,
                    "Supplier PO amendment acknowledgement",
                    "PO-006"),
                cancellationToken);
        }, cancellationToken);

        if (inserted)
            await PublishAsync(
                "procurement.purchase-order.amendment-acknowledged",
                (await LoadDispatchAsync(
                    dispatchId, cancellationToken)).Amendment,
                cancellationToken);
        return MapAcknowledgement(created!);
    }

    private async Task ApplyApprovedAmendmentAsync(
        ProcurementPurchaseOrderAmendment amendment,
        PurchaseOrder purchaseOrder,
        DecideProcurementPurchaseOrderAmendmentRequest request,
        string correlationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await EnsureBaseStillCurrentAsync(
            amendment, purchaseOrder, cancellationToken);
        var proposal = Deserialize<PurchaseOrderSnapshot>(
                           amendment.ProposedSnapshotJson)
                       ?? throw Conflict("PO_AMENDMENT_SNAPSHOT_INVALID",
                           "The retained proposed PO snapshot is invalid.");
        var sourceChanged =
            purchaseOrder.ProcurementSourceType !=
            amendment.ProposedSourceType ||
            purchaseOrder.ProcurementSourceId !=
            amendment.ProposedSourceId ||
            purchaseOrder.BusinessPartnerId !=
            amendment.ProposedBusinessPartnerId;
        var exposureLineageChanged = sourceChanged ||
            purchaseOrder.SourceRequisitionId !=
            amendment.ProposedSourceRequisitionId ||
            purchaseOrder.SourcingReleaseId !=
            amendment.ProposedSourcingReleaseId;
        if (exposureLineageChanged)
            throw Conflict(
                "PO_AMENDMENT_EXPOSURE_REALLOCATION_REQUIRED",
                "An approved governed purchase order cannot change supplier, source, requisition, or contract exposure lineage until an atomic formal-ledger reallocation is available.");
        ProcurementPurchaseOrderSourceResolution source;
        if (sourceChanged)
        {
            await _unitOfWork.AcquireTransactionLockAsync(
                $"procurement:po-source:{_currentUser.TenantId:N}:{(int)amendment.ProposedSourceType}:{amendment.ProposedSourceId:N}",
                cancellationToken);
            source = await _sources.ResolveAsync(
                amendment.ProposedSourceType,
                amendment.ProposedSourceId,
                amendment.ProposedBusinessPartnerId,
                correlationId,
                cancellationToken);
        }
        else
        {
            source = await _sources.RevalidateAsync(
                purchaseOrder,
                "Amend",
                correlationId,
                cancellationToken);
        }
        EnsureProposalSource(amendment, source);
        var sourceLines = ToSourceLines(proposal.Items);
        await _sources.ValidateOrderAsync(
            source,
            sourceLines,
            proposal.TotalAmount,
            proposal.Currency,
            correlationId,
            cancellationToken);
        if (sourceChanged)
        {
            await _sources.ReserveAsync(
                source,
                sourceLines,
                proposal.TotalAmount,
                proposal.Currency,
                purchaseOrder.Id,
                correlationId,
                cancellationToken);
        }

        amendment.Status =
            ProcurementPurchaseOrderAmendmentStatus.Applied;
        amendment.AppliedAtUtc = now;
        amendment.AppliedById = _currentUser.UserId;
        amendment.AppliedByName = ActorName();
        amendment.UpdatedAt = now;
        amendment.UpdatedBy = ActorName();
        amendment.LastModifiedById = _currentUser.UserId;
        await Amendments.UpdateAsync(amendment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ProcurementPurchaseOrderCommitmentAdjustment adjustment;
        var contextSet = false;
        try
        {
            // Bind the aggregate mutation, immutable adjustment, and PO update
            // to this exact approved amendment for the full database sequence.
            await _amendmentStore.SetApprovedSourceMutationContextAsync(
                amendment.Id, cancellationToken);
            contextSet = true;

            adjustment = await AdjustCommitmentAsync(
                amendment,
                purchaseOrder,
                now,
                correlationId,
                cancellationToken);

            if (sourceChanged)
            {
                _sources.Apply(purchaseOrder, source);
            }
            ApplyProposal(purchaseOrder, proposal, now);
            await ApplyItemsAsync(
                purchaseOrder, proposal.Items, now, cancellationToken);
            await PurchaseOrders.UpdateAsync(purchaseOrder);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            if (contextSet)
            {
                await _amendmentStore.ClearApprovedSourceMutationContextAsync(
                    cancellationToken);
            }
        }

        await _compliance.EnforceAsync(
            purchaseOrder,
            "Approve",
            correlationId,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(
            amendment, "AmendmentApplied",
            ProcurementControlEventResult.Succeeded,
            Deserialize<object>(amendment.BeforeSnapshotJson),
            new
            {
                PurchaseOrder = proposal,
                CommitmentAdjustment = new
                {
                    adjustment.Id,
                    adjustment.CommitmentAmountBefore,
                    adjustment.CommitmentAmountAfter,
                    adjustment.DeltaAmount,
                    adjustment.BudgetCommittedBefore,
                    adjustment.BudgetCommittedAfter,
                    adjustment.IntegrityHash
                }
            },
            request.Comment, correlationId, now,
            Evidence(
                request.EvidenceReference,
                request.EvidenceWorkflowDocumentId,
                request.EvidenceFileUploadRecordId,
                "Approved PO amendment",
                "PO-005"),
            cancellationToken);
    }

    private async Task<ProcurementPurchaseOrderCommitmentAdjustment>
        AdjustCommitmentAsync(
            ProcurementPurchaseOrderAmendment amendment,
            PurchaseOrder purchaseOrder,
            DateTime now,
            string correlationId,
            CancellationToken cancellationToken)
    {
        if (!purchaseOrder.SourceRequisitionId.HasValue)
            throw Conflict("PO_AMENDMENT_BUDGET_REQUISITION_REQUIRED",
                "The amended purchase order has no approved requisition budget lineage.");
        if (!_budgetStore.HasRequiredTransaction)
            throw Conflict("PO_AMENDMENT_BUDGET_TRANSACTION_REQUIRED",
                "Commitment adjustment requires the caller-owned serializable transaction.");

        var commitment = await Commitments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId ==
                purchaseOrder.SourceRequisitionId.Value &&
                !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("PO_AMENDMENT_BUDGET_COMMITMENT_REQUIRED",
                "The approved requisition has no budget commitment.");
        var budget = await _budgetStore.GetBudgetForUpdateAsync(
                         _currentUser.TenantId,
                         commitment.ProcurementBudgetId,
                         cancellationToken)
                     ?? throw Conflict("PO_AMENDMENT_BUDGET_NOT_FOUND",
                         "The committed budget was not found in the current tenant.");
        if (!string.Equals(
                budget.Currency,
                purchaseOrder.Currency,
                StringComparison.OrdinalIgnoreCase))
            throw Conflict("PO_AMENDMENT_BUDGET_CURRENCY_MISMATCH",
                "The amended purchase order currency does not match the committed budget.");

        var ledger = _unitOfWork.Repository<ProcurementBudgetCommitmentLedgerEntry>();
        var delta = ProcurementPurchaseOrderAmendmentRules.Round(
            amendment.ProposedTotalAmount - amendment.BeforeTotalAmount);
        var isContractChild = purchaseOrder.ContractId.HasValue;
        if (isContractChild && delta != 0m)
            throw Conflict(
                "PO_AMENDMENT_CONTRACT_ALLOCATION_LEDGER_REQUIRED",
                "A contract-child purchase-order value cannot change until a dedicated atomic allocation-adjustment ledger is available. Non-commercial amendments remain permitted.");
        decimal exposureBefore;
        decimal exposureAfter;
        var commitmentBefore = commitment.ReservedAmount;
        var commitmentAfter = commitmentBefore;
        var budgetCommittedBefore = budget.CommittedAmount;
        var budgetAvailableBefore = Available(budget);
        if (isContractChild)
        {
            var allocation = await ledger.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseRequisitionId == purchaseOrder.SourceRequisitionId.Value &&
                    item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation &&
                    item.SourceType == "PurchaseOrder" &&
                    item.SourceId == purchaseOrder.Id &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw Conflict(
                    "PO_AMENDMENT_CONTRACT_ALLOCATION_REQUIRED",
                    "The contract-child purchase order has no immutable allocation under its parent formal commitment.");
            var parent = allocation.FormalCommitmentEntryId.HasValue
                ? await ledger.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == allocation.FormalCommitmentEntryId.Value &&
                        item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment &&
                        item.SourceType == "Contract" &&
                        item.SourceId == purchaseOrder.ContractId.Value &&
                        !item.IsDeleted)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;
            if (parent is null)
                throw Conflict(
                    "PO_AMENDMENT_CONTRACT_COMMITMENT_REQUIRED",
                    "The contract-child allocation has no exact parent formal contract commitment.");
            var contractIsActive = await _unitOfWork.Repository<Contract>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == purchaseOrder.ContractId.Value &&
                    item.Status == "Active" &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!contractIsActive)
                throw Conflict(
                    "PO_AMENDMENT_CONTRACT_INACTIVE",
                    "The contract-child allocation cannot change outside an active parent contract.");

            var siblingAllocationIds = ledger.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation &&
                    item.FormalCommitmentEntryId == parent.Id &&
                    !item.IsDeleted)
                .Select(item => item.SourceId);
            var baseAllocated = await ledger.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation &&
                    item.FormalCommitmentEntryId == parent.Id &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
            var priorAllocationAdjustments = await Adjustments.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.BudgetCommitmentId == parent.ProcurementBudgetCommitmentId &&
                    siblingAllocationIds.Contains(item.PurchaseOrderId) &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.DeltaAmount, cancellationToken) ?? 0m;
            var thisPurchaseOrderAdjustments = await Adjustments.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == purchaseOrder.Id &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.DeltaAmount, cancellationToken) ?? 0m;
            var effectiveAllocationBefore = ProcurementPurchaseOrderAmendmentRules.Round(
                allocation.Amount + thisPurchaseOrderAdjustments);
            if (effectiveAllocationBefore != amendment.BeforeTotalAmount)
                throw Conflict(
                    "PO_AMENDMENT_CONTRACT_ALLOCATION_STALE",
                    "The retained amendment amount no longer matches the effective child allocation.");
            exposureBefore = ProcurementPurchaseOrderAmendmentRules.Round(
                baseAllocated + priorAllocationAdjustments);
            exposureAfter = ProcurementPurchaseOrderAmendmentRules.Round(exposureBefore + delta);
            if (effectiveAllocationBefore + delta < 0m || exposureAfter > parent.Amount)
                throw Conflict(
                    "PO_AMENDMENT_CONTRACT_ALLOCATION_EXCEEDED",
                    "The amended cumulative child allocations exceed the fixed parent contract commitment.");
        }
        else
        {
            if (commitment.Status != ProcurementBudgetCommitmentStatus.Reserved)
                throw Conflict("PO_AMENDMENT_BUDGET_COMMITMENT_INACTIVE",
                    "The requisition budget commitment is not active.");
            if (!string.Equals(
                    budget.Status, "Approved",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    budget.Status, "Active",
                    StringComparison.OrdinalIgnoreCase))
                throw Conflict("PO_AMENDMENT_BUDGET_NOT_APPROVED",
                    "The committed budget is no longer approved and active.");

            var formalExposure = await ledger.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseRequisitionId == purchaseOrder.SourceRequisitionId.Value &&
                    item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
            var releasedFormalExposure = await ledger.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseRequisitionId == purchaseOrder.SourceRequisitionId.Value &&
                    item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                    item.FormalCommitmentEntryId != null &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
            var directPurchaseOrderIds = ledger.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseRequisitionId == purchaseOrder.SourceRequisitionId.Value &&
                    item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment &&
                    item.SourceType == "PurchaseOrder" &&
                    !item.IsDeleted)
                .Select(item => item.SourceId);
            var priorDirectAdjustments = await Adjustments.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseRequisitionId == purchaseOrder.SourceRequisitionId.Value &&
                    directPurchaseOrderIds.Contains(item.PurchaseOrderId) &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.DeltaAmount, cancellationToken) ?? 0m;
            exposureBefore = ProcurementPurchaseOrderAmendmentRules.Round(
                formalExposure - releasedFormalExposure + priorDirectAdjustments);
            exposureAfter = ProcurementPurchaseOrderAmendmentRules.Round(exposureBefore + delta);
            var outstandingReservation = ProcurementPurchaseOrderAmendmentRules.Round(
                Math.Max(0m,
                    commitment.ReservedAmount -
                    commitment.FormallyCommittedAmount));
            commitmentAfter = ProcurementPurchaseOrderAmendmentRules.Round(
                exposureAfter + outstandingReservation);
            if (delta > 0 && budgetAvailableBefore < delta)
                throw Conflict("PO_AMENDMENT_BUDGET_INSUFFICIENT",
                    $"The approved budget is short by {delta - budgetAvailableBefore:N2} {budget.Currency} for this amendment.");

            budget.CommittedAmount = ProcurementPurchaseOrderAmendmentRules.Round(
                Math.Max(0, budget.CommittedAmount + delta));
            budget.RemainingAmount = Available(budget);
            budget.UpdatedAt = now;
            budget.UpdatedBy = ActorName();
            budget.LastModifiedById = _currentUser.UserId;
            commitment.ReservationSequence += 1;
            commitment.ReservedAmount = commitmentAfter;
            commitment.FormallyCommittedAmount = ProcurementPurchaseOrderAmendmentRules.Round(
                Math.Max(0m, commitment.FormallyCommittedAmount + delta));
            commitment.BudgetAllocatedSnapshot = budget.AllocatedAmount;
            commitment.BudgetUtilizedSnapshot = budget.UtilizedAmount;
            commitment.BudgetCommittedBefore = budgetCommittedBefore;
            commitment.BudgetAvailableBefore = budgetAvailableBefore;
            commitment.BudgetCommittedAfter = budget.CommittedAmount;
            commitment.BudgetAvailableAfter = budget.RemainingAmount;
            commitment.ReservedAtUtc = now;
            commitment.ReservedById = _currentUser.UserId;
            commitment.ReservedByName = ActorName();
            commitment.CorrelationId = correlationId;
            commitment.UpdatedAt = now;
            commitment.UpdatedBy = ActorName();
            commitment.LastModifiedById = _currentUser.UserId;
        }

        var sequence = (await Adjustments.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == purchaseOrder.Id &&
                    !item.IsDeleted)
                .Select(item => (int?)item.Sequence)
                .MaxAsync(cancellationToken) ?? 0) + 1;
        var adjustment =
            new ProcurementPurchaseOrderCommitmentAdjustment
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                AmendmentId = amendment.Id,
                PurchaseOrderId = purchaseOrder.Id,
                PurchaseRequisitionId =
                    purchaseOrder.SourceRequisitionId.Value,
                ProcurementBudgetId = budget.Id,
                BudgetCommitmentId = commitment.Id,
                Sequence = sequence,
                PurchaseOrderAmountBefore =
                    amendment.BeforeTotalAmount,
                PurchaseOrderAmountAfter =
                    amendment.ProposedTotalAmount,
                RequisitionExposureBefore = exposureBefore,
                RequisitionExposureAfter = exposureAfter,
                CommitmentAmountBefore = commitmentBefore,
                CommitmentAmountAfter = commitmentAfter,
                DeltaAmount = delta,
                BudgetCommittedBefore = budgetCommittedBefore,
                BudgetCommittedAfter = budget.CommittedAmount,
                BudgetAvailableBefore = budgetAvailableBefore,
                BudgetAvailableAfter = budget.RemainingAmount,
                Currency = budget.Currency,
                AppliedAtUtc = now,
                AppliedById = _currentUser.UserId,
                AppliedByName = ActorName(),
                CorrelationId = correlationId,
                CreatedAt = now,
                CreatedBy = ActorName(),
                CreatedById = _currentUser.UserId
            };
        adjustment.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion =
                "tdc.po-amendment-commitment-adjustment.v1",
            adjustment.Id,
            adjustment.TenantId,
            adjustment.AmendmentId,
            adjustment.PurchaseOrderId,
            adjustment.PurchaseRequisitionId,
            adjustment.ProcurementBudgetId,
            adjustment.BudgetCommitmentId,
            adjustment.Sequence,
            adjustment.PurchaseOrderAmountBefore,
            adjustment.PurchaseOrderAmountAfter,
            adjustment.RequisitionExposureBefore,
            adjustment.RequisitionExposureAfter,
            adjustment.CommitmentAmountBefore,
            adjustment.CommitmentAmountAfter,
            adjustment.DeltaAmount,
            adjustment.BudgetCommittedBefore,
            adjustment.BudgetCommittedAfter,
            adjustment.BudgetAvailableBefore,
            adjustment.BudgetAvailableAfter,
            adjustment.Currency,
            adjustment.AppliedAtUtc,
            adjustment.AppliedById,
            amendment.ProposedIntegrityHash
        }));
        if (!isContractChild)
        {
            await _unitOfWork.Repository<ProcurementBudget>().UpdateAsync(budget);
            await Commitments.UpdateAsync(commitment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        await Adjustments.AddAsync(adjustment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return adjustment;
    }

    private async Task ApplyItemsAsync(
        PurchaseOrder purchaseOrder,
        IReadOnlyCollection<PurchaseOrderSnapshotItem> proposedItems,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await PurchaseOrderItems.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var proposedExistingIds = proposedItems
            .Where(item => item.PurchaseOrderItemId.HasValue)
            .Select(item => item.PurchaseOrderItemId!.Value)
            .ToHashSet();
        if (proposedExistingIds.Any(id =>
                existing.All(item => item.Id != id)))
            throw Conflict("PO_AMENDMENT_ITEM_LINEAGE_INVALID",
                "A proposed item does not belong to the amended purchase order.");

        foreach (var item in existing)
        {
            var proposal = proposedItems.SingleOrDefault(candidate =>
                candidate.PurchaseOrderItemId == item.Id);
            if (proposal is null)
            {
                item.IsDeleted = true;
                item.DeletedAt = now;
                item.DeletedBy = ActorName();
                item.UpdatedAt = now;
                item.UpdatedBy = ActorName();
                item.LastModifiedById = _currentUser.UserId;
                await PurchaseOrderItems.UpdateAsync(item);
                continue;
            }

            ApplyItem(item, proposal, now);
            await PurchaseOrderItems.UpdateAsync(item);
        }

        foreach (var proposal in proposedItems.Where(item =>
                     !item.PurchaseOrderItemId.HasValue))
        {
            var item = new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                PurchaseOrderId = purchaseOrder.Id,
                CreatedAt = now,
                CreatedBy = ActorName(),
                CreatedById = _currentUser.UserId
            };
            ApplyItem(item, proposal, now);
            await PurchaseOrderItems.AddAsync(item);
        }
    }

    private void ApplyItem(
        PurchaseOrderItem item,
        PurchaseOrderSnapshotItem proposal,
        DateTime now)
    {
        item.InventoryItemId = proposal.InventoryItemId;
        item.BusinessPartnerItemCode =
            Trim(proposal.SupplierItemCode, 100);
        item.ItemDescription =
            Trim(proposal.ItemDescription, 200);
        item.OrderedQuantity = proposal.OrderedQuantity;
        item.ReceivedQuantity = 0;
        item.RemainingQuantity = proposal.OrderedQuantity;
        item.UnitOfMeasure = proposal.UnitOfMeasure;
        item.ItemUnitOfMeasureId = proposal.ItemUnitOfMeasureId;
        item.WarehouseId = proposal.WarehouseId;
        item.UnitPrice = proposal.UnitPrice;
        item.LineTotal = proposal.LineTotal;
        item.AllocatedAdditionalCost = 0;
        item.AllocatedCostPerUnit = 0;
        item.LandedUnitCost = proposal.UnitPrice;
        item.PriceListLineId = proposal.PriceListLineId;
        item.ExpectedDeliveryDate =
            NormalizeUtc(proposal.ExpectedDeliveryDate);
        item.Notes = Trim(proposal.Notes, 1000);
        item.UpdatedAt = now;
        item.UpdatedBy = ActorName();
        item.LastModifiedById = _currentUser.UserId;
    }

    private void ApplyProposal(
        PurchaseOrder purchaseOrder,
        PurchaseOrderSnapshot proposal,
        DateTime now)
    {
        purchaseOrder.BusinessPartnerId =
            proposal.BusinessPartnerId;
        purchaseOrder.RequiredDate =
            NormalizeUtc(proposal.RequiredDate);
        purchaseOrder.PromisedDate =
            NormalizeUtc(proposal.PromisedDate);
        purchaseOrder.PaymentTerms =
            Trim(proposal.PaymentTerms, 100);
        purchaseOrder.ShippingTerms =
            Trim(proposal.ShippingTerms, 100);
        purchaseOrder.Terms = Trim(proposal.Terms, 2000);
        purchaseOrder.Notes = Trim(proposal.Notes, 2000);
        purchaseOrder.DeliveryWarehouseId =
            proposal.DeliveryWarehouseId;
        purchaseOrder.DeliveryAddress =
            Trim(proposal.DeliveryAddress, 500);
        purchaseOrder.DeliveryInstructions =
            Trim(proposal.DeliveryInstructions, 2000);
        purchaseOrder.SubTotal = proposal.SubTotal;
        purchaseOrder.TaxAmount = proposal.TaxAmount;
        purchaseOrder.ShippingCost = proposal.ShippingCost;
        purchaseOrder.MiscellaneousCost =
            proposal.MiscellaneousCost;
        purchaseOrder.TotalAdditionalCost =
            ProcurementPurchaseOrderAmendmentRules.Round(
                proposal.ShippingCost +
                proposal.MiscellaneousCost);
        purchaseOrder.DiscountAmount = proposal.DiscountAmount;
        purchaseOrder.TotalAmount = proposal.TotalAmount;
        purchaseOrder.RevisionNumber = proposal.RevisionNumber;
        purchaseOrder.LastAmendedAt = now;
        purchaseOrder.LastAmendedById = _currentUser.UserId;
        purchaseOrder.Status = "Approved";
        purchaseOrder.ApprovedById = _currentUser.UserId;
        purchaseOrder.ApprovedAt = now;
        purchaseOrder.UpdatedAt = now;
        purchaseOrder.UpdatedBy = ActorName();
        purchaseOrder.LastModifiedById = _currentUser.UserId;
    }

    private async Task RevalidateProposalAsync(
        ProcurementPurchaseOrderAmendment amendment,
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var proposal = Deserialize<PurchaseOrderSnapshot>(
                           amendment.ProposedSnapshotJson)
                       ?? throw Conflict("PO_AMENDMENT_SNAPSHOT_INVALID",
                           "The retained proposed PO snapshot is invalid.");
        ProcurementPurchaseOrderSourceResolution source;
        if (purchaseOrder.ProcurementSourceType ==
                amendment.ProposedSourceType &&
            purchaseOrder.ProcurementSourceId ==
                amendment.ProposedSourceId &&
            purchaseOrder.BusinessPartnerId ==
                amendment.ProposedBusinessPartnerId)
        {
            source = await _sources.RevalidateAsync(
                purchaseOrder,
                "Amend",
                correlationId,
                cancellationToken);
        }
        else
        {
            source = await _sources.ResolveAsync(
                amendment.ProposedSourceType,
                amendment.ProposedSourceId,
                amendment.ProposedBusinessPartnerId,
                correlationId,
                cancellationToken);
        }
        EnsureProposalSource(amendment, source);
        await _sources.ValidateOrderAsync(
            source,
            ToSourceLines(proposal.Items),
            proposal.TotalAmount,
            proposal.Currency,
            correlationId,
            cancellationToken);
    }

    private static void EnsureProposalSource(
        ProcurementPurchaseOrderAmendment amendment,
        ProcurementPurchaseOrderSourceResolution source)
    {
        if (source.SourceType != amendment.ProposedSourceType ||
            source.SourceId != amendment.ProposedSourceId ||
            source.BusinessPartnerId !=
            amendment.ProposedBusinessPartnerId ||
            source.PurchaseRequisitionId !=
            amendment.ProposedSourceRequisitionId ||
            source.SourcingReleaseId !=
            amendment.ProposedSourcingReleaseId ||
            source.SourcingCaseId !=
            amendment.ProposedSourcingCaseId ||
            source.AwardReadinessDecisionId !=
            amendment.ProposedAwardReadinessDecisionId ||
            !string.Equals(
                source.SourceReference,
                amendment.ProposedSourceReference,
                StringComparison.Ordinal) ||
            !string.Equals(
                source.SourceIntegrityHash,
                amendment.ProposedSourceIntegrityHash,
                StringComparison.Ordinal))
            throw Conflict("PO_AMENDMENT_SOURCE_LINEAGE_CHANGED",
                "The proposed approved-source lineage changed after the amendment was documented.");
    }

    private async Task<ProcurementPurchaseOrderSourceResolution>
        ResolveProposedSourceAsync(
            PurchaseOrder purchaseOrder,
            CreateProcurementPurchaseOrderAmendmentRequest request,
            string correlationId,
            CancellationToken cancellationToken)
    {
        if (request.SourceType.HasValue != request.SourceId.HasValue)
            throw Validation("PO_AMENDMENT_SOURCE_SELECTION_INCOMPLETE",
                "SourceType and SourceId must be supplied together.");
        if (request.BusinessPartnerId.HasValue &&
            request.BusinessPartnerId == Guid.Empty)
            throw Validation("PO_AMENDMENT_SUPPLIER_INVALID",
                "The proposed supplier identifier is invalid.");
        if (request.SourceType.HasValue)
        {
            var businessPartnerId =
                request.BusinessPartnerId ??
                purchaseOrder.BusinessPartnerId;
            return await _sources.ResolveAsync(
                request.SourceType.Value,
                request.SourceId!.Value,
                businessPartnerId,
                correlationId,
                cancellationToken);
        }
        if (request.BusinessPartnerId.HasValue &&
            request.BusinessPartnerId !=
            purchaseOrder.BusinessPartnerId)
            throw Validation("PO_AMENDMENT_SOURCE_REQUIRED_FOR_SUPPLIER_CHANGE",
                "Changing the supplier requires a new exact approved source.");
        return await _sources.RevalidateAsync(
            purchaseOrder,
            "Amend",
            correlationId,
            cancellationToken);
    }

    private static PurchaseOrderSnapshot BuildProposal(
        PurchaseOrder purchaseOrder,
        CreateProcurementPurchaseOrderAmendmentRequest request,
        ProcurementPurchaseOrderSourceResolution source)
    {
        var items = request.Items.Select(item =>
            new PurchaseOrderSnapshotItem
            {
                PurchaseOrderItemId =
                    item.PurchaseOrderItemId,
                InventoryItemId = item.InventoryItemId,
                SupplierItemCode =
                    Trim(item.SupplierItemCode, 100),
                ItemDescription =
                    Trim(item.ItemDescription, 200)!,
                OrderedQuantity = item.OrderedQuantity,
                UnitOfMeasure =
                    item.UnitOfMeasure.Trim(),
                ItemUnitOfMeasureId =
                    item.ItemUnitOfMeasureId,
                WarehouseId = item.WarehouseId,
                UnitPrice = item.UnitPrice,
                LineTotal =
                    ProcurementPurchaseOrderAmendmentRules.Round(
                        item.OrderedQuantity *
                        item.UnitPrice),
                PriceListLineId = item.PriceListLineId,
                ExpectedDeliveryDate =
                    NormalizeUtc(item.ExpectedDeliveryDate),
                Notes = Trim(item.Notes, 1000)
            }).ToList();
        var subTotal =
            ProcurementPurchaseOrderAmendmentRules.Round(
                items.Sum(item => item.LineTotal));
        var total =
            ProcurementPurchaseOrderAmendmentRules.CalculateTotal(
                subTotal,
                request.TaxAmount,
                request.ShippingCost,
                request.MiscellaneousCost,
                request.DiscountAmount);
        if (total <= 0)
            throw Validation("PO_AMENDMENT_TOTAL_INVALID",
                "The proposed purchase-order total must be greater than zero.");

        return new PurchaseOrderSnapshot
        {
            RevisionNumber = purchaseOrder.RevisionNumber + 1,
            BusinessPartnerId = source.BusinessPartnerId,
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            SourceReference = source.SourceReference,
            SourceRequisitionId =
                source.PurchaseRequisitionId,
            SourcingReleaseId = source.SourcingReleaseId,
            SourcingCaseId = source.SourcingCaseId,
            AwardReadinessDecisionId =
                source.AwardReadinessDecisionId,
            SourceIntegrityHash =
                source.SourceIntegrityHash,
            Currency = source.CurrencyCode,
            RequiredDate = NormalizeUtc(request.RequiredDate),
            PromisedDate = NormalizeUtc(request.PromisedDate),
            PaymentTerms = Trim(request.PaymentTerms, 100),
            ShippingTerms = Trim(request.ShippingTerms, 100),
            Terms = Trim(request.Terms, 2000),
            Notes = Trim(request.Notes, 2000),
            DeliveryWarehouseId =
                request.DeliveryWarehouseId,
            DeliveryAddress =
                Trim(request.DeliveryAddress, 500),
            DeliveryInstructions =
                Trim(request.DeliveryInstructions, 2000),
            SubTotal = subTotal,
            TaxAmount =
                ProcurementPurchaseOrderAmendmentRules.Round(
                    request.TaxAmount),
            ShippingCost =
                ProcurementPurchaseOrderAmendmentRules.Round(
                    request.ShippingCost),
            MiscellaneousCost =
                ProcurementPurchaseOrderAmendmentRules.Round(
                    request.MiscellaneousCost),
            DiscountAmount =
                ProcurementPurchaseOrderAmendmentRules.Round(
                    request.DiscountAmount),
            TotalAmount = total,
            Items = items
        };
    }

    private static PurchaseOrderSnapshot Capture(
        PurchaseOrder purchaseOrder) =>
        new()
        {
            RevisionNumber = purchaseOrder.RevisionNumber,
            BusinessPartnerId =
                purchaseOrder.BusinessPartnerId,
            SourceType =
                purchaseOrder.ProcurementSourceType ??
                ProcurementPurchaseOrderSourceType.HistoricalMigration,
            SourceId =
                purchaseOrder.ProcurementSourceId ??
                Guid.Empty,
            SourceReference =
                purchaseOrder.ProcurementSourceReference ??
                string.Empty,
            SourceRequisitionId =
                purchaseOrder.SourceRequisitionId ??
                Guid.Empty,
            SourcingReleaseId =
                purchaseOrder.SourcingReleaseId ??
                Guid.Empty,
            SourcingCaseId =
                purchaseOrder.SourcingCaseId ??
                Guid.Empty,
            AwardReadinessDecisionId =
                purchaseOrder.AwardReadinessDecisionId ??
                Guid.Empty,
            SourceIntegrityHash =
                purchaseOrder.SourceIntegrityHash ??
                string.Empty,
            Currency = purchaseOrder.Currency,
            RequiredDate =
                NormalizeUtc(purchaseOrder.RequiredDate),
            PromisedDate =
                NormalizeUtc(purchaseOrder.PromisedDate),
            PaymentTerms = purchaseOrder.PaymentTerms,
            ShippingTerms = purchaseOrder.ShippingTerms,
            Terms = purchaseOrder.Terms,
            Notes = purchaseOrder.Notes,
            DeliveryWarehouseId =
                purchaseOrder.DeliveryWarehouseId,
            DeliveryAddress =
                purchaseOrder.DeliveryAddress,
            DeliveryInstructions =
                purchaseOrder.DeliveryInstructions,
            SubTotal = purchaseOrder.SubTotal,
            TaxAmount = purchaseOrder.TaxAmount,
            ShippingCost = purchaseOrder.ShippingCost,
            MiscellaneousCost =
                purchaseOrder.MiscellaneousCost,
            DiscountAmount =
                purchaseOrder.DiscountAmount,
            TotalAmount = purchaseOrder.TotalAmount,
            Items = purchaseOrder.Items
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.Id)
                .Select(item => new PurchaseOrderSnapshotItem
                {
                    PurchaseOrderItemId = item.Id,
                    InventoryItemId = item.InventoryItemId,
                    SupplierItemCode =
                        item.BusinessPartnerItemCode,
                    ItemDescription =
                        item.ItemDescription ?? string.Empty,
                    OrderedQuantity =
                        item.OrderedQuantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    ItemUnitOfMeasureId =
                        item.ItemUnitOfMeasureId,
                    WarehouseId = item.WarehouseId,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal,
                    PriceListLineId =
                        item.PriceListLineId,
                    ExpectedDeliveryDate =
                        NormalizeUtc(
                            item.ExpectedDeliveryDate),
                    Notes = item.Notes
                }).ToList()
        };

    private static List<ProcurementPurchaseOrderAmendmentDiffDto>
        BuildDiff(
            PurchaseOrderSnapshot before,
            PurchaseOrderSnapshot after)
    {
        var result =
            new List<ProcurementPurchaseOrderAmendmentDiffDto>();
        AddDiff(result, "supplierId",
            before.BusinessPartnerId, after.BusinessPartnerId);
        AddDiff(result, "source.type",
            before.SourceType, after.SourceType);
        AddDiff(result, "source.id",
            before.SourceId, after.SourceId);
        AddDiff(result, "source.reference",
            before.SourceReference, after.SourceReference);
        AddDiff(result, "requiredDate",
            before.RequiredDate, after.RequiredDate);
        AddDiff(result, "promisedDate",
            before.PromisedDate, after.PromisedDate);
        AddDiff(result, "paymentTerms",
            before.PaymentTerms, after.PaymentTerms);
        AddDiff(result, "shippingTerms",
            before.ShippingTerms, after.ShippingTerms);
        AddDiff(result, "terms", before.Terms, after.Terms);
        AddDiff(result, "notes", before.Notes, after.Notes);
        AddDiff(result, "deliveryWarehouseId",
            before.DeliveryWarehouseId,
            after.DeliveryWarehouseId);
        AddDiff(result, "deliveryAddress",
            before.DeliveryAddress, after.DeliveryAddress);
        AddDiff(result, "deliveryInstructions",
            before.DeliveryInstructions,
            after.DeliveryInstructions);
        AddDiff(result, "subTotal",
            before.SubTotal, after.SubTotal);
        AddDiff(result, "taxAmount",
            before.TaxAmount, after.TaxAmount);
        AddDiff(result, "shippingCost",
            before.ShippingCost, after.ShippingCost);
        AddDiff(result, "miscellaneousCost",
            before.MiscellaneousCost,
            after.MiscellaneousCost);
        AddDiff(result, "discountAmount",
            before.DiscountAmount, after.DiscountAmount);
        AddDiff(result, "totalAmount",
            before.TotalAmount, after.TotalAmount);

        var beforeItems = Serialize(before.Items);
        var afterItems = Serialize(after.Items);
        if (!string.Equals(
                beforeItems, afterItems,
                StringComparison.Ordinal))
            result.Add(new ProcurementPurchaseOrderAmendmentDiffDto
            {
                Path = "items",
                Before = beforeItems,
                After = afterItems
            });
        return result;
    }

    private static void AddDiff<T>(
        ICollection<ProcurementPurchaseOrderAmendmentDiffDto> target,
        string path,
        T before,
        T after)
    {
        var left = Serialize(before);
        var right = Serialize(after);
        if (string.Equals(left, right, StringComparison.Ordinal))
            return;
        target.Add(new ProcurementPurchaseOrderAmendmentDiffDto
        {
            Path = path,
            Before = Display(before),
            After = Display(after)
        });
    }

    private async Task EnsureAmendableAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken)
    {
        if (await _frameworkCallOffs.IsFrameworkCallOffPurchaseOrderAsync(
                purchaseOrder.Id, cancellationToken))
            throw Conflict("PO_AMENDMENT_FRAMEWORK_IMMUTABLE",
                "Framework call-off purchase orders remain immutable and must use the dedicated agreement/call-off lifecycle.");
        var hasReceipts = await Receipts.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .AnyAsync(cancellationToken);
        var hasOpen = await Amendments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted &&
                item.Status !=
                ProcurementPurchaseOrderAmendmentStatus.Rejected &&
                item.Status !=
                ProcurementPurchaseOrderAmendmentStatus.Cancelled &&
                item.Status !=
                ProcurementPurchaseOrderAmendmentStatus.Acknowledged)
            .AsNoTracking()
            .AnyAsync(cancellationToken);
        if (!ProcurementPurchaseOrderAmendmentRules.CanCreate(
                purchaseOrder.Status, hasReceipts, hasOpen))
            throw Conflict("PO_AMENDMENT_NOT_ALLOWED",
                BlockedReason(
                    purchaseOrder.Status,
                    false,
                    hasReceipts,
                    hasOpen));
    }

    private async Task EnsureBaseStillCurrentAsync(
        ProcurementPurchaseOrderAmendment amendment,
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken)
    {
        if (purchaseOrder.RevisionNumber !=
            amendment.BaseRevisionNumber)
            throw Conflict("PO_AMENDMENT_BASE_REVISION_STALE",
                "The purchase order has changed since this amendment was documented.");
        if (!string.Equals(
                purchaseOrder.Status,
                amendment.Status ==
                ProcurementPurchaseOrderAmendmentStatus.PendingApproval
                    ? PendingStatus
                    : amendment.PurchaseOrderStatusBefore,
                StringComparison.OrdinalIgnoreCase))
            throw Conflict("PO_AMENDMENT_BASE_STATUS_STALE",
                "The purchase-order lifecycle changed after the amendment was documented.");
        if (await Receipts.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .AnyAsync(cancellationToken))
            throw Conflict("PO_AMENDMENT_RECEIPT_EXISTS",
                "A purchase order with receipt activity cannot be amended in this slice.");
        var currentHash = Hash(Serialize(Capture(purchaseOrder)));
        if (amendment.Status ==
            ProcurementPurchaseOrderAmendmentStatus.Draft &&
            !string.Equals(
                currentHash,
                amendment.BeforeIntegrityHash,
                StringComparison.Ordinal))
            throw Conflict("PO_AMENDMENT_BASE_SNAPSHOT_STALE",
                "The purchase-order values changed after the amendment was documented.");
    }

    private async Task ValidateEvidenceAsync(
        Guid? workflowEvidenceDocumentId,
        Guid? fileUploadRecordId,
        CancellationToken cancellationToken)
    {
        if (workflowEvidenceDocumentId.HasValue)
        {
            var evidence = await _unitOfWork
                .Repository<WorkflowEvidenceDocument>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id ==
                    workflowEvidenceDocumentId.Value &&
                    !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("PO_AMENDMENT_EVIDENCE_NOT_FOUND",
                    "Workflow evidence was not found in the current tenant.");
            if (!evidence.IsCurrent ||
                evidence.VerificationStatus !=
                WorkflowEvidenceVerificationStatus.Verified ||
                evidence.MalwareScanStatus !=
                WorkflowMalwareScanStatus.Clean)
                throw Conflict("PO_AMENDMENT_EVIDENCE_NOT_APPROVED",
                    "Workflow evidence must be current, verified, and malware-clean.");
        }
        if (fileUploadRecordId.HasValue)
        {
            var upload = await _unitOfWork
                .Repository<FileUploadRecord>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == fileUploadRecordId.Value &&
                    !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("PO_AMENDMENT_UPLOAD_NOT_FOUND",
                    "Uploaded evidence was not found in the current tenant.");
            if (upload.VirusScanStatus !=
                FileVirusScanStatus.Clean)
                throw Conflict("PO_AMENDMENT_UPLOAD_UNSAFE",
                    "Uploaded evidence must have a completed Clean safety scan.");
        }
    }

    private async Task RecordEventAsync(
        ProcurementPurchaseOrderAmendment amendment,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        string correlationId,
        DateTime occurredAtUtc,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(
            new ProcurementControlEventWriteRequest
            {
                EventKey = ProcurementControlEventKey.Create(
                    "po-amendment",
                    amendment.TenantId,
                    amendment.Id,
                    action.ToLowerInvariant(),
                    correlationId),
                EventType = EventType,
                Action = action,
                Result = result,
                RuleCode = RuleCodeFor(action),
                RuleVersion = "TDC-0406",
                DecisionKeys = DecisionKeys.ToList(),
                SourceType = SourceType,
                SourceId = amendment.PurchaseOrderId,
                SourceReference =
                    amendment.PurchaseOrder?.OrderNumber ??
                    amendment.AmendmentNumber,
                Reason = reason,
                InputValues = new
                {
                    amendment.AmendmentNumber,
                    amendment.BaseRevisionNumber,
                    amendment.ProposedRevisionNumber,
                    amendment.ProposedSourceType,
                    amendment.ProposedSourceId,
                    amendment.ProposedBusinessPartnerId,
                    amendment.BeforeTotalAmount,
                    amendment.ProposedTotalAmount,
                    amendment.CommitmentDelta,
                    amendment.BeforeIntegrityHash,
                    amendment.ProposedIntegrityHash,
                    amendment.DiffIntegrityHash
                },
                ResultValues = new
                {
                    amendment.Status,
                    amendment.WorkflowInstanceId,
                    amendment.SubmittedAtUtc,
                    amendment.DecidedAtUtc,
                    amendment.AppliedAtUtc
                },
                Before = before,
                After = after,
                CorrelationId = correlationId,
                OccurredAtUtc = occurredAtUtc,
                Evidence = evidence.ToList()
            },
            cancellationToken);
    }

    private async Task PublishAsync(
        string topic,
        ProcurementPurchaseOrderAmendment amendment,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(
                new NotificationTopicEvent
                {
                    TenantId = amendment.TenantId,
                    TopicKey = topic,
                    NotificationType = EventType,
                    EntityType = SourceType,
                    EntityId = amendment.PurchaseOrderId,
                    TriggeredByUserId =
                        _currentUser.UserId,
                    Data = new Dictionary<string, object>
                    {
                        ["amendmentId"] = amendment.Id,
                        ["amendmentNumber"] =
                            amendment.AmendmentNumber,
                        ["purchaseOrderId"] =
                            amendment.PurchaseOrderId,
                        ["revisionNumber"] =
                            amendment.ProposedRevisionNumber,
                        ["status"] =
                            amendment.Status.ToString(),
                        ["supplierId"] =
                            amendment.ProposedBusinessPartnerId
                    },
                    Metadata =
                        new Dictionary<string, object>
                        {
                            ["correlationId"] =
                                amendment.CorrelationId,
                            ["ruleCode"] = RuleCodeFor(topic),
                            ["ruleVersion"] = "TDC-0406"
                        }
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish PO amendment notification {Topic} for {AmendmentId}",
                topic,
                amendment.Id);
        }
    }

    private static string RuleCodeFor(string value) =>
        value.Contains("dispatch", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("acknowledg", StringComparison.OrdinalIgnoreCase)
            ? "PO-006"
            : "PO-005";

    private async Task ExecuteAsync(
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        if (_unitOfWork.HasActiveTransaction)
        {
            await operation();
            return;
        }

        await _unitOfWork.ExecuteInStrategyAsync(
            async () =>
            {
                await _unitOfWork.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                try
                {
                    await operation();
                    await _unitOfWork.CommitAsync(
                        cancellationToken);
                }
                catch
                {
                    if (_unitOfWork.HasActiveTransaction)
                        await _unitOfWork.RollbackAsync(
                            cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            },
            cancellationToken);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw Authorization(
                "Supplier portal users cannot perform internal PO amendment actions.");
        if (IsAdministrator())
            return;
        try
        {
            var decision = await _accessControl.EnforceCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    SourceType = EventType,
                    SourceReference = sourceReference
                },
                correlationId,
                cancellationToken);
            if (!decision.Allowed)
                throw Authorization(decision.Message);
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            throw Authorization(exception.Message);
        }
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw Authorization(
                "Supplier portal users must use the supplier-scoped PO amendment view.");
    }

    private void EnsureExternalReader()
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.IsExternalUser)
            throw Authorization(
                "This operation is restricted to supplier portal users.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw Authorization(
                "An authenticated tenant context is required.");
    }

    private async Task<IReadOnlySet<Guid>>
        GetExternalPartnerIdsAsync(
            CancellationToken cancellationToken) =>
        (await _unitOfWork.Repository<BusinessPartnerUser>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.UserId == _currentUser.UserId &&
                item.IsActive &&
                !item.IsDeleted &&
                item.BusinessPartner.TenantId ==
                _currentUser.TenantId &&
                !item.BusinessPartner.IsDeleted &&
                item.BusinessPartner.IsActive &&
                !item.BusinessPartner.IsBlacklisted &&
                item.BusinessPartner.ApprovalStatus ==
                BusinessPartnerLifecyclePolicy
                    .ApprovedApprovalStatus &&
                (item.BusinessPartner.RegistrationStatus ==
                 BusinessPartnerLifecyclePolicy
                     .ActiveRegistrationStatus ||
                 item.BusinessPartner.RegistrationStatus ==
                 BusinessPartnerLifecyclePolicy
                     .LegacyApprovedRegistrationStatus) &&
                (item.BusinessPartner.PartnerType ==
                 "Supplier" ||
                 item.BusinessPartner.PartnerType ==
                 "Contractor" ||
                 item.BusinessPartner.PartnerType == "Both"))
            .Select(item => item.BusinessPartnerId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    private IQueryable<PurchaseOrder> PurchaseOrderQuery() =>
        PurchaseOrders.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .Include(item => item.BusinessPartner)
            .Include(item =>
                item.Items.Where(line => !line.IsDeleted));

    private IQueryable<ProcurementPurchaseOrderAmendment>
        AmendmentQuery() =>
        Amendments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .Include(item => item.PurchaseOrder)
                .ThenInclude(item => item.BusinessPartner)
            .Include(item => item.ProposedBusinessPartner)
            .Include(item =>
                item.CommitmentAdjustments.Where(row =>
                    !row.IsDeleted))
            .Include(item =>
                item.Dispatches.Where(dispatch =>
                    !dispatch.IsDeleted))
                .ThenInclude(dispatch =>
                    dispatch.Acknowledgements.Where(
                        acknowledgement =>
                            !acknowledgement.IsDeleted));

    private IQueryable<ProcurementPurchaseOrderAmendmentDispatch>
        DispatchQuery() =>
        Dispatches.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .Include(item => item.Amendment)
                .ThenInclude(item => item.PurchaseOrder)
                    .ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Amendment)
                .ThenInclude(item =>
                    item.ProposedBusinessPartner)
            .Include(item =>
                item.Acknowledgements.Where(
                    acknowledgement =>
                        !acknowledgement.IsDeleted));

    private IQueryable<ProcurementPurchaseOrderAmendmentAcknowledgement>
        AcknowledgementQuery() =>
        Acknowledgements.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId &&
            !item.IsDeleted);

    private async Task<ProcurementPurchaseOrderAmendment>
        LoadAmendmentAsync(
            Guid id,
            CancellationToken cancellationToken) =>
        await AmendmentQuery()
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken)
        ?? throw NotFound(
            "PO_AMENDMENT_NOT_FOUND",
            "The PO amendment was not found in the current tenant.");

    private async Task<ProcurementPurchaseOrderAmendment>
        LoadAmendmentForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
        await AmendmentQuery()
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken)
        ?? throw NotFound(
            "PO_AMENDMENT_NOT_FOUND",
            "The PO amendment was not found in the current tenant.");

    private async Task<ProcurementPurchaseOrderAmendmentDispatch>
        LoadDispatchAsync(
            Guid id,
            CancellationToken cancellationToken) =>
        await DispatchQuery()
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken)
        ?? throw NotFound(
            "PO_AMENDMENT_DISPATCH_NOT_FOUND",
            "The PO amendment dispatch was not found in the current tenant.");

    private async Task<ProcurementPurchaseOrderAmendmentDto>
        GetAmendmentDtoAsync(
            Guid id,
            CancellationToken cancellationToken) =>
        Map(await LoadAmendmentAsync(id, cancellationToken));

    private async Task<ProcurementPurchaseOrderAmendmentDispatchDto>
        GetDispatchDtoAsync(
            Guid id,
            CancellationToken cancellationToken) =>
        MapDispatch(
            await LoadDispatchAsync(id, cancellationToken));

    private static ProcurementPurchaseOrderAmendmentDto Map(
        ProcurementPurchaseOrderAmendment item) =>
        new()
        {
            Id = item.Id,
            PurchaseOrderId = item.PurchaseOrderId,
            PurchaseOrderNumber =
                item.PurchaseOrder?.OrderNumber ?? string.Empty,
            SupplierName =
                item.ProposedBusinessPartner?.PartnerName ??
                item.PurchaseOrder?.BusinessPartner?.PartnerName ??
                string.Empty,
            AmendmentNumber = item.AmendmentNumber,
            AmendmentSequence = item.AmendmentSequence,
            BaseRevisionNumber = item.BaseRevisionNumber,
            ProposedRevisionNumber =
                item.ProposedRevisionNumber,
            Status = item.Status,
            Reason = item.Reason,
            ChangeScope = item.ChangeScope,
            BeforeTotalAmount = item.BeforeTotalAmount,
            ProposedTotalAmount = item.ProposedTotalAmount,
            CommitmentDelta = item.CommitmentDelta,
            Currency = item.Currency,
            ProposedSourceType = item.ProposedSourceType,
            ProposedSourceId = item.ProposedSourceId,
            ProposedSourceReference =
                item.ProposedSourceReference,
            ProposedBusinessPartnerId =
                item.ProposedBusinessPartnerId,
            ProposedBusinessPartnerName =
                item.ProposedBusinessPartner?.PartnerName ??
                string.Empty,
            BeforeIntegrityHash = item.BeforeIntegrityHash,
            ProposedIntegrityHash =
                item.ProposedIntegrityHash,
            DiffIntegrityHash = item.DiffIntegrityHash,
            Diffs =
                Deserialize<List<ProcurementPurchaseOrderAmendmentDiffDto>>(
                    item.DiffJson) ?? [],
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowInstanceId = item.WorkflowInstanceId,
            SubmittedAtUtc = item.SubmittedAtUtc,
            SubmittedByName = item.SubmittedByName,
            DecidedAtUtc = item.DecidedAtUtc,
            DecidedByName = item.DecidedByName,
            DecisionComment = item.DecisionComment,
            AppliedAtUtc = item.AppliedAtUtc,
            AppliedByName = item.AppliedByName,
            RequestEvidenceReference =
                item.RequestEvidenceReference ?? string.Empty,
            ApprovalEvidenceReference =
                item.ApprovalEvidenceReference,
            RowVersion = item.RowVersion.Length == 0
                ? string.Empty
                : Convert.ToBase64String(item.RowVersion),
            CommitmentAdjustments =
                item.CommitmentAdjustments
                    .Where(row => !row.IsDeleted)
                    .OrderBy(row => row.Sequence)
                    .Select(MapAdjustment)
                    .ToList(),
            Dispatches = item.Dispatches
                .Where(row => !row.IsDeleted)
                .OrderBy(row => row.Sequence)
                .Select(MapDispatch)
                .ToList()
        };

    private static ProcurementPurchaseOrderCommitmentAdjustmentDto
        MapAdjustment(
            ProcurementPurchaseOrderCommitmentAdjustment item) =>
        new()
        {
            Id = item.Id,
            Sequence = item.Sequence,
            PurchaseOrderAmountBefore =
                item.PurchaseOrderAmountBefore,
            PurchaseOrderAmountAfter =
                item.PurchaseOrderAmountAfter,
            RequisitionExposureBefore =
                item.RequisitionExposureBefore,
            RequisitionExposureAfter =
                item.RequisitionExposureAfter,
            CommitmentAmountBefore =
                item.CommitmentAmountBefore,
            CommitmentAmountAfter =
                item.CommitmentAmountAfter,
            DeltaAmount = item.DeltaAmount,
            BudgetCommittedBefore =
                item.BudgetCommittedBefore,
            BudgetCommittedAfter =
                item.BudgetCommittedAfter,
            BudgetAvailableBefore =
                item.BudgetAvailableBefore,
            BudgetAvailableAfter =
                item.BudgetAvailableAfter,
            Currency = item.Currency,
            AppliedAtUtc = item.AppliedAtUtc,
            AppliedByName = item.AppliedByName,
            IntegrityHash = item.IntegrityHash
        };

    private static ProcurementPurchaseOrderAmendmentDispatchDto
        MapDispatch(
            ProcurementPurchaseOrderAmendmentDispatch item) =>
        new()
        {
            Id = item.Id,
            RevisionNumber = item.RevisionNumber,
            Sequence = item.Sequence,
            Channel = item.Channel,
            Destination = item.Destination,
            DispatchReference = item.DispatchReference,
            DocumentReference = item.DocumentReference,
            OrganizationSignatureEvidenceReference =
                item.OrganizationSignatureEvidenceReference,
            DispatchEvidenceReference =
                item.DispatchEvidenceReference,
            DispatchedAtUtc = item.DispatchedAtUtc,
            DispatchedByName = item.DispatchedByName,
            IntegrityHash = item.IntegrityHash,
            Acknowledgements = item.Acknowledgements
                .Where(row => !row.IsDeleted)
                .OrderBy(row => row.Sequence)
                .Select(MapAcknowledgement)
                .ToList()
        };

    private static ProcurementPurchaseOrderAmendmentAcknowledgementDto
        MapAcknowledgement(
            ProcurementPurchaseOrderAmendmentAcknowledgement item) =>
        new()
        {
            Id = item.Id,
            Sequence = item.Sequence,
            Outcome = item.Outcome,
            AcknowledgedAtUtc = item.AcknowledgedAtUtc,
            AcknowledgedByUserId =
                item.AcknowledgedByUserId,
            AcknowledgedByBusinessPartnerId =
                item.AcknowledgedByBusinessPartnerId,
            AcknowledgementChannel =
                item.AcknowledgementChannel,
            AcknowledgementReference =
                item.AcknowledgementReference,
            EvidenceReference = item.EvidenceReference,
            Comments = item.Comments,
            IntegrityHash = item.IntegrityHash
        };

    private static IReadOnlyList<ProcurementPurchaseOrderSourceOrderLine>
        ToSourceLines(
            IEnumerable<PurchaseOrderSnapshotItem> items) =>
        items.Select(item =>
            new ProcurementPurchaseOrderSourceOrderLine
            {
                InventoryItemId = item.InventoryItemId,
                ItemDescription = item.ItemDescription,
                OrderedQuantity = item.OrderedQuantity,
                UnitOfMeasure = item.UnitOfMeasure,
                UnitPrice = item.UnitPrice
            }).ToList();

    private static string BlockedReason(
        string status,
        bool isFramework,
        bool hasReceipts,
        bool hasOpen)
    {
        if (isFramework)
            return "Framework call-off purchase orders remain governed by their dedicated immutable lifecycle.";
        if (hasReceipts)
            return "A purchase order with receipt activity cannot be amended without changing Phase 5 receiving behavior.";
        if (hasOpen)
            return "Complete or reject the current amendment and supplier acknowledgement before creating another.";
        return $"Purchase-order status {status} is not eligible for a controlled approved amendment.";
    }

    private static void ValidateDestination(
        ProcurementPurchaseOrderDispatchChannel channel,
        string destination,
        Guid supplierId)
    {
        if (channel ==
            ProcurementPurchaseOrderDispatchChannel.SupplierPortal &&
            !string.Equals(
                destination.Trim(),
                "portal",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                destination.Trim(),
                supplierId.ToString(),
                StringComparison.OrdinalIgnoreCase))
            throw Validation("PO_AMENDMENT_DISPATCH_DESTINATION_MISMATCH",
                "Supplier-portal dispatch must target the server-derived approved supplier account.");
    }

    private static IReadOnlyList<ProcurementControlEventEvidenceReference>
        Evidence(
            string reference,
            Guid? workflowDocumentId,
            Guid? fileUploadRecordId,
            string label,
            string requirement)
    {
        var result =
            new List<ProcurementControlEventEvidenceReference>
            {
                new()
                {
                    ReferenceKind =
                        ProcurementControlEvidenceReferenceKind
                            .ExternalReference,
                    Reference = reference.Trim(),
                    Label = label,
                    RequirementKey = requirement
                }
            };
        if (workflowDocumentId.HasValue)
            result.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind =
                    ProcurementControlEvidenceReferenceKind
                        .WorkflowEvidenceDocument,
                ReferenceId = workflowDocumentId,
                Reference =
                    $"workflow-document:{workflowDocumentId:N}",
                Label = label,
                RequirementKey = requirement
            });
        if (fileUploadRecordId.HasValue)
            result.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind =
                    ProcurementControlEvidenceReferenceKind
                        .FileUploadRecord,
                ReferenceId = fileUploadRecordId,
                Reference =
                    $"file-upload:{fileUploadRecordId:N}",
                Label = label,
                RequirementKey = requirement
            });
        return result;
    }

    private bool IsAdministrator() =>
        _currentUser.HasRole("SystemAdmin") ||
        _currentUser.HasRole("SuperAdmin") ||
        _currentUser.HasRole("TenantAdmin") ||
        _currentUser.HasRole("Administrator") ||
        _currentUser.HasRole("Admin");

    private string ActorName() =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

    private static void EnsureStatus(
        ProcurementPurchaseOrderAmendment amendment,
        ProcurementPurchaseOrderAmendmentStatus expected,
        string message)
    {
        if (amendment.Status != expected)
            throw Conflict("PO_AMENDMENT_STATUS_INVALID", message);
    }

    private static void EnsureRowVersion(
        byte[] current,
        string supplied)
    {
        if (current.Length == 0)
        {
            if (!string.IsNullOrEmpty(supplied))
                throw Conflict("PO_AMENDMENT_CONCURRENCY_CONFLICT",
                    "The amendment row version is stale.");
            return;
        }
        byte[] parsed;
        try
        {
            parsed = Convert.FromBase64String(supplied);
        }
        catch (FormatException)
        {
            throw Validation("PO_AMENDMENT_ROW_VERSION_INVALID",
                "RowVersion must be a valid base64 value.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict("PO_AMENDMENT_CONCURRENCY_CONFLICT",
                "The amendment changed after it was loaded. Refresh and retry.");
    }

    private static void Require(
        string? value,
        string code,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Validation(code, message);
    }

    private static void RequireIdempotency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Validation("PO_AMENDMENT_IDEMPOTENCY_REQUIRED",
                "IdempotencyKey is required.");
        if (value.Trim().Length > 100)
            throw Validation("PO_AMENDMENT_IDEMPOTENCY_INVALID",
                "IdempotencyKey cannot exceed 100 characters.");
    }

    private static string NormalizeCorrelation(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : Truncate(value.Trim(), 100);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue
            ? value.Value.Kind == DateTimeKind.Utc
                ? value
                : value.Value.ToUniversalTime()
            : null;

    private static decimal Available(ProcurementBudget budget) =>
        ProcurementPurchaseOrderAmendmentRules.Round(
            budget.AllocatedAmount -
            budget.UtilizedAmount -
            budget.CommittedAmount -
            budget.ReservedAmount);

    private static string? Trim(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Truncate(value.Trim(), maximum);

    private static string Truncate(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions);

    private static string Hash(string value) =>
        Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string? Display<T>(T value) =>
        value switch
        {
            null => null,
            DateTime date => date.ToUniversalTime()
                .ToString("O"),
            DateTimeOffset date => date.ToUniversalTime()
                .ToString("O"),
            _ => value.ToString()
        };

    private static ProcurementPurchaseOrderAmendmentNotFoundException
        NotFound(string code, string message) => new(code, message);

    private static ProcurementPurchaseOrderAmendmentValidationException
        Validation(string code, string message) => new(code, message);

    private static ProcurementPurchaseOrderAmendmentConflictException
        Conflict(string code, string message) => new(code, message);

    private static ProcurementPurchaseOrderAmendmentAuthorizationException
        Authorization(string message) => new(message);

    private sealed class AutomaticApprovalDetectedException :
        InvalidOperationException;

    private sealed class PurchaseOrderSnapshot
    {
        public int RevisionNumber { get; set; }
        public Guid BusinessPartnerId { get; set; }
        public ProcurementPurchaseOrderSourceType SourceType { get; set; }
        public Guid SourceId { get; set; }
        public string SourceReference { get; set; } = string.Empty;
        public Guid SourceRequisitionId { get; set; }
        public Guid SourcingReleaseId { get; set; }
        public Guid SourcingCaseId { get; set; }
        public Guid AwardReadinessDecisionId { get; set; }
        public string SourceIntegrityHash { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public DateTime? RequiredDate { get; set; }
        public DateTime? PromisedDate { get; set; }
        public string? PaymentTerms { get; set; }
        public string? ShippingTerms { get; set; }
        public string? Terms { get; set; }
        public string? Notes { get; set; }
        public Guid? DeliveryWarehouseId { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryInstructions { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal MiscellaneousCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<PurchaseOrderSnapshotItem> Items { get; set; } = [];
    }

    private sealed class PurchaseOrderSnapshotItem
    {
        public Guid? PurchaseOrderItemId { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string? SupplierItemCode { get; set; }
        public string ItemDescription { get; set; } = string.Empty;
        public decimal OrderedQuantity { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public Guid? ItemUnitOfMeasureId { get; set; }
        public Guid? WarehouseId { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public Guid? PriceListLineId { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? Notes { get; set; }
    }
}
