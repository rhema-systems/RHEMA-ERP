using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class JobCandidateService : IJobCandidateService
{
    private readonly IJobCandidateRepository _candidateRepository;
    private readonly IJobCandidateQualificationRepository _qualificationRepository;
    private readonly IJobCandidateWorkHistoryRepository _workHistoryRepository;
    private readonly IJobCandidateRefereeRepository _refereeRepository;
    private readonly IJobCandidateSkillRepository _skillRepository;
    private readonly IJobCandidateInterestRepository _interestRepository;
    private readonly IJobCandidateDocumentRepository _documentRepository;
    private readonly IJobCandidateNoteRepository _noteRepository;
    private readonly IJobVacancyRepository _vacancyRepository;
    private readonly ICandidateSegmentMembershipRepository _segmentMembershipRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobCandidateService> _logger;

    public JobCandidateService(
        IJobCandidateRepository candidateRepository,
        IJobCandidateQualificationRepository qualificationRepository,
        IJobCandidateWorkHistoryRepository workHistoryRepository,
        IJobCandidateRefereeRepository refereeRepository,
        IJobCandidateSkillRepository skillRepository,
        IJobCandidateInterestRepository interestRepository,
        IJobCandidateDocumentRepository documentRepository,
        IJobCandidateNoteRepository noteRepository,
        IJobVacancyRepository vacancyRepository,
        ICandidateSegmentMembershipRepository segmentMembershipRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<JobCandidateService> logger)
    {
        _candidateRepository = candidateRepository;
        _qualificationRepository = qualificationRepository;
        _workHistoryRepository = workHistoryRepository;
        _refereeRepository = refereeRepository;
        _skillRepository = skillRepository;
        _interestRepository = interestRepository;
        _documentRepository = documentRepository;
        _noteRepository = noteRepository;
        _vacancyRepository = vacancyRepository;
        _segmentMembershipRepository = segmentMembershipRepository;
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

    // A candidate owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobCandidate> GetOwnedCandidateAsync(Guid id)
    {
        var entity = await _candidateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Candidate with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobVacancy> GetOwnedVacancyAsync(Guid id)
    {
        var entity = await _vacancyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Vacancy '{id}' not found.");
        return entity;
    }

    private async Task<JobCandidateQualification> GetOwnedQualificationAsync(Guid id)
    {
        var entity = await _qualificationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Qualification with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobCandidateWorkHistory> GetOwnedWorkHistoryAsync(Guid id)
    {
        var entity = await _workHistoryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Work history with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobCandidateReferee> GetOwnedRefereeAsync(Guid id)
    {
        var entity = await _refereeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Referee with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobCandidateSkill> GetOwnedSkillAsync(Guid id)
    {
        var entity = await _skillRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Skill with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobCandidateInterest> GetOwnedInterestAsync(Guid id)
    {
        var entity = await _interestRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interest with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobCandidateDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Document with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobCandidateNote> GetOwnedNoteAsync(Guid id)
    {
        var entity = await _noteRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Note with ID '{id}' not found.");
        return entity;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<JobCandidateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(id);
        return entity.ToDto();
    }

    public async Task<JobCandidateDto?> GetByCandidateNumberAsync(string candidateNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _candidateRepository.GetByCandidateNumberAsync(candidateNumber);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<JobCandidateDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Candidate with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<JobCandidateSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _candidateRepository.GetAllAsync();
        return entities.Where(c => c.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PagedResult<JobCandidateSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = PagingGuard.Clamp(pageNumber, pageSize);

        var tenantId = GetTenantId();
        var query = _candidateRepository.GetQueryable().Where(c => c.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<JobCandidateSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<JobCandidateSummaryDto>> GetTalentPoolAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _candidateRepository.GetTalentPoolCandidatesAsync();
        return entities.Where(c => c.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobCandidateSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedVacancyAsync(vacancyId);
        var tenantId = GetTenantId();
        var entities = await _candidateRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(c => c.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<JobCandidateDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetByEmailAsync(email, GetTenantId());
        return entity?.ToDto();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<JobCandidateDto> CreateAsync(CreateJobCandidateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var existing = await _candidateRepository.GetByEmailAsync(createDto.Email, current);
        if (existing != null)
            throw new InvalidOperationException($"A candidate with email '{createDto.Email}' already exists.");

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.CandidateNumber = await _candidateRepository.GetNextCandidateNumberAsync(current);

        await _candidateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job candidate created: {CandidateNumber}", entity.CandidateNumber);
        return entity.ToDto();
    }

    public async Task<JobCandidateDto> UpdateAsync(UpdateJobCandidateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job candidate updated: {CandidateNumber}", entity.CandidateNumber);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(id);

        await _candidateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Talent pool ───────────────────────────────────────────────────────────

    public async Task<bool> AddToTalentPoolAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);
        entity.IsInTalentPool = true;
        entity.TalentPoolAddedDate = DateTime.UtcNow;
        entity.TalentPoolStatus = ErpSystem.Core.Enums.TalentPoolCandidateStatus.Active;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveFromTalentPoolAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);
        entity.IsInTalentPool = false;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddToTalentPoolRichAsync(Guid candidateId, AddToTalentPoolDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);

        entity.IsInTalentPool = true;
        entity.TalentPoolAddedDate = DateTime.UtcNow;
        entity.TalentPoolStatus = ErpSystem.Core.Enums.TalentPoolCandidateStatus.Active;
        entity.TalentPoolSource = dto.Source;
        entity.TalentPoolNotes = dto.Notes;
        entity.TalentPoolReviewDate = dto.ReviewDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveFromTalentPoolRichAsync(Guid candidateId, RemoveFromTalentPoolDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);

        entity.IsInTalentPool = false;
        entity.TalentPoolRemovalReason = dto.Reason;
        entity.TalentPoolStatus = ErpSystem.Core.Enums.TalentPoolCandidateStatus.Expired;
        entity.TalentPoolNotes = string.IsNullOrWhiteSpace(dto.Notes) ? entity.TalentPoolNotes : dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TalentPoolPagedResultDto> GetTalentPoolFilteredAsync(TalentPoolFilterDto filter, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var (items, total) = await _candidateRepository.GetTalentPoolFilteredAsync(filter, cancellationToken);
        items = items.Where(c => c.TenantId == tenantId).ToList();
        return new TalentPoolPagedResultDto
        {
            Items = items.Select(c => c.ToTalentPoolDto()).ToList(),
            TotalCount = total,
            Page = filter.PageNumber,
            PageSize = filter.PageSize
        };
    }

    public async Task<TalentPoolCandidateDto> GetTalentPoolCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetWithSegmentsAsync(candidateId, cancellationToken);
        if (entity == null || entity.TenantId != GetTenantId()) throw new ArgumentException($"Candidate '{candidateId}' not found.");
        return entity.ToTalentPoolDto();
    }

    public async Task<bool> UpdateTalentPoolStatusAsync(Guid candidateId, ErpSystem.Core.Enums.TalentPoolCandidateStatus status, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);
        entity.TalentPoolStatus = status;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateTalentPoolReviewDateAsync(Guid candidateId, DateTime reviewDate, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);
        entity.TalentPoolReviewDate = reviewDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TalentPoolAnalyticsDto> GetTalentPoolAnalyticsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var pool = await _candidateRepository.GetTalentPoolCandidatesAsync();
        var candidates = pool.Where(c => c.TenantId == tenantId).ToList();
        var now = DateTime.UtcNow;
        var startOfYear = new DateTime(now.Year, 1, 1);
        var startOfMonth = new DateTime(now.Year, now.Month, 1);

        var analytics = new TalentPoolAnalyticsDto
        {
            TotalInPool         = candidates.Count,
            Active              = candidates.Count(c => c.TalentPoolStatus == ErpSystem.Core.Enums.TalentPoolCandidateStatus.Active),
            Passive             = candidates.Count(c => c.TalentPoolStatus == ErpSystem.Core.Enums.TalentPoolCandidateStatus.Passive),
            Dormant             = candidates.Count(c => c.TalentPoolStatus == ErpSystem.Core.Enums.TalentPoolCandidateStatus.Dormant),
            OverdueForReview    = candidates.Count(c => c.TalentPoolReviewDate.HasValue && c.TalentPoolReviewDate.Value < now),
            ConvertedThisYear   = candidates.Count(c => c.TalentPoolStatus == ErpSystem.Core.Enums.TalentPoolCandidateStatus.Converted && c.UpdatedAt >= startOfYear),
            AddedThisMonth      = candidates.Count(c => c.TalentPoolAddedDate.HasValue && c.TalentPoolAddedDate.Value >= startOfMonth),
            AddedThisYear       = candidates.Count(c => c.TalentPoolAddedDate.HasValue && c.TalentPoolAddedDate.Value >= startOfYear),
            AvgDaysInPool       = candidates.Any(c => c.TalentPoolAddedDate.HasValue)
                                    ? candidates.Where(c => c.TalentPoolAddedDate.HasValue).Average(c => (now - c.TalentPoolAddedDate!.Value).TotalDays)
                                    : 0
        };

        analytics.BySource = candidates
            .GroupBy(c => c.TalentPoolSource)
            .Select(g => new TalentPoolSourceBreakdownDto { SourceName = g.Key.ToString(), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        return analytics;
    }

    public async Task<RecruitmentBulkOperationResultDto> BulkTalentPoolOperationAsync(BulkTalentPoolOperationDto dto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var result = new RecruitmentBulkOperationResultDto();
        foreach (var id in dto.CandidateIds)
        {
            try
            {
                var entity = await _candidateRepository.GetByIdAsync(id);
                if (entity == null || entity.TenantId != tenantId) { result.Skipped++; continue; }

                switch (dto.Operation)
                {
                    case ErpSystem.Core.Enums.BulkTalentPoolOperation.AssignSegment
                        when dto.SegmentId.HasValue:
                    {
                        var existing = await _segmentMembershipRepository
                            .GetByCandidateAndSegmentAsync(id, dto.SegmentId.Value);
                        if (existing is null)
                        {
                            var membership = new CandidateSegmentMembership
                            {
                                Id                 = Guid.NewGuid(),
                                JobCandidateId     = id,
                                SegmentId          = dto.SegmentId.Value,
                                TenantId           = tenantId,
                                AddedByEmployeeId  = updatedByUserId,
                                AddedDate          = DateTime.UtcNow,
                                Notes              = dto.Notes
                            };
                            await _segmentMembershipRepository.AddAsync(membership);
                        }
                        break;
                    }
                    case ErpSystem.Core.Enums.BulkTalentPoolOperation.RemoveSegment
                        when dto.SegmentId.HasValue:
                    {
                        var existing = await _segmentMembershipRepository
                            .GetByCandidateAndSegmentAsync(id, dto.SegmentId.Value);
                        if (existing is not null)
                            await _segmentMembershipRepository.DeleteAsync(existing);
                        break;
                    }
                    case ErpSystem.Core.Enums.BulkTalentPoolOperation.SetStatus when dto.Status.HasValue:
                        entity.TalentPoolStatus = dto.Status.Value;
                        break;
                    case ErpSystem.Core.Enums.BulkTalentPoolOperation.RemoveFromPool:
                        entity.IsInTalentPool = false;
                        entity.TalentPoolStatus = ErpSystem.Core.Enums.TalentPoolCandidateStatus.Expired;
                        break;
                }

                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = updatedByUserId.ToString();
                await _candidateRepository.UpdateAsync(entity);
                result.Succeeded++;
            }
            catch { result.Skipped++; }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<List<TalentPoolVacancyMatchResultDto>> MatchToVacancyAsync(Guid vacancyId, int topN, CancellationToken cancellationToken = default)
    {
        await GetOwnedVacancyAsync(vacancyId);
        var tenantId = GetTenantId();
        // Simple scoring: returns active pool candidates ordered by experience, vacancy-specific scoring done in service layer or UI
        var candidates = (await _candidateRepository.GetTalentPoolCandidatesAsync())
            .Where(c => c.TenantId == tenantId && c.TalentPoolStatus == ErpSystem.Core.Enums.TalentPoolCandidateStatus.Active)
            .OrderByDescending(c => c.TotalYearsExperience)
            .Take(topN)
            .Select(c => new TalentPoolVacancyMatchResultDto
            {
                CandidateId = c.Id,
                CandidateName = c.FullName,
                CandidateNumber = c.CandidateNumber,
                Headline = c.Headline,
                TotalYearsExperience = c.TotalYearsExperience,
                PreferredWorkArrangementName = c.PreferredWorkArrangement.ToString(),
                AvailableFrom = c.AvailableFrom,
                MatchScore = 0,
                MatchReasons = new List<string> { "Active pool member" }
            })
            .ToList();

        return candidates;
    }

    public async Task<List<CandidateVacancyMatchResultDto>> MatchCandidateToVacanciesAsync(
        Guid candidateId, int topN = 10, CancellationToken cancellationToken = default)
    {
        var candidate = await GetOwnedCandidateAsync(candidateId);

        var tenantId = GetTenantId();
        var vacancies = (await _vacancyRepository.GetActiveVacanciesAsync())
            .Where(v => v.TenantId == tenantId)
            .ToList();
        var now       = DateTime.UtcNow;
        var results   = new List<CandidateVacancyMatchResultDto>(vacancies.Count);

        foreach (var v in vacancies)
        {
            var score   = 0;
            var reasons = new List<string>();

            // Experience match (+40)
            if (v.RequiredMinExperienceYears is null ||
                (candidate.TotalYearsExperience.HasValue &&
                 candidate.TotalYearsExperience.Value >= v.RequiredMinExperienceYears.Value))
            {
                score += 40;
                reasons.Add(v.RequiredMinExperienceYears is null
                    ? "No minimum experience required"
                    : $"Meets experience requirement ({v.RequiredMinExperienceYears} yr)");
            }

            // Work mode match (+30) — PreferredWorkArrangement.Any always matches
            var workModeMatch =
                candidate.PreferredWorkArrangement == ErpSystem.Core.Enums.PreferredWorkArrangement.Any ||
                candidate.PreferredWorkArrangement.ToString() == v.WorkMode.ToString();
            if (workModeMatch)
            {
                score += 30;
                reasons.Add(candidate.PreferredWorkArrangement == ErpSystem.Core.Enums.PreferredWorkArrangement.Any
                    ? "Open to any work arrangement"
                    : $"Work mode match ({v.WorkMode})");
            }

            // Deadline not yet passed (+20)
            if (v.ApplicationDeadline is null || v.ApplicationDeadline.Value > now)
            {
                score += 20;
                reasons.Add(v.ApplicationDeadline is null
                    ? "No application deadline"
                    : $"Apply by {v.ApplicationDeadline.Value:dd MMM yyyy}");
            }

            if (reasons.Count == 0)
                reasons.Add("Open vacancy");

            results.Add(new CandidateVacancyMatchResultDto
            {
                VacancyId           = v.Id,
                VacancyNumber       = v.VacancyNumber,
                JobTitle            = v.JobTitle,
                VacancyStatus       = v.VacancyStatus,
                VacancyStatusName   = v.VacancyStatus.ToString(),
                ApplicationDeadline = v.ApplicationDeadline,
                HiringManagerName   = v.HiringManager?.FullName,
                NumberOfPositions   = v.NumberOfPositions,
                MatchScore          = score,
                MatchReasons        = reasons,
            });
        }

        return results
            .OrderByDescending(r => r.MatchScore)
            .Take(topN)
            .ToList();
    }

    // ── Qualifications ────────────────────────────────────────────────────────

    public async Task<JobCandidateQualificationDto> AddQualificationAsync(CreateJobCandidateQualificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCandidateAsync(createDto.JobCandidateId);
        var entity = createDto.ToEntity(current, createdByUserId);
        await _qualificationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobCandidateQualificationDto>> GetQualificationsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var entities = await _qualificationRepository.GetByCandidateIdAsync(candidateId);
        return entities.Where(e => e.TenantId == GetTenantId()).Select(e => e.ToDto());
    }

    public async Task<JobCandidateQualificationDto> UpdateQualificationAsync(UpdateJobCandidateQualificationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQualificationAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _qualificationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQualificationAsync(qualificationId);

        await _qualificationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Work histories ────────────────────────────────────────────────────────

    public async Task<JobCandidateWorkHistoryDto> AddWorkHistoryAsync(CreateJobCandidateWorkHistoryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCandidateAsync(createDto.JobCandidateId);
        var entity = createDto.ToEntity(current, createdByUserId);
        await _workHistoryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobCandidateWorkHistoryDto>> GetWorkHistoriesAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var entities = await _workHistoryRepository.GetByCandidateIdAsync(candidateId);
        return entities.Where(e => e.TenantId == GetTenantId()).Select(e => e.ToDto());
    }

    public async Task<JobCandidateWorkHistoryDto> UpdateWorkHistoryAsync(UpdateJobCandidateWorkHistoryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWorkHistoryAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _workHistoryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWorkHistoryAsync(Guid workHistoryId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWorkHistoryAsync(workHistoryId);

        await _workHistoryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Referees ──────────────────────────────────────────────────────────────

    public async Task<JobCandidateRefereeDto> AddRefereeAsync(CreateJobCandidateRefereeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCandidateAsync(createDto.JobCandidateId);
        var entity = createDto.ToEntity(current, createdByUserId);
        await _refereeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobCandidateRefereeDto>> GetRefereesAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var entities = await _refereeRepository.GetByCandidateIdAsync(candidateId);
        return entities.Where(e => e.TenantId == GetTenantId()).Select(e => e.ToDto());
    }

    public async Task<JobCandidateRefereeDto> UpdateRefereeAsync(UpdateJobCandidateRefereeDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRefereeAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _refereeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRefereeAsync(Guid refereeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRefereeAsync(refereeId);

        await _refereeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Skills ────────────────────────────────────────────────────────────────

    public async Task<JobCandidateSkillDto> AddSkillAsync(CreateJobCandidateSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCandidateAsync(createDto.JobCandidateId);
        var entity = createDto.ToEntity(current, createdByUserId);
        await _skillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobCandidateSkillDto>> GetSkillsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var entities = await _skillRepository.GetByCandidateIdAsync(candidateId);
        return entities.Where(e => e.TenantId == GetTenantId()).Select(e => e.ToDto());
    }

    public async Task<JobCandidateSkillDto> UpdateSkillAsync(UpdateJobCandidateSkillDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSkillAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _skillRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSkillAsync(Guid skillId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSkillAsync(skillId);

        await _skillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Interests ─────────────────────────────────────────────────────────────

    public async Task<JobCandidateInterestDto> AddInterestAsync(CreateJobCandidateInterestDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCandidateAsync(createDto.JobCandidateId);
        var entity = createDto.ToEntity(current, createdByUserId);
        await _interestRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobCandidateInterestDto>> GetInterestsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var entities = await _interestRepository.GetByCandidateIdAsync(candidateId);
        return entities.Where(e => e.TenantId == GetTenantId()).Select(e => e.ToDto());
    }

    public async Task<JobCandidateInterestDto> UpdateInterestAsync(UpdateJobCandidateInterestDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterestAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _interestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInterestAsync(Guid interestId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterestAsync(interestId);

        await _interestRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    public async Task<JobCandidateDocumentDto> AddDocumentAsync(
        Guid candidateId,
        JobCandidateDocumentType documentType,
        string fileName,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCandidateAsync(candidateId);

        // FilePath is left empty: the bytes live behind the controlled-upload gate and are reached
        // through FileUploadRecordId / the DMS ids. Only rows written before the gate carry a path.
        var entity = new JobCandidateDocument
        {
            TenantId           = current,
            JobCandidateId     = candidateId,
            DocumentType       = documentType,
            FileName           = fileName,
            FilePath           = string.Empty,
            UploadDate         = DateTime.UtcNow,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId   = documentRecordId,
            DocumentVersionId  = documentVersionId,
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = createdByUserId.ToString(),
        };

        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobCandidateDocumentDto>> GetDocumentsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var entities = await _documentRepository.GetByCandidateIdAsync(candidateId);
        return entities.Where(e => e.TenantId == GetTenantId()).Select(e => e.ToDto());
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Notes ─────────────────────────────────────────────────────────────────

    public async Task<JobCandidateNoteDto> AddNoteAsync(CreateJobCandidateNoteDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCandidateAsync(createDto.JobCandidateId);
        var entity = createDto.ToEntity(current, createdByUserId);
        await _noteRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobCandidateNoteDto>> GetNotesAsync(Guid candidateId, bool includePrivate = false, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var tenantId = GetTenantId();
        var entities = includePrivate
            ? await _noteRepository.GetAllByCandidateIdAsync(candidateId)
            : await _noteRepository.GetByCandidateIdAsync(candidateId);

        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<JobCandidateNoteDto> UpdateNoteAsync(UpdateJobCandidateNoteDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNoteAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _noteRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNoteAsync(noteId);

        await _noteRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
