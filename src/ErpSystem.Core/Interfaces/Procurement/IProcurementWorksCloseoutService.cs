using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementWorksCloseoutService
{
    Task<ProcurementWorksCloseoutOverviewDto> GetOverviewAsync(
        Guid contractId, CancellationToken cancellationToken = default);
    Task<ProcurementWorksCloseoutActionDto> SubmitAsync(
        Guid contractId, SubmitProcurementWorksCloseoutActionRequest request,
        string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementWorksCloseoutActionDto> DecideAsync(
        Guid actionId, DecideProcurementWorksCloseoutActionRequest request,
        string correlationId, CancellationToken cancellationToken = default);
}

public interface IProcurementWorksCloseoutStore
{
    bool HasRequiredTransaction { get; }
    Task SetMutationContextAsync(
        Guid actionId, CancellationToken cancellationToken = default);
    Task ClearMutationContextAsync(
        CancellationToken cancellationToken = default);
}

public class ProcurementWorksCloseoutException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementWorksCloseoutNotFoundException(string code, string message)
    : ProcurementWorksCloseoutException(code, message);
public sealed class ProcurementWorksCloseoutConflictException(string code, string message)
    : ProcurementWorksCloseoutException(code, message);
public sealed class ProcurementWorksCloseoutValidationException(string code, string message)
    : ProcurementWorksCloseoutException(code, message);
public sealed class ProcurementWorksCloseoutAuthorizationException(string message)
    : UnauthorizedAccessException(message);
