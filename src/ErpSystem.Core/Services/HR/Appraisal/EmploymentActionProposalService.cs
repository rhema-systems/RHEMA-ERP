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

/// <summary>Read + status-manage the employment-action proposals raised from appraisal recommendations.</summary>
public class EmploymentActionProposalService : IEmploymentActionProposalService
{
    private readonly IGenericRepository<EmploymentActionProposal> _repository;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmploymentActionProposalService> _logger;

    /// <summary>
    /// Entity type registered with the workflow engine. Must match the catalog entry in
    /// <c>WorkflowEntityTypeCatalogService</c> and the aliases on
    /// <c>EmploymentActionProposalWorkflowStatusAdapter</c>.
    /// </summary>
    private const string EntityType = "EmploymentActionProposal";

    public EmploymentActionProposalService(
        IGenericRepository<EmploymentActionProposal> repository,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmploymentActionProposalService> logger)
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
    private async Task<EmploymentActionProposal> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employment action proposal '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<EmploymentActionProposalDto>> GetAllAsync(EmploymentActionProposalStatus? status = null, CancellationToken cancellationToken = default)
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

    public async Task<EmploymentActionProposalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return ToDto(await ReloadAsync(entity.Id, entity.TenantId, cancellationToken));
    }

    // ── Approval workflow ──────────────────────────────────────────────────
    // Submit / approve / reject / recall run through the generic workflow engine, so a
    // recognition and a termination can route to different approvers without a code change.
    // EmploymentActionProposalWorkflowStatusAdapter maps the engine's outcome onto the entity.
    //
    // ⚠ This used to say "Inoperable until an EmploymentActionProposal workflow definition has been
    // published". It was NOT inoperable — it auto-approved (corrected 2026-09-15). With no
    // definition, WorkflowIntegrationService.SubmitAsync returns WorkflowOutcome.Approved and the
    // adapter maps it to Approved, so submitting a proposal to promote, discipline or terminate
    // someone approved it in one step with nobody having reviewed it. All four methods below now
    // take a no-workflow branch; see HrWorkflowFallbackAuthority for the mechanism and for why
    // fixing submit alone would have left the proposal stuck at PendingApproval for ever.
    //
    // Segregation of duties (F3, D-12, D-94): batch 2 gave the entity its submitter, an employee id,
    // stamped at submission. Who decides is the record's rule on both paths — the Managing Director
    // (D-104), never the submitter and never the employee the proposal is about
    // (ProposalDecisionRules) — and only the submitter recalls when no engine keeps that rule (F9).

    public async Task<EmploymentActionProposalDto> SubmitForApprovalAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status == EmploymentActionProposalStatus.PendingApproval)
            throw new InvalidOperationException("This proposal is already awaiting approval.");
        if (entity.Status != EmploymentActionProposalStatus.Proposed)
            throw new InvalidOperationException($"A {entity.Status} proposal cannot be submitted for approval.");

        var hasWorkflow = await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType);

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start the proposal approval workflow.");

        var submitOutcome = hasWorkflow ? workflowResult.Outcome : WorkflowOutcome.Pending;

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, submitOutcome, _currentUserProvider.UserId);

        // Who submitted it, so they do not decide it and only they recall it (F3, F9).
        entity.SubmittedById = actorEmployeeId is { } submitter && submitter != Guid.Empty ? submitter : null;
        entity.SubmittedDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} submitted for approval", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    /// <summary>
    /// The record's rule before either path is asked: the proposal is awaiting a decision, and the caller may make it.
    /// A fallback approve used to approve a proposal nobody had submitted.
    /// </summary>
    private void EnsureDecidable(EmploymentActionProposal entity, Guid? actorEmployeeId)
    {
        if (entity.Status != EmploymentActionProposalStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only a proposal awaiting approval can be decided. This one is {entity.Status}.");

        ProposalDecisionRules.EnsureMayDecide(
            _currentUserProvider.Roles, actorEmployeeId, entity.SubmittedById, entity.EmployeeId, "employment action proposal");
    }

    public async Task<EmploymentActionProposalDto> ApproveAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        EnsureDecidable(entity, actorEmployeeId);

        // Engine when a definition is published; with none, the record's rule above is the authority.
        var approvalOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalDecidedByRecordAsync(
            _workflowIntegrationService, EntityType, id, userId, "Approve", null);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, userId);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} approval step processed", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<EmploymentActionProposalDto> RejectAsync(Guid id, string? reason, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
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

        _logger.LogInformation("Employment action proposal {Id} rejected", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<EmploymentActionProposalDto> RecallAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        if (entity.Status != EmploymentActionProposalStatus.PendingApproval)
            throw new InvalidOperationException("Only a proposal still awaiting approval can be recalled.");

        // The engine keeps recall to the requester; with no definition nothing did (F9).
        ProposalDecisionRules.EnsureMayRecall(actorEmployeeId, entity.SubmittedById, "employment action proposal");

        // Skipped when nothing is published: RecallWorkflowAsync answers "No active workflow found"
        // without an instance, so the recall would fail on exactly the tenants where submitting now
        // leaves a proposal at PendingApproval.
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            var workflowResult = await _workflowIntegrationService.RecallAsync(EntityType, id, userId);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to recall the proposal.");
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType).ApplyRecallOutcome(entity, userId);
        entity.SubmittedById = null;
        entity.SubmittedDate = null;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} recalled", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<EmploymentActionProposalDto> MarkActionedAsync(Guid id, string? notes, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != EmploymentActionProposalStatus.Approved)
            throw new InvalidOperationException(
                entity.Status == EmploymentActionProposalStatus.Actioned
                    ? "This proposal has already been actioned."
                    : $"Only an approved proposal can be marked actioned. This one is {entity.Status}.");

        entity.Status = EmploymentActionProposalStatus.Actioned;
        if (!string.IsNullOrWhiteSpace(notes)) entity.Notes = notes;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} marked actioned", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    private Guid RequireUserId()
    {
        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new InvalidOperationException("No signed-in user could be resolved for this workflow action.");
        return userId;
    }

    private Task<EmploymentActionProposal> ReloadAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
        => _repository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Employee)
            .Include(p => p.SourceAppraisal)
            .FirstAsync(p => p.Id == id, cancellationToken);

    private static EmploymentActionProposalDto ToDto(EmploymentActionProposal p) => new()
    {
        Id = p.Id,
        EmployeeId = p.EmployeeId,
        EmployeeName = p.Employee?.FullName,
        SourceAppraisalId = p.SourceAppraisalId,
        AppraisalNumber = p.SourceAppraisal?.AppraisalNumber,
        ActionType = p.ActionType,
        Status = p.Status,
        Notes = p.Notes
    };
}
