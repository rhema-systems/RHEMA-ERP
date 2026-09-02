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
        await ReconcileIdentityAccessBaselineAsync(cancellationToken);
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
        await ReconcileIdentityAccessBaselineAsync(cancellationToken);

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

    /// <summary>
    /// Reconciles the global TDC procurement security baseline used by every tenant.
    /// This path is intentionally missing-only: it creates absent roles, permissions,
    /// and grants, but does not rename roles, rewrite configured permission metadata,
    /// remove additional grants, or assign TDC privileges to legacy generic roles.
    /// </summary>
    public async Task ReconcileIdentityAccessBaselineAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var createdRoles = 0;
        var createdPermissions = 0;
        var createdGrants = 0;

        var requiredPermissionCodes = ProcurementAccessControlRegistry.Permissions
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingPermissions = await _context.Permissions.IgnoreQueryFilters()
            .Where(item => requiredPermissionCodes.Contains(item.Name))
            .ToListAsync(cancellationToken);
        var permissionsByName = existingPermissions
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var deletedRequiredPermissions = existingPermissions
            .Where(item => item.IsDeleted)
            .Select(item => item.Name)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (deletedRequiredPermissions.Length > 0)
        {
            throw new InvalidOperationException(
                "Required TDC procurement system permissions are soft-deleted: " +
                string.Join(", ", deletedRequiredPermissions) +
                ". Restore them through the governed security-administration path before startup can continue.");
        }

        var existingRoles = await _context.Roles.ToListAsync(cancellationToken);
        var rolesByNormalizedName = existingRoles
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.NormalizedName))
            .GroupBy(item => NormalizeRoleName(item.NormalizedName ?? item.Name!))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var definition in ProcurementAccessControlRegistry.Roles)
        {
            var normalized = NormalizeRoleName(definition.Code);
            if (rolesByNormalizedName.ContainsKey(normalized))
            {
                continue;
            }

            var role = new ApplicationRole(definition.Code)
            {
                Id = Guid.NewGuid(),
                NormalizedName = normalized,
                Description = definition.Description,
                IsSystemRole = true,
                CreatedAt = now,
                CreatedBy = "System"
            };
            _context.Roles.Add(role);
            rolesByNormalizedName.Add(normalized, role);
            createdRoles++;
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var definition in ProcurementAccessControlRegistry.Permissions)
        {
            if (permissionsByName.ContainsKey(definition.Code))
            {
                continue;
            }

            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = definition.Code,
                DisplayName = definition.Name,
                Description = definition.Description,
                Category = ProcurementAccessControlRegistry.Category,
                IsSystemPermission = true,
                CreatedAt = now,
                CreatedBy = "System"
            };
            _context.Permissions.Add(permission);
            permissionsByName.Add(definition.Code, permission);
            createdPermissions++;
        }
        await _context.SaveChangesAsync(cancellationToken);

        var baselineRoleIds = rolesByNormalizedName.Values.Select(item => item.Id).ToArray();
        var existingGrants = (await _context.RolePermissions
                .Where(item => baselineRoleIds.Contains(item.RoleId))
                .Select(item => new { item.RoleId, item.PermissionId })
                .ToListAsync(cancellationToken))
            .Select(item => (item.RoleId, item.PermissionId))
            .ToHashSet();

        foreach (var roleDefinition in ProcurementAccessControlRegistry.Roles)
        {
            var role = rolesByNormalizedName[NormalizeRoleName(roleDefinition.Code)];
            foreach (var permissionCode in roleDefinition.PermissionCodes)
            {
                var permission = permissionsByName[permissionCode];
                if (!existingGrants.Add((role.Id, permission.Id))) continue;
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    GrantedAt = now,
                    GrantedBy = "System"
                });
                createdGrants++;
            }
        }
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Reconciled global TDC procurement security baseline: created roles={CreatedRoles}, permissions={CreatedPermissions}, grants={CreatedGrants}; existing configured metadata and additional grants were preserved.",
            createdRoles,
            createdPermissions,
            createdGrants);
    }

    private static string NormalizeRoleName(string value) => value.Trim().ToUpperInvariant();

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
