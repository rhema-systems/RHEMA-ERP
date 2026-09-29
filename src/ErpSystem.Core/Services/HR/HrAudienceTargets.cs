using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The two questions every stored audience target raises — "what is this id called?" and "is it a
/// real record of that kind?" — answered once (round 4, lane I).
/// </summary>
/// <remarks>
/// <para><b>Why static over the unit of work, not a service.</b> Orientation audience rules and
/// onboarding template audiences both need it, and the services that own those two depend on each
/// other in one direction already (the trigger diagnostic reports the onboarding template). A third
/// injectable would be a cycle waiting to happen; a pair of functions over the repositories cannot
/// be.</para>
///
/// <para><b>Validation is the point.</b> The target id used to be a free-text GUID box, so a rule
/// could name a unit that did not exist — or a position id under "unit" — and nothing noticed:
/// the rule simply reached nobody, silently, for ever.</para>
/// </remarks>
public static class HrAudienceTargets
{
    public static bool NeedsTarget(HrAudienceTargetType type) => type != HrAudienceTargetType.AllEmployees;

    public static string TypeLabel(HrAudienceTargetType type) => type switch
    {
        HrAudienceTargetType.AllEmployees => "Everyone",
        HrAudienceTargetType.OrganizationUnit => "Organisation unit",
        HrAudienceTargetType.OrganizationLevel => "Organisation level",
        HrAudienceTargetType.Position => "Position",
        HrAudienceTargetType.Location => "Location",
        HrAudienceTargetType.Employee => "Employee",
        _ => type.ToString(),
    };

    /// <summary>Refuses a target no resolver could evaluate. Throws <see cref="InvalidOperationException"/> (a 422).</summary>
    public static async Task ValidateAsync(
        IUnitOfWork unitOfWork, Guid tenantId, HrAudienceTargetType type, Guid? targetId, bool allowEmployee,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(type))
            throw new InvalidOperationException($"'{(int)type}' is not an audience target type.");

        if (type == HrAudienceTargetType.Employee && !allowEmployee)
            throw new InvalidOperationException(
                "A single employee cannot be targeted here — this audience is decided before the person is an employee.");

        if (!NeedsTarget(type))
        {
            if (targetId is { } stray && stray != Guid.Empty)
                throw new InvalidOperationException("'Everyone' takes no target — clear the target, or choose what it narrows to.");
            return;
        }

        if (targetId is not { } id || id == Guid.Empty)
            throw new InvalidOperationException($"Choose the {TypeLabel(type).ToLowerInvariant()} this targets.");

        var exists = type switch
        {
            HrAudienceTargetType.OrganizationUnit => await unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                .AnyAsync(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted, cancellationToken),
            HrAudienceTargetType.OrganizationLevel => await unitOfWork.Repository<OrganizationLevel>().GetQueryable()
                .AnyAsync(l => l.Id == id && l.TenantId == tenantId && !l.IsDeleted, cancellationToken),
            HrAudienceTargetType.Position => await unitOfWork.Repository<EmployeePosition>().GetQueryable()
                .AnyAsync(p => p.Id == id && p.TenantId == tenantId && !p.IsDeleted, cancellationToken),
            HrAudienceTargetType.Location => await unitOfWork.Repository<Location>().GetQueryable()
                .AnyAsync(l => l.Id == id && l.TenantId == tenantId && !l.IsDeleted, cancellationToken),
            HrAudienceTargetType.Employee => await unitOfWork.Repository<Employee>().GetQueryable()
                .AnyAsync(e => e.Id == id && e.TenantId == tenantId && !e.IsDeleted, cancellationToken),
            _ => false,
        };

        if (!exists)
            throw new InvalidOperationException(
                $"No {TypeLabel(type).ToLowerInvariant()} with that id exists in this organisation.");
    }

    /// <summary>The display name of each target id. Ids that resolve to nothing are simply absent.</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(
        IUnitOfWork unitOfWork, Guid tenantId, IEnumerable<(HrAudienceTargetType Type, Guid? Id)> targets,
        CancellationToken cancellationToken)
    {
        var byType = targets
            .Where(t => t.Id is { } g && g != Guid.Empty)
            .GroupBy(t => t.Type)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Id!.Value).Distinct().ToList());

        var names = new Dictionary<Guid, string>();

        if (byType.TryGetValue(HrAudienceTargetType.OrganizationUnit, out var unitIds))
            foreach (var u in await unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                         .Where(u => u.TenantId == tenantId && unitIds.Contains(u.Id))
                         .Select(u => new { u.Id, u.Name }).ToListAsync(cancellationToken))
                names[u.Id] = u.Name;

        if (byType.TryGetValue(HrAudienceTargetType.OrganizationLevel, out var levelIds))
            foreach (var l in await unitOfWork.Repository<OrganizationLevel>().GetQueryable()
                         .Where(l => l.TenantId == tenantId && levelIds.Contains(l.Id))
                         .Select(l => new { l.Id, l.Name }).ToListAsync(cancellationToken))
                names[l.Id] = l.Name;

        if (byType.TryGetValue(HrAudienceTargetType.Position, out var positionIds))
            foreach (var p in await unitOfWork.Repository<EmployeePosition>().GetQueryable()
                         .Where(p => p.TenantId == tenantId && positionIds.Contains(p.Id))
                         .Select(p => new { p.Id, p.Title }).ToListAsync(cancellationToken))
                names[p.Id] = p.Title;

        if (byType.TryGetValue(HrAudienceTargetType.Location, out var locationIds))
            foreach (var l in await unitOfWork.Repository<Location>().GetQueryable()
                         .Where(l => l.TenantId == tenantId && locationIds.Contains(l.Id))
                         .Select(l => new { l.Id, l.Name }).ToListAsync(cancellationToken))
                names[l.Id] = l.Name;

        if (byType.TryGetValue(HrAudienceTargetType.Employee, out var employeeIds))
            foreach (var e in await unitOfWork.Repository<Employee>().GetQueryable()
                         .Where(e => e.TenantId == tenantId && employeeIds.Contains(e.Id))
                         .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeNumber }).ToListAsync(cancellationToken))
                names[e.Id] = $"{e.FirstName} {e.LastName} ({e.EmployeeNumber})";

        return names;
    }

    /// <summary>"Organisation unit: Operations" — or "Everyone".</summary>
    public static string Describe(HrAudienceTargetType type, Guid? targetId, IReadOnlyDictionary<Guid, string> names)
    {
        if (!NeedsTarget(type)) return TypeLabel(type);
        var name = targetId is { } id && names.TryGetValue(id, out var n) ? n : "(no longer exists)";
        return type == HrAudienceTargetType.OrganizationUnit
            ? $"{TypeLabel(type)}: {name} and the units beneath it"
            : $"{TypeLabel(type)}: {name}";
    }
}
