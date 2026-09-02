using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringConfigurationLifecyclePolicy
{
    public static void EnsureEditable(CivilEngineeringConfigurationProfile profile)
    {
        if (profile.LifecycleStatus != CivilEngineeringConfigurationProfileStatus.Draft)
            throw new CivilEngineeringConfigurationConflictException($"Version {profile.Version} is immutable. Clone it to a draft before changing it.");
    }

    public static void EnsureDecisionEditable(CivilEngineeringConfigurationProfile profile, CivilEngineeringConfigurationDecision decision)
    {
        EnsureEditable(profile);
        if (decision.Status is not (CivilEngineeringConfigurationDecisionStatus.Draft or CivilEngineeringConfigurationDecisionStatus.Proposed or CivilEngineeringConfigurationDecisionStatus.Rejected))
            throw new CivilEngineeringConfigurationConflictException($"{decision.ConfigurationKey} is approved and immutable. Return it through a new profile version.");
    }

    public static int NextVersion(IEnumerable<CivilEngineeringConfigurationProfile> versions) =>
        versions.Select(item => item.Version).DefaultIfEmpty().Max() + 1;
}
public class CivilEngineeringConfigurationException(string message) : Exception(message);
public sealed class CivilEngineeringConfigurationNotFoundException(string message) : CivilEngineeringConfigurationException(message);
public sealed class CivilEngineeringConfigurationConflictException(string message) : CivilEngineeringConfigurationException(message);
public sealed class CivilEngineeringConfigurationValidationException(string message, CivilEngineeringValidationResultDto validation) : CivilEngineeringConfigurationException(message)
{
    public CivilEngineeringValidationResultDto Validation { get; } = validation;
}
