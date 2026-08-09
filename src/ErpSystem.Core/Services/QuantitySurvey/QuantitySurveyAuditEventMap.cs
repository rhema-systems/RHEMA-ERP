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
        Definition(RetireEstimateVersion, AuditOperationKind.Update, "QS estimate version retirement", QuantitySurveyAuditFacet.BeforeValues | QuantitySurveyAuditFacet.AfterValues | QuantitySurveyAuditFacet.ApprovalState | QuantitySurveyAuditFacet.Reason)
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
