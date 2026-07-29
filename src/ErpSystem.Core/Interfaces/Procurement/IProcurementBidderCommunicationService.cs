using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementBidderCommunicationService
{
    Task<ProcurementBidderCommunicationOverviewDto> GetOverviewAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderCommunicationOverviewDto> GetExternalOverviewAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderCommunicationOverviewDto> InitializeAsync(
        InitializeProcurementBidderCommunicationRegisterRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderCommunicationLetterVersionDto> ApproveLetterAsync(
        Guid recipientId,
        ApproveProcurementBidderCommunicationLetterRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderCommunicationDispatchDto> DispatchLetterAsync(
        Guid letterVersionId,
        DispatchProcurementBidderCommunicationLetterRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderCommunicationDeliveryDto> RecordDeliveryAsync(
        Guid dispatchId,
        RecordProcurementBidderCommunicationDeliveryRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderCommunicationAcknowledgementDto> RecordAcknowledgementAsync(
        Guid dispatchId,
        RecordProcurementBidderCommunicationAcknowledgementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderCommunicationAcknowledgementDto> RecordExternalAcknowledgementAsync(
        Guid dispatchId,
        RecordProcurementBidderCommunicationAcknowledgementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderAppealDto> FileAppealAsync(
        Guid recipientId,
        FileProcurementBidderAppealRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderAppealDto> FileExternalAppealAsync(
        Guid recipientId,
        FileProcurementBidderAppealRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementBidderAppealDecisionDto> ResolveAppealAsync(
        Guid appealId,
        ResolveProcurementBidderAppealRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementTenderSecurityInstrumentDto> RegisterSecurityAsync(
        Guid recipientId,
        RegisterProcurementTenderSecurityRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementTenderSecurityActionDto> RecordSecurityActionAsync(
        Guid securityId,
        RecordProcurementTenderSecurityActionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementBidderCommunicationNotFoundException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementBidderCommunicationConflictException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementBidderCommunicationValidationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementBidderCommunicationAuthorizationException(string message)
    : UnauthorizedAccessException(message);
