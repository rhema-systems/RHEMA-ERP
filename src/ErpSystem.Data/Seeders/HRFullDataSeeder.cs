using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Shared;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds a standalone demo tenant with sample HR data across core modules.
/// This seeder does not rely on authentication; it creates its own tenant Guid.
/// </summary>
public class HRFullDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HRFullDataSeeder> _logger;

    public HRFullDataSeeder(ApplicationDbContext context, ILogger<HRFullDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Seed into the DEFAULT tenant so the seeded logins (admin/manager/employee,
        // all created under DEFAULT) actually see this demo data. Previously this created
        // a throwaway "HR-DEMO" tenant with a random id that no seeded user was mapped to,
        // so seed-hr-full's employees/org were invisible on login.
        var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
        var tenantId = defaultTenant?.Id ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
        const string tenantCode = "DEFAULT";
        const string tenantName = "Default Tenant";

        _logger.LogInformation("Seeding HR full demo data for tenant {TenantId} (DEFAULT)", tenantId);

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await EnsureTenantAsync(tenantId, tenantCode, tenantName);

                var divisions = await SeedDivisionsAsync(tenantId);
                var departments = await SeedDepartmentsAsync(tenantId, divisions);
                var sections = await SeedSectionsAsync(tenantId, departments);
                var units = await SeedUnitsAsync(tenantId, departments);
                var positions = await SeedPositionsAsync(tenantId, departments);
                var skills = await SeedSkillsAsync(tenantId);
                var employees = await SeedEmployeesAsync(tenantId, departments, sections, units, positions, skills);

                await SeedLeaveAsync(tenantId, employees);
                await SeedAwardsAsync(tenantId, employees);
                await SeedRecruitmentAsync(tenantId, departments, positions);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("HR full demo data seeding completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding HR full demo data");
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private async Task EnsureTenantAsync(Guid tenantId, string code, string name)
    {
        if (!await _context.Tenants.AnyAsync(t => t.Id == tenantId || t.Code == code))
        {
            _context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = code,
                Name = name,
                Status = ErpSystem.Shared.TenantStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder",
                Description = "Demo tenant for HR sample data",
                AllowSelfRegistration = false,
                IsDefaultForInternalUsers = false,
                IsDefaultForPublicUsers = false
            });
        }
    }

    private async Task<List<Division>> SeedDivisionsAsync(Guid tenantId)
    {
        var list = new List<Division>();
        for (var i = 1; i <= 5; i++)
        {
            var code = $"DIV{i:00}";
            if (await _context.Divisions.AnyAsync(d => d.TenantId == tenantId && d.Code == code)) continue;

            var entity = new Division
            {
                TenantId = tenantId,
                Code = code,
                Name = $"Division {i}",
                Description = $"Sample division {i}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            list.Add(entity);
            _context.Divisions.Add(entity);
        }
        await _context.SaveChangesAsync();
        return await _context.Divisions.Where(d => d.TenantId == tenantId).ToListAsync();
    }

    private async Task<List<Department>> SeedDepartmentsAsync(Guid tenantId, List<Division> divisions)
    {
        var list = new List<Department>();
        for (var i = 1; i <= 5; i++)
        {
            var code = $"DEP{i:00}";
            if (await _context.Departments.AnyAsync(d => d.TenantId == tenantId && d.Code == code)) continue;

            var division = divisions[i % divisions.Count];
            var entity = new Department
            {
                TenantId = tenantId,
                Code = code,
                Name = $"Department {i}",
                Description = $"Sample department {i}",
                DivisionId = division.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            list.Add(entity);
            _context.Departments.Add(entity);
        }
        await _context.SaveChangesAsync();
        return await _context.Departments.Where(d => d.TenantId == tenantId).ToListAsync();
    }

    private async Task EnsureOrganizationUnitsForDepartmentsAsync(Guid tenantId, IEnumerable<Department> departments)
    {
        var structure = await _context.Set<OrganizationStructure>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Code == "DEFAULT");

        if (structure == null)
        {
            structure = new OrganizationStructure
            {
                TenantId = tenantId,
                Name = "Default Organization Structure",
                Code = "DEFAULT",
                IsDefault = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            _context.Set<OrganizationStructure>().Add(structure);
            await _context.SaveChangesAsync();
        }

        var level = await _context.Set<OrganizationLevel>()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.StructureId == structure.Id && l.Code == "DEPT");

        if (level == null)
        {
            level = new OrganizationLevel
            {
                TenantId = tenantId,
                StructureId = structure.Id,
                Name = "Department",
                Code = "DEPT",
                LevelNumber = 1,
                RequiresHead = false,
                AllowsDirectEmployees = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            _context.Set<OrganizationLevel>().Add(level);
            await _context.SaveChangesAsync();
        }

        foreach (var dept in departments)
        {
            var exists = await _context.Set<OrganizationUnit>()
                .AnyAsync(u => u.TenantId == tenantId && u.OrganizationLevelId == level.Id && u.Code == dept.Code && !u.IsDeleted);

            if (exists) continue;

            _context.Set<OrganizationUnit>().Add(new OrganizationUnit
            {
                TenantId = tenantId,
                OrganizationLevelId = level.Id,
                Name = dept.Name,
                Code = dept.Code,
                IsActive = true,
                Sequence = 1,
                Path = string.Empty,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            });
        }

        await _context.SaveChangesAsync();

        var missingPath = await _context.Set<OrganizationUnit>()
            .Where(u => u.TenantId == tenantId && u.OrganizationLevelId == level.Id && (u.Path == "" || u.Path == null) && !u.IsDeleted)
            .ToListAsync();

        foreach (var unit in missingPath)
            unit.Path = $"/{unit.Id}";

        await _context.SaveChangesAsync();
    }

    private async Task<List<Section>> SeedSectionsAsync(Guid tenantId, List<Department> departments)
    {
        var list = new List<Section>();
        for (var i = 1; i <= 5; i++)
        {
            var code = $"SEC{i:00}";
            if (await _context.Sections.AnyAsync(d => d.TenantId == tenantId && d.Code == code)) continue;

            var dept = departments[i % departments.Count];
            var entity = new Section
            {
                TenantId = tenantId,
                Code = code,
                Name = $"Section {i}",
                Description = $"Sample section {i}",
                DepartmentId = dept.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            list.Add(entity);
            _context.Sections.Add(entity);
        }
        await _context.SaveChangesAsync();
        return await _context.Sections.Where(d => d.TenantId == tenantId).ToListAsync();
    }

    private async Task<List<Unit>> SeedUnitsAsync(Guid tenantId, List<Department> departments)
    {
        var list = new List<Unit>();
        for (var i = 1; i <= 5; i++)
        {
            var code = $"UNI{i:00}";
            if (await _context.Units.AnyAsync(d => d.TenantId == tenantId && d.Code == code)) continue;

            var dept = departments[i % departments.Count];
            var section = await _context.Sections.FirstAsync(s => s.TenantId == tenantId && s.DepartmentId == dept.Id);
            var entity = new Unit
            {
                TenantId = tenantId,
                Code = code,
                Name = $"Unit {i}",
                Description = $"Sample unit {i}",
                SectionId = section.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            list.Add(entity);
            _context.Units.Add(entity);
        }
        await _context.SaveChangesAsync();
        return await _context.Units.Where(d => d.TenantId == tenantId).ToListAsync();
    }

    private async Task<List<EmployeePosition>> SeedPositionsAsync(Guid tenantId, List<Department> departments)
    {
        var list = new List<EmployeePosition>();

        await EnsureOrganizationUnitsForDepartmentsAsync(tenantId, departments);
        var orgUnitsByCode = await _context.Set<OrganizationUnit>()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .ToDictionaryAsync(u => u.Code, u => u.Id);
        var deptLevel = await _context.Set<OrganizationLevel>()
            .FirstAsync(l => l.TenantId == tenantId && l.Code == "DEPT");

        for (var i = 1; i <= 5; i++)
        {
            var code = $"POS{i:00}";
            if (await _context.EmployeePositions.AnyAsync(p => p.TenantId == tenantId && p.Code == code)) continue;

            var dept = departments[i % departments.Count];
            if (!orgUnitsByCode.TryGetValue(dept.Code, out var orgUnitId))
                continue;

            var entity = new EmployeePosition
            {
                TenantId = tenantId,
                Code = code,
                Title = $"Position {i}",
                OrganizationLevelId = deptLevel.Id,
                OrganizationUnitId = orgUnitId,
                Level = i,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            list.Add(entity);
            _context.EmployeePositions.Add(entity);
        }
        await _context.SaveChangesAsync();
        return await _context.EmployeePositions.Where(p => p.TenantId == tenantId).ToListAsync();
    }

    private async Task<List<Skill>> SeedSkillsAsync(Guid tenantId)
    {
        var names = new[] { "Leadership", "Data Analysis", "Safety Compliance", "Customer Success", "Process Improvement" };
        var list = new List<Skill>();
        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            if (await _context.Skills.AnyAsync(s => s.TenantId == tenantId && s.Name == name)) continue;

            var entity = new Skill
            {
                TenantId = tenantId,
                Name = name,
                Category = "General",
                Description = $"Sample skill: {name}",
                IsActive = true,
                RequiresCertification = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            list.Add(entity);
            _context.Skills.Add(entity);
        }
        await _context.SaveChangesAsync();
        return await _context.Skills.Where(s => s.TenantId == tenantId).ToListAsync();
    }

    private async Task<List<Employee>> SeedEmployeesAsync(
        Guid tenantId,
        List<Department> departments,
        List<Section> sections,
        List<Unit> units,
        List<EmployeePosition> positions,
        List<Skill> skills)
    {
        var list = new List<Employee>();
        for (var i = 1; i <= 5; i++)
        {
            var email = $"employee{i}@demo.local";
            if (await _context.Employees.AnyAsync(e => e.TenantId == tenantId && e.EmailAddress == email)) continue;

            var dept = departments[i % departments.Count];
            var section = sections[i % sections.Count];
            var unit = units[i % units.Count];
            var pos = positions[i % positions.Count];
            var number = $"EMP-{DateTime.UtcNow:yyyy}{i:000}";

            var employee = new Employee
            {
                TenantId = tenantId,
                EmployeeNumber = number,
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                EmailAddress = email,
                MobileNumber = $"+100000000{i}",
                DepartmentId = dept.Id,
                SectionId = section.Id,
                OrganizationUnitId = pos.OrganizationUnitId,
                PositionId = pos.Id,
                StaffStatus = StaffStatus.Active,
                EmploymentType = EmploymentType.Permanent,
                IsFullTime = true,
                Gender = i % 2 == 0 ? Gender.Female : Gender.Male,
                DateEmployed = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6 - i)),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder",
                Specialization = "General",
                ExperienceLevel = "Intermediate",
                CurrentWorkload = 25 + 5 * i
            };
            list.Add(employee);
            _context.Employees.Add(employee);
        }

        await _context.SaveChangesAsync();

        // Assign one skill each as sample (many-to-many not defined here; keeping to base entities only)
        return await _context.Employees.Where(e => e.TenantId == tenantId).ToListAsync();
    }

    private async Task SeedLeaveAsync(Guid tenantId, List<Employee> employees)
    {
        if (!employees.Any()) return;
        var first = employees.First();

        // Seed a simple leave type
        var leaveType = await _context.LeaveTypes.FirstOrDefaultAsync(l => l.TenantId == tenantId);
        if (leaveType == null)
        {
            leaveType = new LeaveType
            {
                TenantId = tenantId,
                Name = "Annual Leave",
                Code = "AL",
                Description = "Sample annual leave",
                IsPaid = true,
                RequiresApproval = true,
                DefaultDaysPerYear = 20,
                MaxDaysPerYear = 30,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            _context.LeaveTypes.Add(leaveType);
            await _context.SaveChangesAsync();
        }

        for (var i = 1; i <= 5; i++)
        {
            var exists = await _context.LeaveRequests.AnyAsync(l => l.TenantId == tenantId && l.Reason == $"Sample leave {i}");
            if (exists) continue;

            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14 + i * 2));
            var end = start.AddDays(2);
            _context.LeaveRequests.Add(new LeaveRequest
            {
                TenantId = tenantId,
                RequestNumber = $"LV-SEED-{i:D3}",
                EmployeeId = first.Id,
                LeaveTypeId = leaveType.Id,
                StartDate = start,
                EndDate = end,
                TotalDays = 3,
                Reason = $"Sample leave {i}",
                Status = LeaveStatus.Approved,
                RequestDate = DateTime.UtcNow.AddDays(-20 + i),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            });
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedAwardsAsync(Guid tenantId, List<Employee> employees)
    {
        if (!employees.Any()) return;
        var awardTypes = new List<AwardType>();
        for (var i = 1; i <= 5; i++)
        {
            var code = $"AWD{i:00}";
            if (await _context.AwardTypes.AnyAsync(a => a.TenantId == tenantId && a.Code == code)) continue;

            var type = new AwardType
            {
                TenantId = tenantId,
                Code = code,
                Name = $"Award Type {i}",
                Category = AwardCategory.Performance,
                Frequency = AwardFrequency.Annual,
                HasMonetaryReward = true,
                MinMonetaryAmount = 500 + i * 50,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            awardTypes.Add(type);
            _context.AwardTypes.Add(type);
        }
        await _context.SaveChangesAsync();
        awardTypes = await _context.AwardTypes.Where(a => a.TenantId == tenantId).ToListAsync();

        var employee = employees.First();
        for (var i = 1; i <= 5; i++)
        {
            var number = $"AW-{DateTime.UtcNow:yyyy}-{i:000}";
            if (await _context.EmployeeAwards.AnyAsync(a => a.TenantId == tenantId && a.AwardNumber == number)) continue;

            var type = awardTypes[i % awardTypes.Count];
            _context.EmployeeAwards.Add(new EmployeeAward
            {
                TenantId = tenantId,
                AwardNumber = number,
                EmployeeId = employee.Id,
                AwardTypeId = type.Id,
                AwardDate = DateTime.UtcNow.AddDays(-i * 7),
                Citation = $"Outstanding performance {i}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            });
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedRecruitmentAsync(Guid tenantId, List<Department> departments, List<EmployeePosition> positions)
    {
        if (!departments.Any() || !positions.Any()) return;
        var requester = await _context.Employees.FirstAsync(e => e.TenantId == tenantId);

        // Seed a minimal country for applicants
        var country = await _context.Countries.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (country == null)
        {
            country = new Country
            {
                TenantId = tenantId,
                Name = "Demo Country",
                Code = "DEM",
                Alpha2Code = "DM",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            _context.Countries.Add(country);
            await _context.SaveChangesAsync();
        }

        var requisitions = new List<StaffRequisition>();
        for (var i = 1; i <= 5; i++)
        {
            var reqNumber = $"REQ-{DateTime.UtcNow:yyyy}-{i:000}";
            if (await _context.StaffRequisitions.AnyAsync(r => r.TenantId == tenantId && r.RequisitionNumber == reqNumber)) continue;

            var pos = positions[i % positions.Count];

            var jobDescription = new JobDescription
            {
                TenantId = tenantId,
                PositionId = pos.Id,
                JobTitle = pos.Title,
                JobDescriptionNumber = $"JD-{i:000}",
                EffectiveDate = DateTime.UtcNow.AddDays(-30),
                JobSummary = $"Job summary for {pos.Title}",
                ReviewCycleMonths = 24,
                Status = JobDescriptionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            _context.JobDescriptions.Add(jobDescription);
            await _context.SaveChangesAsync();

            var req = new StaffRequisition
            {
                TenantId = tenantId,
                RequisitionNumber = reqNumber,
                PositionId = pos.Id,
                Type = StaffRequisitionType.Replacement,
                Priority = StaffRequisitionPriority.Medium,
                NumberOfPositions = 1,
                DesiredStartDate = DateTime.UtcNow.AddDays(30),
                RequestDate = DateTime.UtcNow.AddDays(-10),
                BusinessJustification = $"Need to backfill position {pos.Title}",
                JobDescriptionId = jobDescription.Id,
                RequestedById = requester.Id,
                Status = StaffRequisitionStatus.Submitted,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            };
            requisitions.Add(req);
            _context.StaffRequisitions.Add(req);
        }
        await _context.SaveChangesAsync();

        var vacancies = new List<JobVacancy>();
        foreach (var req in requisitions)
        {
            var vacNumber = $"VAC-{req.RequisitionNumber}";
            if (await _context.JobVacancies.AnyAsync(v => v.TenantId == tenantId && v.VacancyNumber == vacNumber)) continue;

            var pos = positions.First(p => p.Id == req.PositionId);
            vacancies.Add(new JobVacancy
            {
                TenantId = tenantId,
                VacancyNumber = vacNumber,
                StaffRequisitionId = req.Id,
                PositionId = req.PositionId,
                CustomAdvertTitle = $"Opening for {pos.Title}",
                VacancyStatus = JobVacancyStatus.Published,
                PublishDate = DateTime.UtcNow,
                ApplicationDeadline = DateTime.UtcNow.AddDays(20),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seeder"
            });
        }
        _context.JobVacancies.AddRange(vacancies);
        await _context.SaveChangesAsync();

        var vacancyList = await _context.JobVacancies.Where(v => v.TenantId == tenantId).ToListAsync();
        foreach (var vac in vacancyList)
        {
            for (var i = 1; i <= 5; i++)
            {
                var applicantNumber = $"{vac.VacancyNumber}-APP{i:000}";
                if (await _context.JobApplications.AnyAsync(a => a.TenantId == tenantId && a.ApplicationNumber == applicantNumber)) continue;

                var candidate = new JobCandidate
                {
                    TenantId = tenantId,
                    CandidateNumber = $"CAND-{applicantNumber}",
                    FirstName = $"Applicant{i}",
                    LastName = "Demo",
                    Email = $"applicant{i}@{vac.VacancyNumber.Replace("-", "").ToLower()}.local",
                    Phone = $"+19999999{i}",
                    City = "Demo City",
                    CountryId = country.Id,
                    DateOfBirth = DateTime.UtcNow.AddYears(-25 - i),
                    Gender = i % 2 == 0 ? Gender.Female : Gender.Male,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Seeder"
                };
                _context.JobCandidates.Add(candidate);
                await _context.SaveChangesAsync();

                _context.JobApplications.Add(new JobApplication
                {
                    TenantId = tenantId,
                    ApplicationNumber = applicantNumber,
                    JobVacancyId = vac.Id,
                    JobCandidateId = candidate.Id,
                    ApplicationDate = DateTime.UtcNow.AddDays(-i),
                    Status = ApplicationStatus.New,
                    Source = ApplicationSource.CompanyWebsite,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Seeder"
                });
            }
        }
        await _context.SaveChangesAsync();
    }
}

