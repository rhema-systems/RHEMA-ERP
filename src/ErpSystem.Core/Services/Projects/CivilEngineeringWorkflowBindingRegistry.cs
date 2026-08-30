using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Services.Projects;

public sealed record CivilEngineeringWorkflowEntityTypeDefinition(
    string Code,
    string Name,
    string Description,
    string AuthoritativeOwner);

public sealed record CivilEngineeringWorkflowBindingDefinition(
    string DecisionKey,
    string ConfigurationField,
    string EntityTypeCode,
    string RecordFamily,
    string AuthoritativeOwner);

public static class CivilEngineeringWorkflowBindingRegistry
{
    public const string DesignReview = "PROJECT_DESIGN_REVIEW";
    public const string SiteInstruction = "PROJECT_SITE_INSTRUCTION";
    public const string Rfi = "PROJECT_RFI";
    public const string QualityTest = "PROJECT_QUALITY_TEST";
    public const string WeeklyReport = "PROJECT_WEEKLY_REPORT";
    public const string MaintenanceAssessment = "PROJECT_MAINTENANCE_ASSESSMENT";
    public const string MaintenanceCosting = "PROJECT_MAINTENANCE_COSTING";
    public const string ComplaintCosting = "PROJECT_COMPLAINT_COSTING";
    public const string ContractorEngagement = "PROJECT_CONTRACTOR_ENGAGEMENT";
    public const string PermittingReview = "PROJECT_PERMITTING_REVIEW";
    public const string DirectTask = "PROJECT_TASK";
    public const string ExtensionOfTime = "PROJECT_CIVIL_EXTENSION_OF_TIME";

    public static IReadOnlyList<CivilEngineeringWorkflowEntityTypeDefinition> EntityTypes { get; } =
    [
        new(DesignReview, "Project Civil Design Review", "Civil Engineering reconnaissance, design, drafting review and HOD decision lifecycle.", "Projects / Civil Engineering"),
        new(SiteInstruction, "Project Civil Site Instruction", "Civil Engineering site-instruction issue, acknowledgement and closure lifecycle.", "Projects"),
        new(Rfi, "Project Civil RFI", "Civil Engineering request-for-information response and review lifecycle.", "Projects"),
        new(QualityTest, "Project Civil Quality Test", "Civil Engineering quality or laboratory test review and endorsement lifecycle.", "Projects / QA"),
        new(WeeklyReport, "Project Civil Weekly Report", "Civil Engineering weekly report submission, escalation and approval lifecycle.", "Projects / Civil Engineering"),
        new(MaintenanceAssessment, "Project Civil Maintenance Assessment", "Civil Engineering asset/building assessment and scope recommendation lifecycle.", "Projects / Maintenance"),
        new(MaintenanceCosting, "Project Civil Maintenance Costing", "Civil Engineering maintenance-scope costing and approval handoff lifecycle.", "Projects / Quantity Survey"),
        new(ComplaintCosting, "Project Civil Complaint Costing", "Civil Engineering company-asset complaint costing and approval handoff lifecycle.", "Projects / Quantity Survey"),
        new(ContractorEngagement, "Project Civil Contractor Engagement", "Approved Civil scope handoff to Procurement and contractor engagement lifecycle.", "Procurement / Contracts"),
        new(PermittingReview, "Project Civil Permitting Review", "Civil Engineering permitting handoff, comment, recommendation and HOD decision lifecycle.", "Projects / Building Inspectorate"),
        new(DirectTask, "Project Civil Direct Task", "Civil Engineering technical assignment, feedback and closure-acceptance lifecycle.", "Projects"),
        new(ExtensionOfTime, "Project Civil Extension of Time", "Civil Engineering extension-of-time review over the authoritative Projects EOT and QS variation owners.", "Projects / Quantity Survey / Procurement")
    ];

    public static IReadOnlyList<CivilEngineeringWorkflowBindingDefinition> Bindings { get; } =
    [
        new("CIV-CFG-003", "workflowDefinitionId", DesignReview, "Design review", "Projects / Civil Engineering"),
        new("CIV-CFG-005", "siteInstructionWorkflowDefinitionId", SiteInstruction, "Site instruction", "Projects"),
        new("CIV-CFG-005", "rfiWorkflowDefinitionId", Rfi, "RFI response", "Projects"),
        new("CIV-CFG-005", "testReportWorkflowDefinitionId", QualityTest, "Quality test report", "Projects / QA"),
        new("CIV-CFG-005", "interimCertificateWorkflowDefinitionId", QuantitySurveyWorkflowBindingRegistry.PaymentCertificate, "Interim payment certificate", "Quantity Survey"),
        new("CIV-CFG-006", "workflowDefinitionId", WeeklyReport, "Weekly supervision report", "Projects / Civil Engineering"),
        new("CIV-CFG-007", "workflowDefinitionId", MaintenanceAssessment, "Maintenance or complaint assessment", "Projects / Maintenance"),
        new("CIV-CFG-008", "maintenanceWorkflowDefinitionId", MaintenanceCosting, "Maintenance costing", "Projects / Quantity Survey"),
        new("CIV-CFG-008", "complaintWorkflowDefinitionId", ComplaintCosting, "Complaint costing", "Projects / Quantity Survey"),
        new("CIV-CFG-008", "contractorWorkflowDefinitionId", ContractorEngagement, "Contractor engagement", "Procurement / Contracts"),
        new("CIV-CFG-009", "workflowDefinitionId", PermittingReview, "Permitting review", "Projects / Building Inspectorate"),
        new("CIV-CFG-010", "workflowDefinitionId", DirectTask, "Direct technical assignment", "Projects"),
        new("CIV-CFG-011", "workflowDefinitionId", QualityTest, "Quality test report", "Projects / QA"),
        new("CIV-CFG-014", "workflowDefinitionId", ExtensionOfTime, "Extension of time", "Projects / Quantity Survey / Procurement")
    ];

    public static CivilEngineeringWorkflowBindingDefinition GetRequired(string decisionKey, string field)
        => Bindings.SingleOrDefault(value =>
               string.Equals(value.DecisionKey, decisionKey, StringComparison.OrdinalIgnoreCase)
               && string.Equals(value.ConfigurationField, field, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException(
               $"No Civil Engineering workflow binding is registered for {decisionKey}.{field}.");
}

/// <summary>
/// Contract for governed Civil extension records that use the central workflow engine.
/// The workflow instance and activity log remain authoritative; records keep only their projected status.
/// </summary>
public interface ICivilEngineeringWorkflowRecord
{
    string Status { get; set; }
    string ApprovalStatus { get; set; }
    Guid? ApprovedById { get; set; }
    DateTime? ApprovedAt { get; set; }
    string? RejectionReason { get; set; }
}

public sealed class CivilEngineeringWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } =
        CivilEngineeringWorkflowBindingRegistry.EntityTypes
            .SelectMany(value => new[] { value.Code, value.Name })
            .ToArray();

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId, null);

    public void ApplyApprovalOutcome(
        object entity,
        WorkflowOutcome outcome,
        Guid? userId,
        string? rejectionReason = null)
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
        ICivilEngineeringWorkflowRecord record,
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

    private static ICivilEngineeringWorkflowRecord Require(object entity)
        => entity as ICivilEngineeringWorkflowRecord
           ?? throw new InvalidOperationException(
               "Civil Engineering workflow adapter expected an ICivilEngineeringWorkflowRecord entity.");
}
