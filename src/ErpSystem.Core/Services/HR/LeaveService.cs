using System.Text;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Shared;
using ErpSystem.Core.Interfaces.Common;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Application.Extensions;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service for leave management operations
/// </summary>
public class LeaveService : ILeaveService
{
    private const string EntityType = "LeaveRequest";

    private readonly ILeaveRepository _leaveRepository;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly IGenericRepository<LeaveSubType> _leaveSubTypeRepository;
    private readonly IGenericRepository<LeavePlan> _leavePlanRepository;
    private readonly IGenericRepository<LeaveBalance> _leaveBalanceRepository;
    private readonly IGenericRepository<LeaveEncashment> _leaveEncashmentRepository;
    private readonly IGenericRepository<LeaveAdjustment> _leaveAdjustmentRepository;
    private readonly IGenericRepository<EmployeeReliever> _employeeRelieverRepository;
    private readonly IHrWorkingDayCalculator _workingDayCalculator;
    private readonly ILeaveAttendancePostingService _attendancePosting;
    private readonly IGenericRepository<StaffDailyAttendance> _dailyAttendanceRepository;
    private readonly IGenericRepository<PublicHoliday> _holidayNameLookupRepository;
    private readonly IGenericRepository<LeaveRequestAttachment> _attachmentRepository;

    /// <summary>
    /// The medical board register, READ ONLY.
    /// </summary>
    /// <remarks>
    /// ⚠ Leave never writes a board. The board is a Medical-module record and the bridge is one
    /// way by design — this service asks whether one has concluded and does nothing else with it.
    /// A shared mutable clinical record across three modules is how three modules come to
    /// disagree about what a board decided.
    /// </remarks>
    private readonly IGenericRepository<MedicalBoard> _medicalBoardRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILeaveTypeService _leaveTypeService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILeaveYearContext _leaveYear;
    private readonly ILogger<LeaveService> _logger;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILeaveBalanceRecalculationService _recalculationService;
    private readonly ILeaveEntitlementService _entitlementService;
    private readonly IDateTimeProvider _clock;
    private readonly INumberSequenceService _numberSequence;

    /// <summary>
    /// Sequence key for leave request numbers. Year-bucketed: the printed number is
    /// <c>LV{year}{seq:D6}</c> and restarts each January, so the counter must too.
    /// </summary>
    private const string LeaveRequestSequenceKey = "LEAVE-REQ";

    public LeaveService(
            ILeaveRepository leaveRepository,
            IGenericRepository<LeaveType> leaveTypeRepository,
            IGenericRepository<LeaveSubType> leaveSubTypeRepository,
            IGenericRepository<LeavePlan> leavePlanRepository,
            IGenericRepository<LeaveBalance> leaveBalanceRepository,
            IGenericRepository<LeaveEncashment> leaveEncashmentRepository,
            IGenericRepository<LeaveAdjustment> leaveAdjustmentRepository,
            IGenericRepository<EmployeeReliever> employeeRelieverRepository,
            IHrWorkingDayCalculator workingDayCalculator,
            ILeaveAttendancePostingService attendancePosting,
            IGenericRepository<StaffDailyAttendance> dailyAttendanceRepository,
            IGenericRepository<PublicHoliday> holidayNameLookupRepository,
            IGenericRepository<LeaveRequestAttachment> attachmentRepository,
            IGenericRepository<MedicalBoard> medicalBoardRepository,
            IEmployeeRepository employeeRepository,
            ILeaveTypeService leaveTypeService,
            ILeaveYearContext leaveYear,
        ILogger<LeaveService> logger,
            IUnitOfWork unitOfWork,
            IWorkflowIntegrationService workflowIntegrationService,
            IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
            ICurrentUserService currentUserService,
            ILeaveBalanceRecalculationService recalculationService,
            ILeaveEntitlementService entitlementService,
            IDateTimeProvider clock,
            INumberSequenceService numberSequence)
    {
        _leaveRepository = leaveRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _leaveSubTypeRepository = leaveSubTypeRepository;
        _leavePlanRepository = leavePlanRepository;
        _leaveBalanceRepository = leaveBalanceRepository;
        _leaveEncashmentRepository = leaveEncashmentRepository;
        _leaveAdjustmentRepository = leaveAdjustmentRepository;
        _employeeRelieverRepository = employeeRelieverRepository;
        _workingDayCalculator = workingDayCalculator;
        _attendancePosting = attendancePosting;
        _dailyAttendanceRepository = dailyAttendanceRepository;
        _holidayNameLookupRepository = holidayNameLookupRepository;
        _attachmentRepository = attachmentRepository;
        _medicalBoardRepository = medicalBoardRepository;
        _employeeRepository = employeeRepository;
        _leaveTypeService = leaveTypeService;
        _leaveYear = leaveYear;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserService = currentUserService;
        _recalculationService = recalculationService;
        _entitlementService = entitlementService;
        _numberSequence = numberSequence;
        _clock = clock;
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

    // A leave request owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<LeaveRequest> GetOwnedLeaveRequestAsync(Guid id)
    {
        var entity = await _leaveRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        return entity;
    }

    private async Task<LeaveBalance> GetOwnedLeaveBalanceAsync(Guid id)
    {
        var entity = await _leaveBalanceRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave balance with ID '{id}' not found.");
        return entity;
    }

    private async Task<LeaveAdjustment> GetOwnedLeaveAdjustmentAsync(Guid id)
    {
        var entity = await _leaveAdjustmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave adjustment with ID '{id}' not found.");
        return entity;
    }

    private async Task<LeaveRequestAttachment> GetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave request attachment '{id}' not found.");
        return entity;
    }

    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var entity = await _employeeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return entity;
    }

    private async Task<LeaveType> GetOwnedLeaveTypeAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave type with ID '{id}' not found.");
        return entity;
    }

    private async Task<bool> HasConflictingLeaveAsync(
        Guid employeeId, DateOnly startDate, DateOnly endDate, Guid? excludeRequestId = null)
    {
        var tenantId = GetTenantId();
        var query = _leaveRepository.GetQueryable()
            .Where(la => la.TenantId == tenantId &&
                        la.EmployeeId == employeeId &&
                        // ⚠ InProgress is here because leave being taken right now is the most
                        // obvious kind of clash there is. It was omitted while nothing ever set
                        // that status; the R-14 sweep sets it, so without this an employee could
                        // book leave on top of leave they are currently on.
                        (la.Status == LeaveStatus.Approved ||
                         la.Status == LeaveStatus.Pending ||
                         la.Status == LeaveStatus.InProgress) &&
                        (la.StartDate <= endDate && la.EndDate >= startDate));

        if (excludeRequestId.HasValue)
            query = query.Where(la => la.Id != excludeRequestId.Value);

        return await query.AnyAsync();
    }

    private async Task<bool> RelieverHasConflictAsync(Guid relieverId, DateOnly startDate, DateOnly endDate)
    {
        var tenantId = GetTenantId();

        // ⚠ Both checks take InProgress as well as Approved. A reliever who is on leave RIGHT NOW is
        // the one case this guard most needs to catch, and it would have missed it once the R-14
        // sweep started advancing requests into that status.
        var relieverOnLeave = await _leaveRepository.GetQueryable()
            .AnyAsync(la => la.TenantId == tenantId &&
                           la.EmployeeId == relieverId &&
                           (la.Status == LeaveStatus.Approved || la.Status == LeaveStatus.InProgress) &&
                           la.StartDate <= endDate &&
                           la.EndDate >= startDate);

        if (relieverOnLeave)
            return true;

        return await _leaveRepository.GetQueryable()
            .AnyAsync(la => la.TenantId == tenantId &&
                           la.RelieverEmployeeId == relieverId &&
                           (la.Status == LeaveStatus.Approved || la.Status == LeaveStatus.InProgress) &&
                           la.StartDate <= endDate &&
                           la.EndDate >= startDate);
    }

    #region Leave CRUD operations

    public async Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto)
    {
        // Validate employee exists
        var employee = await GetOwnedEmployeeAsync(dto.EmployeeId);

        // Validate leave type exists
        var leaveType = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);

        // A retired leave type may not be taken. Until now this was enforced only by the picker
        // asking for active types, so any other caller — an integration, a harness, a stale tab —
        // could still raise leave against a type the tenant had retired (closure plan L-35).
        if (!leaveType.IsActive)
        {
            throw new InvalidOperationException(
                $"'{leaveType.Name}' has been retired and cannot be requested. Existing requests are unaffected.");
        }

        // A retired sub-type is refused for the same reason. Its cap is still honoured on requests
        // already made against it; what stops is raising new ones.
        if (dto.LeaveSubTypeId is Guid subTypeId)
        {
            var subType = await _leaveSubTypeRepository.GetByIdAsync(subTypeId);
            if (subType == null || subType.TenantId != GetTenantId() || subType.LeaveTypeId != leaveType.Id)
                throw new ArgumentException($"Leave sub-type '{subTypeId}' not found.");
            if (!subType.IsActive)
                throw new InvalidOperationException(
                    $"'{subType.SubTypeName}' has been retired and cannot be requested.");
        }

        // A request raised FROM a plan carries its id. The join has existed since the port and
        // nothing ever wrote it, so an approved plan dead-ended and the employee re-keyed their own
        // dates (closure plan L-9 / R-1). Now that a screen writes it, it needs guarding: the plan
        // must be this employee's, approved, and not already spent.
        if (dto.LeavePlanId is Guid planId)
            await ValidateLeavePlanLinkAsync(planId, dto.EmployeeId);

        // Check that the employee is eligible for this leave type (gender / org / position rules)
        var isEligible = await _leaveTypeService.IsEmployeeEligibleAsync(dto.LeaveTypeId, dto.EmployeeId);
        if (!isEligible)
        {
            throw new InvalidOperationException(
                "This employee does not meet the eligibility criteria for the selected leave type.");
        }

        // Service-access gate: e.g. annual leave requires 1 year of service before it can be taken.
        var isAccessible = await _entitlementService.IsAccessibleAsync(dto.EmployeeId, dto.LeaveTypeId, dto.StartDate);
        if (!isAccessible)
        {
            throw new InvalidOperationException(
                "This employee has not yet completed the minimum service period required to take this leave type.");
        }

        // Validate dates
        if (dto.StartDate < _clock.TodayUtc)
        {
            throw new InvalidOperationException("Leave start date cannot be in the past.");
        }

        if (dto.EndDate < dto.StartDate)
        {
            throw new InvalidOperationException("Leave end date must be after or equal to start date.");
        }

        // Minimum-notice rule: the request must be filed at least N days before it starts.
        // Skipped for drafts so an employee can save a draft any time.
        if (!dto.SaveAsDraft && leaveType.MinDaysNotice is int minNotice && minNotice > 0)
        {
            var noticeDays = dto.StartDate.DayNumber - _clock.TodayUtc.DayNumber;
            if (noticeDays < minNotice)
                throw new InvalidOperationException(
                    $"This leave type requires at least {minNotice} day(s) notice. Only {noticeDays} day(s) given.");
        }

        // Check for conflicting leave
        var hasConflict = await HasConflictingLeaveAsync(dto.EmployeeId, dto.StartDate, dto.EndDate, null);

        if (hasConflict)
        {
            throw new InvalidOperationException("Employee already has a leave request for this period.");
        }

        // Calculate total days
        var totalDays = await CalculateLeaveDaysAsync(dto.StartDate, dto.EndDate, leaveType);

        // Validate against the ACCRUED balance (you cannot take more than has accrued to date).
        // We do NOT persist the balance here — the recalculation service (called inside the
        // transaction below) creates and populates it.
        var currentYear = LeaveYear.For(dto.StartDate, await _leaveYear.StartMonthAsync());
        var tenantId = GetTenantId();
        var balance = await _leaveBalanceRepository.FirstOrDefaultAsync(
            lb => lb.TenantId == tenantId &&
                  lb.EmployeeId == dto.EmployeeId &&
                  lb.LeaveTypeId == dto.LeaveTypeId &&
                  lb.Year == currentYear);

        var accruedAvailable = await GetAccruedAvailableDaysAsync(
            dto.EmployeeId, dto.LeaveTypeId, dto.LeaveSubTypeId, currentYear, balance);
        if (accruedAvailable < totalDays)
        {
            throw new InvalidOperationException(
                $"Insufficient accrued leave balance. Available: {accruedAvailable} days, Requested: {totalDays} days");
        }

        // The sub-type's own annual cap, which nothing enforced before wave D (L-28 / decision D-2).
        if (dto.LeaveSubTypeId is Guid capSubTypeId)
            await EnsureSubTypeCapAsync(dto.EmployeeId, capSubTypeId, currentYear, totalDays, null);

        // Validate explicitly-chosen relievers (hard-fail on clash, as before).
        if (dto.RelieverEmployeeId.HasValue)
            await ValidateRelieverAsync(dto.RelieverEmployeeId.Value, dto.EmployeeId, dto.StartDate, dto.EndDate);
        if (dto.SecondRelieverEmployeeId.HasValue)
            await ValidateRelieverAsync(dto.SecondRelieverEmployeeId.Value, dto.EmployeeId, dto.StartDate, dto.EndDate);

        // Auto-fill empty reliever slots from the employee's pre-defined relievers (by priority),
        // skipping any that are unavailable for the requested period.
        await AutoFillRelieversAsync(dto);

        // Last resort: suggest the manager as primary reliever if still none.
        if (!dto.RelieverEmployeeId.HasValue && employee.ManagerId.HasValue)
            dto.RelieverEmployeeId = await SuggestRelieverAsync(employee.ManagerId.Value, dto.StartDate, dto.EndDate);

        // Reliever requirement: this leave type cannot be submitted without a reliever once all
        // auto-fill/suggestion attempts are exhausted. Drafts are exempt so they can be saved early.
        if (!dto.SaveAsDraft && leaveType.RequiresReliever && !dto.RelieverEmployeeId.HasValue)
            throw new InvalidOperationException(
                "This leave type requires a reliever, but none could be assigned. Please select a reliever.");

        var request = dto.ToEntity();
        request.TenantId = tenantId;
        request.RequestDate = _clock.UtcNow;
        request.TotalDays = totalDays;
        request.Status = dto.SaveAsDraft ? LeaveStatus.Draft : LeaveStatus.Pending;

        // Persist the request and recalculate its balance in one transaction so a recalc
        // failure cannot leave an orphaned request with a stale balance. The unique index on
        // (TenantId, RequestNumber) guards against duplicate numbers under concurrency; on a
        // collision we regenerate the number and retry.
        const int maxAttempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            request.RequestNumber = await GenerateRequestNumberAsync();
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await _leaveRepository.AddAsync(request);
                    await _unitOfWork.SaveChangesAsync(ct);
                    await _recalculationService.RecalculateAsync(dto.EmployeeId, dto.LeaveTypeId, currentYear);
                });
                break;
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && IsDuplicateRequestNumber(ex))
            {
                // Another request grabbed this number first; detach the failed insert and retry.
                _unitOfWork.ClearChangeTracker();
                _logger.LogWarning("Duplicate leave request number on attempt {Attempt}; regenerating.", attempt);
            }
        }

        _logger.LogInformation("Leave request created: {requestNumber}", request.RequestNumber);

        return await GetLeaveRequestByIdAsync(request.Id);
    }

    public async Task<LeaveRequestDto> UpdateDraftAsync(Guid id, CreateLeaveRequestDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.Draft)
            throw new InvalidOperationException("Only draft leave requests can be updated.");

        // Determine what has changed so we recalculate balance only when necessary.
        bool typeChanged  = request.LeaveTypeId != dto.LeaveTypeId;
        bool datesChanged = request.StartDate   != dto.StartDate || request.EndDate != dto.EndDate;
        bool needsBalanceRecalc = typeChanged || datesChanged;

        // If the leave type changed, re-validate eligibility.
        LeaveType leaveType;
        if (typeChanged)
        {
            leaveType = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);

            // Moving a draft ONTO a retired type is the same refusal as raising one against it
            // (L-35). A draft already on a type that has since been retired is left alone — the
            // check is on what is being chosen, not on what is already there.
            if (!leaveType.IsActive)
                throw new InvalidOperationException(
                    $"'{leaveType.Name}' has been retired and cannot be requested.");

            var isEligible = await _leaveTypeService.IsEmployeeEligibleAsync(dto.LeaveTypeId, request.EmployeeId);
            if (!isEligible)
                throw new InvalidOperationException(
                    "This employee does not meet the eligibility criteria for the selected leave type.");

            var isAccessible = await _entitlementService.IsAccessibleAsync(request.EmployeeId, dto.LeaveTypeId, dto.StartDate);
            if (!isAccessible)
                throw new InvalidOperationException(
                    "This employee has not yet completed the minimum service period required to take this leave type.");
        }
        else
        {
            leaveType = await GetOwnedLeaveTypeAsync(request.LeaveTypeId);
        }

        if (dto.EndDate < dto.StartDate)
            throw new InvalidOperationException("Leave end date must be after or equal to start date.");

        // Check for conflicting leave (exclude this request itself).
        if (datesChanged)
        {
            var hasConflict = await HasConflictingLeaveAsync(
                request.EmployeeId, dto.StartDate, dto.EndDate, id);
            if (hasConflict)
                throw new InvalidOperationException("Employee already has a leave request for this period.");
        }

        var totalDays = await CalculateLeaveDaysAsync(dto.StartDate, dto.EndDate, leaveType);

        // Validate balance only when dates or type have changed. A missing balance means the
        // employee still has their full entitlement; the recalculation service persists it later.
        if (needsBalanceRecalc)
        {
            var currentYear = LeaveYear.For(dto.StartDate, await _leaveYear.StartMonthAsync());
            var tenantId = GetTenantId();
            var balance = await _leaveBalanceRepository.FirstOrDefaultAsync(
                lb => lb.TenantId == tenantId &&
                      lb.EmployeeId == request.EmployeeId &&
                      lb.LeaveTypeId == dto.LeaveTypeId &&
                      lb.Year == currentYear);

            var accruedAvailable = await GetAccruedAvailableDaysAsync(
                request.EmployeeId, dto.LeaveTypeId, dto.LeaveSubTypeId, currentYear, balance);
            // Accrued availability + days already reserved by this draft = real headroom.
            var headroom = accruedAvailable + request.TotalDays;
            if (headroom < totalDays)
                throw new InvalidOperationException(
                    $"Insufficient accrued leave balance. Available: {headroom} days, Requested: {totalDays} days");
        }

        // Validate reliever if provided.
        if (dto.RelieverEmployeeId.HasValue)
        {
            await ValidateRelieverAsync(dto.RelieverEmployeeId.Value, request.EmployeeId, dto.StartDate, dto.EndDate);
        }

        // Store the old type/year so we can recalculate the previously-affected balance too.
        var oldLeaveTypeId = request.LeaveTypeId;
        var oldYear        = LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync());

        // Apply all mutable fields.
        request.LeaveTypeId        = dto.LeaveTypeId;
        request.LeaveSubTypeId     = dto.LeaveSubTypeId;
        request.StartDate          = dto.StartDate;
        request.EndDate            = dto.EndDate;
        request.TotalDays          = totalDays;
        request.Reason             = dto.Reason;
        request.RelieverEmployeeId = dto.RelieverEmployeeId;
        request.RelieverNotes      = dto.RelieverNotes;
        request.HandoverNotes      = dto.HandoverNotes;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);

            if (needsBalanceRecalc)
            {
                var newYear = LeaveYear.For(dto.StartDate, await _leaveYear.StartMonthAsync());
                // Recalculate the new balance bucket.
                await _recalculationService.RecalculateAsync(request.EmployeeId, dto.LeaveTypeId, newYear);
                // If the leave type or year changed, also fix up the old bucket.
                if (typeChanged || oldYear != newYear)
                    await _recalculationService.RecalculateAsync(request.EmployeeId, oldLeaveTypeId, oldYear);
            }
        });

        _logger.LogInformation("Draft leave request updated: {requestId}", id);
        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<bool> SubmitForApprovalAsync(Guid id)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.Pending && request.Status != LeaveStatus.Draft)
            throw new InvalidOperationException("Only Pending or Draft leave requests can be submitted for approval.");

        var userId = GetCurrentUserId();
        var leaveType = await GetOwnedLeaveTypeAsync(request.LeaveTypeId);

        // ⚠ BEFORE the auto-approval branch below, deliberately. A leave type that skips the
        // workflow would otherwise approve sick leave with no certificate attached and nobody ever
        // asked — which is the single worst outcome this gate exists to prevent, and exactly the
        // case a reader would assume was covered.
        await EnsureMedicalEvidenceAsync(request, leaveType);

        // Auto-approval: leave types configured with RequiresApproval = false skip the workflow
        // entirely and are approved on submission (balance deducted via the recalc, same as a
        // normal approval). Used for low-risk types (e.g. short casual leave).
        if (!leaveType.RequiresApproval)
        {
            request.Status = LeaveStatus.Approved;
            request.ApprovedById = userId == Guid.Empty ? null : userId;
            request.ApprovedDate = _clock.UtcNow;
            request.RejectionReason = null;

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _leaveRepository.UpdateAsync(request);
                await _unitOfWork.SaveChangesAsync(ct);
                await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()));
            });

            // Approved is approved, however it got there: a leave type that skips the workflow still
            // has to reach the attendance register, or its days never reach the payroll export.
            await ReconcileAttendanceAsync(request.Id);

            _logger.LogInformation("Leave request auto-approved (approval not required): {requestNumber}", request.RequestNumber);
            return true;
        }

        // ⚠ Note what this is NOT. The block above — LeaveType.RequiresApproval == false — is a
        // deliberate, configured auto-approval for low-risk leave types, and it is correct. This is
        // the other path: approval IS required for this type, and with no published definition the
        // engine returned Approved anyway, so the request was approved with nobody asked. The
        // configured opt-out is the right way to make a leave type auto-approve; falling through
        // the engine is not.
        //
        // Defence in depth — a LEAVE_REQUEST definition is seeded, so this bites only on a tenant
        // where seeding has not run. See HrWorkflowFallbackAuthority.
        var (workflowResult, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, id);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(request, submitOutcome, userId);

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave request submitted for approval: {requestNumber}", request.RequestNumber);
        return true;
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
    /// <summary>
    /// Refuses a submission that is missing the medical evidence its leave type requires
    /// (residue plan R-15a — excuse duty and the medical board).
    /// </summary>
    /// <remarks>
    /// <para><b>Two rules, both configured on the leave type, both inert until
    /// <c>RequiresMedicalCertificate</c> is switched on.</b></para>
    ///
    /// <list type="number">
    ///   <item><b>Excuse duty.</b> An absence longer than the self-certification period needs a
    ///   medical certificate. At or under it the employee's own word is enough.</item>
    ///   <item><b>The medical board.</b> Once cumulative days of this leave type in the year pass
    ///   the board threshold, a board's recommendation must be attached as well.</item>
    /// </list>
    ///
    /// <para>⚠ <b>The board rule counts the YEAR, not the request.</b> A per-request threshold is
    /// defeated by splitting one long absence into several short ones, which is precisely what
    /// somebody avoiding a board would do. Same reasoning that made the sub-type cap annual in the
    /// closure build (decision D-2).</para>
    ///
    /// <para>⚠ <b>The refusal names what is missing and what would satisfy it.</b> Eight create
    /// checks in this service already work that way. "Submission failed" sends somebody to HR; "this
    /// needs excuse duty because it is longer than 3 days" sends them to their doctor.</para>
    /// </remarks>
    private async Task EnsureMedicalEvidenceAsync(LeaveRequest request, LeaveType leaveType)
    {
        if (!leaveType.RequiresMedicalCertificate) return;

        var tenantId = GetTenantId();

        var attached = await _attachmentRepository
            .GetQueryable()
            .Where(a => a.TenantId == tenantId && a.LeaveRequestId == request.Id)
            .Select(a => a.EvidenceKind)
            .ToListAsync();

        // ── 1. Excuse duty ──────────────────────────────────────────────────────────────────
        if (request.TotalDays > leaveType.SelfCertificationDays
            && !attached.Contains(LeaveEvidenceKind.ExcuseDuty))
        {
            throw new InvalidOperationException(
                $"This is {request.TotalDays:0.##} day(s) of {leaveType.Name}, and anything longer than "
                + $"{leaveType.SelfCertificationDays} day(s) needs excuse duty — a medical certificate — "
                + "attached before it can be submitted.");
        }

        // ── 2. The medical board ────────────────────────────────────────────────────────────
        if (leaveType.MedicalBoardThresholdDays is not int boardThreshold) return;

        // Everything already taken on this leave type this year, plus what is being asked for now.
        // ⚠ The request itself is excluded from the query and added separately: it may already be
        // Pending (submit is reachable from Pending as well as Draft), and counting it twice would
        // send somebody to a board at half the real threshold.
        // ⚠ Entitlement plan C1: the leave year the request falls in, resolved once and asked of
        // the database as a RANGE. `r.StartDate.Year == request.StartDate.Year` could not survive a
        // leave year that starts anywhere but January.
        var boardYear = LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync());
        var boardYearStart = LeaveYear.StartOf(boardYear, await _leaveYear.StartMonthAsync());
        var boardYearEnd = LeaveYear.EndOf(boardYear, await _leaveYear.StartMonthAsync());

        var takenThisYear = await _leaveRepository
            .GetQueryable()
            .Where(r => r.TenantId == tenantId
                     && r.EmployeeId == request.EmployeeId
                     && r.LeaveTypeId == request.LeaveTypeId
                     && r.Id != request.Id
                     && r.StartDate >= boardYearStart && r.StartDate <= boardYearEnd
                     && (r.Status == LeaveStatus.Approved
                      || r.Status == LeaveStatus.InProgress
                      || r.Status == LeaveStatus.Completed))
            .SumAsync(r => (decimal?)r.TotalDays) ?? 0m;

        var cumulative = takenThisYear + request.TotalDays;

        if (cumulative <= boardThreshold) return;

        // ⚠ TWO ways to satisfy this, and the order matters for the message below.
        //
        // The strong form is a board that actually sat in the Medical module and CONCLUDED — a
        // record naming who ruled, on what finding, and when. The typed attachment remains the
        // other form, because plenty of organisations hold their boards on paper and file the
        // report; refusing them would make the rule unusable rather than rigorous.
        //
        // ⚠ Only Concluded counts. A board that has been requested or convened has not said
        // anything yet, and accepting one would let an absence through on the strength of a
        // meeting somebody has merely scheduled.
        var hasReport = attached.Contains(LeaveEvidenceKind.MedicalBoardRecommendation);

        if (!hasReport && request.MedicalBoardId is Guid boardId)
        {
            hasReport = await _medicalBoardRepository
                .GetQueryable()
                .AnyAsync(b => b.TenantId == tenantId
                            && b.Id == boardId
                            && b.EmployeeId == request.EmployeeId
                            && b.Status == MedicalBoardStatus.Concluded);
        }

        if (!hasReport)
        {
            throw new InvalidOperationException(
                $"This would take {leaveType.Name} to {cumulative:0.##} day(s) in {boardYear}, "
                + $"past the {boardThreshold}-day point at which a medical board must sit. Link a concluded "
                + "medical board, or attach its recommendation, before submitting.");
        }
    }

    private void RefuseSelfApproval(Guid subjectEmployeeId, string what)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == subjectEmployeeId)
            throw new InvalidOperationException(
                $"You cannot approve your own {what}. It has to be approved by someone else.");
    }

    public async Task<LeaveRequestDto> ApproveLeaveAsync(Guid id, ApproveLeaveDto dto)
    {
        var tenantId = GetTenantId();
        var request = await _leaveRepository
            .GetQueryable()
            .Include(lr => lr.Employee)
            .Include(lr => lr.LeaveType)
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.TenantId == tenantId)
            ?? throw new ArgumentException($"Leave request with ID '{id}' not found.");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        RefuseSelfApproval(request.EmployeeId, "leave request");

        var comments = dto.Comments ?? dto.ApprovalNotes;

        var approvalOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserService.Roles, EntityType, id, userId,
            "Approve", comments, "approve a leave request", HrPermissions.ApproveLeave);

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(request, approvalOutcome, userId);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after approval outcome
            await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()));
        });

        // One call for both directions. On a two-stage definition the first approval leaves the
        // request Pending at the HR step, and the reconciler correctly posts NOTHING then — days
        // must not be booked against leave nobody has finally approved.
        await ReconcileAttendanceAsync(request.Id);

        _logger.LogInformation("Leave request approved: {requestNumber}", request.RequestNumber);
        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> SuggestChangesAsync(Guid id, SuggestLeaveRequestChangesDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.Pending)
            throw new InvalidOperationException("Only a submitted leave request can be sent back with different dates.");

        if (dto.SuggestedEndDate < dto.SuggestedStartDate)
            throw new InvalidOperationException("The suggested end date cannot be before the start date.");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        RefuseSelfApproval(request.EmployeeId, "leave request");

        // ⚠ VALIDATE BEFORE TOUCHING THE WORKFLOW. The approver's dates face the same checks the
        // employee's did — overlap, the past, the balance — because validating only on ACCEPT put
        // the consequence of the approver's mistake on the employee.
        //
        // The ORDER is the part that matters, and getting it wrong is not cosmetic: the block below
        // CANCELS the live approval instance, so a refusal raised after it leaves the request at
        // Pending with no instance at all — unapprovable, unsubmittable, exactly the L-1 shape wave
        // A existed to fix. The hr-leave suite caught this on its first real run.
        var suggestedType = await GetOwnedLeaveTypeAsync(request.LeaveTypeId);
        await EnsureMovedDatesAreValidAsync(
            request, dto.SuggestedStartDate, dto.SuggestedEndDate, suggestedType);

        // A third decision verb beside approve and reject, and it needs both of their paths. With a
        // definition published the engine decides who may send a request back; with none, nobody
        // could - CanUserApproveAsync answers false for everyone and there would be no instance to
        // cancel - so the request would sit at Pending with cancellation as its only exit. Exactly
        // the reasoning LeavePlanService.SuggestChangesAsync carries; this is its mirror.
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId);
            if (!canApprove)
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            // Sending it back returns it to the employee, so the live approval is cancelled; a
            // fresh one starts when they answer.
            var cancelResult = await _workflowIntegrationService.CancelWorkflowAsync(
                EntityType, id, "Approver suggested alternative dates");
            if (!cancelResult.Success)
                throw new InvalidOperationException(cancelResult.Message ?? "Failed to update the approval workflow.");
        }
        else
        {
            HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserService.Roles,
                "send a leave request back with suggested dates",
                HrPermissions.ApproveLeave);
        }

        request.Status = LeaveStatus.ChangesSuggested;
        request.SuggestedStartDate = dto.SuggestedStartDate;
        request.SuggestedEndDate = dto.SuggestedEndDate;
        request.ManagerSuggestionNotes = dto.Notes;
        request.WorkflowInstanceId = null;
        request.ApprovedById = null;
        request.ApprovedDate = null;

        // The days stop being reserved while it is back with the employee: PendingDays is derived
        // from the request status by the recalculation service, so the balance must be re-derived
        // or the employee keeps seeing days held against a request nobody is considering.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()));
        });

        _logger.LogInformation("Leave request {number} sent back with suggested dates by {userId}",
            request.RequestNumber, userId);
        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> RespondToSuggestionAsync(Guid id, RespondToLeaveSuggestionDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.ChangesSuggested)
            throw new InvalidOperationException("This leave request has no suggested dates to respond to.");

        DateOnly newStart, newEnd;
        if (dto.Accept)
        {
            if (request.SuggestedStartDate == null || request.SuggestedEndDate == null)
                throw new InvalidOperationException("No suggested dates are available to accept.");
            newStart = request.SuggestedStartDate.Value;
            newEnd = request.SuggestedEndDate.Value;
        }
        else
        {
            if (dto.StartDate == null || dto.EndDate == null)
                throw new InvalidOperationException("Provide your preferred start and end dates.");
            newStart = dto.StartDate.Value;
            newEnd = dto.EndDate.Value;
        }

        if (newEnd < newStart)
            throw new InvalidOperationException("The end date cannot be before the start date.");

        // The dates are new, so the checks that police dates run again - the same ones a fresh
        // request faces. Accepting the approver own suggestion is NOT exempt: an approver can
        // propose a window that overlaps something else or that the balance will not carry, and
        // discovering it at the approval step would be worse than discovering it here.
        var leaveType = await GetOwnedLeaveTypeAsync(request.LeaveTypeId);
        await EnsureMovedDatesAreValidAsync(request, newStart, newEnd, leaveType);

        request.StartDate = newStart;
        request.EndDate = newEnd;
        request.TotalDays = await CalculateLeaveDaysAsync(newStart, newEnd, leaveType);

        // The suggestion is resolved either way - cleared before it goes back for approval.
        request.SuggestedStartDate = null;
        request.SuggestedEndDate = null;
        request.ManagerSuggestionNotes = null;

        // Back through the front door, with the same guard the first submit has: without it a
        // request sent back for changes would approve itself on its way in.
        var (workflowResult, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(request, submitOutcome, GetCurrentUserId());

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()));
        });

        _logger.LogInformation("Leave request {number} re-submitted after a suggestion ({mode})",
            request.RequestNumber, dto.Accept ? "accepted" : "countered");
        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> RescheduleAsync(Guid id, RescheduleLeaveRequestDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.Approved)
            throw new InvalidOperationException(
                "Only an approved leave request can be rescheduled. A draft or submitted request is edited instead.");

        if (request.ClosureDate.HasValue)
            throw new InvalidOperationException("This leave has already been closed and cannot be moved.");

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException(
                "Say why the leave is moving - a reschedule without a reason is what this path exists to prevent.");

        if (dto.StartDate == request.StartDate && dto.EndDate == request.EndDate)
            throw new InvalidOperationException("Those are the dates the request already has.");

        var leaveType = await GetOwnedLeaveTypeAsync(request.LeaveTypeId);
        await EnsureMovedDatesAreValidAsync(request, dto.StartDate, dto.EndDate, leaveType);

        // Keep what it used to say. Only the FIRST move records the original - a request moved
        // twice should still show the dates that were originally approved, not the dates of the
        // previous move.
        if (request.RescheduleCount == 0)
        {
            request.OriginalStartDate = request.StartDate;
            request.OriginalEndDate = request.EndDate;
        }

        var leftYear = LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync());

        request.StartDate = dto.StartDate;
        request.EndDate = dto.EndDate;
        request.TotalDays = await CalculateLeaveDaysAsync(dto.StartDate, dto.EndDate, leaveType);
        request.RescheduledDate = _clock.UtcNow;
        request.RescheduledById = _currentUserService.EmployeeId;
        request.RescheduleReason = dto.Reason.Trim();
        request.RescheduleCount += 1;

        // Moving the dates cancels any "yes, still going" given against the OLD ones.
        request.ObservanceConfirmedDate = null;
        request.ObservanceConfirmedById = null;

        // The approval does not survive the move, and that is decision D-5. An approval is an
        // approval OF DATES; carrying it across to different ones would let the record claim an
        // authority nobody gave. So the request goes back through approval, keeping its number and
        // its history - which is the whole reason this is not cancel-and-re-key.
        request.ApprovedById = null;
        request.ApprovedDate = null;
        request.WorkflowInstanceId = null;

        var (workflowResult, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to restart the approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(request, submitOutcome, GetCurrentUserId());

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()));
            // The move can cross a year boundary; the year it LEFT has to be re-derived too, or the
            // old year keeps counting days nobody is taking.
            if (leftYear != LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()))
                await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, leftYear);
        });

        // The dates it was approved for are no longer the dates, so those attendance days come off
        // now. The reconciler posts nothing in their place, because the move re-opened the approval
        // — attendance is written when leave is approved, not when it is asked for.
        await ReconcileAttendanceAsync(request.Id);

        _logger.LogInformation("Leave request {number} rescheduled to {start}..{end} (move {count})",
            request.RequestNumber, dto.StartDate, dto.EndDate, request.RescheduleCount);
        return await GetLeaveRequestByIdAsync(id);
    }

    /// <summary>
    /// Calls an employee back before their leave ends — curtailment (residue plan R-14).
    /// </summary>
    /// <remarks>
    /// <para><b>Why this is not any of the three things that already existed.</b> <i>Cancel</i>
    /// releases every day including the ones already taken; <i>Close</i> refuses before the end
    /// date; <i>Reschedule</i> records that the leave <b>moved</b>, which is a different fact and
    /// re-opens the approval. Before this, the only route was to cancel and re-key a shorter
    /// request, which loses the number and the approval and leaves the record claiming the leave
    /// was never validly granted.</para>
    ///
    /// <para><b>So this truncates and keeps everything else.</b> Days up to the recall stand as
    /// taken, days after are restored to the balance, and the request keeps its number, its status
    /// and its approval — because the leave <i>was</i> validly approved and then interrupted. The
    /// approval is deliberately NOT re-opened: unlike a reschedule, nobody is being asked to
    /// authorise dates they have not seen. The employer is standing on the approval it already
    /// gave and taking part of it back.</para>
    ///
    /// <para>⚠ <b>The balance is not adjusted here.</b> <c>UsedDays</c> derives from the approved
    /// requests, so shortening <c>TotalDays</c> and re-deriving is what gives the days back. Writing
    /// an adjustment as well would return them twice.</para>
    /// </remarks>
    public async Task<LeaveRequestDto> RecallAsync(Guid id, RecallLeaveRequestDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (!CountsAsTaken(request.Status))
            throw new InvalidOperationException(
                "Only leave that has been approved can be recalled. Nothing has been granted yet on this request.");

        if (request.ClosureDate.HasValue)
            throw new InvalidOperationException("This leave has already been closed, so there is nothing to recall from.");

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException(
                "Say why the employee is being recalled. A recall is the employer's act and the record has to carry its reason.");

        // ⚠ Not RefuseSelfApproval - that guard is about approving, and its message would be wrong
        // here. The rule is the same shape and the reason is different: a recall is something an
        // employer does TO somebody, so the subject cannot be the one doing it. Without this, an
        // employee could shorten their own approved leave and hand themselves the days back.
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == request.EmployeeId)
            throw new InvalidOperationException(
                "You cannot recall yourself from leave. A recall is the employer's act and has to be recorded by someone else.");

        if (dto.EffectiveDate <= request.StartDate)
            throw new InvalidOperationException(
                "A recall dated on or before the first day of the leave means none of it was taken. Cancel the request instead - "
                + "that is the action that releases every day.");

        if (dto.EffectiveDate > request.EndDate)
            throw new InvalidOperationException(
                $"This leave already ends on {request.EndDate:dd MMM yyyy}, so a recall from {dto.EffectiveDate:dd MMM yyyy} "
                + "would give nothing back. Close the leave instead.");

        var leaveType = await GetOwnedLeaveTypeAsync(request.LeaveTypeId);

        // The day before they are due back is the last day of leave.
        var newEndDate = dto.EffectiveDate.AddDays(-1);
        var daysBefore = request.TotalDays;
        var daysAfter = await CalculateLeaveDaysAsync(request.StartDate, newEndDate, leaveType);

        if (daysAfter >= daysBefore)
            throw new InvalidOperationException(
                "That recall date does not shorten the leave - every chargeable day falls before it. "
                + "Check the date against the leave type's weekend and holiday rules.");

        // Only the FIRST recall records what the request used to end on. A leave curtailed twice
        // should still show the end date that was approved, not the end date of the previous recall
        // - the same rule OriginalEndDate follows for reschedules, and the reason these are separate
        // columns: a request can be rescheduled and later recalled, and both facts have to survive.
        request.PreRecallEndDate ??= request.EndDate;

        request.EndDate = newEndDate;
        request.TotalDays = daysAfter;
        request.RecallEffectiveDate = dto.EffectiveDate;
        request.RecalledDate = _clock.UtcNow;
        request.RecalledById = _currentUserService.EmployeeId;
        request.RecallReason = dto.Reason.Trim();

        // Cumulative, so a second recall does not overwrite what the first gave back.
        request.DaysRestored = (request.DaysRestored ?? 0m) + (daysBefore - daysAfter);

        // A "yes, still going" was given against dates that no longer exist.
        request.ObservanceConfirmedDate = null;
        request.ObservanceConfirmedById = null;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()));
        });

        // ⚠ This is the call that makes the truncation real on the attendance register, and it only
        // works because PostAsync prunes. The status is still Approved, so the reconciler takes its
        // POST arm - and posting used to only ever add, which would have left the recalled tail
        // marking the employee on leave they are now back from. See ILeaveAttendancePostingService.
        await ReconcileAttendanceAsync(request.Id);

        _logger.LogInformation(
            "Leave request {number} recalled from {effective}: now ends {end}, {restored} day(s) restored",
            request.RequestNumber, dto.EffectiveDate, newEndDate, daysBefore - daysAfter);

        return await GetLeaveRequestByIdAsync(id);
    }

    /// <inheritdoc />
    public async Task<LeaveRequestDto> LinkMedicalBoardAsync(Guid id, Guid? medicalBoardId)
    {
        var request = await GetOwnedLeaveRequestAsync(id);
        var tenantId = GetTenantId();

        if (medicalBoardId is Guid boardId)
        {
            // ⚠ The board must be about THIS employee. Without that check a request could be
            // satisfied by somebody else's board — which is not a theoretical worry, because the
            // board number is the natural thing to paste and boards are requested in batches.
            var board = await _medicalBoardRepository
                .GetQueryable()
                .Where(b => b.TenantId == tenantId && b.Id == boardId)
                .Select(b => new { b.EmployeeId, b.Status })
                .FirstOrDefaultAsync();

            if (board is null)
                throw new ArgumentException($"Medical board '{boardId}' not found.");

            if (board.EmployeeId != request.EmployeeId)
                throw new InvalidOperationException(
                    "That medical board is about a different employee, so it cannot stand as evidence "
                    + "for this request.");

            // ⚠ A cancelled board is linkable to nothing. Note that a REQUESTED or CONVENED board
            // IS linkable on purpose: the board is usually asked for before it sits, and the request
            // should be able to say which board it is waiting on. The evidence gate is what insists
            // on Concluded — linking records intent, the gate enforces the rule.
            if (board.Status == MedicalBoardStatus.Cancelled)
                throw new InvalidOperationException(
                    "That medical board was cancelled, so it cannot stand as evidence.");
        }

        request.MedicalBoardId = medicalBoardId;

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> ConfirmObservanceAsync(Guid id)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.Approved)
            throw new InvalidOperationException("Only approved leave can be confirmed as going ahead.");

        if (request.ClosureDate.HasValue)
            throw new InvalidOperationException("This leave has already been closed.");

        // Confirming is the answer to "is this still going ahead?" - the other half of the reminder.
        // It moves no days and changes no status: it records that somebody was asked and answered,
        // which is exactly what was missing (closure plan R-7).
        request.ObservanceConfirmedDate = _clock.UtcNow;
        request.ObservanceConfirmedById = _currentUserService.EmployeeId;

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave request {number} confirmed as going ahead", request.RequestNumber);
        return await GetLeaveRequestByIdAsync(id);
    }

    /// <summary>
    /// The date checks a request faces when its dates MOVE - on accepting or countering a
    /// suggestion, and on rescheduling an approved request.
    /// </summary>
    /// <remarks>
    /// Deliberately a subset of the eight create checks. Eligibility and the service-access gate
    /// are not re-run: they are facts about the employee and the leave type, both already settled
    /// when the request was raised, and re-running them could refuse a move for a reason that has
    /// nothing to do with the move. What IS re-run is everything that depends on the dates
    /// themselves - the past, the order, the overlap, and whether the balance carries the new span.
    ///
    /// Minimum notice is deliberately NOT enforced here either. Notice protects the employer
    /// against surprise; a request already in the system is not a surprise, and applying it would
    /// make a request impossible to pull forward even when everyone agrees.
    /// </remarks>
    private async Task EnsureMovedDatesAreValidAsync(
        LeaveRequest request, DateOnly newStart, DateOnly newEnd, LeaveType leaveType)
    {
        if (newStart < _clock.TodayUtc)
            throw new InvalidOperationException("Leave cannot be moved to a date in the past.");

        if (newEnd < newStart)
            throw new InvalidOperationException("The end date cannot be before the start date.");

        var hasConflict = await HasConflictingLeaveAsync(request.EmployeeId, newStart, newEnd, request.Id);
        if (hasConflict)
            throw new InvalidOperationException("The employee already has leave booked over those dates.");

        var newDays = await CalculateLeaveDaysAsync(newStart, newEnd, leaveType);
        var year = LeaveYear.For(newStart, await _leaveYear.StartMonthAsync());
        var tenantId = GetTenantId();

        var balance = await _leaveBalanceRepository.FirstOrDefaultAsync(
            lb => lb.TenantId == tenantId &&
                  lb.EmployeeId == request.EmployeeId &&
                  lb.LeaveTypeId == request.LeaveTypeId &&
                  lb.Year == year);

        var available = await GetAccruedAvailableDaysAsync(
            request.EmployeeId, request.LeaveTypeId, request.LeaveSubTypeId, year, balance);

        // The days this request already holds in that year are its own and come back to it; without
        // adding them a request could fail to move onto dates it is itself the only claimant of.
        var headroom = available + (LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()) == year ? request.TotalDays : 0m);

        if (headroom < newDays)
            throw new InvalidOperationException(
                $"Insufficient leave balance for the new dates. Available: {headroom} days, needed: {newDays} days.");

        // A move can change the number of chargeable days, and can cross into a different year — so
        // the sub-type's annual cap is re-checked against the year the request is moving INTO. The
        // request excludes itself, or its current days would be counted twice.
        if (request.LeaveSubTypeId is Guid capSubTypeId)
            await EnsureSubTypeCapAsync(request.EmployeeId, capSubTypeId, year, newDays, request.Id);
    }

    public async Task<LeaveRequestDto> RejectLeaveAsync(Guid id, RejectLeaveDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var rejectionText = !string.IsNullOrWhiteSpace(dto.RejectionReason) ? dto.RejectionReason : "Rejected";

        var rejectionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserService.Roles, EntityType, id, userId,
            "Reject", rejectionText, "reject a leave request", HrPermissions.ApproveLeave);

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(request, rejectionOutcome, userId, rejectionText);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after rejection
            await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync()));
        });

        _logger.LogInformation("Leave request rejected: {requestNumber}", request.RequestNumber);
        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> GetLeaveRequestByIdAsync(Guid id)
    {
        var tenantId = GetTenantId();
        var request = await _leaveRepository
            .GetQueryable()
            .Include(la => la.Employee)
            .Include(la => la.LeaveType)
            .Include(la => la.LeaveSubType)
            .Include(la => la.RelieverEmployee)
            .Include(la => la.SecondRelieverEmployee)
            .FirstOrDefaultAsync(la => la.Id == id && la.TenantId == tenantId);

        if (request == null)
        {
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        }

        var dto = request.ToDto();

        // The attendance join, made visible. `LeaveRequest.AttendanceDays` was permanently empty
        // before wave D, so a screen could not tell approved leave that reached the register from
        // leave that did not — and the difference is what the payroll export reads (L-27).
        dto.AttendanceDaysRecorded = await _dailyAttendanceRepository
            .GetQueryable()
            .CountAsync(d => d.TenantId == tenantId
                          && d.LeaveRequestId == request.Id
                          && d.Status == StaffAttendanceStatus.OnLeave);

        // These actor columns are bare Guids with no navigation (see the entity's note on shadow
        // FKs), so their names are resolved here rather than Include()d.
        var actorIds = new[] { request.RescheduledById, request.ObservanceConfirmedById, request.RecalledById }
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

        if (actorIds.Count > 0)
        {
            var names = await _employeeRepository
                .GetQueryable()
                .Where(e => e.TenantId == tenantId && actorIds.Contains(e.Id))
                .Select(e => new { e.Id, e.FirstName, e.LastName })
                .ToListAsync();

            string? NameOf(Guid? actorId) => actorId is Guid a
                ? names.Where(n => n.Id == a)
                       .Select(n => $"{n.FirstName} {n.LastName}".Trim())
                       .FirstOrDefault()
                : null;

            dto.RescheduledByName = NameOf(request.RescheduledById);
            dto.ObservanceConfirmedByName = NameOf(request.ObservanceConfirmedById);
            dto.RecalledByName = NameOf(request.RecalledById);
        }

        // The plan it came from, named rather than shown as a Guid.
        if (request.LeavePlanId is Guid planId)
        {
            dto.LeavePlanReference = await _leavePlanRepository
                .GetQueryable()
                .Where(pl => pl.Id == planId && pl.TenantId == tenantId)
                .Select(pl => $"{pl.Year} plan, {pl.StartDate:yyyy-MM-dd} to {pl.EndDate:yyyy-MM-dd}")
                .FirstOrDefaultAsync();
        }

        return dto;
    }

    public async Task<LeaveRequestDto?> GetLeaveRequestByNumberAsync(string requestNumber)
    {
        var tenantId = GetTenantId();
        var request = await _leaveRepository
            .GetQueryable()
            .Include(lr => lr.Employee)
            .Include(lr => lr.LeaveType)
            .Include(lr => lr.LeaveSubType)
            .Include(lr => lr.RelieverEmployee)
            .Include(lr => lr.SecondRelieverEmployee)
            .FirstOrDefaultAsync(lr => lr.TenantId == tenantId && lr.RequestNumber == requestNumber);
        return request?.ToDto();
    }

    /// <summary>
    /// One employee's requests for a year, paged.
    /// </summary>
    /// <remarks>
    /// <paramref name="status"/> is filtered HERE rather than on the returned page. The screen used
    /// to fetch a page and filter it in the browser, so "show me the rejected ones" meant "show the
    /// rejected ones that happen to be on page 1" and the count beside it was the unfiltered total —
    /// it under-reported without ever saying so (closure plan L-7).
    /// </remarks>
    public async Task<PagedResult<LeaveRequestDto>> GetEmployeeLeaveHistoryAsync(
        Guid employeeId, int year, int pageNumber, int pageSize, LeaveStatus? status = null)
    {
        var tenantId = GetTenantId();
        // ⚠ Entitlement plan C1 — one employee's requests for a leave YEAR, as a date range.
        var historyStart = LeaveYear.StartOf(year, await _leaveYear.StartMonthAsync());
        var historyEnd = LeaveYear.EndOf(year, await _leaveYear.StartMonthAsync());
        var query = _leaveRepository
            .GetQueryable()
            .Include(la => la.LeaveType)
            .Include(la => la.RelieverEmployee)
            .Include(la => la.SecondRelieverEmployee)
            .Where(la => la.TenantId == tenantId && la.EmployeeId == employeeId
                      && la.StartDate >= historyStart && la.StartDate <= historyEnd);

        if (status.HasValue)
            query = query.Where(la => la.Status == status.Value);

        var totalCount = await query.CountAsync();

        var requests = await query
            .OrderByDescending(la => la.RequestDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // var requestDtos = _mapper.Map<List<LeaveRequestDto>>(requests);
        var requestDtos = requests.ToDtoList();

        return new PagedResult<LeaveRequestDto>
        {
            Items = requestDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// How many Pending requests the queue will ask the engine about.
    /// </summary>
    /// <remarks>
    /// The engine answers one request at a time (<c>CanUserApproveAsync</c> resolves the instance,
    /// its current step and the caller's roles), so the candidate set has to be bounded. 300 is
    /// several times the largest real approval queue — a December backlog is a couple of hundred —
    /// and the screen says when it has been reached rather than quietly showing a short list.
    /// </remarks>
    private const int ApprovalQueueScanCap = 300;

    public async Task<PagedResult<LeaveRequestDto>> GetMyPendingApprovalsAsync(
        int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var me = _currentUserService.EmployeeId;

        // Candidates: everything undecided in the tenant — NOT just this caller's direct reports.
        // Widening the candidate set is the whole fix: HR's confirmation step belongs to nobody's
        // reporting line, so a report-scoped query could never surface it (L-10).
        var candidates = await _leaveRepository
            .GetQueryable()
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .Include(r => r.LeaveSubType)
            .Include(r => r.RelieverEmployee)
            .Where(r => r.TenantId == tenantId
                     && r.Status == LeaveStatus.Pending
                     // Never queue somebody their own leave: RefuseSelfApproval would turn it down,
                     // so listing it would only be an invitation to be refused.
                     && (me == null || r.EmployeeId != me))
            .OrderBy(r => r.RequestDate)
            .Take(ApprovalQueueScanCap)
            .ToListAsync(ct);

        var mine = new List<LeaveRequest>();

        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            // The engine decides. This is N calls, which is why the scan is capped above.
            foreach (var request in candidates)
            {
                ct.ThrowIfCancellationRequested();
                if (await _workflowIntegrationService.CanUserApproveAsync(EntityType, request.Id, userId))
                    mine.Add(request);
            }
        }
        else
        {
            // No published definition. CanUserApproveAsync answers false for everybody then, so
            // asking it would produce an empty queue on a tenant where the fallback authority can
            // in fact decide — and the queue would be lying in the other direction. Mirror what
            // HrWorkflowFallbackAuthority actually permits.
            if (HrWorkflowFallbackAuthority.CanRuleWithoutWorkflow(
                    _currentUserService.Roles, HrPermissions.ApproveLeave))
            {
                mine.AddRange(candidates);
            }
        }

        var size = Math.Clamp(pageSize, 1, 100);
        var page = Math.Max(pageNumber, 1);

        return new PagedResult<LeaveRequestDto>
        {
            Items = mine.Skip((page - 1) * size).Take(size).ToList().ToDtoList(),
            TotalCount = mine.Count,
            Page = page,
            PageSize = size,
        };
    }

    public async Task<PagedResult<LeaveRequestDto>> GetPendingApprovalsAsync(Guid managerId, int pageNumber, int pageSize)
    {
        var tenantId = GetTenantId();
        var requests = (await _leaveRepository.GetPendingApprovalsForManagerAsync(managerId))
            .Where(r => r.TenantId == tenantId);

        var totalCount = requests.Count();
        var pagedrequests = requests
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // var requestDtos = _mapper.Map<List<LeaveRequestDto>>(pagedrequests);
        var requestDtos = pagedrequests.ToDtoList();

        return new PagedResult<LeaveRequestDto>
        {
            Items = requestDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<LeaveBalanceDto>> GetEmployeeLeaveBalancesAsync(Guid employeeId, int year)
    {
        var tenantId = GetTenantId();
        var balances = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.Employee)
            .Where(lb => lb.TenantId == tenantId && lb.EmployeeId == employeeId && lb.Year == year)
            .ToListAsync();

        var dtos = balances.ToDtoList();
        foreach (var (balance, dto) in balances.Zip(dtos))
            await ApplyLiveEntitlementAsync(balance, dto);

        return dtos;
    }

    public async Task<IEnumerable<LeaveBalanceDto>> GetAllLeaveBalancesAsync(int year, Guid? employeeId, Guid? leaveTypeId)
    {
        var tenantId = GetTenantId();
        var query = _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.LeaveSubType)
            .Include(lb => lb.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Where(lb => lb.TenantId == tenantId && lb.Year == year);

        if (employeeId.HasValue)
            query = query.Where(lb => lb.EmployeeId == employeeId.Value);

        if (leaveTypeId.HasValue)
            query = query.Where(lb => lb.LeaveTypeId == leaveTypeId.Value);

        var balances = await query
            .OrderBy(lb => lb.Employee.LastName)
            .ThenBy(lb => lb.Employee.FirstName)
            .ThenBy(lb => lb.LeaveType.Name)
            .ToListAsync();

        // Accrual is computed live, never stored — the same as the per-employee read above. Without
        // this the Accrued column on the balances screen simply repeated the stored Entitled figure,
        // which is the one number on that screen the create check does NOT use (closure plan L-3).
        // It costs a few queries per row; the screen carries employee and leave-type filters for
        // tenants where that becomes noticeable.
        var dtos = balances.ToDtoList();
        foreach (var (balance, dto) in balances.Zip(dtos))
            await ApplyLiveEntitlementAsync(balance, dto);

        return dtos;
    }

    /// <summary>
    /// Fills the two live figures a stored <c>LeaveBalance</c> row cannot hold: accrued-to-date, and
    /// the availability the create check actually enforces. Both come from one entitlement snapshot
    /// so a screen and the server can never disagree about what is left.
    /// </summary>
    private async Task ApplyLiveEntitlementAsync(LeaveBalance balance, LeaveBalanceDto dto)
    {
        var snapshot = await _entitlementService.GetSnapshotAsync(
            balance.EmployeeId, balance.LeaveTypeId, balance.LeaveSubTypeId, balance.Year);

        dto.AccruedToDateDays = snapshot.AccruedToDateDays;
        dto.AccruedAvailableDays = EnforcedAvailableDays(
            snapshot, balance.EntitledDays, balance.CarriedOverDays, balance.AdjustmentDays,
            balance.UsedDays, balance.PendingDays, balance.EncashedDays);
    }

    /// <summary>
    /// The one definition of "days you can actually take right now".
    /// </summary>
    /// <remarks>
    /// ⚠ The arithmetic moved onto <see cref="LeaveEntitlementSnapshot.AvailableFrom"/> when the
    /// year-end runs became a third caller (entitlement plan W2b). This stays as the name the rest
    /// of this file reads by; it must not grow a body of its own again.
    /// </remarks>
    private static decimal EnforcedAvailableDays(
        LeaveEntitlementSnapshot snapshot, decimal entitled, decimal carried, decimal adjust,
        decimal used, decimal pending, decimal encashed)
        => snapshot.AvailableFrom(entitled, carried, adjust, used, pending, encashed);

    public async Task<HrBulkActionResultDto> BulkDecideAsync(
        BulkLeaveDecisionDto dto, bool approve, CancellationToken ct = default)
    {
        var ids = dto.LeaveRequestIds.Distinct().ToList();

        var result = new HrBulkActionResultDto { RequestedCount = ids.Count };

        // A ceiling, because this is a loop of real service calls and each one starts a transaction,
        // recalculates a balance and may write attendance days. Fifty is a full approval queue page.
        if (ids.Count > 50)
            throw new InvalidOperationException("Decide at most 50 requests at a time.");

        if (!approve && string.IsNullOrWhiteSpace(dto.RejectionReason))
            throw new InvalidOperationException("A rejection reason is required.");

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // The SAME call the single-item endpoint makes — see the interface's remarks. Each
                // one re-checks that this caller may decide THIS request, so a batch cannot smuggle
                // through a request the caller was never assigned.
                if (approve)
                    await ApproveLeaveAsync(id, new ApproveLeaveDto { Comments = dto.Comments });
                else
                    await RejectLeaveAsync(id, new RejectLeaveDto { RejectionReason = dto.RejectionReason! });

                result.SucceededCount++;
                result.Results.Add(new HrBulkActionItemResult { Id = id, Success = true });
            }
            catch (Exception ex) when (ex is InvalidOperationException
                                        or UnauthorizedAccessException
                                        or ArgumentException)
            {
                // One item's refusal must not abandon the rest — and the reason is kept, because
                // the handful that failed is exactly what the person needs to see.
                result.Results.Add(new HrBulkActionItemResult
                {
                    Id = id, Success = false, Reason = ex.Message,
                });
                _logger.LogInformation(
                    "Bulk leave decision skipped {id}: {reason}", id, ex.Message);
            }
        }

        _logger.LogInformation(
            "Bulk leave {verb}: {ok} of {total} succeeded",
            approve ? "approval" : "rejection", result.SucceededCount, result.RequestedCount);

        return result;
    }

    /// <summary>The register query, shared by the paged read and the CSV so they cannot disagree.</summary>
    private IQueryable<LeaveRequest> BuildRegisterQuery(LeaveRegisterFilterDto filter)
    {
        var tenantId = GetTenantId();

        var query = _leaveRepository
            .GetQueryable()
            .Include(r => r.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(r => r.LeaveType)
            .Include(r => r.LeaveSubType)
            .Include(r => r.RelieverEmployee)
            .Where(r => r.TenantId == tenantId);

        // Overlap, not containment — a request running from December into January belongs in a
        // December register as much as a January one.
        if (filter.From.HasValue)
            query = query.Where(r => r.EndDate >= filter.From.Value);
        if (filter.To.HasValue)
            query = query.Where(r => r.StartDate <= filter.To.Value);

        if (filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);
        if (filter.LeaveTypeId.HasValue)
            query = query.Where(r => r.LeaveTypeId == filter.LeaveTypeId.Value);
        if (filter.EmployeeId.HasValue)
            query = query.Where(r => r.EmployeeId == filter.EmployeeId.Value);
        if (filter.OrganizationUnitId.HasValue)
            query = query.Where(r => r.Employee.OrganizationUnitId == filter.OrganizationUnitId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(r =>
                r.RequestNumber.Contains(term) ||
                r.Employee.FirstName.Contains(term) ||
                r.Employee.LastName.Contains(term) ||
                r.Employee.EmployeeNumber.Contains(term));
        }

        return query;
    }

    public async Task<PagedResult<LeaveRequestDto>> GetRegisterAsync(
        LeaveRegisterFilterDto filter, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = BuildRegisterQuery(filter);

        var totalCount = await query.CountAsync(ct);

        var size = Math.Clamp(pageSize, 1, 200);
        var page = Math.Max(pageNumber, 1);

        var rows = await query
            .OrderByDescending(r => r.StartDate)
            .ThenBy(r => r.Employee.LastName)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<LeaveRequestDto>
        {
            Items = rows.ToDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = size,
        };
    }

    /// <summary>
    /// How many rows one export may carry.
    /// </summary>
    /// <remarks>
    /// An export is a spreadsheet somebody opens, not a data dump. Without a ceiling a single
    /// unfiltered click builds the tenant's whole leave history in memory and streams it — so the
    /// cap is here, and the CSV says when it has been reached rather than silently truncating.
    /// </remarks>
    private const int ExportRowCap = 10_000;

    public async Task<byte[]> ExportRegisterCsvAsync(
        LeaveRegisterFilterDto filter, CancellationToken ct = default)
    {
        var rows = await BuildRegisterQuery(filter)
            .OrderByDescending(r => r.StartDate)
            .ThenBy(r => r.Employee.LastName)
            .Take(ExportRowCap + 1)
            .ToListAsync(ct);

        var truncated = rows.Count > ExportRowCap;
        if (truncated) rows = rows.Take(ExportRowCap).ToList();

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", new[]
        {
            "Request number", "Employee number", "Employee", "Organisation unit",
            "Leave type", "Sub-type", "Start date", "End date", "Days", "Status",
            "Requested on", "Reliever", "Closed on",
        }.Select(CsvCell)));

        foreach (var r in rows)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                r.RequestNumber,
                r.Employee?.EmployeeNumber ?? string.Empty,
                r.Employee?.FullName ?? string.Empty,
                r.Employee?.OrganizationUnit?.Name ?? string.Empty,
                r.LeaveType?.Name ?? string.Empty,
                r.LeaveSubType?.SubTypeName ?? string.Empty,
                r.StartDate.ToString("yyyy-MM-dd"),
                r.EndDate.ToString("yyyy-MM-dd"),
                r.TotalDays.ToString("0.##"),
                r.Status.ToString(),
                r.RequestDate.ToString("yyyy-MM-dd"),
                r.RelieverEmployee?.FullName ?? string.Empty,
                r.ClosureDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            }.Select(CsvCell)));
        }

        if (truncated)
            csv.AppendLine(CsvCell($"Truncated at {ExportRowCap} rows — narrow the filters for the rest."));

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    public async Task<byte[]> ExportBalancesCsvAsync(
        int year, Guid? employeeId, Guid? leaveTypeId, CancellationToken ct = default)
    {
        // Reuses the screen's own read, so the CSV carries the LIVE accrued and enforced-available
        // figures rather than a second, simpler derivation that would quietly disagree with it.
        var balances = (await GetAllLeaveBalancesAsync(year, employeeId, leaveTypeId)).ToList();

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", new[]
        {
            "Employee", "Organisation unit", "Leave type", "Sub-type", "Year",
            "Entitled", "Accrued to date", "Carried over", "Adjustments",
            "Used", "Pending", "Encashed", "Available (policy)", "Can take now",
        }.Select(CsvCell)));

        foreach (var b in balances)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                b.EmployeeName,
                b.OrganizationUnitName ?? string.Empty,
                b.LeaveTypeName,
                b.LeaveSubTypeName ?? string.Empty,
                b.Year.ToString(),
                b.EntitledDays.ToString("0.##"),
                b.AccruedToDateDays.ToString("0.##"),
                b.CarriedOverDays.ToString("0.##"),
                b.AdjustmentDays.ToString("0.##"),
                b.UsedDays.ToString("0.##"),
                b.PendingDays.ToString("0.##"),
                b.EncashedDays.ToString("0.##"),
                b.AvailableDays.ToString("0.##"),
                b.AccruedAvailableDays.ToString("0.##"),
            }.Select(CsvCell)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    public async Task<byte[]> ExportComplianceCsvAsync(int year, CancellationToken ct = default)
    {
        var rows = (await GetMandatoryLeaveComplianceAsync(year)).ToList();

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", new[]
        {
            "Employee", "Staff number", "Organisation unit",
            "Leave type", "Year", "Entitled", "Taken", "Scheduled", "Outstanding", "Status",
        }.Select(CsvCell)));

        foreach (var r in rows)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                r.EmployeeName,
                r.EmployeeNumber,
                r.OrganizationUnitName ?? string.Empty,
                r.LeaveTypeName,
                r.Year.ToString(),
                r.EntitledDays.ToString("0.##"),
                r.TakenDays.ToString("0.##"),
                r.ScheduledDays.ToString("0.##"),
                r.OutstandingDays.ToString("0.##"),
                r.Status,
            }.Select(CsvCell)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    /// <summary>
    /// One CSV cell, quoted and escaped.
    /// </summary>
    /// <remarks>
    /// ⚠ The leading apostrophe on a cell starting with =, +, - or @ is not cosmetic: without it a
    /// name or a note beginning with one of those is executed as a formula when the file is opened
    /// in Excel, which is the CSV-injection hole. Everything is quoted, and embedded quotes are
    /// doubled.
    /// </remarks>
    private static string CsvCell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && (text[0] is '=' or '+' or '-' or '@'))
            text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    public async Task<LeaveCalendarDto> GetCalendarAsync(
        DateOnly from, DateOnly to, LeaveCalendarScope scope, Guid? employeeId,
        Guid? leaveTypeId, Guid? organizationUnitId, CancellationToken ct = default)
    {
        if (to < from)
            throw new InvalidOperationException("The calendar's end date cannot be before its start date.");

        // A calendar is a month or a quarter, not a decade. Without a ceiling one request can ask
        // for every leave row the tenant has ever had.
        if (to.DayNumber - from.DayNumber > 400)
            throw new InvalidOperationException("A leave calendar can cover at most 400 days at a time.");

        var tenantId = GetTenantId();

        // Overlap, not containment: leave that starts in June and ends in July belongs on BOTH
        // months' calendars. The same rule the holiday matching has always used.
        var query = _leaveRepository
            .GetQueryable()
            .Include(r => r.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(r => r.LeaveType)
            .Where(r => r.TenantId == tenantId
                     && r.StartDate <= to
                     && r.EndDate >= from
                     // Draft, rejected and cancelled leave is not time anybody is away.
                     && (r.Status == LeaveStatus.Approved
                      || r.Status == LeaveStatus.Pending
                      || r.Status == LeaveStatus.InProgress
                      || r.Status == LeaveStatus.Completed));

        switch (scope)
        {
            case LeaveCalendarScope.Mine:
                var me = employeeId ?? Guid.Empty;
                query = query.Where(r => r.EmployeeId == me);
                break;

            case LeaveCalendarScope.Team:
                // The caller's own direct reports, plus the caller. A manager planning cover needs
                // to see their own leave against the team's, not beside it on another screen.
                var managerId = employeeId ?? Guid.Empty;
                query = query.Where(r => r.EmployeeId == managerId
                                      || (r.Employee.ManagerId != null && r.Employee.ManagerId == managerId));
                break;

            case LeaveCalendarScope.Organisation:
            default:
                if (organizationUnitId.HasValue)
                    query = query.Where(r => r.Employee.OrganizationUnitId == organizationUnitId.Value);
                break;
        }

        if (leaveTypeId.HasValue)
            query = query.Where(r => r.LeaveTypeId == leaveTypeId.Value);

        var rows = await query
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.Employee.LastName)
            .ToListAsync(ct);

        var entries = rows.Select(r => new LeaveCalendarEntryDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            EmployeeId = r.EmployeeId,
            EmployeeName = r.Employee?.FullName ?? string.Empty,
            OrganizationUnitName = r.Employee?.OrganizationUnit?.Name,
            LeaveTypeId = r.LeaveTypeId,
            LeaveTypeName = r.LeaveType?.Name ?? string.Empty,
            CalendarColor = r.LeaveType?.CalendarColor,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            TotalDays = r.TotalDays,
            Status = r.Status,
            IsConfirmed = r.Status == LeaveStatus.Approved
                       || r.Status == LeaveStatus.InProgress
                       || r.Status == LeaveStatus.Completed,
        }).ToList();

        // Holidays underneath the bands, from the module's one holiday answer — so the calendar and
        // the day-counting agree about which days the tenant does not work (L-31/L-32), and the
        // reason a five-day leave charges four days is visible rather than mysterious.
        var holidayDates = await _workingDayCalculator.GetHolidayDatesAsync(tenantId, from, to, ct);
        var holidayNames = await _holidayNameLookupRepository
            .GetQueryable()
            .Where(h => h.TenantId == tenantId && h.IsActive && h.DateFrom <= to && h.DateTo >= from)
            .Select(h => new { h.HolidayName, h.DateFrom, h.DateTo, h.SubstitutionDate })
            .ToListAsync(ct);

        var named = new Dictionary<DateOnly, string>();
        foreach (var h in holidayNames)
        {
            for (var d = h.DateFrom; d <= h.DateTo; d = d.AddDays(1))
                named.TryAdd(d, h.HolidayName);
            if (h.SubstitutionDate is DateOnly sub)
                named.TryAdd(sub, $"{h.HolidayName} (observed)");
        }

        var holidays = holidayDates
            .OrderBy(d => d)
            .Select(d => new LeaveCalendarHolidayDto
            {
                Date = d,
                // The calculator is the authority on WHICH days are non-working; the names are a
                // convenience, so a date it returns without a matching row still shows.
                Name = named.TryGetValue(d, out var name) ? name : "Public holiday",
            })
            .ToList();

        return new LeaveCalendarDto
        {
            From = from,
            To = to,
            Scope = scope,
            Entries = entries,
            Holidays = holidays,
        };
    }

    public async Task<IEnumerable<MandatoryLeaveComplianceDto>> GetMandatoryLeaveComplianceAsync(int year)
    {
        // One pass over the balances for leave types flagged mandatory-to-take. Compliance is
        // measured against the entitlement the employee is required to use within the year:
        // taken (UsedDays) ≥ entitled = Compliant; pending covers the gap = Scheduled; else Outstanding.
        var tenantId = GetTenantId();
        var balances = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.Employee).ThenInclude(e => e.OrganizationUnit)
            .Where(lb => lb.TenantId == tenantId
                      && lb.Year == year
                      && lb.LeaveType.MandatoryAnnualLeave
                      && lb.LeaveType.IsActive
                      && !lb.Employee.IsDeleted)
            .OrderBy(lb => lb.Employee.LastName)
            .ThenBy(lb => lb.Employee.FirstName)
            .ThenBy(lb => lb.LeaveType.Name)
            .ToListAsync();

        return balances.Select(lb =>
        {
            var entitled = lb.EntitledDays + lb.CarriedOverDays;
            var taken = lb.UsedDays;
            var scheduled = lb.PendingDays;
            var outstanding = Math.Max(0m, entitled - taken - scheduled);

            string status = taken >= entitled ? "Compliant"
                          : (taken + scheduled) >= entitled ? "Scheduled"
                          : "Outstanding";

            return new MandatoryLeaveComplianceDto
            {
                EmployeeId = lb.EmployeeId,
                EmployeeName = lb.Employee?.FullName ?? string.Empty,
                EmployeeNumber = lb.Employee?.EmployeeNumber ?? string.Empty,
                OrganizationUnitId = lb.Employee?.OrganizationUnitId,
                OrganizationUnitName = lb.Employee?.OrganizationUnit?.Name,
                LeaveTypeId = lb.LeaveTypeId,
                LeaveTypeName = lb.LeaveType?.Name ?? string.Empty,
                Year = year,
                EntitledDays = entitled,
                TakenDays = taken,
                ScheduledDays = scheduled,
                OutstandingDays = outstanding,
                Status = status
            };
        }).ToList();
    }

    public async Task<LeaveBalanceDetailDto?> GetLeaveBalanceDetailAsync(Guid balanceId)
    {
        var tenantId = GetTenantId();
        var balance = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.LeaveSubType)
            .Include(lb => lb.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .FirstOrDefaultAsync(lb => lb.Id == balanceId && lb.TenantId == tenantId);

        if (balance == null) return null;

        // ⚠ Entitlement plan C1 — the balance's stored Year is a LABEL; these are its date bounds.
        var detailYearStart = LeaveYear.StartOf(balance.Year, await _leaveYear.StartMonthAsync());
        var detailYearEnd = LeaveYear.EndOf(balance.Year, await _leaveYear.StartMonthAsync());

        var requests = await _leaveRepository
            .GetQueryable()
            .Include(r => r.LeaveType)
            .Include(r => r.RelieverEmployee)
            .Include(r => r.SecondRelieverEmployee)
            .Where(r => r.TenantId == tenantId
                     && r.EmployeeId == balance.EmployeeId
                     && r.LeaveTypeId == balance.LeaveTypeId
                     && r.StartDate >= detailYearStart && r.StartDate <= detailYearEnd)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();

        var encashments = await _leaveEncashmentRepository
            .GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.ProcessedByEmployee)
            .Include(e => e.LeaveRequest).ThenInclude(lr => lr.LeaveType)
            .Where(e => e.TenantId == tenantId
                     && e.EmployeeId == balance.EmployeeId
                     && e.LeaveTypeId == balance.LeaveTypeId
                     && e.Year == balance.Year)
            .OrderByDescending(e => e.ProcessedDate)
            .ToListAsync();

        var adjustments = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .Where(a => a.TenantId == tenantId && a.LeaveBalanceId == balanceId)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync();

        var snapshot = await _entitlementService.GetSnapshotAsync(
            balance.EmployeeId, balance.LeaveTypeId, balance.LeaveSubTypeId, balance.Year);

        var dto = new LeaveBalanceDetailDto
        {
            Id                   = balance.Id,
            EmployeeId           = balance.EmployeeId,
            EmployeeName         = balance.Employee?.FullName ?? string.Empty,
            OrganizationUnitName = balance.Employee?.OrganizationUnit?.Name,
            LeaveTypeId          = balance.LeaveTypeId,
            LeaveTypeName        = balance.LeaveType?.Name ?? string.Empty,
            LeaveSubTypeId       = balance.LeaveSubTypeId,
            LeaveSubTypeName     = balance.LeaveSubType?.SubTypeName,
            Year                 = balance.Year,
            EntitledDays         = balance.EntitledDays,
            AccruedToDateDays    = snapshot.AccruedToDateDays,
            UsedDays             = balance.UsedDays,
            PendingDays          = balance.PendingDays,
            CarriedOverDays      = balance.CarriedOverDays,
            AdjustmentDays       = balance.AdjustmentDays,
            EncashedDays         = balance.EncashedDays,
            AvailableDays        = balance.AvailableDays,
            AccruedAvailableDays = EnforcedAvailableDays(
                                       snapshot, balance.EntitledDays, balance.CarriedOverDays,
                                       balance.AdjustmentDays, balance.UsedDays, balance.PendingDays,
                                       balance.EncashedDays),
            AllowCashConversion  = balance.LeaveType?.AllowCashConversion ?? false,
            Requests             = requests.ToDtoList(),
            Encashments          = encashments.ToDtoList(),
            Adjustments          = adjustments.ToDtoList()
        };

        return dto;
    }

    public async Task<bool> CancelLeaveRequestAsync(Guid id, string cancellationReason)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status == LeaveStatus.Cancelled || request.Status == LeaveStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot cancel leave request with status '{request.Status}'.");
        }

        var employeeId   = request.EmployeeId;
        var leaveTypeId  = request.LeaveTypeId;
        var year         = LeaveYear.For(request.StartDate, await _leaveYear.StartMonthAsync());

        request.Status = LeaveStatus.Cancelled;
        request.CancellationDate = _clock.UtcNow;
        request.CancellationReason = cancellationReason;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after cancellation
            await _recalculationService.RecalculateAsync(employeeId, leaveTypeId, year);
        });

        // The days are no longer being taken, so they come off the attendance register too —
        // otherwise a cancelled request keeps inflating DaysOnLeave on the monthly summary, and
        // with it the payroll export.
        await ReconcileAttendanceAsync(request.Id);

        return true;
    }

    public async Task<LeaveRequestDto> CloseLeaveRequestAsync(Guid id, CloseLeaveDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        // ⚠ InProgress closes too, and leaving it out would have been a deadlock. Leave that has
        // started is precisely the leave that reaches its end date and needs closing — and reminder
        // sweep 2 chases exactly those requests. Without this, the sweep would chase a request for
        // ever while the action it points at refused it.
        if (request.Status != LeaveStatus.Approved && request.Status != LeaveStatus.InProgress)
        {
            throw new InvalidOperationException("Only approved leave requests can be closed.");
        }

        if (request.EndDate > _clock.TodayUtc)
        {
            throw new InvalidOperationException("Cannot close leave request before end date.");
        }

        request.Status = LeaveStatus.Completed;
        request.ClosureDate = _clock.UtcNow;
        request.ClosureNotes = dto.ClosureNotes;

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return await GetLeaveRequestByIdAsync(id);
    }

    #endregion Leave CRUD operations

    #region Private helper methods

    /// <summary>
    /// Writes an approved request onto the attendance register, and takes it off again.
    /// </summary>
    /// <remarks>
    /// Both wrap <see cref="ILeaveAttendancePostingService"/> so the call sites stay one line, and
    /// both are deliberately BEST-EFFORT: a leave decision is not undone because the attendance
    /// register could not be written. An approval that rolled back at this point would leave the
    /// employee with neither leave nor an explanation, which is worse than a day the attendance
    /// desk has to fix. Failures are logged loudly and the skipped-day count is logged by the
    /// posting service itself.
    /// </remarks>
    /// <summary>Whether a request's status means its days are actually being taken.</summary>
    private static bool CountsAsTaken(LeaveStatus status)
        => status is LeaveStatus.Approved or LeaveStatus.InProgress or LeaveStatus.Completed;

    public Task ReconcileAttendanceAsync(Guid leaveRequestId, CancellationToken ct = default)
        => ReconcileAttendanceAsync(leaveRequestId, GetTenantId(), ct);

    /// <summary>
    /// The reconciler with an explicit tenant, for the nightly sweep — which runs with no signed-in
    /// user, so <see cref="GetTenantId"/> has nothing to read.
    /// </summary>
    private async Task ReconcileAttendanceAsync(Guid leaveRequestId, Guid tenantId, CancellationToken ct)
    {
        var request = await _leaveRepository
            .GetQueryable()
            .Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == leaveRequestId && r.TenantId == tenantId, ct);

        if (request == null) return;

        if (CountsAsTaken(request.Status))
            await PostAttendanceForApprovedLeaveAsync(request, tenantId);
        else
            await ReverseAttendanceForLeaveAsync(request, tenantId);
    }

    public async Task<int> ReconcileRecentAttendanceAsync(
        Guid tenantId, int lookbackDays = 14, CancellationToken ct = default)
    {
        var since = _clock.UtcNow.AddDays(-Math.Clamp(lookbackDays, 1, 365));

        // Only requests that changed recently, so this stays a sweep rather than a full rebuild.
        var candidates = await _leaveRepository
            .GetQueryable()
            .Include(r => r.LeaveType)
            .Where(r => r.TenantId == tenantId
                     && ((r.UpdatedAt ?? r.CreatedAt) >= since))
            .ToListAsync(ct);

        if (candidates.Count == 0) return 0;

        var ids = candidates.Select(r => r.Id).ToList();

        // How many OnLeave days each request currently has, in one query rather than per request.
        var posted = await _dailyAttendanceRepository
            .GetQueryable()
            .Where(d => d.TenantId == tenantId
                     && d.LeaveRequestId != null
                     && ids.Contains(d.LeaveRequestId!.Value)
                     && d.Status == StaffAttendanceStatus.OnLeave)
            .GroupBy(d => d.LeaveRequestId!.Value)
            .Select(g => new { RequestId = g.Key, Days = g.Count() })
            .ToListAsync(ct);

        var postedByRequest = posted.ToDictionary(x => x.RequestId, x => x.Days);

        var repaired = 0;
        foreach (var request in candidates)
        {
            ct.ThrowIfCancellationRequested();
            postedByRequest.TryGetValue(request.Id, out var days);

            var shouldHaveDays = CountsAsTaken(request.Status);

            // Drift is "should have days and has none" or "should have none and has some". A count
            // that merely differs from TotalDays is NOT drift — the posting step deliberately skips
            // days that already carried a real attendance observation.
            if (shouldHaveDays == (days > 0)) continue;

            await ReconcileAttendanceAsync(request.Id, tenantId, ct);
            repaired++;

            _logger.LogWarning(
                "Attendance drift repaired for leave request {number}: status {status}, had {days} day(s) posted",
                request.RequestNumber, request.Status, days);
        }

        if (repaired > 0)
        {
            _logger.LogWarning(
                "Leave attendance reconciliation repaired {count} request(s) for tenant {tenant} — " +
                "something changed a leave status without going through LeaveService",
                repaired, tenantId);
        }

        return repaired;
    }

    /// <summary>
    /// Advances approved leave that has started into <see cref="LeaveStatus.InProgress"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists at all.</b> <c>InProgress</c> had been in the enum since the port
    /// and <b>nothing anywhere assigned it</b> — eight places read or filtered on it and no code
    /// path ever set it, so the product had no notion of leave that is currently happening. That is
    /// the precondition for recall (R-14): "call this person back" only means something against
    /// leave somebody is actually on.</para>
    ///
    /// <para>⚠ <b>One-directional, on purpose.</b> Approved leave whose dates contain today becomes
    /// InProgress; nothing here moves a request back. Leave whose end date has passed stays
    /// InProgress until a human closes it — that is the same state a request reaches when its leave
    /// simply ends, and reminder sweep 2 chases both. Flipping statuses back and forth around a
    /// recall would churn the record and tell nobody anything.</para>
    ///
    /// <para>⚠ <b>This cannot move a balance, and that is by design.</b> Approved and InProgress are
    /// both counted in <c>UsedDays</c>, so the transition is invisible to the arithmetic. If the two
    /// are ever split across Used and Pending again, this sweep silently starts rewriting people's
    /// balances every morning.</para>
    /// </remarks>
    public async Task<int> AdvanceLeaveInProgressAsync(Guid tenantId, CancellationToken ct = default)
    {
        var today = _clock.TodayUtc;

        var starting = await _leaveRepository
            .GetQueryable()
            .Where(r => r.TenantId == tenantId
                     && r.Status == LeaveStatus.Approved
                     && r.StartDate <= today
                     && r.EndDate >= today)
            .ToListAsync(ct);

        if (starting.Count == 0) return 0;

        foreach (var request in starting)
        {
            ct.ThrowIfCancellationRequested();
            request.Status = LeaveStatus.InProgress;
            await _leaveRepository.UpdateAsync(request);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "{count} leave request(s) advanced to in-progress for tenant {tenant}", starting.Count, tenantId);

        return starting.Count;
    }

    private async Task PostAttendanceForApprovedLeaveAsync(LeaveRequest request, Guid tenantId)
    {
        try
        {
            var leaveType = request.LeaveType ?? await GetOwnedLeaveTypeAsync(request.LeaveTypeId);
            var days = await GetChargeableDaysAsync(request.StartDate, request.EndDate, leaveType, tenantId);
            var result = await _attendancePosting.PostAsync(request, tenantId, days);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Leave request {number}: {written} attendance day(s) marked on leave, {skipped} left alone, {pruned} dropped",
                request.RequestNumber, result.DaysWritten, result.DaysSkipped, result.DaysPruned);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Leave request {number} was approved but its attendance days could not be written",
                request.RequestNumber);
        }
    }

    private async Task ReverseAttendanceForLeaveAsync(LeaveRequest request, Guid tenantId)
    {
        try
        {
            var removed = await _attendancePosting.ReverseAsync(request.Id, tenantId);
            await _unitOfWork.SaveChangesAsync();

            if (removed > 0)
                _logger.LogInformation(
                    "Leave request {number}: {removed} attendance day(s) removed", request.RequestNumber, removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Leave request {number}: its attendance days could not be removed", request.RequestNumber);
        }
    }

    /// <summary>
    /// The individual dates a leave request is charged for, in order.
    /// </summary>
    /// <remarks>
    /// <para>The one definition of "which days does this leave actually cost". <see
    /// cref="CalculateLeaveDaysAsync"/> is its count, and the attendance posting walks the same list
    /// — so the number of <c>OnLeave</c> attendance days can never disagree with the request's
    /// <c>TotalDays</c>. Two separate walks over the same rules would drift the first time either
    /// side was edited.</para>
    ///
    /// <para>Holidays come from the module's shared calculator, so leave and HR's statutory clocks
    /// agree on which days the tenant does not work. Leave used to run its own query matching on
    /// tenant alone, which counted holidays from every calendar including retired ones, ignored
    /// <c>PublicHoliday.IsActive</c>, and never saw a <c>SubstitutionDate</c> — the day actually
    /// taken off when a holiday lands on a weekend (closure plan L-31/L-32). The overlap behaviour
    /// is kept: a Dec 30 – Jan 2 holiday still counts for a Jan 1 – 5 request, because the shared
    /// loader expands each holiday into its individual dates before the range is applied.</para>
    /// </remarks>
    private async Task<List<DateOnly>> GetChargeableDaysAsync(
        DateOnly startDate, DateOnly endDate, LeaveType leaveType, Guid? tenantIdOverride = null)
    {
        var days = new List<DateOnly>();
        if (endDate < startDate) return days;

        // The override exists for the nightly reconciliation, which has no ambient user.
        var tenantId = tenantIdOverride ?? GetTenantId();
        var holidayDates = await _workingDayCalculator.GetHolidayDatesAsync(tenantId, startDate, endDate);

        for (var currentDate = startDate; currentDate <= endDate; currentDate = currentDate.AddDays(1))
        {
            bool isWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday;
            bool isHoliday = holidayDates.Contains(currentDate);

            if (!leaveType.CountWeekendsAsLeave && isWeekend) continue;
            if (!leaveType.CountHolidaysAsLeave && isHoliday) continue;

            days.Add(currentDate);
        }

        return days;
    }

    private async Task<decimal> CalculateLeaveDaysAsync(DateOnly startDate, DateOnly endDate, LeaveType leaveType)
        => (await GetChargeableDaysAsync(startDate, endDate, leaveType)).Count;

    private async Task ValidateRelieverAsync(Guid relieverId, Guid employeeId, DateOnly startDate, DateOnly endDate)
    {
        if (relieverId == employeeId)
        {
            throw new InvalidOperationException("Employee cannot be their own reliever.");
        }

        var reliever = await GetOwnedEmployeeAsync(relieverId);

        if (reliever.StaffStatus != StaffStatus.Active)
        {
            throw new InvalidOperationException("Reliever must be an active employee.");
        }

        var hasConflict = await RelieverHasConflictAsync(relieverId, startDate, endDate);

        if (hasConflict)
        {
            throw new InvalidOperationException("Selected reliever is not available during the requested period.");
        }
    }

    /// <summary>
    /// Best-effort default reliever used only when the requester did not pick one. Currently
    /// proposes the employee's manager when they are free, otherwise returns null (no reliever
    /// auto-assigned — it stays optional unless the leave type requires one). This is superseded
    /// in Phase 3 by pre-defined relievers configured on the employee profile.
    /// </summary>
    private async Task<Guid?> SuggestRelieverAsync(Guid managerId, DateOnly startDate, DateOnly endDate)
    {
        var managerHasConflict = await RelieverHasConflictAsync(managerId, startDate, endDate);

        return managerHasConflict ? null : managerId;
    }

    /// <summary>
    /// Fills any empty reliever slot on the request from the employee's pre-defined relievers
    /// (priority 1 → primary, priority 2 → second), skipping relievers who are themselves on
    /// leave during the period. Explicit selections are left untouched.
    /// </summary>
    private async Task AutoFillRelieversAsync(CreateLeaveRequestDto dto)
    {
        if (dto.RelieverEmployeeId.HasValue && dto.SecondRelieverEmployeeId.HasValue)
            return;

        var tenantId = GetTenantId();
        var predefined = await _employeeRelieverRepository
            .GetQueryable()
            .Where(r => r.TenantId == tenantId && r.EmployeeId == dto.EmployeeId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        foreach (var pr in predefined)
        {
            if (pr.RelieverEmployeeId == dto.EmployeeId) continue;
            if (pr.RelieverEmployeeId == dto.RelieverEmployeeId || pr.RelieverEmployeeId == dto.SecondRelieverEmployeeId)
                continue;

            // Don't auto-assign a reliever who is unavailable (the user can still override on the form).
            var clash = await RelieverHasConflictAsync(pr.RelieverEmployeeId, dto.StartDate, dto.EndDate);
            if (clash) continue;

            if (!dto.RelieverEmployeeId.HasValue)
                dto.RelieverEmployeeId = pr.RelieverEmployeeId;
            else if (!dto.SecondRelieverEmployeeId.HasValue)
                dto.SecondRelieverEmployeeId = pr.RelieverEmployeeId;

            if (dto.RelieverEmployeeId.HasValue && dto.SecondRelieverEmployeeId.HasValue)
                break;
        }
    }

    /// <summary>
    /// Days the employee can actually take right now for a leave type/year: accrued-to-date plus
    /// carry-over and adjustments, less used/pending/encashed. For leave types without an accrual
    /// policy this equals the normal full-entitlement availability (no behavior change), and it
    /// respects a manual <c>EntitledDays</c> override on an existing balance.
    /// </summary>
    private async Task<decimal> GetAccruedAvailableDaysAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, LeaveBalance? balance)
    {
        var snapshot = await _entitlementService.GetSnapshotAsync(employeeId, leaveTypeId, leaveSubTypeId, year);

        return EnforcedAvailableDays(
            snapshot,
            balance?.EntitledDays    ?? snapshot.AnnualEntitledDays,
            balance?.CarriedOverDays ?? 0m,
            balance?.AdjustmentDays  ?? 0m,
            balance?.UsedDays        ?? 0m,
            balance?.PendingDays     ?? 0m,
            balance?.EncashedDays    ?? 0m);
    }

    /// <summary>
    /// Enforces a leave sub-type's <c>MaxDaysAllowed</c> across the employee's year.
    /// </summary>
    /// <remarks>
    /// <para><b>Decision D-2.</b> <c>LeaveBalance</c> is keyed on (employee, leave type, year) with
    /// no sub-type, so all of Sick's sub-types share one pot and a sub-type cap never reached a
    /// balance — the setting was configurable, saved, displayed back, and honoured by nothing
    /// (closure plan L-28). Giving every sub-type its own balance row would multiply every row on
    /// the Balances screen and every year-end run; this delivers what the setting promises for the
    /// cost of one query.</para>
    ///
    /// <para><b>⚠ It is an ANNUAL cap, not a per-request one.</b> The plan proposed checking a
    /// single request against the cap, which would be defeated by the obvious move of splitting one
    /// request into two. What <i>"caps days for this subtype"</i> means is the days taken on it in
    /// the year, so that is what is summed. Cancelled and rejected requests do not count; pending
    /// ones do, or two requests raised the same morning would both pass.</para>
    ///
    /// <para><paramref name="excludeRequestId"/> keeps a request from being counted against itself
    /// when its own dates are being changed.</para>
    /// </remarks>
    private async Task EnsureSubTypeCapAsync(
        Guid employeeId, Guid leaveSubTypeId, int year, decimal newDays, Guid? excludeRequestId)
    {
        var tenantId = GetTenantId();

        var subType = await _leaveSubTypeRepository.GetByIdAsync(leaveSubTypeId);
        if (subType == null || subType.TenantId != tenantId || subType.MaxDaysAllowed is not int cap || cap <= 0)
            return;

        // ⚠ Entitlement plan C1 — the sub-type cap is an ANNUAL cap, so it needs the leave year's
        // bounds rather than a calendar-year comparison.
        var capYearStart = LeaveYear.StartOf(year, await _leaveYear.StartMonthAsync());
        var capYearEnd = LeaveYear.EndOf(year, await _leaveYear.StartMonthAsync());

        var alreadyTaken = await _leaveRepository
            .GetQueryable()
            .Where(r => r.TenantId == tenantId
                     && r.EmployeeId == employeeId
                     && r.LeaveSubTypeId == leaveSubTypeId
                     && r.StartDate >= capYearStart && r.StartDate <= capYearEnd
                     && r.Status != LeaveStatus.Cancelled
                     && r.Status != LeaveStatus.Rejected
                     && r.Status != LeaveStatus.Draft
                     && (excludeRequestId == null || r.Id != excludeRequestId))
            .SumAsync(r => (decimal?)r.TotalDays) ?? 0m;

        if (alreadyTaken + newDays > cap)
        {
            var left = cap - alreadyTaken;
            throw new InvalidOperationException(
                $"'{subType.SubTypeName}' is capped at {cap} day(s) a year. " +
                (left > 0
                    ? $"{alreadyTaken} already taken or pending, so {left} day(s) remain and this request asks for {newDays}."
                    : $"{alreadyTaken} have already been taken or are pending, so none remain this year."));
        }
    }

    /// <summary>
    /// Guards the plan a request says it was raised from.
    /// </summary>
    /// <remarks>
    /// Four things can be wrong and each gets its own message, because "invalid plan" tells the
    /// employee nothing: the plan may not exist or belong to another tenant; it may be somebody
    /// else's; it may not be approved yet (an intention nobody has agreed to is not authority for
    /// anything); or a live request may already have been raised from it. A cancelled or rejected
    /// request does not spend a plan — the same rule the plan reads use.
    ///
    /// The leave TYPE is deliberately not forced to match. A plan says "I intend to be away then";
    /// which leave type the eventual request draws on can legitimately differ, and the eight create
    /// checks already police the type on its own merits.
    /// </remarks>
    private async Task ValidateLeavePlanLinkAsync(Guid planId, Guid employeeId)
    {
        var tenantId = GetTenantId();

        var plan = await _leavePlanRepository
            .GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == planId && p.TenantId == tenantId)
            ?? throw new ArgumentException($"Leave plan '{planId}' not found.");

        if (plan.EmployeeId != employeeId)
            throw new InvalidOperationException(
                "That leave plan belongs to a different employee and cannot be used for this request.");

        if (plan.Status != LeavePlanStatus.Approved)
            throw new InvalidOperationException(
                "Only an approved leave plan can be turned into a request. Submit the plan and have it approved first.");

        var alreadyRaised = await _leaveRepository
            .GetQueryable()
            .AnyAsync(r => r.TenantId == tenantId
                        && r.LeavePlanId == planId
                        && r.Status != LeaveStatus.Cancelled
                        && r.Status != LeaveStatus.Rejected);

        if (alreadyRaised)
            throw new InvalidOperationException(
                "A leave request has already been raised from this plan. Cancel that request first if the dates have changed.");
    }

    private static bool IsDuplicateRequestNumber(DbUpdateException ex)
    {
        // Provider-agnostic: the unique index name (IX_LeaveRequests_TenantId_RequestNumber)
        // and/or the column name appear in the violation message across SQL Server / Postgres.
        var message = (ex.InnerException?.Message ?? ex.Message);
        return message.Contains("RequestNumber", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issues the next leave request number, <c>LV{year}{seq:D6}</c>, from the tenant's atomic
    /// number sequence.
    /// </summary>
    /// <remarks>
    /// <para>Finish-plan lane 4 (2026-09-01). This used to be a max+1 scan over the LIVE rows:
    /// order by number, take the last, add one. Two things were wrong with that. Under concurrency
    /// two creates could read the same maximum (the unique index caught it and the retry loop
    /// below re-ran the same scan). And a soft-deleted request vanished from the scan while its
    /// number stayed in the unique index, so the next create was handed a number already taken —
    /// every retry recomputed the same collision and the create failed. Training, recruitment and
    /// staff numbering had already moved to <see cref="INumberSequenceService"/> for exactly this.</para>
    ///
    /// <para><b>Seeding.</b> Numbers already in the table were never issued by the counter. The first
    /// time the counter is used for a year it is advanced past the highest number on record for that
    /// year — deleted rows included, because those numbers are still in the index — so it keeps
    /// issuing after the numbers already in the wild rather than restarting at 000001. The seed
    /// runs only while the counter reads zero, so it costs one scan per tenant per year, not one per
    /// create. <see cref="INumberSequenceService.AdvanceToAtLeastAsync"/> only ever moves forward, so
    /// two concurrent first creates cannot lower it.</para>
    /// </remarks>
    private async Task<string> GenerateRequestNumberAsync()
    {
        // ⚠ The CALENDAR year, deliberately, and not the leave year (entitlement plan C1).
        //
        // `LV2026000001` is an identifier, not an entitlement period. Tying it to a leave year that
        // starts in April would mean a request raised in March 2026 is numbered LV2025…, which reads
        // as a filing error to everybody who handles it — and the number sequence is keyed on this
        // value, so the counter would restart in April rather than in January.
        //
        // Numbering follows the calendar because people do. Left as it is on purpose.
        var year = _clock.UtcNow.Year;
        var prefix = $"LV{year}";
        var tenantId = GetTenantId();

        if (await _numberSequence.PeekAsync(LeaveRequestSequenceKey, year) == 0)
        {
            // GetQueryableIncludingDeleted: the repository applies its soft-delete filter as a plain
            // Where, which IgnoreQueryFilters cannot remove — and a deleted request's number is
            // still in the unique index.
            var existing = await _leaveRepository
                .GetQueryableIncludingDeleted(lr => lr.TenantId == tenantId && lr.RequestNumber.StartsWith(prefix))
                .Select(lr => lr.RequestNumber)
                .ToListAsync();

            long highest = 0;
            foreach (var number in existing)
            {
                if (number.Length > prefix.Length
                    && long.TryParse(number.AsSpan(prefix.Length), out var value)
                    && value > highest)
                {
                    highest = value;
                }
            }

            if (highest > 0)
                await _numberSequence.AdvanceToAtLeastAsync(LeaveRequestSequenceKey, highest, year);
        }

        var next = await _numberSequence.NextAsync(LeaveRequestSequenceKey, year);
        return $"{prefix}{next:D6}";
    }

    private Guid GetCurrentUserId()
    {
        return Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// The employee the token belongs to, for columns that are Employee foreign keys (an
    /// adjustment's <c>PerformedBy</c>). Not the user id: a user id is never an employee id, and the
    /// column's constraint refuses it.
    /// </summary>
    /// <remarks>
    /// Finish-plan lane 4 (2026-09-01). The screen sent the login's user id and the service accepted
    /// it, so no adjustment raised from the desk had ever been saved (the table was empty). The
    /// house rule from <c>HrControllerBase.TryGetEmployeeWriteContext</c>: an actor column names an
    /// employee, and an unlinked account cannot be that actor.
    /// </remarks>
    private Guid RequireActingEmployeeId(string purpose)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty)
            return me;
        throw new InvalidOperationException(
            $"{purpose} requires your user account to be linked to an employee record. Please contact your administrator.");
    }

    #endregion Private helper methods

    #region Leave Adjustment methods

    public async Task<LeaveAdjustmentDto> AddAdjustmentAsync(CreateLeaveAdjustmentDto dto)
    {
        var tenantId = GetTenantId();
        var balance = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.Employee)
            .FirstOrDefaultAsync(lb => lb.Id == dto.LeaveBalanceId && lb.TenantId == tenantId)
            ?? throw new ArgumentException($"Leave balance with ID '{dto.LeaveBalanceId}' not found.");

        var adjustment = new LeaveAdjustment
        {
            TenantId       = tenantId,
            LeaveBalanceId = dto.LeaveBalanceId,
            EmployeeId     = balance.EmployeeId,
            LeaveTypeId    = balance.LeaveTypeId,
            LeaveSubTypeId = balance.LeaveSubTypeId,
            Year           = balance.Year,
            Days           = dto.Days,
            ReasonCodeId   = dto.ReasonCodeId,
            Reason         = dto.Reason,
            AdjustmentDate = _clock.UtcNow,
            PerformedBy    = RequireActingEmployeeId("Recording a leave adjustment")
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveAdjustmentRepository.AddAsync(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after adjustment
            await _recalculationService.RecalculateAsync(balance.EmployeeId, balance.LeaveTypeId, balance.Year);
        });

        // Reload to get navigation props
        var created = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .FirstOrDefaultAsync(a => a.Id == adjustment.Id && a.TenantId == tenantId)
            ?? adjustment;

        return created.ToDto();
    }

    public async Task<IEnumerable<LeaveAdjustmentDto>> GetAdjustmentsAsync(Guid balanceId)
    {
        await GetOwnedLeaveBalanceAsync(balanceId);

        var tenantId = GetTenantId();
        var adjustments = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .Where(a => a.TenantId == tenantId && a.LeaveBalanceId == balanceId)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync();

        return adjustments.ToDtoList();
    }

    public async Task<IEnumerable<LeaveAdjustmentDto>> GetAllAdjustmentsAsync(
        int year, Guid? employeeId, Guid? leaveTypeId, string? search)
    {
        var tenantId = GetTenantId();
        var query = _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .Where(a => a.TenantId == tenantId && a.Year == year);

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        if (leaveTypeId.HasValue)
            query = query.Where(a => a.LeaveTypeId == leaveTypeId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Reason.Contains(search));

        var adjustments = await query
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync();

        return adjustments.ToDtoList();
    }

    public async Task<LeaveAdjustmentDto?> GetAdjustmentByIdAsync(Guid id)
    {
        var tenantId = GetTenantId();
        var adjustment = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);

        return adjustment?.ToDto();
    }

    public async Task<LeaveAdjustmentDto> CreateStandaloneAdjustmentAsync(CreateLeaveAdjustmentStandaloneDto dto)
    {
        // Kept for its side effect: it refuses a leave type belonging to another tenant, before
        // anything is read or written. The entitlement engine resolves the days below.
        _ = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        var tenantId = GetTenantId();

        var balance = await _leaveBalanceRepository
            .GetQueryable()
            .FirstOrDefaultAsync(lb =>
                lb.TenantId == tenantId &&
                lb.EmployeeId  == dto.EmployeeId  &&
                lb.LeaveTypeId == dto.LeaveTypeId &&
                lb.Year        == dto.Year);

        if (balance == null)
        {
            // ⚠ Entitlement plan A1. This used to be `leaveType.DefaultDaysPerYear`, which skipped
            // the entitlement engine entirely — so a balance opened by posting an adjustment
            // recorded the leave TYPE's default instead of the EMPLOYEE's staff-level allocation.
            // The other two creation sites (LeaveBalanceRecalculationService and
            // LeaveYearEndService) always resolved it properly, so the same employee got a
            // different entitlement depending on which event happened to create their row first.
            //
            // ⚠ It mattered more than a wrong column suggests, because nothing refreshes
            // EntitledDays afterwards: the figure a row is born with is the figure it keeps. And
            // opening balances are loaded exactly this way — by adjustment — which is how a whole
            // tenant ends up on the default. RepairEntitlementsAsync is the pass that puts existing
            // rows right; this stops new ones being born wrong.
            var entitled = await _entitlementService.ResolveAnnualEntitlementAsync(
                dto.EmployeeId, dto.LeaveTypeId, dto.LeaveSubTypeId, dto.Year);

            balance = new LeaveBalance
            {
                TenantId        = tenantId,
                EmployeeId      = dto.EmployeeId,
                LeaveTypeId     = dto.LeaveTypeId,
                LeaveSubTypeId  = dto.LeaveSubTypeId,
                Year            = dto.Year,
                EntitledDays    = entitled,
                CarriedOverDays = 0,
                UsedDays        = 0,
                PendingDays     = 0,
                AdjustmentDays  = 0
            };
            await _leaveBalanceRepository.AddAsync(balance);
            await _unitOfWork.SaveChangesAsync();
        }

        // Stamped from the token, never from the payload — see RequireActingEmployeeId. The old
        // fallback here was GetCurrentUserId(), a USER id, which the Employee constraint refuses.
        var performedBy = RequireActingEmployeeId("Recording a leave adjustment");

        var adjustment = new LeaveAdjustment
        {
            TenantId       = tenantId,
            LeaveBalanceId = balance.Id,
            EmployeeId     = dto.EmployeeId,
            LeaveTypeId    = dto.LeaveTypeId,
            LeaveSubTypeId = dto.LeaveSubTypeId,
            Year           = dto.Year,
            Days           = dto.Days,
            ReasonCodeId   = dto.ReasonCodeId,
            Reason         = dto.Reason,
            AdjustmentDate = dto.AdjustmentDate?.ToUniversalTime() ?? _clock.UtcNow,
            PerformedBy    = performedBy
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveAdjustmentRepository.AddAsync(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(dto.EmployeeId, dto.LeaveTypeId, dto.Year);
        });

        var created = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .FirstOrDefaultAsync(a => a.Id == adjustment.Id && a.TenantId == tenantId)
            ?? adjustment;

        _logger.LogInformation(
            "Standalone adjustment created: employee={EmployeeId} type={LeaveTypeId} year={Year} days={Days}",
            dto.EmployeeId, dto.LeaveTypeId, dto.Year, dto.Days);

        return created.ToDto();
    }

    public async Task<LeaveAdjustmentDto> UpdateAdjustmentAsync(Guid id, UpdateLeaveAdjustmentDto dto)
    {
        var adjustment = await GetOwnedLeaveAdjustmentAsync(id);

        if (dto.Days == 0)
            throw new InvalidOperationException("Adjustment days cannot be zero.");

        adjustment.Days           = dto.Days;
        adjustment.ReasonCodeId   = dto.ReasonCodeId;
        adjustment.Reason         = dto.Reason;
        if (dto.AdjustmentDate.HasValue)
            adjustment.AdjustmentDate = dto.AdjustmentDate.Value.ToUniversalTime();

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveAdjustmentRepository.UpdateAsync(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(adjustment.EmployeeId, adjustment.LeaveTypeId, adjustment.Year);
        });

        var tenantId = GetTenantId();
        var updated = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId)
            ?? adjustment;

        _logger.LogInformation("Leave adjustment updated: id={AdjustmentId}", id);
        return updated.ToDto();
    }

    public async Task DeleteAdjustmentAsync(Guid adjustmentId)
    {
        var adjustment = await GetOwnedLeaveAdjustmentAsync(adjustmentId);

        var balanceForDelete = await GetOwnedLeaveBalanceAsync(adjustment.LeaveBalanceId);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveAdjustmentRepository.DeleteAsync(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after adjustment deletion
            await _recalculationService.RecalculateAsync(balanceForDelete.EmployeeId, balanceForDelete.LeaveTypeId, balanceForDelete.Year);
        });
    }

    #endregion Leave Adjustment methods

    #region Leave Request Attachment methods

    public async Task<LeaveRequestAttachmentDto> UploadAttachmentAsync(
        Guid leaveRequestId, Guid uploadedBy, string fileName, string filePath,
        string? contentType, long? fileSizeBytes,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null,
        LeaveEvidenceKind evidenceKind = LeaveEvidenceKind.Other)
    {
        var request = await GetOwnedLeaveRequestAsync(leaveRequestId);

        var attachment = new LeaveRequestAttachment
        {
            LeaveRequestId = leaveRequestId,
            FileName       = fileName,
            FilePath       = filePath,
            ContentType    = contentType,
            FileSizeBytes  = fileSizeBytes,
            UploadedDate   = _clock.UtcNow,
            UploadedBy     = uploadedBy,
            TenantId       = request.TenantId,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId   = documentRecordId,
            DocumentVersionId  = documentVersionId,
            EvidenceKind       = evidenceKind
        };

        await _attachmentRepository.AddAsync(attachment);
        await _unitOfWork.SaveChangesAsync();

        // Reload with navigation for name
        var saved = await GetOwnedAttachmentAsync(attachment.Id);
        return saved.ToDto();
    }

    public async Task<IEnumerable<LeaveRequestAttachmentDto>> GetAttachmentsAsync(Guid leaveRequestId)
    {
        await GetOwnedLeaveRequestAsync(leaveRequestId);

        var tenantId = GetTenantId();
        var all = await _attachmentRepository
            .GetQueryable()
            .Include(a => a.UploadedByEmployee)
            .Where(a => a.TenantId == tenantId && a.LeaveRequestId == leaveRequestId)
            .ToListAsync();
        return all.Select(a => a.ToDto()).ToList();
    }

    public async Task<LeaveRequestAttachmentDto?> GetAttachmentByIdAsync(Guid attachmentId)
    {
        var attachment = await _attachmentRepository
            .GetQueryable()
            .Include(a => a.UploadedByEmployee)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TenantId == GetTenantId());
        return attachment?.ToDto();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId)
    {
        var tenantId = GetTenantId();
        var attachment = await _attachmentRepository
            .GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TenantId == tenantId);
        if (attachment == null) return false;

        await _attachmentRepository.DeleteAsync(attachment);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    #endregion Leave Request Attachment methods
}