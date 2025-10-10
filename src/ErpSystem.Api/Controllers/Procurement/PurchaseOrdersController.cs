using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Common;

namespace ErpSystem.Api.Controllers.Procurement;

/// <summary>
/// API controller for managing purchase orders
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseOrderItemRepository _purchaseOrderItemRepository;
    private readonly IPurchaseOrderReceiptRepository _purchaseOrderReceiptRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ILogger<PurchaseOrdersController> _logger;

    public PurchaseOrdersController(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseOrderItemRepository purchaseOrderItemRepository,
        IPurchaseOrderReceiptRepository purchaseOrderReceiptRepository,
        ISupplierRepository supplierRepository,
        ILogger<PurchaseOrdersController> logger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseOrderItemRepository = purchaseOrderItemRepository;
        _purchaseOrderReceiptRepository = purchaseOrderReceiptRepository;
        _supplierRepository = supplierRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all purchase orders with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseOrderSummaryDto>>> GetPurchaseOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var purchaseOrders = await _purchaseOrderRepository.GetPurchaseOrdersAsync(
                page, pageSize, search, status, supplierId, startDate, endDate);

            var purchaseOrderDtos = purchaseOrders.Items.Select(po => new PurchaseOrderSummaryDto
            {
                Id = po.Id,
                OrderNumber = po.OrderNumber,
                SupplierId = po.SupplierId,
                SupplierName = po.Supplier?.Name ?? "",
                OrderDate = po.OrderDate,
                RequiredDate = po.RequiredDate,
                PromisedDate = po.PromisedDate,
                Status = po.Status,
                TotalAmount = po.TotalAmount,
                ItemCount = po.Items?.Count ?? 0,
                RequestedByName = po.RequestedBy?.FirstName + " " + po.RequestedBy?.LastName
            }).ToList();

            var result = new PagedResult<PurchaseOrderSummaryDto>
            {
                Items = purchaseOrderDtos,
                TotalCount = purchaseOrders.TotalCount,
                Page = purchaseOrders.Page,
                PageSize = purchaseOrders.PageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders");
            return StatusCode(500, "An error occurred while retrieving purchase orders");
        }
    }

    /// <summary>
    /// Gets a purchase order by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> GetPurchaseOrder(Guid id)
    {
        try
        {
            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            var items = await _purchaseOrderItemRepository.GetItemsByPurchaseOrderIdAsync(id);
            var receipts = await _purchaseOrderReceiptRepository.GetReceiptsByPurchaseOrderIdAsync(id);

            var purchaseOrderDto = new PurchaseOrderDetailDto
            {
                Id = purchaseOrder.Id,
                OrderNumber = purchaseOrder.OrderNumber,
                SupplierId = purchaseOrder.SupplierId,
                SupplierName = purchaseOrder.Supplier?.Name ?? "",
                OrderDate = purchaseOrder.OrderDate,
                RequiredDate = purchaseOrder.RequiredDate,
                PromisedDate = purchaseOrder.PromisedDate,
                ReceivedDate = purchaseOrder.ReceivedDate,
                Status = purchaseOrder.Status,
                ApprovedByName = purchaseOrder.ApprovedBy?.FirstName + " " + purchaseOrder.ApprovedBy?.LastName,
                ApprovedAt = purchaseOrder.ApprovedAt,
                SubTotal = purchaseOrder.SubTotal,
                TaxAmount = purchaseOrder.TaxAmount,
                ShippingCost = purchaseOrder.ShippingCost,
                DiscountAmount = purchaseOrder.DiscountAmount,
                TotalAmount = purchaseOrder.TotalAmount,
                PaymentTerms = purchaseOrder.PaymentTerms,
                ShippingTerms = purchaseOrder.ShippingTerms,
                Terms = purchaseOrder.Terms,
                Notes = purchaseOrder.Notes,
                DeliveryWarehouseId = purchaseOrder.DeliveryWarehouseId,
                DeliveryAddress = purchaseOrder.DeliveryAddress,
                DeliveryInstructions = purchaseOrder.DeliveryInstructions,
                SupplierOrderNumber = purchaseOrder.SupplierOrderNumber,
                ReferenceNumber = purchaseOrder.ReferenceNumber,
                ItemCount = items.Count(),
                RequestedByName = purchaseOrder.RequestedBy?.FirstName + " " + purchaseOrder.RequestedBy?.LastName,
                SupplierPhone = purchaseOrder.Supplier?.Phone,
                SupplierEmail = purchaseOrder.Supplier?.Email,
                SupplierAddress = $"{purchaseOrder.Supplier?.Address}, {purchaseOrder.Supplier?.City}, {purchaseOrder.Supplier?.State} {purchaseOrder.Supplier?.ZipCode}",
                Items = items.Select(item => new PurchaseOrderItemDto
                {
                    Id = item.Id,
                    PurchaseOrderId = item.PurchaseOrderId,
                    InventoryItemId = item.InventoryItemId,
                    SupplierItemCode = item.SupplierItemCode,
                    ItemDescription = item.ItemDescription,
                    OrderedQuantity = item.OrderedQuantity,
                    ReceivedQuantity = item.ReceivedQuantity,
                    RemainingQuantity = item.OrderedQuantity - item.ReceivedQuantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal,
                    ExpectedDeliveryDate = item.ExpectedDeliveryDate,
                    Notes = item.Notes,
                    ItemCode = item.InventoryItem?.ItemCode ?? "",
                    ItemName = item.InventoryItem?.Name ?? ""
                }).ToList(),
                Receipts = receipts.Select(receipt => new PurchaseOrderReceiptDto
                {
                    Id = receipt.Id,
                    PurchaseOrderId = receipt.PurchaseOrderId,
                    ReceiptNumber = receipt.ReceiptNumber,
                    ReceiptDate = receipt.ReceiptDate,
                    DeliveryNote = receipt.DeliveryNote,
                    CarrierName = receipt.CarrierName,
                    TrackingNumber = receipt.TrackingNumber,
                    Status = receipt.Status,
                    ReceivedByName = receipt.ReceivedBy?.FirstName + " " + receipt.ReceivedBy?.LastName,
                    InspectedByName = receipt.InspectedBy?.FirstName + " " + receipt.InspectedBy?.LastName,
                    Notes = receipt.Notes,
                    RequiresInspection = receipt.RequiresInspection,
                    InspectionDate = receipt.InspectionDate,
                    InspectionResult = receipt.InspectionResult,
                    InspectionNotes = receipt.InspectionNotes,
                    PurchaseOrderNumber = purchaseOrder.OrderNumber,
                    SupplierName = purchaseOrder.Supplier?.Name ?? ""
                }).ToList()
            };

            return Ok(purchaseOrderDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the purchase order");
        }
    }

    /// <summary>
    /// Creates a new purchase order
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDetailDto>> CreatePurchaseOrder([FromBody] CreatePurchaseOrderDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verify supplier exists
            var supplier = await _supplierRepository.GetSupplierByIdAsync(createDto.SupplierId);
            if (supplier == null)
            {
                return BadRequest($"Supplier with ID {createDto.SupplierId} not found");
            }

            // Generate purchase order number
            var orderNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync();

            // Calculate totals
            var subtotal = createDto.Items.Sum(item => item.OrderedQuantity * item.UnitPrice);
            var totalAmount = subtotal; // Add tax and shipping calculations as needed

            var purchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = orderNumber,
                SupplierId = createDto.SupplierId,
                OrderDate = DateTime.UtcNow,
                RequiredDate = createDto.RequiredDate,
                PromisedDate = createDto.PromisedDate,
                Status = "Draft",
                SubTotal = subtotal,
                TotalAmount = totalAmount,
                PaymentTerms = createDto.PaymentTerms,
                ShippingTerms = createDto.ShippingTerms,
                Terms = createDto.Terms,
                Notes = createDto.Notes,
                DeliveryWarehouseId = createDto.DeliveryWarehouseId,
                DeliveryAddress = createDto.DeliveryAddress,
                DeliveryInstructions = createDto.DeliveryInstructions,
                ReferenceNumber = createDto.ReferenceNumber,
                RequestedById = createDto.RequestedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _purchaseOrderRepository.CreatePurchaseOrderAsync(purchaseOrder);

            // Create purchase order items
            foreach (var itemDto in createDto.Items)
            {
                var item = new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    InventoryItemId = itemDto.InventoryItemId,
                    SupplierItemCode = itemDto.SupplierItemCode,
                    ItemDescription = itemDto.ItemDescription,
                    OrderedQuantity = itemDto.OrderedQuantity,
                    ReceivedQuantity = 0,
                    UnitPrice = itemDto.UnitPrice,
                    LineTotal = itemDto.OrderedQuantity * itemDto.UnitPrice,
                    ExpectedDeliveryDate = itemDto.ExpectedDeliveryDate,
                    Notes = itemDto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _purchaseOrderItemRepository.CreateItemAsync(item);
            }

            // Return the created purchase order
            var createdPurchaseOrder = await GetPurchaseOrderDetailDto(purchaseOrder.Id);
            return CreatedAtAction(nameof(GetPurchaseOrder), new { id = purchaseOrder.Id }, createdPurchaseOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order");
            return StatusCode(500, "An error occurred while creating the purchase order");
        }
    }

    /// <summary>
    /// Updates purchase order status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdatePurchaseOrderStatus(Guid id, [FromBody] UpdateStatusDto statusDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            await _purchaseOrderRepository.UpdateStatusAsync(id, statusDto.Status);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating purchase order status {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while updating purchase order status");
        }
    }

    /// <summary>
    /// Approves a purchase order
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApprovePurchaseOrder(Guid id, [FromBody] ApprovalDto approvalDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            if (purchaseOrder.Status != "Pending Approval" && purchaseOrder.Status != "Draft")
            {
                return BadRequest($"Purchase order cannot be approved in current status: {purchaseOrder.Status}");
            }

            if (approvalDto.Approved)
            {
                await _purchaseOrderRepository.UpdateStatusAsync(id, "Approved");
            }
            else
            {
                await _purchaseOrderRepository.UpdateStatusAsync(id, "Rejected");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while approving the purchase order");
        }
    }

    /// <summary>
    /// Submits a purchase order for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitPurchaseOrder(Guid id)
    {
        try
        {
            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            if (purchaseOrder.Status != "Draft")
            {
                return BadRequest($"Purchase order cannot be submitted in current status: {purchaseOrder.Status}");
            }

            await _purchaseOrderRepository.UpdateStatusAsync(id, "Pending Approval");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while submitting the purchase order");
        }
    }

    /// <summary>
    /// Receives a purchase order (creates receipt)
    /// </summary>
    [HttpPost("{id}/receive")]
    public async Task<ActionResult<PurchaseOrderReceiptDto>> ReceivePurchaseOrder(
        Guid id, 
        [FromBody] ReceivePurchaseOrderDto receiveDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder == null)
            {
                return NotFound($"Purchase order with ID {id} not found");
            }

            if (purchaseOrder.Status != "Approved")
            {
                return BadRequest($"Purchase order cannot be received in current status: {purchaseOrder.Status}");
            }

            // Generate receipt number
            var receiptNumber = await _purchaseOrderReceiptRepository.GenerateReceiptNumberAsync();

            // Create receipt
            var receipt = new PurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = id,
                ReceiptNumber = receiptNumber,
                ReceiptDate = DateTime.UtcNow,
                DeliveryNote = receiveDto.DeliveryNote,
                CarrierName = receiveDto.CarrierName,
                TrackingNumber = receiveDto.TrackingNumber,
                Status = receiveDto.RequiresInspection ? "Pending Inspection" : "Received",
                ReceivedById = receiveDto.ReceivedById,
                InspectedById = receiveDto.InspectedById,
                Notes = receiveDto.Notes,
                RequiresInspection = receiveDto.RequiresInspection,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _purchaseOrderReceiptRepository.CreateReceiptAsync(receipt);

            // Update purchase order items with received quantities
            foreach (var itemDto in receiveDto.Items)
            {
                var poItem = await _purchaseOrderItemRepository.GetItemByIdAsync(itemDto.PurchaseOrderItemId);
                if (poItem != null)
                {
                    poItem.ReceivedQuantity += itemDto.ReceivedQuantity;
                    poItem.UpdatedAt = DateTime.UtcNow;
                    await _purchaseOrderItemRepository.UpdateItemAsync(poItem);

                    // Create receipt item
                    var receiptItem = new PurchaseOrderReceiptItem
                    {
                        Id = Guid.NewGuid(),
                        ReceiptId = receipt.Id,
                        PurchaseOrderItemId = itemDto.PurchaseOrderItemId,
                        ReceivedQuantity = itemDto.ReceivedQuantity,
                        AcceptedQuantity = itemDto.AcceptedQuantity,
                        RejectedQuantity = itemDto.RejectedQuantity,
                        LocationId = itemDto.LocationId,
                        SerialNumber = itemDto.SerialNumber,
                        LotNumber = itemDto.LotNumber,
                        ExpirationDate = itemDto.ExpirationDate,
                        Notes = itemDto.Notes,
                        RejectionReason = itemDto.RejectionReason,
                        QualityStatus = itemDto.QualityStatus,
                        QualityNotes = itemDto.QualityNotes,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _purchaseOrderReceiptRepository.CreateReceiptItemAsync(receiptItem);
                }
            }

            // Check if purchase order is fully received
            var items = await _purchaseOrderItemRepository.GetItemsByPurchaseOrderIdAsync(id);
            var fullyReceived = items.All(item => item.ReceivedQuantity >= item.OrderedQuantity);
            
            if (fullyReceived)
            {
                await _purchaseOrderRepository.UpdateStatusAsync(id, "Received");
                purchaseOrder.ReceivedDate = DateTime.UtcNow;
                await _purchaseOrderRepository.UpdatePurchaseOrderAsync(purchaseOrder);
            }
            else
            {
                await _purchaseOrderRepository.UpdateStatusAsync(id, "Partially Received");
            }

            // Return the created receipt
            var receiptDto = new PurchaseOrderReceiptDto
            {
                Id = receipt.Id,
                PurchaseOrderId = receipt.PurchaseOrderId,
                ReceiptNumber = receipt.ReceiptNumber,
                ReceiptDate = receipt.ReceiptDate,
                DeliveryNote = receipt.DeliveryNote,
                CarrierName = receipt.CarrierName,
                TrackingNumber = receipt.TrackingNumber,
                Status = receipt.Status,
                Notes = receipt.Notes,
                RequiresInspection = receipt.RequiresInspection,
                PurchaseOrderNumber = purchaseOrder.OrderNumber,
                SupplierName = purchaseOrder.Supplier?.Name ?? ""
            };

            return CreatedAtAction(nameof(GetPurchaseOrder), new { id = purchaseOrder.Id }, receiptDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving purchase order {PurchaseOrderId}", id);
            return StatusCode(500, "An error occurred while receiving the purchase order");
        }
    }

    /// <summary>
    /// Gets purchase orders by supplier
    /// </summary>
    [HttpGet("by-supplier/{supplierId}")]
    public async Task<ActionResult<List<PurchaseOrderSummaryDto>>> GetPurchaseOrdersBySupplier(Guid supplierId)
    {
        try
        {
            var purchaseOrders = await _purchaseOrderRepository.GetPurchaseOrdersBySupplierId(supplierId);

            var purchaseOrderDtos = purchaseOrders.Select(po => new PurchaseOrderSummaryDto
            {
                Id = po.Id,
                OrderNumber = po.OrderNumber,
                SupplierId = po.SupplierId,
                SupplierName = po.Supplier?.Name ?? "",
                OrderDate = po.OrderDate,
                RequiredDate = po.RequiredDate,
                PromisedDate = po.PromisedDate,
                Status = po.Status,
                TotalAmount = po.TotalAmount,
                ItemCount = po.Items?.Count ?? 0,
                RequestedByName = po.RequestedBy?.FirstName + " " + po.RequestedBy?.LastName
            }).ToList();

            return Ok(purchaseOrderDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders for supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while retrieving purchase orders");
        }
    }

    /// <summary>
    /// Gets purchase orders by status
    /// </summary>
    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<List<PurchaseOrderSummaryDto>>> GetPurchaseOrdersByStatus(string status)
    {
        try
        {
            var purchaseOrders = await _purchaseOrderRepository.GetPurchaseOrdersByStatus(status);

            var purchaseOrderDtos = purchaseOrders.Select(po => new PurchaseOrderSummaryDto
            {
                Id = po.Id,
                OrderNumber = po.OrderNumber,
                SupplierId = po.SupplierId,
                SupplierName = po.Supplier?.Name ?? "",
                OrderDate = po.OrderDate,
                RequiredDate = po.RequiredDate,
                PromisedDate = po.PromisedDate,
                Status = po.Status,
                TotalAmount = po.TotalAmount,
                ItemCount = po.Items?.Count ?? 0,
                RequestedByName = po.RequestedBy?.FirstName + " " + po.RequestedBy?.LastName
            }).ToList();

            return Ok(purchaseOrderDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders by status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving purchase orders");
        }
    }

    #region Private Helper Methods

    private async Task<PurchaseOrderDetailDto> GetPurchaseOrderDetailDto(Guid purchaseOrderId)
    {
        var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(purchaseOrderId);
        if (purchaseOrder == null) return null!;

        var items = await _purchaseOrderItemRepository.GetItemsByPurchaseOrderIdAsync(purchaseOrderId);
        var receipts = await _purchaseOrderReceiptRepository.GetReceiptsByPurchaseOrderIdAsync(purchaseOrderId);

        return new PurchaseOrderDetailDto
        {
            Id = purchaseOrder.Id,
            OrderNumber = purchaseOrder.OrderNumber,
            SupplierId = purchaseOrder.SupplierId,
            SupplierName = purchaseOrder.Supplier?.Name ?? "",
            OrderDate = purchaseOrder.OrderDate,
            RequiredDate = purchaseOrder.RequiredDate,
            PromisedDate = purchaseOrder.PromisedDate,
            ReceivedDate = purchaseOrder.ReceivedDate,
            Status = purchaseOrder.Status,
            ApprovedByName = purchaseOrder.ApprovedBy?.FirstName + " " + purchaseOrder.ApprovedBy?.LastName,
            ApprovedAt = purchaseOrder.ApprovedAt,
            SubTotal = purchaseOrder.SubTotal,
            TaxAmount = purchaseOrder.TaxAmount,
            ShippingCost = purchaseOrder.ShippingCost,
            DiscountAmount = purchaseOrder.DiscountAmount,
            TotalAmount = purchaseOrder.TotalAmount,
            PaymentTerms = purchaseOrder.PaymentTerms,
            ShippingTerms = purchaseOrder.ShippingTerms,
            Terms = purchaseOrder.Terms,
            Notes = purchaseOrder.Notes,
            DeliveryWarehouseId = purchaseOrder.DeliveryWarehouseId,
            DeliveryAddress = purchaseOrder.DeliveryAddress,
            DeliveryInstructions = purchaseOrder.DeliveryInstructions,
            SupplierOrderNumber = purchaseOrder.SupplierOrderNumber,
            ReferenceNumber = purchaseOrder.ReferenceNumber,
            ItemCount = items.Count(),
            RequestedByName = purchaseOrder.RequestedBy?.FirstName + " " + purchaseOrder.RequestedBy?.LastName,
            SupplierPhone = purchaseOrder.Supplier?.Phone,
            SupplierEmail = purchaseOrder.Supplier?.Email,
            SupplierAddress = $"{purchaseOrder.Supplier?.Address}, {purchaseOrder.Supplier?.City}, {purchaseOrder.Supplier?.State} {purchaseOrder.Supplier?.ZipCode}",
            Items = items.Select(item => new PurchaseOrderItemDto
            {
                Id = item.Id,
                PurchaseOrderId = item.PurchaseOrderId,
                InventoryItemId = item.InventoryItemId,
                SupplierItemCode = item.SupplierItemCode,
                ItemDescription = item.ItemDescription,
                OrderedQuantity = item.OrderedQuantity,
                ReceivedQuantity = item.ReceivedQuantity,
                RemainingQuantity = item.OrderedQuantity - item.ReceivedQuantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal,
                ExpectedDeliveryDate = item.ExpectedDeliveryDate,
                Notes = item.Notes,
                ItemCode = item.InventoryItem?.ItemCode ?? "",
                ItemName = item.InventoryItem?.Name ?? ""
            }).ToList(),
            Receipts = receipts.Select(receipt => new PurchaseOrderReceiptDto
            {
                Id = receipt.Id,
                PurchaseOrderId = receipt.PurchaseOrderId,
                ReceiptNumber = receipt.ReceiptNumber,
                ReceiptDate = receipt.ReceiptDate,
                DeliveryNote = receipt.DeliveryNote,
                CarrierName = receipt.CarrierName,
                TrackingNumber = receipt.TrackingNumber,
                Status = receipt.Status,
                ReceivedByName = receipt.ReceivedBy?.FirstName + " " + receipt.ReceivedBy?.LastName,
                InspectedByName = receipt.InspectedBy?.FirstName + " " + receipt.InspectedBy?.LastName,
                Notes = receipt.Notes,
                RequiresInspection = receipt.RequiresInspection,
                InspectionDate = receipt.InspectionDate,
                InspectionResult = receipt.InspectionResult,
                InspectionNotes = receipt.InspectionNotes,
                PurchaseOrderNumber = purchaseOrder.OrderNumber,
                SupplierName = purchaseOrder.Supplier?.Name ?? ""
            }).ToList()
        };
    }

    #endregion
}