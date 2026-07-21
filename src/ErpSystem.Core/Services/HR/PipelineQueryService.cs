using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Read-only query service for the production pipeline list+bulk view.
/// Provides lightweight stage overview counts and paginated per-stage application
/// lists. Separated from ApplicationPipelineService so that high-volume reads
/// do not share a transaction scope with stage-move writes.
/// </summary>
public sealed class PipelineQueryService : IPipelineQueryService
{
    private readonly IJobVacancyRepository _vacancyRepository;
    private readonly IRecruitmentPipelineStageRepository _stageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PipelineQueryService(
        IJobVacancyRepository vacancyRepository,
        IRecruitmentPipelineStageRepository stageRepository,
        IUnitOfWork unitOfWork)
    {
        _vacancyRepository = vacancyRepository;
        _stageRepository   = stageRepository;
        _unitOfWork        = unitOfWork;
    }

    // =========================================================================
    // GET PIPELINE OVERVIEW (stage headers + counts only)
    // =========================================================================

    public async Task<PipelineOverviewDto> GetPipelineOverviewAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default)
    {
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy is null)
            throw new KeyNotFoundException($"Vacancy '{vacancyId}' not found.");

        var result = new PipelineOverviewDto
        {
            VacancyId   = vacancyId,
            HasPipeline = vacancy.RecruitmentPipelineId.HasValue,
        };

        // All application IDs for this vacancy
        var allAppIds = await _unitOfWork.Repository<JobApplication>()
            .GetQueryable()
            .Where(a => a.JobVacancyId == vacancyId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        result.TotalApplications = allAppIds.Count;

        if (allAppIds.Count == 0)
        {
            // Still return stage headers with zero counts if pipeline exists
            if (vacancy.RecruitmentPipelineId.HasValue)
            {
                var emptyStages = (await _stageRepository
                    .GetByPipelineIdAsync(vacancy.RecruitmentPipelineId.Value))
                    .OrderBy(s => s.Order)
                    .ToList();

                // Inbox bucket first
                result.Stages.Add(BuildInboxHeader(0));
                result.Stages.AddRange(emptyStages.Select(s => new PipelineStageHeaderDto
                {
                    StageId          = s.Id,
                    StageName        = s.Name,
                    Order            = s.Order,
                    StageType        = s.StageType,
                    ApplicationCount = 0,
                }));
            }
            return result;
        }

        var appIdSet = allAppIds.ToHashSet();

        // Current stage for each application (one row per app, IsCurrent = true)
        var currentHistories = await _unitOfWork.Repository<JobApplicationStageHistory>()
            .GetQueryable()
            .Where(h => appIdSet.Contains(h.JobApplicationId) && h.IsCurrent)
            .ToListAsync(cancellationToken);

        // Applications with no current history = inbox
        var stagedAppIds = currentHistories.Select(h => h.JobApplicationId).ToHashSet();
        var inboxCount   = allAppIds.Count(id => !stagedAppIds.Contains(id));

        // Inbox bucket (sentinel StageId = Guid.Empty)
        result.Stages.Add(BuildInboxHeader(inboxCount));

        if (!vacancy.RecruitmentPipelineId.HasValue)
            return result;

        // Stage counts grouped
        var countByStage = currentHistories
            .GroupBy(h => h.PipelineStageId)
            .ToDictionary(g => g.Key, g => g.Count());

        var stages = (await _stageRepository
            .GetByPipelineIdAsync(vacancy.RecruitmentPipelineId.Value))
            .OrderBy(s => s.Order)
            .ToList();

        result.Stages.AddRange(stages.Select(s => new PipelineStageHeaderDto
        {
            StageId          = s.Id,
            StageName        = s.Name,
            Order            = s.Order,
            StageType        = s.StageType,
            ApplicationCount = countByStage.TryGetValue(s.Id, out var c) ? c : 0,
        }));

        return result;
    }

    // =========================================================================
    // GET STAGE APPLICATIONS (paginated list)
    // =========================================================================

    public async Task<PagedResult<PipelineApplicationListItemDto>> GetStageApplicationsAsync(
        Guid vacancyId,
        Guid stageId,
        StageApplicationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var isInbox = stageId == Guid.Empty;

        // ── 1. Resolve which application IDs belong to this stage/inbox ──────

        IQueryable<JobApplication> appQuery;

        if (isInbox)
        {
            // Inbox: applications for this vacancy that have NO current stage history
            var stagedIds = await _unitOfWork.Repository<JobApplicationStageHistory>()
                .GetQueryable()
                .Where(h => h.IsCurrent)
                .Select(h => h.JobApplicationId)
                .ToListAsync(cancellationToken);

            appQuery = _unitOfWork.Repository<JobApplication>()
                .GetQueryable()
                .Where(a => a.JobVacancyId == vacancyId && !stagedIds.Contains(a.Id));
        }
        else
        {
            // Specific stage: applications whose current history points to this stage
            var stageAppIds = await _unitOfWork.Repository<JobApplicationStageHistory>()
                .GetQueryable()
                .Where(h => h.PipelineStageId == stageId && h.IsCurrent)
                .Select(h => h.JobApplicationId)
                .ToListAsync(cancellationToken);

            appQuery = _unitOfWork.Repository<JobApplication>()
                .GetQueryable()
                .Where(a => a.JobVacancyId == vacancyId && stageAppIds.Contains(a.Id));
        }

        // Always include the candidate navigation for name/email
        appQuery = appQuery.Include(a => a.JobCandidate);

        // ── 2. Apply filters ──────────────────────────────────────────────────

        if (!string.IsNullOrWhiteSpace(query.NameSearch))
        {
            var term = query.NameSearch.Trim().ToLower();
            appQuery = appQuery.Where(a =>
                (a.JobCandidate != null &&
                    (EF.Functions.Like(a.JobCandidate.FirstName.ToLower(), $"%{term}%") ||
                     EF.Functions.Like(a.JobCandidate.LastName.ToLower(),  $"%{term}%"))) ||
                EF.Functions.Like(a.ApplicationNumber.ToLower(), $"%{term}%"));
        }

        if (query.Status.HasValue)
            appQuery = appQuery.Where(a => a.Status == query.Status.Value);

        if (query.Source.HasValue)
            appQuery = appQuery.Where(a => a.Source == query.Source.Value);

        if (query.DateFrom.HasValue)
            appQuery = appQuery.Where(a => a.ApplicationDate >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            appQuery = appQuery.Where(a => a.ApplicationDate <= query.DateTo.Value);

        if (query.MinScore.HasValue)
            appQuery = appQuery.Where(a => a.AutoScore >= query.MinScore.Value);

        if (query.MaxScore.HasValue)
            appQuery = appQuery.Where(a => a.AutoScore <= query.MaxScore.Value);

        // ── 3. Sort ───────────────────────────────────────────────────────────

        appQuery = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name",   false) => appQuery.OrderBy(a => a.JobCandidate!.LastName).ThenBy(a => a.JobCandidate!.FirstName),
            ("name",   true)  => appQuery.OrderByDescending(a => a.JobCandidate!.LastName).ThenByDescending(a => a.JobCandidate!.FirstName),
            ("score",  false) => appQuery.OrderBy(a => a.AutoScore),
            ("score",  true)  => appQuery.OrderByDescending(a => a.AutoScore),
            ("status", false) => appQuery.OrderBy(a => a.Status),
            ("status", true)  => appQuery.OrderByDescending(a => a.Status),
            (_,        false) => appQuery.OrderBy(a => a.ApplicationDate),
            _                 => appQuery.OrderByDescending(a => a.ApplicationDate),
        };

        // ── 4. Count then paginate ────────────────────────────────────────────

        var pageNum  = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var totalCount = await appQuery.CountAsync(cancellationToken);

        var applications = await appQuery
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // ── 5. Resolve stage entry times for non-inbox applications ───────────

        Dictionary<Guid, DateTime> entryTimes = new();
        if (!isInbox && applications.Count > 0)
        {
            var pageAppIds = applications.Select(a => a.Id).ToHashSet();
            entryTimes = await _unitOfWork.Repository<JobApplicationStageHistory>()
                .GetQueryable()
                .Where(h => pageAppIds.Contains(h.JobApplicationId)
                         && h.PipelineStageId == stageId
                         && h.IsCurrent)
                .ToDictionaryAsync(h => h.JobApplicationId, h => h.EnteredAt, cancellationToken);
        }

        // ── 6. Project ────────────────────────────────────────────────────────

        var items = applications.Select(a => new PipelineApplicationListItemDto
        {
            ApplicationId     = a.Id,
            CandidateId       = a.JobCandidateId,
            ApplicationNumber = a.ApplicationNumber,
            CandidateName     = a.JobCandidate?.FullName ?? string.Empty,
            CandidateEmail    = a.JobCandidate?.Email    ?? string.Empty,
            Status            = a.Status,
            Source            = a.Source,
            YearsOfExperience = a.YearsOfExperience,
            AutoScore         = a.AutoScore,
            ScoreIsStale      = a.ScoreIsStale,
            DateApplied       = a.ApplicationDate,
            EnteredStageAt    = entryTimes.TryGetValue(a.Id, out var t) ? t : null,
            IsInternalCandidate = a.IsInternalCandidate,
            IsInbox           = isInbox,
        }).ToList();

        return new PagedResult<PipelineApplicationListItemDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = pageNum,
            PageSize   = pageSize,
        };
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private static PipelineStageHeaderDto BuildInboxHeader(int count) => new()
    {
        StageId          = Guid.Empty,
        StageName        = "New Applications",
        Order            = 0,
        StageType        = null,
        ApplicationCount = count,
        IsInbox          = true,
    };
}
