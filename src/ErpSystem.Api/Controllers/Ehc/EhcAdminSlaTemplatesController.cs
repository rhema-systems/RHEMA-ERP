using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Ehc.Sla;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/sla-templates")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminSlaTemplatesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcAdminSlaTemplatesController> _logger;

    public EhcAdminSlaTemplatesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcAdminSlaTemplatesController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcSlaTemplateDto>() });
        }

        var items = await _db.EhcSlaTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Include(t => t.Category)
            .OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var data = items.Select(t => new EhcSlaTemplateDto
        {
            Id = t.Id,
            Name = t.Name,
            IsActive = t.IsActive,
            TicketType = t.TicketType,
            Priority = t.Priority,
            CategoryId = t.CategoryId,
            CategoryName = t.Category?.Name,
            FirstResponseMinutes = t.FirstResponseMinutes,
            ResolutionMinutes = t.ResolutionMinutes,
            CalendarConfigurationJson = t.CalendarConfigurationJson
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateEhcSlaTemplateRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { success = false, message = "Name is required." });
            }

            string? normalizedCalendarJson = null;
            if (!string.IsNullOrWhiteSpace(request.CalendarConfigurationJson))
            {
                if (!EhcSlaCalendarConfiguration.TryParseAndNormalize(request.CalendarConfigurationJson, out var normalized, out var calendarError))
                {
                    return BadRequest(new { success = false, message = calendarError });
                }
                normalizedCalendarJson = normalized;
            }

            var now = DateTime.UtcNow;
            var tpl = new EhcSlaTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = request.Name.Trim(),
                IsActive = request.IsActive,
                TicketType = request.TicketType,
                Priority = request.Priority,
                CategoryId = request.CategoryId,
                FirstResponseMinutes = Math.Max(1, request.FirstResponseMinutes),
                ResolutionMinutes = Math.Max(1, request.ResolutionMinutes),
                CalendarConfigurationJson = normalizedCalendarJson,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            _db.EhcSlaTemplates.Add(tpl);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new EhcSlaTemplateDto
                {
                    Id = tpl.Id,
                    Name = tpl.Name,
                    IsActive = tpl.IsActive,
                    TicketType = tpl.TicketType,
                    Priority = tpl.Priority,
                    CategoryId = tpl.CategoryId,
                    CategoryName = null,
                    FirstResponseMinutes = tpl.FirstResponseMinutes,
                    ResolutionMinutes = tpl.ResolutionMinutes,
                    CalendarConfigurationJson = tpl.CalendarConfigurationJson
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC SLA template");
            return StatusCode(500, new { success = false, message = "Failed to create SLA template" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEhcSlaTemplateRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var tpl = await _db.EhcSlaTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken);
            if (tpl == null)
            {
                return NotFound(new { success = false, message = "SLA template not found" });
            }

            string? normalizedCalendarJson = null;
            if (!string.IsNullOrWhiteSpace(request.CalendarConfigurationJson))
            {
                if (!EhcSlaCalendarConfiguration.TryParseAndNormalize(request.CalendarConfigurationJson, out var normalized, out var calendarError))
                {
                    return BadRequest(new { success = false, message = calendarError });
                }
                normalizedCalendarJson = normalized;
            }

            tpl.Name = request.Name.Trim();
            tpl.IsActive = request.IsActive;
            tpl.TicketType = request.TicketType;
            tpl.Priority = request.Priority;
            tpl.CategoryId = request.CategoryId;
            tpl.FirstResponseMinutes = Math.Max(1, request.FirstResponseMinutes);
            tpl.ResolutionMinutes = Math.Max(1, request.ResolutionMinutes);
            tpl.CalendarConfigurationJson = normalizedCalendarJson;
            tpl.UpdatedAt = DateTime.UtcNow;
            tpl.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating EHC SLA template {TemplateId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update SLA template" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var tpl = await _db.EhcSlaTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken);
            if (tpl == null)
            {
                return NotFound(new { success = false, message = "SLA template not found" });
            }

            tpl.IsDeleted = true;
            tpl.DeletedAt = DateTime.UtcNow;
            tpl.DeletedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EHC SLA template {TemplateId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete SLA template" });
        }
    }
}

