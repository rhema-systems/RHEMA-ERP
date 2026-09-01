using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Api.Configuration;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Web.Services
{
    public interface IDatabaseSeedingService
    {
        Task SeedAsync();
        Task SeedWithoutMigrationAsync();
        Task SeedBasicDataAsync();
        Task SeedWorkflowDefinitionsAsync();
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
        private readonly ProcurementConfigurationProfileSeeder? _procurementConfigurationProfileSeeder;
        private readonly ProcurementAccessControlSeeder? _procurementAccessControlSeeder;
        private readonly ProcurementStatutoryReportSeeder? _procurementStatutoryReportSeeder;
        private readonly InventoryStatutoryReportSeeder? _inventoryStatutoryReportSeeder;
        private readonly AuditComplianceReportSeeder? _auditComplianceReportSeeder;
        private readonly QuantitySurveyAccessControlSeeder? _quantitySurveyAccessControlSeeder;
        private readonly QuantitySurveyConfigurationProfileSeeder? _quantitySurveyConfigurationProfileSeeder;
        private readonly QuantitySurveyStatutoryReportSeeder? _quantitySurveyStatutoryReportSeeder;
        private readonly CivilEngineeringConfigurationProfileSeeder? _civilEngineeringConfigurationProfileSeeder;
        private readonly CivilEngineeringAccessControlSeeder? _civilEngineeringAccessControlSeeder;
        private readonly CivilEngineeringStatutoryReportSeeder? _civilEngineeringStatutoryReportSeeder;
        private readonly ProcurementSupplierOnboardingTestSeeder? _procurementSupplierOnboardingTestSeeder;
        private readonly bool _allowDevelopmentDataSeedingOutsideDevelopment;

        private static readonly IReadOnlyList<WorkflowApprovalStageSeed> FinanceApprovalStages =
            new List<WorkflowApprovalStageSeed>
            {
                new(
                    "Accounts Officer Review",
                    new[] { "Accounts Officer", "Senior Accountant" },
                    "Initial finance review for completeness, coding, supporting documents, and policy compliance."),
                new(
                    "Finance Manager Approval",
                    new[] { "Finance Manager" },
                    "Finance manager approval for budget, cash, accounting, and operational control."),
                new(
                    "Financial Controller Final Approval",
                    new[] { "Financial Controller" },
                    "Final finance control approval before the document is released to downstream processing.")
            };

        private static readonly IReadOnlyList<WorkflowApprovalStageSeed> FinancePaymentApprovalStages =
            new List<WorkflowApprovalStageSeed>
            {
                new(
                    "Finance Manager Approval",
                    new[] { "Finance Manager" },
                    "Finance manager authorization of the supplier payment after invoice processing is complete."),
                new(
                    "Financial Controller Final Approval",
                    new[] { "Financial Controller" },
                    "Independent final payment authorization before posting, clearing, or settlement finalization.")
            };

        private static readonly JsonSerializerOptions WorkflowSeedJsonOptions = CreateWorkflowSeedJsonOptions();

        private sealed record FinanceWorkflowSeedSpec(
            string EntityCode,
            string EntityName,
            string? EntityClassName,
            string DefinitionName,
            string Description);

        private sealed record WorkflowApprovalStageSeed(
            string StepName,
            IReadOnlyCollection<string> RoleNames,
            string Description);

        private sealed record EstateSopWorkflowSeedSpec(
            string EntityCode,
            string EntityName,
            string DefinitionName,
            string Description,
            IReadOnlyList<EstateSopWorkflowStepSeed> Steps);

        private sealed record EstateSopWorkflowStepSeed(
            string StepName,
            WorkflowStepType StepType,
            string RoleName,
            string Description,
            IReadOnlyList<string> Checklist,
            IReadOnlyList<string> Documents,
            string TaskActionType = "estate-sop-example",
            string DocumentType = "EstateSopEvidence",
            string? Instructions = null);

        public DatabaseSeedingService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<DatabaseSeedingService> logger,
            IWebHostEnvironment environment,
            ProcurementConfigurationProfileSeeder? procurementConfigurationProfileSeeder = null,
            ProcurementAccessControlSeeder? procurementAccessControlSeeder = null,
            ProcurementStatutoryReportSeeder? procurementStatutoryReportSeeder = null,
            InventoryStatutoryReportSeeder? inventoryStatutoryReportSeeder = null,
            IConfiguration? configuration = null,
            AuditComplianceReportSeeder? auditComplianceReportSeeder = null,
            QuantitySurveyAccessControlSeeder? quantitySurveyAccessControlSeeder = null,
            QuantitySurveyConfigurationProfileSeeder? quantitySurveyConfigurationProfileSeeder = null,
            QuantitySurveyStatutoryReportSeeder? quantitySurveyStatutoryReportSeeder = null,
            ProcurementSupplierOnboardingTestSeeder? procurementSupplierOnboardingTestSeeder = null,
            CivilEngineeringConfigurationProfileSeeder? civilEngineeringConfigurationProfileSeeder = null,
            CivilEngineeringAccessControlSeeder? civilEngineeringAccessControlSeeder = null,
            CivilEngineeringStatutoryReportSeeder? civilEngineeringStatutoryReportSeeder = null)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _environment = environment;
            _procurementConfigurationProfileSeeder = procurementConfigurationProfileSeeder;
            _procurementAccessControlSeeder = procurementAccessControlSeeder;
            _procurementStatutoryReportSeeder = procurementStatutoryReportSeeder;
            _inventoryStatutoryReportSeeder = inventoryStatutoryReportSeeder;
            _auditComplianceReportSeeder = auditComplianceReportSeeder;
            _quantitySurveyAccessControlSeeder = quantitySurveyAccessControlSeeder;
            _quantitySurveyConfigurationProfileSeeder = quantitySurveyConfigurationProfileSeeder;
            _quantitySurveyStatutoryReportSeeder = quantitySurveyStatutoryReportSeeder;
            _civilEngineeringConfigurationProfileSeeder = civilEngineeringConfigurationProfileSeeder;
            _civilEngineeringAccessControlSeeder = civilEngineeringAccessControlSeeder;
            _civilEngineeringStatutoryReportSeeder = civilEngineeringStatutoryReportSeeder;
            _procurementSupplierOnboardingTestSeeder = procurementSupplierOnboardingTestSeeder;
            _allowDevelopmentDataSeedingOutsideDevelopment = configuration?.GetValue(
                StartupInitializationPolicy.AllowDevelopmentDataSeedingOutsideDevelopmentKey,
                false) ?? false;
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

                await SeedWorkflowDefinitionsAsync();

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

                if (_procurementConfigurationProfileSeeder is not null)
                {
                    _logger.LogInformation("Ensuring draft procurement configuration profiles are seeded...");
                    await _procurementConfigurationProfileSeeder.SeedAsync();
                }

                if (_procurementAccessControlSeeder is not null)
                {
                    _logger.LogInformation("Ensuring TDC access roles, permissions, committees, and Draft workflow templates are seeded...");
                    await _procurementAccessControlSeeder.SeedAsync();
                }

                if (_civilEngineeringConfigurationProfileSeeder is not null)
                {
                    _logger.LogInformation("Ensuring draft Civil Engineering configuration profiles are seeded...");
                    await _civilEngineeringConfigurationProfileSeeder.SeedAsync();
                }

                if (_civilEngineeringAccessControlSeeder is not null)
                {
                    _logger.LogInformation("Ensuring Civil Engineering roles and permissions are seeded...");
                    await _civilEngineeringAccessControlSeeder.SeedAsync();
                }

                if (_quantitySurveyAccessControlSeeder is not null)
                {
                    _logger.LogInformation("Ensuring TDC Quantity Survey access roles, permissions, and workflow entity types are seeded...");
                    await _quantitySurveyAccessControlSeeder.SeedAsync();
                }

                if (_quantitySurveyConfigurationProfileSeeder is not null)
                {
                    _logger.LogInformation("Ensuring draft TDC Quantity Survey configuration profiles are seeded...");
                    await _quantitySurveyConfigurationProfileSeeder.SeedAsync();
                }

                if (_procurementStatutoryReportSeeder is not null)
                {
                    _logger.LogInformation("Ensuring TDC procurement statutory report catalogue is seeded...");
                    await _procurementStatutoryReportSeeder.SeedAsync();
                }

                if (_inventoryStatutoryReportSeeder is not null)
                {
                    _logger.LogInformation("Ensuring TDC inventory statutory report catalogue is seeded...");
                    await _inventoryStatutoryReportSeeder.SeedAsync();
                }

                if (_auditComplianceReportSeeder is not null)
                {
                    _logger.LogInformation("Ensuring TDC audit and compliance report catalogue is seeded...");
                    await _auditComplianceReportSeeder.SeedAsync();
                }

                if (_quantitySurveyStatutoryReportSeeder is not null)
                {
                    _logger.LogInformation("Ensuring TDC Quantity Survey statutory report catalogue is seeded...");
                    await _quantitySurveyStatutoryReportSeeder.SeedAsync();
                }

                if (_civilEngineeringStatutoryReportSeeder is not null)
                {
                    _logger.LogInformation("Ensuring TDC Civil Engineering report catalogue is seeded...");
                    await _civilEngineeringStatutoryReportSeeder.SeedAsync();
                }

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

                // Ensure development test users exist without changing passwords for existing accounts.
                if (StartupInitializationPolicy.IsDevelopmentDataSeedingPermitted(
                    _environment.EnvironmentName,
                    _allowDevelopmentDataSeedingOutsideDevelopment))
                {
                    _logger.LogInformation("Ensuring development test users exist...");
                    await SeedTestUsersAsync();
                    _logger.LogInformation("Ensuring Estate SOP example cases are seeded...");
                    await EnsureEstateSopExampleCasesSeededAsync();
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

                    if (_procurementSupplierOnboardingTestSeeder is not null)
                    {
                        _logger.LogInformation("Ensuring supplier-onboarding end-to-end test prerequisites are seeded...");
                        await _procurementSupplierOnboardingTestSeeder.SeedAsync();
                    }

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

            await SeedWorkflowDefinitionsAsync();
            await EnsureProjectCatalogDefaultsSeededAsync();

            _logger.LogInformation("Basic data seeding completed");
        }

        public async Task SeedWorkflowDefinitionsAsync()
        {
            // Keep this lightweight and idempotent so startup can repair baseline workflow definitions
            // without enabling the broader development/demo data seed.
            _logger.LogInformation("Ensuring finance permission catalogue and baseline role grants are seeded...");
            await EnsureFinancePermissionAssignmentsAsync();
            _logger.LogInformation("Ensuring EHC workflow is seeded...");
            await EnsureEhcWorkflowSeededAsync();
            _logger.LogInformation("Ensuring finance workflows are seeded...");
            await EnsureFinanceWorkflowsSeededAsync();
            _logger.LogInformation("Ensuring business partner workflows are seeded...");
            await EnsureBusinessPartnerWorkflowsSeededAsync();
            _logger.LogInformation("Ensuring procurement receipt-inspection workflow is seeded...");
            await EnsureProcurementOperationalWorkflowsSeededAsync();
            _logger.LogInformation("Ensuring project workflows are seeded...");
            await EnsureProjectWorkflowsSeededAsync();
            _logger.LogInformation("Ensuring Estate SOP example workflows are seeded...");
            await EnsureEstateSopWorkflowsSeededAsync();
            _logger.LogInformation("Ensuring Legal procedure workflows are seeded...");
            await EnsureLegalProcedureWorkflowsSeededAsync();
            _logger.LogInformation("Ensuring workflow notification topics are seeded...");
            await EnsureWorkflowNotificationTopicsSeededAsync();

            // Program.cs invokes this lightweight path during every permitted VPS
            // startup. Keep the UAT PO route here so Draft templates are repaired and
            // published before operators can submit purchase orders.
            if (_procurementAccessControlSeeder is not null &&
                StartupInitializationPolicy.IsDevelopmentDataSeedingPermitted(
                    _environment.EnvironmentName,
                    _allowDevelopmentDataSeedingOutsideDevelopment))
            {
                _logger.LogInformation(
                    "Ensuring TDC Draft workflow templates and the UAT Purchase Order approval workflow are ready...");
                await _procurementAccessControlSeeder.SeedAsync();
                await _procurementAccessControlSeeder
                    .EnsurePublishedPurchaseOrderApprovalWorkflowForUatAsync();
            }
        }

        private async Task EnsureFinancePermissionAssignmentsAsync()
        {
            // This is an additive, idempotent production-startup repair. It creates no tenants,
            // transactions, or demo records; it only ensures the role and permission catalogue
            // required by Finance authorization policies exists before those policies are enforced.
            await SeedRolesAsync();
            await SeedRolePermissionAssignmentsAsync();
        }

        private async Task EnsureProcurementOperationalWorkflowsSeededAsync()
        {
            try
            {
                const string entityCode = "PROCUREMENT_RECEIPT_INSPECTION";
                const string definitionName = "Procurement Receipt Inspection Approval";
                const string approverRole = "TDC_STORES_MANAGER";
                var stages = new[]
                {
                    new WorkflowApprovalStageSeed(
                        "PendingApproval",
                        new[] { approverRole },
                        "Independent Stores Manager approval of accepted, rejected, damaged, and short receipt quantities.")
                };
                var tenants = await _context.Tenants
                    .Where(tenant => !tenant.IsDeleted && tenant.Status == TenantStatus.Active)
                    .ToListAsync();

                foreach (var tenant in tenants)
                {
                    await EnsureSequentialWorkflowDefinitionSeededAsync(
                        tenant.Id,
                        entityCode,
                        "Procurement Receipt Inspection",
                        typeof(ProcurementReceiptInspectionCase).FullName,
                        definitionName,
                        "Stores maker-checker approval before accepted receipt quantities become stock and AP eligible.",
                        stages);

                    // Earlier system seed data assigned this step to Head of Procurement.
                    // Pending approval rows contain no decision and are safe to realign to
                    // the corrected Stores Manager workflow role. Completed rows remain immutable.
                    var pendingApprovals = await _context.WorkflowApprovals
                        .Include(approval => approval.StepInstance)
                            .ThenInclude(instance => instance.WorkflowStep)
                                .ThenInclude(step => step.WorkflowDefinition)
                        .Where(approval =>
                            approval.TenantId == tenant.Id &&
                            !approval.IsDeleted &&
                            approval.Status == WorkflowApprovalStatus.Pending &&
                            approval.ApproverId == null &&
                            approval.ApproverRole != approverRole &&
                            approval.StepInstance.WorkflowStep.WorkflowDefinition.CreatedBy == "System" &&
                            approval.StepInstance.WorkflowStep.WorkflowDefinition.Name.StartsWith(definitionName))
                        .ToListAsync();

                    if (pendingApprovals.Count == 0)
                        continue;

                    var repairedAt = DateTime.UtcNow;
                    foreach (var approval in pendingApprovals)
                    {
                        approval.ApproverRole = approverRole;
                        approval.UpdatedAt = repairedAt;
                        approval.UpdatedBy = "System";
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "Realigned {Count} pending receipt-inspection approval assignment(s) to {Role} for tenant {TenantId}",
                        pendingApprovals.Count,
                        approverRole,
                        tenant.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed procurement operational workflows");
            }
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

        private async Task EnsureEstateSopWorkflowsSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    foreach (var spec in GetEstateSopWorkflowSeedSpecs())
                    {
                        await EnsureEstateSopWorkflowDefinitionSeededAsync(tenant.Id, spec);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed Estate SOP example workflows");
            }
        }

        private async Task EnsureLegalProcedureWorkflowsSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    foreach (var spec in GetLegalProcedureWorkflowSeedSpecs())
                    {
                        await EnsureEstateSopWorkflowDefinitionSeededAsync(tenant.Id, spec);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed Legal procedure workflows");
            }
        }

        private async Task EnsureEstateSopWorkflowDefinitionSeededAsync(Guid tenantId, EstateSopWorkflowSeedSpec spec)
        {
            var entityType = await EnsureWorkflowEntityTypeAsync(
                tenantId,
                spec.EntityCode,
                spec.EntityName,
                null,
                spec.Description);

            var definitions = await _context.WorkflowDefinitions
                .Include(item => item.Steps)
                .Where(item =>
                    item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.EntityTypeId == entityType.Id
                    && item.Name.StartsWith(spec.DefinitionName))
                .ToListAsync();

            var matchingDefinition = definitions
                .Where(item => HasExpectedEstateSopSteps(item, spec.Steps))
                .OrderByDescending(item => item.Version)
                .ThenByDescending(item => item.PublishedAt ?? item.CreatedAt)
                .FirstOrDefault();

            if (matchingDefinition is not null)
            {
                var changed = false;
                var now = DateTime.UtcNow;

                if (!matchingDefinition.IsActive)
                {
                    matchingDefinition.IsActive = true;
                    changed = true;
                }

                if (matchingDefinition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
                {
                    matchingDefinition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
                    matchingDefinition.PublishedAt ??= now;
                    matchingDefinition.RetiredAt = null;
                    matchingDefinition.RetiredById = null;
                    changed = true;
                }

                if (matchingDefinition.Description != spec.Description)
                {
                    matchingDefinition.Description = spec.Description;
                    changed = true;
                }

                if (EnsureEstateSopStepConfigurations(matchingDefinition, spec.Steps, now))
                {
                    changed = true;
                }

                if (RetireSupersededEstateSopExampleDefinitions(definitions, matchingDefinition.Id, spec, now))
                {
                    changed = true;
                }

                if (changed)
                {
                    matchingDefinition.UpdatedAt = now;
                    matchingDefinition.UpdatedBy = "System";
                    await _context.SaveChangesAsync();
                }

                return;
            }

            var createdAt = DateTime.UtcNow;
            var version = definitions.Count == 0 ? 1 : definitions.Max(item => item.Version) + 1;
            var definitionName = definitions.Any(item => string.Equals(item.Name, spec.DefinitionName, StringComparison.OrdinalIgnoreCase))
                ? $"{spec.DefinitionName} v{version}"
                : spec.DefinitionName;
            var definitionId = Guid.NewGuid();

            var steps = spec.Steps
                .Select((step, index) => new WorkflowStep
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowDefinitionId = definitionId,
                    Name = step.StepName,
                    Description = step.Description,
                    StepType = step.StepType,
                    RequiredRole = step.RoleName,
                    AssignmentType = "Role",
                    Order = index + 1,
                    IsStartStep = index == 0,
                    IsEndStep = index == spec.Steps.Count - 1,
                    IsRequired = true,
                    Configuration = BuildEstateSopStepConfigurationJson(step),
                    CreatedAt = createdAt,
                    CreatedBy = "System"
                })
                .ToList();

            _context.WorkflowDefinitions.Add(new WorkflowDefinition
            {
                Id = definitionId,
                DefinitionKey = definitionId,
                TenantId = tenantId,
                Name = definitionName,
                Description = spec.Description,
                EntityTypeId = entityType.Id,
                Version = version,
                IsActive = true,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                PublishedAt = createdAt,
                CreatedAt = createdAt,
                CreatedBy = "System"
            });

            _context.WorkflowSteps.AddRange(steps);

            for (var index = 0; index < steps.Count - 1; index++)
            {
                _context.WorkflowTransitions.Add(new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowDefinitionId = definitionId,
                    FromStepId = steps[index].Id,
                    ToStepId = steps[index + 1].Id,
                    Name = steps[index + 1].StepType == WorkflowStepType.Approval
                        ? "Submit for approval"
                        : index == steps.Count - 2
                            ? "Close"
                            : "Complete step",
                    IsDefault = true,
                    Priority = 0,
                    CreatedAt = createdAt,
                    CreatedBy = "System"
                });
            }

            RetireSupersededEstateSopExampleDefinitions(definitions, definitionId, spec, createdAt);
            await _context.SaveChangesAsync();
        }

        private static bool HasExpectedEstateSopSteps(
            WorkflowDefinition definition,
            IReadOnlyList<EstateSopWorkflowStepSeed> expectedSteps)
        {
            var actualSteps = definition.Steps
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.Order)
                .Select(item => NormalizeWorkflowEntityTypeKey(item.Name))
                .ToList();

            return actualSteps.SequenceEqual(expectedSteps.Select(item => NormalizeWorkflowEntityTypeKey(item.StepName)));
        }

        private static bool EnsureEstateSopStepConfigurations(
            WorkflowDefinition definition,
            IReadOnlyList<EstateSopWorkflowStepSeed> expectedSteps,
            DateTime now)
        {
            var changed = false;
            foreach (var expectedStep in expectedSteps)
            {
                var step = definition.Steps.FirstOrDefault(item =>
                    !item.IsDeleted && WorkflowEntityTypeKeyMatches(item.Name, expectedStep.StepName));

                if (step is null)
                {
                    continue;
                }

                var expectedConfiguration = BuildEstateSopStepConfigurationJson(expectedStep);
                if (step.Configuration != expectedConfiguration)
                {
                    step.Configuration = expectedConfiguration;
                    changed = true;
                }

                if (step.RequiredRole != expectedStep.RoleName)
                {
                    step.RequiredRole = expectedStep.RoleName;
                    changed = true;
                }

                if (step.StepType != expectedStep.StepType)
                {
                    step.StepType = expectedStep.StepType;
                    changed = true;
                }

                if (changed)
                {
                    step.UpdatedAt = now;
                    step.UpdatedBy = "System";
                }
            }

            return changed;
        }

        private static bool RetireSupersededEstateSopExampleDefinitions(
            IEnumerable<WorkflowDefinition> definitions,
            Guid activeDefinitionId,
            EstateSopWorkflowSeedSpec spec,
            DateTime now)
        {
            var changed = false;
            foreach (var definition in definitions.Where(item =>
                         item.Id != activeDefinitionId
                         && item.IsActive
                         && string.Equals(item.CreatedBy, "System", StringComparison.OrdinalIgnoreCase)
                         && item.Name.StartsWith(spec.DefinitionName, StringComparison.OrdinalIgnoreCase)))
            {
                definition.IsActive = false;
                definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
                definition.RetiredAt ??= now;
                definition.UpdatedAt = now;
                definition.UpdatedBy = "System";
                changed = true;
            }

            return changed;
        }

        private static string BuildEstateSopStepConfigurationJson(EstateSopWorkflowStepSeed step)
        {
            var configuration = new WorkflowStepConfigurationDto
            {
                QualityConfig = new WorkflowQualityConfigDto
                {
                    QualityChecks = step.Checklist
                        .Select((check, index) => new WorkflowQualityCheckDto
                        {
                            Id = $"{NormalizeWorkflowEntityTypeKey(step.StepName)}-CHECK-{index + 1}",
                            Name = check,
                            Description = check,
                            IsRequired = true
                        })
                        .ToList()
                },
                TaskConfig = new WorkflowTaskConfigDto
                {
                    TaskActionType = step.TaskActionType,
                    RequiresDocument = false,
                    Instructions = step.Instructions ?? $"Example SOP step for {step.RoleName}. Replace or refine this in Workflow Setup for the live operating procedure.",
                    DocumentRequirements = step.Documents
                        .Select((document, index) => new WorkflowDocumentRequirementDto
                        {
                            Id = $"{NormalizeWorkflowEntityTypeKey(step.StepName)}-DOC-{index + 1}",
                            RequirementKey = $"{NormalizeWorkflowEntityTypeKey(step.StepName)}-DOC-{index + 1}",
                            DocumentName = document,
                            DocumentType = step.DocumentType,
                            IsRequired = true
                        })
                        .ToList()
                }
            };

            if (step.StepType == WorkflowStepType.Approval)
            {
                configuration.ApprovalConfig = BuildApprovalConfig([step.RoleName]);
            }

            return JsonSerializer.Serialize(configuration, WorkflowSeedJsonOptions);
        }

        private static IReadOnlyList<EstateSopWorkflowSeedSpec> GetEstateSopWorkflowSeedSpecs()
        {
            var registryToManager = new[]
            {
                Step("Registry Intake", WorkflowStepType.Manual, "Registry Officer",
                    ["Request logged in registry / movement book", "Property file reference captured", "Approved form/template selected"],
                    ["Application letter / request form", "File movement trace"]),
                Step("Estate Officer Processing", WorkflowStepType.Manual, "Estate Officer",
                    ["SOP section and fee appendix reference captured", "Finance/Records/Legal handoff need assessed"],
                    ["Property file extract", "Approved fee schedule or appendix extract"]),
                Step("Estate Manager Approval", WorkflowStepType.Approval, "Estate Manager",
                    ["Completeness, arrears, and required evidence reviewed"],
                    ["Approval, recommendation, or routing note"]),
                Step("Records and DMS Closeout", WorkflowStepType.Manual, "Records Officer",
                    ["Records update reference captured", "Final output indexed in Central DMS"],
                    ["Records amendment evidence", "Central DMS reference"])
            };

            var transferSteps = new[]
            {
                Step("Registry Intake", WorkflowStepType.Manual, "Registry Officer",
                    ["Transfer request logged", "House / plot / shop number captured"],
                    ["Application letter / request form", "Property file extract"]),
                Step("Transfer Evidence Review", WorkflowStepType.Manual, "Records Officer",
                    ["Transferor and transferee details captured", "Voluntary vacation evidence checked", "New lessee address captured"],
                    ["Transfer Declaration form completed by transferor and transferee", "Voluntary vacation of tenancy evidence", "New lessee address evidence"]),
                Step("Finance and Legal Review", WorkflowStepType.Manual, "Estate Officer",
                    ["Transfer fee / arrears status checked", "Legal completion or registered instrument reference captured"],
                    ["Transfer fee payment confirmation", "Registered transfer instrument or Legal completion note"]),
                Step("Estate Manager Approval", WorkflowStepType.Approval, "Estate Manager",
                    ["Transfer amendment approved before records are changed"],
                    ["Approval, recommendation, or routing note"]),
                Step("Revenue and Estate Records Update", WorkflowStepType.Manual, "Records Officer",
                    ["Estate register updated", "Revenue register / ledger updated", "Records amendment confirmation entered"],
                    ["Revenue and Estate Records amendment confirmation", "Records amendment evidence"])
            };

            var housingSteps = new[]
            {
                Step("Housing Intake", WorkflowStepType.Manual, "Housing Officer",
                    ["Housing request type selected", "Tenant/unit reference captured"],
                    ["Recognition or HOS application", "Rental Transfer Form"]),
                Step("Revenue and Records Check", WorkflowStepType.Manual, "Records Officer",
                    ["Rent card/register checked", "Payment completion status captured", "HOS ledger impact assessed"],
                    ["Rent Card", "Revenue and Estate Records amendment confirmation"]),
                Step("Estate Manager Approval", WorkflowStepType.Approval, "Estate Manager",
                    ["Recognition, rental transfer, or HOS conversion approved"],
                    ["HOS Offer Letter", "Approval, recommendation, or routing note"]),
                Step("HOS Ledger and DMS Closeout", WorkflowStepType.Manual, "Housing Officer",
                    ["HOS ledger updated", "Lease request generated where property purchased"],
                    ["HOS ledger and Estate Records update evidence", "Lease request for purchased house"])
            };

            var feeAndOfferSteps = new[]
            {
                Step("Application Intake", WorkflowStepType.Manual, "Registry Officer",
                    ["Application form/letter logged", "Land use and plot size captured"],
                    ["Application form or application letter", "Property file extract"]),
                Step("LMF and Ground Rent Calculation", WorkflowStepType.Manual, "Estate Officer",
                    ["Approved fee schedule selected", "LMF calculated", "Ground rent calculated"],
                    ["LMF and Ground Rent calculation worksheet", "Approved fee schedule or appendix extract"]),
                Step("Estate Manager Approval", WorkflowStepType.Approval, "Estate Manager",
                    ["Proposal/offer terms reviewed", "Finance receipt dependency confirmed"],
                    ["Proposal Letter with LMF and Ground Rent", "Approval, recommendation, or routing note"]),
                Step("Offer and Right of Entry Closeout", WorkflowStepType.Manual, "Estate Officer",
                    ["Offer Letter reference captured", "Right of Entry reference captured", "Quarterly reporting reference captured"],
                    ["Offer Letter", "Right of Entry"])
            };

            var reportingSteps = new[]
            {
                Step("Report Compilation", WorkflowStepType.Manual, "Estate Officer",
                    ["Source schedule selected", "Period and source counts captured", "Exception summary captured"],
                    ["Quarterly productivity report", "Allocation and expected revenue report"]),
                Step("Records and Finance Reconciliation", WorkflowStepType.Manual, "Records Officer",
                    ["Rent roll / debtor list checked", "Transfer and assignment return checked", "Fee appendix control sheet attached"],
                    ["Rent roll", "Debtor list", "Approved appendix fee schedule control sheet"]),
                Step("Estate Manager Review", WorkflowStepType.Approval, "Estate Manager",
                    ["Report pack reviewed for Board / management submission"],
                    ["Control exception register", "Board summary / approved report pack"]),
                Step("Published Report Closeout", WorkflowStepType.Manual, "Estate Officer",
                    ["Board submission reference captured", "DMS/audit trail reference captured"],
                    ["Approved report pack", "Central DMS reference"])
            };

            var facilitiesMaintenanceSteps = new[]
            {
                Step("Facilities Intake", WorkflowStepType.Manual, "Facilities Officer",
                    ["Requester, contact, property/unit, issue type, and priority are confirmed", "Service impact, target date, and access notes are recorded", "Maintenance job card need is assessed"],
                    []),
                Step("Maintenance Handoff Review", WorkflowStepType.Manual, "Facilities Supervisor",
                    ["Maintenance routing decision is recorded", "Safety, access, and SLA context are confirmed", "Requester update has been issued"],
                    []),
                Step("Maintenance Closeout", WorkflowStepType.Approval, "Facilities Manager",
                    ["Job card or work order reference is recorded where required", "Inspection, requester feedback, and completion outcome are reviewed", "Facilities case is ready for closeout"],
                    [])
            };

            var facilitiesComplaintSteps = new[]
            {
                Step("Facilities Complaint Intake", WorkflowStepType.Manual, "Facilities Officer",
                    ["Complainant, property/unit, category, and impact are confirmed", "Complaint details and target response date are recorded", "Helpdesk escalation need is assessed"],
                    []),
                Step("Complaint Resolution Review", WorkflowStepType.Manual, "Facilities Supervisor",
                    ["Resolution action or escalation path is recorded", "Customer communication status is updated", "Evidence and service-impact notes are reviewed"],
                    []),
                Step("Complaint Closeout", WorkflowStepType.Approval, "Facilities Manager",
                    ["Resolution outcome and requester feedback are confirmed", "Any Helpdesk or Maintenance reference is captured", "Facilities complaint is ready for closeout"],
                    [])
            };

            return
            [
                Spec("EstateFacilityMaintenance", "Maintenance Intake", facilitiesMaintenanceSteps),
                Spec("EstateFacilityComplaint", "Complaint Management", facilitiesComplaintSteps),
                Spec("EstateRegistrySecretariat", "Secretarial and Estates Registry", registryToManager),
                Spec("EstateRecordsManagement", "Estate Records Management", registryToManager),
                Spec("EstateInspection", "Land and Landed Property Inspection", registryToManager),
                Spec("EstateSearchApplication", "Search Application", registryToManager),
                Spec("EstateRecordAmendment", "Change of Address and Record Amendment", registryToManager),
                Spec("EstateCertifiedTrueCopy", "Certified True Copies", registryToManager),
                Spec("EstateJointOwnership", "Joint Ownership / Addition of Name", transferSteps),
                Spec("EstateTransfer", "Transfer / Portion Transfer of Plot", transferSteps),
                Spec("EstateAssignment", "Assignment", transferSteps),
                Spec("EstateMortgageConsent", "Consent to Mortgage / Mortgage in Principle", registryToManager),
                Spec("EstateLeasePreparation", "Lease Preparation", registryToManager),
                Spec("EstateAdditionalLand", "Additional Land Application", feeAndOfferSteps),
                Spec("EstateLayoutRevision", "Revision of Layout", registryToManager),
                Spec("EstateChangeOfUse", "Change of Land Use", feeAndOfferSteps),
                Spec("EstateReminderRateRevision", "Reminder and Rate Revision Notices", registryToManager),
                Spec("EstateLeaseRenewal", "Lease Surrender and Renewal", registryToManager),
                Spec("EstateServicedPlotAllocation", "Serviced Plots and HOS Allocation", feeAndOfferSteps),
                Spec("EstateLandsPartiallyServiced", "Lands / Partially Serviced Schedule", feeAndOfferSteps),
                Spec("EstateHousingHomeOwnership", "Housing and Home Ownership Scheme", housingSteps),
                Spec("EstateTraditionalLands", "Traditional Lands", feeAndOfferSteps),
                Spec("EstateTenancyRegularisation", "Tenancy Regularisation", feeAndOfferSteps),
                Spec("EstateReportingControls", "Estate Reporting and SOP Controls", reportingSteps)
            ];

            static EstateSopWorkflowStepSeed Step(
                string name,
                WorkflowStepType type,
                string role,
                IReadOnlyList<string> checks,
                IReadOnlyList<string> documents)
                => new(name, type, role, string.Join(" ", checks), checks, documents);

            static EstateSopWorkflowSeedSpec Spec(
                string entityCode,
                string entityName,
                IReadOnlyList<EstateSopWorkflowStepSeed> steps)
                => new(
                    entityCode,
                    entityName,
                    $"Estate SOP Example - {entityName}",
                    $"Example TDC Estate SOP workflow for {entityName}. It provides practical stages, checklist controls, and document requirements that can be cloned/refined in Workflow Setup.",
                    steps);
        }

        private static IReadOnlyList<EstateSopWorkflowSeedSpec> GetLegalProcedureWorkflowSeedSpecs()
        {
            const string TaskActionType = "legal-property-agreement-review";
            const string DocumentType = "LegalAgreementReviewEvidence";

            return
            [
                new(
                    "LegalPropertyAgreementReview",
                    "LegalPropertyAgreementReview",
                    "Legal Property Agreement Review",
                    "Workflow for Legal review of draft rental, lease, and sale agreements submitted from Property Management.",
                    [
                        LegalStep(
                            "Legal Intake",
                            WorkflowStepType.Manual,
                            "Legal Admin Assistant",
                            [
                                "Confirm Estate source reference",
                                "Confirm draft agreement is attached",
                                "Assign Legal Officer"
                            ],
                            [
                                "Generated draft agreement"
                            ]),
                        LegalStep(
                            "Agreement Vetting",
                            WorkflowStepType.Manual,
                            "Legal Officer",
                            [
                                "Verify parties and property",
                                "Review clauses and schedules",
                                "Approve or return for correction"
                            ],
                            [
                                "Legal review note"
                            ]),
                        LegalStep(
                            "Head of Legal Release",
                            WorkflowStepType.Approval,
                            "Head of Legal",
                            [
                                "Confirm Legal Officer recommendation",
                                "Record release decision",
                                "Return approved reference to Property Management"
                            ],
                            [
                                "Approved / released agreement"
                            ])
                    ])
            ];

            static EstateSopWorkflowStepSeed LegalStep(
                string name,
                WorkflowStepType type,
                string role,
                IReadOnlyList<string> checks,
                IReadOnlyList<string> documents)
                => new(
                    name,
                    type,
                    role,
                    string.Join(" ", checks),
                    checks,
                    documents,
                    TaskActionType,
                    DocumentType,
                    $"Complete the {name} task for the property agreement review workflow.");
        }

        private async Task EnsureFinanceWorkflowsSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var tenantId = tenant.Id;
                    await RetireQuarantinedSupplierReturnWorkflowDefinitionsAsync(tenantId);
                    foreach (var spec in GetFinanceWorkflowSeedSpecs())
                    {
                        var approvalStages = spec.EntityCode is
                            "VendorPayment" or "PaymentBatch" or "VendorInvoiceMatchException"
                            ? FinancePaymentApprovalStages
                            : FinanceApprovalStages;

                        await EnsureSequentialWorkflowDefinitionSeededAsync(
                            tenant.Id,
                            spec.EntityCode,
                            spec.EntityName,
                            spec.EntityClassName,
                            spec.DefinitionName,
                            spec.Description,
                            approvalStages);
                    }
                    await EnsureVendorPaymentControlWorkflowSeededAsync(tenant.Id);
                    await EnsureApPaymentControlPoliciesSeededAsync(tenant.Id);
                    var chiefAccountantStage = new[]
                    {
                        new WorkflowApprovalStageSeed(
                            "Chief Accountant Approval",
                            new[] { "Chief Accountant" },
                            "Maker-checker approval of deposit evidence, net banking, destination account, and returned-cheque accounting.")
                    };
                    await EnsureSequentialWorkflowDefinitionSeededAsync(
                        tenant.Id,
                        "BankDepositBatch",
                        "Bank Deposit",
                        typeof(BankDepositBatch).FullName,
                        "Bank Deposit Approval",
                        "Every banking deposit requires Chief Accountant approval before the net bank movement is posted.",
                        chiefAccountantStage);
                    await EnsureSequentialWorkflowDefinitionSeededAsync(
                        tenant.Id,
                        "ReturnedChequeCase",
                        "Returned Cheque",
                        typeof(ReturnedChequeCase).FullName,
                        "Returned Cheque Approval",
                        "Returned cheque cases require Chief Accountant approval before AR is reopened and the bank debit is posted.",
                        chiefAccountantStage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed finance workflows");
            }
        }

        private async Task RetireQuarantinedSupplierReturnWorkflowDefinitionsAsync(Guid tenantId)
        {
            // Finance owns SupplierDebitNote commercial/AP approval only. FIN-INT-012/013 remain
            // quarantined, so historical SupplierReturn definitions must not authorize a Procurement/
            // Inventory return dispatch or imply that Finance owns the producer transaction.
            var activeDefinitions = await _context.WorkflowDefinitions
                .Include(item => item.EntityType)
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .ToListAsync();

            var retiredAt = DateTime.UtcNow;
            var retiredCount = RetireQuarantinedSupplierReturnWorkflowDefinitions(activeDefinitions, retiredAt);
            if (retiredCount == 0)
                return;

            await _context.SaveChangesAsync();
            _logger.LogWarning(
                "Retired {WorkflowDefinitionCount} active SupplierReturn workflow definition(s) for tenant {TenantId}; FIN-INT-012/013 remain quarantined.",
                retiredCount,
                tenantId);
        }

        private static int RetireQuarantinedSupplierReturnWorkflowDefinitions(
            IEnumerable<WorkflowDefinition> definitions,
            DateTime retiredAt)
        {
            var retiredCount = 0;
            foreach (var definition in definitions.Where(item =>
                         item.IsActive &&
                         !item.IsDeleted &&
                         item.EntityType != null &&
                         item.EntityType.TenantId == item.TenantId &&
                         !item.EntityType.IsDeleted &&
                         (WorkflowEntityTypeKeyMatches(item.EntityType.Code, "SupplierReturn") ||
                          WorkflowEntityTypeKeyMatches(item.EntityType.Name, "SupplierReturn") ||
                          string.Equals(
                              item.EntityType.EntityClassName,
                              typeof(SupplierReturn).FullName,
                              StringComparison.Ordinal))))
            {
                definition.IsActive = false;
                definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
                definition.RetiredAt = retiredAt;
                definition.RetiredById = null;
                definition.UpdatedAt = retiredAt;
                definition.UpdatedBy = "System";
                definition.LastModifiedById = null;
                retiredCount++;
            }

            return retiredCount;
        }

        private async Task EnsureVendorPaymentControlWorkflowSeededAsync(Guid tenantId)
        {
            const string definitionName = "Vendor Payment Approval";
            await EnsureWorkflowDefinitionSeededAsync(
                tenantId,
                entityCode: "VendorPayment",
                entityName: "Vendor Payment",
                entityClassName: typeof(VendorPayment).FullName,
                definitionName,
                description: "Direct supplier payment approval with effective-dated evidence, exception, and Managing Director authority controls.",
                approvalRoleNames: new[] { "Chief Accountant", "Managing Director" });

            var definition = await _context.WorkflowDefinitions
                .Include(item => item.EntityType)
                .Include(item => item.Steps)
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.Name == definitionName)
                .OrderByDescending(item => item.Version)
                .FirstOrDefaultAsync();
            if (definition == null)
                return;

            var now = DateTime.UtcNow;
            var changed = false;
            if (!definition.IsActive || definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
            {
                // A prior conformance migration retired the unused direct-payment route. This slice
                // supplies the missing domain submit/evidence controls, so the baseline route is now
                // intentionally re-published rather than creating a second payment approval system.
                definition.IsActive = true;
                definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
                definition.PublishedAt ??= now;
                definition.RetiredAt = null;
                definition.RetiredById = null;
                changed = true;
            }

            var approvalStep = definition.Steps
                .Where(item => item.StepType == WorkflowStepType.Approval && !item.IsDeleted)
                .OrderBy(item => item.Order)
                .FirstOrDefault();
            if (approvalStep != null)
            {
                var desired = BuildVendorPaymentStepConfigurationJson();
                if (!string.Equals(approvalStep.Configuration, desired, StringComparison.Ordinal))
                {
                    approvalStep.Configuration = desired;
                    approvalStep.Name = "Payment Control Approval";
                    approvalStep.Description = "Chief Accountant review with conditional Managing Director authority and named payment evidence.";
                    approvalStep.UpdatedAt = now;
                    approvalStep.UpdatedBy = "System";
                    changed = true;
                }
            }

            if (changed)
            {
                definition.UpdatedAt = now;
                definition.UpdatedBy = "System";
                await _context.SaveChangesAsync();
            }
        }

        private async Task EnsureApPaymentControlPoliciesSeededAsync(Guid tenantId)
        {
            var baseCurrency = await ResolveBaseCurrencyCodeAsync(tenantId);
            var now = DateTime.UtcNow;
            var effectiveFrom = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);
            var policies = new[]
            {
                new
                {
                    Code = "TDC-AP-PAYMENT-BASE",
                    Name = "TDC AP payment standard control",
                    Category = (string?)null,
                    Minimum = (decimal?)null,
                    Priority = 100,
                    RequiresMd = false,
                    Evidence = new[]
                    {
                        PaymentEvidence("payment-support", "Approved payment supporting pack", "PaymentSupport")
                    }
                },
                new
                {
                    Code = "TDC-AP-PAYMENT-CASH",
                    Name = "TDC AP cash payment control",
                    Category = (string?)VendorPaymentMethod.Cash.ToString(),
                    Minimum = (decimal?)null,
                    Priority = 200,
                    RequiresMd = false,
                    Evidence = new[]
                    {
                        PaymentEvidence("payment-support", "Approved payment supporting pack", "PaymentSupport"),
                        PaymentEvidence("cash-custody", "Cash custody and recipient acknowledgement", "CashCustody")
                    }
                },
                new
                {
                    Code = "TDC-AP-PAYMENT-MOBILE",
                    Name = "TDC AP mobile money payment control",
                    Category = (string?)VendorPaymentMethod.MobileMoney.ToString(),
                    Minimum = (decimal?)null,
                    Priority = 200,
                    RequiresMd = false,
                    Evidence = new[]
                    {
                        PaymentEvidence("payment-support", "Approved payment supporting pack", "PaymentSupport"),
                        PaymentEvidence("transaction-confirmation", "Mobile money transaction confirmation", "PaymentConfirmation")
                    }
                },
                new
                {
                    Code = "TDC-AP-PAYMENT-HIGH",
                    Name = "TDC high-value AP payment control",
                    Category = (string?)null,
                    Minimum = (decimal?)100_000m,
                    Priority = 300,
                    RequiresMd = true,
                    Evidence = new[]
                    {
                        PaymentEvidence("payment-support", "Approved payment supporting pack", "PaymentSupport"),
                        PaymentEvidence("high-value-authority", "High-value payment authority memorandum", "AuthorityMemo")
                    }
                },
                new
                {
                    Code = "TDC-AP-PAYMENT-HIGH-CASH",
                    Name = "TDC high-value cash AP payment control",
                    Category = (string?)VendorPaymentMethod.Cash.ToString(),
                    Minimum = (decimal?)100_000m,
                    Priority = 400,
                    RequiresMd = true,
                    Evidence = new[]
                    {
                        PaymentEvidence("payment-support", "Approved payment supporting pack", "PaymentSupport"),
                        PaymentEvidence("cash-custody", "Cash custody and recipient acknowledgement", "CashCustody"),
                        PaymentEvidence("high-value-authority", "High-value payment authority memorandum", "AuthorityMemo")
                    }
                }
            };

            foreach (var seed in policies)
            {
                var exists = await _context.WorkflowApprovalPolicySets.AnyAsync(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.Code == seed.Code);
                if (exists)
                    continue;

                var config = BuildVendorPaymentApprovalConfig(seed.RequiresMd, seed.Evidence);
                _context.WorkflowApprovalPolicySets.Add(new WorkflowApprovalPolicySet
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = seed.Code,
                    Name = seed.Name,
                    Description = "Seeded TDC Finance control. Amount bands are functional-currency equivalents; exceptional or evidence-exception requests always add Managing Director authority.",
                    Module = "Finance",
                    EntityType = "Vendor Payment",
                    Category = seed.Category,
                    MinimumAmount = seed.Minimum,
                    CurrencyCode = baseCurrency,
                    EffectiveFrom = effectiveFrom,
                    Priority = seed.Priority,
                    IsActive = true,
                    LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                    PublishedAt = now,
                    ApprovalConfiguration = JsonSerializer.Serialize(config, WorkflowSeedJsonOptions),
                    CreatedAt = now,
                    CreatedBy = "System"
                });
            }

            await _context.SaveChangesAsync();
        }

        private static WorkflowEvidenceRequirementDto PaymentEvidence(
            string key,
            string name,
            string documentType) => new()
        {
            RequirementKey = key,
            DocumentName = name,
            DocumentType = documentType,
            MinimumDocuments = 1,
            RequireVerification = true
        };

        private static WorkflowApprovalConfigDto BuildVendorPaymentApprovalConfig(
            bool requiresManagingDirector,
            IReadOnlyCollection<WorkflowEvidenceRequirementDto> evidenceRequirements)
        {
            return new WorkflowApprovalConfigDto
            {
                // Multiple requires every activated approval in the current sequential group.
                // Each group currently contains one role, while the explicit value keeps the
                // policy safe if TDC later adds joint approvers to either authority tier.
                ApprovalType = WorkflowApprovalType.Multiple,
                ActivationMode = WorkflowApprovalActivationMode.Sequential,
                MinApprovalsRequired = 1,
                RejectionHandling = WorkflowRejectionHandling.StopWorkflow,
                PreventInitiatorApproval = true,
                RequireDistinctApprovers = true,
                RequiresManagingDirectorApproval = requiresManagingDirector,
                ManagingDirectorApproverRole = "Managing Director",
                AllowEvidenceException = true,
                EvidenceExceptionApproverRole = "Managing Director",
                MinimumExceptionReasonLength = 30,
                EvidenceRequirements = evidenceRequirements.ToList(),
                ApproverRules = new List<WorkflowAssignmentRuleDto>
                {
                    new()
                    {
                        AssignmentType = WorkflowAssignmentType.Role,
                        Role = "Chief Accountant",
                        ApprovalGroup = 1,
                        Priority = 100
                    },
                    new()
                    {
                        AssignmentType = WorkflowAssignmentType.Role,
                        Role = "Managing Director",
                        ApprovalGroup = 2,
                        Priority = 90,
                        Condition = new WorkflowConditionDto
                        {
                            ConditionType = WorkflowConditionType.Expression,
                            Expression = "requiresManagingDirectorApproval == true"
                        }
                    }
                }
            };
        }

        private static string BuildVendorPaymentStepConfigurationJson()
        {
            var allUploadKeys = new[]
            {
                PaymentEvidence("payment-support", "Approved payment supporting pack", "PaymentSupport"),
                PaymentEvidence("cash-custody", "Cash custody and recipient acknowledgement", "CashCustody"),
                PaymentEvidence("transaction-confirmation", "Mobile money transaction confirmation", "PaymentConfirmation"),
                PaymentEvidence("high-value-authority", "High-value payment authority memorandum", "AuthorityMemo")
            };
            var configuration = new WorkflowStepConfigurationDto
            {
                ApprovalConfig = BuildVendorPaymentApprovalConfig(false, allUploadKeys),
                // These keys authorize uploads on the approval step. The applied payment-policy
                // snapshot decides which subset is mandatory; marking every key required here would
                // incorrectly force cash evidence on electronic payments.
                TaskConfig = new WorkflowTaskConfigDto
                {
                    TaskActionType = "payment-evidence",
                    RequiresDocument = false,
                    Instructions = "Attach documents against the requirement keys shown on the AP payment control card.",
                    DocumentRequirements = allUploadKeys.Select(item => new WorkflowDocumentRequirementDto
                    {
                        Id = item.RequirementKey,
                        RequirementKey = item.RequirementKey,
                        DocumentName = item.DocumentName,
                        DocumentType = item.DocumentType,
                        IsRequired = false
                    }).ToList()
                }
            };

            return JsonSerializer.Serialize(configuration, WorkflowSeedJsonOptions);
        }

        private static IReadOnlyList<FinanceWorkflowSeedSpec> GetFinanceWorkflowSeedSpecs()
        {
            return new List<FinanceWorkflowSeedSpec>
            {
                // General Ledger
                new("JournalEntry", "Journal Entry", typeof(JournalEntry).FullName, "Journal Entry Approval",
                    "Sequential finance journal approval: Accounts Officer review -> Finance Manager approval -> Financial Controller final approval."),
                new("JournalBatch", "Journal Batch", typeof(JournalBatch).FullName, "Journal Batch Approval",
                    "Batch-level journal approval with per-entry decisions, control totals, partial posting, and batch reversal controls."),

                // Accounts Payable
                new("FinancePurchaseOrder", "Finance Purchase Order", typeof(FinancePurchaseOrder).FullName, "Finance Purchase Order Approval",
                    "AP purchase order approval before supplier commitment, receiving, invoicing, or closure."),
                new("FinancePurchaseOrderReceipt", "Finance Goods Receipt", typeof(FinancePurchaseOrderReceipt).FullName, "Finance Goods Receipt Approval",
                    "Goods receipt approval before AP invoice matching and inventory/expense recognition."),
                new("VendorInvoice", "Vendor Invoice", typeof(VendorInvoice).FullName, "Accounts Payable Invoice Approval",
                    "Supplier invoice approval workflow for AP controls before payment or posting."),
                new("VendorInvoiceMatchException", "Vendor Invoice Match Exception", typeof(VendorInvoiceMatchException).FullName,
                    "Vendor Invoice Match Exception Approval",
                    "Independent AP-006 exception approval: Finance Manager approval -> Financial Controller final approval. This workflow authorizes a precise match variance only and never allocates or posts payment."),
                new("VendorPayment", "Vendor Payment", typeof(VendorPayment).FullName, "Vendor Payment Authorization",
                    "Manual supplier payment authorization before posting, clearing, or settlement finalization."),
                new("PaymentBatch", "Payment Batch", typeof(PaymentBatch).FullName, "Vendor Payment Batch Approval",
                    "Bulk supplier payment batch approval before processing."),
                // Finance owns the buyer-side commercial credit/AP correction only. This does
                // not lift the SupplierReturn integration quarantine or authorize stock dispatch.
                new("SupplierDebitNote", "Supplier Debit Note", typeof(SupplierDebitNote).FullName,
                    "Supplier Debit Note Approval",
                    "Independent Finance approval of the buyer-side AP debit note before central posting and settlement application."),

                // Accounts Receivable
                new("Quote", "Quotation", typeof(Quote).FullName, "Quotation Approval",
                    "Customer quotation approval before sending, acceptance, conversion, or expiry."),
                new("SalesOrder", "Sales Order", typeof(SalesOrder).FullName, "Sales Order Approval",
                    "Sales order approval before confirmation, delivery, invoicing, or cancellation."),
                new("DeliveryNote", "Delivery", typeof(DeliveryNote).FullName, "Delivery Approval",
                    "Delivery document approval before shipping, delivery confirmation, or stock issue."),
                new("Invoice", "Customer Invoice", typeof(Invoice).FullName, "Accounts Receivable Invoice Approval",
                    "Customer invoice approval workflow for controlled finalization, sending, or voiding."),
                new("ReturnOrder", "Customer Return", typeof(ReturnOrder).FullName, "Customer Return Approval",
                    "Customer return approval before receipt, inspection, credit note, or refund."),
                new("CreditNote", "Credit Note", typeof(CreditNote).FullName, "Credit Note Approval",
                    "Credit note approval before application, posting, or voiding."),
                new("CustomerPayment", "Customer Payment", typeof(CustomerPayment).FullName, "Customer Payment Approval",
                    "Customer payment approval before clearing, allocation, or reversal."),
                new("Refund", "Refund", typeof(Refund).FullName, "Customer Refund Approval",
                    "Customer refund approval before processing and payment reference capture."),

                // Budgeting and unit accounting
                new("BudgetScenario", "Budget Scenario", typeof(BudgetScenario).FullName, "Budget Scenario Approval",
                    "Budget scenario approval before locking, activation, or archival."),
                new("BudgetReturn", "Budget Return", typeof(BudgetReturn).FullName, "Budget Return Approval",
                    "Department budget worksheet approval workflow before consolidation."),
                new("FinanceBudgetOverride", "Finance Budget Override", typeof(FinanceBudgetOverrideRequest).FullName, "Finance Budget Override Approval",
                    "Independent Finance approval of a precise manual-journal budget shortfall. Approval is bound to the immutable evaluation hash and expires when the journal changes."),
                new("UnitJournalEntry", "Unit Journal Entry", typeof(UnitJournalEntry).FullName, "Unit Journal Entry Approval",
                    "Unit accounting journal approval before posting quantity balances."),
                new("UnitAccountBudget", "Unit Budget", typeof(UnitAccountBudget).FullName, "Unit Budget Approval",
                    "Unit budget approval before use in unit-account budget variance reporting."),
                new("AllocationRule", "Allocation", typeof(AllocationRule).FullName, "Allocation Rule Approval",
                    "Allocation rule approval before use in finance allocation runs."),
                new("AllocationRunBatch", "Allocation Run Batch", typeof(AllocationRunBatch).FullName, "Allocation Run Batch Approval",
                    "Controlled allocation run approval before posting generated allocation journals to GL."),

                // Cash and bank
                new("CashTransaction", "Bank Transaction", typeof(CashTransaction).FullName, "Bank Transaction Approval",
                    "Cash and bank transaction approval before posting, reconciliation, or clearing."),
                new("BankReconciliation", "Bank Reconciliation", typeof(BankReconciliation).FullName, "Bank Reconciliation Approval",
                    "Bank reconciliation approval for month-end bank sign-off."),
                new("OpeningBalanceBatch", "Opening Balance Batch", typeof(OpeningBalanceBatch).FullName, "Opening Balance Approval",
                    "Controlled migration opening-balance approval before posting to GL through the Finance posting engine."),

                // Fixed assets
                new("FixedAsset", "Fixed Asset", typeof(FixedAsset).FullName, "Fixed Asset Approval",
                    "Fixed asset registration approval before activation, depreciation, transfer, or disposal."),
                new("FixedAssetDepreciationRun", "Asset Depreciation Run", typeof(FixedAssetDepreciationRun).FullName, "Asset Depreciation Run Approval",
                    "Depreciation run approval before GL posting."),
                new("AssetValuation", "Asset Valuation", typeof(AssetValuation).FullName, "Asset Valuation Approval",
                    "Asset valuation, impairment, or revaluation approval before GL posting."),
                new("AssetTransfer", "Asset Transfer", typeof(AssetTransfer).FullName, "Asset Transfer Approval",
                    "Fixed asset transfer approval before completion."),
                new("AssetDisposal", "Asset Disposal", typeof(AssetDisposal).FullName, "Asset Disposal Approval",
                    "Fixed asset disposal approval before completion and GL posting."),
                new("AssetVerificationSession", "Asset Verification", typeof(AssetVerificationSession).FullName, "Asset Verification Approval",
                    "Asset verification session approval before completion is accepted."),
                new("CapitalProject", "Capital Project", typeof(CapitalProject).FullName, "Capital Project Approval",
                    "Capital project approval before capitalization, settlement, or closure."),
                new("LeaseContract", "Lease Contract", typeof(LeaseContract).FullName, "Lease Contract Approval",
                    "IFRS 16 lease contract approval before activation and recognition.")
            };
        }

        private async Task EnsureBusinessPartnerWorkflowsSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    await EnsureWorkflowDefinitionSeededAsync(
                        tenant.Id,
                        entityCode: "BusinessPartner",
                        entityName: "Business Partner",
                        entityClassName: typeof(BusinessPartner).FullName,
                        definitionName: "Business Partner Approval",
                        description: "Business partner onboarding workflow: Draft/PendingApproval -> PendingApproval -> Approved/Active.",
                        approvalRoleNames: new[] { "Finance Manager", "Financial Controller", Constants.Roles.Manager, Constants.Roles.SuperAdmin });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed business partner workflows");
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
            var constructionType = await EnsureProjectTypeAsync(
                tenantId,
                "CONSTRUCTION",
                "Construction Development",
                "Building and civil works projects covering design, approvals, procurement, construction, handover, and defects-liability follow-through.",
                requiresSponsor: true,
                requiresApproval: true);
            var renovationType = await EnsureProjectTypeAsync(
                tenantId,
                "RENOVATION_FITOUT",
                "Renovation / Fit-Out",
                "Renovation, refurbishment, and fit-out projects with phased handover and change-heavy execution.",
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

            var realEstatePortfolio = await _context.ProjectPortfolios
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == "PORTFOLIO-REAL-ESTATE");
            if (realEstatePortfolio == null)
            {
                realEstatePortfolio = new ProjectPortfolio
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = "PORTFOLIO-REAL-ESTATE",
                    Name = "Real Estate Development Portfolio",
                    Description = "Residential, commercial, and refurbishment developments managed with construction-specific controls.",
                    Status = "Active",
                    StrategicObjective = "Deliver Ghana construction projects with stronger package, unit, handover, and commercial governance.",
                    OwnerId = sponsor.Id,
                    SponsorId = sponsor.Id,
                    StartDate = now.Date.AddMonths(-1),
                    TargetEndDate = now.Date.AddMonths(18),
                    BudgetCap = 9500000m,
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                _context.ProjectPortfolios.Add(realEstatePortfolio);
            }

            var housingProgram = await _context.ProjectPrograms
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == "PROGRAM-ACCRA-HOUSING");
            if (housingProgram == null)
            {
                housingProgram = new ProjectProgram
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PortfolioId = realEstatePortfolio.Id,
                    Code = "PROGRAM-ACCRA-HOUSING",
                    Name = "Accra Housing Delivery Program",
                    Description = "Urban apartment and mixed-use developments with staged procurement, unit sales, and handover controls.",
                    Status = "Active",
                    ProgramManagerId = projectManager.Id,
                    SponsorId = sponsor.Id,
                    StartDate = now.Date.AddMonths(-1),
                    TargetEndDate = now.Date.AddMonths(15),
                    BudgetCap = 6200000m,
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                _context.ProjectPrograms.Add(housingProgram);
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

            var apartmentTemplate = await EnsureProjectTemplateAsync(
                tenantId,
                "TPL-GH-APARTMENT-MULTIUNIT",
                "Ghana Apartment Development - Multi Unit",
                "Recommended defaults for apartment developments delivered as multiple saleable units with phased commercialization and unit-by-unit handover.",
                constructionType.Id,
                "1.0",
                BuildGhanaApartmentMultiUnitTemplateDefinitionJson());

            var wholeBuildingTemplate = await EnsureProjectTemplateAsync(
                tenantId,
                "TPL-GH-WHOLE-BUILDING",
                "Ghana Whole-Building Development",
                "Recommended defaults for single whole-building developments such as offices, schools, warehouses, and owner-occupied facilities.",
                constructionType.Id,
                "1.0",
                BuildGhanaWholeBuildingTemplateDefinitionJson());

            var renovationTemplate = await EnsureProjectTemplateAsync(
                tenantId,
                "TPL-GH-RENOVATION-FITOUT",
                "Ghana Renovation / Fit-Out",
                "Recommended defaults for refurbishment, shell-and-core completion, and tenant fit-out projects.",
                renovationType.Id,
                "1.0",
                BuildGhanaRenovationFitOutTemplateDefinitionJson());

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

            settings.MandatoryFieldsByTypeJson = MergeProjectMandatoryFieldsByTypeJson(settings.MandatoryFieldsByTypeJson);

            await _context.SaveChangesAsync();
            await NormalizeProjectDemoFundingSourcesAsync(tenantId);
            var baseCurrencyCode = await ResolveBaseCurrencyCodeAsync(tenantId);

            var apartmentDeveloper = await EnsureApprovedBusinessPartnerAsync(
                tenantId,
                "CUST-GH-GOLDCOAST",
                "Golden Coast Homes Ltd",
                "Customer",
                sponsor.Id,
                "+233-302-100-200",
                "developments@goldencoasthomes.example",
                "Accra");

            var apartmentBuyerOne = await EnsureApprovedBusinessPartnerAsync(
                tenantId,
                "CUST-GH-AMA-MENSAH",
                "Ama Mensah",
                "Customer",
                sponsor.Id,
                "+233-244-001-101",
                "ama.mensah@example.com",
                "Accra");

            var apartmentBuyerTwo = await EnsureApprovedBusinessPartnerAsync(
                tenantId,
                "CUST-GH-KWESI-OWUSU",
                "Kwesi Owusu",
                "Customer",
                sponsor.Id,
                "+233-244-001-202",
                "kwesi.owusu@example.com",
                "Accra");

            var apartmentContractor = await EnsureApprovedBusinessPartnerAsync(
                tenantId,
                "CONT-GH-ADOM-BUILD",
                "Adom Construction Ltd",
                "Contractor",
                sponsor.Id,
                "+233-302-880-440",
                "tenders@adomconstruction.example",
                "Accra");

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
                    baseCurrencyCode,
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
                    baseCurrencyCode,
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
                    baseCurrencyCode,
                    now));

            await EnsureProjectDemoSeededAsync(
                tenantId,
                "PRJ-DEMO-2001",
                () => CreateAccraApartmentDevelopmentProject(
                    tenantId,
                    constructionType,
                    highPriority,
                    apartmentTemplate,
                    realEstatePortfolio,
                    housingProgram,
                    sponsor,
                    projectManager,
                    financeOwner,
                    teamMember,
                    department,
                    location,
                    apartmentDeveloper,
                    apartmentBuyerOne,
                    apartmentBuyerTwo,
                    apartmentContractor,
                    baseCurrencyCode,
                    now));
            try
            {
                await EnsureAccraApartmentDevelopmentPhasePackagesSeededAsync(tenantId, baseCurrencyCode, now);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                _logger.LogWarning(
                    "Skipping apartment development phase package seed because project schema appears behind code (missing columns). Apply latest migrations and rerun seeding.");
            }

            await EnsureProjectDemoInterdependenciesSeededAsync(tenantId, projectManager, financeOwner, now);
            await EnsureProjectDemoQualityDataSeededAsync(tenantId, sponsor, financeOwner, teamMember, now);
            try
            {
                await EnsureProjectDemoProcurementAndMaterialDataSeededAsync(tenantId, customer, financeOwner, department, now);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping project procurement/material demo seed because project schema appears behind code (missing columns). Apply latest migrations and rerun seeding.");
            }
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

        private async Task EnsureAccraApartmentDevelopmentPhasePackagesSeededAsync(Guid tenantId, string baseCurrencyCode, DateTime now)
        {
            var project = await _context.Projects
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ProjectCode == "PRJ-DEMO-2001")
                .Select(item => new
                {
                    item.Id,
                    item.BaseCurrencyCode
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return;
            }

            var phases = await _context.ProjectPhases
                .Where(item => item.TenantId == tenantId && item.ProjectId == project.Id && !item.IsDeleted)
                .ToListAsync();

            if (phases.Count == 0)
            {
                return;
            }

            var phaseByCode = phases
                .Where(item => !string.IsNullOrWhiteSpace(item.Code))
                .ToDictionary(item => item.Code!, StringComparer.OrdinalIgnoreCase);

            var existingPackages = await _context.ProjectPackages
                .Where(item => item.TenantId == tenantId && item.ProjectId == project.Id && !item.IsDeleted)
                .ToListAsync();

            var packageByCode = existingPackages
                .Where(item => !string.IsNullOrWhiteSpace(item.Code))
                .ToDictionary(item => item.Code!, StringComparer.OrdinalIgnoreCase);

            var existingBoqItems = await _context.ProjectBoqItems
                .Where(item => item.TenantId == tenantId && item.ProjectId == project.Id && !item.IsDeleted)
                .ToListAsync();

            var boqItemCodes = existingBoqItems
                .Where(item => !string.IsNullOrWhiteSpace(item.ItemCode))
                .Select(item => item.ItemCode!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var defaultPartnerId = existingPackages
                .Where(item => item.BusinessPartnerId.HasValue)
                .Select(item => item.BusinessPartnerId!.Value)
                .FirstOrDefault();

            var currencyCode = string.IsNullOrWhiteSpace(project.BaseCurrencyCode) ? baseCurrencyCode : project.BaseCurrencyCode!;
            var createdPackageCount = 0;
            var createdBoqCount = 0;
            var nextSortOrder = existingPackages.Count == 0 ? 100 : existingPackages.Max(item => item.SortOrder) + 10;

            ProjectPackage EnsurePackage(
                string packageCode,
                string phaseCode,
                string name,
                string status,
                decimal budgetAmount,
                decimal? committedAmount,
                decimal? actualAmount,
                decimal? forecastAmount,
                string description,
                string notes)
            {
                if (packageByCode.TryGetValue(packageCode, out var existingPackage))
                {
                    return existingPackage;
                }

                if (!phaseByCode.TryGetValue(phaseCode, out var phase))
                {
                    throw new InvalidOperationException($"Phase {phaseCode} was not found while seeding demo package coverage for PRJ-DEMO-2001.");
                }

                var package = new ProjectPackage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = project.Id,
                    ProjectPhaseId = phase.Id,
                    Code = packageCode,
                    Name = name,
                    Description = description,
                    PackageType = ProjectPackageTypes.WorkPackage,
                    Status = status,
                    SortOrder = nextSortOrder,
                    ProcurementRoute = "Traditional",
                    ContractStrategy = "SubcontractPackages",
                    BusinessPartnerId = defaultPartnerId == Guid.Empty ? null : defaultPartnerId,
                    BudgetAmount = budgetAmount,
                    CommittedAmount = committedAmount,
                    ActualAmount = actualAmount,
                    ForecastAmount = forecastAmount,
                    Currency = currencyCode,
                    Notes = notes,
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                nextSortOrder += 10;
                createdPackageCount++;
                packageByCode[packageCode] = package;
                _context.ProjectPackages.Add(package);
                return package;
            }

            void EnsureBoq(
                string packageCode,
                string lineNumber,
                string itemCode,
                string description,
                decimal quantity,
                string unitOfMeasure,
                decimal? unitRate,
                decimal budgetAmount,
                decimal? committedAmount,
                decimal? actualAmount,
                decimal? forecastAmount,
                string? notes = null)
            {
                if (boqItemCodes.Contains(itemCode))
                {
                    return;
                }

                if (!packageByCode.TryGetValue(packageCode, out var package))
                {
                    throw new InvalidOperationException($"Package {packageCode} was not found while seeding BOQ coverage for PRJ-DEMO-2001.");
                }
                var sortOrder = int.TryParse(lineNumber, out var parsedLineNumber) ? Math.Max(0, parsedLineNumber - 1) : 0;

                _context.ProjectBoqItems.Add(new ProjectBoqItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = project.Id,
                    ProjectPackageId = package.Id,
                    LineNumber = lineNumber,
                    ItemCode = itemCode,
                    ItemType = ProjectBoqItemTypes.Item,
                    Description = description,
                    Quantity = quantity,
                    UnitOfMeasure = unitOfMeasure,
                    UnitRate = unitRate,
                    BudgetAmount = budgetAmount,
                    CommittedAmount = committedAmount,
                    ActualAmount = actualAmount,
                    ForecastAmount = forecastAmount,
                    Currency = currencyCode,
                    Notes = notes,
                    SortOrder = sortOrder,
                    CreatedAt = now,
                    CreatedBy = "System"
                });

                boqItemCodes.Add(itemCode);
                createdBoqCount++;
            }

            EnsurePackage(
                "PKG-FEAS",
                "FEASIBILITY",
                "Feasibility & Site Due Diligence",
                ProjectPackageStatuses.Completed,
                118000m,
                118000m,
                116500m,
                116500m,
                "Topographic survey, geotechnical investigation, feasibility reviews, and preliminary commercial studies.",
                "Closed out during the initial go/no-go and land due-diligence cycle.");
            EnsurePackage(
                "PKG-CONCEPT",
                "CONCEPT_DESIGN",
                "Concept Design Coordination",
                ProjectPackageStatuses.Completed,
                176000m,
                176000m,
                172400m,
                172400m,
                "Architectural concept options, massing coordination, and elemental cost alignment.",
                "Approved concept set used to launch scheme design and buyer mix planning.");
            EnsurePackage(
                "PKG-DDETAIL",
                "DETAILED_DESIGN",
                "Detailed Design & IFC Documentation",
                ProjectPackageStatuses.Completed,
                264000m,
                264000m,
                259800m,
                259800m,
                "Detailed architectural, structural, and MEP documentation including coordinated IFC issue and BOQ support.",
                "Issued-for-construction set closed and superseded by current as-built revision control.");
            EnsurePackage(
                "PKG-APPROVAL",
                "APPROVALS",
                "Statutory Approvals & Utility Clearances",
                ProjectPackageStatuses.Completed,
                92000m,
                92000m,
                88750m,
                88750m,
                "Permit submissions, utility applications, authority fees, and compliance follow-through.",
                "Occupancy certificate remains on the approval register, but the main approvals package is substantially complete.");
            EnsurePackage(
                "PKG-PROC",
                "PROCUREMENT",
                "Tendering, Awards & Package Procurement",
                ProjectPackageStatuses.Completed,
                138000m,
                132500m,
                126400m,
                133900m,
                "Tender preparation, bid evaluation, package awards, and mobilisation procurement planning.",
                "Main trade packages have already been awarded and transitioned into execution.");
            EnsurePackage(
                "PKG-COMM",
                "COMMISSIONING",
                "Commissioning & Systems Testing",
                ProjectPackageStatuses.Active,
                148000m,
                133000m,
                58100m,
                152600m,
                "Common services commissioning, lift witness testing, fire alarm integration, and authority witness support.",
                "Closely tied to common-area energisation and batch handover readiness.");
            EnsurePackage(
                "PKG-HAND",
                "HANDOVER",
                "Phased Unit Handover & Closeout",
                ProjectPackageStatuses.Active,
                104000m,
                64200m,
                28150m,
                109500m,
                "Unit readiness walks, handover packs, client inspections, and closeout documentation by release batch.",
                "Batch 1 is active and later release batches remain forecast-driven.");
            EnsurePackage(
                "PKG-DLP",
                "DEFECTS_LIABILITY",
                "Defects Response & Warranty Support",
                ProjectPackageStatuses.Active,
                68000m,
                22000m,
                8450m,
                68000m,
                "Early defects-response cover, warranty coordination, and retained closeout support for handed-over units.",
                "Active for the units already handed over under the phased turnover model.");

            EnsureBoq("PKG-FEAS", "1", "FEAS-SITE", "Geotechnical investigation, topographic survey, and feasibility reporting", 1m, "LS", 118000m, 118000m, 118000m, 116500m, 116500m);
            EnsureBoq("PKG-CONCEPT", "1", "CONCEPT-ARCH", "Concept design studies, space planning, and elemental cost plan coordination", 1m, "LS", 176000m, 176000m, 176000m, 172400m, 172400m);
            EnsureBoq("PKG-DDETAIL", "1", "DETAIL-IFC", "Coordinated IFC drawings, design calculations, and final BOQ support", 1m, "LS", 264000m, 264000m, 264000m, 259800m, 259800m);
            EnsureBoq("PKG-APPROVAL", "1", "APPROVAL-STAT", "Statutory submissions, permit fees, and utility clearance follow-up", 1m, "LS", 92000m, 92000m, 92000m, 88750m, 88750m);
            EnsureBoq("PKG-PROC", "1", "PROC-TENDER", "Tendering, evaluations, award documentation, and supplier onboarding", 1m, "LS", 138000m, 138000m, 132500m, 126400m, 133900m);
            EnsureBoq("PKG-COMM", "1", "COMM-SYS", "System testing, lift witness activities, and integrated commissioning closeout", 1m, "LS", 148000m, 148000m, 133000m, 58100m, 152600m);
            EnsureBoq("PKG-HAND", "1", "HAND-B1", "Batch handover inspections, O&M pack issue, and client closeout walkthroughs", 1m, "LS", 104000m, 104000m, 64200m, 28150m, 109500m);
            EnsureBoq("PKG-DLP", "1", "DLP-RESP", "Defects response mobilisation, warranty coordination, and rectification cover", 1m, "LS", 68000m, 68000m, 22000m, 8450m, 68000m);

            if (createdPackageCount == 0 && createdBoqCount == 0)
            {
                return;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Seeded {PackageCount} additional phase package(s) and {BoqCount} BOQ line(s) for project PRJ-DEMO-2001 in tenant {TenantId}.",
                createdPackageCount,
                createdBoqCount,
                tenantId);
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

        private async Task<ProjectTemplate> EnsureProjectTemplateAsync(
            Guid tenantId,
            string code,
            string name,
            string description,
            Guid? projectTypeId,
            string versionLabel,
            string templateDefinitionJson)
        {
            var entity = await _context.ProjectTemplates
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.Code == code);

            if (entity == null)
            {
                entity = new ProjectTemplate
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = code,
                    Name = name,
                    Description = description,
                    ProjectTypeId = projectTypeId,
                    VersionLabel = versionLabel,
                    TemplateDefinitionJson = templateDefinitionJson,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.ProjectTemplates.Add(entity);
                return entity;
            }

            entity.Name = name;
            entity.Description = description;
            entity.ProjectTypeId = projectTypeId;
            entity.VersionLabel = versionLabel;
            entity.TemplateDefinitionJson = templateDefinitionJson;
            entity.IsActive = true;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = "System";
            return entity;
        }

        private async Task<BusinessPartner> EnsureApprovedBusinessPartnerAsync(
            Guid tenantId,
            string code,
            string name,
            string partnerType,
            Guid approvedById,
            string phone,
            string email,
            string city)
        {
            var entity = await _context.BusinessPartners
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted && item.PartnerCode == code);

            if (entity == null)
            {
                entity = new BusinessPartner
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PartnerCode = code,
                    PartnerName = name,
                    LegalName = name,
                    PartnerType = partnerType,
                    PrimaryPhone = phone,
                    PrimaryEmail = email,
                    PhysicalCity = city,
                    PhysicalCountry = "Ghana",
                    RegistrationStatus = "Approved",
                    ApprovalStatus = "Approved",
                    ApprovedById = approvedById,
                    ApprovedDate = DateTime.UtcNow.AddDays(-45),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.BusinessPartners.Add(entity);
                return entity;
            }

            entity.PartnerName = name;
            entity.LegalName = name;
            entity.PartnerType = partnerType;
            entity.PrimaryPhone = phone;
            entity.PrimaryEmail = email;
            entity.PhysicalCity = city;
            entity.PhysicalCountry = "Ghana";
            entity.RegistrationStatus = "Approved";
            entity.ApprovalStatus = "Approved";
            entity.ApprovedById = approvedById;
            entity.ApprovedDate ??= DateTime.UtcNow.AddDays(-45);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = "System";
            return entity;
        }

        private static string MergeProjectMandatoryFieldsByTypeJson(string? currentJson)
        {
            Dictionary<string, List<string>> settings;
            try
            {
                settings = string.IsNullOrWhiteSpace(currentJson)
                    ? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(currentJson) ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                settings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }

            settings["IMPLEMENTATION"] = new[] { "SponsorId", "ProjectManagerId", "EstimatedBudget", "StartDate", "TargetEndDate" }.ToList();
            settings["INTERNAL"] = new[] { "SponsorId", "ProjectManagerId", "StartDate" }.ToList();
            settings["CAPEX"] = new[] { "SponsorId", "EstimatedBudget", "FundingSource" }.ToList();
            settings["CONSTRUCTION"] = new[] { "SponsorId", "ProjectManagerId", "EstimatedBudget", "StartDate", "TargetEndDate", "FundingSource" }.ToList();
            settings["RENOVATION_FITOUT"] = new[] { "SponsorId", "ProjectManagerId", "StartDate", "TargetEndDate", "BusinessPartnerId" }.ToList();

            return JsonSerializer.Serialize(settings);
        }

        private static string BuildGhanaApartmentMultiUnitTemplateDefinitionJson()
            => JsonSerializer.Serialize(new
            {
                developmentProfile = new
                {
                    deliveryStructure = ProjectDeliveryStructures.MultiUnit,
                    developmentType = "Residential",
                    procurementRoute = "Traditional",
                    contractStrategy = "SubcontractPackages",
                    handoverStrategy = "UnitByUnitHandover",
                    fundingArrangement = "Developer Equity + Off-plan Sales",
                    notes = "Recommended Ghana apartment-development defaults with package procurement, phased unit release, and unit-by-unit handover."
                },
                projectPhases = new object[]
                {
                    new { code = "FEASIBILITY", name = "Feasibility", isStageGateRequired = true },
                    new { code = "CONCEPT_DESIGN", name = "Concept Design", isStageGateRequired = true },
                    new { code = "DETAILED_DESIGN", name = "Detailed Design", isStageGateRequired = true },
                    new { code = "APPROVALS", name = "Approvals & Permits", isStageGateRequired = true },
                    new { code = "PROCUREMENT", name = "Procurement", isStageGateRequired = true },
                    new { code = "CONSTRUCTION", name = "Construction", isStageGateRequired = false },
                    new { code = "COMMISSIONING", name = "Testing & Commissioning", isStageGateRequired = true },
                    new { code = "HANDOVER", name = "Handover", isStageGateRequired = true },
                    new { code = "DEFECTS_LIABILITY", name = "Defects Liability", isStageGateRequired = false }
                },
                workItems = new object[]
                {
                    new { nodeType = ProjectWorkItemNodeTypes.Phase, title = "Mobilize design consultants", status = "New", priority = "High" },
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Prepare apartment unit mix and floor schedule", status = "New", priority = "High" },
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Prepare package procurement strategy", status = "New", priority = "Normal" },
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Plan phased unit release and handover readiness", status = "New", priority = "Normal" }
                },
                milestones = new object[]
                {
                    new { title = "Planning approval secured", status = "Draft", requiresApproval = true },
                    new { title = "Main contract award completed", status = "Draft", requiresApproval = true },
                    new { title = "Practical completion for first units", status = "Draft", requiresApproval = true },
                    new { title = "First unit handover completed", status = "Draft", requiresApproval = true }
                }
            });

        private static string BuildGhanaWholeBuildingTemplateDefinitionJson()
            => JsonSerializer.Serialize(new
            {
                developmentProfile = new
                {
                    deliveryStructure = ProjectDeliveryStructures.WholeDevelopment,
                    developmentType = "Commercial",
                    procurementRoute = "Traditional",
                    contractStrategy = "LumpSum",
                    handoverStrategy = "SingleHandover",
                    fundingArrangement = "Customer Contract / Capex Funding",
                    notes = "Recommended defaults for whole-building delivery such as schools, offices, warehouses, and owner-occupied facilities."
                },
                projectPhases = new object[]
                {
                    new { code = "FEASIBILITY", name = "Feasibility", isStageGateRequired = true },
                    new { code = "CONCEPT_DESIGN", name = "Concept Design", isStageGateRequired = true },
                    new { code = "DETAILED_DESIGN", name = "Detailed Design", isStageGateRequired = true },
                    new { code = "APPROVALS", name = "Approvals & Permits", isStageGateRequired = true },
                    new { code = "PROCUREMENT", name = "Procurement", isStageGateRequired = true },
                    new { code = "CONSTRUCTION", name = "Construction", isStageGateRequired = false },
                    new { code = "COMMISSIONING", name = "Testing & Commissioning", isStageGateRequired = true },
                    new { code = "HANDOVER", name = "Handover", isStageGateRequired = true },
                    new { code = "DEFECTS_LIABILITY", name = "Defects Liability", isStageGateRequired = false }
                },
                workItems = new object[]
                {
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Confirm client brief and building performance targets", status = "New", priority = "High" },
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Prepare whole-building procurement and contract plan", status = "New", priority = "Normal" },
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Plan integrated commissioning and final handover", status = "New", priority = "Normal" }
                },
                milestones = new object[]
                {
                    new { title = "Building permit approved", status = "Draft", requiresApproval = true },
                    new { title = "Contract execution complete", status = "Draft", requiresApproval = true },
                    new { title = "Practical completion achieved", status = "Draft", requiresApproval = true }
                }
            });

        private static string BuildGhanaRenovationFitOutTemplateDefinitionJson()
            => JsonSerializer.Serialize(new
            {
                developmentProfile = new
                {
                    deliveryStructure = ProjectDeliveryStructures.WholeDevelopment,
                    developmentType = "Renovation",
                    procurementRoute = "Negotiated",
                    contractStrategy = "MeasuredWorks",
                    handoverStrategy = "PhasedHandover",
                    fundingArrangement = "Client Budget / Fit-Out Allowance",
                    notes = "Recommended defaults for renovation and fit-out work with phased handover, high change frequency, and measurement-driven commercial control."
                },
                projectPhases = new object[]
                {
                    new { code = "FEASIBILITY", name = "Feasibility", isStageGateRequired = true, isOptional = true },
                    new { code = "CONCEPT_DESIGN", name = "Concept Design", isStageGateRequired = true },
                    new { code = "DETAILED_DESIGN", name = "Detailed Design", isStageGateRequired = true },
                    new { code = "APPROVALS", name = "Approvals & Permits", isStageGateRequired = false },
                    new { code = "PROCUREMENT", name = "Procurement", isStageGateRequired = true },
                    new { code = "CONSTRUCTION", name = "Construction", isStageGateRequired = false },
                    new { code = "COMMISSIONING", name = "Testing & Commissioning", isStageGateRequired = true },
                    new { code = "HANDOVER", name = "Handover", isStageGateRequired = true }
                },
                workItems = new object[]
                {
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Survey existing condition and confirm demolition scope", status = "New", priority = "High" },
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Freeze finish selections and client alterations process", status = "New", priority = "High" },
                    new { nodeType = ProjectWorkItemNodeTypes.Task, title = "Prepare phased handover plan by work zone", status = "New", priority = "Normal" }
                },
                milestones = new object[]
                {
                    new { title = "Existing-condition sign-off complete", status = "Draft", requiresApproval = true },
                    new { title = "Fit-out package award complete", status = "Draft", requiresApproval = true },
                    new { title = "Zone handover achieved", status = "Draft", requiresApproval = true }
                }
            });

        private async Task NormalizeProjectDemoFundingSourcesAsync(Guid tenantId)
        {
            List<Project> projects;
            try
            {
                projects = await _context.Projects
                    .Where(project =>
                        project.TenantId == tenantId
                        && !project.IsDeleted
                        && (project.ProjectCode == "PRJ-DEMO-1001" || project.ProjectCode == "PRJ-DEMO-1002" || project.ProjectCode == "PRJ-DEMO-1003"))
                    .ToListAsync();
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                _logger.LogWarning(
                    "Skipping demo project funding-source normalization because project schema appears behind code (missing columns). Apply latest migrations and rerun seeding.");
                return;
            }

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
            var baseCurrencyCode = await ResolveBaseCurrencyCodeAsync(tenantId);

            var effectiveDepartment = department
                ?? await _context.Departments
                    .AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
                    .OrderBy(item => item.Name)
                    .FirstOrDefaultAsync();
            if (effectiveDepartment == null)
            {
                effectiveDepartment = new Department
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = "Project Delivery",
                    Code = "PRJ-DEL",
                    Description = "Fallback seeded department for project procurement and material demo flows.",
                    DepartmentType = DepartmentType.Operations,
                    IsActive = true,
                    Color = "#2563EB",
                    Icon = "briefcase",
                    CreatedAt = now,
                    CreatedBy = "System"
                };

                _context.Departments.Add(effectiveDepartment);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded fallback active department {DepartmentCode} for tenant {TenantId} so project procurement/material demo data can be created.", effectiveDepartment.Code, tenantId);
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
                    Currency = baseCurrencyCode,
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

                _context.PurchaseRequisitions.Add(new PurchaseRequisition
                {
                    Id = orderedPrId,
                    TenantId = tenantId,
                    RequisitionNumber = "PR-DEMO-1001-B",
                    RequisitionDate = now.AddDays(-12),
                    RequestedById = financeOwner.Id,
                    RequiredDate = now.Date.AddDays(4),
                    Status = "Approved",
                    Priority = "High",
                    Department = effectiveDepartment.Name,
                    Justification = "Mobile scanners needed for inventory validation and warehouse cutover.",
                    RequisitionType = PurchaseRequisitionType.ProjectPurchase,
                    ProjectId = implementationProject.Id,
                    ProjectCode = implementationProject.ProjectCode,
                    ProjectName = implementationProject.Title,
                    Currency = baseCurrencyCode,
                    PreferredBusinessPartnerId = customer.Id,
                    ApprovedById = financeOwner.Id,
                    ApprovedAt = now.AddDays(-11),
                    TotalAmount = 4500m,
                    CreatedAt = now.AddDays(-12),
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
                    Currency = baseCurrencyCode,
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
                    Currency = baseCurrencyCode,
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

        private void CreateAccraApartmentDevelopmentProject(
            Guid tenantId,
            ProjectType constructionType,
            ProjectPriority highPriority,
            ProjectTemplate apartmentTemplate,
            ProjectPortfolio realEstatePortfolio,
            ProjectProgram housingProgram,
            ApplicationUser sponsor,
            ApplicationUser projectManager,
            ApplicationUser financeOwner,
            ApplicationUser teamMember,
            Department? department,
            Location? location,
            BusinessPartner apartmentDeveloper,
            BusinessPartner apartmentBuyerOne,
            BusinessPartner apartmentBuyerTwo,
            BusinessPartner apartmentContractor,
            string baseCurrencyCode,
            DateTime now)
        {
            var projectId = Guid.NewGuid();
            var project = new Project
            {
                Id = projectId,
                TenantId = tenantId,
                ProjectCode = "PRJ-DEMO-2001",
                Title = "Airport Hills Residences Block A",
                Summary = "Six-floor apartment development in Accra with 24 apartments, phased release, buyer variations, and unit-by-unit handover.",
                BusinessCase = "Deliver a commercially viable residential block with controlled package procurement, phased unit sales, and post-handover support.",
                Objectives = "Complete Block A, commission services, release apartments to market, and manage buyer changes through closeout.",
                StrategicAlignment = "Residential development growth and phased property sales",
                ProjectTypeId = constructionType.Id,
                ProjectPriorityId = highPriority.Id,
                TemplateId = apartmentTemplate.Id,
                PortfolioId = realEstatePortfolio.Id,
                ProgramId = housingProgram.Id,
                Status = ProjectStatuses.InProgress,
                Methodology = "Waterfall",
                SponsorId = sponsor.Id,
                ProjectManagerId = projectManager.Id,
                DepartmentId = department?.Id,
                LocationId = location?.Id,
                CustomerId = apartmentDeveloper.Id,
                BusinessPartnerId = apartmentDeveloper.Id,
                StartDate = now.Date.AddMonths(-8),
                TargetEndDate = now.Date.AddMonths(6),
                ActualStartDate = now.Date.AddMonths(-8).AddDays(5),
                EstimatedBudget = 6200000m,
                ApprovedBudget = 6450000m,
                ActualCost = 4685000m,
                BudgetStatus = "Approved",
                ProgressPercent = 74m,
                ApprovalRequired = true,
                SubmittedAt = now.AddMonths(-9),
                ApprovedAt = now.AddMonths(-9).AddDays(4),
                ScopeStatement = "Construct Block A, commission common services, market apartments, support buyer alterations, and hand over units progressively.",
                Assumptions = "Statutory approvals remain valid and buyer finish selections are frozen by release batch.",
                Constraints = "Lift certification, utility energisation, and buyer finish changes sit on the critical path.",
                ExpectedBenefits = "Earlier unit sales, cleaner package cost tracking, and stronger post-handover support records.",
                FundingSource = "Developer Equity + Off-plan Sales",
                StatusRemarks = "Structure is substantially complete, finishes are active, and phased handover has started for early units.",
                ExternalPortalAccessEnabled = true,
                ExternalCollaborationEnabled = true,
                CreatedAt = now.AddMonths(-9),
                CreatedBy = "System"
            };

            _context.Projects.Add(project);
            _context.ProjectDevelopmentProfiles.Add(new ProjectDevelopmentProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                DeliveryStructure = ProjectDeliveryStructures.MultiUnit,
                DevelopmentType = "Residential",
                SiteName = "Airport Hills, Block A",
                SiteAddress = "Airport Hills Enclave, Accra, Ghana",
                LandReference = "AH/RES/BLK-A/2026",
                ProcurementRoute = "Traditional",
                ContractStrategy = "SubcontractPackages",
                ConsultantTeam = "ArchPlan Studio, Volta Structures, Prime MEP Consult",
                FundingArrangement = "Developer Equity + Off-plan Sales",
                HandoverStrategy = "UnitByUnitHandover",
                Notes = "Seeded Ghana apartment scenario with phased commercialization, buyer variations, commissioning, and defects control.",
                CreatedAt = now.AddMonths(-9),
                CreatedBy = "System"
            });

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
                Notes = "Initial approved residential development brief and baseline.",
                CreatedAt = now.AddMonths(-9).AddDays(4),
                CreatedBy = "System"
            });

            ProjectPhase MakePhase(string code, string name, string status, int sortOrder, bool stageGate, bool optional, DateTime? plannedStart, DateTime? plannedEnd, DateTime? actualStart, DateTime? actualEnd)
                => new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    Code = code,
                    Name = name,
                    Status = status,
                    SortOrder = sortOrder,
                    IsOptional = optional,
                    IsStageGateRequired = stageGate,
                    IsTemplateSeeded = true,
                    PlannedStartDate = plannedStart,
                    PlannedEndDate = plannedEnd,
                    ActualStartDate = actualStart,
                    ActualEndDate = actualEnd,
                    CreatedAt = now.AddMonths(-9).AddDays(6 + sortOrder),
                    CreatedBy = "System"
                };

            var phases = new[]
            {
                MakePhase("FEASIBILITY", "Feasibility", ProjectPhaseStatuses.Completed, 0, true, false, now.Date.AddMonths(-10), now.Date.AddMonths(-9).AddDays(5), now.Date.AddMonths(-10), now.Date.AddMonths(-9).AddDays(2)),
                MakePhase("CONCEPT_DESIGN", "Concept Design", ProjectPhaseStatuses.Completed, 1, true, false, now.Date.AddMonths(-9).AddDays(3), now.Date.AddMonths(-8).AddDays(8), now.Date.AddMonths(-9).AddDays(4), now.Date.AddMonths(-8).AddDays(10)),
                MakePhase("DETAILED_DESIGN", "Detailed Design", ProjectPhaseStatuses.Completed, 2, true, false, now.Date.AddMonths(-8).AddDays(9), now.Date.AddMonths(-6).AddDays(12), now.Date.AddMonths(-8).AddDays(10), now.Date.AddMonths(-6).AddDays(15)),
                MakePhase("APPROVALS", "Approvals & Permits", ProjectPhaseStatuses.Completed, 3, true, false, now.Date.AddMonths(-7), now.Date.AddMonths(-5).AddDays(20), now.Date.AddMonths(-7).AddDays(3), now.Date.AddMonths(-5).AddDays(25)),
                MakePhase("PROCUREMENT", "Procurement", ProjectPhaseStatuses.Completed, 4, true, false, now.Date.AddMonths(-6).AddDays(5), now.Date.AddMonths(-3).AddDays(10), now.Date.AddMonths(-6).AddDays(7), now.Date.AddMonths(-3).AddDays(2)),
                MakePhase("CONSTRUCTION", "Construction", ProjectPhaseStatuses.InProgress, 5, false, false, now.Date.AddMonths(-5), now.Date.AddMonths(4), now.Date.AddMonths(-5).AddDays(3), null),
                MakePhase("COMMISSIONING", "Testing & Commissioning", ProjectPhaseStatuses.InProgress, 6, true, false, now.Date.AddDays(-14), now.Date.AddMonths(3), now.Date.AddDays(-10), null),
                MakePhase("HANDOVER", "Handover", ProjectPhaseStatuses.InProgress, 7, true, false, now.Date.AddDays(-7), now.Date.AddMonths(4), now.Date.AddDays(-4), null),
                MakePhase("DEFECTS_LIABILITY", "Defects Liability", ProjectPhaseStatuses.InProgress, 8, false, false, now.Date.AddDays(-3), now.Date.AddMonths(16), now.Date.AddDays(-2), null)
            };

            _context.ProjectPhases.AddRange(phases);
            var phaseByCode = phases.ToDictionary(item => item.Code!, StringComparer.OrdinalIgnoreCase);

            ProjectPackage MakePackage(string code, string name, string status, int sortOrder, decimal budget, decimal? committed, decimal? actual, decimal? forecast, string description, string notes)
                => new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectPhaseId = phaseByCode["CONSTRUCTION"].Id,
                    Code = code,
                    Name = name,
                    Description = description,
                    PackageType = ProjectPackageTypes.TradePackage,
                    Status = status,
                    SortOrder = sortOrder,
                    ProcurementRoute = "Traditional",
                    ContractStrategy = "SubcontractPackages",
                    BusinessPartnerId = apartmentContractor.Id,
                    BudgetAmount = budget,
                    CommittedAmount = committed,
                    ActualAmount = actual,
                    ForecastAmount = forecast,
                    Currency = baseCurrencyCode,
                    Notes = notes,
                    CreatedAt = now.AddMonths(-5).AddDays(sortOrder),
                    CreatedBy = "System"
                };

            var packages = new[]
            {
                MakePackage("PKG-SUB", "Substructure", ProjectPackageStatuses.Completed, 0, 820000m, 820000m, 810500m, 810500m, "Foundations, retaining walls, and ground beams.", "Completed and certified in the prior valuation cycle."),
                MakePackage("PKG-SUP", "Superstructure", ProjectPackageStatuses.Active, 1, 1650000m, 1625000m, 1510000m, 1668000m, "RC frame, blockwork, roofing, and shell completion.", "Final roof waterproofing and stair-core finishes remain open."),
                MakePackage("PKG-ELEC", "Electrical & ELV", ProjectPackageStatuses.Active, 2, 540000m, 470000m, 281000m, 552000m, "Power, lighting, fire alarm, and access control.", "Common-area testing has started ahead of final lift energisation."),
                MakePackage("PKG-PLUMB", "Plumbing & Drainage", ProjectPackageStatuses.Active, 3, 425000m, 362000m, 238500m, 432000m, "Water supply, drainage, sanitary fixtures, and pumps.", "Upper-floor fixture installation and pump calibration are in progress."),
                MakePackage("PKG-FIN", "Finishes & Joinery", ProjectPackageStatuses.Active, 4, 910000m, 708000m, 468500m, 936000m, "Internal finishes, tiling, kitchens, wardrobes, and painting.", "Buyer finish changes are controlled through the customer variation log."),
                MakePackage("PKG-EXT", "External Works & Landscaping", ProjectPackageStatuses.ProcurementPending, 5, 265000m, null, null, 278000m, "Boundary wall, paving, drainage tie-ins, and landscaping.", "Final release is pending utility trench reinstatement.")
            };

            _context.ProjectPackages.AddRange(packages);
            var packageByCode = packages.ToDictionary(item => item.Code!, StringComparer.OrdinalIgnoreCase);

            ProjectBoqItem MakeBoq(string packageCode, string lineNumber, string itemCode, string description, decimal quantity, string uom, decimal? unitRate, decimal budget, decimal? committed, decimal? actual, decimal? forecast)
                => new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectPackageId = packageByCode[packageCode].Id,
                    LineNumber = lineNumber,
                    ItemCode = itemCode,
                    ItemType = ProjectBoqItemTypes.Item,
                    Description = description,
                    Quantity = quantity,
                    UnitOfMeasure = uom,
                    UnitRate = unitRate,
                    BudgetAmount = budget,
                    CommittedAmount = committed,
                    ActualAmount = actual,
                    ForecastAmount = forecast,
                    Currency = baseCurrencyCode,
                    SortOrder = int.Parse(lineNumber) - 1,
                    CreatedAt = now.AddMonths(-4),
                    CreatedBy = "System"
                };

            _context.ProjectBoqItems.AddRange(
                MakeBoq("PKG-SUB", "1", "SUB-FOUND", "Reinforced concrete foundations and ground beams", 1m, "LS", 810500m, 820000m, 820000m, 810500m, 810500m),
                MakeBoq("PKG-SUP", "1", "SUP-FRAME", "RC frame, slabs, and blockwork shell", 1m, "LS", 1600000m, 1650000m, 1625000m, 1510000m, 1668000m),
                MakeBoq("PKG-ELEC", "1", "ELEC-COMMON", "Common-area distribution boards, lighting, and ELV rough-in", 1m, "LS", 540000m, 540000m, 470000m, 281000m, 552000m),
                MakeBoq("PKG-PLUMB", "1", "PLUMB-RISERS", "Water risers, sanitary stacks, and pump-room fit-out", 1m, "LS", 425000m, 425000m, 362000m, 238500m, 432000m),
                MakeBoq("PKG-FIN", "1", "FIN-APT", "Apartment finishes, joinery, tiling, and painting", 24m, "UNIT", 37916.67m, 910000m, 708000m, 468500m, 936000m),
                MakeBoq("PKG-EXT", "1", "EXT-LAND", "External paving, drainage tie-ins, and landscaping", 1m, "LS", 265000m, 265000m, null, null, 278000m));

            ProjectApprovalRegisterItem MakeApproval(string phaseCode, string type, string title, string authority, string status, string? reference, DateTime? submitted, DateTime? targetDecision, DateTime? approved, string notes)
                => new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectPhaseId = phaseByCode[phaseCode].Id,
                    ApprovalType = type,
                    Title = title,
                    AuthorityName = authority,
                    ReferenceNumber = reference,
                    Status = status,
                    IsRequired = true,
                    SubmittedDate = submitted,
                    TargetDecisionDate = targetDecision,
                    ApprovedDate = approved,
                    Notes = notes,
                    CreatedAt = now.AddMonths(-6),
                    CreatedBy = "System"
                };

            _context.ProjectApprovalRegisterItems.AddRange(
                MakeApproval("APPROVALS", ProjectApprovalRegisterTypes.PlanningPermission, "Planning permission for Block A", "Accra Metropolitan Assembly", ProjectApprovalRegisterStatuses.Approved, "AMA/PLAN/BLOCKA/26/014", now.Date.AddMonths(-7).AddDays(10), now.Date.AddMonths(-6).AddDays(18), now.Date.AddMonths(-6).AddDays(15), "Approved with standard drainage and parking conditions."),
                MakeApproval("APPROVALS", ProjectApprovalRegisterTypes.BuildingPermit, "Building permit for residential block", "Accra Metropolitan Assembly", ProjectApprovalRegisterStatuses.Approved, "AMA/BLD/BLOCKA/26/052", now.Date.AddMonths(-6).AddDays(3), now.Date.AddMonths(-5).AddDays(5), now.Date.AddMonths(-5).AddDays(2), "Permit covers six floors plus rooftop plant room."),
                MakeApproval("APPROVALS", ProjectApprovalRegisterTypes.FireClearance, "Fire service installation clearance", "Ghana National Fire Service", ProjectApprovalRegisterStatuses.Submitted, "GNFS/BLOCKA/26/033", now.Date.AddDays(-18), now.Date.AddDays(9), null, "Awaiting final witness test of alarm and hydrant systems."),
                MakeApproval("APPROVALS", ProjectApprovalRegisterTypes.UtilityClearance, "Utility energisation clearance", "ECG / Ghana Water", ProjectApprovalRegisterStatuses.Approved, "UTIL/BLOCKA/26/017", now.Date.AddMonths(-1), now.Date.AddDays(-10), now.Date.AddDays(-8), "Permanent services are live for common areas and test apartments."),
                MakeApproval("HANDOVER", ProjectApprovalRegisterTypes.OccupancyCertificate, "Occupancy certificate for phased unit handover", "Accra Metropolitan Assembly", ProjectApprovalRegisterStatuses.InPreparation, "AMA/OCC/BLOCKA/26/PH1", null, now.Date.AddMonths(1), null, "Batch 1 submission will cover the first handed-over units and shared services."));

            var units = new List<ProjectUnit>();
            for (var floor = 1; floor <= 6; floor++)
            {
                for (var position = 1; position <= 4; position++)
                {
                    var index = ((floor - 1) * 4) + (position - 1);
                    var code = $"A{floor}{position:00}";
                    var areaSquareMeters = position is 2 or 3 ? 124m : 96m;
                    var valuationRate = 15500m + (floor * 175m) + (position is 2 or 3 ? 350m : 0m);
                    var status = index switch
                    {
                        < 2 => ProjectUnitStatuses.HandedOver,
                        < 8 => ProjectUnitStatuses.Sold,
                        < 14 => ProjectUnitStatuses.Reserved,
                        < 22 => ProjectUnitStatuses.Available,
                        _ => ProjectUnitStatuses.Planned
                    };

                    var customerId = status switch
                    {
                        ProjectUnitStatuses.HandedOver or ProjectUnitStatuses.Sold or ProjectUnitStatuses.Reserved
                            => index % 2 == 0 ? apartmentBuyerOne.Id : apartmentBuyerTwo.Id,
                        _ => (Guid?)null
                    };

                    var released = status != ProjectUnitStatuses.Planned;
                    units.Add(new ProjectUnit
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ProjectId = projectId,
                        CustomerBusinessPartnerId = customerId,
                        IsReleasedForMarket = released,
                        ReleasedAt = released ? now.Date.AddDays(-45 + index) : null,
                        ReleasedById = released ? projectManager.Id : null,
                        Code = code,
                        Name = $"Apartment {code}",
                        UnitType = ProjectUnitTypes.Apartment,
                        Status = status,
                        BlockName = "Block A",
                        FloorLabel = $"Floor {floor}",
                        AreaSquareMeters = areaSquareMeters,
                        ValuationRate = valuationRate,
                        BasePrice = Math.Round(areaSquareMeters * valuationRate, 2, MidpointRounding.AwayFromZero),
                        Currency = baseCurrencyCode,
                        HandoverDate = status == ProjectUnitStatuses.HandedOver ? now.Date.AddDays(-(12 - index)) : null,
                        SortOrder = index,
                        Notes = status switch
                        {
                            ProjectUnitStatuses.HandedOver => "Buyer handover complete; unit has entered early defects monitoring.",
                            ProjectUnitStatuses.Sold => "Sold unit awaiting final finishes or commissioning closeout before handover.",
                            ProjectUnitStatuses.Reserved => "Reserved for buyer pending full sales completion and finish confirmation.",
                            ProjectUnitStatuses.Available => "Released to market for active sales and leasing conversations.",
                            _ => "Held back from release until top-floor finishes are ready."
                        },
                        CreatedAt = now.AddMonths(-2).AddDays(index),
                        CreatedBy = "System"
                    });
                }
            }

            _context.ProjectUnits.AddRange(units);
            var unitByCode = units.ToDictionary(item => item.Code!, StringComparer.OrdinalIgnoreCase);

            _context.ProjectCustomerVariations.AddRange(
                new ProjectCustomerVariation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectUnitId = unitByCode["A203"].Id,
                    CustomerBusinessPartnerId = apartmentBuyerOne.Id,
                    Title = "Upgrade kitchen finishes and extend breakfast counter",
                    Description = "Buyer requested upgraded quartz worktops, revised splashback selection, and a longer breakfast counter in Apartment A203.",
                    VariationType = "BuyerFinishUpgrade",
                    Timing = ProjectCustomerVariationTimings.PreHandover,
                    Status = ProjectCustomerVariationStatuses.Approved,
                    RequestDate = now.Date.AddDays(-16),
                    TargetCompletionDate = now.Date.AddDays(12),
                    EstimatedAmount = 42000m,
                    QuotedAmount = 45500m,
                    ApprovedAmount = 45500m,
                    Currency = baseCurrencyCode,
                    RequiresScheduleAdjustment = true,
                    ScheduleImpactDays = 5,
                    Notes = "Approved after commercial review; finish package sequence has been adjusted.",
                    CreatedAt = now.Date.AddDays(-16),
                    CreatedBy = "System"
                },
                new ProjectCustomerVariation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectUnitId = unitByCode["A101"].Id,
                    CustomerBusinessPartnerId = apartmentBuyerTwo.Id,
                    Title = "Post-handover wardrobe and utility-cabinet modification",
                    Description = "Buyer requested an additional utility cabinet and modified wardrobe shelving after taking possession of Apartment A101.",
                    VariationType = "PostHandoverAlteration",
                    Timing = ProjectCustomerVariationTimings.PostHandover,
                    Status = ProjectCustomerVariationStatuses.Billed,
                    RequestDate = now.Date.AddDays(-9),
                    TargetCompletionDate = now.Date.AddDays(-2),
                    CompletedDate = now.Date.AddDays(-3),
                    EstimatedAmount = 16500m,
                    QuotedAmount = 18500m,
                    ApprovedAmount = 18500m,
                    BilledAmount = 18500m,
                    Currency = baseCurrencyCode,
                    Notes = "Delivered as a billable post-handover alteration and invoiced to the buyer.",
                    CreatedAt = now.Date.AddDays(-9),
                    CreatedBy = "System"
                });

            _context.ProjectCommissioningItems.AddRange(
                new ProjectCommissioningItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    Title = "Fire alarm, smoke detection, and emergency lighting tests",
                    SystemArea = "Life Safety",
                    Status = ProjectCommissioningItemStatuses.Completed,
                    RequiresRegulatoryInspection = true,
                    PlannedDate = now.Date.AddDays(-12),
                    CompletedDate = now.Date.AddDays(-8),
                    CertificateReference = "COMM-LS-001",
                    ResponsibleParty = "Prime MEP Consult",
                    SortOrder = 0,
                    Notes = "Witness test complete for the first occupancy batch.",
                    CreatedAt = now.Date.AddDays(-12),
                    CreatedBy = "System"
                },
                new ProjectCommissioningItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    Title = "Passenger lift load test and certification",
                    SystemArea = "Vertical Transportation",
                    Status = ProjectCommissioningItemStatuses.ReadyForInspection,
                    RequiresRegulatoryInspection = true,
                    PlannedDate = now.Date.AddDays(6),
                    ResponsibleParty = "Adom Construction Ltd",
                    SortOrder = 1,
                    Notes = "Awaiting final inspector slot confirmation.",
                    CreatedAt = now.Date.AddDays(-5),
                    CreatedBy = "System"
                },
                new ProjectCommissioningItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectUnitId = unitByCode["A101"].Id,
                    Title = "Final electrical and plumbing validation for Apartment A101",
                    SystemArea = "Unit Services",
                    Status = ProjectCommissioningItemStatuses.Completed,
                    PlannedDate = now.Date.AddDays(-18),
                    CompletedDate = now.Date.AddDays(-13),
                    CertificateReference = "COMM-A101-006",
                    ResponsibleParty = "Prime MEP Consult",
                    SortOrder = 2,
                    Notes = "Released for phased buyer handover.",
                    CreatedAt = now.Date.AddDays(-18),
                    CreatedBy = "System"
                });

            _context.ProjectHandoverItems.AddRange(
                new ProjectHandoverItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    HandoverType = ProjectHandoverItemTypes.PracticalCompletion,
                    Title = "Practical completion for Batch 1 apartments",
                    Status = ProjectHandoverItemStatuses.InPreparation,
                    ResponsibleParty = "Project Manager",
                    ReferenceNumber = "PC-B1-2026-01",
                    TargetDate = now.Date.AddDays(18),
                    SortOrder = 0,
                    Notes = "Batch 1 includes the first four apartments and related common services.",
                    CreatedAt = now.Date.AddDays(-6),
                    CreatedBy = "System"
                },
                new ProjectHandoverItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    HandoverType = ProjectHandoverItemTypes.AsBuiltDrawing,
                    Title = "As-built drawings and O&M pack for Block A",
                    Status = ProjectHandoverItemStatuses.Ready,
                    ResponsibleParty = "Prime MEP Consult",
                    ReferenceNumber = "AB-BLOCKA-01",
                    TargetDate = now.Date.AddDays(7),
                    SortOrder = 1,
                    Notes = "Ready for sponsor review before occupancy submission.",
                    CreatedAt = now.Date.AddDays(-8),
                    CreatedBy = "System"
                },
                new ProjectHandoverItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectUnitId = unitByCode["A101"].Id,
                    HandoverType = ProjectHandoverItemTypes.KeyHandover,
                    Title = "Key handover for Apartment A101",
                    Status = ProjectHandoverItemStatuses.Completed,
                    ResponsibleParty = "Sales & Handover Desk",
                    ReferenceNumber = "KEY-A101",
                    TargetDate = now.Date.AddDays(-14),
                    CompletedDate = now.Date.AddDays(-12),
                    SortOrder = 2,
                    Notes = "Buyer took possession after snag clearance and services demonstration.",
                    CreatedAt = now.Date.AddDays(-14),
                    CreatedBy = "System"
                },
                new ProjectHandoverItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectUnitId = unitByCode["A102"].Id,
                    HandoverType = ProjectHandoverItemTypes.KeyHandover,
                    Title = "Key handover for Apartment A102",
                    Status = ProjectHandoverItemStatuses.Completed,
                    ResponsibleParty = "Sales & Handover Desk",
                    ReferenceNumber = "KEY-A102",
                    TargetDate = now.Date.AddDays(-10),
                    CompletedDate = now.Date.AddDays(-8),
                    SortOrder = 3,
                    Notes = "Second early unit handover completed with signed acceptance pack.",
                    CreatedAt = now.Date.AddDays(-10),
                    CreatedBy = "System"
                });

            _context.ProjectSnagItems.AddRange(
                new ProjectSnagItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectUnitId = unitByCode["A204"].Id,
                    Title = "Balcony door alignment adjustment",
                    Description = "Door leaf on the living-room balcony opening needs alignment before buyer demonstration.",
                    Severity = ProjectSnagSeverities.Medium,
                    Status = ProjectSnagStatuses.InProgress,
                    ReportedDate = now.Date.AddDays(-6),
                    TargetClosureDate = now.Date.AddDays(3),
                    RaisedByName = "Site QA Team",
                    ResponsibleParty = "Finishes Subcontractor",
                    Notes = "Included in the current snag-closing round.",
                    CreatedAt = now.Date.AddDays(-6),
                    CreatedBy = "System"
                },
                new ProjectSnagItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ProjectUnitId = unitByCode["A503"].Id,
                    Title = "Kitchen backsplash tile replacement",
                    Description = "Two backsplash tiles cracked during appliance installation and require replacement.",
                    Severity = ProjectSnagSeverities.Low,
                    Status = ProjectSnagStatuses.Open,
                    ReportedDate = now.Date.AddDays(-2),
                    TargetClosureDate = now.Date.AddDays(5),
                    RaisedByName = "Clerk of Works",
                    ResponsibleParty = "Finishes Subcontractor",
                    Notes = "Hold release of unit until replacement tiles are fitted and checked.",
                    CreatedAt = now.Date.AddDays(-2),
                    CreatedBy = "System"
                });

            _context.ProjectDefectLiabilityCases.Add(new ProjectDefectLiabilityCase
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                ProjectUnitId = unitByCode["A101"].Id,
                CustomerBusinessPartnerId = apartmentBuyerTwo.Id,
                Title = "Water heater pressure fluctuation after occupancy",
                Description = "Buyer reported intermittent water-heater pressure drop during the first week of occupancy.",
                Status = ProjectDefectLiabilityStatuses.UnderReview,
                ReportedDate = now.Date.AddDays(-4),
                TargetResolutionDate = now.Date.AddDays(2),
                IsWarrantyRelated = true,
                WarrantyExpiryDate = now.Date.AddMonths(12),
                RectificationCost = 1200m,
                ChargeableAmount = 0m,
                Currency = baseCurrencyCode,
                Notes = "Treated as warranty rectification under the defects liability process.",
                CreatedAt = now.Date.AddDays(-4),
                CreatedBy = "System"
            });
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
            string baseCurrencyCode,
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
                Currency = baseCurrencyCode,
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
                Currency = baseCurrencyCode,
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
            string baseCurrencyCode,
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
            string baseCurrencyCode,
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
                Currency = baseCurrencyCode,
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
                Currency = baseCurrencyCode,
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

        private async Task<string> ResolveBaseCurrencyCodeAsync(Guid tenantId)
        {
            var currency = await _context.Currencies
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.IsBaseCurrency && !item.IsDeleted)
                .OrderByDescending(item => item.IsActive)
                .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .Select(item => item.CurrencyCode)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(currency))
            {
                return currency.Trim().ToUpperInvariant();
            }

            var tenantCurrency = await _context.Tenants
                .AsNoTracking()
                .Where(item => item.Id == tenantId)
                .Select(item => item.BaseCurrency)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(tenantCurrency) ? "GHS" : tenantCurrency.Trim().ToUpperInvariant();
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
            var entityTypeCandidates = await _context.WorkflowEntityTypes
                .Where(et => !et.IsDeleted && et.TenantId == tenantId)
                .ToListAsync();

            var entityType = entityTypeCandidates
                .Where(et =>
                    WorkflowEntityTypeKeyMatches(et.Code, entityCode) ||
                    WorkflowEntityTypeKeyMatches(et.Name, entityCode) ||
                    WorkflowEntityTypeKeyMatches(et.Code, entityName) ||
                    WorkflowEntityTypeKeyMatches(et.Name, entityName))
                .OrderByDescending(et => et.IsActive)
                .ThenBy(et => WorkflowEntityTypeKeyMatches(et.Name, entityCode) ? 0 : 1)
                .ThenBy(et => WorkflowEntityTypeKeyMatches(et.Code, entityCode) ? 0 : 1)
                .FirstOrDefault();

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
            else
            {
                var changed = false;

                if (!entityType.IsActive)
                {
                    entityType.IsActive = true;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(entityType.EntityClassName) && !string.IsNullOrWhiteSpace(entityClassName))
                {
                    entityType.EntityClassName = entityClassName;
                    changed = true;
                }

                if (changed)
                {
                    entityType.UpdatedAt = DateTime.UtcNow;
                    entityType.UpdatedBy = "System";
                    await _context.SaveChangesAsync();
                }
            }

            var existingDefinition = await _context.WorkflowDefinitions
                .Include(d => d.Steps)
                .FirstOrDefaultAsync(d =>
                    !d.IsDeleted
                    && d.TenantId == tenantId
                    && (d.EntityTypeId == entityType.Id || d.Name == definitionName));

            if (existingDefinition != null)
            {
                var changed = false;

                if (!existingDefinition.IsActive)
                {
                    existingDefinition.IsActive = true;
                    changed = true;
                }

                if (existingDefinition.EntityTypeId != entityType.Id && existingDefinition.Name == definitionName)
                {
                    existingDefinition.EntityTypeId = entityType.Id;
                    changed = true;
                }

                if (EnsureApprovalStepConfigurations(existingDefinition.Steps, approvalRoleNames))
                {
                    changed = true;
                }

                if (changed)
                {
                    existingDefinition.UpdatedAt = DateTime.UtcNow;
                    existingDefinition.UpdatedBy = "System";
                    await _context.SaveChangesAsync();
                }

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
                DefinitionKey = definitionId,
                TenantId = tenantId,
                Name = definitionName,
                Description = description,
                EntityTypeId = entityType.Id,
                Version = 1,
                IsActive = true,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                PublishedAt = now,
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

        private async Task EnsureSequentialWorkflowDefinitionSeededAsync(
            Guid tenantId,
            string entityCode,
            string entityName,
            string? entityClassName,
            string definitionName,
            string description,
            IReadOnlyList<WorkflowApprovalStageSeed> approvalStages)
        {
            var entityType = await EnsureWorkflowEntityTypeAsync(
                tenantId,
                entityCode,
                entityName,
                entityClassName,
                description);

            var definitions = await _context.WorkflowDefinitions
                .Include(d => d.Steps)
                .Where(d =>
                    !d.IsDeleted
                    && d.TenantId == tenantId
                    && (d.EntityTypeId == entityType.Id
                        || d.Name == definitionName
                        || d.Name.StartsWith($"{definitionName} ")))
                .ToListAsync();

            var sequentialDefinition = definitions
                .Where(d => HasExpectedApprovalStages(d, approvalStages))
                .OrderByDescending(d => d.Version)
                .ThenByDescending(d => d.UpdatedAt ?? d.CreatedAt)
                .FirstOrDefault();

            if (sequentialDefinition != null)
            {
                var changed = false;
                var repairNow = DateTime.UtcNow;
                if (!sequentialDefinition.IsActive)
                {
                    sequentialDefinition.IsActive = true;
                    changed = true;
                }

                // Baseline definitions are runtime controls, not editable drafts. Repair older
                // seed rows that pre-date workflow lifecycle governance so startup does not leave
                // Finance submission paths pointing at an "active" definition the repository
                // correctly excludes from runtime selection.
                if (sequentialDefinition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
                {
                    sequentialDefinition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
                    sequentialDefinition.PublishedAt ??= repairNow;
                    sequentialDefinition.RetiredAt = null;
                    sequentialDefinition.RetiredById = null;
                    changed = true;
                }

                if (sequentialDefinition.Description != description)
                {
                    sequentialDefinition.Description = description;
                    changed = true;
                }

                if (EnsureSequentialApprovalStepConfigurations(sequentialDefinition, approvalStages))
                {
                    changed = true;
                }

                if (RepairLegacyApprovalDefinitions(definitions, definitionName, approvalStages, repairNow))
                {
                    changed = true;
                }

                if (DeactivateLegacyWorkflowDefinitions(
                        definitions,
                        sequentialDefinition.Id,
                        definitionName,
                        approvalStages,
                        repairNow))
                {
                    changed = true;
                }

                if (changed)
                {
                    sequentialDefinition.UpdatedAt = repairNow;
                    sequentialDefinition.UpdatedBy = "System";
                    await _context.SaveChangesAsync();
                }

                return;
            }

            var now = DateTime.UtcNow;
            var version = definitions.Count == 0 ? 1 : definitions.Max(d => d.Version) + 1;
            var safeDefinitionName = definitions.Any(d => string.Equals(d.Name, definitionName, StringComparison.OrdinalIgnoreCase))
                ? $"{definitionName} Sequential"
                : definitionName;

            if (definitions.Any(d => string.Equals(d.Name, safeDefinitionName, StringComparison.OrdinalIgnoreCase)))
            {
                safeDefinitionName = $"{definitionName} Sequential v{version}";
            }

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

            var approvalSteps = approvalStages
                .Select((stage, index) => new WorkflowStep
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowDefinitionId = definitionId,
                    Name = stage.StepName,
                    Description = stage.Description,
                    StepType = WorkflowStepType.Approval,
                    Order = index + 2,
                    IsRequired = true,
                    Configuration = BuildApprovalConfigurationJson(stage.RoleNames),
                    CreatedAt = now,
                    CreatedBy = "System"
                })
                .ToList();

            var approvedStep = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = definitionId,
                Name = "Approved",
                StepType = WorkflowStepType.Manual,
                Order = approvalStages.Count + 2,
                IsEndStep = true,
                IsRequired = true,
                CreatedAt = now,
                CreatedBy = "System"
            };

            _context.WorkflowDefinitions.Add(new WorkflowDefinition
            {
                Id = definitionId,
                DefinitionKey = definitionId,
                TenantId = tenantId,
                Name = safeDefinitionName,
                Description = description,
                EntityTypeId = entityType.Id,
                Version = version,
                IsActive = true,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                PublishedAt = now,
                CreatedAt = now,
                CreatedBy = "System"
            });

            var steps = new List<WorkflowStep> { draftStep };
            steps.AddRange(approvalSteps);
            steps.Add(approvedStep);
            _context.WorkflowSteps.AddRange(steps);

            var transitions = new List<WorkflowTransition>();
            for (var index = 0; index < steps.Count - 1; index++)
            {
                transitions.Add(new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowDefinitionId = definitionId,
                    FromStepId = steps[index].Id,
                    ToStepId = steps[index + 1].Id,
                    Name = index == 0 ? "Submit" : index == steps.Count - 2 ? "Final Approve" : "Approve",
                    IsDefault = true,
                    Priority = 0,
                    CreatedAt = now,
                    CreatedBy = "System"
                });
            }

            _context.WorkflowTransitions.AddRange(transitions);

            RepairLegacyApprovalDefinitions(definitions, definitionName, approvalStages, now);
            DeactivateLegacyWorkflowDefinitions(definitions, definitionId, definitionName, approvalStages, now);

            await _context.SaveChangesAsync();
        }

        private async Task<WorkflowEntityType> EnsureWorkflowEntityTypeAsync(
            Guid tenantId,
            string entityCode,
            string entityName,
            string? entityClassName,
            string description)
        {
            var entityTypeCandidates = await _context.WorkflowEntityTypes
                .Where(et => !et.IsDeleted && et.TenantId == tenantId)
                .ToListAsync();

            var entityType = entityTypeCandidates
                .Where(et =>
                    WorkflowEntityTypeKeyMatches(et.Code, entityCode) ||
                    WorkflowEntityTypeKeyMatches(et.Name, entityCode) ||
                    WorkflowEntityTypeKeyMatches(et.Code, entityName) ||
                    WorkflowEntityTypeKeyMatches(et.Name, entityName))
                .OrderByDescending(et => et.IsActive)
                .ThenBy(et => WorkflowEntityTypeKeyMatches(et.Name, entityCode) ? 0 : 1)
                .ThenBy(et => WorkflowEntityTypeKeyMatches(et.Code, entityCode) ? 0 : 1)
                .FirstOrDefault();

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
                return entityType;
            }

            var changed = false;
            if (!entityType.IsActive)
            {
                entityType.IsActive = true;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(entityType.EntityClassName) && !string.IsNullOrWhiteSpace(entityClassName))
            {
                entityType.EntityClassName = entityClassName;
                changed = true;
            }

            if (changed)
            {
                entityType.UpdatedAt = DateTime.UtcNow;
                entityType.UpdatedBy = "System";
                await _context.SaveChangesAsync();
            }

            return entityType;
        }

        private static bool RepairLegacyApprovalDefinitions(
            IEnumerable<WorkflowDefinition> definitions,
            string definitionName,
            IReadOnlyList<WorkflowApprovalStageSeed> approvalStages,
            DateTime now)
        {
            var changed = false;
            var baselineRoleNames = approvalStages
                .SelectMany(stage => stage.RoleNames)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var definition in definitions.Where(d => IsLegacySeededWorkflowDefinition(d, definitionName, approvalStages)))
            {
                if (EnsureApprovalStepConfigurations(definition.Steps, baselineRoleNames, now))
                {
                    definition.UpdatedAt = now;
                    definition.UpdatedBy = "System";
                    changed = true;
                }
            }

            return changed;
        }

        private static bool DeactivateLegacyWorkflowDefinitions(
            IEnumerable<WorkflowDefinition> definitions,
            Guid activeDefinitionId,
            string definitionName,
            IReadOnlyList<WorkflowApprovalStageSeed> approvalStages,
            DateTime now)
        {
            var changed = false;

            foreach (var definition in definitions.Where(d =>
                         d.Id != activeDefinitionId
                         && d.IsActive
                         && IsReplaceableSeededWorkflowDefinition(d, definitionName, approvalStages)))
            {
                definition.IsActive = false;
                if (definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
                {
                    definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
                    definition.RetiredAt ??= now;
                }
                definition.UpdatedAt = now;
                definition.UpdatedBy = "System";
                changed = true;
            }

            return changed;
        }

        private static bool IsLegacySeededWorkflowDefinition(
            WorkflowDefinition definition,
            string definitionName,
            IReadOnlyList<WorkflowApprovalStageSeed> approvalStages)
        {
            if (!string.Equals(definition.Name, definitionName, StringComparison.OrdinalIgnoreCase)
                && !definition.Name.StartsWith($"{definitionName} ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (HasExpectedApprovalStages(definition, approvalStages))
            {
                return false;
            }

            return definition.Steps.Count(s => s.StepType == WorkflowStepType.Approval && !s.IsDeleted) <= 1;
        }

        private static bool IsReplaceableSeededWorkflowDefinition(
            WorkflowDefinition definition,
            string definitionName,
            IReadOnlyList<WorkflowApprovalStageSeed> approvalStages)
        {
            if (!string.Equals(definition.Name, definitionName, StringComparison.OrdinalIgnoreCase)
                && !definition.Name.StartsWith($"{definitionName} ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (HasExpectedApprovalStages(definition, approvalStages))
            {
                return false;
            }

            // Definitions created by this baseline seeder may have multiple stages from an
            // older release. They remain replaceable; tenant-authored definitions do not.
            return string.Equals(definition.CreatedBy, "System", StringComparison.OrdinalIgnoreCase)
                || IsLegacySeededWorkflowDefinition(definition, definitionName, approvalStages);
        }

        private static bool EnsureSequentialApprovalStepConfigurations(
            WorkflowDefinition definition,
            IReadOnlyList<WorkflowApprovalStageSeed> approvalStages)
        {
            var changed = false;
            var now = DateTime.UtcNow;

            foreach (var stage in approvalStages)
            {
                var step = definition.Steps.FirstOrDefault(s =>
                    s.StepType == WorkflowStepType.Approval
                    && !s.IsDeleted
                    && WorkflowEntityTypeKeyMatches(s.Name, stage.StepName));

                if (step != null && EnsureApprovalStepConfiguration(step, stage.RoleNames, now))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsureApprovalStepConfigurations(
            IEnumerable<WorkflowStep> steps,
            IReadOnlyCollection<string> approvalRoleNames)
        {
            return EnsureApprovalStepConfigurations(steps, approvalRoleNames, DateTime.UtcNow);
        }

        private static bool EnsureApprovalStepConfigurations(
            IEnumerable<WorkflowStep> steps,
            IReadOnlyCollection<string> approvalRoleNames,
            DateTime now)
        {
            var approvalSteps = steps
                .Where(s => s.StepType == WorkflowStepType.Approval && !s.IsDeleted)
                .OrderBy(s => s.Order)
                .ToList();

            if (approvalSteps.Count == 0)
            {
                return false;
            }

            var targetSteps = approvalSteps.Count == 1
                ? approvalSteps
                : approvalSteps.Where(s => WorkflowEntityTypeKeyMatches(s.Name, "PendingApproval")).ToList();

            var changed = false;
            foreach (var step in targetSteps)
            {
                if (EnsureApprovalStepConfiguration(step, approvalRoleNames, now))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsureApprovalStepConfiguration(
            WorkflowStep step,
            IReadOnlyCollection<string> approvalRoleNames,
            DateTime now)
        {
            if (ApprovalRolesMatch(step.Configuration, approvalRoleNames))
            {
                return false;
            }

            var configuration = DeserializeWorkflowStepConfiguration(step.Configuration) ?? new WorkflowStepConfigurationDto();
            configuration.ApprovalConfig = BuildApprovalConfig(approvalRoleNames);

            step.Configuration = JsonSerializer.Serialize(configuration, WorkflowSeedJsonOptions);
            step.UpdatedAt = now;
            step.UpdatedBy = "System";
            return true;
        }

        private static bool HasExpectedApprovalStages(WorkflowDefinition definition, IReadOnlyList<WorkflowApprovalStageSeed> approvalStages)
        {
            var approvalStepNames = definition.Steps
                .Where(s => s.StepType == WorkflowStepType.Approval && !s.IsDeleted)
                .OrderBy(s => s.Order)
                .Select(s => NormalizeWorkflowEntityTypeKey(s.Name))
                .ToList();

            if (approvalStepNames.Count != approvalStages.Count)
            {
                return false;
            }

            for (var index = 0; index < approvalStages.Count; index++)
            {
                if (approvalStepNames[index] != NormalizeWorkflowEntityTypeKey(approvalStages[index].StepName))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ApprovalRolesMatch(string? configurationJson, IReadOnlyCollection<string> approvalRoleNames)
        {
            var expectedRoles = approvalRoleNames
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var actualRoles = GetApprovalRolesFromConfiguration(configurationJson)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return expectedRoles.SetEquals(actualRoles);
        }

        private static IReadOnlyList<string> GetApprovalRolesFromConfiguration(string? configurationJson)
        {
            var approvalConfig = DeserializeWorkflowStepConfiguration(configurationJson)?.ApprovalConfig;
            if (approvalConfig == null)
            {
                return Array.Empty<string>();
            }

            return approvalConfig.ApproverRules
                .Where(rule => rule.AssignmentType == WorkflowAssignmentType.Role && !string.IsNullOrWhiteSpace(rule.Role))
                .Select(rule => rule.Role!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static WorkflowStepConfigurationDto? DeserializeWorkflowStepConfiguration(string? configurationJson)
        {
            if (string.IsNullOrWhiteSpace(configurationJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(configurationJson, WorkflowSeedJsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool WorkflowEntityTypeKeyMatches(string? left, string? right)
            => NormalizeWorkflowEntityTypeKey(left) == NormalizeWorkflowEntityTypeKey(right);

        private static string NormalizeWorkflowEntityTypeKey(string? value)
            => new((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

        private static string BuildApprovalConfigurationJson(IReadOnlyCollection<string> approvalRoleNames)
        {
            var configuration = new WorkflowStepConfigurationDto
            {
                ApprovalConfig = BuildApprovalConfig(approvalRoleNames)
            };

            return JsonSerializer.Serialize(configuration, WorkflowSeedJsonOptions);
        }

        private static WorkflowApprovalConfigDto BuildApprovalConfig(IReadOnlyCollection<string> approvalRoleNames)
        {
            return new WorkflowApprovalConfigDto
            {
                ApprovalType = WorkflowApprovalType.Single,
                MinApprovalsRequired = 1,
                RejectionHandling = WorkflowRejectionHandling.StopWorkflow,
                ApproverRules = approvalRoleNames
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(role => role.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(role => new WorkflowAssignmentRuleDto
                    {
                        AssignmentType = WorkflowAssignmentType.Role,
                        Role = role
                    })
                    .ToList()
            };
        }

        private static JsonSerializerOptions CreateWorkflowSeedJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
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
                        DefinitionKey = definitionId,
                        TenantId = tenant.Id,
                        Name = "EHC Ticket",
                        Description = "Baseline ticket lifecycle: New → Acknowledged → InProgress → Resolved → Closed",
                        EntityTypeId = entityType.Id,
                        Version = 1,
                        IsActive = true,
                        LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                        PublishedAt = DateTime.UtcNow,
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

        private async Task EnsureWorkflowNotificationTopicsSeededAsync()
        {
            var tenants = await _context.Tenants
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active)
                .Select(t => t.Id)
                .ToListAsync();

            var workflowActivities = new[]
            {
                "WorkflowSubmitted",
                "WorkflowStepAssignment",
                "WorkflowApprovalRequest",
                "WorkflowStepEscalated",
                "WorkflowCompleted",
                "WorkflowRejected",
                "WorkflowStepOverdue"
            };

            var requiredActivities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WorkflowStepAssignment",
                "WorkflowApprovalRequest"
            };

            var defaultEmailActivities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WorkflowSubmitted",
                "WorkflowStepAssignment",
                "WorkflowApprovalRequest"
            };

            var createdTopics = 0;
            var updatedTopics = 0;
            var createdRecipients = 0;
            var updatedRecipients = 0;

            foreach (var tenantId in tenants)
            {
                var workflowEntityTypes = await _context.WorkflowEntityTypes
                    .AsNoTracking()
                    .Where(et => et.TenantId == tenantId && !et.IsDeleted && et.IsActive)
                    .Select(et => new { et.Name, et.Code })
                    .ToListAsync();

                var normalizedEntityTypes = workflowEntityTypes
                    .SelectMany(et => new[]
                    {
                        NormalizeNotificationTopicSegment(et.Name),
                        NormalizeNotificationTopicSegment(et.Code)
                    })
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var entityType in normalizedEntityTypes)
                {
                    foreach (var activity in workflowActivities)
                    {
                        var normalizedActivity = NormalizeNotificationTopicSegment(activity);
                        var key = GenerateNotificationTopicKey(entityType, normalizedActivity, "Internal");
                        if (string.IsNullOrWhiteSpace(key)) continue;

                        var isRequired = requiredActivities.Contains(activity);
                        var emailEnabledByDefault = defaultEmailActivities.Contains(activity);
                        var now = DateTime.UtcNow;

                        var topic = await _context.NotificationTopics
                            .Include(t => t.Recipients)
                            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Key == key && !t.IsDeleted);

                        if (topic == null)
                        {
                            topic = new NotificationTopic
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                Key = key,
                                Name = $"{entityType}: {activity}",
                                Description = "System-seeded workflow notification topic.",
                                EntityType = entityType,
                                IsSystem = true,
                                IsRequired = isRequired,
                                IsActive = true,
                                EnableInApp = true,
                                EnableEmail = emailEnabledByDefault,
                                InAppTitleTemplate = "{{Title}}",
                                InAppBodyTemplate = "{{Message}}",
                                ActionUrlTemplate = "{{ActionUrl}}",
                                CreatedAt = now,
                                CreatedBy = "System"
                            };

                            _context.NotificationTopics.Add(topic);
                            createdTopics++;
                        }
                        else
                        {
                            var changed = false;
                            if (!topic.IsSystem) { topic.IsSystem = true; changed = true; }
                            if (isRequired && !topic.IsRequired) { topic.IsRequired = true; changed = true; }
                            if (string.IsNullOrWhiteSpace(topic.EntityType)) { topic.EntityType = entityType; changed = true; }
                            if (!topic.IsActive) { topic.IsActive = true; changed = true; }
                            if (!topic.EnableInApp) { topic.EnableInApp = true; changed = true; }
                            if (emailEnabledByDefault && !topic.EnableEmail) { topic.EnableEmail = true; changed = true; }
                            if (string.IsNullOrWhiteSpace(topic.InAppTitleTemplate)) { topic.InAppTitleTemplate = "{{Title}}"; changed = true; }
                            if (string.IsNullOrWhiteSpace(topic.InAppBodyTemplate)) { topic.InAppBodyTemplate = "{{Message}}"; changed = true; }
                            if (string.IsNullOrWhiteSpace(topic.ActionUrlTemplate)) { topic.ActionUrlTemplate = "{{ActionUrl}}"; changed = true; }

                            if (changed)
                            {
                                topic.UpdatedAt = now;
                                topic.UpdatedBy = "System";
                                updatedTopics++;
                            }
                        }

                        var recipients = (topic.Recipients ?? new List<NotificationTopicRecipient>())
                            .Where(r => !r.IsDeleted)
                            .ToList();

                        foreach (var (kind, value) in GetWorkflowNotificationRecipients(activity))
                        {
                            var recipient = recipients.FirstOrDefault(r =>
                                string.Equals(r.RecipientKind, kind, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(r.RecipientValue, value, StringComparison.OrdinalIgnoreCase));

                            if (recipient == null)
                            {
                                _context.NotificationTopicRecipients.Add(new NotificationTopicRecipient
                                {
                                    Id = Guid.NewGuid(),
                                    TenantId = tenantId,
                                    TopicId = topic.Id,
                                    RecipientKind = kind,
                                    RecipientValue = value,
                                    IsSystem = true,
                                    SendInApp = true,
                                    SendEmail = true,
                                    CreatedAt = now,
                                    CreatedBy = "System"
                                });
                                createdRecipients++;
                                continue;
                            }

                            var recipientChanged = false;
                            if (!recipient.IsSystem) { recipient.IsSystem = true; recipientChanged = true; }
                            if (!recipient.SendInApp) { recipient.SendInApp = true; recipientChanged = true; }
                            if (!recipient.SendEmail) { recipient.SendEmail = true; recipientChanged = true; }

                            if (recipientChanged)
                            {
                                recipient.UpdatedAt = now;
                                recipient.UpdatedBy = "System";
                                updatedRecipients++;
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Workflow notification topic seed completed. Created {CreatedTopics} topics, updated {UpdatedTopics} topics, created {CreatedRecipients} recipient rules, updated {UpdatedRecipients} recipient rules.",
                createdTopics,
                updatedTopics,
                createdRecipients,
                updatedRecipients);
        }

        private static string NormalizeNotificationTopicSegment(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var chars = value.Trim().Where(char.IsLetterOrDigit).ToArray();
            return chars.Length == 0 ? string.Empty : new string(chars);
        }

        private static string GenerateNotificationTopicKey(string entityType, string activity, string audience)
        {
            if (string.IsNullOrWhiteSpace(entityType)) return string.Empty;
            if (string.IsNullOrWhiteSpace(activity)) return string.Empty;
            if (string.IsNullOrWhiteSpace(audience)) return string.Empty;
            return $"{entityType}.{activity}.{audience}";
        }

        private static List<(string Kind, string Value)> GetWorkflowNotificationRecipients(string activity)
        {
            if (string.Equals(activity, "WorkflowApprovalRequest", StringComparison.OrdinalIgnoreCase))
            {
                return new List<(string, string)>
                {
                    ("UserFromData", "TargetUserId"),
                    ("RoleFromData", "TargetRole")
                };
            }

            if (string.Equals(activity, "WorkflowStepEscalated", StringComparison.OrdinalIgnoreCase))
            {
                return new List<(string, string)>
                {
                    ("UsersFromData", "TargetUserIds")
                };
            }

            return new List<(string, string)>
            {
                ("UserFromData", "TargetUserId")
            };
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

            // Ensure seeded accounts exist on every Development seed run.
            // Preserve existing passwords/contact details so local/admin edits are not undone by startup seeding.
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

            await SeedLandAcquisitionTestUsersAsync(defaultTenant);
            await EnsurePropertyManagementTestRoleAssignmentsAsync();

            await CreateTestUserAsync("finance.clerk", "finance.clerk@default.com", "Finance123!",
                "Ama", "Mensah", defaultTenant.Id, "Finance Clerk", AuthenticationProvider.Local);

            await CreateTestUserAsync("accounts.officer", "accounts.officer@default.com", "Finance123!",
                "Kofi", "Boateng", defaultTenant.Id, "Accounts Officer", AuthenticationProvider.Local);

            await CreateTestUserAsync("ap.officer", "ap.officer@default.com", "Finance123!",
                "Akua", "Owusu", defaultTenant.Id, "Accounts Payable Officer", AuthenticationProvider.Local);

            await CreateTestUserAsync("ar.officer", "ar.officer@default.com", "Finance123!",
                "Kwame", "Asante", defaultTenant.Id, "Accounts Receivable Officer", AuthenticationProvider.Local);

            await CreateTestUserAsync("senior.accountant", "senior.accountant@default.com", "Finance123!",
                "Efua", "Addo", defaultTenant.Id, "Senior Accountant", AuthenticationProvider.Local);

            await CreateTestUserAsync("finance.manager", "finance.manager@default.com", "Finance123!",
                "Yaw", "Osei", defaultTenant.Id, "Finance Manager", AuthenticationProvider.Local);

            await CreateTestUserAsync("financial.controller", "financial.controller@default.com", "Finance123!",
                "Abena", "Dapaah", defaultTenant.Id, "Financial Controller", AuthenticationProvider.Local);

            await CreateTestUserAsync("chief.accountant", "chief.accountant@default.com", "Finance123!",
                "Nana", "Adu", defaultTenant.Id, "Chief Accountant", AuthenticationProvider.Local);

            // The development account lets the conditional executive stage be exercised without
            // granting broad tenant-administrator privileges to an approval actor.
            await CreateTestUserAsync("managing.director", "managing.director@default.com", "Finance123!",
                "TDC", "Managing Director", defaultTenant.Id, "Managing Director", AuthenticationProvider.Local);

            await CreateTestUserAsync("budget.officer", "budget.officer@default.com", "Finance123!",
                "Kojo", "Nkrumah", defaultTenant.Id, "Budget Officer", AuthenticationProvider.Local);

            _logger.LogInformation("Test users seeding completed");
        }

        private async Task SeedLandAcquisitionTestUsersAsync(Tenant tenant)
        {
            const string password = "Acquire123!";
            var accounts = new[]
            {
                new { Department = "Estate Management", Code = "LA-EST", Username = "estate.officer1", First = "Ama", Last = "Estate", Role = "Estate Officer", Number = "LA-EST-001" },
                new { Department = "Estate Management", Code = "LA-EST", Username = "estate.officer2", First = "Kojo", Last = "Estate", Role = "Estate Officer", Number = "LA-EST-002" },
                new { Department = "Estate Management", Code = "LA-EST", Username = "estate.manager", First = "Akosua", Last = "Manager", Role = "Estate Manager", Number = "LA-EST-003" },
                new { Department = "Estate Management", Code = "LA-EST", Username = "acquisition.committee", First = "Kwame", Last = "Committee", Role = "Acquisition Committee", Number = "LA-EST-004" },

                new { Department = "Facilities", Code = "FAC", Username = "facilities.officer", First = "Kofi", Last = "Facilities", Role = "Facilities Officer", Number = "FAC-001" },
                new { Department = "Facilities", Code = "FAC", Username = "facilities.supervisor", First = "Abena", Last = "Supervisor", Role = "Facilities Supervisor", Number = "FAC-002" },
                new { Department = "Facilities", Code = "FAC", Username = "facilities.manager", First = "Yaw", Last = "Facilities", Role = "Facilities Manager", Number = "FAC-003" },

                new { Department = "Survey and Demarcation", Code = "LA-SUR", Username = "survey.officer1", First = "Yaw", Last = "Survey", Role = "Survey Officer", Number = "LA-SUR-001" },
                new { Department = "Survey and Demarcation", Code = "LA-SUR", Username = "survey.officer2", First = "Esi", Last = "Survey", Role = "Survey Officer", Number = "LA-SUR-002" },
                new { Department = "Survey and Demarcation", Code = "LA-SUR", Username = "senior.surveyor1", First = "Kofi", Last = "Surveyor", Role = "Senior Surveyor", Number = "LA-SUR-003" },
                new { Department = "Survey and Demarcation", Code = "LA-SUR", Username = "senior.surveyor2", First = "Adwoa", Last = "Surveyor", Role = "Senior Surveyor", Number = "LA-SUR-004" },

                new { Department = "Legal", Code = "LA-LEG", Username = "legal.officer1", First = "Nana", Last = "Legal", Role = "Legal Officer", Number = "LA-LEG-001" },
                new { Department = "Legal", Code = "LA-LEG", Username = "legal.officer2", First = "Abena", Last = "Legal", Role = "Legal Officer", Number = "LA-LEG-002" },
                new { Department = "Legal", Code = "LA-LEG", Username = "legal.manager1", First = "Fiifi", Last = "Legal", Role = "Legal Manager", Number = "LA-LEG-003" },
                new { Department = "Legal", Code = "LA-LEG", Username = "legal.manager2", First = "Mansa", Last = "Legal", Role = "Legal Manager", Number = "LA-LEG-004" },
                new { Department = "Legal", Code = "LA-LEG", Username = "legal.admin", First = "Efua", Last = "Admin", Role = "Legal Admin Assistant", Number = "LA-LEG-005" },
                new { Department = "Legal", Code = "LA-LEG", Username = "legal.head", First = "Yaw", Last = "Head", Role = "Head of Legal", Number = "LA-LEG-006" },

                new { Department = "Finance and Assets", Code = "LA-FIN", Username = "finance.officer", First = "Daniel", Last = "Finance", Role = "Finance Officer", Number = "LA-FIN-001" },
                new { Department = "Finance and Assets", Code = "LA-FIN", Username = "acquisition.finance.manager", First = "Grace", Last = "Finance", Role = "Finance Manager", Number = "LA-FIN-002" },
                new { Department = "Finance and Assets", Code = "LA-FIN", Username = "accounts.payable", First = "Samuel", Last = "Accounts", Role = "Accounts Payable", Number = "LA-FIN-003" },
                new { Department = "Finance and Assets", Code = "LA-FIN", Username = "fixed.asset", First = "Linda", Last = "Assets", Role = "Fixed Asset Officer", Number = "LA-FIN-004" },

                new { Department = "Land Registry and Liaison", Code = "LA-REG", Username = "lands.liaison1", First = "Joseph", Last = "Liaison", Role = "Lands Commission Liaison", Number = "LA-REG-001" },
                new { Department = "Land Registry and Liaison", Code = "LA-REG", Username = "lands.liaison2", First = "Mary", Last = "Liaison", Role = "Lands Commission Liaison", Number = "LA-REG-002" },
                new { Department = "Land Registry and Liaison", Code = "LA-REG", Username = "land.registry1", First = "Peter", Last = "Registry", Role = "Land Registry Officer", Number = "LA-REG-003" },
                new { Department = "Land Registry and Liaison", Code = "LA-REG", Username = "land.registry2", First = "Ruth", Last = "Registry", Role = "Land Registry Officer", Number = "LA-REG-004" },

                new { Department = "Executive Approvals", Code = "LA-EXE", Username = "executive.approver1", First = "Michael", Last = "Executive", Role = "Executive Approver", Number = "LA-EXE-001" },
                new { Department = "Executive Approvals", Code = "LA-EXE", Username = "executive.approver2", First = "Sarah", Last = "Executive", Role = "Executive Approver", Number = "LA-EXE-002" },
                new { Department = "Executive Approvals", Code = "LA-EXE", Username = "executive.approver3", First = "Richard", Last = "Executive", Role = "Executive Approver", Number = "LA-EXE-003" },
                new { Department = "Executive Approvals", Code = "LA-EXE", Username = "executive.approver4", First = "Patricia", Last = "Executive", Role = "Executive Approver", Number = "LA-EXE-004" }
            };

            // [HR-MODULE-PORT] Positions require OrganizationUnitId + OrganizationLevelId (DepartmentId
            // anchoring was removed). Resolve a default org unit/level for the tenant once so the position
            // inserts below satisfy their FKs. Departments are still created — Employee.DepartmentId keeps them.
            var (orgUnitId, orgLevelId) = await ErpSystem.Data.Seeders.SeederOrgDefaults.EnsureDefaultUnitAsync(_context, tenant.Id);

            foreach (var account in accounts)
            {
                var department = await _context.Departments.FirstOrDefaultAsync(item =>
                    item.TenantId == tenant.Id && item.Code == account.Code && !item.IsDeleted);
                if (department == null)
                {
                    department = new Department
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Name = account.Department,
                        Code = account.Code,
                        Description = "Land acquisition workflow test department",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };
                    _context.Departments.Add(department);
                    await _context.SaveChangesAsync();
                }

                var normalizedRoleCode = new string(account.Role
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());
                var positionCode = $"{account.Code}-{normalizedRoleCode[..Math.Min(normalizedRoleCode.Length, 12)]}";
                var position = await _context.EmployeePositions.FirstOrDefaultAsync(item =>
                    item.TenantId == tenant.Id && item.Code == positionCode && !item.IsDeleted);
                if (position == null)
                {
                    position = new EmployeePosition
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        OrganizationUnitId = orgUnitId,
                        OrganizationLevelId = orgLevelId,
                        Title = account.Role,
                        Code = positionCode,
                        Description = "Land acquisition workflow test position",
                        ExpectedHeadcount = 4,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };
                    _context.EmployeePositions.Add(position);
                    await _context.SaveChangesAsync();
                }

                var email = $"{account.Username}@acquisition.test";
                var employee = await _context.Employees.FirstOrDefaultAsync(item =>
                    item.TenantId == tenant.Id && item.EmployeeNumber == account.Number && !item.IsDeleted);
                if (employee == null)
                {
                    employee = new Employee
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        EmployeeNumber = account.Number,
                        FirstName = account.First,
                        LastName = account.Last,
                        EmailAddress = email,
                        DepartmentId = department.Id,
                        PositionId = position.Id,
                        DateEmployed = DateOnly.FromDateTime(DateTime.UtcNow),
                        StaffStatus = StaffStatus.Active,
                        IsFullTime = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };
                    _context.Employees.Add(employee);
                    await _context.SaveChangesAsync();
                }

                await CreateTestUserAsync(
                    account.Username,
                    email,
                    password,
                    account.First,
                    account.Last,
                    tenant.Id,
                    account.Role,
                    AuthenticationProvider.Local);

                var user = await _userManager.FindByNameAsync(account.Username);
                if (user != null && user.EmployeeId != employee.Id)
                {
                    user.EmployeeId = employee.Id;
                    user.UpdatedAt = DateTime.UtcNow;
                    user.UpdatedBy = "System";
                    await _userManager.UpdateAsync(user);
                }
            }
        }

        private async Task EnsurePropertyManagementTestRoleAssignmentsAsync()
        {
            var assignments = new[]
            {
                new { Username = "estate.officer1", Role = PropertyManagementRoles.Officer },
                new { Username = "estate.officer2", Role = PropertyManagementRoles.Supervisor },
                new { Username = "estate.manager", Role = PropertyManagementRoles.Manager }
            };

            foreach (var assignment in assignments)
            {
                var user = await _userManager.FindByNameAsync(assignment.Username);
                if (user == null || await _userManager.IsInRoleAsync(user, assignment.Role))
                {
                    continue;
                }

                var result = await _userManager.AddToRoleAsync(user, assignment.Role);
                if (!result.Succeeded)
                {
                    _logger.LogError(
                        "Failed to assign Property Management role {Role} to {Username}: {Errors}",
                        assignment.Role,
                        assignment.Username,
                        string.Join(", ", result.Errors.Select(error => error.Description)));
                }
            }
        }

        private async Task EnsureEstateSopExampleCasesSeededAsync()
        {
            var tenant = await _context.Tenants.FirstOrDefaultAsync(item => item.Code == "DEFAULT" && !item.IsDeleted)
                ?? await _context.Tenants.FirstOrDefaultAsync(item => item.Status == TenantStatus.Active && !item.IsDeleted);
            if (tenant is null)
            {
                return;
            }

            var owner = await _userManager.FindByNameAsync("estate.manager")
                ?? await _userManager.FindByNameAsync("admin");
            var ownerId = owner?.Id ?? Guid.Empty;
            var now = DateTime.UtcNow;

            foreach (var seed in GetEstateSopExampleCaseSeeds())
            {
                var exists = await _context.ProcedureCases.AnyAsync(item =>
                    item.TenantId == tenant.Id
                    && !item.IsDeleted
                    && item.ReferenceNumber == seed.ReferenceNumber);
                if (exists)
                {
                    continue;
                }

                var procedureCase = new ProcedureCase
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Module = "Estate",
                    EntityType = seed.EntityType,
                    Title = seed.Title,
                    ReferenceNumber = seed.ReferenceNumber,
                    ApplicantName = seed.ApplicantName,
                    SourceDepartment = seed.SourceDepartment,
                    ReceivedDate = now.Date.AddDays(seed.AgeDays * -1),
                    Description = seed.Description,
                    Status = "Open",
                    CurrentStageIndex = 0,
                    CurrentStageName = seed.CurrentStageName,
                    CurrentStageOwner = seed.CurrentStageOwner,
                    CurrentAssignedRole = seed.CurrentStageOwner,
                    OpenedById = ownerId,
                    LastActionById = ownerId,
                    CreatedById = ownerId,
                    CreatedBy = "System",
                    CreatedAt = now
                };

                foreach (var field in seed.Fields)
                {
                    procedureCase.Fields.Add(new ProcedureCaseField
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Key = field.Key,
                        Label = ToEstateSopFieldLabel(field.Key),
                        FieldType = InferEstateSopFieldType(field.Key),
                        Value = field.Value,
                        CreatedById = ownerId,
                        CreatedBy = "System",
                        CreatedAt = now
                    });
                }

                foreach (var check in seed.Checklist)
                {
                    procedureCase.ChecklistItems.Add(new ProcedureCaseChecklistItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        StageIndex = 0,
                        StageName = seed.CurrentStageName,
                        Text = check,
                        IsCompleted = true,
                        CompletedById = ownerId,
                        CompletedAt = now,
                        CreatedById = ownerId,
                        CreatedBy = "System",
                        CreatedAt = now
                    });
                }

                foreach (var document in seed.Documents)
                {
                    procedureCase.Documents.Add(new ProcedureCaseDocument
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Name = document,
                        RequiredFrom = seed.CurrentStageOwner,
                        IsMandatory = true,
                        Notes = "Example SOP evidence placeholder. Replace with uploaded live document in real cases.",
                        CreatedById = ownerId,
                        CreatedBy = "System",
                        CreatedAt = now
                    });
                }

                procedureCase.Activities.Add(new ProcedureCaseActivity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Action = "Example seeded",
                    StageName = seed.CurrentStageName,
                    Details = "Development example case showing how the Estate SOP fields, evidence, and handoff references should be filled.",
                    PerformedById = ownerId,
                    PerformedAt = now,
                    CreatedById = ownerId,
                    CreatedBy = "System",
                    CreatedAt = now
                });

                _context.ProcedureCases.Add(procedureCase);
            }

            await _context.SaveChangesAsync();
        }

        private static IReadOnlyList<EstateSopExampleCaseSeed> GetEstateSopExampleCaseSeeds() =>
        [
            new(
                "EstateTransfer",
                "EXAMPLE - Transfer of Property with Records Amendment",
                "EXAMPLE-EST-SOP-TRANSFER-001",
                "Kwesi Mensah / Ama Tetteh",
                "Estate Records",
                "Transfer Evidence Review",
                "Records Officer",
                5,
                "Shows Transfer Declaration, voluntary vacation, new lessee address, Finance/Legal checks, and Revenue/Estate Records amendment references.",
                new Dictionary<string, string?>
                {
                    ["sopSectionReference"] = "3.9 / Records: Transfer Of Property",
                    ["propertyNumber"] = "C5/SHOP/014",
                    ["housePlotShopNumber"] = "C5/SHOP/014",
                    ["transferProcessType"] = "Transfer of interest",
                    ["transferorName"] = "Kwesi Mensah",
                    ["transfereeName"] = "Ama Tetteh",
                    ["newLesseeAddress"] = "Plot 21, Community 5, Tema",
                    ["transferEffectiveDate"] = "2026-08-01",
                    ["transferDeclarationReference"] = "TD-FORM-2026-001",
                    ["voluntaryVacationReference"] = "VAC-2026-001",
                    ["considerationAmount"] = "185000",
                    ["transferFeePayable"] = "9250",
                    ["approvedFeeScheduleReference"] = "Appendix D - Transfer Fees",
                    ["documentTemplateReference"] = "EST-TRANSFER-DECLARATION",
                    ["financeHandoffStatus"] = "Receipt confirmed",
                    ["legalHandoffStatus"] = "Legal completed",
                    ["recordsHandoffStatus"] = "Pending Records update",
                    ["revenueRecordsReference"] = "REV-AMD-2026-001",
                    ["estateRecordsReference"] = "EST-REC-AMD-2026-001"
                },
                ["Transferor and transferee details captured", "Voluntary vacation evidence checked", "Finance and Legal handoffs referenced"],
                ["Transfer Declaration form completed by transferor and transferee", "Voluntary vacation of tenancy evidence", "Revenue and Estate Records amendment confirmation"]),
            new(
                "EstateHousingHomeOwnership",
                "EXAMPLE - Rental Unit Conversion to HOS",
                "EXAMPLE-EST-SOP-HOS-001",
                "Abena Owusu",
                "Housing",
                "Revenue and Records Check",
                "Housing Officer",
                4,
                "Shows rental-to-HOS conversion data including HOS form, rent card, selling price, purchase amount, and ledger update readiness.",
                new Dictionary<string, string?>
                {
                    ["sopSectionReference"] = "6.3 Conversion of Rental Units to HOS",
                    ["housingRequestType"] = "Conversion to HOS",
                    ["propertyNumber"] = "C6/HSE/088",
                    ["unitNumber"] = "C6/HSE/088",
                    ["applicantName"] = "Abena Owusu",
                    ["hosFormReference"] = "HOS-FORM-2026-014",
                    ["tenantNamesChangingToHos"] = "Abena Owusu",
                    ["houseType"] = "Two-bedroom terrace",
                    ["rentCardNumber"] = "RC-C6-088",
                    ["rentRegisterReference"] = "RENT-LEDGER-C6-088",
                    ["sellingPrice"] = "245000",
                    ["purchaseAmount"] = "245000",
                    ["purchaseDate"] = "2026-08-05",
                    ["paymentCompletionStatus"] = "Full selling price paid",
                    ["ledgerUpdateStatus"] = "HOS ledger updated",
                    ["approvedFeeScheduleReference"] = "Appendix D - HOS / rental conversion fees",
                    ["documentTemplateReference"] = "EST-HOS-CONVERSION, EST-RENT-CARD",
                    ["recordsUpdateReference"] = "HOS-LEDGER-2026-014"
                },
                ["HOS form reference captured", "Payment completion confirmed", "HOS ledger update reference captured"],
                ["House Ownership Scheme form for rental-to-HOS conversion", "House type and selling price / purchase amount schedule", "HOS ledger and Estate Records update evidence"]),
            new(
                "EstateLandsPartiallyServiced",
                "EXAMPLE - Partially Serviced Plot Proposal",
                "EXAMPLE-EST-SOP-LAND-001",
                "Tema Industrial Works Ltd",
                "Lands / Partially Serviced",
                "LMF and Ground Rent Calculation",
                "Estate Officer",
                3,
                "Shows application intake, approved fee appendix, LMF, ground rent, proposal, offer, and ROE references.",
                new Dictionary<string, string?>
                {
                    ["sopSectionReference"] = "5.3 Proposal Letters / LMF and Ground Rent",
                    ["landUse"] = "Industrial",
                    ["plotSizeAcres"] = "1.25",
                    ["lmfRatePerAcre"] = "125000",
                    ["landManagementFeePayable"] = "156250",
                    ["groundRentRatePerAcre"] = "2500",
                    ["groundRentComputed"] = "3125",
                    ["groundRentPayable"] = "3125",
                    ["paymentFrequency"] = "Annual",
                    ["approvedFeeScheduleReference"] = "Appendix D - Approved Fees and Charges",
                    ["documentTemplateReference"] = "EST-PROPOSAL-LETTER, EST-OFFER-LETTER, EST-RIGHT-ENTRY",
                    ["proposalLetterReference"] = "PROP-LPS-2026-007",
                    ["offerLetterReference"] = "OL-LPS-2026-007",
                    ["rightOfEntryReference"] = "ROE-LPS-2026-007",
                    ["financeHandoffStatus"] = "Pending invoice",
                    ["reportingReference"] = "Q3-LPS-PIPELINE"
                },
                ["Approved fee schedule selected", "LMF calculated", "Ground rent calculated"],
                ["LMF and Ground Rent calculation worksheet", "Proposal Letter with LMF and Ground Rent", "Offer Letter", "Right of Entry"]),
            new(
                "EstateLeasePreparation",
                "EXAMPLE - Lease Agreement Preparation",
                "EXAMPLE-EST-SOP-LEASE-001",
                "Tema Industrial Works Ltd",
                "Estate / Legal",
                "Estate Officer Processing",
                "Estate Officer",
                4,
                "Shows lease-request intake, cadastral and lease preparation fees, legal handoff, agreement template, and registered lease return.",
                new Dictionary<string, string?>
                {
                    ["sopSectionReference"] = "3.11 / Lease Preparation",
                    ["propertyNumber"] = "RP/23/A/23",
                    ["applicantName"] = "Tema Industrial Works Ltd",
                    ["landUse"] = "Industrial",
                    ["leaseTermYears"] = "50",
                    ["leaseCommencementDate"] = "2026-09-01",
                    ["groundRentPayable"] = "3125",
                    ["paymentFrequency"] = "Annual",
                    ["leasePreparationFee"] = "1500",
                    ["cadastralInvoiceReference"] = "CAD-INV-2026-044",
                    ["cadastralFeeReceiptReference"] = "CAD-RCPT-2026-044",
                    ["leaseRequestFormReference"] = "LRF-2026-020",
                    ["approvedFeeScheduleReference"] = "Appendix B / Appendix D - cadastral and lease preparation fees",
                    ["documentTemplateReference"] = "EST-LEASE-REQUEST, EST-LEASE-AGREEMENT",
                    ["legalHandoffStatus"] = "Sent to Legal",
                    ["legalLeasePreparationStatus"] = "Legal drafting",
                    ["registeredLeaseReference"] = "LC-REG-2026-020"
                },
                ["Lease request captured", "Cadastral and lease-preparation fees referenced", "Legal handoff captured"],
                ["Lease Request Form", "Cadastral and Lease Preparation invoice", "Draft / registered lease agreement"]),
            new(
                "EstateLeaseRenewal",
                "EXAMPLE - Lease Renewal and Deed of Variation",
                "EXAMPLE-EST-SOP-RENEW-001",
                "Akosua Boateng",
                "Estate / LRTC",
                "Finance and Legal Review",
                "Estate Officer",
                5,
                "Shows surrender/renewal review, premium and improved ground-rent approval, LRTC reference, and Deed of Variation control.",
                new Dictionary<string, string?>
                {
                    ["sopSectionReference"] = "3.14 Lease Renewal / Surrender and Renewal",
                    ["propertyNumber"] = "C11/PLOT/039",
                    ["applicantName"] = "Akosua Boateng",
                    ["originalLeaseReference"] = "LEASE-C11-039-1977",
                    ["variationReason"] = "Lease renewal after surrender option discussion",
                    ["existingLeaseExpiryDate"] = "2029-12-31",
                    ["yearsToExpiry"] = "3",
                    ["unexpiredTermBand"] = "10 years or less",
                    ["surrenderOptionStatus"] = "Surrender accepted",
                    ["renewalPremium"] = "42000",
                    ["improvedGroundRent"] = "1800",
                    ["approvedFeeScheduleReference"] = "LRTC approved renewal premium and improved Ground Rent",
                    ["documentTemplateReference"] = "EST-DEED-VARIATION, EST-RATE-REVISION",
                    ["lrtcReference"] = "LRTC-2026-018",
                    ["committeeDecision"] = "Approved",
                    ["deedOfVariationReference"] = "DOV-C11-039-2026"
                },
                ["LRTC approval captured", "Premium and improved ground rent recorded", "Deed of Variation reference captured"],
                ["LRTC approval form", "Offer / Deed of Variation", "Approved premium and improved Ground Rent schedule"]),
            new(
                "EstateReportingControls",
                "EXAMPLE - Estate Quarterly SOP Report Pack",
                "EXAMPLE-EST-SOP-REPORT-001",
                "Estate Management",
                "Estate Management",
                "Report Compilation",
                "Estate Officer",
                2,
                "Shows quarterly report control data for productivity, rent roll, debtor list, transfer/assignment return, fee appendix control, and Board submission.",
                new Dictionary<string, string?>
                {
                    ["sopSectionReference"] = "Quarterly Reports / Board Summary",
                    ["reportType"] = "Board summary",
                    ["reportingPeriod"] = "2026 Q3",
                    ["sourceSchedule"] = "All Estate",
                    ["applicationsReceived"] = "42",
                    ["applicationsProcessed"] = "31",
                    ["expectedRevenue"] = "860000",
                    ["paymentsReceived"] = "510000",
                    ["debtorCount"] = "18",
                    ["transfersCompleted"] = "6",
                    ["leasesOrMortgagesProcessed"] = "9",
                    ["appendixFeeVersion"] = "Appendix D - 2016 approved fees baseline",
                    ["boardSubmissionReference"] = "BOARD-EST-Q3-2026",
                    ["auditTrailReference"] = "DMS-EST-RPT-Q3-2026"
                },
                ["Period and source schedule captured", "Finance/Records report inputs reconciled", "Board submission reference captured"],
                ["Quarterly productivity report", "Transfer and assignment report", "Approved appendix fee schedule control sheet", "Board summary / approved report pack"])
        ];

        private sealed record EstateSopExampleCaseSeed(
            string EntityType,
            string Title,
            string ReferenceNumber,
            string ApplicantName,
            string SourceDepartment,
            string CurrentStageName,
            string CurrentStageOwner,
            int AgeDays,
            string Description,
            IReadOnlyDictionary<string, string?> Fields,
            IReadOnlyList<string> Checklist,
            IReadOnlyList<string> Documents);

        private static string InferEstateSopFieldType(string key)
        {
            if (key.Contains("Amount", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Fee", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Rent", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Price", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Revenue", StringComparison.OrdinalIgnoreCase))
            {
                return "currency";
            }

            if (key.Contains("Date", StringComparison.OrdinalIgnoreCase))
            {
                return "date";
            }

            if (key.Contains("Status", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Type", StringComparison.OrdinalIgnoreCase))
            {
                return "select";
            }

            if (key.Contains("Address", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Summary", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Names", StringComparison.OrdinalIgnoreCase))
            {
                return "textarea";
            }

            return "text";
        }

        private static string ToEstateSopFieldLabel(string key)
        {
            var chars = key.SelectMany((character, index) =>
                index > 0 && char.IsUpper(character)
                    ? new[] { ' ', character }
                    : new[] { character });

            var label = new string(chars.ToArray()).Trim();
            return string.IsNullOrWhiteSpace(label)
                ? key
                : char.ToUpperInvariant(label[0]) + label[1..];
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
                new { Name = Constants.Roles.ReadOnly, Description = "Read-only user for restricted system access" },
                new { Name = Constants.Roles.ExternalUser, Description = "External portal user (customers/vendors/partners/citizens)" },
                new { Name = Constants.Roles.HelpdeskAgent, Description = "Helpdesk agent for managing tickets" },
                new { Name = Constants.Roles.HelpdeskSupervisor, Description = "Helpdesk supervisor for assignment and escalation" },
                new { Name = Constants.Roles.HelpdeskManager, Description = "Helpdesk manager for dashboards and configuration" },
                new { Name = "Finance User", Description = "User with access to finance module" },
                new { Name = "Finance Clerk", Description = "Finance data entry role for journals, invoices, and supporting schedules" },
                new { Name = "Accounts Officer", Description = "Operational finance role for AP, AR, journals, and reconciliations" },
                new { Name = "Accounts Payable Officer", Description = "Supplier invoice and payables processing role" },
                new { Name = "Accounts Receivable Officer", Description = "Customer invoice, receivables, and collection processing role" },
                new { Name = "Senior Accountant", Description = "Review role for journals, AP/AR transactions, budgets, and period activities" },
                new { Name = "Finance Manager", Description = "Finance approval role for journals, budgets, AP/AR, and reporting" },
                new { Name = "Financial Controller", Description = "Senior finance control role for posting, period close, and finance administration" },
                new { Name = "Chief Accountant", Description = "Maker-checker approval role for bank deposits, returned cheques, and treasury settlement controls" },
                new { Name = "Managing Director", Description = "Restricted executive approval role for exceptional and high-value finance transactions" },
                new { Name = "Budget Officer", Description = "Budget preparation role for scenario returns and worksheet coordination" },
                new { Name = "HR User", Description = "User with access to HR module" },
                new { Name = "Sales User", Description = "User with access to sales module" },
                new { Name = "Inventory User", Description = "User with access to inventory module" },
                new { Name = "Procurement User", Description = "User with access to procurement module" },
                new { Name = "Marketing User", Description = "User with access to marketing module" },
                new { Name = "Registry Officer", Description = "Estate registry intake, form issue, file movement, and dispatch officer" },
                new { Name = "Records Officer", Description = "Estate records, register, ledger, amendment, and DMS indexing officer" },
                new { Name = "Housing Officer", Description = "Estate housing, rent card, HOS conversion, and housing records officer" },
                new { Name = "Planning Officer", Description = "Planning/site-plan coordination role for Estate SOP handoffs" },
                new { Name = "Estate Officer", Description = "Captures and submits land identification records" },
                new { Name = "Estate Manager", Description = "Reviews land suitability assessments" },
                new { Name = "Facilities Officer", Description = "Captures Facilities intake, maintenance handoffs, site service updates, and case closeout records" },
                new { Name = "Facilities Supervisor", Description = "Reviews Facilities intake triage, maintenance routing, SLA follow-up, and service completion controls" },
                new { Name = "Facilities Manager", Description = "Approves Facilities escalations, dashboards, provider decisions, billing coordination, and closeout governance" },
                new { Name = PropertyManagementRoles.Officer, Description = "Handles Property Management intake, handoffs, and customer updates" },
                new { Name = PropertyManagementRoles.Supervisor, Description = "Reviews Property Management availability and commercial terms" },
                new { Name = PropertyManagementRoles.Manager, Description = "Approves Property Management requests and operating decisions" },
                new { Name = "Survey Officer", Description = "Captures cadastral survey and demarcation records" },
                new { Name = "Senior Surveyor", Description = "Verifies cadastral surveys" },
                new { Name = "Legal Officer", Description = "Handles ownership classification and instrument execution" },
                new { Name = "Legal Manager", Description = "Approves ownership verification and statutory consent" },
                new { Name = "Legal Admin Assistant", Description = "Records Legal intake, files, dispatch, and matter routing" },
                new { Name = "Head of Legal", Description = "Approves Legal release, signatures, and final legal decisions" },
                new { Name = "Acquisition Committee", Description = "Handles land agreement negotiations" },
                new { Name = "Executive Approver", Description = "Approves negotiated land agreements" },
                new { Name = "Lands Commission Liaison", Description = "Submits statutory consent applications" },
                new { Name = "Finance Officer", Description = "Captures stamp duty assessments" },
                new { Name = "Finance Manager", Description = "Approves stamp duty assessments" },
                new { Name = "Accounts Payable", Description = "Records stamp duty payments" },
                new { Name = "Land Registry Officer", Description = "Records Lands Commission registrations" },
                new { Name = "Fixed Asset Officer", Description = "Creates acquired land assets" }
            };

            foreach (var roleInfo in roles)
            {
                var isProtectedSystemRole = Constants.Roles.IsProtectedSystemRole(roleInfo.Name);
                var existingRole = await _roleManager.FindByNameAsync(roleInfo.Name);
                if (existingRole == null)
                {
                    var role = new ApplicationRole(roleInfo.Name)
                    {
                        Description = roleInfo.Description,
                        IsSystemRole = isProtectedSystemRole,
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
                else if (isProtectedSystemRole && !existingRole.IsSystemRole)
                {
                    existingRole.IsSystemRole = true;
                    existingRole.UpdatedAt = DateTime.UtcNow;
                    existingRole.UpdatedBy = "System";

                    var result = await _roleManager.UpdateAsync(existingRole);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Marked protected role {RoleName} as a system role.", roleInfo.Name);
                    }
                    else
                    {
                        _logger.LogError(
                            "Failed to mark protected role {RoleName} as a system role: {Errors}",
                            roleInfo.Name,
                            string.Join(", ", result.Errors.Select(e => e.Description)));
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
                new { ModuleName = "Project Management", Description = "Projects, Quantity Survey, and Civil Engineering reports" },
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
            var permissionSeeds = new[]
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
                },
                new
                {
                    Name = "estate.land.project-readiness",
                    DisplayName = "Mark Land Project Ready",
                    Description = "Approve verified land demarcations for project management handoff",
                    Category = "Estate - Land Management"
                },
                new
                {
                    Name = "facilities.access",
                    DisplayName = "Access Facilities",
                    Description = "Access Estate / Facilities workspaces and navigation",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.dashboard.read",
                    DisplayName = "View Facilities Dashboard",
                    Description = "View Estate / Facilities operating dashboard and handoff status",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.case.read",
                    DisplayName = "View Facilities Cases",
                    Description = "View Facilities procedure cases, stages, documents, and activity",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.case.create",
                    DisplayName = "Create Facilities Cases",
                    Description = "Open new Facilities procedure cases and intake records",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.case.update",
                    DisplayName = "Update Facilities Cases",
                    Description = "Update Facilities intake fields, checklists, documents, and notes",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.case.approve",
                    DisplayName = "Approve Facilities Cases",
                    Description = "Approve Facilities stages, exceptions, publishing, and close-out decisions",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.handoff.create",
                    DisplayName = "Create Facilities Handoffs",
                    Description = "Create downstream module handoffs from Facilities",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.documents.manage",
                    DisplayName = "Manage Facilities Documents",
                    Description = "Manage Facilities document index and DMS readiness controls",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.finance.view",
                    DisplayName = "View Facilities Finance Context",
                    Description = "View Facilities billing, arrears, budget, expense, and payment-confirmation context",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "facilities.billing.manage",
                    DisplayName = "Manage Facilities Billing",
                    Description = "Prepare Facilities billing instruction packages and follow-up records",
                    Category = "Estate - Facilities"
                },
                new
                {
                    Name = "Finance.Read",
                    DisplayName = "View Finance",
                    Description = "View finance module records, setup, and reports",
                    Category = "Finance"
                },
                new
                {
                    Name = "Finance.Write",
                    DisplayName = "Maintain Finance",
                    Description = "Create and update operational finance records",
                    Category = "Finance"
                },
                new
                {
                    Name = "Finance.Admin",
                    DisplayName = "Administer Finance",
                    Description = "Manage finance setup, periods, segments, and control settings",
                    Category = "Finance"
                },
                new
                {
                    Name = "Finance.PeriodClose",
                    DisplayName = "Close Fiscal Periods",
                    Description = "Close fiscal periods after month-end checks",
                    Category = "Finance"
                },
                new
                {
                    Name = "Finance.PeriodReopen",
                    DisplayName = "Reopen Fiscal Periods",
                    Description = "Reopen previously closed fiscal periods",
                    Category = "Finance"
                },
                new
                {
                    Name = "Finance.JournalEntries.Create",
                    DisplayName = "Create Journal Entries",
                    Description = "Create manual journal entries",
                    Category = "Finance - General Ledger"
                },
                new
                {
                    Name = "Finance.JournalEntries.Edit",
                    DisplayName = "Edit Journal Entries",
                    Description = "Edit draft journal entries",
                    Category = "Finance - General Ledger"
                },
                new
                {
                    Name = "Finance.JournalEntries.Delete",
                    DisplayName = "Delete Journal Entries",
                    Description = "Delete draft journal entries",
                    Category = "Finance - General Ledger"
                },
                new
                {
                    Name = "Finance.JournalEntries.Write",
                    DisplayName = "Maintain Journal Entries",
                    Description = "Create and update journal entries",
                    Category = "Finance - General Ledger"
                },
                new
                {
                    Name = "Finance.JournalEntries.SubmitForApproval",
                    DisplayName = "Submit Journal Entries",
                    Description = "Submit journal entries for approval",
                    Category = "Finance - General Ledger"
                },
                new
                {
                    Name = "Finance.JournalEntries.Approve",
                    DisplayName = "Approve Journal Entries",
                    Description = "Approve or reject submitted journal entries",
                    Category = "Finance - General Ledger"
                },
                new
                {
                    Name = "Finance.JournalEntries.Post",
                    DisplayName = "Post Journal Entries",
                    Description = "Post approved journal entries to the ledger",
                    Category = "Finance - General Ledger"
                },
                new
                {
                    Name = "Finance.JournalEntries.Reverse",
                    DisplayName = "Reverse Journal Entries",
                    Description = "Create reversals for posted journal entries",
                    Category = "Finance - General Ledger"
                },
                new
                {
                    Name = "Finance.AP.Invoices.Create",
                    DisplayName = "Create AP Invoices",
                    Description = "Capture supplier invoices",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = "Finance.AP.Invoices.Edit",
                    DisplayName = "Edit AP Invoices",
                    Description = "Edit draft supplier invoices",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = "Finance.AP.Invoices.Delete",
                    DisplayName = "Delete AP Invoices",
                    Description = "Delete draft supplier invoices",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = "Finance.AP.Invoices.Write",
                    DisplayName = "Maintain AP Invoices",
                    Description = "Create and update supplier invoices",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = "Finance.AP.Invoices.SubmitForApproval",
                    DisplayName = "Submit AP Invoices",
                    Description = "Submit supplier invoices for approval",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = "Finance.AP.Invoices.Approve",
                    DisplayName = "Approve AP Invoices",
                    Description = "Approve or reject supplier invoices",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = "Finance.AP.Invoices.Void",
                    DisplayName = "Void AP Invoices",
                    Description = "Void supplier invoices with reversal controls",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = FinancePermissions.ManageApSupplierDebitNotes,
                    DisplayName = "Manage AP Supplier Debit Notes",
                    Description = "Create, edit, and cancel controlled supplier debit notes",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = FinancePermissions.SubmitApSupplierDebitNotes,
                    DisplayName = "Submit AP Supplier Debit Notes",
                    Description = "Submit supplier debit notes for independent approval",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = FinancePermissions.ApproveApSupplierDebitNotes,
                    DisplayName = "Approve AP Supplier Debit Notes",
                    Description = "Approve or reject submitted supplier debit notes",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = FinancePermissions.PostApSupplierDebitNotes,
                    DisplayName = "Post AP Supplier Debit Notes",
                    Description = "Post approved supplier debit notes through the Finance engine",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = FinancePermissions.ReverseApSupplierDebitNotes,
                    DisplayName = "Reverse AP Supplier Debit Notes",
                    Description = "Reverse posted supplier debit notes with compensating evidence",
                    Category = "Finance - Accounts Payable"
                },
                new
                {
                    Name = "Finance.AR.Invoices.Create",
                    DisplayName = "Create AR Invoices",
                    Description = "Create customer invoices",
                    Category = "Finance - Accounts Receivable"
                },
                new
                {
                    Name = "Finance.AR.Invoices.Edit",
                    DisplayName = "Edit AR Invoices",
                    Description = "Edit draft customer invoices",
                    Category = "Finance - Accounts Receivable"
                },
                new
                {
                    Name = "Finance.AR.Invoices.Delete",
                    DisplayName = "Delete AR Invoices",
                    Description = "Delete draft customer invoices",
                    Category = "Finance - Accounts Receivable"
                },
                new
                {
                    Name = "Finance.AR.Invoices.Write",
                    DisplayName = "Maintain AR Invoices",
                    Description = "Create and update customer invoices",
                    Category = "Finance - Accounts Receivable"
                },
                new
                {
                    Name = "Finance.AR.Invoices.Send",
                    DisplayName = "Send AR Invoices",
                    Description = "Finalize and send customer invoices",
                    Category = "Finance - Accounts Receivable"
                },
                new
                {
                    Name = "Finance.AR.Invoices.Void",
                    DisplayName = "Void AR Invoices",
                    Description = "Void customer invoices with reversal controls",
                    Category = "Finance - Accounts Receivable"
                },
                new
                {
                    Name = "Finance.Budgeting.Read",
                    DisplayName = "View Budgets",
                    Description = "View budget scenarios, returns, and worksheets",
                    Category = "Finance - Budgeting"
                },
                new
                {
                    Name = "Finance.Budgeting.Write",
                    DisplayName = "Maintain Budgets",
                    Description = "Create budget scenarios, returns, and entries",
                    Category = "Finance - Budgeting"
                },
                new
                {
                    Name = "Finance.BudgetReturns.Assign",
                    DisplayName = "Assign Budget Returns",
                    Description = "Assign budget worksheets to preparers",
                    Category = "Finance - Budgeting"
                },
                new
                {
                    Name = "Finance.BudgetReturns.Edit",
                    DisplayName = "Edit Assigned Budget Returns",
                    Description = "Edit assigned budget worksheets before submission",
                    Category = "Finance - Budgeting"
                },
                new
                {
                    Name = "Finance.BudgetReturns.Submit",
                    DisplayName = "Submit Budget Returns",
                    Description = "Submit assigned budget worksheets",
                    Category = "Finance - Budgeting"
                },
                new
                {
                    Name = "Finance.BudgetReturns.Approve",
                    DisplayName = "Approve Budget Returns",
                    Description = "Approve or reject submitted budget worksheets",
                    Category = "Finance - Budgeting"
                },
                new
                {
                    Name = "Finance.Budgeting.Lock",
                    DisplayName = "Lock Budgets",
                    Description = "Lock approved budget scenarios",
                    Category = "Finance - Budgeting"
                }
            }
            .Concat(FinancePermissions.All.Select(permission => new
            {
                permission.Name,
                permission.DisplayName,
                permission.Description,
                permission.Category
            }))
            .Concat(PropertyManagementPermissions.All.Select(permission => new
            {
                permission.Name,
                permission.DisplayName,
                permission.Description,
                permission.Category
            }))
            .Concat(HrPermissions.All.Select(permission => new
            {
                permission.Name,
                permission.DisplayName,
                permission.Description,
                permission.Category
            }))
            .GroupBy(permission => permission.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();

            foreach (var permissionInfo in permissionSeeds)
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

            var permissions = await _context.Permissions
                .Where(p => permissionSeeds.Select(info => info.Name).Contains(p.Name))
                .ToListAsync();

            if (!permissions.Any())
            {
                _logger.LogWarning("Seed permissions not found yet; skipping role-permission seed.");
                return;
            }

            var rolePermissionMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [Constants.Roles.SuperAdmin] = FinancePermissions.AllNames
                    .Concat(PropertyManagementPermissions.AllNames)
                    .Concat(HrPermissions.AllNames)
                    .Concat(new[]
                    {
                        "facilities.access",
                        "facilities.dashboard.read",
                        "facilities.case.read",
                        "facilities.case.create",
                        "facilities.case.update",
                        "facilities.case.approve",
                        "facilities.handoff.create",
                        "facilities.documents.manage",
                        "facilities.finance.view",
                        "facilities.billing.manage",
                        "maintenance.access"
                    })
                    .ToArray(),
                [Constants.Roles.TenantAdmin] = FinancePermissions.AllNames
                    .Concat(PropertyManagementPermissions.AllNames)
                    .Concat(HrPermissions.AllNames)
                    .Concat(new[]
                    {
                        "facilities.access",
                        "facilities.dashboard.read",
                        "facilities.case.read",
                        "facilities.case.create",
                        "facilities.case.update",
                        "facilities.case.approve",
                        "facilities.handoff.create",
                        "facilities.documents.manage",
                        "facilities.finance.view",
                        "facilities.billing.manage",
                        "maintenance.access"
                    })
                    .ToArray(),
                [PropertyManagementRoles.Officer] = PropertyManagementPermissions.OfficerNames,
                [PropertyManagementRoles.Supervisor] = PropertyManagementPermissions.SupervisorNames,
                [PropertyManagementRoles.Manager] = PropertyManagementPermissions.ManagerNames,
                ["Facilities Officer"] = new[]
                {
                    "facilities.access",
                    "facilities.case.read",
                    "facilities.case.create",
                    "facilities.case.update",
                    "facilities.handoff.create",
                    "maintenance.access"
                },
                ["Facilities Supervisor"] = new[]
                {
                    "facilities.access",
                    "facilities.dashboard.read",
                    "facilities.case.read",
                    "facilities.case.create",
                    "facilities.case.update",
                    "facilities.handoff.create",
                    "facilities.billing.manage",
                    "maintenance.access"
                },
                ["Facilities Manager"] = new[]
                {
                    "facilities.access",
                    "facilities.dashboard.read",
                    "facilities.case.read",
                    "facilities.case.create",
                    "facilities.case.update",
                    "facilities.case.approve",
                    "facilities.handoff.create",
                    "facilities.documents.manage",
                    "facilities.finance.view",
                    "facilities.billing.manage",
                    "maintenance.access"
                },
                ["Estate Officer"] = new[]
                {
                    "estate.land.project-readiness"
                },
                ["Estate Manager"] = new[]
                {
                    "estate.land.project-readiness"
                },
                [Constants.Roles.HelpdeskAgent] = new[]
                {
                    "enquiry.internal.access",
                    "enquiry.external.access",
                    "support.internal.access",
                    "support.external.access"
                },
                [Constants.Roles.HelpdeskSupervisor] = new[]
                {
                    "enquiry.internal.access",
                    "enquiry.external.access",
                    "support.internal.access",
                    "support.external.access"
                },
                [Constants.Roles.HelpdeskManager] = new[]
                {
                    "enquiry.internal.access",
                    "enquiry.external.access",
                    "support.internal.access",
                    "support.external.access"
                },
                ["Finance User"] = new[]
                {
                    "Finance.Read",
                    "Finance.Budgeting.Read",
                    "Finance.JournalEntries.Create",
                    "Finance.JournalEntries.Edit",
                    "Finance.JournalEntries.Write",
                    "Finance.JournalBatches.View"
                },
                ["Finance Clerk"] = new[]
                {
                    "Finance.Read",
                    "Finance.Write",
                    "Finance.JournalEntries.Create",
                    "Finance.JournalEntries.Edit",
                    "Finance.JournalEntries.Write",
                    "Finance.JournalBatches.View",
                    "Finance.JournalBatches.Create",
                    "Finance.JournalBatches.Edit",
                    "Finance.JournalBatches.Delete",
                    "Finance.JournalBatches.Import",
                    "Finance.JournalBatches.Export",
                    "Finance.JournalBatches.Copy",
                    "Finance.ChartOfAccounts.Manage",
                    "Finance.AP.Invoices.Create",
                    "Finance.AP.Invoices.Edit",
                    "Finance.AP.Invoices.Write",
                    "Finance.AP.Invoices.Manage",
                    "Finance.AR.Invoices.Create",
                    "Finance.AR.Invoices.Edit",
                    "Finance.AR.Invoices.Write",
                    "Finance.AR.Invoices.Manage",
                    "Finance.AR.Payments.Receive",
                    "Finance.BankAccounts.Manage",
                    "Finance.CashBank.Transactions.Record",
                    "Finance.CashBank.Documents.Issue",
                    "Finance.Banking.Deposits.Create",
                    "Finance.Budgeting.Read"
                },
                ["Accounts Officer"] = new[]
                {
                    "Finance.Read",
                    "Finance.Write",
                    "Finance.JournalEntries.Create",
                    "Finance.JournalEntries.Edit",
                    "Finance.JournalEntries.Write",
                    "Finance.JournalEntries.SubmitForApproval",
                    "Finance.JournalBatches.View",
                    "Finance.JournalBatches.Create",
                    "Finance.JournalBatches.Edit",
                    "Finance.JournalBatches.Delete",
                    "Finance.JournalBatches.SubmitForApproval",
                    "Finance.JournalBatches.Approve",
                    "Finance.JournalBatches.Import",
                    "Finance.JournalBatches.Export",
                    "Finance.JournalBatches.Copy",
                    "Finance.AP.Invoices.Create",
                    "Finance.AP.Invoices.Edit",
                    "Finance.AP.Invoices.Write",
                    "Finance.AP.Invoices.Manage",
                    "Finance.AP.Invoices.SubmitForApproval",
                    "Finance.AP.Invoices.Approve",
                    FinancePermissions.ManageApSupplierDebitNotes,
                    FinancePermissions.SubmitApSupplierDebitNotes,
                    FinancePermissions.ApproveApSupplierDebitNotes,
                    "Finance.AR.Invoices.Create",
                    "Finance.AR.Invoices.Edit",
                    "Finance.AR.Invoices.Write",
                    "Finance.AR.Invoices.Manage",
                    "Finance.AR.Invoices.Send",
                    "Finance.AR.Payments.Receive",
                    "Finance.CashBank.Transactions.Record",
                    "Finance.CashBank.Documents.Issue",
                    "Finance.Banking.Deposits.Create",
                    "Finance.Banking.Deposits.Submit",
                    "Finance.Banking.ReturnedCheques.Manage",
                    // Operational accountants may assemble an evidenced waiver request, but a
                    // separately permissioned reviewer must decide it.
                    "Finance.PeriodClose.Workspace.Maintain",
                    "Finance.PeriodClose.Waivers.Request",
                    "Finance.Workflow.Submit",
                    // Accounts Officers are configured as first-stage finance workflow reviewers.
                    // The generic permission opens the endpoint; the workflow assignment check
                    // still limits them to approval records assigned to their user or role.
                    "Finance.Workflow.Approve",
                    "Finance.Workflow.Reject",
                    "Finance.Migration.OpeningBalances.Prepare",
                    "Finance.Budgeting.Read",
                    "Finance.BudgetReturns.Edit",
                    "Finance.BudgetReturns.Submit"
                },
                ["Accounts Payable Officer"] = new[]
                {
                    "Finance.Read",
                    "Finance.Write",
                    "Finance.AP.Invoices.Create",
                    "Finance.AP.Invoices.Edit",
                    "Finance.AP.Invoices.Write",
                    "Finance.AP.Invoices.Manage",
                    "Finance.AP.Invoices.SubmitForApproval",
                    FinancePermissions.ManageApSupplierDebitNotes,
                    FinancePermissions.SubmitApSupplierDebitNotes,
                    FinancePermissions.ApproveApSupplierDebitNotes,
                    "Finance.AP.Payments.Process",
                    "Finance.CashBank.Documents.Issue",
                    "Finance.JournalEntries.Create",
                    "Finance.JournalEntries.Edit",
                    "Finance.JournalEntries.Write"
                },
                ["Accounts Payable"] = new[]
                {
                    "Finance.Read",
                    "Finance.AP.Payments.Process"
                },
                ["Accounts Receivable Officer"] = new[]
                {
                    "Finance.Read",
                    "Finance.Write",
                    "Finance.AR.Invoices.Create",
                    "Finance.AR.Invoices.Edit",
                    "Finance.AR.Invoices.Write",
                    "Finance.AR.Invoices.Manage",
                    "Finance.AR.Invoices.Send",
                    "Finance.AR.Payments.Receive",
                    "Finance.CashBank.Documents.Issue",
                    "Finance.JournalEntries.Create",
                    "Finance.JournalEntries.Edit",
                    "Finance.JournalEntries.Write"
                },
                ["Senior Accountant"] = new[]
                {
                    "Finance.Read",
                    "Finance.Write",
                    "Finance.JournalEntries.Create",
                    "Finance.JournalEntries.Edit",
                    "Finance.JournalEntries.Write",
                    "Finance.JournalEntries.SubmitForApproval",
                    "Finance.JournalBatches.View",
                    "Finance.JournalBatches.Create",
                    "Finance.JournalBatches.Edit",
                    "Finance.JournalBatches.Delete",
                    "Finance.JournalBatches.SubmitForApproval",
                    "Finance.JournalBatches.Approve",
                    "Finance.JournalBatches.Post",
                    "Finance.JournalBatches.Import",
                    "Finance.JournalBatches.Export",
                    "Finance.JournalBatches.Copy",
                    "Finance.AP.Invoices.Create",
                    "Finance.AP.Invoices.Edit",
                    "Finance.AP.Invoices.Write",
                    "Finance.AP.Invoices.Manage",
                    "Finance.AP.Invoices.SubmitForApproval",
                    "Finance.AP.Invoices.Approve",
                    FinancePermissions.ManageApSupplierDebitNotes,
                    FinancePermissions.SubmitApSupplierDebitNotes,
                    FinancePermissions.ApproveApSupplierDebitNotes,
                    "Finance.AP.Payments.Process",
                    "Finance.AR.Invoices.Create",
                    "Finance.AR.Invoices.Edit",
                    "Finance.AR.Invoices.Write",
                    "Finance.AR.Invoices.Manage",
                    "Finance.AR.Invoices.Send",
                    "Finance.AR.Payments.Receive",
                    "Finance.BankAccounts.Manage",
                    "Finance.CashBank.Transactions.Record",
                    "Finance.CashBank.Documents.Issue",
                    "Finance.BankReconciliation.Perform",
                    "Finance.Banking.LiquidityAccounts.Manage",
                    "Finance.Banking.Deposits.Create",
                    "Finance.Banking.Deposits.Submit",
                    "Finance.Banking.ReturnedCheques.Manage",
                    "Finance.Reports.Run",
                    "Finance.PeriodClose.Workspace.Maintain",
                    "Finance.PeriodClose.Waivers.Request",
                    "Finance.Workflow.Submit",
                    // Senior Accountants share the first-stage reviewer assignment with
                    // Accounts Officers and therefore require the same action permissions.
                    "Finance.Workflow.Approve",
                    "Finance.Workflow.Reject",
                    "Finance.Migration.OpeningBalances.Prepare",
                    "Finance.Budgeting.Read",
                    "Finance.Budgeting.Write",
                    "Finance.BudgetReturns.Assign",
                    "Finance.BudgetReturns.Edit",
                    "Finance.BudgetReturns.Submit"
                },
                ["Finance Manager"] = new[]
                {
                    "Finance.Read",
                    "Finance.Write",
                    "Finance.JournalEntries.Approve",
                    "Finance.JournalBatches.View",
                    "Finance.JournalBatches.Approve",
                    "Finance.JournalBatches.Post",
                    "Finance.JournalBatches.Reverse",
                    "Finance.JournalBatches.Export",
                    "Finance.JournalBatches.Copy",
                    "Finance.AP.Invoices.Approve",
                    FinancePermissions.ApproveApSupplierDebitNotes,
                    "Finance.AP.Payments.Approve",
                    "Finance.AR.Invoices.ApprovePost",
                    "Finance.AR.Invoices.Void",
                    "Finance.BankReconciliation.Approve",
                    // Finance management may issue an original when covering an operational role
                    // and may authorize a reason-backed replacement. The immutable issue service
                    // still prevents more than one document from being labelled Original.
                    "Finance.CashBank.Documents.Issue",
                    "Finance.CashBank.Documents.Reprint",
                    // Managers may request or review depending on the case; the service still
                    // prevents the same identity from performing both maker/checker actions.
                    "Finance.PeriodClose.Workspace.Maintain",
                    "Finance.PeriodClose.Waivers.Request",
                    "Finance.PeriodClose.Waivers.Approve",
                    // Finance Managers may establish the business case, while the domain still
                    // requires a distinct higher-tier identity to approve and apply the reopen.
                    "Finance.PeriodReopen",
                    "Finance.Reports.Run",
                    "Finance.Reports.Export",
                    "Finance.Workflow.Approve",
                    "Finance.Workflow.Reject",
                    "Finance.Workflow.RequestChanges",
                    "Finance.Budgeting.Read",
                    "Finance.Budgeting.Write",
                    "Finance.BudgetReturns.Assign",
                    "Finance.BudgetReturns.Edit",
                    "Finance.BudgetReturns.Approve",
                    "Finance.Budgeting.Lock"
                },
                ["Chief Accountant"] = new[]
                {
                    "Finance.Read",
                    "Finance.Write",
                    "Finance.BankAccounts.Manage",
                    "Finance.BankReconciliation.Perform",
                    "Finance.BankReconciliation.Approve",
                    "Finance.Banking.LiquidityAccounts.Manage",
                    "Finance.Banking.Deposits.Create",
                    "Finance.Banking.Deposits.Submit",
                    "Finance.Banking.Deposits.Approve",
                    // TDC baseline assigns the post-lodgement bank acknowledgement to the Chief
                    // Accountant. Service-level maker-checker still prevents the preparer from
                    // confirming their own deposit even if roles are customised later.
                    "Finance.Banking.Deposits.Confirm",
                    // TDC assigns replacement-copy supervision to the Chief Accountant. Ordinary
                    // cashiers retain original-issue rights but cannot generate duplicates.
                    "Finance.CashBank.Documents.Issue",
                    "Finance.CashBank.Documents.Reprint",
                    "Finance.Banking.Settings.Manage",
                    "Finance.Banking.ReturnedCheques.Manage",
                    "Finance.PeriodClose.Workspace.Maintain",
                    "Finance.PeriodClose.Waivers.Approve",
                    "Finance.PeriodReopen.Approve",
                    "Finance.AP.Payments.Approve",
                    FinancePermissions.PostApSupplierDebitNotes,
                    FinancePermissions.ReverseApSupplierDebitNotes,
                    "Finance.Workflow.Submit",
                    "Finance.Workflow.Approve",
                    "Finance.Workflow.Reject",
                    "Finance.Workflow.RequestChanges",
                    "Finance.Workflow.PostAfterApproval",
                    "Finance.Reports.Run",
                    // Chief Accountants own period-end review and controlled financial-report
                    // distribution. Export remains separately permission-gated at the API/UI.
                    "Finance.Reports.Export"
                },
                ["Managing Director"] = new[]
                {
                    // Deliberately narrow: executive approvers can inspect the finance record and
                    // decide an assigned workflow task, but cannot prepare, edit, post, or
                    // administer transactions merely because they hold final authority.
                    "Finance.Read",
                    "Finance.AP.Payments.Approve",
                    "Finance.Workflow.Approve",
                    "Finance.Workflow.Reject",
                    "Finance.Workflow.RequestChanges",
                    "Finance.Reports.Run",
                    "Finance.Reports.Export"
                },
                ["Financial Controller"] = FinancePermissions.AllNames,
                ["Budget Officer"] = new[]
                {
                    "Finance.Read",
                    "Finance.Budgeting.Read",
                    "Finance.Budgeting.Write",
                    "Finance.BudgetReturns.Assign",
                    "Finance.BudgetReturns.Edit",
                    "Finance.BudgetReturns.Submit"
                },
                // "HR User" is the role this seeder actually creates; note the HR controllers'
                // [Authorize(Roles = "HR")] attributes reference a bare "HR" that is not seeded
                // here. Both names are covered by HrPermissions.MedicalFallbackRoles.
                // HR staff maintain occupational-health records but do not administer them:
                // deleting a medical record stays with tenant administrators.
                ["HR User"] = new[]
                {
                    HrPermissions.ViewMedicalRecords,
                    HrPermissions.MaintainMedicalRecords
                }
            };

            foreach (var (roleName, permissionNames) in rolePermissionMap)
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

                var requestedPermissionNames = permissionNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missingPermissions = permissions
                    .Where(permission => requestedPermissionNames.Contains(permission.Name))
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
            const string financeJournalCategory = "finance-journal-attachments";
            const string financeCloseEvidenceCategory = "finance-close-evidence";
            const string financeJournalExtensions = ".pdf,.doc,.docx,.xls,.xlsx,.csv,.txt,.rtf,.jpg,.jpeg,.png";
            const string financeJournalMimeTypes =
                "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document," +
                "application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv," +
                "text/plain,application/rtf,image/jpeg,image/png";

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

                var journalPolicy = await _context.FileUploadPolicies
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Category == financeJournalCategory);

                if (journalPolicy == null)
                {
                    _context.FileUploadPolicies.Add(new FileUploadPolicy
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Category = financeJournalCategory,
                        IsEnabled = true,
                        MaxFileSizeBytes = 10 * 1024 * 1024,
                        MaxCategoryTotalBytes = 1024L * 1024 * 1024,
                        AllowedExtensionsCsv = financeJournalExtensions,
                        AllowedMimeTypesCsv = financeJournalMimeTypes,
                        RequireVirusScan = false,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    });
                }
                else
                {
                    var updated = false;
                    var extensions = MergeCsvValues(journalPolicy.AllowedExtensionsCsv, financeJournalExtensions, ensureLeadingDot: true);
                    if (!string.Equals(journalPolicy.AllowedExtensionsCsv, extensions, StringComparison.OrdinalIgnoreCase))
                    {
                        journalPolicy.AllowedExtensionsCsv = extensions;
                        updated = true;
                    }

                    var mimeTypes = MergeCsvValues(journalPolicy.AllowedMimeTypesCsv, financeJournalMimeTypes, ensureLeadingDot: false);
                    if (!string.Equals(journalPolicy.AllowedMimeTypesCsv, mimeTypes, StringComparison.OrdinalIgnoreCase))
                    {
                        journalPolicy.AllowedMimeTypesCsv = mimeTypes;
                        updated = true;
                    }

                    if (!journalPolicy.IsEnabled)
                    {
                        journalPolicy.IsEnabled = true;
                        updated = true;
                    }

                    if (updated)
                    {
                        journalPolicy.UpdatedAt = DateTime.UtcNow;
                        journalPolicy.UpdatedBy = "System";
                    }
                }

                // Close evidence uses the same Office/PDF/image baseline as journal evidence but
                // receives a distinct policy because close packs can legitimately be larger and
                // always require a clean scan. Existing tenant-specific size/quota decisions are
                // preserved; reconciliation only fills the controlled format/security baseline.
                var closeEvidencePolicy = await _context.FileUploadPolicies
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Category == financeCloseEvidenceCategory);
                if (closeEvidencePolicy == null)
                {
                    _context.FileUploadPolicies.Add(new FileUploadPolicy
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Category = financeCloseEvidenceCategory,
                        IsEnabled = true,
                        MaxFileSizeBytes = 20 * 1024 * 1024,
                        MaxCategoryTotalBytes = 1024L * 1024 * 1024,
                        AllowedExtensionsCsv = financeJournalExtensions,
                        AllowedMimeTypesCsv = financeJournalMimeTypes,
                        RequireVirusScan = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    });
                }
                else
                {
                    var updated = false;
                    var extensions = MergeCsvValues(closeEvidencePolicy.AllowedExtensionsCsv, financeJournalExtensions, ensureLeadingDot: true);
                    if (!string.Equals(closeEvidencePolicy.AllowedExtensionsCsv, extensions, StringComparison.OrdinalIgnoreCase))
                    {
                        closeEvidencePolicy.AllowedExtensionsCsv = extensions;
                        updated = true;
                    }

                    var mimeTypes = MergeCsvValues(closeEvidencePolicy.AllowedMimeTypesCsv, financeJournalMimeTypes, ensureLeadingDot: false);
                    if (!string.Equals(closeEvidencePolicy.AllowedMimeTypesCsv, mimeTypes, StringComparison.OrdinalIgnoreCase))
                    {
                        closeEvidencePolicy.AllowedMimeTypesCsv = mimeTypes;
                        updated = true;
                    }

                    if (!closeEvidencePolicy.IsEnabled || !closeEvidencePolicy.RequireVirusScan)
                    {
                        closeEvidencePolicy.IsEnabled = true;
                        closeEvidencePolicy.RequireVirusScan = true;
                        updated = true;
                    }

                    if (updated)
                    {
                        closeEvidencePolicy.UpdatedAt = DateTime.UtcNow;
                        closeEvidencePolicy.UpdatedBy = "System";
                    }
                }
            }

            await _context.SaveChangesAsync();

            static string MergeCsvValues(string? existingCsv, string requiredCsv, bool ensureLeadingDot)
            {
                var values = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var csv in new[] { existingCsv, requiredCsv })
                {
                    if (string.IsNullOrWhiteSpace(csv)) continue;

                    foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var value = raw.Trim();
                        if (string.IsNullOrWhiteSpace(value)) continue;
                        if (ensureLeadingDot && !value.StartsWith('.')) value = "." + value;
                        values.Add(value);
                    }
                }

                return string.Join(",", values);
            }
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
                // Keep the seeded account usable without forcing an edited email back to the seed default.
                var needsUpdate = false;
                if (existingUser.TenantId != tenantId) { existingUser.TenantId = tenantId; needsUpdate = true; }
                if (string.IsNullOrWhiteSpace(existingUser.Email))
                {
                    existingUser.Email = email;
                    needsUpdate = true;
                }
                else
                {
                    var normalizedEmail = _userManager.NormalizeEmail(existingUser.Email);
                    if (!string.Equals(existingUser.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
                    {
                        existingUser.NormalizedEmail = normalizedEmail;
                        needsUpdate = true;
                    }
                }
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

                _logger.LogInformation("Ensured existing seeded user {Username}; password was not changed.", username);

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
            services.AddScoped<PaymentTermBaselineSeeder>();
            services.AddScoped<FinanceCloseTemplateBaselineSeeder>();
            services.AddScoped<ProcurementConfigurationProfileSeeder>();
            services.AddScoped<ProcurementAccessControlSeeder>();
            services.AddScoped<ProcurementStatutoryReportSeeder>();
            services.AddScoped<InventoryStatutoryReportSeeder>();
            services.AddScoped<AuditComplianceReportSeeder>();
            services.AddScoped<QuantitySurveyAccessControlSeeder>();
            services.AddScoped<QuantitySurveyConfigurationProfileSeeder>();
            services.AddScoped<QuantitySurveyStatutoryReportSeeder>();
            services.AddScoped<ProcurementSupplierOnboardingTestSeeder>();
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
