using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 3: APPROVAL WORKFLOW
// ============================================================================

#region Staff Travel Approval Workflow Template Repository

public class StaffTravelApprovalWorkflowTemplateRepository
    : GenericRepository<StaffTravelApprovalWorkflowTemplate>, IStaffTravelApprovalWorkflowTemplateRepository
{
    public StaffTravelApprovalWorkflowTemplateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelApprovalWorkflowTemplate?> GetWithStepsAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.Steps.OrderBy(s => s.StepOrder)).ThenInclude(s => s.SpecificApprover)
            .Include(t => t.Steps).ThenInclude(s => s.EscalationApprover)
            .Include(t => t.AppliesToLevelFrom)
            .Include(t => t.AppliesToLevelTo)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelApprovalWorkflowTemplate>> GetActiveTemplatesAsync()
    {
        return await _dbSet
            .Where(t => t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelApprovalWorkflowTemplate>> GetByTravelTypeAsync(StaffTravelType travelType)
    {
        return await _dbSet
            .Where(t => t.IsActive && !t.IsDeleted && (t.TravelType == null || t.TravelType == travelType))
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<StaffTravelApprovalWorkflowTemplate?> GetMatchingTemplateAsync(
        StaffTravelType travelType, bool isInternational, decimal estimatedBudget, TravelRiskLevel riskLevel)
    {
        var candidates = await _dbSet
            .Include(t => t.Steps.OrderBy(s => s.StepOrder))
            .Where(t => t.IsActive && !t.IsDeleted
                     && (t.TravelType == null || t.TravelType == travelType)
                     && (t.IsInternational == null || t.IsInternational == isInternational)
                     && (t.RiskLevel == null || t.RiskLevel == riskLevel)
                     && (t.MinBudgetThreshold == null || t.MinBudgetThreshold <= estimatedBudget)
                     && (t.MaxBudgetThreshold == null || t.MaxBudgetThreshold >= estimatedBudget))
            .ToListAsync();

        // Prefer the most specific template (the one constraining the most dimensions).
        return candidates
            .OrderByDescending(t => (t.TravelType != null ? 1 : 0)
                                  + (t.IsInternational != null ? 1 : 0)
                                  + (t.RiskLevel != null ? 1 : 0)
                                  + (t.MinBudgetThreshold != null ? 1 : 0)
                                  + (t.MaxBudgetThreshold != null ? 1 : 0))
            .FirstOrDefault();
    }
}

#endregion

#region Staff Travel Approval Workflow Step Repository

public class StaffTravelApprovalWorkflowStepRepository
    : GenericRepository<StaffTravelApprovalWorkflowStep>, IStaffTravelApprovalWorkflowStepRepository
{
    public StaffTravelApprovalWorkflowStepRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelApprovalWorkflowStep>> GetByTemplateIdAsync(Guid templateId)
    {
        return await _dbSet
            .Include(s => s.SpecificApprover)
            .Include(s => s.EscalationApprover)
            .Where(s => s.WorkflowTemplateId == templateId && !s.IsDeleted)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Approval Instance Repository

public class StaffTravelApprovalInstanceRepository
    : GenericRepository<StaffTravelApprovalInstance>, IStaffTravelApprovalInstanceRepository
{
    public StaffTravelApprovalInstanceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelApprovalInstance>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(i => i.WorkflowTemplate)
            .Include(i => i.Decisions)
            .Where(i => i.StaffTravelRequestId == requestId && !i.IsDeleted)
            .OrderByDescending(i => i.InitiatedAt)
            .ToListAsync();
    }

    public async Task<StaffTravelApprovalInstance?> GetWithDecisionsAsync(Guid id)
    {
        return await _dbSet
            .Include(i => i.WorkflowTemplate)
            .Include(i => i.Decisions.OrderBy(d => d.StepOrder)).ThenInclude(d => d.Approver)
            .Include(i => i.Decisions).ThenInclude(d => d.OriginalApprover)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelApprovalInstance>> GetByStatusAsync(TravelApprovalInstanceStatus status)
    {
        return await _dbSet
            .Include(i => i.WorkflowTemplate)
            .Where(i => i.Status == status && !i.IsDeleted)
            .OrderBy(i => i.InitiatedAt)
            .ToListAsync();
    }

    public async Task<StaffTravelApprovalInstance?> GetActiveInstanceForRequestAsync(Guid requestId)
    {
        return await _dbSet
            .Include(i => i.WorkflowTemplate)
            .Include(i => i.Decisions)
            .Where(i => i.StaffTravelRequestId == requestId && !i.IsDeleted
                     && (i.Status == TravelApprovalInstanceStatus.Pending
                      || i.Status == TravelApprovalInstanceStatus.InProgress
                      || i.Status == TravelApprovalInstanceStatus.Escalated))
            .OrderByDescending(i => i.InitiatedAt)
            .FirstOrDefaultAsync();
    }
}

#endregion

#region Staff Travel Approval Decision Repository

public class StaffTravelApprovalDecisionRepository
    : GenericRepository<StaffTravelApprovalDecision>, IStaffTravelApprovalDecisionRepository
{
    public StaffTravelApprovalDecisionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelApprovalDecision>> GetByInstanceIdAsync(Guid instanceId)
    {
        return await _dbSet
            .Include(d => d.Approver)
            .Include(d => d.OriginalApprover)
            .Where(d => d.ApprovalInstanceId == instanceId && !d.IsDeleted)
            .OrderBy(d => d.StepOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelApprovalDecision>> GetByApproverIdAsync(Guid approverId)
    {
        return await _dbSet
            .Include(d => d.ApprovalInstance)
            .Where(d => d.ApproverId == approverId && !d.IsDeleted)
            .OrderByDescending(d => d.DecidedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelApprovalDecision>> GetPendingForApproverAsync(Guid approverId)
    {
        return await _dbSet
            .Include(d => d.ApprovalInstance)
            .Where(d => d.ApproverId == approverId && d.DecidedAt == null && !d.IsDeleted)
            .OrderBy(d => d.SlaDeadline)
            .ToListAsync();
    }
}

#endregion
