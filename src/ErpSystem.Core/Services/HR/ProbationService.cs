using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class ProbationService : IProbationService
{
    private readonly IProbationPeriodRepository _probationRepository;
    private readonly IProbationReviewRepository _reviewRepository;
    private readonly IProbationExtensionRepository _extensionRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICompanyHrPolicySettingsService _policySettings;
    private readonly IProbationConfirmingAuthorityService _authorities;
    private readonly IWorkflowIntegrationService _workflowIntegration;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProbationService> _logger;

    public ProbationService(
        IProbationPeriodRepository probationRepository,
        IProbationReviewRepository reviewRepository,
        IProbationExtensionRepository extensionRepository,
        IEmployeeRepository employeeRepository,
        ICompanyHrPolicySettingsService policySettings,
        IProbationConfirmingAuthorityService authorities,
        IWorkflowIntegrationService workflowIntegration,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ProbationService> logger)
    {
        _probationRepository = probationRepository;
        _reviewRepository = reviewRepository;
        _extensionRepository = extensionRepository;
        _employeeRepository = employeeRepository;
        _policySettings = policySettings;
        _authorities = authorities;
        _workflowIntegration = workflowIntegration;
        _workflowAdapters = workflowAdapters;
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

    // ⚠ Entitlement for reviewer-scoped actions cannot come from a permission gate. A probation
    // review is conducted by the employee line manager (FRD FR-HR-032 routes the month-5 form to
    // the head), and a line manager holds no HR permission at all. Gating submit/complete on
    // HR.Probation.Write made them reachable only by HR - who the rule below then refuses - so the
    // action was reachable by nobody. Entitlement comes from the RECORD: are you the reviewer on
    // it? See slice 2 in plans/HR-Area-15b-Probation-Confirmation-Build-Plan.md.
    private bool IsHrActor()
        => _currentUserProvider.HasRole(Constants.Roles.SuperAdmin)
           || _currentUserProvider.HasRole(Constants.Roles.TenantAdmin)
           || _currentUserProvider.HasRole(Constants.Roles.Hr)
           || _currentUserProvider.HasRole(Constants.Roles.LegacyHrUser);

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    // A probation period owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    //
    // The exception type matters as much as the check. These threw ArgumentException, which
    // GlobalExceptionHandlingMiddleware maps to 400 AND replaces with "Invalid argument provided."
    // - so a missing record and a malformed payload were the same answer, and neither said
    // anything. ProbationWorkflowException carries its reason and its message through.
    private async Task<ProbationPeriod> GetOwnedProbationAsync(Guid id)
    {
        var entity = await _probationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw ProbationWorkflowException.NotFound($"Probation record '{id}' was not found.");
        return entity;
    }

    // As above, but with the navigations the DTO reads. Used by every path that RETURNS a
    // probation: the generic GetByIdAsync has no includes, so those responses came back with a
    // blank employee name and a review count of zero while the lists beside them were correct.
    private async Task<ProbationPeriod> GetOwnedProbationWithDetailsAsync(Guid id)
    {
        var entity = await _probationRepository.GetByIdWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw ProbationWorkflowException.NotFound($"Probation record '{id}' was not found.");
        return entity;
    }

    // A review owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<ProbationReview> GetOwnedReviewAsync(Guid id)
    {
        var entity = await _reviewRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw ProbationWorkflowException.NotFound($"Probation review '{id}' was not found.");
        return entity;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<ProbationPeriodDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationWithDetailsAsync(id);
        return entity.ToDto();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProbationPeriodSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _probationRepository.GetByEmployeeIdAsync(employeeId);
        // The repository already orders newest first; the active one, if any, belongs at the top.
        return entities
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Status == ProbationStatus.Active ? 0 : 1)
            .ThenByDescending(p => p.StartDate)
            .ToSummaryDtoList();
    }

    public async Task<PagedResult<ProbationPeriodSummaryDto>> GetPagedAsync(
        int page = 1, int pageSize = 25, ProbationStatus? status = null, Guid? employeeId = null,
        string? search = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;

        var (items, total) = await _probationRepository.GetPagedAsync(
            tenantId, page, pageSize, status, employeeId, search);

        return new PagedResult<ProbationPeriodSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ProbationPeriodDetailDto> GetWithReviewsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _probationRepository.GetWithReviewsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw ProbationWorkflowException.NotFound($"Probation record '{id}' was not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<ProbationPeriodSummaryDto>> GetByStatusAsync(ProbationStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _probationRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ProbationPeriodSummaryDto>> GetActiveProbationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _probationRepository.GetActiveProbationsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ProbationPeriodSummaryDto>> GetEndingWithinAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _probationRepository.GetEndingWithinAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── FR-HR-031: the length comes from the staff category ───────────────────

    /// <summary>Loads the employee with the position and staff level the policy is read from.</summary>
    private async Task<Employee> GetOwnedEmployeeAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (employee == null || employee.TenantId != GetTenantId())
            throw ProbationWorkflowException.NotFound($"Employee '{employeeId}' was not found.");
        return employee;
    }

    /// <summary>
    /// The employee, loaded so that <b>mutating it is a write</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>EmployeeRepository.BaseQuery</c> is <c>AsNoTracking</c> by default, so
    /// <c>GetByIdWithDetailsAsync</c> hands back a DETACHED employee: changing it does nothing at
    /// all, and calling <c>Update()</c> on it throws "another instance with the same key value is
    /// already being tracked" the moment anything else in the request has loaded the same employee
    /// (the probation reads include it). One cause, two symptoms - a silent no-op and an opaque
    /// 500. The generic <c>GetByIdAsync</c> tracks, so use it for anything that writes and keep the
    /// detail load for read-only work. See ef-tracked-graph-write-traps.
    /// </remarks>
    private async Task<Employee> GetTrackedEmployeeAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != GetTenantId())
            throw ProbationWorkflowException.NotFound($"Employee '{employeeId}' was not found.");
        return employee;
    }

    /// <inheritdoc />
    public async Task<ProbationPolicyDto> GetPolicyForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await GetOwnedEmployeeAsync(employeeId);
        var policy = BuildPolicy(employee, await _policySettings.GetAsync(cancellationToken));

        // Who will have to act on this probation, shown before it is even opened — so an
        // unconfigured tenant is visible at creation rather than a month later when the reminder
        // has nobody to go to.
        var authority = await _authorities.ResolveInternalAsync(GetTenantId(), employee, cancellationToken);
        policy.ConfirmingAuthorityEmployeeId = authority?.AuthorityEmployeeId;
        policy.ConfirmingAuthorityName = authority?.AuthorityEmployee?.FullName;
        policy.ConfirmingAuthorityScope = authority is null
            ? null
            : $"{authority.OrganizationUnit?.Name ?? "All units"} — {authority.StaffLevel?.Name ?? "all levels"}";

        return policy;
    }

    private static ProbationPolicyDto BuildPolicy(Employee employee, CompanyHrPolicySettingsDto settings)
    {
        // The position master already encodes FR-HR-031's rule — measured on the reference tenant
        // 2026-08-18: JNR 3 months (47 positions), SNR 6 (60), MGT 6 (16) — and it is maintained,
        // 123 of 146 positions carrying a value. So the category rule is READ from data rather than
        // hard-coded against level codes, which are tenant-specific and editable.
        var positionMonths = employee.Position?.ProbationPeriodMonths;
        var expected = positionMonths is > 0 ? positionMonths.Value : settings.DefaultProbationMonths;

        return new ProbationPolicyDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            EmployeeNumber = employee.EmployeeNumber,
            StaffLevelId = employee.Position?.StaffLevelId,
            StaffLevelName = employee.Position?.StaffLevel?.Name,
            StaffLevelCode = employee.Position?.StaffLevel?.Code,
            EmploymentType = employee.EmploymentType,
            ExpectedDurationMonths = expected,
            Source = positionMonths is > 0 ? "Position" : "PolicyDefault",
            PositionProbationMonths = positionMonths,
            PolicyDefaultMonths = settings.DefaultProbationMonths,
            // Only permanent staff are bound. A contract or temporary appointment is governed by
            // its own contract terms, which the FRD says explicitly, so a supplied length stands.
            IsEnforced = employee.EmploymentType == EmploymentType.Permanent,
            EndLeadDays = settings.ProbationEndLeadDays,
        };
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<ProbationPeriodDto> CreateAsync(CreateProbationPeriodDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var existing = await _probationRepository.GetByEmployeeIdAsync(createDto.EmployeeId);
        if (existing.Any(p => p.TenantId == tenantId && p.Status == ProbationStatus.Active))
            throw ProbationWorkflowException.Conflict(
                "This employee already has an active probation period. Confirm, extend or terminate it before opening another.");

        var employee = await GetOwnedEmployeeAsync(createDto.EmployeeId);
        var policy = BuildPolicy(employee, await _policySettings.GetAsync(cancellationToken));

        // FR-HR-031. Omitting the length is the normal case: it belongs to the staff category, not
        // to whoever is filling in the form. A supplied length that contradicts the category is
        // refused for permanent staff, and the refusal names both numbers so it can be acted on.
        var durationMonths = createDto.DurationMonths ?? policy.ExpectedDurationMonths;
        if (createDto.DurationMonths.HasValue
            && policy.IsEnforced
            && createDto.DurationMonths.Value != policy.ExpectedDurationMonths)
        {
            var level = string.IsNullOrWhiteSpace(policy.StaffLevelName) ? "their staff category" : policy.StaffLevelName;
            throw ProbationWorkflowException.Invalid(
                $"Probation for {level} runs {policy.ExpectedDurationMonths} months, not {createDto.DurationMonths.Value}. "
                + "Omit the duration to apply the category length, or correct the position's probation period.");
        }

        var entity = createDto.ToEntity(tenantId, createdByUserId, durationMonths);
        entity.Status = ProbationStatus.Active;

        await _probationRepository.AddAsync(entity);
        // The employee is on probation from this moment, and the record has to say so. The hire
        // path already stamps this; an HR-initiated probation did not.
        MarkEmployeeOnProbation(await GetTrackedEmployeeAsync(createDto.EmployeeId));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Probation record created for employee {EmployeeId}", createDto.EmployeeId);
        // Re-read so the response carries the employee name the caller just addressed, rather than
        // the just-saved row with its navigations unloaded.
        return (await GetOwnedProbationWithDetailsAsync(entity.Id)).ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(id);

        if (entity.Status != ProbationStatus.Active)
            throw ProbationWorkflowException.InvalidState(
                $"This probation is {entity.Status}, so it is part of the employment record and cannot be deleted. Only an active probation can be removed.");

        await _probationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── The employee record (slice 5) ─────────────────────────────────────────

    /// <summary>
    /// Puts the employee on probation on the employee record itself.
    /// </summary>
    /// <remarks>
    /// <para><b>The hire path already did this half</b> — <c>JobHireService</c> stamps
    /// <c>StaffStatus.Probation</c> when the offer carries probation months. What was missing was
    /// the same stamp on an <i>HR-initiated</i> probation, and, far more importantly, anything at
    /// all that <b>cleared</b> it again. See <see cref="MarkEmployeeConfirmed"/>.</para>
    ///
    /// <para>Measured on the reference tenant 2026-08-18: all 2,351 live employees sat at
    /// <c>StaffStatus.Active</c> and <b>zero</b> carried a <c>ConfirmationDate</c> — the field
    /// whose own comment reads "Date probation was passed". Those numbers reflect a workforce
    /// loaded by migration rather than hired through the pipeline, not a broken hire path.</para>
    /// </remarks>
    private static void MarkEmployeeOnProbation(Employee employee)
    {
        // Leave a leaver alone: only someone otherwise active moves onto probation.
        if (employee.StaffStatus is StaffStatus.Active or StaffStatus.Probation)
            employee.StaffStatus = StaffStatus.Probation;
    }

    /// <summary>Records on the employee that probation was passed (FR-HR-032).</summary>
    /// <remarks>
    /// ⚠ <b>This is the half that did not exist.</b> Hire sets <c>StaffStatus.Probation</c>;
    /// nothing anywhere cleared it, and nothing ever wrote <c>ConfirmationDate</c> — <c>grep</c>
    /// finds no other writer of that property in HR. So an employee who passed probation stayed
    /// flagged as on probation permanently.
    ///
    /// <para>That is not cosmetic. <c>Employee.IsOnProbation</c> is computed from
    /// <c>StaffStatus</c>, and <c>EmployeeBenefitEnrollmentService</c> refuses enrolment when
    /// <c>!policy.AvailableDuringProbation &amp;&amp; employee.IsOnProbation</c> — with a policy
    /// seeded at <c>AvailableDuringProbation = false</c>. A confirmed employee would have been
    /// refused that benefit for the rest of their career.</para>
    ///
    /// <para>⚠ Clearing the flag makes a dormant rule live in both directions, so the harness
    /// asserts the <b>benefit consequence</b> rather than the flag.</para>
    /// </remarks>
    private static void MarkEmployeeConfirmed(Employee employee, DateOnly confirmedOn)
    {
        if (employee.StaffStatus == StaffStatus.Probation)
            employee.StaffStatus = StaffStatus.Active;
        employee.ConfirmationDate = confirmedOn;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ProbationExtensionDto> ExtendAsync(
        Guid probationId, CreateProbationExtensionDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        // One path, one audit row. The old implementation lived here and wrote none.
        dto.ProbationPeriodId = probationId;
        return await RecordExtensionAsync(dto, GetTenantId(), actorEmployeeId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(Guid probationId, Guid confirmedByUserId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(probationId);

        // ⚠ The engine is the gate WHEN IT IS CONFIGURED, and only then. If a ProbationPeriod
        // definition is published, confirmation must come through the authority's approval, and a
        // direct call here is refused — otherwise the whole point of naming an authority is
        // bypassable by anyone holding Admin. If no definition is published, the direct path stays
        // open, because making the engine mandatory on an unconfigured tenant would leave the
        // feature dead rather than safe.
        if (entity.Status == ProbationStatus.ConfirmationApproved)
        {
            // The authority has decided; HR is recording it and issuing the letter.
        }
        else if (entity.Status != ProbationStatus.Active)
        {
            // One sentence per state, and each true of that state. "cannot be confirmed again" is
            // right for a Completed probation and wrong for a Terminated one, which was never
            // confirmed in the first place.
            throw ProbationWorkflowException.InvalidState(entity.Status switch
            {
                ProbationStatus.Completed =>
                    "This probation is already Completed and cannot be confirmed again.",
                ProbationStatus.PendingConfirmation =>
                    "This probation is with the confirming authority; it cannot be confirmed until they have approved it.",
                ProbationStatus.Terminated =>
                    "This probation was Terminated, so there is nothing to confirm.",
                _ => $"This probation is {entity.Status} and cannot be confirmed.",
            });
        }
        else if (await IsConfirmationWorkflowConfiguredAsync(cancellationToken))
        {
            throw ProbationWorkflowException.InvalidState(
                "Probation confirmation is routed through the confirming authority on this tenant. "
                + "Submit the probation for confirmation instead of confirming it directly.");
        }

        entity.Status = ProbationStatus.Completed;
        if (!string.IsNullOrWhiteSpace(notes)) entity.OutcomeNotes = notes;

        var employee = await GetTrackedEmployeeAsync(entity.EmployeeId);
        MarkEmployeeConfirmed(employee, DateOnly.FromDateTime(DateTime.UtcNow));

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Probation {ProbationId} confirmed by {UserId}; employee {EmployeeId} confirmed on {Date}",
            probationId, confirmedByUserId, employee.Id, employee.ConfirmationDate);
        return true;
    }

    public async Task<bool> TerminateAsync(TerminateProbationPeriodDto dto, Guid terminatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(dto.ProbationId);

        if (entity.Status != ProbationStatus.Active)
            throw ProbationWorkflowException.InvalidState(
                $"This probation is already {entity.Status} and cannot be terminated.");

        entity.Status = ProbationStatus.Terminated;
        entity.OutcomeNotes = dto.Notes;

        // The probation is over, so the employee is no longer ON probation. What happens to their
        // employment is the separation module's business, not this one's - area 15b records the
        // decision and hands off (see the scope line in the build plan). Leaving the flag set would
        // keep a benefit gate closed against someone this area has stopped tracking.
        var employee = await GetTrackedEmployeeAsync(entity.EmployeeId);
        if (employee.StaffStatus == StaffStatus.Probation)
            employee.StaffStatus = StaffStatus.Active;

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Probation {ProbationId} terminated", dto.ProbationId);
        return true;
    }

    // ── Confirmation on the workflow engine (slice 8b) ────────────────────────

    private const string WorkflowEntityType = "ProbationPeriod";

    /// <summary>
    /// Whether this tenant has a published probation-confirmation workflow.
    /// </summary>
    /// <remarks>
    /// Asked rather than assumed, so the same build serves a tenant that has configured the chain
    /// and one that has not. Treated as "not configured" if the engine cannot answer: refusing to
    /// confirm because a workflow lookup failed would be worse than allowing the direct path.
    /// </remarks>
    private async Task<bool> IsConfirmationWorkflowConfiguredAsync(CancellationToken cancellationToken)
    {
        try
        {
            // ⚠ Asked of the definition store directly: IWorkflowIntegrationService has no
            // "is anything published for this type?" method, and inventing one on a shared
            // interface for a single caller is not this slice's business.
            return await _unitOfWork.Repository<Entities.Workflow.WorkflowDefinition>().GetQueryable()
                .AnyAsync(d => d.TenantId == GetTenantId()
                            && !d.IsDeleted
                            && d.IsActive
                            && d.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published
                            && d.EntityType != null
                            && d.EntityType.Code == "PROBATION_PERIOD",
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not determine whether a probation confirmation workflow is published; allowing the direct path.");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ProbationPeriodDto> SubmitForConfirmationAsync(
        Guid probationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(probationId);

        if (entity.Status == ProbationStatus.PendingConfirmation)
            throw ProbationWorkflowException.InvalidState("This probation is already awaiting confirmation.");
        if (entity.Status != ProbationStatus.Active)
            throw ProbationWorkflowException.InvalidState(
                $"A {entity.Status} probation cannot be submitted for confirmation.");

        // ⚠ Refuse before troubling the engine if nobody would receive it. An approval step that
        // resolves to no approver publishes happily and can never be approved — the trap recorded
        // in workflow-engine-integration — and here we can see it coming.
        var employee = await GetOwnedEmployeeAsync(entity.EmployeeId);
        var authority = await _authorities.ResolveInternalAsync(GetTenantId(), employee, cancellationToken);
        if (authority is null)
            throw ProbationWorkflowException.InvalidState(
                "No confirming authority covers this employee, so there is nobody to send the confirmation to. "
                + "Set one under Administration → HR → Probation before submitting.");

        // Submitting must never confirm. With no published definition the engine returns Approved
        // and the adapter maps it to confirmed — making someone's employment permanent with nobody
        // having decided, which is exactly the outcome FR-HR-032's chain (system → head confirms →
        // HR issues the letter) exists to prevent. Defence in depth; a PROBATION_PERIOD definition
        // is seeded. See HrWorkflowFallbackAuthority.
        var (result, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegration, WorkflowEntityType, probationId);
        if (!result.ExecutionResult.Success)
            throw ProbationWorkflowException.InvalidState(
                result.ExecutionResult.Message ?? "Failed to start the probation confirmation workflow.");

        _workflowAdapters.GetAdapter(WorkflowEntityType)
            .ApplySubmitOutcome(entity, submitOutcome, _currentUserProvider.UserId);

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Probation {ProbationId} submitted for confirmation to authority {AuthorityId}",
            probationId, authority.AuthorityEmployeeId);

        return (await GetOwnedProbationWithDetailsAsync(probationId)).ToDto();
    }

    /// <inheritdoc />
    public async Task<ProbationPeriodDto> ApproveConfirmationAsync(
        Guid probationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(probationId);
        var userId = _currentUserProvider.UserId;

        // ⚠ HR.Probation.Approve is NOT granted to the HR desk — see HrStaffGrants. Confirming
        // probation decides whether employment becomes permanent, and the permission map warns
        // against making that "reachable by anyone HR-shaped". On an unseeded tenant this stalls
        // until SuperAdmin/TenantAdmin acts or a definition names the confirming authority, which
        // is the documented intent rather than an oversight.
        var approvalOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegration, _currentUserProvider, WorkflowEntityType, probationId, userId,
            "Approve", null, "confirm a probation", HrPermissions.ApproveProbation);

        _workflowAdapters.GetAdapter(WorkflowEntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, userId);

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetOwnedProbationWithDetailsAsync(probationId)).ToDto();
    }

    /// <inheritdoc />
    public async Task<ProbationPeriodDto> RejectConfirmationAsync(
        Guid probationId, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(probationId);
        var userId = _currentUserProvider.UserId;

        var rejectionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegration, _currentUserProvider, WorkflowEntityType, probationId, userId,
            "Reject", reason, "refuse a probation confirmation", HrPermissions.ApproveProbation);

        _workflowAdapters.GetAdapter(WorkflowEntityType)
            .ApplyApprovalOutcome(entity, rejectionOutcome, userId, reason);

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetOwnedProbationWithDetailsAsync(probationId)).ToDto();
    }

    /// <inheritdoc />
    public async Task<ProbationPeriodDto> RecallConfirmationAsync(
        Guid probationId, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(probationId);

        if (entity.Status != ProbationStatus.PendingConfirmation)
            throw ProbationWorkflowException.InvalidState(
                "Only a probation awaiting confirmation can be recalled.");

        // Skipped when nothing is published; the probation returns to Draft either way.
        await HrWorkflowFallbackAuthority.RecallAsync(
            _workflowIntegration, WorkflowEntityType, probationId, _currentUserProvider.UserId, reason);

        _workflowAdapters.GetAdapter(WorkflowEntityType)
            .ApplyRecallOutcome(entity, _currentUserProvider.UserId, reason);

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetOwnedProbationWithDetailsAsync(probationId)).ToDto();
    }

    // ── Reviews ───────────────────────────────────────────────────────────────

    public async Task<ProbationReviewDto> AddReviewAsync(CreateProbationReviewDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedProbationAsync(createDto.ProbationPeriodId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _reviewRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ProbationReviewDto>> GetReviewsAsync(Guid probationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedProbationAsync(probationId);
        var entities = await _reviewRepository.GetByProbationPeriodIdAsync(probationId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<ProbationReviewDto> UpdateReviewAsync(UpdateProbationReviewDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(updateDto.Id);

        // Rescheduling only. The assessment itself goes through SubmitReviewAsync, which is why
        // UpdateProbationReviewDto deliberately carries nothing else - it used to be handed the
        // full review payload and silently drop all of it behind a 200.
        if (entity.Status == ProbationReviewStatus.Completed)
            throw ProbationWorkflowException.InvalidState(
                "This review is completed and can no longer be rescheduled.");

        if (updateDto.ScheduledDate.HasValue) entity.ScheduledDate = updateDto.ScheduledDate.Value;
        if (updateDto.SecondReviewerId.HasValue) entity.SecondReviewerId = updateDto.SecondReviewerId;
        await _reviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    /// <inheritdoc />
    public async Task<ProbationReviewDto> SubmitReviewAsync(
        Guid reviewId, SubmitProbationReviewDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(reviewId);
        var probation = await GetOwnedProbationAsync(entity.ProbationPeriodId);

        if (probation.Status != ProbationStatus.Active)
            throw ProbationWorkflowException.InvalidState(
                $"This probation is {probation.Status}; its reviews can no longer be changed.");

        if (entity.Status == ProbationReviewStatus.Completed)
            throw ProbationWorkflowException.InvalidState(
                "This review has already been completed. Reopen it before recording a different assessment.");

        // The reviewer is the person the review was assigned to - the named reviewer or the second
        // reviewer. HR schedules reviews; it does not conduct them and does not sign them.
        if (entity.ReviewedById != actorEmployeeId && entity.SecondReviewerId != actorEmployeeId)
            throw new UnauthorizedAccessException(
                "Only the reviewer named on this review, or its second reviewer, may record its assessment.");

        // A recommendation is what the rest of the area acts on, so it is the one required field.
        if (dto.Recommendation is null)
            throw ProbationWorkflowException.Invalid(
                "A review must carry a recommendation: Confirm, Extend, Terminate or ContinueMonitoring.");

        if (dto.Recommendation == ProbationReviewRecommendation.Extend && dto.ProposedExtensionMonths is not > 0)
            throw ProbationWorkflowException.Invalid(
                "A recommendation to extend must say how many months are proposed.");

        entity.ActualDate = dto.ActualDate;
        entity.PerformanceRating = dto.PerformanceRating;
        entity.ConductRating = dto.ConductRating;
        entity.AttitudeRating = dto.AttitudeRating;
        entity.StrengthsObserved = dto.StrengthsObserved;
        entity.AreasForImprovement = dto.AreasForImprovement;
        entity.ReviewerComments = dto.ReviewerComments;
        entity.Recommendation = dto.Recommendation;
        entity.ProposedExtensionMonths = dto.Recommendation == ProbationReviewRecommendation.Extend
            ? dto.ProposedExtensionMonths
            : null;
        entity.SignedDocumentPath = dto.SignedDocumentPath;

        await _reviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Probation review {ReviewId} submitted by {ActorId} recommending {Recommendation}",
            reviewId, actorEmployeeId, dto.Recommendation);

        return (await GetOwnedReviewAsync(reviewId)).ToDto();
    }

    /// <inheritdoc />
    public async Task<ProbationReviewDto> AcknowledgeReviewAsync(
        Guid reviewId, AcknowledgeProbationReviewDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(reviewId);
        var probation = await GetOwnedProbationAsync(entity.ProbationPeriodId);

        // ⚠ The subject, and nobody else. Not HR, not the reviewer. Signing "I have seen this" in
        // someone else's name is the defect area 9 found in its own acknowledgement path.
        if (probation.EmployeeId != actorEmployeeId)
            throw new UnauthorizedAccessException(
                "Only the employee this probation review is about may acknowledge it.");

        if (entity.Recommendation is null)
            throw ProbationWorkflowException.InvalidState(
                "This review has not been conducted yet, so there is nothing to acknowledge.");

        if (entity.EmployeeAcknowledged)
            throw ProbationWorkflowException.InvalidState("You have already acknowledged this review.");

        entity.EmployeeAcknowledged = true;
        entity.EmployeeAcknowledgementDate = DateTime.UtcNow;   // server-stamped, never from the payload
        entity.EmployeeResponse = dto.EmployeeResponse;

        await _reviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Probation review {ReviewId} acknowledged by its subject {ActorId}", reviewId, actorEmployeeId);
        return (await GetOwnedReviewAsync(reviewId)).ToDto();
    }

    /// <inheritdoc />
    public async Task<ProbationReviewDto> HrApproveReviewAsync(
        Guid reviewId, ApproveProbationReviewDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(reviewId);

        if (entity.Status != ProbationReviewStatus.Completed)
            throw ProbationWorkflowException.InvalidState(
                "Only a completed review can be signed off. Record the assessment and complete it first.");

        if (entity.HrApproved)
            throw ProbationWorkflowException.InvalidState("This review has already been signed off.");

        entity.HrApproved = true;
        entity.HrApprovedById = actorEmployeeId;                // from the token, never the payload
        entity.HrApprovalDate = DateTime.UtcNow;                // server-stamped
        if (!string.IsNullOrWhiteSpace(dto.Comments))
            entity.ReviewerComments = string.IsNullOrWhiteSpace(entity.ReviewerComments)
                ? dto.Comments
                : $"{entity.ReviewerComments}\n\nHR: {dto.Comments}";

        await _reviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Probation review {ReviewId} signed off by {ActorId}", reviewId, actorEmployeeId);
        return (await GetOwnedReviewAsync(reviewId)).ToDto();
    }

    /// <inheritdoc />
    public async Task<bool> CompleteReviewAsync(Guid reviewId, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(reviewId);

        // The reviewer closes their own review; HR may close it on their behalf (a reviewer who
        // has left, a review conducted on paper). Everyone else is refused.
        if (!IsHrActor()
            && entity.ReviewedById != completedByUserId
            && entity.SecondReviewerId != completedByUserId)
            throw new UnauthorizedAccessException(
                "Only the reviewer named on this review, its second reviewer, or HR may complete it.");

        // ⚠ A review with no assessment is not a conducted review. Before slice 2 this method set
        // the status and nothing else, so an empty review completed happily and then read as
        // conducted forever - and the reminder queues stopped chasing it.
        if (entity.Recommendation is null)
            throw ProbationWorkflowException.InvalidState(
                "This review has not been conducted yet. Record the reviewer's assessment before completing it.");

        if (entity.Status == ProbationReviewStatus.Completed)
            throw ProbationWorkflowException.InvalidState("This review is already completed.");

        entity.Status = ProbationReviewStatus.Completed;
        entity.ActualDate ??= DateOnly.FromDateTime(DateTime.UtcNow);

        await _reviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public Task<IEnumerable<ProbationReviewDto>> GetMyReviewerQueueAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
        // The by-reviewer read already matches the named reviewer OR the second reviewer; this
        // simply removes the caller's need to know their own employee id.
        => GetReviewsByReviewerAsync(employeeId, cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<ProbationReviewDto>> GetMyReviewsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var probations = (await _probationRepository.GetByEmployeeIdAsync(employeeId))
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.Id)
            .ToHashSet();
        if (probations.Count == 0) return Array.Empty<ProbationReviewDto>();

        var reviews = new List<ProbationReviewDto>();
        foreach (var probationId in probations)
        {
            var rows = await _reviewRepository.GetByProbationPeriodIdAsync(probationId);
            reviews.AddRange(rows.Where(r => r.TenantId == tenantId).Select(r => r.ToDto()));
        }
        return reviews.OrderByDescending(r => r.ScheduledDate).ToList();
    }

    public async Task<IEnumerable<ProbationReviewDto>> GetReviewsByStatusAsync(ProbationReviewStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _reviewRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<ProbationReviewDto>> GetOverdueReviewsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _reviewRepository.GetOverdueReviewsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<ProbationReviewDto>> GetReviewsByReviewerAsync(Guid reviewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _reviewRepository.GetByReviewerIdAsync(reviewerEmployeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<ProbationExtensionDto> RecordExtensionAsync(CreateProbationExtensionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var probation = await GetOwnedProbationAsync(createDto.ProbationPeriodId);

        if (probation.Status != ProbationStatus.Active)
            throw ProbationWorkflowException.InvalidState(
                $"This probation is {probation.Status} and can no longer be extended.");

        var newEndDate = createDto.NewEndDate;
        if (newEndDate <= probation.CurrentEndDate)
            throw ProbationWorkflowException.Invalid(
                $"The new end date ({newEndDate:yyyy-MM-dd}) must be later than the current end date ({probation.CurrentEndDate:yyyy-MM-dd}).");

        // Verify extension months is consistent with the stated new date
        var computedMonths = ((newEndDate.Year - probation.CurrentEndDate.Year) * 12)
                           + newEndDate.Month - probation.CurrentEndDate.Month;
        if (computedMonths < 1)
            throw ProbationWorkflowException.Invalid(
                "An extension must run at least one full calendar month past the current end date.");

        var extension = new ProbationExtension
        {
            TenantId = tenantId,
            ProbationPeriodId = createDto.ProbationPeriodId,
            PreviousEndDate = probation.CurrentEndDate,
            NewEndDate = newEndDate,
            ExtensionMonths = createDto.ExtensionMonths,
            Reason = createDto.Reason,
            ExtendedById = createdByUserId,
            ExtendedDate = DateTime.UtcNow,
            Comments = createDto.Comments,
            CreatedBy = createdByUserId.ToString(),
        };

        // Advance the probation period's end date and increment extension counter
        probation.CurrentEndDate = newEndDate;
        probation.ExtensionCount++;

        await _extensionRepository.AddAsync(extension);
        await _probationRepository.UpdateAsync(probation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Probation {ProbationId} extended by {Months} month(s) to {NewEndDate} by {UserId}",
            probation.Id, createDto.ExtensionMonths, newEndDate, createdByUserId);

        return new ProbationExtensionDto
        {
            Id = extension.Id,
            CreatedAt = extension.CreatedAt,
            CreatedBy = extension.CreatedBy,
            ProbationPeriodId = extension.ProbationPeriodId,
            PreviousEndDate = extension.PreviousEndDate,
            NewEndDate = extension.NewEndDate,
            ExtensionMonths = extension.ExtensionMonths,
            Reason = extension.Reason,
            ExtendedById = extension.ExtendedById,
            ExtendedDate = extension.ExtendedDate,
            Comments = extension.Comments,
        };
    }

    public async Task<IEnumerable<ProbationExtensionDto>> GetExtensionsAsync(Guid probationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedProbationAsync(probationId);
        var extensions = await _extensionRepository.GetByProbationIdAsync(probationId);
        return extensions.Where(e => e.TenantId == tenantId).Select(e => new ProbationExtensionDto
        {
            Id = e.Id,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy ?? string.Empty,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy,
            ProbationPeriodId = e.ProbationPeriodId,
            PreviousEndDate = e.PreviousEndDate,
            NewEndDate = e.NewEndDate,
            ExtensionMonths = e.ExtensionMonths,
            Reason = e.Reason,
            ExtendedById = e.ExtendedById,
            ExtendedByName = e.ExtendedBy?.FullName,
            ExtendedDate = e.ExtendedDate,
            Comments = e.Comments,
        }).ToList();
    }
}
