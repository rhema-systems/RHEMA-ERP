using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorPaymentService
{
    private Task<int> ClaimBatchProcessingAsync(Guid batchId, DateTime interruptedBefore,
        Func<Task<int>> claim, CancellationToken cancellationToken) =>
        _unitOfWork.HasActiveTransaction
            ? ClaimBatchProcessingInTransactionAsync(batchId, interruptedBefore, claim, cancellationToken, false)
            : _unitOfWork.ExecuteInStrategyAsync(() => ClaimBatchProcessingInTransactionAsync(
                batchId, interruptedBefore, claim, cancellationToken, true), cancellationToken);

    private async Task<int> ClaimBatchProcessingInTransactionAsync(Guid batchId, DateTime interruptedBefore,
        Func<Task<int>> claim, CancellationToken cancellationToken, bool ownsTransaction)
    {
        if (CurrentUserId == Guid.Empty) throw new UnauthorizedAccessException("An authenticated payment processor is required.");
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await _unitOfWork.AcquireTransactionLockAsync(ApSettlementLockKeys.Batch(TenantId, batchId), cancellationToken);
            var batch = await _unitOfWork.Repository<PaymentBatch>().GetQueryable(item =>
                    item.TenantId == TenantId && item.Id == batchId && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("The payment batch was not found in this tenant.");
            if (batch.Status != PaymentBatchStatus.Approved && !(batch.Status == PaymentBatchStatus.Processing &&
                    (!batch.ProcessedDate.HasValue || batch.ProcessedDate < interruptedBefore)))
            {
                if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
                return 0;
            }
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(batch.BankAccountId, cancellationToken),
                FinanceAccessLevel.Operate, cancellationToken);
            await _invoicePaymentSod!.RevalidateBatchAuthorizationAsync(batchId, cancellationToken);
            var selections = await _unitOfWork.Repository<PaymentBatchInvoice>().GetQueryable(item =>
                    item.TenantId == TenantId && item.PaymentBatchId == batchId && !item.IsDeleted)
                .Include(item => item.VendorPayment).ToListAsync(cancellationToken);
            if (selections.Count == 0) throw new InvalidOperationException("The batch has no immutable invoice selections.");
            foreach (var selection in selections.OrderBy(item => item.VendorInvoiceId))
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Invoice(TenantId, selection.VendorInvoiceId), cancellationToken);
                if (IsDurablyPosted(selection.VendorPayment)) continue;
                if (!selection.VendorPayment.ApprovalRequired) await ValidateNoApprovalPaymentSnapshotAsync(selection.VendorPayment, cancellationToken);
                var decision = await RequireBatchInvoiceReadinessAsync(selection,
                    ProcurementPaymentReadinessRules.BatchProcessAction, cancellationToken);
                selection.PaymentReadinessControlEventId = decision.Event.Id;
                selection.PaymentReadinessSnapshotHash = decision.Readiness.SnapshotHash;
                selection.PaymentReadinessEvaluatedAtUtc = decision.Readiness.EvaluatedAtUtc;
                selection.UpdatedAt = DateTime.UtcNow;
                selection.UpdatedBy = UserName;
            }
            // The SQL transition guard must observe the exact AP-003 processing evidence
            // before, not after, Approved -> Processing. The claim and evidence are atomic.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var result = await claim();
            if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
            if (ownsTransaction) _unitOfWork.ClearTrackedChanges();
            throw;
        }
    }

    private async Task<bool> IsPaymentApprovalRequiredAsync(string entityType, Guid id) =>
        await _workflowService.HasActiveApprovalInstanceAsync(entityType, id) ||
        await _workflowService.HasActiveApprovalWorkflowAsync(entityType);

    // Separate MD/signature authority is never waived by an inactive internal workflow.
    // Required clean documents are checked separately; no human verification is fabricated.
    private static bool CanCompleteWithoutApproval(WorkflowApprovalConfigDto control) =>
        !control.RequiresManagingDirectorApproval &&
        control.SignaturePolicy?.IsRequired != true;

    private async Task CaptureNoApprovalPaymentPolicyAsync(
        VendorPayment payment, SubmitVendorPaymentDto request, CancellationToken cancellationToken)
    {
        if (_approvalPolicyResolver == null || _financeAuditService == null)
            throw new InvalidOperationException("Payment policy resolution and Finance audit must be configured before completing a payment.");
        if (payment.TenantId != TenantId || CurrentUserId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated user in the payment tenant is required.");
        if (payment.WorkflowInstanceId.HasValue || payment.AuthorizedById.HasValue || payment.AuthorizedDate.HasValue ||
            payment.JournalEntryId.HasValue || payment.InvoicePaymentSodControlEventId.HasValue)
            throw new InvalidOperationException("Existing payment approval and posting history cannot be replaced with a no-approval route.");
        if (request.IsExceptionalPayment || request.RequestEvidenceException || payment.IsExceptionalPayment ||
            payment.RequiresManagingDirectorApproval || payment.EvidenceExceptionRequested)
            throw new InvalidOperationException("This payment requires separate senior authority. The no-workflow payment path does not yet capture that authority; no payment was posted and the requirement was not waived.");
        if (payment.TotalAmount <= 0m || payment.ExchangeRate <= 0m)
            throw new InvalidOperationException("Payment amount and exchange rate must be positive.");

        var now = DateTime.UtcNow;
        var currency = (await _tenantSettingsService.GetBaseCurrencyAsync()).Trim().ToUpperInvariant();
        var resolution = await _approvalPolicyResolver.ResolveAsync(new WorkflowApprovalPolicyContext(
            TenantId, "Vendor Payment", now, Module: "Finance", Category: payment.PaymentMethod.ToString(),
            Amount: decimal.Round(payment.TotalAmount * payment.ExchangeRate, 2, MidpointRounding.AwayFromZero),
            CurrencyCode: currency), cancellationToken);
        var control = resolution?.ApprovalConfig ?? new WorkflowApprovalConfigDto();
        if (!CanCompleteWithoutApproval(control))
            throw new InvalidOperationException("The payment policy requires Managing Director authority or a mandatory signature. Capture of that separate authority without an internal workflow is not yet available; no payment was posted.");
        await RequirePaymentEvidenceAsync(payment, control, cancellationToken);

        payment.ApprovalRequired = false;
        // Authorized remains the established operational ready-to-post state, not a human
        // approval: the explicit snapshot and all null approver fields distinguish the route.
        payment.Status = VendorPaymentStatus.Authorized;
        payment.SubmittedById = CurrentUserId;
        payment.SubmittedAt = now;
        payment.AppliedApprovalPolicySetId = resolution?.PolicySetId;
        payment.AppliedApprovalPolicyCode = resolution?.PolicyCode;
        payment.ApprovalControlSnapshotJson = JsonSerializer.Serialize(control, PaymentControlJsonOptions);
        payment.ApprovalControlSnapshotHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(payment.ApprovalControlSnapshotJson)));
        payment.UpdatedAt = now;
        payment.UpdatedBy = UserName;
        payment.LastModifiedById = CurrentUserId;
    }

    private Task RecordNoApprovalPaymentAuditAsync(VendorPayment payment, CancellationToken cancellationToken) =>
        RecordApPaymentAuditAsync(FinanceAuditEvents.ApPaymentSubmitted, payment,
            afterValues: new
            {
                payment.Status, payment.ApprovalRequired, payment.PaymentBatchId,
                payment.SubmittedById, payment.SubmittedAt, payment.AppliedApprovalPolicySetId,
                payment.AppliedApprovalPolicyCode, payment.ApprovalControlSnapshotHash,
                ApprovalOutcome = "NotRequired", Completion = "ReadyToPost"
            }, reason: "No active approval workflow or in-flight approval exists for this payment source.",
            cancellationToken: cancellationToken);

    private Task<VendorPaymentDto> SubmitWithoutApprovalAsync(
        Guid id, SubmitVendorPaymentDto request, CancellationToken cancellationToken) =>
        _unitOfWork.HasActiveTransaction
            ? SubmitWithoutApprovalInTransactionAsync(id, request, cancellationToken, false)
            : _unitOfWork.ExecuteInStrategyAsync(
                () => SubmitWithoutApprovalInTransactionAsync(id, request, cancellationToken, true), cancellationToken);

    private async Task<VendorPaymentDto> SubmitWithoutApprovalInTransactionAsync(
        Guid id, SubmitVendorPaymentDto request, CancellationToken cancellationToken, bool ownsTransaction)
    {
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await _unitOfWork.AcquireTransactionLockAsync(ApSettlementLockKeys.Payment(TenantId, id), cancellationToken);
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                .Include(item => item.Allocations.Where(allocation => !allocation.IsDeleted))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("The payment was not found in this tenant.");
            if (payment.Status != VendorPaymentStatus.Draft || payment.PaymentBatchId.HasValue)
                throw new InvalidOperationException("Only a draft direct payment can be completed here.");
            if (await IsPaymentApprovalRequiredAsync("VendorPayment", id))
                throw new InvalidOperationException("The payment approval configuration changed. Refresh and submit through the current workflow.");
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                FinanceAccessLevel.Operate, cancellationToken);
            var allocations = GetEffectiveAllocations(payment.Allocations);
            EnsureAllocationTotalIsValid(payment, allocations);
            foreach (var allocation in allocations.OrderBy(item => item.VendorInvoiceId))
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Invoice(TenantId, allocation.VendorInvoiceId), cancellationToken);
                await RequirePaymentReadinessAsync(allocation.VendorInvoiceId,
                    ProcurementPaymentReadinessRules.PostAction, id, null, cancellationToken, allowSettledInvoice: true);
            }
            await CaptureNoApprovalPaymentPolicyAsync(payment, request, cancellationToken);
            // This aggregate is tracked. Do not DbSet.Update the invoice/bank navigation graph.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordNoApprovalPaymentAuditAsync(payment, cancellationToken);
            if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
            return await GetByIdAsync(id, cancellationToken) ?? MapToDto(payment);
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
            if (ownsTransaction) _unitOfWork.ClearTrackedChanges();
            throw;
        }
    }

    private async Task ValidateNoApprovalPaymentSnapshotAsync(VendorPayment payment, CancellationToken cancellationToken)
    {
        if (payment.WorkflowInstanceId.HasValue || payment.AuthorizedById.HasValue || payment.AuthorizedDate.HasValue ||
            payment.InvoicePaymentSodControlEventId.HasValue || !payment.SubmittedById.HasValue || !payment.SubmittedAt.HasValue ||
            payment.IsExceptionalPayment || payment.RequiresManagingDirectorApproval || payment.EvidenceExceptionRequested ||
            payment.ManagingDirectorApprovedById.HasValue || payment.ManagingDirectorApprovedAt.HasValue ||
            payment.EvidenceExceptionApprovedById.HasValue || payment.EvidenceExceptionApprovedAt.HasValue)
            throw new InvalidOperationException("The payment's no-approval completion evidence is invalid.");
        if (string.IsNullOrWhiteSpace(payment.ApprovalControlSnapshotJson) ||
            !string.Equals(payment.ApprovalControlSnapshotHash, Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(payment.ApprovalControlSnapshotJson))), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The payment policy snapshot integrity check failed.");
        var control = DeserializePaymentControlSnapshot(payment.ApprovalControlSnapshotJson)
            ?? throw new InvalidOperationException("The payment policy snapshot is invalid.");
        if (!CanCompleteWithoutApproval(control))
            throw new InvalidOperationException("The payment retains an unsatisfied Managing Director authority or mandatory signature requirement.");
        await RequirePaymentEvidenceAsync(payment, control, cancellationToken);
    }
}
