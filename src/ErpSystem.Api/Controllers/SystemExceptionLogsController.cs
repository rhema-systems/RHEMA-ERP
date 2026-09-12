using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/admin/system-exception-logs")]
[Authorize]
public class SystemExceptionLogsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SystemExceptionLogsController> _logger;

    public SystemExceptionLogsController(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<SystemExceptionLogsController> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "AuditGovernanceRead")]
    public async Task<ActionResult<PagedResult<SystemExceptionLogListItemDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? level = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? resolved = null)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var repo = _unitOfWork.Repository<SystemExceptionLog>();

            var query = repo.GetQueryable()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted);

            if (!string.IsNullOrWhiteSpace(level))
            {
                query = query.Where(x => x.Level == level);
            }

            if (resolved.HasValue)
            {
                query = query.Where(x => x.IsResolved == resolved.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    (x.ShortMessage != null && x.ShortMessage.Contains(search)) ||
                    (x.Logger != null && x.Logger.Contains(search)) ||
                    (x.RequestPath != null && x.RequestPath.Contains(search)) ||
                    (x.Username != null && x.Username.Contains(search)) ||
                    (x.TraceId != null && x.TraceId.Contains(search)));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.LastOccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new SystemExceptionLogListItemDto
                {
                    Id = x.Id,
                    Level = x.Level,
                    Fingerprint = x.Fingerprint,
                    OccurrenceCount = x.OccurrenceCount,
                    Logger = x.Logger,
                    ShortMessage = x.ShortMessage,
                    RequestMethod = x.RequestMethod,
                    RequestPath = x.RequestPath,
                    Username = x.Username,
                    TraceId = x.TraceId,
                    IsResolved = x.IsResolved,
                    CreatedAt = x.CreatedAt,
                    FirstOccurredAt = x.FirstOccurredAt,
                    LastOccurredAt = x.LastOccurredAt
                })
                .ToListAsync();

            return Ok(new PagedResult<SystemExceptionLogListItemDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system exception logs");
            return StatusCode(500, "An error occurred while retrieving exception logs");
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AuditGovernanceRead")]
    public async Task<ActionResult<SystemExceptionLogDetailDto>> GetById(Guid id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var repo = _unitOfWork.Repository<SystemExceptionLog>();

            var item = await repo.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted);

            if (item == null) return NotFound();

            return Ok(new SystemExceptionLogDetailDto
            {
                Id = item.Id,
                Level = item.Level,
                Fingerprint = item.Fingerprint,
                OccurrenceCount = item.OccurrenceCount,
                FirstOccurredAt = item.FirstOccurredAt,
                LastOccurredAt = item.LastOccurredAt,
                Logger = item.Logger,
                ShortMessage = item.ShortMessage,
                FullMessage = item.FullMessage,
                ExceptionType = item.ExceptionType,
                StackTrace = item.StackTrace,
                TraceId = item.TraceId,
                RequestMethod = item.RequestMethod,
                RequestPath = item.RequestPath,
                QueryString = item.QueryString,
                ReferrerUrl = item.ReferrerUrl,
                RemoteIpAddress = item.RemoteIpAddress,
                UserAgent = item.UserAgent,
                UserId = item.UserId,
                Username = item.Username,
                IsResolved = item.IsResolved,
                ResolvedAt = item.ResolvedAt,
                ResolvedById = item.ResolvedById,
                ResolutionNotes = item.ResolutionNotes,
                CreatedAt = item.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving exception log {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the exception log");
        }
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = "AuditGovernanceManage")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveExceptionLogDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var userId = _currentUserService.UserId != null && Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
            var repo = _unitOfWork.Repository<SystemExceptionLog>();

            var item = await repo.GetByIdAsync(id);
            if (item == null || item.TenantId != tenantId || item.IsDeleted) return NotFound();

            item.IsResolved = true;
            item.ResolvedAt = DateTime.UtcNow;
            item.ResolvedById = userId;
            item.ResolutionNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            item.UpdatedAt = DateTime.UtcNow;
            item.LastModifiedById = userId;

            await repo.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving exception log {Id}", id);
            return StatusCode(500, "An error occurred while updating the exception log");
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AuditGovernanceManage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var repo = _unitOfWork.Repository<SystemExceptionLog>();
            var item = await repo.GetByIdAsync(id);
            if (item == null || item.TenantId != tenantId || item.IsDeleted) return NotFound();

            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
            await repo.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting exception log {Id}", id);
            return StatusCode(500, "An error occurred while deleting the exception log");
        }
    }

    [HttpPost("clear")]
    [Authorize(Policy = "AuditGovernanceManage")]
    public async Task<IActionResult> Clear()
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var repo = _unitOfWork.Repository<SystemExceptionLog>();
            var list = await repo.FindAsync(x => x.TenantId == tenantId);

            foreach (var item in list)
            {
                if (item.IsDeleted) continue;
                item.IsDeleted = true;
                item.DeletedAt = DateTime.UtcNow;
                await repo.UpdateAsync(item);
            }

            await _unitOfWork.SaveChangesAsync();
            return Ok(new { success = true, count = list.Count() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing exception logs");
            return StatusCode(500, "An error occurred while clearing exception logs");
        }
    }
}

public class ResolveExceptionLogDto
{
    public string? Notes { get; set; }
}

public class SystemExceptionLogListItemDto
{
    public Guid Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public string? Logger { get; set; }
    public string ShortMessage { get; set; } = string.Empty;
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public string? Username { get; set; }
    public string? TraceId { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime FirstOccurredAt { get; set; }
    public DateTime LastOccurredAt { get; set; }
}

public class SystemExceptionLogDetailDto
{
    public Guid Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public DateTime FirstOccurredAt { get; set; }
    public DateTime LastOccurredAt { get; set; }
    public string? Logger { get; set; }
    public string ShortMessage { get; set; } = string.Empty;
    public string? FullMessage { get; set; }
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
    public string? TraceId { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public string? QueryString { get; set; }
    public string? ReferrerUrl { get; set; }
    public string? RemoteIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedById { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}
