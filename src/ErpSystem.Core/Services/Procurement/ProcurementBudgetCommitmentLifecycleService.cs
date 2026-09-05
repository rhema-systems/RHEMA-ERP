using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementBudgetCommitmentLifecycleService : IProcurementBudgetCommitmentLifecycleService
{
    private const string PurchaseOrderSource = "PurchaseOrder";
    private const string ContractSource = "Contract";
    private const string ReceiptSource = "PurchaseReceipt";
    private const string CertificateSource = "PaymentCertificate";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementBudgetReservationStore _reservationStore;

    public ProcurementBudgetCommitmentLifecycleService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementBudgetReservationStore reservationStore)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _reservationStore = reservationStore;
    }

    private IGenericRepository<ProcurementBudgetCommitment> Commitments =>
        _unitOfWork.Repository<ProcurementBudgetCommitment>();
    private IGenericRepository<ProcurementBudgetCommitmentLedgerEntry> Ledger =>
        _unitOfWork.Repository<ProcurementBudgetCommitmentLedgerEntry>();

    public async Task<ProcurementBudgetCommitmentLedgerEntry> CommitPurchaseOrderAsync(
        PurchaseOrder purchaseOrder, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureContextAndTransaction();
        EnsureSourceTenant(purchaseOrder.TenantId);
        if (!purchaseOrder.SourceRequisitionId.HasValue)
            throw Error("PO_BUDGET_REQUISITION_REQUIRED", "The approved purchase order has no source purchase requisition.");
        var purchaseOrderExists = await _unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(item => item.Id == purchaseOrder.Id &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.SourceRequisitionId == purchaseOrder.SourceRequisitionId)
            .AnyAsync(cancellationToken);
        if (!purchaseOrderExists)
            throw Error("BUDGET_LIFECYCLE_SOURCE_NOT_FOUND",
                "The purchase order source is unavailable in the current tenant.");

        var idempotentReplay = await FindEntryAsync(
            ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
            PurchaseOrderSource,
            purchaseOrder.Id,
            cancellationToken);
        if (idempotentReplay is not null)
        {
            await EnsurePurchaseOrderIdempotentAsync(
                idempotentReplay,
                purchaseOrder,
                cancellationToken);
            return idempotentReplay;
        }

        if (purchaseOrder.ContractId.HasValue)
        {
            var contractCommitment = await FindEntryAsync(
                ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
                ContractSource, purchaseOrder.ContractId.Value, cancellationToken);
            if (contractCommitment is not null)
            {
                return await AllocatePurchaseOrderToContractAsync(
                    purchaseOrder, contractCommitment, correlationId, cancellationToken);
            }
        }
        return await CommitAsync(purchaseOrder.SourceRequisitionId.Value, PurchaseOrderSource,
            purchaseOrder.Id, purchaseOrder.OrderNumber, purchaseOrder.TotalAmount,
            purchaseOrder.Currency, correlationId, cancellationToken);
    }

    public async Task<ProcurementBudgetCommitmentLedgerEntry> CommitContractAsync(
        Contract contract, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureSourceTenant(contract.TenantId);
        var contractExists = await _unitOfWork.Repository<Contract>()
            .GetQueryable(item => item.Id == contract.Id &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.TenderId == contract.TenderId)
            .AnyAsync(cancellationToken);
        if (!contractExists)
            throw Error("BUDGET_LIFECYCLE_SOURCE_NOT_FOUND",
                "The contract source is unavailable in the current tenant.");
        var requisitionId = await _unitOfWork.Repository<Tender>()
            .GetQueryable(item => item.Id == contract.TenderId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Select(item => item.SourcePurchaseRequisitionId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!requisitionId.HasValue)
            throw Error("CONTRACT_BUDGET_REQUISITION_REQUIRED", "The activated contract has no source purchase requisition.");
        return await CommitAsync(requisitionId.Value, ContractSource, contract.Id,
            contract.ContractNumber, contract.ContractValue, contract.Currency,
            correlationId, cancellationToken);
    }

    public Task<ProcurementBudgetCommitmentLedgerEntry> UtilizePurchaseOrderAsync(
        Guid purchaseOrderId, Guid receiptId, string receiptReference, decimal amount,
        string correlationId, CancellationToken cancellationToken = default) =>
        UtilizeAsync(PurchaseOrderSource, purchaseOrderId, ReceiptSource, receiptId,
            receiptReference, amount, correlationId, cancellationToken);

    public Task<ProcurementBudgetCommitmentLedgerEntry> UtilizeContractCertificateAsync(
        Guid contractId, Guid certificateId, string certificateReference, decimal amount,
        string correlationId, CancellationToken cancellationToken = default) =>
        UtilizeAsync(ContractSource, contractId, CertificateSource, certificateId,
            certificateReference, amount, correlationId, cancellationToken);

    public async Task<ProcurementBudgetCommitmentLedgerEntry?> ReleaseUnusedContractAsync(
        Guid contractId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureContextAndTransaction();
        var contract = await _unitOfWork.Repository<Contract>()
            .GetQueryable(item =>
                item.Id == contractId &&
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error(
                "BUDGET_LIFECYCLE_SOURCE_NOT_FOUND",
                "The contract source is unavailable in the current tenant.");
        var formal = await FindEntryAsync(
            ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
            ContractSource,
            contractId,
            cancellationToken);
        if (formal is null)
            return null;

        var existing = await FindEntryAsync(
            ProcurementBudgetCommitmentLedgerEntryType.Release,
            ContractSource,
            contractId,
            cancellationToken);
        if (existing is not null)
        {
            EnsureContractReleaseIdempotent(existing, formal);
            return existing;
        }

        var budget = await _reservationStore.GetBudgetForUpdateAsync(
                _currentUser.TenantId,
                formal.ProcurementBudgetId,
                cancellationToken)
            ?? throw Error(
                "BUDGET_NOT_FOUND",
                "The committed procurement budget is unavailable.");

        existing = await FindEntryAsync(
            ProcurementBudgetCommitmentLedgerEntryType.Release,
            ContractSource,
            contractId,
            cancellationToken);
        if (existing is not null)
        {
            EnsureContractReleaseIdempotent(existing, formal);
            return existing;
        }

        await EnsureContractAllocationsFullyUtilizedAsync(formal, cancellationToken);

        var utilized = await Ledger.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.FormalCommitmentEntryId == formal.Id &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization &&
                !item.IsDeleted)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        if (utilized > formal.Amount)
            throw Error(
                "CONTRACT_BUDGET_UTILIZATION_EXCEEDED",
                "Contract utilization exceeds its immutable formal commitment.");

        var unused = decimal.Round(
            formal.Amount - utilized,
            2,
            MidpointRounding.AwayFromZero);
        if (unused == 0m)
        {
            // Utilization alone cannot close the shared requisition envelope:
            // another PO may still be awarded under the same approved PR. The
            // governed contract-close boundary is the explicit terminal event.
            var fullyUtilizedCommitment = await Commitments.GetQueryable(item =>
                    item.Id == formal.ProcurementBudgetCommitmentId &&
                    item.TenantId == _currentUser.TenantId &&
                    !item.IsDeleted)
                .SingleAsync(cancellationToken);
            if (fullyUtilizedCommitment.Status == ProcurementBudgetCommitmentStatus.Reserved &&
                fullyUtilizedCommitment.ReservedAmount <= fullyUtilizedCommitment.UtilizedAmount)
            {
                var closedAt = DateTime.UtcNow;
                fullyUtilizedCommitment.Status = ProcurementBudgetCommitmentStatus.Consumed;
                fullyUtilizedCommitment.ConsumedAtUtc = closedAt;
                fullyUtilizedCommitment.BudgetAllocatedSnapshot = budget.AllocatedAmount;
                fullyUtilizedCommitment.BudgetUtilizedSnapshot = budget.UtilizedAmount;
                fullyUtilizedCommitment.BudgetCommittedAfter = budget.CommittedAmount;
                fullyUtilizedCommitment.BudgetReservedAfter = budget.ReservedAmount;
                fullyUtilizedCommitment.BudgetAvailableAfter = budget.RemainingAmount;
                fullyUtilizedCommitment.UpdatedAt = closedAt;
                await _reservationStore.SetContractReleaseContextAsync(
                    contract.Id,
                    cancellationToken);
                try
                {
                    await Commitments.UpdateAsync(fullyUtilizedCommitment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    await _reservationStore.SetContractReleaseContextAsync(
                        null,
                        cancellationToken);
                }
            }
            return null;
        }
        if (budget.CommittedAmount < unused)
            throw Error(
                "BUDGET_COMMITTED_BALANCE_MISMATCH",
                "The procurement budget committed balance does not cover the unused contract commitment.");

        var commitment = await Commitments.GetQueryable(item =>
                item.Id == formal.ProcurementBudgetCommitmentId &&
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .SingleAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var release = NewEntry(
            commitment,
            ProcurementBudgetCommitmentLedgerEntryType.Release,
            ContractSource,
            contract.Id,
            contract.ContractNumber,
            unused,
            formal.Currency,
            correlationId,
            now);
        release.FormalCommitmentEntryId = formal.Id;

        // Persist the immutable reversal evidence first inside the same
        // transaction so the guarded aggregate update can prove its exact
        // formal-contract lineage. A later failure still rolls both back.
        await Ledger.AddAsync(release);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        budget.CommittedAmount -= unused;
        budget.RemainingAmount = Available(budget);
        budget.UpdatedAt = now;
        commitment.ReservedAmount = decimal.Round(
            Math.Max(0m, commitment.ReservedAmount - unused),
            2,
            MidpointRounding.AwayFromZero);
        commitment.FormallyCommittedAmount = decimal.Round(
            Math.Max(0m, commitment.FormallyCommittedAmount - unused),
            2,
            MidpointRounding.AwayFromZero);
        if (commitment.ReservedAmount == 0m)
        {
            commitment.Status = ProcurementBudgetCommitmentStatus.Released;
            commitment.ReleasedAtUtc = now;
            commitment.ReleasedById = _currentUser.UserId;
            commitment.ReleasedByName = ActorName();
            commitment.ReleaseReason = "Unused formal contract commitment released at governed Works closeout.";
            commitment.ConsumedAtUtc = null;
        }
        else if (commitment.ReservedAmount <= commitment.UtilizedAmount)
        {
            commitment.Status = ProcurementBudgetCommitmentStatus.Consumed;
            commitment.ConsumedAtUtc ??= now;
        }
        commitment.BudgetAllocatedSnapshot = budget.AllocatedAmount;
        commitment.BudgetUtilizedSnapshot = budget.UtilizedAmount;
        commitment.BudgetCommittedAfter = budget.CommittedAmount;
        commitment.BudgetReservedAfter = budget.ReservedAmount;
        commitment.BudgetAvailableAfter = budget.RemainingAmount;
        commitment.UpdatedAt = now;

        await _reservationStore.SetContractReleaseContextAsync(
            contract.Id,
            cancellationToken);
        try
        {
            await _unitOfWork.Repository<ProcurementBudget>().UpdateAsync(budget);
            await Commitments.UpdateAsync(commitment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await _reservationStore.SetContractReleaseContextAsync(
                null,
                cancellationToken);
        }
        return release;
    }

    private async Task EnsureContractAllocationsFullyUtilizedAsync(
        ProcurementBudgetCommitmentLedgerEntry formal,
        CancellationToken cancellationToken)
    {
        var allocations = await Ledger.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.FormalCommitmentEntryId == formal.Id &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var allocation in allocations)
        {
            var adjustment = await _unitOfWork
                .Repository<ProcurementPurchaseOrderCommitmentAdjustment>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == allocation.SourceId &&
                    item.PurchaseRequisitionId == formal.PurchaseRequisitionId &&
                    item.BudgetCommitmentId == formal.ProcurementBudgetCommitmentId &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.DeltaAmount, cancellationToken) ?? 0m;
            var effectiveAllocation = decimal.Round(
                allocation.Amount + adjustment,
                2,
                MidpointRounding.AwayFromZero);
            if (effectiveAllocation < 0m)
                throw Error(
                    "CONTRACT_CHILD_PO_ALLOCATION_INVALID",
                    $"Purchase order allocation {allocation.SourceReference} has an invalid negative effective amount.");

            var receiptIds = _unitOfWork.Repository<PurchaseOrderReceipt>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == allocation.SourceId &&
                    !item.IsDeleted)
                .Select(item => item.Id);
            var utilized = await Ledger.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.FormalCommitmentEntryId == formal.Id &&
                    item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization &&
                    item.SourceType == ReceiptSource &&
                    receiptIds.Contains(item.SourceId) &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
            utilized = decimal.Round(utilized, 2, MidpointRounding.AwayFromZero);
            if (utilized > effectiveAllocation)
                throw Error(
                    "CONTRACT_CHILD_PO_ALLOCATION_UTILIZATION_EXCEEDED",
                    $"Purchase order allocation {allocation.SourceReference} is utilized above its effective allocation.");
            if (utilized < effectiveAllocation)
                throw Error(
                    "CONTRACT_CHILD_PO_ALLOCATION_OUTSTANDING",
                    $"Purchase order {allocation.SourceReference} still has {effectiveAllocation - utilized:0.00} {allocation.Currency} of unutilized contract allocation. Complete its governed receipt/utilization lifecycle before closing the contract.");
        }
    }

    private async Task<ProcurementBudgetCommitmentLedgerEntry> CommitAsync(
        Guid requisitionId, string sourceType, Guid sourceId, string sourceReference,
        decimal amount, string currency, string correlationId, CancellationToken cancellationToken)
    {
        EnsureContextAndTransaction();
        EnsurePositive(amount, currency);
        var existing = await FindEntryAsync(
            ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
            sourceType, sourceId, cancellationToken);
        if (existing is not null)
        {
            EnsureIdempotent(existing, amount, currency);
            return existing;
        }

        var commitment = await Commitments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseRequisitionId == requisitionId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("BUDGET_RESERVATION_REQUIRED", "The approved source has no active purchase-requisition budget reservation.");
        if (commitment.Status != ProcurementBudgetCommitmentStatus.Reserved)
            throw Error("BUDGET_RESERVATION_INACTIVE", "The purchase-requisition budget reservation is not active.");
        if (!SameCurrency(commitment.Currency, currency))
            throw Error("BUDGET_COMMITMENT_CURRENCY_MISMATCH", "The formal commitment currency does not match the approved reservation.");

        var alreadyCommitted = await Ledger.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProcurementBudgetCommitmentId == commitment.Id && !item.IsDeleted &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var releasedFormalCommitments = await Ledger.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProcurementBudgetCommitmentId == commitment.Id &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.FormalCommitmentEntryId != null &&
                !item.IsDeleted)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var directPurchaseOrderIds = Ledger.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProcurementBudgetCommitmentId == commitment.Id &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment &&
                item.SourceType == PurchaseOrderSource &&
                !item.IsDeleted)
            .Select(item => item.SourceId);
        var directPurchaseOrderAdjustments = await _unitOfWork
            .Repository<ProcurementPurchaseOrderCommitmentAdjustment>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.BudgetCommitmentId == commitment.Id &&
                directPurchaseOrderIds.Contains(item.PurchaseOrderId) &&
                !item.IsDeleted)
            .SumAsync(item => (decimal?)item.DeltaAmount, cancellationToken) ?? 0m;
        alreadyCommitted = alreadyCommitted - releasedFormalCommitments +
            directPurchaseOrderAdjustments;
        if (alreadyCommitted + amount > commitment.ReservedAmount)
            throw Error("BUDGET_RESERVATION_EXCEEDED", "Cumulative approved PO or contract exposure exceeds the purchase-requisition reservation.");

        var budget = await _reservationStore.GetBudgetForUpdateAsync(
            _currentUser.TenantId, commitment.ProcurementBudgetId, cancellationToken)
            ?? throw Error("BUDGET_NOT_FOUND", "The approved procurement budget is unavailable.");
        if (budget.ReservedAmount < amount)
            throw Error("BUDGET_RESERVED_BALANCE_MISMATCH", "The procurement budget reserved balance does not cover this formal commitment.");

        var now = DateTime.UtcNow;
        var entry = NewEntry(commitment, ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
            sourceType, sourceId, sourceReference, amount, currency, correlationId, now);
        var formallyCommittedBefore = commitment.FormallyCommittedAmount;
        var formallyCommittedAfter = alreadyCommitted + amount;

        // Persist immutable proof first inside the existing transaction. The
        // aggregate trigger then accepts only the exact ledger-backed delta.
        await Ledger.AddAsync(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _reservationStore.SetFormalCommitmentContextAsync(
            new ProcurementFormalCommitmentMutationContext(
                commitment.TenantId,
                commitment.Id,
                entry.Id,
                entry.SourceType,
                entry.SourceId,
                formallyCommittedBefore,
                formallyCommittedAfter,
                entry.CorrelationId),
            cancellationToken);
        try
        {
            budget.ReservedAmount -= amount;
            budget.CommittedAmount += amount;
            budget.RemainingAmount = Available(budget);
            budget.UpdatedAt = now;
            commitment.FormallyCommittedAmount = formallyCommittedAfter;
            commitment.CorrelationId = entry.CorrelationId;
            commitment.BudgetAllocatedSnapshot = budget.AllocatedAmount;
            commitment.BudgetUtilizedSnapshot = budget.UtilizedAmount;
            commitment.BudgetReservedAfter = budget.ReservedAmount;
            commitment.BudgetCommittedAfter = budget.CommittedAmount;
            commitment.BudgetAvailableAfter = budget.RemainingAmount;
            commitment.UpdatedAt = now;
            await _unitOfWork.Repository<ProcurementBudget>().UpdateAsync(budget);
            await Commitments.UpdateAsync(commitment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await _reservationStore.SetFormalCommitmentContextAsync(
                null,
                cancellationToken);
        }
        return entry;
    }

    private async Task<ProcurementBudgetCommitmentLedgerEntry> UtilizeAsync(
        string formalSourceType, Guid formalSourceId, string utilizationSourceType,
        Guid utilizationSourceId, string utilizationReference, decimal amount,
        string correlationId, CancellationToken cancellationToken)
    {
        EnsureContextAndTransaction();
        if (amount <= 0m) throw Error("BUDGET_UTILIZATION_AMOUNT_INVALID", "The utilization amount must be positive.");
        var existing = await FindEntryAsync(ProcurementBudgetCommitmentLedgerEntryType.Utilization,
            utilizationSourceType, utilizationSourceId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Amount != amount)
                throw Error("BUDGET_UTILIZATION_IDEMPOTENCY_CONFLICT", "The receipt or certificate was already applied with a different amount.");
            return existing;
        }

        ProcurementBudgetCommitmentLedgerEntry? allocation = null;
        var formal = await FindEntryAsync(ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
            formalSourceType, formalSourceId, cancellationToken);
        if (formal is null && formalSourceType == PurchaseOrderSource)
        {
            allocation = await FindEntryAsync(
                ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation,
                PurchaseOrderSource, formalSourceId, cancellationToken);
            if (allocation?.FormalCommitmentEntryId is { } contractEntryId)
            {
                formal = await Ledger.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId && item.Id == contractEntryId && !item.IsDeleted)
                    .SingleOrDefaultAsync(cancellationToken);
            }
        }
        if (formal is null)
            throw Error("FORMAL_BUDGET_COMMITMENT_REQUIRED",
                "No formal PO or contract commitment exists for this acceptance evidence.");

        if (formalSourceType == PurchaseOrderSource && utilizationSourceType == ReceiptSource)
        {
            var receiptMatchesPurchaseOrder = await _unitOfWork.Repository<PurchaseOrderReceipt>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.Id == utilizationSourceId && item.PurchaseOrderId == formalSourceId && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!receiptMatchesPurchaseOrder)
                throw Error("BUDGET_UTILIZATION_SOURCE_LINEAGE_INVALID",
                    "The receipt does not belong to the purchase order whose commitment is being utilized.");
        }
        if (formalSourceType == ContractSource && utilizationSourceType == CertificateSource)
        {
            var certificateMatchesContract = await _unitOfWork.Repository<ProjectPaymentCertificate>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.Id == utilizationSourceId && item.ContractId == formalSourceId && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!certificateMatchesContract)
                throw Error("BUDGET_UTILIZATION_SOURCE_LINEAGE_INVALID",
                    "The payment certificate does not belong to the contract whose commitment is being utilized.");
        }

        var utilized = await Ledger.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.FormalCommitmentEntryId == formal.Id && !item.IsDeleted &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var purchaseOrderAdjustment = formalSourceType == PurchaseOrderSource
            ? await _unitOfWork.Repository<ProcurementPurchaseOrderCommitmentAdjustment>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == formalSourceId &&
                    item.PurchaseRequisitionId == formal.PurchaseRequisitionId &&
                    item.BudgetCommitmentId == formal.ProcurementBudgetCommitmentId &&
                    !item.IsDeleted)
                .SumAsync(item => (decimal?)item.DeltaAmount, cancellationToken) ?? 0m
            : 0m;
        var releasedFormalCapacity = await Ledger.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.FormalCommitmentEntryId == formal.Id &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                !item.IsDeleted)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var formalCapacity = formal.Amount - releasedFormalCapacity +
            (allocation is null ? purchaseOrderAdjustment : 0m);
        if (utilized + amount > formalCapacity)
            throw Error("FORMAL_BUDGET_COMMITMENT_EXCEEDED", "Cumulative receipts or certificates exceed the formal commitment.");
        if (allocation is not null)
        {
            var purchaseOrderReceiptIds = _unitOfWork.Repository<PurchaseOrderReceipt>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == formalSourceId && !item.IsDeleted)
                .Select(item => item.Id);
            var purchaseOrderUtilized = await Ledger.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                    item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization &&
                    item.SourceType == ReceiptSource && purchaseOrderReceiptIds.Contains(item.SourceId))
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
            var effectiveAllocation = allocation.Amount + purchaseOrderAdjustment;
            if (purchaseOrderUtilized + amount > effectiveAllocation)
                throw Error("PO_CONTRACT_ALLOCATION_EXCEEDED",
                    "Cumulative accepted receipts exceed this purchase order's allocation under the contract commitment.");
        }

        var commitment = await Commitments.GetQueryable(item => item.Id == formal.ProcurementBudgetCommitmentId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .SingleAsync(cancellationToken);
        var budget = await _reservationStore.GetBudgetForUpdateAsync(
            _currentUser.TenantId, formal.ProcurementBudgetId, cancellationToken)
            ?? throw Error("BUDGET_NOT_FOUND", "The committed procurement budget is unavailable.");
        if (budget.CommittedAmount < amount)
            throw Error("BUDGET_COMMITTED_BALANCE_MISMATCH", "The formal committed balance does not cover this utilization.");

        var now = DateTime.UtcNow;
        var entry = NewEntry(commitment, ProcurementBudgetCommitmentLedgerEntryType.Utilization,
            utilizationSourceType, utilizationSourceId, utilizationReference, amount,
            formal.Currency, correlationId, now);
        entry.FormalCommitmentEntryId = formal.Id;
        var utilizedBefore = commitment.UtilizedAmount;
        var utilizedAfter = utilizedBefore + amount;

        // Persist immutable utilization proof first inside the transaction;
        // aggregate mutation is accepted only under this exact context.
        await Ledger.AddAsync(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _reservationStore.SetUtilizationContextAsync(
            new ProcurementUtilizationMutationContext(
                commitment.TenantId,
                commitment.Id,
                entry.Id,
                entry.SourceType,
                entry.SourceId,
                utilizedBefore,
                utilizedAfter,
                entry.CorrelationId),
            cancellationToken);
        try
        {
            budget.CommittedAmount -= amount;
            budget.UtilizedAmount += amount;
            budget.RemainingAmount = Available(budget);
            budget.UpdatedAt = now;
            commitment.UtilizedAmount = utilizedAfter;
            commitment.CorrelationId = entry.CorrelationId;
            // Receipt/certificate utilization does not terminalize the shared PR
            // envelope. A later PO may legitimately expand the same reservation;
            // only an explicit governed close/release operation terminalizes it.
            commitment.BudgetAllocatedSnapshot = budget.AllocatedAmount;
            commitment.BudgetUtilizedSnapshot = budget.UtilizedAmount;
            commitment.BudgetCommittedAfter = budget.CommittedAmount;
            commitment.BudgetReservedAfter = budget.ReservedAmount;
            commitment.BudgetAvailableAfter = budget.RemainingAmount;
            commitment.UpdatedAt = now;
            await _unitOfWork.Repository<ProcurementBudget>().UpdateAsync(budget);
            await Commitments.UpdateAsync(commitment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await _reservationStore.SetUtilizationContextAsync(
                null,
                cancellationToken);
        }
        return entry;
    }

    private async Task<ProcurementBudgetCommitmentLedgerEntry> AllocatePurchaseOrderToContractAsync(
        PurchaseOrder purchaseOrder,
        ProcurementBudgetCommitmentLedgerEntry contractCommitment,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureContextAndTransaction();
        EnsurePositive(purchaseOrder.TotalAmount, purchaseOrder.Currency);
        if (contractCommitment.PurchaseRequisitionId != purchaseOrder.SourceRequisitionId)
            throw Error("PO_CONTRACT_REQUISITION_LINEAGE_MISMATCH",
                "The purchase order and formal contract commitment do not share the same purchase requisition.");
        if (!SameCurrency(contractCommitment.Currency, purchaseOrder.Currency))
            throw Error("PO_CONTRACT_COMMITMENT_CURRENCY_MISMATCH",
                "The purchase order currency does not match its formal contract commitment.");
        var existing = await FindEntryAsync(
            ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation,
            PurchaseOrderSource, purchaseOrder.Id, cancellationToken);
        if (existing is not null)
        {
            await EnsurePurchaseOrderIdempotentAsync(
                existing,
                purchaseOrder,
                cancellationToken);
            if (existing.FormalCommitmentEntryId != contractCommitment.Id)
                throw Error("PO_CONTRACT_COMMITMENT_IDEMPOTENCY_CONFLICT",
                    "The purchase order was already allocated to a different formal commitment.");
            return existing;
        }

        var allocations = Ledger.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation &&
                item.FormalCommitmentEntryId == contractCommitment.Id);
        var allocated = await allocations
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var allocationIds = allocations.Select(item => item.SourceId);
        var allocationAdjustments = await _unitOfWork
            .Repository<ProcurementPurchaseOrderCommitmentAdjustment>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                allocationIds.Contains(item.PurchaseOrderId) &&
                !item.IsDeleted)
            .SumAsync(item => (decimal?)item.DeltaAmount, cancellationToken) ?? 0m;
        allocated += allocationAdjustments;
        var released = await Ledger.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.FormalCommitmentEntryId == contractCommitment.Id)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var netContractCapacity = contractCommitment.Amount - released;
        if (allocated + purchaseOrder.TotalAmount > netContractCapacity)
            throw Error("PO_CONTRACT_COMMITMENT_EXCEEDED",
                "Cumulative purchase orders exceed the remaining formal contract commitment after releases.");

        var commitment = await Commitments.GetQueryable(item =>
                item.Id == contractCommitment.ProcurementBudgetCommitmentId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .SingleAsync(cancellationToken);
        var entry = NewEntry(commitment,
            ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation,
            PurchaseOrderSource, purchaseOrder.Id, purchaseOrder.OrderNumber,
            purchaseOrder.TotalAmount, purchaseOrder.Currency, correlationId, DateTime.UtcNow);
        entry.FormalCommitmentEntryId = contractCommitment.Id;
        await Ledger.AddAsync(entry);
        return entry;
    }

    private Task<ProcurementBudgetCommitmentLedgerEntry?> FindEntryAsync(
        ProcurementBudgetCommitmentLedgerEntryType entryType, string sourceType,
        Guid sourceId, CancellationToken cancellationToken) =>
        Ledger.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.EntryType == entryType && item.SourceType == sourceType &&
                item.SourceId == sourceId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task EnsurePurchaseOrderIdempotentAsync(
        ProcurementBudgetCommitmentLedgerEntry existing,
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken)
    {
        if (!purchaseOrder.SourceRequisitionId.HasValue ||
            existing.PurchaseRequisitionId != purchaseOrder.SourceRequisitionId.Value)
            throw Error(
                "BUDGET_COMMITMENT_IDEMPOTENCY_CONFLICT",
                "The purchase order was already committed against a different requisition.");

        var adjustments = await _unitOfWork
            .Repository<ProcurementPurchaseOrderCommitmentAdjustment>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        if (adjustments.Any(item =>
                item.PurchaseRequisitionId != existing.PurchaseRequisitionId ||
                item.BudgetCommitmentId != existing.ProcurementBudgetCommitmentId ||
                !SameCurrency(item.Currency, existing.Currency)))
            throw Error(
                "BUDGET_COMMITMENT_IDEMPOTENCY_CONFLICT",
                "The purchase order commitment adjustments do not share the immutable formal exposure lineage.");

        var effectiveAmount = decimal.Round(
            existing.Amount + adjustments.Sum(item => item.DeltaAmount),
            2,
            MidpointRounding.AwayFromZero);
        if (effectiveAmount != decimal.Round(
                purchaseOrder.TotalAmount,
                2,
                MidpointRounding.AwayFromZero) ||
            !SameCurrency(existing.Currency, purchaseOrder.Currency))
            throw Error(
                "BUDGET_COMMITMENT_IDEMPOTENCY_CONFLICT",
                "The purchase order was already committed with a different effective amount or currency.");
    }

    private ProcurementBudgetCommitmentLedgerEntry NewEntry(
        ProcurementBudgetCommitment commitment, ProcurementBudgetCommitmentLedgerEntryType entryType,
        string sourceType, Guid sourceId, string sourceReference, decimal amount,
        string currency, string correlationId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), TenantId = _currentUser.TenantId,
        ProcurementBudgetCommitmentId = commitment.Id,
        ProcurementBudgetId = commitment.ProcurementBudgetId,
        PurchaseRequisitionId = commitment.PurchaseRequisitionId,
        EntryType = entryType, SourceType = sourceType, SourceId = sourceId,
        SourceReference = Truncate(sourceReference, 100), Amount = amount,
        Currency = currency.Trim().ToUpperInvariant(), OccurredAtUtc = now,
        ActorUserId = _currentUser.UserId, ActorName = ActorName(),
        CorrelationId = Truncate(correlationId, 100), CreatedAt = now,
        CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId
    };

    private void EnsureContextAndTransaction()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw Error("BUDGET_LIFECYCLE_UNAUTHORIZED", "An authenticated tenant context is required.");
        if (!_reservationStore.HasRequiredTransaction)
            throw Error("BUDGET_LIFECYCLE_TRANSACTION_REQUIRED", "Formal commitment changes require the governing serializable transaction.");
    }

    private void EnsureSourceTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty || tenantId != _currentUser.TenantId)
            throw Error("BUDGET_LIFECYCLE_SOURCE_TENANT_MISMATCH",
                "The commitment source does not belong to the current tenant.");
    }

    private static void EnsurePositive(decimal amount, string currency)
    {
        if (amount <= 0m) throw Error("BUDGET_COMMITMENT_AMOUNT_INVALID", "The formal commitment amount must be positive.");
        if (string.IsNullOrWhiteSpace(currency)) throw Error("BUDGET_COMMITMENT_CURRENCY_REQUIRED", "The formal commitment currency is required.");
    }

    private static void EnsureIdempotent(ProcurementBudgetCommitmentLedgerEntry existing, decimal amount, string currency)
    {
        if (existing.Amount != amount || !SameCurrency(existing.Currency, currency))
            throw Error("BUDGET_COMMITMENT_IDEMPOTENCY_CONFLICT", "This source was already formally committed with different values.");
    }

    private static void EnsureContractReleaseIdempotent(
        ProcurementBudgetCommitmentLedgerEntry existing,
        ProcurementBudgetCommitmentLedgerEntry formal)
    {
        if (existing.FormalCommitmentEntryId != formal.Id ||
            existing.ProcurementBudgetCommitmentId != formal.ProcurementBudgetCommitmentId ||
            existing.PurchaseRequisitionId != formal.PurchaseRequisitionId ||
            existing.Amount <= 0m ||
            existing.Amount > formal.Amount ||
            !SameCurrency(existing.Currency, formal.Currency))
            throw Error(
                "CONTRACT_BUDGET_RELEASE_IDEMPOTENCY_CONFLICT",
                "The contract has a conflicting formal commitment release entry.");
    }

    private static bool SameCurrency(string left, string right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static decimal Available(ProcurementBudget budget) =>
        budget.AllocatedAmount - budget.UtilizedAmount - budget.CommittedAmount - budget.ReservedAmount;
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static string Truncate(string? value, int max) => string.IsNullOrWhiteSpace(value)
        ? "system" : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
    private static ProcurementBudgetCommitmentLifecycleException Error(string code, string message) => new(code, message);
}
