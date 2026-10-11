using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Provisions the two explicit sourcing policies used by the reusable deployment-UAT
/// walkthrough. The seeder owns only its reserved codes and never reconciles an
/// existing policy, profile, or tenant workflow in place.
/// </summary>
public sealed class ProcurementUatPolicySetSeeder(ApplicationDbContext context)
{
    public const string WorksPolicyCode = "UAT-WORKS-TENDER-50K-100K";
    public const string GeneralServicesPolicyCode = "UAT-CLEANING-RFQ-1K-50K";
    public const string SourceProfileCode = "UAT-SOURCING-POLICY-SOURCE";
    public const string SeedActor = "Operational UAT procurement policy baseline";

    private const string WorksServiceClass = "Measured construction works";
    private const string CleaningServiceClass = "Cleaning services";

    private static readonly RequiredWorkflow[] RequiredWorkflows =
    [
        new("TDC_PURCHASE_REQUISITION", "Operational UAT Purchase Requisition Approval"),
        new("TDC_TENDER_APPROVAL", "Operational UAT Tender Approval"),
        new("TDC_TENDER_EVALUATION", "Operational UAT Tender Evaluation"),
        new("TDC_TENDER_AWARD", "Operational UAT Tender Award Approval"),
        new("TDC_CONTRACT", "Operational UAT Procurement Contract Approval")
    ];

    public async Task<ProcurementUatPolicySeedResult> SeedTenantAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("A UAT seed actor is required.", nameof(actorUserId));

        var now = DateTime.UtcNow;
        var workflows = new Dictionary<string, WorkflowDefinition>(StringComparer.OrdinalIgnoreCase);
        var createdWorkflows = 0;
        foreach (var requirement in RequiredWorkflows)
        {
            var outcome = await EnsurePublishedWorkflowAsync(
                tenantId,
                actorUserId,
                requirement,
                now,
                cancellationToken);
            workflows[requirement.TemplateCode] = outcome.Definition;
            createdWorkflows += outcome.Created ? 1 : 0;
        }

        var sourceProfile = await EnsurePublishedSourceProfileAsync(
            tenantId,
            actorUserId,
            now,
            cancellationToken);

        var roles = await LoadRequiredRolesAsync(cancellationToken);
        var createdPolicies = 0;
        createdPolicies += await EnsureWorksPolicyAsync(
            tenantId,
            actorUserId,
            sourceProfile,
            roles,
            workflows,
            now,
            cancellationToken) ? 1 : 0;
        createdPolicies += await EnsureCleaningPolicyAsync(
            tenantId,
            actorUserId,
            sourceProfile,
            roles,
            workflows,
            now,
            cancellationToken) ? 1 : 0;

        return new ProcurementUatPolicySeedResult(createdPolicies, createdWorkflows);
    }

    private async Task<bool> EnsureWorksPolicyAsync(
        Guid tenantId,
        Guid actorUserId,
        ProcurementConfigurationProfile sourceProfile,
        IReadOnlyDictionary<string, ErpSystem.Core.Entities.ApplicationRole> roles,
        IReadOnlyDictionary<string, WorkflowDefinition> workflows,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (await PolicyCodeExistsAsync(tenantId, WorksPolicyCode, cancellationToken))
            return false;

        var policy = NewPolicy(
            tenantId,
            actorUserId,
            sourceProfile,
            WorksPolicyCode,
            "UAT Works Tender GHS 50,000-100,000",
            "Published UAT sourcing policy for measured Works procured through a competitive tender and carried through award to a Works contract.",
            now);

        AddCategoryMethodThreshold(
            policy,
            ProcurementCategoryClass.Works,
            WorksServiceClass,
            ProcurementMethodType.NationalCompetitiveTendering,
            workflows["TDC_TENDER_AWARD"].Id,
            50_000m,
            100_000m,
            minimumQuotationCount: 3,
            "UAT approved Works tender band; confirm statutory production thresholds before operational use.",
            now);
        AddAuthority(
            policy,
            ProcurementCategoryClass.Works,
            roles["TDC_MANAGING_DIRECTOR"],
            workflows["TDC_PURCHASE_REQUISITION"].Id,
            50_000m,
            100_000m,
            now);
        AddEvidence(policy, ProcurementCategoryClass.Works, ProcurementMethodType.NationalCompetitiveTendering,
            ProcurementEvidenceStage.Sourcing, "Approved Works specification and BOQ", "DEC-003", 10, now);
        AddEvidence(policy, ProcurementCategoryClass.Works, ProcurementMethodType.NationalCompetitiveTendering,
            ProcurementEvidenceStage.Evaluation, "Tender evaluation report", "DEC-004", 20, now);
        AddEvidence(policy, ProcurementCategoryClass.Works, ProcurementMethodType.NationalCompetitiveTendering,
            ProcurementEvidenceStage.Award, "Approved tender award decision", "DEC-004", 30, now);
        AddEvidence(policy, ProcurementCategoryClass.Works, ProcurementMethodType.NationalCompetitiveTendering,
            ProcurementEvidenceStage.Contract, "Executed Works contract", "DEC-008", 40, now);
        AddSod(policy, roles["TDC_PROCUREMENT_OFFICER"], roles["TDC_MANAGING_DIRECTOR"],
            "PurchaseRequisition", "Submit", now);

        context.ProcurementPolicySets.Add(policy);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> EnsureCleaningPolicyAsync(
        Guid tenantId,
        Guid actorUserId,
        ProcurementConfigurationProfile sourceProfile,
        IReadOnlyDictionary<string, ErpSystem.Core.Entities.ApplicationRole> roles,
        IReadOnlyDictionary<string, WorkflowDefinition> workflows,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (await PolicyCodeExistsAsync(tenantId, GeneralServicesPolicyCode, cancellationToken))
            return false;

        var policy = NewPolicy(
            tenantId,
            actorUserId,
            sourceProfile,
            GeneralServicesPolicyCode,
            "UAT Cleaning Services RFQ GHS 1,000-50,000",
            "Published UAT sourcing policy for competitive cleaning-service quotations and the resulting awarded services contract.",
            now);

        AddCategoryMethodThreshold(
            policy,
            ProcurementCategoryClass.GeneralServices,
            CleaningServiceClass,
            ProcurementMethodType.RequestForQuotation,
            workflows["TDC_TENDER_EVALUATION"].Id,
            1_000m,
            50_000m,
            minimumQuotationCount: 3,
            "UAT approved General Services RFQ band for cleaning services; confirm statutory production thresholds before operational use.",
            now);
        AddAuthority(
            policy,
            ProcurementCategoryClass.GeneralServices,
            roles["TDC_MANAGING_DIRECTOR"],
            workflows["TDC_PURCHASE_REQUISITION"].Id,
            1_000m,
            50_000m,
            now);
        AddEvidence(policy, ProcurementCategoryClass.GeneralServices, ProcurementMethodType.RequestForQuotation,
            ProcurementEvidenceStage.Sourcing, "Approved cleaning services scope", "DEC-003", 10, now);
        AddEvidence(policy, ProcurementCategoryClass.GeneralServices, ProcurementMethodType.RequestForQuotation,
            ProcurementEvidenceStage.Evaluation, "Three responsive supplier quotations", "DEC-004", 20, now);
        AddEvidence(policy, ProcurementCategoryClass.GeneralServices, ProcurementMethodType.RequestForQuotation,
            ProcurementEvidenceStage.Award, "Approved quotation evaluation and award", "DEC-004", 30, now);
        AddEvidence(policy, ProcurementCategoryClass.GeneralServices, ProcurementMethodType.RequestForQuotation,
            ProcurementEvidenceStage.Contract, "Executed cleaning services contract", "DEC-008", 40, now);
        AddSod(policy, roles["TDC_PROCUREMENT_OFFICER"], roles["TDC_MANAGING_DIRECTOR"],
            "PurchaseRequisition", "Submit", now);

        context.ProcurementPolicySets.Add(policy);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> PolicyCodeExistsAsync(
        Guid tenantId,
        string policyCode,
        CancellationToken cancellationToken) =>
        await context.ProcurementPolicySets.IgnoreQueryFilters().AnyAsync(value =>
            value.TenantId == tenantId && value.Code == policyCode,
            cancellationToken);

    private async Task<ProcurementConfigurationProfile> EnsurePublishedSourceProfileAsync(
        Guid tenantId,
        Guid actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await context.ProcurementConfigurationProfiles.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && value.ProfileCode == SourceProfileCode)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted || existing.LifecycleStatus != ProcurementConfigurationProfileStatus.Published)
            {
                throw new InvalidOperationException(
                    $"Reserved UAT source profile '{SourceProfileCode}' already exists but is not an active Published profile. " +
                    "It was preserved; resolve it through Procurement configuration before retrying UAT provisioning.");
            }
            return existing;
        }

        var profile = new ProcurementConfigurationProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProfileKey = Guid.NewGuid(),
            ProfileCode = SourceProfileCode,
            Name = "Operational UAT sourcing policy source",
            Version = 1,
            LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
            EffectiveFrom = now.Date,
            ChangeSummary = "Controlled, fictional deployment-UAT source for the seeded Works and cleaning-services sourcing policies.",
            IsDefault = false,
            PublishedAt = now,
            PublishedById = actorUserId,
            CreatedAt = now,
            CreatedBy = SeedActor,
            CreatedById = actorUserId
        };
        context.ProcurementConfigurationProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private async Task<IReadOnlyDictionary<string, ErpSystem.Core.Entities.ApplicationRole>> LoadRequiredRolesAsync(
        CancellationToken cancellationToken)
    {
        var required = new[] { "TDC_PROCUREMENT_OFFICER", "TDC_MANAGING_DIRECTOR" };
        var roles = await context.Roles
            .Where(value => value.Name != null && required.Contains(value.Name))
            .ToListAsync(cancellationToken);
        var byName = roles.ToDictionary(value => value.Name!, StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(value => !byName.ContainsKey(value)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Operational UAT procurement policy provisioning requires seeded roles: " + string.Join(", ", missing));
        }
        return byName;
    }

    private async Task<WorkflowSeedOutcome> EnsurePublishedWorkflowAsync(
        Guid tenantId,
        Guid actorUserId,
        RequiredWorkflow requirement,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var template = ProcurementAccessControlRegistry.Workflows.Single(value =>
            value.Code == requirement.TemplateCode);
        var entityType = await context.WorkflowEntityTypes.IgnoreQueryFilters()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                value.IsActive &&
                (value.Code == template.EntityTypeCode || value.Name == template.EntityTypeName),
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Workflow entity type '{template.EntityTypeCode}' must be seeded before UAT policies are provisioned.");

        var published = await context.WorkflowDefinitions.IgnoreQueryFilters()
            .Include(value => value.Steps)
            .Include(value => value.Transitions)
            .Where(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                value.IsActive &&
                value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                value.EntityTypeId == entityType.Id)
            .OrderByDescending(value => value.Name == requirement.SeedName)
            .ThenByDescending(value => value.Version)
            .ToListAsync(cancellationToken);
        if (published.Count > 0)
            return new WorkflowSeedOutcome(published[0], false);

        var definitionId = Guid.NewGuid();
        var submittedId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        var definition = new WorkflowDefinition
        {
            Id = definitionId,
            TenantId = tenantId,
            DefinitionKey = Guid.NewGuid(),
            Name = requirement.SeedName,
            Description = $"Published fictional deployment-UAT route for {template.EntityTypeName}.",
            EntityTypeId = entityType.Id,
            Version = 1,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
            IsActive = true,
            PublishedAt = now,
            PublishedById = actorUserId,
            ChangeSummary = "Operational UAT route; tenant-owned workflow definitions were preserved.",
            Configuration = JsonSerializer.Serialize(new
            {
                templateCode = template.Code,
                source = "operational-uat-procurement-policy-seeder",
                uatOnly = true
            }),
            CreatedAt = now,
            CreatedBy = SeedActor,
            CreatedById = actorUserId
        };
        definition.Steps.Add(CreateStep(
            submittedId, definitionId, tenantId, "Submitted", 1, true, false,
            WorkflowStepType.Manual, template.InitiatorRoleCode, now, actorUserId));
        definition.Steps.Add(CreateStep(
            approvalId, definitionId, tenantId, "Approval", 2, false, false,
            WorkflowStepType.Approval, template.ApprovalRoleCode, now, actorUserId));
        definition.Steps.Add(CreateStep(
            completedId, definitionId, tenantId, "Completed", 3, false, true,
            WorkflowStepType.Automatic, null, now, actorUserId));
        definition.Transitions.Add(CreateTransition(
            definitionId, tenantId, submittedId, approvalId, "Submit for approval", 1, now, actorUserId));
        definition.Transitions.Add(CreateTransition(
            definitionId, tenantId, approvalId, completedId, "Approve", 1, now, actorUserId));

        context.WorkflowDefinitions.Add(definition);
        await context.SaveChangesAsync(cancellationToken);
        return new WorkflowSeedOutcome(definition, true);
    }

    private static ProcurementPolicySet NewPolicy(
        Guid tenantId,
        Guid actorUserId,
        ProcurementConfigurationProfile sourceProfile,
        string code,
        string name,
        string description,
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PolicyKey = Guid.NewGuid(),
        Code = code,
        Name = name,
        Description = description,
        Version = 1,
        LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
        ScopeType = ProcurementPolicyScopeType.TenantBaseline,
        SourceConfigurationProfileId = sourceProfile.Id,
        SourceConfigurationProfile = sourceProfile,
        DefaultCurrencyCode = "GHS",
        EffectiveFrom = now.Date,
        ChangeSummary = "Fictional deployment-UAT baseline. Tenant-owned policy values are never reconciled in place.",
        IsDefault = false,
        PublishedAt = now,
        PublishedById = actorUserId,
        CreatedAt = now,
        CreatedBy = SeedActor,
        CreatedById = actorUserId
    };

    private static void AddCategoryMethodThreshold(
        ProcurementPolicySet policy,
        ProcurementCategoryClass category,
        string serviceClass,
        ProcurementMethodType method,
        Guid workflowDefinitionId,
        decimal lowerBound,
        decimal upperBound,
        int minimumQuotationCount,
        string statutoryReference,
        DateTime now)
    {
        policy.CategoryRules.Add(new ProcurementPolicyCategoryRule
        {
            Id = Guid.NewGuid(), TenantId = policy.TenantId,
            RuleCode = $"{policy.Code}-CATEGORY", Name = $"{serviceClass} coverage",
            Category = category, ServiceClass = serviceClass,
            Description = $"UAT category coverage for {serviceClass}.", RequiresSpecification = true,
            OverrideAction = ProcurementPolicyOverrideAction.Add, SourceDecisionKey = "DEC-001",
            Priority = 100, IsEnabled = true, EffectiveFrom = policy.EffectiveFrom,
            CreatedAt = now, CreatedBy = SeedActor, CreatedById = policy.CreatedById
        });
        policy.MethodRules.Add(new ProcurementPolicyMethodRule
        {
            Id = Guid.NewGuid(), TenantId = policy.TenantId,
            RuleCode = $"{policy.Code}-METHOD", Name = method.ToString(),
            Category = category, ServiceClass = serviceClass, Method = method,
            IsAllowed = true, RequiresCompetition = true, JustificationRequired = false,
            MinimumQuotationCount = minimumQuotationCount, WorkflowDefinitionId = workflowDefinitionId,
            ApplicabilityConditions = $"UAT {serviceClass} in GHS {lowerBound:N0}-{upperBound:N0}, inclusive.",
            OverrideAction = ProcurementPolicyOverrideAction.Add, SourceDecisionKey = "DEC-003",
            Priority = 100, IsEnabled = true, EffectiveFrom = policy.EffectiveFrom,
            CreatedAt = now, CreatedBy = SeedActor, CreatedById = policy.CreatedById
        });
        policy.ThresholdRules.Add(new ProcurementPolicyThresholdRule
        {
            Id = Guid.NewGuid(), TenantId = policy.TenantId,
            RuleCode = $"{policy.Code}-GHS-BAND", Name = $"GHS {lowerBound:N0}-{upperBound:N0} inclusive",
            Category = category, ServiceClass = serviceClass, Method = method, CurrencyCode = "GHS",
            LowerBound = lowerBound, UpperBound = upperBound, LowerInclusive = true, UpperInclusive = true,
            StatutoryReference = statutoryReference,
            OverrideAction = ProcurementPolicyOverrideAction.Add, SourceDecisionKey = "DEC-001",
            Priority = 100, IsEnabled = true, EffectiveFrom = policy.EffectiveFrom,
            CreatedAt = now, CreatedBy = SeedActor, CreatedById = policy.CreatedById
        });
    }

    private static void AddAuthority(
        ProcurementPolicySet policy,
        ProcurementCategoryClass category,
        ErpSystem.Core.Entities.ApplicationRole authorityRole,
        Guid workflowDefinitionId,
        decimal lowerBound,
        decimal upperBound,
        DateTime now) => policy.AuthorityRules.Add(new ProcurementPolicyAuthorityRule
    {
        Id = Guid.NewGuid(), TenantId = policy.TenantId,
        RuleCode = $"{policy.Code}-AUTHORITY", AuthorityName = "Managing Director",
        AuthorityRoleId = authorityRole.Id, AuthorityRole = authorityRole.Name!, Category = category,
        CurrencyCode = "GHS", LowerBound = lowerBound, UpperBound = upperBound,
        LowerInclusive = true, UpperInclusive = true, Sequence = 1, Quorum = 1,
        WorkflowDefinitionId = workflowDefinitionId,
        OverrideAction = ProcurementPolicyOverrideAction.Add, SourceDecisionKey = "DEC-002",
        Priority = 100, IsEnabled = true, EffectiveFrom = policy.EffectiveFrom,
        CreatedAt = now, CreatedBy = SeedActor, CreatedById = policy.CreatedById
    });

    private static void AddEvidence(
        ProcurementPolicySet policy,
        ProcurementCategoryClass category,
        ProcurementMethodType method,
        ProcurementEvidenceStage stage,
        string evidenceName,
        string decisionKey,
        int priority,
        DateTime now) => policy.EvidenceRules.Add(new ProcurementPolicyEvidenceRule
    {
        Id = Guid.NewGuid(), TenantId = policy.TenantId,
        RuleCode = $"{policy.Code}-EVID-{stage.ToString().ToUpperInvariant()}", EvidenceName = evidenceName,
        Stage = stage, Category = category, Method = method,
        SharedRequirementKey = $"{policy.Code}:{stage}", IsMandatory = true, RequiresVerification = true,
        OverrideAction = ProcurementPolicyOverrideAction.Add, SourceDecisionKey = decisionKey,
        Priority = priority, IsEnabled = true, EffectiveFrom = policy.EffectiveFrom,
        CreatedAt = now, CreatedBy = SeedActor, CreatedById = policy.CreatedById
    });

    private static void AddSod(
        ProcurementPolicySet policy,
        ErpSystem.Core.Entities.ApplicationRole initiatorRole,
        ErpSystem.Core.Entities.ApplicationRole conflictingRole,
        string entityType,
        string action,
        DateTime now) => policy.SodRules.Add(new ProcurementPolicySodRule
    {
        Id = Guid.NewGuid(), TenantId = policy.TenantId,
        RuleCode = $"{policy.Code}-SOD", Name = "Independent requisition approval",
        InitiatorRoleId = initiatorRole.Id, InitiatorRole = initiatorRole.Name!,
        ConflictingRoleId = conflictingRole.Id, ConflictingRole = conflictingRole.Name!,
        EntityType = entityType, Action = action, Enforcement = ProcurementSodEnforcement.HardStop,
        Explanation = "The UAT requisition initiator must not approve the same request.",
        OverrideAction = ProcurementPolicyOverrideAction.Add, SourceDecisionKey = "DEC-004",
        Priority = 100, IsEnabled = true, EffectiveFrom = policy.EffectiveFrom,
        CreatedAt = now, CreatedBy = SeedActor, CreatedById = policy.CreatedById
    });

    private static WorkflowStep CreateStep(
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
        AssignmentConfiguration = requiredRole is null ? null : JsonSerializer.Serialize(new { role = requiredRole }),
        Configuration = stepType == WorkflowStepType.Approval && requiredRole is not null
            ? CreateApprovalStepConfiguration(requiredRole)
            : null,
        CreatedAt = now,
        CreatedBy = SeedActor,
        CreatedById = actorUserId
    };

    private static string CreateApprovalStepConfiguration(string approvalRoleCode) =>
        JsonSerializer.Serialize(new WorkflowStepConfigurationDto
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
                        Role = approvalRoleCode,
                        Priority = 100
                    }
                ]
            }
        });

    private static WorkflowTransition CreateTransition(
        Guid definitionId,
        Guid tenantId,
        Guid fromStepId,
        Guid toStepId,
        string name,
        int priority,
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
        Priority = priority,
        CreatedAt = now,
        CreatedBy = SeedActor,
        CreatedById = actorUserId
    };

    private sealed record RequiredWorkflow(string TemplateCode, string SeedName);
    private sealed record WorkflowSeedOutcome(WorkflowDefinition Definition, bool Created);
}

public sealed record ProcurementUatPolicySeedResult(int CreatedPolicies, int CreatedWorkflows);
