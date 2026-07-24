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
}
