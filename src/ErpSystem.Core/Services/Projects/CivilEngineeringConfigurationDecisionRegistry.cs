using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public sealed record CivilEngineeringDecisionDefinition(
    string ConfigurationKey,
    string DisplayName,
    string Description,
    string OwnerGroup,
    Type ValueType,
    IReadOnlyList<CivilEngineeringFieldSchemaDto> Fields);

public sealed record CivilEngineeringValueValidationResult(
    bool IsValid,
    string? CanonicalJson,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    IReadOnlyList<string> Errors);

public static class CivilEngineeringConfigurationDecisionRegistry
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlyDictionary<string, CivilEngineeringDecisionDefinition> Registry = Build()
        .ToDictionary(item => item.ConfigurationKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<CivilEngineeringDecisionDefinition> Definitions =>
        Registry.Values.OrderBy(item => item.ConfigurationKey, StringComparer.Ordinal).ToList();

    public static bool TryGet(string? configurationKey, out CivilEngineeringDecisionDefinition definition) =>
        Registry.TryGetValue(Normalize(configurationKey), out definition!);

    public static CivilEngineeringDecisionDefinition GetRequired(string? configurationKey) =>
        TryGet(configurationKey, out var definition)
            ? definition
            : throw new ArgumentException($"Unknown Civil Engineering configuration key '{configurationKey}'.", nameof(configurationKey));

    public static IReadOnlyList<CivilEngineeringDecisionSchemaDto> ToDtos() => Definitions
        .Select(item => new CivilEngineeringDecisionSchemaDto
        {
            ConfigurationKey = item.ConfigurationKey,
            DisplayName = item.DisplayName,
            Description = item.Description,
            OwnerGroup = item.OwnerGroup,
            SchemaVersion = SchemaVersion,
            Fields = item.Fields
        })
        .ToList();

    public static CivilEngineeringValueValidationResult Validate(string? configurationKey, int schemaVersion, JsonElement value)
    {
        if (!TryGet(configurationKey, out var definition))
            return Invalid($"Unknown Civil Engineering configuration key '{configurationKey}'.");
        if (schemaVersion != SchemaVersion)
            return Invalid($"{definition.ConfigurationKey} requires schema version {SchemaVersion}.");
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return Invalid($"{definition.ConfigurationKey} requires a value.");

        try
        {
            var payloadErrors = ValidateRequiredPayloadFields(value, definition);
            if (payloadErrors.Count > 0) return Invalid(payloadErrors.ToArray());

            var typed = JsonSerializer.Deserialize(value.GetRawText(), definition.ValueType, JsonOptions);
            if (typed is null) return Invalid($"{definition.ConfigurationKey} could not be read.");

            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(typed, new ValidationContext(typed), errors, validateAllProperties: true);
            ValidateRequiredLookups(typed, definition, errors);
            ValidateControlledSelections(typed, definition, errors);
            if (errors.Count > 0)
                return Invalid(errors.Select(item => item.ErrorMessage ?? "Invalid value.").Distinct(StringComparer.Ordinal).ToArray());

            var effective = (CivilEngineeringEffectiveDecisionValue)typed;
            return new CivilEngineeringValueValidationResult(
                true,
                JsonSerializer.Serialize(typed, definition.ValueType, JsonOptions),
                effective.EffectiveFrom,
                effective.EffectiveTo,
                Array.Empty<string>());
        }
        catch (JsonException exception)
        {
            return Invalid($"{definition.ConfigurationKey} does not match its controlled schema: {exception.Message}");
        }
    }

    public static JsonElement ParseValue(string? json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<CivilEngineeringDecisionDefinition> Build() =>
    [
        D<CivilEngineeringRolesAuthorityValue>("CIV-CFG-001", "Roles and authority", "Controlled role groups, scoped access and financial authority levels.", "Civil Engineering + Development + ICT",
            ML("operationalRoleIds", "Operational roles", "roles"), ML("approvalRoleIds", "Approval roles", "roles"), ML("oversightRoleIds", "Oversight roles", "roles"), ML("externalContributorRoleIds", "Contractor and consultant roles", "roles"), L("currencyCode", "Authority currency", "currencies"), N("operationalAuthorityLimit", "Operational authority limit", 0), N("seniorAuthorityLimit", "Senior authority limit", 0), N("executiveAuthorityLimit", "Executive authority limit", 0), B("enforceProjectScope", "Enforce project scope"), B("enforcePropertyScope", "Enforce property scope"), B("enforceDepartmentScope", "Enforce department scope")),
        D<CivilEngineeringWorkClassificationValue>("CIV-CFG-002", "Project and work classification", "Approved project types and Civil Engineering work classifications.", "Civil Engineering + Development",
            ML("projectTypeIds", "Project types", "projectTypes"), MSE<CivilEngineeringWorkClassification>("allowedClassifications", "Allowed classifications"), SE<CivilEngineeringWorkClassification>("defaultClassification", "Default classification"), B("requireProjectReference", "Require project reference"), B("requirePropertyReference", "Require property reference"), B("requireLocationReference", "Require location reference"), B("requirePlanningGisValidation", "Require Planning/GIS validation before technical design")),
        D<CivilEngineeringDesignReviewValue>("CIV-CFG-003", "Design review controls", "Controlled reconnaissance, cross-section, discipline, drafting and HOD review package.", "Civil Engineering + Architecture + Geodetic + Town Planning",
            L("workflowDefinitionId", "Design review workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.DesignReview), L("siteReconnaissanceTemplateId", "Site reconnaissance DMS template", "dmsTemplates"), L("crossSectionTemplateId", "Cross-section DMS template", "dmsTemplates"), L("draftingReviewTemplateId", "Drafting review DMS template", "dmsTemplates"), L("submissionPackageTemplateId", "HOD submission DMS template", "dmsTemplates"), MSE<CivilEngineeringDesignDiscipline>("requiredDisciplines", "Required disciplines"), ML("reviewerRoleIds", "Design reviewer roles", "roles"), B("requireSiteReconnaissance", "Require site reconnaissance"), B("requireVersionedReview", "Require versioned review"), B("requireHodApproval", "Require HOD approval")),
        D<CivilEngineeringDocumentPolicyValue>("CIV-CFG-004", "Engineering document policy", "DMS-owned engineering file types, limits, metadata, versioning, access and retention.", "Civil Engineering + ICT",
            MS("allowedFileExtensions", "Allowed file types", ".dwg", ".dxf", ".pro", ".prc", ".rvt", ".rfa", ".rte", ".std", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".jpg", ".jpeg", ".png"), N("maximumFileSizeMb", "Maximum file size (MB)", 1, 500), L("metadataTemplateId", "Engineering DMS template", "dmsTemplates"), ML("ownerRoleIds", "Document owner roles", "roles"), ML("reviewerRoleIds", "Document reviewer roles", "roles"), SE<CivilEngineeringDocumentNamingPolicy>("namingPolicy", "Version naming policy"), N("minimumRetentionDays", "Minimum retention days", 365, 36500), B("requireVersioning", "Require DMS versioning"), B("allowAuthorizedPreview", "Allow authorized preview"), B("allowAuthorizedDownload", "Allow authorized download")),
        D<CivilEngineeringSupervisionWorkflowValue>("CIV-CFG-005", "Supervision routing", "Workflow-owned site instruction, RFI, testing and interim-certificate routing.", "Civil Engineering + Project Management + QS/Finance",
            L("siteInstructionWorkflowDefinitionId", "Site instruction workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.SiteInstruction), L("rfiWorkflowDefinitionId", "RFI workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.Rfi), L("testReportWorkflowDefinitionId", "Test report workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.QualityTest), L("interimCertificateWorkflowDefinitionId", "Interim certificate workflow", "workflows", ErpSystem.Core.Services.QuantitySurvey.QuantitySurveyWorkflowBindingRegistry.PaymentCertificate), ML("projectEngineerRoleIds", "Project Engineer roles", "roles"), ML("projectManagerRoleIds", "Project Manager roles", "roles"), ML("coordinatorRoleIds", "Projects Coordinator roles", "roles"), B("enforceOrderedRouting", "Enforce ordered routing"), B("requireDmsEvidence", "Require DMS evidence"), B("requireIndependentEndorsement", "Require independent endorsement")),
        D<CivilEngineeringWeeklyReportValue>("CIV-CFG-006", "Weekly supervision report", "Weekly progress, activity, evidence, safety, test and escalation controls.", "Civil Engineering + Development",
            L("metadataTemplateId", "Weekly report DMS template", "dmsTemplates"), L("workflowDefinitionId", "Weekly report workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.WeeklyReport), ML("activityCategoryIds", "Activity categories", "projectCatalogEntries", "civil-weekly-activity-categories"), ML("escalationRoleIds", "Escalation roles", "roles"), N("minimumPhotoCount", "Minimum photo count", 0, 100), SE<CivilEngineeringReportDueDay>("dueDay", "Report due day"), B("requireProgressMeasurement", "Require progress measurement"), B("requireMaterialUsage", "Require material usage"), B("requireSafetyNotes", "Require safety notes"), B("requireTestSummary", "Require test summary")),
        D<CivilEngineeringMaintenanceAssessmentValue>("CIV-CFG-007", "Maintenance and complaint assessment", "Shared intake and evidence controls linking Projects, Estate and Maintenance owners.", "Civil Engineering + Maintenance + Finance",
            L("workflowDefinitionId", "Assessment workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment), L("metadataTemplateId", "Assessment DMS template", "dmsTemplates"), MSE<CivilEngineeringRequestSource>("allowedRequestSources", "Allowed request sources"), ML("defectCategoryIds", "Defect categories", "projectCatalogEntries", "civil-defect-categories"), MSE<CivilEngineeringUrgency>("allowedUrgencies", "Allowed urgency levels"), B("requireAssetOrProperty", "Require asset or property"), B("requireSiteAssessment", "Require site assessment"), B("requireCostEstimate", "Require cost estimate"), B("requireInspectionBeforeClosure", "Require inspection before closure"), B("requireClosureEvidence", "Require closure evidence")),
        D<CivilEngineeringCostingApprovalValue>("CIV-CFG-008", "Costing and approval routing", "Amount thresholds and shared QS, Procurement, Finance and Management approval routes.", "Civil Engineering + QS + Finance + Procurement",
            L("currencyCode", "Threshold currency", "currencies"), N("qsReviewThreshold", "QS review threshold", 0), N("procurementReviewThreshold", "Procurement review threshold", 0), N("managementApprovalThreshold", "Management approval threshold", 0), L("maintenanceWorkflowDefinitionId", "Maintenance costing workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.MaintenanceCosting), L("complaintWorkflowDefinitionId", "Complaint costing workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.ComplaintCosting), L("contractorWorkflowDefinitionId", "Contractor engagement workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.ContractorEngagement), ML("costReviewerRoleIds", "Cost reviewer roles", "roles"), ML("approverRoleIds", "Approval roles", "roles"), B("requireBudgetValidation", "Require budget validation")),
        D<CivilEngineeringPermittingReviewValue>("CIV-CFG-009", "Permitting review", "Controlled inter-section handoff, engineering comments, outcomes and HOD decision.", "Building Inspectorate + Development + Civil Engineering",
            L("workflowDefinitionId", "Permitting review workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.PermittingReview), ML("handoffRoleIds", "Handoff roles", "roles"), ML("commentCategoryIds", "Engineering comment categories", "projectCatalogEntries", "civil-permitting-comment-categories"), MSE<CivilEngineeringPermittingOutcome>("allowedOutcomes", "Allowed outcomes"), B("requireProjectAndPropertyValidation", "Require project and property validation"), B("requireReasonForReturnOrRejection", "Require return or rejection reason"), B("requireHodDecision", "Require HOD decision")),
        D<CivilEngineeringTaskAssignmentValue>("CIV-CFG-010", "Direct task assignment", "Controlled assignment, urgency, reassignment, feedback and closure acceptance.", "Civil Engineering + HR + ICT",
            L("workflowDefinitionId", "Task assignment workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.DirectTask), L("feedbackMetadataTemplateId", "Feedback DMS template", "dmsTemplates"), ML("assigneeRoleIds", "Eligible assignee roles", "roles"), ML("urgentEscalationRoleIds", "Urgent escalation roles", "roles"), MSE<CivilEngineeringUrgency>("allowedUrgencies", "Allowed urgency levels"), N("urgentResponseHours", "Urgent response hours", 1, 720), B("allowControlledReassignment", "Allow controlled reassignment"), B("requireDueDate", "Require due date"), B("requireFeedbackEvidence", "Require feedback evidence"), B("requireClosureAcceptance", "Require closure acceptance")),
        D<CivilEngineeringQualityTestValue>("CIV-CFG-011", "Quality and test reports", "Controlled test catalogue, thresholds, review, endorsement and failure handling.", "Civil Engineering + QA/QC + Consultants",
            L("workflowDefinitionId", "Quality test workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.QualityTest), L("evidenceMetadataTemplateId", "Test evidence DMS template", "dmsTemplates"), MSE<CivilEngineeringQualityTestCategory>("testCategories", "Test categories"), ML("reviewerRoleIds", "Test reviewer roles", "roles"), B("requireConfiguredPassThreshold", "Require configured pass threshold"), B("requireIndependentReview", "Require independent review"), B("requireEndorsementEvidence", "Require endorsement evidence"), B("blockAcceptanceOnFailure", "Block acceptance on failure")),
        D<CivilEngineeringReportPackValue>("CIV-CFG-012", "Reports and dashboards", "Scoped Civil Engineering reports, exports, scheduling and audit drilldown.", "Civil Engineering + Management",
            ML("reportIds", "Reports", "reports"), ML("viewerRoleIds", "Viewer roles", "roles"), MS("exportFormats", "Export formats", "PDF", "XLSX", "CSV"), B("enforceProjectAndPropertyScope", "Enforce project and property scope"), B("allowScheduledDistribution", "Allow scheduled distribution"), B("requireAuditDrilldown", "Require audit drilldown")),
        D<CivilEngineeringMigrationValue>("CIV-CFG-013", "Migration ownership", "Controlled historical-source ownership, staging, reconciliation and signed acceptance.", "Civil Engineering + Records + ICT",
            MSE<CivilEngineeringMigrationSource>("sourceTypes", "Migration sources"), ML("ownerRoleIds", "Source owner roles", "roles"), ML("reviewerRoleIds", "Reviewer roles", "roles"), ML("signOffRoleIds", "Sign-off roles", "roles"), L("reconciliationEvidenceTemplateId", "Reconciliation DMS template", "dmsTemplates"), B("requireStaging", "Require staging"), B("requireReconciliation", "Require reconciliation"), B("requireSignedAcceptance", "Require signed acceptance"), B("preservePhysicalFileReference", "Preserve physical file reference")),
        D<CivilEngineeringExtensionOfTimeValue>("CIV-CFG-014", "Variation and extension-of-time control", "Civil extension-of-time workflow, QS variation/budget and active-contract revalidation, with central-DMS evidence. It does not duplicate QS variation or Finance posting.", "Civil Engineering + QS + Finance + Procurement",
            L("workflowDefinitionId", "Extension-of-time workflow", "workflows", CivilEngineeringWorkflowBindingRegistry.ExtensionOfTime), L("evidenceMetadataTemplateId", "Extension-of-time DMS template", "dmsTemplates"), ML("reviewerRoleIds", "Extension-of-time reviewer roles", "roles"), B("requireQuantitySurveyVariationForCostImpact", "Require QS variation for cost impact"), B("requireFinanceBudgetRevalidation", "Require Finance budget revalidation"), B("requireProcurementContractRevalidation", "Require Procurement contract revalidation"), B("requireIndependentApproval", "Require independent approval"))
    ];

    private static CivilEngineeringDecisionDefinition D<T>(string key, string name, string description, string owner, params CivilEngineeringFieldSchemaDto[] fields)
        where T : CivilEngineeringEffectiveDecisionValue =>
        new(key, name, description, owner, typeof(T), [DateFrom(), DateTo(), .. fields]);

    private static CivilEngineeringFieldSchemaDto DateFrom() => new() { Name = "effectiveFrom", Label = "Effective from", Control = "date", Required = true };
    private static CivilEngineeringFieldSchemaDto DateTo() => new() { Name = "effectiveTo", Label = "Effective to", Control = "date" };
    private static CivilEngineeringFieldSchemaDto B(string name, string label) => new() { Name = name, Label = label, Control = "boolean", Required = true };
    private static CivilEngineeringFieldSchemaDto N(string name, string label, decimal minimum, decimal? maximum = null) => new() { Name = name, Label = label, Control = "number", Required = true, Minimum = minimum, Maximum = maximum };
    private static CivilEngineeringFieldSchemaDto L(string name, string label, string source, string? group = null, bool required = true) => new() { Name = name, Label = label, Control = "lookup", LookupSource = source, LookupGroup = group, Required = required };
    private static CivilEngineeringFieldSchemaDto ML(string name, string label, string source, string? group = null, bool required = true) => new() { Name = name, Label = label, Control = "multilookup", LookupSource = source, LookupGroup = group, Required = required };
    private static CivilEngineeringFieldSchemaDto MS(string name, string label, params string[] options) => new() { Name = name, Label = label, Control = "multiselect", Required = true, Options = options };
    private static CivilEngineeringFieldSchemaDto SE<T>(string name, string label) where T : struct, Enum => new() { Name = name, Label = label, Control = "select", Required = true, Options = CanonicalEnumOptions<T>() };
    private static CivilEngineeringFieldSchemaDto MSE<T>(string name, string label) where T : struct, Enum => new() { Name = name, Label = label, Control = "multiselect", Required = true, Options = CanonicalEnumOptions<T>() };
    private static string[] CanonicalEnumOptions<T>() where T : struct, Enum => Enum.GetNames<T>().Select(JsonNamingPolicy.CamelCase.ConvertName).ToArray();

    private static void ValidateRequiredLookups(object value, CivilEngineeringDecisionDefinition definition, ICollection<ValidationResult> errors)
    {
        foreach (var field in definition.Fields.Where(item => item.Required && item.Control is "lookup" or "multilookup"))
        {
            var property = value.GetType().GetProperty(field.Name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            var selected = property?.GetValue(value);
            if (selected is Guid id && id == Guid.Empty)
                errors.Add(new ValidationResult($"{field.Label} is required."));
            if (selected is IEnumerable<Guid> ids && ids.Any(id => id == Guid.Empty))
                errors.Add(new ValidationResult($"{field.Label} contains an invalid selection."));
        }
    }

    private static List<string> ValidateRequiredPayloadFields(JsonElement value, CivilEngineeringDecisionDefinition definition)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return new List<string> { "The configuration value must be a structured object." };

        var supplied = value.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return definition.Fields
            .Where(item => item.Required && !supplied.Contains(item.Name))
            .Select(item => $"{item.Label} is required.")
            .ToList();
    }

    private static void ValidateControlledSelections(object value, CivilEngineeringDecisionDefinition definition, ICollection<ValidationResult> errors)
    {
        foreach (var field in definition.Fields.Where(item => item.Options.Count > 0))
        {
            var property = value.GetType().GetProperty(field.Name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            var selected = property?.GetValue(value);
            if (selected is string text && !field.Options.Contains(text, StringComparer.OrdinalIgnoreCase))
                errors.Add(new ValidationResult($"{field.Label} contains an unsupported option."));
            if (selected is IEnumerable<string> list && list.Any(item => !field.Options.Contains(item, StringComparer.OrdinalIgnoreCase)))
                errors.Add(new ValidationResult($"{field.Label} contains an unsupported option."));
        }
    }

    private static CivilEngineeringValueValidationResult Invalid(params string[] errors) => new(false, null, null, null, errors);
    private static string Normalize(string? key) => key?.Trim().ToUpperInvariant() ?? string.Empty;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
