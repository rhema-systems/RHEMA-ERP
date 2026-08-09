namespace ErpSystem.Shared;

public sealed record PropertyManagementPermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);

public static class PropertyManagementRoles
{
    public const string Officer = "Property Management Officer";
    public const string Supervisor = "Property Management Supervisor";
    public const string Manager = "Property Manager";
}

public static class PropertyManagementPermissions
{
    public const string Category = "Estate - Property Management";

    public const string Access = "property-management.access";
    public const string ViewDashboard = "property-management.dashboard.read";
    public const string ViewCases = "property-management.case.read";
    public const string CreateCases = "property-management.case.create";
    public const string UpdateCases = "property-management.case.update";
    public const string ApproveCases = "property-management.case.approve";
    public const string CreateHandoffs = "property-management.handoff.create";
    public const string ManageUnits = "property-management.units.manage";
    public const string ManageLeases = "property-management.leases.manage";
    public const string ManageOccupancy = "property-management.occupancy.manage";
    public const string ManageHandover = "property-management.handover.manage";
    public const string ManageDocuments = "property-management.documents.manage";
    public const string ViewBilling = "property-management.billing.view";
    public const string UsePortal = "property-management.portal.access";

    public static readonly PropertyManagementPermissionDefinition[] All =
    [
        new(Access, "Access Property Management", "Access Estate / Property Management workspaces and navigation.", Category),
        new(ViewDashboard, "View Property Dashboard", "View Estate / Property Management operating dashboard and handoff status.", Category),
        new(ViewCases, "View Property Cases", "View Property Management procedure cases, stages, documents, and activity.", Category),
        new(CreateCases, "Create Property Cases", "Open new Property Management procedure cases and intake records.", Category),
        new(UpdateCases, "Update Property Cases", "Update Property Management intake fields, checklists, documents, and notes.", Category),
        new(ApproveCases, "Approve Property Cases", "Approve Property Management stages, exceptions, publishing, and close-out decisions.", Category),
        new(CreateHandoffs, "Create Property Handoffs", "Create downstream Project, Legal, Finance AR, Facilities, Maintenance, Helpdesk, Reports, or document handoffs from Property Management.", Category),
        new(ManageUnits, "Manage Property And Units", "Manage received property, site, unit, space, availability, readiness, and Project Management handoff context.", Category),
        new(ManageLeases, "Manage Property Leases", "Manage lease operations, renewal tracking, termination context, and Legal / Finance AR lease handoff readiness.", Category),
        new(ManageOccupancy, "Manage Occupancy And Availability", "Manage tenant / occupant links, occupancy status, availability state, restrictions, and unit release decisions.", Category),
        new(ManageHandover, "Manage Handover Operations", "Manage move-in, move-out, transfer, keys, access, inspection, clearance, and possession handover records.", Category),
        new(ManageDocuments, "Manage Property Records Index", "Index Property Management documents, classify module metadata, track access and retention, and reference Central DMS records.", Category),
        new(ViewBilling, "View Property Billing Context", "View Property Management billing context, service charge instructions, arrears follow-up, and Finance AR handoff readiness without replacing Finance permissions.", Category),
        new(UsePortal, "Use Property Portal", "Access tenant/client Property Management self-service views for occupancy, handover, documents, invoice status, uploads, and feedback.", Category)
    ];

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();

    public static readonly string[] OfficerNames =
    [
        Access,
        ViewCases,
        CreateCases,
        UpdateCases,
        CreateHandoffs,
        ManageUnits,
        ManageOccupancy,
        ManageHandover
    ];

    public static readonly string[] SupervisorNames =
    [
        Access,
        ViewDashboard,
        ViewCases,
        CreateCases,
        UpdateCases,
        CreateHandoffs,
        ManageUnits,
        ManageOccupancy,
        ManageHandover,
        ViewBilling
    ];

    public static readonly string[] ManagerNames =
    [
        Access,
        ViewDashboard,
        ViewCases,
        CreateCases,
        UpdateCases,
        ApproveCases,
        CreateHandoffs,
        ManageUnits,
        ManageLeases,
        ManageOccupancy,
        ManageHandover,
        ManageDocuments,
        ViewBilling
    ];
}
