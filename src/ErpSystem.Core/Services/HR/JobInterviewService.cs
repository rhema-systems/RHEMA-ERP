using System.Text;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// INTERVIEW QUESTION BANK SERVICE
// ============================================================================

public class JobInterviewQuestionBankService : IJobInterviewQuestionBankService
{
    private readonly IJobInterviewQuestionTypeRepository _questionTypeRepository;
    private readonly IJobInterviewQuestionDetailRepository _questionDetailRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobInterviewQuestionBankService> _logger;

    public JobInterviewQuestionBankService(
        IJobInterviewQuestionTypeRepository questionTypeRepository,
        IJobInterviewQuestionDetailRepository questionDetailRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<JobInterviewQuestionBankService> logger)
    {
        _questionTypeRepository = questionTypeRepository;
        _questionDetailRepository = questionDetailRepository;
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

    // ⚠ The question bank is recruitment setup data — the questions candidates will be asked and the
    // weights their answers are scored against — and is HR-only. That gate lives on
    // InterviewQuestionBankController rather than here: this service has exactly one controller and no
    // per-record rule to express, unlike JobInterviewService where a panelist's access depends on which
    // interview they sit on.

    // A question type owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobInterviewQuestionType> GetOwnedQuestionTypeAsync(Guid id)
    {
        var entity = await _questionTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview question type with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewQuestionDetail> GetOwnedQuestionDetailAsync(Guid id)
    {
        var entity = await _questionDetailRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview question detail with ID '{id}' not found.");
        return entity;
    }

    public async Task<JobInterviewQuestionTypeDto> GetQuestionTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionTypeAsync(id);
        return entity.ToDto();
    }

    /// <summary>
    /// Every question type in the bank, active or not.
    ///
    /// <para>This backs the question-bank admin list and used to call <c>GetActiveTypesAsync</c>, so a
    /// type someone had deactivated vanished from the only screen that could reactivate it — along with
    /// its questions, which remained attached to it. The interview-scheduling picker wants active types
    /// only and filters on <c>IsActive</c> for itself.</para>
    /// </summary>
    public async Task<IEnumerable<JobInterviewQuestionTypeSummaryDto>> GetAllQuestionTypesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _questionTypeRepository.FindAsync(t => !t.IsDeleted);
        return entities
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.TypeName)
            .Select(e => e.ToSummaryDto());
    }

    public async Task<JobInterviewQuestionTypeDto> CreateQuestionTypeAsync(CreateJobInterviewQuestionTypeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _questionTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<JobInterviewQuestionTypeDto> UpdateQuestionTypeAsync(UpdateJobInterviewQuestionTypeDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionTypeAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _questionTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteQuestionTypeAsync(Guid questionTypeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionTypeAsync(questionTypeId);

        await _questionTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewQuestionDetailDto> GetQuestionDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionDetailAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewQuestionDetailDto>> GetQuestionDetailsByTypeAsync(Guid questionTypeId, CancellationToken cancellationToken = default)
    {
        await GetOwnedQuestionTypeAsync(questionTypeId);
        var tenantId = GetTenantId();
        var entities = await _questionDetailRepository.GetByQuestionTypeIdAsync(questionTypeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewQuestionDetailDto>> GetAllQuestionDetailsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _questionDetailRepository.GetAllWithTypeAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<JobInterviewQuestionDetailDto> CreateQuestionDetailAsync(CreateJobInterviewQuestionDetailDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedQuestionTypeAsync(createDto.QuestionTypeId);

        var entity = createDto.ToEntity(current, createdByUserId);
        await _questionDetailRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<JobInterviewQuestionDetailDto> UpdateQuestionDetailAsync(UpdateJobInterviewQuestionDetailDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionDetailAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _questionDetailRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteQuestionDetailAsync(Guid questionDetailId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionDetailAsync(questionDetailId);

        await _questionDetailRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewQuestionTypeDto?> GetQuestionTypeByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _questionTypeRepository.GetByCodeAsync(code);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<JobInterviewQuestionTypeDto?> GetQuestionTypeWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _questionTypeRepository.GetWithQuestionsAsync(id);
        return entity != null && entity.TenantId == GetTenantId() ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<JobInterviewQuestionDetailDto>> GetActiveQuestionsAsync(Guid? questionTypeId = null, CancellationToken cancellationToken = default)
    {
        if (questionTypeId.HasValue)
            await GetOwnedQuestionTypeAsync(questionTypeId.Value);

        var tenantId = GetTenantId();
        var entities = await _questionDetailRepository.GetActiveQuestionsAsync(questionTypeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }
}

// ============================================================================
// INTERVIEW SERVICE
// ============================================================================

public class JobInterviewService : IJobInterviewService
{
    private readonly IJobInterviewRepository _interviewRepository;
    private readonly IJobInterviewPanelistRepository _panelistRepository;
    private readonly IJobInterviewExternalPanelistRepository _externalPanelistRepository;
    private readonly IJobIntervieweeRepository _intervieweeRepository;
    private readonly IJobInterviewQuestionRepository _questionPlanRepository;
    private readonly IJobInterviewSelectedQuestionRepository _selectedQuestionRepository;
    private readonly IJobInterviewQuestionDetailRepository _questionDetailRepository;
    private readonly IJobInterviewQuestionTypeRepository _questionTypeRepository;
    private readonly IJobInterviewScoreSummaryRepository _scoreSummaryRepository;
    private readonly IJobInterviewScoreEntryRepository _scoreEntryRepository;
    private readonly IJobInterviewScoreDraftRepository _draftRepository;
    private readonly IInterviewQuestionPresetRepository _presetRepository;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobInterviewService> _logger;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;
    // Cross-domain repositories used only for the advisory panelist-availability conflict check
    // and application/vacancy validation. Open-generic IGenericRepository<T> is DI-registered.
    private readonly IGenericRepository<JobApplication> _applicationRepository;
    private readonly IGenericRepository<Entities.HR.Employee> _employeeRepository;
    private readonly IGenericRepository<Entities.HR.ExternalAssociate> _associateRepository;
    private readonly IGenericRepository<Entities.HR.StaffLeave.LeaveRequest> _leaveRepository;
    private readonly IGenericRepository<Entities.HR.StaffTravel.StaffTravelRequest> _travelRepository;
    private readonly string _portalUrl;

    public JobInterviewService(
        IJobInterviewRepository interviewRepository,
        IJobInterviewPanelistRepository panelistRepository,
        IJobInterviewExternalPanelistRepository externalPanelistRepository,
        IJobIntervieweeRepository intervieweeRepository,
        IJobInterviewQuestionRepository questionPlanRepository,
        IJobInterviewSelectedQuestionRepository selectedQuestionRepository,
        IJobInterviewQuestionDetailRepository questionDetailRepository,
        IJobInterviewQuestionTypeRepository questionTypeRepository,
        IJobInterviewScoreSummaryRepository scoreSummaryRepository,
        IJobInterviewScoreEntryRepository scoreEntryRepository,
        IJobInterviewScoreDraftRepository draftRepository,
        IInterviewQuestionPresetRepository presetRepository,
        IApplicationPipelineService pipelineService,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<JobInterviewService> logger,
        IEmailService email,
        ITemplatedEmailService templatedEmail,
        IGenericRepository<JobApplication> applicationRepository,
        IGenericRepository<Entities.HR.Employee> employeeRepository,
        IGenericRepository<Entities.HR.ExternalAssociate> associateRepository,
        IGenericRepository<Entities.HR.StaffLeave.LeaveRequest> leaveRepository,
        IGenericRepository<Entities.HR.StaffTravel.StaffTravelRequest> travelRepository,
        IConfiguration configuration)
    {
        _interviewRepository = interviewRepository;
        _panelistRepository = panelistRepository;
        _externalPanelistRepository = externalPanelistRepository;
        _intervieweeRepository = intervieweeRepository;
        _questionPlanRepository = questionPlanRepository;
        _selectedQuestionRepository = selectedQuestionRepository;
        _questionDetailRepository = questionDetailRepository;
        _questionTypeRepository = questionTypeRepository;
        _scoreSummaryRepository = scoreSummaryRepository;
        _scoreEntryRepository = scoreEntryRepository;
        _draftRepository = draftRepository;
        _presetRepository = presetRepository;
        _pipelineService = pipelineService;
        _currentUserProvider = currentUserProvider;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _email = email;
        _templatedEmail = templatedEmail;
        _applicationRepository = applicationRepository;
        _employeeRepository = employeeRepository;
        _associateRepository = associateRepository;
        _leaveRepository = leaveRepository;
        _travelRepository = travelRepository;
        _portalUrl = (configuration["CandidatePortal:PortalUrl"] ?? "").TrimEnd('/');
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

    // An interview owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobInterview> GetOwnedInterviewAsync(Guid id)
    {
        var entity = await _interviewRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterview> GetOwnedInterviewWithDetailsAsync(Guid id)
    {
        var entity = await _interviewRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewPanelist> GetOwnedPanelistAsync(Guid id)
    {
        var entity = await _panelistRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Panelist with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewExternalPanelist> GetOwnedExternalPanelistAsync(Guid id)
    {
        var entity = await _externalPanelistRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"External panelist with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewee> GetOwnedIntervieweeAsync(Guid id)
    {
        var entity = await _intervieweeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interviewee with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewQuestion> GetOwnedQuestionPlanAsync(Guid id)
    {
        var entity = await _questionPlanRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Question plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewScoreSummary> GetOwnedScoreSummaryAsync(Guid id)
    {
        var entity = await _scoreSummaryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Score summary with ID '{id}' not found.");
        return entity;
    }

    // ── Entitlement ───────────────────────────────────────────────────────────
    //
    // An interview is not HR-only the way an application is (see JobApplicationController): the people
    // who have to open it, read the question plan and file a scorecard are ordinary employees sitting on
    // the panel. But it carries the candidate's name and email, the panel's private comments and the
    // hire/no-hire recommendation, so "any authenticated employee" — which is what the controller
    // previously allowed — is far too wide.
    //
    // The rule, applied in the service so it holds whichever route reaches it:
    //   • HR / SuperAdmin           — everything, including recording on an external panelist's behalf.
    //   • A panelist on THAT interview — read it, and score as themselves.
    //   • Everyone else             — 403.
    //
    // External panelists have no login at all; they confirm by emailed token and HR enters their scores,
    // which is why an external scorecard is an HR-only write.

    private bool IsHr =>
        _currentUserProvider.HasRole(Constants.Roles.Hr) ||
        _currentUserProvider.HasRole(Constants.Roles.SuperAdmin);

    private Guid? CallerEmployeeId => _currentUser.EmployeeId;

    /// <summary>
    /// The caller's own panelist row on this interview, or null when they do not sit on it.
    /// </summary>
    private async Task<JobInterviewPanelist?> GetCallerPanelistAsync(Guid interviewId)
    {
        var employeeId = CallerEmployeeId;
        if (employeeId is null || employeeId == Guid.Empty) return null;

        var panelists = await _panelistRepository.GetByInterviewIdAsync(interviewId);
        return panelists.FirstOrDefault(p => p.EmployeeId == employeeId && p.TenantId == GetTenantId());
    }

    /// <summary>Read access: HR, or a panelist on this interview.</summary>
    private async Task EnsureCanReadInterviewAsync(Guid interviewId)
    {
        if (IsHr) return;
        if (await GetCallerPanelistAsync(interviewId) is not null) return;

        throw new UnauthorizedAccessException(
            "Only HR and members of this interview's panel can view it.");
    }

    /// <summary>Write access to the interview record itself (schedule, panel, questions): HR only.</summary>
    private void EnsureHr(string action)
    {
        if (IsHr) return;
        throw new UnauthorizedAccessException($"Only HR can {action}.");
    }

    /// <summary>
    /// Resolves which panelist the caller is allowed to file this scorecard as, and refuses anything else.
    /// The client used to name the panelist in the payload (or the query string, for drafts), so any
    /// authenticated user could submit — or read — a scorecard in someone else's name.
    /// </summary>
    private async Task EnsureCanScoreAsAsync(Guid intervieweeId, Guid? internalPanelistId, Guid? externalPanelistId)
    {
        if (internalPanelistId is null && externalPanelistId is null)
            throw new InvalidOperationException("A scorecard must name the panelist it belongs to.");
        if (internalPanelistId is not null && externalPanelistId is not null)
            throw new InvalidOperationException("A scorecard belongs to one panelist, not both.");

        var interviewee = await GetOwnedIntervieweeAsync(intervieweeId);

        // The panelist must sit on the same interview as the candidate being scored — otherwise a
        // scorecard could be attributed to a panel the candidate never appeared before.
        if (internalPanelistId is not null)
        {
            var panelist = await GetOwnedPanelistAsync(internalPanelistId.Value);
            if (panelist.JobInterviewId != interviewee.JobInterviewId)
                throw new InvalidOperationException("That panelist does not sit on this candidate's interview.");

            if (IsHr) return;
            if (panelist.EmployeeId == CallerEmployeeId) return;

            throw new UnauthorizedAccessException("You can only score as yourself.");
        }

        var external = await GetOwnedExternalPanelistAsync(externalPanelistId!.Value);
        if (external.JobInterviewId != interviewee.JobInterviewId)
            throw new InvalidOperationException("That panelist does not sit on this candidate's interview.");

        // External associates have no login, so only HR can record on their behalf.
        EnsureHr("record scores for an external panelist");
    }

    private async Task<string> GenerateInterviewNumberAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var last = await _interviewRepository.GetQueryable()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted)
            .OrderByDescending(i => i.InterviewNumber)
            .Select(i => i.InterviewNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last != null && int.TryParse(last.Replace("INT-", ""), out var parsed))
            next = parsed + 1;

        return $"INT-{next:D6}";
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<JobInterviewDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(id);
        await EnsureCanReadInterviewAsync(id);
        return entity.ToDto();
    }

    public async Task<JobInterviewDto?> GetByInterviewNumberAsync(string interviewNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _interviewRepository.GetByInterviewNumberAsync(interviewNumber);
        if (entity == null || entity.TenantId != tenantId) return null;

        await EnsureCanReadInterviewAsync(entity.Id);
        return entity.ToDto();
    }

    public async Task<JobInterviewDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewWithDetailsAsync(id);
        await EnsureCanReadInterviewAsync(id);
        return entity.ToDetailDto();
    }

    // The four list reads below are HR's: they span interviews the caller may have nothing to do with,
    // and each row carries the candidate names. A panelist reaches their own sessions through
    // GetMyPanelistSlotsAsync instead.

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        EnsureHr("list a vacancy's interviews");
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByStatusAsync(JobInterviewStatus status, CancellationToken cancellationToken = default)
    {
        EnsureHr("list interviews by status");
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        EnsureHr("list the interview schedule");
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByDateRangeAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByRoundAsync(Guid vacancyId, int round, CancellationToken cancellationToken = default)
    {
        EnsureHr("list a round's interviews");
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByRoundAsync(vacancyId, round);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Availability / conflict detection ──────────────────────────────────────

    public async Task<PanelistAvailabilityCheckDto> CheckPanelistAvailabilityAsync(
        IReadOnlyList<Guid> panelistEmployeeIds, IReadOnlyList<Guid> externalAssociateIds,
        DateOnly date, TimeSpan start, TimeSpan end,
        Guid? excludeInterviewId, CancellationToken cancellationToken = default)
    {
        // This reads other employees' leave and travel to explain a clash. That is HR's to see.
        EnsureHr("check panel availability");

        var result = new PanelistAvailabilityCheckDto();

        var ids       = (panelistEmployeeIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var assocIds  = (externalAssociateIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (ids.Count == 0 && assocIds.Count == 0)
            return result;

        var blockingInterviewStatuses = new[]
        {
            JobInterviewStatus.Scheduled, JobInterviewStatus.Rescheduled, JobInterviewStatus.InProgress
        };

        bool OverlapsSlot(JobInterview i) =>
            i.Id != excludeInterviewId
            && !i.IsDeleted
            && i.TenantId == GetTenantId()
            && blockingInterviewStatuses.Contains(i.Status)
            && i.ScheduledDate == date
            && i.StartTime < end && i.EndTime > start;

        static PanelistInterviewConflictDto ToConflict(JobInterview i) => new()
        {
            InterviewId     = i.Id,
            InterviewNumber = i.InterviewNumber,
            JobTitle        = i.JobVacancy?.JobTitle ?? string.Empty,
            ScheduledDate   = i.ScheduledDate,
            StartTime       = i.StartTime,
            EndTime         = i.EndTime,
            Status          = i.Status,
        };

        await AddInternalPanelistRowsAsync(result, ids, date, OverlapsSlot, ToConflict);
        await AddExternalPanelistRowsAsync(result, assocIds, OverlapsSlot, ToConflict);

        result.HasConflicts = result.Panelists.Any(p => p.HasConflicts);
        return result;
    }

    private async Task AddInternalPanelistRowsAsync(
        PanelistAvailabilityCheckDto result, List<Guid> ids, DateOnly date,
        Func<JobInterview, bool> overlapsSlot, Func<JobInterview, PanelistInterviewConflictDto> toConflict)
    {
        if (ids.Count == 0) return;

        var tenantId = GetTenantId();

        // Resolve display names once.
        var employees = (await _employeeRepository.FindAsync(e => ids.Contains(e.Id)))
            .Where(e => e.TenantId == tenantId)
            .ToList();
        var nameById = employees.ToDictionary(e => e.Id, e => $"{e.FirstName} {e.LastName}".Trim());

        // Approved/pending leave that spans the interview day.
        var activeLeaveStatuses = new[] { LeaveStatus.Approved, LeaveStatus.Pending, LeaveStatus.InProgress };
        var leaves = (await _leaveRepository.FindAsync(l =>
                ids.Contains(l.EmployeeId)
                && activeLeaveStatuses.Contains(l.Status)
                && l.StartDate <= date && l.EndDate >= date))
            .Where(l => l.TenantId == tenantId)
            .ToList();

        // Approved/submitted/in-progress travel that spans the interview day.
        var activeTravelStatuses = new[]
        {
            StaffTravelRequestStatus.Approved, StaffTravelRequestStatus.Submitted, StaffTravelRequestStatus.InProgress
        };
        var travels = (await _travelRepository.FindAsync(t =>
                ids.Contains(t.EmployeeId)
                && activeTravelStatuses.Contains(t.Status)
                && t.TravelStartDate <= date && t.TravelEndDate >= date))
            .Where(t => t.TenantId == tenantId)
            .ToList();

        foreach (var empId in ids)
        {
            var row = new PanelistAvailabilityDto
            {
                EmployeeId   = empId,
                EmployeeName = nameById.TryGetValue(empId, out var n) && !string.IsNullOrWhiteSpace(n) ? n : "Panelist",
                IsExternal   = false,
            };

            // Overlapping interviews this employee already sits on (same day, time windows intersect).
            var panelSlots = await _panelistRepository.GetByEmployeeIdAsync(empId);
            row.InterviewConflicts = panelSlots
                .Where(p => p.TenantId == tenantId && p.JobInterview != null && overlapsSlot(p.JobInterview))
                .OrderBy(p => p.JobInterview!.StartTime)
                .Select(p => toConflict(p.JobInterview!))
                .ToList();

            row.LeaveConflicts = leaves
                .Where(l => l.EmployeeId == empId)
                .OrderBy(l => l.StartDate)
                .Select(l => new PanelistLeaveConflictDto
                {
                    StartDate = l.StartDate,
                    EndDate   = l.EndDate,
                    Status    = l.Status.ToString(),
                })
                .ToList();

            row.TravelConflicts = travels
                .Where(t => t.EmployeeId == empId)
                .OrderBy(t => t.TravelStartDate)
                .Select(t => new PanelistTravelConflictDto
                {
                    RequestNumber = t.RequestNumber,
                    StartDate     = t.TravelStartDate,
                    EndDate       = t.TravelEndDate,
                    Status        = t.Status.ToString(),
                })
                .ToList();

            row.HasConflicts = row.InterviewConflicts.Count > 0 || row.LeaveConflicts.Count > 0 || row.TravelConflicts.Count > 0;
            result.Panelists.Add(row);
        }
    }

    /// <summary>
    /// External associates: interview-overlap only. Grouped per associate; associates with no overlapping
    /// interview simply produce no row (we only surface conflicts). No leave/travel is tracked for them.
    /// </summary>
    private async Task AddExternalPanelistRowsAsync(
        PanelistAvailabilityCheckDto result, List<Guid> assocIds,
        Func<JobInterview, bool> overlapsSlot, Func<JobInterview, PanelistInterviewConflictDto> toConflict)
    {
        if (assocIds.Count == 0) return;

        var tenantId = GetTenantId();
        var extRows = (await _externalPanelistRepository.FindAsync(
                x => assocIds.Contains(x.AssociateId),
                x => x.JobInterview,
                x => x.ExternalAssociate))
            .Where(x => x.TenantId == tenantId && x.JobInterview != null && overlapsSlot(x.JobInterview))
            .ToList();

        foreach (var grp in extRows.GroupBy(x => x.AssociateId))
        {
            var first = grp.First();
            var name  = $"{first.ExternalAssociate?.FirstName} {first.ExternalAssociate?.LastName}".Trim();

            var row = new PanelistAvailabilityDto
            {
                EmployeeId         = grp.Key,
                EmployeeName       = string.IsNullOrWhiteSpace(name) ? "External panelist" : name,
                IsExternal         = true,
                InterviewConflicts = grp
                    .OrderBy(x => x.JobInterview!.StartTime)
                    .Select(x => toConflict(x.JobInterview!))
                    .ToList(),
            };
            row.HasConflicts = row.InterviewConflicts.Count > 0;
            result.Panelists.Add(row);
        }
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<JobInterviewDto> CreateAsync(CreateJobInterviewDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        EnsureHr("schedule an interview");

        await ValidateScheduleAsync(createDto.JobVacancyId, createDto.ScheduledDate, createDto.StartTime, createDto.EndTime,
            createDto.ApplicationIds, cancellationToken);
        await ValidatePanelAsync(createDto.PanelistEmployeeIds, createDto.ExternalPanelistAssociateIds, current);

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.InterviewNumber = await GenerateInterviewNumberAsync(cancellationToken);
        entity.Status = JobInterviewStatus.Scheduled;

        await _interviewRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Interview created: {InterviewNumber}", entity.InterviewNumber);

        // Add interviewees, internal panelists, and external panelists if provided
        bool hasRelated = false;

        if (createDto.ApplicationIds?.Count > 0)
        {
            foreach (var appId in createDto.ApplicationIds)
            {
                var ie = new AddJobIntervieweeDto
                {
                    JobInterviewId   = entity.Id,
                    JobApplicationId = appId,
                }.ToEntity(current, createdByUserId);

                var slot = createDto.ApplicationSlots?.FirstOrDefault(s => s.ApplicationId == appId);
                if (slot != null)
                {
                    ie.SlotStartTime = slot.SlotStartTime;
                    ie.SlotEndTime   = slot.SlotEndTime;
                }

                await _intervieweeRepository.AddAsync(ie);
                hasRelated = true;
            }
        }

        if (createDto.PanelistEmployeeIds?.Count > 0)
        {
            foreach (var empId in createDto.PanelistEmployeeIds)
            {
                var p = new AddJobInterviewPanelistDto
                {
                    JobInterviewId = entity.Id,
                    EmployeeId     = empId,
                    Role           = JobInterviewPanelistRole.Member,
                    IsRequired     = false,
                }.ToEntity(current, createdByUserId);
                await _panelistRepository.AddAsync(p);
                hasRelated = true;
            }
        }

        if (createDto.ExternalPanelistAssociateIds?.Count > 0)
        {
            foreach (var assocId in createDto.ExternalPanelistAssociateIds)
            {
                var ep = new AddJobInterviewExternalPanelistDto
                {
                    JobInterviewId = entity.Id,
                    AssociateId    = assocId,
                    Role           = JobInterviewPanelistRole.Member,
                    IsRequired     = false,
                }.ToEntity(current, createdByUserId);
                await _externalPanelistRepository.AddAsync(ep);
                hasRelated = true;
            }
        }

        if (hasRelated)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Apply evaluation template (preset) if provided
        if (createDto.QuestionPresetId.HasValue)
        {
            var preset = await _presetRepository.GetWithItemsAsync(createDto.QuestionPresetId.Value);
            if (preset?.TenantId == current && preset.Items is { Count: > 0 })
            {
                foreach (var item in preset.Items.OrderBy(i => i.DisplayOrder))
                {
                    var qp = new JobInterviewQuestion
                    {
                        Id                    = Guid.NewGuid(),
                        TenantId              = current,
                        JobInterviewId        = entity.Id,
                        QuestionTypeId        = item.QuestionTypeId,
                        RequiredQuestionCount = item.RequiredQuestionCount,
                        AllowedPoolSize       = item.AllowedPoolSize,
                        DisplayOrder          = item.DisplayOrder,
                        CreatedBy             = createdByUserId.ToString(),
                    };
                    await _questionPlanRepository.AddAsync(qp);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // Auto-select (randomize) questions for each plan now that plans are saved
        if (createDto.QuestionPresetId.HasValue)
            await SelectQuestionsAsync(entity.Id, cancellationToken);

        // Advance pipeline for each interviewee: Interview stage (no-op if no pipeline)
        if (createDto.ApplicationIds?.Count > 0)
        {
            foreach (var appId in createDto.ApplicationIds)
            {
                await _pipelineService.AutoAdvanceToStageTypeAsync(
                    appId, RecruitmentPipelineStageType.Interview, createdByUserId, cancellationToken);
            }
        }

        return entity.ToDto();
    }

    public async Task<JobInterviewDto> UpdateAsync(UpdateJobInterviewDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(updateDto.Id);

        EnsureHr("edit an interview");

        if (entity.Status == JobInterviewStatus.Completed || entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Completed or cancelled interviews cannot be updated.");

        // Status is deliberately NOT taken from the payload — see UpdateJobInterviewDto. It is owned by
        // reschedule / cancel / complete, each of which carries the side effects a bare status write skips.
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(id);

        EnsureHr("delete an interview");

        if (entity.Status == JobInterviewStatus.Completed)
            throw new InvalidOperationException("Completed interviews cannot be deleted.");

        await _interviewRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> RescheduleAsync(RescheduleJobInterviewDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(dto.InterviewId);

        EnsureHr("reschedule an interview");

        if (entity.Status == JobInterviewStatus.Completed || entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Completed or cancelled interviews cannot be rescheduled.");

        ValidateSlot(dto.NewDate, dto.NewStartTime, dto.NewEndTime, "rescheduled to a past date");

        // The original date is what the reschedule audit is for; it was on the entity and never written.
        entity.OriginalDate ??= entity.ScheduledDate;
        entity.ScheduledDate = dto.NewDate;
        entity.StartTime = dto.NewStartTime;
        entity.EndTime = dto.NewEndTime;
        entity.LocationOrLink = dto.LocationOrLink;
        entity.RescheduleReason = dto.RescheduleReason;
        entity.Status = JobInterviewStatus.Rescheduled;

        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Email #9 — Interview rescheduled; regenerate confirmation token so old links are invalidated
        var fullReschedule = await GetOwnedInterviewWithDetailsAsync(dto.InterviewId);
        if (fullReschedule.Interviewees != null)
        {
            var tenantId = fullReschedule.TenantId;
            var allInterviewees = (await _intervieweeRepository.GetByInterviewIdAsync(dto.InterviewId))
                .Where(ie => ie.TenantId == tenantId)
                .ToList();
            foreach (var ie in allInterviewees)
            {
                var email = ie.JobApplication?.JobCandidate?.Email;
                var name  = ie.JobApplication?.JobCandidate?.FullName ?? "Candidate";

                // Fresh token — candidate must re-confirm for the new date/time
                var confirmToken = Guid.NewGuid().ToString("N");
                ie.ConfirmationToken          = confirmToken;
                ie.ConfirmationTokenExpiresAt = TokenExpiryFor(fullReschedule);
                ie.ConfirmedAttendance        = null;
                ie.ConfirmationDate           = null;
                await _intervieweeRepository.UpdateAsync(ie);

                // Best-effort: an unguarded throw here would abandon the token rotations of every
                // remaining interviewee, whose UpdateAsync calls are only saved after this loop.
                try
                {
                    await SendInterviewEmailAsync(email ?? string.Empty, name, fullReschedule, isReschedule: true, confirmToken,
                        slotStart: ie.SlotStartTime, slotEnd: ie.SlotEndTime);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send interview reschedule email to {Email} — the reschedule itself succeeded.",
                        email);
                }
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> CancelAsync(CancelJobInterviewDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(dto.InterviewId);

        EnsureHr("cancel an interview");

        if (entity.Status == JobInterviewStatus.Completed)
            throw new InvalidOperationException("A completed interview cannot be cancelled.");
        if (entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("This interview is already cancelled.");

        entity.Status = JobInterviewStatus.Cancelled;
        entity.CancellationReason = dto.CancellationReason;

        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteAsync(Guid interviewId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(interviewId);

        EnsureHr("close an interview");

        // Completion had no state guard at all, so a cancelled interview could be marked complete —
        // reviving a session nobody attended, and with it every downstream read that keys off Completed.
        if (entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("A cancelled interview cannot be completed.");
        if (entity.Status == JobInterviewStatus.Completed)
            throw new InvalidOperationException("This interview is already complete.");

        entity.Status = JobInterviewStatus.Completed;

        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Internal panelists ────────────────────────────────────────────────────

    public async Task<JobInterviewPanelistDto> AddPanelistAsync(AddJobInterviewPanelistDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedInterviewAsync(createDto.JobInterviewId);
        EnsureHr("change an interview panel");

        // The employee was never validated: an unknown id died on a foreign-key 500 and an id belonging
        // to another tenant was accepted outright.
        await EnsureEmployeeInTenantAsync(createDto.EmployeeId, current);

        var existing = (await _panelistRepository.GetByInterviewIdAsync(createDto.JobInterviewId))
            .Where(p => p.TenantId == current)
            .ToList();
        if (existing.Any(p => p.EmployeeId == createDto.EmployeeId))
            throw new InvalidOperationException("That employee is already on this interview's panel.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _panelistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read so the response carries the employee's name and the interview number, which a freshly
        // constructed entity has no navigations for.
        var saved = await _panelistRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> RemovePanelistAsync(Guid panelistId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(panelistId);
        EnsureHr("change an interview panel");

        await _panelistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewPanelistDto> UpdatePanelistAsync(UpdateJobInterviewPanelistDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(updateDto.Id);
        EnsureHr("change an interview panel");

        entity.Role       = updateDto.Role;
        entity.IsRequired = updateDto.IsRequired;

        await _panelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewPanelistDto>> GetPanelistsAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        await EnsureCanReadInterviewAsync(interviewId);
        var entities = await _panelistRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<bool> ConfirmPanelistAsync(Guid panelistId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(panelistId);

        // Confirming an assignment is a statement about yourself. HR may confirm on someone's behalf
        // (they take the phone call); nobody else may confirm for a colleague.
        if (!IsHr && entity.EmployeeId != CallerEmployeeId)
            throw new UnauthorizedAccessException("You can only confirm your own panel assignment.");

        entity.IsConfirmed = true;
        entity.ConfirmationDate = DateTime.UtcNow;

        await _panelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ConfirmPanelistAssignmentResultDto> ConfirmPanelistAssignmentByTokenAsync(
        string token, CancellationToken cancellationToken = default)
    {
        // Search internal panelists first, then external
        var internalPanelist = await _panelistRepository.GetByConfirmationTokenAsync(token);
        if (internalPanelist != null)
        {
            var already = internalPanelist.IsConfirmed;
            if (!already && IsTokenExpired(internalPanelist.ConfirmationTokenExpiresAt))
                throw new ArgumentException("Confirmation link is invalid or has expired.");
            if (!already)
            {
                internalPanelist.IsConfirmed      = true;
                internalPanelist.ConfirmationDate = DateTime.UtcNow;
            }
            internalPanelist.ConfirmationToken = null; // single-use: invalidate once consumed
            await _panelistRepository.UpdateAsync(internalPanelist);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ConfirmPanelistAssignmentResultDto
            {
                AlreadyConfirmed = already,
                PanelistName     = $"{internalPanelist.Employee?.FirstName} {internalPanelist.Employee?.LastName}".Trim(),
                JobTitle         = internalPanelist.JobInterview?.JobVacancy?.JobTitle ?? string.Empty,
                ScheduledDate    = internalPanelist.JobInterview?.ScheduledDate ?? DateOnly.MinValue,
                InterviewNumber  = internalPanelist.JobInterview?.InterviewNumber ?? string.Empty,
                IsExternal       = false,
            };
        }

        var externalPanelist = await _externalPanelistRepository.GetByConfirmationTokenAsync(token);
        if (externalPanelist != null)
        {
            var already = externalPanelist.IsConfirmed;
            if (!already && IsTokenExpired(externalPanelist.ConfirmationTokenExpiresAt))
                throw new ArgumentException("Confirmation link is invalid or has expired.");
            if (!already)
            {
                externalPanelist.IsConfirmed      = true;
                externalPanelist.ConfirmationDate = DateTime.UtcNow;
            }
            externalPanelist.ConfirmationToken = null; // single-use: invalidate once consumed
            await _externalPanelistRepository.UpdateAsync(externalPanelist);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ConfirmPanelistAssignmentResultDto
            {
                AlreadyConfirmed = already,
                PanelistName     = $"{externalPanelist.ExternalAssociate?.FirstName} {externalPanelist.ExternalAssociate?.LastName}".Trim(),
                JobTitle         = externalPanelist.JobInterview?.JobVacancy?.JobTitle ?? string.Empty,
                ScheduledDate    = externalPanelist.JobInterview?.ScheduledDate ?? DateOnly.MinValue,
                InterviewNumber  = externalPanelist.JobInterview?.InterviewNumber ?? string.Empty,
                IsExternal       = true,
            };
        }

        throw new ArgumentException("Confirmation link is invalid or has expired.");
    }

    public async Task<bool> RecordPanelistAttendanceAsync(Guid panelistId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(panelistId);
        await EnsureCanReadInterviewAsync(entity.JobInterviewId);

        entity.Attended     = attended;
        entity.NoShowReason = attended == false ? noShowReason : null;

        await _panelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── External panelists ────────────────────────────────────────────────────

    public async Task<JobInterviewExternalPanelistDto> AddExternalPanelistAsync(AddJobInterviewExternalPanelistDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedInterviewAsync(createDto.JobInterviewId);
        EnsureHr("change an interview panel");
        await EnsureAssociateInTenantAsync(createDto.AssociateId, current);

        var existing = (await _externalPanelistRepository.GetByInterviewIdAsync(createDto.JobInterviewId))
            .Where(p => p.TenantId == current)
            .ToList();
        if (existing.Any(p => p.AssociateId == createDto.AssociateId))
            throw new InvalidOperationException("That associate is already on this interview's panel.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _externalPanelistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _externalPanelistRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> RemoveExternalPanelistAsync(Guid externalPanelistId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(externalPanelistId);
        EnsureHr("change an interview panel");

        await _externalPanelistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewExternalPanelistDto> UpdateExternalPanelistAsync(UpdateJobInterviewExternalPanelistDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(updateDto.Id);
        EnsureHr("change an interview panel");

        entity.Role       = updateDto.Role;
        entity.IsRequired = updateDto.IsRequired;

        await _externalPanelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewExternalPanelistDto>> GetExternalPanelistsAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        await EnsureCanReadInterviewAsync(interviewId);
        var entities = await _externalPanelistRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<bool> RecordExternalPanelistAttendanceAsync(Guid extPanelistId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(extPanelistId);
        await EnsureCanReadInterviewAsync(entity.JobInterviewId);

        entity.Attended     = attended;
        entity.NoShowReason = attended == false ? noShowReason : null;

        await _externalPanelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Interviewees ──────────────────────────────────────────────────────────

    public async Task<JobIntervieweeDto> AddIntervieweeAsync(AddJobIntervieweeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var interview = await GetOwnedInterviewAsync(createDto.JobInterviewId);
        EnsureHr("add a candidate to an interview");

        // CreateAsync validated that every application exists, is in-tenant and belongs to the
        // interview's vacancy. This — the other door onto the same table — validated nothing, so a
        // candidate who had never applied for the role could be booked into its interview.
        await ValidateApplicationsForVacancyAsync(
            interview.JobVacancyId, new List<Guid> { createDto.JobApplicationId }, current);

        var already = (await _intervieweeRepository.GetByInterviewIdAsync(createDto.JobInterviewId))
            .Where(ie => ie.TenantId == current)
            .ToList();
        if (already.Any(ie => ie.JobApplicationId == createDto.JobApplicationId))
            throw new InvalidOperationException("That candidate is already booked into this interview.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _intervieweeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Note: interview invite emails are NOT sent automatically.
        // Use SendInvitesAsync to send invites on demand.

        // Keep the pipeline in step with the create path, which advances every application it books in.
        await _pipelineService.AutoAdvanceToStageTypeAsync(
            createDto.JobApplicationId, RecruitmentPipelineStageType.Interview, createdByUserId, cancellationToken);

        var saved = await _intervieweeRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> RemoveIntervieweeAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(intervieweeId);
        EnsureHr("remove a candidate from an interview");

        await _intervieweeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<JobIntervieweeDto>> GetIntervieweesAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        await EnsureCanReadInterviewAsync(interviewId);
        var entities = await _intervieweeRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<SendInterviewInvitesResultDto> SendInvitesAsync(
        Guid interviewId, List<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        // Load interview with full interviewee details for email context
        var fullInterview = await GetOwnedInterviewWithDetailsAsync(interviewId);
        EnsureHr("send interview invitations");

        if (fullInterview.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Invitations cannot be sent for a cancelled interview.");

        applicationIds ??= new List<Guid>();

        var allInterviewees = (await _intervieweeRepository.GetByInterviewIdAsync(interviewId))
            .Where(ie => ie.TenantId == fullInterview.TenantId)
            .ToList();

        // If no applicationIds specified, send to everyone; otherwise filter
        var targets = applicationIds.Count > 0
            ? allInterviewees.Where(ie => applicationIds.Contains(ie.JobApplicationId)).ToList()
            : allInterviewees.ToList();

        var result = new SendInterviewInvitesResultDto
        {
            TotalRequested = targets.Count,
        };

        foreach (var ie in targets)
        {
            var email = ie.JobApplication?.JobCandidate?.Email ?? string.Empty;
            var name  = ie.JobApplication?.JobCandidate?.FullName ?? "Candidate";
            var item  = new InterviewInviteResultItemDto
            {
                ApplicationId = ie.JobApplicationId,
                CandidateName = name,
            };

            try
            {
                // Generate a fresh confirmation token for this candidate
                var confirmToken = Guid.NewGuid().ToString("N");
                ie.ConfirmationToken          = confirmToken;
                ie.ConfirmationTokenExpiresAt = TokenExpiryFor(fullInterview);
                ie.ConfirmedAttendance        = null;  // reset any prior confirmation
                ie.ConfirmationDate           = null;
                ie.InvitationSentDate         = DateTime.UtcNow;

                await SendInterviewEmailAsync(email, name, fullInterview, isReschedule: false, confirmToken,
                    slotStart: ie.SlotStartTime, slotEnd: ie.SlotEndTime);
                await _intervieweeRepository.UpdateAsync(ie);
                item.Sent = true;
                result.Sent++;
            }
            catch (Exception ex)
            {
                item.Sent  = false;
                item.Error = ex.Message;
                result.Skipped++;
            }

            result.Results.Add(item);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<SendPanelistNotificationsResultDto> SendPanelistNotificationsAsync(
        Guid interviewId, List<Guid>? employeeIds, List<Guid>? externalAssociateIds,
        CancellationToken cancellationToken = default)
    {
        var fullInterview = await GetOwnedInterviewWithDetailsAsync(interviewId);
        EnsureHr("notify an interview panel");

        if (fullInterview.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Panel notifications cannot be sent for a cancelled interview.");

        var allPanelists    = (await _panelistRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == fullInterview.TenantId).ToList();
        var allExtPanelists = (await _externalPanelistRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == fullInterview.TenantId).ToList();

        // null  = notify everyone in this group (broadcast)
        // empty = skip this group entirely
        // [ids] = notify only the specified members
        var targets = employeeIds == null
            ? allPanelists
            : employeeIds.Count > 0
                ? allPanelists.Where(p => employeeIds.Contains(p.EmployeeId)).ToList()
                : new List<JobInterviewPanelist>();

        var extTargets = externalAssociateIds == null
            ? allExtPanelists
            : externalAssociateIds.Count > 0
                ? allExtPanelists.Where(p => externalAssociateIds.Contains(p.AssociateId)).ToList()
                : new List<JobInterviewExternalPanelist>();

        var result = new SendPanelistNotificationsResultDto
        {
            TotalRequested = targets.Count + extTargets.Count,
        };

        foreach (var p in targets)
        {
            var email = p.Employee?.EmailAddress ?? string.Empty;
            var name  = $"{p.Employee?.FirstName} {p.Employee?.LastName}".Trim();
            var item  = new PanelistNotificationResultItemDto
            {
                PersonId   = p.EmployeeId,
                Name       = name,
                IsExternal = false,
            };
            try
            {
                var pToken = Guid.NewGuid().ToString("N");
                p.ConfirmationToken          = pToken;
                p.ConfirmationTokenExpiresAt = TokenExpiryFor(fullInterview);
                p.IsConfirmed                = false;
                p.ConfirmationDate           = null;
                p.InvitationSentDate         = DateTime.UtcNow;
                await SendPanelistEmailAsync(email, name, fullInterview, pToken);
                await _panelistRepository.UpdateAsync(p);
                item.Sent = true;
                result.Sent++;
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
                result.Skipped++;
            }
            result.Results.Add(item);
        }

        foreach (var ep in extTargets)
        {
            var email = ep.ExternalAssociate?.Email ?? string.Empty;
            var name  = $"{ep.ExternalAssociate?.FirstName} {ep.ExternalAssociate?.LastName}".Trim();
            var item  = new PanelistNotificationResultItemDto
            {
                PersonId   = ep.AssociateId,
                Name       = name,
                IsExternal = true,
            };
            try
            {
                var epToken = Guid.NewGuid().ToString("N");
                ep.ConfirmationToken          = epToken;
                ep.ConfirmationTokenExpiresAt = TokenExpiryFor(fullInterview);
                ep.IsConfirmed                = false;
                ep.ConfirmationDate           = null;
                ep.InvitationSentDate         = DateTime.UtcNow;
                await SendPanelistEmailAsync(email, name, fullInterview, epToken);
                await _externalPanelistRepository.UpdateAsync(ep);
                item.Sent = true;
                result.Sent++;
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
                result.Skipped++;
            }
            result.Results.Add(item);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<ConfirmInterviewAttendanceResultDto> ConfirmAttendanceByTokenAsync(
        string token, CancellationToken cancellationToken = default)
    {
        var ie = await _intervieweeRepository.GetByConfirmationTokenAsync(token);
        if (ie == null)
            throw new ArgumentException("Confirmation link is invalid or has expired.");

        var alreadyConfirmed = ie.ConfirmedAttendance == true;
        if (!alreadyConfirmed && IsTokenExpired(ie.ConfirmationTokenExpiresAt))
            throw new ArgumentException("Confirmation link is invalid or has expired.");
        if (!alreadyConfirmed)
        {
            ie.ConfirmedAttendance = true;
            ie.ConfirmationDate    = DateTime.UtcNow;
        }
        ie.ConfirmationToken = null; // single-use: invalidate once consumed
        await _intervieweeRepository.UpdateAsync(ie);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmInterviewAttendanceResultDto
        {
            AlreadyConfirmed = alreadyConfirmed,
            CandidateName    = ie.JobApplication?.JobCandidate?.FullName ?? string.Empty,
            JobTitle         = ie.JobInterview?.JobVacancy?.JobTitle ?? string.Empty,
            ScheduledDate    = ie.JobInterview?.ScheduledDate ?? DateOnly.MinValue,
            InterviewNumber  = ie.JobInterview?.InterviewNumber ?? string.Empty,
        };
    }

    public async Task<bool> UpdateIntervieweeSlotAsync(UpdateIntervieweeSlotDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(dto.IntervieweeId);
        EnsureHr("change a candidate's interview slot");

        if (dto.SlotStartTime.HasValue && dto.SlotEndTime.HasValue && dto.SlotEndTime <= dto.SlotStartTime)
            throw new InvalidOperationException("A candidate's slot must end after it starts.");

        entity.SlotStartTime = dto.SlotStartTime;
        entity.SlotEndTime   = dto.SlotEndTime;

        await _intervieweeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordAttendanceAsync(Guid intervieweeId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(intervieweeId);
        // Attendance is recorded in the room, so the panel may record it as well as HR.
        await EnsureCanReadInterviewAsync(entity.JobInterviewId);

        entity.CandidateAttended = attended;
        entity.NoShowReason      = attended == false ? noShowReason : null;

        await _intervieweeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordOutcomeAsync(RecordIntervieweeOutcomeDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(dto.IntervieweeId);
        await EnsureCanReadInterviewAsync(entity.JobInterviewId);

        // The outcome is the panel's verdict on this candidate, so it should not be recorded before the
        // candidate has been seen. A no-show has no verdict to give.
        if (entity.CandidateAttended == false)
            throw new InvalidOperationException(
                "This candidate was recorded as a no-show, so there is no interview outcome to record.");

        entity.Outcome = dto.Outcome;

        await _intervieweeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Question plans ────────────────────────────────────────────────────────

    public async Task<JobInterviewQuestionDto> AddQuestionPlanAsync(CreateJobInterviewQuestionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedInterviewAsync(createDto.JobInterviewId);
        EnsureHr("change an interview's question plan");

        await EnsureQuestionTypeInTenantAsync(createDto.QuestionTypeId, current);

        var existingPlans = (await _questionPlanRepository.GetByInterviewIdAsync(createDto.JobInterviewId))
            .Where(p => p.TenantId == current)
            .ToList();
        if (existingPlans.Any(p => p.QuestionTypeId == createDto.QuestionTypeId))
            throw new InvalidOperationException("This interview already has a plan for that question type.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _questionPlanRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _questionPlanRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<IEnumerable<JobInterviewQuestionDto>> GetQuestionPlansAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        // Panelists must be able to read the questions they are about to ask.
        await EnsureCanReadInterviewAsync(interviewId);
        var entities = await _questionPlanRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<JobInterviewQuestionDto> UpdateQuestionPlanAsync(UpdateJobInterviewQuestionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionPlanAsync(updateDto.Id);
        EnsureHr("change an interview's question plan");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _questionPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteQuestionPlanAsync(Guid questionPlanId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionPlanAsync(questionPlanId);
        EnsureHr("change an interview's question plan");

        await _questionPlanRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddSelectedQuestionAsync(Guid questionPlanId, Guid questionDetailId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var plan = await GetOwnedQuestionPlanAsync(questionPlanId);
        EnsureHr("change an interview's question plan");

        var questionDetail = await _questionDetailRepository.GetByIdAsync(questionDetailId);
        if (questionDetail == null || questionDetail.TenantId != plan.TenantId)
            throw new ArgumentException($"Question detail with ID '{questionDetailId}' not found.");

        // A plan is "N questions of this type" — a question from another type is not a member of it.
        if (questionDetail.QuestionTypeId != plan.QuestionTypeId)
            throw new InvalidOperationException("That question belongs to a different question type.");

        var alreadySelected = (await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(questionPlanId))
            .Any(sq => sq.TenantId == plan.TenantId && sq.QuestionDetailId == questionDetailId);
        if (alreadySelected)
            throw new InvalidOperationException("That question is already in this plan.");

        var selectedQuestion = new JobInterviewSelectedQuestion
        {
            Id = Guid.NewGuid(),
            TenantId = plan.TenantId,
            JobInterviewQuestionId = questionPlanId,
            QuestionDetailId = questionDetailId
        };

        await _selectedQuestionRepository.AddAsync(selectedQuestion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveSelectedQuestionAsync(Guid questionPlanId, Guid questionDetailId, CancellationToken cancellationToken = default)
    {
        await GetOwnedQuestionPlanAsync(questionPlanId);
        EnsureHr("change an interview's question plan");
        var selectedQuestions = await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(questionPlanId);
        var toRemove = selectedQuestions.FirstOrDefault(sq => sq.QuestionDetailId == questionDetailId && sq.TenantId == GetTenantId());
        if (toRemove == null)
            return false;

        await _selectedQuestionRepository.DeleteAsync(toRemove);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SelectQuestionsAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        EnsureHr("draw an interview's questions");
        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == interview.TenantId)
            .ToList();
        foreach (var plan in plans)
        {
            // Clear existing selections
            var existing = (await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(plan.Id))
                .Where(sq => sq.TenantId == plan.TenantId)
                .ToList();
            foreach (var sq in existing)
                await _selectedQuestionRepository.DeleteAsync(sq);

            // Get active questions for this type and randomly draw up to AllowedPoolSize
            var questions = (await _questionDetailRepository.GetByQuestionTypeIdAsync(plan.QuestionTypeId))
                .Where(q => q.TenantId == plan.TenantId && q.IsActive)
                .OrderBy(_ => Guid.NewGuid())
                .Take(plan.AllowedPoolSize)
                .ToList();

            for (int i = 0; i < questions.Count; i++)
            {
                await _selectedQuestionRepository.AddAsync(new JobInterviewSelectedQuestion
                {
                    Id                    = Guid.NewGuid(),
                    TenantId              = plan.TenantId,
                    JobInterviewQuestionId = plan.Id,
                    QuestionDetailId      = questions[i].Id,
                    DisplayOrder          = i + 1,
                    CreatedBy             = "system",
                });
            }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<QuestionPlanPreviewDto>> PreviewSelectQuestionsAsync(
        Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        EnsureHr("draw an interview's questions");
        var plans  = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == interview.TenantId)
            .ToList();
        var result = new List<QuestionPlanPreviewDto>(plans.Count);

        foreach (var plan in plans)
        {
            var available = (await _questionDetailRepository.GetByQuestionTypeIdAsync(plan.QuestionTypeId))
                .Where(q => q.TenantId == plan.TenantId && q.IsActive)
                .ToList();

            var questions = available
                .OrderBy(_ => Guid.NewGuid())
                .Take(plan.AllowedPoolSize)
                .ToList();

            result.Add(new QuestionPlanPreviewDto
            {
                PlanId               = plan.Id,
                QuestionTypeId       = plan.QuestionTypeId,
                QuestionTypeName     = plan.QuestionType?.TypeName ?? string.Empty,
                RequiredQuestionCount = plan.RequiredQuestionCount,
                AllowedPoolSize      = plan.AllowedPoolSize,
                DisplayOrder         = plan.DisplayOrder,
                // The bank can hold fewer active questions of this type than the plan requires. Nothing
                // used to say so, and the shortfall only became visible when the panel ran out of
                // questions mid-interview.
                AvailableQuestionCount = available.Count,
                MeetsRequiredCount     = questions.Count >= plan.RequiredQuestionCount,
                Questions = questions.Select((q, i) => new QuestionPreviewItemDto
                {
                    QuestionDetailId = q.Id,
                    QuestionText     = q.QuestionText,
                    Weight           = q.Weight,
                    MinScore         = q.MinScore,
                    MaxScore         = q.MaxScore,
                    DisplayOrder     = i + 1,
                }).ToList(),
            });
        }

        return result;
    }

    public async Task CommitSelectedQuestionsAsync(
        Guid interviewId, CommitInterviewQuestionsDto dto, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        EnsureHr("save an interview's questions");

        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == interview.TenantId)
            .ToList();

        // Validate every id BEFORE mutating anything: the commit clears each plan's selections first, so
        // a bad id half-way through used to leave the plan emptied and only partly rewritten. Same rule
        // as SetGoalRequiredSkillsAsync — validate the whole set, then write it.
        foreach (var planSel in dto.Plans)
        {
            var plan = plans.FirstOrDefault(p => p.Id == planSel.PlanId);
            if (plan is null) continue;

            var ids = planSel.QuestionDetailIds.Distinct().ToList();
            if (ids.Count != planSel.QuestionDetailIds.Count)
                throw new InvalidOperationException("A question cannot be selected twice in the same plan.");
            if (ids.Count == 0) continue;

            var found = (await _questionDetailRepository.FindAsync(q => ids.Contains(q.Id)))
                .Where(q => q.TenantId == interview.TenantId)
                .ToList();

            var missing = ids.Where(id => found.All(q => q.Id != id)).ToList();
            if (missing.Count > 0)
                throw new ArgumentException("One or more selected questions could not be found.");

            if (found.Any(q => q.QuestionTypeId != plan.QuestionTypeId))
                throw new InvalidOperationException(
                    "One or more selected questions belong to a different question type than the plan they were assigned to.");
        }

        foreach (var planSel in dto.Plans)
        {
            var plan = plans.FirstOrDefault(p => p.Id == planSel.PlanId);
            if (plan is null) continue;

            // Clear current selections
            var existing = (await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(plan.Id))
                .Where(sq => sq.TenantId == plan.TenantId)
                .ToList();
            foreach (var sq in existing)
                await _selectedQuestionRepository.DeleteAsync(sq);

            // Persist the user-confirmed questions in order
            for (int i = 0; i < planSel.QuestionDetailIds.Count; i++)
            {
                await _selectedQuestionRepository.AddAsync(new JobInterviewSelectedQuestion
                {
                    Id                     = Guid.NewGuid(),
                    TenantId               = plan.TenantId,
                    JobInterviewQuestionId = plan.Id,
                    QuestionDetailId       = planSel.QuestionDetailIds[i],
                    DisplayOrder           = i + 1,
                    CreatedBy              = "system",
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ── Score summaries ───────────────────────────────────────────────────────

    public async Task<JobInterviewScoreSummaryDto> CreateScoreSummaryAsync(CreateJobInterviewScoreSummaryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var interviewee = await GetOwnedIntervieweeAsync(createDto.JobIntervieweeId);
        await EnsureCanScoreAsAsync(createDto.JobIntervieweeId, createDto.InternalPanelistId, createDto.ExternalPanelistId);

        // 1. One scorecard per panelist per candidate. There is no update endpoint, so without this a
        //    corrected submission simply piled a second scorecard on top of the first and every
        //    aggregate double-counted that panelist. An unfinalised scorecard is replaced in place;
        //    a finalised one is the panelist's signed verdict and is refused.
        var existing = (await _scoreSummaryRepository.GetByIntervieweeIdAsync(createDto.JobIntervieweeId))
            .FirstOrDefault(s => s.TenantId == current
                && s.InternalPanelistId == createDto.InternalPanelistId
                && s.ExternalPanelistId == createDto.ExternalPanelistId);

        if (existing is { IsFinalized: true })
            throw new InvalidOperationException(
                "This panelist has already finalised a scorecard for this candidate. It cannot be replaced.");

        var entries = await BuildScoreEntriesAsync(
            interviewee, createDto.ScoreEntries, current, createdByUserId, cancellationToken);

        JobInterviewScoreSummary entity;
        if (existing is null)
        {
            entity = createDto.ToEntity(current, createdByUserId);
            await _scoreSummaryRepository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            entity = existing;
            entity.Recommendation = createDto.Recommendation;
            entity.Comments       = createDto.Comments;
            entity.EvaluationDate = createDto.EvaluationDate;
            entity.UpdatedAt      = DateTime.UtcNow;
            entity.UpdatedBy      = createdByUserId.ToString();

            foreach (var stale in (await _scoreEntryRepository.GetBySummaryIdAsync(entity.Id))
                     .Where(e => e.TenantId == current))
                await _scoreEntryRepository.DeleteAsync(stale);
        }

        // 2. Persist score entries with server-computed WeightedScore.
        foreach (var e in entries)
            e.ScoreSummaryId = entity.Id;

        if (entries.Count > 0)
            await _scoreEntryRepository.AddRangeAsync(entries);

        // 3. Refresh summary totals from the entries just written.
        entity.TotalRawScore      = entries.Sum(e => e.RawScore);
        entity.TotalWeightedScore = entries.Sum(e => e.WeightedScore);
        await _scoreSummaryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _scoreSummaryRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    /// <summary>
    /// Validates a scorecard's entries against the interview's own question set and computes the
    /// weighted score for each. Formula: (RawScore / MaxScore) × Weight — normalises for score-range
    /// differences between questions.
    /// </summary>
    private async Task<List<JobInterviewScoreEntry>> BuildScoreEntriesAsync(
        JobInterviewee interviewee,
        List<CreateJobInterviewScoreEntryDto> scoreEntries,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        if (scoreEntries.Count == 0) return new List<JobInterviewScoreEntry>();

        var questionIds = scoreEntries.Select(e => e.QuestionDetailId).ToList();
        if (questionIds.Distinct().Count() != questionIds.Count)
            throw new InvalidOperationException("A question cannot be scored twice on the same scorecard.");

        // The questions must be the ones this interview actually locked in. Previously any question id
        // was accepted — including another tenant's, which silently scored 0 because the weight lookup
        // missed, and questions this panel never asked.
        var selectedIds = await GetSelectedQuestionIdsAsync(interviewee.JobInterviewId, tenantId);
        var stray = questionIds.Where(id => !selectedIds.Contains(id)).ToList();
        if (stray.Count > 0)
            throw new InvalidOperationException(
                "One or more scored questions are not part of this interview's question set.");

        var questions = (await _questionDetailRepository.FindAsync(q => questionIds.Contains(q.Id)))
            .Where(q => q.TenantId == tenantId)
            .ToDictionary(q => q.Id);

        return scoreEntries
            .Select(e =>
            {
                var q = questions[e.QuestionDetailId];

                // The raw score has to sit inside the question's own band. The DTO allows 0–100 while a
                // question is typically scored 1–10, and (raw / max) × weight then produced a weighted
                // score many times the question's ceiling — one out-of-range entry could outweigh the
                // whole rest of the scorecard.
                if (e.RawScore < q.MinScore || e.RawScore > q.MaxScore)
                    throw new InvalidOperationException(
                        $"Score {e.RawScore} for \"{q.QuestionText}\" is outside its {q.MinScore}–{q.MaxScore} range.");

                var weighted = q.MaxScore > 0
                    ? Math.Round((e.RawScore / q.MaxScore) * q.Weight, 4)
                    : 0m;

                return new JobInterviewScoreEntry
                {
                    TenantId         = tenantId,
                    QuestionDetailId = e.QuestionDetailId,
                    RawScore         = e.RawScore,
                    WeightedScore    = weighted,
                    Remarks          = e.Remarks,
                    CreatedBy        = createdByUserId.ToString(),
                };
            })
            .ToList();
    }

    /// <summary>Every question locked into an interview's plans, across all of them.</summary>
    private async Task<HashSet<Guid>> GetSelectedQuestionIdsAsync(Guid interviewId, Guid tenantId)
    {
        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == tenantId)
            .ToList();

        var ids = new HashSet<Guid>();
        foreach (var plan in plans)
        {
            var selected = await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(plan.Id);
            foreach (var sq in selected.Where(s => s.TenantId == tenantId))
                ids.Add(sq.QuestionDetailId);
        }
        return ids;
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoreSummariesAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        var interviewee = await GetOwnedIntervieweeAsync(intervieweeId);
        await EnsureCanReadInterviewAsync(interviewee.JobInterviewId);
        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetByIntervieweeIdAsync(intervieweeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<JobInterviewScoreSummaryDetailDto> GetScoreSummaryDetailAsync(Guid scoreSummaryId, CancellationToken cancellationToken = default)
    {
        var entity = await _scoreSummaryRepository.GetWithScoreEntriesAsync(scoreSummaryId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Score summary with ID '{scoreSummaryId}' not found.");

        var interviewee = await GetOwnedIntervieweeAsync(entity.JobIntervieweeId);
        await EnsureCanReadInterviewAsync(interviewee.JobInterviewId);
        return entity.ToDetailDto();
    }

    public async Task<bool> FinalizeScoreAsync(Guid scoreSummaryId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScoreSummaryAsync(scoreSummaryId);
        await EnsureCanScoreAsAsync(entity.JobIntervieweeId, entity.InternalPanelistId, entity.ExternalPanelistId);

        if (entity.IsFinalized)
            throw new InvalidOperationException("This scorecard is already finalised.");

        await EnsureScorecardCoversRequiredQuestionsAsync(entity, cancellationToken);

        entity.IsFinalized = true;
        entity.FinalizedDate = DateTime.UtcNow;

        await _scoreSummaryRepository.UpdateAsync(entity);

        // The draft is a scratchpad for a scorecard that has now been signed off. Leaving it behind
        // means the scoring screen reloads the half-finished version over the submitted one.
        var draft = await _draftRepository.GetByPanelistAsync(
            entity.JobIntervieweeId, entity.InternalPanelistId, entity.ExternalPanelistId, cancellationToken);
        if (draft is not null && draft.TenantId == entity.TenantId)
            await _draftRepository.DeleteAsync(draft);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// <c>RequiredQuestionCount</c> is documented on the entity as "minimum number of questions from
    /// this type that must be asked" — and was read by nothing, so a plan could demand five competency
    /// questions and be signed off with one. Finalisation is where it bites: a draft may be as
    /// incomplete as the panelist likes, a signed scorecard may not.
    /// </summary>
    private async Task EnsureScorecardCoversRequiredQuestionsAsync(
        JobInterviewScoreSummary summary, CancellationToken cancellationToken)
    {
        var interviewee = await GetOwnedIntervieweeAsync(summary.JobIntervieweeId);
        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewee.JobInterviewId))
            .Where(p => p.TenantId == summary.TenantId)
            .ToList();
        if (plans.Count == 0) return;   // an unstructured interview has nothing to require

        var scored = (await _scoreEntryRepository.GetBySummaryIdAsync(summary.Id))
            .Where(e => e.TenantId == summary.TenantId)
            .Select(e => e.QuestionDetailId)
            .ToHashSet();

        foreach (var plan in plans)
        {
            var selected = (await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(plan.Id))
                .Where(sq => sq.TenantId == summary.TenantId)
                .Select(sq => sq.QuestionDetailId)
                .ToList();

            // Never demand more than the bank could actually supply — that would make the scorecard
            // unfinalisable through no fault of the panelist.
            var required = Math.Min(plan.RequiredQuestionCount, selected.Count);
            var answered = selected.Count(scored.Contains);

            if (answered < required)
                throw new InvalidOperationException(
                    $"\"{plan.QuestionType?.TypeName ?? "This section"}\" requires at least {required} " +
                    $"scored question(s); {answered} were scored.");
        }
    }

    /// <summary>The caller's own panel assignments — the diary a panelist opens to find their sessions.</summary>
    public async Task<IEnumerable<JobInterviewPanelistDto>> GetMyPanelistSlotsAsync(CancellationToken cancellationToken = default)
    {
        var employeeId = CallerEmployeeId;
        if (employeeId is null || employeeId == Guid.Empty)
            throw new UnauthorizedAccessException("Your user account is not linked to an employee record.");

        var tenantId = GetTenantId();
        var entities = await _panelistRepository.GetByEmployeeIdAsync(employeeId.Value);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewPanelistDto>> GetInterviewsByPanelistAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Reading someone else's interview diary is HR's; reading your own goes through /me above, which
        // takes the employee from the token rather than the URL.
        if (employeeId != CallerEmployeeId)
            EnsureHr("view another employee's interview assignments");

        var tenantId = GetTenantId();
        var entities = await _panelistRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobIntervieweeDto>> GetInterviewsByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        EnsureHr("view an application's interview history");
        var tenantId = GetTenantId();
        var entities = await _intervieweeRepository.GetByApplicationIdAsync(applicationId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoresByInternalPanelistAsync(Guid panelistId, CancellationToken cancellationToken = default)
    {
        var panelist = await GetOwnedPanelistAsync(panelistId);
        if (!IsHr && panelist.EmployeeId != CallerEmployeeId)
            throw new UnauthorizedAccessException("You can only view your own scorecards.");

        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetByInternalPanelistIdAsync(panelistId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoresByExternalPanelistAsync(Guid externalPanelistId, CancellationToken cancellationToken = default)
    {
        await GetOwnedExternalPanelistAsync(externalPanelistId);
        EnsureHr("view an external panelist's scorecards");
        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetByExternalPanelistIdAsync(externalPanelistId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetFinalizedScoresForIntervieweeAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        var interviewee = await GetOwnedIntervieweeAsync(intervieweeId);
        await EnsureCanReadInterviewAsync(interviewee.JobInterviewId);
        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetFinalizedForIntervieweeAsync(intervieweeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreEntryDto>> GetScoreEntriesAsync(Guid scoreSummaryId, CancellationToken cancellationToken = default)
    {
        var summary = await GetOwnedScoreSummaryAsync(scoreSummaryId);
        var interviewee = await GetOwnedIntervieweeAsync(summary.JobIntervieweeId);
        await EnsureCanReadInterviewAsync(interviewee.JobInterviewId);
        var entities = await _scoreEntryRepository.GetBySummaryIdAsync(scoreSummaryId);
        return entities.Where(e => e.TenantId == summary.TenantId).Select(e => e.ToDto());
    }

    // ── Score drafts ──────────────────────────────────────────────────────────

    public async Task<InterviewScoreDraftDto?> GetScoreDraftAsync(
        Guid intervieweeId,
        Guid? internalPanelistId,
        Guid? externalPanelistId,
        CancellationToken cancellationToken = default)
    {
        // A draft is one panelist's unfinished thinking about a candidate. The panelist used to be named
        // in the query string, so passing a colleague's id read their draft — comments and all.
        await EnsureCanScoreAsAsync(intervieweeId, internalPanelistId, externalPanelistId);

        var tenantId = GetTenantId();
        var entity = await _draftRepository.GetByPanelistAsync(
            intervieweeId, internalPanelistId, externalPanelistId, cancellationToken);
        return entity != null && entity.TenantId == tenantId ? entity.ToDraftDto() : null;
    }

    public async Task<InterviewScoreDraftDto> SaveScoreDraftAsync(
        Guid interviewId,
        SaveInterviewScoreDraftDto dto,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedInterviewAsync(interviewId);
        var interviewee = await GetOwnedIntervieweeAsync(dto.JobIntervieweeId);
        await EnsureCanScoreAsAsync(dto.JobIntervieweeId, dto.InternalPanelistId, dto.ExternalPanelistId);

        // The route's interview and the candidate's interview have to be the same one, or the draft is
        // filed against a session the candidate never sat in.
        if (interviewee.JobInterviewId != interviewId)
            throw new InvalidOperationException("That candidate is not booked into this interview.");

        var existing = await _draftRepository.GetByPanelistAsync(
            dto.JobIntervieweeId, dto.InternalPanelistId, dto.ExternalPanelistId, cancellationToken);
        if (existing != null && existing.TenantId != current)
            existing = null;

        var entriesJson = System.Text.Json.JsonSerializer.Serialize(dto.ScoreEntries);

        if (existing is null)
        {
            existing = new JobInterviewScoreDraft
            {
                TenantId            = current,
                JobInterviewId      = interviewId,
                JobIntervieweeId    = dto.JobIntervieweeId,
                InternalPanelistId  = dto.InternalPanelistId,
                ExternalPanelistId  = dto.ExternalPanelistId,
                DraftJson           = entriesJson,
                Comments            = dto.Comments,
                Recommendation      = dto.Recommendation,
                LastModified        = DateTime.UtcNow
            };
            await _draftRepository.AddAsync(existing);
        }
        else
        {
            existing.DraftJson       = entriesJson;
            existing.Comments        = dto.Comments;
            existing.Recommendation  = dto.Recommendation;
            existing.LastModified    = DateTime.UtcNow;
            await _draftRepository.UpdateAsync(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return existing.ToDraftDto();
    }

    // ── Validation + calendar-invite helpers ───────────────────────────────────

    /// <summary>Validates slot sanity plus that all applications belong to the interview's vacancy.</summary>
    private async Task ValidateScheduleAsync(
        Guid vacancyId, DateOnly date, TimeSpan start, TimeSpan end,
        List<Guid>? applicationIds, CancellationToken cancellationToken)
    {
        ValidateSlot(date, start, end, "scheduled in the past");
        await ValidateApplicationsForVacancyAsync(vacancyId, applicationIds, GetTenantId());
    }

    /// <summary>
    /// Every application booked into an interview must exist, be in-tenant, and be an application for
    /// the interview's own vacancy. Factored out of <c>ValidateScheduleAsync</c> so the add-interviewee
    /// path enforces the same rule — it is the same table, reached by a different route.
    /// </summary>
    private async Task ValidateApplicationsForVacancyAsync(
        Guid vacancyId, List<Guid>? applicationIds, Guid tenantId)
    {
        if (applicationIds is not { Count: > 0 }) return;

        var appIds = applicationIds.Distinct().ToList();
        var apps = (await _applicationRepository.FindAsync(a => appIds.Contains(a.Id)))
            .Where(a => a.TenantId == tenantId)
            .ToList();

        var missing = appIds.Where(id => apps.All(a => a.Id != id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException("One or more selected applications could not be found.");

        if (apps.Any(a => a.JobVacancyId != vacancyId))
            throw new InvalidOperationException("One or more selected applications do not belong to the selected vacancy.");
    }

    /// <summary>
    /// The panel named on a create payload gets the same checks the add-panelist endpoints now make.
    /// Without them an unknown employee id died on a foreign-key violation <i>after</i> the interview
    /// row had already been committed, leaving a half-built interview behind.
    /// </summary>
    private async Task ValidatePanelAsync(
        List<Guid>? panelistEmployeeIds, List<Guid>? externalAssociateIds, Guid tenantId)
    {
        if (panelistEmployeeIds is { Count: > 0 })
        {
            var ids = panelistEmployeeIds.Distinct().ToList();
            if (ids.Count != panelistEmployeeIds.Count)
                throw new InvalidOperationException("The same employee cannot be added to the panel twice.");

            var found = (await _employeeRepository.FindAsync(e => ids.Contains(e.Id)))
                .Where(e => e.TenantId == tenantId)
                .ToList();
            if (found.Count != ids.Count)
                throw new InvalidOperationException("One or more selected panelists could not be found.");
        }

        if (externalAssociateIds is { Count: > 0 })
        {
            var ids = externalAssociateIds.Distinct().ToList();
            if (ids.Count != externalAssociateIds.Count)
                throw new InvalidOperationException("The same associate cannot be added to the panel twice.");

            foreach (var id in ids)
                await EnsureAssociateInTenantAsync(id, tenantId);
        }
    }

    private async Task EnsureEmployeeInTenantAsync(Guid employeeId, Guid tenantId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId || employee.IsDeleted)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");
    }

    private async Task EnsureAssociateInTenantAsync(Guid associateId, Guid tenantId)
    {
        var associate = await _associateRepository.GetByIdAsync(associateId);
        if (associate == null || associate.TenantId != tenantId || associate.IsDeleted)
            throw new ArgumentException($"External associate with ID '{associateId}' not found.");
    }

    private async Task EnsureQuestionTypeInTenantAsync(Guid questionTypeId, Guid tenantId)
    {
        var type = await _questionTypeRepository.GetByIdAsync(questionTypeId);
        if (type == null || type.TenantId != tenantId || type.IsDeleted)
            throw new ArgumentException($"Interview question type with ID '{questionTypeId}' not found.");
    }

    /// <summary>Shared slot sanity checks: end after start, and not in the past.</summary>
    private static void ValidateSlot(DateOnly date, TimeSpan start, TimeSpan end, string pastPhrase)
    {
        if (end <= start)
            throw new InvalidOperationException("Interview end time must be after the start time.");
        if (date < DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException($"Interview cannot be {pastPhrase}.");
    }

    private static bool IsTokenExpired(DateTime? expiresAt) => expiresAt.HasValue && expiresAt.Value < DateTime.UtcNow;

    /// <summary>Confirmation links stay valid until the end of the interview day.</summary>
    private static DateTime TokenExpiryFor(JobInterview interview) =>
        interview.ScheduledDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Hand-builds a minimal RFC 5545 VEVENT for the interview so mail clients render an "Add to calendar"
    /// invite. Times are emitted as floating local time (no library dependency, no timezone assumptions).
    /// </summary>
    private static EmailAttachmentDto BuildInterviewIcs(
        JobInterview interview, string summary, string? attendeeEmail, TimeSpan? slotStart, TimeSpan? slotEnd)
    {
        var startTod = slotStart ?? interview.StartTime;
        var endTod   = slotEnd   ?? interview.EndTime;
        var startDt  = interview.ScheduledDate.ToDateTime(TimeOnly.FromTimeSpan(startTod));
        var endDt    = interview.ScheduledDate.ToDateTime(TimeOnly.FromTimeSpan(endTod));
        if (endDt <= startDt) endDt = startDt.AddHours(1);

        static string Esc(string? s) => (s ?? string.Empty)
            .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
            .Replace("\r\n", "\\n").Replace("\n", "\\n");
        static string Fmt(DateTime dt) => dt.ToString("yyyyMMddTHHmmss");

        var location = string.IsNullOrWhiteSpace(interview.LocationOrLink) ? "To be advised" : interview.LocationOrLink;

        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//ErpSystem//Recruitment//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append("METHOD:REQUEST\r\n");
        sb.Append("BEGIN:VEVENT\r\n");
        sb.Append($"UID:{interview.Id:N}-{Guid.NewGuid():N}@erpsystem\r\n");
        sb.Append($"DTSTAMP:{Fmt(DateTime.UtcNow)}Z\r\n");
        sb.Append($"DTSTART:{Fmt(startDt)}\r\n");
        sb.Append($"DTEND:{Fmt(endDt)}\r\n");
        sb.Append($"SUMMARY:{Esc(summary)}\r\n");
        sb.Append($"LOCATION:{Esc(location)}\r\n");
        sb.Append($"DESCRIPTION:{Esc($"Interview reference {interview.InterviewNumber}")}\r\n");
        if (!string.IsNullOrWhiteSpace(attendeeEmail))
            sb.Append($"ATTENDEE;RSVP=TRUE:mailto:{attendeeEmail}\r\n");
        sb.Append("STATUS:CONFIRMED\r\n");
        sb.Append("SEQUENCE:0\r\n");
        sb.Append("END:VEVENT\r\n");
        sb.Append("END:VCALENDAR\r\n");

        return new EmailAttachmentDto
        {
            FileName    = $"interview-{interview.InterviewNumber}.ics",
            Content     = Encoding.UTF8.GetBytes(sb.ToString()),
            ContentType = "text/calendar",
        };
    }

    private async Task SendInterviewEmailAsync(
        string toEmail, string candidateName, JobInterview interview, bool isReschedule, string confirmToken,
        TimeSpan? slotStart = null, TimeSpan? slotEnd = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;
        var jobTitle   = interview.JobVacancy?.JobTitle ?? "the position";
        var dateStr    = interview.ScheduledDate.ToString("dddd, d MMMM yyyy");
        var startStr   = DateTime.Today.Add(interview.StartTime).ToString("hh:mm tt");
        var endStr     = DateTime.Today.Add(interview.EndTime).ToString("hh:mm tt");
        var location   = string.IsNullOrWhiteSpace(interview.LocationOrLink) ? "To be advised" : interview.LocationOrLink;
        var typeLabel  = interview.Type.ToString().Replace("Interview", " Interview").Trim();
        var roundLabel = $"Round {interview.Round}";

        // Per-candidate slot time: show the candidate's window if set, otherwise fall back to the session time
        var hasSlot      = slotStart.HasValue || slotEnd.HasValue;
        var timeDisplay  = hasSlot
            ? $"{(slotStart.HasValue ? DateTime.Today.Add(slotStart.Value).ToString("hh:mm tt") : "—")} – {(slotEnd.HasValue ? DateTime.Today.Add(slotEnd.Value).ToString("hh:mm tt") : "—")}"
            : $"{startStr} – {endStr}";
        // The session window is shown as a secondary row only when a personal slot is set
        var sessionWindow = hasSlot ? $"{startStr} – {endStr}" : string.Empty;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]   = candidateName,
            ["JobTitle"]        = jobTitle,
            ["InterviewNumber"] = interview.InterviewNumber,
            ["Date"]            = dateStr,
            ["TimeSlot"]        = timeDisplay,
            ["SessionWindow"]   = sessionWindow,
            ["Format"]          = $"{typeLabel} ({roundLabel})",
            ["Location"]        = location,
            ["ConfirmToken"]    = confirmToken,
        };
        if (isReschedule) tokens["RescheduleReason"] = interview.RescheduleReason;

        var eventKey = isReschedule
            ? RecruitmentEmailCatalog.Events.InterviewRescheduled
            : RecruitmentEmailCatalog.Events.InterviewInvitation;

        var ics = BuildInterviewIcs(interview, $"Interview — {jobTitle}", toEmail, slotStart, slotEnd);
        await _templatedEmail.SendAsync(RecruitmentEmailCatalog.Module, eventKey, toEmail, tokens, new[] { ics });
    }

    private async Task SendPanelistEmailAsync(
        string toEmail, string panelistName, JobInterview interview, string? confirmToken = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var jobTitle   = interview.JobVacancy?.JobTitle ?? "the position";
        var dateStr    = interview.ScheduledDate.ToString("dddd, d MMMM yyyy");
        var startStr   = DateTime.Today.Add(interview.StartTime).ToString("hh:mm tt");
        var endStr     = DateTime.Today.Add(interview.EndTime).ToString("hh:mm tt");
        var location   = string.IsNullOrWhiteSpace(interview.LocationOrLink) ? "To be advised" : interview.LocationOrLink;
        var typeLabel  = interview.Type.ToString().Replace("Interview", " Interview").Trim();
        var roundLabel = $"Round {interview.Round}";

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PanelistName"]    = panelistName,
            ["JobTitle"]        = jobTitle,
            ["InterviewNumber"] = interview.InterviewNumber,
            ["Date"]            = dateStr,
            ["Time"]            = $"{startStr} – {endStr}",
            ["Format"]          = $"{typeLabel} ({roundLabel})",
            ["Location"]        = location,
            ["Instructions"]    = interview.Instructions,
            ["ConfirmToken"]    = confirmToken,
        };

        // The panelist path historically propagated send failures to its caller — preserve that by
        // throwing when the templated send reports failure.
        var ics = BuildInterviewIcs(interview, $"Interview panel — {jobTitle}", toEmail, slotStart: null, slotEnd: null);
        var sent = await _templatedEmail.SendAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.InterviewPanelistAssignment, toEmail, tokens, new[] { ics });
        if (!sent)
            throw new InvalidOperationException($"Failed to send panelist notification email to {toEmail}.");
    }
}
