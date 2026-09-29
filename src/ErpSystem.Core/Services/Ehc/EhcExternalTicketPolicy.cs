using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Ehc;

/// <summary>Normalizes supplier-portal input before SLA, workflow and audit selection.</summary>
public static class EhcExternalTicketPolicy
{
    public static async Task ApplyAsync(IUnitOfWork unitOfWork, Guid tenantId,
        CreateEhcTicketRequestDto request, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new InvalidOperationException("Tenant context is required.");
        if (!request.CategoryId.HasValue || request.CategoryId == Guid.Empty)
            throw new ArgumentException("Select a helpdesk category.", nameof(request.CategoryId));

        var categories = await unitOfWork.Repository<EhcTicketCategory>().GetQueryable()
            .AsNoTracking().Where(value => value.TenantId == tenantId && !value.IsDeleted)
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        if (!categories.ContainsKey(request.CategoryId.Value))
            throw new ArgumentException("The selected helpdesk category is unavailable.", nameof(request.CategoryId));

        var selectedId = request.SubcategoryId.HasValue && request.SubcategoryId != Guid.Empty
            ? request.SubcategoryId.Value : request.CategoryId.Value;
        var visited = new HashSet<Guid>();
        EhcTicketType? configuredType = null;
        while (true)
        {
            if (!visited.Add(selectedId) || !categories.TryGetValue(selectedId, out var category))
                throw new ArgumentException("The selected subcategory is not part of the selected helpdesk category.", nameof(request.SubcategoryId));
            configuredType ??= category.AppliesToType;
            if (selectedId == request.CategoryId.Value) break;
            if (!category.ParentCategoryId.HasValue || category.ParentCategoryId == Guid.Empty)
                throw new ArgumentException("The selected subcategory is not part of the selected helpdesk category.", nameof(request.SubcategoryId));
            selectedId = category.ParentCategoryId.Value;
        }
        if (!configuredType.HasValue || !Enum.IsDefined(configuredType.Value))
            throw new ArgumentException("This category has no helpdesk type configured. Please choose another category or contact support.", nameof(request.CategoryId));

        var priorities = await unitOfWork.Repository<EhcTicketPriorityLevel>().GetQueryable()
            .AsNoTracking().Where(value => value.TenantId == tenantId && !value.IsDeleted)
            .OrderBy(value => value.SortOrder).ThenBy(value => value.Priority).ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);
        var active = priorities.Where(value => value.IsActive && Enum.IsDefined(value.Priority)).ToList();
        if (priorities.Count > 0 && active.Count == 0)
            throw new ArgumentException("No helpdesk priority is enabled. Please contact support to enable a default priority.");

        request.TicketType = configuredType.Value;
        request.Priority = active.FirstOrDefault(value => value.Priority == EhcTicketPriority.Medium)?.Priority
            ?? active.FirstOrDefault()?.Priority ?? EhcTicketPriority.Medium;
        request.Source = EhcTicketSource.Web;
    }
}
