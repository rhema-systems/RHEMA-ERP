using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementFrameworkCallOffService
{
    Task<ProcurementFrameworkCallOffSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkCallOffPageDto> SearchAsync(ProcurementFrameworkCallOffSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkCallOffDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkCallOffOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkCallOffDto> CreateAsync(CreateProcurementFrameworkCallOffRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkCallOffDto> SubmitAsync(Guid id, ProcurementFrameworkCallOffLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkCallOffDto> DecideAsync(Guid id, ProcurementFrameworkCallOffDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkCallOffDto> IssueAsync(Guid id, ProcurementFrameworkCallOffLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkCallOffDto> CancelAsync(Guid id, ProcurementFrameworkCallOffLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<int> ProcessExpiryAlertsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsFrameworkCallOffPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementFrameworkCallOffNotFoundException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementFrameworkCallOffValidationException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementFrameworkCallOffConflictException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementFrameworkCallOffAuthorizationException(string message) :
    Exception(message);
