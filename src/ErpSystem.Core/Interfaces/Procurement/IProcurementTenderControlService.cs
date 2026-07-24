using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementTenderControlService
{
    Task<ProcurementTenderControlDto> GetAsync(Guid tenderId, CancellationToken cancellationToken = default);
    Task<bool> IsNctOrIctAsync(Guid tenderId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> PublishAsync(Guid tenderId, PublishProcurementTenderRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentIssueDto> IssueDocumentAsync(Guid tenderId, IssueProcurementTenderDocumentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderSubmissionDisposition?> RecordSubmissionAsync(TenderBid bid, DateTime receivedAtUtc, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> CompleteOpeningAsync(Guid tenderId, CompleteProcurementTenderOpeningRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> SaveTechnicalEvaluationAsync(Guid tenderId, SaveProcurementTenderTechnicalEvaluationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> SaveFinancialEvaluationAsync(Guid tenderId, SaveProcurementTenderFinancialEvaluationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> SubmitApprovalAsync(Guid tenderId, SubmitProcurementTenderApprovalRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> DecideApprovalAsync(Guid tenderId, DecideProcurementTenderApprovalRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> RecordAwardAsync(Guid tenderId, RecordProcurementTenderAwardRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> RecordContractAsync(Guid tenderId, RecordProcurementTenderContractRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderControlDto> RecordAcceptanceAsync(Guid tenderId, RecordProcurementTenderAcceptanceRequest request, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementTenderControlNotFoundException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementTenderControlConflictException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementTenderControlValidationException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementTenderControlAuthorizationException(string message) : UnauthorizedAccessException(message);
