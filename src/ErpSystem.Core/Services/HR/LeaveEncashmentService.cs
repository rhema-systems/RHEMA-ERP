using ErpSystem.Application.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Shared;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Services.HR.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class LeaveEncashmentService : ILeaveEncashmentService
{
    private const string EntityType = "LeaveEncashment";

    private readonly IGenericRepository<LeaveEncashment> _encashmentRepository;
    private readonly ILeaveRepository _leaveRepository;
    private readonly IGenericRepository<LeaveBalance> _balanceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveEncashmentService> _logger;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILeaveBalanceRecalculationService _recalculationService;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly IDateTimeProvider _clock;
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly IHrFinancePostingAdapter _financePosting;
    private readonly ILeaveEntitlementService _entitlementService;
    private readonly ILeaveYearContext _leaveYear;

    public LeaveEncashmentService(
        IGenericRepository<LeaveEncashment> encashmentRepository,
        ILeaveRepository leaveRepository,
        IGenericRepository<LeaveBalance> balanceRepository,
        IUnitOfWork unitOfWork,
        ILogger<LeaveEncashmentService> logger,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ICurrentUserService currentUserService,
        ILeaveBalanceRecalculationService recalculationService,
        IGenericRepository<LeaveType> leaveTypeRepository,
        IDateTimeProvider clock,
        ICompanyHrPolicyProvider policyProvider,
        IHrFinancePostingAdapter financePosting,
        ILeaveEntitlementService entitlementService,
        ILeaveYearContext leaveYear)
    {
        _encashmentRepository = encashmentRepository;
        _leaveRepository = leaveRepository;
        _balanceRepository = balanceRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserService = currentUserService;
        _recalculationService = recalculationService;
        _leaveTypeRepository = leaveTypeRepository;
        _clock = clock;
        _policyProvider = policyProvider;
        _financePosting = financePosting;
        _entitlementService = entitlementService;
        _leaveYear = leaveYear;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    // An encashment owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<LeaveEncashment> GetOwnedEncashmentAsync(Guid id)
    {
        var entity = await _encashmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave encashment '{id}' not found.");
        return entity;
    }

    private async Task<LeaveRequest> GetOwnedLeaveRequestAsync(Guid id)
    {
        var entity = await _leaveRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave request '{id}' not found.");
        return entity;
    }

    private async Task<LeaveType> GetOwnedLeaveTypeAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave type '{id}' not found.");
        return entity;
    }

    public async Task<bool> IsInServiceAllowedAsync()
        => (await _policyProvider.GetAsync()).AllowInServiceEncashment;

    public async Task<LeaveEncashmentDto> RequestEncashmentAsync(CreateLeaveEncashmentDto dto)
    {
        var leaveRequest = await GetOwnedLeaveRequestAsync(dto.LeaveRequestId);

        if (leaveRequest.Encashment != null)
            throw new InvalidOperationException("This leave request has already been encashed.");

        // ⚠ The tenant policy is asked FIRST, and the order matters. FR-HR-046 says leave is
        // encashed "only on exit, no other route", while this path and the seeded
        // AllowCashConversion flag say otherwise — both readings were live in the product at once
        // (residue plan L-D8). The switch settles it per client rather than per requirement
        // document, and it defaults OFF, which is FR-HR-046's reading.
        //
        // Asked before the leave type so the refusal names the real reason. A tenant with the route
        // switched off should be told the route is closed, not sent away to change a flag on a leave
        // type that would make no difference.
        var policy = await _policyProvider.GetAsync();
        if (!policy.AllowInServiceEncashment)
            throw new InvalidOperationException(
                "Leave is encashed only when an employee leaves, not while they are still employed. "
                + "If that is not this organisation's policy, switch on in-service encashment in HR policy settings.");

        // Only ANNUAL leave is cashed in while employed (round 5, decision A4 / lane A2). Asked
        // before the type's own flag, so the refusal names the real reason: sick or casual days are
        // not a reserve of money whatever a flag on their type says.
        var leaveType = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        if (leaveType.Category != LeaveTypeCategory.Annual)
            throw new InvalidOperationException(
                $"Only annual leave can be cashed in, and '{leaveType.Name}' is not annual leave.");

        // The leave type must permit cash conversion before any encashment can be requested.
        if (!leaveType.AllowCashConversion)
            throw new InvalidOperationException("This leave type does not allow cash conversion (encashment).");

        // ⚠ Round 5, lane L3 — defensive, and it matters only where a client switches in-service
        // encashment on. Only the CURRENT leave year's days can be cashed: an earlier year's leftover
        // was carried over or lapsed at its year-end, and cashing it as well would pay for it twice.
        var currentYear = await _leaveYear.CurrentYearAsync();
        if (dto.Year != currentYear)
            throw new InvalidOperationException(
                $"Leave can be cashed in from the current leave year, {currentYear}, only. Days left over "
                + $"from {dto.Year} were carried over or lapsed when that year ended.");

        var tenantId = GetTenantId();
        var balance = await _balanceRepository.FirstOrDefaultAsync(
            b => b.TenantId == tenantId &&
                 b.EmployeeId == dto.EmployeeId &&
                 b.LeaveTypeId == dto.LeaveTypeId &&
                 b.Year == dto.Year);

        if (balance == null)
            throw new InvalidOperationException("No leave balance found for the specified year and leave type.");

        // ⚠ Lane L3: the guard reads CAN TAKE NOW — the days built up so far, less those taken,
        // pending and already cashed — not the whole year's AvailableDays, which counts days not yet
        // earned. Less, too, the employee's other requests still awaiting a decision, which hold no
        // days until they are approved: two submitted side by side would each pass alone.
        var snapshot = await _entitlementService.GetSnapshotAsync(
            dto.EmployeeId, dto.LeaveTypeId, balance.LeaveSubTypeId, dto.Year);
        var awaiting = await _encashmentRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.EmployeeId == dto.EmployeeId
                        && e.LeaveTypeId == dto.LeaveTypeId && e.Year == dto.Year
                        && (e.Status == LeaveEncashmentStatus.Submitted || e.Status == LeaveEncashmentStatus.PendingApproval))
            .SumAsync(e => (decimal?)e.DaysEncashed) ?? 0m;
        var canCashNow = snapshot.AvailableFrom(
            balance.EntitledDays, balance.CarriedOverDays, balance.AdjustmentDays,
            balance.UsedDays, balance.PendingDays, balance.EncashedDays) - awaiting;

        if (canCashNow < dto.DaysEncashed)
            throw new InvalidOperationException(
                $"Only {Math.Max(0m, canCashNow):0.##} day(s) can be cashed in now — the days built up so far, "
                + $"less those taken, booked, already cashed in or awaiting a decision. {dto.DaysEncashed:0.##} "
                + "were asked for.");

        // ⚠ DAYS ONLY (leave settings audit 2, P4). HR worked out a payout here — (basic + linked
        // allowances) ÷ the leave type's working days — and that figure went into Finance's books when
        // the encashment was marked paid. Pay is Finance's: the amount and how it was worked out are
        // entered when Finance marks it paid (MarkAsProcessedAsync, HR.Pay.Value). Until then the
        // payout is zero and its basis empty, which the register shows as "awaiting Finance".
        var entity = new LeaveEncashment
        {
            TenantId = tenantId,
            LeaveRequestId = dto.LeaveRequestId,
            EmployeeId = dto.EmployeeId,
            LeaveTypeId = dto.LeaveTypeId,
            Year = dto.Year,
            DaysEncashed = dto.DaysEncashed,
            AmountPaid = 0m,
            RateBasis = null,
            Notes = dto.Notes,
            Status = LeaveEncashmentStatus.Submitted
        };

        // Create and submit in ONE transaction: a workflow submit that throws
        // must roll the insert back too, or the caller gets an error
        // while an orphaned Submitted row survives — and every retry then refuses with
        // "already been encashed". Found live in area 25 slice 4.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _encashmentRepository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            // Submitting must never approve — an encashment is a payment. Defence in depth; a
            // LEAVE_ENCASHMENT definition is seeded. See HrWorkflowFallbackAuthority.
            var (workflowResult, submitOutcome) =
                await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, entity.Id);

            if (workflowResult.ExecutionResult.Success)
            {
                var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
                adapter.ApplySubmitOutcome(entity, submitOutcome, GetCurrentUserId());
                await _encashmentRepository.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        });

        _logger.LogInformation("Leave encashment requested for employee {employeeId}: {days} days",
            dto.EmployeeId, dto.DaysEncashed);

        return (await GetWithIncludes(entity.Id))!.ToDto();
    }

        /// <summary>
    /// Refuses an approval by the very employee the record is about.
    /// </summary>
    /// <remarks>
    /// This is the segregation the two-stage leave definition relies on, and it is done HERE rather
    /// than with the engine's <c>PreventInitiatorApproval</c> on purpose. That flag guards the
    /// INITIATOR; a leave record's conflicted party is its SUBJECT, and HR raises leave on other
    /// people's behalf from the desk. On a tenant with one HR user the flag would strand every
    /// desk-raised record at the HR stage with nobody able to clear it — trap 7 of
    /// <c>HR-WORKFLOW-ENGINE-INTEGRATION.md</c>, and the area-9b mistake. Checking the subject
    /// blocks the real conflict and cannot strand somebody else's record.
    ///
    /// It sits before the authority call so it holds on the fallback path too, not just the engine.
    /// </remarks>
    private void RefuseSelfApproval(Guid subjectEmployeeId, string what)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == subjectEmployeeId)
            throw new InvalidOperationException(
                $"You cannot approve your own {what}. It has to be approved by someone else.");
    }

    public async Task<LeaveEncashmentDto> ApproveEncashmentAsync(Guid id)
    {
        var entity = await GetOwnedEncashmentAsync(id);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        RefuseSelfApproval(entity.EmployeeId, "leave encashment");

        var approvalOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserService.Roles, EntityType, id, userId,
            "Approve", null, "approve a leave encashment", HrPermissions.ApproveLeave);

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, approvalOutcome, userId);

        await _encashmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave encashment {id} approved", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeaveEncashmentDto> RejectEncashmentAsync(Guid id, string reason)
    {
        var entity = await GetOwnedEncashmentAsync(id);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var rejectionText = !string.IsNullOrWhiteSpace(reason) ? reason : "Rejected";

        var rejectionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserService.Roles, EntityType, id, userId,
            "Reject", rejectionText, "reject a leave encashment", HrPermissions.ApproveLeave);

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, rejectionOutcome, userId, rejectionText);

        await _encashmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave encashment {id} rejected", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    /// <summary>
    /// The acting employee for an actor column that is an <c>Employees</c> foreign key. A login id
    /// is not an employee id; an unlinked account cannot be the actor and is refused with a message
    /// rather than a constraint failure. Mirrors <c>LeaveService.RequireActingEmployeeId</c>.
    /// </summary>
    private Guid RequireActingEmployeeId(string purpose)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty)
            return me;
        throw new InvalidOperationException(
            $"{purpose} requires your user account to be linked to an employee record. Please contact your administrator.");
    }

    public async Task<LeaveEncashmentDto> MarkAsProcessedAsync(Guid id, ProcessLeaveEncashmentDto dto)
    {
        var entity = await GetOwnedEncashmentAsync(id);

        if (entity.Status != LeaveEncashmentStatus.Approved)
            throw new InvalidOperationException("Only approved encashments can be marked as processed.");

        // Finance's figure (leave settings audit 2, P4): the amount it paid, and its payment reference.
        if (dto.Amount <= 0m)
            throw new InvalidOperationException(
                "Enter the amount paid for these days. Finance values leave cashed in; HR records only the days.");
        if (string.IsNullOrWhiteSpace(dto.PaymentReference))
            throw new InvalidOperationException("Enter the payment reference — the voucher or transaction it was paid on.");
        // RateBasis is 500 characters, and the stored sentence prefixes Finance's words.
        if (dto.Basis is { Length: > 440 })
            throw new InvalidOperationException("Say how it was worked out in 440 characters or fewer.");

        // ProcessedByEmployeeId is an Employees foreign key. The screen used to send the login's
        // user id, which no employee has, so the action failed on the constraint every time — the
        // same defect lane 4 fixed for adjustments and plans. The actor comes from the token.
        var processedBy = RequireActingEmployeeId("Marking a leave encashment as paid");

        entity.Status = LeaveEncashmentStatus.Processed;
        entity.ProcessedDate = _clock.UtcNow;
        entity.ProcessedByEmployeeId = processedBy;
        entity.PaymentReference = dto.PaymentReference.Trim();
        entity.AmountPaid = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero);
        entity.RateBasis = string.IsNullOrWhiteSpace(dto.Basis)
            ? $"Valued by Finance when paid: {entity.AmountPaid:N2} for {entity.DaysEncashed:0.##} day(s)."
            : $"Valued by Finance when paid: {dto.Basis.Trim()}";

        // Mark processed, recalculate the balance AND post to Finance in one transaction (HR
        // finish plan lane 8, slice 2). The balance's EncashedDays is derived from processed
        // encashments by the recalculation service — the single source of truth — so we never
        // mutate UsedDays directly here. Whether in-service encashment exists at all is the policy
        // flag AllowInServiceEncashment, checked long before this point; the posting only follows
        // the event. The adapter owns the transaction that this method used to open itself.
        var actedBy = Guid.TryParse(_currentUserService.UserId, out var actorUserId) ? actorUserId : Guid.Empty;
        await _financePosting.RunAsync(async ct =>
        {
            await _encashmentRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(entity.EmployeeId, entity.LeaveTypeId, entity.Year);
            return HrFinancePostingCommandFactory.LeaveEncashmentProcessed(entity);
        }, actedBy);

        _logger.LogInformation("Leave encashment {id} marked as processed with reference {ref}",
            id, dto.PaymentReference);

        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<IEnumerable<LeaveEncashmentDto>> GetEmployeeEncashmentsAsync(Guid employeeId, int year)
    {
        var tenantId = GetTenantId();
        var items = await _encashmentRepository
            .GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.ProcessedByEmployee)
            .Include(e => e.LeaveRequest).ThenInclude(r => r!.LeaveType)
            .Where(e => e.TenantId == tenantId && e.EmployeeId == employeeId && e.Year == year)
            .OrderByDescending(e => e.ProcessedDate)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<IEnumerable<LeaveEncashmentDto>> GetAllEncashmentsAsync(
        int year,
        Guid?     employeeId  = null,
        Guid?     leaveTypeId = null,
        DateTime? from        = null,
        DateTime? to          = null,
        string?   search      = null,
        LeaveEncashmentStatus? status = null)
    {
        var tenantId = GetTenantId();
        var query = _encashmentRepository
            .GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.ProcessedByEmployee)
            .Include(e => e.LeaveRequest).ThenInclude(r => r!.LeaveType)
            .Where(e => e.TenantId == tenantId && e.Year == year);

        if (employeeId.HasValue)  query = query.Where(e => e.EmployeeId  == employeeId.Value);
        if (leaveTypeId.HasValue) query = query.Where(e => e.LeaveTypeId == leaveTypeId.Value);
        if (status.HasValue)      query = query.Where(e => e.Status == status.Value);
        if (from.HasValue)        query = query.Where(e => e.ProcessedDate != null && e.ProcessedDate >= from.Value);
        if (to.HasValue)          query = query.Where(e => e.ProcessedDate != null && e.ProcessedDate <= to.Value.AddDays(1));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                (e.PaymentReference != null && e.PaymentReference.Contains(term)) ||
                e.Employee.FirstName.Contains(term) ||
                e.Employee.LastName.Contains(term));
        }

        var items = await query.OrderByDescending(e => e.ProcessedDate).ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveEncashmentDto> GetByIdAsync(Guid id)
    {
        var entity = await GetWithIncludes(id);
        if (entity == null)
            throw new ArgumentException($"Leave encashment '{id}' not found.");
        return entity.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayToValueItemDto>> GetAwaitingPaymentAsync()
    {
        var tenantId = GetTenantId();
        var leaveTypes = _leaveTypeRepository.GetQueryable();

        var rows = await _encashmentRepository.GetQueryable()
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Status == LeaveEncashmentStatus.Approved)
            .OrderBy(e => e.ApprovedDate)
            .Select(e => new
            {
                e.Id,
                e.Year,
                e.DaysEncashed,
                e.Employee.FirstName,
                e.Employee.LastName,
                e.Employee.EmployeeNumber,
                RequestNumber = e.LeaveRequest.RequestNumber,
                TypeName = leaveTypes.Where(t => t.Id == e.LeaveTypeId).Select(t => t.Name).FirstOrDefault(),
            })
            .ToListAsync();

        return rows.Select(r => new PayToValueItemDto
        {
            Kind = "Encashment",
            Id = r.Id,
            Reference = r.RequestNumber,
            EmployeeName = $"{r.FirstName} {r.LastName}".Trim(),
            EmployeeNumber = r.EmployeeNumber,
            AwaitingCount = 1,
            Days = r.DaysEncashed,
            Summary = $"{r.DaysEncashed:0.##} day(s) of {r.TypeName ?? "leave"} {r.Year} cashed in",
        }).ToList();
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;

    private async Task<LeaveEncashment?> GetWithIncludes(Guid id)
    {
        var tenantId = GetTenantId();
        return await _encashmentRepository
            .GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.ProcessedByEmployee)
            .Include(e => e.LeaveRequest).ThenInclude(r => r!.LeaveType)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
    }
}
