using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds default tenant Benefit Policy demo data.
/// Idempotent: safe to run multiple times.
/// </summary>
public class BenefitPolicyDataSeeder
{
    private static readonly Guid DefaultTenantIdFallback = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ApplicationDbContext _context;
    private readonly ILogger<BenefitPolicyDataSeeder> _logger;

    public BenefitPolicyDataSeeder(ApplicationDbContext context, ILogger<BenefitPolicyDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting Benefit Policy data seeding...");

            var tenantId = await ResolveDefaultTenantIdAsync();
            if (tenantId == Guid.Empty)
            {
                _logger.LogWarning("Default tenant not found; skipping Benefit Policy seeding.");
                return;
            }

            var baseEffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var policies = new List<BenefitPolicySeed>
            {
                new(
                    PolicyType: BenefitPolicyType.Medical,
                    PolicyName: "Medical Cover (Standard)",
                    PolicyCode: "MED-STD",
                    Description: "Annual medical cover for staff and eligible dependents.",
                    Recipient: BenefitRecipient.Both,
                    MaxDependents: 4,
                    EmployeeContribution: 50m,
                    EmployerContribution: 150m,
                    CoverageLimit: 5000m,
                    LimitPeriod: BenefitLimitPeriod.Annual,
                    EffectiveFrom: baseEffectiveFrom,
                    EffectiveTo: null,
                    IsMandatory: true,
                    IsActive: true,
                    Relations:
                    [
                        new RelationSeed(BenefitRelationType.Spouse, 18, 65, true),
                        new RelationSeed(BenefitRelationType.Child, 0, 23, true)
                    ]
                ),

                new(
                    PolicyType: BenefitPolicyType.Insurance,
                    PolicyName: "Life Insurance (Basic)",
                    PolicyCode: "INS-LIFE",
                    Description: "Basic life insurance for staff.",
                    Recipient: BenefitRecipient.Staff,
                    MaxDependents: null,
                    EmployeeContribution: 0m,
                    EmployerContribution: 25m,
                    CoverageLimit: 100000m,
                    LimitPeriod: BenefitLimitPeriod.Lifetime,
                    EffectiveFrom: baseEffectiveFrom,
                    EffectiveTo: null,
                    IsMandatory: true,
                    IsActive: true,
                    Relations: []
                ),

                new(
                    PolicyType: BenefitPolicyType.Transportation,
                    PolicyName: "Transport Allowance",
                    PolicyCode: "TRN-ALLOW",
                    Description: "Monthly transport allowance.",
                    Recipient: BenefitRecipient.Staff,
                    MaxDependents: null,
                    EmployeeContribution: null,
                    EmployerContribution: null,
                    CoverageLimit: 300m,
                    LimitPeriod: BenefitLimitPeriod.Monthly,
                    EffectiveFrom: baseEffectiveFrom,
                    EffectiveTo: null,
                    IsMandatory: false,
                    IsActive: true,
                    Relations: []
                ),

                new(
                    PolicyType: BenefitPolicyType.Housing,
                    PolicyName: "Housing Allowance",
                    PolicyCode: "HOU-ALLOW",
                    Description: "Monthly housing support allowance.",
                    Recipient: BenefitRecipient.Staff,
                    MaxDependents: null,
                    EmployeeContribution: null,
                    EmployerContribution: null,
                    CoverageLimit: 1000m,
                    LimitPeriod: BenefitLimitPeriod.Monthly,
                    EffectiveFrom: baseEffectiveFrom,
                    EffectiveTo: null,
                    IsMandatory: false,
                    IsActive: true,
                    Relations: []
                ),

                new(
                    PolicyType: BenefitPolicyType.Educational,
                    PolicyName: "Education Support (Dependents)",
                    PolicyCode: "EDU-DEP",
                    Description: "Annual education support for eligible dependent children.",
                    Recipient: BenefitRecipient.Dependent,
                    MaxDependents: 3,
                    EmployeeContribution: 0m,
                    EmployerContribution: 200m,
                    CoverageLimit: 2000m,
                    LimitPeriod: BenefitLimitPeriod.Annual,
                    EffectiveFrom: baseEffectiveFrom,
                    EffectiveTo: null,
                    IsMandatory: false,
                    IsActive: true,
                    Relations:
                    [
                        new RelationSeed(BenefitRelationType.Child, 3, 25, true)
                    ]
                ),

                new(
                    PolicyType: BenefitPolicyType.Technology,
                    PolicyName: "Technology Stipend",
                    PolicyCode: "TECH-STIP",
                    Description: "Annual stipend for work-related devices and connectivity.",
                    Recipient: BenefitRecipient.Staff,
                    MaxDependents: null,
                    EmployeeContribution: null,
                    EmployerContribution: null,
                    CoverageLimit: 800m,
                    LimitPeriod: BenefitLimitPeriod.Annual,
                    EffectiveFrom: baseEffectiveFrom,
                    EffectiveTo: null,
                    IsMandatory: false,
                    IsActive: true,
                    Relations: []
                )
            };

            foreach (var seed in policies)
            {
                var policy = await _context.BenefitPolicies
                    .Include(p => p.BenefitPolicyRelations)
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PolicyCode == seed.PolicyCode && !p.IsDeleted);

                if (policy == null)
                {
                    policy = new BenefitPolicy
                    {
                        TenantId = tenantId,
                        PolicyType = seed.PolicyType,
                        PolicyName = seed.PolicyName,
                        PolicyCode = seed.PolicyCode,
                        Description = seed.Description,
                        Recipient = seed.Recipient,
                        MaxDependents = seed.MaxDependents,
                        EmployeeContribution = seed.EmployeeContribution,
                        EmployerContribution = seed.EmployerContribution,
                        CoverageLimit = seed.CoverageLimit,
                        LimitPeriod = seed.LimitPeriod,
                        EffectiveFrom = seed.EffectiveFrom,
                        EffectiveTo = seed.EffectiveTo,
                        IsMandatory = seed.IsMandatory,
                        IsActive = seed.IsActive,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System",
                        IsDeleted = false
                    };

                    _context.BenefitPolicies.Add(policy);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Seeded benefit policy: {PolicyCode} - {PolicyName}", policy.PolicyCode, policy.PolicyName);
                }

                // Ensure relation rules exist (unique by TenantId + PolicyId + RelationType)
                foreach (var relSeed in seed.Relations)
                {
                    var exists = await _context.Set<BenefitPolicyRelation>().AnyAsync(r =>
                        r.TenantId == tenantId
                        && r.BenefitPolicyId == policy.Id
                        && r.RelationType == relSeed.RelationType
                        && !r.IsDeleted);

                    if (exists) continue;

                    _context.Set<BenefitPolicyRelation>().Add(new BenefitPolicyRelation
                    {
                        TenantId = tenantId,
                        BenefitPolicyId = policy.Id,
                        RelationType = relSeed.RelationType,
                        MinAge = relSeed.MinAge,
                        MaxAge = relSeed.MaxAge,
                        IsActive = relSeed.IsActive,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System",
                        IsDeleted = false
                    });
                }

                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Benefit Policy data seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding Benefit Policy data");
            throw;
        }
    }

    private async Task<Guid> ResolveDefaultTenantIdAsync()
    {
        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Code == "DEFAULT" && !t.IsDeleted);
        if (tenant != null) return tenant.Id;

        // Fallback for environments where DEFAULT tenant is seeded via migrations with a known Guid.
        var exists = await _context.Tenants.AsNoTracking().AnyAsync(t => t.Id == DefaultTenantIdFallback && !t.IsDeleted);
        return exists ? DefaultTenantIdFallback : Guid.Empty;
    }

    private sealed record BenefitPolicySeed(
        BenefitPolicyType PolicyType,
        string PolicyName,
        string PolicyCode,
        string? Description,
        BenefitRecipient Recipient,
        int? MaxDependents,
        decimal? EmployeeContribution,
        decimal? EmployerContribution,
        decimal CoverageLimit,
        BenefitLimitPeriod LimitPeriod,
        DateTime EffectiveFrom,
        DateTime? EffectiveTo,
        bool IsMandatory,
        bool IsActive,
        List<RelationSeed> Relations);

    private sealed record RelationSeed(BenefitRelationType RelationType, int? MinAge, int? MaxAge, bool IsActive);
}
