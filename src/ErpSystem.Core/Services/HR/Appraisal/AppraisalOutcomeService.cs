using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Theme 8 backbone. Tracks appraisal outcome recommendations and, on approval, dispatches
/// them to the owning module via registered <see cref="IOutcomeRecommendationHandler"/>s so
/// that approving a recommendation creates a real downstream record — not a dead-end flag.
/// </summary>
public class AppraisalOutcomeService : IAppraisalOutcomeService
{
    private readonly IGenericRepository<AppraisalOutcomeRecommendation> _repository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IEnumerable<IOutcomeRecommendationHandler> _handlers;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalOutcomeService> _logger;

    public AppraisalOutcomeService(
        IGenericRepository<AppraisalOutcomeRecommendation> repository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IEnumerable<IOutcomeRecommendationHandler> handlers,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalOutcomeService> logger)
    {
        _repository = repository;
        _appraisalRepository = appraisalRepository;
        _handlers = handlers;
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

    // A recommendation owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalOutcomeRecommendation> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Recommendation with ID '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<AppraisalOutcomeRecommendationDto>> GetByAppraisalAsync(Guid performanceAppraisalId, CancellationToken cancellationToken = default)
    {
        var items = await Query()
            .Where(r => r.PerformanceAppraisalId == performanceAppraisalId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(ToDto).ToList();
    }

    public async Task<IEnumerable<AppraisalOutcomeRecommendationDto>> GetWorklistAsync(RecommendationStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = Query();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return items.Select(ToDto).ToList();
    }

    public async Task<AppraisalOutcomeRecommendationDto> ProposeAsync(
        CreateAppraisalOutcomeRecommendationDto dto, Guid recommendedById, bool isPrivilegedActor, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Id == dto.PerformanceAppraisalId && a.TenantId == tenantId, cancellationToken);
        if (appraisal == null)
            throw new ArgumentException($"Performance appraisal with ID '{dto.PerformanceAppraisalId}' not found.");

        // A withdrawn appraisal has no result to act on (performance closure E-d1); the withdrawal
        // dismissed whatever was proposed on it.
        if (appraisal.Status == AppraisalStatus.Withdrawn)
            throw new InvalidOperationException(
                "This appraisal was withdrawn from its cycle, so no outcome is proposed on it.");

        // A recommendation is the front half of a promotion, a demotion or a termination — the
        // handler turns an approved one into a real intake record. Anyone authenticated could
        // previously raise one against anyone's appraisal.
        if (!isPrivilegedActor && appraisal.Employee?.ManagerId != recommendedById)
            throw new UnauthorizedAccessException(
                "Only this employee's manager, or HR, can propose an outcome for their appraisal.");

        var entity = new AppraisalOutcomeRecommendation
        {
            TenantId = tenantId,
            PerformanceAppraisalId = dto.PerformanceAppraisalId,
            RecommendationType = dto.RecommendationType,
            Status = RecommendationStatus.Proposed,
            RecommendedById = recommendedById == Guid.Empty ? null : recommendedById,
            RecommendedDate = DateTime.UtcNow,
            Notes = dto.Notes
        };

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Outcome recommendation {Type} proposed for appraisal {AppraisalId}", dto.RecommendationType, dto.PerformanceAppraisalId);
        return ToDto(await GetEntityAsync(entity.Id, cancellationToken));
    }

    public async Task<AppraisalOutcomeRecommendationDto> ApproveAsync(Guid id, Guid approverId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status is RecommendationStatus.Rejected or RecommendationStatus.Dismissed)
            throw new InvalidOperationException("This recommendation has already been closed.");

        // Idempotency: if already actioned (downstream record exists), don't create another.
        if (entity.Status == RecommendationStatus.Actioned && entity.TargetEntityId.HasValue)
            return ToDto(await GetEntityAsync(id, cancellationToken));

        // Approved once (performance closure E-g1, D-80): a second approval re-stamped the approver and the date over the
        // first, and dispatched again. An approval whose record was not created is retried, not re-approved.
        if (entity.Status == RecommendationStatus.Approved)
            throw new InvalidOperationException(
                "This recommendation is already approved. If its downstream record was not created, retry the dispatch.");

        entity.Status = RecommendationStatus.Approved;
        entity.ApprovedById = approverId == Guid.Empty ? null : approverId;
        entity.ApprovedDate = DateTime.UtcNow;

        await DispatchAsync(entity, cancellationToken);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(await GetEntityAsync(id, cancellationToken));
    }

    public async Task<AppraisalOutcomeRecommendationDto> RetryDispatchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != RecommendationStatus.Approved)
            throw new InvalidOperationException(
                entity.Status == RecommendationStatus.Actioned
                    ? "This recommendation has already created its downstream record."
                    : $"Only an approved recommendation can be dispatched. This one is {entity.Status}.");

        await DispatchAsync(entity, cancellationToken);

        if (entity.Status != RecommendationStatus.Actioned)
            throw new InvalidOperationException(
                "The owning module could not create the downstream record. Check the server log and action it there directly.");

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(await GetEntityAsync(id, cancellationToken));
    }

    /// <summary>
    /// Hands the recommendation to the module that owns its type. A handler failure leaves the
    /// row Approved-but-not-Actioned rather than throwing, so the approval still stands and the
    /// worklist can surface it for a retry; the handlers reuse an existing downstream record, so
    /// a retry cannot double up.
    /// </summary>
    private async Task DispatchAsync(AppraisalOutcomeRecommendation entity, CancellationToken cancellationToken)
    {
        var handler = _handlers.FirstOrDefault(h => h.Type == entity.RecommendationType);
        if (handler == null)
        {
            _logger.LogInformation(
                "No handler registered for {Type}; recommendation {Id} approved but not auto-actioned.",
                entity.RecommendationType, entity.Id);
            return;
        }

        try
        {
            var result = await handler.HandleAsync(entity, cancellationToken);
            if (result.HasValue)
            {
                entity.TargetEntityType = result.Value.TargetEntityType;
                entity.TargetEntityId = result.Value.TargetEntityId;
                entity.Status = RecommendationStatus.Actioned;
                entity.ActionedDate = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler for {Type} failed to action recommendation {Id}", entity.RecommendationType, entity.Id);
        }
    }

    public Task<AppraisalOutcomeRecommendationDto> RejectAsync(Guid id, Guid reviewerId, string? notes, CancellationToken cancellationToken = default)
        => CloseAsync(id, RecommendationStatus.Rejected, reviewerId, notes, cancellationToken);

    public Task<AppraisalOutcomeRecommendationDto> DismissAsync(Guid id, Guid reviewerId, string? notes, CancellationToken cancellationToken = default)
        => CloseAsync(id, RecommendationStatus.Dismissed, reviewerId, notes, cancellationToken);

    private async Task<AppraisalOutcomeRecommendationDto> CloseAsync(Guid id, RecommendationStatus status, Guid reviewerId, string? notes, CancellationToken cancellationToken)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status == RecommendationStatus.Actioned)
            throw new InvalidOperationException("An actioned recommendation cannot be rejected or dismissed.");
        // Decided once (E-g1, D-80): closing a closed one again overwrote who decided it, when, and why.
        if (entity.Status is RecommendationStatus.Rejected or RecommendationStatus.Dismissed)
            throw new InvalidOperationException(
                $"This recommendation was already {entity.Status.ToString().ToLowerInvariant()} on {entity.ApprovedDate:d MMM yyyy}.");

        entity.Status = status;
        entity.ApprovedById = reviewerId == Guid.Empty ? null : reviewerId;
        entity.ApprovedDate = DateTime.UtcNow;
        entity.ResolutionNotes = notes;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recommendation {Id} set to {Status}", id, status);
        return ToDto(await GetEntityAsync(id, cancellationToken));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private IQueryable<AppraisalOutcomeRecommendation> Query()
    {
        var tenantId = GetTenantId();
        return _repository.GetQueryable()
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.PerformanceAppraisal)
                .ThenInclude(a => a.Employee);
    }

    private async Task<AppraisalOutcomeRecommendation> GetEntityAsync(Guid id, CancellationToken cancellationToken)
        => await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
           ?? throw new ArgumentException($"Recommendation with ID '{id}' not found.");

    private static AppraisalOutcomeRecommendationDto ToDto(AppraisalOutcomeRecommendation r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        PerformanceAppraisalId = r.PerformanceAppraisalId,
        AppraisalNumber = r.PerformanceAppraisal?.AppraisalNumber,
        EmployeeName = r.PerformanceAppraisal?.Employee?.FullName,
        RecommendationType = r.RecommendationType,
        Status = r.Status,
        RecommendedById = r.RecommendedById,
        RecommendedDate = r.RecommendedDate,
        ApprovedById = r.ApprovedById,
        ApprovedDate = r.ApprovedDate,
        ActionedDate = r.ActionedDate,
        Notes = r.Notes,
        ResolutionNotes = r.ResolutionNotes,
        TargetEntityType = r.TargetEntityType,
        TargetEntityId = r.TargetEntityId
    };
}
