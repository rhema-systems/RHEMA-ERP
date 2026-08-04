using System.Data;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ErpSystem.Data.Repositories.Inventory;

public sealed class InventoryNegativeStockMutationStore : IInventoryNegativeStockMutationStore
{
    private readonly ApplicationDbContext _context;

    public InventoryNegativeStockMutationStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool HasRequiredTransaction =>
        !_context.Database.IsRelational() || _context.Database.CurrentTransaction is not null;

    public async Task<long> GetCurrentTransactionIdAsync(CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational()) return 1;
        var transaction = _context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("An active transaction is required for negative-stock control.");
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT CONVERT(bigint, CURRENT_TRANSACTION_ID());";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public Task SetMutationContextAsync(
        Guid overrideId,
        long transactionId,
        CancellationToken cancellationToken = default) =>
        SetContextAsync(overrideId, transactionId, cancellationToken);

    public Task ClearMutationContextAsync(CancellationToken cancellationToken = default) =>
        SetContextAsync(null, null, cancellationToken);

    private Task SetContextAsync(Guid? overrideId, long? transactionId, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational()) return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("An active transaction is required for negative-stock mutation context.");
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context @key=N'TDC0610_OVERRIDE_ID', @value={overrideId}, @read_only=0;
             EXEC sys.sp_set_session_context @key=N'TDC0610_TRANSACTION_ID', @value={transactionId}, @read_only=0;
             """,
            cancellationToken);
    }
}
