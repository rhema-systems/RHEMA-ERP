using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds a small, realistic set of pay components (allowances &amp; deductions) for the DEFAULT
/// tenant so the emoluments and leave-encashment features have data to work with. Idempotent:
/// each component is keyed on its <c>Code</c> and skipped if already present.
/// </summary>
public class EmolumentDataSeeder
{
    private static readonly Guid DefaultTenantIdFallback = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string SeedUser = "system-seed";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmolumentDataSeeder> _logger;

    public EmolumentDataSeeder(ApplicationDbContext context, ILogger<EmolumentDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedAsync()
    {
        _logger.LogInformation("Starting emolument (pay component) seeding...");

        var tenantId = await ResolveDefaultTenantIdAsync();
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("Default tenant not found; skipping emolument seeding.");
            return 0;
        }

        var now = DateTime.UtcNow;
        var effectiveFrom = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var defaults = new (string Code, string Name, PayComponentType Type, PayComponentCalculationBasis Basis, decimal? Default, bool Taxable)[]
        {
            ("HOUSING",   "Housing Allowance",   PayComponentType.Allowance, PayComponentCalculationBasis.PercentageOfBasic, 20m,    true),
            ("TRANSPORT", "Transport Allowance", PayComponentType.Allowance, PayComponentCalculationBasis.FixedAmount,       500m,   true),
            ("MEDICAL",   "Medical Allowance",   PayComponentType.Allowance, PayComponentCalculationBasis.FixedAmount,       300m,   false),
            ("RESP",      "Responsibility Allowance", PayComponentType.Allowance, PayComponentCalculationBasis.PercentageOfBasic, 10m, true),
            ("PAYE",      "PAYE Tax",            PayComponentType.Deduction, PayComponentCalculationBasis.PercentageOfBasic, 15m,    false),
            ("PENSION",   "Pension (SSNIT 5.5%)",PayComponentType.Deduction, PayComponentCalculationBasis.PercentageOfBasic, 5.5m,   false),
        };

        var existingCodes = await _context.PayComponents
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.Code)
            .ToListAsync();
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var d in defaults)
        {
            if (existing.Contains(d.Code)) continue;

            _context.PayComponents.Add(new PayComponent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = d.Code,
                Name = d.Name,
                ComponentType = d.Type,
                CalculationBasis = d.Basis,
                DefaultAmount = d.Default,
                IsTaxable = d.Taxable,
                EffectiveFrom = effectiveFrom,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = SeedUser
            });
            added++;
        }

        if (added > 0)
            await _context.SaveChangesAsync();

        _logger.LogInformation("Emolument seeding completed. {count} pay component(s) added.", added);
        return added;
    }

    private async Task<Guid> ResolveDefaultTenantIdAsync()
    {
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == DefaultTenantIdFallback);
        return tenant?.Id ?? Guid.Empty;
    }
}
