using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>Theme 11 — read + approve the salary review proposals raised from appraisal recommendations.</summary>
public class SalaryReviewProposalService : ISalaryReviewProposalService
{
    private readonly IGenericRepository<SalaryReviewProposal> _repository;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SalaryReviewProposalService> _logger;

    /// <summary>
    /// Entity type registered with the workflow engine. Must match the catalog entry in
    /// <c>WorkflowEntityTypeCatalogService</c> and the aliases on
    /// <c>SalaryReviewProposalWorkflowStatusAdapter</c>.
    /// </summary>
    private const string EntityType = "SalaryReviewProposal";

    public SalaryReviewProposalService(
        IGenericRepository<SalaryReviewProposal> repository,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SalaryReviewProposalService> logger)
    {
        _repository = repository;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    // A proposal owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<SalaryReviewProposal> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Salary review proposal '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<SalaryReviewProposalDto>> GetAllAsync(SalaryReviewProposalStatus? status = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Employee)
            .Include(p => p.SourceAppraisal)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var items = await query.OrderByDescending(p => p.CreatedAt).Take(500).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<SalaryReviewProposalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return ToDto(await ReloadAsync(entity.Id, entity.TenantId, cancellationToken));
    }

    public async Task<SalaryReviewProposalDto> UpdateAsync(Guid id, UpdateSalaryReviewProposalDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != SalaryReviewProposalStatus.Proposed)
            throw new InvalidOperationException(
                $"Only a proposed salary review can be amended. This one is {entity.Status}.");

        entity.ProposedPercent = dto.ProposedPercent;
        entity.ProposedAmount = dto.ProposedAmount;
        if (dto.Notes != null) entity.Notes = dto.Notes;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} amended", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    // ── Approval workflow ──────────────────────────────────────────────────
    // Submit / approve / reject / recall all run through the generic workflow engine; this
    // service never sets an approval status itself. SalaryReviewProposalWorkflowStatusAdapter
    // maps the engine's outcome onto the entity.
    //
    // ⚠ Like every other entity on the engine, this is inoperable until a SalaryReviewProposal
    // workflow definition has been published — the authority to approve comes from the
    // definition, not from a role attribute.

    public async Task<SalaryReviewProposalDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status == SalaryReviewProposalStatus.PendingApproval)
            throw new InvalidOperationException("This proposal is already awaiting approval.");
        if (entity.Status != SalaryReviewProposalStatus.Proposed)
            throw new InvalidOperationException($"A {entity.Status} proposal cannot be submitted for approval.");

        // A merit increase with no percentage, or a bonus with no amount, is nothing an approver
        // can weigh and nothing payroll could act on. The handler that raises the proposal has an
        // appraisal, not a pay decision, so the figure has to be filled in before it goes out.
        var hasFigure = entity.ProposalType == SalaryReviewProposalType.Bonus
            ? entity.ProposedAmount is > 0
            : entity.ProposedPercent is > 0;

        if (!hasFigure)
            throw new InvalidOperationException(
                entity.ProposalType == SalaryReviewProposalType.Bonus
                    ? "Set the proposed bonus amount before submitting this proposal."
                    : "Set the proposed increase percentage before submitting this proposal.");

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start the proposal approval workflow.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, workflowResult.Outcome, _currentUserProvider.UserId);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} submitted for approval", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<SalaryReviewProposalDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Approve");
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the approval.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, workflowResult.Outcome, userId);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} approval step processed", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<SalaryReviewProposalDto> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var rejectionText = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Reject", rejectionText);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the rejection.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, workflowResult.Outcome, userId, rejectionText);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} rejected", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<SalaryReviewProposalDto> RecallAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        if (entity.Status != SalaryReviewProposalStatus.PendingApproval)
            throw new InvalidOperationException("Only a proposal still awaiting approval can be recalled.");

        var workflowResult = await _workflowIntegrationService.RecallAsync(EntityType, id, userId);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to recall the proposal.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType).ApplyRecallOutcome(entity, userId);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} recalled", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<SalaryReviewProposalDto> MarkAppliedAsync(Guid id, string? notes, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != SalaryReviewProposalStatus.Approved)
            throw new InvalidOperationException(
                entity.Status == SalaryReviewProposalStatus.Applied
                    ? "This proposal has already been applied."
                    : $"Only an approved proposal can be marked applied. This one is {entity.Status}.");

        entity.Status = SalaryReviewProposalStatus.Applied;
        if (!string.IsNullOrWhiteSpace(notes)) entity.Notes = notes;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} marked applied", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    private Guid RequireUserId()
    {
        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new InvalidOperationException("No signed-in user could be resolved for this workflow action.");
        return userId;
    }

    private Task<SalaryReviewProposal> ReloadAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
        => _repository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Employee)
            .Include(p => p.SourceAppraisal)
            .FirstAsync(p => p.Id == id, cancellationToken);

    private static SalaryReviewProposalDto ToDto(SalaryReviewProposal p) => new()
    {
        Id = p.Id,
        EmployeeId = p.EmployeeId,
        EmployeeName = p.Employee?.FullName,
        SourceAppraisalId = p.SourceAppraisalId,
        AppraisalNumber = p.SourceAppraisal?.AppraisalNumber,
        ProposalType = p.ProposalType,
        ProposedPercent = p.ProposedPercent,
        ProposedAmount = p.ProposedAmount,
        Status = p.Status,
        Notes = p.Notes
    };
}
