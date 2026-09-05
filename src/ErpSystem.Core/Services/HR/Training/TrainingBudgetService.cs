using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Training;
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
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingBudgetService> _logger;

    public TrainingBudgetService(
        ITrainingBudgetRepository budgetRepository,
        ITrainingBudgetTransactionRepository transactionRepository,
        IGenericRepository<Employee> employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingBudgetService> logger)
    {
        _budgetRepository = budgetRepository;
        _transactionRepository = transactionRepository;
        _employeeRepository = employeeRepository;
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

    // Callers supply the tenant alongside the DTO (the HR house convention). Reject anything other than
    // the authenticated tenant so a supplied id can never widen the scope of a write.
    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    // A budget from another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<TrainingBudget> GetOwnedBudgetAsync(Guid id)
    {
        var entity = await _budgetRepository.GetForTenantAsync(id, GetTenantId());

        if (entity == null)
            throw new ArgumentException($"Training budget with ID '{id}' not found.");

        return entity;
    }

    // ── Budget queries ────────────────────────────────────────────────────────

    public async Task<TrainingBudgetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetWithFullDetailsAsync(id, GetTenantId());

        if (entity == null)
            throw new ArgumentException($"Training budget with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingBudgetDto?> GetByBudgetCodeAsync(string budgetCode, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetByBudgetCodeAsync(budgetCode, GetTenantId());
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetAllForTenantAsync(GetTenantId());
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetByYearAsync(year, GetTenantId());
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetByYearAndQuarterAsync(int year, int? quarter, CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetByYearAndQuarterAsync(year, quarter, GetTenantId());
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetApprovedAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetApprovedAsync(GetTenantId());
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetOverBudgetAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetWithExceededBudgetAsync(GetTenantId());
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingBudgetSummaryDto>> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await _budgetRepository.GetByOrganizationUnitAsync(organizationUnitId, GetTenantId());
        return entities.ToSummaryDtoList();
    }

    // ── Budget CRUD ───────────────────────────────────────────────────────────

    public async Task<TrainingBudgetDto> CreateAsync(CreateTrainingBudgetDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = RequireCurrentTenant(tenantId);

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        var duplicate = await _budgetRepository.GetQueryable()
            .AnyAsync(b => b.TenantId == current && b.BudgetCode == dto.BudgetCode, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A training budget with code '{dto.BudgetCode}' already exists.");

        var entity = dto.ToEntity(current, createdByUserId);
        entity.Status = TrainingBudgetStatus.Draft;

        await _budgetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training budget created: {BudgetCode}", dto.BudgetCode);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingBudgetDto> UpdateAsync(UpdateTrainingBudgetDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(dto.Id);

        // Once approved, the allocation is what spend is measured against — only a Draft can be
        // edited directly. Matches DeleteAsync's own rule below.
        if (entity.Status != TrainingBudgetStatus.Draft)
            throw new InvalidOperationException($"Cannot update a training budget with status '{entity.Status}'. Only draft budgets can be edited.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(id);

        if (entity.Status != TrainingBudgetStatus.Draft)
            throw new InvalidOperationException("Only draft training budgets can be deleted.");

        await _budgetRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training budget {BudgetId} deleted", id);

        return true;
    }

    // ── Budget workflow ───────────────────────────────────────────────────────

    public async Task<bool> ApproveAsync(ApproveTrainingBudgetDto dto, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(dto.BudgetId);

        if (entity.Status != TrainingBudgetStatus.Draft)
            throw new InvalidOperationException($"Training budget cannot be approved from status '{entity.Status}'.");

        entity.Status = TrainingBudgetStatus.Approved;
        entity.ApprovedById = approvedById;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = approvedById.ToString();

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training budget {BudgetId} approved by {ApprovedById}", dto.BudgetId, approvedById);

        return true;
    }

    // ── Transaction sub-operations ────────────────────────────────────────────

    public async Task<TrainingBudgetTransactionDto> RecordTransactionAsync(CreateTrainingBudgetTransactionDto dto, Guid tenantId, Guid recordedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = RequireCurrentTenant(tenantId);
        var budget = await GetOwnedBudgetAsync(dto.BudgetId);

        if (budget.Status != TrainingBudgetStatus.Approved && budget.Status != TrainingBudgetStatus.Active)
            throw new InvalidOperationException("Transactions can only be recorded against approved or active budgets.");

        // RecordedById is [Required] on the DTO, but that is a no-op on a non-nullable Guid — an
        // omitted value arrives as Guid.Empty and hits the Employees FK as a raw SQL 547. Default it
        // to the caller and validate an explicit id, so a bad one reads as a business rule not a 500.
        // (An explicit id is still accepted: HR legitimately records a transaction on someone's behalf.)
        if (dto.RecordedById == Guid.Empty)
        {
            dto.RecordedById = recordedByEmployeeId;
        }
        else
        {
            var recorder = await _employeeRepository.GetByIdAsync(dto.RecordedById);
            if (recorder == null || recorder.TenantId != resolvedTenantId)
                throw new ArgumentException($"Employee with ID '{dto.RecordedById}' not found.");
        }

        var entity = dto.ToEntity(resolvedTenantId, recordedByEmployeeId);

        // Maintain the running spend so RemainingAmount and the over-budget report stay accurate.
        // Positive Amount = debit/spend, negative = credit/refund.
        var prospectiveSpent = budget.SpentAmount + entity.Amount;
        if (entity.Amount > 0 && prospectiveSpent + budget.CommittedAmount > budget.AllocatedAmount)
            throw new InvalidOperationException(
                $"This transaction would exceed the allocated budget (allocated {budget.AllocatedAmount:N2} {budget.Currency}). " +
                $"Committed + spent would become {prospectiveSpent + budget.CommittedAmount:N2}.");

        budget.SpentAmount = prospectiveSpent;
        budget.UpdatedAt = DateTime.UtcNow;
        budget.UpdatedBy = recordedByEmployeeId.ToString();

        // Transaction insert + budget update persist together in a single SaveChanges (atomic).
        await _transactionRepository.AddAsync(entity);
        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training budget transaction recorded for budget {BudgetId} — Amount: {Amount}; new SpentAmount: {Spent}", dto.BudgetId, dto.Amount, budget.SpentAmount);

        var reloaded = await _transactionRepository.GetByBudgetIdAsync(dto.BudgetId, resolvedTenantId);
        return reloaded.First(t => t.Id == entity.Id).ToDto();
    }

    public async Task<IEnumerable<TrainingBudgetTransactionDto>> GetTransactionsAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var entities = await _transactionRepository.GetByBudgetIdAsync(budgetId, GetTenantId());
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingBudgetTransactionDto>> GetTransactionsByDateRangeAsync(Guid budgetId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var entities = await _transactionRepository.GetByDateRangeAsync(budgetId, from, to, GetTenantId());
        return entities.Select(e => e.ToDto()).ToList();
    }
}
