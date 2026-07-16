using ErpSystem.Core.Interfaces.Estate;

namespace ErpSystem.Core.Services.Estate;

public sealed class EstateProcedureCatalogService : IEstateProcedureCatalogService
{
    public IReadOnlyList<EstateProcedureCatalogItem> GetProcedures() =>
    [
        new("Secretarial and Estates Registry", "EstateRegistrySecretariat", "Source: Estate - Estates Operational Manual", "Incoming files, letters, forms purchase, file dispatch, movement tracing, typing, client updates, and departmental registry controls.", "ClipboardList", 7, "slate"),
        new("Estate Records Management", "EstateRecordsManagement", "Source: Estate - Estates Operational Manual", "Estate registers, HOS ledger cards, record updates, transfer amendments, agency notifications, and building permit ownership verification.", "Database", 8, "indigo"),
        new("Land and Landed Property Inspection", "EstateInspection", "Source: Estate - Estates Operational Manual", "Site inspection, site report preparation, neighbourhood details, current development capture, and photographic evidence.", "MapPin", 5, "teal"),
        new("Search Application", "EstateSearchApplication", "Source: Estate - Estates Operational Manual", "Search request intake, ground-rent arrears control, property-file review, and search report preparation.", "Search", 5, "sky"),
        new("Change of Address and Record Amendment", "EstateRecordAmendment", "Source: Estate - Estates Operational Manual", "Address updates, statutory declaration support, arrears checks, and Revenue and Estate Records amendment routing.", "FilePenLine", 5, "cyan"),
        new("Certified True Copies", "EstateCertifiedTrueCopy", "Source: Estate - Estates Operational Manual", "Certified copy request, arrears verification, fee/payment confirmation, document preparation, and certification routing.", "FileCheck2", 5, "emerald"),
        new("Joint Ownership / Addition of Name", "EstateJointOwnership", "Source: Estate - Estates Operational Manual", "Additional-name requests, lease checks, cadastral plan routing, deed of variation, registration, detachment, and records amendment.", "Users", 7, "violet"),
        new("Transfer / Portion Transfer of Plot", "EstateTransfer", "Source: Estate - Estates Operational Manual", "Transfer request processing, fee calculation, HOE and MD approval, Legal routing, completion, and records amendment.", "ArrowRightLeft", 7, "blue"),
        new("Assignment", "EstateAssignment", "Source: Estate - Estates Operational Manual", "Consent to assign, draft deed review, arrears and development checks, Legal routing, registration, detachment, and completion notices.", "FileSignature", 7, "purple"),
        new("Lease Preparation", "EstateLeasePreparation", "Source: Estate - Estates Operational Manual", "Lease request processing, substantial development checks, cadastral requirements, invoice/payment routing, Legal preparation, and records update.", "FileText", 8, "amber"),
        new("Lease Surrender and Renewal", "EstateLeaseRenewal", "Source: Estate - Estates Operational Manual", "Renewal requirements, surrender option processing, term threshold checks, committee review, invoicing, approval, and lease renewal close-out.", "RefreshCw", 8, "lime"),
        new("Serviced Plots and HOS Allocation", "EstateServicedPlotAllocation", "Source: Estate - Estates Operational Manual", "HOS unit allocation, serviced plot allocation, payment book updates, Offer Letters, Right of Entry letters, and quarterly reporting.", "Landmark", 8, "green"),
        new("Lands / Partially Serviced Schedule", "EstateLandsPartiallyServiced", "Source: Estate - Estates Operational Manual", "Application intake, proposal letters, Land Management Fee and ground rent determination, Offer and Right of Entry preparation, and reports.", "Home", 7, "orange"),
        new("Housing and Home Ownership Scheme", "EstateHousingHomeOwnership", "Source: Estate - Estates Questionnaire Response", "Recognition of tenancy, rental-to-HOS conversion, purchase completion, Offer Letter preparation, lease request, and records update.", "Building2", 7, "rose"),
        new("Traditional Lands", "EstateTraditionalLands", "Source: Estate - Estates Operational Manual", "Traditional lands proposal processing, fee determination, allocation review, Offer and Right of Entry preparation, and quarterly reporting.", "Trees", 6, "emerald"),
        new("Tenancy Regularisation", "EstateTenancyRegularisation", "Source: Estate - Estates Operational Manual", "Regularisation communities, tenancy validation, documentation, fee/payment checks, approvals, and records amendment.", "BadgeCheck", 7, "yellow"),
        new("Reporting and Controls", "EstateReportingControls", "Source: Estate - Estates Questionnaire Response", "Quarterly productivity reports, rent roll, debtor lists, allocation reports, approval thresholds, segregation controls, audit trail, and policy enforcement.", "BarChart3", 6, "fuchsia")
    ];
}
