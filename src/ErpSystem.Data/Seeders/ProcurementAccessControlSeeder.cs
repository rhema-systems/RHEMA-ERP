using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed class ProcurementAccessControlSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProcurementAccessControlSeeder> _logger;

    public ProcurementAccessControlSeeder(
        ApplicationDbContext context,
        ILogger<ProcurementAccessControlSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureIdentityAccessModelAsync(cancellationToken);
        var tenantIds = await _context.Tenants.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
            await SeedTenantAsync(tenantId, null, cancellationToken);
    }

    public async Task SeedTenantAsync(
        Guid tenantId,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        await EnsureIdentityAccessModelAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var template in ProcurementAccessControlRegistry.Committees)
        {
            var exists = await _context.ProcurementCommittees.IgnoreQueryFilters().AnyAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.Code == template.Code,
                cancellationToken);
            if (exists) continue;
            _context.ProcurementCommittees.Add(new ProcurementCommittee
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = template.Code,
                Name = template.Name,
                Description = template.Description,
                CommitteeType = Enum.Parse<ProcurementCommitteeType>(template.CommitteeType),
                Status = ProcurementCommitteeStatus.Draft,
                RequiredQuorum = template.SuggestedQuorum,
                RequiredRoleName = template.RequiredRoleCode,
                EffectiveFrom = now.Date,
                ChangeReason = "Unapproved TDC committee template. Confirm membership and quorum before activation.",
                CreatedAt = now,
                CreatedBy = "System",
                CreatedById = actorUserId
            });
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var template in ProcurementAccessControlRegistry.Workflows)
        {
            // Workflow entity types are shared across Finance, Inventory and Procurement.
            // Prefer the TDC code where it exists, but reuse the already-governed entity
            // type when another module owns the canonical code for the same business name
            // (for example, Finance's SupplierReturn). Creating a second entity type with
            // the same tenant/name violates the central workflow uniqueness constraint.
            var entityType = await _context.WorkflowEntityTypes.IgnoreQueryFilters().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                (item.Code == template.EntityTypeCode || item.Name == template.EntityTypeName),
                cancellationToken);
            if (entityType is null)
            {
                entityType = new WorkflowEntityType
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = template.EntityTypeCode,
                    Name = template.EntityTypeName,
                    Description = $"TDC procurement shared-workflow entity for {template.EntityTypeName}.",
                    IsActive = true,
                    DisplayOrder = 500,
                    CreatedAt = now,
                    CreatedBy = "System",
                    CreatedById = actorUserId
                };
                _context.WorkflowEntityTypes.Add(entityType);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var definitionExists = await _context.WorkflowDefinitions.IgnoreQueryFilters().AnyAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.Name == template.Name,
                cancellationToken);
            if (definitionExists) continue;

            var definitionId = Guid.NewGuid();
            var submittedId = Guid.NewGuid();
            var approvalId = Guid.NewGuid();
            var completeId = Guid.NewGuid();
            var definition = new WorkflowDefinition
            {
                Id = definitionId,
                TenantId = tenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = template.Name,
                Description = template.Description,
                EntityTypeId = entityType.Id,
                Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft,
                IsActive = false,
                ChangeSummary = "TDC-0005 unapproved Draft template; DEC-003 and DEC-004 must approve routing before publication.",
                Configuration = JsonSerializer.Serialize(new
                {
                    templateCode = template.Code,
                    source = "TDC-0005",
                    approvalState = "unapproved",
                    decisionDependencies = new[] { "DEC-003", "DEC-004" }
                }),
                CreatedAt = now,
                CreatedBy = "System",
                CreatedById = actorUserId
            };
            definition.Steps.Add(CreateStep(submittedId, definitionId, tenantId, "Submitted", 1, true, false,
                WorkflowStepType.Manual, template.InitiatorRoleCode, now, actorUserId));
            definition.Steps.Add(CreateStep(approvalId, definitionId, tenantId, "Approval", 2, false, false,
                WorkflowStepType.Approval, template.ApprovalRoleCode, now, actorUserId));
            definition.Steps.Add(CreateStep(completeId, definitionId, tenantId, "Completed", 3, false, true,
                WorkflowStepType.Automatic, null, now, actorUserId));
            definition.Transitions.Add(CreateTransition(definitionId, tenantId, submittedId, approvalId, "Submit for approval", 1, now, actorUserId));
            definition.Transitions.Add(CreateTransition(definitionId, tenantId, approvalId, completeId, "Approve", 1, now, actorUserId));
            _context.WorkflowDefinitions.Add(definition);
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Ensured TDC access, committee, and Draft workflow templates for tenant {TenantId}", tenantId);
    }

    private async Task EnsureIdentityAccessModelAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var definition in ProcurementAccessControlRegistry.Roles)
        {
            var normalized = definition.Code.ToUpperInvariant();
            var role = await _context.Roles.FirstOrDefaultAsync(item => item.NormalizedName == normalized, cancellationToken);
            if (role is null)
            {
                role = new ApplicationRole(definition.Code)
                {
                    Id = Guid.NewGuid(),
                    NormalizedName = normalized,
                    Description = definition.Description,
                    IsSystemRole = true,
                    CreatedAt = now,
                    CreatedBy = "System"
                };
                _context.Roles.Add(role);
            }
            else
            {
                role.Description = definition.Description;
                role.IsSystemRole = true;
            }
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var definition in ProcurementAccessControlRegistry.Permissions)
        {
            var permission = await _context.Permissions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Name == definition.Code, cancellationToken);
            if (permission is null)
            {
                permission = new Permission { Id = Guid.NewGuid(), Name = definition.Code, CreatedAt = now, CreatedBy = "System" };
                _context.Permissions.Add(permission);
            }
            permission.DisplayName = definition.Name;
            permission.Description = definition.Description;
            permission.Category = ProcurementAccessControlRegistry.Category;
            permission.IsSystemPermission = true;
            permission.IsDeleted = false;
            permission.DeletedAt = null;
            permission.DeletedBy = null;
        }
        await _context.SaveChangesAsync(cancellationToken);

        var roles = await _context.Roles.Where(item => item.Name != null).ToDictionaryAsync(item => item.Name!, cancellationToken);
        var permissions = await _context.Permissions
            .Where(item => item.Category == ProcurementAccessControlRegistry.Category)
            .ToDictionaryAsync(item => item.Name, cancellationToken);
        foreach (var roleDefinition in ProcurementAccessControlRegistry.Roles)
        {
            var role = roles[roleDefinition.Code];
            var existing = await _context.RolePermissions.Where(item => item.RoleId == role.Id)
                .Select(item => item.PermissionId).ToListAsync(cancellationToken);
            foreach (var permissionCode in roleDefinition.PermissionCodes)
            {
                var permission = permissions[permissionCode];
                if (existing.Contains(permission.Id)) continue;
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    GrantedAt = now,
                    GrantedBy = "System"
                });
            }
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static WorkflowStep CreateStep(
        Guid id, Guid definitionId, Guid tenantId, string name, int order, bool isStart, bool isEnd,
        WorkflowStepType stepType, string? requiredRole, DateTime now, Guid? actorUserId) => new()
    {
        Id = id,
        TenantId = tenantId,
        WorkflowDefinitionId = definitionId,
        Name = name,
        StepType = stepType,
        Order = order,
        IsStartStep = isStart,
        IsEndStep = isEnd,
        IsRequired = true,
        RequiredRole = requiredRole,
        AssignmentType = requiredRole is null ? "System" : "Role",
        AssignmentConfiguration = requiredRole is null ? null : JsonSerializer.Serialize(new { role = requiredRole }),
        CreatedAt = now,
        CreatedBy = "System",
        CreatedById = actorUserId
    };

    private static WorkflowTransition CreateTransition(
        Guid definitionId, Guid tenantId, Guid fromStepId, Guid toStepId, string name, int priority,
        DateTime now, Guid? actorUserId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        WorkflowDefinitionId = definitionId,
        FromStepId = fromStepId,
        ToStepId = toStepId,
        Name = name,
        IsDefault = true,
        Priority = priority,
        CreatedAt = now,
        CreatedBy = "System",
        CreatedById = actorUserId
    };
}
