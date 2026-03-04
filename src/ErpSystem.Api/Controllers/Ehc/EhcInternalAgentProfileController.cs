using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/agent-profile")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcInternalAgentProfileController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public EhcInternalAgentProfileController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet("reply")]
    public async Task<ActionResult> GetReplyProfile(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = new EhcAgentReplyProfileDto() });

        if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
            return Ok(new { success = true, data = new EhcAgentReplyProfileDto() });

        var entity = await _db.EhcAgentReplyProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.UserId == userId, cancellationToken);

        if (entity == null)
        {
            return Ok(new
            {
                success = true,
                data = new EhcAgentReplyProfileDto
                {
                    Signature = null,
                    IsSignatureEnabled = true,
                    AppendSignatureToReplies = true
                }
            });
        }

        return Ok(new
        {
            success = true,
            data = new EhcAgentReplyProfileDto
            {
                Signature = entity.Signature,
                IsSignatureEnabled = entity.IsSignatureEnabled,
                AppendSignatureToReplies = entity.AppendSignatureToReplies
            }
        });
    }

    [HttpPut("reply")]
    public async Task<ActionResult> UpdateReplyProfile([FromBody] UpdateEhcAgentReplyProfileRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new UpdateEhcAgentReplyProfileRequestDto();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
            return BadRequest(new { success = false, message = "User context is required." });

        var entity = await _db.EhcAgentReplyProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId, cancellationToken);

        var now = DateTime.UtcNow;
        if (entity == null)
        {
            entity = new EhcAgentReplyProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                Signature = string.IsNullOrWhiteSpace(request.Signature) ? null : request.Signature.Trim(),
                IsSignatureEnabled = request.IsSignatureEnabled,
                AppendSignatureToReplies = request.AppendSignatureToReplies,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName,
                CreatedById = userId
            };

            _db.EhcAgentReplyProfiles.Add(entity);
        }
        else
        {
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedBy = null;
            entity.Signature = string.IsNullOrWhiteSpace(request.Signature) ? null : request.Signature.Trim();
            entity.IsSignatureEnabled = request.IsSignatureEnabled;
            entity.AppendSignatureToReplies = request.AppendSignatureToReplies;
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUserService.UserName;
            entity.LastModifiedById = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new EhcAgentReplyProfileDto
            {
                Signature = entity.Signature,
                IsSignatureEnabled = entity.IsSignatureEnabled,
                AppendSignatureToReplies = entity.AppendSignatureToReplies
            }
        });
    }
}

