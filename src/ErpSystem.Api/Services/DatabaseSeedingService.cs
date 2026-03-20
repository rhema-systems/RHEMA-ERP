using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Web.Services
{
    public interface IDatabaseSeedingService
    {
        Task SeedAsync();
        Task SeedWithoutMigrationAsync();
        Task SeedBasicDataAsync();
        Task SeedTestUsersAsync();
        Task SeedMaintenanceE2ETestDataAsync();
        Task<bool> HasSeedDataAsync();
    }

    public class DatabaseSeedingService : IDatabaseSeedingService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ILogger<DatabaseSeedingService> _logger;
        private readonly IWebHostEnvironment _environment;

        public DatabaseSeedingService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<DatabaseSeedingService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _environment = environment;
        }

        public Task SeedAsync() => SeedCoreAsync(applyMigrations: true);

        public Task SeedWithoutMigrationAsync() => SeedCoreAsync(applyMigrations: false);

        private async Task SeedCoreAsync(bool applyMigrations)
        {
            try
            {
                _logger.LogInformation("Starting database seeding...");

                if (applyMigrations)
                {
                    // Ensure database is created and migrated
                    await _context.Database.MigrateAsync();
                }

                // Always ensure roles exist (safe/idempotent; required for new module roles on existing DBs)
                _logger.LogInformation("Ensuring roles are seeded...");
                await SeedRolesAsync();
                await SeedRolePermissionAssignmentsAsync();

                // Check if we already have seed data
                var hasData = await HasSeedDataAsync();
                if (!hasData)
                {
                    // Seed basic data
                    await SeedBasicDataAsync();
                }
                else
                {
                    _logger.LogInformation("Basic data already exists, skipping basic seeding");
                    
                    // But always ensure tenant modules are seeded
                    _logger.LogInformation("Ensuring tenant modules are seeded...");
                    await SeedDefaultTenantModulesAsync();
                }

                // Always ensure baseline EHC workflow exists (required for ticket lifecycle management)
                _logger.LogInformation("Ensuring EHC workflow is seeded...");
                await EnsureEhcWorkflowSeededAsync();
                _logger.LogInformation("Ensuring project workflows are seeded...");
                await EnsureProjectWorkflowsSeededAsync();
                _logger.LogInformation("Ensuring project catalog defaults are seeded...");
                await EnsureProjectCatalogDefaultsSeededAsync();

                // Always ensure baseline EHC workflow routing rules exist (workflow selection by type/category/priority/department)
                _logger.LogInformation("Ensuring EHC workflow routing rules are seeded...");
                await EnsureEhcWorkflowRoutingRulesSeededAsync();

                // Always ensure baseline EHC categories and SLA templates exist (external portal UI depends on them)
                _logger.LogInformation("Ensuring EHC categories and SLA templates are seeded...");
                await EnsureEhcCategoriesSeededAsync();
                await EnsureEhcSlaTemplatesSeededAsync();

                // Always ensure baseline KB exists (Phase 2 agent productivity)
                _logger.LogInformation("Ensuring EHC knowledge base is seeded...");
                await EnsureEhcKnowledgeBaseSeededAsync();

                // Always ensure baseline file upload governance exists (Phase 2 attachment hardening)
                _logger.LogInformation("Ensuring file upload policies are seeded...");
                await EnsureFileUploadPoliciesSeededAsync();

                // Always ensure baseline EHC notification topics exist (templated in-app/email notifications)
                _logger.LogInformation("Ensuring EHC notification topics are seeded...");
                try
                {
                    await EnsureEhcNotificationTopicsSeededAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "EHC notification topic seeding failed (table may not exist yet). Continuing...");
                    _context.ChangeTracker.Clear();
                }

                // Always seed/update test users in development to ensure correct passwords
                if (_environment.IsDevelopment())
                {
                    _logger.LogInformation("Ensuring test users have correct passwords...");
                    await SeedTestUsersAsync();
                    _logger.LogInformation("Ensuring project demo data is seeded...");
                    await EnsureProjectDemoDataSeededAsync();

                    // Always ensure maintenance configuration is seeded in development
                    _logger.LogInformation("Ensuring maintenance configuration is seeded...");
                    // await SeedMaintenanceConfigurationAsync();
                    
                    // Seed comprehensive maintenance data (inventory, assets, templates, checklists)
                    _logger.LogInformation("Ensuring comprehensive maintenance data is seeded...");
                    // await SeedMaintenanceComprehensiveDataAsync();
                    
                    // Seed quality control checklists
                    _logger.LogInformation("Ensuring QC checklists are seeded...");
                    try
                    {
                        await SeedQualityControlChecklistsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "QC checklist seeding failed (schema mismatch). Continuing...");
                        _context.ChangeTracker.Clear();
                    }
                    
                    // Seed finance data (currencies, accounts, fiscal years, settings)
                    _logger.LogInformation("Ensuring finance data is seeded...");
                    await SeedFinanceDataAsync();

                    // Seed EHC helpdesk demo data (tickets, feedback, problems, service requests, channels, compliance)
                    _logger.LogInformation("Ensuring EHC helpdesk demo data is seeded...");
                    var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
                    if (defaultTenant != null)
                    {
                        var ehcDemoSeeder = new EhcHelpdeskDemoSeeder(_context, _logger);
                        await ehcDemoSeeder.SeedAsync(defaultTenant.Id);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database seeding");
                throw;
            }
        }



        public async Task SeedBasicDataAsync()
        {
            _logger.LogInformation("Seeding basic data...");

            // Seed default tenant
            await SeedDefaultTenantAsync();
            
            // Seed default tenant modules
            await SeedDefaultTenantModulesAsync();

            // Seed HR data (departments, positions, employees)
            await SeedHRDataAsync();

            // Seed maintenance configuration (work order types, priority levels, maintenance types)
            // await SeedMaintenanceConfigurationAsync();

            // Seed finance data (currencies, accounts, fiscal years, settings)
            await SeedFinanceDataAsync();

            // Seed baseline EHC workflow definition
            await EnsureEhcWorkflowSeededAsync();
            await EnsureProjectWorkflowsSeededAsync();
            await EnsureProjectCatalogDefaultsSeededAsync();

            _logger.LogInformation("Basic data seeding completed");
        }

        private async Task EnsureProjectWorkflowsSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    await EnsureWorkflowDefinitionSeededAsync(
                        tenant.Id,
                        entityCode: "Project",
                        entityName: "Project",
                        entityClassName: typeof(Project).FullName,
                        definitionName: "Project Approval",
                        description: "Baseline project initiation approval: Draft -> PendingApproval -> Planned.",
                        approvalRoleNames: new[] { Constants.Roles.TenantAdmin, Constants.Roles.Manager });

                    await EnsureWorkflowDefinitionSeededAsync(
                        tenant.Id,
                        entityCode: "ProjectBudgetRevision",
                        entityName: "Project Budget Revision",
                        entityClassName: typeof(ProjectBudgetRevision).FullName,
                        definitionName: "Project Budget Revision Approval",
                        description: "Baseline project budget revision approval: Draft -> PendingApproval -> Approved.",
                        approvalRoleNames: new[] { "Finance User", Constants.Roles.TenantAdmin });

                    await EnsureWorkflowDefinitionSeededAsync(
                        tenant.Id,
                        entityCode: "ProjectClosure",
                        entityName: "Project Closure",
                        entityClassName: typeof(ProjectClosure).FullName,
                        definitionName: "Project Closure Approval",
                        description: "Baseline project closure approval: Draft -> PendingApproval -> Closed.",
                        approvalRoleNames: new[] { Constants.Roles.Manager, Constants.Roles.TenantAdmin });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed project workflows");
            }
        }

        private async Task EnsureProjectCatalogDefaultsSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants
                    .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active)
                    .Select(t => new { t.Id })
                    .ToListAsync();

                var catalogGroups = ProjectCatalogDefaults.GetRecommendedCatalogs();
                foreach (var tenant in tenants)
                {
                    var existing = await _context.ProjectCatalogEntries
                        .Where(entry => !entry.IsDeleted && entry.TenantId == tenant.Id)
                        .Select(entry => new { entry.CatalogType, entry.Code })
                        .ToListAsync();

                    var existingKeys = existing
                        .Select(entry => $"{entry.CatalogType}::{entry.Code}")
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var created = false;
                    foreach (var group in catalogGroups)
                    {
                        for (var index = 0; index < group.Items.Count; index++)
                        {
                            var item = group.Items[index];
                            var key = $"{group.Key}::{item.Code}";
                            if (existingKeys.Contains(key))
                            {
                                continue;
                            }

                            _context.ProjectCatalogEntries.Add(new ProjectCatalogEntry
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenant.Id,
                                CatalogType = group.Key,
                                Code = item.Code,
                                Name = item.Name,
                                SortOrder = (index + 1) * 10,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = "System"
                            });

                            existingKeys.Add(key);
                            created = true;
                        }
                    }

                    if (created)
                    {
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed project catalog defaults");
            }
        }

        private async Task EnsureProjectDemoDataSeededAsync()
        {
            try
            {
                var tenantIds = await _context.Tenants
                    .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active)
                    .Select(t => t.Id)
                    .ToListAsync();

                foreach (var tenantId in tenantIds)
                {
                    await EnsureProjectDemoDataSeededAsync(tenantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed project demo data");
            }
        }

        private async Task EnsureProjectDemoDataSeededAsync(Guid tenantId)
        {
            var activeUsers = await _userManager.Users
                .Where(user => user.TenantId == tenantId && user.IsActive)
                .OrderBy(user => user.UserName)
                .ToListAsync();

            if (activeUsers.Count == 0)
            {
                _logger.LogWarning("Skipping project demo data for tenant {TenantId} because no active users were found.", tenantId);
                return;
            }

            var now = DateTime.UtcNow;
            var sponsor = activeUsers.First();
            var projectManager = activeUsers.Skip(1).FirstOrDefault() ?? sponsor;
            var financeOwner = activeUsers.Skip(2).FirstOrDefault() ?? projectManager;
            var teamMember = activeUsers.Skip(3).FirstOrDefault() ?? financeOwner;

            var department = await _context.Departments
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
                .OrderBy(item => item.Name)
                .FirstOrDefaultAsync();

            var location = await _context.Locations
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderBy(item => item.Name)
                .FirstOrDefaultAsync();

            var customer = await _context.BusinessPartners
                .FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.RegistrationStatus == "Approved"
                    && (item.PartnerType == "Customer" || item.PartnerType == "Both"));

            if (customer == null)
            {
                customer = new BusinessPartner
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PartnerCode = "CUST-DEMO-PM",
                    PartnerName = "Northwind Transformation Group",
                    PartnerType = "Customer",
                    LegalName = "Northwind Transformation Group",
                    PrimaryContactName = "Ava Collins",
                    PrimaryEmail = "projects@northwind.example",
                    PrimaryPhone = "+1-555-0133",
                    PhysicalCity = "Accra",
                    PhysicalCountry = "Ghana",
                    RegistrationStatus = "Approved",
                    ApprovalStatus = "Approved",
                    ApprovedById = sponsor.Id,
                    ApprovedDate = now.AddDays(-120),
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                _context.BusinessPartners.Add(customer);
            }

            var implementationType = await EnsureProjectTypeAsync(
                tenantId,
                "IMPLEMENTATION",
                "Implementation Project",
                "Customer or internal delivery engagements executed against an approved plan.",
                requiresSponsor: true,
                requiresApproval: true);
            var internalType = await EnsureProjectTypeAsync(
                tenantId,
                "INTERNAL",
                "Internal Initiative",
                "Operational improvement and transformation initiatives owned internally.",
                requiresSponsor: true,
                requiresApproval: true);
            var capexType = await EnsureProjectTypeAsync(
                tenantId,
                "CAPEX",
                "Capital Project",
                "Asset or facilities improvement work with formal budget controls.",
                requiresSponsor: true,
                requiresApproval: true);

            var highPriority = await EnsureProjectPriorityAsync(tenantId, "HIGH", "High", "#DC2626", 10);
            var mediumPriority = await EnsureProjectPriorityAsync(tenantId, "MEDIUM", "Medium", "#D97706", 20);
            var lowPriority = await EnsureProjectPriorityAsync(tenantId, "LOW", "Low", "#2563EB", 30);

            var portfolio = await _context.ProjectPortfolios
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == "PORTFOLIO-TRANSFORM");
            if (portfolio == null)
            {
                portfolio = new ProjectPortfolio
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = "PORTFOLIO-TRANSFORM",
                    Name = "Enterprise Transformation Portfolio",
                    Description = "Priority projects for customer delivery, internal transformation, and capital improvement.",
                    Status = "Active",
                    StrategicObjective = "Improve delivery control, cash flow, and operating resilience.",
                    OwnerId = sponsor.Id,
                    SponsorId = sponsor.Id,
                    StartDate = now.Date.AddMonths(-3),
                    TargetEndDate = now.Date.AddMonths(12),
                    BudgetCap = 1500000m,
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                _context.ProjectPortfolios.Add(portfolio);
            }

            var program = await _context.ProjectPrograms
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == "PROGRAM-ERP-DELIVERY");
            if (program == null)
            {
                program = new ProjectProgram
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PortfolioId = portfolio.Id,
                    Code = "PROGRAM-ERP-DELIVERY",
                    Name = "ERP Delivery Program",
                    Description = "Cross-functional delivery work covering implementation, adoption, and stabilization.",
                    Status = "Active",
                    ProgramManagerId = projectManager.Id,
                    SponsorId = sponsor.Id,
                    StartDate = now.Date.AddMonths(-2),
                    TargetEndDate = now.Date.AddMonths(10),
                    BudgetCap = 850000m,
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                _context.ProjectPrograms.Add(program);
            }

            var template = await _context.ProjectTemplates
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == "TPL-ERP-IMPLEMENTATION");
            if (template == null)
            {
                template = new ProjectTemplate
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = "TPL-ERP-IMPLEMENTATION",
                    Name = "ERP Implementation Template",
                    Description = "Baseline phases, deliverables, and controls for ERP delivery projects.",
                    ProjectTypeId = implementationType.Id,
                    VersionLabel = "1.0",
                    TemplateDefinitionJson = JsonSerializer.Serialize(new
                    {
                        phases = new[] { "Initiation", "Design", "Build", "Test", "Go Live", "Stabilization" },
                        milestones = new[] { "Design Sign-off", "UAT Complete", "Go Live" },
                        roles = new[] { "Project Manager", "Functional Lead", "Technical Lead", "Finance Analyst" }
                    }),
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                _context.ProjectTemplates.Add(template);
            }

            var settings = await _context.ProjectManagementSettings
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted);
            if (settings == null)
            {
                settings = new ProjectManagementSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectNumberFormat = "PRJ-{YYYY}-{####}",
                    RequireSponsor = true,
                    DefaultApprovalRequired = true,
                    DefaultProjectTypeId = implementationType.Id,
                    DefaultProjectPriorityId = mediumPriority.Id,
                    DefaultTemplateId = template.Id,
                    MandatoryFieldsByTypeJson = JsonSerializer.Serialize(new
                    {
                        IMPLEMENTATION = new[] { "SponsorId", "ProjectManagerId", "EstimatedBudget", "StartDate", "TargetEndDate" },
                        INTERNAL = new[] { "SponsorId", "ProjectManagerId", "StartDate" },
                        CAPEX = new[] { "SponsorId", "EstimatedBudget", "FundingSource" }
                    }),
                    Notes = "Development seed defaults for project setup and numbering.",
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                _context.ProjectManagementSettings.Add(settings);
            }

            await _context.SaveChangesAsync();
            await NormalizeProjectDemoFundingSourcesAsync(tenantId);

            await EnsureProjectDemoSeededAsync(
                tenantId,
                "PRJ-DEMO-1001",
                () => CreateImplementationProject(
                    tenantId,
                    implementationType,
                    highPriority,
                    template,
                    portfolio,
                    program,
                    sponsor,
                    projectManager,
                    financeOwner,
                    teamMember,
                    department,
                    location,
                    customer,
                    now));

            await EnsureProjectDemoSeededAsync(
                tenantId,
                "PRJ-DEMO-1002",
                () => CreateInternalPlanningProject(
                    tenantId,
                    internalType,
                    mediumPriority,
                    template,
                    portfolio,
                    program,
                    sponsor,
                    projectManager,
                    financeOwner,
                    department,
                    location,
                    now));

            await EnsureProjectDemoSeededAsync(
                tenantId,
                "PRJ-DEMO-1003",
                () => CreateClosedCapexProject(
                    tenantId,
                    capexType,
                    lowPriority,
                    portfolio,
                    sponsor,
                    projectManager,
                    financeOwner,
                    teamMember,
                    department,
                    location,
                    customer,
                    now));

            await EnsureProjectDemoInterdependenciesSeededAsync(tenantId, projectManager, financeOwner, now);
            await EnsureProjectDemoQualityDataSeededAsync(tenantId, sponsor, financeOwner, teamMember, now);
            await EnsureProjectDemoProcurementAndMaterialDataSeededAsync(tenantId, customer, financeOwner, department, now);
        }

        private async Task EnsureProjectDemoSeededAsync(Guid tenantId, string projectCode, Action create)
        {
            var exists = await _context.Projects
                .AnyAsync(project => project.TenantId == tenantId && !project.IsDeleted && project.ProjectCode == projectCode);
            if (exists)
            {
                return;
            }

            create();
            await _context.SaveChangesAsync();
        }

        private async Task<ProjectType> EnsureProjectTypeAsync(
            Guid tenantId,
            string code,
            string name,
            string description,
            bool requiresSponsor,
            bool requiresApproval)
        {
            var entity = await _context.ProjectTypes
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == code);

            if (entity != null)
            {
                return entity;
            }

            entity = new ProjectType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Description = description,
                IsActive = true,
                RequiresSponsor = requiresSponsor,
                RequiresApproval = requiresApproval,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            _context.ProjectTypes.Add(entity);
            return entity;
        }

        private async Task<ProjectPriority> EnsureProjectPriorityAsync(Guid tenantId, string code, string name, string colorHex, int sortOrder)
        {
            var entity = await _context.ProjectPriorities
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == code);

            if (entity != null)
            {
                return entity;
            }

            entity = new ProjectPriority
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                ColorHex = colorHex,
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            _context.ProjectPriorities.Add(entity);
            return entity;
        }

        private async Task NormalizeProjectDemoFundingSourcesAsync(Guid tenantId)
        {
            var projects = await _context.Projects
                .Where(project =>
                    project.TenantId == tenantId
                    && !project.IsDeleted
                    && (project.ProjectCode == "PRJ-DEMO-1001" || project.ProjectCode == "PRJ-DEMO-1002" || project.ProjectCode == "PRJ-DEMO-1003"))
                .ToListAsync();

            if (projects.Count == 0)
            {
                return;
            }

            var changed = false;
            foreach (var project in projects)
            {
                var normalizedFundingSource = project.ProjectCode switch
                {
                    "PRJ-DEMO-1001" => "Customer Contract",
                    "PRJ-DEMO-1002" => "Internal Budget",
                    "PRJ-DEMO-1003" => "Capex Allocation",
                    _ => project.FundingSource
                };

                if (!string.Equals(project.FundingSource, normalizedFundingSource, StringComparison.Ordinal))
                {
                    project.FundingSource = normalizedFundingSource;
                    project.UpdatedAt = DateTime.UtcNow;
                    project.UpdatedBy = "System";
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task EnsureProjectDemoInterdependenciesSeededAsync(
            Guid tenantId,
            ApplicationUser projectManager,
            ApplicationUser financeOwner,
            DateTime now)
        {
            var demoProjects = await _context.Projects
                .Where(project =>
                    project.TenantId == tenantId
                    && !project.IsDeleted
                    && (project.ProjectCode == "PRJ-DEMO-1001"
                        || project.ProjectCode == "PRJ-DEMO-1002"
                        || project.ProjectCode == "PRJ-DEMO-1003"))
                .Select(project => new
                {
                    project.Id,
                    project.ProjectCode
                })
                .ToListAsync();

            if (demoProjects.Count < 3)
            {
                return;
            }

            var projectByCode = demoProjects.ToDictionary(project => project.ProjectCode, StringComparer.OrdinalIgnoreCase);
            var seeds = new[]
            {
                new
                {
                    SourceProjectId = projectByCode["PRJ-DEMO-1001"].Id,
                    TargetProjectId = projectByCode["PRJ-DEMO-1002"].Id,
                    DependencyType = "Reporting",
                    Title = "UAT readiness metrics feed PMO rollout",
                    Status = "Monitoring",
                    ImpactLevel = "High",
                    OwnerId = financeOwner.Id,
                    DueDate = now.Date.AddDays(10),
                    Description = "The customer rollout needs the PMO reporting templates before readiness reporting can be finalized.",
                    MitigationPlan = "Review the shared readiness pack in the weekly PMO cadence and lock the reporting handoff date."
                },
                new
                {
                    SourceProjectId = projectByCode["PRJ-DEMO-1002"].Id,
                    TargetProjectId = projectByCode["PRJ-DEMO-1003"].Id,
                    DependencyType = "LessonsLearned",
                    Title = "Closure lessons feed governance template refresh",
                    Status = "Open",
                    ImpactLevel = "Medium",
                    OwnerId = projectManager.Id,
                    DueDate = now.Date.AddDays(-2),
                    Description = "The PMO rollout still needs the warehouse refresh closure lessons before the governance checklist pack is finalized.",
                    MitigationPlan = "Capture the closure insights in the next PMO workshop and update the template set immediately after review."
                }
            };

            var existing = await _context.ProjectInterdependencies
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => new
                {
                    item.SourceProjectId,
                    item.TargetProjectId,
                    item.DependencyType,
                    item.Title
                })
                .ToListAsync();

            var changed = false;
            foreach (var seed in seeds)
            {
                var exists = existing.Any(item =>
                    item.SourceProjectId == seed.SourceProjectId
                    && item.TargetProjectId == seed.TargetProjectId
                    && string.Equals(item.DependencyType, seed.DependencyType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Title, seed.Title, StringComparison.OrdinalIgnoreCase));

                if (exists)
                {
                    continue;
                }

                _context.ProjectInterdependencies.Add(new ProjectInterdependency
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SourceProjectId = seed.SourceProjectId,
                    TargetProjectId = seed.TargetProjectId,
                    DependencyType = seed.DependencyType,
                    Status = seed.Status,
                    ImpactLevel = seed.ImpactLevel,
                    OwnerId = seed.OwnerId,
                    DueDate = seed.DueDate,
                    Title = seed.Title,
                    Description = seed.Description,
                    MitigationPlan = seed.MitigationPlan,
                    CreatedAt = now,
                    CreatedBy = "System"
                });

                changed = true;
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task EnsureProjectDemoQualityDataSeededAsync(
            Guid tenantId,
            ApplicationUser sponsor,
            ApplicationUser financeOwner,
            ApplicationUser teamMember,
            DateTime now)
        {
            var implementationProject = await _context.Projects
                .AsNoTracking()
                .Where(project => project.TenantId == tenantId && !project.IsDeleted && project.ProjectCode == "PRJ-DEMO-1001")
                .Select(project => new { project.Id })
                .FirstOrDefaultAsync();

            if (implementationProject == null)
            {
                return;
            }

            var deliverable = await _context.ProjectDeliverables
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == implementationProject.Id && !item.IsDeleted)
                .OrderBy(item => item.CreatedAt)
                .Select(item => new { item.Id })
                .FirstOrDefaultAsync();

            var workItem = await _context.ProjectWorkItems
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == implementationProject.Id && !item.IsDeleted)
                .OrderByDescending(item => item.NodeType == ProjectWorkItemNodeTypes.Task)
                .ThenBy(item => item.SortOrder)
                .Select(item => new { item.Id })
                .FirstOrDefaultAsync();

            if (!await _context.ProjectQualityCheckpoints.AnyAsync(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.ProjectId == implementationProject.Id
                    && item.Title == "Wave 1 readiness quality review"))
            {
                var checkpointId = Guid.NewGuid();
                _context.ProjectQualityCheckpoints.Add(new ProjectQualityCheckpoint
                {
                    Id = checkpointId,
                    TenantId = tenantId,
                    ProjectId = implementationProject.Id,
                    WorkItemId = workItem?.Id,
                    DeliverableId = deliverable?.Id,
                    QaOwnerId = financeOwner.Id,
                    Title = "Wave 1 readiness quality review",
                    Description = "Confirm configuration evidence, issue disposition, and UAT pack completeness before customer review.",
                    Status = "InReview",
                    DueDate = now.Date.AddDays(6),
                    RequiresQaSignOff = true,
                    CreatedAt = now.AddDays(-4),
                    CreatedBy = "System"
                });

                _context.ProjectNonConformances.Add(new ProjectNonConformance
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = implementationProject.Id,
                    QualityCheckpointId = checkpointId,
                    DeliverableId = deliverable?.Id,
                    OwnerId = teamMember.Id,
                    Title = "Inventory evidence screenshots use obsolete warehouse labels",
                    Description = "The current evidence pack still shows superseded warehouse names in two UAT scenarios.",
                    Severity = "Medium",
                    Status = "Open",
                    TargetResolutionDate = now.Date.AddDays(4),
                    CorrectiveAction = "Refresh screenshots from the approved warehouse master and rerun the affected scripts.",
                    PreventiveAction = "Include evidence-pack validation in the pre-review checklist.",
                    CreatedAt = now.AddDays(-2),
                    CreatedBy = "System"
                });

                await _context.SaveChangesAsync();
            }

            if (!await _context.ProjectQualityCheckpoints.AnyAsync(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.ProjectId == implementationProject.Id
                    && item.Status == "SignedOff"))
            {
                _context.ProjectQualityCheckpoints.Add(new ProjectQualityCheckpoint
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = implementationProject.Id,
                    DeliverableId = deliverable?.Id,
                    QaOwnerId = sponsor.Id,
                    Title = "Design pack approval evidence",
                    Description = "Historical quality checkpoint showing completed QA sign-off on the approved design pack.",
                    Status = "SignedOff",
                    DueDate = now.Date.AddDays(-12),
                    RequiresQaSignOff = true,
                    SignedOffAt = now.AddDays(-11),
                    SignedOffById = sponsor.Id,
                    SignOffNotes = "Approved after design review and control walkthrough.",
                    CreatedAt = now.AddDays(-13),
                    CreatedBy = "System"
                });

                await _context.SaveChangesAsync();
            }
        }

        private async Task EnsureProjectDemoProcurementAndMaterialDataSeededAsync(
            Guid tenantId,
            BusinessPartner customer,
            ApplicationUser financeOwner,
            Department? department,
            DateTime now)
        {
            var implementationProject = await _context.Projects
                .FirstOrDefaultAsync(project => project.TenantId == tenantId && !project.IsDeleted && project.ProjectCode == "PRJ-DEMO-1001");
            if (implementationProject == null)
            {
                return;
            }

            var effectiveDepartment = department
                ?? await _context.Departments
                    .AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
                    .OrderBy(item => item.Name)
                    .FirstOrDefaultAsync();
            if (effectiveDepartment == null)
            {
                _logger.LogWarning("Skipping project demo procurement/material seed for tenant {TenantId} because no active department was found.", tenantId);
                return;
            }

            var category = await _context.InventoryCategories
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == "PROJECT-DEMO");
            if (category == null)
            {
                category = new InventoryCategory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = "Project Demo Materials",
                    Code = "PROJECT-DEMO",
                    Description = "Seeded category for project procurement and material flows.",
                    IsActive = true,
                    DefaultUnitOfMeasure = "EA",
                    CreatedAt = now,
                    CreatedBy = "System"
                };
                _context.InventoryCategories.Add(category);
            }

            var warehouse = await _context.Warehouses
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == "DEMO-PM");
            if (warehouse == null)
            {
                warehouse = new Warehouse
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = "Project Demo Warehouse",
                    Code = "DEMO-PM",
                    Description = "Seeded warehouse for project material demonstrations.",
                    IsActive = true,
                    WarehouseType = "Standard",
                    CreatedAt = now,
                    CreatedBy = "System"
                };
                _context.Warehouses.Add(warehouse);
            }

            var inventoryItem = await _context.InventoryItems
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.ItemCode == "PM-BARCODE-DEVICE");
            if (inventoryItem == null)
            {
                inventoryItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemCode = "PM-BARCODE-DEVICE",
                    Name = "Barcode Device Kit",
                    Description = "Seeded device kit for project material and procurement flows.",
                    CategoryId = category.Id,
                    UnitOfMeasure = "EA",
                    StandardCost = 750m,
                    AverageCost = 750m,
                    LastPurchaseCost = 750m,
                    CurrentStock = 25m,
                    AvailableStock = 20m,
                    CreatedAt = now,
                    CreatedBy = "System"
                };
                _context.InventoryItems.Add(inventoryItem);
            }

            await _context.SaveChangesAsync();

            if (!await _context.PurchaseRequisitions.AnyAsync(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.ProjectId == implementationProject.Id
                    && item.RequisitionNumber == "PR-DEMO-1001-A"))
            {
                _context.PurchaseRequisitions.Add(new PurchaseRequisition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RequisitionNumber = "PR-DEMO-1001-A",
                    RequisitionDate = now.AddDays(-6),
                    RequestedById = financeOwner.Id,
                    RequiredDate = now.Date.AddDays(9),
                    Status = "Submitted",
                    Priority = "High",
                    Department = effectiveDepartment.Name,
                    Justification = "Additional label printers required before cutover readiness.",
                    RequisitionType = PurchaseRequisitionType.ProjectPurchase,
                    ProjectId = implementationProject.Id,
                    ProjectCode = implementationProject.ProjectCode,
                    ProjectName = implementationProject.Title,
                    Currency = "USD",
                    PreferredBusinessPartnerId = customer.Id,
                    TotalAmount = 3200m,
                    CreatedAt = now.AddDays(-6),
                    CreatedBy = "System"
                });
            }

            if (!await _context.PurchaseRequisitions.AnyAsync(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.ProjectId == implementationProject.Id
                    && item.RequisitionNumber == "PR-DEMO-1001-B"))
            {
                var orderedPrId = Guid.NewGuid();
                var purchaseOrderId = Guid.NewGuid();
                var purchaseOrderItemId = Guid.NewGuid();
                var receiptId = Guid.NewGuid();

                _context.PurchaseRequisitions.Add(new PurchaseRequisition
                {
                    Id = orderedPrId,
                    TenantId = tenantId,
                    RequisitionNumber = "PR-DEMO-1001-B",
                    RequisitionDate = now.AddDays(-12),
                    RequestedById = financeOwner.Id,
                    RequiredDate = now.Date.AddDays(4),
                    Status = "Ordered",
                    Priority = "High",
                    Department = effectiveDepartment.Name,
                    Justification = "Mobile scanners needed for inventory validation and warehouse cutover.",
                    RequisitionType = PurchaseRequisitionType.ProjectPurchase,
                    ProjectId = implementationProject.Id,
                    ProjectCode = implementationProject.ProjectCode,
                    ProjectName = implementationProject.Title,
                    Currency = "USD",
                    PreferredBusinessPartnerId = customer.Id,
                    ApprovedById = financeOwner.Id,
                    ApprovedAt = now.AddDays(-11),
                    TotalAmount = 4500m,
                    CreatedAt = now.AddDays(-12),
                    CreatedBy = "System"
                });

                _context.PurchaseOrders.Add(new PurchaseOrder
                {
                    Id = purchaseOrderId,
                    TenantId = tenantId,
                    OrderNumber = "PO-DEMO-1001-01",
                    BusinessPartnerId = customer.Id,
                    OrderDate = now.AddDays(-10),
                    RequiredDate = now.Date.AddDays(3),
                    Status = "PartiallyReceived",
                    RequestedById = financeOwner.Id,
                    ApprovedById = financeOwner.Id,
                    ApprovedAt = now.AddDays(-10),
                    SubTotal = 4500m,
                    TotalAmount = 4500m,
                    SourceRequisitionId = orderedPrId,
                    SourceRequisitionNumber = "PR-DEMO-1001-B",
                    CreatedAt = now.AddDays(-10),
                    CreatedBy = "System"
                });

                _context.PurchaseOrderItems.Add(new PurchaseOrderItem
                {
                    Id = purchaseOrderItemId,
                    TenantId = tenantId,
                    PurchaseOrderId = purchaseOrderId,
                    InventoryItemId = inventoryItem.Id,
                    ItemDescription = "Barcode Device Kit",
                    OrderedQuantity = 6m,
                    ReceivedQuantity = 6m,
                    RemainingQuantity = 0m,
                    UnitOfMeasure = "EA",
                    UnitPrice = 750m,
                    LineTotal = 4500m,
                    LandedUnitCost = 750m,
                    CreatedAt = now.AddDays(-10),
                    CreatedBy = "System"
                });

                _context.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
                {
                    Id = receiptId,
                    TenantId = tenantId,
                    PurchaseOrderId = purchaseOrderId,
                    ReceiptNumber = "RCV-DEMO-1001-01",
                    ReceiptDate = now.AddDays(-2),
                    Status = "Received",
                    ReceivedById = financeOwner.Id,
                    RequiresInspection = true,
                    Notes = "Devices received and awaiting final inspection before deployment.",
                    CreatedAt = now.AddDays(-2),
                    CreatedBy = "System"
                });

                _context.PurchaseOrderReceiptItems.Add(new PurchaseOrderReceiptItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ReceiptId = receiptId,
                    PurchaseOrderItemId = purchaseOrderItemId,
                    ReceivedQuantity = 6m,
                    AcceptedQuantity = 6m,
                    UnitOfMeasure = "EA",
                    CreatedAt = now.AddDays(-2),
                    CreatedBy = "System"
                });
            }

            if (!await _context.InventoryRequisitions.AnyAsync(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.ProjectId == implementationProject.Id
                    && item.RequisitionNumber == "IR-DEMO-1001-01"))
            {
                _context.InventoryRequisitions.Add(new InventoryRequisition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RequisitionNumber = "IR-DEMO-1001-01",
                    Description = "Issue barcode devices to the project delivery team for customer validation.",
                    DepartmentId = effectiveDepartment.Id,
                    DepartmentName = effectiveDepartment.Name,
                    WarehouseId = warehouse.Id,
                    ProjectId = implementationProject.Id,
                    ProjectCode = implementationProject.ProjectCode,
                    RequestDate = now.AddDays(-3),
                    RequiredDate = now.Date.AddDays(-1),
                    ApprovalDate = now.AddDays(-3),
                    IssuedDate = now.AddDays(-2),
                    Status = RequisitionStatus.PartiallyIssued,
                    RequisitionType = RequisitionType.ProjectRequisition,
                    Priority = "High",
                    RequestedById = financeOwner.Id,
                    ApprovedById = financeOwner.Id,
                    IssuedById = financeOwner.Id,
                    TotalItems = 1,
                    TotalQuantity = 8m,
                    TotalValue = 6000m,
                    Purpose = "Seeded project material issue for inventory reconciliation and cost tracking.",
                    CreatedAt = now.AddDays(-3),
                    CreatedBy = "System",
                    Items =
                    {
                        new InventoryRequisitionItem
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            InventoryItemId = inventoryItem.Id,
                            ItemCode = inventoryItem.ItemCode,
                            ItemName = inventoryItem.Name,
                            RequestedQuantity = 8m,
                            ApprovedQuantity = 8m,
                            IssuedQuantity = 8m,
                            UnitCost = 750m,
                            LineValue = 6000m,
                            UnitOfMeasure = "EA",
                            CreatedAt = now.AddDays(-3),
                            CreatedBy = "System"
                        }
                    }
                });
            }

            if (!await _context.ProjectExpenses.AnyAsync(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.ProjectId == implementationProject.Id
                    && item.Category == "Materials"
                    && item.Notes == "Seeded material consumption posting for project reconciliation."))
            {
                _context.ProjectExpenses.Add(new ProjectExpense
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = implementationProject.Id,
                    UserId = financeOwner.Id,
                    ExpenseDate = now.AddDays(-2),
                    Category = "Materials",
                    Currency = "USD",
                    Amount = 3500m,
                    TaxAmount = 0m,
                    IsBillable = true,
                    Status = "Approved",
                    Notes = "Seeded material consumption posting for project reconciliation.",
                    ApprovedById = financeOwner.Id,
                    ApprovedAt = now.AddDays(-2),
                    CreatedAt = now.AddDays(-2),
                    CreatedBy = "System"
                });
            }

            if (!await _context.ProjectExpenses.AnyAsync(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.ProjectId == implementationProject.Id
                    && item.Category == "Materials"
                    && item.Notes == "Seeded material return adjustment for project reconciliation."))
            {
                _context.ProjectExpenses.Add(new ProjectExpense
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = implementationProject.Id,
                    UserId = financeOwner.Id,
                    ExpenseDate = now.AddDays(-1),
                    Category = "Materials",
                    Currency = "USD",
                    Amount = -500m,
                    TaxAmount = 0m,
                    IsBillable = false,
                    Status = "Approved",
                    Notes = "Seeded material return adjustment for project reconciliation.",
                    ApprovedById = financeOwner.Id,
                    ApprovedAt = now.AddDays(-1),
                    CreatedAt = now.AddDays(-1),
                    CreatedBy = "System"
                });
            }

            await _context.SaveChangesAsync();
        }

        private void CreateImplementationProject(
            Guid tenantId,
            ProjectType implementationType,
            ProjectPriority highPriority,
            ProjectTemplate template,
            ProjectPortfolio portfolio,
            ProjectProgram program,
            ApplicationUser sponsor,
            ApplicationUser projectManager,
            ApplicationUser financeOwner,
            ApplicationUser teamMember,
            Department? department,
            Location? location,
            BusinessPartner customer,
            DateTime now)
        {
            var projectId = Guid.NewGuid();
            var phaseId = Guid.NewGuid();
            var designTaskId = Guid.NewGuid();
            var buildTaskId = Guid.NewGuid();
            var checklistId = Guid.NewGuid();
            var milestoneId = Guid.NewGuid();
            var deliverableId = Guid.NewGuid();
            var qualityCheckpointId = Guid.NewGuid();
            var nonConformanceId = Guid.NewGuid();
            var scheduleId = Guid.NewGuid();
            var invoiceRequestId = Guid.NewGuid();
            var meetingId = Guid.NewGuid();

            var project = new Project
            {
                Id = projectId,
                TenantId = tenantId,
                ProjectCode = "PRJ-DEMO-1001",
                Title = "Northwind ERP Rollout",
                Summary = "Customer-facing ERP implementation covering finance, inventory, procurement, and reporting.",
                BusinessCase = "Replace fragmented legacy tools with a governed ERP delivery model and faster billing cadence.",
                Objectives = "Deliver core ERP modules, train users, and transition the customer to support with auditable controls.",
                StrategicAlignment = "Customer delivery excellence",
                ProjectTypeId = implementationType.Id,
                ProjectPriorityId = highPriority.Id,
                TemplateId = template.Id,
                PortfolioId = portfolio.Id,
                ProgramId = program.Id,
                Status = ProjectStatuses.InProgress,
                Methodology = "Hybrid",
                SponsorId = sponsor.Id,
                ProjectManagerId = projectManager.Id,
                DepartmentId = department?.Id,
                LocationId = location?.Id,
                CustomerId = customer.Id,
                BusinessPartnerId = customer.Id,
                StartDate = now.Date.AddDays(-45),
                TargetEndDate = now.Date.AddDays(75),
                ActualStartDate = now.Date.AddDays(-42),
                EstimatedBudget = 180000m,
                ApprovedBudget = 195000m,
                ActualCost = 28750m,
                BudgetStatus = "Approved",
                ProgressPercent = 58m,
                ApprovalRequired = true,
                SubmittedAt = now.AddDays(-55),
                ApprovedAt = now.AddDays(-52),
                ScopeStatement = "Finance, procurement, inventory, dashboards, and controlled customer collaboration.",
                Assumptions = "Core customer team remains available for design reviews and UAT.",
                Constraints = "Go-live must align with quarter-end controls and contract milestones.",
                ExpectedBenefits = "Faster close cycle, stronger inventory control, and earlier invoice generation.",
                FundingSource = "Customer Contract",
                StatusRemarks = "Build and UAT are running in parallel for the first wave.",
                ExternalPortalAccessEnabled = true,
                ExternalCollaborationEnabled = true,
                CreatedAt = now.AddDays(-60),
                CreatedBy = "System"
            };

            _context.Projects.Add(project);
            _context.ProjectInitiationVersions.Add(new ProjectInitiationVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                VersionNumber = 1,
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    project.ProjectCode,
                    project.Title,
                    project.Status,
                    project.EstimatedBudget,
                    project.StartDate,
                    project.TargetEndDate
                }),
                ChangeType = "Approved",
                Notes = "Initial approved initiation pack.",
                CreatedAt = now.AddDays(-52),
                CreatedBy = "System"
            });

            _context.ProjectMembers.AddRange(
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = sponsor.Id,
                    Role = "Sponsor",
                    JoinedAt = now.AddDays(-60),
                    CreatedAt = now.AddDays(-60),
                    CreatedBy = "System"
                },
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = projectManager.Id,
                    Role = "ProjectManager",
                    JoinedAt = now.AddDays(-58),
                    CreatedAt = now.AddDays(-58),
                    CreatedBy = "System"
                },
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = financeOwner.Id,
                    Role = "FinanceLead",
                    JoinedAt = now.AddDays(-57),
                    CreatedAt = now.AddDays(-57),
                    CreatedBy = "System"
                },
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = teamMember.Id,
                    Role = "FunctionalConsultant",
                    JoinedAt = now.AddDays(-57),
                    CreatedAt = now.AddDays(-57),
                    CreatedBy = "System"
                });

            _context.ProjectWorkItems.AddRange(
                new ProjectWorkItem
                {
                    Id = phaseId,
                    TenantId = tenantId,
                    ProjectId = projectId,
                    NodeType = ProjectWorkItemNodeTypes.Phase,
                    Title = "Solution Design and Build",
                    Description = "Approved design, configuration, integration, and test preparation.",
                    Status = "InProgress",
                    Priority = "High",
                    SortOrder = 10,
                    AssignedToUserId = projectManager.Id,
                    PlannedStartDate = now.Date.AddDays(-40),
                    PlannedEndDate = now.Date.AddDays(30),
                    ActualStartDate = now.Date.AddDays(-38),
                    PercentComplete = 65m,
                    IsRollupEnabled = true,
                    CreatedAt = now.AddDays(-50),
                    CreatedBy = "System"
                },
                new ProjectWorkItem
                {
                    Id = designTaskId,
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ParentId = phaseId,
                    NodeType = ProjectWorkItemNodeTypes.Task,
                    Title = "Finalize finance and procurement design",
                    Description = "Complete workshops, confirm controls, and sign off data mappings.",
                    Status = "Completed",
                    Priority = "High",
                    SortOrder = 20,
                    AssignedToUserId = projectManager.Id,
                    PlannedStartDate = now.Date.AddDays(-35),
                    PlannedEndDate = now.Date.AddDays(-12),
                    ActualStartDate = now.Date.AddDays(-34),
                    ActualEndDate = now.Date.AddDays(-10),
                    PercentComplete = 100m,
                    EffortEstimateHours = 72m,
                    ActualEffortHours = 76m,
                    CreatedAt = now.AddDays(-48),
                    CreatedBy = "System"
                },
                new ProjectWorkItem
                {
                    Id = buildTaskId,
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ParentId = phaseId,
                    NodeType = ProjectWorkItemNodeTypes.Task,
                    Title = "Configure integrations and prepare UAT",
                    Description = "Configure workflows, finance handoff, and customer-facing deliverables.",
                    Status = "InProgress",
                    Priority = "High",
                    SortOrder = 30,
                    AssignedToUserId = teamMember.Id,
                    PlannedStartDate = now.Date.AddDays(-9),
                    PlannedEndDate = now.Date.AddDays(20),
                    ActualStartDate = now.Date.AddDays(-8),
                    PercentComplete = 55m,
                    EffortEstimateHours = 140m,
                    ActualEffortHours = 82m,
                    CreatedAt = now.AddDays(-20),
                    CreatedBy = "System"
                },
                new ProjectWorkItem
                {
                    Id = checklistId,
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ParentId = buildTaskId,
                    NodeType = ProjectWorkItemNodeTypes.ChecklistItem,
                    Title = "Approve UAT script pack",
                    Status = "Assigned",
                    Priority = "Normal",
                    SortOrder = 40,
                    AssignedToUserId = financeOwner.Id,
                    PlannedStartDate = now.Date.AddDays(2),
                    PlannedEndDate = now.Date.AddDays(6),
                    PercentComplete = 0m,
                    CreatedAt = now.AddDays(-5),
                    CreatedBy = "System"
                });

            _context.ProjectTaskDependencies.Add(new ProjectTaskDependency
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                PredecessorWorkItemId = designTaskId,
                SuccessorWorkItemId = buildTaskId,
                DependencyType = "FS",
                LagDays = 1,
                IsEnforced = true,
                CreatedAt = now.AddDays(-15),
                CreatedBy = "System"
            });

            _context.ProjectMilestones.Add(new ProjectMilestone
            {
                Id = milestoneId,
                TenantId = tenantId,
                ProjectId = projectId,
                WorkItemId = buildTaskId,
                Title = "Wave 1 UAT readiness",
                Description = "Configuration, integrations, scripts, and access are ready for customer validation.",
                TargetDate = now.Date.AddDays(12),
                Status = "InReview",
                RequiresApproval = true,
                CreatedAt = now.AddDays(-10),
                CreatedBy = "System"
            });

            _context.ProjectDeliverables.Add(new ProjectDeliverable
            {
                Id = deliverableId,
                TenantId = tenantId,
                ProjectId = projectId,
                WorkItemId = buildTaskId,
                MilestoneId = milestoneId,
                Title = "Wave 1 solution configuration pack",
                Description = "Approved configuration workbook and evidence package for customer review.",
                Status = "InReview",
                TargetDate = now.Date.AddDays(10),
                ExternalSubmissionAllowed = true,
                ExternalSignOffRequired = true,
                IsExternalVisible = true,
                CreatedAt = now.AddDays(-8),
                CreatedBy = "System"
            });

            _context.ProjectResourceAllocations.AddRange(
                new ProjectResourceAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    WorkItemId = buildTaskId,
                    UserId = projectManager.Id,
                    AllocationRole = "Project Manager",
                    AllocationType = "Hours",
                    AllocationValue = 120m,
                    PlannedHours = 120m,
                    StartDate = now.Date.AddDays(-40),
                    EndDate = now.Date.AddDays(20),
                    BookingType = "Hard",
                    Status = "Approved",
                    ApprovedById = sponsor.Id,
                    ApprovedAt = now.AddDays(-40),
                    CreatedAt = now.AddDays(-40),
                    CreatedBy = "System"
                },
                new ProjectResourceAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    WorkItemId = buildTaskId,
                    UserId = teamMember.Id,
                    AllocationRole = "Functional Lead",
                    AllocationType = "Hours",
                    AllocationValue = 160m,
                    PlannedHours = 160m,
                    StartDate = now.Date.AddDays(-20),
                    EndDate = now.Date.AddDays(25),
                    BookingType = "Hard",
                    Status = "Approved",
                    ApprovedById = sponsor.Id,
                    ApprovedAt = now.AddDays(-20),
                    CreatedAt = now.AddDays(-20),
                    CreatedBy = "System"
                });

            _context.ProjectRisks.Add(new ProjectRisk
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Customer test data not signed off on schedule",
                Description = "Delayed data confirmation may push UAT and billing milestones.",
                OwnerId = projectManager.Id,
                Status = "Monitoring",
                Category = "Schedule",
                Probability = 3,
                Impact = 4,
                Exposure = 12,
                ResponseStrategy = "Mitigate",
                MitigationPlan = "Run daily sign-off stand-up and pre-approve fallback data sets.",
                DueDate = now.Date.AddDays(7),
                CreatedAt = now.AddDays(-7),
                CreatedBy = "System"
            });

            _context.ProjectIssues.Add(new ProjectIssue
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Procurement approval for barcode devices is pending",
                Description = "Receiving and device testing depend on the approved purchase order.",
                OwnerId = financeOwner.Id,
                Status = "Open",
                Severity = "High",
                TargetResolutionDate = now.Date.AddDays(5),
                RootCause = "Late vendor quotation alignment.",
                CorrectiveAction = "Escalate commercial approval and maintain substitute loaner stock.",
                CreatedAt = now.AddDays(-3),
                CreatedBy = "System"
            });

            _context.ProjectQualityCheckpoints.Add(new ProjectQualityCheckpoint
            {
                Id = qualityCheckpointId,
                TenantId = tenantId,
                ProjectId = projectId,
                WorkItemId = buildTaskId,
                DeliverableId = deliverableId,
                QaOwnerId = financeOwner.Id,
                Title = "Wave 1 readiness quality review",
                Description = "Confirm configuration evidence, issue disposition, and UAT pack completeness before customer review.",
                Status = "InReview",
                DueDate = now.Date.AddDays(6),
                RequiresQaSignOff = true,
                CreatedAt = now.AddDays(-4),
                CreatedBy = "System"
            });

            _context.ProjectNonConformances.Add(new ProjectNonConformance
            {
                Id = nonConformanceId,
                TenantId = tenantId,
                ProjectId = projectId,
                QualityCheckpointId = qualityCheckpointId,
                DeliverableId = deliverableId,
                OwnerId = teamMember.Id,
                Title = "Inventory evidence screenshots use obsolete warehouse labels",
                Description = "The current evidence pack still shows superseded warehouse names in two UAT scenarios.",
                Severity = "Medium",
                Status = "Open",
                TargetResolutionDate = now.Date.AddDays(4),
                CorrectiveAction = "Refresh screenshots from the approved warehouse master and rerun the affected scripts.",
                PreventiveAction = "Include evidence-pack validation in the pre-review checklist.",
                CreatedAt = now.AddDays(-2),
                CreatedBy = "System"
            });

            _context.ProjectChangeRequests.Add(new ProjectChangeRequest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Add supplier scorecard dashboard to wave 1",
                Description = "Customer requested a lightweight reporting addition before cutover.",
                ChangeType = "Scope",
                Status = "PendingApproval",
                BusinessImpact = "Improves stakeholder adoption and executive reporting.",
                RiskImpact = "Requires additional test effort but limited implementation risk.",
                CostImpact = 8500m,
                ScheduleImpactDays = 4,
                CreatedAt = now.AddDays(-2),
                CreatedBy = "System"
            });

            _context.ProjectBillingSchedules.Add(new ProjectBillingSchedule
            {
                Id = scheduleId,
                TenantId = tenantId,
                ProjectId = projectId,
                MilestoneId = milestoneId,
                Name = "Wave 1 readiness billing",
                BillingType = "Milestone",
                Amount = 45000m,
                BillingPercentage = 25m,
                BillingDate = now.Date.AddDays(14),
                Status = "Ready",
                Description = "Invoice on confirmed UAT readiness and customer review pack.",
                IsBillable = true,
                CreatedAt = now.AddDays(-1),
                CreatedBy = "System"
            });

            _context.ProjectInvoiceRequests.Add(new ProjectInvoiceRequest
            {
                Id = invoiceRequestId,
                TenantId = tenantId,
                ProjectId = projectId,
                BillingScheduleId = scheduleId,
                RequestNumber = "INVREQ-PRJ-DEMO-1001-01",
                RequestedAmount = 45000m,
                Currency = "USD",
                Status = "SentToFinance",
                RequestedAt = now.AddDays(-1),
                SubmittedAt = now.AddDays(-1),
                ExternalReference = "AR-WAVE1-QUEUE",
                Notes = "Submitted to finance for milestone billing review.",
                CreatedAt = now.AddDays(-1),
                CreatedBy = "System"
            });

            _context.ProjectTimesheetEntries.AddRange(
                new ProjectTimesheetEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    WorkItemId = buildTaskId,
                    UserId = projectManager.Id,
                    EntryDate = now.Date.AddDays(-2),
                    Hours = 8m,
                    IsBillable = true,
                    HourlyRate = 125m,
                    CostAmount = 1000m,
                    WorkType = "Project Management",
                    Notes = "Wave planning, issue coordination, and milestone review.",
                    Status = "Approved",
                    ApprovedById = sponsor.Id,
                    ApprovedAt = now.AddDays(-1),
                    CreatedAt = now.AddDays(-2),
                    CreatedBy = "System"
                },
                new ProjectTimesheetEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    WorkItemId = buildTaskId,
                    UserId = teamMember.Id,
                    EntryDate = now.Date.AddDays(-2),
                    Hours = 14m,
                    IsBillable = true,
                    HourlyRate = 95m,
                    CostAmount = 1330m,
                    WorkType = "Configuration",
                    Notes = "Configuration and test pack completion.",
                    Status = "Approved",
                    ApprovedById = projectManager.Id,
                    ApprovedAt = now.AddDays(-1),
                    CreatedAt = now.AddDays(-2),
                    CreatedBy = "System"
                });

            _context.ProjectExpenses.Add(new ProjectExpense
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                WorkItemId = buildTaskId,
                UserId = financeOwner.Id,
                ExpenseDate = now.Date.AddDays(-4),
                Category = "Travel",
                Currency = "USD",
                Amount = 1420m,
                TaxAmount = 80m,
                IsBillable = true,
                Status = "Approved",
                Notes = "Customer design workshop travel and accommodation.",
                ApprovedById = sponsor.Id,
                ApprovedAt = now.AddDays(-3),
                CreatedAt = now.AddDays(-4),
                CreatedBy = "System"
            });

            _context.ProjectRevenueRecognitions.Add(new ProjectRevenueRecognition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                InvoiceRequestId = invoiceRequestId,
                RecognitionPeriod = $"{now:yyyy-MM}",
                RecognizedRevenue = 45000m,
                RecognizedCost = 28750m,
                GrossMargin = 16250m,
                CashCollected = 0m,
                Status = "Submitted",
                Notes = "Current period recognition for wave 1 billing request.",
                CreatedAt = now,
                CreatedBy = "System"
            });

            _context.ProjectBudgetRevisions.Add(new ProjectBudgetRevision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                VersionNumber = 1,
                RevisionName = "Approved Delivery Baseline",
                RevisionType = "Baseline",
                EstimatedBudget = 180000m,
                ApprovedBudget = 195000m,
                CommittedCost = 84500m,
                ForecastCost = 186500m,
                ThresholdWarningPercent = 75m,
                ThresholdCriticalPercent = 90m,
                Status = "Approved",
                EffectiveDate = now.Date.AddDays(-52),
                SubmittedAt = now.AddDays(-53),
                ApprovedAt = now.AddDays(-52),
                ApprovedById = sponsor.Id,
                ChangeReason = "Approved commercial baseline after design sign-off.",
                Notes = "Use as the cost control baseline for wave 1 delivery.",
                CreatedAt = now.AddDays(-53),
                CreatedBy = "System"
            });

            _context.ProjectForecastVersions.Add(new ProjectForecastVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                VersionNumber = 1,
                VersionName = "Current working forecast",
                AsOfDate = now.Date,
                ForecastCost = 186500m,
                EstimateAtCompletion = 186500m,
                ForecastRevenue = 240000m,
                ForecastMargin = 53500m,
                IsActive = true,
                Notes = "Reflects current milestone billing and pending scope addition.",
                CreatedAt = now,
                CreatedBy = "System"
            });

            _context.ProjectBaselines.Add(new ProjectBaseline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Name = "Approved baseline",
                Notes = "Baseline after commercial approval and initial plan sign-off.",
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    project.ProjectCode,
                    ApprovedBudget = 195000m,
                    FinishDate = now.Date.AddDays(75),
                    ProgressPercent = 0m
                }),
                IsLocked = true,
                CreatedOn = now.AddDays(-52),
                CreatedAt = now.AddDays(-52),
                CreatedBy = "System"
            });

            _context.ProjectDocuments.Add(new ProjectDocument
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                DocumentName = "Wave 1 design sign-off",
                Category = "Plan",
                DocumentType = "PDF",
                FilePath = "/seed/projects/PRJ-DEMO-1001/design-signoff.pdf",
                FileType = "application/pdf",
                FileSize = 124000,
                VersionLabel = "1.0",
                Status = "Approved",
                EffectiveDate = now.Date.AddDays(-12),
                IsExternalVisible = true,
                CreatedAt = now.AddDays(-12),
                CreatedBy = "System"
            });

            _context.ProjectComments.Add(new ProjectComment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                WorkItemId = buildTaskId,
                CommentType = "Status",
                Body = "Wave 1 build is on track. UAT content is ready for customer review after finance and inventory validation.",
                CreatedAt = now.AddDays(-1),
                CreatedBy = "System"
            });

            _context.ProjectDecisions.Add(new ProjectDecision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Approve hybrid rollout approach for customer UAT",
                DecisionDate = now.AddDays(-16),
                ApproverId = sponsor.Id,
                Rationale = "Allows early validation of finance processes while inventory hardware procurement completes.",
                AlternativesConsidered = "Wait for full device readiness before UAT start.",
                ImpactSummary = "Protects delivery date while limiting scope risk.",
                Status = "Approved",
                ApprovedAt = now.AddDays(-16),
                CreatedAt = now.AddDays(-16),
                CreatedBy = "System"
            });

            _context.ProjectMeetingMinutes.Add(new ProjectMeetingMinute
            {
                Id = meetingId,
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Weekly customer delivery review",
                MeetingDate = now.AddDays(-3),
                FacilitatorId = projectManager.Id,
                MeetingType = "Status",
                Minutes = "Reviewed milestone progress, open procurement blocker, and billing readiness.",
                AttendeesJson = JsonSerializer.Serialize(new[] { sponsor.Email, projectManager.Email, financeOwner.Email, teamMember.Email }),
                CreatedAt = now.AddDays(-3),
                CreatedBy = "System"
            });

            _context.ProjectActionItems.Add(new ProjectActionItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                MeetingMinuteId = meetingId,
                WorkItemId = buildTaskId,
                Title = "Close barcode device procurement blocker",
                Description = "Confirm approved PO and update deployment readiness.",
                OwnerId = financeOwner.Id,
                DueDate = now.Date.AddDays(4),
                Status = "Open",
                Priority = "High",
                CreatedAt = now.AddDays(-3),
                CreatedBy = "System"
            });

            _context.ProjectExternalAccessPolicies.Add(new ProjectExternalAccessPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                BusinessPartnerId = customer.Id,
                ArtifactType = "Project",
                AccessLevel = "Contribute",
                CanComment = true,
                CanUpload = true,
                CanApprove = true,
                Notes = "Customer PMO can review deliverables and approve sign-off artifacts.",
                CreatedAt = now.AddDays(-30),
                CreatedBy = "System"
            });
        }

        private void CreateInternalPlanningProject(
            Guid tenantId,
            ProjectType internalType,
            ProjectPriority mediumPriority,
            ProjectTemplate template,
            ProjectPortfolio portfolio,
            ProjectProgram program,
            ApplicationUser sponsor,
            ApplicationUser projectManager,
            ApplicationUser financeOwner,
            Department? department,
            Location? location,
            DateTime now)
        {
            var projectId = Guid.NewGuid();
            var phaseId = Guid.NewGuid();
            var planningTaskId = Guid.NewGuid();

            var project = new Project
            {
                Id = projectId,
                TenantId = tenantId,
                ProjectCode = "PRJ-DEMO-1002",
                Title = "Internal Planning and PMO Rollout",
                Summary = "Internal project to operationalize project governance, templates, and reporting cadence.",
                BusinessCase = "Standardize delivery controls across departments before portfolio expansion.",
                Objectives = "Establish templates, approval cadence, dashboards, and PMO reporting routines.",
                StrategicAlignment = "Operational control and reporting",
                ProjectTypeId = internalType.Id,
                ProjectPriorityId = mediumPriority.Id,
                TemplateId = template.Id,
                PortfolioId = portfolio.Id,
                ProgramId = program.Id,
                Status = ProjectStatuses.Planned,
                Methodology = "Waterfall",
                SponsorId = sponsor.Id,
                ProjectManagerId = projectManager.Id,
                DepartmentId = department?.Id,
                LocationId = location?.Id,
                StartDate = now.Date.AddDays(14),
                TargetEndDate = now.Date.AddDays(120),
                EstimatedBudget = 60000m,
                ApprovedBudget = 60000m,
                ActualCost = 0m,
                BudgetStatus = "Approved",
                ProgressPercent = 12m,
                ApprovalRequired = true,
                SubmittedAt = now.AddDays(-10),
                ApprovedAt = now.AddDays(-8),
                ScopeStatement = "Setup, training, governance, and reporting enablement for internal project control.",
                Assumptions = "PMO and delivery leads will allocate time for operating model workshops.",
                Constraints = "Implementation must avoid quarter-end reporting freeze periods.",
                ExpectedBenefits = "Consistent setup, faster approvals, and clearer portfolio visibility.",
                FundingSource = "Internal Budget",
                StatusRemarks = "Planning baseline ready for kickoff.",
                CreatedAt = now.AddDays(-12),
                CreatedBy = "System"
            };

            _context.Projects.Add(project);
            _context.ProjectInitiationVersions.Add(new ProjectInitiationVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                VersionNumber = 1,
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    project.ProjectCode,
                    project.Title,
                    project.Status,
                    project.ApprovedBudget
                }),
                ChangeType = "Approved",
                Notes = "Initial approved operating model setup plan.",
                CreatedAt = now.AddDays(-8),
                CreatedBy = "System"
            });

            _context.ProjectMembers.AddRange(
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = sponsor.Id,
                    Role = "Sponsor",
                    JoinedAt = now.AddDays(-12),
                    CreatedAt = now.AddDays(-12),
                    CreatedBy = "System"
                },
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = projectManager.Id,
                    Role = "ProjectManager",
                    JoinedAt = now.AddDays(-11),
                    CreatedAt = now.AddDays(-11),
                    CreatedBy = "System"
                },
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = financeOwner.Id,
                    Role = "PMOAnalyst",
                    JoinedAt = now.AddDays(-11),
                    CreatedAt = now.AddDays(-11),
                    CreatedBy = "System"
                });

            _context.ProjectWorkItems.AddRange(
                new ProjectWorkItem
                {
                    Id = phaseId,
                    TenantId = tenantId,
                    ProjectId = projectId,
                    NodeType = ProjectWorkItemNodeTypes.Phase,
                    Title = "PMO Operating Model Setup",
                    Status = "Planned",
                    Priority = "Medium",
                    SortOrder = 10,
                    AssignedToUserId = projectManager.Id,
                    PlannedStartDate = now.Date.AddDays(14),
                    PlannedEndDate = now.Date.AddDays(70),
                    PercentComplete = 0m,
                    CreatedAt = now.AddDays(-9),
                    CreatedBy = "System"
                },
                new ProjectWorkItem
                {
                    Id = planningTaskId,
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ParentId = phaseId,
                    NodeType = ProjectWorkItemNodeTypes.Task,
                    Title = "Finalize portfolio reporting templates",
                    Status = "Assigned",
                    Priority = "Medium",
                    SortOrder = 20,
                    AssignedToUserId = financeOwner.Id,
                    PlannedStartDate = now.Date.AddDays(16),
                    PlannedEndDate = now.Date.AddDays(28),
                    PercentComplete = 20m,
                    EffortEstimateHours = 40m,
                    ActualEffortHours = 8m,
                    CreatedAt = now.AddDays(-8),
                    CreatedBy = "System"
                });

            _context.ProjectMilestones.Add(new ProjectMilestone
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                WorkItemId = planningTaskId,
                Title = "Planning baseline approved",
                TargetDate = now.Date.AddDays(21),
                Status = "Planned",
                RequiresApproval = true,
                CreatedAt = now.AddDays(-7),
                CreatedBy = "System"
            });

            _context.ProjectResourceAllocations.Add(new ProjectResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                WorkItemId = planningTaskId,
                UserId = financeOwner.Id,
                AllocationRole = "PMO Analyst",
                AllocationType = "Hours",
                AllocationValue = 40m,
                PlannedHours = 40m,
                StartDate = now.Date.AddDays(15),
                EndDate = now.Date.AddDays(29),
                BookingType = "Soft",
                Status = "Approved",
                ApprovedById = sponsor.Id,
                ApprovedAt = now.AddDays(-7),
                CreatedAt = now.AddDays(-7),
                CreatedBy = "System"
            });

            _context.ProjectBudgetRevisions.Add(new ProjectBudgetRevision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                VersionNumber = 1,
                RevisionName = "Initial approved budget",
                RevisionType = "Baseline",
                EstimatedBudget = 60000m,
                ApprovedBudget = 60000m,
                CommittedCost = 12000m,
                ForecastCost = 57500m,
                Status = "Approved",
                EffectiveDate = now.Date.AddDays(-8),
                SubmittedAt = now.AddDays(-9),
                ApprovedAt = now.AddDays(-8),
                ApprovedById = sponsor.Id,
                ChangeReason = "Initial PMO planning baseline.",
                CreatedAt = now.AddDays(-9),
                CreatedBy = "System"
            });

            _context.ProjectForecastVersions.Add(new ProjectForecastVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                VersionNumber = 1,
                VersionName = "Planning forecast",
                AsOfDate = now.Date,
                ForecastCost = 57500m,
                EstimateAtCompletion = 57500m,
                ForecastRevenue = 0m,
                ForecastMargin = -57500m,
                IsActive = true,
                Notes = "Internal initiative with no external billing.",
                CreatedAt = now,
                CreatedBy = "System"
            });

            _context.ProjectBaselines.Add(new ProjectBaseline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Name = "Planning baseline",
                Notes = "Initial planning and staffing baseline.",
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    project.ProjectCode,
                    ApprovedBudget = 60000m,
                    FinishDate = now.Date.AddDays(120),
                    ProgressPercent = 0m
                }),
                IsLocked = true,
                CreatedOn = now.AddDays(-8),
                CreatedAt = now.AddDays(-8),
                CreatedBy = "System"
            });

            _context.ProjectComments.Add(new ProjectComment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                WorkItemId = planningTaskId,
                CommentType = "Status",
                Body = "Kickoff package approved. Waiting for workshop dates before baseline lock.",
                CreatedAt = now.AddDays(-2),
                CreatedBy = "System"
            });
        }

        private void CreateClosedCapexProject(
            Guid tenantId,
            ProjectType capexType,
            ProjectPriority lowPriority,
            ProjectPortfolio portfolio,
            ApplicationUser sponsor,
            ApplicationUser projectManager,
            ApplicationUser financeOwner,
            ApplicationUser teamMember,
            Department? department,
            Location? location,
            BusinessPartner customer,
            DateTime now)
        {
            var projectId = Guid.NewGuid();
            var milestoneId = Guid.NewGuid();
            var invoiceRequestId = Guid.NewGuid();
            var meetingId = Guid.NewGuid();

            var project = new Project
            {
                Id = projectId,
                TenantId = tenantId,
                ProjectCode = "PRJ-DEMO-1003",
                Title = "Warehouse Upgrade and Asset Refresh",
                Summary = "Capital project completed and closed after warehouse infrastructure refresh.",
                BusinessCase = "Improve warehouse handling capacity and reduce equipment downtime.",
                Objectives = "Install replacement equipment, complete acceptance, and close all open items.",
                StrategicAlignment = "Operational efficiency",
                ProjectTypeId = capexType.Id,
                ProjectPriorityId = lowPriority.Id,
                PortfolioId = portfolio.Id,
                Status = ProjectStatuses.Closed,
                Methodology = "Waterfall",
                SponsorId = sponsor.Id,
                ProjectManagerId = projectManager.Id,
                DepartmentId = department?.Id,
                LocationId = location?.Id,
                CustomerId = customer.Id,
                BusinessPartnerId = customer.Id,
                StartDate = now.Date.AddMonths(-8),
                TargetEndDate = now.Date.AddMonths(-2),
                ActualStartDate = now.Date.AddMonths(-8).AddDays(4),
                ActualEndDate = now.Date.AddMonths(-2).AddDays(-3),
                EstimatedBudget = 95000m,
                ApprovedBudget = 98000m,
                ActualCost = 94200m,
                BudgetStatus = "Approved",
                ProgressPercent = 100m,
                ApprovalRequired = true,
                SubmittedAt = now.AddMonths(-8).AddDays(-2),
                ApprovedAt = now.AddMonths(-8),
                ScopeStatement = "Replace staging equipment, improve storage layout, and complete training and handover.",
                Assumptions = "Approved asset budget and site access were available in line with plan.",
                Constraints = "Work had to avoid peak receiving windows.",
                ExpectedBenefits = "Higher throughput and lower equipment failure rate.",
                FundingSource = "Capex Allocation",
                StatusRemarks = "Project closed after acceptance and reconciliation.",
                ExternalPortalAccessEnabled = true,
                ExternalCollaborationEnabled = false,
                CreatedAt = now.AddMonths(-8).AddDays(-5),
                CreatedBy = "System"
            };

            _context.Projects.Add(project);
            _context.ProjectMembers.AddRange(
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = sponsor.Id,
                    Role = "Sponsor",
                    JoinedAt = now.AddMonths(-8).AddDays(-5),
                    CreatedAt = now.AddMonths(-8).AddDays(-5),
                    CreatedBy = "System"
                },
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = projectManager.Id,
                    Role = "ProjectManager",
                    JoinedAt = now.AddMonths(-8).AddDays(-4),
                    CreatedAt = now.AddMonths(-8).AddDays(-4),
                    CreatedBy = "System"
                },
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    UserId = teamMember.Id,
                    Role = "SiteLead",
                    JoinedAt = now.AddMonths(-8).AddDays(-4),
                    CreatedAt = now.AddMonths(-8).AddDays(-4),
                    CreatedBy = "System"
                });

            _context.ProjectMilestones.Add(new ProjectMilestone
            {
                Id = milestoneId,
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Final acceptance complete",
                Description = "Customer accepted the warehouse refresh and final inspection passed.",
                TargetDate = now.Date.AddMonths(-2).AddDays(-5),
                ActualDate = now.Date.AddMonths(-2).AddDays(-6),
                Status = "Approved",
                RequiresApproval = true,
                ApprovedById = sponsor.Id,
                ApprovedAt = now.AddMonths(-2).AddDays(-6),
                CreatedAt = now.AddMonths(-2).AddDays(-15),
                CreatedBy = "System"
            });

            _context.ProjectDeliverables.Add(new ProjectDeliverable
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                MilestoneId = milestoneId,
                Title = "Warehouse acceptance certificate",
                Description = "Signed acceptance and handover certificate for the completed works.",
                Status = "Approved",
                TargetDate = now.Date.AddMonths(-2).AddDays(-5),
                SubmittedAt = now.AddMonths(-2).AddDays(-7),
                SubmittedById = teamMember.Id,
                ExternalApprovedAt = now.AddMonths(-2).AddDays(-6),
                ExternalApprovedById = sponsor.Id,
                ApprovedAt = now.AddMonths(-2).AddDays(-6),
                ApprovedById = sponsor.Id,
                ExternalSubmissionAllowed = false,
                ExternalSignOffRequired = true,
                IsExternalVisible = true,
                AcceptanceNotes = "Accepted with no outstanding corrective actions.",
                CreatedAt = now.AddMonths(-2).AddDays(-8),
                CreatedBy = "System"
            });

            _context.ProjectBudgetRevisions.Add(new ProjectBudgetRevision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                VersionNumber = 1,
                RevisionName = "Final approved budget",
                RevisionType = "Baseline",
                EstimatedBudget = 95000m,
                ApprovedBudget = 98000m,
                CommittedCost = 94200m,
                ForecastCost = 94200m,
                Status = "Approved",
                EffectiveDate = now.Date.AddMonths(-8),
                SubmittedAt = now.AddMonths(-8).AddDays(-1),
                ApprovedAt = now.AddMonths(-8),
                ApprovedById = sponsor.Id,
                ChangeReason = "Approved capex baseline.",
                CreatedAt = now.AddMonths(-8).AddDays(-1),
                CreatedBy = "System"
            });

            _context.ProjectForecastVersions.Add(new ProjectForecastVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                VersionNumber = 1,
                VersionName = "Final forecast",
                AsOfDate = now.Date.AddMonths(-2),
                ForecastCost = 94200m,
                EstimateAtCompletion = 94200m,
                ForecastRevenue = 128000m,
                ForecastMargin = 33800m,
                IsActive = true,
                Notes = "Final forecast aligned to closed actuals.",
                CreatedAt = now.AddMonths(-2),
                CreatedBy = "System"
            });

            _context.ProjectBillingSchedules.Add(new ProjectBillingSchedule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                MilestoneId = milestoneId,
                Name = "Final completion billing",
                BillingType = "Milestone",
                Amount = 128000m,
                BillingPercentage = 100m,
                BillingDate = now.Date.AddMonths(-2).AddDays(-6),
                Status = "Invoiced",
                Description = "Final commercial billing after acceptance.",
                IsBillable = true,
                CreatedAt = now.AddMonths(-2).AddDays(-10),
                CreatedBy = "System"
            });

            _context.ProjectInvoiceRequests.Add(new ProjectInvoiceRequest
            {
                Id = invoiceRequestId,
                TenantId = tenantId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-PRJ-DEMO-1003-01",
                RequestedAmount = 128000m,
                Currency = "USD",
                Status = "Paid",
                RequestedAt = now.AddMonths(-2).AddDays(-10),
                SubmittedAt = now.AddMonths(-2).AddDays(-9),
                ExternalReference = "AR-CLOSED-1003",
                Notes = "Invoice settled in full after completion sign-off.",
                CreatedAt = now.AddMonths(-2).AddDays(-10),
                CreatedBy = "System"
            });

            _context.ProjectRevenueRecognitions.Add(new ProjectRevenueRecognition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                InvoiceRequestId = invoiceRequestId,
                RecognitionPeriod = $"{now.AddMonths(-2):yyyy-MM}",
                RecognizedRevenue = 128000m,
                RecognizedCost = 94200m,
                GrossMargin = 33800m,
                CashCollected = 128000m,
                Status = "Paid",
                Notes = "Recognition aligned to final invoice and cash receipt.",
                CreatedAt = now.AddMonths(-2).AddDays(-8),
                CreatedBy = "System"
            });

            _context.ProjectTimesheetEntries.Add(new ProjectTimesheetEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                UserId = projectManager.Id,
                EntryDate = now.Date.AddMonths(-3),
                Hours = 16m,
                IsBillable = false,
                HourlyRate = 110m,
                CostAmount = 1760m,
                WorkType = "Closure",
                Notes = "Final completion walkthrough and closeout documentation.",
                Status = "Approved",
                ApprovedById = sponsor.Id,
                ApprovedAt = now.AddMonths(-3).AddDays(1),
                CreatedAt = now.AddMonths(-3),
                CreatedBy = "System"
            });

            _context.ProjectExpenses.Add(new ProjectExpense
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                UserId = financeOwner.Id,
                ExpenseDate = now.Date.AddMonths(-4),
                Category = "Materials",
                Currency = "USD",
                Amount = 22400m,
                TaxAmount = 0m,
                IsBillable = false,
                Status = "Approved",
                Notes = "Final warehouse fit-out material issue.",
                ApprovedById = sponsor.Id,
                ApprovedAt = now.AddMonths(-4).AddDays(1),
                CreatedAt = now.AddMonths(-4),
                CreatedBy = "System"
            });

            _context.ProjectDecisions.Add(new ProjectDecision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Approve final asset refresh scope reduction",
                DecisionDate = now.AddMonths(-5),
                ApproverId = sponsor.Id,
                Rationale = "Defer non-critical shelving enhancement to stay under approved capex ceiling.",
                AlternativesConsidered = "Extend timeline and seek additional budget.",
                ImpactSummary = "Maintained delivery date and protected overall budget.",
                Status = "Approved",
                ApprovedAt = now.AddMonths(-5),
                CreatedAt = now.AddMonths(-5),
                CreatedBy = "System"
            });

            _context.ProjectMeetingMinutes.Add(new ProjectMeetingMinute
            {
                Id = meetingId,
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Project closure review",
                MeetingDate = now.AddMonths(-2).AddDays(-4),
                FacilitatorId = projectManager.Id,
                MeetingType = "Closure",
                Minutes = "Confirmed deliverable acceptance, open item disposition, and finance handoff.",
                AttendeesJson = JsonSerializer.Serialize(new[] { sponsor.Email, projectManager.Email, financeOwner.Email }),
                CreatedAt = now.AddMonths(-2).AddDays(-4),
                CreatedBy = "System"
            });

            _context.ProjectActionItems.Add(new ProjectActionItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                MeetingMinuteId = meetingId,
                Title = "Archive final asset handover pack",
                Description = "Move closure evidence to long-term archive and mark archive complete.",
                OwnerId = financeOwner.Id,
                DueDate = now.Date.AddMonths(-1),
                CompletedAt = now.AddMonths(-1).AddDays(-2),
                Status = "Closed",
                Priority = "Normal",
                CreatedAt = now.AddMonths(-2).AddDays(-4),
                CreatedBy = "System"
            });

            _context.ProjectLessonsLearned.Add(new ProjectLessonLearned
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Title = "Lock vendor lead times earlier in capex planning",
                Category = "Procurement",
                Description = "Long-lead equipment should be confirmed before final baseline approval.",
                Recommendation = "Move vendor commitment review into initiation and baseline gates.",
                AppliedPhase = "Planning",
                Visibility = "Internal",
                CreatedAt = now.AddMonths(-2).AddDays(-2),
                CreatedBy = "System"
            });

            _context.ProjectClosures.Add(new ProjectClosure
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Status = ProjectStatuses.Closed,
                SubmittedAt = now.AddMonths(-2).AddDays(-5),
                ApprovedAt = now.AddMonths(-2).AddDays(-3),
                ApprovedById = sponsor.Id,
                FinalBudget = 98000m,
                FinalCost = 94200m,
                DeliverablesAccepted = true,
                TasksCompletedOrWaived = true,
                AssetsReconciled = true,
                OpenItemsDisposed = true,
                ClosureChecklistJson = JsonSerializer.Serialize(new
                {
                    finalReview = true,
                    deliverablesAccepted = true,
                    financeClosed = true,
                    documentsArchived = true
                }),
                OpenItemsDisposition = "All remaining minor punch items were resolved before archive.",
                AssetReconciliationNotes = "Warehouse equipment transfers were reconciled against the asset register.",
                LessonsLearnedSummary = "Earlier vendor commitment reviews reduced late delivery risk.",
                PostImplementationReview = "Operational throughput improved and downtime fell within the first month.",
                CreatedAt = now.AddMonths(-2).AddDays(-5),
                CreatedBy = "System"
            });

            _context.ProjectDocuments.Add(new ProjectDocument
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                DocumentName = "Final completion certificate",
                Category = "Closure",
                DocumentType = "PDF",
                FilePath = "/seed/projects/PRJ-DEMO-1003/final-completion-certificate.pdf",
                FileType = "application/pdf",
                FileSize = 86000,
                VersionLabel = "1.0",
                Status = "Approved",
                EffectiveDate = now.Date.AddMonths(-2).AddDays(-6),
                IsExternalVisible = true,
                CreatedAt = now.AddMonths(-2).AddDays(-6),
                CreatedBy = "System"
            });

            _context.ProjectComments.Add(new ProjectComment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                CommentType = "Closure",
                Body = "Project closed after final acceptance, revenue recognition, and reconciliation.",
                CreatedAt = now.AddMonths(-2).AddDays(-3),
                CreatedBy = "System"
            });

            _context.ProjectExternalAccessPolicies.Add(new ProjectExternalAccessPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                BusinessPartnerId = customer.Id,
                ArtifactType = "Deliverable",
                AccessLevel = "Read",
                CanComment = false,
                CanUpload = false,
                CanApprove = true,
                Notes = "Customer can review archived completion and acceptance artifacts.",
                CreatedAt = now.AddMonths(-3),
                CreatedBy = "System"
            });
        }

        private async Task EnsureWorkflowDefinitionSeededAsync(
            Guid tenantId,
            string entityCode,
            string entityName,
            string? entityClassName,
            string definitionName,
            string description,
            IReadOnlyCollection<string> approvalRoleNames)
        {
            var entityType = await _context.WorkflowEntityTypes
                .FirstOrDefaultAsync(et => !et.IsDeleted && et.TenantId == tenantId && et.Code == entityCode);

            if (entityType == null)
            {
                entityType = new WorkflowEntityType
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = entityCode,
                    Name = entityName,
                    Description = description,
                    EntityClassName = entityClassName,
                    IsActive = true,
                    DisplayOrder = 60,
                    Icon = "workflow",
                    ColorCode = "#0F766E",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.WorkflowEntityTypes.Add(entityType);
                await _context.SaveChangesAsync();
            }

            var hasDefinition = await _context.WorkflowDefinitions
                .AnyAsync(d => !d.IsDeleted && d.TenantId == tenantId && d.EntityTypeId == entityType.Id);

            if (hasDefinition)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var definitionId = Guid.NewGuid();
            var draftStep = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = definitionId,
                Name = "Draft",
                StepType = WorkflowStepType.Manual,
                Order = 1,
                IsStartStep = true,
                IsRequired = true,
                CreatedAt = now,
                CreatedBy = "System"
            };
            var approvalStep = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = definitionId,
                Name = "PendingApproval",
                StepType = WorkflowStepType.Approval,
                Order = 2,
                IsRequired = true,
                Configuration = BuildApprovalConfigurationJson(approvalRoleNames),
                CreatedAt = now,
                CreatedBy = "System"
            };
            var approvedStep = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = definitionId,
                Name = entityCode.Equals("ProjectClosure", StringComparison.OrdinalIgnoreCase) ? ProjectStatuses.Closed : "Approved",
                StepType = WorkflowStepType.Manual,
                Order = 3,
                IsEndStep = true,
                IsRequired = true,
                CreatedAt = now,
                CreatedBy = "System"
            };

            _context.WorkflowDefinitions.Add(new WorkflowDefinition
            {
                Id = definitionId,
                TenantId = tenantId,
                Name = definitionName,
                Description = description,
                EntityTypeId = entityType.Id,
                Version = 1,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "System"
            });

            _context.WorkflowSteps.AddRange(draftStep, approvalStep, approvedStep);
            _context.WorkflowTransitions.AddRange(
                new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowDefinitionId = definitionId,
                    FromStepId = draftStep.Id,
                    ToStepId = approvalStep.Id,
                    Name = "Submit",
                    IsDefault = true,
                    Priority = 0,
                    CreatedAt = now,
                    CreatedBy = "System"
                },
                new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowDefinitionId = definitionId,
                    FromStepId = approvalStep.Id,
                    ToStepId = approvedStep.Id,
                    Name = "Approve",
                    IsDefault = true,
                    Priority = 0,
                    CreatedAt = now,
                    CreatedBy = "System"
                });

            await _context.SaveChangesAsync();
        }

        private static string BuildApprovalConfigurationJson(IReadOnlyCollection<string> approvalRoleNames)
        {
            var configuration = new WorkflowStepConfigurationDto
            {
                ApprovalConfig = new WorkflowApprovalConfigDto
                {
                    ApprovalType = WorkflowApprovalType.Single,
                    MinApprovalsRequired = 1,
                    RejectionHandling = WorkflowRejectionHandling.StopWorkflow,
                    ApproverRules = approvalRoleNames
                        .Where(role => !string.IsNullOrWhiteSpace(role))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(role => new WorkflowAssignmentRuleDto
                        {
                            AssignmentType = WorkflowAssignmentType.Role,
                            Role = role
                        })
                        .ToList()
                }
            };

            return JsonSerializer.Serialize(configuration);
        }

        private async Task EnsureEhcWorkflowSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var entityType = await _context.WorkflowEntityTypes
                        .FirstOrDefaultAsync(et => !et.IsDeleted && et.TenantId == tenant.Id && et.Code == "EHC_TICKET");

                    if (entityType == null)
                    {
                        entityType = new WorkflowEntityType
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Code = "EHC_TICKET",
                            Name = "EHC Ticket",
                            Description = "Enquiry, Helpdesk & Complaints Ticket",
                            EntityClassName = typeof(EhcTicket).FullName,
                            IsActive = true,
                            DisplayOrder = 50,
                            Icon = "ticket",
                            ColorCode = "#2563EB",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        };

                        _context.WorkflowEntityTypes.Add(entityType);
                        await _context.SaveChangesAsync();
                    }

                    var existingDefinition = await _context.WorkflowDefinitions
                        .FirstOrDefaultAsync(d => !d.IsDeleted && d.TenantId == tenant.Id && d.Name == "EHC Ticket");

                    if (existingDefinition != null)
                    {
                        continue;
                    }

                    var definitionId = Guid.NewGuid();
                    var definition = new WorkflowDefinition
                    {
                        Id = definitionId,
                        TenantId = tenant.Id,
                        Name = "EHC Ticket",
                        Description = "Baseline ticket lifecycle: New → Acknowledged → InProgress → Resolved → Closed",
                        EntityTypeId = entityType.Id,
                        Version = 1,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    _context.WorkflowDefinitions.Add(definition);

                    var stepNew = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.New.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 1,
                        IsStartStep = true,
                        IsEndStep = false,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var stepAck = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.Acknowledged.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 2,
                        IsStartStep = false,
                        IsEndStep = false,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var stepInProgress = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.InProgress.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 3,
                        IsStartStep = false,
                        IsEndStep = false,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var stepResolved = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.Resolved.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 4,
                        IsStartStep = false,
                        IsEndStep = false,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var stepClosed = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.Closed.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 5,
                        IsStartStep = false,
                        IsEndStep = true,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    _context.WorkflowSteps.AddRange(stepNew, stepAck, stepInProgress, stepResolved, stepClosed);

                    _context.WorkflowTransitions.AddRange(
                        new WorkflowTransition
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            WorkflowDefinitionId = definitionId,
                            FromStepId = stepNew.Id,
                            ToStepId = stepAck.Id,
                            Name = "Acknowledge",
                            IsDefault = true,
                            Priority = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        },
                        new WorkflowTransition
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            WorkflowDefinitionId = definitionId,
                            FromStepId = stepAck.Id,
                            ToStepId = stepInProgress.Id,
                            Name = "Start Progress",
                            IsDefault = true,
                            Priority = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        },
                        new WorkflowTransition
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            WorkflowDefinitionId = definitionId,
                            FromStepId = stepInProgress.Id,
                            ToStepId = stepResolved.Id,
                            Name = "Resolve",
                            IsDefault = true,
                            Priority = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        },
                        new WorkflowTransition
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            WorkflowDefinitionId = definitionId,
                            FromStepId = stepResolved.Id,
                            ToStepId = stepClosed.Id,
                            Name = "Close",
                            IsDefault = true,
                            Priority = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC workflow");
            }
        }

        private async Task EnsureEhcNotificationTopicsSeededAsync()
        {
            var tenants = await _context.Tenants
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active)
                .Select(t => t.Id)
                .ToListAsync();

            foreach (var tenantId in tenants)
            {
                // Requester-facing templates (email enabled).
                var createdTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Ticket Created (Requester)",
                    subject: "Ticket created: {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>Ticket created</h2>
                    <p>Your ticket <strong>{{ticketNumber}}</strong> has been created.</p>
                    <p><strong>Status:</strong> {{status}}</p>
                    <p>You can view your ticket here: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var statusChangedTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Ticket Status Changed (Requester)",
                    subject: "Ticket updated: {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>Ticket updated</h2>
                    <p>Your ticket <strong>{{ticketNumber}}</strong> status changed.</p>
                    <p><strong>From:</strong> {{fromStatus}}</p>
                    <p><strong>To:</strong> {{toStatus}}</p>
                    <p>Open ticket: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var messageTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Ticket Message (Requester)",
                    subject: "New message: {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>New message</h2>
                    <p>Support posted a new message on ticket <strong>{{ticketNumber}}</strong>.</p>
                    <p>Open ticket: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var attachmentTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Ticket Attachment (Requester)",
                    subject: "New attachment: {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>New attachment</h2>
                    <p>Support uploaded an attachment on ticket <strong>{{ticketNumber}}</strong>.</p>
                    <p><strong>File:</strong> {{fileName}}</p>
                    <p>Open ticket: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var feedbackRequestedTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Feedback Requested (Requester)",
                    subject: "How did we do? {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>We’d love your feedback</h2>
                    <p>Your ticket <strong>{{ticketNumber}}</strong> has been marked as {{status}}.</p>
                    <p>Please rate your experience here: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Created.Requester",
                    name: "EHC Ticket Created (Requester)",
                    description: "Notify the requester when a ticket is created.",
                    entityType: "EhcTicket",
                    isRequired: true,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Ticket created: {{ticketNumber}}",
                    inAppBodyTemplate: "We received ticket {{ticketNumber}} ({{ticketType}}). Status: {{status}}.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: createdTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.StatusChanged.Requester",
                    name: "EHC Ticket Status Changed (Requester)",
                    description: "Notify the requester when ticket status changes.",
                    entityType: "EhcTicket",
                    isRequired: true,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Ticket updated: {{ticketNumber}}",
                    inAppBodyTemplate: "Status: {{fromStatus}} → {{toStatus}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: statusChangedTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Message.Requester",
                    name: "EHC Ticket Message (Requester)",
                    description: "Notify the requester when an agent posts a message.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "New message: {{ticketNumber}}",
                    inAppBodyTemplate: "Support sent a new message.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: messageTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Attachment.Requester",
                    name: "EHC Ticket Attachment (Requester)",
                    description: "Notify the requester when an agent uploads an attachment.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "New attachment: {{ticketNumber}}",
                    inAppBodyTemplate: "Support uploaded {{fileName}}.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: attachmentTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.FeedbackRequested.Requester",
                    name: "EHC Feedback Requested (Requester)",
                    description: "Ask the requester to rate support after resolution/closure.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Feedback requested: {{ticketNumber}}",
                    inAppBodyTemplate: "How was your experience? Please rate ticket {{ticketNumber}}.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: feedbackRequestedTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                // Internal topics (in-app only by default).
                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Created.Internal",
                    name: "EHC Ticket Created (Internal)",
                    description: "Notify internal helpdesk users when a new ticket is created.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "New ticket: {{ticketNumber}}",
                    inAppBodyTemplate: "A new ticket was created ({{ticketType}} • {{priority}}).",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskAgent, inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Assigned.Internal",
                    name: "EHC Ticket Assigned (Internal)",
                    description: "Notify the assigned agent when a ticket is assigned.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Ticket assigned: {{ticketNumber}}",
                    inAppBodyTemplate: "A ticket was assigned to you.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Message.Internal",
                    name: "EHC Ticket Message (Internal)",
                    description: "Notify internal users when a requester posts a message.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Requester replied: {{ticketNumber}}",
                    inAppBodyTemplate: "The requester posted a new message.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.AgentMessage.Internal",
                    name: "EHC Ticket Agent Message (Internal)",
                    description: "Notify internal users when a support agent replies to the requester.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Support replied: {{ticketNumber}}",
                    inAppBodyTemplate: "{{messagePreview}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.InternalComment.Internal",
                    name: "EHC Ticket Internal Note (Internal)",
                    description: "Notify internal users when an internal note is added to a ticket.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Internal note: {{ticketNumber}}",
                    inAppBodyTemplate: "{{messagePreview}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Mention.Internal",
                    name: "EHC Ticket Mention (Internal)",
                    description: "Notify internal users when they are @mentioned on a ticket.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Mention: {{ticketNumber}}",
                    inAppBodyTemplate: "{{mentionedByName}} mentioned you: {{messagePreview}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UsersFromData", value: "mentionedUserIds", inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Attachment.Internal",
                    name: "EHC Ticket Attachment (Internal)",
                    description: "Notify internal users when a requester uploads an attachment.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Requester attachment: {{ticketNumber}}",
                    inAppBodyTemplate: "Requester uploaded {{fileName}}.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.StatusChanged.Internal",
                    name: "EHC Ticket Status Changed (Internal)",
                    description: "Notify internal users when ticket status changes.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Ticket updated: {{ticketNumber}}",
                    inAppBodyTemplate: "Status: {{fromStatus}} → {{toStatus}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.SlaWarning.Internal",
                    name: "EHC SLA Warning (Internal)",
                    description: "Notify internal users for near-breach SLA warnings.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "SLA warning: {{ticketNumber}}",
                    inAppBodyTemplate: "SLA due soon. Minutes left: {{minutesLeft}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.SlaBreach.Internal",
                    name: "EHC SLA Breach (Internal)",
                    description: "Notify internal users for SLA breaches.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "SLA breach: {{ticketNumber}}",
                    inAppBodyTemplate: "A ticket breached the SLA ({{breachKind}}).",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskManager, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.SlaEscalation.Internal",
                    name: "EHC SLA Escalation (Internal)",
                    description: "Notify internal users when SLA escalation auto-assigns a ticket.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Ticket escalated: {{ticketNumber}}",
                    inAppBodyTemplate: "Ticket auto-assigned due to SLA: {{reason}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "UsersFromData", value: "watcherUserIds", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskManager, inApp: true, email: false)
                    });

                // Service Catalog / Service Requests (customer + internal notifications)
                var srSubmittedTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "Service Request Submitted (Customer)",
                    subject: "Service request submitted: {{requestNumber}}",
                    htmlBody:
                    """
                    <h2>Service request submitted</h2>
                    <p>Your service request <strong>{{requestNumber}}</strong> has been submitted.</p>
                    <p><strong>Type:</strong> {{requestTypeName}}</p>
                    <p>You can view it here: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var srApprovedTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "Service Request Approved (Customer)",
                    subject: "Service request approved: {{requestNumber}}",
                    htmlBody:
                    """
                    <h2>Service request approved</h2>
                    <p>Your service request <strong>{{requestNumber}}</strong> has been approved.</p>
                    <p>View: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var srRejectedTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "Service Request Rejected (Customer)",
                    subject: "Service request rejected: {{requestNumber}}",
                    htmlBody:
                    """
                    <h2>Service request rejected</h2>
                    <p>Your service request <strong>{{requestNumber}}</strong> was rejected.</p>
                    <p><strong>Reason:</strong> {{reason}}</p>
                    <p>View: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var srFulfilledTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "Service Request Fulfilled (Customer)",
                    subject: "Service request fulfilled: {{requestNumber}}",
                    htmlBody:
                    """
                    <h2>Service request fulfilled</h2>
                    <p>Your service request <strong>{{requestNumber}}</strong> has been fulfilled.</p>
                    <p>View: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var srClosedTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "Service Request Closed (Customer)",
                    subject: "Service request closed: {{requestNumber}}",
                    htmlBody:
                    """
                    <h2>Service request closed</h2>
                    <p>Your service request <strong>{{requestNumber}}</strong> has been closed.</p>
                    <p>View: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var srAttachmentTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "Service Request Attachment (Customer)",
                    subject: "New attachment: {{requestNumber}}",
                    htmlBody:
                    """
                    <h2>New attachment</h2>
                    <p>A new attachment was added to service request <strong>{{requestNumber}}</strong>.</p>
                    <p><strong>File:</strong> {{fileName}}</p>
                    <p>View: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcServiceRequest.Submitted.Internal",
                    name: "Service Request Submitted (Internal)",
                    description: "Notify internal helpdesk users when a service request is submitted.",
                    entityType: "EhcServiceRequest",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "New service request: {{requestNumber}}",
                    inAppBodyTemplate: "{{requestTypeName}} — {{title}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "Role", value: Constants.Roles.HelpdeskAgent, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskManager, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcServiceRequest.Attachment.Internal",
                    name: "Service Request Attachment (Internal)",
                    description: "Notify internal helpdesk users when a requester uploads an attachment to a service request.",
                    entityType: "EhcServiceRequest",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Attachment: {{requestNumber}}",
                    inAppBodyTemplate: "New attachment uploaded: {{fileName}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "Role", value: Constants.Roles.HelpdeskAgent, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskManager, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcServiceRequest.Submitted.Customer",
                    name: "Service Request Submitted (Customer)",
                    description: "Notify the requester when a service request is submitted.",
                    entityType: "EhcServiceRequest",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Request submitted: {{requestNumber}}",
                    inAppBodyTemplate: "{{requestTypeName}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: srSubmittedTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "TargetUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcServiceRequest.Approved.Customer",
                    name: "Service Request Approved (Customer)",
                    description: "Notify the requester when a service request is approved.",
                    entityType: "EhcServiceRequest",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Approved: {{requestNumber}}",
                    inAppBodyTemplate: "Your request was approved.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: srApprovedTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "TargetUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcServiceRequest.Rejected.Customer",
                    name: "Service Request Rejected (Customer)",
                    description: "Notify the requester when a service request is rejected.",
                    entityType: "EhcServiceRequest",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Rejected: {{requestNumber}}",
                    inAppBodyTemplate: "{{reason}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: srRejectedTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "TargetUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcServiceRequest.Fulfilled.Customer",
                    name: "Service Request Fulfilled (Customer)",
                    description: "Notify the requester when a service request is fulfilled.",
                    entityType: "EhcServiceRequest",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Fulfilled: {{requestNumber}}",
                    inAppBodyTemplate: "Your request was fulfilled.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: srFulfilledTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "TargetUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcServiceRequest.Closed.Customer",
                    name: "Service Request Closed (Customer)",
                    description: "Notify the requester when a service request is closed.",
                    entityType: "EhcServiceRequest",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Closed: {{requestNumber}}",
                    inAppBodyTemplate: "Your request was closed.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: srClosedTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "TargetUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcServiceRequest.Attachment.Customer",
                    name: "Service Request Attachment (Customer)",
                    description: "Notify the requester when an attachment is added by support.",
                    entityType: "EhcServiceRequest",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Attachment: {{requestNumber}}",
                    inAppBodyTemplate: "{{fileName}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: srAttachmentTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "TargetUserId", inApp: true, email: true)
                    });
            }

            await _context.SaveChangesAsync();
        }

        private async Task<Guid?> EnsureEmailTemplateAsync(Guid tenantId, string name, string subject, string htmlBody)
        {
            var existing = await _context.EmailTemplates
                .FirstOrDefaultAsync(t => !t.IsDeleted && t.TenantId == tenantId && t.Module == "Notifications" && t.Name == name);

            if (existing != null)
            {
                return existing.Id;
            }

            var template = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Module = "Notifications",
                Name = name,
                Subject = subject,
                HtmlBody = htmlBody,
                IsActive = true,
                Category = "EHC",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            _context.EmailTemplates.Add(template);
            await _context.SaveChangesAsync();
            return template.Id;
        }

        private async Task EnsureNotificationTopicAsync(
            Guid tenantId,
            string key,
            string name,
            string description,
            string entityType,
            bool isRequired,
            bool enableInApp,
            bool enableEmail,
            string? inAppTitleTemplate,
            string? inAppBodyTemplate,
            string? actionUrlTemplate,
            Guid? emailTemplateId,
            (string kind, string value, bool inApp, bool email)[] recipients)
        {
            var topic = await _context.NotificationTopics
                .Include(t => t.Recipients)
                .FirstOrDefaultAsync(t => !t.IsDeleted && t.TenantId == tenantId && t.Key == key);

            if (topic == null)
            {
                topic = new NotificationTopic
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = key,
                    Name = name,
                    Description = description,
                    EntityType = entityType,
                    IsSystem = true,
                    IsRequired = isRequired,
                    IsActive = true,
                    EnableInApp = enableInApp,
                    EnableEmail = enableEmail,
                    InAppTitleTemplate = inAppTitleTemplate,
                    InAppBodyTemplate = inAppBodyTemplate,
                    ActionUrlTemplate = actionUrlTemplate,
                    EmailTemplateId = emailTemplateId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.NotificationTopics.Add(topic);
                await _context.SaveChangesAsync();
            }

            var existingRules = (topic.Recipients ?? new List<NotificationTopicRecipient>())
                .Where(r => !r.IsDeleted)
                .Select(r => $"{r.RecipientKind}:{r.RecipientValue}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (kind, value, inApp, email) in recipients)
            {
                var k = $"{kind}:{value}";
                if (existingRules.Contains(k)) continue;

                _context.NotificationTopicRecipients.Add(new NotificationTopicRecipient
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TopicId = topic.Id,
                    RecipientKind = kind,
                    RecipientValue = value,
                    IsSystem = true,
                    SendInApp = inApp,
                    SendEmail = email,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                });
            }
        }

        private async Task EnsureEhcWorkflowRoutingRulesSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var hasAny = await _context.EhcWorkflowRoutingRules.AnyAsync(r => r.TenantId == tenant.Id && !r.IsDeleted);
                    if (hasAny)
                    {
                        continue;
                    }

                    _context.EhcWorkflowRoutingRules.Add(new EhcWorkflowRoutingRule
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Name = "Default EHC Ticket Workflow",
                        IsActive = true,
                        Priority = 0,
                        WorkflowName = "EHC Ticket",
                        TicketType = null,
                        TicketPriority = null,
                        CategoryId = null,
                        SubcategoryId = null,
                        AssignedDepartmentId = null,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC workflow routing rules");
            }
        }

        private async Task EnsureEhcCategoriesSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var hasAny = await _context.EhcTicketCategories.AnyAsync(c => c.TenantId == tenant.Id && !c.IsDeleted);
                    if (hasAny)
                    {
                        continue;
                    }

                    var now = DateTime.UtcNow;

                    var general = new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Code = "GENERAL",
                        Name = "General",
                        Description = "General enquiries and requests",
                        CreatedAt = now,
                        CreatedBy = "System"
                    };
                    var technical = new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Code = "TECH",
                        Name = "Technical Support",
                        Description = "Technical issues and helpdesk requests",
                        AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Helpdesk,
                        CreatedAt = now,
                        CreatedBy = "System"
                    };
                    var complaints = new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Code = "COMPLAINTS",
                        Name = "Complaints",
                        Description = "Service/product complaints",
                        AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Complaint,
                        CreatedAt = now,
                        CreatedBy = "System"
                    };

                    _context.EhcTicketCategories.AddRange(general, technical, complaints);

                    // A few starter subcategories
                    _context.EhcTicketCategories.AddRange(
                        new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            ParentCategoryId = technical.Id,
                            Code = "LOGIN",
                            Name = "Login / Access",
                            AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Helpdesk,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            ParentCategoryId = technical.Id,
                            Code = "BUG",
                            Name = "System Bug",
                            AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Helpdesk,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            ParentCategoryId = complaints.Id,
                            Code = "SERVICE",
                            Name = "Service Quality",
                            AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Complaint,
                            CreatedAt = now,
                            CreatedBy = "System"
                        });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC categories");
            }
        }

        private async Task EnsureEhcSlaTemplatesSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var hasAny = await _context.EhcSlaTemplates.AnyAsync(s => s.TenantId == tenant.Id && !s.IsDeleted);
                    if (hasAny)
                    {
                        continue;
                    }

                    var now = DateTime.UtcNow;
                    _context.EhcSlaTemplates.AddRange(
                        new ErpSystem.Core.Entities.Ehc.EhcSlaTemplate
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = "Default - Low",
                            IsActive = true,
                            Priority = ErpSystem.Core.Enums.EhcTicketPriority.Low,
                            FirstResponseMinutes = 240,
                            ResolutionMinutes = 4320,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcSlaTemplate
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = "Default - Medium",
                            IsActive = true,
                            Priority = ErpSystem.Core.Enums.EhcTicketPriority.Medium,
                            FirstResponseMinutes = 120,
                            ResolutionMinutes = 2880,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcSlaTemplate
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = "Default - High",
                            IsActive = true,
                            Priority = ErpSystem.Core.Enums.EhcTicketPriority.High,
                            FirstResponseMinutes = 60,
                            ResolutionMinutes = 1440,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcSlaTemplate
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = "Default - Critical",
                            IsActive = true,
                            Priority = ErpSystem.Core.Enums.EhcTicketPriority.Critical,
                            FirstResponseMinutes = 30,
                            ResolutionMinutes = 480,
                            CreatedAt = now,
                            CreatedBy = "System"
                        });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC SLA templates");
            }
        }

        public async Task SeedTestUsersAsync()
        {
            _logger.LogInformation("Seeding test users...");

            await SeedRolesAsync();
            await SeedRolePermissionAssignmentsAsync();
            await SeedDefaultTenantAsync();

            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found for test user seeding");
                return;
            }

            // Only create test users if they don't exist - don't update existing users
            // Ensure these accounts exist and remain usable on every Development seed run.
            // (CreateTestUserAsync is idempotent and will update existing users as needed.)
            await CreateTestUserAsync("admin", "admin@default.com", "Admin123!",
                "System", "Administrator", defaultTenant.Id, Constants.Roles.SuperAdmin, AuthenticationProvider.Local);

            await CreateTestUserAsync("manager", "manager@default.com", "Manager123!",
                "John", "Manager", defaultTenant.Id, Constants.Roles.Manager, AuthenticationProvider.Local);

            await CreateTestUserAsync("employee", "employee@default.com", "Employee123!",
                "Jane", "Employee", defaultTenant.Id, Constants.Roles.Employee, AuthenticationProvider.Local);

            await CreateTestUserAsync("helpdesk.agent", "helpdesk.agent@default.com", "Helpdesk123!",
                "Helpdesk", "Agent", defaultTenant.Id, Constants.Roles.HelpdeskAgent, AuthenticationProvider.Local);

            await CreateTestUserAsync("helpdesk.supervisor", "helpdesk.supervisor@default.com", "Helpdesk123!",
                "Helpdesk", "Supervisor", defaultTenant.Id, Constants.Roles.HelpdeskSupervisor, AuthenticationProvider.Local);

            await CreateTestUserAsync("helpdesk.manager", "helpdesk.manager@default.com", "Helpdesk123!",
                "Helpdesk", "Manager", defaultTenant.Id, Constants.Roles.HelpdeskManager, AuthenticationProvider.Local);

            // External portal user (Local auth) for testing support portal flows
            await CreateTestUserAsync("external", "external@default.com", "External123!",
                "External", "User", defaultTenant.Id, Constants.Roles.ExternalUser, AuthenticationProvider.Local);

            _logger.LogInformation("Test users seeding completed");
        }
        
        public async Task SeedMaintenanceE2ETestDataAsync()
        {
            _logger.LogInformation("Seeding Maintenance E2E test data...");
            
            try
            {
                // Create a logger factory to get the properly typed logger
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<MaintenanceE2ETestSeeder>();
                
                var seeder = new MaintenanceE2ETestSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("Maintenance E2E test data seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding Maintenance E2E test data");
                throw;
            }
        }

        private async Task SeedHRDataAsync()
        {
            _logger.LogInformation("Seeding HR data...");
            
            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<HRDataSeeder>();
                
                var seeder = new HRDataSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("HR data seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding HR data");
                throw;
            }
        }

        private async Task SeedMaintenanceConfigurationAsync()
        {
            _logger.LogInformation("Seeding maintenance configuration...");
            
            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<MaintenanceConfigurationSeeder>();
                
                var seeder = new MaintenanceConfigurationSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("Maintenance configuration seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding maintenance configuration");
                throw;
            }
        }
        
        private async Task SeedMaintenanceComprehensiveDataAsync()
        {
            _logger.LogInformation("Seeding comprehensive maintenance data...");
            
            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<MaintenanceComprehensiveDataSeeder>();
                
                var seeder = new MaintenanceComprehensiveDataSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("Comprehensive maintenance data seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding comprehensive maintenance data");
                throw;
            }
        }
        
        private async Task SeedQualityControlChecklistsAsync()
        {
            _logger.LogInformation("Seeding quality control checklists...");
            
            try
            {
                var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
                if (defaultTenant == null)
                {
                    _logger.LogWarning("Default tenant not found, skipping QC checklist seeding");
                    return;
                }
                
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<QualityControlChecklistSeeder>();
                
                var seeder = new QualityControlChecklistSeeder(_context, seederLogger);
                await seeder.SeedAsync(defaultTenant.Id);
                _logger.LogInformation("Quality control checklists seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding quality control checklists");
                throw;
            }
        }

        private async Task SeedFinanceDataAsync()
        {
            _logger.LogInformation("Seeding finance data...");
            
            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<FinanceDataSeeder>();
                
                var seeder = new FinanceDataSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("Finance data seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding finance data");
                throw;
            }
        }

        public async Task<bool> HasSeedDataAsync()
        {
            var hasRoles = await _roleManager.Roles.AnyAsync();
            var hasTenants = await _context.Tenants.AnyAsync();
            return hasRoles && hasTenants;
        }

        private async Task SeedRolesAsync()
        {
            var roles = new[]
            {
                new { Name = Constants.Roles.SuperAdmin, Description = "System Super Administrator with full access" },
                new { Name = Constants.Roles.TenantAdmin, Description = "Tenant Administrator with tenant-wide access" },
                new { Name = Constants.Roles.Manager, Description = "Manager with departmental access" },
                new { Name = Constants.Roles.Employee, Description = "Standard employee with limited access" },
                new { Name = Constants.Roles.ExternalUser, Description = "External portal user (customers/vendors/partners/citizens)" },
                new { Name = Constants.Roles.HelpdeskAgent, Description = "Helpdesk agent for managing tickets" },
                new { Name = Constants.Roles.HelpdeskSupervisor, Description = "Helpdesk supervisor for assignment and escalation" },
                new { Name = Constants.Roles.HelpdeskManager, Description = "Helpdesk manager for dashboards and configuration" },
                new { Name = "Finance User", Description = "User with access to finance module" },
                new { Name = "HR User", Description = "User with access to HR module" },
                new { Name = "Sales User", Description = "User with access to sales module" },
                new { Name = "Inventory User", Description = "User with access to inventory module" },
                new { Name = "Procurement User", Description = "User with access to procurement module" },
                new { Name = "Marketing User", Description = "User with access to marketing module" }
            };

            foreach (var roleInfo in roles)
            {
                var existingRole = await _roleManager.FindByNameAsync(roleInfo.Name);
                if (existingRole == null)
                {
                    var role = new ApplicationRole(roleInfo.Name)
                    {
                        Description = roleInfo.Description,
                        IsSystemRole = roleInfo.Name.Contains("Admin") || roleInfo.Name.Contains("Manager"),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var result = await _roleManager.CreateAsync(role);
                    if (result.Succeeded)
                    {
                        _logger.LogDebug("Created role: {RoleName}", roleInfo.Name);
                    }
                    else
                    {
                        _logger.LogError("Failed to create role {RoleName}: {Errors}", 
                            roleInfo.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
        }

        private async Task SeedDefaultTenantAsync()
        {
            var existingTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");

            if (existingTenant == null)
            {
                var tenant = new Tenant
                {
                    Name = "Default Company",
                    Code = "DEFAULT",
                    Description = "Default tenant for system operations",
                    Status = TenantStatus.Active,
                    ContactEmail = "admin@default.com",
                    ContactPhone = "+1-555-0100",
                    Address = "123 Default Street, Default City, DC 12345",
                    SubscriptionStartDate = DateTime.UtcNow,
                    SubscriptionEndDate = DateTime.UtcNow.AddYears(1),
                    LdapEnabled = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Created default tenant: {TenantName} with ID {TenantId}", tenant.Name, tenant.Id);
            }
        }

        private async Task SeedDefaultTenantModulesAsync()
        {
            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found for module seeding");
                return;
            }

            // IMPORTANT: TenantModules has a unique index on (TenantId, ModuleName) and uses soft-delete.
            // Seeding must be idempotent and must not insert duplicates when a record already exists (even if soft-deleted).
            var modules = new[]
            {
                new { ModuleName = "Finance", Description = "Financial reports and analytics" },
                new { ModuleName = "Sales", Description = "Sales performance and CRM reports" },
                new { ModuleName = "HR", Description = "HR and employee reports" },
                new { ModuleName = "Inventory", Description = "Stock and inventory reports" },
                new { ModuleName = "Procurement", Description = "Purchasing and supplier reports" },
                new { ModuleName = "Marketing", Description = "Marketing campaigns and analytics" },
                new { ModuleName = "WorkflowEngine", Description = "Workflow automation and BPM" }
            };

            var now = DateTime.UtcNow;

            var existing = await _context.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == defaultTenant.Id)
                .ToListAsync();

            var existingByName = existing
                .Where(m => !string.IsNullOrWhiteSpace(m.ModuleName))
                .ToDictionary(m => m.ModuleName.Trim(), m => m, StringComparer.OrdinalIgnoreCase);

            var created = 0;
            var updated = 0;

            foreach (var moduleInfo in modules)
            {
                if (existingByName.TryGetValue(moduleInfo.ModuleName, out var module))
                {
                    var changed = false;

                    if (module.IsDeleted)
                    {
                        module.IsDeleted = false;
                        module.DeletedAt = null;
                        module.DeletedBy = null;
                        changed = true;
                    }

                    if (module.Status != ModuleStatus.Enabled)
                    {
                        module.Status = ModuleStatus.Enabled;
                        changed = true;
                    }

                    // Keep existing descriptions unless empty; some tenants may customize descriptions.
                    if (string.IsNullOrWhiteSpace(module.Description))
                    {
                        module.Description = moduleInfo.Description;
                        changed = true;
                    }

                    if (module.EnabledDate == null)
                    {
                        module.EnabledDate = now;
                        changed = true;
                    }

                    if (changed)
                    {
                        module.UpdatedAt = now;
                        module.UpdatedBy = "System";
                        updated++;
                        _logger.LogDebug("Updated tenant module: {ModuleName} for tenant {TenantName}", moduleInfo.ModuleName, defaultTenant.Name);
                    }

                    continue;
                }

                _context.TenantModules.Add(new TenantModule
                {
                    Id = Guid.NewGuid(),
                    TenantId = defaultTenant.Id,
                    ModuleName = moduleInfo.ModuleName,
                    Description = moduleInfo.Description,
                    Status = ModuleStatus.Enabled,
                    EnabledDate = now,
                    CreatedAt = now,
                    CreatedBy = "System"
                });

                created++;
                _logger.LogDebug("Created tenant module: {ModuleName} for tenant {TenantName}", moduleInfo.ModuleName, defaultTenant.Name);
            }

            if (created > 0 || updated > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Ensured default tenant modules for {TenantName}: created={Created}, updated={Updated}.",
                    defaultTenant.Name,
                    created,
                    updated);
            }
            else
            {
                _logger.LogInformation("Default tenant modules already up to date for {TenantName}.", defaultTenant.Name);
            }
        }

        private async Task SeedRolePermissionAssignmentsAsync()
        {
            var helpdeskPermissions = new[]
            {
                new
                {
                    Name = "enquiry.internal.access",
                    DisplayName = "Access Internal Enquiry",
                    Description = "Access the internal enquiry backoffice branch",
                    Category = "Helpdesk Branch Access"
                },
                new
                {
                    Name = "enquiry.external.access",
                    DisplayName = "Access External Enquiry",
                    Description = "Access the external enquiry backoffice branch",
                    Category = "Helpdesk Branch Access"
                },
                new
                {
                    Name = "support.internal.access",
                    DisplayName = "Access Internal Helpdesk & Complaints",
                    Description = "Access the internal helpdesk and complaints backoffice branch",
                    Category = "Helpdesk Branch Access"
                },
                new
                {
                    Name = "support.external.access",
                    DisplayName = "Access External Helpdesk & Complaints",
                    Description = "Access the external helpdesk and complaints backoffice branch",
                    Category = "Helpdesk Branch Access"
                }
            };

            foreach (var permissionInfo in helpdeskPermissions)
            {
                var existingPermission = await _context.Permissions
                    .FirstOrDefaultAsync(p => p.Name == permissionInfo.Name);

                if (existingPermission != null)
                {
                    continue;
                }

                _context.Permissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = permissionInfo.Name,
                    DisplayName = permissionInfo.DisplayName,
                    Description = permissionInfo.Description,
                    Category = permissionInfo.Category,
                    IsSystemPermission = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                });
            }

            await _context.SaveChangesAsync();

            var helpdeskRoles = new[]
            {
                Constants.Roles.HelpdeskAgent,
                Constants.Roles.HelpdeskSupervisor,
                Constants.Roles.HelpdeskManager
            };

            var permissions = await _context.Permissions
                .Where(p => helpdeskPermissions.Select(info => info.Name).Contains(p.Name))
                .ToListAsync();

            if (!permissions.Any())
            {
                _logger.LogWarning("Helpdesk branch permissions not found yet; skipping role-permission seed.");
                return;
            }

            foreach (var roleName in helpdeskRoles)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    continue;
                }

                var existingPermissionIds = await _context.RolePermissions
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.PermissionId)
                    .ToListAsync();

                var missingPermissions = permissions
                    .Where(permission => !existingPermissionIds.Contains(permission.Id))
                    .Select(permission => new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id,
                        GrantedAt = DateTime.UtcNow,
                        GrantedBy = "System"
                    })
                    .ToList();

                if (missingPermissions.Count == 0)
                {
                    continue;
                }

                _context.RolePermissions.AddRange(missingPermissions);
            }

            await _context.SaveChangesAsync();
        }

        private async Task EnsureEhcKnowledgeBaseSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var hasAny = await _context.EhcKnowledgeBaseArticles.AnyAsync(a => a.TenantId == tenant.Id && !a.IsDeleted);
                    if (hasAny)
                    {
                        continue;
                    }

                    var now = DateTime.UtcNow;

                    var cat = new ErpSystem.Core.Entities.Ehc.EhcKnowledgeBaseCategory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Code = "GENERAL",
                        Name = "General",
                        Description = "General support articles",
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = "System"
                    };

                    _context.EhcKnowledgeBaseCategories.Add(cat);

                    _context.EhcKnowledgeBaseArticles.Add(new ErpSystem.Core.Entities.Ehc.EhcKnowledgeBaseArticle
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Code = "GETTING_STARTED",
                        Title = "Getting started with Helpdesk tickets",
                        Summary = "How to create, assign and transition tickets.",
                        Body = "Use Helpdesk → Tickets to create and track enquiries/complaints/support requests.\n\nTip: You can @mention colleagues using @email and add them as watchers for updates.",
                        CategoryId = cat.Id,
                        TagsCsv = "helpdesk,tickets,workflow,watchers,mentions",
                        IsPublished = true,
                        IsInternalOnly = true,
                        ViewCount = 0,
                        CreatedAt = now,
                        CreatedBy = "System"
                    });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC knowledge base");
            }
        }

        private async Task EnsureFileUploadPoliciesSeededAsync()
        {
            var tenants = await _context.Tenants.AsNoTracking().Where(t => !t.IsDeleted).Select(t => new { t.Id }).ToListAsync();
            if (tenants.Count == 0) return;

            foreach (var t in tenants)
            {
                var tenantId = t.Id;
                if (tenantId == Guid.Empty) continue;

                var hasGlobal = await _context.FileUploadPolicies.AnyAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Category == "*");
                if (!hasGlobal)
                {
                    _context.FileUploadPolicies.Add(new FileUploadPolicy
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Category = "*",
                        IsEnabled = true,
                        MaxFileSizeBytes = 10 * 1024 * 1024, // 10MB (matches appsettings defaults)
                        MaxTenantTotalBytes = 5L * 1024 * 1024 * 1024, // 5GB per tenant baseline
                        MaxCategoryTotalBytes = 1024L * 1024 * 1024, // 1GB per category baseline
                        AllowedExtensionsCsv = null, // fallback to API defaults/config
                        AllowedMimeTypesCsv = null, // fallback to API defaults/config
                        RequireVirusScan = false,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task CreateTestUserAsync(
            string username,
            string email,
            string password,
            string firstName,
            string lastName,
            Guid tenantId,
            string roleName,
            AuthenticationProvider authenticationProvider = AuthenticationProvider.Local)
        {
            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                // Ensure tenant, profile fields, and active status are correct
                var needsUpdate = false;
                if (existingUser.TenantId != tenantId) { existingUser.TenantId = tenantId; needsUpdate = true; }
                if (existingUser.Email != email) { existingUser.Email = email; needsUpdate = true; }
                if (existingUser.FirstName != firstName) { existingUser.FirstName = firstName; needsUpdate = true; }
                if (existingUser.LastName != lastName) { existingUser.LastName = lastName; needsUpdate = true; }
                if (!existingUser.EmailConfirmed) { existingUser.EmailConfirmed = true; needsUpdate = true; }
                if (!existingUser.IsActive) { existingUser.IsActive = true; needsUpdate = true; }
                if (existingUser.AuthenticationProvider != authenticationProvider) { existingUser.AuthenticationProvider = authenticationProvider; needsUpdate = true; }

                if (needsUpdate)
                {
                    await _userManager.UpdateAsync(existingUser);
                }

                await EnsureTestUserTenantAccessAsync(existingUser.Id, tenantId, roleName);

                // Ensure role assignment
                var inRole = await _userManager.IsInRoleAsync(existingUser, roleName);
                if (!inRole)
                {
                    var addRoleResult = await _userManager.AddToRoleAsync(existingUser, roleName);
                    if (!addRoleResult.Succeeded)
                    {
                        _logger.LogError("Failed to ensure role {Role} for user {Username}: {Errors}", roleName, username,
                            string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                    }
                }

                // Reset password to the expected strong password to align with docs/login page
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                var resetResult = await _userManager.ResetPasswordAsync(existingUser, resetToken, password);
                if (resetResult.Succeeded)
                {
                    // Clear lockout just in case
                    await _userManager.SetLockoutEndDateAsync(existingUser, null);
                    await _userManager.ResetAccessFailedCountAsync(existingUser);
                    _logger.LogInformation("Updated existing user {Username} and reset password.", username);
                }
                else
                {
                    _logger.LogError("Failed to reset password for {Username}: {Errors}", username,
                        string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                }

                return;
            }

            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                TenantId = tenantId,
                AuthenticationProvider = authenticationProvider,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                PhoneNumberConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await EnsureTestUserTenantAccessAsync(user.Id, tenantId, roleName);

                // Add user to role
                var roleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (roleResult.Succeeded)
                {
                    _logger.LogInformation("Created test user: {Username} with role {Role}", username, roleName);
                }
                else
                {
                    _logger.LogError("Failed to add role {Role} to user {Username}: {Errors}",
                        roleName, username, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogError("Failed to create test user {Username}: {Errors}",
                    username, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        private async Task EnsureTestUserTenantAccessAsync(Guid userId, Guid tenantId, string roleName)
        {
            var accessLevel = string.Equals(roleName, Constants.Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
                ? UserTenantAccessLevel.Admin
                : UserTenantAccessLevel.Standard;

            var relationships = await _context.UserTenants
                .Where(ut => ut.UserId == userId && !ut.IsDeleted)
                .ToListAsync();

            var targetRelationship = relationships.FirstOrDefault(ut => ut.TenantId == tenantId);
            if (targetRelationship == null)
            {
                targetRelationship = new UserTenant
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TenantId = tenantId,
                    AccessLevel = accessLevel,
                    Status = UserTenantStatus.Active,
                    IsDefault = true,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = "System",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.UserTenants.Add(targetRelationship);
            }
            else
            {
                targetRelationship.AccessLevel = accessLevel;
                targetRelationship.Status = UserTenantStatus.Active;
                targetRelationship.IsDefault = true;
                targetRelationship.ExpiresAt = null;
                targetRelationship.SuspendedAt = null;
                targetRelationship.ReactivatedAt ??= DateTime.UtcNow;
                targetRelationship.GrantedAt = targetRelationship.GrantedAt == default ? DateTime.UtcNow : targetRelationship.GrantedAt;
                targetRelationship.GrantedBy ??= "System";
                targetRelationship.UpdatedAt = DateTime.UtcNow;
                targetRelationship.UpdatedBy = "System";
            }

            foreach (var relationship in relationships.Where(ut => ut.TenantId != tenantId && ut.IsDefault))
            {
                relationship.IsDefault = false;
                relationship.UpdatedAt = DateTime.UtcNow;
                relationship.UpdatedBy = "System";
            }

            await _context.SaveChangesAsync();
        }
    }

    // Extension methods for easy registration
    public static class DatabaseSeedingServiceExtensions
    {
        public static IServiceCollection AddDatabaseSeeding(this IServiceCollection services)
        {
            services.AddScoped<IDatabaseSeedingService, DatabaseSeedingService>();
            return services;
        }

        public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
            await seedingService.SeedAsync();
        }
    }
}
