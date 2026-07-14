using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class FacilitiesProcedureCatalogService : IFacilitiesProcedureCatalogService
{
    private static readonly FacilitiesProcedureCatalogItem[] Procedures =
    [
        new("Property and Site Management", "EstateFacilityPropertySite", "Facilities Management ERP Module", "Site creation, property records, unit and space capture, occupancy status, and availability monitoring.", "Building2", 5, "teal"),
        new("Lease Management", "EstateFacilityLease", "Facilities Management ERP BRS", "Client details, lease dates, rental amount, renewal alerts, termination tracking, and lease status.", "FileSignature", 7, "sky"),
        new("Maintenance Management", "EstateFacilityMaintenance", "Facilities Management ERP Module", "Maintenance request, complaint logging, priority assignment, contractor assignment, completion, inspection, and closure.", "Wrench", 8, "amber"),
        new("Complaint Management", "EstateFacilityComplaint", "Facilities Management ERP BRS", "Tenant/customer complaints, issue tracking, escalation, resolution updates, and closure reporting.", "MessageSquare", 6, "rose"),
        new("Service Provider Management", "EstateFacilityServiceProvider", "Facilities Management ERP BRS", "Contractor database, service categories, contracts, rates, performance records, and invoice history.", "Briefcase", 6, "violet"),
        new("Staff and Cleaner Management", "EstateFacilityStaffCleaner", "Facilities Management ERP Module", "Facilities staff and cleaner assignment, duty monitoring, attendance support, and supervision workflows.", "ClipboardCheck", 5, "emerald"),
        new("Asset Register", "EstateFacilityAssetRegister", "Facilities Management ERP BRS", "Asset number, description, location, cost, warranty, maintenance history, and current status register.", "Database", 6, "indigo"),
        new("Facilities Document Control", "EstateFacilityDocument", "Facilities Management ERP BRS", "Upload, storage, search, and retrieval of leases, contracts, certificates, invoices, payment records, and property documents.", "FileText", 5, "lime")
    ];

    public IReadOnlyList<FacilitiesProcedureCatalogItem> GetProcedures() => Procedures;

    public FacilitiesProcedureWorkspace? GetProcedureWorkspace(string entityType)
    {
        var procedure = Procedures.FirstOrDefault(item =>
            string.Equals(item.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        if (procedure is null)
        {
            return null;
        }

        return procedure.EntityType switch
        {
            "EstateFacilityPropertySite" => PropertySite(procedure),
            "EstateFacilityLease" => Lease(procedure),
            "EstateFacilityMaintenance" => Maintenance(procedure),
            "EstateFacilityComplaint" => Complaint(procedure),
            "EstateFacilityServiceProvider" => ServiceProvider(procedure),
            "EstateFacilityStaffCleaner" => StaffCleaner(procedure),
            "EstateFacilityAssetRegister" => AssetRegister(procedure),
            "EstateFacilityDocument" => DocumentControl(procedure),
            _ => null
        };
    }

    private static FacilitiesProcedureWorkspace PropertySite(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Register site", "Facilities Officer", "Create the site or property record with location, ownership, and responsible office details.", "Capture site code and name.", "Record GPS, address, and estate linkage.", "Assign responsible facilities officer."),
                Stage("Create units and spaces", "Facilities Officer", "Define buildings, units, rooms, yards, and rentable or serviceable spaces.", "Capture unit type and size.", "Set occupancy capacity.", "Attach floor or site plan where available."),
                Stage("Validate occupancy", "Estate / Facilities Supervisor", "Confirm occupancy, availability, restrictions, and operational readiness.", "Verify current tenant or occupant.", "Mark unit status.", "Record access or condition notes."),
                Stage("Attach documents", "Document Control Officer", "Attach plans, certificates, inspection notes, and property documents.", "Upload supporting documents.", "Tag documents by site and unit.", "Confirm retrieval metadata."),
                Stage("Publish and monitor", "Facilities Manager", "Approve the record for operational use and monitor availability changes.", "Approve record completeness.", "Publish available spaces.", "Review occupancy and status changes.")
            ],
            [
                Doc("Site or property creation request", "Estate / Facilities", true),
                Doc("Site plan or layout", "Estate Records", false),
                Doc("Ownership or allocation reference", "Estate Records", true),
                Doc("Inspection or condition note", "Facilities Supervisor", false)
            ],
            CommonFields("Site / property type"),
            ["Active property record", "Unit and space register", "Availability status", "Document-linked site file"],
            CommonHandoffs("Facilities Officer", "Facilities Manager", "Property record is ready for approval"));

    private static FacilitiesProcedureWorkspace Lease(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Open lease record", "Lease Officer", "Create or update the lease record from approved tenant and property details.", "Capture client details.", "Select property, unit, or space.", "Record lease start and end dates."),
                Stage("Capture commercial terms", "Lease Officer / Finance", "Record rent, service charge, deposit, billing cycle, and renewal terms.", "Capture rental amount.", "Record payment terms.", "Confirm billing owner."),
                Stage("Attach lease documents", "Document Control Officer", "Attach signed lease, variation, renewal, and supporting documents.", "Upload signed lease.", "Tag renewal or variation documents.", "Confirm document visibility."),
                Stage("Configure alerts", "Facilities Supervisor", "Set renewal, rent review, expiry, and termination alerts.", "Set renewal notice dates.", "Set expiry reminders.", "Flag terminated or suspended leases."),
                Stage("Review occupancy impact", "Facilities Officer", "Update unit occupancy and availability based on lease status.", "Mark occupied spaces.", "Link tenant to unit.", "Release unit when lease ends."),
                Stage("Approve lease status", "Facilities Manager", "Confirm lease data completeness and operational status.", "Review documents and terms.", "Approve active lease status.", "Send exceptions back for correction."),
                Stage("Monitor and close", "Lease Officer", "Track renewals, terminations, arrears referrals, and closed lease records.", "Monitor upcoming renewals.", "Record termination date.", "Archive closed lease documents.")
            ],
            [
                Doc("Approved lease or tenancy instruction", "Estate / Legal", true),
                Doc("Signed lease document", "Legal / Tenant", true),
                Doc("Client details", "Tenant / CRM", true),
                Doc("Rent and billing terms", "Finance", true),
                Doc("Termination or renewal notice", "Tenant / Estate", false)
            ],
            LeaseFields(),
            ["Lease record", "Renewal and expiry alerts", "Updated occupancy status", "Lease document file"],
            CommonHandoffs("Lease Officer", "Facilities Manager", "Lease record is ready for approval"));

    private static FacilitiesProcedureWorkspace Maintenance(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Log request", "Helpdesk / Facilities Desk", "Capture the maintenance request or complaint and link it to the property, unit, asset, or tenant.", "Record requester details.", "Capture issue description.", "Attach photos or evidence."),
                Stage("Triage priority", "Facilities Supervisor", "Classify the maintenance type, priority, risk, and response target.", "Set priority.", "Confirm safety impact.", "Assign internal or external owner."),
                Stage("Assign provider", "Facilities Supervisor", "Assign technician, cleaner, staff member, or service provider.", "Select assignee.", "Confirm availability.", "Issue work instruction."),
                Stage("Perform work", "Technician / Service Provider", "Complete the maintenance task and submit work notes.", "Record materials used.", "Capture completion notes.", "Attach completion evidence."),
                Stage("Inspect completion", "Facilities Supervisor", "Inspect completed work and accept or return for correction.", "Review work quality.", "Confirm requester satisfaction where required.", "Return failed inspections."),
                Stage("Capture cost and invoice", "Facilities / Finance", "Record cost, invoice, and payment reference where contractor work is involved.", "Capture invoice details.", "Verify contract rate.", "Forward payable item to Finance."),
                Stage("Close request", "Facilities Desk", "Close the work order and update asset, property, or complaint history.", "Update status to closed.", "Record closure date.", "Update maintenance history.")
            ],
            [
                Doc("Maintenance request", "Tenant / Staff / Helpdesk", true),
                Doc("Issue photos or evidence", "Requester", false),
                Doc("Work instruction", "Facilities Supervisor", true),
                Doc("Completion evidence", "Technician / Provider", true),
                Doc("Invoice or cost note", "Service Provider / Finance", false)
            ],
            MaintenanceFields(),
            ["Maintenance work order", "Inspection result", "Cost or invoice record", "Updated maintenance history"],
            CommonHandoffs("Facilities Supervisor", "Technician / Service Provider", "Work order is assigned"));

    private static FacilitiesProcedureWorkspace Complaint(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive complaint", "Facilities Desk", "Log tenant, client, or staff complaint with property and contact details.", "Capture complainant details.", "Record issue category.", "Link property, unit, or lease."),
                Stage("Assess and classify", "Facilities Supervisor", "Classify complaint severity, ownership, and expected response time.", "Set severity.", "Confirm responsible team.", "Flag urgent or recurring issues."),
                Stage("Assign action", "Facilities Supervisor", "Assign investigation or corrective action to the right owner.", "Assign officer or provider.", "Set action due date.", "Notify complainant if required."),
                Stage("Resolve issue", "Assigned Officer / Provider", "Perform corrective action and update the complaint record.", "Record action taken.", "Attach evidence.", "Escalate unresolved issues."),
                Stage("Validate resolution", "Facilities Desk / Supervisor", "Confirm the issue is resolved and acceptable for closure.", "Review resolution notes.", "Confirm complainant feedback where required.", "Approve closure or reopen."),
                Stage("Close and report", "Facilities Manager", "Close the complaint and include it in trend, escalation, and service reporting.", "Set closure status.", "Record closure date.", "Include in complaint reporting.")
            ],
            [
                Doc("Complaint log", "Facilities Desk", true),
                Doc("Supporting photos or messages", "Complainant", false),
                Doc("Investigation note", "Assigned Officer", false),
                Doc("Resolution evidence", "Assigned Officer / Provider", true)
            ],
            CommonFields("Complaint type"),
            ["Complaint record", "Action and resolution log", "Closure note", "Complaint trend data"],
            CommonHandoffs("Facilities Desk", "Facilities Supervisor", "Complaint is logged and needs classification"));

    private static FacilitiesProcedureWorkspace ServiceProvider(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Register provider", "Facilities Admin", "Create the contractor or service provider profile.", "Capture company details.", "Record contact persons.", "Select service categories."),
                Stage("Validate documents", "Facilities Manager / Procurement", "Confirm contracts, licenses, insurance, tax, and compliance documents.", "Upload compliance documents.", "Check validity dates.", "Flag missing requirements."),
                Stage("Capture contract terms", "Facilities / Procurement", "Record contract scope, rates, service area, and payment terms.", "Capture agreed rates.", "Record service locations.", "Set contract start and end dates."),
                Stage("Assign services", "Facilities Supervisor", "Make provider available for approved work categories and properties.", "Map provider to categories.", "Set availability.", "Assign service owner."),
                Stage("Track performance", "Facilities Supervisor", "Record work quality, response time, complaint history, and invoice performance.", "Capture ratings.", "Review SLA adherence.", "Record exceptions."),
                Stage("Review and renew", "Facilities Manager", "Review provider performance before renewal, suspension, or closure.", "Review contract expiry.", "Approve renewal or suspension.", "Archive inactive provider records.")
            ],
            [
                Doc("Provider registration details", "Service Provider", true),
                Doc("Contract or engagement letter", "Procurement / Legal", true),
                Doc("License, insurance, and compliance documents", "Service Provider", true),
                Doc("Rate card or pricing schedule", "Procurement / Provider", false)
            ],
            CommonFields("Service category"),
            ["Approved provider profile", "Contract and rate record", "Performance history", "Renewal or suspension decision"],
            CommonHandoffs("Facilities Admin", "Facilities Manager", "Provider profile is ready for validation"));

    private static FacilitiesProcedureWorkspace StaffCleaner(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Create staff profile", "Facilities Admin", "Register facilities staff or cleaner details and employment or contract status.", "Capture staff details.", "Record role and supervisor.", "Set active status."),
                Stage("Assign duty area", "Facilities Supervisor", "Assign site, building, floor, route, or cleaning zone.", "Select duty location.", "Set shift or schedule.", "Confirm required tools or supplies."),
                Stage("Monitor attendance", "Facilities Supervisor", "Track attendance, duty start, completion, and exceptions.", "Record attendance.", "Capture absence or lateness.", "Escalate repeated exceptions."),
                Stage("Supervise duty quality", "Facilities Supervisor", "Inspect work completion and service quality.", "Record inspection result.", "Capture issues found.", "Assign corrective action."),
                Stage("Report performance", "Facilities Manager", "Review duty performance, staffing gaps, and recurring service issues.", "Review supervisor notes.", "Record performance outcome.", "Update staffing plan.")
            ],
            [
                Doc("Staff or cleaner profile", "Facilities Admin", true),
                Doc("Duty roster", "Facilities Supervisor", true),
                Doc("Attendance record", "Supervisor / Staff", true),
                Doc("Inspection or supervision note", "Facilities Supervisor", false)
            ],
            CommonFields("Duty type"),
            ["Staff assignment", "Duty roster", "Attendance and supervision record", "Performance summary"],
            CommonHandoffs("Facilities Admin", "Facilities Supervisor", "Staff profile is ready for duty assignment"));

    private static FacilitiesProcedureWorkspace AssetRegister(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Register asset", "Facilities Officer", "Create the asset record with number, description, location, acquisition, and ownership details.", "Assign asset number.", "Capture asset category.", "Link site or unit."),
                Stage("Capture financial and warranty data", "Facilities / Finance", "Record cost, supplier, purchase date, warranty, and depreciation references where required.", "Capture acquisition cost.", "Record supplier details.", "Set warranty expiry."),
                Stage("Set status and custodian", "Facilities Supervisor", "Assign custodian, condition, operational status, and service responsibility.", "Set current condition.", "Assign custodian.", "Set active, damaged, disposed, or under-repair status."),
                Stage("Attach documents", "Document Control Officer", "Attach invoices, manuals, warranties, service certificates, and inspection records.", "Upload asset documents.", "Tag by asset number.", "Verify document metadata."),
                Stage("Track maintenance history", "Facilities Supervisor", "Link maintenance requests, inspections, repairs, and replacements to the asset.", "Review work orders.", "Update service history.", "Flag recurring failures."),
                Stage("Review and dispose", "Facilities Manager / Finance", "Approve status changes, disposal, transfer, or replacement actions.", "Review disposal request.", "Approve transfer or write-off.", "Archive inactive asset record.")
            ],
            [
                Doc("Asset acquisition or registration note", "Facilities / Finance", true),
                Doc("Invoice or purchase record", "Finance / Supplier", false),
                Doc("Warranty or manual", "Supplier", false),
                Doc("Inspection or condition report", "Facilities Supervisor", false)
            ],
            CommonFields("Asset category"),
            ["Asset register record", "Warranty and document file", "Maintenance history", "Disposal or transfer record"],
            CommonHandoffs("Facilities Officer", "Facilities Manager", "Asset record is ready for approval"));

    private static FacilitiesProcedureWorkspace DocumentControl(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive document", "Document Control Officer", "Receive leases, contracts, certificates, invoices, payment records, or property documents.", "Capture document title.", "Confirm document source.", "Scan or upload document."),
                Stage("Classify and tag", "Document Control Officer", "Classify the document and tag it to property, lease, provider, asset, or work order records.", "Select document type.", "Link related entity.", "Set effective and expiry dates where relevant."),
                Stage("Validate access", "Facilities Manager", "Confirm access level, confidentiality, and retention requirements.", "Set access group.", "Flag restricted documents.", "Confirm retention category."),
                Stage("Publish to record", "Document Control Officer", "Make the document searchable and available from the linked workspace.", "Confirm file opens.", "Verify metadata.", "Publish document link."),
                Stage("Review and archive", "Document Control Officer / Facilities Manager", "Monitor expiring documents and archive inactive records.", "Review expiry alerts.", "Replace superseded documents.", "Archive inactive files.")
            ],
            [
                Doc("Source document", "Tenant / Provider / Estate / Finance", true),
                Doc("Document metadata", "Document Control Officer", true),
                Doc("Access or confidentiality instruction", "Facilities Manager", false),
                Doc("Replacement or expiry note", "Document owner", false)
            ],
            CommonFields("Document type"),
            ["Indexed facilities document", "Linked workspace record", "Expiry or renewal alert", "Archive history"],
            CommonHandoffs("Document Control Officer", "Facilities Manager", "Document needs access validation"));

    private static FacilitiesProcedureWorkspace Workspace(
        FacilitiesProcedureCatalogItem procedure,
        IReadOnlyList<FacilitiesWorkspaceStage> stages,
        IReadOnlyList<FacilitiesWorkspaceDocument> documents,
        IReadOnlyList<FacilitiesWorkspaceField> fields,
        IReadOnlyList<string> outputs,
        IReadOnlyList<FacilitiesWorkspaceHandoff> handoffs) =>
        new(procedure, stages, documents, fields, outputs, handoffs);

    private static FacilitiesWorkspaceStage Stage(string name, string owner, string summary, params string[] checklist) =>
        new(name, owner, summary, checklist);

    private static FacilitiesWorkspaceDocument Doc(string name, string requiredFrom, bool isMandatory) =>
        new(name, requiredFrom, isMandatory);

    private static FacilitiesWorkspaceField Field(string key, string label, string type, params string[] options) =>
        new(key, label, type, options.Length == 0 ? null : options);

    private static FacilitiesWorkspaceHandoff Handoff(string fromRole, string toRole, string trigger) =>
        new(fromRole, toRole, trigger);

    private static IReadOnlyList<FacilitiesWorkspaceField> CommonFields(string typeLabel) =>
    [
        Field("referenceNumber", "Reference number", "text"),
        Field("type", typeLabel, "text"),
        Field("siteOrProperty", "Site / property", "text"),
        Field("responsibleOfficer", "Responsible officer", "text"),
        Field("receivedDate", "Received date", "date"),
        Field("priority", "Priority", "select", "Normal", "Urgent", "Compliance")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> LeaseFields() =>
    [
        Field("leaseReference", "Lease reference", "text"),
        Field("tenantName", "Tenant / client name", "text"),
        Field("propertyUnit", "Property / unit", "text"),
        Field("leaseStart", "Lease start", "date"),
        Field("leaseEnd", "Lease end", "date"),
        Field("leaseStatus", "Lease status", "select", "Draft", "Active", "Renewal due", "Terminated", "Expired")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> MaintenanceFields() =>
    [
        Field("workOrderNumber", "Work order number", "text"),
        Field("requester", "Requester", "text"),
        Field("propertyUnit", "Property / unit / asset", "text"),
        Field("issueType", "Issue type", "text"),
        Field("priority", "Priority", "select", "Low", "Normal", "High", "Emergency"),
        Field("targetDate", "Target date", "date")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceHandoff> CommonHandoffs(string fromRole, string toRole, string trigger) =>
    [
        Handoff(fromRole, toRole, trigger),
        Handoff(toRole, "Assigned Officer / Provider", "Action or field work is required"),
        Handoff("Assigned Officer / Provider", "Facilities Supervisor", "Action is completed or needs inspection"),
        Handoff("Facilities Supervisor", "Facilities Manager", "Approval, exception review, or reporting is required"),
        Handoff("Facilities Manager", "Records / Document Control", "Record is approved for filing or publication")
    ];
}
