using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportRoleAssignmentController : ControllerBase
{
    private readonly IReportRoleAssignmentRepository _roleAssignmentRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IRoleService _roleService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReportRoleAssignmentController> _logger;

    public ReportRoleAssignmentController(
        IReportRoleAssignmentRepository roleAssignmentRepository,
        IReportRepository reportRepository,
        IRoleService roleService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<ReportRoleAssignmentController> logger)
    {
        _roleAssignmentRepository = roleAssignmentRepository;
        _reportRepository = reportRepository;
        _roleService = roleService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Get all role assignments for a specific report
    /// </summary>
    [HttpGet("report/{reportId}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<List<ReportRoleAssignmentDto>>> GetReportRoleAssignments(Guid reportId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            
            // Verify report exists and belongs to tenant
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null || report.TenantId != tenantId)
                return NotFound("Report not found");

            var assignments = await _roleAssignmentRepository.GetAssignmentsByReportAsync(reportId, tenantId);
            
            var assignmentDtos = assignments.Select(a => new ReportRoleAssignmentDto
            {
                Id = a.Id,
                ReportId = a.ReportId,
                ReportName = report.Name,
                RoleId = a.RoleId,
                RoleName = a.Role?.Name ?? "Unknown Role",
                CanRead = a.CanRead,
                CanExecute = a.CanExecute,
                CanExport = a.CanExport,
                CanEdit = a.CanEdit,
                CanSchedule = a.CanSchedule,
                AssignedAt = a.AssignedAt,
                AssignedBy = a.AssignedBy
            }).ToList();

            return Ok(assignmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role assignments for report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while retrieving role assignments");
        }
    }

    /// <summary>
    /// Get all role assignments for a specific role
    /// </summary>
    [HttpGet("role/{roleId}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<List<ReportRoleAssignmentDto>>> GetRoleReportAssignments(Guid roleId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            
            // Verify role exists
            var role = await _roleService.GetRoleByIdAsync(roleId);
            if (role == null)
                return NotFound("Role not found");

            var assignments = await _roleAssignmentRepository.GetAssignmentsByRoleAsync(roleId, tenantId);
            
            var assignmentDtos = assignments.Select(a => new ReportRoleAssignmentDto
            {
                Id = a.Id,
                ReportId = a.ReportId,
                ReportName = a.Report?.Name ?? "Unknown Report",
                RoleId = a.RoleId,
                RoleName = role?.Name ?? "Unknown Role",
                CanRead = a.CanRead,
                CanExecute = a.CanExecute,
                CanExport = a.CanExport,
                CanEdit = a.CanEdit,
                CanSchedule = a.CanSchedule,
                AssignedAt = a.AssignedAt,
                AssignedBy = a.AssignedBy
            }).ToList();

            return Ok(assignmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report assignments for role {RoleId}", roleId);
            return StatusCode(500, "An error occurred while retrieving report assignments");
        }
    }

    /// <summary>
    /// Create a new role assignment for a report
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<ReportRoleAssignmentDto>> CreateRoleAssignment([FromBody] CreateReportRoleAssignmentDto createDto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("Invalid user context"));
            
            // Verify report exists and belongs to tenant
            var report = await _reportRepository.GetByIdAsync(createDto.ReportId);
            if (report == null || report.TenantId != tenantId)
                return BadRequest("Report not found or does not belong to your tenant");

            // Verify role exists
            var role = await _roleService.GetRoleByIdAsync(createDto.RoleId);
            if (role == null)
                return BadRequest("Role not found");

            // Check if assignment already exists
            var existingAssignment = await _roleAssignmentRepository.GetAssignmentAsync(createDto.ReportId, createDto.RoleId, tenantId);
            if (existingAssignment != null)
                return BadRequest("Role assignment already exists for this report and role");

            var assignment = new ReportRoleAssignment
            {
                ReportId = createDto.ReportId,
                RoleId = createDto.RoleId,
                CanRead = createDto.CanRead,
                CanExecute = createDto.CanExecute,
                CanExport = createDto.CanExport,
                CanEdit = createDto.CanEdit,
                CanSchedule = createDto.CanSchedule,
                TenantId = tenantId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = _currentUserService.UserName,
                CreatedBy = userId.ToString()
            };

            await _roleAssignmentRepository.AddAsync(assignment);
            await _unitOfWork.SaveChangesAsync();

            // Log audit trail
            await LogRoleAssignmentAction("Create", assignment, report.Name, role?.Name ?? "Unknown Role");

            var assignmentDto = new ReportRoleAssignmentDto
            {
                Id = assignment.Id,
                ReportId = assignment.ReportId,
                ReportName = report.Name,
                RoleId = assignment.RoleId,
                RoleName = role?.Name ?? "Unknown Role",
                CanRead = assignment.CanRead,
                CanExecute = assignment.CanExecute,
                CanExport = assignment.CanExport,
                CanEdit = assignment.CanEdit,
                CanSchedule = assignment.CanSchedule,
                AssignedAt = assignment.AssignedAt,
                AssignedBy = assignment.AssignedBy
            };

            return CreatedAtAction(nameof(GetReportRoleAssignments), new { reportId = createDto.ReportId }, assignmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role assignment");
            return StatusCode(500, "An error occurred while creating the role assignment");
        }
    }

    /// <summary>
    /// Update an existing role assignment
    /// </summary>
    [HttpPut("{assignmentId}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<ReportRoleAssignmentDto>> UpdateRoleAssignment(Guid assignmentId, [FromBody] UpdateReportRoleAssignmentDto updateDto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("Invalid user context"));
            
            var assignment = await _roleAssignmentRepository.GetByIdAsync(assignmentId);
            if (assignment == null || assignment.TenantId != tenantId)
                return NotFound("Role assignment not found");

            // Store old values for audit
            var oldValues = new
            {
                CanRead = assignment.CanRead,
                CanExecute = assignment.CanExecute,
                CanExport = assignment.CanExport,
                CanEdit = assignment.CanEdit,
                CanSchedule = assignment.CanSchedule
            };

            assignment.CanRead = updateDto.CanRead;
            assignment.CanExecute = updateDto.CanExecute;
            assignment.CanExport = updateDto.CanExport;
            assignment.CanEdit = updateDto.CanEdit;
            assignment.CanSchedule = updateDto.CanSchedule;
            assignment.UpdatedAt = DateTime.UtcNow;
            assignment.UpdatedBy = userId.ToString();

            await _roleAssignmentRepository.UpdateAsync(assignment);
            await _unitOfWork.SaveChangesAsync();

            // Get related entities for response
            var report = await _reportRepository.GetByIdAsync(assignment.ReportId);
            var role = await _roleService.GetRoleByIdAsync(assignment.RoleId);

            // Log audit trail
            await LogRoleAssignmentAction("Update", assignment, report?.Name ?? "Unknown", role?.Name ?? "Unknown", oldValues);

            var assignmentDto = new ReportRoleAssignmentDto
            {
                Id = assignment.Id,
                ReportId = assignment.ReportId,
                ReportName = report?.Name ?? "Unknown Report",
                RoleId = assignment.RoleId,
                RoleName = role?.Name ?? "Unknown Role",
                CanRead = assignment.CanRead,
                CanExecute = assignment.CanExecute,
                CanExport = assignment.CanExport,
                CanEdit = assignment.CanEdit,
                CanSchedule = assignment.CanSchedule,
                AssignedAt = assignment.AssignedAt,
                AssignedBy = assignment.AssignedBy
            };

            return Ok(assignmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role assignment {AssignmentId}", assignmentId);
            return StatusCode(500, "An error occurred while updating the role assignment");
        }
    }

    /// <summary>
    /// Delete a role assignment
    /// </summary>
    [HttpDelete("{assignmentId}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteRoleAssignment(Guid assignmentId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("Invalid user context"));
            
            var assignment = await _roleAssignmentRepository.GetByIdAsync(assignmentId);
            if (assignment == null || assignment.TenantId != tenantId)
                return NotFound("Role assignment not found");

            // Get related entities for audit logging
            var report = await _reportRepository.GetByIdAsync(assignment.ReportId);
            var role = await _roleService.GetRoleByIdAsync(assignment.RoleId);

            assignment.IsDeleted = true;
            assignment.DeletedAt = DateTime.UtcNow;
            assignment.DeletedBy = userId.ToString();

            await _roleAssignmentRepository.UpdateAsync(assignment);
            await _unitOfWork.SaveChangesAsync();

            // Log audit trail
            await LogRoleAssignmentAction("Delete", assignment, report?.Name ?? "Unknown", role?.Name ?? "Unknown");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role assignment {AssignmentId}", assignmentId);
            return StatusCode(500, "An error occurred while deleting the role assignment");
        }
    }

    /// <summary>
    /// Bulk assign roles to a report
    /// </summary>
    [HttpPost("bulk-assign")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<List<ReportRoleAssignmentDto>>> BulkAssignRoles([FromBody] BulkAssignRolesToReportDto bulkDto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("Invalid user context"));
            
            // Verify report exists
            var report = await _reportRepository.GetByIdAsync(bulkDto.ReportId);
            if (report == null || report.TenantId != tenantId)
                return BadRequest("Report not found or does not belong to your tenant");

            var createdAssignments = new List<ReportRoleAssignmentDto>();

            foreach (var assignmentDto in bulkDto.RoleAssignments)
            {
                // Skip if assignment already exists
                var existing = await _roleAssignmentRepository.GetAssignmentAsync(assignmentDto.ReportId, assignmentDto.RoleId, tenantId);
                if (existing != null)
                    continue;

                // Verify role exists
                var role = await _roleService.GetRoleByIdAsync(assignmentDto.RoleId);
                if (role == null)
                    continue;

                var assignment = new ReportRoleAssignment
                {
                    ReportId = assignmentDto.ReportId,
                    RoleId = assignmentDto.RoleId,
                    CanRead = assignmentDto.CanRead,
                    CanExecute = assignmentDto.CanExecute,
                    CanExport = assignmentDto.CanExport,
                    CanEdit = assignmentDto.CanEdit,
                    CanSchedule = assignmentDto.CanSchedule,
                    TenantId = tenantId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = _currentUserService.UserName,
                    CreatedBy = userId.ToString()
                };

                await _roleAssignmentRepository.AddAsync(assignment);

                createdAssignments.Add(new ReportRoleAssignmentDto
                {
                    Id = assignment.Id,
                    ReportId = assignment.ReportId,
                    ReportName = report.Name,
                    RoleId = assignment.RoleId,
                    RoleName = role?.Name ?? "Unknown Role",
                    CanRead = assignment.CanRead,
                    CanExecute = assignment.CanExecute,
                    CanExport = assignment.CanExport,
                    CanEdit = assignment.CanEdit,
                    CanSchedule = assignment.CanSchedule,
                    AssignedAt = assignment.AssignedAt,
                    AssignedBy = assignment.AssignedBy
                });
            }

            await _unitOfWork.SaveChangesAsync();

            // Log audit trail for bulk assignment
            await LogBulkRoleAssignmentAction(report.Name, createdAssignments.Count);

            return Ok(createdAssignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk role assignment for report {ReportId}", bulkDto.ReportId);
            return StatusCode(500, "An error occurred while creating bulk role assignments");
        }
    }

    /// <summary>
    /// Get role assignments for a specific report (accessible to all authenticated users)
    /// </summary>
    [HttpGet("report/{reportId}/assignments")]
    public async Task<ActionResult<List<ReportRoleAssignmentDto>>> GetReportRoleAssignmentsForUsers(Guid reportId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            _logger.LogInformation("Getting role assignments for report {ReportId} in tenant {TenantId}", reportId, tenantId);
            
            // Verify report exists and belongs to tenant
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null || report.TenantId != tenantId)
            {
                _logger.LogWarning("Report {ReportId} not found or doesn't belong to tenant {TenantId}", reportId, tenantId);
                return NotFound("Report not found");
            }

            var assignments = await _roleAssignmentRepository.GetAssignmentsByReportAsync(reportId, tenantId);
            _logger.LogInformation("Found {AssignmentCount} role assignments for report {ReportId}", assignments.Count(), reportId);
            
            var assignmentDtos = assignments.Select(a => {
                _logger.LogDebug("Processing assignment {AssignmentId}: Role {RoleId} ({RoleName}) -> Report {ReportId}", 
                    a.Id, a.RoleId, a.Role?.Name ?? "NULL", a.ReportId);
                return new ReportRoleAssignmentDto
                {
                    Id = a.Id,
                    ReportId = a.ReportId,
                    ReportName = report.Name,
                    RoleId = a.RoleId,
                    RoleName = a.Role?.Name ?? "Unknown Role",
                    CanRead = a.CanRead,
                    CanExecute = a.CanExecute,
                    CanExport = a.CanExport,
                    CanEdit = a.CanEdit,
                    CanSchedule = a.CanSchedule,
                    AssignedAt = a.AssignedAt,
                    AssignedBy = a.AssignedBy
                };
            }).ToList();

            _logger.LogInformation("Returning {DtoCount} role assignment DTOs for report {ReportId}", assignmentDtos.Count, reportId);
            return Ok(assignmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role assignments for report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while retrieving role assignments");
        }
    }

    /// <summary>
    /// Get all role assignments for current tenant (accessible to all authenticated users)
    /// </summary>
    [HttpGet("tenant/assignments")]
    public async Task<ActionResult<List<ReportRoleAssignmentDto>>> GetAllTenantRoleAssignments()
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            
            // Get all role assignments for the tenant
            var allAssignments = await _roleAssignmentRepository.FindAsync(rra => rra.TenantId == tenantId && !rra.IsDeleted);
            
            var assignmentDtos = allAssignments.Select(a => new ReportRoleAssignmentDto
            {
                Id = a.Id,
                ReportId = a.ReportId,
                ReportName = a.Report?.Name ?? "Unknown Report",
                RoleId = a.RoleId,
                RoleName = a.Role?.Name ?? "Unknown Role",
                CanRead = a.CanRead,
                CanExecute = a.CanExecute,
                CanExport = a.CanExport,
                CanEdit = a.CanEdit,
                CanSchedule = a.CanSchedule,
                AssignedAt = a.AssignedAt,
                AssignedBy = a.AssignedBy
            }).ToList();

            return Ok(assignmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all role assignments for tenant");
            return StatusCode(500, "An error occurred while retrieving role assignments");
        }
    }

    /// <summary>
    /// Get user's access permissions for a specific report
    /// </summary>
    [HttpGet("access/{reportId}")]
    public async Task<ActionResult<ReportAccessDto>> GetReportAccess(Guid reportId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("Invalid user context"));
            
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null || report.TenantId != tenantId)
                return NotFound("Report not found");

            var hasRead = await _roleAssignmentRepository.HasReportAccessAsync(reportId, userId, tenantId, "read");
            var hasExecute = await _roleAssignmentRepository.HasReportAccessAsync(reportId, userId, tenantId, "execute");
            var hasExport = await _roleAssignmentRepository.HasReportAccessAsync(reportId, userId, tenantId, "export");
            var hasEdit = await _roleAssignmentRepository.HasReportAccessAsync(reportId, userId, tenantId, "edit");
            var hasSchedule = await _roleAssignmentRepository.HasReportAccessAsync(reportId, userId, tenantId, "schedule");

            // Get user's accessible roles for this report (for informational purposes)
            var accessibleReportIds = await _roleAssignmentRepository.GetAccessibleReportIdsForUserAsync(userId, tenantId);
            var userRoles = new List<string>(); // This would come from user's role assignments

            var accessDto = new ReportAccessDto
            {
                ReportId = reportId,
                ReportName = report.Name,
                HasAccess = hasRead || hasExecute || hasExport || hasEdit || hasSchedule,
                CanRead = hasRead,
                CanExecute = hasExecute,
                CanExport = hasExport,
                CanEdit = hasEdit,
                CanSchedule = hasSchedule,
                AccessibleRoles = userRoles
            };

            return Ok(accessDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking report access for {ReportId}", reportId);
            return StatusCode(500, "An error occurred while checking report access");
        }
    }

    private async Task LogRoleAssignmentAction(string action, ReportRoleAssignment assignment, string reportName, string roleName, object? oldValues = null)
    {
        try
        {
            await _auditLogService.LogUserActionAsync(
                Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("Invalid user context")),
                _currentUserService.UserName ?? "Unknown",
                action,
                "ReportRoleAssignment",
                assignment.Id.ToString(),
                oldValues,
                new
                {
                    ReportName = reportName,
                    RoleName = roleName,
                    Permissions = new
                    {
                        assignment.CanRead,
                        assignment.CanExecute,
                        assignment.CanExport,
                        assignment.CanEdit,
                        assignment.CanSchedule
                    }
                },
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                HttpContext.Request.Headers["User-Agent"].ToString()
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log audit trail for role assignment action");
        }
    }

    private async Task LogBulkRoleAssignmentAction(string reportName, int assignmentCount)
    {
        try
        {
            await _auditLogService.LogUserActionAsync(
                Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("Invalid user context")),
                _currentUserService.UserName ?? "Unknown",
                "BulkAssign",
                "ReportRoleAssignment",
                reportName,
                null,
                new { AssignmentCount = assignmentCount },
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                HttpContext.Request.Headers["User-Agent"].ToString()
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log audit trail for bulk role assignment action");
        }
    }

    /// <summary>
    /// Debug endpoint to create a test role assignment (Development only)
    /// </summary>
    [HttpPost("debug/create-test-assignment")]
    public async Task<ActionResult> CreateTestRoleAssignment()
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
            var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("Invalid user context"));
            
            // Get first report and first role for testing
            var reports = await _reportRepository.FindAsync(r => r.TenantId == tenantId && !r.IsDeleted);
            var firstReport = reports.FirstOrDefault();
            if (firstReport == null)
                return BadRequest("No reports found in tenant");

            // Get the Employee role (assuming it exists)
            var employeeRole = await _roleService.GetRoleByNameAsync("Employee");
            if (employeeRole == null)
                return BadRequest("Employee role not found");

            // Check if assignment already exists
            var existingAssignment = await _roleAssignmentRepository.GetAssignmentAsync(firstReport.Id, employeeRole.Id, tenantId);
            if (existingAssignment != null)
                return BadRequest("Test assignment already exists");

            var assignment = new ReportRoleAssignment
            {
                ReportId = firstReport.Id,
                RoleId = employeeRole.Id,
                CanRead = true,
                CanExecute = true,
                CanExport = false,
                CanEdit = false,
                CanSchedule = false,
                TenantId = tenantId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = _currentUserService.UserName,
                CreatedBy = userId.ToString()
            };

            await _roleAssignmentRepository.AddAsync(assignment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created test role assignment: Report {ReportName} -> Role {RoleName}", firstReport.Name, employeeRole.Name);
            
            return Ok(new { 
                message = "Test role assignment created successfully",
                reportId = firstReport.Id,
                reportName = firstReport.Name,
                roleId = employeeRole.Id,
                roleName = employeeRole.Name,
                assignmentId = assignment.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test role assignment");
            return StatusCode(500, "An error occurred while creating test role assignment");
        }
    }
}
