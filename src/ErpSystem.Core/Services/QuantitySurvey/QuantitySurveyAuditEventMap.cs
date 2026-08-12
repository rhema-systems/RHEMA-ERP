using ErpSystem.Core.DTOs.Compliance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.Audit;
using ErpSystem.Core.Services.Audit;

namespace ErpSystem.Core.Services.QuantitySurvey;

[Flags]
public enum QuantitySurveyAuditFacet
{
    None = 0,
    Actor = 1,
    ActorRoles = 2,
    Source = 4,
    BeforeValues = 8,
    AfterValues = 16,
    FormulaInputs = 32,
    EvidenceLinks = 64,
    ApprovalState = 128,
    Reason = 256,
    CorrelationId = 512
}

public sealed record QuantitySurveyAuditEventDefinition(
    string Action,
    AuditOperationKind Operation,
    string Control,
    QuantitySurveyAuditFacet RequiredFacets);

/// <summary>
/// Canonical event names and required audit payloads for the QS configuration lifecycle.
/// Future QS services extend this register instead of creating module-local audit conventions.
/// </summary>
public static class QuantitySurveyAuditEventMap
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
    public const string CreateBoqVersion = "CreateBoqVersion";
    public const string SubmitBoqVersion = "SubmitBoqVersion";
    public const string ApproveBoqVersion = "ApproveBoqVersion";
    public const string RejectBoqVersion = "RejectBoqVersion";
    public const string RecallBoqVersion = "RecallBoqVersion";
    public const string PublishBoqVersion = "PublishBoqVersion";
    public const string RetireBoqPublication = "RetireBoqPublication";
    public const string StageTenderBoqSubmission = "StageTenderBoqSubmission";
    public const string CommitTenderBoqSubmission = "CommitTenderBoqSubmission";
    public const string AcceptTenderBoqSubmission = "AcceptTenderBoqSubmission";
    public const string RejectTenderBoqSubmission = "RejectTenderBoqSubmission";
    public const string CreateRateLibraryItem = "CreateRateLibraryItem";
    public const string UpdateRateLibraryItem = "UpdateRateLibraryItem";
    public const string CreateRateDraft = "CreateRateDraft";
    public const string UpdateRateDraft = "UpdateRateDraft";
    public const string PublishRate = "PublishRate";
    public const string RetireRate = "RetireRate";
    public const string CreateMarketSurveyUpdate = "CreateMarketSurveyRate";
    public const string PromoteHistoricalRate = "CreateHistoricalRatePromotion";
    public const string CreateRateBuildUp = "CreateRateBuildUp";
    public const string CreateEstimateVersion = "CreateEstimateVersion";
    public const string SubmitEstimateVersion = "SubmitEstimateVersion";
    public const string ApproveEstimateVersion = "ApproveEstimateVersion";
    public const string RejectEstimateVersion = "RejectEstimateVersion";
    public const string RetireEstimateVersion = "RetireEstimateVersion";
    public const string CreatePriceIndexFamily = "CreatePriceIndexFamily";
    public const string UpdatePriceIndexFamily = "UpdatePriceIndexFamily";
    public const string CreateEscalationFormula = "CreateEscalationFormula";
    public const string UpdateEscalationFormula = "UpdateEscalationFormula";
    public const string SubmitEscalationFormula = "SubmitEscalationFormula";
    public const string ApproveEscalationFormula = "ApproveEscalationFormula";
    public const string RejectEscalationFormula = "RejectEscalationFormula";
    public const string RetireEscalationFormula = "RetireEscalationFormula";
    public const string StagePriceIndexImport = "StagePriceIndexImport";
    public const string SubmitPriceIndexImport = "SubmitPriceIndexImport";
    public const string ApprovePriceIndexImport = "ApprovePriceIndexImport";
    public const string RejectPriceIndexImport = "RejectPriceIndexImport";
    public const string CalculateEscalationRun = "CalculateEscalationRun";
    public const string SubmitEscalationRun = "SubmitEscalationRun";
    public const string ReviewEscalationRun = "ReviewEscalationRun";
    public const string ApproveEscalationRun = "ApproveEscalationRun";
    public const string RejectEscalationRun = "RejectEscalationRun";
    public const string ApplyEscalationToFinalAccount = "ApplyEscalationToFinalAccount";
    public const string OpenEscalationDispute = "OpenEscalationDispute";
    public const string RecordEscalationContractorResponse = "RecordEscalationContractorResponse";
    public const string AttachEscalationDisputeEvidence = "AttachEscalationDisputeEvidence";
    public const string ResolveEscalationDispute = "ResolveEscalationDispute";
    public const string ExportEscalationDisputeAuditPack = "ExportEscalationDisputeAuditPack";
    public const string CreateMeasurementSheet = "CreateMeasurementSheet";
    public const string UpdateMeasurementSheet = "UpdateMeasurementSheet";
    public const string AttachMeasurementEvidence = "AttachMeasurementEvidence";
    public const string RecordMeasurementSheet = "RecordMeasurementSheet";
    public const string CreateRemeasurementVersion = "CreateRemeasurementVersion";
    public const string CreateJointMeasurementRequest = "CreateJointMeasurementRequest";
    public const string SubmitJointMeasurementRequest = "SubmitJointMeasurementRequest";
    public const string ScheduleJointMeasurement = "ScheduleJointMeasurement";
    public const string LinkJointMeasurementSheet = "LinkJointMeasurementSheet";
    public const string RecordJointMeasurementAttendance = "RecordJointMeasurementAttendance";
    public const string EndorseJointMeasurement = "EndorseJointMeasurement";
    public const string SubmitJointMeasurementApproval = "SubmitJointMeasurementApproval";
    public const string ApproveJointMeasurement = "ApproveJointMeasurement";
    public const string RejectJointMeasurement = "RejectJointMeasurement";
    public const string AttachJointMeasurementEvidence = "AttachJointMeasurementEvidence";
    public const string CreateJointMeasurementBoqRevision = "CreateJointMeasurementBoqRevision";
    public const string ApplyJointMeasurementBoqRevision = "ApplyJointMeasurementBoqRevision";
    public const string CreateDesignRevisionImpact = "CreateDesignRevisionImpact";
    public const string SubmitDesignRevisionImpact = "SubmitDesignRevisionImpact";
    public const string ApproveDesignRevisionImpact = "ApproveDesignRevisionImpact";
    public const string RejectDesignRevisionImpact = "RejectDesignRevisionImpact";
    public const string CreateValuationWorksheet = "CreateValuationWorksheet";
    public const string UpdateValuationWorksheet = "UpdateValuationWorksheet";
    public const string AttachValuationEvidence = "AttachValuationEvidence";
    public const string SubmitContractorValuation = "SubmitContractorValuation";
    public const string VetValuation = "VetValuation";
    public const string EndorseValuation = "EndorseValuation";
    public const string SubmitValuationApproval = "SubmitValuationApproval";
    public const string ApproveValuation = "ApproveValuation";
    public const string RejectValuation = "RejectValuation";
    public const string GeneratePaymentCertificate = "GeneratePaymentCertificate";
    public const string UpdatePaymentCertificate = "UpdatePaymentCertificate";
    public const string SubmitPaymentCertificate = "SubmitPaymentCertificate";
    public const string ApprovePaymentCertificate = "ApprovePaymentCertificate";
    public const string RejectPaymentCertificate = "RejectPaymentCertificate";
    public const string HandoffPaymentCertificateToAp = "HandoffPaymentCertificateToAp";
    public const string RefreshPaymentCertificateStatus = "RefreshPaymentCertificateStatus";
    public const string IssuePaymentCertificateDocument = "IssuePaymentCertificateDocument";
    public const string CreateAdvanceRecoveryAgreement = "CreateAdvanceRecoveryAgreement";
    public const string SubmitAdvanceRecoveryAgreement = "SubmitAdvanceRecoveryAgreement";
    public const string ApproveAdvanceRecoveryAgreement = "ApproveAdvanceRecoveryAgreement";
    public const string RejectAdvanceRecoveryAgreement = "RejectAdvanceRecoveryAgreement";
    public const string PrepareFinalAccount = "PrepareFinalAccount";
    public const string SubmitFinalAccount = "SubmitFinalAccount";
    public const string ApproveFinalAccount = "ApproveFinalAccount";
    public const string RejectFinalAccount = "RejectFinalAccount";
    public const string CloseFinalAccount = "CloseFinalAccount";
    public const string PrepareMaterialReconciliation = "PrepareMaterialReconciliation";
    public const string ConfirmMaterialReconciliation = "ConfirmMaterialReconciliation";
    public const string SubmitMaterialReconciliation = "SubmitMaterialReconciliation";
    public const string ApproveMaterialReconciliation = "ApproveMaterialReconciliation";
    public const string RejectMaterialReconciliation = "RejectMaterialReconciliation";
    public const string CreateContractClaim = "CreateContractClaim";
    public const string UpdateContractClaim = "UpdateContractClaim";
    public const string AttachContractClaimEvidence = "AttachContractClaimEvidence";
    public const string SubmitContractClaim = "SubmitContractClaim";
    public const string VetContractClaim = "VetContractClaim";
    public const string SubmitContractClaimApproval = "SubmitContractClaimApproval";
    public const string ApproveContractClaim = "ApproveContractClaim";
    public const string RejectContractClaim = "RejectContractClaim";
    public const string OpenContractClaimDispute = "OpenContractClaimDispute";
    public const string ResolveContractClaimDispute = "ResolveContractClaimDispute";
    public const string SettleContractClaim = "SettleContractClaim";
    public const string CreateDayworkSheet = "CreateDayworkSheet";
    public const string UpdateDayworkSheet = "UpdateDayworkSheet";
    public const string AttachDayworkEvidence = "AttachDayworkEvidence";
    public const string SignDayworkSheet = "SignDayworkSheet";
    public const string VerifyDayworkSheet = "VerifyDayworkSheet";
    public const string RejectDayworkSheet = "RejectDayworkSheet";
    public const string ApplyApprovedVariation = "ApplyApprovedVariation";
    public const string ConfigureContractCommercialTerms = "ConfigureContractCommercialTerms";
    public const string CreateSubcontract = "CreateSubcontract";
    public const string UpdateSubcontract = "UpdateSubcontract";
    public const string AttachSubcontractEvidence = "AttachSubcontractEvidence";
    public const string SubmitSubcontract = "SubmitSubcontract";
    public const string ApproveSubcontract = "ApproveSubcontract";
    public const string RejectSubcontract = "RejectSubcontract";
    public const string CreateSubcontractValuation = "CreateSubcontractValuation";
    public const string UpdateSubcontractValuation = "UpdateSubcontractValuation";
    public const string SubmitSubcontractValuation = "SubmitSubcontractValuation";
    public const string AssessSubcontractValuation = "AssessSubcontractValuation";
    public const string ApproveSubcontractValuation = "ApproveSubcontractValuation";
    public const string RejectSubcontractValuation = "RejectSubcontractValuation";
    public const string HandoffSubcontractCertificateToAp = "HandoffSubcontractCertificateToAp";
    public const string RefreshSubcontractPaymentStatus = "RefreshSubcontractPaymentStatus";
    public const string CloseSubcontractFinalAccount = "CloseSubcontractFinalAccount";
    public const string CreateSubcontractCharge = "CreateSubcontractCharge";
    public const string UpdateSubcontractCharge = "UpdateSubcontractCharge";
    public const string AttachSubcontractChargeEvidence = "AttachSubcontractChargeEvidence";
    public const string IssueSubcontractCharge = "IssueSubcontractCharge";
    public const string RespondSubcontractCharge = "RespondSubcontractCharge";
    public const string SubmitSubcontractCharge = "SubmitSubcontractCharge";
    public const string ApproveSubcontractCharge = "ApproveSubcontractCharge";
    public const string RejectSubcontractCharge = "RejectSubcontractCharge";
    public const string CommunicateSubcontractCharge = "CommunicateSubcontractCharge";
    public const string AllocateSubcontractCharge = "AllocateSubcontractCharge";
    public const string ReleaseSubcontractChargeAllocation = "ReleaseSubcontractChargeAllocation";
    public const string ApplySubcontractCharge = "ApplySubcontractCharge";

    private const QuantitySurveyAuditFacet Context =
        QuantitySurveyAuditFacet.Actor |
        QuantitySurveyAuditFacet.ActorRoles |
        QuantitySurveyAuditFacet.Source |
        QuantitySurveyAuditFacet.CorrelationId;

    private static readonly QuantitySurveyAuditEventDefinition[] Items =
    [
        Definition(CreateProfile, AuditOperationKind.Create, "Configuration profile creation", QuantitySurveyAuditFacet.AfterValues),
        Definition(UpdateProfile, AuditOperationKind.Update, "Controlled profile amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.Reason),
        Definition(SaveDecision, AuditOperationKind.Update, "Typed configuration decision amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitDecision, AuditOperationKind.Create, "Decision submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState),
        Definition(ApproveDecision, AuditOperationKind.Approve, "Independent decision approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectDecision, AuditOperationKind.Reject, "Independent decision rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(LinkEvidence, AuditOperationKind.Update, "Central-DMS evidence link", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(UnlinkEvidence, AuditOperationKind.Update, "Central-DMS evidence unlink", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(PublishProfile, AuditOperationKind.Approve, "Configuration publication", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RetireProfile, AuditOperationKind.Update, "Configuration retirement", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CloneDraft, AuditOperationKind.Create, "Versioned configuration clone", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues),
        Definition(DeleteDraft, AuditOperationKind.Update, "Controlled draft deletion", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.Reason),
        Definition(SeedDraft, AuditOperationKind.Create, "Idempotent tenant seed", QuantitySurveyAuditFacet.AfterValues),
        Definition(CreateBoqVersion, AuditOperationKind.Create, "Immutable BoQ version snapshot", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitBoqVersion, AuditOperationKind.Create, "BoQ workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState),
        Definition(ApproveBoqVersion, AuditOperationKind.Approve, "BoQ workflow approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectBoqVersion, AuditOperationKind.Reject, "BoQ workflow rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RecallBoqVersion, AuditOperationKind.Update, "BoQ workflow recall", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(PublishBoqVersion, AuditOperationKind.Approve, "Immutable approved BoQ publication", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RetireBoqPublication, AuditOperationKind.Update, "Approved BoQ publication retirement", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(StageTenderBoqSubmission, AuditOperationKind.Create, "Tenderer BoQ protected staging", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks),
        Definition(CommitTenderBoqSubmission, AuditOperationKind.Update, "Tenderer BoQ signed reconciliation", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(AcceptTenderBoqSubmission, AuditOperationKind.Approve, "QS tenderer BoQ vetting acceptance", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectTenderBoqSubmission, AuditOperationKind.Reject, "QS tenderer BoQ vetting rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateRateLibraryItem, AuditOperationKind.Create, "QS rate-library item creation", QuantitySurveyAuditFacet.AfterValues),
        Definition(UpdateRateLibraryItem, AuditOperationKind.Update, "QS rate-library item amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.Reason),
        Definition(CreateRateDraft, AuditOperationKind.Create, "QS effective-dated rate preparation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(UpdateRateDraft, AuditOperationKind.Update, "QS draft-rate amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(PublishRate, AuditOperationKind.Approve, "Independent QS rate publication", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RetireRate, AuditOperationKind.Update, "Controlled QS rate retirement", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateMarketSurveyUpdate, AuditOperationKind.Create, "Evidence-backed QS market-survey rate preparation", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(PromoteHistoricalRate, AuditOperationKind.Create, "Historical project-cost promotion into a governed QS rate draft", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateRateBuildUp, AuditOperationKind.Create, "Policy-controlled QS rate build-up calculation", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateEstimateVersion, AuditOperationKind.Create, "Immutable QS estimate version snapshot", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitEstimateVersion, AuditOperationKind.Create, "QS estimate workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState),
        Definition(ApproveEstimateVersion, AuditOperationKind.Approve, "QS estimate workflow approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectEstimateVersion, AuditOperationKind.Reject, "QS estimate workflow rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RetireEstimateVersion, AuditOperationKind.Update, "QS estimate version retirement", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreatePriceIndexFamily, AuditOperationKind.Create, "Controlled QS price-index family creation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.Reason),
        Definition(UpdatePriceIndexFamily, AuditOperationKind.Update, "Controlled QS price-index family amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.Reason),
        Definition(CreateEscalationFormula, AuditOperationKind.Create, "Contract-linked QS escalation formula creation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(UpdateEscalationFormula, AuditOperationKind.Update, "Draft QS escalation formula amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitEscalationFormula, AuditOperationKind.Create, "QS escalation workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveEscalationFormula, AuditOperationKind.Approve, "Independent QS escalation formula approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectEscalationFormula, AuditOperationKind.Reject, "QS escalation formula rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RetireEscalationFormula, AuditOperationKind.Update, "Controlled QS escalation formula retirement", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(StagePriceIndexImport, AuditOperationKind.Create, "Validated QS price-index source staging", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitPriceIndexImport, AuditOperationKind.Create, "QS price-index import workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApprovePriceIndexImport, AuditOperationKind.Approve, "Independent QS price-index publication", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectPriceIndexImport, AuditOperationKind.Reject, "QS price-index import rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CalculateEscalationRun, AuditOperationKind.Create, "Controlled QS escalation calculation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitEscalationRun, AuditOperationKind.Create, "QS escalation calculation workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ReviewEscalationRun, AuditOperationKind.Update, "Independent QS escalation calculation review", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveEscalationRun, AuditOperationKind.Approve, "Independent QS escalation calculation approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectEscalationRun, AuditOperationKind.Reject, "QS escalation calculation rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApplyEscalationToFinalAccount, AuditOperationKind.Approve, "Approved escalation application to governed final-account reconciliation", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(OpenEscalationDispute, AuditOperationKind.Create, "QS escalation dispute opening", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RecordEscalationContractorResponse, AuditOperationKind.Update, "Contractor escalation response", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(AttachEscalationDisputeEvidence, AuditOperationKind.Create, "Central-DMS escalation dispute evidence", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(ResolveEscalationDispute, AuditOperationKind.Approve, "Independent escalation dispute resolution", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ExportEscalationDisputeAuditPack, AuditOperationKind.Other, "Escalation dispute audit-pack export", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks),
        Definition(CreateMeasurementSheet, AuditOperationKind.Create, "QS taking-off sheet creation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState),
        Definition(UpdateMeasurementSheet, AuditOperationKind.Update, "Draft QS taking-off sheet amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(AttachMeasurementEvidence, AuditOperationKind.Create, "Central-DMS taking-off evidence", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(RecordMeasurementSheet, AuditOperationKind.Approve, "Immutable QS measurement recording", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateRemeasurementVersion, AuditOperationKind.Create, "Measurement-derived BoQ remeasurement revision", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateJointMeasurementRequest, AuditOperationKind.Create, "Contractor joint-remeasurement request", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitJointMeasurementRequest, AuditOperationKind.Create, "Contractor joint-remeasurement submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ScheduleJointMeasurement, AuditOperationKind.Update, "Joint-measurement schedule and governed participant access", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(LinkJointMeasurementSheet, AuditOperationKind.Update, "Recorded measurement lineage link", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(RecordJointMeasurementAttendance, AuditOperationKind.Update, "Joint-measurement participant attendance", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(EndorseJointMeasurement, AuditOperationKind.Approve, "Joint-measurement digital endorsement", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitJointMeasurementApproval, AuditOperationKind.Create, "Joint-measurement workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveJointMeasurement, AuditOperationKind.Approve, "Independent joint-measurement approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectJointMeasurement, AuditOperationKind.Reject, "Independent joint-measurement rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(AttachJointMeasurementEvidence, AuditOperationKind.Create, "Central-DMS joint-measurement evidence", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(CreateJointMeasurementBoqRevision, AuditOperationKind.Create, "Approved joint-measurement BoQ revision handoff", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApplyJointMeasurementBoqRevision, AuditOperationKind.Approve, "Approved joint-measurement working-BoQ application", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateDesignRevisionImpact, AuditOperationKind.Create, "Drawing-revision to approved-BoQ impact registration", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitDesignRevisionImpact, AuditOperationKind.Create, "Design-impact measurement or variation workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveDesignRevisionImpact, AuditOperationKind.Approve, "Independent design-impact routing approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectDesignRevisionImpact, AuditOperationKind.Reject, "Independent design-impact routing rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateValuationWorksheet, AuditOperationKind.Create, "BoQ line valuation worksheet creation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState),
        Definition(UpdateValuationWorksheet, AuditOperationKind.Update, "Draft BoQ line valuation worksheet amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(AttachValuationEvidence, AuditOperationKind.Create, "Central-DMS valuation evidence", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitContractorValuation, AuditOperationKind.Create, "Contractor interim-valuation submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(VetValuation, AuditOperationKind.Update, "QS interim-valuation vetting", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(EndorseValuation, AuditOperationKind.Approve, "Consultant interim-valuation endorsement", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitValuationApproval, AuditOperationKind.Create, "Interim-valuation workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveValuation, AuditOperationKind.Approve, "Independent interim-valuation approval and certificate readiness", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectValuation, AuditOperationKind.Reject, "Independent interim-valuation rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(GeneratePaymentCertificate, AuditOperationKind.Create, "Approved valuation to payment-certificate generation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(UpdatePaymentCertificate, AuditOperationKind.Update, "Draft payment-certificate deductions amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitPaymentCertificate, AuditOperationKind.Create, "Payment-certificate workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApprovePaymentCertificate, AuditOperationKind.Approve, "Independent payment-certificate approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectPaymentCertificate, AuditOperationKind.Reject, "Independent payment-certificate rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(HandoffPaymentCertificateToAp, AuditOperationKind.Create, "Approved certificate handoff to Finance AP", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RefreshPaymentCertificateStatus, AuditOperationKind.Update, "Finance-owned invoice and payment status refresh", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState),
        Definition(IssuePaymentCertificateDocument, AuditOperationKind.Dispatch, "Central-DMS payment certificate issue", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState),
        Definition(CreateAdvanceRecoveryAgreement, AuditOperationKind.Create, "Posted Finance supplier-advance recovery preparation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitAdvanceRecoveryAgreement, AuditOperationKind.Create, "Advance recovery submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveAdvanceRecoveryAgreement, AuditOperationKind.Approve, "Independent advance recovery approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectAdvanceRecoveryAgreement, AuditOperationKind.Reject, "Independent advance recovery rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(PrepareFinalAccount, AuditOperationKind.Create, "Project final-account reconciliation preparation", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitFinalAccount, AuditOperationKind.Create, "Final-account workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveFinalAccount, AuditOperationKind.Approve, "Independent final-account approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectFinalAccount, AuditOperationKind.Reject, "Independent final-account rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CloseFinalAccount, AuditOperationKind.Approve, "Settled final-account closure", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(PrepareMaterialReconciliation, AuditOperationKind.Create, "Governed material valuation and inventory reconciliation preparation", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(ConfirmMaterialReconciliation, AuditOperationKind.Update, "Contractor material reconciliation confirmation", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitMaterialReconciliation, AuditOperationKind.Create, "Material reconciliation workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveMaterialReconciliation, AuditOperationKind.Approve, "Independent material reconciliation approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectMaterialReconciliation, AuditOperationKind.Reject, "Independent material reconciliation rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateContractClaim, AuditOperationKind.Create, "Contractor formal claim preparation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(UpdateContractClaim, AuditOperationKind.Update, "Contractor formal claim amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(AttachContractClaimEvidence, AuditOperationKind.Create, "Central-DMS contractor claim evidence", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitContractClaim, AuditOperationKind.Create, "Contractor formal claim submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(VetContractClaim, AuditOperationKind.Update, "QS contractor claim vetting", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitContractClaimApproval, AuditOperationKind.Create, "Contractor claim workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveContractClaim, AuditOperationKind.Approve, "Independent contractor claim approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectContractClaim, AuditOperationKind.Reject, "Independent contractor claim rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(OpenContractClaimDispute, AuditOperationKind.Create, "Contractor claim dispute opening", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ResolveContractClaimDispute, AuditOperationKind.Approve, "Independent contractor claim dispute resolution", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(SettleContractClaim, AuditOperationKind.Update, "Contractor claim settlement tracking", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateDayworkSheet, AuditOperationKind.Create, "Contractor daywork sheet preparation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(UpdateDayworkSheet, AuditOperationKind.Update, "Contractor daywork sheet amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(AttachDayworkEvidence, AuditOperationKind.Create, "Central-DMS daywork evidence", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(SignDayworkSheet, AuditOperationKind.Create, "Authenticated contractor daywork signature", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(VerifyDayworkSheet, AuditOperationKind.Approve, "Independent QS daywork verification", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectDayworkSheet, AuditOperationKind.Reject, "Independent QS daywork rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApplyApprovedVariation, AuditOperationKind.Approve, "Atomic approved-variation downstream application", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ConfigureContractCommercialTerms, AuditOperationKind.Update, "Canonical Works-contract commercial terms configuration", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.FormulaInputs),
        Definition(CreateSubcontract, AuditOperationKind.Create, "Governed subcontract register creation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs),
        Definition(UpdateSubcontract, AuditOperationKind.Update, "Governed subcontract amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(AttachSubcontractEvidence, AuditOperationKind.Create, "Central-DMS subcontract evidence", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks),
        Definition(SubmitSubcontract, AuditOperationKind.Create, "Subcontract workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveSubcontract, AuditOperationKind.Approve, "Independent subcontract approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectSubcontract, AuditOperationKind.Reject, "Subcontract rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CreateSubcontractValuation, AuditOperationKind.Create, "Subcontractor valuation creation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs),
        Definition(UpdateSubcontractValuation, AuditOperationKind.Update, "Subcontractor valuation amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitSubcontractValuation, AuditOperationKind.Create, "Subcontractor valuation submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(AssessSubcontractValuation, AuditOperationKind.Update, "Independent QS subcontract valuation assessment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveSubcontractValuation, AuditOperationKind.Approve, "Subcontract certificate approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectSubcontractValuation, AuditOperationKind.Reject, "Subcontract valuation rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(HandoffSubcontractCertificateToAp, AuditOperationKind.Create, "Subcontract certificate Finance AP handoff", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(RefreshSubcontractPaymentStatus, AuditOperationKind.Update, "Finance-owned subcontract payment refresh", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues),
        Definition(CloseSubcontractFinalAccount, AuditOperationKind.Approve, "Subcontract final settlement closure", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(CreateSubcontractCharge, AuditOperationKind.Create, "Governed subcontract charge preparation", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(UpdateSubcontractCharge, AuditOperationKind.Update, "Governed subcontract charge amendment", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.Reason),
        Definition(AttachSubcontractChargeEvidence, AuditOperationKind.Create, "Central-DMS subcontract charge evidence", QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks),
        Definition(IssueSubcontractCharge, AuditOperationKind.Dispatch, "Subcontractor charge-notice issue", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(RespondSubcontractCharge, AuditOperationKind.Update, "Authenticated subcontractor charge response", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.Reason),
        Definition(SubmitSubcontractCharge, AuditOperationKind.Create, "Subcontract charge workflow submission", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(ApproveSubcontractCharge, AuditOperationKind.Approve, "Independent subcontract charge approval", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(RejectSubcontractCharge, AuditOperationKind.Reject, "Independent subcontract charge rejection", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.EvidenceLinks | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason),
        Definition(CommunicateSubcontractCharge, AuditOperationKind.Update, "Durable subcontract charge communication", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues),
        Definition(AllocateSubcontractCharge, AuditOperationKind.Update, "Approved charge allocation to valuation", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs),
        Definition(ReleaseSubcontractChargeAllocation, AuditOperationKind.Update, "Rejected valuation charge release", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.Reason),
        Definition(ApplySubcontractCharge, AuditOperationKind.Approve, "Approved charge application to certificate", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.FormulaInputs)
    ];

    private static readonly IReadOnlyDictionary<string, QuantitySurveyAuditEventDefinition> ByAction =
        Items.ToDictionary(value => value.Action, StringComparer.Ordinal);

    public static IReadOnlyList<QuantitySurveyAuditEventDefinition> Definitions => Items;

    public static QuantitySurveyAuditEventDefinition GetRequired(string action)
        => ByAction.TryGetValue(action, out var definition)
            ? definition
            : throw new InvalidOperationException($"QS audit action '{action}' is not registered in the shared event map.");

    private static QuantitySurveyAuditEventDefinition Definition(
        string action,
        AuditOperationKind operation,
        string control,
        QuantitySurveyAuditFacet facets)
        => new(action, operation, control, Context | facets);
}

public sealed class QuantitySurveyAuditEventCoverageContributor : IAuditEventCoverageContributor
{
    public IReadOnlyList<AuditEventCoverageDefinitionDto> GetDefinitions()
        => QuantitySurveyAuditEventMap.Definitions
            .GroupBy(value => new { value.Operation, value.Control })
            .Select(group => new AuditEventCoverageDefinitionDto(
                "Quantity Survey",
                group.Key.Operation,
                group.Key.Control,
                group.Select(value => value.Action).ToArray()))
            .ToArray();
}
