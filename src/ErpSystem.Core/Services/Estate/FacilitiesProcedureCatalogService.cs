using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class FacilitiesProcedureCatalogService : IFacilitiesProcedureCatalogService
{
    private static readonly FacilitiesProcedureCatalogItem[] Procedures =
    [
        new("Maintenance Intake", "EstateFacilityMaintenance", "Source: Estate / Facilities -> Maintenance Management", "Estate / Facilities maintenance intake with property, unit, requester, evidence, SLA, service-impact, and source context routed into existing Maintenance Management job cards, work orders, inspections, costs, and closure feedback.", "Wrench", 8, "amber"),
        new("Complaint Management", "EstateFacilityComplaint", "Source: Estate / Facilities -> Helpdesk Complaint Management", "Estate / Facilities complaint intake with property, unit, tenant, evidence, service-impact, SLA context, and source label routed into existing Helpdesk complaint tickets, escalation, investigation, resolution, and feedback history.", "MessageSquare", 8, "rose"),
        new("Service Provider Management", "EstateFacilityServiceProvider", "Source: Estate / Facilities -> Procurement / Finance AP", "Estate / Facilities provider operating profile for approved Procurement suppliers, with service categories, site coverage, compliance expiry, assignment readiness, SLA monitoring, performance, Maintenance/Complaint links, and Finance AP invoice/payment references.", "Briefcase", 8, "violet"),
        new("Staff & Cleaner Duty Operations", "EstateFacilityStaffCleaner", "Source: Estate / Facilities -> HR / Administration", "Estate / Facilities duty operations for HR-sourced staff and cleaners, with Administration user-employee links, roster, site coverage, attendance exceptions, supervision, quality tracking, and HR escalation references.", "ClipboardCheck", 8, "emerald"),
        new("Facilities Asset Operating View", "EstateFacilityAssetRegister", "Source: Estate / Facilities -> Finance Fixed Assets / Maintenance", "Estate / Facilities operational asset view for Project Management handover, Finance Fixed Assets, or Maintenance asset records, with location, custodian, condition, warranty, inspection, service-impact, maintenance, complaint, provider, and Finance action references without duplicating the financial asset register.", "Database", 8, "indigo"),
        new("Facilities Billing / Service Charge Operations", "EstateFacilityBillingServiceCharge", "Source: Estate / Facilities -> Finance AR", "Estate / Facilities billing instructions for service charges, common-area recoveries, deposits, arrears, statements, disputes, and documents routed into existing Finance AR without duplicating invoices, receipts, balances, or GL postings.", "CreditCard", 8, "cyan"),
        new("Facilities Document Index & DMS Readiness", "EstateFacilityDocument", "Source: Estate / Facilities -> Central DMS", "Estate / Facilities document index for source-module records, metadata definition, expiry tracking, access control, retention, comments/annotation readiness, and Central Document Management migration without duplicating the source repository.", "FileText", 8, "lime")
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
            "EstateFacilityMaintenance" => Maintenance(procedure),
            "EstateFacilityComplaint" => Complaint(procedure),
            "EstateFacilityServiceProvider" => ServiceProvider(procedure),
            "EstateFacilityStaffCleaner" => StaffCleaner(procedure),
            "EstateFacilityAssetRegister" => AssetRegister(procedure),
            "EstateFacilityBillingServiceCharge" => BillingServiceCharge(procedure),
            "EstateFacilityDocument" => DocumentControl(procedure),
            _ => null
        };
    }

    private static FacilitiesProcedureWorkspace Maintenance(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Capture facilities maintenance intake", "Facilities Desk", "Capture the maintenance need, service impact, requester, location, evidence, and Source: Estate / Facilities before routing execution to Maintenance Management.", "Record requester and contact details.", "Capture property, unit, asset, occupant, complaint, or handover reference.", "Attach photos, documents, or evidence.", "Record Source: Estate / Facilities."),
                Stage("Validate location and service impact", "Facilities Supervisor", "Validate the facilities context before Maintenance Management receives the job card request.", "Confirm site, property, floor, unit, asset, or common area.", "Confirm access, safety, utility, occupancy, or service disruption impact.", "Flag tenant, complaint, provider, or Property Management impact.", "Confirm duplicate or recurring issue status."),
                Stage("Triage priority and SLA", "Facilities Supervisor", "Classify maintenance category, priority, risk, SLA, and escalation route without assigning technicians or parts from Facilities.", "Set priority and response target.", "Confirm safety or emergency impact.", "Select maintenance category and preferred execution route.", "Confirm whether Property Management must block or release a unit."),
                Stage("Prepare Maintenance Management handoff", "Facilities Supervisor", "Prepare the approved intake package for the existing Maintenance Management job card and work order flow.", "Create or reference Maintenance job card request.", "Include Facilities case reference and source label.", "Include evidence, location, SLA, requester, and service-impact notes.", "Do not duplicate work order execution in Facilities."),
                Stage("Track Maintenance Management execution status", "Facilities Desk / Maintenance Coordinator", "Track job card, work order, inspection, technician, provider, parts, and cost references from Maintenance Management while Maintenance owns execution.", "Capture job card reference.", "Capture work order reference.", "Track Maintenance status and expected completion.", "Escalate overdue or blocked maintenance follow-up."),
                Stage("Coordinate affected facilities workspaces", "Facilities Supervisor", "Update linked Facilities, Property Management, Complaint, Asset, Service Provider, Document, or Staff workspaces affected by the maintenance outcome.", "Update complaint or service case where applicable.", "Update asset operational history reference.", "Notify Property Management for unit block, release, occupancy, or handover impact.", "Index maintenance evidence where required."),
                Stage("Receive inspection and requester feedback", "Facilities Supervisor", "Receive Maintenance Management completion, inspection, quality result, and requester feedback before Facilities closes the intake.", "Confirm maintenance completion or return for correction.", "Record requester or occupant feedback.", "Capture inspection and closeout status.", "Confirm cost or invoice references are routed to the owning Finance or Procurement process."),
                Stage("Close facilities intake", "Facilities Manager", "Close the Facilities maintenance intake after Maintenance execution, linked workspace updates, records indexing, and management follow-up are complete.", "Confirm Maintenance job card or work order is closed or assigned for follow-up.", "Confirm linked complaints, assets, property status, and records were updated.", "Capture closure notes and lessons learned.", "Close the Facilities case with audit trail.")
            ],
            [
                Doc("Facilities maintenance intake", "Estate / Facilities, Tenant, Staff, Helpdesk, or Property Management", true),
                Doc("Issue photos or evidence", "Requester", false),
                Doc("Location, access, safety, or service-impact note", "Facilities Supervisor", true),
                Doc("Maintenance Management job card or work order reference", "Maintenance Management", true),
                Doc("Completion, inspection, or correction evidence", "Maintenance Management", false),
                Doc("Requester feedback or closure note", "Facilities Supervisor", false),
                Doc("Invoice, cost, parts, or provider reference", "Maintenance / Procurement / Finance", false)
            ],
            MaintenanceFields(),
            ["Facilities maintenance intake reference", "Source: Estate / Facilities handoff package", "Linked Maintenance Management job card and work order", "Service impact, SLA, risk, and location context", "Property, asset, complaint, provider, handover, or occupant links", "Maintenance execution status and inspection feedback", "Cost, invoice, parts, or provider references owned by Maintenance / Procurement / Finance", "Closed Facilities intake with updated history"],
            [
                Handoff("Requester / Helpdesk / Property Management", "Facilities Desk", "Maintenance need is reported with Estate / Facilities context"),
                Handoff("Facilities Desk", "Facilities Supervisor", "Maintenance intake requires validation, priority, SLA, and service-impact triage"),
                Handoff("Facilities Supervisor", "Maintenance Management", "Facilities intake package is approved for Maintenance job card and work order execution"),
                Handoff("Maintenance Management", "Facilities Desk", "Job card, work order, inspection, cost, or closure status is available"),
                Handoff("Facilities Supervisor", "Property Management / Complaint / Asset / Provider / Records", "Maintenance outcome affects a linked Facilities or Property workspace"),
                Handoff("Facilities Supervisor", "Facilities Manager", "Closure, escalation, recurring issue, or exception review is required")
            ]);

    private static FacilitiesProcedureWorkspace Complaint(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Capture facilities complaint intake", "Facilities Desk", "Capture the complaint, complainant, property, unit, service impact, evidence, and Source: Estate / Facilities before routing ticket ownership to Helpdesk.", "Capture complainant and contact details.", "Capture property, unit, occupant, lease, asset, or maintenance reference.", "Attach photos, messages, or evidence.", "Record Source: Estate / Facilities."),
                Stage("Validate estate and service context", "Facilities Supervisor", "Validate the estate/facilities context so Helpdesk receives a clean complaint package.", "Confirm site, property, floor, unit, common area, or asset.", "Confirm service category, tenant impact, safety impact, and access impact.", "Check duplicate, recurring, or linked maintenance issue.", "Flag Property Management, Maintenance, Provider, or Staff impact."),
                Stage("Classify severity and SLA context", "Facilities Supervisor", "Classify complaint category, severity, priority, SLA expectation, and escalation route before Helpdesk ticket creation.", "Set complaint category and severity.", "Set priority and expected response target.", "Identify responsible execution module.", "Confirm urgent, safety, reputational, or management escalation flags."),
                Stage("Prepare Helpdesk handoff", "Facilities Desk", "Prepare the approved Facilities intake package for the existing Helpdesk complaint ticket lifecycle.", "Create or reference Helpdesk complaint ticket.", "Include Facilities case reference and source label.", "Include evidence, property context, complainant, SLA context, and service impact.", "Do not duplicate Helpdesk SLA or resolution history in Facilities."),
                Stage("Track Helpdesk complaint status", "Facilities Desk / Helpdesk Coordinator", "Track Helpdesk ticket status, SLA, escalation, investigation, assignment, and resolution references while Helpdesk owns the complaint workflow.", "Capture Helpdesk ticket reference.", "Track SLA, escalation, and ticket status.", "Record linked owner such as Maintenance, Provider, Staff, Property Management, or Helpdesk.", "Escalate overdue or blocked complaint follow-up."),
                Stage("Coordinate linked facilities actions", "Facilities Supervisor", "Coordinate facilities-side follow-up without taking over the Helpdesk ticket lifecycle.", "Open Maintenance Intake if repair work is required.", "Update Service Provider, Staff/Cleaner, Asset, Document, or Property Management references where affected.", "Notify complainant of facilities-side progress where required.", "Index evidence or correspondence where required."),
                Stage("Receive resolution and feedback", "Facilities Desk / Supervisor", "Receive Helpdesk resolution, closure notes, complainant feedback, satisfaction status, and any reopening reason before Facilities closes the intake.", "Confirm Helpdesk resolution result.", "Capture complainant feedback.", "Record reopen, unresolved, or escalation status.", "Confirm linked workspaces have been updated."),
                Stage("Close facilities complaint intake", "Facilities Manager", "Close the Facilities complaint intake after Helpdesk resolution, feedback, linked updates, records indexing, and management reporting are complete.", "Confirm Helpdesk ticket is closed or assigned for follow-up.", "Confirm complaint trends, recurring issue flags, and lessons learned.", "Confirm records index or evidence links.", "Close the Facilities case with audit trail.")
            ],
            [
                Doc("Facilities complaint intake", "Estate / Facilities Desk", true),
                Doc("Supporting photos or messages", "Complainant", false),
                Doc("Property, tenant, asset, maintenance, or service context note", "Facilities Supervisor", true),
                Doc("Helpdesk complaint ticket reference", "Helpdesk", true),
                Doc("Investigation, escalation, or resolution reference", "Helpdesk / Assigned Owner", false),
                Doc("Complainant feedback or reopening note", "Facilities Desk", false),
                Doc("Maintenance, provider, staff, or Property Management follow-up reference", "Linked Module", false)
            ],
            ComplaintFields(),
            ["Facilities complaint intake reference", "Source: Estate / Facilities handoff package", "Linked Helpdesk complaint ticket", "Property, tenant, asset, service, evidence, severity, and SLA context", "Linked Maintenance, Provider, Staff, Property Management, or Document references", "Helpdesk escalation, investigation, resolution, and feedback references", "Complaint trend and recurring issue data", "Closed Facilities complaint intake with audit trail"],
            [
                Handoff("Complainant / Helpdesk / Property Management", "Facilities Desk", "Facilities complaint is reported with Estate / Facilities context"),
                Handoff("Facilities Desk", "Facilities Supervisor", "Complaint intake requires context validation, severity, SLA, and routing review"),
                Handoff("Facilities Supervisor", "Helpdesk Complaints", "Facilities complaint package is approved for Helpdesk ticket lifecycle"),
                Handoff("Helpdesk Complaints", "Facilities Desk", "Ticket status, SLA, escalation, investigation, resolution, or closure feedback is available"),
                Handoff("Facilities Supervisor", "Maintenance / Provider / Staff / Property Management / Records", "Complaint outcome affects linked Facilities or Property workspaces"),
                Handoff("Facilities Supervisor", "Facilities Manager", "Closure, escalation, recurring issue, or management review is required")
            ]);

    private static FacilitiesProcedureWorkspace ServiceProvider(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive approved provider source", "Facilities Admin", "Receive or reference the approved supplier, contractor, or business partner from Procurement before Facilities can assign provider work.", "Capture Procurement supplier or business partner reference.", "Confirm provider is approved, active, or conditionally available.", "Capture contract, rate, and compliance source references.", "Record Source: Procurement -> Estate / Facilities."),
                Stage("Validate contract, legal, and compliance readiness", "Facilities Manager / Procurement / Legal", "Confirm contract, license, insurance, tax, safety, legal review, and compliance status without duplicating Procurement contract ownership.", "Link Procurement contract and rate card.", "Record Legal review status where required.", "Confirm insurance, license, tax, and safety compliance expiry.", "Flag expired, missing, suspended, or conditional documents."),
                Stage("Capture operational service profile", "Facilities Supervisor", "Record the service categories, service areas, SLA, emergency availability, escalation contacts, supervisor, and operational owner used by Facilities.", "Select service categories.", "Map provider to properties, sites, zones, units, assets, or service areas.", "Capture SLA, response target, and escalation contact.", "Set provider availability and emergency coverage."),
                Stage("Approve assignment readiness", "Facilities Manager", "Approve the provider for Facilities work only after compliance, coverage, capacity, and operational ownership are clear.", "Confirm assignment readiness status.", "Confirm provider can support maintenance, complaints, cleaning, inspections, security, utilities, or other approved services.", "Set service restrictions or approval conditions.", "Publish provider for approved workstreams."),
                Stage("Link provider to facilities workstreams", "Facilities Supervisor", "Link the provider to Maintenance Intake, Complaint Management, Staff/Cleaner support, Asset Operating View, inspections, or other Facilities workstreams without taking over those workflows.", "Link maintenance job card or work order references.", "Link complaint or service case references.", "Link asset, site, or document references.", "Confirm work instruction is issued through the owning workstream."),
                Stage("Track SLA, quality, and performance", "Facilities Supervisor", "Track provider response time, completion quality, complaint history, safety issues, repeat failures, and SLA adherence for operational performance reporting.", "Capture SLA performance.", "Record quality rating and inspection outcome.", "Capture complaint, defect, or incident references.", "Flag underperformance, suspension risk, or management escalation."),
                Stage("Record Finance AP and cost references", "Facilities Supervisor / Finance AP", "Record Finance AP invoice, payment, cost, retention, or dispute references while Finance AP owns invoices, payments, supplier balances, and postings.", "Capture AP invoice or payment reference.", "Capture cost or contract rate exception.", "Flag invoice dispute or returned correction.", "Do not create AP accounting records in Facilities."),
                Stage("Review renewal, suspension, or closure", "Facilities Manager", "Review contract expiry, compliance expiry, performance, invoice disputes, complaints, and operational need before renewal, suspension, closure, or return to Procurement for contract action.", "Review contract and compliance expiry.", "Approve renewal recommendation, suspension, restriction, or closure.", "Return contract amendment or renewal to Procurement.", "Archive inactive provider operating profile.")
            ],
            [
                Doc("Approved supplier or business partner reference", "Procurement", true),
                Doc("Contract or engagement letter", "Procurement / Legal", true),
                Doc("License, insurance, and compliance documents", "Service Provider", true),
                Doc("Rate card or pricing schedule", "Procurement / Provider", false),
                Doc("Service coverage, SLA, or escalation contact", "Facilities Supervisor", true),
                Doc("Maintenance, complaint, asset, or service assignment reference", "Facilities Workspace", false),
                Doc("Performance, inspection, or SLA review note", "Facilities Supervisor", false),
                Doc("AP invoice, payment, cost, or dispute reference", "Finance AP", false)
            ],
            ServiceProviderFields(),
            ["Facilities provider operating profile", "Linked Procurement supplier, contract, rate, compliance, and Legal review references", "Service categories, coverage map, SLA, escalation contact, and availability status", "Assignment readiness and approved workstream restrictions", "Maintenance, complaint, staff, asset, inspection, and document links", "SLA, quality, complaint, safety, and performance history", "Finance AP invoice, payment, cost, retention, or dispute references", "Renewal, amendment, suspension, restriction, or closure recommendation"],
            [
                Handoff("Procurement", "Facilities Admin", "Approved supplier or contract is ready for Facilities use"),
                Handoff("Facilities Admin", "Facilities Manager / Procurement / Legal", "Contract and compliance validation is required"),
                Handoff("Facilities Manager", "Facilities Supervisor", "Provider is approved, restricted, or conditionally available for operational assignment"),
                Handoff("Facilities Supervisor", "Maintenance / Complaint / Asset / Staff Workstreams", "Provider is needed for approved facilities work"),
                Handoff("Facilities Supervisor", "Finance AP", "Completed provider work has invoice, cost, payment, retention, or dispute follow-up"),
                Handoff("Facilities Supervisor", "Facilities Manager", "SLA, quality, complaint, safety, or compliance issue requires review"),
                Handoff("Facilities Manager", "Procurement / Legal", "Renewal, amendment, suspension, restriction, closure, or contract action is required")
            ]);

    private static FacilitiesProcedureWorkspace StaffCleaner(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Reference HR employee profile", "Facilities Admin", "Reference the HR or Payroll employee profile before Facilities assigns duties, without creating a separate staff master record.", "Capture HR or Payroll employee reference.", "Capture employee number and name.", "Confirm active, on-leave, temporary, suspended, or inactive status.", "Record Source: HR / Payroll -> Estate / Facilities."),
                Stage("Validate user-employee link and eligibility", "Facilities Admin / Administration", "Confirm whether the staff member has a system login and Administration user-employee link when digital access or approvals are required.", "Capture Administration user-employee link reference where available.", "Capture identity user reference only when login access is needed.", "Confirm employment eligibility for facilities duty.", "Do not create login accounts from Facilities."),
                Stage("Create facilities duty profile", "Facilities Supervisor", "Create the Facilities operational duty profile for staff or cleaners based on role, duty type, site coverage, supervisor, and service responsibility.", "Select staff type and duty type.", "Assign responsible supervisor.", "Set site, property, floor, zone, route, or service area.", "Confirm Source: Estate / Facilities on the operational record."),
                Stage("Assign roster, access, tools, and supplies", "Facilities Supervisor", "Assign shift, roster, access needs, tools, supplies, uniforms, equipment, and replacement coverage rules for the duty period.", "Set shift or roster.", "Confirm access and keys where required.", "Confirm tools, supplies, and equipment availability.", "Set replacement or backup coverage."),
                Stage("Monitor attendance and exceptions", "Facilities Supervisor", "Track duty start, completion, absence, lateness, replacement coverage, and exception escalation.", "Record attendance follow-up.", "Capture absence, lateness, or incomplete duty.", "Record replacement coverage.", "Escalate repeated exceptions to HR where required."),
                Stage("Supervise duty quality and incidents", "Facilities Supervisor", "Inspect work completion, cleaning quality, service quality, incidents, complaints, corrective action, and linked maintenance impact.", "Record inspection or supervisor result.", "Capture issue, incident, or complaint reference.", "Assign corrective action.", "Link Maintenance Intake or Complaint Management where required."),
                Stage("Route linked follow-up", "Facilities Manager", "Route linked HR, Maintenance, Complaint, Provider, Asset, Inventory, or Document follow-up without taking over those module workflows.", "Send employment, conduct, or staffing action to HR.", "Send repair work to Maintenance Intake.", "Send supply replenishment to Inventory or Procurement.", "Index related documents where required."),
                Stage("Review duty period and close", "Facilities Manager / HR", "Review duty coverage, performance, recurring service issues, staffing gaps, HR escalation needs, and closure for the duty period.", "Review attendance and quality outcomes.", "Record performance or service outcome.", "Confirm linked module follow-up references.", "Close the Facilities duty record with audit trail.")
            ],
            [
                Doc("HR or Payroll staff profile reference", "HR / Payroll", true),
                Doc("Administration user-employee link reference", "Administration / Identity Management", false),
                Doc("Duty profile and roster", "Facilities Supervisor", true),
                Doc("Attendance and replacement record", "Supervisor / Staff", true),
                Doc("Inspection or supervision note", "Facilities Supervisor", false),
                Doc("Tools, supplies, access, or equipment note", "Facilities Supervisor / Inventory", false),
                Doc("Linked complaint, maintenance, provider, or asset reference", "Linked Module", false),
                Doc("HR escalation or disciplinary reference", "HR", false)
            ],
            StaffCleanerFields(),
            ["HR-sourced employee profile reference", "Administration user-employee link reference where login access is required", "Facilities duty profile", "Duty roster, site coverage, tools, supplies, access, and replacement plan", "Attendance exception record", "Supervision, quality, incident, complaint, or maintenance reference", "Linked HR, Inventory, Provider, Asset, Document, or Maintenance follow-up", "Closed Facilities duty period with performance or HR escalation summary"],
            [
                Handoff("HR / Payroll", "Facilities Admin", "Active staff or cleaner profile is available for Facilities duty assignment"),
                Handoff("Administration User Management", "Facilities Admin", "User-employee link or identity user reference is required for system access"),
                Handoff("Facilities Admin", "Facilities Supervisor", "Staff reference and eligibility are ready for roster and duty allocation"),
                Handoff("Facilities Supervisor", "Assigned Staff / Cleaner", "Duty roster or work instruction is issued"),
                Handoff("Assigned Staff / Cleaner", "Facilities Supervisor", "Duty completion, absence, or issue requires supervision update"),
                Handoff("Facilities Supervisor", "Maintenance / Complaint / Provider / Asset / Inventory", "Duty issue affects a linked Facilities or ERP workstream"),
                Handoff("Facilities Supervisor", "Facilities Manager / HR", "Performance gap, attendance exception, conduct issue, or staffing issue requires review"),
                Handoff("Facilities Manager", "HR", "HR action, replacement, or disciplinary follow-up is required")
            ]);

    private static FacilitiesProcedureWorkspace AssetRegister(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive source asset reference", "Facilities Officer", "Receive Project Management handover, Finance Fixed Assets, Maintenance asset, Procurement, or Inventory source reference before Facilities opens an operational asset view.", "Capture project handover, fixed asset, maintenance asset, procurement, or inventory reference.", "Confirm the source module and source owner.", "Confirm asset identifier and source status.", "Record Source: Project Management / Finance Fixed Assets / Maintenance -> Estate / Facilities."),
                Stage("Validate operating location and custody", "Facilities Supervisor", "Validate the operational location, property, unit, common area, custodian, responsible officer, service owner, access, and usage context.", "Confirm site, property, unit, floor, zone, or common area.", "Assign custodian or responsible officer.", "Confirm access, keys, safety, and usage constraints.", "Flag Property Management impact where the asset affects a unit or common area."),
                Stage("Link financial and project context", "Facilities / Finance Fixed Assets / Project Management", "Reference the Finance Fixed Assets register and Project Management handover where capitalization, depreciation, valuation, transfer, disposal, and project closeout remain outside Facilities.", "Capture fixed asset number where available.", "Capture project handover or unit handover reference where available.", "Confirm capitalization, non-capital, pending capitalization, or not-applicable status.", "Do not create depreciation, valuation, transfer, or disposal records in Facilities."),
                Stage("Capture condition, warranty, and inspection baseline", "Facilities Supervisor", "Capture operational condition, service readiness, warranty status, next inspection date, risk level, and service-impact baseline.", "Record condition and operational status.", "Record warranty and expiry details.", "Set next inspection or verification date.", "Flag safety, service-impact, or occupancy risk."),
                Stage("Index operational documents", "Document Control Officer", "Index manuals, warranties, handover certificates, service certificates, inspection records, photos, and approved supporting documents while source modules remain document owners.", "Link source documents or Facilities Document Index references.", "Tag documents by asset, location, property, and source module.", "Confirm access and retention classification.", "Flag Central DMS migration readiness where needed."),
                Stage("Track maintenance, complaint, and provider links", "Facilities Supervisor / Maintenance", "Link maintenance intake, job cards, work orders, inspections, complaints, service providers, and recurring failures without taking over Maintenance execution.", "Capture Maintenance Intake or work order reference.", "Capture complaint or Helpdesk reference where service users are affected.", "Capture provider support reference.", "Flag recurring defects or service-impact trends."),
                Stage("Route Finance or operational action", "Facilities Manager", "Route capitalization, transfer, disposal, retirement, valuation, and depreciation questions to Finance Fixed Assets while Facilities handles only operational status, custody, inspection, and service continuity.", "Send financial action to Finance Fixed Assets.", "Send unit or occupancy impact to Property Management.", "Send repair execution to Maintenance Intake.", "Send supply or replacement need to Procurement or Inventory."),
                Stage("Review operating status and close period", "Facilities Manager", "Review operating status, custody, condition, inspections, maintenance links, complaint trends, provider performance, Finance action references, and close the Facilities operating period.", "Confirm asset is active, monitored, under maintenance, retired from service, or archived operationally.", "Confirm linked workstreams are updated.", "Record management review notes.", "Close the Facilities operating view period with audit trail.")
            ],
            [
                Doc("Project handover, fixed asset, or maintenance asset source reference", "Project Management / Finance Fixed Assets / Maintenance", true),
                Doc("Finance Fixed Assets register reference", "Finance Fixed Assets", false),
                Doc("Invoice, purchase, or supplier record", "Finance / Procurement / Supplier", false),
                Doc("Warranty or manual", "Supplier", false),
                Doc("Inspection, verification, or condition report", "Facilities Supervisor", false),
                Doc("Maintenance intake, job card, or work order reference", "Maintenance Management", false),
                Doc("Complaint, provider, or service-impact reference", "Helpdesk / Provider / Facilities", false),
                Doc("Finance action reference", "Finance Fixed Assets", false)
            ],
            AssetOperationsFields(),
            ["Facilities operational asset view", "Linked Project Management handover, Finance Fixed Assets, or Maintenance asset source reference", "Custodian, site, property, unit, and access context", "Condition, warranty, inspection, risk, and service-impact baseline", "Facilities document index and future DMS readiness references", "Linked maintenance, complaint, provider, and recurring defect history", "Finance Fixed Assets capitalization, transfer, disposal, valuation, or depreciation action reference", "Closed Facilities operating review with updated operational status"],
            [
                Handoff("Project Management / Finance Fixed Assets", "Facilities Officer", "Asset source record is ready for operational tracking"),
                Handoff("Maintenance Management", "Facilities Officer", "Maintenance asset or service history context is available for Facilities operating view"),
                Handoff("Facilities Officer", "Facilities Supervisor", "Operational details and custodian need validation"),
                Handoff("Facilities Supervisor", "Maintenance Management", "Asset requires inspection, service, repair, or work order follow-up"),
                Handoff("Facilities Supervisor", "Document Control Officer", "Warranty, manual, certificate, or inspection document needs filing"),
                Handoff("Facilities Supervisor", "Property Management / Complaint / Provider", "Asset condition affects occupancy, service quality, complaint resolution, or provider performance"),
                Handoff("Facilities Manager", "Finance Fixed Assets", "Transfer, disposal, capitalization, valuation, or retirement action is required"),
                Handoff("Finance Fixed Assets", "Facilities Manager", "Financial asset register action is complete")
            ]);

    private static FacilitiesProcedureWorkspace DocumentControl(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Reference source module document", "Document Control Officer", "Reference documents already created in Legal, Procurement, Project Management, Maintenance, Finance, Estate, Helpdesk, Workflow, or other source modules without copying ownership into Facilities.", "Capture source module and source record.", "Record existing file, URL, repository, or document reference.", "Capture Source: Source Module -> Estate / Facilities.", "Avoid duplicating the owning module document."),
                Stage("Define facilities metadata", "Document Control Officer", "Define the Facilities metadata needed to find, classify, secure, and migrate the document later into Central Document Management.", "Select module metadata template.", "Capture document type, title, subject, source owner, related workspace, related entity, and tags.", "Capture property, unit, asset, provider, complaint, maintenance, lease, or project links.", "Flag mandatory metadata gaps."),
                Stage("Validate ownership and access", "Facilities Manager / Source Module Owner", "Validate source ownership, access level, confidentiality, retention class, legal sensitivity, and publishing permission before exposing the index entry.", "Confirm source owner and approver.", "Set access level and confidentiality.", "Set retention category.", "Flag restricted, confidential, legal, or finance-sensitive records."),
                Stage("Set lifecycle and expiry controls", "Document Control Officer", "Capture effective dates, expiry dates, renewal dates, review dates, replacement status, and alert requirements for Facilities monitoring.", "Capture effective and expiry dates.", "Set renewal or review alert.", "Record replacement or superseded status.", "Notify source module owner for renewal or correction."),
                Stage("Link facilities workspaces", "Facilities Supervisor", "Link the indexed document to Facilities Maintenance Intake, Complaint Management, Provider, Staff/Cleaner, Asset Operating View, Property Management, or future workflows.", "Link related Facilities workspace.", "Link source record and related entity reference.", "Confirm document is visible only where permitted.", "Update linked workspace document reference."),
                Stage("Prepare comments and annotation readiness", "Document Control Officer", "Capture Central DMS needs for comments, PDF viewer annotations, markups, version comparison, redlines, and audit trail without implementing DMS behavior inside Facilities.", "Set annotation requirement.", "Set comments or review requirement.", "Record versioning expectation.", "Flag PDF viewer annotation readiness where needed."),
                Stage("Publish index and monitor exceptions", "Document Control Officer / Facilities Manager", "Publish the Facilities index entry and monitor metadata gaps, access exceptions, expiring documents, missing source files, and correction requests.", "Confirm source document opens or can be retrieved.", "Publish index entry.", "Track missing, expired, restricted, or correction-required status.", "Route exceptions to source module owner."),
                Stage("Review Central DMS migration readiness", "Facilities Manager / Central DMS Owner", "Review indexed records for Central Document Management migration, including module metadata template, repository ownership, versioning, comments, annotations, retention, and audit readiness.", "Confirm metadata completeness.", "Confirm source ownership and migration status.", "Flag ready for Central DMS migration.", "Close Facilities document index review with audit trail.")
            ],
            [
                Doc("Source document or existing module reference", "Legal / Procurement / Project Management / Maintenance / Finance / Estate / Helpdesk / Workflow", true),
                Doc("Facilities metadata template", "Document Control Officer", true),
                Doc("Access, confidentiality, or retention instruction", "Facilities Manager / Source Module Owner", true),
                Doc("Effective, expiry, renewal, or review note", "Document owner", false),
                Doc("Linked Facilities workspace reference", "Facilities Supervisor", false),
                Doc("Comments, annotation, or versioning readiness note", "Document Control Officer", false),
                Doc("Central Document Management migration note", "Document Control Officer / Central DMS Owner", false)
            ],
            DocumentIndexFields(),
            ["Facilities document index entry", "Linked source module document reference", "Facilities metadata template and required metadata set", "Access, confidentiality, and retention classification", "Effective, expiry, renewal, review, and replacement controls", "Related Facilities workspace and entity links", "Comments, PDF viewer annotation, versioning, and audit readiness markers", "Central Document Management migration status"],
            [
                Handoff("Source Module", "Document Control Officer", "Document reference is available for Facilities indexing"),
                Handoff("Document Control Officer", "Source Module Owner", "Source ownership, metadata, replacement, renewal, or correction needs validation"),
                Handoff("Document Control Officer", "Facilities Manager", "Access, confidentiality, retention, or expiry validation is required"),
                Handoff("Facilities Manager", "Document Control Officer", "Indexed document is approved for Facilities workspace publication"),
                Handoff("Facilities Supervisor", "Document Control Officer", "Facilities workspace needs a document indexed or corrected"),
                Handoff("Document Control Officer", "Source Module Owner", "Source document needs correction, renewal, replacement, or version update"),
                Handoff("Document Control Officer", "Central Document Management", "Record is ready for future versioning, comments, PDF viewer annotations, and audit migration")
            ]);

    private static FacilitiesProcedureWorkspace BillingServiceCharge(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive facilities billing trigger", "Facilities Billing Officer", "Open a billing operation from service charge setup, common-area service, utility recovery, provider pass-through, deposit event, arrears review, complaint outcome, maintenance recovery, or approved manual request.", "Capture source workspace and facilities case reference.", "Capture property, unit, occupant, provider, asset, service, or meter context.", "Record Source: Estate / Facilities -> Finance AR.", "Confirm whether Property Management must also be notified."),
                Stage("Validate billable party and property context", "Facilities Supervisor / Property Management", "Confirm the billable customer, tenant, owner, property, unit, occupancy, service area, and Finance AR account before any invoice, receipt, statement, arrears, or deposit instruction is sent.", "Verify property, unit, common area, or service area.", "Verify occupant, tenant, owner, lease, or customer account reference.", "Confirm Finance AR customer account is active or request setup.", "Flag dispute, occupancy mismatch, service exception, or billing hold."),
                Stage("Define charge and allocation setup", "Facilities Billing Officer", "Define the charge type, allocation basis, billing cycle, service period, calculation source, tax handling, due date, and supporting evidence without posting Finance AR entries in Facilities.", "Select charge type and billing instruction type.", "Set allocation basis such as area, unit count, meter reading, fixed amount, actual cost, or percentage.", "Capture amount, currency, tax treatment, due date, and service period.", "Attach approval, service schedule, meter, provider, or cost evidence."),
                Stage("Approve billing instruction package", "Facilities Manager", "Approve the Facilities billing instruction before Finance AR creates invoices, receipts, allocations, arrears, statements, balances, deposits, or GL postings.", "Review source label and source record.", "Review amount, allocation, evidence, and account context.", "Approve, return for correction, or mark disputed.", "Confirm DMS filing requirement for invoice, receipt, statement, demand notice, or adjustment evidence."),
                Stage("Send to Finance AR", "Facilities Billing Officer / Finance AR", "Send the approved package to Finance AR for customer account linkage, invoice creation, receipt recording, allocation, statement issue, arrears update, deposit handling, and GL posting.", "Capture handoff date and Finance AR action requested.", "Capture Finance AR customer account reference.", "Capture expected response date.", "Do not create Finance AR ledger records in Facilities."),
                Stage("Track Finance AR result or correction", "Finance AR / Facilities Billing Officer", "Track Finance AR outcome references while Finance AR remains the accounting system of record.", "Capture invoice, receipt, allocation, statement, balance, deposit, GL batch, or returned-correction reference.", "Capture correction reason and owner.", "Update arrears, dispute, or service charge follow-up status.", "Resubmit corrected instruction where required."),
                Stage("Follow up arrears, statements, and disputes", "Facilities Billing Officer / Property Management", "Use Facilities for service evidence, common-area explanation, tenant or occupant query response, payment promise follow-up, and dispute evidence while Finance AR keeps balances and postings.", "Record arrears or dispute status.", "Capture statement query and supporting evidence.", "Track payment promise, demand notice, or escalation.", "Notify Property Management when occupancy, lease, service, or unit status is affected."),
                Stage("Close billing operation and publish documents", "Facilities Manager / Document Control Officer", "Close the operation after Finance AR result, service charge follow-up, dispute correction, receipt, statement, deposit, or arrears outcome is confirmed and supporting documents are indexed into Central DMS.", "Confirm Finance AR result and references.", "Update Facilities, Property Management, provider, asset, or complaint notes.", "Publish invoice, receipt, statement, demand notice, adjustment, or approval documents to Central DMS.", "Close the Facilities billing operation with audit trail.")
            ],
            [
                Doc("Facilities billing instruction", "Estate / Facilities", true),
                Doc("Property, unit, occupant, service area, or customer account reference", "Estate / Facilities / Property Management / Finance AR", true),
                Doc("Charge setup, allocation, meter, service schedule, or calculation note", "Facilities Billing Officer", true),
                Doc("Provider cost, maintenance recovery, complaint outcome, or service evidence", "Facilities / Maintenance / Provider / Helpdesk", false),
                Doc("Approval or returned correction note", "Facilities Manager / Finance AR", true),
                Doc("Invoice, receipt, statement, allocation, arrears, deposit, balance, or GL reference", "Finance AR", false),
                Doc("Demand notice, payment promise, dispute evidence, or adjustment note", "Facilities Billing Officer / Property Management", false),
                Doc("Central DMS document reference", "Central DMS", false)
            ],
            BillingServiceChargeFields(),
            ["Approved Facilities billing instruction package", "Linked Finance AR customer account and action request", "Service charge, common-area recovery, deposit, arrears, statement, or dispute calculation trail", "Finance AR invoice, receipt, allocation, balance, deposit, statement, and GL references", "Returned correction and resubmission trail", "Arrears, payment promise, demand notice, and dispute follow-up trail", "DMS references for invoice, receipt, statement, demand notice, adjustment, and approval documents", "Closed Facilities billing operation with source and audit trail"],
            [
                Handoff("Facilities / Property Management / Maintenance / Helpdesk / Provider", "Facilities Billing Officer", "Service charge, recovery, deposit, arrears, statement, or adjustment trigger is raised"),
                Handoff("Facilities Billing Officer", "Facilities Supervisor / Property Management", "Billable party, occupancy, service, property, or account context requires validation"),
                Handoff("Facilities Billing Officer", "Facilities Manager", "Charge calculation and billing instruction package requires approval"),
                Handoff("Facilities Manager", "Finance AR", "Approved instruction is ready for invoice, receipt, allocation, statement, arrears, deposit, balance, or posting action"),
                Handoff("Finance AR", "Facilities Billing Officer", "Invoice, receipt, statement, allocation, balance, GL, or returned correction result is available"),
                Handoff("Facilities Billing Officer", "Finance AR", "Corrected instruction is ready for resubmission"),
                Handoff("Facilities Billing Officer", "Property Management / Tenant / Finance AR", "Arrears follow-up, statement query, payment promise, service dispute, or demand notice is required"),
                Handoff("Facilities Manager", "Central Document Management", "Billing documents are ready for DMS indexing, versioning, comments, and PDF viewer annotation where required")
            ]);

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

    private static IReadOnlyList<FacilitiesWorkspaceField> MaintenanceFields() =>
    [
        Field("facilitiesCaseReference", "Facilities case reference", "text"),
        Field("sourceLabel", "Source label", "select", "Source: Estate / Facilities"),
        Field("requester", "Requester", "text"),
        Field("requesterType", "Requester type", "select", "Tenant / occupant", "Staff", "Facilities officer", "Property Management", "Helpdesk", "Security", "Service provider", "Management"),
        Field("contactReference", "Requester contact / account reference", "text"),
        Field("propertyUnit", "Property / unit / asset", "text"),
        Field("locationDetail", "Location / access detail", "text"),
        Field("assetReference", "Facilities asset reference", "text"),
        Field("complaintReference", "Complaint / helpdesk reference", "text"),
        Field("propertyManagementReference", "Property Management reference", "text"),
        Field("handoverReference", "Handover / occupancy reference", "text"),
        Field("issueType", "Issue type", "select", "Building fabric", "Plumbing", "Electrical", "HVAC", "Cleaning", "Security", "Access", "Utility", "Safety", "Landscape", "Pest control", "Other"),
        Field("issueDescription", "Issue description", "textarea"),
        Field("serviceImpact", "Service impact", "select", "No service impact", "Tenant affected", "Common area affected", "Safety risk", "Access restricted", "Utility outage", "Unit block required", "Business interruption"),
        Field("safetyImpact", "Safety impact", "select", "None", "Low", "Medium", "High", "Emergency"),
        Field("priority", "Priority", "select", "Low", "Normal", "High", "Emergency"),
        Field("slaTarget", "SLA / response target", "text"),
        Field("targetDate", "Target date", "date"),
        Field("maintenanceJobCardReference", "Maintenance job card reference", "text"),
        Field("workOrderNumber", "Maintenance work order number", "text"),
        Field("maintenanceStatus", "Maintenance Management status", "select", "Not sent", "Sent to Maintenance", "Job card opened", "Work order created", "Assigned", "In progress", "Inspection pending", "Returned for correction", "Completed", "Closed"),
        Field("providerReference", "Service provider / technician reference", "text"),
        Field("propertyImpact", "Property Management impact", "select", "No impact", "Block unit", "Release unit", "Occupancy affected", "Handover affected", "Tenant follow-up", "Document update required"),
        Field("costReference", "Cost / invoice / parts reference", "text"),
        Field("feedbackStatus", "Requester feedback status", "select", "Not required", "Pending", "Satisfied", "Not satisfied", "Escalated"),
        Field("closureStatus", "Facilities closure status", "select", "Open", "Awaiting Maintenance", "Awaiting inspection", "Awaiting feedback", "Escalated", "Closed"),
        Field("notes", "Facilities maintenance notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> ComplaintFields() =>
    [
        Field("facilitiesCaseReference", "Facilities case reference", "text"),
        Field("sourceLabel", "Source label", "select", "Source: Estate / Facilities"),
        Field("complainantName", "Complainant name", "text"),
        Field("complainantType", "Complainant type", "select", "Tenant / occupant", "Client", "Staff", "Property Management", "Helpdesk", "Visitor", "Service provider", "Management"),
        Field("contactReference", "Complainant contact / account reference", "text"),
        Field("propertyUnit", "Property / unit / common area", "text"),
        Field("occupantReference", "Tenant / occupant reference", "text"),
        Field("leaseReference", "Lease / occupancy reference", "text"),
        Field("assetReference", "Facilities asset reference", "text"),
        Field("maintenanceReference", "Maintenance intake / work order reference", "text"),
        Field("providerReference", "Service provider reference", "text"),
        Field("staffReference", "Staff / cleaner reference", "text"),
        Field("complaintCategory", "Complaint category", "select", "Maintenance", "Cleaning", "Security", "Noise", "Access", "Utilities", "Service provider", "Staff conduct", "Common area", "Billing-related", "Safety", "Other"),
        Field("complaintDescription", "Complaint description", "textarea"),
        Field("serviceImpact", "Service impact", "select", "Low", "Tenant affected", "Multiple tenants affected", "Common area affected", "Safety risk", "Access restricted", "Utility outage", "Reputational risk"),
        Field("severity", "Severity", "select", "Low", "Normal", "High", "Critical"),
        Field("priority", "Priority", "select", "Low", "Normal", "High", "Emergency"),
        Field("slaTarget", "SLA / response target", "text"),
        Field("targetDate", "Target date", "date"),
        Field("helpdeskTicketReference", "Helpdesk complaint ticket reference", "text"),
        Field("helpdeskStatus", "Helpdesk ticket status", "select", "Not sent", "Sent to Helpdesk", "Ticket opened", "Assigned", "In progress", "Escalated", "Resolved", "Reopened", "Closed"),
        Field("escalationStatus", "Escalation status", "select", "None", "Supervisor", "Manager", "Maintenance", "Property Management", "Service Provider", "Helpdesk", "Management"),
        Field("resolutionReference", "Resolution / investigation reference", "text"),
        Field("feedbackStatus", "Complainant feedback status", "select", "Not required", "Pending", "Satisfied", "Not satisfied", "Reopened", "Escalated"),
        Field("recurrenceStatus", "Recurring issue status", "select", "New", "Repeat", "Recurring", "Linked to active case", "Trend review required"),
        Field("documentIndexReference", "Facilities document index reference", "text"),
        Field("closureStatus", "Facilities closure status", "select", "Open", "Awaiting Helpdesk", "Awaiting linked action", "Awaiting feedback", "Escalated", "Closed"),
        Field("notes", "Facilities complaint notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> ServiceProviderFields() =>
    [
        Field("procurementSupplierReference", "Procurement supplier / business partner reference", "text"),
        Field("sourceLabel", "Source label", "select", "Source: Procurement -> Estate / Facilities"),
        Field("providerName", "Provider name", "text"),
        Field("providerType", "Provider type", "select", "Contractor", "Service company", "Consultant", "Cleaning provider", "Maintenance provider", "Security provider", "Inspection provider"),
        Field("serviceCategory", "Service category", "select", "Maintenance", "Cleaning", "Security", "Inspection", "Landscaping", "Waste management", "Utilities", "Pest control", "Fire safety", "Other"),
        Field("contractReference", "Procurement contract reference", "text"),
        Field("rateCardReference", "Procurement rate card / pricing reference", "text"),
        Field("contractStatus", "Contract status", "select", "Not linked", "Draft", "Under review", "Active", "Expiring soon", "Expired", "Suspended"),
        Field("legalReviewStatus", "Legal review status", "select", "Not required", "Pending", "In review", "Approved", "Returned for correction"),
        Field("insuranceStatus", "Insurance status", "select", "Not required", "Pending", "Valid", "Expiring soon", "Expired", "Missing"),
        Field("licenseStatus", "License / permit status", "select", "Not required", "Pending", "Valid", "Expiring soon", "Expired", "Missing"),
        Field("taxClearanceStatus", "Tax clearance status", "select", "Not required", "Pending", "Valid", "Expired", "Missing"),
        Field("safetyComplianceStatus", "Safety compliance status", "select", "Not required", "Pending", "Compliant", "Non-compliant", "Expired", "Suspended"),
        Field("complianceStatus", "Compliance status", "select", "Pending", "Compliant", "Conditional", "Missing documents", "Expired documents", "Suspended"),
        Field("serviceArea", "Service area / properties", "text"),
        Field("coverageType", "Coverage type", "select", "Site-specific", "Multi-site", "Emergency only", "Scheduled service", "On-call", "Project-based", "Other"),
        Field("slaTarget", "SLA / response target", "text"),
        Field("escalationContact", "Escalation contact", "text"),
        Field("availabilityStatus", "Availability status", "select", "Available", "Limited", "Unavailable", "Suspended"),
        Field("assignmentReadiness", "Assignment readiness", "select", "Not ready", "Ready", "Ready with restrictions", "Compliance pending", "Contract pending", "Suspended", "Closed"),
        Field("approvedWorkstreams", "Approved facilities workstreams", "select", "Maintenance", "Complaints", "Cleaning", "Inspection", "Security", "Asset operations", "Staff support", "Utilities", "Other"),
        Field("maintenanceReference", "Maintenance job card / work order reference", "text"),
        Field("complaintReference", "Complaint / Helpdesk reference", "text"),
        Field("assetReference", "Facilities asset reference", "text"),
        Field("documentIndexReference", "Facilities document index reference", "text"),
        Field("slaPerformanceStatus", "SLA performance status", "select", "Not measured", "On target", "At risk", "Breached", "Improving", "Poor"),
        Field("performanceRating", "Performance rating", "select", "Excellent", "Good", "Satisfactory", "Needs improvement", "Poor"),
        Field("qualityStatus", "Quality status", "select", "Not reviewed", "Accepted", "Returned for correction", "Recurring defects", "Safety issue", "Escalated"),
        Field("complaintHistoryStatus", "Complaint history status", "select", "None", "Low", "Moderate", "Recurring complaints", "Under review", "Suspension risk"),
        Field("financeApReference", "Finance AP invoice / payment reference", "text"),
        Field("financeApStatus", "Finance AP status", "select", "Not applicable", "Invoice pending", "Invoice submitted", "Payment pending", "Paid", "Disputed", "Returned for correction"),
        Field("renewalAction", "Renewal / suspension action", "select", "No action", "Renew", "Amend contract", "Restrict", "Suspend", "Close"),
        Field("reviewDate", "Review date", "date"),
        Field("providerNotes", "Provider notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> StaffCleanerFields() =>
    [
        Field("hrEmployeeReference", "HR / Payroll employee reference", "text"),
        Field("userEmployeeLinkReference", "Administration user-employee link reference", "text"),
        Field("identityUserReference", "Identity user reference", "text"),
        Field("loginRequired", "System login required", "select", "No", "Yes", "Existing user linked", "Link pending"),
        Field("employeeName", "Staff / cleaner name", "text"),
        Field("employeeNumber", "Employee number", "text"),
        Field("sourceLabel", "Source label", "select", "Source: HR / Payroll -> Estate / Facilities"),
        Field("staffType", "Staff type", "select", "Facilities officer", "Cleaner", "Supervisor", "Gardener", "Security support", "Maintenance support", "Contract staff"),
        Field("employmentStatus", "Employment status", "select", "Active", "On leave", "Temporary", "Suspended", "Inactive"),
        Field("dutyType", "Duty type", "select", "Cleaning", "Inspection", "Supervision", "Security support", "Maintenance support", "Waste management", "Other"),
        Field("dutyArea", "Duty area / route", "text"),
        Field("siteOrProperty", "Site / property", "text"),
        Field("floorOrZone", "Floor / zone / common area", "text"),
        Field("shiftOrRoster", "Shift / roster", "text"),
        Field("supervisorName", "Supervisor", "text"),
        Field("attendanceStatus", "Attendance status", "select", "Present", "Late", "Absent", "Replaced", "Excused", "Not recorded"),
        Field("replacementCoverage", "Replacement coverage", "select", "Not required", "Covered", "Pending", "No coverage", "Escalated"),
        Field("qualityStatus", "Duty quality status", "select", "Good", "Needs follow-up", "Issue logged", "Corrective action assigned"),
        Field("toolsSuppliesStatus", "Tools / supplies status", "select", "Available", "Partially available", "Unavailable", "Not required"),
        Field("accessStatus", "Access / keys / pass status", "select", "Not required", "Ready", "Pending", "Blocked", "Returned"),
        Field("complaintReference", "Complaint / Helpdesk reference", "text"),
        Field("maintenanceReference", "Maintenance intake / work order reference", "text"),
        Field("providerReference", "Service provider support reference", "text"),
        Field("inventoryReference", "Inventory / supply reference", "text"),
        Field("documentIndexReference", "Facilities document index reference", "text"),
        Field("hrEscalationStatus", "HR escalation status", "select", "Not required", "Pending", "Sent to HR", "Resolved"),
        Field("reviewDate", "Review date", "date"),
        Field("supervisionNotes", "Supervision / performance notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> AssetOperationsFields() =>
    [
        Field("assetSource", "Asset source", "select", "Project handover", "Finance Fixed Assets", "Maintenance asset", "Procurement", "Inventory", "Manual operational record"),
        Field("sourceLabel", "Source label", "select", "Source: Project Management / Finance Fixed Assets / Maintenance -> Estate / Facilities"),
        Field("sourceModule", "Source module", "select", "Project Management", "Finance Fixed Assets", "Maintenance Management", "Procurement", "Inventory", "Estate / Facilities"),
        Field("projectHandoverReference", "Project handover reference", "text"),
        Field("fixedAssetReference", "Finance fixed asset reference", "text"),
        Field("maintenanceAssetReference", "Maintenance asset reference", "text"),
        Field("assetNumber", "Asset number", "text"),
        Field("assetName", "Asset name / description", "text"),
        Field("assetCategory", "Asset category", "select", "Building equipment", "Plant and machinery", "Furniture and fittings", "ICT equipment", "Utility asset", "Safety equipment", "Vehicle", "Other"),
        Field("siteOrProperty", "Site / property / unit", "text"),
        Field("floorZoneOrCommonArea", "Floor / zone / common area", "text"),
        Field("custodian", "Custodian / responsible officer", "text"),
        Field("accessStatus", "Access / keys / lockout status", "select", "Ready", "Restricted", "Access pending", "Locked out", "Not applicable"),
        Field("conditionStatus", "Condition status", "select", "Good", "Needs inspection", "Under maintenance", "Damaged", "Unsafe", "Retired"),
        Field("operationalStatus", "Facilities operational status", "select", "In service", "Standby", "Under repair", "Restricted use", "Out of service", "Transferred", "Disposed by Finance", "Inactive"),
        Field("serviceImpact", "Service impact", "select", "No impact", "Tenant affected", "Common area affected", "Safety risk", "Access restricted", "Utility affected", "Unit block required", "Business interruption"),
        Field("riskLevel", "Risk level", "select", "Low", "Medium", "High", "Critical"),
        Field("warrantyStatus", "Warranty status", "select", "Under warranty", "Expired", "Not applicable", "Unknown"),
        Field("warrantyExpiry", "Warranty expiry", "date"),
        Field("maintenanceReference", "Maintenance intake / job card / work order reference", "text"),
        Field("complaintReference", "Complaint / Helpdesk reference", "text"),
        Field("providerReference", "Service provider reference", "text"),
        Field("documentIndexReference", "Facilities document index reference", "text"),
        Field("financeActionStatus", "Finance Fixed Assets action", "select", "Not required", "Capitalization pending", "Capitalized", "Transfer requested", "Disposal requested", "Disposed", "Valuation requested", "Depreciation review requested"),
        Field("propertyManagementImpact", "Property Management impact", "select", "No impact", "Block unit", "Release unit", "Occupancy affected", "Handover affected", "Common area affected", "Tenant follow-up"),
        Field("nextInspectionDate", "Next inspection date", "date"),
        Field("reviewDate", "Review date", "date"),
        Field("assetNotes", "Operational notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> BillingServiceChargeFields() =>
    [
        Field("facilitiesBillingReference", "Facilities billing reference", "text"),
        Field("sourceLabel", "Source label", "select", "Source: Estate / Facilities -> Finance AR"),
        Field("sourceWorkspace", "Source workspace", "select", "Facilities Maintenance", "Complaint Management", "Service Provider Management", "Staff & Cleaner Duty Operations", "Facilities Asset Operating View", "Property Management", "Manual approved request"),
        Field("billingInstructionType", "Billing instruction type", "select", "New invoice", "Recurring service charge", "Common-area recovery", "Utility recovery", "Provider pass-through", "Maintenance recovery", "Receipt allocation", "Deposit collection", "Deposit refund / deduction", "Credit adjustment", "Debit adjustment", "Statement issue", "Arrears follow-up", "Demand notice", "Final account"),
        Field("chargeType", "Charge type", "select", "Service charge", "Cleaning", "Security", "Utilities", "Waste management", "Maintenance recovery", "Common-area charge", "Penalty", "Deposit", "Adjustment", "Other"),
        Field("propertyUnit", "Property / unit / common area", "text"),
        Field("serviceArea", "Service area / charge area", "text"),
        Field("occupantReference", "Tenant / occupant reference", "text"),
        Field("propertyManagementReference", "Property Management reference", "text"),
        Field("customerAccountReference", "Finance AR customer account reference", "text"),
        Field("customerAccountStatus", "Finance AR customer account status", "select", "Not linked", "Account requested", "Active", "On hold", "Closed", "Returned for correction"),
        Field("servicePeriod", "Service / billing period", "text"),
        Field("billingCycle", "Billing cycle", "select", "One-off", "Monthly", "Quarterly", "Semi-annual", "Annual", "Custom"),
        Field("allocationBasis", "Allocation basis", "select", "Fixed amount", "Area / square meter", "Unit count", "Meter reading", "Actual cost", "Percentage", "Consumption", "Manual approved allocation"),
        Field("allocationReference", "Allocation schedule / meter / cost reference", "text"),
        Field("amount", "Amount", "text"),
        Field("currency", "Currency", "text"),
        Field("taxTreatment", "Tax treatment", "select", "Taxable", "Exempt", "Inclusive", "Exclusive", "Not applicable", "Finance AR to confirm"),
        Field("dueDate", "Due date", "date"),
        Field("approvalStatus", "Facilities approval status", "select", "Draft", "Ready for review", "Approved", "Returned for correction", "Disputed", "Cancelled"),
        Field("financeArAction", "Finance AR action requested", "select", "Create invoice", "Record receipt", "Allocate receipt", "Issue statement", "Update arrears", "Handle deposit", "Post adjustment", "Reverse / credit", "Final account", "Confirm balance"),
        Field("financeArStatus", "Finance AR status", "select", "Not sent", "Sent to Finance AR", "Customer account linked", "Invoice created", "Receipt recorded", "Allocated", "Statement issued", "Arrears updated", "Deposit handled", "GL posted", "Returned for correction", "Closed"),
        Field("financeArInvoiceReference", "Finance AR invoice reference", "text"),
        Field("financeArReceiptReference", "Finance AR receipt reference", "text"),
        Field("statementReference", "Statement reference", "text"),
        Field("depositReference", "Deposit reference", "text"),
        Field("arrearsStatus", "Arrears status", "select", "Not applicable", "Current", "In arrears", "Payment promise", "Demand notice issued", "Escalated", "Disputed", "Cleared"),
        Field("paymentPromiseDate", "Payment promise date", "date"),
        Field("disputeStatus", "Dispute status", "select", "None", "Tenant query", "Under review", "Evidence requested", "Resolved", "Rejected", "Escalated"),
        Field("dmsDocumentReference", "Central DMS document reference", "text"),
        Field("documentStatus", "Billing document status", "select", "Not required", "Invoice pending DMS", "Receipt pending DMS", "Statement pending DMS", "Demand notice pending DMS", "Published to DMS", "Returned for document correction"),
        Field("closureStatus", "Facilities billing closure status", "select", "Open", "Awaiting approval", "Awaiting Finance AR", "Awaiting correction", "Awaiting payment", "In dispute", "Ready to close", "Closed"),
        Field("billingNotes", "Facilities billing notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> DocumentIndexFields() =>
    [
        Field("sourceModule", "Source module", "select", "Legal", "Procurement", "Project Management", "Maintenance", "Finance", "Estate", "Helpdesk", "Workflow", "Property Management", "Facilities", "Central Document Management"),
        Field("sourceLabel", "Source label", "select", "Source: Source Module -> Estate / Facilities -> Central DMS"),
        Field("sourceRecordReference", "Source record reference", "text"),
        Field("sourceDocumentReference", "Source document / file reference", "text"),
        Field("sourceOwner", "Source owner / custodian", "text"),
        Field("metadataTemplate", "Module metadata template", "select", "Facilities default", "Maintenance intake", "Complaint", "Service provider", "Staff and cleaner", "Asset operating view", "Property Management", "Lease", "Project handover", "Finance", "Procurement contract", "Legal", "Custom"),
        Field("documentTitle", "Document title", "text"),
        Field("documentType", "Document type", "select", "Lease", "Contract", "Certificate", "Invoice", "Payment record", "Property document", "Project handover", "Warranty", "Manual", "Inspection report", "Maintenance attachment", "Complaint evidence", "Photo evidence", "Approval", "Correspondence", "Other"),
        Field("relatedWorkspace", "Related Facilities workspace", "select", "Property Management", "Maintenance Intake", "Complaint Management", "Service provider", "Staff and cleaner", "Asset operating view", "Document index"),
        Field("relatedEntityReference", "Related property / lease / provider / asset / case reference", "text"),
        Field("requiredMetadataStatus", "Required metadata status", "select", "Complete", "Missing required fields", "Needs source owner review", "Needs Facilities review", "Ready for migration"),
        Field("accessLevel", "Access level", "select", "Open", "Internal", "Restricted", "Confidential"),
        Field("retentionCategory", "Retention category", "select", "Operational", "Legal", "Finance", "Compliance", "Permanent", "Archive review"),
        Field("confidentialityStatus", "Confidentiality status", "select", "Standard", "Restricted", "Confidential", "Legal sensitive", "Finance sensitive", "HR sensitive"),
        Field("effectiveDate", "Effective date", "date"),
        Field("expiryDate", "Expiry / renewal date", "date"),
        Field("reviewDate", "Review date", "date"),
        Field("expiryStatus", "Expiry status", "select", "Not applicable", "Current", "Expiring soon", "Expired", "Renewal requested", "Replacement pending"),
        Field("versionStatus", "Version status", "select", "Current", "Superseded", "Pending replacement", "Future central versioning"),
        Field("commentsStatus", "Comments status", "select", "Not required", "Requires central comments", "Comments exist in source module", "Comments pending migration"),
        Field("annotationStatus", "Annotation status", "select", "Not required", "Requires PDF viewer annotation", "Annotated in source module", "Annotation pending migration"),
        Field("auditReadiness", "Audit readiness", "select", "Ready", "Missing source owner", "Missing metadata", "Access review required", "Retention review required", "Version review required"),
        Field("centralDocumentStatus", "Central Document Management status", "select", "Not migrated", "Ready for migration", "Migrated", "Requires review"),
        Field("documentNotes", "Index / retrieval notes", "textarea")
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
