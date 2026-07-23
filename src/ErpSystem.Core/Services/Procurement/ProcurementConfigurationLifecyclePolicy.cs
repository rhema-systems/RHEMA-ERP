using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementConfigurationLifecyclePolicy
{
    public static bool IsEditable(ProcurementConfigurationProfile profile) =>
        profile.LifecycleStatus == ProcurementConfigurationProfileStatus.Draft;

    public static bool IsDecisionEditable(
        ProcurementConfigurationProfile profile,
        ProcurementConfigurationDecision decision) =>
        IsEditable(profile) &&
        decision.Status is ProcurementConfigurationDecisionStatus.Draft or ProcurementConfigurationDecisionStatus.Proposed;

    public static bool IsReturnToProposed(
        ProcurementConfigurationDecision decision,
        ProcurementConfigurationDecisionStatus requestedStatus,
        ProcurementConfigurationApprovalStatus requestedApprovalStatus) =>
        decision.Status is ProcurementConfigurationDecisionStatus.Approved or ProcurementConfigurationDecisionStatus.Rejected &&
        requestedStatus == ProcurementConfigurationDecisionStatus.Proposed &&
        requestedApprovalStatus == ProcurementConfigurationApprovalStatus.Pending;

    public static bool IsRuntimeEligible(ProcurementConfigurationProfile profile, DateTime atUtc) =>
        profile.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
        profile.EffectiveFrom <= atUtc &&
        (!profile.EffectiveTo.HasValue || profile.EffectiveTo.Value >= atUtc);

    public static int GetNextVersion(IEnumerable<ProcurementConfigurationProfile> versions) =>
        versions.Select(item => item.Version).DefaultIfEmpty(0).Max() + 1;

    public static void EnsureEditable(ProcurementConfigurationProfile profile)
    {
        if (!IsEditable(profile))
            throw new ProcurementConfigurationConflictException(
                $"Profile version {profile.Version} is {profile.LifecycleStatus.ToString().ToLowerInvariant()} and is immutable. Clone it to a new draft first.");
    }

    public static void EnsureDecisionEditable(
        ProcurementConfigurationProfile profile,
        ProcurementConfigurationDecision decision)
    {
        EnsureEditable(profile);
        if (decision.Status is not (ProcurementConfigurationDecisionStatus.Draft or ProcurementConfigurationDecisionStatus.Proposed))
            throw new ProcurementConfigurationConflictException(
                $"{decision.DecisionKey} is {decision.Status.ToString().ToLowerInvariant()} and cannot be edited until it is returned to proposed state by an authorized approver.");
    }

    public static void EnsureCanPublish(ProcurementConfigurationProfile profile)
    {
        if (profile.LifecycleStatus != ProcurementConfigurationProfileStatus.Draft)
            throw new ProcurementConfigurationConflictException("Only a draft procurement configuration profile can be published.");
    }

    public static void EnsureCanRetire(ProcurementConfigurationProfile profile)
    {
        if (profile.LifecycleStatus != ProcurementConfigurationProfileStatus.Published)
            throw new ProcurementConfigurationConflictException("Only a published procurement configuration profile can be retired.");
    }
}

public class ProcurementConfigurationException : Exception
{
    public ProcurementConfigurationException(string message) : base(message) { }
}

public sealed class ProcurementConfigurationNotFoundException : ProcurementConfigurationException
{
    public ProcurementConfigurationNotFoundException(string message) : base(message) { }
}

public sealed class ProcurementConfigurationConflictException : ProcurementConfigurationException
{
    public ProcurementConfigurationConflictException(string message) : base(message) { }
}

public sealed class ProcurementConfigurationAuthorizationException : ProcurementConfigurationException
{
    public ProcurementConfigurationAuthorizationException(string message) : base(message) { }
}

public sealed class ProcurementConfigurationValidationException : ProcurementConfigurationException
{
    public ProcurementConfigurationValidationException(
        string message,
        ProcurementConfigurationValidationResultDto validation) : base(message)
    {
        Validation = validation;
    }

    public ProcurementConfigurationValidationResultDto Validation { get; }
}
