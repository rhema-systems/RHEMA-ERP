using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public sealed class ProcurementBudgetReservationStore : IProcurementBudgetReservationStore
{
    private readonly ApplicationDbContext _context;

    public ProcurementBudgetReservationStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool HasRequiredTransaction =>
        !_context.Database.IsRelational() || _context.Database.CurrentTransaction is not null;

    public async Task<ProcurementBudget?> GetBudgetForUpdateAsync(
        Guid tenantId,
        Guid budgetId,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            return await _context.ProcurementBudgets.SingleOrDefaultAsync(item =>
                item.Id == budgetId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        }

        return await _context.ProcurementBudgets
            .FromSqlInterpolated($@"
                SELECT *
                FROM [dbo].[ProcurementBudgets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                WHERE [Id] = {budgetId}
                  AND [TenantId] = {tenantId}
                  AND [IsDeleted] = CAST(0 AS bit)")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task SetContractReleaseContextAsync(
        Guid? contractId,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The contract commitment release context requires an active transaction.");
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context
                 @key=N'PROCUREMENT_CONTRACT_RELEASE_ID',
                 @value={contractId},
                 @read_only=0
             """,
            cancellationToken);
    }

    public Task SetDownstreamReservationContextAsync(
        ProcurementDownstreamReservationMutationContext? context,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The downstream reservation context requires an active transaction.");

        Guid? tenantId = context?.TenantId;
        Guid? purchaseRequisitionId = context?.PurchaseRequisitionId;
        Guid? commitmentId = context?.CommitmentId;
        decimal? reservedAmountBefore = context?.ReservedAmountBefore;
        decimal? reservedAmountAfter = context?.ReservedAmountAfter;
        int? reservationSequenceBefore = context?.ReservationSequenceBefore;
        int? reservationSequenceAfter = context?.ReservationSequenceAfter;
        string? correlationId = context?.CorrelationId;
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_TENANT_ID', @value={tenantId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_REQUISITION_ID', @value={purchaseRequisitionId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_COMMITMENT_ID', @value={commitmentId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_BEFORE', @value={reservedAmountBefore}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_AFTER', @value={reservedAmountAfter}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_SEQUENCE_BEFORE', @value={reservationSequenceBefore}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_SEQUENCE_AFTER', @value={reservationSequenceAfter}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_CORRELATION_ID', @value={correlationId}, @read_only=0;
             """,
            cancellationToken);
    }

    public Task SetRequisitionReservationReleaseContextAsync(
        ProcurementRequisitionReservationReleaseContext? context,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The requisition reservation release context requires an active transaction.");

        Guid? tenantId = context?.TenantId;
        Guid? purchaseRequisitionId = context?.PurchaseRequisitionId;
        Guid? commitmentId = context?.CommitmentId;
        decimal? reservedAmountBefore = context?.ReservedAmountBefore;
        int? reservationSequence = context?.ReservationSequence;
        string? correlationId = context?.CorrelationId;
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_TENANT_ID', @value={tenantId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_REQUISITION_ID', @value={purchaseRequisitionId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_COMMITMENT_ID', @value={commitmentId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_AMOUNT_BEFORE', @value={reservedAmountBefore}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_SEQUENCE', @value={reservationSequence}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_CORRELATION_ID', @value={correlationId}, @read_only=0;
             """,
            cancellationToken);
    }

    public Task SetFormalCommitmentContextAsync(
        ProcurementFormalCommitmentMutationContext? context,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The formal commitment context requires an active transaction.");

        Guid? tenantId = context?.TenantId;
        Guid? commitmentId = context?.CommitmentId;
        Guid? ledgerEntryId = context?.LedgerEntryId;
        string? sourceType = context?.SourceType;
        Guid? sourceId = context?.SourceId;
        decimal? amountBefore = context?.FormallyCommittedAmountBefore;
        decimal? amountAfter = context?.FormallyCommittedAmountAfter;
        string? correlationId = context?.CorrelationId;
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_FORMAL_TENANT_ID', @value={tenantId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_FORMAL_COMMITMENT_ID', @value={commitmentId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_FORMAL_LEDGER_ENTRY_ID', @value={ledgerEntryId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_FORMAL_SOURCE_TYPE', @value={sourceType}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_FORMAL_SOURCE_ID', @value={sourceId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_FORMAL_AMOUNT_BEFORE', @value={amountBefore}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_FORMAL_AMOUNT_AFTER', @value={amountAfter}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_FORMAL_CORRELATION_ID', @value={correlationId}, @read_only=0;
             """,
            cancellationToken);
    }

    public Task SetUtilizationContextAsync(
        ProcurementUtilizationMutationContext? context,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The utilization context requires an active transaction.");

        Guid? tenantId = context?.TenantId;
        Guid? commitmentId = context?.CommitmentId;
        Guid? ledgerEntryId = context?.LedgerEntryId;
        string? sourceType = context?.SourceType;
        Guid? sourceId = context?.SourceId;
        decimal? amountBefore = context?.UtilizedAmountBefore;
        decimal? amountAfter = context?.UtilizedAmountAfter;
        string? correlationId = context?.CorrelationId;
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_UTILIZATION_TENANT_ID', @value={tenantId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_UTILIZATION_COMMITMENT_ID', @value={commitmentId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_UTILIZATION_LEDGER_ENTRY_ID', @value={ledgerEntryId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_UTILIZATION_SOURCE_TYPE', @value={sourceType}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_UTILIZATION_SOURCE_ID', @value={sourceId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_UTILIZATION_AMOUNT_BEFORE', @value={amountBefore}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_UTILIZATION_AMOUNT_AFTER', @value={amountAfter}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'PROCUREMENT_UTILIZATION_CORRELATION_ID', @value={correlationId}, @read_only=0;
             """,
            cancellationToken);
    }
}
