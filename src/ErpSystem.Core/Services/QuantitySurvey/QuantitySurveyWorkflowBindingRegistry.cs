using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyWorkflowEntityTypeDefinition(
    string Code,
    string Name,
    string Description);

public sealed record QuantitySurveyWorkflowBindingDefinition(
    string DecisionKey,
    string ConfigurationField,
    string EntityTypeCode,
    string RecordFamily);

public static class QuantitySurveyWorkflowBindingRegistry
{
    public const string Boq = "QS_BOQ";
    public const string Estimate = "QS_ESTIMATE";
    public const string Escalation = "QS_ESCALATION";
    public const string Measurement = "QS_MEASUREMENT";
    public const string Valuation = "QS_VALUATION";
    public const string PaymentCertificate = "QS_PAYMENT_CERTIFICATE";
    public const string RetentionRelease = "QS_RETENTION_RELEASE";
    public const string MaterialDeduction = "QS_MATERIAL_DEDUCTION";
    public const string Variation = "QS_VARIATION";
    public const string Claim = "QS_CLAIM";
    public const string Subcontract = "QS_SUBCONTRACT";
    public const string FinalAccount = "QS_FINAL_ACCOUNT";

    public static IReadOnlyList<QuantitySurveyWorkflowEntityTypeDefinition> EntityTypes { get; } =
    [
        new(Boq, "QS BoQ", "Quantity Survey bill-of-quantities approval lifecycle."),
        new(Estimate, "QS Estimate", "Quantity Survey cost-plan and estimate approval lifecycle."),
        new(Escalation, "QS Escalation", "Quantity Survey price-adjustment calculation approval lifecycle."),
        new(Measurement, "QS Measurement", "Quantity Survey measurement and remeasurement approval lifecycle."),
        new(Valuation, "QS Valuation", "Quantity Survey interim valuation approval lifecycle."),
        new(PaymentCertificate, "QS Payment Certificate", "Quantity Survey payment-certificate approval lifecycle."),
        new(RetentionRelease, "QS Retention Release", "Quantity Survey retention-release approval lifecycle."),
        new(MaterialDeduction, "QS Material Deduction", "Quantity Survey material valuation and deduction approval lifecycle."),
        new(Variation, "QS Variation", "Quantity Survey variation and change-order approval lifecycle."),
        new(Claim, "QS Claim", "Quantity Survey contractor and subcontractor claim approval lifecycle."),
        new(Subcontract, "QS Subcontract", "Quantity Survey subcontract approval lifecycle."),
        new(FinalAccount, "QS Final Account", "Quantity Survey final-account approval lifecycle.")
    ];

    public static IReadOnlyList<QuantitySurveyWorkflowBindingDefinition> Bindings { get; } =
    [
        new("QS-DEC-003", "boqWorkflowDefinitionId", Boq, "BoQ"),
        new("QS-DEC-003", "estimateWorkflowDefinitionId", Estimate, "Estimate"),
        new("QS-DEC-006", "approvalWorkflowDefinitionId", Escalation, "Escalation"),
        new("QS-DEC-007", "workflowDefinitionId", Measurement, "Measurement"),
        new("QS-DEC-008", "valuationWorkflowDefinitionId", Valuation, "Valuation"),
        new("QS-DEC-008", "certificateWorkflowDefinitionId", PaymentCertificate, "Payment certificate"),
        new("QS-DEC-009", "approvalWorkflowDefinitionId", RetentionRelease, "Retention release"),
        new("QS-DEC-010", "approvalWorkflowDefinitionId", MaterialDeduction, "Material deduction"),
        new("QS-DEC-011", "variationWorkflowDefinitionId", Variation, "Variation"),
        new("QS-DEC-011", "claimWorkflowDefinitionId", Claim, "Claim"),
        new("QS-DEC-012", "subcontractWorkflowDefinitionId", Subcontract, "Subcontract"),
        new("QS-DEC-012", "finalAccountWorkflowDefinitionId", FinalAccount, "Final account")
    ];

    public static QuantitySurveyWorkflowBindingDefinition GetRequired(string decisionKey, string field)
        => Bindings.SingleOrDefault(value =>
               string.Equals(value.DecisionKey, decisionKey, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(value.ConfigurationField, field, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException($"No QS workflow binding is registered for {decisionKey}.{field}.");
}

/// <summary>
/// Contract implemented by governed QS records that participate in the shared workflow engine.
/// </summary>
public interface IQuantitySurveyWorkflowRecord
{
    string Status { get; set; }
    string ApprovalStatus { get; set; }
    Guid? ApprovedById { get; set; }
    DateTime? ApprovedAt { get; set; }
    string? RejectionReason { get; set; }
}

public sealed class QuantitySurveyWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } =
        QuantitySurveyWorkflowBindingRegistry.EntityTypes
            .SelectMany(value => new[] { value.Code, value.Name })
            .ToArray();

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, userId, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var record = Require(entity);
        record.Status = "Draft";
        record.ApprovalStatus = "Draft";
        record.ApprovedById = null;
        record.ApprovedAt = null;
        record.RejectionReason = reason;
    }

    private static void Apply(
        IQuantitySurveyWorkflowRecord record,
        WorkflowOutcome outcome,
        Guid? userId,
        string? rejectionReason)
    {
        record.Status = outcome switch
        {
            WorkflowOutcome.Approved => "Approved",
            WorkflowOutcome.Rejected => "Rejected",
            _ => "PendingApproval"
        };
        record.ApprovalStatus = outcome switch
        {
            WorkflowOutcome.Approved => "Approved",
            WorkflowOutcome.Rejected => "Rejected",
            _ => "Pending"
        };
        record.ApprovedById = outcome == WorkflowOutcome.Approved ? userId : null;
        record.ApprovedAt = outcome == WorkflowOutcome.Approved ? DateTime.UtcNow : null;
        record.RejectionReason = outcome == WorkflowOutcome.Rejected ? rejectionReason : null;
    }

    private static IQuantitySurveyWorkflowRecord Require(object entity)
        => entity as IQuantitySurveyWorkflowRecord
           ?? throw new InvalidOperationException("QS workflow adapter expected an IQuantitySurveyWorkflowRecord entity.");
}
