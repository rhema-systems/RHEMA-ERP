using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementPolicyLifecyclePolicy
{
    public static bool IsEditable(ProcurementPolicySet policySet) =>
        policySet.LifecycleStatus == ProcurementPolicyLifecycleStatus.Draft;

    public static bool IsRuntimeEligible(ProcurementPolicySet policySet, DateTime atUtc) =>
        policySet.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
        policySet.EffectiveFrom <= atUtc &&
        (!policySet.EffectiveTo.HasValue || policySet.EffectiveTo.Value >= atUtc);

    public static int GetNextVersion(IEnumerable<ProcurementPolicySet> versions) =>
        versions.Select(item => item.Version).DefaultIfEmpty(0).Max() + 1;

    public static void EnsureEditable(ProcurementPolicySet policySet)
    {
        if (!IsEditable(policySet))
            throw new ProcurementPolicyConflictException(
                $"Policy version {policySet.Version} is {policySet.LifecycleStatus.ToString().ToLowerInvariant()} and immutable. Clone it to a new draft first.");
    }

    public static void EnsureCanPublish(ProcurementPolicySet policySet)
    {
        if (policySet.LifecycleStatus != ProcurementPolicyLifecycleStatus.Draft)
            throw new ProcurementPolicyConflictException("Only a draft procurement policy can be published.");
    }

    public static void EnsureCanRetire(ProcurementPolicySet policySet)
    {
        if (policySet.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published)
            throw new ProcurementPolicyConflictException("Only a published procurement policy can be retired.");
    }
}

public class ProcurementPolicyException : Exception
{
    public ProcurementPolicyException(string message) : base(message) { }
}

public sealed class ProcurementPolicyNotFoundException : ProcurementPolicyException
{
    public ProcurementPolicyNotFoundException(string message) : base(message) { }
}

public sealed class ProcurementPolicyConflictException : ProcurementPolicyException
{
    public ProcurementPolicyConflictException(string message) : base(message) { }
}

public sealed class ProcurementPolicyAuthorizationException : ProcurementPolicyException
{
    public ProcurementPolicyAuthorizationException(string message) : base(message) { }
}

public sealed class ProcurementPolicyValidationException : ProcurementPolicyException
{
    public ProcurementPolicyValidationException(string message, ProcurementPolicyValidationResultDto validation) : base(message) => Validation = validation;
    public ProcurementPolicyValidationResultDto Validation { get; }
}
