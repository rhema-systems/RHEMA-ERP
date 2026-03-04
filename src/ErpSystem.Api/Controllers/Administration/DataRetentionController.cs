using ErpSystem.Core.DTOs.Compliance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Administration;

[ApiController]
[Route("api/admin/retention")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles = "SuperAdmin,TenantAdmin")]
public sealed class DataRetentionController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DataRetentionController> _logger;

    public DataRetentionController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<DataRetentionController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet("policy")]
    public async Task<ActionResult> GetPolicy(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = (DataRetentionPolicyDto?)null });
        }

        var policy = await _db.DataRetentionPolicies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

        if (policy == null)
        {
            policy = new DataRetentionPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Enabled = true,
                AuditLogRetentionDays = 365,
                SecurityLogRetentionDays = 365,
                NotificationRetentionDays = 180,
                EhcAuditEventRetentionDays = 365,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            _db.DataRetentionPolicies.Add(policy);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            success = true,
            data = new DataRetentionPolicyDto
            {
                Id = policy.Id,
                TenantId = policy.TenantId,
                Enabled = policy.Enabled,
                AuditLogRetentionDays = policy.AuditLogRetentionDays,
                SecurityLogRetentionDays = policy.SecurityLogRetentionDays,
                NotificationRetentionDays = policy.NotificationRetentionDays,
                EhcAuditEventRetentionDays = policy.EhcAuditEventRetentionDays
            }
        });
    }

    [HttpPut("policy")]
    public async Task<ActionResult> UpdatePolicy([FromBody] UpdateDataRetentionPolicyRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new UpdateDataRetentionPolicyRequestDto();

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var policy = await _db.DataRetentionPolicies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

            var now = DateTime.UtcNow;
            if (policy == null)
            {
                policy = new DataRetentionPolicy
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName ?? "System"
                };
                _db.DataRetentionPolicies.Add(policy);
            }

            policy.Enabled = request.Enabled;
            policy.AuditLogRetentionDays = Math.Clamp(request.AuditLogRetentionDays, 1, 3650);
            policy.SecurityLogRetentionDays = Math.Clamp(request.SecurityLogRetentionDays, 1, 3650);
            policy.NotificationRetentionDays = Math.Clamp(request.NotificationRetentionDays, 1, 3650);
            policy.EhcAuditEventRetentionDays = Math.Clamp(request.EhcAuditEventRetentionDays, 1, 3650);
            policy.UpdatedAt = now;
            policy.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating data retention policy");
            return StatusCode(500, new { success = false, message = "Failed to update policy" });
        }
    }

    [HttpGet("runs")]
    public async Task<ActionResult> ListRuns([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<DataRetentionJobRunDto>() });
        }

        take = Math.Clamp(take, 1, 200);

        var runs = await _db.DataRetentionJobRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(take)
            .Select(r => new DataRetentionJobRunDto
            {
                Id = r.Id,
                TenantId = r.TenantId,
                JobName = r.JobName,
                StartedAtUtc = r.StartedAtUtc,
                CompletedAtUtc = r.CompletedAtUtc,
                Success = r.Success,
                CountsJson = r.CountsJson,
                Error = r.Error
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = runs });
    }
}

