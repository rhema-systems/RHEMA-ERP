using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Idempotently supplies missing account mappings for manifest-owned statutory taxes.
/// Existing tenant choices are never overwritten.
/// </summary>
public sealed class FinanceTaxAccountProvisioningSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger _logger;

    public FinanceTaxAccountProvisioningSeeder(ApplicationDbContext db, ILogger logger) =>
        (_db, _logger) = (db, logger);

    public async Task SeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var accounts = await _db.Accounts
            .Where(account => account.TenantId == tenantId && !account.IsDeleted &&
                new[] { "1130", "1140", "2200" }.Contains(account.AccountCode))
            .ToDictionaryAsync(account => account.AccountCode, cancellationToken);

        var whtReceivable = RequireAccount(accounts, "1130", AccountType.Asset);
        var inputTaxReceivable = RequireAccount(accounts, "1140", AccountType.Asset);
        var outputTaxPayable = RequireAccount(accounts, "2200", AccountType.Liability);

        var taxes = await _db.Taxes
            .Where(tax => tax.TenantId == tenantId && !tax.IsDeleted)
            .ToDictionaryAsync(tax => tax.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var changed = false;
        changed |= SetMissing(taxes, ["NHIL", "GETFUND", "VAT-STD", "WHT-SERV", "WHT-GOODS", "WHT-WORKS"],
            tax => tax.TaxPayableAccountId, (tax, value) => tax.TaxPayableAccountId = value, outputTaxPayable.Id);
        changed |= SetMissing(taxes, ["WHT-REC-SERV", "VAT-WHT-REC"],
            tax => tax.TaxReceivableAccountId, (tax, value) => tax.TaxReceivableAccountId = value, whtReceivable.Id);
        changed |= SetMissing(taxes, ["NHIL-PUR", "GETFUND-PUR", "VAT-STD-PUR"],
            tax => tax.TaxReceivableAccountId, (tax, value) => tax.TaxReceivableAccountId = value, inputTaxReceivable.Id);

        if (!changed) return;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Provisioned missing canonical tax account mappings for tenant {TenantId}", tenantId);
    }

    private static Account RequireAccount(
        IReadOnlyDictionary<string, Account> accounts,
        string code,
        AccountType expectedType)
    {
        if (!accounts.TryGetValue(code, out var account) ||
            account.Status != AccountStatus.Active ||
            account.AccountType != expectedType ||
            !account.IsControlAccount)
        {
            throw new InvalidOperationException(
                $"FINANCE_TAX_ACCOUNT_{code}_NOT_READY: active {expectedType} control account {code} is required.");
        }

        return account;
    }

    private static bool SetMissing(
        IReadOnlyDictionary<string, Tax> taxes,
        IEnumerable<string> codes,
        Func<Tax, Guid?> get,
        Action<Tax, Guid> set,
        Guid accountId)
    {
        var changed = false;
        foreach (var code in codes)
        {
            if (!taxes.TryGetValue(code, out var tax) || get(tax).HasValue) continue;
            set(tax, accountId);
            tax.UpdatedAt = DateTime.UtcNow;
            tax.UpdatedBy = "System (Finance tax account provisioning)";
            changed = true;
        }
        return changed;
    }
}
