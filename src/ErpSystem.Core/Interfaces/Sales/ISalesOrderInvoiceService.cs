using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

public interface ISalesOrderInvoiceService
{
    Task<SalesOrderInvoiceDetailDto> GenerateAsync(Guid orderId, GenerateSalesOrderInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderInvoiceDetailDto> GetAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<SalesOrderInvoiceDetailDto> SubmitAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<SalesOrderInvoiceDetailDto> PostAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<ErpSystem.Core.DTOs.Finance.InvoiceDistributionDto> GetDistributionAsync(Guid orderId, CancellationToken cancellationToken = default);
}
