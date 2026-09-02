using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed class CivilEngineeringAccessControlSeeder(
    ApplicationDbContext context,
    ILogger<CivilEngineeringAccessControlSeeder> logger)
{
    public async Task SeedAsync(CancellationToken token = default)
    {
        var now = DateTime.UtcNow;
        foreach (var definition in CivilEngineeringAccessControlRegistry.Roles)
        {
            var normalized = definition.Code.ToUpperInvariant();
            var role = await context.Roles
                .FirstOrDefaultAsync(item => item.NormalizedName == normalized, token);
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
                context.Roles.Add(role);
            }
            else
            {
                role.Description = definition.Description;
                role.IsSystemRole = true;
            }
        }
        await context.SaveChangesAsync(token);

        foreach (var definition in CivilEngineeringAccessControlRegistry.Permissions)
        {
            var permission = await context.Permissions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Name == definition.Code, token);
            if (permission is null)
            {
                permission = new Permission { Id = Guid.NewGuid(), Name = definition.Code, CreatedAt = now, CreatedBy = "System" };
                context.Permissions.Add(permission);
            }
            permission.DisplayName = definition.Name;
            permission.Description = definition.Description;
            permission.Category = CivilEngineeringAccessControlRegistry.Category;
            permission.IsSystemPermission = true;
            permission.IsDeleted = false;
            permission.DeletedAt = null;
            permission.DeletedBy = null;
        }

        var centralProjectAccess = await context.Permissions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Name == CivilEngineeringAccessControlRegistry.CentralProjectAccess, token);
        if (centralProjectAccess is null)
        {
            centralProjectAccess = new Permission
            {
                Id = Guid.NewGuid(),
                Name = CivilEngineeringAccessControlRegistry.CentralProjectAccess,
                DisplayName = "Access Project Management",
                Description = "Access the project management module",
                Category = "Module Access",
                IsSystemPermission = true,
                CreatedAt = now,
                CreatedBy = "System"
            };
            context.Permissions.Add(centralProjectAccess);
        }
        else
        {
            centralProjectAccess.IsDeleted = false;
            centralProjectAccess.DeletedAt = null;
            centralProjectAccess.DeletedBy = null;
        }
        await context.SaveChangesAsync(token);

        var roles = await context.Roles
            .Where(item => item.Name != null)
            .ToDictionaryAsync(item => item.Name!, StringComparer.OrdinalIgnoreCase, token);
        var permissionCodes = CivilEngineeringAccessControlRegistry.Permissions
            .Select(item => item.Code)
            .Append(CivilEngineeringAccessControlRegistry.CentralProjectAccess)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = (await context.Permissions
                .Where(item => permissionCodes.Contains(item.Name))
                .ToListAsync(token))
            .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in CivilEngineeringAccessControlRegistry.Roles)
        {
            var role = roles[definition.Code];
            var existingPermissionIds = (await context.RolePermissions
                    .Where(item => item.RoleId == role.Id)
                    .Select(item => item.PermissionId)
                    .ToListAsync(token))
                .ToHashSet();

            foreach (var permissionCode in definition.Permissions.Append(CivilEngineeringAccessControlRegistry.CentralProjectAccess))
            {
                var permission = permissions[permissionCode];
                if (existingPermissionIds.Add(permission.Id))
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id,
                        GrantedAt = now,
                        GrantedBy = "System"
                    });
                }
            }
        }

        await context.SaveChangesAsync(token);
        await EnsureWorkflowEntityTypesAsync(now, token);
        logger.LogInformation(
            "Ensured {PermissionCount} Civil Engineering permissions, {RoleCount} shared security roles and {WorkflowEntityTypeCount} shared workflow entity types",
            CivilEngineeringAccessControlRegistry.Permissions.Count,
            CivilEngineeringAccessControlRegistry.Roles.Count,
            CivilEngineeringWorkflowBindingRegistry.EntityTypes.Count);
    }

    private async Task EnsureWorkflowEntityTypesAsync(DateTime now, CancellationToken token)
    {
        var tenantIds = await context.Tenants.AsNoTracking()
            .Where(value => !value.IsDeleted)
            .Select(value => value.Id)
            .ToListAsync(token);

        foreach (var tenantId in tenantIds)
        {
            foreach (var definition in CivilEngineeringWorkflowBindingRegistry.EntityTypes)
            {
                var entityType = await context.WorkflowEntityTypes.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(value =>
                        value.TenantId == tenantId && value.Code == definition.Code, token);
                if (entityType is null)
                {
                    context.WorkflowEntityTypes.Add(new WorkflowEntityType
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Code = definition.Code,
                        Name = definition.Name,
                        Description = definition.Description,
                        EntityClassName = typeof(ICivilEngineeringWorkflowRecord).FullName,
                        IsActive = true,
                        DisplayOrder = 650,
                        CreatedAt = now,
                        CreatedBy = "System"
                    });
                    continue;
                }

                entityType.Name = definition.Name;
                entityType.Description = definition.Description;
                entityType.EntityClassName = typeof(ICivilEngineeringWorkflowRecord).FullName;
                entityType.IsActive = true;
                entityType.IsDeleted = false;
                entityType.DeletedAt = null;
                entityType.DeletedBy = null;
            }
        }

        await context.SaveChangesAsync(token);
    }
}
