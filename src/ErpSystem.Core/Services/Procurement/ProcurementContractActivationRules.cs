using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementContractActivationRules
{
    public static bool IsTerminal(ProcurementContractActivationStatus status) =>
        status is ProcurementContractActivationStatus.Rejected
            or ProcurementContractActivationStatus.Activated
            or ProcurementContractActivationStatus.Cancelled;

    public static bool CanSubmitContract(string status) =>
        string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "PendingSignature", StringComparison.OrdinalIgnoreCase);

    public static bool CanDecide(ProcurementContractActivationStatus status) =>
        status == ProcurementContractActivationStatus.PendingApproval;

    public static bool CanActivate(ProcurementContractActivationStatus status) =>
        status == ProcurementContractActivationStatus.Approved ||
        status == ProcurementContractActivationStatus.RevalidationFailed;

    public static bool IsIndependent(Guid actorUserId, Guid submittedById, Guid? contractCreatedById) =>
        actorUserId != Guid.Empty &&
        actorUserId != submittedById &&
        (!contractCreatedById.HasValue || actorUserId != contractCreatedById.Value);

    public static bool AmountMatchesAward(decimal contractValue, decimal awardValue) =>
        decimal.Round(contractValue, 2, MidpointRounding.AwayFromZero) ==
        decimal.Round(awardValue, 2, MidpointRounding.AwayFromZero);
}
