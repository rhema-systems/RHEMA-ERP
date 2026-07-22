using System.Text.Json;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed class WorkflowRuntimeGovernanceService : IWorkflowRuntimeGovernanceService
{
    private readonly ApplicationDbContext _db;

    public WorkflowRuntimeGovernanceService(ApplicationDbContext db) => _db = db;

    public async Task<WorkflowDelegationResolution?> ResolveDelegateAsync(Guid tenantId, Guid principalUserId,
        string? module, string? entityType, Guid? workflowDefinitionId, Guid? workflowStepId,
        decimal? amount, string? currencyCode, DateTime effectiveAt,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _db.WorkflowDelegations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
                item.PrincipalUserId == principalUserId && item.EffectiveFrom <= effectiveAt &&
                item.EffectiveTo >= effectiveAt)
            .ToListAsync(cancellationToken);

        var match = WorkflowDelegationPolicy.SelectEffective(candidates, module, entityType,
            workflowDefinitionId, workflowStepId, amount, currencyCode);

        return match == null ? null : new WorkflowDelegationResolution(
            match.Id, match.PrincipalUserId, match.DelegateUserId, match.Reason, match.AllowRedelegation);
    }

    public async Task ValidateOneOffDelegationAsync(Guid tenantId, Guid principalUserId, Guid delegateUserId,
        bool allowRedelegation, CancellationToken cancellationToken = default)
    {
        if (principalUserId == delegateUserId)
            throw new InvalidOperationException("An approval cannot be delegated to the same user.");

        var delegateExists = await _db.Users.AsNoTracking().AnyAsync(user =>
            user.Id == delegateUserId && user.TenantId == tenantId && user.IsActive, cancellationToken);
        if (!delegateExists)
            throw new InvalidOperationException("The delegate must be an active user in the current tenant.");

        var createsCycle = await _db.WorkflowDelegations.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
            item.PrincipalUserId == delegateUserId && item.DelegateUserId == principalUserId &&
            item.EffectiveFrom <= DateTime.UtcNow && item.EffectiveTo >= DateTime.UtcNow, cancellationToken);
        if (createsCycle)
            throw new InvalidOperationException("This delegation would create an active delegation cycle.");

        if (!allowRedelegation)
        {
            var principalIsDelegate = await _db.WorkflowDelegations.AsNoTracking().AnyAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
                item.DelegateUserId == principalUserId && !item.AllowRedelegation &&
                item.EffectiveFrom <= DateTime.UtcNow && item.EffectiveTo >= DateTime.UtcNow, cancellationToken);
            if (principalIsDelegate)
                throw new InvalidOperationException("The delegated authority does not permit re-delegation.");
        }
    }

    public async Task<DateTime?> CalculateDueDateAsync(Guid tenantId, DateTime startedAtUtc,
        double? workingHours, CancellationToken cancellationToken = default)
    {
        if (!workingHours.HasValue || workingHours <= 0) return null;

        var calendar = await _db.WorkflowWorkingCalendars.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
            .OrderByDescending(item => item.IsDefault).ThenBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (calendar == null) return startedAtUtc.AddHours(workingHours.Value);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        var holidays = ParseHolidays(calendar.HolidaysJson);
        return WorkflowDelegationPolicy.CalculateDueDate(startedAtUtc, workingHours.Value, zone,
            calendar.WorkingDaysMask, calendar.WorkDayStart, calendar.WorkDayEnd, holidays);
    }

    private static HashSet<DateOnly> ParseHolidays(string json)
    {
        try { return (JsonSerializer.Deserialize<List<DateOnly>>(json) ?? []).ToHashSet(); }
        catch (JsonException) { return []; }
    }

}
