using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Services.Workflow;

public static class WorkflowDelegationPolicy
{
    public static WorkflowDelegation? SelectEffective(IEnumerable<WorkflowDelegation> candidates,
        string? module, string? entityType, Guid? workflowDefinitionId, Guid? workflowStepId,
        decimal? amount, string? currencyCode)
    {
        return candidates
            .Where(item => string.IsNullOrWhiteSpace(item.Module) || Same(item.Module, module))
            .Where(item => string.IsNullOrWhiteSpace(item.EntityType) || Same(item.EntityType, entityType))
            .Where(item => !item.WorkflowDefinitionId.HasValue ||
                workflowDefinitionId.HasValue && item.WorkflowDefinitionId == workflowDefinitionId)
            .Where(item => !item.WorkflowStepId.HasValue ||
                workflowStepId.HasValue && item.WorkflowStepId == workflowStepId)
            .Where(item => !item.MaximumAmount.HasValue || amount.HasValue && amount <= item.MaximumAmount)
            .Where(item => !item.MaximumAmount.HasValue ||
                string.IsNullOrWhiteSpace(item.CurrencyCode) || Same(item.CurrencyCode, currencyCode))
            .OrderByDescending(item => item.WorkflowStepId.HasValue)
            .ThenByDescending(item => item.WorkflowDefinitionId.HasValue)
            .ThenByDescending(item => !string.IsNullOrWhiteSpace(item.EntityType))
            .ThenByDescending(item => !string.IsNullOrWhiteSpace(item.Module))
            .ThenByDescending(item => item.Kind)
            .ThenByDescending(item => item.EffectiveFrom)
            .FirstOrDefault();
    }

    public static DateTime CalculateDueDate(DateTime startedAtUtc, double workingHours, TimeZoneInfo zone,
        int workingDaysMask, TimeSpan workDayStart, TimeSpan workDayEnd, ISet<DateOnly> holidays)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc), zone);
        var remaining = TimeSpan.FromHours(workingHours);
        while (remaining > TimeSpan.Zero)
        {
            if (!IsWorkingDay(local.Date, workingDaysMask, holidays))
            {
                local = NextDayStart(local.Date, workDayStart);
                continue;
            }
            var dayStart = local.Date + workDayStart;
            var dayEnd = local.Date + workDayEnd;
            if (local < dayStart) local = dayStart;
            if (local >= dayEnd)
            {
                local = NextDayStart(local.Date, workDayStart);
                continue;
            }
            var available = dayEnd - local;
            var consumed = remaining < available ? remaining : available;
            local += consumed;
            remaining -= consumed;
            if (remaining > TimeSpan.Zero) local = NextDayStart(local.Date, workDayStart);
        }
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkingDay(DateTime date, int mask, ISet<DateOnly> holidays)
    {
        var weekdayIndex = ((int)date.DayOfWeek + 6) % 7;
        return (mask & (1 << weekdayIndex)) != 0 && !holidays.Contains(DateOnly.FromDateTime(date));
    }

    private static DateTime NextDayStart(DateTime date, TimeSpan start) => date.AddDays(1) + start;
}
