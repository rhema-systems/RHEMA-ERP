using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF DISCIPLINARY CASE SERVICE
// ============================================================================

#region Staff Disciplinary Case Service

public class StaffDisciplinaryCaseService : IStaffDisciplinaryCaseService
{
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly IStaffDisciplineActionStepRepository _actionStepRepository;
    private readonly IStaffDisciplineCorrectiveActionItemRepository _correctiveItemRepository;
    private readonly IStaffDisciplineFineRepository _fineRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplinaryCaseService> _logger;

    public StaffDisciplinaryCaseService(
        IStaffDisciplinaryActionRepository caseRepository,
        IStaffDisciplineActionStepRepository actionStepRepository,
        IStaffDisciplineCorrectiveActionItemRepository correctiveItemRepository,
        IStaffDisciplineFineRepository fineRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplinaryCaseService> logger)
    {
        _caseRepository = caseRepository;
        _actionStepRepository = actionStepRepository;
        _correctiveItemRepository = correctiveItemRepository;
        _fineRepository = fineRepository;
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
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid id)
    {
        var entity = await _caseRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Disciplinary case with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// Validates a body-supplied employee id and returns the row.
    /// </summary>
    /// <remarks>
    /// Two jobs in one call, as in the SHE and staff-movement services. It stops an unknown or another
    /// tenant's id reaching the database as an FK violation (SQL 547, which surfaces as an unexplained
    /// 500 rather than "that employee was not found"), and because the row ends up tracked, EF fixes up
    /// the navigation on the entity being written — so the write response carries the subject's or
    /// reporter's name instead of an empty string, with no second read.
    /// </remarks>
    private async Task<Employee> GetOwnedEmployeeAsync(Guid employeeId, string role)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (employee == null || employee.IsDeleted || employee.TenantId != GetTenantId())
            throw new ArgumentException($"The {role} employee with ID '{employeeId}' was not found.");
        return employee;
    }

    /// <remarks>
    /// The offense is the case's classification and drives the procedure steps, so an id from another
    /// tenant's catalog would silently mis-file the case — the cross-tenant child-attach shape the SHE
    /// slices met repeatedly, here on the parent rather than a child.
    /// </remarks>
    private async Task<StaffOffense> GetOwnedOffenseAsync(Guid offenseId)
    {
        var offense = await _unitOfWork.Repository<StaffOffense>().GetByIdAsync(offenseId);
        if (offense == null || offense.IsDeleted || offense.TenantId != GetTenantId())
            throw new ArgumentException($"The offense with ID '{offenseId}' was not found.");
        return offense;
    }

    private async Task<StaffDisciplinaryActionType> GetOwnedActionTypeAsync(Guid actionTypeId)
    {
        var actionType = await _unitOfWork.Repository<StaffDisciplinaryActionType>().GetByIdAsync(actionTypeId);
        if (actionType == null || actionType.IsDeleted || actionType.TenantId != GetTenantId())
            throw new ArgumentException($"The disciplinary action type with ID '{actionTypeId}' was not found.");
        return actionType;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<StaffDisciplinaryActionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetWithFullDetailsAsync(tenantId, id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Disciplinary case with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<StaffDisciplinaryActionDto?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetByCaseNumberAsync(tenantId, caseNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetAllOpenAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetOpenCasesAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    /// <remarks>
    /// Paging moved into the repository so the register shares the one include set every other list
    /// read uses. Built here from a bare queryable, it loaded no navigations at all — so the main
    /// case list rendered blank employee and offense names, and every sanction badge read false,
    /// while the narrower quick views beside it showed them correctly.
    /// </remarks>
    public async Task<PagedResult<StaffDisciplinaryActionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _caseRepository.GetPagedAsync(GetTenantId(), pageNumber, pageSize);

        return new PagedResult<StaffDisciplinaryActionSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetByEmployeeAsync(tenantId, employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByStatusAsync(DisciplinaryStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetByStatusAsync(tenantId, status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByOffenseAsync(Guid offenseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetByOffenseAsync(tenantId, offenseId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetBySeverityAsync(StaffOffenseSeverity minimumSeverity, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetBySeverityAsync(tenantId, minimumSeverity);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByIncidentDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetByIncidentDateRangeAsync(tenantId, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByReportedDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetByReportedDateRangeAsync(tenantId, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingInvestigationAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetPendingInvestigationAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingHearingAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetPendingHearingAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingClosureAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetPendingClosureAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveWarningAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetWithActiveWarningAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveSuspensionAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetWithActiveSuspensionAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithOutstandingFineAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetWithOutstandingFineAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithPendingTerminationAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetWithPendingTerminationAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveAppealAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetWithActiveAppealAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveLegalReviewAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _caseRepository.GetWithActiveLegalReviewAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<int> GetOpenCaseCountForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await _caseRepository.GetOpenCaseCountForEmployeeAsync(GetTenantId(), employeeId);
    }

    public async Task<bool> CaseNumberExistsAsync(string caseNumber, Guid tenantId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        return await _caseRepository.CaseNumberExistsAsync(caseNumber, tenantId);
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<StaffDisciplinaryActionDto> CreateAsync(CreateStaffDisciplinaryActionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        // Guard every id the caller supplied before it reaches the database. Unvalidated, each of
        // these is an FK violation reported to the user as an unexplained 500; guarded, they also
        // leave the rows tracked, so the response below carries real names instead of empty strings.
        await GetOwnedEmployeeAsync(createDto.EmployeeId, "subject");
        await GetOwnedEmployeeAsync(createDto.ReportedById, "reporting");
        if (createDto.ReportedToId is Guid reportedTo)
            await GetOwnedEmployeeAsync(reportedTo, "reported-to");
        await GetOwnedOffenseAsync(createDto.StaffOffenseId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        if (string.IsNullOrWhiteSpace(entity.CaseNumber))
            entity.CaseNumber = await GenerateCaseNumberAsync(tenantId, cancellationToken);

        entity.Status = DisciplinaryStatus.Draft;

        await _caseRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case created: {CaseNumber}", entity.CaseNumber);

        return entity.ToDto();
    }

    public async Task<StaffDisciplinaryActionDto> UpdateAsync(UpdateStaffDisciplinaryActionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(updateDto.Id);

        if (entity.Status == DisciplinaryStatus.Closed || entity.Status == DisciplinaryStatus.Dismissed)
            throw new InvalidOperationException("A closed or dismissed case cannot be edited.");

        await GetOwnedEmployeeAsync(updateDto.ReportedById, "reporting");
        if (updateDto.ReportedToId is Guid reportedTo)
            await GetOwnedEmployeeAsync(reportedTo, "reported-to");
        await GetOwnedOffenseAsync(updateDto.StaffOffenseId);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case updated: {CaseNumber}", entity.CaseNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(id);

        if (entity.Status != DisciplinaryStatus.Draft)
            throw new InvalidOperationException("Only Draft cases can be deleted. Close or dismiss the case instead.");

        await _caseRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case deleted: {CaseNumber}", entity.CaseNumber);

        return true;
    }

    // ── Workflow transitions ──────────────────────────────────────────────────

    public async Task<bool> SubmitAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(caseId);

        if (entity.Status != DisciplinaryStatus.Draft)
            throw new InvalidOperationException("Only Draft cases can be submitted.");

        entity.Status = DisciplinaryStatus.Reported;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case submitted: {CaseNumber}", entity.CaseNumber);

        return true;
    }

    public async Task<bool> StartReviewAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(caseId);

        if (entity.Status != DisciplinaryStatus.Reported)
            throw new InvalidOperationException("Only Reported cases can be moved to UnderReview.");

        entity.Status = DisciplinaryStatus.UnderReview;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case moved to UnderReview: {CaseNumber}", entity.CaseNumber);

        return true;
    }

    public async Task<bool> RecordDecisionAsync(RecordDisciplinaryDecisionDto dto, Guid decidedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(dto.CaseId);

        var allowedStatuses = new[]
        {
            DisciplinaryStatus.UnderReview,
            DisciplinaryStatus.InvestigationComplete,
            DisciplinaryStatus.HearingConducted,
        };

        if (!allowedStatuses.Contains(entity.Status))
            throw new InvalidOperationException($"Cannot record a decision for a case in '{entity.Status}' status.");

        await GetOwnedActionTypeAsync(dto.ActionTypeId);

        entity.ActionTypeId = dto.ActionTypeId;
        entity.ActionDetails = dto.ActionDetails;
        entity.DecisionRationale = dto.DecisionRationale;
        entity.DecisionDate = dto.DecisionDate;
        entity.DecisionById = decidedByEmployeeId;
        entity.Status = DisciplinaryStatus.AwaitingDecision;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = decidedByEmployeeId.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Decision recorded for case {CaseNumber}, ActionType: {ActionTypeId}", entity.CaseNumber, dto.ActionTypeId);

        return true;
    }

    public async Task<bool> CloseCaseAsync(CloseDisciplinaryCaseDto dto, Guid closedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(dto.CaseId);

        var allowedStatuses = new[]
        {
            DisciplinaryStatus.AwaitingDecision,
            DisciplinaryStatus.DecisionMade,
            DisciplinaryStatus.UnderAppeal,
        };

        if (!allowedStatuses.Contains(entity.Status))
            throw new InvalidOperationException($"Cannot close a case in '{entity.Status}' status.");

        entity.Status = DisciplinaryStatus.Closed;
        entity.ClosedDate = dto.ClosedDate;
        entity.ClosureNotes = dto.ClosureNotes;
        entity.ClosedById = closedByEmployeeId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = closedByEmployeeId.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case closed: {CaseNumber}", entity.CaseNumber);

        return true;
    }

    public async Task<bool> PutOnHoldAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(caseId);

        var terminalStatuses = new[] { DisciplinaryStatus.Closed, DisciplinaryStatus.Dismissed, DisciplinaryStatus.OnHold };

        if (terminalStatuses.Contains(entity.Status))
            throw new InvalidOperationException($"Cannot put a case in '{entity.Status}' status on hold.");

        entity.Status = DisciplinaryStatus.OnHold;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case put on hold: {CaseNumber}", entity.CaseNumber);

        return true;
    }

    public async Task<bool> ReactivateCaseAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(caseId);

        if (entity.Status != DisciplinaryStatus.OnHold)
            throw new InvalidOperationException("Only OnHold cases can be reactivated.");

        entity.Status = DisciplinaryStatus.UnderReview;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case reactivated: {CaseNumber}", entity.CaseNumber);

        return true;
    }

    public async Task<bool> DismissCaseAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCaseAsync(caseId);

        var allowedStatuses = new[]
        {
            DisciplinaryStatus.Draft,
            DisciplinaryStatus.Reported,
            DisciplinaryStatus.UnderReview,
        };

        if (!allowedStatuses.Contains(entity.Status))
            throw new InvalidOperationException($"Cases in '{entity.Status}' status cannot be dismissed.");

        entity.Status = DisciplinaryStatus.Dismissed;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case dismissed: {CaseNumber}", entity.CaseNumber);

        return true;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<StaffDisciplineDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var today = DateTime.UtcNow.Date;
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var caseQuery = _caseRepository.GetQueryable().Where(d => d.TenantId == tenantId);

        var cases = await caseQuery.Select(d => new
        {
            d.Id,
            d.CaseNumber,
            EmployeeName = d.Employee.FirstName + " " + d.Employee.LastName,
            OffenseName = d.StaffOffense.OffenseName,
            d.Severity,
            d.Status,
            d.IncidentDate,
            d.ClosedDate,
            HasWarning         = d.Warning != null,
            HasSuspension      = d.Suspension != null
                              && d.Suspension.SuspensionStartDate <= DateTime.UtcNow
                              && (d.Suspension.SuspensionEndDate == null || d.Suspension.SuspensionEndDate >= DateTime.UtcNow),
            HasOutstandingFine = d.Fine != null
                              && d.Fine.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid,
            HasTerminationPending = d.Termination != null && !d.Termination.FinalPaycheckProcessed,
            HasActiveAppeal    = d.Appeal != null
                              && d.Appeal.AppealStatus != DisciplineAppealStatus.DecisionMade
                              && d.Appeal.AppealStatus != DisciplineAppealStatus.Dismissed,
        }).ToListAsync(cancellationToken);

        var terminalStatuses = new[] { DisciplinaryStatus.Closed, DisciplinaryStatus.Dismissed };
        var openCases = cases.Where(c => !terminalStatuses.Contains(c.Status)).ToList();

        // Overdue action steps
        var overdueSteps = await _actionStepRepository.GetOverdueStepsAsync(tenantId);
        int overdueStepCount = overdueSteps.Count();

        // Overdue corrective action items
        var overdueItems = await _correctiveItemRepository.GetOverdueItemsAsync(tenantId);
        int overdueItemCount = overdueItems.Count();

        // Outstanding fines
        var outstandingFines = await _fineRepository.GetOutstandingAsync(tenantId);
        int outstandingFineCount = outstandingFines.Count();

        // Recent open cases for alerts (newest 15)
        var recentOpenAlerts = openCases
            .OrderByDescending(c => c.IncidentDate)
            .Take(15)
            .Select(c => new DisciplinaryCaseAlertDto
            {
                CaseId       = c.Id,
                CaseNumber   = c.CaseNumber,
                EmployeeName = c.EmployeeName,
                OffenseName  = c.OffenseName,
                Severity     = c.Severity,
                Status       = c.Status,
                IncidentDate = c.IncidentDate,
                DaysOpen     = (int)(today - c.IncidentDate.Date).TotalDays,
            })
            .ToList();

        // Overdue cases — open cases where incident date is more than 30 days ago
        var overdueCaseAlerts = openCases
            .Where(c => (today - c.IncidentDate.Date).TotalDays > 30)
            .OrderBy(c => c.IncidentDate)
            .Take(20)
            .Select(c => new DisciplinaryCaseAlertDto
            {
                CaseId       = c.Id,
                CaseNumber   = c.CaseNumber,
                EmployeeName = c.EmployeeName,
                OffenseName  = c.OffenseName,
                Severity     = c.Severity,
                Status       = c.Status,
                IncidentDate = c.IncidentDate,
                DaysOpen     = (int)(today - c.IncidentDate.Date).TotalDays,
            })
            .ToList();

        return new StaffDisciplineDashboardDto
        {
            TotalOpenCases             = openCases.Count,
            CasesUnderInvestigation    = openCases.Count(c => c.Status == DisciplinaryStatus.UnderInvestigation),
            CasesAwaitingHearing       = openCases.Count(c => c.Status == DisciplinaryStatus.HearingScheduled),
            CasesAwaitingDecision      = openCases.Count(c => c.Status == DisciplinaryStatus.AwaitingDecision),
            CasesWithActiveAppeal      = openCases.Count(c => c.HasActiveAppeal),
            CasesClosedThisMonth       = cases.Count(c => c.Status == DisciplinaryStatus.Closed
                                             && c.ClosedDate.HasValue
                                             && c.ClosedDate.Value >= firstDayOfMonth),
            ActiveWarnings             = openCases.Count(c => c.HasWarning),
            ActiveSuspensions          = openCases.Count(c => c.HasSuspension),
            ActiveFines                = openCases.Count(c => c.HasOutstandingFine),
            TerminationsPendingProcessing = openCases.Count(c => c.HasTerminationPending),
            MinorCases                 = openCases.Count(c => c.Severity == StaffOffenseSeverity.Minor),
            ModerateCases              = openCases.Count(c => c.Severity == StaffOffenseSeverity.Moderate),
            SeriousCases               = openCases.Count(c => c.Severity == StaffOffenseSeverity.Serious),
            GrossMisconductCases       = openCases.Count(c => c.Severity == StaffOffenseSeverity.GrossMisconduct),
            OverdueActionSteps         = overdueStepCount,
            OverdueCorrectiveActionItems = overdueItemCount,
            OutstandingFines           = outstandingFineCount,
            RecentOpenCases            = recentOpenAlerts,
            OverdueCases               = overdueCaseAlerts,
            ComputedAt                 = DateTime.UtcNow,
        };
    }

    // ── Helper methods ────────────────────────────────────────────────────────

    /// <remarks>
    /// Numbers come from the highest suffix already issued, over rows INCLUDING soft-deleted ones —
    /// not from a count of live rows. Counting re-issues a number the moment anything is deleted: the
    /// count drops back, and the next case collides with a number still held by the deleted row's
    /// unique index. Soft-deleted rows keep their numbers, so they have to keep their place in the
    /// sequence too. The same shape was fixed across the SHE and staff-movement generators.
    /// </remarks>
    private async Task<string> GenerateCaseNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"DC-{DateTime.UtcNow.Year}-";

        var issued = await _caseRepository
            .GetQueryableIncludingDeleted(d => d.TenantId == tenantId && d.CaseNumber.StartsWith(prefix))
            .Select(d => d.CaseNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(number => int.TryParse(number[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
    }
}

#endregion
