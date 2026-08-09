using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Services.DocumentManagement;

public sealed class CentralDocumentManagementService : ICentralDocumentManagementService
{
    private static readonly CentralDocumentWorkspaceItem[] Workspaces =
    [
        new("Document Register", "CentralDocumentRegister", "Source: Central DMS", "Central register for documents received from ERP modules, with source labels, repository status, metadata completeness, relationships, and lifecycle state routed through configured workflows.", "FileText", 0, "slate"),
        new("Metadata Template Management", "CentralDocumentMetadataTemplate", "Source: Central DMS -> All ERP Modules", "Define required metadata per module and document type through DMS configuration and configured workflows.", "BookTemplate", 0, "cyan"),
        new("Version & Revision Control", "CentralDocumentVersion", "Source: Central DMS", "Own document versions, revisions, superseded copies, rendition references, comparison readiness, and publication history through configured workflows.", "GitBranch", 0, "violet"),
        new("Access, Retention & Audit", "CentralDocumentGovernance", "Source: Central DMS -> Administration / Legal / Records", "Control repository access, confidentiality, retention rules, legal holds, archival decisions, expiry review, and audit evidence through configured workflows.", "ShieldCheck", 0, "emerald"),
        new("Module Integration Queue", "CentralDocumentIntegrationQueue", "Source: ERP Modules -> Central DMS", "Receive document-ready records from ERP modules and process them through configured DMS workflows.", "Workflow", 0, "indigo")
    ];

    private static readonly CentralDocumentMetadataTemplate[] Templates =
    [
        new("Estate", "Registry file movement", "EST-REG-FILE", ["Estate file reference", "Property number", "Applicant / lessee", "Incoming date", "Source department", "Current officer", "Dispatch status"], ["Registry", "Records", "Applicant", "HOE", "Officer"], "Permanent or departmental records review", "Estate + Records", "Source: Estate - Registry -> Central DMS"),
        new("Estate", "Records amendment", "EST-REC-AMD", ["Estate file reference", "Property number", "Lessee / tenant", "Amendment type", "Revenue verification", "Approval reference", "Agency notification status"], ["Records", "Revenue", "Property", "Lessee", "Agency notification"], "Permanent estate records", "Estate Records restricted", "Source: Estate - Records -> Central DMS"),
        new("Estate", "Lease / transfer / assignment control", "EST-LEASE-XFER", ["Estate file reference", "Property number", "Party / transferee", "Transaction type", "Legal reference", "Finance reference", "Registration status"], ["Lease", "Transfer", "Assignment", "Legal", "Finance", "Records"], "Permanent legal and estate records", "Estate + Legal restricted", "Source: Estate - Lease / Transfer / Assignment -> Central DMS"),
        new("Estate", "Allocation / offer / right of entry", "EST-ALLOC-ROE", ["Property number", "Allocation schedule", "Applicant", "Land use", "LMF reference", "Ground rent reference", "Offer / ROE status"], ["Allocation", "Serviced Plot", "Traditional Land", "HOS", "Finance", "Records"], "Permanent allocation records", "Estate + Finance", "Source: Estate - Allocation / Offer / ROE -> Central DMS"),
        new("Estate", "Tenancy regularisation", "EST-REGULAR", ["Regularisation area", "Plot number", "Applicant", "Interview reference", "Committee decision", "Planning site plan reference", "Payment confirmation"], ["Regularisation", "Planning", "Revenue", "Committee", "Records"], "Permanent regularisation records", "Estate + Planning + Records", "Source: Estate - Regularisation -> Central DMS"),
        new("Planning", "Planning application / site report", "PLN-APP-SITE", ["Planning reference", "Applicant / requester", "Plot or parcel reference", "Location", "Assigned officer", "Site visit date", "Recommendation status"], ["Site report", "Inspection", "Vetting", "Compliance", "Complaint"], "Permanent planning records", "Planning + Records", "Source: Planning -> Central DMS"),
        new("Planning", "Layout / site plan version", "PLN-LAYOUT", ["Planning reference", "Layout or site plan number", "Version", "Prepared by", "Reviewed by", "Approval reference", "Superseded version"], ["Layout", "Site plan", "Drawing office", "Version control", "Approval"], "Permanent layout and site plan records", "Planning restricted", "Source: Planning Layout / Site Plan -> Central DMS"),
        new("Planning", "Official search / data response", "PLN-SEARCH", ["Search reference", "Consent reference", "Applicant", "Site plan reference", "Planning records searched", "Estate cross-check", "Revenue cross-check"], ["Official search", "Planning data", "Estate cross-check", "Revenue cross-check"], "Permanent search response records", "Planning + Estate + Revenue", "Source: Planning Search -> Central DMS"),
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
        new("DMS-PLN-0004", "Planning site report and layout evidence", "Planning", "PlanningSiteReport: PLN-SITE-031", "PLN-APP-SITE", "v1.0", "Linked", "No annotations", "No open comments", "Permanent planning records", "Source: Planning -> Central DMS"),
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
                new("Planning", "Source: Planning -> Central DMS", 8, 4, 3, 2),
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
            [],
            [],
            [],
            []);
}
