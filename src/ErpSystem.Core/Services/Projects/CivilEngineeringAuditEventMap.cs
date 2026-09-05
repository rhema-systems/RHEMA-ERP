using ErpSystem.Core.DTOs.Compliance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.Audit;
using ErpSystem.Core.Services.Audit;

namespace ErpSystem.Core.Services.Projects;

[Flags]
public enum CivilEngineeringAuditFacet
{
    None = 0,
    Tenant = 1,
    Actor = 2,
    ActorRoles = 4,
    Source = 8,
    BeforeValues = 16,
    AfterValues = 32,
    FileVersionEvidence = 64,
    ApprovalState = 128,
    CorrelationId = 256,
    Reason = 512
}

public sealed record CivilEngineeringAuditEventDefinition(
    string Action,
    AuditOperationKind Operation,
    string SourceType,
    string Control,
    CivilEngineeringAuditFacet RequiredFacets);

/// <summary>
/// Canonical event names and minimum audit payload contract for Civil Engineering actions.
/// Civil services contribute to the shared audit-governance surface and must not create a
/// separate audit store. Evidence is a central-DMS record/version reference, never a file copy.
/// </summary>
public static class CivilEngineeringAuditEventMap
{
    public const string CreateProfile = "CreateProfile";
    public const string UpdateProfile = "UpdateProfile";
    public const string SaveDecision = "SaveDecision";
    public const string SubmitDecision = "SubmitDecision";
    public const string ApproveDecision = "ApproveDecision";
    public const string RejectDecision = "RejectDecision";
    public const string LinkEvidence = "LinkEvidence";
    public const string UnlinkEvidence = "UnlinkEvidence";
    public const string PublishProfile = "PublishProfile";
    public const string RetireProfile = "RetireProfile";
    public const string CloneDraft = "CloneDraft";
    public const string DeleteDraft = "DeleteDraft";
    public const string SeedDraft = "SeedDraft";

    public const string CreateEngineeringCase = "CreateEngineeringCase";
    public const string UpdateEngineeringCase = "UpdateEngineeringCase";
    public const string SubmitEngineeringCase = "SubmitEngineeringCase";
    public const string ApproveEngineeringCase = "ApproveEngineeringCase";
    public const string RejectEngineeringCase = "RejectEngineeringCase";
    public const string LinkEngineeringCaseSource = "LinkEngineeringCaseSource";
    public const string CreatePlanningGisValidation = "CreatePlanningGisValidation";
    public const string SubmitPlanningGisValidation = "SubmitPlanningGisValidation";
    public const string ApprovePlanningGisValidation = "ApprovePlanningGisValidation";
    public const string RejectPlanningGisValidation = "RejectPlanningGisValidation";

    public const string CreateDesignDirective = "CreateDesignDirective";
    public const string UpdateDesignTaskAssignment = "UpdateDesignTaskAssignment";
    public const string CreateSiteReconnaissance = "CreateSiteReconnaissance";
    public const string UpdateSiteReconnaissance = "UpdateSiteReconnaissance";
    public const string CreateCrossSectionInputRequest = "CreateCrossSectionInputRequest";
    public const string SubmitCrossSectionInput = "SubmitCrossSectionInput";
    public const string ApproveCrossSectionInput = "ApproveCrossSectionInput";
    public const string RejectCrossSectionInput = "RejectCrossSectionInput";
    public const string SubmitDesignForReview = "SubmitDesignForReview";
    public const string ApproveDesign = "ApproveDesign";
    public const string RejectDesign = "RejectDesign";
    public const string UpdateDraftingAssignment = "UpdateDraftingAssignment";
    public const string SubmitDrawingForReview = "SubmitDrawingForReview";
    public const string ApproveDrawing = "ApproveDrawing";
    public const string RejectDrawing = "RejectDrawing";
    public const string SubmitDesignPackage = "SubmitDesignPackage";
    public const string ApproveDesignPackage = "ApproveDesignPackage";
    public const string RejectDesignPackage = "RejectDesignPackage";
    public const string CreateEngineeringFileVersion = "CreateEngineeringFileVersion";
    public const string PublishEngineeringFileVersion = "PublishEngineeringFileVersion";
    public const string RetireEngineeringFileVersion = "RetireEngineeringFileVersion";

    public const string CreateProjectEngineerAssignment = "CreateProjectEngineerAssignment";
    public const string UpdateProjectEngineerAssignment = "UpdateProjectEngineerAssignment";
    public const string IssueSiteInstruction = "IssueSiteInstruction";
    public const string AcknowledgeSiteInstruction = "AcknowledgeSiteInstruction";
    public const string UpdateSiteInstructionResponse = "UpdateSiteInstructionResponse";
    public const string ApproveSiteInstructionResponse = "ApproveSiteInstructionResponse";
    public const string ReturnSiteInstructionResponse = "ReturnSiteInstructionResponse";
    public const string FollowUpSiteInstruction = "FollowUpSiteInstruction";
    public const string CloseSiteInstruction = "CloseSiteInstruction";
    public const string SupersedeSiteInstruction = "SupersedeSiteInstruction";
    public const string CreateRfi = "CreateRfi";
    public const string SubmitRfiResponse = "SubmitRfiResponse";
    public const string ApproveRfiResponse = "ApproveRfiResponse";
    public const string RejectRfiResponse = "RejectRfiResponse";
    public const string CreateEngineeringTestReport = "CreateEngineeringTestReport";
    public const string ApproveEngineeringTestReport = "ApproveEngineeringTestReport";
    public const string RejectEngineeringTestReport = "RejectEngineeringTestReport";
    public const string CreateWeeklySupervisionReport = "CreateWeeklySupervisionReport";
    public const string SubmitWeeklySupervisionReport = "SubmitWeeklySupervisionReport";
    public const string ApproveWeeklySupervisionReport = "ApproveWeeklySupervisionReport";
    public const string RejectWeeklySupervisionReport = "RejectWeeklySupervisionReport";
    public const string EscalateWeeklySupervisionReport = "EscalateWeeklySupervisionReport";
    public const string SubmitIpcEngineeringCheck = "SubmitIpcEngineeringCheck";
    public const string ApproveIpcEndorsement = "ApproveIpcEndorsement";
    public const string RejectIpcEndorsement = "RejectIpcEndorsement";
    public const string CreateCivilExtensionOfTime = "CreateCivilExtensionOfTime";
    public const string SubmitCivilExtensionOfTime = "SubmitCivilExtensionOfTime";
    public const string ApproveCivilExtensionOfTime = "ApproveCivilExtensionOfTime";
    public const string RejectCivilExtensionOfTime = "RejectCivilExtensionOfTime";

    public const string CreateCivilWorkIntake = "CreateCivilWorkIntake";
    public const string UpdateCivilWorkAssessment = "UpdateCivilWorkAssessment";
    public const string SubmitRemediationScope = "SubmitRemediationScope";
    public const string ApproveRemediationScope = "ApproveRemediationScope";
    public const string RejectRemediationScope = "RejectRemediationScope";
    public const string HandoffCostingApproval = "HandoffCostingApproval";
    public const string LinkMaintenanceWorkOrder = "LinkMaintenanceWorkOrder";
    public const string CreateCivilCompletionReport = "CreateCivilCompletionReport";
    public const string SubmitCivilCompletionReport = "SubmitCivilCompletionReport";
    public const string ApproveCivilCompletionReport = "ApproveCivilCompletionReport";
    public const string RejectCivilCompletionReport = "RejectCivilCompletionReport";
    public const string CreateCivilInspection = "CreateCivilInspection";
    public const string ApproveCivilInspectionPlan = "ApproveCivilInspectionPlan";
    public const string RejectCivilInspectionPlan = "RejectCivilInspectionPlan";
    public const string ApproveCivilInspection = "ApproveCivilInspection";
    public const string RejectCivilInspection = "RejectCivilInspection";
    public const string CloseCivilInspection = "CloseCivilInspection";
    public const string SubmitPaymentRecommendation = "SubmitPaymentRecommendation";
    public const string ApproveCivilWorkClosure = "ApproveCivilWorkClosure";
    public const string RejectCivilWorkClosure = "RejectCivilWorkClosure";

    public const string CreateDevelopmentApprovalFile = "CreateDevelopmentApprovalFile";
    public const string UpdateDevelopmentFileHandoff = "UpdateDevelopmentFileHandoff";
    public const string SubmitEngineeringRecommendation = "SubmitEngineeringRecommendation";
    public const string ApproveEngineeringRecommendation = "ApproveEngineeringRecommendation";
    public const string RejectEngineeringRecommendation = "RejectEngineeringRecommendation";
    public const string SubmitDevelopmentDecision = "SubmitDevelopmentDecision";
    public const string ApproveDevelopmentDecision = "ApproveDevelopmentDecision";
    public const string RejectDevelopmentDecision = "RejectDevelopmentDecision";
    public const string UpdateDevelopmentDecisionReturn = "UpdateDevelopmentDecisionReturn";

    public const string CreateCivilTask = "CreateCivilTask";
    public const string UpdateCivilTaskAssignment = "UpdateCivilTaskAssignment";
    public const string AcknowledgeCivilTask = "AcknowledgeCivilTask";
    public const string UpdateCivilTaskProgress = "UpdateCivilTaskProgress";
    public const string AttachCivilTaskEvidence = "AttachCivilTaskEvidence";
    public const string SubmitCivilTaskCompletion = "SubmitCivilTaskCompletion";
    public const string ApproveCivilTaskCompletion = "ApproveCivilTaskCompletion";
    public const string RejectCivilTaskCompletion = "RejectCivilTaskCompletion";
    public const string OverrideCivilTaskUrgency = "OverrideCivilTaskUrgency";
    public const string CreateCivilMobileFeedback = "CreateCivilMobileFeedback";
    public const string AttachCivilMobileEvidence = "AttachCivilMobileEvidence";
    public const string SubmitCivilMobileFeedback = "SubmitCivilMobileFeedback";
    public const string ApproveCivilMobileFeedback = "ApproveCivilMobileFeedback";

    public const string CreateCivilInterfaceHandoff = "CreateCivilInterfaceHandoff";
    public const string UpdateCivilInterfaceStatus = "UpdateCivilInterfaceStatus";
    public const string AttachCivilInterfaceEvidence = "AttachCivilInterfaceEvidence";
    public const string SubmitCivilVariationImpact = "SubmitCivilVariationImpact";
    public const string ApproveCivilVariationImpact = "ApproveCivilVariationImpact";
    public const string RejectCivilVariationImpact = "RejectCivilVariationImpact";
    public const string CreateCivilDefect = "CreateCivilDefect";
    public const string UpdateCivilDefectCorrectiveAction = "UpdateCivilDefectCorrectiveAction";
    public const string SubmitCivilDefectReinspection = "SubmitCivilDefectReinspection";
    public const string ApproveCivilDefectClosure = "ApproveCivilDefectClosure";
    public const string RejectCivilDefectClosure = "RejectCivilDefectClosure";
    public const string CreateCivilCompletionCertificate = "CreateCivilCompletionCertificate";
    public const string ApproveCivilCompletionCertificate = "ApproveCivilCompletionCertificate";
    public const string RejectCivilCompletionCertificate = "RejectCivilCompletionCertificate";
    public const string CreateCivilHandover = "CreateCivilHandover";
    public const string ApproveCivilHandover = "ApproveCivilHandover";
    public const string RejectCivilHandover = "RejectCivilHandover";
    public const string LinkCivilAssetHistory = "LinkCivilAssetHistory";
    public const string CreateCivilReportExport = "CreateCivilReportExport";
    public const string CreateCivilMigrationBatch = "CreateCivilMigrationBatch";
    public const string SubmitCivilMigrationBatch = "SubmitCivilMigrationBatch";
    public const string ApproveCivilMigrationBatch = "ApproveCivilMigrationBatch";
    public const string RejectCivilMigrationBatch = "RejectCivilMigrationBatch";

    private const CivilEngineeringAuditFacet RequiredContext =
        CivilEngineeringAuditFacet.Tenant |
        CivilEngineeringAuditFacet.Actor |
        CivilEngineeringAuditFacet.ActorRoles |
        CivilEngineeringAuditFacet.Source |
        CivilEngineeringAuditFacet.BeforeValues |
        CivilEngineeringAuditFacet.AfterValues |
        CivilEngineeringAuditFacet.FileVersionEvidence |
        CivilEngineeringAuditFacet.ApprovalState |
        CivilEngineeringAuditFacet.CorrelationId;

    private static readonly CivilEngineeringAuditEventDefinition[] Items =
    [
        Definition(CreateProfile, AuditOperationKind.Create, "CivilEngineeringConfigurationProfile", "Configuration profile creation"),
        Definition(UpdateProfile, AuditOperationKind.Update, "CivilEngineeringConfigurationProfile", "Controlled profile amendment", CivilEngineeringAuditFacet.Reason),
        Definition(SaveDecision, AuditOperationKind.Update, "CivilEngineeringConfigurationDecision", "Typed configuration decision amendment", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitDecision, AuditOperationKind.Create, "CivilEngineeringConfigurationDecision", "Configuration decision submission"),
        Definition(ApproveDecision, AuditOperationKind.Approve, "CivilEngineeringConfigurationDecision", "Independent configuration decision approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectDecision, AuditOperationKind.Reject, "CivilEngineeringConfigurationDecision", "Independent configuration decision rejection", CivilEngineeringAuditFacet.Reason),
        Definition(LinkEvidence, AuditOperationKind.Update, "CivilEngineeringConfigurationDecision", "Central-DMS configuration evidence link", CivilEngineeringAuditFacet.Reason),
        Definition(UnlinkEvidence, AuditOperationKind.Update, "CivilEngineeringConfigurationDecision", "Central-DMS configuration evidence unlink", CivilEngineeringAuditFacet.Reason),
        Definition(PublishProfile, AuditOperationKind.Approve, "CivilEngineeringConfigurationProfile", "Configuration publication", CivilEngineeringAuditFacet.Reason),
        Definition(RetireProfile, AuditOperationKind.Update, "CivilEngineeringConfigurationProfile", "Configuration retirement", CivilEngineeringAuditFacet.Reason),
        Definition(CloneDraft, AuditOperationKind.Create, "CivilEngineeringConfigurationProfile", "Versioned configuration clone"),
        Definition(DeleteDraft, AuditOperationKind.Update, "CivilEngineeringConfigurationProfile", "Controlled configuration draft deletion", CivilEngineeringAuditFacet.Reason),
        Definition(SeedDraft, AuditOperationKind.Create, "CivilEngineeringConfigurationProfile", "Idempotent tenant configuration seed"),

        Definition(CreateEngineeringCase, AuditOperationKind.Create, "CivilEngineeringCase", "Engineering case initiation"),
        Definition(UpdateEngineeringCase, AuditOperationKind.Update, "CivilEngineeringCase", "Engineering case amendment", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitEngineeringCase, AuditOperationKind.Create, "CivilEngineeringCase", "Engineering case submission", CivilEngineeringAuditFacet.Reason),
        Definition(ApproveEngineeringCase, AuditOperationKind.Approve, "CivilEngineeringCase", "Independent engineering case approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectEngineeringCase, AuditOperationKind.Reject, "CivilEngineeringCase", "Independent engineering case rejection", CivilEngineeringAuditFacet.Reason),
        Definition(LinkEngineeringCaseSource, AuditOperationKind.Update, "CivilEngineeringCase", "Authoritative source-record linkage", CivilEngineeringAuditFacet.Reason),
        Definition(CreatePlanningGisValidation, AuditOperationKind.Create, "CivilPlanningGisValidation", "Estate and Planning/GIS validation registration", CivilEngineeringAuditFacet.FileVersionEvidence),
        Definition(SubmitPlanningGisValidation, AuditOperationKind.Create, "CivilPlanningGisValidation", "Planning/GIS validation submission", CivilEngineeringAuditFacet.FileVersionEvidence),
        Definition(ApprovePlanningGisValidation, AuditOperationKind.Approve, "CivilPlanningGisValidation", "Independent Planning/GIS validation approval", CivilEngineeringAuditFacet.FileVersionEvidence),
        Definition(RejectPlanningGisValidation, AuditOperationKind.Reject, "CivilPlanningGisValidation", "Independent Planning/GIS validation rejection", CivilEngineeringAuditFacet.FileVersionEvidence),

        Definition(CreateDesignDirective, AuditOperationKind.Create, "CivilDesignPackage", "HOD design directive"),
        Definition(UpdateDesignTaskAssignment, AuditOperationKind.Update, "CivilDesignPackage", "Controlled design-task assignment", CivilEngineeringAuditFacet.Reason),
        Definition(CreateSiteReconnaissance, AuditOperationKind.Create, "CivilSiteReconnaissance", "Site reconnaissance capture"),
        Definition(UpdateSiteReconnaissance, AuditOperationKind.Update, "CivilSiteReconnaissance", "Site reconnaissance amendment", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCrossSectionInputRequest, AuditOperationKind.Create, "CivilDesignInputRequest", "Cross-section input request"),
        Definition(SubmitCrossSectionInput, AuditOperationKind.Create, "CivilDesignInputRequest", "Cross-section response submission"),
        Definition(ApproveCrossSectionInput, AuditOperationKind.Approve, "CivilDesignInputRequest", "Cross-section input acceptance", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCrossSectionInput, AuditOperationKind.Reject, "CivilDesignInputRequest", "Cross-section input rejection", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitDesignForReview, AuditOperationKind.Update, "CivilDesignPackage", "Civil design review submission"),
        Definition(ApproveDesign, AuditOperationKind.Approve, "CivilDesignPackage", "SCE design approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectDesign, AuditOperationKind.Reject, "CivilDesignPackage", "SCE design rejection", CivilEngineeringAuditFacet.Reason),
        Definition(UpdateDraftingAssignment, AuditOperationKind.Update, "CivilDesignPackage", "Controlled drafting assignment", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitDrawingForReview, AuditOperationKind.Update, "CivilDesignPackage", "Engineering drawing review submission"),
        Definition(ApproveDrawing, AuditOperationKind.Approve, "CivilDesignPackage", "Engineering drawing approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectDrawing, AuditOperationKind.Reject, "CivilDesignPackage", "Engineering drawing rejection", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitDesignPackage, AuditOperationKind.Create, "CivilDesignPackage", "Complete design package submission"),
        Definition(ApproveDesignPackage, AuditOperationKind.Approve, "CivilDesignPackage", "HOD design package approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectDesignPackage, AuditOperationKind.Reject, "CivilDesignPackage", "HOD design package rejection", CivilEngineeringAuditFacet.Reason),
        Definition(CreateEngineeringFileVersion, AuditOperationKind.Create, "CentralDocumentVersion", "Central-DMS engineering file version"),
        Definition(PublishEngineeringFileVersion, AuditOperationKind.Approve, "CentralDocumentVersion", "Approved engineering file publication", CivilEngineeringAuditFacet.Reason),
        Definition(RetireEngineeringFileVersion, AuditOperationKind.Update, "CentralDocumentVersion", "Superseded engineering file retirement", CivilEngineeringAuditFacet.Reason),

        Definition(CreateProjectEngineerAssignment, AuditOperationKind.Create, "ProjectMember", "Project Engineer assignment"),
        Definition(UpdateProjectEngineerAssignment, AuditOperationKind.Update, "ProjectMember", "Project Engineer reassignment", CivilEngineeringAuditFacet.Reason),
        Definition(IssueSiteInstruction, AuditOperationKind.Dispatch, "ProjectSiteInstruction", "Site instruction issue through Project Manager", CivilEngineeringAuditFacet.Reason),
        Definition(AcknowledgeSiteInstruction, AuditOperationKind.Receive, "ProjectSiteInstruction", "Contractor site-instruction acknowledgement"),
        Definition(UpdateSiteInstructionResponse, AuditOperationKind.Update, "ProjectSiteInstruction", "Contractor site-instruction response"),
        Definition(ApproveSiteInstructionResponse, AuditOperationKind.Approve, "ProjectSiteInstruction", "Engineering review of contractor site-instruction response", CivilEngineeringAuditFacet.Reason | CivilEngineeringAuditFacet.FileVersionEvidence),
        Definition(ReturnSiteInstructionResponse, AuditOperationKind.Reject, "ProjectSiteInstruction", "Engineering return of contractor site-instruction response", CivilEngineeringAuditFacet.Reason | CivilEngineeringAuditFacet.FileVersionEvidence),
        Definition(FollowUpSiteInstruction, AuditOperationKind.Update, "ProjectSiteInstruction", "Engineering site-instruction follow-up", CivilEngineeringAuditFacet.Reason | CivilEngineeringAuditFacet.FileVersionEvidence),
        Definition(CloseSiteInstruction, AuditOperationKind.Approve, "ProjectSiteInstruction", "Engineering site-instruction closure", CivilEngineeringAuditFacet.Reason | CivilEngineeringAuditFacet.FileVersionEvidence),
        Definition(SupersedeSiteInstruction, AuditOperationKind.Update, "ProjectSiteInstruction", "Versioned site-instruction supersession", CivilEngineeringAuditFacet.Reason),
        Definition(CreateRfi, AuditOperationKind.Create, "ProjectRfi", "Contractor or consultant RFI creation"),
        Definition(SubmitRfiResponse, AuditOperationKind.Update, "ProjectRfi", "Versioned RFI response submission"),
        Definition(ApproveRfiResponse, AuditOperationKind.Approve, "ProjectRfi", "RFI response approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectRfiResponse, AuditOperationKind.Reject, "ProjectRfi", "RFI response rejection", CivilEngineeringAuditFacet.Reason),
        Definition(CreateEngineeringTestReport, AuditOperationKind.Create, "ProjectQualityTestReport", "Engineering test-report registration"),
        Definition(ApproveEngineeringTestReport, AuditOperationKind.Approve, "ProjectQualityTestReport", "Engineering test-report endorsement", CivilEngineeringAuditFacet.Reason),
        Definition(RejectEngineeringTestReport, AuditOperationKind.Reject, "ProjectQualityTestReport", "Failed or unverified test-report rejection", CivilEngineeringAuditFacet.Reason),
        Definition(CreateWeeklySupervisionReport, AuditOperationKind.Create, "CivilWeeklySupervisionReport", "Weekly supervision report preparation"),
        Definition(SubmitWeeklySupervisionReport, AuditOperationKind.Create, "CivilWeeklySupervisionReport", "Weekly supervision report submission"),
        Definition(ApproveWeeklySupervisionReport, AuditOperationKind.Approve, "CivilWeeklySupervisionReport", "SCE or HOD weekly report approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectWeeklySupervisionReport, AuditOperationKind.Reject, "CivilWeeklySupervisionReport", "Weekly supervision report return", CivilEngineeringAuditFacet.Reason),
        Definition(EscalateWeeklySupervisionReport, AuditOperationKind.Dispatch, "CivilWeeklySupervisionReport", "Overdue weekly supervision report escalation", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitIpcEngineeringCheck, AuditOperationKind.Create, "QuantitySurveyPaymentCertificate", "IPC engineering check submission"),
        Definition(ApproveIpcEndorsement, AuditOperationKind.Approve, "QuantitySurveyPaymentCertificate", "Project Engineer IPC endorsement", CivilEngineeringAuditFacet.Reason),
        Definition(RejectIpcEndorsement, AuditOperationKind.Reject, "QuantitySurveyPaymentCertificate", "Project Engineer IPC rejection", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCivilExtensionOfTime, AuditOperationKind.Create, "ProjectCivilExtensionOfTimeControl", "Create governed Civil extension-of-time request", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitCivilExtensionOfTime, AuditOperationKind.Create, "ProjectCivilExtensionOfTimeControl", "Submit governed Civil extension-of-time workflow", CivilEngineeringAuditFacet.Reason),
        Definition(ApproveCivilExtensionOfTime, AuditOperationKind.Approve, "ProjectCivilExtensionOfTimeControl", "Approve governed Civil extension of time", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilExtensionOfTime, AuditOperationKind.Reject, "ProjectCivilExtensionOfTimeControl", "Reject governed Civil extension of time", CivilEngineeringAuditFacet.Reason),

        Definition(CreateCivilWorkIntake, AuditOperationKind.Create, "CivilWorkCase", "Maintenance or complaint intake"),
        Definition(UpdateCivilWorkAssessment, AuditOperationKind.Update, "CivilWorkCase", "Civil assessment and remediation scope", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitRemediationScope, AuditOperationKind.Create, "CivilWorkCase", "Remediation scope submission"),
        Definition(ApproveRemediationScope, AuditOperationKind.Approve, "CivilWorkCase", "Independent remediation scope approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectRemediationScope, AuditOperationKind.Reject, "CivilWorkCase", "Remediation scope rejection", CivilEngineeringAuditFacet.Reason),
        Definition(HandoffCostingApproval, AuditOperationKind.Create, "CivilWorkCase", "QS, Finance and Procurement costing handoff"),
        Definition(LinkMaintenanceWorkOrder, AuditOperationKind.Update, "CivilWorkCase", "Authoritative Maintenance work-order linkage", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCivilCompletionReport, AuditOperationKind.Create, "CivilWorkCompletion", "Civil completion report preparation"),
        Definition(SubmitCivilCompletionReport, AuditOperationKind.Create, "CivilWorkCompletion", "Civil completion report submission"),
        Definition(ApproveCivilCompletionReport, AuditOperationKind.Approve, "CivilWorkCompletion", "Independent completion report approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilCompletionReport, AuditOperationKind.Reject, "CivilWorkCompletion", "Completion report rejection", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCivilInspection, AuditOperationKind.Create, "ProjectInspection", "Civil completion or corrective inspection"),
        Definition(ApproveCivilInspectionPlan, AuditOperationKind.Approve, "ProjectInspection", "Approved governed Civil inspection plan", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilInspectionPlan, AuditOperationKind.Reject, "ProjectInspection", "Rejected governed Civil inspection plan", CivilEngineeringAuditFacet.Reason),
        Definition(ApproveCivilInspection, AuditOperationKind.Approve, "ProjectInspection", "Passed Civil inspection", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilInspection, AuditOperationKind.Reject, "ProjectInspection", "Failed Civil inspection", CivilEngineeringAuditFacet.Reason),
        Definition(CloseCivilInspection, AuditOperationKind.Approve, "ProjectInspection", "Independent Civil inspection closure", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitPaymentRecommendation, AuditOperationKind.Create, "CivilWorkCase", "Payment recommendation submission"),
        Definition(ApproveCivilWorkClosure, AuditOperationKind.Approve, "CivilWorkCase", "Civil work closure approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilWorkClosure, AuditOperationKind.Reject, "CivilWorkCase", "Civil work closure rejection", CivilEngineeringAuditFacet.Reason),

        Definition(CreateDevelopmentApprovalFile, AuditOperationKind.Create, "DevelopmentApprovalFile", "Development approval file registration"),
        Definition(UpdateDevelopmentFileHandoff, AuditOperationKind.Update, "DevelopmentApprovalFile", "Inter-section development file handoff"),
        Definition(SubmitEngineeringRecommendation, AuditOperationKind.Create, "DevelopmentApprovalFile", "Engineering recommendation submission"),
        Definition(ApproveEngineeringRecommendation, AuditOperationKind.Approve, "DevelopmentApprovalFile", "SCE engineering recommendation", CivilEngineeringAuditFacet.Reason),
        Definition(RejectEngineeringRecommendation, AuditOperationKind.Reject, "DevelopmentApprovalFile", "Engineering recommendation rejection", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitDevelopmentDecision, AuditOperationKind.Create, "DevelopmentApprovalFile", "HOD development decision submission"),
        Definition(ApproveDevelopmentDecision, AuditOperationKind.Approve, "DevelopmentApprovalFile", "HOD development approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectDevelopmentDecision, AuditOperationKind.Reject, "DevelopmentApprovalFile", "HOD development rejection", CivilEngineeringAuditFacet.Reason),
        Definition(UpdateDevelopmentDecisionReturn, AuditOperationKind.Update, "DevelopmentApprovalFile", "Development file return for correction", CivilEngineeringAuditFacet.Reason),

        Definition(CreateCivilTask, AuditOperationKind.Create, "ProjectTask", "Direct Civil task assignment"),
        Definition(UpdateCivilTaskAssignment, AuditOperationKind.Update, "ProjectTask", "Civil task reassignment", CivilEngineeringAuditFacet.Reason),
        Definition(AcknowledgeCivilTask, AuditOperationKind.Receive, "ProjectTask", "Assignee task acknowledgement"),
        Definition(UpdateCivilTaskProgress, AuditOperationKind.Update, "ProjectTask", "Assignee progress update"),
        Definition(AttachCivilTaskEvidence, AuditOperationKind.Create, "ProjectTask", "Central-DMS task evidence attachment"),
        Definition(SubmitCivilTaskCompletion, AuditOperationKind.Create, "ProjectTask", "Civil task completion submission"),
        Definition(ApproveCivilTaskCompletion, AuditOperationKind.Approve, "ProjectTask", "Reviewer task acceptance", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilTaskCompletion, AuditOperationKind.Reject, "ProjectTask", "Reviewer task send-back", CivilEngineeringAuditFacet.Reason),
        Definition(OverrideCivilTaskUrgency, AuditOperationKind.Override, "ProjectTask", "Audited urgent-task override", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCivilMobileFeedback, AuditOperationKind.Create, "ProjectMobileAssignment", "Civil field feedback capture"),
        Definition(AttachCivilMobileEvidence, AuditOperationKind.Create, "ProjectMobileAssignment", "Central-DMS mobile evidence attachment"),
        Definition(SubmitCivilMobileFeedback, AuditOperationKind.Create, "ProjectMobileAssignment", "Civil mobile feedback submission"),
        Definition(ApproveCivilMobileFeedback, AuditOperationKind.Approve, "ProjectMobileAssignment", "Civil mobile feedback acceptance", CivilEngineeringAuditFacet.Reason),

        Definition(CreateCivilInterfaceHandoff, AuditOperationKind.Create, "CivilInterfaceHandoff", "Controlled cross-module handoff"),
        Definition(UpdateCivilInterfaceStatus, AuditOperationKind.Update, "CivilInterfaceHandoff", "Authoritative interface-status reconciliation", CivilEngineeringAuditFacet.Reason),
        Definition(AttachCivilInterfaceEvidence, AuditOperationKind.Create, "CivilInterfaceHandoff", "Central-DMS interface evidence"),
        Definition(SubmitCivilVariationImpact, AuditOperationKind.Create, "QuantitySurveyVariation", "Civil scope, time and cost impact submission", CivilEngineeringAuditFacet.Reason),
        Definition(ApproveCivilVariationImpact, AuditOperationKind.Approve, "QuantitySurveyVariation", "Civil variation impact approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilVariationImpact, AuditOperationKind.Reject, "QuantitySurveyVariation", "Civil variation impact rejection", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCivilDefect, AuditOperationKind.Create, "ProjectNonConformance", "Civil defect or snag registration"),
        Definition(UpdateCivilDefectCorrectiveAction, AuditOperationKind.Update, "ProjectNonConformance", "Defect corrective action", CivilEngineeringAuditFacet.Reason),
        Definition(SubmitCivilDefectReinspection, AuditOperationKind.Create, "ProjectNonConformance", "Defect reinspection submission"),
        Definition(ApproveCivilDefectClosure, AuditOperationKind.Approve, "ProjectNonConformance", "Independent defect closure approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilDefectClosure, AuditOperationKind.Reject, "ProjectNonConformance", "Defect closure rejection", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCivilCompletionCertificate, AuditOperationKind.Create, "ProjectCompletionCertificate", "Controlled completion certificate creation"),
        Definition(ApproveCivilCompletionCertificate, AuditOperationKind.Approve, "ProjectCompletionCertificate", "Independent completion certificate approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilCompletionCertificate, AuditOperationKind.Reject, "ProjectCompletionCertificate", "Completion certificate rejection", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCivilHandover, AuditOperationKind.Create, "ProjectHandover", "Civil handover preparation"),
        Definition(ApproveCivilHandover, AuditOperationKind.Approve, "ProjectHandover", "Civil handover approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilHandover, AuditOperationKind.Reject, "ProjectHandover", "Civil handover rejection", CivilEngineeringAuditFacet.Reason),
        Definition(LinkCivilAssetHistory, AuditOperationKind.Update, "CivilEngineeringCase", "Property, Maintenance and Fixed Asset history linkage", CivilEngineeringAuditFacet.Reason),
        Definition(CreateCivilReportExport, AuditOperationKind.Create, "CivilReport", "Permission-scoped Civil report export"),
        Definition(CreateCivilMigrationBatch, AuditOperationKind.Create, "CivilMigrationBatch", "Civil migration staging"),
        Definition(SubmitCivilMigrationBatch, AuditOperationKind.Create, "CivilMigrationBatch", "Civil migration validation submission", CivilEngineeringAuditFacet.Reason),
        Definition(ApproveCivilMigrationBatch, AuditOperationKind.Approve, "CivilMigrationBatch", "Independent Civil migration approval", CivilEngineeringAuditFacet.Reason),
        Definition(RejectCivilMigrationBatch, AuditOperationKind.Reject, "CivilMigrationBatch", "Civil migration rejection", CivilEngineeringAuditFacet.Reason)
    ];

    private static readonly IReadOnlyDictionary<string, CivilEngineeringAuditEventDefinition> ByAction =
        Items.ToDictionary(value => value.Action, StringComparer.Ordinal);

    public static IReadOnlyList<CivilEngineeringAuditEventDefinition> Definitions => Items;

    public static CivilEngineeringAuditEventDefinition GetRequired(string action) =>
        ByAction.TryGetValue(action, out var definition)
            ? definition
            : throw new InvalidOperationException($"Civil Engineering audit action '{action}' is not registered in the shared event map.");

    private static CivilEngineeringAuditEventDefinition Definition(
        string action,
        AuditOperationKind operation,
        string sourceType,
        string control,
        CivilEngineeringAuditFacet facets = CivilEngineeringAuditFacet.None) =>
        new(action, operation, sourceType, control, RequiredContext | facets);
}

public sealed class CivilEngineeringAuditEventCoverageContributor : IAuditEventCoverageContributor
{
    public IReadOnlyList<AuditEventCoverageDefinitionDto> GetDefinitions() =>
        CivilEngineeringAuditEventMap.Definitions
            .GroupBy(value => new { value.Operation, value.Control })
            .Select(group => new AuditEventCoverageDefinitionDto(
                "Civil Engineering",
                group.Key.Operation,
                group.Key.Control,
                group.Select(value => value.Action).ToArray()))
            .ToArray();
}
