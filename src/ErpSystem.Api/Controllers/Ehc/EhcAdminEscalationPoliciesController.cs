using System.Text.Json;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/escalation-policies")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminEscalationPoliciesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcAdminEscalationPoliciesController> _logger;

    public EhcAdminEscalationPoliciesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcAdminEscalationPoliciesController> logger)
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
            return Ok(new { success = true, data = Array.Empty<EhcEscalationPolicyDto>() });
        }

        var policies = await _db.EhcEscalationPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .Include(p => p.Levels)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.Priority)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var data = policies.Select(Map).ToList();
        return Ok(new { success = true, data });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = (EhcEscalationPolicyDto?)null });
        }

        var policy = await _db.EhcEscalationPolicies
            .AsNoTracking()
            .Where(p => p.Id == id && p.TenantId == tenantId && !p.IsDeleted)
            .Include(p => p.Levels)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy == null) return NotFound(new { success = false, message = "Policy not found" });
        return Ok(new { success = true, data = Map(policy) });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateEhcEscalationPolicyRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new CreateEhcEscalationPolicyRequestDto();

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return BadRequest(new { success = false, message = "Tenant context is required." });

            var name = (request.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, message = "Name is required." });

            var now = DateTime.UtcNow;
            var policy = new EhcEscalationPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                IsActive = request.IsActive,
                Priority = request.Priority,
                Trigger = request.Trigger,
                DueSoonMinutes = Math.Max(1, request.DueSoonMinutes),
                TicketType = request.TicketType,
                TicketPriority = request.TicketPriority,
                CategoryId = NormalizeOptionalGuid(request.CategoryId),
                SubcategoryId = NormalizeOptionalGuid(request.SubcategoryId),
                DepartmentId = NormalizeOptionalGuid(request.DepartmentId),
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            var levels = NormalizeLevels(request.Levels);
            if (levels.Count == 0)
                return BadRequest(new { success = false, message = "At least one escalation level is required." });

            foreach (var l in levels)
            {
                policy.Levels.Add(new EhcEscalationPolicyLevel
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolicyId = policy.Id,
                    Level = l.Level,
                    DelayMinutes = Math.Max(0, l.DelayMinutes),
                    NotifyRolesJson = SerializeRoles(l.NotifyRoles),
                    NotifyAssignedAgent = l.NotifyAssignedAgent,
                    NotifyUserId = NormalizeOptionalGuid(l.NotifyUserId),
                    AddInternalComment = l.AddInternalComment,
                    ReassignToRole = string.IsNullOrWhiteSpace(l.ReassignToRole) ? null : l.ReassignToRole.Trim(),
                    ReassignToUserId = NormalizeOptionalGuid(l.ReassignToUserId),
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName ?? "System"
                });
            }

            _db.EhcEscalationPolicies.Add(policy);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, data = Map(policy) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC escalation policy");
            return StatusCode(500, new { success = false, message = "Failed to create escalation policy" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEhcEscalationPolicyRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new UpdateEhcEscalationPolicyRequestDto();

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return BadRequest(new { success = false, message = "Tenant context is required." });

            var policy = await _db.EhcEscalationPolicies
                .Where(p => p.Id == id && p.TenantId == tenantId && !p.IsDeleted)
                .Include(p => p.Levels)
                .FirstOrDefaultAsync(cancellationToken);

            if (policy == null)
                return NotFound(new { success = false, message = "Policy not found" });

            var name = (request.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, message = "Name is required." });

            var normalizedLevels = NormalizeLevels(request.Levels);
            if (normalizedLevels.Count == 0)
                return BadRequest(new { success = false, message = "At least one escalation level is required." });

            var now = DateTime.UtcNow;
            policy.Name = name;
            policy.IsActive = request.IsActive;
            policy.Priority = request.Priority;
            policy.Trigger = request.Trigger;
            policy.DueSoonMinutes = Math.Max(1, request.DueSoonMinutes);
            policy.TicketType = request.TicketType;
            policy.TicketPriority = request.TicketPriority;
            policy.CategoryId = NormalizeOptionalGuid(request.CategoryId);
            policy.SubcategoryId = NormalizeOptionalGuid(request.SubcategoryId);
            policy.DepartmentId = NormalizeOptionalGuid(request.DepartmentId);
            policy.UpdatedAt = now;
            policy.UpdatedBy = _currentUserService.UserName ?? "System";

            // Reconcile levels by Level number. Prefer undeleting existing rows instead of inserting duplicates
            // (unique index is on TenantId+PolicyId+Level).
            var existingLevels = await _db.EhcEscalationPolicyLevels
                .IgnoreQueryFilters()
                .Where(l => l.PolicyId == policy.Id && l.TenantId == tenantId)
                .ToListAsync(cancellationToken);

            var byLevel = existingLevels.ToDictionary(l => l.Level, l => l);
            var targetLevels = new HashSet<int>(normalizedLevels.Select(l => l.Level));

            foreach (var l in normalizedLevels)
            {
                if (!byLevel.TryGetValue(l.Level, out var entity))
                {
                    entity = new EhcEscalationPolicyLevel
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        PolicyId = policy.Id,
                        CreatedAt = now,
                        CreatedBy = _currentUserService.UserName ?? "System"
                    };
                    _db.EhcEscalationPolicyLevels.Add(entity);
                }

                if (entity.IsDeleted)
                {
                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                    entity.DeletedBy = null;
                }

                entity.Level = l.Level;
                entity.DelayMinutes = Math.Max(0, l.DelayMinutes);
                entity.NotifyRolesJson = SerializeRoles(l.NotifyRoles);
                entity.NotifyAssignedAgent = l.NotifyAssignedAgent;
                entity.NotifyUserId = NormalizeOptionalGuid(l.NotifyUserId);
                entity.AddInternalComment = l.AddInternalComment;
                entity.ReassignToRole = string.IsNullOrWhiteSpace(l.ReassignToRole) ? null : l.ReassignToRole.Trim();
                entity.ReassignToUserId = NormalizeOptionalGuid(l.ReassignToUserId);
                entity.UpdatedAt = now;
                entity.UpdatedBy = _currentUserService.UserName ?? "System";
            }

            foreach (var entity in existingLevels.Where(x => !targetLevels.Contains(x.Level)))
            {
                if (entity.IsDeleted) continue;
                entity.IsDeleted = true;
                entity.DeletedAt = now;
                entity.DeletedBy = _currentUserService.UserName ?? "System";
                entity.UpdatedAt = now;
                entity.UpdatedBy = _currentUserService.UserName ?? "System";
            }

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating EHC escalation policy {PolicyId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update escalation policy" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return BadRequest(new { success = false, message = "Tenant context is required." });

            var policy = await _db.EhcEscalationPolicies.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);
            if (policy == null)
                return NotFound(new { success = false, message = "Policy not found" });

            policy.IsDeleted = true;
            policy.DeletedAt = DateTime.UtcNow;
            policy.DeletedBy = _currentUserService.UserName ?? "System";
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EHC escalation policy {PolicyId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete escalation policy" });
        }
    }

    private static Guid? NormalizeOptionalGuid(Guid? value)
    {
        if (!value.HasValue) return null;
        return value.Value == Guid.Empty ? (Guid?)null : value.Value;
    }

    private static List<EhcEscalationPolicyLevelDto> NormalizeLevels(List<EhcEscalationPolicyLevelDto>? levels)
    {
        var items = (levels ?? new List<EhcEscalationPolicyLevelDto>())
            .Where(l => l != null)
            .Select(l => new EhcEscalationPolicyLevelDto
            {
                Level = Math.Max(1, l.Level),
                DelayMinutes = Math.Max(0, l.DelayMinutes),
                NotifyRoles = (l.NotifyRoles ?? Array.Empty<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                NotifyAssignedAgent = l.NotifyAssignedAgent,
                NotifyUserId = NormalizeOptionalGuid(l.NotifyUserId),
                AddInternalComment = l.AddInternalComment,
                ReassignToRole = string.IsNullOrWhiteSpace(l.ReassignToRole) ? null : l.ReassignToRole.Trim(),
                ReassignToUserId = NormalizeOptionalGuid(l.ReassignToUserId)
            })
            .GroupBy(l => l.Level)
            .Select(g => g.First())
            .OrderBy(l => l.Level)
            .ToList();

        // Ensure levels are sequential 1..n. If not, normalize ordering to avoid gaps.
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Level = i + 1;
        }

        return items;
    }

    private static string? SerializeRoles(string[]? roles)
    {
        var clean = (roles ?? Array.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return clean.Length == 0 ? null : JsonSerializer.Serialize(clean);
    }

    private static string[] DeserializeRoles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            var roles = JsonSerializer.Deserialize<string[]>(json);
            return roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                   ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static EhcEscalationPolicyDto Map(EhcEscalationPolicy p)
    {
        var levels = (p.Levels ?? new List<EhcEscalationPolicyLevel>())
            .Where(l => !l.IsDeleted)
            .OrderBy(l => l.Level)
            .Select(l => new EhcEscalationPolicyLevelDto
            {
                Level = l.Level,
                DelayMinutes = l.DelayMinutes,
                NotifyRoles = DeserializeRoles(l.NotifyRolesJson),
                NotifyAssignedAgent = l.NotifyAssignedAgent,
                NotifyUserId = l.NotifyUserId,
                AddInternalComment = l.AddInternalComment,
                ReassignToRole = l.ReassignToRole,
                ReassignToUserId = l.ReassignToUserId
            })
            .ToList();

        return new EhcEscalationPolicyDto
        {
            Id = p.Id,
            Name = p.Name,
            IsActive = p.IsActive,
            Priority = p.Priority,
            Trigger = p.Trigger,
            DueSoonMinutes = p.DueSoonMinutes,
            TicketType = p.TicketType,
            TicketPriority = p.TicketPriority,
            CategoryId = p.CategoryId,
            SubcategoryId = p.SubcategoryId,
            DepartmentId = p.DepartmentId,
            Levels = levels
        };
    }
}

