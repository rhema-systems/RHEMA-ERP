using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosTillFinalizationGuard
{
    /// <summary>
    /// Validates any Mobile POS close evidence for the Finance till and stages the evidence as
    /// finalized in the shared DbContext. The calling Finance service owns the atomic SaveChanges.
    /// </summary>
    Task PrepareFinalizationAsync(
        Guid cashierTillSessionId,
        Guid liquidityAccountId,
        CancellationToken cancellationToken);

    /// <summary>Stages Mobile POS evidence as returned inside Finance's recount transaction.</summary>
    Task PrepareReturnForRecountAsync(
        Guid cashierTillSessionId,
        Guid liquidityAccountId,
        CancellationToken cancellationToken);
}

public sealed class MobilePosTillFinalizationGuard : IMobilePosTillFinalizationGuard
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MobilePosTillFinalizationGuard(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required for Mobile POS.");

    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName)
        ? "Unknown"
        : _currentUser.UserName.Trim();

    public async Task PrepareFinalizationAsync(
        Guid cashierTillSessionId,
        Guid liquidityAccountId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var mappedToMobilePos = await _db.MobilePosTills.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.LiquidityAccountId == liquidityAccountId
            && !item.IsDeleted && item.Status != MobilePosTillStatus.Retired,
            cancellationToken);
        if (!mappedToMobilePos)
            return;

        var submission = await _db.MobilePosTillCloseSubmissions.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.CashierTillSessionId == cashierTillSessionId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "This Mobile POS till cannot be finalized until the cashier submits the device day-end count and sync evidence.");

        var pendingIds = MobilePosTillSessionService.DeserializePendingMutationIds(
            submission.PendingMutationIdsJson);
        if (pendingIds.Length != submission.PendingMutationCount
            || !string.Equals(
                MobilePosTillSessionService.HashPendingMutationIds(pendingIds),
                submission.PendingMutationDigest,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The retained Mobile POS pending-sync evidence failed its integrity check.");
        }

        if (pendingIds.Length > 0 && submission.Status != MobilePosTillCloseSubmissionStatus.SyncExceptionResolved)
        {
            var synchronizedIds = await _db.MobileMutationReceipts.AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && item.MobilePosDeviceId == submission.MobilePosDeviceId
                    && pendingIds.Contains(item.ClientMutationId)
                    && item.Status == MobileMutationReceiptStatus.Completed
                    && !item.IsDeleted)
                .Select(item => item.ClientMutationId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var unresolvedCount = pendingIds.Except(synchronizedIds, StringComparer.Ordinal).Count();
            if (unresolvedCount > 0)
            {
                throw new InvalidOperationException(
                    $"This till close still has {unresolvedCount} unresolved Mobile POS mutation(s). Synchronize them or record an independent HQ exception resolution before approval.");
            }
        }

        if (submission.Status is not (
            MobilePosTillCloseSubmissionStatus.ReadyForReview
            or MobilePosTillCloseSubmissionStatus.PendingSync
            or MobilePosTillCloseSubmissionStatus.SyncExceptionResolved))
        {
            throw new InvalidOperationException("The Mobile POS till-close evidence is not ready for final approval.");
        }

        var now = DateTime.UtcNow;
        submission.Status = MobilePosTillCloseSubmissionStatus.Finalized;
        submission.FinalizedAtUtc = now;
        submission.FinalizedByUserId = UserId;
        submission.UpdatedAt = now;
        submission.UpdatedBy = UserName;
        submission.LastModifiedById = UserId;
    }

    public async Task PrepareReturnForRecountAsync(
        Guid cashierTillSessionId,
        Guid liquidityAccountId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var mappedToMobilePos = await _db.MobilePosTills.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.LiquidityAccountId == liquidityAccountId
            && !item.IsDeleted && item.Status != MobilePosTillStatus.Retired,
            cancellationToken);
        if (!mappedToMobilePos)
            return;

        var submission = await _db.MobilePosTillCloseSubmissions.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.CashierTillSessionId == cashierTillSessionId && !item.IsDeleted,
            cancellationToken);
        if (submission == null)
            return;

        var now = DateTime.UtcNow;
        submission.Status = MobilePosTillCloseSubmissionStatus.ReturnedForRecount;
        submission.FinalizedAtUtc = null;
        submission.FinalizedByUserId = null;
        submission.UpdatedAt = now;
        submission.UpdatedBy = UserName;
        submission.LastModifiedById = UserId;
    }
}
