using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Prepares role-separated actors, typed prerequisites, and clearly labelled
/// fixture-assisted tender scenarios for the disposable tender browser acceptance run.
/// Sourcing releases and sourcing cases remain API-owned and are linked by the guarded
/// preparation script only after their authoritative identities have been returned.
/// The harness supplies all passwords through process-scoped environment variables.
/// </summary>
public sealed class TenderLifecycleE2ETestSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager)
{
    private const string DatabasePrefix = "RhemaERP_TenderBrowser_";
    private const string FixtureActor = "Tender E2E Seeder (fixture prerequisites only)";

    private static readonly ActorDefinition[] Actors =
    [
        new("manager", "tender.requester@acceptance.test", "Tender", "Requester",
            "TENDER_E2E_REQUESTER_PASSWORD", "TDC_REQUISITIONER"),
        new("finance.clerk", "tender.pr-approver@acceptance.test", "Tender", "PR Approver",
            "TENDER_E2E_PR_APPROVER_PASSWORD", "TDC_FINANCE_REVIEWER"),
        new("accounts.officer", "tender.officer@acceptance.test", "Tender", "Officer",
            "TENDER_E2E_PROCUREMENT_OFFICER_PASSWORD", "TDC_PROCUREMENT_OFFICER"),
        new("ap.officer", "tender.fee-verifier@acceptance.test", "Tender", "Fee Verifier",
            "TENDER_E2E_FEE_VERIFIER_PASSWORD", "TDC_PROCUREMENT_OFFICER"),
        new("helpdesk.agent", "tender.evaluator-a@acceptance.test", "Tender", "Evaluator A",
            "TENDER_E2E_EVALUATOR_PASSWORD", "TDC_EVALUATOR"),
        new("helpdesk.supervisor", "tender.evaluator-b@acceptance.test", "Tender", "Evaluator B",
            "TENDER_E2E_EVALUATOR_B_PASSWORD", "TDC_EVALUATOR"),
        new("helpdesk.manager", "tender.evaluator-c@acceptance.test", "Tender", "Evaluator C",
            "TENDER_E2E_EVALUATOR_C_PASSWORD", "TDC_EVALUATOR"),
        new("financial.controller", "tender.approver@acceptance.test", "Tender", "Approver",
            "TENDER_E2E_APPROVER_PASSWORD", "TDC_HEAD_OF_PROCUREMENT"),
        new("tdc.tender.etc-approver", "tender.etc-approver@acceptance.test", "Tender", "ETC Approver",
            "TENDER_E2E_ETC_APPROVER_PASSWORD", "TDC_ETC_MEMBER"),
        new("chief.accountant", "tender.contract-approver@acceptance.test", "Tender", "Contract Approver",
            "TENDER_E2E_CONTRACT_APPROVER_PASSWORD", "TDC_LEGAL_REVIEWER"),
        new("external", "tender.supplier-a@acceptance.test", "Tender", "Supplier A",
            "TENDER_E2E_SUPPLIER_PASSWORD", Constants.Roles.ExternalUser),
        new("tdc.tender.supplier-b", "tender.supplier-b@acceptance.test", "Tender", "Supplier B",
            "TENDER_E2E_SUPPLIER_B_PASSWORD", Constants.Roles.ExternalUser),
        new("tdc.tender.supplier-blocked", "tender.supplier-blocked@acceptance.test", "Tender", "Supplier Blocked",
            "TENDER_E2E_SUPPLIER_BLOCKED_PASSWORD", Constants.Roles.ExternalUser),
        new("employee", "tender.unauthorized@acceptance.test", "Tender", "Unauthorized",
            "TENDER_E2E_UNAUTHORIZED_PASSWORD", null)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Testing",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tender browser actor seeding is restricted to ASPNETCORE_ENVIRONMENT=Testing.");
        }

        var databaseName = db.Database.GetDbConnection().Database;
        if (!Regex.IsMatch(
                databaseName,
                $"^{Regex.Escape(DatabasePrefix)}[0-9a-f]{{32}}$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException(
                $"Tender browser fixture seeding is restricted to databases named {DatabasePrefix}<32-hex-id>.");
        }

        var runId = Environment.GetEnvironmentVariable("TENDER_E2E_RUN_ID")?.Trim();
        if (string.IsNullOrWhiteSpace(runId) ||
            !Regex.IsMatch(runId, "^[A-Za-z0-9][A-Za-z0-9._-]{7,63}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "TENDER_E2E_RUN_ID must contain 8-64 safe characters for disposable fixture correlation.");
        }

        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(
            value => value.Code == "DEFAULT" && !value.IsDeleted,
            cancellationToken);

        foreach (var actor in Actors)
        {
            await PrepareActorAsync(tenant.Id, actor, cancellationToken);
        }

        await PreparePrerequisitesAsync(tenant.Id, runId, cancellationToken);
    }

    private async Task PreparePrerequisitesAsync(
        Guid tenantId,
        string runId,
        CancellationToken cancellationToken)
    {
        var requester = await RequiredUserAsync("manager", tenantId, cancellationToken);
        var approver = await RequiredUserAsync("finance.clerk", tenantId, cancellationToken);
        var officer = await RequiredUserAsync("accounts.officer", tenantId, cancellationToken);
        var tenderApprover = await RequiredUserAsync("financial.controller", tenantId, cancellationToken);
        var etcApprover = await RequiredUserAsync("tdc.tender.etc-approver", tenantId, cancellationToken);
        var contractApprover = await RequiredUserAsync("chief.accountant", tenantId, cancellationToken);
        var supplierAUser = await RequiredUserAsync("external", tenantId, cancellationToken);
        var supplierBUser = await RequiredUserAsync("tdc.tender.supplier-b", tenantId, cancellationToken);
        var blockedSupplierUser = await RequiredUserAsync(
            "tdc.tender.supplier-blocked", tenantId, cancellationToken);

        var department = await db.Departments.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive)
            .OrderBy(value => value.Code)
            .FirstOrDefaultAsync(cancellationToken);
        if (department is null)
        {
            department = new Department
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Tender Browser Acceptance",
                Code = $"TE2E-{ShortHash(runId)}",
                Description = "Fixture-only department for disposable tender browser acceptance.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = FixtureActor
            };
            db.Departments.Add(department);
            await db.SaveChangesAsync(cancellationToken);
        }

        var goodsCategory = await EnsureGoodsCategoryAsync(tenantId, cancellationToken);
        await EnsureTenderApprovalWorkflowAsync(
            tenantId, officer, tenderApprover, cancellationToken);
        await EnsureDedicatedApprovalWorkflowAsync(
            tenantId,
            "TDC Tender E2E PR Authority",
            "PURCHASE_REQUISITION",
            "PurchaseRequisition",
            "TDC_REQUISITIONER",
            "TDC_HEAD_OF_PROCUREMENT",
            requester,
            tenderApprover,
            cancellationToken);
        await EnsureDedicatedApprovalWorkflowAsync(
            tenantId,
            "TDC Tender E2E Award Approval",
            "TENDER_AWARD",
            "TenderAward",
            "TDC_HEAD_OF_PROCUREMENT",
            "TDC_ETC_MEMBER",
            tenderApprover,
            etcApprover,
            cancellationToken);
        await EnsureDedicatedApprovalWorkflowAsync(
            tenantId,
            "TDC Tender E2E Contract Activation",
            "PROCUREMENT_CONTRACT",
            "Procurement Contract",
            "TDC_PROCUREMENT_OFFICER",
            "TDC_LEGAL_REVIEWER",
            officer,
            contractApprover,
            cancellationToken);
        await EnsureExclusiveActiveWorkflowAsync(
            tenantId, "PROCUREMENT_CONTRACT", "TDC Tender E2E Contract Activation",
            contractApprover, cancellationToken);
        await EnsureSourcingPolicyAsync(
            tenantId, runId, officer, cancellationToken);
        await EnsureSupplierAsync(
            tenantId, runId, "A", supplierAUser, approver, goodsCategory, suspended: false,
            cancellationToken);
        await EnsureSupplierAsync(
            tenantId, runId, "B", supplierBUser, approver, goodsCategory, suspended: false,
            cancellationToken);
        await EnsureSupplierAsync(
            tenantId, runId, "BLOCKED", blockedSupplierUser, approver, goodsCategory,
            suspended: true, cancellationToken);
        await EnsureCurrentOpenFiscalPeriodAsync(tenantId, runId, cancellationToken);

        await EnsureApprovedSourceAsync(
            tenantId,
            runId,
            "PREPUBLICATION",
            requester,
            approver,
            department,
            cancellationToken);
        await EnsureApprovedSourceAsync(
            tenantId,
            runId,
            "LIFECYCLE-SOURCE",
            requester,
            approver,
            department,
            cancellationToken);
        await EnsureApprovedSourceAsync(
            tenantId,
            runId,
            "ALTERNATE-HANDOFF",
            requester,
            approver,
            department,
            cancellationToken);
        await EnsureApprovedSourceAsync(
            tenantId,
            runId,
            "CONTRACT-HANDOFF",
            requester,
            approver,
            department,
            cancellationToken);
        foreach (var scenario in new[]
                 {
                     "SUPPLIER-LIFECYCLE",
                     "COMMITTEE-ABSENT",
                     "COMMITTEE-DRAFT",
                     "COMMITTEE-ACTIVE"
                 })
        {
            await EnsureApprovedSourceAsync(
                tenantId,
                runId,
                scenario,
                requester,
                approver,
                department,
                cancellationToken);
        }
        await EnsureEvaluationTemplateAsync(
            tenantId, runId, officer, cancellationToken);
        await EnsureFixtureAssistedScenariosAsync(
            tenantId, runId, officer, cancellationToken);
        await EnsureAlternateApprovedAwardAsync(
            tenantId, runId, officer, tenderApprover,
            cancellationToken);
        await EnsureContractApprovedAwardAsync(
            tenantId, runId, officer, tenderApprover, etcApprover,
            cancellationToken);
        await EnsureCrossTenantTenderAsync(runId, cancellationToken);
    }

    private async Task EnsureTenderApprovalWorkflowAsync(
        Guid tenantId,
        ApplicationUser procurementOfficer,
        ApplicationUser headOfProcurement,
        CancellationToken cancellationToken)
    {
        // Do not mutate the standard Draft workflow seeded from the tenant registry.
        // The disposable acceptance run owns a separate published definition so its
        // fixture setup cannot collide with the registry seeder's tracked children.
        const string workflowName = "TDC Tender E2E Approval";
        const string initiatorRole = "TDC_PROCUREMENT_OFFICER";
        const string approverRole = "TDC_HEAD_OF_PROCUREMENT";
        var now = DateTime.UtcNow;

        var entityType = await db.WorkflowEntityTypes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(value =>
                    value.TenantId == tenantId &&
                    !value.IsDeleted &&
                    (value.Code == "TENDER" || value.Name == "Tender"),
                cancellationToken);
        if (entityType is null)
        {
            entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "TENDER",
                Name = "Tender",
                Description = "TDC tender approval workflow entity.",
                IsActive = true,
                DisplayOrder = 500,
                CreatedAt = now,
                CreatedBy = FixtureActor,
                CreatedById = procurementOfficer.Id
            };
            db.WorkflowEntityTypes.Add(entityType);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            entityType.IsActive = true;
        }

        var definition = await db.WorkflowDefinitions.IgnoreQueryFilters()
            .Include(value => value.Steps)
            .Include(value => value.Transitions)
            .Where(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                value.EntityTypeId == entityType.Id &&
                value.Name == workflowName)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (definition is null)
        {
            definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = workflowName,
                Description = "TDC Procurement Officer submission with independent Head of Procurement approval.",
                EntityTypeId = entityType.Id,
                Version = 1,
                CreatedAt = now,
                CreatedBy = FixtureActor,
                CreatedById = procurementOfficer.Id
            };
            db.WorkflowDefinitions.Add(definition);
        }
        else
        {
            // The guarded disposable fixture must exercise one deterministic route.
            // Rebuild only this fixture's standard definition before it has instances.
            db.WorkflowTransitions.RemoveRange(definition.Transitions);
            db.WorkflowSteps.RemoveRange(definition.Steps);
            definition.Transitions.Clear();
            definition.Steps.Clear();
        }

        var submittedId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        definition.Steps.Add(CreateTenderWorkflowStep(
            submittedId, definition.Id, tenantId, "Submitted", 1,
            isStart: true, isEnd: false, WorkflowStepType.Manual,
            initiatorRole, now, procurementOfficer.Id));
        definition.Steps.Add(CreateTenderWorkflowStep(
            approvalId, definition.Id, tenantId, "Approval", 2,
            isStart: false, isEnd: false, WorkflowStepType.Approval,
            approverRole, now, procurementOfficer.Id));
        definition.Steps.Add(CreateTenderWorkflowStep(
            completedId, definition.Id, tenantId, "Completed", 3,
            isStart: false, isEnd: true, WorkflowStepType.Automatic,
            requiredRole: null, now, procurementOfficer.Id));
        definition.Transitions.Add(CreateTenderWorkflowTransition(
            definition.Id, tenantId, submittedId, approvalId,
            "Submit for approval", now, procurementOfficer.Id));
        definition.Transitions.Add(CreateTenderWorkflowTransition(
            definition.Id, tenantId, approvalId, completedId,
            "Approve", now, procurementOfficer.Id));

        definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
        definition.IsActive = true;
        definition.IsDeleted = false;
        definition.PublishedAt = now;
        definition.PublishedById = headOfProcurement.Id;
        definition.ChangeSummary =
            "Published fixture baseline: Procurement Officer submits and Head of Procurement approves independently.";
        definition.Configuration = JsonSerializer.Serialize(new
        {
            templateCode = "TDC_TENDER_APPROVAL",
            source = "guarded-tender-browser-acceptance",
            fixtureOnly = true
        });
        definition.UpdatedAt = now;
        definition.UpdatedBy = FixtureActor;
        definition.LastModifiedById = headOfProcurement.Id;

        var competingDefinitions = await db.WorkflowDefinitions.IgnoreQueryFilters()
            .Where(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                value.EntityTypeId == entityType.Id &&
                value.Id != definition.Id &&
                value.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var competingDefinition in competingDefinitions)
        {
            competingDefinition.IsActive = false;
            competingDefinition.UpdatedAt = now;
            competingDefinition.UpdatedBy = FixtureActor;
            competingDefinition.LastModifiedById = headOfProcurement.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDedicatedApprovalWorkflowAsync(
        Guid tenantId,
        string workflowName,
        string entityCode,
        string entityName,
        string initiatorRole,
        string approverRole,
        ApplicationUser fixtureCreator,
        ApplicationUser publisher,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var entityType = await db.WorkflowEntityTypes.IgnoreQueryFilters()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                (value.Code == entityCode || value.Name == entityName),
                cancellationToken);
        if (entityType is null)
        {
            entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = entityCode,
                Name = entityName,
                Description = $"Fixture-only {entityName} workflow entity.",
                IsActive = true,
                DisplayOrder = 501,
                CreatedAt = now,
                CreatedBy = FixtureActor,
                CreatedById = fixtureCreator.Id
            };
            db.WorkflowEntityTypes.Add(entityType);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            entityType.IsActive = true;
        }

        var definition = await db.WorkflowDefinitions.IgnoreQueryFilters()
            .Include(value => value.Steps)
            .Include(value => value.Transitions)
            .Where(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                value.EntityTypeId == entityType.Id &&
                value.Name == workflowName)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (definition is null)
        {
            definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = workflowName,
                Description = $"Fixture-only independent approval for {entityName}.",
                EntityTypeId = entityType.Id,
                Version = 1,
                CreatedAt = now,
                CreatedBy = FixtureActor,
                CreatedById = fixtureCreator.Id
            };
            db.WorkflowDefinitions.Add(definition);
        }
        else
        {
            var reusablePublishedFixture = definition.IsActive &&
                definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                definition.Steps.Count == 3 &&
                definition.Transitions.Count == 2 &&
                definition.Steps.Any(value => value.Name == "Submitted" &&
                    value.StepType == WorkflowStepType.Manual && value.RequiredRole == initiatorRole) &&
                definition.Steps.Any(value => value.Name == "Approval" &&
                    value.StepType == WorkflowStepType.Approval && value.RequiredRole == approverRole) &&
                definition.Steps.Any(value => value.Name == "Completed" &&
                    value.StepType == WorkflowStepType.Automatic && value.IsEndStep);
            if (reusablePublishedFixture)
                return;

            db.WorkflowTransitions.RemoveRange(definition.Transitions);
            db.WorkflowSteps.RemoveRange(definition.Steps);
            definition.Transitions.Clear();
            definition.Steps.Clear();
        }

        var submittedId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        definition.Steps.Add(CreateTenderWorkflowStep(
            submittedId, definition.Id, tenantId, "Submitted", 1,
            isStart: true, isEnd: false, WorkflowStepType.Manual,
            initiatorRole, now, fixtureCreator.Id));
        definition.Steps.Add(CreateTenderWorkflowStep(
            approvalId, definition.Id, tenantId, "Approval", 2,
            isStart: false, isEnd: false, WorkflowStepType.Approval,
            approverRole, now, fixtureCreator.Id));
        definition.Steps.Add(CreateTenderWorkflowStep(
            completedId, definition.Id, tenantId, "Completed", 3,
            isStart: false, isEnd: true, WorkflowStepType.Automatic,
            requiredRole: null, now, fixtureCreator.Id));
        definition.Transitions.Add(CreateTenderWorkflowTransition(
            definition.Id, tenantId, submittedId, approvalId,
            "Submit for approval", now, fixtureCreator.Id));
        definition.Transitions.Add(CreateTenderWorkflowTransition(
            definition.Id, tenantId, approvalId, completedId,
            "Approve", now, fixtureCreator.Id));
        definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
        definition.IsActive = true;
        definition.IsDeleted = false;
        definition.PublishedAt = now;
        definition.PublishedById = publisher.Id;
        definition.ChangeSummary = "Published fixture baseline for a guarded disposable tender lifecycle.";
        definition.Configuration = JsonSerializer.Serialize(new
        {
            source = "guarded-tender-browser-acceptance",
            fixtureOnly = true,
            entityType = entityName
        });
        definition.UpdatedAt = now;
        definition.UpdatedBy = FixtureActor;
        definition.LastModifiedById = publisher.Id;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureExclusiveActiveWorkflowAsync(
        Guid tenantId,
        string entityCode,
        string retainedWorkflowName,
        ApplicationUser actor,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var definitions = await db.WorkflowDefinitions.IgnoreQueryFilters()
            .Include(value => value.EntityType)
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.EntityType.TenantId == tenantId && !value.EntityType.IsDeleted &&
                value.EntityType.Code == entityCode)
            .ToListAsync(cancellationToken);
        var retained = definitions.Single(value => value.Name == retainedWorkflowName);
        foreach (var definition in definitions)
        {
            definition.IsActive = definition.Id == retained.Id;
            definition.UpdatedAt = now;
            definition.UpdatedBy = FixtureActor;
            definition.LastModifiedById = actor.Id;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static WorkflowStep CreateTenderWorkflowStep(
        Guid id,
        Guid definitionId,
        Guid tenantId,
        string name,
        int order,
        bool isStart,
        bool isEnd,
        WorkflowStepType stepType,
        string? requiredRole,
        DateTime now,
        Guid actorUserId) => new()
    {
        Id = id,
        TenantId = tenantId,
        WorkflowDefinitionId = definitionId,
        Name = name,
        StepType = stepType,
        Order = order,
        IsStartStep = isStart,
        IsEndStep = isEnd,
        IsRequired = true,
        RequiredRole = requiredRole,
        AssignmentType = requiredRole is null ? "System" : "Role",
        AssignmentConfiguration = requiredRole is null
            ? null
            : JsonSerializer.Serialize(new { role = requiredRole }),
        Configuration = stepType == WorkflowStepType.Approval && requiredRole is not null
            ? JsonSerializer.Serialize(new WorkflowStepConfigurationDto
            {
                ApprovalConfig = new WorkflowApprovalConfigDto
                {
                    ApprovalType = WorkflowApprovalType.Single,
                    ActivationMode = WorkflowApprovalActivationMode.Parallel,
                    MinApprovalsRequired = 1,
                    PreventInitiatorApproval = true,
                    RequireDistinctApprovers = true,
                    RejectionHandling = WorkflowRejectionHandling.StopWorkflow,
                    ApproverRules =
                    [
                        new WorkflowAssignmentRuleDto
                        {
                            ApprovalGroup = 1,
                            AssignmentType = WorkflowAssignmentType.Role,
                            Role = requiredRole,
                            Priority = 100
                        }
                    ]
                }
            })
            : null,
        CreatedAt = now,
        CreatedBy = FixtureActor,
        CreatedById = actorUserId
    };

    private static WorkflowTransition CreateTenderWorkflowTransition(
        Guid definitionId,
        Guid tenantId,
        Guid fromStepId,
        Guid toStepId,
        string name,
        DateTime now,
        Guid actorUserId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        WorkflowDefinitionId = definitionId,
        FromStepId = fromStepId,
        ToStepId = toStepId,
        Name = name,
        IsDefault = true,
        Priority = 1,
        CreatedAt = now,
        CreatedBy = FixtureActor,
        CreatedById = actorUserId
    };

    private async Task EnsureSourcingPolicyAsync(
        Guid tenantId,
        string runId,
        ApplicationUser officer,
        CancellationToken cancellationToken)
    {
        var shortId = ShortHash(runId);
        var policyCode = $"TE2E-{shortId}";
        if (await db.ProcurementPolicySets.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.Code == policyCode,
                cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var tenderAwardWorkflow = await db.WorkflowDefinitions.IgnoreQueryFilters()
            .Include(value => value.EntityType)
            .SingleAsync(value =>
                    value.TenantId == tenantId &&
                    !value.IsDeleted &&
                    value.IsActive &&
                    value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                    value.Name == "TDC Tender E2E Award Approval",
                cancellationToken);
        var requisitionAuthorityWorkflow = await db.WorkflowDefinitions.IgnoreQueryFilters()
            .Include(value => value.EntityType)
            .SingleAsync(value =>
                    value.TenantId == tenantId &&
                    !value.IsDeleted &&
                    value.IsActive &&
                    value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                    value.Name == "TDC Tender E2E PR Authority",
                cancellationToken);
        var authorityRole = await db.Roles.SingleAsync(
            value => value.Name == "TDC_HEAD_OF_PROCUREMENT",
            cancellationToken);
        var profile = new ProcurementConfigurationProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProfileKey = Guid.NewGuid(),
            ProfileCode = policyCode,
            Name = "Tender E2E sourcing profile",
            Version = 1,
            LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
            EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddYears(1),
            ChangeSummary = "Fixture-only published profile in a disposable Tender browser database.",
            IsDefault = true,
            PublishedAt = now.AddDays(-1),
            PublishedById = officer.Id,
            CreatedAt = now,
            CreatedBy = FixtureActor
        };
        var policy = new ProcurementPolicySet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PolicyKey = Guid.NewGuid(),
            Code = policyCode,
            Name = "Tender E2E Goods sourcing policy",
            Description = "Fixture-only Goods threshold and method policy for real API sourcing transitions.",
            Version = 1,
            LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
            ScopeType = ProcurementPolicyScopeType.TenantBaseline,
            SourceConfigurationProfileId = profile.Id,
            SourceConfigurationProfile = profile,
            DefaultCurrencyCode = "GHS",
            EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddYears(1),
            ChangeSummary = "Disposable browser acceptance only; never a production statutory policy.",
            IsDefault = true,
            PublishedAt = now.AddDays(-1),
            PublishedById = officer.Id,
            CreatedAt = now,
            CreatedBy = FixtureActor
        };
        policy.CategoryRules.Add(new ProcurementPolicyCategoryRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleCode = $"{policyCode}-GOODS",
            Name = "Tender E2E Goods coverage",
            Category = ProcurementCategoryClass.Goods,
            Description = "Fixture-only Goods category coverage.",
            RequiresSpecification = true,
            OverrideAction = ProcurementPolicyOverrideAction.Add,
            SourceDecisionKey = "DEC-001",
            Priority = 100,
            IsEnabled = true,
            EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddYears(1),
            CreatedAt = now,
            CreatedBy = FixtureActor
        });
        policy.MethodRules.Add(new ProcurementPolicyMethodRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleCode = $"{policyCode}-RFQ",
            Name = "Tender E2E request for quotation handoff",
            Category = ProcurementCategoryClass.Goods,
            Method = ProcurementMethodType.RequestForQuotation,
            IsAllowed = true,
            RequiresCompetition = true,
            JustificationRequired = false,
            MinimumQuotationCount = 1,
            WorkflowDefinitionId = tenderAwardWorkflow.Id,
            ApplicabilityConditions = "Fixture-only alternate PO handoff method.",
            OverrideAction = ProcurementPolicyOverrideAction.Add,
            SourceDecisionKey = "DEC-001",
            Priority = 110,
            IsEnabled = true,
            EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddYears(1),
            CreatedAt = now,
            CreatedBy = FixtureActor
        });
        policy.MethodRules.Add(new ProcurementPolicyMethodRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleCode = $"{policyCode}-NCT",
            Name = "Tender E2E national competitive tendering",
            Category = ProcurementCategoryClass.Goods,
            Method = ProcurementMethodType.NationalCompetitiveTendering,
            IsAllowed = true,
            RequiresCompetition = true,
            JustificationRequired = false,
            MinimumQuotationCount = 2,
            WorkflowDefinitionId = tenderAwardWorkflow.Id,
            ApplicabilityConditions = "Fixture-only automatic method for disposable browser acceptance.",
            OverrideAction = ProcurementPolicyOverrideAction.Add,
            SourceDecisionKey = "DEC-001",
            Priority = 100,
            IsEnabled = true,
            EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddYears(1),
            CreatedAt = now,
            CreatedBy = FixtureActor
        });
        policy.ThresholdRules.Add(new ProcurementPolicyThresholdRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleCode = $"{policyCode}-GHS-RFQ-BAND",
            Name = "Tender E2E RFQ handoff value band",
            Category = ProcurementCategoryClass.Goods,
            Method = ProcurementMethodType.RequestForQuotation,
            CurrencyCode = "GHS",
            LowerBound = 0m,
            UpperBound = 5_000m,
            LowerInclusive = true,
            UpperInclusive = true,
            StatutoryReference = "Fixture-only alternate PO handoff threshold.",
            OverrideAction = ProcurementPolicyOverrideAction.Add,
            SourceDecisionKey = "DEC-001",
            Priority = 110,
            IsEnabled = true,
            EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddYears(1),
            CreatedAt = now,
            CreatedBy = FixtureActor
        });
        policy.ThresholdRules.Add(new ProcurementPolicyThresholdRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleCode = $"{policyCode}-GHS-BAND",
            Name = "Tender E2E approved Goods value band",
            Category = ProcurementCategoryClass.Goods,
            Method = ProcurementMethodType.NationalCompetitiveTendering,
            CurrencyCode = "GHS",
            LowerBound = 5_000m,
            UpperBound = 100_000m,
            LowerInclusive = false,
            UpperInclusive = true,
            StatutoryReference = "Fixture-only lifecycle acceptance threshold; not a production statutory value.",
            OverrideAction = ProcurementPolicyOverrideAction.Add,
            SourceDecisionKey = "DEC-001",
            Priority = 100,
            IsEnabled = true,
            EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddYears(1),
            CreatedAt = now,
            CreatedBy = FixtureActor
        });
        policy.AuthorityRules.Add(new ProcurementPolicyAuthorityRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleCode = $"{policyCode}-NCT-AUTHORITY",
            AuthorityName = "Head of Procurement",
            AuthorityRoleId = authorityRole.Id,
            AuthorityRole = authorityRole.Name!,
            Category = ProcurementCategoryClass.Goods,
            CurrencyCode = "GHS",
            LowerBound = 0m,
            UpperBound = 100_000m,
            LowerInclusive = true,
            UpperInclusive = true,
            Sequence = 1,
            Quorum = 1,
            IsObserver = false,
            WorkflowDefinitionId = requisitionAuthorityWorkflow.Id,
            OverrideAction = ProcurementPolicyOverrideAction.Add,
            SourceDecisionKey = "DEC-002",
            Priority = 100,
            IsEnabled = true,
            EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddYears(1),
            CreatedAt = now,
            CreatedBy = FixtureActor
        });

        db.ProcurementPolicySets.Add(policy);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ApplicationUser> RequiredUserAsync(
        string username,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await db.Users.IgnoreQueryFilters().SingleAsync(
            value => value.TenantId == tenantId && value.UserName == username && value.IsActive,
            cancellationToken);
    }

    private async Task<PartnerCategory> EnsureGoodsCategoryAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var category = await db.PartnerCategories.IgnoreQueryFilters()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                value.CategoryCode == "TENDER-E2E-GOODS",
                cancellationToken);
        if (category is not null)
            return category;

        category = new PartnerCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryCode = "TENDER-E2E-GOODS",
            CategoryName = "Goods - Tender browser acceptance",
            CategoryType = "Supplier",
            Description = "Fixture-only Goods category in a disposable Tender browser database.",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = FixtureActor
        };
        db.PartnerCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category;
    }

    private async Task EnsureSupplierAsync(
        Guid tenantId,
        string runId,
        string supplierLabel,
        ApplicationUser supplierUser,
        ApplicationUser approver,
        PartnerCategory goodsCategory,
        bool suspended,
        CancellationToken cancellationToken)
    {
        var marker = $"TENDER-E2E:{runId}:SUPPLIER-{supplierLabel}";
        var partner = await db.BusinessPartners.IgnoreQueryFilters()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.Notes == marker,
                cancellationToken);

        if (partner is null)
        {
            // Keep the disposable actor's supplier association unambiguous without
            // deleting any reference seed record that may already use this test user.
            var priorPrimaryLinks = await db.BusinessPartners.IgnoreQueryFilters()
                .Where(value => value.TenantId == tenantId &&
                    !value.IsDeleted && value.UserId == supplierUser.Id)
                .ToListAsync(cancellationToken);
            foreach (var prior in priorPrimaryLinks)
                prior.UserId = null;

            var priorMemberships = await db.BusinessPartnerUsers.IgnoreQueryFilters()
                .Where(value => value.TenantId == tenantId &&
                    !value.IsDeleted && value.UserId == supplierUser.Id && value.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var prior in priorMemberships)
                prior.IsActive = false;

            partner = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartnerCode = $"TE2E-{ShortHash(runId)}-{supplierLabel}",
                PartnerName = $"Tender E2E Supplier {supplierLabel}",
                LegalName = $"Tender E2E Supplier {supplierLabel} Limited",
                PartnerType = "Supplier",
                BusinessRegistrationNumber = $"BR-{ShortHash(runId)}-{supplierLabel}",
                TaxIdentificationNumber = $"TIN-{ShortHash(runId)}-{supplierLabel}",
                PrimaryContactName = $"Tender Supplier {supplierLabel}",
                PrimaryEmail = supplierUser.Email,
                Currency = "GHS",
                IndustryClassification = "Goods",
                BeneficialOwnershipJson = "[]",
                OwnershipVerifiedAtUtc = DateTime.UtcNow.AddDays(-1),
                ComplianceStatus = suspended ? "Suspended" : "Compliant",
                ComplianceReviewDateUtc = DateTime.UtcNow.AddDays(-1),
                ComplianceValidUntilUtc = DateTime.UtcNow.AddYears(1),
                ComplianceNotes = "Fixture-only eligibility evidence for disposable browser acceptance.",
                RegistrationStatus = suspended ? "Suspended" : "Approved",
                ApprovalStatus = "Approved",
                ApprovedById = approver.Id,
                ApprovedDate = DateTime.UtcNow.AddDays(-1),
                PerformanceRating = suspended ? 1m : 4.5m,
                RiskLevel = suspended ? "High" : "Low",
                CreditRating = suspended ? "D" : "A",
                IsActive = !suspended,
                IsBlacklisted = false,
                UserId = supplierUser.Id,
                Notes = marker,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = FixtureActor
            };
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            partner.UserId = supplierUser.Id;
            partner.RegistrationStatus = suspended ? "Suspended" : "Approved";
            partner.ApprovalStatus = "Approved";
            partner.IsActive = !suspended;
            partner.IsBlacklisted = false;
            partner.ComplianceStatus = suspended ? "Suspended" : "Compliant";
            partner.ComplianceValidUntilUtc = DateTime.UtcNow.AddYears(1);
        }

        var membership = await db.BusinessPartnerUsers.IgnoreQueryFilters()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted &&
                value.BusinessPartnerId == partner.Id && value.UserId == supplierUser.Id,
                cancellationToken);
        if (membership is null)
        {
            db.BusinessPartnerUsers.Add(new BusinessPartnerUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BusinessPartnerId = partner.Id,
                UserId = supplierUser.Id,
                Role = "Admin",
                IsActive = !suspended,
                GrantedAt = DateTime.UtcNow,
                GrantedById = approver.Id,
                Notes = marker,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = FixtureActor
            });
        }
        else
        {
            membership.IsActive = !suspended;
        }

        if (!await db.BusinessPartnerCategories.AnyAsync(value =>
                value.BusinessPartnerId == partner.Id && value.CategoryId == goodsCategory.Id,
                cancellationToken))
        {
            db.BusinessPartnerCategories.Add(new BusinessPartnerCategory
            {
                Id = Guid.NewGuid(),
                BusinessPartnerId = partner.Id,
                CategoryId = goodsCategory.Id,
                IsPrimary = true
            });
        }

        var taxEvidence = await db.BusinessPartnerDocuments.IgnoreQueryFilters()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted &&
                value.BusinessPartnerId == partner.Id &&
                value.DocumentType == "TAX-CLEARANCE",
                cancellationToken);
        if (taxEvidence is null)
        {
            db.BusinessPartnerDocuments.Add(new BusinessPartnerDocument
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BusinessPartnerId = partner.Id,
                DocumentType = "TAX-CLEARANCE",
                DocumentName = $"Tender E2E tax clearance {supplierLabel}",
                DocumentPath = $"fixture://tender-e2e/{runId}/supplier-{supplierLabel}/tax-clearance",
                MimeType = "application/pdf",
                IssueDate = DateTime.UtcNow.AddDays(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                IsVerified = !suspended,
                VerifiedById = approver.Id,
                VerifiedDate = DateTime.UtcNow.AddDays(-1),
                VerificationNotes = "Fixture-only verified evidence in a disposable database.",
                UploadedById = supplierUser.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = FixtureActor
            });
        }
        else
        {
            taxEvidence.ExpiryDate = DateTime.UtcNow.AddYears(1);
            taxEvidence.IsVerified = !suspended;
            taxEvidence.VerifiedById = approver.Id;
            taxEvidence.VerifiedDate = DateTime.UtcNow.AddDays(-1);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureApprovedSourceAsync(
        Guid tenantId,
        string runId,
        string scenario,
        ApplicationUser requester,
        ApplicationUser approver,
        Department department,
        CancellationToken cancellationToken)
    {
        var marker = $"TENDER-E2E:{runId}:{scenario}";
        var existing = await db.PurchaseRequisitions.IgnoreQueryFilters().SingleOrDefaultAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.Notes == marker,
            cancellationToken);
        if (existing is not null)
        {
            await EnsureApprovedSourceAuthorityRouteAsync(
                tenantId, runId, existing, requester, approver, cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        var shortId = ShortHash($"{runId}:{scenario}");
        var alternateHandoff = scenario == "ALTERNATE-HANDOFF";
        var firstUnitPrice = alternateHandoff ? 2_000m : 10_000m;
        var secondUnitPrice = alternateHandoff ? 500m : 2_500m;
        var requisitionTotal = firstUnitPrice * 2m + secondUnitPrice * 4m;
        var budget = new ProcurementBudget
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BudgetCode = $"TE2E-{shortId}",
            Title = $"Tender E2E {scenario} budget",
            Description = "Approved effective GHS budget for a disposable Tender browser fixture.",
            DepartmentId = department.Id,
            FiscalYear = now.Year,
            AllocatedAmount = 100_000m,
            UtilizedAmount = 0m,
            CommittedAmount = 0m,
            ReservedAmount = 0m,
            RemainingAmount = 100_000m,
            Currency = "GHS",
            Status = "Approved",
            ControlLevel = "Strict",
            WarningThresholdPercent = 80m,
            EffectiveDate = now.AddDays(-1),
            ExpiryDate = now.AddYears(1),
            ApprovedById = approver.Id,
            ApprovedDate = now.AddDays(-1),
            Notes = marker,
            CreatedAt = now,
            CreatedBy = FixtureActor
        };
        db.ProcurementBudgets.Add(budget);

        var requisition = new PurchaseRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequisitionNumber = $"PR-TE2E-{shortId}",
            RequisitionDate = now.AddDays(-2),
            RequestedById = requester.Id,
            RequiredDate = now.AddDays(30),
            Status = "Approved",
            Priority = "Normal",
            Department = department.Name,
            CostCenter = department.Code,
            Justification = "Two-line approved Goods demand for real Tender lifecycle browser acceptance.",
            Notes = marker,
            RequisitionType = PurchaseRequisitionType.StockReplenishment,
            BudgetId = budget.Id,
            BudgetCode = budget.BudgetCode,
            BudgetAllocated = budget.AllocatedAmount,
            BudgetRemaining = budget.RemainingAmount,
            BudgetValidated = true,
            ProcurementCategory = ProcurementCategoryClass.Goods,
            ApprovalLevel = 1,
            RequiredApprovalLevel = 1,
            Currency = "GHS",
            ApprovedById = approver.Id,
            ApprovedAt = now.AddDays(-1),
            TotalAmount = requisitionTotal,
            CreatedAt = now,
            CreatedBy = FixtureActor,
            Items =
            {
                new PurchaseRequisitionItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemDescription = $"{scenario} business laptop",
                    Quantity = 2m,
                    UnitOfMeasure = "EA",
                    EstimatedUnitPrice = firstUnitPrice,
                    LineTotal = firstUnitPrice * 2m,
                    RequiredDate = now.AddDays(30),
                    Specifications = "Business-class laptop, minimum 16 GB RAM and three-year warranty.",
                    Notes = marker,
                    Status = "Pending",
                    CreatedAt = now,
                    CreatedBy = FixtureActor
                },
                new PurchaseRequisitionItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemDescription = $"{scenario} external monitor",
                    Quantity = 4m,
                    UnitOfMeasure = "EA",
                    EstimatedUnitPrice = secondUnitPrice,
                    LineTotal = secondUnitPrice * 4m,
                    RequiredDate = now.AddDays(30),
                    Specifications = "Minimum 24-inch business display with HDMI and adjustable stand.",
                    Notes = marker,
                    Status = "Pending",
                    CreatedAt = now,
                    CreatedBy = FixtureActor
                }
            }
        };
        db.PurchaseRequisitions.Add(requisition);
        await db.SaveChangesAsync(cancellationToken);
        await EnsureApprovedSourceAuthorityRouteAsync(
            tenantId, runId, requisition, requester, approver, cancellationToken);
    }

    private async Task EnsureApprovedSourceAuthorityRouteAsync(
        Guid tenantId,
        string runId,
        PurchaseRequisition requisition,
        ApplicationUser requester,
        ApplicationUser approver,
        CancellationToken cancellationToken)
    {
        if (await db.ProcurementRequisitionAuthorityRoutes.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && value.PurchaseRequisitionId == requisition.Id &&
                !value.IsDeleted,
                cancellationToken))
        {
            return;
        }

        var policyCode = $"TE2E-{ShortHash(runId)}";
        var policy = await db.ProcurementPolicySets.IgnoreQueryFilters()
            .Include(value => value.AuthorityRules)
            .SingleAsync(value =>
                    value.TenantId == tenantId && !value.IsDeleted &&
                    value.Code == policyCode &&
                    value.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published,
                cancellationToken);
        var rule = policy.AuthorityRules.Single(value =>
            !value.IsDeleted && value.IsEnabled &&
            value.Category == ProcurementCategoryClass.Goods &&
            value.CurrencyCode == "GHS" &&
            value.LowerBound <= requisition.TotalAmount &&
            (!value.UpperBound.HasValue || value.UpperBound.Value >= requisition.TotalAmount));
        var workflow = await db.WorkflowDefinitions.IgnoreQueryFilters()
            .Include(value => value.EntityType)
            .Include(value => value.Steps)
            .SingleAsync(value =>
                    value.Id == rule.WorkflowDefinitionId && value.TenantId == tenantId &&
                    !value.IsDeleted && value.IsActive &&
                    value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published,
                cancellationToken);
        var approvalStep = workflow.Steps.Single(value =>
            !value.IsDeleted && value.StepType == WorkflowStepType.Approval);
        var capturedAt = DateTime.UtcNow;
        var routeId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        var correlationId = $"tender-e2e-authority-{ShortHash($"{runId}:{requisition.Id:N}")}";
        var capturedByName = string.Join(' ', new[] { approver.FirstName, approver.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(capturedByName))
            capturedByName = approver.UserName ?? "Tender E2E PR Approver";

        var stepSnapshot = new
        {
            sequence = 1,
            ruleId = rule.Id,
            rulePolicySetId = policy.Id,
            rulePolicyCode = policy.Code,
            rulePolicyVersion = policy.Version,
            ruleCode = rule.RuleCode,
            sourceDecisionKey = rule.SourceDecisionKey,
            sourceRuleId = (Guid?)rule.Id,
            authorityName = rule.AuthorityName,
            authorityRole = rule.AuthorityRole,
            currencyCode = rule.CurrencyCode,
            lowerBound = rule.LowerBound,
            upperBound = rule.UpperBound,
            lowerInclusive = rule.LowerInclusive,
            upperInclusive = rule.UpperInclusive,
            quorum = rule.Quorum,
            isObserver = rule.IsObserver,
            escalationAuthority = rule.EscalationAuthority,
            workflowDefinitionId = workflow.Id,
            workflowStepId = approvalStep.Id,
            workflowStepName = approvalStep.Name,
            workflowStepOrder = approvalStep.Order
        };
        var snapshot = new
        {
            schemaVersion = "tdc.pr-authority-route.v1",
            routeId,
            purchaseRequisitionId = requisition.Id,
            requisition.RequisitionNumber,
            attemptNumber = 1,
            evaluationId,
            policyDateUtc = requisition.RequisitionDate,
            evaluatedAtUtc = capturedAt,
            category = requisition.ProcurementCategory,
            amount = requisition.TotalAmount,
            currencyCode = requisition.Currency,
            policy = new
            {
                policySetId = policy.Id,
                policy.PolicyKey,
                policy.Code,
                policy.Name,
                policy.Version,
                policy.ScopeType,
                policy.SourceConfigurationProfileId,
                policy.BasePolicySetId
            },
            workflow = new
            {
                workflowDefinitionId = workflow.Id,
                workflow.DefinitionKey,
                workflow.Name,
                workflow.Version,
                entityTypeCode = workflow.EntityType.Code
            },
            steps = new[] { stepSnapshot },
            capturedAtUtc = capturedAt,
            capturedById = approver.Id,
            capturedByName,
            correlationId
        };
        var snapshotJson = JsonSerializer.Serialize(snapshot);
        var route = new ProcurementRequisitionAuthorityRoute
        {
            Id = routeId,
            TenantId = tenantId,
            PurchaseRequisitionId = requisition.Id,
            AttemptNumber = 1,
            RouteReference = $"ARR-{requisition.RequisitionNumber}-A1",
            EvaluationId = evaluationId,
            CorrelationId = correlationId,
            PolicySetId = policy.Id,
            PolicyKey = policy.PolicyKey,
            PolicyCode = policy.Code,
            PolicyName = policy.Name,
            PolicyVersion = policy.Version,
            PolicyScopeType = policy.ScopeType,
            SourceConfigurationProfileId = policy.SourceConfigurationProfileId,
            BasePolicySetId = policy.BasePolicySetId,
            Category = requisition.ProcurementCategory!.Value,
            Amount = requisition.TotalAmount,
            CurrencyCode = requisition.Currency,
            PolicyDateUtc = requisition.RequisitionDate,
            EvaluatedAtUtc = capturedAt,
            WorkflowDefinitionId = workflow.Id,
            WorkflowDefinitionKey = workflow.DefinitionKey,
            WorkflowName = workflow.Name,
            WorkflowVersion = workflow.Version,
            WorkflowEntityTypeCode = workflow.EntityType.Code,
            CapturedAtUtc = capturedAt,
            CapturedById = approver.Id,
            CapturedByName = capturedByName,
            SnapshotJson = snapshotJson,
            IntegrityHash = Hash64(snapshotJson),
            CreatedAt = capturedAt,
            CreatedBy = FixtureActor,
            CreatedById = approver.Id
        };
        route.Steps.Add(new ProcurementRequisitionAuthorityRouteStep
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AuthorityRouteId = route.Id,
            Sequence = 1,
            AuthorityRuleId = rule.Id,
            RulePolicySetId = policy.Id,
            RulePolicyCode = policy.Code,
            RulePolicyVersion = policy.Version,
            RuleCode = rule.RuleCode,
            SourceDecisionKey = rule.SourceDecisionKey,
            SourceRuleId = rule.Id,
            AuthorityName = rule.AuthorityName,
            AuthorityRole = rule.AuthorityRole,
            CurrencyCode = rule.CurrencyCode,
            LowerBound = rule.LowerBound,
            UpperBound = rule.UpperBound,
            LowerInclusive = rule.LowerInclusive,
            UpperInclusive = rule.UpperInclusive,
            Quorum = rule.Quorum,
            IsObserver = rule.IsObserver,
            EscalationAuthority = rule.EscalationAuthority,
            WorkflowDefinitionId = workflow.Id,
            WorkflowStepId = approvalStep.Id,
            WorkflowStepName = approvalStep.Name,
            WorkflowStepOrder = approvalStep.Order,
            CreatedAt = capturedAt,
            CreatedBy = FixtureActor,
            CreatedById = approver.Id
        });

        var workflowInstance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowDefinitionId = workflow.Id,
            EntityId = requisition.Id,
            EntityTypeId = workflow.EntityTypeId,
            Status = WorkflowInstanceStatus.Completed,
            Priority = WorkflowPriority.Normal,
            InitiatedById = requester.Id,
            StartedById = requester.Id,
            CreatedDate = requisition.RequisitionDate,
            StartedDate = requisition.RequisitionDate,
            CompletedDate = requisition.ApprovedAt ?? capturedAt,
            DataContext = JsonSerializer.Serialize(new
            {
                source = "guarded-tender-browser-acceptance",
                requisitionId = requisition.Id,
                approvedById = approver.Id
            }),
            Notes = "Fixture-only completed PR authority workflow in a disposable SQL acceptance database.",
            CreatedAt = requisition.RequisitionDate,
            CreatedBy = FixtureActor,
            CreatedById = requester.Id
        };
        foreach (var definitionStep in workflow.Steps.OrderBy(value => value.Order))
        {
            var stepInstance = new WorkflowStepInstance
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowInstanceId = workflowInstance.Id,
                WorkflowStepId = definitionStep.Id,
                Status = WorkflowStepInstanceStatus.Completed,
                AssignedToId = definitionStep.StepType == WorkflowStepType.Approval
                    ? approver.Id
                    : requester.Id,
                CreatedDate = requisition.RequisitionDate,
                StartedDate = requisition.RequisitionDate,
                CompletedDate = requisition.ApprovedAt ?? capturedAt,
                Comments = "Completed fixture prerequisite for the guarded Tender lifecycle.",
                CreatedAt = requisition.RequisitionDate,
                CreatedBy = FixtureActor,
                CreatedById = requester.Id
            };
            if (definitionStep.StepType == WorkflowStepType.Approval)
            {
                stepInstance.Approvals.Add(new WorkflowApproval
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    StepInstanceId = stepInstance.Id,
                    ApproverId = approver.Id,
                    ApproverRole = rule.AuthorityRole,
                    ApprovalGroup = 1,
                    Status = WorkflowApprovalStatus.Approved,
                    RequestedDate = requisition.RequisitionDate,
                    ProcessedDate = requisition.ApprovedAt ?? capturedAt,
                    Comments = "Independent fixture authority approval for disposable lifecycle acceptance.",
                    ProcessedById = approver.Id,
                    Priority = WorkflowPriority.Normal,
                    CreatedAt = requisition.RequisitionDate,
                    CreatedBy = FixtureActor,
                    CreatedById = requester.Id
                });
            }

            workflowInstance.StepInstances.Add(stepInstance);
        }

        db.ProcurementRequisitionAuthorityRoutes.Add(route);
        db.WorkflowInstances.Add(workflowInstance);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureEvaluationTemplateAsync(
        Guid tenantId,
        string runId,
        ApplicationUser officer,
        CancellationToken cancellationToken)
    {
        var code = $"TE2E-{ShortHash(runId)}-ITB";
        if (await db.EvaluationTemplates.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.TemplateCode == code,
                cancellationToken))
        {
            return;
        }

        var technical = new EvaluationCriterion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CriterionName = "Tender E2E technical responsiveness",
            CriterionCode = $"{code}-TECH",
            Category = "Technical",
            EvaluationType = "Technical",
            Description = "Fixture-only technical criterion.",
            MaxScore = 100,
            Weight = 60m,
            IsActive = true,
            DisplayOrder = 1,
            CreatedById = officer.Id,
            CreatedAt = DateTime.UtcNow
        };
        var commercial = new EvaluationCriterion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CriterionName = "Tender E2E commercial responsiveness",
            CriterionCode = $"{code}-COMM",
            Category = "Financial",
            EvaluationType = "Financial",
            Description = "Fixture-only commercial criterion.",
            MaxScore = 100,
            Weight = 40m,
            IsActive = true,
            DisplayOrder = 2,
            CreatedById = officer.Id,
            CreatedAt = DateTime.UtcNow
        };
        var template = new EvaluationTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateName = $"Tender E2E Goods ITB {ShortHash(runId)}",
            TemplateCode = code,
            Description = "Fixture-only active Goods/ITB evaluation template.",
            Category = "Goods",
            TenderType = "ITB",
            IsDefault = true,
            IsActive = true,
            PassingScore = 70m,
            ScoringMethod = "WeightedAverage",
            DisplayOrder = 1,
            CreatedById = officer.Id,
            CreatedAt = DateTime.UtcNow
        };
        template.TemplateCriteria.Add(new EvaluationTemplateCriterion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EvaluationCriterion = technical,
            Weight = 60m,
            MaxScore = 100,
            IsMandatory = true,
            MinimumScore = 50m,
            DisplayOrder = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = FixtureActor
        });
        template.TemplateCriteria.Add(new EvaluationTemplateCriterion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EvaluationCriterion = commercial,
            Weight = 40m,
            MaxScore = 100,
            IsMandatory = true,
            MinimumScore = 40m,
            DisplayOrder = 2,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = FixtureActor
        });
        db.EvaluationTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureFixtureAssistedScenariosAsync(
        Guid tenantId,
        string runId,
        ApplicationUser officer,
        CancellationToken cancellationToken)
    {
        var supplierA = await db.BusinessPartners.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            value.Notes == $"TENDER-E2E:{runId}:SUPPLIER-A", cancellationToken);
        var supplierB = await db.BusinessPartners.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            value.Notes == $"TENDER-E2E:{runId}:SUPPLIER-B", cancellationToken);
        var template = await db.EvaluationTemplates.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            value.TemplateCode == $"TE2E-{ShortHash(runId)}-ITB", cancellationToken);
        var policy = await db.ProcurementPolicySets.IgnoreQueryFilters()
            .Include(value => value.SourceConfigurationProfile)
            .Include(value => value.MethodRules)
            .SingleAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.Code == $"TE2E-{ShortHash(runId)}", cancellationToken);
        var methodRule = policy.MethodRules.Single(value =>
            value.IsAllowed && value.IsEnabled &&
            value.Method == ProcurementMethodType.NationalCompetitiveTendering);
        var evaluatorA = await RequiredUserAsync("helpdesk.agent", tenantId, cancellationToken);
        var evaluatorB = await RequiredUserAsync("helpdesk.supervisor", tenantId, cancellationToken);
        var evaluatorC = await RequiredUserAsync("helpdesk.manager", tenantId, cancellationToken);
        var (receivingAccount, revenueAccount) = await EnsureTenderFeeAccountsAsync(
            tenantId, runId, cancellationToken);
        var committee = await EnsureEvaluationCommitteeTemplateAsync(
            tenantId, runId, [evaluatorA, evaluatorB, evaluatorC], cancellationToken);
        await EnsureTenderDocumentTemplateAsync(
            tenantId, runId, policy, methodRule, cancellationToken);

        await EnsureTenderScenarioAsync(
            tenantId, runId, "SUPPLIER-LIFECYCLE", "Approved", supplierA, supplierB,
            template, officer, evaluatorA, receivingAccount, revenueAccount,
            policy, methodRule, committee, CommitteeFixtureState.None, cancellationToken);
        await EnsureTenderScenarioAsync(
            tenantId, runId, "COMMITTEE-ABSENT", "Closed", supplierA, supplierB,
            template, officer, evaluatorA, receivingAccount, revenueAccount,
            policy, methodRule, committee, CommitteeFixtureState.None, cancellationToken);
        await EnsureTenderScenarioAsync(
            tenantId, runId, "COMMITTEE-DRAFT", "Closed", supplierA, supplierB,
            template, officer, evaluatorA, receivingAccount, revenueAccount,
            policy, methodRule, committee, CommitteeFixtureState.Draft, cancellationToken);
        await EnsureTenderScenarioAsync(
            tenantId, runId, "COMMITTEE-ACTIVE", "Closed", supplierA, supplierB,
            template, officer, evaluatorA, receivingAccount, revenueAccount,
            policy, methodRule, committee, CommitteeFixtureState.ActiveReady, cancellationToken);
    }

    private async Task EnsureAlternateApprovedAwardAsync(
        Guid tenantId,
        string runId,
        ApplicationUser officer,
        ApplicationUser recommendationMaker,
        CancellationToken cancellationToken)
    {
        var marker = $"TENDER-E2E:{runId}:ALTERNATE-HANDOFF";
        if (await db.Tenders.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.Notes == marker,
                cancellationToken))
            return;

        var source = await db.PurchaseRequisitions.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.Notes == marker,
            cancellationToken);
        var supplier = await db.BusinessPartners.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            value.Notes == $"TENDER-E2E:{runId}:SUPPLIER-A", cancellationToken);
        var template = await db.EvaluationTemplates.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            value.TemplateCode == $"TE2E-{ShortHash(runId)}-ITB", cancellationToken);
        var now = DateTime.UtcNow;
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            TenderNumber = $"TND-TE2E-{ShortHash(marker)}",
            Title = "Tender E2E independently approved alternate handoff",
            Description = "Fixture prerequisite whose conversion must execute through the real purchase-order boundary.",
            TenderType = "ITB", Status = "Evaluated", PublishDate = now.AddDays(-3),
            SubmissionDeadline = now.AddDays(-2), OpeningDate = now.AddDays(-2).AddHours(1),
            EstimatedValue = source.TotalAmount, Currency = "GHS",
            SourcePurchaseRequisitionId = source.Id,
            EvaluationTemplateId = template.Id, PriceWeightage = 40m, QualityWeightage = 40m,
            DeliveryWeightage = 10m, ExperienceWeightage = 10m,
            CreatedById = officer.Id, PublishedById = officer.Id,
            Notes = marker,
            TermsAndConditions = "Disposable alternate-handoff acceptance fixture only.",
            CreatedAt = now.AddDays(-3)
        };
        var lot = new TenderLot
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id,
            LotNumber = 1, LotCode = "LOT-01", Title = "Alternate handoff Goods lot",
            Description = "Exact approved demand for real award conversion.",
            EstimatedValue = source.TotalAmount, Currency = "GHS", Status = "Active",
            RequiredDeliveryDate = now.AddDays(30), DeliveryLocation = "Tender E2E acceptance location",
            DisplayOrder = 1, Notes = marker, CreatedAt = now.AddDays(-3), CreatedBy = FixtureActor
        };
        var item = new TenderItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id, LotId = lot.Id,
            LineNumber = 1, ItemCode = $"TE2E-{ShortHash("ALTERNATE-HANDOFF")}",
            Description = "ALTERNATE-HANDOFF approved Goods package", Quantity = 1m,
            UnitOfMeasure = "EA", Specifications = "Exact independently approved alternate award package.",
            RequiredDeliveryDate = now.AddDays(30), DeliveryLocation = lot.DeliveryLocation,
            CreatedAt = now.AddDays(-3), CreatedBy = FixtureActor
        };
        tender.Lots.Add(lot);
        tender.Items.Add(item);
        var evaluator = new TenderEvaluator
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id,
            UserId = recommendationMaker.Id, Role = "Evaluator",
            AssignedDate = now.AddDays(-2), AssignedById = officer.Id,
            Status = "Completed", AcceptedDate = now.AddDays(-2),
            CompletedDate = now.AddDays(-1), WeightagePercentage = 100m,
            Notes = marker, CreatedAt = now.AddDays(-2), CreatedBy = FixtureActor
        };
        tender.Evaluators.Add(evaluator);
        tender.Invitations.Add(NewInvitation(
            tenantId, tender.Id, supplier.Id, officer.Id, "Submitted", now.AddDays(-2)));
        db.Tenders.Add(tender);
        await db.SaveChangesAsync(cancellationToken);

        await AddFixtureBidAsync(
            tender, lot, item, supplier, officer, "Evaluated", source.TotalAmount, 1,
            cancellationToken);
        var bid = await db.TenderBids.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && value.TenderId == tender.Id && !value.IsDeleted,
            cancellationToken);
        var evaluation = new TenderEvaluation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderBidId = bid.Id,
            TenderEvaluatorId = evaluator.Id, EvaluationDate = now.AddDays(-1),
            Status = "Submitted", PriceScore = 95m, QualityScore = 95m,
            DeliveryScore = 95m, ExperienceScore = 95m, TechnicalScore = 95m,
            ComplianceScore = 100m, TotalScore = 95m, IsRecommended = true,
            Recommendation = "Recommend the responsive alternate handoff bid.",
            OverallComments = "Fixture prerequisite for real independent award approval.",
            SubmittedDate = now.AddDays(-1), CreatedAt = now.AddDays(-1),
            CreatedBy = FixtureActor, CreatedById = recommendationMaker.Id
        };
        db.TenderEvaluations.Add(evaluation);
        await db.SaveChangesAsync(cancellationToken);

        var policy = await db.ProcurementPolicySets.IgnoreQueryFilters()
            .Include(value => value.SourceConfigurationProfile)
            .Include(value => value.MethodRules)
            .SingleAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.Code == $"TE2E-{ShortHash(runId)}", cancellationToken);
        var methodRule = policy.MethodRules.Single(value =>
            value.IsAllowed && value.IsEnabled &&
            value.Method == ProcurementMethodType.NationalCompetitiveTendering);
        var committee = await db.ProcurementCommittees.IgnoreQueryFilters()
            .Include(value => value.Members).ThenInclude(value => value.Assignment)
                .ThenInclude(value => value.User)
            .SingleAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.Code == $"TE2E-{ShortHash(runId)}-EVAL", cancellationToken);
        await AddFixtureCommitteeControlAsync(
            tender, committee, policy, methodRule, officer, true, marker, cancellationToken);
        var control = await db.ProcurementEvaluationCommitteeControls.IgnoreQueryFilters()
            .SingleAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.SourceType == ProcurementEvaluationSourceType.Tender &&
                value.SourceId == tender.Id, cancellationToken);
        var meeting = await db.ProcurementEvaluationMeetings.IgnoreQueryFilters()
            .SingleAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.CommitteeControlId == control.Id &&
                value.Phase == ProcurementEvaluationPhase.Combined, cancellationToken);
        var appointment = await db.ProcurementEvaluationCommitteeAppointments.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.CommitteeControlId == control.Id)
            .OrderBy(value => value.Id)
            .FirstAsync(cancellationToken);
        var scoreSnapshot = JsonSerializer.Serialize(new
        {
            fixtureAssisted = true, runId, tenderId = tender.Id,
            evaluationId = evaluation.Id, bidId = bid.Id, totalScore = 95m
        });
        db.ProcurementEvaluationScoreSheets.Add(new ProcurementEvaluationScoreSheet
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CommitteeControlId = control.Id,
            MeetingId = meeting.Id, AppointmentId = appointment.Id,
            Phase = ProcurementEvaluationPhase.Combined,
            ScoreSubjectType = "TenderEvaluation", ScoreSubjectId = evaluation.Id,
            Attempt = 1, Status = ProcurementEvaluationScoreSheetStatus.Locked,
            SubmittedAtUtc = now.AddDays(-1), SubmittedByUserId = appointment.UserId,
            SubmittedByName = appointment.UserDisplayName, ScoreSnapshotJson = scoreSnapshot,
            SignatureReference = $"fixture://{marker}/score/signature",
            EvidenceReference = $"fixture://{marker}/score/evidence",
            IntegrityHash = Hash64(scoreSnapshot),
            IdempotencyKey = $"TE2E:{ShortHash(marker)}:score",
            CreatedAt = now.AddDays(-1), CreatedBy = FixtureActor,
            CreatedById = appointment.UserId
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureContractApprovedAwardAsync(
        Guid tenantId,
        string runId,
        ApplicationUser officer,
        ApplicationUser recommendationMaker,
        ApplicationUser independentApprover,
        CancellationToken cancellationToken)
    {
        var marker = $"TENDER-E2E:{runId}:CONTRACT-HANDOFF";
        if (await db.Tenders.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.Notes == marker,
                cancellationToken))
            return;

        var source = await db.PurchaseRequisitions.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.Notes == marker,
            cancellationToken);
        var supplier = await db.BusinessPartners.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            value.Notes == $"TENDER-E2E:{runId}:SUPPLIER-A", cancellationToken);
        var template = await db.EvaluationTemplates.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            value.TemplateCode == $"TE2E-{ShortHash(runId)}-ITB", cancellationToken);
        var now = DateTime.UtcNow;
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            TenderNumber = $"TND-TE2E-{ShortHash(marker)}",
            Title = "Tender E2E independently approved contract handoff",
            Description = "Fixture prerequisite whose handoff must execute through the real Contract API.",
            TenderType = "ITB", Status = "Awarded", PublishDate = now.AddDays(-3),
            SubmissionDeadline = now.AddDays(-2), OpeningDate = now.AddDays(-2).AddHours(1),
            AwardDate = now.AddDays(-1), EstimatedValue = source.TotalAmount, Currency = "GHS",
            SourcePurchaseRequisitionId = source.Id,
            EvaluationTemplateId = template.Id, PriceWeightage = 40m, QualityWeightage = 40m,
            DeliveryWeightage = 10m, ExperienceWeightage = 10m,
            CreatedById = officer.Id, PublishedById = officer.Id,
            AwardedById = independentApprover.Id, Notes = marker,
            TermsAndConditions = "Disposable contract-handoff acceptance fixture only.",
            CreatedAt = now.AddDays(-3)
        };
        var lot = new TenderLot
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id,
            LotNumber = 1, LotCode = "LOT-01", Title = "Contract handoff Goods lot",
            Description = "Exact approved demand for real contract creation.",
            EstimatedValue = source.TotalAmount, Currency = "GHS", Status = "Awarded",
            RequiredDeliveryDate = now.AddDays(30), DeliveryLocation = "Tender E2E acceptance location",
            DisplayOrder = 1, Notes = marker, CreatedAt = now.AddDays(-3), CreatedBy = FixtureActor
        };
        var item = new TenderItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id, LotId = lot.Id,
            LineNumber = 1, ItemCode = $"TE2E-{ShortHash("CONTRACT-HANDOFF")}",
            Description = "CONTRACT-HANDOFF approved Goods package", Quantity = 1m,
            UnitOfMeasure = "EA", Specifications = "Exact independently approved contract award package.",
            RequiredDeliveryDate = now.AddDays(30), DeliveryLocation = lot.DeliveryLocation,
            CreatedAt = now.AddDays(-3), CreatedBy = FixtureActor
        };
        tender.Lots.Add(lot);
        tender.Items.Add(item);
        tender.Invitations.Add(NewInvitation(
            tenantId, tender.Id, supplier.Id, officer.Id, "Submitted", now.AddDays(-2)));
        db.Tenders.Add(tender);
        await db.SaveChangesAsync(cancellationToken);

        await AddFixtureBidAsync(
            tender, lot, item, supplier, officer, "Awarded", source.TotalAmount, 1,
            cancellationToken);
        var bid = await db.TenderBids.IgnoreQueryFilters().SingleAsync(value =>
            value.TenantId == tenantId && value.TenderId == tender.Id && !value.IsDeleted,
            cancellationToken);
        var award = new TenderAward
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id, LotId = lot.Id,
            TenderBidId = bid.Id, BusinessPartnerId = supplier.Id,
            AwardDate = now.AddDays(-1), OriginalBidAmount = source.TotalAmount,
            AwardedAmount = source.TotalAmount, Currency = "GHS",
            AwardedById = independentApprover.Id,
            AwardJustification = "Independently approved real contract-handoff fixture prerequisite.",
            Status = "Awarded",
            Notes = $"{marker}; recommendation maker {recommendationMaker.Id:N}; independent approver {independentApprover.Id:N}",
            CreatedById = recommendationMaker.Id, CreatedAt = now.AddDays(-1), CreatedBy = FixtureActor
        };
        var readinessSnapshot = JsonSerializer.Serialize(new
        {
            fixturePrerequisite = true,
            runId,
            awardId = award.Id,
            tenderId = tender.Id,
            bidId = bid.Id,
            supplierId = supplier.Id,
            recommendationMakerId = recommendationMaker.Id,
            independentApproverId = independentApprover.Id
        });
        db.TenderAwards.Add(award);
        db.ProcurementAwardReadinessDecisions.Add(new ProcurementAwardReadinessDecision
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            SourceType = ProcurementAwardReadinessSourceType.Tender, SourceId = tender.Id,
            SourceReference = tender.TenderNumber,
            Method = ProcurementMethodType.NationalCompetitiveTendering,
            DecisionSequence = 1, Status = ProcurementAwardReadinessDecisionStatus.Ready,
            RecommendationSubjectType = "TenderBid",
            RecommendedSubjectIdsJson = JsonSerializer.Serialize(new[] { bid.Id }),
            RecommendedBusinessPartnerIdsJson = JsonSerializer.Serialize(new[] { supplier.Id }),
            RecommendationSnapshotJson = readinessSnapshot,
            SourceIntegrityHash = Hash64($"{tender.Id:N}|{source.Id:N}|{bid.Id:N}"),
            IntegrityHash = Hash64(readinessSnapshot),
            IdempotencyKey = $"TE2E-{ShortHash(marker)}-READY",
            CorrelationId = $"te2e-{ShortHash(marker)}",
            EvaluatedAtUtc = now.AddDays(-1), EvaluatedByUserId = independentApprover.Id,
            EvaluatedByName = independentApprover.FullName,
            CreatedAt = now.AddDays(-1), CreatedBy = FixtureActor,
            CreatedById = independentApprover.Id
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCrossTenantTenderAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        var tenantCode = $"TE2E-X-{ShortHash(runId)}";
        var foreignTenant = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(value =>
            value.Code == tenantCode && !value.IsDeleted, cancellationToken);
        if (foreignTenant is null)
        {
            foreignTenant = new Tenant
            {
                Id = Guid.NewGuid(), Code = tenantCode,
                Name = $"Tender E2E foreign tenant {ShortHash(runId)}",
                Description = "Disposable foreign-tenant isolation fixture.",
                Status = TenantStatus.Active, BaseCurrency = "GHS",
                CreatedAt = DateTime.UtcNow, CreatedBy = FixtureActor
            };
            db.Tenants.Add(foreignTenant);
            await db.SaveChangesAsync(cancellationToken);
        }

        var marker = $"TENDER-E2E:{runId}:CROSS-TENANT";
        if (await db.Tenders.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == foreignTenant.Id && !value.IsDeleted && value.Notes == marker,
                cancellationToken))
            return;
        db.Tenders.Add(new Tender
        {
            Id = Guid.NewGuid(), TenantId = foreignTenant.Id,
            TenderNumber = $"TND-XTEN-{ShortHash(marker)}",
            Title = "Foreign tenant tender isolation probe", Description = marker,
            TenderType = "ITB", Status = "Draft", EstimatedValue = 1_000m, Currency = "GHS",
            Notes = marker, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTenderScenarioAsync(
        Guid tenantId,
        string runId,
        string scenario,
        string status,
        BusinessPartner supplierA,
        BusinessPartner supplierB,
        EvaluationTemplate template,
        ApplicationUser officer,
        ApplicationUser evaluator,
        Account receivingAccount,
        Account revenueAccount,
        ProcurementPolicySet policy,
        ProcurementPolicyMethodRule methodRule,
        ProcurementCommittee committee,
        CommitteeFixtureState committeeState,
        CancellationToken cancellationToken)
    {
        var marker = $"TENDER-E2E:{runId}:{scenario}";
        if (await db.Tenders.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.Notes == marker,
                cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var future = scenario == "SUPPLIER-LIFECYCLE";
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderNumber = $"TND-TE2E-{ShortHash(marker)}",
            Title = $"Tender E2E {scenario}",
            Description = "Fixture-assisted browser scenario in an exact guarded disposable Testing database.",
            TenderType = "ITB",
            Status = status,
            PublishDate = future ? null : now.AddDays(-1),
            SubmissionDeadline = future ? now.AddHours(6) : now.AddHours(-4),
            OpeningDate = future ? now.AddHours(6).AddMinutes(15) : now.AddHours(-3),
            EstimatedValue = 30_000m,
            Currency = "GHS",
            MinimumPerformanceRating = 0m,
            RequiresPrequalification = false,
            AllowPartialBids = false,
            EvaluationTemplateId = template.Id,
            EvaluationTemplate = template,
            PriceWeightage = 40m,
            QualityWeightage = 40m,
            DeliveryWeightage = 10m,
            ExperienceWeightage = 10m,
            RequiresAcceptanceDeclaration = false,
            CreatedById = officer.Id,
            PublishedById = future ? null : officer.Id,
            Notes = marker,
            TermsAndConditions = "Disposable tender browser acceptance fixture only; never production terms.",
            CreatedAt = now.AddDays(-1)
        };
        var lot = new TenderLot
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id,
            LotNumber = 1, LotCode = "LOT-01", Title = $"{scenario} Goods lot",
            Description = "Fixture-assisted Goods lot.", EstimatedValue = 30_000m,
            Currency = "GHS", Status = "Active", RequiredDeliveryDate = now.AddDays(30),
            DeliveryLocation = "Tender E2E acceptance location", DisplayOrder = 1,
            Notes = marker, CreatedAt = now.AddDays(-1), CreatedBy = FixtureActor
        };
        var item = new TenderItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id, LotId = lot.Id,
            LineNumber = 1, ItemCode = $"TE2E-{ShortHash(scenario)}",
            Description = $"{scenario} business laptop", Quantity = 2m,
            UnitOfMeasure = "EA", Specifications = "Business laptop, 16 GB RAM, three-year warranty.",
            RequiredDeliveryDate = now.AddDays(30), DeliveryLocation = lot.DeliveryLocation,
            CreatedAt = now.AddDays(-1), CreatedBy = FixtureActor
        };
        tender.Lots.Add(lot);
        tender.Items.Add(item);
        tender.Invitations.Add(NewInvitation(tenantId, tender.Id, supplierA.Id, officer.Id,
            future ? "Invited" : "Submitted", now));
        tender.Invitations.Add(NewInvitation(tenantId, tender.Id, supplierB.Id, officer.Id,
            future ? "Invited" : "Submitted", now));
        tender.Evaluators.Add(new TenderEvaluator
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id,
            UserId = evaluator.Id, Role = "Evaluator", AssignedDate = now.AddDays(-1),
            AssignedById = officer.Id, Status = "Accepted", AcceptedDate = now.AddDays(-1),
            WeightagePercentage = 100m, Notes = marker,
            CreatedAt = now.AddDays(-1), CreatedBy = FixtureActor
        });
        if (future)
        {
            tender.Fees.Add(new TenderFee
            {
                Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tender.Id,
                FeeType = "TenderFee", Amount = 500m, Currency = "GHS",
                PaymentMethod = "BankTransfer", ReceivingAccountId = receivingAccount.Id,
                RevenueAccountId = revenueAccount.Id, IsMandatory = true,
                DueDate = now.AddHours(5),
                Description = "Fixture-assisted positive mandatory fee for payment verification coverage.",
                BankAccountDetails = "Disposable acceptance bank reference",
                CreatedAt = now, CreatedBy = FixtureActor
            });
        }

        db.Tenders.Add(tender);
        await db.SaveChangesAsync(cancellationToken);

        if (!future)
        {
            await AddFixtureBidAsync(tender, lot, item, supplierA, officer,
                status: committeeState == CommitteeFixtureState.ActiveReady ? "Submitted" : "Opened",
                amount: 24_000m, sequence: 1, cancellationToken);
            if (committeeState == CommitteeFixtureState.ActiveReady)
            {
                await AddFixtureBidAsync(tender, lot, item, supplierB, officer,
                    status: "Submitted", amount: 28_000m, sequence: 2, cancellationToken);
            }
        }

        if (committeeState != CommitteeFixtureState.None)
        {
            await AddFixtureCommitteeControlAsync(
                tender, committee, policy, methodRule, officer,
                committeeState == CommitteeFixtureState.ActiveReady, marker, cancellationToken);
        }
    }

    private async Task EnsureTenderDocumentTemplateAsync(
        Guid tenantId,
        string runId,
        ProcurementPolicySet policy,
        ProcurementPolicyMethodRule methodRule,
        CancellationToken cancellationToken)
    {
        var code = $"TE2E-{ShortHash(runId)}-NCT-DOC";
        if (await db.ProcurementTenderDocumentTemplateVersions.IgnoreQueryFilters()
            .AnyAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.TemplateCode == code && value.Version == 1, cancellationToken))
        {
            return;
        }

        if (!methodRule.WorkflowDefinitionId.HasValue)
            throw new InvalidOperationException(
                "The disposable NCT method rule must retain its published award workflow before the tender-document template is created.");

        var now = DateTime.UtcNow;
        var snapshot = JsonSerializer.Serialize(new
        {
            fixtureAssisted = true,
            runId,
            method = ProcurementMethodType.NationalCompetitiveTendering,
            policyId = policy.Id,
            methodRuleId = methodRule.Id
        });
        var template = new ProcurementTenderDocumentTemplateVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateKey = Guid.NewGuid(),
            TemplateCode = code,
            Name = "Tender E2E NCT controlled document",
            Description = "Published fixture prerequisite for disposable real-browser NCT submission acceptance.",
            DocumentTypeCode = "ITB",
            Version = 1,
            Status = ProcurementTenderDocumentTemplateStatus.Published,
            EffectiveFromUtc = now.AddDays(-1),
            EffectiveToUtc = now.AddYears(1),
            PolicySetId = policy.Id,
            PolicySetCode = policy.Code,
            PolicySetVersion = policy.Version,
            SourceConfigurationProfileId = policy.SourceConfigurationProfileId,
            ContentReference = $"fixture://tender-e2e/{runId}/nct-document",
            ContentChecksumSha256 = Hash64($"{runId}|NCT|controlled-document"),
            WorkflowDefinitionId = methodRule.WorkflowDefinitionId.Value,
            ApprovalEvidenceReference = $"fixture://tender-e2e/{runId}/nct-document-approval",
            PublishedAtUtc = now,
            PublishedByName = FixtureActor,
            LifecycleSnapshotJson = snapshot,
            IntegrityHash = Hash64(snapshot),
            CreatedAt = now,
            CreatedBy = FixtureActor
        };
        template.ApplicableMethods.Add(new ProcurementTenderDocumentTemplateMethod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateVersionId = template.Id,
            Method = ProcurementMethodType.NationalCompetitiveTendering,
            IntegrityHash = Hash64($"{template.Id}|{ProcurementMethodType.NationalCompetitiveTendering}"),
            CreatedAt = now,
            CreatedBy = FixtureActor
        });
        db.ProcurementTenderDocumentTemplateVersions.Add(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AddFixtureBidAsync(
        Tender tender,
        TenderLot lot,
        TenderItem item,
        BusinessPartner supplier,
        ApplicationUser officer,
        string status,
        decimal amount,
        int sequence,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var bid = new TenderBid
        {
            Id = Guid.NewGuid(), TenantId = tender.TenantId, TenderId = tender.Id,
            BusinessPartnerId = supplier.Id,
            BidNumber = $"BID-TE2E-{ShortHash($"{tender.Id:N}:{sequence}")}",
            SubmittedDate = now.AddHours(-5).AddMinutes(sequence), Status = status,
            OpenedDate = status == "Opened" ? now.AddHours(-2) : null,
            OpenedById = status == "Opened" ? officer.Id : null,
            TotalBidAmount = amount, Currency = "GHS", DeliveryDays = 14 + sequence,
            PaymentTerms = "Net 30 days", WarrantyTerms = "Three years",
            TechnicalProposal = "Fixture-assisted responsive technical proposal retained for browser evaluation.",
            CommercialProposal = "Fixture-assisted responsive commercial proposal retained for browser evaluation.",
            AssociationType = "Self", AcceptedDeclaration = true,
            DeclarationAcceptedAt = now.AddHours(-6), IsCompliant = true,
            CreatedAt = now.AddHours(-5).AddMinutes(sequence), CreatedBy = FixtureActor
        };
        var bidLot = new TenderBidLot
        {
            Id = Guid.NewGuid(), TenantId = tender.TenantId, TenderBidId = bid.Id,
            LotId = lot.Id, TotalLotAmount = amount, Currency = "GHS",
            DeliveryDays = bid.DeliveryDays, PaymentTerms = bid.PaymentTerms,
            WarrantyTerms = bid.WarrantyTerms, TechnicalProposal = bid.TechnicalProposal,
            CommercialProposal = bid.CommercialProposal, Status = "Submitted",
            Notes = tender.Notes, CreatedAt = bid.CreatedAt, CreatedBy = FixtureActor
        };
        bid.BidLots.Add(bidLot);
        bid.Items.Add(new TenderBidItem
        {
            Id = Guid.NewGuid(), TenantId = tender.TenantId, TenderBidId = bid.Id,
            BidLotId = bidLot.Id, TenderItemId = item.Id, OfferedQuantity = item.Quantity,
            UnitPrice = amount / item.Quantity, TotalPrice = amount,
            DeliveryDays = bid.DeliveryDays, Specifications = item.Specifications,
            Brand = sequence == 1 ? "SupplierA" : "SupplierB",
            Model = sequence == 1 ? "A-100" : "B-200",
            TechnicalDetails = "Fixture-assisted bid line retained for persistence checks.",
            CreatedAt = bid.CreatedAt, CreatedBy = FixtureActor
        });
        db.TenderBids.Add(bid);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AddFixtureCommitteeControlAsync(
        Tender tender,
        ProcurementCommittee committee,
        ProcurementPolicySet policy,
        ProcurementPolicyMethodRule methodRule,
        ApplicationUser officer,
        bool activate,
        string marker,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fixtureKey = $"TE2E:{ShortHash(marker)}";
        var control = new ProcurementEvaluationCommitteeControl
        {
            Id = Guid.NewGuid(), TenantId = tender.TenantId,
            SourceType = ProcurementEvaluationSourceType.Tender, SourceId = tender.Id,
            Version = 1, SourceReference = tender.TenderNumber,
            Purpose = "Fixture-assisted evaluation committee state for disposable browser acceptance.",
            Status = ProcurementEvaluationCommitteeControlStatus.Draft,
            CommitteeTemplateId = committee.Id, CommitteeCode = committee.Code,
            CommitteeName = committee.Name, RequiredQuorum = committee.RequiredQuorum,
            PolicySetId = policy.Id, PolicyCode = policy.Code, PolicyVersion = policy.Version,
            ConfigurationProfileId = policy.SourceConfigurationProfileId,
            ConfigurationProfileCode = policy.SourceConfigurationProfile?.ProfileCode,
            ConfigurationProfileVersion = policy.SourceConfigurationProfile?.Version,
            MethodRuleId = methodRule.Id, MethodRuleCode = methodRule.RuleCode,
            WorkflowDefinitionId = methodRule.WorkflowDefinitionId,
            EffectiveFromUtc = now.AddDays(-1), EffectiveToUtc = now.AddYears(1),
            CreationIdempotencyKey = $"{fixtureKey}:committee",
            CompositionSnapshotJson = "{}", CompositionIntegrityHash = Hash64("{}"),
            CreatedAt = now, CreatedBy = FixtureActor
        };
        foreach (var member in committee.Members.OrderBy(value => value.MemberKind))
        {
            control.RequiredRoles.Add(new ProcurementEvaluationCommitteeRoleRequirement
            {
                Id = Guid.NewGuid(), TenantId = tender.TenantId,
                MemberKind = member.MemberKind, RoleName = member.Assignment.RoleName,
                MinimumCount = 1, IsVoting = member.IsVoting, IsRequiredForQuorum = true,
                CreatedAt = now, CreatedBy = FixtureActor
            });
            control.Appointments.Add(new ProcurementEvaluationCommitteeAppointment
            {
                Id = Guid.NewGuid(), TenantId = tender.TenantId,
                CommitteeMemberId = member.Id, ResponsibilityAssignmentId = member.AssignmentId,
                UserId = member.Assignment.UserId,
                UserDisplayName = member.Assignment.User.UserName ?? member.Assignment.User.Email ?? "Evaluator",
                RoleName = member.Assignment.RoleName, MemberKind = member.MemberKind,
                IsVoting = member.IsVoting, EffectiveFromUtc = now.AddDays(-1),
                EffectiveToUtc = now.AddYears(1), Status = ProcurementEvaluationAppointmentStatus.Pending,
                CreatedAt = now, CreatedBy = FixtureActor
            });
        }
        control.CompositionSnapshotJson = JsonSerializer.Serialize(new
        {
            fixtureAssisted = true, marker, control.Id, control.SourceId,
            members = control.Appointments.Select(value => new
                { value.UserId, value.MemberKind, value.RoleName, value.IsVoting })
        });
        control.CompositionIntegrityHash = Hash64(control.CompositionSnapshotJson);
        db.ProcurementEvaluationCommitteeControls.Add(control);
        await db.SaveChangesAsync(cancellationToken);
        if (!activate)
            return;

        control.Status = ProcurementEvaluationCommitteeControlStatus.Active;
        control.ActivatedAtUtc = now;
        control.ActivatedByUserId = officer.Id;
        control.ActivationEvidenceReference = $"fixture://{marker}/committee-constitution";
        control.ActivationIdempotencyKey = $"{fixtureKey}:activate";
        await db.SaveChangesAsync(cancellationToken);

        foreach (var appointment in control.Appointments)
        {
            appointment.Status = ProcurementEvaluationAppointmentStatus.Accepted;
            appointment.AcceptedAtUtc = now;
            appointment.AcceptanceSignatureReference = $"fixture://{marker}/appointment/{appointment.Id:N}/signature";
            appointment.AcceptanceEvidenceReference = $"fixture://{marker}/appointment/{appointment.Id:N}/evidence";
            appointment.AcceptanceIdempotencyKey = $"{fixtureKey}:accept:{appointment.Id:N}";
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var appointment in control.Appointments)
        {
            var snapshot = JsonSerializer.Serialize(new
            {
                fixtureAssisted = true, marker, appointment.Id, appointment.UserId,
                outcome = ProcurementEvaluationConflictOutcome.NoConflict
            });
            db.ProcurementEvaluationConflictDeclarations.Add(new ProcurementEvaluationConflictDeclaration
            {
                Id = Guid.NewGuid(), TenantId = tender.TenantId, AppointmentId = appointment.Id,
                Version = 1, Outcome = ProcurementEvaluationConflictOutcome.NoConflict,
                Declaration = "Fixture-assisted no-conflict declaration for disposable browser acceptance.",
                SignatureReference = $"fixture://{marker}/coi/{appointment.Id:N}/signature",
                EvidenceReference = $"fixture://{marker}/coi/{appointment.Id:N}/evidence",
                ValidFromUtc = now.AddDays(-1), ValidToUtc = now.AddYears(1),
                DeclaredAtUtc = now, DeclaredByUserId = appointment.UserId,
                IdempotencyKey = $"{fixtureKey}:coi:{appointment.Id:N}",
                SnapshotJson = snapshot, IntegrityHash = Hash64(snapshot),
                CreatedAt = now, CreatedBy = FixtureActor
            });
        }
        await db.SaveChangesAsync(cancellationToken);

        var evaluationPhases = new[]
        {
            ProcurementEvaluationPhase.Technical,
            ProcurementEvaluationPhase.Financial,
            ProcurementEvaluationPhase.Combined
        };
        for (var phaseIndex = 0; phaseIndex < evaluationPhases.Length; phaseIndex++)
        {
            var phase = evaluationPhases[phaseIndex];
            var phaseKey = phase.ToString().ToLowerInvariant();
            var meeting = new ProcurementEvaluationMeeting
            {
                Id = Guid.NewGuid(), TenantId = tender.TenantId, CommitteeControlId = control.Id,
                Sequence = phaseIndex + 1, Phase = phase,
                Status = ProcurementEvaluationMeetingStatus.Draft, MeetingMode = "InPerson",
                MeetingChannel = "Disposable browser acceptance room", ScheduledAtUtc = now.AddMinutes(-30),
                EvidenceReference = $"fixture://{marker}/{phaseKey}-meeting-notice",
                QuorumSnapshotJson = "{}", QuorumIntegrityHash = Hash64("{}"),
                IdempotencyKey = $"{fixtureKey}:meeting:{phaseKey}", CreatedAt = now, CreatedBy = FixtureActor
            };
            db.ProcurementEvaluationMeetings.Add(meeting);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var appointment in control.Appointments)
            {
                var snapshot = JsonSerializer.Serialize(new
                {
                    fixtureAssisted = true, marker, phase, meetingId = meeting.Id,
                    appointmentId = appointment.Id,
                    appointment.UserId, present = true
                });
                db.ProcurementEvaluationAttendanceRecords.Add(new ProcurementEvaluationAttendanceRecord
                {
                    Id = Guid.NewGuid(), TenantId = tender.TenantId, MeetingId = meeting.Id,
                    AppointmentId = appointment.Id, IsPresent = true, SignedAtUtc = now,
                    SignatureReference = $"fixture://{marker}/{phaseKey}/attendance/{appointment.Id:N}/signature",
                    EvidenceReference = $"fixture://{marker}/{phaseKey}/attendance/{appointment.Id:N}/evidence",
                    WasEligibleAtSignature = true,
                    IdempotencyKey = $"{fixtureKey}:attendance:{phaseKey}:{appointment.Id:N}",
                    SnapshotJson = snapshot, IntegrityHash = Hash64(snapshot),
                    CreatedAt = now, CreatedBy = FixtureActor
                });
            }
            await db.SaveChangesAsync(cancellationToken);

            meeting.Status = ProcurementEvaluationMeetingStatus.QuorumConfirmed;
            meeting.StartedAtUtc = now;
            meeting.EligibleVotingMemberCount = control.Appointments.Count(value => value.IsVoting);
            meeting.SignedVotingAttendanceCount = control.Appointments.Count(value => value.IsVoting);
            meeting.ChairPresent = true;
            meeting.SecretaryPresent = true;
            meeting.QuorumMet = true;
            meeting.QuorumIdempotencyKey = $"{fixtureKey}:quorum:{phaseKey}";
            meeting.QuorumSnapshotJson = JsonSerializer.Serialize(new
            {
                fixtureAssisted = true, marker, phase, meeting.Id, meeting.CommitteeControlId,
                meeting.EligibleVotingMemberCount, meeting.SignedVotingAttendanceCount,
                meeting.ChairPresent, meeting.SecretaryPresent, meeting.QuorumMet
            });
            meeting.QuorumIntegrityHash = Hash64(meeting.QuorumSnapshotJson);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ProcurementCommittee> EnsureEvaluationCommitteeTemplateAsync(
        Guid tenantId,
        string runId,
        IReadOnlyList<ApplicationUser> evaluators,
        CancellationToken cancellationToken)
    {
        var code = $"TE2E-{ShortHash(runId)}-EVAL";
        var existing = await db.ProcurementCommittees.IgnoreQueryFilters()
            .Include(value => value.Members).ThenInclude(value => value.Assignment)
                .ThenInclude(value => value.User)
            .SingleOrDefaultAsync(value => value.TenantId == tenantId &&
                !value.IsDeleted && value.Code == code, cancellationToken);
        if (existing is not null)
            return existing;

        var role = await db.Roles.SingleAsync(value => value.Name == "TDC_EVALUATOR",
            cancellationToken);
        var assignments = new List<ProcurementResponsibilityAssignment>();
        foreach (var evaluator in evaluators)
        {
            var assignment = await db.ProcurementResponsibilityAssignments.IgnoreQueryFilters()
                .Include(value => value.User)
                .SingleOrDefaultAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                    value.IsActive && value.UserId == evaluator.Id && value.RoleName == role.Name,
                    cancellationToken);
            if (assignment is null)
            {
                assignment = new ProcurementResponsibilityAssignment
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, UserId = evaluator.Id,
                    RoleId = role.Id, RoleName = role.Name!,
                    WarehouseScopeMode = ProcurementWarehouseScopeMode.None,
                    LocationScopeMode = ProcurementLocationScopeMode.None,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1), EffectiveTo = DateTime.UtcNow.AddYears(1),
                    IsActive = true,
                    Reason = "Fixture-assisted evaluation responsibility in a disposable browser database.",
                    User = evaluator, Role = role, CreatedAt = DateTime.UtcNow, CreatedBy = FixtureActor
                };
                db.ProcurementResponsibilityAssignments.Add(assignment);
                await db.SaveChangesAsync(cancellationToken);
            }
            assignments.Add(assignment);
        }

        var committee = new ProcurementCommittee
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = code,
            Name = "Tender E2E Evaluation Committee",
            Description = "Fixture-assisted active committee template in a disposable browser database.",
            CommitteeType = ProcurementCommitteeType.EvaluationCommittee,
            Status = ProcurementCommitteeStatus.Active, RequiredQuorum = 2,
            RequiredRoleName = role.Name!, EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            EffectiveTo = DateTime.UtcNow.AddYears(1),
            ChangeReason = "Disposable browser acceptance fixture only.",
            CreatedAt = DateTime.UtcNow, CreatedBy = FixtureActor
        };
        var kinds = new[]
        {
            ProcurementCommitteeMemberKind.Chair,
            ProcurementCommitteeMemberKind.Secretary,
            ProcurementCommitteeMemberKind.VotingMember
        };
        for (var index = 0; index < assignments.Count; index++)
        {
            committee.Members.Add(new ProcurementCommitteeMember
            {
                Id = Guid.NewGuid(), TenantId = tenantId, CommitteeId = committee.Id,
                AssignmentId = assignments[index].Id, Assignment = assignments[index],
                MemberKind = kinds[index], IsVoting = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1), EffectiveTo = DateTime.UtcNow.AddYears(1),
                IsActive = true, Reason = "Fixture-assisted committee composition.",
                CreatedAt = DateTime.UtcNow, CreatedBy = FixtureActor
            });
        }
        db.ProcurementCommittees.Add(committee);
        await db.SaveChangesAsync(cancellationToken);
        return committee;
    }

    private async Task<(Account Receiving, Account Revenue)> EnsureTenderFeeAccountsAsync(
        Guid tenantId,
        string runId,
        CancellationToken cancellationToken)
    {
        var receiving = await db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.Status == AccountStatus.Active &&
            value.AccountType == AccountType.Asset && value.AllowDirectPosting && !value.IsControlAccount &&
            value.CurrencyCode == "GHS", cancellationToken);
        var revenue = await db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.Status == AccountStatus.Active &&
            value.AccountType == AccountType.Revenue && value.AllowDirectPosting && !value.IsControlAccount &&
            value.CurrencyCode == "GHS", cancellationToken);
        if (receiving is null)
        {
            receiving = NewFixtureAccount(tenantId, runId, "CASH", AccountType.Asset,
                "Tender E2E fee receiving account");
            db.Accounts.Add(receiving);
        }
        if (revenue is null)
        {
            revenue = NewFixtureAccount(tenantId, runId, "REV", AccountType.Revenue,
                "Tender E2E fee revenue account");
            db.Accounts.Add(revenue);
        }
        await db.SaveChangesAsync(cancellationToken);
        return (receiving, revenue);
    }

    private async Task EnsureCurrentOpenFiscalPeriodAsync(
        Guid tenantId,
        string runId,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var coveringPeriods = await db.FiscalPeriods.IgnoreQueryFilters().Where(value =>
            value.TenantId == tenantId &&
            !value.IsDeleted &&
            value.StartDate <= today &&
            value.EndDate >= today)
            .ToListAsync(cancellationToken);
        if (coveringPeriods.Count > 1)
            throw new InvalidOperationException(
                "Tender browser fixture found overlapping fiscal periods for the current posting date.");
        if (coveringPeriods.Count == 1)
        {
            var existingPeriod = coveringPeriods[0];
            var existingYear = await db.FiscalYears.IgnoreQueryFilters().SingleAsync(value =>
                value.TenantId == tenantId && value.Id == existingPeriod.FiscalYearId,
                cancellationToken);
            existingYear.Status = "Open";
            existingYear.IsActive = true;
            existingYear.IsClosed = false;
            existingYear.IsLocked = false;
            existingPeriod.PeriodStatus = "Open";
            existingPeriod.IsOpen = true;
            existingPeriod.IsClosed = false;
            existingPeriod.IsLocked = false;
            existingPeriod.IsGlobalLockSuspended = false;
            existingPeriod.AllowFutureDating = false;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var yearStart = new DateTime(today.Year, 1, 1);
        var yearEnd = new DateTime(today.Year, 12, 31);
        var fiscalYear = new ErpSystem.Core.Entities.Finance.FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = $"Tender E2E Fiscal Year {today.Year}",
            FiscalYearCode = $"FY{today.Year}-{ShortHash(runId)[..8]}",
            Year = today.Year,
            FiscalYearType = "Calendar",
            StartDate = yearStart,
            EndDate = yearEnd,
            TotalDays = (yearEnd - yearStart).Days + 1,
            NumberOfPeriods = 12,
            Status = "Open",
            IsActive = true,
            IsClosed = false,
            IsLocked = false,
            ReportingFramework = "IFRS",
            BaseCurrency = "GHS",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = FixtureActor
        };
        var periodStart = new DateTime(today.Year, today.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var fiscalPeriod = new ErpSystem.Core.Entities.Finance.FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = $"{periodStart:MMMM yyyy}",
            PeriodCode = $"P{today:yyyyMM}-{ShortHash(runId)[..8]}",
            PeriodNumber = today.Month,
            PeriodType = PeriodType.Monthly,
            StartDate = periodStart,
            EndDate = periodEnd,
            PeriodDays = (periodEnd - periodStart).Days + 1,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false,
            IsGlobalLockSuspended = false,
            AllowFutureDating = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = FixtureActor
        };

        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(fiscalPeriod);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Account NewFixtureAccount(
        Guid tenantId,
        string runId,
        string suffix,
        AccountType type,
        string name)
    {
        var code = $"TE2E-{ShortHash(runId)}-{suffix}";
        return new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = code,
            AccountNumber = code, AccountName = name, AccountType = type,
            AccountCategory = type == AccountType.Asset ? "Current Assets" : "Other Income",
            Description = "Fixture-only direct-posting account in a disposable Tender browser database.",
            IsSegmented = false, CurrencyCode = "GHS", IsMultiCurrency = false,
            AllowDirectPosting = true, IsControlAccount = false, Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow, CreatedBy = FixtureActor
        };
    }

    private static TenderInvitation NewInvitation(
        Guid tenantId,
        Guid tenderId,
        Guid partnerId,
        Guid officerId,
        string status,
        DateTime now) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = tenderId,
            BusinessPartnerId = partnerId,
            InvitedDate = now.AddDays(-1), InvitedById = officerId, Status = status,
            ResponseDate = status == "Submitted" ? now.AddHours(-5) : null,
            CreatedAt = now.AddDays(-1), CreatedBy = FixtureActor
        };

    private static string Hash64(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private enum CommitteeFixtureState
    {
        None,
        Draft,
        ActiveReady
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..12];
    }

    private async Task PrepareActorAsync(
        Guid tenantId,
        ActorDefinition actor,
        CancellationToken cancellationToken)
    {
        var password = Environment.GetEnvironmentVariable(actor.PasswordEnvironmentName);
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"{actor.PasswordEnvironmentName} must be set for disposable tender browser seeding.");
        }

        var user = await userManager.FindByNameAsync(actor.Username);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = actor.Username,
                Email = actor.Email,
                EmailConfirmed = true,
                FirstName = actor.FirstName,
                LastName = actor.LastName,
                TenantId = tenantId,
                AuthenticationProvider = AuthenticationProvider.Local,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tender E2E Seeder"
            };

            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create tender acceptance actor '{actor.Username}': " +
                    string.Join(", ", created.Errors.Select(value => value.Description)));
            }
        }
        else
        {
            user.TenantId = tenantId;
            user.IsActive = true;
            user.EmailConfirmed = true;
            user.AuthenticationProvider = AuthenticationProvider.Local;
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, resetToken, password);
            if (!reset.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign the ephemeral password for tender actor '{actor.Username}'.");
            }

            var updated = await userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not activate tender actor '{actor.Username}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(actor.RoleName) &&
            !await userManager.IsInRoleAsync(user, actor.RoleName))
        {
            var roleResult = await userManager.AddToRoleAsync(user, actor.RoleName);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign role '{actor.RoleName}' to tender actor '{actor.Username}'.");
            }
        }

        var relationship = await db.UserTenants.IgnoreQueryFilters().SingleOrDefaultAsync(
            value => value.UserId == user.Id && value.TenantId == tenantId && !value.IsDeleted,
            cancellationToken);
        if (relationship is null)
        {
            db.UserTenants.Add(new UserTenant
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenantId,
                AccessLevel = UserTenantAccessLevel.Standard,
                Status = UserTenantStatus.Active,
                IsDefault = true,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = "Tender E2E Seeder",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tender E2E Seeder"
            });
        }
        else
        {
            relationship.Status = UserTenantStatus.Active;
            relationship.IsDefault = true;
            relationship.IsDeleted = false;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record ActorDefinition(
        string Username,
        string Email,
        string FirstName,
        string LastName,
        string PasswordEnvironmentName,
        string? RoleName);
}
