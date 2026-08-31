using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/approvals")]
public class FinanceApprovalsController : ControllerBase
{
    private static readonly JsonSerializerOptions PaymentControlJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> FinanceWorkflowEntityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        Normalize("JournalEntry"),
        Normalize("FinancePurchaseOrder"),
        Normalize("FinancePurchaseOrderReceipt"),
        Normalize("VendorInvoice"),
        Normalize("VendorPayment"),
        Normalize("PaymentBatch"),
        Normalize("SupplierReturn"),
        Normalize("Quote"),
        Normalize("SalesOrder"),
        Normalize("DeliveryNote"),
        Normalize("Invoice"),
        Normalize("ReturnOrder"),
        Normalize("CreditNote"),
        Normalize("CustomerPayment"),
        Normalize("Refund"),
        Normalize("BudgetScenario"),
        Normalize("BudgetReturn"),
        Normalize("BudgetRevision"),
        Normalize("FinanceBudgetOverride"),
        Normalize("UnitJournalEntry"),
        Normalize("UnitAccountBudget"),
        Normalize("AllocationRule"),
        Normalize("AllocationRunBatch"),
        Normalize("CashTransaction"),
        Normalize("BankReconciliation"),
        Normalize("OpeningBalanceBatch"),
        Normalize("FixedAsset"),
        Normalize("AssetDepreciationSchedule"),
        Normalize("FixedAssetDepreciationRun"),
        Normalize("AssetValuation"),
        Normalize("AssetTransfer"),
        Normalize("AssetDisposal"),
        Normalize("AssetVerificationSession"),
        Normalize("CapitalProject"),
        Normalize("LeaseContract")
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IWorkflowService _workflowService;
    private readonly IWorkflowEntityDisplayService _displayService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly IInvoiceService _invoiceService;
    private readonly IVendorInvoiceService? _vendorInvoiceService;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly FinancePurchaseOrderReceiptPostingService _receiptPostingService;
    private readonly ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService _inventoryValuationService;
    private readonly ILogger<FinanceApprovalsController> _logger;
    private readonly IProcurementInvoicePaymentSodService? _invoicePaymentSod;
    private readonly IVendorPaymentService? _vendorPaymentService;
    private readonly IFinanceBudgetControlService? _budgetControl;

    public FinanceApprovalsController(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        IWorkflowService workflowService,
        IWorkflowEntityDisplayService displayService,
        IJournalEntryService journalEntryService,
        IInvoiceService invoiceService,
        ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService inventoryValuationService,
        FinancePurchaseOrderReceiptPostingService receiptPostingService,
        ILogger<FinanceApprovalsController> logger,
        IVendorInvoiceService? vendorInvoiceService = null,
        IFinanceAuditService? financeAuditService = null,
        IProcurementInvoicePaymentSodService? invoicePaymentSod = null,
        IVendorPaymentService? vendorPaymentService = null,
        IFinanceBudgetControlService? budgetControl = null)
    {
        _db = db;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _workflowService = workflowService;
        _displayService = displayService;
        _journalEntryService = journalEntryService;
        _invoiceService = invoiceService;
        _inventoryValuationService = inventoryValuationService;
        _receiptPostingService = receiptPostingService;
        _vendorInvoiceService = vendorInvoiceService;
        _financeAuditService = financeAuditService;
        _logger = logger;
        _invoicePaymentSod = invoicePaymentSod;
        _vendorPaymentService = vendorPaymentService;
        _budgetControl = budgetControl;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<FinanceApprovalQueueItemDto>>> GetPending(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest("Page must be at least 1 and pageSize must be between 1 and 100.");
        }
        if (page > (int.MaxValue / pageSize) + 1)
        {
            return BadRequest("The requested approval page is outside the supported range.");
        }
        var skip = (page - 1) * pageSize;

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var tenantId = TenantId;
        var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var canApproveByPermission = (await _authorizationService
            .AuthorizeAsync(User, FinancePermissions.WorkflowApprove)).Succeeded;
        var canApproveApPayments = (await _authorizationService
            .AuthorizeAsync(User, FinancePermissions.ApproveApPayments)).Succeeded;
        var canRejectByPermission = (await _authorizationService
            .AuthorizeAsync(User, FinancePermissions.WorkflowReject)).Succeeded;

        var currentRoles = roleSet.ToArray();
        var pageRows = await QueryPendingApprovals(tenantId)
            .Where(approval =>
                approval.ApproverId == currentUserId.Value ||
                (approval.ApproverRole != null && currentRoles.Contains(approval.ApproverRole)))
            .AsNoTracking()
            .OrderBy(approval => approval.StepInstance.WorkflowInstance.StartedDate ??
                                 approval.StepInstance.WorkflowInstance.CreatedDate)
            .ThenBy(approval => approval.Id)
            .Skip(skip)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);
        Response.Headers["X-Page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Page-Size"] = pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Has-More"] = (pageRows.Count > pageSize).ToString().ToLowerInvariant();
        var approvals = pageRows
            .Take(pageSize)
            .Where(approval => CanActOnApproval(approval, currentUserId.Value, roleSet))
            .Where(approval => IsFinanceEntity(
                approval.StepInstance.WorkflowInstance.EntityType.Code ??
                approval.StepInstance.WorkflowInstance.EntityType.Name))
            .ToList();

        ProcurementInvoicePaymentSodQueueReadinessDto? queueReadiness = null;
        string? queueReadinessFailure = null;
        if (canApproveApPayments && _invoicePaymentSod != null)
        {
            var paymentIds = approvals
                .Where(approval => Normalize(
                    approval.StepInstance.WorkflowInstance.EntityType.Code ??
                    approval.StepInstance.WorkflowInstance.EntityType.Name) == Normalize("VendorPayment"))
                .Select(approval => approval.StepInstance.WorkflowInstance.EntityId)
                .Distinct()
                .ToArray();
            var batchIds = approvals
                .Where(approval => Normalize(
                    approval.StepInstance.WorkflowInstance.EntityType.Code ??
                    approval.StepInstance.WorkflowInstance.EntityType.Name) == Normalize("PaymentBatch"))
                .Select(approval => approval.StepInstance.WorkflowInstance.EntityId)
                .Distinct()
                .ToArray();
            if (paymentIds.Length > 0 || batchIds.Length > 0)
            {
                try
                {
                    queueReadiness = await _invoicePaymentSod.GetQueueReadinessAsync(
                        paymentIds,
                        batchIds,
                        HttpContext.TraceIdentifier,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    queueReadinessFailure = exception.Message;
                    _logger.LogWarning(exception,
                        "Unable to evaluate the paged Finance payment approval queue SOD readiness.");
                }
            }
        }

        var results = new List<FinanceApprovalQueueItemDto>();
        foreach (var approval in approvals)
        {
            var instance = approval.StepInstance.WorkflowInstance;
            var entityType = instance.EntityType.Code ?? instance.EntityType.Name;
            if (!IsFinanceEntity(entityType))
            {
                continue;
            }

            var submitterApprovalBlocked =
                RequiresSubmitterApproverSeparation(entityType) &&
                instance.InitiatedById == currentUserId.Value;
            var paymentSodBlocked = false;
            string? paymentSodReason = null;
            var isPaymentApproval =
                string.Equals(Normalize(entityType), Normalize("VendorPayment"), StringComparison.Ordinal) ||
                string.Equals(Normalize(entityType), Normalize("PaymentBatch"), StringComparison.Ordinal);
            if (isPaymentApproval)
            {
                if (!canApproveApPayments)
                {
                    paymentSodBlocked = true;
                    paymentSodReason = $"Your roles do not include {FinancePermissions.ApproveApPayments}.";
                }
                else if (_invoicePaymentSod == null)
                {
                    paymentSodBlocked = true;
                    paymentSodReason = "The authoritative invoice/payment SOD service is unavailable.";
                }
                else
                {
                    ProcurementInvoicePaymentSodReadinessDto? sod = null;
                    var readinessFound = Normalize(entityType) == Normalize("VendorPayment")
                        ? queueReadiness?.Payments.TryGetValue(instance.EntityId, out sod) == true
                        : queueReadiness?.Batches.TryGetValue(instance.EntityId, out sod) == true;
                    if (!string.IsNullOrWhiteSpace(queueReadinessFailure))
                    {
                        paymentSodBlocked = true;
                        paymentSodReason = queueReadinessFailure;
                    }
                    else if (!readinessFound || sod == null)
                    {
                        paymentSodBlocked = true;
                        paymentSodReason = "The payment approval source was not found in the current tenant.";
                    }
                    else
                    {
                        paymentSodBlocked = !sod.CanApprove;
                        paymentSodReason = paymentSodBlocked ? sod.Message : null;
                    }
                }
            }
            var approveDisabledReason = GetActionDisabledReason(
                "approve",
                FinancePermissions.WorkflowApprove,
                canApproveByPermission,
                submitterApprovalBlocked || paymentSodBlocked,
                paymentSodReason);
            var rejectDisabledReason = GetActionDisabledReason(
                "reject",
                FinancePermissions.WorkflowReject,
                canRejectByPermission,
                submitterApprovalBlocked);

            results.Add(await MapApprovalAsync(
                approval,
                canApproveByPermission && !submitterApprovalBlocked && !paymentSodBlocked,
                canRejectByPermission && !submitterApprovalBlocked,
                approveDisabledReason,
                rejectDisabledReason,
                cancellationToken));
        }

        return Ok(results
            .OrderBy(item => item.SubmittedAt ?? DateTime.MaxValue)
            .ThenBy(item => item.Reference)
            .ToList());
    }

    [HttpPost("{approvalId:guid}/approve")]
    public async Task<ActionResult<WorkflowExecutionResult>> Approve(Guid approvalId, [FromBody] FinanceApprovalActionRequest? request, CancellationToken cancellationToken)
        => await ProcessApprovalAsync(approvalId, "Approve", request?.Comments, cancellationToken);

    [HttpPost("{approvalId:guid}/reject")]
    public async Task<ActionResult<WorkflowExecutionResult>> Reject(Guid approvalId, [FromBody] FinanceApprovalActionRequest? request, CancellationToken cancellationToken)
    {
        var reason = request?.Reason ?? request?.Comments;
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest("A rejection reason is required.");
        }

        return await ProcessApprovalAsync(approvalId, "Reject", reason, cancellationToken);
    }

    private async Task<ActionResult<WorkflowExecutionResult>> ProcessApprovalAsync(
        Guid approvalId,
        string action,
        string? comments,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var tenantId = TenantId;
        var approval = await QueryPendingApprovals(tenantId)
            .FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
        if (approval == null)
        {
            return NotFound();
        }

        var instance = approval.StepInstance.WorkflowInstance;
        var entityType = instance.EntityType.Code ?? instance.EntityType.Name;
        if (!IsFinanceEntity(entityType))
        {
            return BadRequest("This approval is not a finance workflow approval.");
        }

        var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (!CanActOnApproval(approval, currentUserId.Value, roleSet))
        {
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "WORKFLOW",
                entityType,
                instance.EntityId,
                FinanceAuditEvents.FinanceWorkflowApproverPermissionRejected,
                new { approvalId, currentUserId },
                comments,
                cancellationToken);
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Approval action not permitted",
                detail: "This approval is not assigned to your user or any of your current roles.");
        }

        if (RequiresSubmitterApproverSeparation(entityType) && instance.InitiatedById == currentUserId.Value)
        {
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "WORKFLOW",
                entityType,
                instance.EntityId,
                FinanceAuditEvents.FinanceWorkflowApproverPermissionRejected,
                new { approvalId, currentUserId, reason = "Submitter self-approval is blocked for this high-risk Finance workflow." },
                comments,
                cancellationToken);
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Approval action not permitted",
                detail: "The submitter cannot approve or reject this high-risk finance workflow item.");
        }

        // The workflow assignment check above answers "is this item assigned to me?"; the
        // Finance permission check answers the separate question "may I approve AP payments?".
        // Preserve both gates before evaluating evidence so an unauthorised user cannot probe
        // payment-control details through validation messages.
        if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase) &&
            (Normalize(entityType) is "VENDORPAYMENT" or "PAYMENTBATCH") &&
            !(await _authorizationService.AuthorizeAsync(User, FinancePermissions.ApproveApPayments)).Succeeded)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Payment approval not permitted",
                detail: $"Your roles do not include {FinancePermissions.ApproveApPayments}.");
        }

        if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase) &&
            Normalize(entityType) == Normalize("VendorPayment"))
        {
            var evidenceError = await ValidateVendorPaymentEvidenceForApprovalAsync(
                tenantId,
                instance,
                cancellationToken);
            if (evidenceError != null)
            {
                await RecordFinanceWorkflowAuditAsync(
                    tenantId,
                    "WORKFLOW",
                    entityType,
                    instance.EntityId,
                    FinanceAuditEvents.ApPaymentEvidenceApprovalBlocked,
                    new { approvalId, currentUserId, reason = evidenceError },
                    comments,
                    cancellationToken);
                return BadRequest(new WorkflowExecutionResult
                {
                    Success = false,
                    Status = instance.Status,
                    WorkflowInstanceId = instance.Id,
                    CurrentStepId = instance.CurrentStepId,
                    Message = evidenceError
                });
            }
        }

        // Batch approval is a domain operation rather than a generic workflow-only transition:
        // the service freezes allocations and rechecks the same payment controls transactionally.
        if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase) &&
            Normalize(entityType) == Normalize("PaymentBatch"))
        {
            if (_vendorPaymentService == null)
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Payment batch approval unavailable",
                    detail: "The authoritative payment batch service is unavailable.");
            try
            {
                var batch = await _vendorPaymentService.ApprovePaymentBatchAsync(instance.EntityId, cancellationToken);
                return Ok(new WorkflowExecutionResult
                {
                    Success = true,
                    Status = batch.Status == PaymentBatchStatus.Approved
                        ? WorkflowInstanceStatus.Completed
                        : WorkflowInstanceStatus.InProgress,
                    Message = batch.Status == PaymentBatchStatus.Approved
                        ? "Payment batch approved."
                        : "Payment batch approval step recorded."
                });
            }
            catch (ProcurementInvoicePaymentSodBlockedException exception)
            {
                return UnprocessableEntity(new { code = exception.Code, message = exception.Message, readiness = exception.Readiness });
            }
            catch (VendorPaymentControlException exception)
            {
                return UnprocessableEntity(new { code = exception.Code, message = exception.Message });
            }
        }

        Guid? invoicePaymentSodControlEventId = null;
        if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase) &&
            Normalize(entityType) == Normalize("VendorPayment"))
        {
            if (_invoicePaymentSod == null)
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Payment authorization unavailable",
                    detail: "The authoritative invoice/payment SOD service is unavailable.");
            try
            {
                var sod = await _invoicePaymentSod.EnforcePaymentApprovalAsync(
                    instance.EntityId,
                    HttpContext.TraceIdentifier,
                    cancellationToken);
                invoicePaymentSodControlEventId = sod.ControlEventId;
            }
            catch (ProcurementInvoicePaymentSodBlockedException exception)
            {
                return UnprocessableEntity(new { code = exception.Code, message = exception.Message, readiness = exception.Readiness });
            }
        }

        if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase) &&
            Normalize(entityType) == Normalize("JournalEntry"))
        {
            if (_budgetControl == null)
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Finance budget control unavailable",
                    detail: "The authoritative Finance budget-control service is unavailable.");
            try
            {
                // Recheck at every approval stage. This occurs before the workflow transition,
                // so a budget failure cannot consume an approver's task or complete the workflow.
                await _budgetControl.ValidateManualJournalForPostingAsync(instance.EntityId, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new WorkflowExecutionResult
                {
                    Success = false,
                    Status = instance.Status,
                    WorkflowInstanceId = instance.Id,
                    CurrentStepId = instance.CurrentStepId,
                    Message = exception.Message
                });
            }
        }

        var workflowResult = await ProcessWorkflowAndOutcomeAtomicallyAsync(
            tenantId,
            entityType,
            instance.EntityId,
            currentUserId.Value,
            action,
            comments,
            invoicePaymentSodControlEventId,
            cancellationToken);

        if (!workflowResult.Success)
        {
            return BadRequest(workflowResult);
        }

        return Ok(workflowResult);
    }

    private async Task<string?> ValidateVendorPaymentEvidenceForApprovalAsync(
        Guid tenantId,
        WorkflowInstance instance,
        CancellationToken cancellationToken)
    {
        var payment = await _db.Set<VendorPayment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == instance.EntityId &&
                !item.IsDeleted,
                cancellationToken);
        if (payment == null)
            return "The vendor payment no longer exists for this tenant.";
        if (string.IsNullOrWhiteSpace(payment.ApprovalControlSnapshotJson) ||
            string.IsNullOrWhiteSpace(payment.ApprovalControlSnapshotHash))
        {
            return "The payment has no immutable approval/evidence policy snapshot. Return it to Draft and resubmit under a published policy.";
        }

        var calculatedSnapshotHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(payment.ApprovalControlSnapshotJson)));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(calculatedSnapshotHash),
                TryDecodeHex(payment.ApprovalControlSnapshotHash)))
        {
            // A database or application defect must not be able to change evidence/authority
            // requirements after submission while leaving an apparently valid workflow in place.
            return "The payment approval/evidence snapshot integrity check failed. Administrator review and controlled resubmission are required.";
        }

        WorkflowApprovalConfigDto control;
        try
        {
            control = JsonSerializer.Deserialize<WorkflowApprovalConfigDto>(
                payment.ApprovalControlSnapshotJson,
                PaymentControlJsonOptions)
                ?? throw new JsonException("Empty payment control snapshot.");
        }
        catch (JsonException)
        {
            return "The payment approval/evidence policy snapshot is invalid and requires administrator review.";
        }

        // An evidence exception is never a silent bypass. Submission has already forced the MD
        // approval group, and final outcome handling verifies that this group actually approved.
        if (payment.EvidenceExceptionRequested)
        {
            return control.AllowEvidenceException
                ? null
                : "The snapshotted payment policy does not permit an evidence exception.";
        }

        var stepIds = await _db.WorkflowStepInstances
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.WorkflowInstanceId == instance.Id &&
                !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var evidence = await _db.WorkflowEvidenceDocuments
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                stepIds.Contains(item.StepInstanceId) &&
                item.IsCurrent &&
                !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var failures = new List<string>();
        foreach (var requirement in control.EvidenceRequirements
                     .Where(item => !string.IsNullOrWhiteSpace(item.RequirementKey)))
        {
            var valid = evidence.Where(item =>
                string.Equals(item.RequirementKey, requirement.RequirementKey, StringComparison.OrdinalIgnoreCase) &&
                item.MalwareScanStatus == WorkflowMalwareScanStatus.Clean &&
                (!item.ExpiryDate.HasValue || item.ExpiryDate.Value >= DateTime.UtcNow));
            if (requirement.RequireVerification)
            {
                // Verification by the uploader would collapse evidence preparation and checking
                // into the same act. Count only an independently verified clean document.
                valid = valid.Where(item =>
                    item.VerificationStatus == WorkflowEvidenceVerificationStatus.Verified &&
                    item.VerifiedById.HasValue &&
                    item.VerifiedById.Value != item.UploadedById);
            }

            var requiredCount = Math.Max(requirement.MinimumDocuments, 1);
            var actualCount = valid.Count();
            if (actualCount < requiredCount)
            {
                var label = string.IsNullOrWhiteSpace(requirement.DocumentName)
                    ? requirement.RequirementKey
                    : requirement.DocumentName;
                failures.Add($"{label}: {actualCount} of {requiredCount} acceptable document(s)");
            }
        }

        return failures.Count == 0
            ? null
            : $"Payment approval is blocked by supporting-evidence policy: {string.Join("; ", failures)}. Upload clean evidence and have a different authorized reviewer verify it, or resubmit with an allowed evidence-exception request.";
    }

    private static byte[] TryDecodeHex(string value)
    {
        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            // FixedTimeEquals also requires equal length. Returning a deliberately different
            // length turns malformed stored hashes into a safe integrity failure.
            return Array.Empty<byte>();
        }
    }

    private async Task<WorkflowExecutionResult> ProcessWorkflowAndOutcomeAtomicallyAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        Guid currentUserId,
        string action,
        string? comments,
        Guid? invoicePaymentSodControlEventId,
        CancellationToken cancellationToken)
    {
        async Task<WorkflowExecutionResult> ProcessAndApplyAsync()
        {
            var result = await _workflowService.ProcessApprovalStepAsync(
                entityType,
                entityId,
                currentUserId,
                action,
                comments);

            if (!result.Success)
            {
                return result;
            }

            if (result.Status == WorkflowInstanceStatus.Completed)
            {
                await ApplyApprovedOutcomeAsync(tenantId, entityType, entityId, currentUserId, comments, invoicePaymentSodControlEventId, cancellationToken);
            }
            else if (result.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
            {
                await ApplyRejectedOutcomeAsync(tenantId, entityType, entityId, currentUserId, comments, cancellationToken);
            }

            return result;
        }

        // Workflow repositories, Finance outcome services, and the posting engine share this scoped
        // DbContext. Keep the final workflow state and its business/GL outcome in one transaction so
        // a failed outcome leaves the approval pending and retryable instead of consuming it.
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction != null)
        {
            return await ProcessAndApplyAsync();
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await ProcessAndApplyAsync();
                if (!result.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    return result;
                }

                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    internal IQueryable<WorkflowApproval> QueryPendingApprovals(Guid tenantId)
        => _db.WorkflowApprovals
            .Include(a => a.StepInstance)
                .ThenInclude(si => si.WorkflowStep)
            .Include(a => a.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.EntityType)
            .Include(a => a.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.WorkflowDefinition)
            .Include(a => a.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.InitiatedBy)
            .Where(a =>
                a.TenantId == tenantId &&
                a.Status == WorkflowApprovalStatus.Pending &&
                !a.IsDeleted &&
                !a.StepInstance.IsDeleted &&
                !a.StepInstance.WorkflowInstance.IsDeleted &&
                // Approval rows are retained as immutable workflow history. Only the active
                // instance/current step is actionable; otherwise an earlier Pending row can
                // reappear after the Finance document has already been approved and posted.
                (a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Created ||
                 a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.InProgress ||
                 a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Waiting ||
                 a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Suspended) &&
                a.StepInstance.WorkflowInstance.CurrentStepId.HasValue &&
                a.StepInstance.WorkflowStepId == a.StepInstance.WorkflowInstance.CurrentStepId.Value &&
                (a.StepInstance.Status == WorkflowStepInstanceStatus.Pending ||
                 a.StepInstance.Status == WorkflowStepInstanceStatus.InProgress));

    private async Task<FinanceApprovalQueueItemDto> MapApprovalAsync(
        WorkflowApproval approval,
        bool canApprove,
        bool canReject,
        string? approveDisabledReason,
        string? rejectDisabledReason,
        CancellationToken cancellationToken)
    {
        var instance = approval.StepInstance.WorkflowInstance;
        var entityType = instance.EntityType.Code ?? instance.EntityType.Name;
        var display = await _displayService.GetEntityDisplayInfoAsync(entityType, instance.EntityId);
        var facts = await ResolveFactsAsync(approval.TenantId, entityType, instance.EntityId, cancellationToken);
        var reference = FirstNonEmpty(display.EntityNumber, facts.Reference, instance.EntityId.ToString("N")[..8].ToUpperInvariant());
        var title = FirstNonEmpty(display.EntityName, facts.Title, display.EntityType, entityType);
        var detailHref = ResolveDetailHref(entityType, instance.EntityId, display.ActionUrl);
        if (Normalize(entityType) == "FINANCEBUDGETOVERRIDE")
        {
            var journalId = await _db.FinanceBudgetOverrideRequests.AsNoTracking()
                .Where(x => x.TenantId == approval.TenantId && x.Id == instance.EntityId && !x.IsDeleted)
                .Select(x => (Guid?)x.SourceDocumentId)
                .FirstOrDefaultAsync(cancellationToken);
            if (journalId.HasValue)
                detailHref = $"/finance/journal-entries/{journalId.Value:D}";
        }

        return new FinanceApprovalQueueItemDto
        {
            ApprovalId = approval.Id,
            EntityId = instance.EntityId,
            EntityType = display.EntityType,
            Reference = reference,
            Title = title,
            DetailHref = detailHref,
            DocumentType = GetDocumentType(entityType),
            Module = GetModule(entityType),
            CurrentStep = approval.StepInstance.WorkflowStep?.Name ?? "Approval",
            StatusLabel = facts.StatusLabel ?? "Pending Approval",
            SubmittedAt = instance.StartedDate ?? instance.CreatedDate,
            Amount = facts.Amount,
            CurrencyCode = facts.CurrencyCode,
            SubmittedBy = instance.InitiatedBy == null
                ? null
                : string.Join(" ", new[] { instance.InitiatedBy.FirstName, instance.InitiatedBy.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
            ApproverRole = approval.ApproverRole,
            WorkflowName = instance.WorkflowDefinition?.Name,
            CanApprove = canApprove,
            CanReject = canReject,
            ApproveDisabledReason = approveDisabledReason,
            RejectDisabledReason = rejectDisabledReason,
            Metadata = facts.Metadata
        };
    }

    private async Task<FinanceApprovalFacts> ResolveFactsAsync(Guid tenantId, string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        var key = Normalize(entityType);

        if (key == Normalize("FinanceBudgetOverride"))
        {
            var item = await _db.FinanceBudgetOverrideRequests.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted, cancellationToken);
            if (item == null)
                return FinanceApprovalFacts.Empty;
            var journalNumber = await _db.JournalEntries.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == item.SourceDocumentId && !x.IsDeleted)
                .Select(x => x.JournalEntryNumber)
                .FirstOrDefaultAsync(cancellationToken);
            return new(
                $"Budget override - {journalNumber ?? item.SourceDocumentId.ToString()}",
                item.Reason,
                item.Status,
                item.RequestedAt,
                item.ShortfallAmount,
                item.CurrencyCode);
        }

        if (key == Normalize("JournalEntry"))
        {
            var item = await _db.JournalEntries.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.JournalEntryNumber, item.Description, item.PostingStatus, item.EntryDate, item.TotalDebitAmount, item.PrimaryCurrency ?? "GHS");
        }

        if (key == Normalize("FinancePurchaseOrder"))
        {
            var item = await _db.FinancePurchaseOrders.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.OrderNumber, null, MapFinancePurchaseOrderStatus(item.Status), item.OrderDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("FinancePurchaseOrderReceipt"))
        {
            var item = await _db.FinancePurchaseOrderReceipts
                .AsNoTracking()
                .Include(x => x.FinancePurchaseOrder)
                    .ThenInclude(x => x.Vendor)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null
                ? FinanceApprovalFacts.Empty
                : new(item.ReceiptNumber, item.FinancePurchaseOrder?.Vendor?.PartnerName, item.Status.ToString(), item.ReceiptDate, null, item.FinancePurchaseOrder?.CurrencyCode);
        }

        if (key == Normalize("VendorInvoice"))
        {
            var item = await _db.VendorInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.InvoiceNumber, item.SupplierName, item.Status.ToString(), item.InvoiceDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("Invoice"))
        {
            var item = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.InvoiceNumber, item.CustomerName, item.Status.ToString(), item.InvoiceDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("VendorPayment"))
        {
            var item = await _db.Set<VendorPayment>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.PaymentNumber, null, item.Status.ToString(), item.PaymentDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("PaymentBatch"))
        {
            var item = await _db.Set<PaymentBatch>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.BatchNumber, item.Description, item.Status.ToString(), item.BatchDate, item.TotalAmount, null);
        }

        if (key == Normalize("SupplierReturn"))
        {
            var item = await _db.SupplierReturns.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ReturnNumber, item.VendorName, item.Status.ToString(), item.ReturnDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("CustomerPayment"))
        {
            var item = await _db.Set<CustomerPayment>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.PaymentNumber, null, item.Status, item.PaymentDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("BudgetReturn"))
        {
            var item = await _db.BudgetReturns.AsNoTracking().Include(x => x.BudgetScenario).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.BudgetScenario?.Name, item.Notes, item.Status, item.SubmittedDate, null, null);
        }

        if (key == Normalize("BudgetRevision"))
        {
            var item = await _db.BudgetRevisions.AsNoTracking()
                .Include(x => x.SourceScenario)
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null
                ? FinanceApprovalFacts.Empty
                : new(
                    $"{item.RevisionNumber} - {item.RevisionType}",
                    $"{item.SourceScenario?.Name}; Board resolution {item.BoardResolutionReference}; {item.Justification}",
                    item.Status,
                    item.SubmittedAt,
                    // Net change is zero for a valid virement, so the approval inbox
                    // displays the gross increase being authorized rather than an
                    // apparently immaterial zero-value request.
                    item.Lines.Where(line => !line.IsDeleted && line.AdjustmentAmountBase > 0m)
                        .Sum(line => line.AdjustmentAmountBase),
                    item.SourceScenario?.BaseCurrencyCode);
        }

        if (key == Normalize("UnitJournalEntry"))
        {
            var item = await _db.Set<UnitJournalEntry>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.EntryNumber, item.Description, item.Status.ToString(), item.EntryDate, null, null);
        }

        if (key == Normalize("UnitAccountBudget"))
        {
            var item = await _db.Set<UnitAccountBudget>()
                .AsNoTracking()
                .Include(x => x.UnitAccount)
                .Include(x => x.FiscalPeriod)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null
                ? FinanceApprovalFacts.Empty
                : new(item.BudgetVersion, item.UnitAccount?.Name, item.Status, item.FiscalPeriod?.StartDate, item.BudgetQuantity, null);
        }

        if (key == Normalize("AllocationRule"))
        {
            var item = await _db.Set<AllocationRule>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.Code, item.Name, item.ApprovalStatus, item.LastRunDate, null, null);
        }

        if (key == Normalize("AllocationRunBatch"))
        {
            var item = await _db.Set<AllocationRunBatch>()
                .AsNoTracking()
                .Include(x => x.AllocationRule)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null
                ? FinanceApprovalFacts.Empty
                : new(item.BatchNumber, item.AllocationRule?.Name ?? item.Description, item.Status.ToString(), item.AllocationDate, item.TotalAllocated, item.FunctionalCurrencyCode);
        }

        if (key == Normalize("CashTransaction"))
        {
            var item = await _db.Set<CashTransaction>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.TransactionNumber, item.Description ?? item.PayeeOrPayer, item.ApprovalStatus.ToString(), item.TransactionDate, item.Amount, item.Currency);
        }

        if (key == Normalize("BankReconciliation"))
        {
            var item = await _db.Set<BankReconciliation>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new($"REC-{item.ReconciliationDate:yyyyMMdd}", null, item.Status.ToString(), item.ReconciliationDate, item.StatementBalance, null);
        }

        if (key == Normalize("ExchangeRate"))
        {
            var item = await _db.ExchangeRates.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null
                ? FinanceApprovalFacts.Empty
                : new(
                    $"{item.BaseCurrencyCode}/{item.TargetCurrencyCode} {item.RateType} {item.EffectiveDate:yyyy-MM-dd}",
                    item.RateSource,
                    item.ApprovalStatus.ToString(),
                    item.EffectiveDate,
                    item.Rate,
                    item.BaseCurrencyCode);
        }

        if (key == Normalize("OpeningBalanceBatch"))
        {
            var item = await _db.OpeningBalanceBatches
                .AsNoTracking()
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null
                ? FinanceApprovalFacts.Empty
                : new FinanceApprovalFacts(
                    Reference: item.BatchNumber,
                    Title: item.Description ?? item.SourceReference,
                    StatusLabel: item.Status,
                    Date: item.OpeningDate,
                    Amount: item.TotalDebit,
                    // Opening-balance debit/credit totals are functional-currency amounts.
                    // Use the validated line currency instead of period or book metadata.
                    CurrencyCode: item.Lines
                        .Where(line => !line.IsDeleted)
                        .OrderBy(line => line.LineNumber)
                        .Select(line => line.FunctionalCurrencyCode)
                        .FirstOrDefault());
        }

        if (key == Normalize("FixedAsset"))
        {
            var item = await _db.FixedAssets.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.AssetCode, item.Name, item.Status.ToString(), item.PurchaseDate, item.AcquisitionCost, null);
        }

        if (key == Normalize("FixedAssetDepreciationRun") || key == Normalize("AssetDepreciationSchedule"))
        {
            var item = await _db.FixedAssetDepreciationRuns
                .AsNoTracking()
                .Include(x => x.FiscalPeriod)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null
                ? FinanceApprovalFacts.Empty
                : new(
                    $"DEP-{item.FiscalPeriod.PeriodCode}-{item.BookClassification}",
                    item.FixedAssetId.HasValue ? "Single asset depreciation" : "Period depreciation",
                    item.Status,
                    item.PostingDate,
                    item.TotalDepreciationAmount,
                    null);
        }

        if (key == Normalize("AssetValuation"))
        {
            var item = await _db.AssetValuations
                .AsNoTracking()
                .Include(x => x.FixedAsset)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null
                ? FinanceApprovalFacts.Empty
                : new(
                    $"VAL-{item.FixedAsset.AssetCode}-{item.ValuationDate:yyyyMMdd}",
                    item.Reason,
                    item.Status,
                    item.ValuationDate,
                    Math.Abs(item.AdjustmentAmount),
                    item.FixedAsset.FunctionalCurrencyCode);
        }

        if (key == Normalize("AssetTransfer"))
        {
            var item = await _db.AssetTransfers.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ReferenceNumber, item.ToLocation, item.Status.ToString(), item.TransferDate, item.TransferCost, null);
        }

        if (key == Normalize("AssetDisposal"))
        {
            var item = await _db.AssetDisposals.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ReferenceNumber, item.Reason, item.Status.ToString(), item.DisposalDate, item.SaleProceeds, null);
        }

        if (key == Normalize("AssetVerificationSession"))
        {
            var item = await _db.AssetVerificationSessions.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ReferenceNumber, item.SessionName, item.Status.ToString(), item.ScheduledDate, null, null);
        }

        if (key == Normalize("CapitalProject"))
        {
            var item = await _db.CapitalProjects.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ProjectCode, item.Name, item.Status.ToString(), item.StartDate, item.TotalBudgetAmount, null);
        }

        if (key == Normalize("LeaseContract"))
        {
            var item = await _db.LeaseContracts.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ContractNumber, item.Description, item.Status.ToString(), item.StartDate, item.PresentValue, null);
        }

        return FinanceApprovalFacts.Empty;
    }

    private async Task ApplyApprovedOutcomeAsync(Guid tenantId, string entityType, Guid entityId, Guid userId, string? comments, Guid? invoicePaymentSodControlEventId, CancellationToken cancellationToken)
    {
        var key = Normalize(entityType);
        var now = DateTime.UtcNow;

        if (key == Normalize("JournalEntry"))
        {
            await _journalEntryService.UpdateApprovalStatusAsync(entityId, "Approved", "Approved", userId, cancellationToken: cancellationToken);
            return;
        }

        if (key == Normalize("FinanceBudgetOverride"))
        {
            if (_budgetControl == null)
                throw new InvalidOperationException("Finance budget control is not configured.");
            await _budgetControl.ApplyOverrideOutcomeAsync(entityId, true, userId, comments, cancellationToken);
            return;
        }

        if (key == Normalize("Invoice"))
        {
            var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            if (invoice?.Status == InvoiceStatus.Draft)
            {
                await RecordCustomerInvoiceAuditAsync(
                    tenantId,
                    invoice,
                    FinanceAuditEvents.ArInvoiceApproved,
                    new
                    {
                        invoice.Status,
                        approvedByUserId = userId,
                        approvedAt = now
                    },
                    comments,
                    cancellationToken);

                await _invoiceService.SendInvoiceAsync(entityId, cancellationToken);
            }
            return;
        }

        if (key == Normalize("VendorInvoice"))
        {
            await FinalizeVendorInvoiceApprovalAsync(tenantId, entityId, userId, comments, cancellationToken);
            return;
        }

        if (key == Normalize("FinancePurchaseOrder"))
        {
            await UpdateIfFoundAsync(_db.FinancePurchaseOrders, tenantId, entityId, item => item.Status = 2, cancellationToken);
            return;
        }

        if (key == Normalize("FinancePurchaseOrderReceipt"))
        {
            await _receiptPostingService.ApproveAndPostAsync(
                tenantId,
                entityId,
                userId,
                _currentUserService.UserName,
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("VendorPayment"))
        {
            var payment = await _db.Set<VendorPayment>().FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (payment == null)
            {
                return;
            }

            WorkflowApprovalConfigDto? control = null;
            if (!string.IsNullOrWhiteSpace(payment.ApprovalControlSnapshotJson))
            {
                try
                {
                    control = JsonSerializer.Deserialize<WorkflowApprovalConfigDto>(
                        payment.ApprovalControlSnapshotJson,
                        PaymentControlJsonOptions);
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException(
                        "The payment approval policy snapshot is invalid; authorization cannot be finalized.");
                }
            }

            var approvedWorkflowRoles = payment.WorkflowInstanceId.HasValue
                ? await _db.WorkflowApprovals
                    .AsNoTracking()
                    .Where(approval =>
                        approval.TenantId == tenantId &&
                        !approval.IsDeleted &&
                        approval.Status == WorkflowApprovalStatus.Approved &&
                        approval.StepInstance.WorkflowInstanceId == payment.WorkflowInstanceId.Value)
                    .Select(approval => new
                    {
                        approval.ApproverRole,
                        approval.ProcessedById,
                        approval.ProcessedDate
                    })
                    .ToListAsync(cancellationToken)
                : [];

            var managingDirectorRole = string.IsNullOrWhiteSpace(control?.ManagingDirectorApproverRole)
                ? "Managing Director"
                : control.ManagingDirectorApproverRole.Trim();
            var managingDirectorApproval = approvedWorkflowRoles
                .Where(item => string.Equals(item.ApproverRole, managingDirectorRole, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.ProcessedDate)
                .FirstOrDefault();
            if (payment.RequiresManagingDirectorApproval && managingDirectorApproval == null)
            {
                throw new InvalidOperationException(
                    $"Payment authorization cannot complete without the configured '{managingDirectorRole}' approval.");
            }

            var evidenceExceptionRole = string.IsNullOrWhiteSpace(control?.EvidenceExceptionApproverRole)
                ? managingDirectorRole
                : control.EvidenceExceptionApproverRole.Trim();
            var evidenceExceptionApproval = approvedWorkflowRoles
                .Where(item => string.Equals(item.ApproverRole, evidenceExceptionRole, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.ProcessedDate)
                .FirstOrDefault();
            if (payment.EvidenceExceptionRequested && evidenceExceptionApproval == null)
            {
                throw new InvalidOperationException(
                    $"The requested evidence exception requires completed '{evidenceExceptionRole}' approval.");
            }

            payment.Status = VendorPaymentStatus.Authorized;
            payment.AuthorizedById = userId;
            payment.AuthorizedDate = now;
            payment.ManagingDirectorApprovedById = managingDirectorApproval?.ProcessedById;
            payment.ManagingDirectorApprovedAt = managingDirectorApproval?.ProcessedDate;
            payment.EvidenceExceptionApprovedById = evidenceExceptionApproval?.ProcessedById;
            payment.EvidenceExceptionApprovedAt = evidenceExceptionApproval?.ProcessedDate;
            payment.InvoicePaymentSodControlEventId = invoicePaymentSodControlEventId;
            payment.UpdatedAt = now;
            payment.UpdatedBy = _currentUserService.UserName ?? "system";
            await _db.SaveChangesAsync(cancellationToken);

            await RecordVendorPaymentAuditAsync(
                tenantId,
                payment,
                FinanceAuditEvents.ApPaymentApproved,
                new
                {
                    payment.Status,
                    payment.AuthorizedById,
                    payment.AuthorizedDate,
                    payment.AppliedApprovalPolicySetId,
                    payment.AppliedApprovalPolicyCode,
                    payment.ApprovalControlSnapshotHash,
                    payment.RequiresManagingDirectorApproval,
                    payment.ManagingDirectorApprovedById,
                    payment.ManagingDirectorApprovedAt,
                    payment.EvidenceExceptionRequested,
                    payment.EvidenceExceptionApprovedById,
                    payment.EvidenceExceptionApprovedAt
                },
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("PaymentBatch"))
        {
            await UpdateIfFoundAsync(_db.Set<PaymentBatch>(), tenantId, entityId, item =>
            {
                item.Status = PaymentBatchStatus.Approved;
                item.ApprovedById = userId;
                item.ApprovedDate = now;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("SupplierReturn"))
        {
            await UpdateIfFoundAsync(_db.SupplierReturns, tenantId, entityId, item => item.Status = SupplierReturnStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("CustomerPayment"))
        {
            await UpdateIfFoundAsync(_db.Set<CustomerPayment>(), tenantId, entityId, item => item.Status = "Approved", cancellationToken);
            return;
        }

        if (key == Normalize("UnitJournalEntry"))
        {
            await UpdateIfFoundAsync(_db.Set<UnitJournalEntry>(), tenantId, entityId, item =>
            {
                item.Status = UnitJournalEntryStatus.Approved;
                item.ApprovedAt = now;
                item.ApprovedBy = userId;
                item.ApprovedByName = _currentUserService.UserName;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("UnitAccountBudget"))
        {
            await UpdateIfFoundAsync(_db.Set<UnitAccountBudget>(), tenantId, entityId, item =>
            {
                item.Status = "Approved";
                item.IsActive = true;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AllocationRule"))
        {
            await UpdateIfFoundAsync(_db.Set<AllocationRule>(), tenantId, entityId, item =>
            {
                item.ApprovalStatus = "Approved";
                item.IsActive = true;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AllocationRunBatch"))
        {
            await UpdateIfFoundAsync(_db.Set<AllocationRunBatch>(), tenantId, entityId, item =>
            {
                item.Status = AllocationRunBatchStatus.Approved;
                item.ApprovedAt = now;
                item.ApprovedBy = userId;
                item.ApprovedByName = _currentUserService.UserName;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("BudgetReturn"))
        {
            await UpdateIfFoundAsync(_db.BudgetReturns, tenantId, entityId, item =>
            {
                item.Status = "Approved";
                item.ApprovedDate = now;
                item.ApproverUserId = userId;
                item.RejectionReason = null;
            }, cancellationToken);
            await RecordBudgetAuditAsync(
                tenantId,
                "BudgetReturn",
                entityId,
                FinanceAuditEvents.BudgetReturnApproved,
                "Submitted",
                "Approved",
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("BudgetScenario"))
        {
            var scenario = await _db.BudgetScenarios.FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == entityId && !item.IsDeleted,
                cancellationToken);
            if (scenario == null)
                return;

            scenario.Status = "Approved";
            // Approval authorizes and locks the scenario. Adoption as the official
            // reporting baseline is a separate, explicit Budgeting action.
            scenario.IsActive = false;
            scenario.LockedDate = now;
            scenario.LockedByUserId = userId;
            scenario.UpdatedAt = now;
            scenario.LastModifiedById = userId;
            await _db.SaveChangesAsync(cancellationToken);
            await RecordBudgetAuditAsync(
                tenantId,
                "BudgetScenario",
                entityId,
                FinanceAuditEvents.BudgetScenarioApproved,
                "InReview",
                "Approved",
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("BudgetRevision"))
        {
            await UpdateIfFoundAsync(_db.BudgetRevisions, tenantId, entityId, item =>
            {
                // Workflow approval authorizes the request only. A separate Finance
                // Apply action creates and adopts the successor budget so reviewers
                // can distinguish authorization from changing the official baseline.
                item.Status = "Approved";
                item.ApprovedAt = now;
                item.ApprovedByUserId = userId;
                item.RejectionReason = null;
            }, cancellationToken);
            await RecordBudgetAuditAsync(
                tenantId,
                "BudgetRevision",
                entityId,
                FinanceAuditEvents.BudgetRevisionApproved,
                "Submitted",
                "Approved",
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("SalesOrder"))
        {
            await UpdateIfFoundAsync(_db.SalesOrders, tenantId, entityId, item => item.OrderStatus = SalesOrderStatus.Confirmed, cancellationToken);
            return;
        }

        if (key == Normalize("Quote"))
        {
            await UpdateIfFoundAsync(_db.Quotes, tenantId, entityId, item =>
            {
                item.QuoteStatus = "Sent";
                item.SentDate = now;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("ReturnOrder"))
        {
            await UpdateIfFoundAsync(_db.ReturnOrders, tenantId, entityId, item => item.ReturnStatus = ReturnOrderStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("CreditNote"))
        {
            await UpdateIfFoundAsync(_db.CreditNotes, tenantId, entityId, item => item.CreditNoteStatus = CreditNoteStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("Refund"))
        {
            await UpdateIfFoundAsync(_db.Refunds, tenantId, entityId, item => item.RefundStatus = RefundStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("BankReconciliation"))
        {
            var reconciliation = await _db.Set<BankReconciliation>().FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (reconciliation == null)
            {
                return;
            }

            reconciliation.Status = ReconciliationStatus.Approved;
            reconciliation.ApprovedBy = userId;
            reconciliation.ApprovedAt = now;
            reconciliation.UpdatedAt = now;
            reconciliation.UpdatedBy = _currentUserService.UserName ?? "system";
            await _db.SaveChangesAsync(cancellationToken);
            await RecordBankReconciliationAuditAsync(
                tenantId,
                reconciliation,
                FinanceAuditEvents.BankReconciliationApproved,
                new
                {
                    reconciliation.Status,
                    reconciliation.ApprovedBy,
                    reconciliation.ApprovedAt
                },
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("CashTransaction"))
        {
            var transaction = await _db.Set<CashTransaction>().FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (transaction == null || transaction.IsPosted)
            {
                return;
            }

            transaction.ApprovalStatus = CashTransactionApprovalStatus.Approved;
            transaction.ApprovedAt = now;
            transaction.ApprovedById = userId;
            transaction.ApprovalComments = comments;
            transaction.UpdatedAt = now;
            transaction.UpdatedBy = _currentUserService.UserName ?? "system";
            await _db.SaveChangesAsync(cancellationToken);

            await RecordCashBankTransactionAuditAsync(
                tenantId,
                transaction,
                FinanceAuditEvents.CashBankTransactionApproved,
                new
                {
                    transaction.ApprovalStatus,
                    transaction.ApprovedAt,
                    transaction.ApprovedById,
                    transaction.ApprovalComments
                },
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("ExchangeRate"))
        {
            var rate = await _db.ExchangeRates.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (rate == null || rate.ApprovalStatus == RateApprovalStatus.Approved)
            {
                return;
            }

            rate.ApprovalStatus = RateApprovalStatus.Approved;
            rate.ApprovalDate = now;
            rate.ApprovedByUserId = userId;
            rate.Comments = comments ?? rate.Comments;
            rate.ModifiedDate = now;
            rate.ModifiedByUserId = userId;
            await _db.SaveChangesAsync(cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "FX",
                "ExchangeRate",
                rate.Id,
                FinanceAuditEvents.FinanceWorkflowApproved,
                new { rate.ApprovalStatus, rate.ApprovalDate, rate.ApprovedByUserId },
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("OpeningBalanceBatch"))
        {
            await UpdateIfFoundAsync(_db.OpeningBalanceBatches, tenantId, entityId, item =>
            {
                if (string.Equals(item.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
                {
                    item.Status = "Approved";
                    item.ApprovedAt = now;
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUserService.UserName ?? "system";
                    item.LastModifiedById = userId;
                }
            }, cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "MIGRATION",
                "OpeningBalanceBatch",
                entityId,
                FinanceAuditEvents.FinanceWorkflowApproved,
                new { Status = "Approved" },
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("FixedAsset"))
        {
            await UpdateIfFoundAsync(_db.FixedAssets, tenantId, entityId, item =>
            {
                if (item.Status == FixedAssetStatus.PendingApproval)
                {
                    item.Status = FixedAssetStatus.Acquired;
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUserService.UserName ?? "system";
                }
            }, cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "FA",
                "FixedAsset",
                entityId,
                FinanceAuditEvents.FinanceWorkflowApproved,
                new { Status = FixedAssetStatus.Acquired },
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("FixedAssetDepreciationRun") || key == Normalize("AssetDepreciationSchedule"))
        {
            await UpdateIfFoundAsync(_db.FixedAssetDepreciationRuns, tenantId, entityId, item =>
            {
                if (string.Equals(item.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
                {
                    item.Status = "Approved";
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUserService.UserName ?? "system";
                }
            }, cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "FA",
                "FixedAssetDepreciationRun",
                entityId,
                FinanceAuditEvents.FinanceWorkflowApproved,
                new { Status = "Approved" },
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("AssetValuation"))
        {
            await UpdateIfFoundAsync(_db.AssetValuations, tenantId, entityId, item =>
            {
                if (string.Equals(item.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
                {
                    item.Status = "Approved";
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUserService.UserName ?? "system";
                }
            }, cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "FA",
                "FixedAssetValuation",
                entityId,
                FinanceAuditEvents.FinanceWorkflowApproved,
                new { Status = "Approved" },
                comments,
                cancellationToken);
            return;
        }

        if (key == Normalize("AssetTransfer"))
        {
            await UpdateIfFoundAsync(_db.AssetTransfers, tenantId, entityId, item =>
            {
                item.Status = AssetTransferStatus.Approved;
                item.ApprovedAt = now;
                item.Comments = comments ?? item.Comments;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AssetDisposal"))
        {
            await UpdateIfFoundAsync(_db.AssetDisposals, tenantId, entityId, item =>
            {
                item.Status = AssetDisposalStatus.Approved;
                item.ApprovedAt = now;
                item.Comments = comments ?? item.Comments;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AssetVerificationSession"))
        {
            await UpdateIfFoundAsync(_db.AssetVerificationSessions, tenantId, entityId, item => item.Status = VerificationSessionStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("CapitalProject"))
        {
            await UpdateIfFoundAsync(_db.CapitalProjects, tenantId, entityId, item => item.Status = ProjectStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("LeaseContract"))
        {
            await UpdateIfFoundAsync(_db.LeaseContracts, tenantId, entityId, item => item.Status = LeaseStatus.Active, cancellationToken);
        }
    }

    private async Task ApplyRejectedOutcomeAsync(Guid tenantId, string entityType, Guid entityId, Guid userId, string? reason, CancellationToken cancellationToken)
    {
        var key = Normalize(entityType);
        var now = DateTime.UtcNow;

        if (key == Normalize("JournalEntry"))
        {
            await _journalEntryService.UpdateApprovalStatusAsync(entityId, "Rejected", "Rejected", rejectionReason: reason, cancellationToken: cancellationToken);
            return;
        }

        if (key == Normalize("FinanceBudgetOverride"))
        {
            if (_budgetControl == null)
                throw new InvalidOperationException("Finance budget control is not configured.");
            await _budgetControl.ApplyOverrideOutcomeAsync(entityId, false, userId, reason, cancellationToken);
            return;
        }

        if (key == Normalize("FinancePurchaseOrder"))
        {
            await UpdateIfFoundAsync(_db.FinancePurchaseOrders, tenantId, entityId, item =>
            {
                item.Status = 10;
                item.Remarks = AppendReason(item.Remarks, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("FinancePurchaseOrderReceipt"))
        {
            await _receiptPostingService.RejectAsync(
                tenantId,
                entityId,
                userId,
                _currentUserService.UserName,
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("VendorInvoice"))
        {
            if (_vendorInvoiceService == null)
                throw new InvalidOperationException("AP invoice lifecycle service is not configured.");

            // The shared workbench owns the workflow action; AP still owns its document and
            // Finance-budget outcome. Delegate instead of directly changing status so rejection
            // cannot strand an active expense reservation.
            await _vendorInvoiceService.ApplyRejectedWorkflowOutcomeAsync(
                entityId,
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("Invoice"))
        {
            var invoice = await _db.Invoices.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (invoice == null)
            {
                return;
            }

            invoice.Status = InvoiceStatus.Rejected;
            await _db.SaveChangesAsync(cancellationToken);

            await RecordCustomerInvoiceAuditAsync(
                tenantId,
                invoice,
                FinanceAuditEvents.ArInvoiceRejected,
                new
                {
                    invoice.Status,
                    rejectionReason = reason
                },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("VendorPayment"))
        {
            var payment = await _db.Set<VendorPayment>().FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (payment == null)
            {
                return;
            }

            var rejectedPolicyCode = payment.AppliedApprovalPolicyCode;
            var rejectedWorkflowInstanceId = payment.WorkflowInstanceId;
            payment.Status = VendorPaymentStatus.Draft;
            payment.Notes = AppendReason(payment.Notes, reason);
            // Rejection returns an unposted direct payment to its maker. Clear the applied route so
            // resubmission must resolve and snapshot the policy that is effective at the new date.
            payment.SubmittedById = null;
            payment.SubmittedAt = null;
            payment.WorkflowInstanceId = null;
            payment.AppliedApprovalPolicySetId = null;
            payment.AppliedApprovalPolicyCode = null;
            payment.ApprovalControlSnapshotJson = null;
            payment.ApprovalControlSnapshotHash = null;
            payment.IsExceptionalPayment = false;
            payment.ExceptionalPaymentReason = null;
            payment.RequiresManagingDirectorApproval = false;
            payment.ManagingDirectorApprovedById = null;
            payment.ManagingDirectorApprovedAt = null;
            payment.EvidenceExceptionRequested = false;
            payment.EvidenceExceptionReason = null;
            payment.EvidenceExceptionRequestedById = null;
            payment.EvidenceExceptionRequestedAt = null;
            payment.EvidenceExceptionApprovedById = null;
            payment.EvidenceExceptionApprovedAt = null;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.UpdatedBy = _currentUserService.UserName ?? "system";
            await _db.SaveChangesAsync(cancellationToken);

            await RecordVendorPaymentAuditAsync(
                tenantId,
                payment,
                FinanceAuditEvents.ApPaymentRejected,
                new
                {
                    payment.Status,
                    payment.Notes,
                    rejectedPolicyCode,
                    rejectedWorkflowInstanceId
                },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("SupplierReturn"))
        {
            await UpdateIfFoundAsync(_db.SupplierReturns, tenantId, entityId, item => item.Status = SupplierReturnStatus.Rejected, cancellationToken);
            return;
        }

        if (key == Normalize("CustomerPayment"))
        {
            await UpdateIfFoundAsync(_db.Set<CustomerPayment>(), tenantId, entityId, item =>
            {
                item.Status = "Rejected";
                item.Notes = AppendReason(item.Notes, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("UnitJournalEntry"))
        {
            await UpdateIfFoundAsync(_db.Set<UnitJournalEntry>(), tenantId, entityId, item =>
            {
                item.Status = UnitJournalEntryStatus.Rejected;
                item.RejectionReason = reason;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("UnitAccountBudget"))
        {
            await UpdateIfFoundAsync(_db.Set<UnitAccountBudget>(), tenantId, entityId, item =>
            {
                item.Status = "Rejected";
                item.IsActive = false;
                item.Notes = AppendReason(item.Notes, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AllocationRule"))
        {
            await UpdateIfFoundAsync(_db.Set<AllocationRule>(), tenantId, entityId, item =>
            {
                item.ApprovalStatus = "Rejected";
                item.IsActive = false;
                item.Description = AppendReason(item.Description, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AllocationRunBatch"))
        {
            await UpdateIfFoundAsync(_db.Set<AllocationRunBatch>(), tenantId, entityId, item =>
            {
                item.Status = AllocationRunBatchStatus.Rejected;
                item.RejectionReason = reason;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("BudgetReturn"))
        {
            await UpdateIfFoundAsync(_db.BudgetReturns, tenantId, entityId, item =>
            {
                item.Status = "Rejected";
                item.RejectionReason = reason;
                item.ApprovedDate = null;
            }, cancellationToken);
            await RecordBudgetAuditAsync(
                tenantId,
                "BudgetReturn",
                entityId,
                FinanceAuditEvents.BudgetReturnRejected,
                "Submitted",
                "Rejected",
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("BudgetScenario"))
        {
            await UpdateIfFoundAsync(_db.BudgetScenarios, tenantId, entityId, item =>
            {
                item.Status = "Collecting";
                item.LockedDate = null;
                item.LockedByUserId = null;
                item.Description = AppendReason(item.Description, reason);
            }, cancellationToken);
            await RecordBudgetAuditAsync(
                tenantId,
                "BudgetScenario",
                entityId,
                FinanceAuditEvents.BudgetScenarioRejected,
                "InReview",
                "Collecting",
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("BudgetRevision"))
        {
            await UpdateIfFoundAsync(_db.BudgetRevisions, tenantId, entityId, item =>
            {
                item.Status = "Rejected";
                item.ApprovedAt = null;
                item.ApprovedByUserId = null;
                item.RejectionReason = reason;
            }, cancellationToken);
            await RecordBudgetAuditAsync(
                tenantId,
                "BudgetRevision",
                entityId,
                FinanceAuditEvents.BudgetRevisionRejected,
                "Submitted",
                "Rejected",
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("SalesOrder"))
        {
            await UpdateIfFoundAsync(_db.SalesOrders, tenantId, entityId, item => item.OrderStatus = SalesOrderStatus.Rejected, cancellationToken);
            return;
        }

        if (key == Normalize("Quote"))
        {
            await UpdateIfFoundAsync(_db.Quotes, tenantId, entityId, item => item.QuoteStatus = "Rejected", cancellationToken);
            return;
        }

        if (key == Normalize("ReturnOrder"))
        {
            await UpdateIfFoundAsync(_db.ReturnOrders, tenantId, entityId, item => item.ReturnStatus = ReturnOrderStatus.Rejected, cancellationToken);
            return;
        }

        if (key == Normalize("CreditNote"))
        {
            await UpdateIfFoundAsync(_db.CreditNotes, tenantId, entityId, item =>
            {
                item.CreditNoteStatus = CreditNoteStatus.Voided;
                item.Reason = AppendReason(item.Reason, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("Refund"))
        {
            await UpdateIfFoundAsync(_db.Refunds, tenantId, entityId, item => item.RefundStatus = RefundStatus.Rejected, cancellationToken);
            return;
        }

        if (key == Normalize("BankReconciliation"))
        {
            var reconciliation = await _db.Set<BankReconciliation>().FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (reconciliation == null)
            {
                return;
            }

            reconciliation.Status = ReconciliationStatus.Rejected;
            reconciliation.UpdatedAt = now;
            reconciliation.UpdatedBy = _currentUserService.UserName ?? "system";
            reconciliation.Notes = AppendReason(reconciliation.Notes, reason);
            await _db.SaveChangesAsync(cancellationToken);
            await RecordBankReconciliationAuditAsync(
                tenantId,
                reconciliation,
                FinanceAuditEvents.BankReconciliationRejected,
                new
                {
                    reconciliation.Status,
                    reconciliation.Notes
                },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("CashTransaction"))
        {
            var transaction = await _db.Set<CashTransaction>().FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (transaction == null || transaction.IsPosted)
            {
                return;
            }

            transaction.ApprovalStatus = CashTransactionApprovalStatus.Rejected;
            transaction.RejectedAt = now;
            transaction.RejectedById = userId;
            transaction.RejectionReason = reason;
            transaction.UpdatedAt = now;
            transaction.UpdatedBy = _currentUserService.UserName ?? "system";
            await _db.SaveChangesAsync(cancellationToken);

            await RecordCashBankTransactionAuditAsync(
                tenantId,
                transaction,
                FinanceAuditEvents.CashBankTransactionRejected,
                new
                {
                    transaction.ApprovalStatus,
                    transaction.RejectedAt,
                    transaction.RejectedById,
                    transaction.RejectionReason
                },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("ExchangeRate"))
        {
            var rate = await _db.ExchangeRates.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == entityId && !x.IsDeleted,
                cancellationToken);
            if (rate == null)
            {
                return;
            }

            rate.ApprovalStatus = RateApprovalStatus.Rejected;
            rate.Comments = AppendReason(rate.Comments, reason);
            rate.ModifiedDate = now;
            rate.ModifiedByUserId = userId;
            await _db.SaveChangesAsync(cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "FX",
                "ExchangeRate",
                rate.Id,
                FinanceAuditEvents.FinanceWorkflowRejected,
                new { rate.ApprovalStatus, rate.Comments },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("OpeningBalanceBatch"))
        {
            await UpdateIfFoundAsync(_db.OpeningBalanceBatches, tenantId, entityId, item =>
            {
                if (!string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                {
                    item.Status = "Rejected";
                    item.FailureReason = AppendReason(item.FailureReason, reason);
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUserService.UserName ?? "system";
                    item.LastModifiedById = userId;
                }
            }, cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "MIGRATION",
                "OpeningBalanceBatch",
                entityId,
                FinanceAuditEvents.FinanceWorkflowRejected,
                new { Status = "Rejected", Reason = reason },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("FixedAsset"))
        {
            await UpdateIfFoundAsync(_db.FixedAssets, tenantId, entityId, item =>
            {
                if (item.Status == FixedAssetStatus.PendingApproval)
                {
                    item.Status = FixedAssetStatus.Rejected;
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUserService.UserName ?? "system";
                }
            }, cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "FA",
                "FixedAsset",
                entityId,
                FinanceAuditEvents.FinanceWorkflowRejected,
                new { Status = FixedAssetStatus.Rejected },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("FixedAssetDepreciationRun") || key == Normalize("AssetDepreciationSchedule"))
        {
            await UpdateIfFoundAsync(_db.FixedAssetDepreciationRuns, tenantId, entityId, item =>
            {
                if (!string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                {
                    item.Status = "Rejected";
                    item.FailureReason = reason;
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUserService.UserName ?? "system";
                }
            }, cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "FA",
                "FixedAssetDepreciationRun",
                entityId,
                FinanceAuditEvents.FinanceWorkflowRejected,
                new { Status = "Rejected", Reason = reason },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("AssetValuation"))
        {
            await UpdateIfFoundAsync(_db.AssetValuations, tenantId, entityId, item =>
            {
                if (!item.IsPostedToGL)
                {
                    item.Status = "Rejected";
                    item.FailureReason = reason;
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUserService.UserName ?? "system";
                }
            }, cancellationToken);
            await RecordFinanceWorkflowAuditAsync(
                tenantId,
                "FA",
                "FixedAssetValuation",
                entityId,
                FinanceAuditEvents.FinanceWorkflowRejected,
                new { Status = "Rejected", Reason = reason },
                reason,
                cancellationToken);
            return;
        }

        if (key == Normalize("AssetTransfer"))
        {
            await UpdateIfFoundAsync(_db.AssetTransfers, tenantId, entityId, item =>
            {
                item.Status = AssetTransferStatus.Rejected;
                item.Comments = reason ?? item.Comments;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AssetDisposal"))
        {
            await UpdateIfFoundAsync(_db.AssetDisposals, tenantId, entityId, item =>
            {
                item.Status = AssetDisposalStatus.Rejected;
                item.Comments = reason ?? item.Comments;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AssetVerificationSession"))
        {
            await UpdateIfFoundAsync(_db.AssetVerificationSessions, tenantId, entityId, item =>
            {
                item.Status = VerificationSessionStatus.Rejected;
                item.Description = AppendReason(item.Description, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("CapitalProject"))
        {
            await UpdateIfFoundAsync(_db.CapitalProjects, tenantId, entityId, item =>
            {
                item.Status = ProjectStatus.Rejected;
                item.Description = AppendReason(item.Description, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("LeaseContract"))
        {
            await UpdateIfFoundAsync(_db.LeaseContracts, tenantId, entityId, item =>
            {
                item.Status = LeaseStatus.Rejected;
                item.Description = AppendReason(item.Description, reason);
            }, cancellationToken);
        }
    }

    private async Task FinalizeVendorInvoiceApprovalAsync(Guid tenantId, Guid entityId, Guid userId, string? comments, CancellationToken cancellationToken)
    {
        var invoice = await _db.VendorInvoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == entityId && !i.IsDeleted, cancellationToken);
        if (invoice == null || invoice.Status == VendorInvoiceStatus.Approved)
        {
            return;
        }

        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = userId;
        invoice.ApprovedDate = DateTime.UtcNow;
        invoice.ApprovalComments = comments;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = _currentUserService.UserName ?? "system";

        // Inventory is posted only by the governed purchase-receipt/inspection
        // lifecycle. Workflow approval of an invoice must not post stock again.

        await _db.SaveChangesAsync(cancellationToken);
        await RecordVendorInvoiceAuditAsync(
            tenantId,
            invoice,
            FinanceAuditEvents.ApInvoiceApproved,
            new
            {
                invoice.Status,
                invoice.ApprovalStatus,
                invoice.ApprovedById,
                invoice.ApprovedDate,
                invoice.ApprovalComments
            },
            comments,
            cancellationToken);

        if (invoice.IsOpeningBalance)
        {
            // Opening-balance AP invoices are approved as migration subledger evidence;
            // controlled opening-balance posting, not normal AP invoice posting, owns GL impact.
            return;
        }

        if (_vendorInvoiceService == null)
        {
            throw new InvalidOperationException("Vendor invoice posting service is not configured.");
        }

        await _vendorInvoiceService.PostAsync(invoice.Id, cancellationToken);
    }

    private async Task RecordFinanceWorkflowAuditAsync(
        Guid tenantId,
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string eventType,
        object? afterValues,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            AfterValues = afterValues,
            Comment = comment,
            Reason = eventType == FinanceAuditEvents.FinanceWorkflowRejected
                || eventType == FinanceAuditEvents.FinanceWorkflowApproverPermissionRejected
                    ? comment
                    : null,
            Resource = $"Finance.{sourceDocumentType}",
            ResourceId = sourceDocumentId.ToString()
        }, cancellationToken);
    }

    private async Task RecordVendorInvoiceAuditAsync(
        Guid tenantId,
        VendorInvoice invoice,
        string eventType,
        object? afterValues,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = "AP",
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = invoice.Id,
            JournalEntryId = invoice.JournalEntryId,
            AfterValues = afterValues,
            Comment = comment,
            Reason = eventType == FinanceAuditEvents.ApInvoiceRejected ? comment : null,
            Resource = "Finance.APInvoice",
            ResourceId = invoice.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordVendorPaymentAuditAsync(
        Guid tenantId,
        VendorPayment payment,
        string eventType,
        object? afterValues,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = "AP",
            SourceDocumentType = "VendorPayment",
            SourceDocumentId = payment.Id,
            JournalEntryId = payment.JournalEntryId,
            AfterValues = afterValues,
            Comment = comment,
            Reason = eventType == FinanceAuditEvents.ApPaymentRejected ? comment : null,
            Resource = "Finance.APPayment",
            ResourceId = payment.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordCustomerInvoiceAuditAsync(
        Guid tenantId,
        Invoice invoice,
        string eventType,
        object? afterValues,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = "AR",
            SourceDocumentType = "CustomerInvoice",
            SourceDocumentId = invoice.Id,
            JournalEntryId = invoice.JournalEntryId,
            AfterValues = afterValues,
            Comment = comment,
            Reason = eventType == FinanceAuditEvents.ArInvoiceRejected ? comment : null,
            Resource = "Finance.ARInvoice",
            ResourceId = invoice.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordCashBankTransactionAuditAsync(
        Guid tenantId,
        CashTransaction transaction,
        string eventType,
        object? afterValues,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = "CASHBANK",
            SourceDocumentType = transaction.TransactionType switch
            {
                CashTransactionType.Receipt => "CashBankReceipt",
                CashTransactionType.Payment => "CashBankPayment",
                CashTransactionType.Transfer => "CashBankTransfer",
                _ => "CashBankTransaction"
            },
            SourceDocumentId = transaction.Id,
            JournalEntryId = transaction.JournalEntryId,
            WorkflowInstanceId = transaction.WorkflowInstanceId,
            AfterValues = afterValues,
            Comment = comment,
            Reason = eventType == FinanceAuditEvents.CashBankTransactionRejected ? comment : null,
            Resource = "Finance.CashTransaction",
            ResourceId = transaction.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordBankReconciliationAuditAsync(
        Guid tenantId,
        BankReconciliation reconciliation,
        string eventType,
        object? afterValues,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = "CASHBANK",
            SourceDocumentType = "BankReconciliation",
            SourceDocumentId = reconciliation.Id,
            AfterValues = afterValues,
            Comment = comment,
            Reason = eventType == FinanceAuditEvents.BankReconciliationRejected ? comment : null,
            Resource = "Finance.BankReconciliation",
            ResourceId = reconciliation.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordBudgetAuditAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        string eventType,
        string fromStatus,
        string toStatus,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = "BUDGETING",
            SourceDocumentType = entityType,
            SourceDocumentId = entityId,
            BeforeValues = new { Status = fromStatus },
            AfterValues = new { Status = toStatus },
            Comment = comment,
            Reason = eventType is FinanceAuditEvents.BudgetReturnRejected
                or FinanceAuditEvents.BudgetScenarioRejected
                    ? comment
                    : null,
            Resource = $"Finance.{entityType}",
            ResourceId = entityId.ToString()
        }, cancellationToken);
    }

    private async Task UpdateIfFoundAsync<TEntity>(DbSet<TEntity> set, Guid tenantId, Guid id, Action<TEntity> apply, CancellationToken cancellationToken)
        where TEntity : class
    {
        var entity = await set.FirstOrDefaultAsync(e =>
            EF.Property<Guid>(e, "TenantId") == tenantId &&
            EF.Property<Guid>(e, "Id") == id &&
            !EF.Property<bool>(e, "IsDeleted"),
            cancellationToken);
        if (entity == null)
        {
            return;
        }

        apply(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private Guid? GetCurrentUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private static bool CanActOnApproval(WorkflowApproval approval, Guid currentUserId, HashSet<string> roles)
        => (approval.ApproverId.HasValue && approval.ApproverId.Value == currentUserId)
           || (!string.IsNullOrWhiteSpace(approval.ApproverRole) && roles.Contains(approval.ApproverRole));

    private static bool IsFinanceEntity(string? entityType)
        => FinanceWorkflowEntityKeys.Contains(Normalize(entityType));

    internal static string ResolveDetailHref(string? entityType, Guid entityId, string? displayUrl)
    {
        if (!string.IsNullOrWhiteSpace(displayUrl))
        {
            return displayUrl;
        }

        return Normalize(entityType) == "OPENINGBALANCEBATCH"
            ? $"/finance/opening-balances?batchId={entityId:D}"
            : "/finance/approvals";
    }

    private static bool RequiresSubmitterApproverSeparation(string? entityType)
    {
        var key = Normalize(entityType);
        return key is "EXCHANGERATE"
            or "FINANCEBUDGETOVERRIDE"
            or "VENDORPAYMENT"
            or "PAYMENTBATCH"
            or "OPENINGBALANCEBATCH"
            or "FIXEDASSET"
            or "FIXEDASSETDEPRECIATIONRUN"
            or "ASSETDEPRECIATIONSCHEDULE"
            or "ASSETVALUATION";
    }

    private static string? GetActionDisabledReason(
        string action,
        string requiredPermission,
        bool hasPermission,
        bool submitterApprovalBlocked,
        string? controlReason = null)
    {
        if (!hasPermission)
        {
            return $"You are assigned to this workflow step but your roles do not include {requiredPermission}.";
        }

        if (submitterApprovalBlocked)
        {
            return string.IsNullOrWhiteSpace(controlReason)
                ? $"You cannot {action} this item because you submitted it and separation of duties is required."
                : controlReason;
        }

        return null;
    }

    private static string Normalize(string? value)
        => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string AppendReason(string? existing, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return existing ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(existing)
            ? $"Rejected: {reason.Trim()}"
            : $"{existing}{Environment.NewLine}Rejected: {reason.Trim()}";
    }

    private static string MapFinancePurchaseOrderStatus(int status)
        => status switch
        {
            1 => "Draft",
            2 => "Approved",
            3 => "Partially Received",
            4 => "Received",
            5 => "Partially Invoiced",
            6 => "Invoiced",
            7 => "Closed",
            8 => "Cancelled",
            9 => "Pending Approval",
            10 => "Rejected",
            _ => "Unknown"
        };

    private static string GetModule(string entityType)
    {
        var key = Normalize(entityType);
        if (new[] { "FINANCEPURCHASEORDER", "FINANCEPURCHASEORDERRECEIPT", "VENDORINVOICE", "VENDORPAYMENT", "PAYMENTBATCH", "PURCHASERETURN" }.Contains(key))
        {
            return "Accounts Payable";
        }

        if (new[] { "QUOTE", "SALESORDER", "DELIVERYNOTE", "INVOICE", "RETURNORDER", "CREDITNOTE", "CUSTOMERPAYMENT", "REFUND" }.Contains(key))
        {
            return "Accounts Receivable";
        }

        if (new[] { "BUDGETSCENARIO", "BUDGETRETURN" }.Contains(key))
        {
            return "Budgeting";
        }

        if (new[] { "UNITJOURNALENTRY", "UNITACCOUNTBUDGET", "ALLOCATIONRULE" }.Contains(key))
        {
            return "Unit Accounting";
        }

        if (new[] { "CASHTRANSACTION", "BANKRECONCILIATION", "CHEQUE" }.Contains(key))
        {
            return "Cash Management";
        }

        if (key == "EXCHANGERATE")
        {
            return "Foreign Exchange";
        }

        if (key.StartsWith("ASSET", StringComparison.OrdinalIgnoreCase) || key is "FIXEDASSET" or "FIXEDASSETDEPRECIATIONRUN" or "CAPITALPROJECT" or "LEASECONTRACT")
        {
            return "Fixed Assets";
        }

        return "General Ledger";
    }

    private static string GetDocumentType(string entityType)
        => Normalize(entityType) switch
        {
            "FINANCEPURCHASEORDER" => "Purchase Order",
            "FINANCEPURCHASEORDERRECEIPT" => "Goods Receipt",
            "VENDORINVOICE" => "Supplier Invoice",
            "VENDORPAYMENT" => "Supplier Payment",
            "PAYMENTBATCH" => "Payment Batch",
            "PURCHASERETURN" => "Supplier Return",
            "QUOTE" => "Quotation",
            "SALESORDER" => "Sales Order",
            "DELIVERYNOTE" => "Delivery",
            "INVOICE" => "Customer Invoice",
            "RETURNORDER" => "Customer Return",
            "CREDITNOTE" => "Credit Note",
            "CUSTOMERPAYMENT" => "Customer Payment",
            "REFUND" => "Refund",
            "BUDGETSCENARIO" => "Budget Scenario",
            "BUDGETRETURN" => "Budget Return",
            "UNITJOURNALENTRY" => "Unit Journal",
            "UNITACCOUNTBUDGET" => "Unit Budget",
            "ALLOCATIONRULE" => "Allocation",
            "CASHTRANSACTION" => "Bank Transaction",
            "BANKRECONCILIATION" => "Bank Reconciliation",
            "CHEQUE" => "Cheque",
            "EXCHANGERATE" => "Exchange Rate",
            "FIXEDASSET" => "Fixed Asset",
            "FIXEDASSETDEPRECIATIONRUN" => "Depreciation Run",
            "ASSETDEPRECIATIONSCHEDULE" => "Depreciation",
            "ASSETVALUATION" => "Asset Valuation",
            "ASSETTRANSFER" => "Asset Transfer",
            "ASSETDISPOSAL" => "Asset Disposal",
            "ASSETVERIFICATIONSESSION" => "Asset Verification",
            "CAPITALPROJECT" => "Capital Project",
            "LEASECONTRACT" => "Lease Contract",
            _ => entityType
        };

    public sealed class FinanceApprovalActionRequest
    {
        public string? Comments { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class FinanceApprovalQueueItemDto
    {
        public Guid ApprovalId { get; set; }
        public Guid EntityId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string DetailHref { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string CurrentStep { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        public decimal? Amount { get; set; }
        public string? CurrencyCode { get; set; }
        public string? SubmittedBy { get; set; }
        public string? ApproverRole { get; set; }
        public string? WorkflowName { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public string? ApproveDisabledReason { get; set; }
        public string? RejectDisabledReason { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    private sealed record FinanceApprovalFacts(
        string? Reference,
        string? Title,
        string? StatusLabel,
        DateTime? Date,
        decimal? Amount,
        string? CurrencyCode)
    {
        public static readonly FinanceApprovalFacts Empty = new(null, null, "Pending Approval", null, null, null);
        public Dictionary<string, string> Metadata { get; init; } = new();
    }
}
