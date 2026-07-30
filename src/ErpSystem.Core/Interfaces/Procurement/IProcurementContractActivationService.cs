using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementContractActivationService
{
    Task<ProcurementContractActivationOverviewDto> GetOverviewAsync(
        Guid contractId, CancellationToken cancellationToken = default);
    Task<ProcurementContractActivationDto> SubmitAsync(
        Guid contractId, SubmitProcurementContractActivationRequest request,
        string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementContractActivationDto> DecideAsync(
        Guid activationId, DecideProcurementContractActivationRequest request,
        string correlationId, CancellationToken cancellationToken = default);
    Task<ContractDto> ActivateAsync(
        Guid activationId, ActivateProcurementContractRequest request,
        string correlationId, CancellationToken cancellationToken = default);
}

public interface IProcurementContractActivationStore
{
    bool HasRequiredTransaction { get; }
    Task SetMutationContextAsync(Guid activationId, CancellationToken cancellationToken = default);
    Task ClearMutationContextAsync(CancellationToken cancellationToken = default);
}

public class ProcurementContractActivationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementContractActivationNotFoundException(string code, string message)
    : ProcurementContractActivationException(code, message);
public sealed class ProcurementContractActivationConflictException(string code, string message)
    : ProcurementContractActivationException(code, message);
public sealed class ProcurementContractActivationValidationException(string code, string message)
    : ProcurementContractActivationException(code, message);
public sealed class ProcurementContractActivationAuthorizationException(string message)
    : UnauthorizedAccessException(message);
