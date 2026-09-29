using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed class QuantitySurveyAccessControlSeeder(ApplicationDbContext context, ILogger<QuantitySurveyAccessControlSeeder> logger)
{
    public async Task SeedAsync(CancellationToken token = default, Guid? tenantId = null)
    {
        if (tenantId.HasValue && !await context.Tenants.AsNoTracking()
                .AnyAsync(value => value.Id == tenantId.Value && !value.IsDeleted, token))
            throw new InvalidOperationException("The requested access-control seed tenant does not exist or is deleted.");
        var now = DateTime.UtcNow;
        foreach (var definition in QuantitySurveyAccessControlRegistry.Roles)
        {
            var normalized = definition.Code.ToUpperInvariant(); var role = await context.Roles.FirstOrDefaultAsync(x => x.NormalizedName == normalized, token);
            if (role is null) { role = new ApplicationRole(definition.Code) { Id = Guid.NewGuid(), NormalizedName = normalized, Description = definition.Description, IsSystemRole = true, CreatedAt = now, CreatedBy = "System" }; context.Roles.Add(role); }
            else { role.Description = definition.Description; role.IsSystemRole = true; }
        }
        await context.SaveChangesAsync(token);
        foreach (var definition in QuantitySurveyAccessControlRegistry.Permissions)
        {
            var permission = await context.Permissions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Name == definition.Code, token);
            if (permission is null) { permission = new Permission { Id = Guid.NewGuid(), Name = definition.Code, CreatedAt = now, CreatedBy = "System" }; context.Permissions.Add(permission); }
            permission.DisplayName = definition.Name; permission.Description = definition.Description; permission.Category = QuantitySurveyAccessControlRegistry.Category; permission.IsSystemPermission = true; permission.IsDeleted = false; permission.DeletedAt = null; permission.DeletedBy = null;
        }
        await context.SaveChangesAsync(token);
        var roles = await context.Roles.Where(x => x.Name != null).ToDictionaryAsync(x => x.Name!, token); var permissions = await context.Permissions.Where(x => x.Category == QuantitySurveyAccessControlRegistry.Category).ToDictionaryAsync(x => x.Name, token);
        foreach (var definition in QuantitySurveyAccessControlRegistry.Roles)
        {
            var role = roles[definition.Code]; var existing = await context.RolePermissions.Where(x => x.RoleId == role.Id).Select(x => x.PermissionId).ToListAsync(token);
            foreach (var code in definition.Permissions) if (!existing.Contains(permissions[code].Id)) context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissions[code].Id, GrantedAt = now, GrantedBy = "System" });
        }
        await context.SaveChangesAsync(token);
        await EnsureWorkflowEntityTypesAsync(now, tenantId, token);
        logger.LogInformation("Ensured QS permissions, roles, and shared-workflow entity types");
    }

    private async Task EnsureWorkflowEntityTypesAsync(DateTime now, Guid? selectedTenantId, CancellationToken token)
    {
        var tenantIds = await context.Tenants.AsNoTracking()
            .Where(value => !value.IsDeleted && (!selectedTenantId.HasValue || value.Id == selectedTenantId.Value))
            .Select(value => value.Id)
            .ToListAsync(token);
        foreach (var tenantId in tenantIds)
        {
            foreach (var definition in QuantitySurveyWorkflowBindingRegistry.EntityTypes)
            {
                var entityType = await context.WorkflowEntityTypes.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(value =>
                        value.TenantId == tenantId && value.Code == definition.Code, token);
                if (entityType is null)
                {
                    entityType = new WorkflowEntityType
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Code = definition.Code,
                        Name = definition.Name,
                        Description = definition.Description,
                        EntityClassName = typeof(IQuantitySurveyWorkflowRecord).FullName,
                        IsActive = true,
                        DisplayOrder = 600,
                        CreatedAt = now,
                        CreatedBy = "System"
                    };
                    context.WorkflowEntityTypes.Add(entityType);
                    continue;
                }

                entityType.Name = definition.Name;
                entityType.Description = definition.Description;
                entityType.EntityClassName = typeof(IQuantitySurveyWorkflowRecord).FullName;
                entityType.IsActive = true;
                entityType.IsDeleted = false;
                entityType.DeletedAt = null;
                entityType.DeletedBy = null;
            }
        }

        await context.SaveChangesAsync(token);
    }
}
