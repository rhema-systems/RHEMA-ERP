using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class FacilitiesProcedureCatalogService : IFacilitiesProcedureCatalogService
{
    private static readonly FacilitiesWorkspaceStage[] MaintenanceStages =
    [
        new("Facilities Intake", "Facilities Officer",
            [
                "Requester, contact, property/unit, and issue are confirmed",
                "Service impact, target date, and access notes are recorded",
                "Maintenance job card need is assessed"
            ]),
        new("Maintenance Handoff Review", "Facilities Supervisor",
            [
                "Maintenance type, priority, job description, hours, and cost are confirmed",
                "Safety, access, and SLA context are confirmed",
                "Requester update has been issued"
            ]),
        new("Maintenance Closeout", "Facilities Manager",
            [
                "Job card or work order reference is recorded where required",
                "Inspection, requester feedback, and completion outcome are reviewed",
                "Facilities case is ready for closeout"
            ])
    ];

    private static readonly FacilitiesWorkspaceStage[] ComplaintStages =
    [
        new("Facilities Complaint Intake", "Facilities Officer",
            [
                "Complainant, contact, property/unit, category, and priority are confirmed",
                "Service impact, incident date, target date, and complaint details are recorded",
                "Complaint routing path is assessed"
            ]),
        new("Complaint Resolution Review", "Facilities Supervisor",
            [
                "Resolution route and responsible team are recorded",
                "Linked helpdesk, maintenance, provider, or estate action is confirmed",
                "Requester update has been issued"
            ]),
        new("Complaint Closeout", "Facilities Manager",
            [
                "Resolution outcome and feedback status are recorded",
                "Any linked ticket or handoff reference is captured",
                "Facilities complaint case is ready for closeout"
            ])
    ];

    private static readonly FacilitiesWorkspaceField[] MaintenanceFields =
    [
        new("referenceNumber", "Reference number", "text"),
        new("applicantName", "Requester name", "text"),
        new("contactReference", "Requester contact", "text"),
        new("propertyUnit", "Property / unit / plot", "text"),
        new("estateManagedAssetId", "Estate property ID", "text"),
        new("location", "Location", "text"),
        new("issueType", "Issue type", "text"),
        new("priority", "Facilities priority", "select", ["Low", "Medium", "High"]),
        new("serviceImpact", "Service impact", "select", ["Low", "Medium", "High", "Critical"]),
        new("targetDate", "Target date", "date"),
        new("preferredVisitDate", "Preferred visit date", "date"),
        new("accessInstructions", "Access instructions", "textarea"),
        new("issueDescription", "Issue description", "textarea"),
        new("maintenanceTypeId", "Maintenance type", "text"),
        new("handoffDescription", "Job card description", "textarea"),
        new("estimatedHours", "Estimated hours", "number"),
        new("estimatedCost", "Estimated cost", "currency"),
        new("serviceProviderBusinessPartnerId", "Approved service provider", "text"),
        new("serviceProviderContractId", "Active service contract", "text"),
        new("maintenanceJobCardReference", "Maintenance job card reference", "text"),
        new("maintenanceWorkOrderReference", "Maintenance work order reference", "text"),
        new("inspectionOutcome", "Inspection outcome", "select", ["Pending", "Passed", "Failed", "Not required"]),
        new("inspectionReference", "Inspection reference", "text"),
        new("requesterFeedbackStatus", "Requester feedback status", "select", ["Pending", "Satisfied", "Not satisfied", "Not required"]),
        new("closureNotes", "Closeout notes", "textarea")
    ];

    private static readonly FacilitiesWorkspaceField[] ComplaintFields =
    [
        new("referenceNumber", "Reference number", "text"),
        new("applicantName", "Complainant name", "text"),
        new("contactReference", "Complainant contact", "text"),
        new("propertyUnit", "Property / unit / plot", "text"),
        new("location", "Location", "text"),
        new("complaintCategory", "Complaint category", "text"),
        new("priority", "Priority", "select", ["Low", "Normal", "High", "Urgent"]),
        new("serviceImpact", "Service impact", "select", ["Low", "Medium", "High", "Critical"]),
        new("incidentDate", "Incident date", "date"),
        new("targetDate", "Target date", "date"),
        new("complaintDescription", "Complaint description", "textarea"),
        new("desiredResolution", "Desired resolution", "textarea"),
        new("helpdeskTicketReference", "Helpdesk ticket reference", "text"),
        new("requesterFeedbackStatus", "Requester feedback status", "select", ["Pending", "Satisfied", "Not satisfied", "Not required"]),
        new("closureNotes", "Closeout notes", "textarea")
    ];

    private static readonly FacilitiesWorkspaceHandoff[] MaintenanceHandoffs =
    [
        new("Facilities Officer", "Facilities Supervisor", "Maintenance request intake is validated and ready for routing."),
        new("Facilities Supervisor", "Maintenance Management", "Request requires a job card or work order in Maintenance Management."),
        new("Facilities Manager", "Requester / Estate Services", "Maintenance case is closed and requester outcome is available.")
    ];

    private static readonly FacilitiesWorkspaceHandoff[] ComplaintHandoffs =
    [
        new("Facilities Officer", "Facilities Supervisor", "Complaint intake is validated and ready for resolution review."),
        new("Facilities Supervisor", "Helpdesk / Maintenance / Service Provider", "Complaint requires downstream operational action."),
        new("Facilities Manager", "Requester / Estate Services", "Complaint case is closed and requester outcome is available.")
    ];

    private static readonly FacilitiesProcedureCatalogItem[] Procedures =
    [
        new("Property / Site Operating View", "EstateFacilityPropertySite", "Building2", 0, "teal", "Register"),
        new("Lease / Occupancy Coordination", "EstateFacilityLease", "FileCheck", 0, "sky", "Operational Queue"),
        new("Maintenance Intake", "EstateFacilityMaintenance", "Wrench", 3, "amber"),
        new("Complaint Management", "EstateFacilityComplaint", "MessageSquare", 3, "rose"),
        new("Approved Provider Assignments", "EstateFacilityServiceProvider", "Briefcase", 0, "violet"),
        new("Staff & Cleaner Duty Operations", "EstateFacilityStaffCleaner", "ClipboardCheck", 0, "emerald"),
        new("Facilities Asset Operating View", "EstateFacilityAssetRegister", "Database", 0, "indigo"),
        new("Facilities Billing / Service Charge Operations", "EstateFacilityBillingServiceCharge", "CreditCard", 0, "cyan"),
        new("Facilities Document Index & DMS Readiness", "EstateFacilityDocument", "FileText", 0, "lime")
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

        IReadOnlyList<FacilitiesWorkspaceField> fields = entityType switch
        {
            "EstateFacilityMaintenance" => MaintenanceFields,
            "EstateFacilityComplaint" => ComplaintFields,
            _ => Array.Empty<FacilitiesWorkspaceField>()
        };

        IReadOnlyList<FacilitiesWorkspaceStage> stages = entityType switch
        {
            "EstateFacilityMaintenance" => MaintenanceStages,
            "EstateFacilityComplaint" => ComplaintStages,
            _ => Array.Empty<FacilitiesWorkspaceStage>()
        };

        IReadOnlyList<string> outputs = entityType switch
        {
            "EstateFacilityMaintenance" =>
            [
                "Validated maintenance intake",
                "Maintenance job card or work order reference",
                "Requester update",
                "Facilities closeout record"
            ],
            "EstateFacilityComplaint" =>
            [
                "Validated complaint intake",
                "Resolution review note",
                "Requester update",
                "Facilities complaint closeout record"
            ],
            _ => Array.Empty<string>()
        };

        IReadOnlyList<FacilitiesWorkspaceHandoff> handoffs = entityType switch
        {
            "EstateFacilityMaintenance" => MaintenanceHandoffs,
            "EstateFacilityComplaint" => ComplaintHandoffs,
            _ => Array.Empty<FacilitiesWorkspaceHandoff>()
        };

        return new FacilitiesProcedureWorkspace(procedure, stages, [], fields, outputs, handoffs);
    }
}
