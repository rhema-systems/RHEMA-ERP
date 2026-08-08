using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds enterprise-grade benefit definitions that exercise the new tax / Benefit-in-Kind /
/// statutory fields, including Ghana (GRA, Income Tax Act 896) statutory BIK valuations encoded as
/// configurable rate/cap fields. Idempotent by policy code; safe to run repeatedly.
/// </summary>
public class BenefitEnterpriseDataSeeder
{
    private static readonly Guid DefaultTenantIdFallback = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ApplicationDbContext _context;
    private readonly ILogger<BenefitEnterpriseDataSeeder> _logger;

    public BenefitEnterpriseDataSeeder(ApplicationDbContext context, ILogger<BenefitEnterpriseDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedAsync()
    {
        var tenantId = await ResolveDefaultTenantIdAsync();
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("Default tenant not found; skipping enterprise benefit seeding.");
            return 0;
        }

        var baseEffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var added = 0;

        foreach (var seed in BuildSeeds(baseEffectiveFrom))
        {
            var exists = await _context.BenefitPolicies
                .AnyAsync(p => p.TenantId == tenantId && p.PolicyCode == seed.PolicyCode && !p.IsDeleted);

            if (exists)
            {
                continue;
            }

            seed.TenantId = tenantId;
            seed.CreatedAt = DateTime.UtcNow;
            seed.CreatedBy = "System";
            _context.BenefitPolicies.Add(seed);
            added++;
            _logger.LogInformation("Seeded enterprise benefit policy: {Code} - {Name}", seed.PolicyCode, seed.PolicyName);
        }

        await _context.SaveChangesAsync();
        return added;
    }

    private static IEnumerable<BenefitPolicy> BuildSeeds(DateTime effectiveFrom)
    {
        // GRA BIK: company vehicle + fuel + driver = 12.5% of total cash emoluments, capped GHS 600/month.
        yield return new BenefitPolicy
        {
            PolicyType = BenefitPolicyType.Transportation,
            PolicyName = "Company Vehicle (with fuel & driver) — BIK",
            PolicyCode = "BIK-VEHICLE-FULL",
            Description = "Employer-provided vehicle, fuel and driver. Valued per GRA at 12.5% of total cash emoluments, capped at GHS 600/month.",
            Recipient = BenefitRecipient.Staff,
            CoverageLimit = 0m,
            LimitPeriod = BenefitLimitPeriod.Monthly,
            EffectiveFrom = effectiveFrom,
            IsMandatory = false,
            IsActive = true,
            DeliveryType = BenefitDeliveryType.InKind,
            Currency = "GHS",
            Frequency = PayFrequency.Monthly,
            CalculationBasis = BenefitCalculationBasis.PercentageOfGross,
            IsTaxable = true,
            TaxTreatment = BenefitTaxTreatment.FullyTaxable,
            ValuationMethod = BenefitValuationMethod.StatutoryFormula,
            ValuationRate = 12.5m,
            ValuationCap = 600m,
            IsPensionable = false,
            AffectsGrossPay = true,
            AffectsNetPay = false,
            ContributionResponsibility = BenefitContributionResponsibility.EmployerPaysAll
        };

        // GRA BIK: employer-provided furnished accommodation = 10% of total cash emoluments.
        yield return new BenefitPolicy
        {
            PolicyType = BenefitPolicyType.Housing,
            PolicyName = "Employer Accommodation (furnished) — BIK",
            PolicyCode = "BIK-ACCOM-FURN",
            Description = "Employer-provided furnished accommodation. Valued per GRA at 10% of total cash emoluments.",
            Recipient = BenefitRecipient.Staff,
            CoverageLimit = 0m,
            LimitPeriod = BenefitLimitPeriod.Monthly,
            EffectiveFrom = effectiveFrom,
            IsMandatory = false,
            IsActive = true,
            DeliveryType = BenefitDeliveryType.InKind,
            Currency = "GHS",
            Frequency = PayFrequency.Monthly,
            CalculationBasis = BenefitCalculationBasis.PercentageOfGross,
            IsTaxable = true,
            TaxTreatment = BenefitTaxTreatment.FullyTaxable,
            ValuationMethod = BenefitValuationMethod.PercentageOfCashEmoluments,
            ValuationRate = 10m,
            IsPensionable = false,
            AffectsGrossPay = true,
            AffectsNetPay = false,
            ContributionResponsibility = BenefitContributionResponsibility.EmployerPaysAll
        };

        // Cash, pensionable, fully taxable responsibility-allowance — feeds payroll as a cash earning.
        yield return new BenefitPolicy
        {
            PolicyType = BenefitPolicyType.Financial,
            PolicyName = "Responsibility Allowance (Cash)",
            PolicyCode = "CASH-RESP-ALLOW",
            Description = "Monthly cash responsibility allowance. Fully taxable and pensionable.",
            Recipient = BenefitRecipient.Staff,
            CoverageLimit = 0m,
            LimitPeriod = BenefitLimitPeriod.Monthly,
            EffectiveFrom = effectiveFrom,
            IsMandatory = false,
            IsActive = true,
            DeliveryType = BenefitDeliveryType.Cash,
            Currency = "GHS",
            Frequency = PayFrequency.Monthly,
            CalculationBasis = BenefitCalculationBasis.FixedAmount,
            FlatValue = 500m,
            EmployerContribution = 500m,
            IsTaxable = true,
            TaxTreatment = BenefitTaxTreatment.FullyTaxable,
            ValuationMethod = BenefitValuationMethod.FlatRate,
            IsPensionable = true,
            AffectsGrossPay = true,
            AffectsNetPay = true,
            ContributionResponsibility = BenefitContributionResponsibility.EmployerPaysAll,
            MinServiceMonths = 6,
            AvailableDuringProbation = false
        };

        // Tax-exempt, employer-paid medical refund up to a threshold.
        yield return new BenefitPolicy
        {
            PolicyType = BenefitPolicyType.Medical,
            PolicyName = "Medical Reimbursement (Tax-Exempt)",
            PolicyCode = "MED-REIMB-EXEMPT",
            Description = "Employer reimbursement of approved medical expenses; tax-exempt up to the annual limit.",
            Recipient = BenefitRecipient.Both,
            MaxDependents = 4,
            CoverageLimit = 6000m,
            LimitPeriod = BenefitLimitPeriod.Annual,
            EffectiveFrom = effectiveFrom,
            IsMandatory = false,
            IsActive = true,
            DeliveryType = BenefitDeliveryType.Reimbursement,
            Currency = "GHS",
            Frequency = PayFrequency.Annually,
            CalculationBasis = BenefitCalculationBasis.FixedAmount,
            FlatValue = 6000m,
            IsTaxable = false,
            TaxTreatment = BenefitTaxTreatment.TaxExempt,
            ValuationMethod = BenefitValuationMethod.FlatRate,
            IsPensionable = false,
            AffectsGrossPay = false,
            AffectsNetPay = false,
            ContributionResponsibility = BenefitContributionResponsibility.EmployerPaysAll
        };
    }

    private async Task<Guid> ResolveDefaultTenantIdAsync()
    {
        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Code == "DEFAULT" && !t.IsDeleted);
        if (tenant != null) return tenant.Id;

        var exists = await _context.Tenants.AsNoTracking().AnyAsync(t => t.Id == DefaultTenantIdFallback && !t.IsDeleted);
        return exists ? DefaultTenantIdFallback : Guid.Empty;
    }
}
