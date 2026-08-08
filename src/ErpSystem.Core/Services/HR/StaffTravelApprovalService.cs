using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 3: APPROVAL WORKFLOW SERVICE
// ============================================================================

#region Staff Travel Approval Service

public class StaffTravelApprovalService : IStaffTravelApprovalService
{
    private readonly IStaffTravelApprovalWorkflowTemplateRepository _templateRepository;
    private readonly IStaffTravelApprovalWorkflowStepRepository _stepRepository;
    private readonly IStaffTravelApprovalInstanceRepository _instanceRepository;
    private readonly IStaffTravelApprovalDecisionRepository _decisionRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelApprovalService> _logger;

    public StaffTravelApprovalService(
        IStaffTravelApprovalWorkflowTemplateRepository templateRepository,
        IStaffTravelApprovalWorkflowStepRepository stepRepository,
        IStaffTravelApprovalInstanceRepository instanceRepository,
        IStaffTravelApprovalDecisionRepository decisionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelApprovalService> logger)
    {
        _templateRepository = templateRepository;
        _stepRepository = stepRepository;
        _instanceRepository = instanceRepository;
        _decisionRepository = decisionRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffTravelApprovalWorkflowTemplate> GetOwnedTemplateAsync(Guid id)
    {
        var entity = await _templateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Approval workflow template with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelApprovalWorkflowStep> GetOwnedStepAsync(Guid id)
    {
        var entity = await _stepRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Approval workflow step with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelApprovalInstance> GetOwnedInstanceAsync(Guid id)
    {
        var entity = await _instanceRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Approval instance with ID '{id}' not found.");
        return entity;
    }

    // ---- Templates ---------------------------------------------------------

    public async Task<StaffTravelApprovalWorkflowTemplateDto> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _templateRepository.GetWithStepsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Approval workflow template with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelApprovalWorkflowTemplateSummaryDto>> GetAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _templateRepository.GetAllAsync())
            .Where(t => t.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffTravelApprovalWorkflowTemplateSummaryDto>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _templateRepository.GetActiveTemplatesAsync())
            .Where(t => t.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<StaffTravelApprovalWorkflowTemplateDto> CreateTemplateAsync(CreateStaffTravelApprovalWorkflowTemplateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _templateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelApprovalWorkflowTemplateDto> UpdateTemplateAsync(UpdateStaffTravelApprovalWorkflowTemplateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTemplateAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _templateRepository.GetWithStepsAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Approval workflow template with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<bool> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTemplateAsync(id);
        await _templateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Steps -------------------------------------------------------------

    public async Task<StaffTravelApprovalWorkflowStepDto> AddStepAsync(CreateStaffTravelApprovalWorkflowStepDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedTemplateAsync(createDto.WorkflowTemplateId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _stepRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelApprovalWorkflowStepDto>> GetStepsAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedTemplateAsync(templateId);
        var tenantId = GetTenantId();
        return (await _stepRepository.GetByTemplateIdAsync(templateId))
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.ToDto())
            .ToList();
    }

    public async Task<StaffTravelApprovalWorkflowStepDto> UpdateStepAsync(UpdateStaffTravelApprovalWorkflowStepDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedStepAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _stepRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteStepAsync(Guid stepId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedStepAsync(stepId);
        await _stepRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Instances ---------------------------------------------------------

    public async Task<StaffTravelApprovalInstanceDto> InitiateAsync(CreateStaffTravelApprovalInstanceDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var template = await _templateRepository.GetWithStepsAsync(createDto.WorkflowTemplateId);
        if (template == null || template.TenantId != tenantId)
            throw new ArgumentException($"Approval workflow template with ID '{createDto.WorkflowTemplateId}' not found.");

        var existing = await _instanceRepository.GetActiveInstanceForRequestAsync(createDto.StaffTravelRequestId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("An active approval instance already exists for this request.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.CurrentStepOrder = template.Steps.Any() ? template.Steps.Min(s => s.StepOrder) : 1;
        entity.Status = TravelApprovalInstanceStatus.InProgress;

        await _instanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Approval instance initiated for request {RequestId} using template {TemplateId}",
            entity.StaffTravelRequestId, entity.WorkflowTemplateId);

        var created = await _instanceRepository.GetWithDecisionsAsync(entity.Id);
        if (created == null || created.TenantId != tenantId)
            throw new ArgumentException($"Approval instance with ID '{entity.Id}' not found.");
        return created.ToDto();
    }

    public async Task<StaffTravelApprovalInstanceDto> GetInstanceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _instanceRepository.GetWithDecisionsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Approval instance with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelApprovalInstanceSummaryDto>> GetInstancesByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _instanceRepository.GetByRequestIdAsync(requestId))
            .Where(i => i.TenantId == tenantId)
            .Select(i => i.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelApprovalInstanceSummaryDto>> GetInstancesByStatusAsync(TravelApprovalInstanceStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _instanceRepository.GetByStatusAsync(status))
            .Where(i => i.TenantId == tenantId)
            .Select(i => i.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelApprovalInstanceDto?> GetActiveInstanceForRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _instanceRepository.GetActiveInstanceForRequestAsync(requestId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    // ---- Decisions ---------------------------------------------------------

    public async Task<StaffTravelApprovalDecisionDto> RecordDecisionAsync(RecordStaffTravelApprovalDecisionDto decisionDto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedInstanceAsync(decisionDto.ApprovalInstanceId);

        var instance = await _instanceRepository.GetWithDecisionsAsync(decisionDto.ApprovalInstanceId);
        if (instance == null || instance.TenantId != tenantId)
            throw new ArgumentException($"Approval instance with ID '{decisionDto.ApprovalInstanceId}' not found.");

        if (instance.Status is TravelApprovalInstanceStatus.Approved
            or TravelApprovalInstanceStatus.Rejected
            or TravelApprovalInstanceStatus.Withdrawn
            or TravelApprovalInstanceStatus.Expired)
            throw new InvalidOperationException($"Decisions cannot be recorded on an instance in status '{instance.Status}'.");

        var decision = decisionDto.ToEntity(tenantId, decisionDto.ApproverId);
        await _decisionRepository.AddAsync(decision);

        // Advance the instance based on the decision outcome.
        switch (decisionDto.Decision)
        {
            case TravelApprovalDecision.Rejected:
                instance.Status = TravelApprovalInstanceStatus.Rejected;
                instance.CompletedAt = DateTime.UtcNow;
                break;

            case TravelApprovalDecision.ReturnedForRevision:
                instance.Status = TravelApprovalInstanceStatus.Withdrawn;
                instance.CompletedAt = DateTime.UtcNow;
                break;

            case TravelApprovalDecision.Escalated:
                instance.Status = TravelApprovalInstanceStatus.Escalated;
                break;

            case TravelApprovalDecision.Approved:
            case TravelApprovalDecision.Delegated:
            case TravelApprovalDecision.Abstained:
            default:
                var maxStepOrder = await GetMaxStepOrderAsync(instance.WorkflowTemplateId);
                if (decisionDto.StepOrder >= maxStepOrder)
                {
                    instance.Status = TravelApprovalInstanceStatus.Approved;
                    instance.CompletedAt = DateTime.UtcNow;
                }
                else
                {
                    instance.Status = TravelApprovalInstanceStatus.InProgress;
                    instance.CurrentStepOrder = decisionDto.StepOrder + 1;
                }
                break;
        }

        await _instanceRepository.UpdateAsync(instance);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Decision {Decision} recorded for approval instance {InstanceId} at step {Step}",
            decisionDto.Decision, decisionDto.ApprovalInstanceId, decisionDto.StepOrder);

        return decision.ToDto();
    }

    public async Task<IEnumerable<StaffTravelApprovalDecisionDto>> GetDecisionsForInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        await GetOwnedInstanceAsync(instanceId);
        var tenantId = GetTenantId();
        return (await _decisionRepository.GetByInstanceIdAsync(instanceId))
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelApprovalDecisionDto>> GetPendingDecisionsForApproverAsync(Guid approverId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _decisionRepository.GetPendingForApproverAsync(approverId))
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.ToDto())
            .ToList();
    }

    // ---- Helpers -----------------------------------------------------------

    private async Task<int> GetMaxStepOrderAsync(Guid templateId)
    {
        await GetOwnedTemplateAsync(templateId);
        var tenantId = GetTenantId();
        var steps = (await _stepRepository.GetByTemplateIdAsync(templateId))
            .Where(s => s.TenantId == tenantId);
        return steps.Any() ? steps.Max(s => s.StepOrder) : 1;
    }
}

#endregion
