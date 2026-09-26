using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>Procurement owns its invoice workspace; AP remains the accounting authority.</summary>
public sealed class ProcurementSupplierInvoiceBoundaryFilter(
    ApplicationDbContext context, ICurrentUserService currentUser) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext action, ActionExecutionDelegate next)
    {
        if (!action.HttpContext.Request.Path.StartsWithSegments("/api/procurement/supplier-invoices"))
        {
            await next();
            return;
        }
        var tenantId = currentUser.GetRequiredFinanceTenantId();
        var token = action.HttpContext.RequestAborted;
        var invoices = context.Set<VendorInvoice>().AsNoTracking().Where(i =>
            i.TenantId == tenantId && !i.IsDeleted && !i.IsOpeningBalance &&
            (i.PurchaseOrderId.HasValue || i.AcceptedSupplyKind.HasValue || i.AutoInvoiceRequestId.HasValue || i.EstateAcquisitionId.HasValue ||
                i.LineItems.Any(l => !l.IsDeleted && l.LandedCostItemId.HasValue)));
        foreach (var query in action.ActionArguments.Values.OfType<VendorInvoiceQueryDto>())
        {
            query.ProcurementOnly = true;
            query.IsOpeningBalance = false;
        }
        if (action.ActionArguments.TryGetValue("id", out var idValue) && idValue is Guid id &&
            !await invoices.AnyAsync(i => i.Id == id, token))
        {
            action.Result = new NotFoundResult(); return;
        }
        if (action.ActionArguments.TryGetValue("invoiceNumber", out var number) &&
            !await invoices.AnyAsync(i => i.InvoiceNumber == (string)number!, token))
        {
            action.Result = new NotFoundResult(); return;
        }
        if (action.ActionArguments.TryGetValue("exceptionId", out var exceptionValue) && exceptionValue is Guid exceptionId &&
            !await context.Set<VendorInvoiceMatchException>().AnyAsync(e => e.Id == exceptionId &&
                e.TenantId == tenantId && !e.IsDeleted && invoices.Any(i => i.Id == e.VendorInvoiceId), token))
        {
            action.Result = new NotFoundResult(); return;
        }
        foreach (var dto in action.ActionArguments.Values.OfType<VendorInvoiceCreateDto>())
        {
            if (dto.IsOpeningBalance || (!dto.PurchaseOrderId.HasValue && !dto.AcceptedSupplyKind.HasValue))
            {
                action.Result = InvalidSource(); return;
            }
        }
        foreach (var dto in action.ActionArguments.Values.OfType<VendorInvoiceUpdateDto>())
        {
            // The route owns identity, just as in the existing AP controller. Never inspect
            // a different body's ID when deciding whether the routed source can change.
            var documentId = idValue is Guid routeId ? routeId : dto.Id;
            var original = await invoices.Include(i => i.LineItems).SingleOrDefaultAsync(i => i.Id == documentId, token);
            if (original == null) { action.Result = new NotFoundResult(); return; }
            if (dto.IsOpeningBalance || dto.PurchaseOrderId != original.PurchaseOrderId ||
                dto.AcceptedSupplyKind != original.AcceptedSupplyKind || dto.AcceptedSupplySourceId != original.AcceptedSupplySourceId)
            {
                action.Result = InvalidSource(); return;
            }
            if (!original.PurchaseOrderId.HasValue && !original.AcceptedSupplyKind.HasValue && !original.AutoInvoiceRequestId.HasValue && !original.EstateAcquisitionId.HasValue &&
                !dto.LineItems.Any(line => original.LineItems.Any(source => !source.IsDeleted && source.LandedCostItemId.HasValue &&
                    (source.Id == line.Id || source.LandedCostItemId == line.LandedCostItemId))))
            {
                action.Result = InvalidSource(); return;
            }
        }
        await next();
    }

    private static ObjectResult InvalidSource() => new(new ProblemDetails
    {
        Status = 422, Title = "Procurement invoice source required",
        Detail = "Use the purchase order or accepted supply source. Source ownership cannot be removed from a supplier invoice.",
        Extensions = { ["code"] = "PROCUREMENT_INVOICE_SOURCE_REQUIRED" }
    }) { StatusCode = 422 };
}
