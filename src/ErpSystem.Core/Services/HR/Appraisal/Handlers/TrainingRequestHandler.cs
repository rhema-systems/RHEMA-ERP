using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Handlers;

/// <summary>
/// Theme 10 handler. On approval of a TrainingNomination recommendation, raises a container-free
/// <see cref="TrainingRequest"/> for the appraised employee (a "needs training" request that HR/L&amp;D
/// can later match to a program/schedule) — no specific session required.
/// </summary>
public class TrainingRequestHandler : IOutcomeRecommendationHandler
{
    private readonly IGenericRepository<TrainingRequest> _requestRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingRequestHandler> _logger;

    public TrainingRequestHandler(
        IGenericRepository<TrainingRequest> requestRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingRequestHandler> logger)
    {
        _requestRepository = requestRepository;
        _appraisalRepository = appraisalRepository;
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

    public RecommendationType Type => RecommendationType.TrainingNomination;

    public async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireCurrentTenant(recommendation.TenantId);
        var appraisal = await _appraisalRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == recommendation.PerformanceAppraisalId && a.TenantId == tenantId, cancellationToken);
        if (appraisal == null)
        {
            _logger.LogWarning("TrainingRequestHandler: appraisal {Id} not found", recommendation.PerformanceAppraisalId);
            return null;
        }

        // Deterministic request number keyed on the recommendation so a re-dispatch (worklist Retry)
        // of an Approved-but-not-Actioned recommendation reuses the existing request instead of
        // creating a duplicate. (Fits the 50-char RequestNumber column: "TR-APR-" + 32-char GUID.)
        var requestNumber = $"TR-APR-{recommendation.Id:N}";

        var existing = await _requestRepository.GetQueryable()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.RequestNumber == requestNumber, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("Training: existing request {RequestNumber} reused for recommendation {Id}", requestNumber, recommendation.Id);
            return ("TrainingRequest", existing.Id);
        }

        var title = !string.IsNullOrWhiteSpace(recommendation.Notes)
            ? Truncate(recommendation.Notes!, 200)
            : "Development training (from appraisal review)";

        var entity = new TrainingRequest
        {
            TenantId = tenantId,
            RequestNumber = requestNumber,
            EmployeeId = appraisal.EmployeeId,
            RequestedTrainingTitle = title,
            Description = recommendation.Notes,
            Justification = "Raised from an appraisal outcome recommendation.",
            RequestDate = DateTime.UtcNow,
            Status = TrainingRequestStatus.Submitted
        };

        await _requestRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training: raised request {RequestNumber} for employee {EmployeeId} from appraisal {AppraisalId}",
            entity.RequestNumber, appraisal.EmployeeId, appraisal.Id);

        return ("TrainingRequest", entity.Id);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
