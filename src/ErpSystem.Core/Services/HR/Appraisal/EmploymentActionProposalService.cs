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
    // ⚠ No segregation-of-duties check is added here, unlike the movement and the offer: this
    // entity carries no unambiguous requester field (only TenantEntity.CreatedById, whose id space
    // is not stated on this type), so there is nothing safe to compare the approver against. If a
    // proposer field is added later, guard it here the way StaffMovementService.ApproveAsync does.

    public async Task<EmploymentActionProposalDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default)
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

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} submitted for approval", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<EmploymentActionProposalDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        WorkflowOutcome approvalOutcome;
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId))
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Approve");
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the approval.");

            approvalOutcome = workflowResult.Outcome;
        }
        else
        {
            HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserProvider, "approve an employment action proposal", HrPermissions.ApprovePerformance);
            approvalOutcome = WorkflowOutcome.Approved;
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, userId);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} approval step processed", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<EmploymentActionProposalDto> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        var rejectionText = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();

        WorkflowOutcome rejectionOutcome;
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId))
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Reject", rejectionText);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the rejection.");

            rejectionOutcome = workflowResult.Outcome;
        }
        else
        {
            HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserProvider, "reject an employment action proposal", HrPermissions.ApprovePerformance);
            rejectionOutcome = WorkflowOutcome.Rejected;
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, rejectionOutcome, userId, rejectionText);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} rejected", id);
        return ToDto(await ReloadAsync(id, entity.TenantId, cancellationToken));
    }

    public async Task<EmploymentActionProposalDto> RecallAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        if (entity.Status != EmploymentActionProposalStatus.PendingApproval)
            throw new InvalidOperationException("Only a proposal still awaiting approval can be recalled.");

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
