using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public sealed record CivilEngineeringDesignReviewCheck(
    string Id,
    string Name,
    string Description);

/// <summary>
/// Civil Engineering's required design-review checks, hosted by the shared Workflow engine.
/// The IDs are stable control keys; workflow administrators may improve the labels and guidance,
/// but cannot omit or make these checks optional for the configured Civil design route.
/// </summary>
public static class CivilEngineeringDesignReviewChecklistPolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<CivilEngineeringDesignReviewCheck> RequiredChecks { get; } =
    [
        new("civil-design", "Design reviewed", "The engineering design is complete and technically coordinated."),
        new("civil-drawings", "Drawings reviewed", "The current controlled drawings match the reviewed design."),
        new("civil-calculations", "Calculations reviewed", "Supporting engineering calculations are complete and checked."),
        new("civil-specifications", "Specifications reviewed", "Specifications are complete and consistent with the design and drawings."),
        new("civil-dependencies", "Dependencies resolved", "Required cross-section inputs, constraints, and dependencies are resolved."),
    ];

    public static IReadOnlyList<string> Validate(
        WorkflowDefinition definition,
        bool requireHodApproval)
    {
        var errors = new List<string>();
        var approvalSteps = definition.Steps
            .Where(step => !step.IsDeleted && step.StepType == WorkflowStepType.Approval)
            .OrderBy(step => step.Order)
            .ToList();

        if (approvalSteps.Count == 0)
        {
            return ["The selected Civil design workflow requires an SCE approval step."];
        }

        var sceStep = approvalSteps[0];
        if (!HasApproverRole(sceStep, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole))
        {
            errors.Add($"The first Civil design approval step must be assigned to {CivilEngineeringAccessControlRegistry.SupervisingEngineerRole}.");
        }

        var configuredChecks = ReadConfiguration(sceStep)?.QualityConfig?.QualityChecks?
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToList() ?? [];
        foreach (var duplicate in configuredChecks
                     .GroupBy(item => item.Id!.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"The SCE approval checklist control '{duplicate.Key}' is configured more than once.");
        }
        var checklist = configuredChecks
            .GroupBy(item => item.Id!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var required in RequiredChecks)
        {
            if (!checklist.TryGetValue(required.Id, out var configured))
            {
                errors.Add($"The SCE approval checklist is missing required control '{required.Id}' ({required.Name}).");
            }
            else if (!configured.IsRequired)
            {
                errors.Add($"The SCE approval checklist control '{required.Id}' must be required.");
            }
        }

        if (requireHodApproval)
        {
            if (approvalSteps.Count < 2)
            {
                errors.Add("The selected Civil design workflow requires a HOD approval step after SCE review.");
            }
            else if (!approvalSteps.Skip(1).Any(step =>
                         HasApproverRole(step, CivilEngineeringAccessControlRegistry.HeadRole)))
            {
                errors.Add($"A Civil design approval step after SCE review must be assigned to {CivilEngineeringAccessControlRegistry.HeadRole}.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateCurrentResponses(
        IReadOnlyCollection<WorkflowApprovalChecklistResponseDto> responses,
        Guid reviewerUserId,
        DateTime evidenceNotBeforeUtc)
    {
        var errors = new List<string>();
        var lookup = responses
            .Where(response => !string.IsNullOrWhiteSpace(response.Id))
            .GroupBy(response => response.Id!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        foreach (var required in RequiredChecks)
        {
            if (!lookup.TryGetValue(required.Id, out var response)
                || !response.IsSatisfied
                || response.CompletedById != reviewerUserId
                || !response.CompletedAt.HasValue
                || response.CompletedAt.Value.ToUniversalTime() < evidenceNotBeforeUtc)
            {
                errors.Add($"Complete '{required.Name}' against the current design evidence before submitting the package to HOD.");
            }
        }

        return errors;
    }

    private static bool HasApproverRole(WorkflowStep step, string role)
    {
        if (string.Equals(step.RequiredRole?.Trim(), role, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ReadConfiguration(step)?.ApprovalConfig?.ApproverRules?.Any(rule =>
            rule.AssignmentType == WorkflowAssignmentType.Role
            && string.Equals(rule.Role?.Trim(), role, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static WorkflowStepConfigurationDto? ReadConfiguration(WorkflowStep step)
    {
        if (string.IsNullOrWhiteSpace(step.Configuration))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(step.Configuration, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
