using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Handlers;

/// <summary>
/// Production-readiness Phase B handler. On approval of a PerformanceImprovementPlan recommendation,
/// creates a real <see cref="PerformanceImprovementPlan"/> draft (Active) anchored to the appraisal — the
/// PIP module is fully built, so a PIP is the natural, self-contained artifact for this outcome.
/// </summary>
public class PipRecommendationHandler : IOutcomeRecommendationHandler
{
    private readonly IGenericRepository<PerformanceImprovementPlan> _pipRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PipRecommendationHandler> _logger;

    public PipRecommendationHandler(
        IGenericRepository<PerformanceImprovementPlan> pipRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IUnitOfWork unitOfWork,
        ILogger<PipRecommendationHandler> logger)
    {
        _pipRepository = pipRepository;
        _appraisalRepository = appraisalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public RecommendationType Type => RecommendationType.PerformanceImprovementPlan;

    public async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Id == recommendation.PerformanceAppraisalId, cancellationToken);
        if (appraisal == null)
        {
            _logger.LogWarning("PipRecommendationHandler: appraisal {Id} not found", recommendation.PerformanceAppraisalId);
            return null;
        }

        // Idempotency: reuse the PIP already raised from this appraisal on a re-dispatch.
        var existing = await _pipRepository.GetQueryable()
            .FirstOrDefaultAsync(p => p.AppraisalId == appraisal.Id, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("PIP: existing plan {Id} reused for appraisal {AppraisalId}", existing.Id, appraisal.Id);
            return ("PerformanceImprovementPlan", existing.Id);
        }

        // PIP.SupervisorId is a required Employee FK. Prefer the employee's manager, then the
        // recommendation approver/recommender. If none can be resolved, leave Approved-not-Actioned.
        var supervisorId = appraisal.Employee?.ManagerId
                           ?? (recommendation.ApprovedById is { } a && a != Guid.Empty ? a : (Guid?)null)
                           ?? (recommendation.RecommendedById is { } r && r != Guid.Empty ? r : (Guid?)null);
        if (supervisorId is null)
        {
            _logger.LogWarning(
                "PipRecommendationHandler: cannot resolve a supervisor for employee {EmployeeId}; PIP not created.",
                appraisal.EmployeeId);
            return null;
        }

        var now = DateTime.UtcNow;
        var entity = new PerformanceImprovementPlan
        {
            PipNumber = $"PIP-APR-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            EmployeeId = appraisal.EmployeeId,
            AppraisalId = appraisal.Id,
            SupervisorId = supervisorId.Value,
            StartDate = now,
            EndDate = now.AddDays(90),
            Status = PipStatus.Active,
            PerformanceIssues = !string.IsNullOrWhiteSpace(recommendation.Notes)
                ? recommendation.Notes!
                : "Performance concerns identified during the appraisal review.",
            ExpectedStandards = "To be defined with the employee at the PIP kick-off.",
            ImprovementActions = "To be agreed with the employee at the PIP kick-off."
        };

        await _pipRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP {PipNumber} created for employee {EmployeeId} from appraisal {AppraisalId}",
            entity.PipNumber, appraisal.EmployeeId, appraisal.Id);

        return ("PerformanceImprovementPlan", entity.Id);
    }
}
