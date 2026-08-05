using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds a coherent medical / health-benefits demo dataset for the DEFAULT tenant.
/// References (employees, dependents, country, staff levels) are resolved from the
/// database at run time — no hard-coded IDs. Idempotent: safe to run repeatedly,
/// keyed on natural codes/numbers.
/// </summary>
public class MedicalDataSeeder
{
    private static readonly Guid DefaultTenantIdFallback = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ApplicationDbContext _context;
    private readonly ILogger<MedicalDataSeeder> _logger;

    public MedicalDataSeeder(ApplicationDbContext context, ILogger<MedicalDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting Medical demo data seeding...");

        var tenantId = await ResolveDefaultTenantIdAsync();
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("Default tenant not found; skipping Medical seeding.");
            return;
        }

        var employees = await _context.Employees
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .ToListAsync();

        if (employees.Count == 0)
        {
            _logger.LogWarning("No employees in DEFAULT tenant; run HR seeding first. Skipping Medical seeding.");
            return;
        }

        var dependents = await _context.EmployeeDependents
            .Where(d => d.TenantId == tenantId && !d.IsDeleted)
            .ToListAsync();

        var ghanaId = (await _context.Countries.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == "Ghana" && !c.IsDeleted))?.Id;

        var staffLevels = await _context.StaffLevels
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .ToListAsync();

        Guid? StaffLevel(string code) => staffLevels.FirstOrDefault(s => s.Code == code)?.Id;

        var facilities = await SeedFacilitiesAsync(tenantId, ghanaId);
        await SeedFacilityServicesAsync(tenantId, facilities);
        var physicians = await SeedPhysiciansAsync(tenantId, facilities);
        var providers = await SeedProvidersAsync(tenantId, ghanaId);
        var plans = await SeedPlansAsync(tenantId, providers);
        await SeedNetworkAsync(tenantId, providers, facilities);
        await SeedProviderDocumentsAsync(tenantId, providers);
        await SeedPremiumsAsync(tenantId, providers, plans);
        var (scheme, tiers) = await SeedSchemeAndTiersAsync(tenantId, StaffLevel);
        var policies = await SeedPoliciesAsync(tenantId, employees, providers, plans, tiers);
        await SeedPolicyDependentsAsync(tenantId, policies, dependents);
        var profiles = await SeedHealthProfilesAsync(tenantId, employees, facilities, physicians);
        await SeedConditionsAllergiesExamsAsync(tenantId, profiles, facilities, physicians);
        await SeedClaimsAsync(tenantId, employees, facilities, physicians, policies);
        await SeedClinicalAsync(tenantId, employees, facilities, physicians, policies);
        await SeedNhisAsync(tenantId, employees, facilities);

        _logger.LogInformation("Medical demo data seeding completed.");
    }

    // ── Facilities ───────────────────────────────────────────────────────────────

    private async Task<List<HealthcareFacility>> SeedFacilitiesAsync(Guid tenantId, Guid? countryId)
    {
        var defs = new (string Code, string Name, HealthFacilityType Type, string Addr, string City, bool Nhis, bool Emerg, string Phone)[]
        {
            ("HF-KBTH", "Korle Bu Teaching Hospital", HealthFacilityType.TeachingHospital, "Guggisberg Ave, Korle Bu", "Accra", true, true, "+233302674000"),
            ("HF-NYAHO", "Nyaho Medical Centre", HealthFacilityType.MedicalCenter, "35 Aviation Rd, Airport Residential", "Accra", true, true, "+233302610900"),
            ("HF-LISTER", "Lister Hospital", HealthFacilityType.SpecializedHospital, "Spintex Road", "Accra", false, true, "+233302812000"),
            ("HF-MEDLAB", "MedLab Diagnostics", HealthFacilityType.DiagnosticCenter, "Ring Road Central", "Accra", true, false, "+233302222333"),
        };

        var result = new List<HealthcareFacility>();
        foreach (var d in defs)
        {
            var existing = await _context.HealthcareFacilities
                .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.FacilityCode == d.Code && !f.IsDeleted);
            if (existing != null) { result.Add(existing); continue; }

            var f = new HealthcareFacility
            {
                TenantId = tenantId,
                FacilityName = d.Name,
                FacilityCode = d.Code,
                FacilityType = d.Type,
                PhysicalAddress = d.Addr,
                City = d.City,
                CountryId = countryId,
                PrimaryPhone = d.Phone,
                Email = $"info@{d.Code.ToLowerInvariant()}.example",
                HasEmergencyServices = d.Emerg,
                Has24HourService = d.Emerg,
                HasLaboratory = true,
                HasPharmacy = true,
                AcceptsNHIS = d.Nhis,
                NHISAccreditationNumber = d.Nhis ? $"NHIA-{d.Code}" : null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            };
            _context.HealthcareFacilities.Add(f);
            result.Add(f);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Facilities ready: {Count}", result.Count);
        return result;
    }

    private async Task SeedFacilityServicesAsync(Guid tenantId, List<HealthcareFacility> facilities)
    {
        var common = new (string Name, MedicalServiceType Type, decimal Cost, bool Appt, bool PreAuth)[]
        {
            ("General Consultation", MedicalServiceType.Consultation, 150m, true, false),
            ("Laboratory Tests", MedicalServiceType.LaboratoryServices, 200m, false, false),
            ("Imaging / X-Ray", MedicalServiceType.ImagingRadiology, 400m, true, true),
            ("Pharmacy Dispensing", MedicalServiceType.Pharmacy, 0m, false, false),
        };

        foreach (var f in facilities)
        {
            foreach (var s in common)
            {
                var exists = await _context.FacilityServices.AnyAsync(x =>
                    x.TenantId == tenantId && x.FacilityId == f.Id && x.Name == s.Name && !x.IsDeleted);
                if (exists) continue;

                _context.FacilityServices.Add(new FacilityService
                {
                    TenantId = tenantId,
                    FacilityId = f.Id,
                    Name = s.Name,
                    ServiceType = s.Type,
                    EstimatedCost = s.Cost,
                    RequiresAppointment = s.Appt,
                    RequiresPreAuthorization = s.PreAuth,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System",
                    IsDeleted = false,
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task<List<Physician>> SeedPhysiciansAsync(Guid tenantId, List<HealthcareFacility> facilities)
    {
        var defs = new (string License, string First, string Last, string Spec, int FacIdx)[]
        {
            ("MDC-PG-1001", "Ama", "Mensah", "General Practice", 0),
            ("MDC-PG-1002", "Kwabena", "Osei", "Cardiology", 1),
            ("MDC-PG-1003", "Efua", "Boateng", "Pediatrics", 1),
            ("MDC-PG-1004", "Yaw", "Asante", "Radiology", 3),
        };

        var result = new List<Physician>();
        foreach (var d in defs)
        {
            var existing = await _context.Physicians
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.MedicalLicenseNumber == d.License && !p.IsDeleted);
            if (existing != null) { result.Add(existing); continue; }

            var p = new Physician
            {
                TenantId = tenantId,
                FirstName = d.First,
                LastName = d.Last,
                Title = "Dr.",
                Specialization = d.Spec,
                MedicalLicenseNumber = d.License,
                PhoneNumber = "+23324" + Random.Shared.Next(1000000, 9999999),
                Email = $"{d.First.ToLowerInvariant()}.{d.Last.ToLowerInvariant()}@clinic.example",
                FacilityId = facilities.Count > d.FacIdx ? facilities[d.FacIdx].Id : (Guid?)null,
                IsVerified = true,
                VerificationDate = DateTime.UtcNow.AddMonths(-3),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            };
            _context.Physicians.Add(p);
            result.Add(p);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Physicians ready: {Count}", result.Count);
        return result;
    }

    // ── Providers / plans / network / docs / premiums ──────────────────────────────

    private async Task<List<MedicalInsuranceProvider>> SeedProvidersAsync(Guid tenantId, Guid? countryId)
    {
        var defs = new (string Code, string Name, MedicalInsuranceProviderType Type, string License)[]
        {
            ("INS-NMI", "Nationwide Medical Insurance", MedicalInsuranceProviderType.HMO, "NIC-HMO-2201"),
            ("INS-PHI", "Premier Health Insurance", MedicalInsuranceProviderType.PrivateHealthInsurance, "NIC-PHI-3307"),
        };

        var result = new List<MedicalInsuranceProvider>();
        foreach (var d in defs)
        {
            var existing = await _context.MedicalInsuranceProviders
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == d.Code && !p.IsDeleted);
            if (existing != null) { result.Add(existing); continue; }

            var p = new MedicalInsuranceProvider
            {
                TenantId = tenantId,
                Name = d.Name,
                Code = d.Code,
                ProviderType = d.Type,
                LicenseNumber = d.License,
                Address = "High Street, Accra Central",
                City = "Accra",
                CountryId = countryId,
                PrimaryPhone = "+233302900000",
                ClaimsHotline = "+233302900111",
                Email = $"claims@{d.Code.ToLowerInvariant()}.example",
                ClaimsEmail = $"claims@{d.Code.ToLowerInvariant()}.example",
                HasOnlinePortal = true,
                ClaimsPortalUrl = $"https://portal.{d.Code.ToLowerInvariant()}.example",
                StandardProcessingDays = 14,
                EmergencyProcessingDays = 3,
                ClaimSubmissionDeadlineDays = 90,
                PreferredPaymentMethod = PaymentMethod.BankTransfer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            };
            _context.MedicalInsuranceProviders.Add(p);
            result.Add(p);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Providers ready: {Count}", result.Count);
        return result;
    }

    private async Task<List<MedicalInsurancePlan>> SeedPlansAsync(Guid tenantId, List<MedicalInsuranceProvider> providers)
    {
        var defs = new (string Code, string Name, MedicalInsurancePlanType Type, decimal Annual, decimal Monthly)[]
        {
            ("STD", "Standard Cover", MedicalInsurancePlanType.StandardPlan, 20000m, 120m),
            ("PREM", "Premium Cover", MedicalInsurancePlanType.PremiumPlan, 50000m, 300m),
        };

        var result = new List<MedicalInsurancePlan>();
        foreach (var prov in providers)
        {
            foreach (var d in defs)
            {
                var existing = await _context.MedicalInsurancePlans
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.MedicalInsuranceProviderId == prov.Id && p.Code == d.Code && !p.IsDeleted);
                if (existing != null) { result.Add(existing); continue; }

                var p = new MedicalInsurancePlan
                {
                    TenantId = tenantId,
                    MedicalInsuranceProviderId = prov.Id,
                    Name = d.Name,
                    Code = d.Code,
                    PlanType = d.Type,
                    Description = $"{d.Name} from {prov.Name}",
                    AnnualLimit = d.Annual,
                    OutpatientLimit = d.Annual * 0.4m,
                    InpatientLimit = d.Annual * 0.6m,
                    DentalLimit = d.Annual * 0.1m,
                    OpticalLimit = d.Annual * 0.1m,
                    MaternityLimit = d.Annual * 0.2m,
                    CoversDependents = true,
                    MaxDependents = 4,
                    MaxChildAge = 21,
                    MonthlyPremium = d.Monthly,
                    AnnualPremium = d.Monthly * 12m,
                    EmployerContributionPercent = 80m,
                    EmployeeContributionPercent = 20m,
                    EffectiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System",
                    IsDeleted = false,
                };
                _context.MedicalInsurancePlans.Add(p);
                result.Add(p);
            }
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Plans ready: {Count}", result.Count);
        return result;
    }

    private async Task SeedNetworkAsync(Guid tenantId, List<MedicalInsuranceProvider> providers, List<HealthcareFacility> facilities)
    {
        foreach (var prov in providers)
        {
            // Each provider networks the first 3 facilities; first is preferred.
            for (var i = 0; i < Math.Min(3, facilities.Count); i++)
            {
                var fac = facilities[i];
                var exists = await _context.MedicalInsuranceProviderFacilities.AnyAsync(x =>
                    x.TenantId == tenantId && x.ProviderId == prov.Id && x.FacilityId == fac.Id && !x.IsDeleted);
                if (exists) continue;

                _context.MedicalInsuranceProviderFacilities.Add(new MedicalInsuranceProviderFacility
                {
                    TenantId = tenantId,
                    ProviderId = prov.Id,
                    FacilityId = fac.Id,
                    EffectiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsPreferredProvider = i == 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System",
                    IsDeleted = false,
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedProviderDocumentsAsync(Guid tenantId, List<MedicalInsuranceProvider> providers)
    {
        foreach (var prov in providers)
        {
            var fileName = $"{prov.Code}-operating-license.pdf";
            var exists = await _context.MedicalInsuranceProviderDocuments.AnyAsync(x =>
                x.TenantId == tenantId && x.ProviderId == prov.Id && x.FileName == fileName && !x.IsDeleted);
            if (exists) continue;

            _context.MedicalInsuranceProviderDocuments.Add(new MedicalInsuranceProviderDocument
            {
                TenantId = tenantId,
                ProviderId = prov.Id,
                FileName = fileName,
                FilePath = $"/documents/insurance/{fileName}",
                DocumentType = MedicalInsuranceProviderDocumentType.OperatingLicense,
                Description = "NIC operating license",
                UploadDate = DateTime.UtcNow.AddMonths(-6),
                ExpiryDate = DateTime.UtcNow.AddMonths(18),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedPremiumsAsync(Guid tenantId, List<MedicalInsuranceProvider> providers, List<MedicalInsurancePlan> plans)
    {
        var periodStart = new DateOnly(2026, 1, 1);
        var periodEnd = new DateOnly(2026, 3, 31);

        foreach (var prov in providers)
        {
            var plan = plans.FirstOrDefault(p => p.MedicalInsuranceProviderId == prov.Id);
            if (plan == null) continue;

            var exists = await _context.MedicalInsurancePremiumRecords.AnyAsync(x =>
                x.TenantId == tenantId && x.ProviderId == prov.Id && x.PlanId == plan.Id && x.BillingPeriodStart == periodStart && !x.IsDeleted);
            if (exists) continue;

            _context.MedicalInsurancePremiumRecords.Add(new MedicalInsurancePremiumRecord
            {
                TenantId = tenantId,
                ProviderId = prov.Id,
                PlanId = plan.Id,
                BillingPeriodStart = periodStart,
                BillingPeriodEnd = periodEnd,
                TotalPremiumAmount = 4320m,
                EmployerContribution = 3456m,
                EmployeeContribution = 864m,
                CoveredLivesCount = 6,
                Status = prov.Code == "INS-NMI" ? MedicalInsurancePremiumPaymentStatus.Paid : MedicalInsurancePremiumPaymentStatus.Pending,
                DueDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                PaymentDate = prov.Code == "INS-NMI" ? new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) : null,
                PaymentReference = prov.Code == "INS-NMI" ? "PMT-Q1-NMI" : null,
                PaymentMethod = prov.Code == "INS-NMI" ? PaymentMethod.BankTransfer : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });
        }
        await _context.SaveChangesAsync();
    }

    // ── Benefit scheme & tiers ─────────────────────────────────────────────────────

    private async Task<(MedicalBenefitScheme Scheme, List<MedicalBenefitTier> Tiers)> SeedSchemeAndTiersAsync(Guid tenantId, Func<string, Guid?> staffLevel)
    {
        var scheme = await _context.MedicalBenefitSchemes
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Code == "CHS-2026" && !s.IsDeleted);
        if (scheme == null)
        {
            scheme = new MedicalBenefitScheme
            {
                TenantId = tenantId,
                Name = "Corporate Health Scheme 2026",
                Code = "CHS-2026",
                Description = "Company-wide medical benefit scheme with staff-level tiers.",
                EffectiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            };
            _context.MedicalBenefitSchemes.Add(scheme);
            await _context.SaveChangesAsync();
        }

        var tierDefs = new (string Name, string? LevelCode, decimal Annual)[]
        {
            ("Junior Tier", "L1", 20000m),
            ("Senior Tier", "L3", 35000m),
            ("Management Tier", "M1", 60000m),
        };

        var tiers = new List<MedicalBenefitTier>();
        foreach (var t in tierDefs)
        {
            var existing = await _context.MedicalBenefitTiers
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SchemeId == scheme.Id && x.TierName == t.Name && !x.IsDeleted);
            if (existing != null) { tiers.Add(existing); continue; }

            var tier = new MedicalBenefitTier
            {
                TenantId = tenantId,
                SchemeId = scheme.Id,
                TierName = t.Name,
                StaffLevelId = t.LevelCode != null ? staffLevel(t.LevelCode) : null,
                TierDescription = $"Coverage tier for {t.Name}",
                AnnualLimit = t.Annual,
                InpatientLimit = t.Annual * 0.6m,
                OutpatientLimit = t.Annual * 0.4m,
                DentalLimit = t.Annual * 0.1m,
                OpticalLimit = t.Annual * 0.1m,
                MaternityLimit = t.Annual * 0.2m,
                CoversDependents = true,
                MaxDependents = 4,
                DependentAnnualLimit = t.Annual * 0.5m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            };
            _context.MedicalBenefitTiers.Add(tier);
            tiers.Add(tier);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Benefit scheme + {Count} tiers ready", tiers.Count);
        return (scheme, tiers);
    }

    // ── Policies & dependents ──────────────────────────────────────────────────────

    private async Task<List<EmployeeMedicalInsurancePolicy>> SeedPoliciesAsync(
        Guid tenantId, List<Employee> employees, List<MedicalInsuranceProvider> providers,
        List<MedicalInsurancePlan> plans, List<MedicalBenefitTier> tiers)
    {
        var result = new List<EmployeeMedicalInsurancePolicy>();
        var enrollCount = Math.Min(6, employees.Count);

        for (var i = 0; i < enrollCount; i++)
        {
            var emp = employees[i];
            var provider = providers[i % providers.Count];
            var plan = plans.First(p => p.MedicalInsuranceProviderId == provider.Id);
            var tier = tiers.Count > 0 ? tiers[i % tiers.Count] : null;
            var policyNumber = $"POL-{(string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? (i + 1).ToString("D4") : emp.EmployeeNumber)}";

            var existing = await _context.EmployeeMedicalInsurancePolicies
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PolicyNumber == policyNumber && !p.IsDeleted);
            if (existing != null) { result.Add(existing); continue; }

            var annualLimit = tier?.AnnualLimit ?? plan.AnnualLimit;
            var policy = new EmployeeMedicalInsurancePolicy
            {
                TenantId = tenantId,
                EmployeeId = emp.Id,
                ProviderId = provider.Id,
                PlanId = plan.Id,
                BenefitTierId = tier?.Id,
                PolicyNumber = policyNumber,
                MembershipNumber = $"MEM-{provider.Code}-{(i + 1):D3}",
                StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AnnualLimit = annualLimit,
                UtilizedAmount = i % 3 == 0 ? Math.Round(annualLimit * 0.25m, 2) : 0m, // some usage for the bars
                CoversDependents = true,
                Status = MedicalInsurancePolicyStatus.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            };
            _context.EmployeeMedicalInsurancePolicies.Add(policy);
            result.Add(policy);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Policies ready: {Count}", result.Count);
        return result;
    }

    private async Task SeedPolicyDependentsAsync(Guid tenantId, List<EmployeeMedicalInsurancePolicy> policies, List<EmployeeDependent> dependents)
    {
        foreach (var dep in dependents)
        {
            var policy = policies.FirstOrDefault(p => p.EmployeeId == dep.EmployeeId);
            if (policy == null) continue;

            var exists = await _context.MedicalInsurancePolicyDependents.AnyAsync(x =>
                x.TenantId == tenantId && x.PolicyId == policy.Id && x.DependentId == dep.Id && !x.IsDeleted);
            if (exists) continue;

            _context.MedicalInsurancePolicyDependents.Add(new MedicalInsurancePolicyDependent
            {
                TenantId = tenantId,
                PolicyId = policy.Id,
                DependentId = dep.Id,
                MembershipNumber = $"{policy.MembershipNumber}-D",
                CoverageStartDate = policy.StartDate,
                AnnualLimit = Math.Round(policy.AnnualLimit * 0.5m, 2),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });
        }
        await _context.SaveChangesAsync();
    }

    // ── Health profiles ────────────────────────────────────────────────────────────

    private async Task<List<EmployeeHealthProfile>> SeedHealthProfilesAsync(
        Guid tenantId, List<Employee> employees, List<HealthcareFacility> facilities, List<Physician> physicians)
    {
        var bloodGroups = new[] { BloodGroup.OPositive, BloodGroup.APositive, BloodGroup.BPositive, BloodGroup.ABPositive };
        var result = new List<EmployeeHealthProfile>();
        var count = Math.Min(4, employees.Count);

        for (var i = 0; i < count; i++)
        {
            var emp = employees[i];
            var existing = await _context.EmployeeHealthProfiles
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.EmployeeId == emp.Id && !p.IsDeleted);
            if (existing != null) { result.Add(existing); continue; }

            var profile = new EmployeeHealthProfile
            {
                TenantId = tenantId,
                EmployeeId = emp.Id,
                BloodGroup = bloodGroups[i % bloodGroups.Length],
                HeightCm = 165 + i * 3,
                WeightKg = 68 + i * 4,
                DisabilityStatus = DisabilityStatus.None,
                EmergencyContactName = "Next of Kin",
                EmergencyContactPhone = "+23320" + Random.Shared.Next(1000000, 9999999),
                EmergencyContactRelationship = i % 2 == 0 ? "Spouse" : "Sibling",
                PreferredFacilityId = facilities.Count > 0 ? facilities[i % facilities.Count].Id : null,
                PreferredPhysicianId = physicians.Count > 0 ? physicians[i % physicians.Count].Id : null,
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            };
            _context.EmployeeHealthProfiles.Add(profile);
            result.Add(profile);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Health profiles ready: {Count}", result.Count);
        return result;
    }

    private async Task SeedConditionsAllergiesExamsAsync(
        Guid tenantId, List<EmployeeHealthProfile> profiles, List<HealthcareFacility> facilities, List<Physician> physicians)
    {
        // Give the first two profiles a condition + allergy; all profiles a recent exam.
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];

            if (i < 2)
            {
                var condName = i == 0 ? "Hypertension" : "Type 2 Diabetes";
                if (!await _context.EmployeeHealthConditions.AnyAsync(c => c.TenantId == tenantId && c.HealthProfileId == profile.Id && c.ConditionName == condName && !c.IsDeleted))
                {
                    _context.EmployeeHealthConditions.Add(new EmployeeHealthCondition
                    {
                        TenantId = tenantId,
                        HealthProfileId = profile.Id,
                        ConditionName = condName,
                        ICDCode = i == 0 ? "I10" : "E11",
                        Severity = HealthConditionSeverity.Moderate,
                        Status = HealthConditionStatus.Managed,
                        DiagnosedDate = DateOnly.FromDateTime(DateTime.Today.AddYears(-2)),
                        TreatmentSummary = "Managed with medication and lifestyle changes.",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System",
                        IsDeleted = false,
                    });
                }

                var allergen = i == 0 ? "Penicillin" : "Peanuts";
                if (!await _context.EmployeeAllergies.AnyAsync(a => a.TenantId == tenantId && a.HealthProfileId == profile.Id && a.Allergen == allergen && !a.IsDeleted))
                {
                    _context.EmployeeAllergies.Add(new EmployeeAllergy
                    {
                        TenantId = tenantId,
                        HealthProfileId = profile.Id,
                        Allergen = allergen,
                        AllergyType = i == 0 ? AllergyType.Drug : AllergyType.Food,
                        Severity = AllergySeverity.Severe,
                        ReactionDescription = i == 0 ? "Rash and swelling" : "Anaphylaxis risk",
                        ManagementPlan = "Avoid allergen; carry medication.",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System",
                        IsDeleted = false,
                    });
                }
            }

            var examDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-2));
            if (!await _context.EmployeeMedicalExams.AnyAsync(e => e.TenantId == tenantId && e.HealthProfileId == profile.Id && e.ExamDate == examDate && !e.IsDeleted))
            {
                _context.EmployeeMedicalExams.Add(new EmployeeMedicalExam
                {
                    TenantId = tenantId,
                    HealthProfileId = profile.Id,
                    ExamDate = examDate,
                    FacilityId = facilities.Count > 0 ? facilities[i % facilities.Count].Id : null,
                    PhysicianId = physicians.Count > 0 ? physicians[i % physicians.Count].Id : null,
                    HeightCm = profile.HeightCm,
                    WeightKg = profile.WeightKg,
                    BloodPressure = "120/80",
                    Result = i % 4 == 2 ? MedicalExamResult.FitWithRestrictions : MedicalExamResult.Fit,
                    Findings = "Routine annual medical — no acute concerns.",
                    Recommendations = "Maintain healthy lifestyle.",
                    NextExamDueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(20)), // shows in "Exams Due"
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System",
                    IsDeleted = false,
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    // ── Expense claims ─────────────────────────────────────────────────────────────

    private async Task SeedClaimsAsync(
        Guid tenantId, List<Employee> employees, List<HealthcareFacility> facilities,
        List<Physician> physicians, List<EmployeeMedicalInsurancePolicy> policies)
    {
        if (facilities.Count == 0 || policies.Count == 0) return;
        var author = employees[0];

        var defs = new (string Num, int PolIdx, MedicalExpenseType Type, string Desc, decimal Total, decimal Requested, ClaimStatus Status, bool Flagged)[]
        {
            ("MC-SEED-01", 0, MedicalExpenseType.Consultation, "Outpatient consultation and tests", 450m, 450m, ClaimStatus.Pending, false),
            ("MC-SEED-02", 1, MedicalExpenseType.Medication, "Prescription medication refill", 220m, 220m, ClaimStatus.Approved, false),
            ("MC-SEED-03", 2, MedicalExpenseType.Hospitalization, "Two-night inpatient admission", 3800m, 3800m, ClaimStatus.Paid, false),
            ("MC-SEED-04", 0, MedicalExpenseType.DentalCare, "Dental scaling and filling", 600m, 600m, ClaimStatus.Pending, true),
            ("MC-SEED-05", 1, MedicalExpenseType.Imaging, "MRI scan — lower back", 1500m, 1500m, ClaimStatus.Rejected, false),
        };

        foreach (var d in defs)
        {
            if (await _context.MedicalExpenseClaims.AnyAsync(c => c.TenantId == tenantId && c.ClaimNumber == d.Num && !c.IsDeleted))
                continue;

            var policy = policies[d.PolIdx % policies.Count];
            var approved = d.Status is ClaimStatus.Approved or ClaimStatus.Paid ? d.Requested : (decimal?)null;

            var claim = new MedicalExpenseClaim
            {
                TenantId = tenantId,
                ClaimNumber = d.Num,
                EmployeeId = policy.EmployeeId,
                ClaimDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(5, 40)),
                ServiceDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(41, 80)),
                ExpenseType = d.Type,
                Description = d.Desc,
                FacilityId = facilities[d.PolIdx % facilities.Count].Id,
                PhysicianId = physicians.Count > 0 ? physicians[d.PolIdx % physicians.Count].Id : null,
                Diagnosis = "See attached medical report",
                TotalAmount = d.Total,
                AmountRequested = d.Requested,
                InsurancePolicyId = policy.Id,
                Status = d.Status,
                AmountApproved = approved,
                PaymentProcessed = d.Status == ClaimStatus.Paid,
                PaymentDate = d.Status == ClaimStatus.Paid ? DateTime.UtcNow.AddDays(-2) : null,
                PaymentReference = d.Status == ClaimStatus.Paid ? $"PAY-{d.Num}" : null,
                PaymentMethod = d.Status == ClaimStatus.Paid ? PaymentMethod.BankTransfer : null,
                IsFlaggedForReview = d.Flagged,
                FlagReason = d.Flagged ? "Amount above typical range for service type — verify receipts." : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            };
            _context.MedicalExpenseClaims.Add(claim);
            await _context.SaveChangesAsync();

            // A couple of line items.
            _context.MedicalExpenseItems.Add(new MedicalExpenseItem
            {
                TenantId = tenantId,
                ClaimId = claim.Id,
                Description = d.Desc,
                ItemType = MedicalItemType.ProfessionalFee,
                Quantity = 1,
                UnitCost = d.Total,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });

            // An internal note.
            _context.MedicalExpenseClaimNotes.Add(new MedicalExpenseClaimNote
            {
                TenantId = tenantId,
                ClaimId = claim.Id,
                AuthorId = author.Id,
                NoteType = MedicalExpenseClaimNoteType.InternalHR,
                Content = $"Seeded claim in {d.Status} status for demo purposes.",
                IsInternal = true,
                NoteDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });

            await _context.SaveChangesAsync();
        }
        _logger.LogInformation("Expense claims seeded.");
    }

    // ── Clinical (pre-auth, referral, appointment) ─────────────────────────────────

    private async Task SeedClinicalAsync(
        Guid tenantId, List<Employee> employees, List<HealthcareFacility> facilities,
        List<Physician> physicians, List<EmployeeMedicalInsurancePolicy> policies)
    {
        if (policies.Count == 0 || facilities.Count == 0) return;
        var fac = facilities[0];
        var phys = physicians.Count > 0 ? physicians[0].Id : (Guid?)null;

        // Pre-authorizations
        var preAuthDefs = new (string Num, int PolIdx, ClaimPreAuthorizationStatus Status, decimal Cost)[]
        {
            ("PA-SEED-01", 0, ClaimPreAuthorizationStatus.PendingApproval, 5000m),
            ("PA-SEED-02", 1, ClaimPreAuthorizationStatus.Approved, 12000m),
        };
        foreach (var d in preAuthDefs)
        {
            if (await _context.MedicalClaimPreAuthorizations.AnyAsync(x => x.TenantId == tenantId && x.AuthorizationNumber == d.Num && !x.IsDeleted)) continue;
            var policy = policies[d.PolIdx % policies.Count];
            _context.MedicalClaimPreAuthorizations.Add(new MedicalClaimPreAuthorization
            {
                TenantId = tenantId,
                AuthorizationNumber = d.Num,
                EmployeeId = policy.EmployeeId,
                PolicyId = policy.Id,
                FacilityId = fac.Id,
                PhysicianId = phys,
                ServiceType = MedicalServiceType.Surgery,
                Diagnosis = "Planned elective procedure",
                ProposedTreatment = "Surgical intervention with short admission",
                PlannedServiceDate = DateTime.UtcNow.AddDays(21),
                EstimatedCost = d.Cost,
                RequestDate = DateTime.UtcNow.AddDays(-5),
                Status = d.Status,
                AuthorizedAmount = d.Status == ClaimPreAuthorizationStatus.Approved ? d.Cost : null,
                AuthorizationDate = d.Status == ClaimPreAuthorizationStatus.Approved ? DateTime.UtcNow.AddDays(-2) : null,
                ExpiryDate = d.Status == ClaimPreAuthorizationStatus.Approved ? DateTime.UtcNow.AddDays(60) : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });
        }

        // Referrals
        var refDefs = new (string Num, int EmpIdx, MedicalReferralStatus Status, MedicalReferralPriority Priority)[]
        {
            ("RF-SEED-01", 0, MedicalReferralStatus.Pending, MedicalReferralPriority.Routine),
            ("RF-SEED-02", 1, MedicalReferralStatus.Issued, MedicalReferralPriority.Urgent),
        };
        foreach (var d in refDefs)
        {
            if (await _context.MedicalReferrals.AnyAsync(x => x.TenantId == tenantId && x.ReferralNumber == d.Num && !x.IsDeleted)) continue;
            var emp = employees[d.EmpIdx % employees.Count];
            _context.MedicalReferrals.Add(new MedicalReferral
            {
                TenantId = tenantId,
                ReferralNumber = d.Num,
                EmployeeId = emp.Id,
                ReferringFacilityId = fac.Id,
                ReferringPhysicianId = phys,
                ReferredToFacilityId = facilities.Count > 1 ? facilities[1].Id : null,
                ReferralDate = DateTime.UtcNow.AddDays(-3),
                Priority = d.Priority,
                Diagnosis = "Requires specialist review",
                ReasonForReferral = "Specialist consultation for ongoing symptoms.",
                Status = d.Status,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });
        }

        // Appointments
        var apptDefs = new (string Num, int EmpIdx, int DaysAhead, MedicalAppointmentStatus Status)[]
        {
            ("AP-SEED-01", 0, 7, MedicalAppointmentStatus.Confirmed),
            ("AP-SEED-02", 1, 14, MedicalAppointmentStatus.Scheduled),
            ("AP-SEED-03", 2, -10, MedicalAppointmentStatus.Completed),
        };
        foreach (var d in apptDefs)
        {
            if (await _context.MedicalAppointments.AnyAsync(x => x.TenantId == tenantId && x.AppointmentNumber == d.Num && !x.IsDeleted)) continue;
            var emp = employees[d.EmpIdx % employees.Count];
            _context.MedicalAppointments.Add(new MedicalAppointment
            {
                TenantId = tenantId,
                AppointmentNumber = d.Num,
                EmployeeId = emp.Id,
                FacilityId = fac.Id,
                PhysicianId = phys,
                AppointmentDateTime = DateTime.UtcNow.AddDays(d.DaysAhead).Date.AddHours(9),
                DurationMinutes = 30,
                ServiceType = MedicalServiceType.Consultation,
                Purpose = "Routine consultation",
                Status = d.Status,
                CheckInTime = d.Status == MedicalAppointmentStatus.Completed ? DateTime.UtcNow.AddDays(d.DaysAhead).Date.AddHours(9) : null,
                CheckOutTime = d.Status == MedicalAppointmentStatus.Completed ? DateTime.UtcNow.AddDays(d.DaysAhead).Date.AddHours(9).AddMinutes(40) : null,
                OutcomeSummary = d.Status == MedicalAppointmentStatus.Completed ? "Consultation completed; follow-up in 3 months." : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Clinical (pre-auth/referral/appointment) seeded.");
    }

    // ── NHIS claims ────────────────────────────────────────────────────────────────

    private async Task SeedNhisAsync(Guid tenantId, List<Employee> employees, List<HealthcareFacility> facilities)
    {
        var nhisFac = facilities.FirstOrDefault(f => f.AcceptsNHIS) ?? facilities.FirstOrDefault();
        if (nhisFac == null) return;

        var defs = new (string Num, int EmpIdx, NHISClaimStatus Status, decimal Total)[]
        {
            ("NH-SEED-01", 0, NHISClaimStatus.Submitted, 320m),
            ("NH-SEED-02", 1, NHISClaimStatus.Approved, 540m),
        };
        foreach (var d in defs)
        {
            if (await _context.NHISClaims.AnyAsync(x => x.TenantId == tenantId && x.ClaimNumber == d.Num && !x.IsDeleted)) continue;
            var emp = employees[d.EmpIdx % employees.Count];
            _context.NHISClaims.Add(new NHISClaim
            {
                TenantId = tenantId,
                ClaimNumber = d.Num,
                EmployeeId = emp.Id,
                NHISMembershipNumber = $"NHIS-{(d.EmpIdx + 1):D6}",
                FacilityId = nhisFac.Id,
                ServiceDate = DateTime.UtcNow.AddDays(-15),
                ServiceType = MedicalServiceType.OutpatientCare,
                ServiceDescription = "Outpatient visit covered under NHIS",
                Diagnosis = "Malaria",
                ICDCode = "B54",
                TotalCost = d.Total,
                NHISCoveredAmount = Math.Round(d.Total * 0.8m, 2),
                CoPayAmount = Math.Round(d.Total * 0.2m, 2),
                SubmissionDate = DateTime.UtcNow.AddDays(-10),
                Status = d.Status,
                ApprovalDate = d.Status == NHISClaimStatus.Approved ? DateTime.UtcNow.AddDays(-3) : null,
                ApprovedAmount = d.Status == NHISClaimStatus.Approved ? Math.Round(d.Total * 0.8m, 2) : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false,
            });
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("NHIS claims seeded.");
    }

    private async Task<Guid> ResolveDefaultTenantIdAsync()
    {
        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Code == "DEFAULT" && !t.IsDeleted);
        if (tenant != null) return tenant.Id;
        var exists = await _context.Tenants.AsNoTracking().AnyAsync(t => t.Id == DefaultTenantIdFallback && !t.IsDeleted);
        return exists ? DefaultTenantIdFallback : Guid.Empty;
    }
}
