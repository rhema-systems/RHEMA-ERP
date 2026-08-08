using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds sample Skills for an existing tenant (typically DEFAULT).
/// Safe to run multiple times (idempotent by skill name).
/// </summary>
public sealed class SkillDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SkillDataSeeder> _logger;

    public SkillDataSeeder(ApplicationDbContext context, ILogger<SkillDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedForDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT", cancellationToken);
        if (defaultTenant == null)
        {
            _logger.LogError("Default tenant not found. Cannot seed skills.");
            return 0;
        }

        return await SeedForTenantAsync(defaultTenant.Id, cancellationToken);
    }

    public async Task<int> SeedForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding sample skills for tenant {TenantId}...", tenantId);

        var skills = new[]
        {
            // Maintenance / Technical
            new { Name = "HVAC Maintenance", Category = "Technical", Description = "Heating, ventilation, and air conditioning systems", RequiresCertification = false },
            new { Name = "Electrical Systems", Category = "Technical", Description = "Electrical wiring and systems maintenance", RequiresCertification = true },
            new { Name = "Plumbing", Category = "Technical", Description = "Plumbing systems and fixtures", RequiresCertification = false },
            new { Name = "Mechanical Repair", Category = "Technical", Description = "General mechanical repairs", RequiresCertification = false },
            new { Name = "Preventive Maintenance", Category = "Technical", Description = "Scheduled preventive maintenance procedures", RequiresCertification = false },
            new { Name = "Equipment Diagnostics", Category = "Technical", Description = "Diagnosing equipment problems", RequiresCertification = false },
            new { Name = "Welding", Category = "Technical", Description = "Welding and metal fabrication", RequiresCertification = true },
            new { Name = "Forklift Operation", Category = "Safety", Description = "Operate forklifts and material-handling equipment", RequiresCertification = true },

            // Safety / Compliance
            new { Name = "First Aid", Category = "Safety", Description = "Basic first aid response and incident reporting", RequiresCertification = true },
            new { Name = "Fire Safety", Category = "Safety", Description = "Fire prevention, extinguisher use, and evacuation protocols", RequiresCertification = false },
            new { Name = "Safety Compliance", Category = "Safety", Description = "Safety protocols, audits, and compliance checks", RequiresCertification = false },
            new { Name = "Risk Assessment", Category = "Safety", Description = "Identify hazards and perform workplace risk assessments", RequiresCertification = false },

            // Leadership / Soft skills
            new { Name = "Communication", Category = "Soft Skills", Description = "Clear written and verbal communication", RequiresCertification = false },
            new { Name = "Conflict Resolution", Category = "Soft Skills", Description = "Resolve workplace conflicts and mediate disputes", RequiresCertification = false },
            new { Name = "Time Management", Category = "Soft Skills", Description = "Prioritize tasks and manage deadlines", RequiresCertification = false },
            new { Name = "Team Leadership", Category = "Leadership", Description = "Lead teams, delegate work, and coach performance", RequiresCertification = false },
            new { Name = "Project Management", Category = "Leadership", Description = "Plan, execute, and track projects end-to-end", RequiresCertification = false },
            new { Name = "PMP (Project Management Professional)", Category = "Certification", Description = "Project management certification (PMP)", RequiresCertification = true },

            // HR / Finance
            new { Name = "Recruitment & Interviewing", Category = "HR", Description = "Candidate sourcing, screening, and structured interviews", RequiresCertification = false },
            new { Name = "Payroll Processing", Category = "HR", Description = "Payroll preparation, deductions, and payslip processing", RequiresCertification = false },
            new { Name = "Labor Law Compliance", Category = "Compliance", Description = "Employment law basics and HR compliance practices", RequiresCertification = false },
            new { Name = "Budgeting", Category = "Finance", Description = "Build and track department budgets", RequiresCertification = false },

            // IT / Data
            new { Name = "Cybersecurity Awareness", Category = "IT", Description = "Phishing prevention and secure data handling", RequiresCertification = false },
            new { Name = "Network Troubleshooting", Category = "IT", Description = "Diagnose and resolve basic network connectivity issues", RequiresCertification = false },
            new { Name = "SQL Basics", Category = "IT", Description = "Querying data with SQL (SELECT, JOIN, WHERE)", RequiresCertification = false },
            new { Name = "C# Development", Category = "IT", Description = "Build and maintain .NET applications", RequiresCertification = false }
        };

        var created = 0;

        foreach (var s in skills)
        {
            var exists = await _context.Skills.AnyAsync(x => x.TenantId == tenantId && x.Name == s.Name && !x.IsDeleted, cancellationToken);
            if (exists) continue;

            _context.Skills.Add(new Skill
            {
                TenantId = tenantId,
                Name = s.Name,
                Category = s.Category,
                Description = s.Description,
                IsActive = true,
                RequiresCertification = s.RequiresCertification,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            });

            created++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Skill seeding completed. Created {Created} new skills.", created);

        return created;
    }
}
