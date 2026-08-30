using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementBudgetReservationStore
{
    bool HasRequiredTransaction { get; }

    Task<ProcurementBudget?> GetBudgetForUpdateAsync(
        Guid tenantId,
        Guid budgetId,
        CancellationToken cancellationToken = default);

    Task SetContractReleaseContextAsync(
        Guid? contractId,
        CancellationToken cancellationToken = default);

    Task SetDownstreamReservationContextAsync(
        ProcurementDownstreamReservationMutationContext? context,
        CancellationToken cancellationToken = default);

    Task SetRequisitionReservationReleaseContextAsync(
        ProcurementRequisitionReservationReleaseContext? context,
        CancellationToken cancellationToken = default);

    Task SetFormalCommitmentContextAsync(
        ProcurementFormalCommitmentMutationContext? context,
        CancellationToken cancellationToken = default);

    Task SetUtilizationContextAsync(
        ProcurementUtilizationMutationContext? context,
        CancellationToken cancellationToken = default);
}

public sealed record ProcurementDownstreamReservationMutationContext(
    Guid TenantId,
    Guid PurchaseRequisitionId,
    Guid CommitmentId,
    decimal ReservedAmountBefore,
    decimal ReservedAmountAfter,
    int ReservationSequenceBefore,
    int ReservationSequenceAfter,
    string CorrelationId);

public sealed record ProcurementRequisitionReservationReleaseContext(
    Guid TenantId,
    Guid PurchaseRequisitionId,
    Guid CommitmentId,
    decimal ReservedAmountBefore,
    int ReservationSequence,
    string CorrelationId);

public sealed record ProcurementFormalCommitmentMutationContext(
    Guid TenantId,
    Guid CommitmentId,
    Guid LedgerEntryId,
    string SourceType,
    Guid SourceId,
    decimal FormallyCommittedAmountBefore,
    decimal FormallyCommittedAmountAfter,
    string CorrelationId);

public sealed record ProcurementUtilizationMutationContext(
    Guid TenantId,
    Guid CommitmentId,
    Guid LedgerEntryId,
    string SourceType,
    Guid SourceId,
    decimal UtilizedAmountBefore,
    decimal UtilizedAmountAfter,
    string CorrelationId);
