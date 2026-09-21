using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="IEmployeeSalaryChangeRequestService"/>
/// <remarks>
/// The tenth application of the workflow recipe (see <c>HR-WORKFLOW-ENGINE-INTEGRATION.md</c>).
/// Two things this one insists on: <see cref="RequireApprovalWasActuallySought"/> on submit — the
/// engine auto-approves when no definition is published, and on a raise that is the control
/// removed, not a convenience — and applying INSIDE the approve call rather than behind a button,
/// so an approved request cannot sit un-applied with nobody noticing.
/// </remarks>
public class EmployeeSalaryChangeRequestService : IEmployeeSalaryChangeRequestService
{
    public const string EntityType = "HrEmployeeSalaryChangeRequest";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _adapters;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IEmployeeService _employees;
    private readonly IPayrollMembershipService _payrollMembership;
    private readonly ILogger<EmployeeSalaryChangeRequestService> _logger;

    public EmployeeSalaryChangeRequestService(
        IUnitOfWork unitOfWork,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry adapters,
        ICurrentUserProvider currentUserProvider,
        IEmployeeService employees,
        IPayrollMembershipService payrollMembership,
        ILogger<EmployeeSalaryChangeRequestService> logger)
    {
        _unitOfWork = unitOfWork;
        _workflow = workflow;
        _adapters = adapters;
        _currentUserProvider = currentUserProvider;
        _employees = employees;
        _payrollMembership = payrollMembership;
        _logger = logger;
    }

    private IGenericRepository<EmployeeSalaryChangeRequest> Requests => _unitOfWork.Repository<EmployeeSalaryChangeRequest>();

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireUserId()
    {
        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("The current user could not be identified.");
        return userId;
    }

    private IQueryable<EmployeeSalaryChangeRequest> WithNames() =>
        Requests.GetQueryable()
            .Include(r => r.Employee)
            .Include(r => r.RequestedBy)
            .Include(r => r.ProposedGrade)
            .Include(r => r.ProposedLevel)
            .Include(r => r.ProposedNotch);

    // A request owned by another tenant is reported as missing rather than forbidden.
    private async Task<EmployeeSalaryChangeRequest> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return await Requests.GetQueryable().FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Salary change request '{id}' not found.");
    }

    private async Task<SalaryChangeRequestDto> ReloadDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await WithNames().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ArgumentException($"Salary change request '{id}' not found.");
        var currentGrade = entity.CurrentGradeId is { } g ? await _unitOfWork.Repository<SalaryGrade>().GetByIdAsync(g) : null;
        var currentNotch = entity.CurrentNotchId is { } n ? await _unitOfWork.Repository<SalaryNotch>().GetByIdAsync(n) : null;
        return ToDto(entity, currentGrade?.Code, currentNotch?.NotchNumber.ToString());
    }

    private static SalaryChangeRequestDto ToDto(EmployeeSalaryChangeRequest r, string? currentGradeCode = null, string? currentNotchNumber = null)
    {
        var notchAmount = r.ProposedNotch?.SalaryAmount;
        var targetBasis = r.ProposedPayBasis ?? r.CurrentPayBasis;
        var resulting = targetBasis == PayBasis.Negotiated ? r.ProposedAmount : (notchAmount ?? r.ProposedAmount);
        return new SalaryChangeRequestDto
        {
            Id = r.Id,
            EmployeeId = r.EmployeeId,
            EmployeeName = r.Employee?.FullName,
            EmployeeNumber = r.Employee?.EmployeeNumber,
            Kind = r.Kind,
            Status = r.Status,
            CurrentPayBasis = r.CurrentPayBasis,
            CurrentGradeId = r.CurrentGradeId,
            CurrentGradeCode = currentGradeCode,
            CurrentLevelId = r.CurrentLevelId,
            CurrentNotchId = r.CurrentNotchId,
            CurrentNotchNumber = currentNotchNumber,
            CurrentAmount = r.CurrentAmount,
            ProposedPayBasis = r.ProposedPayBasis,
            ProposedGradeId = r.ProposedGradeId,
            ProposedGradeCode = r.ProposedGrade?.Code,
            ProposedGradeName = r.ProposedGrade?.Name,
            ProposedLevelId = r.ProposedLevelId,
            ProposedLevelCode = r.ProposedLevel?.Code,
            ProposedNotchId = r.ProposedNotchId,
            ProposedNotchNumber = r.ProposedNotch?.NotchNumber.ToString(),
            ProposedNotchAmount = notchAmount,
            ProposedAmount = r.ProposedAmount,
            ProposedCurrencyCode = r.ProposedCurrencyCode,
            ResultingMonthlyBasicPay = resulting,
            EffectiveDate = r.EffectiveDate,
            Reason = r.Reason,
            RequestedById = r.RequestedById,
            RequestedByName = r.RequestedBy?.FullName,
            SourceProposalId = r.SourceProposalId,
            RejectionReason = r.RejectionReason,
            HrAppliedOn = r.HrAppliedOn,
            AppliedOn = r.AppliedOn,
            AppliedPlacementId = r.AppliedPlacementId,
            ApplyFailure = r.ApplyFailure,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
        };
    }

    // ── Reads ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SalaryChangeRequestDto>> GetAllAsync(Guid? employeeId = null, SalaryChangeRequestStatus? status = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = WithNames().Where(r => r.TenantId == tenantId);
        if (employeeId.HasValue) query = query.Where(r => r.EmployeeId == employeeId.Value);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        var items = await query.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync(cancellationToken);
        return items.Select(r => ToDto(r)).ToList();
    }

    public async Task<SalaryChangeRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return await ReloadDtoAsync(entity.Id, cancellationToken);
    }

    // ── Raise, amend, withdraw ────────────────────────────────────────────

    public async Task<SalaryChangeRequestDto> CreateAsync(CreateSalaryChangeRequestDto dto, Guid requestedByEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var employee = await RequireEmployeeAsync(dto.EmployeeId, tenantId, cancellationToken);

        var entity = new EmployeeSalaryChangeRequest
        {
            TenantId = tenantId,
            EmployeeId = employee.Id,
            RequestedById = requestedByEmployeeId,
            SourceProposalId = dto.SourceProposalId,
            Status = SalaryChangeRequestStatus.Draft,
        };
        await ApplyProposalAsync(entity, employee, dto.Kind, dto.ProposedPayBasis, dto.ProposedGradeId, dto.ProposedLevelId,
            dto.ProposedNotchId, dto.ProposedAmount, dto.ProposedCurrencyCode, dto.EffectiveDate, dto.Reason, cancellationToken);
        await SnapshotCurrentTermsAsync(entity, employee, cancellationToken);

        await Requests.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Salary change request {Id} ({Kind}) raised for {EmployeeId}.", entity.Id, entity.Kind, entity.EmployeeId);
        return await ReloadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SalaryChangeRequestDto> UpdateAsync(Guid id, UpdateSalaryChangeRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await GetOwnedAsync(id, cancellationToken);
        if (entity.Status is not (SalaryChangeRequestStatus.Draft or SalaryChangeRequestStatus.Rejected))
            throw new InvalidOperationException($"Only a draft or rejected request can be amended. This one is {entity.Status}.");

        var employee = await RequireEmployeeAsync(entity.EmployeeId, entity.TenantId, cancellationToken);
        await ApplyProposalAsync(entity, employee, dto.Kind, dto.ProposedPayBasis, dto.ProposedGradeId, dto.ProposedLevelId,
            dto.ProposedNotchId, dto.ProposedAmount, dto.ProposedCurrencyCode, dto.EffectiveDate, dto.Reason, cancellationToken);
        // The terms are re-read too: a request amended a month later should compare against today.
        await SnapshotCurrentTermsAsync(entity, employee, cancellationToken);

        await Requests.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReloadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        if (entity.Status != SalaryChangeRequestStatus.Draft)
            throw new InvalidOperationException("Only a draft request can be deleted. Recall it first if it is out for approval; a decided request is kept as the record of the decision.");
        await Requests.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Employee> RequireEmployeeAsync(Guid employeeId, Guid tenantId, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");
        return employee;
    }

    /// <summary>
    /// The shape rules, by kind — checked when raised and again when amended, never left for the
    /// approver to discover. A grade is resolved through the same resolver every placement uses, so
    /// a notch of another grade is refused here rather than at apply time.
    /// </summary>
    private async Task ApplyProposalAsync(
        EmployeeSalaryChangeRequest entity, Employee employee, SalaryChangeKind kind, PayBasis? proposedBasis,
        Guid? gradeId, Guid? levelId, Guid? notchId, decimal? amount, string? currencyCode,
        DateTime effectiveDate, string reason, CancellationToken cancellationToken)
    {
        if (!employee.IsOnPayroll)
            throw new InvalidOperationException(
                $"{employee.EmployeeNumber} is not on payroll, so their pay cannot be changed here. Put them on payroll first.");
        if (effectiveDate == default)
            throw new InvalidOperationException("An effective date is required.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Say why the pay is changing — the approver reads the reason before the figure.");

        PayBasis targetBasis;
        switch (kind)
        {
            case SalaryChangeKind.Placement:
                if (employee.PayBasis != PayBasis.SalaryScale)
                    throw new InvalidOperationException(
                        $"{employee.EmployeeNumber} is paid a negotiated amount. Raise a pay-basis switch to the scale (with the grade), or a negotiated amount.");
                if (gradeId is null)
                    throw new InvalidOperationException("A placement names the grade (and, usually, the notch) the person moves to.");
                targetBasis = PayBasis.SalaryScale;
                proposedBasis = null;
                amount = null;
                break;

            case SalaryChangeKind.NegotiatedAmount:
                if (employee.PayBasis != PayBasis.Negotiated)
                    throw new InvalidOperationException(
                        $"{employee.EmployeeNumber} is paid on the salary scale. Raise a placement, or a pay-basis switch to a negotiated amount.");
                if (amount is not > 0)
                    throw new InvalidOperationException("A negotiated amount needs the monthly figure.");
                targetBasis = PayBasis.Negotiated;
                proposedBasis = null;
                gradeId = levelId = notchId = null;
                break;

            case SalaryChangeKind.PayBasisSwitch:
                if (proposedBasis is null)
                    throw new InvalidOperationException("A pay-basis switch names the basis the person moves to.");
                if (proposedBasis == employee.PayBasis)
                    throw new InvalidOperationException(
                        $"{employee.EmployeeNumber} is already paid {(employee.PayBasis == PayBasis.SalaryScale ? "on the salary scale" : "a negotiated amount")}.");
                targetBasis = proposedBasis.Value;
                if (targetBasis == PayBasis.SalaryScale)
                {
                    if (gradeId is null)
                        throw new InvalidOperationException("Moving onto the scale needs the grade (and, usually, the notch) the person is placed on.");
                    amount = null;
                }
                else
                {
                    if (amount is not > 0)
                        throw new InvalidOperationException("Moving to a negotiated amount needs the monthly figure.");
                    gradeId = levelId = notchId = null;
                }
                break;

            default:
                throw new InvalidOperationException("Unknown kind of salary change.");
        }

        if (gradeId is { } g)
            levelId = await _employees.ResolvePlacementLevelAsync(g, levelId, notchId, cancellationToken);

        entity.Kind = kind;
        entity.ProposedPayBasis = proposedBasis;
        entity.ProposedGradeId = gradeId;
        entity.ProposedLevelId = levelId;
        entity.ProposedNotchId = notchId;
        entity.ProposedAmount = amount;
        entity.ProposedCurrencyCode = targetBasis == PayBasis.Negotiated
            ? (string.IsNullOrWhiteSpace(currencyCode) ? "GHS" : currencyCode.Trim().ToUpperInvariant())
            : null;
        entity.EffectiveDate = effectiveDate.Date;
        entity.Reason = reason.Trim();
    }

    private async Task SnapshotCurrentTermsAsync(EmployeeSalaryChangeRequest entity, Employee employee, CancellationToken cancellationToken)
    {
        entity.CurrentPayBasis = employee.PayBasis;
        var today = DateTime.UtcNow.Date;
        var inForce = await _unitOfWork.Repository<EmployeeSalaryAssignment>().GetQueryable()
            .Where(a => a.EmployeeId == employee.Id && !a.IsDeleted && a.WithdrawnAt == null
                     && a.EffectiveDate <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .OrderByDescending(a => a.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);
        entity.CurrentGradeId = inForce?.GradeId;
        entity.CurrentLevelId = inForce?.LevelId;
        entity.CurrentNotchId = inForce?.NotchId;
        try
        {
            var (basic, _) = await _payrollMembership.ResolveMonthlyBasicPayAsync(employee.Id, cancellationToken);
            entity.CurrentAmount = basic;
        }
        catch (Exception ex)
        {
            // The snapshot is context for the approver, not a rule; a payroll read that fails must not stop a raise.
            _logger.LogWarning(ex, "Could not resolve the current basic pay for {EmployeeId} while raising a salary change request.", employee.Id);
            entity.CurrentAmount = employee.Salary;
        }
    }

    // ── The engine ────────────────────────────────────────────────────────

    public async Task<SalaryChangeRequestDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        if (entity.Status == SalaryChangeRequestStatus.PendingApproval)
            throw new InvalidOperationException("This request is already awaiting approval.");
        if (entity.Status is not (SalaryChangeRequestStatus.Draft or SalaryChangeRequestStatus.Rejected))
            throw new InvalidOperationException($"A {entity.Status} request cannot be submitted.");

        var result = await _workflow.SubmitAsync(EntityType, id);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "Failed to start the salary change approval.");
        RequireApprovalWasActuallySought(result);

        _adapters.GetAdapter(EntityType).ApplySubmitOutcome(entity, result.Outcome, _currentUserProvider.UserId);
        entity.RejectionReason = null;

        await Requests.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Salary change request {Id} submitted; now {Status}.", id, entity.Status);
        return await ReloadDtoAsync(id, cancellationToken);
    }

    /// <summary>
    /// TRAP 5 of the recipe: with no published definition the engine answers "approved, nobody
    /// asked". On a pay change that is the control removed, not a shortcut — refused.
    /// </summary>
    private static void RequireApprovalWasActuallySought(WorkflowIntegrationResult result)
    {
        if (result.ApprovalRequired) return;
        throw new InvalidOperationException(
            "No approval workflow is configured for salary changes, so submitting would approve this request with nobody having been asked. "
            + "Ask an administrator to publish a workflow definition for HR Employee Salary Change Request first.");
    }

    /// <summary>
    /// The record-level rule, run BEFORE the engine's assignment check (the house pattern): the
    /// person who raised the request and the person it is about cannot decide it. The seeded
    /// default definition names roles (HR among them) and does not bar the initiator, so without
    /// this an HR officer approved the raise they had just raised — found by the lane-S harness.
    /// </summary>
    private static void RequireDisinterestedDecider(EmployeeSalaryChangeRequest entity, Guid? actorEmployeeId)
    {
        if (actorEmployeeId is not { } actor) return;
        if (actor == entity.RequestedById)
            throw new UnauthorizedAccessException("You raised this salary change request; someone else must decide it.");
        if (actor == entity.EmployeeId)
            throw new UnauthorizedAccessException("This request is about your own pay; someone else must decide it.");
    }

    public async Task<SalaryChangeRequestDto> ApproveAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        var userId = RequireUserId();
        RequireDisinterestedDecider(entity, actorEmployeeId);

        if (!await _workflow.CanUserApproveAsync(EntityType, id, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var result = await _workflow.ProcessApprovalAsync(EntityType, id, userId, "Approve");
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "Failed to process the approval.");

        _adapters.GetAdapter(EntityType).ApplyApprovalOutcome(entity, result.Outcome, userId);
        await Requests.UpdateAsync(entity);
        // The decision is saved BEFORE applying, so a failure in applying can never lose it.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (entity.Status == SalaryChangeRequestStatus.Approved)
        {
            await ApplyAsync(entity, userId, cancellationToken);
            await Requests.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Salary change request {Id} approval step processed; now {Status}.", id, entity.Status);
        return await ReloadDtoAsync(id, cancellationToken);
    }

    public async Task<SalaryChangeRequestDto> RejectAsync(Guid id, string? reason, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        var userId = RequireUserId();
        RequireDisinterestedDecider(entity, actorEmployeeId);

        if (!await _workflow.CanUserApproveAsync(EntityType, id, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var text = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();
        var result = await _workflow.ProcessApprovalAsync(EntityType, id, userId, "Reject", text);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "Failed to process the rejection.");

        _adapters.GetAdapter(EntityType).ApplyApprovalOutcome(entity, result.Outcome, userId, text);
        await Requests.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReloadDtoAsync(id, cancellationToken);
    }

    public async Task<SalaryChangeRequestDto> RecallAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        var userId = RequireUserId();
        if (entity.Status != SalaryChangeRequestStatus.PendingApproval)
            throw new InvalidOperationException("Only a request still awaiting approval can be recalled.");

        var result = await _workflow.RecallAsync(EntityType, id, userId);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "Failed to recall the request.");

        _adapters.GetAdapter(EntityType).ApplyRecallOutcome(entity, userId);
        await Requests.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReloadDtoAsync(id, cancellationToken);
    }

    // ── Applying ──────────────────────────────────────────────────────────

    public async Task<SalaryChangeRequestDto> RetryApplyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        if (entity.Status is not (SalaryChangeRequestStatus.Approved or SalaryChangeRequestStatus.AwaitingPayrollEntry))
            throw new InvalidOperationException(
                entity.Status == SalaryChangeRequestStatus.Applied
                    ? "This request has already been applied."
                    : $"Only an approved request can be applied. This one is {entity.Status}.");

        await ApplyAsync(entity, RequireUserId(), cancellationToken);
        await Requests.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReloadDtoAsync(id, cancellationToken);
    }

    /// <summary>
    /// HR's half through the same doors the Salary tab uses, with the approved authority; then
    /// payroll's half through payroll's own upsert. Each half is stamped as it goes through, so a
    /// retry never repeats what already took.
    /// </summary>
    private async Task ApplyAsync(EmployeeSalaryChangeRequest entity, Guid actorUserId, CancellationToken cancellationToken)
    {
        var employee = await RequireEmployeeAsync(entity.EmployeeId, entity.TenantId, cancellationToken);
        var targetBasis = entity.ProposedPayBasis ?? entity.CurrentPayBasis;

        if (entity.HrAppliedOn == null)
        {
            try
            {
                if (entity.Kind == SalaryChangeKind.PayBasisSwitch && entity.ProposedPayBasis is { } newBasis)
                {
                    await _employees.SetPayBasisAsync(entity.EmployeeId,
                        new SetEmployeePayBasisDto { PayBasis = newBasis, Note = entity.Reason },
                        cancellationToken, SalaryChangeAuthority.Approved);
                }

                if (targetBasis == PayBasis.SalaryScale && entity.ProposedGradeId is { } gradeId)
                {
                    var placed = await _employees.AssignSalaryAsync(new CreateEmployeeSalaryAssignmentDto
                    {
                        EmployeeId = entity.EmployeeId,
                        GradeId = gradeId,
                        LevelId = entity.ProposedLevelId,
                        NotchId = entity.ProposedNotchId,
                        EffectiveDate = entity.EffectiveDate,
                        AssignmentReason = Truncate($"Salary change request: {entity.Reason}", 200),
                    }, cancellationToken, SalaryChangeAuthority.Approved);
                    entity.AppliedPlacementId = placed.Id;
                }

                if (targetBasis == PayBasis.Negotiated && entity.ProposedAmount is { } amount)
                {
                    var repo = _unitOfWork.Repository<Employee>();
                    var tracked = await repo.GetByIdAsync(entity.EmployeeId) ?? employee;
                    tracked.Salary = amount;
                    tracked.UpdatedAt = DateTime.UtcNow;
                    await repo.UpdateAsync(tracked);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                entity.HrAppliedOn = DateTime.UtcNow;
                entity.ApplyFailure = null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                // The decision stands (Approved); what stopped it is on the record for the retry.
                entity.ApplyFailure = Truncate($"HR: {ex.Message}", 1000);
                _logger.LogWarning(ex, "Salary change request {Id} approved but HR's half could not be applied.", entity.Id);
                return;
            }
        }

        // Payroll's half: the monthly basic. A placement on a grade alone (no notch) has no
        // determinate figure, so payroll is left as it is and the request completes on HR's half.
        var payrollAmount = await ResolvePayrollAmountAsync(entity, targetBasis, cancellationToken);
        if (payrollAmount is { } figure)
        {
            var write = await _payrollMembership.UpdateMonthlyBasicAsync(
                entity.EmployeeId, figure, entity.ProposedCurrencyCode, entity.EffectiveDate, cancellationToken);
            if (!write.Success)
            {
                entity.Status = SalaryChangeRequestStatus.AwaitingPayrollEntry;
                entity.ApplyFailure = Truncate($"Payroll: {write.Failure}", 1000);
                _logger.LogWarning("Salary change request {Id}: HR's half applied, payroll's did not — {Failure}", entity.Id, write.Failure);
                return;
            }
        }

        entity.Status = SalaryChangeRequestStatus.Applied;
        entity.AppliedOn = DateTime.UtcNow;
        entity.AppliedByUserId = actorUserId;
        entity.ApplyFailure = null;
    }

    private async Task<decimal?> ResolvePayrollAmountAsync(EmployeeSalaryChangeRequest entity, PayBasis targetBasis, CancellationToken cancellationToken)
    {
        if (targetBasis == PayBasis.Negotiated) return entity.ProposedAmount;
        if (entity.ProposedNotchId is not { } notchId) return null;
        var notch = await _unitOfWork.Repository<SalaryNotch>().GetByIdAsync(notchId);
        return notch?.SalaryAmount;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
