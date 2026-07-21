using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplinaryCaseService> _logger;

    public StaffDisciplinaryCaseService(
        IStaffDisciplinaryActionRepository caseRepository,
        IStaffDisciplineActionStepRepository actionStepRepository,
        IStaffDisciplineCorrectiveActionItemRepository correctiveItemRepository,
        IStaffDisciplineFineRepository fineRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplinaryCaseService> logger)
    {
        _caseRepository = caseRepository;
        _actionStepRepository = actionStepRepository;
        _correctiveItemRepository = correctiveItemRepository;
        _fineRepository = fineRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<StaffDisciplinaryActionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _caseRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<StaffDisciplinaryActionDto?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _caseRepository.GetByCaseNumberAsync(caseNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetAllOpenAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetOpenCasesAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffDisciplinaryActionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _caseRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.IncidentDate)
            .ThenByDescending(d => d.ReportedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

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
        var entities = await _caseRepository.GetByEmployeeAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByStatusAsync(DisciplinaryStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByOffenseAsync(Guid offenseId, CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetByOffenseAsync(offenseId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetBySeverityAsync(StaffOffenseSeverity minimumSeverity, CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetBySeverityAsync(minimumSeverity);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByIncidentDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetByIncidentDateRangeAsync(from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetByReportedDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetByReportedDateRangeAsync(from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingInvestigationAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetPendingInvestigationAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingHearingAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetPendingHearingAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetPendingClosureAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetPendingClosureAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveWarningAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetWithActiveWarningAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveSuspensionAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetWithActiveSuspensionAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithOutstandingFineAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetWithOutstandingFineAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithPendingTerminationAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetWithPendingTerminationAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveAppealAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetWithActiveAppealAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionSummaryDto>> GetWithActiveLegalReviewAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _caseRepository.GetWithActiveLegalReviewAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<int> GetOpenCaseCountForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => await _caseRepository.GetOpenCaseCountForEmployeeAsync(employeeId);

    public async Task<bool> CaseNumberExistsAsync(string caseNumber, Guid tenantId, CancellationToken cancellationToken = default)
        => await _caseRepository.CaseNumberExistsAsync(caseNumber, tenantId);

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<StaffDisciplinaryActionDto> CreateAsync(CreateStaffDisciplinaryActionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        if (string.IsNullOrWhiteSpace(entity.CaseNumber))
            entity.CaseNumber = await GenerateCaseNumberAsync(cancellationToken);

        entity.Status = DisciplinaryStatus.Draft;

        await _caseRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case created: {CaseNumber}", entity.CaseNumber);

        return entity.ToDto();
    }

    public async Task<StaffDisciplinaryActionDto> UpdateAsync(UpdateStaffDisciplinaryActionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _caseRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{updateDto.Id}' not found.");

        if (entity.Status == DisciplinaryStatus.Closed || entity.Status == DisciplinaryStatus.Dismissed)
            throw new InvalidOperationException("A closed or dismissed case cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case updated: {CaseNumber}", entity.CaseNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _caseRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{id}' not found.");

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
        var entity = await _caseRepository.GetByIdAsync(caseId);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");

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
        var entity = await _caseRepository.GetByIdAsync(caseId);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");

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

    public async Task<bool> RecordDecisionAsync(RecordDisciplinaryDecisionDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        var allowedStatuses = new[]
        {
            DisciplinaryStatus.UnderReview,
            DisciplinaryStatus.InvestigationComplete,
            DisciplinaryStatus.HearingConducted,
        };

        if (!allowedStatuses.Contains(entity.Status))
            throw new InvalidOperationException($"Cannot record a decision for a case in '{entity.Status}' status.");

        entity.ActionTypeId = dto.ActionTypeId;
        entity.ActionDetails = dto.ActionDetails;
        entity.DecisionRationale = dto.DecisionRationale;
        entity.DecisionDate = dto.DecisionDate;
        entity.DecisionById = dto.DecisionById;
        entity.Status = DisciplinaryStatus.AwaitingDecision;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = dto.DecisionById.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Decision recorded for case {CaseNumber}, ActionType: {ActionTypeId}", entity.CaseNumber, dto.ActionTypeId);

        return true;
    }

    public async Task<bool> CloseCaseAsync(CloseDisciplinaryCaseDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

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
        entity.ClosedById = dto.ClosedById;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = dto.ClosedById.ToString();

        await _caseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary case closed: {CaseNumber}", entity.CaseNumber);

        return true;
    }

    public async Task<bool> PutOnHoldAsync(Guid caseId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _caseRepository.GetByIdAsync(caseId);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");

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
        var entity = await _caseRepository.GetByIdAsync(caseId);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");

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
        var entity = await _caseRepository.GetByIdAsync(caseId);

        if (entity == null)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");

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
        var today = DateTime.UtcNow.Date;
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var caseQuery = _caseRepository.GetQueryable();

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
        var overdueSteps = await _actionStepRepository.GetOverdueStepsAsync();
        int overdueStepCount = overdueSteps.Count();

        // Overdue corrective action items
        var overdueItems = await _correctiveItemRepository.GetOverdueItemsAsync();
        int overdueItemCount = overdueItems.Count();

        // Outstanding fines
        var outstandingFines = await _fineRepository.GetOutstandingAsync();
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

    private async Task<string> GenerateCaseNumberAsync(CancellationToken cancellationToken)
    {
        var count = await _caseRepository.CountAsync();
        return $"DC-{DateTime.UtcNow.Year}-{(count + 1):D5}";
    }
}

#endregion
