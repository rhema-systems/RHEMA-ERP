using System.Text.Json;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/workflow/governance")]
[Authorize]
public sealed class WorkflowGovernanceController : ControllerBase
{
    private static readonly string[] AdminRoles = ["SystemAdmin", "WorkflowAdmin", "SuperAdmin", "TenantAdmin"];
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowRuntimeGovernanceService _governance;

    public WorkflowGovernanceController(ApplicationDbContext db, ICurrentUserService currentUser,
        IWorkflowRuntimeGovernanceService governance)
    {
        _db = db;
        _currentUser = currentUser;
        _governance = governance;
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var query = _db.Users.AsNoTracking().Where(user => user.TenantId == tenantId && user.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.FirstName.Contains(term) || user.LastName.Contains(term) ||
                (user.Email != null && user.Email.Contains(term)) || (user.UserName != null && user.UserName.Contains(term)));
        }
        var users = await query.OrderBy(user => user.FirstName).ThenBy(user => user.LastName).Take(100)
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.Email, user.UserName })
            .ToListAsync(cancellationToken);
        return Ok(new { success = true, data = users });
    }

    [HttpGet("delegation-scope-options")]
    public async Task<IActionResult> DelegationScopeOptions(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var modules = await _db.ModuleDefinitions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.ModuleName)
            .Select(item => new
            {
                item.Id,
                code = item.ModuleCode,
                name = item.ModuleName,
                item.Description
            })
            .ToListAsync(cancellationToken);

        var entityTypes = await _db.WorkflowEntityTypes.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.Name,
                item.Description,
                item.DisplayOrder,
                item.Icon,
                item.ColorCode,
                item.IsActive
            })
            .ToListAsync(cancellationToken);

        var definitions = await _db.WorkflowDefinitions.AsNoTracking()
            .Include(item => item.EntityType)
            .Include(item => item.Steps)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Retired)
            .OrderBy(item => item.EntityType.Name)
            .ThenBy(item => item.Name)
            .ThenByDescending(item => item.Version)
            .ToListAsync(cancellationToken);

        var workflowDefinitions = definitions.Select(item => new
        {
            item.Id,
            item.Name,
            item.Version,
            item.LifecycleStatus,
            entityType = item.EntityType.Name,
            entityTypeCode = item.EntityType.Code,
            module = InferModule(item.EntityType.Name),
            steps = item.Steps
                .OrderBy(step => step.Order)
                .ThenBy(step => step.Name)
                .Select(step => new
                {
                    step.Id,
                    step.Name,
                    step.StepType,
                    step.Order,
                    step.IsStartStep,
                    step.IsEndStep
                })
                .ToList()
        }).ToList();

        return Ok(new { success = true, data = new { modules, entityTypes, workflowDefinitions } });
    }

    [HttpGet("delegations")]
    public async Task<IActionResult> Delegations(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var userId = RequireUser();
        var admin = IsAdmin();
        var rows = await _db.WorkflowDelegations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                (admin || item.PrincipalUserId == userId || item.DelegateUserId == userId))
            .OrderByDescending(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost("delegations")]
    public async Task<IActionResult> CreateDelegation([FromBody] SaveWorkflowDelegationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var actorId = RequireUser();
        var principalId = request.PrincipalUserId ?? actorId;
        if (principalId != actorId && !IsAdmin()) return Forbid();
        if (request.EffectiveFrom >= request.EffectiveTo) return BadRequest("Effective end must be after the start.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("A delegation reason is required.");
        if (request.MaximumAmount is <= 0) return BadRequest("Maximum authority must be greater than zero.");
        if (request.WorkflowStepId.HasValue && !request.WorkflowDefinitionId.HasValue)
            return BadRequest("Select a workflow before selecting a workflow step.");

        await _governance.ValidateOneOffDelegationAsync(tenantId, principalId, request.DelegateUserId,
            allowRedelegation: true, cancellationToken);
        WorkflowDefinition? workflowDefinition = null;
        if (request.WorkflowDefinitionId.HasValue)
        {
            workflowDefinition = await _db.WorkflowDefinitions.Include(item => item.EntityType)
                .FirstOrDefaultAsync(item => item.Id == request.WorkflowDefinitionId.Value &&
                    item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
            if (workflowDefinition == null) return BadRequest("The selected workflow is not available.");
        }

        if (request.WorkflowStepId.HasValue)
        {
            var stepExists = await _db.WorkflowSteps.AsNoTracking().AnyAsync(item =>
                item.Id == request.WorkflowStepId.Value &&
                item.WorkflowDefinitionId == request.WorkflowDefinitionId!.Value &&
                item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
            if (!stepExists) return BadRequest("The selected workflow step does not belong to the selected workflow.");
        }

        var module = Normalize(request.Module);
        var entityType = Normalize(request.EntityType);
        if (workflowDefinition != null)
        {
            entityType = workflowDefinition.EntityType.Name;
            module ??= InferModule(entityType);
        }

        var overlaps = await _db.WorkflowDelegations.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
            item.PrincipalUserId == principalId && item.Kind == request.Kind &&
            item.Module == module && item.EntityType == entityType &&
            item.WorkflowDefinitionId == request.WorkflowDefinitionId &&
            item.WorkflowStepId == request.WorkflowStepId &&
            item.EffectiveFrom < request.EffectiveTo && request.EffectiveFrom < item.EffectiveTo,
            cancellationToken);
        if (overlaps) return Conflict("An active delegation already overlaps this user, scope, and date range.");

        var entity = new WorkflowDelegation
        {
            TenantId = tenantId,
            PrincipalUserId = principalId,
            DelegateUserId = request.DelegateUserId,
            Kind = request.Kind,
            Module = module,
            EntityType = entityType,
            WorkflowDefinitionId = request.WorkflowDefinitionId,
            WorkflowStepId = request.WorkflowStepId,
            MaximumAmount = request.MaximumAmount,
            CurrencyCode = Normalize(request.CurrencyCode)?.ToUpperInvariant(),
            EffectiveFrom = request.EffectiveFrom.ToUniversalTime(),
            EffectiveTo = request.EffectiveTo.ToUniversalTime(),
            Reason = request.Reason.Trim(),
            AllowRedelegation = request.AllowRedelegation,
            IsActive = true,
            CreatedById = actorId,
            CreatedBy = _currentUser.UserName
        };
        _db.WorkflowDelegations.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = entity });
    }

    [HttpGet("sla-breaches")]
    public async Task<IActionResult> SlaBreaches([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var now = DateTime.UtcNow;
        var since = now.AddDays(-Math.Clamp(days, 1, 365));

        var breachedApprovals = await _db.WorkflowApprovals.AsNoTracking()
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowStep)
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowInstance).ThenInclude(instance => instance.WorkflowDefinition)
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowInstance).ThenInclude(instance => instance.EntityType)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.Status == WorkflowApprovalStatus.Pending &&
                item.DueDate.HasValue && item.DueDate.Value < now)
            .OrderBy(item => item.DueDate)
            .Take(200)
            .ToListAsync(cancellationToken);

        var escalationExecutions = await _db.WorkflowEscalationExecutions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ExecutedAt >= since)
            .OrderByDescending(item => item.ExecutedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var approvalIds = breachedApprovals.Select(item => item.Id)
            .Concat(escalationExecutions.Select(item => item.ApprovalId))
            .Distinct()
            .ToList();

        var escalationApprovals = await _db.WorkflowApprovals.AsNoTracking()
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowStep)
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowInstance).ThenInclude(instance => instance.WorkflowDefinition)
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowInstance).ThenInclude(instance => instance.EntityType)
            .Where(item => approvalIds.Contains(item.Id) && item.TenantId == tenantId && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var userIds = breachedApprovals.Select(item => item.ApproverId)
            .Concat(escalationExecutions.Select(item => item.TargetUserId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        string? UserName(Guid? id) => id.HasValue && users.TryGetValue(id.Value, out var user)
            ? $"{user.FirstName} {user.LastName}".Trim()
            : null;

        object ApprovalInfo(WorkflowApproval approval) => new
        {
            approvalId = approval.Id,
            workflowInstanceId = approval.StepInstance.WorkflowInstanceId,
            workflowName = approval.StepInstance.WorkflowInstance.WorkflowDefinition.Name,
            entityType = approval.StepInstance.WorkflowInstance.EntityType.Name,
            approval.StepInstance.WorkflowInstance.EntityId,
            stepInstanceId = approval.StepInstanceId,
            stepName = approval.StepInstance.WorkflowStep.Name,
            approval.ApproverId,
            approverName = UserName(approval.ApproverId),
            approval.ApproverRole,
            approval.DueDate,
            hoursOverdue = approval.DueDate.HasValue ? Math.Round((now - approval.DueDate.Value).TotalHours, 1) : 0
        };

        var breaches = breachedApprovals.Select(ApprovalInfo).ToList();
        var escalations = escalationExecutions.Select(item =>
        {
            escalationApprovals.TryGetValue(item.ApprovalId, out var approval);
            return new
            {
                item.Id,
                item.ApprovalId,
                item.RuleIndex,
                item.Action,
                item.TargetUserId,
                targetUserName = UserName(item.TargetUserId),
                item.TargetRole,
                item.ExecutedAt,
                item.Result,
                approval = approval == null ? null : ApprovalInfo(approval)
            };
        }).ToList();

        return Ok(new { success = true, data = new { generatedAt = now, breaches, escalations } });
    }

    [HttpPost("delegations/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeDelegation(Guid id, [FromBody] RevokeWorkflowDelegationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var actorId = RequireUser();
        var entity = await _db.WorkflowDelegations.FirstOrDefaultAsync(item =>
            item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (entity == null) return NotFound();
        if (entity.PrincipalUserId != actorId && !IsAdmin()) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("A revocation reason is required.");
        entity.IsActive = false;
        entity.RevokedAt = DateTime.UtcNow;
        entity.RevokedById = actorId;
        entity.RevocationReason = request.Reason.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = entity });
    }

    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var calendar = await _db.WorkflowWorkingCalendars.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
            .OrderByDescending(item => item.IsDefault).FirstOrDefaultAsync(cancellationToken);
        return Ok(new { success = true, data = calendar });
    }

    [HttpPut("calendar")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> SaveCalendar([FromBody] SaveWorkflowCalendarRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        if (request.WorkDayStart >= request.WorkDayEnd) return BadRequest("Work day end must be after its start.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
        catch (TimeZoneNotFoundException) { return BadRequest("The selected time zone is not available on this server."); }

        var existing = await _db.WorkflowWorkingCalendars.FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.IsDefault, cancellationToken);
        existing ??= new WorkflowWorkingCalendar { TenantId = tenantId, IsDefault = true };
        if (existing.Id == Guid.Empty) existing.Id = Guid.NewGuid();
        existing.Name = request.Name.Trim();
        existing.TimeZoneId = request.TimeZoneId.Trim();
        existing.WorkingDaysMask = request.WorkingDaysMask;
        existing.WorkDayStart = request.WorkDayStart;
        existing.WorkDayEnd = request.WorkDayEnd;
        existing.HolidaysJson = JsonSerializer.Serialize(request.Holidays.Distinct().Order().ToList());
        existing.IsActive = true;
        existing.UpdatedAt = DateTime.UtcNow;
        if (_db.Entry(existing).State == EntityState.Detached) _db.WorkflowWorkingCalendars.Add(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = existing });
    }

    [HttpPost("approvals/{approvalId:guid}/send-back")]
    public async Task<IActionResult> SendBack(Guid approvalId, [FromBody] SendBackWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var actorId = RequireUser();
        var approval = await _db.WorkflowApprovals
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowInstance)
            .FirstOrDefaultAsync(item => item.Id == approvalId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);
        if (approval == null) return NotFound();
        if (approval.Status != WorkflowApprovalStatus.Pending || approval.ApproverId != actorId) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Instructions)) return BadRequest("Correction instructions are required.");
        var ownerId = request.CorrectionOwnerId ?? approval.StepInstance.WorkflowInstance.InitiatedById;
        var ownerExists = await _db.Users.AsNoTracking().AnyAsync(user =>
            user.Id == ownerId && user.TenantId == tenantId && user.IsActive, cancellationToken);
        if (!ownerExists) return BadRequest("The correction owner must be an active tenant user.");

        approval.Status = WorkflowApprovalStatus.MoreInfoRequested;
        approval.ProcessedById = actorId;
        approval.ProcessedDate = DateTime.UtcNow;
        approval.Comments = request.Instructions.Trim();
        approval.StepInstance.WorkflowInstance.Status = WorkflowInstanceStatus.Waiting;
        var correction = new WorkflowCorrectionRequest
        {
            TenantId = tenantId,
            WorkflowInstanceId = approval.StepInstance.WorkflowInstanceId,
            ApprovalId = approval.Id,
            RequestedById = actorId,
            CorrectionOwnerId = ownerId,
            TargetStepInstanceId = request.TargetStepInstanceId,
            Instructions = request.Instructions.Trim(),
            DueAt = await _governance.CalculateDueDateAsync(tenantId, DateTime.UtcNow, request.DueWorkingHours),
            CreatedById = actorId,
            CreatedBy = _currentUser.UserName
        };
        _db.WorkflowCorrectionRequests.Add(correction);
        AddActivity(approval.StepInstance.WorkflowInstanceId, approval.StepInstanceId,
            WorkflowActivityType.ApprovalMoreInfoRequested, "Sent back for correction", correction.Instructions,
            actorId, new { correction.Id, correction.CorrectionOwnerId, correction.TargetStepInstanceId });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = correction });
    }

    [HttpGet("corrections/my")]
    public async Task<IActionResult> MyCorrections(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var userId = RequireUser();
        var rows = await _db.WorkflowCorrectionRequests.AsNoTracking().Where(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.CorrectionOwnerId == userId &&
            item.Status == WorkflowCorrectionStatus.Open).OrderBy(item => item.DueAt).ToListAsync(cancellationToken);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost("corrections/{id:guid}/resubmit")]
    public async Task<IActionResult> Resubmit(Guid id, [FromBody] ResubmitWorkflowCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var actorId = RequireUser();
        var correction = await _db.WorkflowCorrectionRequests.FirstOrDefaultAsync(item =>
            item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (correction == null) return NotFound();
        if (correction.CorrectionOwnerId != actorId || correction.Status != WorkflowCorrectionStatus.Open) return Forbid();
        var approval = await _db.WorkflowApprovals.Include(item => item.StepInstance)
            .ThenInclude(step => step.WorkflowInstance).FirstAsync(item => item.Id == correction.ApprovalId, cancellationToken);
        correction.Status = WorkflowCorrectionStatus.Resubmitted;
        correction.ResubmittedAt = DateTime.UtcNow;
        correction.ResubmissionData = JsonSerializer.Serialize(request.Data);
        approval.Status = WorkflowApprovalStatus.Pending;
        approval.ProcessedById = null;
        approval.ProcessedDate = null;
        approval.Comments = null;
        approval.StepInstance.WorkflowInstance.Status = WorkflowInstanceStatus.InProgress;
        AddActivity(correction.WorkflowInstanceId, approval.StepInstanceId, WorkflowActivityType.DataUpdated,
            "Correction resubmitted", request.Comments, actorId, new { correction.Id, request.Data });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = correction });
    }

    [HttpPost("steps/{stepInstanceId:guid}/ad-hoc-approvers")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> AddAdHocApprover(Guid stepInstanceId, [FromBody] AddAdHocApproverRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var actorId = RequireUser();
        if (!request.UserId.HasValue && string.IsNullOrWhiteSpace(request.Role))
            return BadRequest("An approver user or role is required.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("A reason is required.");
        var step = await _db.WorkflowStepInstances.Include(item => item.WorkflowInstance)
            .FirstOrDefaultAsync(item => item.Id == stepInstanceId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);
        if (step == null) return NotFound();
        if (step.Status is WorkflowStepInstanceStatus.Completed or WorkflowStepInstanceStatus.Cancelled)
            return Conflict("Approvers cannot be changed after the step is closed.");

        var approval = new WorkflowApproval
        {
            TenantId = tenantId,
            StepInstanceId = step.Id,
            ApproverId = request.UserId,
            ApproverRole = Normalize(request.Role),
            ApprovalGroup = Math.Max(request.ApprovalGroup, 1),
            Status = WorkflowApprovalStatus.Pending,
            RequestedDate = DateTime.UtcNow,
            DueDate = await _governance.CalculateDueDateAsync(tenantId, DateTime.UtcNow, request.DueWorkingHours),
            IsAdHoc = true,
            AddedById = actorId,
            AddedAt = DateTime.UtcNow,
            AdditionReason = request.Reason.Trim(),
            CreatedById = actorId,
            CreatedBy = _currentUser.UserName
        };
        _db.WorkflowApprovals.Add(approval);
        AddActivity(step.WorkflowInstanceId, step.Id, WorkflowActivityType.ApprovalRequested,
            "Ad hoc approver added", approval.AdditionReason, actorId,
            new { approval.Id, approval.ApproverId, approval.ApproverRole, approval.ApprovalGroup });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = approval });
    }

    [HttpDelete("approvals/{approvalId:guid}/ad-hoc")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> RemoveAdHocApprover(Guid approvalId, [FromQuery] string reason,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        if (string.IsNullOrWhiteSpace(reason)) return BadRequest("A removal reason is required.");
        var approval = await _db.WorkflowApprovals.Include(item => item.StepInstance)
            .FirstOrDefaultAsync(item => item.Id == approvalId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);
        if (approval == null) return NotFound();
        if (!approval.IsAdHoc || approval.Status != WorkflowApprovalStatus.Pending)
            return Conflict("Only pending ad hoc approvals can be removed.");
        approval.IsDeleted = true;
        approval.DeletedAt = DateTime.UtcNow;
        approval.DeletedBy = _currentUser.UserName;
        AddActivity(approval.StepInstance.WorkflowInstanceId, approval.StepInstanceId,
            WorkflowActivityType.DataUpdated, "Ad hoc approver removed", reason.Trim(), RequireUser(),
            new { approval.Id });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    private void AddActivity(Guid instanceId, Guid? stepId, WorkflowActivityType type, string title,
        string? description, Guid actorId, object? data)
    {
        _db.WorkflowActivityLogs.Add(new WorkflowActivityLog
        {
            TenantId = RequireTenant(), WorkflowInstanceId = instanceId, StepInstanceId = stepId,
            ActivityType = type, Title = title, Description = description, PerformedById = actorId,
            ActivityDate = DateTime.UtcNow, Data = data == null ? null : JsonSerializer.Serialize(data),
            IpAddress = _currentUser.IpAddress, UserAgent = _currentUser.UserAgent
        });
    }

    private Guid RequireTenant() => _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
    private Guid RequireUser() => Guid.TryParse(_currentUser.UserId, out var id) ? id : throw new UnauthorizedAccessException("User context is required.");
    private bool IsAdmin() => AdminRoles.Any(_currentUser.IsInRole);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? InferModule(string? entityType)
    {
        var normalized = (entityType ?? string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();

        return normalized switch
        {
            "purchaseorder" or "purchaserequisition" or "procurementplan" or "tender" or "rfq" or
                "supplierquote" or "bid" or "evaluation" or "vendor" or "businesspartner" => "Procurement",
            "workorder" or "jobcard" or "fleettrip" or "fleettripinspection" or "asset" or "quality" => "Maintenance",
            "inventory" or "inventorytransfer" or "inventoryrequisition" => "Inventory",
            "employee" or "payrollrun" or "payrollpayslipemail" or "payrollsalaryadvance" or
                "payrollbonussetup" or "payrollbackpaysetup" => "Human Resources",
            "project" or "projectdeliverable" or "projectclosure" => "Projects",
            "customer" or "salesorder" or "salesagreement" or "salesallocation" or "refund" or "creditnote" => "Sales",
            "servicerequest" => "Service Management",
            _ => null
        };
    }
}

public sealed class SaveWorkflowDelegationRequest
{
    public Guid? PrincipalUserId { get; set; }
    public Guid DelegateUserId { get; set; }
    public WorkflowDelegationKind Kind { get; set; }
    public string? Module { get; set; }
    public string? EntityType { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowStepId { get; set; }
    public decimal? MaximumAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool AllowRedelegation { get; set; }
}

public sealed class RevokeWorkflowDelegationRequest { public string Reason { get; set; } = string.Empty; }
public sealed class SaveWorkflowCalendarRequest
{
    public string Name { get; set; } = "Default working calendar";
    public string TimeZoneId { get; set; } = "UTC";
    public int WorkingDaysMask { get; set; } = 31;
    public TimeSpan WorkDayStart { get; set; } = new(8, 0, 0);
    public TimeSpan WorkDayEnd { get; set; } = new(17, 0, 0);
    public List<DateOnly> Holidays { get; set; } = [];
}
public sealed class SendBackWorkflowRequest
{
    public Guid? CorrectionOwnerId { get; set; }
    public Guid? TargetStepInstanceId { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public double? DueWorkingHours { get; set; }
}
public sealed class ResubmitWorkflowCorrectionRequest
{
    public object? Data { get; set; }
    public string? Comments { get; set; }
}
public sealed class AddAdHocApproverRequest
{
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public int ApprovalGroup { get; set; } = 1;
    public double? DueWorkingHours { get; set; }
    public string Reason { get; set; } = string.Empty;
}
