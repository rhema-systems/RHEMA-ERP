using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.DTOs.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ErpSystem.Api.Controllers.Finance
{
    [Authorize]
    [ApiController]
    [Route("api/finance/ap/purchase-orders")]
    public class ApPurchaseOrdersController : ControllerBase
    {
        private readonly IFinancePurchaseOrderService _purchaseOrderService;
        private readonly ICurrentUserService _currentUserService;

        public ApPurchaseOrdersController(IFinancePurchaseOrderService purchaseOrderService, ICurrentUserService currentUserService)
        {
            _purchaseOrderService = purchaseOrderService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FinancePurchaseOrderDto>>> GetAll()
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var orders = await _purchaseOrderService.GetAllAsync(tenantId);
            return Ok(orders.Select(ToDto));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<FinancePurchaseOrderDto>> GetById(Guid id)
        {
            try
            {
                var po = await _purchaseOrderService.GetByIdAsync(id);
                return Ok(ToDto(po));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<FinancePurchaseOrderDto>> Create([FromBody] CreateFinancePurchaseOrderDto dto)
        {
            try
            {
                var created = await _purchaseOrderService.CreateAsync(dto);
                var result = ToDto(created);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private static FinancePurchaseOrderDto ToDto(FinancePurchaseOrder po) => new()
        {
            Id = po.Id,
            OrderNumber = po.OrderNumber,
            VendorId = po.VendorId,
            VendorName = po.Vendor?.PartnerName,
            OrderDate = po.OrderDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            Status = po.Status,
            CurrencyCode = po.CurrencyCode ?? "GHS",
            ExchangeRate = po.ExchangeRate,
            TotalAmount = po.TotalAmount,
            Remarks = po.Remarks,
            TaxGroupId = po.TaxGroupId,
            CreatedAt = po.CreatedAt,
            Items = po.Items?.Select(i => new FinancePurchaseOrderItemDto
            {
                Id = i.Id,
                FinancePurchaseOrderId = i.FinancePurchaseOrderId,
                LineType = i.LineType,
                InventoryItemId = i.InventoryItemId,
                InventoryItemName = i.InventoryItem?.Name,
                WarehouseId = i.WarehouseId,
                GlAccountId = i.GlAccountId,
                GlAccountName = i.GlAccount?.AccountName,
                Description = i.Description,
                OrderedQuantity = i.OrderedQuantity,
                ReceivedQuantity = i.ReceivedQuantity,
                InvoicedQuantity = i.InvoicedQuantity,
                CancelledQuantity = i.CancelledQuantity,
                UnitPrice = i.UnitPrice,
                CurrencyCode = i.CurrencyCode,
                ExchangeRate = i.ExchangeRate,
                TaxCode = i.TaxCode,
                TaxGroupId = i.TaxGroupId,
                TaxRate = i.TaxRate,
                TaxAmount = i.TaxAmount,
                LineTotal = i.LineTotal,
            }).ToList() ?? new(),
        };

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> Approve(Guid id)
        {
            try
            {
                await _purchaseOrderService.ApproveAsync(id);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
