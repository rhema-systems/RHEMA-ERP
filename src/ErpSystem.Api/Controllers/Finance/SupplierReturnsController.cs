using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Api.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/ap/supplier-returns")]
public class SupplierReturnsController : ControllerBase
{
    internal const string PlannedBoundaryCode = "FIN-INT-012-013-PLANNED";

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public SupplierReturnsController(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    /// <summary>
    /// Retains read-only access to legacy Finance supplier-return records for audit and migration
    /// review. A row in this register is not evidence that Procurement dispatched the goods or that
    /// Inventory posted the authoritative quantity and valuation movement.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierReturnDto>>> GetAll(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var returns = await BaseQuery(tenantId)
            .OrderByDescending(item => item.ReturnDate)
            .ThenByDescending(item => item.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return Ok(returns.Select(MapToDto).ToList());
    }

    /// <summary>Returns one historical legacy record without changing any source-module state.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierReturnDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplierReturn = await BaseQuery(TenantId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return supplierReturn == null ? NotFound() : Ok(MapToDto(supplierReturn));
    }

    /// <summary>
    /// Quarantined legacy mutation. Procurement owns the approved Return-to-Vendor request/RMA and
    /// dispatch; Inventory owns the outbound quantity and carrying-cost movement; Finance consumes
    /// those immutable facts through FIN-INT-012 and later consumes the independently evidenced
    /// supplier resolution through FIN-INT-013. Finance must not manufacture either producer event.
    /// </summary>
    [HttpPost]
    public ActionResult<SupplierReturnDto> Create(
        [FromBody] CreateSupplierReturnDto dto,
        CancellationToken cancellationToken)
        => LegacyMutationUnavailable("create");

    /// <summary>
    /// Quarantined legacy one-click approval/posting path. Keeping this endpoint fail-closed avoids
    /// implying that one Finance action can approve Procurement evidence, move Inventory and accept
    /// a supplier credit in the same business event.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public ActionResult<SupplierReturnDto> Approve(Guid id, CancellationToken cancellationToken)
        => LegacyMutationUnavailable("approve-and-post");

    private ObjectResult LegacyMutationUnavailable(string operation)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Post-acceptance supplier return is not available",
            Detail =
                "The legacy Finance supplier-return mutation is quarantined. FIN-INT-012 requires an approved Procurement Return-to-Vendor dispatch plus the posted Inventory movement and valuation evidence. FIN-INT-013 separately requires the supplier's evidenced commercial resolution. No return, inventory movement, supplier debit note, tax adjustment, AP application, or journal was created."
        };
        problem.Extensions["code"] = PlannedBoundaryCode;
        problem.Extensions["status"] = "Planned";
        problem.Extensions["operation"] = operation;
        return StatusCode(StatusCodes.Status409Conflict, problem);
    }

    private IQueryable<SupplierReturn> BaseQuery(Guid tenantId)
        => _dbContext.SupplierReturns
            .Include(item => item.LineItems.Where(line => !line.IsDeleted))
            .Include(item => item.OriginalVendorInvoice)
            .Include(item => item.OriginalFinancePurchaseOrderReceipt)
            .Include(item => item.Vendor)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);

    private SupplierReturnDto MapToDto(SupplierReturn supplierReturn)
    {
        // Historical debit-note linkage is exposed for audit only. It does not certify that the
        // agreed Procurement/Inventory producer evidence or FIN-INT-012/013 lifecycle existed.
        var debitNote = _dbContext.SupplierDebitNotes
            .AsNoTracking()
            .Where(item =>
                item.TenantId == supplierReturn.TenantId &&
                item.SupplierReturnId == supplierReturn.Id &&
                !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new SupplierDebitNoteDto
            {
                Id = item.Id,
                DebitNoteNumber = item.DebitNoteNumber,
                JournalEntryId = item.JournalEntryId,
                Status = (int)item.Status,
                StatusName = item.Status.ToString()
            })
            .FirstOrDefault();

        return new SupplierReturnDto
        {
            Id = supplierReturn.Id,
            ReturnNumber = supplierReturn.ReturnNumber,
            VendorId = supplierReturn.VendorId,
            VendorName = supplierReturn.VendorName,
            OriginalVendorInvoiceId = supplierReturn.OriginalVendorInvoiceId,
            OriginalFinancePurchaseOrderReceiptId = supplierReturn.OriginalFinancePurchaseOrderReceiptId,
            OriginalVendorInvoice = supplierReturn.OriginalVendorInvoice == null
                ? null
                : new LinkedVendorInvoiceDto
                {
                    Id = supplierReturn.OriginalVendorInvoice.Id,
                    InvoiceNumber = supplierReturn.OriginalVendorInvoice.InvoiceNumber
                },
            OriginalFinancePurchaseOrderReceipt = supplierReturn.OriginalFinancePurchaseOrderReceipt == null
                ? null
                : new LinkedFinancePurchaseOrderReceiptDto
                {
                    Id = supplierReturn.OriginalFinancePurchaseOrderReceipt.Id,
                    ReceiptNumber = supplierReturn.OriginalFinancePurchaseOrderReceipt.ReceiptNumber
                },
            ReturnDate = supplierReturn.ReturnDate,
            Reason = supplierReturn.Reason,
            CurrencyCode = supplierReturn.CurrencyCode,
            ExchangeRate = supplierReturn.ExchangeRate,
            SubTotal = supplierReturn.SubTotal,
            TaxAmount = supplierReturn.TaxAmount,
            DiscountAmount = supplierReturn.DiscountAmount,
            TotalAmount = supplierReturn.TotalAmount,
            BaseCurrencyAmount = supplierReturn.BaseCurrencyAmount,
            Status = (int)supplierReturn.Status,
            StatusName = supplierReturn.Status.ToString(),
            DebitNote = debitNote,
            LineItems = supplierReturn.LineItems
                .Where(line => !line.IsDeleted)
                .OrderBy(line => line.CreatedAt)
                .Select(line => new SupplierReturnLineItemDto
                {
                    Id = line.Id,
                    OriginalVendorInvoiceLineItemId = line.OriginalVendorInvoiceLineItemId,
                    OriginalFinancePurchaseOrderItemId = line.OriginalFinancePurchaseOrderItemId,
                    Description = line.Description,
                    QuantityReturned = line.QuantityReturned,
                    UnitPrice = line.UnitPrice,
                    TaxGroupId = line.TaxGroupId,
                    TaxRate = line.TaxRate,
                    TaxAmount = line.TaxAmount,
                    DiscountPercentage = line.DiscountPercentage,
                    DiscountAmount = line.DiscountAmount,
                    LineTotal = line.LineTotal
                })
                .ToList(),
            CreatedAt = supplierReturn.CreatedAt
        };
    }
}
