using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingPlanService : ITrainingPlanService
{
    private readonly ITrainingPlanRepository _planRepository;
    private readonly ITrainingPlanItemRepository _planItemRepository;
    private readonly ITrainingPlanBudgetLineRepository _budgetLineRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingPlanService> _logger;

    private readonly INumberSequenceService _numberSequence;

    public TrainingPlanService(
        ITrainingPlanRepository planRepository,
        ITrainingPlanItemRepository planItemRepository,
        ITrainingPlanBudgetLineRepository budgetLineRepository,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingPlanService> logger)
    {
        _planRepository = planRepository;
        _planItemRepository = planItemRepository;
        _budgetLineRepository = budgetLineRepository;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
        _logger = logger;
    }

    // ── Plan queries ──────────────────────────────────────────────────────────

    public async Task<TrainingPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training plan with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingPlanDto?> GetByPlanNumberAsync(string planNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByPlanNumberAsync(planNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TrainingPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingPlanSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByYearAsync(year);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingPlanSummaryDto>> GetByDepartmentIdAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByOrganizationUnitAsync(departmentId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingPlanSummaryDto>> GetByStatusAsync(TrainingPlanStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    // ── Plan CRUD ─────────────────────────────────────────────────────────────

    public async Task<TrainingPlanDto> CreateAsync(CreateTrainingPlanDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);
        entity.PlanNumber = await GeneratePlanNumberAsync(cancellationToken);
        entity.Status = TrainingPlanStatus.Draft;

        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training plan created: {PlanNumber}", entity.PlanNumber);

        return entity.ToDto();
    }

    public async Task<TrainingPlanDto> UpdateAsync(UpdateTrainingPlanDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Training plan with ID '{dto.Id}' not found.");

        if (entity.Status == TrainingPlanStatus.Approved || entity.Status == TrainingPlanStatus.Completed)
            throw new InvalidOperationException("Cannot update an approved or completed training plan.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training plan with ID '{id}' not found.");

        if (entity.Status != TrainingPlanStatus.Draft)
            throw new InvalidOperationException("Only draft training plans can be deleted.");

        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training plan {PlanId} deleted", id);

        return true;
    }

    // ── Plan workflow ─────────────────────────────────────────────────────────

    public async Task<bool> SubmitForApprovalAsync(Guid planId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(planId);

        if (entity == null)
            throw new ArgumentException($"Training plan with ID '{planId}' not found.");

        if (entity.Status != TrainingPlanStatus.Draft && entity.Status != TrainingPlanStatus.Revised)
            throw new InvalidOperationException($"Training plan cannot be submitted for approval from status '{entity.Status}'.");

        entity.Status = TrainingPlanStatus.PendingApproval;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training plan {PlanNumber} submitted for approval", entity.PlanNumber);

        return true;
    }

    public async Task<bool> ApproveAsync(ApproveTrainingPlanDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(dto.PlanId);

        if (entity == null)
            throw new ArgumentException($"Training plan with ID '{dto.PlanId}' not found.");

        if (entity.Status != TrainingPlanStatus.PendingApproval)
            throw new InvalidOperationException($"Training plan cannot be approved from status '{entity.Status}'.");

        entity.Status = TrainingPlanStatus.Approved;
        entity.ApprovedById = dto.ApprovedById;
        entity.ApprovalDate = dto.ApprovalDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = dto.ApprovedById.ToString();

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training plan {PlanNumber} approved by {ApprovedById}", entity.PlanNumber, dto.ApprovedById);

        return true;
    }

    // ── Plan item sub-operations ──────────────────────────────────────────────

    public async Task<TrainingPlanItemDto> AddItemAsync(CreateTrainingPlanItemDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(dto.PlanId);

        if (plan == null)
            throw new ArgumentException($"Training plan with ID '{dto.PlanId}' not found.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _planItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingPlanItemDto>> GetItemsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _planItemRepository.GetByPlanIdAsync(planId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainingPlanItemDto> UpdateItemAsync(UpdateTrainingPlanItemDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planItemRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Training plan item with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _planItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await _planItemRepository.GetByIdAsync(itemId);

        if (entity == null)
            throw new ArgumentException($"Training plan item with ID '{itemId}' not found.");

        await _planItemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Budget line sub-operations ────────────────────────────────────────────

    public async Task<TrainingPlanBudgetLineDto> AddBudgetLineAsync(CreateTrainingPlanBudgetLineDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(dto.PlanId);

        if (plan == null)
            throw new ArgumentException($"Training plan with ID '{dto.PlanId}' not found.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _budgetLineRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingPlanBudgetLineDto>> GetBudgetLinesAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _budgetLineRepository.GetByPlanIdAsync(planId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainingPlanBudgetLineDto> UpdateBudgetLineAsync(UpdateTrainingPlanBudgetLineDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetLineRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Training plan budget line with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _budgetLineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteBudgetLineAsync(Guid budgetLineId, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetLineRepository.GetByIdAsync(budgetLineId);

        if (entity == null)
            throw new ArgumentException($"Training plan budget line with ID '{budgetLineId}' not found.");

        await _budgetLineRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<string> GeneratePlanNumberAsync(CancellationToken ct)
        => _numberSequence.GenerateAsync("TPLAN", ct);
}
