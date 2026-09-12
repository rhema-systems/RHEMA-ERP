using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
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
    private readonly IGenericRepository<TrainingProgram> _programRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingPlanService> _logger;

    private readonly INumberSequenceService _numberSequence;

    public TrainingPlanService(
        ITrainingPlanRepository planRepository,
        ITrainingPlanItemRepository planItemRepository,
        ITrainingPlanBudgetLineRepository budgetLineRepository,
        IGenericRepository<TrainingProgram> programRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingPlanService> logger)
    {
        _planRepository = planRepository;
        _planItemRepository = planItemRepository;
        _budgetLineRepository = budgetLineRepository;
        _programRepository = programRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
        _logger = logger;
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

    // A plan owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<TrainingPlan> GetOwnedPlanAsync(Guid id)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training plan with ID '{id}' not found.");
        return entity;
    }

    // An item owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<TrainingPlanItem> GetOwnedItemAsync(Guid id)
    {
        var entity = await _planItemRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training plan item with ID '{id}' not found.");
        return entity;
    }

    // A budget line owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<TrainingPlanBudgetLine> GetOwnedBudgetLineAsync(Guid id)
    {
        var entity = await _budgetLineRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training plan budget line with ID '{id}' not found.");
        return entity;
    }

    private async Task EnsureOwnedProgramAsync(Guid? programId, Guid tenantId)
    {
        if (!programId.HasValue || programId.Value == Guid.Empty)
            return;

        var program = await _programRepository.GetByIdAsync(programId.Value);
        if (program == null || program.TenantId != tenantId)
            throw new ArgumentException($"Training program with ID '{programId}' not found.");
    }

    // ── Plan queries ──────────────────────────────────────────────────────────

    public async Task<TrainingPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Training plan with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingPlanDto?> GetByPlanNumberAsync(string planNumber, CancellationToken cancellationToken = default)
    {
        // Plan numbers come from a per-tenant sequence, so the same number can exist in several tenants.
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetByPlanNumberAsync(planNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<TrainingPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _planRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingPlanSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _planRepository.GetByYearAsync(year);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingPlanSummaryDto>> GetByDepartmentIdAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _planRepository.GetByOrganizationUnitAsync(departmentId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingPlanSummaryDto>> GetByStatusAsync(TrainingPlanStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _planRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Plan CRUD ─────────────────────────────────────────────────────────────

    public async Task<TrainingPlanDto> CreateAsync(CreateTrainingPlanDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);
        entity.PlanNumber = await GeneratePlanNumberAsync(cancellationToken);
        entity.Status = TrainingPlanStatus.Draft;

        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training plan created: {PlanNumber}", entity.PlanNumber);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingPlanDto> UpdateAsync(UpdateTrainingPlanDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(dto.Id);

        // Once submitted, the plan is what the approver sees — only a Draft can be edited directly;
        // everything past that moves only through Submit/Approve. Matches DeleteAsync's own rule below.
        if (entity.Status != TrainingPlanStatus.Draft)
            throw new InvalidOperationException($"Cannot update a training plan with status '{entity.Status}'. Only draft plans can be edited.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(id);

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
        var entity = await GetOwnedPlanAsync(planId);

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

    public async Task<bool> ApproveAsync(ApproveTrainingPlanDto dto, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(dto.PlanId);

        if (entity.Status != TrainingPlanStatus.PendingApproval)
            throw new InvalidOperationException($"Training plan cannot be approved from status '{entity.Status}'.");

        entity.Status = TrainingPlanStatus.Approved;
        entity.ApprovedById = approvedById;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = approvedById.ToString();

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training plan {PlanNumber} approved by {ApprovedById}", entity.PlanNumber, approvedById);

        return true;
    }

    // ── Plan item sub-operations ──────────────────────────────────────────────

    public async Task<TrainingPlanItemDto> AddItemAsync(CreateTrainingPlanItemDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedPlanAsync(dto.PlanId);
        await EnsureOwnedProgramAsync(dto.ProgramId, current);

        var entity = dto.ToEntity(current, createdByUserId);

        await _planItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _planItemRepository.GetByPlanIdAsync(dto.PlanId);
        return reloaded.First(i => i.Id == entity.Id).ToDto();
    }

    public async Task<IEnumerable<TrainingPlanItemDto>> GetItemsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _planItemRepository.GetByPlanIdAsync(planId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainingPlanItemDto> UpdateItemAsync(UpdateTrainingPlanItemDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(dto.Id);
        await EnsureOwnedProgramAsync(dto.ProgramId, entity.TenantId);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _planItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _planItemRepository.GetByPlanIdAsync(entity.PlanId);
        return reloaded.First(i => i.Id == entity.Id).ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);

        await _planItemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Budget line sub-operations ────────────────────────────────────────────

    public async Task<TrainingPlanBudgetLineDto> AddBudgetLineAsync(CreateTrainingPlanBudgetLineDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedPlanAsync(dto.PlanId);

        var entity = dto.ToEntity(current, createdByUserId);

        await _budgetLineRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _budgetLineRepository.GetByPlanIdAsync(dto.PlanId);
        return reloaded.First(b => b.Id == entity.Id).ToDto();
    }

    public async Task<IEnumerable<TrainingPlanBudgetLineDto>> GetBudgetLinesAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _budgetLineRepository.GetByPlanIdAsync(planId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainingPlanBudgetLineDto> UpdateBudgetLineAsync(UpdateTrainingPlanBudgetLineDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetLineAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _budgetLineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _budgetLineRepository.GetByPlanIdAsync(entity.PlanId);
        return reloaded.First(b => b.Id == entity.Id).ToDto();
    }

    public async Task<bool> DeleteBudgetLineAsync(Guid budgetLineId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetLineAsync(budgetLineId);

        await _budgetLineRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<string> GeneratePlanNumberAsync(CancellationToken ct)
        => _numberSequence.GenerateAsync("TPLAN", ct);
}
