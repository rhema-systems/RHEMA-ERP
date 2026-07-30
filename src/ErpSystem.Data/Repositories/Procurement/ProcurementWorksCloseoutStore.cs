using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public sealed class ProcurementWorksCloseoutStore :
    IProcurementWorksCloseoutStore
{
    private readonly ApplicationDbContext _context;

    public ProcurementWorksCloseoutStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool HasRequiredTransaction =>
        !_context.Database.IsRelational() ||
        _context.Database.CurrentTransaction is not null;

    public Task SetMutationContextAsync(
        Guid actionId,
        CancellationToken cancellationToken = default) =>
        SetContextAsync(actionId, cancellationToken);

    public Task ClearMutationContextAsync(
        CancellationToken cancellationToken = default) =>
        SetContextAsync(null, cancellationToken);

    private Task SetContextAsync(
        Guid? actionId,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The Works-closeout mutation context requires an active transaction.");
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context
                 @key=N'TDC0409_WORKS_CLOSEOUT_ACTION_ID',
                 @value={actionId},
                 @read_only=0
             """,
            cancellationToken);
    }
}
