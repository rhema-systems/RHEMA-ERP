using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF REQUISITION SERVICE
// ============================================================================

#region Staff Requisition Service

public class StaffRequisitionService : IStaffRequisitionService
{
    private readonly IStaffRequisitionRepository _requisitionRepository;
    private readonly IStaffRequisitionCostRepository _costRepository;
    private readonly IStaffRequisitionAttachmentRepository _attachmentRepository;
    private readonly IStaffRequisitionCommentRepository _commentRepository;
    private readonly IStaffRequisitionHistoryRepository _historyRepository;
    private readonly IJobDescriptionRepository _jobDescriptionRepository;
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HrCurrencyBridge _currency;
    private readonly ILogger<StaffRequisitionService> _logger;

    /// <summary>
    /// The workflow entity type this service drives. Approval authority comes from the published
    /// definition for this type, not from a role attribute on the controller — see
    /// <c>StaffRequisitionWorkflowStatusAdapter</c>.
    /// </summary>
    private const string EntityType = "StaffRequisition";

    public StaffRequisitionService(
        IStaffRequisitionRepository requisitionRepository,
        IStaffRequisitionCostRepository costRepository,
        IStaffRequisitionAttachmentRepository attachmentRepository,
        IStaffRequisitionCommentRepository commentRepository,
        IStaffRequisitionHistoryRepository historyRepository,
        IJobDescriptionRepository jobDescriptionRepository,
        ICompanyHrPolicyProvider policyProvider,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IUnitOfWork unitOfWork,
        HrCurrencyBridge currency,
        ILogger<StaffRequisitionService> logger)
    {
        _currency = currency;
        _requisitionRepository = requisitionRepository;
        _costRepository = costRepository;
        _attachmentRepository = attachmentRepository;
        _commentRepository = commentRepository;
        _historyRepository = historyRepository;
        _jobDescriptionRepository = jobDescriptionRepository;
        _policyProvider = policyProvider;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
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

    // A requisition owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffRequisition> GetOwnedAsync(Guid id)
    {
        var entity = await _requisitionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff requisition with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffRequisitionCost> GetOwnedCostAsync(Guid id)
    {
        var entity = await _costRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Requisition cost with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffRequisitionAttachment> GetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Requisition attachment with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffRequisitionComment> GetOwnedCommentAsync(Guid id)
    {
        var entity = await _commentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Requisition comment with ID '{id}' not found.");
        return entity;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<StaffRequisitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _requisitionRepository.GetWithSummaryNavAsync(id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff requisition with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<StaffRequisitionDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _requisitionRepository.GetWithFullDetailsAsync(id);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDetailDto();
    }

    public async Task<StaffRequisitionStatusSummaryDto> GetStatusSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _requisitionRepository.GetSummaryQueryable().Where(r => r.TenantId == tenantId);
        var counts = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var total = await query.CountAsync(cancellationToken);

        return new StaffRequisitionStatusSummaryDto
        {
            TotalRequisitions    = total,
            Draft                = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Draft)?.Count ?? 0,
            Submitted            = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Submitted)?.Count ?? 0,
            UnderReview          = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.UnderReview)?.Count ?? 0,
            Approved             = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Approved)?.Count ?? 0,
            Rejected             = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Rejected)?.Count ?? 0,
            OnHold               = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.OnHold)?.Count ?? 0,
            Cancelled            = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Cancelled)?.Count ?? 0,
            PartiallyFulfilled   = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.PartiallyFulfilled)?.Count ?? 0,
            Fulfilled            = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Fulfilled)?.Count ?? 0,
        };
    }

    public async Task<StaffRequisitionDto?> GetByRequisitionNumberAsync(string requisitionNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _requisitionRepository.GetByRequisitionNumberAsync(requisitionNumber);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetSummaryQueryable()
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffRequisitionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = PagingGuard.Clamp(pageNumber, pageSize);

        var tenantId = GetTenantId();
        var query = _requisitionRepository.GetSummaryQueryable().Where(r => r.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.RequestDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffRequisitionSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByOrganizationUnitAsync(organizationUnitId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByLocationAsync(locationId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByPositionAsync(positionId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByStatusAsync(StaffRequisitionStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByTypeAsync(StaffRequisitionType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByRequestedByAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByRequestedByAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetPendingReviewAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetPendingReviewAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetOpenRequisitionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetOpenRequisitionsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetOverdueAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByJobVacancyAsync(Guid jobVacancyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByJobVacancyAsync(jobVacancyId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetUpcomingStartDateAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetUpcomingStartDateAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<StaffRequisitionDto> CreateAsync(CreateStaffRequisitionDto createDto, Guid tenantId, Guid requestedByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        if (!createDto.AllowInternalCandidates && !createDto.AllowExternalCandidates)
            throw new ArgumentException("At least one of Allow Internal Candidates or Allow External Candidates must be selected.");

        var entity = createDto.ToEntity(current, requestedByUserId);
        entity.RequisitionNumber = await GenerateRequisitionNumberAsync(current, cancellationToken);
        await RequireJobDescriptionOfPositionAsync(entity, cancellationToken);
        await ApplyBudgetLinkAsync(entity, createDto.ManpowerBudgetLineId, cancellationToken);

        await _requisitionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition created: {RequisitionNumber}", entity.RequisitionNumber);

        return await ReloadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<StaffRequisitionDto> UpdateAsync(UpdateStaffRequisitionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        if (!updateDto.AllowInternalCandidates && !updateDto.AllowExternalCandidates)
            throw new ArgumentException("At least one of Allow Internal Candidates or Allow External Candidates must be selected.");

        var entity = await GetOwnedAsync(updateDto.Id);

        if (entity.Status != StaffRequisitionStatus.Draft && entity.Status != StaffRequisitionStatus.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected requisitions can be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await RequireJobDescriptionOfPositionAsync(entity, cancellationToken);
        await ApplyBudgetLinkAsync(entity, updateDto.ManpowerBudgetLineId, cancellationToken);

        await _requisitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition updated: {RequisitionNumber}", entity.RequisitionNumber);

        return await ReloadDtoAsync(entity.Id, cancellationToken);
    }

    /// <summary>
    /// A requisition may name the job description it recruits against (round 3, lane Q — the
    /// form never sent one, and nothing checked one). The reference is optional; when present it
    /// must be a description of THIS requisition's position: an offer letter later picks the
    /// position's description, and a requisition pointing at another post's description would
    /// read as one job and hire for another. Refused as an <see cref="InvalidOperationException"/>
    /// — a bad reference inside a payload is a bad request, not a missing resource, and this
    /// controller maps it to 422 like every other rule here.
    /// </summary>
    private async Task RequireJobDescriptionOfPositionAsync(StaffRequisition entity, CancellationToken cancellationToken)
    {
        if (entity.JobDescriptionId is not { } jobDescriptionId) return;

        var jobDescription = await _jobDescriptionRepository.GetByIdAsync(jobDescriptionId);
        if (jobDescription == null || jobDescription.IsDeleted || jobDescription.TenantId != entity.TenantId)
            throw new InvalidOperationException("The job description named on this requisition does not exist.");

        if (jobDescription.PositionId != entity.PositionId)
            throw new InvalidOperationException(
                $"The job description '{jobDescription.JobTitle}' describes a different position; choose one written for the requisition's position.");
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != StaffRequisitionStatus.Draft)
            throw new InvalidOperationException("Only Draft requisitions can be deleted.");

        await _requisitionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition deleted: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> SubmitAsync(SubmitStaffRequisitionDto submitDto, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(submitDto.RequisitionId);

        if (entity.Status != StaffRequisitionStatus.Draft && entity.Status != StaffRequisitionStatus.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected requisitions can be submitted.");

        // Budget enforcement stays here rather than in the workflow definition: whether a request
        // exceeds approved manpower budget is a fact about the requisition, not a routing choice,
        // and Block mode must refuse before an approver is ever troubled with it.
        await EnforceBudgetAsync(entity, "submitted", cancellationToken);
        await EnforceEstablishmentAsync(entity, "submitted", cancellationToken);
        // Round 2b, R5 (D-4, D-2): the exception must be written down, and the establishment the
        // approver is asked to decide against is stamped on the record.
        await RequireExceptionJustificationAsync(entity, cancellationToken);
        await StampEstablishmentSnapshotAsync(entity, cancellationToken);

        var fromStatus = entity.Status;

        // ── G-4.1 (2026-09-15): submitting must never approve ──────────────────────────────────
        // WorkflowIntegrationService.SubmitAsync returns WorkflowOutcome.Approved whenever no
        // active definition exists for the entity type — a deliberate "approval is not configured,
        // use the direct lifecycle" signal that nine modules share. The requisition adapter maps
        // Approved straight to StaffRequisitionStatus.Approved, so on a tenant with no published
        // StaffRequisition definition pressing Submit took a requisition Draft → Approved in one
        // step, with no
        // approver, no CanUserApproveAsync check, and — the part that matters — no segregation of
        // duties, because "you cannot approve a requisition you raised yourself" lives in
        // ApproveAsync, which was never called. The history row recorded the transition, so
        // afterwards it was indistinguishable from a reviewed approval.
        //
        // ⚠ Corrected 2026-09-16: an earlier version of this comment added "(which is every
        // tenant: none is seeded anywhere in the solution)". That was false —
        // EnsureHrWorkflowsSeededAsync seeds a published StaffRequisition definition, so on a
        // seeded tenant this never fired. Kept as defence in depth; see HrWorkflowFallbackAuthority.
        //
        // The shared fallback is left alone; it is load-bearing for the other eight applications.
        // Here we simply ask the question first and refuse to let submission decide. With no
        // definition the requisition lands at Submitted and waits for a human — ApproveAsync then
        // runs its own checks, including the ones the engine would have run. See
        // RequiresFallbackApprovalAsync for who may give that decision.
        var hasWorkflow = await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType);

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to start the requisition approval workflow.");

        var submitOutcome = hasWorkflow ? workflowResult.Outcome : WorkflowOutcome.Pending;

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, submitOutcome, _currentUserProvider.UserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = submittedByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, entity.Status, submittedByUserId, submitDto.Notes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition submitted: {RequisitionNumber} (now {Status})",
            entity.RequisitionNumber, entity.Status);

        return true;
    }

    public async Task<bool> ApproveAsync(ApproveStaffRequisitionDto approveDto, Guid approvedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(approveDto.RequisitionId);

        if (entity.Status != StaffRequisitionStatus.Submitted && entity.Status != StaffRequisitionStatus.UnderReview)
            throw new InvalidOperationException("Only Submitted or UnderReview requisitions can be approved.");

        // Segregation of duties: a requester must not approve their own headcount request. The
        // definition's preventInitiatorApproval covers the same ground by ApplicationUser, but this
        // one compares Employee ids — so it still catches a requester approving through a second
        // login — and it gives the requester a message about their own requisition rather than a
        // generic "not an approver".
        // NOTE: despite its name, approvedByUserId carries the caller's EMPLOYEE id (the controller
        // passes _currentUser.EmployeeId), which is what RequestedById holds — so this compares like
        // with like.
        if (entity.RequestedById == approvedByUserId)
            throw new InvalidOperationException("You cannot approve a requisition that you raised yourself.");

        // FR-HR-136 is worded about approving a vacancy, and this is the act that authorises one:
        // a vacancy is opened FROM an approved requisition, never standalone.
        await EnforceBudgetAsync(entity, "approved", cancellationToken);
        await EnforceEstablishmentAsync(entity, "approved", cancellationToken);

        // The engine resolves approvers by ApplicationUser, so it gets UserId; everything the
        // entity stores (RequestedById, the history row's ChangedById) is an Employee FK and gets
        // the id the controller passed. See hr-attendance-actor-conventions.
        var actingUserId = _currentUserProvider.UserId;
        var fromStatus = entity.Status;

        // With a definition published, the engine names the approver and rules on the step. With
        // none, it has no instance to answer about — CanUserApproveAsync returns false and
        // ProcessApprovalAsync has nothing to process — so authority falls to the recruitment
        // administer tier and the outcome is applied directly. See RecruitmentApprovalAuthority
        // for why that tier, and note that the segregation-of-duties check above has already run
        // either way: holding the permission never lets you approve your own requisition.
        WorkflowOutcome approvalOutcome;
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                EntityType, entity.Id, actingUserId, "Approve", approveDto.Comments);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(
                    workflowResult.ExecutionResult.Message ?? "Failed to process the approval.");

            approvalOutcome = workflowResult.Outcome;
        }
        else
        {
            RecruitmentApprovalAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserProvider, "approve a staff requisition");
            approvalOutcome = WorkflowOutcome.Approved;
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, actingUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = approvedByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, entity.Status, approvedByUserId, approveDto.Comments, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition approval step processed: {RequisitionNumber} (now {Status})",
            entity.RequisitionNumber, entity.Status);

        return true;
    }

    public async Task<bool> RejectAsync(RejectStaffRequisitionDto rejectDto, Guid rejectedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(rejectDto.RequisitionId);

        if (entity.Status != StaffRequisitionStatus.Submitted && entity.Status != StaffRequisitionStatus.UnderReview)
            throw new InvalidOperationException("Only Submitted or UnderReview requisitions can be rejected.");

        var actingUserId = _currentUserProvider.UserId;
        var fromStatus = entity.Status;
        var rejectionText = string.IsNullOrWhiteSpace(rejectDto.Comments) ? "Rejected" : rejectDto.Comments.Trim();

        // Same two paths as ApproveAsync. Rejection matters as much as approval here: without the
        // no-workflow branch, a requisition submitted on an unconfigured tenant could be neither
        // approved nor rejected nor recalled, and Cancel would be its only exit.
        WorkflowOutcome rejectionOutcome;
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                EntityType, entity.Id, actingUserId, "Reject", rejectionText);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(
                    workflowResult.ExecutionResult.Message ?? "Failed to process the rejection.");

            rejectionOutcome = workflowResult.Outcome;
        }
        else
        {
            RecruitmentApprovalAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserProvider, "reject a staff requisition");
            rejectionOutcome = WorkflowOutcome.Rejected;
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, rejectionOutcome, actingUserId, rejectionText);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = rejectedByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, entity.Status, rejectedByUserId, rejectDto.Comments, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition rejected: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    /// <summary>
    /// Withdraws a requisition the requester has sent but nobody has ruled on yet, returning it to
    /// Draft. Distinct from Cancel, which retires the request for good and is available from any
    /// live state.
    /// </summary>
    public async Task<bool> RecallAsync(Guid requisitionId, string? reason, Guid recalledByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(requisitionId);

        if (entity.Status != StaffRequisitionStatus.Submitted && entity.Status != StaffRequisitionStatus.UnderReview)
            throw new InvalidOperationException("Only a requisition still awaiting approval can be recalled.");

        if (entity.RequestedById != recalledByUserId)
            throw new InvalidOperationException("Only the person who raised a requisition can recall it.");

        var fromStatus = entity.Status;

        // The requester-only rule above is the whole gate, so there is no permission branch here —
        // but the engine still has to be skipped when nothing is published, because
        // RecallWorkflowAsync answers "No active workflow found" and the recall would fail. Before
        // G-4.1 this branch could not be reached at all: submission went straight to Approved, and
        // an approved requisition is past recalling. Closing that opened this.
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            var workflowResult = await _workflowIntegrationService.RecallAsync(EntityType, entity.Id, _currentUserProvider.UserId);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(
                    workflowResult.ExecutionResult.Message ?? "Failed to recall the requisition.");
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyRecallOutcome(entity, _currentUserProvider.UserId, reason);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = recalledByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, entity.Status, recalledByUserId, reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition recalled: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> PutOnHoldAsync(HoldStaffRequisitionDto holdDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(holdDto.RequisitionId);

        var nonHoldableStatuses = new[]
        {
            StaffRequisitionStatus.Cancelled,
            StaffRequisitionStatus.Fulfilled,
            StaffRequisitionStatus.OnHold,
        };

        if (nonHoldableStatuses.Contains(entity.Status))
            throw new InvalidOperationException($"A requisition in '{entity.Status}' status cannot be put on hold.");

        var fromStatus = entity.Status;
        entity.Status = StaffRequisitionStatus.OnHold;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, StaffRequisitionStatus.OnHold, userId, holdDto.Reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition put on hold: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> CancelAsync(CancelStaffRequisitionDto cancelDto, Guid cancelledByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(cancelDto.RequisitionId);

        if (entity.Status == StaffRequisitionStatus.Cancelled || entity.Status == StaffRequisitionStatus.Fulfilled)
            throw new InvalidOperationException($"A requisition in '{entity.Status}' status cannot be cancelled.");

        var fromStatus = entity.Status;
        entity.Status = StaffRequisitionStatus.Cancelled;
        entity.CancelledById = cancelledByUserId;
        entity.CancelledDate = DateTime.UtcNow;
        entity.CancellationReason = cancelDto.CancellationReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = cancelledByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, StaffRequisitionStatus.Cancelled, cancelledByUserId, cancelDto.CancellationReason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition cancelled: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> FulfillAsync(FulfillStaffRequisitionDto fulfillDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(fulfillDto.RequisitionId);

        if (entity.Status != StaffRequisitionStatus.Approved && entity.Status != StaffRequisitionStatus.PartiallyFulfilled)
            throw new InvalidOperationException("Only Approved or PartiallyFulfilled requisitions can be fulfilled.");

        if (fulfillDto.PositionsFilled < 1 || fulfillDto.PositionsFilled > entity.NumberOfPositions)
            throw new InvalidOperationException($"Positions filled must be between 1 and {entity.NumberOfPositions}.");

        // ── G-4.5 (2026-09-15): the other half of reconciling the two writers ──────────────────
        // This path sets an ABSOLUTE total from what a user types; ConfirmStartAsync derives one
        // from the confirmed hire records. They used to be additive, so a manual "2" followed by a
        // confirmed start read 3. They are now reconciled on the rule that **a confirmed hire is a
        // fact and a typed number is a claim**: the claim may exceed the facts (a seat filled by a
        // transfer or secondment that produced no hire record is real), but it may not contradict
        // them by going below. Saying "1 filled" when two people have started is not a correction,
        // it is a mistake, and it used to be silently accepted.
        var confirmedHires = await _unitOfWork.Repository<JobHireRecord>().GetQueryable()
            .Where(h => h.TenantId == entity.TenantId
                     && !h.IsDeleted
                     && h.Status == JobHireStatus.Active
                     && h.Application != null
                     && h.Application.JobVacancy != null
                     && h.Application.JobVacancy.StaffRequisitionId == entity.Id)
            .CountAsync(cancellationToken);

        if (fulfillDto.PositionsFilled < confirmedHires)
            throw new InvalidOperationException(
                $"{confirmedHires} hire(s) have already been confirmed against this requisition, so " +
                $"the number filled cannot be {fulfillDto.PositionsFilled}. Record {confirmedHires} or more.");

        var fromStatus = entity.Status;
        entity.PositionsFilled = fulfillDto.PositionsFilled;

        var fullyFilled = entity.PositionsFilled >= entity.NumberOfPositions;
        entity.Status = fullyFilled ? StaffRequisitionStatus.Fulfilled : StaffRequisitionStatus.PartiallyFulfilled;
        entity.IsFulfilled = fullyFilled;
        entity.FulfilledDate = fullyFilled ? (fulfillDto.FulfilledDate ?? DateTime.UtcNow) : null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, entity.Status, userId,
            $"Positions filled: {entity.PositionsFilled}/{entity.NumberOfPositions}", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition {Status}: {RequisitionNumber}", entity.Status, entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> LinkToVacancyAsync(LinkStaffRequisitionToVacancyDto linkDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(linkDto.RequisitionId);

        // The vacancy id arrived from the payload and was written straight onto the FK. An id from
        // another tenant linked silently; an unknown one reached the database and came back as a
        // foreign-key violation. Check it belongs here before writing it.
        var vacancyExists = await _unitOfWork.Repository<JobVacancy>().GetQueryable()
            .AnyAsync(v => v.Id == linkDto.JobVacancyId
                        && v.TenantId == entity.TenantId
                        && !v.IsDeleted, cancellationToken);

        if (!vacancyExists)
            throw new ArgumentException($"Job vacancy with ID '{linkDto.JobVacancyId}' not found.");

        entity.JobVacancyId = linkDto.JobVacancyId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition {RequisitionNumber} linked to vacancy {VacancyId}",
            entity.RequisitionNumber, linkDto.JobVacancyId);

        return true;
    }

    // ── Cost operations ───────────────────────────────────────────────────────

    public async Task<StaffRequisitionCostDto> AddCostAsync(CreateStaffRequisitionCostDto createDto, Guid tenantId, Guid recordedByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(createDto.RequisitionId);

        var entity = createDto.ToEntity(current, recordedByUserId);
        await ApplyCostMoneyAsync(entity, cancellationToken);
        await _costRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await WithEnvelopeAsync((await ReloadCostAsync(entity.Id, cancellationToken)).ToDto(), entity, false, cancellationToken);
    }

    /// <summary>
    /// Round 2b, R6 (decision D-5, Q-R5): the recruitment envelope is the linked budget's
    /// <c>RecruitmentBudget</c>, drawn down by the APPROVED costs of every requisition on that
    /// budget. Recording never refuses (a recorded cost is not signed); approving refuses under
    /// Block and warns under Warn. An envelope of 0 means "not set" and constrains nothing.
    /// Returns the approved total so far (excluding this cost), the envelope and the budget number,
    /// or null when the requisition is not linked to a live budget.
    /// </summary>
    private async Task<(decimal Approved, decimal Pending, decimal Envelope, string BudgetNumber, BudgetEnforcementMode Mode)?> EnvelopeAsync(
        StaffRequisitionCost cost, CancellationToken cancellationToken)
    {
        var requisition = await _unitOfWork.Repository<StaffRequisition>().GetQueryable().AsNoTracking()
            .Include(r => r.ManpowerBudgetLine).ThenInclude(l => l!.ManpowerBudget)
            .FirstOrDefaultAsync(r => r.Id == cost.RequisitionId, cancellationToken);
        var budget = requisition?.ManpowerBudgetLine?.ManpowerBudget;
        if (budget == null || budget.IsDeleted
            || (budget.Status != ManpowerBudgetStatus.Approved && budget.Status != ManpowerBudgetStatus.Active))
            return null;
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var others = await _unitOfWork.Repository<StaffRequisitionCost>().GetQueryable().AsNoTracking()
            .Where(c => c.TenantId == cost.TenantId && !c.IsDeleted && c.Id != cost.Id
                     && c.Requisition != null && !c.Requisition.IsDeleted
                     && c.Requisition.Status != StaffRequisitionStatus.Cancelled
                     && c.Requisition.Status != StaffRequisitionStatus.Rejected
                     && c.Requisition.ManpowerBudgetLine != null
                     && c.Requisition.ManpowerBudgetLine.ManpowerBudgetId == budget.Id
                     && c.Status != StaffRequisitionCostStatus.Rejected)
            .Select(c => new { c.Status, c.AmountBaseCurrency })
            .ToListAsync(cancellationToken);
        return (
            others.Where(c => c.Status == StaffRequisitionCostStatus.Approved).Sum(c => c.AmountBaseCurrency),
            others.Where(c => c.Status == StaffRequisitionCostStatus.Recorded).Sum(c => c.AmountBaseCurrency),
            budget.RecruitmentBudget,
            budget.BudgetNumber,
            settings.BudgetEnforcementMode);
    }

    /// <summary>Decorates a cost's write response with the budget it counts against and a warning when the envelope would be passed.</summary>
    private async Task<StaffRequisitionCostDto> WithEnvelopeAsync(StaffRequisitionCostDto dto, StaffRequisitionCost cost, bool approving, CancellationToken cancellationToken)
    {
        var env = await EnvelopeAsync(cost, cancellationToken);
        if (env is null) return dto;
        var (approved, pending, envelope, number, mode) = env.Value;
        dto.BudgetNumber = number;
        if (envelope <= 0 || mode == BudgetEnforcementMode.Off) return dto;
        var projected = approved + cost.AmountBaseCurrency + (approving ? 0 : pending);
        if (projected > envelope)
            dto.BudgetWarning = approving
                ? $"Approving this cost takes {number}'s recruitment spend to {projected:N2} against an envelope of {envelope:N2} ({approved:N2} already approved)."
                : $"With this cost, {number}'s recorded and approved recruitment spend would be {projected:N2} against an envelope of {envelope:N2} ({approved:N2} approved, {pending:N2} pending). Approval will be {(mode == BudgetEnforcementMode.Block ? "refused" : "warned")}.";
        return dto;
    }

    /// <summary>
    /// Round 2b, R7: the money on a cost comes from Finance's masters. The currency must be one
    /// Finance holds; the rate is Finance's for the cost date; the base-currency amount is stored
    /// so the total does not move with the rate. The payee is a Procurement supplier (name
    /// snapshotted) or, with none, a typed name. Refusals are <see cref="InvalidOperationException"/>
    /// → 422 on this controller.
    /// </summary>
    private async Task ApplyCostMoneyAsync(StaffRequisitionCost entity, CancellationToken cancellationToken)
    {
        await _currency.RequireKnownCurrencyAsync(entity.Currency, cancellationToken);
        entity.Currency = entity.Currency.Trim().ToUpperInvariant();
        entity.ExchangeRate = await _currency.GetRateToBaseAsync(entity.Currency, entity.CostDate, cancellationToken);
        entity.AmountBaseCurrency = Math.Round(entity.Amount * entity.ExchangeRate, 2);

        if (entity.SupplierId is { } supplierId)
        {
            var supplier = await _unitOfWork.Repository<Supplier>().GetQueryable().AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == supplierId && s.TenantId == entity.TenantId && !s.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("The supplier named does not exist in this organisation.");
            if (!supplier.IsActive)
                throw new InvalidOperationException($"Supplier {supplier.Name} is inactive; choose an active supplier or name the payee.");
            entity.PayeeName = supplier.Name;
        }
        else if (string.IsNullOrWhiteSpace(entity.PayeeName))
        {
            throw new InvalidOperationException("Say who was paid: choose a supplier, or name the payee.");
        }
    }

    private async Task<StaffRequisitionCost> ReloadCostAsync(Guid id, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<StaffRequisitionCost>().GetQueryable().AsNoTracking()
            .Include(c => c.Requisition).Include(c => c.RecordedBy).Include(c => c.Supplier).Include(c => c.ApprovedBy)
            .FirstAsync(c => c.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<StaffRequisitionCostDto> DecideCostAsync(Guid costId, bool approve, string? note, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCostAsync(costId);
        if (entity.Status != StaffRequisitionCostStatus.Recorded)
            throw new InvalidOperationException($"This cost is already {entity.Status}.");
        // Two-actor rule: the person who recorded a cost does not approve it.
        if (entity.RecordedById == actorEmployeeId)
            throw new UnauthorizedAccessException("The person who recorded a cost cannot approve or reject it.");
        // R6: the envelope is enforced at APPROVAL — the act that commits the money.
        if (approve && await EnvelopeAsync(entity, cancellationToken) is { } env
            && env.Envelope > 0 && env.Mode == BudgetEnforcementMode.Block
            && env.Approved + entity.AmountBaseCurrency > env.Envelope)
            throw new InvalidOperationException(
                $"Approving this cost would take {env.BudgetNumber}'s recruitment spend to {env.Approved + entity.AmountBaseCurrency:N2} against an envelope of {env.Envelope:N2} " +
                $"({env.Approved:N2} already approved). Budget enforcement is set to Block: revise the budget's recruitment envelope, or reject the cost.");

        entity.Status = approve ? StaffRequisitionCostStatus.Approved : StaffRequisitionCostStatus.Rejected;
        entity.ApprovedById = actorEmployeeId;
        entity.ApprovedOn = DateTime.UtcNow;
        entity.ApprovalNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _costRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var dto = (await ReloadCostAsync(entity.Id, cancellationToken)).ToDto();
        if (!approve) return dto;
        // After the save the cost is Approved, so "others" excludes it and the projection is exact.
        var after = await EnvelopeAsync(entity, cancellationToken);
        if (after is { } a)
        {
            dto.BudgetNumber = a.BudgetNumber;
            if (a.Envelope > 0 && a.Mode == BudgetEnforcementMode.Warn && a.Approved + entity.AmountBaseCurrency > a.Envelope)
            {
                dto.BudgetWarning = $"{a.BudgetNumber}'s approved recruitment spend is now {a.Approved + entity.AmountBaseCurrency:N2} against an envelope of {a.Envelope:N2}. Budget enforcement is set to Warn.";
                _logger.LogWarning("Recruitment cost {CostId} approved over the envelope of {Budget}: {Warning}", entity.Id, a.BudgetNumber, dto.BudgetWarning);
            }
        }
        return dto;
    }

    public async Task<IEnumerable<StaffRequisitionCostDto>> GetCostsAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _costRepository.GetByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<decimal> GetTotalCostAsync(Guid requisitionId, CancellationToken cancellationToken = default)
        => await GetTotalCostAsync(requisitionId, null, cancellationToken);

    /// <inheritdoc />
    public async Task<decimal> GetTotalCostAsync(Guid requisitionId, StaffRequisitionCostStatus? status, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _costRepository.GetByRequisitionIdAsync(requisitionId);
        // The STORED base-currency figure (R7), not amount × a rate that may have moved since.
        return entities.Where(e => e.TenantId == tenantId && (status == null || e.Status == status.Value))
            .Sum(e => e.AmountBaseCurrency);
    }

    public async Task<IEnumerable<StaffRequisitionCostDto>> GetCostsByCategoryAsync(Guid requisitionId, StaffRequisitionCostCategory category, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _costRepository.GetByRequisitionAndCategoryAsync(requisitionId, category);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffRequisitionCostDto> UpdateCostAsync(UpdateStaffRequisitionCostDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCostAsync(updateDto.Id);

        if (entity.Status == StaffRequisitionCostStatus.Approved)
        {
            // An approved cost is what HR signed; only the voucher and the note may follow it.
            var moneyChanged = updateDto.Amount != entity.Amount
                || !string.Equals(updateDto.Currency?.Trim(), entity.Currency, StringComparison.OrdinalIgnoreCase)
                || updateDto.Category != entity.Category
                || updateDto.Purpose != entity.Purpose
                || updateDto.SupplierId != entity.SupplierId
                || (updateDto.CostDate.HasValue && updateDto.CostDate.Value != entity.CostDate);
            if (moneyChanged)
                throw new InvalidOperationException(
                    "This cost has been approved; its amount, currency, date, category, purpose and payee are fixed. Only the payment voucher and the note can be changed.");
            entity.PaymentVoucherNumber = updateDto.PaymentVoucherNumber;
            entity.Description = updateDto.Description;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = updatedByUserId.ToString();
        }
        else
        {
            entity.UpdateEntity(updateDto, updatedByUserId);
            await ApplyCostMoneyAsync(entity, cancellationToken);
        }

        await _costRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await WithEnvelopeAsync((await ReloadCostAsync(entity.Id, cancellationToken)).ToDto(), entity, false, cancellationToken);
    }

    public async Task<bool> DeleteCostAsync(Guid costId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCostAsync(costId);

        await _costRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Attachment operations ─────────────────────────────────────────────────

    /// <summary>
    /// Records an attachment against a requisition. The file itself has already been scanned and
    /// registered by the controlled-upload gate in the controller — this only writes the row, from
    /// the stored document's own metadata rather than anything the caller typed.
    /// </summary>
    public async Task<StaffRequisitionAttachmentDto> AddAttachmentAsync(
        Guid requisitionId,
        Guid uploadedById,
        string fileName,
        long fileSize,
        string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null)
    {
        var requisition = await GetOwnedAsync(requisitionId);

        var entity = new StaffRequisitionAttachment
        {
            TenantId           = requisition.TenantId,
            RequisitionId      = requisitionId,
            FileName           = fileName,
            // The stored file is addressed by its upload/DMS ids, not by a path the client chose.
            FilePath           = string.Empty,
            FileSizeBytes      = fileSize,
            Description        = description,
            UploadDate         = DateTime.UtcNow,
            UploadedById       = uploadedById,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId   = documentRecordId,
            DocumentVersionId  = documentVersionId,
            CreatedBy          = uploadedById.ToString(),
        };

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read so the response carries the uploader's name rather than a null navigation.
        var reloaded = (await _attachmentRepository.GetByRequisitionIdAsync(requisitionId))
            .FirstOrDefault(a => a.Id == entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<StaffRequisitionAttachmentDto>> GetAttachmentsAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.GetByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffRequisitionAttachmentDto>> GetAttachmentsByUploaderAsync(Guid uploadedByUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.GetByUploadedByAsync(uploadedByUserId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAttachmentAsync(attachmentId);

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Comment operations ────────────────────────────────────────────────────

    public async Task<StaffRequisitionCommentDto> AddCommentAsync(CreateStaffRequisitionCommentDto createDto, Guid tenantId, Guid authorId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(createDto.RequisitionId);

        var entity = createDto.ToEntity(current, authorId);
        await _commentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentsAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _commentRepository.GetThreadedByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffRequisitionCommentDto>> GetAllCommentsAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _commentRepository.GetAllByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentsByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _commentRepository.GetByAuthorAsync(authorId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentRepliesAsync(Guid parentCommentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _commentRepository.GetRepliesAsync(parentCommentId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffRequisitionCommentDto> UpdateCommentAsync(UpdateStaffRequisitionCommentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommentAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _commentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommentAsync(commentId);

        await _commentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── History (read-only) ───────────────────────────────────────────────────

    public async Task<IEnumerable<StaffRequisitionHistoryDto>> GetHistoryAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _historyRepository.GetByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffRequisitionHistoryDto?> GetLatestHistoryAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entity = await _historyRepository.GetLatestAsync(requisitionId);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    // ── Budget-aware requisitions ───────────────────────────────────────────────

    public async Task<RequisitionBudgetCheckDto> CheckBudgetAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(requisitionId);

        return await BuildBudgetCheckAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Builds the budget-check result for a requisition. Resolves the fiscal year from the
    /// requisition's desired start date, finds the matching <see cref="ManpowerBudgetLine"/> for the
    /// position, and compares the projected headcount against the approved planned count.
    /// </summary>
    /// <summary>
    /// FR-HR-136: verify the position against the approved establishment before a vacancy may be
    /// approved. Returns null when there is nothing to say.
    /// </summary>
    /// <remarks>
    /// <para><b>How this differs from the budget check next to it</b>, which is not obvious and
    /// matters: the budget check reads <c>ManpowerBudgetLine.CurrentFilled</c> — a number someone
    /// typed when they wrote the budget, possibly in January. This one counts the employees
    /// <b>actually in the post right now</b>. A budget written in January and a requisition raised
    /// in November will disagree, and when they do, the live count is the true one.</para>
    ///
    /// <para>⚠ <b>Only positions with an authorised establishment are constrained.</b>
    /// <c>EstablishmentApprovedOn == null</c> means nobody has ever approved a headcount for the
    /// post, and <c>ExpectedHeadcount</c> is then just its default of 1 — which 132 of 146 live
    /// positions still carry. Enforcing against that would refuse very nearly every requisition,
    /// and a rule that refuses everything is one people route around rather than obey. This is the
    /// same shape as the budget check's "no approved budget line" branch, and it is what allows the
    /// mode to default to Block.</para>
    /// </remarks>
    private async Task<string?> CheckEstablishmentAsync(
        StaffRequisition entity, bool blockingOnly, CancellationToken cancellationToken)
    {
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var mode = settings.EstablishmentEnforcementMode;
        if (mode == BudgetEnforcementMode.Off) return null;
        if (blockingOnly && mode != BudgetEnforcementMode.Block) return null;
        // One computation for the refusal and the panel (R5).
        var est = await BuildEstablishmentCheckAsync(entity, settings, cancellationToken);
        return est.WouldExceed ? est.Message : null;
    }

    private async Task<RequisitionBudgetCheckDto> BuildBudgetCheckAsync(StaffRequisition entity, CancellationToken cancellationToken)
    {
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var mode = settings.BudgetEnforcementMode;

        // Round 2b, R5: the policy's fiscal year, not the calendar year (§ 3 defect 3).
        var referenceDate = entity.DesiredStartDate != default ? entity.DesiredStartDate : entity.RequestDate;
        var fiscalYear = HrFiscalYear.For(referenceDate, settings);

        var result = new RequisitionBudgetCheckDto
        {
            Mode               = mode,
            FiscalYear         = fiscalYear,
            RequestedPositions = entity.NumberOfPositions,
            Establishment      = await BuildEstablishmentCheckAsync(entity, settings, cancellationToken),
        };

        // The line: the one the requisition is LINKED to (R5), else the old match by position and
        // fiscal year. Only an APPROVED or ACTIVE budget counts — a Draft authorises nothing
        // (area 17 slice 7), and a linked budget since rejected or withdrawn is reported by name
        // rather than silently treated as absent (Q-R4).
        ManpowerBudgetLine? line = null;
        if (entity.ManpowerBudgetLineId is { } linkedId)
        {
            var linked = await _unitOfWork.Repository<ManpowerBudgetLine>().GetQueryable().AsNoTracking()
                .Include(l => l.ManpowerBudget)
                .FirstOrDefaultAsync(l => l.Id == linkedId && l.TenantId == entity.TenantId, cancellationToken);
            if (linked?.ManpowerBudget != null)
            {
                result.LinkedLineId = linked.Id;
                result.LinkedBudgetId = linked.ManpowerBudgetId;
                result.LinkedBudgetNumber = linked.ManpowerBudget.BudgetNumber;
                result.LinkedBudgetStatus = linked.ManpowerBudget.Status.ToString();
                var live = !linked.ManpowerBudget.IsDeleted
                        && (linked.ManpowerBudget.Status == ManpowerBudgetStatus.Approved
                         || linked.ManpowerBudget.Status == ManpowerBudgetStatus.Active);
                if (live) { line = linked; result.IsLinked = true; }
            }
        }
        line ??= await _unitOfWork.Repository<ManpowerBudgetLine>().GetQueryable().AsNoTracking()
            .Include(l => l.ManpowerBudget)
            .Where(l => l.TenantId == entity.TenantId
                     && l.PositionId == entity.PositionId
                     && !l.IsDeleted
                     && l.ManpowerBudget != null
                     && !l.ManpowerBudget.IsDeleted
                     && l.ManpowerBudget.FiscalYear == fiscalYear
                     && (l.ManpowerBudget.Status == ManpowerBudgetStatus.Approved
                      || l.ManpowerBudget.Status == ManpowerBudgetStatus.Active))
            .OrderByDescending(l => l.ManpowerBudget.Status == ManpowerBudgetStatus.Active)
            .FirstOrDefaultAsync(cancellationToken);

        if (line is null)
        {
            result.HasBudgetLine = false;
            result.ProjectedHeadcount = entity.NumberOfPositions;
            result.ExceptionRequired = mode != BudgetEnforcementMode.Off;
            result.ExceptionReason = result.LinkedBudgetNumber != null
                ? $"Budget {result.LinkedBudgetNumber} is {result.LinkedBudgetStatus}, so it no longer authorises anything."
                : "No approved manpower budget line covers this position for the fiscal year.";
            // D-4: under Block a requisition not drawn from an approved budget is refused outright.
            result.WouldBlock = mode == BudgetEnforcementMode.Block;
            result.Message = mode switch
            {
                BudgetEnforcementMode.Off => "Budget enforcement is turned off.",
                BudgetEnforcementMode.Block =>
                    $"No approved manpower budget line covers this position in {fiscalYear}, and budget enforcement is set to Block: " +
                    "a requisition must be raised against an approved budget line. Plan a budget from the establishment, or ask for the setting to be relaxed.",
                _ => $"No approved manpower budget line covers this position in {fiscalYear}; the requisition is not budget-constrained, but the exception must be justified.",
            };
            return result;
        }

        // D-8: what the line budgets for, and what other requisitions have already asked of it.
        var budgeted = line.PlannedNewPositions > 0 ? line.PlannedNewPositions : Math.Max(0, line.PlannedCount - line.CurrentFilled);
        var drawdown = await DrawdownAsync(line.Id, entity.Id, cancellationToken);

        result.HasBudgetLine = true;
        result.PlannedCount = line.PlannedCount;
        result.CurrentFilled = line.CurrentFilled;
        result.BudgetedNewPosts = budgeted;
        result.Drawdown = drawdown;
        result.Remaining = budgeted - drawdown;
        result.ProjectedHeadcount = line.CurrentFilled + drawdown + entity.NumberOfPositions;
        if (!result.IsLinked)
        {
            // Matched, not linked: the form can offer this line. Naming it is what makes the
            // requisition budgeted; until then the exception rule applies.
            result.LinkedLineId ??= line.Id;
            result.LinkedBudgetId ??= line.ManpowerBudgetId;
            result.LinkedBudgetNumber ??= line.ManpowerBudget?.BudgetNumber;
            result.LinkedBudgetStatus ??= line.ManpowerBudget?.Status.ToString();
            result.ExceptionRequired = mode != BudgetEnforcementMode.Off;
            result.ExceptionReason = $"Budget {line.ManpowerBudget?.BudgetNumber} covers this position but the requisition is not raised against it.";
        }
        else if (result.Establishment is { IsEstablished: true, Gap: <= 0 } est && mode != BudgetEnforcementMode.Off)
        {
            result.ExceptionRequired = true;
            result.ExceptionReason = $"The post is established for {est.ExpectedHeadcount} and {est.Filled} are in it — no establishment gap.";
        }
        result.IsOverBudget = mode != BudgetEnforcementMode.Off && drawdown + entity.NumberOfPositions > budgeted;
        result.WouldBlock = mode == BudgetEnforcementMode.Block && (result.IsOverBudget || !result.IsLinked);

        if (mode == BudgetEnforcementMode.Off)
        {
            result.Message = "Budget enforcement is turned off.";
        }
        else if (result.IsOverBudget)
        {
            result.Message =
                $"This requisition would take the position to {result.ProjectedHeadcount} filled " +
                $"({line.CurrentFilled} filled + {drawdown} already requested + {entity.NumberOfPositions} requested) against " +
                $"{budgeted} new post(s) budgeted for {fiscalYear} on {line.ManpowerBudget?.BudgetNumber} (planned headcount {line.PlannedCount})." +
                (result.WouldBlock
                    ? " Budget enforcement is set to Block, so it cannot be submitted or approved until the budget is revised."
                    : " Budget enforcement is set to Warn — proceed with caution.");
        }
        else if (!result.IsLinked && mode == BudgetEnforcementMode.Block)
        {
            result.Message =
                $"Budget {line.ManpowerBudget?.BudgetNumber} covers this position in {fiscalYear} but the requisition is not raised against it, " +
                "and budget enforcement is set to Block: choose the budget line on the requisition.";
        }
        else
        {
            result.Message =
                $"Within budget: {drawdown + entity.NumberOfPositions} of {budgeted} new post(s) budgeted for {fiscalYear}" +
                (result.IsLinked ? $" on {line.ManpowerBudget?.BudgetNumber}." : $" on {line.ManpowerBudget?.BudgetNumber} — not yet raised against it.");
        }

        return result;
    }

    /// <summary>D-8: posts on OTHER live requisitions drawing down from the same line.</summary>
    private Task<int> DrawdownAsync(Guid lineId, Guid? excludeRequisitionId, CancellationToken cancellationToken)
        => _unitOfWork.Repository<StaffRequisition>().GetQueryable().AsNoTracking()
            .Where(r => r.ManpowerBudgetLineId == lineId && !r.IsDeleted
                     && r.Id != excludeRequisitionId
                     && r.Status != StaffRequisitionStatus.Cancelled
                     && r.Status != StaffRequisitionStatus.Rejected)
            .SumAsync(r => (int?)r.NumberOfPositions, cancellationToken)
            .ContinueWith(t => t.Result ?? 0, cancellationToken);

    /// <summary>
    /// The establishment side of the check, as a DTO (R5): the numbers <see cref="CheckEstablishmentAsync"/>
    /// refuses on, now visible on the panel before submit.
    /// </summary>
    private async Task<RequisitionEstablishmentCheckDto> BuildEstablishmentCheckAsync(
        StaffRequisition entity, CompanyHrPolicySettings settings, CancellationToken cancellationToken)
    {
        var mode = settings.EstablishmentEnforcementMode;
        var position = await _unitOfWork.Repository<EmployeePosition>().GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == entity.PositionId && p.TenantId == entity.TenantId, cancellationToken);
        // ⚠ G-4.4 (2026-09-15): this counted `e.IsActive` ONLY, ignoring StaffStatus entirely —
        // a third definition of "how many people are in this post", alongside the establishment
        // grid's and the vacancy mutation's. A terminated employee whose IsActive flag was never
        // cleared counted as filled here but not on the establishment screen, so the two screens
        // could disagree about whether a gap existed — and this is the count that REFUSES a
        // submission, so the disagreement had teeth. All three now use the shared predicate.
        var occupied = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == entity.TenantId && e.PositionId == entity.PositionId)
            .Where(HrServingEmployees.Predicate)
            .CountAsync(cancellationToken);

        var dto = new RequisitionEstablishmentCheckDto { Mode = mode, Filled = occupied };
        if (position?.EstablishmentApprovedOn == null)
        {
            dto.IsEstablished = false;
            dto.Message = "Nobody has established this post, so no headcount rule applies to it.";
            return dto;
        }
        dto.IsEstablished = true;
        dto.ExpectedHeadcount = position.ExpectedHeadcount;
        dto.Gap = Math.Max(0, position.ExpectedHeadcount - occupied);
        if (position.EstablishmentSourceBudgetId is { } sid)
            dto.SourceBudgetNumber = await _unitOfWork.Repository<ManpowerBudget>().GetQueryable().AsNoTracking()
                .Where(b => b.Id == sid).Select(b => b.BudgetNumber).FirstOrDefaultAsync(cancellationToken);
        var projected = occupied + entity.NumberOfPositions;
        dto.WouldExceed = mode != BudgetEnforcementMode.Off && projected > position.ExpectedHeadcount;
        dto.WouldBlock = dto.WouldExceed && mode == BudgetEnforcementMode.Block;
        dto.Message = dto.WouldExceed
            ? $"{position.Title} is established for {position.ExpectedHeadcount} post(s) and {occupied} are filled. This requisition would take it to {projected}, outside the establishment approved on {position.EstablishmentApprovedOn:dd MMM yyyy}."
            : $"{position.Title} is established for {position.ExpectedHeadcount} post(s); {occupied} filled, {dto.Gap} to fill.";
        return dto;
    }

    /// <summary>
    /// Round 2b, R5: derives the budget link and the two derived columns. Null = not raised
    /// against a budget. A line must name this position, belong to this tenant, sit on an
    /// Approved/Active budget, and cover the desired start date's fiscal year.
    /// </summary>
    /// <remarks>
    /// ⚠ Refusals are <see cref="InvalidOperationException"/>, which this controller's rules filter
    /// answers as 422 — the same code its establishment and budget refusals use. An
    /// <see cref="ArgumentException"/> here would come back as 404, and a bad reference INSIDE a
    /// payload is a refusal, not a missing resource (the lane-B2 lesson, learned twice).
    /// </remarks>
    private async Task ApplyBudgetLinkAsync(StaffRequisition entity, Guid? lineId, CancellationToken cancellationToken)
    {
        if (lineId is null)
        {
            entity.ManpowerBudgetLineId = null;
            entity.IsBudgeted = false;
            entity.BudgetCode = null;
            return;
        }
        var line = await _unitOfWork.Repository<ManpowerBudgetLine>().GetQueryable().AsNoTracking()
            .Include(l => l.ManpowerBudget)
            .FirstOrDefaultAsync(l => l.Id == lineId.Value && l.TenantId == entity.TenantId && !l.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The manpower budget line named does not exist in this organisation.");
        if (line.PositionId != entity.PositionId)
            throw new InvalidOperationException("The manpower budget line named is for a different position than the requisition.");
        var budget = line.ManpowerBudget ?? throw new InvalidOperationException("The manpower budget line named belongs to no budget.");
        if (budget.IsDeleted || (budget.Status != ManpowerBudgetStatus.Approved && budget.Status != ManpowerBudgetStatus.Active))
            throw new InvalidOperationException($"Budget {budget.BudgetNumber} is {budget.Status}; only an Approved budget can be drawn down from.");
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var referenceDate = entity.DesiredStartDate != default ? entity.DesiredStartDate : entity.RequestDate;
        var fiscalYear = HrFiscalYear.For(referenceDate, settings);
        if (budget.FiscalYear != fiscalYear)
            throw new InvalidOperationException($"Budget {budget.BudgetNumber} is for {budget.FiscalYear}; the desired start date falls in fiscal year {fiscalYear}.");

        entity.ManpowerBudgetLineId = line.Id;
        entity.IsBudgeted = true;
        entity.BudgetCode = budget.BudgetNumber;
    }

    /// <summary>D-4: an exception must be written down where the check says one is required.</summary>
    private async Task RequireExceptionJustificationAsync(StaffRequisition entity, CancellationToken cancellationToken)
    {
        var check = await BuildBudgetCheckAsync(entity, cancellationToken);
        if (!check.ExceptionRequired || !string.IsNullOrWhiteSpace(entity.ExceptionJustification)) return;
        throw new InvalidOperationException(
            $"This requisition needs an exception justification before it can be submitted: {check.ExceptionReason} " +
            "Say why the post should be recruited for anyway.");
    }

    /// <summary>D-2: the establishment as the approver will be asked to decide against it.</summary>
    private async Task StampEstablishmentSnapshotAsync(StaffRequisition entity, CancellationToken cancellationToken)
    {
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var est = await BuildEstablishmentCheckAsync(entity, settings, cancellationToken);
        entity.EstablishmentSnapshotOn = DateTime.UtcNow;
        entity.EstablishmentSnapshotIsEstablished = est.IsEstablished;
        entity.EstablishmentSnapshotExpected = est.ExpectedHeadcount;
        entity.EstablishmentSnapshotFilled = est.Filled;
        entity.EstablishmentSnapshotSourceBudgetNumber = est.SourceBudgetNumber;
    }

    /// <inheritdoc />
    public async Task<RequisitionBudgetCheckDto> PreviewBudgetCheckAsync(RequisitionBudgetCheckPreviewDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var probe = new StaffRequisition
        {
            Id = dto.ExcludeRequisitionId ?? Guid.Empty,
            TenantId = tenantId,
            PositionId = dto.PositionId,
            NumberOfPositions = dto.NumberOfPositions,
            DesiredStartDate = dto.DesiredStartDate ?? DateTime.UtcNow.AddDays(30),
            RequestDate = DateTime.UtcNow,
            ManpowerBudgetLineId = dto.ManpowerBudgetLineId,
        };
        return await BuildBudgetCheckAsync(probe, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StaffRequisitionDto> CreateFromBudgetLineAsync(Guid lineId, Guid requestedByUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var line = await _unitOfWork.Repository<ManpowerBudgetLine>().GetQueryable().AsNoTracking()
            .Include(l => l.ManpowerBudget).Include(l => l.Position)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId && !l.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The manpower budget line does not exist in this organisation.");
        var budget = line.ManpowerBudget!;
        if (budget.Status != ManpowerBudgetStatus.Approved && budget.Status != ManpowerBudgetStatus.Active)
            throw new InvalidOperationException($"Budget {budget.BudgetNumber} is {budget.Status}; requisitions are raised from an Approved budget.");
        var budgeted = line.PlannedNewPositions > 0 ? line.PlannedNewPositions : Math.Max(0, line.PlannedCount - line.CurrentFilled);
        var drawdown = await DrawdownAsync(line.Id, null, cancellationToken);
        var remaining = budgeted - drawdown;
        if (remaining <= 0)
            throw new InvalidOperationException(
                $"Budget {budget.BudgetNumber} authorises {budgeted} new post(s) for {line.Position?.Title} and {drawdown} are already requested; nothing is left to draw down.");

        var start = budget.PeriodStartDate > DateTime.UtcNow ? budget.PeriodStartDate : DateTime.UtcNow.AddDays(30);
        var dto = new CreateStaffRequisitionDto
        {
            PositionId = line.PositionId,
            OrganizationUnitId = line.Position?.OrganizationUnitId,
            OrganizationLevelId = line.Position?.OrganizationLevelId,
            RequisitionTitle = line.Position?.Title ?? "Requisition from budget",
            Type = StaffRequisitionType.NewPosition,
            // The budget line's Critical maps to the requisition's Urgent — the two ladders differ by one name.
            Priority = line.Priority switch { BudgetPriority.Critical => StaffRequisitionPriority.Urgent, BudgetPriority.High => StaffRequisitionPriority.High, BudgetPriority.Low => StaffRequisitionPriority.Low, _ => StaffRequisitionPriority.Medium },
            NumberOfPositions = remaining,
            DesiredStartDate = start,
            BusinessJustification = $"Raised from manpower budget {budget.BudgetNumber} ({budget.FiscalYear}), line for {line.Position?.Title}: {budgeted} new post(s) authorised, {drawdown} already requested.",
            Notes = string.Empty,
            AllowInternalCandidates = true,
            AllowExternalCandidates = true,
            ManpowerBudgetLineId = line.Id,
        };
        return await CreateAsync(dto, tenantId, requestedByUserId, cancellationToken);
    }

    /// <summary>
    /// Applies budget enforcement on a workflow transition. Throws when the mode is Block and the
    /// requisition is over budget; otherwise logs a warning (Warn) or does nothing (Off / within budget).
    /// </summary>
    /// <summary>Refuses the action when FR-HR-136 is set to Block and the post is full.</summary>
    private async Task EnforceEstablishmentAsync(
        StaffRequisition entity, string action, CancellationToken cancellationToken)
    {
        var breach = await CheckEstablishmentAsync(entity, blockingOnly: true, cancellationToken);
        if (breach == null) return;

        throw new InvalidOperationException(
            $"This requisition cannot be {action}: {breach} Revise the manpower budget for this "
            + "position, or reduce the number of posts requested.");
    }

    private async Task EnforceBudgetAsync(StaffRequisition entity, string action, CancellationToken cancellationToken)
    {
        var check = await BuildBudgetCheckAsync(entity, cancellationToken);

        if (check.WouldBlock)
            throw new InvalidOperationException(check.Message);

        if (check.IsOverBudget)
            _logger.LogWarning(
                "Requisition {RequisitionNumber} {Action} over budget: {Message}",
                entity.RequisitionNumber, action, check.Message);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Re-reads a requisition through the summary-nav query so a write response carries the same
    /// position title, org unit, location and requester name a subsequent GET would.
    ///
    /// <para>Mapping the tracked entity directly is what create and update used to do, and every
    /// one of those names came back blank: a freshly built entity has no navigations at all, and an
    /// edited one has whatever was loaded before its FKs changed — EF does not re-query them. The
    /// screen then showed an empty Position column until it happened to refetch.</para>
    /// </summary>
    private async Task<StaffRequisitionDto> ReloadDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var reloaded = await _requisitionRepository.GetWithSummaryNavAsync(id);
        if (reloaded == null)
            throw new ArgumentException($"Staff requisition with ID '{id}' not found.");
        return reloaded.ToDto();
    }

    private async Task<string> GenerateRequisitionNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var sequence = await _requisitionRepository.GetNextSequenceNumberAsync(tenantId);
        return $"REQ-{DateTime.UtcNow.Year}-{sequence:D5}";
    }

    private async Task RecordHistoryAsync(
        StaffRequisition entity,
        StaffRequisitionStatus fromStatus,
        StaffRequisitionStatus toStatus,
        Guid changedById,
        string? comments,
        CancellationToken cancellationToken)
    {
        var history = StaffRequisitionMappingExtensions.ToEntity(
            entity.TenantId,
            entity.Id,
            fromStatus,
            toStatus,
            changedById,
            comments);

        await _historyRepository.AddAsync(history);
    }
}

#endregion
