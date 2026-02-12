using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/external/related-entities")]
[Authorize(Policy = "ExternalOnly")]
public sealed class EhcExternalRelatedEntitiesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcExternalRelatedEntitiesController> _logger;

    public EhcExternalRelatedEntitiesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcExternalRelatedEntitiesController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    // External portal safe validation (no search/listing)
    [HttpGet("validate")]
    public async Task<ActionResult> Validate(
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
            if (t is not ("asset" or "vehicle"))
            {
                // For phase 1, external users can only validate tenant-scoped assets/vehicles without exposing listings.
                return Ok(new { success = true, data = new EhcRelatedEntityResolveDto { Exists = false } });
            }

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
                    Label = null,
                    OpenUrl = null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating related entity");
            return StatusCode(500, new { success = false, message = "Failed to validate related entity" });
        }
    }
}

