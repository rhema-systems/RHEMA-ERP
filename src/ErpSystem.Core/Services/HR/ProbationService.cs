using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class ProbationService : IProbationService
{
    private readonly IProbationPeriodRepository _probationRepository;
    private readonly IProbationReviewRepository _reviewRepository;
    private readonly IProbationExtensionRepository _extensionRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProbationService> _logger;

    public ProbationService(
        IProbationPeriodRepository probationRepository,
        IProbationReviewRepository reviewRepository,
        IProbationExtensionRepository extensionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ProbationService> logger)
    {
        _probationRepository = probationRepository;
        _reviewRepository = reviewRepository;
        _extensionRepository = extensionRepository;
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
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    // A probation period owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<ProbationPeriod> GetOwnedProbationAsync(Guid id)
    {
        var entity = await _probationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Probation record with ID '{id}' not found.");
        return entity;
    }

    // A review owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<ProbationReview> GetOwnedReviewAsync(Guid id)
    {
        var entity = await _reviewRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Probation review with ID '{id}' not found.");
        return entity;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<ProbationPeriodDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(id);
        return entity.ToDto();
    }

    public async Task<ProbationPeriodDto?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _probationRepository.GetByEmployeeIdAsync(employeeId);
        var scoped = entities.Where(p => p.TenantId == tenantId).ToList();
        var active = scoped.FirstOrDefault(p => p.Status == ProbationStatus.Active)
            ?? scoped.OrderByDescending(p => p.StartDate).FirstOrDefault();
        return active?.ToDto();
    }

    public async Task<ProbationPeriodDetailDto> GetWithReviewsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _probationRepository.GetWithReviewsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Probation record with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<ProbationPeriodSummaryDto>> GetByStatusAsync(ProbationStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _probationRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ProbationPeriodSummaryDto>> GetActiveProbationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _probationRepository.GetActiveProbationsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ProbationPeriodSummaryDto>> GetEndingWithinAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _probationRepository.GetEndingWithinAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<ProbationPeriodDto> CreateAsync(CreateProbationPeriodDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var existing = await _probationRepository.GetByEmployeeIdAsync(createDto.EmployeeId);
        if (existing.Any(p => p.TenantId == tenantId && p.Status == ProbationStatus.Active))
            throw new InvalidOperationException("This employee already has an active probation period.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.Status = ProbationStatus.Active;

        await _probationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Probation record created for employee {EmployeeId}", createDto.EmployeeId);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(id);

        if (entity.Status != ProbationStatus.Active)
            throw new InvalidOperationException("Only active probation records can be deleted.");

        await _probationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> ExtendAsync(Guid probationId, DateTime newEndDate, string reason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(probationId);

        if (entity.Status != ProbationStatus.Active)
            throw new InvalidOperationException("Only active probations can be extended.");

        if (DateOnly.FromDateTime(newEndDate) <= entity.CurrentEndDate)
            throw new ArgumentException("New end date must be later than the current end date.");

        entity.CurrentEndDate = DateOnly.FromDateTime(newEndDate);
        entity.OutcomeNotes = reason;
        entity.ExtensionCount++;

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Probation {ProbationId} extended to {NewEndDate}", probationId, newEndDate);
        return true;
    }

    public async Task<bool> ConfirmAsync(Guid probationId, Guid confirmedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(probationId);

        if (entity.Status != ProbationStatus.Active)
            throw new InvalidOperationException("Only active probations can be confirmed.");

        entity.Status = ProbationStatus.Completed;

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Probation {ProbationId} confirmed by {UserId}", probationId, confirmedByUserId);
        return true;
    }

    public async Task<bool> TerminateAsync(TerminateProbationPeriodDto dto, Guid terminatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProbationAsync(dto.ProbationId);

        if (entity.Status != ProbationStatus.Active)
            throw new InvalidOperationException("Only active probations can be terminated.");

        entity.Status = ProbationStatus.Terminated;
        entity.OutcomeNotes = dto.Notes;

        await _probationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Probation {ProbationId} terminated", dto.ProbationId);
        return true;
    }

    // ── Reviews ───────────────────────────────────────────────────────────────

    public async Task<ProbationReviewDto> AddReviewAsync(CreateProbationReviewDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedProbationAsync(createDto.ProbationPeriodId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _reviewRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ProbationReviewDto>> GetReviewsAsync(Guid probationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedProbationAsync(probationId);
        var entities = await _reviewRepository.GetByProbationPeriodIdAsync(probationId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<ProbationReviewDto> UpdateReviewAsync(UpdateProbationReviewDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(updateDto.Id);

        if (updateDto.ScheduledDate.HasValue) entity.ScheduledDate = updateDto.ScheduledDate.Value;
        if (updateDto.SecondReviewerId.HasValue) entity.SecondReviewerId = updateDto.SecondReviewerId;
        await _reviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> CompleteReviewAsync(Guid reviewId, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(reviewId);

        entity.Status = ProbationReviewStatus.Completed;

        await _reviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<ProbationReviewDto>> GetReviewsByStatusAsync(ProbationReviewStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _reviewRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<ProbationReviewDto>> GetOverdueReviewsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _reviewRepository.GetOverdueReviewsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<ProbationReviewDto>> GetReviewsByReviewerAsync(Guid reviewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _reviewRepository.GetByReviewerIdAsync(reviewerEmployeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<ProbationExtensionDto> RecordExtensionAsync(CreateProbationExtensionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var probation = await GetOwnedProbationAsync(createDto.ProbationPeriodId);

        if (probation.Status != ProbationStatus.Active)
            throw new InvalidOperationException("Only active probation periods can be extended.");

        var newEndDate = createDto.NewEndDate;
        if (newEndDate <= probation.CurrentEndDate)
            throw new ArgumentException($"New end date ({newEndDate:d}) must be later than the current end date ({probation.CurrentEndDate:d}).");

        // Verify extension months is consistent with the stated new date
        var computedMonths = ((newEndDate.Year - probation.CurrentEndDate.Year) * 12)
                           + newEndDate.Month - probation.CurrentEndDate.Month;
        if (computedMonths < 1)
            throw new ArgumentException("Extension must be at least one full calendar month.");

        var extension = new ProbationExtension
        {
            TenantId = tenantId,
            ProbationPeriodId = createDto.ProbationPeriodId,
            PreviousEndDate = probation.CurrentEndDate,
            NewEndDate = newEndDate,
            ExtensionMonths = createDto.ExtensionMonths,
            Reason = createDto.Reason,
            ExtendedById = createdByUserId,
            ExtendedDate = DateTime.UtcNow,
            Comments = createDto.Comments,
            CreatedBy = createdByUserId.ToString(),
        };

        // Advance the probation period's end date and increment extension counter
        probation.CurrentEndDate = newEndDate;
        probation.ExtensionCount++;

        await _extensionRepository.AddAsync(extension);
        await _probationRepository.UpdateAsync(probation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Probation {ProbationId} extended by {Months} month(s) to {NewEndDate} by {UserId}",
            probation.Id, createDto.ExtensionMonths, newEndDate, createdByUserId);

        return new ProbationExtensionDto
        {
            Id = extension.Id,
            CreatedAt = extension.CreatedAt,
            CreatedBy = extension.CreatedBy,
            ProbationPeriodId = extension.ProbationPeriodId,
            PreviousEndDate = extension.PreviousEndDate,
            NewEndDate = extension.NewEndDate,
            ExtensionMonths = extension.ExtensionMonths,
            Reason = extension.Reason,
            ExtendedById = extension.ExtendedById,
            ExtendedDate = extension.ExtendedDate,
            Comments = extension.Comments,
        };
    }

    public async Task<IEnumerable<ProbationExtensionDto>> GetExtensionsAsync(Guid probationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedProbationAsync(probationId);
        var extensions = await _extensionRepository.GetByProbationIdAsync(probationId);
        return extensions.Where(e => e.TenantId == tenantId).Select(e => new ProbationExtensionDto
        {
            Id = e.Id,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy ?? string.Empty,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy,
            ProbationPeriodId = e.ProbationPeriodId,
            PreviousEndDate = e.PreviousEndDate,
            NewEndDate = e.NewEndDate,
            ExtensionMonths = e.ExtensionMonths,
            Reason = e.Reason,
            ExtendedById = e.ExtendedById,
            ExtendedDate = e.ExtendedDate,
            Comments = e.Comments,
        }).ToList();
    }
}
