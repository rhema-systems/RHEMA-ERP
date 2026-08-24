using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
// The exit register, for the one question this service asks of it: has a charge's balance already
// been taken at exit? See ChargesTakenAtExitAsync.
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

// ⚠ An ALIAS, not `using ErpSystem.Core.Interfaces.Finance`. That namespace declares its own
// `IAssetTransferService`, and so does `ErpSystem.Core.Interfaces.HR` — importing it whole is what
// made every mention of that name ambiguous in `AssetsServices.cs` (build plan §3.3). One type,
// named once.
using ICurrencyService = ErpSystem.Core.Interfaces.Finance.ICurrencyService;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Charging an employee for a company asset — <b>AST-3</b>, defect <b>D-d</b>, decision <b>D9</b>.
/// Area 16, slice 7.
/// </summary>
/// <remarks>
/// <para><b>What D-d actually was.</b> <c>EmployeeLiable</c>, <c>RepairCost</c> and
/// <c>ReplacementCost</c> have sat on <see cref="AssetAssignment"/> since the port, written by the
/// return path and read by nothing. Slice 4 made them coherent — they cannot be set on a return
/// that reports no damage — and the defect stayed open because coherent is not the same as used.
/// This service is their reader, and the reading is deliberately narrow: they establish that a
/// charge is <i>permissible</i> and they seed its default amount. What the employee is actually
/// asked to pay stays the employer's decision, recorded as one.</para>
///
/// <para><b>The order of the gates is the design.</b> A charge cannot go for approval until it has
/// been put to the employee and they have answered — or until somebody states, on the record, why
/// it is going anyway. Charging a person money on evidence they were never shown is the failure
/// area 9 built its discipline gate to prevent, and it is worth more here than a status column.</para>
///
/// <para><b>Where this stops.</b> It deducts nothing and posts nothing.
/// <see cref="GetPayrollDeductionLinesAsync"/> is a read-only projection payroll consumes;
/// <see cref="RecordRecoveryAsync"/> records what somebody else collected. See the payroll
/// ownership boundary, decision D2, and <c>docs/HR-FINANCE-INTEGRATION-BACKLOG.md</c>.</para>
/// </remarks>
public class AssetSurchargeService : IAssetSurchargeService
{
    private readonly IAssetSurchargeRepository _surchargeRepo;
    private readonly IAssetSurchargeRecoveryRepository _recoveryRepo;
    private readonly IAssetAssignmentRepository _assignmentRepo;
    private readonly ICurrencyService _currencies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;

    /// <summary>
    /// ⚠ <c>HrAssetSurcharge</c>, not <c>AssetSurcharge</c>. The workflow entity-type keys are a
    /// flat namespace shared with Finance's fixed assets and Inventory, and build plan §3.3 records
    /// what happens when an HR key collides with one of theirs. The prefix is not decoration.
    /// </summary>
    private const string EntityType = "HrAssetSurcharge";

    /// <summary>The states from which a charge is still live, so a second one cannot be raised.</summary>
    private static readonly AssetSurchargeStatus[] LiveStatuses =
    [
        AssetSurchargeStatus.Draft,
        AssetSurchargeStatus.WithEmployee,
        AssetSurchargeStatus.Submitted,
        AssetSurchargeStatus.Approved,
        AssetSurchargeStatus.Recovering
    ];

    public AssetSurchargeService(
        IAssetSurchargeRepository surchargeRepo,
        IAssetSurchargeRecoveryRepository recoveryRepo,
        IAssetAssignmentRepository assignmentRepo,
        ICurrencyService currencies,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters)
    {
        _surchargeRepo = surchargeRepo;
        _recoveryRepo = recoveryRepo;
        _assignmentRepo = assignmentRepo;
        _currencies = currencies;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
    }

    // ── plumbing ─────────────────────────────────────────────────────────────

    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    /// <summary>The login, for the engine. Not the same id as the Employee FKs on the record.</summary>
    private Guid RequireCallerUserId() =>
        Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

    private Guid RequireCallerEmployeeId(string action) =>
        AssetActor.CallerEmployeeId(_currentUserService)
            ?? throw new UnauthorizedAccessException(
                $"Your user account is not linked to an employee record, so it cannot {action}.");

    private async Task<AssetSurcharge> GetOwnedAsync(Guid id)
    {
        var entity = await _surchargeRepo.GetWithDetailsAsync(id);
        if (entity is null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound($"No asset surcharge was found with id {id}.");
        return entity;
    }

    /// <summary>
    /// The read gate.
    /// </summary>
    /// <remarks>
    /// A charge that has not yet been put to the employee is HR's own deliberation, so to them it
    /// does not exist — <b>404, not 403</b>. A 403 would confirm that a charge against them is
    /// being drafted, which is the one thing a draft is not ready to say.
    /// </remarks>
    private void EnsureReadable(AssetSurcharge entity, string action)
    {
        if (AssetActor.IsHr(_currentUserService)) return;

        if (AssetActor.CallerEmployeeId(_currentUserService) != entity.EmployeeId)
            throw new UnauthorizedAccessException($"Only HR and the employee concerned can {action}.");

        if (entity.NotifiedAt is null)
            throw AssetsWorkflowException.NotFound($"No asset surcharge was found with id {entity.Id}.");
    }

    /// <summary>
    /// The currency for a charge: what was asked for, HR's configured default, or Finance's base.
    /// </summary>
    /// <remarks>
    /// Validated against Finance's canonical list, which is the read-side integration this module
    /// is allowed to do now. Area 13 recorded what its absence costs: <c>"ZZZ"</c> was accepted and
    /// stored. GL posting is what waits for the sweep; knowing whether a currency exists does not.
    /// </remarks>
    private async Task<string> ResolveCurrencyAsync(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var code = requested.Trim().ToUpperInvariant();
            var known = await _currencies.GetByCodeAsync(code);
            if (known is null)
                throw AssetsWorkflowException.Invalid(
                    $"'{code}' is not a currency Finance holds. Add it in Finance, or use one that exists.");
            return code;
        }

        var baseCurrency = await _currencies.GetBaseCurrencyAsync();
        if (baseCurrency is null)
            throw AssetsWorkflowException.Invalid(
                "No base currency is configured in Finance, so a surcharge amount has no currency to be in. "
                + "Set one, or state the currency on the charge.");

        return baseCurrency.CurrencyCode;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<AssetSurchargeDto?> GetByIdAsync(Guid id)
    {
        var entity = await _surchargeRepo.GetWithDetailsAsync(id);
        if (entity is null || entity.TenantId != GetTenantId()) return null;

        EnsureReadable(entity, "read an asset surcharge");
        return entity.ToDto();
    }

    public async Task<IEnumerable<AssetSurchargeSummaryDto>> GetAllAsync()
        => (await _surchargeRepo.GetByTenantAsync(GetTenantId())).ToSummaryDtoList();

    public async Task<PagedResult<AssetSurchargeSummaryDto>> GetPagedAsync(
        int page, int pageSize, string? searchTerm = null, AssetSurchargeStatus? status = null)
    {
        var all = (await _surchargeRepo.GetByTenantAsync(GetTenantId())).AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            all = all.Where(x => x.SurchargeNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || x.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (x.Employee != null
                    && (x.Employee.FirstName + " " + x.Employee.LastName)
                        .Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        if (status.HasValue)
            all = all.Where(x => x.Status == status.Value);

        var totalCount = all.Count();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<AssetSurchargeSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AssetSurchargeSummaryDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        AssetActor.EnsureSelfOrHr(_currentUserService, employeeId,
            "list the asset surcharges raised against an employee");

        var tenantId = GetTenantId();
        var isHr = AssetActor.IsHr(_currentUserService);

        var rows = (await _surchargeRepo.GetByEmployeeIdAsync(employeeId))
            .Where(x => x.TenantId == tenantId)
            // The employee sees what has been put to them, not what is being drafted about them.
            .Where(x => isHr || x.NotifiedAt is not null);

        return rows.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AssetSurchargeSummaryDto>> GetByAssignmentIdAsync(Guid assignmentId)
    {
        var tenantId = GetTenantId();
        return (await _surchargeRepo.GetByAssignmentIdAsync(assignmentId))
            .Where(x => x.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<AssetSurchargeSummaryDto>> GetOutstandingAsync()
        => (await _surchargeRepo.GetOutstandingAsync(GetTenantId())).ToSummaryDtoList();

    /// <summary>
    /// The read-only projection payroll consumes.
    /// </summary>
    /// <remarks>
    /// <para>Only <b>approved</b> charges with a balance and a <c>PayrollDeduction</c> plan appear.
    /// A disputed-but-undecided charge is not a debt, and a charge to be settled at exit or paid
    /// directly is not payroll's to deduct — putting either in this list would have payroll
    /// collecting money nobody has ruled is owed.</para>
    ///
    /// <para><c>InstalmentAmount</c> is the assessed amount divided by the declared instalment
    /// count, rounded to two places. It is a <i>statement of intent</i>, not a schedule: HR does not
    /// know payroll's periods, its rounding or its net-pay floor, and inventing them here is
    /// precisely the parallel mechanism the ownership boundary exists to prevent.</para>
    ///
    /// <para>⚠ <b>A balance already taken at exit drops off this list</b> — area 16 slice 10. An
    /// employee part-way through four instalments who then leaves has the remaining balance carried
    /// onto their clearance form and deducted from the FR-HR-184 final settlement. Leaving the row
    /// here as well would have payroll collect the same money a second time, and neither side could
    /// see the other doing it: the settlement is a one-off statement, this list is a standing
    /// instruction, and nothing connected them. Suppressed once the clearance form is COMPLETE,
    /// not before — an exit that is abandoned mid-clearance must not stop a live recovery.</para>
    /// </remarks>
    public async Task<IEnumerable<AssetSurchargePayrollLineDto>> GetPayrollDeductionLinesAsync()
    {
        var tenantId = GetTenantId();
        var outstanding = await _surchargeRepo.GetOutstandingAsync(tenantId);

        var takenAtExit = await ChargesTakenAtExitAsync(tenantId);

        return outstanding
            .Where(x => x.RecoveryMethod == AssetSurchargeRecoveryMethod.PayrollDeduction)
            .Where(x => !takenAtExit.Contains(x.Id))
            .Select(x => new AssetSurchargePayrollLineDto
            {
                SurchargeId = x.Id,
                SurchargeNumber = x.SurchargeNumber,
                EmployeeId = x.EmployeeId,
                EmployeeName = x.Employee is null ? string.Empty : $"{x.Employee.FirstName} {x.Employee.LastName}",
                EmployeeNumber = x.Employee?.EmployeeNumber ?? string.Empty,
                AssetName = x.Assignment?.Asset?.AssetName ?? string.Empty,
                CurrencyCode = x.CurrencyCode,
                AssessedAmount = x.AssessedAmount,
                AmountRecovered = x.AmountRecovered,
                AmountOutstanding = x.AssessedAmount - x.AmountRecovered,
                InstalmentCount = x.InstalmentCount,
                InstalmentAmount = x.InstalmentCount is > 0
                    ? decimal.Round(x.AssessedAmount / x.InstalmentCount.Value, 2)
                    : null,
                RecoveryStartDate = x.RecoveryStartDate,
                ApprovalDate = x.ApprovalDate
            })
            .ToList();
    }

    /// <summary>
    /// The charges whose balance a completed exit clearance has already carried to a final
    /// settlement, and which payroll must therefore stop deducting — area 16 slice 10.
    /// </summary>
    /// <remarks>
    /// <para>Read here rather than pushed from the separation side on purpose. Whether a charge is
    /// still payroll's to collect is a question about the charge, and the answer belongs beside the
    /// projection that answers it — a flag written onto the surcharge by another area's service
    /// would be a second copy of a fact that can change (a settlement can be cancelled), and it
    /// would drift.</para>
    ///
    /// <para>The join is <c>SourceSurchargeId</c>, set when the clearance line was drawn from the
    /// asset register. A hand-entered amount on a hand-written property line carries no surcharge
    /// id and correctly suppresses nothing — nobody can tell what it was about.</para>
    /// </remarks>
    private async Task<HashSet<Guid>> ChargesTakenAtExitAsync(Guid tenantId)
    {
        var ids = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted
                        && i.SourceSurchargeId != null
                        && i.OutstandingAmount > 0
                        // ⚠ The SEPARATION's deletion, not the item's. Deleting a separation does
                        // not cascade to its clearance lines, so a voided exit would otherwise go
                        // on suppressing a live payroll recovery for ever — money quietly not
                        // collected, with nothing on either screen to say why.
                        && !i.Separation.IsDeleted
                        && i.Separation.Status >= SeparationStatus.ClearanceCompleted
                        && i.Separation.Status != SeparationStatus.Cancelled)
            .Select(i => i.SourceSurchargeId!.Value)
            .Distinct()
            .ToListAsync();

        return ids.ToHashSet();
    }

    // ── raising a charge ─────────────────────────────────────────────────────

    /// <summary>
    /// Raises a charge against the holder of an assignment.
    /// </summary>
    /// <remarks>
    /// <para>Two preconditions, and both are the point of the slice. The assignment must record
    /// that something went wrong — damage on return, or a loss — and it must record that the
    /// employee is answerable for it. A surcharge on a record that says the asset came back fine,
    /// or that says the employee was not liable, contradicts the very evidence it rests on; that is
    /// the same failure D-c fixed on the return path, one step downstream.</para>
    ///
    /// <para>The amount defaults to what it would cost to replace the asset, or to repair it where
    /// there is no replacement cost — and it is only a default. Both figures are copied onto the
    /// charge as its basis, so a decision to charge less than the damage cost stays visible as one.</para>
    /// </remarks>
    public async Task<AssetSurchargeDto> CreateAsync(CreateAssetSurchargeDto dto)
    {
        var tenantId = GetTenantId();
        var userId = RequireCallerUserId();
        var raisedById = RequireCallerEmployeeId("raise an asset surcharge");

        var assignment = await _assignmentRepo.GetWithDetailsAsync(dto.AssignmentId);
        if (assignment is null || assignment.TenantId != tenantId)
            throw AssetsWorkflowException.NotFound(
                $"No asset assignment was found with id {dto.AssignmentId}.");

        var recordsIncident = assignment.DamageReported
            || assignment.Status is AssignmentStatus.Lost or AssignmentStatus.Damaged;

        if (!recordsIncident)
            throw AssetsWorkflowException.InvalidState(
                $"Assignment {assignment.AssignmentNumber} records no damage and no loss, so there is "
                + "nothing to charge for. Record the return's damage, or report the asset lost, first.");

        if (!assignment.EmployeeLiable)
            throw AssetsWorkflowException.InvalidState(
                $"Assignment {assignment.AssignmentNumber} records that the employee is not liable. "
                + "A charge cannot contradict the record it rests on.");

        var live = (await _surchargeRepo.GetByAssignmentIdAsync(assignment.Id))
            .FirstOrDefault(x => x.TenantId == tenantId && LiveStatuses.Contains(x.Status));
        if (live is not null)
            throw AssetsWorkflowException.Conflict(
                $"Surcharge {live.SurchargeNumber} is already live against this assignment ({live.Status}). "
                + "Waive or cancel it before raising another.");

        var amount = dto.AssessedAmount
            ?? assignment.ReplacementCost
            ?? assignment.RepairCost
            ?? throw AssetsWorkflowException.Invalid(
                "No repair or replacement cost is recorded on the assignment, so there is no amount to "
                + "default to. State the amount being charged.");

        if (amount <= 0)
            throw AssetsWorkflowException.Invalid("A surcharge must be for more than zero.");

        var entity = new AssetSurcharge
        {
            TenantId = tenantId,
            SurchargeNumber = $"SUR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            AssignmentId = assignment.Id,
            EmployeeId = assignment.EmployeeId,
            Reason = dto.Reason,
            Description = dto.Description.Trim(),
            AssessedAmount = amount,
            CurrencyCode = await ResolveCurrencyAsync(dto.CurrencyCode),
            BasisRepairCost = assignment.RepairCost,
            BasisReplacementCost = assignment.ReplacementCost,
            AmountRecovered = 0m,
            Status = AssetSurchargeStatus.Draft,
            RaisedById = raisedById,
            RaisedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };

        await _surchargeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _surchargeRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<AssetSurchargeDto> UpdateAsync(Guid id, UpdateAssetSurchargeDto dto)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != AssetSurchargeStatus.Draft)
            throw AssetsWorkflowException.InvalidState(
                entity.Status == AssetSurchargeStatus.WithEmployee
                    ? "This charge has already been put to the employee. Changing the amount they were "
                      + "shown would make their answer an answer to something else."
                    : $"Only a draft surcharge can be edited; this one is {entity.Status}.");

        if (dto.AssessedAmount <= 0)
            throw AssetsWorkflowException.Invalid("A surcharge must be for more than zero.");

        entity.Reason = dto.Reason;
        entity.Description = dto.Description.Trim();
        entity.AssessedAmount = dto.AssessedAmount;
        entity.CurrencyCode = await ResolveCurrencyAsync(dto.CurrencyCode ?? entity.CurrencyCode);
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != AssetSurchargeStatus.Draft)
            throw AssetsWorkflowException.InvalidState(
                $"Only a draft surcharge can be deleted; this one is {entity.Status}. "
                + "Cancel it or waive it instead, so the record of it survives.");

        await _surchargeRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── the employee's side (decision D9) ────────────────────────────────────

    public async Task<AssetSurchargeDto> NotifyEmployeeAsync(Guid id)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != AssetSurchargeStatus.Draft)
            throw AssetsWorkflowException.InvalidState(
                entity.Status == AssetSurchargeStatus.WithEmployee
                    ? "This charge is already with the employee."
                    : $"Only a draft surcharge can be put to the employee; this one is {entity.Status}.");

        entity.Status = AssetSurchargeStatus.WithEmployee;
        entity.NotifiedAt = DateTime.UtcNow;
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    /// <summary>
    /// The employee's own answer to a charge against them.
    /// </summary>
    /// <remarks>
    /// Refused for everybody but the person being charged, <b>HR included</b> — the same rule that
    /// governs acknowledging receipt of an asset, and for the same reason. HR entering an employee's
    /// acceptance on their behalf is not a right of reply; it is the absence of one, minuted.
    /// </remarks>
    public async Task<AssetSurchargeDto> RespondAsync(Guid id, RespondToAssetSurchargeDto dto)
    {
        var entity = await GetOwnedAsync(id);

        AssetActor.EnsureIsSubject(_currentUserService, entity.EmployeeId,
            "respond to a surcharge raised against them");

        if (entity.Status != AssetSurchargeStatus.WithEmployee)
            throw AssetsWorkflowException.InvalidState(
                entity.Status == AssetSurchargeStatus.Draft
                    ? "This charge has not been put to you yet."
                    : $"This charge is no longer open for your response; it is {entity.Status}.");

        if (entity.EmployeeResponse != AssetSurchargeEmployeeResponse.NotYetGiven)
            throw AssetsWorkflowException.Conflict(
                $"You have already answered this charge ({entity.EmployeeResponse}).");

        entity.EmployeeResponse = dto.Accepted
            ? AssetSurchargeEmployeeResponse.Accepted
            : AssetSurchargeEmployeeResponse.Disputed;
        entity.EmployeeRespondedAt = DateTime.UtcNow;
        entity.EmployeeResponseComments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    // ── the decision ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends the charge for approval — the workflow engine, decision D3's recipe again.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The natural-justice gate lives here.</b> A charge cannot reach an approver until it has
    /// been put to the employee and they have answered — or until the caller states why it is going
    /// without an answer, which is then on the record the approver reads. Silence must not be a veto
    /// and must not be invisible either.
    /// </remarks>
    public async Task<AssetSurchargeDto> SubmitAsync(Guid id, SubmitAssetSurchargeDto dto)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != AssetSurchargeStatus.WithEmployee)
            throw AssetsWorkflowException.InvalidState(
                entity.Status == AssetSurchargeStatus.Draft
                    ? "This charge has not been put to the employee yet. They must be given the chance "
                      + "to answer before it goes for approval."
                    : $"Only a charge that is with the employee can be submitted; this one is {entity.Status}.");

        if (entity.EmployeeResponse == AssetSurchargeEmployeeResponse.NotYetGiven)
        {
            if (string.IsNullOrWhiteSpace(dto.ProceedWithoutResponseReason))
                throw AssetsWorkflowException.InvalidState(
                    "The employee has not answered this charge. Either wait for their response, or state "
                    + "why it is going for approval without one.");

            entity.ProceededWithoutResponseReason = dto.ProceedWithoutResponseReason.Trim();
        }

        var result = await AssetRequisitionService.RunWorkflowAsync(
            () => _workflow.SubmitAsync(EntityType, entity.Id),
            "start the surcharge approval workflow");

        var actingUserId = RequireCallerUserId();
        _workflowAdapters.GetAdapter(EntityType).ApplySubmitOutcome(entity, result.Outcome, actingUserId);
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    public async Task<AssetSurchargeDto> RecallAsync(Guid id, string? reason)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != AssetSurchargeStatus.Submitted)
            throw AssetsWorkflowException.InvalidState(
                $"Only a charge awaiting approval can be recalled; this one is {entity.Status}.");

        var actingUserId = RequireCallerUserId();
        var result = await AssetRequisitionService.RunWorkflowAsync(
            () => _workflow.RecallAsync(EntityType, entity.Id, actingUserId, reason),
            "recall the surcharge");

        _workflowAdapters.GetAdapter(EntityType).ApplyRecallOutcome(entity, actingUserId, reason);
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    /// <summary>
    /// Approves the charge, optionally at a lower figure than was assessed.
    /// </summary>
    /// <remarks>
    /// <para>Lowering is allowed and raising is not, and that asymmetry is the right of reply doing
    /// its job: reducing a charge is the ordinary outcome of a dispute the employee partly won,
    /// while raising it would charge them a figure they were never shown and never got to answer.</para>
    ///
    /// <para>Nobody approves a charge against themselves.</para>
    /// </remarks>
    public async Task ApproveAsync(Guid id, ApproveAssetSurchargeDto dto)
    {
        var entity = await GetOwnedAsync(id);

        var approverId = RequireCallerEmployeeId("approve an asset surcharge");
        var actingUserId = RequireCallerUserId();

        RequireDecidable(entity);
        RequireNotTheSubject(entity, approverId, "approve");

        if (dto.ApprovedAmount is { } approved)
        {
            if (approved <= 0)
                throw AssetsWorkflowException.Invalid("An approved surcharge must be for more than zero.");

            if (approved > entity.AssessedAmount)
                throw AssetsWorkflowException.Invalid(
                    $"An approved amount cannot exceed the {entity.AssessedAmount:0.00} the employee was "
                    + "shown and given the chance to answer. Reject this charge and raise a new one.");
        }

        var result = await ProcessApprovalAsync(entity, "Approve", dto.ApprovalComments);

        _workflowAdapters.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, actingUserId);

        // Stamped only once the chain has actually finished — a two-step definition leaves the
        // record Submitted after the first signature, and naming an approver there would name one
        // signatory as *the* approver of something not yet approved.
        if (result.Outcome == WorkflowOutcome.Approved)
        {
            entity.ApprovedById = approverId;
            entity.ApprovalDate = DateTime.UtcNow;
            if (dto.ApprovedAmount is { } finalAmount)
                entity.AssessedAmount = finalAmount;
        }

        if (!string.IsNullOrWhiteSpace(dto.ApprovalComments))
            entity.ApprovalComments = dto.ApprovalComments.Trim();

        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RejectAsync(Guid id, RejectAssetSurchargeDto dto)
    {
        var entity = await GetOwnedAsync(id);

        var approverId = RequireCallerEmployeeId("reject an asset surcharge");
        var actingUserId = RequireCallerUserId();

        RequireDecidable(entity);
        RequireNotTheSubject(entity, approverId, "reject");

        var reason = string.IsNullOrWhiteSpace(dto.RejectionReason) ? "Rejected" : dto.RejectionReason.Trim();
        var result = await ProcessApprovalAsync(entity, "Reject", reason);

        _workflowAdapters.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, actingUserId, reason);

        entity.RejectionReason = reason;
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── recovery: declared, then recorded ────────────────────────────────────

    public async Task<AssetSurchargeDto> SetRecoveryPlanAsync(Guid id, SetAssetSurchargeRecoveryPlanDto dto)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status is not (AssetSurchargeStatus.Approved or AssetSurchargeStatus.Recovering))
            throw AssetsWorkflowException.InvalidState(
                $"A recovery plan belongs to an approved charge; this one is {entity.Status}.");

        // An exit settlement is collected once, out of what the employee is owed on leaving. Spreading
        // it over instalments would describe a deduction from pay that is never going to run.
        if (dto.RecoveryMethod == AssetSurchargeRecoveryMethod.ExitSettlement && dto.InstalmentCount is > 1)
            throw AssetsWorkflowException.Invalid(
                "A charge settled at exit is deducted once, from the final settlement. It cannot be "
                + "spread over instalments.");

        entity.RecoveryMethod = dto.RecoveryMethod;
        entity.InstalmentCount = dto.InstalmentCount;
        entity.RecoveryStartDate = dto.RecoveryStartDate;
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    /// <summary>
    /// Records money actually collected against the charge.
    /// </summary>
    /// <remarks>
    /// A record of what happened, not an instruction. It cannot take more than is outstanding —
    /// over-recovering is the employer owing the employee, which is not a thing this record can
    /// express and must not silently become.
    /// </remarks>
    public async Task<AssetSurchargeDto> RecordRecoveryAsync(Guid id, RecordAssetSurchargeRecoveryDto dto)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireCallerUserId();
        var recordedById = RequireCallerEmployeeId("record a surcharge recovery");

        if (entity.Status is not (AssetSurchargeStatus.Approved or AssetSurchargeStatus.Recovering))
            throw AssetsWorkflowException.InvalidState(
                $"Money can only be recorded against an approved charge; this one is {entity.Status}.");

        var outstanding = entity.AssessedAmount - entity.AmountRecovered;
        if (dto.Amount > outstanding)
            throw AssetsWorkflowException.Invalid(
                $"Only {outstanding:0.00} is still outstanding on this charge; {dto.Amount:0.00} would "
                + "over-recover it.");

        var recovery = new AssetSurchargeRecovery
        {
            TenantId = entity.TenantId,
            SurchargeId = entity.Id,
            Amount = dto.Amount,
            RecoveredOn = dto.RecoveredOn,
            Method = dto.Method,
            Reference = string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            RecordedById = recordedById,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };

        // ⚠ Added through its own repository rather than onto `entity.Recoveries`. Adding to a
        // tracked navigation collection on an edit path turns the parent into an UPDATE of the whole
        // graph — the EF tracked-graph trap this repo has recorded before.
        await _recoveryRepo.AddAsync(recovery);

        entity.AmountRecovered += dto.Amount;
        entity.Status = entity.AmountRecovered >= entity.AssessedAmount
            ? AssetSurchargeStatus.Recovered
            : AssetSurchargeStatus.Recovering;
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    /// <summary>
    /// Forgives what is still outstanding.
    /// </summary>
    /// <remarks>
    /// Whatever was already collected stays collected; a waiver forgives the balance, it does not
    /// refund. A charge cannot be waived before anybody has raised it with the employee, because
    /// there would be nothing to forgive — delete the draft instead.
    /// </remarks>
    public async Task<AssetSurchargeDto> WaiveAsync(Guid id, WaiveAssetSurchargeDto dto)
    {
        var entity = await GetOwnedAsync(id);
        var waivedById = RequireCallerEmployeeId("waive an asset surcharge");

        if (entity.Status is AssetSurchargeStatus.Draft)
            throw AssetsWorkflowException.InvalidState(
                "A draft charge has not been put to anybody, so there is nothing to waive. Delete it.");

        if (entity.Status is AssetSurchargeStatus.Waived or AssetSurchargeStatus.Recovered
            or AssetSurchargeStatus.Rejected or AssetSurchargeStatus.Cancelled)
            throw AssetsWorkflowException.InvalidState(
                $"This charge is already closed ({entity.Status}).");

        entity.Status = AssetSurchargeStatus.Waived;
        entity.WaivedById = waivedById;
        entity.WaivedAt = DateTime.UtcNow;
        entity.WaiverReason = dto.WaiverReason.Trim();
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    /// <summary>Withdraws a charge raised in error, before anybody has ruled on it.</summary>
    public async Task<AssetSurchargeDto> CancelAsync(Guid id, CancelAssetSurchargeDto dto)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status is not (AssetSurchargeStatus.Draft or AssetSurchargeStatus.WithEmployee))
            throw AssetsWorkflowException.InvalidState(
                entity.Status == AssetSurchargeStatus.Submitted
                    ? "This charge is with an approver. Recall it first, or let them rule on it."
                    : $"Only a charge nobody has ruled on can be cancelled; this one is {entity.Status}.");

        entity.Status = AssetSurchargeStatus.Cancelled;
        entity.CancelledAt = DateTime.UtcNow;
        entity.CancellationReason = dto.CancellationReason.Trim();
        Stamp(entity);

        await _surchargeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await _surchargeRepo.GetWithDetailsAsync(id))!.ToDto();
    }

    // ── rules ────────────────────────────────────────────────────────────────

    private void Stamp(AssetSurcharge entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserId;
    }

    private static void RequireDecidable(AssetSurcharge entity)
    {
        if (entity.Status == AssetSurchargeStatus.Submitted) return;

        throw AssetsWorkflowException.InvalidState(
            entity.Status is AssetSurchargeStatus.Draft or AssetSurchargeStatus.WithEmployee
                ? "This charge has not been submitted for approval yet."
                : $"Only a charge awaiting approval can be decided; this one is {entity.Status}.");
    }

    /// <summary>Nobody rules on a charge against themselves.</summary>
    private static void RequireNotTheSubject(AssetSurcharge entity, Guid actorEmployeeId, string verb)
    {
        if (actorEmployeeId != entity.EmployeeId) return;

        throw new UnauthorizedAccessException(
            $"You cannot {verb} a surcharge raised against you.");
    }

    private async Task<WorkflowIntegrationResult> ProcessApprovalAsync(
        AssetSurcharge entity, string action, string? comments)
    {
        var actingUserId = RequireCallerUserId();

        if (!await _workflow.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
            throw new UnauthorizedAccessException(
                "You are not assigned as an approver for the current step of this surcharge's approval workflow.");

        return await AssetRequisitionService.RunWorkflowAsync(
            () => _workflow.ProcessApprovalAsync(EntityType, entity.Id, actingUserId, action, comments),
            $"process the {action.ToLowerInvariant()}");
    }
}
