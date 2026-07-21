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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalOutcomeService> _logger;

    public AppraisalOutcomeService(
        IGenericRepository<AppraisalOutcomeRecommendation> repository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IEnumerable<IOutcomeRecommendationHandler> handlers,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalOutcomeService> logger)
    {
        _repository = repository;
        _appraisalRepository = appraisalRepository;
        _handlers = handlers;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

    public async Task<AppraisalOutcomeRecommendationDto> ProposeAsync(CreateAppraisalOutcomeRecommendationDto dto, Guid recommendedById, CancellationToken cancellationToken = default)
    {
        var entity = new AppraisalOutcomeRecommendation
        {
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
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Recommendation with ID '{id}' not found.");

        if (entity.Status is RecommendationStatus.Rejected or RecommendationStatus.Dismissed)
            throw new InvalidOperationException("This recommendation has already been closed.");

        // Idempotency: if already actioned (downstream record exists), don't create another.
        if (entity.Status == RecommendationStatus.Actioned && entity.TargetEntityId.HasValue)
            return ToDto(await GetEntityAsync(id, cancellationToken));

        entity.Status = RecommendationStatus.Approved;
        entity.ApprovedById = approverId == Guid.Empty ? null : approverId;
        entity.ApprovedDate = DateTime.UtcNow;

        // Dispatch to the owning module to create the real downstream record.
        var handler = _handlers.FirstOrDefault(h => h.Type == entity.RecommendationType);
        if (handler != null)
        {
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
                // Leave Approved-but-not-Actioned so the worklist surfaces it for manual follow-up.
                _logger.LogError(ex, "Handler for {Type} failed to action recommendation {Id}", entity.RecommendationType, id);
            }
        }
        else
        {
            _logger.LogInformation("No handler registered for {Type}; recommendation {Id} approved but not auto-actioned.", entity.RecommendationType, id);
        }

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(await GetEntityAsync(id, cancellationToken));
    }

    public Task<AppraisalOutcomeRecommendationDto> RejectAsync(Guid id, Guid reviewerId, string? notes, CancellationToken cancellationToken = default)
        => CloseAsync(id, RecommendationStatus.Rejected, reviewerId, notes, cancellationToken);

    public Task<AppraisalOutcomeRecommendationDto> DismissAsync(Guid id, Guid reviewerId, string? notes, CancellationToken cancellationToken = default)
        => CloseAsync(id, RecommendationStatus.Dismissed, reviewerId, notes, cancellationToken);

    private async Task<AppraisalOutcomeRecommendationDto> CloseAsync(Guid id, RecommendationStatus status, Guid reviewerId, string? notes, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Recommendation with ID '{id}' not found.");

        if (entity.Status == RecommendationStatus.Actioned)
            throw new InvalidOperationException("An actioned recommendation cannot be rejected or dismissed.");

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
        => _repository.GetQueryable()
            .Include(r => r.PerformanceAppraisal)
                .ThenInclude(a => a.Employee);

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
