using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds sample Employee Positions for an existing tenant (typically DEFAULT).
/// Safe to run multiple times (idempotent by position code, plus by join keys).
/// </summary>
public sealed class EmployeePositionDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmployeePositionDataSeeder> _logger;

    public EmployeePositionDataSeeder(ApplicationDbContext context, ILogger<EmployeePositionDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(int positions, int requirements, int benefits)> SeedForDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT", cancellationToken);
        if (defaultTenant == null)
        {
            _logger.LogError("Default tenant not found. Cannot seed positions.");
            return (0, 0, 0);
        }

        return await SeedForTenantAsync(defaultTenant.Id, cancellationToken);
    }

    public async Task<(int positions, int requirements, int benefits)> SeedForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding demo employee positions for tenant {TenantId}...", tenantId);

        // Lookups by name (based on your DB sample data)
        var orgUnitNames = new[]
        {
            "Executive Office",
            "Board of Directors",
            "Finance Division",
            "Budget & Planning Department",
            "Human Resources Division",
            "Employee Relations Department",
            "Training & Development Department",
            "Enterprise Systems Section",
            "Backend Development Unit",
            "Frontend Development Unit",
            "QA & Testing Unit",
            "Procurement Department",
            "Logistics Department",
            "Vendor Payments Unit",
            "Expense Reconciliation Unit"
        };

        var staffLevelNames = new[]
        {
            "C-Level / Executive",
            "Vice President",
            "Director",
            "Senior Manager",
            "Manager",
            "Senior",
            "Mid-Level / Associate",
            "Junior / Entry Level",
            "Intern"
        };

        var skillNames = new[]
        {
            "Communication",
            "Conflict Resolution",
            "Recruitment & Interviewing",
            "Labor Law Compliance",
            "Payroll Processing",
            "Budgeting",
            "Risk Assessment",
            "Project Management",
            "PMP (Project Management Professional)",
            "Cybersecurity Awareness",
            "Network Troubleshooting",
            "C# Development",
            "HVAC Maintenance",
            "Equipment Diagnostics",
            "Electrical Systems",
            "Mechanical Repair",
            "Preventive Maintenance",
            "Fire Safety",
            "First Aid",
            "Forklift Operation"
        };

        var policyNames = new[]
        {
            "Housing Allowance",
            "Transport Allowance",
            "Technology Stipend",
            "Medical Cover (Standard)",
            "Life Insurance (Basic)",
            "Education Support (Dependents)"
        };

        var orgUnits = await _context.Set<OrganizationUnit>()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && orgUnitNames.Contains(x.Name))
            .ToListAsync(cancellationToken);

        var staffLevels = await _context.Set<StaffLevel>()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && staffLevelNames.Contains(x.Name))
            .ToListAsync(cancellationToken);

        var skills = await _context.Set<Skill>()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && skillNames.Contains(x.Name))
            .ToListAsync(cancellationToken);

        var policies = await _context.Set<BenefitPolicy>()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && policyNames.Contains(x.PolicyName))
            .ToListAsync(cancellationToken);

        var orgUnitByName = orgUnits.ToDictionary(x => x.Name, x => x);
        var staffLevelByName = staffLevels.ToDictionary(x => x.Name, x => x);
        var skillByName = skills.ToDictionary(x => x.Name, x => x);
        var policyByName = policies.ToDictionary(x => x.PolicyName, x => x);

        // Helper to resolve lookups and log friendly missing data
        OrganizationUnit? GetOrgUnit(string name)
        {
            if (orgUnitByName.TryGetValue(name, out var v)) return v;
            _logger.LogWarning("Missing OrganizationUnit '{OrgUnitName}'. Position that references it will be skipped.", name);
            return null;
        }

        StaffLevel? GetStaffLevel(string name)
        {
            if (staffLevelByName.TryGetValue(name, out var v)) return v;
            _logger.LogWarning("Missing StaffLevel '{StaffLevelName}'. Position that references it will be created without staff level.", name);
            return null;
        }

        Skill? GetSkill(string name)
        {
            if (skillByName.TryGetValue(name, out var v)) return v;
            _logger.LogWarning("Missing Skill '{SkillName}'. Requirement will be skipped.", name);
            return null;
        }

        BenefitPolicy? GetPolicy(string name)
        {
            if (policyByName.TryGetValue(name, out var v)) return v;
            _logger.LogWarning("Missing BenefitPolicy '{PolicyName}'. Benefit will be skipped.", name);
            return null;
        }

        var expiryNextYear = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));

        // Specs (use unique codes for idempotency)
        var specs = new List<PositionSpec>
        {
            new(
                Code: "POS-CEO",
                Title: "Chief Executive Officer (CEO)",
                OrgUnitName: "Executive Office",
                StaffLevelName: "C-Level / Executive",
                ReportsToCode: null,
                Level: 10,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 1,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new()
                {
                    new("Communication", SkillLevel.Expert, true, 5),
                    new("Budgeting", SkillLevel.Advanced, true, 4),
                    new("Project Management", SkillLevel.Advanced, false, 3)
                },
                Benefits: new()
                {
                    new("Housing Allowance", 5000m, null),
                    new("Transport Allowance", 1000m, null),
                    new("Technology Stipend", 800m, null),
                    new("Medical Cover (Standard)", null, null),
                    new("Life Insurance (Basic)", null, null),
                    new("Education Support (Dependents)", null, expiryNextYear)
                }
            ),

            new(
                Code: "POS-BOD-CHAIR",
                Title: "Board Chair",
                OrgUnitName: "Board of Directors",
                StaffLevelName: "C-Level / Executive",
                ReportsToCode: null,
                Level: 10,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 1,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Communication", SkillLevel.Expert, true, 5), new("Risk Assessment", SkillLevel.Advanced, false, 3) },
                Benefits: new() { new("Transport Allowance", 800m, null) }
            ),

            new(
                Code: "POS-VPHR",
                Title: "Vice President, Human Resources",
                OrgUnitName: "Human Resources Division",
                StaffLevelName: "Vice President",
                ReportsToCode: "POS-CEO",
                Level: 8,
                WorkMode: WorkMode.Hybrid,
                ExpectedHeadcount: 1,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new()
                {
                    new("Recruitment & Interviewing", SkillLevel.Advanced, true, 5),
                    new("Conflict Resolution", SkillLevel.Advanced, true, 4),
                    new("Labor Law Compliance", SkillLevel.Advanced, true, 4),
                    new("Communication", SkillLevel.Advanced, true, 3)
                },
                Benefits: new()
                {
                    new("Housing Allowance", 3000m, null),
                    new("Transport Allowance", 800m, null),
                    new("Medical Cover (Standard)", null, null),
                    new("Life Insurance (Basic)", null, null)
                }
            ),

            new(
                Code: "POS-HRMGR",
                Title: "Human Resources Manager",
                OrgUnitName: "Employee Relations Department",
                StaffLevelName: "Manager",
                ReportsToCode: "POS-VPHR",
                Level: 6,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 1,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new()
                {
                    new("Conflict Resolution", SkillLevel.Intermediate, true, 5),
                    new("Recruitment & Interviewing", SkillLevel.Intermediate, true, 4),
                    new("Payroll Processing", SkillLevel.Intermediate, false, 2),
                    new("Labor Law Compliance", SkillLevel.Intermediate, false, 2),
                    new("Communication", SkillLevel.Intermediate, true, 3)
                },
                Benefits: new() { new("Transport Allowance", 400m, null), new("Medical Cover (Standard)", null, null) }
            ),

            new(
                Code: "POS-LND-LEAD",
                Title: "Learning & Development Lead",
                OrgUnitName: "Training & Development Department",
                StaffLevelName: "Senior",
                ReportsToCode: "POS-VPHR",
                Level: 5,
                WorkMode: WorkMode.Hybrid,
                ExpectedHeadcount: 1,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Communication", SkillLevel.Advanced, true, 4), new("Project Management", SkillLevel.Intermediate, false, 2) },
                Benefits: new() { new("Technology Stipend", 300m, null), new("Transport Allowance", 350m, null) }
            ),

            new(
                Code: "POS-FINDIR",
                Title: "Finance Director",
                OrgUnitName: "Finance Division",
                StaffLevelName: "Director",
                ReportsToCode: "POS-CEO",
                Level: 7,
                WorkMode: WorkMode.Hybrid,
                ExpectedHeadcount: 1,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new()
                {
                    new("Budgeting", SkillLevel.Expert, true, 5),
                    new("Risk Assessment", SkillLevel.Advanced, true, 4),
                    new("Communication", SkillLevel.Advanced, true, 3)
                },
                Benefits: new() { new("Transport Allowance", 700m, null), new("Medical Cover (Standard)", null, null), new("Life Insurance (Basic)", null, null) }
            ),

            new(
                Code: "POS-BUD-ANL",
                Title: "Budget & Planning Analyst",
                OrgUnitName: "Budget & Planning Department",
                StaffLevelName: "Mid-Level / Associate",
                ReportsToCode: "POS-FINDIR",
                Level: 4,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 3,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Budgeting", SkillLevel.Advanced, true, 5), new("Communication", SkillLevel.Intermediate, true, 3), new("Risk Assessment", SkillLevel.Intermediate, false, 2) },
                Benefits: new() { new("Transport Allowance", 300m, null), new("Medical Cover (Standard)", null, null) }
            ),

            new(
                Code: "POS-IT-DIR",
                Title: "Head of Enterprise Systems",
                OrgUnitName: "Enterprise Systems Section",
                StaffLevelName: "Director",
                ReportsToCode: "POS-CEO",
                Level: 7,
                WorkMode: WorkMode.Hybrid,
                ExpectedHeadcount: 1,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new()
                {
                    new("Cybersecurity Awareness", SkillLevel.Advanced, true, 4),
                    new("Network Troubleshooting", SkillLevel.Advanced, true, 3),
                    new("Project Management", SkillLevel.Advanced, true, 3),
                    new("Communication", SkillLevel.Advanced, true, 2)
                },
                Benefits: new() { new("Technology Stipend", 600m, null), new("Medical Cover (Standard)", null, null), new("Transport Allowance", 500m, null) }
            ),

            new(
                Code: "POS-BE-SEN",
                Title: "Senior Backend Developer",
                OrgUnitName: "Backend Development Unit",
                StaffLevelName: "Senior",
                ReportsToCode: "POS-IT-DIR",
                Level: 5,
                WorkMode: WorkMode.Hybrid,
                ExpectedHeadcount: 2,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("C# Development", SkillLevel.Expert, true, 5), new("Cybersecurity Awareness", SkillLevel.Intermediate, true, 3), new("Communication", SkillLevel.Intermediate, false, 2) },
                Benefits: new() { new("Technology Stipend", 500m, null), new("Medical Cover (Standard)", null, null) }
            ),

            new(
                Code: "POS-FE-MID",
                Title: "Frontend Developer (Mid-Level)",
                OrgUnitName: "Frontend Development Unit",
                StaffLevelName: "Mid-Level / Associate",
                ReportsToCode: "POS-IT-DIR",
                Level: 4,
                WorkMode: WorkMode.Hybrid,
                ExpectedHeadcount: 2,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Communication", SkillLevel.Intermediate, true, 3), new("Project Management", SkillLevel.Beginner, false, 1) },
                Benefits: new() { new("Technology Stipend", 400m, null), new("Medical Cover (Standard)", null, null) }
            ),

            new(
                Code: "POS-QA-MID",
                Title: "QA Engineer",
                OrgUnitName: "QA & Testing Unit",
                StaffLevelName: "Mid-Level / Associate",
                ReportsToCode: "POS-IT-DIR",
                Level: 4,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 2,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Risk Assessment", SkillLevel.Intermediate, true, 4), new("Communication", SkillLevel.Intermediate, true, 3) },
                Benefits: new() { new("Transport Allowance", 300m, null), new("Medical Cover (Standard)", null, null) }
            ),

            new(
                Code: "POS-PROC",
                Title: "Procurement Officer",
                OrgUnitName: "Procurement Department",
                StaffLevelName: "Mid-Level / Associate",
                ReportsToCode: "POS-FINDIR",
                Level: 4,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 2,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Communication", SkillLevel.Advanced, true, 4), new("Budgeting", SkillLevel.Intermediate, true, 3), new("Risk Assessment", SkillLevel.Intermediate, false, 1) },
                Benefits: new() { new("Transport Allowance", 250m, null) }
            ),

            new(
                Code: "POS-AP",
                Title: "Accounts Payable Officer",
                OrgUnitName: "Vendor Payments Unit",
                StaffLevelName: "Mid-Level / Associate",
                ReportsToCode: "POS-FINDIR",
                Level: 3,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 2,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Budgeting", SkillLevel.Intermediate, true, 3), new("Communication", SkillLevel.Intermediate, true, 2) },
                Benefits: new() { new("Transport Allowance", 220m, null) }
            ),

            new(
                Code: "POS-EXP",
                Title: "Expense Reconciliation Officer",
                OrgUnitName: "Expense Reconciliation Unit",
                StaffLevelName: "Junior / Entry Level",
                ReportsToCode: "POS-AP",
                Level: 2,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 2,
                RequiresCertification: false,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Budgeting", SkillLevel.Beginner, true, 2), new("Communication", SkillLevel.Beginner, true, 2) },
                Benefits: new() { new("Transport Allowance", 180m, null) }
            ),

            new(
                Code: "POS-HVAC",
                Title: "Maintenance Technician (HVAC)",
                OrgUnitName: "Logistics Department",
                StaffLevelName: "Senior",
                ReportsToCode: null,
                Level: 3,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 2,
                RequiresCertification: true,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new()
                {
                    new("HVAC Maintenance", SkillLevel.Advanced, true, 5),
                    new("Equipment Diagnostics", SkillLevel.Advanced, true, 4),
                    new("Preventive Maintenance", SkillLevel.Intermediate, true, 3),
                    new("Electrical Systems", SkillLevel.Intermediate, false, 2),
                    new("Fire Safety", SkillLevel.Intermediate, true, 2),
                    new("First Aid", SkillLevel.Beginner, false, 1)
                },
                Benefits: new() { new("Transport Allowance", 250m, null), new("Medical Cover (Standard)", null, null) }
            ),

            new(
                Code: "POS-FORK",
                Title: "Forklift Operator",
                OrgUnitName: "Logistics Department",
                StaffLevelName: "Junior / Entry Level",
                ReportsToCode: null,
                Level: 2,
                WorkMode: WorkMode.OnSite,
                ExpectedHeadcount: 4,
                RequiresCertification: true,
                RequiresGuarantor: false,
                RequiresLicense: false,
                Skills: new() { new("Forklift Operation", SkillLevel.Intermediate, true, 5), new("Fire Safety", SkillLevel.Beginner, true, 2), new("First Aid", SkillLevel.Beginner, false, 1) },
                Benefits: new() { new("Transport Allowance", 150m, null) }
            )
        };

        var createdPositions = 0;
        var createdRequirements = 0;
        var createdBenefits = 0;

        // Pass 1: create positions (no report-to wiring yet)
        foreach (var spec in specs)
        {
            var exists = await _context.EmployeePositions.AnyAsync(
                p => p.TenantId == tenantId && !p.IsDeleted && p.Code == spec.Code,
                cancellationToken);
            if (exists) continue;

            var orgUnit = GetOrgUnit(spec.OrgUnitName);
            if (orgUnit == null) continue;

            var staffLevel = !string.IsNullOrWhiteSpace(spec.StaffLevelName) ? GetStaffLevel(spec.StaffLevelName) : null;

            var position = new EmployeePosition
            {
                TenantId = tenantId,
                Title = spec.Title,
                Code = spec.Code,
                Description = spec.Description,
                OrganizationUnitId = orgUnit.Id,
                // [HR-MODULE-PORT] OrganizationLevelId is also REQUIRED; take it from the resolved unit.
                OrganizationLevelId = orgUnit.OrganizationLevelId,
                StaffLevelId = staffLevel?.Id,
                Level = spec.Level,
                ExpectedHeadcount = spec.ExpectedHeadcount,
                WorkMode = spec.WorkMode,
                RequiresCertification = spec.RequiresCertification,
                RequiresGuarantor = spec.RequiresGuarantor,
                RequiresLicense = spec.RequiresLicense,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };

            _context.EmployeePositions.Add(position);
            createdPositions++;

            // Add skill requirements (idempotent by position+skill)
            foreach (var reqSpec in spec.Skills)
            {
                var skill = GetSkill(reqSpec.SkillName);
                if (skill == null) continue;

                var already = await _context.Set<PositionSkillRequirement>().AnyAsync(
                    r => r.TenantId == tenantId && !r.IsDeleted && r.PositionId == position.Id && r.SkillId == skill.Id,
                    cancellationToken);

                if (already) continue;

                position.SkillRequirements.Add(new PositionSkillRequirement
                {
                    TenantId = tenantId,
                    Position = position,
                    SkillId = skill.Id,
                    RequiredLevel = reqSpec.Level,
                    IsRequired = reqSpec.IsRequired,
                    Priority = reqSpec.Priority,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Seeder"
                });

                createdRequirements++;
            }

            // Add position benefits (idempotent by position+policy)
            foreach (var benSpec in spec.Benefits)
            {
                var policy = GetPolicy(benSpec.PolicyName);
                if (policy == null) continue;

                var already = await _context.Set<EmployeePositionBenefit>().AnyAsync(
                    b => b.TenantId == tenantId && !b.IsDeleted && b.PositionId == position.Id && b.PolicyId == policy.Id,
                    cancellationToken);

                if (already) continue;

                position.PositionBenefits.Add(new EmployeePositionBenefit
                {
                    TenantId = tenantId,
                    Position = position,
                    PolicyId = policy.Id,
                    PositionAmount = benSpec.Amount,
                    ExpiryDate = benSpec.ExpiryDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Seeder"
                });

                createdBenefits++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Pass 2: wire report-to using code lookups (only for positions that exist)
        var positionsByCode = await _context.EmployeePositions
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Code, p => p, cancellationToken);

        var wired = 0;

        foreach (var spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.ReportsToCode)) continue;

            if (!positionsByCode.TryGetValue(spec.Code, out var position)) continue;
            if (position.ReportsToPositionId.HasValue) continue;

            if (!positionsByCode.TryGetValue(spec.ReportsToCode, out var manager))
            {
                _logger.LogWarning("ReportsTo position code '{ReportsToCode}' not found for position '{PositionCode}'.", spec.ReportsToCode, spec.Code);
                continue;
            }

            position.ReportsToPositionId = manager.Id;
            wired++;
        }

        if (wired > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Position seeding completed. Created positions={Positions}, requirements={Requirements}, benefits={Benefits}, wiredReportsTo={Wired}.",
            createdPositions, createdRequirements, createdBenefits, wired);

        return (createdPositions, createdRequirements, createdBenefits);
    }

    private sealed record PositionSpec(
        string Code,
        string Title,
        string OrgUnitName,
        string StaffLevelName,
        string? ReportsToCode,
        int Level,
        WorkMode WorkMode,
        int ExpectedHeadcount,
        bool RequiresCertification,
        bool RequiresGuarantor,
        bool RequiresLicense,
        List<SkillReqSpec> Skills,
        List<BenefitSpec> Benefits,
        string? Description = null);

    private sealed record SkillReqSpec(string SkillName, SkillLevel Level, bool IsRequired, int Priority);

    private sealed record BenefitSpec(string PolicyName, decimal? Amount, DateOnly? ExpiryDate);
}
