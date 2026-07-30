using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public sealed class ProcurementPurchaseOrderAmendmentStore :
    IProcurementPurchaseOrderAmendmentStore
{
    private readonly ApplicationDbContext _context;

    public ProcurementPurchaseOrderAmendmentStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool HasRequiredTransaction =>
        !_context.Database.IsRelational() ||
        _context.Database.CurrentTransaction is not null;

    public async Task<PurchaseOrder?> GetPurchaseOrderForUpdateAsync(
        Guid tenantId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            return await _context.PurchaseOrders
                .Include(item => item.Items.Where(line => !line.IsDeleted))
                .SingleOrDefaultAsync(item =>
                    item.Id == purchaseOrderId &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
                    cancellationToken);
        }

        await _context.PurchaseOrders
            .FromSqlInterpolated($"""
                SELECT *
                FROM [dbo].[PurchaseOrders] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                WHERE [Id] = {purchaseOrderId}
                  AND [TenantId] = {tenantId}
                  AND [IsDeleted] = CAST(0 AS bit)
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return await _context.PurchaseOrders
            .Include(item => item.Items.Where(line => !line.IsDeleted))
            .SingleOrDefaultAsync(item =>
                item.Id == purchaseOrderId &&
                item.TenantId == tenantId &&
                !item.IsDeleted,
                cancellationToken);
    }

    public Task SetApprovedSourceMutationContextAsync(
        Guid amendmentId,
        CancellationToken cancellationToken = default) =>
        SetContextAsync(amendmentId, cancellationToken);

    public Task ClearApprovedSourceMutationContextAsync(
        CancellationToken cancellationToken = default) =>
        SetContextAsync(null, cancellationToken);

    private Task SetContextAsync(
        Guid? amendmentId,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The controlled purchase-order source mutation context requires an active transaction.");

        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context
                 @key=N'TDC0406_PO_AMENDMENT_ID',
                 @value={amendmentId},
                 @read_only=0
             """,
            cancellationToken);
    }
}
