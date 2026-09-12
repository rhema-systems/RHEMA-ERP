using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds sample External Associate data for an existing tenant (typically DEFAULT).
/// Safe to run multiple times (idempotent by email address).
/// </summary>
/// <remarks>
/// ⚠ These numbers were <c>EXT-001</c>..<c>EXT-0nn</c> — three digits — while
/// <c>ExternalAssociateService</c> mints four (<c>EXT-0001</c>). Two formats for one series, so a
/// seeded <c>EXT-001</c> and a minted <c>EXT-0001</c> would sit in the register looking like the same
/// reference on two different people, and the generator's <c>int.TryParse</c> would read the seeded
/// rows as numbers 1..n and mint straight over them in the other format. Normalised to D4 in areas
/// 19-23 slice 8. Nothing had to be migrated: this seeder has never run on DEFAULT, whose seven live
/// associates all carry four-digit numbers from a recruitment e2e fixture.
/// </remarks>
public sealed class ExternalAssociateDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExternalAssociateDataSeeder> _logger;

    public ExternalAssociateDataSeeder(ApplicationDbContext context, ILogger<ExternalAssociateDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedForDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT", cancellationToken);
        if (defaultTenant == null)
        {
            _logger.LogError("Default tenant not found. Cannot seed external associates.");
            return 0;
        }

        return await SeedForTenantAsync(defaultTenant.Id, cancellationToken);
    }

    public async Task<int> SeedForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding sample external associate data for tenant {TenantId}...", tenantId);

        var now = DateTime.UtcNow;
        var totalCreated = 0;

        var associatesData = new[]
        {
            new
            {
                AssociateNumber = "EXT-0001",
                Title = "Dr.",
                FirstName = "Samuel",
                MiddleName = "",
                LastName = "Asante",
                Email = "s.asante@talentbridge.gh",
                PhoneNumber = "+233244100001",
                CompanyName = "TalentBridge Recruitment",
                Role = "Recruitment Consultant",
                HasFixedModule = true,
                ModuleId = (int?)1,
                IsActive = true
            },
            new
            {
                AssociateNumber = "EXT-0002",
                Title = "Mrs.",
                FirstName = "Grace",
                MiddleName = "Ama",
                LastName = "Mensah",
                Email = "g.mensah@legalpros.gh",
                PhoneNumber = "+233244100002",
                CompanyName = "LegalPros Consulting",
                Role = "HR Legal Advisor",
                HasFixedModule = false,
                ModuleId = (int?)null,
                IsActive = true
            },
            new
            {
                AssociateNumber = "EXT-0003",
                Title = "Mr.",
                FirstName = "Kweku",
                MiddleName = "",
                LastName = "Boateng",
                Email = "k.boateng@trainingplus.gh",
                PhoneNumber = "+233244100003",
                CompanyName = "TrainingPlus Academy",
                Role = "Training & Development Specialist",
                HasFixedModule = true,
                ModuleId = (int?)2,
                IsActive = true
            },
            new
            {
                AssociateNumber = "EXT-0004",
                Title = "Ms.",
                FirstName = "Abena",
                MiddleName = "",
                LastName = "Owusu",
                Email = "a.owusu@payrollexpert.com",
                PhoneNumber = "+233244100004",
                CompanyName = "PayrollExpert Services",
                Role = "Payroll Consultant",
                HasFixedModule = true,
                ModuleId = (int?)3,
                IsActive = true
            },
            new
            {
                AssociateNumber = "EXT-0005",
                Title = "Mr.",
                FirstName = "Emmanuel",
                MiddleName = "Kofi",
                LastName = "Darko",
                Email = "e.darko@hranalytics.io",
                PhoneNumber = "+233244100005",
                CompanyName = "HR Analytics IO",
                Role = "HR Data Analyst",
                HasFixedModule = false,
                ModuleId = (int?)null,
                IsActive = true
            },
            new
            {
                AssociateNumber = "EXT-0006",
                Title = "Dr.",
                FirstName = "Comfort",
                MiddleName = "",
                LastName = "Agyemang",
                Email = "c.agyemang@wellnesshub.gh",
                PhoneNumber = "+233244100006",
                CompanyName = "WellnessHub Ghana",
                Role = "Employee Wellness Consultant",
                HasFixedModule = false,
                ModuleId = (int?)null,
                IsActive = true
            },
            new
            {
                AssociateNumber = "EXT-0007",
                Title = "Mr.",
                FirstName = "Nana",
                MiddleName = "Yaw",
                LastName = "Frimpong",
                Email = "n.frimpong@itstaff.com",
                PhoneNumber = "+233244100007",
                CompanyName = "IT Staff Augmentation Ltd.",
                Role = "IT Contractor Liaison",
                HasFixedModule = true,
                ModuleId = (int?)4,
                IsActive = true
            },
            new
            {
                AssociateNumber = "EXT-0008",
                Title = "Mrs.",
                FirstName = "Joana",
                MiddleName = "",
                LastName = "Quaynor",
                Email = "j.quaynor@benifitsco.gh",
                PhoneNumber = "+233244100008",
                CompanyName = "BenefitsCo Ghana",
                Role = "Benefits Administrator",
                HasFixedModule = false,
                ModuleId = (int?)null,
                IsActive = false
            },
            new
            {
                AssociateNumber = "EXT-0009",
                Title = "Prof.",
                FirstName = "Richard",
                MiddleName = "Kwame",
                LastName = "Appiah",
                Email = "r.appiah@orgchange.edu.gh",
                PhoneNumber = "+233244100009",
                CompanyName = "Org Change Consulting",
                Role = "Organizational Development Advisor",
                HasFixedModule = false,
                ModuleId = (int?)null,
                IsActive = true
            },
            new
            {
                AssociateNumber = "EXT-0010",
                Title = "Ms.",
                FirstName = "Harriet",
                MiddleName = "",
                LastName = "Oti",
                Email = "h.oti@compliancepro.gh",
                PhoneNumber = "+233244100010",
                CompanyName = "CompliancePro Ghana",
                Role = "Labour Law Compliance Officer",
                HasFixedModule = true,
                ModuleId = (int?)5,
                IsActive = true
            }
        };

        foreach (var data in associatesData)
        {
            var existing = await _context.ExternalAssociates
                .FirstOrDefaultAsync(ea => ea.TenantId == tenantId && ea.Email == data.Email, cancellationToken);

            if (existing != null)
            {
                _logger.LogInformation("External associate already exists: {Email}", data.Email);
                continue;
            }

            var associate = new ExternalAssociate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AssociateNumber = data.AssociateNumber,
                Title = data.Title,
                FirstName = data.FirstName,
                MiddleName = data.MiddleName,
                LastName = data.LastName,
                Email = data.Email,
                PhoneNumber = data.PhoneNumber,
                CompanyName = data.CompanyName,
                Role = data.Role,
                HasFixedModule = data.HasFixedModule,
                ModuleId = data.ModuleId,
                IsActive = data.IsActive,
                PicturePath = string.Empty,
                DateAdded = now,
                CreatedAt = now,
                CreatedBy = string.Empty
            };

            _context.ExternalAssociates.Add(associate);
            totalCreated++;
            _logger.LogInformation("Created external associate: {Name} ({Company})",
                $"{data.FirstName} {data.LastName}", data.CompanyName);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("External associate seeding completed. Created {Count} records.", totalCreated);
        return totalCreated;
    }
}
