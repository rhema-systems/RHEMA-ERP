using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Interfaces.Data;

/// <summary>
/// Interface for workflow data access to avoid circular dependencies with ErpSystem.Data
/// </summary>
public interface IWorkflowDataContext
{
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStep> WorkflowSteps { get; }
    DbSet<WorkflowTransition> WorkflowTransitions { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<WorkflowStepInstance> WorkflowStepInstances { get; }
    DbSet<WorkflowActivityLog> WorkflowActivityLogs { get; }
    DbSet<WorkflowApproval> WorkflowApprovals { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
}