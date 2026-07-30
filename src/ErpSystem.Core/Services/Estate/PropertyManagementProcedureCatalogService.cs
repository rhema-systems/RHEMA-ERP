using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class PropertyManagementProcedureCatalogService : IPropertyManagementProcedureCatalogService
{
    private static readonly FacilitiesProcedureCatalogItem[] Procedures =
    [
        new("Property and Unit Register", "EstatePropertyManagementPropertyUnit", "Source: Estate / Property Management - Property Management ERP Module", "Estate land bank records pulled into Project Management, then completed properties, sites, units, spaces, common areas, and service areas pushed into Property Management receiving, readiness, availability, leasing, billing, facilities, maintenance, and records operations.", "Building2", 8, "teal"),
        new("Lease Management", "EstatePropertyManagementLease", "Source: Estate / Property Management - Property Management ERP BRS", "Lease operations for units released as Active for Leasing, with tenant/customer validation, commercial terms, Legal instrument routing, Finance AR billing package, occupancy activation, handover, renewal, termination, and document index handoffs.", "FileSignature", 9, "sky"),
        new("Tenant / Occupant Operations", "EstatePropertyManagementTenantOccupant", "Source: Estate / Property Management - Property Management ERP BRS", "Property Management tenant and occupant operations for CRM, Finance AR customer, business partner, or tenant administration source records, with unit links, lease context, access, contacts, billing status, service history, complaints, maintenance, move-in, move-out, and closure.", "Users", 8, "cyan"),
        new("Billing / Service Charge Operations", "EstatePropertyManagementBillingServiceCharge", "Source: Estate / Property Management -> Finance AR", "Property Management billing instructions, service charge allocations, deposits, arrears follow-up, dispute evidence, and correction loops routed into existing Finance AR invoices, receipts, statements, allocations, balances, and postings.", "CreditCard", 8, "amber"),
        new("Occupancy / Availability Operations", "EstatePropertyManagementOccupancyAvailability", "Source: Estate / Property Management - Property Management ERP BRS", "Property Management control of unit availability, reservations, occupation, maintenance blocks, management holds, releases, billing impact, and leasing visibility across linked workspaces.", "Home", 8, "emerald"),
        new("Move-in / Move-out / Handover Operations", "EstatePropertyManagementMoveInMoveOutHandover", "Source: Estate / Property Management - Property Management ERP BRS", "Property Management possession, scheduling, readiness, keys, access, inspection, clearance, defects, billing impact, unit release, and signed handover evidence for move-in, move-out, transfer, and handback events.", "KeyRound", 8, "violet"),
        new("Property Documents / Records Index", "EstatePropertyManagementDocumentRecordIndex", "Source: Estate / Property Management -> Central DMS", "Property Management document index, module metadata, source classification, access, retention, lifecycle, DMS reference, version, annotation, and comment readiness for property, unit, lease, tenant, billing, occupancy, handover, maintenance, and complaint records.", "FileText", 8, "lime")
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
            "EstatePropertyManagementPropertyUnit" => PropertyUnit(procedure),
            "EstatePropertyManagementLease" => Lease(procedure),
            "EstatePropertyManagementTenantOccupant" => TenantOccupant(procedure),
            "EstatePropertyManagementBillingServiceCharge" => BillingServiceCharge(procedure),
            "EstatePropertyManagementOccupancyAvailability" => OccupancyAvailability(procedure),
            "EstatePropertyManagementMoveInMoveOutHandover" => MoveInMoveOutHandover(procedure),
            "EstatePropertyManagementDocumentRecordIndex" => DocumentRecordIndex(procedure),
            _ => null
        };
    }

    private static FacilitiesProcedureWorkspace PropertyUnit(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive Estate and Project handoff", "Property Management Officer", "Receive the completed property, site, unit, space, common area, or service area after Project Management pulls ready land from the Estate land bank and marks the developed record ready for Property Management.", "Capture Estate land bank reference.", "Capture Project Management reference and delivery package.", "Capture handoff batch, approval, and development completion date.", "Record Source: Estate Land Bank -> Project Management -> Estate / Property Management."),
                Stage("Validate receiving register", "Property Management Officer", "Check the pushed property or unit against the Property Management receiving register before it can be used downstream.", "Confirm handoff batch or approval reference.", "Check property, site, block, floor, unit, space, common area, and service area identifiers.", "Check unit type, size, floor, block, serviceable area, and use classification.", "Set handoff state to Received Clean or Received with Exceptions."),
                Stage("Confirm property hierarchy and identifiers", "Property Management Supervisor", "Confirm the property structure is usable for leasing, occupancy, billing, facilities, maintenance, complaints, and reporting.", "Confirm parent property, site, block, floor, unit, and space hierarchy.", "Confirm unique property and unit identifiers.", "Confirm address, location, and service area details.", "Flag duplicate, missing, or conflicting identifiers."),
                Stage("Validate readiness and condition", "Property Management Supervisor / Facilities", "Confirm operational readiness, condition, defects, utilities, access, safety, facilities serviceability, and maintenance restrictions before release.", "Confirm readiness status.", "Capture condition status and inspection result.", "Link defects, snag items, or maintenance work orders.", "Confirm utilities, meters, access, and safety readiness."),
                Stage("Set occupancy and availability baseline", "Property Management Officer", "Create the initial Property Management occupancy and availability baseline for the received unit or space.", "Set baseline status as Available, Reserved, Occupied, Under Maintenance, Blocked, or Not Ready.", "Confirm leasing visibility.", "Link existing occupant, allocation, reservation, or restriction where applicable.", "Open Occupancy / Availability Operations for exceptions."),
                Stage("Prepare billing and service context", "Property Billing Officer / Facilities", "Capture billing readiness, charge responsibility, service charge basis, utility responsibility, facilities service links, and maintenance responsibility without creating Finance AR postings here.", "Confirm billable area or charge basis.", "Capture rent or service charge setup requirement.", "Capture utility, meter, service, and common area responsibilities.", "Route billing setup to Billing / Service Charge Operations where required."),
                Stage("Index receiving documents", "Property Records Officer", "Index project handoff documents, plans, certificates, inspection notes, completion approvals, allocation references, and property records while Central DMS owns storage and versioning.", "Create or link Property Documents / Records Index entry.", "Capture Central DMS reference where available.", "Tag land bank, project, property, unit, billing, facilities, maintenance, occupancy, and handover relationships.", "Flag missing mandatory documents."),
                Stage("Approve release into Property Management", "Property Manager", "Approve the received property or unit for operational use and release it to leasing, occupancy, billing, facilities, maintenance, complaint, reporting, and records workflows where applicable.", "Approve clean receiving or approved-exception receiving.", "Set release state such as Active for Leasing, Active for Operations, Blocked, Under Maintenance, or Not Ready.", "Notify linked workspaces.", "Confirm audit trail and source label.")
            ],
            [
                Doc("Project Management property or unit handoff", "Project Management", true),
                Doc("Estate land bank source reference", "Estate Land Management", true),
                Doc("Project completion or readiness approval", "Project Management", true),
                Doc("Site plan or layout", "Estate Records", false),
                Doc("Project, ownership, or allocation reference", "Project Management / Estate Records", true),
                Doc("Inspection, condition, snag, or defect note", "Property Management / Facilities", false),
                Doc("Utility, meter, access, or serviceability note", "Facilities / Project Management", false),
                Doc("Billing or service charge setup instruction", "Estate / Property Management", false),
                Doc("Property records index or DMS reference", "Property Records / Central DMS", false)
            ],
            PropertyUnitFields(),
            ["Estate land bank source reference", "Project-published property and unit reference", "Property Management receiving register", "Property hierarchy, identifiers, address, and serviceable-area baseline", "Readiness, condition, utilities, access, facilities, and maintenance validation trail", "Occupancy and availability baseline with leasing visibility", "Billing, service charge, utility, facilities, and maintenance context", "Document-linked Property Management file and DMS reference", "Approved operational release state"],
            [
                Handoff("Estate Land Management", "Project Management", "Land Bank Ready"),
                Handoff("Project Management", "Project Management Delivery Team", "Pulled by Project"),
                Handoff("Project Management Delivery Team", "Project Management Approver", "Development Complete"),
                Handoff("Project Management", "Property Management", "Pushed to Property Management"),
                Handoff("Property Management Officer", "Property Management Supervisor", "Receiving register, identifiers, readiness, or exception needs validation"),
                Handoff("Property Management Supervisor", "Facilities / Maintenance Management", "Defect, serviceability, access, safety, utility, or maintenance issue must be cleared or linked"),
                Handoff("Property Management Officer", "Occupancy / Availability Operations", "Baseline unit status, leasing visibility, block, hold, or exception must be created"),
                Handoff("Property Billing Officer", "Billing / Service Charge Operations", "Billing, service charge, utility, or charge basis setup is required"),
                Handoff("Property Records Officer", "Property Documents / Records Index", "Handoff documents and property records must be indexed"),
                Handoff("Property Manager", "Leasing / Occupancy / Billing / Facilities / Maintenance", "Unit is released as Active for Leasing, Active for Operations, Blocked, Under Maintenance, or Not Ready")
            ]);

    private static FacilitiesProcedureWorkspace Lease(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Open lease from active unit", "Lease Officer", "Open the lease only from a Property Management unit released as Active for Leasing.", "Capture the Property Management receiving reference.", "Confirm handoff state is Active for Leasing.", "Select property, unit, or space.", "Record Source: Estate / Property Management - Lease Management."),
                Stage("Validate tenant and customer source", "Lease Officer", "Confirm the tenant, occupant, client, CRM, business partner, or Finance AR customer record before lease terms are drafted.", "Link tenant / occupant profile.", "Verify customer account or request Finance AR customer setup.", "Confirm identity, contact, and billing party.", "Flag missing source record before Legal or Finance handoff."),
                Stage("Capture commercial terms", "Lease Officer / Property Billing Officer", "Capture rent, service charge, deposit, escalation, billing cycle, renewal option, termination rule, and possession conditions before approval.", "Record rent, service charge, deposit, and billing cycle.", "Capture start, end, renewal, and notice dates.", "Confirm possession and handover requirements.", "Prepare billing instruction package for later Finance AR handoff."),
                Stage("Approve lease instruction package", "Property Manager", "Approve the Property Management lease instruction package before routing Legal drafting or Finance AR billing setup.", "Confirm unit is still available or reserved.", "Confirm commercial terms and tenant source.", "Approve lease instruction or return for correction.", "Record Source: Estate / Property Management - Lease Management."),
                Stage("Route legal instrument", "Lease Officer / Legal", "Send the approved lease, renewal, variation, termination, consent, or sublease request into the existing Legal instrument procedure.", "Open or reference Legal lease procedure.", "Attach approved property, tenant, and commercial terms.", "Track Legal drafting, vetting, execution, and returned-correction status."),
                Stage("Activate Finance AR billing package", "Property Billing Officer / Finance AR", "Route approved billing terms into Billing / Service Charge Operations and Finance AR for customer account, invoice, receipt, deposit, allocation, arrears, and statement actions.", "Create or link customer account.", "Open billing / service charge operation.", "Capture invoice, receipt, deposit, statement, or correction reference.", "Do not duplicate Finance AR ledger records in Lease Management."),
                Stage("Activate occupancy and handover", "Property Management Officer", "Update Occupancy / Availability and Move-in / Move-out / Handover once the lease is approved or executed.", "Mark unit Reserved or Occupied.", "Link tenant to unit.", "Open move-in or possession handover where required.", "Record access, possession, and effective date."),
                Stage("Index lease documents", "Property Records Officer", "Index signed leases, variations, renewals, terminations, notices, approvals, and supporting records in the Property Documents / Records Index while Central DMS owns repository/versioning.", "Capture Legal instrument reference.", "Capture DMS or records index reference.", "Tag property, unit, tenant, billing, occupancy, and handover links."),
                Stage("Monitor renewal, termination, and close", "Lease Officer", "Track renewals, expiries, terminations, variations, arrears referrals, handback, and closed lease records.", "Monitor renewal and notice dates.", "Route termination or renewal to Legal where needed.", "Trigger final billing and occupancy release.", "Archive or supersede closed lease documents.")
            ],
            [
                Doc("Property Management Active for Leasing reference", "Property Management", true),
                Doc("Approved lease instruction package", "Estate / Property Management", true),
                Doc("Signed lease document", "Legal / Tenant", true),
                Doc("Tenant, CRM, business partner, or Finance AR customer reference", "Tenant / CRM / Finance AR", true),
                Doc("Rent, service charge, deposit, renewal, and billing terms", "Property Management / Finance AR", true),
                Doc("Legal instrument procedure reference", "Legal", false),
                Doc("Billing / Service Charge operation reference", "Estate / Property Management", false),
                Doc("Occupancy, availability, or handover reference", "Property Management", false),
                Doc("Termination, renewal, variation, or notice document", "Tenant / Estate / Legal", false),
                Doc("Property records index or DMS reference", "Property Records / Central DMS", false)
            ],
            LeaseFields(),
            ["Approved lease instruction package", "Linked Active for Leasing unit", "Linked tenant / occupant and Finance AR customer context", "Commercial terms and billing package", "Legal instrument request and execution status", "Billing / Service Charge and Finance AR references", "Occupancy, availability, and handover updates", "Lease document index reference", "Renewal, termination, expiry, and closeout trail"],
            [
                Handoff("Property Management", "Lease Officer", "Unit is Active for Leasing"),
                Handoff("Lease Officer", "Tenant / Occupant Operations", "Tenant, occupant, CRM, or customer source must be linked"),
                Handoff("Lease Officer", "Property Manager", "Lease instruction package requires approval"),
                Handoff("Property Manager", "Legal", "Lease instrument, renewal, variation, termination, consent, or sublease drafting is required"),
                Handoff("Lease Officer", "Billing / Service Charge Operations", "Approved billing terms are ready for Finance AR instruction"),
                Handoff("Billing / Service Charge Operations", "Finance AR", "Customer account, invoice, receipt, deposit, allocation, statement, or arrears action is required"),
                Handoff("Legal", "Property Records Officer", "Executed lease instrument is ready for indexing"),
                Handoff("Lease Officer", "Occupancy / Availability Operations", "Reservation, activation, expiry, termination, or release changes unit status"),
                Handoff("Lease Officer", "Move-in / Move-out / Handover Operations", "Possession, move-in, move-out, transfer, or handback must be scheduled")
            ]);

    private static FacilitiesProcedureWorkspace TenantOccupant(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Reference existing tenant or customer", "Property Management Officer", "Reference the existing CRM, Finance AR customer, business partner, or tenant administration record before opening the Property Management occupancy profile.", "Capture CRM or Finance AR customer reference.", "Confirm tenant / occupant type.", "Record Source: Estate / Property Management - Tenant / Occupant Operations."),
                Stage("Validate occupant eligibility and source quality", "Property Management Supervisor", "Validate identity/source quality, customer account status, occupant category, contactability, and operating restrictions before linking to a unit or lease.", "Confirm identity and source record quality.", "Confirm Finance AR customer account status where applicable.", "Flag restricted, disputed, duplicate, or incomplete source records.", "Confirm source ownership remains with CRM, Finance AR, Business Partner, or Tenant Administration."),
                Stage("Link occupant to property, unit, and lease", "Property Management Officer", "Connect the tenant, occupant, resident, client, or licensee to the Estate property, site, unit, space, lease, reservation, or occupancy record.", "Capture property and unit reference.", "Confirm occupancy status.", "Link active lease where applicable.", "Confirm occupancy / availability update required."),
                Stage("Maintain contact, access, and communication context", "Property Management Desk", "Maintain operational contact, emergency contact, permitted users, access status, communication preference, and service instructions without replacing the customer master record.", "Confirm primary contact.", "Record access, key, card, permit, or security instructions.", "Capture emergency contact and communication preference.", "Flag lost access, restriction, or replacement need."),
                Stage("Confirm lease and billing context", "Lease Officer / Property Billing Officer", "Reference the lease, billing account, invoices, receipts, arrears, deposits, statements, and payment promises that remain in Legal, Billing Operations, and Finance AR.", "Link lease reference.", "Capture Finance AR customer account.", "Record billing, arrears, deposit, and statement status.", "Open Billing / Service Charge follow-up where required."),
                Stage("Track occupancy and handover events", "Property Management Officer", "Track move-in, move-out, transfer, renewal, termination, complaint, maintenance, access, handover, and unit release events that affect the occupant profile.", "Record occupancy event type.", "Link move-in / move-out / handover reference.", "Update Occupancy / Availability where required.", "Capture event date and responsible officer."),
                Stage("Track service, complaint, and maintenance context", "Property Management Desk", "Maintain the occupant-facing service history and route execution to Facilities, Maintenance Management, or Helpdesk without duplicating those modules.", "Link complaint or helpdesk case.", "Link maintenance work order or job card.", "Capture service entitlement or restriction.", "Track open exceptions and follow-up owner."),
                Stage("Review closure or transition", "Property Manager", "Review expired, terminated, vacated, transferred, inactive, disputed, or deceased/closed occupant records before closure or transition.", "Confirm close-out documents.", "Confirm billing, access, handover, maintenance, and complaint follow-ups.", "Release unit where applicable.", "Archive or close occupancy profile.")
            ],
            [
                Doc("CRM, Finance AR customer, or tenant administration reference", "CRM / Finance AR / Tenant Administration", true),
                Doc("Identity, source-quality, or customer validation note", "Property Management Supervisor", true),
                Doc("Lease, reservation, or occupancy instruction", "Property Management / Legal", false),
                Doc("Move-in or handover record", "Property Management Officer", false),
                Doc("Identification, contact, or access instruction", "Tenant / Client / Property Management", false),
                Doc("Billing, statement, or arrears reference", "Finance AR", false),
                Doc("Complaint, service, or maintenance reference", "Facilities / Maintenance / Helpdesk", false),
                Doc("Move-out, transfer, closure, or unit release note", "Property Manager", false)
            ],
            TenantOccupantFields(),
            ["Property Management tenant / occupant operations profile", "Linked CRM, Finance AR, business partner, or tenant administration source reference", "Linked property, unit, lease, occupancy, and handover references", "Contact, emergency contact, access, permitted users, and communication context", "Billing, deposit, statement, arrears, and payment promise context", "Complaint, service, maintenance, and access history", "Move-in, move-out, transfer, closure, and unit release history", "Closed or archived occupancy profile"],
            [
                Handoff("CRM / Finance AR / Tenant Administration", "Property Management Officer", "Tenant, customer, or client source record is available for occupancy operations"),
                Handoff("Property Management Officer", "Property Management Supervisor", "Tenant source, customer account, eligibility, or restriction requires validation"),
                Handoff("Property Management Officer", "Occupancy / Availability Operations", "Occupant must be linked to an active property, unit, space, or release event"),
                Handoff("Property Management Officer", "Lease Officer / Legal", "Lease, renewal, variation, termination, or occupancy instrument is required"),
                Handoff("Property Management Officer", "Move-in / Move-out / Handover Operations", "Move-in, move-out, transfer, handback, access, or possession event is required"),
                Handoff("Lease Officer / Property Billing Officer", "Billing / Finance AR", "Billing account, invoice, receipt, deposit, arrears, statement, payment promise, or correction action is required"),
                Handoff("Property Management Desk", "Facilities / Maintenance / Helpdesk", "Occupant service request, complaint, access issue, or maintenance event is raised"),
                Handoff("Property Manager", "Property Management / Finance AR / Records", "Move-out, transfer, closure, unit release, billing closeout, or archive requires final action")
            ]);

    private static FacilitiesProcedureWorkspace BillingServiceCharge(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive billing trigger", "Property Management Officer", "Open a billing or service charge operation from a lease, tenant / occupant profile, property unit, service charge review, deposit event, arrears review, or approved manual request.", "Capture the source workspace.", "Capture property, unit, occupant, and lease references.", "Record Source: Estate / Property Management - Billing / Service Charge Operations."),
                Stage("Validate customer and account context", "Property Management Supervisor", "Confirm the billable party, property, unit, lease, occupancy status, and Finance AR customer account before any invoice or receipt action is requested.", "Verify property and unit status.", "Verify occupant, lease, or customer account reference.", "Confirm AR customer account is active or request account creation.", "Flag dispute or exception before Finance posting."),
                Stage("Calculate charge and allocation basis", "Property Billing Officer", "Calculate rent, service charge, deposit, penalty, adjustment, refund, or arrears instruction using the approved amount basis and billing period.", "Confirm charge type and billing instruction type.", "Confirm amount, currency, tax handling, due date, and billing period.", "Attach service charge schedule or allocation approval.", "Record allocation basis and calculation note."),
                Stage("Approve AR instruction package", "Property Manager", "Approve the Property Management billing instruction package before Finance AR creates invoices, receipts, allocations, statements, balances, or GL postings.", "Review supporting documents.", "Confirm source and property context.", "Approve or return billing instruction.", "Record Source: Estate / Property Management -> Finance AR."),
                Stage("Send to Finance AR", "Property Billing Officer / Finance AR", "Send the approved instruction to Finance AR for customer account linkage, invoice creation, receipt recording, allocation, statement issue, balance update, and GL posting.", "Capture handoff date.", "Capture Finance AR action requested.", "Capture expected response date.", "Link Finance AR customer record."),
                Stage("Track AR posting or return", "Finance AR / Property Billing Officer", "Track Finance AR outcome without duplicating the finance ledger inside Property Management.", "Capture invoice, receipt, allocation, statement, balance, or GL batch reference.", "Capture returned-for-correction reason.", "Assign correction owner if Finance AR rejects or queries the instruction."),
                Stage("Follow up arrears, disputes, or corrections", "Property Management Officer", "Use Property Management for occupant communication, arrears follow-up, payment promises, service charge questions, dispute evidence, and correction resubmission while Finance AR keeps the account balance.", "Record arrears or dispute status.", "Capture tenant query and evidence.", "Track promise-to-pay or escalation.", "Resubmit corrected instruction where required."),
                Stage("Close billing operation", "Property Manager", "Close the operation once Finance AR posting, receipt, statement, dispute, correction, arrears, or final account outcome is confirmed and the property, lease, and tenant context is updated.", "Confirm Finance AR result.", "Update lease, tenant, and property notes.", "Archive linked documents and references.", "Confirm no duplicate billing action remains open.")
            ],
            [
                Doc("Approved billing instruction package", "Estate / Property Management", true),
                Doc("Lease or occupancy billing instruction", "Estate / Property Management", true),
                Doc("Finance AR customer account reference", "Finance AR", true),
                Doc("Service charge allocation schedule", "Property Management / Finance AR", true),
                Doc("Charge calculation or approval note", "Property Billing Officer", true),
                Doc("Deposit, arrears, or dispute evidence", "Tenant / Property Management", false),
                Doc("Invoice, receipt, allocation, statement, balance, or GL reference", "Finance AR", false),
                Doc("Returned correction or AR query note", "Finance AR / Property Manager", false)
            ],
            BillingServiceChargeFields(),
            ["Approved Property Management billing instruction package", "Linked Finance AR customer account", "Charge calculation, allocation, and billing schedule", "Finance AR action request and handoff trail", "Invoice, receipt, allocation, statement, balance, and GL references", "Returned correction and resubmission trail", "Arrears, payment promise, and dispute follow-up trail", "Closed billing operation with property context updated"],
            [
                Handoff("Lease / Tenant / Property Management", "Property Management Officer", "Billing, service charge, deposit, arrears, dispute, or adjustment trigger is raised"),
                Handoff("Property Management Officer", "Property Management Supervisor", "Customer, lease, property, unit, and account context requires validation"),
                Handoff("Property Billing Officer", "Property Manager", "Charge calculation and billing instruction package requires approval"),
                Handoff("Property Manager", "Finance AR", "Approved instruction is ready for invoice, receipt, allocation, statement, balance, or posting action"),
                Handoff("Finance AR", "Property Billing Officer", "Invoice, receipt, allocation, statement, balance, GL, or returned-for-correction result is available"),
                Handoff("Property Billing Officer", "Finance AR", "Corrected instruction is ready for resubmission"),
                Handoff("Property Management Officer", "Tenant / Occupant / Finance AR", "Arrears follow-up, dispute evidence, payment promise, statement query, or correction is required"),
                Handoff("Property Manager", "Finance AR / Records", "Billing operation is closed and references are ready for filing")
            ]);

    private static FacilitiesProcedureWorkspace OccupancyAvailability(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive status change trigger", "Property Management Officer", "Open an occupancy or availability operation from a property unit, lease action, tenant move-in or move-out, maintenance block, complaint, access issue, billing hold, or management decision.", "Capture the source workspace and source reference.", "Capture property, unit, space, lease, tenant, or maintenance reference.", "Record Source: Estate / Property Management - Occupancy / Availability Operations."),
                Stage("Confirm current unit state", "Property Management Officer", "Confirm the current unit state before any new reservation, occupation, block, release, or closure is approved.", "Verify Property and Unit Register state.", "Check current lease, occupant, handover, and billing links.", "Confirm the current availability state is not stale or duplicated.", "Flag mismatches for supervisor review."),
                Stage("Validate restrictions and readiness", "Property Management Supervisor", "Confirm the unit can move to the requested status without breaking lease, tenant, maintenance, safety, access, billing, legal, document, or management controls.", "Check active lease or reservation restrictions.", "Check maintenance, complaint, safety, access, and defect restrictions.", "Check billing hold, arrears hold, deposit, or final account impact.", "Confirm document or approval requirements."),
                Stage("Apply status decision", "Property Manager", "Approve the requested unit status decision and define whether the unit is visible for leasing, reserved, occupied, under maintenance, blocked, released, or closed.", "Approve or return the requested status.", "Set leasing visibility and availability state.", "Record hold, block, release, or closure reason.", "Capture effective date and review date."),
                Stage("Coordinate linked workspaces", "Property Management Desk", "Notify the workspaces affected by the status change while keeping Property Management as the occupancy and availability source.", "Notify Lease Management when availability affects leasing.", "Notify Tenant / Occupant Operations for move-in, move-out, or transfer.", "Notify Facilities or Maintenance for block and release actions.", "Notify Billing / Service Charge Operations when charges must start, stop, or change."),
                Stage("Route billing and handover impact", "Property Billing Officer / Handover Officer", "Route status changes that affect rent, service charge, deposits, final accounts, possession, keys, access, or inspection into the correct Property Management or Finance AR follow-up.", "Open Billing / Service Charge follow-up where required.", "Open Move-in / Move-out / Handover where possession changes.", "Capture Finance AR instruction reference if charges are affected.", "Do not post invoices or receipts in this workspace."),
                Stage("Publish availability view", "Property Manager", "Publish the approved status for dashboards, availability lists, lease selection, unit search, and operational reporting.", "Confirm dashboard visibility.", "Confirm whether the unit is selectable for leasing.", "Capture exception notes for blocked, held, or under-maintenance units.", "Record source label for downstream users."),
                Stage("Review, release, or close status operation", "Property Manager", "Review temporary holds, maintenance blocks, reservations, releases, and closed operations until the unit is either cleanly available, occupied, blocked, or closed.", "Confirm linked actions are complete.", "Archive move-in, move-out, hold, release, or closure evidence.", "Schedule next review where status is temporary.", "Close the status operation with an audit trail.")
            ],
            [
                Doc("Unit status change instruction", "Estate / Property Management", true),
                Doc("Lease, reservation, or occupancy reference", "Lease Management / Tenant Operations", false),
                Doc("Move-in, move-out, transfer, or handover note", "Property Management Officer", false),
                Doc("Maintenance hold or release reference", "Facilities / Maintenance Management", false),
                Doc("Billing hold, arrears, deposit, or final account reference", "Billing / Finance AR", false),
                Doc("Inspection, access, or complaint note", "Facilities / Helpdesk / Property Management", false),
                Doc("Status approval, hold, block, release, or closure note", "Property Manager", true),
                Doc("Property records index or DMS reference", "Property Records / Central DMS", false)
            ],
            OccupancyAvailabilityFields(),
            ["Property Management occupancy and availability status record", "Current and requested unit status validation trail", "Approved unit availability state and leasing visibility", "Lease, tenant, handover, maintenance, complaint, billing, and document links", "Billing start, stop, hold, adjustment, or final account instruction reference", "Availability list or blocked-unit exception trail", "Move-in, move-out, transfer, hold, block, release, and closure history", "Closed status operation with audit trail"],
            [
                Handoff("Property and Unit Register", "Property Management Officer", "Unit status is ready for occupancy or availability update"),
                Handoff("Lease Officer", "Property Management Officer", "Lease, renewal, termination, reservation, or expiry changes unit status"),
                Handoff("Tenant / Occupant Operations", "Property Management Officer", "Move-in, move-out, transfer, or occupancy event changes unit status"),
                Handoff("Facilities / Maintenance Management", "Property Management Officer", "Unit must be blocked, placed under maintenance, or released after maintenance"),
                Handoff("Property Management Officer", "Property Management Supervisor", "Status mismatch, restriction, or readiness exception requires validation"),
                Handoff("Property Manager", "Billing / Service Charge Operations", "Billing must start, stop, hold, adjust, or close because status changed"),
                Handoff("Property Manager", "Move-in / Move-out / Handover Operations", "Possession, keys, access, inspection, or release event must be scheduled"),
                Handoff("Property Manager", "Property Records", "Status operation is closed and availability history is ready for filing")
            ]);

    private static FacilitiesProcedureWorkspace MoveInMoveOutHandover(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive handover trigger", "Property Management Officer", "Open a handover operation from a lease activation, tenant move-in, move-out notice, transfer, unit return, post-maintenance release, or management-approved handback.", "Capture the source workspace and source reference.", "Capture property, unit, occupant, lease, and availability references.", "Record Source: Estate / Property Management - Move-in / Move-out / Handover Operations."),
                Stage("Schedule handover event", "Property Management Desk", "Schedule the move-in, move-out, transfer, return of keys, post-maintenance release, or unit handback and assign responsible officers.", "Set scheduled date and handover window.", "Assign property officer and inspection officer.", "Confirm tenant, representative, facilities, security, or maintenance attendance.", "Capture reschedule or no-show rules."),
                Stage("Verify readiness and clearance", "Property Management Supervisor", "Confirm the unit, tenant, lease, billing, availability, maintenance, access, and facilities context before possession changes hands.", "Confirm handover type.", "Check occupancy and availability status.", "Confirm lease, termination, transfer, or occupancy approval.", "Check Finance AR clearance, deposit, arrears, billing start, billing stop, or final account impact.", "Check open maintenance, safety, complaint, or access restrictions."),
                Stage("Conduct condition and inventory inspection", "Property Management Officer / Facilities", "Record the property condition, inventory, meter readings, defects, access items, photographs, and safety notes at move-in, move-out, transfer, or handback.", "Capture inspection result.", "Record meter or utility readings.", "List defects, missing items, damaged items, or tenant chargeback items.", "Attach photographs or inspection notes.", "Flag items requiring Maintenance Management execution."),
                Stage("Manage keys, access, and possession", "Property Management Desk / Security", "Issue, receive, replace, disable, or reconcile keys, cards, permits, remotes, access tokens, parking access, and possession documents.", "Record keys and access items issued or returned.", "Update access status.", "Confirm possession issued, returned, partial, disputed, or restricted.", "Capture lost access item charges or replacement follow-up."),
                Stage("Route billing, defect, and service impact", "Property Billing Officer / Facilities", "Route handover outcomes that affect billing, deposits, deductions, final accounts, maintenance defects, facilities service readiness, or tenant charges to the owning modules.", "Open Billing / Service Charge follow-up where charges start, stop, adjust, or close.", "Capture Finance AR clearance, deposit, final account, or receipt reference.", "Raise or link Maintenance Management work order for defects.", "Route complaint or service follow-up where required."),
                Stage("Update linked property operations", "Property Management Officer", "Push handover outcomes to Occupancy / Availability, Tenant / Occupant, Lease Management, Property Records, Billing / Service Charge, Facilities, and Maintenance workspaces.", "Update occupancy and availability status.", "Update tenant / occupant move-in, move-out, transfer, access, or closure record.", "Update lease possession, expiry, termination, or renewal context.", "Index handover evidence and DMS references where available."),
                Stage("Close handover file", "Property Manager", "Close the handover after signatures, clearance, inspection evidence, access reconciliation, billing/defect routing, and linked workspace updates are complete.", "Confirm signed handover certificate.", "Confirm linked actions are complete or assigned.", "Confirm unit release, occupation, block, or closure status.", "Archive handover documents and audit notes.")
            ],
            [
                Doc("Move-in, move-out, transfer, or handover instruction", "Estate / Property Management", true),
                Doc("Lease, occupancy, or termination reference", "Lease Management / Tenant Operations", false),
                Doc("Scheduled handover appointment or attendance note", "Property Management Desk", false),
                Doc("Condition inspection and inventory checklist", "Property Management / Facilities", true),
                Doc("Keys, access, permit, or possession schedule", "Property Management Desk", true),
                Doc("Finance AR clearance, deposit, or final account reference", "Finance AR / Billing Operations", false),
                Doc("Signed handover certificate", "Tenant / Occupant / Property Manager", true),
                Doc("Photographs, meter readings, or utility evidence", "Property Management / Facilities", false),
                Doc("Maintenance defect or release reference", "Facilities / Maintenance Management", false),
                Doc("Property records index or DMS reference", "Property Records / Central DMS", false)
            ],
            MoveInMoveOutHandoverFields(),
            ["Move-in, move-out, transfer, or handover record", "Scheduled handover appointment and attendance trail", "Readiness, clearance, restriction, and approval validation", "Signed possession and handover certificate", "Condition, inventory, meter, photo, and access trail", "Billing start, stop, clearance, deposit, deduction, or final account instruction", "Facilities, maintenance, service, or complaint follow-up reference", "Updated occupancy, availability, tenant, lease, and document index records", "Closed handover file"],
            [
                Handoff("Lease / Tenant / Occupant Operations", "Property Management Officer", "Move-in, move-out, transfer, termination, or handover event is approved"),
                Handoff("Property Management Officer", "Property Management Desk", "Handover event must be scheduled and assigned"),
                Handoff("Property Management Officer", "Property Management Supervisor", "Readiness, clearance, and handover context require validation"),
                Handoff("Property Management Officer", "Facilities / Maintenance Management", "Inspection, defects, access, or release follow-up is required"),
                Handoff("Property Billing Officer", "Billing / Finance AR", "Billing start, stop, deposit, deduction, clearance, receipt, or final account action is required"),
                Handoff("Property Management Officer", "Occupancy / Availability Operations", "Unit possession or release changes availability status"),
                Handoff("Property Management Officer", "Tenant / Occupant Operations", "Move-in, move-out, transfer, access, closure, or service context must be updated"),
                Handoff("Property Manager", "Property Records", "Handover is signed, reconciled, indexed, and ready for filing")
            ]);

    private static FacilitiesProcedureWorkspace DocumentRecordIndex(FacilitiesProcedureCatalogItem procedure) =>
        Workspace(
            procedure,
            [
                Stage("Receive document index trigger", "Property Management Records Officer", "Open a property document index entry from a property, unit, lease, tenant / occupant, billing, occupancy, handover, facilities, maintenance, complaint, Legal, Finance AR, Project Management, or approved external source.", "Capture source workspace and source operation reference.", "Capture property, unit, tenant, lease, billing, occupancy, handover, maintenance, complaint, or legal reference.", "Record Source: Estate / Property Management - Property Documents / Records Index."),
                Stage("Classify document source and purpose", "Property Management Records Officer", "Classify why the document is being indexed and whether it supports ownership, project handoff, lease, tenant, billing, occupancy, handover, maintenance, compliance, correspondence, or audit activity.", "Select document category and type.", "Set document purpose and source system.", "Capture source document date and originating department.", "Flag documents that belong to another module's execution record."),
                Stage("Define module metadata template", "Property Management Records Officer", "Apply the Property Management metadata template so Central DMS can classify, search, version, annotate, comment on, and secure the document correctly by module.", "Select module metadata template.", "Capture required module fields.", "Tag property, unit, tenant, lease, billing, occupancy, handover, maintenance, complaint, or legal relationships.", "Record metadata completeness status."),
                Stage("Validate document control", "Document Control Officer", "Confirm ownership, completeness, quality, confidentiality, access, retention, and mandatory references before the record is published into the Property Management index.", "Confirm document owner and custodian.", "Confirm mandatory references and evidence quality.", "Set confidentiality and access level.", "Confirm retention category, expiry, or next review date."),
                Stage("Link central DMS reference", "Document Control Officer / Central DMS", "Store only the document reference and metadata in Property Management; the central DMS owns file storage, versions, Syncfusion annotations, comments, renditions, and repository permissions.", "Capture DMS document reference.", "Capture version or revision reference.", "Capture repository status.", "Flag migration or DMS-link gaps without duplicating the file."),
                Stage("Track version, annotation, and comments", "Document Control Officer / Central DMS", "Track document version status, annotation status, comment status, and unresolved review items so Property Management can follow up while the central DMS remains the document collaboration owner.", "Capture version status.", "Capture annotation and comment status.", "Record open review or comment owner.", "Route annotation, comment, or version changes to central DMS."),
                Stage("Publish records index", "Property Manager", "Publish the approved index entry for property operations, workflow cases, searches, audits, reports, and handoffs without duplicating the document repository.", "Confirm index visibility.", "Confirm linked workspaces can find the document.", "Confirm source label and module relationships.", "Record approval or exception note."),
                Stage("Review, archive, supersede, or restrict", "Property Management Records Officer", "Review expired, superseded, replaced, restricted, archived, rejected, or transferred document references and keep the Property Management index aligned with central DMS lifecycle status.", "Mark superseded, archived, restricted, or rejected references.", "Update review date and lifecycle status.", "Route versioning, annotation, comment, or permission changes to central DMS.", "Close the index action with an audit trail.")
            ],
            [
                Doc("Document source instruction", "Estate / Property Management", true),
                Doc("Property, unit, tenant, lease, billing, occupancy, or handover reference", "Property Management Workspace", true),
                Doc("Maintenance, complaint, Legal, Finance AR, or Project source reference", "Linked Source Module", false),
                Doc("Module metadata template", "Property Management / Central DMS", true),
                Doc("Access, confidentiality, retention, and lifecycle rule", "Document Control Officer", true),
                Doc("DMS document reference", "Central DMS", false),
                Doc("Version, annotation, or comment reference", "Central DMS", false),
                Doc("Metadata completeness or exception note", "Property Management Records Officer", false),
                Doc("Retention, archive, restriction, or supersession approval", "Document Control Officer", false)
            ],
            DocumentRecordIndexFields(),
            ["Property Management records index entry", "Document source, purpose, and module classification", "Module-specific metadata template and completeness status", "Property, unit, tenant, lease, billing, occupancy, handover, maintenance, complaint, Legal, Finance AR, and Project Management links", "Central DMS document, repository, version, annotation, and comment references", "Access, confidentiality, retention, lifecycle, and review trail", "Published searchable index without duplicate file storage", "Archived, superseded, restricted, rejected, or transferred record index status"],
            [
                Handoff("Property Management Workspace", "Property Management Records Officer", "Document or record must be indexed against a property operation"),
                Handoff("Property Management Records Officer", "Document Control Officer", "Metadata, access, retention, or confidentiality requires validation"),
                Handoff("Document Control Officer", "Central DMS", "File storage, versioning, Syncfusion annotation, comments, repository permission, or rendition control is required"),
                Handoff("Central DMS", "Property Management Records Officer", "Document reference, version, annotation, comment, or storage status is available"),
                Handoff("Property Manager", "Property Management Workspaces", "Approved record index is available for linked operations"),
                Handoff("Property Management Records Officer", "Linked Source Module", "Missing, incorrect, rejected, or conflicting source document must be corrected by the owner"),
                Handoff("Property Management Records Officer", "Central DMS / Records", "Document is superseded, archived, restricted, rejected, transferred, or due for review")
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

    private static IReadOnlyList<FacilitiesWorkspaceField> PropertyUnitFields() =>
    [
        Field("landBankReference", "Estate land bank reference", "text"),
        Field("landBankStatus", "Estate land bank status", "select", "Ready for Project", "Pulled by Project", "Developed", "Partially developed", "Exception", "Closed"),
        Field("projectReference", "Project Management reference", "text"),
        Field("projectName", "Source project name", "text"),
        Field("handoffBatch", "Handoff batch / approval reference", "text"),
        Field("projectCompletionDate", "Project completion / handoff date", "date"),
        Field("handoffState", "Handoff state", "select", "Land Bank Ready", "Pulled by Project", "Development Complete", "Pushed to Property Management", "Received with Exceptions", "Received Clean", "Active for Leasing", "Active for Operations", "Blocked", "Under Maintenance", "Not Ready", "Closed"),
        Field("releaseState", "Property Management release state", "select", "Not released", "Active for Leasing", "Active for Operations", "Reserved", "Occupied", "Blocked", "Under Maintenance", "Not Ready", "Closed"),
        Field("propertyId", "Property ID", "text"),
        Field("siteName", "Site / property name", "text"),
        Field("parentPropertyReference", "Parent property / site reference", "text"),
        Field("blockReference", "Block / phase reference", "text"),
        Field("unitOrSpaceNumber", "Unit / space number", "text"),
        Field("unitType", "Unit / space type", "select", "Residential unit", "Commercial unit", "Office", "Shop", "Yard", "Common area", "Service space", "Parking", "Utility area", "Amenity"),
        Field("useClassification", "Use classification", "select", "Residential", "Commercial", "Mixed-use", "Office", "Retail", "Industrial", "Common area", "Service area", "Utility", "Other"),
        Field("floorOrBlock", "Floor / block", "text"),
        Field("addressOrLocation", "Address / location", "text"),
        Field("size", "Size / area", "text"),
        Field("serviceableArea", "Serviceable / chargeable area", "text"),
        Field("readinessStatus", "Readiness status", "select", "Ready for Property Management", "Ready with exceptions", "Requires inspection", "Maintenance required", "Documents pending", "Billing setup pending", "Not ready"),
        Field("occupancyStatus", "Occupancy status", "select", "Available", "Reserved", "Occupied", "Under Maintenance", "Blocked", "Not Ready", "Closed"),
        Field("leasingVisibility", "Leasing visibility", "select", "Selectable", "Hidden", "Manager approval required", "Reserved only", "Blocked", "Closed"),
        Field("conditionStatus", "Condition status", "select", "Good", "Minor defects", "Major defects", "Pending inspection", "Requires maintenance", "Safety restriction"),
        Field("utilityMeterStatus", "Utility / meter status", "select", "Not applicable", "Pending", "Installed", "Verified", "Defective", "Shared meter", "Requires setup"),
        Field("accessStatus", "Access status", "select", "Not issued", "Available", "Restricted", "Missing keys", "Security setup pending", "Disabled"),
        Field("facilitiesReadiness", "Facilities readiness", "select", "Ready", "Ready with exceptions", "Cleaning pending", "Security pending", "Utilities pending", "Maintenance pending", "Not ready"),
        Field("maintenanceReference", "Maintenance / defect reference", "text"),
        Field("billingSetupStatus", "Billing / service charge setup status", "select", "Not required", "Pending", "Ready for Billing Operations", "Sent to Billing Operations", "Finance AR setup pending", "Active", "Returned for correction"),
        Field("billingOperationReference", "Billing / service charge operation reference", "text"),
        Field("occupancyAvailabilityReference", "Occupancy / availability reference", "text"),
        Field("documentIndexReference", "Property records index / DMS reference", "text"),
        Field("tenantClientName", "Tenant / client", "text"),
        Field("responsibleOfficer", "Responsible property officer", "text"),
        Field("receivedDate", "Received date", "date"),
        Field("readinessNotes", "Readiness / exception notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> LeaseFields() =>
    [
        Field("leaseReference", "Lease reference", "text"),
        Field("propertyReceivingReference", "Property Management receiving reference", "text"),
        Field("unitHandoffState", "Unit handoff state", "select", "Active for Leasing", "Reserved", "Occupied", "Under Maintenance", "Blocked", "Released"),
        Field("tenantName", "Tenant / client name", "text"),
        Field("tenantAccountReference", "Tenant / customer account reference", "text"),
        Field("tenantSourceSystem", "Tenant source system", "select", "CRM", "Finance AR Customers", "Business Partner", "Tenant Administration", "Manual pending source link"),
        Field("tenantSourceReference", "Tenant source reference", "text"),
        Field("tenantOccupantReference", "Tenant / occupant workspace reference", "text"),
        Field("propertyUnit", "Property / unit", "text"),
        Field("leaseType", "Lease type", "select", "New lease", "Renewal", "Variation", "Sublease", "Termination", "Consent", "Assignment support"),
        Field("leaseStart", "Lease start", "date"),
        Field("leaseEnd", "Lease end", "date"),
        Field("possessionDate", "Possession / handover date", "date"),
        Field("rentAmount", "Rent amount", "text"),
        Field("serviceChargeAmount", "Service charge amount", "text"),
        Field("depositAmount", "Deposit amount", "text"),
        Field("billingCycle", "Billing cycle", "select", "Monthly", "Quarterly", "Semi-annual", "Annual", "One-off", "Custom"),
        Field("escalationTerms", "Escalation / review terms", "text"),
        Field("noticePeriod", "Notice period", "text"),
        Field("legalProcedureReference", "Legal procedure reference", "text"),
        Field("legalInstrumentStatus", "Legal instrument status", "select", "Not started", "Sent to Legal", "Drafting", "Vetting", "Executed", "Returned for correction", "Terminated", "Superseded"),
        Field("billingOperationReference", "Billing / Service Charge operation reference", "text"),
        Field("financeBillingStatus", "Finance billing status", "select", "Not sent", "Billing package prepared", "Sent to Billing Operations", "Sent to Finance AR", "Customer account linked", "Invoice created", "Receipt recorded", "Returned for correction", "Closed"),
        Field("occupancyAvailabilityReference", "Occupancy / availability reference", "text"),
        Field("handoverReference", "Move-in / move-out / handover reference", "text"),
        Field("documentIndexReference", "Property records index / DMS reference", "text"),
        Field("occupancyImpact", "Occupancy impact", "select", "Reserve unit", "Mark occupied", "Release unit", "Block unit", "No change"),
        Field("renewalNoticeDate", "Renewal notice date", "date"),
        Field("terminationDate", "Termination / expiry date", "date"),
        Field("leaseApprovalStatus", "Lease approval status", "select", "Draft", "Pending manager approval", "Approved", "Returned for correction", "Cancelled"),
        Field("leaseStatus", "Lease status", "select", "Draft", "Approved for Legal", "Legal review", "Executed", "Billing setup", "Active", "Renewal due", "On notice", "Terminated", "Expired", "Superseded", "Closed")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> TenantOccupantFields() =>
    [
        Field("occupantReference", "Property Management occupant reference", "text"),
        Field("sourceSystem", "Source system", "select", "CRM", "Finance AR Customers", "Business Partner", "Tenant Administration", "Manual pending source link"),
        Field("sourceRecordReference", "Source customer / tenant reference", "text"),
        Field("sourceRecordStatus", "Source record status", "select", "Pending validation", "Active", "Incomplete", "Duplicate review", "Restricted", "Disputed", "Closed"),
        Field("occupantName", "Tenant / occupant / client name", "text"),
        Field("occupantType", "Occupant type", "select", "Residential tenant", "Commercial tenant", "Client", "Licensee", "Staff occupant", "Temporary occupant", "Other"),
        Field("eligibilityStatus", "Eligibility / restriction status", "select", "Not checked", "Eligible", "Restricted", "Pending documents", "Disputed", "Management approval required", "Closed"),
        Field("customerAccountReference", "Finance AR customer account reference", "text"),
        Field("customerAccountStatus", "Finance AR customer account status", "select", "Not linked", "Account requested", "Active", "On hold", "Closed", "Returned for correction"),
        Field("propertyUnit", "Property / unit / space", "text"),
        Field("leaseReference", "Lease / occupancy reference", "text"),
        Field("occupancyAvailabilityReference", "Occupancy / availability reference", "text"),
        Field("handoverReference", "Move-in / move-out / handover reference", "text"),
        Field("occupancyStatus", "Occupancy status", "select", "Prospective", "Reserved", "Active", "On notice", "Transferred", "Vacated", "Blocked", "Closed"),
        Field("occupancyEventType", "Latest occupancy event", "select", "None", "Move-in", "Move-out", "Transfer", "Renewal", "Termination", "Access change", "Maintenance block", "Complaint follow-up", "Unit release"),
        Field("moveInDate", "Move-in date", "date"),
        Field("moveOutDate", "Move-out / expected exit date", "date"),
        Field("primaryContact", "Primary contact", "text"),
        Field("emergencyContact", "Emergency contact", "text"),
        Field("communicationPreference", "Communication preference", "select", "Email", "SMS", "Phone", "Letter", "Portal", "Agent / representative", "Other"),
        Field("permittedUsers", "Permitted users / representatives", "textarea"),
        Field("accessStatus", "Access status", "select", "Not issued", "Issued", "Restricted", "Revoked", "Lost / replacement required"),
        Field("billingStatus", "Billing / arrears status", "select", "Not linked", "Active billing", "Current", "In arrears", "Payment promise", "Disputed", "On hold", "Final account", "Closed"),
        Field("depositStatus", "Deposit status", "select", "Not applicable", "Pending", "Collected", "Held", "Refund review", "Deduction review", "Refunded", "Closed"),
        Field("latestStatementReference", "Latest Finance AR statement reference", "text"),
        Field("paymentPromiseDate", "Payment promise date", "date"),
        Field("complaintReference", "Complaint / helpdesk reference", "text"),
        Field("maintenanceReference", "Maintenance / facilities reference", "text"),
        Field("serviceContext", "Service / communication notes", "textarea"),
        Field("closureStatus", "Closure status", "select", "Not closing", "Pending handover", "Pending billing closeout", "Pending access return", "Pending maintenance closeout", "Ready to close", "Closed")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> BillingServiceChargeFields() =>
    [
        Field("billingReference", "Property Management billing reference", "text"),
        Field("sourceWorkspace", "Source workspace", "select", "Lease Management", "Tenant / Occupant Operations", "Property and Unit Register", "Occupancy / Availability Operations", "Move-in / Move-out / Handover Operations", "Service Charge Review", "Manual approved request"),
        Field("billingInstructionType", "Billing instruction type", "select", "New invoice", "Recurring charge", "Receipt allocation", "Credit adjustment", "Debit adjustment", "Deposit collection", "Deposit refund / deduction", "Final account", "Statement query", "Arrears follow-up"),
        Field("propertyUnit", "Property / unit / space", "text"),
        Field("occupantReference", "Tenant / occupant reference", "text"),
        Field("leaseReference", "Lease / occupancy reference", "text"),
        Field("customerAccountReference", "Finance AR customer account reference", "text"),
        Field("arCustomerStatus", "AR customer status", "select", "Not linked", "Account requested", "Active", "On hold", "Closed", "Returned for correction"),
        Field("chargeType", "Charge type", "select", "Rent", "Service charge", "Deposit", "Penalty", "Adjustment", "Refund", "Other"),
        Field("billingPeriod", "Billing period", "text"),
        Field("amount", "Amount", "text"),
        Field("currency", "Currency", "text"),
        Field("taxHandling", "Tax handling", "select", "Not applicable", "Tax inclusive", "Tax exclusive", "Exempt", "Finance AR to determine"),
        Field("dueDate", "Due date", "date"),
        Field("allocationBasis", "Allocation basis", "select", "Fixed amount", "Unit size", "Occupancy", "Manual", "Contract terms", "Not applicable"),
        Field("serviceChargeScheduleReference", "Service charge schedule reference", "text"),
        Field("financeArActionRequested", "Finance AR action requested", "select", "Create customer account", "Create invoice", "Record receipt", "Allocate receipt", "Issue statement", "Update arrears", "Process deposit", "Post adjustment", "Review correction"),
        Field("financeArStatus", "Finance AR status", "select", "Not sent", "Sent to Finance AR", "Customer account linked", "Invoice created", "Receipt recorded", "Allocation completed", "Statement issued", "Balance updated", "GL posted", "Returned for correction", "Closed"),
        Field("arrearsStatus", "Arrears status", "select", "Not applicable", "Current", "In arrears", "Disputed", "Payment plan", "Legal escalation", "Closed"),
        Field("disputeStatus", "Dispute status", "select", "No dispute", "Raised", "Under review", "Resolved", "Escalated"),
        Field("invoiceReference", "Finance AR invoice reference", "text"),
        Field("receiptReference", "Finance AR receipt reference", "text"),
        Field("allocationReference", "Finance AR allocation reference", "text"),
        Field("statementReference", "Finance AR statement reference", "text"),
        Field("glPostingReference", "Finance AR GL posting reference", "text"),
        Field("financeReturnReason", "Finance return / correction reason", "textarea"),
        Field("correctionOwner", "Correction owner", "select", "Property Management", "Finance AR", "Tenant / Occupant", "Lease Officer", "Legal", "Facilities / Maintenance", "Not applicable"),
        Field("paymentPromiseDate", "Payment promise date", "date"),
        Field("nextFollowUpDate", "Next follow-up date", "date"),
        Field("notes", "Billing / service charge notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> OccupancyAvailabilityFields() =>
    [
        Field("occupancyAvailabilityReference", "Occupancy / availability reference", "text"),
        Field("sourceWorkspace", "Source workspace", "select", "Property and Unit Register", "Lease Management", "Tenant / Occupant Operations", "Billing / Service Charge Operations", "Facilities Maintenance", "Complaint Management", "Manual approved request"),
        Field("sourceReference", "Source operation reference", "text"),
        Field("propertyUnit", "Property / unit / space", "text"),
        Field("currentStatus", "Current status", "select", "Available", "Reserved", "Occupied", "Under Maintenance", "Blocked", "Released", "Closed"),
        Field("requestedStatus", "Requested status", "select", "Available", "Reserved", "Occupied", "Under Maintenance", "Blocked", "Released", "Closed"),
        Field("availabilityState", "Availability state", "select", "Selectable for leasing", "Reserved pending lease", "Occupied", "Temporarily unavailable", "Blocked from use", "Released for review", "Closed"),
        Field("leasingVisibility", "Leasing visibility", "select", "Selectable", "Hidden", "Manager approval required", "Reserved only", "Blocked", "Closed"),
        Field("readinessStatus", "Readiness status", "select", "Ready", "Ready with exceptions", "Inspection required", "Maintenance required", "Documents pending", "Billing clearance required", "Not ready"),
        Field("holdReason", "Hold / release reason", "select", "Lease reservation", "Move-in", "Move-out", "Maintenance", "Complaint", "Access issue", "Management hold", "Billing hold", "Other"),
        Field("restrictionType", "Restriction type", "select", "None", "Legal", "Finance", "Maintenance", "Safety", "Access", "Management", "Document pending"),
        Field("restrictionOwner", "Restriction owner", "select", "None", "Property Management", "Lease Management", "Billing / Finance AR", "Facilities", "Maintenance", "Helpdesk", "Legal", "Management"),
        Field("occupantReference", "Tenant / occupant reference", "text"),
        Field("leaseReference", "Lease / reservation reference", "text"),
        Field("handoverReference", "Move-in / move-out / handover reference", "text"),
        Field("maintenanceReference", "Maintenance / facilities reference", "text"),
        Field("complaintReference", "Complaint / helpdesk reference", "text"),
        Field("billingOperationReference", "Billing / service charge operation reference", "text"),
        Field("billingImpact", "Billing impact", "select", "No impact", "Start billing", "Stop billing", "Adjust billing", "Hold billing", "Review arrears", "Final account", "Deposit review"),
        Field("effectiveDate", "Effective date", "date"),
        Field("reviewOrReleaseDate", "Review / release date", "date"),
        Field("documentIndexReference", "Property records index / DMS reference", "text"),
        Field("exceptionStatus", "Exception status", "select", "None", "Status mismatch", "Lease conflict", "Occupant conflict", "Maintenance block", "Billing hold", "Document pending", "Management hold", "Resolved"),
        Field("approvalStatus", "Approval status", "select", "Draft", "Pending review", "Approved", "Returned for correction", "Closed"),
        Field("responsibleOfficer", "Responsible property officer", "text"),
        Field("notes", "Occupancy / availability notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> MoveInMoveOutHandoverFields() =>
    [
        Field("handoverReference", "Handover reference", "text"),
        Field("handoverType", "Handover type", "select", "Move-in", "Move-out", "Transfer", "Unit handover", "Return of keys", "Post-maintenance release", "Other"),
        Field("sourceWorkspace", "Source workspace", "select", "Lease Management", "Tenant / Occupant Operations", "Occupancy / Availability Operations", "Billing / Service Charge Operations", "Facilities Maintenance", "Manual approved request"),
        Field("sourceReference", "Source operation reference", "text"),
        Field("propertyUnit", "Property / unit / space", "text"),
        Field("occupantReference", "Tenant / occupant reference", "text"),
        Field("leaseReference", "Lease / occupancy reference", "text"),
        Field("occupancyAvailabilityReference", "Occupancy / availability reference", "text"),
        Field("scheduledDate", "Scheduled handover date", "date"),
        Field("handoverWindow", "Handover window", "text"),
        Field("actualDate", "Actual handover date", "date"),
        Field("attendanceStatus", "Attendance status", "select", "Not due", "Confirmed", "Tenant no-show", "Officer no-show", "Rescheduled", "Completed", "Disputed"),
        Field("handoverStatus", "Handover status", "select", "Draft", "Scheduled", "Pending readiness", "In inspection", "Pending clearance", "Pending signatures", "Pending linked updates", "Completed", "Returned for correction", "Cancelled"),
        Field("possessionStatus", "Possession status", "select", "Not handed over", "Possession issued", "Possession returned", "Partial handover", "Access restricted", "Disputed"),
        Field("keyAccessStatus", "Keys / access status", "select", "Not issued", "Issued", "Returned", "Partially returned", "Lost / replacement required", "Disabled", "Not applicable"),
        Field("accessItems", "Access items / keys / permits", "textarea"),
        Field("conditionStatus", "Condition status", "select", "Good", "Minor defects", "Major defects", "Requires maintenance", "Disputed", "Pending inspection"),
        Field("meterReading", "Meter / utility reading", "text"),
        Field("photoEvidenceStatus", "Photo evidence status", "select", "Not required", "Pending", "Captured", "Incomplete", "Disputed"),
        Field("defectsFound", "Defects / inventory findings", "textarea"),
        Field("defectChargebackStatus", "Defect chargeback status", "select", "Not applicable", "Pending review", "Charge tenant", "No charge", "Deposit deduction review", "Disputed", "Closed"),
        Field("readinessClearanceStatus", "Readiness / clearance status", "select", "Not checked", "Ready", "Ready with exceptions", "Finance clearance pending", "Maintenance pending", "Document pending", "Access pending", "Not ready"),
        Field("financeClearanceStatus", "Finance clearance status", "select", "Not required", "Pending Finance AR", "Cleared", "Deposit review", "Final account pending", "In arrears", "Disputed"),
        Field("billingOperationReference", "Billing / service charge operation reference", "text"),
        Field("financeArReference", "Finance AR clearance / receipt / final account reference", "text"),
        Field("billingImpact", "Billing impact", "select", "No impact", "Start billing", "Stop billing", "Final account", "Deposit refund / deduction", "Service charge adjustment", "Chargeback", "Hold billing"),
        Field("occupancyUpdate", "Occupancy update", "select", "Mark occupied", "Reserve unit", "Release unit", "Block unit", "Under maintenance", "Close unit", "No change"),
        Field("tenantOccupantUpdate", "Tenant / occupant update", "select", "Move-in", "Move-out", "Transfer", "Access update", "Closure review", "No change"),
        Field("maintenanceReference", "Maintenance work order reference", "text"),
        Field("maintenanceFollowUp", "Maintenance follow-up", "select", "None", "Raise work order", "Existing work order linked", "Inspection required", "Release after maintenance", "Emergency defect", "Chargeable repair review"),
        Field("serviceComplaintReference", "Service / complaint reference", "text"),
        Field("signatureStatus", "Signature status", "select", "Pending", "Partially signed", "Signed", "Disputed", "Waived by approval"),
        Field("documentIndexReference", "Property records index / DMS reference", "text"),
        Field("documentStatus", "Document status", "select", "Pending", "Partially signed", "Signed", "Indexed", "Archived", "Returned for correction"),
        Field("responsibleOfficer", "Responsible property officer", "text"),
        Field("notes", "Handover notes", "textarea")
    ];

    private static IReadOnlyList<FacilitiesWorkspaceField> DocumentRecordIndexFields() =>
    [
        Field("recordIndexReference", "Property records index reference", "text"),
        Field("sourceWorkspace", "Source workspace", "select", "Property and Unit Register", "Lease Management", "Tenant / Occupant Operations", "Billing / Service Charge Operations", "Occupancy / Availability Operations", "Move-in / Move-out / Handover Operations", "Facilities Maintenance", "Complaint Management", "Legal", "Finance AR", "Project Management", "External source"),
        Field("sourceReference", "Source operation / document reference", "text"),
        Field("sourceSystem", "Source system", "select", "Estate / Property Management", "Estate / Facilities", "Maintenance Management", "Complaint Management", "Legal", "Finance AR", "Project Management", "Central DMS", "External party"),
        Field("propertyUnit", "Property / unit / space", "text"),
        Field("occupantReference", "Tenant / occupant reference", "text"),
        Field("leaseReference", "Lease / occupancy reference", "text"),
        Field("billingReference", "Billing / Finance AR reference", "text"),
        Field("occupancyAvailabilityReference", "Occupancy / availability reference", "text"),
        Field("handoverReference", "Move-in / move-out / handover reference", "text"),
        Field("maintenanceReference", "Maintenance / facilities reference", "text"),
        Field("complaintReference", "Complaint / helpdesk reference", "text"),
        Field("legalReference", "Legal reference", "text"),
        Field("projectReference", "Project Management reference", "text"),
        Field("operationReference", "Linked operation reference", "text"),
        Field("documentCategory", "Document category", "select", "Property title / ownership", "Site plan / layout", "Project handoff", "Lease / legal instrument", "Tenant / occupant", "Billing / finance", "Occupancy / availability", "Move-in / move-out / handover", "Maintenance / facilities", "Complaint / service", "Compliance / statutory", "Correspondence", "Approval / memo", "Photograph / inspection evidence", "Other"),
        Field("documentType", "Document type", "text"),
        Field("documentPurpose", "Document purpose", "select", "Evidence", "Approval", "Reference", "Contract / instrument", "Inspection", "Billing support", "Compliance", "Correspondence", "Audit", "Other"),
        Field("metadataTemplate", "Module metadata template", "select", "Property", "Unit / space", "Lease", "Tenant / occupant", "Billing / service charge", "Occupancy / availability", "Handover", "Maintenance / facilities", "Complaint / service", "Legal instrument", "Project handoff", "General property record"),
        Field("metadataCompletenessStatus", "Metadata completeness status", "select", "Pending", "Complete", "Incomplete", "Requires correction", "Template missing", "Exception approved"),
        Field("dmsDocumentReference", "Central DMS document reference", "text"),
        Field("dmsVersionReference", "Central DMS version / revision reference", "text"),
        Field("dmsRepositoryStatus", "Central DMS repository status", "select", "Not linked", "Pending upload", "Linked", "Migration pending", "Version pending", "Permission pending", "Repository exception"),
        Field("versionStatus", "Version status", "select", "Not applicable", "Current", "Draft", "Under review", "Superseded", "Revision pending", "Archived"),
        Field("annotationStatus", "Syncfusion annotation status", "select", "Not applicable", "No annotations", "Annotations pending", "Annotations completed", "Annotations returned", "Annotations locked"),
        Field("commentStatus", "Comment status", "select", "Not applicable", "No comments", "Comments open", "Comments resolved", "Comments escalated", "Comments locked"),
        Field("openReviewOwner", "Open review / comment owner", "text"),
        Field("confidentialityLevel", "Confidentiality level", "select", "Public within module", "Internal", "Restricted", "Confidential", "Legal privileged"),
        Field("accessLevel", "Access level", "select", "Property Management", "Property + Facilities", "Property + Finance", "Property + Legal", "Management only", "Custom restricted"),
        Field("retentionCategory", "Retention category", "select", "Permanent", "Lease term plus retention", "Tenant lifecycle", "Operational period", "Statutory period", "Review required"),
        Field("expiryOrReviewDate", "Expiry / review date", "date"),
        Field("lifecycleStatus", "Lifecycle status", "select", "Draft index", "Pending validation", "Published", "Under review", "Superseded", "Archived", "Restricted", "Rejected", "Transferred"),
        Field("documentStatus", "Document status", "select", "Pending metadata", "Indexed", "DMS link pending", "Active", "Superseded", "Archived", "Restricted", "Rejected"),
        Field("documentOwner", "Document owner", "text"),
        Field("documentCustodian", "Document custodian", "text"),
        Field("notes", "Records index notes", "textarea")
    ];
}
