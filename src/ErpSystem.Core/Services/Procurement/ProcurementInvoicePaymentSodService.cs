using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementInvoicePaymentSodService : IProcurementInvoicePaymentSodService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ILogger<ProcurementInvoicePaymentSodService> _logger;
    private readonly IProcurementSodPolicy _sodPolicy;

    public ProcurementInvoicePaymentSodService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        ILogger<ProcurementInvoicePaymentSodService> logger,
        IProcurementSodPolicy? sodPolicy = null)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _logger = logger;
        _sodPolicy = sodPolicy ?? new ProcurementSodPolicy(unitOfWork);
    }

    public Task<ProcurementInvoicePaymentSodReadinessDto> GetPaymentReadinessAsync(
        Guid paymentId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EvaluatePaymentAsync(paymentId, correlationId, enforce: false, cancellationToken);

    public Task<ProcurementInvoicePaymentSodReadinessDto> GetBatchReadinessAsync(
        Guid batchId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EvaluateBatchAsync(batchId, correlationId, enforce: false, cancellationToken);

    public async Task<ProcurementInvoicePaymentSodQueueReadinessDto> GetQueueReadinessAsync(
        IReadOnlyCollection<Guid> paymentIds,
        IReadOnlyCollection<Guid> batchIds,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantAndActor();
        _ = NormalizeCorrelation(correlationId);
        var normalizedPaymentIds = paymentIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var normalizedBatchIds = batchIds.Where(id => id != Guid.Empty).Distinct().ToArray();

        var payments = normalizedPaymentIds.Length == 0
            ? new List<VendorPayment>()
            : await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    normalizedPaymentIds.Contains(item.Id) &&
                    !item.IsDeleted)
                .Include(item => item.Allocations.Where(allocation => !allocation.IsDeleted))
                    .ThenInclude(allocation => allocation.VendorInvoice)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        var batches = normalizedBatchIds.Length == 0
            ? new List<PaymentBatch>()
            : await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    normalizedBatchIds.Contains(item.Id) &&
                    !item.IsDeleted)
                .Include(item => item.Items)
                    .ThenInclude(item => item.Invoices)
                        .ThenInclude(item => item.VendorInvoice)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        var coverage = await _sodGuard.GetCoverageAsync(DateTime.UtcNow, cancellationToken);
        var paymentResults = new Dictionary<Guid, ProcurementInvoicePaymentSodReadinessDto>();
        var batchResults = new Dictionary<Guid, ProcurementInvoicePaymentSodReadinessDto>();
        foreach (var payment in payments)
            paymentResults[payment.Id] = BuildQueueReadiness(
                ProcurementInvoicePaymentSodRules.PaymentSourceType, payment.Id, payment.PaymentNumber,
                GetEffectiveAllocations(payment.Allocations).Select(allocation => allocation.VendorInvoice).ToList(), coverage,
                await _sodPolicy.IsRequiredForSourceAsync(TenantId, ProcurementInvoicePaymentSodRules.PaymentSourceType, payment.Id, cancellationToken));
        foreach (var batch in batches)
            batchResults[batch.Id] = BuildQueueReadiness(
                ProcurementInvoicePaymentSodRules.BatchSourceType, batch.Id, batch.BatchNumber,
                batch.Items.SelectMany(item => item.Invoices).Select(item => item.VendorInvoice).ToList(), coverage,
                await _sodPolicy.IsRequiredForSourceAsync(TenantId, ProcurementInvoicePaymentSodRules.BatchSourceType, batch.Id, cancellationToken));
        return new ProcurementInvoicePaymentSodQueueReadinessDto { Payments = paymentResults, Batches = batchResults };
    }

    public Task<ProcurementInvoicePaymentSodReadinessDto> EnforcePaymentApprovalAsync(
        Guid paymentId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EvaluatePaymentAsync(paymentId, correlationId, enforce: true, cancellationToken);

    public Task<ProcurementInvoicePaymentSodReadinessDto> EnforceBatchApprovalAsync(
        Guid batchId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EvaluateBatchAsync(batchId, correlationId, enforce: true, cancellationToken);

    public async Task RevalidatePaymentAuthorizationAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantAndActor();
        var payment = await _unitOfWork.Repository<VendorPayment>()
            .GetQueryable(item => item.TenantId == TenantId && item.Id == paymentId && !item.IsDeleted)
            .Include(item => item.PaymentBatch)
            .Include(item => item.Allocations.Where(allocation => !allocation.IsDeleted))
                .ThenInclude(allocation => allocation.VendorInvoice)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementInvoicePaymentSodNotFoundException(
                $"Vendor payment with Id '{paymentId}' was not found in the current tenant.");

        if (!payment.ApprovalRequired)
        {
            if (payment.AuthorizedById.HasValue || payment.AuthorizedDate.HasValue || payment.WorkflowInstanceId.HasValue ||
                payment.InvoicePaymentSodControlEventId.HasValue || !payment.SubmittedById.HasValue || !payment.SubmittedAt.HasValue ||
                (payment.PaymentBatchId.HasValue && (payment.PaymentBatch == null || payment.PaymentBatch.IsDeleted ||
                    payment.PaymentBatch.TenantId != TenantId || payment.PaymentBatch.ApprovalRequired)))
                throw BlockedEvidence("VendorPayment", payment.Id, payment.PaymentNumber, null);
            await RevalidateNoApprovalSourceAsync(payment.PaymentBatchId ?? payment.Id,
                GetEffectiveAllocations(payment.Allocations).Select(item => item.VendorInvoice).ToList(), cancellationToken);
            return;
        }

        await RevalidatePersistedAsync(
            payment.PaymentBatchId.HasValue
                ? ProcurementInvoicePaymentSodRules.BatchSourceType
                : ProcurementInvoicePaymentSodRules.PaymentSourceType,
            payment.PaymentBatchId ?? payment.Id,
            payment.PaymentBatch?.BatchNumber ?? payment.PaymentNumber,
            payment.AuthorizedById,
            payment.InvoicePaymentSodControlEventId,
            GetEffectiveAllocations(payment.Allocations)
                .Select(item => item.VendorInvoice)
                .ToList(),
            cancellationToken);
    }

    public async Task RevalidateBatchAuthorizationAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantAndActor();
        var batch = await _unitOfWork.Repository<PaymentBatch>()
            .GetQueryable(item => item.TenantId == TenantId && item.Id == batchId && !item.IsDeleted)
            .Include(item => item.Items)
                .ThenInclude(item => item.Invoices)
                    .ThenInclude(item => item.VendorInvoice)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementInvoicePaymentSodNotFoundException(
                $"Payment batch with Id '{batchId}' was not found in the current tenant.");

        if (!batch.ApprovalRequired)
        {
            if (batch.ApprovedById.HasValue || batch.ApprovedDate.HasValue || batch.InvoicePaymentSodControlEventId.HasValue ||
                !batch.CreatedById.HasValue || batch.CreatedById == Guid.Empty)
                throw BlockedEvidence("PaymentBatch", batch.Id, batch.BatchNumber, null);
            await RevalidateNoApprovalSourceAsync(batch.Id,
                batch.Items.SelectMany(item => item.Invoices).Select(item => item.VendorInvoice).ToList(), cancellationToken);
            return;
        }

        await RevalidatePersistedAsync(
            ProcurementInvoicePaymentSodRules.BatchSourceType,
            batch.Id,
            batch.BatchNumber,
            batch.ApprovedById,
            batch.InvoicePaymentSodControlEventId,
            batch.Items.SelectMany(item => item.Invoices).Select(item => item.VendorInvoice).ToList(),
            cancellationToken);
    }

    private async Task<ProcurementInvoicePaymentSodReadinessDto> EvaluatePaymentAsync(
        Guid paymentId,
        string correlationId,
        bool enforce,
        CancellationToken cancellationToken)
    {
        EnsureTenantAndActor();
        var payment = await _unitOfWork.Repository<VendorPayment>()
            .GetQueryable(item => item.TenantId == TenantId && item.Id == paymentId && !item.IsDeleted)
            .Include(item => item.Allocations.Where(allocation => !allocation.IsDeleted))
                .ThenInclude(allocation => allocation.VendorInvoice)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementInvoicePaymentSodNotFoundException(
                $"Vendor payment with Id '{paymentId}' was not found in the current tenant.");

        return await EvaluateAsync(
            ProcurementInvoicePaymentSodRules.PaymentSourceType,
            payment.Id,
            payment.PaymentNumber,
            GetEffectiveAllocations(payment.Allocations)
                .Select(item => item.VendorInvoice)
                .ToList(),
            correlationId,
            enforce,
            cancellationToken);
    }

    private ProcurementInvoicePaymentSodReadinessDto BuildQueueReadiness(
        string sourceType,
        Guid sourceId,
        string sourceReference,
        IReadOnlyCollection<VendorInvoice> sourceInvoices,
        ProcurementSodCoverageDto coverage,
        bool enforceSeparation)
    {
        var actorId = CurrentActorId;
        var invoices = sourceInvoices
            .Where(item => item != null && !item.IsDeleted)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderBy(item => item.InvoiceNumber)
            .Select(item => new ProcurementInvoicePaymentSodInvoiceDto
            {
                VendorInvoiceId = item.Id,
                InvoiceNumber = item.InvoiceNumber,
                InvoiceProcessorUserId = item.SubmittedById,
                SubmittedAtUtc = item.SubmittedDate,
                ProcessorLineagePresent = item.SubmittedById.HasValue && item.SubmittedById.Value != Guid.Empty,
                ConflictsWithCurrentActor = enforceSeparation && ProcurementInvoicePaymentSodRules.HasConflict(item.SubmittedById, actorId)
            })
            .ToList();
        var missingLineage = invoices.Any(item => !item.ProcessorLineagePresent);
        var hasConflict = invoices.Any(item => item.ConflictsWithCurrentActor);
        var control = coverage.Controls.SingleOrDefault(item =>
            string.Equals(item.Code, ProcurementInvoicePaymentSodRules.ControlCode, StringComparison.OrdinalIgnoreCase));

        bool allowed;
        string code;
        string message;
        if (missingLineage)
        {
            allowed = false;
            code = ProcurementInvoicePaymentSodRules.LineageCode;
            message = "Every selected invoice must retain its server-owned submitting processor before payment approval.";
        }
        else if (!enforceSeparation)
        {
            allowed = true;
            code = "SOD_DISABLED";
            message = "Actor separation is disabled for this procurement payment. Invoice lineage and payment approvals remain required.";
        }
        else if (control == null || !control.IsConfigured || !control.IsEffective || !control.IsHardStop)
        {
            allowed = false;
            code = "SOD_POLICY_INCOMPLETE";
            message = control?.ConfigurationIssue ??
                      "The invoice-processor versus payment-approver hard stop is not effective.";
        }
        else if (hasConflict)
        {
            allowed = false;
            code = ProcurementInvoicePaymentSodRules.ConflictCode;
            message = "The current actor cannot approve a payment for an invoice they processed.";
        }
        else
        {
            allowed = true;
            code = "SOD_ALLOWED";
            message = invoices.Count == 0
                ? "No invoice is allocated, so there is no invoice-processor identity conflict; the required hard-stop policy is effective."
                : "The current actor is independent of the recorded invoice processor participant(s).";
        }

        return new ProcurementInvoicePaymentSodReadinessDto
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceReference = sourceReference,
            CurrentActorUserId = actorId,
            CanApprove = allowed,
            HasInvoiceProcessorLineage = !missingLineage,
            Code = code,
            Message = message,
            EvaluatedAtUtc = coverage.EvaluatedAtUtc,
            PolicySetId = coverage.PolicySetId,
            PolicyCode = coverage.PolicyCode,
            PolicyVersion = coverage.PolicyVersion,
            RuleId = control?.RuleId,
            RuleCode = control?.RuleCode,
            DecisionKeys = ProcurementInvoicePaymentSodRules.DecisionKeys,
            Invoices = invoices
        };
    }

    private async Task<ProcurementInvoicePaymentSodReadinessDto> EvaluateBatchAsync(
        Guid batchId,
        string correlationId,
        bool enforce,
        CancellationToken cancellationToken)
    {
        EnsureTenantAndActor();
        var batch = await _unitOfWork.Repository<PaymentBatch>()
            .GetQueryable(item => item.TenantId == TenantId && item.Id == batchId && !item.IsDeleted)
            .Include(item => item.Items)
                .ThenInclude(item => item.Invoices)
                    .ThenInclude(item => item.VendorInvoice)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementInvoicePaymentSodNotFoundException(
                $"Payment batch with Id '{batchId}' was not found in the current tenant.");

        return await EvaluateAsync(
            ProcurementInvoicePaymentSodRules.BatchSourceType,
            batch.Id,
            batch.BatchNumber,
            batch.Items.SelectMany(item => item.Invoices).Select(item => item.VendorInvoice).ToList(),
            correlationId,
            enforce,
            cancellationToken);
    }

    private async Task<ProcurementInvoicePaymentSodReadinessDto> EvaluateAsync(
        string sourceType,
        Guid sourceId,
        string sourceReference,
        IReadOnlyList<VendorInvoice> sourceInvoices,
        string correlationId,
        bool enforce,
        CancellationToken cancellationToken)
    {
        var actorId = CurrentActorId;
        var evaluatedAtUtc = DateTime.UtcNow;
        var enforceSeparation = await _sodPolicy.IsRequiredForSourceAsync(TenantId, sourceType, sourceId, cancellationToken);
        var invoices = sourceInvoices
            .Where(item => item != null && !item.IsDeleted)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderBy(item => item.InvoiceNumber)
            .Select(item => new ProcurementInvoicePaymentSodInvoiceDto
            {
                VendorInvoiceId = item.Id,
                InvoiceNumber = item.InvoiceNumber,
                InvoiceProcessorUserId = item.SubmittedById,
                SubmittedAtUtc = item.SubmittedDate,
                ProcessorLineagePresent = item.SubmittedById.HasValue && item.SubmittedById.Value != Guid.Empty,
                ConflictsWithCurrentActor = enforceSeparation && ProcurementInvoicePaymentSodRules.HasConflict(item.SubmittedById, actorId)
            })
            .ToList();

        var missingLineage = invoices.Any(item => !item.ProcessorLineagePresent);
        var processors = invoices
            .Where(item => item.InvoiceProcessorUserId.HasValue && item.InvoiceProcessorUserId.Value != Guid.Empty)
            .Select(item => item.InvoiceProcessorUserId!.Value)
            .Distinct()
            .ToList();

        bool allowed;
        string code;
        string message;
        Guid? policySetId = null;
        string? policyCode = null;
        int? policyVersion = null;
        Guid? ruleId = null;
        string? ruleCode = null;

        if (missingLineage)
        {
            allowed = false;
            code = ProcurementInvoicePaymentSodRules.LineageCode;
            message = "Every selected invoice must retain its server-owned submitting processor before payment approval.";
        }
        else if (!enforceSeparation)
        {
            allowed = true;
            code = "SOD_DISABLED";
            message = "Actor separation is disabled for this procurement payment. Invoice lineage and payment approvals remain required.";
        }
        else if (processors.Count == 0)
        {
            var coverage = await _sodGuard.GetCoverageAsync(evaluatedAtUtc, cancellationToken);
            var control = coverage.Controls.Single(item =>
                string.Equals(item.Code, ProcurementInvoicePaymentSodRules.ControlCode, StringComparison.OrdinalIgnoreCase));
            allowed = control.IsConfigured && control.IsEffective && control.IsHardStop;
            code = allowed ? "SOD_ALLOWED" : "SOD_POLICY_INCOMPLETE";
            message = allowed
                ? "No invoice is allocated, so there is no invoice-processor identity conflict; the required hard-stop policy is effective."
                : control.ConfigurationIssue ?? "The invoice-processor versus payment-approver hard stop is not effective.";
            policySetId = coverage.PolicySetId;
            policyCode = coverage.PolicyCode;
            policyVersion = coverage.PolicyVersion;
            ruleId = control.RuleId;
            ruleCode = control.RuleCode;
        }
        else
        {
            var request = new ProcurementSodGuardRequest
            {
                ControlCode = ProcurementInvoicePaymentSodRules.ControlCode,
                SourceType = sourceType,
                SourceReference = sourceId.ToString(),
                ProhibitedActorUserIds = processors
            };
            var decision = enforce
                ? await _sodGuard.EnforceAsync(request, NormalizeCorrelation(correlationId), cancellationToken)
                : await _sodGuard.CheckAsync(request, NormalizeCorrelation(correlationId), cancellationToken);
            allowed = decision.Allowed;
            code = decision.Allowed ? decision.Code :
                decision.Code == "SOD_CONFLICT" ? ProcurementInvoicePaymentSodRules.ConflictCode : decision.Code;
            message = decision.Message;
            policySetId = decision.PolicySetId;
            policyCode = decision.PolicyCode;
            policyVersion = decision.PolicyVersion;
            ruleId = decision.RuleId;
            ruleCode = decision.RuleCode;
        }

        Guid? controlEventId = null;
        if (enforce)
        {
            var correlation = NormalizeCorrelation(correlationId);
            var controlEvent = await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
            {
                EventKey = ProcurementControlEventKey.Create(
                    "invoice-payment-sod", TenantId, sourceType, sourceId, actorId, correlation),
                EventType = ProcurementInvoicePaymentSodRules.EventType,
                Action = ProcurementInvoicePaymentSodRules.ApproveAction,
                Result = allowed ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
                RuleCode = ProcurementInvoicePaymentSodRules.RequirementCode,
                RuleId = ruleId,
                RuleVersion = ProcurementInvoicePaymentSodRules.RuleVersion,
                DecisionKeys = ProcurementInvoicePaymentSodRules.DecisionKeys.ToList(),
                SourceType = sourceType,
                SourceId = sourceId,
                SourceReference = sourceReference,
                Reason = message,
                InputValues = new
                {
                    ControlCode = ProcurementInvoicePaymentSodRules.ControlCode,
                    InvoiceProcessors = processors,
                    MissingProcessorInvoiceIds = invoices
                        .Where(item => !item.ProcessorLineagePresent)
                        .Select(item => item.VendorInvoiceId),
                    ActorUserId = actorId
                },
                ResultValues = new { Allowed = allowed, Code = code, policySetId, policyCode, policyVersion, ruleId, ruleCode },
                CorrelationId = correlation,
                CausationId = correlation,
                OccurredAtUtc = evaluatedAtUtc
            }, cancellationToken);
            controlEventId = controlEvent.Id;
        }

        var readiness = new ProcurementInvoicePaymentSodReadinessDto
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceReference = sourceReference,
            CurrentActorUserId = actorId,
            CanApprove = allowed,
            HasInvoiceProcessorLineage = !missingLineage,
            Code = code,
            Message = message,
            EvaluatedAtUtc = evaluatedAtUtc,
            ControlEventId = controlEventId,
            PolicySetId = policySetId,
            PolicyCode = policyCode,
            PolicyVersion = policyVersion,
            RuleId = ruleId,
            RuleCode = ruleCode,
            DecisionKeys = ProcurementInvoicePaymentSodRules.DecisionKeys,
            Invoices = invoices
        };

        _logger.LogInformation(
            "TDC-0506 invoice/payment SOD {Result} for {SourceType} {SourceId} and actor {ActorId}",
            allowed ? "allowed" : "blocked", sourceType, sourceId, actorId);

        if (enforce && !allowed)
            throw new ProcurementInvoicePaymentSodBlockedException(code, message, readiness);

        return readiness;
    }

    private async Task RevalidateNoApprovalSourceAsync(
        Guid sourceId, IReadOnlyList<VendorInvoice> invoices, CancellationToken cancellationToken)
    {
        // This removes only a nonexistent approver comparison. Tenant/source lineage remains
        // compulsory, and the normal payment owner still enforces AP-003 and Finance posting.
        if (invoices.Any(item => item == null || item.IsDeleted || item.TenantId != TenantId ||
                !item.SubmittedById.HasValue || item.SubmittedById == Guid.Empty) ||
            await _unitOfWork.Repository<WorkflowInstance>().GetQueryable(item =>
                item.TenantId == TenantId && item.EntityId == sourceId && !item.IsDeleted &&
                (item.Status == WorkflowInstanceStatus.Created || item.Status == WorkflowInstanceStatus.InProgress ||
                 item.Status == WorkflowInstanceStatus.Waiting || item.Status == WorkflowInstanceStatus.Suspended))
                .AnyAsync(cancellationToken))
            throw BlockedEvidence("Payment", sourceId, sourceId.ToString(), null);
    }

    private async Task RevalidatePersistedAsync(
        string sourceType,
        Guid sourceId,
        string sourceReference,
        Guid? approverUserId,
        Guid? controlEventId,
        IReadOnlyList<VendorInvoice> invoices,
        CancellationToken cancellationToken)
    {
        if (!approverUserId.HasValue || approverUserId.Value == Guid.Empty || !controlEventId.HasValue)
            throw BlockedEvidence(sourceType, sourceId, sourceReference, approverUserId);

        var enforceSeparation = await _sodPolicy.IsRequiredForSourceAsync(TenantId, sourceType, sourceId, cancellationToken);
        if (invoices.Any(item => !item.SubmittedById.HasValue || item.SubmittedById.Value == Guid.Empty) ||
            (enforceSeparation && invoices.Any(item => ProcurementInvoicePaymentSodRules.HasConflict(item.SubmittedById, approverUserId.Value))))
            throw BlockedEvidence(sourceType, sourceId, sourceReference, approverUserId);

        var eventValid = await _unitOfWork.Repository<ProcurementControlEvent>()
            .GetQueryable(item =>
                item.TenantId == TenantId &&
                item.Id == controlEventId.Value &&
                item.EventType == ProcurementInvoicePaymentSodRules.EventType &&
                item.Action == ProcurementInvoicePaymentSodRules.ApproveAction &&
                item.Result == ProcurementControlEventResult.Allowed &&
                item.RuleCode == ProcurementInvoicePaymentSodRules.RequirementCode &&
                item.RuleVersion == ProcurementInvoicePaymentSodRules.RuleVersion &&
                item.SourceType == sourceType &&
                item.SourceId == sourceId &&
                item.ActorUserId == approverUserId.Value &&
                !item.IsDeleted)
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (!eventValid)
            throw BlockedEvidence(sourceType, sourceId, sourceReference, approverUserId);
    }

    private static ProcurementInvoicePaymentSodBlockedException BlockedEvidence(
        string sourceType,
        Guid sourceId,
        string sourceReference,
        Guid? approverUserId) => new(
        ProcurementInvoicePaymentSodRules.EvidenceCode,
        "The payment approval does not retain current, independently validated TDC-0506 SOD evidence.",
        new ProcurementInvoicePaymentSodReadinessDto
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceReference = sourceReference,
            CurrentActorUserId = approverUserId ?? Guid.Empty,
            CanApprove = false,
            HasInvoiceProcessorLineage = false,
            Code = ProcurementInvoicePaymentSodRules.EvidenceCode,
            Message = "The payment approval does not retain current, independently validated TDC-0506 SOD evidence.",
            EvaluatedAtUtc = DateTime.UtcNow,
            DecisionKeys = ProcurementInvoicePaymentSodRules.DecisionKeys
        });

    private static List<VendorPaymentAllocation> GetEffectiveAllocations(
        IEnumerable<VendorPaymentAllocation> allocations)
    {
        var allocationHistory = allocations
            .Where(item => !item.IsDeleted)
            .ToList();
        var reversedOriginalIds = allocationHistory
            .Where(item => item.IsReversal && item.OriginalAllocationId.HasValue)
            .Select(item => item.OriginalAllocationId!.Value)
            .ToHashSet();

        return allocationHistory
            .Where(item => !item.IsReversal && !reversedOriginalIds.Contains(item.Id))
            .ToList();
    }

    private Guid TenantId => _currentUser.TenantId ??
        throw new InvalidOperationException("An authenticated tenant is required for payment SOD evaluation.");

    private Guid CurrentActorId => Guid.TryParse(_currentUser.UserId, out var actorId) && actorId != Guid.Empty
        ? actorId
        : throw new InvalidOperationException("An authenticated user is required for payment SOD evaluation.");

    private void EnsureTenantAndActor()
    {
        _ = TenantId;
        _ = CurrentActorId;
    }

    private static string NormalizeCorrelation(string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim().Length <= 100
                ? correlationId.Trim()
                : correlationId.Trim()[..100];
}
