using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementReceiptInspectionService :
    IProcurementReceiptInspectionService
{
    private const string EventType = "ProcurementReceiptInspection";
    private const string WorkflowEntityType = "PROCUREMENT_RECEIPT_INSPECTION";
    private const string ReadPermission = "procurement.inventory.read";
    private const string ManagePermission = "procurement.inventory.receive";
    private const string ApprovePermission = "procurement.purchase-order.approve";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementConfigurationService _configuration;
    private readonly IProcurementComplianceDecisionService _compliance;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IProcurementSodGuardService _sod;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly IProcurementPurchaseOrderSodService _purchaseOrderSod;
    private readonly IProcurementReceiptSourceControlService _sourceControl;
    private readonly IProcurementReceiptInspectionStore _store;
    private readonly IInventoryValuationService _valuation;
    private readonly IProcurementBudgetService _budgetService;
    private readonly ILogger<ProcurementReceiptInspectionService> _logger;

    public ProcurementReceiptInspectionService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementConfigurationService configuration,
        IProcurementComplianceDecisionService compliance,
        IWorkflowIntegrationService workflow,
        IProcurementSodGuardService sod,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        IProcurementPurchaseOrderSodService purchaseOrderSod,
        IProcurementReceiptSourceControlService sourceControl,
        IProcurementReceiptInspectionStore store,
        IInventoryValuationService valuation,
        IProcurementBudgetService budgetService,
        ILogger<ProcurementReceiptInspectionService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _access = access;
        _configuration = configuration;
        _compliance = compliance;
        _workflow = workflow;
        _sod = sod;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _purchaseOrderSod = purchaseOrderSod;
        _sourceControl = sourceControl;
        _store = store;
        _valuation = valuation;
        _budgetService = budgetService;
        _logger = logger;
    }

    private IGenericRepository<ProcurementReceiptInspectionCase> Cases =>
        _unitOfWork.Repository<ProcurementReceiptInspectionCase>();
    private IGenericRepository<ProcurementReceiptInspectionLine> Lines =>
        _unitOfWork.Repository<ProcurementReceiptInspectionLine>();
    private IGenericRepository<ProcurementReceiptInspectionEvidence> Evidence =>
        _unitOfWork.Repository<ProcurementReceiptInspectionEvidence>();
    private IGenericRepository<ProcurementReceiptInspectionAction> Actions =>
        _unitOfWork.Repository<ProcurementReceiptInspectionAction>();

    public async Task<ProcurementReceiptInspectionOverviewDto> GetOverviewAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await LoadReceiptAsync(receiptId, false, cancellationToken);
        if (_currentUser.IsExternalUser)
            await EnsureLinkedSupplierAsync(receipt.PurchaseOrder.BusinessPartnerId, cancellationToken);
        else
            await EnsureCapabilityAsync(ReadPermission, receipt, NewCorrelation(), cancellationToken);

        var history = await CaseQuery(false)
            .Where(item => item.PurchaseOrderReceiptId == receiptId)
            .OrderByDescending(item => item.Sequence)
            .ToListAsync(cancellationToken);
        var current = history.FirstOrDefault(item =>
            item.Status is not ProcurementReceiptInspectionStatus.Cancelled);
        var externalLinked = _currentUser.IsExternalUser;
        return new ProcurementReceiptInspectionOverviewDto
        {
            PurchaseOrderReceiptId = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            PurchaseOrderNumber = receipt.PurchaseOrder.OrderNumber,
            SupplierName = receipt.PurchaseOrder.BusinessPartner?.PartnerName ?? string.Empty,
            CanEdit = !externalLinked && current is not null &&
                      ProcurementReceiptInspectionRules.CanEdit(current.Status),
            CanSubmit = !externalLinked && current is not null &&
                        ProcurementReceiptInspectionRules.CanSubmit(current.Status),
            CanDecide = !externalLinked && current is not null &&
                        ProcurementReceiptInspectionRules.CanDecide(current.Status),
            CanAcknowledge = externalLinked && current is not null && current.QualityHold &&
                             current.SupplierAcknowledgementStatus ==
                             ProcurementReceiptSupplierAcknowledgementStatus.Pending,
            CanResolve = !externalLinked && current is not null &&
                         ProcurementReceiptInspectionRules.CanResolve(current.Status),
            CanClose = !externalLinked && current is not null &&
                       current.Status == ProcurementReceiptInspectionStatus.ClosureReady &&
                       ProcurementReceiptInspectionRules.CanClose(
                           current.SupplierAcknowledgementStatus,
                           current.ResolutionStatus),
            DecisionKeys = DecisionKeys,
            Current = current is null ? null : Map(current),
            History = history.Select(Map).ToList()
        };
    }

    public async Task<IReadOnlyList<ProcurementReceiptInspectionOverviewDto>>
        GetSupplierOverviewAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.IsExternalUser)
            throw new ProcurementReceiptInspectionAuthorizationException(
                "Only linked supplier portal users may access the supplier receipt-inspection queue.");

        var directPartnerIds = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.UserId == _currentUser.UserId && !item.IsDeleted)
            .AsNoTracking().Select(item => item.Id).ToListAsync(cancellationToken);
        var linkedPartnerIds = await _unitOfWork.Repository<BusinessPartnerUser>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.UserId == _currentUser.UserId && item.IsActive && !item.IsDeleted)
            .AsNoTracking().Select(item => item.BusinessPartnerId).ToListAsync(cancellationToken);
        var partnerIds = directPartnerIds.Concat(linkedPartnerIds).Distinct().ToList();
        if (partnerIds.Count == 0)
            throw new ProcurementReceiptInspectionAuthorizationException(
                "The current portal account is not linked to a supplier in this tenant.");

        var receiptIds = await Cases.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                partnerIds.Contains(item.PurchaseOrderReceipt.PurchaseOrder.BusinessPartnerId))
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.PurchaseOrderReceiptId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var result = new List<ProcurementReceiptInspectionOverviewDto>(receiptIds.Count);
        foreach (var receiptId in receiptIds)
            result.Add(await GetOverviewAsync(receiptId, cancellationToken));
        return result;
    }

    public async Task<ProcurementReceiptInspectionDto> InitializeAsync(
        Guid receiptId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var receipt = await LoadReceiptAsync(receiptId, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, receipt, correlation, cancellationToken);
        await _purchaseOrderSod.EnforceReceiptActionAsync(
            receipt.PurchaseOrder,
            ProcurementPurchaseOrderSodRules.InitializeReceiptInspection,
            correlation,
            cancellationToken);
        var existing = await CaseQuery(true)
            .FirstOrDefaultAsync(item => item.PurchaseOrderReceiptId == receiptId &&
                                         item.Status != ProcurementReceiptInspectionStatus.Cancelled &&
                                         item.Status != ProcurementReceiptInspectionStatus.Rejected,
                cancellationToken);
        if (existing is not null) return Map(existing);

        ProcurementReceiptInspectionCase? created = null;
        await ExecuteAsync(async () =>
        {
            receipt = await LoadReceiptAsync(receiptId, true, cancellationToken);
            var concurrent = await CaseQuery(true)
                .FirstOrDefaultAsync(item => item.PurchaseOrderReceiptId == receiptId &&
                                             item.Status != ProcurementReceiptInspectionStatus.Cancelled &&
                                             item.Status != ProcurementReceiptInspectionStatus.Rejected,
                    cancellationToken);
            if (concurrent is not null)
            {
                created = concurrent;
                return;
            }

            var governance = await ResolveGovernanceAsync(receipt, correlation, cancellationToken);
            var sequence = (await Cases.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderReceiptId == receiptId && !item.IsDeleted)
                .Select(item => (int?)item.Sequence).MaxAsync(cancellationToken) ?? 0) + 1;
            var now = DateTime.UtcNow;
            var sourceSnapshot = Serialize(new
            {
                receipt.Id,
                receipt.ReceiptNumber,
                receipt.PurchaseOrderId,
                receipt.PurchaseOrder.OrderNumber,
                receipt.PurchaseOrder.BusinessPartnerId,
                receipt.ReceiptSourceIntegrityHash,
                lines = receipt.Items.OrderBy(item => item.Id).Select(item => new
                {
                    item.Id,
                    item.PurchaseOrderItemId,
                    item.ReceivedQuantity,
                    item.ReceiptLineIntegrityHash
                })
            });
            created = new ProcurementReceiptInspectionCase
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                PurchaseOrderReceiptId = receipt.Id,
                Sequence = sequence,
                Status = ProcurementReceiptInspectionStatus.Draft,
                ReceivedQuantity = receipt.Items.Sum(item => item.ReceivedQuantity),
                PendingQuantity = receipt.Items.Sum(item => item.ReceivedQuantity),
                SupplierAcknowledgementStatus = ProcurementReceiptSupplierAcknowledgementStatus.NotRequired,
                ResolutionKind = ProcurementReceiptResolutionKind.None,
                ResolutionStatus = ProcurementReceiptResolutionStatus.NotRequired,
                ApBlockedQuantity = receipt.Items.Sum(item => item.ReceivedQuantity),
                ConfigurationProfileId = governance.Profile.Id,
                ConfigurationProfileVersion = governance.Profile.Version,
                PolicySetId = governance.Authority.Policy!.PolicySetId,
                PolicyVersion = governance.Authority.Policy.Version,
                AuthorityRuleId = governance.Authority.Steps.First().RuleId,
                AuthorityName = governance.Authority.Steps.First().AuthorityName,
                WorkflowDefinitionId = governance.Authority.Workflow!.WorkflowDefinitionId,
                CreatedByUserId = _currentUser.UserId,
                CreatedByName = ActorName,
                IdempotencyKey = $"inspection:{receipt.Id:N}:{sequence}",
                CorrelationId = correlation,
                SourceSnapshotJson = sourceSnapshot,
                SourceSnapshotHash = Hash(sourceSnapshot),
                DecisionSnapshotJson = governance.Snapshot,
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            created.IntegrityHash = CaseHash(created);
            foreach (var receiptLine in receipt.Items.OrderBy(item => item.Id))
            {
                var line = new ProcurementReceiptInspectionLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUser.TenantId,
                    InspectionCaseId = created.Id,
                    PurchaseOrderReceiptItemId = receiptLine.Id,
                    ReceivedQuantity = receiptLine.ReceivedQuantity,
                    PendingQuantity = receiptLine.ReceivedQuantity,
                    Disposition = ProcurementReceiptDisposition.Pending,
                    CreatedAt = now,
                    CreatedBy = ActorName,
                    CreatedById = _currentUser.UserId
                };
                line.IntegrityHash = LineHash(line);
                created.Lines.Add(line);
            }
            await Cases.AddAsync(created);
            await AddActionAsync(created, ProcurementReceiptInspectionActionType.Created,
                "Inspection case initialized", "Pending governed inspection", correlation,
                created.IdempotencyKey, null, cancellationToken);
            receipt.RequiresInspection = true;
            receipt.Status = "Pending Inspection";
            receipt.InspectionResult = "Pending";
            receipt.UpdatedAt = now;
            await _unitOfWork.Repository<PurchaseOrderReceipt>().UpdateAsync(receipt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        await RecordEventAsync(created!, "Created", ProcurementControlEventResult.ReviewRequired,
            null, Snapshot(created!), "Receipt inspection initialized", correlation, cancellationToken);
        return Map(created!);
    }

    public async Task<ProcurementReceiptInspectionDto> SaveAsync(
        Guid receiptId,
        SaveProcurementReceiptInspectionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var receipt = await LoadReceiptAsync(receiptId, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, receipt, correlation, cancellationToken);
        await _purchaseOrderSod.EnforceReceiptActionAsync(
            receipt.PurchaseOrder,
            ProcurementPurchaseOrderSodRules.SaveReceiptInspection,
            correlation,
            cancellationToken);
        var inspection = await CaseQuery(true).FirstOrDefaultAsync(item =>
            item.PurchaseOrderReceiptId == receiptId &&
            item.Status != ProcurementReceiptInspectionStatus.Cancelled &&
            item.Status != ProcurementReceiptInspectionStatus.Rejected, cancellationToken);
        if (inspection is null)
        {
            await InitializeAsync(receiptId, correlation, cancellationToken);
            inspection = await CaseQuery(true).FirstAsync(item =>
                item.PurchaseOrderReceiptId == receiptId &&
                item.Status == ProcurementReceiptInspectionStatus.Draft, cancellationToken);
        }
        if (!ProcurementReceiptInspectionRules.CanEdit(inspection.Status))
            throw Conflict("RCV_INSPECTION_NOT_EDITABLE", "Only a Draft inspection can be edited.");
        if (!string.IsNullOrWhiteSpace(request.RowVersion))
            EnsureRowVersion(inspection.RowVersion, request.RowVersion);
        if (request.Lines.Count != inspection.Lines.Count ||
            request.Lines.Select(item => item.PurchaseOrderReceiptItemId).Distinct().Count() != request.Lines.Count)
            throw Validation("RCV_INSPECTION_LINES_INCOMPLETE",
                "Every governed receipt line must occur exactly once in the inspection decision.");

        var before = Snapshot(inspection);
        foreach (var line in inspection.Lines)
        {
            var candidate = request.Lines.SingleOrDefault(item =>
                item.PurchaseOrderReceiptItemId == line.PurchaseOrderReceiptItemId)
                ?? throw Validation("RCV_INSPECTION_LINE_MISSING",
                    "Every governed receipt line requires a disposition.");
            var result = ProcurementReceiptInspectionRules.Evaluate(
                line.ReceivedQuantity, candidate.AcceptedQuantity, candidate.RejectedQuantity);
            if (result.Rejected > 0 && string.IsNullOrWhiteSpace(candidate.RejectionReason))
                throw Validation("RCV_REJECTION_REASON_REQUIRED",
                    "A documented rejection reason is required for every rejected quantity.");
            if (result.Rejected > 0 && (!candidate.QuarantineLocationId.HasValue ||
                                        candidate.QuarantineLocationId == Guid.Empty))
                throw Validation("RCV_QUARANTINE_LOCATION_REQUIRED",
                    "Rejected quantities require a controlled quarantine location.");
            if (candidate.QuarantineLocationId.HasValue)
                await EnsureQuarantineLocationAsync(candidate.QuarantineLocationId.Value,
                    receipt, cancellationToken);
            line.AcceptedQuantity = result.Accepted;
            line.RejectedQuantity = result.Rejected;
            line.PendingQuantity = result.Pending;
            line.Disposition = result.Disposition;
            line.RejectionReason = Trim(candidate.RejectionReason, 1000);
            line.InspectionNotes = Trim(candidate.InspectionNotes, 1000);
            line.QuarantineLocationId = candidate.QuarantineLocationId;
            line.UpdatedAt = DateTime.UtcNow;
            line.UpdatedBy = ActorName;
            line.LastModifiedById = _currentUser.UserId;
            line.IntegrityHash = LineHash(line);
            await Lines.UpdateAsync(line);
        }
        Recalculate(inspection);
        inspection.DecisionComment = request.Comment.Trim();
        inspection.CorrelationId = correlation;
        inspection.UpdatedAt = DateTime.UtcNow;
        inspection.UpdatedBy = ActorName;
        inspection.LastModifiedById = _currentUser.UserId;
        inspection.IntegrityHash = CaseHash(inspection);
        await AddActionAsync(inspection, ProcurementReceiptInspectionActionType.Saved,
            request.Comment, "Inspection quantities saved", correlation,
            request.IdempotencyKey.Trim(), null, cancellationToken);
        await Cases.UpdateAsync(inspection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(inspection, "Saved", ProcurementControlEventResult.ReviewRequired,
            before, Snapshot(inspection), request.Comment, correlation, cancellationToken);
        return Map(inspection);
    }

    public async Task<ProcurementReceiptInspectionDto> SubmitAsync(
        Guid caseId,
        SubmitProcurementReceiptInspectionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var inspection = await LoadCaseAsync(caseId, cancellationToken);
        var receipt = inspection.PurchaseOrderReceipt;
        await EnsureCapabilityAsync(ManagePermission, receipt, correlation, cancellationToken);
        await _purchaseOrderSod.EnforceReceiptActionAsync(
            receipt.PurchaseOrder,
            ProcurementPurchaseOrderSodRules.SubmitReceiptInspection,
            correlation,
            cancellationToken);
        EnsureRowVersion(inspection.RowVersion, request.RowVersion);
        if (!ProcurementReceiptInspectionRules.CanSubmit(inspection.Status))
            throw Conflict("RCV_INSPECTION_NOT_SUBMITTABLE", "Only a Draft inspection can be submitted.");
        if (inspection.PendingQuantity != 0)
            throw Validation("RCV_INSPECTION_PENDING_QUANTITY",
                "Every received quantity must be accepted or rejected before submission.");
        if (inspection.RejectedQuantity > 0 && inspection.Lines.Any(item =>
                item.RejectedQuantity > 0 && (string.IsNullOrWhiteSpace(item.RejectionReason) ||
                                               !item.QuarantineLocationId.HasValue)))
            throw Validation("RCV_REJECTION_CONTROL_INCOMPLETE",
                "Every rejected line requires a reason and quarantine location.");

        var evidence = await ValidateEvidenceAsync(caseId, request.Evidence, cancellationToken);
        var before = Snapshot(inspection);
        await ExecuteAsync(async () =>
        {
            inspection = await LoadCaseAsync(caseId, cancellationToken);
            EnsureRowVersion(inspection.RowVersion, request.RowVersion);
            if (!ProcurementReceiptInspectionRules.CanSubmit(inspection.Status))
                throw Conflict("RCV_INSPECTION_NOT_SUBMITTABLE",
                    "The inspection changed before workflow startup. Reload and retry.");

            foreach (var item in evidence)
            {
                item.InspectionCaseId = inspection.Id;
                await Evidence.AddAsync(item);
                inspection.Evidence.Add(item);
            }
            inspection.Status = ProcurementReceiptInspectionStatus.PendingApproval;
            inspection.SubmittedByUserId = _currentUser.UserId;
            inspection.SubmittedAtUtc = DateTime.UtcNow;
            inspection.DecisionComment = request.Comment.Trim();
            inspection.CorrelationId = correlation;
            inspection.IntegrityHash = CaseHash(inspection);
            await Cases.UpdateAsync(inspection);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var workflow = await _workflow.SubmitAsync(
                WorkflowEntityType, inspection.Id, inspection.WorkflowDefinitionId);
            if (!workflow.ExecutionResult.Success)
                throw Conflict("RCV_INSPECTION_WORKFLOW_START_FAILED",
                    workflow.ExecutionResult.Message ?? "The configured receipt-inspection workflow could not be started.");

            inspection.WorkflowInstanceId = workflow.ExecutionResult.WorkflowInstanceId;
            await AddActionAsync(inspection, ProcurementReceiptInspectionActionType.Submitted,
                request.Comment, "Submitted for independent approval", correlation,
                $"submit:{inspection.Id:N}:{inspection.Sequence}", null, cancellationToken);
            inspection.IntegrityHash = CaseHash(inspection);
            await Cases.UpdateAsync(inspection);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        await RecordEventAsync(inspection, "Submitted", ProcurementControlEventResult.ReviewRequired,
            before, Snapshot(inspection), request.Comment, correlation, cancellationToken);
        await PublishAsync("procurement.receipt-inspection.submitted", inspection, cancellationToken);
        return Map(inspection);
    }

    public async Task<ProcurementReceiptInspectionDto> DecideAsync(
        Guid caseId,
        DecideProcurementReceiptInspectionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var inspection = await LoadCaseAsync(caseId, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, inspection.PurchaseOrderReceipt,
            correlation, cancellationToken);
        EnsureRowVersion(inspection.RowVersion, request.RowVersion);
        if (!ProcurementReceiptInspectionRules.CanDecide(inspection.Status))
            throw Conflict("RCV_INSPECTION_NOT_DECIDABLE", "Only a Pending Approval inspection can be decided.");
        if (!inspection.SubmittedByUserId.HasValue)
            throw Conflict("RCV_INSPECTION_SUBMITTER_MISSING", "The inspection submitter was not retained.");
        if (request.Approved)
        {
            await _purchaseOrderSod.EnforceReceiptActionAsync(
                inspection.PurchaseOrderReceipt.PurchaseOrder,
                ProcurementPurchaseOrderSodRules.ApproveReceiptInspection,
                correlation,
                cancellationToken);
        }
        await _sod.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = EventType,
            SourceReference = inspection.PurchaseOrderReceipt.ReceiptNumber,
            ProhibitedActorUserIds = new List<Guid>
            {
                inspection.CreatedByUserId,
                inspection.SubmittedByUserId.Value,
                inspection.PurchaseOrderReceipt.ReceivedById ?? Guid.Empty
            }.Where(item => item != Guid.Empty).Distinct().ToList()
        }, correlation, cancellationToken);
        if (!await _workflow.CanUserApproveAsync(
                WorkflowEntityType, inspection.Id, _currentUser.UserId))
            throw new ProcurementReceiptInspectionAuthorizationException(
                "The current actor is not eligible for the active shared-workflow step.");

        var workflow = await _workflow.ProcessApprovalAsync(
            WorkflowEntityType, inspection.Id, _currentUser.UserId,
            request.Approved ? "Approve" : "Reject", request.Comment);
        if (!workflow.ExecutionResult.Success)
            throw Conflict("RCV_INSPECTION_WORKFLOW_DECISION_FAILED",
                workflow.ExecutionResult.Message ?? "The shared workflow decision failed.");
        if (request.Approved && workflow.Outcome == WorkflowOutcome.Rejected)
            throw Conflict("RCV_INSPECTION_WORKFLOW_REJECTED", "The shared workflow rejected the inspection.");
        if (!request.Approved && workflow.Outcome == WorkflowOutcome.Approved)
            throw Conflict("RCV_INSPECTION_WORKFLOW_ALREADY_APPROVED",
                "An approved shared workflow cannot be recorded as rejected.");
        if (workflow.Outcome == WorkflowOutcome.Pending)
            return Map(await LoadCaseAsync(caseId, cancellationToken));

        ProcurementReceiptInspectionDto? result = null;
        await ExecuteAsync(async () =>
        {
            inspection = await LoadCaseAsync(caseId, cancellationToken);
            if (!ProcurementReceiptInspectionRules.CanDecide(inspection.Status))
                throw Conflict("RCV_INSPECTION_NOT_DECIDABLE", "The inspection decision changed. Reload and retry.");
            var before = Snapshot(inspection);
            inspection.DecidedByUserId = _currentUser.UserId;
            inspection.DecidedAtUtc = DateTime.UtcNow;
            inspection.DecisionComment = request.Comment.Trim();
            inspection.CorrelationId = correlation;
            if (!request.Approved)
            {
                inspection.Status = ProcurementReceiptInspectionStatus.Rejected;
                await AddActionAsync(inspection, ProcurementReceiptInspectionActionType.Rejected,
                    request.Comment, "Inspection workflow rejected", correlation,
                    $"decision:{inspection.Id:N}:reject", null, cancellationToken);
                inspection.IntegrityHash = CaseHash(inspection);
                await Cases.UpdateAsync(inspection);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(inspection, "Rejected", ProcurementControlEventResult.Rejected,
                    before, Snapshot(inspection), request.Comment, correlation, cancellationToken);
                result = Map(inspection);
                return;
            }

            await _sourceControl.RevalidatePurchaseOrderReceiptAsync(
                inspection.PurchaseOrderReceiptId, "ApproveReceiptInspection", correlation,
                cancellationToken);
            await _store.SetMutationContextAsync(inspection.Id, cancellationToken);
            try
            {
                await ApplyAcceptedQuantitiesAndStockAsync(inspection, cancellationToken);
                var hasRejection = inspection.RejectedQuantity > 0;
                inspection.Status = hasRejection
                    ? ProcurementReceiptInspectionStatus.QualityHold
                    : ProcurementReceiptInspectionStatus.Closed;
                inspection.QualityHold = hasRejection;
                inspection.QualityHoldReason = hasRejection
                    ? "Rejected quantities remain quarantined until supplier acknowledgement and return/replacement closure."
                    : null;
                inspection.SupplierAcknowledgementStatus = hasRejection
                    ? ProcurementReceiptSupplierAcknowledgementStatus.Pending
                    : ProcurementReceiptSupplierAcknowledgementStatus.NotRequired;
                inspection.ResolutionKind = ProcurementReceiptResolutionKind.None;
                inspection.ResolutionStatus = hasRejection
                    ? ProcurementReceiptResolutionStatus.Required
                    : ProcurementReceiptResolutionStatus.NotRequired;
                inspection.StockEligibleQuantity = inspection.AcceptedQuantity;
                inspection.StockPostedQuantity = inspection.AcceptedQuantity;
                inspection.StockPostedAtUtc = inspection.AcceptedQuantity > 0 ? DateTime.UtcNow : null;
                inspection.ApEligibleQuantity = inspection.AcceptedQuantity;
                inspection.ApBlockedQuantity = inspection.RejectedQuantity;
                inspection.PurchaseOrderReceipt.Status = inspection.AcceptedQuantity == 0
                    ? "Rejected"
                    : hasRejection ? "Partially Accepted" : "Accepted";
                inspection.PurchaseOrderReceipt.InspectionResult = inspection.AcceptedQuantity == 0
                    ? "Failed"
                    : hasRejection ? "Conditional" : "Passed";
                inspection.PurchaseOrderReceipt.InspectionDate = DateTime.UtcNow;
                inspection.PurchaseOrderReceipt.InspectedById = _currentUser.UserId;
                inspection.PurchaseOrderReceipt.InspectionNotes = request.Comment.Trim();
                if (hasRejection)
                {
                    inspection.RejectionNoteNumber =
                        $"RN-{inspection.PurchaseOrderReceipt.ReceiptNumber}-{inspection.Sequence:00}";
                    await AddActionAsync(inspection,
                        ProcurementReceiptInspectionActionType.RejectionNoteIssued,
                        "Formal rejection note issued for quarantined quantities.",
                        inspection.RejectionNoteNumber, correlation,
                        $"rejection-note:{inspection.Id:N}", null, cancellationToken);
                }
                await AddActionAsync(inspection, ProcurementReceiptInspectionActionType.Approved,
                    request.Comment, hasRejection ? "Accepted with quality hold" : "Accepted and closed",
                    correlation, $"decision:{inspection.Id:N}:approve", null, cancellationToken);
                inspection.IntegrityHash = CaseHash(inspection);
                await Cases.UpdateAsync(inspection);
                await _unitOfWork.Repository<PurchaseOrderReceipt>()
                    .UpdateAsync(inspection.PurchaseOrderReceipt);
                await UpdatePurchaseOrderStatusAsync(inspection.PurchaseOrderReceipt.PurchaseOrder,
                    cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                await _store.ClearMutationContextAsync(cancellationToken);
            }
            await RecordEventAsync(inspection, "Approved", ProcurementControlEventResult.Allowed,
                before, Snapshot(inspection), request.Comment, correlation, cancellationToken);
            result = Map(inspection);
        }, cancellationToken);
        await PublishAsync(inspection.QualityHold
                ? "procurement.receipt-inspection.quality-hold"
                : "procurement.receipt-inspection.accepted",
            inspection, cancellationToken);
        return result!;
    }

    public async Task<ProcurementReceiptInspectionDto> AcknowledgeAsync(
        Guid caseId,
        ProcurementReceiptSupplierAcknowledgementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var inspection = await LoadCaseAsync(caseId, cancellationToken);
        var partner = await EnsureLinkedSupplierAsync(
            inspection.PurchaseOrderReceipt.PurchaseOrder.BusinessPartnerId,
            cancellationToken);
        EnsureRowVersion(inspection.RowVersion, request.RowVersion);
        if (!inspection.QualityHold || inspection.SupplierAcknowledgementStatus !=
            ProcurementReceiptSupplierAcknowledgementStatus.Pending)
            throw Conflict("RCV_SUPPLIER_ACK_NOT_AVAILABLE",
                "Supplier acknowledgement is available only for an active rejected-quantity quality hold.");
        var evidence = await ValidateEvidenceAsync(caseId, request.Evidence, cancellationToken);
        if (request.Acknowledged && evidence.Count == 0)
            throw Validation("RCV_SUPPLIER_ACK_EVIDENCE_REQUIRED",
                "Acknowledgement requires controlled supplier evidence.");
        foreach (var item in evidence)
        {
            item.InspectionCaseId = inspection.Id;
            await Evidence.AddAsync(item);
            inspection.Evidence.Add(item);
        }
        inspection.SupplierAcknowledgementStatus = request.Acknowledged
            ? ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged
            : ProcurementReceiptSupplierAcknowledgementStatus.Disputed;
        await AddActionAsync(inspection,
            request.Acknowledged
                ? ProcurementReceiptInspectionActionType.SupplierAcknowledged
                : ProcurementReceiptInspectionActionType.SupplierDisputed,
            request.Comment, request.Reference, correlation, request.IdempotencyKey,
            partner.Id, cancellationToken);
        inspection.IntegrityHash = CaseHash(inspection);
        await Cases.UpdateAsync(inspection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(inspection,
            request.Acknowledged ? "SupplierAcknowledged" : "SupplierDisputed",
            request.Acknowledged ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.ReviewRequired,
            null, Snapshot(inspection), request.Comment, correlation, cancellationToken);
        await PublishAsync("procurement.receipt-inspection.supplier-response", inspection,
            cancellationToken);
        return Map(inspection);
    }

    public async Task<ProcurementReceiptInspectionDto> ResolveAsync(
        Guid caseId,
        ProcurementReceiptResolutionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var inspection = await LoadCaseAsync(caseId, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, inspection.PurchaseOrderReceipt,
            correlation, cancellationToken);
        EnsureRowVersion(inspection.RowVersion, request.RowVersion);
        if (!ProcurementReceiptInspectionRules.CanResolve(inspection.Status))
            throw Conflict("RCV_RESOLUTION_NOT_AVAILABLE", "The receipt has no active quality-hold resolution.");
        if (inspection.SupplierAcknowledgementStatus !=
            ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged)
            throw Conflict("RCV_SUPPLIER_ACK_REQUIRED",
                "The linked supplier must acknowledge the rejection note before return or replacement progression.");
        if (request.ResolutionKind == ProcurementReceiptResolutionKind.None)
            throw Validation("RCV_RESOLUTION_KIND_REQUIRED", "Return or Replacement must be selected.");
        if (inspection.ResolutionKind != ProcurementReceiptResolutionKind.None &&
            inspection.ResolutionKind != request.ResolutionKind)
            throw Conflict("RCV_RESOLUTION_KIND_IMMUTABLE",
                "The selected return/replacement route cannot be changed after progression begins.");
        if (request.ResolutionKind == ProcurementReceiptResolutionKind.Replacement &&
            inspection.ResolutionStatus ==
            ProcurementReceiptResolutionStatus.ReplacementRequested)
        {
            await _purchaseOrderSod.EnforceReceiptActionAsync(
                inspection.PurchaseOrderReceipt.PurchaseOrder,
                ProcurementPurchaseOrderSodRules.ConfirmReplacementReceipt,
                correlation,
                cancellationToken);
        }
        var evidence = await ValidateEvidenceAsync(caseId, request.Evidence, cancellationToken);
        if (evidence.Count == 0)
            throw Validation("RCV_RESOLUTION_EVIDENCE_REQUIRED",
                "Every return/replacement progression requires controlled evidence.");
        foreach (var item in evidence)
        {
            item.InspectionCaseId = inspection.Id;
            await Evidence.AddAsync(item);
            inspection.Evidence.Add(item);
        }
        inspection.ResolutionKind = request.ResolutionKind;
        ProcurementReceiptInspectionActionType action;
        if (request.ResolutionKind == ProcurementReceiptResolutionKind.Return)
        {
            if (inspection.ResolutionStatus == ProcurementReceiptResolutionStatus.Required)
            {
                inspection.ResolutionStatus = ProcurementReceiptResolutionStatus.Authorized;
                inspection.Status = ProcurementReceiptInspectionStatus.ReturnPending;
                action = ProcurementReceiptInspectionActionType.ReturnAuthorized;
            }
            else if (inspection.ResolutionStatus == ProcurementReceiptResolutionStatus.Authorized)
            {
                inspection.ResolutionStatus = ProcurementReceiptResolutionStatus.Dispatched;
                inspection.Status = ProcurementReceiptInspectionStatus.ClosureReady;
                action = ProcurementReceiptInspectionActionType.ReturnDispatched;
            }
            else
                throw Conflict("RCV_RETURN_ALREADY_DISPATCHED", "The return route has already reached dispatch.");
        }
        else
        {
            if (inspection.ResolutionStatus == ProcurementReceiptResolutionStatus.Required)
            {
                inspection.ResolutionStatus = ProcurementReceiptResolutionStatus.ReplacementRequested;
                inspection.Status = ProcurementReceiptInspectionStatus.ReplacementPending;
                action = ProcurementReceiptInspectionActionType.ReplacementRequested;
            }
            else if (inspection.ResolutionStatus == ProcurementReceiptResolutionStatus.ReplacementRequested)
            {
                await ValidateReplacementReceiptAsync(inspection, request.Reference, cancellationToken);
                inspection.ResolutionStatus = ProcurementReceiptResolutionStatus.ReplacementReceived;
                inspection.Status = ProcurementReceiptInspectionStatus.ClosureReady;
                action = ProcurementReceiptInspectionActionType.ReplacementReceived;
            }
            else
                throw Conflict("RCV_REPLACEMENT_ALREADY_RECEIVED", "The replacement route has already been completed.");
        }
        await AddActionAsync(inspection, action, request.Comment, request.Reference,
            correlation, request.IdempotencyKey, null, cancellationToken);
        inspection.IntegrityHash = CaseHash(inspection);
        await Cases.UpdateAsync(inspection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(inspection, action.ToString(), ProcurementControlEventResult.Allowed,
            null, Snapshot(inspection), request.Comment, correlation, cancellationToken);
        return Map(inspection);
    }

    public async Task<ProcurementReceiptInspectionDto> CloseAsync(
        Guid caseId,
        ProcurementReceiptResolutionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var inspection = await LoadCaseAsync(caseId, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, inspection.PurchaseOrderReceipt,
            correlation, cancellationToken);
        await _purchaseOrderSod.EnforceReceiptActionAsync(
            inspection.PurchaseOrderReceipt.PurchaseOrder,
            ProcurementPurchaseOrderSodRules.CloseReceiptInspection,
            correlation,
            cancellationToken);
        EnsureRowVersion(inspection.RowVersion, request.RowVersion);
        if (inspection.Status != ProcurementReceiptInspectionStatus.ClosureReady ||
            !ProcurementReceiptInspectionRules.CanClose(
                inspection.SupplierAcknowledgementStatus, inspection.ResolutionStatus))
            throw Conflict("RCV_CLOSURE_NOT_READY",
                "Supplier acknowledgement and evidenced return dispatch or accepted replacement are required before closure.");
        if (inspection.DecidedByUserId == _currentUser.UserId ||
            inspection.CreatedByUserId == _currentUser.UserId)
            throw new ProcurementReceiptInspectionAuthorizationException(
                "Quality-hold closure requires an actor independent from inspection creation and approval.");
        var evidence = await ValidateEvidenceAsync(caseId, request.Evidence, cancellationToken);
        if (evidence.Count == 0)
            throw Validation("RCV_CLOSURE_EVIDENCE_REQUIRED", "Closure evidence is required.");
        foreach (var item in evidence)
        {
            item.InspectionCaseId = inspection.Id;
            await Evidence.AddAsync(item);
            inspection.Evidence.Add(item);
        }
        inspection.Status = ProcurementReceiptInspectionStatus.Closed;
        inspection.ResolutionStatus = ProcurementReceiptResolutionStatus.Closed;
        inspection.QualityHold = false;
        inspection.QualityHoldReleasedAtUtc = DateTime.UtcNow;
        await AddActionAsync(inspection, ProcurementReceiptInspectionActionType.Closed,
            request.Comment, request.Reference, correlation, request.IdempotencyKey,
            null, cancellationToken);
        inspection.IntegrityHash = CaseHash(inspection);
        await Cases.UpdateAsync(inspection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(inspection, "Closed", ProcurementControlEventResult.Allowed,
            null, Snapshot(inspection), request.Comment, correlation, cancellationToken);
        await PublishAsync("procurement.receipt-inspection.closed", inspection,
            cancellationToken);
        return Map(inspection);
    }

    public async Task EnsureApEligibilityAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var receipts = await _unitOfWork.Repository<PurchaseOrderReceipt>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.PurchaseOrderId == purchaseOrderId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (receipts.Count == 0)
            throw Conflict("RCV_AP_RECEIPT_MISSING", "No governed receipt exists for the purchase order.");
        var receiptIds = receipts.Select(item => item.Id).ToList();
        var latest = await Cases.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                receiptIds.Contains(item.PurchaseOrderReceiptId) && !item.IsDeleted)
            .AsNoTracking().GroupBy(item => item.PurchaseOrderReceiptId)
            .Select(group => group.OrderByDescending(item => item.Sequence).First())
            .ToListAsync(cancellationToken);
        if (latest.Count != receipts.Count || latest.Any(item =>
                !ProcurementReceiptInspectionRules.IsApEligible(
                    item.Status, item.PendingQuantity, item.ApEligibleQuantity)))
            throw Conflict("RCV_AP_INSPECTION_NOT_ELIGIBLE",
                "AP matching is blocked until a governed inspection approves accepted quantities.");
    }

    private async Task ApplyAcceptedQuantitiesAndStockAsync(
        ProcurementReceiptInspectionCase inspection,
        CancellationToken cancellationToken)
    {
        foreach (var line in inspection.Lines)
        {
            var receiptLine = line.PurchaseOrderReceiptItem;
            var poLine = receiptLine.PurchaseOrderItem;
            receiptLine.AcceptedQuantity = line.AcceptedQuantity;
            receiptLine.RejectedQuantity = line.RejectedQuantity;
            receiptLine.RejectionReason = line.RejectionReason;
            receiptLine.QualityNotes = line.InspectionNotes;
            receiptLine.QualityStatus = line.Disposition switch
            {
                ProcurementReceiptDisposition.Accepted => "Passed",
                ProcurementReceiptDisposition.Rejected => "Failed",
                ProcurementReceiptDisposition.PartiallyAccepted => "Conditional",
                _ => "Pending"
            };
            receiptLine.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<PurchaseOrderReceiptItem>().UpdateAsync(receiptLine);
            if (line.AcceptedQuantity <= 0) continue;
            poLine.ReceivedQuantity += line.AcceptedQuantity;
            poLine.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<PurchaseOrderItem>().UpdateAsync(poLine);
            await PostStockAsync(inspection.PurchaseOrderReceipt, receiptLine, poLine,
                line.AcceptedQuantity, cancellationToken);
        }
        // Persist the governed receipt acceptance and stock movement first so
        // the linked GRN projection trigger can validate against durable values
        // inside this same serializable transaction.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SynchronizeLinkedGoodsReceiptNoteAsync(inspection, cancellationToken);
    }

    private async Task SynchronizeLinkedGoodsReceiptNoteAsync(
        ProcurementReceiptInspectionCase inspection,
        CancellationToken cancellationToken)
    {
        var grn = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderReceiptId == inspection.PurchaseOrderReceiptId &&
                !item.IsDeleted)
            .Include(item => item.Items)
            .SingleOrDefaultAsync(cancellationToken);
        if (grn is null) return;

        foreach (var receiptLine in inspection.PurchaseOrderReceipt.Items)
        {
            var grnLine = grn.Items.SingleOrDefault(item =>
                item.PurchaseOrderItemId == receiptLine.PurchaseOrderItemId &&
                !item.IsDeleted);
            if (grnLine is null)
                throw Conflict("RCV_GRN_PROJECTION_INCOMPLETE",
                    "The linked GRN does not contain every governed purchase-order receipt line.");
            var conversion = 1m;
            if (receiptLine.ItemUnitOfMeasureId.HasValue)
            {
                conversion = await _unitOfWork.Repository<ItemUnitOfMeasure>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == receiptLine.ItemUnitOfMeasureId.Value &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .Select(item => item.ConversionToBase)
                    .SingleOrDefaultAsync(cancellationToken);
                if (conversion <= 0) conversion = 1m;
            }
            grnLine.AcceptedQuantity = receiptLine.AcceptedQuantity * conversion;
            grnLine.RejectedQuantity = receiptLine.RejectedQuantity * conversion;
            grnLine.RejectionReason = receiptLine.RejectionReason;
            grnLine.InspectionNotes = receiptLine.QualityNotes;
            grnLine.InspectionResult = receiptLine.QualityStatus switch
            {
                "Passed" => InspectionResult.Passed,
                "Failed" => InspectionResult.Failed,
                "Conditional" => InspectionResult.ConditionalPass,
                _ => InspectionResult.Pending
            };
            grnLine.UpdatedAt = DateTime.UtcNow;
            grnLine.UpdatedBy = ActorName;
            grnLine.LastModifiedById = _currentUser.UserId;
            await _unitOfWork.Repository<GoodsReceiptNoteItem>().UpdateAsync(grnLine);
        }

        grn.TotalQuantityAccepted = grn.Items.Sum(item => item.AcceptedQuantity);
        grn.TotalQuantityRejected = grn.Items.Sum(item => item.RejectedQuantity);
        grn.TotalValue = grn.Items.Sum(item => item.AcceptedQuantity * item.UnitCost);
        grn.InspectionResult = grn.TotalQuantityRejected == 0
            ? InspectionResult.Passed
            : grn.TotalQuantityAccepted == 0
                ? InspectionResult.Failed
                : InspectionResult.ConditionalPass;
        grn.Status = GRNStatus.StockUpdated;
        grn.InspectionDate = DateTime.UtcNow;
        grn.InspectedById = _currentUser.UserId;
        grn.StockUpdated = true;
        grn.StockUpdatedAt = DateTime.UtcNow;
        grn.UpdatedAt = DateTime.UtcNow;
        grn.UpdatedBy = ActorName;
        grn.LastModifiedById = _currentUser.UserId;
        await _unitOfWork.Repository<GoodsReceiptNote>().UpdateAsync(grn);
    }

    private async Task PostStockAsync(
        PurchaseOrderReceipt receipt,
        PurchaseOrderReceiptItem receiptLine,
        PurchaseOrderItem poLine,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        if (!poLine.InventoryItemId.HasValue || poLine.InventoryItemId == Guid.Empty) return;
        if (!receiptLine.LocationId.HasValue || receiptLine.LocationId == Guid.Empty)
            throw Validation("RCV_STOCK_LOCATION_REQUIRED", "Accepted stock requires a governed receipt location.");
        var location = await _unitOfWork.Repository<WarehouseLocation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == receiptLine.LocationId.Value && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("RCV_STOCK_LOCATION_INVALID", "The accepted-stock location was not found in the current tenant.");
        var conversion = 1m;
        if (receiptLine.ItemUnitOfMeasureId.HasValue)
        {
            var uom = await _unitOfWork.Repository<ItemUnitOfMeasure>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                      item.Id == receiptLine.ItemUnitOfMeasureId.Value && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (uom is not null && uom.ConversionToBase > 0) conversion = uom.ConversionToBase;
        }
        var baseQuantity = quantity * conversion;
        var purchaseCost = poLine.LandedUnitCost > 0 ? poLine.LandedUnitCost : poLine.UnitPrice;
        var baseCost = purchaseCost / conversion;
        var inventoryItem = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == poLine.InventoryItemId.Value && !item.IsDeleted)
            .SingleAsync(cancellationToken);
        await _valuation.ProcessReceiptAsync(poLine.InventoryItemId.Value,
            location.InventoryWarehouseId, receiptLine.LocationId, baseQuantity, baseCost,
            ReferenceType.PO, receipt.ReceiptNumber, receipt.Id,
            receiptLine.LotNumber, receiptLine.SerialNumber, receiptLine.ExpirationDate);

        var valuationBalance = await _unitOfWork.Repository<InventoryBalance>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.InventoryItemId == poLine.InventoryItemId.Value &&
                                  item.WarehouseId == location.InventoryWarehouseId &&
                                  item.LocationId == receiptLine.LocationId.Value &&
                                  !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);
        var postedAverageCost = valuationBalance?.AverageUnitCost ??
                                (inventoryItem.ValuationMethod == ValuationMethod.StandardCost
                                    ? inventoryItem.StandardCost
                                    : baseCost);
        var receiptValuationCost = inventoryItem.ValuationMethod == ValuationMethod.StandardCost
            ? inventoryItem.StandardCost
            : baseCost;

        var inventoryLocation = await _unitOfWork.Repository<InventoryLocation>()
            .FirstOrDefaultAsync(item => item.TenantId == _currentUser.TenantId &&
                                         item.InventoryItemId == poLine.InventoryItemId.Value &&
                                         item.LocationId == receiptLine.LocationId.Value);
        if (inventoryLocation is null)
        {
            inventoryLocation = new InventoryLocation
            {
                TenantId = _currentUser.TenantId,
                InventoryItemId = poLine.InventoryItemId.Value,
                LocationId = receiptLine.LocationId.Value,
                Quantity = baseQuantity,
                AvailableQuantity = baseQuantity,
                AverageCost = postedAverageCost,
                LastMovementDate = DateTime.UtcNow,
                CreatedById = _currentUser.UserId
            };
            await _unitOfWork.Repository<InventoryLocation>().AddAsync(inventoryLocation);
        }
        else
        {
            inventoryLocation.Quantity += baseQuantity;
            inventoryLocation.AvailableQuantity += baseQuantity;
            inventoryLocation.AverageCost = postedAverageCost;
            inventoryLocation.LastMovementDate = DateTime.UtcNow;
            await _unitOfWork.Repository<InventoryLocation>().UpdateAsync(inventoryLocation);
        }
        var warehouseQuantity = await _unitOfWork.Repository<WarehouseQuantity>()
            .FirstOrDefaultAsync(item => item.TenantId == _currentUser.TenantId &&
                                         item.InventoryItemId == poLine.InventoryItemId.Value &&
                                         item.WarehouseId == location.InventoryWarehouseId);
        if (warehouseQuantity is null)
        {
            warehouseQuantity = new WarehouseQuantity
            {
                TenantId = _currentUser.TenantId,
                InventoryItemId = poLine.InventoryItemId.Value,
                WarehouseId = location.InventoryWarehouseId,
                CurrentStock = baseQuantity,
                AvailableStock = baseQuantity,
                AverageCost = postedAverageCost,
                LastMovementDate = DateTime.UtcNow,
                CreatedById = _currentUser.UserId
            };
            await _unitOfWork.Repository<WarehouseQuantity>().AddAsync(warehouseQuantity);
        }
        else
        {
            var priorWarehouseStock = warehouseQuantity.CurrentStock;
            warehouseQuantity.AverageCost =
                ProcurementReceiptInspectionRules.CalculateWeightedAverageCost(
                    priorWarehouseStock,
                    warehouseQuantity.AverageCost,
                    baseQuantity,
                    receiptValuationCost);
            warehouseQuantity.CurrentStock += baseQuantity;
            warehouseQuantity.AvailableStock += baseQuantity;
            warehouseQuantity.LastMovementDate = DateTime.UtcNow;
            await _unitOfWork.Repository<WarehouseQuantity>().UpdateAsync(warehouseQuantity);
        }
        var warehouse = await _unitOfWork.Repository<Warehouse>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == location.InventoryWarehouseId && !item.IsDeleted)
            .AsNoTracking().SingleAsync(cancellationToken);
        if (!warehouse.IsConsignmentWarehouse)
        {
            inventoryItem.CurrentStock += baseQuantity;
            inventoryItem.AvailableStock += baseQuantity;
            inventoryItem.LastPurchaseCost = baseCost;
            inventoryItem.LastPurchaseDate = DateTime.UtcNow;
            await _unitOfWork.Repository<InventoryItem>().UpdateAsync(inventoryItem);
        }
    }

    private async Task UpdatePurchaseOrderStatusAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken)
    {
        var wasFullyReceived = string.Equals(
            purchaseOrder.Status,
            "Received",
            StringComparison.OrdinalIgnoreCase);
        var lines = await _unitOfWork.Repository<PurchaseOrderItem>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.PurchaseOrderId == purchaseOrder.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var fullyAccepted = lines.Count > 0 && lines.All(item =>
            item.ReceivedQuantity >= item.OrderedQuantity);
        purchaseOrder.Status = fullyAccepted ? "Received" : "Partially Received";
        purchaseOrder.ReceivedDate = fullyAccepted ? DateTime.UtcNow : purchaseOrder.ReceivedDate;
        purchaseOrder.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Repository<PurchaseOrder>().UpdateAsync(purchaseOrder);
        if (fullyAccepted && !wasFullyReceived)
        {
            await _budgetService.UtilizePurchaseOrderCommittedBudgetAsync(
                purchaseOrder.Id,
                purchaseOrder.TotalAmount);
        }
    }

    private async Task ValidateReplacementReceiptAsync(
        ProcurementReceiptInspectionCase original,
        string reference,
        CancellationToken cancellationToken)
    {
        var value = reference.Trim();
        var hasReceiptId = Guid.TryParse(value, out var receiptId);
        var replacement = await _unitOfWork.Repository<PurchaseOrderReceipt>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id != original.PurchaseOrderReceiptId &&
                                  item.PurchaseOrderId == original.PurchaseOrderReceipt.PurchaseOrderId &&
                                  (item.ReceiptNumber == value ||
                                   (hasReceiptId && item.Id == receiptId)) &&
                                  !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("RCV_REPLACEMENT_RECEIPT_NOT_FOUND",
                "The replacement reference must identify another governed receipt for the same purchase order.");
        var accepted = await Cases.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderReceiptId == replacement.Id && !item.IsDeleted &&
                item.Status == ProcurementReceiptInspectionStatus.Closed)
            .AsNoTracking().OrderByDescending(item => item.Sequence)
            .Select(item => (decimal?)item.AcceptedQuantity)
            .FirstOrDefaultAsync(cancellationToken);
        if (!accepted.HasValue || accepted.Value < original.RejectedQuantity)
            throw Conflict("RCV_REPLACEMENT_NOT_ACCEPTED",
                "The replacement receipt must be independently inspected and accept at least the rejected quantity.");
    }

    private async Task EnsureQuarantineLocationAsync(
        Guid locationId,
        PurchaseOrderReceipt receipt,
        CancellationToken cancellationToken)
    {
        var location = await _unitOfWork.Repository<WarehouseLocation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == locationId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("RCV_QUARANTINE_LOCATION_INVALID",
                "The quarantine location was not found in the current tenant.");
        if (!receipt.Items.Any(item => item.LocationId.HasValue))
            throw Validation("RCV_QUARANTINE_LOCATION_INVALID",
                "The quarantine location is outside the governed receipt warehouse.");
        var receiptWarehouseIds = await _unitOfWork.Repository<WarehouseLocation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  receipt.Items.Where(line => line.LocationId.HasValue)
                                      .Select(line => line.LocationId!.Value).Contains(item.Id))
            .Select(item => item.InventoryWarehouseId).Distinct().ToListAsync(cancellationToken);
        if (!receiptWarehouseIds.Contains(location.InventoryWarehouseId))
            throw Validation("RCV_QUARANTINE_WAREHOUSE_MISMATCH",
                "The quarantine location must belong to the receipt warehouse.");
    }

    private async Task<List<ProcurementReceiptInspectionEvidence>> ValidateEvidenceAsync(
        Guid caseId,
        IEnumerable<ProcurementReceiptInspectionEvidenceRequest> requests,
        CancellationToken cancellationToken)
    {
        var rows = requests.ToList();
        if (rows.GroupBy(item => $"{item.ActionKey.Trim()}|{item.RequirementKey.Trim()}",
                StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw Validation("RCV_EVIDENCE_DUPLICATE", "Each action evidence requirement may be linked only once.");
        var result = new List<ProcurementReceiptInspectionEvidence>();
        foreach (var request in rows)
        {
            if (string.IsNullOrWhiteSpace(request.ActionKey) ||
                string.IsNullOrWhiteSpace(request.RequirementKey) ||
                string.IsNullOrWhiteSpace(request.EvidenceReference))
                throw Validation("RCV_EVIDENCE_REQUIRED", "Evidence action, requirement, and reference are required.");
            Guid? workflowId = null;
            Guid? uploadId = null;
            string evidenceHash;
            if (request.ReferenceKind == ProcurementReceiptInspectionEvidenceKind.WorkflowEvidenceDocument)
            {
                if (!request.WorkflowEvidenceDocumentId.HasValue || request.FileUploadRecordId.HasValue)
                    throw Validation("RCV_WORKFLOW_EVIDENCE_INVALID",
                        "Workflow evidence requires exactly one workflow evidence document ID.");
                var evidence = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                    .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                          item.Id == request.WorkflowEvidenceDocumentId.Value && !item.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("RCV_EVIDENCE_NOT_FOUND", "Workflow evidence was not found in the current tenant.");
                if (!evidence.IsCurrent ||
                    evidence.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified ||
                    evidence.MalwareScanStatus != WorkflowMalwareScanStatus.Clean)
                    throw Conflict("RCV_EVIDENCE_NOT_VERIFIED",
                        "Workflow evidence must be current, verified, and malware-clean.");
                workflowId = evidence.Id;
                evidenceHash = evidence.Sha256;
            }
            else
            {
                if (!request.FileUploadRecordId.HasValue || request.WorkflowEvidenceDocumentId.HasValue)
                    throw Validation("RCV_DMS_EVIDENCE_INVALID",
                        "Central-DMS evidence requires exactly one controlled upload ID.");
                var version = await _unitOfWork.Repository<CentralDocumentVersion>()
                    .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                          item.FileUploadRecordId == request.FileUploadRecordId && !item.IsDeleted)
                    .Include(item => item.DocumentRecord)
                    .AsNoTracking().OrderByDescending(item => item.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("RCV_DMS_EVIDENCE_NOT_FOUND",
                        "The upload is not retained by the central document repository.");
                var upload = await _unitOfWork.Repository<FileUploadRecord>()
                    .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                          item.Id == request.FileUploadRecordId && !item.IsDeleted)
                    .AsNoTracking().SingleAsync(cancellationToken);
                if (upload.VirusScanStatus != FileVirusScanStatus.Clean)
                    throw Conflict("RCV_DMS_EVIDENCE_UNSAFE",
                        "Central-DMS evidence must have a completed Clean malware scan.");
                uploadId = upload.Id;
                evidenceHash = Hash(Serialize(new
                {
                    version.Id,
                    version.DocumentRecordId,
                    version.VersionNumber,
                    UploadId = upload.Id,
                    upload.FilePath,
                    upload.FileSize
                }));
            }
            result.Add(new ProcurementReceiptInspectionEvidence
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                InspectionCaseId = caseId,
                ActionKey = request.ActionKey.Trim(),
                RequirementKey = request.RequirementKey.Trim(),
                ReferenceKind = request.ReferenceKind,
                WorkflowEvidenceDocumentId = workflowId,
                FileUploadRecordId = uploadId,
                EvidenceReference = request.EvidenceReference.Trim(),
                EvidenceHash = evidenceHash,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            });
        }
        return result;
    }

    private async Task<Governance> ResolveGovernanceAsync(
        PurchaseOrderReceipt receipt,
        string correlation,
        CancellationToken cancellationToken)
    {
        var profile = await _configuration.GetEffectiveProfileAsync(
            "TDC-PROCUREMENT", DateTime.UtcNow, cancellationToken)
            ?? throw Validation("RCV_CONFIGURATION_MISSING",
                "No effective Published TDC procurement configuration profile exists.");
        if (profile.Decisions.Count != 14 || profile.Decisions.Any(item => !item.IsComplete))
            throw Validation("RCV_CONFIGURATION_INCOMPLETE",
                "The effective configuration must contain fourteen complete approved decisions.");
        var purchaseOrder = receipt.PurchaseOrder;
        var authority = await _compliance.EvaluateAuthorityRouteAsync(
            new ProcurementAuthorityRouteDecisionRequest
            {
                Category = ProcurementCategoryClass.Goods,
                Amount = purchaseOrder.TotalAmount,
                CurrencyCode = NormalizeCurrency(purchaseOrder.Currency),
                AtUtc = DateTime.UtcNow,
                SourceType = EventType,
                SourceReference = receipt.ReceiptNumber
            }, correlation, cancellationToken);
        if (!authority.IsReady || authority.Policy is null ||
            authority.Workflow is null || authority.Steps.Count == 0)
            throw Validation("RCV_AUTHORITY_WORKFLOW_BLOCKED", authority.Message);
        return new Governance(profile, authority, Serialize(new
        {
            schemaVersion = "tdc.receipt-inspection-governance.v1",
            profile = new { profile.Id, profile.ProfileCode, profile.Version },
            decisions = profile.Decisions.OrderBy(item => item.DecisionKey)
                .Select(item => new
                {
                    item.DecisionKey,
                    ValueHash = Hash(item.Value.GetRawText()),
                    item.IsComplete
                }),
            authority
        }));
    }

    private async Task<PurchaseOrderReceipt> LoadReceiptAsync(
        Guid receiptId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<PurchaseOrderReceipt> query = _unitOfWork.Repository<PurchaseOrderReceipt>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == receiptId && !item.IsDeleted)
            .Include(item => item.PurchaseOrder).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Items).ThenInclude(item => item.PurchaseOrderItem)
                .ThenInclude(item => item.InventoryItem);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
               ?? throw NotFound("RCV_RECEIPT_NOT_FOUND", "The receipt was not found in the current tenant.");
    }

    private IQueryable<ProcurementReceiptInspectionCase> CaseQuery(bool tracked)
    {
        var query = Cases.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.PurchaseOrderReceipt).ThenInclude(item => item.PurchaseOrder)
                .ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Lines).ThenInclude(item => item.PurchaseOrderReceiptItem)
                .ThenInclude(item => item.PurchaseOrderItem).ThenInclude(item => item.InventoryItem)
            .Include(item => item.Evidence)
            .Include(item => item.Actions);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<ProcurementReceiptInspectionCase> LoadCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken) =>
        await CaseQuery(true).SingleOrDefaultAsync(item => item.Id == caseId, cancellationToken)
        ?? throw NotFound("RCV_INSPECTION_NOT_FOUND", "The inspection case was not found in the current tenant.");

    private async Task<BusinessPartner> EnsureLinkedSupplierAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.IsExternalUser)
            throw new ProcurementReceiptInspectionAuthorizationException(
                "Only a linked supplier portal user may acknowledge a receipt rejection.");
        var direct = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == businessPartnerId && item.UserId == _currentUser.UserId &&
                                  !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (direct is not null) return direct;
        var linked = await _unitOfWork.Repository<BusinessPartnerUser>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.BusinessPartnerId == businessPartnerId &&
                                  item.UserId == _currentUser.UserId && item.IsActive && !item.IsDeleted)
            .Include(item => item.BusinessPartner)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return linked?.BusinessPartner
               ?? throw new ProcurementReceiptInspectionAuthorizationException(
                   "The current supplier portal user is not linked to this receipt supplier.");
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        PurchaseOrderReceipt receipt,
        string correlation,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementReceiptInspectionAuthorizationException(
                "Supplier portal users cannot administer internal receipt inspection.");
        if (IsAdministrator()) return;
        var warehouseId = receipt.Items.Select(item => item.LocationId)
            .FirstOrDefault(item => item.HasValue && item != Guid.Empty);
        var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = EventType,
            SourceReference = receipt.ReceiptNumber,
            WarehouseId = warehouseId
        }, correlation, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementReceiptInspectionAuthorizationException(decision.Message);
    }

    private async Task AddActionAsync(
        ProcurementReceiptInspectionCase inspection,
        ProcurementReceiptInspectionActionType action,
        string comment,
        string reference,
        string correlation,
        string idempotencyKey,
        Guid? actorBusinessPartnerId,
        CancellationToken cancellationToken)
    {
        var existing = await Actions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.IdempotencyKey == idempotencyKey.Trim() && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.InspectionCaseId != inspection.Id || existing.ActionType != action)
                throw Conflict("RCV_ACTION_IDEMPOTENCY_CONFLICT",
                    "The action idempotency key belongs to another receipt operation.");
            return;
        }
        var persistedSequence = await Actions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.InspectionCaseId == inspection.Id && !item.IsDeleted)
            .Select(item => (int?)item.Sequence).MaxAsync(cancellationToken) ?? 0;
        var trackedSequence = inspection.Actions.Count == 0
            ? 0
            : inspection.Actions.Max(item => item.Sequence);
        var sequence = Math.Max(persistedSequence, trackedSequence) + 1;
        var row = new ProcurementReceiptInspectionAction
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            InspectionCaseId = inspection.Id,
            Sequence = sequence,
            ActionType = action,
            StatusAfter = inspection.Status,
            ResolutionKind = inspection.ResolutionKind,
            Quantity = action is ProcurementReceiptInspectionActionType.RejectionNoteIssued
                or ProcurementReceiptInspectionActionType.ReturnAuthorized
                or ProcurementReceiptInspectionActionType.ReturnDispatched
                or ProcurementReceiptInspectionActionType.ReplacementRequested
                or ProcurementReceiptInspectionActionType.ReplacementReceived
                    ? inspection.RejectedQuantity
                    : inspection.AcceptedQuantity,
            Reference = Trim(reference, 100) ?? action.ToString(),
            Comment = Trim(comment, 1000) ?? action.ToString(),
            ActorUserId = _currentUser.UserId,
            ActorBusinessPartnerId = actorBusinessPartnerId,
            ActorName = ActorName,
            OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId
        };
        row.IntegrityHash = ActionHash(row);
        await Actions.AddAsync(row);
        inspection.Actions.Add(row);
    }

    private async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        if (_unitOfWork.HasActiveTransaction)
        {
            await action();
            return;
        }
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await action();
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private async Task RecordEventAsync(
        ProcurementReceiptInspectionCase inspection,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string reason,
        string correlation,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "receipt-inspection", inspection.TenantId, inspection.Id,
                action.ToLowerInvariant(), correlation),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = "TDC-0502",
            RuleId = inspection.AuthorityRuleId,
            RuleVersion = $"{inspection.PolicyVersion}",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = "PurchaseOrderReceipt",
            SourceId = inspection.PurchaseOrderReceiptId,
            SourceReference = inspection.PurchaseOrderReceipt.ReceiptNumber,
            Reason = reason,
            Before = before,
            After = after,
            CorrelationId = correlation,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = inspection.Evidence.Select(item => new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = item.ReferenceKind == ProcurementReceiptInspectionEvidenceKind.WorkflowEvidenceDocument
                    ? ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument
                    : ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                ReferenceId = item.WorkflowEvidenceDocumentId ?? item.FileUploadRecordId,
                Reference = item.EvidenceReference,
                Label = item.ActionKey,
                RequirementKey = item.RequirementKey
            }).ToList()
        }, cancellationToken);
    }

    private async Task PublishAsync(
        string topic,
        ProcurementReceiptInspectionCase inspection,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = _currentUser.TenantId,
                TopicKey = topic,
                NotificationType = EventType,
                EntityType = "PurchaseOrderReceipt",
                EntityId = inspection.PurchaseOrderReceiptId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["receiptNumber"] = inspection.PurchaseOrderReceipt.ReceiptNumber,
                    ["status"] = inspection.Status.ToString(),
                    ["acceptedQuantity"] = inspection.AcceptedQuantity,
                    ["rejectedQuantity"] = inspection.RejectedQuantity,
                    ["qualityHold"] = inspection.QualityHold
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish receipt-inspection notification for {InspectionId}", inspection.Id);
        }
    }

    private static void Recalculate(ProcurementReceiptInspectionCase inspection)
    {
        inspection.ReceivedQuantity = inspection.Lines.Sum(item => item.ReceivedQuantity);
        inspection.AcceptedQuantity = inspection.Lines.Sum(item => item.AcceptedQuantity);
        inspection.RejectedQuantity = inspection.Lines.Sum(item => item.RejectedQuantity);
        inspection.PendingQuantity = inspection.Lines.Sum(item => item.PendingQuantity);
        inspection.StockEligibleQuantity = inspection.AcceptedQuantity;
        inspection.ApEligibleQuantity = inspection.PendingQuantity == 0 ? inspection.AcceptedQuantity : 0;
        inspection.ApBlockedQuantity = inspection.ReceivedQuantity - inspection.ApEligibleQuantity;
    }

    private static ProcurementReceiptInspectionDto Map(ProcurementReceiptInspectionCase item) => new()
    {
        Id = item.Id,
        PurchaseOrderReceiptId = item.PurchaseOrderReceiptId,
        Sequence = item.Sequence,
        Status = item.Status,
        ReceivedQuantity = item.ReceivedQuantity,
        AcceptedQuantity = item.AcceptedQuantity,
        RejectedQuantity = item.RejectedQuantity,
        PendingQuantity = item.PendingQuantity,
        QualityHold = item.QualityHold,
        QualityHoldReason = item.QualityHoldReason,
        RejectionNoteNumber = item.RejectionNoteNumber,
        SupplierAcknowledgementStatus = item.SupplierAcknowledgementStatus,
        ResolutionKind = item.ResolutionKind,
        ResolutionStatus = item.ResolutionStatus,
        StockEligibleQuantity = item.StockEligibleQuantity,
        StockPostedQuantity = item.StockPostedQuantity,
        ApEligibleQuantity = item.ApEligibleQuantity,
        ApBlockedQuantity = item.ApBlockedQuantity,
        WorkflowInstanceId = item.WorkflowInstanceId,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        Lines = item.Lines.OrderBy(line => line.CreatedAt).Select(line =>
            new ProcurementReceiptInspectionLineDto
            {
                Id = line.Id,
                PurchaseOrderReceiptItemId = line.PurchaseOrderReceiptItemId,
                PurchaseOrderItemId = line.PurchaseOrderReceiptItem.PurchaseOrderItemId,
                ItemCode = line.PurchaseOrderReceiptItem.PurchaseOrderItem.InventoryItem?.ItemCode ?? string.Empty,
                ItemName = line.PurchaseOrderReceiptItem.PurchaseOrderItem.InventoryItem?.Name ??
                           line.PurchaseOrderReceiptItem.PurchaseOrderItem.ItemDescription ?? string.Empty,
                UnitOfMeasure = line.PurchaseOrderReceiptItem.UnitOfMeasure ?? string.Empty,
                ReceivedQuantity = line.ReceivedQuantity,
                AcceptedQuantity = line.AcceptedQuantity,
                RejectedQuantity = line.RejectedQuantity,
                PendingQuantity = line.PendingQuantity,
                Disposition = line.Disposition,
                RejectionReason = line.RejectionReason,
                InspectionNotes = line.InspectionNotes,
                QuarantineLocationId = line.QuarantineLocationId
            }).ToList(),
        Evidence = item.Evidence.OrderBy(row => row.CreatedAt).Select(row =>
            new ProcurementReceiptInspectionEvidenceDto
            {
                Id = row.Id,
                ActionKey = row.ActionKey,
                RequirementKey = row.RequirementKey,
                ReferenceKind = row.ReferenceKind,
                WorkflowEvidenceDocumentId = row.WorkflowEvidenceDocumentId,
                FileUploadRecordId = row.FileUploadRecordId,
                EvidenceReference = row.EvidenceReference,
                EvidenceHash = row.EvidenceHash
            }).ToList(),
        Actions = item.Actions.OrderBy(row => row.Sequence).Select(row =>
            new ProcurementReceiptInspectionActionDto
            {
                Id = row.Id,
                Sequence = row.Sequence,
                ActionType = row.ActionType,
                StatusAfter = row.StatusAfter,
                ResolutionKind = row.ResolutionKind,
                Quantity = row.Quantity,
                Reference = row.Reference,
                Comment = row.Comment,
                ActorName = row.ActorName,
                OccurredAtUtc = row.OccurredAtUtc
            }).ToList()
    };

    private static object Snapshot(ProcurementReceiptInspectionCase item) => new
    {
        item.Id,
        item.Sequence,
        item.Status,
        item.ReceivedQuantity,
        item.AcceptedQuantity,
        item.RejectedQuantity,
        item.PendingQuantity,
        item.QualityHold,
        item.RejectionNoteNumber,
        item.SupplierAcknowledgementStatus,
        item.ResolutionKind,
        item.ResolutionStatus,
        item.StockPostedQuantity,
        item.ApEligibleQuantity,
        item.ApBlockedQuantity,
        item.WorkflowInstanceId,
        item.IntegrityHash
    };

    private static string CaseHash(ProcurementReceiptInspectionCase item) => Hash(Serialize(new
    {
        item.TenantId,
        item.PurchaseOrderReceiptId,
        item.Sequence,
        item.Status,
        item.ReceivedQuantity,
        item.AcceptedQuantity,
        item.RejectedQuantity,
        item.PendingQuantity,
        item.QualityHold,
        item.RejectionNoteNumber,
        item.SupplierAcknowledgementStatus,
        item.ResolutionKind,
        item.ResolutionStatus,
        item.ConfigurationProfileId,
        item.ConfigurationProfileVersion,
        item.PolicySetId,
        item.PolicyVersion,
        item.AuthorityRuleId,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.SourceSnapshotHash
    }));

    private static string LineHash(ProcurementReceiptInspectionLine item) => Hash(Serialize(new
    {
        item.TenantId,
        item.InspectionCaseId,
        item.PurchaseOrderReceiptItemId,
        item.ReceivedQuantity,
        item.AcceptedQuantity,
        item.RejectedQuantity,
        item.PendingQuantity,
        item.Disposition,
        item.RejectionReason,
        item.QuarantineLocationId
    }));

    private static string ActionHash(ProcurementReceiptInspectionAction item) => Hash(Serialize(new
    {
        item.TenantId,
        item.InspectionCaseId,
        item.Sequence,
        item.ActionType,
        item.StatusAfter,
        item.ResolutionKind,
        item.Quantity,
        item.Reference,
        item.Comment,
        item.ActorUserId,
        item.ActorBusinessPartnerId,
        item.OccurredAtUtc,
        item.IdempotencyKey,
        item.CorrelationId
    }));

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementReceiptInspectionAuthorizationException(
                "An authenticated tenant user is required.");
    }

    private bool IsAdministrator() =>
        _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");

    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.FullName)
        ? _currentUser.Username
        : _currentUser.FullName;

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch { throw Validation("RCV_ROW_VERSION_INVALID", "The receipt inspection row version is invalid."); }
        if (!current.SequenceEqual(parsed))
            throw Conflict("RCV_ROW_VERSION_CONFLICT", "The receipt inspection changed. Reload and retry.");
    }

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3
            ? "GHS"
            : currency.Trim().ToUpperInvariant();
    private static string NormalizeCorrelation(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NewCorrelation() : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static string NewCorrelation() => Guid.NewGuid().ToString("N");
    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static ProcurementReceiptInspectionNotFoundException NotFound(string code, string message) => new(code, message);
    private static ProcurementReceiptInspectionValidationException Validation(string code, string message) => new(code, message);
    private static ProcurementReceiptInspectionConflictException Conflict(string code, string message) => new(code, message);

    private sealed record Governance(
        ProcurementConfigurationProfileDto Profile,
        ProcurementAuthorityRouteDecisionDto Authority,
        string Snapshot);
}
