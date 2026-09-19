using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// One reciprocal C7/C8 namespace for owner-effect receipts. Both producer paths take the same
/// transaction-owned lock before checking either durable receipt table, so cross-table races have one winner.
/// </summary>
internal static class FinanceProducerOwnerEffectAuthority
{
    public static async Task AcquireAsync(ApplicationDbContext db, Guid tenantId, string participantCode,
        string effectFingerprint, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer()) return;
        // Keep this exact resource grammar in the C7/C8 receipt triggers. The unhashed value is
        // bounded below SQL Server's 255-character applock limit and lets raw SQL share the lock.
        var resource = $"FIN:C7C8:{tenantId:D}|{participantCode.Trim().ToUpperInvariant()}|{effectFingerprint.Trim().ToUpperInvariant()}";
        await db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @result int;
EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=30000;
IF @result < 0 THROW 51000, 'PRODUCER_OWNER_EFFECT_LOCK_FAILED: receipt identity could not be serialized.', 1;", cancellationToken);
    }

    public static async Task<bool> IsUsedByAnotherAsync(ApplicationDbContext db, Guid tenantId,
        string participantCode, string effectFingerprint, Guid? accountingEventId, Guid? producerIntentGroupId,
        CancellationToken cancellationToken)
    {
        var participant = participantCode.Trim().ToUpperInvariant();
        var fingerprint = effectFingerprint.Trim().ToUpperInvariant();
        return await db.AccountingEventProducerReceipts.AsNoTracking().AnyAsync(item => item.TenantId == tenantId
                && (!accountingEventId.HasValue || item.AccountingEventId != accountingEventId.Value)
                && item.ParticipantCode == participant && item.EffectFingerprint == fingerprint, cancellationToken)
            || await db.ProducerIntentGroupReceipts.AsNoTracking().AnyAsync(item => item.TenantId == tenantId
                && (!producerIntentGroupId.HasValue || item.ProducerIntentGroupId != producerIntentGroupId.Value)
                && item.ParticipantCode == participant && item.EffectFingerprint == fingerprint, cancellationToken);
    }
}
