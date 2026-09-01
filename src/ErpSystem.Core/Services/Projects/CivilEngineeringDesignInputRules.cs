using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public enum CivilEngineeringDesignInputReviewAction
{
    Accept = 0,
    Return = 1
}

public static class CivilEngineeringDesignInputRules
{
    public static readonly IReadOnlySet<string> Priorities = new HashSet<string>(
        [ProjectRfiPriorities.Low, ProjectRfiPriorities.Medium, ProjectRfiPriorities.High, ProjectRfiPriorities.Critical],
        StringComparer.OrdinalIgnoreCase);

    public static void EnsureCanCreate(string designStage, DateTime dueAtUtc, DateTime nowUtc)
    {
        if (designStage != CivilEngineeringDesignStages.SceInformationGathering)
            throw new InvalidOperationException("Cross-section input requests can be created only during SCE information gathering.");
        if (dueAtUtc <= nowUtc)
            throw new InvalidOperationException("Select a future response due date.");
    }

    public static void EnsureCanRespond(string status)
    {
        if (status != ProjectRfiStatuses.Submitted)
            throw new InvalidOperationException("Only a submitted cross-section request can receive a response.");
    }

    public static string ReviewStatus(string status, CivilEngineeringDesignInputReviewAction action)
    {
        if (status != ProjectRfiStatuses.Answered)
            throw new InvalidOperationException("Only an answered cross-section request can be reviewed.");
        return action == CivilEngineeringDesignInputReviewAction.Accept
            ? ProjectRfiStatuses.Closed
            : ProjectRfiStatuses.Submitted;
    }

    public static bool IsReady(bool blocksReadiness, string status)
        => !blocksReadiness || status == ProjectRfiStatuses.Closed;
}
