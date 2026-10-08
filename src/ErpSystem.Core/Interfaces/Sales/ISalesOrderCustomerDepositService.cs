using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

public interface ISalesOrderCustomerDepositService
{
    Task<IReadOnlyList<SalesOrderCustomerDepositDto>> GetAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<SalesOrderCustomerDepositDto> CreateAsync(
        Guid salesOrderId,
        CreateSalesOrderCustomerDepositDto request,
        CancellationToken cancellationToken = default);
}
