using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Service contract for managing Invoices in the AR module.
/// </summary>
public interface IInvoiceService
{
    /// <summary>
    /// Retrieves an invoice by ID.
    /// </summary>
    Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an invoice by invoice number.
    /// </summary>
    Task<InvoiceDto?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all invoices with optional filtering and pagination.
    /// </summary>
    Task<PagedResult<InvoiceDto>> GetAllAsync(InvoiceQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new invoice.
    /// - Auto-generates invoice number with configurable prefix/suffix
    /// - Calculates tax amounts using TaxCalculationEngine
    /// - Validates customer credit limit
    /// - Checks for duplicate invoices
    /// </summary>
    Task<InvoiceDto> CreateAsync(InvoiceCreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing invoice (only in Draft status).
    /// </summary>
    Task<InvoiceDto> UpdateAsync(InvoiceUpdateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an invoice (only in Draft status).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes invoice status from Draft to Sent.
    /// Updates customer's outstanding balance.
    /// </summary>
    Task<InvoiceDto> SendInvoiceAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a sent customer invoice to the General Ledger through the central finance posting engine.
    /// </summary>
    Task<InvoiceDto> PostAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids/cancels an invoice.
    /// Reverses customer's outstanding balance.
    /// </summary>
    Task<InvoiceDto> VoidInvoiceAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payment allocations for an invoice.
    /// </summary>
    Task<List<ErpSystem.Core.DTOs.Finance.PaymentAllocationDto>> GetInvoiceAllocationsAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a printable invoice (PDF/HTML).
    /// </summary>
    Task<byte[]> GenerateInvoicePrintAsync(Guid id, string format = "PDF", CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for duplicate invoices based on reference or date range.
    /// </summary>
    Task<bool> IsDuplicateAsync(Guid customerId, string reference, DateTime invoiceDate, CancellationToken cancellationToken = default);
}
