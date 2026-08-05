using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public sealed class ProcurementReceiptInspectionStore :
    IProcurementReceiptInspectionStore
{
    private readonly ApplicationDbContext _context;

    public ProcurementReceiptInspectionStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool HasRequiredTransaction =>
        !_context.Database.IsRelational() ||
        _context.Database.CurrentTransaction is not null;

    public Task SetMutationContextAsync(
        Guid inspectionCaseId,
        CancellationToken cancellationToken = default) =>
        SetContextAsync(inspectionCaseId, cancellationToken);

    public Task ClearMutationContextAsync(
        CancellationToken cancellationToken = default) =>
        SetContextAsync(null, cancellationToken);

    private Task SetContextAsync(
        Guid? inspectionCaseId,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational()) return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The receipt-inspection mutation context requires an active transaction.");
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context
                 @key=N'TDC0502_RECEIPT_INSPECTION_CASE_ID',
                 @value={inspectionCaseId},
                 @read_only=0
             """,
            cancellationToken);
    }
}
