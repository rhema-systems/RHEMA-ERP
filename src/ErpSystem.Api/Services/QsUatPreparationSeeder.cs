using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.DocumentManagement;
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
/// identity; QS decisions remain drafts for independent, evidence-backed owner approval.
/// No project, contract, BOQ, certificate, stock or financial transaction is created.
/// </summary>
public sealed class QsUatPreparationSeeder(
    ApplicationDbContext db,
    ICurrentUserService actor,
    WorkflowOwner workflowOwner,
    IQuantitySurveyConfigurationService configurationOwner,
    QuantitySurveyConfigurationProfileSeeder profileSeeder,
    QuantitySurveyStatutoryReportSeeder reportSeeder,
    IReportTemplateLifecycleService reportTemplateOwner)
{
    internal const string SeedMarker = "QS-UAT-PREPARATION-V1";
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
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.ProfileCode == "TDC-QUANTITY-SURVEY" &&
                p.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Draft)
            .SingleOrDefaultAsync(token);
        if (profile is null)
            return new(workflows, null, [], ["No editable QS draft. Existing published/historical configuration was preserved."], [], true);

        var lookup = (await configurationOwner.GetLookupsAsync(token)).Sources;
        var references = ResolveReferences(lookup, workflows, templates, reportTemplates, profile.EffectiveFrom);
        if (preparedReferences is not null)
            foreach (var reference in preparedReferences)
                references[reference.Key] = reference.Value.ToString();
        var prepared = new List<string>();
        var unresolved = new List<string>();
        var proposals = new List<QsUatDecisionProposal>();
        var current = await configurationOwner.GetProfileAsync(profile.Id, token);
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
            if (!CanPrepareDecision(decision))
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
        return new(workflows, profile.Id, prepared, unresolved, proposals, true);
    }

    internal static void ValidateTarget(string expected, string actual, bool testMode)
    {
        if (!testMode || string.IsNullOrWhiteSpace(expected) || !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase) ||
            new[] { "master", "model", "msdb", "tempdb" }.Contains(actual, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("QS UAT preparation requires explicit test mode and the exact non-system target database.");
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
        Select("ExpenseAccount", "postingExpenseAccounts", "DEFAULT-5000");
        Select("ApAccount", "accountsPayableAccounts", "DEFAULT-2000");
        Select("PaymentTerm", "supplierPaymentTerms", "NET30");
        Select("TaxGroup", "supplierTaxGroups", "GH-PURCH-STD");
        Select("WithholdingTax", "supplierWithholdingTaxes", "WHT-WORKS");
        if (lookups.TryGetValue("reports", out var reports))
        {
            var ids = reports.Where(r => r.Group == "quantity-survey").Select(r => r.Value).ToArray();
            if (ids.Length > 0) result["ReportIds"] = JsonSerializer.Serialize(ids);
        }
        return result;

        void Select(string key, string source, string label)
        {
            if (!lookups.TryGetValue(source, out var options)) return;
            var matching = options.Where(o => o.Label.Equals(label, StringComparison.OrdinalIgnoreCase) ||
                o.Label.StartsWith(label + " - ", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matching.Length == 1) result[key] = matching[0].Value;
            else if (options.Count == 1) result[key] = options[0].Value;
        }
    }
}

public sealed record QsUatDecisionProposal(string DecisionKey, JsonElement? Value, IReadOnlyList<string> MissingReferences);
public sealed record QsUatPreparationResult(IReadOnlyDictionary<string, Guid> Workflows, Guid? ProfileId,
    IReadOnlyList<string> PreparedDecisions, IReadOnlyList<string> Unresolved, IReadOnlyList<QsUatDecisionProposal> Proposals,
    bool IndependentConfigurationReviewRequired);
