using System.Security.Claims;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/landed-costs")]
[Authorize]
public class LandedCostsController : ControllerBase
{
    private readonly ILandedCostService _landedCostService;
    private readonly ILogger<LandedCostsController> _logger;
    private readonly ApplicationDbContext? _db;
    private readonly ICurrentUserService? _currentUser;
    private readonly IVendorInvoiceService? _invoices;
    private readonly IProcurementAccessControlService? _access;
    private readonly ICurrentUserProvider? _actor;

    public LandedCostsController(
        ILandedCostService landedCostService,
        ILogger<LandedCostsController> logger,
        ApplicationDbContext? db = null,
        ICurrentUserService? currentUser = null,
        IVendorInvoiceService? invoices = null,
        IProcurementAccessControlService? access = null,
        ICurrentUserProvider? actor = null)
    {
        _landedCostService = landedCostService;
        _logger = logger;
        _db = db;
        _currentUser = currentUser;
        _invoices = invoices; _access = access; _actor = actor;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LandedCostDto>>> GetAll()
    {
        try
        {
            var landedCosts = await _landedCostService.GetAllAsync();
            return Ok(landedCosts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landed costs");
            return StatusCode(500, "An error occurred while retrieving landed costs");
        }
    }

    [HttpGet("by-grn/{grnId}")]
    public async Task<ActionResult<IEnumerable<LandedCostDto>>> GetByGrn(Guid grnId, [FromQuery] bool ensure = false)
    {
        try
        {
            _logger.LogInformation("Get landed costs by GRN {GrnId} (ensure={Ensure})", grnId, ensure);
            var landedCosts = (await _landedCostService.GetByGRNAsync(grnId)).ToList();
            // NOTE:
            // We intentionally do NOT auto-create landed cost vouchers on GET (even if ensure=true).
            // In ERP workflows, document creation should be an explicit user action or a defined business event
            // (e.g., GRN posting), not a side-effect of opening a screen.

            _logger.LogInformation("Returned {Count} landed cost voucher(s) for GRN {GrnId}", landedCosts.Count, grnId);
            return Ok(landedCosts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landed costs for GRN {GrnId}", grnId);
            return StatusCode(500, "An error occurred while retrieving landed costs");
        }
    }

    [HttpGet("by-po/{purchaseOrderId:guid}")]
    public async Task<ActionResult<List<LandedCostDetailDto>>> GetByPurchaseOrder(Guid purchaseOrderId)
        => Ok(await _landedCostService.GetByPurchaseOrderAsync(purchaseOrderId));

    [HttpGet("by-invoice/{invoiceId:guid}")]
    public async Task<ActionResult<List<LandedCostDetailDto>>> GetByInvoice(Guid invoiceId)
    {
        try { return Ok(await _landedCostService.GetByInvoiceAsync(invoiceId)); }
        catch (ArgumentException ex) { return NotFound(ex.Message); }
    }

    public sealed class InvoiceLinkRequest { public Guid InvoiceId { get; set; } }

    [HttpPut("{id:guid}/items/{itemId:guid}/invoice")]
    public async Task<ActionResult<LandedCostDetailDto>> LinkInvoice(Guid id, Guid itemId, InvoiceLinkRequest request)
    {
        // Linking is an AP-authorized reference action. No invoice amount, approval or posting is changed.
        if (_db == null || _currentUser?.TenantId == null || GetCurrentUserId() == Guid.Empty) return Forbid();
        var allowed = _currentUser.IsInRole("SuperAdmin") || _currentUser.IsInRole("TenantAdmin");
        if (!allowed)
        {
            var names = await _db.UserRoles.Where(r => r.UserId == GetCurrentUserId())
                .SelectMany(r => r.Role.RolePermissions.Select(p => p.Permission.Name)).ToListAsync();
            allowed = names.Any(p => new[] { "Finance.AP.Invoices.Edit", "Finance.AP.Invoices.Write", "Finance.AP.Invoices.Create" }
                .Contains(p, StringComparer.OrdinalIgnoreCase));
        }
        if (!allowed) return Forbid();
        try { return Ok(await _landedCostService.LinkInvoiceAsync(id, itemId, request.InvoiceId, GetCurrentUserId())); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LandedCostDetailDto>> GetById(Guid id)
    {
        try
        {
            var landedCost = await _landedCostService.GetByIdAsync(id);
            if (landedCost == null)
                return NotFound($"Landed cost with ID {id} not found");

            return Ok(landedCost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landed cost {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the landed cost");
        }
    }

    [HttpPost]
    public async Task<ActionResult<LandedCostDto>> Create([FromBody] CreateLandedCostDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var landedCost = await _landedCostService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = landedCost.Id }, landedCost);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating landed cost");
            return StatusCode(500, "An error occurred while creating the landed cost");
        }
    }

    [HttpPut("{id}/draft")]
    public async Task<ActionResult<LandedCostDto>> UpdateDraft(Guid id, [FromBody] UpdateLandedCostDto dto)
    {
        try
        {
            return Ok(await _landedCostService.UpdateDraftAsync(id, dto, GetCurrentUserId()));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("initialize-from-po/{grnId}")]
    public async Task<ActionResult<LandedCostDto>> InitializeFromPo(Guid grnId)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Initialize landed cost from PO plan for GRN {GrnId} by {UserId}", grnId, userId);
            var landedCost = await _landedCostService.InitializeFromPurchaseOrderPlanAsync(grnId, userId);
            _logger.LogInformation("Initialized landed cost {LandedCostId} ({LandedCostNumber}) for GRN {GrnId}", landedCost.Id, landedCost.LandedCostNumber, grnId);
            return Ok(landedCost);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing landed cost from PO plan for GRN {GrnId}", grnId);
            return StatusCode(500, "An error occurred while initializing landed cost");
        }
    }

    [HttpPost("{id}/allocate")]
    public async Task<ActionResult> Allocate(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.AllocateCostsAsync(id, userId);
            if (!ok) return BadRequest("Allocation failed");
            return Ok(new { message = "Landed costs allocated successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating landed cost {Id}", id);
            return StatusCode(500, "An error occurred while allocating landed cost");
        }
    }

    [HttpPut("{landedCostId}/items/{landedCostItemId}/manual-allocations")]
    public async Task<ActionResult> SetManualAllocations(Guid landedCostId, Guid landedCostItemId, [FromBody] SetManualLandedCostAllocationsDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.SetManualAllocationsAsync(landedCostId, landedCostItemId, dto, userId);
            if (!ok) return BadRequest("Failed to set manual allocations");
            return Ok(new { message = "Manual allocations saved successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting manual allocations for landed cost {LandedCostId} item {ItemId}", landedCostId, landedCostItemId);
            return StatusCode(500, "An error occurred while saving manual allocations");
        }
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult> Approve(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.ApproveAsync(id, userId);
            if (!ok) return BadRequest("Approval failed");
            return Ok(new { message = "Landed cost approved successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving landed cost {Id}", id);
            return StatusCode(500, "An error occurred while approving landed cost");
        }
    }

    // Receiving permission permits only source-bound draft generation as a posting
    // side effect. It does not grant manual AP creation, tax edit, approval or payment.
    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<PostLandedCostResultDto>> Post(Guid id, PostLandedCostDto dto)
    {
        if (_db == null || _invoices == null || _access == null || _actor == null ||
            _actor.IsExternalUser || _actor.UserId == Guid.Empty || _actor.TenantId == Guid.Empty) return Forbid();
        var source = await _db.LandedCosts.AsNoTracking().Where(c =>
            c.Id == id && c.TenantId == _actor.TenantId && !c.IsDeleted &&
            c.GoodsReceiptNote.TenantId == _actor.TenantId && !c.GoodsReceiptNote.IsDeleted)
            .Select(c => new { c.LandedCostNumber, c.GoodsReceiptNote.WarehouseId }).SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (source == null) return NotFound();
        try
        {
            var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.receive", WarehouseId = source.WarehouseId,
                SourceType = "InventoryLandedCost", SourceReference = source.LandedCostNumber
            }, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
            if (!decision.Allowed) return StatusCode(403, new { message = decision.Message });
            return Ok(await _invoices.PostLandedCostAsync(id, dto,
                new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceApVendorInvoice), HttpContext.RequestAborted));
        }
        catch (ProcurementAccessAuthorizationException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { code = "LANDED_COST_BILLING_REQUIRED", message = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { code = "LANDED_COST_POST_NOT_READY", message = ex.Message }); }
    }

    [HttpPost("{id}/post-to-inventory")]
    public async Task<ActionResult> PostToInventory(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.PostToInventoryAsync(id, userId);
            if (!ok) return BadRequest("Posting failed");
            return Ok(new { message = "Landed cost posted to inventory successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting landed cost {Id} to inventory", id);
            return StatusCode(500, "An error occurred while posting landed cost to inventory");
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, [FromQuery] string reason)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.CancelAsync(id, reason, userId);
            if (!ok) return BadRequest("Cancel failed");
            return Ok(new { message = "Landed cost cancelled successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling landed cost {Id}", id);
            return StatusCode(500, "An error occurred while cancelling landed cost");
        }
    }
}
