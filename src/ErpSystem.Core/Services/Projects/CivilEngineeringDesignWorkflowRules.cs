namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringDesignStages
{
    public const string DraftDirective = "DraftDirective";
    public const string SceInformationGathering = "SceInformationGathering";
    public const string CivilEngineerDesign = "CivilEngineerDesign";
    public const string SceDesignReview = "SceDesignReview";
    public const string Drafting = "Drafting";
    public const string SceDrawingReview = "SceDrawingReview";
    public const string HodFinalReview = "HodFinalReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class CivilEngineeringDesignEvidenceTypes
{
    public const string Directive = "Directive";
    public const string Reconnaissance = "Reconnaissance";
    public const string Design = "Design";
    public const string Drawing = "Drawing";
    public const string SubmissionPackage = "SubmissionPackage";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Directive,
        Reconnaissance,
        Design,
        Drawing,
        SubmissionPackage
    };
}

public enum CivilEngineeringDesignAction
{
    DirectToSce = 0,
    AssignCivilEngineer = 1,
    SubmitDesign = 2,
    ReturnDesign = 3,
    AssignDraftsman = 4,
    SubmitDrawings = 5,
    ReturnDrawings = 6,
    SubmitPackage = 7,
    ReturnPackage = 8,
    Approve = 9,
    Reject = 10,
    Cancel = 11
}

public enum CivilEngineeringDesignActor
{
    Hod = 0,
    SupervisingCivilEngineer = 1,
    CivilEngineer = 2,
    Draftsman = 3
}

public sealed record CivilEngineeringDesignTransitionDefinition(
    string FromStage,
    CivilEngineeringDesignAction Action,
    string ToStage,
    CivilEngineeringDesignActor Actor,
    string? RequiredEvidenceType = null,
    string? AssigneeRole = null,
    bool RequiresReason = false,
    bool StartsSharedWorkflow = false,
    bool AdvancesSharedWorkflow = false,
    bool CompletesSharedWorkflow = false);

public static class CivilEngineeringDesignWorkflowRules
{
    public static IReadOnlyList<CivilEngineeringDesignTransitionDefinition> Transitions { get; } =
    [
        new(CivilEngineeringDesignStages.DraftDirective, CivilEngineeringDesignAction.DirectToSce,
            CivilEngineeringDesignStages.SceInformationGathering, CivilEngineeringDesignActor.Hod,
            CivilEngineeringDesignEvidenceTypes.Directive,
            RequiresReason: true),
        new(CivilEngineeringDesignStages.SceInformationGathering, CivilEngineeringDesignAction.AssignCivilEngineer,
            CivilEngineeringDesignStages.CivilEngineerDesign, CivilEngineeringDesignActor.SupervisingCivilEngineer,
            CivilEngineeringDesignEvidenceTypes.Reconnaissance,
            CivilEngineeringAccessControlRegistry.CivilEngineerRole,
            RequiresReason: true),
        new(CivilEngineeringDesignStages.CivilEngineerDesign, CivilEngineeringDesignAction.SubmitDesign,
            CivilEngineeringDesignStages.SceDesignReview, CivilEngineeringDesignActor.CivilEngineer,
            CivilEngineeringDesignEvidenceTypes.Design,
            StartsSharedWorkflow: true),
        new(CivilEngineeringDesignStages.SceDesignReview, CivilEngineeringDesignAction.ReturnDesign,
            CivilEngineeringDesignStages.CivilEngineerDesign, CivilEngineeringDesignActor.SupervisingCivilEngineer,
            RequiresReason: true),
        new(CivilEngineeringDesignStages.SceDesignReview, CivilEngineeringDesignAction.AssignDraftsman,
            CivilEngineeringDesignStages.Drafting, CivilEngineeringDesignActor.SupervisingCivilEngineer,
            CivilEngineeringDesignEvidenceTypes.Design,
            CivilEngineeringAccessControlRegistry.DraftsmanRole,
            RequiresReason: true),
        new(CivilEngineeringDesignStages.Drafting, CivilEngineeringDesignAction.SubmitDrawings,
            CivilEngineeringDesignStages.SceDrawingReview, CivilEngineeringDesignActor.Draftsman,
            CivilEngineeringDesignEvidenceTypes.Drawing),
        new(CivilEngineeringDesignStages.SceDrawingReview, CivilEngineeringDesignAction.ReturnDrawings,
            CivilEngineeringDesignStages.Drafting, CivilEngineeringDesignActor.SupervisingCivilEngineer,
            RequiresReason: true),
        new(CivilEngineeringDesignStages.SceDrawingReview, CivilEngineeringDesignAction.SubmitPackage,
            CivilEngineeringDesignStages.HodFinalReview, CivilEngineeringDesignActor.SupervisingCivilEngineer,
            CivilEngineeringDesignEvidenceTypes.SubmissionPackage,
            AdvancesSharedWorkflow: true),
        new(CivilEngineeringDesignStages.HodFinalReview, CivilEngineeringDesignAction.Approve,
            CivilEngineeringDesignStages.Approved, CivilEngineeringDesignActor.Hod,
            CompletesSharedWorkflow: true),
        new(CivilEngineeringDesignStages.HodFinalReview, CivilEngineeringDesignAction.Reject,
            CivilEngineeringDesignStages.Rejected, CivilEngineeringDesignActor.Hod,
            RequiresReason: true, CompletesSharedWorkflow: true),
        new(CivilEngineeringDesignStages.DraftDirective, CivilEngineeringDesignAction.Cancel,
            CivilEngineeringDesignStages.Cancelled, CivilEngineeringDesignActor.Hod,
            RequiresReason: true)
    ];

    public static CivilEngineeringDesignTransitionDefinition GetRequired(
        string stage,
        CivilEngineeringDesignAction action)
        => Transitions.SingleOrDefault(value =>
               string.Equals(value.FromStage, stage, StringComparison.Ordinal)
               && value.Action == action)
           ?? throw new InvalidOperationException(
               $"Action {action} is not allowed while the Civil design case is at {stage}.");

    public static void EnsureDistinctAssignments(
        Guid hodUserId,
        Guid supervisingCivilEngineerUserId,
        Guid? civilEngineerUserId,
        Guid? draftsmanUserId)
    {
        var assignments = new[]
            {
                hodUserId,
                supervisingCivilEngineerUserId,
                civilEngineerUserId ?? Guid.Empty,
                draftsmanUserId ?? Guid.Empty
            }
            .Where(value => value != Guid.Empty)
            .ToList();
        if (assignments.Distinct().Count() != assignments.Count)
            throw new InvalidOperationException(
                "HOD, SCE, Civil Engineer and Draftsman assignments must be held by different users.");
    }
}
