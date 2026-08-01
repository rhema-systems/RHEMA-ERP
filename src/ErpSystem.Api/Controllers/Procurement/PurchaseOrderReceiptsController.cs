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
    private readonly IProcurementReceiptDocumentService _documentService;
    private readonly IProcurementReceiptInspectionService _inspectionService;
    private readonly ILogger<PurchaseOrderReceiptsController> _logger;

    public PurchaseOrderReceiptsController(
        IPurchaseOrderReceiptRepository purchaseOrderReceiptRepository,
        ApplicationDbContext context,
        IProcurementReceiptDocumentService documentService,
        IProcurementReceiptInspectionService inspectionService,
        ILogger<PurchaseOrderReceiptsController> logger)
    {
        _purchaseOrderReceiptRepository = purchaseOrderReceiptRepository;
        _context = context;
        _documentService = documentService;
        _inspectionService = inspectionService;
        _logger = logger;
    }

    [HttpGet("{id:guid}/inspection-control")]
    public Task<ActionResult<ProcurementReceiptInspectionOverviewDto>> GetInspectionControl(Guid id) =>
        ExecuteInspectionAsync(() => _inspectionService.GetOverviewAsync(id, HttpContext.RequestAborted));

    [HttpGet("inspection-control/supplier")]
    public Task<ActionResult<PagedResult<ProcurementReceiptInspectionOverviewDto>>>
        GetSupplierInspectionControl([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        ExecuteInspectionAsync(() => _inspectionService.GetSupplierOverviewAsync(
            page, pageSize, HttpContext.RequestAborted));

    [HttpPost("{id:guid}/inspection-control/initialize")]
    public Task<ActionResult<ProcurementReceiptInspectionDto>> InitializeInspection(Guid id) =>
        ExecuteInspectionAsync(() => _inspectionService.InitializeAsync(
            id, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPut("{id:guid}/inspection-control")]
    public Task<ActionResult<ProcurementReceiptInspectionDto>> SaveInspection(
        Guid id, [FromBody] SaveProcurementReceiptInspectionRequest request) =>
        ExecuteInspectionAsync(() => _inspectionService.SaveAsync(
            id, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("inspection-control/{caseId:guid}/submit")]
    public Task<ActionResult<ProcurementReceiptInspectionDto>> SubmitInspection(
        Guid caseId, [FromBody] SubmitProcurementReceiptInspectionRequest request) =>
        ExecuteInspectionAsync(() => _inspectionService.SubmitAsync(
            caseId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("inspection-control/{caseId:guid}/decision")]
    public Task<ActionResult<ProcurementReceiptInspectionDto>> DecideInspection(
        Guid caseId, [FromBody] DecideProcurementReceiptInspectionRequest request) =>
        ExecuteInspectionAsync(() => _inspectionService.DecideAsync(
            caseId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("inspection-control/{caseId:guid}/supplier-acknowledgement")]
    public Task<ActionResult<ProcurementReceiptInspectionDto>> AcknowledgeInspection(
        Guid caseId, [FromBody] ProcurementReceiptSupplierAcknowledgementRequest request) =>
        ExecuteInspectionAsync(() => _inspectionService.AcknowledgeAsync(
            caseId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("inspection-control/{caseId:guid}/resolution")]
    public Task<ActionResult<ProcurementReceiptInspectionDto>> ResolveInspection(
        Guid caseId, [FromBody] ProcurementReceiptResolutionRequest request) =>
        ExecuteInspectionAsync(() => _inspectionService.ResolveAsync(
            caseId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

    [HttpPost("inspection-control/{caseId:guid}/close")]
    public Task<ActionResult<ProcurementReceiptInspectionDto>> CloseInspection(
        Guid caseId, [FromBody] ProcurementReceiptResolutionRequest request) =>
        ExecuteInspectionAsync(() => _inspectionService.CloseAsync(
            caseId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));

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
            var file = await _documentService.DownloadGrnAsync(
                id, HttpContext.RequestAborted);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ProcurementReceiptDocumentNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ProcurementReceiptDocumentConflictException ex)
        {
            return Conflict(new { code = "RCV_DOCUMENT_NOT_ISSUED", message = ex.Message });
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
            SupplierName = receipt.PurchaseOrder?.BusinessPartner?.PartnerName ?? string.Empty,
            ReceiptTolerancePercent = receipt.ReceiptTolerancePercent,
            ReceiptSourceIntegrityHash =
                receipt.ReceiptSourceIntegrityHash,
            ReceiptSourceValidatedAtUtc =
                receipt.ReceiptSourceValidatedAtUtc,
            RowVersion = Convert.ToBase64String(receipt.RowVersion)
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

    private async Task<ActionResult<T>> ExecuteInspectionAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (ProcurementReceiptInspectionNotFoundException ex)
        {
            return NotFound(InspectionProblem(ex.Code, ex.Message, StatusCodes.Status404NotFound));
        }
        catch (ProcurementReceiptInspectionAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                InspectionProblem("RCV_INSPECTION_FORBIDDEN", ex.Message, StatusCodes.Status403Forbidden));
        }
        catch (ProcurementPurchaseOrderSodBlockedException ex)
        {
            var problem = InspectionProblem(
                ex.Code,
                ex.Message,
                StatusCodes.Status403Forbidden);
            problem.Extensions["readiness"] = ex.Readiness;
            return StatusCode(StatusCodes.Status403Forbidden, problem);
        }
        catch (ProcurementPurchaseOrderSodAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                InspectionProblem("PO_SOD_FORBIDDEN", ex.Message,
                    StatusCodes.Status403Forbidden));
        }
        catch (ProcurementReceiptInspectionValidationException ex)
        {
            return UnprocessableEntity(InspectionProblem(ex.Code, ex.Message,
                StatusCodes.Status422UnprocessableEntity));
        }
        catch (ProcurementReceiptInspectionConflictException ex)
        {
            return Conflict(InspectionProblem(ex.Code, ex.Message, StatusCodes.Status409Conflict));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(InspectionProblem("RCV_ROW_VERSION_CONFLICT",
                "The receipt inspection changed. Reload and retry.", StatusCodes.Status409Conflict));
        }
    }

    private ProblemDetails InspectionProblem(string code, string detail, int status)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        return problem;
    }
}
