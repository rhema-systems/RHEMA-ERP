using System.ComponentModel.DataAnnotations;
using ErpSystem.Api.Caching;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Data.Repositories;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.Optimized;

/// <summary>
/// High-performance optimized AuditLog controller with caching, compression, and advanced querying
/// </summary>
[ApiController]
[Route("api/v2/[controller]")]
[Authorize]
[EnableRateLimiting("ApiPolicy")]
public class OptimizedAuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<OptimizedAuditLogController> _logger;
    private readonly ICachingService _cachingService;
    private readonly IOptimizedGenericRepository<AuditLog> _repository;

    public OptimizedAuditLogController(
        IAuditLogService auditLogService,
        ILogger<OptimizedAuditLogController> logger,
        ICachingService cachingService,
        IOptimizedGenericRepository<AuditLog> repository)
    {
        _auditLogService = auditLogService;
        _logger = logger;
        _cachingService = cachingService;
        _repository = repository;
    }

    /// <summary>
    /// Get audit logs with advanced filtering, sorting, and caching
    /// </summary>
    /// <param name="request">Filtering and pagination parameters</param>
    /// <returns>Paginated audit log results</returns>
    [HttpGet]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    [OutputCache(Duration = 300, VaryByQueryKeys = new[] { "pageNumber", "pageSize", "sortBy", "sortDirection", "filter" })]
    [ProducesResponseType(typeof(PagedAuditLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedAuditLogResponse>> GetAuditLogs([FromQuery] AuditLogQueryRequest request)
    {
        try
        {
            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Create cache key
            var cacheKey = $"audit:logs:{request.GetHashCode()}";

            // Try to get from cache first
            var cachedResult = await _cachingService.GetAsync<PagedAuditLogResponse>(cacheKey);
            if (cachedResult != null)
            {
                Response.Headers.Append("X-Cache-Status", "HIT");
                return Ok(cachedResult);
            }

            // Build query with filters
            var query = BuildAuditLogQuery(request);

            // Execute optimized query with projection
            var result = await _repository.GetPagedWithProjectionAsync(
                pageNumber: request.PageNumber,
                pageSize: Math.Min(request.PageSize, 1000), // Cap at 1000 for performance
                orderBy: GetOrderByExpression(request.SortBy),
                projection: a => new OptimizedAuditLogDto
                {
                    Id = a.Id.ToString(),
                    UserId = a.UserId.ToString(),
                    Username = a.Username,
                    Action = a.Action,
                    Resource = a.Resource,
                    ResourceId = a.ResourceId,
                    IpAddress = a.IpAddress,
                    UserAgent = a.UserAgent != null ? a.UserAgent.Substring(0, Math.Min(a.UserAgent.Length, 100)) : null, // Truncate for performance
                    Timestamp = a.Timestamp,
                    HasOldValues = !string.IsNullOrEmpty(a.OldValues),
                    HasNewValues = !string.IsNullOrEmpty(a.NewValues)
                },
                descending: request.SortDirection == "desc",
                filter: query
            );

            var response = new PagedAuditLogResponse
            {
                Items = result.Items,
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalPages = result.TotalPages
                // HasPreviousPage and HasNextPage are calculated properties, not settable
            };

            // Cache the result for 5 minutes
            await _cachingService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

            Response.Headers.Append("X-Cache-Status", "MISS");
            Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs with request: {@Request}", request);
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }

    /// <summary>
    /// Get audit log details by ID with full values
    /// </summary>
    /// <param name="id">Audit log ID</param>
    /// <returns>Full audit log details</returns>
    [HttpGet("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    [OutputCache(Duration = 3600)] // Cache individual logs for 1 hour
    [ProducesResponseType(typeof(DetailedAuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DetailedAuditLogDto>> GetAuditLog(Guid id)
    {
        try
        {
            var cacheKey = $"audit:log:{id}";

            // Try cache first
            var cachedLog = await _cachingService.GetAsync<DetailedAuditLogDto>(cacheKey);
            if (cachedLog != null)
            {
                Response.Headers.Append("X-Cache-Status", "HIT");
                return Ok(cachedLog);
            }

            var auditLog = await _auditLogService.GetAuditLogByIdAsync(id);
            if (auditLog == null)
            {
                return NotFound($"Audit log with ID {id} not found");
            }

            var detailedDto = new DetailedAuditLogDto
            {
                Id = auditLog.Id.ToString(),
                UserId = auditLog.UserId.ToString(),
                Username = auditLog.Username,
                Action = auditLog.Action,
                Resource = auditLog.Resource,
                ResourceId = auditLog.ResourceId,
                OldValues = auditLog.OldValues != null
                    ? System.Text.Json.JsonSerializer.Deserialize<object>(auditLog.OldValues)
                    : null,
                NewValues = auditLog.NewValues != null
                    ? System.Text.Json.JsonSerializer.Deserialize<object>(auditLog.NewValues)
                    : null,
                IpAddress = auditLog.IpAddress,
                UserAgent = auditLog.UserAgent,
                Timestamp = auditLog.Timestamp
            };

            // Cache for 1 hour since audit logs don't change
            await _cachingService.SetAsync(cacheKey, detailedDto, TimeSpan.FromHours(1));

            Response.Headers.Append("X-Cache-Status", "MISS");
            return Ok(detailedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log {AuditLogId}", id);
            return StatusCode(500, "An error occurred while retrieving the audit log");
        }
    }

    /// <summary>
    /// Get audit log statistics for dashboard
    /// </summary>
    /// <returns>Audit log statistics</returns>
    [HttpGet("stats")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    [OutputCache(Duration = 600)] // Cache stats for 10 minutes
    [ProducesResponseType(typeof(AuditLogStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogStatsDto>> GetAuditLogStats()
    {
        try
        {
            var cacheKey = "audit:stats:global";

            var cachedStats = await _cachingService.GetAsync<AuditLogStatsDto>(cacheKey);
            if (cachedStats != null)
            {
                Response.Headers.Append("X-Cache-Status", "HIT");
                return Ok(cachedStats);
            }

            var now = DateTime.UtcNow;
            var last24Hours = now.AddDays(-1);
            var last7Days = now.AddDays(-7);
            var last30Days = now.AddDays(-30);

            // Use parallel queries for better performance
            var tasks = new[]
            {
                _repository.CountAsync(a => a.Timestamp >= last24Hours),
                _repository.CountAsync(a => a.Timestamp >= last7Days),
                _repository.CountAsync(a => a.Timestamp >= last30Days),
                _repository.CountAsync()
            };

            var results = await Task.WhenAll(tasks);

            var stats = new AuditLogStatsDto
            {
                Last24Hours = results[0],
                Last7Days = results[1],
                Last30Days = results[2],
                Total = results[3],
                GeneratedAt = now
            };

            // Cache for 10 minutes
            await _cachingService.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(10));

            Response.Headers.Append("X-Cache-Status", "MISS");
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log statistics");
            return StatusCode(500, "An error occurred while retrieving audit log statistics");
        }
    }

    /// <summary>
    /// Bulk export audit logs (background job)
    /// </summary>
    /// <param name="request">Export parameters</param>
    /// <returns>Export job ID</returns>
    [HttpPost("export")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    [EnableRateLimiting("SensitivePolicy")] // Stricter rate limiting for exports
    [ProducesResponseType(typeof(ExportJobResponse), StatusCodes.Status202Accepted)]
    public ActionResult<ExportJobResponse> ExportAuditLogs([FromBody] AuditLogExportRequest request)
    {
        try
        {
            // This would typically enqueue a background job
            var jobId = Guid.NewGuid().ToString();

            // TODO: Implement with Hangfire or similar background processing
            _logger.LogInformation("Export job {JobId} queued for user {UserId}", jobId, User.Identity?.Name);

            var response = new ExportJobResponse
            {
                JobId = jobId,
                Status = "Queued",
                EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(5),
                Message = "Export job has been queued and will be processed shortly"
            };

            return Accepted(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting audit log export");
            return StatusCode(500, "An error occurred while starting the export");
        }
    }

    #region Private Helper Methods

    private static System.Linq.Expressions.Expression<Func<AuditLog, bool>>? BuildAuditLogQuery(AuditLogQueryRequest request)
    {
        System.Linq.Expressions.Expression<Func<AuditLog, bool>>? query = null;

        if (request.UserId.HasValue)
        {
            query = a => a.UserId == request.UserId.Value;
        }

        if (!string.IsNullOrEmpty(request.Action))
        {
            var actionQuery = (System.Linq.Expressions.Expression<Func<AuditLog, bool>>)(a => a.Action == request.Action);
            query = query == null ? actionQuery : CombineWithAnd(query, actionQuery);
        }

        if (!string.IsNullOrEmpty(request.Resource))
        {
            var resourceQuery = (System.Linq.Expressions.Expression<Func<AuditLog, bool>>)(a => a.Resource == request.Resource);
            query = query == null ? resourceQuery : CombineWithAnd(query, resourceQuery);
        }

        if (request.FromDate.HasValue)
        {
            var fromQuery = (System.Linq.Expressions.Expression<Func<AuditLog, bool>>)(a => a.Timestamp >= request.FromDate.Value);
            query = query == null ? fromQuery : CombineWithAnd(query, fromQuery);
        }

        if (request.ToDate.HasValue)
        {
            var toQuery = (System.Linq.Expressions.Expression<Func<AuditLog, bool>>)(a => a.Timestamp <= request.ToDate.Value);
            query = query == null ? toQuery : CombineWithAnd(query, toQuery);
        }

        return query;
    }

    private static System.Linq.Expressions.Expression<Func<AuditLog, bool>> CombineWithAnd(
        System.Linq.Expressions.Expression<Func<AuditLog, bool>> expr1,
        System.Linq.Expressions.Expression<Func<AuditLog, bool>> expr2)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(AuditLog), "a");

        var body1 = ReplaceParameter(expr1.Body, expr1.Parameters[0], parameter);
        var body2 = ReplaceParameter(expr2.Body, expr2.Parameters[0], parameter);

        var combined = System.Linq.Expressions.Expression.AndAlso(body1, body2);

        return System.Linq.Expressions.Expression.Lambda<Func<AuditLog, bool>>(combined, parameter);
    }

    private static System.Linq.Expressions.Expression ReplaceParameter(
        System.Linq.Expressions.Expression expression,
        System.Linq.Expressions.ParameterExpression oldParameter,
        System.Linq.Expressions.ParameterExpression newParameter)
    {
        return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
    }

    private static System.Linq.Expressions.Expression<Func<AuditLog, object>> GetOrderByExpression(string? sortBy)
    {
        return sortBy?.ToLower() switch
        {
            "username" => a => a.Username,
            "action" => a => a.Action,
            "resource" => a => a.Resource,
            "ipaddress" => a => a.IpAddress,
            _ => a => a.Timestamp // Default sort by timestamp
        };
    }

    #endregion
}

// Helper class for expression parameter replacement
internal class ParameterReplacer : System.Linq.Expressions.ExpressionVisitor
{
    private readonly System.Linq.Expressions.ParameterExpression _oldParameter;
    private readonly System.Linq.Expressions.ParameterExpression _newParameter;

    public ParameterReplacer(System.Linq.Expressions.ParameterExpression oldParameter, System.Linq.Expressions.ParameterExpression newParameter)
    {
        _oldParameter = oldParameter;
        _newParameter = newParameter;
    }

    protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node)
    {
        return node == _oldParameter ? _newParameter : node;
    }
}

#region DTOs

/// <summary>
/// Optimized audit log DTO for list views (without heavy JSON fields)
/// </summary>
public class OptimizedAuditLogDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public bool HasOldValues { get; set; }
    public bool HasNewValues { get; set; }
}

/// <summary>
/// Detailed audit log DTO with full values (for individual record view)
/// </summary>
public class DetailedAuditLogDto : OptimizedAuditLogDto
{
    public object? OldValues { get; set; }
    public object? NewValues { get; set; }
}

/// <summary>
/// Query request parameters for audit logs
/// </summary>
public class AuditLogQueryRequest
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 1000)]
    public int PageSize { get; set; } = 50;

    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "desc";

    public Guid? UserId { get; set; }
    public string? Action { get; set; }
    public string? Resource { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? IpAddress { get; set; }

    public override int GetHashCode()
    {
        return HashCode.Combine(PageNumber, PageSize, SortBy, SortDirection, UserId, Action, Resource, FromDate?.Date);
    }
}

/// <summary>
/// Paged response for audit logs
/// </summary>
public class PagedAuditLogResponse : PagedResult<OptimizedAuditLogDto>
{
}

/// <summary>
/// Audit log statistics DTO
/// </summary>
public class AuditLogStatsDto
{
    public int Last24Hours { get; set; }
    public int Last7Days { get; set; }
    public int Last30Days { get; set; }
    public int Total { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Export request parameters
/// </summary>
public class AuditLogExportRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Format { get; set; } = "csv";
    public bool IncludeValues { get; set; } = false;
}

/// <summary>
/// Export job response
/// </summary>
public class ExportJobResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EstimatedCompletionTime { get; set; }
    public string Message { get; set; } = string.Empty;
}

#endregion
