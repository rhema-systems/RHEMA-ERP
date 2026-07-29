using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Services.DocumentManagement;

public sealed class CentralDocumentManagementService : ICentralDocumentManagementService
{
    private static readonly CentralDocumentWorkspaceItem[] Workspaces =
    [
        new("Document Register", "CentralDocumentRegister", "Source: Central DMS", "Central register for documents received from ERP modules, with source labels, repository status, metadata completeness, relationships, and lifecycle state.", "FileText", 8, "slate"),
        new("Metadata Template Management", "CentralDocumentMetadataTemplate", "Source: Central DMS -> All ERP Modules", "Define required metadata per module and document type so Estate, Finance, Legal, HR, Projects, Maintenance, Helpdesk, and Procurement documents are searchable and controlled.", "BookTemplate", 7, "cyan"),
        new("Version & Revision Control", "CentralDocumentVersion", "Source: Central DMS", "Own document versions, revisions, superseded copies, rendition references, comparison readiness, and controlled publication history.", "GitBranch", 7, "violet"),
        new("Access, Retention & Audit", "CentralDocumentGovernance", "Source: Central DMS -> Administration / Legal / Records", "Control repository access, confidentiality, retention rules, legal holds, archival decisions, expiry review, and audit evidence.", "ShieldCheck", 8, "emerald"),
        new("Module Integration Queue", "CentralDocumentIntegrationQueue", "Source: ERP Modules -> Central DMS", "Receive document-ready records from Property, Facilities, Finance, Legal, HR, Projects, Maintenance, Helpdesk, Procurement, Inventory, and Reports.", "Workflow", 7, "indigo")
    ];

    private static readonly CentralDocumentMetadataTemplate[] Templates =
    [
        new("Estate", "Registry file movement", "EST-REG-FILE", ["Estate file reference", "Property number", "Applicant / lessee", "Incoming date", "Source department", "Current officer", "Dispatch status"], ["Registry", "Records", "Applicant", "HOE", "Officer"], "Permanent or departmental records review", "Estate + Records", "Source: Estate - Registry -> Central DMS"),
        new("Estate", "Records amendment", "EST-REC-AMD", ["Estate file reference", "Property number", "Lessee / tenant", "Amendment type", "Revenue verification", "Approval reference", "Agency notification status"], ["Records", "Revenue", "Property", "Lessee", "Agency notification"], "Permanent estate records", "Estate Records restricted", "Source: Estate - Records -> Central DMS"),
        new("Estate", "Lease / transfer / assignment control", "EST-LEASE-XFER", ["Estate file reference", "Property number", "Party / transferee", "Transaction type", "Legal reference", "Finance reference", "Registration status"], ["Lease", "Transfer", "Assignment", "Legal", "Finance", "Records"], "Permanent legal and estate records", "Estate + Legal restricted", "Source: Estate - Lease / Transfer / Assignment -> Central DMS"),
        new("Estate", "Allocation / offer / right of entry", "EST-ALLOC-ROE", ["Property number", "Allocation schedule", "Applicant", "Land use", "LMF reference", "Ground rent reference", "Offer / ROE status"], ["Allocation", "Serviced Plot", "Traditional Land", "HOS", "Finance", "Records"], "Permanent allocation records", "Estate + Finance", "Source: Estate - Allocation / Offer / ROE -> Central DMS"),
        new("Estate", "Tenancy regularisation", "EST-REGULAR", ["Regularisation area", "Plot number", "Applicant", "Interview reference", "Committee decision", "Planning site plan reference", "Payment confirmation"], ["Regularisation", "Planning", "Revenue", "Committee", "Records"], "Permanent regularisation records", "Estate + Planning + Records", "Source: Estate - Regularisation -> Central DMS"),
        new("Estate / Property Management", "Property record", "EST-PROP-REC", ["Property reference", "Unit reference", "Document category", "Source workspace", "Access level", "Retention category"], ["Property", "Unit", "Lease", "Tenant", "Billing", "Project handover"], "Permanent or lifecycle review", "Property + Records", "Source: Estate / Property Management -> Central DMS"),
        new("Estate / Facilities", "Facilities operating document", "EST-FAC-OPS", ["Facilities workspace", "Source module", "Related asset or case", "Expiry date", "Access level", "Annotation requirement"], ["Property", "Asset", "Maintenance", "Complaint", "Provider", "Staff"], "Operational period plus review", "Facilities + Records", "Source: Estate / Facilities -> Central DMS"),
        new("Finance AR", "Invoice / receipt evidence", "FIN-AR-EVD", ["Customer account", "Invoice or receipt reference", "Amount period", "Posting reference", "Document purpose"], ["Customer", "Invoice", "Receipt", "Statement", "Property billing"], "Finance statutory retention", "Finance restricted", "Source: Finance AR -> Central DMS"),
        new("Legal", "Legal instrument", "LEG-INST", ["Matter reference", "Instrument type", "Counterparty", "Execution date", "Effective date", "Legal hold status"], ["Matter", "Lease", "Property", "Customer", "Court process"], "Permanent or legal retention", "Legal restricted", "Source: Legal -> Central DMS"),
        new("Project Management", "Project handover package", "PRJ-HND", ["Project reference", "Deliverable", "Handover batch", "Completion approval", "Receiving module"], ["Project", "Deliverable", "Property", "Asset", "Defect"], "Project closeout retention", "Project + Receiving module", "Source: Project Management -> Central DMS"),
        new("Maintenance Management", "Work order evidence", "MNT-WO-EVD", ["Work order", "Asset", "Location", "Technician or provider", "Completion status", "Inspection result"], ["Work order", "Job card", "Asset", "Property", "Complaint"], "Maintenance service history", "Maintenance + Facilities", "Source: Maintenance Management -> Central DMS")
    ];

    private static readonly CentralDocumentRegisterItem[] RegisterItems =
    [
        new("DMS-EST-CORE-0003", "Estate records amendment support pack", "Estate", "EstateRecordsManagement: EST-REC-024", "EST-REC-AMD", "v1.0", "Linked", "No annotations", "No open comments", "Permanent estate records", "Source: Estate - Records -> Central DMS"),
        new("DMS-EST-PROP-0001", "Property handover certificate batch", "Estate / Property Management", "EstatePropertyManagementPropertyUnit: PM-HND-018", "EST-PROP-REC", "v1.0", "Linked", "No annotations", "No open comments", "Permanent", "Source: Estate / Property Management -> Central DMS"),
        new("DMS-EST-FAC-0007", "Lift service certificate", "Estate / Facilities", "EstateFacilityAssetRegister: FAC-AST-044", "EST-FAC-OPS", "v2.1", "Version pending", "Annotations pending", "Review comments open", "Expiry review due", "Source: Estate / Facilities -> Central DMS"),
        new("DMS-FIN-AR-0012", "Service charge invoice support pack", "Finance AR", "AR Invoice: INV-2026-0418", "FIN-AR-EVD", "v1.0", "Linked", "Not applicable", "No open comments", "Finance retention", "Source: Finance AR -> Central DMS"),
        new("DMS-LEG-0005", "Executed lease instrument", "Legal", "LegalLeaseVariationRenewalSublease: LEG-LEASE-022", "LEG-INST", "v3.0", "Linked", "Annotations locked", "No open comments", "Permanent", "Source: Legal -> Central DMS"),
        new("DMS-PRJ-0009", "Project completion and defects pack", "Project Management", "ProjectClosure: PRJ-CL-014", "PRJ-HND", "v1.2", "Linked", "No annotations", "Two comments open", "Project closeout", "Source: Project Management -> Central DMS")
    ];

    public IReadOnlyList<CentralDocumentWorkspaceItem> GetWorkspaces() => Workspaces;

    public CentralDocumentWorkspace? GetWorkspace(string entityType)
    {
        var item = Workspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        return item is null ? null : BuildWorkspace(item);
    }

    public CentralDocumentDashboard GetDashboard() =>
        new(
            [
                new("Documents indexed", "1,248", "Records linked from source modules", "+86 this month"),
                new("Metadata complete", "78%", "Documents with required module template fields", "+6%"),
                new("Open annotations", "34", "PDF annotation reviews needing action", "-9"),
                new("Retention reviews", "19", "Documents due for archive, legal hold, restriction, or supersede review", "+3")
            ],
            [
                new("Repository linked", "84%", "Documents with DMS repository references", "stable"),
                new("Version controlled", "71%", "Documents with current version or revision reference", "+8%"),
                new("Source labels clean", "92%", "Records with explicit source module labels", "+5%"),
                new("Access classified", "88%", "Documents with confidentiality and access profile", "+4%")
            ],
            [
                new("Estate", "Source: Estate -> Central DMS", 9, 4, 2, 2),
                new("Estate / Property Management", "Source: Estate / Property Management -> Central DMS", 11, 6, 4, 3),
                new("Estate / Facilities", "Source: Estate / Facilities -> Central DMS", 14, 8, 9, 5),
                new("Finance AR", "Source: Finance AR -> Central DMS", 7, 3, 0, 4),
                new("Legal", "Source: Legal -> Central DMS", 5, 5, 8, 2),
                new("Project Management", "Source: Project Management -> Central DMS", 9, 4, 2, 3),
                new("Maintenance Management", "Source: Maintenance Management -> Central DMS", 12, 7, 6, 2)
            ]);

    public IReadOnlyList<CentralDocumentMetadataTemplate> GetMetadataTemplates() => Templates;

    public IReadOnlyList<CentralDocumentRegisterItem> GetRegisterItems() => RegisterItems;

    private static CentralDocumentWorkspace BuildWorkspace(CentralDocumentWorkspaceItem item) =>
        new(
            item,
            Stages(item.EntityType),
            Fields(item.EntityType),
            Outputs(item.EntityType),
            Handoffs(item.EntityType));

    private static IReadOnlyList<CentralDocumentStage> Stages(string entityType) =>
        entityType switch
        {
            "CentralDocumentMetadataTemplate" =>
            [
                Stage("Capture module requirement", "DMS Administrator", "Define the module, document type, source owner, and business purpose.", "Confirm owning module.", "Name the document type.", "Capture source label."),
                Stage("Define required fields", "Records Manager", "Set mandatory fields, optional fields, validation rules, and lookup values.", "Add required metadata.", "Define relationships.", "Confirm search keys."),
                Stage("Set access and retention", "Records / Legal / Security", "Assign confidentiality, role access, retention category, archival rule, and legal hold behavior.", "Set access profile.", "Set retention rule.", "Confirm legal hold handling."),
                Stage("Publish template", "DMS Owner", "Publish the template for module use and downstream validation.", "Approve template.", "Notify module owner.", "Record audit trail.")
            ],
            "CentralDocumentVersion" =>
            [
                Stage("Receive version request", "Document Controller", "Capture a new version, revision, supersede, or replacement request.", "Link source document.", "Capture reason.", "Confirm author."),
                Stage("Validate current state", "Document Controller", "Check current version, lock status, review state, and impacted module references.", "Confirm latest version.", "Check open comments.", "Check legal hold."),
                Stage("Publish version", "DMS Owner", "Publish the approved version, mark superseded copies, and update references.", "Assign version number.", "Generate rendition reference.", "Notify linked modules.")
            ],
            "CentralDocumentGovernance" =>
            [
                Stage("Review access", "Security / Records", "Confirm repository access, confidentiality, sharing rules, and exceptional permissions.", "Review role access.", "Flag restricted records.", "Approve exceptions."),
                Stage("Review lifecycle", "Records Manager", "Evaluate retention, expiry, supersede, archive, destruction, or legal hold action.", "Check retention rule.", "Check legal hold.", "Set lifecycle action."),
                Stage("Complete audit", "DMS Owner", "Close governance review with evidence and source-module notifications.", "Record decision.", "Notify owners.", "Close audit action.")
            ],
            "CentralDocumentIntegrationQueue" =>
            [
                Stage("Receive module package", "DMS Intake", "Receive document-ready metadata from an ERP source module.", "Validate source label.", "Validate source record.", "Check duplicate."),
                Stage("Match template", "DMS Intake", "Match module and document type to the published metadata template.", "Apply template.", "Flag metadata gaps.", "Route correction."),
                Stage("Create DMS reference", "DMS Controller", "Create or link the central document reference, version, access profile, and repository status.", "Create document reference.", "Set version.", "Return DMS reference.")
            ],
            _ =>
            [
                Stage("Register document", "DMS Intake", "Create or link the central document record from a source module.", "Capture source module.", "Capture source record.", "Record source label."),
                Stage("Apply metadata template", "Document Controller", "Apply required module metadata and relationships.", "Select template.", "Capture required fields.", "Flag gaps."),
                Stage("Set repository and version", "Document Controller", "Link repository file, current version, rendition, and review state.", "Set repository status.", "Set version.", "Set rendition reference."),
                Stage("Classify access and retention", "Records Manager", "Set access profile, confidentiality, retention, legal hold, and lifecycle status.", "Classify access.", "Set retention.", "Set lifecycle state."),
                Stage("Publish document record", "DMS Owner", "Publish the DMS reference to linked modules and close the intake.", "Notify module.", "Publish reference.", "Record audit trail.")
            ]
        };

    private static IReadOnlyList<CentralDocumentField> Fields(string entityType) =>
    [
        Field("documentReference", "Central DMS document reference", "text"),
        Field("sourceModule", "Source module", "select", "Estate", "Estate / Property Management", "Estate / Facilities", "Finance AR", "Finance AP", "Legal", "Project Management", "Maintenance Management", "Helpdesk", "Procurement", "HR / Payroll", "Inventory", "Reports"),
        Field("sourceLabel", "Source label", "text"),
        Field("sourceRecordReference", "Source record reference", "text"),
        Field("metadataTemplate", "Metadata template", "select", Templates.Select(item => item.TemplateCode).ToArray()),
        Field("repositoryStatus", "Repository status", "select", "Not linked", "Pending upload", "Linked", "Version pending", "Permission pending", "Migration exception"),
        Field("versionReference", "Version / revision reference", "text"),
        Field("annotationStatus", "PDF viewer annotation status", "select", "Not required", "No annotations", "Annotations pending", "Annotations completed", "Annotations locked", "Returned for action"),
        Field("commentStatus", "Comment status", "select", "No comments", "Open comments", "Returned for action", "Resolved", "Locked"),
        Field("accessProfile", "Access profile", "select", "Open internal", "Module restricted", "Finance restricted", "Legal restricted", "HR restricted", "Management only", "Custom restricted"),
        Field("retentionStatus", "Retention status", "select", "Current", "Review due", "Archive due", "Legal hold", "Superseded", "Destruction approval required"),
        Field("notes", "Document control notes", "textarea")
    ];

    private static IReadOnlyList<string> Outputs(string entityType) =>
    [
        "Central DMS document reference",
        "Published module metadata template",
        "Repository, version, annotation, and comment status",
        "Source module and source record links",
        "Access, retention, legal hold, and lifecycle classification",
        "Audit-ready history for module handoff and document governance"
    ];

    private static IReadOnlyList<CentralDocumentHandoff> Handoffs(string entityType) =>
    [
        new("ERP source module", "Central DMS", "A document requires central repository, versioning, annotation, comments, or retention control"),
        new("Central DMS", "ERP source module", "A DMS reference, version update, comment, annotation, or correction status is available"),
        new("Central DMS", "Administration / Records / Legal", "Access exception, retention review, legal hold, or audit decision is required"),
        new("Records / Legal / Security", "Central DMS", "Governance decision has been approved and must be applied")
    ];

    private static CentralDocumentStage Stage(string name, string owner, string summary, params string[] checklist) =>
        new(name, owner, summary, checklist);

    private static CentralDocumentField Field(string key, string label, string type, params string[] options) =>
        new(key, label, type, options.Length == 0 ? null : options);
}
