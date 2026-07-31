using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementReceiptDocumentService
{
    Task<ProcurementReceiptDocumentOverviewDto> EnsureAsync(Guid receiptId, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptDocumentOverviewDto> GetOverviewAsync(Guid receiptId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptDocumentDto> SignAsync(Guid documentId, SignProcurementReceiptDocumentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptDocumentDto> IssueAsync(Guid documentId, IssueProcurementReceiptDocumentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptDocumentDto> CancelAsync(Guid documentId, CancelProcurementReceiptDocumentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptDocumentOverviewDto> ReconcileAsync(Guid receiptId, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptDocumentFileDto> DownloadAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementReceiptDocumentNotFoundException(string message) : Exception(message);
public sealed class ProcurementReceiptDocumentAuthorizationException(string message) : Exception(message);
public sealed class ProcurementReceiptDocumentConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
public sealed class ProcurementReceiptDocumentValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
