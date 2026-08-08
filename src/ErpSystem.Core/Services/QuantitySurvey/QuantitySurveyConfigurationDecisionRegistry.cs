using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyDecisionDefinition(
    string DecisionKey, string ConfigurationKey, string DisplayName, string Description,
    string OwnerGroup, Type ValueType, IReadOnlyList<QuantitySurveyFieldSchemaDto> Fields);

public sealed record QuantitySurveyValueValidationResult(
    bool IsValid, string? CanonicalJson, DateTime? EffectiveFrom, DateTime? EffectiveTo,
    IReadOnlyList<string> Errors);

public static class QuantitySurveyConfigurationDecisionRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlyDictionary<string, QuantitySurveyDecisionDefinition> Registry = Build()
        .ToDictionary(item => item.DecisionKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<QuantitySurveyDecisionDefinition> Definitions => Registry.Values.OrderBy(x => x.DecisionKey).ToList();
    public static bool TryGet(string? key, out QuantitySurveyDecisionDefinition definition) =>
        Registry.TryGetValue(key?.Trim().ToUpperInvariant() ?? string.Empty, out definition!);
    public static QuantitySurveyDecisionDefinition GetRequired(string? key) => TryGet(key, out var value)
        ? value : throw new ArgumentException($"Unknown quantity-survey decision key '{key}'.", nameof(key));

    public static IReadOnlyList<QuantitySurveyDecisionSchemaDto> ToDtos() => Definitions.Select(x => new QuantitySurveyDecisionSchemaDto
    {
        DecisionKey = x.DecisionKey,
        ConfigurationKey = x.ConfigurationKey,
        DisplayName = x.DisplayName,
        Description = x.Description,
        OwnerGroup = x.OwnerGroup,
        SchemaVersion = 1,
        Fields = x.Fields
    }).ToList();

    public static QuantitySurveyValueValidationResult Validate(string? key, int schemaVersion, JsonElement value)
    {
        if (!TryGet(key, out var definition)) return Invalid($"Unknown quantity-survey decision key '{key}'.");
        if (schemaVersion != 1) return Invalid($"{definition.DecisionKey} requires schema version 1.");
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return Invalid($"{definition.DecisionKey} requires a value.");
        try
        {
            var payloadErrors = ValidateRequiredPayloadFields(value, definition);
            if (payloadErrors.Count > 0) return Invalid(payloadErrors.ToArray());
            var typed = JsonSerializer.Deserialize(value.GetRawText(), definition.ValueType, JsonOptions);
            if (typed is null) return Invalid($"{definition.DecisionKey} could not be read.");
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(typed, new ValidationContext(typed), errors, true);
            ValidateRequiredGuids(typed, definition, errors);
            ValidateControlledStrings(typed, definition, errors);
            ValidateDomainRules(typed, errors);
            if (errors.Count > 0) return Invalid(errors.Select(x => x.ErrorMessage ?? "Invalid value.").Distinct().ToArray());
            var dated = (QuantitySurveyEffectiveDecisionValue)typed;
            return new(true, JsonSerializer.Serialize(typed, definition.ValueType, JsonOptions), dated.EffectiveFrom, dated.EffectiveTo, Array.Empty<string>());
        }
        catch (JsonException ex) { return Invalid($"{definition.DecisionKey} does not match its controlled schema: {ex.Message}"); }
    }

    public static JsonElement ParseValue(string? json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<QuantitySurveyDecisionDefinition> Build() =>
    [
        D<QsRolesAuthorityValue>("QS-DEC-001", "QS-CFG-001", "Roles and authority", "Role groups, scoped access and financial authority levels.", "QS + Development + ICT",
            ML("officerRoleIds", "QS officer roles", "roles"), ML("approverRoleIds", "Approval roles", "roles"), ML("oversightRoleIds", "Oversight roles", "roles"),
            L("currencyCode", "Authority currency", "currencies"), N("operationalAuthorityLimit", "Operational authority limit", 0), N("seniorAuthorityLimit", "Senior authority limit", 0), N("executiveAuthorityLimit", "Executive authority limit", 0),
            B("enforceProjectScope", "Enforce project scope"), B("enforceContractScope", "Enforce contract scope"), B("enforceSectionScope", "Enforce section scope")),
        D<QsBoqStandardsValue>("QS-DEC-002", "QS-CFG-002", "BoQ standards and catalogues", "Approved measurement standards and applicable project types.", "QS + Development",
            MSE<QuantitySurveyBoqStandard>("allowedStandards", "Allowed standards"), SE<QuantitySurveyBoqStandard>("defaultStandard", "Default standard"), ML("projectTypeIds", "Project types", "projectTypes"), B("requireCostCode", "Require cost code"), B("requireTrade", "Require trade"), B("requireWorkPackage", "Require work package")),
        D<QsBoqVersionPolicyValue>("QS-DEC-003", "QS-CFG-003", "BoQ version policy", "Required BoQ versions, comparisons and immutable approved snapshots.", "QS + Legal/Contracts",
            MSE<QuantitySurveyBoqVersionType>("requiredVersionTypes", "Required versions"), WL("boqWorkflowDefinitionId", "BoQ workflow", QuantitySurveyWorkflowBindingRegistry.Boq), WL("estimateWorkflowDefinitionId", "Estimate workflow", QuantitySurveyWorkflowBindingRegistry.Estimate), B("approvedVersionsImmutable", "Approved versions are immutable"), B("requireWorkflowBeforeUse", "Require approval before use"), B("requireLineLevelComparison", "Require line comparison")),
        D<QsRateBuildUpValue>("QS-DEC-004", "QS-CFG-004", "Rate build-up structure", "Auditable rate components, controlled markups and rounding.", "QS + Finance",
            MSE<QuantitySurveyRateComponent>("components", "Rate components"), P("maximumOverheadPercent", "Maximum overhead"), P("maximumProfitPercent", "Maximum profit"), P("maximumContingencyPercent", "Maximum contingency"), P("maximumWastagePercent", "Maximum wastage"), N("decimalPlaces", "Decimal places", 0, 6)),
        D<QsRateLibraryValue>("QS-DEC-005", "QS-CFG-005", "Rate library policy", "Dimensions, locations and market update cadence.", "QS + Procurement",
            MSE<QuantitySurveyRateDimension>("dimensions", "Rate dimensions"), N("updateCadenceMonths", "Update cadence in months", 1, 36), ML("projectTypeIds", "Project types", "projectTypes"), ML("locationIds", "Regions and locations", "locations"), B("requireMarketEvidence", "Require market evidence")),
        D<QsEscalationValue>("QS-DEC-006", "QS-CFG-006", "Price adjustment and indices", "Controlled formula, coefficient set, index sources and approval route.", "QS + Finance + Legal/Contracts",
            MSE<QuantitySurveyIndexSource>("indexSources", "Index sources"), SE<QuantitySurveyEscalationFormula>("formula", "Formula"), P("materialCoefficient", "Material coefficient"), P("labourCoefficient", "Labour coefficient"), P("plantCoefficient", "Plant coefficient"), P("otherCoefficient", "Other coefficient"), WL("approvalWorkflowDefinitionId", "Approval workflow", QuantitySurveyWorkflowBindingRegistry.Escalation), S("importFormat", "Import format", "Controlled Excel", "CSV", "API")),
        D<QsMeasurementValue>("QS-DEC-007", "QS-CFG-007", "Measurement workflow", "Taking-off evidence, joint attendance, consultant endorsement and signatures.", "QS + Development + Contractors/Consultants",
            WL("workflowDefinitionId", "Measurement workflow", QuantitySurveyWorkflowBindingRegistry.Measurement), L("metadataTemplateId", "Taking-off DMS template", "dmsTemplates"), ML("jointAttendanceRoleIds", "Joint attendance roles", "roles"), ML("consultantRoleIds", "Consultant roles", "roles"), B("requireContractorSignature", "Require contractor signature"), B("requireConsultantSignature", "Require consultant signature")),
        D<QsValuationCertificateValue>("QS-DEC-008", "QS-CFG-008", "Valuation and certificate policy", "Valuation/certificate workflows, templates, tax and deduction rules.", "QS + Finance",
            WL("valuationWorkflowDefinitionId", "Valuation workflow", QuantitySurveyWorkflowBindingRegistry.Valuation), WL("certificateWorkflowDefinitionId", "Certificate workflow", QuantitySurveyWorkflowBindingRegistry.PaymentCertificate), L("valuationTemplateId", "Valuation report template", "reportTemplates"), L("certificateTemplateId", "Certificate report template", "reportTemplates"), SE<QuantitySurveyTaxHandling>("taxHandling", "Tax handling"), B("requirePreviousCertificate", "Require previous certificate logic"), B("applyAdvanceRecovery", "Apply advance recovery"), B("applyRetention", "Apply retention")),
        D<QsRetentionValue>("QS-DEC-009", "QS-CFG-009", "Retention terms", "Retention ceiling, partial releases, defects period, bond and approval route.", "QS + Legal/Contracts + Finance",
            P("maximumRetentionPercent", "Maximum retention"), P("practicalCompletionReleasePercent", "Practical completion release"), P("sectionalTakeoverReleasePercent", "Sectional takeover release"), P("defectsReleasePercent", "Defects release"), N("defectsLiabilityDays", "Defects liability days", 0, 3650), WL("approvalWorkflowDefinitionId", "Retention workflow", QuantitySurveyWorkflowBindingRegistry.RetentionRelease), B("allowRetentionBond", "Allow retention bond")),
        D<QsMaterialDeductionValue>("QS-DEC-010", "QS-CFG-010", "Material deductions", "Materials on/off site, TDC-issued material deductions and inventory reconciliation.", "QS + Stores + Finance",
            B("allowMaterialsOnSite", "Allow materials on site"), B("allowOffSiteMaterials", "Allow off-site materials"), B("deductTdcSuppliedMaterials", "Deduct TDC-supplied materials"), SE<QuantitySurveyMaterialValuationBasis>("valuationBasis", "Valuation basis"), B("requireInventoryReconciliation", "Require inventory reconciliation"), WL("approvalWorkflowDefinitionId", "Material deduction workflow", QuantitySurveyWorkflowBindingRegistry.MaterialDeduction)),
        D<QsVariationClaimsValue>("QS-DEC-011", "QS-CFG-011", "Variations and claims", "Controlled variation, claim, daywork and additional-work workflow effects.", "QS + Development + Legal/Contracts",
            WL("variationWorkflowDefinitionId", "Variation workflow", QuantitySurveyWorkflowBindingRegistry.Variation), WL("claimWorkflowDefinitionId", "Claim workflow", QuantitySurveyWorkflowBindingRegistry.Claim), MS("allowedTypes", "Allowed record types", "Variation", "Claim", "Daywork", "Additional Work", "Site Instruction", "Change Order"), B("updateContractSum", "Update contract sum"), B("updateBudget", "Update budget"), B("updateForecast", "Update forecast"), B("updateCertificate", "Update certificate")),
        D<QsContractControlsValue>("QS-DEC-012", "QS-CFG-012", "Contract commercial controls", "QS commercial controls and final-account workflow.", "QS + Procurement + Legal",
            B("controlProvisionalSums", "Control provisional sums"), B("controlContingencies", "Control contingencies"), B("controlDefectsLiability", "Control defects liability"), B("controlSubcontracts", "Control subcontracts"), B("controlBackCharges", "Control back charges"), B("controlContraCharges", "Control contra charges"), WL("finalAccountWorkflowDefinitionId", "Final account workflow", QuantitySurveyWorkflowBindingRegistry.FinalAccount)),
        D<QsExternalSubmissionValue>("QS-DEC-013", "QS-CFG-013", "External submissions", "Controlled contractor/consultant intake channels and document limits.", "QS + ICT + Contractors",
            MSE<QuantitySurveyExternalSubmissionChannel>("channels", "Submission channels"), MS("allowedFileExtensions", "Allowed file types", ".xlsx", ".csv", ".pdf", ".docx"), N("maximumFileSizeMb", "Maximum file size (MB)", 1, 500), B("requirePortalIdentity", "Require portal identity"), B("requireEvidence", "Require evidence"), B("requireSignature", "Require signature")),
        D<QsThirdPartyToolsValue>("QS-DEC-014", "QS-CFG-014", "Third-party estimation tools", "Allowed tools, exchange modes and controlled staging.", "QS + ICT + Development",
            MSE<QuantitySurveyThirdPartyTool>("allowedTools", "Allowed tools", false), MSE<QuantitySurveyExternalSubmissionChannel>("exchangeModes", "Exchange modes"), B("requireStagingAndReconciliation", "Require staging and reconciliation"), ML("metadataTemplateIds", "DMS exchange templates", "dmsTemplates", false)),
        D<QsReportsValue>("QS-DEC-015", "QS-CFG-015", "Reports and dashboards", "Report catalogue, scoped viewers, exports and scheduling.", "QS + Management",
            ML("reportIds", "Reports", "reports"), ML("viewerRoleIds", "Viewer roles", "roles"), MS("exportFormats", "Export formats", "PDF", "XLSX", "CSV"), B("enforceScopedDrilldown", "Enforce scoped drilldown"), B("allowScheduledDistribution", "Allow scheduled distribution")),
        D<QsInterfacesValue>("QS-DEC-016", "QS-CFG-016", "Integration controls", "Source-of-truth, idempotency and reconciliation controls across shared modules.", "ICT + Finance + QS",
            MSE<QuantitySurveyIntegrationModule>("modules", "Integrated modules"), SE<QuantitySurveyPostingMode>("postingMode", "Posting mode"), B("requireIdempotencyKey", "Require idempotency key"), B("requireReconciliation", "Require reconciliation"), B("prohibitDuplicatePosting", "Prohibit duplicate posting")),
        D<QsMigrationValue>("QS-DEC-017", "QS-CFG-017", "Migration ownership", "Controlled source types, accountable roles, staging and signed reconciliation.", "QS + ICT",
            MSE<QuantitySurveyMigrationSource>("sourceTypes", "Migration sources"), ML("ownerRoleIds", "Source owner roles", "roles"), ML("reviewerRoleIds", "Reviewer roles", "roles"), ML("signOffRoleIds", "Sign-off roles", "roles"), B("requireStaging", "Require staging"), B("requireReconciliation", "Require reconciliation"), B("requireSignedAcceptance", "Require signed acceptance"))
    ];

    private static QuantitySurveyDecisionDefinition D<T>(string key, string cfg, string name, string description, string owner, params QuantitySurveyFieldSchemaDto[] fields) where T : QuantitySurveyEffectiveDecisionValue =>
        new(key, cfg, name, description, owner, typeof(T), [DateFrom(), DateTo(), .. fields]);
    private static QuantitySurveyFieldSchemaDto DateFrom() => new() { Name = "effectiveFrom", Label = "Effective from", Control = "date", Required = true };
    private static QuantitySurveyFieldSchemaDto DateTo() => new() { Name = "effectiveTo", Label = "Effective to", Control = "date" };
    private static QuantitySurveyFieldSchemaDto B(string n, string l) => new() { Name = n, Label = l, Control = "boolean", Required = true };
    private static QuantitySurveyFieldSchemaDto N(string n, string l, decimal min, decimal? max = null) => new() { Name = n, Label = l, Control = "number", Required = true, Minimum = min, Maximum = max };
    private static QuantitySurveyFieldSchemaDto P(string n, string l) => N(n, l + " (%)", 0, 100);
    private static QuantitySurveyFieldSchemaDto L(string n, string l, string source, bool required = true) => new() { Name = n, Label = l, Control = "lookup", LookupSource = source, Required = required };
    private static QuantitySurveyFieldSchemaDto WL(string n, string l, string entityTypeCode) => new() { Name = n, Label = l, Control = "lookup", LookupSource = "workflows", LookupGroup = entityTypeCode, Required = true };
    private static QuantitySurveyFieldSchemaDto ML(string n, string l, string source, bool required = true) => new() { Name = n, Label = l, Control = "multilookup", LookupSource = source, Required = required };
    private static QuantitySurveyFieldSchemaDto S(string n, string l, params string[] options) => new() { Name = n, Label = l, Control = "select", Required = true, Options = options };
    private static QuantitySurveyFieldSchemaDto MS(string n, string l, params string[] options) => new() { Name = n, Label = l, Control = "multiselect", Required = true, Options = options };
    private static QuantitySurveyFieldSchemaDto SE<T>(string n, string l) where T : struct, Enum => S(n, l, CanonicalEnumOptions<T>());
    private static QuantitySurveyFieldSchemaDto MSE<T>(string n, string l, bool required = true) where T : struct, Enum => new() { Name = n, Label = l, Control = "multiselect", Required = required, Options = CanonicalEnumOptions<T>() };
    private static string[] CanonicalEnumOptions<T>() where T : struct, Enum =>
        Enum.GetNames<T>().Select(JsonNamingPolicy.CamelCase.ConvertName).ToArray();

    private static void ValidateRequiredGuids(object value, QuantitySurveyDecisionDefinition definition, ICollection<ValidationResult> errors)
    {
        foreach (var field in definition.Fields.Where(x => x.Required && x.Control == "lookup"))
        {
            var property = value.GetType().GetProperty(field.Name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property?.PropertyType == typeof(Guid) && (Guid)(property.GetValue(value) ?? Guid.Empty) == Guid.Empty)
                errors.Add(new ValidationResult($"{field.Label} is required."));
        }
    }

    private static List<string> ValidateRequiredPayloadFields(JsonElement value, QuantitySurveyDecisionDefinition definition)
    {
        var errors = new List<string>();
        if (value.ValueKind != JsonValueKind.Object) { errors.Add("The decision value must be a structured object."); return errors; }
        var supplied = value.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.Fields.Where(x => x.Required))
            if (!supplied.Contains(field.Name)) errors.Add($"{field.Label} is required.");
        return errors;
    }

    private static void ValidateControlledStrings(object value, QuantitySurveyDecisionDefinition definition, ICollection<ValidationResult> errors)
    {
        foreach (var field in definition.Fields.Where(x => x.Options.Count > 0))
        {
            var property = value.GetType().GetProperty(field.Name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            var raw = property?.GetValue(value);
            if (raw is string selected && !field.Options.Contains(selected, StringComparer.OrdinalIgnoreCase))
                errors.Add(new ValidationResult($"{field.Label} contains an unsupported option."));
            if (raw is IEnumerable<string> selectedList && selectedList.Any(x => !field.Options.Contains(x, StringComparer.OrdinalIgnoreCase)))
                errors.Add(new ValidationResult($"{field.Label} contains an unsupported option."));
        }
    }

    private static void ValidateDomainRules(object value, ICollection<ValidationResult> errors)
    {
        if (value is not QsBoqVersionPolicyValue policy) return;
        if (!policy.RequireWorkflowBeforeUse)
            errors.Add(new ValidationResult("BoQ workflow approval before use is mandatory."));
        if (!policy.ApprovedVersionsImmutable)
            errors.Add(new ValidationResult("Approved BoQ publications must be immutable."));
    }

    private static QuantitySurveyValueValidationResult Invalid(params string[] errors) => new(false, null, null, null, errors);
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        return options;
    }
}
