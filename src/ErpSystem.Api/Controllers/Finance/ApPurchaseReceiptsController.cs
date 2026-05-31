using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.DTOs.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ErpSystem.Api.Controllers.Finance
{
    [Authorize]
    [ApiController]
    [Route("api/finance/ap")]
    public class ApPurchaseReceiptsController : ControllerBase
    {
        private readonly IFinancePurchaseOrderReceiptService _receiptService;

        public ApPurchaseReceiptsController(IFinancePurchaseOrderReceiptService receiptService)
        {
            _receiptService = receiptService;
        }

        [HttpGet("purchase-receipts")]
        public async Task<ActionResult<IEnumerable<FinancePurchaseOrderReceipt>>> GetAll()
        {
            return Ok(await _receiptService.GetAllAsync());
        }

        [HttpGet("purchase-receipts/{id}")]
        public async Task<ActionResult<FinancePurchaseOrderReceipt>> GetReceiptById(Guid id)
        {
            try
            {
                var receipt = await _receiptService.GetByIdAsync(id);
                return Ok(receipt);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost("purchase-receipts")]
        public async Task<ActionResult<FinancePurchaseOrderReceipt>> CreateReceiptGlobal([FromBody] CreateFinancePurchaseReceiptDto dto)
        {
            try
            {
                var created = await _receiptService.ReceiveAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("purchase-orders/{poId}/receipts")]
        public async Task<ActionResult<FinancePurchaseOrderReceipt>> CreateReceipt(Guid poId, [FromBody] FinancePurchaseOrderReceipt receipt)
        {
            try
            {
                receipt.FinancePurchaseOrderId = poId;
                receipt.TenantId = Guid.Empty; // Placeholder for tenant context
                
                var created = await _receiptService.ReceiveAsync(receipt);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("purchase-orders/{poId}/receipts")]
        public async Task<ActionResult<IEnumerable<FinancePurchaseOrderReceipt>>> GetByPurchaseOrderId(Guid poId)
        {
            var receipts = await _receiptService.GetByPurchaseOrderIdAsync(poId);
            return Ok(receipts);
        }

        [HttpGet("receipts/{id}")]
        public async Task<ActionResult<FinancePurchaseOrderReceipt>> GetById(Guid id)
        {
            try
            {
                var receipt = await _receiptService.GetByIdAsync(id);
                return Ok(receipt);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost("invoices/from-receipt/{receiptId}")]
        public async Task<IActionResult> ConvertToInvoice(Guid receiptId)
        {
            try
            {
                var userId = Guid.Empty; // Placeholder
                var invoice = await _receiptService.ConvertToVendorInvoiceAsync(receiptId, userId);
                return Ok(invoice);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
