using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementBudgetCommitmentLifecycleService
{
    Task<ProcurementBudgetCommitmentLedgerEntry> CommitPurchaseOrderAsync(
        PurchaseOrder purchaseOrder, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementBudgetCommitmentLedgerEntry> CommitContractAsync(
        Contract contract, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementBudgetCommitmentLedgerEntry> UtilizePurchaseOrderAsync(
        Guid purchaseOrderId, Guid receiptId, string receiptReference, decimal amount,
        string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementBudgetCommitmentLedgerEntry> UtilizeContractCertificateAsync(
        Guid contractId, Guid certificateId, string certificateReference, decimal amount,
        string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementBudgetCommitmentLifecycleException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
