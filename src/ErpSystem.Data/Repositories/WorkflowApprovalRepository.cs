using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

public class WorkflowApprovalRepository : IWorkflowApprovalRepository
{
    private readonly ApplicationDbContext _context;

    public WorkflowApprovalRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowApproval?> GetByIdAsync(Guid id) =>
        await _context.WorkflowApprovals.FirstOrDefaultAsync(a => a.Id == id);

    public async Task<WorkflowApproval?> GetByIdWithDetailsAsync(Guid id) =>
        await _context.WorkflowApprovals
            .Include(a => a.WorkflowStepInstance)
            .ThenInclude(si => si.WorkflowStep)
            .Include(a => a.WorkflowStepInstance)
            .ThenInclude(si => si.WorkflowInstance)
            .ThenInclude(wi => wi.WorkflowDefinition)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<IEnumerable<WorkflowApproval>> GetAllAsync() =>
        await _context.WorkflowApprovals.OrderByDescending(a => a.CreatedDate).ToListAsync();

    public async Task<WorkflowApproval> CreateAsync(WorkflowApproval approval)
    {
        approval.Id = Guid.NewGuid();
        approval.CreatedDate = DateTime.UtcNow;
        await _context.WorkflowApprovals.AddAsync(approval);
        return approval;
    }

    public async Task<WorkflowApproval> UpdateAsync(WorkflowApproval approval)
    {
        _context.WorkflowApprovals.Update(approval);
        return approval;
    }

    public async Task DeleteAsync(Guid id)
    {
        var approval = await GetByIdAsync(id);
        if (approval != null) _context.WorkflowApprovals.Remove(approval);
    }

    public async Task<IEnumerable<WorkflowApproval>> GetByStepInstanceAsync(Guid stepInstanceId) =>
        await _context.WorkflowApprovals.Where(a => a.WorkflowStepInstanceId == stepInstanceId).ToListAsync();

    public async Task<WorkflowApproval?> GetActiveApprovalForStepAsync(Guid stepInstanceId) =>
        await _context.WorkflowApprovals
            .FirstOrDefaultAsync(a => a.WorkflowStepInstanceId == stepInstanceId && a.Status == "Pending");

    public async Task<IEnumerable<WorkflowApproval>> GetByStatusAsync(WorkflowApprovalStatus status) =>
        await GetByStatusAsync(status.ToString());

    public async Task<IEnumerable<WorkflowApproval>> GetByStatusAsync(string status) =>
        await _context.WorkflowApprovals
            .Include(a => a.WorkflowStepInstance).ThenInclude(si => si.WorkflowInstance)
            .Where(a => a.Status == status).OrderByDescending(a => a.CreatedDate).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetPendingApprovalsAsync() =>
        await GetByStatusAsync("Pending");

    public async Task<IEnumerable<WorkflowApproval>> GetApprovedAsync(DateTime? fromDate = null)
    {
        var query = _context.WorkflowApprovals.Where(a => a.Status == "Approved");
        if (fromDate.HasValue) query = query.Where(a => a.ResponseDate >= fromDate.Value);
        return await query.OrderByDescending(a => a.ResponseDate).ToListAsync();
    }

    public async Task<IEnumerable<WorkflowApproval>> GetRejectedAsync(DateTime? fromDate = null)
    {
        var query = _context.WorkflowApprovals.Where(a => a.Status == "Rejected");
        if (fromDate.HasValue) query = query.Where(a => a.ResponseDate >= fromDate.Value);
        return await query.OrderByDescending(a => a.ResponseDate).ToListAsync();
    }

    public async Task<IEnumerable<WorkflowApproval>> GetByApproverAsync(Guid approverId) =>
        await _context.WorkflowApprovals
            .Include(a => a.WorkflowStepInstance).ThenInclude(si => si.WorkflowInstance)
            .Where(a => a.ApproverId == approverId).OrderByDescending(a => a.CreatedDate).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetByApproverRoleAsync(string role) =>
        await _context.WorkflowApprovals
            .Include(a => a.WorkflowStepInstance).ThenInclude(si => si.WorkflowInstance)
            .Where(a => a.ApproverRole == role).OrderByDescending(a => a.CreatedDate).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetPendingForApproverAsync(Guid approverId) =>
        await _context.WorkflowApprovals
            .Include(a => a.WorkflowStepInstance).ThenInclude(si => si.WorkflowInstance)
            .Where(a => a.ApproverId == approverId && a.Status == "Pending")
            .OrderBy(a => a.Priority).ThenBy(a => a.CreatedDate).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetPendingForRoleAsync(string role) =>
        await _context.WorkflowApprovals
            .Include(a => a.WorkflowStepInstance).ThenInclude(si => si.WorkflowInstance)
            .Where(a => a.ApproverRole == role && a.Status == "Pending")
            .OrderBy(a => a.Priority).ThenBy(a => a.CreatedDate).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetPendingForUserOrRoleAsync(Guid? userId, string? role)
    {
        var query = _context.WorkflowApprovals
            .Include(a => a.WorkflowStepInstance).ThenInclude(si => si.WorkflowInstance)
            .Where(a => a.Status == "Pending");

        if (userId.HasValue && !string.IsNullOrEmpty(role))
            query = query.Where(a => a.ApproverId == userId.Value || a.ApproverRole == role);
        else if (userId.HasValue)
            query = query.Where(a => a.ApproverId == userId.Value);
        else if (!string.IsNullOrEmpty(role))
            query = query.Where(a => a.ApproverRole == role);

        return await query.OrderBy(a => a.Priority).ThenBy(a => a.CreatedDate).ToListAsync();
    }

    // Simplified implementations for remaining interface methods
    public async Task<IEnumerable<WorkflowApproval>> GetByPriorityAsync(int priority) =>
        await _context.WorkflowApprovals.Where(a => a.Priority == priority).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetHighPriorityPendingAsync(int maxPriority = 2) =>
        await _context.WorkflowApprovals.Where(a => a.Status == "Pending" && a.Priority <= maxPriority).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetOverduePendingAsync() =>
        await _context.WorkflowApprovals.Where(a => a.Status == "Pending" && a.DueDate < DateTime.UtcNow).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetCreatedBetweenAsync(DateTime startDate, DateTime endDate) =>
        await _context.WorkflowApprovals.Where(a => a.CreatedDate >= startDate && a.CreatedDate <= endDate).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetRespondedBetweenAsync(DateTime startDate, DateTime endDate) =>
        await _context.WorkflowApprovals.Where(a => a.ResponseDate >= startDate && a.ResponseDate <= endDate).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetDueBetweenAsync(DateTime startDate, DateTime endDate) =>
        await _context.WorkflowApprovals.Where(a => a.DueDate >= startDate && a.DueDate <= endDate).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetByWorkflowInstanceAsync(Guid workflowInstanceId) =>
        await _context.WorkflowApprovals
            .Include(a => a.WorkflowStepInstance)
            .Where(a => a.WorkflowStepInstance.WorkflowInstanceId == workflowInstanceId).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetApprovalHistoryAsync(Guid stepInstanceId) =>
        await _context.WorkflowApprovals
            .Where(a => a.WorkflowStepInstanceId == stepInstanceId)
            .OrderBy(a => a.CreatedDate).ToListAsync();

    public async Task<Dictionary<string, int>> GetStatusCountsAsync() =>
        await _context.WorkflowApprovals.GroupBy(a => a.Status).ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<Dictionary<Guid, int>> GetApproverCountsAsync() =>
        await _context.WorkflowApprovals.GroupBy(a => a.ApproverId).ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<Dictionary<string, int>> GetRoleCountsAsync() =>
        await _context.WorkflowApprovals.Where(a => a.ApproverRole != null)
            .GroupBy(a => a.ApproverRole!).ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<TimeSpan?> GetAverageResponseTimeAsync()
    {
        var respondedApprovals = await _context.WorkflowApprovals
            .Where(a => a.ResponseDate.HasValue)
            .Select(a => new { a.CreatedDate, a.ResponseDate })
            .ToListAsync();

        if (!respondedApprovals.Any()) return null;

        var totalTicks = respondedApprovals.Sum(a => (a.ResponseDate!.Value - a.CreatedDate).Ticks);
        return new TimeSpan(totalTicks / respondedApprovals.Count);
    }

    public async Task<Dictionary<int, int>> GetPriorityDistributionAsync() =>
        await _context.WorkflowApprovals.GroupBy(a => a.Priority).ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<IEnumerable<WorkflowApproval>> SearchByCommentsAsync(string searchTerm) =>
        await _context.WorkflowApprovals.Where(a => a.Comments != null && a.Comments.Contains(searchTerm)).ToListAsync();

    public async Task<IEnumerable<WorkflowApproval>> GetPagedAsync(int page, int pageSize, Guid? approverId = null, string? status = null)
    {
        var query = _context.WorkflowApprovals.AsQueryable();
        if (approverId.HasValue) query = query.Where(a => a.ApproverId == approverId.Value);
        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.Status == status);
        return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public async Task<IEnumerable<WorkflowApproval>> GetExpiringSoonAsync(int daysAhead = 3)
    {
        var expiryDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _context.WorkflowApprovals
            .Where(a => a.Status == "Pending" && a.DueDate <= expiryDate)
            .OrderBy(a => a.DueDate).ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}