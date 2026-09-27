using ErpSystem.Core.Interfaces.Planning;

namespace ErpSystem.Core.Services.Planning;

public sealed class PlanningProcedureCatalogService : IPlanningProcedureCatalogService
{
    private static readonly PlanningProcedureCatalogItem[] Procedures =
    [
        Procedure(
            "Vetting of Applications for Land Allocations or Temporary License",
            "PlanningLandAllocationVetting",
            "ClipboardCheck",
            "teal"),
        Procedure(
            "Change of Use Review",
            "PlanningChangeOfUseReview",
            "RefreshCw",
            "sky"),
        Procedure(
            "Preparation of Planning Scheme / Layout",
            "PlanningSchemeLayoutPreparation",
            "Map",
            "indigo"),
        Procedure(
            "Site Report",
            "PlanningSiteReport",
            "MapPin",
            "emerald"),
        Procedure(
            "Preparation of Site Plan",
            "PlanningSitePlanPreparation",
            "FileText",
            "blue"),
        Procedure(
            "Official Search and Provision of Data",
            "PlanningOfficialSearchData",
            "Search",
            "cyan"),
        Procedure(
            "Development Permit Conformity Review",
            "PlanningDevelopmentPermitConformity",
            "FileCheck2",
            "violet"),
        Procedure(
            "Undertake Regularization",
            "PlanningRegularization",
            "BadgeCheck",
            "amber"),
        Procedure(
            "Layout Review and Correction",
            "PlanningLayoutReviewCorrection",
            "FilePenLine",
            "orange"),
        Procedure(
            "Compliance Site Inspection and Reporting",
            "PlanningComplianceInspection",
            "ClipboardList",
            "rose"),
        Procedure(
            "Dispute Resolution and Client Complaint Management",
            "PlanningDisputeComplaint",
            "MessageSquare",
            "purple"),
        Procedure(
            "District Assembly Spatial Planning Committee Meetings",
            "PlanningAssemblySpatialCommittee",
            "Users",
            "slate")
    ];

    public IReadOnlyList<PlanningProcedureCatalogItem> GetProcedures() => Procedures;

    public PlanningProcedureWorkspace? GetProcedureWorkspace(string entityType)
    {
        var procedure = Procedures.FirstOrDefault(item =>
            string.Equals(item.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        return procedure is null
            ? null
            : new PlanningProcedureWorkspace(procedure, [], [], [], [], []);
    }

    private static PlanningProcedureCatalogItem Procedure(
        string title,
        string entityType,
        string icon,
        string accent) =>
        new(title, entityType, icon, 0, accent);
}
