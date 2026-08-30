using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Creates the narrow, disposable SQL Server fixture used by the authenticated Civil
/// Playwright acceptance. It refuses to run outside a uniquely named browser-test database.
/// Actor passwords must be supplied as ephemeral environment variables by the harness.
/// </summary>
public sealed class CivilEngineeringE2ETestSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    CivilEngineeringAccessControlSeeder accessControlSeeder,
    CivilEngineeringConfigurationProfileSeeder configurationSeeder)
{
    public static readonly Guid ProjectId = Guid.Parse("c1b5f34d-618c-42af-95ea-744e9fe32b32");
    private static readonly Guid IsolationTenantId = Guid.Parse("d892c595-2e62-409a-b83e-d58c66437570");
    private static readonly Guid WorkflowId = Guid.Parse("60a38642-67f4-4ca0-aa07-9e3c050db99f");
    private static readonly Guid WorkflowStepId = Guid.Parse("f21b4d60-08c3-474b-be9b-fd452cc39b6a");
    private static readonly Guid MetadataTemplateId = Guid.Parse("41d93292-8367-462f-912a-11f4fa7f5f33");
    private const string DatabasePrefix = "RhemaERP_CivilBrowser_";

    public async Task SeedAsync(CancellationToken token = default)
    {
        var databaseName = db.Database.GetDbConnection().Database;
        if (!databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Civil browser seeding is restricted to databases named {DatabasePrefix}<unique-id>.");

        var tenant = await db.Tenants.IgnoreQueryFilters()
            .SingleAsync(value => value.Code == "DEFAULT" && !value.IsDeleted, token);

        await accessControlSeeder.SeedAsync(token);
        await configurationSeeder.SeedTenantAsync(tenant.Id, null, token);

        var assigner = await PrepareActorAsync("manager", "RHEMA_CIVIL_E2E_ASSIGNER_PASSWORD",
            CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, token);
        var assignee = await PrepareActorAsync("employee", "RHEMA_CIVIL_E2E_ASSIGNEE_PASSWORD",
            CivilEngineeringAccessControlRegistry.TechnicianRole, token);
        var reviewer = await PrepareActorAsync("helpdesk.supervisor", "RHEMA_CIVIL_E2E_REVIEWER_PASSWORD",
            CivilEngineeringAccessControlRegistry.CivilEngineerRole, token);
        _ = await PrepareActorAsync("external", "RHEMA_CIVIL_E2E_UNAUTHORIZED_PASSWORD", null, token);
        await PrepareIsolationActorAsync(token);

        var project = await db.Projects.IgnoreQueryFilters().SingleOrDefaultAsync(value => value.Id == ProjectId, token);
        if (project is null)
        {
            project = new Project
            {
                Id = ProjectId,
                TenantId = tenant.Id,
                ProjectCode = "CIV-E2E-001",
                Title = "Civil Engineering Browser Acceptance",
                Summary = "Disposable Civil Engineering role-separated browser acceptance fixture.",
                Status = ProjectStatuses.Approved,
                Methodology = "Controlled Civil UAT",
                ProjectManagerId = assigner.Id,
                ApprovalRequired = false,
                StartDate = DateTime.UtcNow.Date,
                TargetEndDate = DateTime.UtcNow.Date.AddMonths(1),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Civil E2E Seeder",
                CreatedById = assigner.Id
            };
            db.Projects.Add(project);
        }

        await EnsureProjectMemberAsync(tenant.Id, project.Id, assigner.Id,
            CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, token);
        await EnsureProjectMemberAsync(tenant.Id, project.Id, assignee.Id,
            CivilEngineeringAccessControlRegistry.TechnicianRole, token);
        await EnsureProjectMemberAsync(tenant.Id, project.Id, reviewer.Id,
            CivilEngineeringAccessControlRegistry.CivilEngineerRole, token);

        var entityType = await db.WorkflowEntityTypes.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenant.Id && value.Code == CivilEngineeringWorkflowBindingRegistry.DirectTask && !value.IsDeleted, token);

        var workflow = await db.WorkflowDefinitions.IgnoreQueryFilters().SingleOrDefaultAsync(value => value.Id == WorkflowId, token);
        if (workflow is null)
        {
            workflow = new WorkflowDefinition
            {
                Id = WorkflowId,
                TenantId = tenant.Id,
                DefinitionKey = WorkflowId,
                Name = "Civil E2E Direct Task Acceptance",
                Description = "Single independent Civil Engineer acceptance step for disposable browser UAT.",
                EntityTypeId = entityType.Id,
                Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true,
                PublishedAt = DateTime.UtcNow,
                PublishedById = reviewer.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Civil E2E Seeder",
                CreatedById = assigner.Id
            };
            db.WorkflowDefinitions.Add(workflow);
        }

        if (!await db.WorkflowSteps.IgnoreQueryFilters().AnyAsync(value => value.Id == WorkflowStepId, token))
        {
            db.WorkflowSteps.Add(new WorkflowStep
            {
                Id = WorkflowStepId,
                TenantId = tenant.Id,
                WorkflowDefinitionId = workflow.Id,
                Name = "Independent Civil completion acceptance",
                Description = "Completion must be accepted by a Civil Engineer who is not the assignee.",
                StepType = WorkflowStepType.Approval,
                Order = 1,
                IsStartStep = true,
                IsEndStep = true,
                AssignmentType = "Role",
                RequiredRole = CivilEngineeringAccessControlRegistry.CivilEngineerRole,
                IsRequired = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Civil E2E Seeder",
                CreatedById = assigner.Id
            });
        }

        var template = await db.CentralDocumentMetadataTemplates.IgnoreQueryFilters()
            .SingleOrDefaultAsync(value => value.Id == MetadataTemplateId, token);
        if (template is null)
        {
            db.CentralDocumentMetadataTemplates.Add(new CentralDocumentMetadataTemplate
            {
                Id = MetadataTemplateId,
                TenantId = tenant.Id,
                Module = "Civil Engineering",
                DocumentType = "Direct task feedback",
                TemplateCode = "CIV-E2E-DIRECT-TASK",
                SourceLabel = "Disposable Civil browser acceptance",
                RequiredFieldsJson = "[]",
                RelationshipsJson = "[]",
                RetentionRule = "Disposable acceptance evidence",
                AccessProfile = "Civil project members",
                IsActive = true,
                PublishedAt = DateTime.UtcNow,
                PublishedById = reviewer.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Civil E2E Seeder",
                CreatedById = assigner.Id
            });
        }

        await db.SaveChangesAsync(token);

        var profile = await db.CivilEngineeringConfigurationProfiles.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenant.Id && value.ProfileCode == "TDC-CIVIL-ENGINEERING" && !value.IsDeleted)
            .OrderByDescending(value => value.Version)
            .FirstAsync(token);
        profile.LifecycleStatus = CivilEngineeringConfigurationProfileStatus.Published;
        profile.IsDefault = true;
        profile.EffectiveFrom = DateTime.UtcNow.Date.AddDays(-1);
        profile.PublishedAt = DateTime.UtcNow;
        profile.PublishedById = reviewer.Id;

        var decision = await db.CivilEngineeringConfigurationDecisions.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenant.Id && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-010" && !value.IsDeleted, token);
        decision.Status = CivilEngineeringConfigurationDecisionStatus.Approved;
        decision.ApprovalStatus = CivilEngineeringConfigurationApprovalStatus.Approved;
        decision.EvidenceStatus = CivilEngineeringConfigurationEvidenceStatus.Verified;
        decision.DecisionDate = DateTime.UtcNow;
        decision.EffectiveFrom = profile.EffectiveFrom;
        decision.ApprovedAt = DateTime.UtcNow;
        decision.ApprovedById = reviewer.Id;
        decision.ApprovalReference = "CIV-E2E-DISPOSABLE";
        decision.ValueJson = JsonSerializer.Serialize(new CivilEngineeringTaskAssignmentValue
        {
            EffectiveFrom = profile.EffectiveFrom,
            WorkflowDefinitionId = WorkflowId,
            FeedbackMetadataTemplateId = MetadataTemplateId,
            AssigneeRoleIds = [await RoleIdAsync(CivilEngineeringAccessControlRegistry.TechnicianRole, token)],
            UrgentEscalationRoleIds = [await RoleIdAsync(CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, token)],
            AllowedUrgencies = [CivilEngineeringUrgency.Routine],
            UrgentResponseHours = 24,
            AllowControlledReassignment = false,
            RequireDueDate = true,
            RequireFeedbackEvidence = false,
            RequireClosureAcceptance = true
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await db.SaveChangesAsync(token);
    }

    private async Task<ApplicationUser> PrepareActorAsync(
        string username,
        string passwordEnvironmentName,
        string? civilRole,
        CancellationToken token)
    {
        var password = Environment.GetEnvironmentVariable(passwordEnvironmentName);
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"{passwordEnvironmentName} must be set for disposable Civil browser seeding.");

        var user = await userManager.FindByNameAsync(username)
            ?? throw new InvalidOperationException($"The development seed did not create the required disposable actor '{username}'.");
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, resetToken, password);
        if (!reset.Succeeded)
            throw new InvalidOperationException($"Could not assign the ephemeral password for actor '{username}'.");

        if (!string.IsNullOrWhiteSpace(civilRole) && !await userManager.IsInRoleAsync(user, civilRole))
        {
            var roleResult = await userManager.AddToRoleAsync(user, civilRole);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Could not assign the Civil acceptance role to actor '{username}'.");
        }

        return user;
    }

    private async Task PrepareIsolationActorAsync(CancellationToken token)
    {
        const string username = "civil.tenant-isolation";
        var password = Environment.GetEnvironmentVariable("RHEMA_CIVIL_E2E_ISOLATION_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("RHEMA_CIVIL_E2E_ISOLATION_PASSWORD must be set for disposable Civil browser seeding.");

        var tenant = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(value => value.Id == IsolationTenantId, token);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = IsolationTenantId,
                Name = "Civil Browser Isolation Tenant",
                Code = "CIVIL-ISOLATION",
                Description = "Disposable second-tenant boundary used only by Civil browser acceptance.",
                Status = TenantStatus.Active,
                BaseCurrency = "GHS",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Civil E2E Seeder"
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(token);
        }

        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = username,
                Email = "civil.tenant-isolation@acceptance.test",
                EmailConfirmed = true,
                FirstName = "Civil",
                LastName = "Isolation",
                TenantId = tenant.Id,
                AuthenticationProvider = AuthenticationProvider.Local,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Civil E2E Seeder"
            };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Could not create the Civil tenant-isolation actor: {string.Join(", ", created.Errors.Select(value => value.Description))}");
        }
        else
        {
            user.TenantId = tenant.Id;
            user.IsActive = true;
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, resetToken, password);
            if (!reset.Succeeded)
                throw new InvalidOperationException("Could not assign the ephemeral password for the Civil tenant-isolation actor.");
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, CivilEngineeringAccessControlRegistry.CivilEngineerRole))
        {
            var roleResult = await userManager.AddToRoleAsync(user, CivilEngineeringAccessControlRegistry.CivilEngineerRole);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Could not assign the Civil tenant-isolation role.");
        }

        if (!await db.UserTenants.IgnoreQueryFilters().AnyAsync(value =>
                value.UserId == user.Id && value.TenantId == tenant.Id && !value.IsDeleted, token))
        {
            db.UserTenants.Add(new UserTenant
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenant.Id,
                AccessLevel = UserTenantAccessLevel.Standard,
                Status = UserTenantStatus.Active,
                IsDefault = true,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = "Civil E2E Seeder",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Civil E2E Seeder"
            });
            await db.SaveChangesAsync(token);
        }
    }

    private async Task EnsureProjectMemberAsync(Guid tenantId, Guid projectId, Guid userId, string role, CancellationToken token)
    {
        if (await db.ProjectMembers.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && value.ProjectId == projectId && value.UserId == userId && value.Role == role && !value.IsDeleted, token))
            return;

        db.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Civil E2E Seeder"
        });
    }

    private async Task<Guid> RoleIdAsync(string name, CancellationToken token) =>
        await db.Roles.Where(value => value.Name == name).Select(value => value.Id).SingleAsync(token);
}
