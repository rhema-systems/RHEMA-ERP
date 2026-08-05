using ErpSystem.Core.Entities.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Installs the current TDC Finance close control set for new and existing active tenants.
/// Approved templates are immutable, so an older TDC baseline is superseded by a new version
/// rather than edited. A custom active template is a tenant governance decision and is never
/// silently replaced by startup reconciliation.
/// </summary>
public sealed class FinanceCloseTemplateBaselineSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FinanceCloseTemplateBaselineSeeder> _logger;

    public FinanceCloseTemplateBaselineSeeder(
        ApplicationDbContext context,
        ILogger<FinanceCloseTemplateBaselineSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedAllActiveTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(tenant => tenant.Status == TenantStatus.Active && !tenant.IsDeleted)
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);

        var added = 0;
        foreach (var tenantId in tenantIds)
        {
            added += await SeedTenantBaselineAsync(_context, tenantId, cancellationToken);
        }

        _logger.LogInformation(
            "Finance close-template baseline reconciliation completed for {TenantCount} active tenant(s); {AddedCount} template version(s) added",
            tenantIds.Count,
            added);
        return added;
    }

    public async Task<int> SeedTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var added = await SeedTenantBaselineAsync(_context, tenantId, cancellationToken);
        _logger.LogInformation(
            "Finance close-template baseline reconciliation completed for tenant {TenantId}; {AddedCount} template version(s) added",
            tenantId,
            added);
        return added;
    }

    public static async Task<int> SeedTenantBaselineAsync(
        ApplicationDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var added = 0;
        var changed = false;
        var currentControlCodes = FinanceCloseTemplateBaselineCatalog.Tasks
            .Where(task => task.IsAutomated && task.CheckCode != null)
            .Select(task => task.CheckCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var baseline in FinanceCloseTemplateBaselineCatalog.Templates)
        {
            var existing = await context.FinanceCloseTemplates
                .IgnoreQueryFilters()
                .Include(template => template.TaskDefinitions)
                .Where(template => template.TenantId == tenantId &&
                    template.CloseType == baseline.CloseType && !template.IsDeleted)
                .ToListAsync(cancellationToken);

            // A Finance administrator's active template is explicit tenant policy. The seeder
            // must not bypass its maker-checker approval by activating a system replacement.
            if (existing.Any(template => template.IsActive && !template.IsSystemDefault))
            {
                continue;
            }

            var currentBaseline = existing.FirstOrDefault(template =>
                template.IsSystemDefault &&
                template.Status == FinanceCloseTemplateStatuses.Approved &&
                currentControlCodes.All(code => template.TaskDefinitions.Any(task =>
                    string.Equals(task.CheckCode, code, StringComparison.OrdinalIgnoreCase))));

            if (currentBaseline != null)
            {
                // Repair activation only when no custom active version exists. This can occur
                // after an interrupted development reset, but it does not alter approved tasks.
                if (!currentBaseline.IsActive)
                {
                    SupersedeActiveSystemBaselines(existing, currentBaseline.Id, now);
                    currentBaseline.IsActive = true;
                    currentBaseline.UpdatedAt = now;
                    currentBaseline.UpdatedBy = FinanceCloseTemplateBaselineCatalog.SystemActorName;
                    changed = true;
                }
                continue;
            }

            changed |= SupersedeActiveSystemBaselines(existing, exceptTemplateId: null, now);
            var nextVersion = existing.Count == 0 ? 1 : existing.Max(template => template.Version) + 1;
            var template = BuildTemplate(tenantId, baseline, nextVersion, now);
            context.FinanceCloseTemplates.Add(template);
            added++;
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return added;
    }

    private static FinanceCloseTemplate BuildTemplate(
        Guid tenantId,
        FinanceCloseTemplateBaselineDefinition baseline,
        int version,
        DateTime now)
    {
        var template = new FinanceCloseTemplate
        {
            TenantId = tenantId,
            TemplateCode = baseline.TemplateCode,
            Name = baseline.Name,
            CloseType = baseline.CloseType,
            Version = version,
            Status = FinanceCloseTemplateStatuses.Approved,
            IsActive = true,
            IsSystemDefault = true,
            Description = FinanceCloseTemplateBaselineCatalog.Description,
            ApprovedByUserName = FinanceCloseTemplateBaselineCatalog.SystemActorName,
            ApprovedAt = now,
            ApprovalDeclaration = $"System-provided TDC Finance close control set v{FinanceCloseTemplateBaselineCatalog.ControlSetVersion}.",
            CreatedAt = now,
            CreatedBy = FinanceCloseTemplateBaselineCatalog.SystemActorName
        };

        foreach (var definition in FinanceCloseTemplateBaselineCatalog.Tasks)
        {
            template.TaskDefinitions.Add(new FinanceCloseTemplateTaskDefinition
            {
                TenantId = tenantId,
                FinanceCloseTemplateId = template.Id,
                TaskCode = definition.TaskCode,
                Title = definition.Title,
                Category = definition.Category,
                DependsOnTaskCode = definition.DependsOnTaskCode,
                CheckCode = definition.CheckCode,
                Sequence = definition.Sequence,
                IsMandatory = definition.IsMandatory,
                IsAutomated = definition.IsAutomated,
                DueDaysAfterPeriodEnd = baseline.DueDaysAfterPeriodEnd,
                Instructions = definition.Instructions,
                CreatedAt = now,
                CreatedBy = FinanceCloseTemplateBaselineCatalog.SystemActorName
            });
        }

        return template;
    }

    private static bool SupersedeActiveSystemBaselines(
        IEnumerable<FinanceCloseTemplate> templates,
        Guid? exceptTemplateId,
        DateTime now)
    {
        var changed = false;
        foreach (var template in templates.Where(item =>
            item.IsSystemDefault && item.IsActive && item.Id != exceptTemplateId))
        {
            template.IsActive = false;
            template.Status = FinanceCloseTemplateStatuses.Superseded;
            template.SupersededAt = now;
            template.UpdatedAt = now;
            template.UpdatedBy = FinanceCloseTemplateBaselineCatalog.SystemActorName;
            changed = true;
        }

        return changed;
    }
}
