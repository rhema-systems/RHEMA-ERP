using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public sealed class ProcurementContractActivationStore : IProcurementContractActivationStore
{
    private readonly ApplicationDbContext _context;

    public ProcurementContractActivationStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool HasRequiredTransaction =>
        !_context.Database.IsRelational() ||
        _context.Database.CurrentTransaction is not null;

    public Task SetMutationContextAsync(
        Guid activationId,
        CancellationToken cancellationToken = default) =>
        SetContextAsync(activationId, cancellationToken);

    public Task ClearMutationContextAsync(
        CancellationToken cancellationToken = default) =>
        SetContextAsync(null, cancellationToken);

    private Task SetContextAsync(Guid? activationId, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return Task.CompletedTask;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The controlled contract-activation mutation context requires an active transaction.");
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context
                 @key=N'TDC0407_CONTRACT_ACTIVATION_ID',
                 @value={activationId},
                 @read_only=0
             """,
            cancellationToken);
    }
}
