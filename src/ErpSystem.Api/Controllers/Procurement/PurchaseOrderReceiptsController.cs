using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchaseOrderReceiptsController : ControllerBase
{
    private readonly IPurchaseOrderReceiptRepository _purchaseOrderReceiptRepository;
    private readonly ApplicationDbContext _context;
    private readonly Services.PurchaseOrderReceiptDocumentService _documentService;
    private readonly ILogger<PurchaseOrderReceiptsController> _logger;

    public PurchaseOrderReceiptsController(
        IPurchaseOrderReceiptRepository purchaseOrderReceiptRepository,
        ApplicationDbContext context,
        Services.PurchaseOrderReceiptDocumentService documentService,
        ILogger<PurchaseOrderReceiptsController> logger)
    {
        _purchaseOrderReceiptRepository = purchaseOrderReceiptRepository;
        _context = context;
        _documentService = documentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseOrderReceiptDto>>> GetPurchaseOrderReceipts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _purchaseOrderReceiptRepository.GetQueryable()
                .Include(r => r.PurchaseOrder)
                    .ThenInclude(po => po.BusinessPartner)
                .Include(r => r.ReceivedBy)
                .Include(r => r.InspectedBy)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(r =>
                    r.ReceiptNumber.ToLower().Contains(term) ||
                    (r.DeliveryNote != null && r.DeliveryNote.ToLower().Contains(term)) ||
                    (r.PurchaseOrder.OrderNumber != null && r.PurchaseOrder.OrderNumber.ToLower().Contains(term)) ||
                    (r.PurchaseOrder.BusinessPartner != null && r.PurchaseOrder.BusinessPartner.PartnerName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            if (startDate.HasValue)
            {
                query = query.Where(r => r.ReceiptDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.ReceiptDate <= endDate.Value);
            }

            var totalCount = await query.CountAsync();
            var receipts = await query
                .OrderByDescending(r => r.ReceiptDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<PurchaseOrderReceiptDto>
            {
                Items = receipts.Select(MapReceiptSummary).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase order receipts");
            return StatusCode(500, "An error occurred while retrieving purchase order receipts");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseOrderReceiptDto>> GetPurchaseOrderReceiptById(Guid id)
    {
        try
        {
            var receipt = await _purchaseOrderReceiptRepository.GetQueryable(r => r.Id == id)
                .Include(r => r.PurchaseOrder)
                    .ThenInclude(po => po.BusinessPartner)
                .Include(r => r.ReceivedBy)
                .Include(r => r.InspectedBy)
                .Include(r => r.Items)
                    .ThenInclude(i => i.PurchaseOrderItem)
                        .ThenInclude(poi => poi.InventoryItem)
                .Include(r => r.Items)
                    .ThenInclude(i => i.PurchaseOrderItem)
                        .ThenInclude(poi => poi.Warehouse)
                .FirstOrDefaultAsync();

            if (receipt == null)
            {
                return NotFound($"Purchase order receipt with ID {id} not found");
            }

            // Resolve warehouse/location details from the receipt line's LocationId (put-away location).
            // This is more reliable than using the PO-line warehouse because the receipt location can differ at receiving time.
            var locationLookup = new Dictionary<Guid, WarehouseLocation>();
            var locationIds = (receipt.Items ?? [])
                .Where(i => i.LocationId.HasValue && i.LocationId.Value != Guid.Empty)
                .Select(i => i.LocationId!.Value)
                .Distinct()
                .ToList();

            if (locationIds.Count > 0)
            {
                var locations = await _context.WarehouseLocations
                    .Include(l => l.Warehouse)
                    .Where(l => locationIds.Contains(l.Id))
                    .ToListAsync();

                locationLookup = locations.ToDictionary(l => l.Id, l => l);
            }

            return Ok(MapReceiptDetail(receipt, locationLookup));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase order receipt {ReceiptId}", id);
            return StatusCode(500, "An error occurred while retrieving the purchase order receipt");
        }
    }

    /// <summary>
    /// Generates a Goods Received Note (GRN) PDF for the purchase receipt.
    /// </summary>
    [HttpGet("{id:guid}/grn")]
    public async Task<ActionResult> GetGoodsReceivedNotePdf(Guid id)
    {
        try
        {
            var receiptNumber = await _context.PurchaseOrderReceipts
                .Where(r => r.Id == id)
                .Select(r => r.ReceiptNumber)
                .FirstOrDefaultAsync();

            var pdfBytes = await _documentService.GenerateGrnAsync(id);
            return File(pdfBytes, "application/pdf", $"GRN-{receiptNumber ?? id.ToString()}.pdf");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating GRN PDF for purchase receipt {ReceiptId}", id);
            return StatusCode(500, "An error occurred while generating the GRN PDF");
        }
    }

    [HttpGet("by-purchase-order/{purchaseOrderId:guid}")]
    public async Task<ActionResult<IEnumerable<PurchaseOrderReceiptDto>>> GetReceiptsByPurchaseOrderId(Guid purchaseOrderId)
    {
        try
        {
            var receipts = await _purchaseOrderReceiptRepository.GetReceiptsByPurchaseOrderIdAsync(purchaseOrderId);
            var result = receipts
                .OrderByDescending(r => r.ReceiptDate)
                .Select(MapReceiptSummary)
                .ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase order receipts for PO {PurchaseOrderId}", purchaseOrderId);
            return StatusCode(500, "An error occurred while retrieving receipts");
        }
    }

    private static PurchaseOrderReceiptDto MapReceiptSummary(PurchaseOrderReceipt receipt)
    {
        return new PurchaseOrderReceiptDto
        {
            Id = receipt.Id,
            PurchaseOrderId = receipt.PurchaseOrderId,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate,
            DeliveryNote = receipt.DeliveryNote,
            CarrierName = receipt.CarrierName,
            TrackingNumber = receipt.TrackingNumber,
            Status = receipt.Status,
            ReceivedByName = BuildUserName(receipt.ReceivedBy?.FirstName, receipt.ReceivedBy?.LastName),
            InspectedByName = BuildUserName(receipt.InspectedBy?.FirstName, receipt.InspectedBy?.LastName),
            Notes = receipt.Notes,
            RequiresInspection = receipt.RequiresInspection,
            InspectionDate = receipt.InspectionDate,
            InspectionResult = receipt.InspectionResult,
            InspectionNotes = receipt.InspectionNotes,
            PurchaseOrderNumber = receipt.PurchaseOrder?.OrderNumber ?? string.Empty,
            SupplierName = receipt.PurchaseOrder?.BusinessPartner?.PartnerName ?? string.Empty
        };
    }

    private static PurchaseOrderReceiptDto MapReceiptDetail(PurchaseOrderReceipt receipt, IReadOnlyDictionary<Guid, WarehouseLocation> locationLookup)
    {
        var dto = MapReceiptSummary(receipt);
        dto.Items = (receipt.Items ?? [])
            .OrderBy(i => i.PurchaseOrderItem?.ItemDescription)
            .Select(i => new PurchaseOrderReceiptItemDto
            {
                // Prefer the receipt line's put-away location (WarehouseLocation) since receiving can override the PO-line warehouse.
                // Fallback to the PO-line warehouse if the receipt didn't specify a location.
                Id = i.Id,
                ReceiptId = i.ReceiptId,
                PurchaseOrderItemId = i.PurchaseOrderItemId,
                ReceivedQuantity = i.ReceivedQuantity,
                AcceptedQuantity = i.AcceptedQuantity,
                RejectedQuantity = i.RejectedQuantity,
                UnitOfMeasure = i.UnitOfMeasure ?? i.PurchaseOrderItem?.UnitOfMeasure ?? i.PurchaseOrderItem?.InventoryItem?.UnitOfMeasure,
                WarehouseId = i.LocationId.HasValue && locationLookup.TryGetValue(i.LocationId.Value, out var wl)
                    ? wl.WarehouseId
                    : i.PurchaseOrderItem?.WarehouseId,
                WarehouseCode = i.LocationId.HasValue && locationLookup.TryGetValue(i.LocationId.Value, out var wl2)
                    ? wl2.Warehouse?.Code
                    : i.PurchaseOrderItem?.Warehouse?.Code,
                WarehouseName = i.LocationId.HasValue && locationLookup.TryGetValue(i.LocationId.Value, out var wl3)
                    ? wl3.Warehouse?.Name
                    : i.PurchaseOrderItem?.Warehouse?.Name,
                LocationId = i.LocationId,
                SerialNumber = i.SerialNumber,
                LotNumber = i.LotNumber,
                ExpirationDate = i.ExpirationDate,
                Notes = i.Notes,
                RejectionReason = i.RejectionReason,
                QualityStatus = i.QualityStatus,
                QualityNotes = i.QualityNotes,
                ItemCode = i.PurchaseOrderItem?.InventoryItem?.ItemCode ?? string.Empty,
                ItemName = i.PurchaseOrderItem?.InventoryItem?.Name ?? i.PurchaseOrderItem?.ItemDescription ?? string.Empty,
                LocationCode = i.LocationId.HasValue && locationLookup.TryGetValue(i.LocationId.Value, out var wl4)
                    ? wl4.LocationCode
                    : null
            })
            .ToList();

        return dto;
    }

    private static string? BuildUserName(string? firstName, string? lastName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }
}
