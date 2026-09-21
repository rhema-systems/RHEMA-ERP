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
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IGenericRepository<EmployeePosition> _positionRepository;
    private readonly IGenericRepository<JobFamily> _jobFamilyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CandidateTalentSegmentService> _logger;

    public CandidateTalentSegmentService(
        ICandidateTalentSegmentRepository segmentRepository,
        ICandidateSegmentMembershipRepository membershipRepository,
        IJobCandidateRepository candidateRepository,
        IEmployeeRepository employeeRepository,
        IGenericRepository<EmployeePosition> positionRepository,
        IGenericRepository<JobFamily> jobFamilyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CandidateTalentSegmentService> logger)
    {
        _segmentRepository  = segmentRepository;
        _membershipRepository = membershipRepository;
        _candidateRepository = candidateRepository;
        _employeeRepository = employeeRepository;
        _positionRepository = positionRepository;
        _jobFamilyRepository = jobFamilyRepository;
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
    // ⚠ Reads the full graph (lane V): every caller that maps the result needs Memberships and the
    // three ownership navigations, and a bare GetByIdAsync answered MemberCount 0 and nameless
    // owners on create, update and the detail read alike.
    private async Task<CandidateTalentSegment> GetOwnedSegmentAsync(Guid id)
    {
        var entity = await _segmentRepository.GetDetailAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent segment '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// Round 3, lane V (D-6). The owner, the target position and the job family are optional, but
    /// a named one must be this tenant's live row — an unvalidated FK write is how a segment could
    /// have pointed at another tenant's employee, and how a bad id became a 500 rather than a
    /// sentence. 422 through <c>RecruitmentBusinessRules</c>.
    /// </summary>
    private async Task RequireOwnershipTargetsAsync(Guid? ownerEmployeeId, Guid? targetPositionId, Guid? jobFamilyId)
    {
        var tenantId = GetTenantId();

        if (ownerEmployeeId is { } ownerId)
        {
            var owner = await _employeeRepository.GetByIdAsync(ownerId);
            if (owner == null || owner.TenantId != tenantId || owner.IsDeleted)
                throw new InvalidOperationException("That owner is not an employee of this organisation. Pick one from the employee register.");
        }

        if (targetPositionId is { } positionId)
        {
            var position = await _positionRepository.GetByIdAsync(positionId);
            if (position == null || position.TenantId != tenantId || position.IsDeleted)
                throw new InvalidOperationException("That target position is not one of this organisation's positions.");
        }

        if (jobFamilyId is { } familyId)
        {
            var family = await _jobFamilyRepository.GetByIdAsync(familyId);
            if (family == null || family.TenantId != tenantId || family.IsDeleted)
                throw new InvalidOperationException("That job family is not one of this organisation's job families.");
        }
    }

    public async Task<IEnumerable<CandidateTalentSegmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var current = RequireCurrentTenant(tenantId);
        var items = await _segmentRepository.GetForTenantAsync(current, activeOnly: false);
        return items.Select(s => s.ToDto());
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

        await RequireOwnershipTargetsAsync(dto.OwnerEmployeeId, dto.TargetPositionId, dto.JobFamilyId);

        var entity = dto.ToEntity(current, createdByUserId);
        await _segmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Re-read so the response carries the owner / position / family names the form just set.
        return (await _segmentRepository.GetDetailAsync(entity.Id))?.ToDto() ?? entity.ToDto();
    }

    public async Task<CandidateTalentSegmentDto> UpdateAsync(
        UpdateCandidateTalentSegmentDto dto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(dto.Id);

        if (await _segmentRepository.NameExistsAsync(entity.TenantId, dto.Name, dto.Id))
            throw new InvalidOperationException($"A talent segment named '{dto.Name}' already exists.");

        await RequireOwnershipTargetsAsync(dto.OwnerEmployeeId, dto.TargetPositionId, dto.JobFamilyId);

        entity.UpdateEntity(dto, updatedByUserId);
        await _segmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await _segmentRepository.GetDetailAsync(entity.Id))?.ToDto() ?? entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(id);

        // Lane V. The delete is soft and the membership rows are not swept with it, so a deleted
        // segment used to keep its members — and keep appearing on their candidate records. The
        // screen already tells the user the other way out, so say the same thing here.
        var members = await _segmentRepository.CountLiveMembersAsync(id);
        if (members > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' still has {members} candidate{(members == 1 ? string.Empty : "s")} in it. " +
                "Remove them first, or deactivate the segment to retire it from the pickers.");

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
        var segment = await GetOwnedSegmentAsync(dto.SegmentId);

        // Lane V: "deactivate one to retire it from pickers" only holds if the door agrees — the
        // add accepted a retired segment, so anything calling it directly (the bulk operation
        // included) could keep filling a segment the recruiter had put away.
        if (!segment.IsActive)
            throw new InvalidOperationException($"'{segment.Name}' has been deactivated, so candidates cannot be added to it.");

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
