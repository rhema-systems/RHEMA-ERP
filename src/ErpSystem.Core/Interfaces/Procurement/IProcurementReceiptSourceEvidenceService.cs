using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementReceiptSourceEvidenceReadinessService
{
    Task EnsureWaybillReadyAsync(Guid receiptId, CancellationToken cancellationToken = default);
}

public interface IProcurementReceiptSourceEvidenceService : IProcurementReceiptSourceEvidenceReadinessService
{
    Task<ProcurementReceiptSourceEvidenceOverviewDto> GetOverviewAsync(Guid receiptId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptSourceEvidenceDto> UploadAsync(Guid receiptId, ProcurementReceiptSourceEvidenceUploadCommand command,
        string correlationId, CancellationToken cancellationToken = default);
    Task<CentralDocumentRepositoryContent> OpenAsync(Guid receiptId, Guid evidenceId, CancellationToken cancellationToken = default);
}

public class ProcurementReceiptSourceEvidenceException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementReceiptSourceEvidenceNotFoundException(string message)
    : ProcurementReceiptSourceEvidenceException("RCV_SOURCE_EVIDENCE_NOT_FOUND", message);
public sealed class ProcurementReceiptSourceEvidenceValidationException(string code, string message)
    : ProcurementReceiptSourceEvidenceException(code, message);
public sealed class ProcurementReceiptSourceEvidenceConflictException(string code, string message)
    : ProcurementReceiptSourceEvidenceException(code, message);
public sealed class ProcurementReceiptSourceEvidenceAuthorizationException(string message)
    : UnauthorizedAccessException(message);
