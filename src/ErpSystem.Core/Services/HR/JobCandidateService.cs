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
    private readonly ICandidateTalentSegmentRepository _segmentRepository;
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
        ICandidateTalentSegmentRepository segmentRepository,
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
        _segmentRepository = segmentRepository;
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

    /// <summary>
    /// Resolves a relationship-catalogue id for the candidate referee screen, and returns the words
    /// to mirror into the row's free-text <c>Relationship</c>.
    /// </summary>
    /// <remarks>
    /// <para>Round 2, lane D2. ⚠ PROFESSIONAL and OTHER only. A candidate may name a pastor, a
    /// lecturer or a family friend; they may not name their mother, and a referee list that let
    /// them would be worth nothing to the people reading it.</para>
    ///
    /// <para>A null id leaves the typed words standing — a tie nobody has catalogued may still be
    /// typed, and every referee recorded before the catalogue keeps its wording.</para>
    /// </remarks>
    private async Task<string> ResolveRefereeRelationshipAsync(
        Guid? relationshipTypeId, string typed, CancellationToken cancellationToken)
    {
        if (relationshipTypeId is not Guid id) return typed;

        var tenantId = GetTenantId();
        var type = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.RelationshipType>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Relationship type '{id}' was not found.");

        if (!type.IsActive)
            throw new InvalidOperationException(
                $"Relationship type '{type.Name}' is retired and cannot be chosen for a new record.");

        if (type.Category == RelationshipCategory.Familial)
            throw new InvalidOperationException(
                $"'{type.Name}' is a familial relationship, and a referee accepts professional and "
                + "other ones.");

        return type.Name;
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
        var entities = await _candidateRepository.GetTalentPoolCandidatesAsync(GetTenantId());
        return entities.ToSummaryDtoList();
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

    // The flat add/remove pair and the rich pair are two doors onto the same state and must
    // write it identically — the flat remove used to leave the status Active on a candidate no
    // longer in the pool, so the old screen and the pool screen disagreed about the same row.

    public async Task<bool> AddToTalentPoolAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);
        ApplyPoolEntry(entity, updatedByUserId);
        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveFromTalentPoolAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);
        ApplyPoolExit(entity, reason: null, updatedByUserId);
        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddToTalentPoolRichAsync(Guid candidateId, AddToTalentPoolDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);
        var tenantId = GetTenantId();

        ApplyPoolEntry(entity, updatedByUserId);
        entity.TalentPoolSource = dto.Source;
        entity.TalentPoolNotes = dto.Notes;
        entity.TalentPoolReviewDate = dto.ReviewDate;
        await _candidateRepository.UpdateAsync(entity);

        // SegmentIds was declared on this DTO from the start and silently dropped; a UI that
        // sent segments on add lost them without an error.
        foreach (var segmentId in dto.SegmentIds.Distinct())
        {
            var segment = await _segmentRepository.GetByIdAsync(segmentId);
            if (segment == null || segment.TenantId != tenantId || segment.IsDeleted)
                throw new ArgumentException($"Talent segment '{segmentId}' not found.");

            var existing = await _segmentMembershipRepository.GetByCandidateAndSegmentAsync(candidateId, segmentId);
            if (existing != null) continue;

            await _segmentMembershipRepository.AddAsync(new CandidateSegmentMembership
            {
                TenantId          = tenantId,
                JobCandidateId    = candidateId,
                SegmentId         = segmentId,
                AddedByEmployeeId = updatedByUserId,
                AddedDate         = DateTime.UtcNow,
                CreatedAt         = DateTime.UtcNow,
                CreatedBy         = updatedByUserId.ToString()
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveFromTalentPoolRichAsync(Guid candidateId, RemoveFromTalentPoolDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);

        ApplyPoolExit(entity, dto.Reason, updatedByUserId);
        entity.TalentPoolNotes = string.IsNullOrWhiteSpace(dto.Notes) ? entity.TalentPoolNotes : dto.Notes;

        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ApplyPoolEntry(JobCandidate entity, Guid updatedByUserId)
    {
        entity.IsInTalentPool = true;
        entity.TalentPoolAddedDate = DateTime.UtcNow;
        entity.TalentPoolStatus = ErpSystem.Core.Enums.TalentPoolCandidateStatus.Active;
        // A re-added candidate is not still carrying the record of their removal.
        entity.TalentPoolRemovedDate = null;
        entity.TalentPoolRemovalReason = null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
    }

    private static void ApplyPoolExit(JobCandidate entity, string? reason, Guid updatedByUserId)
    {
        entity.IsInTalentPool = false;
        entity.TalentPoolStatus = ErpSystem.Core.Enums.TalentPoolCandidateStatus.Expired;
        entity.TalentPoolRemovedDate = DateTime.UtcNow;
        entity.TalentPoolRemovalReason = reason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
    }

    public async Task<TalentPoolPagedResultDto> GetTalentPoolFilteredAsync(TalentPoolFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _candidateRepository.GetTalentPoolFilteredAsync(filter, GetTenantId(), cancellationToken);
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var candidates = (await _candidateRepository.GetTalentPoolCandidatesAsync(current)).ToList();
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

        // BySegment was declared on the DTO and never populated — the dashboard's segment
        // chart rendered empty for as long as this endpoint has existed.
        analytics.BySegment = candidates
            .SelectMany(c => c.SegmentMemberships ?? Enumerable.Empty<CandidateSegmentMembership>())
            .Where(m => !m.IsDeleted && m.Segment != null)
            .GroupBy(m => m.SegmentId)
            .Select(g => new TalentPoolSegmentBreakdownDto
            {
                SegmentId    = g.Key,
                SegmentName  = g.First().Segment.Name,
                SegmentColor = g.First().Segment.Color,
                Count        = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        return analytics;
    }

    public async Task<RecruitmentBulkOperationResultDto> BulkTalentPoolOperationAsync(BulkTalentPoolOperationDto dto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // A malformed operation refuses the whole request rather than reporting per-row success
        // on a switch that matched nothing — the first cut counted `Succeeded` for every row of
        // an AssignSegment with no SegmentId, having assigned nothing.
        var needsSegment = dto.Operation is ErpSystem.Core.Enums.BulkTalentPoolOperation.AssignSegment
                                          or ErpSystem.Core.Enums.BulkTalentPoolOperation.RemoveSegment;
        if (needsSegment && !dto.SegmentId.HasValue)
            throw new InvalidOperationException($"Operation '{dto.Operation}' requires a segmentId.");
        if (dto.Operation == ErpSystem.Core.Enums.BulkTalentPoolOperation.SetStatus && !dto.Status.HasValue)
            throw new InvalidOperationException("Operation 'SetStatus' requires a status.");

        if (needsSegment)
        {
            var segment = await _segmentRepository.GetByIdAsync(dto.SegmentId!.Value);
            if (segment == null || segment.TenantId != current || segment.IsDeleted)
                throw new ArgumentException($"Talent segment '{dto.SegmentId}' not found.");
        }

        var result = new RecruitmentBulkOperationResultDto();
        foreach (var id in dto.CandidateIds)
        {
            try
            {
                var entity = await _candidateRepository.GetByIdAsync(id);
                if (entity == null || entity.TenantId != current)
                {
                    result.Skipped++;
                    result.Results.Add(new RecruitmentBulkOperationItemResult
                        { CandidateId = id, Success = false, Message = "Candidate not found." });
                    continue;
                }

                switch (dto.Operation)
                {
                    case ErpSystem.Core.Enums.BulkTalentPoolOperation.AssignSegment:
                    {
                        var existing = await _segmentMembershipRepository
                            .GetByCandidateAndSegmentAsync(id, dto.SegmentId!.Value);
                        if (existing is null)
                        {
                            var membership = new CandidateSegmentMembership
                            {
                                Id                 = Guid.NewGuid(),
                                JobCandidateId     = id,
                                SegmentId          = dto.SegmentId.Value,
                                TenantId           = current,
                                AddedByEmployeeId  = updatedByUserId,
                                AddedDate          = DateTime.UtcNow,
                                Notes              = dto.Notes,
                                CreatedAt          = DateTime.UtcNow,
                                CreatedBy          = updatedByUserId.ToString()
                            };
                            await _segmentMembershipRepository.AddAsync(membership);
                        }
                        break;
                    }
                    case ErpSystem.Core.Enums.BulkTalentPoolOperation.RemoveSegment:
                    {
                        var existing = await _segmentMembershipRepository
                            .GetByCandidateAndSegmentAsync(id, dto.SegmentId!.Value);
                        if (existing is not null)
                            await _segmentMembershipRepository.DeleteAsync(existing);
                        break;
                    }
                    case ErpSystem.Core.Enums.BulkTalentPoolOperation.SetStatus:
                        entity.TalentPoolStatus = dto.Status!.Value;
                        break;
                    case ErpSystem.Core.Enums.BulkTalentPoolOperation.RemoveFromPool:
                        ApplyPoolExit(entity, dto.Notes, updatedByUserId);
                        break;
                }

                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = updatedByUserId.ToString();
                await _candidateRepository.UpdateAsync(entity);
                result.Succeeded++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                    { CandidateId = id, Success = true });
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                    { CandidateId = id, Success = false, Message = ex.Message });
            }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<List<TalentPoolVacancyMatchResultDto>> MatchToVacancyAsync(Guid vacancyId, int topN, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        // The same 40/30/20 rubric MatchCandidateToVacanciesAsync runs in the other direction —
        // this used to be a stub returning MatchScore 0 for everyone, which is worse than no
        // score because a column of zeros reads as "nobody fits".
        var results = new List<TalentPoolVacancyMatchResultDto>();
        foreach (var c in (await _candidateRepository.GetTalentPoolCandidatesAsync(tenantId))
                     .Where(c => c.TalentPoolStatus == ErpSystem.Core.Enums.TalentPoolCandidateStatus.Active))
        {
            var score   = 0;
            var reasons = new List<string>();

            // Experience match (+40)
            if (vacancy.RequiredMinExperienceYears is null ||
                (c.TotalYearsExperience.HasValue &&
                 c.TotalYearsExperience.Value >= vacancy.RequiredMinExperienceYears.Value))
            {
                score += 40;
                reasons.Add(vacancy.RequiredMinExperienceYears is null
                    ? "No minimum experience required"
                    : $"Meets experience requirement ({vacancy.RequiredMinExperienceYears} yr)");
            }

            // Work mode match (+30) — PreferredWorkArrangement.Any always matches
            if (c.PreferredWorkArrangement == ErpSystem.Core.Enums.PreferredWorkArrangement.Any ||
                c.PreferredWorkArrangement.ToString() == vacancy.WorkMode.ToString())
            {
                score += 30;
                reasons.Add(c.PreferredWorkArrangement == ErpSystem.Core.Enums.PreferredWorkArrangement.Any
                    ? "Open to any work arrangement"
                    : $"Work mode match ({vacancy.WorkMode})");
            }

            // Availability (+20)
            if (c.AvailableFrom is null || c.AvailableFrom.Value <= now)
            {
                score += 20;
                reasons.Add("Available now");
            }
            else
            {
                reasons.Add($"Available from {c.AvailableFrom.Value:dd MMM yyyy}");
            }

            if (reasons.Count == 0)
                reasons.Add("Active pool member");

            results.Add(new TalentPoolVacancyMatchResultDto
            {
                CandidateId = c.Id,
                CandidateName = c.FullName,
                CandidateNumber = c.CandidateNumber,
                Headline = c.Headline,
                TotalYearsExperience = c.TotalYearsExperience,
                PreferredWorkArrangementName = c.PreferredWorkArrangement.ToString(),
                AvailableFrom = c.AvailableFrom,
                MatchScore = score,
                MatchReasons = reasons
            });
        }

        return results
            .OrderByDescending(r => r.MatchScore)
            .ThenByDescending(r => r.TotalYearsExperience)
            .Take(topN)
            .ToList();
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
        entity.Relationship = await ResolveRefereeRelationshipAsync(
            entity.RelationshipTypeId, entity.Relationship, cancellationToken);
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
        entity.Relationship = await ResolveRefereeRelationshipAsync(
            entity.RelationshipTypeId, entity.Relationship, cancellationToken);
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
