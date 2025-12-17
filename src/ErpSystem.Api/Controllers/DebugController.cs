using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DebugController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<DebugController> _logger;

    public DebugController(
        ICurrentUserService currentUserService,
        ICurrentUserProvider currentUserProvider,
        ILogger<DebugController> logger)
    {
        _currentUserService = currentUserService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get current user context info for debugging
    /// </summary>
    [HttpGet("current-user")]
    public ActionResult<object> GetCurrentUserInfo()
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
            var tenantId = _currentUserService.TenantId;
            var username = _currentUserService.UserName;
            var isAuthenticated = _currentUserService.IsAuthenticated;

            // Get all claims for debugging
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray();

            var result = new
            {
                IsAuthenticated = isAuthenticated,
                UserId = userId,
                TenantId = tenantId,
                Username = username,
                IsExternalUser = _currentUserProvider.IsExternalUser,
                AuthenticationProvider = _currentUserProvider.AuthenticationProvider,
                Claims = claims
            };

            _logger.LogInformation("Debug user info: {@UserInfo}", result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user info");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// TEMP DEBUG: Check database content directly
    /// </summary>
    [HttpGet("db-check")]
    [AllowAnonymous]
    public async Task<ActionResult> CheckDatabaseContent([FromServices] ApplicationDbContext context)
    {
        try
        {
            var reports = await context.Reports
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.TenantId,
                    r.IsDeleted,
                    r.Status
                })
                .Take(10)
                .ToListAsync();

            var tenants = await context.Tenants
                .Select(t => new
                {
                    t.Id,
                    t.Code,
                    t.Name,
                    t.IsDeleted
                })
                .ToListAsync();

            var roles = await context.Roles
                .Select(r => new
                {
                    r.Id,
                    r.Name
                })
                .ToListAsync();

            var reportRoleAssignments = await context.ReportRoleAssignments
                .Select(rra => new
                {
                    rra.Id,
                    rra.ReportId,
                    rra.RoleId,
                    rra.TenantId,
                    rra.CanRead,
                    rra.IsDeleted
                })
                .Take(10)
                .ToListAsync();

            return Ok(new
            {
                DatabaseConnected = true,
                ReportsCount = reports.Count,
                Reports = reports,
                TenantsCount = tenants.Count,
                Tenants = tenants,
                RolesCount = roles.Count,
                Roles = roles,
                ReportRoleAssignmentsCount = reportRoleAssignments.Count,
                ReportRoleAssignments = reportRoleAssignments
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// TEMP DEBUG: Fix report role assignments for SuperAdmin role
    /// </summary>
    [HttpPost("fix-report-assignments")]
    [AllowAnonymous]
    public async Task<ActionResult> FixReportRoleAssignments([FromServices] ApplicationDbContext context)
    {
        try
        {
            // Get the SuperAdmin role ID
            var superAdminRole = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == "SuperAdmin");
            if (superAdminRole == null)
            {
                return BadRequest("SuperAdmin role not found");
            }

            // Get all reports in TECHSTART tenant
            var techstartTenantId = new Guid("00000000-0000-0000-0000-000000000003");
            var reports = await context.Reports
                .Where(r => r.TenantId == techstartTenantId && !r.IsDeleted)
                .ToListAsync();

            var addedAssignments = new List<object>();

            foreach (var report in reports)
            {
                // Check if assignment already exists
                var existingAssignment = await context.ReportRoleAssignments
                    .FirstOrDefaultAsync(rra => rra.ReportId == report.Id &&
                                               rra.RoleId == superAdminRole.Id &&
                                               rra.TenantId == techstartTenantId);

                if (existingAssignment == null)
                {
                    // Create new assignment for SuperAdmin
                    var newAssignment = new ReportRoleAssignment
                    {
                        Id = Guid.NewGuid(),
                        ReportId = report.Id,
                        RoleId = superAdminRole.Id,
                        TenantId = techstartTenantId,
                        CanRead = true,
                        CanExecute = true,
                        CanExport = true,
                        CanEdit = true,
                        CanSchedule = true,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = "System Debug Fix",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "debug-system"
                    };

                    context.ReportRoleAssignments.Add(newAssignment);
                    addedAssignments.Add(new
                    {
                        ReportName = report.Name,
                        ReportId = report.Id,
                        RoleId = superAdminRole.Id,
                        RoleName = "SuperAdmin",
                        TenantId = techstartTenantId
                    });
                }
            }

            await context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Report role assignments fixed for SuperAdmin",
                SuperAdminRoleId = superAdminRole.Id,
                TechstartTenantId = techstartTenantId,
                ReportsCount = reports.Count,
                AddedAssignments = addedAssignments,
                AssignmentsAdded = addedAssignments.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// TEMP DEBUG: Check detailed report info including queries
    /// </summary>
    [HttpGet("report-details")]
    [AllowAnonymous]
    public async Task<ActionResult> CheckReportDetails([FromServices] ApplicationDbContext context)
    {
        try
        {
            var reportsWithDetails = await context.Reports
                .Where(r => !r.IsDeleted)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.TenantId,
                    r.Status,
                    r.Query,
                    r.Type,
                    r.Description,
                    r.CreatedBy,
                    r.CreatedAt,
                    HasQuery = !string.IsNullOrEmpty(r.Query)
                })
                .ToListAsync();

            return Ok(new
            {
                ReportsCount = reportsWithDetails.Count,
                Reports = reportsWithDetails
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }
}
