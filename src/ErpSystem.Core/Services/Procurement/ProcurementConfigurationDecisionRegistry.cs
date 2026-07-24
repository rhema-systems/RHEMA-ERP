using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public sealed record ProcurementDecisionSchemaDefinition(
    string DecisionKey,
    string DisplayName,
    string Description,
    string OwnerGroup,
    int SchemaVersion,
    Type ValueType,
    bool RequiresApproval,
    bool RequiresEvidence,
    bool RequiresRenewedApproval);

public sealed record ProcurementDecisionValueValidationResult(
    bool IsValid,
    string? CanonicalJson,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    IReadOnlyList<string> Errors);

public static class ProcurementConfigurationDecisionRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static readonly IReadOnlyDictionary<string, ProcurementDecisionSchemaDefinition> Registry =
        new[]
        {
            Define<ProcurementMethodThresholdDecisionValueDto>("DEC-001", "Procurement method thresholds", "Effective-dated category and service-class method thresholds with statutory lineage.", "TDC Procurement + Legal/PPA"),
            Define<ProcurementAuthorityDecisionValueDto>("DEC-002", "Approval authority matrix", "Amount boundaries, inclusivity, applicable categories, and escalation authority.", "TDC Procurement + Legal/PPA"),
            Define<ProcurementWorkflowSelectionDecisionValueDto>("DEC-003", "Workflow selection", "Policy-selected shared workflow definition and applicability conditions by entity type.", "MD + Procurement + Finance"),
            Define<ProcurementAuthorityStageDecisionValueDto>("DEC-004", "Authority and committee stages", "Committee, observer, quorum, evidence, sequencing, and escalation requirements.", "TDC Procurement + Legal/Internal Audit"),
            Define<ProcurementPettyPurchaseDecisionValueDto>("DEC-005", "Petty purchase and waiver", "Petty threshold, waiver eligibility, evidence, approver, and expiry.", "TDC Procurement + Finance"),
            Define<ProcurementExceptionPrerequisiteDecisionValueDto>("DEC-006", "Restricted and single-source prerequisites", "Approval authority, statutory prerequisites, evidence checklist, filing reference, and expiry.", "TDC Procurement + Legal/PPA"),
            Define<ProcurementSupplierFeeDecisionValueDto>("DEC-007", "Supplier fees", "Fee, tax, payment, receipt, exemption, refund, and renewal configuration.", "Procurement + Finance"),
            Define<ProcurementSignatureDecisionValueDto>("DEC-008", "Document signatures", "Allowed signature mode, signatory role/order, verification, and evidence requirements.", "Legal + ICT + Procurement"),
            Define<ProcurementGhanepsDecisionValueDto>("DEC-009", "GHANEPS exchange profile", "File/template mappings, frequency, ownership, acknowledgement, and reconciliation.", "Procurement + ICT/PPA"),
            Define<ProcurementNegativeStockDecisionValueDto>("DEC-010", "Negative stock policy", "Default prohibition and any controlled emergency permission/workflow/evidence path.", "Stores + Finance + Internal Audit"),
            Define<ProcurementSupplierRiskDecisionValueDto>("DEC-011", "Supplier AVL and risk", "Review frequency, dimensions, bands, concentration, score, and eligibility action.", "Procurement + Internal Audit"),
            Define<ProcurementCutoverDecisionValueDto>("DEC-012", "Deployment cutover gate", "Cutover, dual run, data ownership, acceptance signatories, release, and evidence.", "Steering Committee"),
            Define<ProcurementReceiptDocumentDecisionValueDto>("DEC-013", "GRN/MRN receipt documents", "Receipt document applicability, coexistence, numbering, templates, signatures, and evidence.", "Stores + Procurement + Finance"),
            Define<ProcurementNonFunctionalDecisionValueDto>("DEC-014", "Non-functional acceptance", "Workload, availability, response, backup, RPO/RTO, security, observability, usability, and accessibility targets.", "ICT + Procurement + Stores")
        }.ToDictionary(item => item.DecisionKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ProcurementDecisionSchemaDefinition> Definitions =>
        Registry.Values.OrderBy(item => item.DecisionKey, StringComparer.Ordinal).ToList();

    public static bool TryGet(string? decisionKey, out ProcurementDecisionSchemaDefinition definition) =>
        Registry.TryGetValue(NormalizeKey(decisionKey), out definition!);

    public static ProcurementDecisionSchemaDefinition GetRequired(string? decisionKey)
    {
        if (!TryGet(decisionKey, out var definition))
            throw new ArgumentException($"Unknown procurement configuration decision key '{decisionKey}'.", nameof(decisionKey));
        return definition;
    }

    public static ProcurementDecisionValueValidationResult Validate(
        string? decisionKey,
        int schemaVersion,
        JsonElement value)
    {
        if (!TryGet(decisionKey, out var definition))
            return Invalid($"Unknown procurement configuration decision key '{decisionKey}'.");

        if (schemaVersion != definition.SchemaVersion)
            return Invalid($"{definition.DecisionKey} requires schema version {definition.SchemaVersion}; received {schemaVersion}.");

        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return Invalid($"{definition.DecisionKey} requires a typed value payload.");

        try
        {
            var typedValue = JsonSerializer.Deserialize(value.GetRawText(), definition.ValueType, JsonOptions);
            if (typedValue is null)
                return Invalid($"{definition.DecisionKey} value payload could not be read.");

            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(
                typedValue,
                new ValidationContext(typedValue),
                validationResults,
                validateAllProperties: true);

            ValidateStringCollections(typedValue, validationResults);

            if (validationResults.Count > 0)
            {
                return new ProcurementDecisionValueValidationResult(
                    false,
                    null,
                    null,
                    null,
                    validationResults
                        .Select(result => result.ErrorMessage ?? "Invalid value.")
                        .Distinct(StringComparer.Ordinal)
                        .ToList());
            }

            var effectiveValue = (EffectiveDatedDecisionValueDto)typedValue;
            return new ProcurementDecisionValueValidationResult(
                true,
                JsonSerializer.Serialize(typedValue, definition.ValueType, JsonOptions),
                effectiveValue.EffectiveFrom,
                effectiveValue.EffectiveTo,
                Array.Empty<string>());
        }
        catch (JsonException exception)
        {
            return Invalid($"{definition.DecisionKey} value does not match schema version {definition.SchemaVersion}: {exception.Message}");
        }
    }

    public static JsonElement ParseValue(string? valueJson)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(valueJson) ? "{}" : valueJson);
        return document.RootElement.Clone();
    }

    public static IReadOnlyList<ProcurementDecisionSchemaDto> ToDtos() => Definitions
        .Select(item => new ProcurementDecisionSchemaDto
        {
            DecisionKey = item.DecisionKey,
            DisplayName = item.DisplayName,
            Description = item.Description,
            OwnerGroup = item.OwnerGroup,
            SchemaVersion = item.SchemaVersion,
            ValueType = item.ValueType.Name,
            RequiresApproval = item.RequiresApproval,
            RequiresEvidence = item.RequiresEvidence,
            RequiresRenewedApproval = item.RequiresRenewedApproval
        })
        .ToList();

    private static ProcurementDecisionSchemaDefinition Define<T>(
        string key,
        string displayName,
        string description,
        string ownerGroup) where T : EffectiveDatedDecisionValueDto =>
        new(key, displayName, description, ownerGroup, 1, typeof(T), true, true, true);

    private static string NormalizeKey(string? decisionKey) => decisionKey?.Trim().ToUpperInvariant() ?? string.Empty;

    private static ProcurementDecisionValueValidationResult Invalid(string error) =>
        new(false, null, null, null, new[] { error });

    private static void ValidateStringCollections(object typedValue, ICollection<ValidationResult> results)
    {
        foreach (var property in typedValue.GetType().GetProperties())
        {
            if (property.GetValue(typedValue) is not IEnumerable<string> values)
                continue;

            if (values.Any(string.IsNullOrWhiteSpace))
                results.Add(new ValidationResult($"{property.Name} cannot contain blank values.", new[] { property.Name }));
        }
    }

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
