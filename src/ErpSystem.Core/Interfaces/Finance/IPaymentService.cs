using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Service contract for managing Customer Payments and Allocations in the AR module.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Retrieves a customer payment by ID.
    /// </summary>
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto?> GetByIdAsync(
        Guid id,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default);

    /// <summary>Returns source, operational-subledger, ledger, reversal, and audit evidence.</summary>
    Task<CustomerPaymentTraceDto?> GetTraceAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a payment by payment number.
    /// </summary>
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto?> GetByPaymentNumberAsync(string paymentNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all payments with optional filtering.
    /// </summary>
    Task<PagedResult<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto>> GetAllAsync(PaymentQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new customer payment receipt.
    /// - Auto-generates payment number
    /// - Supports multiple payment methods (Bank, Cash, Check, etc.)
    /// - Handles multi-currency with exchange rates
    /// - Creates credit note record if applicable
    /// </summary>
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto> CreateAsync(PaymentCreateDto dto, CancellationToken cancellationToken = default);
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto> CreateAsync(
        PaymentCreateDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an approved or postable customer receipt to the General Ledger through the central finance posting engine.
    /// </summary>
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto> PostAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto> PostAsync(
        Guid id,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses a posted receipt through linked compensating Finance postings and operational
    /// records. The original receipt and allocations remain immutable audit evidence.
    /// </summary>
    Task<CustomerPaymentDto> ReversePaymentAsync(
        Guid id,
        ReverseCustomerPaymentDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing payment (only if status is Pending).
    /// </summary>
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto> UpdateAsync(PaymentUpdateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Allocates payment amount to one or more invoices.
    /// Supports:
    /// - Partial payment allocation
    /// - Multi-invoice allocation
    /// - Overpayment handling (leaves unallocated amount)
    /// - Cash discount application
    /// </summary>
    Task<PaymentAllocationResultDto> AllocatePaymentAsync(PaymentAllocation_CreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses a payment allocation.
    /// Restores invoice balance and payment unallocated amount.
    /// </summary>
    Task ReverseAllocationAsync(Guid allocationId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a payment as cleared/reconciled.
    /// Updates status and cleared date.
    /// </summary>
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto> ClearPaymentAsync(Guid id, DateTime clearedDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a payment as bounced/dishonored.
    /// Reverses all allocations and updates invoice balances.
    /// </summary>
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto> BouncedPaymentAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all allocations for a specific payment.
    /// </summary>
    Task<List<ErpSystem.Core.DTOs.Finance.PaymentAllocationDto>> GetPaymentAllocationsAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets outstanding (not fully paid) invoices for a customer.
    /// Used when allocating payments.
    /// </summary>
    Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a payment receipt for printing.
    /// </summary>
    Task<byte[]> GeneratePaymentReceiptAsync(Guid id, string format = "PDF", CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles credit note creation and allocation.
    /// </summary>
    Task<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto> CreateCreditNoteAsync(CreditNoteCreateDto dto, CancellationToken cancellationToken = default);
}
