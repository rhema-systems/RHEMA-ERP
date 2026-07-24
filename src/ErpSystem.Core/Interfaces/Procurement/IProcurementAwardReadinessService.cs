using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementAwardReadinessService
{
    Task<ProcurementAwardReadinessDto> EvaluateAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        EvaluateProcurementAwardReadinessRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementAwardReadinessDto> EnsureAwardReadyAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        EvaluateProcurementAwardReadinessRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementEvaluatorAwardApproverSodStatusDto>
        GetEvaluatorAwardApproverSodStatusAsync(
            ProcurementAwardReadinessSourceType sourceType,
            Guid sourceId,
            string correlationId,
            CancellationToken cancellationToken = default);

    Task<ProcurementAwardReadinessDto?> GetLatestAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcurementAwardReadinessDto>> GetHistoryAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        int take = 50,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementAwardReadinessNotFoundException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementAwardReadinessConflictException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementAwardReadinessValidationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementAwardReadinessBlockedException(
    string code,
    string message,
    ProcurementAwardReadinessDto decision)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public ProcurementAwardReadinessDto Decision { get; } = decision;
}

public sealed class ProcurementAwardReadinessAuthorizationException(string message)
    : UnauthorizedAccessException(message);
