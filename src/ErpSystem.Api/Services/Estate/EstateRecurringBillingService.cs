using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

public sealed record EstateRecurringBillingResult(int GroundRentInvoices, int RentInvoices, int Failures);

public sealed class EstateRecurringBillingService(
    ApplicationDbContext db,
    IInvoiceService invoiceService,
    IGroundRentAdministrationService groundRentService,
    IDistributedLockService lockService,
    ILogger<EstateRecurringBillingService> logger)
{
    private const int MaxPeriodsPerAccountPerRun = 12;

    public async Task<EstateRecurringBillingResult> RunForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var lease = await lockService.TryAcquireAsync(
            $"estate:recurring-billing:{tenantId}", TimeSpan.FromMinutes(30), cancellationToken);
        if (lease is null)
            return new EstateRecurringBillingResult(0, 0, 0);

        var today = DateTime.UtcNow.Date;
        var groundIds = await db.EstateGroundRentAccounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.Status == "Active" && item.NextDueDate <= today.AddDays(365))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var rentIds = await db.EstateManagedAssets.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.AutoGenerateRentInvoices && item.NextRentBillingDate <= today
                && item.AssetType != EstateManagedAssetType.Land)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var groundCount = 0;
        var rentCount = 0;
        var failures = 0;
        foreach (var id in groundIds)
        {
            try
            {
                // Catch up missed periods, but bound each run so a bad schedule cannot loop forever.
                for (var period = 0; period < MaxPeriodsPerAccountPerRun; period++)
                {
                    var account = await db.EstateGroundRentAccounts.AsNoTracking()
                        .FirstAsync(item => item.Id == id && item.TenantId == tenantId, cancellationToken);
                    if (account.Status != "Active"
                        || account.NextDueDate.Date > today.AddDays(account.PaymentTermsDays))
                        break;
                    await groundRentService.GenerateInvoiceAsync(id,
                        new GenerateEstateGroundRentInvoiceDto { AllowFutureDueDate = true }, cancellationToken);
                    groundCount++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                db.ChangeTracker.Clear();
                logger.LogError(ex, "Ground rent billing failed for account {AccountId}", id);
            }
        }

        foreach (var id in rentIds)
        {
            try
            {
                for (var period = 0; period < MaxPeriodsPerAccountPerRun; period++)
                {
                    var asset = await db.EstateManagedAssets
                        .FirstAsync(item => item.Id == id && item.TenantId == tenantId, cancellationToken);
                    if (!asset.AutoGenerateRentInvoices || asset.NextRentBillingDate is not { } due
                        || due.Date > today)
                        break;
                    await GenerateRentInvoiceAsync(asset, due.Date, tenantId, cancellationToken);
                    rentCount++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                db.ChangeTracker.Clear();
                logger.LogError(ex, "Monthly rent billing failed for asset {AssetId}", id);
            }
        }

        return new EstateRecurringBillingResult(groundCount, rentCount, failures);
    }

    private async Task GenerateRentInvoiceAsync(EstateManagedAsset asset, DateTime due, Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (asset.Status is not EstateManagedAssetStatus.Leased and not EstateManagedAssetStatus.Occupied
            || asset.ExternalListingType?.Contains("Lease", StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException("Only occupied monthly rentals can be billed recurrently.");
        if (!asset.CustomerBusinessPartnerId.HasValue || string.IsNullOrWhiteSpace(asset.PropertyFileReference))
            throw new InvalidOperationException("A customer and signed agreement are required for rent billing.");
        var amount = asset.ExternalMonthlyRent ?? asset.ExternalListingPrice;
        if (amount is null or <= 0m)
            throw new InvalidOperationException("A positive monthly rent is required.");

        var reference = $"RENT-{asset.AssetCode}-{due:yyyyMM}";
        if (reference.Length > 100) reference = reference[..100];
        var existing = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted
            && item.BusinessPartnerId == asset.CustomerBusinessPartnerId.Value
            && item.Reference == reference, cancellationToken);
        if (existing?.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException($"Rent invoice {reference} was cancelled; Finance must resolve it before billing continues.");

        var incomeAccount = await db.Accounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.Status == AccountStatus.Active && item.AccountType == AccountType.Revenue
                && item.AllowDirectPosting && !item.IsControlAccount
                && (item.AccountCode == "4110" || item.AccountCode == "4100"))
            .OrderBy(item => item.AccountCode == "4110" ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Finance must configure an active Rental Income account.");

        var invoice = existing is null
            ? await invoiceService.CreateAsync(new InvoiceCreateDto
            {
                BusinessPartnerId = asset.CustomerBusinessPartnerId.Value,
                InvoiceDate = DateTime.UtcNow.Date,
                DueDate = due,
                Reference = reference,
                CurrencyCode = string.IsNullOrWhiteSpace(asset.ExternalListingCurrency)
                    ? asset.Currency : asset.ExternalListingCurrency,
                ExchangeRate = 1m,
                Notes = $"Monthly rent for {asset.AssetCode}; agreement {asset.PropertyFileReference}. Source: Estate / Property Management -> Finance AR",
                LineItems =
                [
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "GLAccount",
                        GLAccountId = incomeAccount.Id,
                        Description = $"Rent {due:dd MMM yyyy} - {due.AddMonths(1).AddDays(-1):dd MMM yyyy}: {asset.Name}",
                        Quantity = 1m,
                        UnitPrice = amount.Value,
                        DiscountPercentage = 0m
                    }
                ]
            }, cancellationToken)
            : await invoiceService.GetByIdAsync(existing.Id, cancellationToken)
                ?? throw new InvalidOperationException("The existing rent invoice could not be loaded.");

        asset.LastRentInvoiceId = invoice.Id;
        asset.LastRentInvoiceNumber = invoice.InvoiceNumber;
        asset.NextRentBillingDate = due.AddMonths(1);
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = "Scheduled billing";
        await db.SaveChangesAsync(cancellationToken);

        // Finance approval rules still take precedence. A draft remains in Finance if release is blocked.
        try
        {
            if (existing is null || existing.Status is InvoiceStatus.Draft or InvoiceStatus.Approved or InvoiceStatus.ReadyToPost)
                await invoiceService.SendInvoiceAsync(invoice.Id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogInformation(ex, "Rent invoice {InvoiceId} is awaiting Finance release", invoice.Id);
        }
    }
}
