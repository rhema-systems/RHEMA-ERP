using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

public class WorkflowActivityLogRepository : IWorkflowActivityLogRepository
{
    private readonly ApplicationDbContext _context;

    public WorkflowActivityLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowActivityLog?> GetByIdAsync(Guid id) =>
        await _context.WorkflowActivityLogs.FirstOrDefaultAsync(al => al.Id == id);

    public async Task<IEnumerable<WorkflowActivityLog>> GetAllAsync() =>
        await _context.WorkflowActivityLogs.OrderByDescending(al => al.Timestamp).ToListAsync();

    public async Task<WorkflowActivityLog> CreateAsync(WorkflowActivityLog activityLog)
    {
        activityLog.Id = Guid.NewGuid();
        activityLog.Timestamp = DateTime.UtcNow;
        await _context.WorkflowActivityLogs.AddAsync(activityLog);
        return activityLog;
    }

    public async Task<WorkflowActivityLog> UpdateAsync(WorkflowActivityLog activityLog)
    {
        _context.WorkflowActivityLogs.Update(activityLog);
        return activityLog;
    }

    public async Task DeleteAsync(Guid id)
    {
        var activityLog = await GetByIdAsync(id);
        if (activityLog != null) _context.WorkflowActivityLogs.Remove(activityLog);
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetByWorkflowInstanceAsync(Guid workflowInstanceId) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(al => al.Timestamp)
            .ToListAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> GetByWorkflowInstanceAsync(Guid workflowInstanceId, DateTime? fromDate = null)
    {
        var query = _context.WorkflowActivityLogs.Where(al => al.WorkflowInstanceId == workflowInstanceId);
        if (fromDate.HasValue) query = query.Where(al => al.Timestamp >= fromDate.Value);
        return await query.OrderBy(al => al.Timestamp).ToListAsync();
    }

    public async Task<WorkflowActivityLog?> GetLatestActivityAsync(Guid workflowInstanceId) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.WorkflowInstanceId == workflowInstanceId)
            .OrderByDescending(al => al.Timestamp)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> GetByStepInstanceAsync(Guid stepInstanceId) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.WorkflowStepInstanceId == stepInstanceId)
            .OrderBy(al => al.Timestamp)
            .ToListAsync();

    public async Task<WorkflowActivityLog?> GetLatestStepActivityAsync(Guid stepInstanceId) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.WorkflowStepInstanceId == stepInstanceId)
            .OrderByDescending(al => al.Timestamp)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypeAsync(WorkflowActivityType activityType) =>
        await GetByActivityTypeAsync(activityType.ToString());

    public async Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypeAsync(string activityType) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.ActivityType == activityType)
            .OrderByDescending(al => al.Timestamp)
            .ToListAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypesAsync(IEnumerable<WorkflowActivityType> activityTypes)
    {
        var typeStrings = activityTypes.Select(t => t.ToString()).ToList();
        return await _context.WorkflowActivityLogs
            .Where(al => typeStrings.Contains(al.ActivityType))
            .OrderByDescending(al => al.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetByUserAsync(Guid userId) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.UserId == userId)
            .OrderByDescending(al => al.Timestamp)
            .ToListAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> GetByUserAsync(Guid userId, DateTime? fromDate = null)
    {
        var query = _context.WorkflowActivityLogs.Where(al => al.UserId == userId);
        if (fromDate.HasValue) query = query.Where(al => al.Timestamp >= fromDate.Value);
        return await query.OrderByDescending(al => al.Timestamp).ToListAsync();
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetUserActivityInWorkflowAsync(Guid userId, Guid workflowInstanceId) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.UserId == userId && al.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(al => al.Timestamp)
            .ToListAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> GetBetweenDatesAsync(DateTime startDate, DateTime endDate) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.Timestamp >= startDate && al.Timestamp <= endDate)
            .OrderBy(al => al.Timestamp)
            .ToListAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> GetRecentActivityAsync(int hours = 24)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-hours);
        return await _context.WorkflowActivityLogs
            .Where(al => al.Timestamp >= cutoffTime)
            .OrderByDescending(al => al.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetTodayActivityAsync()
    {
        var startOfDay = DateTime.UtcNow.Date;
        return await GetBetweenDatesAsync(startOfDay, startOfDay.AddDays(1));
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetWorkflowTimelineAsync(Guid workflowInstanceId) =>
        await GetByWorkflowInstanceAsync(workflowInstanceId);

    public async Task<IEnumerable<WorkflowActivityLog>> GetStepTimelineAsync(Guid stepInstanceId) =>
        await GetByStepInstanceAsync(stepInstanceId);

    public async Task<IEnumerable<WorkflowActivityLog>> GetUserActivityTimelineAsync(Guid userId, DateTime? fromDate = null) =>
        await GetByUserAsync(userId, fromDate);

    // Simplified implementations for remaining interface methods
    public async Task<IEnumerable<WorkflowActivityLog>> GetErrorLogsAsync(DateTime? fromDate = null)
    {
        var query = _context.WorkflowActivityLogs.Where(al => al.ActivityType.Contains("Error") || al.ActivityType.Contains("Failed"));
        if (fromDate.HasValue) query = query.Where(al => al.Timestamp >= fromDate.Value);
        return await query.OrderByDescending(al => al.Timestamp).ToListAsync();
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetFailureLogsAsync(Guid? workflowInstanceId = null)
    {
        var query = _context.WorkflowActivityLogs.Where(al => al.ActivityType.Contains("Failed"));
        if (workflowInstanceId.HasValue) query = query.Where(al => al.WorkflowInstanceId == workflowInstanceId.Value);
        return await query.OrderByDescending(al => al.Timestamp).ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetActivityTypeCountsAsync(DateTime? fromDate = null)
    {
        var query = _context.WorkflowActivityLogs.AsQueryable();
        if (fromDate.HasValue) query = query.Where(al => al.Timestamp >= fromDate.Value);
        return await query.GroupBy(al => al.ActivityType).ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<Guid, int>> GetUserActivityCountsAsync(DateTime? fromDate = null)
    {
        var query = _context.WorkflowActivityLogs.Where(al => al.UserId.HasValue);
        if (fromDate.HasValue) query = query.Where(al => al.Timestamp >= fromDate.Value);
        return await query.GroupBy(al => al.UserId!.Value).ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<DateTime, int>> GetDailyActivityCountsAsync(DateTime fromDate, DateTime toDate) =>
        await _context.WorkflowActivityLogs
            .Where(al => al.Timestamp >= fromDate && al.Timestamp <= toDate)
            .GroupBy(al => al.Timestamp.Date)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<int> GetActivityCountAsync(Guid workflowInstanceId) =>
        await _context.WorkflowActivityLogs.CountAsync(al => al.WorkflowInstanceId == workflowInstanceId);

    public async Task<IEnumerable<WorkflowActivityLog>> SearchByDescriptionAsync(string searchTerm) =>
        await _context.WorkflowActivityLogs.Where(al => al.Description.Contains(searchTerm)).ToListAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> SearchByDataAsync(string searchTerm) =>
        await _context.WorkflowActivityLogs.Where(al => al.ActivityData != null && al.ActivityData.Contains(searchTerm)).ToListAsync();

    public async Task<IEnumerable<WorkflowActivityLog>> GetPagedAsync(int page, int pageSize, Guid? workflowInstanceId = null, string? activityType = null)
    {
        var query = _context.WorkflowActivityLogs.AsQueryable();
        if (workflowInstanceId.HasValue) query = query.Where(al => al.WorkflowInstanceId == workflowInstanceId.Value);
        if (!string.IsNullOrEmpty(activityType)) query = query.Where(al => al.ActivityType == activityType);
        return await query.OrderByDescending(al => al.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public async Task<int> DeleteOldLogsAsync(DateTime beforeDate)
    {
        var oldLogs = await _context.WorkflowActivityLogs.Where(al => al.Timestamp < beforeDate).ToListAsync();
        _context.WorkflowActivityLogs.RemoveRange(oldLogs);
        return oldLogs.Count;
    }

    public async Task<int> GetLogCountAsync(DateTime? fromDate = null)
    {
        var query = _context.WorkflowActivityLogs.AsQueryable();
        if (fromDate.HasValue) query = query.Where(al => al.Timestamp >= fromDate.Value);
        return await query.CountAsync();
    }

    public async Task<DateTime?> GetOldestLogDateAsync() =>
        await _context.WorkflowActivityLogs.OrderBy(al => al.Timestamp).Select(al => al.Timestamp).FirstOrDefaultAsync();

    public async Task<DateTime?> GetNewestLogDateAsync() =>
        await _context.WorkflowActivityLogs.OrderByDescending(al => al.Timestamp).Select(al => al.Timestamp).FirstOrDefaultAsync();

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}