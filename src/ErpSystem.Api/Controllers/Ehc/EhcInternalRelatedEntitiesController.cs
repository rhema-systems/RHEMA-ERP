using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/related-entities")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcInternalRelatedEntitiesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcInternalRelatedEntitiesController> _logger;

    public EhcInternalRelatedEntitiesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcInternalRelatedEntitiesController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet("search")]
    public async Task<ActionResult> Search(
        [FromQuery] string entityType,
        [FromQuery] string q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return Ok(new { success = true, data = Array.Empty<EhcRelatedEntityLookupItemDto>() });
            }

            entityType = (entityType ?? string.Empty).Trim();
            q = (q ?? string.Empty).Trim();
            limit = Math.Clamp(limit, 1, 50);

            if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(q))
            {
                return Ok(new { success = true, data = Array.Empty<EhcRelatedEntityLookupItemDto>() });
            }

            var t = entityType.ToLowerInvariant();

            if (t is "asset" or "vehicle")
            {
                var qry = _db.MaintenanceAssets
                    .AsNoTracking()
                    .Where(a => a.TenantId == tenantId && !a.IsDeleted);

                if (t == "vehicle")
                {
                    qry = qry.Where(a => a.LicensePlate != null || a.VIN != null || a.Mileage != null);
                }

                var rows = await qry
                    .Where(a =>
                        a.Name.Contains(q) ||
                        a.AssetNumber.Contains(q) ||
                        (a.SerialNumber != null && a.SerialNumber.Contains(q)) ||
                        (a.LicensePlate != null && a.LicensePlate.Contains(q)) ||
                        (a.VIN != null && a.VIN.Contains(q)))
                    .OrderBy(a => a.Name)
                    .Take(limit)
                    .Select(a => new EhcRelatedEntityLookupItemDto
                    {
                        Id = a.Id,
                        EntityType = t == "vehicle" ? "Vehicle" : "Asset",
                        Reference = !string.IsNullOrWhiteSpace(a.AssetNumber) ? a.AssetNumber :
                            !string.IsNullOrWhiteSpace(a.LicensePlate) ? a.LicensePlate! :
                            a.Id.ToString(),
                        Label = a.Name,
                        OpenUrl = $"/maintenance/assets?id={a.Id}&edit=1"
                    })
                    .ToListAsync(cancellationToken);

                return Ok(new { success = true, data = rows });
            }

            if (t is "workorder" or "work-order")
            {
                var rows = await _db.WorkOrders
                    .AsNoTracking()
                    .Where(w => w.TenantId == tenantId && !w.IsDeleted)
                    .Where(w => w.WorkOrderNumber.Contains(q) || w.Title.Contains(q))
                    .OrderByDescending(w => w.CreatedAt)
                    .Take(limit)
                    .Select(w => new { w.Id, w.WorkOrderNumber, w.Title })
                    .ToListAsync(cancellationToken);

                var data = rows.Select(w => new EhcRelatedEntityLookupItemDto
                {
                    Id = w.Id,
                    EntityType = "WorkOrder",
                    Reference = w.WorkOrderNumber,
                    Label = w.Title,
                    // Work Orders page supports deep-linking by workOrderNumber (auto-opens details dialog)
                    OpenUrl = $"/maintenance/work-orders?workOrderNumber={w.WorkOrderNumber}"
                }).ToList();

                return Ok(new { success = true, data });
            }

            if (t is "purchaseorder" or "purchase-order" or "order")
            {
                var rows = await _db.PurchaseOrders
                    .AsNoTracking()
                    .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                    .Where(p =>
                        p.OrderNumber.Contains(q) ||
                        (p.ReferenceNumber != null && p.ReferenceNumber.Contains(q)) ||
                        (p.BusinessPartnerOrderNumber != null && p.BusinessPartnerOrderNumber.Contains(q)))
                    .OrderByDescending(p => p.OrderDate)
                    .Take(limit)
                    .Select(p => new { p.Id, p.OrderNumber })
                    .ToListAsync(cancellationToken);

                var data = rows.Select(p => new EhcRelatedEntityLookupItemDto
                {
                    Id = p.Id,
                    EntityType = "PurchaseOrder",
                    Reference = p.OrderNumber,
                    Label = $"Purchase Order {p.OrderNumber}",
                    OpenUrl = $"/procurement/purchase-orders/{p.Id}"
                }).ToList();

                return Ok(new { success = true, data });
            }

            if (t is "purchaserequisition" or "purchase-requisition" or "pr")
            {
                var rows = await _db.PurchaseRequisitions
                    .AsNoTracking()
                    .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                    .Where(p => p.RequisitionNumber.Contains(q))
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(limit)
                    .Select(p => new { p.Id, p.RequisitionNumber })
                    .ToListAsync(cancellationToken);

                var data = rows.Select(p => new EhcRelatedEntityLookupItemDto
                {
                    Id = p.Id,
                    EntityType = "PurchaseRequisition",
                    Reference = p.RequisitionNumber,
                    Label = $"Purchase Requisition {p.RequisitionNumber}",
                    OpenUrl = $"/procurement/purchase-requisitions/{p.Id}"
                }).ToList();

                return Ok(new { success = true, data });
            }

            if (t is "tender" or "rfp" or "itb")
            {
                var rows = await _db.Tenders
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                    .Where(x => x.TenderNumber.Contains(q) || x.Title.Contains(q))
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(limit)
                    .Select(x => new { x.Id, x.TenderNumber, x.Title })
                    .ToListAsync(cancellationToken);

                var data = rows.Select(x => new EhcRelatedEntityLookupItemDto
                {
                    Id = x.Id,
                    EntityType = "Tender",
                    Reference = x.TenderNumber,
                    Label = x.Title,
                    OpenUrl = $"/procurement/tenders/{x.Id}"
                }).ToList();

                return Ok(new { success = true, data });
            }

            if (t is "rfq" or "requestforquotation" or "request-for-quotation")
            {
                var rows = await _db.RequestForQuotations
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                    .Where(x => x.RfqNumber.Contains(q) || x.Title.Contains(q))
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(limit)
                    .Select(x => new { x.Id, x.RfqNumber, x.Title })
                    .ToListAsync(cancellationToken);

                var data = rows.Select(x => new EhcRelatedEntityLookupItemDto
                {
                    Id = x.Id,
                    EntityType = "RFQ",
                    Reference = x.RfqNumber,
                    Label = x.Title,
                    OpenUrl = $"/procurement/rfqs/{x.Id}/edit"
                }).ToList();

                return Ok(new { success = true, data });
            }

            return Ok(new { success = true, data = Array.Empty<EhcRelatedEntityLookupItemDto>() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching related entities");
            return StatusCode(500, new { success = false, message = "Failed to search related entities" });
        }
    }

    [HttpGet("resolve")]
    public async Task<ActionResult> Resolve(
        [FromQuery] string entityType,
        [FromQuery] string reference,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
            }

            entityType = (entityType ?? string.Empty).Trim();
            reference = (reference ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(reference))
            {
                return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
            }

            var t = entityType.ToLowerInvariant();

            if (t is "asset" or "vehicle")
            {
                IQueryable<MaintenanceAsset> qry = _db.MaintenanceAssets.AsNoTracking()
                    .Where(a => a.TenantId == tenantId && !a.IsDeleted);

                if (t == "vehicle")
                {
                    qry = qry.Where(a => a.LicensePlate != null || a.VIN != null || a.Mileage != null);
                }

                var refUpper = reference.ToUpperInvariant();
                var asset = await qry.FirstOrDefaultAsync(a =>
                    a.AssetNumber.ToUpper() == refUpper ||
                    (a.LicensePlate != null && a.LicensePlate.ToUpper() == refUpper) ||
                    (a.VIN != null && a.VIN.ToUpper() == refUpper) ||
                    (a.SerialNumber != null && a.SerialNumber.ToUpper() == refUpper), cancellationToken);

                if (asset == null)
                {
                    return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
                }

                var resolvedRef = !string.IsNullOrWhiteSpace(asset.AssetNumber) ? asset.AssetNumber :
                    !string.IsNullOrWhiteSpace(asset.LicensePlate) ? asset.LicensePlate! :
                    reference;

                return Ok(new
                {
                    success = true,
                    data = new EhcRelatedEntityResolveDto
                    {
                        Exists = true,
                        Id = asset.Id,
                        EntityType = t == "vehicle" ? "Vehicle" : "Asset",
                        Reference = resolvedRef,
                        Label = asset.Name,
                        OpenUrl = $"/maintenance/assets?id={asset.Id}&edit=1"
                    }
                });
            }

            if (t is "workorder" or "work-order")
            {
                var w = await _db.WorkOrders.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.WorkOrderNumber == reference, cancellationToken);
                if (w == null)
                {
                    return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
                }

                return Ok(new
                {
                    success = true,
                    data = new EhcRelatedEntityResolveDto
                    {
                        Exists = true,
                        Id = w.Id,
                        EntityType = "WorkOrder",
                        Reference = w.WorkOrderNumber,
                        Label = w.Title,
                        // Work Orders page supports deep-linking by workOrderNumber (auto-opens details dialog)
                        OpenUrl = $"/maintenance/work-orders?workOrderNumber={w.WorkOrderNumber}"
                    }
                });
            }

            if (t is "purchaseorder" or "purchase-order" or "order")
            {
                var p = await _db.PurchaseOrders.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.OrderNumber == reference, cancellationToken);
                if (p == null)
                {
                    return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
                }

                return Ok(new
                {
                    success = true,
                    data = new EhcRelatedEntityResolveDto
                    {
                        Exists = true,
                        Id = p.Id,
                        EntityType = "PurchaseOrder",
                        Reference = p.OrderNumber,
                        Label = $"Purchase Order {p.OrderNumber}",
                        OpenUrl = $"/procurement/purchase-orders/{p.Id}"
                    }
                });
            }

            if (t is "purchaserequisition" or "purchase-requisition" or "pr")
            {
                var p = await _db.PurchaseRequisitions.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.RequisitionNumber == reference, cancellationToken);
                if (p == null)
                {
                    return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
                }

                return Ok(new
                {
                    success = true,
                    data = new EhcRelatedEntityResolveDto
                    {
                        Exists = true,
                        Id = p.Id,
                        EntityType = "PurchaseRequisition",
                        Reference = p.RequisitionNumber,
                        Label = $"Purchase Requisition {p.RequisitionNumber}",
                        OpenUrl = $"/procurement/purchase-requisitions/{p.Id}"
                    }
                });
            }

            if (t is "tender" or "rfp" or "itb")
            {
                var x = await _db.Tenders.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.TenantId == tenantId && !v.IsDeleted && v.TenderNumber == reference, cancellationToken);
                if (x == null)
                {
                    return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
                }

                return Ok(new
                {
                    success = true,
                    data = new EhcRelatedEntityResolveDto
                    {
                        Exists = true,
                        Id = x.Id,
                        EntityType = "Tender",
                        Reference = x.TenderNumber,
                        Label = x.Title,
                        OpenUrl = $"/procurement/tenders/{x.Id}"
                    }
                });
            }

            if (t is "rfq" or "requestforquotation" or "request-for-quotation")
            {
                var x = await _db.RequestForQuotations.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.TenantId == tenantId && !v.IsDeleted && v.RfqNumber == reference, cancellationToken);
                if (x == null)
                {
                    return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
                }

                return Ok(new
                {
                    success = true,
                    data = new EhcRelatedEntityResolveDto
                    {
                        Exists = true,
                        Id = x.Id,
                        EntityType = "RFQ",
                        Reference = x.RfqNumber,
                        Label = x.Title,
                        OpenUrl = $"/procurement/rfqs/{x.Id}/edit"
                    }
                });
            }

            return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving related entity");
            return StatusCode(500, new { success = false, message = "Failed to resolve related entity" });
        }
    }
}
