using ErpSystem.Core.Entities.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed record PaymentTermBaselineDefinition(
    string Code,
    string Name,
    string Description,
    int DueDays,
    decimal DiscountPercent,
    int DiscountDays,
    int DisplayOrder,
    string ApplicableTo = "All");

/// <summary>
/// Installs the minimum payment-term catalogue required by finance documents.
/// This is deliberately a missing-only seed: tenant changes, deactivations and deletions
/// are business configuration and must not be silently reversed at application startup.
/// </summary>
public sealed class PaymentTermBaselineSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentTermBaselineSeeder> _logger;

    public PaymentTermBaselineSeeder(
        ApplicationDbContext context,
        ILogger<PaymentTermBaselineSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public static IReadOnlyList<PaymentTermBaselineDefinition> Definitions { get; } =
    [
        new("NET30", "Net 30 Days", "Payment due 30 days from invoice date.", 30, 0m, 0, 10),
        new("COD", "Cash on Delivery", "Payment due immediately on delivery.", 0, 0m, 0, 20),
        new("NET7", "Net 7 Days", "Payment due 7 days from invoice date.", 7, 0m, 0, 30),
        new("NET15", "Net 15 Days", "Payment due 15 days from invoice date.", 15, 0m, 0, 40),
        new("NET45", "Net 45 Days", "Payment due 45 days from invoice date.", 45, 0m, 0, 50),
        new("NET60", "Net 60 Days", "Payment due 60 days from invoice date.", 60, 0m, 0, 60),
        new("2_10_NET30", "2/10 Net 30", "2% discount if paid within 10 days; otherwise due in 30 days.", 30, 2m, 10, 70)
    ];

    public async Task<int> SeedAllActiveTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(tenant => tenant.Status == TenantStatus.Active && !tenant.IsDeleted)
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);

        var added = 0;
        foreach (var tenantId in tenantIds)
        {
            added += await SeedTenantBaselineAsync(_context, tenantId, cancellationToken);
        }

        _logger.LogInformation(
            "Payment-term baseline reconciliation completed for {TenantCount} active tenant(s); {AddedCount} term(s) added",
            tenantIds.Count,
            added);
        return added;
    }

    public async Task<int> SeedTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var added = await SeedTenantBaselineAsync(_context, tenantId, cancellationToken);
        _logger.LogInformation(
            "Payment-term baseline reconciliation completed for tenant {TenantId}; {AddedCount} term(s) added",
            tenantId,
            added);
        return added;
    }

    public static async Task<int> SeedTenantBaselineAsync(
        ApplicationDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existingTerms = await context.PaymentTerms
            .IgnoreQueryFilters()
            .Where(term => term.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var existingCodes = existingTerms
            .Select(term => term.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasUsableDefault = existingTerms.Any(term =>
            !term.IsDeleted && term.IsActive && term.IsDefault);
        var added = 0;

        foreach (var definition in Definitions)
        {
            if (existingCodes.Contains(definition.Code))
            {
                continue;
            }

            context.PaymentTerms.Add(new PaymentTerm
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = definition.Code,
                Name = definition.Name,
                Description = definition.Description,
                DueDays = definition.DueDays,
                DiscountPercent = definition.DiscountPercent,
                DiscountDays = definition.DiscountDays,
                IsActive = true,
                IsDefault = definition.Code == "NET30" && !hasUsableDefault,
                DisplayOrder = definition.DisplayOrder,
                ApplicableTo = definition.ApplicableTo,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            });

            if (definition.Code == "NET30" && !hasUsableDefault)
            {
                hasUsableDefault = true;
            }

            existingCodes.Add(definition.Code);
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return added;
    }
}
