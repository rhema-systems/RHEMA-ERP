using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Manages manager feedback entries on development plans.
/// </summary>
public class DevelopmentPlanFeedbackService : IDevelopmentPlanFeedbackService
{
    private readonly IGenericRepository<EmployeeDevelopmentPlanFeedback> _feedbackRepository;
    private readonly IGenericRepository<EmployeeDevelopmentPlan> _planRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAppraisalNotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DevelopmentPlanFeedbackService> _logger;

    public DevelopmentPlanFeedbackService(
        IGenericRepository<EmployeeDevelopmentPlanFeedback> feedbackRepository,
        IGenericRepository<EmployeeDevelopmentPlan> planRepository,
        ICurrentUserProvider currentUserProvider,
        IAppraisalNotificationService notifications,
        IUnitOfWork unitOfWork,
        ILogger<DevelopmentPlanFeedbackService> logger)
    {
        _feedbackRepository = feedbackRepository;
        _planRepository     = planRepository;
        _currentUserProvider = currentUserProvider;
        _notifications      = notifications;
        _unitOfWork         = unitOfWork;
        _logger             = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private async Task<EmployeeDevelopmentPlan> GetOwnedPlanAsync(Guid planId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan is null || plan.TenantId != GetTenantId())
            throw new ArgumentException($"Development plan '{planId}' not found.");
        return plan;
    }

    public async Task<IEnumerable<EmployeeDevelopmentPlanFeedbackDto>> GetByPlanIdAsync(
        Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = await _feedbackRepository
            .GetQueryable(f => f.DevelopmentPlanId == planId && f.TenantId == tenantId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<EmployeeDevelopmentPlanFeedbackDto> AddAsync(
        CreateEmployeeDevelopmentPlanFeedbackDto dto, CancellationToken cancellationToken = default)
    {
        var plan = await GetOwnedPlanAsync(dto.DevelopmentPlanId);

        var entity = dto.ToEntity(GetTenantId());

        await _feedbackRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Feedback added to plan {PlanId} by manager {ManagerId}: {FeedbackId}",
            dto.DevelopmentPlanId, dto.ManagerId, entity.Id);

        // Feedback nobody is told about is feedback nobody reads. The employee's own notes on
        // their plan are not news to them, so those raise nothing.
        if (plan.EmployeeId != dto.ManagerId)
        {
            try
            {
                await _notifications.RaiseAsync(new[]
                {
                    new AppraisalNotificationRequest(
                        plan.EmployeeId,
                        AppraisalNotificationType.DevelopmentFeedbackAdded,
                        "New development feedback",
                        string.IsNullOrWhiteSpace(plan.Title)
                            ? "Your manager has added feedback to your development plan."
                            : $"Your manager has added feedback to \"{plan.Title}\".",
                        NavigationUrl: $"/hr/performance/development-plans/{plan.Id}")
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify the employee of new development feedback; the feedback stands.");
            }
        }

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid feedbackId, CancellationToken cancellationToken = default)
    {
        var entity = await _feedbackRepository.GetByIdAsync(feedbackId);
        if (entity is null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Feedback entry '{feedbackId}' not found.");

        await _feedbackRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Feedback entry deleted: {FeedbackId}", feedbackId);
        return true;
    }
}
