using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DevelopmentPlanFeedbackService> _logger;

    public DevelopmentPlanFeedbackService(
        IGenericRepository<EmployeeDevelopmentPlanFeedback> feedbackRepository,
        IGenericRepository<EmployeeDevelopmentPlan> planRepository,
        IUnitOfWork unitOfWork,
        ILogger<DevelopmentPlanFeedbackService> logger)
    {
        _feedbackRepository = feedbackRepository;
        _planRepository     = planRepository;
        _unitOfWork         = unitOfWork;
        _logger             = logger;
    }

    public async Task<IEnumerable<EmployeeDevelopmentPlanFeedbackDto>> GetByPlanIdAsync(
        Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _feedbackRepository
            .GetQueryable(f => f.DevelopmentPlanId == planId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<EmployeeDevelopmentPlanFeedbackDto> AddAsync(
        CreateEmployeeDevelopmentPlanFeedbackDto dto, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(dto.DevelopmentPlanId);
        if (plan is null)
            throw new ArgumentException($"Development plan '{dto.DevelopmentPlanId}' not found.");

        var entity = dto.ToEntity(plan.TenantId);

        await _feedbackRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Feedback added to plan {PlanId} by manager {ManagerId}: {FeedbackId}",
            dto.DevelopmentPlanId, dto.ManagerId, entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid feedbackId, CancellationToken cancellationToken = default)
    {
        var entity = await _feedbackRepository.GetByIdAsync(feedbackId);
        if (entity is null)
            throw new ArgumentException($"Feedback entry '{feedbackId}' not found.");

        await _feedbackRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Feedback entry deleted: {FeedbackId}", feedbackId);
        return true;
    }
}
