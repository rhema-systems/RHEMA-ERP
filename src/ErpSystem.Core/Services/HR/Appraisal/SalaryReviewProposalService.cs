using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
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
    // Submit / approve / reject / recall run through the generic workflow engine when a definition
    // is published, and through HrWorkflowFallbackAuthority when none is; this service never sets
    // an approval status itself. SalaryReviewProposalWorkflowStatusAdapter maps the outcome onto
    // the entity.
    //
    // Who decides is the record's rule, on both paths (F3, D-12, D-94, D-104): the Managing Director,
    // never the submitter and never the employee the proposal is about — see ProposalDecisionRules.
    // The submitter is recorded at submission for it.

    public async Task<SalaryReviewProposalDto> SubmitForApprovalAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
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

        // Submitting must never approve. With no published definition the engine returns
        // WorkflowOutcome.Approved as its "approval is not configured" signal, and this adapter
        // maps Approved to SalaryReviewProposalStatus.Approved — so Submit would approve a pay
        // proposal with nobody asked. Defence in depth: a SALARY_REVIEW_PROPOSAL definition IS
        // seeded, so this only bites on an unseeded tenant. See HrWorkflowFallbackAuthority.
        var (workflowResult, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start the proposal approval workflow.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, submitOutcome, _currentUserProvider.UserId);

        // Who submitted it, so they do not decide it and only they recall it (F3, F9).
        entity.SubmittedById = actorEmployeeId is { } submitter && submitter != Guid.Empty ? submitter : null;
        entity.SubmittedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} submitted for approval", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    /// <summary>
    /// The record's rule before either path is asked: the proposal is awaiting a decision, and the caller may make it.
    /// A fallback approve used to approve a proposal nobody had submitted.
    /// </summary>
    private void EnsureDecidable(SalaryReviewProposal entity, Guid? actorEmployeeId)
    {
        if (entity.Status != SalaryReviewProposalStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only a proposal awaiting approval can be decided. This one is {entity.Status}.");

        ProposalDecisionRules.EnsureMayDecide(
            _currentUserProvider.Roles, actorEmployeeId, entity.SubmittedById, entity.EmployeeId, "salary review proposal");
    }

    public async Task<SalaryReviewProposalDto> ApproveAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        EnsureDecidable(entity, actorEmployeeId);

        // Engine when a definition is published; with none, the record's rule above is the authority — the Managing
        // Director holds no HR approve permission, and HR, which does, is not a decider.
        var approvalOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalDecidedByRecordAsync(
            _workflowIntegrationService, EntityType, id, userId, "Approve", null);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, userId);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} approval step processed", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<SalaryReviewProposalDto> RejectAsync(Guid id, string? reason, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        EnsureDecidable(entity, actorEmployeeId);

        var rejectionText = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();

        var rejectionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalDecidedByRecordAsync(
            _workflowIntegrationService, EntityType, id, userId, "Reject", rejectionText);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, rejectionOutcome, userId, rejectionText);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} rejected", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<SalaryReviewProposalDto> RecallAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        if (entity.Status != SalaryReviewProposalStatus.PendingApproval)
            throw new InvalidOperationException("Only a proposal still awaiting approval can be recalled.");

        // The engine keeps recall to the requester; with no definition nothing did (F9).
        ProposalDecisionRules.EnsureMayRecall(actorEmployeeId, entity.SubmittedById, "salary review proposal");

        // Skipped when nothing is published — RecallWorkflowAsync answers "No active workflow
        // found" without an instance, which would strand a proposal that Submit had just left at
        // PendingApproval. The record returns to Draft via the adapter either way.
        await HrWorkflowFallbackAuthority.RecallAsync(_workflowIntegrationService, EntityType, id, userId);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType).ApplyRecallOutcome(entity, userId);
        entity.SubmittedById = null;
        entity.SubmittedDate = null;

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
