using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementGhanepsExchangeService
{
    Task<ProcurementGhanepsExchangeOptionsDto> GetOptionsAsync(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<ProcurementGhanepsExchangeOverviewDto> GetOverviewAsync(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<ProcurementGhanepsExchangeEventDto> GetAsync(
        Guid exchangeEventId,
        CancellationToken cancellationToken = default);

    Task<ProcurementGhanepsExchangeEventDto> PrepareExportAsync(
        PrepareProcurementGhanepsExportRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementGhanepsExchangeEventDto> RecordImportAsync(
        RecordProcurementGhanepsImportRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementGhanepsExchangeEventDto> RecordAttemptAsync(
        Guid exchangeEventId,
        RecordProcurementGhanepsAttemptRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementGhanepsExchangeEventDto> RetryAsync(
        Guid exchangeEventId,
        RetryProcurementGhanepsExchangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementGhanepsExchangeEventDto> RecordAcknowledgementAsync(
        Guid exchangeEventId,
        RecordProcurementGhanepsAcknowledgementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementGhanepsExchangeEventDto> ReconcileAsync(
        Guid exchangeEventId,
        ReconcileProcurementGhanepsExchangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementGhanepsExchangeNotFoundException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementGhanepsExchangeConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementGhanepsExchangeValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementGhanepsExchangeAuthorizationException(string message) : Exception(message)
{
    public string Code { get; } = "GHANEPS_EXCHANGE_ACCESS_FORBIDDEN";
}
