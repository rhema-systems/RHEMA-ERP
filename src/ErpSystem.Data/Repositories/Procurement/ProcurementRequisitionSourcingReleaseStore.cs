using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public sealed class ProcurementRequisitionSourcingReleaseStore : IProcurementRequisitionSourcingReleaseStore
{
    private readonly ApplicationDbContext _context;

    public ProcurementRequisitionSourcingReleaseStore(ApplicationDbContext context) => _context = context;

    public bool UsesRelationalDatabase => _context.Database.IsRelational();

    public bool HasRequiredTransaction =>
        !_context.Database.IsRelational() || _context.Database.CurrentTransaction is not null;

    public async Task<PurchaseRequisition?> GetRequisitionForUpdateAsync(
        Guid tenantId,
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            return await _context.PurchaseRequisitions.SingleOrDefaultAsync(item =>
                item.Id == requisitionId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        }

        return await _context.PurchaseRequisitions
            .FromSqlInterpolated($@"
                SELECT *
                FROM [dbo].[PurchaseRequisitions] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                WHERE [Id] = {requisitionId}
                  AND [TenantId] = {tenantId}
                  AND [IsDeleted] = CAST(0 AS bit)")
            .SingleOrDefaultAsync(cancellationToken);
    }
}
