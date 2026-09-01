namespace ErpSystem.Core.Services.Projects;

/// <summary>Pure lifecycle hard-stops for the Civil control envelope around a Project site instruction.</summary>
public static class CivilEngineeringSiteInstructionRoutingPolicy
{
    public const string ContractorAcknowledged = "ContractorAcknowledged";
    public const string ContractorResponded = "ContractorResponded";
    public const string EngineeringResponseAccepted = "EngineeringResponseAccepted";
    public const string EngineeringResponseReturned = "EngineeringResponseReturned";
    public const string EngineeringFollowUp = "EngineeringFollowUp";
    public const string InstructionClosed = "InstructionClosed";
    public const string ProjectManagerReviewed = "ProjectManagerReviewed";

    public static IReadOnlyList<string> ValidateIssue(
        Guid clientRequestId,
        Guid projectEngineerAssignmentId,
        Guid centralDocumentVersionId,
        string? title,
        string? description)
    {
        var errors = new List<string>();
        if (clientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (projectEngineerAssignmentId == Guid.Empty) errors.Add("An active Project Engineer appointment is required.");
        if (centralDocumentVersionId == Guid.Empty) errors.Add("Select a current published central-DMS drawing or instruction evidence version.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 3) errors.Add("Instruction title must contain at least 3 characters.");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 10) errors.Add("Instruction narrative must contain at least 10 characters.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateContractorAction(string? action, string? message, bool hasDmsEvidence)
    {
        var errors = new List<string>();
        if (action is not (ContractorAcknowledged or ContractorResponded)) errors.Add("Select ContractorAcknowledged or ContractorResponded.");
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length < 3) errors.Add("Provide an acknowledgement or response message.");
        if (action == ContractorResponded && !hasDmsEvidence) errors.Add("A contractor response must reference a current published central-DMS document version.");
        return errors;
    }

    public static bool CanReceiveContractorResponse(string status, string approvalStatus)
        => status is "AwaitingContractorAcknowledgement" or "ContractorResponded"
           && string.Equals(approvalStatus, "Approved", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ValidateEngineeringReview(bool approve, string? reason, bool hasDmsEvidence)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
            errors.Add("Provide an engineering review reason.");
        if (!hasDmsEvidence)
            errors.Add("Select current Published central-DMS engineering-review evidence.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateFollowUp(string? action, string? reason, bool hasDmsEvidence)
    {
        var errors = new List<string>();
        if (action is not (EngineeringFollowUp or InstructionClosed))
            errors.Add("Select EngineeringFollowUp or InstructionClosed.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
            errors.Add("Provide a follow-up or closure reason.");
        if (!hasDmsEvidence)
            errors.Add("Select current Published central-DMS follow-up or closure evidence.");
        return errors;
    }

    public static bool CanEngineeringReview(string status, string approvalStatus)
        => status == "AwaitingEngineeringReview"
           && string.Equals(approvalStatus, "Approved", StringComparison.OrdinalIgnoreCase);

    public static bool CanFollowUp(string status, string approvalStatus)
        => status == "AwaitingEngineeringFollowUp"
           && string.Equals(approvalStatus, "Approved", StringComparison.OrdinalIgnoreCase);
}
