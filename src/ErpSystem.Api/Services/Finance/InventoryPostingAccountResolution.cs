using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

/// <summary>Optional item overrides share the tenant's chart of accounts; unset uses the existing Finance default.</summary>
internal static class InventoryPostingAccountResolution
{
    public static async Task<Guid> ResolveAsync(ApplicationDbContext db, Guid tenantId, Guid? itemAccountId,
        Guid? fallbackAccountId, string purpose, CancellationToken ct, params AccountType[] allowedTypes)
    {
        var id = itemAccountId ?? fallbackAccountId
            ?? throw new InvalidOperationException($"{purpose} is not configured on the item or in Finance Settings.");
        // Existing global mappings retain their posting-engine checks. New master overrides must
        // never cross tenants or turn a parent/nonposting account into a posting target.
        if (itemAccountId.HasValue)
        {
            var valid = await db.Accounts.AsNoTracking().AnyAsync(account => account.TenantId == tenantId &&
                account.Id == id && !account.IsDeleted && account.Status == AccountStatus.Active &&
                (account.AllowDirectPosting || account.IsControlAccount) && allowedTypes.Contains(account.AccountType), ct);
            if (!valid) throw new InvalidOperationException($"{purpose} must be an active posting account of the correct type in this tenant.");
        }
        return id;
    }
}
