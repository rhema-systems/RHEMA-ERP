using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/workflow-routing-rules")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminWorkflowRoutingRulesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcAdminWorkflowRoutingRulesController> _logger;

    public EhcAdminWorkflowRoutingRulesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcAdminWorkflowRoutingRulesController> logger)
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
            return Ok(new { success = true, data = Array.Empty<EhcWorkflowRoutingRuleDto>() });
        }

        var items = await _db.EhcWorkflowRoutingRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Category)
            .Include(r => r.Subcategory)
            .Include(r => r.AssignedDepartment)
            .OrderByDescending(r => r.IsActive)
            .ThenByDescending(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var data = items.Select(r => new EhcWorkflowRoutingRuleDto
        {
            Id = r.Id,
            Name = r.Name,
            IsActive = r.IsActive,
            Priority = r.Priority,
            WorkflowName = r.WorkflowName,
            TicketType = r.TicketType,
            TicketPriority = r.TicketPriority,
            CategoryId = r.CategoryId,
            CategoryName = r.Category?.Name,
            SubcategoryId = r.SubcategoryId,
            SubcategoryName = r.Subcategory?.Name,
            AssignedDepartmentId = r.AssignedDepartmentId,
            AssignedDepartmentName = r.AssignedDepartment?.Name
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateEhcWorkflowRoutingRuleRequestDto request, CancellationToken cancellationToken)
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

            if (string.IsNullOrWhiteSpace(request.WorkflowName))
            {
                return BadRequest(new { success = false, message = "WorkflowName is required." });
            }

            var now = DateTime.UtcNow;
            var rule = new EhcWorkflowRoutingRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = request.Name.Trim(),
                IsActive = request.IsActive,
                Priority = request.Priority,
                WorkflowName = request.WorkflowName.Trim(),
                TicketType = request.TicketType,
                TicketPriority = request.TicketPriority,
                CategoryId = request.CategoryId,
                SubcategoryId = request.SubcategoryId,
                AssignedDepartmentId = request.AssignedDepartmentId,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            _db.EhcWorkflowRoutingRules.Add(rule);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, data = new { id = rule.Id } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC workflow routing rule");
            return StatusCode(500, new { success = false, message = "Failed to create workflow routing rule" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEhcWorkflowRoutingRuleRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var rule = await _db.EhcWorkflowRoutingRules.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);
            if (rule == null)
            {
                return NotFound(new { success = false, message = "Workflow routing rule not found" });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { success = false, message = "Name is required." });
            }

            if (string.IsNullOrWhiteSpace(request.WorkflowName))
            {
                return BadRequest(new { success = false, message = "WorkflowName is required." });
            }

            rule.Name = request.Name.Trim();
            rule.IsActive = request.IsActive;
            rule.Priority = request.Priority;
            rule.WorkflowName = request.WorkflowName.Trim();
            rule.TicketType = request.TicketType;
            rule.TicketPriority = request.TicketPriority;
            rule.CategoryId = request.CategoryId;
            rule.SubcategoryId = request.SubcategoryId;
            rule.AssignedDepartmentId = request.AssignedDepartmentId;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating EHC workflow routing rule {RuleId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update workflow routing rule" });
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

            var rule = await _db.EhcWorkflowRoutingRules.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);
            if (rule == null)
            {
                return NotFound(new { success = false, message = "Workflow routing rule not found" });
            }

            if (rule.IsDeleted)
            {
                return Ok(new { success = true });
            }

            rule.IsDeleted = true;
            rule.DeletedAt = DateTime.UtcNow;
            rule.DeletedBy = _currentUserService.UserName ?? "System";
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EHC workflow routing rule {RuleId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete workflow routing rule" });
        }
    }
}

