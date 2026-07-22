using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

public interface ISalesAllocationService
{
    Task<IReadOnlyCollection<SalesAllocationDto>> GetAllocationsAsync(
        Guid? saleableSourceId = null,
        string? sourceItemId = null,
        string? status = null,
        Guid? businessPartnerId = null,
        Guid? salesOrderId = null,
        Guid? salesAgreementId = null,
        bool activeOnly = false);

    Task<SalesAllocationDto?> GetAllocationByIdAsync(Guid id);
    Task<SalesAllocationDto> CreateAllocationAsync(CreateSalesAllocationDto dto);
    Task<SalesAllocationDto> UpdateAllocationStatusAsync(Guid id, UpdateSalesAllocationStatusDto dto);
    Task<SalesAllocationDto> SubmitForApprovalAsync(Guid id);
    Task<SalesAllocationDto> ProcessApprovalAsync(Guid id, SalesAllocationApprovalDto dto);
    Task<SalesAllocationDto> TransferAllocationAsync(Guid id, TransferSalesAllocationDto dto);
    Task<bool> HasActiveAllocationAsync(Guid saleableSourceId, string sourceItemId, Guid? excludeAllocationId = null);
}
