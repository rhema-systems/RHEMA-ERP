using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow step instances using Entity Framework
/// </summary>
public class WorkflowStepInstanceRepository : IWorkflowStepInstanceRepository
{
    private readonly ApplicationDbContext _context;

    public WorkflowStepInstanceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowStepInstance?> GetByIdAsync(Guid id)
    {
        return await _context.WorkflowStepInstances.FirstOrDefaultAsync(si => si.Id == id);
    }

    public async Task<WorkflowStepInstance?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .ThenInclude(wi => wi.WorkflowDefinition)
            .FirstOrDefaultAsync(si => si.Id == id);
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetAllAsync()
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .OrderByDescending(si => si.CreatedDate)
            .ToListAsync();
    }

    public async Task<WorkflowStepInstance> CreateAsync(WorkflowStepInstance stepInstance)
    {
        stepInstance.Id = Guid.NewGuid();
        stepInstance.CreatedDate = DateTime.UtcNow;
        await _context.WorkflowStepInstances.AddAsync(stepInstance);
        return stepInstance;
    }

    public async Task<WorkflowStepInstance> UpdateAsync(WorkflowStepInstance stepInstance)
    {
        _context.WorkflowStepInstances.Update(stepInstance);
        return stepInstance;
    }

    public async Task DeleteAsync(Guid id)
    {
        var stepInstance = await GetByIdAsync(id);
        if (stepInstance != null)
        {
            _context.WorkflowStepInstances.Remove(stepInstance);
        }
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetByWorkflowInstanceAsync(Guid workflowInstanceId)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Where(si => si.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(si => si.CreatedDate)
            .ToListAsync();
    }

    public async Task<WorkflowStepInstance?> GetCurrentStepAsync(Guid workflowInstanceId)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Where(si => si.WorkflowInstanceId == workflowInstanceId && 
                        (si.Status == "InProgress" || si.Status == "Pending"))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetActiveStepsAsync(Guid workflowInstanceId)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Where(si => si.WorkflowInstanceId == workflowInstanceId && 
                        (si.Status == "InProgress" || si.Status == "Pending"))
            .OrderBy(si => si.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetCompletedStepsAsync(Guid workflowInstanceId)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Where(si => si.WorkflowInstanceId == workflowInstanceId && si.Status == "Completed")
            .OrderBy(si => si.CompletedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetByStatusAsync(WorkflowStepInstanceStatus status)
    {
        return await GetByStatusAsync(status.ToString());
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetByStatusAsync(string status)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.Status == status)
            .OrderByDescending(si => si.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetPendingStepsAsync()
    {
        return await GetByStatusAsync("Pending");
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetInProgressStepsAsync()
    {
        return await GetByStatusAsync("InProgress");
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepsAsync()
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.DueDate < DateTime.UtcNow && 
                        (si.Status == "Pending" || si.Status == "InProgress"))
            .OrderBy(si => si.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetAssignedToUserAsync(Guid userId)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.AssignedToId == userId)
            .OrderByDescending(si => si.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetAssignedToRoleAsync(string role)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.WorkflowStep.RequiredRole == role)
            .OrderByDescending(si => si.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetUnassignedStepsAsync()
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.AssignedToId == null && 
                        (si.Status == "Pending" || si.Status == "InProgress"))
            .OrderByDescending(si => si.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetPendingForUserAsync(Guid userId)
    {
        return await _context.WorkflowStepInstances
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.AssignedToId == userId && 
                        (si.Status == "Pending" || si.Status == "InProgress"))
            .OrderBy(si => si.DueDate ?? DateTime.MaxValue)
            .ToListAsync();
    }

    // Implementing remaining interface methods with simplified implementations
    public async Task<IEnumerable<WorkflowStepInstance>> GetByStepDefinitionAsync(Guid stepId) => 
        await _context.WorkflowStepInstances.Where(si => si.WorkflowStepId == stepId).ToListAsync();

    public async Task<WorkflowStepInstance?> GetLatestInstanceForStepAsync(Guid stepId, Guid workflowInstanceId) =>
        await _context.WorkflowStepInstances
            .Where(si => si.WorkflowStepId == stepId && si.WorkflowInstanceId == workflowInstanceId)
            .OrderByDescending(si => si.CreatedDate)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<WorkflowStepInstance>> GetCreatedBetweenAsync(DateTime startDate, DateTime endDate) =>
        await _context.WorkflowStepInstances.Where(si => si.CreatedDate >= startDate && si.CreatedDate <= endDate).ToListAsync();

    public async Task<IEnumerable<WorkflowStepInstance>> GetCompletedBetweenAsync(DateTime startDate, DateTime endDate) =>
        await _context.WorkflowStepInstances.Where(si => si.CompletedDate >= startDate && si.CompletedDate <= endDate).ToListAsync();

    public async Task<IEnumerable<WorkflowStepInstance>> GetDueBetweenAsync(DateTime startDate, DateTime endDate) =>
        await _context.WorkflowStepInstances.Where(si => si.DueDate >= startDate && si.DueDate <= endDate).ToListAsync();

    public async Task<Dictionary<string, int>> GetStatusCountsAsync() =>
        await _context.WorkflowStepInstances.GroupBy(si => si.Status).ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<Dictionary<string, int>> GetStatusCountsForWorkflowAsync(Guid workflowInstanceId) =>
        await _context.WorkflowStepInstances.Where(si => si.WorkflowInstanceId == workflowInstanceId)
            .GroupBy(si => si.Status).ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<Dictionary<Guid, int>> GetAssignmentCountsAsync() =>
        await _context.WorkflowStepInstances.Where(si => si.AssignedToId.HasValue)
            .GroupBy(si => si.AssignedToId!.Value).ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<TimeSpan?> GetAverageCompletionTimeAsync(Guid stepId)
    {
        var completedSteps = await _context.WorkflowStepInstances
            .Where(si => si.WorkflowStepId == stepId && si.CompletedDate.HasValue && si.StartedDate.HasValue)
            .Select(si => new { si.StartedDate, si.CompletedDate })
            .ToListAsync();

        if (!completedSteps.Any()) return null;

        var totalTicks = completedSteps.Sum(s => (s.CompletedDate!.Value - s.StartedDate!.Value).Ticks);
        return new TimeSpan(totalTicks / completedSteps.Count);
    }

    public async Task<IEnumerable<WorkflowStepInstance>> SearchByCommentsAsync(string searchTerm) =>
        await _context.WorkflowStepInstances.Where(si => si.Comments != null && si.Comments.Contains(searchTerm)).ToListAsync();

    public async Task<IEnumerable<WorkflowStepInstance>> GetPagedAsync(int page, int pageSize, Guid? userId = null, string? status = null)
    {
        var query = _context.WorkflowStepInstances.AsQueryable();
        if (userId.HasValue) query = query.Where(si => si.AssignedToId == userId.Value);
        if (!string.IsNullOrEmpty(status)) query = query.Where(si => si.Status == status);
        return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}