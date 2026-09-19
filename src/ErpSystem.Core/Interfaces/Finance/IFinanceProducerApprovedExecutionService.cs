using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Closed Finance execution boundary for an owner service that prepared one C7 intent and later received
/// independent approval. The caller must stage its deterministic owner effect in the same scoped
/// ApplicationDbContext and its already-open Serializable transaction before calling Execute. Finance neither
/// starts nor completes that transaction and never accepts executable owner callbacks.
/// </summary>
public interface IFinanceProducerApprovedExecutionService
{
    /// <summary>
    /// Executes a C13-prepared reversal by durable AccountingEvent identity. Finance reconstructs the complete
    /// immutable C7 request internally; the owner supplies only its deterministic compensating receipt.
    /// </summary>
    Task<FinanceProducerApprovedExecutionResultDto> ExecuteInAmbientTransactionAsync(
        Guid accountingEventId,
        ProducerOwnerEffectReceiptDto receipt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes only the exact approved event/request/receipt combination in the caller-owned ambient
    /// Serializable transaction and returns the Finance-selected C10 compatibility representation.
    /// </summary>
    Task<FinanceProducerApprovedExecutionResultDto> ExecuteInAmbientTransactionAsync(
        Guid accountingEventId,
        ProducerAccountingIntentDto preparedIntent,
        ProducerOwnerEffectReceiptDto receipt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the exact failed approved attempt after the caller has rolled back the ambient transaction.
    /// Finance requires no active transaction and establishes a clean tracking boundary before writing durable
    /// failure evidence. Callers must not use this method while their owner transaction is active.
    /// </summary>
    Task RecordFailureAfterRollbackAsync(
        Guid accountingEventId,
        ProducerAccountingIntentDto preparedIntent,
        ProducerOwnerEffectReceiptDto receipt,
        Exception failure,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed ID-only C13 reversal execution after rollback. Finance clears rolled-back tracking state
    /// and reconstructs the immutable request without returning its economics to the owner.
    /// </summary>
    Task RecordFailureAfterRollbackAsync(
        Guid accountingEventId,
        ProducerOwnerEffectReceiptDto receipt,
        Exception failure,
        CancellationToken cancellationToken = default);
}
