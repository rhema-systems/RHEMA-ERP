using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// CANDIDATE TALENT SEGMENT SERVICE
// ============================================================================

public class CandidateTalentSegmentService : ICandidateTalentSegmentService
{
    private readonly ICandidateTalentSegmentRepository _segmentRepository;
    private readonly ICandidateSegmentMembershipRepository _membershipRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CandidateTalentSegmentService> _logger;

    public CandidateTalentSegmentService(
        ICandidateTalentSegmentRepository segmentRepository,
        ICandidateSegmentMembershipRepository membershipRepository,
        IUnitOfWork unitOfWork,
        ILogger<CandidateTalentSegmentService> logger)
    {
        _segmentRepository  = segmentRepository;
        _membershipRepository = membershipRepository;
        _unitOfWork         = unitOfWork;
        _logger             = logger;
    }

    public async Task<IEnumerable<CandidateTalentSegmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = await _segmentRepository.FindAsync(s => s.TenantId == tenantId && !s.IsDeleted);
        return items.OrderBy(s => s.Name).Select(s => s.ToDto());
    }

    public async Task<IEnumerable<CandidateTalentSegmentDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = await _segmentRepository.GetActiveByTenantAsync(tenantId);
        return items.Select(s => s.ToDto());
    }

    public async Task<CandidateTalentSegmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _segmentRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Talent segment '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<CandidateTalentSegmentDto> CreateAsync(
        CreateCandidateTalentSegmentDto dto,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        if (await _segmentRepository.NameExistsAsync(tenantId, dto.Name))
            throw new InvalidOperationException($"A talent segment named '{dto.Name}' already exists.");

        var entity = dto.ToEntity(tenantId, createdByUserId);
        await _segmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<CandidateTalentSegmentDto> UpdateAsync(
        UpdateCandidateTalentSegmentDto dto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _segmentRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Talent segment '{dto.Id}' not found.");

        if (await _segmentRepository.NameExistsAsync(entity.TenantId, dto.Name, dto.Id))
            throw new InvalidOperationException($"A talent segment named '{dto.Name}' already exists.");

        entity.UpdateEntity(dto, updatedByUserId);
        await _segmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _segmentRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Talent segment '{id}' not found.");
        await _segmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CandidateSegmentMembershipDto> AddCandidateAsync(
        Guid candidateId,
        AddCandidateToSegmentDto dto,
        Guid tenantId,
        Guid addedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _membershipRepository.GetByCandidateAndSegmentAsync(candidateId, dto.SegmentId);
        if (existing != null)
            throw new InvalidOperationException("Candidate is already in this segment.");

        var membership = new CandidateSegmentMembership
        {
            TenantId            = tenantId,
            JobCandidateId      = candidateId,
            SegmentId           = dto.SegmentId,
            AddedByEmployeeId   = addedByEmployeeId,
            AddedDate           = DateTime.UtcNow,
            Notes               = dto.Notes,
            CreatedAt           = DateTime.UtcNow,
            CreatedBy           = addedByEmployeeId.ToString()
        };

        await _membershipRepository.AddAsync(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // reload with navigation for segment name
        var reloaded = await _membershipRepository.GetByCandidateAndSegmentAsync(candidateId, dto.SegmentId);
        return reloaded?.ToDto() ?? membership.ToDto();
    }

    public async Task<bool> RemoveCandidateAsync(Guid candidateId, Guid segmentId, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByCandidateAndSegmentAsync(candidateId, segmentId)
            ?? throw new ArgumentException("Candidate is not a member of this segment.");
        await _membershipRepository.DeleteAsync(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<CandidateSegmentMembershipDto>> GetCandidateSegmentsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var memberships = await _membershipRepository.GetByCandidateIdAsync(candidateId);
        return memberships.Select(m => m.ToDto());
    }
}

// ============================================================================
// CANDIDATE ENGAGEMENT EVENT SERVICE
// ============================================================================

public class CandidateEngagementEventService : ICandidateEngagementEventService
{
    private readonly ICandidateEngagementEventRepository _eventRepository;
    private readonly IJobCandidateRepository _candidateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CandidateEngagementEventService> _logger;

    public CandidateEngagementEventService(
        ICandidateEngagementEventRepository eventRepository,
        IJobCandidateRepository candidateRepository,
        IUnitOfWork unitOfWork,
        ILogger<CandidateEngagementEventService> logger)
    {
        _eventRepository    = eventRepository;
        _candidateRepository = candidateRepository;
        _unitOfWork         = unitOfWork;
        _logger             = logger;
    }

    public async Task<IEnumerable<CandidateEngagementEventDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var events = await _eventRepository.GetByCandidateIdAsync(candidateId);
        return events.Select(e => e.ToEngagementEventDto());
    }

    public async Task<CandidateEngagementEventDto> LogEventAsync(
        CreateCandidateEngagementEventDto dto,
        Guid tenantId,
        Guid recordedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var eventEntity = dto.ToEntity(tenantId, recordedByEmployeeId);
        await _eventRepository.AddAsync(eventEntity);

        // update candidate's LastEngagedDate
        var candidate = await _candidateRepository.GetByIdAsync(dto.JobCandidateId);
        if (candidate != null)
        {
            candidate.LastEngagedDate = dto.EventDate;
            candidate.UpdatedAt = DateTime.UtcNow;
            candidate.UpdatedBy = recordedByEmployeeId.ToString();
            await _candidateRepository.UpdateAsync(candidate);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return eventEntity.ToEngagementEventDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _eventRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Engagement event '{id}' not found.");
        await _eventRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
