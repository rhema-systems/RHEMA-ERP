using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using WorkflowOwner = ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService;

namespace ErpSystem.Api.Services;

/// <summary>
/// Explicit test-environment preparation. Configurations are authored as the real bootstrap
/// identity; by default QS decisions remain drafts for independent, evidence-backed owner
/// approval. An explicit target-guarded auto-approval switch is available for the isolated
/// UAT database only. No project, contract, BOQ, certificate, stock or financial transaction
/// is created.
/// </summary>
public sealed class QsUatPreparationSeeder(
    ApplicationDbContext db,
    ICurrentUserService actor,
    WorkflowOwner workflowOwner,
    IQuantitySurveyConfigurationService configurationOwner,
    QuantitySurveyConfigurationProfileSeeder profileSeeder,
    QuantitySurveyStatutoryReportSeeder reportSeeder,
    IReportTemplateLifecycleService reportTemplateOwner,
    IConfiguration configuration)
{
    internal const string SeedMarker = "QS-UAT-PREPARATION-V1";
    internal const string AutoApprovalMarker = "QS-UAT-AUTO-APPROVAL-V1";
    private const string SeedName = "QS UAT bootstrap";
    internal static readonly string[] RequiredActors =
        ["uat.qs.preparer", "uat.qs.reviewer", "uat.qs.engineer", "financereviewer", "uat.qs.approver", "procurementapprover"];

    public async Task<QsUatPreparationResult> PrepareAsync(string expectedDatabase, bool testMode,
        IReadOnlyDictionary<string, Guid>? preparedReferences = null, CancellationToken token = default)
    {
        if (!db.Database.IsSqlServer()) throw new InvalidOperationException("QS UAT preparation requires SQL Server.");
        ValidateTarget(expectedDatabase, db.Database.GetDbConnection().Database, testMode);
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId || tenantId == Guid.Empty ||
            !Guid.TryParse(actor.UserId, out var actorId) || actorId == Guid.Empty ||
            !actor.IsInRole("SuperAdmin") || actor.UserName != "qs.uat.bootstrap")
            throw new InvalidOperationException("QS UAT preparation requires the dedicated CLI bootstrap identity and tenant context.");
        if (!await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == actorId && u.TenantId == tenantId && u.UserName == "qs.uat.bootstrap", token))
            throw new InvalidOperationException("The bootstrap identity must be a persisted user in the target tenant.");
        if ((await db.Database.GetPendingMigrationsAsync(token)).Any())
            throw new InvalidOperationException("Apply all migrations before QS UAT preparation.");

        var users = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && RequiredActors.Contains(u.UserName!))
            .ToDictionaryAsync(u => u.UserName!, u => u.Id, token);
        ValidateActors(users);
        var missingMemberships = await db.UserTenants.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.Status == ErpSystem.Core.Entities.UserTenantStatus.Active &&
                (!m.ExpiresAt.HasValue || m.ExpiresAt > DateTime.UtcNow))
            .Select(m => m.UserId).ToListAsync(token);
        if (users.Values.Any(id => !missingMemberships.Contains(id)))
            throw new InvalidOperationException("Every QS UAT workflow actor needs active DEFAULT tenant membership.");

        var workflows = new Dictionary<string, Guid>();
        foreach (var entity in QuantitySurveyWorkflowBindingRegistry.EntityTypes)
        {
            var recipe = CreateWorkflowRecipe(entity.Code, entity.Name, users);
            var existing = await db.WorkflowDefinitions.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(w => w.TenantId == tenantId && w.Name == recipe.Name, token);
            if (existing is not null && (existing.IsDeleted || existing.Configuration != recipe.Configuration ||
                existing.CreatedById != actorId || existing.Version != 1))
                throw new InvalidOperationException($"UAT workflow '{recipe.Name}' conflicts with existing configuration; no replacement is permitted.");
            if (existing is not null)
            {
                var graph = await workflowOwner.GetWorkflowDefinitionAsync(existing.Id)
                    ?? throw new InvalidOperationException("Existing UAT workflow could not be read.");
                ValidateExistingWorkflow(graph, recipe);
            }
            var definition = existing ?? await workflowOwner.CreateWorkflowDefinitionAsync(recipe);
            if (definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published || !definition.IsActive)
            {
                if (definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Draft)
                    throw new InvalidOperationException($"UAT workflow '{recipe.Name}' was retired; review it instead of republishing.");
                definition = await workflowOwner.PublishWorkflowDefinitionAsync(definition.Id, actorId);
            }
            workflows.Add(entity.Code, definition.Id);
        }

        await profileSeeder.SeedTenantAsync(tenantId, actorId, token);
        await reportSeeder.SeedTenantAsync(tenantId, token);
        var templates = await PrepareMetadataTemplatesAsync(tenantId, actorId, token);
        var reportTemplates = await PrepareReportTemplatesAsync(tenantId, actorId, token);
        var autoApprove = configuration.GetValue<bool>("QsUat:AutoApprove");
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.ProfileCode == "TDC-QUANTITY-SURVEY" &&
                p.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Draft)
            .SingleOrDefaultAsync(token);
        if (profile is null)
        {
            if (autoApprove)
            {
                var published = await db.QuantitySurveyConfigurationProfiles.AsNoTracking()
                    .SingleOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted &&
                        p.ProfileCode == "TDC-QUANTITY-SURVEY" && p.IsDefault &&
                        p.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published &&
                        p.EffectiveFrom <= DateTime.UtcNow && (!p.EffectiveTo.HasValue || p.EffectiveTo >= DateTime.UtcNow), token)
                    ?? throw new InvalidOperationException("QS_UAT_AUTO_APPROVAL_NO_PROFILE: no seeded draft or current published profile exists.");
                var verified = await configurationOwner.GetProfileAsync(published.Id, token);
                if (!verified.Validation.IsValid || verified.Decisions.Count != QuantitySurveyConfigurationDecisionRegistry.Definitions.Count ||
                    verified.Decisions.Any(d => d.ApprovalReference != AutoApprovalMarker || !d.IsComplete))
                    throw new InvalidOperationException("QS_UAT_AUTO_APPROVAL_EXISTING_PROFILE: existing configuration was preserved; it is not a complete UAT auto-approved profile.");
                return new(workflows, published.Id, verified.Decisions.Select(d => d.DecisionKey).ToArray(), [], [], false);
            }
            return new(workflows, null, [], ["No editable QS draft. Existing published/historical configuration was preserved."], [], true);
        }

        var lookup = (await configurationOwner.GetLookupsAsync(token)).Sources;
        var references = ResolveReferences(lookup, workflows, templates, reportTemplates, profile.EffectiveFrom);
        if (preparedReferences is not null)
            foreach (var reference in preparedReferences)
                references[reference.Key] = reference.Value.ToString();
        var prepared = new List<string>();
        var unresolved = new List<string>();
        var proposals = new List<QsUatDecisionProposal>();
        var current = await configurationOwner.GetProfileAsync(profile.Id, token);
        // The bootstrap identity has no password and cannot be used through the UI.
        // This distinguishes a stale value written by an earlier UAT run from a
        // real user's draft, even when the original profile came from startup seed data.
        var bootstrapModifiedDecisionIds = (await db.QuantitySurveyConfigurationDecisions.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.ProfileId == profile.Id && !d.IsDeleted &&
                d.SourceLineage == SeedMarker && d.LastModifiedById == actorId)
            .Select(d => d.Id).ToListAsync(token)).ToHashSet();
        foreach (var decision in current.Decisions.OrderBy(d => d.DecisionKey))
        {
            var resolved = QsUatDecisionRecipes.Resolve(decision.DecisionKey, references);
            proposals.Add(new(decision.DecisionKey, resolved.Value, resolved.Missing));
            if (resolved.Value is null) { unresolved.Add($"{decision.DecisionKey}: missing controlled selection {string.Join(", ", resolved.Missing)}"); continue; }
            var validation = QuantitySurveyConfigurationDecisionRegistry.Validate(decision.DecisionKey, 1, resolved.Value.Value);
            if (!validation.IsValid) { unresolved.Add($"{decision.DecisionKey}: {string.Join("; ", validation.Errors)}"); continue; }
            if (decision.SourceLineage == SeedMarker && System.Text.Json.Nodes.JsonNode.DeepEquals(
                System.Text.Json.Nodes.JsonNode.Parse(decision.Value.GetRawText()), System.Text.Json.Nodes.JsonNode.Parse(validation.CanonicalJson!)))
            { prepared.Add(decision.DecisionKey); continue; }
            if (!CanPrepareDecision(decision) &&
                !CanReconcileSeedDecision(decision, bootstrapModifiedDecisionIds.Contains(decision.Id)))
            {
                unresolved.Add($"{decision.DecisionKey}: existing user preparation or approval preserved; compare the UAT proposal manually.");
                continue;
            }
            await configurationOwner.SaveDecisionAsync(profile.Id, decision.DecisionKey, new SaveQuantitySurveyDecisionRequest
            {
                Value = resolved.Value.Value, SchemaVersion = 1, RowVersion = decision.RowVersion,
                SourceLineage = SeedMarker,
                Notes = "Explicitly authorized test-only configuration proposal. No business approval, signed review or published evidence is asserted.",
                Reason = "Prepare the VPS QS UAT configuration for genuine independent owner review."
            }, $"qs-uat-prepare-{profile.Id:N}-{decision.DecisionKey}", token);
            prepared.Add(decision.DecisionKey);
        }
        if (autoApprove)
        {
            if (unresolved.Count > 0)
                throw new InvalidOperationException(
                    "QS_UAT_AUTO_APPROVAL_UNRESOLVED: all 17 decisions must have valid controlled values before auto-approval. " +
                    string.Join(" | ", unresolved));
            await AutoApproveAndPublishAsync(profile.Id, tenantId, actorId, token);
        }
        return new(workflows, profile.Id, prepared, unresolved, proposals, !autoApprove);
    }

    internal Task AutoApproveAndPublishAsync(Guid profileId, Guid tenantId, Guid actorId,
        CancellationToken token) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        var database = db.Database.GetDbConnection().Database;
        ValidateTarget(configuration["QsUat:ExpectedDatabase"] ?? "", database,
            configuration.GetValue<bool>("QsUat:Enabled") && configuration.GetValue<bool>("QsUat:AutoApprove"));
        if (!actor.IsAuthenticated || actor.TenantId != tenantId || actor.UserName != "qs.uat.bootstrap" ||
            !Guid.TryParse(actor.UserId, out var currentActor) || currentActor != actorId || !actor.IsInRole("SuperAdmin"))
            throw new InvalidOperationException("QS_UAT_AUTO_APPROVAL_ACTOR: only the dedicated bootstrap identity can run this UAT operation.");

        // Re-query on retry; evidence, approvals and owner-controlled publication
        // either commit together or all roll back.
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token);
        var profile = await db.QuantitySurveyConfigurationProfiles
            .SingleAsync(value => value.TenantId == tenantId && value.Id == profileId && !value.IsDeleted, token);
        if (profile.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published)
        {
            var existing = await configurationOwner.GetProfileAsync(profileId, token);
            if (!existing.Validation.IsValid || existing.Decisions.Count != QuantitySurveyConfigurationDecisionRegistry.Definitions.Count ||
                existing.Decisions.Any(d => d.ApprovalReference != AutoApprovalMarker || !d.IsComplete))
                throw new InvalidOperationException("QS_UAT_AUTO_APPROVAL_EXISTING_PROFILE: existing configuration was preserved.");
            return;
        }
        if (profile.LifecycleStatus != QuantitySurveyConfigurationProfileStatus.Draft)
            throw new InvalidOperationException("QS_UAT_AUTO_APPROVAL_PROFILE_STATE: only the seeded draft profile may be auto-approved.");

        var decisions = await db.QuantitySurveyConfigurationDecisions
            .Where(value => value.TenantId == tenantId && value.ProfileId == profileId && !value.IsDeleted)
            .OrderBy(value => value.DecisionKey)
            .ToListAsync(token);
        ValidateAutoApprovalDecisions(decisions, actorId, (await configurationOwner.GetLookupsAsync(token)).Sources);
        if (await db.QuantitySurveyConfigurationEvidenceLinks.AnyAsync(e => e.TenantId == tenantId &&
                e.ProfileId == profileId && !e.IsDeleted, token))
            throw new InvalidOperationException("QS_UAT_AUTO_APPROVAL_EXISTING_EVIDENCE: existing review evidence was preserved.");

        var now = DateTime.UtcNow;
        var reference = $"QS-UAT-CONFIG-{profile.Id:N}";
        var record = await db.CentralDocumentRecords.SingleOrDefaultAsync(value =>
            value.TenantId == tenantId && value.DocumentReference == reference && !value.IsDeleted, token);
        if (record is null)
        {
            record = new CentralDocumentRecord
            {
                Id = Guid.NewGuid(), TenantId = tenantId, DocumentReference = reference,
                Title = "QS UAT system auto-approval memorandum", SourceModule = "QuantitySurvey",
                SourceLabel = AutoApprovalMarker, SourceEntityType = "QuantitySurveyConfigurationProfile",
                SourceRecordReference = $"{profile.ProfileCode}/v{profile.Version}", SourceRecordId = profile.Id,
                RepositoryStatus = "Not linked", CurrentVersion = "v1.0", VersionStatus = "Published",
                LifecycleStatus = "Active", RetentionStatus = "Current", PublishedAt = now,
                PublishedById = actorId, Notes = "Metadata-only system memorandum recording the operator's explicit -AutoApproveQsUat authorization for all 17 seeded decisions on " + database + ". This is UAT setup, not independent business sign-off. Decision values and approval states are retained in the QS audit revisions. No uploaded or signed file is asserted.",
                CreatedAt = now, CreatedBy = "QS UAT bootstrap", CreatedById = actorId
            };
            db.CentralDocumentRecords.Add(record);
            db.CentralDocumentVersions.Add(new CentralDocumentVersion
            {
                Id = Guid.NewGuid(), TenantId = tenantId, DocumentRecordId = record.Id,
                VersionNumber = "v1.0", Status = "Published",
                CreatedByUserId = actorId, PublishedAt = now, PublishedById = actorId,
                ChangeSummary = "System-generated UAT authorization memorandum. Explicit operator opt-in; not a human reviewer signature.",
                CreatedAt = now, CreatedBy = SeedName, CreatedById = actorId
            });
            await db.SaveChangesAsync(token);
        }
        if (record.SourceLabel != AutoApprovalMarker || record.SourceRecordId != profileId ||
            record.CreatedById != actorId || record.LifecycleStatus != "Active" || record.VersionStatus != "Published")
            throw new InvalidOperationException("QS_UAT_AUTO_APPROVAL_EVIDENCE_CONFLICT: existing document was preserved.");
        var version = await db.CentralDocumentVersions.SingleAsync(value =>
            value.TenantId == tenantId && value.DocumentRecordId == record.Id &&
            value.VersionNumber == record.CurrentVersion && !value.IsDeleted && value.Status == "Published", token);

        foreach (var decision in decisions)
        {
            var evidence = await db.QuantitySurveyConfigurationEvidenceLinks.AnyAsync(value =>
                value.TenantId == tenantId && value.ProfileId == profile.Id && value.DecisionId == decision.Id &&
                value.CentralDocumentRecordId == record.Id && value.CentralDocumentVersionId == version.Id && !value.IsDeleted, token);
            if (!evidence)
            {
                db.QuantitySurveyConfigurationEvidenceLinks.Add(new QuantitySurveyConfigurationEvidenceLink
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, ProfileId = profile.Id, DecisionId = decision.Id,
                    CentralDocumentRecordId = record.Id, CentralDocumentVersionId = version.Id,
                    EvidenceType = "Approval memorandum", ExternalReference = reference,
                    LinkedById = actorId, LinkedAt = now, CreatedAt = now,
                    CreatedBy = "QS UAT bootstrap", CreatedById = actorId
                });
                db.QuantitySurveyConfigurationRevisions.Add(new QuantitySurveyConfigurationRevision
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, ProfileId = profile.Id, DecisionId = decision.Id,
                    Action = QuantitySurveyAuditEventMap.LinkEvidence, Result = "Succeeded",
                    CorrelationId = $"qs-uat-auto-evidence-{decision.DecisionKey.ToLowerInvariant()}",
                    ActorUserId = actorId, ActorName = "QS UAT bootstrap", ActorRoles = "SuperAdmin",
                    Reason = "Explicit test-only auto-approval evidence.", AfterJson = $"{{\"decisionKey\":\"{decision.DecisionKey}\",\"evidence\":\"{reference}\"}}",
                    CreatedAt = now, CreatedBy = "QS UAT bootstrap", CreatedById = actorId
                });
            }

            var alreadyApproved = decision.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
                decision.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
                decision.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified &&
                decision.ApprovalReference == AutoApprovalMarker;
            if (!alreadyApproved)
            {
                var before = ApprovalSnapshot(decision);
                decision.Status = QuantitySurveyConfigurationDecisionStatus.Approved;
                decision.ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Approved;
                decision.EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Verified;
                decision.DecisionDate ??= now;
                decision.EffectiveFrom ??= profile.EffectiveFrom;
                decision.ApprovedById = actorId;
                decision.ApprovedAt = now;
                decision.ApprovalReference = AutoApprovalMarker;
                decision.SourceLineage = AutoApprovalMarker;
                decision.Notes = "Explicit test-only auto-approval authorized for the isolated QS UAT database.";
                decision.UpdatedAt = now; decision.UpdatedBy = "QS UAT bootstrap"; decision.LastModifiedById = actorId;
                db.QuantitySurveyConfigurationRevisions.Add(new QuantitySurveyConfigurationRevision
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, ProfileId = profile.Id, DecisionId = decision.Id,
                    Action = QuantitySurveyAuditEventMap.ApproveDecision, Result = "Succeeded",
                    CorrelationId = $"qs-uat-auto-approve-{decision.DecisionKey.ToLowerInvariant()}",
                    ActorUserId = actorId, ActorName = "QS UAT bootstrap", ActorRoles = "SuperAdmin",
                    Reason = "Explicit test-only auto-approval authorized by the UAT owner.",
                    BeforeJson = before, AfterJson = ApprovalSnapshot(decision),
                    CreatedAt = now, CreatedBy = "QS UAT bootstrap", CreatedById = actorId
                });
            }
        }

        await db.SaveChangesAsync(token);
        await configurationOwner.PublishProfileAsync(profileId, new QuantitySurveyLifecycleRequest
        {
            RowVersion = Convert.ToBase64String(profile.RowVersion),
            Reason = "Explicit test-only auto-publication authorized by the UAT operator: " + AutoApprovalMarker
        }, $"qs-uat-auto-publish-{profile.Id:N}", token);
        await transaction.CommitAsync(token);
    });

    internal static void ValidateAutoApprovalDecisions(IReadOnlyList<QuantitySurveyConfigurationDecision> decisions,
        Guid actorId, IReadOnlyDictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>> lookups)
    {
        var required = QuantitySurveyConfigurationDecisionRegistry.Definitions.Select(d => d.DecisionKey).ToHashSet();
        if (decisions.Count != required.Count || !required.SetEquals(decisions.Select(d => d.DecisionKey)))
            throw new InvalidOperationException("QS_UAT_AUTO_APPROVAL_DECISION_COUNT: the seeded profile must contain all 17 decisions exactly once.");
        foreach (var decision in decisions)
        {
            if (decision.SourceLineage != SeedMarker || decision.LastModifiedById != actorId ||
                decision.Status != QuantitySurveyConfigurationDecisionStatus.Draft ||
                decision.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Pending ||
                decision.ApprovedById.HasValue || decision.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Missing)
                throw new InvalidOperationException($"QS_UAT_AUTO_APPROVAL_USER_OWNED: {decision.DecisionKey} was changed or reviewed and was preserved.");
            var value = QuantitySurveyConfigurationDecisionRegistry.ParseValue(decision.ValueJson);
            var validation = QuantitySurveyConfigurationDecisionRegistry.Validate(decision.DecisionKey, decision.SchemaVersion, value);
            if (!validation.IsValid)
                throw new InvalidOperationException($"QS_UAT_AUTO_APPROVAL_INVALID_VALUE: {decision.DecisionKey} is not valid.");
            foreach (var field in QuantitySurveyConfigurationDecisionRegistry.GetRequired(decision.DecisionKey).Fields.Where(f => f.LookupSource != null))
            {
                if (!value.TryGetProperty(field.Name, out var selected)) continue;
                var allowed = lookups.TryGetValue(field.LookupSource!, out var options)
                    ? options.Where(o => field.LookupGroup == null || string.Equals(o.Group, field.LookupGroup, StringComparison.OrdinalIgnoreCase))
                        .Select(o => o.Value).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
                var values = selected.ValueKind == JsonValueKind.Array ? selected.EnumerateArray().Select(v => v.GetString() ?? "") : [selected.GetString() ?? ""];
                if (values.Any(v => !allowed.Contains(v)))
                    throw new InvalidOperationException($"QS_UAT_AUTO_APPROVAL_STALE_SELECTION: {decision.DecisionKey} contains an inactive, missing or cross-tenant selection.");
            }
        }
    }

    private static string ApprovalSnapshot(QuantitySurveyConfigurationDecision decision) => JsonSerializer.Serialize(new
    {
        decision.DecisionKey, decision.Status, decision.ApprovalStatus, decision.EvidenceStatus,
        Value = QuantitySurveyConfigurationDecisionRegistry.ParseValue(decision.ValueJson), decision.ApprovedById,
        decision.ApprovedAt, decision.ApprovalReference, decision.SourceLineage, decision.EffectiveFrom, decision.EffectiveTo
    });

    internal static void ValidateTarget(string expected, string actual, bool testMode)
    {
        QsUatActorSeeder.ValidateTarget(actual, expected, "Test", testMode);
    }

    internal static void ValidateActors(IReadOnlyDictionary<string, Guid> users)
    {
        var missing = RequiredActors.Where(name => !users.TryGetValue(name, out var id) || id == Guid.Empty).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Missing active QS UAT actors: " + string.Join(", ", missing));
        if (RequiredActors.Select(name => users[name]).Distinct().Count() != RequiredActors.Length)
            throw new InvalidOperationException("QS UAT preparation, review, engineering, Finance, procurement and final approval must use distinct actors.");
    }

    internal static bool CanPrepareDecision(QuantitySurveyDecisionDto decision) =>
        decision.Status == QuantitySurveyConfigurationDecisionStatus.Draft &&
        decision.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Pending &&
        decision.Evidence.Count == 0 &&
        (decision.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
         decision.Value.ValueKind == JsonValueKind.Object && !decision.Value.EnumerateObject().Any());

    internal static bool CanReconcileSeedDecision(QuantitySurveyDecisionDto decision, bool bootstrapModified) =>
        bootstrapModified && decision.SourceLineage == SeedMarker &&
        decision.Status == QuantitySurveyConfigurationDecisionStatus.Draft &&
        decision.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Pending &&
        decision.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Missing &&
        decision.Evidence.Count == 0;

    internal static CreateWorkflowDefinitionDto CreateWorkflowRecipe(string code, string name, IReadOnlyDictionary<string, Guid> users)
    {
        ValidateActors(users);
        var stages = code switch
        {
            "QS_VALUATION" or "QS_PAYMENT_CERTIFICATE" => new[]
            { ("QS review", "uat.qs.reviewer"), ("Engineering and project confirmation", "uat.qs.engineer"),
              ("Finance validation", "financereviewer"), ("Final independent approval", "uat.qs.approver") },
            "QS_VARIATION" => new[]
            { ("Engineer source confirmation", "uat.qs.engineer"), ("QS valuation", "uat.qs.reviewer"),
              ("Procurement contract review", "procurementapprover"), ("Finance budget validation", "financereviewer"),
              ("Final authority approval", "uat.qs.approver") },
            _ => new[] { ("Technical review", "uat.qs.reviewer"), ("Independent approval", "uat.qs.approver") }
        };
        var dto = new CreateWorkflowDefinitionDto
        {
            Name = $"QS UAT v1 - {name}", EntityType = code,
            Description = "Explicit test-only configuration. Performs no approvals or transaction creation during setup.",
            Configuration = JsonSerializer.Serialize(new { seed = SeedMarker, testOnly = true, entityType = code,
                stageActors = stages.Select(s => new { stage = s.Item1, userId = users[s.Item2] }) })
        };
        for (var i = 0; i < stages.Length; i++)
        {
            dto.Steps.Add(new CreateWorkflowStepDto
            {
                Id = Guid.NewGuid(), Name = stages[i].Item1, Order = i + 1, StepType = WorkflowStepType.Approval,
                IsRequired = true, EstimatedHours = 24,
                Configuration = new WorkflowStepConfigurationDto { ApprovalConfig = new WorkflowApprovalConfigDto
                {
                    ApprovalType = WorkflowApprovalType.Single, MinApprovalsRequired = 1,
                    PreventInitiatorApproval = true, RequireDistinctApprovers = true,
                    ApproverRules = [new WorkflowAssignmentRuleDto { AssignmentType = WorkflowAssignmentType.User, UserId = users[stages[i].Item2] }],
                    ConflictRules = i == 0 ? [] : [new WorkflowApprovalConflictRuleDto
                    { Id = $"qs-uat-{code}-{i}", Name = "Independent stage performer", ActorSource = WorkflowApprovalActorSource.AnyPreviousApprover,
                        Message = "A previous approver cannot complete this independent QS stage." }]
                } }
            });
            if (i > 0) dto.Transitions.Add(new CreateWorkflowTransitionDto
            { FromStepId = dto.Steps[i - 1].Id!.Value, ToStepId = dto.Steps[i].Id!.Value, Name = "Continue", IsDefault = true });
        }
        return dto;
    }

    private static void ValidateExistingWorkflow(ErpSystem.Core.Entities.Workflow.WorkflowDefinition existing, CreateWorkflowDefinitionDto expected)
    {
        var steps = existing.Steps.Where(s => !s.IsDeleted).OrderBy(s => s.Order).ToArray();
        if (steps.Length != expected.Steps.Count || steps.Where((step, index) => step.Name != expected.Steps[index].Name ||
            step.StepType != WorkflowStepType.Approval || !step.IsRequired).Any())
            throw new InvalidOperationException($"UAT workflow '{existing.Name}' was changed; it will not be overwritten.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        for (var i = 0; i < steps.Length; i++)
        {
            var actual = JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(steps[i].Configuration ?? "{}", options)?.ApprovalConfig;
            var wanted = expected.Steps[i].Configuration!.ApprovalConfig!;
            if (actual is null || !actual.PreventInitiatorApproval || !actual.RequireDistinctApprovers ||
                actual.ApproverRules.Count != 1 || actual.ApproverRules[0].UserId != wanted.ApproverRules[0].UserId ||
                actual.ApproverRules[0].AssignmentType != WorkflowAssignmentType.User || actual.AutoApprovalCondition is not null ||
                actual.ConflictRules.Count != wanted.ConflictRules.Count)
                throw new InvalidOperationException($"UAT workflow '{existing.Name}' performer or separation controls were changed.");
        }
    }

    private async Task<Dictionary<string, Guid>> PrepareMetadataTemplatesAsync(Guid tenantId, Guid actorId, CancellationToken token)
    {
        var values = new[]
        {
            ("MeasurementTemplate", "QS-UAT-MEAS-EVD", "QS measurement evidence", new[] { "measurementSheetId", "projectId", "boqLineKey", "evidenceType", "checksumSha256" }),
            ("ValuationDmsTemplate", "QS-UAT-VAL-EVD", "QS valuation evidence", new[] { "valuationWorksheetId", "projectId", "interimValuationId", "evidenceType", "checksumSha256" }),
            ("CertificateDmsTemplate", "QS-UAT-CERT-EVD", "PaymentCertificate", new[] { "projectId", "valuationWorksheetId", "certificateNumber", "checksumSha256" }),
            ("VariationDmsTemplate", "QS-UAT-VAR-EVD", "Variation Evidence", new[] { "variationOrderId", "projectId", "checksumSha256" })
        };
        var result = new Dictionary<string, Guid>();
        foreach (var (key, code, type, fields) in values)
        {
            var existing = await db.CentralDocumentMetadataTemplates.IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.TenantId == tenantId && t.TemplateCode == code, token);
            var json = JsonSerializer.Serialize(fields);
            if (existing is not null && (existing.IsDeleted || !existing.IsActive || existing.CreatedById != actorId ||
                existing.SourceLabel != SeedMarker || existing.RequiredFieldsJson != json || existing.DocumentType != type ||
                existing.Module != "QuantitySurvey" || !existing.PublishedAt.HasValue))
                throw new InvalidOperationException($"Metadata template '{code}' conflicts with user-owned configuration.");
            if (existing is null)
            {
                existing = new CentralDocumentMetadataTemplate
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, TemplateCode = code, Module = "QuantitySurvey", DocumentType = type,
                    SourceLabel = SeedMarker, RequiredFieldsJson = json, RelationshipsJson = "[\"Project\",\"Contract\",\"Workflow\"]",
                    RetentionRule = "UAT evidence; retain until the test owner authorizes disposal", AccessProfile = "QS project and audit scope",
                    IsActive = true, PublishedAt = DateTime.UtcNow, PublishedById = actorId,
                    CreatedAt = DateTime.UtcNow, CreatedBy = SeedName, CreatedById = actorId
                };
                // Metadata schema publication is setup, not publication of document content or signed evidence.
                db.CentralDocumentMetadataTemplates.Add(existing);
                await db.SaveChangesAsync(token);
            }
            result.Add(key, existing.Id);
        }
        return result;
    }

    private async Task<Dictionary<string, Guid>> PrepareReportTemplatesAsync(Guid tenantId, Guid actorId, CancellationToken token)
    {
        var result = new Dictionary<string, Guid>();
        foreach (var (key, code, reportCode) in new[]
        { ("ValuationTemplate", "QS-UAT-VALUATION", "valuation-statement"), ("CertificateTemplate", "QS-UAT-CERTIFICATE", "certificate-register") })
        {
            var report = await db.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.TenantId == tenantId && !r.IsDeleted &&
                r.Type == "quantity-survey" && r.Query != null && r.Query.EndsWith(reportCode), token);
            if (report is null) continue;
            var existing = await db.ReportTemplates.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.TemplateKey == code, token);
            if (existing is not null && (existing.IsDeleted || existing.CreatedBy != actorId.ToString() || existing.ReportId != report.Id ||
                existing.Description != SeedMarker || existing.Version != 1 || existing.Status is not ("Draft" or "Published")))
                throw new InvalidOperationException($"Report template '{code}' conflicts with existing configuration.");
            var template = existing is null
                ? await reportTemplateOwner.CreateAsync(new CreateReportTemplateDto
                {
                    ReportId = report.Id, TemplateKey = code, Name = "QS UAT " + report.Name, Description = SeedMarker,
                    Category = "QuantitySurvey", Type = "Table", Audience = "Finance", Cadence = "AdHoc",
                    DefaultOutputFormat = "Online", OutputFormats = ["Online", "PDF", "XLSX"]
                }, tenantId, actorId, true, token)
                : (await reportTemplateOwner.GetByIdAsync(existing.Id, tenantId, actorId, true, token))!;
            if (template.Status == "Draft") template = await reportTemplateOwner.PublishAsync(template.Id,
                new ReportTemplateLifecycleActionDto { RowVersion = template.RowVersion }, tenantId, actorId, true, token);
            result.Add(key, template.Id);
        }
        return result;
    }

    internal static Dictionary<string, string> ResolveReferences(IReadOnlyDictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>> lookups,
        IReadOnlyDictionary<string, Guid> workflows, IReadOnlyDictionary<string, Guid> templates,
        IReadOnlyDictionary<string, Guid> reportTemplates, DateTime effectiveFrom)
    {
        var result = templates.Concat(reportTemplates).ToDictionary(p => p.Key, p => p.Value.ToString());
        result["From"] = effectiveFrom.ToString("yyyy-MM-dd");
        foreach (var (key, code) in new[] { ("BoqWorkflow", "QS_BOQ"), ("EstimateWorkflow", "QS_ESTIMATE"),
            ("EscalationWorkflow", "QS_ESCALATION"), ("MeasurementWorkflow", "QS_MEASUREMENT"), ("ValuationWorkflow", "QS_VALUATION"),
            ("CertificateWorkflow", "QS_PAYMENT_CERTIFICATE"), ("RetentionWorkflow", "QS_RETENTION_RELEASE"),
            ("MaterialWorkflow", "QS_MATERIAL_DEDUCTION"), ("VariationWorkflow", "QS_VARIATION"), ("ClaimWorkflow", "QS_CLAIM"),
            ("SubcontractWorkflow", "QS_SUBCONTRACT"), ("FinalWorkflow", "QS_FINAL_ACCOUNT") })
            if (workflows.TryGetValue(code, out var id)) result[key] = id.ToString();
        Select("OfficerRoleId", "roles", "TDC_QUANTITY_SURVEYOR");
        Select("ApproverRoleId", "roles", "TDC_SUPERVISING_QUANTITY_SURVEYOR");
        Select("OversightRoleId", "roles", "TDC_INTERNAL_AUDIT");
        Select("ProjectTypeId", "projectTypes", "QS-UAT-CONSTRUCTION");
        Select("LocationId", "locations", "QS-UAT-SITE");
        // Finance stores the seeded natural account numbers as 5000/2000. Some
        // segmented displays add the DEFAULT prefix, so accept either exact
        // controlled label without falling back across multiple accounts.
        Select("ExpenseAccount", "postingExpenseAccounts", "DEFAULT-5000", "5000");
        Select("ApAccount", "accountsPayableAccounts", "DEFAULT-2000", "2000");
        Select("PaymentTerm", "supplierPaymentTerms", "NET30");
        Select("TaxGroup", "supplierTaxGroups", "VAT-STD-PURCHASES", "GH-PURCH-STD");
        Select("WithholdingTax", "supplierWithholdingTaxes", "WHT-WORKS");
        if (lookups.TryGetValue("reports", out var reports))
        {
            var ids = reports.Where(r => r.Group == "quantity-survey").Select(r => r.Value).ToArray();
            if (ids.Length > 0) result["ReportIds"] = JsonSerializer.Serialize(ids);
        }
        return result;

        void Select(string key, string source, params string[] labels)
        {
            if (!lookups.TryGetValue(source, out var options)) return;
            foreach (var label in labels)
            {
                var matching = options.Where(o => o.Label.Equals(label, StringComparison.OrdinalIgnoreCase) ||
                    o.Label.StartsWith(label + " - ", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matching.Length == 1) { result[key] = matching[0].Value; return; }
                if (matching.Length > 1) return;
            }
            if (options.Count == 1) result[key] = options[0].Value;
        }
    }
}

public sealed record QsUatDecisionProposal(string DecisionKey, JsonElement? Value, IReadOnlyList<string> MissingReferences);
public sealed record QsUatPreparationResult(IReadOnlyDictionary<string, Guid> Workflows, Guid? ProfileId,
    IReadOnlyList<string> PreparedDecisions, IReadOnlyList<string> Unresolved, IReadOnlyList<QsUatDecisionProposal> Proposals,
    bool IndependentConfigurationReviewRequired);
