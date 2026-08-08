using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyConfigurationLifecyclePolicy
{
    public static void EnsureEditable(QuantitySurveyConfigurationProfile profile)
    {
        if (profile.LifecycleStatus != QuantitySurveyConfigurationProfileStatus.Draft)
            throw new QuantitySurveyConfigurationConflictException($"Version {profile.Version} is immutable. Clone it to a draft before changing it.");
    }

    public static void EnsureDecisionEditable(QuantitySurveyConfigurationProfile profile, QuantitySurveyConfigurationDecision decision)
    {
        EnsureEditable(profile);
        if (decision.Status is not (QuantitySurveyConfigurationDecisionStatus.Draft or QuantitySurveyConfigurationDecisionStatus.Proposed or QuantitySurveyConfigurationDecisionStatus.Rejected))
            throw new QuantitySurveyConfigurationConflictException($"{decision.DecisionKey} is approved and immutable. Return it through a new profile version.");
    }

    public static int NextVersion(IEnumerable<QuantitySurveyConfigurationProfile> versions) => versions.Select(x => x.Version).DefaultIfEmpty().Max() + 1;
}

public class QuantitySurveyConfigurationException(string message) : Exception(message);
public sealed class QuantitySurveyConfigurationNotFoundException(string message) : QuantitySurveyConfigurationException(message);
public sealed class QuantitySurveyConfigurationConflictException(string message) : QuantitySurveyConfigurationException(message);
public sealed class QuantitySurveyConfigurationValidationException(string message, QuantitySurveyValidationResultDto validation) : QuantitySurveyConfigurationException(message)
{
    public QuantitySurveyValidationResultDto Validation { get; } = validation;
}
