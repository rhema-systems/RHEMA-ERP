using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds HR data including departments and sample employees
/// </summary>
public class HRDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HRDataSeeder> _logger;

    public HRDataSeeder(ApplicationDbContext context, ILogger<HRDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting HR data seeding...");

            // Get default tenant
            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found. Cannot seed HR data.");
                return;
            }

            // Seed departments
            await SeedDepartmentsAsync(defaultTenant.Id);

            // Seed positions
            await SeedPositionsAsync(defaultTenant.Id);

            // Seed sample employees
            await SeedSampleEmployeesAsync(defaultTenant.Id);

            // Seed skills
            await SeedSkillsAsync(defaultTenant.Id);

            await _context.SaveChangesAsync();
            _logger.LogInformation("HR data seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during HR data seeding");
            throw;
        }
    }

    private async Task SeedDepartmentsAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding departments...");

        var departments = new[]
        {
            new { Code = "OPS", Name = "Operations", Type = DepartmentType.Operations, Description = "Operations department" },
            new { Code = "ADMIN", Name = "Administration", Type = DepartmentType.Administration, Description = "Administration department" },
            new { Code = "HR", Name = "Human Resources", Type = DepartmentType.HumanResources, Description = "HR department" },
            new { Code = "FIN", Name = "Finance", Type = DepartmentType.Finance, Description = "Finance department" },
            new { Code = "IT", Name = "Information Technology", Type = DepartmentType.IT, Description = "IT department" },
            new { Code = "MAINT", Name = "Maintenance", Type = DepartmentType.Maintenance, Description = "Maintenance and facilities department" },
            new { Code = "SAFE", Name = "Safety", Type = DepartmentType.Safety, Description = "Health and safety department" },
            new { Code = "QA", Name = "Quality Assurance", Type = DepartmentType.QualityAssurance, Description = "Quality assurance department" },
            new { Code = "RND", Name = "Research & Development", Type = DepartmentType.RnD, Description = "R&D department" },
            new { Code = "MKTG", Name = "Marketing", Type = DepartmentType.Marketing, Description = "Marketing department" },
            new { Code = "SALES", Name = "Sales", Type = DepartmentType.Sales, Description = "Sales department" }
        };

        foreach (var dept in departments)
        {
            var exists = await _context.Departments.AnyAsync(d => d.TenantId == tenantId && d.Code == dept.Code);
            if (!exists)
            {
                var department = new Department
                {
                    TenantId = tenantId,
                    Code = dept.Code,
                    Name = dept.Name,
                    DepartmentType = dept.Type,
                    Description = dept.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.Departments.Add(department);
                _logger.LogInformation("Seeded department: {DepartmentName}", dept.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedPositionsAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding positions...");

        // Get the maintenance department for positions
        var maintenanceDept = await _context.Departments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Code == "MAINT");
        var hrDept = await _context.Departments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Code == "HR");
        var itDept = await _context.Departments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Code == "IT");
        var adminDept = await _context.Departments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Code == "ADMIN");

        if (maintenanceDept == null)
        {
            _logger.LogWarning("Maintenance department not found. Skipping position seeding.");
            return;
        }

        // [HR-MODULE-PORT] Positions now REQUIRE OrganizationUnitId + OrganizationLevelId (DepartmentId
        // anchoring was removed). Resolve a default org unit/level for the tenant so SaveChanges succeeds.
        var (orgUnitId, orgLevelId) = await SeederOrgDefaults.EnsureDefaultUnitAsync(_context, tenantId);

        var positions = new[]
        {
            // Maintenance positions
            new { Title = "Maintenance Technician", Code = "TECH", Level = 1, DeptId = maintenanceDept.Id },
            new { Title = "Senior Maintenance Technician", Code = "SR-TECH", Level = 2, DeptId = maintenanceDept.Id },
            new { Title = "Maintenance Supervisor", Code = "MAINT-SUP", Level = 3, DeptId = maintenanceDept.Id },
            new { Title = "Maintenance Manager", Code = "MAINT-MGR", Level = 4, DeptId = maintenanceDept.Id },
            new { Title = "HVAC Technician", Code = "HVAC-TECH", Level = 1, DeptId = maintenanceDept.Id },
            new { Title = "Electrical Technician", Code = "ELEC-TECH", Level = 1, DeptId = maintenanceDept.Id },
            new { Title = "Facilities Engineer", Code = "FAC-ENG", Level = 2, DeptId = maintenanceDept.Id },
            
            // Other positions
            new { Title = "General Manager", Code = "GM", Level = 5, DeptId = adminDept?.Id ?? maintenanceDept.Id },
            new { Title = "HR Manager", Code = "HR-MGR", Level = 4, DeptId = hrDept?.Id ?? maintenanceDept.Id },
            new { Title = "IT Manager", Code = "IT-MGR", Level = 4, DeptId = itDept?.Id ?? maintenanceDept.Id }
        };

        foreach (var pos in positions)
        {
            var exists = await _context.EmployeePositions.AnyAsync(p => p.TenantId == tenantId && p.Code == pos.Code);
            if (!exists)
            {
                var position = new EmployeePosition
                {
                    TenantId = tenantId,
                    Title = pos.Title,
                    Code = pos.Code,
                    Level = pos.Level,
                    // [HR-MODULE-PORT] Positions anchor to the org structure via the REQUIRED
                    // OrganizationUnitId + OrganizationLevelId (DepartmentId was removed).
                    OrganizationUnitId = orgUnitId,
                    OrganizationLevelId = orgLevelId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.EmployeePositions.Add(position);
                _logger.LogInformation("Seeded position: {PositionTitle}", pos.Title);
            }
        }

        await _context.SaveChangesAsync();
        await _context.SaveChangesAsync();
    }

    private async Task SeedSampleEmployeesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding sample employees...");

        // Get Maintenance department
        var maintenanceDept = await _context.Departments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Code == "MAINT");

        if (maintenanceDept == null)
        {
            _logger.LogWarning("Maintenance department not found. Skipping employee seeding.");
            return;
        }

        // Get positions
        var techPosition = await _context.EmployeePositions
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == "TECH");
        var srTechPosition = await _context.EmployeePositions
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == "SR-TECH");
        var hvacTechPosition = await _context.EmployeePositions
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == "HVAC-TECH");
        var elecTechPosition = await _context.EmployeePositions
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == "ELEC-TECH");

        if (techPosition == null)
        {
            _logger.LogWarning("Technician positions not found. Skipping employee seeding.");
            return;
        }

        var sampleEmployees = new[]
        {
            new
            {
                FirstName = "John",
                LastName = "Smith",
                Email = "john.smith@company.com",
                Phone = "+1-555-0101",
                PositionId = techPosition.Id,
                Specialization = "General Maintenance"
            },
            new
            {
                FirstName = "Emily",
                LastName = "Junior",
                Email = "emily.junior@company.com",
                Phone = "+1-555-0102",
                PositionId = techPosition.Id,
                Specialization = "Electrical"
            },
            new
            {
                FirstName = "David",
                LastName = "Senior",
                Email = "david.senior@company.com",
                Phone = "+1-555-0103",
                PositionId = srTechPosition?.Id ?? techPosition.Id,
                Specialization = "HVAC"
            },
            new
            {
                FirstName = "Sarah",
                LastName = "Wilson",
                Email = "sarah.wilson@company.com",
                Phone = "+1-555-0104",
                PositionId = hvacTechPosition?.Id ?? techPosition.Id,
                Specialization = "HVAC Systems"
            },
            new
            {
                FirstName = "Michael",
                LastName = "Brown",
                Email = "michael.brown@company.com",
                Phone = "+1-555-0105",
                PositionId = elecTechPosition?.Id ?? techPosition.Id,
                Specialization = "Electrical Systems"
            }
        };

        // Keep the generated development employee numbers unique within this seeding run.
        // CountAsync does not include newly tracked employees until SaveChangesAsync, so
        // calculating it inside the loop previously assigned the same number to every
        // new sample employee and prevented a blank development database from seeding.
        var nextEmployeeSequence = await _context.Employees
            .CountAsync(e => e.TenantId == tenantId);

        foreach (var emp in sampleEmployees)
        {
            var exists = await _context.Employees.AnyAsync(e => e.TenantId == tenantId && e.EmailAddress == emp.Email);
            if (!exists)
            {
                // Generate employee number
                var currentYear = DateTime.Now.Year.ToString();
                var employeeNumber = $"{currentYear}{++nextEmployeeSequence:D4}";

                var employee = new Employee
                {
                    TenantId = tenantId,
                    EmployeeNumber = employeeNumber,
                    FirstName = emp.FirstName,
                    LastName = emp.LastName,
                    EmailAddress = emp.Email,
                    MobileNumber = emp.Phone,
                    DepartmentId = maintenanceDept.Id,
                    PositionId = emp.PositionId,
                    DateEmployed = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12)),
                    StaffStatus = StaffStatus.Active,
                    EmploymentType = EmploymentType.Permanent,
                    IsFullTime = true,
                    Gender = Gender.Male, // Simplified for sample data
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.Employees.Add(employee);
                _logger.LogInformation("Seeded employee: {FirstName} {LastName}", emp.FirstName, emp.LastName);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedSkillsAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding skills...");

        var skills = new[]
        {
            new { Name = "HVAC Maintenance", Category = "Technical", Description = "Heating, ventilation, and air conditioning systems" },
            new { Name = "Electrical Systems", Category = "Technical", Description = "Electrical wiring and systems maintenance" },
            new { Name = "Plumbing", Category = "Technical", Description = "Plumbing systems and fixtures" },
            new { Name = "Mechanical Repair", Category = "Technical", Description = "General mechanical repairs" },
            new { Name = "Preventive Maintenance", Category = "Technical", Description = "Scheduled preventive maintenance procedures" },
            new { Name = "Safety Compliance", Category = "Safety", Description = "Safety protocols and compliance" },
            new { Name = "Equipment Diagnostics", Category = "Technical", Description = "Diagnosing equipment problems" },
            new { Name = "Welding", Category = "Technical", Description = "Welding and metal fabrication" }
        };

        foreach (var skill in skills)
        {
            var exists = await _context.Skills.AnyAsync(s => s.TenantId == tenantId && s.Name == skill.Name);
            if (!exists)
            {
                var skillEntity = new Skill
                {
                    TenantId = tenantId,
                    Name = skill.Name,
                    Category = skill.Category,
                    Description = skill.Description,
                    IsActive = true,
                    RequiresCertification = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.Skills.Add(skillEntity);
                _logger.LogInformation("Seeded skill: {SkillName}", skill.Name);
            }
        }

        await _context.SaveChangesAsync();
    }
}
