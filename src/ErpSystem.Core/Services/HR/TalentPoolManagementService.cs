using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
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
    private readonly IJobCandidateRepository _candidateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CandidateTalentSegmentService> _logger;

    public CandidateTalentSegmentService(
        ICandidateTalentSegmentRepository segmentRepository,
        ICandidateSegmentMembershipRepository membershipRepository,
        IJobCandidateRepository candidateRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CandidateTalentSegmentService> logger)
    {
        _segmentRepository  = segmentRepository;
        _membershipRepository = membershipRepository;
        _candidateRepository = candidateRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork         = unitOfWork;
        _logger             = logger;
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

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        if (tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    // A segment owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<CandidateTalentSegment> GetOwnedSegmentAsync(Guid id)
    {
        var entity = await _segmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent segment '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<CandidateTalentSegmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var current = RequireCurrentTenant(tenantId);
        var items = await _segmentRepository.FindAsync(s => s.TenantId == current && !s.IsDeleted);
        return items.OrderBy(s => s.Name).Select(s => s.ToDto());
    }

    public async Task<IEnumerable<CandidateTalentSegmentDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var current = RequireCurrentTenant(tenantId);
        var items = await _segmentRepository.GetActiveByTenantAsync(current);
        return items.Select(s => s.ToDto());
    }

    public async Task<CandidateTalentSegmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(id);
        return entity.ToDto();
    }

    public async Task<CandidateTalentSegmentDto> CreateAsync(
        CreateCandidateTalentSegmentDto dto,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var current = RequireCurrentTenant(tenantId);

        if (await _segmentRepository.NameExistsAsync(current, dto.Name))
            throw new InvalidOperationException($"A talent segment named '{dto.Name}' already exists.");

        var entity = dto.ToEntity(current, createdByUserId);
        await _segmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<CandidateTalentSegmentDto> UpdateAsync(
        UpdateCandidateTalentSegmentDto dto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(dto.Id);

        if (await _segmentRepository.NameExistsAsync(entity.TenantId, dto.Name, dto.Id))
            throw new InvalidOperationException($"A talent segment named '{dto.Name}' already exists.");

        entity.UpdateEntity(dto, updatedByUserId);
        await _segmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(id);
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
        var current = RequireCurrentTenant(tenantId);
        await GetOwnedSegmentAsync(dto.SegmentId);

        // The segment was validated and the candidate was not — an unvalidated FK write that
        // let a membership row name a foreign tenant's candidate (or 500 on a nonexistent one).
        var candidate = await _candidateRepository.GetByIdAsync(candidateId);
        if (candidate == null || candidate.TenantId != current)
            throw new ArgumentException($"Candidate '{candidateId}' not found.");

        var existing = await _membershipRepository.GetByCandidateAndSegmentAsync(candidateId, dto.SegmentId);
        if (existing != null && existing.TenantId == current)
            throw new InvalidOperationException("Candidate is already in this segment.");

        var membership = new CandidateSegmentMembership
        {
            TenantId            = current,
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

        // Reload with the Segment navigation so the response carries the segment name — the
        // repo method's Include is what makes this reload do anything.
        var reloaded = await _membershipRepository.GetByCandidateAndSegmentAsync(candidateId, dto.SegmentId);
        return reloaded?.ToDto() ?? membership.ToDto();
    }

    public async Task<bool> RemoveCandidateAsync(Guid candidateId, Guid segmentId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSegmentAsync(segmentId);

        var membership = await _membershipRepository.GetByCandidateAndSegmentAsync(candidateId, segmentId);
        if (membership == null || membership.TenantId != GetTenantId())
            throw new ArgumentException("Candidate is not a member of this segment.");

        await _membershipRepository.DeleteAsync(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<CandidateSegmentMembershipDto>> GetCandidateSegmentsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var memberships = await _membershipRepository.GetByCandidateIdAsync(candidateId);
        return memberships.Where(m => m.TenantId == tenantId).Select(m => m.ToDto());
    }
}

// ============================================================================
// CANDIDATE ENGAGEMENT EVENT SERVICE
// ============================================================================

public class CandidateEngagementEventService : ICandidateEngagementEventService
{
    private readonly ICandidateEngagementEventRepository _eventRepository;
    private readonly IJobCandidateRepository _candidateRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CandidateEngagementEventService> _logger;

    public CandidateEngagementEventService(
        ICandidateEngagementEventRepository eventRepository,
        IJobCandidateRepository candidateRepository,
        IEmployeeRepository employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CandidateEngagementEventService> logger)
    {
        _eventRepository    = eventRepository;
        _candidateRepository = candidateRepository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork         = unitOfWork;
        _logger             = logger;
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

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        if (tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    // An engagement event owned by another tenant is reported as missing rather than forbidden.
    private async Task<CandidateEngagementEvent> GetOwnedEventAsync(Guid id)
    {
        var entity = await _eventRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Engagement event '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<CandidateEngagementEventDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var candidate = await _candidateRepository.GetByIdAsync(candidateId);
        if (candidate == null || candidate.TenantId != tenantId)
            throw new ArgumentException($"Candidate '{candidateId}' not found.");

        var events = (await _eventRepository.GetByCandidateIdAsync(candidateId))
            .Where(e => e.TenantId == tenantId)
            .ToList();

        // RecordedByEmployeeId has no navigation, so the recorders are resolved in one query —
        // without this the timeline could never say who logged a contact.
        var recorderNames = await ResolveRecorderNamesAsync(events.Select(e => e.RecordedByEmployeeId));
        return events.Select(e => e.ToEngagementEventDto(
            candidate.FullName,
            recorderNames.GetValueOrDefault(e.RecordedByEmployeeId)));
    }

    public async Task<CandidateEngagementEventDto> LogEventAsync(
        CreateCandidateEngagementEventDto dto,
        Guid tenantId,
        Guid recordedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var current = RequireCurrentTenant(tenantId);

        // Validate the candidate BEFORE writing the event: the first cut created the row
        // unconditionally and only skipped the LastEngagedDate update when the candidate was
        // foreign or missing — an unvalidated FK write.
        var candidate = await _candidateRepository.GetByIdAsync(dto.JobCandidateId);
        if (candidate == null || candidate.TenantId != current)
            throw new ArgumentException($"Candidate '{dto.JobCandidateId}' not found.");

        var eventEntity = dto.ToEntity(current, recordedByEmployeeId);
        await _eventRepository.AddAsync(eventEntity);

        candidate.LastEngagedDate = dto.EventDate;
        candidate.UpdatedAt = DateTime.UtcNow;
        candidate.UpdatedBy = recordedByEmployeeId.ToString();
        await _candidateRepository.UpdateAsync(candidate);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var recorderNames = await ResolveRecorderNamesAsync(new[] { recordedByEmployeeId });
        return eventEntity.ToEngagementEventDto(
            candidate.FullName,
            recorderNames.GetValueOrDefault(recordedByEmployeeId));
    }

    private async Task<Dictionary<Guid, string>> ResolveRecorderNamesAsync(IEnumerable<Guid> employeeIds)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var employees = await _employeeRepository.FindAsync(e => ids.Contains(e.Id));
        return employees.ToDictionary(e => e.Id, e => e.FullName);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(id);
        await _eventRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
