using ErpSystem.Core.Interfaces.Planning;

namespace ErpSystem.Core.Services.Planning;

public sealed class PlanningProcedureCatalogService : IPlanningProcedureCatalogService
{
    private const string SourceName = "Source: Project Management / Planning Section SOP";

    private static readonly PlanningProcedureDefinition[] Procedures =
    {
        Procedure(
            "Vetting of Applications for Land Allocations or Temporary License",
            "PlanningLandAllocationVetting",
            "Ensure land allocation or license applications align with approved master plans and TDC land use strategy.",
            "ClipboardCheck",
            "teal",
            new[]
            {
                Stage("Receive referred file", "Head of Development / Supervising Town Planner", "Receive the application or file from HOD with supporting documents.", "Record applicant, land reference, request type, and referral date.", "Confirm supporting documents are attached.", "Acknowledge receipt by the Planning Section."),
                Stage("Assign technical review", "Supervising Town Planner", "Assign the application to a Town Planner, Physical Planner, or technical officer.", "Confirm the technical reviewer.", "Set review scope and expected response date.", "Provide master plan and layout references."),
                Stage("Check land use alignment", "Planning Technical Officer", "Cross-reference the proposed use with the approved master plan, layout, and land use strategy.", "Review zoning and planning scheme references.", "Identify conflicts, buffers, easements, and access constraints.", "Record technical observations."),
                Stage("Conduct site verification", "Planning Technical Officer", "Visit the site where required and prepare the situational report.", "Verify ground conditions.", "Capture photos and access notes.", "Document surrounding land uses."),
                Stage("Recommend or reject", "Supervising Town Planner", "Send the vetted recommendation or rejection with justification to HOD.", "Summarize findings.", "Attach site report where applicable.", "Route final planning advice to HOD.")
            },
            new[] { "Vetted planning recommendation", "Rejection justification where applicable", "Site situational report", "Master plan compatibility note" }),
        Procedure(
            "Change of Use Review",
            "PlanningChangeOfUseReview",
            "Confirm proposed change of use supports spatial development strategy and avoids land use conflicts.",
            "RefreshCw",
            "sky",
            new[]
            {
                Stage("Receive change request", "Head of Development / Supervising Town Planner", "Receive the application from HOD with supporting documents.", "Capture existing and proposed use.", "Confirm plot reference and applicant details.", "Validate attachments."),
                Stage("Refer to committee", "Head of Development", "Route the application to the change of use committee.", "Register committee review requirement.", "Share relevant layouts and land use records.", "Set meeting or review timeline."),
                Stage("Committee vetting", "Change of Use Committee", "Determine whether the proposed land use is permissible or otherwise.", "Assess land use compatibility.", "Review impact on adjoining uses.", "Record committee observations."),
                Stage("Site and stakeholder review", "Planning Technical Officer", "Visit the site and conduct stakeholder or neighborhood consultation where needed.", "Prepare site report.", "Record consultation requirement.", "Attach consultation feedback if conducted."),
                Stage("Finalize planning advice", "Supervising Town Planner", "Submit recommended or rejected change of use advice to HOD as committee chair.", "Compile technical and committee findings.", "State approval conditions or rejection reasons.", "Forward signed advice to HOD.")
            },
            new[] { "Change of use recommendation", "Committee review note", "Site report", "Stakeholder consultation record" }),
        Procedure(
            "Preparation of Planning Scheme / Layout",
            "PlanningSchemeLayoutPreparation",
            "Prepare planning schemes and layouts for new company acquisitions.",
            "Map",
            "indigo",
            new[]
            {
                Stage("Receive base map or site", "Head of Development / Supervising Town Planner", "Receive the base map or parcel details from HOD.", "Confirm acquisition reference.", "Validate survey or base map information.", "Open layout preparation record."),
                Stage("Initiate internal design", "Supervising Town Planner", "Coordinate Town Planners, Physical Planners, Drawing Office Supervisor, and technical officers.", "Assign layout roles.", "Confirm planning standards.", "Set internal design timeline."),
                Stage("Prepare layout and reports", "Planning Technical Team", "Produce the planning layout and supporting reports.", "Design roads, parcels, buffers, and utility corridors.", "Prepare planning justification report.", "Check consistency with company strategy."),
                Stage("Internal quality review", "Supervising Town Planner / Drawing Office Supervisor", "Review the layout package before submission.", "Check plot dimensions.", "Validate access, easements, and road reservations.", "Mark corrections or approve draft."),
                Stage("Submit for approval", "Supervising Town Planner", "Forward final layouts and reports to HOD for approval and further action.", "Attach final drawings.", "Attach planning reports.", "Record submission date.")
            },
            new[] { "Planning scheme layout", "Planning report", "Reviewed base map package", "HOD approval submission" }),
        Procedure(
            "Site Report",
            "PlanningSiteReport",
            "Visit site and report on the ground situation for a requested issue, file, or application.",
            "MapPin",
            "emerald",
            new[]
            {
                Stage("Receive request", "Head of Development / Supervising Town Planner", "Receive the application, file, or letter from HOD.", "Capture request purpose.", "Confirm site location.", "Validate supporting documents."),
                Stage("Assign inspection", "Supervising Town Planner", "Visit the site directly or assign a Planning technical officer.", "Assign officer.", "Set inspection scope.", "Provide file and location references."),
                Stage("Conduct site visit", "Assigned Planning Officer", "Inspect the site and collect observations.", "Verify access and boundaries.", "Capture surrounding development.", "Take photos or field notes."),
                Stage("Prepare report", "Assigned Planning Officer", "Produce a site report for STP review.", "Summarize ground situation.", "Identify planning issues.", "Attach evidence."),
                Stage("Submit report", "Supervising Town Planner", "Review and forward the site report to HOD.", "Check report completeness.", "Add STP comments.", "Route to HOD for further action.")
            },
            new[] { "Site report", "Inspection photos or notes", "Planning observations", "HOD routing note" }),
        Procedure(
            "Preparation of Site Plan",
            "PlanningSitePlanPreparation",
            "Prepare site plans for lessees or transferees and verify spatial and planning standards.",
            "FileText",
            "blue",
            new[]
            {
                Stage("Receive file", "Head of Development / Supervising Town Planner", "Receive the file from HOD with supporting documents.", "Capture lessee or transferee details.", "Confirm plot reference.", "Check required attachments."),
                Stage("Planning vetting", "Supervising Town Planner", "Vet the request or assign a technical officer for review.", "Check planning status.", "Confirm layout and land use references.", "Resolve missing information."),
                Stage("Refer to Drawing Office", "Supervising Town Planner", "Refer the file to the Drawing Office Supervisor for site plan preparation.", "Record referral date.", "Attach vetting notes.", "Set preparation timeline."),
                Stage("Draft and validate site plan", "Drawing Office Supervisor / Draughtsman", "Prepare and validate the site plan.", "Review plot dimensions.", "Check road access, utility corridors, buffers, and easements.", "Validate survey coordinates."),
                Stage("Approve and sign", "Draughtsman / Drawing Office Supervisor / Supervising Town Planner", "Approve, amend, and sign the final site plan.", "Resolve corrections.", "Capture signatures.", "Return completed file to HOD.")
            },
            new[] { "Signed site plan", "Coordinate validation note", "Planning vetting note", "Returned file to HOD" }),
        Procedure(
            "Official Search and Provision of Data",
            "PlanningOfficialSearchData",
            "Provide verified land use information based on available Planning Section records.",
            "Search",
            "cyan",
            new[]
            {
                Stage("Receive search request", "Supervising Town Planner", "Receive the referred application with consent for search and supporting documents.", "Confirm consent for search.", "Capture applicant and site plan reference.", "Log received date."),
                Stage("Search planning records", "Planning Technical Officer", "Search prepared site plans, notebooks, layouts, and Planning records.", "Check site plan records.", "Search notebooks and prepared plans.", "Record available or missing references."),
                Stage("Superimpose and verify", "Planning Technical Officer", "Manually superimpose the attached site plan on existing layouts or prepared site plans.", "Check overlap and boundary consistency.", "Confirm zoning status.", "Identify planning constraints."),
                Stage("Coordinate cross-checks", "Supervising Town Planner", "Recommend Estate and Revenue records checks where required.", "List records requiring cross-check.", "Route recommendation to Estate and Revenue.", "Capture cross-check response if available."),
                Stage("Provide planning information", "Supervising Town Planner", "Forward land status, zoning, and available planning information to HOD.", "Attach site plan.", "Summarize Planning Section findings.", "Return application to HOD.")
            },
            new[] { "Official planning search response", "Zoning status note", "Records availability note", "Cross-check recommendation" }),
        Procedure(
            "Development Permit Conformity Review",
            "PlanningDevelopmentPermitConformity",
            "Assess whether a development proposal conforms with the approved layout and planning controls.",
            "FileCheck2",
            "violet",
            new[]
            {
                Stage("Receive proposal", "Supervising Town Planner", "Receive the development permit proposal for conformity review.", "Capture proposal type.", "Confirm site plan and drawings.", "Open conformity review record."),
                Stage("Check layout compatibility", "Planning Technical Officer", "Compare the site plan and proposal with the approved layout.", "Check land use compatibility.", "Review plot boundaries.", "Confirm access and reservation compliance."),
                Stage("Assess planning controls", "Planning Technical Officer", "Review height zoning, use restrictions, and relevant planning controls.", "Check height zoning.", "Check density or intensity constraints.", "Record inconsistencies."),
                Stage("Request revisions where needed", "Supervising Town Planner", "Advise revision where inconsistencies are found.", "State required correction.", "Route revision request.", "Track revised submission if applicable."),
                Stage("Endorse or return file", "Supervising Town Planner", "Approve and endorse conforming proposals for further processing.", "Attach conformity comments.", "Endorse if compliant.", "Refer file for next action.")
            },
            new[] { "Development permit conformity endorsement", "Revision advice", "Planning controls checklist", "Layout compatibility note" }),
        Procedure(
            "Undertake Regularization",
            "PlanningRegularization",
            "Guide regularization of unplanned or informally occupied TDC lands.",
            "BadgeCheck",
            "amber",
            new[]
            {
                Stage("Receive regularization file", "Supervising Town Planner", "Receive the numbered parcel, plot file, or regularization request.", "Capture plot reference.", "Identify whether request came from HOD or Regularization Office.", "Confirm attachments."),
                Stage("Check existing records", "Planning Technical Officer", "Verify whether Planning has existing records on the parcel or plot.", "Search layouts and notebooks.", "Compare site plan against existing layout.", "Record available information."),
                Stage("Verify ground situation", "Planning Section / Regularization Committee", "Conduct site verification and area profiling where required.", "Inspect current occupation.", "Prepare area profile.", "Record committee site report where applicable."),
                Stage("Seek approval path", "Regularization Committee / Managing Director", "Route site report and recommendation for approval or disapproval when records are missing or committee action is required.", "Compile findings.", "Submit to MD where required.", "Capture approval decision."),
                Stage("Prepare approved site plan", "Drawing Office Supervisor / Draughtsman", "Prepare the site plan for favorable or approved applications.", "Review dimensions and access.", "Validate buffers, easements, and coordinates.", "Prepare draughtsman, DOS, and STP signatures."),
                Stage("Return for further action", "Supervising Town Planner", "Refer the completed file to HOD or Regularization Office for the next action.", "Attach signed site plan if prepared.", "Attach planning information note.", "Record dispatch.")
            },
            new[] { "Regularization planning advice", "Site verification report", "Approved site plan where applicable", "MD or committee decision note" }),
        Procedure(
            "Layout Review and Correction",
            "PlanningLayoutReviewCorrection",
            "Identify and rectify anomalies in approved layouts.",
            "FilePenLine",
            "orange",
            new[]
            {
                Stage("Identify anomaly", "Planning Section", "Identify layout anomalies from audit findings or recommendations.", "Record anomaly type.", "Capture affected layout or parcel.", "Attach audit finding where available."),
                Stage("Technical review", "Planning Technical Team", "Review the issue against the approved master plan or layout.", "Cross-reference land use.", "Check affected files and drawings.", "Document correction requirement."),
                Stage("Request required files", "Supervising Town Planner", "Request Estate files where needed to support correction or review.", "List required files.", "Send official request to Estate.", "Track file receipt."),
                Stage("Correct layout", "Drawing Office Supervisor", "Amend or update the layout to resolve the anomaly.", "Prepare correction drawing.", "Validate dimensions and references.", "Keep correction history."),
                Stage("Submit updated layout", "Supervising Town Planner", "Vet the response and submit the updated layout to HOD.", "Review correction package.", "Attach explanation.", "Forward to HOD for further action.")
            },
            new[] { "Updated layout", "Layout anomaly correction note", "Estate file request trail", "HOD submission" }),
        Procedure(
            "Compliance Site Inspection and Reporting",
            "PlanningComplianceInspection",
            "Physically inspect and verify ground situation for compliance matters.",
            "ClipboardList",
            "rose",
            new[]
            {
                Stage("Receive inspection request", "Head of Development / Supervising Town Planner", "Receive the application, file, or letter for compliance inspection.", "Capture compliance concern.", "Confirm site reference.", "Validate attachments."),
                Stage("Assign review", "Supervising Town Planner", "Vet and assign the request to a Town Planner, Physical Planner, or technical officer.", "Confirm assigned officer.", "Provide inspection scope.", "Set due date."),
                Stage("Conduct inspection", "Planning Technical Officer", "Perform the compliance site inspection.", "Verify use and development on ground.", "Check against planning approvals.", "Capture photos and notes."),
                Stage("Prepare compliance report", "Planning Technical Officer", "Produce the compliance site report.", "State compliance status.", "List breaches or observations.", "Recommend next action."),
                Stage("Submit to HOD", "Supervising Town Planner", "Review and refer the site report to HOD.", "Check completeness.", "Add STP comments.", "Route for further action.")
            },
            new[] { "Compliance site report", "Inspection evidence", "Compliance recommendation", "HOD routing note" }),
        Procedure(
            "Dispute Resolution and Client Complaint Management",
            "PlanningDisputeComplaint",
            "Handle planning-related complaints and boundary disputes fairly.",
            "MessageSquare",
            "purple",
            new[]
            {
                Stage("Receive complaint or dispute", "Head of Development / Supervising Town Planner", "Receive the file, application, or letter with supporting documents.", "Capture complainant and subject.", "Identify boundary or planning issue.", "Confirm supporting records."),
                Stage("Assign assessment", "Supervising Town Planner", "Assign the matter to a Town Planner, Physical Planner, or technical officer.", "Set assessment scope.", "Provide planning records.", "Set response timeline."),
                Stage("Internal review", "Planning Technical Officer", "Review available Planning Section records and assess the issue.", "Check layouts and site plans.", "Review prior correspondence.", "Identify gaps or conflicts."),
                Stage("Coordinate with HOD", "Supervising Town Planner", "Liaise with HOD on further action based on available records.", "Summarize findings.", "Recommend resolution route.", "Escalate where records are insufficient."),
                Stage("Close response", "Supervising Town Planner", "Submit Planning Section response for further action.", "Attach record references.", "State unresolved issues.", "Record dispatch.")
            },
            new[] { "Planning complaint assessment", "Boundary dispute findings", "Resolution recommendation", "Planning records summary" }),
        Procedure(
            "District Assembly Spatial Planning Committee Meetings",
            "PlanningAssemblySpatialCommittee",
            "Represent TDC's interest in MMDAs permitting and spatial planning committee processes.",
            "Users",
            "slate",
            new[]
            {
                Stage("Receive invitation", "Head of Development / Supervising Town Planner", "Receive the committee invitation letter from HOD.", "Capture meeting date and MMDA.", "Confirm agenda items.", "Log referral."),
                Stage("Assign representative", "Supervising Town Planner", "Attend the meeting or assign a Town Planner, Physical Planner, or officer.", "Confirm representative.", "Provide company position and relevant files.", "Set reporting expectation."),
                Stage("Attend meeting", "Assigned Planning Officer", "Represent TDC's interest at the meeting.", "Record deliberations.", "Track decisions affecting TDC lands.", "Capture action items."),
                Stage("Prepare meeting report", "Assigned Planning Officer", "Prepare the meeting report after attendance.", "Summarize agenda and outcomes.", "Highlight risks or required actions.", "Attach supporting documents."),
                Stage("Submit through STP", "Supervising Town Planner", "Submit the report to HOD through STP for further action.", "Review report.", "Add STP comments.", "Forward to HOD.")
            },
            new[] { "Spatial planning committee report", "Action item log", "TDC position note", "HOD submission" })
    };

    public IReadOnlyList<PlanningProcedureCatalogItem> GetProcedures() =>
        Procedures.Select(item => item.CatalogItem).ToList();

    public PlanningProcedureWorkspace? GetProcedureWorkspace(string entityType)
    {
        var procedure = Procedures.FirstOrDefault(item =>
            string.Equals(item.CatalogItem.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        if (procedure is null)
        {
            return null;
        }

        return new PlanningProcedureWorkspace(
            procedure.CatalogItem,
            procedure.Stages,
            CommonDocuments(procedure.CatalogItem.Title),
            CommonFields(procedure.CatalogItem.Title),
            procedure.Outputs,
            CommonHandoffs());
    }

    private static PlanningProcedureDefinition Procedure(
        string title,
        string entityType,
        string summary,
        string icon,
        string accent,
        IReadOnlyList<PlanningWorkspaceStage> stages,
        IReadOnlyList<string> outputs) =>
        new(
            new PlanningProcedureCatalogItem(title, entityType, SourceName, summary, icon, stages.Count, accent),
            stages,
            outputs);

    private static PlanningWorkspaceStage Stage(string name, string owner, string summary, params string[] checklist) =>
        new(name, owner, summary, checklist);

    private static PlanningWorkspaceDocument Doc(string name, string requiredFrom, bool isMandatory) =>
        new(name, requiredFrom, isMandatory);

    private static PlanningWorkspaceField Field(string key, string label, string type, params string[] options) =>
        new(key, label, type, options.Length == 0 ? null : options);

    private static PlanningWorkspaceHandoff Handoff(string fromRole, string toRole, string trigger) =>
        new(fromRole, toRole, trigger);

    private static IReadOnlyList<PlanningWorkspaceDocument> CommonDocuments(string procedureTitle) =>
        new[]
        {
            Doc("Application, file, or request letter", "Head of Development / Applicant", true),
            Doc("Site plan, base map, or layout extract", "Applicant / Planning Records", true),
            Doc("Supporting ownership or allocation documents", "Estate / Applicant", false),
            Doc("Inspection photos, field notes, or site report", "Planning Technical Officer", false),
            Doc($"{procedureTitle} recommendation or report", "Planning Section", true)
        };

    private static IReadOnlyList<PlanningWorkspaceField> CommonFields(string procedureTitle) =>
        new[]
        {
            Field("referenceNumber", "Reference number", "text"),
            Field("procedureType", "Procedure", "text", procedureTitle),
            Field("applicantName", "Applicant / client name", "text"),
            Field("plotOrParcelReference", "Plot / parcel reference", "text"),
            Field("estateLandBankReference", "Estate land bank reference", "text"),
            Field("projectReference", "Project reference", "text"),
            Field("location", "Location", "text"),
            Field("masterPlanLayoutReference", "Master plan / layout reference", "text"),
            Field("receivedDate", "Received date", "date"),
            Field("priority", "Priority", "select", "Normal", "Urgent", "Committee deadline", "Permit deadline"),
            Field("assignedOfficer", "Assigned planning officer", "text"),
            Field("dmsFolderReference", "DMS folder reference", "text")
        };

    private static IReadOnlyList<PlanningWorkspaceHandoff> CommonHandoffs() =>
        new[]
        {
            Handoff("Head of Development", "Supervising Town Planner", "Application, file, letter, base map, or invitation is referred to Planning"),
            Handoff("Supervising Town Planner", "Planning Technical Officer", "Technical review, inspection, search, or design action is required"),
            Handoff("Planning Technical Officer", "Supervising Town Planner", "Draft report, layout, site plan, or findings are ready for vetting"),
            Handoff("Supervising Town Planner", "Head of Development", "Planning recommendation, report, or endorsed output is complete"),
            Handoff("Head of Development", "Committee / Estate / Regularization Office", "Further action, approval, file correction, or stakeholder routing is required")
        };

    private sealed record PlanningProcedureDefinition(
        PlanningProcedureCatalogItem CatalogItem,
        IReadOnlyList<PlanningWorkspaceStage> Stages,
        IReadOnlyList<string> Outputs);
}
