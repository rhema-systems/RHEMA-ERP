using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/workflow/platform")]
[Authorize]
public sealed class WorkflowPlatformController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowDefinitionService _definitions;
    private readonly IWorkflowConditionEvaluator _conditions;
    private readonly IWorkflowEngine _engine;

    public WorkflowPlatformController(ApplicationDbContext db, ICurrentUserService currentUser,
        IWorkflowDefinitionService definitions, IWorkflowConditionEvaluator conditions, IWorkflowEngine engine)
    { _db = db; _currentUser = currentUser; _definitions = definitions; _conditions = conditions; _engine = engine; }

    [HttpPost("definitions/{id:guid}/simulate")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public async Task<IActionResult> Simulate(Guid id, [FromBody] WorkflowSimulationRequest request,
        CancellationToken cancellationToken)
    {
        var definition = await _db.WorkflowDefinitions.AsNoTracking().Include(item => item.Steps)
            .Include(item => item.Transitions).FirstOrDefaultAsync(item =>
                item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        if (definition == null) return NotFound();
        var current = definition.Steps.FirstOrDefault(step => step.IsStartStep) ?? definition.Steps.OrderBy(step => step.Order).FirstOrDefault();
        var path = new List<object>(); var visited = new HashSet<Guid>(); var warnings = new List<string>();
        while (current != null && visited.Add(current.Id) && path.Count < 100)
        {
            var config = ParseConfig(current.Configuration);
            path.Add(new { current.Id, current.Name, current.StepType, current.RequiredRole,
                approvers = config?.ApprovalConfig?.ApproverRules.Select(rule => new { rule.UserId, rule.Role, rule.ApprovalGroup }).ToList() });
            var outgoing = definition.Transitions.Where(item => item.FromStepId == current.Id).OrderByDescending(item => item.Priority).ToList();
            WorkflowTransition? selected = null;
            foreach (var transition in outgoing.Where(item => !string.IsNullOrWhiteSpace(item.Condition)))
                if (await _conditions.EvaluateConditionAsync(transition.Condition!, request.DataContext)) { selected = transition; break; }
            selected ??= outgoing.FirstOrDefault(item => item.IsDefault) ?? outgoing.FirstOrDefault();
            current = selected == null ? null : definition.Steps.FirstOrDefault(step => step.Id == selected.ToStepId);
        }
        if (current != null) warnings.Add("A cycle or path longer than 100 steps was detected.");
        return Ok(new { success = true, data = new { definition.Id, definition.Version, path, warnings, isValid = warnings.Count == 0 } });
    }

    [HttpPost("definitions/{id:guid}/rollback-draft")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> Rollback(Guid id, CancellationToken cancellationToken)
    {
        var source = await _db.WorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        if (source == null) return NotFound();
        var draft = await _definitions.CloneWorkflowDefinitionDraftAsync(id,
            $"Controlled rollback from version {source.Version}", UserId);
        return Ok(new { success = true, data = new { draft.Id, draft.Name, draft.Version, draft.LifecycleStatus } });
    }

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken cancellationToken)
    {
        var rows = await _db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType)
            .Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .OrderBy(item => item.Name).Select(item => new { item.Id, item.Name, item.Description,
                entityType = item.EntityType.Name, item.Version }).ToListAsync(cancellationToken);
        return Ok(new { success = true, data = rows });
    }

    [HttpGet("templates/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var definition = await _db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType)
            .Include(item => item.Steps).Include(item => item.Transitions).FirstOrDefaultAsync(item =>
                item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        if (definition == null) return NotFound();
        return Ok(new { schemaVersion = 1, exportedAt = DateTime.UtcNow, tenantNeutral = true,
            definition = new { definition.Name, definition.Description, entityType = definition.EntityType.Code,
                definition.Configuration, steps = definition.Steps.Select(step => new { step.Id, step.Name, step.Description,
                    step.StepType, step.Order, step.IsStartStep, step.IsEndStep, step.IsRequired, step.RequiredRole,
                    step.EstimatedHours, step.Configuration }), transitions = definition.Transitions.Select(item => new {
                    item.FromStepId, item.ToStepId, item.Name, item.Description, item.Condition, item.IsDefault, item.Priority }) } });
    }

    [HttpPost("templates/import")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> Import([FromBody] WorkflowTemplatePackage package, CancellationToken cancellationToken)
    {
        if (package.SchemaVersion != 1) return BadRequest("Unsupported workflow package version.");
        var graphErrors = WorkflowPlatformPolicy.ValidateTemplateGraph(package.Definition.Steps.Select(step => step.Id),
            package.Definition.Transitions.Select(item => (item.FromStepId, item.ToStepId)));
        if (graphErrors.Count > 0) return BadRequest(new { success = false, errors = graphErrors });
        var entityType = await _db.WorkflowEntityTypes.FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && !item.IsDeleted && (item.Code == package.Definition.EntityType || item.Name == package.Definition.EntityType), cancellationToken);
        if (entityType == null) return BadRequest("The package entity type is not configured for this tenant.");
        var definitionId = Guid.NewGuid();
        var idMap = package.Definition.Steps.ToDictionary(step => step.Id, _ => Guid.NewGuid());
        var definition = new WorkflowDefinition { Id = definitionId, TenantId = TenantId, DefinitionKey = Guid.NewGuid(),
            Name = $"{package.Definition.Name} (Imported {DateTime.UtcNow:yyyyMMddHHmm})", Description = package.Definition.Description,
            EntityTypeId = entityType.Id, Configuration = package.Definition.Configuration, Version = 1,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft, IsActive = false, CreatedById = UserId, CreatedBy = _currentUser.UserName };
        foreach (var step in package.Definition.Steps) definition.Steps.Add(new WorkflowStep { Id = idMap[step.Id], TenantId = TenantId,
            WorkflowDefinitionId = definitionId, Name = step.Name, Description = step.Description, StepType = step.StepType,
            Order = step.Order, IsStartStep = step.IsStartStep, IsEndStep = step.IsEndStep, IsRequired = step.IsRequired,
            RequiredRole = step.RequiredRole, EstimatedHours = step.EstimatedHours, Configuration = step.Configuration });
        foreach (var transition in package.Definition.Transitions) definition.Transitions.Add(new WorkflowTransition { TenantId = TenantId,
            WorkflowDefinitionId = definitionId, FromStepId = idMap[transition.FromStepId], ToStepId = idMap[transition.ToStepId],
            Name = transition.Name, Description = transition.Description, Condition = transition.Condition,
            IsDefault = transition.IsDefault, Priority = transition.Priority });
        _db.WorkflowDefinitions.Add(definition); await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = new { definition.Id, definition.Name } });
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-WorkflowPlatformPolicy.NormalizeAnalyticsDays(days));
        var instances = await _db.WorkflowInstances.AsNoTracking().Where(item =>
            item.TenantId == TenantId && !item.IsDeleted && item.CreatedDate >= since).ToListAsync(cancellationToken);
        var steps = await _db.WorkflowStepInstances.AsNoTracking().Include(item => item.WorkflowStep).Where(item =>
            item.TenantId == TenantId && !item.IsDeleted && item.CreatedDate >= since).ToListAsync(cancellationToken);
        var approvals = await _db.WorkflowApprovals.AsNoTracking().Where(item =>
            item.TenantId == TenantId && !item.IsDeleted && item.RequestedDate >= since).ToListAsync(cancellationToken);
        var completed = instances.Where(item => item.CompletedDate.HasValue).ToList();
        var averageHours = completed.Count == 0 ? 0 : completed.Average(item => (item.CompletedDate!.Value - (item.StartedDate ?? item.CreatedDate)).TotalHours);
        var overdue = approvals.Count(item => item.Status == WorkflowApprovalStatus.Pending && item.DueDate < DateTime.UtcNow);
        var bottlenecks = steps.GroupBy(item => item.WorkflowStep?.Name ?? "Unknown").Select(group => new { step = group.Key,
            averageHours = group.Where(item => item.CompletedDate.HasValue && item.StartedDate.HasValue)
                .Select(item => (item.CompletedDate!.Value - item.StartedDate!.Value).TotalHours).DefaultIfEmpty().Average(), count = group.Count() })
            .OrderByDescending(item => item.averageHours).Take(5).ToList();
        return Ok(new { success = true, data = new { total = instances.Count, active = instances.Count(item => item.Status == WorkflowInstanceStatus.InProgress || item.Status == WorkflowInstanceStatus.Waiting),
            completed = completed.Count, failed = instances.Count(item => item.Status == WorkflowInstanceStatus.Failed), averageHours,
            rejectionRate = approvals.Count == 0 ? 0 : approvals.Count(item => item.Status == WorkflowApprovalStatus.Rejected) * 100d / approvals.Count,
            slaCompliance = approvals.Count == 0 ? 100 : (approvals.Count - overdue) * 100d / approvals.Count, overdue, bottlenecks } });
    }

    [HttpGet("audit/archive")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,InternalAudit")]
    public async Task<IActionResult> AuditArchive([FromQuery] DateTime from, [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        if (to <= from || to - from > TimeSpan.FromDays(366)) return BadRequest("Select a valid range of no more than one year.");
        var events = await _db.WorkflowActivityLogs.AsNoTracking().Where(item =>
            item.TenantId == TenantId && item.ActivityDate >= from && item.ActivityDate < to && !item.IsDeleted)
            .OrderBy(item => item.ActivityDate).ThenBy(item => item.Id).Select(item => new { item.Id,
                item.WorkflowInstanceId, item.StepInstanceId, item.ActivityType, item.Title, item.Description,
                item.Data, item.PerformedById, item.ActivityDate, item.IpAddress, item.UserAgent }).ToListAsync(cancellationToken);
        var json = JsonSerializer.Serialize(events);
        var hash = WorkflowPlatformPolicy.ComputeArchiveHash(json);
        return Ok(new { success = true, data = new { from, to, count = events.Count, sha256 = hash, events } });
    }

    [HttpGet("mobile/inbox")]
    public async Task<IActionResult> MobileInbox(CancellationToken cancellationToken)
    {
        var roles = new HashSet<string>(_currentUser.Roles ?? [], StringComparer.OrdinalIgnoreCase);
        var rows = await _db.WorkflowApprovals.AsNoTracking().Include(item => item.StepInstance).ThenInclude(step => step.WorkflowInstance)
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowStep).Where(item =>
                item.TenantId == TenantId && !item.IsDeleted && item.Status == WorkflowApprovalStatus.Pending &&
                (item.ApproverId == UserId || item.ApproverRole != null && roles.Contains(item.ApproverRole)))
            .OrderBy(item => item.DueDate).Select(item => new { item.Id, item.StepInstanceId, step = item.StepInstance.WorkflowStep.Name,
                item.StepInstance.WorkflowInstance.EntityId, item.DueDate, item.Priority }).ToListAsync(cancellationToken);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost("integrations")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,IntegrationService")]
    public async Task<IActionResult> QueueIntegration([FromBody] QueueWorkflowIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        try { WorkflowPlatformPolicy.EnsureIdempotencyKey(request.IdempotencyKey); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        var idempotencyKey = request.IdempotencyKey.Trim();
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https"))
            return BadRequest("A valid HTTP or HTTPS integration endpoint is required.");
        var existing = await _db.WorkflowIntegrationExecutions.FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing != null) return Ok(new { success = true, data = existing });
        var execution = new WorkflowIntegrationExecution { TenantId = TenantId,
            WorkflowInstanceId = request.WorkflowInstanceId, StepInstanceId = request.StepInstanceId,
            IdempotencyKey = idempotencyKey, Operation = request.Operation.Trim(), Endpoint = request.Endpoint.Trim(),
            RequestPayload = request.Payload?.GetRawText(), Status = WorkflowExecutionQueueStatus.Pending,
            MaximumAttempts = Math.Clamp(request.MaximumAttempts, 1, 20), NextAttemptAt = DateTime.UtcNow,
            CreatedById = UserId, CreatedBy = _currentUser.UserName };
        _db.WorkflowIntegrationExecutions.Add(execution); await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = execution });
    }

    [HttpGet("integrations/exceptions")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> IntegrationExceptions(CancellationToken cancellationToken)
    {
        var rows = await _db.WorkflowIntegrationExecutions.AsNoTracking().Where(item =>
            item.TenantId == TenantId && !item.IsDeleted &&
            (item.Status == WorkflowExecutionQueueStatus.Failed || item.Status == WorkflowExecutionQueueStatus.DeadLetter))
            .OrderByDescending(item => item.LastAttemptAt).Take(200).ToListAsync(cancellationToken);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost("integrations/{id:guid}/retry")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> RetryIntegration(Guid id, CancellationToken cancellationToken)
    {
        var execution = await _db.WorkflowIntegrationExecutions.FirstOrDefaultAsync(item =>
            item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        if (execution == null) return NotFound();
        execution.Status = WorkflowExecutionQueueStatus.Pending; execution.NextAttemptAt = DateTime.UtcNow;
        execution.LastError = null; await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = execution });
    }

    [HttpPost("integrations/{id:guid}/reconcile")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,InternalAudit")]
    public async Task<IActionResult> ReconcileIntegration(Guid id, [FromBody] string notes, CancellationToken cancellationToken)
    {
        var execution = await _db.WorkflowIntegrationExecutions.FirstOrDefaultAsync(item =>
            item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        if (execution == null) return NotFound();
        execution.ReconciledAt = DateTime.UtcNow; execution.ReconciledById = UserId;
        execution.ReconciliationNotes = notes?.Trim(); await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = execution });
    }

    [HttpPost("mobile/actions")]
    public async Task<IActionResult> OfflineAction([FromBody] WorkflowOfflineActionRequest request, CancellationToken cancellationToken)
    {
        string actionType;
        try
        {
            WorkflowPlatformPolicy.EnsureIdempotencyKey(request.IdempotencyKey);
            actionType = WorkflowPlatformPolicy.NormalizeOfflineAction(request.ActionType);
        }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        var idempotencyKey = request.IdempotencyKey.Trim();
        var existing = await _db.WorkflowOfflineActions.FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing != null)
            return existing.UserId == UserId ? Ok(new { success = true, data = existing }) : Conflict("The idempotency key is already in use.");
        var queued = new WorkflowOfflineAction { TenantId = TenantId, UserId = UserId,
            IdempotencyKey = idempotencyKey, ActionType = actionType, Payload = JsonSerializer.Serialize(request.Payload),
            Status = WorkflowExecutionQueueStatus.Processing, CreatedById = UserId };
        _db.WorkflowOfflineActions.Add(queued); await _db.SaveChangesAsync(cancellationToken);
        try
        {
            if (!request.Payload.TryGetProperty("stepInstanceId", out var stepIdElement) || !Guid.TryParse(stepIdElement.GetString(), out var stepId))
                throw new InvalidOperationException("A step instance is required.");
            var action = actionType == "approve" ? WorkflowStepAction.Complete : WorkflowStepAction.Reject;
            var result = await _engine.ProcessStepAsync(stepId, UserId, action, comments: request.Payload.TryGetProperty("comments", out var comments) ? comments.GetString() : null);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            queued.Status = WorkflowExecutionQueueStatus.Succeeded; queued.ProcessedAt = DateTime.UtcNow;
        }
        catch (Exception exception) { queued.Status = WorkflowExecutionQueueStatus.Failed; queued.Error = exception.Message; queued.ProcessedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = queued.Status == WorkflowExecutionQueueStatus.Succeeded, data = queued });
    }

    private WorkflowStepConfigurationDto? ParseConfig(string? json)
    { try { return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { return null; } }
    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : throw new UnauthorizedAccessException();
}

public sealed class WorkflowSimulationRequest { public object? DataContext { get; set; } }
public sealed class WorkflowOfflineActionRequest { public string IdempotencyKey { get; set; } = string.Empty; public string ActionType { get; set; } = string.Empty; public JsonElement Payload { get; set; } }
public sealed class QueueWorkflowIntegrationRequest { public Guid WorkflowInstanceId { get; set; } public Guid? StepInstanceId { get; set; } public string IdempotencyKey { get; set; } = string.Empty; public string Operation { get; set; } = string.Empty; public string Endpoint { get; set; } = string.Empty; public JsonElement? Payload { get; set; } public int MaximumAttempts { get; set; } = 5; }
public sealed class WorkflowTemplatePackage { public int SchemaVersion { get; set; } public WorkflowTemplateDefinition Definition { get; set; } = new(); }
public sealed class WorkflowTemplateDefinition { public string Name { get; set; } = string.Empty; public string? Description { get; set; } public string EntityType { get; set; } = string.Empty; public string? Configuration { get; set; } public List<WorkflowTemplateStep> Steps { get; set; } = []; public List<WorkflowTemplateTransition> Transitions { get; set; } = []; }
public sealed class WorkflowTemplateStep { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public string? Description { get; set; } public WorkflowStepType StepType { get; set; } public int Order { get; set; } public bool IsStartStep { get; set; } public bool IsEndStep { get; set; } public bool IsRequired { get; set; } public string? RequiredRole { get; set; } public double? EstimatedHours { get; set; } public string? Configuration { get; set; } }
public sealed class WorkflowTemplateTransition { public Guid FromStepId { get; set; } public Guid ToStepId { get; set; } public string Name { get; set; } = string.Empty; public string? Description { get; set; } public string? Condition { get; set; } public bool IsDefault { get; set; } public int Priority { get; set; } }
