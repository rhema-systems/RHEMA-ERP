using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Delivery Note management — the fulfillment leg of the order-to-cash cycle.
/// </summary>
public interface IDeliveryService
{
    // ── CRUD ────────────────────────────────────────────────────────────

    Task<DeliveryNoteDetailDto> CreateDeliveryNoteAsync(CreateDeliveryNoteDto dto);
    Task<DeliveryNoteDetailDto?> GetDeliveryNoteByIdAsync(Guid id);
    Task<PagedResult<DeliveryNoteSummaryDto>> GetDeliveryNotesAsync(
        int page, int pageSize,
        string? search = null,
        DeliveryNoteStatus? status = null,
        Guid? salesOrderId = null,
        Guid? customerId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    // ── Lifecycle Actions ───────────────────────────────────────────────

    /// <summary>
    /// Mark items as packed and ready for shipment
    /// </summary>
    Task<DeliveryNoteDetailDto> MarkAsPackedAsync(Guid id);

    /// <summary>
    /// Mark as shipped with carrier/tracking details
    /// </summary>
    Task<DeliveryNoteDetailDto> MarkAsShippedAsync(Guid id, string? carrierName = null, string? trackingNumber = null);

    /// <summary>
    /// Confirm delivery — deducts stock from inventory, updates SO delivered quantities
    /// </summary>
    Task<DeliveryNoteDetailDto> ConfirmDeliveryAsync(Guid id, ConfirmDeliveryDto dto);

    /// <summary>
    /// Cancel a delivery note — restores reserved stock if applicable
    /// </summary>
    Task<DeliveryNoteDetailDto> CancelDeliveryNoteAsync(Guid id, string? reason = null);

    // ── Utilities ───────────────────────────────────────────────────────

    Task<string> GenerateDeliveryNumberAsync();

    /// <summary>
    /// Get deliverable lines for a Sales Order (lines with remaining quantity)
    /// </summary>
    Task<List<SalesOrderLineDto>> GetDeliverableLinesAsync(Guid salesOrderId);
}
