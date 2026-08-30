namespace ErpSystem.Core.Services.Projects;

public sealed record CivilEngineeringPermissionDefinition(string Code, string Name, string Description);
public sealed record CivilEngineeringRoleDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions);
public sealed record CivilEngineeringOperationAccessDefinition(
    string Operation,
    string Permission,
    bool RequiresProjectScope,
    bool RequiresAssetOrBuildingScope,
    bool RequiresSectionScope,
    bool RequiresAssignment,
    bool RequiresConfiguredAuthority,
    bool RequiresIndependentChecker);
public sealed record CivilEngineeringAccessScopeFacts(
    bool IsTenantMatch,
    bool HasPermission,
    bool HasProjectScope,
    bool HasAssetOrBuildingScope,
    bool HasSectionScope,
    bool IsAssigned,
    bool HasConfiguredAuthority,
    bool IsIndependentChecker);
public sealed record CivilEngineeringAccessDecision(bool IsAllowed, string Code, string Message)
{
    public static CivilEngineeringAccessDecision Allowed { get; } =
        new(true, "CIVIL_ACCESS_ALLOWED", "The Civil Engineering operation is permitted within the assigned scope.");
}

public static class CivilEngineeringAccessControlRegistry
{
    public const string Category = "Civil Engineering";
    public const string CentralProjectAccess = "project.access";

    public const string ConfigurationRead = "civil-engineering.configuration.read";
    public const string ConfigurationManage = "civil-engineering.configuration.manage";
    public const string ConfigurationApprove = "civil-engineering.configuration.approve";
    public const string AuditRead = "civil-engineering.audit.read";
    public const string WorkspaceRead = "civil-engineering.workspace.read";
    public const string DesignManage = "civil-engineering.design.manage";
    public const string DesignInputRespond = "civil-engineering.design-input.respond";
    public const string SupervisionManage = "civil-engineering.supervision.manage";
    public const string CommercialManage = "civil-engineering.commercial.manage";
    public const string MaintenanceManage = "civil-engineering.maintenance.manage";
    public const string PermittingManage = "civil-engineering.permitting.manage";
    public const string AssignmentsManage = "civil-engineering.assignments.manage";
    public const string AssignedWorkManage = "civil-engineering.assigned-work.manage";
    public const string DocumentsManage = "civil-engineering.documents.manage";
    public const string TransactionsApprove = "civil-engineering.transactions.approve";
    public const string ReportsRead = "civil-engineering.reports.read";
    public const string ReportsExport = "civil-engineering.reports.export";
    public const string MigrationManage = "civil-engineering.migration.manage";
    public const string ExternalAccessManage = "civil-engineering.external-access.manage";

    public const string HeadRole = "TDC_HEAD_OF_CIVIL_ENGINEERING";
    public const string SupervisingEngineerRole = "TDC_SUPERVISING_CIVIL_ENGINEER";
    public const string CivilEngineerRole = "TDC_CIVIL_ENGINEER";
    public const string ProjectEngineerRole = "TDC_PROJECT_ENGINEER";
    public const string DraftsmanRole = "TDC_DRAFTSMAN";
    public const string TechnicianRole = "TDC_CIVIL_TECHNICIAN";
    public const string ArtisanRole = "TDC_CIVIL_ARTISAN";

    public static IReadOnlyList<CivilEngineeringPermissionDefinition> Permissions { get; } =
    [
        new(ConfigurationRead, "Read Civil Engineering configuration", "View effective and historical Civil Engineering configuration profiles."),
        new(ConfigurationManage, "Manage Civil Engineering configuration", "Create and edit draft Civil Engineering configuration profiles and evidence."),
        new(ConfigurationApprove, "Approve Civil Engineering configuration", "Approve decisions and publish or retire Civil Engineering configuration profiles."),
        new(AuditRead, "Read Civil Engineering audit", "View immutable Civil Engineering configuration and transaction audit history."),
        new(WorkspaceRead, "Read Civil Engineering workspace", "View Civil Engineering records only where central tenant and project access also allow it."),
        new(DesignManage, "Manage Civil Engineering designs", "Prepare and review governed reconnaissance, design, drawing and specification records within assigned projects."),
        new(DesignInputRespond, "Respond to Civil Engineering design inputs", "Respond to a Civil design-input request only for the user's current HR section, with governed DMS evidence."),
        new(SupervisionManage, "Manage Civil Engineering supervision", "Manage site supervision, RFIs, instructions, quality evidence and progress reports within assigned projects."),
        new(CommercialManage, "Manage Civil Engineering commercial controls", "Prepare and review Civil extension-of-time controls that link to authoritative QS variation, Finance budget and Procurement contract owners."),
        new(MaintenanceManage, "Manage Civil Engineering maintenance", "Assess and supervise maintenance or complaint work for linked and permitted assets or buildings."),
        new(PermittingManage, "Manage Civil Engineering permitting reviews", "Prepare controlled engineering comments and recommendations for assigned development or permitting files."),
        new(AssignmentsManage, "Manage Civil Engineering assignments", "Assign and reassign governed Civil Engineering work within authorized projects and sections."),
        new(AssignedWorkManage, "Manage assigned Civil Engineering work", "Update only Civil Engineering tasks assigned through the shared Projects assignment controls."),
        new(DocumentsManage, "Manage Civil Engineering documents", "Attach protected central-DMS document and version references within authorized Civil records."),
        new(TransactionsApprove, "Approve Civil Engineering transactions", "Approve governed Civil Engineering records within configured authority and assigned record scope."),
        new(ReportsRead, "Read Civil Engineering reports", "View Civil Engineering reports and drilldowns within assigned project, asset/building and section scope."),
        new(ReportsExport, "Export Civil Engineering reports", "Export Civil Engineering reports only within assigned project, asset/building and section scope."),
        new(MigrationManage, "Manage Civil Engineering migration staging", "Stage, reconcile and independently sign off Civil migration batches within an assigned project. It does not grant owner-record posting."),
        new(ExternalAccessManage, "Manage Civil Engineering external access", "Manage contractor and consultant access through the shared Projects external-access controls.")
    ];

    public static IReadOnlyList<CivilEngineeringRoleDefinition> Roles { get; } =
    [
        new(HeadRole, "TDC Head of Civil Engineering", "Owns Civil Engineering governance and approvals within configured authority and assigned Development scope.",
            [ConfigurationRead, ConfigurationManage, ConfigurationApprove, AuditRead, WorkspaceRead, DesignManage, DesignInputRespond, SupervisionManage, CommercialManage, MaintenanceManage, PermittingManage, AssignmentsManage, AssignedWorkManage, DocumentsManage, TransactionsApprove, ReportsRead, ReportsExport, MigrationManage, ExternalAccessManage]),
        new(SupervisingEngineerRole, "TDC Supervising Civil Engineer", "Reviews, assigns and supervises Civil Engineering work within configured authority and assigned scope.",
            [ConfigurationRead, ConfigurationManage, AuditRead, WorkspaceRead, DesignManage, DesignInputRespond, SupervisionManage, CommercialManage, MaintenanceManage, PermittingManage, AssignmentsManage, AssignedWorkManage, DocumentsManage, TransactionsApprove, ReportsRead, ReportsExport, MigrationManage, ExternalAccessManage]),
        new(CivilEngineerRole, "TDC Civil Engineer", "Prepares and supervises Civil Engineering work within assigned projects, assets/buildings, sections and tasks.",
            [ConfigurationRead, AuditRead, WorkspaceRead, DesignManage, DesignInputRespond, SupervisionManage, CommercialManage, MaintenanceManage, PermittingManage, AssignmentsManage, AssignedWorkManage, DocumentsManage, ReportsRead, ReportsExport]),
        new(ProjectEngineerRole, "TDC Project Engineer", "Manages field supervision and engineering evidence within assigned projects and tasks.",
            [ConfigurationRead, WorkspaceRead, SupervisionManage, MaintenanceManage, AssignedWorkManage, DocumentsManage, ReportsRead]),
        new(DraftsmanRole, "TDC Draftsman", "Prepares controlled drawings and design evidence only within assigned projects and tasks.",
            [ConfigurationRead, WorkspaceRead, DesignManage, AssignedWorkManage, DocumentsManage, ReportsRead]),
        new(TechnicianRole, "TDC Civil Technician", "Performs and reports assigned technical work within the shared Projects task controls.",
            [ConfigurationRead, WorkspaceRead, AssignedWorkManage, DocumentsManage, ReportsRead]),
        new(ArtisanRole, "TDC Civil Artisan", "Performs and reports assigned field work within the shared Projects task controls.",
            [WorkspaceRead, AssignedWorkManage, DocumentsManage])
    ];

    public static IReadOnlyList<CivilEngineeringOperationAccessDefinition> Operations { get; } =
    [
        new("ViewWorkspace", WorkspaceRead, true, false, false, false, false, false),
        new("ManageDesign", DesignManage, true, false, true, false, false, false),
        new("RespondDesignInput", DesignInputRespond, false, false, true, false, false, false),
        new("ManageSupervision", SupervisionManage, true, false, true, false, false, false),
        new("ManageCommercial", CommercialManage, true, false, true, false, false, false),
        new("ManageMaintenance", MaintenanceManage, true, true, true, false, false, false),
        new("ManagePermitting", PermittingManage, true, true, true, false, false, false),
        new("ManageAssignments", AssignmentsManage, true, false, true, false, false, false),
        new("ManageAssignedWork", AssignedWorkManage, true, false, false, true, false, false),
        new("ManageDocuments", DocumentsManage, true, false, false, false, false, false),
        new("ApproveTransaction", TransactionsApprove, true, false, true, false, true, true),
        new("ReadReports", ReportsRead, true, false, true, false, false, false),
        new("ExportReports", ReportsExport, true, false, true, false, false, false),
        new("ManageMigration", MigrationManage, true, false, true, false, true, false),
        new("ManageExternalAccess", ExternalAccessManage, true, false, true, false, true, false),
        new("ReadConfiguration", ConfigurationRead, false, false, false, false, false, false),
        new("ManageConfiguration", ConfigurationManage, false, false, false, false, false, false),
        new("ApproveConfiguration", ConfigurationApprove, false, false, false, false, true, true),
        new("ReadAudit", AuditRead, false, false, false, false, false, false)
    ];

    public static CivilEngineeringOperationAccessDefinition GetOperation(string operation)
        => Operations.SingleOrDefault(value => string.Equals(value.Operation, operation, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Civil Engineering access operation '{operation}' is not registered.");

    public static CivilEngineeringAccessDecision Evaluate(
        CivilEngineeringOperationAccessDefinition operation,
        CivilEngineeringAccessScopeFacts facts)
    {
        if (!facts.IsTenantMatch)
            return Denied("CIVIL_TENANT_SCOPE_DENIED", "The record is outside the active tenant.");
        if (!facts.HasPermission)
            return Denied("CIVIL_PERMISSION_DENIED", $"Permission '{operation.Permission}' is required.");
        if (operation.RequiresProjectScope && !facts.HasProjectScope)
            return Denied("CIVIL_PROJECT_SCOPE_DENIED", "The user is not assigned to the record's project.");
        if (operation.RequiresAssetOrBuildingScope && !facts.HasAssetOrBuildingScope)
            return Denied("CIVIL_ASSET_BUILDING_SCOPE_DENIED", "The asset or building is not linked to the permitted project scope.");
        if (operation.RequiresSectionScope && !facts.HasSectionScope)
            return Denied("CIVIL_SECTION_SCOPE_DENIED", "The record is outside the user's permitted department or section scope.");
        if (operation.RequiresAssignment && !facts.IsAssigned)
            return Denied("CIVIL_ASSIGNMENT_SCOPE_DENIED", "The task is not assigned to the current user.");
        if (operation.RequiresConfiguredAuthority && !facts.HasConfiguredAuthority)
            return Denied("CIVIL_CONFIGURED_AUTHORITY_DENIED", "The user is outside the effective Civil Engineering authority policy.");
        if (operation.RequiresIndependentChecker && !facts.IsIndependentChecker)
            return Denied("CIVIL_MAKER_CHECKER_CONFLICT", "The maker cannot approve the same governed record.");

        return CivilEngineeringAccessDecision.Allowed;
    }

    public static bool IsManagementProjectRole(string? role)
        => NormalizeRole(role) is "tdcheadofcivilengineering" or "tdcsupervisingcivilengineer";

    public static bool IsExecutionProjectRole(string? role)
        => NormalizeRole(role) is "tdccivilengineer" or "tdcprojectengineer" or "tdcdraftsman" or "tdcciviltechnician" or "tdccivilartisan";

    private static CivilEngineeringAccessDecision Denied(string code, string message)
        => new(false, code, message);

    private static string NormalizeRole(string? role)
        => string.IsNullOrWhiteSpace(role)
            ? string.Empty
            : string.Concat(role.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
