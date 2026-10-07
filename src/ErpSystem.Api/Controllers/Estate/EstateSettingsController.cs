using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/settings")]
[Authorize]
public sealed class EstateSettingsController : ControllerBase
{
    public const string SquareMetersPerPlotSettingKey = "Estate:LandSquareMetersPerPlot";
    private const string EstateAdministrationRoles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public EstateSettingsController(ApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet("plots")]
    [Authorize(Roles = EstateAdministrationRoles)]
    public async Task<IActionResult> GetPlotSettings(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var value = await GetSquareMetersPerPlotAsync(_db, tenantId, cancellationToken);
        return Ok(new { success = true, data = new EstatePlotSettingsDto { SquareMetersPerPlot = value } });
    }

    [HttpPut("plots")]
    [Authorize(Roles = EstateAdministrationRoles)]
    public async Task<IActionResult> SavePlotSettings(
        [FromBody] UpdateEstatePlotSettingsDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        if (request.SquareMetersPerPlot is <= 0m)
        {
            return BadRequest(new { success = false, message = "Square meters per plot must be greater than zero." });
        }

        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId
                && item.Key == SquareMetersPerPlotSettingKey,
                cancellationToken);

        if (request.SquareMetersPerPlot is null)
        {
            if (setting is not null)
            {
                setting.Value = string.Empty;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedBy = _currentUserService.UserName;
                setting.LastModifiedById = GetUserId();
            }
        }
        else if (setting is null)
        {
            setting = new SystemSettings
            {
                TenantId = tenantId,
                Key = SquareMetersPerPlotSettingKey,
                Value = request.SquareMetersPerPlot.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
                Description = "Estate land setup: square meters that equal one plot.",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName,
                CreatedById = GetUserId()
            };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.IsDeleted = false;
            setting.DeletedAt = null;
            setting.DeletedBy = null;
            setting.Value = request.SquareMetersPerPlot.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            setting.Description = "Estate land setup: square meters that equal one plot.";
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = _currentUserService.UserName;
            setting.LastModifiedById = GetUserId();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new EstatePlotSettingsDto { SquareMetersPerPlot = request.SquareMetersPerPlot },
            message = "Estate plot size setup saved."
        });
    }

    public static async Task<decimal?> GetSquareMetersPerPlotAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        var value = await db.SystemSettings
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.Key == SquareMetersPerPlotSettingKey
                && !item.IsDeleted)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            && parsed > 0m
                ? parsed
                : null;
    }

    private Guid GetTenantId()
        => _currentUserService.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("Tenant context is required.");

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;
}
