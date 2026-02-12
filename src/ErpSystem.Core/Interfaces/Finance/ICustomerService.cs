using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Service contract for managing Customers in the AR module.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Retrieves a customer by ID.
    /// </summary>
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a customer by customer code.
    /// </summary>
    Task<CustomerDto?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all customers for the current tenant with optional filtering.
    /// </summary>
    Task<PagedResult<CustomerDto>> GetAllAsync(CustomerQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new customer.
    /// Validates credit limit, generates customer code if not provided.
    /// </summary>
    Task<CustomerDto> CreateAsync(CustomerCreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing customer.
    /// </summary>
    Task<CustomerDto> UpdateAsync(CustomerUpdateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a customer (marks as inactive).
    /// Validates that customer has no outstanding balance.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets customer's outstanding balance and aging details.
    /// </summary>
    Task<CustomerBalanceDto> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a customer can have additional credit.
    /// Checks credit limit vs outstanding balance.
    /// </summary>
    Task<CreditCheckResultDto> CheckCreditLimitAsync(Guid customerId, decimal additionalAmount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all invoices for a customer.
    /// </summary>
    Task<List<ErpSystem.Core.DTOs.Finance.InvoiceDto>> GetCustomerInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payment history for a customer.
    /// </summary>
    Task<List<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto>> GetCustomerPaymentsAsync(Guid customerId, CancellationToken cancellationToken = default);
}
