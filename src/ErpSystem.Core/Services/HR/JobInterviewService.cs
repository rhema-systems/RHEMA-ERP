using System.Text;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
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

    public async Task<IEnumerable<JobInterviewQuestionTypeSummaryDto>> GetAllQuestionTypesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _questionTypeRepository.GetActiveTypesAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToSummaryDto());
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
    private readonly IJobInterviewScoreSummaryRepository _scoreSummaryRepository;
    private readonly IJobInterviewScoreEntryRepository _scoreEntryRepository;
    private readonly IJobInterviewScoreDraftRepository _draftRepository;
    private readonly IInterviewQuestionPresetRepository _presetRepository;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobInterviewService> _logger;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;
    // Cross-domain repositories used only for the advisory panelist-availability conflict check
    // and application/vacancy validation. Open-generic IGenericRepository<T> is DI-registered.
    private readonly IGenericRepository<JobApplication> _applicationRepository;
    private readonly IGenericRepository<Entities.HR.Employee> _employeeRepository;
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
        IJobInterviewScoreSummaryRepository scoreSummaryRepository,
        IJobInterviewScoreEntryRepository scoreEntryRepository,
        IJobInterviewScoreDraftRepository draftRepository,
        IInterviewQuestionPresetRepository presetRepository,
        IApplicationPipelineService pipelineService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<JobInterviewService> logger,
        IEmailService email,
        ITemplatedEmailService templatedEmail,
        IGenericRepository<JobApplication> applicationRepository,
        IGenericRepository<Entities.HR.Employee> employeeRepository,
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
        _scoreSummaryRepository = scoreSummaryRepository;
        _scoreEntryRepository = scoreEntryRepository;
        _draftRepository = draftRepository;
        _presetRepository = presetRepository;
        _pipelineService = pipelineService;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _email = email;
        _templatedEmail = templatedEmail;
        _applicationRepository = applicationRepository;
        _employeeRepository = employeeRepository;
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
        return entity.ToDto();
    }

    public async Task<JobInterviewDto?> GetByInterviewNumberAsync(string interviewNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _interviewRepository.GetByInterviewNumberAsync(interviewNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<JobInterviewDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewWithDetailsAsync(id);
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByStatusAsync(JobInterviewStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByDateRangeAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByRoundAsync(Guid vacancyId, int round, CancellationToken cancellationToken = default)
    {
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

        await ValidateScheduleAsync(createDto.JobVacancyId, createDto.ScheduledDate, createDto.StartTime, createDto.EndTime,
            createDto.ApplicationIds, cancellationToken);

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

        if (entity.Status == JobInterviewStatus.Completed || entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Completed or cancelled interviews cannot be updated.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(id);

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

        if (entity.Status == JobInterviewStatus.Completed || entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Completed or cancelled interviews cannot be rescheduled.");

        ValidateSlot(dto.NewDate, dto.NewStartTime, dto.NewEndTime, "rescheduled to a past date");

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

        if (entity.Status == JobInterviewStatus.Completed)
            throw new InvalidOperationException("A completed interview cannot be cancelled.");

        entity.Status = JobInterviewStatus.Cancelled;
        entity.CancellationReason = dto.CancellationReason;

        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteAsync(Guid interviewId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(interviewId);

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

        var entity = createDto.ToEntity(current, createdByUserId);
        await _panelistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemovePanelistAsync(Guid panelistId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(panelistId);

        await _panelistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewPanelistDto> UpdatePanelistAsync(UpdateJobInterviewPanelistDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(updateDto.Id);

        entity.Role       = updateDto.Role;
        entity.IsRequired = updateDto.IsRequired;

        await _panelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewPanelistDto>> GetPanelistsAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        var entities = await _panelistRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<bool> ConfirmPanelistAsync(Guid panelistId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(panelistId);

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

        var entity = createDto.ToEntity(current, createdByUserId);
        await _externalPanelistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveExternalPanelistAsync(Guid externalPanelistId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(externalPanelistId);

        await _externalPanelistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewExternalPanelistDto> UpdateExternalPanelistAsync(UpdateJobInterviewExternalPanelistDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(updateDto.Id);

        entity.Role       = updateDto.Role;
        entity.IsRequired = updateDto.IsRequired;

        await _externalPanelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewExternalPanelistDto>> GetExternalPanelistsAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        var entities = await _externalPanelistRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<bool> RecordExternalPanelistAttendanceAsync(Guid extPanelistId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(extPanelistId);

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

        await GetOwnedInterviewAsync(createDto.JobInterviewId);

        var entity = createDto.ToEntity(current, createdByUserId);
        await _intervieweeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Note: interview invite emails are NOT sent automatically.
        // Use SendInvitesAsync to send invites on demand.

        return entity.ToDto();
    }

    public async Task<bool> RemoveIntervieweeAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(intervieweeId);

        await _intervieweeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<JobIntervieweeDto>> GetIntervieweesAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        var entities = await _intervieweeRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<SendInterviewInvitesResultDto> SendInvitesAsync(
        Guid interviewId, List<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        // Load interview with full interviewee details for email context
        var fullInterview = await GetOwnedInterviewWithDetailsAsync(interviewId);

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

        entity.SlotStartTime = dto.SlotStartTime;
        entity.SlotEndTime   = dto.SlotEndTime;

        await _intervieweeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordAttendanceAsync(Guid intervieweeId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(intervieweeId);

        entity.CandidateAttended = attended;
        entity.NoShowReason      = attended == false ? noShowReason : null;

        await _intervieweeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordOutcomeAsync(RecordIntervieweeOutcomeDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(dto.IntervieweeId);

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

        var entity = createDto.ToEntity(current, createdByUserId);
        await _questionPlanRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewQuestionDto>> GetQuestionPlansAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        var entities = await _questionPlanRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<JobInterviewQuestionDto> UpdateQuestionPlanAsync(UpdateJobInterviewQuestionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionPlanAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _questionPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteQuestionPlanAsync(Guid questionPlanId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionPlanAsync(questionPlanId);

        await _questionPlanRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddSelectedQuestionAsync(Guid questionPlanId, Guid questionDetailId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var plan = await GetOwnedQuestionPlanAsync(questionPlanId);

        var questionDetail = await _questionDetailRepository.GetByIdAsync(questionDetailId);
        if (questionDetail == null || questionDetail.TenantId != plan.TenantId)
            throw new ArgumentException($"Question detail with ID '{questionDetailId}' not found.");

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
        var plans  = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == interview.TenantId)
            .ToList();
        var result = new List<QuestionPlanPreviewDto>(plans.Count);

        foreach (var plan in plans)
        {
            var questions = (await _questionDetailRepository.GetByQuestionTypeIdAsync(plan.QuestionTypeId))
                .Where(q => q.TenantId == plan.TenantId && q.IsActive)
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
        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == interview.TenantId)
            .ToList();

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

        await GetOwnedIntervieweeAsync(createDto.JobIntervieweeId);

        // 1. Persist the summary header first so we have its generated Id.
        var entity = createDto.ToEntity(current, createdByUserId);
        await _scoreSummaryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 2. Persist score entries with server-computed WeightedScore.
        //    Formula: (RawScore / MaxScore) × Weight  — normalises for score-range differences.
        if (createDto.ScoreEntries.Count > 0)
        {
            // Batch-load question details to retrieve Weight and MaxScore.
            var questionIds = createDto.ScoreEntries.Select(e => e.QuestionDetailId).ToHashSet();
            var questions   = await _questionDetailRepository.FindAsync(q => questionIds.Contains(q.Id));
            var questionMap = questions.Where(q => q.TenantId == current).ToDictionary(q => q.Id);

            var entries = createDto.ScoreEntries
                .Select(e =>
                {
                    decimal weighted = 0m;
                    if (questionMap.TryGetValue(e.QuestionDetailId, out var q) && q.MaxScore > 0)
                        weighted = Math.Round((e.RawScore / q.MaxScore) * q.Weight, 4);

                    return new JobInterviewScoreEntry
                    {
                        TenantId         = current,
                        ScoreSummaryId   = entity.Id,
                        QuestionDetailId = e.QuestionDetailId,
                        RawScore         = e.RawScore,
                        WeightedScore    = weighted,
                        Remarks          = e.Remarks,
                        CreatedBy        = createdByUserId.ToString(),
                    };
                })
                .ToList();

            await _scoreEntryRepository.AddRangeAsync(entries);

            // 3. Refresh summary totals from the persisted entries.
            entity.TotalRawScore      = entries.Sum(e => e.RawScore);
            entity.TotalWeightedScore = entries.Sum(e => e.WeightedScore);
            await _scoreSummaryRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoreSummariesAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        await GetOwnedIntervieweeAsync(intervieweeId);
        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetByIntervieweeIdAsync(intervieweeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<JobInterviewScoreSummaryDetailDto> GetScoreSummaryDetailAsync(Guid scoreSummaryId, CancellationToken cancellationToken = default)
    {
        var entity = await _scoreSummaryRepository.GetWithScoreEntriesAsync(scoreSummaryId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Score summary with ID '{scoreSummaryId}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<bool> FinalizeScoreAsync(Guid scoreSummaryId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScoreSummaryAsync(scoreSummaryId);

        entity.IsFinalized = true;
        entity.FinalizedDate = DateTime.UtcNow;

        await _scoreSummaryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<JobInterviewPanelistDto>> GetInterviewsByPanelistAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _panelistRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobIntervieweeDto>> GetInterviewsByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _intervieweeRepository.GetByApplicationIdAsync(applicationId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoresByInternalPanelistAsync(Guid panelistId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPanelistAsync(panelistId);
        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetByInternalPanelistIdAsync(panelistId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoresByExternalPanelistAsync(Guid externalPanelistId, CancellationToken cancellationToken = default)
    {
        await GetOwnedExternalPanelistAsync(externalPanelistId);
        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetByExternalPanelistIdAsync(externalPanelistId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetFinalizedScoresForIntervieweeAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        await GetOwnedIntervieweeAsync(intervieweeId);
        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetFinalizedForIntervieweeAsync(intervieweeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreEntryDto>> GetScoreEntriesAsync(Guid scoreSummaryId, CancellationToken cancellationToken = default)
    {
        var summary = await GetOwnedScoreSummaryAsync(scoreSummaryId);
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
        await GetOwnedIntervieweeAsync(intervieweeId);
        if (internalPanelistId.HasValue)
            await GetOwnedPanelistAsync(internalPanelistId.Value);
        if (externalPanelistId.HasValue)
            await GetOwnedExternalPanelistAsync(externalPanelistId.Value);

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
        await GetOwnedIntervieweeAsync(dto.JobIntervieweeId);
        if (dto.InternalPanelistId.HasValue)
            await GetOwnedPanelistAsync(dto.InternalPanelistId.Value);
        if (dto.ExternalPanelistId.HasValue)
            await GetOwnedExternalPanelistAsync(dto.ExternalPanelistId.Value);

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

        if (applicationIds is { Count: > 0 })
        {
            var tenantId = GetTenantId();
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
