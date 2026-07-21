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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingRequestHandler> _logger;

    public TrainingRequestHandler(
        IGenericRepository<TrainingRequest> requestRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainingRequestHandler> logger)
    {
        _requestRepository = requestRepository;
        _appraisalRepository = appraisalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public RecommendationType Type => RecommendationType.TrainingNomination;

    public async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == recommendation.PerformanceAppraisalId, cancellationToken);
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
            .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber, cancellationToken);
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
