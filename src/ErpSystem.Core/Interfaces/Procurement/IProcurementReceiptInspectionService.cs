using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementReceiptInspectionService
{
    Task<ProcurementReceiptInspectionOverviewDto> GetOverviewAsync(Guid receiptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementReceiptInspectionOverviewDto>> GetSupplierOverviewAsync(CancellationToken cancellationToken = default);
    Task<ProcurementReceiptInspectionDto> InitializeAsync(Guid receiptId, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptInspectionDto> SaveAsync(Guid receiptId, SaveProcurementReceiptInspectionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptInspectionDto> SubmitAsync(Guid caseId, SubmitProcurementReceiptInspectionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptInspectionDto> DecideAsync(Guid caseId, DecideProcurementReceiptInspectionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptInspectionDto> AcknowledgeAsync(Guid caseId, ProcurementReceiptSupplierAcknowledgementRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptInspectionDto> ResolveAsync(Guid caseId, ProcurementReceiptResolutionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementReceiptInspectionDto> CloseAsync(Guid caseId, ProcurementReceiptResolutionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task EnsureApEligibilityAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
}

public interface IProcurementReceiptInspectionStore
{
    bool HasRequiredTransaction { get; }
    Task SetMutationContextAsync(Guid inspectionCaseId, CancellationToken cancellationToken = default);
    Task ClearMutationContextAsync(CancellationToken cancellationToken = default);
}

public class ProcurementReceiptInspectionException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
public sealed class ProcurementReceiptInspectionNotFoundException(string code, string message) : ProcurementReceiptInspectionException(code, message);
public sealed class ProcurementReceiptInspectionValidationException(string code, string message) : ProcurementReceiptInspectionException(code, message);
public sealed class ProcurementReceiptInspectionConflictException(string code, string message) : ProcurementReceiptInspectionException(code, message);
public sealed class ProcurementReceiptInspectionAuthorizationException(string message) : UnauthorizedAccessException(message);
