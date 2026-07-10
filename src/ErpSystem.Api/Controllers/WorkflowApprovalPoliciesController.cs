using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/workflow/approval-policies")]
[Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
public sealed class WorkflowApprovalPoliciesController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public WorkflowApprovalPoliciesController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var tenantId = RequireTenant();
        var policies = await _db.WorkflowApprovalPolicySets
            .Where(policy => policy.TenantId == tenantId && !policy.IsDeleted)
            .OrderBy(policy => policy.Code)
            .ThenByDescending(policy => policy.EffectiveFrom)
            .ToListAsync();
        return Ok(new { success = true, data = policies.Select(ToDto) });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] SaveWorkflowApprovalPolicyRequest request)
    {
        ValidateRequest(request);
        var tenantId = RequireTenant();
        var policy = new WorkflowApprovalPolicySet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "System",
            CreatedById = CurrentUserId()
        };
        ApplyRequest(policy, request);
        _db.WorkflowApprovalPolicySets.Add(policy);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = policy.Id }, new { success = true, data = ToDto(policy) });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] SaveWorkflowApprovalPolicyRequest request)
    {
        ValidateRequest(request);
        var policy = await FindPolicy(id);
        if (policy.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Draft)
        {
            return Conflict("Published or retired policies are immutable. Clone a draft to make changes.");
        }

        ApplyRequest(policy, request);
        policy.UpdatedAt = DateTime.UtcNow;
        policy.UpdatedBy = _currentUser.UserName ?? "System";
        policy.LastModifiedById = CurrentUserId();
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = ToDto(policy) });
    }

    [HttpPost("{id:guid}/clone-draft")]
    public async Task<ActionResult> CloneDraft(Guid id)
    {
        var source = await FindPolicy(id);
        var clone = new WorkflowApprovalPolicySet
        {
            Id = Guid.NewGuid(),
            TenantId = source.TenantId,
            Code = source.Code,
            Name = source.Name,
            Description = source.Description,
            Module = source.Module,
            EntityType = source.EntityType,
            Category = source.Category,
            LocationId = source.LocationId,
            LegalEntityId = source.LegalEntityId,
            MinimumAmount = source.MinimumAmount,
            MaximumAmount = source.MaximumAmount,
            CurrencyCode = source.CurrencyCode,
            EffectiveFrom = source.EffectiveTo?.AddTicks(1) ?? DateTime.UtcNow,
            Priority = source.Priority,
            IsActive = true,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft,
            ApprovalConfiguration = source.ApprovalConfiguration,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "System",
            CreatedById = CurrentUserId()
        };
        _db.WorkflowApprovalPolicySets.Add(clone);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = ToDto(clone) });
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult> Publish(Guid id)
    {
        var policy = await FindPolicy(id);
        if (policy.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Draft)
        {
            return Conflict("Only draft policies can be published.");
        }

        _ = DeserializeConfiguration(policy.ApprovalConfiguration);
        var overlaps = await _db.WorkflowApprovalPolicySets.AnyAsync(candidate =>
            candidate.Id != policy.Id && candidate.TenantId == policy.TenantId && !candidate.IsDeleted &&
            candidate.IsActive && candidate.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
            candidate.Code == policy.Code &&
            (!candidate.EffectiveTo.HasValue || candidate.EffectiveTo.Value >= policy.EffectiveFrom) &&
            (!policy.EffectiveTo.HasValue || policy.EffectiveTo.Value >= candidate.EffectiveFrom));
        if (overlaps)
        {
            return Conflict("A published policy with this code already overlaps the requested effective period.");
        }

        policy.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
        policy.PublishedAt = DateTime.UtcNow;
        policy.PublishedById = CurrentUserId();
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = ToDto(policy) });
    }

    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult> Retire(Guid id)
    {
        var policy = await FindPolicy(id);
        if (policy.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
        {
            return Conflict("Only published policies can be retired.");
        }

        policy.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
        policy.IsActive = false;
        policy.RetiredAt = DateTime.UtcNow;
        policy.RetiredById = CurrentUserId();
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = ToDto(policy) });
    }

    private async Task<WorkflowApprovalPolicySet> FindPolicy(Guid id)
    {
        var tenantId = RequireTenant();
        return await _db.WorkflowApprovalPolicySets.FirstOrDefaultAsync(policy =>
            policy.Id == id && policy.TenantId == tenantId && !policy.IsDeleted)
            ?? throw new KeyNotFoundException("Workflow approval policy not found.");
    }

    private Guid RequireTenant()
        => _currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("Tenant is required.");

    private Guid? CurrentUserId()
        => Guid.TryParse(_currentUser.UserId, out var userId) ? userId : null;

    private static void ValidateRequest(SaveWorkflowApprovalPolicyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Policy code and name are required.");
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom)
            throw new ArgumentException("Effective-to date cannot precede effective-from date.");
        if (request.MinimumAmount.HasValue && request.MaximumAmount.HasValue && request.MaximumAmount < request.MinimumAmount)
            throw new ArgumentException("Maximum amount cannot be lower than minimum amount.");
        if (request.ApprovalConfig.ApproverRules.Count == 0)
            throw new ArgumentException("At least one approver rule is required.");
    }

    private static void ApplyRequest(WorkflowApprovalPolicySet policy, SaveWorkflowApprovalPolicyRequest request)
    {
        policy.Code = request.Code.Trim().ToUpperInvariant();
        policy.Name = request.Name.Trim();
        policy.Description = request.Description?.Trim();
        policy.Module = request.Module?.Trim();
        policy.EntityType = request.EntityType?.Trim();
        policy.Category = request.Category?.Trim();
        policy.LocationId = request.LocationId;
        policy.LegalEntityId = request.LegalEntityId;
        policy.MinimumAmount = request.MinimumAmount;
        policy.MaximumAmount = request.MaximumAmount;
        policy.CurrencyCode = request.CurrencyCode?.Trim().ToUpperInvariant();
        policy.EffectiveFrom = request.EffectiveFrom;
        policy.EffectiveTo = request.EffectiveTo;
        policy.Priority = request.Priority;
        policy.IsActive = request.IsActive;
        policy.ApprovalConfiguration = JsonSerializer.Serialize(request.ApprovalConfig, JsonOptions);
    }

    private static WorkflowApprovalConfigDto DeserializeConfiguration(string json)
        => JsonSerializer.Deserialize<WorkflowApprovalConfigDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Approval policy configuration is invalid.");

    private static object ToDto(WorkflowApprovalPolicySet policy) => new
    {
        policy.Id, policy.Code, policy.Name, policy.Description, policy.Module, policy.EntityType,
        policy.Category, policy.LocationId, policy.LegalEntityId, policy.MinimumAmount, policy.MaximumAmount,
        policy.CurrencyCode, policy.EffectiveFrom, policy.EffectiveTo, policy.Priority, policy.IsActive,
        policy.LifecycleStatus, policy.PublishedAt, policy.PublishedById, policy.RetiredAt,
        approvalConfig = DeserializeConfiguration(policy.ApprovalConfiguration)
    };
}

public sealed class SaveWorkflowApprovalPolicyRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Module { get; set; }
    public string? EntityType { get; set; }
    public string? Category { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? LegalEntityId { get; set; }
    public decimal? MinimumAmount { get; set; }
    public decimal? MaximumAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public WorkflowApprovalConfigDto ApprovalConfig { get; set; } = new();
}
