using System.Data;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>
/// Finance/AP implementation of the AP-006 lifecycle. It composes the existing
/// invoice matcher and shared workflow, DMS/evidence, notification and control-
/// event services. Payment allocation and posting remain in VendorPaymentService.
/// </summary>
public sealed class VendorInvoiceMatchExceptionService : IVendorInvoiceMatchExceptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IVendorInvoiceService _invoiceService;
    private readonly IWorkflowService _workflow;
    private readonly IProcurementConfigurationService _configuration;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly IAuthorizationService _authorization;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<VendorInvoiceMatchExceptionService> _logger;

    public VendorInvoiceMatchExceptionService(
        ApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IVendorInvoiceService invoiceService,
        IWorkflowService workflow,
        IProcurementConfigurationService configuration,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        IAuthorizationService authorization,
        IHttpContextAccessor httpContextAccessor,
        ILogger<VendorInvoiceMatchExceptionService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _invoiceService = invoiceService;
        _workflow = workflow;
        _configuration = configuration;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _authorization = authorization;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid ActorId => Guid.TryParse(_currentUser.UserId, out var value) ? value : Guid.Empty;
    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.UserName)
        ? "Authenticated finance user"
        : _currentUser.UserName!;
    private bool IsPrivileged => _currentUser.IsInRole("SuperAdmin") || _currentUser.IsInRole("TenantAdmin");

    public async Task<VendorInvoiceMatchExceptionOverviewDto> GetOverviewAsync(
        Guid vendorInvoiceId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedActor();
        var invoice = await _db.VendorInvoices.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == vendorInvoiceId && !item.IsDeleted,
                cancellationToken)
            ?? throw NotFound("AP_INVOICE_NOT_FOUND", "The vendor invoice was not found in the current tenant.");
        var readiness = await _invoiceService.GetThreeWayMatchReadinessAsync(vendorInvoiceId, cancellationToken);
        var history = await ExceptionQuery()
            .Where(item => item.VendorInvoiceId == vendorInvoiceId)
            .OrderByDescending(item => item.Sequence)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var principal = _httpContextAccessor.HttpContext?.User;
        var canManageApInvoices = principal?.Identity?.IsAuthenticated == true &&
            (await _authorization.AuthorizeAsync(
                principal,
                resource: null,
                FinancePermissions.ManageApInvoices)).Succeeded;
        var canApproveApInvoices = principal?.Identity?.IsAuthenticated == true &&
            (await _authorization.AuthorizeAsync(
                principal,
                resource: null,
                FinancePermissions.ApproveApInvoices)).Succeeded;
        var active = history.FirstOrDefault(item =>
            item.ExpiresAtUtc > now && item.Status is
                VendorInvoiceMatchExceptionStatus.PendingApproval or
                VendorInvoiceMatchExceptionStatus.Approved);
        var correctiveActionItem = history.FirstOrDefault(item =>
            item.Status == VendorInvoiceMatchExceptionStatus.Approved &&
            item.CorrectiveActionStatus == VendorInvoiceMatchCorrectiveActionStatus.Planned);
        var canDecide = false;
        if (active?.WorkflowInstanceId is Guid workflowId &&
            VendorInvoiceMatchExceptionRules.CanDecide(active.Status))
        {
            var priorApprovers = await ApprovedActorIdsAsync(workflowId, cancellationToken);
            canDecide = canApproveApInvoices &&
                        VendorInvoiceMatchExceptionRules.IsIndependent(
                            ActorId, active.RequestedById, invoice.SubmittedById, priorApprovers) &&
                        await _workflow.CanUserApproveAsync(
                            VendorInvoiceMatchExceptionRules.WorkflowEntityType,
                            active.Id,
                            ActorId);
        }

        return new VendorInvoiceMatchExceptionOverviewDto
        {
            VendorInvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            MatchingReadiness = readiness,
            CanRequest = canManageApInvoices &&
                         VendorInvoiceMatchExceptionRules.IsExceptionable(readiness) &&
                         active == null,
            CanDecide = canDecide,
            CanCancel = canManageApInvoices &&
                        active != null &&
                        VendorInvoiceMatchExceptionRules.CanCancel(active.Status) &&
                        (active.RequestedById == ActorId || IsPrivileged),
            CanCompleteCorrectiveAction = canManageApInvoices &&
                correctiveActionItem != null &&
                VendorInvoiceMatchExceptionRules.CanCompleteCorrectiveAction(
                    correctiveActionItem!.Status, correctiveActionItem.CorrectiveActionStatus,
                    correctiveActionItem.ExpiresAtUtc, now) &&
                (correctiveActionItem.CorrectiveActionOwnerId == ActorId || IsPrivileged),
            RequiredEvidenceKeys = VendorInvoiceMatchExceptionRules.RequiredEvidenceKeys,
            DecisionKeys = VendorInvoiceMatchExceptionRules.DecisionKeys,
            Active = active == null ? null : Map(active, now),
            CorrectiveActionItem = correctiveActionItem == null ? null : Map(correctiveActionItem, now),
            History = history.Select(item => Map(item, now)).ToList()
        };
    }

    public Task<VendorInvoiceMatchExceptionDto> RequestAsync(
        Guid vendorInvoiceId,
        CreateVendorInvoiceMatchExceptionDto request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(
            () => RequestCoreAsync(vendorInvoiceId, request, correlationId, cancellationToken),
            cancellationToken);

    public Task<VendorInvoiceMatchExceptionDto> DecideAsync(
        Guid exceptionId,
        DecideVendorInvoiceMatchExceptionDto request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(
            () => DecideCoreAsync(exceptionId, request, correlationId, cancellationToken),
            cancellationToken);

    public Task<VendorInvoiceMatchExceptionDto> CancelAsync(
        Guid exceptionId,
        CancelVendorInvoiceMatchExceptionDto request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(
            () => CancelCoreAsync(exceptionId, request, correlationId, cancellationToken),
            cancellationToken);

    public Task<VendorInvoiceMatchExceptionDto> CompleteCorrectiveActionAsync(
        Guid exceptionId,
        CompleteVendorInvoiceMatchCorrectiveActionDto request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(
            () => CompleteCorrectiveActionCoreAsync(exceptionId, request, correlationId, cancellationToken),
            cancellationToken);

    private async Task<VendorInvoiceMatchExceptionDto> RequestCoreAsync(
        Guid invoiceId,
        CreateVendorInvoiceMatchExceptionDto request,
        string suppliedCorrelation,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedActor();
        var now = DateTime.UtcNow;
        var correlation = Normalize(suppliedCorrelation, 100);
        ValidateRequest(request, now);
        var idempotencyKey = request.IdempotencyKey.Trim();
        await _unitOfWork.AcquireTransactionLockAsync($"tdc0507-invoice:{TenantId:N}:{invoiceId:N}", cancellationToken);
        await _unitOfWork.AcquireTransactionLockAsync(
            $"tdc0507-idempotency:{TenantId:N}:{idempotencyKey}",
            cancellationToken);

        var duplicate = await ExceptionQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (duplicate != null)
        {
            if (duplicate.VendorInvoiceId != invoiceId)
                throw Conflict(
                    "AP_MATCH_EXCEPTION_IDEMPOTENCY_MISMATCH",
                    "The idempotency key is already bound to a different vendor invoice.");
            if (!string.Equals(
                    RequestFingerprint(invoiceId, request),
                    RequestFingerprint(duplicate),
                    StringComparison.Ordinal))
                throw Conflict(
                    "AP_MATCH_EXCEPTION_IDEMPOTENCY_PAYLOAD_MISMATCH",
                    "The idempotency key is already bound to a different match-exception request payload.");

            return Map(duplicate, now);
        }

        var invoice = await _db.VendorInvoices
            .Include(item => item.BusinessPartner)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == invoiceId && !item.IsDeleted,
                cancellationToken)
            ?? throw NotFound("AP_INVOICE_NOT_FOUND", "The vendor invoice was not found in the current tenant.");
        if (!invoice.PurchaseOrderId.HasValue)
            throw Validation("AP_MATCH_EXCEPTION_PO_REQUIRED", "Only a PO-linked invoice can request a three-way-match exception.");

        var readiness = await _invoiceService.GetThreeWayMatchReadinessAsync(invoice.Id, cancellationToken);
        if (!VendorInvoiceMatchExceptionRules.IsExceptionable(readiness))
            throw Conflict("AP_MATCH_EXCEPTION_NOT_ELIGIBLE",
                "The current invoice has no exception-eligible tolerance variance or has a non-overridable hard stop.");
        if (string.IsNullOrWhiteSpace(readiness.SnapshotHash))
            throw Conflict("AP_MATCH_EXCEPTION_SNAPSHOT_MISSING", "The authoritative invoice-match snapshot is unavailable.");

        var hasActive = await _db.Set<VendorInvoiceMatchException>().AnyAsync(item =>
            item.TenantId == TenantId && item.VendorInvoiceId == invoice.Id && !item.IsDeleted &&
            (item.Status == VendorInvoiceMatchExceptionStatus.PendingApproval ||
             item.Status == VendorInvoiceMatchExceptionStatus.Approved) && item.ExpiresAtUtc > now, cancellationToken);
        if (hasActive)
            throw Conflict("AP_MATCH_EXCEPTION_ACTIVE",
                "This invoice already has a pending or current approved three-way-match exception.");

        var owner = await _db.Users.AsNoTracking().SingleOrDefaultAsync(user =>
            user.Id == request.CorrectiveActionOwnerId && user.IsActive &&
            (user.TenantId == TenantId || user.UserTenants.Any(link =>
                link.TenantId == TenantId && !link.IsDeleted && link.Status == UserTenantStatus.Active)),
            cancellationToken)
            ?? throw Validation("AP_MATCH_EXCEPTION_OWNER_INVALID",
                "The corrective-action owner is not an active user in the current tenant.");
        var ownerCanComplete = await _db.UserRoles.AsNoTracking()
            .Where(userRole => userRole.UserId == owner.Id)
            .AnyAsync(userRole => userRole.Role.RolePermissions.Any(rolePermission =>
                    rolePermission.Permission.Name.ToUpper() ==
                    FinancePermissions.ManageApInvoices.ToUpper()),
                cancellationToken);
        if (!ownerCanComplete)
            throw Validation(
                "AP_MATCH_EXCEPTION_OWNER_PERMISSION_REQUIRED",
                "The corrective-action owner must have permission to manage AP invoices so the assigned action can be completed.");
        var profile = await _configuration.GetEffectiveProfileAsync("TDC-PROCUREMENT", now, cancellationToken);
        if (profile == null || profile.Decisions.Count != 14 || profile.Decisions.Any(item => !item.IsComplete))
            throw Conflict("AP_MATCH_EXCEPTION_CONFIGURATION_MISSING",
                "A complete effective Published TDC procurement configuration is required.");
        var evidence = await ValidateEvidenceAsync(invoice, request.Evidence, cancellationToken);
        EnsureRequiredEvidence(evidence, VendorInvoiceMatchExceptionRules.RequiredEvidenceKeys);
        var sequence = await _db.Set<VendorInvoiceMatchException>()
            .Where(item => item.TenantId == TenantId && item.VendorInvoiceId == invoice.Id)
            .Select(item => (int?)item.Sequence).MaxAsync(cancellationToken) ?? 0;
        var variances = readiness.Discrepancies.Select(discrepancy =>
            new VendorInvoiceMatchExceptionVariance
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                VarianceType = Normalize(discrepancy.DiscrepancyType, 100),
                ItemDescription = Normalize(discrepancy.ItemDescription, 500),
                ActualValue = discrepancy.InvoiceValue,
                ExpectedValue = discrepancy.ExpectedValue ?? 0m,
                Variance = discrepancy.Variance,
                VariancePercentage = discrepancy.VariancePercentage,
                ConfiguredTolerancePercent = string.Equals(discrepancy.DiscrepancyType, "Quantity", StringComparison.OrdinalIgnoreCase)
                    ? readiness.QuantityTolerancePercentage
                    : readiness.PriceTolerancePercentage,
                CreatedAt = now,
                CreatedBy = ActorName
            }).ToList();
        var item = new VendorInvoiceMatchException
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            VendorInvoiceId = invoice.Id,
            PurchaseOrderId = invoice.PurchaseOrderId.Value,
            Sequence = sequence + 1,
            Status = VendorInvoiceMatchExceptionStatus.PendingApproval,
            VarianceType = string.Join(", ", variances.Select(row => row.VarianceType).Distinct(StringComparer.OrdinalIgnoreCase)),
            PriceTolerancePercent = readiness.PriceTolerancePercentage,
            QuantityTolerancePercent = readiness.QuantityTolerancePercentage,
            RootCauseCategory = request.RootCauseCategory.Trim(),
            RootCauseDescription = request.RootCauseDescription.Trim(),
            Justification = request.Justification.Trim(),
            CorrectiveAction = request.CorrectiveAction.Trim(),
            CorrectiveActionOwnerId = owner.Id,
            CorrectiveActionOwnerName = owner.FullName,
            CorrectiveActionDueAtUtc = EnsureUtc(request.CorrectiveActionDueAtUtc),
            ExpiresAtUtc = EnsureUtc(request.ExpiresAtUtc),
            InvoiceSnapshotHash = readiness.SnapshotHash,
            VarianceSnapshotJson = JsonSerializer.Serialize(readiness.Discrepancies, JsonOptions),
            ConfigurationProfileId = profile.Id,
            ConfigurationProfileVersion = profile.Version,
            RequestedById = ActorId,
            RequestedByName = ActorName,
            RequestedAtUtc = now,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = ActorName,
            Variances = variances,
            Evidence = evidence
        };
        item.IntegrityHash = Integrity(item);
        _db.Add(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var workflow = await _workflow.StartApprovalWorkflowAsync(
            VendorInvoiceMatchExceptionRules.WorkflowEntityType, item.Id);
        if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue)
            throw Conflict("AP_MATCH_EXCEPTION_WORKFLOW_START_FAILED",
                workflow.Message ?? "The shared exception workflow could not be started.");
        item.WorkflowInstanceId = workflow.WorkflowInstanceId.Value;
        AddAction(item, VendorInvoiceMatchExceptionRules.RequestAction,
            VendorInvoiceMatchExceptionStatus.PendingApproval,
            VendorInvoiceMatchExceptionStatus.PendingApproval,
            request.Justification, now);
        item.IntegrityHash = Integrity(item);
        await RecordEventAsync(item, VendorInvoiceMatchExceptionRules.RequestAction,
            ProcurementControlEventResult.ReviewRequired, request.Justification, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyAsync("finance.ap.match-exception-requested", item, cancellationToken);
        return Map(await LoadAsync(item.Id, cancellationToken), now);
    }

    private async Task<VendorInvoiceMatchExceptionDto> DecideCoreAsync(
        Guid exceptionId,
        DecideVendorInvoiceMatchExceptionDto request,
        string suppliedCorrelation,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedActor();
        var now = DateTime.UtcNow;
        await _unitOfWork.AcquireTransactionLockAsync($"tdc0507-exception:{TenantId:N}:{exceptionId:N}", cancellationToken);
        var item = await LoadAsync(exceptionId, cancellationToken);
        EnsureRowVersion(item.RowVersion, request.RowVersion);
        if (!VendorInvoiceMatchExceptionRules.CanDecide(item.Status))
            throw Conflict("AP_MATCH_EXCEPTION_STATE_INVALID", "Only a pending exception can be decided.");
        if (item.ExpiresAtUtc <= now)
            throw Conflict("AP_MATCH_EXCEPTION_EXPIRED", "The requested exception expired before final approval.");
        if (!item.WorkflowInstanceId.HasValue)
            throw Conflict("AP_MATCH_EXCEPTION_WORKFLOW_MISSING", "The exception has no shared workflow instance.");
        var priorApprovers = await ApprovedActorIdsAsync(item.WorkflowInstanceId.Value, cancellationToken);
        if (!VendorInvoiceMatchExceptionRules.IsIndependent(
                ActorId, item.RequestedById, item.VendorInvoice.SubmittedById, priorApprovers))
            throw Forbidden("AP_MATCH_EXCEPTION_SOD_BLOCKED",
                "The requester, invoice processor, or a prior approver cannot decide this approval stage.");
        if (!await _workflow.CanUserApproveAsync(
                VendorInvoiceMatchExceptionRules.WorkflowEntityType, item.Id, ActorId))
            throw Forbidden("AP_MATCH_EXCEPTION_APPROVER_INELIGIBLE",
                "The current actor is not assigned to the active shared-workflow approval stage.");

        var result = await _workflow.ProcessApprovalStepAsync(
            VendorInvoiceMatchExceptionRules.WorkflowEntityType,
            item.Id,
            ActorId,
            request.Approved ? "Approve" : "Reject",
            request.Comment.Trim());
        if (!result.Success)
            throw Conflict("AP_MATCH_EXCEPTION_WORKFLOW_DECISION_FAILED",
                result.Message ?? "The shared workflow decision failed.");

        var from = item.Status;
        if (request.Approved && result.Status == WorkflowInstanceStatus.Completed)
        {
            await RevalidateForFinalApprovalAsync(item, cancellationToken);
            var approvedActors = await ApprovedActorIdsAsync(item.WorkflowInstanceId.Value, cancellationToken);
            if (approvedActors.Distinct().Count() < 2 || approvedActors.Any(actor =>
                    !VendorInvoiceMatchExceptionRules.IsIndependent(
                        actor, item.RequestedById, item.VendorInvoice.SubmittedById,
                        approvedActors.Where(other => other != actor))))
                throw Conflict("AP_MATCH_EXCEPTION_DUAL_APPROVAL_MISSING",
                    "Two distinct independent shared-workflow approvers are required.");
            var approvalEvent = await RecordEventAsync(item,
                VendorInvoiceMatchExceptionRules.ApprovalAction,
                ProcurementControlEventResult.Allowed,
                request.Comment,
                new
                {
                    purchaseOrderId = item.PurchaseOrderId,
                    workflowInstanceId = item.WorkflowInstanceId,
                    invoiceSnapshotHash = item.InvoiceSnapshotHash,
                    expiresAtUtc = item.ExpiresAtUtc,
                    exceptionId = item.Id,
                    varianceType = item.VarianceType,
                    priceTolerancePercent = item.PriceTolerancePercent,
                    quantityTolerancePercent = item.QuantityTolerancePercent,
                    rootCauseCategory = item.RootCauseCategory,
                    rootCauseDescription = item.RootCauseDescription,
                    correctiveAction = item.CorrectiveAction,
                    correctiveActionOwnerId = item.CorrectiveActionOwnerId,
                    correctiveActionDueAtUtc = item.CorrectiveActionDueAtUtc,
                    approvedActorIds = approvedActors.Distinct().OrderBy(value => value)
                },
                cancellationToken);
            item.Status = VendorInvoiceMatchExceptionStatus.Approved;
            item.FinalApprovedById = ActorId;
            item.FinalApprovedByName = ActorName;
            item.FinalApprovedAtUtc = now;
            item.DecisionComment = request.Comment.Trim();
            item.ApprovalControlEventId = approvalEvent.Id;
            AddAction(item, VendorInvoiceMatchExceptionRules.ApprovalAction, from, item.Status,
                request.Comment, now);
        }
        else if (request.Approved)
        {
            if (result.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                throw Conflict("AP_MATCH_EXCEPTION_WORKFLOW_REJECTED",
                    "The shared workflow rejected the exception.");
            AddAction(item, "InvoiceMatchExceptionApprovalStageCompleted", from, from,
                request.Comment, now);
        }
        else
        {
            if (result.Status == WorkflowInstanceStatus.Completed)
                throw Conflict("AP_MATCH_EXCEPTION_WORKFLOW_ALREADY_APPROVED",
                    "A completed approved workflow cannot be recorded as rejected.");
            if (result.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
                throw Conflict("AP_MATCH_EXCEPTION_REJECTION_NOT_TERMINAL",
                    "The shared workflow did not reach a rejected terminal state.");
            item.Status = VendorInvoiceMatchExceptionStatus.Rejected;
            item.RejectedById = ActorId;
            item.RejectedByName = ActorName;
            item.RejectedAtUtc = now;
            item.DecisionComment = request.Comment.Trim();
            AddAction(item, VendorInvoiceMatchExceptionRules.RejectionAction, from, item.Status,
                request.Comment, now);
            await RecordEventAsync(item, VendorInvoiceMatchExceptionRules.RejectionAction,
                ProcurementControlEventResult.Denied, request.Comment, null, cancellationToken);
        }

        item.CorrelationId = Normalize(suppliedCorrelation, 100);
        item.UpdatedAt = now;
        item.UpdatedBy = ActorName;
        item.IntegrityHash = Integrity(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyAsync(item.Status == VendorInvoiceMatchExceptionStatus.Approved
                ? "finance.ap.match-exception-approved"
                : item.Status == VendorInvoiceMatchExceptionStatus.Rejected
                    ? "finance.ap.match-exception-rejected"
                    : "finance.ap.match-exception-stage-approved",
            item, cancellationToken);
        return Map(await LoadAsync(item.Id, cancellationToken), now);
    }

    private async Task<VendorInvoiceMatchExceptionDto> CancelCoreAsync(
        Guid exceptionId,
        CancelVendorInvoiceMatchExceptionDto request,
        string suppliedCorrelation,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedActor();
        var now = DateTime.UtcNow;
        await _unitOfWork.AcquireTransactionLockAsync($"tdc0507-exception:{TenantId:N}:{exceptionId:N}", cancellationToken);
        var item = await LoadAsync(exceptionId, cancellationToken);
        EnsureRowVersion(item.RowVersion, request.RowVersion);
        if (!VendorInvoiceMatchExceptionRules.CanCancel(item.Status))
            throw Conflict("AP_MATCH_EXCEPTION_CANCELLATION_INVALID", "Only a pending exception can be cancelled.");
        if (item.RequestedById != ActorId && !IsPrivileged)
            throw Forbidden("AP_MATCH_EXCEPTION_CANCELLATION_FORBIDDEN",
                "Only the requester or a tenant administrator can cancel this exception.");
        var workflow = await _workflow.CancelWorkflowAsync(
            VendorInvoiceMatchExceptionRules.WorkflowEntityType, item.Id, request.Reason.Trim());
        if (!workflow.Success || workflow.Status is not WorkflowInstanceStatus.Cancelled)
            throw Conflict("AP_MATCH_EXCEPTION_WORKFLOW_CANCEL_FAILED",
                workflow.Message ?? "The shared workflow could not be cancelled.");
        var from = item.Status;
        item.Status = VendorInvoiceMatchExceptionStatus.Cancelled;
        item.DecisionComment = request.Reason.Trim();
        item.CorrelationId = Normalize(suppliedCorrelation, 100);
        item.UpdatedAt = now;
        item.UpdatedBy = ActorName;
        AddAction(item, VendorInvoiceMatchExceptionRules.CancellationAction, from, item.Status,
            request.Reason, now);
        await RecordEventAsync(item, VendorInvoiceMatchExceptionRules.CancellationAction,
            ProcurementControlEventResult.Denied, request.Reason, null, cancellationToken);
        item.IntegrityHash = Integrity(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyAsync("finance.ap.match-exception-cancelled", item, cancellationToken);
        return Map(await LoadAsync(item.Id, cancellationToken), now);
    }

    private async Task<VendorInvoiceMatchExceptionDto> CompleteCorrectiveActionCoreAsync(
        Guid exceptionId,
        CompleteVendorInvoiceMatchCorrectiveActionDto request,
        string suppliedCorrelation,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedActor();
        var now = DateTime.UtcNow;
        await _unitOfWork.AcquireTransactionLockAsync($"tdc0507-exception:{TenantId:N}:{exceptionId:N}", cancellationToken);
        var item = await LoadAsync(exceptionId, cancellationToken);
        EnsureRowVersion(item.RowVersion, request.RowVersion);
        if (!VendorInvoiceMatchExceptionRules.CanCompleteCorrectiveAction(
                item.Status, item.CorrectiveActionStatus, item.ExpiresAtUtc, now))
            throw Conflict("AP_MATCH_EXCEPTION_CORRECTIVE_STATE_INVALID",
                "Corrective action can be completed once for an approved or expired exception.");
        if (item.CorrectiveActionOwnerId != ActorId && !IsPrivileged)
            throw Forbidden("AP_MATCH_EXCEPTION_CORRECTIVE_OWNER_REQUIRED",
                "Only the assigned corrective-action owner or a tenant administrator can complete it.");
        var completionEvidence = await ValidateEvidenceAsync(item.VendorInvoice, request.Evidence, cancellationToken);
        EnsureRequiredEvidence(completionEvidence, new[] { VendorInvoiceMatchExceptionRules.CorrectiveCompletionEvidenceKey });
        EnsureOnlyEvidenceKeys(
            completionEvidence,
            new[] { VendorInvoiceMatchExceptionRules.CorrectiveCompletionEvidenceKey });
        var existingEvidenceKeys = item.Evidence
            .Where(evidence => !evidence.IsDeleted)
            .Select(evidence => evidence.RequirementKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repeatedEvidenceKeys = completionEvidence
            .Select(evidence => evidence.RequirementKey)
            .Where(existingEvidenceKeys.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (repeatedEvidenceKeys.Length > 0)
            throw Validation(
                "AP_MATCH_EXCEPTION_EVIDENCE_ALREADY_ATTACHED",
                $"Corrective-action evidence cannot reuse requirement keys already attached to the exception: {string.Join(", ", repeatedEvidenceKeys)}.");
        foreach (var evidence in completionEvidence) item.Evidence.Add(evidence);
        item.CorrectiveActionStatus = VendorInvoiceMatchCorrectiveActionStatus.Completed;
        item.CorrectiveActionCompletedAtUtc = now;
        item.CorrectiveActionCompletedById = ActorId;
        item.CorrectiveActionCompletionNote = request.CompletionNote.Trim();
        item.CorrelationId = Normalize(suppliedCorrelation, 100);
        item.UpdatedAt = now;
        item.UpdatedBy = ActorName;
        AddAction(item, VendorInvoiceMatchExceptionRules.CorrectiveActionCompleted,
            item.Status, item.Status, request.CompletionNote, now);
        await RecordEventAsync(item, VendorInvoiceMatchExceptionRules.CorrectiveActionCompleted,
            ProcurementControlEventResult.Succeeded, request.CompletionNote,
            new
            {
                exceptionId = item.Id,
                item.CorrectiveActionOwnerId,
                item.CorrectiveActionDueAtUtc,
                completedAtUtc = now
            }, cancellationToken);
        item.IntegrityHash = Integrity(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyAsync("finance.ap.match-exception-corrective-completed", item, cancellationToken);
        return Map(await LoadAsync(item.Id, cancellationToken), now);
    }

    private async Task RevalidateForFinalApprovalAsync(
        VendorInvoiceMatchException item,
        CancellationToken cancellationToken)
    {
        var readiness = await _invoiceService.GetThreeWayMatchReadinessAsync(item.VendorInvoiceId, cancellationToken);
        if (!VendorInvoiceMatchExceptionRules.IsExceptionable(readiness) ||
            !string.Equals(readiness.SnapshotHash, item.InvoiceSnapshotHash, StringComparison.OrdinalIgnoreCase))
            throw Conflict("AP_MATCH_EXCEPTION_REVALIDATION_FAILED",
                "The invoice, receipt, PO, tolerance, or variance snapshot changed. Submit a new exception request.");
        var profile = await _configuration.GetEffectiveProfileAsync("TDC-PROCUREMENT", DateTime.UtcNow, cancellationToken);
        if (profile == null || profile.Id != item.ConfigurationProfileId ||
            profile.Version != item.ConfigurationProfileVersion ||
            profile.Decisions.Count != 14 || profile.Decisions.Any(decision => !decision.IsComplete))
            throw Conflict("AP_MATCH_EXCEPTION_CONFIGURATION_CHANGED",
                "The effective procurement configuration changed after this exception was requested.");
        await VendorInvoiceMatchExceptionEvidenceValidator.RevalidateAsync(
            _unitOfWork, TenantId, item.VendorInvoice, item.Evidence, cancellationToken);
    }

    private async Task<List<VendorInvoiceMatchExceptionEvidence>> ValidateEvidenceAsync(
        VendorInvoice invoice,
        IReadOnlyCollection<VendorInvoiceMatchExceptionEvidenceRequestDto> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            throw Validation("AP_MATCH_EXCEPTION_EVIDENCE_REQUIRED", "Controlled exception evidence is required.");
        if (requests.Any(item => string.IsNullOrWhiteSpace(item.RequirementKey) ||
                                 string.IsNullOrWhiteSpace(item.EvidenceReference)))
            throw Validation("AP_MATCH_EXCEPTION_EVIDENCE_INVALID",
                "Every evidence item requires a requirement key and reference.");
        if (requests.GroupBy(item => item.RequirementKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            throw Validation("AP_MATCH_EXCEPTION_EVIDENCE_DUPLICATE",
                "Each evidence requirement can be linked only once per action.");

        var rows = new List<VendorInvoiceMatchExceptionEvidence>();
        foreach (var request in requests)
        {
            Guid? workflowEvidenceId = null;
            Guid? uploadId = null;
            string hash;
            if (request.ReferenceKind == VendorInvoiceMatchExceptionEvidenceKind.WorkflowEvidenceDocument)
            {
                if (!request.WorkflowEvidenceDocumentId.HasValue || request.FileUploadRecordId.HasValue)
                    throw Validation("AP_MATCH_EXCEPTION_WORKFLOW_EVIDENCE_INVALID",
                        "Workflow evidence requires exactly one workflow evidence document ID.");
                var evidence = await (
                    from document in _db.WorkflowEvidenceDocuments.AsNoTracking()
                    join step in _db.WorkflowStepInstances.AsNoTracking()
                        on document.StepInstanceId equals step.Id
                    join workflow in _db.WorkflowInstances.AsNoTracking()
                        on step.WorkflowInstanceId equals workflow.Id
                    where document.TenantId == TenantId && step.TenantId == TenantId &&
                          workflow.TenantId == TenantId && document.Id == request.WorkflowEvidenceDocumentId &&
                          !document.IsDeleted && !step.IsDeleted && !workflow.IsDeleted &&
                          (workflow.EntityId == invoice.Id || workflow.EntityId == invoice.PurchaseOrderId)
                    select document).SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFound("AP_MATCH_EXCEPTION_EVIDENCE_NOT_FOUND",
                        "Workflow evidence was not found on a workflow for this invoice or purchase order.");
                if (!evidence.IsCurrent ||
                    evidence.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified ||
                    evidence.MalwareScanStatus != WorkflowMalwareScanStatus.Clean)
                    throw Conflict("AP_MATCH_EXCEPTION_EVIDENCE_UNVERIFIED",
                        "Workflow evidence must be current, verified, and malware-clean.");
                workflowEvidenceId = evidence.Id;
                hash = evidence.Sha256;
            }
            else if (request.ReferenceKind == VendorInvoiceMatchExceptionEvidenceKind.CentralDocument)
            {
                if (!request.FileUploadRecordId.HasValue || request.WorkflowEvidenceDocumentId.HasValue)
                    throw Validation("AP_MATCH_EXCEPTION_DMS_EVIDENCE_INVALID",
                        "Central-DMS evidence requires exactly one controlled upload ID.");
                var version = await _db.CentralDocumentVersions
                    .Include(item => item.DocumentRecord)
                    .Where(CentralDocumentEvidenceRules.CurrentPublished())
                    .AsNoTracking().SingleOrDefaultAsync(item =>
                        item.TenantId == TenantId && item.FileUploadRecordId == request.FileUploadRecordId &&
                        (item.DocumentRecord.SourceRecordId == invoice.Id ||
                         item.DocumentRecord.SourceRecordId == invoice.PurchaseOrderId), cancellationToken)
                    ?? throw NotFound("AP_MATCH_EXCEPTION_DMS_DOCUMENT_NOT_FOUND",
                        "The evidence is not a central-DMS document for this invoice or purchase order.");
                var upload = await _db.FileUploadRecords.AsNoTracking().SingleOrDefaultAsync(item =>
                    item.TenantId == TenantId && item.Id == request.FileUploadRecordId && !item.IsDeleted,
                    cancellationToken)
                    ?? throw NotFound("AP_MATCH_EXCEPTION_UPLOAD_NOT_FOUND",
                        "The controlled upload was not found in the current tenant.");
                if (upload.VirusScanStatus != FileVirusScanStatus.Clean)
                    throw Conflict("AP_MATCH_EXCEPTION_DMS_DOCUMENT_UNSAFE",
                        "Central-DMS evidence must have a completed Clean malware scan.");
                uploadId = upload.Id;
                hash = VendorInvoiceMatchExceptionRules.Hash(new
                {
                    upload.Id,
                    version.DocumentRecordId,
                    VersionId = version.Id,
                    upload.FilePath,
                    upload.FileSize,
                    upload.ScannedAtUtc
                });
            }
            else
            {
                throw Validation("AP_MATCH_EXCEPTION_EVIDENCE_KIND_INVALID", "The evidence kind is invalid.");
            }

            rows.Add(new VendorInvoiceMatchExceptionEvidence
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RequirementKey = request.RequirementKey.Trim().ToUpperInvariant(),
                ReferenceKind = request.ReferenceKind,
                WorkflowEvidenceDocumentId = workflowEvidenceId,
                FileUploadRecordId = uploadId,
                EvidenceReference = request.EvidenceReference.Trim(),
                EvidenceHash = hash,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = ActorName
            });
        }
        return rows;
    }

    private async Task<ProcurementControlEventDto> RecordEventAsync(
        VendorInvoiceMatchException item,
        string action,
        ProcurementControlEventResult result,
        string reason,
        object? resultValues,
        CancellationToken cancellationToken)
    {
        return await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "ap-match-exception", TenantId, item.Id, action, item.CorrelationId),
            EventType = VendorInvoiceMatchExceptionRules.EventType,
            Action = action,
            Result = result,
            RuleCode = VendorInvoiceMatchExceptionRules.RuleCode,
            RuleVersion = VendorInvoiceMatchExceptionRules.RuleVersion,
            DecisionKeys = VendorInvoiceMatchExceptionRules.DecisionKeys.ToList(),
            SourceType = "VendorInvoice",
            SourceId = item.VendorInvoiceId,
            SourceReference = item.VendorInvoice?.InvoiceNumber ?? item.VendorInvoiceId.ToString(),
            Reason = reason,
            InputValues = new
            {
                exceptionId = item.Id,
                item.VendorInvoiceId,
                item.PurchaseOrderId,
                item.InvoiceSnapshotHash,
                item.VarianceType,
                item.PriceTolerancePercent,
                item.QuantityTolerancePercent,
                item.RootCauseCategory,
                item.RootCauseDescription,
                item.Justification,
                item.CorrectiveAction,
                item.CorrectiveActionOwnerId,
                item.CorrectiveActionDueAtUtc,
                item.ExpiresAtUtc,
                item.ConfigurationProfileId,
                item.ConfigurationProfileVersion,
                item.WorkflowInstanceId
            },
            ResultValues = resultValues ?? new
            {
                exceptionId = item.Id,
                status = item.Status.ToString(),
                purchaseOrderId = item.PurchaseOrderId,
                workflowInstanceId = item.WorkflowInstanceId,
                invoiceSnapshotHash = item.InvoiceSnapshotHash,
                expiresAtUtc = item.ExpiresAtUtc
            },
            CorrelationId = item.CorrelationId,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = item.Evidence.Select(evidence => new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = evidence.ReferenceKind == VendorInvoiceMatchExceptionEvidenceKind.WorkflowEvidenceDocument
                    ? ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument
                    : ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                ReferenceId = evidence.WorkflowEvidenceDocumentId ?? evidence.FileUploadRecordId,
                Reference = evidence.EvidenceReference,
                Label = evidence.RequirementKey,
                RequirementKey = evidence.RequirementKey
            }).ToList()
        }, cancellationToken);
    }

    private IQueryable<VendorInvoiceMatchException> ExceptionQuery() =>
        _db.Set<VendorInvoiceMatchException>()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .Include(item => item.VendorInvoice)
            .Include(item => item.PurchaseOrder)
            .Include(item => item.Variances)
            .Include(item => item.Evidence)
            .Include(item => item.Actions);

    private async Task<VendorInvoiceMatchException> LoadAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ExceptionQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound(
                "AP_MATCH_EXCEPTION_NOT_FOUND", "The match exception was not found in the current tenant.");
    }

    private async Task<List<Guid>> ApprovedActorIdsAsync(Guid workflowId, CancellationToken cancellationToken) =>
        await _db.WorkflowApprovals.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                           item.StepInstance.WorkflowInstanceId == workflowId &&
                           item.Status == WorkflowApprovalStatus.Approved && item.ProcessedById.HasValue)
            .Select(item => item.ProcessedById!.Value)
            .ToListAsync(cancellationToken);

    private static VendorInvoiceMatchExceptionDto Map(VendorInvoiceMatchException item, DateTime now) => new()
    {
        Id = item.Id,
        VendorInvoiceId = item.VendorInvoiceId,
        InvoiceNumber = item.VendorInvoice?.InvoiceNumber ?? string.Empty,
        PurchaseOrderId = item.PurchaseOrderId,
        PurchaseOrderNumber = item.PurchaseOrder?.OrderNumber ?? string.Empty,
        Sequence = item.Sequence,
        Status = VendorInvoiceMatchExceptionRules.EffectiveStatus(item.Status, item.ExpiresAtUtc, now),
        VarianceType = item.VarianceType,
        PriceTolerancePercent = item.PriceTolerancePercent,
        QuantityTolerancePercent = item.QuantityTolerancePercent,
        RootCauseCategory = item.RootCauseCategory,
        RootCauseDescription = item.RootCauseDescription,
        Justification = item.Justification,
        CorrectiveAction = item.CorrectiveAction,
        CorrectiveActionOwnerId = item.CorrectiveActionOwnerId,
        CorrectiveActionOwnerName = item.CorrectiveActionOwnerName,
        CorrectiveActionDueAtUtc = item.CorrectiveActionDueAtUtc,
        CorrectiveActionStatus = item.CorrectiveActionStatus,
        CorrectiveActionCompletedAtUtc = item.CorrectiveActionCompletedAtUtc,
        CorrectiveActionCompletedById = item.CorrectiveActionCompletedById,
        CorrectiveActionCompletionNote = item.CorrectiveActionCompletionNote,
        ExpiresAtUtc = item.ExpiresAtUtc,
        InvoiceSnapshotHash = item.InvoiceSnapshotHash,
        ConfigurationProfileId = item.ConfigurationProfileId,
        ConfigurationProfileVersion = item.ConfigurationProfileVersion,
        WorkflowInstanceId = item.WorkflowInstanceId,
        RequestedById = item.RequestedById,
        RequestedByName = item.RequestedByName,
        RequestedAtUtc = item.RequestedAtUtc,
        FinalApprovedById = item.FinalApprovedById,
        FinalApprovedByName = item.FinalApprovedByName,
        FinalApprovedAtUtc = item.FinalApprovedAtUtc,
        ApprovalControlEventId = item.ApprovalControlEventId,
        IntegrityHash = item.IntegrityHash,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        Variances = item.Variances.OrderBy(value => value.CreatedAt).Select(value =>
            new VendorInvoiceMatchExceptionVarianceDto
            {
                Id = value.Id,
                VarianceType = value.VarianceType,
                ItemDescription = value.ItemDescription,
                ActualValue = value.ActualValue,
                ExpectedValue = value.ExpectedValue,
                Variance = value.Variance,
                VariancePercentage = value.VariancePercentage,
                ConfiguredTolerancePercent = value.ConfiguredTolerancePercent
            }).ToList(),
        Evidence = item.Evidence.OrderBy(value => value.CreatedAt).Select(value =>
            new VendorInvoiceMatchExceptionEvidenceDto
            {
                Id = value.Id,
                RequirementKey = value.RequirementKey,
                ReferenceKind = value.ReferenceKind,
                WorkflowEvidenceDocumentId = value.WorkflowEvidenceDocumentId,
                FileUploadRecordId = value.FileUploadRecordId,
                EvidenceReference = value.EvidenceReference,
                EvidenceHash = value.EvidenceHash
            }).ToList(),
        Actions = item.Actions.OrderBy(value => value.Sequence).Select(value =>
            new VendorInvoiceMatchExceptionActionDto
            {
                Id = value.Id,
                Sequence = value.Sequence,
                Action = value.Action,
                FromStatus = value.FromStatus,
                ToStatus = value.ToStatus,
                ActorUserId = value.ActorUserId,
                ActorName = value.ActorName,
                Comment = value.Comment,
                OccurredAtUtc = value.OccurredAtUtc
            }).ToList()
    };

    private void AddAction(
        VendorInvoiceMatchException item,
        string action,
        VendorInvoiceMatchExceptionStatus from,
        VendorInvoiceMatchExceptionStatus to,
        string comment,
        DateTime occurredAt)
    {
        var sequence = item.Actions.Count == 0 ? 1 : item.Actions.Max(value => value.Sequence) + 1;
        item.Actions.Add(new VendorInvoiceMatchExceptionAction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Sequence = sequence,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = ActorId,
            ActorName = ActorName,
            Comment = comment.Trim(),
            OccurredAtUtc = occurredAt,
            IntegrityHash = VendorInvoiceMatchExceptionRules.Hash(new
            {
                item.Id,
                sequence,
                action,
                from,
                to,
                ActorId,
                occurredAt,
                Comment = comment.Trim()
            }),
            CreatedAt = occurredAt,
            CreatedBy = ActorName
        });
    }

    private async Task<T> ExecuteAtomicAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (_unitOfWork.HasActiveTransaction) return await operation();
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation();
                await _unitOfWork.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private async Task NotifyAsync(
        string topic,
        VendorInvoiceMatchException item,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = item.TenantId,
                TopicKey = topic,
                NotificationType = VendorInvoiceMatchExceptionRules.EventType,
                EntityType = "VendorInvoice",
                EntityId = item.VendorInvoiceId,
                TriggeredByUserId = ActorId,
                Data = new Dictionary<string, object>
                {
                    ["exceptionId"] = item.Id,
                    ["invoiceId"] = item.VendorInvoiceId,
                    ["invoiceNumber"] = item.VendorInvoice?.InvoiceNumber ?? string.Empty,
                    ["status"] = item.Status.ToString(),
                    ["expiresAtUtc"] = item.ExpiresAtUtc
                },
                Metadata = new Dictionary<string, object>
                {
                    ["correlationId"] = item.CorrelationId,
                    ["ruleCode"] = VendorInvoiceMatchExceptionRules.RuleCode,
                    ["ruleVersion"] = VendorInvoiceMatchExceptionRules.RuleVersion
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish match-exception notification {Topic} for {ExceptionId}", topic, item.Id);
        }
    }

    private static void ValidateRequest(CreateVendorInvoiceMatchExceptionDto request, DateTime now)
    {
        Require(request.RootCauseCategory, "AP_MATCH_EXCEPTION_ROOT_CAUSE_CATEGORY_REQUIRED");
        Require(request.RootCauseDescription, "AP_MATCH_EXCEPTION_ROOT_CAUSE_REQUIRED");
        Require(request.Justification, "AP_MATCH_EXCEPTION_JUSTIFICATION_REQUIRED");
        Require(request.CorrectiveAction, "AP_MATCH_EXCEPTION_CORRECTIVE_ACTION_REQUIRED");
        Require(request.IdempotencyKey, "AP_MATCH_EXCEPTION_IDEMPOTENCY_REQUIRED");
        if (request.Evidence.Any(item => string.Equals(
                item.RequirementKey?.Trim(),
                VendorInvoiceMatchExceptionRules.CorrectiveCompletionEvidenceKey,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw Validation(
                "AP_MATCH_EXCEPTION_COMPLETION_EVIDENCE_RESERVED",
                "Corrective-action completion evidence can only be added after the exception is approved.");
        }
        var expiry = EnsureUtc(request.ExpiresAtUtc);
        var due = EnsureUtc(request.CorrectiveActionDueAtUtc);
        if (request.CorrectiveActionOwnerId == Guid.Empty)
            throw Validation("AP_MATCH_EXCEPTION_OWNER_REQUIRED", "A corrective-action owner is required.");
        if (expiry <= now || expiry > now.AddDays(365))
            throw Validation("AP_MATCH_EXCEPTION_EXPIRY_INVALID",
                "Exception expiry must be in the future and no more than 365 days from request time.");
        if (due <= now)
            throw Validation("AP_MATCH_EXCEPTION_CORRECTIVE_DUE_INVALID",
                "Corrective-action due date must be in the future.");
    }

    private static void EnsureRequiredEvidence(
        IEnumerable<VendorInvoiceMatchExceptionEvidence> evidence,
        IEnumerable<string> required)
    {
        var keys = evidence.Select(item => item.RequirementKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(key => !keys.Contains(key)).ToArray();
        if (missing.Length > 0)
            throw Validation("AP_MATCH_EXCEPTION_EVIDENCE_INCOMPLETE",
                $"Required controlled evidence is missing: {string.Join(", ", missing)}.");
    }

    private static void EnsureOnlyEvidenceKeys(
        IEnumerable<VendorInvoiceMatchExceptionEvidence> evidence,
        IEnumerable<string> allowed)
    {
        var allowedKeys = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unexpected = evidence
            .Select(item => item.RequirementKey)
            .Where(key => !allowedKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unexpected.Length > 0)
            throw Validation(
                "AP_MATCH_EXCEPTION_EVIDENCE_UNEXPECTED",
                $"Unexpected evidence requirements were supplied for this action: {string.Join(", ", unexpected)}.");
    }

    private void EnsureAuthenticatedActor()
    {
        if (!_currentUser.IsAuthenticated || ActorId == Guid.Empty)
            throw Forbidden("AP_MATCH_EXCEPTION_ACTOR_REQUIRED", "An authenticated finance actor is required.");
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException)
        {
            throw Validation("AP_MATCH_EXCEPTION_ROW_VERSION_INVALID", "The row version is invalid.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict("AP_MATCH_EXCEPTION_CONCURRENCY",
                "The exception changed. Reload it before retrying the action.");
    }

    private static string Integrity(VendorInvoiceMatchException item) =>
        VendorInvoiceMatchExceptionRules.Hash(new
        {
            item.Id,
            item.TenantId,
            item.VendorInvoiceId,
            item.PurchaseOrderId,
            item.Sequence,
            item.Status,
            item.VarianceType,
            item.PriceTolerancePercent,
            item.QuantityTolerancePercent,
            item.RootCauseCategory,
            item.RootCauseDescription,
            item.Justification,
            item.CorrectiveAction,
            item.CorrectiveActionOwnerId,
            item.CorrectiveActionDueAtUtc,
            item.CorrectiveActionStatus,
            item.ExpiresAtUtc,
            item.InvoiceSnapshotHash,
            item.ConfigurationProfileId,
            item.ConfigurationProfileVersion,
            item.WorkflowInstanceId,
            item.RequestedById,
            item.FinalApprovedById,
            item.ApprovalControlEventId,
            Evidence = item.Evidence.OrderBy(value => value.RequirementKey)
                .Select(value => new { value.RequirementKey, value.EvidenceHash }),
            Variances = item.Variances.OrderBy(value => value.Id)
                .Select(value => new { value.VarianceType, value.ActualValue, value.ExpectedValue, value.VariancePercentage })
        });

    private static string RequestFingerprint(
        Guid vendorInvoiceId,
        CreateVendorInvoiceMatchExceptionDto request) =>
        VendorInvoiceMatchExceptionRules.Hash(new
        {
            VendorInvoiceId = vendorInvoiceId,
            RootCauseCategory = request.RootCauseCategory.Trim(),
            RootCauseDescription = request.RootCauseDescription.Trim(),
            Justification = request.Justification.Trim(),
            CorrectiveAction = request.CorrectiveAction.Trim(),
            request.CorrectiveActionOwnerId,
            CorrectiveActionDueAtUtc = EnsureUtc(request.CorrectiveActionDueAtUtc),
            ExpiresAtUtc = EnsureUtc(request.ExpiresAtUtc),
            Evidence = request.Evidence
                .Select(item => new
                {
                    RequirementKey = item.RequirementKey.Trim().ToUpperInvariant(),
                    item.ReferenceKind,
                    item.WorkflowEvidenceDocumentId,
                    item.FileUploadRecordId,
                    EvidenceReference = item.EvidenceReference.Trim()
                })
                .OrderBy(item => item.RequirementKey, StringComparer.Ordinal)
                .ThenBy(item => item.ReferenceKind)
                .ThenBy(item => item.WorkflowEvidenceDocumentId)
                .ThenBy(item => item.FileUploadRecordId)
                .ThenBy(item => item.EvidenceReference, StringComparer.Ordinal)
                .ToArray()
        });

    private static string RequestFingerprint(VendorInvoiceMatchException item) =>
        VendorInvoiceMatchExceptionRules.Hash(new
        {
            VendorInvoiceId = item.VendorInvoiceId,
            RootCauseCategory = item.RootCauseCategory.Trim(),
            RootCauseDescription = item.RootCauseDescription.Trim(),
            Justification = item.Justification.Trim(),
            CorrectiveAction = item.CorrectiveAction.Trim(),
            item.CorrectiveActionOwnerId,
            CorrectiveActionDueAtUtc = EnsureUtc(item.CorrectiveActionDueAtUtc),
            ExpiresAtUtc = EnsureUtc(item.ExpiresAtUtc),
            Evidence = item.Evidence
                .Where(evidence => !string.Equals(
                    evidence.RequirementKey,
                    VendorInvoiceMatchExceptionRules.CorrectiveCompletionEvidenceKey,
                    StringComparison.OrdinalIgnoreCase))
                .Select(evidence => new
                {
                    RequirementKey = evidence.RequirementKey.Trim().ToUpperInvariant(),
                    evidence.ReferenceKind,
                    evidence.WorkflowEvidenceDocumentId,
                    evidence.FileUploadRecordId,
                    EvidenceReference = evidence.EvidenceReference.Trim()
                })
                .OrderBy(evidence => evidence.RequirementKey, StringComparer.Ordinal)
                .ThenBy(evidence => evidence.ReferenceKind)
                .ThenBy(evidence => evidence.WorkflowEvidenceDocumentId)
                .ThenBy(evidence => evidence.FileUploadRecordId)
                .ThenBy(evidence => evidence.EvidenceReference, StringComparer.Ordinal)
                .ToArray()
        });

    private static string Normalize(string? value, int maximum)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();
        return normalized[..Math.Min(normalized.Length, maximum)];
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private static void Require(string? value, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Validation(code, "A required match-exception value is missing.");
    }

    private static VendorInvoiceMatchExceptionControlException Validation(string code, string message) =>
        new(code, message, 422);
    private static VendorInvoiceMatchExceptionControlException Conflict(string code, string message) =>
        new(code, message, 409);
    private static VendorInvoiceMatchExceptionControlException NotFound(string code, string message) =>
        new(code, message, 404);
    private static VendorInvoiceMatchExceptionControlException Forbidden(string code, string message) =>
        new(code, message, 403);
}
