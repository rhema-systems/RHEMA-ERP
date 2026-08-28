using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class PropertyManagementProcedureCatalogService : IPropertyManagementProcedureCatalogService
{
    private static readonly FacilitiesProcedureCatalogItem[] Procedures =
    [
        new("Property and Unit Register", "EstatePropertyManagementPropertyUnit", "Source: Estate / Property Management - Property Management ERP Module", "Property, site, unit, space, common-area, and service-area receiving/register operations routed through configured workflows.", "Building2", 0, "teal", "Register"),
        new("Lease Management", "EstatePropertyManagementLease", "Source: Estate / Property Management - Lease Management", "Lease setup, variation, renewal, termination, agreement, signature, and billing handoff operations routed through configured workflows.", "FileCheck", 0, "amber", "Case Workflow"),
        new("Tenant / Occupant Operations", "EstatePropertyManagementTenantOccupant", "Source: Estate / Property Management - Property Management ERP BRS", "Tenant and occupant operations using existing CRM, Finance AR customer, business partner, or tenant administration sources routed through configured workflows.", "Users", 0, "cyan", "Register"),
        new("Billing / Service Charge Operations", "EstatePropertyManagementBillingServiceCharge", "Source: Estate / Property Management -> Finance AR", "Billing readiness and service-charge instruction operations routed through configured workflows and existing Finance AR.", "CreditCard", 0, "purple", "Operational Queue"),
        new("Ground Rent Administration", "EstatePropertyManagementGroundRent", "Source: Estate / Property Management - Ground Rent", "Land-only ground-rent account setup, assessment, review, billing readiness, and finance handoff routed through configured workflows.", "Banknote", 0, "emerald", "Operational Queue"),
        new("Listing / Application Operations", "EstatePropertyManagementListingApplication", "Source: External Portal -> Estate / Property Management", "External portal rent/sale listing request intake, review, reservation, and decision operations routed through configured workflows.", "ClipboardList", 0, "blue", "Case Workflow"),
        new("Occupancy / Availability Operations", "EstatePropertyManagementOccupancyAvailability", "Source: Estate / Property Management - Occupancy Operations", "Availability, reservation, occupancy, move-in, move-out, sale, block, and portal visibility operations routed through configured workflows.", "Home", 0, "rose", "Operational Queue"),
        new("Move-in / Move-out / Handover Operations", "EstatePropertyManagementMoveInMoveOutHandover", "Source: Estate / Property Management - Handover", "Move-in, move-out, key/access, condition, snag, handover, handback, and linked billing/records operations routed through configured workflows.", "ClipboardCheck", 0, "orange", "Operational Queue"),
        new("Property Documents / Records Index", "EstatePropertyManagementDocumentRecordIndex", "Source: Estate / Property Management -> Central DMS", "Property Management document index, module metadata, access, retention, lifecycle, DMS reference, version, annotation, and comment readiness routed through configured workflows.", "FileText", 0, "lime", "Register")
    ];

    public IReadOnlyList<FacilitiesProcedureCatalogItem> GetProcedures() => Procedures;

    public FacilitiesProcedureWorkspace? GetProcedureWorkspace(string entityType)
    {
        var procedure = Procedures.FirstOrDefault(item =>
            string.Equals(item.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        return procedure is null
            ? null
            : new FacilitiesProcedureWorkspace(procedure, [], [], [], [], []);
    }
}
