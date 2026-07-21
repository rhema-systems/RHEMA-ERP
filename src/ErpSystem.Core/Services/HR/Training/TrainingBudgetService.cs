using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingBudgetService : ITrainingBudgetService
{
    private readonly ITrainingBudgetRepository _budgetRepository;
    private readonly ITrainingBudgetTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingBudgetService> _logger;

    public TrainingBudgetService(
        ITrainingBudgetRepository budgetRepository,
        ITrainingBudgetTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainingBudgetService> logger)
    {
        _budgetRepository = budgetRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Budget queries ────────────────────────────────────────────────────────

    public async Task<TrainingBudgetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training budget with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingBudgetDto?> GetByBudgetCodeAsync(string budgetCode, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetByBudgetCodeAsync(budgetCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetByYearAsync(year);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetByYearAndQuarterAsync(int year, int? quarter, CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetByYearAndQuarterAsync(year, quarter);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetApprovedAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetApprovedAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetOverBudgetAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetWithExceededBudgetAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetByOrganizationUnitAsync(organizationUnitId);
        return entities.ToSummaryDtoList();
    }

    // ── Budget CRUD ───────────────────────────────────────────────────────────

    public async Task<TrainingBudgetDto> CreateAsync(CreateTrainingBudgetDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);
        entity.Status = TrainingBudgetStatus.Draft;

        await _budgetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training budget created: {BudgetCode}", dto.BudgetCode);

        return entity.ToDto();
    }

    public async Task<TrainingBudgetDto> UpdateAsync(UpdateTrainingBudgetDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Training budget with ID '{dto.Id}' not found.");

        if (entity.Status == TrainingBudgetStatus.Approved || entity.Status == TrainingBudgetStatus.Closed)
            throw new InvalidOperationException("Cannot update an approved or closed training budget.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training budget with ID '{id}' not found.");

        if (entity.Status != TrainingBudgetStatus.Draft)
            throw new InvalidOperationException("Only draft training budgets can be deleted.");

        await _budgetRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training budget {BudgetId} deleted", id);

        return true;
    }

    // ── Budget workflow ───────────────────────────────────────────────────────

    public async Task<bool> ApproveAsync(ApproveTrainingBudgetDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetByIdAsync(dto.BudgetId);

        if (entity == null)
            throw new ArgumentException($"Training budget with ID '{dto.BudgetId}' not found.");

        if (entity.Status != TrainingBudgetStatus.Draft)
            throw new InvalidOperationException($"Training budget cannot be approved from status '{entity.Status}'.");

        entity.Status = TrainingBudgetStatus.Approved;
        entity.ApprovedById = dto.ApprovedById;
        entity.ApprovalDate = dto.ApprovalDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = dto.ApprovedById.ToString();

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training budget {BudgetId} approved by {ApprovedById}", dto.BudgetId, dto.ApprovedById);

        return true;
    }

    // ── Transaction sub-operations ────────────────────────────────────────────

    public async Task<TrainingBudgetTransactionDto> RecordTransactionAsync(CreateTrainingBudgetTransactionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(dto.BudgetId);

        if (budget == null)
            throw new ArgumentException($"Training budget with ID '{dto.BudgetId}' not found.");

        if (budget.Status != TrainingBudgetStatus.Approved && budget.Status != TrainingBudgetStatus.Active)
            throw new InvalidOperationException("Transactions can only be recorded against approved or active budgets.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        // Maintain the running spend so RemainingAmount and the over-budget report stay accurate.
        // Positive Amount = debit/spend, negative = credit/refund.
        var prospectiveSpent = budget.SpentAmount + entity.Amount;
        if (entity.Amount > 0 && prospectiveSpent + budget.CommittedAmount > budget.AllocatedAmount)
            throw new InvalidOperationException(
                $"This transaction would exceed the allocated budget (allocated {budget.AllocatedAmount:N2} {budget.Currency}). " +
                $"Committed + spent would become {prospectiveSpent + budget.CommittedAmount:N2}.");

        budget.SpentAmount = prospectiveSpent;
        budget.UpdatedAt = DateTime.UtcNow;
        budget.UpdatedBy = createdByUserId.ToString();

        // Transaction insert + budget update persist together in a single SaveChanges (atomic).
        await _transactionRepository.AddAsync(entity);
        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training budget transaction recorded for budget {BudgetId} — Amount: {Amount}; new SpentAmount: {Spent}", dto.BudgetId, dto.Amount, budget.SpentAmount);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingBudgetTransactionDto>> GetTransactionsAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var entities = await _transactionRepository.GetByBudgetIdAsync(budgetId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingBudgetTransactionDto>> GetTransactionsByDateRangeAsync(Guid budgetId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var entities = await _transactionRepository.GetByDateRangeAsync(budgetId, from, to);
        return entities.Select(e => e.ToDto()).ToList();
    }
}
