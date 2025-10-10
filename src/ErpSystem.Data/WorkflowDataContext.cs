using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces.Data;

namespace ErpSystem.Data;

/// <summary>
/// Implementation of IWorkflowDataContext that delegates to ApplicationDbContext
/// This avoids circular dependencies while providing workflow services access to data
/// </summary>
public class WorkflowDataContext : IWorkflowDataContext
{
    private readonly ApplicationDbContext _context;

    public WorkflowDataContext(ApplicationDbContext context)
    {
        _context = context;
    }

    public DbSet<WorkflowDefinition> WorkflowDefinitions => _context.WorkflowDefinitions;
    public DbSet<WorkflowStep> WorkflowSteps => _context.WorkflowSteps;
    public DbSet<WorkflowTransition> WorkflowTransitions => _context.WorkflowTransitions;
    public DbSet<WorkflowInstance> WorkflowInstances => _context.WorkflowInstances;
    public DbSet<WorkflowStepInstance> WorkflowStepInstances => _context.WorkflowStepInstances;
    public DbSet<WorkflowActivityLog> WorkflowActivityLogs => _context.WorkflowActivityLogs;
    public DbSet<WorkflowApproval> WorkflowApprovals => _context.WorkflowApprovals;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public DbSet<TEntity> Set<TEntity>() where TEntity : class
    {
        return _context.Set<TEntity>();
    }
}