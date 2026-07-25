using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Service contract for managing Vendor Payments, Allocations, and Payment Batches
/// in the Accounts Payable module.
/// </summary>
public interface IVendorPaymentService
{
    // ── Single Payments ─────────────────────────────────────────────────

    /// <summary>
    /// Retrieves a vendor payment by ID, including allocations.
    /// </summary>
    Task<VendorPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves vendor payments with filtering, sorting, and pagination.
    /// </summary>
    Task<PagedResult<VendorPaymentDto>> GetAllAsync(VendorPaymentQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new vendor payment.
    /// - Auto-generates payment number (VP-YYYY-NNNNN)
    /// - Optionally creates allocations in the same transaction
    /// - Calculates WHT deduction
    /// </summary>
    Task<VendorPaymentDto> CreateAsync(VendorPaymentCreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an approved or processed vendor payment to the General Ledger through the central finance posting engine.
    /// </summary>
    Task<VendorPaymentDto> PostAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Allocations ─────────────────────────────────────────────────────

    /// <summary>
    /// Allocates payment amount to one or more vendor invoices.
    /// Supports:
    /// - Partial payment allocation
    /// - Multi-invoice allocation
    /// - Early payment discount application (auto-calculate if eligible)
    /// - WHT deduction
    /// </summary>
    Task<VendorPaymentAllocationResultDto> AllocatePaymentAsync(Guid paymentId, List<VendorPaymentAllocationCreateDto> allocations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses a specific payment allocation.
    /// Restores invoice balance and increases payment unallocated amount.
    /// </summary>
    Task ReverseAllocationAsync(Guid allocationId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all allocations for a specific vendor payment.
    /// </summary>
    Task<List<VendorPaymentAllocationDto>> GetPaymentAllocationsAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets outstanding (not fully paid) invoices for a specific supplier.
    /// Used when creating payments or allocating funds.
    /// </summary>
    Task<List<OutstandingVendorInvoiceDto>> GetOutstandingInvoicesAsync(Guid supplierId, CancellationToken cancellationToken = default);

    // ── Payment Status ──────────────────────────────────────────────────

    /// <summary>
    /// Marks a payment as cleared/reconciled with the bank statement.
    /// </summary>
    Task<VendorPaymentDto> ClearPaymentAsync(Guid id, DateTime clearedDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids a vendor payment.
    /// Reverses all allocations and updates invoice balances.
    /// </summary>
    Task<VendorPaymentDto> VoidPaymentAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    // ── Early Payment Discount ──────────────────────────────────────────

    /// <summary>
    /// Calculates the early payment discount available for a specific vendor invoice
    /// based on the proposed payment date.
    /// </summary>
    Task<EarlyPaymentDiscountResultDto> CalculateEarlyPaymentDiscountAsync(Guid invoiceId, DateTime paymentDate, CancellationToken cancellationToken = default);

    // ── Payment Batches ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a payment batch from a list of approved vendor invoices.
    /// Auto-creates individual VendorPayment records grouped by supplier.
    /// </summary>
    Task<PaymentBatchDto> CreatePaymentBatchAsync(PaymentBatchCreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a payment batch by ID, including its items.
    /// </summary>
    Task<PaymentBatchDto?> GetPaymentBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves payment batches with filtering and pagination.
    /// </summary>
    Task<PagedResult<PaymentBatchDto>> GetAllBatchesAsync(PaymentBatchQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a payment batch for processing.
    /// </summary>
    Task<PaymentBatchDto> ApprovePaymentBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an approved payment batch.
    /// Executes all payments, allocates to invoices, and updates statuses.
    /// Returns the batch with updated item statuses (Processed/Failed).
    /// </summary>
    Task<PaymentBatchDto> ProcessPaymentBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}
