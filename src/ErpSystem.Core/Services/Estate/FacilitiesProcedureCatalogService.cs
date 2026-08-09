using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class FacilitiesProcedureCatalogService : IFacilitiesProcedureCatalogService
{
    private static readonly FacilitiesProcedureCatalogItem[] Procedures =
    [
        new("Property / Site Operating View", "EstateFacilityPropertySite", "Source: Estate / Facilities -> Property / Site Operations", "Facilities site, building, floor, unit, common-area, occupancy impact, responsible officer, and operating-document view routed through configured workflows.", "Building2", 0, "teal", "Register"),
        new("Lease / Occupancy Coordination", "EstateFacilityLease", "Source: Estate / Facilities -> Property Management / Finance AR", "Facilities lease, occupancy, viewing, billing trigger, access, renewal, termination, and service-impact coordination routed through configured workflows.", "FileCheck", 0, "sky", "Operational Queue"),
        new("Maintenance Intake", "EstateFacilityMaintenance", "Source: Estate / Facilities -> Maintenance Management", "Estate / Facilities maintenance intake routed into the configured workflow and existing Maintenance Management execution.", "Wrench", 0, "amber"),
        new("Complaint Management", "EstateFacilityComplaint", "Source: Estate / Facilities -> Helpdesk Complaint Management", "Estate / Facilities complaint intake routed into the configured workflow and existing Helpdesk complaint lifecycle.", "MessageSquare", 0, "rose"),
        new("Service Provider Management", "EstateFacilityServiceProvider", "Source: Estate / Facilities -> Procurement / Finance AP", "Estate / Facilities provider operating view for approved Procurement suppliers and configured provider workflows.", "Briefcase", 0, "violet"),
        new("Staff & Cleaner Duty Operations", "EstateFacilityStaffCleaner", "Source: Estate / Facilities -> HR / Administration", "Estate / Facilities duty operations for HR-sourced staff and cleaners through configured workflows.", "ClipboardCheck", 0, "emerald"),
        new("Facilities Asset Operating View", "EstateFacilityAssetRegister", "Source: Estate / Facilities -> Finance Fixed Assets / Maintenance", "Estate / Facilities operational asset view linked to source asset systems and configured workflows.", "Database", 0, "indigo"),
        new("Facilities Billing / Service Charge Operations", "EstateFacilityBillingServiceCharge", "Source: Estate / Facilities -> Finance AR", "Estate / Facilities billing instructions routed into configured workflows and existing Finance AR.", "CreditCard", 0, "cyan"),
        new("Facilities Document Index & DMS Readiness", "EstateFacilityDocument", "Source: Estate / Facilities -> Central DMS", "Estate / Facilities document indexing and DMS readiness routed through configured workflows.", "FileText", 0, "lime")
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
