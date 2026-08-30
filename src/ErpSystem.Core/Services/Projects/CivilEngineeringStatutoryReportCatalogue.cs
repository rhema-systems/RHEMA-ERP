using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Services.Projects;

public sealed record CivilEngineeringSystemReportDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<string> Tags,
    string? SourceCode = null)
{
    public string Query => CivilEngineeringStatutoryReportCatalogue.QueryPrefix + Code;
    public string EffectiveSourceCode => SourceCode ?? Code;
}

/// <summary>
/// Static, tenant-safe report definitions for Civil Engineering.  The shared report engine owns
/// catalogue visibility, execution history and export delivery; source modules remain authoritative.
/// </summary>
public static class CivilEngineeringStatutoryReportCatalogue
{
    public const string QueryPrefix = "system://tdc/civil-engineering/";
    public const string ReportType = "civil-engineering";
    public const string ReadPermission = CivilEngineeringAccessControlRegistry.ReportsRead;
    public const string ExportPermission = CivilEngineeringAccessControlRegistry.ReportsExport;

    public const string DesignBacklogCode = "design-backlog";
    public const string FieldTaskRegisterCode = "field-task-register";
    public const string SupervisionControlCode = "supervision-controls";
    public const string MaintenanceComplaintCode = "maintenance-complaints";
    public const string PermittingWatchCode = "permitting-watch";
    public const string CompletionHandoverCode = "completion-handover";

    // Architecture and Design Document section 16.5. These codes intentionally expose the
    // exact named Civil Engineering outputs while reusing the authoritative source records
    // behind the consolidated operator reports above.
    public const string EngineeringWorkRegisterCode = "engineering-work-register";
    public const string InspectionReportCode = "inspection-report";
    public const string SiteInstructionLogCode = "site-instruction-log";
    public const string ProgressReportCode = "progress-report";
    public const string DefectReportCode = "defect-report";
    public const string CompletionCertificateReportCode = "completion-certificate-report";
    public const string ProjectDashboardCode = "project-dashboard";
    public const string EngineeringAuditTrailCode = "engineering-audit-trail";

    public static IReadOnlyList<CivilEngineeringSystemReportDefinition> Definitions { get; } =
    [
        Definition(DesignBacklogCode, "Design Backlog",
            "Reconciles governed design cases and versioned engineering documents for the selected project.",
            C("ProjectCode", "Project code"), C("RecordType", "Record type"), C("Reference", "Reference"),
            C("Title", "Title"), C("Stage", "Stage"), C("Status", "Status"),
            C("ApprovalStatus", "Approval status"), C("Assignee", "Assignee"), C("DueDate", "Due date", "DateTime"),
            C("IsOverdue", "Overdue", "Boolean"), C("RecordedAt", "Recorded", "DateTime")),
        Definition(FieldTaskRegisterCode, "Field Task Register",
            "Reconciles governed Civil assignments, including drafting workload, field feedback, completion and overdue exposure.",
            C("ProjectCode", "Project code"), C("TaskTitle", "Task"), C("AssignedTo", "Assigned to"),
            C("AssignedRole", "Assigned role"), C("Urgency", "Urgency"), C("Status", "Status"),
            C("ApprovalStatus", "Approval status"), C("ProgressPercent", "Progress", "Decimal", "N2"),
            C("DueDate", "Due date", "DateTime"), C("IsOverdue", "Overdue", "Boolean"),
            C("RecordedAt", "Recorded", "DateTime")),
        Definition(SupervisionControlCode, "Supervision Controls",
            "Reconciles RFIs, site instructions, weekly supervision reports, quality tests and IPC endorsements.",
            C("ProjectCode", "Project code"), C("RecordType", "Record type"), C("Reference", "Reference"),
            C("Title", "Title"), C("Status", "Status"), C("ApprovalStatus", "Approval status"),
            C("DueDate", "Due date", "DateTime"), C("IsOverdue", "Overdue", "Boolean"),
            C("EvidenceStatus", "Evidence status"), C("RecordedAt", "Recorded", "DateTime")),
        Definition(MaintenanceComplaintCode, "Maintenance and Complaint Register",
            "Reconciles Civil intake, assessment, costing, owner execution and completion controls for the selected project.",
            C("ProjectCode", "Project code"), C("RecordType", "Record type"), C("Reference", "Reference"),
            C("Title", "Title"), C("Stage", "Stage"), C("Status", "Status"),
            C("ApprovalStatus", "Approval status"), C("DueDate", "Due date", "DateTime"),
            C("IsOverdue", "Overdue", "Boolean"), C("RecordedAt", "Recorded", "DateTime")),
        Definition(PermittingWatchCode, "Permitting Watch",
            "Reconciles development-approval files, section handoffs, engineering reviews and HOD outcomes.",
            C("ProjectCode", "Project code"), C("FileNumber", "File number"), C("ApplicationReference", "Application reference"),
            C("RecordType", "Record type"), C("CurrentSection", "Current section"), C("Stage", "Stage"),
            C("Status", "Status"), C("DueDate", "Due date", "DateTime"), C("IsOverdue", "Overdue", "Boolean"),
            C("RecordedAt", "Recorded", "DateTime")),
        Definition(CompletionHandoverCode, "Completion and Handover Register",
            "Reconciles governed Civil inspection, snag, handover, as-built, closeout and authoritative asset-history links for the selected project.",
            C("ProjectCode", "Project code"), C("RecordType", "Record type"), C("Reference", "Reference"),
            C("Title", "Title"), C("Stage", "Stage"), C("Status", "Status"), C("ApprovalStatus", "Approval status"),
            C("DueDate", "Due date", "DateTime"), C("IsOverdue", "Overdue", "Boolean"),
            C("EvidenceStatus", "Evidence status"), C("RecordedAt", "Recorded", "DateTime")),
        ArchitectureDefinition(EngineeringWorkRegisterCode, "Engineering Work Register",
            "Registers governed Civil design, maintenance, complaint and execution work for the selected project.",
            EngineeringWorkRegisterCode,
            C("ProjectCode", "Project code"), C("RecordType", "Record type"), C("Reference", "Reference"),
            C("Title", "Title"), C("Stage", "Stage"), C("Status", "Status"), C("ApprovalStatus", "Approval status"),
            C("DueDate", "Due date", "DateTime"), C("IsOverdue", "Overdue", "Boolean"), C("RecordedAt", "Recorded", "DateTime")),
        ArchitectureDefinition(InspectionReportCode, "Inspection Report",
            "Lists governed Civil inspections, outcomes, evidence and closure status for the selected project.",
            CompletionHandoverCode, CompletionColumns()),
        ArchitectureDefinition(SiteInstructionLogCode, "Site Instruction Log",
            "Lists governed site instructions, issue status, response status and retained evidence for the selected project.",
            SupervisionControlCode, SupervisionColumns()),
        ArchitectureDefinition(ProgressReportCode, "Progress Report",
            "Lists governed weekly Civil supervision progress, overdue exposure and retained evidence for the selected project.",
            SupervisionControlCode, SupervisionColumns()),
        ArchitectureDefinition(DefectReportCode, "Defect Report",
            "Lists snags, defects, corrective status, due dates and closure evidence for the selected project.",
            CompletionHandoverCode, CompletionColumns()),
        ArchitectureDefinition(CompletionCertificateReportCode, "Completion Certificate Report",
            "Lists completion, handover, closeout and project-closure records with approval and evidence status.",
            CompletionCertificateReportCode, CompletionColumns()),
        ArchitectureDefinition(ProjectDashboardCode, "Project Dashboard",
            "Summarises Civil design, field, supervision, maintenance, permitting and closeout exposure for the selected project.",
            ProjectDashboardCode,
            C("ProjectCode", "Project code"), C("Area", "Area"), C("TotalRecords", "Total records", "Int32"),
            C("OverdueRecords", "Overdue", "Int32"), C("PendingRecords", "Pending", "Int32"),
            C("LastActivityAt", "Last activity", "DateTime")),
        ArchitectureDefinition(EngineeringAuditTrailCode, "Engineering Audit Trail",
            "Lists tenant- and project-scoped Civil Engineering actions from the shared immutable audit log.",
            EngineeringAuditTrailCode,
            C("ProjectCode", "Project code"), C("Action", "Action"), C("Resource", "Resource"),
            C("ResourceId", "Resource ID"), C("Actor", "Actor"), C("OccurredAt", "Occurred", "DateTime"),
            C("CorrelationId", "Correlation ID"))
    ];

    public static CivilEngineeringSystemReportDefinition? Resolve(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || !query.StartsWith(QueryPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        var code = query[QueryPrefix.Length..].Trim();
        return Definitions.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, object> BuildParameters() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["projectId"] = Parameter("Project", "project", true),
        ["startDate"] = Parameter("Start date", "date", false),
        ["endDate"] = Parameter("End date", "date", false)
    };

    private static CivilEngineeringSystemReportDefinition Definition(string code, string name, string description, params ReportColumnDto[] columns) =>
        new(code, name, description, columns, ["statutory", "civil-engineering", code]);

    private static CivilEngineeringSystemReportDefinition ArchitectureDefinition(
        string code,
        string name,
        string description,
        string sourceCode,
        params ReportColumnDto[] columns) =>
        new(code, name, description, columns, ["statutory", "civil-engineering", "architecture-16.5", code], sourceCode);

    private static ReportColumnDto[] SupervisionColumns() =>
    [
        C("ProjectCode", "Project code"), C("RecordType", "Record type"), C("Reference", "Reference"),
        C("Title", "Title"), C("Status", "Status"), C("ApprovalStatus", "Approval status"),
        C("DueDate", "Due date", "DateTime"), C("IsOverdue", "Overdue", "Boolean"),
        C("EvidenceStatus", "Evidence status"), C("RecordedAt", "Recorded", "DateTime")
    ];

    private static ReportColumnDto[] CompletionColumns() =>
    [
        C("ProjectCode", "Project code"), C("RecordType", "Record type"), C("Reference", "Reference"),
        C("Title", "Title"), C("Stage", "Stage"), C("Status", "Status"), C("ApprovalStatus", "Approval status"),
        C("DueDate", "Due date", "DateTime"), C("IsOverdue", "Overdue", "Boolean"),
        C("EvidenceStatus", "Evidence status"), C("RecordedAt", "Recorded", "DateTime")
    ];

    private static ReportColumnDto C(string name, string displayName, string type = "String", string? format = null) =>
        new() { Name = name, DisplayName = displayName, DataType = type, Format = format, IsVisible = true };

    private static Dictionary<string, object> Parameter(string label, string type, bool required) => new()
    {
        ["label"] = label,
        ["type"] = type,
        ["required"] = required
    };
}
