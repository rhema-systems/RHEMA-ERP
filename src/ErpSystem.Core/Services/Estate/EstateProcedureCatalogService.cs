using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class EstateProcedureCatalogService : IEstateProcedureCatalogService
{
    public IReadOnlyList<EstateProcedureCatalogItem> GetProcedures() =>
    [
        new("Secretarial and Estates Registry", "EstateRegistrySecretariat", "ClipboardList", 0, "slate", "Operational Queue"),
        new("Estate Records Management", "EstateRecordsManagement", "Database", 0, "indigo", "Register"),
        new("Land and Landed Property Inspection", "EstateInspection", "MapPin", 0, "teal", "Event Workflow"),
        new("Search Application", "EstateSearchApplication", "Search", 0, "sky"),
        new("Change of Address and Record Amendment", "EstateRecordAmendment", "FilePenLine", 0, "cyan"),
        new("Certified True Copies", "EstateCertifiedTrueCopy", "FileCheck2", 0, "emerald"),
        new("Joint Ownership / Addition of Name", "EstateJointOwnership", "Users", 0, "violet"),
        new("Transfer / Portion Transfer of Plot", "EstateTransfer", "ArrowRightLeft", 0, "blue"),
        new("Assignment", "EstateAssignment", "FileSignature", 0, "purple"),
        new("Consent to Mortgage / Mortgage in Principle", "EstateMortgageConsent", "ShieldCheck", 0, "zinc"),
        new("Lease Preparation", "EstateLeasePreparation", "FileText", 0, "amber"),
        new("Additional Land Application", "EstateAdditionalLand", "SquarePlus", 0, "teal"),
        new("Revision of Layout", "EstateLayoutRevision", "LayoutPanelTop", 0, "cyan"),
        new("Change of Land Use", "EstateChangeOfUse", "RefreshCw", 0, "sky"),
        new("Reminder and Rate Revision Notices", "EstateReminderRateRevision", "BellRing", 0, "red", "Operational Queue"),
        new("Lease Surrender and Renewal", "EstateLeaseRenewal", "RefreshCw", 0, "lime"),
        new("Serviced Plots and HOS Allocation", "EstateServicedPlotAllocation", "Landmark", 0, "green"),
        new("Lands / Partially Serviced Schedule", "EstateLandsPartiallyServiced", "Home", 0, "orange"),
        new("Housing and Home Ownership Scheme", "EstateHousingHomeOwnership", "Building2", 0, "rose"),
        new("Traditional Lands", "EstateTraditionalLands", "Trees", 0, "emerald"),
        new("Tenancy Regularisation", "EstateTenancyRegularisation", "BadgeCheck", 0, "yellow"),
        new("Estate Reporting and SOP Controls", "EstateReportingControls", "BarChart3", 0, "fuchsia", "Dashboard / Report")
    ];
}
