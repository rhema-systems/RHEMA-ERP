using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
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
        var supplierCategories = await EnsureCanonicalSupplierCategoriesAsync(
            tenantId,
            actorUserId,
            cancellationToken);
        await ReconcileApprovedSupplierCategoriesAsync(
            tenantId,
            supplierCategories,
            cancellationToken);

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

            var existingDefinition = await _context.WorkflowDefinitions
                .IgnoreQueryFilters()
                .Include(item => item.Steps)
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.Name == template.Name)
                .OrderByDescending(item => item.Version)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingDefinition is not null)
            {
                if (existingDefinition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Draft &&
                    EnsureApprovalStepConfiguration(
                        existingDefinition,
                        template.ApprovalRoleCode))
                {
                    existingDefinition.UpdatedAt = now;
                    existingDefinition.UpdatedBy = "System";
                    existingDefinition.LastModifiedById = actorUserId;
                    await _context.SaveChangesAsync(cancellationToken);
                }
                continue;
            }

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
    /// Enables the standard role-based PO approval route only in environments where
    /// development/UAT data seeding has been explicitly enabled. Existing published
    /// tenant workflows always take precedence and are never replaced.
    /// </summary>
    public async Task EnsurePublishedPurchaseOrderApprovalWorkflowForUatAsync(
        CancellationToken cancellationToken = default)
    {
        var template = ProcurementAccessControlRegistry.Workflows.Single(item =>
            item.Code == "TDC_PURCHASE_ORDER");
        var tenantIds = await _context.Tenants.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            var entityType = await _context.WorkflowEntityTypes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.Code == template.EntityTypeCode,
                    cancellationToken);
            if (entityType is null)
            {
                _logger.LogWarning(
                    "Cannot publish the UAT PO workflow because entity type {EntityType} is missing for tenant {TenantId}",
                    template.EntityTypeCode,
                    tenantId);
                continue;
            }

            var hasPublishedWorkflow = await _context.WorkflowDefinitions
                .IgnoreQueryFilters()
                .AnyAsync(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.EntityTypeId == entityType.Id &&
                    item.IsActive &&
                    item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published,
                    cancellationToken);
            if (hasPublishedWorkflow)
                continue;

            var definition = await _context.WorkflowDefinitions
                .IgnoreQueryFilters()
                .Include(item => item.Steps)
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.EntityTypeId == entityType.Id &&
                    item.Name == template.Name &&
                    item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Draft)
                .OrderByDescending(item => item.Version)
                .FirstOrDefaultAsync(cancellationToken);
            if (definition is null)
            {
                _logger.LogWarning(
                    "Cannot publish the UAT PO workflow because the standard Draft template is missing for tenant {TenantId}",
                    tenantId);
                continue;
            }

            EnsureApprovalStepConfiguration(definition, template.ApprovalRoleCode);
            var now = DateTime.UtcNow;
            definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
            definition.IsActive = true;
            definition.PublishedAt = now;
            definition.PublishedById ??= definition.CreatedById;
            definition.ChangeSummary =
                "Published UAT baseline: PO makers submit and TDC Head of Procurement approves independently.";
            definition.UpdatedAt = now;
            definition.UpdatedBy = "System";
            definition.LastModifiedById = definition.PublishedById;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Published the standard UAT Purchase Order approval workflow for tenant {TenantId}",
                tenantId);
        }
    }

    private async Task<IReadOnlyDictionary<ProcurementSupplierRegistrationCategory, PartnerCategory>>
        EnsureCanonicalSupplierCategoriesAsync(
            Guid tenantId,
            Guid? actorUserId,
            CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var categoryCodes = ProcurementSupplierCategoryRegistry.Definitions
            .Select(item => item.Code)
            .ToArray();
        var existing = await _context.PartnerCategories
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && categoryCodes.Contains(item.CategoryCode))
            .ToListAsync(cancellationToken);
        var byCode = existing.ToDictionary(item => item.CategoryCode, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<ProcurementSupplierRegistrationCategory, PartnerCategory>();

        foreach (var definition in ProcurementSupplierCategoryRegistry.Definitions)
        {
            if (!byCode.TryGetValue(definition.Code, out var category))
            {
                category = new PartnerCategory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CategoryCode = definition.Code,
                    CategoryName = definition.Name,
                    CategoryType = "Supplier",
                    Description = definition.Description,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "System",
                    CreatedById = actorUserId
                };
                _context.PartnerCategories.Add(category);
            }
            else
            {
                category.IsActive = true;
                category.IsDeleted = false;
                category.DeletedAt = null;
                category.DeletedBy = null;
                category.UpdatedAt = now;
                category.UpdatedBy = "System";
                category.LastModifiedById = actorUserId;
            }

            result[definition.RegistrationCategory] = category;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task ReconcileApprovedSupplierCategoriesAsync(
        Guid tenantId,
        IReadOnlyDictionary<ProcurementSupplierRegistrationCategory, PartnerCategory> categories,
        CancellationToken cancellationToken)
    {
        var registrations = await _context.BusinessPartnerRegistrations
            .IgnoreQueryFilters()
            .Where(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                item.Status == "Approved" &&
                item.BusinessPartnerId.HasValue &&
                item.RegistrationCategory.HasValue)
            .Select(item => new
            {
                BusinessPartnerId = item.BusinessPartnerId!.Value,
                RegistrationCategory = item.RegistrationCategory!.Value
            })
            .ToListAsync(cancellationToken);
        if (registrations.Count == 0)
            return;

        var partnerIds = registrations.Select(item => item.BusinessPartnerId).Distinct().ToArray();
        var validPartnerIds = (await _context.BusinessPartners
                .IgnoreQueryFilters()
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    partnerIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var existingAssignments = await _context.BusinessPartnerCategories
            .Where(item => partnerIds.Contains(item.BusinessPartnerId))
            .Select(item => new { item.BusinessPartnerId, item.CategoryId })
            .ToListAsync(cancellationToken);
        var assignmentKeys = existingAssignments
            .Select(item => (item.BusinessPartnerId, item.CategoryId))
            .ToHashSet();
        var partnersWithCategories = existingAssignments
            .Select(item => item.BusinessPartnerId)
            .ToHashSet();
        var added = 0;

        foreach (var registration in registrations)
        {
            if (!validPartnerIds.Contains(registration.BusinessPartnerId) ||
                !categories.TryGetValue(registration.RegistrationCategory, out var category) ||
                assignmentKeys.Contains((registration.BusinessPartnerId, category.Id)))
                continue;

            _context.BusinessPartnerCategories.Add(new BusinessPartnerCategory
            {
                Id = Guid.NewGuid(),
                BusinessPartnerId = registration.BusinessPartnerId,
                CategoryId = category.Id,
                IsPrimary = !partnersWithCategories.Contains(registration.BusinessPartnerId)
            });
            assignmentKeys.Add((registration.BusinessPartnerId, category.Id));
            partnersWithCategories.Add(registration.BusinessPartnerId);
            added++;
        }

        if (added == 0)
            return;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Reconciled {Count} approved supplier onboarding category assignments for tenant {TenantId}",
            added,
            tenantId);
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
        Configuration = stepType == WorkflowStepType.Approval && requiredRole is not null
            ? CreateApprovalStepConfiguration(requiredRole)
            : null,
        CreatedAt = now,
        CreatedBy = "System",
        CreatedById = actorUserId
    };

    private static bool EnsureApprovalStepConfiguration(
        WorkflowDefinition definition,
        string approvalRoleCode)
    {
        var approvalStep = definition.Steps
            .OrderBy(item => item.Order)
            .FirstOrDefault(item => item.StepType == WorkflowStepType.Approval);
        if (approvalStep is null || HasConfiguredApprover(approvalStep.Configuration))
            return false;

        approvalStep.RequiredRole = approvalRoleCode;
        approvalStep.AssignmentType = "Role";
        approvalStep.AssignmentConfiguration = JsonSerializer.Serialize(new
        {
            role = approvalRoleCode
        });
        approvalStep.Configuration = CreateApprovalStepConfiguration(approvalRoleCode);
        return true;
    }

    private static bool HasConfiguredApprover(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
            return false;

        try
        {
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(
                    configuration,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?
                .ApprovalConfig?.ApproverRules.Any() == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string CreateApprovalStepConfiguration(string approvalRoleCode) =>
        JsonSerializer.Serialize(new WorkflowStepConfigurationDto
        {
            ApprovalConfig = new WorkflowApprovalConfigDto
            {
                ApprovalType = WorkflowApprovalType.Single,
                ActivationMode = WorkflowApprovalActivationMode.Parallel,
                MinApprovalsRequired = 1,
                PreventInitiatorApproval = true,
                RequireDistinctApprovers = true,
                RejectionHandling = WorkflowRejectionHandling.StopWorkflow,
                ApproverRules =
                [
                    new WorkflowAssignmentRuleDto
                    {
                        ApprovalGroup = 1,
                        AssignmentType = WorkflowAssignmentType.Role,
                        Role = approvalRoleCode,
                        Priority = 100
                    }
                ]
            }
        });

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
