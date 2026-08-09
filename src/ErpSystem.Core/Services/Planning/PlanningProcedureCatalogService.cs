using ErpSystem.Core.Interfaces.Planning;

namespace ErpSystem.Core.Services.Planning;

public sealed class PlanningProcedureCatalogService : IPlanningProcedureCatalogService
{
    private const string SourceName = "Source: Project Management / Planning Section SOP";

    private static readonly PlanningProcedureCatalogItem[] Procedures =
    [
        Procedure(
            "Vetting of Applications for Land Allocations or Temporary License",
            "PlanningLandAllocationVetting",
            "Land allocation or temporary license applications are vetted against approved master plans, layout controls, and TDC land use strategy through configured workflows.",
            "ClipboardCheck",
            "teal"),
        Procedure(
            "Change of Use Review",
            "PlanningChangeOfUseReview",
            "Change of use requests are reviewed for spatial strategy alignment, adjoining-use impact, consultation needs, and committee recommendation through configured workflows.",
            "RefreshCw",
            "sky"),
        Procedure(
            "Preparation of Planning Scheme / Layout",
            "PlanningSchemeLayoutPreparation",
            "Planning schemes and layouts for acquisitions, projects, and development areas are prepared, reviewed, versioned, and approved through configured workflows.",
            "Map",
            "indigo"),
        Procedure(
            "Site Report",
            "PlanningSiteReport",
            "Planning site inspections, observations, photos, and situational reports are assigned, reviewed, and submitted through configured workflows.",
            "MapPin",
            "emerald"),
        Procedure(
            "Preparation of Site Plan",
            "PlanningSitePlanPreparation",
            "Site plan preparation for lessees, transferees, allocations, and related files is vetted, drafted, corrected, and signed through configured workflows.",
            "FileText",
            "blue"),
        Procedure(
            "Official Search and Provision of Data",
            "PlanningOfficialSearchData",
            "Planning records searches, site plan superimposition, land use data responses, and Estate or Revenue cross-checks are controlled through configured workflows.",
            "Search",
            "cyan"),
        Procedure(
            "Development Permit Conformity Review",
            "PlanningDevelopmentPermitConformity",
            "Development permit proposals are checked against layouts, land use controls, reservations, and planning conditions through configured workflows.",
            "FileCheck2",
            "violet"),
        Procedure(
            "Undertake Regularization",
            "PlanningRegularization",
            "Regularization planning checks, site verification, committee or MD decision routing, and approved site plan preparation are handled through configured workflows.",
            "BadgeCheck",
            "amber"),
        Procedure(
            "Layout Review and Correction",
            "PlanningLayoutReviewCorrection",
            "Layout anomalies, corrections, drawing updates, Estate file requests, and HOD submissions are tracked through configured workflows.",
            "FilePenLine",
            "orange"),
        Procedure(
            "Compliance Site Inspection and Reporting",
            "PlanningComplianceInspection",
            "Compliance inspection requests, field verification, breach observations, recommendations, and HOD routing are handled through configured workflows.",
            "ClipboardList",
            "rose"),
        Procedure(
            "Dispute Resolution and Client Complaint Management",
            "PlanningDisputeComplaint",
            "Planning complaints, boundary disputes, record checks, HOD coordination, and response closure are handled through configured workflows.",
            "MessageSquare",
            "purple"),
        Procedure(
            "District Assembly Spatial Planning Committee Meetings",
            "PlanningAssemblySpatialCommittee",
            "MMDA spatial planning committee invitations, representation, meeting reports, action items, and HOD submissions are tracked through configured workflows.",
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
        string summary,
        string icon,
        string accent) =>
        new(title, entityType, SourceName, summary, icon, 0, accent);
}
