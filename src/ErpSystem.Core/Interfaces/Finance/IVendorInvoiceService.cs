using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Service contract for managing Vendor Invoices in the Accounts Payable module.
/// Handles full invoice lifecycle: creation, matching (2-way/3-way), approval, and voiding.
/// </summary>
public interface IVendorInvoiceService
{
    /// <summary>
    /// Retrieves a vendor invoice by ID, including line items and payment allocations.
    /// </summary>
    Task<VendorInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a vendor invoice by its auto-generated invoice number.
    /// </summary>
    Task<VendorInvoiceDto?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves vendor invoices with filtering, sorting, and pagination.
    /// </summary>
    Task<PagedResult<VendorInvoiceDto>> GetAllAsync(VendorInvoiceQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new vendor invoice.
    /// - Auto-generates invoice number (VI-YYYY-NNNNN)
    /// - Calculates totals from line items
    /// - Checks for duplicates (supplier + reference + date)
    /// - Calculates WHT and early payment discount
    /// </summary>
    Task<VendorInvoiceDto> CreateAsync(VendorInvoiceCreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a vendor invoice (only if Draft or Rejected status).
    /// Recalculates totals and matching status.
    /// </summary>
    Task<VendorInvoiceDto> UpdateAsync(VendorInvoiceUpdateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vendor invoice (only if Draft status, soft-delete).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a vendor invoice for approval.
    /// Validates matching status if matching type is set.
    /// </summary>
    Task<VendorInvoiceDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending vendor invoice.
    /// </summary>
    Task<VendorInvoiceDto> ApproveAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an approved vendor invoice to the general ledger through the central finance posting engine.
    /// </summary>
    Task<VendorInvoiceDto> PostAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending vendor invoice with required comments.
    /// </summary>
    Task<VendorInvoiceDto> RejectAsync(Guid id, string comments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the AP-owned terminal outcome after the shared Finance workbench has already
    /// completed the workflow rejection. This releases any active Finance budget reservation
    /// and is idempotent so a retry can repair interrupted outcome handling.
    /// </summary>
    Task<VendorInvoiceDto> ApplyRejectedWorkflowOutcomeAsync(
        Guid id,
        string? comments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids an approved vendor invoice.
    /// Cannot void if any payment allocations exist.
    /// </summary>
    Task<VendorInvoiceDto> VoidAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs 2-way matching: compares invoice totals against purchase order totals.
    /// Uses a configurable tolerance (default ±1%).
    /// </summary>
    Task<InvoiceMatchingResultDto> PerformTwoWayMatchAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs 3-way matching: compares invoice vs PO vs goods receipt quantities and amounts.
    /// Uses a configurable tolerance (default ±1%).
    /// </summary>
    Task<InvoiceMatchingResultDto> PerformThreeWayMatchAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current matching result for a vendor invoice without re-executing matching.
    /// </summary>
    Task<InvoiceMatchingResultDto> GetMatchingResultAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the authoritative current three-way approval readiness without mutating the invoice.
    /// </summary>
    Task<InvoiceMatchingResultDto> GetThreeWayMatchReadinessAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a potential duplicate exists based on supplier, reference, and date.
    /// </summary>
    Task<bool> IsDuplicateAsync(Guid supplierId, string? supplierInvoiceNumber, DateTime invoiceDate, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
